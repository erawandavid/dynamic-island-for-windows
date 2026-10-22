namespace Pulau.Core.Layout;

/// <summary>Size and corner radius of the pill, in DIPs.</summary>
public readonly record struct IslandSize(double Width, double Height, double CornerRadius);

/// <summary>The island's standard sizes (PLAN.md 3.3).</summary>
public static class IslandSizes
{
    /// <summary>Empty pill shown when nothing is going on.</summary>
    public static IslandSize Idle { get; } = new(120, 32, 16);

    /// <summary>Default compact size for an activity. Compact content can use 220–280 DIPs of width.</summary>
    public static IslandSize Compact { get; } = new(240, 34, 17);

    /// <summary>Expanded with no activity: clock, date and battery.</summary>
    public static IslandSize Home { get; } = new(340, 110, 28);

    /// <summary>Default expanded size for an activity (media controls).</summary>
    public static IslandSize Expanded { get; } = new(380, 140, 28);
}
