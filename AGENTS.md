# AGENTS.md

Guidance for AI agents working in this repository.

## Guidelines for AI Agents

### 1. Think Before Coding
**Don't assume. Don't hide confusion. Surface tradeoffs.**
- State your assumptions explicitly. If you're uncertain, ask.
- If there are several interpretations, present them. Don't pick one silently.
- If a simpler approach exists, say so. Push back when it's warranted.
- If something is unclear, stop, name what's confusing, and ask.

### 2. Simplicity First
**Write the minimum code that solves the problem. Nothing speculative.**
- No features beyond what was asked, no abstractions for single-use code, and no configurability that wasn't requested.
- No error handling for impossible scenarios.
- Test: would a senior engineer call this overcomplicated? If so, simplify.

### 3. Surgical Changes
**Touch only what you must. Clean up only your own mess.**
- Don't "improve" adjacent code, comments or formatting, and don't refactor what isn't broken. Match the existing style.
- Mention unrelated dead code instead of deleting it. Do remove imports and variables that *your* change made unused.
- Every changed line should trace back to the request.

### 4. Goal-Driven Execution
**Define success criteria. Loop until verified.**
- "Fix the bug" means: write a test that reproduces it, then make it pass. "Refactor X" means: tests pass before and after.
- For multi-step tasks, state a brief plan with a verification check per step.

---

## What is BaGetter (this fork)

A lightweight NuGet and symbol server: ASP.NET Core on .NET 10, implementing the NuGet v3 protocol, with pluggable database, storage and search backends.

This repo is **letreset/BaGetter**. It's a fork of Niverplast/BaGetter, which is itself a fork of bagetter/BaGetter, and it has diverged substantially from upstream:
- **Multi-feed**: each feed has its own packages, settings, mirror and permissions.
- **Local/Entra/Hybrid auth**: users, groups, PATs and per-feed permissions.
- **Admin UI**, PAT-expiry emails, and Data Protection keys persisted to storage.

Upstream is only a source to cherry-pick from. We don't send PRs there.

## Branches, versions, releases

- `main` is the only long-lived branch. Work happens on `feature/*` branches, which are merged into `main` with `--no-ff`.
- Versioning is independent semver starting at **2.0.0** and is unrelated to upstream's 1.x.
- Pushing a `vX.Y.Z` tag on `main` runs `.github/workflows/release.yml`. It runs the tests, creates a GitHub release with a zip and a git-cliff changelog, pushes the Docker image `letreset/bagetter` to Docker Hub, and pushes the Helm chart to `oci://ghcr.io/letreset/charts`.
- A `-` in the tag (e.g. `v2.1.0-rc.1`) marks a prerelease, which does not move the `latest` image tag.
- Release notes credit contributors by GitHub username (`@nick`), never by real name. `cliff.toml` resolves authors through the GitHub API (`[remote.github]`, needs `GITHUB_TOKEN`); keep it that way, and use `@nick` when writing or editing release notes by hand.
- Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `ci:`, …). `cliff.toml` groups the changelog by these prefixes.
- The roadmap lives in GitHub issues on letreset/BaGetter, one issue per task.

## Repo layout

| Path | Contents |
|---|---|
| `src/BaGetter/` | Host: `Program.cs`, `Startup.cs` (DI and middleware pipeline), `ValidateBaGetterOptions`, `ConfigureBaGetterServer` (CORS, forms, forwarded headers, IIS), Data Protection key storage |
| `src/BaGetter.Core/` | Business logic, database-agnostic. `Authentication/`, `Configuration/`, `Content/`, `Email/`, `Entities/`, `Feeds/`, `Indexing/`, `Metadata/`, `Notifications/`, `Search/`, `ServiceIndex/`, `Statistics/`, `Storage/`, `Upstream/`, `Validation/` |
| `src/BaGetter.Web/` | Controllers, Razor Pages (`Pages/`, `Pages/Admin/`, `Pages/Account/`), `Middleware/`, `Authentication/`, routing (`BaGetterEndpointBuilder`, `Routes`), `BaGetterUrlGenerator` |
| `src/BaGetter.Protocol/` | NuGet v3 client and models, used for upstream mirrors |
| `src/BaGetter.Database.{Sqlite,SqlServer,PostgreSql,MySql}/` | EF Core context and migrations per provider |
| `src/BaGetter.{Aws,Azure,Gcp,Aliyun,Tencent}/` | Cloud storage providers |
| `tests/` | xUnit projects mirroring `src/` |
| `docs/` | Docusaurus site, deployed to GitHub Pages by `docs.yml` |
| `deployment templates/` | Helm chart (`chart/bagetter`, built on bjw-s app-template) |

