"""按原版格挡图标（游戏包内 res://images/ui/combat/block.png）的画风画混乱值徽章图标：
128×128，半透明黑描边约 3 像素、内侧亮边约 4 像素、左浅右深两色，形状换成左右带尖角的横向六边形（呼应血条两端的尖角），以区别于格挡盾牌、并给数字留出宽度。
4 倍超采样后缩小，得到抗锯齿边缘。需要 Pillow。
用法：python tools/make_stagger_badge_icon.py images/ui/combat/stagger_badge.png"""
import sys
from PIL import Image, ImageDraw, ImageFilter

S = 4
N = 128 * S
OUTLINE = (0, 0, 0, 138)
RIM = (255, 232, 160, 255)
LIGHT = (255, 214, 104, 255)
DARK = (222, 156, 36, 255)
SEAM = (255, 226, 140, 255)

def hexagon(cx, cy, rx, ry):
    tip = ry * 0.55
    return [(cx - rx, cy), (cx - rx + tip, cy - ry), (cx + rx - tip, cy - ry), (cx + rx, cy), (cx + rx - tip, cy + ry), (cx - rx + tip, cy + ry)]

def rounded(poly, radius):
    # 先画多边形再按半径做一次开闭运算，近似圆角。
    mask = Image.new("L", (N, N), 0)
    ImageDraw.Draw(mask).polygon(poly, fill=255)
    if radius > 0:
        mask = mask.filter(ImageFilter.MinFilter(radius * 2 + 1)).filter(ImageFilter.MaxFilter(radius * 2 + 1))
    return mask

cx, cy = 64 * S, 66 * S
rx, ry = 57 * S, 37 * S
outer = rounded(hexagon(cx, cy, rx, ry), 3 * S)
inner = rounded(hexagon(cx, cy, rx - 4 * S, ry - 3 * S), 3 * S)
core = rounded(hexagon(cx, cy, rx - 8 * S, ry - 6 * S), 2 * S)

img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
img.paste(Image.new("RGBA", (N, N), OUTLINE), mask=outer)
img.paste(Image.new("RGBA", (N, N), RIM), mask=inner)

halves = Image.new("RGBA", (N, N), LIGHT)
ImageDraw.Draw(halves).rectangle([cx, 0, N, N], fill=DARK)
ImageDraw.Draw(halves).rectangle([cx - 1 * S, 0, cx + 1 * S, N], fill=SEAM)
img.paste(halves, mask=core)

img.resize((128, 128), Image.LANCZOS).save(sys.argv[1])
