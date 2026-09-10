param(
    [string]$SamplePath = "_tmp\r1-real-worldbook-authoring-sample.md",
    [string]$EvidencePath = "..\..\docs\evidence\WORLDBOOK-STUDIO-AI-AUTHORING-R1-REAL-WORLDBOOK-WORKER.json"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sample = [IO.Path]::GetFullPath((Join-Path $root $SamplePath))
$evidence = [IO.Path]::GetFullPath((Join-Path $root $EvidencePath))
$url = $env:WORLD_BOOK_LOCAL_WORKER_URL
$secretName = $env:WORLD_BOOK_LOCAL_WORKER_SECRET_ENV
$started = [DateTimeOffset]::UtcNow

if (-not (Test-Path -LiteralPath $sample -PathType Leaf)) { throw "WB-REAL-WORKER-001: sample not found: $sample" }
$sampleHash = (Get-FileHash -LiteralPath $sample -Algorithm SHA256).Hash.ToLowerInvariant()
$result = [ordered]@{
    schema_version = 'awake.worldbook.real-worker-authoring-preflight.v1'
    started_at_utc = $started.ToString('O')
    finished_at_utc = $null
    sample_path = $sample
    sample_sha256 = $sampleHash
    worker_url_configured = -not [string]::IsNullOrWhiteSpace($url)
    worker_secret_env_configured = -not [string]::IsNullOrWhiteSpace($secretName)
    worker_execution = 'not_attempted_worker_unconfigured'
    network_boundary = 'not_attempted'
    game_directory_touched = $false
    candidate_count = $null
    document_created = $false
    canonical_hashes_verified = $false
    passed = $false
    error = $null
}

try {
    if ([string]::IsNullOrWhiteSpace($url) -or [string]::IsNullOrWhiteSpace($secretName)) {
        $result.error = 'WORLD_BOOK_LOCAL_WORKER_URL and WORLD_BOOK_LOCAL_WORKER_SECRET_ENV are required.'
        return
    }
    $secret = [Environment]::GetEnvironmentVariable($secretName)
    if ([string]::IsNullOrWhiteSpace($secret)) {
        $result.worker_execution = 'not_attempted_worker_secret_unavailable'
        $result.error = "Worker secret environment variable is unavailable: $secretName"
        return
    }
    if (-not [Uri]::TryCreate($url, [UriKind]::Absolute, [ref]$null)) { throw 'WB-REAL-WORKER-002: worker URL is invalid.' }
    $result.worker_execution = 'ready_to_execute'
    $result.network_boundary = 'configured_worker_only'
    throw 'WB-REAL-WORKER-003: execution adapter is intentionally not implicit; run draft-workflow-smoke.ps1 with the configured Worker and record its returned evidence.'
}
catch {
    $result.error = $_.Exception.Message
}
finally {
    $result.finished_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
    $directory = Split-Path -Parent $evidence
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $result | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $evidence -Encoding utf8
    $result | ConvertTo-Json -Depth 20
}
