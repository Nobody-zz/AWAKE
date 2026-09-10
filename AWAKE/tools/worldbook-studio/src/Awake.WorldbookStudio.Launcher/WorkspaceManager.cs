using System.Text;
using System.Text.Json;
using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.Launcher;

public sealed record WorkspaceSelection(string Root, bool Initialized);

public sealed class WorkspaceManager
{
    private const int SettingsVersion = 1;
    private const string MarkerName = ".awake-worldbook-workspace.json";
    private readonly LauncherLog _log;

    private enum SettingsReadKind
    {
        Missing,
        Valid,
        Invalid,
        Unreadable
    }

    private sealed record SettingsReadResult(SettingsReadKind Kind, StudioSettings? Settings, string Code);

    public WorkspaceManager(LauncherLog log) => _log = log;

    public WorkspaceSelection Resolve(IWin32Window owner, string packageRoot, string schemaRoot, bool smoke)
    {
        var smokeRoot = Environment.GetEnvironmentVariable("AWAKE_WB_SMOKE_WORKSPACE");
        if (smoke && !string.IsNullOrWhiteSpace(smokeRoot)) return PrepareNew(smokeRoot, packageRoot, schemaRoot, true);

        var settingsPath = GetSettingsPath();
        var settingsResult = ReadSettings(settingsPath);
        if (settingsResult.Kind == SettingsReadKind.Valid && settingsResult.Settings is not null)
        {
            try { return PrepareExisting(settingsResult.Settings.Workspace, packageRoot, schemaRoot); }
            catch (InvalidOperationException exception)
            {
                var failure = LauncherFailure.From(exception, "workspace-recovery");
                if (failure.Code == "WB-SETTINGS-RECOVER-404")
                    return RecoverMissingSavedWorkspace(settingsPath, settingsResult.Settings, packageRoot, schemaRoot, smoke);
                return RecoverSavedWorkspace(owner, packageRoot, schemaRoot, smoke, failure.Code);
            }
        }

        if (settingsResult.Kind is SettingsReadKind.Invalid or SettingsReadKind.Unreadable)
        {
            return RecoverSavedWorkspace(owner, packageRoot, schemaRoot, smoke, settingsResult.Code);
        }

        var defaultRoot = GetDefaultWorkspaceRoot(smoke);
        if (smoke) return PrepareNew(defaultRoot, packageRoot, schemaRoot, true);

        var choice = MessageBox.Show(owner,
            $"欢迎使用 AWAKE Worldbook Studio。\n\n默认工作区：\n{defaultRoot}\n\n选择“是”使用默认位置，选择“否”自定义工作区。",
            "首次运行设置", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
        if (choice == DialogResult.Cancel) throw new OperationCanceledException();
        return PrepareNew(SelectWorkspace(owner, defaultRoot, choice), packageRoot, schemaRoot, false);
    }

    private WorkspaceSelection RecoverMissingSavedWorkspace(
        string settingsPath,
        StudioSettings savedSettings,
        string packageRoot,
        string schemaRoot,
        bool smoke)
    {
        var defaultRoot = GetDefaultWorkspaceRoot(smoke);
        _log.Write("WB-SETTINGS-RECOVER-404", "workspace-recovery", valueHash: LauncherLog.HashValue(savedSettings.Workspace));
        BackupSettings(settingsPath);
        var selection = PrepareNew(defaultRoot, packageRoot, schemaRoot, false);
        _log.Write("WB-SETTINGS-RECOVERED", "workspace-recovery", valueHash: LauncherLog.HashValue(defaultRoot));
        return selection;
    }

    private WorkspaceSelection RecoverSavedWorkspace(IWin32Window owner, string packageRoot, string schemaRoot, bool smoke, string code)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(code) ? "WB-SETTINGS-RECOVER-422" : code;
        _log.Write(normalizedCode, "workspace-recovery");
        if (smoke) throw new InvalidOperationException($"{normalizedCode}: 已保存工作区无法恢复。");

        var defaultRoot = GetDefaultWorkspaceRoot(smoke);
        var choice = MessageBox.Show(owner,
            $"上次保存的 Worldbook Studio 工作区无法使用（{normalizedCode}）。\n\n旧设置不会被覆盖。请选择新的工作区，或取消退出。",
            "工作区需要恢复", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
        if (choice != DialogResult.OK) throw new OperationCanceledException();
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择新的 AWAKE Worldbook Studio 工作区。旧工作区设置不会被覆盖。",
            SelectedPath = defaultRoot,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog(owner) != DialogResult.OK) throw new OperationCanceledException();
        return PrepareNew(dialog.SelectedPath, packageRoot, schemaRoot, false);
    }

