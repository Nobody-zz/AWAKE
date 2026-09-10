param(
    [string]$EvidencePath = "artifacts\current-test\evidence\authoring-save-smoke.test.json",
    [switch]$KeepTemp
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$webProject = Join-Path $root 'src\Awake.WorldbookStudio.Web\Awake.WorldbookStudio.Web.csproj'
$schemaRoot = Join-Path $root '..\..\docs\worldbook-studio-plan'
$fixturePath = Join-Path $schemaRoot 'fixture-valid-minimal.yaml'
$webPort = 0
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('awake-worldbook-authoring-save-smoke-' + [Guid]::NewGuid().ToString('N'))
$workspace = Join-Path $tempRoot 'workspace'
$targetPath = Join-Path $workspace 'authoring\demo.yaml'
$webLog = Join-Path $tempRoot 'web.log'
$webErrorLog = Join-Path $tempRoot 'web-error.log'
$webProcess = $null
$cases = @()
$evidence = [ordered]@{
    schema_version = 'awake.worldbook.authoring-save-smoke.v1'
    started_at_utc = [DateTime]::UtcNow.ToString('O')
    finished_at_utc = $null
    cases = @()
    network_boundary = 'loopback_only'
    game_directory_touched = $false
    source_auto_saved = $false
    passed = $false
    error = $null
}

function Select-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try { $listener.Start(); return ([Net.IPEndPoint]$listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}

function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "WB-AUTHORING-SMOKE-FAIL: $Message" }
}

function Stop-Child([object]$Child) {
    if ($null -eq $Child) { return }
    try {
        if (-not $Child.HasExited) {
            try { $Child.Kill($true) } catch { $Child.Kill() }
            [void]$Child.WaitForExit(5000)
        }
    }
    catch { }
    try { $Child.Dispose() } catch { }
}

