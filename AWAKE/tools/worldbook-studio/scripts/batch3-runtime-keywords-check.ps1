param(
    [string]$EvidencePath = '..\..\docs\evidence\WORLDBOOKSTUDIO-BATCH3-RUNTIME-KEYWORDS.json',
    [string]$PackageDir = 'artifacts\current-test\WorldbookStudio',
    [switch]$KeepWorkspace
)
# 批 3 验收（K1 + D1-3）：用打包版 CLI 编译 pravend-cluster 夹具，
# 断言运行包 entry 的 keywords 包含文档别名（如"巴拉维诺斯"）与可读地点名称，
# 且不再把内部文档 id 当作唯一关键词。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$package = [IO.Path]::GetFullPath((Join-Path $root $PackageDir))
$cliExe = @((Join-Path $package 'cli\worldbook-studio.exe'), (Join-Path $package 'worldbook-studio.exe')) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
$cliDll = @((Join-Path $package 'cli\worldbook-studio.dll'), (Join-Path $package 'worldbook-studio.dll')) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
$fixture = Join-Path $root 'tests\fixtures\official-reference\pravend-cluster'
$schemaRoot = Join-Path $root '..\..\docs\worldbook-studio-plan'

if (Test-Path -LiteralPath $cliExe) { $cli = @($cliExe) }
elseif (Test-Path -LiteralPath $cliDll) { $cli = @('dotnet', $cliDll) }
else { throw 'missing packaged CLI; run scripts\package.ps1 first.' }
$cliHost = $cli[0]
$cliPrefix = @()
if ($cli.Count -gt 1) { $cliPrefix = @($cli[1..($cli.Count - 1)]) }
if (-not (Test-Path -LiteralPath $fixture -PathType Container)) { throw "missing fixture: $fixture" }

$workspace = Join-Path ([IO.Path]::GetTempPath()) ('awake-batch3-keywords-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $workspace | Out-Null
Copy-Item -LiteralPath (Join-Path $fixture 'authoring') -Destination $workspace -Recurse -Force
$sourceDir = Join-Path $workspace 'authoring\sources'
New-Item -ItemType Directory -Force -Path $sourceDir | Out-Null
Get-ChildItem -LiteralPath (Join-Path $fixture 'sources') -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $sourceDir -Force }
$outRoot = Join-Path $workspace 'compiled\batch3-keywords'

$evidence = [ordered]@{
    schema_version = 'awake.worldbook.batch3-runtime-keywords.v1'
    cli = ($cli -join ' ')
    workspace = $workspace
    steps = @()
    entry_keyword_report = $null
    assertions = [ordered]@{}
    passed = $false
    error = $null
}

function Invoke-Cli([string]$command, [string[]]$arguments) {
    $all = $cliPrefix + @($command) + $arguments + @('--workspace', $workspace, '--schema-root', $schemaRoot)
    $output = & $cliHost $all 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($command + ' ' + ($arguments -join ' ') + ' failed: ' + (($output | Out-String).Trim())) }
    return (($output | Out-String).Trim() | ConvertFrom-Json)
}

