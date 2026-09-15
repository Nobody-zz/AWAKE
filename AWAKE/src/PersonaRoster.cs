using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Awake;

/// <summary>
/// 角色卡运行时名册。
///
/// 直接扫描「角色卡根目录」下的 <c>persona_definitions/definitions/**.json</c>，按 characterId 建索引，
/// 供 <see cref="PersonaRuntimeProvider"/> 按「正在和谁说话」挑卡；不再依赖运行时包里那一张单数 definition。
///
/// <b>根目录由调用方给定，且刻意不从 manifest 推导</b>（丙-a2）：manifest 只影响
/// <c>personaDefinitionDirectory</c> / <c>personaTagRegistryFile</c> 两个相对路径的覆盖，
/// 缺失时走 <see cref="DefaultDefinitionDirectory"/> / <see cref="DefaultRegistryFile"/> 默认值。
/// 这样角色卡装载与世界书能否加载成功无关。
///
/// 审批门（2026-09-14 定，试行名单制）：
/// <list type="bullet">
/// <item><c>approved</c>：全收。</item>
/// <item><c>draft</c>：默认不收；仅当该卡的 <c>definitionId</c>（或 <c>characterId</c>）出现在
/// 同目录 <c>pilot-allowlist.json</c> 的试行名单里才放行。<b>放行只在内存内把 Status 视为 approved，
/// 源文件与 definition 的 Raw 快照都保持 draft 不动</b>，并且每次放行都留一条警告。</item>
/// <item><c>disabled</c>：一律不收，名单也不放行。</item>
/// </list>
///
/// 本类刻意不引用 AwakeLog / TaleWorlds / MarcusAwakeFramework，
/// 以便离线模拟器（tools/worldbook-runtime-sim）编译同一份源码直接跑。
/// </summary>
internal sealed class PersonaRoster
{
    internal const string AllowlistSchema = "awake.persona.pilot-allowlist.v1";
    internal const string DefaultDefinitionDirectory = "persona_definitions/definitions";
    internal const string DefaultRegistryFile = "persona_definitions/tag_registry.json";
    internal const string AllowlistFileName = "pilot-allowlist.json";

    private readonly Dictionary<string, PersonaDefinition> _byCharacterId =
        new Dictionary<string, PersonaDefinition>(StringComparer.Ordinal);
    private readonly List<PersonaDefinition> _fallbackPool = new List<PersonaDefinition>();
    private readonly List<string> _admittedDraftIds = new List<string>();
    private readonly List<WorldbookImportWarning> _warnings = new List<WorldbookImportWarning>();
    private readonly HashSet<string> _allowlistEntries = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _allowlistMatched = new HashSet<string>(StringComparer.Ordinal);

    private PersonaRoster(string worldbookRoot, string sourceLabel)
    {
        Root = worldbookRoot ?? string.Empty;
        SourceLabel = sourceLabel ?? string.Empty;
    }

    /// <summary>角色卡根目录（含 persona_definitions/ 的那一层）。</summary>
    internal string Root { get; }

    /// <summary>名册来源（用于警告与日志定位）。</summary>
    internal string SourceLabel { get; }

    /// <summary>实际扫描的 definitions 目录绝对路径。</summary>
    internal string DefinitionDirectory { get; private set; } = string.Empty;

    /// <summary>试行名单文件绝对路径。</summary>
    internal string AllowlistPath { get; private set; } = string.Empty;

    /// <summary>标签注册表；缺失时为 null（选卡仍可进行，但生成必然退兜底）。</summary>
    internal PersonaTagRegistry Registry { get; private set; }

    /// <summary>按 characterId 可命中的卡数。</summary>
    internal int CharacterCount => _byCharacterId.Count;

    /// <summary>不针对具体人的兜底池大小（scope=fallback/role/global）。</summary>
    internal int FallbackCount => _fallbackPool.Count;

    /// <summary>名册里可用的卡总数。</summary>
    internal int Count => _byCharacterId.Count + _fallbackPool.Count;

