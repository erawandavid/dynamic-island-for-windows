using Pulau.Core.Media;

namespace Pulau.Core.Tests.Media;

public class PlaybackTimelineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static TimelineSnapshot Snapshot(double positionSeconds, double durationSeconds = 200, DateTimeOffset? reportedAt = null) =>
        new(TimeSpan.FromSeconds(positionSeconds), TimeSpan.FromSeconds(durationSeconds), reportedAt ?? T0, T0);

    [Fact]
    public void AdvancesWhilePlaying()
    {
        var position = PlaybackTimeline.Interpolate(Snapshot(30), T0.AddSeconds(12), isPlaying: true);

        Assert.Equal(TimeSpan.FromSeconds(42), position);
    }

    [Fact]
    public void StaysPutWhilePaused()
    {
        var position = PlaybackTimeline.Interpolate(Snapshot(30), T0.AddSeconds(12), isPlaying: false);

        Assert.Equal(TimeSpan.FromSeconds(30), position);
    }

    [Fact]
    public void HonorsPlaybackRate()
    {
        var position = PlaybackTimeline.Interpolate(Snapshot(10), T0.AddSeconds(10), isPlaying: true, playbackRate: 1.5);

        Assert.Equal(TimeSpan.FromSeconds(25), position);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void InvalidPlaybackRate_CountsAsNormalSpeed(double rate)
    {
        var position = PlaybackTimeline.Interpolate(Snapshot(10), T0.AddSeconds(10), isPlaying: true, playbackRate: rate);

        Assert.Equal(TimeSpan.FromSeconds(20), position);
    }

    [Fact]
    public void ClampsToTheEndOfTheTrack()
    {
        var position = PlaybackTimeline.Interpolate(Snapshot(190, durationSeconds: 200), T0.AddMinutes(5), isPlaying: true);

        Assert.Equal(TimeSpan.FromSeconds(200), position);
    }

    [Fact]
    public void UsesReceiveTime_WhenReportedTimeIsImplausible()
    {
        // Some apps leave the timestamp at its default or let it go stale.
        var stale = Snapshot(30, reportedAt: DateTimeOffset.FromUnixTimeSeconds(0));
        var future = Snapshot(30, reportedAt: T0.AddMinutes(10));

        Assert.Equal(TimeSpan.FromSeconds(35), PlaybackTimeline.Interpolate(stale, T0.AddSeconds(5), true));
        Assert.Equal(TimeSpan.FromSeconds(35), PlaybackTimeline.Interpolate(future, T0.AddSeconds(5), true));
    }

    [Fact]
    public void UsesReportedTime_WhenPlausible()
    {
        // Reported 2 s before we received it: 2 s of playback already happened.
        var snapshot = Snapshot(30, reportedAt: T0.AddSeconds(-2));

        Assert.Equal(TimeSpan.FromSeconds(37), PlaybackTimeline.Interpolate(snapshot, T0.AddSeconds(5), true));
    }

    [Fact]
    public void WithoutTimeline_DoesNotClamp()
    {
        var snapshot = Snapshot(30, durationSeconds: 0);

        Assert.False(snapshot.HasTimeline);
        Assert.Equal(TimeSpan.FromSeconds(40), PlaybackTimeline.Interpolate(snapshot, T0.AddSeconds(10), true));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(7, "0:07")]
    [InlineData(187, "3:07")]
    [InlineData(3599, "59:59")]
    [InlineData(3723, "1:02:03")]
    [InlineData(-5, "0:00")]
    public void FormatsLikeAMediaPlayer(int seconds, string expected)
    {
        Assert.Equal(expected, PlaybackTimeline.Format(TimeSpan.FromSeconds(seconds)));
    }
}
