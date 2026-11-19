param(
    [string]$BaseUrl = 'http://localhost:5080',
    [string]$OutputPath = (Join-Path $PSScriptRoot '../docs/openapi.json')
)
$ErrorActionPreference = 'Stop'
$document = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/openapi/v1.json')
if ($document.openapi -notlike '3.0.*') { throw 'Expected OpenAPI 3.0.' }
$json = $document | ConvertTo-Json -Depth 100
[System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($OutputPath), $json, [System.Text.UTF8Encoding]::new($false))
Write-Output ('OpenAPI exported to ' + [System.IO.Path]::GetFullPath($OutputPath))
