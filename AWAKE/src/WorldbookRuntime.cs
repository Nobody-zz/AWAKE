using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Awake;

internal static class WorldbookRuntime
{
    private static WorldbookService _current;
    private static WorldKnowledgeQueryService _knowledge;
    private static PersonaRuntimeProvider _persona;
    private static WorldbookActivationState _activation;
    private static string _requestedPackageId = string.Empty;

    internal static WorldbookService Current => _current;
    internal static IWorldKnowledgeQuery Knowledge => _knowledge;

    internal static void SetKnowledgeForTesting(WorldKnowledgeQueryService knowledge)
    {
        _knowledge = knowledge;
    }
    internal static int OverlayRevision => _knowledge == null ? 0 : (_knowledge as WorldKnowledgeQueryService)?.OverlayRevision ?? 0;
    internal static string ActivePackageId => _activation?.PackageId ?? string.Empty;
    internal static PersonaRuntimeProvider Persona => _persona;
    internal static int PersonaBundleRevision => _persona?.Bundle?.Revision ?? 0;
    internal static string PersonaBundleDigest => _persona?.Bundle?.Digest ?? string.Empty;
    internal static PersonaGenerationResult BuildPersonaProjection(ContextSnapshot snapshot)
    {
        return PersonaRuntimeProvider.BuildProjection(_persona ?? new PersonaRuntimeProvider(null), snapshot);
    }

    internal static bool TryApplyOverlay(string kind, string targetId, string value, int baseRevision, string reason, out string error)
    {
        if (_knowledge == null) { error = "WB2-MANIFEST-MISSING"; return false; }
        WorldKnowledgeQueryService service = _knowledge as WorldKnowledgeQueryService;
        if (service == null) { error = "WB2-MANIFEST-MISSING"; return false; }
        bool applied = service.TryApplyOverlay(kind, targetId, value, baseRevision, reason, out error);
        if (applied) _persona?.InvalidatePersonaCache();
        return applied;
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
        try
        {
            bool imported = service.TryImportOverlay(Newtonsoft.Json.Linq.JObject.Parse(json), out error);
            if (imported) _persona?.InvalidatePersonaCache();
            return imported;
        }
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
            string previousRequestedPackageId = _requestedPackageId;
            _requestedPackageId = document["world"]?["packageId"]?.Value<string>() ?? string.Empty;
            if (_knowledge != null && !StringComparer.Ordinal.Equals(_requestedPackageId, ActivePackageId) && !Reload())
            {
                _requestedPackageId = previousRequestedPackageId;
                error = "WB2-ACTIVATION-RELOAD-FAILED";
                return false;
            }
            return true;
        }
        catch (Exception ex) { error = "WB2-ACTIVATION-IMPORT:" + ex.Message; return false; }
    }

    internal static void EnsureCreated()
    {
        // 摘耦合（丙-a2）：角色卡名册先独立起好，不再等世界书包装载成败。
        EnsurePersonaCreated();
        if (_knowledge != null || _current != null) return;
        TryLoadAndPublish();
    }

    /// <summary>
    /// 独立装载角色卡名册。只依赖「磁盘上有一个含 persona_definitions/ 的目录」——
    /// 不依赖世界书 manifest、不依赖 v2 包完整性校验、不依赖世界书能否加载成功。
    /// </summary>
    private static void EnsurePersonaCreated()
    {
        if (_persona != null && _persona.Roster != null) return;
        string root = LocatePersonaRoot();
        if (string.IsNullOrWhiteSpace(root))
        {
            AwakeLog.Write("persona_runtime_root_not_found");
            _persona = new PersonaRuntimeProvider(null);
            return;
        }
        PersonaRoster roster = PersonaRoster.Create(root, root);
        _persona = new PersonaRuntimeProvider(null, roster);
        AwakeLog.Write("worldbook_runtime_persona_roster " + roster.BuildStatusText()
            + " bundle_present=False admitted=" + string.Join(",", roster.AdmittedDraftIds));
        foreach (WorldbookImportWarning warning in roster.Warnings)
        {
            AwakeLog.Write("worldbook_runtime_persona_roster_warning code=" + warning.Code
                + " source=" + warning.Source + " message=" + warning.Message);
        }
    }

    private static bool TryLoadAndPublish()
    {
        string entryPath = LocateManifest();
        if (string.IsNullOrWhiteSpace(entryPath))
        {
            AwakeLog.Write("worldbook_runtime_manifest_not_found");
            return false;
        }
        try
        {
            string manifestPath = entryPath;
            WorldbookVerifiedPackage verified;
            WorldbookActivationState candidateActivation;
            JObject entry = JObject.Parse(File.ReadAllText(entryPath));
            if (StringComparer.Ordinal.Equals(entry["schemaVersion"]?.Value<string>(), "awake.worldbook.registry.v1"))
            {
                WorldbookPackageRegistry registry = WorldbookPackageRegistry.Load(entryPath);
                candidateActivation = registry.Select(_requestedPackageId);
                verified = registry.LastSelectedPackage;
                manifestPath = verified.ManifestPath;
            }
            else if (StringComparer.Ordinal.Equals(entry["schemaVersion"]?.Value<string>(), "awake.worldbook.v2"))
            {
                verified = WorldbookPackageIntegrity.ReadAndVerify(entryPath);
                candidateActivation = new WorldbookActivationState
                {
                    PackageId = verified.Manifest["packageId"]?.Value<string>() ?? string.Empty,
                    Version = verified.Manifest["version"]?.Value<string>() ?? string.Empty,
                    PackageHash = verified.PackageHash,
                    ManifestPath = verified.ManifestPath
                };
            }
            else throw new InvalidOperationException("WB2-SCHEMA-UNSUPPORTED:entry");
            WorldKnowledgeSnapshot snapshot = WorldKnowledgeLoader.LoadVerified(verified);
            RuntimeBundle personaBundle;
            string personaError;
            if (!PersonaRuntimeBundleLoader.TryLoad(verified, out personaBundle, out personaError))
            {
                throw new InvalidOperationException(personaError);
            }
            WorldKnowledgeQueryService candidateKnowledge = new WorldKnowledgeQueryService(snapshot);
            // 丙-a2：名册已由 EnsurePersonaCreated 独立起好，不再以 manifest 所在目录为根；
            // 这里只负责把运行时包（若包里有 persona 节点）接到同一个 provider 上。
            EnsurePersonaCreated();
            PersonaRoster roster = _persona?.Roster;
            PersonaRuntimeProvider candidatePersona = new PersonaRuntimeProvider(personaBundle, roster);
            _current = null;
            _activation = candidateActivation;
            _knowledge = candidateKnowledge;
            _persona = candidatePersona;
            WorldEventServices.BindKnowledge(snapshot, candidateKnowledge);
            AwakeLog.Write("worldbook_runtime_initialized schema=awake.worldbook.v2 build_id=" + AwakeVersion.BuildId
                + " package=" + (_activation?.PackageId ?? string.Empty)
                + " manifest_sha256=" + AwakeBuildIdentity.TryComputeFileSha256(manifestPath)
                + " entries=" + snapshot.Entries.Count
                + " identities=" + snapshot.Identities.Count
                + " referrals=" + snapshot.Referrals.Count
                + " warnings=" + snapshot.Warnings.Count);
            AwakeLog.Write("worldbook_runtime_persona_bundle bundle_present=" + (personaBundle != null)
                + " roster_present=" + (roster != null)
                + " roster_chars=" + (roster == null ? 0 : roster.CharacterCount));
            return true;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("worldbook_runtime_init_error error=" + ex.Message);
            return false;
        }
    }

    internal static void ShutdownCurrent()
    {
        _current = null;
        _knowledge = null;
        _persona = null;
    }

    internal static bool Reload()
    {
        return TryLoadAndPublish();
    }

    internal static string BuildStatusText()
    {
        string text;
        if (_knowledge != null)
        {
            text = _knowledge.BuildStatusText();
        }
        else if (_current == null)
        {
            text = "世界书未加载";
        }
        else
        {
            text = "rules=" + _current.RuleCount
                + " personas=" + _current.PersonaCount
                + " warnings=" + _current.WarningCount;
        }
        if (_persona?.Roster != null)
        {
            text = text + Environment.NewLine + _persona.Roster.BuildStatusText();
        }
        return text;
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

    /// <summary>
    /// 定位角色卡根目录。**刻意不读 manifest**——这正是「角色卡不依赖世界书」的关键：
    /// manifest 缺失或包校验失败都不影响名册装载。定位逻辑独立成
    /// <see cref="PersonaRootLocator"/>，以便测试工程编同一份源码验证。
    /// </summary>
    private static string LocatePersonaRoot()
    {
        string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
        return PersonaRootLocator.Locate(assemblyDir);
    }
}

