$ErrorActionPreference = 'Continue'
$out = Join-Path $env:TEMP 'awake_p1_01_result.txt'
$lines = @()

$testExe = 'D:\AWAKE-Dev\AWAKE.Tests\bin\Debug\net472\Awake.SdkSmoke.exe'
$lines += 'exe exists : ' + (Test-Path $testExe)
if (Test-Path $testExe) { $lines += 'exe mtime  : ' + (Get-Item $testExe).LastWriteTime }

try {
    $assembly = [Reflection.Assembly]::LoadFrom($testExe)
    $flags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'

    try {
        $logType = $assembly.GetType('Awake.AwakeLog')
        if ($logType) {
            $prop = $logType.GetProperty('Enabled', $flags)
            if ($prop) { $prop.SetValue($null, $false, $null); $lines += 'log muted  : True' }
            else { $lines += 'log muted  : property not found (skipped)' }
        }
    } catch { $lines += 'log mute failed (ignored): ' + $_.Exception.Message }

    $type = $assembly.GetType('Awake.AwakeFileStorageService+JsonFileKeyValueStore', $true)
    $lines += 'store type : ' + $type.FullName

    $ctor = $type.GetConstructor($flags, $null, [type[]]@([string], [string]), $null)
    $lines += 'ctor found : ' + ($null -ne $ctor)

    # parent path is an existing plain FILE => directory creation must fail
    $target = $testExe + '\impossible.json'
    $store = $ctor.Invoke(@('audit', $target))

    $t1 = $type.GetMethod('SetAsync').Invoke($store, @('key', 'not-on-disk', $null, [Threading.CancellationToken]::None))
    $r1 = $t1.GetAwaiter().GetResult()
    $lines += 'WRITE_SUCCESS        = ' + $r1.IsSuccess

    $t2 = $type.GetMethod('GetAsync').Invoke($store, @('key', $null, [Threading.CancellationToken]::None))
    $lines += 'READ_SAME_INSTANCE   = [' + $t2.GetAwaiter().GetResult().Value + ']'

    $reopened = $ctor.Invoke(@('audit', $target))
    $t3 = $type.GetMethod('GetAsync').Invoke($reopened, @('key', $null, [Threading.CancellationToken]::None))
    $lines += 'READ_REOPENED        = [' + $t3.GetAwaiter().GetResult().Value + ']'

    # control: a writable target in TEMP must really persist
    try {
        $okDir = Join-Path $env:TEMP 'awake_p1_01_ok'
        New-Item -ItemType Directory -Path $okDir -Force | Out-Null
        $okTarget = Join-Path $okDir 'ok.json'
        if (Test-Path $okTarget) { Remove-Item $okTarget -Force }
        $lines += 'control target : ' + $okTarget

        $setM = $type.GetMethod('SetAsync')
        $getM = $type.GetMethod('GetAsync')
        $lines += 'control methods: set=' + ($null -ne $setM) + ' get=' + ($null -ne $getM)

        $store2 = $ctor.Invoke([object[]]@([string]'audit', [string]$okTarget))
        $lines += 'control store  : ' + ($null -ne $store2) + ' / ' + $store2.GetType().Name

        $t4 = $setM.Invoke($store2, [object[]]@([string]'key', [string]'on-disk', $null, [Threading.CancellationToken]::None))
        $lines += 'CONTROL_WRITE_SUCCESS= ' + $t4.GetAwaiter().GetResult().IsSuccess
        $lines += 'control file exists  = ' + (Test-Path $okTarget)

        $re2 = $ctor.Invoke([object[]]@([string]'audit', [string]$okTarget))
        $t5 = $getM.Invoke($re2, [object[]]@([string]'key', $null, [Threading.CancellationToken]::None))
        $lines += 'CONTROL_REOPENED     = [' + $t5.GetAwaiter().GetResult().Value + ']'
    } catch {
        $lines += 'CONTROL EXCEPTION: ' + $_.Exception.GetType().Name + ' :: ' + $_.Exception.Message
        if ($_.Exception.InnerException) { $lines += 'CONTROL INNER: ' + $_.Exception.InnerException.Message }
    }
}
catch {
    $lines += 'EXCEPTION: ' + $_.Exception.GetType().Name + ' :: ' + $_.Exception.Message
}

$lines | Out-File -Encoding utf8 $out
'DONE -> ' + $out
