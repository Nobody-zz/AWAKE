param([switch]$NoWindow)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$packagedExe = Join-Path $root "PersonaWorkbench.Web.exe"
$developmentExe = Join-Path $root "src\PersonaWorkbench.Web\bin\Release\net10.0\PersonaWorkbench.Web.exe"
$projectFile = Join-Path $root "src\PersonaWorkbench.Web\PersonaWorkbench.Web.csproj"
$runtime = Join-Path $root ".runtime"
$pidFile = Join-Path $runtime "persona-workbench.pid"
$stdout = Join-Path $runtime "server.stdout.log"
$stderr = Join-Path $runtime "server.stderr.log"
$startupLog = Join-Path $runtime "startup.log"
$healthUrl = "http://127.0.0.1:51337/"
$runId = [Guid]::NewGuid().ToString("N")
$executionMode = ""
$packageVersion = ""
$buildId = ""
$sourceManifestHash = ""

function Get-Hash([string]$path) {
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Test-PackageIdentity {
    $requiredFiles = @(
        "PersonaWorkbench.Web.exe",
        "PersonaWorkbench.Launcher.exe",
        "PACKAGE-MANIFEST.sha256.txt",
        "BUILD-ID.txt",
        "BUILD-SOURCE-MANIFEST.sha256.txt"
    )
    foreach ($relativePath in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $relativePath) -PathType Leaf)) {
            throw "发布包缺少 $relativePath；请完整解压新的独立测试包，不要与旧目录合并。"
        }
    }

    $manifestPath = Join-Path $root "PACKAGE-MANIFEST.sha256.txt"
    $manifestEntries = @{}
    foreach ($line in Get-Content -LiteralPath $manifestPath -Encoding UTF8) {
        $normalized = $line.TrimStart([char]0xFEFF)
        if ([string]::IsNullOrWhiteSpace($normalized)) { continue }
        if ($normalized.Length -lt 66 -or $normalized[64] -ne [char]32 -or $normalized[65] -ne [char]32) {
            throw "发布包清单格式无效。"
        }
        $hash = $normalized.Substring(0, 64).ToUpperInvariant()
        $relativePath = $normalized.Substring(66).Replace('/', '\')
        if ($manifestEntries.ContainsKey($relativePath)) { throw "发布包清单包含重复文件：$relativePath" }
        $manifestEntries[$relativePath] = $hash
        $fullPath = [IO.Path]::GetFullPath((Join-Path $root $relativePath))
        $rootPrefix = ([IO.Path]::GetFullPath($root)).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "发布包清单缺少或越界文件：$relativePath"
        }
        if ((Get-Hash $fullPath) -ne $hash) { throw "发布包文件校验失败：$relativePath" }
    }

    $actualFiles = Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
        $relative = $_.FullName.Substring(([IO.Path]::GetFullPath($root)).Length).TrimStart([char]92, [char]47)
        $relative -notmatch '^(\.runtime|\.runtime[\\/])' -and $relative -ne 'PACKAGE-MANIFEST.sha256.txt'
    } | ForEach-Object {
        $_.FullName.Substring(([IO.Path]::GetFullPath($root)).Length).TrimStart([char]92, [char]47).Replace('/', '\')
    }
    $extraFiles = @($actualFiles | Where-Object { -not $manifestEntries.ContainsKey($_) })
    if ($extraFiles.Count -gt 0) { throw "发布包目录混入未登记文件：$($extraFiles -join '; ')" }

    $versionPath = Join-Path $root "PACKAGE-VERSION.txt"
    if (Test-Path -LiteralPath $versionPath -PathType Leaf) {
        $packageVersion = ((Get-Content -LiteralPath $versionPath -Encoding UTF8 | Where-Object { $_ -match '^Package:' } | Select-Object -First 1) -replace '^Package:\s*', '').Trim()
    }
    $buildLines = Get-Content -LiteralPath (Join-Path $root "BUILD-ID.txt") -Encoding UTF8
    $buildId = (($buildLines | Where-Object { $_ -match '^BuildId=' } | Select-Object -First 1) -replace '^BuildId=', '').Trim()
    $sourceManifestHash = (($buildLines | Where-Object { $_ -match '^SourceManifestHash=' } | Select-Object -First 1) -replace '^SourceManifestHash=', '').Trim().ToUpperInvariant()
    if ([string]::IsNullOrWhiteSpace($buildId) -or $sourceManifestHash -notmatch '^[0-9A-F]{64}$') { throw "发布包 BUILD-ID.txt 无效。" }
    if ((Get-Hash (Join-Path $root "BUILD-SOURCE-MANIFEST.sha256.txt")) -ne $sourceManifestHash) { throw "发布包源码身份校验失败。" }
    return [pscustomobject]@{
        PackageVersion = $packageVersion
        BuildId = $buildId
        SourceManifestHash = $sourceManifestHash
    }
}

