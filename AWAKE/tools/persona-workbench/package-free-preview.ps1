param([string]$Destination = "")

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "src\PersonaWorkbench.Web\PersonaWorkbench.Web.csproj"
$launcherSource = Join-Path $root "src\PersonaWorkbench.Launcher\Program.cs"
$launcherManifest = Join-Path $root "src\PersonaWorkbench.Launcher\app.manifest"
$launcherCompiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$publish = Join-Path $root (".publish-free-preview-" + [Guid]::NewGuid().ToString("N"))
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $root "artifacts\PersonaWorkbench-FreePreview"
}

function Full([string]$path) { return [IO.Path]::GetFullPath($path) }
function UnderRoot([string]$path) {
    $full = Full $path
    $base = (Full $root).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) { throw "Path is outside package root: $full" }
}

UnderRoot $publish
UnderRoot $Destination
$destinationFull = Full $Destination
$artifactName = [IO.Path]::GetFileName($destinationFull.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar))
$zipPath = $destinationFull + ".zip"
$zipShaPath = $zipPath + ".sha256"
UnderRoot $zipPath
UnderRoot $zipShaPath
if (Test-Path -LiteralPath $Destination) { throw "Destination already exists; choose an empty package path." }
if (Test-Path -LiteralPath $zipPath) { throw "ZIP already exists; choose a new package path: $zipPath" }
if (Test-Path -LiteralPath $zipShaPath) { throw "ZIP sidecar already exists; choose a new package path: $zipShaPath" }
New-Item -ItemType Directory -Force -Path $Destination | Out-Null

& dotnet publish $project -c Release -r win-x64 --self-contained true -o $publish --nologo -p:PublishSingleFile=true -p:DebugSymbols=false -p:DebugType=None
if ($LASTEXITCODE -ne 0) { throw "Persona Workbench publish failed." }
$webExecutable = Join-Path $publish "PersonaWorkbench.Web.exe"
if (-not (Test-Path -LiteralPath $webExecutable)) { throw "Self-contained web executable is missing." }
if (Test-Path -LiteralPath (Join-Path $publish "PersonaWorkbench.Web.runtimeconfig.json")) { throw "Release unexpectedly depends on an external .NET runtime." }
if (Test-Path -LiteralPath (Join-Path $publish "PersonaWorkbench.Web.deps.json")) { throw "Release unexpectedly contains a framework-dependent dependency manifest." }
if (-not (Test-Path -LiteralPath $launcherCompiler)) { throw "Windows launcher compiler is unavailable: $launcherCompiler" }
$launcherOutput = Join-Path $publish "PersonaWorkbench.Launcher.exe"
& $launcherCompiler /nologo /target:winexe /optimize+ /platform:anycpu /win32manifest:$launcherManifest /out:$launcherOutput /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $launcherSource
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $launcherOutput)) { throw "Persona Workbench launcher build failed." }
Copy-Item -Path (Join-Path $publish "*") -Destination $Destination -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root "start-free-preview.ps1") -Destination $Destination -Force
Copy-Item -LiteralPath (Join-Path $root "launch-free-preview.vbs") -Destination $Destination -Force
Copy-Item -LiteralPath (Join-Path $root 'stop-free-preview.vbs') -Destination $Destination -Force
Copy-Item -LiteralPath (Join-Path $root "stop-free-preview.ps1") -Destination $Destination -Force
Copy-Item -LiteralPath (Join-Path $root "README-FreePreview.md") -Destination $Destination -Force
New-Item -ItemType Directory -Force -Path (Join-Path $Destination "contracts") | Out-Null
$contractFiles = Get-ChildItem -LiteralPath (Join-Path $root "contracts") -File
if ($contractFiles.Count -eq 0) { throw "Persona Workbench contract files are missing." }
Copy-Item -LiteralPath $contractFiles.FullName -Destination (Join-Path $Destination "contracts") -Force
$usageDocument = Get-ChildItem -LiteralPath $root -File | Where-Object {
    $_.Name -like "PersonaWorkbench-*.md" -and $_.Name -ne "README-FreePreview.md"
} | Sort-Object Name | Select-Object -First 1
if ($null -eq $usageDocument) { throw "Persona Workbench usage document is missing." }
Copy-Item -LiteralPath $usageDocument.FullName -Destination $Destination -Force

$releaseLabel = $artifactName
@("Persona Workbench Free Preview", "Package: $releaseLabel", "Do not merge this package into an older extracted directory.") | Set-Content -LiteralPath (Join-Path $Destination "PACKAGE-VERSION.txt") -Encoding utf8
$utf8Bom = New-Object Text.UTF8Encoding($true)
foreach ($scriptName in @("start-free-preview.ps1", "stop-free-preview.ps1")) {
    $scriptPath = Join-Path $Destination $scriptName
    $scriptText = [IO.File]::ReadAllText($scriptPath, [Text.Encoding]::UTF8)
    [IO.File]::WriteAllText($scriptPath, $scriptText, $utf8Bom)
    $scriptBytes = [IO.File]::ReadAllBytes($scriptPath)
    if ($scriptBytes.Length -lt 3 -or $scriptBytes[0] -ne 0xEF -or $scriptBytes[1] -ne 0xBB -or $scriptBytes[2] -ne 0xBF) {
        throw "Windows PowerShell compatibility encoding failed: $scriptName"
    }
}

$sourceManifestWriter = Join-Path $root "tools\write-source-manifest.ps1"
if (-not (Test-Path -LiteralPath $sourceManifestWriter -PathType Leaf)) { throw "Source manifest helper is missing: $sourceManifestWriter" }
& $sourceManifestWriter -SourceRoot $root -Destination $Destination | ForEach-Object { Write-Output $_ }
$forbiddenPatterns = @("*.env", "*.log", "*.map", "*.pdb", "*.cs", "*.csproj", "*.sln", "*.user", "*Tests*", ".history", ".conflicts", ".pending", "*secret*", "*key*")
$forbidden = Get-ChildItem -LiteralPath $Destination -Recurse -Force | Where-Object {
    $name = $_.Name
    $forbiddenPatterns | Where-Object { $name -like $_ } | Select-Object -First 1
}
if ($forbidden) { throw "Release whitelist rejected files: " + (($forbidden | ForEach-Object FullName) -join "; ") }

$manifest = Join-Path $Destination "PACKAGE-MANIFEST.sha256.txt"
$destinationFull = Full $Destination
$entries = Get-ChildItem -LiteralPath $Destination -Recurse -File | Where-Object { $_.FullName -ne $manifest } | Sort-Object FullName
$manifestLines = foreach ($entry in $entries) {
    $relative = $entry.FullName.Substring($destinationFull.Length).TrimStart([char]92, [char]47)
    $hash = (Get-FileHash -LiteralPath $entry.FullName -Algorithm SHA256).Hash
    "$hash  $relative"
}
$manifestLines | Set-Content -LiteralPath $manifest -Encoding utf8
[IO.Directory]::Delete($publish, $true)
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory(
    $destinationFull,
    $zipPath,
    [IO.Compression.CompressionLevel]::Optimal,
    $false
)
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToUpperInvariant()
[IO.File]::WriteAllText(
    $zipShaPath,
    "$zipHash  $([IO.Path]::GetFileName($zipPath))`n",
    [Text.UTF8Encoding]::new($false)
)
Write-Output "Persona Workbench Free Preview package directory: $destinationFull"
Write-Output "Persona Workbench Free Preview self-contained ZIP: $zipPath"
Write-Output "Persona Workbench Free Preview ZIP SHA-256: $zipShaPath"

