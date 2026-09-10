using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Windows.Forms;
using Awake.WorldbookStudio.Core;
using Awake.WorldbookStudio.Launcher;
using Awake.WorldbookStudio.Launcher.Tests;

var failures = new List<string>();
Run("workspace_missing_saved_path_recovers_to_default", TestMissingSavedWorkspace);
Run("workspace_corrupt_settings_is_not_rewritten", TestCorruptSettings);
Run("workspace_corrupt_marker_is_not_rewritten", TestCorruptMarker);
Run("web_host_rejects_repeated_start_without_losing_first_pid", TestHostRejectsRepeatedStart);
Run("web_host_stop_wait_cancellation_does_not_skip_cleanup", TestStopWaitCancellation);
Run("fake_host_reuses_lifecycle_tasks_and_releases_resources", TestFakeHostLifecycleTasks);
Run("fake_host_start_cancellation_reaches_final_cleanup", TestFakeHostStartCancellation);
Run("fake_host_assign_failure_releases_resources", TestFakeHostAssignFailure);
Run("fake_host_resume_failure_releases_resources", TestFakeHostResumeFailure);
Run("fake_host_terminate_job_failure_uses_process_fallback", TestFakeHostTerminateFallback);
Run("fake_host_health_timeout_is_classified", TestFakeHostHealthTimeout);
Run("fake_host_cleanup_timeout_is_not_success", TestFakeHostCleanupTimeout);
Run("form_closing_during_start_has_one_cleanup", TestFormClosingDuringStart);

if (failures.Count > 0)
{
    Console.Error.WriteLine($"FAIL: {failures.Count} launcher test(s)");
    foreach (var failure in failures) Console.Error.WriteLine(failure);
    Environment.ExitCode = 1;
}
else
{
    Console.WriteLine("PASS: launcher tests");
}

void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"PASS: {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{name}: {exception.Message}");
    }
}

void TestMissingSavedWorkspace()
{
    var root = Path.Combine(Path.GetTempPath(), "awake-worldbook-launcher-test-" + Guid.NewGuid().ToString("N"));
    var localAppData = Path.Combine(root, "LocalAppData");
    var settingsDirectory = Path.Combine(localAppData, "AWAKE", "WorldbookStudio");
    var packageRoot = Path.Combine(root, "Package");
    var schemaRoot = Path.Combine(packageRoot, "schemas");
    var savedWorkspace = Path.Combine(root, "DeletedWorkspace");
    var documents = Path.Combine(root, "Documents");
    var defaultWorkspace = Path.Combine(documents, "AWAKE", "WorldbookStudio");
    var settingsPath = Path.Combine(settingsDirectory, "settings.json");
    var settingsJson = JsonSerializer.Serialize(new { Version = 1, Workspace = savedWorkspace, Language = "zh-CN" });
    Directory.CreateDirectory(settingsDirectory);
    Directory.CreateDirectory(schemaRoot);
    File.WriteAllText(settingsPath, settingsJson, new UTF8Encoding(false));
    var settingsBefore = File.ReadAllBytes(settingsPath);
    var previousLocalAppData = Environment.GetEnvironmentVariable("AWAKE_WB_SMOKE_LOCALAPPDATA");
    var previousDocuments = Environment.GetEnvironmentVariable("AWAKE_WB_SMOKE_DOCUMENTS");

    try
    {
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_LOCALAPPDATA", localAppData);
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_DOCUMENTS", documents);
        var manager = new WorkspaceManager(new LauncherLog());
        var selection = manager.Resolve(null!, packageRoot, schemaRoot, smoke: true);
        Assert(Path.GetFullPath(selection.Root).Equals(Path.GetFullPath(defaultWorkspace), StringComparison.OrdinalIgnoreCase), "失效工作区没有回退到默认工作区。");
        Assert(!Directory.Exists(savedWorkspace), "保存的缺失工作区被自动创建。");
        Assert(File.Exists(Path.Combine(defaultWorkspace, ".awake-worldbook-workspace.json")), "默认工作区没有创建有效 marker。");
        var settingsAfter = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(settingsPath));
        Assert(settingsAfter.GetProperty("Workspace").GetString() == defaultWorkspace, "settings 没有切换到默认工作区。");
        var backups = Directory.GetFiles(settingsDirectory, "settings.json.recovery-*.bak");
        Assert(backups.Length == 1, "失效 settings 没有生成唯一恢复备份。");
        Assert(settingsBefore.SequenceEqual(File.ReadAllBytes(backups[0])), "恢复备份没有保留原始 settings。");
    }
    finally
    {
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_LOCALAPPDATA", previousLocalAppData);
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_DOCUMENTS", previousDocuments);
        try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); } catch { }
    }
}

