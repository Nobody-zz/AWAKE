[CmdletBinding()]
param(
    [string]$ProjectRoot = '',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputRoot = '',
    [switch]$ValidateOnly,
    [switch]$AllowMissing
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { $ProjectRoot = Split-Path -Parent $scriptRoot }
$runtimeIdentifier = 'win-x64'
$manifestSchema = 'awake.embedded-runtime.v1'

function Get-FullPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'Path must not be empty.' }
    return [IO.Path]::GetFullPath($Path).TrimEnd('\')
}

function Get-RelativePath([string]$Root, [string]$Path) {
    $rootFull = Get-FullPath $Root
    $pathFull = Get-FullPath $Path
    if ($pathFull.Equals($rootFull, [StringComparison]::OrdinalIgnoreCase)) { return '' }
    $prefix = $rootFull + '\'
    if (-not $pathFull.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Path escaped root: $pathFull" }
    return $pathFull.Substring($prefix.Length)
}

function Assert-NoReparseComponents([string]$Path) {
    $full = Get-FullPath $Path
    $qualifier = Split-Path -Qualifier $full
    $tail = $full.Substring($qualifier.Length).TrimStart('\')
    $current = $qualifier + '\'
    foreach ($part in $tail.Split('\')) {
        if ([string]::IsNullOrWhiteSpace($part)) { continue }
        $current = Join-Path $current $part
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse point is not allowed: $current" }
        }
    }
}

function Assert-ExistingFile([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Missing ${Label}: $Path" }
    $item = Get-Item -LiteralPath $Path -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse file is not allowed: $Path" }
}

function Get-Sha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Test-IsForbiddenRelativePath([string]$Relative) {
    $normalized = $Relative.Replace('\', '/')
    $extension = [IO.Path]::GetExtension($Relative).ToLowerInvariant()
    $forbiddenExtensions = @(
        '.pdb', '.cs', '.csproj', '.sln', '.props', '.targets', '.user',
        '.snk', '.key', '.pem', '.pfx', '.p12', '.db', '.sqlite', '.sqlite3',
        '.log', '.dmp', '.bak', '.tmp'
    )
    if ($forbiddenExtensions -contains $extension) { return $true }

    $forbiddenDirectories = @('source', 'src', 'secrets', 'secret', 'keys', 'key', 'credentials', 'credential', 'logs', 'log', 'database', 'databases')
    foreach ($segment in $normalized.Split('/')) {
        if ($forbiddenDirectories -contains $segment.ToLowerInvariant()) { return $true }
    }

    $fileName = [IO.Path]::GetFileName($Relative).ToLowerInvariant()
    # `token` MUST carry word boundaries: without them Microsoft.ML.Tokenizers.dll (the tokenizer,
    # a real dependency of MarcusAwakeStorage) matches and gets silently dropped by
    # Copy-PublishPayload. Found 2026-09-30 -- see docs/AUDIT-MOD-SURFACE-20260930.md P0-1.
    if ($fileName -match 'secret|credential|privatekey|password|\btoken\b') { return $true }
    return $false
}

function Test-IsForeignRuntimePath([string]$Relative) {
    $segments = $Relative.Replace('/', '\').Split('\')
    if ($segments.Count -ge 2 -and $segments[0].Equals('runtimes', [StringComparison]::OrdinalIgnoreCase)) {
        return -not $segments[1].Equals($runtimeIdentifier, [StringComparison]::OrdinalIgnoreCase)
    }
    return $false
}

function Assert-SafeOutputRoot([string]$Path) {
    $full = Get-FullPath $Path
    if ([IO.Path]::GetFileName($full) -ne 'Runtime') { throw "Runtime output must end in Runtime: $full" }
    Assert-NoReparseComponents $full
    $parent = Split-Path -Parent $full
    if (Test-Path -LiteralPath $parent) {
        if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw "Runtime output parent is not a directory: $parent" }
        Assert-NoReparseComponents $parent
    }
    return $full
}

function Get-PayloadFiles([string]$Root) {
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) { throw "Runtime package directory is missing: $Root" }
    $files = @(Get-ChildItem -LiteralPath $Root -Recurse -File -Force)
    return @($files | Where-Object {
        $relative = Get-RelativePath $Root $_.FullName
        $relative -ne 'manifest.json' -and $relative -ne 'SHA256SUMS.txt'
    } | Sort-Object FullName)
}

function Assert-PayloadFiles([string]$Root, $Files) {
    foreach ($file in @($Files)) {
        $relative = Get-RelativePath $Root $file.FullName
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse package file is not allowed: $relative" }
        if (Test-IsForbiddenRelativePath $relative) { throw "Forbidden runtime package file: $relative" }
        if (Test-IsForeignRuntimePath $relative) { throw "Non-$runtimeIdentifier native runtime path is not allowed: $relative" }
    }
}

function Assert-RequiredPayload([string]$Root, $Files) {
    $required = @(
        'MarcusAwakeRuntimeService.exe',
        'MarcusAwakeRuntimeService.dll',
        'MarcusAwakeRuntimeService.deps.json',
        'MarcusAwakeRuntimeService.runtimeconfig.json',
        'MarcusAwakeProvider.dll',
        'MarcusAwakeStorage.dll',
        'MarcusAwakeTransport.dll',
        'MarcusAwakeFramework.dll',
        'Microsoft.Data.Sqlite.dll',
        'SQLitePCLRaw.batteries_v2.dll',
        'SQLitePCLRaw.core.dll',
        'SQLitePCLRaw.provider.e_sqlite3.dll'
    )
    foreach ($relative in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $Root $relative) -PathType Leaf)) { throw "Required runtime package file is missing: $relative" }
    }
    $native = @($Files | Where-Object { $_.Name.Equals('e_sqlite3.dll', [StringComparison]::OrdinalIgnoreCase) })
    if ($native.Count -eq 0) { throw 'SQLite native dependency e_sqlite3.dll is missing.' }
}

