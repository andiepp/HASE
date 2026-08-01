namespace Hase.DesktopHost.App.Hosting;

public sealed class DesktopRuntimeHostSingleInstanceLease : IDisposable
{
    private static readonly object ActiveNamesSyncRoot = new();
    private static readonly HashSet<string> ActiveNames =
        new(StringComparer.Ordinal);

    private readonly Mutex mutex;
    private readonly string mutexName;
    private bool disposed;

    private DesktopRuntimeHostSingleInstanceLease(
        Mutex mutex,
        string mutexName)
    {
        this.mutex = mutex;
        this.mutexName = mutexName;
    }

    public static DesktopRuntimeHostSingleInstanceLease? TryAcquire(
        string mutexName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);

        lock (ActiveNamesSyncRoot)
        {
            if (!ActiveNames.Add(mutexName))
            {
                return null;
            }
        }

        var mutex = new Mutex(
            initiallyOwned: false,
            mutexName);
        bool acquired =
            false;

        try
        {
            try
            {
                acquired = mutex.WaitOne(
                    millisecondsTimeout: 0,
                    exitContext: false);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }

            if (!acquired)
            {
                mutex.Dispose();
                return null;
            }

            return new DesktopRuntimeHostSingleInstanceLease(
                mutex,
                mutexName);
        }
        finally
        {
            if (!acquired)
            {
                lock (ActiveNamesSyncRoot)
                {
                    ActiveNames.Remove(mutexName);
                }
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            mutex.ReleaseMutex();
        }
        finally
        {
            mutex.Dispose();

            lock (ActiveNamesSyncRoot)
            {
                ActiveNames.Remove(mutexName);
            }
        }
    }
}
