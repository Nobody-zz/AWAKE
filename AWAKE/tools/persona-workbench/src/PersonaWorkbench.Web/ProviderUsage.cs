using System.Text.Json;
namespace PersonaWorkbench.Web;
public sealed record ProviderUsage(int PromptTokens, int CompletionTokens, int TotalTokens)
{
    public static ProviderUsage? TryParse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (root.TryGetProperty("usage", out JsonElement usage) && usage.ValueKind == JsonValueKind.Object)
        {
            if (!TryRead(usage, "prompt_tokens", out int promptTokens) || !TryRead(usage, "completion_tokens", out int completionTokens) || !TryRead(usage, "total_tokens", out int totalTokens)) return null;
            long sum = (long)promptTokens + completionTokens;
            return sum <= int.MaxValue && totalTokens == sum ? new ProviderUsage(promptTokens, completionTokens, totalTokens) : null;
        }
        if (!TryRead(root, "prompt_eval_count", out int promptEvalCount) || !TryRead(root, "eval_count", out int evalCount)) return null;
        long nativeSum = (long)promptEvalCount + evalCount;
        return nativeSum <= int.MaxValue ? new ProviderUsage(promptEvalCount, evalCount, (int)nativeSum) : null;
    }
    private static bool TryRead(JsonElement obj, string name, out int value)
    {
        value=0; if (!obj.TryGetProperty(name,out JsonElement e) || e.ValueKind != JsonValueKind.Number) return false;
        return e.TryGetInt32(out value) && value >= 0;
    }
}