void TestHostRejectsRepeatedStart()
{
    var packageRoot = Environment.GetEnvironmentVariable("AWAKE_WB_TEST_PACKAGE");
    if (string.IsNullOrWhiteSpace(packageRoot)) throw new InvalidOperationException("AWAKE_WB_TEST_PACKAGE 未设置。");
    packageRoot = Path.GetFullPath(packageRoot);
    var root = Path.Combine(Path.GetTempPath(), "awake-worldbook-host-test-" + Guid.NewGuid().ToString("N"));
    var workspace = Path.Combine(root, "Workspace");
    Directory.CreateDirectory(workspace);
    var firstProcessId = 0;

    try
    {
        using var host = new WebProcessHost(packageRoot, workspace, new LauncherLog());
        host.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        firstProcessId = host.ProcessId;
        host.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(host.ProcessId == firstProcessId, "重复 StartAsync 改写了首个 Web PID。");
        host.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(!IsProcessAlive(firstProcessId), "StopAsync 后首个 Web PID 仍存在。");
    }
    finally
    {
        TryKill(firstProcessId);
        try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); } catch { }
    }
}

void TestFakeHostLifecycleTasks()
{
    var fixture = CreateFakeFixture();
    try
    {
        var native = new FakeNativeApi();
        var health = new FakeHealthProbe();
        using var host = new WebProcessHost(fixture.PackageRoot, fixture.Workspace, new LauncherLog(), native, health, FastTiming());
        health.Host = host;
        host.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        var startTask = host.StartTaskForTests ?? throw new InvalidOperationException("缺少 start task。");
        host.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(ReferenceEquals(startTask, host.StartTaskForTests), "重复 StartAsync 没有复用同一内部 task。");
        Assert(native.Calls.Count(call => call == "create-process") == 1, "重复 StartAsync 创建了第二个 fake Web 进程。");

        host.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        var stopTask = host.StopTaskForTests ?? throw new InvalidOperationException("缺少 stop task。");
        host.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(ReferenceEquals(stopTask, host.StopTaskForTests), "重复 StopAsync 没有复用同一内部 task。");
        Assert(host.LifecycleStateForTests == "Stopped", "Host 没有进入 Stopped。");
        Assert(host.HandlesReleasedForTests && native.OutstandingHandles == 0, "正常 Stop 后仍有 fake 资源未释放。");
    }
    finally
    {
        DeleteFixture(fixture.Root);
    }
}

void TestFakeHostStartCancellation()
{
    var fixture = CreateFakeFixture();
    try
    {
        var native = new FakeNativeApi();
        var health = new FakeHealthProbe { BlockHealth = true };
        using var host = new WebProcessHost(fixture.PackageRoot, fixture.Workspace, new LauncherLog(), native, health, FastTiming());
        health.Host = host;
        var startTask = host.StartAsync(CancellationToken.None);
        Assert(SpinWait.SpinUntil(() => health.HealthCalls > 0, 2000), "fake health 没有进入等待阶段。");
        host.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        try { startTask.GetAwaiter().GetResult(); } catch { }
        Assert(host.LifecycleStateForTests == "Stopped", "启动取消后 Host 没有进入 Stopped。");
        Assert(native.ProcessExited, "启动取消没有触发 fake 进程回收。");
        Assert(host.HandlesReleasedForTests && native.OutstandingHandles == 0, "启动取消后仍有 fake 资源未释放。");
    }
    finally
    {
        DeleteFixture(fixture.Root);
    }
}

