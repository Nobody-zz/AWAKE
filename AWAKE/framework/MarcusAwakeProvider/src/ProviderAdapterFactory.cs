using System;
using System.Net.Http;

namespace MarcusAwakeProvider;

public static class ProviderKindCodec
{
    public static bool TryParseWireName(string? value, out ProviderKind kind)
    {
        switch (value)
        {
            case "openai_compatible":
                kind = ProviderKind.OpenAiCompatible;
                return true;
            case "anthropic":
                kind = ProviderKind.Anthropic;
                return true;
            case "ollama":
                kind = ProviderKind.Ollama;
                return true;
            case "player2":
                kind = ProviderKind.Player2;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    public static string ToWireName(ProviderKind kind)
    {
        return kind switch
        {
            ProviderKind.OpenAiCompatible => "openai_compatible",
            ProviderKind.Anthropic => "anthropic",
            ProviderKind.Ollama => "ollama",
            ProviderKind.Player2 => "player2",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}

public static class ProviderAdapterFactory
{
    public static IProviderAdapter Create(
        ProviderConnectionProfile profile,
        HttpMessageInvoker invoker,
        IProviderEndpointPolicy? endpointPolicy = null,
        ProviderLimits? limits = null,
        IProviderClock? clock = null)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (invoker == null) throw new ArgumentNullException(nameof(invoker));

        return profile.Kind switch
        {
            ProviderKind.OpenAiCompatible => new OpenAiCompatibleProvider(profile, invoker, endpointPolicy, limits, clock),
            ProviderKind.Anthropic => new AnthropicProvider(profile, invoker, endpointPolicy, limits, clock),
            ProviderKind.Ollama => new OllamaProvider(profile, invoker, endpointPolicy, limits, clock),
            ProviderKind.Player2 => new Player2Provider(profile, invoker, endpointPolicy, limits, clock),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile.Kind, "Unsupported Provider kind.")
        };
    }

    /// <summary>
    /// 同 <see cref="Create"/>，但只在适配器真会生图时才返回它。
    /// **不抛错**：路由靠这个过滤候选，遇到不会生图的适配器是正常情况，不是异常。
    /// </summary>
    public static IProviderImageAdapter? CreateImage(
        ProviderConnectionProfile profile,
        HttpMessageInvoker invoker,
        IProviderEndpointPolicy? endpointPolicy = null,
        ProviderLimits? limits = null,
        IProviderClock? clock = null,
        int maxImageAssetBytes = ProviderImageMedia.DefaultMaxAssetBytes)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        return profile.Kind switch
        {
            ProviderKind.Player2 => new Player2Provider(profile, invoker, endpointPolicy, limits, clock, maxImageAssetBytes),
            ProviderKind.OpenAiCompatible => new OpenAiCompatibleProvider(profile, invoker, endpointPolicy, limits, clock, maxImageAssetBytes),
            _ => null
        };
    }
}
