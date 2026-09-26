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
| N5 | `admin` | Open **Statistics** on Internal | 22 packages, 28 versions (including the unlisted one), "28 stable, 0 prerelease (1 unlisted)", 171 total downloads, the stored package size and the services in use. **Most downloaded** starts with Contoso.Core (50), Contoso.Logging (47), Contoso.Testing (22); **Recently published** lists 10 versions, newest first, without Contoso.Logging 1.4.0 |
| N6 | `admin` | Open `/feeds/nope/` | 404 |
| N7 | none | Open the theme menu (palette icon) on the sign-in page | BaGetter Light and BaGetter Dark, then the 16 Bootswatch themes (Cerulean to Yeti); the active one is highlighted. Without a saved choice it follows the system light/dark setting |
| N8 | `admin` | Pick **Flatly**, open Internal > Contoso.Logging, then switch to Experimental and reload | The page reloads in Flatly (green navigation bar, theme font) and keeps it across feeds and reloads; the install tabs, copy button and search button use the theme colors |
| N9 | `admin` | Pick **Darkly**, then **Superhero**, and open Admin > Feeds and Admin > Accounts | Dark pages with readable tables, forms and user menu; no leftover BaGetter blue or orange. Pick **BaGetter Light** afterwards |

## Package list, search and filters

| # | Account | Steps | Expected |
|---|---|---|---|
| L1 | `admin` | Open Internal | 20 packages and a page link **2**; page 2 lists the remaining 2 |
| L2 | `admin` | Search `logging` | Contoso.Logging |
| L3 | `admin` | Search `LOGGING` | Same result (case-insensitive) |
| L4 | `admin` | Search `observability` | Contoso.Logging and Contoso.Metrics (tag and description matches) |
| L5 | `admin` | Tag filter: type `obs` in the dropdown and pick `observability` | Contoso.Logging and Contoso.Metrics |
| L6 | `admin` | Framework filter: `.NET Standard 2.0` | All Contoso packages |
| L7 | `admin` | Framework dropdown on Internal | `.NET 10.0`, `.NET 8.0`, `.NET Standard 2.0`, in that order. On Default (with mirrored packages) every entry has a readable name, grouped by family, newest first; after R3 the portable profiles read e.g. `.NET Portable (net45, win8, wp8, wpa81)` |
| L8 | `admin` | Experimental, clear **Include prerelease** | No packages left (all of them are prereleases) |
| L9 | `admin` | Open `/feeds/internal?p=99` | "No packages found" with a link to the first page |

## Package page

