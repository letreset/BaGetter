# Upgrading from 1.x

This page is for people moving from upstream [bagetter/BaGetter](https://github.com/bagetter/BaGetter) 1.x to this fork's 2.x. The upgrade is in place: point the new version at your existing database and storage, and your packages, API keys and URLs keep working.

## Before you start

**Back up the database and the package storage.** BaGetter 2.x runs its database migrations automatically on startup (`RunMigrationsAtStartup`, on by default). They add the feed, user, group, permission and token tables, and move every existing package into the default feed. There is no downgrade path back to 1.x once they have run.

## Step by step

1. Back up the database and storage.
2. Change the image from `bagetter/bagetter` to [`letreset/bagetter`](https://hub.docker.com/r/letreset/bagetter), or download the zip from the [releases page](https://github.com/letreset/BaGetter/releases). Pin a version, for example `letreset/bagetter:2.0.0`.
3. Keep your existing configuration. Nothing needs to change for BaGetter to start.
4. Make sure the storage location is persistent (for Docker, the `/data` volume). BaGetter 2.x also stores its Data Protection keys there; see [below](#data-protection-keys).
5. Start BaGetter and check the log for `Copying global Mirror configuration to default feed` if you use a mirror.
6. Browse to the site, and restore and push a package to confirm everything works.

## What changes

### Feeds and URLs

Every package now belongs to a [feed](feeds.md). On the first start BaGetter creates the **default feed** (slug `default`) and puts all your existing packages in it.

- The default feed stays at the old URLs: `https://your-server/v3/index.json`. Existing `nuget.config` files and CI pipelines keep working.
- New feeds live under `/feeds/{slug}/`, for example `https://your-server/feeds/internal/v3/index.json`.
- New packages in the default feed are stored under `packages/default/…` in storage. Packages pushed by 1.x stay at their old path (`packages/{id}/…`) and are still found, so you don't need to move files.

### Mirror settings move to the feed

The global `Mirror` section is **obsolete**. On first start, if it is enabled, BaGetter copies it (source, legacy flag, timeout and upstream authentication) to the default feed as its first mirror. From then on each feed's own mirror settings, edited in **Admin > Feeds**, are what apply. Changing `Mirror` in `appsettings.json` afterwards has no effect, so you can remove it once the copy has happened.

:::info Upgrading from an earlier 2.x release

A feed can now have [several mirrors](feeds.md#multiple-mirrors). The database migration moves each feed's existing mirror into the new mirror list, keeping its enabled state and credentials, so single-mirror feeds keep working unchanged. The admin feeds API (`/api/v1/feeds`) now returns a `mirrors` array instead of the `mirror*` fields.

:::

### Feed settings override the global ones

These settings can now be set per feed. The values in configuration become the **defaults** for feeds that don't override them:

- `AllowPackageOverwrites`
- `PackageDeletionBehavior`
- `IsReadOnlyMode`
- `MaxPackageSizeGiB`
- `Retention` (max major, minor, patch and prerelease versions)

Nothing changes until you override a setting on a feed. See [Feed settings](feeds.md#feed-settings).

### Authentication

`Authentication:Mode` selects how people sign in. The default is `Config`, which is the 1.x behavior: `ApiKey`/`ApiKeys` protect pushes and `Credentials` protect reads. If you do nothing, authentication works exactly as before.

| Mode | Use it when |
|---|---|
| `Config` | You want the 1.x behavior (the default) |
| `Local` | You want user accounts, groups and per-feed permissions stored in BaGetter |
| `Entra` | Everyone signs in with Microsoft Entra ID |
| `Hybrid` | You want Entra ID for people and local accounts for build agents or external users |

Switching away from `Config` turns off anonymous access, `ApiKey` and `Credentials`. Plan the switch before you make it; see [Authentication](authentication.md).

### Data Protection keys

BaGetter 2.x keeps its ASP.NET Core Data Protection keys (which protect sign-in cookies and forms) in the configured package storage, at `dataprotection/keyring.xml`. With file system storage in Docker this is inside `/data`. If `/data` isn't a persistent volume, every restart signs everybody out, and several replicas can't share cookies.

### New settings you may want

These are optional and off or safe by default. See [Configuration](configuration.md).

- `RegistrationPageSize`: registration index paging for packages with many versions (default 64).
- `Cors`: allowed origins for browser clients.
- `SecurityHeaders`: security headers (on by default) and optional HSTS.
- `RequestRateLimit`: per-client request rate limiting (off by default).
- `Email` and `PatExpiryNotification`: emails before personal access tokens expire.
