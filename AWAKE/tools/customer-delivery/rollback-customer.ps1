[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BuildId,
    [string]$OutputRoot = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'artifacts\customer-delivery')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$outputRootFull = [IO.Path]::GetFullPath($OutputRoot)
$utf8NoBom = [Text.UTF8Encoding]::new($false)

function Fail([string]$Code, [string]$Message) { throw "${Code}: $Message" }
function Get-Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Read-Json([string]$Path) {
    try { Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json -Depth 100 }
    catch { Fail 'WB-DELIVERY-422' "JSON 无法解析：$Path" }
}
function Write-Atomic([string]$Path, [string]$Content) {
    $temp = "$Path.$([Guid]::NewGuid().ToString('N')).tmp"
    $backup = "$Path.$([Guid]::NewGuid().ToString('N')).bak"
    try {
        [IO.File]::WriteAllText($temp, $Content, $utf8NoBom)
        if (Test-Path -LiteralPath $Path -PathType Leaf) {
            [IO.File]::Replace($temp, $Path, $backup)
        }
        else { [IO.File]::Move($temp, $Path) }
    }
    finally {
        if (Test-Path -LiteralPath $temp -PathType Leaf) { Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue }
        if (Test-Path -LiteralPath $backup -PathType Leaf) { Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue }
    }
}
function Get-TreeHash([string]$Root) {
    $base = ([IO.Path]::GetFullPath($Root)).TrimEnd([char]'\', [char]'/') + [IO.Path]::DirectorySeparatorChar
    $records = foreach ($file in @(Get-ChildItem -LiteralPath $Root -Recurse -File -Force)) {
        if ($file.Name -in @('manifest.json','delivery-manifest.json','SHA256SUMS.txt','PACKAGE-MANIFEST.sha256.txt','BUILD-SOURCE-MANIFEST.sha256.txt','BUILD-ID.txt','package-verification.json') -or $file.Extension -in @('.zip','.sha256')) { continue }
        $relative = $file.FullName.Substring($base.Length).Replace('\','/')
        "$relative|$($file.Length)|$(Get-Hash $file.FullName)"
    }
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes([string]::Join("`n", @($records | Sort-Object)))))).Replace('-','').ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}

if (-not (Test-Path -LiteralPath $outputRootFull -PathType Container)) { Fail 'WB-DELIVERY-404' '交付根目录不存在。' }
$pointerPath = Join-Path $outputRootFull 'rollback-pointer.json'
if (-not (Test-Path -LiteralPath $pointerPath -PathType Leaf)) { Fail 'WB-DELIVERY-404' 'rollback pointer 不存在。' }
$pointer = Read-Json $pointerPath
if ($pointer.schema_version -ne 'awake.customer.rollback-pointer.v1') { Fail 'WB-DELIVERY-422' 'rollback pointer schema 无效。' }
$currentRoot = Join-Path $outputRootFull $pointer.current_root
if (-not (Test-Path -LiteralPath $currentRoot -PathType Container)) { Fail 'WB-DELIVERY-422' '当前指针目标不存在。' }
$targetRoot = Join-Path $outputRootFull $BuildId
$targetZip = Join-Path $outputRootFull "$BuildId.zip"
if (-not (Test-Path -LiteralPath $targetRoot -PathType Container) -or -not (Test-Path -LiteralPath $targetZip -PathType Leaf)) { Fail 'WB-DELIVERY-404' "目标构建不存在：$BuildId" }
$targetManifest = Read-Json (Join-Path $targetRoot 'delivery-manifest.json')
if ($targetManifest.build_id -ne $BuildId) { Fail 'WB-DELIVERY-422' '目标 manifest BuildId 不匹配。' }
if ($targetManifest.package_sha256 -ne (Get-TreeHash $targetRoot)) { Fail 'WB-DELIVERY-422' '目标 package tree 校验失败。' }
$targetZipHash = Get-Hash $targetZip
$sidecar = Get-Content -LiteralPath "$targetZip.sha256" | Select-Object -First 1
if ($sidecar -notmatch '^([0-9A-Fa-f]{64})\s{2}(.+)$' -or $matches[1].ToLowerInvariant() -ne $targetZipHash) { Fail 'WB-DELIVERY-422' '目标 ZIP sidecar 校验失败。' }
$newPointer = [ordered]@{
    schema_version = 'awake.customer.rollback-pointer.v1'
    current_build_id = $BuildId
    previous_build_id = $pointer.current_build_id
    current_root = $BuildId
    previous_root = $pointer.current_root
    updated_at_utc = [DateTime]::UtcNow.ToString('o')
}
Write-Atomic $pointerPath ($newPointer | ConvertTo-Json -Depth 20 -Compress)
$report = [ordered]@{
    schema_version = 'awake.customer.rollback-verification.v1'
    rolled_back_to = $BuildId
    previous_current = $pointer.current_build_id
    target_package_sha256 = $targetManifest.package_sha256
    target_zip_sha256 = $targetZipHash
    pointer_sha256 = Get-Hash $pointerPath
    verified_at_utc = [DateTime]::UtcNow.ToString('o')
}
$reportPath = Join-Path $outputRootFull "rollback-verification-$BuildId.json"
[IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 20 -Compress), $utf8NoBom)
Write-Output "ROLLBACK_BUILD_ID=$BuildId"
Write-Output "ROLLBACK_POINTER=$pointerPath"
Write-Output "ROLLBACK_REPORT=$reportPath"