    private static string SelectWorkspace(IWin32Window owner, string defaultRoot, DialogResult choice)
    {
        if (choice == DialogResult.Yes) return defaultRoot;
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择 AWAKE Worldbook Studio 的工作区。工作区用于保存世界书编辑内容，不会写入游戏目录。",
            SelectedPath = defaultRoot,
            ShowNewFolderButton = true
        };
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.SelectedPath : throw new OperationCanceledException();
    }

    private WorkspaceSelection PrepareNew(string candidate, string packageRoot, string schemaRoot, bool allowExisting)
    {
        if (string.IsNullOrWhiteSpace(candidate)) throw new InvalidOperationException("WB-ROOT-001: workspace 路径不能为空。");
        var root = Path.GetFullPath(candidate);
        WorkspacePathPolicy.ValidateWorkspaceRoot(root, packageRoot, schemaRoot);
        var existed = Directory.Exists(root);
        if (File.Exists(root)) throw new InvalidOperationException("WB-ROOT-005: workspace 不能是文件。");
        Directory.CreateDirectory(root);
        WorkspacePathPolicy.ValidateWorkspaceRoot(root, packageRoot, schemaRoot);

        var markerPath = Path.Combine(root, MarkerName);
        var marker = ReadMarker(markerPath);
        var entries = Directory.EnumerateFileSystemEntries(root).Where(path => !path.Equals(markerPath, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (marker is null && entries.Length > 0 && !allowExisting)
            throw new InvalidOperationException("WB-ROOT-009: 选择的目录不是空工作区，也没有有效的 AWAKE 标记。");
        if (marker is null && entries.Length > 0 && allowExisting)
            throw new InvalidOperationException("WB-ROOT-009: 已有目录缺少 AWAKE 工作区标记。");

        return CommitWorkspace(root, existed, marker, createMarker: true);
    }

    private WorkspaceSelection PrepareExisting(string candidate, string packageRoot, string schemaRoot)
    {
        if (string.IsNullOrWhiteSpace(candidate)) throw new InvalidOperationException("WB-SETTINGS-RECOVER-422: 保存的 workspace 路径为空。");
        var root = Path.GetFullPath(candidate);
        if (File.Exists(root)) throw new InvalidOperationException("WB-SETTINGS-RECOVER-422: 保存的 workspace 是文件。");
        if (!Directory.Exists(root)) throw new InvalidOperationException("WB-SETTINGS-RECOVER-404: 保存的 workspace 不存在。");
        WorkspacePathPolicy.ValidateWorkspaceRoot(root, packageRoot, schemaRoot);
        var marker = ReadMarker(Path.Combine(root, MarkerName));
        if (marker is null) throw new InvalidOperationException("WB-SETTINGS-RECOVER-422: 保存的 workspace marker 无效。");
        return CommitWorkspace(root, existed: true, marker, createMarker: false);
    }

    private WorkspaceSelection CommitWorkspace(string root, bool existed, WorkspaceMarker? marker, bool createMarker)
    {
        WorkspacePathPolicy.EnsureNoReparsePoint(root);
        var markerPath = Path.Combine(root, MarkerName);
        var createdMarker = false;
        try
        {
            ProbeWrite(root);
            if (marker is null && createMarker)
            {
                WriteAtomic(markerPath, new WorkspaceMarker(1, StudioRuntimeConstants.Product, DateTime.UtcNow.ToString("O")));
                createdMarker = true;
            }
            WriteSettings(new StudioSettings(SettingsVersion, root, "zh-CN"));
            _log.Write("WB-WORKSPACE-READY", "workspace", valueHash: LauncherLog.HashValue(root));
            return new WorkspaceSelection(root, !existed || createdMarker);
        }
        catch (UnauthorizedAccessException exception)
        {
            RollbackWorkspace(root, existed, markerPath, createdMarker);
            throw new InvalidOperationException("WB-SETTINGS-WRITE-403: 无法写入工作区设置。", exception);
        }
        catch (IOException exception)
        {
            RollbackWorkspace(root, existed, markerPath, createdMarker);
            throw new InvalidOperationException("WB-SETTINGS-COMMIT-500: 工作区设置提交失败。", exception);
        }
        catch
        {
            RollbackWorkspace(root, existed, markerPath, createdMarker);
            throw;
        }
    }

    private static void RollbackWorkspace(string root, bool existed, string markerPath, bool createdMarker)
    {
        if (createdMarker && File.Exists(markerPath))
        {
            try { File.Delete(markerPath); } catch { }
        }
        if (!existed)
        {
            try
            {
                if (Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root).Any()) Directory.Delete(root);
            }
            catch { }
        }
    }

    private static SettingsReadResult ReadSettings(string path)
    {
        try
        {
            if (!File.Exists(path)) return new SettingsReadResult(SettingsReadKind.Missing, null, "WB-SETTINGS-FIRST-RUN");
            var settings = JsonSerializer.Deserialize<StudioSettings>(File.ReadAllText(path));
            return settings is { Version: SettingsVersion } && !string.IsNullOrWhiteSpace(settings.Workspace)
                ? new SettingsReadResult(SettingsReadKind.Valid, settings, string.Empty)
                : new SettingsReadResult(SettingsReadKind.Invalid, null, "WB-SETTINGS-RECOVER-422");
        }
        catch (UnauthorizedAccessException) { return new SettingsReadResult(SettingsReadKind.Unreadable, null, "WB-SETTINGS-READ-403"); }
        catch (IOException) { return new SettingsReadResult(SettingsReadKind.Unreadable, null, "WB-SETTINGS-READ-500"); }
        catch (JsonException) { return new SettingsReadResult(SettingsReadKind.Invalid, null, "WB-SETTINGS-RECOVER-422"); }
    }

    private static WorkspaceMarker? ReadMarker(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var marker = JsonSerializer.Deserialize<WorkspaceMarker>(File.ReadAllText(path));
            return marker is { Version: 1 } && string.Equals(marker.Product, StudioRuntimeConstants.Product, StringComparison.Ordinal) ? marker : null;
        }
        catch { return null; }
    }

    private static string GetSettingsPath(bool createDirectory = false)
    {
        var local = Environment.GetEnvironmentVariable("AWAKE_WB_SMOKE_LOCALAPPDATA")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local)) throw new InvalidOperationException("WB-SETTINGS-001: 找不到本地应用数据目录。");
        var directory = Path.Combine(local, "AWAKE", "WorldbookStudio");
        if (createDirectory) Directory.CreateDirectory(directory);
        return Path.Combine(directory, "settings.json");
    }

    private static void ProbeWrite(string root)
    {
        WorkspacePathPolicy.EnsureNoReparsePoint(root);
        var path = Path.Combine(root, $".awake-write-probe-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(path, "probe", new UTF8Encoding(false));
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    private static void WriteSettings(StudioSettings settings)
    {
        var path = GetSettingsPath(createDirectory: true);
        WriteAtomic(path, settings);
    }

    private static string GetDefaultWorkspaceRoot(bool smoke)
    {
        var documents = smoke
            ? Environment.GetEnvironmentVariable("AWAKE_WB_SMOKE_DOCUMENTS")
            : null;
        documents ??= Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents)) throw new InvalidOperationException("WB-SETTINGS-001: 找不到 Windows 文档目录。");
        return Path.Combine(documents, "AWAKE", "WorldbookStudio");
    }

    private static void BackupSettings(string settingsPath)
    {
        if (!File.Exists(settingsPath)) return;
        var directory = Path.GetDirectoryName(settingsPath)
            ?? throw new InvalidOperationException("WB-SETTINGS-002: 设置目录无效。");
        var name = Path.GetFileName(settingsPath);
        var backupPath = Path.Combine(directory, $"{name}.recovery-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.bak");
        File.Copy(settingsPath, backupPath, overwrite: false);
    }

    private static void WriteAtomic<T>(string target, T value)
    {
        var directory = Path.GetDirectoryName(target) ?? throw new InvalidOperationException("WB-SETTINGS-002: 设置目录无效。");
        Directory.CreateDirectory(directory);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            File.Move(temporary, target, true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }
}
