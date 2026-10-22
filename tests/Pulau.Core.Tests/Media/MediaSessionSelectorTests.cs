using Microsoft.Extensions.Time.Testing;
using Pulau.Core.Media;

namespace Pulau.Core.Tests.Media;

public class MediaSessionSelectorTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly MediaSessionSelector _selector;
    private int _changes;

    public MediaSessionSelectorTests()
    {
        _selector = new MediaSessionSelector(_time);
        _selector.ActiveSessionChanged += (_, _) => _changes++;
    }

    [Fact]
    public void PicksAPlayingSession()
    {
        _selector.Update("Spotify.exe", isPlaying: true);

        Assert.Equal("Spotify.exe", _selector.ActiveSessionId);
        Assert.Equal(1, _changes);
    }

    [Fact]
    public void IgnoresSessionsThatNeverPlayed()
    {
        // E.g. a paused YouTube tab that was already open when Pulau started.
        _selector.Update("chrome", isPlaying: false);

        Assert.Null(_selector.ActiveSessionId);
        Assert.Equal(0, _changes);
    }

    [Fact]
    public void PrefersPlayingOverPaused()
    {
        _selector.Update("Spotify.exe", true);
        _selector.Update("chrome", true);
        _selector.Update("chrome", false);

        Assert.Equal("Spotify.exe", _selector.ActiveSessionId);
    }

    [Fact]
    public void AmongPlayingSessions_PicksTheOneThatStartedMostRecently()
    {
        _selector.Update("Spotify.exe", true);
        _time.Advance(TimeSpan.FromSeconds(10));
        _selector.Update("chrome", true);

        Assert.Equal("chrome", _selector.ActiveSessionId);

        // Further updates without a state change don't steal focus back.
        _selector.Update("Spotify.exe", true);
        Assert.Equal("chrome", _selector.ActiveSessionId);
    }

    [Fact]
    public void PausedSession_StaysForFiveMinutes_ThenDisappears()
    {
        _selector.Update("Spotify.exe", true);
        _time.Advance(TimeSpan.FromMinutes(1));
        _selector.Update("Spotify.exe", false);

        _time.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        Assert.Equal("Spotify.exe", _selector.ActiveSessionId);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(_selector.ActiveSessionId);
    }

    [Fact]
    public void ResumingWithinTheTimeout_KeepsTheSession()
    {
        _selector.Update("Spotify.exe", true);
        _selector.Update("Spotify.exe", false);
        _time.Advance(TimeSpan.FromMinutes(4));

        _selector.Update("Spotify.exe", true);
        _time.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal("Spotify.exe", _selector.ActiveSessionId);
    }

    [Fact]
    public void WhenNothingPlays_FallsBackToTheMostRecentlyPaused()
    {
        _selector.Update("Spotify.exe", true);
        _selector.Update("chrome", true);
        _selector.Update("Spotify.exe", false);
        _time.Advance(TimeSpan.FromSeconds(30));

        _selector.Update("chrome", false);

        Assert.Equal("chrome", _selector.ActiveSessionId);

        // Spotify paused 30 s earlier, so chrome (paused later) keeps the island longer.
        _time.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        Assert.Equal("chrome", _selector.ActiveSessionId);
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(_selector.ActiveSessionId);
    }

    [Fact]
    public void ClosingTheActiveSession_SwitchesToTheNextBest()
    {
        _selector.Update("Spotify.exe", true);
        _selector.Update("chrome", true);

        _selector.Remove("chrome");
        Assert.Equal("Spotify.exe", _selector.ActiveSessionId);

        _selector.Remove("Spotify.exe");
        Assert.Null(_selector.ActiveSessionId);
    }

    [Fact]
    public void RemovingUnknownSession_DoesNothing()
    {
        _selector.Remove("nothing");

        Assert.Null(_selector.ActiveSessionId);
        Assert.Equal(0, _changes);
    }

    [Fact]
    public void RaisesChanged_OnlyWhenTheActiveSessionChanges()
    {
        _selector.Update("Spotify.exe", true);
        _selector.Update("Spotify.exe", true);
        _selector.Update("Spotify.exe", false);

        Assert.Equal(1, _changes);
    }
}
