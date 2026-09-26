# UI test checklist

Run these checks against the [test environment](README.md) after UI changes and before a release, so a broken page is noticed early. Start from a fresh environment:

```bash
docker compose -f testenv/docker-compose.yml down -v
docker compose -f testenv/docker-compose.yml up -d
```

Each check names the account to use (passwords are in [README.md](README.md)) and the expected result. Checks marked **Known issue** currently fail because of the linked issue; they should pass once it is fixed. Agents run these checks in the browser through the chrome-devtools MCP server, with one isolated browser context per account.

For every page you open, the browser console should show no errors.

## Sign-in

| # | Account | Steps | Expected |
|---|---|---|---|
| S1 | none | Open `/` | "Sign in required" and a **Sign in** button, no packages or feed names, and the page title is "Sign in required - BaGetter" |
| S2 | none | Sign in as `admin` with a wrong password | "Invalid username or password.", the username is kept |
| S3 | none | Sign in as `admin` | Lands on the first feed in the Admin > Feeds order, user menu shows `admin` |
| S4 | none | Open `/Login?ReturnUrl=https%3A%2F%2Fexample.com%2F` and sign in | Stays on the BaGetter site |
| S5 | none | Sign in as `build-agent` | Refused: this account can't sign in to the web UI |
| S6 | `admin` | User menu > **Sign out** | Back to the sign-in prompt |
| S7 | none | Sign in as `bob` with a wrong password 5 times, then with the right one | "This account is locked due to too many failed attempts" (resets after 15 minutes or with `down -v`) |

## Navigation

| # | Account | Steps | Expected |
|---|---|---|---|
| N1 | `admin` | Open the feed switcher | Default, Internal, Experimental, Archive in that order |
| N2 | `carol` | Look at the tabs on Internal | Packages, Connect, Statistics. No Upload (no push permission) |
| N3 | `carol` | Look at the tabs on Experimental | Packages, Connect, Upload, Statistics |
| N4 | `carol` | Open `/Admin/Feeds` | Redirected to the package list |
| N5 | `admin` | Open **Statistics** on Internal | 22 packages, 28 versions (including the unlisted one), and the services in use |
| N6 | `admin` | Open `/feeds/nope/` | 404 |

## Package list, search and filters

