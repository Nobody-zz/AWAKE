using System;
using Newtonsoft.Json.Linq;

namespace Awake;

public interface IAwakeContentPack
{
    string Id { get; }
    void Register(IAwakeContentRegistry registry);
}

public interface IAwakeContentRegistry
{
    bool RegisterRule(AwakeContentRule rule);
    bool RegisterEvent(AwakeContentEvent evt);
    bool RegisterProactiveMotive(AwakeContentMotive motive);
}

public sealed class AwakeContentRule
{
    public string Id { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = "awake.rule.v1";
    public string Group { get; set; } = "content";
    public int Priority { get; set; }
    public bool Enabled { get; set; } = true;
    public string Fingerprint { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
}

public sealed class AwakeContentEvent
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string Source { get; set; } = "PresetRule";
    public string Context { get; set; } = "Settlement";
    public string Subject { get; set; } = "PlayerNpc";
    public string Content { get; set; } = "Daily";
    public string Resolution { get; set; } = "NarrativeOnly";
    public string ChoiceShape { get; set; } = "TwoChoice";
    public string Persistence { get; set; } = "Repeatable";
    public int Weight { get; set; } = 1;
    public int CooldownHours { get; set; }
    public int MaxPerDay { get; set; }
    public string Condition { get; set; } = "Always";
    public string NextEventId { get; set; } = string.Empty;
    public string DialogueChoice { get; set; } = string.Empty;
    public string DialogueTargetId { get; set; } = string.Empty;
    public string DialogueOpeningHint { get; set; } = string.Empty;
    public string DiscussionTargetId { get; set; } = string.Empty;
    public string DiscussionOpeningHint { get; set; } = string.Empty;
    public string EffectChoice { get; set; } = string.Empty;
    public string EffectTargetId { get; set; } = string.Empty;
    public int TrustDelta { get; set; }
    public int LoveDelta { get; set; }
    public int HostilityDelta { get; set; }
    public string EffectReason { get; set; } = string.Empty;
}

public sealed class AwakeContentMotive
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int BaseWeight { get; set; } = 1;
    public string OpeningHint { get; set; } = string.Empty;
    public int MinAffinity { get; set; } = -100;
    public int MaxAffinity { get; set; } = 100;
}

public static class AwakeContentPackManager
{
    public static void Register(IAwakeContentPack pack)
    {
        if (pack == null) return;
        try
        {
            pack.Register(new AwakeContentRegistry());
            AwakeLog.Write("content_pack_registered id=" + (pack.Id ?? "unknown"));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("content_pack_register_error id=" + (pack.Id ?? "unknown") + " error=" + ex.Message);
        }
    }
}

internal sealed class AwakeContentRegistry : IAwakeContentRegistry
{
    public bool RegisterRule(AwakeContentRule rule)
    {
        if (rule == null) return false;
        JObject payload;
        try
        {
            payload = JObject.Parse(string.IsNullOrWhiteSpace(rule.PayloadJson) ? "{}" : rule.PayloadJson);
        }
        catch
        {
            return false;
        }
        return AwakeRuleRegistry.Register(new AwakeRuleManifest
        {
            SchemaVersion = string.IsNullOrWhiteSpace(rule.SchemaVersion) ? "awake.rule.v1" : rule.SchemaVersion,
            Id = rule.Id,
            Group = string.IsNullOrWhiteSpace(rule.Group) ? "content" : rule.Group,
            Priority = rule.Priority,
            Enabled = rule.Enabled,
            Fingerprint = rule.Fingerprint,
            Payload = payload,
            Raw = payload
        });
    }

    public bool RegisterEvent(AwakeContentEvent evt)
    {
        if (evt == null || string.IsNullOrWhiteSpace(evt.Id)) return false;
        JObject eventObject = new JObject
        {
            ["id"] = evt.Id,
            ["title"] = evt.Title,
            ["body"] = evt.Body,
            ["optionA"] = evt.OptionA,
            ["optionB"] = evt.OptionB,
            ["source"] = evt.Source,
            ["context"] = evt.Context,
            ["subject"] = evt.Subject,
            ["content"] = evt.Content,
            ["resolution"] = evt.Resolution,
            ["choiceShape"] = evt.ChoiceShape,
            ["persistence"] = evt.Persistence
        };
        if (!string.IsNullOrWhiteSpace(evt.DialogueChoice))
        {
            eventObject["dialogueAction"] = new JObject
            {
                ["choice"] = evt.DialogueChoice,
                ["targetId"] = evt.DialogueTargetId,
                ["openingHint"] = evt.DialogueOpeningHint
            };
        }
        if (!string.IsNullOrWhiteSpace(evt.DiscussionTargetId))
        {
            eventObject["discussionAction"] = new JObject
            {
                ["choice"] = "discuss",
                ["targetId"] = evt.DiscussionTargetId,
                ["openingHint"] = evt.DiscussionOpeningHint
            };
        }
        if (!string.IsNullOrWhiteSpace(evt.EffectChoice))
        {
            eventObject["effect"] = new JObject
            {
                ["choice"] = evt.EffectChoice,
                ["targetId"] = evt.EffectTargetId,
                ["trustDelta"] = evt.TrustDelta,
                ["loveDelta"] = evt.LoveDelta,
                ["hostilityDelta"] = evt.HostilityDelta,
                ["reason"] = evt.EffectReason
            };
        }
        JObject payload = new JObject
        {
            ["kind"] = "event",
            ["weight"] = evt.Weight,
            ["cooldownHours"] = evt.CooldownHours,
            ["maxPerDay"] = evt.MaxPerDay,
            ["condition"] = evt.Condition,
            ["nextEventId"] = evt.NextEventId,
            ["event"] = eventObject
        };
        return AwakeRuleRegistry.Register(new AwakeRuleManifest
        {
            Id = evt.Id + ".event",
            Group = "content",
            Priority = 100,
            Enabled = true,
            Fingerprint = evt.Id,
            Payload = payload,
            Raw = payload
        });
    }

    public bool RegisterProactiveMotive(AwakeContentMotive motive)
    {
        if (motive == null || string.IsNullOrWhiteSpace(motive.Id)) return false;
        JObject payload = new JObject
        {
            ["kind"] = "proactive_motive",
            ["id"] = motive.Id,
            ["displayName"] = motive.DisplayName,
            ["baseWeight"] = motive.BaseWeight,
            ["openingHint"] = motive.OpeningHint,
            ["minAffinity"] = motive.MinAffinity,
            ["maxAffinity"] = motive.MaxAffinity
        };
        return AwakeRuleRegistry.Register(new AwakeRuleManifest
        {
            Id = motive.Id + ".motive",
            Group = "content",
            Priority = 100,
            Enabled = true,
            Fingerprint = motive.Id,
            Payload = payload,
            Raw = payload
        });
    }
}
