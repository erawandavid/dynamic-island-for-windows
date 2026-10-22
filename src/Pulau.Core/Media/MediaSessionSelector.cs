using Pulau.Core.Timing;

namespace Pulau.Core.Media;

/// <summary>
/// Chooses which media session the island follows when several apps expose one (PLAN.md Phase 3):
/// <list type="number">
/// <item>a playing session wins; among several, the one that started playing most recently;</item>
/// <item>otherwise the most recently paused session, for <see cref="PausedTimeout"/> after it paused;</item>
/// <item>otherwise none. A session that has not played since Pulau started is never chosen,
/// so a forgotten paused tab doesn't pop up on startup.</item>
/// </list>
/// Not thread-safe: use it from one thread, with a TimeProvider that calls back on that thread.
/// </summary>
public sealed class MediaSessionSelector : IDisposable
{
    public static readonly TimeSpan DefaultPausedTimeout = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _time;
    private readonly Dictionary<string, SessionInfo> _sessions = new(StringComparer.Ordinal);
    private readonly OneShotTimer _pausedExpiry;

    public MediaSessionSelector(TimeProvider time)
        : this(time, DefaultPausedTimeout)
    {
    }

    public MediaSessionSelector(TimeProvider time, TimeSpan pausedTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pausedTimeout, TimeSpan.Zero);
        _time = time;
        PausedTimeout = pausedTimeout;
        _pausedExpiry = new OneShotTimer(time, Reselect);
    }

    /// <summary>The chosen session changed (including to or from <c>null</c>).</summary>
    public event EventHandler? ActiveSessionChanged;

    public TimeSpan PausedTimeout { get; }

    /// <summary>The session to show, or <c>null</c> for no media activity.</summary>
    public string? ActiveSessionId { get; private set; }

    /// <summary>Reports a session and whether it is playing right now.</summary>
    public void Update(string sessionId, bool isPlaying)
    {
        var now = _time.GetUtcNow();
        if (!_sessions.TryGetValue(sessionId, out var info))
        {
            _sessions[sessionId] = new SessionInfo(isPlaying, now, everPlayed: isPlaying);
        }
        else if (info.IsPlaying != isPlaying)
        {
            info.IsPlaying = isPlaying;
            info.Since = now;
            info.EverPlayed |= isPlaying;
        }

        Reselect();
    }

    public void Remove(string sessionId)
    {
        if (_sessions.Remove(sessionId))
        {
            Reselect();
        }
    }

    public void Dispose() => _pausedExpiry.Dispose();

    private void Reselect()
    {
        var now = _time.GetUtcNow();
        string? best = null;
        var bestSince = DateTimeOffset.MinValue;

        foreach (var (id, info) in _sessions)
        {
            if (info.IsPlaying && (best is null || info.Since > bestSince))
            {
                best = id;
                bestSince = info.Since;
            }
        }

        if (best is not null)
        {
            _pausedExpiry.Cancel();
        }
        else
        {
            foreach (var (id, info) in _sessions)
            {
                if (info.EverPlayed && now - info.Since < PausedTimeout && (best is null || info.Since > bestSince))
                {
                    best = id;
                    bestSince = info.Since;
                }
            }

            if (best is not null)
            {
                _pausedExpiry.Start(bestSince + PausedTimeout - now);
            }
            else
            {
                _pausedExpiry.Cancel();
            }
        }

        if (!string.Equals(best, ActiveSessionId, StringComparison.Ordinal))
        {
            ActiveSessionId = best;
            ActiveSessionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class SessionInfo(bool isPlaying, DateTimeOffset since, bool everPlayed)
    {
        public bool IsPlaying { get; set; } = isPlaying;

        /// <summary>When the session started playing (if playing) or stopped playing (if not).</summary>
        public DateTimeOffset Since { get; set; } = since;

        public bool EverPlayed { get; set; } = everPlayed;
    }
}
