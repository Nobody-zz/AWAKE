param([string]$Package = "artifacts\current-test\WorldbookStudio")

$ErrorActionPreference = 'Stop'

function Get-FileSha256([string]$path) {
    $fullPath = [System.IO.Path]::GetFullPath($path)
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    $stream = $null
    try {
        $stream = [System.IO.File]::OpenRead("\\?\$fullPath")
        return [System.BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        if ($null -ne $stream) { $stream.Dispose() }
        $algorithm.Dispose()
    }
}

function Get-FileLength([string]$path) {
    $fullPath = [System.IO.Path]::GetFullPath($path)
    $stream = [System.IO.File]::OpenRead("\\?\$fullPath")
    try { return $stream.Length }
    finally { $stream.Dispose() }
}
$root = Split-Path -Parent $PSScriptRoot
$path = if ([System.IO.Path]::IsPathRooted($Package)) { [System.IO.Path]::GetFullPath($Package) } else { [System.IO.Path]::GetFullPath((Join-Path $root $Package)) }
if (-not (Test-Path -LiteralPath $path -PathType Container)) { throw 'WB-RELEASE-000: 发布目录不存在。' }
$batchContractRelative = 'schemas/batch/awake.worldbook.batch-authoring-contracts-r14.json'
$approvedBatchContractSha256 = 'df19cc670db082bed8702c2cd95c60c22c91b5340a27d841d915514810bd8ffb'
$batchContractPath = Join-Path $path ($batchContractRelative.Replace('/', '\'))
$testScript = Join-Path $root 'scripts\test.ps1'
$authorityEvidencePath = Join-Path $root 'artifacts\current-test\evidence\authority-gate.test.json'
$authorityContracts = @(
    'authoring-wire-contract.v1.json',
    'authoring-action-route-registry.v1.json',
    'authoring-action-contract-catalog.v1.json',
    'authoring-v1-legacy-route-matrix.json'
)
$sourceContractsRoot = Join-Path $root 'contracts'
$packageContractsRoot = Join-Path $path 'contracts'
$contractCheckScript = Join-Path $root 'scripts\batch-contract-check.ps1'
$sourceContractPath = Join-Path $root '..\..\docs\superpowers\specs\2026-08-25-worldbook-studio-batch-authoring-contracts-r14.json'
$sourceAuthoringSchemaPath = Join-Path $root '..\..\docs\worldbook-studio-plan\awake.worldbook.authoring.v1.schema.json'
$sourceWebRoot = Join-Path $root 'src\Awake.WorldbookStudio.Web\wwwroot'
$packageWebRoot = Join-Path $path 'web\wwwroot'
$sourceDraftScriptPath = Join-Path $root 'src\Awake.WorldbookStudio.Web\wwwroot\studio-draft.js'
$packageAuthoringSchemaPath = Join-Path $path 'schemas\awake.worldbook.authoring.v1.schema.json'
$packageDraftScriptPath = Join-Path $path 'web\wwwroot\studio-draft.js'
$testEvidencePath = Join-Path ([System.IO.Path]::GetTempPath()) ('worldbook-studio-release-check-test-' + [Guid]::NewGuid().ToString('N') + '.txt')
$contractEvidencePath = Join-Path ([System.IO.Path]::GetTempPath()) ('worldbook-studio-release-check-contract-' + [Guid]::NewGuid().ToString('N') + '.txt')
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Read-CompressedBytes([string]$FilePath) {
    $raw = [System.IO.File]::ReadAllBytes($FilePath)
    $input = [System.IO.MemoryStream]::new($raw, $false)
    $stream = $null
    $output = [System.IO.MemoryStream]::new()
    try {
        switch ([System.IO.Path]::GetExtension($FilePath).ToLowerInvariant()) {
            '.gz' { $stream = [System.IO.Compression.GZipStream]::new($input, [System.IO.Compression.CompressionMode]::Decompress) }
            '.br' { $stream = [System.IO.Compression.BrotliStream]::new($input, [System.IO.Compression.CompressionMode]::Decompress) }
            default { throw "不支持的压缩扩展名 $FilePath" }
        }
        $stream.CopyTo($output)
        return ,$output.ToArray()
    }
    finally {
        if ($null -ne $stream) { $stream.Dispose() }
        $output.Dispose()
        $input.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $testScript -PathType Leaf)) { throw 'WB-RELEASE-019: 当前源码 test.ps1 缺失。' }
if (-not (Test-Path -LiteralPath $contractCheckScript -PathType Leaf)) { throw 'WB-RELEASE-026: 当前源码 batch-contract-check.ps1 缺失。' }
if (-not (Test-Path -LiteralPath $sourceContractPath -PathType Leaf)) { throw 'WB-RELEASE-027: 当前源码 Revision 14 批量合同缺失。' }
if (-not (Test-Path -LiteralPath $sourceAuthoringSchemaPath -PathType Leaf)) { throw 'WB-RELEASE-028: 当前源码 authoring schema 缺失。' }
if (-not (Test-Path -LiteralPath $sourceWebRoot -PathType Container)) { throw 'WB-RELEASE-036: 当前源码 wwwroot 缺失。' }
if (-not (Test-Path -LiteralPath $packageWebRoot -PathType Container)) { throw 'WB-RELEASE-037: 发布包 web/wwwroot 缺失。' }
if (-not (Test-Path -LiteralPath $sourceDraftScriptPath -PathType Leaf)) { throw 'WB-RELEASE-034: 当前源码 studio-draft.js 缺失。' }
if (-not (Test-Path -LiteralPath $sourceContractsRoot -PathType Container)) { throw 'WB-RELEASE-039: 当前源码 authority contracts 缺失。' }
if (-not (Test-Path -LiteralPath $packageContractsRoot -PathType Container)) { throw 'WB-RELEASE-040: 发布包 authority contracts 缺失。' }

$authorityCommand = "& `"$testScript`" -Suite AuthorityGate"
$authorityOutput = @()
$authorityExitCode = -1
try {
    $authorityOutput = @(& $testScript -Suite AuthorityGate 2>&1)
    $authorityExitCode = $LASTEXITCODE
}
catch {
    $authorityOutput += $_
    $authorityExitCode = if ($null -ne $LASTEXITCODE) { $LASTEXITCODE } else { -1 }
}
if ($authorityExitCode -ne 0) { throw "WB-RELEASE-041: AuthorityGate suite failed (exit=$authorityExitCode). Output: $($authorityOutput -join [Environment]::NewLine)" }
if (-not (Test-Path -LiteralPath $authorityEvidencePath -PathType Leaf)) { throw 'WB-RELEASE-042: AuthorityGate evidence missing after explicit suite run.' }
$authorityEvidence = Get-Content -LiteralPath $authorityEvidencePath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($authorityEvidence.authority_gate_passed -ne $true) { throw 'WB-RELEASE-043: AuthorityGate evidence did not pass.' }
$authorityCases = @($authorityEvidence.cases)
if ($authorityCases.Count -lt 3 -or @($authorityCases | Where-Object { $_.passed -ne $true }).Count -ne 0) { throw 'WB-RELEASE-044: AuthorityGate evidence does not contain three passing fixture groups.' }
Write-Output 'authority_gate_passed=true'
Write-Output "authority_gate_command=$authorityCommand"
Write-Output "authority_gate_evidence=$authorityEvidencePath"

$testCommand = "& `"$testScript`""
$testOutput = @()
$testExitCode = -1
try {
    $testOutput = @(& $testScript 2>&1)
    $testExitCode = $LASTEXITCODE
}
catch {
    $testOutput += $_
    $testExitCode = if ($null -ne $LASTEXITCODE) { $LASTEXITCODE } else { -1 }
}
[System.IO.File]::WriteAllLines($testEvidencePath, @(
    "command=$testCommand",
    "script=$testScript",
    "exit_code=$testExitCode",
    'output:'
) + @($testOutput | ForEach-Object { [string]$_ }), $utf8NoBom)
if ($testExitCode -ne 0) { throw "WB-RELEASE-020: 当前源码 test.ps1 失败（exit=$testExitCode）。Evidence: $testEvidencePath" }
$batchSmokeEvidencePath = Join-Path $root 'artifacts\current-test\evidence\batch-workflow-smoke.test.json'
if (-not (Test-Path -LiteralPath $batchSmokeEvidencePath -PathType Leaf)) { throw 'WB-RELEASE-029: test.ps1 未生成批量 HTTP Smoke 证据。' }
$batchSmokeEvidence = Get-Content -LiteralPath $batchSmokeEvidencePath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($batchSmokeEvidence.passed -ne $true) { throw 'WB-RELEASE-030: 批量 HTTP Smoke 证据未通过。' }
Write-Output "TEST: PASS evidence=$testEvidencePath"

$contractOutput = @()
$contractExitCode = -1
try {
    $contractOutput = @(& $contractCheckScript -ContractPath $sourceContractPath 2>&1)
    $contractExitCode = $LASTEXITCODE
}
catch {
    $contractOutput += $_
    $contractExitCode = if ($null -ne $LASTEXITCODE) { $LASTEXITCODE } else { -1 }
}
[System.IO.File]::WriteAllLines($contractEvidencePath, @(
    "command=& `"$contractCheckScript`" -ContractPath `"$sourceContractPath`"",
    "script=$contractCheckScript",
    "exit_code=$contractExitCode",
    'output:'
) + @($contractOutput | ForEach-Object { [string]$_ }), $utf8NoBom)
if ($contractExitCode -ne 0) { throw "WB-RELEASE-031: Revision 14 机械契约检查失败（exit=$contractExitCode）。Evidence: $contractEvidencePath" }
Write-Output "CONTRACT: PASS evidence=$contractEvidencePath"
$sourceContract = Get-Content -LiteralPath $sourceContractPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($sourceContract.test_entrypoints.wired_now -ne $true) { throw 'WB-RELEASE-033: Revision 14 批量测试入口未完成发布接线（wired_now=false）。' }

foreach ($contractName in $authorityContracts) {
    $sourcePath = Join-Path $sourceContractsRoot $contractName
    $packagePath = Join-Path $packageContractsRoot $contractName
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "WB-RELEASE-045: 当前源码缺少 authority contract $contractName。" }
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) { throw "WB-RELEASE-046: 发布包缺少 authority contract $contractName。" }
    $contract = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $status = if ($null -ne $contract.status) { [string]$contract.status } else { [string]$contract.registry_status }
    if ($status -eq 'draft' -or [string]::IsNullOrWhiteSpace($status)) { throw "WB-RELEASE-047: authority contract $contractName 未绑定（status=$status）。" }
    $sourceHash = Get-FileSha256 $sourcePath
    $packageHash = Get-FileSha256 $packagePath
    if ($sourceHash -ne $packageHash) { throw "WB-RELEASE-048: authority contract $contractName 哈希不一致（source=$sourceHash package=$packageHash）。" }
    Write-Output "authority_contract=$contractName status=$status sha256=$sourceHash"
}

$required = @(
    'Awake.WorldbookStudio.Launcher.exe',
    'web\Awake.WorldbookStudio.Web.exe',
    'cli\worldbook-studio.exe',
    'web\wwwroot\index.html',
    'web\wwwroot\studio-draft.js',
    'schemas\awake.worldbook.authoring.v1.schema.json',
    'schemas\batch\awake.worldbook.batch-authoring-contracts-r14.json',
    'manifest.json',
    'SHA256SUMS.txt'
)
foreach ($relative in $required) { if (-not (Test-Path -LiteralPath (Join-Path $path $relative) -PathType Leaf)) { throw "WB-RELEASE-001: 缺少 $relative。" } }
try { $batchContract = Get-Content -LiteralPath $batchContractPath -Raw -Encoding UTF8 | ConvertFrom-Json } catch { throw "WB-RELEASE-019: Revision 14 批量合同无法解析。$batchContractRelative" }
$batchContractHash = Get-FileSha256 $batchContractPath
if ($batchContract.revision -ne 14 -or $batchContract.schema_version -ne 'awake.worldbook.batch-authoring.contracts.v2') { throw 'WB-RELEASE-021: 批量合同不是 Revision 14。' }
if ($batchContractHash -ne $approvedBatchContractSha256) { throw "WB-RELEASE-022: Revision 14 批量合同 SHA-256 不匹配（actual=$batchContractHash）。" }
$sourceAuthoringSchemaHash = Get-FileSha256 $sourceAuthoringSchemaPath
$packageAuthoringSchemaHash = Get-FileSha256 $packageAuthoringSchemaPath
if ($sourceAuthoringSchemaHash -ne $packageAuthoringSchemaHash) { throw "WB-RELEASE-032: 发布包 authoring schema 与当前源码不一致（source=$sourceAuthoringSchemaHash package=$packageAuthoringSchemaHash）。" }
$sourceDraftScriptHash = Get-FileSha256 $sourceDraftScriptPath
$packageDraftScriptHash = Get-FileSha256 $packageDraftScriptPath
if ($sourceDraftScriptHash -ne $packageDraftScriptHash) { throw "WB-RELEASE-035: 发布包 studio-draft.js 与当前源码不一致（source=$sourceDraftScriptHash package=$packageDraftScriptHash）。" }
$sourceWebFiles = @(Get-ChildItem -LiteralPath $sourceWebRoot -Recurse -File)
$sourceWebPaths = @{}
foreach ($sourceFile in $sourceWebFiles) {
    $relative = $sourceFile.FullName.Substring($sourceWebRoot.Length + 1).Replace('\', '/')
    $key = $relative.ToLowerInvariant()
    $sourceWebPaths[$key] = $true
    $packageFile = Join-Path $packageWebRoot ($relative.Replace('/', '\'))
    if (-not (Test-Path -LiteralPath $packageFile -PathType Leaf)) { throw "WB-RELEASE-038: 发布包缺少 wwwroot 文件 $relative。" }
    $sourceHash = Get-FileSha256 $sourceFile.FullName
    $packageHash = Get-FileSha256 $packageFile
    if ($sourceHash -ne $packageHash) { throw "WB-RELEASE-039: 发布包 wwwroot 文件与当前源码不一致 $relative（source=$sourceHash package=$packageHash）。" }
}
$packageWebFiles = @(Get-ChildItem -LiteralPath $packageWebRoot -Recurse -File | Where-Object { $_.Extension -notin @('.br', '.gz') })
foreach ($packageFile in $packageWebFiles) {
    $relative = $packageFile.FullName.Substring($packageWebRoot.Length + 1).Replace('\', '/')
    if (-not $sourceWebPaths.ContainsKey($relative.ToLowerInvariant())) { throw "WB-RELEASE-040: 发布包存在源码未登记的 wwwroot 文件 $relative。" }
}
if ($packageWebFiles.Count -ne $sourceWebFiles.Count) { throw 'WB-RELEASE-041: 发布包与源码 wwwroot 文件数量不一致。' }
$compressedWebFiles = @(Get-ChildItem -LiteralPath $packageWebRoot -Recurse -File | Where-Object { $_.Extension -in @('.br', '.gz') })
foreach ($compressedFile in $compressedWebFiles) {
    $relative = $compressedFile.FullName.Substring($packageWebRoot.Length + 1).Replace('\', '/')
    $baseRelative = $relative.Substring(0, $relative.Length - $compressedFile.Extension.Length)
    $basePath = Join-Path $packageWebRoot ($baseRelative.Replace('/', '\'))
    if (-not (Test-Path -LiteralPath $basePath -PathType Leaf)) { throw "WB-RELEASE-042: 压缩 wwwroot 文件没有对应原始文件 $relative。" }
    try { $decoded = Read-CompressedBytes $compressedFile.FullName }
    catch { throw "WB-RELEASE-043: 压缩 wwwroot 文件无法解压 $relative。$($_.Exception.Message)" }
    $original = [System.IO.File]::ReadAllBytes($basePath)
    if (-not [System.Linq.Enumerable]::SequenceEqual($decoded, $original)) { throw "WB-RELEASE-044: 压缩 wwwroot 文件与原始文件内容不一致 $relative。" }
}
if (Test-Path -LiteralPath (Join-Path $path 'launcher')) { throw 'WB-RELEASE-002: 发布包包含旧 launcher 子目录。' }
if (-not (Get-ChildItem -LiteralPath (Join-Path $path 'web') -Recurse -File | Where-Object { $_.Name -in @('hostfxr.dll', 'System.Private.CoreLib.dll') })) { throw 'WB-RELEASE-003: Web self-contained runtime 缺失。' }
if (-not (Get-ChildItem -LiteralPath (Join-Path $path 'cli') -Recurse -File | Where-Object { $_.Name -in @('hostfxr.dll', 'System.Private.CoreLib.dll') })) { throw 'WB-RELEASE-004: CLI self-contained runtime 缺失。' }

$manifest = Get-Content -LiteralPath (Join-Path $path 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or $manifest.product -ne 'AWAKE.WorldbookStudio' -or $manifest.rid -ne 'win-x64' -or $manifest.selfContained -ne $true) { throw 'WB-RELEASE-005: manifest 元数据不符合契约。' }
$batchManifestEntries = @($manifest.files | Where-Object { [string]$_.path -eq $batchContractRelative })
if ($batchManifestEntries.Count -ne 1) { throw "WB-RELEASE-023: manifest 未精确登记 $batchContractRelative。" }
$batchManifestEntry = $batchManifestEntries[0]
if ([string]$batchManifestEntry.sha256 -ine $approvedBatchContractSha256 -or [int64]$batchManifestEntry.length -ne (Get-Item -LiteralPath $batchContractPath).Length) { throw "WB-RELEASE-024: manifest 中的 Revision 14 批量合同哈希或长度不匹配。" }
$manifestPaths = @{}
foreach ($entry in @($manifest.files)) {
    $relative = [string]$entry.path
    $segments = $relative.Split('/', [System.StringSplitOptions]::None)
    $hasInvalidSegment = @($segments | Where-Object { [string]::IsNullOrEmpty($_) -or $_ -eq '.' -or $_ -eq '..' }).Count -gt 0
    if ([System.IO.Path]::IsPathRooted($relative) -or $relative.Contains('\') -or $segments.Count -eq 0 -or $hasInvalidSegment -or $segments[0] -match '^[A-Za-z]:$') { throw 'WB-RELEASE-006: manifest 路径格式无效。' }
    if ($manifestPaths.ContainsKey($relative.ToLowerInvariant())) { throw 'WB-RELEASE-007: manifest 存在重复路径。' }
    $manifestPaths[$relative.ToLowerInvariant()] = $true
    $file = Join-Path $path ($relative.Replace('/', '\'))
    if (-not [System.IO.File]::Exists("\\?\$([System.IO.Path]::GetFullPath($file))")) { throw "WB-RELEASE-008: manifest 文件缺失 $relative。" }
    $hash = Get-FileSha256 $file
    if ($hash -ne ([string]$entry.sha256).ToLowerInvariant() -or [int64]$entry.length -ne (Get-FileLength $file)) { throw "WB-RELEASE-009: manifest 校验失败 $relative。" }
}
$actualPaths = Get-ChildItem -LiteralPath $path -Recurse -File | Where-Object { $_.Name -notin @('manifest.json', 'SHA256SUMS.txt') } | ForEach-Object { $_.FullName.Substring($path.Length + 1).Replace('\', '/') }
foreach ($relative in $actualPaths) { if (-not $manifestPaths.ContainsKey($relative.ToLowerInvariant())) { throw "WB-RELEASE-010: 存在未登记文件 $relative。" } }
if ($manifestPaths.Count -ne @($actualPaths).Count) { throw 'WB-RELEASE-011: manifest 文件数量不一致。' }

$sumPath = Join-Path $path 'SHA256SUMS.txt'
$sumLines = [System.IO.File]::ReadAllLines($sumPath, [System.Text.UTF8Encoding]::new($false)) | Where-Object { $_.Length -gt 0 }
if ($sumLines.Count -ne $manifestPaths.Count) { throw 'WB-RELEASE-013: SHA256SUMS 文件数量与 manifest 不一致。' }
$sumPaths = @{}
foreach ($line in $sumLines) {
    if ($line -notmatch '^(?<hash>[A-Fa-f0-9]{64})  (?<relative>.+)$') { throw 'WB-RELEASE-014: SHA256SUMS 存在无效行或编码损坏。' }
    $relative = $Matches.relative
    $hash = $Matches.hash.ToLowerInvariant()
    $key = $relative.ToLowerInvariant()
    if ($sumPaths.ContainsKey($key)) { throw "WB-RELEASE-015: SHA256SUMS 存在重复路径 $relative。" }
    if (-not $manifestPaths.ContainsKey($key)) { throw "WB-RELEASE-016: SHA256SUMS 存在未登记路径 $relative。" }
    $sumPaths[$key] = $true
    $file = Join-Path $path ($relative.Replace('/', '\'))
    $actualHash = Get-FileSha256 $file
    if ($actualHash -ne $hash) { throw "WB-RELEASE-017: SHA256SUMS 校验失败 $relative。" }
}
if ($sumPaths.Count -ne $manifestPaths.Count) { throw 'WB-RELEASE-018: SHA256SUMS 路径集合与 manifest 不一致。' }
if (-not $sumPaths.ContainsKey($batchContractRelative.ToLowerInvariant())) { throw "WB-RELEASE-025: SHA256SUMS 未登记 $batchContractRelative。" }

$forbidden = Get-ChildItem -LiteralPath $path -Recurse -Force | Where-Object { $_.FullName -match '\\(Modules|ModuleData|dist|_build_out|candidate_frozen|pending_game)(\\|$)' }
if ($forbidden) { throw 'WB-RELEASE-012: 发布包包含运行时或冻结候选路径。' }
Write-Output "PASS: Worldbook Studio release check $path"
