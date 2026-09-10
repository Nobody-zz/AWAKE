using System;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Awake;

internal static class WorldbookRuntime
{
    private static WorldbookService _current;
    private static WorldKnowledgeQueryService _knowledge;
    private static WorldbookActivationState _activation;
    private static string _requestedPackageId = string.Empty;

    internal static WorldbookService Current => _current;
    internal static IWorldKnowledgeQuery Knowledge => _knowledge;
    internal static int OverlayRevision => _knowledge == null ? 0 : (_knowledge as WorldKnowledgeQueryService)?.OverlayRevision ?? 0;
    internal static string ActivePackageId => _activation?.PackageId ?? string.Empty;

    internal static bool TryApplyOverlay(string kind, string targetId, string value, int baseRevision, string reason, out string error)
    {
        if (_knowledge == null) { error = "WB2-MANIFEST-MISSING"; return false; }
        WorldKnowledgeQueryService service = _knowledge as WorldKnowledgeQueryService;
        if (service == null) { error = "WB2-MANIFEST-MISSING"; return false; }
        return service.TryApplyOverlay(kind, targetId, value, baseRevision, reason, out error);
    }

    internal static string ExportOverlay()
    {
        string json = ExportOverlayJson();
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;
        string root = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".", "PlayerExports", "WorldbookOverlays");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "campaign-overlay.json");
        string temp = path + ".tmp";
        File.WriteAllText(temp, json, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(temp, path, null);
        else File.Move(temp, path);
        return path;
    }

    internal static string ExportOverlayJson()
    {
        return _knowledge is WorldKnowledgeQueryService service
            ? service.ExportOverlay().ToString(Newtonsoft.Json.Formatting.None)
            : string.Empty;
    }

    internal static bool ImportOverlayJson(string json, out string error)
    {
        error = string.Empty;
        if (_knowledge is not WorldKnowledgeQueryService service) { error = "WB2-MANIFEST-MISSING"; return false; }
        try { return service.TryImportOverlay(Newtonsoft.Json.Linq.JObject.Parse(json), out error); }
        catch (Exception ex) { error = "WB2-OVERLAY-IMPORT:" + ex.Message; return false; }
    }

    internal static string ExportActivationJson()
    {
        return _activation?.ToJson().ToString(Newtonsoft.Json.Formatting.None) ?? string.Empty;
    }

    internal static bool ImportActivationJson(string json, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json)) return true;
        try
        {
            JObject document = JObject.Parse(json);
            if (!StringComparer.Ordinal.Equals(document["schemaVersion"]?.Value<string>(), "awake.worldbook.campaign-activation.v1")) { error = "WB2-SCHEMA-UNSUPPORTED:activation"; return false; }
            _requestedPackageId = document["world"]?["packageId"]?.Value<string>() ?? string.Empty;
            if (_knowledge != null && !StringComparer.Ordinal.Equals(_requestedPackageId, ActivePackageId)) Reload();
            return true;
        }
        catch (Exception ex) { error = "WB2-ACTIVATION-IMPORT:" + ex.Message; return false; }
    }

    internal static void EnsureCreated()
    {
        if (_knowledge != null || _current != null) return;
        string entryPath = LocateManifest();
        if (string.IsNullOrWhiteSpace(entryPath))
        {
            AwakeLog.Write("worldbook_runtime_manifest_not_found");
            return;
        }
        try
        {
            string manifestPath = entryPath;
            WorldbookVerifiedPackage verified;
            JObject entry = JObject.Parse(File.ReadAllText(entryPath));
            if (StringComparer.Ordinal.Equals(entry["schemaVersion"]?.Value<string>(), "awake.worldbook.registry.v1"))
            {
                WorldbookPackageRegistry registry = WorldbookPackageRegistry.Load(entryPath);
                _activation = registry.Select(_requestedPackageId);
                verified = registry.LastSelectedPackage;
                manifestPath = verified.ManifestPath;
            }
            else if (StringComparer.Ordinal.Equals(entry["schemaVersion"]?.Value<string>(), "awake.worldbook.v2"))
            {
                verified = WorldbookPackageIntegrity.ReadAndVerify(entryPath);
                _activation = new WorldbookActivationState
                {
                    PackageId = verified.Manifest["packageId"]?.Value<string>() ?? string.Empty,
                    Version = verified.Manifest["version"]?.Value<string>() ?? string.Empty,
                    PackageHash = verified.PackageHash,
                    ManifestPath = verified.ManifestPath
                };
            }
            else throw new InvalidOperationException("WB2-SCHEMA-UNSUPPORTED:entry");
            WorldKnowledgeSnapshot snapshot = WorldKnowledgeLoader.LoadVerified(verified);
            _current = null;
            _knowledge = new WorldKnowledgeQueryService(snapshot);
            WorldEventServices.BindKnowledge(snapshot, _knowledge);
            AwakeLog.Write("worldbook_runtime_initialized schema=awake.worldbook.v2 build_id=" + AwakeVersion.BuildId
                + " package=" + (_activation?.PackageId ?? string.Empty)
                + " manifest_sha256=" + AwakeBuildIdentity.TryComputeFileSha256(manifestPath)
                + " entries=" + snapshot.Entries.Count
                + " identities=" + snapshot.Identities.Count
                + " referrals=" + snapshot.Referrals.Count
                + " warnings=" + snapshot.Warnings.Count);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("worldbook_runtime_init_error error=" + ex.Message);
        }
    }

    internal static void ShutdownCurrent()
    {
        _current = null;
        _knowledge = null;
    }

    internal static void Reload()
    {
        ShutdownCurrent();
        EnsureCreated();
    }

    internal static string BuildStatusText()
    {
        if (_knowledge != null)
        {
            return _knowledge.BuildStatusText();
        }
        if (_current == null)
        {
            return "世界书未加载";
        }
        return "rules=" + _current.RuleCount
            + " personas=" + _current.PersonaCount
            + " warnings=" + _current.WarningCount;
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
