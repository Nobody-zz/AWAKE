[CmdletBinding()]
param(
    [string]$OutputRoot = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'artifacts\customer-delivery'),
    [switch]$RunOffline,
    [switch]$RunWorker
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$deliveryToolRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$awakeRoot = [IO.Path]::GetFullPath((Join-Path $deliveryToolRoot '..\..'))
$outputRootFull = [IO.Path]::GetFullPath($OutputRoot)
$utf8NoBom = [Text.UTF8Encoding]::new($false)

function Fail([string]$Code, [string]$Message) {
    throw "${Code}: $Message"
}

function Get-Full([string]$Path) {
    return [IO.Path]::GetFullPath($Path)
}

function Get-Hash([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-BytesHash([byte[]]$Bytes) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($algorithm.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Write-Utf8([string]$Path, [string]$Content) {
    [IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

function Get-Relative([string]$Base, [string]$Path) {
    $baseFull = (Get-Full $Base).TrimEnd([char]'\', [char]'/') + [IO.Path]::DirectorySeparatorChar
    $pathFull = Get-Full $Path
    if (-not $pathFull.StartsWith($baseFull, [StringComparison]::OrdinalIgnoreCase)) {
        Fail 'WB-DELIVERY-403' "路径越过根目录：$Path"
    }
    return $pathFull.Substring($baseFull.Length).Replace('\', '/')
}

function Assert-Under([string]$Path, [string]$Base, [string]$Code = 'WB-DELIVERY-403') {
    $full = Get-Full $Path
    $baseFull = (Get-Full $Base).TrimEnd([char]'\', [char]'/')
    $prefix = $baseFull + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -and
        -not [string]::Equals($full, $baseFull, [StringComparison]::OrdinalIgnoreCase)) {
        Fail $Code "路径必须位于指定根目录：$full"
    }
}

function Get-CanonicalJson([object]$Value) {
    return ($Value | ConvertTo-Json -Depth 100 -Compress)
}

function Get-CanonicalManifestHash([object]$Manifest) {
    $clone = Get-CanonicalJson $Manifest | ConvertFrom-Json -Depth 100
    $clone.metadata_sha256.'delivery-manifest.json' = $null
    return Get-BytesHash ([Text.Encoding]::UTF8.GetBytes((Get-CanonicalJson $clone)))
}

function Get-PackageTreeRecords([string]$Root) {
    foreach ($file in @(Get-ChildItem -LiteralPath $Root -Recurse -File -Force)) {
        if ($file.Name -in @(
            'manifest.json',
            'delivery-manifest.json',
            'SHA256SUMS.txt',
            'PACKAGE-MANIFEST.sha256.txt',
            'BUILD-SOURCE-MANIFEST.sha256.txt',
            'BUILD-ID.txt',
            'package-verification.json'
        ) -or $file.Extension -in @('.zip', '.sha256')) {
            continue
        }
        $relative = Get-Relative $Root $file.FullName
        "$relative|$($file.Length)|$(Get-Hash $file.FullName)"
    }
}

function Get-PackageTreeHash([string]$Root) {
    $records = @((Get-PackageTreeRecords $Root) | Sort-Object)
    return Get-BytesHash ([Text.Encoding]::UTF8.GetBytes([string]::Join("`n", $records)))
}

function Get-ArchiveContentHash([string]$Root) {
    $records = foreach ($file in @(Get-ChildItem -LiteralPath $Root -Recurse -File -Force)) {
        $relative = Get-Relative $Root $file.FullName
        if ($relative -eq 'delivery-manifest.json' -or $relative -eq 'package-verification.json') {
            continue
        }
        "$relative|$($file.Length)|$(Get-Hash $file.FullName)"
    }
    return Get-BytesHash ([Text.Encoding]::UTF8.GetBytes([string]::Join("`n", @($records | Sort-Object))))
}

function Assert-ZipAndExtract([string]$ZipPath, [string]$ExpectedRoot, [string]$ExpectedTreeHash) {
    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    $extractRoot = Join-Path ([IO.Path]::GetTempPath()) ("awake-delivery-extract-" + [Guid]::NewGuid().ToString('N'))
    try {
        $names = @{}
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace('\', '/')
            if ([string]::IsNullOrWhiteSpace($name) -or $name.EndsWith('/')) { continue }
            if ($name.StartsWith('/') -or $name.Contains('../') -or $name -match '(^|/)\.\.(/|$)') {
                Fail 'WB-DELIVERY-422' "ZIP 含路径穿越条目：$name"
            }
            if ($names.ContainsKey($name)) { Fail 'WB-DELIVERY-422' "ZIP 含重复条目：$name" }
            $names[$name] = $true
        }
        New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null
        [IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $extractRoot)
        Assert-NoUnsafeFiles $extractRoot
        $actual = @(Get-ChildItem -LiteralPath $extractRoot -Recurse -File -Force | ForEach-Object {
            [pscustomobject]@{
                path = Get-Relative $extractRoot $_.FullName
                length = $_.Length
                sha256 = Get-Hash $_.FullName
            }
        })
        $expected = @(Get-ChildItem -LiteralPath $ExpectedRoot -Recurse -File -Force | ForEach-Object {
            [pscustomobject]@{
                path = Get-Relative $ExpectedRoot $_.FullName
                length = $_.Length
                sha256 = Get-Hash $_.FullName
            }
        })
        $expectedByPath = @{}
        foreach ($item in $expected) { $expectedByPath[$item.path] = $item }
        foreach ($item in $actual) {
            if (-not $expectedByPath.ContainsKey($item.path)) { Fail 'WB-DELIVERY-422' "ZIP 解压出现额外文件：$($item.path)" }
            $match = $expectedByPath[$item.path]
            if ($match.length -ne $item.length -or $match.sha256 -ne $item.sha256) { Fail 'WB-DELIVERY-422' "ZIP 解压文件哈希不匹配：$($item.path)" }
        }
        if ($actual.Count -ne $expected.Count) { Fail 'WB-DELIVERY-422' 'ZIP 解压文件集合不完整。' }
        if ((Get-PackageTreeHash $extractRoot) -ne $ExpectedTreeHash) { Fail 'WB-DELIVERY-422' 'ZIP 解压 tree hash 不匹配。' }
    }
    finally {
        if ($null -ne $archive) { $archive.Dispose() }
        if (Test-Path -LiteralPath $extractRoot -PathType Container) { Remove-Item -LiteralPath $extractRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

function Assert-NoUnsafeFiles([string]$Root) {
    foreach ($entry in @(Get-ChildItem -LiteralPath $Root -Recurse -Force)) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            Fail 'WB-DELIVERY-422' "不允许符号链接或重解析点：$($entry.FullName)"
        }
        if ($entry.Name -match '(?i)(^|\.)(pdb|log|tmp|temp)$|(^|[-_.])(secret|password|apikey|api-key|token)([-_.]|$)') {
            Fail 'WB-DELIVERY-422' "不允许敏感或临时文件：$($entry.FullName)"
        }
    }
}

function Read-Json([string]$Path) {
    try {
        return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json -Depth 100
    }
    catch {
        Fail 'WB-DELIVERY-422' "JSON 无法解析：$Path；$($_.Exception.Message)"
    }
}

function Get-SumEntries([string]$Path) {
    $entries = @{}
    foreach ($line in @(Get-Content -LiteralPath $Path)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -notmatch '^([0-9A-Fa-f]{64})\s{2}(.+)$') {
            Fail 'WB-DELIVERY-422' "哈希清单格式无效：$Path"
        }
        $entries[$matches[2].Replace('\', '/')] = $matches[1].ToLowerInvariant()
    }
    return $entries
}

function Assert-ChildPackage([string]$Root, [string]$ToolId, [string]$ZipPath) {
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        Fail 'WB-DELIVERY-404' "缺少 $ToolId 子包目录：$Root"
    }
    if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
        Fail 'WB-DELIVERY-404' "缺少 $ToolId 子包 ZIP：$ZipPath"
    }
    Assert-NoUnsafeFiles $Root
    $manifestPath = Join-Path $Root 'manifest.json'
    $sumPath = Join-Path $Root 'SHA256SUMS.txt'
    $packageManifestPath = Join-Path $Root 'PACKAGE-MANIFEST.sha256.txt'
    $sourceManifestPath = Join-Path $Root 'BUILD-SOURCE-MANIFEST.sha256.txt'
    $manifest = $null
    if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
        $manifest = Read-Json $manifestPath
        if ($null -eq $manifest.files) {
            Fail 'WB-DELIVERY-422' "$ToolId manifest 缺少 files：$manifestPath"
        }
        foreach ($entry in @($manifest.files)) {
            $file = Join-Path $Root ($entry.path.Replace('/', '\'))
            if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
                Fail 'WB-DELIVERY-422' "$ToolId manifest 引用缺失文件：$($entry.path)"
            }
            if ((Get-Hash $file) -ne $entry.sha256.ToLowerInvariant() -or
                (Get-Item -LiteralPath $file).Length -ne [int64]$entry.length) {
                Fail 'WB-DELIVERY-422' "$ToolId manifest 哈希不匹配：$($entry.path)"
            }
        }
    }
    $sumFile = if (Test-Path -LiteralPath $sumPath -PathType Leaf) { $sumPath } elseif (Test-Path -LiteralPath $packageManifestPath -PathType Leaf) { $packageManifestPath } else { $null }
    if ($null -eq $sumFile) {
        Fail 'WB-DELIVERY-422' "$ToolId 缺少 manifest 或哈希清单：manifest=$manifestPath sum=$sumPath package_manifest=$packageManifestPath root=$Root"
    }
    $sumEntries = Get-SumEntries $sumFile
    foreach ($relative in $sumEntries.Keys) {
        $file = Join-Path $Root ($relative.Replace('/', '\'))
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            Fail 'WB-DELIVERY-422' "$ToolId 清单引用缺失文件：$relative"
        }
        if ((Get-Hash $file) -ne $sumEntries[$relative]) {
            Fail 'WB-DELIVERY-422' "$ToolId 清单哈希不匹配：$relative"
        }
    }
    $zipSidecar = "$ZipPath.sha256"
    if (-not (Test-Path -LiteralPath $zipSidecar -PathType Leaf)) {
        Fail 'WB-DELIVERY-422' "$ToolId 缺少 ZIP sidecar。"
    }
    $zipLine = (Get-Content -LiteralPath $zipSidecar | Select-Object -First 1)
    $zipHash = Get-Hash $ZipPath
    $zipMatch = $zipLine -match '^([0-9A-Fa-f]{64})\s{2}(.+)$'
    $declaredZipHash = if ($zipMatch) { $matches[1].ToLowerInvariant() } else { $null }
    $declaredZipName = if ($zipMatch) { $matches[2] } else { $null }
    $legacyZipMatch = $zipLine -match '^([0-9A-Fa-f]{64})$'
    $declaredHash = if ($zipMatch) { $declaredZipHash } elseif ($legacyZipMatch) { $matches[1].ToLowerInvariant() } else { $null }
    if ((-not $zipMatch -and -not $legacyZipMatch) -or
        ($zipMatch -and $declaredZipName -ne [IO.Path]::GetFileName($ZipPath)) -or
        $declaredHash -ne $zipHash) {
        Fail 'WB-DELIVERY-422' "$ToolId ZIP sidecar 哈希不匹配。"
    }
    $sourceManifest = if (Test-Path -LiteralPath $sourceManifestPath) { $sourceManifestPath } else { $manifestPath }
    return [pscustomobject]@{
        tool_id = $ToolId
        root = (Get-Full $Root)
        zip = (Get-Full $ZipPath)
        manifest = $manifest
        source_manifest_path = $sourceManifest
        source_manifest_sha256 = Get-Hash $sourceManifest
        package_tree_sha256 = Get-PackageTreeHash $Root
        zip_sha256 = Get-Hash $ZipPath
    }
}

function Find-Latest([string]$ArtifactsRoot, [scriptblock]$Predicate) {
    $candidates = @(Get-ChildItem -LiteralPath $ArtifactsRoot -Recurse -Directory -Force -ErrorAction SilentlyContinue | Where-Object { & $Predicate $_ })
    if ($candidates.Count -eq 0) { return $null }
    return $candidates | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
}

function Resolve-Inputs {
    $wbsArtifacts = Join-Path $awakeRoot 'tools\worldbook-studio\artifacts'
    $pwbArtifacts = Join-Path $awakeRoot 'tools\persona-workbench\artifacts'
    $uiArtifacts = Join-Path $awakeRoot 'tools\ui-workstation\artifacts'
    $wbsRoot = Join-Path $wbsArtifacts 'current-test\WorldbookStudio'
    $wbsZip = Join-Path $wbsArtifacts 'current-test\WorldbookStudio-win-x64.zip'
    if (-not (Test-Path -LiteralPath $wbsRoot -PathType Container) -or -not (Test-Path -LiteralPath $wbsZip -PathType Leaf)) {
        $wbsCandidate = Find-Latest $wbsArtifacts {
            param($d)
            (Test-Path (Join-Path $d.FullName 'manifest.json') -PathType Leaf) -and
            ((Read-Json (Join-Path $d.FullName 'manifest.json')).product -eq 'AWAKE.WorldbookStudio')
        }
        if ($null -eq $wbsCandidate) { Fail 'WB-DELIVERY-404' '找不到可消费的 WBS 子包。' }
        $wbsRoot = $wbsCandidate.FullName
        $wbsZip = Get-ChildItem -LiteralPath $wbsArtifacts -Filter '*.zip' -File | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
    $pwbRoot = Find-Latest $pwbArtifacts {
        param($d)
        (Test-Path (Join-Path $d.FullName 'PACKAGE-MANIFEST.sha256.txt') -PathType Leaf) -and
        (Test-Path (Join-Path $d.Parent.FullName "$($d.Name).zip") -PathType Leaf)
    }
    if ($null -eq $pwbRoot) { Fail 'WB-DELIVERY-404' '找不到可消费的 PWB 子包。' }
    $pwbZip = Join-Path $pwbRoot.Parent.FullName "$($pwbRoot.Name).zip"
    $uiRoot = Find-Latest $uiArtifacts {
        param($d)
        (Test-Path (Join-Path $d.FullName 'manifest.json') -PathType Leaf) -and
        (Test-Path (Join-Path $d.Parent.FullName "$($d.Name).zip") -PathType Leaf)
    }
    if ($null -eq $uiRoot) { Fail 'WB-DELIVERY-404' '找不到可消费的 UI Workstation 子包。' }
    $uiZip = Join-Path $uiRoot.Parent.FullName "$($uiRoot.Name).zip"
    return @(
        Assert-ChildPackage $uiRoot.FullName 'ui_workstation' $uiZip
        Assert-ChildPackage $wbsRoot 'worldbook_studio' $wbsZip
        Assert-ChildPackage $pwbRoot.FullName 'persona_workbench' $pwbZip
    )
}

function Get-BuildId([object[]]$Inputs) {
    $tuple = [string]::Join("`n", @($Inputs | Sort-Object tool_id | ForEach-Object { "$($_.tool_id)|$($_.source_manifest_sha256)" }))
    $tupleHash = Get-BytesHash ([Text.Encoding]::UTF8.GetBytes($tuple))
    $suffix = (Get-Random -Minimum 0 -Maximum 2147483647).ToString('x8')
    return "awake-customer-$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff'))-$($tupleHash.Substring(0,12))-$suffix"
}

function Assert-BuildIdAvailable([string]$BuildId) {
    $buildRoot = Join-Path $outputRootFull $BuildId
    $zip = Join-Path $outputRootFull "$BuildId.zip"
    $pointer = Join-Path $outputRootFull 'rollback-pointer.json'
    if ((Test-Path -LiteralPath $buildRoot) -or (Test-Path -LiteralPath $zip)) {
        Fail 'WB-DELIVERY-409' "BuildId 已存在，拒绝覆盖：$BuildId"
    }
    if (Test-Path -LiteralPath $pointer -PathType Leaf) {
        $pointerValue = Read-Json $pointer
        if ($pointerValue.current_build_id -eq $BuildId -or $pointerValue.previous_build_id -eq $BuildId) {
            Fail 'WB-DELIVERY-409' "BuildId 已被回滚指针引用：$BuildId"
        }
    }
}

function Get-RelativeFileEntries([string]$Root) {
    foreach ($file in @(Get-ChildItem -LiteralPath $Root -Recurse -File -Force)) {
        $relative = Get-Relative $Root $file.FullName
        if ($relative -eq 'delivery-manifest.json' -or
            $relative -eq 'SHA256SUMS.txt' -or
            $relative -eq 'BUILD-ID.txt' -or
            $relative -eq 'package-verification.json' -or
            $relative -match '(^|/)[^/]+\.zip$' -or
            $relative -match '(^|/)[^/]+\.zip\.sha256$') {
            continue
        }
        [ordered]@{ path = $relative; sha256 = Get-Hash $file.FullName; length = $file.Length }
    }
}

function Get-RollbackCandidate([string]$BuildId, [string]$PreviousBuildId) {
    [ordered]@{
        schema_version = 'awake.customer.rollback-pointer.v1'
        current_build_id = $BuildId
        previous_build_id = if ([string]::IsNullOrWhiteSpace($PreviousBuildId)) { $null } else { $PreviousBuildId }
        current_root = "$BuildId"
        previous_root = if ([string]::IsNullOrWhiteSpace($PreviousBuildId)) { $null } else { "$PreviousBuildId" }
        updated_at_utc = [DateTime]::UtcNow.ToString('o')
    }
}

function Assert-Pointer([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $value = Read-Json $Path
    foreach ($field in @('schema_version','current_build_id','previous_build_id','current_root','previous_root','updated_at_utc')) {
        if ($null -eq $value.PSObject.Properties[$field]) { Fail 'WB-DELIVERY-422' "rollback pointer 缺少字段：$field" }
    }
    if ($value.schema_version -ne 'awake.customer.rollback-pointer.v1') { Fail 'WB-DELIVERY-422' 'rollback pointer schema_version 无效。' }
    if ($value.current_build_id -notmatch '^awake-customer-[A-Za-z0-9._-]+$') { Fail 'WB-DELIVERY-422' 'rollback pointer current_build_id 无效。' }
    if ($value.current_root -ne $value.current_build_id) { Fail 'WB-DELIVERY-422' 'rollback pointer current_root 不匹配。' }
    $currentRoot = Join-Path $outputRootFull $value.current_root
    if (-not (Test-Path -LiteralPath $currentRoot -PathType Container)) { Fail 'WB-DELIVERY-422' 'rollback pointer 指向不存在的当前构建。' }
    return $value
}

function Write-Atomic([string]$Path, [string]$Content) {
    $temp = "$Path.$([Guid]::NewGuid().ToString('N')).tmp"
    $backup = "$Path.$([Guid]::NewGuid().ToString('N')).bak"
    try {
        Write-Utf8 $temp $Content
        if (Test-Path -LiteralPath $Path -PathType Leaf) {
            [IO.File]::Replace($temp, $Path, $backup)
        }
        else {
            [IO.File]::Move($temp, $Path)
        }
    }
    finally {
        if (Test-Path -LiteralPath $temp -PathType Leaf) { Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue }
        if (Test-Path -LiteralPath $backup -PathType Leaf) { Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue }
    }
}

if ($outputRootFull.StartsWith((Get-Full 'D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord'), [StringComparison]::OrdinalIgnoreCase)) {
    Fail 'WB-DELIVERY-GAME-403' '客户交付不得写入游戏目录。'
}
foreach ($protectedRoot in @(
    (Get-Full (Join-Path $awakeRoot 'tools')),
    (Get-Full (Join-Path $awakeRoot 'src')),
    (Get-Full (Join-Path $awakeRoot 'ModuleData'))
)) {
    if ($outputRootFull.StartsWith($protectedRoot.TrimEnd([char]'\', [char]'/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        Fail 'WB-DELIVERY-403' "客户交付不得写入源码或工具目录：$outputRootFull"
    }
}
New-Item -ItemType Directory -Force -Path $outputRootFull | Out-Null

$inputs = Resolve-Inputs
$buildId = Get-BuildId $inputs
if ($env:AWAKE_CUSTOMER_DELIVERY_TEST_BUILD_ID) { $buildId = $env:AWAKE_CUSTOMER_DELIVERY_TEST_BUILD_ID }
Assert-BuildIdAvailable $buildId

$oldPointerPath = Join-Path $outputRootFull 'rollback-pointer.json'
$oldPointer = Assert-Pointer $oldPointerPath
$previousBuildId = if ($null -eq $oldPointer) { $null } else { [string]$oldPointer.current_build_id }
$candidatePointer = Get-RollbackCandidate $buildId $previousBuildId
$candidatePointerBytes = [Text.Encoding]::UTF8.GetBytes((Get-CanonicalJson $candidatePointer))
$candidatePointerHash = Get-BytesHash $candidatePointerBytes

$stagingRoot = Join-Path $outputRootFull ".staging-$([Guid]::NewGuid().ToString('N'))"
$buildRoot = Join-Path $outputRootFull $buildId
$rootZip = Join-Path $outputRootFull "$buildId.zip"
$rootZipSidecar = "$rootZip.sha256"
$tempRootZip = Join-Path $outputRootFull ".$buildId.zip.tmp"
New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null
try {
    foreach ($input in $inputs) {
        $toolDir = Join-Path $stagingRoot $input.tool_id.Replace('_', '-')
        New-Item -ItemType Directory -Force -Path $toolDir | Out-Null
        Copy-Item -Path (Join-Path $input.root '*') -Destination $toolDir -Recurse -Force
        Copy-Item -LiteralPath $input.zip -Destination (Join-Path $toolDir "$($input.tool_id).zip") -Force
        Copy-Item -LiteralPath "$($input.zip).sha256" -Destination (Join-Path $toolDir "$($input.tool_id).zip.sha256") -Force
    }
    $docsRoot = Join-Path $stagingRoot 'docs'
    New-Item -ItemType Directory -Force -Path $docsRoot | Out-Null
    $sourceDocs = Join-Path $deliveryToolRoot 'docs'
    foreach ($doc in @('README.md','安装说明.md','故障排查.md','恢复与回滚.md','本地Worker配置.md','已知限制.md','证据边界.md')) {
        $source = Join-Path $sourceDocs $doc
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { Fail 'WB-DELIVERY-404' "缺少客户文档模板：$source" }
        Copy-Item -LiteralPath $source -Destination (Join-Path $docsRoot $doc) -Force
    }
    Assert-NoUnsafeFiles $stagingRoot

    $stagingFiles = @(Get-RelativeFileEntries $stagingRoot)
    $toolRecords = foreach ($input in @($inputs | Sort-Object tool_id)) {
        $packagePath = "$($input.tool_id.Replace('_','-'))"
        [ordered]@{
            tool_id = $input.tool_id
            source_manifest_sha256 = $input.source_manifest_sha256
            package_root = $packagePath
            package_tree_sha256 = Get-PackageTreeHash (Join-Path $stagingRoot $packagePath)
            zip_path = "$packagePath/$($input.tool_id).zip"
            zip_sha256 = $input.zip_sha256
            entrypoint = switch ($input.tool_id) {
                'ui_workstation' { 'ui-workstation/start-ui-workstation.ps1' }
                'worldbook_studio' { 'worldbook-studio/Awake.WorldbookStudio.Launcher.exe' }
                'persona_workbench' { 'persona-workbench/PersonaWorkbench.Launcher.exe' }
            }
            status = 'verified'
        }
    }
    $documents = foreach ($file in @(Get-ChildItem -LiteralPath (Join-Path $stagingRoot 'docs') -File | Sort-Object Name)) {
        [ordered]@{ path = "docs/$($file.Name)"; sha256 = Get-Hash $file.FullName; status = 'verified' }
    }
    $files = @($stagingFiles | Sort-Object path)
    $manifest = [ordered]@{
        schema_version = 'awake.customer.delivery-manifest.v1'
        build_id = $buildId
        generated_at_utc = [DateTime]::UtcNow.ToString('o')
        source_revision = "source-tuple-$([string]::Join('', @($inputs | Sort-Object tool_id | ForEach-Object { $_.source_manifest_sha256.Substring(0, 12) })))"
        tools = @($toolRecords)
        documents = @($documents)
        files = @($files)
        package_sha256 = Get-PackageTreeHash $stagingRoot
        zip_sha256 = Get-ArchiveContentHash $stagingRoot
        metadata_sha256 = [ordered]@{
            'delivery-manifest.json' = $null
            'SHA256SUMS.txt' = '0' * 64
            'BUILD-ID.txt' = '0' * 64
            'package-verification.json' = '0' * 64
        }
        rollback_pointer_sha256 = $candidatePointerHash
        verification = [ordered]@{
            source_bound = $true
            tree_bound = $true
            zip_bound = $true
            extracted_bound = $true
            offline_passed = $false
            local_worker_passed = $false
            ready_for_user_verification = $true
        }
    }
    $buildIdText = "$buildId`n"
    $sumLines = foreach ($file in @($files)) { "$($file.sha256)  $($file.path)" }
    $sumText = ([string]::Join("`n", $sumLines) + "`n")
    Write-Utf8 (Join-Path $stagingRoot 'BUILD-ID.txt') $buildIdText
    Write-Utf8 (Join-Path $stagingRoot 'SHA256SUMS.txt') $sumText

    $verification = [ordered]@{
        schema_version = 'awake.customer.package-verification.v1'
        build_id = $buildId
        source_manifest_hashes = [ordered]@{}
        package_tree_sha256 = $manifest.package_sha256
        zip_content_sha256 = $manifest.zip_sha256
        zip_file_sha256 = $null
        extracted_tree_sha256 = $manifest.package_sha256
        checks = [ordered]@{
            source_bound = $true
            child_packages = $true
            root_tree = $true
            zip = $true
            extracted = $true
        }
        evidence_status = [ordered]@{
            offline = if ($RunOffline) { 'not_run_by_package_builder' } else { 'not_run' }
            local_worker = if ($RunWorker) { 'not_run_by_package_builder' } else { 'not_run' }
        }
    }
    foreach ($input in $inputs) { $verification.source_manifest_hashes[$input.tool_id] = $input.source_manifest_sha256 }
    $verificationText = Get-CanonicalJson $verification
    Write-Utf8 (Join-Path $stagingRoot 'package-verification.json') $verificationText
    $manifest.package_sha256 = Get-PackageTreeHash $stagingRoot
    $manifest.zip_sha256 = Get-ArchiveContentHash $stagingRoot
    $verification.package_tree_sha256 = $manifest.package_sha256
    $verification.zip_content_sha256 = $manifest.zip_sha256
    Write-Utf8 (Join-Path $stagingRoot 'package-verification.json') (Get-CanonicalJson $verification)
    $manifest.metadata_sha256.'SHA256SUMS.txt' = Get-Hash (Join-Path $stagingRoot 'SHA256SUMS.txt')
    $manifest.metadata_sha256.'BUILD-ID.txt' = Get-Hash (Join-Path $stagingRoot 'BUILD-ID.txt')
    $manifest.metadata_sha256.'package-verification.json' = Get-Hash (Join-Path $stagingRoot 'package-verification.json')
    $manifest.metadata_sha256.'delivery-manifest.json' = Get-CanonicalManifestHash $manifest
    Write-Utf8 (Join-Path $stagingRoot 'delivery-manifest.json') (Get-CanonicalJson $manifest)

    New-Item -ItemType Directory -Force -Path $buildRoot | Out-Null
    Copy-Item -Path (Join-Path $stagingRoot '*') -Destination $buildRoot -Recurse -Force
    $manifest.package_sha256 = Get-PackageTreeHash $buildRoot
    $verification.package_tree_sha256 = $manifest.package_sha256
    Write-Utf8 (Join-Path $buildRoot 'package-verification.json') (Get-CanonicalJson $verification)
    $manifest.metadata_sha256.'package-verification.json' = Get-Hash (Join-Path $buildRoot 'package-verification.json')
    $manifest.metadata_sha256.'delivery-manifest.json' = Get-CanonicalManifestHash $manifest
    Write-Utf8 (Join-Path $buildRoot 'delivery-manifest.json') (Get-CanonicalJson $manifest)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($buildRoot, $tempRootZip, [IO.Compression.CompressionLevel]::Optimal, $false)
    $rootZipHash = Get-Hash $tempRootZip
    Move-Item -LiteralPath $tempRootZip -Destination $rootZip
    Write-Utf8 $rootZipSidecar "$rootZipHash  $([IO.Path]::GetFileName($rootZip))`n"

    $finalManifest = Read-Json (Join-Path $buildRoot 'delivery-manifest.json')
    $finalPackageTreeHash = Get-PackageTreeHash $buildRoot
    if ($finalManifest.package_sha256 -ne $finalPackageTreeHash) {
        $beforeRecords = @((Get-PackageTreeRecords $stagingRoot) | Sort-Object)
        $afterRecords = @((Get-PackageTreeRecords $buildRoot) | Sort-Object)
        $recordDiff = @(
            Compare-Object -ReferenceObject $beforeRecords -DifferenceObject $afterRecords |
                Select-Object -First 3 |
                ForEach-Object { "$($_.SideIndicator):$($_.InputObject)" }
        )
        $beforeRecordHash = Get-BytesHash ([Text.Encoding]::UTF8.GetBytes([string]::Join("`n", $beforeRecords)))
        $afterRecordHash = Get-BytesHash ([Text.Encoding]::UTF8.GetBytes([string]::Join("`n", $afterRecords)))
        Fail 'WB-DELIVERY-422' "最终 package tree hash 不匹配：manifest=$($finalManifest.package_sha256) actual=$finalPackageTreeHash records_before=$beforeRecordHash records_after=$afterRecordHash count_before=$($beforeRecords.Count) count_after=$($afterRecords.Count) diff=$([string]::Join(';',$recordDiff))"
    }
    $finalArchiveContentHash = Get-ArchiveContentHash $buildRoot
    if ($finalManifest.zip_sha256 -ne $finalArchiveContentHash) { Fail 'WB-DELIVERY-422' "最终规范 ZIP 内容哈希不匹配：manifest=$($finalManifest.zip_sha256) actual=$finalArchiveContentHash" }
    if ((Get-Hash $rootZip) -ne $rootZipHash) { Fail 'WB-DELIVERY-422' '最终根 ZIP 哈希不匹配。' }
    Assert-ZipAndExtract $rootZip $buildRoot $finalManifest.package_sha256
    Assert-NoUnsafeFiles $buildRoot
    Write-Atomic $oldPointerPath (Get-CanonicalJson $candidatePointer)
    Write-Output "BUILD_ID=$buildId"
    Write-Output "PACKAGE=$buildRoot"
    Write-Output "ZIP=$rootZip"
    Write-Output "ZIP_SHA256=$rootZipHash"
}
catch {
    if (Test-Path -LiteralPath $buildRoot -PathType Container) { Remove-Item -LiteralPath $buildRoot -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $rootZip -PathType Leaf) { Remove-Item -LiteralPath $rootZip -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $rootZipSidecar -PathType Leaf) { Remove-Item -LiteralPath $rootZipSidecar -Force -ErrorAction SilentlyContinue }
    throw
}
finally {
    if (Test-Path -LiteralPath $stagingRoot -PathType Container) { Remove-Item -LiteralPath $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $tempRootZip -PathType Leaf) { Remove-Item -LiteralPath $tempRootZip -Force -ErrorAction SilentlyContinue }
}
