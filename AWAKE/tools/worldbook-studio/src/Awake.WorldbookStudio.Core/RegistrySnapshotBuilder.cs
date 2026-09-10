using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal static class RegistrySnapshotBuilder
{
    internal static RegistrySnapshot Build(JsonObject profiles, JsonObject referrals, byte[] profileBytes, byte[] referralBytes, ValidationReport report)
    {
        var result = new RegistrySnapshot
        {
            ProfileRegistry = profiles,
            ReferralRegistry = referrals,
            ProfileVersion = profiles["registry_version"]?.GetValue<string>() ?? "",
            ReferralVersion = referrals["registry_version"]?.GetValue<string>() ?? "",
            ProfileHash = Hashing.Sha256Bytes(profileBytes),
            ReferralHash = Hashing.Sha256Bytes(referralBytes)
        };
        var profileIds = new List<string>();
        foreach (var profile in profiles["profiles"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            var id = profile["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (result.Profiles.Add(id)) profileIds.Add(id);
            result.ProfileParents[id] = profile["inherits"]?.GetValue<string>();
            result.ProfileObjects[id] = profile;
        }
        foreach (var referral in referrals["referrals"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            var id = referral["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id)) continue;
            result.Referrals.Add(id);
            if (referral["publicly_askable"]?.GetValue<bool>() == true) result.PubliclyAskableReferrals.Add(id);
        }
        foreach (var id in profileIds)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { id };
            var parent = result.ProfileParents[id];
            while (!string.IsNullOrWhiteSpace(parent))
            {
                if (!result.Profiles.Contains(parent))
                {
                    report.Error("WB-PROFILE-001", "profile 继承链包含未知 profile。", id, parent);
                    break;
                }
                if (!seen.Add(parent))
                {
                    report.Error("WB-PROFILE-001", "profile 继承链存在循环。", id, string.Join(" -> ", seen));
                    break;
                }
                parent = result.ProfileParents[parent];
            }
        }
        return result;
    }
}
