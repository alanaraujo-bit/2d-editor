"""Botão "Baixar para Windows" do README (renderizado em 2x).
Uso: python mkbutton.py <arquivo-saida.png>"""
import sys
from PIL import Image, ImageDraw, ImageFont

S = 3
W, H, R = 300 * S, 56 * S, 12 * S
img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
d.rounded_rectangle((0, 0, W - 1, H - 1), radius=R, fill=(0, 90, 171, 255))

# logo do Windows (4 quadrados)
q, g = 8 * S, 2 * S
x0, y0 = 30 * S, (H - (2 * q + g)) // 2
for dx in (0, q + g):
    for dy in (0, q + g):
        d.rectangle((x0 + dx, y0 + dy, x0 + dx + q - 1, y0 + dy + q - 1), fill=(255, 255, 255, 255))

f = ImageFont.truetype("C:/Windows/Fonts/seguisb.ttf", 19 * S)
text = "Baixar para Windows"
tw = d.textlength(text, font=f)
tx = x0 + 2 * q + g + 14 * S
asc, desc = f.getmetrics()
d.text((tx, (H - (asc + desc)) // 2 - 1 * S), text, font=f, fill=(255, 255, 255, 255))

# ajusta a largura ao conteúdo (margem simétrica)
right = int(tx + tw + 30 * S)
img = img.crop((0, 0, right, H))
mask = Image.new("L", img.size, 0)
ImageDraw.Draw(mask).rounded_rectangle((0, 0, img.width - 1, H - 1), radius=R, fill=255)
out = Image.new("RGBA", img.size, (0, 0, 0, 0))
out.paste(img, (0, 0), mask)
out = out.resize((out.width * 2 // S, out.height * 2 // S), Image.LANCZOS)
out.save(sys.argv[1], optimize=True)
print(out.size)
