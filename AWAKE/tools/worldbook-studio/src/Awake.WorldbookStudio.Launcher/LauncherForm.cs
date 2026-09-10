using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.Launcher;

public interface IBrowserOpener
{
    bool Open(string address);
}

public sealed class ShellBrowserOpener : IBrowserOpener
{
    public bool Open(string address)
    {
        return Process.Start(new ProcessStartInfo(address) { UseShellExecute = true }) is not null;
    }
}

public sealed class LauncherForm : Form
{
    private readonly Label _status = new();
    private readonly Button _open = new();
    private readonly Button _copy = new();
    private readonly Button _exit = new();
    private readonly LauncherLog _log = new();
    private readonly bool _smoke;
    private readonly IBrowserOpener _browser;
    private readonly IWebProcessHostFactory _hostFactory;
    private readonly ILauncherEnvironment _environment;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly object _cleanupGate = new();
    private Mutex? _mutex;
    private IWebProcessHost? _host;
    private Task? _startupTask;
    private Task? _cleanupTask;
    private string _address = string.Empty;
    private string _workspace = string.Empty;
    private bool _ownsMutex;
    private bool _cleanupCompleted;
    private bool _allowCloseAfterCleanup;
    private bool _closeRequested;
    private bool _smokeOk;
    private string _smokeCode = "WB-START-001";
    private bool _smokeBrowserOpened;
    private bool _smokeResultWritten;

    public LauncherForm()
        : this(new SystemLauncherEnvironment())
    {
    }

    private LauncherForm(SystemLauncherEnvironment environment)
        : this(environment.IsSmoke ? new SmokeBrowserOpener(environment) : new ShellBrowserOpener(), new ProductionWebProcessHostFactory(), environment)
    {
    }

    internal LauncherForm(IBrowserOpener browser, IWebProcessHostFactory hostFactory, ILauncherEnvironment environment)
    {
        _smoke = environment.IsSmoke;
        _browser = browser;
        _hostFactory = hostFactory;
        _environment = environment;
        Text = "AWAKE Worldbook Studio";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(620, 220);
        MinimumSize = new Size(620, 220);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        _status.AutoSize = false;
        _status.Dock = DockStyle.Top;
        _status.Height = 100;
        _status.Padding = new Padding(20, 18, 20, 8);
        _status.Text = "正在准备 Worldbook Studio…";
        _status.Font = new Font(Font.FontFamily, 11f);
        _open.Text = "打开编辑器";
        _copy.Text = "复制地址";
        _exit.Text = "退出";
        _open.Enabled = false;
        _copy.Enabled = false;
        _open.Width = 120;
        _copy.Width = 100;
        _exit.Width = 80;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(20, 10, 20, 10), FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.Add(_open);
        buttons.Controls.Add(_copy);
        buttons.Controls.Add(_exit);
        Controls.Add(buttons);
        Controls.Add(_status);
        _open.Click += (_, _) => OpenBrowser();
        _copy.Click += (_, _) => CopyAddress();
        _exit.Click += (_, _) => Close();
        Shown += OnShown;
        FormClosing += OnFormClosing;
        if (_smoke)
        {
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
        }
    }

    private async void OnShown(object? sender, EventArgs e)
    {
        if (_startupTask is not null) return;
        _startupTask = StartAsync();
        try { await _startupTask.ConfigureAwait(true); } catch { }
        if (_smoke && _smokeOk && string.Equals(_environment.Get("AWAKE_WB_SMOKE_HOLD"), "1", StringComparison.Ordinal))
        {
            WriteSmokeReady();
            try { await WaitForSmokeReleaseAsync(_environment.Get("AWAKE_WB_SMOKE_RELEASE"), _lifetimeCts.Token).ConfigureAwait(true); } catch (OperationCanceledException) { }
        }
        if (_smoke) await RequestCleanupAndCloseAsync().ConfigureAwait(true);
    }

