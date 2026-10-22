namespace Pulau.Core.Media;

public readonly record struct RgbColor(byte R, byte G, byte B);

/// <summary>
/// Picks an accent color from album art for the equalizer and progress bar: the average color of
/// the most common vivid hue, with its lightness clamped so it stays readable on the black pill.
/// </summary>
public static class AccentColor
{
    private const int HueBuckets = 12;
    private const double MinLightness = 0.55;
    private const double MaxLightness = 0.72;
    private const double MinSaturation = 0.45;

    /// <summary>Used for missing or nearly colorless art.</summary>
    public static RgbColor Fallback { get; } = new(0xE6, 0xE6, 0xE6);

    /// <summary>Picks the accent from 32-bit BGRA pixels (WPF's <c>Bgra32</c>).</summary>
    public static RgbColor FromBgra32(ReadOnlySpan<byte> pixels)
    {
        Span<double> bucketWeight = stackalloc double[HueBuckets];
        Span<double> bucketR = stackalloc double[HueBuckets];
        Span<double> bucketG = stackalloc double[HueBuckets];
        Span<double> bucketB = stackalloc double[HueBuckets];
        var totalWeight = 0.0;
        var opaquePixels = 0;

        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            if (pixels[i + 3] < 128)
            {
                continue;
            }

            opaquePixels++;
            var b = pixels[i];
            var g = pixels[i + 1];
            var r = pixels[i + 2];
            var (hue, saturation, lightness) = ToHsl(r, g, b);

            // Vividness: saturated pixels that are neither near-black nor near-white.
            var weight = saturation * (1 - Math.Abs((2 * lightness) - 1));
            if (weight <= 0)
            {
                continue;
            }

            var bucket = (int)(hue / (360.0 / HueBuckets)) % HueBuckets;
            bucketWeight[bucket] += weight;
            bucketR[bucket] += r * weight;
            bucketG[bucket] += g * weight;
            bucketB[bucket] += b * weight;
            totalWeight += weight;
        }

        // Mostly black, white or grey art: no meaningful hue.
        if (opaquePixels == 0 || totalWeight / opaquePixels < 0.08)
        {
            return Fallback;
        }

        var best = 0;
        for (var i = 1; i < HueBuckets; i++)
        {
            if (bucketWeight[i] > bucketWeight[best])
            {
                best = i;
            }
        }

        var w = bucketWeight[best];
        var (h, s, l) = ToHsl(bucketR[best] / w, bucketG[best] / w, bucketB[best] / w);
        return FromHsl(h, Math.Max(s, MinSaturation), Math.Clamp(l, MinLightness, MaxLightness));
    }

    /// <summary>Hue in degrees [0, 360), saturation and lightness in [0, 1].</summary>
    public static (double Hue, double Saturation, double Lightness) ToHsl(double r, double g, double b)
    {
        r /= 255;
        g /= 255;
        b /= 255;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2;
        if (max - min < 1e-9)
        {
            return (0, 0, lightness);
        }

        var delta = max - min;
        var saturation = lightness > 0.5 ? delta / (2 - max - min) : delta / (max + min);
        double hue;
        if (max == r)
        {
            hue = ((g - b) / delta) + (g < b ? 6 : 0);
        }
        else if (max == g)
        {
            hue = ((b - r) / delta) + 2;
        }
        else
        {
            hue = ((r - g) / delta) + 4;
        }

        return (hue * 60 % 360, saturation, lightness);
    }

    public static RgbColor FromHsl(double hue, double saturation, double lightness)
    {
        var c = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var x = c * (1 - Math.Abs((hue / 60 % 2) - 1));
        var m = lightness - (c / 2);
        var (r, g, b) = (hue % 360) switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return new RgbColor(ToByte(r + m), ToByte(g + m), ToByte(b + m));
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
}