## Build & test

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet run --project src/BaGetter     # http://localhost:5000
dotnet test --filter "FullyQualifiedName~UserServiceTests"
```

The SDK is pinned in `global.json`.

EF migrations need one per provider (Sqlite, SqlServer, PostgreSql, MySql):
```bash
dotnet ef migrations add Name --project src/BaGetter.Database.Sqlite --startup-project src/BaGetter
```

## Architecture

### Provider pattern
Storage, database, search and email use `IProvider<T>`. Every implementation is registered (via `TryAdd*`), and the configuration (`Database:Type`, `Storage:Type`, …) picks the active one at runtime through `DependencyInjectionExtensions.GetServiceFromProviders<T>`.

### Multi-feed
- The `Feed` entity (`Core/Entities/Feed.cs`) holds per-feed overrides: overwrite and deletion behavior, read-only mode, max package size, retention, and **mirror settings, which live in the DB rather than in config**. `IFeedSettingsResolver` merges a feed's overrides with the global `BaGetterOptions`.
- `FeedResolutionMiddleware` maps `/feeds/{slug}/…` to that feed by moving the slug into `PathBase`. Any other path resolves to the default feed (`Feed.DefaultSlug = "default"`). Controllers read `IFeedContext.CurrentFeed`, and services take `feedId`/`feedSlug`.
- Because the slug is in `PathBase`, routes and `BaGetterUrlGenerator` stay feed-agnostic. Add a route once and it works for every feed.
- Storage paths are `packages/{feedSlug}/{id}/{version}/…` and `symbols/{feedSlug}/…`.
- Upstream clients come from `UpstreamClientFactory.CreateForFeed(feed)`.

### Authentication & authorization
- `Authentication:Mode` is one of `Config` (legacy `ApiKey`/`Credentials`, backward compatible), `Local`, `Entra` or `Hybrid`.
- The `NugetBasicAuth` scheme is the default. It forwards to the cookie scheme (`BaGetter.Auth`, 60-minute sliding expiry) when a cookie is present without an `Authorization` header, which separates browsers from client tools.
- `IFeedAuthenticationService` authenticates by PAT (`AuthenticateByTokenAsync`) or by username/password (`AuthenticateByCredentialsAsync`). Passwords use bcrypt; tokens are stored as prefix + hash.
- `FeedPermissionHandler` enforces per-feed permissions (pull/push/delete) for the current feed. User permissions come from groups via `PermissionService`, and `EntraRoleSyncService` syncs Entra app roles into local groups.

### Data model
All entities are defined in `Core/Entities/AbstractContext.cs`: Feed, Package, PackageDependency, PackageType, TargetFramework, User, Group, UserGroup, FeedPermission, PersonalAccessToken.

### HTTP pipeline (`Startup.Configure`, in order)
ForwardedHeaders → PathBase → HSTS (optional) → `SecurityHeadersMiddleware` → ResponseCompression → `/livez` → `FeedStaticFilePathMiddleware` → StaticFiles → Authentication → RateLimiter (only when `RequestRateLimit:Enabled`) → `FeedResolutionMiddleware` → Routing → Authorization → CORS → `OperationCancelledMiddleware` (maps `OperationCanceledException` to 409) → endpoints → health check (`HealthCheck:Path`).

### API routes (`BaGetterEndpointBuilder`; every route is also available under `/feeds/{slug}/`)
- `GET /v3/index.json`: the service index.
- `GET /v3/search`, `GET /v3/autocomplete`.
- `GET /v3/registration/{id}/index.json`, `…/page/{lower}/{upper}.json`, `…/{version}.json`. The index is paged once a package has more than `RegistrationPageSize` versions (default 64).
- `GET /v3/package/{id}/index.json`, and `…/{version}/{id}.{version}.nupkg`, `.nuspec`, `/readme`, `/icon`.
- `GET /v3/dependents`.
- `PUT /api/v2/package`, `DELETE`/`POST` `/api/v2/package/{id}/{version}`.
- `PUT /api/v2/symbol`, `GET /api/download/symbols/…`.

## Configuration

`BaGetterOptions` (`Core/Configuration/`) is bound from the config root. Sources, later ones winning: `appsettings*.json` (from `BAGET_CONFIG_ROOT` if set), user secrets, the optional machine-wide file (`%ProgramData%\BaGetter\appsettings.json` or `/etc/bagetter/appsettings.json`, see `Program.AddPlatformConfigFile`), environment variables, command line, `/run/secrets` (key-per-file). `ValidateBaGetterOptions` fails startup on invalid values.

Main keys:
- `Database`, `Storage`, `Search`: each has a `Type`.
- `Authentication`: `Mode`, `Entra`, token and lockout limits.
- `Email`, `PatExpiryNotification`.
- `MaxPackageSizeGiB`, `RegistrationPageSize`, `Cors` (`AllowedOrigins`, `AllowCredentials`), `SecurityHeaders` (`Enabled`, `EnableHsts`, `HstsMaxAgeDays`), `RequestRateLimit` (`Enabled`, `PermitLimit`, `WindowSeconds`, `QueueLimit`; off by default).
- `HealthCheck`, `Statistics`.
- `Mirror`, `AllowPackageOverwrites`, `PackageDeletionBehavior` and `Retention` are only **defaults and seeds**. Per-feed values in the DB override them. The global `Mirror` block is `[Obsolete]` and is only read once, to seed the default feed.

Docker defaults (`Dockerfile`): the `/data` volume holds packages, symbols and the SQLite DB (`Data Source=/data/db/bagetter.db`).

## Code style (`.editorconfig`, warnings)

- 4-space indent (2-space for JSON).
- Naming: PascalCase for public members, `_camelCase` for private fields.
- `var` preferred. `System` usings first, all usings outside the namespace, file-scoped namespaces.
- Accessibility modifiers are required. Mark fields readonly where possible. No `this.`. Use predefined types (`int`, `string`).
- No primary constructors. No expression-bodied methods or constructors (properties and accessors are fine).
- One top-level type per file. The exception is a small helper that is only meaningful next to its owner (e.g. `enum PackageAddResult` beside `IPackageDatabase`). If a reader would look for the helper anywhere else, split it out.
- Suppress CS1591 for non-public XML docs.

## Testing

- The stack is xUnit + Moq. Integration tests use a temp-dir SQLite DB.
- Name the outer class after the type under test: `<Type>Tests` in `BaGetter.Core.Tests`, `<Type>Facts` in `BaGetter.Web.Tests`.
- Group each method's tests in a nested class named after the method, sharing a `FactsBase` (e.g. `PermissionServiceTests` → `public class CanPushAsync : FactsBase`). Don't use the old `The<Method>Method` naming.
- `BaGetterApplication` (`tests/BaGetter.Tests/Support/BaGetApplication.cs`) is the `WebApplicationFactory` host. It pins `SystemTime` to 2020-01-01. Pass `inMemoryConfiguration: dict => …` to override config, and seed data with `AddPackageAsync`/`AddSymbolPackageAsync`.
- Adding a service index resource changes the expected counts in `BaGetClientIntegrationTests`/`NuGetClientIntegrationTests` and the expected JSON in `TestData.resx`.

## Packages

All versions live in `Directory.Packages.props` (central package management). Never put versions in a `.csproj`.
