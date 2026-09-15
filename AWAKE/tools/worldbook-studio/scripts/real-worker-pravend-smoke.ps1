param(
    [string]$Model = 'qwen2.5:latest',
    [string]$EvidencePath = '_tmp\pravend-real-worker-evidence.json',
    [string]$MappingRoot = '',
    [switch]$KeepWorkspace,
    [string]$SourceFile = '',
    [string]$CaseName = 'pravend'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$studioRoot = Split-Path -Parent (Split-Path -Parent $root)
if ([string]::IsNullOrWhiteSpace($MappingRoot)) { $MappingRoot = Join-Path $studioRoot 'docs\mappings\persona-entity' }
$schemaRoot = Join-Path $studioRoot 'docs\worldbook-studio-plan'
$cluster = Join-Path $root 'tests\fixtures\official-reference\pravend-cluster'
$extractPath = if ([string]::IsNullOrWhiteSpace($SourceFile)) { Join-Path $cluster 'sources\pravend-official-cns-extract.txt' } else { [IO.Path]::GetFullPath($SourceFile) }
$locatorName = Split-Path $extractPath -Leaf
$sourceText = Get-Content -LiteralPath $extractPath -Raw -Encoding UTF8
$evidenceFile = Join-Path $root $EvidencePath

function Select-FreePort { $l=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0);try{$l.Start();return ([Net.IPEndPoint]$l.LocalEndpoint).Port}finally{$l.Stop()} }
function Start-Child([string]$file,[string[]]$arguments,[hashtable]$environment,[string]$wd,[string]$so,[string]$se){
  $i=[Diagnostics.ProcessStartInfo]::new();$i.FileName=$file;$i.WorkingDirectory=$wd;$i.UseShellExecute=$false;$i.CreateNoWindow=$true;$i.RedirectStandardOutput=$true;$i.RedirectStandardError=$true
  $i.Arguments=(($arguments|ForEach-Object{'"'+$_.Trim('"').Replace('"','\"')+'"'}) -join ' ')
  foreach($e in $environment.GetEnumerator()){$i.Environment[$e.Key]=[string]$e.Value}
  $p=[Diagnostics.Process]::new();$p.StartInfo=$i;[void]$p.Start();$null=$p.StandardOutput.ReadToEndAsync();$null=$p.StandardError.ReadToEndAsync();return $p
}
function Invoke-Json([string]$method,[string]$uri,$session,[hashtable]$headers,$body){ (Invoke-WebRequest -Method $method -Uri $uri -WebSession $session -Headers $headers -ContentType 'application/json' -Body ($body|ConvertTo-Json -Compress -Depth 40) -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 300) }
function Sha([string]$t){$h=[Security.Cryptography.SHA256]::Create();try{([Convert]::ToHexString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($t)))).ToLowerInvariant()}finally{$h.Dispose()}}

$tmp=Join-Path ([IO.Path]::GetTempPath()) ('awake-pravend-'+[Guid]::NewGuid().ToString('N'))
$workspace=Join-Path $tmp 'workspace'; $workerScript=Join-Path $tmp 'worker.ps1'; $ready=Join-Path $tmp 'worker.ready'
$workerTrace=Join-Path $tmp 'worker.trace.log'; $webLog=Join-Path $tmp 'web.log'; $webErr=Join-Path $tmp 'web.error.log'
$secret='awake-pravend-'+[Guid]::NewGuid().ToString('N'); $workerPort=Select-FreePort; $webPort=Select-FreePort
$evidence=[ordered]@{ schema_version='awake.worldbook.case-real-worker.v1'; case=$CaseName; model=$Model; source_file=$extractPath; source_text_sha256=(Sha $sourceText); worker_execution='not_attempted'; candidate_count=$null; passed=$false; error=$null; candidates=$null; quote_audit=$null; document_created=$false; document_path=$null; document_status=$null; document_era=$null; document_entity_ids=$null; document_create_error=$null; workspace=$workspace }

