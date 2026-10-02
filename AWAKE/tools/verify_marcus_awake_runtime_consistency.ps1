[CmdletBinding()]
param(
    [string]$ProjectRoot = '',
    [ValidateSet('1.3.15', '1.4.8')]
    [string]$BannerlordApi = '1.4.8',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$GameModule = 'D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE',
    [string]$RuntimeBuildRoot = '',
    [string]$ProviderBuildPath = ''
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path -Parent $PSScriptRoot
}

function Get-FullPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw 'Path must not be empty.'
    }

    $full = [IO.Path]::GetFullPath($Path)
    if ($full.Length -gt 3) {
        $full = $full.TrimEnd('\')
    }
    return $full
}

$ProjectRoot = Get-FullPath $ProjectRoot
$GameModule = Get-FullPath $GameModule

$buildRoot = Join-Path $ProjectRoot "_build_out\$BannerlordApi\$Configuration"
$distModule = Join-Path $ProjectRoot 'dist\Modules\AWAKE'
$distBin = Join-Path $distModule 'bin\Win64_Shipping_Client'
$distRuntime = Join-Path $distBin 'Runtime'
$runtimeBuildRoot = if ([string]::IsNullOrWhiteSpace($RuntimeBuildRoot)) {
    Join-Path $ProjectRoot "framework\MarcusAwakeRuntimeService\_build_out\$Configuration\win-x64"
} else {
    Get-FullPath $RuntimeBuildRoot
}
$providerBuildPath = if ([string]::IsNullOrWhiteSpace($ProviderBuildPath)) {
    Join-Path $ProjectRoot "framework\MarcusAwakeProvider\_build_out\$Configuration\MarcusAwakeProvider.dll"
} else {
    Get-FullPath $ProviderBuildPath
}

$script:failed = $false
$script:syncBlocked = $false
$script:passCount = 0
$script:failures = @()
$script:blocked = @()

function Emit([string]$Message) {
    [Console]::WriteLine($Message)
}

function Add-Pass([string]$Message) {
    $script:passCount++
    Emit ('PASS ' + $Message)
}

function Add-Fail([string]$Message) {
    $script:failed = $true
    $script:failures += $Message
    Emit ('FAIL ' + $Message)
}

function Add-Blocked([string]$Message) {
    $script:syncBlocked = $true
    $script:blocked += $Message
    Emit ('BLOCKED_SYNC ' + $Message)
}

function Value-Or([string]$Value, [string]$Fallback) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Fallback
    }
    return $Value
}

function Get-RelativePath([string]$Root, [string]$Path) {
    $rootFull = Get-FullPath $Root
    $pathFull = Get-FullPath $Path
    if ($pathFull.Equals($rootFull, [StringComparison]::OrdinalIgnoreCase)) {
        return ''
    }

    $prefix = $rootFull + '\'
    if (-not $pathFull.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escaped root: $pathFull"
    }
    return $pathFull.Substring($prefix.Length).Replace('\', '/')
}

function Test-ReparsePoint([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    $item = Get-Item -LiteralPath $Path -Force
    return (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
}

function Test-NoReparsePath([string]$Root, [string]$RelativePath) {
    $current = Get-FullPath $Root
    if (Test-ReparsePoint $current) {
        return $false
    }

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return $true
    }

    foreach ($segment in $RelativePath.Replace('/', '\').Split('\')) {
        if ([string]::IsNullOrWhiteSpace($segment)) {
            continue
        }
        $current = Join-Path $current $segment
        if ((Test-Path -LiteralPath $current -ErrorAction SilentlyContinue) -and (Test-ReparsePoint $current)) {
            return $false
        }
    }
    return $true
}

function Get-FileSha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    try {
        return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    } catch {
        return $null
    }
}

function Get-AssemblyMetadata([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    try {
        $fullPath = (Get-Item -LiteralPath $Path -Force).FullName
        $assemblyName = [Reflection.AssemblyName]::GetAssemblyName($fullPath)
        $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($fullPath)
        return [pscustomobject]@{
            AssemblyVersion = if ($null -eq $assemblyName.Version) { '' } else { $assemblyName.Version.ToString() }
            FileVersion = [string]$fileVersion.FileVersion
            ProductVersion = [string]$fileVersion.ProductVersion
        }
    } catch {
        return $null
    }
}

function Get-SourceConstant([string]$Path, [string]$Name) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    $text = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $pattern = '(?m)\b(?:public|internal|private|protected)?\s*(?:const|static\s+readonly)\s+string\s+' + [regex]::Escape($Name) + '\s*=\s*"([^"]+)"'
    $match = [regex]::Match($text, $pattern)
    if (-not $match.Success) {
        return $null
    }
    return $match.Groups[1].Value
}

function Get-SourceAssemblyAttribute([string]$Path, [string]$AttributeName) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    $text = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $pattern = '\[assembly:\s*' + [regex]::Escape($AttributeName) + '\("([^"]+)"\)\]'
    $match = [regex]::Match($text, $pattern)
    if (-not $match.Success) {
        return $null
    }
    return $match.Groups[1].Value
}

function Test-SourceAssemblyAttribute([string]$Path, [string]$AttributeName) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    $text = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $pattern = '\[assembly:\s*' + [regex]::Escape($AttributeName) + '\('
    return [regex]::IsMatch($text, $pattern)
}

