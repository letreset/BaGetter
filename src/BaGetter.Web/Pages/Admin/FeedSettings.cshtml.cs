using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Core.Authentication;
using BaGetter.Core.Configuration;
using BaGetter.Core.Entities;
using BaGetter.Core.Feeds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace BaGetter.Web.Pages.Admin;

[Authorize(AuthenticationSchemes = Core.Authentication.AuthenticationConstants.CookieScheme)]
public class FeedSettingsModel : PageModel
{
    private readonly IFeedService _feedService;
    private readonly IUserService _userService;

    public FeedSettingsModel(IFeedService feedService, IUserService userService, IOptions<BaGetterOptions> options)
    {
        _feedService = feedService ?? throw new ArgumentNullException(nameof(feedService));
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        GlobalOptions = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public BaGetterOptions GlobalOptions { get; }

    public Feed Feed { get; set; }

    // General
    [BindProperty]
    public string Name { get; set; }

    [BindProperty]
    public string Description { get; set; }

    [BindProperty]
    public bool? IsReadOnlyMode { get; set; }

    [BindProperty]
    public bool UseGlobalReadOnly { get; set; }

    // Package behavior
    [BindProperty]
    public PackageOverwriteAllowed? AllowPackageOverwrites { get; set; }

    [BindProperty]
    public bool UseGlobalOverwrite { get; set; }

    [BindProperty]
    public PackageDeletionBehavior? PackageDeletionBehavior { get; set; }

    [BindProperty]
    public bool UseGlobalDeletion { get; set; }

    [BindProperty]
    public uint? MaxPackageSizeGiB { get; set; }

    [BindProperty]
    public bool UseGlobalMaxSize { get; set; }

    // Retention
    [BindProperty]
    public int? RetentionMaxMajorVersions { get; set; }

    [BindProperty]
    public bool UseGlobalRetentionMajor { get; set; }

    [BindProperty]
    public int? RetentionMaxMinorVersions { get; set; }

    [BindProperty]
    public bool UseGlobalRetentionMinor { get; set; }

    [BindProperty]
    public int? RetentionMaxPatchVersions { get; set; }

    [BindProperty]
    public bool UseGlobalRetentionPatch { get; set; }

    [BindProperty]
    public int? RetentionMaxPrereleaseVersions { get; set; }

    [BindProperty]
    public bool UseGlobalRetentionPrerelease { get; set; }

    // Mirrors, in priority order (the posted list order is the saved order)
    [BindProperty]
    public List<MirrorInput> Mirrors { get; set; } = [];

    public string SuccessMessage { get; set; }
    public string ErrorMessage { get; set; }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }

    private async Task<bool> IsCurrentUserAdminAsync(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return false;
        return await _userService.IsAdminAsync(userId, cancellationToken);
    }

    private void PopulateFromFeed(Feed feed)
    {
        Name = feed.Name;
        Description = feed.Description;

        UseGlobalReadOnly = !feed.IsReadOnlyMode.HasValue;
        IsReadOnlyMode = feed.IsReadOnlyMode;

        UseGlobalOverwrite = !feed.AllowPackageOverwrites.HasValue;
        AllowPackageOverwrites = feed.AllowPackageOverwrites;

        UseGlobalDeletion = !feed.PackageDeletionBehavior.HasValue;
        PackageDeletionBehavior = feed.PackageDeletionBehavior;

        UseGlobalMaxSize = !feed.MaxPackageSizeGiB.HasValue;
        MaxPackageSizeGiB = feed.MaxPackageSizeGiB;

        UseGlobalRetentionMajor = !feed.RetentionMaxMajorVersions.HasValue;
        RetentionMaxMajorVersions = feed.RetentionMaxMajorVersions;

        UseGlobalRetentionMinor = !feed.RetentionMaxMinorVersions.HasValue;
        RetentionMaxMinorVersions = feed.RetentionMaxMinorVersions;

        UseGlobalRetentionPatch = !feed.RetentionMaxPatchVersions.HasValue;
        RetentionMaxPatchVersions = feed.RetentionMaxPatchVersions;

        UseGlobalRetentionPrerelease = !feed.RetentionMaxPrereleaseVersions.HasValue;
        RetentionMaxPrereleaseVersions = feed.RetentionMaxPrereleaseVersions;

        // Secrets (Password/Token) are NOT populated; use the "leave blank to keep" pattern
        Mirrors = feed.Mirrors
            .OrderBy(m => m.SortOrder)
            .Select(m => new MirrorInput
            {
                Id = m.Id,
                Enabled = m.Enabled,
                PackageSource = m.PackageSource,
                Legacy = m.Legacy,
                DownloadTimeoutSeconds = m.DownloadTimeoutSeconds,
                AuthType = m.AuthType,
                AuthUsername = m.AuthUsername,
                AuthCustomHeaders = m.AuthCustomHeaders,
            })
            .ToList();

        SetSecretIndicators(feed);
    }

