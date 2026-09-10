using System.Text.Json;
using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.Launcher;

public sealed class LauncherLog
{
    private readonly object _gate = new();
    private readonly string _path;

    public LauncherLog()
    {
        var root = Environment.GetEnvironmentVariable("AWAKE_WB_SMOKE_LOCALAPPDATA")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) root = System.IO.Path.GetTempPath();
        var directory = System.IO.Path.Combine(root, "AWAKE", "WorldbookStudio", "logs");
        Directory.CreateDirectory(directory);
        _path = System.IO.Path.Combine(directory, $"launcher-{DateTime.UtcNow:yyyyMMdd}.log");
    }

    public string Path => _path;

    public void Write(string code, string phase, int? pid = null, int? exitCode = null, string? valueHash = null)
    {
        var entry = new
        {
            atUtc = DateTime.UtcNow,
            code,
            phase,
            port = StudioRuntimeConstants.Port,
            pid,
            exitCode,
            valueHash
        };
        var line = JsonSerializer.Serialize(entry);
        lock (_gate) File.AppendAllText(_path, line + Environment.NewLine);
    }

    public static string HashValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : StudioRuntimeHashing.Sha256Prefix(value);
}