function Get-ModuleVersion([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    try {
        [xml]$xml = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
        return [string]$xml.Module.Version.value
    } catch {
        return $null
    }
}

function Test-BinaryContainsText([string]$Path, [string]$Value) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf) -or [string]::IsNullOrWhiteSpace($Value)) {
        return $false
    }

    try {
        $bytes = [IO.File]::ReadAllBytes((Get-Item -LiteralPath $Path -Force).FullName)
        $ascii = [Text.Encoding]::ASCII.GetString($bytes)
        if ($ascii.IndexOf($Value, [StringComparison]::Ordinal) -ge 0) {
            return $true
        }
        $unicode = [Text.Encoding]::Unicode.GetString($bytes)
        return $unicode.IndexOf($Value, [StringComparison]::Ordinal) -ge 0
    } catch {
        return $false
    }
}

function Normalize-ManifestPath([string]$RawPath) {
    if ([string]::IsNullOrWhiteSpace($RawPath)) {
        throw 'empty relative path'
    }

    $windowsPath = $RawPath.Trim().Replace('/', '\')
    if ([IO.Path]::IsPathRooted($windowsPath)) {
        throw "rooted path: $RawPath"
    }
    $segments = $windowsPath.Split('\')
    if ($segments.Count -eq 0 -or @($segments | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0) {
        throw "empty path segment: $RawPath"
    }
    foreach ($segment in $segments) {
        if ($segment -eq '.') {
            throw "dot path segment: $RawPath"
        }
        if ($segment -eq '..') {
            throw "parent path segment: $RawPath"
        }
    }
    if ($windowsPath -match '[<>:"|?*]') {
        throw "invalid path character: $RawPath"
    }
    if ($windowsPath.EndsWith('\')) {
        throw "directory path is not a file entry: $RawPath"
    }
    return $windowsPath.Replace('\', '/')
}

function Test-ForbiddenRelativePath([string]$RelativePath) {
    $normalized = $RelativePath.Replace('\', '/')
    $extension = [IO.Path]::GetExtension($normalized).ToLowerInvariant()
    $forbiddenExtensions = @(
        '.pdb', '.cs', '.csproj', '.sln', '.props', '.targets', '.user',
        '.snk', '.key', '.pem', '.pfx', '.p12', '.db', '.sqlite', '.sqlite3',
        '.log', '.dmp', '.bak', '.tmp'
    )
    if ($forbiddenExtensions -contains $extension) {
        return $true
    }

    $forbiddenDirectories = @(
        'source', 'src', 'secrets', 'secret', 'keys', 'key', 'credentials',
        'credential', 'logs', 'log', 'database', 'databases', 'obj', 'bin'
    )
    foreach ($segment in $normalized.Split('/')) {
        if ($forbiddenDirectories -contains $segment.ToLowerInvariant()) {
            return $true
        }
    }

    $fileName = [IO.Path]::GetFileName($normalized).ToLowerInvariant()
    if ($fileName -match 'secret|credential|privatekey|password|token') {
        return $true
    }
    if ($fileName -in @('awake.dll', 'marcusai.framework.dll', 'mcmv5.dll', 'sha256sums')) {
        return $true
    }
    return $false
}

function Compare-Files(
    [string]$Label,
    [string]$ExpectedPath,
    [string]$ActualPath,
    [bool]$BlockActual)
{
    if (-not (Test-Path -LiteralPath $ExpectedPath -PathType Leaf)) {
        Add-Fail "$Label expected file missing path=$ExpectedPath"
        return $false
    }

    if (-not (Test-Path -LiteralPath $ActualPath -PathType Leaf)) {
        if ($BlockActual) {
            Add-Blocked "$Label actual file missing path=$ActualPath"
        } else {
            Add-Fail "$Label actual file missing path=$ActualPath"
        }
        return $false
    }

    $expectedHash = Get-FileSha256 $ExpectedPath
    $actualHash = Get-FileSha256 $ActualPath
    if ([string]::IsNullOrWhiteSpace($expectedHash) -or [string]::IsNullOrWhiteSpace($actualHash)) {
        if ($BlockActual) {
            Add-Blocked "$Label hash could not be read"
        } else {
            Add-Fail "$Label hash could not be read"
        }
        return $false
    }

    $expectedLength = [int64](Get-Item -LiteralPath $ExpectedPath -Force).Length
    $actualLength = [int64](Get-Item -LiteralPath $ActualPath -Force).Length
    if ($expectedHash -ne $actualHash -or $expectedLength -ne $actualLength) {
        $message = "$Label mismatch expected_sha256=$expectedHash actual_sha256=$actualHash expected_length=$expectedLength actual_length=$actualLength"
        if ($BlockActual) {
            Add-Blocked $message
        } else {
            Add-Fail $message
        }
        return $false
    }

    Add-Pass "$Label sha256=$actualHash length=$actualLength"
    return $true
}

function Get-RuntimePackageAudit([string]$Root) {
    $issues = New-Object 'System.Collections.Generic.List[string]'
    $fullRoot = Get-FullPath $Root
    $payloadMap = @{}
    $expectedMap = @{}
    $manifest = $null
    $manifestPath = Join-Path $fullRoot 'manifest.json'
    $sumsPath = Join-Path $fullRoot 'SHA256SUMS.txt'

    if (-not (Test-Path -LiteralPath $fullRoot -PathType Container)) {
        [void]$issues.Add("package directory missing path=$fullRoot")
        return [pscustomobject]@{
            Root = $fullRoot
            Valid = $false
            Issues = [string[]]$issues
            Manifest = $null
            ManifestHash = $null
            SumsHash = $null
            PayloadCount = 0
            PayloadMap = @{}
        }
    }

    if (Test-ReparsePoint $fullRoot) {
        [void]$issues.Add('package root is a reparse point')
    }

    $items = @()
    try {
        $items = @(Get-ChildItem -LiteralPath $fullRoot -Recurse -Force -ErrorAction Stop)
    } catch {
        [void]$issues.Add('package enumeration failed: ' + $_.Exception.Message)
    }

    foreach ($item in $items) {
        $relative = Get-RelativePath $fullRoot $item.FullName
        $normalized = $relative.Replace('\', '/')
        if (-not (Test-NoReparsePath $fullRoot $normalized)) {
            [void]$issues.Add("reparse path: $normalized")
        }

        if (Test-ForbiddenRelativePath $normalized) {
            [void]$issues.Add("forbidden file or path: $normalized")
        }

        $isRootManifest = $normalized.Equals('manifest.json', [StringComparison]::OrdinalIgnoreCase)
        $isRootSums = $normalized.Equals('SHA256SUMS.txt', [StringComparison]::OrdinalIgnoreCase)
        if (($item.Name -ieq 'manifest.json' -or $item.Name -ieq 'SHA256SUMS.txt') -and -not ($isRootManifest -or $isRootSums)) {
            [void]$issues.Add("metadata file must be at package root: $normalized")
        }

        if (-not $item.PSIsContainer -and -not $isRootManifest -and -not $isRootSums) {
            $key = $normalized.ToLowerInvariant()
            if ($payloadMap.ContainsKey($key)) {
                [void]$issues.Add("duplicate package path: $normalized")
            } else {
                $payloadMap[$key] = [pscustomobject]@{
                    Path = $normalized
                    FullPath = $item.FullName
                    Length = [int64]$item.Length
                }
            }
        }
    }

    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        [void]$issues.Add('manifest.json is missing')
    } else {
        try {
            $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        } catch {
            [void]$issues.Add('manifest.json is not valid JSON: ' + $_.Exception.Message)
        }
    }

    if ($null -ne $manifest) {
        if ([string]$manifest.schemaVersion -ne 'awake.embedded-runtime.v1') {
            [void]$issues.Add("manifest schemaVersion mismatch actual=$($manifest.schemaVersion)")
        }
        if ([string]$manifest.product -ne 'AWAKE.RuntimeService') {
            [void]$issues.Add("manifest product mismatch actual=$($manifest.product)")
        }
        if ([string]$manifest.rid -ne 'win-x64') {
            [void]$issues.Add("manifest rid mismatch actual=$($manifest.rid)")
        }
        if ($manifest.selfContained -ne $true) {
            [void]$issues.Add('manifest selfContained must be true')
        }
        if ([string]$manifest.configuration -ne $Configuration) {
            [void]$issues.Add("manifest configuration mismatch expected=$Configuration actual=$($manifest.configuration)")
        }
        if ([string]$manifest.entryPoint -ne 'MarcusAwakeRuntimeService.exe') {
            [void]$issues.Add("manifest entryPoint mismatch actual=$($manifest.entryPoint)")
        }

        $requiredComponents = @('Runtime', 'Provider', 'Storage', 'Transport', 'Framework', 'SQLite')
        $components = @($manifest.components | ForEach-Object { [string]$_ })
        foreach ($component in $requiredComponents) {
            if ($components -notcontains $component) {
                [void]$issues.Add("manifest component missing: $component")
            }
        }

        $entries = @($manifest.files)
        if ($entries.Count -eq 0) {
            [void]$issues.Add('manifest files is empty')
        }

        foreach ($entry in $entries) {
            if ($null -eq $entry) {
                [void]$issues.Add('manifest contains a null file entry')
                continue
            }

            $normalized = $null
            try {
                $normalized = Normalize-ManifestPath ([string]$entry.path)
            } catch {
                [void]$issues.Add('manifest path invalid: ' + $_.Exception.Message)
                continue
            }

            $manifestListsMetadata = $normalized.Equals('manifest.json', [StringComparison]::OrdinalIgnoreCase) -or $normalized.Equals('SHA256SUMS.txt', [StringComparison]::OrdinalIgnoreCase)
            if ($manifestListsMetadata) {
                [void]$issues.Add("manifest cannot list metadata file: $normalized")
            }

            $key = $normalized.ToLowerInvariant()
            if ($expectedMap.ContainsKey($key)) {
                [void]$issues.Add("duplicate manifest path: $normalized")
                continue
            }

            $hash = [string]$entry.sha256
            if ($hash -notmatch '^[0-9A-Fa-f]{64}$') {
                [void]$issues.Add("manifest SHA-256 invalid: $normalized")
                $hash = ''
            } else {
                $hash = $hash.ToLowerInvariant()
            }

            $length = 0L
            $lengthValid = $null -ne $entry.length -and [long]::TryParse(
                [string]$entry.length,
                [Globalization.NumberStyles]::Integer,
                [Globalization.CultureInfo]::InvariantCulture,
                [ref]$length) -and $length -ge 0
            if (-not $lengthValid) {
                [void]$issues.Add("manifest length invalid: $normalized")
            }

            $expectedMap[$key] = [pscustomobject]@{
                Path = $normalized
                Hash = $hash
                Length = $length
            }
        }

        if ($payloadMap.Count -ne $expectedMap.Count) {
            [void]$issues.Add("manifest payload count mismatch manifest=$($expectedMap.Count) actual=$($payloadMap.Count)")
        }

        foreach ($key in $expectedMap.Keys) {
            $expected = $expectedMap[$key]
            if (-not $payloadMap.ContainsKey($key)) {
                [void]$issues.Add("manifest file missing from package: $($expected.Path)")
                continue
            }

            $actual = $payloadMap[$key]
            $actualHash = Get-FileSha256 $actual.FullPath
            if ([string]::IsNullOrWhiteSpace($actualHash) -or $actualHash -ne $expected.Hash) {
                [void]$issues.Add("manifest SHA-256 mismatch: $($expected.Path)")
            }
            if ($actual.Length -ne $expected.Length) {
                [void]$issues.Add("manifest length mismatch: $($expected.Path)")
            }
        }

        foreach ($key in $payloadMap.Keys) {
            if (-not $expectedMap.ContainsKey($key)) {
                [void]$issues.Add("package file is not listed in manifest: $($payloadMap[$key].Path)")
            }
        }
    }

    $requiredFiles = @(
        'MarcusAwakeRuntimeService.exe',
        'MarcusAwakeRuntimeService.dll',
        'MarcusAwakeRuntimeService.deps.json',
        'MarcusAwakeRuntimeService.runtimeconfig.json',
        'MarcusAwakeProvider.dll',
        'MarcusAwakeStorage.dll',
        'MarcusAwakeTransport.dll',
        'MarcusAwakeFramework.dll',
        'Microsoft.Data.Sqlite.dll',
        'SQLitePCLRaw.batteries_v2.dll',
        'SQLitePCLRaw.core.dll',
        'SQLitePCLRaw.provider.e_sqlite3.dll'
    )
    foreach ($required in $requiredFiles) {
        $key = $required.ToLowerInvariant()
        if (-not $payloadMap.ContainsKey($key)) {
            [void]$issues.Add("required runtime entry missing: $required")
        }
    }
    $nativeSqlite = @($payloadMap.Values | Where-Object { [IO.Path]::GetFileName($_.Path) -ieq 'e_sqlite3.dll' })
    if ($nativeSqlite.Count -eq 0) {
        [void]$issues.Add('required SQLite native entry missing: e_sqlite3.dll')
    }

    if (-not (Test-Path -LiteralPath $sumsPath -PathType Leaf)) {
        [void]$issues.Add('SHA256SUMS.txt is missing')
    } else {
        $sumMap = @{}
        try {
            foreach ($line in @(Get-Content -LiteralPath $sumsPath -Encoding UTF8)) {
                $text = [string]$line
                if ([string]::IsNullOrWhiteSpace($text)) {
                    [void]$issues.Add('SHA256SUMS.txt contains a blank line')
                    continue
                }
                if ($text -notmatch '^(?<hash>[0-9A-Fa-f]{64})  (?<path>.+)$') {
                    [void]$issues.Add("SHA256SUMS.txt line invalid: $text")
                    continue
                }

                $normalized = $null
                try {
                    $normalized = Normalize-ManifestPath $Matches['path']
                } catch {
                    [void]$issues.Add('SHA256SUMS.txt path invalid: ' + $_.Exception.Message)
                    continue
                }
                $key = $normalized.ToLowerInvariant()
                if ($sumMap.ContainsKey($key)) {
                    [void]$issues.Add("duplicate SHA256SUMS.txt path: $normalized")
                    continue
                }
                if (-not $expectedMap.ContainsKey($key)) {
                    [void]$issues.Add("SHA256SUMS.txt lists unknown file: $normalized")
                } elseif ($normalized.Equals('manifest.json', [StringComparison]::OrdinalIgnoreCase) -or $normalized.Equals('SHA256SUMS.txt', [StringComparison]::OrdinalIgnoreCase)) {
                    [void]$issues.Add("SHA256SUMS.txt lists metadata file: $normalized")
                }
                $sumMap[$key] = ([string]$Matches['hash']).ToLowerInvariant()
            }
        } catch {
            [void]$issues.Add('SHA256SUMS.txt could not be read: ' + $_.Exception.Message)
        }

        if ($sumMap.Count -ne $expectedMap.Count) {
            [void]$issues.Add("SHA256SUMS.txt count mismatch sums=$($sumMap.Count) manifest=$($expectedMap.Count)")
        }
        foreach ($key in $expectedMap.Keys) {
            if (-not $sumMap.ContainsKey($key)) {
                [void]$issues.Add("SHA256SUMS.txt missing: $($expectedMap[$key].Path)")
            } elseif ($sumMap[$key] -ne $expectedMap[$key].Hash) {
                [void]$issues.Add("SHA256SUMS.txt hash mismatch: $($expectedMap[$key].Path)")
            }
        }
    }

    return [pscustomobject]@{
        Root = $fullRoot
        Valid = $issues.Count -eq 0
        Issues = [string[]]$issues
        Manifest = $manifest
        ManifestHash = Get-FileSha256 $manifestPath
        SumsHash = Get-FileSha256 $sumsPath
        PayloadCount = $payloadMap.Count
        PayloadMap = $payloadMap
    }
}

function Report-RuntimePackageAudit($Audit, [string]$Label, [bool]$BlockSync) {
    if ($null -eq $Audit) {
        if ($BlockSync) {
            Add-Blocked "$Label audit returned no result"
        } else {
            Add-Fail "$Label audit returned no result"
        }
        return
    }

    if ($Audit.Valid) {
        Add-Pass "$Label valid files=$($Audit.PayloadCount) manifest_sha256=$($Audit.ManifestHash) sums_sha256=$($Audit.SumsHash)"
        return
    }

    foreach ($issue in @($Audit.Issues)) {
        if ($BlockSync) {
            Add-Blocked "$Label $issue"
        } else {
            Add-Fail "$Label $issue"
        }
    }
}

function Compare-RuntimePayloadToBuild($Audit, [string]$RuntimeRoot, [string]$ProviderPath) {
    if ($null -eq $Audit -or -not $Audit.Valid) {
        return
    }

    if (-not (Test-Path -LiteralPath $RuntimeRoot -PathType Container)) {
        Add-Fail "runtime build output missing path=$RuntimeRoot"
        return
    }
    if (-not (Test-Path -LiteralPath $ProviderPath -PathType Leaf)) {
        Add-Fail "provider build output missing path=$ProviderPath"
        return
    }

    $mismatches = @()
    foreach ($entry in $Audit.PayloadMap.Values) {
        $sourcePath = if ($entry.Path.Equals('MarcusAwakeProvider.dll', [StringComparison]::OrdinalIgnoreCase)) {
            $ProviderPath
        } else {
            Join-Path $RuntimeRoot $entry.Path.Replace('/', '\')
        }

        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            $mismatches += "$($entry.Path): source missing"
            continue
        }

        $sourceHash = Get-FileSha256 $sourcePath
        $sourceLength = [int64](Get-Item -LiteralPath $sourcePath -Force).Length
        $packageHash = Get-FileSha256 $entry.FullPath
        if ($sourceHash -ne $packageHash -or $sourceLength -ne $entry.Length) {
            $mismatches += "$($entry.Path): source_sha256=$sourceHash package_sha256=$packageHash source_length=$sourceLength package_length=$($entry.Length)"
        }
    }

    if ($mismatches.Count -eq 0) {
        Add-Pass "Runtime payload matches current build outputs files=$($Audit.PayloadCount)"
    } else {
        foreach ($mismatch in $mismatches) {
            Add-Fail "Runtime payload build mismatch $mismatch"
        }
    }
}

function Compare-RuntimePackages($Expected, $Actual, [string]$Label) {
    if ($null -eq $Expected -or $null -eq $Actual -or -not $Expected.Valid -or -not $Actual.Valid) {
        return
    }

    $mismatches = @()
    if ($Expected.ManifestHash -ne $Actual.ManifestHash) {
        $mismatches += "manifest_sha256 expected=$($Expected.ManifestHash) actual=$($Actual.ManifestHash)"
    }
    if ($Expected.SumsHash -ne $Actual.SumsHash) {
        $mismatches += "sums_sha256 expected=$($Expected.SumsHash) actual=$($Actual.SumsHash)"
    }
    if ($Expected.PayloadMap.Count -ne $Actual.PayloadMap.Count) {
        $mismatches += "payload_count expected=$($Expected.PayloadMap.Count) actual=$($Actual.PayloadMap.Count)"
    }

    foreach ($key in $Expected.PayloadMap.Keys) {
        $expectedEntry = $Expected.PayloadMap[$key]
        if (-not $Actual.PayloadMap.ContainsKey($key)) {
            $mismatches += "$($expectedEntry.Path): actual file missing"
            continue
        }
        $actualEntry = $Actual.PayloadMap[$key]
        $expectedHash = Get-FileSha256 $expectedEntry.FullPath
        $actualHash = Get-FileSha256 $actualEntry.FullPath
        if ($expectedHash -ne $actualHash -or $expectedEntry.Length -ne $actualEntry.Length) {
            $mismatches += "$($expectedEntry.Path): expected_sha256=$expectedHash actual_sha256=$actualHash expected_length=$($expectedEntry.Length) actual_length=$($actualEntry.Length)"
        }
    }
    foreach ($key in $Actual.PayloadMap.Keys) {
        if (-not $Expected.PayloadMap.ContainsKey($key)) {
            $mismatches += "$($Actual.PayloadMap[$key].Path): unexpected actual file"
        }
    }

    if ($mismatches.Count -eq 0) {
        Add-Pass "$Label files=$($Expected.PayloadMap.Count) manifest_sha256=$($Actual.ManifestHash) sums_sha256=$($Actual.SumsHash)"
    } else {
        foreach ($mismatch in $mismatches) {
            Add-Blocked "$Label $mismatch"
        }
    }
}

function Check-AssemblyMetadata(
    [string]$Label,
    [string]$Path,
    [string]$ExpectedAssemblyVersion,
    [string]$ExpectedFileVersion,
    [string]$ExpectedProductVersion)
{
    $metadata = Get-AssemblyMetadata $Path
    if ($null -eq $metadata) {
        Add-Fail "$Label assembly metadata unreadable path=$Path"
        return
    }

    if (-not [string]::IsNullOrWhiteSpace($ExpectedAssemblyVersion) -and $metadata.AssemblyVersion -ne $ExpectedAssemblyVersion) {
        Add-Fail "$Label AssemblyVersion mismatch expected=$ExpectedAssemblyVersion actual=$($metadata.AssemblyVersion)"
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedFileVersion) -and $metadata.FileVersion -ne $ExpectedFileVersion) {
        Add-Fail "$Label FileVersion mismatch expected=$ExpectedFileVersion actual=$($metadata.FileVersion)"
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedProductVersion) -and $metadata.ProductVersion -ne $ExpectedProductVersion) {
        Add-Fail "$Label ProductVersion mismatch expected=$ExpectedProductVersion actual=$($metadata.ProductVersion)"
    }
    $expectedMetadataComplete = -not [string]::IsNullOrWhiteSpace($ExpectedAssemblyVersion) -and -not [string]::IsNullOrWhiteSpace($ExpectedFileVersion) -and -not [string]::IsNullOrWhiteSpace($ExpectedProductVersion)
    $metadataMatches = $metadata.AssemblyVersion -eq $ExpectedAssemblyVersion -and $metadata.FileVersion -eq $ExpectedFileVersion -and $metadata.ProductVersion -eq $ExpectedProductVersion
    if ($expectedMetadataComplete -and $metadataMatches) {
        Add-Pass "$Label metadata assembly=$($metadata.AssemblyVersion) file=$($metadata.FileVersion) product=$($metadata.ProductVersion)"
    }
}

Emit '===== AWAKE Marcus Runtime Consistency ====='
Emit "ProjectRoot=$ProjectRoot"
Emit "BannerlordApi=$BannerlordApi"
Emit "Configuration=$Configuration"
Emit "BuildRoot=$buildRoot"
Emit "DistRuntime=$distRuntime"
Emit "GameModule=$GameModule"
Emit "RuntimeBuildRoot=$runtimeBuildRoot"
Emit "ProviderBuildPath=$providerBuildPath"

try {
    $constantsPath = Join-Path $ProjectRoot 'src\AwakeConstants.cs'
    $subModulePath = Join-Path $ProjectRoot 'SubModule.xml'
    $subModuleSourcePath = Join-Path $ProjectRoot 'src\SubModule.cs'
    $distSubModulePath = Join-Path $distModule 'SubModule.xml'

    $version = Get-SourceConstant $constantsPath 'Version'
    $informationalVersion = Get-SourceConstant $constantsPath 'InformationalVersion'
    $buildId = Get-SourceConstant $constantsPath 'BuildId'
    $moduleVersion = Get-ModuleVersion $subModulePath
    $distModuleVersion = Get-ModuleVersion $distSubModulePath
    $sourceAssemblyVersion = Get-SourceAssemblyAttribute $subModuleSourcePath 'AssemblyVersion'
    $sourceInformationalAttributePresent = Test-SourceAssemblyAttribute $subModuleSourcePath 'AssemblyInformationalVersion'
    $expectedAwakeAssemblyVersion = if ([string]::IsNullOrWhiteSpace($sourceAssemblyVersion)) { "$version.0" } else { $sourceAssemblyVersion }

    Emit "TRACE_BUILD_ID=$(Value-Or $buildId 'unknown')"
    Emit "TRACE_VERSION=$(Value-Or $version 'unknown')"
    Emit "TRACE_INFORMATIONAL_VERSION=$(Value-Or $informationalVersion 'unknown')"

    if ([string]::IsNullOrWhiteSpace($version)) { Add-Fail 'AwakeConstants.cs Version is missing' }
    if ([string]::IsNullOrWhiteSpace($informationalVersion)) { Add-Fail 'AwakeConstants.cs InformationalVersion is missing' }
    if ([string]::IsNullOrWhiteSpace($buildId) -or $buildId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{7,127}$') { Add-Fail 'AwakeConstants.cs BuildId is missing or malformed' }
    if (-not [string]::IsNullOrWhiteSpace($informationalVersion) -and $informationalVersion -notmatch [regex]::Escape("bannerlord.$BannerlordApi")) {
        Add-Fail "InformationalVersion does not identify BannerlordApi=$BannerlordApi"
    }
    if ([string]::IsNullOrWhiteSpace($moduleVersion)) {
        Add-Fail 'SubModule.xml version is missing'
    } elseif ($moduleVersion -ne "v$version") {
        Add-Fail "SubModule.xml version mismatch expected=v$version actual=$moduleVersion"
    } else {
        Add-Pass "source module version=$moduleVersion"
    }
    if ([string]::IsNullOrWhiteSpace($distModuleVersion)) {
        Add-Fail 'dist SubModule.xml version is missing'
    } elseif ($distModuleVersion -ne $moduleVersion) {
        Add-Fail "dist SubModule.xml version mismatch expected=$moduleVersion actual=$distModuleVersion"
    } else {
        Add-Pass "dist module version=$distModuleVersion"
    }
    if ([string]::IsNullOrWhiteSpace($sourceAssemblyVersion)) {
        Add-Fail 'SubModule.cs AssemblyVersion is missing'
    }
    if (-not $sourceInformationalAttributePresent) {
        Add-Fail 'SubModule.cs AssemblyInformationalVersion is missing'
    }

    $buildAwakePath = Join-Path $buildRoot 'Awake.dll'
    $distAwakePath = Join-Path $distBin 'Awake.dll'
    Compare-Files 'Awake.dll build-to-dist' $buildAwakePath $distAwakePath $false | Out-Null
    Check-AssemblyMetadata 'Awake.dll build' $buildAwakePath $expectedAwakeAssemblyVersion $expectedAwakeAssemblyVersion $informationalVersion
    Check-AssemblyMetadata 'Awake.dll dist' $distAwakePath $expectedAwakeAssemblyVersion $expectedAwakeAssemblyVersion $informationalVersion

    if (-not [string]::IsNullOrWhiteSpace($buildId)) {
        if (Test-BinaryContainsText $buildAwakePath $buildId) { Add-Pass 'Awake.dll build contains current BuildId marker' } else { Add-Fail 'Awake.dll build does not contain current BuildId marker' }
        if (Test-BinaryContainsText $distAwakePath $buildId) { Add-Pass 'Awake.dll dist contains current BuildId marker' } else { Add-Fail 'Awake.dll dist does not contain current BuildId marker' }
    }

    $embeddedDefinitions = @(
        [ordered]@{
            Name = 'MarcusAwakeFramework.dll'
            Build = Join-Path $buildRoot 'MarcusAwakeFramework.dll'
            FrameworkBuild = Join-Path $ProjectRoot "framework\MarcusAwakeFramework\_build_out\$Configuration\MarcusAwakeFramework.dll"
            DistEmbedded = Join-Path $distBin 'MarcusAwakeFramework.dll'
            Runtime = Join-Path $distRuntime 'MarcusAwakeFramework.dll'
            SourceAssemblyInfo = Join-Path $ProjectRoot 'framework\MarcusAwakeFramework\src\AssemblyInfo.cs'
        },
        [ordered]@{
            Name = 'MarcusAwakeTransport.dll'
            Build = Join-Path $buildRoot 'MarcusAwakeTransport.dll'
            FrameworkBuild = Join-Path $ProjectRoot "framework\MarcusAwakeTransport\_build_out\$Configuration\MarcusAwakeTransport.dll"
            DistEmbedded = Join-Path $distBin 'MarcusAwakeTransport.dll'
            Runtime = Join-Path $distRuntime 'MarcusAwakeTransport.dll'
            SourceAssemblyInfo = Join-Path $ProjectRoot 'framework\MarcusAwakeTransport\src\AssemblyInfo.cs'
        }
    )

    foreach ($definition in $embeddedDefinitions) {
        Compare-Files "$($definition.Name) framework-build-to-AWAKE-build" $definition.FrameworkBuild $definition.Build $false | Out-Null
        Compare-Files "$($definition.Name) AWAKE-build-to-dist-embedded" $definition.Build $definition.DistEmbedded $false | Out-Null
        Compare-Files "$($definition.Name) dist-embedded-to-runtime" $definition.DistEmbedded $definition.Runtime $false | Out-Null
        $expectedVersion = Get-SourceAssemblyAttribute $definition.SourceAssemblyInfo 'AssemblyVersion'
        if ([string]::IsNullOrWhiteSpace($expectedVersion)) {
            Add-Fail "$($definition.Name) source AssemblyVersion is missing"
        } else {
            Check-AssemblyMetadata "$($definition.Name) build" $definition.Build $expectedVersion $null $null
            Check-AssemblyMetadata "$($definition.Name) dist embedded" $definition.DistEmbedded $expectedVersion $null $null
            Check-AssemblyMetadata "$($definition.Name) runtime" $definition.Runtime $expectedVersion $null $null
        }
    }

    $runtimeServiceSourceInfo = Join-Path $ProjectRoot 'framework\MarcusAwakeRuntimeService\src\AssemblyInfo.cs'
    $runtimeServiceVersion = Get-SourceAssemblyAttribute $runtimeServiceSourceInfo 'AssemblyVersion'
    if ([string]::IsNullOrWhiteSpace($runtimeServiceVersion)) {
        Add-Fail 'MarcusAwakeRuntimeService source AssemblyVersion is missing'
    } else {
        Check-AssemblyMetadata 'MarcusAwakeRuntimeService runtime' (Join-Path $distRuntime 'MarcusAwakeRuntimeService.dll') $runtimeServiceVersion $null $null
    }

    $distRuntimeAudit = Get-RuntimePackageAudit $distRuntime
    Report-RuntimePackageAudit $distRuntimeAudit 'dist Runtime package' $false
    Compare-RuntimePayloadToBuild $distRuntimeAudit $runtimeBuildRoot $providerBuildPath

    if (-not (Test-Path -LiteralPath $GameModule -PathType Container)) {
        Add-Blocked "game module directory missing path=$GameModule"
    } else {
        $gameBin = Join-Path $GameModule 'bin\Win64_Shipping_Client'
        Compare-Files 'Awake.dll dist-to-game' $distAwakePath (Join-Path $gameBin 'Awake.dll') $true | Out-Null
        foreach ($definition in $embeddedDefinitions) {
            Compare-Files "$($definition.Name) dist-embedded-to-game" $definition.DistEmbedded (Join-Path $gameBin $definition.Name) $true | Out-Null
        }

        $gameRuntime = Join-Path $gameBin 'Runtime'
        $gameRuntimeAudit = Get-RuntimePackageAudit $gameRuntime
        Report-RuntimePackageAudit $gameRuntimeAudit 'game Runtime package' $true
        Compare-RuntimePackages $distRuntimeAudit $gameRuntimeAudit 'game Runtime package matches dist'

        $gameSubModulePath = Join-Path $GameModule 'SubModule.xml'
        $gameModuleVersion = Get-ModuleVersion $gameSubModulePath
        if ([string]::IsNullOrWhiteSpace($gameModuleVersion) -or $gameModuleVersion -ne $moduleVersion) {
            Add-Blocked "game SubModule.xml version mismatch expected=$moduleVersion actual=$(Value-Or $gameModuleVersion 'missing')"
        } else {
            Add-Pass "game module version=$gameModuleVersion"
        }
    }
} catch {
    Add-Fail ('unexpected audit exception: ' + $_.Exception.Message)
}

Emit "SUMMARY passes=$script:passCount failures=$($script:failures.Count) blocked_sync=$($script:blocked.Count)"
if ($script:failed) {
    Emit 'RUNTIME_CONSISTENCY_FAILED'
    exit 1
}
if ($script:syncBlocked) {
    Emit 'RUNTIME_CONSISTENCY_BLOCKED_SYNC'
    Emit 'SYNC_STATUS=BLOCKED_SYNC'
    exit 2
}
Emit 'RUNTIME_CONSISTENCY_OK'
Emit 'SYNC_STATUS=SYNC_OK'
exit 0
