using System.Collections.Generic;
using BaGetter.Core.Configuration;
using BaGetter.Core.Entities;

namespace BaGetter.Core.Feeds;

public interface IFeedSettingsResolver
{
    PackageOverwriteAllowed GetAllowPackageOverwrites(Feed feed);
    PackageDeletionBehavior GetPackageDeletionBehavior(Feed feed);
    bool GetIsReadOnlyMode(Feed feed);
    uint GetMaxPackageSizeGiB(Feed feed);
    RetentionOptions GetRetentionOptions(Feed feed);

    /// <summary>
    /// The feed's enabled mirrors, in priority order. Empty when the feed does not mirror.
    /// </summary>
    IReadOnlyList<MirrorOptions> GetMirrorOptions(Feed feed);
}
