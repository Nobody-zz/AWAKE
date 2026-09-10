param(
    [Parameter(Mandatory = $true)][string]$AwakeRoot,
    [Parameter(Mandatory = $true)][string]$ReceiptPath,
    [string]$HandoffPath,
    [string]$DocumentPath,
    [string]$ExpectedSessionId,
    [string]$ExpectedIssuerId,
    [long]$ExpectedFence = 0,
    [datetime]$NowUtc = [datetime]::UtcNow
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Fail([string]$message) { throw "AUTHORING_HANDOFF_INVALID: $message" }
function InputFail([string]$message) { throw "AUTHORING_HANDOFF_INPUT_INVALID: $message" }
function Full([string]$path) { try { [IO.Path]::GetFullPath($path) } catch { InputFail "invalid path: $path" } }
function Require([object]$object,[string]$name) { if($null -eq $object){Fail "missing $name"}; $object }
function Sha([string]$path) { if(-not(Test-Path -LiteralPath $path -PathType Leaf)){InputFail "missing file: $path"}; (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToUpperInvariant() }
function ShaText([string]$value) { $bytes=[Text.UTF8Encoding]::new($false).GetBytes($value); $sha=[Security.Cryptography.SHA256]::Create(); try { ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','').ToUpperInvariant() } finally { $sha.Dispose() } }
function ShaTextLower([string]$value) { (ShaText $value).ToLowerInvariant() }
function UtcStamp([object]$value) { ([datetime]$value).ToUniversalTime().ToString('o', [Globalization.CultureInfo]::InvariantCulture) }
function IsHash([object]$value) { $value -is [string] -and $value.Length -eq 64 -and $value -match '^[0-9A-Fa-f]{64}$' }
function CheckFingerprint([object]$value,[string]$name,[int]$revision,[string]$hash) { if($null -eq $value -or $value.revision -ne $revision -or -not(IsHash $value.sha256) -or $value.sha256.ToUpperInvariant() -ne $hash){Fail "$name revision/hash is not bound to current contract input."} }
function CheckCommon([object]$value,[string]$schema,[string]$kind) {
    if($value.schemaVersion -ne $schema){Fail "$kind schemaVersion is invalid."}; if($value.awakeApproval -ne 'not_requested'){Fail "$kind awakeApproval must be not_requested."}
    $issuer=Require $value.issuer "$kind issuer"; $session=Require $value.session "$kind session"; if($session.authorization -ne 'session_csrf'){Fail "$kind session authorization is invalid."}
    if($ExpectedSessionId -and ($session.sessionId -ne $ExpectedSessionId -or $issuer.sessionId -ne $ExpectedSessionId)){Fail "$kind session binding mismatch."}; if($ExpectedIssuerId -and $issuer.issuerId -ne $ExpectedIssuerId){Fail "$kind issuer mismatch."}; if($ExpectedFence -gt 0 -and $value.fence -ne $ExpectedFence){Fail "$kind fence mismatch."}
    if($value.fence -lt 1 -or $value.issuedAtUtc -gt $value.expiresAtUtc -or [datetime]$value.expiresAtUtc -le $NowUtc){Fail "$kind is expired or has invalid time bounds."}
    foreach($field in 'source','authoring','crosswalk','registry'){if($null -eq $value.$field){Fail "$kind missing $field fingerprint."}}
}
function CheckEnvelope([object]$handoff,[object]$receipt) {
    $envelope=Require $handoff.envelope 'handoff envelope'
    if($envelope.schema_version -ne 'awake.workstation.handoff-envelope.v1'){Fail 'handoff envelope schema_version is invalid.'}
    if($envelope.handoff_id -notmatch '^pwb-handoff-[a-f0-9]{32}$'){Fail 'handoff envelope handoff_id is invalid.'}
    if($envelope.workspace_id -notmatch '^[a-z0-9][a-z0-9._-]{2,127}$'){Fail 'handoff envelope workspace_id is invalid.'}
    if($envelope.document_id -ne $handoff.documentId){Fail 'handoff envelope document_id mismatch.'}
    if($envelope.revision -lt 1){Fail 'handoff envelope revision is invalid.'}
    if(-not($envelope.content_sha256 -cmatch '^[a-f0-9]{64}$') -or $envelope.content_sha256 -ne $handoff.contentSha256.ToLowerInvariant()){Fail 'handoff envelope content_sha256 mismatch.'}
    if($envelope.producer -ne 'persona_workbench'){Fail 'handoff envelope producer is invalid.'}
    if($envelope.review_only -ne $true){Fail 'handoff envelope review_only must be true.'}
    if($envelope.review_status -ne 'approved_local'){Fail 'handoff envelope review_status is invalid.'}
    if($envelope.handoff_id -ne $handoff.handoffId){Fail 'handoff envelope handoff_id mismatch.'}
    if((UtcStamp $envelope.issued_at_utc) -ne (UtcStamp $handoff.issuedAtUtc)){Fail 'handoff envelope issued_at_utc mismatch.'}
    if((UtcStamp $envelope.expires_at_utc) -ne (UtcStamp $handoff.expiresAtUtc)){Fail 'handoff envelope expires_at_utc mismatch.'}
    if($envelope.payload -ne $handoff.canonicalJson){Fail 'handoff envelope payload mismatch.'}
    if(-not($envelope.request_fingerprint -cmatch '^[a-f0-9]{64}$')){Fail 'handoff envelope request_fingerprint is invalid.'}
    $provenance=Require $envelope.provenance 'handoff envelope provenance'
    if($provenance.source_schema -ne 'persona-workbench.approval-receipt.v1' -or $provenance.source_id -ne $receipt.receiptId -or $provenance.source_revision -lt 1 -or $provenance.source_sha256 -ne $receipt.contentSha256.ToLowerInvariant() -or $provenance.issuer_id -ne $receipt.issuer.issuerId){Fail 'handoff envelope provenance is invalid.'}
    $fingerprintInput=@($envelope.schema_version,$envelope.handoff_id,$envelope.workspace_id,$envelope.document_id,[string]$envelope.revision,$envelope.content_sha256,$envelope.producer,'true',$envelope.review_status,(UtcStamp $envelope.issued_at_utc),(UtcStamp $envelope.expires_at_utc),$envelope.payload) -join "`n"
    $expectedFingerprint = ShaTextLower $fingerprintInput
    if($envelope.request_fingerprint -ne $expectedFingerprint){Fail "handoff envelope request_fingerprint does not match shared canonical input."}
}
try {
    $awake=Full $AwakeRoot; $receipt=Get-Content -LiteralPath (Full $ReceiptPath) -Raw | ConvertFrom-Json -Depth 100; $authoring=Sha (Join-Path $awake 'docs\persona-contract\awake.persona.authoring.v2.schema.json'); $crosswalk=Sha (Join-Path $awake 'docs\persona-contract\persona-workbench-to-awake.crosswalk.v1.json'); $registry=Sha (Join-Path $awake 'ModuleData\Worldbook\persona_definitions\tag_registry.json')
    CheckCommon $receipt 'persona-workbench.approval-receipt.v1' 'receipt'; if($receipt.localApproval -ne 'approved'){Fail 'receipt localApproval must be approved.'}; if([string]::IsNullOrWhiteSpace($receipt.receiptId)){Fail 'receiptId is required.'}; if([string]::IsNullOrWhiteSpace($receipt.evidenceId)){Fail 'evidenceId is required.'}; CheckFingerprint $receipt.authoring 'authoring' 1 $authoring; CheckFingerprint $receipt.crosswalk 'crosswalk' 1 $crosswalk; CheckFingerprint $receipt.registry 'registry' 1 $registry; if($receipt.source.revision -ne 1 -or -not(IsHash $receipt.source.sha256)){Fail 'receipt source fingerprint is invalid.'}
    if($HandoffPath){
        $handoff=Get-Content -LiteralPath (Full $HandoffPath) -Raw | ConvertFrom-Json -Depth 100; CheckCommon $handoff 'persona-workbench.authoring-handoff.v1' 'handoff'; if([string]::IsNullOrWhiteSpace($handoff.handoffId)){Fail 'handoffId is required.'}; if($handoff.receiptId -ne $receipt.receiptId){Fail 'handoff receiptId mismatch.'}; if($handoff.documentId -ne $receipt.documentId){Fail 'handoff documentId mismatch.'}; if($handoff.contentSha256 -ne $receipt.contentSha256 -or $handoff.canonicalProofSha256 -ne $receipt.canonicalProofSha256){Fail 'handoff content/canonical proof mismatch.'}; if([string]::IsNullOrWhiteSpace($handoff.canonicalJson)){Fail 'handoff canonicalJson is required.'}; $canonicalHash=ShaText $handoff.canonicalJson; if($handoff.contentSha256 -ne $canonicalHash -or $handoff.canonicalProofSha256 -ne $canonicalHash){Fail 'handoff canonical proof hash does not match canonicalJson bytes.'}; if(-not(IsHash $handoff.requestFingerprint)){Fail 'handoff requestFingerprint is invalid.'}; foreach($field in 'source','authoring','crosswalk','registry'){if($handoff.$field.revision -ne $receipt.$field.revision -or $handoff.$field.sha256 -ne $receipt.$field.sha256){Fail "handoff $field fingerprint mismatch."}}; CheckEnvelope $handoff $receipt
        if($receipt.warnings -and @($receipt.warnings).Count -gt 0){Fail 'warning-bearing receipt cannot create handoff.'}
    }
    Write-Output 'PASS persona-workbench.authoring-handoff.contract'; if($HandoffPath){Write-Output "HandoffId=$($handoff.handoffId)"}; Write-Output "ReceiptId=$($receipt.receiptId)"; exit 0
}
catch [System.Management.Automation.RuntimeException] { $message=$_.Exception.Message; if($message.StartsWith('AUTHORING_HANDOFF_INPUT_INVALID:')){Write-Error $message; exit 2}; Write-Error $message; exit 1 }
catch { Write-Error ('AUTHORING_HANDOFF_INPUT_INVALID: ' + $_.Exception.Message); exit 2 }



