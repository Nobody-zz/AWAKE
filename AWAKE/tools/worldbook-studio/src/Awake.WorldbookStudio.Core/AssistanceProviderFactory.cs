namespace Awake.WorldbookStudio.Core;

public static class AssistanceProviderFactory
{
    public static IAssistanceProvider Create(string providerId, ProviderConfiguration? configuration = null)
    {
        var settings = configuration ?? ProviderConfiguration.FromEnvironment();
        return providerId.Trim().ToLowerInvariant() switch
        {
            "cloud" => new OpenAICompatibleCloudProvider(settings),
            "local" => new LocalWorkerProvider(settings),
            _ => throw new InvalidOperationException("WB-AI-PROVIDER-400: 未知 AI Provider。")
        };
    }
}