using System;
using Newtonsoft.Json.Linq;

namespace Awake;

internal static class AwakeEventEffectRules
{
    internal static bool ShouldApply(AwakeEventEffect effect, string choice)
    {
        return effect != null
            && !string.IsNullOrWhiteSpace(effect.TargetId)
            && StringComparer.Ordinal.Equals(effect.Choice, choice);
    }

    internal static JObject BuildRelationshipArgs(
        string targetId,
        AwakeEventEffect effect,
        string fallbackReason = null)
    {
        if (string.IsNullOrWhiteSpace(targetId)
            || effect == null
            || effect.TrustDelta < -100
            || effect.TrustDelta > 100
            || effect.LoveDelta < -100
            || effect.LoveDelta > 100
            || effect.HostilityDelta < -100
            || effect.HostilityDelta > 100
            || (effect.TrustDelta == 0 && effect.LoveDelta == 0 && effect.HostilityDelta == 0))
        {
            return null;
        }
        string reason = string.IsNullOrWhiteSpace(effect.Reason)
            ? fallbackReason ?? string.Empty
            : effect.Reason;
        return new JObject
        {
            ["heroId"] = targetId,
            ["trustDelta"] = effect.TrustDelta,
            ["loveDelta"] = effect.LoveDelta,
            ["hostilityDelta"] = effect.HostilityDelta,
            ["reason"] = reason ?? string.Empty
        };
    }
}