void TestFakeHostAssignFailure()
{
    var fixture = CreateFakeFixture();
    try
    {
        var native = new FakeNativeApi { AssignResult = false };
        var health = new FakeHealthProbe();
        using var host = new WebProcessHost(fixture.PackageRoot, fixture.Workspace, new LauncherLog(), native, health, FastTiming());
        health.Host = host;
        var exception = AssertThrows<InvalidOperationException>(() => host.StartAsync(CancellationToken.None).GetAwaiter().GetResult());
        AssertStartsWith(exception.Message, "WB-JOB-001:");
        Assert(!native.Calls.Contains("resume"), "Job 接管失败后错误地 Resume 了 Web 线程。");
        Assert(native.ProcessExited && native.OutstandingHandles == 0, "Job 接管失败后 fake 资源未回收。");
        Assert(host.LifecycleStateForTests == "Stopped", "Job 接管失败后 Host 没有进入 Stopped。");
    }
    finally
    {
        DeleteFixture(fixture.Root);
    }
}

void TestFakeHostResumeFailure()
{
    var fixture = CreateFakeFixture();
    try
    {
        var native = new FakeNativeApi { ResumeResult = false };
        var health = new FakeHealthProbe();
        using var host = new WebProcessHost(fixture.PackageRoot, fixture.Workspace, new LauncherLog(), native, health, FastTiming());
        health.Host = host;
        var exception = AssertThrows<InvalidOperationException>(() => host.StartAsync(CancellationToken.None).GetAwaiter().GetResult());
        AssertStartsWith(exception.Message, "WB-PROCESS-001:");
        Assert(native.Calls.Contains("terminate-job"), "Resume 失败后没有走 Job 回收。");
        Assert(native.ProcessExited && native.OutstandingHandles == 0, "Resume 失败后 fake 资源未回收。");
    }
    finally
    {
        DeleteFixture(fixture.Root);
    }
}

void TestFakeHostTerminateFallback()
{
    var fixture = CreateFakeFixture();
    try
    {
        var native = new FakeNativeApi { TerminateJobResult = false, TerminateProcessResult = true };
        var health = new FakeHealthProbe();
        using var host = new WebProcessHost(fixture.PackageRoot, fixture.Workspace, new LauncherLog(), native, health, FastTiming());
        health.Host = host;
        host.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        host.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(native.Calls.Contains("terminate-job") && native.Calls.Contains("terminate-process"), "TerminateJobObject 失败后没有立即走进程 fallback。");
        Assert(host.ExitedObserved && host.LastCleanupCode is null, "fallback 退出没有留下成功退出证据。");
        Assert(native.OutstandingHandles == 0, "fallback 后仍有 fake 资源未释放。");
    }
    finally
    {
        DeleteFixture(fixture.Root);
    }
}

void TestFakeHostHealthTimeout()
{
    var fixture = CreateFakeFixture();
    try
    {
        var native = new FakeNativeApi();
        var health = new FakeHealthProbe { ReturnInvalidHealth = true };
        using var host = new WebProcessHost(fixture.PackageRoot, fixture.Workspace, new LauncherLog(), native, health, FastTiming());
        health.Host = host;
        var exception = AssertThrows<InvalidOperationException>(() => host.StartAsync(CancellationToken.None).GetAwaiter().GetResult());
        AssertStartsWith(exception.Message, "WB-HEALTH-408:");
        Assert(health.HealthCalls > 0, "health timeout 没有执行探测。");
        Assert(native.ProcessExited && native.OutstandingHandles == 0, "health timeout 后 fake 资源未回收。");
    }
    finally
    {
        DeleteFixture(fixture.Root);
    }
}

