using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.Launcher;

internal interface IWebProcessNativeApi
{
    NativeHandle CreateJob();
    void CreateBootstrapPipe(out NativeHandle read, out NativeHandle write);
    NativeProcessLaunch CreateWebProcess(string webExe, Dictionary<string, string?> environment, NativeHandle bootstrapRead);
    Task WriteBootstrapAsync(NativeHandle pipe, byte[] frame, CancellationToken cancellationToken);
    bool AssignProcessToJobObject(NativeHandle job, NativeHandle process);
    uint ResumeThread(NativeHandle thread);
    uint WaitForSingleObject(NativeHandle handle, int milliseconds);
    bool GetExitCodeProcess(NativeHandle process, out uint exitCode);
    bool TerminateJobObject(NativeHandle job, uint exitCode);
    bool TerminateProcess(NativeHandle process, uint exitCode);
}

internal interface IWebProcessHealthProbe
{
    Task<StudioHealth?> GetHealthAsync(CancellationToken cancellationToken);
    Task<int> SendShutdownAsync(string instanceId, string proof, CancellationToken cancellationToken);
}

internal sealed record NativeProcessLaunch(NativeHandle Process, NativeHandle Thread, int ProcessId);

internal sealed record LauncherTiming(
    TimeSpan HealthTimeout,
    TimeSpan HealthPollInterval,
    TimeSpan GracefulPhase,
    TimeSpan FinalCleanupPhase,
    TimeSpan JobFallbackPhase)
{
    public static LauncherTiming Default { get; } = new(
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(2000),
        TimeSpan.FromMilliseconds(500));
}

