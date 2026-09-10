[CmdletBinding()]
param(
    [Alias('ProjectRoot')]
    [string]$SourceRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$script:Checks = @()
$script:SourceRecords = @()
$script:SourceByName = @{}
$script:SourceByRelativePath = @{}
$script:SourceRootResolved = ''

function Get-FullPath([string]$Path) {
    return [System.IO.Path]::GetFullPath($Path)
}

function Get-RelativePath([string]$Root, [string]$Path) {
    $normalizedRoot = $Root
    while ($normalizedRoot.EndsWith('\') -or $normalizedRoot.EndsWith('/') ) {
        $normalizedRoot = $normalizedRoot.Substring(0, $normalizedRoot.Length - 1)
    }
    $normalizedPath = Get-FullPath $Path
    if ($normalizedPath.StartsWith($normalizedRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $normalizedPath.Substring($normalizedRoot.Length + 1).Replace('\', '/')
    }
    return $normalizedPath.Replace('\', '/')
}

function Get-SourceRecord([string]$FileName) {
    if ([string]::IsNullOrWhiteSpace($FileName)) { return $null }
    $normalized = $FileName.Replace('\', '/')
    if ($script:SourceByRelativePath.ContainsKey($normalized)) {
        return $script:SourceByRelativePath[$normalized]
    }
    $leaf = [System.IO.Path]::GetFileName($FileName)
    if ($script:SourceByName.ContainsKey($leaf)) {
        return $script:SourceByName[$leaf]
    }
    return $null
}

function Get-PatternMatches([string]$FileName, [string]$Pattern) {
    $source = Get-SourceRecord $FileName
    if ($null -eq $source) { return @() }
    $regexMatches = [System.Text.RegularExpressions.Regex]::Matches(
        $source.Text,
        $Pattern,
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $results = New-Object 'System.Collections.Generic.List[object]'
    foreach ($regexMatch in $regexMatches) {
        $prefix = $source.Text.Substring(0, $regexMatch.Index)
        $lineNumber = ([System.Text.RegularExpressions.Regex]::Matches($prefix, "`n")).Count + 1
        $results.Add([ordered]@{
                path = $source.RelativePath
                line = $lineNumber
                text = $source.Lines[$lineNumber - 1].Trim()
            }) | Out-Null
    }
    return $results.ToArray()
}

function Test-LineOrder([object[]]$Before, [object[]]$After) {
    foreach ($beforeEvidence in @($Before)) {
        foreach ($afterEvidence in @($After)) {
            if ([int]$beforeEvidence.line -lt [int]$afterEvidence.line) { return $true }
        }
    }
    return $false
}

function Add-Check(
    [string]$Id,
    [string]$Title,
    [bool]$Passed,
    [string]$Message,
    [object]$Evidence,
    [object]$Details) {
    $normalizedEvidence = @()
    if ($null -ne $Evidence) { $normalizedEvidence = @($Evidence) }
    $script:Checks += [ordered]@{
        id = $Id
        title = $Title
        status = if ($Passed) { 'PASS' } else { 'FAIL' }
        message = $Message
        evidence = $normalizedEvidence
        details = $Details
    }
}

function Get-SourceRuleFindings([string]$RuleId, [string]$Pattern) {
    $findings = New-Object 'System.Collections.Generic.List[object]'
    foreach ($source in $script:SourceRecords) {
        foreach ($evidence in @(Get-PatternMatches $source.RelativePath $Pattern)) {
            $findings.Add([ordered]@{
                    rule = $RuleId
                    path = $evidence.path
                    line = $evidence.line
                    text = $evidence.text
                }) | Out-Null
        }
    }
    return $findings.ToArray()
}

function Mask-CSharpNonCode([string]$Text) {
    if ($null -eq $Text) { return '' }
    $builder = New-Object System.Text.StringBuilder
    $state = 'Code'
    $verbatimString = $false
    $length = $Text.Length

    for ($index = 0; $index -lt $length; $index++) {
        $character = $Text[$index]
        $next = [char]0
        if ($index + 1 -lt $length) { $next = $Text[$index + 1] }

        if ($state -eq 'Code') {
            if ($character -eq '/' -and $next -eq '/') {
                $builder.Append('  ') | Out-Null
                $state = 'LineComment'
                $index++
                continue
            }
            if ($character -eq '/' -and $next -eq '*') {
                $builder.Append('  ') | Out-Null
                $state = 'BlockComment'
                $index++
                continue
            }
            if ($character -eq '"') {
                $verbatimString = $index -gt 0 -and $Text[$index - 1] -eq '@'
                $builder.Append(' ') | Out-Null
                $state = 'String'
                continue
            }
            if ($character -eq "'") {
                $verbatimString = $false
                $builder.Append(' ') | Out-Null
                $state = 'Char'
                continue
            }
            $builder.Append($character) | Out-Null
            continue
        }

        if ($state -eq 'LineComment') {
            if ($character -eq [char]13 -or $character -eq [char]10) {
                $builder.Append($character) | Out-Null
                $state = 'Code'
            } else {
                $builder.Append(' ') | Out-Null
            }
            continue
        }

        if ($state -eq 'BlockComment') {
            if ($character -eq '*' -and $next -eq '/') {
                $builder.Append('  ') | Out-Null
                $state = 'Code'
                $index++
            } elseif ($character -eq [char]13 -or $character -eq [char]10) {
                $builder.Append($character) | Out-Null
            } else {
                $builder.Append(' ') | Out-Null
            }
            continue
        }

        if ($state -eq 'String') {
            if ($verbatimString) {
                if ($character -eq '"' -and $next -eq '"') {
                    $builder.Append('  ') | Out-Null
                    $index++
                } elseif ($character -eq '"') {
                    $builder.Append(' ') | Out-Null
                    $state = 'Code'
                } elseif ($character -eq [char]13 -or $character -eq [char]10) {
                    $builder.Append($character) | Out-Null
                } else {
                    $builder.Append(' ') | Out-Null
                }
            } elseif ($character -eq '\' -and $index + 1 -lt $length) {
                $builder.Append('  ') | Out-Null
                $index++
            } elseif ($character -eq '"') {
                $builder.Append(' ') | Out-Null
                $state = 'Code'
            } elseif ($character -eq [char]13 -or $character -eq [char]10) {
                $builder.Append($character) | Out-Null
            } else {
                $builder.Append(' ') | Out-Null
            }
            continue
        }

        if ($character -eq '\' -and $index + 1 -lt $length) {
            $builder.Append('  ') | Out-Null
            $index++
        } elseif ($character -eq "'") {
            $builder.Append(' ') | Out-Null
            $state = 'Code'
        } elseif ($character -eq [char]13 -or $character -eq [char]10) {
            $builder.Append($character) | Out-Null
        } else {
            $builder.Append(' ') | Out-Null
        }
    }

    return $builder.ToString()
}

function Get-CodePatternMatches([string]$FileName, [string]$Pattern) {
    $source = Get-SourceRecord $FileName
    if ($null -eq $source) { return @() }
    $maskedText = $source.CodeText
    $regexMatches = [System.Text.RegularExpressions.Regex]::Matches(
        $maskedText,
        $Pattern,
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $results = New-Object 'System.Collections.Generic.List[object]'
    foreach ($regexMatch in $regexMatches) {
        $prefix = $source.Text.Substring(0, $regexMatch.Index)
        $lineNumber = ([System.Text.RegularExpressions.Regex]::Matches($prefix, "`n")).Count + 1
        $results.Add([ordered]@{
                path = $source.RelativePath
                line = $lineNumber
                text = $source.Lines[$lineNumber - 1].Trim()
            }) | Out-Null
    }
    return $results.ToArray()
}

function Get-CodeRuleFindings([string]$RuleId, [string]$Pattern) {
    $findings = New-Object 'System.Collections.Generic.List[object]'
    foreach ($source in $script:SourceRecords) {
        foreach ($evidence in @(Get-CodePatternMatches $source.RelativePath $Pattern)) {
            $findings.Add([ordered]@{
                    rule = $RuleId
                    path = $evidence.path
                    line = $evidence.line
                    text = $evidence.text
                }) | Out-Null
        }
    }
    return $findings.ToArray()
}

try {
    if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
        $SourceRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'src'
    } else {
        $projectSourceRoot = Join-Path $SourceRoot 'src'
        if (Test-Path -LiteralPath $projectSourceRoot -PathType Container) {
            $SourceRoot = $projectSourceRoot
        }
    }
    $script:SourceRootResolved = Get-FullPath $SourceRoot
    if (-not (Test-Path -LiteralPath $script:SourceRootResolved -PathType Container)) {
        throw "Source root does not exist: $script:SourceRootResolved"
    }

    $sourceFiles = @(Get-ChildItem -LiteralPath $script:SourceRootResolved -Recurse -File -Filter '*.cs' | Sort-Object FullName)
    if ($sourceFiles.Count -eq 0) {
        throw "No C# source files found under: $script:SourceRootResolved"
    }

    foreach ($file in $sourceFiles) {
        $fullPath = Get-FullPath $file.FullName
        $text = [System.IO.File]::ReadAllText($fullPath)
        $relativePath = Get-RelativePath $script:SourceRootResolved $fullPath
        $record = [pscustomobject]@{
            FullPath = $fullPath
            RelativePath = $relativePath
            FileName = $file.Name
            Text = $text
            CodeText = Mask-CSharpNonCode $text
            Lines = @($text -split "`r?`n")
        }
        $script:SourceRecords += $record
        $script:SourceByRelativePath[$relativePath] = $record
        if (-not $script:SourceByName.ContainsKey($file.Name)) {
            $script:SourceByName[$file.Name] = $record
        }
    }

    $launcherResolve = @(Get-PatternMatches 'NpcDialogueLauncher.cs' 'IMarcusAiFrameworkHost\s+host\s*=\s*AwakeRuntime\.ResolveHost\s*\(\s*\)')
    $launcherCreate = @(Get-PatternMatches 'NpcDialogueLauncher.cs' '(?:new\s+NpcDialogueService|NpcDialogueService\.CreateSceneShout)\s*\(\s*host\b')
    $launcherNullGate = @(Get-PatternMatches 'NpcDialogueLauncher.cs' 'if\s*\(\s*host\s*==\s*null')
    $launcherOrdered = Test-LineOrder $launcherResolve $launcherCreate
    $launcherPassed = $launcherResolve.Count -gt 0 -and $launcherCreate.Count -gt 0 -and $launcherNullGate.Count -gt 0 -and $launcherOrdered
    Add-Check 'caller.launcher' 'NpcDialogueLauncher Host resolution and service creation' $launcherPassed `
        $(if ($launcherPassed) { 'Host is resolved, guarded, and passed into NpcDialogueService creation.' } else { 'Missing Host resolution, null gate, service creation, or source order.' }) `
        (@($launcherResolve | Select-Object -First 2) + @($launcherCreate | Select-Object -First 3) + @($launcherNullGate | Select-Object -First 1)) `
        ([ordered]@{
            resolve_count = $launcherResolve.Count
            create_count = $launcherCreate.Count
            null_gate_count = $launcherNullGate.Count
            resolve_before_create = $launcherOrdered
        })

    $dialogueHostConstructor = @(Get-PatternMatches 'NpcDialogueService.cs' 'NpcDialogueService\s*\(\s*IMarcusAiFrameworkHost\s+host')
    $dialogueHostAssignment = @(Get-PatternMatches 'NpcDialogueService.cs' '_host\s*=\s*host\b')
    $dialogueGatewayField = @(Get-PatternMatches 'NpcDialogueService.cs' '\bAiTaskGateway\s+_gateway\b')
    $dialogueGatewayConstructor = @(Get-PatternMatches 'NpcDialogueService.cs' '_gateway\s*=\s*new\s+AiTaskGateway\s*\(\s*host\b')
    $dialogueGatewaySubmit = @(Get-PatternMatches 'NpcDialogueService.cs' '_gateway\s*\.\s*SubmitAsync\s*\(')
    $dialogueDirectAiSubmit = @(Get-PatternMatches 'NpcDialogueService.cs' '\.\s*Ai\s*\.\s*SubmitAsync\s*\(')
    $dialoguePassed = $dialogueHostConstructor.Count -gt 0 -and $dialogueHostAssignment.Count -gt 0 -and $dialogueGatewayField.Count -gt 0 -and $dialogueGatewayConstructor.Count -gt 0 -and $dialogueGatewaySubmit.Count -gt 0 -and $dialogueDirectAiSubmit.Count -eq 0
    Add-Check 'caller.dialogue_gateway' 'NpcDialogueService routes AI submission through AiTaskGateway' $dialoguePassed `
        $(if ($dialoguePassed) { 'NpcDialogueService stores the injected Host, constructs AiTaskGateway, and submits through the gateway.' } else { 'Missing injected Host, gateway construction, gateway submission, or direct AI submission was found.' }) `
        (@($dialogueHostConstructor | Select-Object -First 1) + @($dialogueHostAssignment | Select-Object -First 1) + @($dialogueGatewayConstructor | Select-Object -First 1) + @($dialogueGatewaySubmit | Select-Object -First 1) + @($dialogueDirectAiSubmit | Select-Object -First 5)) `
        ([ordered]@{
            host_constructor_count = $dialogueHostConstructor.Count
            host_assignment_count = $dialogueHostAssignment.Count
            gateway_field_count = $dialogueGatewayField.Count
            gateway_constructor_count = $dialogueGatewayConstructor.Count
            gateway_submit_count = $dialogueGatewaySubmit.Count
            direct_ai_submit_count = $dialogueDirectAiSubmit.Count
        })

    $gatewayHostField = @(Get-PatternMatches 'AiTaskGateway.cs' 'private\s+readonly\s+IMarcusAiFrameworkHost\s+_host')
    $gatewayConstructor = @(Get-PatternMatches 'AiTaskGateway.cs' 'AiTaskGateway\s*\(\s*IMarcusAiFrameworkHost\s+host')
    $gatewayAssignment = @(Get-PatternMatches 'AiTaskGateway.cs' '_host\s*=\s*host\b')
    $gatewaySubmitMethod = @(Get-PatternMatches 'AiTaskGateway.cs' 'Task\s*<\s*AiTaskSubmitResult\s*>\s+SubmitAsync\s*\(')
    $gatewayBind = @(Get-PatternMatches 'AiTaskGateway.cs' 'AwakeRuntime\.EnsureCurrentHeroBoundAsync\s*\(\s*_host\b')
    $gatewayAiSubmit = @(Get-PatternMatches 'AiTaskGateway.cs' '_host\s*\.\s*Ai\s*\.\s*SubmitAsync\s*\(')
    $gatewayBindBeforeSubmit = Test-LineOrder $gatewayBind $gatewayAiSubmit
    $gatewayPassed = $gatewayHostField.Count -gt 0 -and $gatewayConstructor.Count -gt 0 -and $gatewayAssignment.Count -gt 0 -and $gatewaySubmitMethod.Count -gt 0 -and $gatewayBind.Count -gt 0 -and $gatewayAiSubmit.Count -gt 0 -and $gatewayBindBeforeSubmit
    Add-Check 'caller.gateway' 'AiTaskGateway binds the current hero and submits through Host.Ai' $gatewayPassed `
        $(if ($gatewayPassed) { 'AiTaskGateway owns the injected Host, binds the current hero, then calls Host.Ai.SubmitAsync.' } else { 'Missing gateway Host ownership, SubmitAsync method, hero binding, AI submit, or source order.' }) `
        (@($gatewayConstructor | Select-Object -First 1) + @($gatewayBind | Select-Object -First 1) + @($gatewayAiSubmit | Select-Object -First 1)) `
        ([ordered]@{
            host_field_count = $gatewayHostField.Count
            constructor_count = $gatewayConstructor.Count
            host_assignment_count = $gatewayAssignment.Count
            submit_method_count = $gatewaySubmitMethod.Count
            ensure_binding_count = $gatewayBind.Count
            ai_submit_count = $gatewayAiSubmit.Count
            binding_before_submit = $gatewayBindBeforeSubmit
        })

    $memoryGatewayField = @(Get-PatternMatches 'NpcMemoryService.cs' '\bAiTaskGateway\s+_gateway\b')
    $memoryGatewayConstructor = @(Get-PatternMatches 'NpcMemoryService.cs' '_gateway\s*=\s*new\s+AiTaskGateway\s*\(\s*host\b')
    $memoryRouteDefinition = @(Get-PatternMatches 'NpcMemoryService.cs' 'RouteId\s*=\s*AiTaskConstants\.RouteMemoryDaily\b')
    $memoryGatewaySubmit = @(Get-PatternMatches 'NpcMemoryService.cs' '_gateway\s*\.\s*SubmitAsync\s*\(\s*NpcMemoryConstants\.RouteId\b')
    $memoryDirectAiSubmit = @(Get-PatternMatches 'NpcMemoryService.cs' '\.\s*Ai\s*\.\s*SubmitAsync\s*\(')
    $memoryPassed = $memoryGatewayField.Count -gt 0 -and $memoryGatewayConstructor.Count -gt 0 -and $memoryRouteDefinition.Count -gt 0 -and $memoryGatewaySubmit.Count -gt 0 -and $memoryDirectAiSubmit.Count -eq 0
    Add-Check 'caller.memory' 'NpcMemoryService uses the memory route and AiTaskGateway' $memoryPassed `
        $(if ($memoryPassed) { 'NpcMemoryService constructs the same gateway type and submits NpcMemoryConstants.RouteId, backed by RouteMemoryDaily.' } else { 'Missing memory gateway, memory route definition, gateway submission, or direct AI submission was found.' }) `
        (@($memoryGatewayConstructor | Select-Object -First 1) + @($memoryRouteDefinition | Select-Object -First 1) + @($memoryGatewaySubmit | Select-Object -First 1) + @($memoryDirectAiSubmit | Select-Object -First 5)) `
        ([ordered]@{
            gateway_field_count = $memoryGatewayField.Count
            gateway_constructor_count = $memoryGatewayConstructor.Count
            memory_route_definition_count = $memoryRouteDefinition.Count
            memory_gateway_submit_count = $memoryGatewaySubmit.Count
            direct_ai_submit_count = $memoryDirectAiSubmit.Count
        })

    $entryContracts = @(
        [pscustomobject]@{
            id = 'event-engine'
            label = 'event entry'
            file = 'AwakeEventEngine.cs'
            patterns = @(
                'IMarcusAiFrameworkHost\s+host\s*=\s*AwakeRuntime\.ResolveHost\s*\(\s*\)',
                'new\s+WorldCommandBridge\s*\(\s*host\b'
            )
        },
        [pscustomobject]@{
            id = 'letter-service'
            label = 'letter entry'
            file = 'AwakeLetterService.cs'
            patterns = @('IMarcusAiFrameworkHost\s+host\s*=\s*AwakeRuntime\.ResolveHost\s*\(\s*\)')
        },
        [pscustomobject]@{
            id = 'gold-settlement'
            label = 'gold entry'
            file = 'AwakeGoldSettlementService.cs'
            patterns = @('IMarcusAiFrameworkHost\s+host\s*=\s*AwakeRuntime\.ResolveHost\s*\(\s*\)')
        },
        [pscustomobject]@{
            id = 'world-command-bridge'
            label = 'command entry'
            file = 'WorldCommandBridge.cs'
            patterns = @(
                'WorldCommandBridge\s*\(\s*IMarcusAiFrameworkHost\s+host',
                '_host\s*=\s*host\b',
                '_host\s*\.\s*Commands\s*\.\s*(?:PreflightAsync|SubmitAsync)\s*\('
            )
        },
        [pscustomobject]@{
            id = 'messenger-entry'
            label = 'messenger dialogue entry'
            file = 'AwakeMessengerVM.cs'
            patterns = @(
                'IMarcusAiFrameworkHost\s+host\s*=\s*AwakeRuntime\.ResolveHost\s*\(\s*\)',
                'new\s+NpcDialogueService\s*\(\s*host\b'
            )
        },
        [pscustomobject]@{
            id = 'encounter-entry'
            label = 'encounter entry'
            file = 'AwakeEncounterBehavior.cs'
            patterns = @('AwakeRuntime\.ResolveHost\s*\(\s*\)')
        },
        [pscustomobject]@{
            id = 'context-provider'
            label = 'context provider entry'
            file = 'ContextProviders.cs'
            patterns = @('IMarcusAiFrameworkHost\s+host\s*=\s*AwakeRuntime\.ResolveHost\s*\(\s*\)')
        },
        [pscustomobject]@{
            id = 'knowledge-service'
            label = 'knowledge service entry'
            file = 'KnowledgeService.cs'
            patterns = @(
                'KnowledgeService\s*\(\s*IMarcusAiFrameworkHost\s+host',
                '_host\s*=\s*host\b'
            )
        }
    )

    $entryResults = @()
    foreach ($entryContract in $entryContracts) {
        $missingPatterns = @()
        $entryEvidence = @()
        foreach ($pattern in @($entryContract.patterns)) {
            $patternEvidence = @(Get-PatternMatches $entryContract.file $pattern)
            if ($patternEvidence.Count -eq 0) {
                $missingPatterns += $pattern
            } else {
                $entryEvidence += @($patternEvidence | Select-Object -First 2)
            }
        }
        $directLocatorEvidence = @(Get-PatternMatches $entryContract.file 'FrameworkHostLocator\s*\.\s*Resolve\s*\(')
        $entryPassed = $missingPatterns.Count -eq 0 -and $directLocatorEvidence.Count -eq 0
        $entryResults += [ordered]@{
            id = $entryContract.id
            title = $entryContract.label
            file = $entryContract.file
            status = if ($entryPassed) { 'PASS' } else { 'FAIL' }
            missing_patterns = $missingPatterns
            direct_locator_count = $directLocatorEvidence.Count
            evidence = $entryEvidence
            direct_locator_evidence = @($directLocatorEvidence | Select-Object -First 5)
        }
    }
    $failedEntries = @($entryResults | Where-Object { $_.status -eq 'FAIL' })
    $entriesPassed = $failedEntries.Count -eq 0
    Add-Check 'caller.entry_host_boundaries' 'Event, letter, money, command, and related AI entries use Host boundaries' $entriesPassed `
        $(if ($entriesPassed) { 'All declared AI-related entries resolve Host through AwakeRuntime or receive an injected Host.' } else { 'One or more declared AI-related entries lack the required Host boundary or use a direct framework locator.' }) `
        $entryResults `
        ([ordered]@{
            entry_count = $entryResults.Count
            failed_entry_count = $failedEntries.Count
            failed_entry_ids = @($failedEntries | ForEach-Object { $_.id })
        })

    $transportFindings = @(Get-CodeRuleFindings 'direct_http_transport' '(?i)(?:System\.Net\.Http|HttpClient|HttpWebRequest|HttpRequestMessage|HttpResponseMessage|WebClient|WebRequest|SocketsHttpHandler|TcpClient|UdpClient|UnityWebRequest|RestClient)')

    $secretValuePattern = '(?i)(?:\bsecret\b|\bapiKey\b|\bapi_key\b|\bapiSecret\b|\bcredentialValue\b|\bcredentialSecret\b|\bproviderKey\b|\bpassword\b|\baccessToken\b|\bauthToken\b|\bbearerToken\b|\btokenValue\b)'
    $secretWritePatterns = @(
        '(?is)\b(?:System\.IO\.)?File\s*\.\s*(?:WriteAllText|AppendAllText|WriteAllBytes|AppendAllBytes|WriteAllLines|OpenWrite|CreateText|Create)\s*\([^)]{0,400}' + $secretValuePattern,
        '(?is)\bEnvironment\s*\.\s*SetEnvironmentVariable\s*\([^)]{0,400}' + $secretValuePattern,
        '(?is)\b(?:StreamWriter|BinaryWriter|FileStream)\b[\s\S]{0,400}?\.\s*(?:Write|WriteLine|WriteAsync|WriteByte|WriteBytes)\s*\([^)]{0,400}' + $secretValuePattern,
        '(?is)\b(?:AwakeLog|Logger|Log|Console|Debug|Trace)\s*\.\s*(?:Write|WriteLine|Info|Warn|Warning|Error|Log|Debug|Trace)\s*\([^)]{0,400}' + $secretValuePattern,
        '(?is)\b(?:storage|store|kv|namespace|sidecar|database|repository|persistence|stateStore|worldStateStore)\w*\s*\.\s*(?:Set|Put|Write|Save|Upsert|Insert|Update|Append|Store|Persist)\w*\s*\([^)]{0,400}' + $secretValuePattern
    )
    $secretWriteFindings = @()
    foreach ($pattern in $secretWritePatterns) {
        $secretWriteFindings += @(Get-CodeRuleFindings 'secret_write_or_log' $pattern)
    }

    $allowedCredentialFiles = @('AwakeConfig.cs', 'AwakeMcmActions.cs', 'AwakeProviderConfiguration.cs')
    $credentialBypassPatterns = @(
        '(?i)\bnew\s+ProviderCredentialRequest\s*\(',
        '(?i)\b(?:SecureStorage|CredentialStore|CredentialManager|SecretStore|ProviderCredentialStore)\s*(?:\.|\()',
        '(?i)\b(?:Save|Load|Get|Resolve|Read|Write|Store|Persist)(?:Credential|Secret)\w*\s*\(',
        '(?i)\b(?:Authorization|Bearer|XApiKey|ApiKeyHeader)\b\s*(?:=|\.|:)'
    )
    $credentialBypassFindings = @()
    foreach ($pattern in $credentialBypassPatterns) {
        $credentialBypassFindings += @(Get-CodeRuleFindings 'credential_bypass' $pattern | Where-Object { $allowedCredentialFiles -notcontains $_.path })
    }

    $credentialFlowSignals = @(
        @(Get-CodePatternMatches 'AwakeConfig.cs' '\bConfigureProviderApiKey\s*=')
        @(Get-CodePatternMatches 'AwakeMcmActions.cs' '\bPromptForApiKey\s*\(')
        @(Get-CodePatternMatches 'AwakeProviderConfiguration.cs' '\bProviderCredentialRequest\s*\(')
        @(Get-CodePatternMatches 'AwakeProviderConfiguration.cs' '\.\s*UpsertCredentialAsync\s*\(')
    )
    $credentialGovernanceFindings = @()
    if ($credentialFlowSignals.Count -gt 0) {
        $governedCredentialPatterns = @(
            [pscustomobject]@{ id = 'mcm_action'; file = 'AwakeConfig.cs'; pattern = '\bConfigureProviderApiKey\s*=' },
            [pscustomobject]@{ id = 'mcm_forward'; file = 'AwakeMcmActions.cs'; pattern = '\bPromptForApiKey\s*\(' },
            [pscustomobject]@{ id = 'host_resolution'; file = 'AwakeProviderConfiguration.cs'; pattern = 'FrameworkHostLocator\s*\.\s*Resolve\s*\(' },
            [pscustomobject]@{ id = 'runtime_resolution'; file = 'AwakeProviderConfiguration.cs'; pattern = '\bTryResolveRuntime\s*\(' },
            [pscustomobject]@{ id = 'credential_port'; file = 'AwakeProviderConfiguration.cs'; pattern = '\bIProviderRuntimePort\b' },
            [pscustomobject]@{ id = 'credential_request'; file = 'AwakeProviderConfiguration.cs'; pattern = '\bProviderCredentialRequest\s*\(' },
            [pscustomobject]@{ id = 'credential_port_call'; file = 'AwakeProviderConfiguration.cs'; pattern = '\.\s*UpsertCredentialAsync\s*\(' }
        )
        foreach ($pattern in $governedCredentialPatterns) {
            if (@(Get-CodePatternMatches $pattern.file $pattern.pattern).Count -eq 0) {
                $credentialGovernanceFindings += [ordered]@{
                    rule = 'credential_governance_missing'
                    path = $pattern.file
                    line = 0
                    text = 'Missing governed credential boundary: ' + $pattern.id
                }
            }
        }
    }

    $apiKeyFindings = @($secretWriteFindings) + @($credentialBypassFindings) + @($credentialGovernanceFindings)
    $companionFindings = @(Get-CodeRuleFindings 'external_companion_reference' '(?i)(?:\bnew\s+[A-Za-z_]\w*Companion\w*\s*\(|\b[A-Za-z_]\w*Companion\w*\s*\.\s*[A-Za-z_]\w*\s*\()')
    $forbiddenPassed = $transportFindings.Count -eq 0 -and $apiKeyFindings.Count -eq 0 -and $companionFindings.Count -eq 0
    $forbiddenDetails = [ordered]@{
        direct_http_transport = [ordered]@{
            count = $transportFindings.Count
            evidence = @($transportFindings | Select-Object -First 20)
        }
        api_key_or_credential_handling = [ordered]@{
            count = $apiKeyFindings.Count
            evidence = @($apiKeyFindings | Select-Object -First 20)
        }
        external_companion_reference = [ordered]@{
            count = $companionFindings.Count
            evidence = @($companionFindings | Select-Object -First 20)
        }
        secret_write_or_log = [ordered]@{
            count = $secretWriteFindings.Count
            evidence = @($secretWriteFindings | Select-Object -First 20)
        }
        credential_bypass = [ordered]@{
            count = $credentialBypassFindings.Count
            evidence = @($credentialBypassFindings | Select-Object -First 20)
        }
        credential_governance = [ordered]@{
            allowed_files = $allowedCredentialFiles
            finding_count = $credentialGovernanceFindings.Count
            evidence = @($credentialGovernanceFindings | Select-Object -First 20)
        }
        note = 'Credential names, provider URLs, MCM labels, and governed Runtime/credential-port references are not violations. Findings require direct transport, secret persistence/logging, or an ungoverned credential path. This check is source-text-only and does not inspect runtime traffic or secrets.'
    }
    Add-Check 'boundary.no_direct_transport_or_companion' 'No direct transport, secret persistence/logging, or ungoverned credential path' $forbiddenPassed `
        $(if ($forbiddenPassed) { 'No direct transport, secret persistence/logging, or ungoverned credential path was found; governed credential references are allowed.' } else { 'Forbidden direct transport, secret persistence/logging, or ungoverned credential findings were found; inspect the structured findings for exact paths and lines.' }) `
        $forbiddenDetails `
        ([ordered]@{
            direct_http_transport_count = $transportFindings.Count
            api_key_or_credential_handling_count = $apiKeyFindings.Count
            external_companion_reference_count = $companionFindings.Count
        })

    $mcmContracts = @(
        [pscustomobject]@{ action = 'ConfigureProviderApiKey'; provider = 'PromptForApiKey' },
        [pscustomobject]@{ action = 'ApplyProviderConfiguration'; provider = 'ApplyProviderConfiguration' },
        [pscustomobject]@{ action = 'PullProviderModels'; provider = 'PullModels' },
        [pscustomobject]@{ action = 'TestProviderConnection'; provider = 'TestConnection' },
        [pscustomobject]@{ action = 'RefreshAiStatus'; provider = 'RefreshRuntimeStatus' }
    )
    $mcmResults = @()
    foreach ($mcmContract in $mcmContracts) {
        $patterns = @(
            [pscustomobject]@{
                id = 'button_property'
                file = 'AwakeConfig.cs'
                pattern = '\[SettingPropertyButton[\s\S]{0,800}?public\s+Action\s+' + $mcmContract.action + '\b'
            },
            [pscustomobject]@{
                id = 'constructor_assignment'
                file = 'AwakeConfig.cs'
                pattern = '\b' + $mcmContract.action + '\s*=\s*AwakeMcmActions\.' + $mcmContract.action + '\b'
            },
            [pscustomobject]@{
                id = 'mcm_action'
                file = 'AwakeMcmActions.cs'
                pattern = 'internal\s+static\s+void\s+' + $mcmContract.action + '\s*\('
            },
            [pscustomobject]@{
                id = 'provider_forward'
                file = 'AwakeMcmActions.cs'
                pattern = 'AwakeProviderConfiguration\.' + $mcmContract.provider + '\s*\('
            },
            [pscustomobject]@{
                id = 'provider_entry'
                file = 'AwakeProviderConfiguration.cs'
                pattern = 'internal\s+static\s+(?:async\s+)?(?:void|Task(?:<[^>\r\n]+>)?)\s+' + $mcmContract.provider + '\s*\('
            }
        )
        $missingPatterns = @()
        $mappingEvidence = @()
        foreach ($pattern in $patterns) {
            $patternEvidence = @(Get-PatternMatches $pattern.file $pattern.pattern)
            if ($patternEvidence.Count -eq 0) {
                $missingPatterns += $pattern.id
            } else {
                $mappingEvidence += @($patternEvidence | Select-Object -First 1)
            }
        }
        $mcmResults += [ordered]@{
            action = $mcmContract.action
            provider_entry = $mcmContract.provider
            status = if ($missingPatterns.Count -eq 0) { 'PASS' } else { 'FAIL' }
            missing = $missingPatterns
            evidence = $mappingEvidence
        }
    }

    $mcmBasePatterns = @(
        [pscustomobject]@{ id = 'mcm_config_class'; file = 'AwakeConfig.cs'; pattern = 'class\s+AwakeConfig\s*:\s*AttributeGlobalSettings\s*<\s*AwakeConfig\s*>' },
        [pscustomobject]@{ id = 'mcm_actions_class'; file = 'AwakeMcmActions.cs'; pattern = 'class\s+AwakeMcmActions\b' },
        [pscustomobject]@{ id = 'provider_configuration_class'; file = 'AwakeProviderConfiguration.cs'; pattern = 'class\s+AwakeProviderConfiguration\b' }
    )
    $mcmBaseMissing = @()
    $mcmBaseEvidence = @()
    foreach ($pattern in $mcmBasePatterns) {
        $patternEvidence = @(Get-PatternMatches $pattern.file $pattern.pattern)
        if ($patternEvidence.Count -eq 0) {
            $mcmBaseMissing += $pattern.id
        } else {
            $mcmBaseEvidence += @($patternEvidence | Select-Object -First 1)
        }
    }

    $mcmScopeLegacyFindings = @()
    foreach ($fileName in @('AwakeConfig.cs', 'AwakeMcmActions.cs', 'AwakeProviderConfiguration.cs')) {
        foreach ($evidence in @(Get-PatternMatches $fileName '(?i)\b(?:Sagan|SlaaneshConfig|GoddessAiGateway|AnimusForge|LoveHate)\w*\b')) {
            $mcmScopeLegacyFindings += [ordered]@{
                path = $evidence.path
                line = $evidence.line
                text = $evidence.text
            }
        }
    }
    $failedMcmMappings = @($mcmResults | Where-Object { $_.status -eq 'FAIL' })
    $mcmPassed = $mcmBaseMissing.Count -eq 0 -and $failedMcmMappings.Count -eq 0 -and $mcmScopeLegacyFindings.Count -eq 0
    Add-Check 'configuration.mcm_provider_entry' 'MCM actions forward to AwakeProviderConfiguration without Sagan-specific duplication' $mcmPassed `
        $(if ($mcmPassed) { 'MCM action properties, forwarding methods, and centralized provider entries are all present.' } else { 'MCM/provider wiring is incomplete or legacy Sagan-specific configuration markers were found.' }) `
        ([ordered]@{
            base_evidence = $mcmBaseEvidence
            action_mappings = $mcmResults
            legacy_scope_findings = $mcmScopeLegacyFindings
        }) `
        ([ordered]@{
            base_missing = $mcmBaseMissing
            failed_mapping_count = $failedMcmMappings.Count
            legacy_scope_finding_count = $mcmScopeLegacyFindings.Count
        })

    $passCount = @($script:Checks | Where-Object { $_.status -eq 'PASS' }).Count
    $failCount = @($script:Checks | Where-Object { $_.status -eq 'FAIL' }).Count
    $auditPassed = $failCount -eq 0
    $report = [ordered]@{
        schema_version = 'awake/marcus-awake-caller-contract/v1'
        status = if ($auditPassed) { 'PASS' } else { 'FAIL' }
        mode = 'offline-static-source-audit'
        source_root = $script:SourceRootResolved
        source_file_count = $script:SourceRecords.Count
        runtime_evidence = $false
        game_evidence = $false
        evidence_boundary = 'Checks inspect current C# source text only; they do not claim runtime behavior, game execution, provider traffic, or in-game evidence.'
        summary = [ordered]@{
            checks = $script:Checks.Count
            pass = $passCount
            fail = $failCount
        }
        checks = $script:Checks
    }
    Write-Output ($report | ConvertTo-Json -Depth 12)
    if (-not $auditPassed) { exit 1 }
    exit 0
}
catch {
    $errorReport = [ordered]@{
        schema_version = 'awake/marcus-awake-caller-contract/v1'
        status = 'FAIL'
        mode = 'offline-static-source-audit'
        source_root = $script:SourceRootResolved
        runtime_evidence = $false
        game_evidence = $false
        evidence_boundary = 'The audit could not complete; no runtime or game evidence was collected.'
        error = [ordered]@{
            type = $_.Exception.GetType().FullName
            message = $_.Exception.Message
        }
        checks = $script:Checks
    }
    Write-Output ($errorReport | ConvertTo-Json -Depth 12)
    exit 2
}