internal static class PersonaRuntimeBundleLoader
{
    internal static bool TryLoad(WorldbookVerifiedPackage package, out RuntimeBundle bundle, out string error)
    {
        bundle = null;
        error = string.Empty;
        if (package == null || package.Manifest == null || package.Runtime == null)
        {
            error = "persona.bundle_missing";
            return false;
        }
        JObject persona = (package.Runtime["extensions"] as JObject)?["persona"] as JObject;
        if (persona == null)
        {
            // 不是错误：丙-a 起，角色卡由 PersonaRoster 从目录直读，包内不再需要 persona 节点。
            // 但绝不静默——旧行为是 return true 不留痕，导致「全员兜底」查不出原因。
            AwakeLog.Write("persona_runtime_bundle_absent package=" + (package.Manifest?["packageId"]?.Value<string>() ?? string.Empty)
                + " reason=extensions.persona_missing");
            return true;
        }
        if (!StringComparer.Ordinal.Equals(persona["schemaVersion"]?.Value<string>(), "awake.persona.runtime-bundle.v1"))
        {
            error = "persona.bundle_schema_unsupported";
            return false;
        }
        JObject definitionJson = persona["definition"] as JObject;
        JObject registryJson = persona["registry"] as JObject;
        if (definitionJson == null || registryJson == null)
        {
            error = "persona.bundle_parts_missing";
            return false;
        }
        PersonaDefinition definition;
        if (!PersonaDataLoader.TryParseDefinition(definitionJson, "runtime.persona", package.ManifestPath, out definition))
        {
            error = "persona.definition_invalid";
            return false;
        }
        List<WorldbookImportWarning> warnings = new List<WorldbookImportWarning>();
        PersonaTagRegistryDocument registryDocument = PersonaDataLoader.ParseRegistry(registryJson, package.ManifestPath, warnings);
        if (!StringComparer.Ordinal.Equals(registryDocument.SchemaVersion, PersonaSchemaConstants.RegistrySchema))
        {
            error = "persona.registry_schema_unsupported";
            return false;
        }
        bundle = new RuntimeBundle
        {
            BundleId = package.Manifest["packageId"]?.Value<string>() ?? string.Empty,
            Version = package.Manifest["version"]?.Value<string>() ?? string.Empty,
            Revision = package.Runtime["revision"]?.Value<int>() ?? 0,
            Digest = package.PackageHash ?? string.Empty,
            Definition = definition,
            Registry = new PersonaTagRegistry(registryDocument)
        };
        if (!bundle.IsApproved)
        {
            bundle = null;
            error = "persona.bundle_not_approved";
            return false;
        }
        return true;
    }
}

