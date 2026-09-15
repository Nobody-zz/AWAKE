[CmdletBinding()]
param(
    [ValidateSet('1.3.15', '1.4.8')]
    [string]$BannerlordApi = '1.3.15',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$GamePath = 'D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord',
    [string]$FrameworkPathOverride = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2',
    [switch]$SkipGameVersionCheck
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'AWAKE.csproj'
$msbuild = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
$nativeSubModule = Join-Path $GamePath 'Modules\Native\SubModule.xml'

if (-not (Test-Path -LiteralPath $project)) { throw "AWAKE.csproj not found: $project" }
if (-not (Test-Path -LiteralPath $msbuild)) { throw "MSBuild not found: $msbuild" }
if (-not (Test-Path -LiteralPath $FrameworkPathOverride)) { throw "Framework reference root not found: $FrameworkPathOverride" }
if (-not (Test-Path -LiteralPath $GamePath)) { throw "GamePath not found: $GamePath" }

if (-not $SkipGameVersionCheck) {
    if (-not (Test-Path -LiteralPath $nativeSubModule)) { throw "Native SubModule.xml not found: $nativeSubModule" }
    [xml]$nativeXml = Get-Content -LiteralPath $nativeSubModule -Raw
    $detectedVersion = [string]$nativeXml.Module.Version.value
    if ($detectedVersion -ne "v$BannerlordApi") {
        throw "BannerlordApi=$BannerlordApi does not match GamePath Native version $detectedVersion. Supply the matching game root or use -SkipGameVersionCheck only for an explicitly isolated reference build."
    }
}

Push-Location $root
try {
    & $msbuild $project /restore /t:Rebuild "/p:Configuration=$Configuration" "/p:BannerlordApi=$BannerlordApi" "/p:GamePath=$GamePath" "/p:FrameworkPathOverride=$FrameworkPathOverride" /nologo /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "AWAKE build failed with exit code $LASTEXITCODE" }
    $output = Join-Path $root "_build_out\$BannerlordApi\$Configuration\Awake.dll"
    if (-not (Test-Path -LiteralPath $output)) { throw "Build reported success but output is missing: $output" }
    Write-Output "BUILD_OK api=$BannerlordApi configuration=$Configuration output=$output"

    # 判据：AWAKE.Tests 也必须能编译。该闸口此前不在任何构建链上 —— 这正是 F8
    # （测试工程自 e9c9069 起编不过）长期无人发现的根因。红测记录见
    # docs/AUDIT-REDTEST-GATES-20260913.md。只编译，不运行（persona 用例失败属角色卡线，见 TOPIC-CODE）。
    $testsProject = '..\AWAKE.Tests\AWAKE.Tests.csproj'
    if (-not (Test-Path -LiteralPath $testsProject)) { throw "AWAKE.Tests.csproj not found (relative to $root): $testsProject" }
    & dotnet build $testsProject -c $Configuration --nologo -v:minimal
    if ($LASTEXITCODE -ne 0) { throw "AWAKE.Tests build failed with exit code $LASTEXITCODE" }
    Write-Output "TESTS_OK configuration=$Configuration"
}
finally {
    Pop-Location
}