| # | Account | Steps | Expected |
|---|---|---|---|
| P1 | `admin` | Open Internal > Contoso.Logging | Version 2.0.0, install tabs (.NET CLI, Package Manager, PackageReference, CPM, Paket CLI, Script & Interactive, File-based Apps, Cake) on one line on a desktop screen, lined up with the command box, copy button works |
| P2 | `admin` | Expand **Readme** | The Contoso.Logging readme with a code block |
| P3 | `admin` | Expand **Dependencies** | Contoso.Core (>= 2.0.0), grouped by target framework |
| P4 | `admin` | Open Contoso.Core, expand **Used By** | Contoso.Logging and Contoso.Configuration |
| P5 | `admin` | Versions of Contoso.Logging | 2.0.0, 1.5.0, and 1.4.0 struck through as "(unlisted)" with **Relist** |
| P6 | `carol` | Versions of Contoso.Logging | 2.0.0 and 1.5.0 only, no Manage section |
| P7 | `admin` | Open `/feeds/internal/packages/Contoso.Logging/99.0.0` | "Version not found" with a link to the latest version (also for `/not-a-version`) |
| P8 | `admin` | **Download package** | Downloads `contoso.logging.2.0.0.nupkg` |
| P9 | `admin` | Look under the Contoso.Logging title, hover a badge | Badges `.NET 8.0` and `.NET Standard 2.0`; the tooltip says the package is compatible with that framework or higher |
| P10 | `admin` | Expand **Frameworks** on Contoso.Logging | `.NET 10.0`, `.NET 8.0`, `.NET Standard 2.0`, in that order |
| P11 | `admin` | Contoso.Logging > **CPM** and **Cake** tabs, copy each | One line per entry (`PackageVersion` and `PackageReference`; `#addin` and `#tool`), and the copied text has the same lines. **Package Manager** shows `NuGet\Install-Package Contoso.Logging -Version 2.0.0` |
| P12 | `admin` | Experimental > Contoso.Preview.Ai > **Cake** tab | Both lines end with `&prerelease` (no `&amp;`) |
| P13 | `admin` | Experimental > Contoso.Preview.Ai, then Internal > Contoso.Logging | "This is a prerelease version of Contoso.Preview.Ai." under the install box; no such note on Contoso.Logging |
| P14 | `admin` | Statistics of Contoso.Logging | 47 total downloads, 35 of the current version, and a "per day average" line |
| P15 | `admin` | Default > `/packages/Newtonsoft.Json` (needs internet), clear **Include prerelease** | Only the prerelease rows disappear, the remaining stable versions are all shown (no 5-row cap) and **Show more** is hidden; ticking it again restores the list. Contoso.Logging (no prereleases) has no checkbox |
| P16 | `admin` | Info of Internal > Contoso.Logging (on an image with this change the startup backfill fills the snapshot's packages) | The license link reads "MIT license" and opens `https://licenses.nuget.org/MIT`; **Download package** is followed by the size, e.g. "(8.21 KB)" |
| P17 | `carol` | Pack a package with `<Copyright>Copyright (c) Contoso</Copyright>` and push it to Experimental, then open it | A **Copyright** section in the sidebar with that text |
| P18 | `admin` | Contoso.Logging > **Atom feed** in the sidebar; then `curl -u carol:<password> http://localhost:5000/feeds/internal/packages/Contoso.Logging/atom.xml` | The link points to `/feeds/internal/packages/contoso.logging/atom.xml` and the page head has a `<link rel="alternate" type="application/atom+xml">`; curl returns an Atom feed with 2.0.0 and 1.5.0 (not the unlisted 1.4.0), and without `-u` it gets 401 |

## Package management

| # | Account | Steps | Expected |
|---|---|---|---|
| M1 | `admin` | Contoso.Logging 1.4.0 > **Relist** | 1.4.0 is listed again; **Unlist** it again afterwards |
| M2 | `alice` | Internal > Contoso.Testing > **Unlist**, then **Relist** | Both work (Package owners have delete on Internal) |
| M3 | `carol` | Experimental > Contoso.Preview.Ai 0.1.0-alpha.1 > **Delete** | Confirmation, then the version is gone |
| M4 | `admin` | Archive (read-only) > Contoso.Legacy > **Unlist** | No Manage section and no Relist links; a crafted Unlist POST returns 403 and the version stays listed |
| M5 | `admin` | Do M1, then check the container log (`docker compose -f testenv/docker-compose.yml logs bagetter`) | An `AUDIT package_relist_succeeded` line (and `package_unlist_succeeded` for the unlist), with `actor=admin` |
| M6 | `admin` | Disable and enable `bob` on Admin > Accounts, then check the container log | `AUDIT account_disabled target=bob` and `AUDIT account_enabled target=bob` lines with `actor=admin` |

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
| FS3 | `admin` | Enter `-3` for Max major versions and save | A message next to the field, "nothing was saved", and the stored value is unchanged |
| FS4 | `admin` | Default > Settings > Mirrors | One mirror, `https://api.nuget.org/v3/index.json`, enabled |
| FS5 | `admin` | Add a mirror with the URL `not a url` and save | "the package source must be an absolute http(s) URL" |
| FS6 | `admin` | Add two mirrors, move the second up with the arrow, remove one | Titles renumber (Mirror 1, Mirror 2); do `down -v` afterwards |

## Admin > Accounts

| # | Account | Steps | Expected |
|---|---|---|---|
| A1 | `admin` | Create `dave` with the password `short` | "Password must be at least 12 characters." |
| A2 | `admin` | Create `ALICE` with a valid password | "already exists", and signing in as `Alice` works like `alice` |
| A3 | `admin` | Disable `bob`, then check a signed-in `bob` session | `bob` is signed out on the next request; enable again |
| A4 | `admin` | Disable `dave` and delete him | The confirmation shows the username; the account is gone |
| A5 | `admin` | Look at the `admin` row | An **Admin** label, and no Disable, Revoke Web Access or Remove admin button on the signed-in admin's own row |
| A5a | `admin` | `alice` > **Make admin**, sign in as `alice` and open Admin > Accounts; then **Remove admin** as `admin` | `alice` can open the page while she is an admin |
| A5b | `admin` | After S7 (`bob` locked), open Admin > Accounts and **Unlock** `bob` | A "Locked until" label before; `bob` can sign in right away after |
| A6 | `admin` | `bob` > **Reset password** with a 12+ character password, then sign in as `bob` with it | "Password of 'bob' reset successfully.", and the sign-in works (also right after S7's lockout) |
| A7 | `admin` | Look at the account table, hover the action buttons | Headers, dates and usernames stay on one line; every row has **New token** and **Reset password** on the first line and the state buttons below. The actions are colored icon buttons whose names show on hover |
| A8 | `admin` | Open **New token** on `build-agent` (the last row), press Esc, open it again, type a name and press Enter | A small popup over the page with the cursor in the name field and nothing cut off; Esc closes it; Enter creates the token |

## Admin > Groups & Permissions

| # | Account | Steps | Expected |
|---|---|---|---|
| G1 | `admin` | Open Developers > **Feed Permissions** | Pull on Default, Internal, Archive; Pull, Push, Delete on Experimental |
| G2 | `admin` | Create a group `Developers`, then `developers` | "already exists" both times |
| G3 | `admin` | Remove `carol` from Developers, then check `carol` | `carol` sees "No feeds available"; add her back and she sees all four feeds again |
| G4 | `admin` | Clear Pull on Internal for Developers and **Save all** | `bob` no longer sees Internal; restore it |
| G5 | `admin` | Do G4, then check the container log | One `AUDIT feed_permission_revoked` line for Internal and one `feed_permission_set` line for the restore, no lines for the unchanged feeds |
| G6 | `admin` | Create a group, open its delete confirmation in two tabs, confirm in both | The first says "Group deleted successfully.", the second "Group not found." |

## Access control

| # | Account | Steps | Expected |
|---|---|---|---|
| X1 | none | Open `/feeds/internal/packages/Contoso.Logging` | Sign-in prompt, and the page title doesn't name the package |
| X2 | `carol` | Open `/feeds/internal/packages/Contoso.Logging` | No Manage section; a crafted Unlist POST returns 403 |
| X3 | none | `curl -u carol:<password> -X PUT -F package=@testenv/packages/Contoso.Mail.1.0.0.nupkg http://localhost:5000/feeds/internal/api/v2/package` | 403 (the same request without `-u` gets 401) |

## Mirror

| # | Account | Steps | Expected |
|---|---|---|---|
| R1 | `admin` | Open Default > `/packages/Newtonsoft.Json` (needs internet) | The package page with nuget.org's versions |
| R2 | `admin` | Look at the versions | Versions not stored in the feed carry a **mirror** label, no downloads, no `1900-01-01` dates and no Relist; upstream-unlisted versions aren't listed; opening a mirror-only version shows no Manage section |
| R3 | `admin` | Download a mirror-only version, e.g. `curl -u admin:<password> -o NUL http://localhost:5000/v3/package/newtonsoft.json/12.0.1/newtonsoft.json.12.0.1.nupkg`, then reload the Newtonsoft.Json page | 12.0.1 loses the **mirror** label and keeps its publish date from nuget.org (in 2018), not today's |

## Small screens

| # | Account | Steps | Expected |
|---|---|---|---|
| Y1 | `admin` | Emulate a 375 x 812 viewport, open Internal, a package page, Connect, Upload, Statistics, Admin > Accounts and Admin > Feeds | No horizontal page scrolling (wide admin tables scroll inside their box), the navigation is collapsed behind the menu button and opens with it |

## Security

| # | Account | Steps | Expected |
|---|---|---|---|
| Z1 | `admin` | Create an account named `x'+(document.title='pwned')+'`, disable it, click **Delete** and cancel | The confirmation shows the username literally; the page title doesn't change |
