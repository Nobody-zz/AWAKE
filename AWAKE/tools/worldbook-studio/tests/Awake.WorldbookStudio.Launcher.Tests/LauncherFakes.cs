using Awake.WorldbookStudio.Core;
using Awake.WorldbookStudio.Launcher;

namespace Awake.WorldbookStudio.Launcher.Tests;

internal sealed class FakeNativeApi : IWebProcessNativeApi
{
    private readonly object _gate = new();
    private readonly Dictionary<IntPtr, string> _kinds = new();
    private long _nextHandle = 100;
    private NativeHandle? _process;

    public bool AssignResult { get; set; } = true;
    public bool ResumeResult { get; set; } = true;
    public bool TerminateJobResult { get; set; } = true;
    public bool TerminateProcessResult { get; set; } = true;
    public TaskCompletionSource? BootstrapGate { get; set; }
    public bool ProcessExited { get; private set; }
    public int ProcessId { get; private set; }
    public int CreatedHandles { get; private set; }
    public int DisposedHandles { get; private set; }
    public List<string> Calls { get; } = new();
    public int OutstandingHandles => CreatedHandles - DisposedHandles;

    public NativeHandle CreateJob() => CreateHandle("job");

    public void CreateBootstrapPipe(out NativeHandle read, out NativeHandle write)
    {
        read = CreateHandle("read");
        write = CreateHandle("write");
    }

    public NativeProcessLaunch CreateWebProcess(string webExe, Dictionary<string, string?> environment, NativeHandle bootstrapRead)
    {
        _process = CreateHandle("process");
        var thread = CreateHandle("thread");
        ProcessId = 30000 + CreatedHandles;
        Calls.Add("create-process");
        return new NativeProcessLaunch(_process, thread, ProcessId);
    }

    public async Task WriteBootstrapAsync(NativeHandle pipe, byte[] frame, CancellationToken cancellationToken)
    {
        Calls.Add("write-bootstrap");
        if (BootstrapGate is not null) await BootstrapGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public bool AssignProcessToJobObject(NativeHandle job, NativeHandle process)
    {
        Calls.Add("assign-job");
        return AssignResult;
    }

    public uint ResumeThread(NativeHandle thread)
    {
        Calls.Add("resume");
        return ResumeResult ? 1u : uint.MaxValue;
    }

    public uint WaitForSingleObject(NativeHandle handle, int milliseconds)
    {
        lock (_gate)
        {
            if (_process is not null && ReferenceEquals(handle, _process)) return ProcessExited ? 0u : 258u;
            return 0u;
        }
    }

    public bool GetExitCodeProcess(NativeHandle process, out uint exitCode)
    {
        exitCode = ProcessExited ? 0u : 259u;
        return true;
    }

    public bool TerminateJobObject(NativeHandle job, uint exitCode)
    {
        Calls.Add("terminate-job");
        if (TerminateJobResult) ProcessExited = true;
        return TerminateJobResult;
    }

    public bool TerminateProcess(NativeHandle process, uint exitCode)
    {
        Calls.Add("terminate-process");
        if (TerminateProcessResult) ProcessExited = true;
        return TerminateProcessResult;
    }

    private NativeHandle CreateHandle(string kind)
    {
        lock (_gate)
        {
            var id = new IntPtr(_nextHandle++);
            _kinds[id] = kind;
            CreatedHandles++;
            return new TrackedHandle(id, () =>
            {
                lock (_gate)
                {
                    if (_kinds.Remove(id)) DisposedHandles++;
                }
            });
        }
    }

    private sealed class TrackedHandle : NativeHandle
    {
        private readonly Action _released;

        public TrackedHandle(IntPtr handle, Action released) : base(handle, true) => _released = released;

        protected override bool ReleaseHandle()
        {
            _released();
            return true;
        }
    }
}

internal sealed class FakeHealthProbe : IWebProcessHealthProbe
{
    public WebProcessHost? Host { get; set; }
    public bool BlockHealth { get; set; }
    public bool ReturnInvalidHealth { get; set; }
    public int ShutdownStatusCode { get; set; } = 200;
    public int HealthCalls { get; private set; }

    public async Task<StudioHealth?> GetHealthAsync(CancellationToken cancellationToken)
    {
        HealthCalls++;
        if (BlockHealth) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        if (ReturnInvalidHealth) return new StudioHealth(false, "fake", 0, "fake", "fake", "fake", 0);
        var host = Host ?? throw new InvalidOperationException("FakeHealthProbe 未绑定 Host。");
        return new StudioHealth(
            true,
            StudioRuntimeConstants.Product,
            StudioRuntimeConstants.ProtocolVersion,
            host.InstanceId,
            host.LaunchNonceHashForTests,
            host.WorkspaceHashForTests,
            StudioRuntimeConstants.ResolvePort());
    }

    public Task<int> SendShutdownAsync(string instanceId, string proof, CancellationToken cancellationToken)
        => Task.FromResult(ShutdownStatusCode);

}

internal sealed class FakeLauncherEnvironment : ILauncherEnvironment
{
    public bool IsSmoke { get; init; }
    public Dictionary<string, string?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int ExitCode { get; private set; }

    public string? Get(string name) => Values.TryGetValue(name, out var value) ? value : null;
    public void SetExitCode(int exitCode) => ExitCode = exitCode;
}

internal sealed class FakeBrowserOpener : IBrowserOpener
{
    public bool Result { get; set; } = true;
    public int Calls { get; private set; }

    public bool Open(string address)
    {
        Calls++;
        return Result;
    }
}

internal sealed class FakeLauncherHostFactory : IWebProcessHostFactory
{
    public FakeLauncherHost Host { get; } = new();

    public IWebProcessHost Create(string packageRoot, string workspaceRoot, LauncherLog log) => Host;
}

internal sealed class FakeLauncherHost : IWebProcessHost
{
    public bool HoldStart { get; set; }
    public int StartCalls { get; private set; }
    public int StopCalls { get; private set; }
    public bool ExitedObserved { get; private set; }
    public string? ObservedExitCode => ExitedObserved ? "0x00000000 (0)" : null;
    public string Address => $"http://127.0.0.1:{StudioRuntimeConstants.ResolvePort()}/";
    public int ProcessId => 41001;
    public string? LastCleanupCode { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        StartCalls++;
        if (HoldStart) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopCalls++;
        ExitedObserved = true;
        return Task.CompletedTask;
    }

    public void Dispose() => ExitedObserved = true;
}
