#!/usr/bin/env python3
"""Render the app icon: a cream Ա on the app's deep red (colours from App.uss).

Writes:
  Assets/Art/AppIcon.png                       1024x1024, the default icon for every platform
  Assets/WebGLTemplates/Ayb/favicon.png        64x64, browser tab
  Assets/WebGLTemplates/Ayb/apple-touch-icon.png  180x180, "add to home screen" on phones

Full-bleed square: phones round the corners themselves. The letter stays inside the central
circle that Android's adaptive icons keep. Rerun after changing the design:

    pip install pillow
    python3 Tools/gen_icon.py
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
FONT = ROOT / "Assets" / "Resources" / "Fonts" / "NotoSansArmenian-Regular.ttf"
RED = (0xB5, 0x33, 0x2E)
PAPER = (0xFF, 0xF8, 0xF0)
SIZE = 1024


def render():
    img = Image.new("RGB", (SIZE, SIZE), RED)
    draw = ImageDraw.Draw(img)
    font = ImageFont.truetype(str(FONT), int(SIZE * 0.62))
    stroke = 20  # a little bolder than the regular font
    l, t, r, b = draw.textbbox((0, 0), "Ա", font=font, stroke_width=stroke)
    pos = ((SIZE - (r - l)) / 2 - l, (SIZE - (b - t)) / 2 - t)
    draw.text(pos, "Ա", font=font, fill=PAPER, stroke_width=stroke, stroke_fill=PAPER)
    return img


def main():
    icon = render()
    outputs = {
        ROOT / "Assets" / "Art" / "AppIcon.png": SIZE,
        ROOT / "Assets" / "WebGLTemplates" / "Ayb" / "favicon.png": 64,
        ROOT / "Assets" / "WebGLTemplates" / "Ayb" / "apple-touch-icon.png": 180,
    }
    for path, size in outputs.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        (icon if size == SIZE else icon.resize((size, size), Image.LANCZOS)).save(path, optimize=True)
        print(path.relative_to(ROOT))


if __name__ == "__main__":
    main()