void TestFakeHostCleanupTimeout()
{
    var fixture = CreateFakeFixture();
    try
    {
        var native = new FakeNativeApi { TerminateJobResult = false, TerminateProcessResult = false };
        var health = new FakeHealthProbe();
        using var host = new WebProcessHost(fixture.PackageRoot, fixture.Workspace, new LauncherLog(), native, health, FastTiming());
        health.Host = host;
        host.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        host.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(!host.ExitedObserved, "进程仍存活时错误报告 ExitedObserved=true。");
        Assert(host.LastCleanupCode == "WB-CLEANUP-408", "最终回收超时没有固定为 WB-CLEANUP-408。");
        Assert(host.HandlesReleasedForTests && native.OutstandingHandles == 0, "最终回收超时后仍有 fake 资源未释放。");
    }
    finally
    {
        DeleteFixture(fixture.Root);
    }
}

void TestCorruptSettings()
{
    var root = Path.Combine(Path.GetTempPath(), "awake-worldbook-corrupt-settings-test-" + Guid.NewGuid().ToString("N"));
    var localAppData = Path.Combine(root, "LocalAppData");
    var settingsDirectory = Path.Combine(localAppData, "AWAKE", "WorldbookStudio");
    var packageRoot = Path.Combine(root, "Package");
    var schemaRoot = Path.Combine(packageRoot, "schemas");
    var settingsPath = Path.Combine(settingsDirectory, "settings.json");
    var previousLocalAppData = Environment.GetEnvironmentVariable("AWAKE_WB_SMOKE_LOCALAPPDATA");
    var previousSmokeWorkspace = Environment.GetEnvironmentVariable("AWAKE_WB_SMOKE_WORKSPACE");
    var previousDocuments = Environment.GetEnvironmentVariable("AWAKE_WB_SMOKE_DOCUMENTS");

    try
    {
        Directory.CreateDirectory(settingsDirectory);
        Directory.CreateDirectory(schemaRoot);
        File.WriteAllText(settingsPath, "{not-json", new UTF8Encoding(false));
        var settingsBefore = File.ReadAllBytes(settingsPath);
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_LOCALAPPDATA", localAppData);
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_WORKSPACE", null);
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_DOCUMENTS", Path.Combine(root, "Documents"));
        var manager = new WorkspaceManager(new LauncherLog());
        var exception = AssertThrows<InvalidOperationException>(() => manager.Resolve(null!, packageRoot, schemaRoot, smoke: true));
        AssertStartsWith(exception.Message, "WB-SETTINGS-RECOVER-422:");
        Assert(settingsBefore.SequenceEqual(File.ReadAllBytes(settingsPath)), "损坏 settings 被自动重写。");
    }
    finally
    {
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_LOCALAPPDATA", previousLocalAppData);
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_WORKSPACE", previousSmokeWorkspace);
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_DOCUMENTS", previousDocuments);
        try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); } catch { }
    }
}

