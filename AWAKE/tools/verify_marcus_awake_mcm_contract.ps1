[CmdletBinding()]
param(
    [string]$ProjectRoot = ''
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path -Parent $PSScriptRoot
}

$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot)
$script:PassCount = 0
$script:Failures = @()

function Add-Pass {
    param([string]$Message)

    $script:PassCount++
    Write-Output ('PASS ' + $Message)
}

function Add-Fail {
    param([string]$Message)

    $script:Failures += $Message
    Write-Output ('FAIL ' + $Message)
}

function Read-SourceText {
    param([string]$RelativePath)

    $fullPath = Join-Path $ProjectRoot $RelativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        Add-Fail ('source file missing: ' + $RelativePath)
        return ''
    }

    try {
        return [IO.File]::ReadAllText([IO.Path]::GetFullPath($fullPath))
    }
    catch {
        Add-Fail ('source file unreadable: ' + $RelativePath)
        return ''
    }
}

function Assert-ContainsAll {
    param(
        [string]$Text,
        [object[]]$Requirements,
        [string]$Label
    )

    $missing = @()
    foreach ($requirement in $Requirements) {
        if ([string]::IsNullOrEmpty($Text) -or $Text -notmatch $requirement.Pattern) {
            $missing += $requirement.Name
        }
    }

    if ($missing.Count -eq 0) {
        Add-Pass $Label
    }
    else {
        Add-Fail ($Label + '; missing=' + ($missing -join ','))
    }
}

function Assert-NotContains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Label
    )

    if (-not [string]::IsNullOrEmpty($Text) -and $Text -match $Pattern) {
        Add-Fail $Label
    }
    else {
        Add-Pass $Label
    }
}

function Find-MatchingBrace {
    param(
        [string]$Text,
        [int]$OpenBraceIndex
    )

    $braceDepth = 0
    $inString = $false
    $inCharacter = $false
    $inLineComment = $false
    $inBlockComment = $false
    $escapeNext = $false

    for ($position = $OpenBraceIndex; $position -lt $Text.Length; $position++) {
        $character = $Text[$position]
        $nextCharacter = [char]0
        if (($position + 1) -lt $Text.Length) {
            $nextCharacter = $Text[$position + 1]
        }

        if ($inLineComment) {
            if ($character -eq [char]0x0A) {
                $inLineComment = $false
            }
            continue
        }

        if ($inBlockComment) {
            if ($character -eq [char]0x2A -and $nextCharacter -eq [char]0x2F) {
                $inBlockComment = $false
                $position++
            }
            continue
        }

        if ($inString) {
            if ($escapeNext) {
                $escapeNext = $false
            }
            elseif ($character -eq [char]0x5C) {
                $escapeNext = $true
            }
            elseif ($character -eq [char]0x22) {
                $inString = $false
            }
            continue
        }

        if ($inCharacter) {
            if ($escapeNext) {
                $escapeNext = $false
            }
            elseif ($character -eq [char]0x5C) {
                $escapeNext = $true
            }
            elseif ($character -eq [char]0x27) {
                $inCharacter = $false
            }
            continue
        }

        if ($character -eq [char]0x2F -and $nextCharacter -eq [char]0x2F) {
            $inLineComment = $true
            $position++
            continue
        }

        if ($character -eq [char]0x2F -and $nextCharacter -eq [char]0x2A) {
            $inBlockComment = $true
            $position++
            continue
        }

        if ($character -eq [char]0x22) {
            $inString = $true
            continue
        }

        if ($character -eq [char]0x27) {
            $inCharacter = $true
            continue
        }

        if ($character -eq [char]0x7B) {
            $braceDepth++
        }
        elseif ($character -eq [char]0x7D) {
            $braceDepth--
            if ($braceDepth -eq 0) {
                return $position
            }
        }
    }

    return -1
}

