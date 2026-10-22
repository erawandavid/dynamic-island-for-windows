using Pulau.Core.Media;

namespace Pulau.Core.Tests.Media;

public class AccentColorTests
{
    private static byte[] Image(params (byte R, byte G, byte B, byte A, int Count)[] runs)
    {
        var pixels = new List<byte>();
        foreach (var (r, g, b, a, count) in runs)
        {
            for (var i = 0; i < count; i++)
            {
                pixels.AddRange([b, g, r, a]);
            }
        }

        return [.. pixels];
    }

    private static (double Hue, double Saturation, double Lightness) Hsl(RgbColor c) => AccentColor.ToHsl(c.R, c.G, c.B);

    [Fact]
    public void PicksTheHueOfAVividImage()
    {
        var accent = AccentColor.FromBgra32(Image((220, 30, 30, 255, 100)));

        var (hue, saturation, lightness) = Hsl(accent);
        Assert.InRange(hue, 0, 5);
        Assert.True(saturation > 0.6);
        Assert.InRange(lightness, 0.549, 0.721);
    }

    [Fact]
    public void PicksTheDominantHue_NotAMuddyAverage()
    {
        // 70% blue, 30% red: averaging would give purple.
        var accent = AccentColor.FromBgra32(Image((30, 60, 220, 255, 70), (220, 30, 30, 255, 30)));

        Assert.InRange(Hsl(accent).Hue, 210, 240);
    }

    [Fact]
    public void BrightensDarkArt_SoItIsReadableOnBlack()
    {
        var accent = AccentColor.FromBgra32(Image((10, 20, 70, 255, 64)));

        Assert.True(Hsl(accent).Lightness >= 0.549);
    }

    [Fact]
    public void TonesDownPastelArt()
    {
        var accent = AccentColor.FromBgra32(Image((250, 200, 220, 255, 64)));

        Assert.True(Hsl(accent).Lightness <= 0.721);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(255, 255, 255)]
    [InlineData(128, 128, 128)]
    [InlineData(120, 124, 120)]
    public void ColorlessArt_UsesFallback(byte r, byte g, byte b)
    {
        Assert.Equal(AccentColor.Fallback, AccentColor.FromBgra32(Image((r, g, b, 255, 64))));
    }

    [Fact]
    public void IgnoresTransparentPixels()
    {
        var accent = AccentColor.FromBgra32(Image((220, 30, 30, 0, 90), (30, 200, 60, 255, 10)));

        Assert.InRange(Hsl(accent).Hue, 120, 150);
    }

    [Fact]
    public void EmptyImage_UsesFallback()
    {
        Assert.Equal(AccentColor.Fallback, AccentColor.FromBgra32([]));
    }

    [Theory]
    [InlineData(0, 1, 0.5, 255, 0, 0)]
    [InlineData(120, 1, 0.5, 0, 255, 0)]
    [InlineData(240, 1, 0.5, 0, 0, 255)]
    [InlineData(0, 0, 1, 255, 255, 255)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    public void ConvertsHslToRgb(double h, double s, double l, byte r, byte g, byte b)
    {
        Assert.Equal(new RgbColor(r, g, b), AccentColor.FromHsl(h, s, l));
    }
}
