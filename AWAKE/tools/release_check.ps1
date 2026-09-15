param(
    [string]$ProjectRoot = '',
    [string]$BannerlordApi = "1.3.15",
    [string]$GameModule = "D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE",
    [string]$RepoModule = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { $ProjectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path) }
if ([string]::IsNullOrWhiteSpace($RepoModule)) { $RepoModule = $ProjectRoot }
$distModule = Join-Path $ProjectRoot "dist\Modules\AWAKE"
$buildDll = Join-Path $ProjectRoot "_build_out\$BannerlordApi\Release\Awake.dll"
$distDll = Join-Path $distModule "bin\Win64_Shipping_Client\Awake.dll"
$gameDll = Join-Path $GameModule "bin\Win64_Shipping_Client\Awake.dll"
$embeddedAssemblyNames = @('MarcusAwakeFramework.dll', 'MarcusAwakeTransport.dll')
$buildBin = Join-Path $ProjectRoot "_build_out\$BannerlordApi\Release"
$distBin = Join-Path $distModule "bin\Win64_Shipping_Client"
$gameBin = Join-Path $GameModule "bin\Win64_Shipping_Client"
$frameworkBuildBin = Join-Path $ProjectRoot "framework\MarcusAwakeFramework\_build_out\Release"
$transportBuildBin = Join-Path $ProjectRoot "framework\MarcusAwakeTransport\_build_out\Release"
$runtimeBuildBin = Join-Path $ProjectRoot "framework\MarcusAwakeRuntimeService\_build_out\Release\win-x64"
$providerBuildBin = Join-Path $ProjectRoot "framework\MarcusAwakeProvider\_build_out\Release"
$storageBuildBin = Join-Path $ProjectRoot "framework\MarcusAwakeStorage\_build_out\Release"
$embeddedAssemblySourcePaths = [ordered]@{
    'MarcusAwakeFramework.dll' = Join-Path $frameworkBuildBin 'MarcusAwakeFramework.dll'
    'MarcusAwakeTransport.dll' = Join-Path $transportBuildBin 'MarcusAwakeTransport.dll'
}
$runtimeComponentSourcePaths = [ordered]@{
    'MarcusAwakeFramework.dll' = $embeddedAssemblySourcePaths['MarcusAwakeFramework.dll']
    'MarcusAwakeTransport.dll' = $embeddedAssemblySourcePaths['MarcusAwakeTransport.dll']
    'MarcusAwakeRuntimeService.dll' = Join-Path $runtimeBuildBin 'MarcusAwakeRuntimeService.dll'
    'MarcusAwakeRuntimeService.exe' = Join-Path $runtimeBuildBin 'MarcusAwakeRuntimeService.exe'
    'MarcusAwakeProvider.dll' = Join-Path $providerBuildBin 'MarcusAwakeProvider.dll'
    'MarcusAwakeStorage.dll' = Join-Path $storageBuildBin 'MarcusAwakeStorage.dll'
}
$subModuleXml = Join-Path $ProjectRoot "SubModule.xml"
$awakeConstants = Join-Path $ProjectRoot "src\AwakeConstants.cs"
$subModuleCs = Join-Path $ProjectRoot "src\SubModule.cs"
$worldbookManifest = Join-Path $ProjectRoot "ModuleData\Worldbook\manifest.json"
$distManifest = Join-Path $distModule "ModuleData\Worldbook\manifest.json"
$gameManifest = Join-Path $GameModule "ModuleData\Worldbook\manifest.json"
$validateScript = Join-Path $ProjectRoot "tools\validate_localization.ps1"
$assetLintScript = Join-Path $ProjectRoot "tools\asset_boundary_lint.ps1"
$embeddedRuntimeScript = Join-Path $ProjectRoot "tools\package_embedded_runtime.ps1"
$apiLayersScript = Join-Path $ProjectRoot "tools\verify_marcus_awake_api_layers.ps1"
$mcmContractScript = Join-Path $ProjectRoot "tools\verify_marcus_awake_mcm_contract.ps1"
$callerContractScript = Join-Path $ProjectRoot "tools\verify_marcus_awake_caller_contract.ps1"
$windowsPowerShell = Join-Path $env:WINDIR "System32\WindowsPowerShell\v1.0\powershell.exe"
$distRuntime = Join-Path $distModule "bin\Win64_Shipping_Client\Runtime"
$gameRuntime = Join-Path $GameModule "bin\Win64_Shipping_Client\Runtime"
$failed = $false
$syncBlocked = $false

function Assert-True([string]$Message, [bool]$Condition) {
    if (-not $Condition) {
        Write-Output "FAIL $Message"
        $script:failed = $true
    } else {
        Write-Output "PASS $Message"
    }
}

