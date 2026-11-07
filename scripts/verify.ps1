$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $root
try {
    dotnet restore .\CareerForge.slnx
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed' }
    dotnet build .\CareerForge.slnx --no-restore -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }
    dotnet test .\CareerForge.slnx --no-build
    if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed' }
    Push-Location web
    try {
        npm install --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'npm install failed' }
        npm run build
        if ($LASTEXITCODE -ne 0) { throw 'web build failed' }
    } finally { Pop-Location }
    Write-Host 'ALL GREEN' -ForegroundColor Green
} finally { Pop-Location }
