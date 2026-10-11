"""Draws YtPlayer's icon and writes the files the build uses.

The mark is the compact button's glyph grown into an app icon: a screen, with a picture-in-picture panel
in its lower right that holds a play triangle, in the strip's own colours (PipChrome). Each size is drawn
on its own (supersampled, then reduced), so a 16 px icon gets strokes thick enough to survive.

While the video plays, the window's icon changes: the screen's outline goes red and the panel shows an
equalizer's three bars, so the taskbar says something is playing even at 24 px.

    python tools/make-icon.py

Writes, under src/YtPlayer/Assets/:
  ytplayer.ico       16-256 px, PNG-compressed entries; the Windows exe icon (ApplicationIcon)
  ytplayer-256.png   for the README and anything else that wants a picture
  ytplayer-64.rgba   64x64 raw RGBA bytes, the window icon on Linux (SDL_SetWindowIcon), and on
                     Windows too once the video has played
  ytplayer-playing-64.rgba   the same, while the video plays

Needs Pillow.
"""
import io
import struct
from pathlib import Path

from PIL import Image, ImageDraw

TILE = (0x18, 0x18, 0x1C, 0xFF)    # PipChrome.Background
INK = (0xD8, 0xD8, 0xDC, 0xFF)     # PipChrome.Ink
ACCENT = (0xFF, 0x30, 0x30, 0xFF)  # PipChrome.Accent

ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
SUPERSAMPLE = 8
OUT = Path(__file__).resolve().parent.parent / "src" / "YtPlayer" / "Assets"


def draw(size: int, playing: bool = False) -> Image.Image:
    s = size * SUPERSAMPLE
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    def box(x0, y0, x1, y1):
        return [round(x0 * s), round(y0 * s), round(x1 * s), round(y1 * s)]

    # Small sizes need relatively thicker lines and a little less margin to stay legible.
    small = size <= 24
    stroke = max(0.065, 1.4 / size) if small else 0.06
    margin = 0.02 if small else 0.04

    d.rounded_rectangle(box(margin, margin, 1 - margin, 1 - margin), radius=round(0.2 * s), fill=TILE)
    # The screen; red while playing.
    d.rounded_rectangle(box(0.15, 0.22, 0.85, 0.78), radius=round(0.07 * s), outline=ACCENT if playing else INK,
                        width=max(1, round(stroke * s)))
    # The picture in it, lower right, and its play triangle.
    px0, py0, px1, py1 = (0.44, 0.45, 0.78, 0.71) if not small else (0.42, 0.43, 0.80, 0.73)
    d.rounded_rectangle(box(px0, py0, px1, py1), radius=round(0.045 * s), fill=ACCENT)
    if playing:
        # Three bars of an equalizer in place of the triangle, standing on one baseline.
        pw, ph = px1 - px0, py1 - py0
        bar, gap = pw * 0.15, pw * 0.09
        x = px0 + (pw - 3 * bar - 2 * gap) / 2
        base = py1 - ph * 0.2
        for height in (0.45, 0.62, 0.32):
            d.rectangle(box(x, base - ph * height, x + bar, base), fill=TILE)
            x += bar + gap
    else:
        cx, cy = (px0 + px1) / 2 + 0.01, (py0 + py1) / 2
        h = (py1 - py0) * 0.52
        w = h * 0.87
        d.polygon([(round((cx - w / 2) * s), round((cy - h / 2) * s)),
                   (round((cx + w / 2) * s), round(cy * s)),
                   (round((cx - w / 2) * s), round((cy + h / 2) * s))], fill=TILE)

    return img.resize((size, size), Image.LANCZOS)


def png_bytes(img: Image.Image) -> bytes:
    buf = io.BytesIO()
    img.save(buf, format="PNG", optimize=True)
    return buf.getvalue()


def write_ico(path: Path, images: list[Image.Image]) -> None:
    # ICONDIR, then one ICONDIRENTRY per image, then the PNG blobs (Vista+ reads PNG entries at any size).
    blobs = [png_bytes(im) for im in images]
    header = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries = b""
    for im, blob in zip(images, blobs):
        w, h = im.size
        entries += struct.pack("<BBBBHHII", w % 256, h % 256, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
    path.write_bytes(header + entries + b"".join(blobs))


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    images = [draw(size) for size in ICO_SIZES]
    write_ico(OUT / "ytplayer.ico", images)
    images[ICO_SIZES.index(256)].save(OUT / "ytplayer-256.png", optimize=True)
    (OUT / "ytplayer-64.rgba").write_bytes(images[ICO_SIZES.index(64)].tobytes())
    (OUT / "ytplayer-playing-64.rgba").write_bytes(draw(64, playing=True).tobytes())
    print(f"wrote {', '.join(p.name for p in sorted(OUT.iterdir()))} to {OUT}")


if __name__ == "__main__":
    main()