function Read-Json([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { throw 'WB-AUTHORING-SMOKE-JSON: 响应为空。' }
    return $Text | ConvertFrom-Json
}

function Invoke-Json([string]$Method, [string]$Uri, [object]$Body) {
    $parameters = @{
        Method = $Method
        Uri = $Uri
        UseBasicParsing = $true
        TimeoutSec = 20
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $json = $Body | ConvertTo-Json -Compress -Depth 30
        $parameters.Body = [Text.Encoding]::UTF8.GetBytes($json)
    }
    try { $response = Invoke-WebRequest @parameters }
    catch {
        $errorResponse = $_.Exception.Response
        if ($null -eq $errorResponse) { throw }
        $content = $_.ErrorDetails.Message
        $response = [pscustomobject]@{ StatusCode = [int]$errorResponse.StatusCode; Content = $content }
    }
    return [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Body = Read-Json $response.Content }
}

function Read-Document([string]$Origin, [string]$Path) {
    $encoded = [Uri]::EscapeDataString($Path)
    $response = Invoke-WebRequest -Uri "$Origin/api/document?path=$encoded" -UseBasicParsing -TimeoutSec 20
    Assert ($response.StatusCode -eq 200) '档案读取失败。'
    return Read-Json $response.Content
}

function File-Hash([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

try {
    Assert (Test-Path -LiteralPath $webProject -PathType Leaf) 'Web 项目文件不存在。'
    Assert (Test-Path -LiteralPath $fixturePath -PathType Leaf) '保存烟测 fixture 不存在。'
    New-Item -ItemType Directory -Force -Path (Join-Path $workspace 'authoring') | Out-Null
    Copy-Item -LiteralPath $fixturePath -Destination $targetPath

    if ($env:AWAKE_WB_TEST_PORT) { $webPort = [int]$env:AWAKE_WB_TEST_PORT } else { $webPort = Select-FreePort }
    $environment = @{
        AWAKE_WB_DEV_MODE = '1'
        WORLD_BOOK_WORKSPACE = $workspace
        WORLD_BOOK_SCHEMA_ROOT = $schemaRoot
        AWAKE_WB_PORT = [string]$webPort
        ASPNETCORE_ENVIRONMENT = 'Development'
        DOTNET_ENVIRONMENT = 'Development'
    }
    $arguments = @('run', '--project', ('"' + $webProject + '"'), '--configuration', 'Release', '--no-build', '--no-restore')
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = (Get-Command dotnet -ErrorAction Stop).Source
    $startInfo.WorkingDirectory = $root
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Arguments = (($arguments | ForEach-Object { '"' + $_.Trim('"').Replace('"', '\"') + '"' }) -join ' ')
    foreach ($entry in $environment.GetEnumerator()) { $startInfo.Environment[$entry.Key] = [string]$entry.Value }
    $webProcess = [Diagnostics.Process]::new()
    $webProcess.StartInfo = $startInfo
    [void]$webProcess.Start()
    $stdoutTask = $webProcess.StandardOutput.ReadToEndAsync()
    $stderrTask = $webProcess.StandardError.ReadToEndAsync()
    $webProcess.add_Exited({
        try { [IO.File]::WriteAllText($webLog, $stdoutTask.Result) } catch { }
        try { [IO.File]::WriteAllText($webErrorLog, $stderrTask.Result) } catch { }
    })
    $origin = "http://127.0.0.1:$webPort"
    $healthy = $false
    for ($attempt = 0; $attempt -lt 80 -and -not $healthy; $attempt++) {
        try { $healthy = (Invoke-WebRequest -Uri "$origin/api/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { Start-Sleep -Milliseconds 250 }
    }
    Assert $healthy 'Web Studio 未能启动到健康状态。'

    $initial = Read-Document $origin 'authoring/demo.yaml'
    $initialContent = [string]$initial.content
    $initialHash = [string]$initial.report.inputHash
    $initialRevision = [int]$initial.document.revision
    Assert ($initialRevision -eq 1 -and $initialHash.Length -eq 64) '初始档案版本信息不完整。'
    $firstContent = $initialContent.Replace('示例事实', '第一次更新')
    $firstPayload = [ordered]@{ path = 'authoring/demo.yaml'; content = $firstContent; sourceHash = $initialHash; revision = $initialRevision }

    $saved = Invoke-Json 'Post' "$origin/api/authoring/save-authoring" $firstPayload
    Assert ($saved.StatusCode -eq 200 -and $saved.Body.ok -eq $true) ("合法高级保存没有成功。status=$($saved.StatusCode) error=$($saved.Body.error) message=$($saved.Body.message) diagnostics=$($saved.Body.diagnostics | ConvertTo-Json -Compress -Depth 5)")
    Assert ([int]$saved.Body.revision -eq 2 -and [string]$saved.Body.sourceHash -ne $initialHash) '高级保存没有推进 revision/hash。'
    Assert ([string]$saved.Body.expectedContentHash -match '^[A-Fa-f0-9]{64}$' -and [string]$saved.Body.expectedContentHash -eq [string]$saved.Body.sourceHash) '高级保存没有返回与实际写入版本对应的期望内容哈希。'
    $afterFirst = Read-Document $origin 'authoring/demo.yaml'
    $afterFirstHash = [string]$afterFirst.report.inputHash
    $afterFirstRevision = [int]$afterFirst.document.revision
    Assert ($afterFirstRevision -eq 2 -and [string]$afterFirst.document.assertions[0].text.'zh-CN' -eq '第一次更新') ("合法保存后的磁盘内容不正确。revision=$afterFirstRevision text=$([string]$afterFirst.document.assertions[0].text.'zh-CN') responseText=$([string]$saved.Body.document.document.assertions[0].text.'zh-CN')")
    $cases += [ordered]@{ id = 'valid-save-advances-version'; passed = $true; revision = $afterFirstRevision }

    $beforeInvalidHash = File-Hash $targetPath
    $invalid = Invoke-Json 'Post' "$origin/api/authoring/save-authoring" ([ordered]@{ path = 'authoring/demo.yaml'; content = 'title: [unclosed'; sourceHash = $afterFirstHash; revision = $afterFirstRevision })
    Assert ($invalid.StatusCode -eq 422 -and [string]$invalid.Body.error -eq 'WB-AUTHORING-PARSE-422') '非法 YAML 没有返回解析错误。'
    Assert ((File-Hash $targetPath) -eq $beforeInvalidHash) '非法 YAML 改变了磁盘原文件。'
    $cases += [ordered]@{ id = 'invalid-yaml-preserves-file'; passed = $true; status = $invalid.StatusCode }

    $invalidPath = Invoke-Json 'Post' "$origin/api/authoring/save-authoring" ([ordered]@{ path = '../outside.yaml'; content = $afterFirst.content; sourceHash = $afterFirstHash; revision = $afterFirstRevision })
    Assert ($invalidPath.StatusCode -eq 400 -and [string]$invalidPath.Body.error -eq 'WB-AUTHORING-SAVE-400') '非法高级保存路径没有返回请求错误。'
    $cases += [ordered]@{ id = 'invalid-path-request-error'; passed = $true; status = $invalidPath.StatusCode }

    $schemaContent = $afterFirst.content.Replace('domain: politics', 'domain: not_a_domain')
    $beforeSchemaHash = File-Hash $targetPath
    $schemaFailure = Invoke-Json 'Post' "$origin/api/authoring/save-authoring" ([ordered]@{ path = 'authoring/demo.yaml'; content = $schemaContent; sourceHash = $afterFirstHash; revision = $afterFirstRevision })
    Assert ($schemaFailure.StatusCode -eq 422 -and [string]$schemaFailure.Body.error -eq 'WB-AUTHORING-SCHEMA-422') '结构或分类错误没有返回 schema 错误。'
    Assert ((File-Hash $targetPath) -eq $beforeSchemaHash) '结构错误改变了磁盘原文件。'
    $cases += [ordered]@{ id = 'schema-failure-preserves-file'; passed = $true; status = $schemaFailure.StatusCode }

    $stale = Invoke-Json 'Post' "$origin/api/authoring/save-authoring" ([ordered]@{ path = 'authoring/demo.yaml'; content = $initialContent; sourceHash = $initialHash; revision = $initialRevision })
    Assert ($stale.StatusCode -eq 409 -and [string]$stale.Body.error -eq 'WB-AUTHORING-CAS-409') '过期 hash/revision 没有触发 CAS 冲突。'
    Assert ((File-Hash $targetPath) -eq $afterFirstHash) 'CAS 冲突覆盖了磁盘原文件。'
    $cases += [ordered]@{ id = 'stale-version-rejected'; passed = $true; status = $stale.StatusCode }

    $notSubmitted = Invoke-Json 'Post' "$origin/api/authoring/save-authoring/check" ([ordered]@{ path = 'authoring/demo.yaml'; content = $afterFirst.content; sourceHash = $afterFirstHash; revision = $afterFirstRevision })
    Assert ($notSubmitted.StatusCode -eq 200 -and [string]$notSubmitted.Body.status -eq 'not-submitted') '未提交保存检查状态不正确。'
    $cases += [ordered]@{ id = 'save-check-not-submitted'; passed = $true }

    $secondContent = $afterFirst.content.Replace('第一次更新', '第二次更新')
    $secondPayload = [ordered]@{ path = 'authoring/demo.yaml'; content = $secondContent; sourceHash = $afterFirstHash; revision = $afterFirstRevision }
    $secondSave = Invoke-Json 'Post' "$origin/api/authoring/save-authoring" $secondPayload
    Assert ($secondSave.StatusCode -eq 200 -and [int]$secondSave.Body.revision -eq 3) '第二次高级保存没有成功。'
    $confirmed = Invoke-Json 'Post' "$origin/api/authoring/save-authoring/check" $secondPayload
    Assert ($confirmed.StatusCode -eq 200 -and [string]$confirmed.Body.status -eq 'confirmed' -and $null -ne $confirmed.Body.editorDocument) '迟到保存结果没有被确认并返回作者投影。'
    $afterSecond = Read-Document $origin 'authoring/demo.yaml'
    $afterSecondHash = [string]$afterSecond.report.inputHash
    $afterSecondRevision = [int]$afterSecond.document.revision
    Assert ($afterSecondRevision -eq 3) '确认后的 revision 不正确。'
    $cases += [ordered]@{ id = 'save-check-confirmed'; passed = $true; revision = $afterSecondRevision }

    $externalContent = $afterSecond.content.Replace('第二次更新', '外部更新')
    [IO.File]::WriteAllText($targetPath, $externalContent, [Text.UTF8Encoding]::new($false))
    $conflict = Invoke-Json 'Post' "$origin/api/authoring/save-authoring/check" ([ordered]@{ path = 'authoring/demo.yaml'; content = $externalContent.Replace('外部更新', '第三次更新'); sourceHash = $afterSecondHash; revision = $afterSecondRevision })
    Assert ($conflict.StatusCode -eq 200 -and [string]$conflict.Body.status -eq 'conflict') '外部修改后的保存检查没有返回冲突。'
    $cases += [ordered]@{ id = 'save-check-conflict'; passed = $true }

    $missingVersion = Invoke-Json 'Post' "$origin/api/validate?path=authoring%2Fdemo.yaml" $null
    Assert ($missingVersion.StatusCode -eq 400 -and [string]$missingVersion.Body.error -eq 'WB-OP-VERSION-400') '操作缺少版本信息时没有被拒绝。'
    $staleOperation = Invoke-Json 'Post' "$origin/api/validate?path=authoring%2Fdemo.yaml&sourceHash=$([Uri]::EscapeDataString($afterFirstHash))&revision=2" $null
    Assert ($staleOperation.StatusCode -eq 409 -and [string]$staleOperation.Body.error -eq 'WB-OP-VERSION-409') '操作使用旧版本时没有被拒绝。'
    $cases += [ordered]@{ id = 'operation-version-gate'; passed = $true }

    $evidence.cases = @($cases)
    $evidence.passed = $true
}
catch {
    $evidence.error = $_.Exception.Message + " temp_root=$tempRoot"
    throw
}
finally {
    $evidence.finished_at_utc = [DateTime]::UtcNow.ToString('O')
    $evidencePathFull = if ([IO.Path]::IsPathRooted($EvidencePath)) { [IO.Path]::GetFullPath($EvidencePath) } else { [IO.Path]::GetFullPath((Join-Path $root $EvidencePath)) }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $evidencePathFull) | Out-Null
    $evidence | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $evidencePathFull -Encoding UTF8
    Stop-Child $webProcess
    if (-not $KeepTemp) { try { if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force } } catch { } }
}

$evidence | ConvertTo-Json -Depth 30
if (-not $evidence.passed) { exit 1 }
Write-Output "PASS: Authoring save HTTP smoke"
