# Feeds

A **feed** is a separate package source inside one BaGetter server. Each feed has its own packages, its own settings (overwrites, deletion, retention, size limit, read-only mode), its own read-through mirror and its own permissions. Use feeds to keep, for example, `internal` packages apart from `experimental` ones, or to give each team a mirror of nuget.org with different rules.

## Feed URLs

Every feed has a **slug**, and its NuGet v3 endpoints live under `/feeds/{slug}/`:

| Feed | Service index |
|---|---|
| Default feed (slug `default`) | `https://your-server/v3/index.json` |
| Any other feed, e.g. `internal` | `https://your-server/feeds/internal/v3/index.json` |

All endpoints (search, registrations, package content, push, delete, symbols) work under the feed prefix, so a client only needs the service index URL. The web UI works the same way: `https://your-server/feeds/internal/` lists that feed's packages.

The **default feed** is created on first start and can't be deleted. Its URLs are the ones BaGetter has always used, so clients configured before multi-feed support keep working.

## Managing feeds

Feeds are managed by administrators on **Admin > Feeds**.

- **Create** a feed with a slug, a display name and an optional description. A slug is lowercase letters, digits and hyphens, can't start or end with a hyphen, and is at most 128 characters. It becomes part of the URL, so choose it with care.
- **Reorder** feeds by dragging them. The order is used wherever feeds are listed in the UI.
- **Edit** a feed's name and description, or open its **Settings**.
- **Delete** a feed. This deletes all of its packages from the database and storage. It can't be undone.

The same operations are available as a JSON API at `/api/v1/feeds` (`GET`, `POST`, `PUT /{slug}`, `DELETE /{slug}`) for signed-in administrators.

## Feed settings

Open **Admin > Feeds > Settings** for a feed (`/Admin/Feeds/{slug}/Settings`). Each setting has a **Use global default** checkbox: while it is checked, the feed follows the value from [configuration](configuration.md); uncheck it to override the value for this feed only.