function Get-AwakeVersion([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    $text = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    if ($text -match 'Version\s*=\s*"([0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?)"') {
        return $Matches[1]
    }
    if ($text -match 'AssemblyVersion\("([0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?)"\)') {
        return $Matches[1]
    }
    return $null
}

function Get-AwakeBuildId([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    $text = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    if ($text -match 'BuildId\s*=\s*"([^"]+)"') {
        return $Matches[1]
    }
    return $null
}

function Get-Sha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
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

function Assert-ArtifactFresh([string]$Label, [string]$ArtifactPath, [string[]]$SourcePaths) {
    $artifact = Get-Item -LiteralPath $ArtifactPath -Force -ErrorAction SilentlyContinue
    $latestSource = Get-LatestSourceWriteTimeUtc $SourcePaths
    $fresh = $null -ne $artifact -and -not $artifact.PSIsContainer -and $null -ne $latestSource -and $artifact.LastWriteTimeUtc -ge $latestSource
    Assert-True "$Label build artifact is not older than source inputs" $fresh
    if (-not $fresh) {
        $script:syncBlocked = $true
        Write-Output "BLOCKED_SYNC stale build artifact: $Label"
    }
}

function Get-AssemblyDisplayName([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    try { return [Reflection.AssemblyName]::GetAssemblyName($Path).FullName } catch { return $null }
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    try { return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json } catch { return $null }
}

function Invoke-MarcusAwakeApiLayersValidation([string]$ScriptPath) {
    $outputPath = Join-Path ([IO.Path]::GetTempPath()) ("awake-api-layers-" + [Guid]::NewGuid().ToString('N') + '.json')
    $output = @()
    $exitCode = 1
    $evidence = $null
    try {
        $output = @(& $windowsPowerShell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $ScriptPath -ProjectRoot $ProjectRoot -OutputPath $outputPath 2>&1)
        $exitCode = $LASTEXITCODE
        $evidence = Read-Json $outputPath
    } catch {
        $output = @($_.Exception.Message)
    } finally {
        if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath -Force -ErrorAction SilentlyContinue }
    }
    return [ordered]@{ exit_code = $exitCode; text = (($output | Out-String).Trim()); evidence = $evidence }
}

function Invoke-MarcusAwakeSourceOnlyContractValidation([string]$ContractName, [string]$ScriptPath) {
    $relativePath = "tools\$(Split-Path -Leaf $ScriptPath)"
    $scriptExists = Test-Path -LiteralPath $ScriptPath -PathType Leaf
    if (-not $scriptExists) {
        Assert-True "Required source-only $ContractName contract script exists for current Marcus-Awake migration batch: $relativePath" $false
        Write-Output "SOURCE_ONLY_CONTRACT_MISSING=$relativePath"
        return
    }

    $powershellExists = Test-Path -LiteralPath $windowsPowerShell -PathType Leaf
    if (-not $powershellExists) {
        Assert-True "Windows PowerShell 5.1 executable exists for source-only $ContractName contract validation" $false
        Write-Output "SOURCE_ONLY_CONTRACT_HOST_MISSING=$windowsPowerShell"
        return
    }

    $output = @()
    $exitCode = 1
    $contractArguments = @('-ProjectRoot', $ProjectRoot)
    if ($ContractName -eq 'caller') {
        $contractArguments = @('-SourceRoot', (Join-Path $ProjectRoot 'src'))
    }
    try {
        $output = @(& $windowsPowerShell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $ScriptPath @contractArguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    catch {
        $output = @($_.Exception.Message)
    }

    $text = ($output | Out-String).Trim()
    if (-not [string]::IsNullOrWhiteSpace($text)) {
        $text -split ([char]13 + [char]10) | ForEach-Object { Write-Output "SOURCE_ONLY_$($ContractName.ToUpperInvariant()) $_" }
    }
    Write-Output ("SOURCE_ONLY_" + $ContractName + "ExitCode=" + $exitCode)
    Assert-True "Marcus-Awake source-only $ContractName contract validation passed" ($exitCode -eq 0)
}

function Invoke-EmbeddedRuntimeValidation([string]$RuntimeRoot, [bool]$AllowMissing) {
    $output = @()
    $exitCode = 1
    $runtimeArguments = @(
        '-ProjectRoot', $ProjectRoot,
        '-OutputRoot', $RuntimeRoot,
        '-ValidateOnly'
    )
    if ($AllowMissing) { $runtimeArguments += '-AllowMissing' }
    try {
        $output = @(& $windowsPowerShell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $embeddedRuntimeScript @runtimeArguments 2>&1)
        $exitCode = if ($null -eq $LASTEXITCODE) { 1 } else { [int]$LASTEXITCODE }
    } catch {
        $output = @($_.Exception.Message)
    }
    $text = ($output | Out-String).Trim()
    return [ordered]@{ exit_code = $exitCode; text = $text }
}

function Value-Or([string]$Value, [string]$Fallback) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $Fallback }
    return $Value
}

Write-Output "===== AWAKE Release Check ====="
Write-Output "ProjectRoot=$ProjectRoot"
Write-Output "BannerlordApi=$BannerlordApi"
Write-Output "DistModule=$distModule"
Write-Output "GameModule=$GameModule"
Write-Output "RepoModule=$RepoModule"

$moduleVersion = $null
if (Test-Path -LiteralPath $subModuleXml) {
    [xml]$xml = Get-Content -LiteralPath $subModuleXml -Raw -Encoding UTF8
    $moduleVersion = $xml.Module.Version.value
}
Assert-True "SubModule.xml exists and has version" ($moduleVersion -ne $null)

$constantsVersion = Get-AwakeVersion $awakeConstants
Assert-True "AwakeConstants version matches SubModule ($moduleVersion)" `
    ($moduleVersion -and $constantsVersion -and $moduleVersion -eq ("v" + $constantsVersion))

$assemblyVersion = Get-AwakeVersion $subModuleCs
Assert-True "SubModule.cs assembly version matches ($moduleVersion)" `
    ($moduleVersion -and $assemblyVersion -and (
        $assemblyVersion -eq $constantsVersion -or
        $assemblyVersion.StartsWith($constantsVersion + ".")
    ))

$buildId = Get-AwakeBuildId $awakeConstants
Assert-True "Awake BuildId exists" (-not [string]::IsNullOrWhiteSpace($buildId))
Write-Output "BuildId=$(Value-Or $buildId 'unknown')"

$apiLayersEvidence = $null
$apiLayersScriptExists = Test-Path -LiteralPath $apiLayersScript -PathType Leaf
Assert-True "Marcus-Awake layered API audit script exists" $apiLayersScriptExists
if ($apiLayersScriptExists) {
    $apiLayersResult = Invoke-MarcusAwakeApiLayersValidation $apiLayersScript
    if (-not [string]::IsNullOrWhiteSpace($apiLayersResult.text)) {
        $apiLayersResult.text -split "`r?`n" | ForEach-Object { Write-Output "API_LAYERS $_" }
    }
    $apiLayersEvidence = $apiLayersResult.evidence
    $legacyMissing = @()
    if ($null -ne $apiLayersEvidence -and $null -ne $apiLayersEvidence.legacy_baseline) {
        $legacyMissing = @($apiLayersEvidence.legacy_baseline.missing_types | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
    }
    $apiLegacyTypeCount = if ($null -ne $apiLayersEvidence -and $null -ne $apiLayersEvidence.legacy_baseline) { [int]$apiLayersEvidence.legacy_baseline.type_count } else { 0 }
    $apiCurrentTypeCount = if ($null -ne $apiLayersEvidence -and $null -ne $apiLayersEvidence.current_baseline) { [int]$apiLayersEvidence.current_baseline.type_count } else { 0 }
    $apiLayersPass = $apiLayersResult.exit_code -eq 0 -and
        $null -ne $apiLayersEvidence -and
        $apiLayersEvidence.pass -eq $true -and
        $apiLegacyTypeCount -eq 109 -and
        $legacyMissing.Count -eq 0 -and
        $apiLayersEvidence.legacy_baseline.preserved -eq $true -and
        $apiLayersEvidence.current_baseline.match -eq $true
    Write-Output "ApiLayersLegacyTypeCount=$apiLegacyTypeCount"
    Write-Output "ApiLayersCurrentTypeCount=$apiCurrentTypeCount"
    Assert-True "Marcus-Awake layered API audit preserves P3A-109 and approves current additions" $apiLayersPass
}

foreach ($contract in @(
    @{ Name = 'MCM'; Path = $mcmContractScript },
    @{ Name = 'caller'; Path = $callerContractScript }
)) {
    Invoke-MarcusAwakeSourceOnlyContractValidation -ContractName $contract.Name -ScriptPath $contract.Path
}

$buildHash = Get-Sha256 $buildDll
$distHash = Get-Sha256 $distDll
$gameHash = Get-Sha256 $gameDll
Assert-True "Build DLL exists" ($buildHash -ne $null)
Assert-True "Dist DLL exists" ($distHash -ne $null)
Write-Output "BuildDllSha256=$(Value-Or $buildHash 'missing')"
Write-Output "DistDllSha256=$(Value-Or $distHash 'missing')"
Write-Output "GameDllSha256=$(Value-Or $gameHash 'missing')"
Assert-ArtifactFresh 'Awake.dll' $buildDll @((Join-Path $ProjectRoot 'src'), (Join-Path $ProjectRoot 'AWAKE.csproj'))
if (-not $buildHash -or -not $distHash -or $buildHash -ne $distHash) {
    $syncBlocked = $true
    Write-Output "BLOCKED_SYNC AWAKE.dll source build and dist hashes differ (build=$(Value-Or $buildHash 'missing') dist=$(Value-Or $distHash 'missing'))"
} else {
    Write-Output "PASS AWAKE.dll source build and dist SHA-256 match"
}
if (-not $gameHash -or -not $buildHash -or $gameHash -ne $buildHash) {
    $syncBlocked = $true
    Write-Output "BLOCKED_SYNC AWAKE.dll game copy is missing or differs from source build/dist (build=$(Value-Or $buildHash 'missing') dist=$(Value-Or $distHash 'missing') game=$(Value-Or $gameHash 'missing'))"
} else {
    Write-Output "PASS AWAKE.dll game copy matches source build/dist"
}

foreach ($assemblyName in $embeddedAssemblyNames) {
    $latestPath = $embeddedAssemblySourcePaths[$assemblyName]
    $latestHash = Get-Sha256 $latestPath
    $buildPath = Join-Path $buildBin $assemblyName
    $distPath = Join-Path $distBin $assemblyName
    $gamePath = Join-Path $gameBin $assemblyName
    $buildAssemblyHash = Get-Sha256 $buildPath
    $distAssemblyHash = Get-Sha256 $distPath
    $gameAssemblyHash = Get-Sha256 $gamePath
    $distRuntimeAssemblyHash = Get-Sha256 (Join-Path $distRuntime $assemblyName)
    $gameRuntimeAssemblyHash = Get-Sha256 (Join-Path $gameRuntime $assemblyName)
    Assert-True "Latest Framework/Transport build exists: $assemblyName" ($latestHash -ne $null)
    Assert-True "Build embedded assembly exists: $assemblyName" ($buildAssemblyHash -ne $null)
    Assert-True "Dist embedded assembly exists: $assemblyName" ($distAssemblyHash -ne $null)
    Assert-True "Build embedded assembly is readable: $assemblyName" (-not [string]::IsNullOrWhiteSpace((Get-AssemblyDisplayName $buildPath)))
    Assert-True "Dist embedded assembly is readable: $assemblyName" (-not [string]::IsNullOrWhiteSpace((Get-AssemblyDisplayName $distPath)))
    Write-Output "Latest\${assemblyName}Sha256=$(Value-Or $latestHash 'missing')"
    Write-Output "Build\${assemblyName}Sha256=$(Value-Or $buildAssemblyHash 'missing')"
    Write-Output "Dist\${assemblyName}Sha256=$(Value-Or $distAssemblyHash 'missing')"
    Write-Output "Game\${assemblyName}Sha256=$(Value-Or $gameAssemblyHash 'missing')"
    Write-Output "RuntimeDist\${assemblyName}Sha256=$(Value-Or $distRuntimeAssemblyHash 'missing')"
    Write-Output "RuntimeGame\${assemblyName}Sha256=$(Value-Or $gameRuntimeAssemblyHash 'missing')"
    $assemblySourceInputs = if ($assemblyName -eq 'MarcusAwakeFramework.dll') {
        @((Join-Path $ProjectRoot 'framework\MarcusAwakeFramework\src'), (Join-Path $ProjectRoot 'framework\MarcusAwakeFramework\MarcusAwakeFramework.csproj'))
    } else {
        @((Join-Path $ProjectRoot 'framework\MarcusAwakeTransport\src'), (Join-Path $ProjectRoot 'framework\MarcusAwakeTransport\MarcusAwakeTransport.csproj'))
    }
    Assert-ArtifactFresh $assemblyName $latestPath $assemblySourceInputs
    if (-not $latestHash -or -not $buildAssemblyHash -or -not $distAssemblyHash -or -not $distRuntimeAssemblyHash -or
        $latestHash -ne $buildAssemblyHash -or $latestHash -ne $distAssemblyHash -or $latestHash -ne $distRuntimeAssemblyHash) {
        $syncBlocked = $true
        Write-Output "BLOCKED_SYNC $assemblyName latest source/build/dist/Runtime hashes differ"
    } else {
        Write-Output "PASS $assemblyName latest source/build/dist/Runtime hashes match"
    }
    if (-not $gameAssemblyHash -or -not $buildAssemblyHash -or $gameAssemblyHash -ne $buildAssemblyHash) {
        $syncBlocked = $true
        Write-Output "BLOCKED_SYNC $assemblyName game copy is missing or differs from source build/dist"
    } else {
        Write-Output "PASS $assemblyName game copy matches source build/dist"
    }
    if ($apiLayersEvidence -and $assemblyName -eq 'MarcusAwakeFramework.dll') {
        $apiAssemblyHash = [string]$apiLayersEvidence.assembly.sha256
        Write-Output "ApiLayersFrameworkSha256=$(Value-Or $apiAssemblyHash 'missing')"
        Assert-True "Layered API evidence matches latest Framework build SHA-256" `
            ($apiAssemblyHash -and $latestHash -and $apiAssemblyHash.Equals($latestHash, [StringComparison]::OrdinalIgnoreCase))
    }
}

$sourceManifestHash = Get-Sha256 $worldbookManifest
$distManifestHash = Get-Sha256 $distManifest
$gameManifestHash = Get-Sha256 $gameManifest
Assert-True "Source worldbook manifest exists" ($sourceManifestHash -ne $null)
Assert-True "Dist worldbook manifest exists" ($distManifestHash -ne $null)
Write-Output "WorldbookManifestSha256=$(Value-Or $sourceManifestHash 'missing')"
Write-Output "DistWorldbookManifestSha256=$(Value-Or $distManifestHash 'missing')"
Write-Output "GameWorldbookManifestSha256=$(Value-Or $gameManifestHash 'missing')"
if (-not $sourceManifestHash -or -not $distManifestHash -or $sourceManifestHash -ne $distManifestHash) {
    $syncBlocked = $true
    Write-Output "BLOCKED_SYNC worldbook manifest source and dist hashes differ (source=$(Value-Or $sourceManifestHash 'missing') dist=$(Value-Or $distManifestHash 'missing'))"
} else {
    Write-Output "PASS Source and dist worldbook manifest SHA-256 match"
}
if (-not $gameManifestHash -or -not $sourceManifestHash -or $gameManifestHash -ne $sourceManifestHash) {
    $syncBlocked = $true
    Write-Output "BLOCKED_SYNC worldbook manifest in game is missing or differs from source/dist"
} else {
    Write-Output "PASS Game worldbook manifest matches candidate"
}

$worldbookRoot = Join-Path $ProjectRoot "ModuleData\Worldbook"
$worldbookJsonCount = @(Get-ChildItem -LiteralPath $worldbookRoot -Recurse -File -Filter *.json -ErrorAction SilentlyContinue).Count
# Repository-side package form (2026-09-14, docs/worldbook-studio-plan/RUNTIME-MAPPING-CONTRACT.md):
# the root carries a registry (awake.worldbook.registry.v1) and the content lives in packages/<world>/.
# The old rules/ + personality_background/ counts were always 0: the runtime rejects awake.worldbook.v1
# (WB2-SCHEMA-UNSUPPORTED:entry) and nothing under src/ reads those directories.
$worldbookRegistry = $null
try { $worldbookRegistry = Get-Content -LiteralPath (Join-Path $worldbookRoot 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json } catch { }
$worldbookPackageCount = @($worldbookRegistry.packages).Count
$worldbookRuntimeBytes = 0
foreach ($package in @($worldbookRegistry.packages)) {
    $packageRoot = Join-Path $worldbookRoot ([string]$package.relativePath).Replace('/', '\')
    $packageManifestPath = Join-Path $packageRoot 'manifest.json'
    if (-not (Test-Path -LiteralPath $packageManifestPath -PathType Leaf)) { continue }
    $packageManifest = $null
    try { $packageManifest = Get-Content -LiteralPath $packageManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json } catch { continue }
    $runtimeRelative = [string]$packageManifest.entrypoints.runtime
    if ([string]::IsNullOrWhiteSpace($runtimeRelative)) { continue }
    $runtimePath = Join-Path $packageRoot ($runtimeRelative.Replace('/', '\'))
    if (-not (Test-Path -LiteralPath $runtimePath -PathType Leaf)) { continue }
    # Do NOT ConvertFrom-Json the package runtime here. Windows PowerShell 5.1 pipes JSON through
    # JavaScriptSerializer, which treats object keys CASE-INSENSITIVELY and throws on
    # 'duplicate key "Cow" and "cow"' -- and the runtime keyword table legitimately holds both.
    # Entry-level structure is verified by tools/worldbook-runtime-smoke (TestRepositoryPackageForm)
    # and by WorldbookPackageIntegrity.ReadAndVerify, both of which use Newtonsoft.
    $worldbookRuntimeBytes += (Get-Item -LiteralPath $runtimePath).Length
}
Write-Output "WorldbookJsonFiles=$worldbookJsonCount"
Write-Output "WorldbookRegistryPackages=$worldbookPackageCount"
Write-Output "WorldbookRuntimeBytes=$worldbookRuntimeBytes"
Assert-True "Source worldbook registry lists at least one package" ($worldbookPackageCount -ge 1)
Assert-True "Source worldbook packages carry a runtime payload" ($worldbookRuntimeBytes -gt 0)

$required = @(
    "bin\Win64_Shipping_Client\Awake.dll",
    "bin\Win64_Shipping_Client\MarcusAwakeFramework.dll",
    "bin\Win64_Shipping_Client\MarcusAwakeTransport.dll",
    "GUI\Prefabs\NpcDialogue.xml",
    "GUI\Prefabs\AwakeMessenger.xml",
    "GUI\Prefabs\WorldEventInbox.xml",
    "GUI\Prefabs\WeeklyReportBrowser.xml",
    "GUI\Prefabs\SceneDialogueStatus.xml",
    "ModuleData\Languages\awake_strings.xml",
    "ModuleData\Languages\CNs\awake_strings-zh-HANS.xml",
    "ModuleData\Worldbook\manifest.json"
)
foreach ($rel in $required) {
    Assert-True "Required file exists in dist: $rel" (Test-Path -LiteralPath (Join-Path $distModule $rel))
}

# The registry alone is not enough: without the package it points at, the module ships no world book
# and the runtime throws WB2-MANIFEST-MISSING. Derive the expectation from the registry rather than
# hardcoding a package slug (2026-09-14, repository-side package form).
$distRegistryPath = Join-Path $distModule "ModuleData\Worldbook\manifest.json"
if (Test-Path -LiteralPath $distRegistryPath -PathType Leaf) {
    $distRegistry = $null
    try { $distRegistry = Get-Content -LiteralPath $distRegistryPath -Raw -Encoding UTF8 | ConvertFrom-Json } catch { }
    Assert-True "Dist worldbook registry parses and is awake.worldbook.registry.v1" ($null -ne $distRegistry -and [string]$distRegistry.schemaVersion -eq 'awake.worldbook.registry.v1')
    Assert-True "Dist worldbook registry lists at least one package" (@($distRegistry.packages).Count -ge 1)
    foreach ($package in @($distRegistry.packages)) {
        $packageId = [string]$package.packageId
        $packageRoot = Join-Path (Join-Path $distModule "ModuleData\Worldbook") ([string]$package.relativePath).Replace('/', '\')
        $packageManifestPath = Join-Path $packageRoot "manifest.json"
        Assert-True "Dist worldbook package manifest exists: $packageId" (Test-Path -LiteralPath $packageManifestPath -PathType Leaf)
        if (-not (Test-Path -LiteralPath $packageManifestPath -PathType Leaf)) { continue }
        $packageManifest = $null
        try { $packageManifest = Get-Content -LiteralPath $packageManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json } catch { }
        Assert-True "Dist worldbook package is awake.worldbook.v2: $packageId" ($null -ne $packageManifest -and [string]$packageManifest.schemaVersion -eq 'awake.worldbook.v2')
        if ($null -eq $packageManifest) { continue }
        foreach ($entrypoint in @('runtime', 'index')) {
            $entryRelative = ([string]$packageManifest.entrypoints.$entrypoint).Replace('/', '\')
            Assert-True "Dist worldbook package $entrypoint exists: $packageId" (-not [string]::IsNullOrWhiteSpace($entryRelative) -and (Test-Path -LiteralPath (Join-Path $packageRoot $entryRelative) -PathType Leaf))
        }
        $sourcePackageRoot = Join-Path $worldbookRoot ([string]$package.relativePath).Replace('/', '\')
        foreach ($name in @('manifest.json', 'runtime.json', 'index.json')) {
            $sourceFile = Join-Path $sourcePackageRoot $name
            $distFile = Join-Path $packageRoot $name
            if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) { continue }
            Assert-True "Source and dist worldbook package $name match: $packageId" ((Get-Sha256 $sourceFile) -and (Get-Sha256 $sourceFile) -eq (Get-Sha256 $distFile))
        }
    }
}

$runtimeScriptExists = Test-Path -LiteralPath $embeddedRuntimeScript -PathType Leaf
Assert-True "Embedded Runtime package script exists" $runtimeScriptExists
if ($runtimeScriptExists) {
    $distRuntimeResult = Invoke-EmbeddedRuntimeValidation $distRuntime $false
    if (-not [string]::IsNullOrWhiteSpace($distRuntimeResult.text)) { Write-Output "RUNTIME_DIST $($distRuntimeResult.text)" }
    $distRuntimeValid = $distRuntimeResult.exit_code -eq 0 -and $distRuntimeResult.text -match 'RUNTIME_PACKAGE_OK'
    Assert-True "Embedded Runtime package validates in dist" $distRuntimeValid
    $distRuntimeManifestHash = Get-Sha256 (Join-Path $distRuntime 'manifest.json')
    $distRuntimeSumsHash = Get-Sha256 (Join-Path $distRuntime 'SHA256SUMS.txt')
    Write-Output "DistRuntimeManifestSha256=$(Value-Or $distRuntimeManifestHash 'missing')"
    Write-Output "DistRuntimeSumsSha256=$(Value-Or $distRuntimeSumsHash 'missing')"
    Write-Output "RuntimePackageManifestSha256=$(Value-Or $distRuntimeManifestHash 'missing')"
    Write-Output "RuntimePackageSumsSha256=$(Value-Or $distRuntimeSumsHash 'missing')"

    $gameRuntimeResult = Invoke-EmbeddedRuntimeValidation $gameRuntime $true
    if (-not [string]::IsNullOrWhiteSpace($gameRuntimeResult.text)) { Write-Output "RUNTIME_GAME $($gameRuntimeResult.text)" }
    $gameRuntimeManifestHash = Get-Sha256 (Join-Path $gameRuntime 'manifest.json')
    $gameRuntimeSumsHash = Get-Sha256 (Join-Path $gameRuntime 'SHA256SUMS.txt')
    Write-Output "GameRuntimeManifestSha256=$(Value-Or $gameRuntimeManifestHash 'missing')"
    Write-Output "GameRuntimeSumsSha256=$(Value-Or $gameRuntimeSumsHash 'missing')"
    if ($gameRuntimeResult.text -match 'RUNTIME_PACKAGE_NOT_PRESENT') {
        $syncBlocked = $true
        Write-Output "BLOCKED_SYNC embedded Runtime package is missing in game"
    } elseif ($gameRuntimeResult.exit_code -ne 0) {
        $syncBlocked = $true
        Write-Output "BLOCKED_SYNC embedded Runtime package in game is invalid"
    } elseif (-not $distRuntimeValid -or -not $distRuntimeManifestHash -or -not $distRuntimeSumsHash -or $gameRuntimeManifestHash -ne $distRuntimeManifestHash -or $gameRuntimeSumsHash -ne $distRuntimeSumsHash) {
        $syncBlocked = $true
        Write-Output "BLOCKED_SYNC embedded Runtime package in game differs from dist"
    } else {
        Write-Output "PASS Game embedded Runtime manifest and SHA256SUMS match dist"
    }

    foreach ($component in $runtimeComponentSourcePaths.GetEnumerator()) {
        $componentName = [string]$component.Key
        $sourceComponentHash = Get-Sha256 ([string]$component.Value)
        $distComponentHash = Get-Sha256 (Join-Path $distRuntime $componentName)
        $gameComponentHash = Get-Sha256 (Join-Path $gameRuntime $componentName)
        Assert-True "Latest Runtime component build exists: $componentName" ($sourceComponentHash -ne $null)
        Write-Output "RuntimeSource\${componentName}Sha256=$(Value-Or $sourceComponentHash 'missing')"
        Write-Output "RuntimeDist\${componentName}Sha256=$(Value-Or $distComponentHash 'missing')"
        Write-Output "RuntimeGame\${componentName}Sha256=$(Value-Or $gameComponentHash 'missing')"
        $componentSourceInputs = switch ($componentName) {
            'MarcusAwakeFramework.dll' { @((Join-Path $ProjectRoot 'framework\MarcusAwakeFramework\src'), (Join-Path $ProjectRoot 'framework\MarcusAwakeFramework\MarcusAwakeFramework.csproj')); break }
            'MarcusAwakeTransport.dll' { @((Join-Path $ProjectRoot 'framework\MarcusAwakeTransport\src'), (Join-Path $ProjectRoot 'framework\MarcusAwakeTransport\MarcusAwakeTransport.csproj')); break }
            'MarcusAwakeRuntimeService.dll' { @((Join-Path $ProjectRoot 'framework\MarcusAwakeRuntimeService\src'), (Join-Path $ProjectRoot 'framework\MarcusAwakeRuntimeService\MarcusAwakeRuntimeService.csproj')); break }
            'MarcusAwakeRuntimeService.exe' { @((Join-Path $ProjectRoot 'framework\MarcusAwakeRuntimeService\src'), (Join-Path $ProjectRoot 'framework\MarcusAwakeRuntimeService\MarcusAwakeRuntimeService.csproj')); break }
            'MarcusAwakeProvider.dll' { @((Join-Path $ProjectRoot 'framework\MarcusAwakeProvider\src'), (Join-Path $ProjectRoot 'framework\MarcusAwakeProvider\MarcusAwakeProvider.csproj')); break }
            'MarcusAwakeStorage.dll' { @((Join-Path $ProjectRoot 'framework\MarcusAwakeStorage\src'), (Join-Path $ProjectRoot 'framework\MarcusAwakeStorage\MarcusAwakeStorage.csproj')); break }
            default { @() }
        }
        Assert-ArtifactFresh "Runtime $componentName" ([string]$component.Value) $componentSourceInputs
        if (-not $sourceComponentHash -or -not $distComponentHash -or $sourceComponentHash -ne $distComponentHash) {
            $syncBlocked = $true
            Write-Output "BLOCKED_SYNC Runtime package component differs from latest build: $componentName"
        } else {
            Write-Output "PASS Runtime package component matches latest build: $componentName"
        }
        if ($gameRuntimeResult.text -notmatch 'RUNTIME_PACKAGE_NOT_PRESENT' -and
            (-not $gameComponentHash -or -not $distComponentHash -or $gameComponentHash -ne $distComponentHash)) {
            $syncBlocked = $true
            Write-Output "BLOCKED_SYNC Runtime package game component is missing or differs from dist: $componentName"
        }
    }
}

$personaRequired = @(
    "ModuleData\Worldbook\persona_definitions\tag_registry.json",
    "ModuleData\Worldbook\persona_definitions\definitions\hero_default.json"
)
$personaRoots = @(
    @{ Name = "source"; Root = $ProjectRoot },
    @{ Name = "dist"; Root = $distModule },
    @{ Name = "game"; Root = $GameModule }
)
foreach ($entry in $personaRoots) {
    foreach ($rel in $personaRequired) {
        $personaPath = Join-Path $entry.Root $rel
        if ($entry.Name -eq "game" -and -not (Test-Path -LiteralPath $personaPath -PathType Leaf)) {
            $syncBlocked = $true
            Write-Output "BLOCKED_SYNC missing Persona file in game: $rel"
        } else {
            Assert-True "Persona file exists ($($entry.Name)): $rel" (Test-Path -LiteralPath $personaPath -PathType Leaf)
        }
    }
    $nested = Join-Path $entry.Root "ModuleData\Worldbook\persona_definitions\persona_definitions"
    $nestedFiles = @(Get-ChildItem -LiteralPath $nested -Recurse -File -ErrorAction SilentlyContinue)
    if ($entry.Name -eq "game" -and $nestedFiles.Count -gt 0) {
        $syncBlocked = $true
        Write-Output "BLOCKED_SYNC nested Persona files in game: $($nestedFiles.Count)"
    } else {
        Assert-True "No nested Persona files ($($entry.Name)) ($($nestedFiles.Count))" ($nestedFiles.Count -eq 0)
    }
}
$disallowedExtensions = @('.html', '.tmp', '.bak', '.pdb', '.user', '.log')
$disallowed = @(Get-ChildItem -LiteralPath $distModule -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $disallowedExtensions -contains $_.Extension.ToLowerInvariant() })
Assert-True "No disallowed files in dist ($($disallowed.Count))" ($disallowed.Count -eq 0)

if (Test-Path -LiteralPath $validateScript) {
    $localizationOutput = & $windowsPowerShell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $validateScript 2>&1
    $localizationOutput | ForEach-Object { Write-Output "LOCALIZATION $_" }
    Assert-True "Localization validation passed" `
        (($localizationOutput | Out-String) -match 'LOCALIZATION_OK')
} else {
    Assert-True "Localization validator exists" $false
}

if (Test-Path -LiteralPath $assetLintScript) {
    $assetOutput = & $windowsPowerShell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $assetLintScript 2>&1
    $assetOutput | ForEach-Object { Write-Output "ASSET_LINT $_" }
    Assert-True "Asset boundary lint passed" `
        (($assetOutput | Out-String) -match 'ASSET_BOUNDARY_OK')
} else {
    Assert-True "Asset boundary lint exists" $false
}

if ($failed) {
    Write-Output "RELEASE_CHECK_FAILED"
    Write-Output "RELEASE_STATIC_CHECK_FAILED=true"
    if ($syncBlocked) {
        Write-Output "RELEASE_STATUS=BLOCKED_SYNC"
    } else {
        Write-Output "RELEASE_STATUS=FAILED"
    }
    exit 1
}

Write-Output "RELEASE_CHECK_OK"
if ($syncBlocked) {
    Write-Output "RELEASE_STATUS=BLOCKED_SYNC"
} else {
    Write-Output "RELEASE_STATUS=CANDIDATE_SYNC_OK"
}
exit 0
