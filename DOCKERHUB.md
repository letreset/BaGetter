# BaGetter

A lightweight, self-hosted **NuGet and symbol server** for .NET teams. It implements the NuGet v3 protocol, so `dotnet`, NuGet, Visual Studio and Rider work with it out of the box.

This is an independently maintained fork of [bagetter/BaGetter](https://github.com/bagetter/BaGetter). It adds:

- **Multiple feeds**: each feed has its own packages, settings, retention, and read-through mirror of nuget.org or any other feed.
- **Users and permissions**: local accounts, **Microsoft Entra ID** sign-in, groups, per-feed pull/push/delete permissions, and personal access tokens with expiry email reminders.
- **An admin UI** for feeds, accounts and groups.
- **Pluggable backends**: SQLite, SQL Server, PostgreSQL or MySQL for the database; the file system, Azure Blob, AWS S3, Google Cloud Storage, Aliyun OSS or Tencent COS for storage.

## Quick start

```bash
docker run -d --name bagetter -p 5000:8080 -v bagetter-data:/data letreset/bagetter:latest
```

Open http://localhost:5000, then push a package:

```bash
dotnet nuget push -s http://localhost:5000/v3/index.json -k <api-key> MyPackage.1.0.0.nupkg
```

By default the image stores packages, symbols, the SQLite database and Data Protection keys in `/data`. Mount a volume there to keep them.

## Configuration

Configure BaGetter with environment variables, using `__` as the section separator:

```bash
docker run -d -p 5000:8080 -v bagetter-data:/data \
  -e Authentication__Mode=Local \
  -e Database__Type=PostgreSql \
  -e Database__ConnectionString="Host=db;Database=bagetter;Username=bagetter;Password=..." \
  letreset/bagetter:latest
```

Secrets can also be mounted as files under `/run/secrets` (key-per-file). See the full [configuration docs](https://letreset.github.io/BaGetter/docs/configuration).

## Tags

| Tag | Meaning |
|-----|---------|
| `latest` | Latest stable release |
| `2`, `2.0` | Latest release in that major/minor line |
| `2.0.0` | Exact version (immutable) |
| `2.1.0-rc.1` | Prerelease (never tagged `latest`) |

Images are published for `linux/amd64` and `linux/arm64`.

## Kubernetes

```bash
helm install bagetter oci://ghcr.io/letreset/charts/bagetter --version <version>
```

## Links

- Source & issues: https://github.com/letreset/BaGetter
- Releases & changelog: https://github.com/letreset/BaGetter/releases
- Documentation: https://letreset.github.io/BaGetter/
