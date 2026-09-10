param(
    [string]$Output = "artifacts\current-test\WorldbookStudio",
    [switch]$RunSmoke
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = [System.IO.Path]::GetFullPath((Join-Path $root $Output))
$currentPackage = [System.IO.Path]::GetFullPath((Join-Path $root 'artifacts\current-test\WorldbookStudio'))
$currentTestRoot = Split-Path -Parent $currentPackage
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
$rid = 'win-x64'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction Stop
Add-Type -AssemblyName System.IO.Compression -ErrorAction Stop
$batchContractRelative = 'schemas\batch\awake.worldbook.batch-authoring-contracts-r14.json'
$batchContractSource = [System.IO.Path]::GetFullPath((Join-Path $root '..\..\docs\superpowers\specs\2026-08-25-worldbook-studio-batch-authoring-contracts-r14.json'))
$approvedBatchContractSha256 = 'df19cc670db082bed8702c2cd95c60c22c91b5340a27d841d915514810bd8ffb'

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

function Get-WebAssetsFingerprint([string]$directory) {
    $entries = foreach ($file in @(Get-ChildItem -LiteralPath $directory -Recurse -File | Sort-Object FullName)) {
        $relative = $file.FullName.Substring($directory.Length + 1).Replace('\', '/')
        $hash = Get-FileSha256 $file.FullName
        "$relative|$($file.Length)|$hash"
    }
    $payload = [string]::Join("`n", $entries)
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try { return [System.BitConverter]::ToString($algorithm.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($payload))).Replace('-', '').ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}

if (Test-Path -LiteralPath $out) {
    if ([string]::Equals($out, $currentPackage, [StringComparison]::OrdinalIgnoreCase)) {
        $existingManifest = Join-Path $out 'manifest.json'
        $existingManifestHash = if (Test-Path -LiteralPath $existingManifest -PathType Leaf) {
            (Get-FileHash -LiteralPath $existingManifest -Algorithm SHA256).Hash.ToLowerInvariant().Substring(0, 12)
        } else { 'no-manifest' }
        $archiveName = "current-test-$(Get-Date -Format 'yyyyMMdd-HHmmss')-$existingManifestHash"
        $archiveRoot = Join-Path $artifactRoot "archive\$(Get-Date -Format 'yyyy-MM-dd')\$archiveName"
        New-Item -ItemType Directory -Force -Path $archiveRoot | Out-Null
        Move-Item -LiteralPath $out -Destination (Join-Path $archiveRoot 'WorldbookStudio') -Force
        foreach ($fileName in @('WorldbookStudio-win-x64.zip', 'WorldbookStudio-win-x64.zip.sha256', 'CURRENT-TEST-PACKAGE.json')) {
            $filePath = Join-Path $currentTestRoot $fileName
            if (Test-Path -LiteralPath $filePath -PathType Leaf) { Move-Item -LiteralPath $filePath -Destination $archiveRoot -Force }
        }
        $evidencePath = Join-Path $currentTestRoot 'evidence'
        if (Test-Path -LiteralPath $evidencePath -PathType Container) { Move-Item -LiteralPath $evidencePath -Destination $archiveRoot -Force }
    } else {
        Remove-Item -LiteralPath $out -Recurse -Force
    }
}
New-Item -ItemType Directory -Force -Path $out | Out-Null

Push-Location $root
try {
    & dotnet restore Awake.WorldbookStudio.slnx --configfile NuGet.Config --locked-mode --force-evaluate --ignore-failed-sources --runtime $rid
    if ($LASTEXITCODE -ne 0) { throw 'WB-PACKAGE-003: RID 还原失败。' }
    & dotnet build Awake.WorldbookStudio.slnx --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'WB-PACKAGE-004: Release 构建失败。' }
    $publishCommon = @('--no-restore', '--configuration', 'Release', '--runtime', $rid, '--self-contained', 'true')
    & dotnet publish src\Awake.WorldbookStudio.Launcher\Awake.WorldbookStudio.Launcher.csproj @publishCommon '--output' (Join-Path $out 'launcher') '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' '-p:DebugType=None'
    if ($LASTEXITCODE -ne 0) { throw 'WB-PACKAGE-005: Launcher 发布失败。' }
    & dotnet publish src\Awake.WorldbookStudio.Web\Awake.WorldbookStudio.Web.csproj @publishCommon '--output' (Join-Path $out 'web') '-p:PublishSingleFile=false' '-p:DebugType=None'
    if ($LASTEXITCODE -ne 0) { throw 'WB-PACKAGE-006: Web 发布失败。' }
    & dotnet publish src\Awake.WorldbookStudio.Cli\Awake.WorldbookStudio.Cli.csproj @publishCommon '--output' (Join-Path $out 'cli') '-p:PublishSingleFile=false' '-p:DebugType=None'
    if ($LASTEXITCODE -ne 0) { throw 'WB-PACKAGE-007: CLI 发布失败。' }
    Copy-Item -LiteralPath (Join-Path $root '..\..\docs\worldbook-studio-plan') -Destination (Join-Path $out 'schemas') -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $root 'contracts') -Destination (Join-Path $out 'contracts') -Recurse -Force
    if (-not (Test-Path -LiteralPath $batchContractSource -PathType Leaf)) { throw 'WB-PACKAGE-014: Revision 14 批量合同缺失。' }
    $batchContract = Get-Content -LiteralPath $batchContractSource -Raw -Encoding UTF8 | ConvertFrom-Json
    $batchContractHash = (Get-FileHash -LiteralPath $batchContractSource -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($batchContract.revision -ne 14 -or $batchContract.schema_version -ne 'awake.worldbook.batch-authoring.contracts.v2' -or $batchContractHash -ne $approvedBatchContractSha256) { throw 'WB-PACKAGE-015: Revision 14 批量合同版本或哈希不符合批准契约。' }
    $batchContractOutput = Join-Path $out $batchContractRelative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $batchContractOutput) | Out-Null
    Copy-Item -LiteralPath $batchContractSource -Destination $batchContractOutput -Force
    $entityMappingSource = Join-Path $root '..\..\docs\mappings\persona-entity'
    if (-not (Test-Path -LiteralPath $entityMappingSource)) { throw 'WB-PACKAGE-010: 人物和家族目录缺失。' }
    $entityMappingOutput = Join-Path $out 'schemas\mappings\persona-entity'
    New-Item -ItemType Directory -Force -Path $entityMappingOutput | Out-Null
    Copy-Item -LiteralPath (Join-Path $entityMappingSource 'current-pointer.v1.json') -Destination $entityMappingOutput -Force
    $entityPointer = Get-Content -LiteralPath (Join-Path $entityMappingSource 'current-pointer.v1.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $entityGeneration = [string]$entityPointer.generation_relative_path
    if ([string]::IsNullOrWhiteSpace($entityGeneration)) { throw 'WB-PACKAGE-011: 人物和家族目录指针缺少当前 generation。' }
    $entityGenerationSource = Join-Path $entityMappingSource ($entityGeneration.Replace('/', '\'))
    if (-not (Test-Path -LiteralPath $entityGenerationSource)) { throw 'WB-PACKAGE-012: 人物和家族目录当前 generation 缺失。' }
    $entityGenerationOutput = Join-Path $entityMappingOutput ($entityGeneration.Replace('/', '\'))
    New-Item -ItemType Directory -Force -Path $entityGenerationOutput | Out-Null
    $entityGenerationSourceFull = [System.IO.Path]::GetFullPath($entityGenerationSource)
    $entityGenerationOutputFull = [System.IO.Path]::GetFullPath($entityGenerationOutput)
    foreach ($sourceFile in Get-ChildItem -LiteralPath $entityGenerationSourceFull -Recurse -File) {
        $sourceFileFull = [System.IO.Path]::GetFullPath($sourceFile.FullName)
        $relativeFile = $sourceFileFull.Substring($entityGenerationSourceFull.Length + 1)
        $targetFile = Join-Path $entityGenerationOutputFull $relativeFile
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $targetFile) | Out-Null
        [System.IO.File]::Copy("\\?\$sourceFileFull", "\\?\$targetFile", $true)
        $visible = $false
        for ($visibilityAttempt = 0; $visibilityAttempt -lt 10 -and -not $visible; $visibilityAttempt++) {
            $visible = [System.IO.File]::Exists("\\?\$targetFile")
            if (-not $visible) { Start-Sleep -Milliseconds 100 }
        }
        if (-not $visible) {
            throw "WB-PACKAGE-013: 人物和家族目录文件复制后不存在：$relativeFile。"
        }
        $sourceHash = [System.Security.Cryptography.SHA256]::Create()
        try {
            $sourceBytes = [System.IO.File]::ReadAllBytes("\\?\$sourceFileFull")
            $targetBytes = [System.IO.File]::ReadAllBytes("\\?\$targetFile")
            $sourceDigest = [System.BitConverter]::ToString($sourceHash.ComputeHash($sourceBytes)).Replace('-', '').ToLowerInvariant()
            $targetDigest = [System.BitConverter]::ToString($sourceHash.ComputeHash($targetBytes)).Replace('-', '').ToLowerInvariant()
        }
        finally { $sourceHash.Dispose() }
        if ($sourceDigest -ne $targetDigest) {
            throw "WB-PACKAGE-013: 人物和家族目录文件哈希不一致：$relativeFile。"
        }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $entityMappingOutput $entityGeneration.Replace('/', '\')))) { throw 'WB-PACKAGE-013: 人物和家族目录指针无法解析到当前 generation。' }
    Get-ChildItem -LiteralPath $entityMappingOutput -Recurse -File -Filter '*.tmp' | Remove-Item -Force
    Copy-Item -LiteralPath (Join-Path $root 'README_使用说明.txt') -Destination (Join-Path $out 'README_使用说明.txt') -Force
    Copy-Item -LiteralPath (Join-Path $root '新手指引_世界书内容编辑者.md') -Destination (Join-Path $out '新手指引_世界书内容编辑者.md') -Force

    $launcherExe = Join-Path $out 'launcher\Awake.WorldbookStudio.Launcher.exe'
    $webExe = Join-Path $out 'web\Awake.WorldbookStudio.Web.exe'
    $cliExe = Join-Path $out 'cli\worldbook-studio.exe'
    if (-not (Test-Path -LiteralPath $launcherExe) -or -not (Test-Path -LiteralPath $webExe) -or -not (Test-Path -LiteralPath $cliExe)) { throw 'WB-PACKAGE-002: 发布 EXE 缺失。' }
    Move-Item -LiteralPath $launcherExe -Destination (Join-Path $out 'Awake.WorldbookStudio.Launcher.exe') -Force
    Remove-Item -LiteralPath (Join-Path $out 'launcher') -Recurse -Force

    $manifestFiles = Get-ChildItem -LiteralPath $out -Recurse -File | Where-Object { $_.Name -notin @('manifest.json', 'SHA256SUMS.txt') } | Sort-Object FullName
    $entries = foreach ($file in $manifestFiles) {
        $relative = $file.FullName.Substring($out.Length + 1).Replace('\', '/')
        [ordered]@{ path = $relative; sha256 = Get-FileSha256 $file.FullName; length = $file.Length }
    }
    $manifest = [ordered]@{ schemaVersion = 1; product = 'AWAKE.WorldbookStudio'; rid = $rid; selfContained = $true; files = @($entries) }
    $manifestJson = $manifest | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText((Join-Path $out 'manifest.json'), $manifestJson + [Environment]::NewLine, $utf8NoBom)

    $sumPath = Join-Path $out 'SHA256SUMS.txt'
    $sumLines = foreach ($entry in $entries) { "$($entry.sha256)  $($entry.path)" }
    [System.IO.File]::WriteAllText($sumPath, ($sumLines -join [Environment]::NewLine) + [Environment]::NewLine, $utf8NoBom)

    & "$PSScriptRoot\release-check.ps1" -Package $out
    $zip = Join-Path (Split-Path -Parent $out) 'WorldbookStudio-win-x64.zip'
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
    $zipStream = [System.IO.File]::Open("\\?\$zip", [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    $zipArchive = [System.IO.Compression.ZipArchive]::new(
        $zipStream,
        [System.IO.Compression.ZipArchiveMode]::Create,
        $false,
        [System.Text.Encoding]::UTF8)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $out -Recurse -File | Where-Object { $_.Name -notin @('manifest.json', 'SHA256SUMS.txt') }) {
            $fileFull = [System.IO.Path]::GetFullPath($file.FullName)
            $relative = $fileFull.Substring($out.Length + 1).Replace('\', '/')
            $entry = $zipArchive.CreateEntry($relative, [System.IO.Compression.CompressionLevel]::Optimal)
            $inputStream = [System.IO.File]::OpenRead("\\?\$fileFull")
            $outputStream = $null
            try {
                $outputStream = $entry.Open()
                $inputStream.CopyTo($outputStream)
            }
            finally {
                if ($null -ne $outputStream) { $outputStream.Dispose() }
                $inputStream.Dispose()
            }
        }
        foreach ($metadataFile in @('manifest.json', 'SHA256SUMS.txt')) {
            $metadataPath = Join-Path $out $metadataFile
            $entry = $zipArchive.CreateEntry($metadataFile, [System.IO.Compression.CompressionLevel]::Optimal)
            $inputStream = [System.IO.File]::OpenRead("\\?\$([System.IO.Path]::GetFullPath($metadataPath))")
            $outputStream = $null
            try {
                $outputStream = $entry.Open()
                $inputStream.CopyTo($outputStream)
            }
            finally {
                if ($null -ne $outputStream) { $outputStream.Dispose() }
                $inputStream.Dispose()
            }
        }
    }
    finally {
        $zipArchive.Dispose()
        $zipStream.Dispose()
    }
    (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant() | Set-Content -LiteralPath ($zip + '.sha256') -Encoding ASCII
    if ([string]::Equals($out, $currentPackage, [StringComparison]::OrdinalIgnoreCase)) {
        $currentPointer = [ordered]@{
            schemaVersion = 1
            status = 'current-test'
            packageName = 'AWAKE.WorldbookStudio.current-test'
            packageRoot = 'WorldbookStudio'
            launcherPath = 'WorldbookStudio\Awake.WorldbookStudio.Launcher.exe'
            zipPath = 'WorldbookStudio-win-x64.zip'
             generatedAtLocal = [DateTimeOffset]::Now.ToString('O')
             manifestSha256 = (Get-FileHash -LiteralPath (Join-Path $out 'manifest.json') -Algorithm SHA256).Hash.ToLowerInvariant()
             sourceFrontendSha256 = (Get-FileHash -LiteralPath (Join-Path $root 'src\Awake.WorldbookStudio.Web\wwwroot\studio-batch.js') -Algorithm SHA256).Hash.ToLowerInvariant()
             sourceWebAssetsSha256 = Get-WebAssetsFingerprint (Join-Path $root 'src\Awake.WorldbookStudio.Web\wwwroot')
             sourceWebAssemblySha256 = (Get-FileHash -LiteralPath (Join-Path $root 'src\Awake.WorldbookStudio.Web\bin\Release\net10.0\win-x64\Awake.WorldbookStudio.Web.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
             sourceCoreAssemblySha256 = (Get-FileHash -LiteralPath (Join-Path $root 'src\Awake.WorldbookStudio.Core\bin\Release\net10.0\Awake.WorldbookStudio.Core.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
             frontendSha256 = (Get-FileHash -LiteralPath (Join-Path $out 'web\wwwroot\studio-batch.js') -Algorithm SHA256).Hash.ToLowerInvariant()
             packageWebAssetsSha256 = Get-WebAssetsFingerprint (Join-Path $out 'web\wwwroot')
             webAssemblySha256 = (Get-FileHash -LiteralPath (Join-Path $out 'web\Awake.WorldbookStudio.Web.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
            coreAssemblySha256 = (Get-FileHash -LiteralPath (Join-Path $out 'web\Awake.WorldbookStudio.Core.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
        }
        $currentPointer | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $currentTestRoot 'CURRENT-TEST-PACKAGE.json') -Encoding UTF8
    }
    $zipArchive = $null
    try {
        $zipArchive = [System.IO.Compression.ZipFile]::OpenRead($zip)
        $draftEntries = @($zipArchive.Entries | Where-Object { $_.FullName.Replace('\', '/') -eq 'web/wwwroot/studio-draft.js' })
        if ($draftEntries.Count -ne 1) { throw "WB-PACKAGE-016: ZIP 中 studio-draft.js 条目数量异常（count=$($draftEntries.Count)）。" }
        $draftEntry = $draftEntries[0]
        $sourceDraftScriptPath = Join-Path $root 'src\Awake.WorldbookStudio.Web\wwwroot\studio-draft.js'
        $sourceDraftScriptHash = (Get-FileHash -LiteralPath $sourceDraftScriptPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $hashAlgorithm = [System.Security.Cryptography.SHA256]::Create()
        $draftStream = $null
        try {
            $draftStream = $draftEntry.Open()
            $zipDraftScriptHash = [System.BitConverter]::ToString($hashAlgorithm.ComputeHash($draftStream)).Replace('-', '').ToLowerInvariant()
        }
        finally {
            if ($null -ne $draftStream) { $draftStream.Dispose() }
            $hashAlgorithm.Dispose()
        }
        if ($zipDraftScriptHash -ne $sourceDraftScriptHash) { throw "WB-PACKAGE-017: ZIP 中的 studio-draft.js 与当前源码不一致（source=$sourceDraftScriptHash zip=$zipDraftScriptHash）。" }
    }
    finally { if ($null -ne $zipArchive) { $zipArchive.Dispose() } }
    Write-Output "PACKAGE: $out"
    Write-Output "ZIP: $zip"
    if ($RunSmoke) {
        $evidenceRoot = Join-Path (Split-Path -Parent $out) 'evidence'
        & "$PSScriptRoot\launcher-tests.ps1" -PackagePath $out -EvidencePath (Join-Path $evidenceRoot 'launcher-tests.v2.json')
        if ($LASTEXITCODE -ne 0) { throw 'WB-PACKAGE-008: Launcher seam tests failed.' }
        foreach ($scenario in @('clean-start', 'browser-failure', 'stale-settings-missing', 'stale-settings-marker', 'duplicate-launch', 'graceful-shutdown')) {
            & "$PSScriptRoot\smoke.ps1" -Package $out -Scenario $scenario -EvidencePath (Join-Path $evidenceRoot ($scenario + '.v2.json'))
            if ($LASTEXITCODE -ne 0) { throw "WB-PACKAGE-009: smoke 场景失败 $scenario。" }
        }
    }
}
finally { Pop-Location }
