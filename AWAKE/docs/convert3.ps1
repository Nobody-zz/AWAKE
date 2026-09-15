# 清扫历史文字提示的引号残留：
#  1) selfClaimRules/realSelfBehaviors/selfClaimExamples 里的转义双引号 \" -> 全角 “ ”
#  2) 转义单引号 \u0027 -> 全角 ‘ ’
#  3) realSelfBehaviors 里字面的裸引号 '  -> 全角 ‘ ’（彭同"依律当如是"）
#  4) selfClaimExamples 里并列用的 ASCII "/" -> 全角 ／
# 转义用负向后顾 (?<!\\)\\"，避免把前导双反斜杠后的结构引号误判为内容。
$ErrorActionPreference = "Stop"
$base = "D:\AWAKE-Dev\AWAKE"
$charDir = Join-Path $base "tools\persona-workbench\characters"

$script:X = 0

function Convert-Pair {
    param([string]$Type)
    switch ($Type) {
        "DQ" { $open  = [string][char]0x201C; $close = [string][char]0x201D }
        "SQ" { $open  = [string][char]0x2018; $close = [string][char]0x2019 }
        "SL" { $script:X++; return [string][char]0xFF0F }
    }
    $script:X++
    if ($script:X % 2 -eq 1) { return $open } else { return $close }
}

$done = New-Object System.Collections.Generic.List[string]

foreach ($f in Get-ChildItem $charDir -Filter "*.persona.json") {
    $name = $f.BaseName
    $raw  = [System.IO.File]::ReadAllText($f.FullName, [System.Text.UTF8Encoding]::new($false))
    $orig = $raw
    $lines = $raw -split "`n"
    $mode = 0

    for ($k = 0; $k -lt $lines.Count; $k++) {
        $line = $lines[$k]
        if ($line -match '^\s*"(selfClaimRules|selfClaimExamples|realSelfBehaviors)"\s*:\s*\[') {
            $mode = 1
            if ($line -match 'selfClaimExamples') { $mode = 2 }
            if ($line -match 'realSelfBehaviors') { $mode = 3 }
            $script:X = 0
            continue
        }
        if ($mode -ne 0 -and $line -match '^\s*\]') { $mode = 0; continue }
        if ($mode -eq 0) { continue }

        $new = $line

        if ($new.Contains('\"')) {
            $new = [regex]::Replace($new, '(?<!\\)\\"', { Convert-Pair "DQ" })
        }
        if ($new.Contains('\u0027')) {
            $new = [regex]::Replace($new, '\\u0027', { Convert-Pair "SQ" })
        }
        if ($mode -eq 3 -and $new.Contains("'")) {
            $new = [regex]::Replace($new, "'", { Convert-Pair "SQ" })
        }
        if ($mode -eq 2 -and $new.Contains('/')) {
            $new = $new.Replace('/', [string][char]0xFF0F)
        }

        if ($new -ne $line) { $lines[$k] = $new }
    }

    $newFull = [string]::Join("`n", $lines)
    if ($newFull -ne $orig) {
        [System.IO.File]::WriteAllText($f.FullName, $newFull, [System.Text.UTF8Encoding]::new($false))
        $done.Add($name)
        Write-Host "converted: $name"
    }
}
Write-Host ("done converted = " + $done.Count + " / " + (Get-ChildItem $charDir -Filter "*.persona.json").Count)