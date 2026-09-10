using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

public static class WorldbookInputNormalization
{
    public static string NormalizeProvider(string? providerId)
    {
        var value = providerId?.Trim().ToLowerInvariant();
        if (value is not ("local" or "cloud")) throw new InvalidOperationException("WB-AI-PROVIDER-400: Provider 必须是 local 或 cloud。");
        return value;
    }

    public static int ReadRevision(JsonObject document)
        => document["revision"] is null ? 1 : checked((int)document["revision"]!.GetValue<long>());
}
