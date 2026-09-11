using System;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;

namespace Awake;

internal enum PersonaLoadOutcome
{
    NoPersona,
    Loaded,
    Rejected
}

/// <summary>
/// Persona 锚点接缝（锚点切片）。
///
/// 只做三件事：固定存档键、保存/载入方向的字符串搬运、载入结果分类。
/// 本文件没有 campaign 逻辑依赖（不读 Campaign / Hero / 存档槽），Campaign 身份由调用方
/// 通过参数或 snapshotProvider 传入；仍 <c>using TaleWorlds.CampaignSystem</c> 只是为了
/// <see cref="IDataStore"/> 本身。
/// </summary>
internal static class PersonaContinuitySync
{
    /// <summary>存档键是兼容契约，写死后不得改名。</summary>
    internal const string SaveKey = "awake_persona_continuity_v1";
    internal const string TimelineId = "awake.timeline";
    internal const string RootBranchId = "root";
    internal const string OldSaveCampaignId = "oldSave";

    internal const string NoPersonaReason = "no_persona";
    internal const string ReasonCampaignIdEmpty = "campaign_id_empty";
    internal const string ReasonCampaignIdNotUnique = "campaign_id_not_unique";
    internal const string ReasonCampaignMismatch = "campaign_mismatch";
    internal const string ReasonCharacterMismatch = "character_mismatch";
    internal const string ReasonCharacterUnavailable = "character_unavailable";
    internal const string ReasonSchemaUnsupported = "schema_unsupported";
    internal const string ReasonMalformed = "persona.persistence.malformed_json";
    internal const string ReasonSerializeFailed = "persona.persistence.serialize_failed";

    private static readonly JsonSerializerSettings AnchorSerializer = new JsonSerializerSettings
    {
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Include,
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    /// <summary>
    /// 唯一搬运点：保存方向用 snapshotProvider 刷新字符串后写入，载入方向只取回字符串。
    /// 方向判别用 <see cref="IDataStore.IsSaving"/>，不用值变化启发式。
    /// 载入方向不解析；解析只在 <c>CampaignEvents.OnGameLoadedEvent</c>（见 Adopt）。
    /// </summary>
    internal static void Sync(IDataStore dataStore, ref string json, Func<string> snapshotProvider)
    {
        if (dataStore == null) return;
        if (dataStore.IsSaving)
        {
            json = snapshotProvider == null ? string.Empty : (snapshotProvider() ?? string.Empty);
            // fail-closed：没有可写身份（空档/oldSave/无主角）时不落任何 persona 记录。
            if (string.IsNullOrWhiteSpace(json)) return;
        }
        dataStore.SyncData(SaveKey, ref json);
    }

    /// <summary>
    /// 构造锚点快照。纯函数：身份字符串由调用方提供，本方法只做装载/序列化。
    /// campaign 身份为空、为共享的 <c>oldSave</c>、或主角不可用时拒绝写入。
    /// </summary>
    internal static bool TryBuildAnchorSnapshot(
        string campaignId,
        string characterId,
        out string json,
        out string reason)
    {
        json = string.Empty;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(campaignId))
        {
            reason = ReasonCampaignIdEmpty;
            return false;
        }
        if (StringComparer.Ordinal.Equals(campaignId, OldSaveCampaignId))
        {
            reason = ReasonCampaignIdNotUnique;
            return false;
        }
        if (string.IsNullOrWhiteSpace(characterId))
        {
            reason = ReasonCharacterUnavailable;
            return false;
        }

        PersonaPersistenceEnvelope envelope = new PersonaPersistenceEnvelope
        {
            Schema = AwakeStorageContract.PersonaContinuitySchema,
            CharacterId = characterId,
            Timeline = new PersonaTimelineIdentity
            {
                CampaignId = campaignId,
                SaveId = string.Empty,
                TimelineId = TimelineId,
                BranchId = RootBranchId,
                ParentBranchId = string.Empty,
                ForkSequence = 0
            },
            Sequence = 0,
            Watermarks = new PersonaProjectionWatermarks(),
            Source = string.Empty,
            PayloadHash = string.Empty
        };

        string error;
        if (!PersonaPersistenceValidator.TryValidateEnvelope(envelope, out error))
        {
            reason = error;
            return false;
        }
        json = Serialize(envelope);
        if (json.Length == 0)
        {
            reason = ReasonSerializeFailed;
            return false;
        }
        return true;
    }

    /// <summary>
    /// 载入分类（纯函数）。顺序：空串 → 当前档 guard → 反序列化 → schema 前置判等 →
    /// 校验器 → campaign 比对 → character 比对。任何一步不通过都 fail-closed，不做部分装载。
    /// 空/空白 json 表示"这是旧档，没有 persona"，返回 NoPersona 且不告警。
    /// </summary>
    internal static PersonaLoadOutcome Adopt(
        string json,
        string currentCampaignId,
        string currentCharacterId,
        out PersonaPersistenceEnvelope envelope,
        out string reason)
    {
        envelope = null;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            reason = NoPersonaReason;
            return PersonaLoadOutcome.NoPersona;
        }
        if (string.IsNullOrWhiteSpace(currentCampaignId))
        {
            reason = ReasonCampaignIdEmpty;
            return PersonaLoadOutcome.Rejected;
        }
        if (StringComparer.Ordinal.Equals(currentCampaignId, OldSaveCampaignId))
        {
            reason = ReasonCampaignIdNotUnique;
            return PersonaLoadOutcome.Rejected;
        }

        PersonaPersistenceEnvelope parsed = Deserialize(json);
        if (parsed == null)
        {
            reason = ReasonMalformed;
            return PersonaLoadOutcome.Rejected;
        }
        // 装载器只收 continuity；校验器本身仍接受 override，属既有行为，不改。
        if (!StringComparer.Ordinal.Equals(parsed.Schema, AwakeStorageContract.PersonaContinuitySchema))
        {
            reason = ReasonSchemaUnsupported;
            return PersonaLoadOutcome.Rejected;
        }

        string error;
        if (!PersonaPersistenceValidator.TryValidateEnvelope(parsed, out error))
        {
            reason = error;
            return PersonaLoadOutcome.Rejected;
        }
        if (!StringComparer.Ordinal.Equals(parsed.Timeline.CampaignId, currentCampaignId))
        {
            reason = ReasonCampaignMismatch;
            return PersonaLoadOutcome.Rejected;
        }
        if (!StringComparer.Ordinal.Equals(parsed.CharacterId, currentCharacterId ?? string.Empty))
        {
            reason = ReasonCharacterMismatch;
            return PersonaLoadOutcome.Rejected;
        }

        envelope = parsed;
        reason = string.Empty;
        return PersonaLoadOutcome.Loaded;
    }

    private static string Serialize(PersonaPersistenceEnvelope envelope)
    {
        try
        {
            return JsonConvert.SerializeObject(envelope, AnchorSerializer) ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static PersonaPersistenceEnvelope Deserialize(string json)
    {
        try
        {
            return JsonConvert.DeserializeObject<PersonaPersistenceEnvelope>(json, AnchorSerializer);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