| # | Account | Steps | Expected |
|---|---|---|---|
| L1 | `admin` | Open Internal | 20 packages and a page link **2**; page 2 lists the remaining 2 |
| L2 | `admin` | Search `logging` | Contoso.Logging |
| L3 | `admin` | Search `LOGGING` | Same result (case-insensitive) |
| L4 | `admin` | Search `observability` | Contoso.Logging and Contoso.Metrics. **Known issue** [#44](https://github.com/letreset/BaGetter/issues/44): search only matches ids |
| L5 | `admin` | Tag filter: type `obs` in the dropdown and pick `observability` | Contoso.Logging and Contoso.Metrics |
| L6 | `admin` | Framework filter: `.NET Standard 2.0` | All Contoso packages |
| L7 | `admin` | Framework dropdown on Internal | `.NET 10.0`, `.NET 8.0`, `.NET Standard 2.0`, in that order. Older frameworks from real packages show raw monikers: [#45](https://github.com/letreset/BaGetter/issues/45) |
| L8 | `admin` | Experimental, clear **Include prerelease** | No packages left (all of them are prereleases) |
| L9 | `admin` | Open `/feeds/internal?p=99` | An empty page with a message. **Known issue** [#45](https://github.com/letreset/BaGetter/issues/45) |

## Package page

| # | Account | Steps | Expected |
|---|---|---|---|
| P1 | `admin` | Open Internal > Contoso.Logging | Version 2.0.0, install tabs (.NET CLI, PackageReference, Paket CLI, Package Manager), copy button works |
| P2 | `admin` | Expand **Readme** | The Contoso.Logging readme with a code block |
| P3 | `admin` | Expand **Dependencies** | Contoso.Core (>= 2.0.0), grouped by target framework |
| P4 | `admin` | Open Contoso.Core, expand **Used By** | Contoso.Logging and Contoso.Configuration |
| P5 | `admin` | Versions of Contoso.Logging | 2.0.0, 1.5.0, and 1.4.0 struck through as "(unlisted)" with **Relist** |
| P6 | `carol` | Versions of Contoso.Logging | 2.0.0 and 1.5.0 only, no Manage section |
| P7 | `admin` | Open `/feeds/internal/packages/Contoso.Logging/99.0.0` | "Version not found". **Known issue** [#40](https://github.com/letreset/BaGetter/issues/40): shows 2.0.0 |
| P8 | `admin` | **Download package** | Downloads `contoso.logging.2.0.0.nupkg` |

## Package management

| # | Account | Steps | Expected |
|---|---|---|---|
| M1 | `admin` | Contoso.Logging 1.4.0 > **Relist** | 1.4.0 is listed again; **Unlist** it again afterwards |
| M2 | `alice` | Internal > Contoso.Testing > **Unlist**, then **Relist** | Both work (Package owners have delete on Internal) |
| M3 | `carol` | Experimental > Contoso.Preview.Ai 0.1.0-alpha.1 > **Delete** | Confirmation, then the version is gone |
| M4 | `admin` | Archive (read-only) > Contoso.Legacy > **Unlist** | Refused. **Known issue** [#38](https://github.com/letreset/BaGetter/issues/38): the version is unlisted |
| M5 | `admin` | Do M1, then check the container log (`docker compose -f testenv/docker-compose.yml logs bagetter`) | An `AUDIT package_relist_succeeded` line. **Known issue** [#39](https://github.com/letreset/BaGetter/issues/39) |

## Connect and Upload

| # | Account | Steps | Expected |
|---|---|---|---|
| C1 | `carol` | Internal > **Connect** | Service index `http://localhost:5000/feeds/internal/v3/index.json`, copy button, tabs for .NET CLI, NuGet, nuget.config, Paket |
| C2 | `carol` | Read the authentication text, then open **My Tokens** in the user menu | The text points to My Tokens; the page opens, and a new token is shown once and can be revoked |
| C2a | `admin` | Admin > Accounts > `build-agent` > **New token** | The token is shown once in the success message |
| C3 | `carol` | Experimental > **Upload** | Push commands for the Experimental service index |

## Admin > Feeds

| # | Account | Steps | Expected |
|---|---|---|---|
| F1 | `admin` | Create a feed with slug `Bad Slug` | Validation message, nothing created |
| F2 | `admin` | Create a feed with slug `internal` | "already exists" |
| F3 | `admin` | Create `ui-test`, drag it above Internal, reload | The new order is kept, also in the feed switcher |
| F4 | `admin` | Edit details of `ui-test`, then delete it | Both work; the feed disappears from the switcher |

## Admin > Feed settings

| # | Account | Steps | Expected |
|---|---|---|---|
| FS1 | `admin` | Open Internal > Settings | Overwrite policy **Prerelease only** and Max major versions **3**, with their "Use global default" boxes cleared |
| FS2 | `admin` | Press **Save Settings** without changes, reopen | Same values as FS1 |
| FS2a | `admin` | Tick **Use global default** for Max major versions, clear it again | The field shows `3` again |
| FS3 | `admin` | Enter `-3` for Max major versions and save | Validation message. **Known issue** [#36](https://github.com/letreset/BaGetter/issues/36) |
| FS4 | `admin` | Default > Settings > Mirrors | One mirror, `https://api.nuget.org/v3/index.json`, enabled |
| FS5 | `admin` | Add a mirror with the URL `not a url` and save | "the package source must be an absolute http(s) URL" |
| FS6 | `admin` | Add two mirrors, move the second up with the arrow, remove one | Titles renumber (Mirror 1, Mirror 2); do `down -v` afterwards |

## Admin > Accounts

| # | Account | Steps | Expected |
|---|---|---|---|
| A1 | `admin` | Create `dave` with the password `short` | "Password must be at least 12 characters." |
| A2 | `admin` | Create `ALICE` with a valid password | "already exists". **Known issue** [#41](https://github.com/letreset/BaGetter/issues/41) on SQLite |
| A3 | `admin` | Disable `bob`, then check a signed-in `bob` session | `bob` is signed out on the next request; enable again |
| A4 | `admin` | Disable `dave` and delete him | The confirmation shows the username; the account is gone |
| A5 | `admin` | Look at the `admin` row | No Disable button for the last administrator. **Known issue** [#42](https://github.com/letreset/BaGetter/issues/42) |
| A6 | `admin` | `bob` > **Reset password** with a 12+ character password, then sign in as `bob` with it | "Password of 'bob' reset successfully.", and the sign-in works (also right after S7's lockout) |

## Admin > Groups & Permissions

| # | Account | Steps | Expected |
|---|---|---|---|
| G1 | `admin` | Open Developers > **Feed Permissions** | Pull on Default, Internal, Archive; Pull, Push, Delete on Experimental |
| G2 | `admin` | Create a group `Developers` | "already exists" |
| G3 | `admin` | Remove `carol` from Developers, then check `carol` | `carol` sees "No feeds available"; add her back and she sees all four feeds again |
| G4 | `admin` | Clear Pull on Internal for Developers and **Save all** | `bob` no longer sees Internal; restore it |

## Access control

| # | Account | Steps | Expected |
|---|---|---|---|
| X1 | none | Open `/feeds/internal/packages/Contoso.Logging` | Sign-in prompt, and the page title doesn't name the package |
| X2 | `carol` | Open `/feeds/internal/packages/Contoso.Logging` | No Manage section; a crafted Unlist POST returns 403 |
| X3 | none | `curl -u carol:<password> -X PUT -F package=@testenv/packages/Contoso.Mail.1.0.0.nupkg http://localhost:5000/feeds/internal/api/v2/package` | 403. **Known issue** [#46](https://github.com/letreset/BaGetter/issues/46): 401 |

## Mirror

| # | Account | Steps | Expected |
|---|---|---|---|
| R1 | `admin` | Open Default > `/packages/Newtonsoft.Json` (needs internet) | The package page with nuget.org's versions |
| R2 | `admin` | Look at the versions | Upstream-only versions are marked and have no Relist. **Known issue** [#40](https://github.com/letreset/BaGetter/issues/40) |

## Small screens

| # | Account | Steps | Expected |
|---|---|---|---|
| Y1 | `admin` | Emulate a 390 x 844 viewport, open Internal, a package page, Connect and Admin > Accounts | No horizontal scrolling, collapsed navigation. **Known issue** [#43](https://github.com/letreset/BaGetter/issues/43) |

## Security

| # | Account | Steps | Expected |
|---|---|---|---|
| Z1 | `admin` | Create an account named `x'+(document.title='pwned')+'`, disable it, click **Delete** and cancel | The confirmation shows the username literally; the page title doesn't change |
