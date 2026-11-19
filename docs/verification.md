# Verification record

Local verification date: **2026-11-19**, Asia/Colombo. This file records observed results rather than a claim of production readiness.

## Environment

- Windows PowerShell 5.1
- .NET SDK 10.0.102, installed runtime 10.0.2
- Node.js 24.13.0, npm 11.6.2
- Docker Desktop / engine 29.5.3, Linux containers
- Angular 21.2.26, CLI/build 21.2.30
- EF Core / ASP.NET packages and local EF tool 10.0.15

## Checks

| Check | Result |
| --- | --- |
| Backend solution build | Passed, zero warnings/errors |
| xUnit unit and HTTP/SQLite integration tests | **65 passed**, zero failures/skips, including final OpenAPI security and schema-limit assertions |
| .NET dependency vulnerability scan, including transitive dependencies | No vulnerable packages reported by configured sources |
| Angular production build | **Passed** on final source; initial bundle 196.96 kB, estimated transfer 57.25 kB |
| Angular unit tests | **11 passed** on final source with patched Vitest 4.1.12 |
| npm clean install and audit | **Passed**, 473 packages installed, zero vulnerabilities after patched Vitest and glob/minimatch overrides |
| Localhost API smoke check | Passed: readiness, services, hourly history, incidents and OpenAPI |
| OpenAPI export | **Passed**, OpenAPI 3.0.4; actual write-key scheme and request limits verified; six services and three incidents in smoke check |
| EF model/migration consistency | **Passed**, no pending model changes |
| PowerShell scripts | All three scripts parse; smoke and OpenAPI export executed successfully |
| Docker image build | **Passed**, final multi-stage image `opspulse-api:local`, including Linux restore/publish |
| Docker runtime and restart | **Passed**, readiness and full API smoke on loopback; uid/gid 1654 (`app`); SQLite state and three incident seeds preserved across restart; verification container stopped |
| Browser integration checks | **2 passed** using installed Microsoft Edge via Playwright: real API data, filters, 390px mobile layout and connection-failure behavior; screenshots reviewed |
| GitHub Actions workflow | Triggered on first publication; backend and Docker passed, frontend clean install failed on missing optional lockfile entries; see publication record and Actions for subsequent results |
| Local Choreo API Manager routing | Anonymous status JSON and services reads returned 200; anonymous incident listing returned 401 at `https://localhost:8243/opspulse/1.0.0` |

## Publication recheck on 2026-11-19

The source was rechecked before its first public Git push. No application behavior was changed during repository preparation.

| Check | Observed result |
| --- | --- |
| Release solution build | Passed, zero warnings/errors |
| xUnit tests | 65 passed, zero failures/skips; includes incident create/resolve against isolated SQLite |
| Angular production build | Passed, initial bundle 196.96 kB |
| Vitest | 11 passed across three test files |
| Playwright / installed Edge | 2 passed against the existing local backend and frontend |
| Docker rebuild | Passed as `opspulse-api:publication` |
| Fresh container smoke | Readiness, six services, hourly history, three incidents and OpenAPI passed on loopback port 5082 |
| Container execution | uid/gid 1654 (`app`), loopback-only port mapping; verification container stopped |
| Dependency audits | NuGet: no vulnerable packages reported; npm: zero vulnerabilities |
| Publication security review | All candidate source/docs read by the review script; no untriaged configured secret-pattern matches; real screenshots visually reviewed |
| Screenshots | New real Playwright desktop/mobile captures copied into `docs/screenshots/` |
| Gateway public status | HTTP 200 with six services, `isDemo: true`, and three recent incidents |
| Gateway public services | HTTP 200 |
| Gateway anonymous incident list | HTTP 401 |
| Gateway successful OAuth and incident mutations | Not tested; no OAuth token read or used |
| Rate limiting / production deployment | Not verified / not performed |

Commands and publication boundaries are recorded in [publication-review.md](publication-review.md). Hosted GitHub Actions results are separate from local verification and can be inspected in the repository's Actions tab.

