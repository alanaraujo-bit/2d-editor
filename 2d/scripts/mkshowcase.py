"""Gera as imagens de vitrine do README do 2D Editor.

Uso: python mkshowcase.py <pasta-capturas> <pasta-brand> <pasta-saida>

- banner.png      capa 1280x640 (em 2x): marca, título, frase e o editor escuro
- atualizacao.png o cartão de atualização sobre o editor, como o usuário vê
"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont

cap, brand, out = map(Path, sys.argv[1:4])
out.mkdir(parents=True, exist_ok=True)
S = 2  # escala de renderização

def font(name, size):
    return ImageFont.truetype(f"C:/Windows/Fonts/{name}", size * S)

def rounded(img, radius):
    mask = Image.new("L", img.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, img.width - 1, img.height - 1), radius=radius, fill=255)
    img = img.convert("RGBA")
    img.putalpha(mask)
    return img

def shadow(size, radius, blur, opacity):
    w, h = size
    pad = blur * 3
    sh = Image.new("RGBA", (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
    ImageDraw.Draw(sh).rounded_rectangle((pad, pad, pad + w, pad + h), radius=radius, fill=(0, 0, 0, int(255 * opacity)))
    return sh.filter(ImageFilter.GaussianBlur(blur)), pad

# ------------------------------------------------------------------ banner
W, H = 1280 * S, 640 * S
banner = Image.new("RGB", (W, H))
d = ImageDraw.Draw(banner)
top, bot = (13, 25, 54), (8, 14, 30)
for y in range(H):
    t = y / (H - 1)
    d.line([(0, y), (W, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(top, bot)))
banner = banner.convert("RGBA")

# editor escuro à direita, sangrando para fora do quadro
ed = Image.open(cap / "editor-escuro.png").convert("RGB")
ew = int(860 * S)
ed = ed.resize((ew, int(ed.height * ew / ed.width)), Image.LANCZOS)
ed = rounded(ed, 14 * S)
border = Image.new("RGBA", ed.size, (0, 0, 0, 0))
ImageDraw.Draw(border).rounded_rectangle((0, 0, ed.width - 1, ed.height - 1), radius=14 * S, outline=(46, 62, 102, 255), width=S)
ex, ey = int(560 * S), int(150 * S)
sh, pad = shadow(ed.size, 14 * S, 28 * S, 0.55)
banner.alpha_composite(sh, (ex - pad, ey - pad + 10 * S))
banner.alpha_composite(ed, (ex, ey))
banner.alpha_composite(border, (ex, ey))

# esmaece a borda direita/inferior do editor para o fundo (acabamento limpo)
fade = Image.new("RGBA", (W, H), (0, 0, 0, 0))
fd = ImageDraw.Draw(fade)
for i in range(int(180 * S)):
    a = int(255 * (i / (180 * S)) ** 2)
    fd.line([(0, H - int(180 * S) + i), (W, H - int(180 * S) + i)], fill=(8, 14, 30, a))
banner.alpha_composite(fade)

# texto à esquerda
logo = Image.open(brand / "logo-white.png").convert("RGBA")
lh = int(54 * S)
logo = logo.resize((int(logo.width * lh / logo.height), lh), Image.LANCZOS)
x0 = int(84 * S)
banner.alpha_composite(logo, (x0, int(150 * S)))
d = ImageDraw.Draw(banner)
d.text((x0 - 4 * S, int(232 * S)), "2D Editor", font=font("seguisb.ttf", 76), fill=(255, 255, 255))
d.text((x0, int(338 * S)), "Editor de texto e código", font=font("segoeui.ttf", 27), fill=(186, 200, 226))
d.text((x0, int(374 * S)), "para Windows.", font=font("segoeui.ttf", 27), fill=(186, 200, 226))
items = ["Leve", "Em português", "Atualiza sozinho"]
ix = x0
iy = int(446 * S)
f_small = font("segoeui.ttf", 17)
for i, it in enumerate(items):
    if i:
        d.ellipse((ix + 2 * S, iy + 11 * S, ix + 6 * S, iy + 15 * S), fill=(76, 120, 190))
        ix += 18 * S
    d.text((ix, iy), it, font=f_small, fill=(130, 150, 186))
    ix += int(d.textlength(it, font=f_small)) + 10 * S
d.text((x0, int(560 * S)), "2dtecnologia.com", font=font("segoeui.ttf", 16), fill=(96, 116, 154))
banner.convert("RGB").save(out / "banner.png", optimize=True)

# ------------------------------------------------------------------ atualização em contexto
def showcase(editor_png, card_png, name, dim):
    base = Image.open(cap / editor_png).convert("RGBA")
    base = base.resize((base.width * S // 2 * 2 // 2 * 1, base.height), Image.LANCZOS) if False else base
    k = 1.0  # capturas já em 1280x800
    veil = Image.new("RGBA", base.size, dim)
    blurred = base.filter(ImageFilter.GaussianBlur(1.2))
    blurred.alpha_composite(veil)
    card = Image.open(cap / card_png).convert("RGBA")
    card = card.resize((card.width // 2 * 1 if False else int(card.width * 0.5), int(card.height * 0.5)), Image.LANCZOS)
    cx = (blurred.width - card.width) // 2
    cy = (blurred.height - card.height) // 2 + 10
    blurred.alpha_composite(card, (cx, cy))
    framed = rounded(blurred, 10)
    canvas = Image.new("RGBA", (framed.width + 80, framed.height + 80), (0, 0, 0, 0))
    sh2, pad2 = shadow(framed.size, 10, 18, 0.28)
    canvas.alpha_composite(sh2, (40 - pad2, 40 - pad2 + 8))
    canvas.alpha_composite(framed, (40, 40))
    canvas.save(out / name, optimize=True)

showcase("editor-claro.png", "card-light.png", "atualizacao.png", (15, 20, 30, 40))
showcase("editor-escuro.png", "card-dark.png", "atualizacao-escuro.png", (0, 0, 0, 70))

# capturas do editor com moldura arredondada e sombra suave
for src, dst in (("editor-claro.png", "editor-claro.png"), ("editor-escuro.png", "editor-escuro.png")):
    im = rounded(Image.open(cap / src), 10)
    canvas = Image.new("RGBA", (im.width + 80, im.height + 80), (0, 0, 0, 0))
    sh3, pad3 = shadow(im.size, 10, 18, 0.25)
    canvas.alpha_composite(sh3, (40 - pad3, 40 - pad3 + 8))
    canvas.alpha_composite(im, (40, 40))
    canvas.save(out / dst, optimize=True)
print("ok")
