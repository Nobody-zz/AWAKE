[CmdletBinding()]
param(
    [string]$ProjectRoot = "",
    [string]$DistModule,
    [string]$GameModule = "D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE",
    [string]$ReleaseStagingRoot = "",
    [string]$BuildDllPath,
    [string]$ReportPath,
    [switch]$SkipGame,
    [switch]$ConfirmGameSync,
    [switch]$WhatIf,
    [ValidateRange(0, 100000)]
    [int]$TestFailAfterCopies = 0
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { $ProjectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path) }
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmssfff'
if ([string]::IsNullOrWhiteSpace($DistModule)) { $DistModule = Join-Path $ProjectRoot 'dist\Modules\AWAKE' }
if ([string]::IsNullOrWhiteSpace($BuildDllPath)) { $BuildDllPath = Join-Path $ProjectRoot '_build_out\1.3.15\Release\Awake.dll' }
if ([string]::IsNullOrWhiteSpace($ReportPath)) { $ReportPath = Join-Path $ProjectRoot ("docs\sync-reports\sync-" + $timestamp + '.json') }
$embeddedRuntimeScript = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'package_embedded_runtime.ps1'

$managedRootFiles = @('SubModule.xml', 'README_CN.md', 'README_EN.txt', 'BUILD_VERIFICATION.txt')
$managedGuiFiles = @(
    'GUI\Prefabs\AwakeMessenger.xml',
    'GUI\Prefabs\DeveloperCheck.xml',
    'GUI\Prefabs\NpcDialogue.xml',
    'GUI\Prefabs\SceneDialogueStatus.xml',
    'GUI\Prefabs\WeeklyReportBrowser.xml',
    'GUI\Prefabs\WorldEventInbox.xml'
)
$managedLanguageFiles = @(
    'ModuleData\Languages\awake_strings.xml',
    'ModuleData\Languages\language_data.xml',
    'ModuleData\Languages\CNs\awake_strings-zh-HANS.xml',
    'ModuleData\Languages\CNs\language_data.xml'
)
$managedWorldbookFiles = @(
    'ModuleData\Worldbook\manifest.json',
    'ModuleData\Worldbook\persona_definitions\tag_registry.json'
)
$managedWorldbookDirectories = @(
    'ModuleData\Worldbook\rules',
    'ModuleData\Worldbook\personality_background',
    'ModuleData\Worldbook\unnamed_persona',
    'ModuleData\Worldbook\voice_mapping',
    'ModuleData\Worldbook\event_data',
    'ModuleData\Worldbook\debt',
    'ModuleData\Worldbook\dialogue_history',
    'ModuleData\Worldbook\compressed_memory',
    'ModuleData\Worldbook\persona_definitions\definitions'
)
$obsoletePersonaFiles = @(
    'ModuleData\Worldbook\persona_definitions\persona_definitions\tag_registry.json',
    'ModuleData\Worldbook\persona_definitions\persona_definitions\definitions\hero_default.json'
)
$preservedRoots = @('Config.json', 'Logs', 'PlayerExports', 'Runtime', 'Saves', 'Cache')
$embeddedRuntimeRelativeRoot = 'bin\Win64_Shipping_Client\Runtime'