    internal bool HasRegistry => Registry != null;

    /// <summary>试行名单条数（文件里写的）。</summary>
    internal int AllowlistEntryCount => _allowlistEntries.Count;

    /// <summary>本次因试行名单被放行的草稿卡 definitionId，按序。</summary>
    internal IReadOnlyList<string> AdmittedDraftIds => _admittedDraftIds;

    internal IReadOnlyList<WorldbookImportWarning> Warnings => _warnings;

    /// <summary>可用集合的内容摘要（id|characterId|源状态），与目录顺序无关。</summary>
    internal string Digest { get; private set; } = string.Empty;

    internal static PersonaRoster Create(string worldbookRoot, string sourceLabel)
    {
        PersonaRoster roster = new PersonaRoster(worldbookRoot, sourceLabel);
        roster.Load();
        return roster;
    }

    /// <summary>
    /// 选卡：先按 characterId 精确命中；未命中则退兜底池（scope=fallback &gt; role &gt; global）。
    /// 两者都没有时返回 false，调用方应走只含 ID/NAME 的运行时兜底。
    /// </summary>
    internal bool TrySelect(string characterId, out PersonaDefinition definition, out bool exact)
    {
        definition = null;
        exact = false;
        if (!string.IsNullOrWhiteSpace(characterId)
            && _byCharacterId.TryGetValue(characterId, out PersonaDefinition matched)
            && matched != null)
        {
            definition = matched;
            exact = true;
            return true;
        }
        if (_fallbackPool.Count > 0)
        {
            definition = _fallbackPool[0];
            return true;
        }
        return false;
    }

