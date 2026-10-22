"""Draws the Pulau app icon (a black pill with a light outline) as a multi-size .ico.

Usage: python tools/make_icon.py src/Pulau.App/Assets/Pulau.ico preview.png
Requires Pillow.
"""
import sys
from PIL import Image, ImageDraw

SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
SS = 8  # supersampling factor

def render(size: int) -> Image.Image:
    big = size * SS
    img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    w = round(size * 0.9375) * SS          # 15/16 of the icon width
    h = round(size * 0.5) * SS             # 2:1 aspect: the real ~4:1 pill is unreadable at 16 px
    x0 = (big - w) // 2
    y0 = (big - h) // 2
    stroke = max(1, round(size / 16)) * SS
    outer = (x0, y0, x0 + w - 1, y0 + h - 1)
    d.rounded_rectangle(outer, radius=h // 2, fill=(214, 214, 214, 255))
    inner = (x0 + stroke, y0 + stroke, x0 + w - 1 - stroke, y0 + h - 1 - stroke)
    d.rounded_rectangle(inner, radius=(h - 2 * stroke) // 2, fill=(0, 0, 0, 255))
    return img.resize((size, size), Image.LANCZOS)

images = [render(s) for s in SIZES]
images[-1].save(sys.argv[1], format="ICO", sizes=[(s, s) for s in SIZES], append_images=images[:-1])
images[-1].save(sys.argv[2])
