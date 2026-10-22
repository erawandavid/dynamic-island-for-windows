using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Pulau.Views.Controls;

/// <summary>
/// Four small bars that bounce while <see cref="IsActive"/> (music playing) and rest at
/// uneven heights otherwise. Purely decorative: it doesn't follow the actual audio.
/// The animation only runs while the control is on screen, at 30 frames per second.
/// </summary>
public sealed class EqualizerBars : StackPanel
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive),
        typeof(bool),
        typeof(EqualizerBars),
        new PropertyMetadata(false, (d, _) => ((EqualizerBars)d).UpdateAnimation()));

    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush),
        typeof(Brush),
        typeof(EqualizerBars),
        new PropertyMetadata(Brushes.White, (d, _) => ((EqualizerBars)d).ApplyBrush()));

    private const double BarWidth = 3;
    private const double BarHeight = 16;
    private const double BarGap = 2;

    // Seconds per half bounce, and resting heights (as a share of the full height), per bar.
    private static readonly double[] HalfPeriods = [0.42, 0.31, 0.47, 0.36];
    private static readonly double[] RestingScales = [0.35, 0.6, 0.45, 0.3];

    private readonly Rectangle[] _bars = new Rectangle[4];
    private readonly ScaleTransform[] _scales = new ScaleTransform[4];
    private bool _animating;

    public EqualizerBars()
    {
        Orientation = Orientation.Horizontal;
        IsHitTestVisible = false;

        for (var i = 0; i < _bars.Length; i++)
        {
            _scales[i] = new ScaleTransform(1, RestingScales[i]);
            _bars[i] = new Rectangle
            {
                Width = BarWidth,
                Height = BarHeight,
                RadiusX = BarWidth / 2,
                RadiusY = BarWidth / 2,
                Margin = new Thickness(i == 0 ? 0 : BarGap, 0, 0, 0),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = _scales[i],
                Fill = BarBrush,
            };
            Children.Add(_bars[i]);
        }

        Loaded += (_, _) => UpdateAnimation();
        Unloaded += (_, _) => UpdateAnimation();
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public Brush BarBrush
    {
        get => (Brush)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    private void ApplyBrush()
    {
        foreach (var bar in _bars)
        {
            bar.Fill = BarBrush;
        }
    }

    private void UpdateAnimation()
    {
        var shouldAnimate = IsActive && IsLoaded && SystemParameters.ClientAreaAnimation;
        if (shouldAnimate == _animating)
        {
            return;
        }

        _animating = shouldAnimate;
        for (var i = 0; i < _scales.Length; i++)
        {
            if (shouldAnimate)
            {
                var bounce = new DoubleAnimation(0.2, 1.0, TimeSpan.FromSeconds(HalfPeriods[i]))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                };
                Timeline.SetDesiredFrameRate(bounce, 30);
                bounce.Freeze();
                _scales[i].BeginAnimation(ScaleTransform.ScaleYProperty, bounce);
            }
            else
            {
                _scales[i].BeginAnimation(ScaleTransform.ScaleYProperty, null);
                _scales[i].ScaleY = RestingScales[i];
            }
        }
    }
}
