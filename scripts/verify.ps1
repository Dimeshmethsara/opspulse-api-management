param([switch]$SkipFrontend)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    dotnet restore OpsPulse.slnx
    if ($LASTEXITCODE -ne 0) { throw 'Backend restore failed.' }
    dotnet build OpsPulse.slnx --no-restore -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }
    dotnet test backend/OpsPulse.Tests --no-build -c Release --collect:'XPlat Code Coverage' --results-directory artifacts/test-results
    if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed.' }
    if (-not $SkipFrontend) {
        Push-Location frontend
        try {
            npm ci
            if ($LASTEXITCODE -ne 0) { throw 'Frontend dependency install failed.' }
            npm run build
            if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
            npm test
            if ($LASTEXITCODE -ne 0) { throw 'Frontend tests failed.' }
        }
        finally { Pop-Location }
    }
}
finally { Pop-Location }
