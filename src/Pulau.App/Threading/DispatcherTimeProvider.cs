using System.Windows.Threading;

namespace Pulau.Threading;

/// <summary>
/// System time whose timers fire on the WPF dispatcher (UI) thread. Core components such as the
/// state machine and the activity manager use <see cref="TimeProvider"/> timers; with this provider
/// their callbacks run on the UI thread, so they need no locking.
/// </summary>
internal sealed class DispatcherTimeProvider(Dispatcher dispatcher) : TimeProvider
{
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        new DispatcherTimerAdapter(dispatcher, callback, state, dueTime, period);

    private sealed class DispatcherTimerAdapter : ITimer
    {
        private readonly DispatcherTimer _timer;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private TimeSpan _period;
        private bool _disposed;

        public DispatcherTimerAdapter(Dispatcher dispatcher, TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _callback = callback;
            _state = state;
            _timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher);
            _timer.Tick += OnTick;
            Change(dueTime, period);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (_disposed)
            {
                return false;
            }

            _timer.Stop();
            _period = period;
            if (dueTime == Timeout.InfiniteTimeSpan)
            {
                return true;
            }

            _timer.Interval = dueTime < TimeSpan.Zero ? TimeSpan.Zero : dueTime;
            _timer.Start();
            return true;
        }

        public void Dispose()
        {
            _disposed = true;
            _timer.Stop();
            _timer.Tick -= OnTick;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            if (_period > TimeSpan.Zero && _period != Timeout.InfiniteTimeSpan)
            {
                _timer.Interval = _period;
            }
            else
            {
                _timer.Stop();
            }

            _callback(_state);
        }
    }
}
