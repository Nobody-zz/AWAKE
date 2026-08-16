using System;
using System.IO;
using System.Reflection;

namespace Awake;

internal static class WorldbookRuntime
{
    private static WorldbookService _current;

    internal static WorldbookService Current => _current;

    internal static void EnsureCreated()
    {
        if (_current != null) return;
        string manifestPath = LocateManifest();
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            AwakeLog.Write("worldbook_runtime_manifest_not_found");
            return;
        }
        try
        {
            WorldbookDocument document = WorldbookLoader.LoadDirectory(manifestPath);
            _current = new WorldbookService(document);
            AwakeLog.Write("worldbook_runtime_initialized rules=" + document.Rules.Count
                + " personas=" + document.Personas.Count
                + " warnings=" + document.Warnings.Count);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("worldbook_runtime_init_error error=" + ex.Message);
        }
    }

    internal static void ShutdownCurrent()
    {
        _current = null;
    }

    private static string LocateManifest()
    {
        string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
        DirectoryInfo current = new DirectoryInfo(assemblyDir);
        for (int i = 0; i < 6 && current != null; i++)
        {
            string candidate = Path.Combine(current.FullName, "ModuleData", "Worldbook", "manifest.json");
            if (File.Exists(candidate)) return candidate;
            candidate = Path.Combine(current.FullName, "Worldbook", "manifest.json");
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        return null;
    }
}
