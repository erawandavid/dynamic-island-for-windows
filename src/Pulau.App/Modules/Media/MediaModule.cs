using System.Windows.Threading;
using Pulau.Core.Activities;
using Pulau.Core.Layout;
using Pulau.Core.Media;
using Serilog;
using Windows.Media.Control;
using Windows.Storage.Streams;
using WindowsMediaController;

namespace Pulau.Modules.Media;

/// <summary>
/// Shows what is playing in any app that uses the Windows media controls (Spotify, browsers,
/// media players…), through the System Media Transport Controls (SMTC).
/// <list type="bullet">
/// <item>Playing → persistent activity (compact: artwork + equalizer; expanded: controls and progress).</item>
/// <item>Paused → stays for five minutes, then disappears. Session closed → disappears.</item>
/// <item>Track change while playing → auto-peek.</item>
/// </list>
/// SMTC events arrive on background threads; every one is handed to the UI thread first.
/// </summary>
public sealed class MediaModule : IIslandModule, IDisposable
{
    public const string ModuleId = "media";

    private static readonly TimeSpan ArtTimeout = TimeSpan.FromSeconds(3);
    private static readonly ILogger Logger = Log.ForContext<MediaModule>();

    private readonly Dispatcher _dispatcher;
    private readonly TimeProvider _clock;
    private readonly MediaSessionSelector _selector;
    private readonly Dictionary<string, SessionState> _sessions = new(StringComparer.Ordinal);
    private readonly NowPlayingViewModel _nowPlaying;
    private readonly MediaCompactViewModel _compact;
    private readonly MediaExpandedViewModel _expanded;

    private MediaManager? _manager;
    private string? _shownSessionId;
    private string? _shownTrackKey;
    private string? _artTrackKey;
    private int _artVersion;
    private volatile bool _stopped;

    public MediaModule(Dispatcher dispatcher, TimeProvider clock)
    {
        _dispatcher = dispatcher;
        _clock = clock;
        _selector = new MediaSessionSelector(clock);
        _selector.ActiveSessionChanged += (_, _) => OnActiveSessionChanged();
        _nowPlaying = new NowPlayingViewModel(clock, SendCommandAsync);
        _compact = new MediaCompactViewModel(_nowPlaying);
        _expanded = new MediaExpandedViewModel(_nowPlaying);
    }

    public event EventHandler<IslandActivity?>? ActivityChanged;

    public string Id => ModuleId;

    public string DisplayName => "Media";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var manager = new MediaManager();
        manager.OnAnySessionOpened += session => Post(() => OnSessionOpened(session));
        manager.OnAnySessionClosed += session => Post(() => OnSessionClosed(session.Id));
        manager.OnAnyPlaybackStateChanged += (session, playback) => Post(() => OnPlaybackChanged(session, playback));
        manager.OnAnyMediaPropertyChanged += (session, properties) => Post(() => OnMediaPropertiesChanged(session, properties));
        manager.OnAnyTimelinePropertyChanged += (session, timeline) => Post(() => OnTimelineChanged(session, timeline));
        _manager = manager;