function Get-MethodBodies {
    param(
        [string]$Text,
        [string]$MethodName
    )

    $methodPattern = '(?ms)\b(?:public|private|protected|internal)\s+(?:(?:static|async|virtual|override|sealed|extern|new)\s+)*[\w<>\[\],.?]+\s+' + [regex]::Escape($MethodName) + '\s*\([^;{}]*\)\s*\{'
    $methodMatches = [regex]::Matches($Text, $methodPattern)
    $bodies = @()

    foreach ($methodMatch in $methodMatches) {
        $openBraceIndex = $Text.IndexOf('{', $methodMatch.Index)
        if ($openBraceIndex -lt 0) {
            continue
        }

        $closeBraceIndex = Find-MatchingBrace -Text $Text -OpenBraceIndex $openBraceIndex
        if ($closeBraceIndex -lt 0) {
            continue
        }

        $bodies += $Text.Substring($openBraceIndex, $closeBraceIndex - $openBraceIndex + 1)
    }

    return @($bodies)
}

function Assert-MethodContainsAll {
    param(
        [string]$Text,
        [string]$MethodName,
        [object[]]$Requirements,
        [string]$Label
    )

    $bodies = @(Get-MethodBodies -Text $Text -MethodName $MethodName)
    if ($bodies.Count -eq 0) {
        Add-Fail ($Label + '; method missing')
        return
    }

    $bestMissing = $null
    foreach ($body in $bodies) {
        $missing = @()
        foreach ($requirement in $Requirements) {
            if ($body -notmatch $requirement.Pattern) {
                $missing += $requirement.Name
            }
        }

        if ($missing.Count -eq 0) {
            Add-Pass $Label
            return
        }

        if ($null -eq $bestMissing -or $missing.Count -lt $bestMissing.Count) {
            $bestMissing = $missing
        }
    }

    Add-Fail ($Label + '; missing=' + ($bestMissing -join ','))
}

function Assert-PropertyContract {
    param(
        [string]$Text,
        [string]$Name,
        [string]$ExpectedType,
        [string]$SettingAttribute
    )

    $pattern = '(?ms)(?<attributes>(?:\s*\[[^\]]+\]\s*)*)public\s+' + [regex]::Escape($ExpectedType) + '\s+' + [regex]::Escape($Name) + '\s*\{\s*get;\s*set;\s*\}'
    $matches = [regex]::Matches($Text, $pattern)
    $label = 'MCM member ' + $Name + ' has type ' + $ExpectedType + ' and ' + $SettingAttribute

    if ($matches.Count -ne 1) {
        Add-Fail ($label + '; declaration count=' + $matches.Count)
        return
    }

    $attributes = $matches[0].Groups['attributes'].Value
    if ($attributes -match '(?i)JsonIgnore') {
        Add-Fail ($label + '; unexpected JsonIgnore')
        return
    }

    if ($attributes -notmatch [regex]::Escape($SettingAttribute)) {
        Add-Fail ($label + '; setting attribute missing')
        return
    }

    Add-Pass $label
}

function Get-ProductionSourceFiles {
    param([string[]]$Roots)

    $files = @()
    foreach ($relativeRoot in $Roots) {
        $rootPath = Join-Path $ProjectRoot $relativeRoot
        if (-not (Test-Path -LiteralPath $rootPath -PathType Container)) {
            Add-Fail ('production source root missing: ' + $relativeRoot)
            continue
        }

        $files += @(Get-ChildItem -LiteralPath $rootPath -Recurse -File -Filter '*.cs' | Where-Object {
            $_.FullName -notmatch '\\(?:obj|bin|dist|tests)\\'
        })
    }

    return @($files | Sort-Object FullName -Unique)
}

Write-Output 'EVIDENCE_LEVEL=E0_STATIC_SOURCE_ONLY'
Write-Output 'REAL_KEY_READ=NO'
Write-Output 'NETWORK_EXECUTED=NO'
Write-Output 'RUNTIME_STARTED=NO'
Write-Output 'GAMEPLAY_VERIFIED=NO'

