$f = "c:\Users\26811\.trae-cn\memory\projects\-d-AWAKE-Dev--p2-41b365bca236a5dc1917\20260912\session_memory_6aa3e54b86780ef207e9a38b.jsonl"
$lines = Get-Content -LiteralPath $f -Encoding utf8
$i = 0
foreach($ln in $lines){
  $i++
  if($ln -notmatch "人气|阿尔瓦|斯瓦娜|阿巴该|西加|拉盖娅|伊拉|官方中文|率" ){ continue }
  try {
    $j = $ln | ConvertFrom-Json
    $t = if($j.message_summary_time){$j.message_summary_time}else{"n/a"}
    Write-Host ("== L"+$i+"  "+$t+"  msg="+$j.message_id)
    if($j.intent){ Write-Host ("  intent: "+$j.intent) }
    if($j.outcome){ Write-Host ("  outcome: "+$j.outcome) }
    if($j.learned){ $j.learned | ForEach-Object { Write-Host ("    - "+$_) } }
  } catch { }
}