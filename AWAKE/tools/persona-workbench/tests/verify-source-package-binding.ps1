param(
    [string]$SourceRoot,
    [string[]]$InputRoot,
    [string]$AwakeRoot,
    [Parameter(Mandatory = $true)][string]$PackagePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Fail([string]$message) { throw "SOURCE_PACKAGE_BINDING_MISMATCH: $message" }
function Full([string]$path) { [IO.Path]::GetFullPath($path) }
function Rel([string]$path, [string]$base) { $path.Substring($base.Length).TrimStart([char]92, [char]47).Replace([char]47, [char]92) }
function Add([Collections.Generic.List[object]]$files, [string]$path, [string]$root, [string]$prefix) {
    $full = Full (Join-Path $root $path); if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { Fail "source input is missing: $full" }
    $files.Add([pscustomobject]@{ Full = $full; Relative = ($prefix + (Rel $full (Full $root))).TrimStart([char]92) })
}
function AddTree([Collections.Generic.List[object]]$files, [string]$path, [string]$root, [string]$prefix, [string]$filter = '*') {
    $dir = Full (Join-Path $root $path); if (-not (Test-Path -LiteralPath $dir -PathType Container)) { Fail "source directory is missing: $dir" }
    foreach ($item in (Get-ChildItem -LiteralPath $dir -Recurse -File -Filter $filter | Sort-Object FullName)) {
        $relative = Rel $item.FullName (Full $root); if ($relative -match '(^|[\/])(bin|obj|artifacts|tests|backup[^\/]*|[^\/]*_backup_[^\/]*)($|[\/])') { continue }
        $files.Add([pscustomobject]@{ Full = Full $item.FullName; Relative = ($prefix + $relative).Replace([char]47, [char]92) })
    }
}
function GetExpectedLines([string]$awake, [string[]]$roots) {
    $files = [Collections.Generic.List[object]]::new()
    $workbench = $roots | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'src\PersonaWorkbench.Core') -PathType Container } | Select-Object -First 1
    if ($null -eq $workbench) { Fail 'InputRoot must include Persona Workbench root.' }
    AddTree $files 'src\PersonaWorkbench.Core' (Full $workbench) 'tools\persona-workbench\' '*.cs'
    Add $files 'src\PersonaWorkbench.Core\PersonaWorkbench.Core.csproj' (Full $workbench) 'tools\persona-workbench\'
    AddTree $files 'src\PersonaWorkbench.Web' (Full $workbench) 'tools\persona-workbench\' '*.cs'
    Add $files 'src\PersonaWorkbench.Web\PersonaWorkbench.Web.csproj' (Full $workbench) 'tools\persona-workbench\'
    AddTree $files 'src\PersonaWorkbench.Web\wwwroot' (Full $workbench) 'tools\persona-workbench\'
    Add $files 'src\PersonaWorkbench.Launcher\Program.cs' (Full $workbench) 'tools\persona-workbench\'
    Add $files 'src\PersonaWorkbench.Launcher\app.manifest' (Full $workbench) 'tools\persona-workbench\'
    AddTree $files 'contracts' (Full $workbench) 'tools\persona-workbench\' '*.json'
    AddTree $files 'contracts' (Full $workbench) 'tools\persona-workbench\' '*.md'
    foreach ($name in @('package-free-preview.ps1','tools\write-source-manifest.ps1','tests\verify-source-package-binding.ps1','tests\PersonaWorkbench.PackageZip.Tests.ps1','tools\validate-authoring-handoff.ps1','tools\finalize-ai-link-evidence.ps1','start-free-preview.ps1','stop-free-preview.ps1','launch-free-preview.vbs','stop-free-preview.vbs','README-FreePreview.md')) { Add $files $name (Full $workbench) 'tools\persona-workbench\' }
    $usage = Get-ChildItem -LiteralPath $workbench -File | Where-Object { $_.Name -like 'PersonaWorkbench-*.md' -and $_.Name -ne 'README-FreePreview.md' } | Sort-Object Name | Select-Object -First 1
    if ($null -eq $usage) { Fail 'Persona Workbench usage document is missing.' }
    $files.Add([pscustomobject]@{ Full = Full $usage.FullName; Relative = 'tools\persona-workbench\' + $usage.Name })
    AddTree $files 'docs\persona-contract' $awake ''
    Add $files 'ModuleData\Worldbook\persona_definitions\tag_registry.json' $awake ''
    foreach ($entry in ($files | Sort-Object Relative -Unique)) { "$((Get-FileHash -LiteralPath $entry.Full -Algorithm SHA256).Hash.ToUpperInvariant())  $($entry.Relative)" }
}
function HashBytes([byte[]]$bytes) { $sha=[Security.Cryptography.SHA256]::Create(); try { ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','').ToUpperInvariant() } finally { $sha.Dispose() } }
function ReadKey([string[]]$lines,[string]$key) { $line=$lines|Where-Object {$_ -like "$key=*"}|Select-Object -First 1; if($null -eq $line){Fail "BUILD-ID.txt missing $key"}; $line.Substring($key.Length+1) }
$source = if ($SourceRoot) { Full $SourceRoot } else { $null }
$roots = @()
if ($null -ne $InputRoot) { foreach($input in $InputRoot) { if(-not [string]::IsNullOrWhiteSpace($input)) { $roots += Full $input } } }
if($roots.Count -eq 0 -and $null -ne $source){$roots=@($source)}; if($roots.Count -eq 0){Fail 'InputRoot or SourceRoot is required.'}
$workbench = $roots | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'src\PersonaWorkbench.Core') -PathType Container } | Select-Object -First 1
if([string]::IsNullOrWhiteSpace($AwakeRoot)){$AwakeRoot=if($source -and (Test-Path -LiteralPath (Join-Path $source 'docs\persona-contract') -PathType Container)){$source}else{Split-Path (Split-Path (Full $workbench) -Parent) -Parent}}
$awake=Full $AwakeRoot; $package=Full $PackagePath
if(-not(Test-Path -LiteralPath $awake -PathType Container)){Fail "AwakeRoot missing: $awake"}; if(-not(Test-Path -LiteralPath $package -PathType Container)){Fail "PackagePath missing: $package"}
$manifestTemp=Join-Path ([IO.Path]::GetTempPath()) ('pwb-source-manifest-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Force -Path $manifestTemp|Out-Null;try{$writer=Join-Path (Full $workbench) 'tools\write-source-manifest.ps1';& $writer -InputRoot $roots -AwakeRoot $awake -Destination $manifestTemp|Out-Null;$expected=@(Get-Content -LiteralPath (Join-Path $manifestTemp 'BUILD-SOURCE-MANIFEST.sha256.txt'))}finally{if(Test-Path -LiteralPath $manifestTemp){Remove-Item -LiteralPath $manifestTemp -Recurse -Force}} $manifest=Join-Path $package 'BUILD-SOURCE-MANIFEST.sha256.txt'; $build=Join-Path $package 'BUILD-ID.txt'; $packageManifest=Join-Path $package 'PACKAGE-MANIFEST.sha256.txt'; foreach($path in @($manifest,$build,$packageManifest)){if(-not(Test-Path -LiteralPath $path -PathType Leaf)){Fail "package metadata missing: $path"}}
$actual=@(Get-Content -LiteralPath $manifest); if($actual.Count -ne $expected.Count){Fail 'source manifest line count differs.'}; for($i=0;$i -lt $expected.Count;$i++){if($actual[$i] -ne $expected[$i]){Fail "source manifest differs at line $($i+1)."}}
$text=($expected -join "`n")+"`n"; $sourceHash=HashBytes ([Text.UTF8Encoding]::new($false).GetBytes($text)); $buildLines=@(Get-Content -LiteralPath $build); $buildId=ReadKey $buildLines 'BuildId'; if((ReadKey $buildLines 'SourceManifestHash') -ne $sourceHash){Fail 'source manifest hash differs.'}; if($buildId -notmatch '^PWB-[0-9]{8}-[0-9]{6}Z-[0-9A-F]{12}$'){Fail "invalid BuildId: $buildId"}; if($buildId.Substring($buildId.Length-12) -ne $sourceHash.Substring(0,12)){Fail 'BuildId is not bound to source hash.'}
$packageRoot=$package.TrimEnd('\','/')+'\'; $covered=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase); foreach($line in @(Get-Content -LiteralPath $packageManifest)){if([string]::IsNullOrWhiteSpace($line)){continue}; if($line -notmatch '^([0-9A-Fa-f]{64})  (.+)$'){Fail "invalid package manifest line: $line"}; $relative=$matches[2]; $candidate=Full (Join-Path $package $relative); if(-not $candidate.StartsWith($packageRoot,[StringComparison]::OrdinalIgnoreCase)){Fail "package manifest escapes package: $relative"}; if(-not(Test-Path -LiteralPath $candidate -PathType Leaf)){Fail "package manifest entry missing: $relative"}; if((Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash.ToUpperInvariant() -ne $matches[1].ToUpperInvariant()){Fail "package hash differs: $relative"}; [void]$covered.Add($relative)}
foreach($file in (Get-ChildItem -LiteralPath $package -Recurse -File | Where-Object {$_.FullName -ine $packageManifest})){ $relative=Rel $file.FullName $package; if($relative -eq 'ollama-workbench-routes.json' -or $relative.StartsWith('.runtime\',[StringComparison]::OrdinalIgnoreCase)){continue}; if(-not $covered.Contains($relative)){Fail "package file is not covered: $relative"} }
if(-not $covered.Contains('PersonaWorkbench.Web.exe')){Fail 'package executable is not covered.'}; Write-Output 'PASS source/package BuildId and manifest match'; Write-Output "BuildId=$buildId"; Write-Output "SourceManifestHash=$sourceHash"




