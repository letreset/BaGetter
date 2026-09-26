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

### Azure Table Storage is no longer supported

The `AzureTable` database type can't store feeds, users, groups, permissions or tokens, so BaGetter 2.x stops at startup when `Database:Type` is `AzureTable`. If you use it, move to one of the SQL databases (`Sqlite`, `SqlServer`, `PostgreSql` or `MySql`) before you upgrade: start 1.x with the new database and the same storage, and push your packages again (see [Import packages from a local feed](Import/local-feeds.md)).

### MySQL: the database moves to utf8mb4

Earlier versions stored MySQL data as `latin1`, so a package whose metadata contains other characters (for example the author "Havlíček", or Polish or Turkish text) failed to push or mirror, and user, group and feed names were limited to latin1 too. A migration now converts the database and every table to `utf8mb4` with the `utf8mb4_unicode_ci` collation. Existing data is kept. This also applies when you upgrade from an earlier 2.x release.

- **Back up the database first.** The migration rewrites every table (one `ALTER TABLE` each), which can take a while on a large database and blocks writes to the table being converted. Plan a maintenance window.
- The tables are set to the `DYNAMIC` row format, which MySQL 5.7.9+, MySQL 8 and MariaDB 10.2+ use by default. It is needed because utf8mb4 index keys are larger.
- On MySQL, the nine long package columns (authors, description, summary, tags and the URLs) become `text` so that a package row still fits MySQL's row size limit. They still hold 4000 characters.
- The new collation, like the old one, ignores case. It also ignores accents, so `Muller` and `Müller` count as the same user, group or feed name. If your database already contains two such names, the migration stops with a duplicate key error; rename one of them and start BaGetter again.
- Rolling this migration back converts the tables to `latin1` again. It fails with `Incorrect string value` as soon as a value doesn't fit into latin1 instead of silently replacing characters, so remove or change those values first and run the rollback again.

### Data Protection keys

BaGetter 2.x keeps its ASP.NET Core Data Protection keys (which protect sign-in cookies and forms) in the configured package storage, at `dataprotection/keyring.xml`. With file system storage in Docker this is inside `/data`. If `/data` isn't a persistent volume, every restart signs everybody out, and several replicas can't share cookies.

### PostgreSQL: package versions become case-insensitive

Some PostgreSQL databases created by BaGet or upstream BaGetter still have `Packages.Version` as `varchar(64)`, even though the migration that should have made it `citext` is recorded as applied. On those databases, prerelease versions with capital letters (for example `5.7.22-Hdbfd3d9a85-b6`) can't be found or deleted. BaGetter 2.x repairs this automatically on startup and leaves databases where the column is already `citext` alone.

If the log says `Cannot convert "Packages"."Version" to citext`, the database has versions of the same package that only differ by case. Delete one of each pair and start BaGetter again.

### Usernames and group names are unique regardless of case

Usernames and group names have always been looked up case-insensitively, but on SQLite and PostgreSQL the database itself allowed `alice` and `ALICE` side by side. A migration now adds a unique index on the upper-cased names. If the database already has names that only differ by case, BaGetter stops at startup before the migration runs and lists them, for example `Usernames 'alice', 'ALICE'`. Rename or delete all but one of each (on 1.x or with a database tool) and start BaGetter again.

### Mirrored feeds cache upstream version lists

Mirrored feeds now keep upstream version lists and metadata in memory for 5 minutes by default, instead of asking the upstream on every request. This takes most of the load off the upstreams during restores, but a version newly published upstream can take up to 5 minutes to appear in the feed. To keep the old behavior, set `UpstreamListingCacheSeconds` to `0` globally or on the feed. See [Upstream listing cache](feeds.md#upstream-listing-cache).

### New settings you may want

These are optional and off or safe by default. See [Configuration](configuration.md).

- `RegistrationPageSize`: registration index paging for packages with many versions (default 64).
- `Cors`: allowed origins for browser clients.
- `SecurityHeaders`: security headers (on by default) and optional HSTS.
- `RequestRateLimit`: per-client request rate limiting (off by default).
- `Database:ServerVersion` (MySQL): skips server version detection.
- `Database:JournalMode` (SQLite): sets the journal mode, e.g. `WAL`.
- `Email` and `PatExpiryNotification`: emails before personal access tokens expire.