internal interface IWebProcessHost : IDisposable
{
    string Address { get; }
    int ProcessId { get; }
    bool ExitedObserved { get; }
    string? ObservedExitCode { get; }
    string? LastCleanupCode { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

internal interface IWebProcessHostFactory
{
    IWebProcessHost Create(string packageRoot, string workspaceRoot, LauncherLog log);
}

internal interface ILauncherEnvironment
{
    bool IsSmoke { get; }
    string? Get(string name);
    void SetExitCode(int exitCode);
}

internal sealed class ProductionWebProcessHostFactory : IWebProcessHostFactory
{
    public IWebProcessHost Create(string packageRoot, string workspaceRoot, LauncherLog log)
        => new WebProcessHost(packageRoot, workspaceRoot, log);
}

internal sealed class SystemLauncherEnvironment : ILauncherEnvironment
{
    public bool IsSmoke => string.Equals(Get("AWAKE_WB_SMOKE"), "1", StringComparison.Ordinal);
    public string? Get(string name) => Environment.GetEnvironmentVariable(name);
    public void SetExitCode(int exitCode) => Environment.ExitCode = exitCode;
}

public sealed class WebProcessHost : IWebProcessHost, IDisposable
{
    private const uint CreateSuspended = 0x00000004;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint HandleInherit = 0x00000001;
    private const uint JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;
    private const uint WaitObject0 = 0;
    private const uint WaitTimeout = 258;
    private const int ErrorInsufficientBuffer = 122;
    private static readonly IntPtr ProcThreadAttributeHandleList = new(0x00020002);

    private readonly string _packageRoot;
    private readonly string _workspaceRoot;
    private readonly LauncherLog _log;
    private readonly HttpClient _http;
    private readonly int _port;
    private readonly IWebProcessNativeApi _native;
    private readonly IWebProcessHealthProbe _health;
    private readonly LauncherTiming _timing;
    private readonly object _lifecycleLock = new();
    private NativeHandle? _job;
    private NativeHandle? _process;
    private NativeHandle? _thread;
    private NativeHandle? _bootstrapRead;
    private NativeHandle? _bootstrapWrite;
    private byte[]? _launchNonce;
    private LifecycleState _state = LifecycleState.Created;
    private Task? _startTask;
    private Task? _stopTask;
    private Task? _finalCleanupTask;
    private CancellationTokenSource? _internalStartCts;
    private CancellationTokenSource? _internalGracefulCts;
    private bool _jobAssigned;
    private bool _started;

    private enum LifecycleState
    {
        Created,
        Starting,
        Running,
        Stopping,
        Stopped,
        Disposed
    }

    public WebProcessHost(string packageRoot, string workspaceRoot, LauncherLog log)
        : this(packageRoot, workspaceRoot, log, null, null, null)
    {
    }

    internal WebProcessHost(
        string packageRoot,
        string workspaceRoot,
        LauncherLog log,
        IWebProcessNativeApi? native,
        IWebProcessHealthProbe? health,
        LauncherTiming? timing)
    {
        _packageRoot = StudioRuntimeHashing.NormalizePathForIdentity(packageRoot);
        _workspaceRoot = StudioRuntimeHashing.NormalizePathForIdentity(workspaceRoot);
        _log = log;
        _port = StudioRuntimeConstants.ResolvePort();
        _http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}/"), Timeout = TimeSpan.FromMilliseconds(500) };
        _native = native ?? new WindowsWebProcessNativeApi(this);
        _health = health ?? new HttpWebProcessHealthProbe(_http);
        _timing = timing ?? LauncherTiming.Default;
    }

    public string InstanceId { get; private set; } = string.Empty;
    public string Address => $"http://127.0.0.1:{_port}/";
    public int ProcessId { get; private set; }
    public bool ExitedObserved { get; private set; }
    public string? ObservedExitCode { get; private set; }
    public string? LastCleanupCode { get; private set; }
    internal string LifecycleStateForTests => GetLifecycleStateName();
    internal Task? StartTaskForTests => _startTask;
    internal Task? StopTaskForTests => _stopTask;
    internal Task? FinalCleanupTaskForTests => _finalCleanupTask;
    internal bool HandlesReleasedForTests => _job is null && _process is null && _thread is null && _bootstrapRead is null && _bootstrapWrite is null;
    internal string LaunchNonceHashForTests => _launchNonce is null
        ? string.Empty
        : StudioRuntimeHashing.Sha256Prefix(Convert.ToHexString(_launchNonce).ToLowerInvariant());
    internal string WorkspaceHashForTests => StudioRuntimeHashing.WorkspaceHash(_workspaceRoot);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Task task;
        lock (_lifecycleLock)
        {
            task = _state switch
            {
                LifecycleState.Created => StartFromCreatedLocked(),
                LifecycleState.Starting or LifecycleState.Running => _startTask ?? Task.CompletedTask,
                LifecycleState.Stopping or LifecycleState.Stopped => throw new InvalidOperationException("WB-LAUNCHER-STATE-409: WebProcessHost 已停止或正在停止。"),
                LifecycleState.Disposed => throw new ObjectDisposedException(nameof(WebProcessHost)),
                _ => throw new InvalidOperationException("WB-LAUNCHER-STATE-409: WebProcessHost 状态无效。")
            };
        }

        return WaitForCallerAsync(task, cancellationToken);
    }

    private Task StartFromCreatedLocked()
    {
        _state = LifecycleState.Starting;
        _internalStartCts = new CancellationTokenSource();
        _startTask = StartCoreAsync(_internalStartCts.Token);
        return _startTask;
    }

