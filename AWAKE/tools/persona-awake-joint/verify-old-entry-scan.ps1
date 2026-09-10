param(
    [Parameter(Mandatory = $true)][string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'persona-awake-joint.ps1')

$reportPathFull = $null
$matches = [Collections.Generic.List[object]]::new()
$assertions = [Collections.Generic.List[object]]::new()
$report = [ordered]@{
    schemaVersion = 'awake.persona.old-entry-scan.v1'
    commandLine = 'pwsh -NoProfile -File "' + [IO.Path]::GetFullPath($PSCommandPath) + '" -ReportPath "' + $ReportPath + '"'
    normalizedCwd = [IO.Path]::GetFullPath((Get-Location).Path)
    status = 'error'
    exitCode = 40
    sourceRoot = $null
    sourceTreeSha256 = $null
    checkedSymbols = @()
    allowedCurrentSymbols = @('WorldbookRuntime.Knowledge')
    matches = @()
    interpretation = 'Static findings are evidence of source symbols/call sites only; this scan does not claim that runtime integration is fixed or broken by itself.'
    assertions = @()
    observedErrors = @()
}

try {
    $reportPathFull = Assert-JointOutputPath $ReportPath 'ReportPath'
    $sourceRoot = Join-Path $script:JointAwakeRoot 'src'
    if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) { throw [DirectoryNotFoundException]::new('AWAKE src directory is missing.') }
    $report.sourceRoot = [IO.Path]::GetFullPath($sourceRoot)
    $sourceSnapshot = @(Get-JointProtectedPathSnapshot @($sourceRoot))
    $report.sourceTreeSha256 = Get-JointJsonProperty $sourceSnapshot[0] 'sha256'
    $symbols = @(
        'PersonaDslGenerator.Generate',
        'BuildLegacyFallback',
        'WorldbookRuntime.Current',
        'KnowledgeRuntime.Current',
        'WorldbookService.BuildPersona',
        'SelectPersonaDefinition',
        'KnowledgeRuntime.EnsureCreated',
        'WorldbookRuntime.Knowledge'
    )
    $report.checkedSymbols = @($symbols)
    $sourceFiles = Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter '*.cs' | Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj|dist|backup[^\\/]*)[\\/]'
    } | Sort-Object FullName
    foreach ($file in $sourceFiles) {
        $relative = Get-JointRelativePath $file.FullName $sourceRoot
        $lines = Get-Content -LiteralPath $file.FullName
        for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
            $line = [string]$lines[$lineIndex]
            foreach ($symbol in $symbols) {
                if ($line.IndexOf($symbol, [StringComparison]::Ordinal) -ge 0) {
                    $matches.Add([ordered]@{ symbol = $symbol; relativePath = $relative; line = $lineIndex + 1; text = $line.Trim() })
                }
            }
        }
    }
    $uniqueMatches = @($matches.ToArray())
    $report.matches = $uniqueMatches
    Add-JointAssertion $assertions 'source_symbols_scanned' $true ('Scanned ' + $sourceFiles.Count + ' source files.')
    Add-JointAssertion $assertions 'required_symbol_set_scanned' (($symbols.Count -eq 8)) 'All required legacy/current entry markers were included.'
    $legacySymbols = @('BuildLegacyFallback','WorldbookRuntime.Current','KnowledgeRuntime.Current','KnowledgeRuntime.EnsureCreated','WorldbookService.BuildPersona')
    $legacyMatches = @($uniqueMatches | Where-Object { (Get-JointRawProperty $_ 'symbol') -in $legacySymbols })
    if ($legacyMatches.Count -gt 0) {
        $report.status = 'reject'
        $report.exitCode = 10
        $report.observedErrors = @([pscustomobject]@{ code = 'persona.old_entry_present'; detail = ($legacyMatches | ForEach-Object { (Get-JointRawProperty $_ 'symbol') + '@' + (Get-JointRawProperty $_ 'relativePath') + ':' + (Get-JointRawProperty $_ 'line') }) -join '; ' })
        Add-JointAssertion $assertions 'legacy_entry_absent' $false ('Found ' + $legacyMatches.Count + ' legacy/fallback source matches.')
    } else {
        $report.status = 'pass'
        $report.exitCode = 0
        $report.observedErrors = @()
        Add-JointAssertion $assertions 'legacy_entry_absent' $true 'No legacy/fallback source markers were found.'
    }
} catch {
    $errorRecord = Convert-JointExceptionToError $_.Exception 'verify-old-entry-scan'
    $report.status = 'error'
    $report.exitCode = 40
    $report.observedErrors = @($errorRecord)
} finally {
    $report.assertions = @($assertions)
    if ($null -ne $reportPathFull) { Write-JointReport $report $reportPathFull }
}

exit ([int]$report.exitCode)
