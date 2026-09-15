param(
    [string]$SyncScript = (Join-Path (Split-Path -Parent $PSScriptRoot) 'sync_module.ps1')
)

$ErrorActionPreference = 'Stop'
$script:Passed = 0

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Write-TestFile([string]$Path, [string]$Content) {
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [IO.File]::WriteAllText($Path, $Content, (New-Object Text.UTF8Encoding($false)))
}

function New-RuntimePackage([string]$RuntimeRoot, $Payload) {
    foreach ($entry in $Payload.GetEnumerator()) {
        Write-TestFile (Join-Path $RuntimeRoot ([string]$entry.Key)) ([string]$entry.Value)
    }

    $entries = @(
        foreach ($file in @(Get-ChildItem -LiteralPath $RuntimeRoot -Recurse -File -Force | Sort-Object FullName)) {
            $relative = $file.FullName.Substring($RuntimeRoot.Length + 1).Replace('\', '/')
            [ordered]@{
                path = $relative
                sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                length = [int64]$file.Length
            }
        }
    )
    $manifest = [ordered]@{
        schemaVersion = 'awake.embedded-runtime.v1'
        product = 'AWAKE.RuntimeService'
        rid = 'win-x64'
        selfContained = $true
        configuration = 'Release'
        entryPoint = 'MarcusAwakeRuntimeService.exe'
        components = @('Runtime', 'Provider', 'Storage', 'Transport', 'Framework', 'SQLite')
        files = $entries
    }
    Write-TestFile (Join-Path $RuntimeRoot 'manifest.json') (($manifest | ConvertTo-Json -Depth 10) + [Environment]::NewLine)
    $sumLines = @($entries | ForEach-Object { "$($_.sha256)  $($_.path)" })
    Write-TestFile (Join-Path $RuntimeRoot 'SHA256SUMS.txt') (($sumLines -join [Environment]::NewLine) + [Environment]::NewLine)
}

function New-SyncFixture([string]$Name, [switch]$OmitWorldbookPackage) {
    $base = Join-Path ([IO.Path]::GetTempPath()) ('awake-sync-tests-' + $Name + '-' + [Guid]::NewGuid().ToString('N'))
    $project = Join-Path $base 'AWAKE'
    $dist = Join-Path $project 'dist\Modules\AWAKE'
    $game = Join-Path $base 'Game\Modules\AWAKE'
    $build = Join-Path $project '_build_out\1.3.15\Release\Awake.dll'
    $framework = Join-Path $project 'framework\MarcusAwakeFramework\_build_out\Release\MarcusAwakeFramework.dll'
    $transport = Join-Path $project 'framework\MarcusAwakeTransport\_build_out\Release\MarcusAwakeTransport.dll'
    $runtimeService = Join-Path $project 'framework\MarcusAwakeRuntimeService\_build_out\Release\win-x64\MarcusAwakeRuntimeService.dll'
    $runtimeServiceExe = Join-Path $project 'framework\MarcusAwakeRuntimeService\_build_out\Release\win-x64\MarcusAwakeRuntimeService.exe'
    $provider = Join-Path $project 'framework\MarcusAwakeProvider\_build_out\Release\MarcusAwakeProvider.dll'
    $storage = Join-Path $project 'framework\MarcusAwakeStorage\_build_out\Release\MarcusAwakeStorage.dll'
    New-Item -ItemType Directory -Path $project -Force | Out-Null
    Write-TestFile (Join-Path $project 'SubModule.xml') '<Module><Version value="v0.2.0" /></Module>'
    Write-TestFile (Join-Path $project 'README_CN.md') 'readme-cn'
    Write-TestFile (Join-Path $project 'README_EN.txt') 'readme-en'
    Write-TestFile (Join-Path $project 'BUILD_VERIFICATION.txt') 'fixture-build-verification'
    Write-TestFile (Join-Path $project 'AWAKE.csproj') '<Project />'
    Write-TestFile (Join-Path $project 'src\SyncFixture.cs') 'class SyncFixture {}'
    foreach ($component in @('MarcusAwakeFramework', 'MarcusAwakeTransport', 'MarcusAwakeRuntimeService', 'MarcusAwakeProvider', 'MarcusAwakeStorage')) {
        Write-TestFile (Join-Path $project ('framework\' + $component + '\' + $component + '.csproj')) '<Project />'
        Write-TestFile (Join-Path $project ('framework\' + $component + '\src\SyncFixture.cs')) ('class ' + $component + 'SyncFixture {}')
    }
    Write-TestFile $build 'new-dll'
    Write-TestFile $framework 'new-framework-dll'
    Write-TestFile $transport 'new-transport-dll'
    Write-TestFile $runtimeService 'runtime-service-dll'
    Write-TestFile $runtimeServiceExe 'runtime-service-exe'
    Write-TestFile $provider 'provider-dll'
    Write-TestFile $storage 'storage-dll'
    foreach ($file in @('AwakeMessenger.xml','AwakePortraitProbe.xml','DeveloperCheck.xml','NpcDialogue.xml','SceneDialogueStatus.xml','WeeklyReportBrowser.xml','WorldEventInbox.xml')) {
        Write-TestFile (Join-Path $project ('GUI\Prefabs\' + $file)) ('<' + $file + ' />')
    }
    Write-TestFile (Join-Path $project 'ModuleData\Languages\awake_strings.xml') '<strings />'
    Write-TestFile (Join-Path $project 'ModuleData\Languages\language_data.xml') '<language />'
    Write-TestFile (Join-Path $project 'ModuleData\Languages\CNs\awake_strings-zh-HANS.xml') '<strings-cn />'
    Write-TestFile (Join-Path $project 'ModuleData\Languages\CNs\language_data.xml') '<language-cn />'
    # Repository-side package form (2026-09-14): a registry at the Worldbook root plus one
    # packages/<slug>/ runtime three. The old v1 manifest / rules/ sample is gone on purpose.
    $packageManifest = @{
        schemaVersion = 'awake.worldbook.v2'
        packageId = 'awake:worldbook:calradia'
        worldId = 'awake:world:calradia'
        version = '1.0.0'
        kind = 'universe'
        entrypoints = @{ runtime = 'runtime.json'; index = 'index.json' }
        hashes = @{ manifestHash = ('a' * 64); contentHash = ('b' * 64); packageHash = ('c' * 64) }
    } | ConvertTo-Json -Depth 6
    $registryManifest = @{
        schemaVersion = 'awake.worldbook.registry.v1'
        registryId = 'awake:registry:installed'
        packages = @(@{
            packageId = 'awake:worldbook:calradia'
            version = '1.0.0'
            kind = 'universe'
            relativePath = 'packages/calradia'
            manifestHash = ('a' * 64)
            contentHash = ('b' * 64)
            packageHash = ('c' * 64)
            enabledByDefault = $true
        })
    } | ConvertTo-Json -Depth 6
    Write-TestFile (Join-Path $project 'ModuleData\Worldbook\manifest.json') $registryManifest
    Write-TestFile (Join-Path $project 'ModuleData\Worldbook\migration_report.json') '{}'
    if (-not $OmitWorldbookPackage) {
        Write-TestFile (Join-Path $project 'ModuleData\Worldbook\packages\calradia\manifest.json') $packageManifest
        Write-TestFile (Join-Path $project 'ModuleData\Worldbook\packages\calradia\runtime.json') '{"schemaVersion":"awake.worldbook.v2","entries":[]}'
        Write-TestFile (Join-Path $project 'ModuleData\Worldbook\packages\calradia\index.json') '{}'
    }
    Write-TestFile (Join-Path $project 'ModuleData\Worldbook\persona_definitions\tag_registry.json') '{"tags":[]}'
    Write-TestFile (Join-Path $project 'ModuleData\Worldbook\persona_definitions\definitions\hero_default.json') '{"characterId":"hero.default"}'
    New-RuntimePackage (Join-Path $dist 'bin\Win64_Shipping_Client\Runtime') ([ordered]@{
        'MarcusAwakeRuntimeService.exe' = 'runtime-service-exe'
        'MarcusAwakeRuntimeService.dll' = 'runtime-service-dll'
        'MarcusAwakeRuntimeService.deps.json' = 'runtime-service-deps'
        'MarcusAwakeRuntimeService.runtimeconfig.json' = 'runtime-service-runtimeconfig'
        'MarcusAwakeProvider.dll' = 'provider-dll'
        'MarcusAwakeStorage.dll' = 'storage-dll'
        'MarcusAwakeTransport.dll' = 'new-transport-dll'
        'MarcusAwakeFramework.dll' = 'new-framework-dll'
        'Microsoft.Data.Sqlite.dll' = 'sqlite-managed'
        'SQLitePCLRaw.batteries_v2.dll' = 'sqlite-batteries'
        'SQLitePCLRaw.core.dll' = 'sqlite-core'
        'SQLitePCLRaw.provider.e_sqlite3.dll' = 'sqlite-provider'
        'e_sqlite3.dll' = 'sqlite-native'
    })
    return [ordered]@{ Base = $base; Project = $project; Dist = $dist; Game = $game; Build = $build; Framework = $framework; Transport = $transport }
}

function Invoke-Sync($Fixture, [string[]]$Arguments) {
    $all = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$SyncScript,'-ProjectRoot',$Fixture.Project,'-DistModule',$Fixture.Dist,'-GameModule',$Fixture.Game,'-BuildDllPath',$Fixture.Build) + $Arguments
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & powershell @all 2>&1
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousPreference
    }
    return [ordered]@{ ExitCode = $exitCode; Output = ($output | Out-String) }
}

function Run-Test([string]$Name, [scriptblock]$Body) {
    & $Body
    $script:Passed += 1
    Write-Output "PASS $Name"
}

Run-Test 'PowerShell 5.1 parses sync_module.ps1' {
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($SyncScript, [ref]$tokens, [ref]$errors) | Out-Null
    Assert-True ($errors.Count -eq 0) (($errors | Out-String))
}

Run-Test 'BUILD_VERIFICATION is copied as a managed root file' {
    $fixture = New-SyncFixture 'build-verification'
    try {
        Write-TestFile (Join-Path $fixture.Project 'src\\SyncFixture.cs') 'class SyncFixture {}'
        Write-TestFile (Join-Path $fixture.Project 'AWAKE.csproj') '<Project />'
        Write-TestFile (Join-Path $fixture.Project 'framework\\MarcusAwakeFramework\\src\\SyncFixture.cs') 'class FrameworkSyncFixture {}'
        Write-TestFile (Join-Path $fixture.Project 'framework\\MarcusAwakeFramework\\MarcusAwakeFramework.csproj') '<Project />'
        Write-TestFile (Join-Path $fixture.Project 'framework\\MarcusAwakeTransport\\src\\SyncFixture.cs') 'class TransportSyncFixture {}'
        Write-TestFile (Join-Path $fixture.Project 'framework\\MarcusAwakeTransport\\MarcusAwakeTransport.csproj') '<Project />'
        Start-Sleep -Milliseconds 100
        Write-TestFile $fixture.Build 'new-dll'
        Write-TestFile (Join-Path $fixture.Project 'framework\\MarcusAwakeFramework\\_build_out\\Release\\MarcusAwakeFramework.dll') 'new-framework-dll'
        Write-TestFile (Join-Path $fixture.Project 'framework\\MarcusAwakeTransport\\_build_out\\Release\\MarcusAwakeTransport.dll') 'new-transport-dll'
        $result = Invoke-Sync $fixture @('-SkipGame')
        Assert-True ($result.ExitCode -eq 0) $result.Output
        $target = Join-Path $fixture.Dist 'BUILD_VERIFICATION.txt'
        Assert-True (Test-Path -LiteralPath $target -PathType Leaf) 'BUILD_VERIFICATION.txt was not copied to dist.'
        Assert-True ((Get-Content -LiteralPath $target -Raw) -eq 'fixture-build-verification') 'BUILD_VERIFICATION.txt content mismatch.'
    } finally { Remove-Item -LiteralPath $fixture.Base -Recurse -Force }
}

Run-Test 'WhatIf has zero filesystem side effects' {
    $fixture = New-SyncFixture 'whatif'
    try {
        $report = Join-Path $fixture.Base 'whatif-report.json'
        $beforeFiles = @(Get-ChildItem -LiteralPath $fixture.Dist -Recurse -File -Force | ForEach-Object { $_.FullName.Substring($fixture.Dist.Length + 1) + '=' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } | Sort-Object)
        $beforeDirectories = @(Get-ChildItem -LiteralPath $fixture.Dist -Recurse -Directory -Force | ForEach-Object { $_.FullName.Substring($fixture.Dist.Length + 1) } | Sort-Object)
        $result = Invoke-Sync $fixture @('-SkipGame','-WhatIf','-ReportPath',$report)
        Assert-True ($result.ExitCode -eq 0) $result.Output
        $afterFiles = @(Get-ChildItem -LiteralPath $fixture.Dist -Recurse -File -Force | ForEach-Object { $_.FullName.Substring($fixture.Dist.Length + 1) + '=' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } | Sort-Object)
        $afterDirectories = @(Get-ChildItem -LiteralPath $fixture.Dist -Recurse -Directory -Force | ForEach-Object { $_.FullName.Substring($fixture.Dist.Length + 1) } | Sort-Object)
        Assert-True (($beforeFiles -join "`n") -eq ($afterFiles -join "`n")) 'WhatIf changed or added a file.'
        Assert-True (($beforeDirectories -join "`n") -eq ($afterDirectories -join "`n")) 'WhatIf changed or added a directory.'
        Assert-True (-not (Test-Path -LiteralPath $report)) 'WhatIf created a report file.'
    } finally { Remove-Item -LiteralPath $fixture.Base -Recurse -Force }
}

Run-Test 'Release staging uses a strict allowlist and leaves dist untouched' {
    $fixture = New-SyncFixture 'release-staging'
    try {
        Write-TestFile (Join-Path $fixture.Project 'AGENTS.md') 'internal instructions'
        Write-TestFile (Join-Path $fixture.Project 'AWAKE-Task-Queue-20260816.md') 'internal queue'
        Write-TestFile (Join-Path $fixture.Project 'GRILLME-FRAMEWORK-BUGFIX-20260815.md') 'internal review'
        Write-TestFile (Join-Path $fixture.Project 'docs\internal.md') 'internal docs'
        $beforeDist = @(Get-ChildItem -LiteralPath $fixture.Dist -Recurse -File -Force | ForEach-Object { $_.FullName.Substring($fixture.Dist.Length + 1) + '=' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } | Sort-Object)
        $staging = Join-Path $fixture.Base 'release-staging\Modules\AWAKE'
        $report = Join-Path $fixture.Base 'release-staging-report.json'
        $result = Invoke-Sync $fixture @('-SkipGame','-ReleaseStagingRoot',$staging,'-ReportPath',$report)
        Assert-True ($result.ExitCode -eq 0) $result.Output
        Assert-True (Test-Path -LiteralPath (Join-Path $staging 'SubModule.xml') -PathType Leaf) 'Release staging missed SubModule.xml.'
        Assert-True (Test-Path -LiteralPath (Join-Path $staging 'bin\Win64_Shipping_Client\Runtime\manifest.json') -PathType Leaf) 'Release staging missed Runtime manifest.'
        Assert-True (Test-Path -LiteralPath (Join-Path $staging 'ModuleData\Worldbook\manifest.json') -PathType Leaf) 'Release staging missed the worldbook registry.'
        Assert-True (Test-Path -LiteralPath (Join-Path $staging 'ModuleData\Worldbook\packages\calradia\runtime.json') -PathType Leaf) 'Release staging missed the compiled worldbook package.'
        foreach ($forbidden in @(
            'AGENTS.md',
            'AWAKE-Task-Queue-20260816.md',
            'GRILLME-FRAMEWORK-BUGFIX-20260815.md',
            'docs',
            'ModuleData\Worldbook\migration_report.json'
        )) {
            Assert-True (-not (Test-Path -LiteralPath (Join-Path $staging $forbidden))) ('Forbidden release staging path was included: ' + $forbidden)
        }
        $afterDist = @(Get-ChildItem -LiteralPath $fixture.Dist -Recurse -File -Force | ForEach-Object { $_.FullName.Substring($fixture.Dist.Length + 1) + '=' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } | Sort-Object)
        Assert-True (($beforeDist -join "`n") -eq ($afterDist -join "`n")) 'Release staging changed dist.'
        $reportValue = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
        Assert-True ($reportValue.mode -eq 'release_staging') 'Release staging report mode is incorrect.'
        Assert-True ($reportValue.state -eq 'verified') 'Release staging report was not verified.'
        Assert-True (@($reportValue.allowlist.files) -notcontains 'ModuleData\Worldbook\migration_report.json') 'Internal migration report entered the allowlist.'
    } finally { Remove-Item -LiteralPath $fixture.Base -Recurse -Force }
}

Run-Test 'Release staging WhatIf has zero filesystem side effects' {
    $fixture = New-SyncFixture 'release-staging-whatif'
    try {
        $staging = Join-Path $fixture.Base 'release-staging\Modules\AWAKE'
        $report = Join-Path $fixture.Base 'release-staging-whatif-report.json'
        $beforeDist = @(Get-ChildItem -LiteralPath $fixture.Dist -Recurse -File -Force | ForEach-Object { $_.FullName.Substring($fixture.Dist.Length + 1) + '=' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } | Sort-Object)
        $result = Invoke-Sync $fixture @('-SkipGame','-WhatIf','-ReleaseStagingRoot',$staging,'-ReportPath',$report)
        Assert-True ($result.ExitCode -eq 0) $result.Output
        Assert-True (-not (Test-Path -LiteralPath $staging)) 'Release staging WhatIf created its destination.'
        Assert-True (-not (Test-Path -LiteralPath $report)) 'Release staging WhatIf created a report.'
        $afterDist = @(Get-ChildItem -LiteralPath $fixture.Dist -Recurse -File -Force | ForEach-Object { $_.FullName.Substring($fixture.Dist.Length + 1) + '=' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } | Sort-Object)
        Assert-True (($beforeDist -join "`n") -eq ($afterDist -join "`n")) 'Release staging WhatIf changed dist.'
    } finally { Remove-Item -LiteralPath $fixture.Base -Recurse -Force }
}

Run-Test 'Release staging injected failure leaves destination untouched' {
    $fixture = New-SyncFixture 'release-staging-rollback'
    try {
        $staging = Join-Path $fixture.Base 'release-staging\Modules\AWAKE'
        $report = Join-Path $fixture.Base 'release-staging-rollback-report.json'
        $result = Invoke-Sync $fixture @('-SkipGame','-TestFailAfterCopies','1','-ReleaseStagingRoot',$staging,'-ReportPath',$report)
        Assert-True ($result.ExitCode -ne 0) 'Injected release staging failure unexpectedly succeeded.'
        Assert-True (-not (Test-Path -LiteralPath $staging)) 'Failed release staging left a destination behind.'
        $reportValue = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
        Assert-True ($reportValue.state -eq 'rollback_verified') ('Unexpected release staging rollback state: ' + $reportValue.state)
        Assert-True ($reportValue.rollback.verified -eq $true) 'Release staging rollback was not verified.'
        Assert-True ($reportValue.rollback.destination_untouched -eq $true) 'Release staging destination was not reported untouched.'
    } finally { Remove-Item -LiteralPath $fixture.Base -Recurse -Force }
}

Run-Test 'Release staging refuses an existing destination without changing it' {
    $fixture = New-SyncFixture 'release-staging-existing'
    try {
        $staging = Join-Path $fixture.Base 'release-staging\Modules\AWAKE'
        $sentinel = Join-Path $staging 'sentinel.txt'
        Write-TestFile $sentinel 'do-not-touch'
        $result = Invoke-Sync $fixture @('-SkipGame','-ReleaseStagingRoot',$staging)
        Assert-True ($result.ExitCode -ne 0) 'Existing release staging destination was accepted.'
        Assert-True ($result.Output -match 'already exists') $result.Output
        Assert-True ((Get-Content -LiteralPath $sentinel -Raw) -eq 'do-not-touch') 'Existing release staging destination was changed.'
    } finally { Remove-Item -LiteralPath $fixture.Base -Recurse -Force }
}

Run-Test 'Dist sync copies embedded framework assemblies beside Awake.dll' {
    $fixture = New-SyncFixture 'embedded-assemblies'
    try {
        $result = Invoke-Sync $fixture @('-SkipGame')
        Assert-True ($result.ExitCode -eq 0) $result.Output
        foreach ($entry in @(
            @{ Name = 'MarcusAwakeFramework.dll'; Source = $fixture.Framework },
            @{ Name = 'MarcusAwakeTransport.dll'; Source = $fixture.Transport }
        )) {
            $target = Join-Path $fixture.Dist ('bin\Win64_Shipping_Client\' + $entry.Name)
            Assert-True (Test-Path -LiteralPath $target -PathType Leaf) ('Missing copied embedded assembly: ' + $entry.Name)
            Assert-True ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $entry.Source -Algorithm SHA256).Hash) ('Embedded assembly hash mismatch: ' + $entry.Name)
        }
    } finally { Remove-Item -LiteralPath $fixture.Base -Recurse -Force }
}

Run-Test 'Game sync copies and verifies embedded Runtime payload' {
    $fixture = New-SyncFixture 'game-runtime'
    try {
        New-Item -ItemType Directory -Path $fixture.Game -Force | Out-Null
        $report = Join-Path $fixture.Base 'game-runtime-report.json'
        $result = Invoke-Sync $fixture @('-ConfirmGameSync','-ReportPath',$report)
        Assert-True ($result.ExitCode -eq 0) $result.Output
        $runtime = Join-Path $fixture.Game 'bin\Win64_Shipping_Client\Runtime'
        foreach ($name in @('manifest.json','SHA256SUMS.txt','MarcusAwakeRuntimeService.exe')) {
            $target = Join-Path $runtime $name
            Assert-True (Test-Path -LiteralPath $target -PathType Leaf) ('Game Runtime file missing: ' + $name)
            $source = Join-Path $fixture.Dist ('bin\Win64_Shipping_Client\Runtime\' + $name)
            Assert-True ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash) ('Game Runtime hash mismatch: ' + $name)
        }
        $reportValue = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
        Assert-True (@($reportValue.managed_files) -contains 'bin\Win64_Shipping_Client\Runtime\manifest.json') 'Runtime manifest was not managed.'
        Assert-True (@($reportValue.preserved | Where-Object { $_ -match '^game/bin[\\/]Win64_Shipping_Client[\\/]Runtime[\\/]' }).Count -eq 0) 'Runtime payload was incorrectly preserved.'
    } finally { Remove-Item -LiteralPath $fixture.Base -Recurse -Force }
}

Run-Test 'Missing embedded framework assembly is rejected before sync' {
    $fixture = New-SyncFixture 'missing-embedded-assembly'
    try {
        Remove-Item -LiteralPath $fixture.Framework -Force
        $result = Invoke-Sync $fixture @('-SkipGame','-WhatIf')
        Assert-True ($result.ExitCode -ne 0) 'Missing embedded framework assembly was accepted.'
        Assert-True ($result.Output -match 'Missing build artifact') $result.Output
    } finally { Remove-Item -LiteralPath $fixture.Base -Recurse -Force }
}

Run-Test 'Game write requires explicit authorization' {
    $fixture = New-SyncFixture 'authorization'
    try {
        New-Item -ItemType Directory -Path $fixture.Game -Force | Out-Null
        $report = Join-Path $fixture.Base 'authorization-report.json'
        $result = Invoke-Sync $fixture @('-ReportPath',$report)
        Assert-True ($result.ExitCode -ne 0) 'Game sync unexpectedly succeeded without authorization.'
        Assert-True ($result.Output -match 'requires -ConfirmGameSync') $result.Output
        Assert-True (-not (Test-Path -LiteralPath $report)) 'Unauthorized sync wrote a report.'
    } finally { Remove-Item -LiteralPath $fixture.Base -Recurse -Force }
}

Run-Test 'Unknown nested Persona files block cleanup' {
    $fixture = New-SyncFixture 'unknown-nested'
    try {
        $unknown = Join-Path $fixture.Dist 'ModuleData\Worldbook\persona_definitions\persona_definitions\unknown.json'
        Write-TestFile $unknown '{}'
        $result = Invoke-Sync $fixture @('-SkipGame','-WhatIf')
        Assert-True ($result.ExitCode -ne 0) 'Unknown nested Persona file was accepted.'
        Assert-True ($result.Output -match 'Unexpected nested Persona file') $result.Output
        Assert-True (Test-Path -LiteralPath $unknown -PathType Leaf) 'Unknown file was modified.'
    } finally { Remove-Item -LiteralPath $fixture.Base -Recurse -Force }
}

Run-Test 'File-directory conflicts are rejected' {
    $fixture = New-SyncFixture 'path-conflict'
    try {
        New-Item -ItemType Directory -Path (Join-Path $fixture.Dist 'SubModule.xml') -Force | Out-Null
        $result = Invoke-Sync $fixture @('-SkipGame','-WhatIf')
        Assert-True ($result.ExitCode -ne 0) 'Managed file-directory conflict was accepted.'
        Assert-True ($result.Output -match 'Managed target is a directory') $result.Output
    } finally { Remove-Item -LiteralPath $fixture.Base -Recurse -Force }
}

Run-Test 'Dist repair copies canonical Persona files and preserves unknown files' {
    $fixture = New-SyncFixture 'dist-repair'
    try {
        Write-TestFile (Join-Path $fixture.Dist 'ModuleData\Worldbook\persona_definitions\persona_definitions\tag_registry.json') '{"tags":[]}'
        Write-TestFile (Join-Path $fixture.Dist 'ModuleData\Worldbook\persona_definitions\persona_definitions\definitions\hero_default.json') '{"characterId":"hero.default"}'
        Write-TestFile (Join-Path $fixture.Dist 'custom-user-file.txt') 'preserve-me'
        $report = Join-Path $fixture.Base 'dist-report.json'
        $result = Invoke-Sync $fixture @('-SkipGame','-ReportPath',$report)
        Assert-True ($result.ExitCode -eq 0) $result.Output
        Assert-True (Test-Path -LiteralPath (Join-Path $fixture.Dist 'ModuleData\Worldbook\persona_definitions\tag_registry.json') -PathType Leaf) 'Canonical registry missing.'
        Assert-True (Test-Path -LiteralPath (Join-Path $fixture.Dist 'ModuleData\Worldbook\persona_definitions\definitions\hero_default.json') -PathType Leaf) 'Canonical definition missing.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $fixture.Dist 'ModuleData\Worldbook\persona_definitions\persona_definitions'))) 'Nested Persona root remains.'
        Assert-True ((Get-Content -LiteralPath (Join-Path $fixture.Dist 'custom-user-file.txt') -Raw) -eq 'preserve-me') 'Unknown file changed.'
        $reportValue = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
        Assert-True ($reportValue.state -eq 'verified') 'Dist report was not verified.'
    } finally { Remove-Item -LiteralPath $fixture.Base -Recurse -Force }
}

Run-Test 'Injected failure restores files and created directories' {
    $fixture = New-SyncFixture 'rollback'
    try {
        New-Item -ItemType Directory -Path $fixture.Dist -Force | Out-Null
        Write-TestFile (Join-Path $fixture.Dist 'SubModule.xml') 'old-submodule'
        Write-TestFile (Join-Path $fixture.Dist 'custom-user-file.txt') 'preserve-me'
        $beforeFiles = @(Get-ChildItem -LiteralPath $fixture.Dist -Recurse -File | ForEach-Object { $_.FullName.Substring($fixture.Dist.Length + 1) + '=' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } | Sort-Object)
        $beforeDirectories = @(Get-ChildItem -LiteralPath $fixture.Dist -Recurse -Directory | ForEach-Object { $_.FullName.Substring($fixture.Dist.Length + 1) } | Sort-Object)
        $report = Join-Path $fixture.Base 'rollback-report.json'
        $result = Invoke-Sync $fixture @('-SkipGame','-TestFailAfterCopies','1','-ReportPath',$report)
        Assert-True ($result.ExitCode -ne 0) 'Injected failure unexpectedly succeeded.'
        $afterFiles = @(Get-ChildItem -LiteralPath $fixture.Dist -Recurse -File | ForEach-Object { $_.FullName.Substring($fixture.Dist.Length + 1) + '=' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } | Sort-Object)
        $afterDirectories = @(Get-ChildItem -LiteralPath $fixture.Dist -Recurse -Directory | ForEach-Object { $_.FullName.Substring($fixture.Dist.Length + 1) } | Sort-Object)
        Assert-True (($beforeFiles -join "`n") -eq ($afterFiles -join "`n")) 'Rollback file snapshot differs.'
        Assert-True (($beforeDirectories -join "`n") -eq ($afterDirectories -join "`n")) 'Rollback directory snapshot differs.'
        $reportValue = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
        Assert-True ($reportValue.state -eq 'rollback_verified') ('Unexpected rollback state: ' + $reportValue.state)
        Assert-True ($reportValue.rollback.verified -eq $true) 'Rollback was not verified.'
    } finally { Remove-Item -LiteralPath $fixture.Base -Recurse -Force }
}

Run-Test 'Worldbook registry without its package is rejected before any copy' {
    # Negative case for Assert-SourceManifest (2026-09-14 package form). Without this, the new
    # assertion is only ever seen passing == never actually tested.
    $fixture = New-SyncFixture 'worldbook-package-missing' -OmitWorldbookPackage
    try {
        $result = Invoke-Sync $fixture @('-SkipGame','-WhatIf')
        Assert-True ($result.ExitCode -ne 0) 'Sync accepted a registry whose package is absent.'
        Assert-True ($result.Output -match 'Worldbook registry package') ('Sync failed without naming the missing package: ' + $result.Output)
    } finally { Remove-Item -LiteralPath $fixture.Base -Recurse -Force }
}

Run-Test 'Worldbook package missing a declared entrypoint is rejected before any copy' {
    $fixture = New-SyncFixture 'worldbook-entrypoint-missing'
    try {
        Remove-Item -LiteralPath (Join-Path $fixture.Project 'ModuleData\Worldbook\packages\calradia\index.json') -Force
        $result = Invoke-Sync $fixture @('-SkipGame','-WhatIf')
        Assert-True ($result.ExitCode -ne 0) 'Sync accepted a package whose index entrypoint is absent.'
        Assert-True ($result.Output -match 'entrypoints.index') ('Sync failed without naming the missing entrypoint: ' + $result.Output)
    } finally { Remove-Item -LiteralPath $fixture.Base -Recurse -Force }
}

Write-Output "PASS ALL sync_module tests=$script:Passed"
exit 0
