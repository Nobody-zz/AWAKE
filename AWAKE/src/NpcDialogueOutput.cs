using System;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class NpcDialogueValidatedOutput
{
    internal string Reply { get; }
    internal string Mood { get; }
    internal string[] Effects { get; }
    internal NpcDialogueCommandProposal Command { get; }

    internal NpcDialogueValidatedOutput(string reply, string mood, string[] effects, NpcDialogueCommandProposal command)
    {
        Reply = reply ?? string.Empty;
        Mood = mood ?? string.Empty;
        Effects = effects ?? Array.Empty<string>();
        Command = command;
    }
}

internal static class NpcDialogueOutputValidator
{
    internal static bool TryValidate(string text, string expectedContractId, out NpcDialogueValidatedOutput output, out string error)
    {
        output = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "empty_output";
            return false;
        }
        if (!StringComparer.Ordinal.Equals(expectedContractId, NpcDialogueConstants.OutputContractId))
        {
            error = "contract_mismatch";
            return false;
        }
        JObject root;
        try
        {
            root = JObject.Parse(text);
        }
        catch (Exception ex)
        {
            error = "invalid_json:" + ex.Message;
            return false;
        }
        if (root["reply"] is not JValue replyValue
            || replyValue.Type != JTokenType.String
            || string.IsNullOrWhiteSpace(replyValue.Value<string>())
            || replyValue.Value<string>().Length > 4000)
        {
            error = "missing_reply";
            return false;
        }
        string reply = replyValue.Value<string>();

        if (root["mood"] is not JValue moodValue
            || moodValue.Type != JTokenType.String
            || string.IsNullOrWhiteSpace(moodValue.Value<string>())
            || moodValue.Value<string>().Length > 8)
        {
            error = "missing_mood";
            return false;
        }
        string mood = moodValue.Value<string>();

        if (root["effects"] is not JArray effects || effects.Count > 8)
        {
            error = "missing_effects";
            return false;
        }
        string[] effectArray = new string[effects.Count];
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] is not JValue valueToken
                || valueToken.Type != JTokenType.String
                || string.IsNullOrWhiteSpace(valueToken.Value<string>()))
            {
                error = "invalid_effects";
                return false;
            }
            effectArray[i] = valueToken.Value<string>();
        }

        NpcDialogueCommandProposal command = null;
        if (root["command"] != null)
        {
            if (root["command"] is not JObject commandObject)
            {
                error = "invalid_command";
                return false;
            }
            string commandId = (string)commandObject["commandId"];
            JToken argumentsToken = commandObject["arguments"];
            if (string.IsNullOrWhiteSpace(commandId)
                || commandId.Length > 80
                || argumentsToken is not JObject argumentsObject)
            {
                error = "invalid_command";
                return false;
            }
            string reason = (string)commandObject["reason"] ?? string.Empty;
            if (reason.Length > 200)
            {
                error = "invalid_command";
                return false;
            }
            if (Encoding.UTF8.GetByteCount(commandObject.ToString(Newtonsoft.Json.Formatting.None)) > 16 * 1024)
            {
                error = "invalid_command";
                return false;
            }
            command = new NpcDialogueCommandProposal(
                commandId,
                argumentsObject.ToString(Newtonsoft.Json.Formatting.None),
                reason);
        }

        if (root.Count > 4)
        {
            error = "unexpected_properties";
            return false;
        }

        output = new NpcDialogueValidatedOutput(reply, mood, effectArray, command);
        return true;
    }
}

internal static class NpcDialogueStateFormatter
{
    internal static string FormatIdentity(string name, string gender, string culture)
    {
        string genderText;
        if (StringComparer.Ordinal.Equals(gender, "female")) genderText = "女性";
        else if (StringComparer.Ordinal.Equals(gender, "male")) genderText = "男性";
        else genderText = "未知";
        if (string.IsNullOrWhiteSpace(culture)) return name + "，" + genderText;
        return name + "，" + genderText + "，" + culture;
    }

    internal static string FormatState(JObject relationship, JObject body, JObject estrus)
    {
        return "关系、身体与发情状态由内容包提供。";
    }
}
