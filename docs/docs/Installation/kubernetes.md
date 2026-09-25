# Run BaGetter on Kubernetes

Every release publishes a Helm chart as an OCI artifact on GitHub Container Registry. The chart is built on the [bjw-s common library](https://github.com/bjw-s-labs/helm-charts/tree/main/charts/library/common), so every option of that library is available.

## Install

```shell
helm install bagetter oci://ghcr.io/letreset/charts/bagetter --version 2.0.0
```

The chart version always matches the BaGetter version, and the chart deploys the image `letreset/bagetter` with that same tag. See the [releases page](https://github.com/letreset/BaGetter/releases) for the available versions.

To see every value with its default:

```shell
helm show values oci://ghcr.io/letreset/charts/bagetter --version 2.0.0
```

## Configuration

BaGetter's settings are environment variables in a ConfigMap, under `configMaps.bagetter-env.data`. The defaults use SQLite and file system storage on the persistent volume:

```yaml
configMaps:
  bagetter-env:
    data:
      ApiKey: "ChangeMe"
      Storage__Type: "FileSystem"
      Storage__Path: "/data"
      Database__Type: "Sqlite"
      Database__ConnectionString: "Data Source=/data/bagetter.db"
```

:::warning

**Change `ApiKey`.** The default is `ChangeMe`. Better, remove it from the ConfigMap and pass it from a Secret, as shown below.

:::

Any setting from [Configuration](../configuration.md) can be added the same way, with `__` as the section separator (for example `Authentication__Mode: "Local"`).

### Secrets

Keep secrets such as the API key, database passwords and the Entra client secret in a Kubernetes Secret, and add it to the container's `envFrom`:

```shell
kubectl create secret generic bagetter-secrets \
  --from-literal=ApiKey='a-long-random-value' \
  --from-literal=Database__ConnectionString='Host=postgres;Database=bagetter;Username=bagetter;Password=...'
```

```yaml
controllers:
  bagetter:
    containers:
      bagetter:
        envFrom:
          - configMapRef:
              name: bagetter-bagetter-env
          - secretRef:
              name: bagetter-secrets
```

`envFrom` replaces the default list, so keep the ConfigMap entry. Its name is `<release name>-bagetter-env`, so adjust it if your release isn't called `bagetter`. When a key is in both, the Secret wins because it is listed last.

## Key values

| Key | Default | Description |
|-----|---------|-------------|
| `configMaps.bagetter-env.data` | SQLite and file system on `/data` | BaGetter settings as environment variables |
| `controllers.bagetter.containers.bagetter.image.tag` | The chart version | Image tag |
| `controllers.bagetter.containers.bagetter.resources` | 100m / 256Mi requested, 500m / 512Mi limit | CPU and memory |
| `controllers.bagetter.containers.bagetter.probes` | `/livez` liveness, `/health` readiness | Health probes |
| `service.bagetter-srv.ports.http.port` | `8080` | Service port |
| `ingress.bagetter-ingress.enabled` | `false` | Create an Ingress |
| `persistence.bagetter-data` | 10Gi, `ReadWriteOnce`, mounted at `/data` | Packages, symbols, the SQLite database and Data Protection keys |

## Probes

The liveness probe calls `/livez`, which only checks that the process responds. The readiness probe calls `/health`, which also checks the database and storage, so a pod that can't reach them is taken out of the Service without being restarted. If you change `HealthCheck__Path`, change the readiness probe path too.

## Ingress

```yaml
ingress:
  bagetter-ingress:
    enabled: true
    className: nginx
    annotations:
      # Allow large packages (the default package limit is 8 GiB).
      nginx.ingress.kubernetes.io/proxy-body-size: "0"
    hosts:
      - host: nuget.example.com
        paths:
          - path: /
            service:
              identifier: bagetter-srv
              port: http
    tls:
      - secretName: nuget-example-com-tls
        hosts:
          - nuget.example.com
```

The ingress controller must forward `X-Forwarded-Proto` and `X-Forwarded-Host` (ingress-nginx does by default), so BaGetter returns `https://` URLs in its service index.

## Scaling out

With the defaults (SQLite and a `ReadWriteOnce` volume) run **one replica**. To run more:

- Use PostgreSQL, SQL Server or MySQL instead of SQLite.
- Use shared storage: a `ReadWriteMany` volume, or cloud storage such as Azure Blob Storage or AWS S3.

The Data Protection keys live in the package storage, so every replica shares them and sign-in cookies work on all of them.

## Upgrade

```shell
helm upgrade bagetter oci://ghcr.io/letreset/charts/bagetter --version <new-version> --reuse-values
```

BaGetter runs database migrations on startup. Back up the database before upgrading across minor or major versions.
