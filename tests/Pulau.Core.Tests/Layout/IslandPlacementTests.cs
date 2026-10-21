using Pulau.Core.Layout;

namespace Pulau.Core.Tests.Layout;

public class IslandPlacementTests
{
    [Theory]
    [InlineData(1.00, 720, 480, 220)]  // 100%: (1920 - 480) / 2
    [InlineData(1.25, 660, 600, 275)]  // 125%
    [InlineData(1.50, 600, 720, 330)]  // 150%
    [InlineData(1.75, 540, 840, 385)]  // 175%
    public void CentersOnWorkArea_AtCommonScales(double scale, int expectedLeft, int expectedWidth, int expectedHeight)
    {
        var workArea = new PixelRect(0, 0, 1920, 1032);

        var bounds = IslandPlacement.ComputeWindowBounds(workArea, scale);

        Assert.Equal(new PixelRect(expectedLeft, 0, expectedWidth, expectedHeight), bounds);
    }

    [Fact]
    public void FollowsWorkAreaTop_WhenTaskbarIsAtTheTop()
    {
        var workArea = new PixelRect(0, 48, 2560, 1392);

        var bounds = IslandPlacement.ComputeWindowBounds(workArea, 1.0);

        Assert.Equal(48, bounds.Top);
    }

    [Fact]
    public void CentersOnWorkArea_NotOnMonitor_WhenTaskbarIsOnTheLeft()
    {
        // 1920-wide monitor with a 100 px taskbar on the left.
        var workArea = new PixelRect(100, 0, 1820, 1080);

        var bounds = IslandPlacement.ComputeWindowBounds(workArea, 1.0);

        Assert.Equal(100 + (1820 - 480) / 2, bounds.Left);
    }

    [Fact]
    public void HandlesMonitorsAtNegativeCoordinates()
    {
        // Secondary monitor to the left of (and above) the primary one.
        var workArea = new PixelRect(-2560, -200, 2560, 1400);

        var bounds = IslandPlacement.ComputeWindowBounds(workArea, 1.0);

        Assert.Equal(new PixelRect(-2560 + 1040, -200, 480, 220), bounds);
    }

    [Fact]
    public void StaysCentered_WhenRemainingSpaceIsOdd()
    {
        var workArea = new PixelRect(0, 0, 1366, 728);

        var bounds = IslandPlacement.ComputeWindowBounds(workArea, 1.0);

        // 886 px of free space: 443 on each side.
        Assert.Equal(443, bounds.Left);
        Assert.Equal(workArea.Right - bounds.Right, bounds.Left - workArea.Left);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void RejectsNonPositiveScale(double scale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IslandPlacement.ComputeWindowBounds(new PixelRect(0, 0, 1920, 1080), scale));
    }
}
