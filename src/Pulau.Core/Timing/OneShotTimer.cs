namespace Pulau.Core.Timing;

/// <summary>
/// A restartable single-shot timer on top of <see cref="TimeProvider"/>. Restarting or
/// cancelling guarantees that an earlier, already-queued expiry is ignored.
/// Not thread-safe: use it from one thread, with a TimeProvider that calls back on that thread.
/// </summary>
internal sealed class OneShotTimer : IDisposable
{
    private readonly TimeProvider _time;
    private readonly Action _onElapsed;
    private ITimer? _timer;
    private long _generation;

    public OneShotTimer(TimeProvider time, Action onElapsed)
    {
        _time = time;
        _onElapsed = onElapsed;
    }

    public bool IsRunning => _timer is not null;

    public void Start(TimeSpan dueTime)
    {
        Cancel();
        var generation = _generation;
        var timer = _time.CreateTimer(_ => Fire(generation), null, dueTime < TimeSpan.Zero ? TimeSpan.Zero : dueTime, Timeout.InfiniteTimeSpan);

        // A zero due time may fire synchronously inside CreateTimer; don't keep a timer that already fired.
        if (generation == _generation)
        {
            _timer = timer;
        }
        else
        {
            timer.Dispose();
        }
    }

    public void Cancel()
    {
        _generation++;
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Cancel();

    private void Fire(long generation)
    {
        if (generation != _generation)
        {
            return;
        }

        _generation++;
        _timer?.Dispose();
        _timer = null;
        _onElapsed();
    }
}
