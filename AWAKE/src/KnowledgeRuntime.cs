using System.Threading.Tasks;
using MarcusAIFramework.Api;

namespace Awake;

internal static class KnowledgeRuntime
{
    private static KnowledgeService _current;

    internal static KnowledgeService Current => _current;

    internal static void EnsureCreated(IMarcusAiFrameworkHost host)
    {
        if (_current != null) return;
        KnowledgeService service = new KnowledgeService(
            host,
            null,
            (permission, purpose) => Task.FromResult(true),
            _ => { });
        service.Initialize();
        _current = service;
    }

    internal static void ShutdownCurrent()
    {
        KnowledgeService service = _current;
        _current = null;
        if (service != null)
        {
            try
            {
                service.Dispose();
            }
            catch
            {
            }
        }
    }
}