void TestCorruptMarker()
{
    var root = Path.Combine(Path.GetTempPath(), "awake-worldbook-corrupt-marker-test-" + Guid.NewGuid().ToString("N"));
    var localAppData = Path.Combine(root, "LocalAppData");
    var settingsDirectory = Path.Combine(localAppData, "AWAKE", "WorldbookStudio");
    var packageRoot = Path.Combine(root, "Package");
    var schemaRoot = Path.Combine(packageRoot, "schemas");
    var workspace = Path.Combine(root, "Workspace");
    var markerPath = Path.Combine(workspace, ".awake-worldbook-workspace.json");
    var settingsPath = Path.Combine(settingsDirectory, "settings.json");
    var previousLocalAppData = Environment.GetEnvironmentVariable("AWAKE_WB_SMOKE_LOCALAPPDATA");
    var previousSmokeWorkspace = Environment.GetEnvironmentVariable("AWAKE_WB_SMOKE_WORKSPACE");
    var previousDocuments = Environment.GetEnvironmentVariable("AWAKE_WB_SMOKE_DOCUMENTS");

    try
    {
        Directory.CreateDirectory(settingsDirectory);
        Directory.CreateDirectory(schemaRoot);
        Directory.CreateDirectory(workspace);
        File.WriteAllText(markerPath, "{\"Version\":99,\"Product\":\"wrong\"}", new UTF8Encoding(false));
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(new { Version = 1, Workspace = workspace, Language = "zh-CN" }), new UTF8Encoding(false));
        var settingsBefore = File.ReadAllBytes(settingsPath);
        var markerBefore = File.ReadAllBytes(markerPath);
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_LOCALAPPDATA", localAppData);
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_WORKSPACE", null);
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_DOCUMENTS", Path.Combine(root, "Documents"));
        var manager = new WorkspaceManager(new LauncherLog());
        var exception = AssertThrows<InvalidOperationException>(() => manager.Resolve(null!, packageRoot, schemaRoot, smoke: true));
        AssertStartsWith(exception.Message, "WB-SETTINGS-RECOVER-422:");
        Assert(settingsBefore.SequenceEqual(File.ReadAllBytes(settingsPath)), "marker 损坏时 settings 被重写。");
        Assert(markerBefore.SequenceEqual(File.ReadAllBytes(markerPath)), "损坏 marker 被自动修复或覆盖。");
    }
    finally
    {
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_LOCALAPPDATA", previousLocalAppData);
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_WORKSPACE", previousSmokeWorkspace);
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_DOCUMENTS", previousDocuments);
        try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); } catch { }
    }
}

void TestStopWaitCancellation()
{
    var packageRoot = Environment.GetEnvironmentVariable("AWAKE_WB_TEST_PACKAGE");
    if (string.IsNullOrWhiteSpace(packageRoot)) throw new InvalidOperationException("AWAKE_WB_TEST_PACKAGE 未设置。");
    var root = Path.Combine(Path.GetTempPath(), "awake-worldbook-stop-test-" + Guid.NewGuid().ToString("N"));
    var workspace = Path.Combine(root, "Workspace");
    Directory.CreateDirectory(workspace);
    var processId = 0;

    try
    {
        using var host = new WebProcessHost(packageRoot, workspace, new LauncherLog());
        host.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        processId = host.ProcessId;
        using var callerCancellation = new CancellationTokenSource();
        callerCancellation.Cancel();
        AssertThrows<OperationCanceledException>(() => host.StopAsync(callerCancellation.Token).GetAwaiter().GetResult());
        host.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(host.ExitedObserved, "Stop cleanup 没有记录真实退出观察值。");
        Assert(!IsProcessAlive(processId), "caller token 取消后 Web PID 仍存在。");
    }
    finally
    {
        TryKill(processId);
        try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); } catch { }
    }
}

void TestFormClosingDuringStart()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try { TestFormClosingDuringStartSta(); }
        catch (Exception exception) { failure = exception; }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    Assert(thread.Join(5000), "STA FormClosing 测试超时。");
    if (failure is not null) throw failure;
}

