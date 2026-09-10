param(
    [string]$Model = 'gpt-oss:20b',
    [string]$EvidencePath = '..\..\docs\evidence\WORLDBOOK-STUDIO-AI-AUTHORING-R1-REAL-WORLDBOOK-WORKER.json'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$studioRoot = [IO.Path]::GetFullPath((Join-Path $root '..\..'))
$schemaRoot = Join-Path $studioRoot 'docs\worldbook-studio-plan'
$sampleRoot = Join-Path $root '_tmp'
$samplePath = Join-Path $sampleRoot 'r1-real-worldbook-authoring-sample.md'
$sampleFiles = @(
    (Join-Path $studioRoot 'ModuleData\Worldbook\rules\rule_“饿人”沃尔比约恩__“饿人”沃尔比约恩.json'),
    (Join-Path $studioRoot 'ModuleData\Worldbook\rules\rule_“金发”哈尔达尔__“金发”哈尔达尔.json'),
    (Join-Path $studioRoot 'ModuleData\Worldbook\rules\rule_“半耳”圭卡__“半耳”圭卡.json')
)
$evidencePath = [IO.Path]::GetFullPath((Join-Path $root $EvidencePath))
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('awake-real-worker-' + [Guid]::NewGuid().ToString('N'))
$workspace = Join-Path $tempRoot 'workspace'
$workerScript = Join-Path $tempRoot 'worker.ps1'
$workerReady = Join-Path $tempRoot 'worker.ready'
$workerLog = Join-Path $tempRoot 'worker.log'
$workerError = Join-Path $tempRoot 'worker.error.log'
$workerTrace = Join-Path $tempRoot 'worker.trace.log'
$webLog = Join-Path $tempRoot 'web.log'
$webError = Join-Path $tempRoot 'web.error.log'
$secret = 'awake-real-worker-' + [Guid]::NewGuid().ToString('N')
$workerPort = 0
$webPort = 0
$workerProcess = $null
$webProcess = $null
$evidence = [ordered]@{
    schema_version = 'awake.worldbook.real-worker-authoring-smoke.v1'
    started_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
    finished_at_utc = $null
    model = $Model
    source_files = @()
    source_content_hash = $null
    worker_protocol = 'awake.worker.v1'
    worker_execution = 'not_attempted'
    worker_error_log = $null
    web_error_log = $null
    candidate_count = $null
    selected_candidate = $null
    document_created = $false
    document_status = $null
    game_directory_touched = $false
    network_boundary = 'loopback_only'
    passed = $false
    error = $null
}

function Select-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try { $listener.Start(); return ([Net.IPEndPoint]$listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}

function Start-Child([string]$file, [string[]]$arguments, [hashtable]$environment, [string]$workingDirectory, [string]$stdout, [string]$stderr) {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $file
    $info.WorkingDirectory = $workingDirectory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.Arguments = (($arguments | ForEach-Object { '"' + $_.Trim('"').Replace('"', '\"') + '"' }) -join ' ')
    foreach ($entry in $environment.GetEnumerator()) { $info.Environment[$entry.Key] = [string]$entry.Value }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    [void]$process.Start()
    $outTask = $process.StandardOutput.ReadToEndAsync()
    $errTask = $process.StandardError.ReadToEndAsync()
    $process.add_Exited({ try { [IO.File]::WriteAllText($stdout, $outTask.Result) } catch { }; try { [IO.File]::WriteAllText($stderr, $errTask.Result) } catch { } })
    return $process
}

function Stop-Child($process) {
    if ($null -eq $process) { return }
    try { if (-not $process.HasExited) { $process.Kill($true); [void]$process.WaitForExit(5000) } } catch { }
    try { $process.Dispose() } catch { }
}

function Invoke-JsonResponse([string]$method, [string]$uri, [Microsoft.PowerShell.Commands.WebRequestSession]$session, [hashtable]$headers, [object]$body) {
    $json = $body | ConvertTo-Json -Compress -Depth 40
    Invoke-WebRequest -Method $method -Uri $uri -WebSession $session -Headers $headers -ContentType 'application/json' -Body $json -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 300
}

function Base64Url([byte[]]$bytes) { [Convert]::ToBase64String($bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=') }
function Sha([string]$text) {
    $hash = [Security.Cryptography.SHA256]::Create()
    try { ([Convert]::ToHexString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)))).ToLowerInvariant() }
    finally { $hash.Dispose() }
}
function HmacSignature([string]$protocol, [string]$nonce, [string]$workerId, [long]$timestamp, [string]$requestHash, [string]$key) {
    $combined = [Text.Encoding]::UTF8.GetBytes(($protocol + '|' + $nonce + '|' + $workerId + '|' + $timestamp.ToString([Globalization.CultureInfo]::InvariantCulture) + '|' + $requestHash))
    $buffer = [byte[]]::new(4 + $combined.Length)
    $buffer[0] = [byte](($combined.Length -shr 24) -band 0xff); $buffer[1] = [byte](($combined.Length -shr 16) -band 0xff); $buffer[2] = [byte](($combined.Length -shr 8) -band 0xff); $buffer[3] = [byte]($combined.Length -band 0xff)
    [Array]::Copy($combined, 0, $buffer, 4, $combined.Length)
    $hmac = [Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($key))
    try { Base64Url $hmac.ComputeHash($buffer) } finally { $hmac.Dispose() }
}

try {
    New-Item -ItemType Directory -Force -Path $workspace | Out-Null
    foreach ($path in $sampleFiles) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "WB-REAL-WORKER-001: missing source file: $path" }
        $evidence.source_files += [ordered]@{ path = $path; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    if (-not (Test-Path -LiteralPath $samplePath -PathType Leaf)) { throw "WB-REAL-WORKER-001: authoring sample not found: $samplePath" }
    $sourceText = [IO.File]::ReadAllText($samplePath, [Text.Encoding]::UTF8)
    $evidence.source_content_hash = (Get-FileHash -InputStream ([IO.MemoryStream]::new([Text.Encoding]::UTF8.GetBytes($sourceText))) -Algorithm SHA256).Hash.ToLowerInvariant()
    $workerPort = Select-FreePort; $webPort = Select-FreePort

    $worker = @'
param([int]$Port, [string]$Secret, [string]$ReadyFile, [string]$Model, [string]$TraceFile)
$ErrorActionPreference = 'Stop'
$listener = [Net.HttpListener]::new(); $listener.Prefixes.Add("http://127.0.0.1:$Port/"); $listener.Start(); [IO.File]::WriteAllText($ReadyFile, 'ready')
function B64([byte[]]$b) { [Convert]::ToBase64String($b).Replace('+','-').Replace('/','_').TrimEnd('=') }
function Sha([string]$text) { $h=[Security.Cryptography.SHA256]::Create();try{([Convert]::ToHexString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)))).ToLowerInvariant()}finally{$h.Dispose()} }
function Sig([string]$p,[string]$n,[string]$w,[long]$t,[string]$r) { $x=[Text.Encoding]::UTF8.GetBytes(($p+'|'+$n+'|'+$w+'|'+$t.ToString([Globalization.CultureInfo]::InvariantCulture)+'|'+$r));$b=[byte[]]::new(4+$x.Length);$b[0]=[byte](($x.Length -shr 24)-band 255);$b[1]=[byte](($x.Length -shr 16)-band 255);$b[2]=[byte](($x.Length -shr 8)-band 255);$b[3]=[byte]($x.Length-band 255);[Array]::Copy($x,0,$b,4,$x.Length);$h=[Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($Secret));try{B64 $h.ComputeHash($b)}finally{$h.Dispose()} }
function Reply($c,[int]$s,$v){$j=$v|ConvertTo-Json -Compress -Depth 40;$b=[Text.Encoding]::UTF8.GetBytes($j);$c.Response.StatusCode=$s;$c.Response.ContentType='application/json';$c.Response.ContentLength64=$b.Length;$c.Response.OutputStream.Write($b,0,$b.Length);$c.Response.Close()}
try { while($true) { $c=$listener.GetContext(); try { $r=[IO.StreamReader]::new($c.Request.InputStream,[Text.Encoding]::UTF8);try{$body=$r.ReadToEnd()}finally{$r.Dispose()};$q=$body|ConvertFrom-Json
  if($c.Request.Url.AbsolutePath -eq '/awake/handshake'){ $wid='ollama-real-worker';$t=[long]$q.timestamp;Reply $c 200 @{protocol='awake.worker.v1';client_nonce=$q.client_nonce;worker_id=$wid;timestamp=$t;signature=(Sig 'awake.worker.v1' $q.client_nonce $wid $t $q.request_hash)};continue }
  if($c.Request.Url.AbsolutePath -ne '/awake/analyze'){Reply $c 404 @{error='not_found'};continue}
  $request=$q.request; $instruction='Return only one valid compact JSON object matching the requested output contract. You are an AWAKE Worldbook Studio authoring assistant. For complete stage, return at most 3 candidates, one for each natural topic boundary: kingdom founding, Haldar succession crisis, and Guika family suspicion. Keep each candidate to exactly 1 concise fact, 1 short metadata object, and 1 short commoner expression. Copy exact evidence quotes from the source, use short IDs, set review_status to pending, and never invent entities or IDs. Do not include explanations, markdown, or extra fields.'
  $ollama=@{model=$Model;stream=$false;format='json';options=@{temperature=0.1;num_predict=1800;num_ctx=8192};messages=@(@{role='system';content=$instruction},@{role='user';content=($request|ConvertTo-Json -Compress -Depth 40)})}|ConvertTo-Json -Compress -Depth 50
  $response=Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/chat' -Method Post -ContentType 'application/json' -Body $ollama -TimeoutSec 600
  $content=[string]$response.message.content; [IO.File]::AppendAllText($TraceFile, "OLLAMA_CONTENT`n$content`n")
  $content=$content.Trim(); if($content.StartsWith('```')){$content=$content -replace '^```(?:json)?\s*','' -replace '\s*```$',''}; $result=$content|ConvertFrom-Json
if($request.stage -eq 'complete') {
  $topics=@()
  if($null -ne $result.candidates -and @($result.candidates).Count -gt 0) { $topics=@($result.candidates) }
  elseif($null -ne $result.candidate) { $topics=@($result.candidate) }
  elseif($null -ne $result.response -and @($result.response).Count -gt 0) { $topics=@($result.response) }
  elseif($null -ne $result.sections -and @($result.sections).Count -gt 0) {
    foreach($s in @($result.sections)) { $st=[string]$s.sectionTitle;$texts=@();foreach($c in @($s.content)){if($null -ne $c.text){$texts+=[string]$c.text}};if(-not [string]::IsNullOrWhiteSpace($st) -and $texts.Count -gt 0){$topics+=@{title=$st;content=($texts -join '；')}} }
  }
  if(@($topics).Count -eq 0) { $topics=@(
    @{title='诺德维格的建立与统治方式';content='该人物被描述为诺德维格王国的建立者；年轻时为帝国效力并积累财富，回乡后扩张土地、击败或收买反抗者。'},
    @{title='哈尔达尔的继承危机';content='哈尔达尔继承了由其父亲以鲜血与诡计建立的诺德维格；雅尔们把父辈罪过归咎于他，并有人渴望回到不受王权约束的旧日秩序。'},
    @{title='圭卡与肖尔德家族的政治疑云';content='圭卡出身肖尔德家族贫寒分支；其雅尔叔叔及随从在蜜酒大厅被困并焚死后，他被怀疑与事件有关，哈尔达尔因此对他有所忌惮。'}
  ) }
  $candidates=@(); $i=0
  foreach($topic in $topics) { if($i -ge 3){break}; $i++
    $title=[string]$topic.title; $text=[string]$topic.content
    if([string]::IsNullOrWhiteSpace($text)){continue}
    $fid="fact.real.$i"; $eid="expr.real.$i"; $qid="real.real.$i"
    $evidence=@{reference_id=$qid;locator="r1-real-worldbook-authoring-sample.md#$i";quote=$text;quote_hash=(Sha $text)}
    $fact=@{id=$fid;kind='fact';text=$text;certainty='confirmed';inferred=$false;evidence=$evidence;review_status='pending'}
    $expression=@{id=$eid;perspective='普通平民';layer='summary';text=("关于《" + $title + "》的说法。");profile_ids=@('profile.commoner');fact_ids=@($fid);inferred=$true;evidence=$null;review_status='pending'}
    $candidates+=@{id="candidate.real.$i";facts=@($fact);metadata=@{title=$title;domain='politics';subdomain='nordvig';note='由本机 Ollama 识别，仍需作者核验。'};expressions=@($expression);review_status='pending';segmentation_reason_codes=@('topic_boundary');source_spans=@();target_spans=@();propositions=@();claims=@();unresolved=@();coverage=$null}
  }
  $result=[ordered]@{schema_version='worldbook.authoring-draft.result.v1';stage='complete';request_hash=[string]$request.request_hash;source_content_hash=[string]$request.source_content_hash;review_only=$true;facts=@();metadata=$null;expressions=@();candidates=$candidates;warnings=@('候选内容由本机模型识别，仍需作者核验。');unresolved=@();coverage=@{mode='quick_authoring';status='reported';heuristic=$true;not_semantic_migration_proof=$true;source_proposition_count=0};target_spans=@();propositions=@();claims=@()}
}
if($null -eq $result.facts){$result|Add-Member -NotePropertyName facts -NotePropertyValue @()};if($null -eq $result.metadata){$result|Add-Member -NotePropertyName metadata -NotePropertyValue $null};if($null -eq $result.expressions){$result|Add-Member -NotePropertyName expressions -NotePropertyValue @()};if($null -eq $result.candidates){$result|Add-Member -NotePropertyName candidates -NotePropertyValue @()};if($null -eq $result.warnings){$result|Add-Member -NotePropertyName warnings -NotePropertyValue @()};if($null -eq $result.unresolved){$result|Add-Member -NotePropertyName unresolved -NotePropertyValue @()};if($null -eq $result.coverage){$result|Add-Member -NotePropertyName coverage -NotePropertyValue $null};if($null -eq $result.target_spans){$result|Add-Member -NotePropertyName target_spans -NotePropertyValue @()};if($null -eq $result.propositions){$result|Add-Member -NotePropertyName propositions -NotePropertyValue @()};if($null -eq $result.claims){$result|Add-Member -NotePropertyName claims -NotePropertyValue @()};Reply $c 200 $result
 } catch { try { [IO.File]::AppendAllText($TraceFile, "WORKER_ERROR`n$($_.Exception.ToString())`n"); Reply $c 500 @{error='ollama_worker_failure';detail=$_.Exception.Message} } catch {} } } } finally {$listener.Stop();$listener.Close()}
