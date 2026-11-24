"""Gera as imagens do assistente do instalador (Inno Setup) com a marca 2D.
Uso: python mkwizard.py <pasta-brand>"""
import sys
from PIL import Image, ImageDraw, ImageFont

brand = sys.argv[1]
NAVY_TOP, NAVY_BOT = (16, 43, 102), (10, 18, 38)  # #102B66 -> #0A1226
BRAND2 = (43, 143, 230)
logo_white = Image.open(f"{brand}/logo-white.png").convert("RGBA")
tile = Image.open(f"{brand}/2d-256.png").convert("RGBA")

def font(size, bold=False):
    for name in (("segoeuib.ttf" if bold else "segoeui.ttf"), "arial.ttf"):
        try:
            return ImageFont.truetype(f"C:/Windows/Fonts/{name}", size)
        except OSError:
            pass
    return ImageFont.load_default()

def large(scale):
    w, h = 164 * scale, 314 * scale
    im = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(im)
    for y in range(h):  # degradê vertical marinho
        t = y / (h - 1)
        d.line([(0, y), (w, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(NAVY_TOP, NAVY_BOT)))
    # faixa azul de destaque
    d.rectangle([0, h - 6 * scale, w, h], fill=BRAND2)
    lg = logo_white.copy()
    lg.thumbnail((int(w * 0.72), int(w * 0.72)), Image.LANCZOS)
    im.paste(lg, ((w - lg.width) // 2, int(h * 0.18)), lg)
    f1, f2 = font(17 * scale, True), font(10 * scale)
    for text, f, y in (("2D Editor", f1, 0.62), ("Editor de texto e código", f2, 0.70), ("2dtecnologia.com", f2, 0.90)):
        tw = d.textlength(text, font=f)
        d.text(((w - tw) / 2, h * y), text, font=f, fill=(232, 238, 252))
    return im

def small(scale):
    s = 55 * scale
    im = Image.new("RGB", (s, s), (255, 255, 255))
    t = tile.copy()
    t.thumbnail((s - 4 * scale, s - 4 * scale), Image.LANCZOS)
    im.paste(t, ((s - t.width) // 2, (s - t.height) // 2), t)
    return im

for sc in (1, 2):
    large(sc).save(f"{brand}/wizard-large-{sc}x.bmp")
    small(sc).save(f"{brand}/wizard-small-{sc}x.bmp")
large(2).save(f"{brand}/wizard-large-preview.png")
print("ok")
