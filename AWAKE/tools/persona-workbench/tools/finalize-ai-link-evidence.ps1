param(
    [Parameter(Mandatory = $true)][string]$WorkspaceRoot,
    [Parameter(Mandatory = $true)][string]$EvidencePath,
    [Parameter(Mandatory = $true)][string]$BaselineEvidencePath,
    [Parameter(Mandatory = $true)][string]$RequestCapturePath,
    [Parameter(Mandatory = $true)][string]$BaselineRequestCapturePath,
    [string]$BuildId,
    [string]$SourceHash,
    [string]$PackageHash,
    [string]$EvidenceHash,
    [long]$SessionFence = 0,
    [string]$ExpectedCurrentHash,
    [string]$CurrentPointerPath,
    [string]$SupersessionPath,
    [int]$WorkbenchPort = 51337
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($false)
function Full([string]$path) { [IO.Path]::GetFullPath($path) }
function Under([string]$path, [string]$root, [string]$label) { $full=Full $path; $prefix=(Full $root).TrimEnd('\','/')+'\'; if(-not $full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw "$label is outside WorkspaceRoot: $full"};$full }
function HashFile([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToUpperInvariant() }
function Rel([string]$path, [string]$base) { $path.Substring($base.Length).TrimStart('\','/').Replace('/','\') }
function WriteAtomic([string]$path, [string]$text) { $tmp="$path.$([guid]::NewGuid().ToString('N')).tmp"; [IO.File]::WriteAllText($tmp,$text,$utf8); try { if(Test-Path -LiteralPath $path -PathType Leaf){[IO.File]::Replace($tmp,$path,$null)}else{Move-Item -LiteralPath $tmp -Destination $path -Force}} finally {if(Test-Path -LiteralPath $tmp -PathType Leaf){Remove-Item -LiteralPath $tmp -Force}} }
$workspace=Full $WorkspaceRoot
$evidence=Under $EvidencePath $workspace 'EvidencePath';$baseline=Under $BaselineEvidencePath $workspace 'BaselineEvidencePath';$capture=Under $RequestCapturePath $workspace 'RequestCapturePath';$baselineCapture=Under $BaselineRequestCapturePath $workspace 'BaselineRequestCapturePath'
foreach($path in @($evidence,$baseline,$capture,$baselineCapture)){if(-not(Test-Path -LiteralPath $path -PathType Leaf)){throw "required input is missing: $path"}}
$evidenceJson=Get-Content -LiteralPath $evidence -Raw|ConvertFrom-Json -Depth 100;if($evidenceJson.verdicts.overall -ne 'PASS'){throw 'only complete PASS evidence may become current.'}
if(-not $BuildId){$BuildId=$evidenceJson.candidate.buildId};if(-not $SourceHash){$SourceHash=$evidenceJson.candidate.sourceManifestHash};if(-not $EvidenceHash){$EvidenceHash=HashFile $evidence};if($BuildId -notmatch '^PWB-[0-9]{8}-[0-9]{6}Z-[0-9A-F]{12}$'){throw 'BuildId is invalid.'};if($SourceHash -notmatch '^[0-9A-Fa-f]{64}$'){throw 'SourceHash must be SHA-256.'};if($PackageHash -and $PackageHash -notmatch '^[0-9A-Fa-f]{64}$'){throw 'PackageHash must be SHA-256.'};if($EvidenceHash -notmatch '^[0-9A-Fa-f]{64}$'){throw 'EvidenceHash must be SHA-256.'};if($SessionFence -lt 1){throw 'SessionFence is required for current pointer update.'};if([string]::IsNullOrWhiteSpace($ExpectedCurrentHash)){throw 'ExpectedCurrentHash is required for CAS pointer update.'}
$current=if([string]::IsNullOrWhiteSpace($CurrentPointerPath)){Join-Path $workspace 'artifacts\CURRENT-evidence.json'}else{Under $CurrentPointerPath $workspace 'CurrentPointerPath'};$sidecar=if([string]::IsNullOrWhiteSpace($SupersessionPath)){Join-Path (Split-Path $current -Parent) ('supersession-'+$BuildId+'.json')}else{Under $SupersessionPath $workspace 'SupersessionPath'};$lockPath=$current+'.lock';New-Item -ItemType Directory -Force -Path (Split-Path $current -Parent)|Out-Null
$lock=$null;try{
    $lock=[IO.File]::Open($lockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    $actual=if(Test-Path -LiteralPath $current -PathType Leaf){HashFile $current}else{'NONE'}
    $sidecarObject=[ordered]@{schemaVersion='persona-workbench.evidence-supersession.v1';status='superseded';supersededEvidenceHash=(HashFile $baseline);superseded_by=(Rel $evidence $workspace);newBuildId=$BuildId;newSourceHash=$SourceHash.ToUpperInvariant();newPackageHash=if($PackageHash){$PackageHash.ToUpperInvariant()}else{$null};newEvidenceHash=$EvidenceHash.ToUpperInvariant();sessionFence=$SessionFence;createdAtUtc=[DateTime]::UtcNow.ToString('o')}
    WriteAtomic $sidecar (($sidecarObject|ConvertTo-Json -Depth 20)+"`n")
    if($ExpectedCurrentHash -ne $actual){throw "EVIDENCE_POINTER_CONFLICT: expected $ExpectedCurrentHash but found $actual"}
    $pointer=[ordered]@{schemaVersion='persona-workbench.current-evidence.v1';status='PASS';evidencePath=(Rel $evidence $workspace);supersessionPath=(Rel $sidecar $workspace);evidenceHash=$EvidenceHash.ToUpperInvariant();sourceHash=$SourceHash.ToUpperInvariant();packageHash=if($PackageHash){$PackageHash.ToUpperInvariant()}else{$null};BuildId=$BuildId;expectedCurrentHash=$ExpectedCurrentHash;sessionFence=$SessionFence;updatedAtUtc=[DateTime]::UtcNow.ToString('o')}
    WriteAtomic $current (($pointer|ConvertTo-Json -Depth 20)+"`n")
}finally{if($null -ne $lock){$lock.Dispose()}}
Write-Output 'PASS persona-workbench evidence pointer updated';Write-Output "CurrentPointer=$current";Write-Output "Supersession=$sidecar";Write-Output "EvidenceHash=$EvidenceHash"

