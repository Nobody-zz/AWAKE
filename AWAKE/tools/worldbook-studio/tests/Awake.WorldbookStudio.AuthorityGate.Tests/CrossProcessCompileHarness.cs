using System.Diagnostics;
using System.Text;
using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.AuthorityGate.Tests;

internal static class CrossProcessCompileHarness
{
    public static int RunChild(string[] args)
    {
        if (args.Length < 4) return 64;
        var mode = args[0];
        var workspaceRoot = args[1];
        var proofId = args[2];
        var outputRoot = args[3];
        var readyPath = args.Length > 4 ? args[4] : null;
        var releasePath = args.Length > 5 ? args[5] : null;
        try
        {
            var schemaRoot = AuthorityGateTestFixture.FindSchemaRoot();
            var workspace = new WorkspaceService(new WorkspaceOptions(workspaceRoot, schemaRoot));
            var application = new WorldbookApplicationService(workspace);
            var authority = new AuthorityGateService(workspace, application, current =>
            {
                if (mode != "compile-hold" || current != CompileSettlementFaultPoint.AfterReservation || readyPath is null || releasePath is null) return;
                File.WriteAllText(readyPath, "ready", Encoding.UTF8);
                var deadline = DateTime.UtcNow.AddSeconds(20);
                while (!File.Exists(releasePath) && DateTime.UtcNow < deadline) Thread.Sleep(20);
            });
            application.Initialize();
            authority.Initialize();
            if (mode == "initialize")
            {
                Console.WriteLine("INITIALIZED");
                return 0;
            }
            authority.CompileApproved(proofId, outputRoot: outputRoot);
            Console.WriteLine("SUCCESS");
            return 0;
        }
        catch (InvalidOperationException error)
        {
            Console.WriteLine("ERROR|" + error.Message.Split(':', 2)[0]);
            return 2;
        }
        catch (Exception error)
        {
            Console.WriteLine("ERROR|UNEXPECTED|" + error.GetType().Name);
            return 3;
        }
    }

    public static Process Start(string mode, string workspaceRoot, string proofId, string outputRoot, string? readyPath = null, string? releasePath = null)
    {
        var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("test process path unavailable");
        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        if (Path.GetFileName(processPath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            startInfo.ArgumentList.Add(Path.GetFullPath(typeof(CrossProcessCompileHarness).Assembly.Location));
        startInfo.ArgumentList.Add("--compile-child");
        startInfo.ArgumentList.Add(mode);
        startInfo.ArgumentList.Add(workspaceRoot);
        startInfo.ArgumentList.Add(proofId);
        startInfo.ArgumentList.Add(outputRoot);
        if (readyPath is not null) startInfo.ArgumentList.Add(readyPath);
        if (releasePath is not null) startInfo.ArgumentList.Add(releasePath);
        return Process.Start(startInfo) ?? throw new InvalidOperationException("child process failed to start");
    }

    public static string ReadOutput(Process process)
    {
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return stdout + stderr;
    }

    public static void WaitForFile(string path, params Process[] processes)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!File.Exists(path) && DateTime.UtcNow < deadline)
        {
            if (processes.Any(process => process.HasExited))
            {
                var details = string.Join(Environment.NewLine, processes.Where(process => process.HasExited).Select(process => $"exit={process.ExitCode}\n{ReadOutput(process)}"));
                throw new InvalidOperationException("child process exited before synchronization point: " + details);
            }
            Thread.Sleep(20);
        }
        if (!File.Exists(path)) throw new InvalidOperationException("child process did not reach synchronization point");
    }
}
