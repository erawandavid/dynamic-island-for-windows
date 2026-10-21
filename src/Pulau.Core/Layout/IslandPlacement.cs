namespace Pulau.Core.Layout;

/// <summary>
/// Where the island's host window goes on a monitor. The window is a fixed-size transparent
/// surface; the pill is drawn inside it, so only the window's bounds are computed here.
/// </summary>
public static class IslandPlacement
{
    /// <summary>Host window width in DIPs: wider than the largest expanded pill (380) plus shadow margin.</summary>
    public const double WindowWidthDip = 480;

    /// <summary>Host window height in DIPs: taller than the largest expanded pill (140) plus shadow margin.</summary>
    public const double WindowHeightDip = 220;

    /// <summary>Gap between the top edge of the work area and the top of the pill, in DIPs.</summary>
    public const double PillTopMarginDip = 8;

    /// <summary>
    /// Computes the host window bounds: horizontally centered on <paramref name="workArea"/> and
    /// aligned to its top edge, scaled by the monitor's DPI scale (1.0 = 96 DPI = 100%).
    /// </summary>
    public static PixelRect ComputeWindowBounds(PixelRect workArea, double dpiScale)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dpiScale);

        var width = (int)Math.Round(WindowWidthDip * dpiScale, MidpointRounding.AwayFromZero);
        var height = (int)Math.Round(WindowHeightDip * dpiScale, MidpointRounding.AwayFromZero);
        var left = workArea.Left + (int)Math.Floor((workArea.Width - width) / 2.0);

        return new PixelRect(left, workArea.Top, width, height);
    }
}
