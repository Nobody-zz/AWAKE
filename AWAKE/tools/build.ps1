[CmdletBinding()]
param(
    [ValidateSet('1.3.15', '1.4.8')]
    [string]$BannerlordApi = '1.3.15',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$GamePath = 'D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord',
    [string]$FrameworkPathOverride = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2',
    [switch]$SkipGameVersionCheck,
    [switch]$SkipSmoke
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

    # 判据：离线烟测必须**真跑**，不能只编译。
    # 此前这里只有上面的 dotnet build —— 构建恒绿、测试恒红，于是
    # 世界书 contentHash 口径不一致（WB2-HASH-MISMATCH:content，2026-10-01 修复）
    # 藏了 8 天没人发现：任何一次 build.ps1 都是绿的。同类欠账见
    # docs/HANDOFF-STORAGE-CONSISTENCY-20260922.md:139（"build.ps1 只编译不跑测试"）。
    #
    # ⚠️ 不得把结果并进 TESTS_OK —— 那是"编译通过"，语义不同。
    # ⚠️ 已知代价：Awake.SdkSmoke 的 dialogue-chain-redtest 判据是**已投送的**世界书
    #    （DialogueChainRedtest.ResolveDeployedManifest 默认指向游戏模块目录），
    #    所以本步骤要求游戏侧模块已投送。干净克隆上跑请加 -SkipSmoke。
    if ($SkipSmoke) {
        Write-Output "SMOKE_SKIPPED (requested by -SkipSmoke)"
    }
    else {
        $smokeExe = Join-Path $root "..\AWAKE.Tests\bin\$Configuration\net472\Awake.SdkSmoke.exe"
        if (-not (Test-Path -LiteralPath $smokeExe)) { throw "Awake.SdkSmoke.exe not found: $smokeExe" }
        $smokeOutput = & $smokeExe 2>&1
        $smokeExit = $LASTEXITCODE
        $smokeOutput | Select-String -Pattern '^(RESULT|PASS ALL|FAILED_CASES)' | ForEach-Object { Write-Output $_.Line }
        if ($smokeExit -ne 0) {
            $smokeOutput | Select-String -Pattern '^(FAIL_CASE|FAIL_MSG|FAIL_TYPE)' | ForEach-Object { Write-Output $_.Line }
            throw "Awake.SdkSmoke failed with exit code $smokeExit (offline gate is RED)"
        }
        Write-Output "SMOKE_OK configuration=$Configuration"
    }
}
finally {
    Pop-Location
}