void TestFormClosingDuringStartSta()
{
    var root = Path.Combine(Path.GetTempPath(), "awake-worldbook-form-test-" + Guid.NewGuid().ToString("N"));
    var workspace = Path.Combine(root, "Workspace");
    var localAppData = Path.Combine(root, "LocalAppData");
    var resultPath = Path.Combine(root, "smoke-result.json");
    var previous = new Dictionary<string, string?>
    {
        ["AWAKE_WB_SMOKE_WORKSPACE"] = Environment.GetEnvironmentVariable("AWAKE_WB_SMOKE_WORKSPACE"),
        ["AWAKE_WB_SMOKE_LOCALAPPDATA"] = Environment.GetEnvironmentVariable("AWAKE_WB_SMOKE_LOCALAPPDATA"),
        ["AWAKE_WB_SMOKE_RESULT"] = Environment.GetEnvironmentVariable("AWAKE_WB_SMOKE_RESULT"),
        ["AWAKE_WB_SMOKE_BROWSER"] = Environment.GetEnvironmentVariable("AWAKE_WB_SMOKE_BROWSER")
    };
    try
    {
        Directory.CreateDirectory(workspace);
        var environment = new FakeLauncherEnvironment { IsSmoke = true };
        environment.Values["AWAKE_WB_SMOKE_WORKSPACE"] = workspace;
        environment.Values["AWAKE_WB_SMOKE_LOCALAPPDATA"] = localAppData;
        environment.Values["AWAKE_WB_SMOKE_RESULT"] = resultPath;
        environment.Values["AWAKE_WB_SMOKE_BROWSER"] = "success";
        environment.Values[StudioRuntimeConstants.MutexEnvironment] = $"Local\\AWAKE.WorldbookStudio.FormTests.{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_WORKSPACE", workspace);
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_LOCALAPPDATA", localAppData);
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_RESULT", resultPath);
        Environment.SetEnvironmentVariable("AWAKE_WB_SMOKE_BROWSER", "success");

        var browser = new FakeBrowserOpener();
        var factory = new FakeLauncherHostFactory();
        factory.Host.HoldStart = true;
        using var form = new LauncherForm(browser, factory, environment);
        var canceledEvents = 0;
        form.FormClosing += (_, args) => { if (args.Cancel) canceledEvents++; };
        form.Show();
        var shown = typeof(LauncherForm).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(method => method.Name == "OnShown" && method.GetParameters().Length == 2);
        shown.Invoke(form, new object?[] { form, EventArgs.Empty });
        Assert(SpinWait.SpinUntil(() => factory.Host.StartCalls > 0, 2000), "Form 没有启动 Host。");
        form.Close();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (!form.IsDisposed && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }

        Assert(form.IsDisposed, "FormClosing 清理完成后没有关闭窗口。");
        Assert(canceledEvents == 1, "FormClosing 没有严格执行一次暂缓关闭。");
        Assert(factory.Host.StopCalls == 1, "FormClosing 重复触发了 Host cleanup。");
    }
    finally
    {
        foreach (var entry in previous) Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        DeleteFixture(root);
    }
}

(string Root, string PackageRoot, string Workspace) CreateFakeFixture()
{
    var root = Path.Combine(Path.GetTempPath(), "awake-worldbook-fake-host-" + Guid.NewGuid().ToString("N"));
    var packageRoot = Path.Combine(root, "Package");
    var workspace = Path.Combine(root, "Workspace");
    Directory.CreateDirectory(Path.Combine(packageRoot, "web"));
    Directory.CreateDirectory(Path.Combine(packageRoot, "schemas"));
    Directory.CreateDirectory(workspace);
    File.WriteAllText(Path.Combine(packageRoot, "web", "Awake.WorldbookStudio.Web.exe"), "fake");
    File.WriteAllText(Path.Combine(packageRoot, "schemas", "awake.worldbook.authoring.v1.schema.json"), "{}");
    return (root, packageRoot, workspace);
}

LauncherTiming FastTiming()
    => new(
        TimeSpan.FromMilliseconds(80),
        TimeSpan.FromMilliseconds(2),
        TimeSpan.FromMilliseconds(10),
        TimeSpan.FromMilliseconds(20),
        TimeSpan.FromMilliseconds(10));

static void DeleteFixture(string root)
{
    try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); } catch { }
}

TException AssertThrows<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException exception)
    {
        return exception;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}, but no exception was thrown.");
}

static void AssertStartsWith(string value, string prefix)
{
    if (!value.StartsWith(prefix, StringComparison.Ordinal))
        throw new InvalidOperationException($"Expected '{prefix}', actual '{value}'.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static bool IsProcessAlive(int processId)
{
    if (processId <= 0) return false;
    try
    {
        using var process = Process.GetProcessById(processId);
        return !process.HasExited;
    }
    catch (ArgumentException) { return false; }
    catch (InvalidOperationException) { return false; }
}

static void TryKill(int processId)
{
    if (processId <= 0) return;
    try
    {
        using var process = Process.GetProcessById(processId);
        if (!process.HasExited) process.Kill(entireProcessTree: true);
        process.WaitForExit(2000);
    }
    catch (ArgumentException) { }
    catch (InvalidOperationException) { }
}
