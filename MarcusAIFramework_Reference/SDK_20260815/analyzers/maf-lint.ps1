[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExtensionRoot
)

$resolved = Resolve-Path -LiteralPath $ExtensionRoot -ErrorAction Stop
$root = $resolved.Path
$warnings = [System.Collections.Generic.List[string]]::new()
$sourceFiles = Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '\\(_build_out|bin|obj|artifacts)\\' }

function Add-MafWarning([string]$code, [string]$file, [int]$line, [string]$message) {
    $relative = $file.Substring($root.Length).TrimStart([char[]]@('\', '/'))
    $warnings.Add(('{0}:{1}: warning {2}: {3}' -f $relative, $line, $code, $message))
}

$rules = @(
    @{ Code = 'MAF001'; Pattern = 'TaleWorlds\.|Microsoft\.Data\.Sqlite|System\.Net\.Http|Gauntlet'; Message = 'Public DTOs should not expose TaleWorlds, SQLite, HTTP, or UI concrete types.' },
    @{ Code = 'MAF002'; Pattern = '\.(Wait\(\)|Result\b)'; Message = 'Avoid synchronous waits in SDK handlers.' },
    @{ Code = 'MAF003'; Pattern = 'Campaign\.Current'; Message = 'Do not access Campaign.Current during extension registration or module load.' },
    @{ Code = 'MAF004'; Pattern = 'Task<|Task\s'; Message = 'Review every async API for CancellationToken and RequestContext deadline propagation.' },
    @{ Code = 'MAF005'; Pattern = '(capability://|command\.|prompt\.|save[_-]?key).{0,24}["''][A-Za-z0-9_-]{1,24}["'']'; Message = 'Stable IDs should include the extension namespace and an explicit version where applicable.' }
)

foreach ($file in $sourceFiles) {
    $lines = Get-Content -LiteralPath $file.FullName
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index].TrimStart().StartsWith('//')) { continue }
        foreach ($rule in $rules) {
            if ($rule.Code -eq 'MAF001' -and $lines[$index] -notmatch '\bpublic\s+(sealed\s+)?(class|interface|struct|enum|delegate)\b') { continue }
            if ($lines[$index] -match $rule.Pattern) {
                Add-MafWarning $rule.Code $file.FullName ($index + 1) $rule.Message
            }
        }
    }
}

$redistributed = @('MarcusAIFramework.dll', '0Harmony.dll', 'Bannerlord.ButterLib.dll', 'Bannerlord.UIExtenderEx.dll', 'MCMv5.dll')
foreach ($name in $redistributed) {
    Get-ChildItem -LiteralPath $root -Recurse -File -Filter $name -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\(_build_out|bin|obj|artifacts)\\' } |
        ForEach-Object { Add-MafWarning 'MAF006' $_.FullName 1 'Extension packages must not redistribute framework or prerequisite DLLs.' }
}

foreach ($warning in $warnings) { Write-Warning $warning }
Write-Output ('MAF Preview lint completed with {0} warning(s). No files were modified.' -f $warnings.Count)
exit 0
