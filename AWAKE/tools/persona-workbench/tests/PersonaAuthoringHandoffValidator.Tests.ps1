param(
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$AwakeRoot = (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent)
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert([bool]$condition, [string]$message) {
    if (-not $condition) { throw "FAIL PersonaAuthoringHandoffValidator.Tests: $message" }
}

function HashTextLower([string]$value) {
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($value)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

$temp = Join-Path $env:TEMP ('pwb-validator-fixture-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $temp | Out-Null
try {
    $now = [DateTime]::UtcNow
    $session = 'pwb-session-' + ('a' * 32)
    $issuer = 'pwb-issuer-' + ('b' * 32)
    $authoring = (Get-FileHash (Join-Path $AwakeRoot 'docs\persona-contract\awake.persona.authoring.v2.schema.json') -Algorithm SHA256).Hash.ToUpperInvariant()
    $crosswalk = (Get-FileHash (Join-Path $AwakeRoot 'docs\persona-contract\persona-workbench-to-awake.crosswalk.v1.json') -Algorithm SHA256).Hash.ToUpperInvariant()
    $registry = (Get-FileHash (Join-Path $AwakeRoot 'ModuleData\Worldbook\persona_definitions\tag_registry.json') -Algorithm SHA256).Hash.ToUpperInvariant()
    $schema = Get-Content -LiteralPath (Join-Path $ProjectRoot 'contracts\persona-workbench.authoring-handoff.v1.schema.json') -Raw | ConvertFrom-Json -Depth 100
    $envelopeSchema = $schema.'$defs'.envelope
    $required = @('schema_version', 'handoff_id', 'workspace_id', 'document_id', 'revision', 'content_sha256', 'producer', 'provenance', 'review_only', 'review_status', 'issued_at_utc', 'expires_at_utc', 'payload', 'request_fingerprint')
    foreach ($field in $required) { Assert ($null -ne $envelopeSchema.properties.$field) "shared envelope must expose $field" }
    Assert ($null -eq $envelopeSchema.properties.PSObject.Properties['expires_at']) 'shared envelope must not expose legacy expires_at'
    Assert ($null -eq $envelopeSchema.properties.PSObject.Properties['lifecycle_status']) 'shared envelope must not expose consumer lifecycle_status'

    $base = [ordered]@{
        schemaVersion = 'persona-workbench.approval-receipt.v1'
        receiptId = 'pwb-receipt-' + ('c' * 32)
        localApproval = 'approved'
        awakeApproval = 'not_requested'
        documentId = 'fixture.contract.persona'
        contentSha256 = '44136FA355B3678A1146AD16F7E8649E94FB4FC21FE77E8310C060F61CAAFF8A'
        canonicalProofSha256 = '44136FA355B3678A1146AD16F7E8649E94FB4FC21FE77E8310C060F61CAAFF8A'
        evidenceId = 'fixture-evidence'
        issuedAtUtc = $now.ToString('o')
        expiresAtUtc = $now.AddMinutes(10).ToString('o')
        issuer = [ordered]@{ issuerId = $issuer; sessionId = $session; issuedAtUtc = $now.ToString('o') }
        session = [ordered]@{ sessionId = $session; authorization = 'session_csrf' }
        fence = 1
        source = [ordered]@{ revision = 1; sha256 = ('f' * 64).ToUpperInvariant() }
        authoring = [ordered]@{ revision = 1; sha256 = $authoring }
        crosswalk = [ordered]@{ revision = 1; sha256 = $crosswalk }
        registry = [ordered]@{ revision = 1; sha256 = $registry }
        warnings = @()
    }
    $receiptPath = Join-Path $temp 'receipt.json'
    $base | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $receiptPath -Encoding utf8

    $handoffId = 'pwb-handoff-' + ('1' * 32)
    $handoff = [ordered]@{
        schemaVersion = 'persona-workbench.authoring-handoff.v1'
        handoffId = $handoffId
        receiptId = $base.receiptId
        awakeApproval = 'not_requested'
        documentId = $base.documentId
        canonicalJson = '{}'
        contentSha256 = $base.contentSha256
        canonicalProofSha256 = $base.canonicalProofSha256
        evidenceId = $base.evidenceId
        issuedAtUtc = $base.issuedAtUtc
        expiresAtUtc = $base.expiresAtUtc
        issuer = $base.issuer
        session = $base.session
        fence = 1
        source = $base.source
        authoring = $base.authoring
        crosswalk = $base.crosswalk
        registry = $base.registry
        requestFingerprint = ('2' * 64).ToUpperInvariant()
        envelope = [ordered]@{
            schema_version = 'awake.workstation.handoff-envelope.v1'
            handoff_id = $handoffId
            workspace_id = 'workspace.fixture'
            document_id = $base.documentId
            revision = 3
            content_sha256 = $base.contentSha256.ToLowerInvariant()
            producer = 'persona_workbench'
            provenance = [ordered]@{
                source_schema = 'persona-workbench.approval-receipt.v1'
                source_id = $base.receiptId
                source_revision = 1
                source_sha256 = $base.contentSha256.ToLowerInvariant()
                issuer_id = $issuer
            }
            review_only = $true
            review_status = 'approved_local'
            issued_at_utc = $base.issuedAtUtc
            expires_at_utc = $base.expiresAtUtc
            payload = '{}'
            request_fingerprint = ''
        }
    }
    $fingerprintInput = @(
        $handoff.envelope.schema_version, $handoff.envelope.handoff_id, $handoff.envelope.workspace_id,
        $handoff.envelope.document_id, [string]$handoff.envelope.revision, $handoff.envelope.content_sha256,
        $handoff.envelope.producer, 'true', $handoff.envelope.review_status,
        $handoff.envelope.issued_at_utc, $handoff.envelope.expires_at_utc, $handoff.envelope.payload
    ) -join "`n"
    $handoff.envelope.request_fingerprint = HashTextLower $fingerprintInput
    $handoffPath = Join-Path $temp 'handoff.json'
    $handoff | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $handoffPath -Encoding utf8
    $validator = Join-Path $ProjectRoot 'tools\validate-authoring-handoff.ps1'

    & $PSHOME\pwsh.exe -NoProfile -File $validator -AwakeRoot $AwakeRoot -ReceiptPath $receiptPath -HandoffPath $handoffPath -ExpectedSessionId $session -ExpectedIssuerId $issuer -ExpectedFence 1 | Out-Null
    Assert ($LASTEXITCODE -eq 0) 'valid receipt/handoff must pass offline validator'

    $warned = $base | ConvertTo-Json -Depth 20 | ConvertFrom-Json -Depth 20
    $warned.warnings = @('preserve_only:legacy')
    $warned | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $receiptPath -Encoding utf8
    & $PSHOME\pwsh.exe -NoProfile -File $validator -AwakeRoot $AwakeRoot -ReceiptPath $receiptPath -HandoffPath $handoffPath | Out-Null
    Assert ($LASTEXITCODE -ne 0) 'warning-bearing receipt must fail closed'

    $base | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $receiptPath -Encoding utf8
    $forged = $handoff | ConvertTo-Json -Depth 20 | ConvertFrom-Json -Depth 20
    $forged.awakeApproval = 'approved'
    $forged | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $handoffPath -Encoding utf8
    & $PSHOME\pwsh.exe -NoProfile -File $validator -AwakeRoot $AwakeRoot -ReceiptPath $receiptPath -HandoffPath $handoffPath | Out-Null
    Assert ($LASTEXITCODE -ne 0) 'handoff must reject forged AWAKE approval'

    $forged = $handoff | ConvertTo-Json -Depth 20 | ConvertFrom-Json -Depth 20
    $forged.envelope.review_only = $false
    $forged | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $handoffPath -Encoding utf8
    & $PSHOME\pwsh.exe -NoProfile -File $validator -AwakeRoot $AwakeRoot -ReceiptPath $receiptPath -HandoffPath $handoffPath | Out-Null
    Assert ($LASTEXITCODE -ne 0) 'shared envelope must reject review_only=false'

    $expired = $base | ConvertTo-Json -Depth 20 | ConvertFrom-Json -Depth 20
    $expired.expiresAtUtc = $now.AddMinutes(-1).ToString('o')
    $expired | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $receiptPath -Encoding utf8
    & $PSHOME\pwsh.exe -NoProfile -File $validator -AwakeRoot $AwakeRoot -ReceiptPath $receiptPath | Out-Null
    Assert ($LASTEXITCODE -ne 0) 'expired receipt must fail closed'
    Write-Output 'PASS PersonaAuthoringHandoffValidator.Tests'
}
finally {
    $resolved = (Resolve-Path -LiteralPath $temp).Path
    if ($resolved -like "$env:TEMP*") { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
