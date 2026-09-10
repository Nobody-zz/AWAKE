[CmdletBinding()]
param(
    [string]$ScriptPath = (Join-Path $PSScriptRoot '..\local-worker-package-acceptance.ps1')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "LOCAL-WORKER-TEST-FAIL: $Message"
    }
}

if (-not (Test-Path -LiteralPath $ScriptPath -PathType Leaf)) {
    throw "LOCAL-WORKER-TEST-FAIL: acceptance script is missing: $ScriptPath"
}

$source = Get-Content -Raw -LiteralPath $ScriptPath
Assert-True ($source -match '\[string\]\$PackageRoot') 'PackageRoot input is required.'
Assert-True ($source -match '\[string\]\$ZipPath') 'ZipPath input is supported.'
Assert-True ($source -match 'worldbook_studio\.zip') 'The customer package must provide the WBS ZIP.'
Assert-True ($source -match 'delivery-local-worker') 'Evidence identifies the package local-worker section.'
Assert-True ($source -match 'not_configured') 'Missing Ollama configuration has an explicit status.'
Assert-True ($source -match 'unverified') 'Unavailable execution has an explicit unverified status.'
Assert-True ($source -match 'handshake') 'Handshake evidence is recorded.'
Assert-True ($source -match 'analyze') 'Analyze evidence is recorded.'
Assert-True ($source -match 'review_only') 'Review-only preservation is asserted.'
Assert-True ($source -match 'candidate') 'Candidate evidence is recorded.'
Assert-True ($source -match '127\.0\.0\.1') 'Worker and WBS endpoints are loopback constrained.'
Assert-True ($source -notmatch 'src[\\/]+Awake\.WorldbookStudio') 'Acceptance must not launch the source project.'
Assert-True ($source -match 'secret' -and $source -match 'redact') 'Secret values must be redacted from evidence.'

Write-Output 'LOCAL_WORKER_PACKAGE_ACCEPTANCE_CONTRACT_PASS'
