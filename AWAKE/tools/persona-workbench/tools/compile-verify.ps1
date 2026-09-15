# Compile verify: load each character card through the real compilation chain
# (PersonaDocumentCodec -> PersonaValidator -> PersonaAuthoringV2Adapter.Build)
# and report per-card errors. Exits 0 if all pass, 1 if any fail.

param(
    [string]$CharactersDir = "",
    [string]$ReportPath = ""
)

$ErrorActionPreference = "Stop"

$base = "D:\AWAKE-Dev\AWAKE"
$toolsRoot = Join-Path $base "tools\persona-workbench"
$project = Join-Path $toolsRoot "src\PersonaWorkbench.Verify\PersonaWorkbench.Verify.csproj"

if (-not $CharactersDir) { $CharactersDir = Join-Path $toolsRoot "characters" }
if (-not $ReportPath)     { $ReportPath    = Join-Path $base "docs\AUDIT-COMPILE-VERIFY-latest.json" }

if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    Write-Error "Verify project not found: $project"
    exit 2
}
if (-not (Test-Path -LiteralPath $CharactersDir -PathType Container)) {
    Write-Error "Characters directory not found: $CharactersDir"
    exit 2
}

$reportDir = Split-Path -Parent $ReportPath
if ($reportDir -and -not (Test-Path -LiteralPath $reportDir -PathType Container)) {
    New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
}

Write-Host "Running compile verify..."
Write-Host "  characters: $CharactersDir"
Write-Host "  report:     $ReportPath"
Write-Host ""

dotnet run --project $project --configuration Release -- $CharactersDir $ReportPath
$exitCode = $LASTEXITCODE

Write-Host ""
if ($exitCode -eq 0) {
    Write-Host "ALL CARDS PASS compile verify" -ForegroundColor Green
} else {
    Write-Host "COMPILE VERIFY FAILED (exit=$exitCode). See report for details." -ForegroundColor Red
}

exit $exitCode