using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pulau.Core.Animation;
using Pulau.Core.Layout;
using Pulau.Core.States;
using Pulau.ViewModels;

namespace Pulau.Views;

/// <summary>
/// Morphs the pill with springs and cross-fades its content (PLAN.md 3.3):
/// the old content fades out quickly while the shape starts to move, the new content is swapped in,
/// and it fades in (growing slightly) once most of the morph is done.
/// The render loop (<see cref="CompositionTarget.Rendering"/>) only runs while something is moving.
/// </summary>
internal sealed class IslandAnimator
{
    private readonly Border _pill;
    private readonly ScaleTransform _pillScale;
    private readonly TranslateTransform _pillOffset;
    private readonly ContentPresenter _contentHost;
    private readonly ScaleTransform _contentScale;
    private readonly RectangleGeometry _clip = new();

    private readonly Spring _width;
    private readonly Spring _height;
    private readonly Spring _radius;
    private readonly Spring _hide;
    private readonly Spring[] _shapeSprings;

    private object? _shownContent;
    private object? _targetContent;
    private IslandSize _contentSize = IslandSizes.Idle;
    private IslandSize _targetContentSize = IslandSizes.Idle;

    private ContentPhase _phase = ContentPhase.Steady;
    private double _phaseSeconds;
    private double _fadeOutFrom;
    private double _morphDistanceAtSwap;

    private bool _running;
    private bool _hasLastFrame;
    private TimeSpan _lastFrame;

    public IslandAnimator(Border pill, ScaleTransform pillScale, TranslateTransform pillOffset, ContentPresenter contentHost, ScaleTransform contentScale)
    {
        _pill = pill;
        _pillScale = pillScale;
        _pillOffset = pillOffset;
        _contentHost = contentHost;
        _contentScale = contentScale;
        _pill.Clip = _clip;

        var idle = IslandSizes.Idle;
        _width = ShapeSpring(idle.Width);
        _height = ShapeSpring(idle.Height);
        _radius = ShapeSpring(idle.CornerRadius);
        _hide = new Spring(0, IslandMotion.HideStiffness, IslandMotion.HideDampingRatio, IslandMotion.HideRestThreshold);
        _shapeSprings = [_width, _height, _radius];
    }

    private enum ContentPhase
    {
        Steady,
        FadingOut,
        WaitingToFadeIn,
        FadingIn,
    }

    /// <summary>The Windows "Animation effects" setting.</summary>
    private static bool AnimationsEnabled => SystemParameters.ClientAreaAnimation;

    private bool IsSettled => _phase == ContentPhase.Steady && _hide.IsAtRest && _shapeSprings.All(s => s.IsAtRest);

    /// <summary>Shows <paramref name="presentation"/> immediately, without animation.</summary>
    public void JumpTo(IslandPresentation presentation)
    {
        StopLoop();
        SetTargets(presentation);
        foreach (var spring in _shapeSprings)
        {
            spring.SnapToTarget();
        }

        _hide.SnapToTarget();

        _targetContent = presentation.Content;
        _targetContentSize = presentation.Size;
        ShowContent(presentation.Content, presentation.Size);
        _contentHost.Opacity = 1;
        SetContentScale(1);
        _phase = ContentPhase.Steady;
        Apply();
    }

    public void AnimateTo(IslandPresentation presentation)
    {
        if (!AnimationsEnabled)
        {
            JumpTo(presentation);
            return;
        }

        SetTargets(presentation);
        _targetContentSize = presentation.Size;

        if (!ReferenceEquals(presentation.Content, _targetContent))
        {
            _targetContent = presentation.Content;
            if (_phase != ContentPhase.FadingOut)
            {
                _phase = ContentPhase.FadingOut;
                _phaseSeconds = 0;
                _fadeOutFrom = _contentHost.Opacity;
            }
        }
        else if (_phase == ContentPhase.Steady && _contentSize != presentation.Size)
        {
            // Same content in a new size: resize its box right away.
            ShowContent(_shownContent, presentation.Size);
        }

        StartLoop();
    }

    private static Spring ShapeSpring(double value) =>
        new(value, IslandMotion.ShapeStiffness, IslandMotion.ShapeDampingRatio, IslandMotion.ShapeRestThreshold);

    private void SetTargets(IslandPresentation presentation)
    {
        _width.Target = presentation.Size.Width;
        _height.Target = presentation.Size.Height;
        _radius.Target = presentation.Size.CornerRadius;
        _hide.Target = presentation.State == IslandState.Hidden ? 1 : 0;
    }

