using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulau.Core.Activities;
using Pulau.Core.Media;

namespace Pulau.Modules.Media;

public enum MediaCommand
{
    Previous,
    PlayPause,
    Next,
}

/// <summary>
/// The media session the island follows: track, artwork, playback state and position.
/// Shared by the compact and the expanded views; <see cref="MediaModule"/> keeps it up to date.
/// </summary>
public sealed class NowPlayingViewModel : ObservableObject
{
    private readonly TimeProvider _clock;
    private readonly DispatcherTimer _progressTimer;
    private readonly AsyncRelayCommand _previousCommand;
    private readonly AsyncRelayCommand _playPauseCommand;
    private readonly AsyncRelayCommand _nextCommand;

    private string _title = string.Empty;
    private string _artist = string.Empty;
    private string _sourceName = string.Empty;
    private ImageSource? _art;
    private Brush _accentBrush = ToBrush(AccentColor.Fallback);
    private bool _isPlaying;
    private bool _canPrevious;
    private bool _canPlayPause;
    private bool _canNext;
    private bool _hasTimeline;
    private double _progress;
    private string _positionText = "0:00";
    private string _durationText = "0:00";
    private TimelineSnapshot? _timeline;
    private double _playbackRate = 1;
    private bool _progressWanted;

    public NowPlayingViewModel(TimeProvider clock, Func<MediaCommand, Task> sendCommand)
    {
        _clock = clock;
        _progressTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(500) };
        _progressTimer.Tick += (_, _) => UpdateProgress();

        _previousCommand = new AsyncRelayCommand(() => sendCommand(MediaCommand.Previous), () => CanPrevious);
        _playPauseCommand = new AsyncRelayCommand(() => sendCommand(MediaCommand.PlayPause), () => CanPlayPause);
        _nextCommand = new AsyncRelayCommand(() => sendCommand(MediaCommand.Next), () => CanNext);
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Artist
    {
        get => _artist;
        private set => SetProperty(ref _artist, value);
    }

    /// <summary>The app playing the media, e.g. "Spotify".</summary>
    public string SourceName
    {
        get => _sourceName;
        private set => SetProperty(ref _sourceName, value);
    }

    public ImageSource? Art
    {
        get => _art;
        private set
        {
            if (SetProperty(ref _art, value))
            {
                OnPropertyChanged(nameof(HasArt));
            }
        }
    }

    public bool HasArt => _art is not null;

    /// <summary>Color picked from the artwork, for the equalizer and the progress bar.</summary>
    public Brush AccentBrush
    {
        get => _accentBrush;
        private set => SetProperty(ref _accentBrush, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(PlayPauseLabel));
            }
        }
    }

    /// <summary>Accessible name of the play/pause button.</summary>
    public string PlayPauseLabel => IsPlaying ? "Pause" : "Play";

    public bool CanPrevious
    {
        get => _canPrevious;
        private set => SetProperty(ref _canPrevious, value);
    }

    public bool CanPlayPause
    {
        get => _canPlayPause;
        private set => SetProperty(ref _canPlayPause, value);
    }

    public bool CanNext
    {
        get => _canNext;
        private set => SetProperty(ref _canNext, value);
    }

    /// <summary>False when the app doesn't report a track length; the progress bar is hidden then.</summary>
    public bool HasTimeline
    {
        get => _hasTimeline;
        private set => SetProperty(ref _hasTimeline, value);
    }

    /// <summary>Playback position as a share of the track, 0–1.</summary>
    public double Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    public string PositionText
    {
        get => _positionText;
        private set => SetProperty(ref _positionText, value);
    }

    public string DurationText
    {
        get => _durationText;
        private set => SetProperty(ref _durationText, value);
    }

    public IAsyncRelayCommand PreviousCommand => _previousCommand;

    public IAsyncRelayCommand PlayPauseCommand => _playPauseCommand;

    public IAsyncRelayCommand NextCommand => _nextCommand;

    internal void SetTrack(string title, string artist, string sourceName)
    {
        Title = title;
        Artist = artist;
        SourceName = sourceName;
    }

    internal void SetArt(ImageSource? art, RgbColor accent)
    {
        Art = art;
        AccentBrush = ToBrush(accent);
    }

    internal void SetPlayback(bool isPlaying, bool canPrevious, bool canPlayPause, bool canNext, double playbackRate)
    {
        IsPlaying = isPlaying;
        CanPrevious = canPrevious;
        CanPlayPause = canPlayPause;
        CanNext = canNext;
        _playbackRate = playbackRate;
        _previousCommand.NotifyCanExecuteChanged();
        _playPauseCommand.NotifyCanExecuteChanged();
        _nextCommand.NotifyCanExecuteChanged();
        UpdateProgress();
        UpdateProgressTimer();
    }

    internal void SetTimeline(TimelineSnapshot? timeline)
    {
        _timeline = timeline;
        UpdateProgress();
        UpdateProgressTimer();
    }

    /// <summary>Starts advancing the position locally; called while the expanded view is on screen.</summary>
    internal void StartProgressUpdates()
    {
        _progressWanted = true;
        UpdateProgress();
        UpdateProgressTimer();
    }

    internal void StopProgressUpdates()
    {
        _progressWanted = false;
        UpdateProgressTimer();
    }

    private static Brush ToBrush(RgbColor color)
    {
        var brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    private void UpdateProgressTimer()
    {
        if (_progressWanted && IsPlaying && HasTimeline)
        {
            _progressTimer.Start();
        }
        else
        {
            _progressTimer.Stop();
        }
    }

    private void UpdateProgress()
    {
        if (_timeline is not { HasTimeline: true } timeline)
        {
            HasTimeline = false;
            return;
        }

        HasTimeline = true;
        var position = PlaybackTimeline.Interpolate(timeline, _clock.GetUtcNow(), IsPlaying, _playbackRate);
        Progress = Math.Clamp(position.TotalSeconds / timeline.Duration.TotalSeconds, 0, 1);
        PositionText = PlaybackTimeline.Format(position);
        DurationText = PlaybackTimeline.Format(timeline.Duration);
    }
}

/// <summary>Compact media content: artwork and equalizer.</summary>
public sealed class MediaCompactViewModel(NowPlayingViewModel nowPlaying)
{
    public NowPlayingViewModel NowPlaying { get; } = nowPlaying;
}

/// <summary>Expanded media content: artwork, track, source app, controls and progress.</summary>
public sealed class MediaExpandedViewModel(NowPlayingViewModel nowPlaying) : IIslandContent
{
    public NowPlayingViewModel NowPlaying { get; } = nowPlaying;

    public void OnShown() => NowPlaying.StartProgressUpdates();

    public void OnHidden() => NowPlaying.StopProgressUpdates();
}