if (Test-Path -LiteralPath $packagedExe -PathType Leaf) {
    $executionMode = "package"
    $packageIdentity = Test-PackageIdentity
    $packageVersion = [string]$packageIdentity.PackageVersion
    $buildId = [string]$packageIdentity.BuildId
    $sourceManifestHash = [string]$packageIdentity.SourceManifestHash
    $exe = $packagedExe
    $webProject = $root
} elseif (Test-Path -LiteralPath $projectFile -PathType Leaf) {
    $executionMode = "source"
    $webProject = Split-Path -Parent $projectFile
    & dotnet build $projectFile -c Release --nologo --no-restore
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $developmentExe -PathType Leaf)) {
        throw "当前源码构建没有生成 PersonaWorkbench.Web.exe。"
    }
    $exe = $developmentExe
} else {
    throw "没有找到当前 Persona Workbench。请使用独立测试包目录，或从源码项目根目录启动。"
}

$exeHash = Get-Hash $exe

New-Item -ItemType Directory -Force -Path $runtime | Out-Null
Set-Content -LiteralPath $stdout -Value "" -NoNewline -Encoding utf8
Set-Content -LiteralPath $stderr -Value "" -NoNewline -Encoding utf8
Set-Content -LiteralPath $startupLog -Value ("[{0}] startup_begin runId={1} executionMode={2} exePath={3} exeSha256={4} packageVersion={5} buildId={6} sourceManifestHash={7} port=51337" -f [DateTimeOffset]::Now.ToString("O"), $runId, $executionMode, $exe, $exeHash, $packageVersion, $buildId, $sourceManifestHash) -Encoding utf8

