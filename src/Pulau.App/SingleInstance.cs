namespace Pulau;

/// <summary>Holds a per-session named mutex so only one Pulau runs per signed-in user.</summary>
internal sealed class SingleInstance : IDisposable
{
    // The "Local" namespace scopes the mutex to the current Windows session; the GUID keeps the name unique to Pulau.
    private const string MutexName = @"Local\Pulau.SingleInstance.0678FB8B-7CE7-4C64-9C8B-0B0B3BF03457";

    private readonly Mutex _mutex;

    private SingleInstance(Mutex mutex) => _mutex = mutex;

    /// <summary>Returns the instance lock, or <c>null</c> if another Pulau already holds it.</summary>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew)
        {
            return new SingleInstance(mutex);
        }

        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
