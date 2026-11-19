# Repository publication review

Date: **2026-11-19** (Asia/Colombo). Target: [Dimeshmethsara/opspulse-api-management](https://github.com/Dimeshmethsara/opspulse-api-management).

## Scope and history

The workspace initially had no Git repository, commits, branches or remotes. The nested bare repository under ignored `artifacts/` was a prior ignore-rule test, not application history. The target GitHub repository returned not found when inspected under the authenticated owner account. No existing application or remote history was overwritten, reset or force-pushed.

Commit attribution is configured locally as **Dimesh Methsara Opanayaka**, using the authenticated account's ID-based GitHub noreply address. Global author settings are unchanged. GitHub CLI authentication uses its existing OS keyring entry; no personal access token is requested, stored in source, or printed.

## Reviewed publication contents

The candidate set includes the .NET and Angular source, EF migration, tests, package manifests/lockfile, Dockerfile, workflow, PowerShell scripts, public OpenAPI contract, safe examples, documentation and two real dashboard PNG captures. Application behavior was preserved.

Excluded content includes database/WAL files, `bin`, `obj`, `node_modules`, build/cache/test outputs, temporary artifacts, logs, `.env` files, private configuration, credentials, certificates/keys/keystores, archives, downloaded API Manager installations and local editor configuration. The safe `.env.example` contains no active key. The sample `incident.json` contains a fictional incident only.

`scripts/review-publication.mjs` reads every Git candidate, inventories its path/size/hash, rejects forbidden runtime/private artifacts, and searches configured patterns for GitHub tokens, JWTs, private keys, AWS keys, password-bearing connection strings, literal credentials and credential-bearing URLs. It reports only paths, rule names and line numbers. Its exact `user:secret` URL exceptions are deliberate negative test fixtures and the scanner's own fixture comparison. These strings are not real credentials. The in-memory test host's fixed write key is test-only and is not used by runtime configuration.

Candidate and staged scans are separate. Private review manifests/logs stay under ignored `artifacts/`; screenshots were visually reviewed before inclusion. This is a best-effort review, not a guarantee against every possible secret pattern or a production penetration test. There was no existing application commit history in which to find a previously committed secret. If a real committed secret is discovered later, revoke/rotate it, coordinate removal from affected history and copies, and re-review before further publication.

## Substantive commands and results

These are the commands used for repository/authentication inspection and verification. Read-only source-file reads and output polling are omitted from this table. No credential values are included.

| Command | Result |
| --- | --- |
| `git rev-parse --show-toplevel`; `git status --short --branch`; `git remote -v`; `git branch -avv`; `git log -5 --format='%h %ad %s' --date=iso-strict` | Initially reported no repository; no history to preserve |
| `gh --version`; `gh auth status` | GitHub CLI 2.87.3, authenticated as the intended owner using the OS keyring |
| `gh api user --jq '{login: .login, id: .id, created_at: .created_at}'` | Confirmed owner/account ID for noreply attribution |
| `gh repo view Dimeshmethsara/opspulse-api-management --json nameWithOwner,url,isEmpty,isPrivate,defaultBranchRef,description` | Target initially did not exist |
| `gh api repos/Dimeshmethsara/opspulse-api-management --jq '{full_name: .full_name, private: .private, default_branch: .default_branch}'` | Confirmed initial HTTP 404 under owner authentication |
| `git init --initial-branch=main` | Created a new local repository |
| `git config --local user.name 'Dimesh Methsara Opanayaka'` | Set repository-local author name |
| `git config --local user.email '188300635+Dimeshmethsara@users.noreply.github.com'` | Set account-ID noreply address; personal email not used in the commit |
| `git ls-files --others --exclude-standard` | Enumerated publication candidates without ignored outputs |
| `node scripts/review-publication.mjs` | Candidate review passed after triaging exact negative-test URL fixtures |
| `git check-ignore -v backend/OpsPulse.Api/storage/opspulse.db backend/OpsPulse.Api/Data/opspulse.db .env secret.pfx choreo-api-manager-local/INSTALL.txt downloads/wso2.zip frontend/node_modules/package.json artifacts/ignore-validation.git/config` | Sensitive/runtime example paths are ignored |
| `dotnet build OpsPulse.slnx -c Release` | Passed, zero warnings/errors |
| `dotnet test backend/OpsPulse.Tests -c Release --no-build --collect:'XPlat Code Coverage' --results-directory artifacts/publication-tests --logger:'console;verbosity=minimal'` | 65 passed; coverage generated locally |
| `npm run build` (in `frontend`) | Passed, initial bundle 196.96 kB |
| `npm test` (in `frontend`) | 11 passed |
| `$env:OPSPULSE_BROWSER_CHANNEL = 'msedge'; npm run test:e2e` (in `frontend`) | 2 passed; captured real desktop/mobile screenshots |
| `dotnet list OpsPulse.slnx package --vulnerable --include-transitive` | No vulnerable packages reported |
| `npm audit --audit-level=moderate` (in `frontend`) | Zero vulnerabilities |
| `docker build -t opspulse-api:publication -f backend/OpsPulse.Api/Dockerfile .` | Passed; build output retained only in ignored artifacts |
| `docker run --detach --name opspulse-publication-verify -p 127.0.0.1:5082:8080 opspulse-api:publication` | Started an isolated local verification container |
| `.\scripts\smoke-test.ps1 -BaseUrl http://127.0.0.1:5082` | Readiness/services/history/incidents/OpenAPI passed |
| `docker exec opspulse-publication-verify id` | Unprivileged uid/gid 1654 (`app`) |
| `docker inspect --format '{{.Config.User}} {{json .HostConfig.PortBindings}}' opspulse-publication-verify` | `app`; published port bound only to 127.0.0.1 |
| `docker stop opspulse-publication-verify`; `docker inspect --format '{{.State.Status}}' opspulse-publication-verify` | Verification container stopped; state `exited` |

Local gateway checks used `curl.exe -k --silent --show-error --max-time 5 --output NUL --write-out '%{http_code}'` with each of these URLs:

- `https://localhost:8243/opspulse/1.0.0/api/status`: 200; its body was separately checked for the expected six-service demo shape.
- `https://localhost:8243/opspulse/1.0.0/api/services`: 200.
- `https://localhost:8243/opspulse/1.0.0/api/incidents`: 401 without credentials.

No OAuth token or backend operator key was read or used in those gateway checks. Backend create/resolve behavior passed in isolated integration tests. Successful OAuth access, gateway incident mutation, scopes and rate limiting remain unverified. No production or public application deployment is claimed.

## Git publication checks

The first push succeeded to the public repository at `https://github.com/Dimeshmethsara/opspulse-api-management`, default branch `main`. Initial commit: `e3153d7509d371d38f7880d9465574bbccfc470e`. GitHub's commit API confirmed author name **Dimesh Methsara Opanayaka** and associated GitHub account `Dimeshmethsara`. Local HEAD and the remote `refs/heads/main` matched. The anonymous repository page returned HTTP 200.

| Command | Observed result |
| --- | --- |
| `git add -- .dockerignore .env.example .gitattributes .github .gitignore OpsPulse.slnx README.md backend docs dotnet-tools.json frontend global.json incident.json scripts` | Staged 70 reviewed files, excluding runtime/private artifacts |
| `git diff --cached --check` | Passed |
| `node scripts/review-publication.mjs --staged` | All 70 staged files passed configured pattern/artifact checks |
| `git commit -m 'Build OpsPulse service monitoring and API management demo'` | Created the initial commit above |
| `gh repo create Dimeshmethsara/opspulse-api-management --public --source . --remote origin --description 'Local .NET 10 and Angular service monitoring demo with SQLite incidents, safe probes, and Choreo API Manager integration.'` | Created the intended empty public repository and origin |
| `git push --set-upstream origin main` | Created remote `main` and configured tracking; no force option used |
| `git rev-parse HEAD`; `git ls-remote --heads origin main`; `git status --short --branch` | Matching commit; clean tracked working tree after the initial push |
| `gh repo view Dimeshmethsara/opspulse-api-management --json nameWithOwner,url,isPrivate,defaultBranchRef` | Correct owner/name, public visibility and default branch `main` |
| `gh api repos/Dimeshmethsara/opspulse-api-management/commits/main --jq '{sha: .sha, github_author: .author.login, author_name: .commit.author.name, author_email: .commit.author.email, message: .commit.message}'` | Confirmed commit, requested author and GitHub account association |
| `gh api repos/Dimeshmethsara/opspulse-api-management/readme -H 'Accept: application/vnd.github.html+json'` | GitHub-rendered heading, tables, security section and screenshot reference verified |
| `gh api repos/Dimeshmethsara/opspulse-api-management/contents/docs/screenshots/dashboard-desktop.png --jq '{path: .path, size: .size, type: .type}'` | Real desktop image present, 214261 bytes |
| `gh api repos/Dimeshmethsara/opspulse-api-management/contents/docs/screenshots/dashboard-mobile.png --jq '{path: .path, size: .size, type: .type}'` | Real mobile image present, 191355 bytes |

Git's line-ending warnings during initial staging reflected the explicit `.gitattributes` policy (LF text, CRLF PowerShell); the staged whitespace check passed. No file content was replaced to suppress a test failure.

## First hosted CI failure and lockfile repair

[Initial Actions run 44532949164](https://github.com/Dimeshmethsara/opspulse-api-management/actions/runs/44532949164) ran against the initial commit. The backend build/tests and Docker image build passed. The frontend job failed at `npm ci`, reporting missing `@emnapi/core@1.11.3` and `@emnapi/runtime@1.11.3` lock entries. Its later frontend steps were skipped, not passing. Logs were inspected with `gh run view 44532949164 --repo Dimeshmethsara/opspulse-api-management --log-failed`.

Local npm metadata requests briefly encountered DNS failures and then succeeded. Regenerating with the Windows npm environment did not add the missing entries. A separate Linux container using an already available supported Node 22 image regenerated the copied manifests under ignored `artifacts/linux-lockfile-review`, without installing system-wide software or deleting source files:

```powershell
docker run --detach --name opspulse-lockfile-review `
  --mount 'type=bind,source=F:\OpsPulse\artifacts\linux-lockfile-review,target=/work' `
  -w /work node:22-alpine sh -c `
  'npm install --package-lock-only --ignore-scripts --include=optional && npm ci --dry-run --ignore-scripts'
```

The Linux command exited successfully with zero audit findings. Comparison showed exactly two added optional packages and no removed package or changed existing dependency version. The reviewed lockfile was copied back to `frontend/package-lock.json`; metadata-only peer flags were normalized by npm. No application source behavior or test expectation changed. The correction and this command record are committed normally and pushed to `main`; no history rewrite, test bypass, or force push is used. Subsequent hosted run results must be checked in Actions rather than inferred from local checks or this document.
