[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$WorkspaceRoot,
    [Parameter(Mandatory = $true)]
    [string]$WbsBaseUrl,
    [int]$Port = 0,
    [string]$RuntimeRoot = (Join-Path $PSScriptRoot '.runtime'),
    [string]$GameRoot = ''
)

$ErrorActionPreference = 'Stop'
$workstationId = 'ui_workstation'
$protocolVersion = 'awake.workstation.v1'
$buildId = if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'build.json')) {
    ([System.Text.Json.JsonDocument]::Parse((Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'build.json')))).RootElement.GetProperty('build_id').GetString()
} else { 'dev' }
$workspaceHash = ([System.Security.Cryptography.SHA256]::Create().ComputeHash([System.Text.Encoding]::UTF8.GetBytes([System.IO.Path]::GetFullPath($WorkspaceRoot))) |
    ForEach-Object { $_.ToString('x2') }) -join ''
$workspaceHash = $workspaceHash.Substring(0, 16)
$workspaceId = 'workspace.' + $workspaceHash

function Resolve-AwakeGameRoot([string]$Explicit) {
    if (-not [string]::IsNullOrWhiteSpace($Explicit)) { return [System.IO.Path]::GetFullPath($Explicit) }
    if (-not [string]::IsNullOrWhiteSpace($env:AWAKE_GAME_ROOT)) { return [System.IO.Path]::GetFullPath($env:AWAKE_GAME_ROOT) }
    $libraries = @()
    foreach ($probe in @(@('HKCU:\Software\Valve\Steam', 'SteamPath'), @('HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'InstallPath'))) {
        try {
            $value = (Get-ItemProperty -LiteralPath $probe[0] -Name $probe[1] -ErrorAction Stop).($probe[1])
            if (-not [string]::IsNullOrWhiteSpace($value)) { $libraries += [System.IO.Path]::GetFullPath([string]$value) }
        } catch { }
    }
    foreach ($library in @($libraries)) {
        $vdf = Join-Path $library 'steamapps\libraryfolders.vdf'
        if (-not (Test-Path -LiteralPath $vdf -PathType Leaf)) { continue }
        try {
            foreach ($match in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s+"(?<p>[^"]+)"')) {
                $libraries += $match.Groups['p'].Value.Replace('\\', '\')
            }
        } catch { }
    }
    foreach ($library in @($libraries | Select-Object -Unique)) {
        $candidate = Join-Path $library 'steamapps\common\Mount & Blade II Bannerlord'
        if (Test-Path -LiteralPath (Join-Path $candidate 'Modules\Native\SubModule.xml') -PathType Leaf) { return [System.IO.Path]::GetFullPath($candidate) }
    }
    return ''
}

function Test-PathInsideBannerlordInstall([string]$Path) {
    $current = [System.IO.Path]::GetFullPath($Path)
    for ($depth = 0; $depth -lt 6 -and -not [string]::IsNullOrWhiteSpace($current); $depth++) {
        if (Test-Path -LiteralPath (Join-Path $current 'Modules\Native\SubModule.xml') -PathType Leaf) { return $true }
        $parent = [System.IO.Directory]::GetParent($current)
        if ($null -eq $parent) { break }
        $current = $parent.FullName
    }
    return $false
}

$gameRoot = Resolve-AwakeGameRoot $GameRoot
$runtimeFull = [System.IO.Path]::GetFullPath($RuntimeRoot)
$insideGame = $false
if (-not [string]::IsNullOrWhiteSpace($gameRoot)) {
    $gamePrefix = $gameRoot.TrimEnd([char]'\', [char]'/') + [System.IO.Path]::DirectorySeparatorChar
    $insideGame = $runtimeFull.StartsWith($gamePrefix, [StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($runtimeFull, $gameRoot, [StringComparison]::OrdinalIgnoreCase)
}
if (-not $insideGame) { $insideGame = Test-PathInsideBannerlordInstall $runtimeFull }
if ($insideGame) {
    throw 'WB-WORKSTATION-GAME-403: UI Workstation 不得将运行时文件写入游戏目录。'
}
if ([string]::IsNullOrWhiteSpace($gameRoot)) {
    Write-Warning 'WB-WORKSTATION-GAME-UNVERIFIED: 未能自动定位游戏安装目录；本次改用目录特征（Modules\Native\SubModule.xml）判断运行时目录是否位于游戏内。可用 -GameRoot 或环境变量 AWAKE_GAME_ROOT 指定游戏目录以启用完整检查。'
}
$base = [Uri]$WbsBaseUrl
if ($base.Host -notin @('127.0.0.1', 'localhost', '::1')) { throw 'WB-WORKSTATION-LOOPBACK-403: WbsBaseUrl 必须是 loopback。' }
if ($base.Scheme -ne 'http') { throw 'WB-WORKSTATION-LOOPBACK-403: WbsBaseUrl 必须使用 http。' }

New-Item -ItemType Directory -Force -Path $RuntimeRoot | Out-Null
$lockPath = Join-Path $RuntimeRoot 'ui-workstation.lock.json'
$readyPath = Join-Path $RuntimeRoot 'ready.json'
$mutex = [Threading.Mutex]::new($false, 'Local\AWAKE.Workstation.ui_workstation')
$ownsMutex = $false
$listener = $null
$stopping = $false
$instanceId = 'ui-' + [Guid]::NewGuid().ToString('N')

function Write-JsonResponse([System.Net.HttpListenerResponse]$Response, [int]$StatusCode, $Payload) {
    $bytes = [Text.Encoding]::UTF8.GetBytes(($Payload | ConvertTo-Json -Compress -Depth 20))
    $Response.StatusCode = $StatusCode
    $Response.ContentType = 'application/json; charset=utf-8'
    $Response.ContentLength64 = $bytes.Length
    $Response.OutputStream.Write($bytes, 0, $bytes.Length)
    $Response.Close()
}

function Read-JsonBody([System.Net.HttpListenerRequest]$Request) {
    $reader = [IO.StreamReader]::new($Request.InputStream, [Text.Encoding]::UTF8)
    try { return $reader.ReadToEnd() | ConvertFrom-Json -Depth 20 }
    finally { $reader.Dispose() }
}

function HealthPayload([string]$State) {
    [ordered]@{
        ok = $State -eq 'ready'
        workstation_id = $workstationId
        instance_id = $instanceId
        protocol_version = $protocolVersion
        build_id = $buildId
        workspace_id = $workspaceId
        state = $State
        product = 'AWAKE.UI.Workstation'
        protocolVersion = $protocolVersion
        instanceId = $instanceId
        workspaceHash = $workspaceHash
        port = $Port
    }
}

try {
    $ownsMutex = $mutex.WaitOne(0)
    if (-not $ownsMutex) { Write-Error 'WB-WORKSTATION-INSTANCE-409: UI Workstation 已在运行。'; exit 409 }
    if (Test-Path -LiteralPath $lockPath -PathType Leaf) {
        $lock = Get-Content -Raw -LiteralPath $lockPath | ConvertFrom-Json
        $live = $false
        if ($lock.pid) {
            try { Get-Process -Id ([int]$lock.pid) -ErrorAction Stop | Out-Null; $live = $true } catch {}
        }
        if ($live) { Write-Error 'WB-WORKSTATION-INSTANCE-409: UI Workstation 锁仍由活动进程持有。'; exit 409 }
        Remove-Item -LiteralPath $lockPath -Force
    }

    $tcp = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port)
    $tcp.Start()
    $Port = ([Net.IPEndPoint]$tcp.LocalEndpoint).Port
    $tcp.Stop()
    $listener = [Net.HttpListener]::new()
    $listener.Prefixes.Add("http://127.0.0.1:$Port/")
    $listener.Start()
    $lockPayload = [ordered]@{ workstation_id = $workstationId; instance_id = $instanceId; pid = $PID; port = $Port; started_at_utc = [DateTime]::UtcNow.ToString('O') }
    [IO.File]::WriteAllText($lockPath, ($lockPayload | ConvertTo-Json -Compress), [Text.UTF8Encoding]::new($false))
    $readyPayload = [ordered]@{ workstation_id = $workstationId; instance_id = $instanceId; port = $Port; address = "http://127.0.0.1:$Port"; state = 'ready' }
    [IO.File]::WriteAllText($readyPath, ($readyPayload | ConvertTo-Json -Compress), [Text.UTF8Encoding]::new($false))
    Write-Output ($readyPayload | ConvertTo-Json -Compress)

    while ($listener.IsListening) {
        $context = $listener.GetContext()
        try {
            $path = $context.Request.Url.AbsolutePath
            if ($context.Request.HttpMethod -eq 'GET' -and $path -in @('/health', '/api/ui-workstation/health')) {
                Write-JsonResponse $context.Response 200 (HealthPayload $(if ($stopping) { 'stopping' } else { 'ready' }))
                continue
            }
            if ($context.Request.HttpMethod -eq 'POST' -and $path -eq '/shutdown') {
                $body = Read-JsonBody $context.Request
                if ([string]$body.instance_id -ne $instanceId) {
                    Write-JsonResponse $context.Response 409 @{ ok = $false; error = 'WB-WORKSTATION-INSTANCE-409'; message = '实例身份不匹配，请使用当前运行实例。' }
                    continue
                }
                $stopping = $true
                Write-JsonResponse $context.Response 200 @{ ok = $true; state = 'stopping'; instance_id = $instanceId }
                $listener.Stop()
                continue
            }
            if ($context.Request.HttpMethod -eq 'POST' -and $path -eq '/api/ui-workstation/navigate') {
                $body = Read-JsonBody $context.Request
                if ([string]$body.target -ne 'worldbook_studio' -or [string]::IsNullOrWhiteSpace([string]$body.path)) {
                    Write-JsonResponse $context.Response 400 @{ ok = $false; error = 'WB-HANDOFF-400'; message = '导航目标或档案路径无效。' }
                    continue
                }
                $encodedPath = [Uri]::EscapeDataString([string]$body.path)
                $readbackUri = "$($base.AbsoluteUri.TrimEnd('/'))/api/authoring/document?path=$encodedPath"
                try { $readback = Invoke-RestMethod -Uri $readbackUri -Method Get -TimeoutSec 10 } catch {
                    Write-JsonResponse $context.Response 503 @{ ok = $false; error = 'WB-HANDOFF-503'; message = '无法读取 Worldbook Studio 当前档案版本，请稍后重试。' }
                    continue
                }
                $data = if ($readback.data) { $readback.data } elseif ($readback.document) { $readback.document } else { $readback }
                if ([string]$data.workspace_id -ne [string]$body.workspace_id -or
                    [string]$data.document_id -ne [string]$body.document_id -or
                    [int]$data.revision -ne [int]$body.revision -or
                    [string]$data.content_sha256 -ne [string]$body.content_sha256) {
                    Write-JsonResponse $context.Response 409 @{ ok = $false; error = 'WB-HANDOFF-409'; message = 'Worldbook Studio 档案版本已变化，请刷新后再导航。' }
                    continue
                }
                $url = "$($base.AbsoluteUri.TrimEnd('/'))/editor?path=$encodedPath&revision=$([int]$body.revision)"
                Write-JsonResponse $context.Response 200 @{ ok = $true; data = @{ target = 'worldbook_studio'; url = $url; workspace_id = $body.workspace_id; document_id = $body.document_id; revision = [int]$body.revision; content_sha256 = $body.content_sha256 } }
                continue
            }
            Write-JsonResponse $context.Response 404 @{ ok = $false; error = 'WB-WORKSTATION-404'; message = 'UI Workstation 路由不存在。' }
        } catch {
            try { Write-JsonResponse $context.Response 400 @{ ok = $false; error = 'WB-WORKSTATION-400'; message = 'UI Workstation 请求无效。' } } catch {}
        }
    }
} finally {
    if ($null -ne $listener) { $listener.Close() }
    foreach ($path in @($readyPath, $lockPath)) { try { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force } } catch {} }
    if ($ownsMutex) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