| Setting | Global setting | Description |
|---|---|---|
| Read-only mode | `IsReadOnlyMode` | Reject pushes and deletes on this feed |
| Package overwrite policy | `AllowPackageOverwrites` | Disallow (recommended), prerelease only, or allow all. See [package overwrites](configuration.md#enable-package-overwrites). |
| Deletion behavior | `PackageDeletionBehavior` | Unlist (recommended) or hard delete. See [hard deletions](configuration.md#enable-package-hard-deletions). |
| Max package size (GiB) | `MaxPackageSizeGiB` | Largest package this feed accepts |
| Retention | `Retention` | How many major versions, minor versions per major, patch versions per minor and prerelease versions per patch to keep. See [auto-deletion](configuration.md#package-auto-deletion). |
| Upstream listing cache (seconds) | `UpstreamListingCacheSeconds` | How long mirrored version lists are reused before asking the upstreams again. See [upstream listing cache](#upstream-listing-cache). |

:::tip

Set the values you want for most feeds in configuration, and override only where a feed differs. Changing a global value then updates every feed that still uses the default.

:::

## Mirror (read-through cache)

A feed can mirror an upstream NuGet feed, for example nuget.org. When a client asks the feed for a package it doesn't have, BaGetter fetches it from the upstream source, stores it in the feed and serves it. Later restores are served locally, which speeds up builds and keeps them working when the upstream is unreachable.

Mirrors are listed on each feed's settings page, under **Mirrors**. Use **Add mirror** to add one; each mirror has these settings:

| Setting | Description |
|---|---|
| Enabled | Use this mirror. Clear it to pause a mirror without losing its settings |
| Package source URL | The upstream service index, e.g. `https://api.nuget.org/v3/index.json` |
| Use NuGet V2 (legacy) protocol | For upstream servers that only speak the V2 protocol |
| Download timeout (seconds) | How long to wait for an upstream download |
| Authentication type | `None`, `Basic` (username and password), `Bearer` (token) or `Custom` (headers as JSON, e.g. `{"X-Api-Key":"value"}`) |

Password and token fields are write-only: leave them blank to keep the stored value.

:::warning

Upstream credentials are stored in the BaGetter database. Restrict access to the database and its backups, and use a read-only token for the upstream feed where possible.

:::

### Multiple mirrors

A feed can mirror several upstreams, in order. This is useful when packages live in different places, for example open source packages on nuget.org and licensed packages on a vendor's private feed:

1. `https://api.nuget.org/v3/index.json`
2. `https://nuget.vendor.example/v3/index.json` (Basic authentication)

Developers then add only the BaGetter feed to `nuget.config`, and the vendor credentials stay on the server.

With more than one enabled mirror, BaGetter:

- **Merges version lists and metadata** from every mirror, so a package that exists on both upstreams shows the versions of both. When the same version exists on several mirrors, the earlier mirror's metadata wins.
- **Downloads from the first mirror that has the package**, and caches it in the feed. The package records which upstream it came from.
- **Skips a failing mirror**: an upstream that is unreachable or returns an error is logged and skipped, and the next mirror is tried.

Use the arrow buttons to change the order and **Remove** to delete a mirror, then **Save Settings**. With a single enabled mirror, the feed behaves exactly as it did before multiple mirrors existed.

Put the upstream that has most of your packages first: version lists query every mirror, but a download stops at the first mirror that has the package.

### Upstream listing cache

A restore asks the feed for the version list and metadata of every package, and for a mirrored feed each of those requests also goes to the upstream, even when the package is already stored locally. With many build agents this adds up to thousands of upstream calls per restore, which is slow and can hit upstream rate limits (private feeds, Azure Artifacts, GitHub Packages).

BaGetter therefore keeps each upstream listing in memory for a short time, per feed and package id. Set the duration on the feed's settings page with **Upstream listing cache (seconds)**, below the mirror list, or globally with `UpstreamListingCacheSeconds`:

```json
{
    ...

    "UpstreamListingCacheSeconds": 300,

    ...
}
```

- The default is `300` (5 minutes). `0` turns the cache off, so every request asks the upstreams again.
- With several mirrors, the merged listing of all mirrors is cached.
- Only listings that found the package are cached. When no upstream has the package, or an upstream fails, the next request asks again.
- Concurrent requests for a package that is not cached share one upstream call, so a burst of restores (for example many build agents starting at once, or the moment an entry expires) queries each upstream once per package instead of once per request. A client that disconnects stops waiting, but the shared call continues for the others until the upstream answers or times out.
- Package downloads are not cached here: a downloaded package is stored in the feed and served locally from then on.
- Saving the feed's settings (for example adding, removing or reordering a mirror) invalidates the feed's cached listings.
- The cache lives in the memory of each BaGetter instance and is empty after a restart.

The tradeoff is freshness: a version newly published to an upstream shows up in this feed with a delay of up to the cache duration. Lower the value, or set it to `0`, for a feed where new upstream versions must be visible immediately.

:::info

The global `Mirror` configuration section is obsolete. It is only read once, on the first start after an upgrade, to fill in the default feed's mirror settings. See [Upgrading from 1.x](upgrading.md#mirror-settings-move-to-the-feed).

:::

## Permissions

With `Authentication:Mode` set to `Local`, `Entra` or `Hybrid`, access to each feed is controlled by **pull**, **push** and **delete** permissions, granted to groups on **Admin > Groups**. Administrators can do everything on every feed. Feeds a user can't pull from are hidden from them. See [Feed permissions](authentication.md#feed-permissions).

With the default `Config` mode there are no per-feed permissions: the configured API keys and credentials apply to all feeds.

## Connecting a client

Each feed has a **Connect** page (`/feeds/{slug}/Connect`, or the **Connect** link while browsing the feed) that shows its service index URL and how to authenticate.

With the dotnet CLI:

```shell
dotnet nuget add source "https://your-server/feeds/internal/v3/index.json" --name internal
dotnet nuget push -s internal -k <api-key-or-token> MyPackage.1.0.0.nupkg
```

Or in a `nuget.config` next to your solution:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="internal" value="https://your-server/feeds/internal/v3/index.json" />
    <add key="mirror" value="https://your-server/v3/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <internal>
      <add key="Username" value="build-agent" />
      <add key="ClearTextPassword" value="%BAGETTER_TOKEN%" />
    </internal>
  </packageSourceCredentials>
</configuration>
```

Which username and password to use depends on the [authentication mode](authentication.md#using-bagetter-from-nuget-clients). Keep secrets out of source control: reference an environment variable as above, or add the credentials with `dotnet nuget update source … --username … --password …` on each machine.
