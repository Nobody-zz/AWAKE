[CmdletBinding()]
param(
    [string]$OutputRoot = (Join-Path $PSScriptRoot 'artifacts'),
    [string]$BuildId = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PSScriptRoot)
$output = [IO.Path]::GetFullPath($OutputRoot)
$utf8NoBom = [Text.UTF8Encoding]::new($false)
$gameRoot = [IO.Path]::GetFullPath('D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord')
$gamePrefix = $gameRoot.TrimEnd([char]'\', [char]'/') + [IO.Path]::DirectorySeparatorChar
if ($output.StartsWith($gamePrefix, [StringComparison]::OrdinalIgnoreCase) -or
    [string]::Equals($output, $gameRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'WB-UI-PACKAGE-GAME-403: UI Workstation 包不得写入游戏目录。'
}
$runtimeFiles = @(
    'UiWorkstation.Adapter.ps1',
    'start-ui-workstation.ps1',
    'stop-ui-workstation.ps1'
)

function Get-FileSha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-RelativePath([string]$Base, [string]$Path) {
    return $Path.Substring($Base.Length).TrimStart([char]'\', [char]'/').Replace('\', '/')
}

function Get-PackageTreeHash([string]$PackagePath) {
    $packageFull = [IO.Path]::GetFullPath($PackagePath).TrimEnd([char]'\', [char]'/')
    $entries = foreach ($file in @(Get-ChildItem -LiteralPath $PackagePath -File | Sort-Object Name)) {
        if ($file.Name -in @('manifest.json', 'SHA256SUMS.txt')) {
            continue
        }
        $relative = Get-RelativePath $packageFull $file.FullName
        "$relative|$($file.Length)|$(Get-FileSha256 $file.FullName)"
    }
    $payload = [string]::Join("`n", $entries)
    $bytes = $utf8NoBom.GetBytes($payload)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($algorithm.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Assert-UnderRoot([string]$Path, [string]$Base) {
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = ([IO.Path]::GetFullPath($Base)).TrimEnd([char]'\', [char]'/') + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "WB-UI-PACKAGE-403: 输出路径必须位于 UI Workstation artifacts 目录：$full"
    }
}

if ([string]::IsNullOrWhiteSpace($BuildId)) {
    $BuildId = 'ui-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
}
if ($BuildId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{2,63}$') {
    throw 'WB-UI-PACKAGE-400: BuildId 格式无效。'
}

New-Item -ItemType Directory -Force -Path $output | Out-Null
$packageName = "ui-workstation-$BuildId"
$package = Join-Path $output $packageName
$zip = Join-Path $output "$packageName.zip"
Assert-UnderRoot $package $output
Assert-UnderRoot $zip $output
if ((Test-Path -LiteralPath $package -PathType Container) -or
    (Test-Path -LiteralPath $zip -PathType Leaf) -or
    (Test-Path -LiteralPath ($zip + '.sha256') -PathType Leaf)) {
    throw "WB-UI-PACKAGE-409: BuildId 已存在，拒绝覆盖：$BuildId"
}

$stage = Join-Path ([IO.Path]::GetTempPath()) ('awake-ui-package-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $stage | Out-Null
try {
    foreach ($fileName in $runtimeFiles) {
        $source = Join-Path $root $fileName
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "WB-UI-PACKAGE-404: 缺少运行时文件：$fileName"
        }
        Copy-Item -LiteralPath $source -Destination (Join-Path $stage $fileName)
    }

    $buildJson = [ordered]@{
        build_id = $BuildId
        workstation_id = 'ui_workstation'
        protocol_version = 'awake.workstation.v1'
    } | ConvertTo-Json -Compress
    [IO.File]::WriteAllText((Join-Path $stage 'build.json'), $buildJson, $utf8NoBom)

    $fileEntries = foreach ($fileName in @($runtimeFiles + 'build.json' | Sort-Object)) {
        $path = Join-Path $stage $fileName
        [ordered]@{
            path = $fileName
            length = (Get-Item -LiteralPath $path).Length
            sha256 = Get-FileSha256 $path
        }
    }
    $manifest = [ordered]@{
        schema_version = 'awake.ui-workstation.package.v1'
        package_id = 'awake.ui_workstation'
        build_id = $BuildId
        workstation_id = 'ui_workstation'
        protocol_version = 'awake.workstation.v1'
        entrypoint = 'start-ui-workstation.ps1'
        stop_entrypoint = 'stop-ui-workstation.ps1'
        files = @($fileEntries)
        package_tree_sha256 = Get-PackageTreeHash $stage
        game_directory_access = 'forbidden'
    }
    [IO.File]::WriteAllText(
        (Join-Path $stage 'manifest.json'),
        ($manifest | ConvertTo-Json -Depth 10),
        $utf8NoBom
    )

    $sumLines = foreach ($file in @(Get-ChildItem -LiteralPath $stage -File | Sort-Object Name)) {
        "$((Get-FileSha256 $file.FullName))  $($file.Name)"
    }
    [IO.File]::WriteAllText((Join-Path $stage 'SHA256SUMS.txt'), ([string]::Join("`n", $sumLines) + "`n"), $utf8NoBom)

    New-Item -ItemType Directory -Force -Path $package | Out-Null
    Copy-Item -Path (Join-Path $stage '*') -Destination $package -Force

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory(
        $package,
        $zip,
        [IO.Compression.CompressionLevel]::Optimal,
        $false
    )
    $zipHash = Get-FileSha256 $zip
    [IO.File]::WriteAllText(($zip + '.sha256'), "$zipHash  $(Split-Path -Leaf $zip)`n", [Text.UTF8Encoding]::new($false))

    Write-Output "PACKAGE: $package"
    Write-Output "ZIP: $zip"
    Write-Output "ZIP_SHA256: $zipHash"
}
finally {
    if (Test-Path -LiteralPath $stage -PathType Container) {
        Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
    }
}
