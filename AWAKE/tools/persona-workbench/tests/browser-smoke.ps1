param(
    [string]$Root = (Split-Path -Parent $MyInvocation.MyCommand.Path),
    [switch]$KeepArtifacts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$project = Join-Path $Root "PersonaWorkbench.BrowserSmoke\PersonaWorkbench.BrowserSmoke.csproj"
if (-not (Test-Path -LiteralPath $project)) { throw "Browser smoke project is missing: $project" }
$packageRoot = Split-Path -Parent $Root

try {
    & dotnet run --project $project --no-launch-profile -- $packageRoot
    if ($LASTEXITCODE -ne 0) { throw "Browser smoke failed with exit code $LASTEXITCODE." }
}
finally {
    if (-not $KeepArtifacts) {
        Get-ChildItem -LiteralPath $packageRoot -Filter "browser-smoke-*.png" -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    }
}

Write-Output "Persona Workbench browser smoke: PASS"