try {
if (-not (Test-Path -LiteralPath $exe)) {
    if (-not (Test-Path -LiteralPath $projectFile)) {
        throw "PersonaWorkbench.Web.exe 缺失。请重新完整解压发布包，不要直接在 ZIP 内运行。"
    }
}

function Start-PersonaWorkbenchWindow([string]$url) {
    $edgeCandidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft\Edge\Application\msedge.exe"),
        (Join-Path $env:ProgramFiles "Microsoft\Edge\Application\msedge.exe"),
        (Join-Path $env:LOCALAPPDATA "Microsoft\Edge\Application\msedge.exe")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $edge = $edgeCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $edge) {
        Start-Process -FilePath "explorer.exe" -ArgumentList $url -WindowStyle Normal
        return
    }

    $edgeProfile = Join-Path $runtime "edge-profile"
    New-Item -ItemType Directory -Force -Path $edgeProfile | Out-Null
    $edgeProcess = Start-Process -FilePath $edge -ArgumentList @(
        "--user-data-dir=`"$edgeProfile`"",
        "--app=$url",
        "--no-first-run",
        "--disable-features=msEdgeFirstRunExperience"
    ) -PassThru -WindowStyle Normal

    if (-not ("PersonaWorkbenchWindowActivation" -as [type])) {
        Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class PersonaWorkbenchWindowActivation {
    [DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@
    }

    $windowProcess = $null
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        Start-Sleep -Milliseconds 100
        $edgeProcess.Refresh()
        if ($edgeProcess.MainWindowHandle -ne 0) {
            $windowProcess = $edgeProcess
            break
        }
        $windowProcess = Get-Process msedge -ErrorAction SilentlyContinue |
            Where-Object { $_.MainWindowHandle -ne 0 -and $_.StartTime -ge $edgeProcess.StartTime.AddSeconds(-2) } |
            Sort-Object StartTime -Descending |
            Select-Object -First 1
        if ($windowProcess) { break }
    }
    $windowHandle = if ($windowProcess) { $windowProcess.MainWindowHandle } else { $null }
    if ($null -ne $windowHandle -and [IntPtr]$windowHandle -ne [IntPtr]::Zero) {
        [PersonaWorkbenchWindowActivation]::ShowWindowAsync([IntPtr]$windowHandle, 9) | Out-Null
        [PersonaWorkbenchWindowActivation]::SetForegroundWindow([IntPtr]$windowHandle) | Out-Null
    }
}
function Test-ExpectedProcess($candidate) {
    if (-not $candidate) { return $false }
    try {
        return [IO.Path]::GetFullPath($candidate.MainModule.FileName) -ieq [IO.Path]::GetFullPath($exe)
    }
    catch {
        return $false
    }
}

$process = $null
if (Test-Path -LiteralPath $pidFile) {
    $savedPid = 0
    if ([int]::TryParse((Get-Content -LiteralPath $pidFile -Raw).Trim(), [ref]$savedPid)) {
        $savedProcess = Get-Process -Id $savedPid -ErrorAction SilentlyContinue
        if (Test-ExpectedProcess $savedProcess) { $process = $savedProcess }
    }
}
if (-not $process) {
    $matching = @(Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($exe)) -ErrorAction SilentlyContinue | Where-Object { Test-ExpectedProcess $_ })
    if ($matching.Count -gt 1) { throw "Multiple Persona Workbench processes are already running." }
    if ($matching.Count -eq 1) { $process = $matching[0] }
}

$startedNew = $false
if (-not $process) {
    $otherServiceReady = $false
    try {
        $otherServiceReady = (Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200
    }
    catch {
    }
    if ($otherServiceReady) { throw "Port 51337 is already used by another process." }

    Remove-Item -LiteralPath $stdout,$stderr -Force -ErrorAction SilentlyContinue
    $process = Start-Process -FilePath $exe -ArgumentList "--no-browser" -WorkingDirectory $webProject -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $startedNew = $true
}
$process.Id | Set-Content -LiteralPath $pidFile -Encoding ascii

$ready = $false
for ($attempt = 0; $attempt -lt 120; $attempt++) {
    Start-Sleep -Milliseconds 250
    if ($process.HasExited) { break }
    try {
        $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 2
        if ($response.StatusCode -eq 200) {
            $ready = $true
            break
        }
    }
    catch {
    }
}

if (-not $ready) {
    $exitCode = if ($process.HasExited) { $process.ExitCode } else { $null }
    $failureDetail = ""
    if (Test-Path -LiteralPath $stderr) {
        $failureDetail = ((Get-Content -LiteralPath $stderr -Tail 12 -ErrorAction SilentlyContinue) |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join " "
    }
    if ($startedNew -and -not $process.HasExited -and (Test-ExpectedProcess $process)) {
        $process.Kill()
        $process.WaitForExit(5000) | Out-Null
    }
    if ($failureDetail -match "You must install or update .NET|framework.*was not found") {
        throw "此发布包仍依赖外部 .NET 运行时，请改用自包含版。详情见 .runtime/server.stderr.log。"
    }
    if ($failureDetail -match "address already in use|Failed to bind|Only one usage of each socket address") {
        throw "端口 51337 已被其他程序占用。请关闭旧版 Persona Workbench 后重试。"
    }
    if ($failureDetail -match "Access.*denied|UnauthorizedAccessException|Permission denied") {
        throw "程序没有写入或启动权限。请完整解压到桌面或文档目录，并检查安全软件隔离记录。"
    }
    if ($null -ne $exitCode) {
        $summary = if ([string]::IsNullOrWhiteSpace($failureDetail)) { "无错误日志" } else { $failureDetail }
        throw "Persona Workbench 服务启动后立即退出（退出码 $exitCode）：$summary"
    }
    throw "Persona Workbench 在 30 秒内未完成启动。请检查安全软件是否正在扫描或拦截 PersonaWorkbench.Web.exe，并查看 .runtime/server.stderr.log。"
}

Add-Content -LiteralPath $startupLog -Value ("[{0}] startup_ready runId={1} executionMode={2} exePath={3} exeSha256={4} pid={5} url={6}" -f [DateTimeOffset]::Now.ToString("O"), $runId, $executionMode, $exe, $exeHash, $process.Id, $healthUrl) -Encoding utf8
if (-not $NoWindow) { Start-PersonaWorkbenchWindow $healthUrl }
Write-Output "Persona Workbench opened on $healthUrl (PID $($process.Id))."
}
catch {
    $failureText = ($_ | Out-String).Trim()
    $failureEntry = "[{0}] startup_error runId={1} executionMode={2} exePath={3} {4}" -f [DateTimeOffset]::Now.ToString("O"), $runId, $executionMode, $exe, $failureText
    try { Add-Content -LiteralPath $startupLog -Value $failureEntry -Encoding utf8 -ErrorAction Stop } catch {}
    try { Add-Content -LiteralPath $stderr -Value $failureEntry -Encoding utf8 -ErrorAction Stop } catch {}
    throw
}