$worker = @'
param([int]$Port,[string]$Secret,[string]$ReadyFile,[string]$Model,[string]$TraceFile,[string]$MappingRoot,[string]$CaseName,[string]$LocatorName)
$ErrorActionPreference='Stop'
$listener=[Net.HttpListener]::new();$listener.Prefixes.Add("http://127.0.0.1:$Port/");$listener.Start();[IO.File]::WriteAllText($ReadyFile,'ready')
$script:anchorMap=@{}
try{
  $ptr=Get-Content -LiteralPath (Join-Path $MappingRoot 'current-pointer.v1.json') -Raw -Encoding UTF8 | ConvertFrom-Json
  $genDir=Join-Path $MappingRoot ($ptr.generation_relative_path -replace '/','\')
  $reg=Get-Content -LiteralPath (Join-Path $genDir 'entity-registry.v1.json') -Raw -Encoding UTF8 | ConvertFrom-Json
  foreach($e in $reg.entities){
    if($e.kind -ne 'settlement'){ continue }
    if($e.display_name_zh){ $script:anchorMap[[string]$e.display_name_zh]=[string]$e.entity_id }
    foreach($al in @($e.aliases)){ if(-not [string]::IsNullOrWhiteSpace([string]$al)){ $script:anchorMap[[string]$al]=[string]$e.entity_id } }
  }
  [IO.File]::AppendAllText($TraceFile, "ANCHORS_LOADED`n" + $script:anchorMap.Count + "`n")
}catch{ [IO.File]::AppendAllText($TraceFile, "ANCHORS_FAILED`n" + $_.Exception.Message + "`n") }
$script:anchorNames=@($script:anchorMap.Keys | Sort-Object Length -Descending)
function B64([byte[]]$b){[Convert]::ToBase64String($b).Replace('+','-').Replace('/','_').TrimEnd('=')}
function Sha([string]$t){$h=[Security.Cryptography.SHA256]::Create();try{([Convert]::ToHexString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($t)))).ToLowerInvariant()}finally{$h.Dispose()}}
function Sig([string]$p,[string]$n,[string]$w,[long]$t,[string]$r){$x=[Text.Encoding]::UTF8.GetBytes(($p+'|'+$n+'|'+$w+'|'+$t.ToString([Globalization.CultureInfo]::InvariantCulture)+'|'+$r));$b=[byte[]]::new(4+$x.Length);$b[0]=[byte](($x.Length -shr 24)-band 255);$b[1]=[byte](($x.Length -shr 16)-band 255);$b[2]=[byte](($x.Length -shr 8)-band 255);$b[3]=[byte]($x.Length-band 255);[Array]::Copy($x,0,$b,4,$x.Length);$h=[Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($Secret));try{B64 $h.ComputeHash($b)}finally{$h.Dispose()}}
function Reply($c,[int]$s,$v){$j=$v|ConvertTo-Json -Compress -Depth 40;$b=[Text.Encoding]::UTF8.GetBytes($j);$c.Response.StatusCode=$s;$c.Response.ContentType='application/json';$c.Response.ContentLength64=$b.Length;$c.Response.OutputStream.Write($b,0,$b.Length);$c.Response.Close()}
$instruction='你是 AWAKE 世界书作者助手。只依据用户给出的官方中文摘录输出紧凑 JSON，格式严格为 {"sections":[{"sectionTitle":"主题标题","summary":"1-2 句中文摘要","domain":"politics|economy|culture|war|geography","subdomain":"该 domain 下的合法值","era":"historical|current","content":[{"kind":"fact|state|relation|interpretation","text":"用现代中文复述该事实","quote":"从摘录中逐字复制的支撑原句"}]}]}。最多 6 个 section，每个 section 1-3 条。硬性要求：摘录中的每一句都必须被至少一个 section 的 fact 覆盖；输出前逐句核对，绝不遗漏（现名、传承、结局类句子尤其不能漏）。每个 section 必须给出 summary 与 subdomain，不得留空。domain/subdomain 只能从以下选择：politics[throne,kingdoms,territories,offices,law,diplomacy,clans,succession]；economy[land_production,food,trade,taxation,currency,workshops,debt,trade_routes]；culture[faith,customs,language,identity,marriage,clothing,festivals,arts]；war[war_history,military_system,troops,weapons,tactics,fortifications,logistics,prisoners]；geography[terrain,climate,directions,rivers,roads,settlements,natural_boundaries,resources,sea_routes]。era：历史沿革用 historical，当今现状或现名用 current。kind：客观发生用 fact，状态或归属用 state，人物家族传承等关系用 relation，主观判断用 interpretation。text 必须是自己复述的话，不得与 quote 完全相同；quote 必须逐字来自摘录。禁止新增年份、人物、战争、正式实体 ID、现代术语。只输出 JSON，不要解释或 Markdown。'
try{ while($true){ $c=$listener.GetContext(); try{ $r=[IO.StreamReader]::new($c.Request.InputStream,[Text.Encoding]::UTF8);try{$body=$r.ReadToEnd()}finally{$r.Dispose()};$q=$body|ConvertFrom-Json
  if($c.Request.Url.AbsolutePath -eq '/awake/handshake'){$wid=('ollama-'+$CaseName+'-worker');$t=[long]$q.timestamp;Reply $c 200 @{protocol='awake.worker.v1';client_nonce=$q.client_nonce;worker_id=$wid;timestamp=$t;signature=(Sig 'awake.worker.v1' $q.client_nonce $wid $t $q.request_hash)};continue}
  if($c.Request.Url.AbsolutePath -ne '/awake/analyze'){Reply $c 404 @{error='not_found'};continue}
  $request=$q.request
  $ollama=@{model=$Model;stream=$false;format='json';options=@{temperature=0.1;num_predict=2200;num_ctx=4096};messages=@(@{role='system';content=$instruction},@{role='user';content=($request|ConvertTo-Json -Compress -Depth 40)})}|ConvertTo-Json -Compress -Depth 50
  $response=Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/chat' -Method Post -ContentType 'application/json' -Body $ollama -TimeoutSec 600
  $content=([string]$response.message.content).Trim(); if($content.StartsWith('```')){$content=$content -replace '^```(?:json)?\s*','' -replace '\s*```$',''}; [IO.File]::AppendAllText($TraceFile,"OLLAMA_CONTENT`n$content`n")
  $parsed=$content|ConvertFrom-Json
  $allow=@{politics=@('throne','kingdoms','territories','offices','law','diplomacy','clans','succession');economy=@('land_production','food','trade','taxation','currency','workshops','debt','trade_routes');culture=@('faith','customs','language','identity','marriage','clothing','festivals','arts');war=@('war_history','military_system','troops','weapons','tactics','fortifications','logistics','prisoners');geography=@('terrain','climate','directions','rivers','roads','settlements','natural_boundaries','resources','sea_routes')}
  $topics=@()
  $allowedEra=@('current','historical','before_event','after_event','persistent','unknown','custom')
  $subdomainFallback=@{politics='kingdoms';economy='trade';culture='customs';war='war_history';geography='settlements'}
  if($null -ne $parsed.sections -and @($parsed.sections).Count -gt 0){ foreach($s in @($parsed.sections)){ $st=[string]$s.sectionTitle;$sm=[string]$s.summary;$dm=[string]$s.domain;$sb=[string]$s.subdomain;$er=[string]$s.era;if(-not $allow.ContainsKey($dm)){$dm='geography'};if($allow[$dm] -notcontains $sb){$sb=[string]$subdomainFallback[$dm]};if($allowedEra -notcontains $er){$er='unknown'};$items=@();foreach($x in @($s.content)){ $items+=[pscustomobject]@{text=[string]$x.text;quote=[string]$x.quote;kind=[string]$x.kind} }; if(-not [string]::IsNullOrWhiteSpace($st)){ $topics+=[pscustomobject]@{title=$st;summary=$sm;domain=$dm;subdomain=$sb;era=$er;items=$items} } } }
  elseif($null -ne $parsed.response -and @($parsed.response).Count -gt 0){ foreach($s in @($parsed.response)){ $topics+=[pscustomobject]@{title=[string]$s.title;domain='geography';subdomain=$null;items=@([pscustomobject]@{text=[string]$s.content;quote=''})} } }
  $cands=@(); $i=0
  $sourceText=[string]$request.source_text
  if([string]::IsNullOrWhiteSpace($sourceText)){ $sourceText=[string]$request.sourceText }
  $sentences=@([regex]::Split($sourceText,'(?<=。)')|Where-Object{$_.Trim()})
  function BestSentence([string]$t){ $best='';$bestScore=-1; foreach($s in $sentences){ if(-not $s){continue}; $score=0; for($z=0;$z -lt ($t.Length-1);$z++){ $bg=$t.Substring($z,2); if($s.Contains($bg)){$score++} }; if($score -gt $bestScore){$bestScore=$score;$best=$s} }; return $best.Trim() }
  $allowedKinds=@('fact','state','relation','interpretation')
  $perspectives=@($request.perspectives|Where-Object{$_})
  foreach($tp in $topics){ if($i -ge 5){break}; $i++
    $facts=@(); $k=0
    foreach($it in @($tp.items)){ if([string]::IsNullOrWhiteSpace($it.text)){continue}; $k++
      $kind=if($allowedKinds -contains [string]$it.kind){[string]$it.kind}else{'fact'}
      $quote=[string]$it.quote
      if([string]::IsNullOrWhiteSpace($quote) -or -not $sourceText.Contains($quote)){ $quote=BestSentence ([string]$it.text) }
      if([string]::IsNullOrWhiteSpace($quote)){ continue }
      $factText=[string]$it.text
      if($factText.Trim() -eq $quote.Trim()){ $factText = '官方记载：' + $quote.Trim() }
      $facts+=@{id="fact.$CaseName.$i.$k";kind=$kind;text=$factText;certainty='confirmed';inferred=$false;evidence=@{reference_id="$CaseName.origin.$i.$k";locator=$LocatorName;quote=$quote;quote_hash=(Sha $quote)};review_status='pending'} }
    if($facts.Count -eq 0){continue}
    $exprs=@()
    if($perspectives.Count -gt 0){ foreach($p in $perspectives){ $exprs+=@{id="expr.$CaseName.$i";perspective=$p;layer='summary';text=$facts[0].text;profile_ids=@('profile.commoner');fact_ids=@($facts[0].id);inferred=$true;evidence=$null;review_status='pending'} } }
    $related=@(switch($tp.domain){ 'politics' {'geography'} 'geography' {'politics'} 'war' {'politics'} 'economy' {'geography'} 'culture' {'politics'} default {'politics'} })
    $anchorIds=@()
    foreach($fa in $facts){ $hay=([string]$fa.text + ' ' + [string]$fa.evidence.quote); foreach($nm in $script:anchorNames){ if($hay.Contains($nm)){ $anchorIds += $script:anchorMap[$nm] } } }
    $anchorIds=@($anchorIds | Select-Object -Unique)
    $summaryText=if([string]::IsNullOrWhiteSpace([string]$tp.summary)){ $facts[0].text } else { [string]$tp.summary }
    $cands+=@{id="candidate.$CaseName.$i";facts=$facts;metadata=@{title=$tp.title;summary=$summaryText;domain=$tp.domain;subdomain=$tp.subdomain;era=$tp.era;related_domains=$related;entity_ids=$anchorIds;note='由本机 Ollama 识别，仍需作者核验。'};expressions=$exprs;review_status='pending';segmentation_reason_codes=@('topic_boundary');source_spans=@();target_spans=@();propositions=@();claims=@();unresolved=@();coverage=$null}
  }
  $coveredQuotes=(@($cands|ForEach-Object{$_.facts}|ForEach-Object{[string]$_.evidence.quote}) -join '|')
  $warnList=@('候选由本机模型识别，证据待作者核验。')
  $sidx=0
  foreach($s in $sentences){ $sidx++; $t=$s.Trim(); if(-not $t){continue}; if(-not $coveredQuotes.Contains($t)){ $warnList+=('来源句未被候选覆盖：' + $t) } }
  $result=[ordered]@{schema_version='worldbook.authoring-draft.result.v1';stage='complete';request_hash=[string]$request.request_hash;source_content_hash=[string]$request.source_content_hash;review_only=$true;facts=@();metadata=$null;expressions=@();candidates=$cands;warnings=$warnList;unresolved=@();coverage=@{mode='quick_authoring';status='reported';heuristic=$true;not_semantic_migration_proof=$true;source_proposition_count=0};target_spans=@();propositions=@();claims=@()}
  Reply $c 200 $result
 } catch { try{[IO.File]::AppendAllText($TraceFile,"WORKER_ERROR`n$($_.Exception.ToString())`n");Reply $c 500 @{error='ollama_worker_failure';detail=$_.Exception.Message}}catch{} } } } finally {$listener.Stop();$listener.Close()}
