# Local operations and configuration

## Replace the demo with real probes

The following is an example **configuration shape**, not an endpoint OpsPulse contacts by default. Replace `status.your-company.example` with a public health endpoint you operate, and replace the entire `Monitoring:Services` array in server configuration rather than partially overriding demo indices with environment variables.

```json
{
  "Monitoring": {
    "WorkerEnabled": true,
    "SeedDemoData": false,
    "IntervalSeconds": 30,
    "TimeoutSeconds": 5,
    "DegradedAfterMs": 1000,
    "RetentionDays": 7,
    "AllowedHosts": ["status.your-company.example"],
    "Services": [
      {
        "Id": "customer-api",
        "Name": "Customer API",
        "Description": "Customer profile access",
        "Kind": "Http",
        "Endpoint": "https://status.your-company.example/health"
      }
    ]
  }
}
```

Restart the API after configuration changes. Invalid configuration fails startup. DNS resolution must contain only public addresses. A blocked, timed-out or failed destination records Down with no diagnostic URL or response body exposed to the public API. A real endpoint is never replaced with synthetic data when it fails.

Use a **new service ID** when changing the meaning of a monitored service. Reusing a demo service ID for a real endpoint keeps its existing synthetic history until retention removes it. To start a fresh demonstration without deleting data, point `ConnectionStrings__OpsPulse` to a new database path. The configured service list is the registry; write endpoints cannot create services or change destinations.

## Frontend API configuration

Edit `frontend/public/config.json` before serving/building:

```json
{ "apiBaseUrl": "http://localhost:5080" }
```

The same file is copied to `frontend/dist/opspulse/browser/config.json` at build time and can be changed in that output for a local runtime configuration. It must contain only a public base URL. A later gateway URL can include its context/version path. No client secret, operator key or bearer token belongs in this file. Recheck the exact CORS origin whenever the frontend origin changes.

## Readiness and service health

`GET /health` means the API can reach SQLite. It can return Healthy while a monitored service is Down. Use `/api/status` for fleet state. Probe status is independent of operator incident status, so an incident can remain open after recovery until an operator resolves it.

The worker polls immediately, then on its timer. A slow cycle can exceed the configured interval; it never overlaps itself and limits probe concurrency to four. Each outgoing request has its own timeout. The UI labels overdue checks after three intervals. A previously Healthy overdue service is shown as Degraded rather than silently healthy. The original stored status is not rewritten by a read request.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Browser cannot load status | API is running on 5080; `config.json` is correct; origin exactly matches configured CORS; browser console/network panel |
| Status shows demo data | Default `Kind: Demo`; replace configuration and disable seeding for real monitoring |
| POST/PATCH returns 503 | No server `IncidentApi__WriteKey` was supplied before startup |
| POST/PATCH returns 401 | Missing/invalid `X-OpsPulse-Key`; this key is not a gateway OAuth token |
| API fails on startup | Configuration validation message, writable database directory, migration output, .NET 10 runtime |
| Probe stays Down | Allowlisted hostname, public DNS answers, trusted TLS certificate, HTTPS port 443, 2xx health route, timeout |
| Container port already in use | Stop the directly running API or choose another host port and update frontend configuration |
| Container cannot write SQLite | Writable `/app/data` mount owned by the container's unprivileged `app` user |
| Docker build cannot reach MCR | Docker daemon/network/DNS/proxy trust; no registry credentials are required for the public .NET base images |
| History contains old synthetic data | Use a fresh service ID or a new database path; do not mix demo and production identity |
| `npm ci` reports a lock mismatch | Update dependencies intentionally with `npm install`, review the lockfile diff, then run tests |

## Backup and migrations

Stop the local API before copying the SQLite database and any associated WAL files for a consistent filesystem backup. Preserve the Docker named volume. Startup runs checked-in EF migrations for the single-instance application; do not run multiple migrating replicas against a shared file. No script deletes a database or tears down a volume.

Local generated test/build outputs are ignored. GitHub Actions has read-only repository permissions and does not push images, request external credentials or deploy an environment.
