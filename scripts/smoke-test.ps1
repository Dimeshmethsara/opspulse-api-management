param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
$base = $BaseUrl.TrimEnd('/')
$health = Invoke-RestMethod "$base/health"
if ($health.status -ne 'Healthy') { throw 'API is not ready.' }
$summary = Invoke-RestMethod "$base/api/status"
if ($summary.services.Count -lt 1) { throw 'No services registered.' }
foreach ($service in $summary.services) {
    if ($service.history.Count -ne 24) { throw ('Expected 24 history buckets for ' + $service.id) }
}
$incidents = Invoke-RestMethod "$base/api/incidents"
$document = Invoke-RestMethod "$base/openapi/v1.json"
if ($document.info.title -ne 'OpsPulse API') { throw 'OpenAPI document is invalid.' }
Write-Output ('PASS: readiness, {0} services, hourly history, {1} incidents and OpenAPI.' -f $summary.services.Count, @($incidents).Count)
