using System;
using System.IO;
using System.Reflection;

namespace Awake;

internal static class AwakeLog
{
    internal static bool Enabled = true;
    internal static Action<string> Recorder;
    private static readonly object FileLock = new object();

    internal static void Write(string line)
    {
        try
        {
            Recorder?.Invoke(line);
        }
        catch
        {
            // The in-memory recorder must never break gameplay.
        }
        if (!Enabled) return;
        try
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            DirectoryInfo current = new DirectoryInfo(assemblyDir);
            string moduleDir = assemblyDir;
            for (int i = 0; i < 6 && current != null; i++)
            {
                if (File.Exists(Path.Combine(current.FullName, "SubModule.xml")))
                {
                    moduleDir = current.FullName;
                    break;
                }
                current = current.Parent;
            }
            string logs = Path.Combine(moduleDir, "Logs");
            Directory.CreateDirectory(logs);
            string path = Path.Combine(logs, AwakeConstants.LogFileName);
            lock (FileLock)
            {
                TryRotate(path);
                File.AppendAllText(
                    path,
                    DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " " + line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never break gameplay.
        }
    }

    internal static void WriteCode(string code, string message)
    {
        Write("[" + (code ?? "unknown") + "] " + (message ?? string.Empty));
    }

    private static void TryRotate(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            FileInfo info = new FileInfo(path);
            if (info.Length < 2 * 1024 * 1024) return;
            string rotated = path + ".1";
            if (File.Exists(rotated)) File.Delete(rotated);
            File.Move(path, rotated);
        }
        catch
        {
            // Rotation must never break logging.
        }
    }
}
