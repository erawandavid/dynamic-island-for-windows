namespace Pulau.Core.Layout;

/// <summary>A rectangle in physical (device) pixels, as Win32 reports monitors and windows.</summary>
public readonly record struct PixelRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;
}