    internal string BuildStatusText()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("persona_roster characters=").Append(CharacterCount)
            .Append(" fallbacks=").Append(FallbackCount)
            .Append(" registry_tags=").Append(Registry == null ? 0 : Registry.Count)
            .Append(" allowlist=").Append(AllowlistEntryCount)
            .Append(" drafts_admitted=").Append(_admittedDraftIds.Count)
            .Append(" warnings=").Append(_warnings.Count);
        if (!string.IsNullOrWhiteSpace(Digest)) builder.Append(" digest=").Append(Digest);
        return builder.ToString();
    }

    private void Load()
    {
        if (string.IsNullOrWhiteSpace(Root) || !Directory.Exists(Root))
        {
            _warnings.Add(new WorldbookImportWarning(SourceLabel, "persona.roster_root_missing", Root ?? string.Empty));
            return;
        }

        string definitionRelative = DefaultDefinitionDirectory;
        string registryRelative = DefaultRegistryFile;
        JObject manifest = TryReadRootManifest();
        if (manifest != null)
        {
            definitionRelative = Str(manifest, "personaDefinitionDirectory") ?? definitionRelative;
            registryRelative = Str(manifest, "personaTagRegistryFile") ?? registryRelative;
        }
        DefinitionDirectory = Resolve(Root, definitionRelative);
        AllowlistPath = Path.Combine(
            Root,
            Path.GetDirectoryName(registryRelative.Replace('/', Path.DirectorySeparatorChar)) ?? "persona_definitions",
            AllowlistFileName);

        // 扫描与解析完全复用既有 PersonaDataLoader：把 manifest 的 persona 路径投影成一份最小文档再喂进去，
        // 避免出现第二套目录扫描实现。
        WorldbookDocument document = new WorldbookDocument
        {
            Manifest = new WorldbookManifest
            {
                PersonaDefinitionDirectory = definitionRelative,
                PersonaTagRegistryFile = registryRelative
            }
        };
        PersonaDataLoader.Load(document, Root);
        foreach (WorldbookImportWarning warning in document.Warnings) _warnings.Add(warning);
        if (document.PersonaTagRegistry != null)
        {
            Registry = new PersonaTagRegistry(document.PersonaTagRegistry);
            if (Registry.Count == 0)
            {
                _warnings.Add(new WorldbookImportWarning(Resolve(Root, registryRelative), "persona.registry_empty", "标签注册表里没有可用标签，所有卡都会展开失败。"));
            }
        }

        ReadAllowlist();

        // 排序后再入池，保证兜底选择与文件枚举顺序无关。
        IEnumerable<PersonaDefinition> ordered = document.PersonaDefinitions
            .Where(item => item != null)
            .OrderBy(item => item.CharacterId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(item => item.Id ?? string.Empty, StringComparer.Ordinal);

        HashSet<string> seenCharacterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (PersonaDefinition definition in ordered)
        {
            bool admittedAsDraft;
            if (!TryAdmit(definition, out admittedAsDraft)) continue;

            if (admittedAsDraft)
            {
                _admittedDraftIds.Add(definition.Id);
                _warnings.Add(new WorldbookImportWarning(
                    definition.SourcePath,
                    "persona.pilot_draft_admitted",
                    "草稿卡经试行名单放行：" + definition.Id));
            }
            if (!string.IsNullOrWhiteSpace(definition.CharacterId))
            {
                if (!seenCharacterIds.Add(definition.CharacterId))
                {
                    _warnings.Add(new WorldbookImportWarning(
                        definition.SourcePath,
                        "persona.character_duplicate",
                        "characterId 重复，已忽略后者：" + definition.CharacterId));
                    continue;
                }
                _byCharacterId[definition.CharacterId] = definition;
                continue;
            }

            if (ScopeRank(definition.Scope) < FallbackScopeRankUnknown) _fallbackPool.Add(definition);
            else
            {
                _warnings.Add(new WorldbookImportWarning(
                    definition.SourcePath,
                    "persona.definition_scope_unusable",
                    "既无 characterId，scope 也不可用于兜底：" + definition.Scope));
            }
        }

        _fallbackPool.Sort(CompareFallback);
        foreach (string entry in _allowlistEntries)
        {
            if (_allowlistMatched.Contains(entry)) continue;
            _warnings.Add(new WorldbookImportWarning(
                AllowlistPath,
                "persona.pilot_allowlist_unmatched",
                "试行名单指向的卡在名册里不存在（或未通过状态门）：" + entry));
        }
        Digest = ComputeDigest();
    }

    private const int FallbackScopeRankUnknown = 3;

    /// <summary>
    /// 状态门。返回 false 即该卡不进入名册。
    /// 放行草稿时<b>只改内存内 Status</b>，源文件与 Raw 不动。
    /// </summary>
    private bool TryAdmit(PersonaDefinition definition, out bool admittedAsDraft)
    {
        admittedAsDraft = false;
        if (definition == null || string.IsNullOrWhiteSpace(definition.Id)) return false;
        if (StringComparer.Ordinal.Equals(definition.Status, PersonaSchemaConstants.StatusDisabled)) return false;
        if (StringComparer.Ordinal.Equals(definition.Status, PersonaSchemaConstants.StatusApproved)) return true;

        if (!IsAllowlisted(definition)) return false;
        definition.Status = PersonaSchemaConstants.StatusApproved;
        admittedAsDraft = true;
        return true;
    }

    private bool IsAllowlisted(PersonaDefinition definition)
    {
        if (_allowlistEntries.Count == 0) return false;
        bool matched = false;
        if (!string.IsNullOrWhiteSpace(definition.Id) && _allowlistEntries.Contains(definition.Id))
        {
            _allowlistMatched.Add(definition.Id);
            matched = true;
        }
        if (!string.IsNullOrWhiteSpace(definition.CharacterId) && _allowlistEntries.Contains(definition.CharacterId))
        {
            _allowlistMatched.Add(definition.CharacterId);
            matched = true;
        }
        return matched;
    }

    private void ReadAllowlist()
    {
        if (!File.Exists(AllowlistPath)) return;
        try
        {
            JObject document = JObject.Parse(File.ReadAllText(AllowlistPath));
            string schema = Str(document, "schemaVersion");
            if (!StringComparer.Ordinal.Equals(schema, AllowlistSchema))
            {
                _warnings.Add(new WorldbookImportWarning(AllowlistPath, "persona.pilot_allowlist_schema", schema ?? string.Empty));
                return;
            }
            foreach (JToken token in document["entries"] as JArray ?? new JArray())
            {
                JObject item = token as JObject;
                if (item == null) continue;
                string definitionId = Str(item, "definitionId");
                string characterId = Str(item, "characterId");
                if (!string.IsNullOrWhiteSpace(definitionId)) _allowlistEntries.Add(definitionId);
                if (!string.IsNullOrWhiteSpace(characterId)) _allowlistEntries.Add(characterId);
                if (string.IsNullOrWhiteSpace(definitionId) && string.IsNullOrWhiteSpace(characterId))
                {
                    _warnings.Add(new WorldbookImportWarning(AllowlistPath, "persona.pilot_allowlist_entry_invalid", item.ToString(Newtonsoft.Json.Formatting.None)));
                }
            }
        }
        catch (Exception ex)
        {
            _warnings.Add(new WorldbookImportWarning(AllowlistPath, "persona.pilot_allowlist_parse_failed", ex.Message));
        }
    }

    private JObject TryReadRootManifest()
    {
        string path = Path.Combine(Root, "manifest.json");
        if (!File.Exists(path)) return null;
        try { return JObject.Parse(File.ReadAllText(path)); }
        catch (Exception ex)
        {
            _warnings.Add(new WorldbookImportWarning(path, "persona.roster_manifest_parse_failed", ex.Message));
            return null;
        }
    }

    private static string SourceStatus(PersonaDefinition definition)
    {
        JToken recorded = definition?.Raw?["status"];
        string value = recorded == null || recorded.Type == JTokenType.Null ? null : recorded.ToString();
        return string.IsNullOrWhiteSpace(value) ? "<missing>" : value;
    }

    private static int ScopeRank(string scope)
    {
        if (StringComparer.Ordinal.Equals(scope, "fallback")) return 0;
        if (StringComparer.Ordinal.Equals(scope, "role")) return 1;
        if (StringComparer.Ordinal.Equals(scope, "global")) return 2;
        return FallbackScopeRankUnknown;
    }

    private static int CompareFallback(PersonaDefinition left, PersonaDefinition right)
    {
        int rank = ScopeRank(left.Scope).CompareTo(ScopeRank(right.Scope));
        if (rank != 0) return rank;
        int priority = right.Priority.CompareTo(left.Priority);
        if (priority != 0) return priority;
        return string.CompareOrdinal(left.Id, right.Id);
    }

    private string ComputeDigest()
    {
        List<string> lines = new List<string>();
        foreach (PersonaDefinition definition in _byCharacterId.Values.Concat(_fallbackPool))
        {
            lines.Add(definition.Id + "|" + (definition.CharacterId ?? string.Empty) + "|" + SourceStatus(definition));
        }
        lines.Sort(StringComparer.Ordinal);

        StringBuilder builder = new StringBuilder();
        builder.Append("schema=").Append(AllowlistSchema)
            .Append(" allowlist=").Append(_allowlistEntries.Count)
            .Append(" registry_tags=").Append(Registry == null ? 0 : Registry.Count)
            .Append('\n');
        foreach (string line in lines) builder.Append(line).Append('\n');
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            StringBuilder hex = new StringBuilder(hash.Length * 2);
            foreach (byte item in hash) hex.Append(item.ToString("x2"));
            return hex.ToString();
        }
    }

    private static string Resolve(string baseDir, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative)) return baseDir ?? string.Empty;
        return Path.GetFullPath(Path.Combine(
            baseDir ?? string.Empty,
            relative.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string Str(JObject obj, string name)
    {
        JToken token = obj?[name];
        if (token == null || token.Type == JTokenType.Null) return null;
        string value = token.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