$sourceMap = [ordered]@{
    AwakeConfig = 'src\AwakeConfig.cs'
    AwakeMcmActions = 'src\AwakeMcmActions.cs'
    AwakeProviderConfiguration = 'src\AwakeProviderConfiguration.cs'
    AiTaskConstants = 'src\AiTaskConstants.cs'
    ProbeExtension = 'src\ProbeExtension.cs'
    AwakeHostComposition = 'src\AwakeHostComposition.cs'
    ProviderRuntimeApi = 'framework\MarcusAwakeFramework\src\ProviderRuntimeApi.cs'
    RuntimeServiceClient = 'framework\MarcusAwakeFramework\src\RuntimeServiceClient.cs'
    RuntimeServiceHost = 'framework\MarcusAwakeRuntimeService\src\RuntimeServiceHost.cs'
    ProviderRegistry = 'framework\MarcusAwakeRuntimeService\src\ProviderRegistry.cs'
    ProviderAdapterFactory = 'framework\MarcusAwakeProvider\src\ProviderAdapterFactory.cs'
    Credentials = 'framework\MarcusAwakeProvider\src\Credentials.cs'
    ProtectedFileCredentialStore = 'framework\MarcusAwakeProvider\src\ProtectedFileCredentialStore.cs'
}

$sourceTexts = @{}
foreach ($entry in $sourceMap.GetEnumerator()) {
    $sourceTexts[$entry.Key] = Read-SourceText -RelativePath $entry.Value
}

