# Local Choreo API Manager integration

The frontend defaults to direct backend access. A separately installed local Choreo API Manager / WSO2 API Manager gateway is reachable at `https://localhost:8243/opspulse/1.0.0`. During repository preparation on 2026-11-19, anonymous `GET /api/status` returned HTTP 200 with the expected six-service demo summary, `GET /api/services` returned 200, and `GET /api/incidents` returned 401. No access token was read or used for these checks.

This verifies local public routing and anonymous rejection on the incident-list resource. It does not verify a successful OAuth request, operator scopes, incident POST/PATCH through the gateway, or throttling. The gateway's private installation/configuration is not included in this repository, so its setup is documented as a manual procedure rather than reproducible provisioning. No public or production deployment is claimed.

## Prepared contract

With the local API running:

```powershell
.\scripts\export-openapi.ps1
```

`docs/openapi.json` uses OpenAPI 3.0, includes public schemas and validation constraints, and declares the `IncidentKey` API-key security scheme only for incident mutation operations. It contains no monitored service probe destinations, connection strings or key values. The generated API document contains the API's HTTP routes; it does not declare that the entire API is publicly writable.

The [official API Manager OpenAPI import guide](https://apim.docs.wso2.com/en/latest/api-design-manage/design/create-api/create-rest-api/create-a-rest-api-from-an-openapi-definition/) describes importing a definition file or URL into the Publisher Portal. Use the guide corresponding to your installed manager version; UI labels and gateway types can differ. A definition file can be reviewed locally without granting OpsPulse credentials.

## Manual setup and verification

1. Review the exported definition and use context `/opspulse` with version `1.0.0` for the example gateway URL.
2. Import the definition and set a backend URL reachable **from the gateway**, such as `http://localhost:5080` when both run directly on the same Windows host. Gateway `localhost` refers to the gateway host/container, not another computer or container. Configure ASP.NET's root `AllowedHosts` for the backend request hostname; the default accepts local development hosts only. This host filter is separate from `Monitoring:AllowedHosts`, which governs outbound probes.
3. Map `/opspulse/1.0.0/api/status` to the backend `/api/status`, with the equivalent mapping for other routes. Decide whether readiness/OpenAPI should be exposed through the gateway; remove those resources from the imported gateway definition if they are infrastructure-only.
4. Keep public status GETs anonymous or apply the organization's chosen read policy. Protect POST/PATCH with scoped operator identity and rate limits. Keep the backend write key in gateway/server secret storage and inject it only after gateway operator authorization; do not distribute it to dashboard users. Restrict direct backend network access in that deployment.
5. Decide OAuth/BFF behavior separately. Never embed a confidential OAuth client secret or a long-lived token in Angular. A public SPA requiring authenticated reads needs an approved public-client flow; this release has anonymous public reads and no token flow.
6. To use the gateway for dashboard reads, change the dashboard's public `config.json` base URL to `https://localhost:8243/opspulse/1.0.0`. Configure gateway CORS for the exact frontend origin, and review backend CORS only if direct browser access remains necessary. The current Angular client sends no bearer token. It renders incidents from the public status summary, so that summary's incident text must remain suitable for public disclosure even if the separate incident-list resource is protected.
7. Re-run service/incident/error/CORS checks through the gateway and verify that public payloads contain no probe destinations or private configuration. Gateway error responses should preserve a useful correlation ID without exposing upstream secrets.

Use the PowerShell curl examples in [README.md](../README.md) to test public and protected resources. Obtain any token through the installation's approved browser/Developer Portal flow and keep it out of files, logs, screenshots and source control. OAuth credentials, certificates, API Manager binaries, local user stores and private gateway configuration must not be committed.

Remaining verification: successful authorized incident reads, approved operator scope enforcement, backend-key injection, create/resolve through the gateway, revoked/expired-token behavior, CORS from the browser, and rate-limit policy tests. The existing xUnit tests cover backend incident creation/resolution independently of the gateway.