        await manager.StartAsync();
        Logger.Information("Listening to Windows media sessions");
    }

    public Task StopAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        var manager = _manager;
        _manager = null;
        if (manager?.IsStarted == true)
        {
            try
            {
                manager.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error while closing media sessions");
            }
        }

        _nowPlaying.StopProgressUpdates();
        _selector.Dispose();
        _sessions.Clear();
    }

    private static void ApplyPlayback(SessionState state, GlobalSystemMediaTransportControlsSessionPlaybackInfo? playback)
    {
        if (playback is null)
        {
            return;
        }

        var controls = playback.Controls;
        state.IsPlaying = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        state.CanToggle = controls.IsPlayPauseToggleEnabled;
        state.CanPlayPause = controls.IsPlayPauseToggleEnabled || (state.IsPlaying ? controls.IsPauseEnabled : controls.IsPlayEnabled);
        state.CanPrevious = controls.IsPreviousEnabled;
        state.CanNext = controls.IsNextEnabled;
        state.PlaybackRate = playback.PlaybackRate ?? 1;
    }

    /// <summary>Runs <paramref name="action"/> on the UI thread; exceptions are logged, never thrown.</summary>
    private void Post(Action action)
    {
        if (_stopped)
        {
            return;
        }

        _dispatcher.InvokeAsync(() =>
        {
            if (_stopped)
            {
                return;
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Handling a media session update failed");
            }
        });
    }

    private SessionState GetState(MediaManager.MediaSession session)
    {
        if (!_sessions.TryGetValue(session.Id, out var state))
        {
            state = new SessionState(session);
            _sessions[session.Id] = state;
        }

        // A session that closed and reopened comes back as a new wrapper object.
        state.Session = session;
        return state;
    }

    private void OnSessionOpened(MediaManager.MediaSession session)
    {
        var state = GetState(session);
        Logger.Information("Media session opened: {App}", session.Id);

        // The wrapper reports properties and timeline for new sessions, but not the current playback state.
        ApplyPlayback(state, session.ControlSession?.GetPlaybackInfo());
        _selector.Update(session.Id, state.IsPlaying);
        if (session.Id == _shownSessionId)
        {
            PushPlayback(state);
        }
    }

    private void OnSessionClosed(string sessionId)
    {
        if (_sessions.Remove(sessionId))
        {
            Logger.Information("Media session closed: {App}", sessionId);
        }

        _selector.Remove(sessionId);
    }

    private void OnPlaybackChanged(MediaManager.MediaSession session, GlobalSystemMediaTransportControlsSessionPlaybackInfo playback)
    {
        var state = GetState(session);
        ApplyPlayback(state, playback);
        _selector.Update(session.Id, state.IsPlaying);
        if (session.Id == _shownSessionId)
        {
            PushPlayback(state);
        }
    }

    private void OnMediaPropertiesChanged(MediaManager.MediaSession session, GlobalSystemMediaTransportControlsSessionMediaProperties properties)
    {
        var state = GetState(session);
        state.Title = properties.Title?.Trim() ?? string.Empty;
        state.Artist = FirstNonEmpty(properties.Artist, properties.AlbumArtist);
        state.Thumbnail = properties.Thumbnail;

        if (session.Id != _shownSessionId)
        {
            return;
        }

        PushTrack(state);
        _ = LoadArtAsync(state.Thumbnail, state.TrackKey);

        // Auto-peek when the song changes while playing; not for the first track after switching sessions.
        if (string.IsNullOrEmpty(state.Title) || state.TrackKey == _shownTrackKey)
        {
            return;
        }

        var isNewTrack = _shownTrackKey is not null;
        _shownTrackKey = state.TrackKey;
        if (isNewTrack && state.IsPlaying)
        {
            Logger.Debug("Track changed in {App}", session.Id);
            Publish(requestPeek: true);
        }
    }

    private void OnTimelineChanged(MediaManager.MediaSession session, GlobalSystemMediaTransportControlsSessionTimelineProperties timeline)
    {
        var state = GetState(session);
        var duration = timeline.EndTime - timeline.StartTime;
        var position = timeline.Position - timeline.StartTime;
        state.Timeline = new TimelineSnapshot(
            position < TimeSpan.Zero ? TimeSpan.Zero : position,
            duration < TimeSpan.Zero ? TimeSpan.Zero : duration,
            timeline.LastUpdatedTime,
            _clock.GetUtcNow());

        if (session.Id == _shownSessionId)
        {
            _nowPlaying.SetTimeline(state.Timeline);
        }
    }

    private void OnActiveSessionChanged()
    {
        var sessionId = _selector.ActiveSessionId;
        if (sessionId is null || !_sessions.TryGetValue(sessionId, out var state))
        {
            _shownSessionId = null;
            _shownTrackKey = null;
            _artVersion++;
            Logger.Debug("No media to show");
            ActivityChanged?.Invoke(this, null);
            return;
        }

        _shownSessionId = sessionId;
        _shownTrackKey = string.IsNullOrEmpty(state.Title) ? null : state.TrackKey;
        PushTrack(state);
        PushPlayback(state);
        _nowPlaying.SetTimeline(state.Timeline);
        _ = LoadArtAsync(state.Thumbnail, state.TrackKey);
        Logger.Information("Showing media from {App}", sessionId);
        Publish(requestPeek: false);
    }

    private void Publish(bool requestPeek) =>
        ActivityChanged?.Invoke(this, new IslandActivity(
            ModuleId,
            ActivityPriority.Normal,
            ActivityKind.Persistent,
            null,
            _compact,
            _expanded)
        {
            CompactSize = IslandSizes.Compact,
            ExpandedSize = IslandSizes.Expanded,
            RequestsPeek = requestPeek,
        });

    private void PushTrack(SessionState state)
    {
        var source = MediaSourceNames.FromAppId(state.Session.Id);
        _nowPlaying.SetTrack(string.IsNullOrEmpty(state.Title) ? source : state.Title, state.Artist, source);
    }

    private void PushPlayback(SessionState state) =>
        _nowPlaying.SetPlayback(state.IsPlaying, state.CanPrevious, state.CanPlayPause, state.CanNext, state.PlaybackRate);

    private async Task LoadArtAsync(IRandomAccessStreamReference? thumbnail, string trackKey)
    {
        var version = ++_artVersion;
        if (thumbnail is null)
        {
            // Some apps send the artwork a moment after the title; keep the current art for the same track.
            if (trackKey != _artTrackKey)
            {
                _artTrackKey = trackKey;
                _nowPlaying.SetArt(null, AccentColor.Fallback);
            }

            return;
        }

        try
        {
            var art = await AlbumArt.LoadAsync(thumbnail, ArtTimeout);
            if (version != _artVersion || _stopped)
            {
                return;
            }

            _artTrackKey = trackKey;
            _nowPlaying.SetArt(art?.Image, art?.Accent ?? AccentColor.Fallback);
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Could not load the artwork");
            if (version == _artVersion && !_stopped)
            {
                _artTrackKey = trackKey;
                _nowPlaying.SetArt(null, AccentColor.Fallback);
            }
        }
    }

    private async Task SendCommandAsync(MediaCommand command)
    {
        if (_shownSessionId is not { } sessionId || !_sessions.TryGetValue(sessionId, out var state))
        {
            return;
        }

        var control = state.Session.ControlSession;
        if (control is null)
        {
            return;
        }

        try
        {
            var accepted = command switch
            {
                MediaCommand.Previous => await control.TrySkipPreviousAsync(),
                MediaCommand.Next => await control.TrySkipNextAsync(),
                _ when state.CanToggle => await control.TryTogglePlayPauseAsync(),
                _ when state.IsPlaying => await control.TryPauseAsync(),
                _ => await control.TryPlayAsync(),
            };

            if (!accepted)
            {
                Logger.Information("{App} did not accept {Command}", sessionId, command);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Sending {Command} to {App} failed", command, sessionId);
        }
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    private sealed class SessionState(MediaManager.MediaSession session)
    {
        public MediaManager.MediaSession Session { get; set; } = session;

        public string Title { get; set; } = string.Empty;

        public string Artist { get; set; } = string.Empty;

        public IRandomAccessStreamReference? Thumbnail { get; set; }

        public bool IsPlaying { get; set; }

        public bool CanToggle { get; set; }

        public bool CanPlayPause { get; set; }

        public bool CanPrevious { get; set; }

        public bool CanNext { get; set; }

        public double PlaybackRate { get; set; } = 1;

        public TimelineSnapshot? Timeline { get; set; }

        public string TrackKey => $"{Title}\u001F{Artist}";
    }
}