try {
    $configText = $sourceTexts['AwakeConfig']
    $actionsText = $sourceTexts['AwakeMcmActions']
    $providerText = $sourceTexts['AwakeProviderConfiguration']
    $routesText = $sourceTexts['AiTaskConstants']
    $probeText = $sourceTexts['ProbeExtension']
    $hostText = $sourceTexts['AwakeHostComposition']
    $apiText = $sourceTexts['ProviderRuntimeApi']
    $clientText = $sourceTexts['RuntimeServiceClient']
    $runtimeText = $sourceTexts['RuntimeServiceHost']
    $registryText = $sourceTexts['ProviderRegistry']
    $factoryText = $sourceTexts['ProviderAdapterFactory']
    $credentialsText = $sourceTexts['Credentials']
    $protectedStoreText = $sourceTexts['ProtectedFileCredentialStore']

    Assert-PropertyContract -Text $configText -Name 'ProviderKind' -ExpectedType 'Dropdown<string>' -SettingAttribute 'SettingPropertyDropdown'
    Assert-PropertyContract -Text $configText -Name 'ProviderBaseUrl' -ExpectedType 'string' -SettingAttribute 'SettingPropertyText'
    Assert-PropertyContract -Text $configText -Name 'ProviderModel' -ExpectedType 'string' -SettingAttribute 'SettingPropertyText'
    Assert-PropertyContract -Text $configText -Name 'ProviderIsCloud' -ExpectedType 'bool' -SettingAttribute 'SettingPropertyBool'

    Assert-PropertyContract -Text $configText -Name 'ConfigureProviderApiKey' -ExpectedType 'Action' -SettingAttribute 'SettingPropertyButton'
    Assert-PropertyContract -Text $configText -Name 'ApplyProviderConfiguration' -ExpectedType 'Action' -SettingAttribute 'SettingPropertyButton'
    Assert-PropertyContract -Text $configText -Name 'PullProviderModels' -ExpectedType 'Action' -SettingAttribute 'SettingPropertyButton'
    Assert-PropertyContract -Text $configText -Name 'TestProviderConnection' -ExpectedType 'Action' -SettingAttribute 'SettingPropertyButton'
    Assert-PropertyContract -Text $configText -Name 'RefreshAiStatus' -ExpectedType 'Action' -SettingAttribute 'SettingPropertyButton'

    Assert-ContainsAll -Text $configText -Label 'MCM constructor assigns all provider actions' -Requirements @(
        @{ Name = 'ConfigureProviderApiKey assignment'; Pattern = 'ConfigureProviderApiKey\s*=\s*AwakeMcmActions\.ConfigureProviderApiKey\s*;' },
        @{ Name = 'ApplyProviderConfiguration assignment'; Pattern = 'ApplyProviderConfiguration\s*=\s*AwakeMcmActions\.ApplyProviderConfiguration\s*;' },
        @{ Name = 'PullProviderModels assignment'; Pattern = 'PullProviderModels\s*=\s*AwakeMcmActions\.PullProviderModels\s*;' },
        @{ Name = 'TestProviderConnection assignment'; Pattern = 'TestProviderConnection\s*=\s*AwakeMcmActions\.TestProviderConnection\s*;' },
        @{ Name = 'RefreshAiStatus assignment'; Pattern = 'RefreshAiStatus\s*=\s*AwakeMcmActions\.RefreshAiStatus\s*;' }
    )

    Assert-MethodContainsAll -Text $actionsText -MethodName 'ConfigureProviderApiKey' -Label 'MCM API key action reaches AwakeProviderConfiguration' -Requirements @(
        @{ Name = 'PromptForApiKey'; Pattern = 'AwakeProviderConfiguration\.PromptForApiKey\s*\(\s*\)' }
    )
    Assert-MethodContainsAll -Text $actionsText -MethodName 'ApplyProviderConfiguration' -Label 'MCM save/apply action reaches AwakeProviderConfiguration' -Requirements @(
        @{ Name = 'ApplyProviderConfiguration'; Pattern = 'AwakeProviderConfiguration\.ApplyProviderConfiguration\s*\(\s*\)' }
    )
    Assert-MethodContainsAll -Text $actionsText -MethodName 'PullProviderModels' -Label 'MCM pull-model action reaches AwakeProviderConfiguration' -Requirements @(
        @{ Name = 'PullModels'; Pattern = 'AwakeProviderConfiguration\.PullModels\s*\(\s*\)' }
    )
    Assert-MethodContainsAll -Text $actionsText -MethodName 'TestProviderConnection' -Label 'MCM test-connection action reaches AwakeProviderConfiguration' -Requirements @(
        @{ Name = 'TestConnection'; Pattern = 'AwakeProviderConfiguration\.TestConnection\s*\(\s*\)' }
    )
    Assert-MethodContainsAll -Text $actionsText -MethodName 'RefreshAiStatus' -Label 'MCM status-refresh action reaches AwakeProviderConfiguration' -Requirements @(
        @{ Name = 'RefreshRuntimeStatus'; Pattern = 'AwakeProviderConfiguration\.RefreshRuntimeStatus\s*\(\s*\)' }
    )

    Assert-ContainsAll -Text $configText -Label 'MCM save path reaches BaseSettingsProvider.SaveSettings' -Requirements @(
        @{ Name = 'AwakeSettings'; Pattern = 'internal\s+static\s+class\s+AwakeSettings' },
        @{ Name = 'TrySaveCurrentConfiguration'; Pattern = 'TrySaveCurrentConfiguration\s*\(' },
        @{ Name = 'SaveSettings'; Pattern = 'BaseSettingsProvider\.Instance\.?\s*SaveSettings\s*\(\s*config\s*\)' }
    )
    Assert-MethodContainsAll -Text $providerText -MethodName 'TryPrepareConfiguration' -Label 'Provider preparation saves current MCM configuration' -Requirements @(
        @{ Name = 'snapshot'; Pattern = 'TryCaptureSnapshot\s*\(' },
        @{ Name = 'MCM save'; Pattern = 'AwakeSettings\.TrySaveCurrentConfiguration\s*\(' }
    )
    Assert-MethodContainsAll -Text $providerText -MethodName 'ApplyProviderConfiguration' -Label 'Save/apply action reaches profile application' -Requirements @(
        @{ Name = 'prepare'; Pattern = 'TryPrepareConfiguration\s*\(' },
        @{ Name = 'profile application'; Pattern = 'ApplyProfilesAsync\s*\(' }
    )
    Assert-MethodContainsAll -Text $providerText -MethodName 'ApplyProfilesAsync' -Label 'Profile application covers every AWAKE route and provider upsert' -Requirements @(
        @{ Name = 'all routes'; Pattern = 'foreach\s*\(\s*string\s+routeId\s+in\s+AiTaskConstants\.AllRouteIds\s*\)' },
        @{ Name = 'profile request'; Pattern = 'new\s+ProviderProfileRequest\s*\(' },
        @{ Name = 'profile upsert'; Pattern = 'provider\.UpsertProfileAsync\s*\(' }
    )

    Assert-MethodContainsAll -Text $providerText -MethodName 'PromptForApiKey' -Label 'API key input action enters credential-save path' -Requirements @(
        @{ Name = 'text inquiry'; Pattern = 'InformationManager\.ShowTextInquiry\s*\(' },
        @{ Name = 'credential save'; Pattern = 'BeginCredentialSave\s*\(' }
    )
    Assert-MethodContainsAll -Text $providerText -MethodName 'BeginCredentialSave' -Label 'API key input reaches credential operation' -Requirements @(
        @{ Name = 'SaveCredentialAsync'; Pattern = 'SaveCredentialAsync\s*\(' }
    )
    Assert-MethodContainsAll -Text $providerText -MethodName 'SaveCredentialAsync' -Label 'Credential operation reaches provider credential upsert' -Requirements @(
        @{ Name = 'credential request'; Pattern = 'new\s+ProviderCredentialRequest\s*\(' },
        @{ Name = 'credential upsert'; Pattern = 'provider\.UpsertCredentialAsync\s*\(' }
    )

    Assert-MethodContainsAll -Text $providerText -MethodName 'PullModels' -Label 'Pull-model action prepares configuration and lists models' -Requirements @(
        @{ Name = 'prepare'; Pattern = 'TryPrepareConfiguration\s*\(' },
        @{ Name = 'list models'; Pattern = 'ListModelsAsync\s*\(' }
    )
    Assert-MethodContainsAll -Text $providerText -MethodName 'TestConnection' -Label 'Test-connection action prepares configuration and lists models' -Requirements @(
        @{ Name = 'prepare'; Pattern = 'TryPrepareConfiguration\s*\(' },
        @{ Name = 'list models'; Pattern = 'ListModelsAsync\s*\(' }
    )
    Assert-MethodContainsAll -Text $providerText -MethodName 'ListModelsAsync' -Label 'Model listing reapplies profiles before provider model listing' -Requirements @(
        @{ Name = 'profile application'; Pattern = 'ApplyProfilesAsync\s*\(' },
        @{ Name = 'provider models'; Pattern = 'provider\.ListModelsAsync\s*\(' }
    )

    Assert-MethodContainsAll -Text $providerText -MethodName 'RefreshRuntimeStatus' -Label 'Status refresh performs Runtime health check and UI update' -Requirements @(
        @{ Name = 'operation'; Pattern = 'StartOperation\s*\(' },
        @{ Name = 'health check'; Pattern = 'runtime\.CheckHealthAsync\s*\(' },
        @{ Name = 'status update'; Pattern = 'UpdateStatusOnUi\s*\(' }
    )
    Assert-MethodContainsAll -Text $providerText -MethodName 'UpdateStatusOnUi' -Label 'Status result updates visible AWAKE status' -Requirements @(
        @{ Name = 'AwakeSettings status'; Pattern = 'AwakeSettings\.UpdateRuntimeStatus\s*\(' }
    )
    Assert-ContainsAll -Text $configText -Label 'MCM status field reads Runtime status' -Requirements @(
        @{ Name = 'AiRuntimeStatus'; Pattern = 'public\s+string\s+AiRuntimeStatus' },
        @{ Name = 'latest text'; Pattern = 'AwakeRuntimeStatus\.LatestText' }
    )

    $routeDeclarations = [regex]::Matches($routesText, '(?m)^\s*internal\s+const\s+string\s+(?<name>Route\w+)\s*=\s*"(?<value>AWAKE\.route\.[^"]+)"\s*;')
    $allRoutesMatch = [regex]::Match($routesText, '(?ms)internal\s+static\s+readonly\s+string\[\]\s+AllRouteIds\s*=\s*new\[\]\s*\{(?<body>.*?)\}')
    if ($routeDeclarations.Count -eq 0 -or -not $allRoutesMatch.Success) {
        Add-Fail 'AWAKE route declarations or AllRouteIds block missing'
    }
    else {
        $declaredRouteNames = @($routeDeclarations | ForEach-Object { $_.Groups['name'].Value })
        $declaredRouteValues = @($routeDeclarations | ForEach-Object { $_.Groups['value'].Value })
        $allRouteNames = @([regex]::Matches($allRoutesMatch.Groups['body'].Value, '\b(?<name>Route\w+)\b') | ForEach-Object { $_.Groups['name'].Value })
        $routeFailures = @()

        foreach ($routeName in $declaredRouteNames) {
            $count = @($allRouteNames | Where-Object { $_ -ceq $routeName }).Count
            if ($count -ne 1) {
                $routeFailures += ($routeName + ':count=' + $count)
            }
        }

        foreach ($allRouteName in $allRouteNames) {
            if ($declaredRouteNames -cnotcontains $allRouteName) {
                $routeFailures += ($allRouteName + ':undeclared')
            }
        }

        $duplicateValues = @($declaredRouteValues | Group-Object | Where-Object { $_.Count -ne 1 })
        if ($duplicateValues.Count -gt 0) {
            $routeFailures += 'duplicate route literal'
        }

        if ($routeFailures.Count -eq 0) {
            Add-Pass ('All declared AWAKE routes are listed exactly once in AllRouteIds; count=' + $declaredRouteNames.Count)
        }
        else {
            Add-Fail ('AWAKE route list mismatch; details=' + ($routeFailures -join ','))
        }
    }

    Assert-ContainsAll -Text $probeText -Label 'Extension manifest receives complete AWAKE route list' -Requirements @(
        @{ Name = 'AllRouteIds'; Pattern = 'AiTaskConstants\.AllRouteIds' },
        @{ Name = 'ExtensionManifest'; Pattern = 'new\s+ExtensionManifest\s*\(' }
    )

    Assert-ContainsAll -Text $hostText -Label 'AWAKE host wires the built-in Runtime executable' -Requirements @(
        @{ Name = 'RuntimeServiceClientOptions'; Pattern = 'new\s+RuntimeServiceClientOptions\s*\(' },
        @{ Name = 'RuntimeServiceClient'; Pattern = 'new\s+RuntimeServiceClient\s*\(' },
        @{ Name = 'runtime path'; Pattern = 'Path\.Combine\(assemblyDirectory,\s*"Runtime",\s*"MarcusAwakeRuntimeService\.exe"\)' },
        @{ Name = 'host registration'; Pattern = 'FrameworkHostLocator\.Register\s*\(' }
    )
    Assert-MethodContainsAll -Text $hostText -MethodName 'StartRuntimeService' -Label 'AWAKE host starts Runtime before provider profile application' -Requirements @(
        @{ Name = 'ready gate'; Pattern = 'result\.Value\.IsReady' },
        @{ Name = 'provider port'; Pattern = 'as\s+IProviderRuntimePort' },
        @{ Name = 'profile apply'; Pattern = 'AwakeProviderConfiguration\.ApplyProfilesAsync\s*\(' }
    )

    Assert-ContainsAll -Text $apiText -Label 'Marcus provider Runtime port declares required operations' -Requirements @(
        @{ Name = 'IProviderRuntimePort'; Pattern = 'public\s+interface\s+IProviderRuntimePort' },
        @{ Name = 'profile'; Pattern = 'UpsertProfileAsync\s*\(' },
        @{ Name = 'credential'; Pattern = 'UpsertCredentialAsync\s*\(' },
        @{ Name = 'remove'; Pattern = 'RemoveProfileAsync\s*\(' },
        @{ Name = 'models'; Pattern = 'ListModelsAsync\s*\(' }
    )
    Assert-ContainsAll -Text $clientText -Label 'RuntimeServiceClient implements the provider port and health endpoint' -Requirements @(
        @{ Name = 'provider port implementation'; Pattern = 'class\s+RuntimeServiceClient\s*:\s*[^\{]*IProviderRuntimePort' },
        @{ Name = 'health'; Pattern = 'CheckHealthAsync\s*\(' },
        @{ Name = 'profile'; Pattern = 'UpsertProfileAsync\s*\(' },
        @{ Name = 'credential'; Pattern = 'UpsertCredentialAsync\s*\(' },
        @{ Name = 'models'; Pattern = 'ListModelsAsync\s*\(' }
    )
    Assert-MethodContainsAll -Text $runtimeText -MethodName 'RunAsync' -Label 'Runtime service initializes provider runtime before ready signal' -Requirements @(
        @{ Name = 'provider initialization'; Pattern = 'InitializeProviderRuntime\s*\(' },
        @{ Name = 'ready signal'; Pattern = 'WriteServiceReadyAsync\s*\(' }
    )
    Assert-ContainsAll -Text $runtimeText -Label 'Runtime service dispatches provider profile, credential, and model operations' -Requirements @(
        @{ Name = 'business dispatch'; Pattern = 'HandleProviderBusinessAsync\s*\(' },
        @{ Name = 'profile dispatch'; Pattern = 'ProviderWireOperation\.ProfileUpsert' },
        @{ Name = 'credential dispatch'; Pattern = 'ProviderWireOperation\.CredentialUpsert' },
        @{ Name = 'model dispatch'; Pattern = 'ProviderWireOperation\.Models' },
        @{ Name = 'registry profile'; Pattern = 'providerRegistry\.Upsert\s*\(' },
        @{ Name = 'registry credential'; Pattern = 'providerRegistry\.UpsertCredentialAsync\s*\(' },
        @{ Name = 'registry models'; Pattern = 'providerRegistry\.ListModelsAsync\s*\(' }
    )
    Assert-MethodContainsAll -Text $runtimeText -MethodName 'InitializeProviderRuntime' -Label 'Runtime service creates protected credential store and provider registry' -Requirements @(
        @{ Name = 'provider assembly'; Pattern = 'ProviderRegistry\.ResolveProviderAssemblyPath\s*\(' },
        @{ Name = 'protected store'; Pattern = 'ProviderRegistry\.TryCreateCredentialStore\s*\(' },
        @{ Name = 'HTTP invoker'; Pattern = 'providerInvoker\s*=\s*new\s+HttpMessageInvoker' },
        @{ Name = 'provider registry'; Pattern = 'providerRegistry\s*=\s*new\s+ProviderRegistry' }
    )
    Assert-ContainsAll -Text $registryText -Label 'Provider registry persists credentials and lists models through adapters' -Requirements @(
        @{ Name = 'ProviderRegistry'; Pattern = 'internal\s+sealed\s+class\s+ProviderRegistry' },
        @{ Name = 'credential store type'; Pattern = 'ProtectedFileCredentialStore' },
        @{ Name = 'credential save'; Pattern = 'FindMethod\s*\([^\r\n]*SaveAsync' },
        @{ Name = 'provider adapter'; Pattern = 'CreateAdapter\s*\(' },
        @{ Name = 'model list'; Pattern = 'ListModelsAsync\s*\(' }
    )
    Assert-ContainsAll -Text $factoryText -Label 'Provider adapter factory supports all configured provider kinds' -Requirements @(
        @{ Name = 'OpenAI compatible'; Pattern = 'ProviderKind\.OpenAiCompatible' },
        @{ Name = 'Anthropic'; Pattern = 'ProviderKind\.Anthropic' },
        @{ Name = 'Ollama'; Pattern = 'ProviderKind\.Ollama' },
        @{ Name = 'factory'; Pattern = 'class\s+ProviderAdapterFactory' }
    )
    Assert-ContainsAll -Text $credentialsText -Label 'Provider credential contract keeps secret material separate from references' -Requirements @(
        @{ Name = 'ApiKeyCredential'; Pattern = 'class\s+ApiKeyCredential' },
        @{ Name = 'owned secret buffer'; Pattern = 'char\[\]\s+secret' },
        @{ Name = 'copy for request'; Pattern = 'CopySecretForRequest\s*\(' },
        @{ Name = 'credential store interface'; Pattern = 'interface\s+IProviderCredentialStore' }
    )
    Assert-ContainsAll -Text $protectedStoreText -Label 'Protected credential store has save, read, and delete paths' -Requirements @(
        @{ Name = 'store'; Pattern = 'class\s+ProtectedFileCredentialStore' },
        @{ Name = 'save'; Pattern = 'SaveAsync\s*\(' },
        @{ Name = 'read'; Pattern = 'GetAsync\s*\(' },
        @{ Name = 'delete'; Pattern = 'DeleteAsync\s*\(' },
        @{ Name = 'protection'; Pattern = 'Protect\s*\(' }
    )

    Assert-MethodContainsAll -Text $credentialsText -MethodName 'ToString' -Label 'Credential ToString does not expose secret contents' -Requirements @(
        @{ Name = 'reference/state only'; Pattern = 'Reference' },
        @{ Name = 'state marker'; Pattern = 'state' }
    )
    $toStringBodies = @(Get-MethodBodies -Text $credentialsText -MethodName 'ToString')
    if ($toStringBodies.Count -eq 0) {
        Add-Fail 'Credential ToString secret scan; method missing'
    }
    else {
        $unsafeToString = @($toStringBodies | Where-Object { $_ -match '(?i)secret|CopySecret|GetSecret' })
        if ($unsafeToString.Count -eq 0) {
            Add-Pass 'Credential ToString secret scan'
        }
        else {
            Add-Fail 'Credential ToString secret scan; secret-bearing expression found'
        }
    }

    $secretMemberPattern = '(?im)^\s*(?:public|private|protected|internal)\s+(?:static\s+)?(?<type>[\w<>\[\].?]+)\s+(?<name>\w*(?:ApiKey|Secret|Token|Password|Credential)\w*)\s*(?:\{|=|;)'
    $secretMembers = @([regex]::Matches($configText, $secretMemberPattern))
    $unexpectedSecretMembers = @($secretMembers | Where-Object { $_.Groups['type'].Value -notmatch '(?i)^Action$' })
    if ($unexpectedSecretMembers.Count -eq 0) {
        Add-Pass 'AwakeConfig has no serialized secret-bearing value member; API key surface is Action-only'
    }
    else {
        Add-Fail 'AwakeConfig has a secret-bearing non-Action member'
    }

    $mcmSecretValuePattern = '(?is)\[SettingProperty[^\]]*\].{0,500}?public\s+(?!Action\b)[\w<>\[\].?]+\s+\w*(?:ApiKey|Secret|Token|Password|Credential)\w*\s*\{'
    Assert-NotContains -Text $configText -Pattern $mcmSecretValuePattern -Label 'MCM attributes do not decorate a secret-bearing value member'

    $productionRoots = @(
        'src',
        'framework\MarcusAwakeFramework\src',
        'framework\MarcusAwakeRuntimeService\src',
        'framework\MarcusAwakeProvider\src'
    )
    $productionFiles = @(Get-ProductionSourceFiles -Roots $productionRoots)
    $loggerSecretPattern = '(?is)(?:AwakeLog\.Write|Console\.(?:Error\.)?WriteLine|Debug\.WriteLine|Trace\.WriteLine)\s*\([^;]*(?:\bsecret\b|\bapiKey\b|\bapi_key\b|\bcredentialSecret\b|\bpassword\b|\btoken\b|\bserviceKey\b|\bframeKey\b|\bbootstrapKeyMaterial\b)'
    $literalSecretPattern = '(?i)(?:sk-ant-[A-Za-z0-9_-]{10,}|sk-[A-Za-z0-9_-]{16,}|AIza[A-Za-z0-9_-]{20,}|(?:ghp|github_pat|xox[baprs])-[A-Za-z0-9_-]{16,}|AKIA[0-9A-Z]{16}|Bearer\s+[A-Za-z0-9._~-]{24,}|eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,})'
    $loggerFindings = @()
    $literalFindings = @()

    foreach ($file in $productionFiles) {
        $text = [IO.File]::ReadAllText($file.FullName)
        if ($text -match $loggerSecretPattern) {
            $loggerFindings += $file.Name
        }
        if ($text -match $literalSecretPattern) {
            $literalFindings += $file.Name
        }
    }

    if ($loggerFindings.Count -eq 0) {
        Add-Pass 'Production logger calls do not concatenate secret-bearing identifiers'
    }
    else {
        Add-Fail 'Production logger calls contain secret-bearing identifiers in: ' + (($loggerFindings | Sort-Object -Unique) -join ',')
    }

    if ($literalFindings.Count -eq 0) {
        Add-Pass 'No secret-like API key literal found in production source'
    }
    else {
        Add-Fail 'Secret-like literal found in production source: ' + (($literalFindings | Sort-Object -Unique) -join ',')
    }
}
catch {
    Add-Fail 'verifier exception; source values were not emitted'
}

Write-Output ('SUMMARY passes=' + $script:PassCount + ' failures=' + $script:Failures.Count)
if ($script:Failures.Count -gt 0) {
    Write-Output 'MARCUS_AWAKE_MCM_CONTRACT=FAIL'
    exit 1
}

Write-Output 'MARCUS_AWAKE_MCM_CONTRACT=PASS'
exit 0
