using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Core;
using BaGetter.Core.Authentication;
using BaGetter.Core.Configuration;
using BaGetter.Core.Content;
using BaGetter.Core.Entities;
using BaGetter.Core.Feeds;
using BaGetter.Core.Indexing;
using BaGetter.Core.Search;
using BaGetter.Web.Audit;
using BaGetter.Web.Authentication;
using Markdig;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace BaGetter.Web.Pages;

public class PackageModel : PageModel
{
    private static readonly MarkdownPipeline _markdownPipeline;

    private readonly IPackageService _packages;
    private readonly IPackageContentService _content;
    private readonly ISearchService _search;
    private readonly IUrlGenerator _url;
    private readonly IFeedContext _feedContext;
    private readonly IPermissionService _permissions;
    private readonly IPackageDeletionService _deletionService;
    private readonly IFeedSettingsResolver _feedSettings;
    private readonly WebAuditLog _audit;
    private readonly IOptionsSnapshot<NugetAuthenticationOptions> _authOptions;

    static PackageModel()
    {
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public PackageModel(
        IPackageService packages,
        IPackageContentService content,
        ISearchService search,
        IUrlGenerator url,
        IFeedContext feedContext,
        IPermissionService permissions,
        IPackageDeletionService deletionService,
        IFeedSettingsResolver feedSettings,
        WebAuditLog audit,
        IOptionsSnapshot<NugetAuthenticationOptions> authOptions)
    {
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _url = url ?? throw new ArgumentNullException(nameof(url));
        _feedContext = feedContext ?? throw new ArgumentNullException(nameof(feedContext));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _deletionService = deletionService ?? throw new ArgumentNullException(nameof(deletionService));
        _feedSettings = feedSettings ?? throw new ArgumentNullException(nameof(feedSettings));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _authOptions = authOptions ?? throw new ArgumentNullException(nameof(authOptions));
    }

    public bool Found { get; private set; }

    /// <summary>
    /// Whether the current user may unlist/delete packages in this feed. Gates the
    /// Unlist and Delete buttons on the page. False in Config mode and for anonymous users.
    /// </summary>
    public bool CanDelete { get; private set; }

    /// <summary>
    /// Whether the feed is in read-only mode. Unlist, relist and delete are refused then.
    /// </summary>
    public bool IsReadOnly { get; private set; }

    /// <summary>
    /// Whether the Manage section and the Relist links are shown.
    /// </summary>
    public bool CanManage => CanDelete && !IsReadOnly;

    /// <summary>
    /// Whether the shown version is stored in this feed. Versions that are only available from a
    /// mirror can't be unlisted or deleted here.
    /// </summary>
    public bool IsStoredLocally { get; private set; }

    /// <summary>
    /// The requested version when it doesn't exist; the page then says so instead of showing
    /// another version.
    /// </summary>
    public string VersionNotFound { get; private set; }

    public Package Package { get; private set; }

    public bool IsDotnetTemplate { get; private set; }
    public bool IsDotnetTool { get; private set; }
    public DateTime LastUpdated { get; private set; }
    public long TotalDownloads { get; private set; }

    public IReadOnlyList<PackageDependent> UsedBy { get; set; }
    public IReadOnlyList<DependencyGroupModel> DependencyGroups { get; private set; }
    public IReadOnlyList<VersionModel> Versions { get; private set; }

    public HtmlString Readme { get; private set; }

    public HtmlString ParsedReleaseNotes { get; private set; }

    public string IconUrl { get; private set; }
    public string LicenseUrl { get; private set; }
    public string PackageDownloadUrl { get; private set; }

    public async Task<IActionResult> OnGetAsync(string id, string version, CancellationToken cancellationToken)
    {
        if (FeedAccessGuard.RequiresSignIn(HttpContext, _authOptions.Value.Mode)) return Page();

        var denied = await FeedAccessGuard.CheckReadAccessAsync(
            HttpContext, _feedContext, _permissions, _authOptions.Value.Mode, cancellationToken);
        if (denied != null) return denied;

        CanDelete = await FeedAccessGuard.CanDeleteFromCurrentFeedAsync(
            HttpContext, _feedContext, _permissions, _authOptions.Value.Mode, cancellationToken);
        IsReadOnly = _feedSettings.GetIsReadOnlyMode(_feedContext.CurrentFeed);

        var packages = await _packages.FindPackagesAsync(_feedContext.CurrentFeed.Id, id, cancellationToken);
        var listedPackages = packages.Where(p => p.Listed).ToList();

        if (!string.IsNullOrEmpty(version))
        {
            // A requested version that doesn't exist is reported, not replaced by the latest one.
            if (NuGetVersion.TryParse(version, out var requestedVersion))
            {
                Package = packages.SingleOrDefault(p => p.Version == requestedVersion);
            }

            if (Package == null && packages.Count > 0)
            {
                Package = new Package { Id = packages[0].Id };
                VersionNotFound = version;
                Found = false;
                return Page();
            }
        }

        // Otherwise display the latest version.
        Package ??= listedPackages.OrderByDescending(p => p.Version).FirstOrDefault();

        if (Package == null)
        {
            Package = new Package { Id = id };
            Found = false;
            return Page();
        }

        IsStoredLocally = IsLocal(Package);

        var packageVersion = Package.Version;

        Found = true;
        IsDotnetTemplate = Package.PackageTypes.Any(t => t.Name.Equals("Template", StringComparison.OrdinalIgnoreCase));
        IsDotnetTool = Package.PackageTypes.Any(t => t.Name.Equals("DotnetTool", StringComparison.OrdinalIgnoreCase));
        LastUpdated = packages.Max(p => p.Published);
        TotalDownloads = packages.Sum(p => p.Downloads);

        var dependents = await _search.FindDependentsAsync(_feedContext.CurrentFeed.Id, Package.Id, cancellationToken);

        UsedBy = dependents.Data;
        DependencyGroups = ToDependencyGroups(Package);

        // Managers (CanDelete) also see unlisted versions of this feed so they can relist them;
        // the versions table strikes those through. Unlisted versions that only exist on a mirror
        // can't be relisted, so they stay hidden. Everyone else only sees listed versions.
        var versionsToShow = CanDelete
            ? packages.Where(p => p.Listed || IsLocal(p)).ToList()
            : listedPackages;
        Versions = ToVersions(versionsToShow, packageVersion);

        if (Package.HasReadme)
        {
            Readme = await GetReadmeHtmlStringOrNullAsync(Package.Id, packageVersion, cancellationToken);
        }

        ParsedReleaseNotes = ParseReleaseNotes();

        IconUrl = Package.HasEmbeddedIcon
            ? _url.GetPackageIconDownloadUrl(Package.Id, packageVersion)
            : Package.IconUrlString;
        LicenseUrl = Package.LicenseUrlString;
        PackageDownloadUrl = _url.GetPackageDownloadUrl(Package.Id, packageVersion);

        return Page();
    }

    public async Task<IActionResult> OnPostUnlistAsync(string id, string version, CancellationToken cancellationToken)
    {
        return await ManageVersionAsync(
            "unlist", id, version,
            v => _deletionService.TryUnlistPackageAsync(_feedContext.CurrentFeed.Id, id, v, cancellationToken),
            RedirectToPage(new { id, version }),
            cancellationToken);
    }

    public async Task<IActionResult> OnPostRelistAsync(string id, string version, CancellationToken cancellationToken)
    {
        return await ManageVersionAsync(
            "relist", id, version,
            v => _deletionService.TryRelistPackageAsync(_feedContext.CurrentFeed.Id, id, v, cancellationToken),
            RedirectToPage(new { id, version }),
            cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id, string version, CancellationToken cancellationToken)
    {
        // The version is gone afterwards; land on the package's default view (latest remaining or not-found).
        return await ManageVersionAsync(
            "delete", id, version,
            v => _deletionService.TryHardDeletePackageAsync(_feedContext.CurrentFeed.Id, _feedContext.CurrentFeed.Slug, id, v, cancellationToken),
            RedirectToPage(new { id }),
            cancellationToken);
    }

    /// <summary>
    /// Runs an unlist, relist or delete from the Manage section and writes its audit line
    /// (<c>package_{action}_{succeeded,unauthorized,read_only,not_found}</c>).
    /// </summary>
    private async Task<IActionResult> ManageVersionAsync(
        string action,
        string id,
        string version,
        Func<NuGetVersion, Task<bool>> operation,
        IActionResult success,
        CancellationToken cancellationToken)
    {
        var feed = _feedContext.CurrentFeed.Slug;

        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            _audit.Package(HttpContext, LogLevel.Warning, $"package_{action}_not_found", feed, id, version);
            return NotFound();
        }

        if (_feedSettings.GetIsReadOnlyMode(_feedContext.CurrentFeed))
        {
            _audit.Package(HttpContext, LogLevel.Warning, $"package_{action}_read_only", feed, id, version);
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!await CanDeleteCurrentFeedAsync(cancellationToken))
        {
            _audit.Package(HttpContext, LogLevel.Warning, $"package_{action}_unauthorized", feed, id, version);
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var found = await operation(nugetVersion);
        _audit.Package(
            HttpContext,
            found ? LogLevel.Information : LogLevel.Warning,
            found ? $"package_{action}_succeeded" : $"package_{action}_not_found",
            feed, id, version);

        return success;
    }

    /// <summary>
    /// Packages read from the database have a key; packages that only exist on a mirror come
    /// from the upstream client and don't.
    /// </summary>
    private static bool IsLocal(Package package)
    {
        return package.Key != 0;
    }

    private Task<bool> CanDeleteCurrentFeedAsync(CancellationToken cancellationToken)
        => FeedAccessGuard.CanDeleteFromCurrentFeedAsync(
            HttpContext, _feedContext, _permissions, _authOptions.Value.Mode, cancellationToken);

    private static List<DependencyGroupModel> ToDependencyGroups(Package package)
    {
        return package
            .Dependencies
            .GroupBy(d => d.TargetFramework)
            .Select(group =>
            {
                return new DependencyGroupModel
                {
                    Name = PrettifyTargetFramework(group.Key),
                    Dependencies = group
                        .Where(d => d.Id != null)
                        .Select(d => new DependencyModel
                        {
                            PackageId = d.Id,
                            VersionSpec = (d.VersionRange != null)
                                ? VersionRange.Parse(d.VersionRange).PrettyPrint()
                                : string.Empty
                        })
                        .ToList()
                };
            })
            .ToList();
    }

    private static string PrettifyTargetFramework(string targetFramework)
    {
        if (targetFramework == null) return "All Frameworks";

        NuGetFramework framework;
        try
        {
            framework = NuGetFramework.Parse(targetFramework);
        }
        catch (Exception)
        {
            return targetFramework;
        }

        string frameworkName;
        if (framework.Framework.Equals(FrameworkConstants.FrameworkIdentifiers.NetCoreApp,
            StringComparison.OrdinalIgnoreCase))
        {
            frameworkName = (framework.Version.Major >= 5)
                ? ".NET"
                : ".NET Core";
        }
        else if (framework.Framework.Equals(FrameworkConstants.FrameworkIdentifiers.NetStandard,
            StringComparison.OrdinalIgnoreCase))
        {
            frameworkName = ".NET Standard";
        }
        else if (framework.Framework.Equals(FrameworkConstants.FrameworkIdentifiers.Net,
            StringComparison.OrdinalIgnoreCase))
        {
            frameworkName = ".NET Framework";
        }
        else
        {
            frameworkName = framework.Framework;
        }

        var frameworkVersion = (framework.Version.Build == 0)
            ? framework.Version.ToString(2)
            : framework.Version.ToString(3);

        return $"{frameworkName} {frameworkVersion}";
    }

    private static List<VersionModel> ToVersions(IReadOnlyList<Package> packages, NuGetVersion selectedVersion)
    {
        return packages
            .Select(p => new VersionModel
            {
                Version = p.Version,
                Downloads = p.Downloads,
                Selected = p.Version == selectedVersion,
                // Upstreams report 1900-01-01 for unlisted versions they have no date for.
                LastUpdated = p.Published.Year > 1900 ? p.Published : null,
                Listed = p.Listed,
                IsLocal = IsLocal(p),
            })
            .OrderByDescending(m => m.Version)
            .ToList();
    }

    private async Task<HtmlString> GetReadmeHtmlStringOrNullAsync(
        string packageId,
        NuGetVersion packageVersion,
        CancellationToken cancellationToken)
    {
        await using var readmeStream = await _content.GetPackageReadmeStreamOrNullAsync(_feedContext.CurrentFeed.Id, _feedContext.CurrentFeed.Slug, packageId, packageVersion, cancellationToken);
        if (readmeStream == null) return null;

        using var reader = new StreamReader(readmeStream);
        var readme = await reader.ReadToEndAsync(cancellationToken);

        var readmeHtml = Markdown.ToHtml(readme, _markdownPipeline);
        return new HtmlString(readmeHtml);
    }

    private HtmlString ParseReleaseNotes()
    {
        if (string.IsNullOrWhiteSpace(Package.ReleaseNotes))
        {
            return HtmlString.Empty;
        }

        var releseNotesHtml = Markdown.ToHtml(Package.ReleaseNotes, _markdownPipeline);
        return new HtmlString(releseNotesHtml);
    }

    public class DependencyGroupModel
    {
        public string Name { get; set; }
        public IReadOnlyList<DependencyModel> Dependencies { get; set; }
    }

    // TODO: Convert this to records.
    public class DependencyModel
    {
        public string PackageId { get; set; }
        public string VersionSpec { get; set; }
    }

    // TODO: Convert this to records.
    public class VersionModel
    {
        public NuGetVersion Version { get; set; }
        public long Downloads { get; set; }
        public bool Selected { get; set; }
        public DateTime? LastUpdated { get; set; }
        public bool Listed { get; set; }
        public bool IsLocal { get; set; }
    }
}
