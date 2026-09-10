param(
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [string]$ZipPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Full([string]$path) { [IO.Path]::GetFullPath($path) }
function Fail([string]$message) { throw "Persona Workbench package ZIP test failed: $message" }
function Relative([string]$path, [string]$base) {
    $path.Substring($base.Length).TrimStart([char]92, [char]47).Replace([char]47, [char]92)
}
function Hash([string]$path) {
    (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToUpperInvariant()
}

$package = Full $PackagePath
if (-not (Test-Path -LiteralPath $package -PathType Container)) { Fail "package directory is missing: $package" }
if ([string]::IsNullOrWhiteSpace($ZipPath)) { $ZipPath = "$package.zip" }
$zip = Full $ZipPath
$sidecar = "$zip.sha256"
if (-not (Test-Path -LiteralPath $zip -PathType Leaf)) { Fail "ZIP is missing: $zip" }
if (-not (Test-Path -LiteralPath $sidecar -PathType Leaf)) { Fail "ZIP sidecar is missing: $sidecar" }

$sidecarLines = @(Get-Content -LiteralPath $sidecar)
if ($sidecarLines.Count -ne 1 -or $sidecarLines[0] -notmatch '^([0-9A-Fa-f]{64})  ([^\\/:]+)$') {
    Fail "ZIP sidecar must contain one '<sha256>  <filename>' line."
}
$declaredHash = $matches[1].ToUpperInvariant()
$declaredName = $matches[2]
if ($declaredName -cne [IO.Path]::GetFileName($zip)) { Fail "ZIP sidecar names the wrong file." }
if ($declaredHash -cne (Hash $zip)) { Fail "ZIP sidecar hash does not match ZIP bytes." }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$extract = Join-Path ([IO.Path]::GetTempPath()) ("pwb-package-zip-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $extract | Out-Null
try {
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $entryNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $archive.Entries) {
            $entryName = $entry.FullName.Replace([char]47, [char]92)
            if ([string]::IsNullOrWhiteSpace($entryName)) { Fail "ZIP contains an empty entry name." }
            if ([IO.Path]::IsPathRooted($entryName) -or $entryName.StartsWith("..\", [StringComparison]::Ordinal)) {
                Fail "ZIP contains a path traversal entry: $($entry.FullName)"
            }
            if (-not $entryNames.Add($entryName)) { Fail "ZIP contains duplicate entries: $($entry.FullName)" }
        }
    }
    finally {
        $archive.Dispose()
    }
    [IO.Compression.ZipFile]::ExtractToDirectory($zip, $extract)

    $packageRoot = $package.TrimEnd([char]92, [char]47) + [char]92
    $extractRoot = $extract.TrimEnd([char]92, [char]47) + [char]92
    $packageFiles = @{}
    foreach ($file in (Get-ChildItem -LiteralPath $package -Recurse -File)) {
        $relative = Relative $file.FullName $package
        $packageFiles[$relative] = Hash $file.FullName
    }
    $extractFiles = @{}
    foreach ($file in (Get-ChildItem -LiteralPath $extract -Recurse -File)) {
        $relative = Relative $file.FullName $extract
        $extractFiles[$relative] = Hash $file.FullName
    }
    if ($packageFiles.Count -ne $extractFiles.Count) {
        Fail "extracted file count differs: package=$($packageFiles.Count), extracted=$($extractFiles.Count)"
    }
    foreach ($relative in $packageFiles.Keys) {
        if (-not $extractFiles.ContainsKey($relative)) { Fail "ZIP is missing package file: $relative" }
        if ($extractFiles[$relative] -cne $packageFiles[$relative]) { Fail "ZIP file hash differs: $relative" }
    }
    if (-not $extractFiles.ContainsKey("PersonaWorkbench.Web.exe")) { Fail "ZIP does not contain the self-contained web executable." }
    foreach ($forbidden in @("PersonaWorkbench.Web.runtimeconfig.json", "PersonaWorkbench.Web.deps.json")) {
        if ($extractFiles.ContainsKey($forbidden)) { Fail "ZIP contains forbidden framework-dependent file: $forbidden" }
    }
}
finally {
    if (Test-Path -LiteralPath $extract) { Remove-Item -LiteralPath $extract -Recurse -Force }
}

Write-Output "Persona Workbench package ZIP readback: PASS"
Write-Output "PackagePath=$package"
Write-Output "ZipPath=$zip"
Write-Output "ZipSha256=$declaredHash"
