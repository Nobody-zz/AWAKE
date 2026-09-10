namespace Awake;

internal static class KnowledgeRuntime
{
    private static KnowledgeService _current;

    internal static KnowledgeService Current => _current;

    internal static void SetForOfflineTest(KnowledgeService service)
    {
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
