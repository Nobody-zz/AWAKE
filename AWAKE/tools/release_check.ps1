param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$BannerlordApi = "1.3.15",
    [string]$GameModule = "D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE",
    [string]$RepoModule = "C:\Users\26811\OneDrive\文档\New project\AWAKE-Repo\AWAKE"
)

$ErrorActionPreference = 'Stop'
$distModule = Join-Path $ProjectRoot "dist\Modules\AWAKE"
$buildDll = Join-Path $ProjectRoot "_build_out\$BannerlordApi\Release\Awake.dll"
$subModuleXml = Join-Path $ProjectRoot "SubModule.xml"
$awakeConstants = Join-Path $ProjectRoot "src\AwakeConstants.cs"
$subModuleCs = Join-Path $ProjectRoot "src\SubModule.cs"
$validateScript = Join-Path $ProjectRoot "tools\validate_localization.ps1"
$failed = $false

function Assert-True([string]$Message, [bool]$Condition) {
    if (-not $Condition) {
        Write-Output "FAIL $Message"
        $script:failed = $true
    } else {
        Write-Output "PASS $Message"
    }
}

function Get-AwakeVersion([string]$Path) {
    if (-not (Test-Path $Path)) { return $null }
    $text = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    if ($text -match 'Version\s*=\s*"([0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?)"') {
        return $Matches[1]
    }
    if ($text -match 'AssemblyVersion\("([0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?)"\)') {
        return $Matches[1]
    }
    return $null
}

Write-Output "===== AWAKE Release Check ====="
Write-Output "ProjectRoot=$ProjectRoot"
Write-Output "BannerlordApi=$BannerlordApi"
Write-Output "DistModule=$distModule"
Write-Output "GameModule=$GameModule"
Write-Output "RepoModule=$RepoModule"

$moduleVersion = $null
if (Test-Path $subModuleXml) {
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

$dllPaths = @($buildDll, (Join-Path $distModule "bin\Win64_Shipping_Client\Awake.dll"))
if (Test-Path $GameModule) { $dllPaths += (Join-Path $GameModule "bin\Win64_Shipping_Client\Awake.dll") }

$dllHashes = @()
foreach ($dllPath in $dllPaths) {
    if (-not (Test-Path $dllPath)) {
        Assert-True "DLL exists: $dllPath" $false
        continue
    }
    $dllHashes += (Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash
}
if ($dllHashes.Count -gt 0) {
    Assert-True "All DLL copies have identical SHA-256" (($dllHashes | Select-Object -Unique).Count -eq 1)
} else {
    Assert-True "At least one DLL copy exists" $false
}

$required = @(
    "bin\Win64_Shipping_Client\Awake.dll",
    "GUI\Prefabs\NpcDialogue.xml",
    "GUI\Prefabs\AwakeMessenger.xml",
    "GUI\Prefabs\WorldEventInbox.xml",
    "GUI\Prefabs\WeeklyReportBrowser.xml",
    "ModuleData\Languages\awake_strings.xml",
    "ModuleData\Languages\CNs\awake_strings-zh-HANS.xml",
    "ModuleData\Worldbook\manifest.json"
)
foreach ($rel in $required) {
    Assert-True "Required file exists in dist: $rel" (Test-Path (Join-Path $distModule $rel))
}

$disallowedExtensions = @('.html', '.tmp', '.bak', '.pdb', '.user', '.log')
$disallowed = Get-ChildItem -LiteralPath $distModule -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $disallowedExtensions -contains $_.Extension.ToLowerInvariant() }
Assert-True "No disallowed files in dist ($($disallowed.Count))" ($disallowed.Count -eq 0)

if (Test-Path $validateScript) {
    $localizationOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $validateScript 2>&1
    $localizationOutput | ForEach-Object { Write-Output "LOCALIZATION $_" }
    Assert-True "Localization validation passed" `
        (($localizationOutput | Out-String) -match 'LOCALIZATION_OK')
} else {
    Assert-True "Localization validator exists" $false
}

if ($failed) {
    Write-Output "RELEASE_CHECK_FAILED"
    exit 1
}

Write-Output "RELEASE_CHECK_OK"
exit 0
