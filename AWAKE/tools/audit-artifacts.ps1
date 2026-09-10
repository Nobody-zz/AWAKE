[CmdletBinding()]
param(
    [string]$AwakeRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputPath,
    [switch]$IncludeHashes
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$awakeRootFull = [IO.Path]::GetFullPath($AwakeRoot)
$customerRoot = Join-Path $awakeRootFull 'artifacts\customer-delivery'
$worldbookArchiveRoot = Join-Path $awakeRootFull 'tools\worldbook-studio\artifacts\archive'
$generatedAt = [DateTime]::UtcNow

function Get-RelativePath([string]$BasePath, [string]$TargetPath) {
    $baseUri = [Uri]::new(([IO.Path]::GetFullPath($BasePath).TrimEnd('\') + '\'))
    $targetUri = [Uri]::new([IO.Path]::GetFullPath($TargetPath))
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace('/', '\')
}

function Get-DirectorySize([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        return [long]0
    }

    $measure = @(Get-ChildItem -LiteralPath $Path -File -Recurse -Force -ErrorAction SilentlyContinue |
        Measure-Object -Property Length -Sum)
    if ($measure.Count -eq 0 -or $null -eq $measure[0].Sum) {
        return [long]0
    }
    return [long]$measure[0].Sum
}

function Get-FileRecord([IO.FileInfo]$File, [string]$Kind, [string]$Status, [string]$Reason) {
    $record = [ordered]@{
        path = Get-RelativePath $awakeRootFull $File.FullName
        kind = $Kind
        size_bytes = $File.Length
        size_gib = [math]::Round($File.Length / 1GB, 3)
        last_write_time_utc = $File.LastWriteTimeUtc.ToString('o')
        status = $Status
        retention_reason = $Reason
    }
    if ($IncludeHashes) {
        $record.sha256 = (Get-FileHash -LiteralPath $File.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    return [pscustomobject]$record
}

function Get-TextReferenceHits([string]$Needle) {
    $scanRoots = @(
        (Join-Path $awakeRootFull 'tools'),
        (Join-Path $awakeRootFull 'docs'),
        (Join-Path $awakeRootFull 'README_CN.md'),
        (Join-Path $awakeRootFull 'README_EN.txt'),
        (Join-Path $awakeRootFull 'BUILD_VERIFICATION.txt')
    ) | Where-Object { Test-Path -LiteralPath $_ }

    $hits = [System.Collections.Generic.List[string]]::new()
    foreach ($scanRoot in $scanRoots) {
        $files = if (Test-Path -LiteralPath $scanRoot -PathType Container) {
            Get-ChildItem -LiteralPath $scanRoot -File -Recurse -Force -ErrorAction SilentlyContinue
        } else {
            Get-Item -LiteralPath $scanRoot
        }
        foreach ($file in $files) {
            if ($file.FullName -like "$worldbookArchiveRoot*") {
                continue
            }
            if ($file.Extension -notin @('.ps1', '.psm1', '.md', '.txt', '.json', '.xml', '.yml', '.yaml')) {
                continue
            }
            Select-String -LiteralPath $file.FullName -Pattern $Needle -SimpleMatch -ErrorAction SilentlyContinue |
                ForEach-Object { [void]$hits.Add((Get-RelativePath $awakeRootFull $file.FullName)) }
        }
    }
    return @($hits | Sort-Object -Unique)
}

$pointer = $null
$pointerPath = Join-Path $customerRoot 'rollback-pointer.json'
if (Test-Path -LiteralPath $pointerPath -PathType Leaf) {
    $pointer = Get-Content -Raw -LiteralPath $pointerPath | ConvertFrom-Json
}

$currentBuildId = if ($pointer) { [string]$pointer.current_build_id } else { $null }
$previousBuildId = if ($pointer) { [string]$pointer.previous_build_id } else { $null }

$records = [System.Collections.Generic.List[object]]::new()
$customerBuilds = @()
$customerZips = @()
$customerAncillaryFiles = @()

if (Test-Path -LiteralPath $customerRoot -PathType Container) {
    $customerBuilds = @(Get-ChildItem -LiteralPath $customerRoot -Directory -Force |
        Where-Object { $_.Name -like 'awake-customer-*' })
    $customerZips = @(Get-ChildItem -LiteralPath $customerRoot -File -Force |
        Where-Object { $_.Extension -eq '.zip' -and $_.BaseName -like 'awake-customer-*' })
    $customerAncillaryFiles = @(Get-ChildItem -LiteralPath $customerRoot -File -Force |
        Where-Object {
            $_.Name -like 'awake-customer-*.zip.sha256' -or
            $_.Name -like 'rollback-verification-*.json'
        })
}

$validationReferencedBuildIds = @(
    $customerAncillaryFiles |
        Where-Object { $_.Name -like 'rollback-verification-*.json' } |
        ForEach-Object { $_.BaseName -replace '^rollback-verification-(.+)$', '$1' }
)

foreach ($build in $customerBuilds) {
    $isCurrent = $build.Name -eq $currentBuildId
    $isPrevious = $build.Name -eq $previousBuildId
    $hasValidationReference = $build.Name -in $validationReferencedBuildIds
    $status = if ($isCurrent) { 'protected_current' } elseif ($isPrevious) { 'protected_previous' } else { 'cleanup_candidate_review' }
    $reason = if ($isCurrent) {
        'rollback-pointer.current_build_id 指向；不得自动清理。'
    } elseif ($isPrevious) {
        'rollback-pointer.previous_build_id 指向；不得自动清理。'
    } else {
        '未被当前 rollback pointer 保护；需确认是否为长期回退或调查证据。'
    }
    $size = Get-DirectorySize $build.FullName
    $records.Add([pscustomobject][ordered]@{
        path = Get-RelativePath $awakeRootFull $build.FullName
        kind = 'customer_build_directory'
        build_id = $build.Name
        size_bytes = $size
        size_gib = [math]::Round($size / 1GB, 3)
        file_count = @(Get-ChildItem -LiteralPath $build.FullName -File -Recurse -Force).Count
        current = $isCurrent
        previous = $isPrevious
        status = $status
        referenced_by_rollback_pointer = ($isCurrent -or $isPrevious)
        referenced_by_validation_record = $hasValidationReference
        retention_reason = $reason
    })
}

foreach ($zip in $customerZips) {
    $buildId = $zip.BaseName
    $isCurrent = $buildId -eq $currentBuildId
    $isPrevious = $buildId -eq $previousBuildId
    $hasValidationReference = $buildId -in $validationReferencedBuildIds
    $status = if ($isCurrent) { 'protected_current' } elseif ($isPrevious) { 'protected_previous' } else { 'cleanup_candidate_review' }
    $reason = if ($isCurrent) {
        'current 交付 ZIP；与 current build 配套保留。'
    } elseif ($isPrevious) {
        'previous 交付 ZIP；与 previous build 配套保留。'
    } else {
        '未被当前 rollback pointer 保护；需先确认长期回退/验证引用。'
    }
    $zipRecord = Get-FileRecord $zip 'customer_zip' $status $reason
    $zipRecord | Add-Member -NotePropertyMembers @{
        build_id = $buildId
        current = $isCurrent
        previous = $isPrevious
        referenced_by_rollback_pointer = ($isCurrent -or $isPrevious)
        referenced_by_validation_record = $hasValidationReference
    }
    $records.Add($zipRecord)
}

foreach ($file in $customerAncillaryFiles) {
    $buildId = if ($file.Name -like 'rollback-verification-*.json') {
        $file.BaseName -replace '^rollback-verification-(.+)$', '$1'
    } else {
        $file.BaseName -replace '\.zip$', ''
    }
    $isCurrent = $buildId -eq $currentBuildId
    $isPrevious = $buildId -eq $previousBuildId
    $kind = if ($file.Name -like '*.zip.sha256') { 'customer_zip_sidecar' } else { 'rollback_verification_record' }
    $reason = if ($kind -eq 'rollback_verification_record') {
        '回滚验证记录；在确认其内容已被汇总且不再是调查证据前不得清理。'
    } elseif ($isCurrent -or $isPrevious) {
        'current/previous ZIP 的校验 sidecar；与对应交付 ZIP 配套保留。'
    } else {
        '历史 ZIP 的校验 sidecar；只有对应 ZIP 被确认清理后才可一并清理。'
    }
    $status = if ($kind -eq 'rollback_verification_record' -or $isCurrent -or $isPrevious) {
        'protected_or_review_required'
    } else {
        'cleanup_candidate_review'
    }
    $ancillaryRecord = Get-FileRecord $file $kind $status $reason
    $ancillaryRecord | Add-Member -NotePropertyMembers @{
        build_id = $buildId
        current = $isCurrent
        previous = $isPrevious
        referenced_by_rollback_pointer = ($isCurrent -or $isPrevious)
        referenced_by_validation_record = ($kind -eq 'rollback_verification_record')
    }
    $records.Add($ancillaryRecord)
}

if (Test-Path -LiteralPath $pointerPath -PathType Leaf) {
    $pointerFile = Get-Item -LiteralPath $pointerPath
    $records.Add((Get-FileRecord $pointerFile 'rollback_pointer' 'protected' '唯一外置回滚指针；不得自动清理。'))
}

$archiveRecords = [System.Collections.Generic.List[object]]::new()
if (Test-Path -LiteralPath $worldbookArchiveRoot -PathType Container) {
    $archiveDirectories = @(Get-ChildItem -LiteralPath $worldbookArchiveRoot -Directory -Force |
        Where-Object { $_.Name -match '^\d{4}-\d{2}-\d{2}$' } | Sort-Object Name)
    $keepDates = @($archiveDirectories | Sort-Object Name -Descending | Select-Object -First 3 | ForEach-Object Name)

    foreach ($archiveDirectory in $archiveDirectories) {
        $size = Get-DirectorySize $archiveDirectory.FullName
        $isRecent = $archiveDirectory.Name -in $keepDates
        $status = if ($isRecent) { 'policy_keep_recent' } else { 'cleanup_candidate_review' }
        $reason = if ($isRecent) {
            '默认保留最近三个日期归档；仍需确认是否含唯一争议/失败证据。'
        } else {
            '超过默认最近三个日期窗口；需检查唯一证据、脚本引用和是否可压缩后再处理。'
        }
        $archiveRecords.Add([pscustomobject][ordered]@{
            path = Get-RelativePath $awakeRootFull $archiveDirectory.FullName
            kind = 'worldbook_studio_date_archive'
            archive_date = $archiveDirectory.Name
            size_bytes = $size
            size_gib = [math]::Round($size / 1GB, 3)
            file_count = @(Get-ChildItem -LiteralPath $archiveDirectory.FullName -File -Recurse -Force).Count
            status = $status
            current = $false
            previous = $false
            referenced_by_rollback_pointer = $false
            retention_reason = $reason
        })
    }
}

$topLevelRecords = foreach ($relativePath in @('artifacts', '_build_out', 'dist', 'obj')) {
    $path = Join-Path $awakeRootFull $relativePath
    if (Test-Path -LiteralPath $path -PathType Container) {
        $size = Get-DirectorySize $path
        [pscustomobject][ordered]@{
            path = $relativePath
            kind = 'top_level_area'
            size_bytes = $size
            size_gib = [math]::Round($size / 1GB, 3)
            status = 'inventory_only'
            retention_reason = '汇总区；不据此自动删除，需按子目录审计。'
        }
    }
}

$generatedAreaRecords = foreach ($relativePath in @(
    'tools\worldbook-studio\artifacts',
    'tools\worldbook-studio\_tmp',
    'tools\persona-workbench\artifacts',
    'tools\persona-workbench\.runtime'
)) {
    $path = Join-Path $awakeRootFull $relativePath
    if (Test-Path -LiteralPath $path -PathType Container) {
        $size = Get-DirectorySize $path
        [pscustomobject][ordered]@{
            path = $relativePath
            kind = 'tool_generated_area'
            size_bytes = $size
            size_gib = [math]::Round($size / 1GB, 3)
            file_count = @(Get-ChildItem -LiteralPath $path -File -Recurse -Force -ErrorAction SilentlyContinue).Count
            status = 'inventory_only'
            retention_reason = '工具目录产物；本审计只统计，不修改 Worldbook Studio 或 Persona Workbench。'
        }
    }
}

$report = [ordered]@{
    schema_version = 'awake.artifact-retention-audit.v1'
    generated_at_utc = $generatedAt.ToString('o')
    awake_root = $awakeRootFull
    mode = 'read_only_preview'
    include_hashes = [bool]$IncludeHashes
    cleanup_performed = $false
    drive = [ordered]@{
        name = 'C'
        free_bytes = (Get-PSDrive -Name C).Free
        free_gib = [math]::Round((Get-PSDrive -Name C).Free / 1GB, 3)
    }
    rollback_pointer = if ($pointer) {
        [ordered]@{
            path = Get-RelativePath $awakeRootFull $pointerPath
            current_build_id = $currentBuildId
            previous_build_id = $previousBuildId
        }
    } else { $null }
    summary = [ordered]@{
        customer_build_count = $customerBuilds.Count
        customer_zip_count = $customerZips.Count
        customer_ancillary_file_count = $customerAncillaryFiles.Count
        validation_reference_count = @($customerAncillaryFiles | Where-Object { $_.Name -like 'rollback-verification-*.json' }).Count
        worldbook_archive_date_count = $archiveRecords.Count
        customer_builds_protected = @($customerBuilds | Where-Object { $_.Name -in @($currentBuildId, $previousBuildId) }).Count
        customer_cleanup_candidates = @($customerBuilds | Where-Object { $_.Name -notin @($currentBuildId, $previousBuildId) }).Count
        worldbook_cleanup_candidates = @($archiveRecords | Where-Object status -eq 'cleanup_candidate_review').Count
    }
    top_level_areas = @($topLevelRecords)
    generated_tool_areas = @($generatedAreaRecords)
    customer_delivery = @($records)
    worldbook_studio_archive = @($archiveRecords)
    safeguards = @(
        '此脚本默认只读，不执行删除、移动、压缩、同步或发布。',
        'current、previous 和 rollback-pointer.json 始终标记 protected。',
        '未被指针保护的构建只标记 cleanup_candidate_review，不代表可直接删除。',
        'Worldbook Studio 日期归档按最近三个日期给出策略预览，不修改 Studio 工具或权威运行时内容。',
        'IncludeHashes 会增加磁盘读取时间；不会写回任何被审计产物。'
    )
}

$json = $report | ConvertTo-Json -Depth 20
if ($OutputPath) {
    $outputFull = [IO.Path]::GetFullPath($OutputPath)
    $outputDirectory = Split-Path -Parent $outputFull
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    [IO.File]::WriteAllText($outputFull, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
    Write-Output "REPORT=$outputFull"
} else {
    Write-Output $json
}