function Get-ManifestEntries([string]$Root) {
    $files = @(Get-PayloadFiles $Root)
    $entries = foreach ($file in $files) {
        $relative = (Get-RelativePath $Root $file.FullName).Replace('\', '/')
        [ordered]@{
            path = $relative
            sha256 = Get-Sha256 $file.FullName
            length = [int64]$file.Length
        }
    }
    return @($entries | Sort-Object -Property path)
}

function Write-Utf8NoBom([string]$Path, [string]$Content) {
    [IO.File]::WriteAllText($Path, $Content, (New-Object Text.UTF8Encoding($false)))
}

function Read-Manifest([string]$Root) {
    $path = Join-Path $Root 'manifest.json'
    Assert-ExistingFile $path 'runtime manifest'
    try {
        return (Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json)
    } catch {
        throw "Runtime manifest is not valid JSON: $path. $($_.Exception.Message)"
    }
}

function Assert-Manifest([string]$Root, $Files) {
    $manifest = Read-Manifest $Root
    if ([string]$manifest.schemaVersion -ne $manifestSchema) { throw "Unsupported runtime manifest schema: $($manifest.schemaVersion)" }
    if ([string]$manifest.product -ne 'AWAKE.RuntimeService') { throw "Unexpected runtime manifest product: $($manifest.product)" }
    if ([string]$manifest.rid -ne $runtimeIdentifier) { throw "Runtime manifest RID must be $runtimeIdentifier" }
    if ($manifest.selfContained -ne $true) { throw 'Runtime manifest must declare selfContained=true.' }
    if ([string]$manifest.entryPoint -ne 'MarcusAwakeRuntimeService.exe') { throw 'Runtime manifest entryPoint is invalid.' }

    $requiredComponents = @('Runtime', 'Provider', 'Storage', 'Transport', 'Framework', 'SQLite')
    $components = @($manifest.components | ForEach-Object { [string]$_ })
    foreach ($component in $requiredComponents) {
        if ($components -notcontains $component) { throw "Runtime manifest component is missing: $component" }
    }

    $entries = @($manifest.files)
    if ($entries.Count -eq 0) { throw 'Runtime manifest has no file entries.' }
    $expected = @{}
    foreach ($entry in $entries) {
        if ($null -eq $entry) { throw 'Runtime manifest contains a null file entry.' }
        $rawPath = [string]$entry.path
        if ([string]::IsNullOrWhiteSpace($rawPath)) { throw 'Runtime manifest contains an empty file path.' }
        $relative = $rawPath.Replace('/', '\')
        if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|\\)\.\.(\\|$)' -or $relative -match '[<>:"|?*]') { throw "Unsafe runtime manifest path: $rawPath" }
        $normalized = $relative.Replace('\', '/')
        if ($normalized -eq 'manifest.json' -or $normalized -eq 'SHA256SUMS.txt') { throw "Runtime manifest cannot list metadata file: $rawPath" }
        $key = $normalized.ToLowerInvariant()
        if ($expected.ContainsKey($key)) { throw "Duplicate runtime manifest path: $normalized" }
        if ([string]$entry.sha256 -notmatch '^[0-9A-Fa-f]{64}$') { throw "Invalid runtime manifest SHA-256: $normalized" }
        if ($null -eq $entry.length) { throw "Missing runtime manifest length: $normalized" }
        try { $length = [int64]$entry.length } catch { throw "Invalid runtime manifest length: $normalized" }
        $expected[$key] = [ordered]@{ path = $normalized; sha256 = ([string]$entry.sha256).ToLowerInvariant(); length = $length }
    }

    $actual = @{}
    foreach ($file in @($Files)) {
        $relative = (Get-RelativePath $Root $file.FullName).Replace('\', '/')
        $key = $relative.ToLowerInvariant()
        if ($actual.ContainsKey($key)) { throw "Duplicate runtime package path: $relative" }
        $actual[$key] = $file
    }
    if ($actual.Count -ne $expected.Count) { throw "Runtime manifest file count mismatch: manifest=$($expected.Count) actual=$($actual.Count)" }
    foreach ($key in $expected.Keys) {
        if (-not $actual.ContainsKey($key)) { throw "Runtime manifest file is missing from package: $($expected[$key].path)" }
        $file = $actual[$key]
        $entry = $expected[$key]
        $hash = Get-Sha256 $file.FullName
        if ($hash -ne $entry.sha256) { throw "Runtime package SHA-256 mismatch: $($entry.path)" }
        if ([int64]$file.Length -ne [int64]$entry.length) { throw "Runtime package length mismatch: $($entry.path)" }
    }

    $sumPath = Join-Path $Root 'SHA256SUMS.txt'
    Assert-ExistingFile $sumPath 'runtime SHA256SUMS'
    $sumEntries = @{}
    foreach ($line in @(Get-Content -LiteralPath $sumPath -Encoding UTF8)) {
        $text = [string]$line
        if ([string]::IsNullOrWhiteSpace($text) -or $text -notmatch '^(?<hash>[0-9A-Fa-f]{64})  (?<path>.+)$') { throw "Invalid runtime SHA256SUMS line: $text" }
        $normalized = ([string]$Matches['path']).Replace('\', '/')
        $key = $normalized.ToLowerInvariant()
        if ($sumEntries.ContainsKey($key)) { throw "Duplicate runtime SHA256SUMS path: $normalized" }
        if (-not $expected.ContainsKey($key)) { throw "Runtime SHA256SUMS lists an unknown file: $normalized" }
        $sumEntries[$key] = ([string]$Matches['hash']).ToLowerInvariant()
    }
    if ($sumEntries.Count -ne $expected.Count) { throw "Runtime SHA256SUMS file count mismatch: sums=$($sumEntries.Count) manifest=$($expected.Count)" }
    foreach ($key in $expected.Keys) {
        if ($sumEntries[$key] -ne $expected[$key].sha256) { throw "Runtime SHA256SUMS hash mismatch: $($expected[$key].path)" }
    }
}

function Test-EmbeddedRuntimePackage([string]$Root) {
    $full = Get-FullPath $Root
    if (-not (Test-Path -LiteralPath $full -PathType Container)) { throw "Runtime package directory is missing: $full" }
    Assert-NoReparseComponents $full
    $files = @(Get-PayloadFiles $full)
    Assert-PayloadFiles $full $files
    Assert-RequiredPayload $full $files
    Assert-Manifest $full $files
    return [ordered]@{
        root = $full
        file_count = $files.Count
        manifest_sha256 = Get-Sha256 (Join-Path $full 'manifest.json')
        sums_sha256 = Get-Sha256 (Join-Path $full 'SHA256SUMS.txt')
    }
}

function Invoke-Dotnet([string]$DotnetPath, [string[]]$Arguments) {
    & $DotnetPath @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet command failed with exit code $($LASTEXITCODE): $([string]::Join(' ', $Arguments))" }
}

function Copy-PublishPayload([string]$SourceRoot, [string]$DestinationRoot) {
    foreach ($file in @(Get-ChildItem -LiteralPath $SourceRoot -Recurse -File -Force)) {
        $relative = Get-RelativePath $SourceRoot $file.FullName
        if (Test-IsForbiddenRelativePath $relative) { continue }
        if (Test-IsForeignRuntimePath $relative) { continue }
        $target = Join-Path $DestinationRoot $relative
        $parent = Split-Path -Parent $target
        if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    }
}

# The local semantic model is a HAND-PLACED asset: `dotnet publish` never emits it, so before
# 2026-09-30 it was absent from the packaging pipeline entirely (the copy on the game side was
# dropped in by hand on 09-17). The standing decision is that the model ships inside the module
# package rather than requiring players to install it themselves -- see the 2026-09-16 DECISION
# doc under docs/. The 94.9 MB of weights do not belong in git, so the source lives under
# artifacts/models/, which .gitignore:129 already ignores.
# Per-file sha256: tools/semantic-model-provenance.json (verifier: tools/verify_semantic_model.py).
# NOTE: keep this file ASCII-only -- it has no BOM, so non-ASCII bytes break PS 5.1 parsing.
function Copy-ModelPayload([string]$DestinationRoot) {
    $sourceRoot = Join-Path $ProjectRoot 'artifacts\models'
    if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) {
        Write-Warning ("Local model source is missing: {0} -- the package will NOT contain models/. Download bge-small-zh-v1.5 into that directory (see tools/semantic-model-provenance.json)." -f $sourceRoot)
        return
    }
    foreach ($file in @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Force)) {
        $relative = Get-RelativePath $sourceRoot $file.FullName
        if (Test-IsForbiddenRelativePath $relative) { continue }
        if (Test-IsForeignRuntimePath $relative) { continue }
        $target = Join-Path (Join-Path $DestinationRoot 'models') $relative
        $parent = Split-Path -Parent $target
        if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    }
}

$projectRoot = Get-FullPath $ProjectRoot
if (-not (Test-Path -LiteralPath $projectRoot -PathType Container)) { throw "ProjectRoot is missing: $projectRoot" }
Assert-NoReparseComponents $projectRoot

$defaultOutputRoot = Get-FullPath (Join-Path $projectRoot 'dist\Modules\AWAKE\bin\Win64_Shipping_Client\Runtime')
$requestedOutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $defaultOutputRoot } else { $OutputRoot }
$outputRoot = Assert-SafeOutputRoot $requestedOutputRoot

if ($AllowMissing -and -not $ValidateOnly) { throw '-AllowMissing requires -ValidateOnly.' }
if ($ValidateOnly) {
    if (-not (Test-Path -LiteralPath $outputRoot)) {
        if ($AllowMissing) {
            Write-Output "RUNTIME_PACKAGE_NOT_PRESENT path=$outputRoot"
            return
        }
        throw "Runtime package directory is missing: $outputRoot"
    }
    $state = Test-EmbeddedRuntimePackage $outputRoot
    Write-Output "RUNTIME_PACKAGE_OK path=$($state.root) files=$($state.file_count) manifest_sha256=$($state.manifest_sha256) sums_sha256=$($state.sums_sha256)"
    return
}

$runtimeProject = Join-Path $projectRoot 'framework\MarcusAwakeRuntimeService\MarcusAwakeRuntimeService.csproj'
$providerProject = Join-Path $projectRoot 'framework\MarcusAwakeProvider\MarcusAwakeProvider.csproj'
$storageProject = Join-Path $projectRoot 'framework\MarcusAwakeStorage\MarcusAwakeStorage.csproj'
$transportProject = Join-Path $projectRoot 'framework\MarcusAwakeTransport\MarcusAwakeTransport.csproj'
$frameworkProject = Join-Path $projectRoot 'framework\MarcusAwakeFramework\MarcusAwakeFramework.csproj'
$providerDll = Join-Path $projectRoot "framework\MarcusAwakeProvider\_build_out\$Configuration\MarcusAwakeProvider.dll"
$dotnet = (Get-Command dotnet -ErrorAction Stop).Path

foreach ($project in @($runtimeProject, $providerProject, $storageProject, $transportProject, $frameworkProject)) {
    Assert-ExistingFile $project 'Runtime project'
}

$stageRoot = Join-Path ([IO.Path]::GetTempPath()) ('awake-embedded-runtime-' + [Guid]::NewGuid().ToString('N'))
$publishRoot = Join-Path $stageRoot 'publish'
$packageRoot = Join-Path $stageRoot 'package'
try {
    New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null

    Push-Location $projectRoot
    try {
        foreach ($project in @($transportProject, $frameworkProject, $providerProject)) {
            Invoke-Dotnet $dotnet @('restore', $project, '--ignore-failed-sources')
        }
        foreach ($project in @($storageProject, $runtimeProject)) {
            Invoke-Dotnet $dotnet @('restore', $project, '--runtime', $runtimeIdentifier, '--ignore-failed-sources')
        }
        foreach ($project in @($transportProject, $frameworkProject, $storageProject, $providerProject)) {
            Invoke-Dotnet $dotnet @('build', $project, '--configuration', $Configuration, '--no-restore', '--nologo')
        }
        Invoke-Dotnet $dotnet @(
            'publish', $runtimeProject,
            '--configuration', $Configuration,
            '--runtime', $runtimeIdentifier,
            '--self-contained', 'true',
            '--no-restore',
            '--output', $publishRoot,
            '-p:PublishSingleFile=false',
            '-p:PublishTrimmed=false',
            '-p:DebugType=None',
            '-p:DebugSymbols=false',
            '-p:IncludeNativeLibrariesForSelfExtract=false',
            '-p:UseAppHost=true'
        )
    }
    finally {
        Pop-Location
    }

    Copy-PublishPayload $publishRoot $packageRoot
    Copy-ModelPayload $packageRoot
    Assert-ExistingFile $providerDll 'Provider build output'
    Copy-Item -LiteralPath $providerDll -Destination (Join-Path $packageRoot 'MarcusAwakeProvider.dll') -Force

    $payloadFiles = @(Get-PayloadFiles $packageRoot)
    Assert-PayloadFiles $packageRoot $payloadFiles
    Assert-RequiredPayload $packageRoot $payloadFiles
    $entries = @(Get-ManifestEntries $packageRoot)
    $manifest = [ordered]@{
        schemaVersion = $manifestSchema
        product = 'AWAKE.RuntimeService'
        rid = $runtimeIdentifier
        selfContained = $true
        configuration = $Configuration
        entryPoint = 'MarcusAwakeRuntimeService.exe'
        components = @('Runtime', 'Provider', 'Storage', 'Transport', 'Framework', 'SQLite')
        files = $entries
    }
    $manifestJson = $manifest | ConvertTo-Json -Depth 10
    Write-Utf8NoBom (Join-Path $packageRoot 'manifest.json') ($manifestJson + [Environment]::NewLine)

    $sumLines = foreach ($entry in $entries) { "$($entry.sha256)  $($entry.path)" }
    Write-Utf8NoBom (Join-Path $packageRoot 'SHA256SUMS.txt') (($sumLines -join [Environment]::NewLine) + [Environment]::NewLine)
    [void](Test-EmbeddedRuntimePackage $packageRoot)

    $outputParent = Split-Path -Parent $outputRoot
    if (-not (Test-Path -LiteralPath $outputParent)) { New-Item -ItemType Directory -Path $outputParent -Force | Out-Null }
    Assert-NoReparseComponents $outputParent
    if (Test-Path -LiteralPath $outputRoot) { Remove-Item -LiteralPath $outputRoot -Recurse -Force }
    Move-Item -LiteralPath $packageRoot -Destination $outputRoot -Force
    $packageRoot = $null

    $finalState = Test-EmbeddedRuntimePackage $outputRoot
    Write-Output "RUNTIME_PACKAGE_OK path=$($finalState.root) files=$($finalState.file_count) manifest_sha256=$($finalState.manifest_sha256) sums_sha256=$($finalState.sums_sha256)"
}
finally {
    if ($packageRoot -and (Test-Path -LiteralPath $packageRoot)) { Remove-Item -LiteralPath $packageRoot -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $stageRoot) { Remove-Item -LiteralPath $stageRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
