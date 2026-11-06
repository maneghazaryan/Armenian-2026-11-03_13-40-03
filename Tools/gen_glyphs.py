#!/usr/bin/env python3
"""Render the letter shapes used by the drawing exercises.

For every letter (small and capital) writes to Assets/Resources/Glyphs/:
  <id>_l.bytes / <id>_u.bytes  PNG, white ink with alpha: the faint guide to trace over
  refs.json                    64x64 bit masks of the thin letter, which drawings are compared with

The letter is centred and scaled to the same box in both, so a guide pixel and a mask cell
line up with the same spot of the drawing pad. Rerun after changing the font or letters.json:

    pip install pillow
    python3 Tools/gen_glyphs.py
"""
import io
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets" / "Resources" / "Glyphs"
FONT = ROOT / "Assets" / "Resources" / "Fonts" / "NotoSansArmenian-Regular.ttf"
SIZE = 256   # guide image size; the pad shows it edge to edge
BOX = 184    # largest ink extent, leaves a margin to draw in
CELLS = 64   # mask resolution; must match DrawingCheck.N


def layout(text):
    """Font size and offset that fit the bold guide into BOX; the thin mask reuses them."""
    for size in range(260, 40, -4):
        font = ImageFont.truetype(str(FONT), size)
        stroke = max(1, round(size * 0.03))
        l, t, r, b = ImageDraw.Draw(Image.new("L", (1, 1))).textbbox((0, 0), text, font=font, stroke_width=stroke)
        if r - l <= BOX and b - t <= BOX:
            return font, stroke, ((SIZE - (r - l)) / 2 - l, (SIZE - (b - t)) / 2 - t)
    raise ValueError(text)


def render(text, font, stroke, pos):
    img = Image.new("L", (SIZE, SIZE), 0)
    ImageDraw.Draw(img).text(pos, text, font=font, fill=255, stroke_width=stroke, stroke_fill=255)
    return img


def mask_hex(img):
    """A cell is ink when any of its pixels is; rows top to bottom, 4 cells per hex digit."""
    step = SIZE // CELLS
    px = img.load()
    bits = []
    for cy in range(CELLS):
        for cx in range(CELLS):
            bits.append(any(px[cx * step + x, cy * step + y] > 128 for y in range(step) for x in range(step)))
    return "".join("%x" % (bits[i] << 3 | bits[i + 1] << 2 | bits[i + 2] << 1 | bits[i + 3]) for i in range(0, len(bits), 4))


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    letters = json.loads((ROOT / "Assets" / "Resources" / "Data" / "letters.json").read_text(encoding="utf-8"))["letters"]
    refs = []
    for letter in letters:
        for suffix, text in (("l", letter["lower"]), ("u", letter["upper"])):
            font, stroke, pos = layout(text)
            guide = render(text, font, stroke, pos)
            rgba = Image.merge("RGBA", (Image.new("L", guide.size, 255),) * 3 + (guide,))
            buf = io.BytesIO()
            rgba.save(buf, "PNG", optimize=True)
            key = f"{letter['id']}_{suffix}"
            (OUT / f"{key}.bytes").write_bytes(buf.getvalue())
            refs.append({"key": key, "bits": mask_hex(render(text, font, 0, pos))})
    (OUT / "refs.json").write_text(json.dumps({"items": refs}, separators=(",", ":")) + "\n", encoding="utf-8")
    print(f"{len(refs)} glyphs -> {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