## Failures encountered and corrections

- The first backend restore failed because NuGet audit treated vulnerable transitive `Microsoft.OpenApi` and native SQLite packages as errors. Updated the .NET 10 package patch versions and the compatible SQLite bundle. Vulnerability checking remains enabled; the subsequent audit reports no vulnerable packages.
- An intermediate restore rejected an explicitly pinned OpenAPI dependency below the newer ASP.NET package's minimum. Removed the unnecessary direct pin and used the patched transitive version required by ASP.NET.
- Initial incident integration tests returned 500 because record property validation attributes were incompatible with MVC constructor validation. Corrected validation, then used required request properties so runtime validation and generated OpenAPI constraints agree.
- Polling against the integration host's shared SQLite connection attempted concurrent transactions. Kept probes concurrent, but persisted observations in one SQLite write batch. The polling/retention integration test subsequently passed.
- The updated OpenAPI library emits 3.0.4. Changed assertions to verify the 3.0 contract family rather than a particular patch identifier. Adjusted the distinct-path assertion to count paths separately from HTTP operations.
- Inspecting the actual exported document found an unresolved security-scheme reference and missing request length constraints. Bound the security reference to the generated document, used required validated request properties, and strengthened the integration assertions for actual scheme names and limits.
- The initial npm install reported 18 moderate development-tool advisories. Updated Vitest and added patched glob/minimatch overrides; the dependency resolution audit then reported zero vulnerabilities. The overrides are project-local and covered by the subsequent build/tests.
- A clean npm install correctly rejected the old lockfile after those intentional package changes. Regenerated the lockfile, then repeated the clean project-local install.
- The first live smoke request preceded the API listener becoming ready. The same smoke check passed once startup completed.
- The first pending-migration CLI check mistakenly used `-c Release`, where EF interprets `-c` as a context name. Corrected the command to `--configuration Release`.
- Docker Desktop temporarily failed to resolve `mcr.microsoft.com` while Windows DNS could resolve it. A retry reached image/package restore without changing Docker or host network settings; the final container result is recorded above.
- The first container readiness window expired during cold startup. Subsequent IPv4 and localhost readiness checks and full smoke checks passed, including after restart.
- An optional local CI YAML check initially could not find a YAML parser in the Angular dependencies. Parsed the workflow successfully with a temporary npm CLI tool, without adding an application dependency or global installation.
- Windows treats `data/` and the source `Data/` as the same directory. Changed the local runtime database location to ignored `storage/` and avoided ignoring the source directory. No database files were deleted.
- The first hosted CI run exposed missing optional `@emnapi/core` and `@emnapi/runtime` entries in the Windows-generated npm lockfile. Regenerated lock metadata in an isolated Linux container; exactly those two packages were added, with no existing dependency version changed. Linux lockfile regeneration/audit and install dry-run passed. Hosted CI is rerun on the corrected commit; its actual result is available in Actions.

## Retained local artifacts

Local Docker verification images and stopped verification containers remain available. Temporary results, coverage, Docker logs and the isolated Git ignore-validation repository are under ignored output directories; approved screenshots are separately copied into `docs/screenshots/`. No service was publicly deployed by these checks. Repository preparation reused and preserved the user's existing local API, dashboard and gateway processes. Source files and migrations were checked against Git ignore rules; databases, dependencies, private configuration and `.env` are ignored, while source and redacted examples remain includable.

## Limits

Demo probes are deterministic simulations, and the default application makes no outbound monitoring calls. Address safety and endpoint rules are tested locally; no live external probe target, real operator credential or production deployment was used. The local machine's installed .NET runtime is older than the selected package patches; use a current patched .NET 10 SDK/runtime for regular use. Gateway checks verify anonymous reads/rejection only. Successful OAuth/scopes, gateway incident mutations and throttling still require separate verification. GitHub-hosted CI is reported separately from local test results.
