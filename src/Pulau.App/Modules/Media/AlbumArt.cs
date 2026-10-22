using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pulau.Core.Media;
using Windows.Storage.Streams;

namespace Pulau.Modules.Media;

/// <summary>Loads a media session's thumbnail into a WPF image and picks its accent color.</summary>
internal static class AlbumArt
{
    /// <summary>Decoded width in pixels: enough for the 56 DIP expanded artwork at high DPI.</summary>
    private const int DecodeWidth = 192;

    /// <summary>The accent color is computed from a tiny copy of the image.</summary>
    private const int SampleSize = 24;

    /// <summary>Returns <c>null</c> when the app provided an empty thumbnail.</summary>
    /// <exception cref="OperationCanceledException">Loading took longer than <paramref name="timeout"/>.</exception>
    public static async Task<(BitmapSource Image, RgbColor Accent)?> LoadAsync(IRandomAccessStreamReference thumbnail, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        using var buffer = new MemoryStream();
        using (var source = await thumbnail.OpenReadAsync().AsTask(cancellation.Token))
        await using (var stream = source.AsStreamForRead())
        {
            await stream.CopyToAsync(buffer, cancellation.Token);
        }

        if (buffer.Length == 0)
        {
            return null;
        }

        buffer.Position = 0;
        var image = Decode(buffer);
        return (image, SampleAccent(image));
    }

    private static BitmapSource Decode(Stream stream)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        image.DecodePixelWidth = DecodeWidth;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static RgbColor SampleAccent(BitmapSource image)
    {
        if (image.PixelWidth == 0 || image.PixelHeight == 0)
        {
            return AccentColor.Fallback;
        }

        var scaled = new TransformedBitmap(
            image,
            new ScaleTransform((double)SampleSize / image.PixelWidth, (double)SampleSize / image.PixelHeight));
        var pixels = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
        var stride = pixels.PixelWidth * 4;
        var bytes = new byte[stride * pixels.PixelHeight];
        pixels.CopyPixels(bytes, stride, 0);
        return AccentColor.FromBgra32(bytes);
    }
}
