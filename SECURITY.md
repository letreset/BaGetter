# Security Policy

## Supported versions

Security fixes are made on `main` and shipped in the next release. Only the latest release line receives fixes.

| Version | Supported |
|---------|-----------|
| Latest 2.x release | Yes |
| Older 2.x releases | No, please upgrade |
| 1.x (upstream bagetter/BaGetter) | No, not maintained here |

## Reporting a vulnerability

**Please do not report security vulnerabilities through public GitHub issues, discussions or pull requests.**

Report them privately through GitHub's [private vulnerability reporting](https://github.com/letreset/BaGetter/security/advisories/new) form.

Include as much of the following as you can:

- The affected version (image tag, release or commit) and deployment type (Docker, Helm, IIS, `dotnet run`).
- The relevant configuration: `Authentication:Mode`, database, storage and any reverse proxy in front of BaGetter. Remove secrets first.
- The type of issue (for example authentication bypass, privilege escalation across feeds, XSS, path traversal) and its impact.
- Step-by-step instructions or a proof of concept to reproduce it.

## What to expect

- We aim to acknowledge a report within 5 working days.
- We will confirm the issue, agree on the severity with you and keep you updated while a fix is prepared.
- Once a fixed release is available, we publish a GitHub security advisory. We credit reporters by GitHub username unless they ask to stay anonymous.

Please give us a reasonable amount of time to release a fix before disclosing the issue publicly.

## Scope

This policy covers the code in this repository: the BaGetter server, the Docker image `letreset/bagetter` and the Helm chart. Vulnerabilities in upstream projects (bagetter/BaGetter, NuGet client libraries, .NET) should be reported to those projects.