    private async Task StartAsync()
    {
        try
        {
            if (_closeRequested) return;
            var packageRoot = StudioRuntimeHashing.NormalizePathForIdentity(AppContext.BaseDirectory);
            var mutexName = _environment.Get(StudioRuntimeConstants.MutexEnvironment);
            _mutex = new Mutex(true, string.IsNullOrWhiteSpace(mutexName) ? StudioRuntimeHashing.PackageMutexName(packageRoot) : mutexName, out var createdNew);
            _ownsMutex = createdNew;
            if (!createdNew)
            {
                _log.Write("WB-INSTANCE-409", "mutex");
                SetSmokeOutcome(false, "WB-INSTANCE-409", false);
                if (!_smoke) MessageBox.Show(this, "Worldbook Studio 已经在运行。请使用已有窗口。", "已在运行", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var schemaRoot = Path.Combine(packageRoot, "schemas");
            var manager = new WorkspaceManager(_log);
            var selection = manager.Resolve(this, packageRoot, schemaRoot, _smoke);
            _workspace = selection.Root;
            _host = _hostFactory.Create(packageRoot, _workspace, _log);
            _status.Text = "正在启动本机编辑器…";
            await _host.StartAsync(_lifetimeCts.Token);
            _address = _host.Address;
            _status.Text = $"编辑器已启动。\n工作区：{_workspace}\n\n浏览器地址：{_address}";
            _open.Enabled = true;
            _copy.Enabled = true;
            if (_closeRequested) return;
            var browserOpened = TryOpenBrowser();
            if (_smoke)
            {
                SetSmokeOutcome(true, browserOpened ? "WB-SMOKE-PASS" : "WB-BROWSER-OPEN-FAILED", browserOpened);
            }
            else if (!browserOpened)
            {
                MessageBox.Show(this, $"浏览器没有自动打开。\n\n请复制下面地址并在浏览器中打开：\n{_address}", "需要手动打开", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (OperationCanceledException)
        {
            _log.Write("WB-START-CANCELLED", "startup");
            SetSmokeOutcome(false, "WB-START-CANCELLED", false);
            if (!_closeRequested && !_smoke) _status.Text = "启动已取消。";
        }
        catch (Exception ex)
        {
            var failure = LauncherFailure.From(ex, "startup");
            _log.Write(failure.Code, failure.Stage, _host?.ProcessId);
            SetSmokeOutcome(false, failure.Code, false);
            _status.Text = DescribeError(failure.Code);
            if (!_smoke && !_closeRequested)
            {
                MessageBox.Show(this, _status.Text, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void SetSmokeOutcome(bool ok, string code, bool browserOpened)
    {
        if (!_smoke) return;
        _smokeOk = ok;
        _smokeCode = code;
        _smokeBrowserOpened = browserOpened;
    }

    private bool TryOpenBrowser()
    {
        try
        {
            var opened = _browser.Open(_address);
            _log.Write(opened ? "WB-BROWSER-OPENED" : "WB-BROWSER-OPEN-FAILED", "browser");
            return opened;
        }
        catch
        {
            _log.Write("WB-BROWSER-OPEN-FAILED", "browser");
            return false;
        }
    }

    private void WriteSmokeReady()
    {
        var path = _environment.Get("AWAKE_WB_SMOKE_READY");
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                launcherProcessId = Environment.ProcessId,
                webProcessId = _host?.ProcessId ?? 0,
                address = _address,
                workspaceHash = LauncherLog.HashValue(_workspace)
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception exception)
        {
            _log.Write("WB-SMOKE-READY-500", "smoke", _host?.ProcessId, valueHash: LauncherLog.HashValue(exception.GetType().FullName));
            SetSmokeOutcome(false, "WB-SMOKE-READY-500", false);
        }
    }

    private static async Task WaitForSmokeReleaseAsync(string? releasePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(releasePath)) return;
        while (!File.Exists(releasePath)) await Task.Delay(25, cancellationToken).ConfigureAwait(true);
    }

    private void OpenBrowser()
    {
        if (!TryOpenBrowser()) MessageBox.Show(this, $"浏览器没有自动打开。\n\n请复制地址：\n{_address}", "需要手动打开", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void CopyAddress()
    {
        if (string.IsNullOrWhiteSpace(_address)) return;
        Clipboard.SetText(_address);
        _status.Text = "地址已复制到剪贴板。";
    }

    private async Task RequestCleanupAndCloseAsync()
    {
        try { await CleanupAndCloseAsync().ConfigureAwait(true); }
        catch (Exception exception) { _log.Write("WB-CLEANUP-500", "cleanup", _host?.ProcessId, valueHash: LauncherLog.HashValue(exception.GetType().FullName)); }
        if (IsDisposed || Disposing) return;
        if (_allowCloseAfterCleanup) return;
        _allowCloseAfterCleanup = true;
        try
        {
            if (IsHandleCreated) BeginInvoke(new Action(Close));
            else Close();
        }
        catch (InvalidOperationException) { }
    }

    private Task CleanupAndCloseAsync()
    {
        lock (_cleanupGate) return _cleanupTask ??= CleanupCoreAsync();
    }

    private async Task CleanupCoreAsync()
    {
        _lifetimeCts.Cancel();
        try
        {
            if (_host is not null)
            {
                try { await _host.StopAsync(CancellationToken.None).ConfigureAwait(false); }
                catch (Exception exception) { _log.Write("WB-CLEANUP-500", "cleanup", _host.ProcessId, valueHash: LauncherLog.HashValue(exception.GetType().FullName)); }
                try { _host.Dispose(); }
                catch (Exception exception) { _log.Write("WB-CLEANUP-500", "dispose", _host.ProcessId, valueHash: LauncherLog.HashValue(exception.GetType().FullName)); }
                if (!_host.ExitedObserved)
                {
                    _smokeOk = false;
                    _smokeCode = _host.LastCleanupCode ?? "WB-CLEANUP-408";
                }
            }
        }
        finally
        {
            ReleaseMutexIfOwned();
            _cleanupCompleted = true;
            if (_smoke) CompleteSmoke(_smokeOk, _smokeCode);
            _lifetimeCts.Dispose();
        }
    }

    private void ReleaseMutexIfOwned()
    {
        var mutex = _mutex;
        _mutex = null;
        if (mutex is null) return;
        try
        {
            if (_ownsMutex) mutex.ReleaseMutex();
        }
        catch (ApplicationException) { }
        finally
        {
            mutex.Dispose();
            _ownsMutex = false;
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowCloseAfterCleanup || _cleanupCompleted) return;
        e.Cancel = true;
        _closeRequested = true;
        _lifetimeCts.Cancel();
        _ = RequestCleanupAndCloseAsync();
    }

    private void CompleteSmoke(bool ok, string code)
    {
        if (!_smoke || _smokeResultWritten) return;
        _smokeResultWritten = true;
        var path = _environment.Get("AWAKE_WB_SMOKE_RESULT")
            ?? Path.Combine(Path.GetTempPath(), "awake-worldbook-studio-smoke-result.json");
        var exited = _host is null || _host.ExitedObserved;
        var result = new
        {
            ok = ok && exited,
            code,
            browserOpened = _smokeBrowserOpened,
            workspaceHash = LauncherLog.HashValue(_workspace),
            logHash = LauncherLog.HashValue(_log.Path),
            processId = _host?.ProcessId ?? 0,
            exited,
            observedExitCode = _host?.ObservedExitCode,
            webExitSnapshot = new
            {
                pid = _host?.ProcessId ?? 0,
                exitCode = _host?.ObservedExitCode,
                exitedObserved = exited
            }
        };
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
        _environment.SetExitCode(result.ok ? 0 : 1);
    }

    private static string DescribeError(string code)
        => code switch
        {
            "WB-ROOT-001" or "WB-ROOT-002" or "WB-ROOT-005" or "WB-ROOT-006" or "WB-ROOT-007" => "工作区位置不安全，请重新选择不在游戏目录或程序目录中的文件夹。",
            "WB-ROOT-009" => "所选文件夹不是有效的 Worldbook Studio 工作区，请选择空文件夹或已有工作区。",
            "WB-PACKAGE-001" => "发行包文件不完整，请重新解压完整的 Worldbook Studio 压缩包。",
            "WB-HEALTH-408" or "WB-HEALTH-409" => "编辑器启动后没有通过实例校验，请退出其他本地 Studio 后重试。",
            "WB-PORT-409-OTHER" or "WB-PORT-409-NON_STUDIO" => $"本机 {StudioRuntimeConstants.Port} 端口已被其他程序占用。请关闭占用该端口的程序后重试；需要保留该程序时，可设置 {StudioRuntimeConstants.PortEnvironment} 指定其他端口再启动。",
            "WB-WEB-EXIT-001" => $"编辑器进程启动后立即退出。请先确认本机 {StudioRuntimeConstants.Port} 端口没有被其他程序占用（关闭占用程序后重试），仍失败时查看本机日志。",
            "WB-BROWSER-OPEN-FAILED" => "浏览器没有自动打开，可以使用“复制地址”手动打开。",
            _ => "Worldbook Studio 启动失败。请查看本机日志并重新选择工作区。"
        };
}
