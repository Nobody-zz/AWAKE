param(
    [string]$SourceRoot,
    [string[]]$InputRoot,
    [string]$AwakeRoot,
    [Parameter(Mandatory = $true)][string]$Destination
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Full([string]$path) { [IO.Path]::GetFullPath($path) }
function Rel([string]$path, [string]$base) { $path.Substring($base.Length).TrimStart([char]92, [char]47).Replace([char]47, [char]92) }
function Add([Collections.Generic.List[object]]$files, [string]$path, [string]$root, [string]$prefix) {
    $full = Full (Join-Path $root $path)
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "Source manifest input is missing: $full" }
    $files.Add([pscustomobject]@{ Full = $full; Relative = ($prefix + (Rel $full (Full $root))).TrimStart([char]92) })
}
function AddTree([Collections.Generic.List[object]]$files, [string]$path, [string]$root, [string]$prefix, [string]$filter = '*') {
    $dir = Full (Join-Path $root $path)
    if (-not (Test-Path -LiteralPath $dir -PathType Container)) { throw "Source manifest directory is missing: $dir" }
    foreach ($item in (Get-ChildItem -LiteralPath $dir -Recurse -File -Filter $filter | Sort-Object FullName)) {
        $relative = Rel $item.FullName (Full $root)
        if ($relative -match '(^|[\/])(bin|obj|artifacts|tests|backup[^\/]*|[^\/]*_backup_[^\/]*)($|[\/])') { continue }
        $files.Add([pscustomobject]@{ Full = Full $item.FullName; Relative = ($prefix + $relative).Replace([char]47, [char]92) })
    }
}
function AddWorkbenchFiles([Collections.Generic.List[object]]$files, [string]$workbenchRoot) {
    AddTree $files 'src\PersonaWorkbench.Core' $workbenchRoot 'tools\persona-workbench\' '*.cs'
    Add $files 'src\PersonaWorkbench.Core\PersonaWorkbench.Core.csproj' $workbenchRoot 'tools\persona-workbench\'
    AddTree $files 'src\PersonaWorkbench.Web' $workbenchRoot 'tools\persona-workbench\' '*.cs'
    Add $files 'src\PersonaWorkbench.Web\PersonaWorkbench.Web.csproj' $workbenchRoot 'tools\persona-workbench\'
    AddTree $files 'src\PersonaWorkbench.Web\wwwroot' $workbenchRoot 'tools\persona-workbench\'
    Add $files 'src\PersonaWorkbench.Launcher\Program.cs' $workbenchRoot 'tools\persona-workbench\'
    Add $files 'src\PersonaWorkbench.Launcher\app.manifest' $workbenchRoot 'tools\persona-workbench\'
    AddTree $files 'contracts' $workbenchRoot 'tools\persona-workbench\' '*.json'
    AddTree $files 'contracts' $workbenchRoot 'tools\persona-workbench\' '*.md'
    foreach ($name in @('package-free-preview.ps1','tools\write-source-manifest.ps1','tests\verify-source-package-binding.ps1','tests\PersonaWorkbench.PackageZip.Tests.ps1','tools\validate-authoring-handoff.ps1','tools\finalize-ai-link-evidence.ps1','start-free-preview.ps1','stop-free-preview.ps1','launch-free-preview.vbs','stop-free-preview.vbs','README-FreePreview.md')) {
        Add $files $name $workbenchRoot 'tools\persona-workbench\'
    }
    $usage = Get-ChildItem -LiteralPath $workbenchRoot -File | Where-Object { $_.Name -like 'PersonaWorkbench-*.md' -and $_.Name -ne 'README-FreePreview.md' } | Sort-Object Name | Select-Object -First 1
    if ($null -eq $usage) { throw 'Persona Workbench usage document is missing.' }
    $files.Add([pscustomobject]@{ Full = Full $usage.FullName; Relative = 'tools\persona-workbench\' + $usage.Name })
}
function GetEntries([string]$root, [string[]]$roots) {
    $files = [Collections.Generic.List[object]]::new()
    $workbench = $roots | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'src\PersonaWorkbench.Core') -PathType Container } | Select-Object -First 1
    if ($null -eq $workbench) { throw 'InputRoot must include Persona Workbench root.' }
    AddWorkbenchFiles $files (Full $workbench)
    AddTree $files 'docs\persona-contract' $root ''
    Add $files 'ModuleData\Worldbook\persona_definitions\tag_registry.json' $root ''
    $unique = $files | Sort-Object Relative -Unique
    foreach ($entry in $unique) {
        $hash = (Get-FileHash -LiteralPath $entry.Full -Algorithm SHA256).Hash.ToUpperInvariant()
        "$hash  $($entry.Relative)"
    }
}
function HashBytes([byte[]]$bytes) {
    $sha = [Security.Cryptography.SHA256]::Create(); try { ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','').ToUpperInvariant() } finally { $sha.Dispose() }
}

$source = if ($SourceRoot) { Full $SourceRoot } else { $null }
$roots = @($InputRoot | Where-Object { $_ } | ForEach-Object { Full $_ })
if ($roots.Count -eq 0 -and $source) { $roots = @($source) }
if ($roots.Count -eq 0) { throw 'InputRoot or SourceRoot is required.' }
$workbenchRoot = $roots | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'src\PersonaWorkbench.Core') -PathType Container } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($AwakeRoot)) { $AwakeRoot = if ($source -and (Test-Path -LiteralPath (Join-Path $source 'docs\persona-contract') -PathType Container)) { $source } else { Split-Path (Split-Path (Full $workbenchRoot) -Parent) -Parent } }
$awake = Full $AwakeRoot
if (-not (Test-Path -LiteralPath $awake -PathType Container)) { throw "AwakeRoot is missing: $awake" }
if (-not (Test-Path -LiteralPath (Join-Path $awake 'docs\persona-contract') -PathType Container)) { throw 'AwakeRoot docs/persona-contract is missing.' }
if (-not (Test-Path -LiteralPath $Destination -PathType Container)) { throw "Destination is missing: $Destination" }
$lines = @(GetEntries $awake $roots) | Sort-Object
if ($lines.Count -eq 0) { throw 'Source manifest is empty.' }
$utf8 = [Text.UTF8Encoding]::new($false)
$text = ($lines -join "`n") + "`n"
$hash = HashBytes $utf8.GetBytes($text)
$now = [DateTime]::UtcNow
$buildId = 'PWB-{0}-{1}-{2}' -f $now.ToString('yyyyMMdd'), $now.ToString("HHmmss'Z'"), $hash.Substring(0,12)
[IO.File]::WriteAllText((Join-Path $Destination 'BUILD-SOURCE-MANIFEST.sha256.txt'), $text, $utf8)
[IO.File]::WriteAllText((Join-Path $Destination 'BUILD-ID.txt'), (@("BuildId=$buildId","GeneratedAtUtc=$($now.ToString('o'))","SourceManifestHash=$hash","SourceManifestFile=BUILD-SOURCE-MANIFEST.sha256.txt","InputRoot=$awake") -join "`n") + "`n", $utf8)
Write-Output "PASS source manifest written: $hash"
Write-Output "BuildId=$buildId"
Write-Output "SourceManifestHash=$hash"



