using System;
using Newtonsoft.Json.Linq;

namespace Awake;

internal static class AwakeEventDataLoader
{
    internal static bool TryParseDefinition(JObject payload, out AwakeEventDefinition definition, out string error)
    {
        definition = null;
        error = null;
        if (payload == null)
        {
            error = "payload";
            return false;
        }
        JObject eventObject = payload["event"] as JObject ?? payload;
        try
        {
            definition = new AwakeEventDefinition(
                Str(eventObject["id"]),
                Str(eventObject["title"]),
                Str(eventObject["body"]),
                Str(eventObject["optionA"]),
                Str(eventObject["optionB"]),
                ParseDialogueAction(eventObject["dialogueAction"] as JObject),
                ParseDialogueAction(eventObject["discussionAction"] as JObject),
                ParseEnum<AwakeEventSource>(Str(eventObject["source"])),
                ParseEnum<AwakeEventContext>(Str(eventObject["context"])),
                ParseEnum<AwakeEventSubject>(Str(eventObject["subject"])),
                ParseEnum<AwakeEventContent>(Str(eventObject["content"])),
                ParseEnum<AwakeEventResolution>(Str(eventObject["resolution"])),
                ParseEnum<AwakeEventChoiceShape>(Str(eventObject["choiceShape"])),
                ParseEnum<AwakeEventPersistence>(Str(eventObject["persistence"])),
                ParseEffect(eventObject["effect"] as JObject));
        }
        catch (Exception ex)
        {
            error = "parse:" + ex.Message;
            return false;
        }
        if (!AwakeEventValidation.Validate(definition, out error))
        {
            return false;
        }
        return true;
    }

    internal static bool TryParseRule(JObject payload, out AwakeEventRule rule, out string error)
    {
        rule = null;
        error = null;
        AwakeEventDefinition definition;
        if (!TryParseDefinition(payload, out definition, out error))
        {
            return false;
        }
        try
        {
            rule = new AwakeEventRule(
                definition,
                IntValue(payload["weight"], 1),
                IntValue(payload["cooldownHours"], 0),
                ParseEnum<AwakeEventCondition>(Str(payload["condition"])) ?? AwakeEventCondition.Always,
                Str(payload["nextEventId"]),
                IntValue(payload["maxPerDay"], 0));
            return true;
        }
        catch (Exception ex)
        {
            error = "rule:" + ex.Message;
            return false;
        }
    }

    internal static AwakeEventDialogueAction ParseDialogueAction(JObject obj)
    {
        if (obj == null) return null;
        return new AwakeEventDialogueAction(
            Str(obj["choice"]),
            Str(obj["targetId"]),
            Str(obj["openingHint"]));
    }

    private static AwakeEventEffect ParseEffect(JObject obj)
    {
        if (obj == null) return null;
        return new AwakeEventEffect(
            Str(obj["choice"]),
            Str(obj["targetId"]),
            IntValue(obj["trustDelta"], 0),
            IntValue(obj["loveDelta"], 0),
            IntValue(obj["hostilityDelta"], 0),
            Str(obj["reason"]));
    }

    private static T? ParseEnum<T>(string value) where T : struct
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        T parsed;
        return Enum.TryParse(value, true, out parsed) ? parsed : (T?)null;
    }

    private static string Str(JToken token)
    {
        return token == null ? null : token.ToString();
    }

    private static int IntValue(JToken token, int fallback)
    {
        if (token == null || token.Type != JTokenType.Integer) return fallback;
        try { return (int)token; } catch { return fallback; }
    }
}