    // The "a password/token is set" hints come from the stored mirrors, never from the form.
    private void SetSecretIndicators(Feed feed)
    {
        foreach (var input in Mirrors)
        {
            var existing = feed.Mirrors.FirstOrDefault(m => m.Id == input.Id);
            input.HasAuthPassword = !string.IsNullOrEmpty(existing?.AuthPassword);
            input.HasAuthToken = !string.IsNullOrEmpty(existing?.AuthToken);
        }
    }

    private static bool TryValidateMirror(MirrorInput input, int position, out string error)
    {
        if (!Uri.TryCreate(input.PackageSource?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = $"Mirror {position}: the package source must be an absolute http(s) URL.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(input.AuthCustomHeaders)
            && !TryValidateCustomHeaders(input.AuthCustomHeaders.Trim(), out var headersError))
        {
            error = $"Mirror {position}: {headersError}";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Applies the posted mirror list to the feed: posted rows are updated or added in list order,
    /// and stored mirrors missing from the list are removed.
    /// </summary>
    private void ApplyMirrors()
    {
        var existing = Feed.Mirrors.ToDictionary(m => m.Id);
        var mirrors = new List<FeedMirror>();

        for (var i = 0; i < Mirrors.Count; i++)
        {
            var input = Mirrors[i];

            // An id that doesn't belong to this feed is treated as a new mirror, so secrets
            // can never be carried over from another feed's mirror.
            var mirror = input.Id is int id && existing.Remove(id, out var stored)
                ? stored
                : new FeedMirror();

            mirror.SortOrder = i;
            mirror.Enabled = input.Enabled;
            mirror.PackageSource = input.PackageSource.Trim();
            mirror.Legacy = input.Legacy;
            mirror.DownloadTimeoutSeconds = input.DownloadTimeoutSeconds;
            mirror.AuthType = input.AuthType;
            mirror.AuthUsername = string.IsNullOrWhiteSpace(input.AuthUsername) ? null : input.AuthUsername.Trim();
            mirror.AuthCustomHeaders = string.IsNullOrWhiteSpace(input.AuthCustomHeaders) ? null : input.AuthCustomHeaders.Trim();

            // Only overwrite secrets if a new value was provided; leave blank to keep existing
            if (!string.IsNullOrEmpty(input.AuthPasswordNew))
                mirror.AuthPassword = input.AuthPasswordNew;

            if (!string.IsNullOrEmpty(input.AuthTokenNew))
                mirror.AuthToken = input.AuthTokenNew;

            mirrors.Add(mirror);
        }

        foreach (var removed in existing.Values)
            Feed.Mirrors.Remove(removed);

        foreach (var mirror in mirrors.Where(m => !Feed.Mirrors.Contains(m)))
            Feed.Mirrors.Add(mirror);
    }

    private static readonly HashSet<string> _blockedHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Cookie", "Host", "Content-Length", "Transfer-Encoding",
        "Connection", "Upgrade", "Proxy-Authorization", "Set-Cookie"
    };

    private static bool TryValidateCustomHeaders(string json, out string error)
    {
        Dictionary<string, string> headers;
        try
        {
            headers = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (JsonException)
        {
            error = "Custom headers must be valid JSON (e.g. {\"X-My-Header\": \"value\"}).";
            return false;
        }

        if (headers == null)
        {
            error = "Custom headers must be a JSON object.";
            return false;
        }

        foreach (var name in headers.Keys)
        {
            if (_blockedHeaderNames.Contains(name))
            {
                error = $"Custom headers may not include the security-sensitive header '{name}'.";
                return false;
            }
        }

        error = null;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        if (!await IsCurrentUserAdminAsync(cancellationToken))
            return RedirectToPage("/Index");

        Feed = await _feedService.GetFeedBySlugAsync(slug, cancellationToken);
        if (Feed == null) return NotFound();

        PopulateFromFeed(Feed);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string slug, CancellationToken cancellationToken)
    {
        if (!await IsCurrentUserAdminAsync(cancellationToken))
            return RedirectToPage("/Index");

        Feed = await _feedService.GetFeedBySlugAsync(slug, cancellationToken);
        if (Feed == null) return NotFound();

        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 256)
        {
            ErrorMessage = "Name is required and must be at most 256 characters.";
            PopulateFromFeed(Feed);
            return Page();
        }

        // Validate every mirror before touching the feed. On failure the posted form is shown
        // again as-is, so the admin doesn't lose edits to the mirror list.
        for (var i = 0; i < Mirrors.Count; i++)
        {
            if (!TryValidateMirror(Mirrors[i], i + 1, out var mirrorError))
            {
                ErrorMessage = mirrorError;
                SetSecretIndicators(Feed);
                return Page();
            }
        }

        Feed.Name = Name.Trim();
        Feed.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

        Feed.IsReadOnlyMode = UseGlobalReadOnly ? null : IsReadOnlyMode;
        Feed.AllowPackageOverwrites = UseGlobalOverwrite ? null : AllowPackageOverwrites;
        Feed.PackageDeletionBehavior = UseGlobalDeletion ? null : PackageDeletionBehavior;
        Feed.MaxPackageSizeGiB = UseGlobalMaxSize ? null : MaxPackageSizeGiB;

        Feed.RetentionMaxMajorVersions = UseGlobalRetentionMajor ? null : RetentionMaxMajorVersions;
        Feed.RetentionMaxMinorVersions = UseGlobalRetentionMinor ? null : RetentionMaxMinorVersions;
        Feed.RetentionMaxPatchVersions = UseGlobalRetentionPatch ? null : RetentionMaxPatchVersions;
        Feed.RetentionMaxPrereleaseVersions = UseGlobalRetentionPrerelease ? null : RetentionMaxPrereleaseVersions;

        ApplyMirrors();

        await _feedService.UpdateFeedAsync(Feed, cancellationToken);

        SuccessMessage = "Settings saved.";
        PopulateFromFeed(Feed);
        return Page();
    }

    public class MirrorInput
    {
        /// <summary>The stored mirror's id, or null for a mirror added in this form.</summary>
        public int? Id { get; set; }
        public bool Enabled { get; set; }
        public string PackageSource { get; set; }
        public bool Legacy { get; set; }
        public int? DownloadTimeoutSeconds { get; set; }
        public MirrorAuthenticationType? AuthType { get; set; }
        public string AuthUsername { get; set; }
        public string AuthPasswordNew { get; set; }
        public string AuthTokenNew { get; set; }
        public string AuthCustomHeaders { get; set; }

        // Display only, set from the stored mirror
        public bool HasAuthPassword { get; set; }
        public bool HasAuthToken { get; set; }
    }
}
