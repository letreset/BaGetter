# bagetter

A Helm chart for [BaGetter](https://github.com/letreset/BaGetter), a lightweight NuGet and symbol server. It's built on the [bjw-s common library](https://github.com/bjw-s-labs/helm-charts/tree/main/charts/library/common).

## Install

The chart is published as an OCI artifact with every release:

```bash
helm install bagetter oci://ghcr.io/letreset/charts/bagetter --version <version>
```

The chart and app versions match the [release](https://github.com/letreset/BaGetter/releases) tag. By default the chart deploys the image `letreset/bagetter:<version>`.

## Values

See [`values.yaml`](values.yaml) for all options. Common ones:

| Key | Default | Description |
|-----|---------|-------------|
| `configMaps.bagetter-env.data` | SQLite + FileSystem on `/data` | Environment variables passed to BaGetter. See the [configuration docs](https://letreset.github.io/BaGetter/docs/configuration). Put secrets in a Secret instead. |
| `controllers.bagetter.containers.bagetter.env` | unset | Secret-backed environment variables, e.g. `Authentication__InitialAdmin__Password` from a Secret for the [first administrator](https://letreset.github.io/BaGetter/docs/authentication#the-first-administrator) in `Local` mode |
| `controllers.bagetter.containers.bagetter.image.repository` | `letreset/bagetter` | Image repository |
| `controllers.bagetter.containers.bagetter.image.tag` | release version | Image tag |
| `controllers.bagetter.containers.bagetter.probes` | `/livez` liveness, `/health` readiness | Health probes |
| `service.bagetter-srv.ports.http.port` | `8080` | Service port |
| `ingress.bagetter-ingress.enabled` | `false` | Enable ingress |
| `persistence.bagetter-data` | 10Gi RWO at `/data` | Packages, symbols, SQLite DB and Data Protection keys |
