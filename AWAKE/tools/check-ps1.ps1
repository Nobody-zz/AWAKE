[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string[]]$Path
)

# ASCII-only on purpose: this file has no BOM, and non-ASCII bytes would make
# Windows PowerShell 5.1 misparse it (see SKILL.md section 1). Keep it ASCII.

$ErrorActionPreference = 'Stop'

function Test-Bom([byte[]]$b) {
    return ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF)
}

$rows = @()
foreach ($pattern in $Path) {
    $items = @()
    try { $items = @(Get-Item -Path $pattern -ErrorAction Stop) } catch { }
    if ($items.Count -eq 0) {
        try { $items = @(Get-ChildItem -Path $pattern -File -ErrorAction Stop) } catch { }
    }
    if ($items.Count -eq 0) {
        Write-Output ("no match: " + $pattern)
        continue
    }

    foreach ($item in $items) {
        if ($item.PSIsContainer) { continue }
        $bytes = [IO.File]::ReadAllBytes($item.FullName)
        $bom = Test-Bom $bytes

        # Count non-ASCII payload bytes. Skip the BOM itself so a clean
        # BOM-prefixed ASCII file reports 0.
        $start = 0
        if ($bom) { $start = 3 }
        $nonAscii = 0
        for ($i = $start; $i -lt $bytes.Length; $i++) {
            if ($bytes[$i] -gt 0x7F) { $nonAscii++ }
        }

        $crlf = 0
        $lf = 0
        for ($i = 0; $i -lt $bytes.Length; $i++) {
            if ($bytes[$i] -eq 0x0A) {
                if ($i -gt 0 -and $bytes[$i - 1] -eq 0x0D) { $crlf++ } else { $lf++ }
            }
        }

        $parseErrors = @()
        $err = $null
        [void][System.Management.Automation.Language.Parser]::ParseFile($item.FullName, [ref]$null, [ref]$err)
        if ($err) { $parseErrors = @($err) }

        $verdict = 'ok'
        if ($parseErrors.Count -gt 0) { $verdict = 'SYNTAX-ERROR' }
        elseif ($nonAscii -gt 0 -and -not $bom) { $verdict = 'NEEDS-BOM' }

        $rows += [pscustomobject]@{
            File         = $item.Name
            Bom          = $bom
            NonAscii     = $nonAscii
            Crlf         = $crlf
            Lf           = $lf
            SyntaxErrors = $parseErrors.Count
            Verdict      = $verdict
        }

        foreach ($e in $parseErrors) {
            Write-Output ("  {0} L{1}: {2}" -f $item.Name, $e.Extent.StartLineNumber, $e.Message)
        }
    }
}

$rows | Format-Table -AutoSize

$fail = @($rows | Where-Object { $_.Verdict -ne 'ok' })
Write-Output ""
if ($fail.Count -gt 0) {
    Write-Output ("FAIL: {0} of {1} file(s) need attention." -f $fail.Count, $rows.Count)
    foreach ($f in $fail) {
        if ($f.Verdict -eq 'NEEDS-BOM') {
            Write-Output ("  NEEDS-BOM : {0}  (has {1} non-ASCII bytes, no UTF-8 BOM)" -f $f.File, $f.NonAscii)
        }
    }
    exit 1
}
Write-Output ("OK: {0} file(s) checked." -f $rows.Count)
exit 0
