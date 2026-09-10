[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,
    [Parameter(Mandatory = $true)]
    [string]$ZipPath,
    [Parameter(Mandatory = $true)]
    [string]$PointerPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$packageRoot = [IO.Path]::GetFullPath($PackageRoot)
$zipPath = [IO.Path]::GetFullPath($ZipPath)
$pointerPath = [IO.Path]::GetFullPath($PointerPath)

function Fail([string]$Code, [string]$Message) { throw "${Code}: $Message" }
function Read-Json([string]$Path) {
    try { Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json -Depth 100 }
    catch { Fail 'WB-DELIVERY-422' "JSON 无法解析：$Path" }
}
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function BytesHash([byte[]]$Bytes) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($algorithm.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}
function Relative([string]$Root, [string]$Path) {
    $base = [IO.Path]::GetFullPath($Root).TrimEnd([char]'\', [char]'/') + [IO.Path]::DirectorySeparatorChar
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) { Fail 'WB-DELIVERY-403' "越过包根：$Path" }
    $full.Substring($base.Length).Replace('\', '/')
}
function Canonical([object]$Value) { $Value | ConvertTo-Json -Depth 100 -Compress }
function ManifestSelfHash([object]$Manifest) {
    $clone = Canonical $Manifest | ConvertFrom-Json -Depth 100
    $clone.metadata_sha256.'delivery-manifest.json' = $null
    BytesHash ([Text.Encoding]::UTF8.GetBytes((Canonical $clone)))
}
function PackageRecords([string]$Root) {
    foreach ($file in @(Get-ChildItem -LiteralPath $Root -Recurse -File -Force)) {
        if ($file.Name -in @('manifest.json','delivery-manifest.json','SHA256SUMS.txt','PACKAGE-MANIFEST.sha256.txt','BUILD-SOURCE-MANIFEST.sha256.txt','BUILD-ID.txt','package-verification.json') -or $file.Extension -in @('.zip','.sha256')) { continue }
        $relative = Relative $Root $file.FullName
        "$relative|$($file.Length)|$(Hash $file.FullName)"
    }
}
function PackageTreeHash([string]$Root) { BytesHash ([Text.Encoding]::UTF8.GetBytes([string]::Join("`n", @((PackageRecords $Root) | Sort-Object)))) }
function ArchiveContentHash([string]$Root) {
    $records = foreach ($file in @(Get-ChildItem -LiteralPath $Root -Recurse -File -Force)) {
        $relative = Relative $Root $file.FullName
        if ($relative -eq 'delivery-manifest.json' -or $relative -eq 'package-verification.json') { continue }
        "$relative|$($file.Length)|$(Hash $file.FullName)"
    }
    BytesHash ([Text.Encoding]::UTF8.GetBytes([string]::Join("`n", @($records | Sort-Object))))
}
function Assert-Safe([string]$Root) {
    foreach ($item in @(Get-ChildItem -LiteralPath $Root -Recurse -Force)) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { Fail 'WB-DELIVERY-422' "发现重解析点：$($item.FullName)" }
    }
}
function Assert-Zip([string]$ArchivePath, [string]$ExpectedRoot) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    $extract = Join-Path ([IO.Path]::GetTempPath()) ('awake-delivery-verify-' + [Guid]::NewGuid().ToString('N'))
    try {
        $seen = @{}
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace('\','/')
            if ($name.EndsWith('/')) { continue }
            if ($name.StartsWith('/') -or $name -match '(^|/)\.\.(/|$)') { Fail 'WB-DELIVERY-422' "ZIP 路径不安全：$name" }
            if ($seen.ContainsKey($name)) { Fail 'WB-DELIVERY-422' "ZIP 条目重复：$name" }
            $seen[$name] = $true
        }
        New-Item -ItemType Directory -Force -Path $extract | Out-Null
        [IO.Compression.ZipFile]::ExtractToDirectory($ArchivePath, $extract)
        Assert-Safe $extract
        $expected = @{}
        foreach ($file in @(Get-ChildItem -LiteralPath $ExpectedRoot -Recurse -File -Force)) { $expected[(Relative $ExpectedRoot $file.FullName)] = "$($file.Length)|$(Hash $file.FullName)" }
        $actual = @(Get-ChildItem -LiteralPath $extract -Recurse -File -Force)
        foreach ($file in $actual) {
            $key = Relative $extract $file.FullName
            if (-not $expected.ContainsKey($key)) { Fail 'WB-DELIVERY-422' "ZIP 多出文件：$key" }
            if ($expected[$key] -ne "$($file.Length)|$(Hash $file.FullName)") { Fail 'WB-DELIVERY-422' "ZIP 文件内容不匹配：$key" }
        }
        if ($actual.Count -ne $expected.Count) { Fail 'WB-DELIVERY-422' 'ZIP 文件集合不完整。' }
        if ((PackageTreeHash $extract) -ne (PackageTreeHash $ExpectedRoot)) { Fail 'WB-DELIVERY-422' 'ZIP 解压 tree hash 不匹配。' }
    }
    finally {
        $archive.Dispose()
        if (Test-Path -LiteralPath $extract -PathType Container) { Remove-Item -LiteralPath $extract -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) { Fail 'WB-DELIVERY-404' '包根不存在。' }
if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) { Fail 'WB-DELIVERY-404' '根 ZIP 不存在。' }
if (-not (Test-Path -LiteralPath $pointerPath -PathType Leaf)) { Fail 'WB-DELIVERY-404' 'rollback pointer 不存在。' }
Assert-Safe $packageRoot
$topDirectories = @(Get-ChildItem -LiteralPath $packageRoot -Directory -Force | Select-Object -ExpandProperty Name | Sort-Object)
$expectedTopDirectories = @('docs','persona-workbench','ui-workstation','worldbook-studio') | Sort-Object
if ([string]::Join('|',$topDirectories) -ne [string]::Join('|',$expectedTopDirectories)) { Fail 'WB-DELIVERY-422' '根包目录集合不匹配。' }
$topFiles = @(Get-ChildItem -LiteralPath $packageRoot -File -Force | Select-Object -ExpandProperty Name | Sort-Object)
$expectedTopFiles = @('BUILD-ID.txt','SHA256SUMS.txt','delivery-manifest.json','package-verification.json') | Sort-Object
if ([string]::Join('|',$topFiles) -ne [string]::Join('|',$expectedTopFiles)) { Fail 'WB-DELIVERY-422' '根包元数据文件集合不匹配。' }
$expectedDocuments = @('README.md','安装说明.md','故障排查.md','恢复与回滚.md','本地Worker配置.md','已知限制.md','证据边界.md')
$actualDocuments = @(Get-ChildItem -LiteralPath (Join-Path $packageRoot 'docs') -File -Force | Select-Object -ExpandProperty Name | Sort-Object)
if ([string]::Join('|',$actualDocuments) -ne [string]::Join('|',($expectedDocuments | Sort-Object))) { Fail 'WB-DELIVERY-422' '客户文档集合不匹配。' }
$manifestPath = Join-Path $packageRoot 'delivery-manifest.json'
$manifest = Read-Json $manifestPath
if ($manifest.schema_version -ne 'awake.customer.delivery-manifest.v1') { Fail 'WB-DELIVERY-422' 'delivery manifest schema 无效。' }
if ($manifest.build_id -ne (Get-Content -Raw -LiteralPath (Join-Path $packageRoot 'BUILD-ID.txt')).Trim()) { Fail 'WB-DELIVERY-422' 'BUILD-ID.txt 不匹配。' }
$toolIds = @($manifest.tools | ForEach-Object { $_.tool_id } | Sort-Object)
if ([string]::Join('|',$toolIds) -ne 'persona_workbench|ui_workstation|worldbook_studio') { Fail 'WB-DELIVERY-422' '工具清单不完整或重复。' }
foreach ($tool in @($manifest.tools)) {
    $entrypoint = Join-Path $packageRoot ($tool.entrypoint.Replace('/','\'))
    if (-not (Test-Path -LiteralPath $entrypoint -PathType Leaf)) { Fail 'WB-DELIVERY-422' "客户入口缺失：$($tool.entrypoint)" }
    $childZip = Join-Path $packageRoot ($tool.zip_path.Replace('/','\'))
    if (-not (Test-Path -LiteralPath $childZip -PathType Leaf)) { Fail 'WB-DELIVERY-422' "子包 ZIP 缺失：$($tool.zip_path)" }
    if ((Hash $childZip) -ne $tool.zip_sha256) { Fail 'WB-DELIVERY-422' "子包 ZIP 哈希不匹配：$($tool.tool_id)" }
}
$packageVerification = Read-Json (Join-Path $packageRoot 'package-verification.json')
if ($packageVerification.schema_version -ne 'awake.customer.package-verification.v1' -or $packageVerification.build_id -ne $manifest.build_id) {
    Fail 'WB-DELIVERY-422' 'package-verification 绑定不匹配。'
}
foreach ($check in @('source_bound','child_packages','root_tree','zip','extracted')) {
    if (-not [bool]$packageVerification.checks.$check) { Fail 'WB-DELIVERY-422' "package-verification 检查未通过：$check" }
}
$fileEntries = @($manifest.files)
$actualFiles = @((Get-ChildItem -LiteralPath $packageRoot -Recurse -File -Force | ForEach-Object {
    $relative = Relative $packageRoot $_.FullName
    if ($relative -eq 'delivery-manifest.json' -or $relative -eq 'SHA256SUMS.txt' -or $relative -eq 'BUILD-ID.txt' -or $relative -eq 'package-verification.json' -or $relative -match '(^|/)[^/]+\.zip$' -or $relative -match '(^|/)[^/]+\.zip\.sha256$') { return }
    $relative
})) | Sort-Object
$manifestFiles = @($fileEntries.path | Sort-Object)
if ([string]::Join('|',$actualFiles) -ne [string]::Join('|',$manifestFiles)) { Fail 'WB-DELIVERY-422' 'manifest files 集合不匹配。' }
foreach ($entry in $fileEntries) {
    $file = Join-Path $packageRoot ($entry.path.Replace('/','\'))
    if ((Hash $file) -ne $entry.sha256 -or (Get-Item -LiteralPath $file).Length -ne [int64]$entry.length) { Fail 'WB-DELIVERY-422' "manifest 文件校验失败：$($entry.path)" }
}
$sumEntries = @{}
foreach ($line in @(Get-Content -LiteralPath (Join-Path $packageRoot 'SHA256SUMS.txt'))) {
    if ($line -notmatch '^([0-9a-fA-F]{64})\s{2}(.+)$') { Fail 'WB-DELIVERY-422' 'SHA256SUMS 格式无效。' }
    $sumEntries[$matches[2].Replace('\','/')] = $matches[1].ToLowerInvariant()
}
if ([string]::Join('|',@($sumEntries.Keys | Sort-Object)) -ne [string]::Join('|',$manifestFiles)) { Fail 'WB-DELIVERY-422' 'SHA256SUMS 文件集合不匹配。' }
foreach ($path in $sumEntries.Keys) { if ((Hash (Join-Path $packageRoot ($path.Replace('/','\')))) -ne $sumEntries[$path]) { Fail 'WB-DELIVERY-422' "SHA256SUMS 校验失败：$path" } }
if ($manifest.package_sha256 -ne (PackageTreeHash $packageRoot)) { Fail 'WB-DELIVERY-422' 'package tree hash 不匹配。' }
if ($manifest.zip_sha256 -ne (ArchiveContentHash $packageRoot)) { Fail 'WB-DELIVERY-422' '规范 ZIP 内容哈希不匹配。' }
if ($manifest.metadata_sha256.'delivery-manifest.json' -ne (ManifestSelfHash $manifest)) { Fail 'WB-DELIVERY-422' 'delivery manifest 自哈希不匹配。' }
foreach ($name in @('SHA256SUMS.txt','BUILD-ID.txt','package-verification.json')) {
    if ($manifest.metadata_sha256.$name -ne (Hash (Join-Path $packageRoot $name))) { Fail 'WB-DELIVERY-422' "元数据哈希不匹配：$name" }
}
$zipSidecar = Get-Content -LiteralPath "$zipPath.sha256" | Select-Object -First 1
$zipHash = Hash $zipPath
if ($zipSidecar -notmatch '^([0-9a-fA-F]{64})\s{2}(.+)$' -or $matches[1].ToLowerInvariant() -ne $zipHash) { Fail 'WB-DELIVERY-422' '根 ZIP sidecar 不匹配。' }
Assert-Zip $zipPath $packageRoot
$pointer = Read-Json $pointerPath
if ($pointer.current_build_id -ne $manifest.build_id -or $pointer.current_root -ne $manifest.build_id) { Fail 'WB-DELIVERY-422' 'rollback pointer 未指向当前包。' }
Write-Output "DELIVERY_VALIDATION_PASS build=$($manifest.build_id) files=$($fileEntries.Count) zip_sha256=$zipHash"
