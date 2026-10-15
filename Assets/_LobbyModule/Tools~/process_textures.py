"""
ACCERTONE Lobby Module - texture processing (run with Python 3 + Pillow + numpy).

Produces de-branded, ACCERTONE-styled textures from the imported model maps:
  * Car: tyre sidewall (Accert markings), rims, driver nameplate, chassis plate, blank decal sheet,
         logo masks used by the in-editor livery bake.
  * Drivers: URP metallic/smoothness maps (glossy helmet + visor, cloth suit), fabric mask,
             tileable fabric-weave detail normal.

This folder ends with "~" so Unity ignores it.
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
TEX = os.path.join(ROOT, "Textures")
CAR_SRC = os.path.join(TEX, "Car", "Source")
CAR_OUT = os.path.join(TEX, "Car")
LOGOS = os.path.join(TEX, "Car", "Livery")
DRIVERS = os.path.join(TEX, "Drivers")
FONTS = os.path.abspath(os.path.join(ROOT, "..", "ACCERTONE", "Art", "Fonts", "Source"))

DISPLAY = os.path.join(FONTS, "Roboto-BlackItalic.ttf")
HEAVY = os.path.join(FONTS, "Roboto-Black.ttf")
BOLD = os.path.join(FONTS, "Roboto-Bold.ttf")
LABEL = os.path.join(FONTS, "Inter-SemiBold.ttf")

# Livery palette (from the reference render).
MAGENTA = (232, 64, 214)
VIOLET = (124, 58, 255)
DEEP_VIOLET = (52, 20, 110)
WHITE = (243, 245, 248)


def font(path, size):
    return ImageFont.truetype(path, size)


def fit_text(draw, text, path, box_w, box_h, tracking=0.0):
    """Largest font size whose cap height fits box_h and width fits box_w."""
    size = int(box_h * 1.45)
    while size > 6:
        f = font(path, size)
        w = text_width(draw, text, f, tracking)
        bbox = draw.textbbox((0, 0), "H", font=f)
        cap = bbox[3] - bbox[1]
        if w <= box_w and cap <= box_h:
            return f
        size -= 1
    return font(path, 6)


def text_width(draw, text, f, tracking):
    w = 0
    for i, ch in enumerate(text):
        w += draw.textlength(ch, font=f)
        if i < len(text) - 1:
            w += tracking * f.size
    return w


def draw_tracked(draw, xy, text, f, fill, tracking=0.0):
    x, y = xy
    for ch in text:
        draw.text((x, y), ch, font=f, fill=fill)
        x += draw.textlength(ch, font=f) + tracking * f.size


def text_image(text, path, height_px, tracking=0.0, pad=0.12):
    """White-on-transparent text mask, tightly cropped with padding."""
    f = font(path, height_px)
    tmp = Image.new("L", (10, 10))
    d = ImageDraw.Draw(tmp)
    w = int(text_width(d, text, f, tracking)) + height_px
    h = int(height_px * 1.6)
    img = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(img)
    draw_tracked(d, (height_px * 0.3, 0), text, f, 255, tracking)
    bbox = img.getbbox()
    img = img.crop(bbox)
    p = int(img.height * pad)
    out = Image.new("L", (img.width + 2 * p, img.height + 2 * p), 0)
    out.paste(img, (p, p))
    rgba = Image.new("RGBA", out.size, (255, 255, 255, 0))
    rgba.putalpha(out)
    return rgba


def slash_mark(height_px):
    """The Accert one double forward-leaning slash (brand mark)."""
    h = height_px
    skew = int(h * 0.27)
    a_w = skew + int(h * 0.27)
    b_x = a_w - skew + int(h * 0.12)
    b_w = skew + int(h * 0.15)
    w = b_x + b_w + 4
    img = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(img)
    d.polygon([(0, h), (skew, 0), (a_w, 0), (a_w - skew, h)], fill=255)
    d.polygon([(b_x, h), (b_x + skew, 0), (b_x + b_w, 0), (b_x + b_w - skew, h)], fill=255)
    rgba = Image.new("RGBA", img.size, (255, 255, 255, 0))
    rgba.putalpha(img)
    return rgba


# --------------------------------------------------------------------------- car

def tyre_sidewall():
    src = Image.open(os.path.join(CAR_SRC, "TIRE_SIDES_BaseColor.png")).convert("RGB")
    a = np.asarray(src).astype(np.float32)
    lum = a.mean(axis=2)
    out = a.copy()
    bright = lum > 120
    rows = np.where(bright.any(axis=1))[0]
    groups, start, prev = [], rows[0], rows[0]
    for r in rows[1:]:
        if r - prev > 3:
            groups.append((start, prev))
            start = r
        prev = r
    groups.append((start, prev))

    img = Image.fromarray(out.astype(np.uint8))
    draw = ImageDraw.Draw(img)
    rubber = tuple(int(v) for v in np.median(a[200:400, 200:400].reshape(-1, 3), axis=0))
    for gi, (y0, y1) in enumerate(groups):
        band = bright[y0:y1 + 1]
        cols = np.where(band.any(axis=0))[0]
        segs, s, pv = [], cols[0], cols[0]
        for c in cols[1:]:
            if c - pv > 20:
                segs.append((s, pv))
                s = c
            pv = c
        segs.append((s, pv))
        flipped = gi == 1  # the middle strip is stored rotated 180 degrees
        for (x0, x1) in segs:
            width = x1 - x0 + 1
            sub = band[:, x0:x1 + 1]
            ys = np.where(sub.any(axis=1))[0]
            sy0, sy1 = y0 + ys[0], y0 + ys[-1]
            if width >= 440:
                # Compound stripe: recolour bright pixels magenta, keep the anti-aliased shape.
                region = out[sy0 - 2:sy1 + 3, x0 - 2:x1 + 3]
                l = region.mean(axis=2, keepdims=True)
                t = np.clip((l - rubber[0]) / (230.0 - rubber[0]), 0, 1)
                region[:] = region * (1 - t) + np.array(MAGENTA, np.float32) * t
                img = Image.fromarray(out.astype(np.uint8))
                draw = ImageDraw.Draw(img)
            else:
                # Brand text: erase with the locally sampled rubber tone, then write ACCERT
                # (or the slash mark in the small slots).
                ring = np.concatenate([a[max(0, sy0 - 16):sy0 - 9, x0:x1 + 1].reshape(-1, 3),
                                       a[sy1 + 9:sy1 + 16, x0:x1 + 1].reshape(-1, 3)])
                local = tuple(int(v) for v in np.median(ring, axis=0)) if len(ring) else rubber
                draw.rectangle([x0 - 6, sy0 - 6, x1 + 6, sy1 + 6], fill=local)
                out = np.asarray(img).astype(np.float32).copy()
                box_w, box_h = width, sy1 - sy0 + 1
                if width >= 150:
                    label = text_image("ACCERT", DISPLAY, 200, tracking=0.04)
                    scale = min(box_w * 0.96 / label.width, box_h * 0.86 / label.height)
                    label = label.resize((max(1, int(label.width * scale)), max(1, int(label.height * scale))), Image.LANCZOS)
                else:
                    label = slash_mark(200)
                    scale = box_h * 0.8 / label.height
                    label = label.resize((max(1, int(label.width * scale)), max(1, int(label.height * scale))), Image.LANCZOS)
                if flipped:
                    label = label.rotate(180)
                tint = Image.new("RGBA", label.size, MAGENTA + (255,))
                tint.putalpha(label.getchannel("A"))
                px = x0 + (box_w - label.width) // 2
                py = sy0 + (box_h - label.height) // 2
                img.paste(tint, (px, py), tint)
                out = np.asarray(img).astype(np.float32).copy()
                draw = ImageDraw.Draw(img)
    img = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))
    img.save(os.path.join(CAR_OUT, "T_Car_Tyre_Sidewall.png"))
    print("tyre sidewall: %d strips" % len(groups))


def rims():
    src = Image.open(os.path.join(CAR_SRC, "discs_BaseColor.png")).convert("RGB")
    hsv = np.asarray(src.convert("HSV")).astype(np.float32)
    rgb = np.asarray(src).astype(np.float32)
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    # Warm saturated accents (yellow / gold / red) become the livery magenta-violet.
    warm = ((h < 50) | (h > 230)) & (s > 90) & (v > 60)
    t = np.clip((s - 90) / 80.0, 0, 1) * warm
    target = np.where((h < 25)[..., None] | (h > 230)[..., None], np.array(VIOLET, np.float32), np.array(MAGENTA, np.float32))
    shade = (v / 255.0)[..., None]
    out = rgb * (1 - t[..., None]) + target * shade * t[..., None]
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(os.path.join(CAR_OUT, "T_Car_Wheel.png"))
    print("rims recoloured")


def driver_nameplate():
    src = Image.open(os.path.join(CAR_SRC, "driver_number_BaseColor.png")).convert("RGB")
    w, h = src.size
    img = Image.new("RGB", (w, h), (0, 0, 0))
    d = ImageDraw.Draw(img)
    # Original layout: name block top-left (~0..900 x 0..280), flag (~1100..1250), code (~1500..2048).
    small = font(LABEL, 70)
    d.text((80, 36), "Accert one", font=small, fill=(180, 186, 196))
    big = font(HEAVY, 150)
    d.text((70, 110), "DRIVER 01", font=big, fill=WHITE)
    mark = slash_mark(170)
    tint = Image.new("RGBA", mark.size, MAGENTA + (255,))
    tint.putalpha(mark.getchannel("A"))
    img.paste(tint, (1080, 70), tint)
    code = font(HEAVY, 150)
    d.text((1460, 110), "ACC13", font=code, fill=WHITE)
    img.save(os.path.join(CAR_OUT, "T_Car_DriverNameplate.png"))
    print("nameplate replaced")


def chassis_plate():
    w = h = 1024
    img = Image.new("RGB", (w, h), (24, 22, 32))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([40, 40, w - 40, h - 40], radius=40, outline=VIOLET, width=14)
    d.rectangle([90, 230, w - 90, 860], fill=(232, 233, 238))
    d.text((90, 90), "Accert one", font=font(DISPLAY, 92), fill=WHITE)
    d.text((660, 98), "2026", font=font(HEAVY, 86), fill=MAGENTA)
    rows = [("EVENT", "GRAND PRIX"), ("CAR", "Accert one Works"), ("CHASSIS", "AC-26 / 01"), ("ENTRANT", "DRIVER 01  #13")]
    y = 280
    for k, v in rows:
        d.text((130, y), k, font=font(LABEL, 46), fill=(70, 72, 84))
        d.text((420, y), v, font=font(BOLD, 50), fill=(20, 20, 28))
        y += 135
    img.save(os.path.join(CAR_OUT, "T_Car_ChassisPlate.png"))
    print("chassis plate replaced")


def blank_decal():
    Image.new("RGBA", (64, 64), (0, 0, 0, 0)).save(os.path.join(CAR_OUT, "T_Car_Decal_Blank.png"))


def logo_masks():
    os.makedirs(LOGOS, exist_ok=True)
    text_image("Accert one", DISPLAY, 360, tracking=0.015).save(os.path.join(LOGOS, "Logo_Wordmark.png"))
    text_image("OctaDezx", LABEL, 320, tracking=0.0).save(os.path.join(LOGOS, "Logo_OctaDezx.png"))
    text_image("Dezxcorp", LABEL, 320, tracking=0.0).save(os.path.join(LOGOS, "Logo_Dezxcorp.png"))
    text_image("Zeriotic", LABEL, 320, tracking=0.0).save(os.path.join(LOGOS, "Logo_Zeriotic.png"))
    text_image("13", DISPLAY, 420, tracking=0.0).save(os.path.join(LOGOS, "Logo_13.png"))
    slash_mark(512).save(os.path.join(LOGOS, "Logo_Mark.png"))
    print("logo masks written")


# --------------------------------------------------------------------------- drivers

def fabric_weave_normal(size=256, threads=16):
    """Tileable plain-weave height field -> tangent-space normal map."""
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    u = x * threads * 2 * math.pi
    v = y * threads * 2 * math.pi
    warp = (np.sin(u) * 0.5 + 0.5) * (np.cos(v * 0.5) ** 2)
    weft = (np.sin(v) * 0.5 + 0.5) * (np.sin(u * 0.5) ** 2)
    hgt = warp + weft
    dx = np.roll(hgt, -1, axis=1) - np.roll(hgt, 1, axis=1)
    dy = np.roll(hgt, -1, axis=0) - np.roll(hgt, 1, axis=0)
    strength = 2.2
    n = np.stack([-dx * strength, -dy * strength, np.ones_like(hgt)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    rgb = ((n * 0.5 + 0.5) * 255).astype(np.uint8)
    Image.fromarray(rgb).save(os.path.join(DRIVERS, "T_Fabric_Weave_Normal.png"))
    print("fabric weave normal written")


def driver_maps(name):
    folder = os.path.join(DRIVERS, name)
    mr = np.asarray(Image.open(os.path.join(folder, name + "_MetalRough_Source.jpg")).convert("RGB")).astype(np.float32) / 255.0
    rough = mr[..., 1]
    metal = mr[..., 2]
    smooth = 1.0 - rough
    # Glossy islands (helmet shell, visor) are already low-roughness in the source; push them to a
    # lacquered finish and pull the cloth down to a soft fabric response.
    gloss = np.clip((smooth - 0.55) / 0.2, 0, 1)
    cloth_smooth = np.clip(0.18 + (smooth - 0.3) * 0.5, 0.12, 0.34)
    out_smooth = cloth_smooth * (1 - gloss) + 0.93 * gloss
    out_metal = np.clip(metal * 0.6, 0, 1)
    ms = np.zeros(mr.shape[:2] + (4,), np.uint8)
    ms[..., 0] = (out_metal * 255).astype(np.uint8)
    ms[..., 1] = ms[..., 0]
    ms[..., 2] = ms[..., 0]
    ms[..., 3] = (out_smooth * 255).astype(np.uint8)
    Image.fromarray(ms, "RGBA").save(os.path.join(folder, name + "_MetallicSmoothness.png"))
    # Fabric mask (detail normal applies to cloth only), stored in alpha as URP expects.
    fabric = ((1 - gloss) * 255).astype(np.uint8)
    fm = Image.fromarray(fabric, "L").filter(ImageFilter.GaussianBlur(2))
    rgba = Image.new("RGBA", fm.size, (255, 255, 255, 255))
    rgba.putalpha(fm)
    rgba.save(os.path.join(folder, name + "_FabricMask.png"))
    print("%s: gloss coverage %.1f%%" % (name, gloss.mean() * 100))


if __name__ == "__main__":
    os.makedirs(CAR_OUT, exist_ok=True)
    tyre_sidewall()
    rims()
    driver_nameplate()
    chassis_plate()
    blank_decal()
    logo_masks()
    fabric_weave_normal()
    for d in ("Driver_01_Hero", "Driver_02_Horizon", "Driver_03_Velocity"):
        driver_maps(d)