internal sealed class PersonaRuntimeProvider
{
    private const int MaximumCacheEntries = 512;

    private readonly object _gate = new object();
    private readonly RuntimeBundle _bundle;
    private readonly PersonaRoster _roster;
    private readonly Dictionary<string, PersonaGenerationResult> _cache = new Dictionary<string, PersonaGenerationResult>(StringComparer.Ordinal);
    private readonly LinkedList<string> _cacheOrder = new LinkedList<string>();

    internal PersonaRuntimeProvider(RuntimeBundle bundle)
        : this(bundle, null)
    {
    }

    internal PersonaRuntimeProvider(RuntimeBundle bundle, PersonaRoster roster)
    {
        _bundle = bundle;
        _roster = roster;
    }

    internal RuntimeBundle Bundle => _bundle;

    /// <summary>角色卡名册；为空表示本次运行没有可用名册（只有包内单卡或直接兜底）。</summary>
    internal PersonaRoster Roster => _roster;

    internal static PersonaGenerationResult BuildProjection(PersonaRuntimeProvider provider, ContextSnapshot snapshot)
    {
        return (provider ?? new PersonaRuntimeProvider(null)).BuildProjection(snapshot, PersonaDslGenerator.DefaultMaximumDslBytes);
    }

    internal PersonaGenerationResult BuildProjection(ContextSnapshot snapshot, int maximumBytes = PersonaDslGenerator.DefaultMaximumDslBytes)
    {
        snapshot = (snapshot ?? new ContextSnapshot()).DeepClone();
        if (string.IsNullOrWhiteSpace(snapshot.CharacterId))
            return PersonaDslGenerator.BuildRuntimeFallback(snapshot, maximumBytes);

        PersonaDefinition definition = null;
        PersonaTagRegistry registry = null;

        // 1) 名册优先：按「正在和谁说话」挑卡（丙-a 主路径）。
        bool exact;
        if (_roster != null && _roster.TrySelect(snapshot.CharacterId, out definition, out exact) && definition != null)
        {
            registry = _roster.Registry;
        }
        // 2) 名册给不出卡时，退回包内那一张单数 definition（兼容旧包）。
        if (definition == null && _bundle != null && _bundle.IsApproved)
        {
            definition = _bundle.Definition;
            registry = _bundle.Registry;
        }
        // 3) 都没有：只含 ID/NAME 的运行时兜底。
        if (definition == null || registry == null)
            return PersonaDslGenerator.BuildRuntimeFallback(snapshot, maximumBytes);

        if (_bundle != null)
        {
            if (string.IsNullOrWhiteSpace(snapshot.BundleId)) snapshot.BundleId = _bundle.BundleId;
            if (snapshot.BundleRevision <= 0) snapshot.BundleRevision = _bundle.Revision;
            if (string.IsNullOrWhiteSpace(snapshot.BundleDigest)) snapshot.BundleDigest = _bundle.Digest;
        }
        string fingerprint = snapshot.ComputeFingerprint();
        lock (_gate)
        {
            PersonaGenerationResult cached;
            if (_cache.TryGetValue(fingerprint, out cached)) return cached;
        }
        PersonaGenerationResult generated = PersonaDslGenerator.Generate(definition, registry, snapshot, maximumBytes);
        generated.Fingerprint = fingerprint;
        lock (_gate)
        {
            if (!_cache.ContainsKey(fingerprint))
            {
                _cache[fingerprint] = generated;
                _cacheOrder.AddLast(fingerprint);
                while (_cache.Count > MaximumCacheEntries && _cacheOrder.First != null)
                {
                    string oldest = _cacheOrder.First.Value;
                    _cacheOrder.RemoveFirst();
                    _cache.Remove(oldest);
                }
            }
        }
        return generated;
    }

    internal void InvalidatePersonaCache()
    {
        lock (_gate)
        {
            _cache.Clear();
            _cacheOrder.Clear();
        }
    }
}