function Get-FullPath([string]$Path) {
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

function Test-PathUnderRoot([string]$Root, [string]$Path) {
    try { [void](Get-RelativePath $Root $Path); return $true } catch { return $false }
}

function Assert-NoReparseComponents([string]$Path) {
    $full = Get-FullPath $Path
    $qualifier = Split-Path -Qualifier $full
    $tail = $full.Substring($qualifier.Length).TrimStart('\')
    $current = $qualifier + '\'
    if ($tail.Length -eq 0) { $tail = '' }
    foreach ($part in $tail.Split('\')) {
        if ([string]::IsNullOrWhiteSpace($part)) { continue }
        $current = Join-Path $current $part
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse point is not allowed: $current" }
        }
    }
}

function Assert-SafeModuleRoot([string]$Path, [string]$Label) {
    if (-not [IO.Path]::IsPathRooted($Path)) { throw "$Label must be absolute." }
    $full = Get-FullPath $Path
    if ([IO.Path]::GetFileName($full) -ne 'AWAKE') { throw "$Label must end in AWAKE: $full" }
    Assert-NoReparseComponents $full
    return $full
}

function Assert-ExistingFile([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Missing ${Label}: $Path" }
    $item = Get-Item -LiteralPath $Path -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse file is not allowed: $Path" }
}

function Assert-TargetParent([string]$Root, [string]$Relative) {
    $target = Join-Path $Root $Relative
    $parent = Split-Path -Parent $target
    if (-not (Test-Path -LiteralPath $parent)) { return }
    $parentItem = Get-Item -LiteralPath $parent -Force
    if (-not $parentItem.PSIsContainer) { throw "Target parent is a file: $parent" }
    Assert-NoReparseComponents $parent
}

function Get-Hash([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-LatestSourceWriteTimeUtc([string[]]$Paths) {
    $files = New-Object 'System.Collections.Generic.List[object]'
    foreach ($path in $Paths) {
        if (-not (Test-Path -LiteralPath $path)) { continue }
        $item = Get-Item -LiteralPath $path -Force
        if ($item.PSIsContainer) {
            foreach ($file in Get-ChildItem -LiteralPath $path -Recurse -File -Force | Where-Object { $_.Extension.ToLowerInvariant() -in @('.cs', '.csproj', '.props', '.targets') }) {
                $files.Add($file)
            }
        } else {
            $files.Add($item)
        }
    }
    if ($files.Count -eq 0) { return $null }
    return ($files | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).LastWriteTimeUtc
}

function Assert-FreshBuildArtifact([string]$Label, [string]$ArtifactPath, [string[]]$SourcePaths) {
    $artifact = Get-Item -LiteralPath $ArtifactPath -Force -ErrorAction SilentlyContinue
    if ($null -eq $artifact) { throw "Missing build artifact: $Label ($ArtifactPath)" }
    if ($artifact.PSIsContainer) { throw "Build artifact is a directory: $Label ($ArtifactPath)" }
    $latestSource = Get-LatestSourceWriteTimeUtc $SourcePaths
    if ($null -eq $latestSource) { throw "No source inputs found for build artifact: $Label" }
    if ($artifact.LastWriteTimeUtc -lt $latestSource) {
        throw "Stale build artifact: $Label ($ArtifactPath) is older than its source inputs. Rebuild before synchronization."
    }
}

function Invoke-EmbeddedRuntimeValidation([string]$RuntimeRoot, [string]$Label) {
    if (-not (Test-Path -LiteralPath $embeddedRuntimeScript -PathType Leaf)) { throw "Embedded Runtime package script is missing: $embeddedRuntimeScript" }
    $parameters = @{
        ProjectRoot = $projectRoot
        OutputRoot = $RuntimeRoot
        ValidateOnly = $true
        AllowMissing = $true
    }
    $output = @(& $embeddedRuntimeScript @parameters 2>&1)
    foreach ($line in $output) { Write-Output "$Label Runtime $line" }
    $success = @($output | Where-Object { [string]$_ -match '^RUNTIME_PACKAGE_(OK|NOT_PRESENT)\b' }).Count -gt 0
    if (-not $success) { throw "$Label embedded Runtime validation failed: $($output | Out-String)" }
    return ($output | Out-String).Trim()
}

function Get-EmbeddedRuntimeSourcePaths([string]$SourceRoot) {
    return [ordered]@{
        'MarcusAwakeFramework.dll' = Join-Path $SourceRoot 'framework\MarcusAwakeFramework\_build_out\Release\MarcusAwakeFramework.dll'
        'MarcusAwakeTransport.dll' = Join-Path $SourceRoot 'framework\MarcusAwakeTransport\_build_out\Release\MarcusAwakeTransport.dll'
        'MarcusAwakeRuntimeService.dll' = Join-Path $SourceRoot 'framework\MarcusAwakeRuntimeService\_build_out\Release\win-x64\MarcusAwakeRuntimeService.dll'
        'MarcusAwakeRuntimeService.exe' = Join-Path $SourceRoot 'framework\MarcusAwakeRuntimeService\_build_out\Release\win-x64\MarcusAwakeRuntimeService.exe'
        'MarcusAwakeProvider.dll' = Join-Path $SourceRoot 'framework\MarcusAwakeProvider\_build_out\Release\MarcusAwakeProvider.dll'
        'MarcusAwakeStorage.dll' = Join-Path $SourceRoot 'framework\MarcusAwakeStorage\_build_out\Release\MarcusAwakeStorage.dll'
    }
}

function Assert-EmbeddedRuntimeMatchesBuild([string]$RuntimeRoot, [string]$SourceRoot, [string]$Label) {
    foreach ($entry in (Get-EmbeddedRuntimeSourcePaths $SourceRoot).GetEnumerator()) {
        $sourceHash = Get-Hash ([string]$entry.Value)
        $runtimePath = Join-Path $RuntimeRoot ([string]$entry.Key)
        $runtimeHash = Get-Hash $runtimePath
        if (-not $sourceHash) { throw "Missing Runtime build artifact: $($entry.Key) ($($entry.Value))" }
        if (-not $runtimeHash) { throw "Missing $Label Runtime component: $($entry.Key) ($runtimePath)" }
        if ($sourceHash -ne $runtimeHash) {
            throw "Stale $Label Runtime component: $($entry.Key) does not match the latest build. Run tools\package_embedded_runtime.ps1 before synchronization."
        }
    }
}

function Get-ManagedSourcePath([string]$Relative, [string]$SourceRoot, [string]$BuildDll, [string]$RuntimeSourceRoot = '') {
    if ($Relative.Equals($embeddedRuntimeRelativeRoot, [StringComparison]::OrdinalIgnoreCase) -or $Relative.StartsWith($embeddedRuntimeRelativeRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        if ([string]::IsNullOrWhiteSpace($RuntimeSourceRoot)) { throw "Runtime source root is required for managed Runtime file: $Relative" }
        $runtimeRelative = $Relative.Substring($embeddedRuntimeRelativeRoot.Length).TrimStart('\')
        return Join-Path $RuntimeSourceRoot $runtimeRelative
    }
    switch ($Relative) {
        'bin\Win64_Shipping_Client\Awake.dll' { return $BuildDll }
        'bin\Win64_Shipping_Client\MarcusAwakeFramework.dll' {
            return Join-Path $SourceRoot 'framework\MarcusAwakeFramework\_build_out\Release\MarcusAwakeFramework.dll'
        }
        'bin\Win64_Shipping_Client\MarcusAwakeTransport.dll' {
            return Join-Path $SourceRoot 'framework\MarcusAwakeTransport\_build_out\Release\MarcusAwakeTransport.dll'
        }
        default { return Join-Path $SourceRoot $Relative }
    }
}

function Add-ManagedFile([System.Collections.Generic.List[string]]$List, [string]$Relative, [string]$SourceRoot, [string]$BuildDll) {
    $source = Get-ManagedSourcePath $Relative $SourceRoot $BuildDll
    Assert-ExistingFile $source "managed source file"
    if ($List.Contains($Relative)) { throw "Duplicate managed path: $Relative" }
    $List.Add($Relative)
}

function Get-ManagedFiles([string]$SourceRoot, [string]$BuildDll) {
    $list = New-Object 'System.Collections.Generic.List[string]'
    foreach ($relative in $managedRootFiles) { Add-ManagedFile $list $relative $SourceRoot $BuildDll }
    Add-ManagedFile $list 'bin\Win64_Shipping_Client\Awake.dll' $SourceRoot $BuildDll
    Add-ManagedFile $list 'bin\Win64_Shipping_Client\MarcusAwakeFramework.dll' $SourceRoot $BuildDll
    Add-ManagedFile $list 'bin\Win64_Shipping_Client\MarcusAwakeTransport.dll' $SourceRoot $BuildDll
    foreach ($relative in $managedGuiFiles) { Add-ManagedFile $list $relative $SourceRoot $BuildDll }
    foreach ($relative in $managedLanguageFiles) { Add-ManagedFile $list $relative $SourceRoot $BuildDll }
    foreach ($relative in $managedWorldbookFiles) { Add-ManagedFile $list $relative $SourceRoot $BuildDll }
    foreach ($directory in $managedWorldbookDirectories) {
        $directoryPath = Join-Path $SourceRoot $directory
        if (-not (Test-Path -LiteralPath $directoryPath)) { continue }
        Assert-NoReparseComponents $directoryPath
        foreach ($file in Get-ChildItem -LiteralPath $directoryPath -Recurse -File -Force) {
            if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse source file is not allowed: $($file.FullName)" }
            $relative = Get-RelativePath $SourceRoot $file.FullName
            if ($relative -match '^ModuleData\\Worldbook\\persona_definitions\\persona_definitions(?:\\|$)') { throw "Nested Persona path is not an allowed source path: $relative" }
            if ($list.Contains($relative)) { throw "Duplicate managed path: $relative" }
            $list.Add($relative)
        }
    }
    return @($list | Sort-Object)
}

function Get-EmbeddedRuntimeManagedFiles([string]$RuntimeRoot) {
    if (-not (Test-Path -LiteralPath $RuntimeRoot -PathType Container)) { throw "Embedded Runtime source root is missing: $RuntimeRoot" }
    Assert-NoReparseComponents $RuntimeRoot
    $list = New-Object 'System.Collections.Generic.List[string]'
    foreach ($file in Get-ChildItem -LiteralPath $RuntimeRoot -Recurse -File -Force) {
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse Runtime source file is not allowed: $($file.FullName)" }
        $runtimeRelative = Get-RelativePath $RuntimeRoot $file.FullName
        $relative = Join-Path $embeddedRuntimeRelativeRoot $runtimeRelative
        if ($list.Contains($relative)) { throw "Duplicate managed Runtime path: $relative" }
        $list.Add($relative)
    }
    if ($list.Count -eq 0) { throw "Embedded Runtime source root is empty: $RuntimeRoot" }
    return @($list | Sort-Object)
}

function Test-PathOverlap([string]$Left, [string]$Right) {
    $leftFull = Get-FullPath $Left
    $rightFull = Get-FullPath $Right
    return $leftFull.Equals($rightFull, [StringComparison]::OrdinalIgnoreCase) -or
        (Test-PathUnderRoot $leftFull $rightFull) -or
        (Test-PathUnderRoot $rightFull $leftFull)
}

function Assert-SafeReleaseStagingRoot([string]$Path, [string]$SourceRoot, [string]$DistRoot, [string]$GameRoot) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'ReleaseStagingRoot must not be empty.' }
    if (-not [IO.Path]::IsPathRooted($Path)) { throw 'ReleaseStagingRoot must be absolute.' }
    $full = Get-FullPath $Path
    foreach ($protected in @(
        @{ Label = 'ProjectRoot'; Path = $SourceRoot },
        @{ Label = 'DistModule'; Path = $DistRoot },
        @{ Label = 'GameModule'; Path = $GameRoot }
    )) {
        if (Test-PathOverlap $full ([string]$protected.Path)) {
            throw "ReleaseStagingRoot overlaps $($protected.Label): $full"
        }
    }
    Assert-NoReparseComponents $full
    $parent = Split-Path -Parent $full
    if (Test-Path -LiteralPath $parent) {
        if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw "Release staging parent is not a directory: $parent" }
        Assert-NoReparseComponents $parent
    }
    if (Test-Path -LiteralPath $full) { throw "Release staging destination already exists: $full" }
    return $full
}

function Get-ReleaseStagingEntries([string]$SourceRoot, [string]$BuildDll, [string]$RuntimeRoot, [string[]]$ManagedFiles) {
    $entries = New-Object 'System.Collections.Generic.List[object]'
    $seen = @{}
    foreach ($relative in @($ManagedFiles)) {
        $source = Get-ManagedSourcePath $relative $SourceRoot $BuildDll
        $key = $relative.Replace('\', '/').ToLowerInvariant()
        if ($seen.ContainsKey($key)) { throw "Duplicate release staging path: $relative" }
        $seen[$key] = $true
        $entries.Add([ordered]@{ relative = $relative; source = $source })
    }
    foreach ($file in @(Get-ChildItem -LiteralPath $RuntimeRoot -Recurse -File -Force)) {
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse Runtime source file is not allowed: $($file.FullName)" }
        $runtimeRelative = Get-RelativePath $RuntimeRoot $file.FullName
        $relative = Join-Path 'bin\Win64_Shipping_Client\Runtime' $runtimeRelative
        $key = $relative.Replace('\', '/').ToLowerInvariant()
        if ($seen.ContainsKey($key)) { throw "Duplicate release staging path: $relative" }
        $seen[$key] = $true
        $entries.Add([ordered]@{ relative = $relative; source = $file.FullName })
    }
    return @($entries | Sort-Object relative)
}

function Assert-ReleaseStagingPayload([string]$Root, $Entries) {
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) { throw "Release staging directory is missing: $Root" }
    Assert-NoReparseComponents $Root
    $expected = @{}
    foreach ($entry in @($Entries)) {
        $relative = ([string]$entry.relative).Replace('\', '/')
        $key = $relative.ToLowerInvariant()
        if ($expected.ContainsKey($key)) { throw "Duplicate release staging allowlist path: $relative" }
        $expected[$key] = $entry
    }
    $actual = @{}
    foreach ($file in @(Get-ChildItem -LiteralPath $Root -Recurse -File -Force)) {
        $relative = (Get-RelativePath $Root $file.FullName).Replace('\', '/')
        $key = $relative.ToLowerInvariant()
        if ($actual.ContainsKey($key)) { throw "Duplicate release staging file path: $relative" }
        if (-not $expected.ContainsKey($key)) { throw "Unexpected release staging payload file: $relative" }
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse release staging file is not allowed: $relative" }
        $actual[$key] = $file
        $sourceHash = Get-Hash ([string]$expected[$key].source)
        if ($sourceHash -ne (Get-Hash $file.FullName)) { throw "Release staging hash mismatch: $relative" }
    }
    if ($actual.Count -ne $expected.Count) { throw "Release staging allowlist count mismatch: expected=$($expected.Count) actual=$($actual.Count)" }
    foreach ($key in $expected.Keys) {
        if (-not $actual.ContainsKey($key)) { throw "Missing release staging payload file: $($expected[$key].relative)" }
    }
    return @($actual.Values | Sort-Object FullName)
}

function Invoke-ReleaseStaging(
    [string]$SourceRoot,
    [string]$DistRoot,
    [string]$GameRoot,
    [string]$BuildDll,
    [string]$DestinationRoot,
    [string]$Report,
    [string[]]$ManagedFiles,
    [switch]$WhatIf,
    [int]$FailAfterCopies
) {
    $destination = Assert-SafeReleaseStagingRoot $DestinationRoot $SourceRoot $DistRoot $GameRoot
    $runtimeSourceRoot = Join-Path $DistRoot 'bin\Win64_Shipping_Client\Runtime'
    [void](Invoke-EmbeddedRuntimeValidation $runtimeSourceRoot 'release-staging-source')
    Assert-EmbeddedRuntimeMatchesBuild $runtimeSourceRoot $SourceRoot 'release-staging-source'
    $entries = Get-ReleaseStagingEntries $SourceRoot $BuildDll $runtimeSourceRoot $ManagedFiles
    $transaction = [ordered]@{
        schema_version = 'awake_sync_transaction_v1'
        mode = 'release_staging'
        state = 'planned'
        started_at = (Get-Date).ToUniversalTime().ToString('o')
        parameters = [ordered]@{
            project_root = $SourceRoot
            dist_module = $DistRoot
            game_module = $GameRoot
            release_staging_root = $destination
            build_dll = $BuildDll
            what_if = [bool]$WhatIf
            test_fail_after_copies = $FailAfterCopies
        }
        allowlist = [ordered]@{
            file_count = $entries.Count
            files = @($entries | ForEach-Object { $_.relative })
            game_preserved_roots = @($preservedRoots)
            excluded_patterns = @('AGENTS.md', 'docs\**', '*Task-Queue*.md', '*GRILLME*.md', 'ModuleData\Worldbook\migration_report.json')
        }
        release_staging = [ordered]@{
            root = $destination
            runtime_source_root = $runtimeSourceRoot
            destination_preexisting = $false
            file_count = $entries.Count
        }
        copied = New-Object 'System.Collections.Generic.List[string]'
        rollback = [ordered]@{ started = $false; verified = $false; destination_untouched = $true; error = $null }
    }
    if ($WhatIf) {
        $transaction.state = 'what_if'
        $transaction.release_staging.would_write = $true
        Write-Output ($transaction | ConvertTo-Json -Depth 20)
        return
    }

    $workingRoot = Join-Path ([IO.Path]::GetTempPath()) ('awake-release-staging-' + $timestamp + '-' + [Guid]::NewGuid().ToString('N'))
    $createdParentDirectories = New-Object 'System.Collections.Generic.List[string]'
    $moved = $false
    try {
        New-Item -ItemType Directory -Path $workingRoot -Force | Out-Null
        $transaction.state = 'staging'
        $copiedCount = 0
        foreach ($entry in $entries) {
            $target = Join-Path $workingRoot ([string]$entry.relative)
            $created = New-Object 'System.Collections.Generic.List[string]'
            Copy-FileWithParents ([string]$entry.source) $target $created
            if ((Get-Hash $target) -ne (Get-Hash ([string]$entry.source))) { throw "Release staging hash mismatch during copy: $($entry.relative)" }
            $copiedCount += 1
            $transaction.copied.Add([string]$entry.relative)
            if ($FailAfterCopies -gt 0 -and $copiedCount -eq $FailAfterCopies) { throw "Injected release staging failure after $copiedCount copies." }
        }
        [void](Assert-ReleaseStagingPayload $workingRoot $entries)
        $parent = Split-Path -Parent $destination
        if (-not (Test-Path -LiteralPath $parent)) {
            $cursor = $parent
            while (-not (Test-Path -LiteralPath $cursor)) {
                $createdParentDirectories.Add($cursor)
                $next = Split-Path -Parent $cursor
                if ([string]::IsNullOrWhiteSpace($next) -or $next -eq $cursor) { throw "Cannot resolve release staging parent: $cursor" }
                $cursor = $next
            }
            Assert-NoReparseComponents $cursor
            foreach ($directory in @($createdParentDirectories | Sort-Object Length)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
        }
        if (Test-Path -LiteralPath $destination) { throw "Release staging destination appeared during synchronization: $destination" }
        Move-Item -LiteralPath $workingRoot -Destination $destination
        $moved = $true
        $transaction.state = 'verified'
        $transaction.completed_at = (Get-Date).ToUniversalTime().ToString('o')
        Write-JsonFile $Report $transaction
        Write-Output ($transaction | ConvertTo-Json -Depth 20)
    } catch {
        $transaction.state = 'failed'
        $transaction.failure = $_.Exception.Message
        $transaction.rollback.started = $true
        try {
            if ($moved -and (Test-Path -LiteralPath $destination -PathType Container)) {
                [void](Assert-ReleaseStagingPayload $destination $entries)
                Remove-Item -LiteralPath $destination -Recurse -Force
            }
            if (Test-Path -LiteralPath $workingRoot -PathType Container) { Remove-Item -LiteralPath $workingRoot -Recurse -Force }
            foreach ($directory in @($createdParentDirectories | Sort-Object Length -Descending)) {
                if ((Test-Path -LiteralPath $directory -PathType Container) -and @(Get-ChildItem -LiteralPath $directory -Force).Count -eq 0) { Remove-Item -LiteralPath $directory -Force }
            }
            $transaction.rollback.destination_untouched = -not (Test-Path -LiteralPath $destination)
            $transaction.rollback.verified = [bool]$transaction.rollback.destination_untouched
            if ($transaction.rollback.verified) { $transaction.state = 'rollback_verified' }
        } catch { $transaction.rollback.error = $_.Exception.Message; $transaction.state = 'rollback_failed' }
        try { Write-JsonFile $Report $transaction } catch { }
        throw
    } finally {
        if (Test-Path -LiteralPath $workingRoot -PathType Container) { Remove-Item -LiteralPath $workingRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

function Assert-SourceManifest([string]$SourceRoot) {
    $manifestPath = Join-Path $SourceRoot 'ModuleData\Worldbook\manifest.json'
    Assert-ExistingFile $manifestPath 'worldbook manifest'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($manifest.personaDefinitionDirectory -ne 'persona_definitions/definitions' -or $manifest.personaTagRegistryFile -ne 'persona_definitions/tag_registry.json') {
        throw 'Worldbook manifest Persona paths are not canonical.'
    }
    Assert-ExistingFile (Join-Path $SourceRoot 'ModuleData\Worldbook\persona_definitions\tag_registry.json') 'Persona tag registry'
    $definitions = Join-Path $SourceRoot 'ModuleData\Worldbook\persona_definitions\definitions'
    if (-not (Test-Path -LiteralPath $definitions -PathType Container)) { throw "Persona definitions directory is missing: $definitions" }
    Assert-NoReparseComponents $definitions
}

function Assert-NoNestedPersona([string]$Root, [string]$Label, [bool]$AllowAbsent) {
    $nestedRoot = Join-Path $Root 'ModuleData\Worldbook\persona_definitions\persona_definitions'
    if (-not (Test-Path -LiteralPath $nestedRoot)) { if ($AllowAbsent) { return } else { return } }
    $files = @(Get-ChildItem -LiteralPath $nestedRoot -Recurse -File -Force)
    foreach ($file in $files) {
        $relative = Get-RelativePath $Root $file.FullName
        if ($obsoletePersonaFiles -notcontains $relative) { throw "Unexpected nested Persona file in ${Label}: $relative" }
    }
    foreach ($file in $obsoletePersonaFiles) {
        $path = Join-Path $Root $file
        if (Test-Path -LiteralPath $path -PathType Leaf) { Assert-NoReparseComponents $path }
    }
}

function Get-ProcessSnapshot {
    return @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^(Bannerlord|TaleWorlds)' } | Select-Object ProcessName, Id, StartTime)
}

function Assert-GameStopped {
    $snapshot = Get-ProcessSnapshot
    if ($snapshot.Count -gt 0) { throw "Bannerlord/TaleWorlds is running: $($snapshot | ConvertTo-Json -Compress)" }
    return $snapshot
}

function Get-FileSnapshot([string]$Root, [string[]]$ManagedFiles, [string[]]$ObsoleteFiles) {
    $managed = @{}
    foreach ($relative in $ManagedFiles) {
        $path = Join-Path $Root $relative
        if (Test-Path -LiteralPath $path -PathType Container) { throw "Managed target is a directory: $relative" }
        $managed[$relative] = [ordered]@{ exists = (Test-Path -LiteralPath $path -PathType Leaf); hash = (Get-Hash $path) }
    }
    $obsolete = @{}
    foreach ($relative in $ObsoleteFiles) {
        $path = Join-Path $Root $relative
        if (Test-Path -LiteralPath $path -PathType Container) { throw "Obsolete target is a directory: $relative" }
        if (Test-Path -LiteralPath $path -PathType Leaf) { Assert-NoReparseComponents $path }
        $obsolete[$relative] = [ordered]@{ exists = (Test-Path -LiteralPath $path -PathType Leaf); hash = (Get-Hash $path) }
    }
    $excluded = @{}
    foreach ($relative in $ManagedFiles + $ObsoleteFiles) { $excluded[$relative.ToLowerInvariant()] = $true }
    $preserved = New-Object 'System.Collections.Generic.List[object]'
    if (Test-Path -LiteralPath $Root -PathType Container) {
        foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File -Force) {
            $relative = Get-RelativePath $Root $file.FullName
            if (-not $excluded.ContainsKey($relative.ToLowerInvariant())) {
                $preserved.Add([ordered]@{ relative = $relative; hash = (Get-Hash $file.FullName) })
            }
        }
    }
    return [ordered]@{ managed = $managed; obsolete = $obsolete; preserved = @($preserved | Sort-Object relative) }
}

function Write-JsonFile([string]$Path, $Value) {
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [IO.File]::WriteAllText($Path, (($Value | ConvertTo-Json -Depth 20) + [Environment]::NewLine), (New-Object Text.UTF8Encoding($false)))
}

function Copy-FileWithParents([string]$Source, [string]$Target, [System.Collections.Generic.List[string]]$CreatedDirectories) {
    $parent = Split-Path -Parent $Target
    $missing = New-Object 'System.Collections.Generic.List[string]'
    $cursor = $parent
    while (-not (Test-Path -LiteralPath $cursor)) { $missing.Add($cursor); $cursor = Split-Path -Parent $cursor }
    foreach ($directory in @($missing | Sort-Object Length)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null; $CreatedDirectories.Add($directory) }
    Copy-Item -LiteralPath $Source -Destination $Target -Force
}

function Backup-Target([string]$Root, [string]$Label, [string[]]$ManagedFiles, [string[]]$ObsoleteFiles, [string]$BackupRoot, $Snapshot) {
    $records = New-Object 'System.Collections.Generic.List[object]'
    foreach ($kind in @('managed', 'obsolete')) {
        $entries = $Snapshot[$kind]
        foreach ($relative in $entries.Keys) {
            if (-not $entries[$relative].exists) { $records.Add([ordered]@{ label = $Label; kind = $kind; relative = $relative; exists = $false; backup = $null }); continue }
            $source = Join-Path $Root $relative
            $backup = Join-Path (Join-Path $BackupRoot $Label) $relative
            $parent = Split-Path -Parent $backup
            if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
            Copy-Item -LiteralPath $source -Destination $backup -Force
            if ((Get-Hash $backup) -ne $entries[$relative].hash) { throw "Backup hash mismatch: $Label/$relative" }
            $records.Add([ordered]@{ label = $Label; kind = $kind; relative = $relative; exists = $true; backup = $backup; hash = $entries[$relative].hash })
        }
    }
    return $records.ToArray()
}

function Restore-Target($Records, [System.Collections.Generic.List[string]]$CreatedDirectories, $Roots) {
    foreach ($record in $Records) {
        $root = $Roots[$record.label]
        $target = Join-Path $root $record.relative
        if ($record.exists) {
            Copy-FileWithParents $record.backup $target $CreatedDirectories
        } elseif (Test-Path -LiteralPath $target -PathType Leaf) {
            Remove-Item -LiteralPath $target -Force
        }
    }
    foreach ($directory in @($CreatedDirectories | Sort-Object Length -Descending)) {
        if ((Test-Path -LiteralPath $directory -PathType Container) -and @(Get-ChildItem -LiteralPath $directory -Force).Count -eq 0) { Remove-Item -LiteralPath $directory -Force }
    }
}

function Assert-SnapshotRestored([string]$Root, $Snapshot) {
    foreach ($kind in @('managed', 'obsolete')) {
        foreach ($relative in $Snapshot[$kind].Keys) {
            $expected = $Snapshot[$kind][$relative]
            $path = Join-Path $Root $relative
            $actualExists = Test-Path -LiteralPath $path -PathType Leaf
            if ($actualExists -ne $expected.exists -or ($actualExists -and (Get-Hash $path) -ne $expected.hash)) { throw "Rollback verification failed: $relative" }
        }
    }
    foreach ($entry in $Snapshot.preserved) {
        $path = Join-Path $Root $entry.relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Hash $path) -ne $entry.hash) { throw "Preserved file changed: $($entry.relative)" }
    }
}

function Assert-TargetVerified([string]$Root, [string]$SourceRoot, [string[]]$ManagedFiles, [string[]]$ObsoleteFiles, $Before, [string]$RuntimeSourceRoot = '') {
    foreach ($relative in $ManagedFiles) {
        $source = Get-ManagedSourcePath $relative $SourceRoot $BuildDllPath $RuntimeSourceRoot
        $target = Join-Path $Root $relative
        if ((Get-Hash $target) -ne (Get-Hash $source)) { throw "Managed hash mismatch: $relative" }
    }
    foreach ($relative in $ObsoleteFiles) { if (Test-Path -LiteralPath (Join-Path $Root $relative)) { throw "Obsolete path remains: $relative" } }
    $after = Get-FileSnapshot $Root $ManagedFiles $ObsoleteFiles
    foreach ($entry in $Before.preserved) {
        $matching = @($after.preserved | Where-Object { $_.relative -eq $entry.relative })
        if ($matching.Count -ne 1 -or $matching[0].hash -ne $entry.hash) { throw "Preserved file verification failed: $($entry.relative)" }
    }
}

$projectRoot = Assert-SafeModuleRoot $ProjectRoot 'ProjectRoot'
$distModule = Get-FullPath $DistModule
$gameModule = Get-FullPath $GameModule
$embeddedRuntimeRoot = Join-Path $distModule 'bin\Win64_Shipping_Client\Runtime'
$gameEmbeddedRuntimeRoot = Join-Path $gameModule 'bin\Win64_Shipping_Client\Runtime'
if (-not (Test-PathUnderRoot $projectRoot $distModule)) { throw 'DistModule must remain under ProjectRoot.' }
Assert-SafeModuleRoot $distModule 'DistModule' | Out-Null
Assert-SafeModuleRoot $gameModule 'GameModule' | Out-Null
if (-not (Test-Path -LiteralPath $projectRoot -PathType Container)) { throw "ProjectRoot is missing: $projectRoot" }
Assert-SourceManifest $projectRoot
$buildArtifacts = [ordered]@{
    'Awake.dll' = $BuildDllPath
    'MarcusAwakeFramework.dll' = Join-Path $projectRoot 'framework\MarcusAwakeFramework\_build_out\Release\MarcusAwakeFramework.dll'
    'MarcusAwakeTransport.dll' = Join-Path $projectRoot 'framework\MarcusAwakeTransport\_build_out\Release\MarcusAwakeTransport.dll'
}
$buildInputs = [ordered]@{
    'Awake.dll' = @((Join-Path $projectRoot 'src'), (Join-Path $projectRoot 'AWAKE.csproj'))
    'MarcusAwakeFramework.dll' = @((Join-Path $projectRoot 'framework\MarcusAwakeFramework\src'), (Join-Path $projectRoot 'framework\MarcusAwakeFramework\MarcusAwakeFramework.csproj'))
    'MarcusAwakeTransport.dll' = @((Join-Path $projectRoot 'framework\MarcusAwakeTransport\src'), (Join-Path $projectRoot 'framework\MarcusAwakeTransport\MarcusAwakeTransport.csproj'))
}
foreach ($entry in $buildArtifacts.GetEnumerator()) { Assert-FreshBuildArtifact ([string]$entry.Key) ([string]$entry.Value) $buildInputs[$entry.Key] }
$baseManagedFiles = Get-ManagedFiles $projectRoot $BuildDllPath
if (-not [string]::IsNullOrWhiteSpace($ReleaseStagingRoot)) {
    if ($ConfirmGameSync) { throw 'Release staging mode cannot be combined with -ConfirmGameSync.' }
    Invoke-ReleaseStaging -SourceRoot $projectRoot -DistRoot $distModule -GameRoot $gameModule -BuildDll $BuildDllPath -DestinationRoot $ReleaseStagingRoot -Report $ReportPath -ManagedFiles $baseManagedFiles -WhatIf:$WhatIf -FailAfterCopies $TestFailAfterCopies
    exit 0
}
Assert-NoNestedPersona $distModule 'dist' $true
if (-not $SkipGame) {
    if (-not $WhatIf -and -not $ConfirmGameSync) { throw 'Game synchronization requires -ConfirmGameSync.' }
    if (-not (Test-Path -LiteralPath $gameModule -PathType Container)) { throw "GameModule is missing: $gameModule" }
    Assert-NoNestedPersona $gameModule 'game' $true
}
[void](Invoke-EmbeddedRuntimeValidation $embeddedRuntimeRoot 'dist')
Assert-EmbeddedRuntimeMatchesBuild $embeddedRuntimeRoot $projectRoot 'dist'
$runtimeManagedFiles = Get-EmbeddedRuntimeManagedFiles $embeddedRuntimeRoot
$managedFiles = @($baseManagedFiles + $runtimeManagedFiles | Sort-Object)
if (-not $SkipGame) { [void](Invoke-EmbeddedRuntimeValidation $gameEmbeddedRuntimeRoot 'game') }
$distBefore = Get-FileSnapshot $distModule $managedFiles $obsoletePersonaFiles
$gameBefore = if ($SkipGame) { $null } else { Get-FileSnapshot $gameModule $managedFiles $obsoletePersonaFiles }
$processBefore = if ($SkipGame) { @() } else { Get-ProcessSnapshot }
if (-not $SkipGame -and $processBefore.Count -gt 0) { throw "Bannerlord/TaleWorlds is running before sync: $($processBefore | ConvertTo-Json -Compress)" }
$targets = [ordered]@{ dist = $distModule }
if (-not $SkipGame) { $targets.game = $gameModule }
$transaction = [ordered]@{
    schema_version = 'awake_sync_transaction_v1'
    state = 'planned'
    started_at = (Get-Date).ToUniversalTime().ToString('o')
    parameters = [ordered]@{ project_root = $projectRoot; dist_module = $DistModule; game_module = $GameModule; build_dll = $BuildDllPath; skip_game = [bool]$SkipGame; confirm_game_sync = [bool]$ConfirmGameSync; what_if = [bool]$WhatIf; test_fail_after_copies = $TestFailAfterCopies }
    process_before = @($processBefore)
    managed_files = @($managedFiles)
    targets = [ordered]@{ dist = [ordered]@{ before = $distBefore }; game = if ($gameBefore) { [ordered]@{ before = $gameBefore } } else { $null } }
    copied = New-Object 'System.Collections.Generic.List[string]'
    unchanged = New-Object 'System.Collections.Generic.List[string]'
    preserved = New-Object 'System.Collections.Generic.List[string]'
    removed = New-Object 'System.Collections.Generic.List[string]'
    created_directories = New-Object 'System.Collections.Generic.List[string]'
    backup_files = New-Object 'System.Collections.Generic.List[string]'
    rollback = [ordered]@{ started = $false; verified = $false; error = $null }
}
foreach ($entry in $distBefore.preserved) { $transaction.preserved.Add('dist/' + $entry.relative) }
if ($gameBefore) { foreach ($entry in $gameBefore.preserved) { $transaction.preserved.Add('game/' + $entry.relative) } }
if ($WhatIf) {
    $transaction.state = 'what_if'
    $transaction.targets.dist.after = 'would_apply'
    if (-not $SkipGame) { $transaction.targets.game.after = 'would_apply' }
    Write-Output ($transaction | ConvertTo-Json -Depth 20)
    exit 0
}

$stageRoot = Join-Path ([IO.Path]::GetTempPath()) ('awake-sync-stage-' + $timestamp)
$backupRoot = Join-Path ([IO.Path]::GetTempPath()) ('awake-sync-backup-' + $timestamp)
$reportParent = Split-Path -Parent $ReportPath
try {
    New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $reportParent -Force | Out-Null
    foreach ($relative in $managedFiles) {
        $source = Get-ManagedSourcePath $relative $projectRoot $BuildDllPath $embeddedRuntimeRoot
        $staged = Join-Path $stageRoot $relative
        $created = New-Object 'System.Collections.Generic.List[string]'
        Copy-FileWithParents $source $staged $created
        if ((Get-Hash $staged) -ne (Get-Hash $source)) { throw "Stage hash mismatch: $relative" }
    }
    $transaction.state = 'staged'
    $backupRecords = New-Object 'System.Collections.Generic.List[object]'
    foreach ($record in (Backup-Target $distModule 'dist' $managedFiles $obsoletePersonaFiles $backupRoot $distBefore)) { $backupRecords.Add($record) }
    if ($gameBefore) { foreach ($record in (Backup-Target $gameModule 'game' $managedFiles $obsoletePersonaFiles $backupRoot $gameBefore)) { $backupRecords.Add($record) } }
    foreach ($record in $backupRecords) { if ($record.backup) { $transaction.backup_files.Add($record.label + '/' + $record.relative) } }
    $transaction.state = 'backed_up'
    Write-JsonFile (Join-Path $backupRoot 'transaction.json') $transaction
    if (-not $SkipGame) { [void](Assert-GameStopped) }
    $appliedCopyCount = 0
    $applyCreatedDirectories = New-Object 'System.Collections.Generic.List[string]'
    foreach ($label in $targets.Keys) {
        $targetRoot = $targets[$label]
        foreach ($relative in $managedFiles) {
            $source = Join-Path $stageRoot $relative
            $target = Join-Path $targetRoot $relative
            $created = New-Object 'System.Collections.Generic.List[string]'
            Copy-FileWithParents $source $target $created
            foreach ($directory in $created) {
                $applyCreatedDirectories.Add($directory)
                if (Test-PathUnderRoot $targetRoot $directory) {
                    $transaction.created_directories.Add($label + '/' + (Get-RelativePath $targetRoot $directory))
                }
            }
            $appliedCopyCount += 1
            if ($TestFailAfterCopies -gt 0 -and $appliedCopyCount -eq $TestFailAfterCopies) { throw "Injected sync failure after $appliedCopyCount copies." }
            $before = if ($label -eq 'dist') { $distBefore.managed[$relative] } else { $gameBefore.managed[$relative] }
            if ($before.exists -and $before.hash -eq (Get-Hash $target)) { $transaction.unchanged.Add($label + '/' + $relative) } else { $transaction.copied.Add($label + '/' + $relative) }
        }
        $nestedRoot = Join-Path $targetRoot 'ModuleData\Worldbook\persona_definitions\persona_definitions'
        foreach ($relative in $obsoletePersonaFiles) {
            $path = Join-Path $targetRoot $relative
            if (Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path -Force; $transaction.removed.Add($label + '/' + $relative) }
        }
        foreach ($directory in @((Join-Path $nestedRoot 'definitions'), $nestedRoot)) {
            if ((Test-Path -LiteralPath $directory -PathType Container) -and @(Get-ChildItem -LiteralPath $directory -Force).Count -eq 0) { Remove-Item -LiteralPath $directory -Force; $transaction.removed.Add($label + '/' + (Get-RelativePath $targetRoot $directory)) }
        }
    }
    $transaction.state = 'applied'
    [void](Invoke-EmbeddedRuntimeValidation $embeddedRuntimeRoot 'dist')
    if ($gameBefore) { [void](Invoke-EmbeddedRuntimeValidation $gameEmbeddedRuntimeRoot 'game') }
    foreach ($label in $targets.Keys) {
        $targetRoot = $targets[$label]
        $before = if ($label -eq 'dist') { $distBefore } else { $gameBefore }
        Assert-TargetVerified $targetRoot $projectRoot $managedFiles $obsoletePersonaFiles $before $embeddedRuntimeRoot
    }
    $transaction.state = 'verified'
    $transaction.completed_at = (Get-Date).ToUniversalTime().ToString('o')
    Write-JsonFile $ReportPath $transaction
    Write-Output ($transaction | ConvertTo-Json -Depth 20)
} catch {
    $transaction.state = 'failed'
    $transaction.failure = $_.Exception.Message
    try {
        if ($backupRecords) {
            $transaction.rollback.started = $true
            if (-not $applyCreatedDirectories) { $applyCreatedDirectories = New-Object 'System.Collections.Generic.List[string]' }
            Restore-Target $backupRecords $applyCreatedDirectories $targets
            Assert-SnapshotRestored $distModule $distBefore
            if ($gameBefore) { Assert-SnapshotRestored $gameModule $gameBefore }
            $transaction.rollback.verified = $true
            $transaction.state = 'rollback_verified'
        }
    } catch { $transaction.rollback.error = $_.Exception.Message; $transaction.state = 'rollback_failed' }
    try { New-Item -ItemType Directory -Path $reportParent -Force | Out-Null; Write-JsonFile $ReportPath $transaction } catch { }
    throw
} finally {
    if (Test-Path -LiteralPath $stageRoot) { Remove-Item -LiteralPath $stageRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
