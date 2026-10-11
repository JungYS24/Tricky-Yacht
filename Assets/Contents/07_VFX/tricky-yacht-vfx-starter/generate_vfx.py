"""Offline PNG authoring tool. Requires Pillow; not a Unity runtime script."""
from pathlib import Path
import math
import zipfile
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "png"
OUT.mkdir(exist_ok=True)
ASSETS = []


def save(name, image):
    assert image.mode == "RGBA"
    image.save(OUT / (name + ".png"))
    ASSETS.append((name, image))


def polygon_asset(name, size, points, center, layers, grid=2):
    im = Image.new("RGBA", (size[0] // grid, size[1] // grid))
    draw = ImageDraw.Draw(im)
    cx, cy = center
    for scale, color in layers:
        coords = [(round((cx + (x-cx)*scale)/grid), round((cy + (y-cy)*scale)/grid)) for x, y in points]
        draw.polygon(coords, fill=color)
    save(name, im.resize(size, Image.Resampling.NEAREST))


cream = [(1, (255, 194, 92, 220)), (.83, (255, 234, 172, 255)), (.53, (255, 255, 242, 255))]
polygon_asset("hit_flash_01", (64,64), [(30,10),(35,24),(46,14),(40,28),(54,31),(40,36),(48,49),(35,41),(30,54),(26,40),(12,46),(22,34),(10,27),(25,27)], (31,32), cream)
polygon_asset("hit_flash_02", (64,64), [(14,12),(30,25),(37,10),(38,26),(54,20),(43,32),(52,43),(37,39),(38,54),(29,42),(14,52),(23,36),(10,32),(24,28)], (32,33), cream)
polygon_asset("hit_flash_03", (64,64), [(24,10),(33,25),(50,12),(41,28),(54,34),(40,37),(48,52),(33,43),(26,54),(25,40),(10,44),(21,33),(12,19),(27,26)], (32,33), cream)

# A tapered crescent on a 2px grid, rather than a rotated rectangle.
im = Image.new("RGBA", (64,32))
d = ImageDraw.Draw(im)
outer = [(10,24),(15,18),(22,13),(30,9),(39,7),(49,6),(54,7),(46,9),(38,12),(31,15),(23,18),(16,22)]
d.polygon(outer, fill=(255,204,119,235))
d.polygon([(11,24),(19,17),(28,12),(39,9),(51,7),(43,10),(34,13),(25,16),(18,20)], fill=(255,255,240,255))
save("slash_arc", im.resize((128,64), Image.Resampling.NEAREST))

for name, pts in [
    ("shard_long", [(9,3),(11,6),(7,13),(5,10)]),
    ("shard_triangle", [(4,4),(12,7),(7,12)]),
    ("shard_chip", [(5,4),(11,5),(12,9),(8,12),(4,9)]),
]:
    polygon_asset(name, (16,16), pts, (8,8), [(1,(200,211,223,255)),(.60,(255,255,255,255))], grid=1)

# Smooth grayscale assets can be tinted cyan, violet, gold, or orange in Unity.
for name in ("glow_soft", "shockwave_ring"):
    im = Image.new("RGBA", (128,128))
    px = im.load()
    for y in range(128):
        for x in range(128):
            r = math.hypot(x-63.5, y-63.5)
            if name == "glow_soft":
                alpha = max(0, (math.exp(-3*(r/44)**2)-math.exp(-3))/(1-math.exp(-3))) if r < 44 else 0
            else:
                dist = abs(r-37)
                alpha = min(1, .95*math.exp(-(dist/1.05)**2) + .20*math.exp(-(dist/3.1)**2)) if dist < 7 else 0
            px[x,y] = (255,255,255,round(alpha*255)) if alpha > 0 else (0,0,0,0)
    save(name, im)

polygon_asset("sparkle_cross", (32,32), [(16,5),(18,13),(26,16),(18,18),(16,27),(13,18),(5,16),(13,13)], (16,16), [(1,(220,231,255,190)),(.72,(255,255,255,255))], grid=1)
polygon_asset("ember_01", (16,16), [(9,3),(10,6),(12,8),(11,11),(8,13),(5,11),(4,8),(7,7)], (8,9), [(1,(255,91,25,220)),(.72,(255,175,40,255)),(.38,(255,249,175,255))], grid=1)

# Preview only: opaque checkerboard and labels are not part of the asset PNGs.
sheet = Image.new("RGB", (1000, 1010), (23,27,33))
d = ImageDraw.Draw(sheet)
font_path = Path("C:/Windows/Fonts/consola.ttf")
font = ImageFont.truetype(str(font_path), 17) if font_path.exists() else ImageFont.load_default()
small = ImageFont.truetype(str(font_path), 13) if font_path.exists() else font
d.text((22,14), "TRICKY YACHT | PROCEDURAL VFX STARTER | 11 RGBA PNGs", fill="white", font=font)
for i, (name, im) in enumerate(ASSETS):
    col, row = i % 3, i // 3
    x, y = 16+col*330, 52+row*237
    for yy in range(y,y+184,16):
        for xx in range(x,x+316,16):
            c = (40,50,48) if ((xx-x)//16+(yy-y)//16)%2 else (48,59,55)
            d.rectangle((xx,yy,min(xx+15,x+315),min(yy+15,y+183)), fill=c)
    factor = 6 if im.width==16 else 4 if im.width==32 else 2 if im.width==64 else 1
    big = im.resize((im.width*factor,im.height*factor),Image.Resampling.NEAREST)
    sheet.paste(big,(x+158-big.width//2,y+92-big.height//2),big)
    d.text((x,y+191), name, fill=(237,242,247),font=font)
    d.text((x,y+214), f"{im.width}x{im.height} | preview {factor}x",fill=(163,183,193),font=small)
sheet.save(ROOT/"preview.png")

for name, im in ASSETS:
    bbox = im.getbbox()
    assert bbox and bbox[0] > 0 and bbox[1] > 0 and bbox[2] < im.width and bbox[3] < im.height, name
    assert im.getchannel("A").getextrema()[0] == 0, name
print(f"Verified {len(ASSETS)} RGBA PNGs: dimensions, transparent edges, nonempty artwork.")
with zipfile.ZipFile(ROOT/"tricky-yacht-vfx-starter.zip", "w", zipfile.ZIP_DEFLATED) as archive:
    for f in sorted(OUT.glob("*.png")):
        archive.write(f, "png/"+f.name)
    for f in (ROOT/"generate_vfx.py", ROOT/"preview.png", ROOT/"README.md"):
        if f.exists():
            archive.write(f, f.name)
print(ROOT)
