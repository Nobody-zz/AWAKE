using System.Text;

namespace PersonaWorkbench.Web;

public static class ProviderProtocolKind
{
    public const string Ollama = "ollama";
    public const string OpenAiCompatible = "openai_compatible";

    public static bool IsOllama(string? value) => string.Equals(value, Ollama, StringComparison.Ordinal);

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0) normalized = Ollama;
        return normalized is Ollama or OpenAiCompatible;
    }
}

public static class ProviderRequestBudget
{
    public const int ShortInputBytes = 4096;
    public const int ExpansionComplexInputBytes = 2048;
    public const int SimpleExpansionOutputTokens = 3072;
    public const int ComplexExpansionOutputTokens = 4096;
    public const int SimpleDslOutputTokens = 4096;
    public const int ComplexDslOutputTokens = 6144;
    public const int ExpansionMaximumInputBytes = 16 * 1024;
    public const int DslMaximumInputBytes = 24 * 1024;
    public const int DefaultRequestTimeoutSeconds = 120;

    public static int SelectDslContextTokens(string sourceText)
    {
        return SelectDslContextTokens(Encoding.UTF8.GetByteCount(sourceText ?? string.Empty));
    }

    public static int SelectDslContextTokens(int sourceBytes)
    {
        return sourceBytes <= ShortInputBytes ? 16 * 1024 : 32 * 1024;
    }

    public static bool TryGetOllamaNativeEndpoint(Uri endpoint, out Uri nativeEndpoint)
    {
        nativeEndpoint = endpoint;
        if (!ProviderEndpointPolicy.IsLoopbackEndpoint(endpoint)) return false;
        UriBuilder builder = new UriBuilder(endpoint)
        {
            Path = "/api/chat",
            Query = string.Empty
        };
        nativeEndpoint = builder.Uri;
        return true;
    }

    public static int SelectExpansionTokens(string sourceText)
    {
        return SelectExpansionTokens(Encoding.UTF8.GetByteCount(sourceText ?? string.Empty));
    }

    public static int SelectExpansionTokens(int sourceBytes)
    {
        return sourceBytes <= ExpansionComplexInputBytes ? SimpleExpansionOutputTokens : ComplexExpansionOutputTokens;
    }

    public static int SelectDslTokens(string sourceText)
    {
        return SelectDslTokens(Encoding.UTF8.GetByteCount(sourceText ?? string.Empty));
    }

    public static int SelectDslTokens(int sourceBytes)
    {
        return sourceBytes <= ShortInputBytes ? SimpleDslOutputTokens : ComplexDslOutputTokens;
    }
}
