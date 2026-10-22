namespace Pulau.Core.Animation;

/// <summary>Motion tuning for the island. Adjust by eye; see PLAN.md 3.3.</summary>
public static class IslandMotion
{
    /// <summary>Stiffness of the shape springs (width, height, corner radius).</summary>
    public const double ShapeStiffness = 300;

    /// <summary>Slightly under 1, so the shape settles with a small, lively overshoot.</summary>
    public const double ShapeDampingRatio = 0.8;

    /// <summary>Settled when within this many DIPs of the target.</summary>
    public const double ShapeRestThreshold = 0.1;

    /// <summary>Hiding and showing the island: quick, without bounce.</summary>
    public const double HideStiffness = 380;

    public const double HideDampingRatio = 1.0;

    public const double HideRestThreshold = 0.002;

    /// <summary>How long the old content takes to fade out.</summary>
    public static readonly TimeSpan ContentFadeOut = TimeSpan.FromMilliseconds(100);

    /// <summary>How long the new content takes to fade in.</summary>
    public static readonly TimeSpan ContentFadeIn = TimeSpan.FromMilliseconds(180);

    /// <summary>New content starts fading in once this share of the shape morph is done.</summary>
    public const double ContentFadeInAtProgress = 0.7;

    /// <summary>New content grows from this scale to 1 while it fades in.</summary>
    public const double ContentStartScale = 0.94;
}
