[CmdletBinding()]
param(
    [string]$OutputRoot = (Join-Path ([IO.Path]::GetTempPath()) ('awake-customer-package-acceptance-' + [Guid]::NewGuid().ToString('N')))
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$scriptRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageScript = Join-Path $scriptRoot 'package-customer.ps1'
$validatorScript = Join-Path $scriptRoot 'validate-customer-delivery.ps1'
$rollbackScript = Join-Path $scriptRoot 'rollback-customer.ps1'
$outputRootFull = [IO.Path]::GetFullPath($OutputRoot)
$createdOutput = -not (Test-Path -LiteralPath $outputRootFull)

function Fail([string]$Message) { throw "WB-DELIVERY-TEST-FAIL: $Message" }
if ((Get-Content -Raw -LiteralPath $packageScript) -notmatch "current-test[\\/]+WorldbookStudio-win-x64\.zip") {
    Fail 'package-customer must prefer the current-test WBS ZIP.'
}
function Invoke-Package {
    $lines = @(& pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $packageScript -OutputRoot $outputRootFull)
    if ($LASTEXITCODE -ne 0) { Fail "package-customer 失败：$([string]::Join("`n",$lines))" }
    $buildLine = $lines | Where-Object { $_ -match '^BUILD_ID=' } | Select-Object -Last 1
    if ($null -eq $buildLine) { Fail 'package-customer 没有返回 BUILD_ID。' }
    return ($buildLine -replace '^BUILD_ID=', '')
}
function Invoke-Validation([string]$BuildId) {
    $root = Join-Path $outputRootFull $BuildId
    $zip = Join-Path $outputRootFull "$BuildId.zip"
    $pointer = Join-Path $outputRootFull 'rollback-pointer.json'
    & pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $validatorScript -PackageRoot $root -ZipPath $zip -PointerPath $pointer
    if ($LASTEXITCODE -ne 0) { Fail "交付包校验失败：$BuildId" }
}

New-Item -ItemType Directory -Force -Path $outputRootFull | Out-Null
$oldTestBuildId = $env:AWAKE_CUSTOMER_DELIVERY_TEST_BUILD_ID
try {
    $first = Invoke-Package
    Invoke-Validation $first
    $firstRoot = Join-Path $outputRootFull $first
    $firstManifestHash = (Get-FileHash -LiteralPath (Join-Path $firstRoot 'delivery-manifest.json') -Algorithm SHA256).Hash

    $env:AWAKE_CUSTOMER_DELIVERY_TEST_BUILD_ID = $first
    $collisionLines = @(& pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $packageScript -OutputRoot $outputRootFull 2>&1)
    if ($LASTEXITCODE -eq 0 -or -not ([string]::Join("`n",$collisionLines) -match 'WB-DELIVERY-409')) {
        Fail 'BuildId 冲突未硬拒绝。'
    }
    if ((Get-FileHash -LiteralPath (Join-Path $firstRoot 'delivery-manifest.json') -Algorithm SHA256).Hash -ne $firstManifestHash) {
        Fail '冲突尝试改写了既有构建。'
    }
    Remove-Item Env:AWAKE_CUSTOMER_DELIVERY_TEST_BUILD_ID -ErrorAction SilentlyContinue

    $second = Invoke-Package
    Invoke-Validation $second
    if ($first -eq $second) { Fail '两次构建生成了相同 BuildId。' }
    $pointerBefore = Get-Content -Raw -LiteralPath (Join-Path $outputRootFull 'rollback-pointer.json') | ConvertFrom-Json
    if ($pointerBefore.current_build_id -ne $second -or $pointerBefore.previous_build_id -ne $first) {
        Fail '第二次构建没有正确推进 rollback pointer。'
    }

    & pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $rollbackScript -OutputRoot $outputRootFull -BuildId $first
    if ($LASTEXITCODE -ne 0) { Fail 'rollback-customer 失败。' }
    $pointerAfter = Get-Content -Raw -LiteralPath (Join-Path $outputRootFull 'rollback-pointer.json') | ConvertFrom-Json
    if ($pointerAfter.current_build_id -ne $first -or $pointerAfter.previous_build_id -ne $second) {
        Fail '回滚后 pointer 状态不正确。'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $outputRootFull "rollback-verification-$first.json") -PathType Leaf)) {
        Fail '回滚验证报告缺失。'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $outputRootFull $second) -PathType Container)) {
        Fail '回滚覆盖或删除了历史构建。'
    }
    Write-Output "PACKAGE_ONLY_ACCEPTANCE_PASS first=$first second=$second"
}
finally {
    if ($null -eq $oldTestBuildId) {
        Remove-Item Env:AWAKE_CUSTOMER_DELIVERY_TEST_BUILD_ID -ErrorAction SilentlyContinue
    }
    else {
        $env:AWAKE_CUSTOMER_DELIVERY_TEST_BUILD_ID = $oldTestBuildId
    }
    if ($createdOutput -and (Test-Path -LiteralPath $outputRootFull -PathType Container)) {
        Remove-Item -LiteralPath $outputRootFull -Recurse -Force -ErrorAction SilentlyContinue
    }
}
