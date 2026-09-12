# W2 落库驱动：构建 Web 宿主 → 启动（真实工作区）→ 取登记哈希 → 逐档建档+保存链+校验 → 证据
$ErrorActionPreference = 'Stop'
$Root     = 'D:\AWAKE-Dev\AWAKE\tools\worldbook-studio'
$WebProj  = Join-Path $Root 'src\Awake.WorldbookStudio.Web\Awake.WorldbookStudio.Web.csproj'
$Workspace= Join-Path $Root 'workspace'
$SchemaRoot='D:\AWAKE-Dev\AWAKE\docs\worldbook-studio-plan'
$YamlDir  = 'D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\authoring-out'
$Evidence = 'D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\projection-evidence.json'
$Port     = 5099
$Origin   = "http://127.0.0.1:$Port"

Write-Host '[0/5] kill stale host (holds build-output file locks)...'
$old = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
if ($old) { $old | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }; Start-Sleep -Seconds 1; Write-Host 'killed stale listener' }

Write-Host '[1/5] build web...'
dotnet build $WebProj -c Release --nologo -v q 2>&1 | Select-Object -Last 3

Write-Host '[2/5] start host...'
$env:AWAKE_WB_DEV_MODE='1'; $env:WORLD_BOOK_WORKSPACE=$Workspace; $env:WORLD_BOOK_SCHEMA_ROOT=$SchemaRoot
$env:AWAKE_WB_PORT=[string]$Port; $env:ASPNETCORE_ENVIRONMENT='Development'; $env:DOTNET_ENVIRONMENT='Development'
$web = Start-Process -FilePath 'dotnet' -ArgumentList @('run','--project',$WebProj,'--configuration','Release','--no-build','--no-restore') -WorkingDirectory $Root -PassThru -WindowStyle Hidden -RedirectStandardOutput "$env:TEMP\wb-web-out.log" -RedirectStandardError "$env:TEMP\wb-web-err.log"
$healthy = $false
for ($i=0; $i -lt 120 -and -not $healthy; $i++) {
    try { $healthy = (Invoke-WebRequest -Uri "$Origin/api/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { Start-Sleep -Milliseconds 500 }
}
if (-not $healthy) { Write-Host (Get-Content "$env:TEMP\wb-web-err.log" -Tail 20); throw 'host not healthy' }
Write-Host 'host healthy'

function Invoke-Json([string]$Method,[string]$Uri,[object]$Body) {
    $pr = @{ Method=$Method; Uri=$Uri; UseBasicParsing=$true; TimeoutSec=30 }
    if ($null -ne $Body) { $pr.ContentType='application/json'; $pr.Body=[Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Compress -Depth 30)) }
    try { $r = Invoke-WebRequest @pr; return @{ code=[int]$r.StatusCode; body=($r.Content | ConvertFrom-Json) } }
    catch { $er=$_.Exception.Response
        if ($null -eq $er) { throw }
        $raw = ''
        try { $stream = $er.GetResponseStream(); if ($stream) { $reader = New-Object IO.StreamReader($stream); $raw = $reader.ReadToEnd() } } catch {}
        if ([string]::IsNullOrWhiteSpace($raw)) { $raw = $_.ErrorDetails.Message }
        Write-Host "    HTTP $($_.Exception.Response.StatusCode.value__): $raw"
        $parsed = $null
        try { $parsed = $raw | ConvertFrom-Json } catch {}
        if ($null -eq $parsed) { $parsed = @{ raw = $raw } }
        return @{ code=[int]$er.StatusCode; body=$parsed } }
}

Write-Host '[3/5] catalog hashes...'
$cat = (Invoke-WebRequest -Uri "$Origin/api/editor-catalog" -UseBasicParsing -TimeoutSec 30).Content | ConvertFrom-Json
$pHash = $cat.profileRegistryHash; $pVer = $cat.profileRegistryVersion
$rHash = $cat.referralRegistryHash; $rVer = $cat.referralRegistryVersion
Write-Host ("profile={0}/{1} referral={2}/{3}" -f $pVer,$pHash.Substring(0,8),$rVer,$rHash.Substring(0,8))

$results = @()
Write-Host '[4/5] project 12 documents...'
Get-ChildItem "$YamlDir\*.yaml" | Sort-Object Name | ForEach-Object {
    $key = $_.BaseName
    try {
        $text = [IO.File]::ReadAllText($_.FullName)
        $text = $text -replace 'profile_registry_version: [^\r\n]+', "profile_registry_version: $pVer"
        $text = $text -replace 'profile_registry_hash: [^\r\n]+',   "profile_registry_hash: $pHash"
        $text = $text -replace 'referral_registry_version: [^\r\n]+',"referral_registry_version: $rVer"
        $text = $text -replace 'referral_registry_hash: [^\r\n]+',  "referral_registry_hash: $rHash"
        $rel = "authoring/$key.yaml"
        [IO.File]::WriteAllText((Join-Path $Workspace $rel.Replace('/','\')), $text, (New-Object Text.UTF8Encoding $false))

        $doc = (Invoke-WebRequest -Uri "$Origin/api/document?path=$([Uri]::EscapeDataString($rel))" -UseBasicParsing -TimeoutSec 30).Content | ConvertFrom-Json
        $hash = [string]$doc.report.inputHash; $rev = [int]$doc.document.revision

        $saved = Invoke-Json 'Post' "$Origin/api/authoring/save-authoring" @{ path=$rel; content=$text; sourceHash=$hash; revision=$rev }
        $saveOk = ($saved.code -eq 200 -and $saved.body.ok -eq $true)

        $newHash = [string]$saved.body.sourceHash; $newRev = [int]$saved.body.revision
        $val = Invoke-Json 'Post' "$Origin/api/validate?path=$([Uri]::EscapeDataString($rel))&sourceHash=$newHash&revision=$newRev" $null
        $valid = ($val.code -eq 200 -and ($val.body.valid -eq $true -or $val.body.ok -eq $true))
        $diag = @()
        if ($val.body.diagnostics) { $diag = @($val.body.diagnostics | ForEach-Object { "$($_.code):$($_.message)" }) }
        if ($val.body.results) { $diag += @($val.body.results | Where-Object { $_.severity -eq 'error' } | ForEach-Object { "$($_.code):$($_.message)" }) }

        $results += [ordered]@{ doc=$key; placed=($null -ne $doc); save_ok=$saveOk; save_error=$saved.body.error; validate_ok=$valid; diagnostics=$diag; revision=[int]$saved.body.revision }
        Write-Host ("  {0}: save={1} validate={2} diag={3}" -f $key, $saveOk, $valid, ($diag -join ' | '))
    } catch {
        $results += [ordered]@{ doc=$key; error=$_.Exception.Message }
        Write-Host ("  {0}: EXC {1}" -f $key, $_.Exception.Message)
    }
}

Write-Host '[5/5] evidence...'
$ev = [ordered]@{
    schema_version='awake.worldbook.authoring-projection.v1'
    date='2026-09-12'; network_boundary='loopback_only'; game_directory_touched=$false
    profile_registry=@{version=$pVer;hash=$pHash}; referral_registry=@{version=$rVer;hash=$rHash}
    documents=$results
    passed=(@($results | Where-Object { $_.save_ok -and $_.validate_ok }).Count -eq 12)
}
[IO.File]::WriteAllText($Evidence, ($ev | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding $true))
try { if (-not $web.HasExited) { $web.Kill($true) } } catch {}
$passedCount = @($results | Where-Object { $_.save_ok -and $_.validate_ok }).Count
Write-Host "DONE: $passedCount/12 save+validate ok"