    private void StartLoop()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        _hasLastFrame = false;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopLoop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = e is RenderingEventArgs rendering ? rendering.RenderingTime : TimeSpan.FromMilliseconds(Environment.TickCount64);

        // Rendering can fire more than once per frame; only step on new frames.
        if (_hasLastFrame && now == _lastFrame)
        {
            return;
        }

        var deltaSeconds = _hasLastFrame ? (now - _lastFrame).TotalSeconds : 1.0 / 60;
        _lastFrame = now;
        _hasLastFrame = true;

        Step(deltaSeconds);
        if (IsSettled)
        {
            StopLoop();
        }
    }

    private void Step(double deltaSeconds)
    {
        foreach (var spring in _shapeSprings)
        {
            spring.Step(deltaSeconds);
        }

        _hide.Step(deltaSeconds);

        switch (_phase)
        {
            case ContentPhase.FadingOut:
                _phaseSeconds += deltaSeconds;
                var fadeOutSeconds = IslandMotion.ContentFadeOut.TotalSeconds * _fadeOutFrom;
                var fadedOut = fadeOutSeconds <= 0 ? 1 : Math.Min(1, _phaseSeconds / fadeOutSeconds);
                _contentHost.Opacity = _fadeOutFrom * (1 - fadedOut);
                if (fadedOut >= 1)
                {
                    SwapContent();
                }

                break;

            case ContentPhase.WaitingToFadeIn:
                var remaining = MorphDistance();
                if (remaining <= _morphDistanceAtSwap * (1 - IslandMotion.ContentFadeInAtProgress) || _shapeSprings.All(s => s.IsAtRest))
                {
                    BeginFadeIn();
                }

                break;

            case ContentPhase.FadingIn:
                _phaseSeconds += deltaSeconds;
                var progress = Math.Min(1, _phaseSeconds / IslandMotion.ContentFadeIn.TotalSeconds);
                var eased = 1 - Math.Pow(1 - progress, 3);
                _contentHost.Opacity = eased;
                SetContentScale(IslandMotion.ContentStartScale + ((1 - IslandMotion.ContentStartScale) * eased));
                if (progress >= 1)
                {
                    _phase = ContentPhase.Steady;
                }

                break;
        }

        Apply();
    }

    private void SwapContent()
    {
        ShowContent(_targetContent, _targetContentSize);
        _contentHost.Opacity = 0;
        SetContentScale(IslandMotion.ContentStartScale);

        if (_shownContent is null)
        {
            _phase = ContentPhase.Steady;
            return;
        }

        _morphDistanceAtSwap = MorphDistance();
        _phase = ContentPhase.WaitingToFadeIn;
        if (_morphDistanceAtSwap < 1)
        {
            BeginFadeIn();
        }
    }

    private void BeginFadeIn()
    {
        _phase = ContentPhase.FadingIn;
        _phaseSeconds = 0;
    }

    private void ShowContent(object? content, IslandSize size)
    {
        _shownContent = content;
        _contentSize = size;
        _contentHost.Content = content;

        // The content box always has the final size, so its layout doesn't change while the pill morphs;
        // the pill's rounded clip hides whatever doesn't fit yet.
        _contentHost.Width = size.Width;
        _contentHost.Height = size.Height;
    }

    private void SetContentScale(double scale)
    {
        _contentScale.ScaleX = scale;
        _contentScale.ScaleY = scale;
    }

    private double MorphDistance() =>
        Math.Max(Math.Abs(_width.Value - _width.Target), Math.Abs(_height.Value - _height.Target));

    private void Apply()
    {
        var width = Math.Max(0, _width.Value);
        var height = Math.Max(0, _height.Value);
        var radius = Math.Clamp(_radius.Value, 0, Math.Min(width, height) / 2);

        _pill.Width = width;
        _pill.Height = height;
        _pill.CornerRadius = new CornerRadius(radius);
        _clip.Rect = new Rect(0, 0, width, height);
        _clip.RadiusX = radius;
        _clip.RadiusY = radius;
        Canvas.SetLeft(_contentHost, (width - _contentSize.Width) / 2);

        // Hidden: fade out while shrinking and sliding up.
        var hide = Math.Clamp(_hide.Value, 0, 1);
        _pill.Opacity = 1 - hide;
        var scale = 1 - (0.25 * hide);
        _pillScale.ScaleX = scale;
        _pillScale.ScaleY = scale;
        _pillOffset.Y = -hide * (height + IslandPlacement.PillTopMarginDip) * 0.5;
        _pill.IsHitTestVisible = hide < 0.5;
    }
}