'@

New-Item -ItemType Directory -Force -Path $tmp | Out-Null
[IO.File]::WriteAllText($workerScript,$worker,[Text.UTF8Encoding]::new($false))
New-Item -ItemType Directory -Force -Path (Join-Path $workspace 'authoring') | Out-Null
Copy-Item -Path (Join-Path $cluster 'authoring\*') -Destination (Join-Path $workspace 'authoring') -Recurse -Force
New-Item -ItemType Directory -Force -Path (Join-Path $workspace 'authoring\sources') | Out-Null
Copy-Item -Path (Join-Path $cluster 'sources\*') -Destination (Join-Path $workspace 'authoring\sources') -Force

$pwsh=(Get-Command pwsh -ErrorAction Stop).Source
$workerProcess=Start-Child $pwsh @('-NoProfile','-ExecutionPolicy','Bypass','-File',$workerScript,'-Port',([string]$workerPort),'-Secret',$secret,'-ReadyFile',$ready,'-Model',$Model,'-TraceFile',$workerTrace,'-MappingRoot',$MappingRoot,'-CaseName',$CaseName,'-LocatorName',$locatorName) @{} $tmp (Join-Path $tmp 'worker.log') (Join-Path $tmp 'worker.err')
for($i=0;$i -lt 120 -and -not(Test-Path $ready);$i++){Start-Sleep -Milliseconds 250}
if(-not (Test-Path $ready)){ throw 'WB-PRAVEND-002: worker did not start.' }
$envs=@{AWAKE_WB_DEV_MODE='1';WORLD_BOOK_WORKSPACE=$workspace;WORLD_BOOK_SCHEMA_ROOT=$schemaRoot;WORLD_BOOK_LOCAL_WORKER_URL="http://127.0.0.1:$workerPort/";WORLD_BOOK_LOCAL_WORKER_SECRET_ENV='AWAKE_PRAVEND_SECRET';AWAKE_PRAVEND_SECRET=$secret;AWAKE_WB_PORT=[string]$webPort;ASPNETCORE_ENVIRONMENT='Development';DOTNET_ENVIRONMENT='Development'}
$webProcess=Start-Child (Get-Command dotnet -ErrorAction Stop).Source @('run','--project',(Join-Path $root 'src\Awake.WorldbookStudio.Web\Awake.WorldbookStudio.Web.csproj'),'--configuration','Release','--no-build','--no-restore') $envs $root $webLog $webErr
$origin="http://127.0.0.1:$webPort"; $healthy=$false
for($i=0;$i -lt 120 -and -not $healthy;$i++){try{$healthy=(Invoke-WebRequest "$origin/api/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200}catch{Start-Sleep -Milliseconds 250}}
if(-not $healthy){ throw 'WB-PRAVEND-003: Studio did not become healthy.' }
try{
  $session=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
  $boot=Invoke-WebRequest "$origin/api/ai/session/bootstrap" -Method Post -UseBasicParsing -WebSession $session -Headers @{Origin=$origin}
  $b=$boot.Content|ConvertFrom-Json; $headers=@{Origin=$origin;'X-AWAKE-CSRF'=$b.csrfToken}
  $prepare=Invoke-Json 'Post' "$origin/api/ai/authoring/draft/prepare" $session $headers @{providerId='local';stage='complete';sourceName='帕拉汶德官方中文摘录';sourceNature='reference_material';sourceText=$sourceText;perspectives=@()}
  if($prepare.StatusCode -lt 200 -or $prepare.StatusCode -ge 300){ throw ("WB-PRAVEND-005: prepare failed " + $prepare.StatusCode + " " + $prepare.Content) }
  $ticket=$prepare.Content|ConvertFrom-Json
  $gen=Invoke-Json 'Post' "$origin/api/ai/authoring/draft/generate" $session $headers @{draftToken=$ticket.draftToken;attemptId=$ticket.attemptId}
  if($gen.StatusCode -lt 200 -or $gen.StatusCode -ge 300){ throw ("WB-PRAVEND-006: generate failed " + $gen.StatusCode + " " + $gen.Content) }
  $payload=$gen.Content|ConvertFrom-Json; $result=$payload.result
  $candidates=@($result.candidateSet.candidates)
  $evidence.worker_execution='executed_real_protocol_worker'; $evidence.candidate_count=$candidates.Count; $evidence.candidates=$candidates
  $audit=@()
  foreach($cd in $candidates){
    foreach($fa in @($cd.facts)){
      $q=[string]$fa.evidence.quote
      $audit+=[ordered]@{ candidate=[string]$cd.candidate_id; title=[string]$cd.metadata.title; fact=[string]$fa.text; quote=$q; quote_locatable=($sourceText.Contains($q)); quote_hash_ok=([string]$fa.evidence.quote_hash -eq (Sha $q)); review_status=[string]$fa.review_status }
    }
  }
  $evidence.quote_audit=$audit
  $joined=(@($candidates | ForEach-Object { @($_.facts) | ForEach-Object { [string]$_.evidence.quote } }) -join '|')
  if($CaseName -eq 'pravend'){
    $markers=[ordered]@{ 'founded-kaladios'='卡拉狄乌斯大帝'; 'capital-shalas'='沙拉斯'; 'western-economic'='西部的经济重镇'; 'osric-surrender'='协商让该城投降'; 'dey-tir-inheritance'='戴·提尔家族'; 'current-name-pravend'='称其为帕拉汶德' }
    $cov=[ordered]@{}; $hit=0; foreach($k in $markers.Keys){ $ok=$joined.Contains($markers[$k]); $cov[$k]=$ok; if($ok){$hit++} }
    $evidence.reference_coverage=[ordered]@{ covered=$hit; total=$markers.Count; detail=$cov }
  }
  $evidence.passed=($candidates.Count -ge 1 -and $result.reviewOnly -eq $true)
  if($candidates.Count -ge 1) {
    $chosen=$candidates[0]
    $acceptedFacts=@(); foreach($f in @($chosen.facts)){ $ev=$f.evidence; $acceptedFacts+=[ordered]@{ id=[string]$f.id; kind=[string]$f.kind; text=[string]$f.text; certainty=[string]$f.certainty; inferred=[bool]$f.inferred; evidence=[ordered]@{ referenceId=[string]$ev.reference_id; locator=[string]$ev.locator; quote=[string]$ev.quote; quoteHash=[string]$ev.quote_hash }; reviewStatus='accepted' } }
    $acceptedExpressions=@(); foreach($x in @($chosen.expressions)){ $acceptedExpressions+=[ordered]@{ id=[string]$x.id; perspective=[string]$x.perspective; layer=[string]$x.layer; text=[string]$x.text; profileIds=[array]@($x.profile_ids); factIds=[array]@($x.fact_ids); inferred=[bool]$x.inferred; evidence=$null; reviewStatus='accepted' } }
    $relatedDomains=@($chosen.metadata.related_domains)
    $createPayload=@{ draftId=$ticket.draftId; candidateId=$chosen.candidate_id; title=$chosen.metadata.title; summary=$chosen.metadata.summary; domain=$chosen.metadata.domain; subdomain=$chosen.metadata.subdomain; relatedDomains=[array]$relatedDomains; era=[string]$chosen.metadata.era; contentTier='base'; facts=$acceptedFacts; expressions=$acceptedExpressions }
    $create=Invoke-Json 'Post' "$origin/api/ai/authoring/draft/create-document" $session $headers $createPayload
    if($create.StatusCode -ge 200 -and $create.StatusCode -lt 300) {
      $created=$create.Content|ConvertFrom-Json
      $evidence.document_create_response=([string]$create.Content).Substring(0,[Math]::Min(800,([string]$create.Content).Length))
      $evidence.document_created=$true
      $evidence.document_path=[string]$created.document.path
      $read=Invoke-WebRequest -Uri ("$origin/api/editor-document?path=" + [Uri]::EscapeDataString([string]$created.document.path)) -UseBasicParsing -TimeoutSec 20
      $editorModel=($read.Content|ConvertFrom-Json).model
      $evidence.document_status=[string]$editorModel.status
      $evidence.document_era=[string]$editorModel.era.key
      $evidence.document_entity_ids=@($editorModel.advanced.entityIds)
      $docFsPath=Join-Path $workspace ([string]$created.document.path -replace '/','\')
      if(Test-Path -LiteralPath $docFsPath){ $docText=Get-Content -LiteralPath $docFsPath -Raw -Encoding UTF8; $evidence.document_yaml=$docText }
      $evidence.document_readback=([string]$read.Content).Substring(0,[Math]::Min(20000,([string]$read.Content).Length))
    } else {
      $evidence.document_create_error=([string]$create.StatusCode + ' ' + [string]$create.Content)
    }
  }
} catch { $evidence.error=$_.Exception.Message }
finally {
  $evidence | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $evidenceFile -Encoding UTF8
  try{ if(-not $webProcess.HasExited){$webProcess.Kill($true)} }catch{}
  try{ if(-not $workerProcess.HasExited){$workerProcess.Kill($true)} }catch{}
  if(-not $KeepWorkspace){ try{ Remove-Item -LiteralPath $workspace -Recurse -Force -ErrorAction SilentlyContinue }catch{} }
  Get-Content -LiteralPath $evidenceFile -Raw -Encoding UTF8
}
