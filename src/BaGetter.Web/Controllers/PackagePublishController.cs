using System;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Core;
using BaGetter.Core.Authentication;
using BaGetter.Core.Configuration;
using BaGetter.Core.Feeds;
using BaGetter.Core.Indexing;
using BaGetter.Web.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace BaGetter.Web.Controllers;

public partial class PackagePublishController : Controller
{
    private readonly IAuthenticationService _authentication;
    private readonly IFeedAuthenticationService _feedAuthentication;
    private readonly IPermissionService _permissionService;
    private readonly IFeedContext _feedContext;
    private readonly IFeedSettingsResolver _feedSettings;
    private readonly IPackageIndexingService _indexer;
    private readonly IPackageDatabase _packages;
    private readonly IPackageDeletionService _deleteService;
    private readonly IOptionsSnapshot<BaGetterOptions> _options;
    private readonly ILogger<PackagePublishController> _logger;

    public PackagePublishController(
        IAuthenticationService authentication,
        IFeedAuthenticationService feedAuthentication,
        IPermissionService permissionService,
        IFeedContext feedContext,
        IFeedSettingsResolver feedSettings,
        IPackageIndexingService indexer,
        IPackageDatabase packages,
        IPackageDeletionService deletionService,
        IOptionsSnapshot<BaGetterOptions> options,
        ILogger<PackagePublishController> logger)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _feedAuthentication = feedAuthentication ?? throw new ArgumentNullException(nameof(feedAuthentication));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _feedContext = feedContext ?? throw new ArgumentNullException(nameof(feedContext));
        _feedSettings = feedSettings ?? throw new ArgumentNullException(nameof(feedSettings));
        _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _deleteService = deletionService ?? throw new ArgumentNullException(nameof(deletionService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // See: https://docs.microsoft.com/en-us/nuget/api/package-publish-resource#push-a-package
    public async Task Upload(CancellationToken cancellationToken)
    {
        if (_feedSettings.GetIsReadOnlyMode(_feedContext.CurrentFeed))
        {
            LogAudit(LogLevel.Warning, "package_upload_read_only", null, null, GetActor());
            HttpContext.Response.StatusCode = 401;
            return;
        }

        // The package is only read after authorization, so denied uploads are logged without its id and version.
        var (authorized, actor) = await AuthorizePushAsync(cancellationToken);
        if (!authorized)
        {
            LogAudit(LogLevel.Warning, "package_upload_unauthorized", null, null, actor);
            HttpContext.Response.StatusCode = 401;
            return;
        }

        try
        {
            using var uploadStream = await Request.GetUploadStreamOrNullAsync(cancellationToken);
            if (uploadStream == null)
            {
                LogAudit(LogLevel.Warning, "package_upload_invalid_package", null, null, actor);
                HttpContext.Response.StatusCode = 400;
                return;
            }

            var identity = TryReadPackageIdentity(uploadStream);
            var packageId = identity?.Id;
            var packageVersion = identity?.Version?.ToNormalizedString();

            var result = await _indexer.IndexAsync(_feedContext.CurrentFeed.Id, _feedContext.CurrentFeed.Slug, uploadStream, cacheFeedUrl: null, cancellationToken);

            switch (result)
            {
                case PackageIndexingResult.InvalidPackage:
                    LogAudit(LogLevel.Warning, "package_upload_invalid_package", packageId, packageVersion, actor);
                    HttpContext.Response.StatusCode = 400;
                    break;

                case PackageIndexingResult.PackageAlreadyExists:
                    LogAudit(LogLevel.Warning, "package_upload_already_exists", packageId, packageVersion, actor);
                    HttpContext.Response.StatusCode = 409;
                    break;

                case PackageIndexingResult.Success:
                    LogAudit(LogLevel.Information, "package_upload_succeeded", packageId, packageVersion, actor);
                    HttpContext.Response.StatusCode = 201;
                    break;
            }
        }
        catch (Exception e)
        {
            LogUploadException(e);

            HttpContext.Response.StatusCode = 500;
        }
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(string id, string version, CancellationToken cancellationToken)
    {
        if (_feedSettings.GetIsReadOnlyMode(_feedContext.CurrentFeed))
        {
            LogAudit(LogLevel.Warning, "package_delete_read_only", id, version, GetActor());
            return Unauthorized();
        }

        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            LogAudit(LogLevel.Warning, "package_delete_not_found", id, version, GetActor());
            return NotFound();
        }

        var (authorized, actor) = await AuthorizeDeleteAsync(cancellationToken);
        if (!authorized)
        {
            LogAudit(LogLevel.Warning, "package_delete_unauthorized", id, version, actor);
            return Unauthorized();
        }

        if (await _deleteService.TryDeletePackageAsync(_feedContext.CurrentFeed.Id, _feedContext.CurrentFeed.Slug, id, nugetVersion, cancellationToken))
        {
            LogAudit(LogLevel.Information, "package_delete_succeeded", id, version, actor);
            return NoContent();
        }
        else
        {
            LogAudit(LogLevel.Warning, "package_delete_not_found", id, version, actor);
            return NotFound();
        }
    }

    [HttpPost]
    public async Task<IActionResult> Relist(string id, string version, CancellationToken cancellationToken)
    {
        if (_feedSettings.GetIsReadOnlyMode(_feedContext.CurrentFeed))
        {
            LogAudit(LogLevel.Warning, "package_relist_read_only", id, version, GetActor());
            return Unauthorized();
        }

        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            LogAudit(LogLevel.Warning, "package_relist_not_found", id, version, GetActor());
            return NotFound();
        }

        var (authorized, actor) = await AuthorizePushAsync(cancellationToken);
        if (!authorized)
        {
            LogAudit(LogLevel.Warning, "package_relist_unauthorized", id, version, actor);
            return Unauthorized();
        }

        if (await _packages.RelistPackageAsync(_feedContext.CurrentFeed.Id, id, nugetVersion, cancellationToken))
        {
            LogAudit(LogLevel.Information, "package_relist_succeeded", id, version, actor);
            return Ok();
        }
        else
        {
            LogAudit(LogLevel.Warning, "package_relist_not_found", id, version, actor);
            return NotFound();
        }
    }

    private async Task<(bool Authorized, string Actor)> AuthorizePushAsync(CancellationToken cancellationToken)
    {
        var authMode = _options.Value.Authentication?.Mode ?? AuthenticationMode.Config;

        if (authMode == AuthenticationMode.Config)
        {
            // Static auth mode: use configured API key
            return (await _authentication.AuthenticateAsync(Request.GetApiKey(), cancellationToken), GetActor());
        }

        var feedId = _feedContext.CurrentFeed.Id;

        // New mode: prefer X-NuGet-ApiKey (dotnet nuget push -k <token>), fall back to
        // the user identity already established by Basic auth middleware.
        var apiKey = Request.GetApiKey();
        if (!string.IsNullOrEmpty(apiKey))
        {
            var authResult = await _feedAuthentication.AuthenticateByTokenAsync(apiKey, cancellationToken);
            if (authResult.IsAuthenticated && authResult.UserId.HasValue)
                return (await _permissionService.CanPushAsync(authResult.UserId.Value, feedId, cancellationToken), authResult.Username);
        }

        var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
            return (await _permissionService.CanPushAsync(userId, feedId, cancellationToken), GetActor());

        return (false, GetActor());
    }

    private async Task<(bool Authorized, string Actor)> AuthorizeDeleteAsync(CancellationToken cancellationToken)
    {
        var authMode = _options.Value.Authentication?.Mode ?? AuthenticationMode.Config;

        if (authMode == AuthenticationMode.Config)
        {
            // Static auth mode has no per-user delete permission; the configured API key
            // governs deletion exactly as it governs push.
            return (await _authentication.AuthenticateAsync(Request.GetApiKey(), cancellationToken), GetActor());
        }

        var feedId = _feedContext.CurrentFeed.Id;

        var apiKey = Request.GetApiKey();
        if (!string.IsNullOrEmpty(apiKey))
        {
            var authResult = await _feedAuthentication.AuthenticateByTokenAsync(apiKey, cancellationToken);
            if (authResult.IsAuthenticated && authResult.UserId.HasValue)
                return (await _permissionService.CanDeleteAsync(authResult.UserId.Value, feedId, cancellationToken), authResult.Username);
        }

        var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
            return (await _permissionService.CanDeleteAsync(userId, feedId, cancellationToken), GetActor());

        return (false, GetActor());
    }

    private string GetActor()
    {
        var name = HttpContext.User.Identity?.Name;
        if (!string.IsNullOrEmpty(name))
            return name;

        // Config mode API keys are shared and carry no user identity.
        return string.IsNullOrEmpty(Request.GetApiKey()) ? "anonymous" : "api-key";
    }

    private void LogAudit(LogLevel level, string eventName, string packageId, string packageVersion, string actor)
    {
        if (!_logger.IsEnabled(level))
            return;

        LogAuditEvent(
            level,
            eventName,
            _feedContext.CurrentFeed.Slug,
            packageId,
            packageVersion,
            actor,
            HttpContext.Connection.RemoteIpAddress);
    }

    private static PackageIdentity TryReadPackageIdentity(Stream packageStream)
    {
        try
        {
            using var reader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
            return reader.GetIdentity();
        }
        catch (Exception)
        {
            // The indexer rejects the package and logs why.
            return null;
        }
        finally
        {
            packageStream.Position = 0;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Exception thrown during package upload")]
    private partial void LogUploadException(Exception exception);

    [LoggerMessage(Message = "AUDIT {Event} feed={Feed} package_id={PackageId} package_version={PackageVersion} actor={Actor} ip={Ip}")]
    private partial void LogAuditEvent(LogLevel level, string @event, string feed, string packageId, string packageVersion, string actor, IPAddress ip);
}
