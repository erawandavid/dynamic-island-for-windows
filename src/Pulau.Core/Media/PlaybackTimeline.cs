namespace Pulau.Core.Media;

/// <summary>A timeline update as reported by a media app.</summary>
/// <param name="Position">Position within the track (relative to its start).</param>
/// <param name="Duration">Track length; zero when the app doesn't provide a timeline.</param>
/// <param name="ReportedAt">When the app says the position was measured.</param>
/// <param name="ReceivedAt">When Pulau received the update.</param>
public readonly record struct TimelineSnapshot(
    TimeSpan Position,
    TimeSpan Duration,
    DateTimeOffset ReportedAt,
    DateTimeOffset ReceivedAt)
{
    public bool HasTimeline => Duration > TimeSpan.Zero;
}

/// <summary>
/// Apps only report the playback position now and then (on play, pause, seek, track change),
/// so the island advances it locally in between.
/// </summary>
public static class PlaybackTimeline
{
    /// <summary>
    /// A reported time this far from the moment we received it is not trusted (some apps leave it at
    /// zero or let it go stale); the receive time is used instead.
    /// </summary>
    public static readonly TimeSpan MaxReportedSkew = TimeSpan.FromHours(1);

    public static TimeSpan Interpolate(TimelineSnapshot snapshot, DateTimeOffset now, bool isPlaying, double playbackRate = 1)
    {
        var position = snapshot.Position;
        if (isPlaying)
        {
            var reportedAt = snapshot.ReportedAt;
            if (reportedAt > snapshot.ReceivedAt || snapshot.ReceivedAt - reportedAt > MaxReportedSkew)
            {
                reportedAt = snapshot.ReceivedAt;
            }

            var elapsed = now - reportedAt;
            if (elapsed > TimeSpan.Zero)
            {
                var rate = playbackRate > 0 && double.IsFinite(playbackRate) ? playbackRate : 1;
                position += TimeSpan.FromTicks((long)(elapsed.Ticks * rate));
            }
        }

        if (position < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return snapshot.HasTimeline && position > snapshot.Duration ? snapshot.Duration : position;
    }

    /// <summary>Formats a position like a media player: 3:07, or 1:02:03 for long media.</summary>
    public static string Format(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";
    }
}