try {
    $documents = @(Get-ChildItem -LiteralPath (Join-Path $workspace 'authoring') -Recurse -File -Filter '*.yaml' |
        Where-Object { $_.FullName -notmatch '\\sources\\' } |
        ForEach-Object { $_.FullName.Substring((Join-Path $workspace 'authoring').Length + 1) -replace '\\', '/' })
    if ($documents.Count -eq 0) { throw 'no authoring documents in fixture cluster.' }
    $documentIds = @()
    foreach ($document in $documents) {
        $register = Invoke-Cli 'authoring-register' @('--operation', "batch3-register-$([Guid]::NewGuid().ToString('N'))", '--path', "authoring/$document")
        $documentIds += [string]$register.document_revision.documentId
    }
    $evidence.steps += [ordered]@{ id = 'register'; passed = $true; detail = ($documentIds -join ',') }

    $select = Invoke-Cli 'authoring-select' @('--operation', "batch3-select-$([Guid]::NewGuid().ToString('N'))", '--document-id', ($documentIds -join ','))
    $selectionId = [string]$select.selection.selectionId
    if ([string]::IsNullOrWhiteSpace($selectionId)) { throw 'authoring-select returned no selectionId.' }
    $evidence.steps += [ordered]@{ id = 'select'; passed = $true; detail = $selectionId }

    $approve = Invoke-Cli 'authoring-approve' @('--operation', "batch3-approve-$([Guid]::NewGuid().ToString('N'))", '--selection', $selectionId)
    $approvalId = [string]$approve.approval_proof.approvalId
    if ([string]::IsNullOrWhiteSpace($approvalId)) { throw 'authoring-approve returned no approvalId.' }
    $evidence.steps += [ordered]@{ id = 'approve'; passed = $true; detail = $approvalId }

    $proof = Invoke-Cli 'authoring-proof' @('--operation', "batch3-proof-$([Guid]::NewGuid().ToString('N'))", '--approval', $approvalId, '--content-tier', 'base')
    $proofId = [string]$proof.compile_proof.compileProofId
    if ([string]::IsNullOrWhiteSpace($proofId)) { throw 'authoring-proof returned no compileProofId.' }
    $evidence.steps += [ordered]@{ id = 'compile-proof'; passed = $true; detail = $proofId }

    $compile = Invoke-Cli 'compile' @('--proof', $proofId, '--out', $outRoot)
    $evidence.steps += [ordered]@{ id = 'compile'; passed = $true; detail = [string]$compile.compiled_path }
    # D1-2：三个词条共用实体锚点名称（帕拉汶德 / Pravend / 巴拉维诺斯 / Paravenos），
    # 编译期必须逐条产出「一个关键词命中多条词条」的诊断，并列出命中条目。
    $validationJson = if ($null -eq $compile.validation) { '' } else { ($compile.validation | ConvertTo-Json -Depth 12 -Compress) }
    $evidence.ambiguity_diagnostic_count = ([regex]::Matches($validationJson, 'WB-INDEX-AMBIGUOUS')).Count

    $compiledRoot = [IO.Path]::GetFullPath((Join-Path $workspace ([string]$compile.output_root)))
    if (-not $compiledRoot.StartsWith([IO.Path]::GetFullPath($workspace), [StringComparison]::OrdinalIgnoreCase)) { throw 'compile output_root escaped workspace.' }
    $runtimeFile = Get-ChildItem -LiteralPath $compiledRoot -Recurse -File -Filter 'runtime.json' | Select-Object -First 1
    if ($null -eq $runtimeFile) { throw ('no runtime.json in compile output: ' + $compiledRoot) }
    $runtime = Get-Content -LiteralPath $runtimeFile.FullName -Raw -Encoding UTF8 | ConvertFrom-Json

    $report = @()
    $allKeywords = @()
    $aliasOnlyInternalDocIdEntries = 0
    foreach ($entry in @($runtime.entries)) {
        $keywords = @($entry.keywords | Where-Object { $_ } | ForEach-Object { [string]$_ })
        $titles = @()
        if ($entry.title) { $titles = @($entry.title.PSObject.Properties | ForEach-Object { [string]$_.Value } | Where-Object { $_ }) }
        $readable = @($keywords | Where-Object { $_ -notlike 'doc.*' -and $_ -notlike 'awake:*' })
        if ($readable.Count -eq 0) { $aliasOnlyInternalDocIdEntries++ }
        $allKeywords += $keywords
        $report += [ordered]@{
            entry_id = [string]$entry.id
            domain = [string]$entry.domain
            source_document_id = [string]$entry.extensions.sourceDocumentId
            title = ($titles -join ' / ')
            keywords = $keywords
            readable_keyword_count = $readable.Count
        }
    }
    $evidence.entry_keyword_report = $report

    # 断言用「精确关键词匹配」：标题里的子串（如"帕拉汶德历史沿革与旧称巴拉维诺斯"）不算命中。
    $primary = $report | Where-Object { $_.source_document_id -eq 'doc.geography.pravend' } | Select-Object -First 1
    if ($null -eq $primary) { throw 'compiled runtime package is missing the geography/pravend entry.' }
    $primaryKeywords = @($primary.keywords)
    $evidence.primary_entry_id = [string]$primary.entry_id
    $evidence.assertions['aliases_present'] = ($primaryKeywords -contains '巴拉维诺斯')
    $evidence.assertions['readable_title_present'] = ($primaryKeywords -contains '帕拉汶德')
    # Pravend / Paravenos 只存在于 docs\mappings\persona-entity 的实体登记表里，
    # 运行包关键词里出现它们，才证明「地点锚点的可读名称」确实被解析并写入。
    $evidence.assertions['entity_anchor_english_name_present'] = ($primaryKeywords -contains 'Pravend')
    $evidence.assertions['entity_anchor_alias_present'] = ($primaryKeywords -contains 'Paravenos')
    $evidence.assertions['internal_doc_id_not_sole_keyword'] = ($aliasOnlyInternalDocIdEntries -eq 0)
    $evidence.assertions['internal_doc_id_kept_as_fallback'] = ($primaryKeywords -contains 'doc.geography.pravend')
    $evidence.assertions['ambiguity_diagnostic_present'] = ($evidence.ambiguity_diagnostic_count -ge 1)
    $evidence.assertions['ambiguity_diagnostic_lists_entries'] = ($validationJson -match 'awake:entry:geography\.pravend' -and $validationJson -match 'awake:entry:politics\.pravend-succession')
    $evidence.passed = ($evidence.assertions.Values -notcontains $false)
}
catch { $evidence.error = $_.Exception.Message }
finally {
    $evidenceFile = [IO.Path]::GetFullPath((Join-Path $root $EvidencePath))
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $evidenceFile) | Out-Null
    ($evidence | ConvertTo-Json -Depth 12) | Set-Content -LiteralPath $evidenceFile -Encoding UTF8
    Write-Output ($evidence | ConvertTo-Json -Depth 12)
    if (-not $KeepWorkspace) {
        $cloneFull = [IO.Path]::GetFullPath($workspace)
        $expectedPrefix = Join-Path ([IO.Path]::GetFullPath([IO.Path]::GetTempPath())) 'awake-batch3-keywords-'
        if ($cloneFull.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $cloneFull)) {
            Remove-Item -LiteralPath $cloneFull -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
