namespace Awake.WorldbookStudio.Core;

internal sealed class BatchWorkspaceWriteLease : IDisposable
{
    private readonly Mutex _mutex;
    private bool _ownsMutex;

    private BatchWorkspaceWriteLease(Mutex mutex, bool ownsMutex)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
    }

    public static BatchWorkspaceWriteLease Acquire(string batchRoot)
    {
        var name = "Local\\AWAKE.WorldbookStudio.Batch." + Hashing.Sha256Text(batchRoot)[..24];
        var mutex = new Mutex(false, name);
        try
        {
            var ownsMutex = false;
            try
            {
                ownsMutex = mutex.WaitOne(TimeSpan.FromSeconds(30));
            }
            catch (AbandonedMutexException)
            {
                ownsMutex = true;
            }

            if (!ownsMutex)
                throw new InvalidOperationException("WB-BATCH-WRITE-409: 该批次正在由另一个工作室实例写入。");
            return new BatchWorkspaceWriteLease(mutex, true);
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (!_ownsMutex) return;
        _ownsMutex = false;
        try { _mutex.ReleaseMutex(); }
        finally { _mutex.Dispose(); }
    }
}