    private async Task StartCoreAsync(CancellationToken internalToken)
    {
        try
        {
            if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("WB-LAUNCHER-PLATFORM: 首批发行包只支持 Windows x64。");
            var webExe = Path.Combine(_packageRoot, "web", "Awake.WorldbookStudio.Web.exe");
            var schemaRoot = Path.Combine(_packageRoot, "schemas");
            if (!File.Exists(webExe) || !File.Exists(Path.Combine(schemaRoot, "awake.worldbook.authoring.v1.schema.json")))
                throw new InvalidOperationException("WB-PACKAGE-001: 发行包缺少 Web 或规范文件。");

            InstanceId = Guid.NewGuid().ToString("N");
            _launchNonce = RandomNumberGenerator.GetBytes(32);
            _job = _native.CreateJob();
            _native.CreateBootstrapPipe(out var bootstrapRead, out var bootstrapWrite);
            _bootstrapRead = bootstrapRead;
            _bootstrapWrite = bootstrapWrite;

            var environment = BuildEnvironment(bootstrapRead);
            var launch = _native.CreateWebProcess(webExe, environment, _bootstrapRead);
            _process = launch.Process;
            _thread = launch.Thread;
            ProcessId = launch.ProcessId;
            _bootstrapRead.Dispose();
            _bootstrapRead = null;
            if (!_native.AssignProcessToJobObject(_job!, _process!))
                throw new InvalidOperationException("WB-JOB-001: 无法接管 Web 子进程。");
            _jobAssigned = true;

            var bootstrap = new StudioBootstrap(
                StudioRuntimeConstants.ProtocolVersion,
                InstanceId,
                Convert.ToHexString(_launchNonce).ToLowerInvariant(),
                _workspaceRoot,
                schemaRoot,
                _packageRoot);
            await WriteBootstrapAsync(bootstrap, internalToken).ConfigureAwait(false);
            if (_native.ResumeThread(_thread!) == uint.MaxValue)
                throw new InvalidOperationException("WB-PROCESS-001: 无法启动 Web 子进程。");
            _started = true;
            _log.Write("WB-WEB-STARTED", "launch", ProcessId, valueHash: LauncherLog.HashValue(InstanceId));
            await WaitForHealthAsync(internalToken).ConfigureAwait(false);
            lock (_lifecycleLock)
            {
                if (_state != LifecycleState.Starting) throw new OperationCanceledException(internalToken);
                _state = LifecycleState.Running;
            }
        }
        catch (OperationCanceledException) when (internalToken.IsCancellationRequested)
        {
            await EnsureFinalCleanupAsync().ConfigureAwait(false);
            throw LauncherFailure.Create("WB-START-CANCELLED", "startup", "Web 启动已取消。");
        }
        catch (Exception exception)
        {
            await EnsureFinalCleanupAsync().ConfigureAwait(false);
            throw LauncherFailure.From(exception, "startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Task task;
        lock (_lifecycleLock)
        {
            switch (_state)
            {
                case LifecycleState.Created:
                    _state = LifecycleState.Stopped;
                    _stopTask = Task.CompletedTask;
                    task = _stopTask;
                    break;
                case LifecycleState.Starting:
                    _state = LifecycleState.Stopping;
                    _internalStartCts?.Cancel();
                    _stopTask ??= StopAfterStartAsync(_startTask ?? Task.CompletedTask);
                    task = _stopTask;
                    break;
                case LifecycleState.Running:
                    _state = LifecycleState.Stopping;
                    _stopTask ??= EnsureFinalCleanupAsync();
                    task = _stopTask;
                    break;
                case LifecycleState.Stopping:
                    task = _stopTask ?? EnsureFinalCleanupAsync();
                    break;
                case LifecycleState.Stopped:
                case LifecycleState.Disposed:
                    task = Task.CompletedTask;
                    break;
                default:
                    throw new InvalidOperationException("WB-LAUNCHER-STATE-409: WebProcessHost 状态无效。");
            }
        }

        return WaitForCallerAsync(task, cancellationToken);
    }

    private async Task StopAfterStartAsync(Task startTask)
    {
        try { await startTask.ConfigureAwait(false); } catch { }
        await EnsureFinalCleanupAsync().ConfigureAwait(false);
    }

    private Task EnsureFinalCleanupAsync()
    {
        lock (_lifecycleLock)
        {
            _finalCleanupTask ??= FinalCleanupCoreAsync();
            return _finalCleanupTask;
        }
    }

    private async Task FinalCleanupCoreAsync()
    {
        var cleanupStarted = DateTime.UtcNow;
        var gracefulDeadline = cleanupStarted + _timing.GracefulPhase;
        var finalDeadline = gracefulDeadline + _timing.FinalCleanupPhase;
        var evidenceDeadline = finalDeadline + _timing.JobFallbackPhase;
        try
        {
            if (_started && _launchNonce is not null)
                await TrySendShutdownAsync(gracefulDeadline).ConfigureAwait(false);
            await WaitForExitUntilAsync(gracefulDeadline).ConfigureAwait(false);

            if (!HasExited())
            {
                if (_jobAssigned && _job is not null && !_job.IsInvalid)
                {
                    if (!_native.TerminateJobObject(_job, 1))
                    {
                        _log.Write("WB-CLEANUP-JOB-TERMINATE-FAILED", "cleanup", ProcessId);
                        if (_process is not null && !_process.IsInvalid) _native.TerminateProcess(_process, 1);
                    }
                }
                else if (_process is not null && !_process.IsInvalid)
                {
                    _native.TerminateProcess(_process, 1);
                }
                await WaitForExitUntilAsync(finalDeadline).ConfigureAwait(false);
            }

            if (!HasExited() && DateTime.UtcNow < evidenceDeadline && _job is not null && !_job.IsInvalid)
            {
                _job.Dispose();
                _job = null;
                _jobAssigned = false;
                _log.Write("WB-CLEANUP-JOB-FALLBACK", "cleanup", ProcessId);
                await WaitForExitUntilAsync(evidenceDeadline).ConfigureAwait(false);
            }

            CaptureExitSnapshot();
            if (!ExitedObserved) LastCleanupCode = "WB-CLEANUP-408";
            else _log.Write("WB-WEB-STOPPED", "shutdown", ProcessId);
        }
        finally
        {
            DisposeHandles();
            lock (_lifecycleLock) _state = LifecycleState.Stopped;
        }
    }

    private async Task TrySendShutdownAsync(DateTime gracefulDeadline)
    {
        using var gracefulCts = new CancellationTokenSource();
        var remaining = gracefulDeadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) return;
            gracefulCts.CancelAfter(remaining);
            _internalGracefulCts = gracefulCts;
            try
            {
                var proof = Convert.ToHexString(HMACSHA256.HashData(_launchNonce!, Encoding.UTF8.GetBytes("shutdown:v1:" + InstanceId))).ToLowerInvariant();
            var statusCode = await _health.SendShutdownAsync(InstanceId, proof, gracefulCts.Token).ConfigureAwait(false);
            _log.Write(statusCode is >= 200 and < 300 ? "WB-SHUTDOWN-ACCEPTED" : "WB-SHUTDOWN-REJECTED", "shutdown", ProcessId, exitCode: statusCode);
        }
        catch (OperationCanceledException) { _log.Write("WB-SHUTDOWN-TRANSPORT", "shutdown", ProcessId); }
        catch (HttpRequestException) { _log.Write("WB-SHUTDOWN-TRANSPORT", "shutdown", ProcessId); }
        finally { _internalGracefulCts = null; }
    }

    private async Task WaitForExitUntilAsync(DateTime deadline)
    {
        while (!HasExited())
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) return;
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, Math.Min(25, remaining.TotalMilliseconds)))).ConfigureAwait(false);
        }
    }

    private void CaptureExitSnapshot()
    {
        ExitedObserved = _process is null || _process.IsInvalid || _native.WaitForSingleObject(_process, 0) == WaitObject0;
        ObservedExitCode = GetExitCode();
    }

    private static async Task WaitForCallerAsync(Task task, CancellationToken cancellationToken)
    {
        await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public bool HasExited()
        => _process is null || _process.IsInvalid
            ? ProcessId == 0 || ExitedObserved
            : _native.WaitForSingleObject(_process, 0) == WaitObject0;

    public string? GetExitCode()
    {
        if (_process is null || _process.IsInvalid) return ObservedExitCode;
        if (!_native.GetExitCodeProcess(_process, out var code)) return null;
        return $"0x{code:X8} ({code})";
    }

    private async Task WaitForHealthAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timing.HealthTimeout);
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            if (HasExited()) throw new InvalidOperationException($"WB-WEB-EXIT-001: Web 启动失败，退出码 {GetExitCode()}。");
            try
            {
                var health = await _health.GetHealthAsync(timeout.Token).ConfigureAwait(false);
                if (health is not null
                    && health.Ok
                    && string.Equals(health.Product, StudioRuntimeConstants.Product, StringComparison.Ordinal)
                    && health.ProtocolVersion == StudioRuntimeConstants.ProtocolVersion
                    && health.Port == _port
                    && string.Equals(health.InstanceId, InstanceId, StringComparison.Ordinal)
                    && string.Equals(health.LaunchNonceHash, StudioRuntimeHashing.Sha256Prefix(Convert.ToHexString(_launchNonce!).ToLowerInvariant()), StringComparison.Ordinal)
                    && string.Equals(health.WorkspaceIdHash, StudioRuntimeHashing.WorkspaceHash(_workspaceRoot), StringComparison.Ordinal))
                {
                    _log.Write("WB-HEALTH-OK", "health", ProcessId, valueHash: LauncherLog.HashValue(InstanceId));
                    return;
                }
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested) { throw new InvalidOperationException("WB-HEALTH-408: Web 健康检查超时。"); }
            catch (OperationCanceledException) { }
            catch (HttpRequestException) { }
            catch (JsonException) { }
            try
            {
                await Task.Delay(_timing.HealthPollInterval, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException("WB-HEALTH-408: Web 健康检查超时。");
            }
        }
    }

    private async Task WriteBootstrapAsync(StudioBootstrap bootstrap, CancellationToken cancellationToken)
    {
        var pipe = _bootstrapWrite ?? throw new InvalidOperationException("WB-BOOTSTRAP-001: 管道未创建。");
        _bootstrapWrite = null;
        var frame = StudioBootstrapCodec.EncodeFrame(bootstrap);
        try
        {
            await _native.WriteBootstrapAsync(pipe, frame, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(frame);
            pipe.Dispose();
        }
    }

    private Dictionary<string, string?> BuildEnvironment(NativeHandle bootstrapRead)
    {
        var environment = Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(item => (string)item.Key, item => item.Value?.ToString(), StringComparer.OrdinalIgnoreCase);
        environment[StudioRuntimeConstants.WorkspaceEnvironment] = _workspaceRoot;
        environment[StudioRuntimeConstants.SchemaEnvironment] = Path.Combine(_packageRoot, "schemas");
        environment[StudioRuntimeConstants.PackageEnvironment] = _packageRoot;
        environment[StudioRuntimeConstants.PortEnvironment] = _port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        environment[StudioRuntimeConstants.InstanceEnvironment] = InstanceId;
        environment[StudioRuntimeConstants.BootstrapHandleEnvironment] = ((ulong)bootstrapRead.DangerousGetHandle().ToInt64()).ToString(System.Globalization.CultureInfo.InvariantCulture);
        environment.Remove("WORLD_BOOK_WORKSPACE");
        environment.Remove("WORLD_BOOK_SCHEMA_ROOT");
        environment.Remove("AWAKE_WB_DEV_MODE");
        environment.Remove("DOTNET_ROOT");
        environment.Remove("DOTNET_ROOT(x86)");
        return environment;
    }

    private void CreateWebProcess(string webExe, Dictionary<string, string?> environment, NativeHandle bootstrapRead, out NativeHandle process, out NativeHandle thread)
    {
        var envBlock = BuildEnvironmentBlock(environment);
        var envPointer = Marshal.StringToHGlobalUni(envBlock);
        var attributeSize = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeSize);
        var attributes = Marshal.AllocHGlobal(attributeSize);
        var handleList = new[] { bootstrapRead.DangerousGetHandle() };
        var pinned = GCHandle.Alloc(handleList, GCHandleType.Pinned);
        try
        {
            if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref attributeSize)) throw new InvalidOperationException("WB-PROCESS-002: 无法初始化启动属性。");
            if (!UpdateProcThreadAttribute(attributes, 0, ProcThreadAttributeHandleList, pinned.AddrOfPinnedObject(), (IntPtr)(IntPtr.Size), IntPtr.Zero, IntPtr.Zero))
                throw new InvalidOperationException("WB-PROCESS-003: 无法限制继承句柄。");
            var startup = new STARTUPINFOEX { StartupInfo = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFOEX>() }, lpAttributeList = attributes };
            var commandLine = new StringBuilder();
            if (!CreateProcess(webExe, commandLine, IntPtr.Zero, IntPtr.Zero, true, CreateSuspended | ExtendedStartupInfoPresent | CreateUnicodeEnvironment, envPointer, Path.GetDirectoryName(webExe), ref startup, out var info))
                throw new InvalidOperationException("WB-PROCESS-004: 无法创建 Web 进程。");
            process = new NativeHandle(info.hProcess, true);
            thread = new NativeHandle(info.hThread, true);
            _processId = info.dwProcessId;
        }
        finally
        {
            if (pinned.IsAllocated) pinned.Free();
            DeleteProcThreadAttributeList(attributes);
            Marshal.FreeHGlobal(attributes);
            Marshal.FreeHGlobal(envPointer);
        }
    }

    private static string BuildEnvironmentBlock(Dictionary<string, string?> values)
        => string.Join('\0', values.Where(pair => pair.Value is not null).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => pair.Key + "=" + pair.Value)) + "\0\0";

    private static NativeHandle CreateJob()
    {
        var job = new NativeHandle(CreateJobObject(IntPtr.Zero, null), true);
        if (job.IsInvalid) throw new InvalidOperationException("WB-JOB-002: 无法创建 Job Object。");
        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION { BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION { LimitFlags = JobObjectLimitKillOnJobClose } };
        if (!SetInformationJobObject(job.DangerousGetHandle(), JobObjectExtendedLimitInformation, ref info, Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()))
        {
            job.Dispose();
            throw new InvalidOperationException("WB-JOB-003: 无法设置 Job Object 回收策略。");
        }
        return job;
    }

    private static void CreateBootstrapPipe(out NativeHandle read, out NativeHandle write)
    {
        var security = new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(), bInheritHandle = 1 };
        if (!CreatePipe(out var readHandle, out var writeHandle, ref security, 0)) throw new InvalidOperationException("WB-BOOTSTRAP-005: 无法创建启动管道。");
        read = new NativeHandle(readHandle, true);
        write = new NativeHandle(writeHandle, true);
        if (!SetHandleInformation(write.DangerousGetHandle(), HandleInherit, 0))
        {
            read.Dispose();
            write.Dispose();
            throw new InvalidOperationException("WB-BOOTSTRAP-005: 无法限制管道继承。");
        }
    }

    private void DisposeHandles()
    {
        _thread?.Dispose();
        _thread = null;
        _process?.Dispose();
        _process = null;
        _bootstrapWrite?.Dispose();
        _bootstrapWrite = null;
        _bootstrapRead?.Dispose();
        _bootstrapRead = null;
        _job?.Dispose();
        _job = null;
        _jobAssigned = false;
        if (_launchNonce is not null) CryptographicOperations.ZeroMemory(_launchNonce);
        _launchNonce = null;
        _internalStartCts?.Dispose();
        _internalStartCts = null;
        _internalGracefulCts?.Dispose();
        _internalGracefulCts = null;
    }

    private string GetLifecycleStateName()
    {
        lock (_lifecycleLock) return _state.ToString();
    }

    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_state == LifecycleState.Disposed) return;
        }

        try { StopAsync(CancellationToken.None).GetAwaiter().GetResult(); }
        finally
        {
            _http.Dispose();
            lock (_lifecycleLock) _state = LifecycleState.Disposed;
        }
    }

    private sealed class WindowsWebProcessNativeApi : IWebProcessNativeApi
    {
        private readonly WebProcessHost _host;

        public WindowsWebProcessNativeApi(WebProcessHost host) => _host = host;

        public NativeHandle CreateJob() => WebProcessHost.CreateJob();

        public void CreateBootstrapPipe(out NativeHandle read, out NativeHandle write)
            => WebProcessHost.CreateBootstrapPipe(out read, out write);

        public NativeProcessLaunch CreateWebProcess(string webExe, Dictionary<string, string?> environment, NativeHandle bootstrapRead)
        {
            _host.CreateWebProcess(webExe, environment, bootstrapRead, out var process, out var thread);
            return new NativeProcessLaunch(process, thread, checked((int)_host._processId));
        }

        public async Task WriteBootstrapAsync(NativeHandle pipe, byte[] frame, CancellationToken cancellationToken)
        {
            using var safeFile = new SafeFileHandle(pipe.DangerousGetHandle(), ownsHandle: false);
            using var stream = new FileStream(safeFile, FileAccess.Write, 4096, isAsync: false);
            await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        public bool AssignProcessToJobObject(NativeHandle job, NativeHandle process)
            => WebProcessHost.AssignProcessToJobObject(job.DangerousGetHandle(), process.DangerousGetHandle());

        public uint ResumeThread(NativeHandle thread)
            => WebProcessHost.ResumeThread(thread.DangerousGetHandle());

        public uint WaitForSingleObject(NativeHandle handle, int milliseconds)
            => WebProcessHost.WaitForSingleObject(handle.DangerousGetHandle(), milliseconds);

        public bool GetExitCodeProcess(NativeHandle process, out uint exitCode)
            => WebProcessHost.GetExitCodeProcess(process.DangerousGetHandle(), out exitCode);

        public bool TerminateJobObject(NativeHandle job, uint exitCode)
            => WebProcessHost.TerminateJobObject(job.DangerousGetHandle(), exitCode);

        public bool TerminateProcess(NativeHandle process, uint exitCode)
            => WebProcessHost.TerminateProcess(process.DangerousGetHandle(), exitCode);
    }

    private sealed class HttpWebProcessHealthProbe : IWebProcessHealthProbe
    {
        private readonly HttpClient _http;

        public HttpWebProcessHealthProbe(HttpClient http) => _http = http;

        public Task<StudioHealth?> GetHealthAsync(CancellationToken cancellationToken)
            => _http.GetFromJsonAsync<StudioHealth>("health", cancellationToken: cancellationToken);

        public async Task<int> SendShutdownAsync(string instanceId, string proof, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/launcher/shutdown");
            request.Headers.Add("X-AWAKE-Instance-Id", instanceId);
            request.Headers.Add("X-AWAKE-Shutdown-Proof", proof);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return (int)response.StatusCode;
        }
    }

    private uint _processId;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(string? applicationName, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint creationFlags, IntPtr environment, string? currentDirectory, ref STARTUPINFOEX startupInfo, out PROCESS_INFORMATION processInformation);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(IntPtr job, uint infoClass, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION info, int length);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateJobObject(IntPtr job, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(IntPtr process, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr handle, int milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CreatePipe(out IntPtr readPipe, out IntPtr writePipe, ref SECURITY_ATTRIBUTES securityAttributes, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool InitializeProcThreadAttributeList(IntPtr attributeList, int attributeCount, int flags, ref IntPtr size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool UpdateProcThreadAttribute(IntPtr attributeList, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previousValue, IntPtr returnSize);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern void DeleteProcThreadAttributeList(IntPtr attributeList);

    [StructLayout(LayoutKind.Sequential)] private struct SECURITY_ATTRIBUTES { public int nLength; public IntPtr lpSecurityDescriptor; public int bInheritHandle; }
    [StructLayout(LayoutKind.Sequential)] private struct PROCESS_INFORMATION { public IntPtr hProcess; public IntPtr hThread; public uint dwProcessId; public uint dwThreadId; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct STARTUPINFO { public int cb; public string? lpReserved; public string? lpDesktop; public string? lpTitle; public int dwX; public int dwY; public int dwXSize; public int dwYSize; public int dwXCountChars; public int dwYCountChars; public int dwFillAttribute; public int dwFlags; public short wShowWindow; public short cbReserved2; public IntPtr lpReserved2; public IntPtr hStdInput; public IntPtr hStdOutput; public IntPtr hStdError; }
    [StructLayout(LayoutKind.Sequential)] private struct STARTUPINFOEX { public STARTUPINFO StartupInfo; public IntPtr lpAttributeList; }
    [StructLayout(LayoutKind.Sequential)] private struct JOBOBJECT_BASIC_LIMIT_INFORMATION { public long PerProcessUserTimeLimit; public long PerJobUserTimeLimit; public uint LimitFlags; public UIntPtr MinimumWorkingSetSize; public UIntPtr MaximumWorkingSetSize; public uint ActiveProcessLimit; public UIntPtr Affinity; public uint PriorityClass; public uint SchedulingClass; }
    [StructLayout(LayoutKind.Sequential)] private struct IO_COUNTERS { public ulong ReadOperationCount; public ulong WriteOperationCount; public ulong OtherOperationCount; public ulong ReadTransferCount; public ulong WriteTransferCount; public ulong OtherTransferCount; }
    [StructLayout(LayoutKind.Sequential)] private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION { public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation; public IO_COUNTERS IoInfo; public UIntPtr ProcessMemoryLimit; public UIntPtr JobMemoryLimit; public UIntPtr PeakProcessMemoryUsed; public UIntPtr PeakJobMemoryUsed; }
}

public class NativeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public NativeHandle(IntPtr handle, bool ownsHandle) : base(ownsHandle) => SetHandle(handle);
    protected override bool ReleaseHandle() => CloseHandle(handle);

    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
}
