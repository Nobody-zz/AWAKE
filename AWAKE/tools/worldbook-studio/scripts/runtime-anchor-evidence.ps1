param(
    [string]$EvidencePath = '..\..\docs\evidence\WORLDBOOKSTUDIO-RUNTIME-ANCHOR-EVIDENCE.json'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$package = Join-Path $root 'artifacts\current-test\WorldbookStudio'
$cliDll = Join-Path $package 'cli\worldbook-studio.dll'
if (-not (Test-Path -LiteralPath $cliDll)) { throw 'missing packaged CLI; run scripts\package.ps1 first.' }
$generation = Get-Content -LiteralPath (Join-Path $root '_tmp\pravend-real-worker-evidence.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$sourceWorkspace = [string]$generation.workspace
$documentPath = [string]$generation.document_path
if (-not (Test-Path -LiteralPath $sourceWorkspace)) { throw 'acceptance workspace missing; run _tmp\real-worker-pravend.ps1 -KeepWorkspace first.' }
$schemaRoot = Join-Path $root '..\..\docs\worldbook-studio-plan'

# Clone the acceptance workspace (authoring inputs only) so every run starts from clean
# authority state and never poisons the acceptance workspace's authoring-v1 records.
$workspace = Join-Path ([IO.Path]::GetTempPath()) ('awake-runtime-anchor-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $workspace | Out-Null
Get-ChildItem -LiteralPath $sourceWorkspace -Force |
    Where-Object { $_.Name -notin @('authoring-v1', 'compiled') } |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $workspace -Recurse -Force }
$outRoot = Join-Path $workspace 'compiled\anchor-evidence'

$evidence = [ordered]@{
    schema_version = 'awake.worldbook.runtime-anchor-evidence.v1'
    source_workspace = $sourceWorkspace
    workspace = $workspace
    document_path = $documentPath
    steps = @()
    runtime_entity_refs = $null
    passed = $false
    error = $null
}
try {
    $out1 = & dotnet $cliDll 'authoring-register' '--workspace' $workspace '--schema-root' $schemaRoot '--operation' 'anchor-op-register' '--path' $documentPath 2>&1
    if ($LASTEXITCODE -ne 0) { throw ('authoring-register failed: ' + ($out1 -join ' ')) }
    $register = (($out1 | Out-String).Trim() | ConvertFrom-Json)
    $evidence.steps += [ordered]@{ id = 'register'; passed = $true; detail = [string]$register.ok }
    $documentId = [string]$register.document_revision.documentId
    $out2 = & dotnet $cliDll 'authoring-select' '--workspace' $workspace '--schema-root' $schemaRoot '--operation' 'anchor-op-select' '--document-id' $documentId 2>&1
    if ($LASTEXITCODE -ne 0) { throw ('authoring-select failed: ' + ($out2 -join ' ')) }
    $select = (($out2 | Out-String).Trim() | ConvertFrom-Json)
    $evidence.steps += [ordered]@{ id = 'select'; passed = $true; detail = $documentId }
    $selectionId = [string]$select.selection.selectionId
    if ([string]::IsNullOrWhiteSpace($selectionId)) { throw 'authoring-select returned no selectionId.' }
    $out3 = & dotnet $cliDll 'authoring-approve' '--workspace' $workspace '--schema-root' $schemaRoot '--operation' 'anchor-op-approve' '--selection' $selectionId 2>&1
    if ($LASTEXITCODE -ne 0) { throw ('authoring-approve failed: ' + ($out3 -join ' ')) }
    $approve = (($out3 | Out-String).Trim() | ConvertFrom-Json)
    $approvalId = [string]$approve.approval_proof.approvalId
    if ([string]::IsNullOrWhiteSpace($approvalId)) { throw 'authoring-approve returned no approvalId.' }
    $evidence.steps += [ordered]@{ id = 'approve'; passed = $true; detail = $approvalId }
    $out4 = & dotnet $cliDll 'authoring-proof' '--workspace' $workspace '--schema-root' $schemaRoot '--operation' 'anchor-op-proof' '--approval' $approvalId '--content-tier' 'base' 2>&1
    if ($LASTEXITCODE -ne 0) { throw ('authoring-proof failed: ' + ($out4 -join ' ')) }
    $proof = (($out4 | Out-String).Trim() | ConvertFrom-Json)
    $proofId = [string]$proof.compile_proof.compileProofId
    if ([string]::IsNullOrWhiteSpace($proofId)) { throw 'authoring-proof returned no compileProofId.' }
    $evidence.steps += [ordered]@{ id = 'compile-proof'; passed = $true; detail = $proofId }
    $out5 = & dotnet $cliDll 'compile' '--workspace' $workspace '--schema-root' $schemaRoot '--proof' $proofId '--out' $outRoot 2>&1
    if ($LASTEXITCODE -ne 0) { throw ('compile failed: ' + ($out5 -join ' ')) }
    $compile = (($out5 | Out-String).Trim() | ConvertFrom-Json)
    $evidence.steps += [ordered]@{ id = 'compile'; passed = $true; detail = [string]$compile.compiled_path }
    $evidence.output_root = [string]$compile.output_root
    $evidence.manifest_hash = [string]$compile.manifest_hash

    $compiledRoot = [IO.Path]::GetFullPath((Join-Path $workspace ([string]$compile.output_root)))
    if (-not $compiledRoot.StartsWith([IO.Path]::GetFullPath($workspace), [StringComparison]::OrdinalIgnoreCase)) { throw 'compile output_root escaped workspace.' }
    $runtimeFile = Get-ChildItem -LiteralPath $compiledRoot -Recurse -File -Filter 'runtime.json' | Select-Object -First 1
    if ($null -eq $runtimeFile) { throw ('no runtime.json in compile output: ' + $compiledRoot) }
    $runtime = Get-Content -LiteralPath $runtimeFile.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $refs = @()
    foreach ($entry in @($runtime.entries)) { $refs += @($entry.extensions.entityRefs) }
    $refs = @($refs | Where-Object { $_ } | Select-Object -Unique)
    $evidence.runtime_entity_refs = $refs
    if ($refs -notcontains 'awake:settlement:town_v3') { throw ('runtime package missing settlement anchor; got=' + ($refs -join ',')) }
    $evidence.passed = $true
}
catch { $evidence.error = $_.Exception.Message }
finally {
    $evidenceFile = [IO.Path]::GetFullPath((Join-Path $root $EvidencePath))
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $evidenceFile) | Out-Null
    ($evidence | ConvertTo-Json -Depth 12) | Set-Content -LiteralPath $evidenceFile -Encoding UTF8
    Write-Output ($evidence | ConvertTo-Json -Depth 12)
    $cloneFull = [IO.Path]::GetFullPath($workspace)
    $tempFull = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $expectedPrefix = Join-Path $tempFull 'awake-runtime-anchor-'
    if ($cloneFull.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $cloneFull)) {
        Remove-Item -LiteralPath $cloneFull -Recurse -Force -ErrorAction SilentlyContinue
    }
}