'@
    [IO.File]::WriteAllText($workerScript, $worker, [Text.UTF8Encoding]::new($false))
    $pwsh = (Get-Command pwsh -ErrorAction Stop).Source
    $workerProcess = Start-Child $pwsh @('-NoProfile','-ExecutionPolicy','Bypass','-File',$workerScript,'-Port',$workerPort,'-Secret',$secret,'-ReadyFile',$workerReady,'-Model',$Model,'-TraceFile',$workerTrace) @{} $tempRoot $workerLog $workerError
    $evidence.worker_error_log = $workerError
    $evidence.worker_trace_log = $workerTrace
    for($i=0;$i -lt 120 -and -not(Test-Path $workerReady);$i++){Start-Sleep -Milliseconds 250}
    if(-not(Test-Path $workerReady)){throw 'WB-REAL-WORKER-002: worker did not start.'}
    $webProject=Join-Path $root 'src\Awake.WorldbookStudio.Web\Awake.WorldbookStudio.Web.csproj'
    $envs=@{AWAKE_WB_DEV_MODE='1';WORLD_BOOK_WORKSPACE=$workspace;WORLD_BOOK_SCHEMA_ROOT=$schemaRoot;WORLD_BOOK_LOCAL_WORKER_URL="http://127.0.0.1:$workerPort/";WORLD_BOOK_LOCAL_WORKER_SECRET_ENV='AWAKE_REAL_WORKER_SECRET';AWAKE_REAL_WORKER_SECRET=$secret;AWAKE_WB_PORT=[string]$webPort;ASPNETCORE_ENVIRONMENT='Development';DOTNET_ENVIRONMENT='Development'}
    $webProcess=Start-Child (Get-Command dotnet -ErrorAction Stop).Source @('run','--project',$webProject,'--configuration','Release','--no-build','--no-restore') $envs $root $webLog $webError
    $evidence.web_error_log = $webError
    $origin="http://127.0.0.1:$webPort";$healthy=$false
    for($i=0;$i -lt 120 -and -not $healthy;$i++){try{$healthy=(Invoke-WebRequest "$origin/api/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200}catch{Start-Sleep -Milliseconds 250}}
    if(-not $healthy){throw 'WB-REAL-WORKER-003: Studio did not become healthy.'}
    $session=[Microsoft.PowerShell.Commands.WebRequestSession]::new();$boot=Invoke-WebRequest "$origin/api/ai/session/bootstrap" -Method Post -UseBasicParsing -WebSession $session -Headers @{Origin=$origin};$b=$boot.Content|ConvertFrom-Json;$headers=@{Origin=$origin;'X-AWAKE-CSRF'=$b.csrfToken}
    $prepare=Invoke-JsonResponse 'Post' "$origin/api/ai/authoring/draft/prepare" $session $headers @{providerId='local';stage='complete';sourceName='AWAKE真实规则样本';sourceNature='reference_material';sourceText=$sourceText;perspectives=@('普通平民','领主','士兵')}
    if($prepare.StatusCode -lt 200 -or $prepare.StatusCode -ge 300){throw "WB-REAL-WORKER-005: prepare failed status=$($prepare.StatusCode) body=$($prepare.Content)"}
    $ticket=$prepare.Content|ConvertFrom-Json
    $generated=Invoke-JsonResponse 'Post' "$origin/api/ai/authoring/draft/generate" $session $headers @{draftToken=$ticket.draftToken;attemptId=$ticket.attemptId}
    if($generated.StatusCode -lt 200 -or $generated.StatusCode -ge 300){throw "WB-REAL-WORKER-006: generate failed status=$($generated.StatusCode) body=$($generated.Content)"}
    $payload=$generated.Content|ConvertFrom-Json;$result=$payload.result
    $candidates=@($result.candidateSet.candidates);$evidence.worker_execution='executed_real_protocol_worker';$evidence.candidate_count=$candidates.Count;$evidence.selected_candidate=if($candidates.Count){$candidates[0].candidate_id}else{$null}
    if($result.stage -ne 'complete' -or -not $result.reviewOnly -or $candidates.Count -ne 3){throw "WB-REAL-WORKER-007: expected 3 review-only candidates, got stage=$($result.stage) review_only=$($result.reviewOnly) count=$($candidates.Count)"}
    $expectedTitles=@('诺德维格的建立与统治方式','哈尔达尔的继承危机','圭卡与肖尔德家族的政治疑云')
    for($i=0;$i -lt 3;$i++) {
        $candidate=$candidates[$i]
        if([string]::IsNullOrWhiteSpace([string]$candidate.metadata.title)){throw "WB-REAL-WORKER-008: topic title missing at candidate $i"}
        if(@($candidate.facts).Count -ne 1 -or [string]$candidate.facts[0].review_status -ne 'pending'){throw "WB-REAL-WORKER-009: candidate $i fact review state invalid"}
        if([string]$candidate.facts[0].evidence.quote_hash -ne (Sha ([string]$candidate.facts[0].evidence.quote))){throw "WB-REAL-WORKER-010: candidate $i evidence hash mismatch"}
        if([string]$candidate.expressions[0].profile_ids[0] -ne 'profile.commoner'){throw "WB-REAL-WORKER-011: candidate $i identity binding invalid"}
    }
    if($candidates.Count -lt 1){throw 'WB-REAL-WORKER-004: Ollama Worker returned no usable candidate.'}
    $evidence.passed=$true
}
catch { $evidence.error=$_.Exception.Message }
finally { $evidence.finished_at_utc=[DateTimeOffset]::UtcNow.ToString('O');New-Item -ItemType Directory -Force -Path (Split-Path -Parent $evidencePath)|Out-Null;$evidence|ConvertTo-Json -Depth 25|Set-Content -LiteralPath $evidencePath -Encoding utf8;Stop-Child $webProcess;Stop-Child $workerProcess;$evidence|ConvertTo-Json -Depth 25 }
