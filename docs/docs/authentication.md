# Authentication

BaGetter can protect your feeds with API keys from configuration, with local accounts stored in its database, with Microsoft Entra ID (formerly Azure AD) single sign-on, or with Entra ID and local accounts together. Database-backed modes add groups, per-feed permissions and personal access tokens.

## Authentication modes

The `Authentication:Mode` setting controls which mechanisms are active:

| Mode | Who signs in | Use it when |
|------|-------------|-------------|
| `Config` | Nobody. Pushes need an API key from configuration, reads optionally need a username and password from configuration. | (Default) A single team, a quick setup, or the 1.x behavior. See [Require an API key](configuration.md#require-an-api-key) and [Private feeds](configuration.md#private-feeds). |
| `Local` | Local accounts created by an administrator | You don't use Entra ID but want users, groups and per-feed permissions |
| `Entra` | Microsoft Entra ID accounts only | Everyone in your organization has an Entra ID account |
| `Hybrid` | Entra ID accounts and local accounts | People sign in with Entra ID, and build agents or external partners use local accounts |

```json
{
    "Authentication": {
        "Mode": "Hybrid"
    }
}
```

:::info

When `Mode` is `Config` (or the `Authentication` section is omitted), BaGetter uses `ApiKey`, `ApiKeys` and `Credentials` from configuration, exactly like 1.x. Existing deployments require no changes.

In every other mode those settings are ignored: there is no anonymous access, every request needs a signed-in user, a local account password or a personal access token, and what a user can do is decided by [feed permissions](#feed-permissions).

:::

## The first administrator

Administrators manage feeds, accounts, groups and permissions, and can pull, push and delete on every feed. How you get the first one depends on the mode:

- **`Entra` and `Hybrid`**: assign the `Admin` app role to yourself in Entra ID (see [Step 3](#step-3-define-app-roles-recommended)) and sign in. Admin status always follows the token.
- **`Local`**: there is currently no built-in way to create the first administrator, and the admin UI can't grant admin rights. Until this is solved ([#14](https://github.com/letreset/BaGetter/issues/14)), start in `Hybrid` mode with an Entra admin, or set `IsAdmin` on a user directly in the database.

## Azure Entra ID setup

### Prerequisites

1. An Azure Entra ID tenant
2. An App Registration in your tenant
3. A client secret for the App Registration
4. (Optional) App Roles defined in the App Registration for role-based group sync

### Step 1: Create an App Registration

1. Go to the [Azure Portal](https://portal.azure.com) > **Microsoft Entra ID** > **App registrations** > **New registration**
2. Set the **Name** (e.g., "BaGetter NuGet Feed")
3. Set **Supported account types** to "Accounts in this organizational directory only" (single tenant)
4. Set the **Redirect URI** to `https://your-bagetter-url/signin-oidc` (type: Web)
5. Click **Register**
6. Note the **Application (client) ID** and **Directory (tenant) ID** from the overview page

### Step 2: Create a client secret

1. In your App Registration, go to **Certificates & secrets** > **Client secrets** > **New client secret**
2. Add a description and expiration period
3. Copy the secret **Value** immediately (it will not be shown again)

### Step 3: Define App Roles (recommended)

App Roles let you manage admin access and group memberships in Entra ID instead of in BaGetter. Roles appear in the `roles` claim of the ID token.

1. In your App Registration, go to **App roles** > **Create app role**
2. Create roles matching your team structure:

| Display Name | Value | Allowed Member Types | Description |
|---|---|---|---|
| Administrator | `Admin` | Users/Groups | Full admin access to BaGetter |
| Frontend Team | `TeamFrontend` | Users/Groups | Auto-joins "Frontend Team" group |
| Backend Team | `TeamBackend` | Users/Groups | Auto-joins "Backend Team" group |

3. In **Enterprise Applications** > your app > **Users and groups**, assign users or Entra security groups to the appropriate App Roles.

:::info

The `Admin` App Role is special and hardcoded. Any user whose token contains a role with value `Admin` is automatically granted `IsAdmin = true` and full access to all feeds. This cannot be overridden. All other role values work through group membership and feed permissions.

:::

### Step 4: Configure BaGetter

Add the Entra configuration to `appsettings.json`:

```json
{
    "Authentication": {
        "Mode": "Entra",
        "Entra": {
            "Instance": "https://login.microsoftonline.com/",
            "TenantId": "<your-tenant-id>",
            "ClientId": "<your-client-id>",
            "ClientSecret": "<your-client-secret>",
            "CallbackPath": "/signin-oidc",
            "RoleClaim": "roles"
        }
    }
}
```

:::warning

Do not store the `ClientSecret` in `appsettings.json` in production. Use environment variables, Docker secrets, or a secrets manager instead:

```shell
# Environment variable
Authentication__Entra__ClientSecret=your-client-secret

# Docker secret file
/run/secrets/Authentication__Entra__ClientSecret
```

:::

### Entra configuration reference

| Setting | Required | Default | Description |
|---------|----------|---------|-------------|
| `Instance` | Yes | | The Entra ID instance URL (e.g., `https://login.microsoftonline.com/`) |
| `TenantId` | Yes | | Your tenant ID |
| `ClientId` | Yes | | The application (client) ID from your App Registration |
| `ClientSecret` | Yes | | The client secret value |
| `CallbackPath` | No | `/signin-oidc` | The OIDC callback path. Must match the redirect URI in your App Registration. |
| `RoleClaim` | No | `roles` | The token claim name to read App Roles from. Change only if your identity provider uses a non-standard claim name. |

### How Entra authentication works

When a user signs in via Entra ID:

1. The user is redirected to Microsoft's login page
2. After successful authentication, the OIDC token is validated
3. BaGetter automatically provisions a local user record linked to the Entra Object ID, capturing the user's email from the token's `email`, `mail`, or UPN claim (used for [token expiry notifications](#expiry-notifications))
4. The `roles` claim is read from the token
5. **Admin sync (bidirectional):** If the token contains the `Admin` role, `IsAdmin` is set to `true`. If not, `IsAdmin` is set to `false`. Admin status is always driven by the token: there is no way to persist admin for an Entra user outside of the App Role.
6. **Group membership sync (full reconciliation):** The user is added to all BaGetter groups whose `AppRoleValue` matches a role in the token, and removed from role-linked groups whose role is no longer present. Manually-managed groups (no `AppRoleValue`) are never touched.
7. A session cookie (`BaGetter.Auth`) is issued with a 60-minute sliding expiration

## Local accounts

When `Mode` is `Local` or `Hybrid`, administrators manage local accounts on **Admin > Accounts**:

- **Create** an account with a username, an optional display name, an optional email address (used for [token expiry notifications](#expiry-notifications)) and a password of at least 12 characters. Passwords are stored as bcrypt hashes.
- **Enable or disable** an account. Disabled accounts can't sign in or use their tokens.
- **Allow or block web sign-in** ("can log in to UI"). Turn it off for build agents that should only use NuGet clients.
- **Reset** the password, or **delete** the account.

### Account lockout

Local accounts are protected by an automatic lockout mechanism:

| Setting | Default | Description |
|---------|---------|-------------|
| `MaxFailedAttempts` | `5` | Number of consecutive failed logins before lockout |
| `LockoutMinutes` | `15` | Duration (in minutes) that the account remains locked |

```json
{
    "Authentication": {
        "Mode": "Local",
        "MaxFailedAttempts": 5,
        "LockoutMinutes": 15
    }
}
```

After `MaxFailedAttempts` consecutive failed logins, the account is locked for `LockoutMinutes`. The counter resets on successful login.

## Groups

Groups are managed on **Admin > Groups & Permissions**. A user inherits the permissions of every group they belong to. Groups come in two flavors:

- **Role-linked groups** have an `AppRoleValue` set (e.g., `TeamFrontend`). Membership for Entra users is synchronized from the token's `roles` claim on each sign-in and can't be changed by hand: it is controlled by the App Role assignments in Entra ID. Local users can still be added manually.
- **Manually-managed groups** have no `AppRoleValue`. Membership is managed entirely in the BaGetter admin UI, for all user types.

This lets Entra ID control *who has which role*, while BaGetter controls *what each role grants* on each feed.

## Feed permissions

In the `Local`, `Entra` and `Hybrid` modes every [feed](feeds.md) has its own permissions. They are granted to groups on **Admin > Groups & Permissions**, one row per feed:

| Permission | Allows |
|------------|--------|
| Pull | Browsing the feed in the web UI, search, and restoring or downloading packages and symbols |
| Push | Publishing packages and symbol packages |
| Delete | Unlisting or deleting packages (following the feed's [deletion behavior](feeds.md#feed-settings)) |

Administrators have all three on every feed. Feeds a user can't pull from are hidden from them in the UI. Requests without the needed permission get `401 Unauthorized` (not signed in) or `403 Forbidden`.

## Personal access tokens (PATs)

Personal access tokens let users authenticate from NuGet clients and CI without their interactive credentials. They are available to Entra and local users.

- Users create tokens on **My Tokens** (in the user menu), with a name and an expiry (90 days by default).
- The token (it starts with `bg_`) is shown only once, at creation time.
- Tokens are stored as SHA-256 hashes, and can be revoked at any time.
- A token acts as its owner: it has exactly the owner's permissions, and stops working when the owner is disabled or deleted.

### Token expiry

The maximum allowed token lifetime is controlled by the `MaxTokenExpiryDays` setting:

```json
{
    "Authentication": {
        "MaxTokenExpiryDays": 365
    }
}
```

### Expiry notifications

BaGetter can email token owners before their personal access tokens expire, so they can create a replacement before clients start failing authentication. This requires:

- [Email](configuration.md#email) to be configured (`Email:Type` set to `Smtp` or `Graph`). When email is disabled, the scanner does not run.
- The token owner to have a stored email address. Local users get one from the admin **Accounts** page; Entra users have it derived from their token's `email`, `mail`, or UPN claim on sign-in. Owners without an email address are skipped (with a warning logged).

A background scanner wakes every `ScanIntervalHours` and emails owners as each configured threshold (whole days before expiry) is crossed. Each threshold is sent at most once per token.

```json
{
    "Email": {
        "Type": "Smtp"
        // ... see the Email configuration section
    },
    "PatExpiryNotification": {
        "Enabled": true,
        "ScanIntervalHours": 24,
        "NotificationDaysBeforeExpiry": [ 14, 7, 2, 0 ],
        "WebBaseUrl": "https://packages.example.com"
    }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | `true` | Whether the scanner runs. When `false`, no scanning or emailing happens regardless of email configuration. |
| `ScanIntervalHours` | `1` | How often (in hours) the scanner looks for tokens nearing expiry. Minimum `1`. |
| `NotificationDaysBeforeExpiry` | `[14, 7, 2, 0]` | Thresholds, in whole days before expiry, at which an owner is emailed. `0` means the expiry day itself. Values must be distinct and zero or greater. |
| `WebBaseUrl` | | Public base URL of this site (e.g. `https://packages.example.com`), used to link owners to the token page. Must be an absolute `http(s)` URL when set. Omit for a name-only reference. |

:::info

The scanner runs outside an HTTP request and cannot infer the site URL, so `WebBaseUrl` must be configured for notification emails to include a working link.

:::

## Using BaGetter from NuGet clients

NuGet clients send a username and password (HTTP Basic) for restores, and an API key for pushes. What BaGetter accepts depends on the mode:

| Mode | Restore (username / password) | Push (`-k` API key) |
|---|---|---|
| `Config` | A `Credentials` entry, if any are configured | An `ApiKey`/`ApiKeys` value |
| `Local` | Your username and a PAT (recommended), or your account password | A PAT |
| `Entra` | Your username and a PAT as the password | A PAT |
| `Hybrid` | Your username and a PAT, or a local account's password | A PAT |

The **Connect** page of each feed shows the right instructions for the current mode.

Prefer a PAT over the account password on build agents and developer machines: it can expire, be revoked on its own, and failed PAT attempts never lock the account.

```shell
# Add the source (dotnet CLI)
dotnet nuget add source "https://your-bagetter-url/v3/index.json" \
    --name "BaGetter" \
    --username "<your-username>" \
    --password "<your-personal-access-token>"

# Push a package with a PAT
dotnet nuget push -s https://your-bagetter-url/v3/index.json \
    -k <your-personal-access-token> \
    package.1.0.0.nupkg
```

:::note

When a PAT is used as a password, the username must be the token owner's username. Use the service index of the [feed](feeds.md#feed-urls) you want, e.g. `https://your-bagetter-url/feeds/internal/v3/index.json`.

:::

## Full configuration reference

```json
{
    "Authentication": {
        "Mode": "Hybrid",
        "Entra": {
            "Instance": "https://login.microsoftonline.com/",
            "TenantId": "<tenant-id>",
            "ClientId": "<client-id>",
            "ClientSecret": "<client-secret>",
            "CallbackPath": "/signin-oidc",
            "RoleClaim": "roles"
        },
        "MaxTokenExpiryDays": 365,
        "MaxFailedAttempts": 5,
        "LockoutMinutes": 15,
        "Credentials": [
            {
                "Username": "legacy-user",
                "Password": "legacy-password"
            }
        ],
        "ApiKeys": [
            {
                "Key": "legacy-api-key"
            }
        ]
    }
}
```

:::info

The `Credentials` and `ApiKeys` arrays are only used when `Mode` is `Config`. When `Mode` is `Entra`, `Local`, or `Hybrid`, authentication is handled through the database-backed user system and PATs.

:::

## Environment variables

All authentication settings can be provided via environment variables using the double-underscore (`__`) separator:

| Environment Variable | Description |
|---------------------|-------------|
| `Authentication__Mode` | Authentication mode (`Config`, `Entra`, `Local`, `Hybrid`) |
| `Authentication__Entra__Instance` | Entra ID instance URL |
| `Authentication__Entra__TenantId` | Tenant ID |
| `Authentication__Entra__ClientId` | Application (client) ID |
| `Authentication__Entra__ClientSecret` | Client secret |
| `Authentication__Entra__CallbackPath` | OIDC callback path |
| `Authentication__Entra__RoleClaim` | Token claim name for App Roles (default: `roles`) |
| `Authentication__MaxTokenExpiryDays` | Maximum PAT lifetime in days |
| `Authentication__MaxFailedAttempts` | Failed login threshold for lockout |
| `Authentication__LockoutMinutes` | Lockout duration in minutes |

## Docker Compose example

```yaml
services:
  bagetter:
    image: letreset/bagetter:latest
    ports:
      - "5000:8080"
    environment:
      - Database__Type=PostgreSql
      - Database__ConnectionString=Host=db;Database=bagetter;Username=bagetter;Password=secret
      - Authentication__Mode=Entra
      - Authentication__Entra__Instance=https://login.microsoftonline.com/
      - Authentication__Entra__TenantId=your-tenant-id
      - Authentication__Entra__ClientId=your-client-id
      - Authentication__Entra__CallbackPath=/signin-oidc
      - Authentication__Entra__RoleClaim=roles
    volumes:
      - bagetter-data:/data
    secrets:
      - source: entra_client_secret
        target: Authentication__Entra__ClientSecret

volumes:
  bagetter-data:

secrets:
  entra_client_secret:
    file: ./secrets/entra-client-secret.txt
```

The secret is mounted at `/run/secrets/Authentication__Entra__ClientSecret`, which BaGetter reads as the `Authentication:Entra:ClientSecret` setting (see [Load secrets from files](configuration.md#load-secrets-from-files)).

## Database migrations

When upgrading from a version of BaGetter without authentication support, the required database tables (Users, Groups, UserGroups, PersonalAccessTokens, FeedPermissions) are created automatically on startup via EF Core migrations. No manual migration steps are needed. See [Upgrading from 1.x](upgrading.md).

## Troubleshooting

### "The 'TenantId' config is required for Entra authentication"

The `Authentication.Entra.TenantId` is missing or empty. Ensure your configuration or environment variables include the tenant ID from your Azure App Registration.

### "The 'ClientId' config is required for Entra authentication"

The `Authentication.Entra.ClientId` is missing or empty. Copy the Application (client) ID from the Azure Portal App Registration overview page.

### OIDC callback fails with "correlation failed"

This typically means the redirect URI in your Azure App Registration does not match the `CallbackPath` combined with your application's external URL. Verify:

1. The redirect URI in Azure is set to `https://your-external-url/signin-oidc`
2. Your reverse proxy (if any) is forwarding the `Host` header and the `X-Forwarded-Proto` header correctly
3. The application is using HTTPS in production

### Everyone is signed out after a restart

The Data Protection keys that protect the sign-in cookie are stored in package storage (`dataprotection/keyring.xml`). Make sure the storage (for Docker, the `/data` volume) is persistent and shared by all replicas.

### App Roles are missing from the token

If users are not getting admin permissions or group memberships despite being assigned App Roles:

1. Verify that **App Roles** are defined in the App Registration under **App roles**
2. Verify users are assigned to the roles in **Enterprise Applications** > your app > **Users and groups**
3. Check that the token includes the `roles` claim (use [jwt.ms](https://jwt.ms) to decode a token)
4. Ensure the `RoleClaim` config matches the claim name in your token (default: `roles`)
5. Verify that BaGetter groups have the correct `AppRoleValue` set to match the role values in the token

### Admin role not granting access

The admin role value is hardcoded as `Admin` (case-sensitive). Verify your App Role in Entra ID has exactly `Admin` as the **Value** (not the display name).

### Local account is locked out

If a local account is locked after too many failed attempts, wait for the lockout period to expire (`LockoutMinutes`, default 15 minutes). Resetting the password doesn't end the lockout early. Personal access tokens keep working while the account is locked.

### NuGet client returns 401 Unauthorized

1. Verify the NuGet source is configured with valid credentials for your mode (see [Using BaGetter from NuGet clients](#using-bagetter-from-nuget-clients))
2. Check that the user is in a group with `Pull` permission on the feed
3. If using a PAT, verify it has not expired or been revoked, and that the username matches the token owner
4. Ensure the `Mode` setting matches your authentication method (e.g., do not use `Credentials` when `Mode` is `Entra`)
