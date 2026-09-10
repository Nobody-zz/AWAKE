param(
    [string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
)

$ErrorActionPreference = 'Stop'

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot)
$configPath = Join-Path $ProjectRoot 'src\AwakeConfig.cs'
Assert-True (Test-Path -LiteralPath $configPath -PathType Leaf) ('Missing source file: ' + $configPath)

$source = [IO.File]::ReadAllText($configPath)
$assignment = [regex]::Match($source, '(?m)^\s*OpenDeveloperReport\s*=\s*(?<binding>[^;]+);')
Assert-True $assignment.Success 'Could not locate the production OpenDeveloperReport constructor binding.'

$binding = $assignment.Groups['binding'].Value.Trim()
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('awake-developer-report-binding-' + [Guid]::NewGuid().ToString('N'))
$sourceFile = Join-Path $tempRoot 'BindingHarness.cs'
$executable = Join-Path $tempRoot 'BindingHarness.exe'
$compiler = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe'

New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
try {
    $harness = @"
using System;

internal static class AwakeMcmActions
{
    internal static Action ShowDeveloperReport = () => { };
}

internal sealed class AwakeConfig
{
    internal Action OpenDeveloperReport { get; set; }

    internal AwakeConfig()
    {
        OpenDeveloperReport = $binding;
    }
}

internal static class Program
{
    internal static int Main()
    {
        string observed = string.Empty;
        AwakeMcmActions.ShowDeveloperReport = () => observed = "old";
        AwakeConfig config = new AwakeConfig();
        AwakeMcmActions.ShowDeveloperReport = () => observed = "new";
        config.OpenDeveloperReport();
        if (!string.Equals(observed, "new", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Expected the existing config instance to call the replacement delegate; observed=" + observed);
            return 1;
        }
        return 0;
    }
}
"@
    [IO.File]::WriteAllText($sourceFile, $harness, (New-Object Text.UTF8Encoding($false)))
    Assert-True (Test-Path -LiteralPath $compiler -PathType Leaf) ('Missing C# compiler: ' + $compiler)

    $compileOutput = @(& $compiler /nologo /target:exe "/out:$executable" $sourceFile 2>&1)
    $compileExitCode = $LASTEXITCODE
    Assert-True ($compileExitCode -eq 0) ('Binding harness compilation failed: ' + ($compileOutput -join [Environment]::NewLine))

    $runOutput = @(& $executable 2>&1)
    $runExitCode = $LASTEXITCODE
    Assert-True ($runExitCode -eq 0) ('Late-binding regression failed: ' + ($runOutput -join [Environment]::NewLine))
    Write-Output 'PASS AwakeConfig developer report late-binding regression'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
