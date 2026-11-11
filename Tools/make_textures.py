"""
Generates the tileable surface textures used by the game (Assets/Resources/Textures/*.bytes).

Each file is a 512x512 PNG stored with a .bytes extension, so Unity loads it as raw data with no import
settings to get wrong. RGB is a mostly neutral detail colour that the game tints with each object's colour,
and the alpha channel is a height map that the game turns into a normal map at load time.

Run:  python3 Tools/make_textures.py   (needs numpy and pillow)
"""
import io
import os

import numpy as np
from PIL import Image

N = 512
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Textures")
rng = np.random.default_rng(1700)


# ---------- tileable building blocks ----------

def fbm(scale, octaves=5, persistence=0.5, n=N, seed=None):
    """Tileable fractal noise in 0..1, made by filtering white noise in frequency space (periodic by construction)."""
    r = np.random.default_rng(seed) if seed is not None else rng
    fx = np.fft.fftfreq(n)[None, :]
    fy = np.fft.fftfreq(n)[:, None]
    f = np.sqrt(fx * fx + fy * fy) * n
    out = np.zeros((n, n))
    amp, total = 1.0, 0.0
    base = scale
    for _ in range(octaves):
        spec = np.fft.fft2(r.standard_normal((n, n)))
        band = np.exp(-((f - base) ** 2) / (2 * (base * 0.6 + 0.5) ** 2))
        layer = np.real(np.fft.ifft2(spec * band))
        layer /= layer.std() + 1e-9
        out += layer * amp
        total += amp
        amp *= persistence
        base *= 2.0
    out /= total
    out = (out - out.min()) / (out.max() - out.min())
    return out


def voronoi(cells, n=N, seed=None, jitter=0.8):
    """Tileable Voronoi: distance to nearest and second nearest point, and the id of the nearest cell."""
    r = np.random.default_rng(seed) if seed is not None else rng
    g = cells
    pts = (np.stack(np.meshgrid(np.arange(g), np.arange(g)), -1).reshape(-1, 2) + 0.5 +
           (r.random((g * g, 2)) - 0.5) * jitter) / g
    ys, xs = np.mgrid[0:n, 0:n] / n
    d1 = np.full((n, n), 9.0)
    d2 = np.full((n, n), 9.0)
    ids = np.zeros((n, n), dtype=int)
    for i, (px, py) in enumerate(pts):
        dx = np.abs(xs - px); dx = np.minimum(dx, 1 - dx)
        dy = np.abs(ys - py); dy = np.minimum(dy, 1 - dy)
        d = np.sqrt(dx * dx + dy * dy)
        closer = d < d1
        d2 = np.where(closer, d1, np.minimum(d2, d))
        ids = np.where(closer, i, ids)
        d1 = np.where(closer, d, d1)
    return d1 * g, d2 * g, ids


def norm01(a):
    return (a - a.min()) / (a.max() - a.min() + 1e-9)


def rgb(gray, tint=(1, 1, 1), var=None, var_tint=(1, 1, 1), amount=0.0):
    """Grey detail with an optional second tint mixed in where var is high (for subtle colour variation)."""
    c = np.stack([gray * t for t in tint], -1)
    if var is not None:
        c2 = np.stack([gray * t for t in var_tint], -1)
        c = c * (1 - var[..., None] * amount) + c2 * var[..., None] * amount
    return c


def save(name, color, height):
    color = np.clip(color, 0, 1)
    height = np.clip(height, 0, 1)
    rgba = np.concatenate([color, height[..., None]], -1)
    img = Image.fromarray((rgba * 255 + 0.5).astype(np.uint8), "RGBA")
    buf = io.BytesIO()
    img.save(buf, "PNG", optimize=True)
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, name + ".bytes"), "wb") as f:
        f.write(buf.getvalue())
    # A preview copy for people (not used by the game).
    prev = os.environ.get("TEX_PREVIEW")
    if prev:
        os.makedirs(prev, exist_ok=True)
        Image.fromarray((color * 255).astype(np.uint8), "RGB").save(os.path.join(prev, name + ".png"))
    print(name, "mean", color.mean(axis=(0, 1)).round(3), len(buf.getvalue()) // 1024, "KB")


def bricks(rows, cols, mortar, seed, offset=0.5, jitter=0.0):
    """Tileable running-bond bricks. Returns (inside 0..1 bevel, brick id)."""
    ys, xs = np.mgrid[0:N, 0:N] / N
    row = np.floor(ys * rows).astype(int)
    xo = xs * cols + (row % 2) * offset * 1.0
    col = np.floor(xo).astype(int) % cols
    fx = xo - np.floor(xo)
    fy = ys * rows - row
    edge = np.minimum(np.minimum(fx, 1 - fx) / cols * rows, np.minimum(fy, 1 - fy))  # in row-height units
    bevel = np.clip((edge - mortar) / (mortar * 1.5 + 1e-6), 0, 1)
    ids = row * 131 + col
    return bevel, ids


def per_id(ids, lo, hi, seed):
    r = np.random.default_rng(seed)
    table = r.uniform(lo, hi, ids.max() + 1)
    return table[ids]


# ---------- surfaces ----------

def grass():
    n1 = fbm(6); n2 = fbm(40, 3); blades = fbm(140, 2)
    h = 0.5 * blades + 0.3 * n2 + 0.2 * n1
    g = 0.6 + 0.4 * (0.45 * n1 + 0.55 * blades)
    dry = np.clip((fbm(4) - 0.55) * 4, 0, 1)
    c = rgb(g, (0.95, 1.05, 0.9), dry, (1.25, 1.1, 0.7), 0.6)
    save("grass", c, h)


def dirt():
    n1 = fbm(5); n2 = fbm(30, 4); pebbles = voronoi(40, seed=3)[0]
    peb = np.clip(1 - pebbles * 2.2, 0, 1) * (fbm(20) > 0.55)
    h = 0.5 * n2 + 0.3 * n1 + 0.4 * peb
    g = 0.7 + 0.25 * n1 + 0.15 * n2 + 0.1 * peb
    c = rgb(g, (1.05, 1.0, 0.92))
    save("dirt", c, norm01(h))


def cobble():
    d1, d2, ids = voronoi(9, seed=5, jitter=0.7)
    gap = np.clip((d2 - d1) * 3.0, 0, 1)
    stone = np.sqrt(gap) * (0.85 + 0.15 * fbm(30, 3))
    shade = per_id(ids, 0.75, 1.05, 6)
    g = np.where(gap > 0.08, (0.62 + 0.3 * fbm(25, 3)) * shade, 0.38 + 0.15 * fbm(60, 2))
    c = rgb(g, (1.0, 0.98, 0.95))
    save("cobble", c, stone)


def flagstone():
    bevel, ids = bricks(4, 3, 0.04, 7, offset=0.37)
    n = fbm(20, 4); cracks = np.clip(1 - np.abs(fbm(12, 3) - 0.5) * 30, 0, 1) * 0.6
    shade = per_id(ids, 0.75, 1.05, 8)
    h = bevel * (0.75 + 0.25 * n) - cracks * 0.3 * bevel
    g = (0.55 + 0.35 * n) * shade * (0.55 + 0.45 * bevel) * (1 - cracks * 0.3)
    moss = np.clip((fbm(6) - 0.6) * 3, 0, 1) * (1 - bevel * 0.7)
    c = rgb(g, (1, 1, 1), moss, (0.75, 1.0, 0.6), 0.6)
    save("flagstone", c, norm01(h))


def brick():
    bevel, ids = bricks(10, 4, 0.08, 9)
    n = fbm(40, 3); big = fbm(5)
    shade = per_id(ids, 0.7, 1.08, 10)
    chips = np.clip((fbm(70, 2) - 0.7) * 5, 0, 1)
    h = bevel * (0.8 + 0.2 * n) - chips * 0.3
    g = np.where(bevel > 0.05, (0.62 + 0.3 * n) * shade * (0.8 + 0.2 * big), 0.42 + 0.1 * n)
    g = g * (1 - chips * 0.25)
    grime = np.clip((fbm(4) - 0.5) * 2, 0, 1)
    c = rgb(g, (1.0, 0.97, 0.93), grime, (0.7, 0.75, 0.65), 0.4)
    save("brick", c, norm01(h))


def stone():
    n1 = fbm(4); n2 = fbm(18, 4); n3 = fbm(70, 2)
    d1, d2, _ = voronoi(6, seed=11)
    facets = np.clip((d2 - d1) * 2, 0, 1)
    h = 0.4 * n2 + 0.3 * facets + 0.2 * n1 + 0.1 * n3
    g = 0.6 + 0.25 * n2 + 0.15 * n3 - 0.15 * (1 - facets)
    lich = np.clip((fbm(8) - 0.62) * 4, 0, 1)
    c = rgb(g, (1, 1, 0.98), lich, (0.95, 1.0, 0.7), 0.4)
    save("stone", c, norm01(h))


def wood():
    ys, xs = np.mgrid[0:N, 0:N] / N
    planks = 4
    pid = np.floor(xs * planks).astype(int)
    fx = xs * planks - pid
    gap = np.clip(np.minimum(fx, 1 - fx) * 40, 0, 1)
    warp = fbm(3, 3) * 1.6 + per_id(pid, 0, 50, 12)
    grain = 0.5 + 0.5 * np.sin((xs * planks * 9 + warp) * 2 * np.pi)
    fine = fbm(200, 2)
    knots_d, _, _ = voronoi(5, seed=13)
    knot = np.clip(1 - knots_d * 5, 0, 1) ** 2
    shade = per_id(pid, 0.8, 1.05, 14)
    g = (0.62 + 0.18 * grain + 0.12 * fine - 0.3 * knot) * shade * (0.6 + 0.4 * gap)
    h = gap * (0.7 + 0.15 * grain + 0.15 * fine)
    c = rgb(g, (1.05, 1.0, 0.92))
    save("wood", c, h)


def bark():
    ys, xs = np.mgrid[0:N, 0:N] / N
    ridges = fbm(12, 3)
    stretched = np.array(Image.fromarray((ridges * 255).astype(np.uint8)).resize((N // 6, N)).resize((N, N))) / 255
    v = 0.5 + 0.5 * np.sin((xs * 14 + fbm(3, 2) * 3) * 2 * np.pi)
    h = norm01(0.6 * v + 0.6 * stretched + 0.2 * fbm(60, 2))
    g = 0.5 + 0.45 * h
    save("bark", rgb(g, (1.05, 1.0, 0.93)), h)


def plaster():
    n1 = fbm(3); n2 = fbm(25, 4); n3 = fbm(120, 2)
    stain = np.clip((fbm(2.5) - 0.55) * 2.5, 0, 1)
    h = 0.5 * n2 + 0.3 * n3 + 0.2 * n1
    g = 0.86 + 0.08 * n2 + 0.06 * n3 - 0.08 * stain
    c = rgb(g, (1, 0.99, 0.96), stain, (0.9, 0.87, 0.78), 0.35)
    save("plaster", c, norm01(h))


def thatch():
    ys, xs = np.mgrid[0:N, 0:N] / N
    straws = fbm(90, 2)
    s2 = np.array(Image.fromarray((straws * 255).astype(np.uint8)).resize((N, N // 10)).resize((N, N))) / 255
    rows = 6
    fy = ys * rows - np.floor(ys * rows)
    layer = np.clip(fy * 1.2, 0, 1)
    h = norm01(0.6 * s2 + 0.5 * layer + 0.2 * fbm(10))
    g = 0.55 + 0.4 * s2 * (0.5 + 0.5 * layer)
    dark = np.clip((fbm(5) - 0.5) * 2, 0, 1)
    c = rgb(g, (1.05, 1.0, 0.85), dark, (0.75, 0.7, 0.6), 0.5)
    save("thatch", c, h)


def weave(name, threads, depth, base, tint):
    ys, xs = np.mgrid[0:N, 0:N] / N
    wx = np.sin(xs * threads * 2 * np.pi)
    wy = np.sin(ys * threads * 2 * np.pi)
    over = (np.floor(xs * threads * 2) + np.floor(ys * threads * 2)) % 2
    h = np.where(over > 0, 0.5 + 0.5 * np.abs(wx), 0.5 + 0.5 * np.abs(wy))
    fuzz = fbm(150, 2)
    slub = fbm(8)
    g = base + (1 - base) * (depth * h + (1 - depth) * fuzz) * (0.85 + 0.15 * slub)
    save(name, rgb(g, tint), norm01(h * 0.8 + fuzz * 0.2))


def leather():
    d1, d2, _ = voronoi(60, seed=17)
    crease = np.clip((d2 - d1) * 4, 0, 1)
    n = fbm(6); scuff = np.clip((fbm(10) - 0.6) * 3, 0, 1)
    h = norm01(crease * 0.6 + fbm(40, 3) * 0.4)
    g = 0.7 + 0.15 * crease + 0.15 * n + 0.2 * scuff
    save("leather", rgb(g, (1.04, 1.0, 0.95)), h)


def metal():
    ys, xs = np.mgrid[0:N, 0:N] / N
    brushed = fbm(200, 2)
    b2 = np.array(Image.fromarray((brushed * 255).astype(np.uint8)).resize((N, N // 16)).resize((N, N))) / 255
    dents = fbm(8, 3)
    rust = np.clip((fbm(5) - 0.7) * 3, 0, 1) * (0.5 + 0.5 * fbm(40, 2))
    g = 0.78 + 0.15 * b2 + 0.07 * dents - 0.2 * rust
    h = norm01(0.4 * dents + 0.1 * b2 - 0.3 * rust)
    c = rgb(g, (1, 1, 1), rust, (1.15, 0.85, 0.7), 0.5)
    save("metal", c, h)


def skin():
    pores = fbm(180, 2)
    mottle = fbm(10, 3)
    veins = np.clip(1 - np.abs(fbm(6, 3) - 0.5) * 25, 0, 1)
    h = norm01(pores * 0.7 + fbm(40, 2) * 0.3)
    g = 0.9 + 0.06 * pores + 0.04 * mottle
    blush = np.clip((mottle - 0.45) * 2, 0, 1)
    c = rgb(g, (1, 1, 1), blush, (1.06, 0.92, 0.9), 0.5)
    c = c * (1 - veins[..., None] * np.array([0.03, 0.02, -0.01]))
    save("skin", c, h)


def scales():
    d1, d2, ids = voronoi(28, seed=19, jitter=0.6)
    edge = np.clip((d2 - d1) * 3, 0, 1)
    dome = np.clip(1 - d1 * 1.2, 0, 1)
    warts_d, _, _ = voronoi(9, seed=20)
    wart = np.clip(1 - warts_d * 3, 0, 1) ** 1.5 * (fbm(9) > 0.45)
    shade = per_id(ids, 0.8, 1.05, 21)
    h = norm01(dome * edge * 0.7 + wart * 0.6)
    g = (0.65 + 0.3 * dome * edge) * shade + 0.1 * wart
    save("scales", rgb(g), h)


def carapace():
    n = fbm(5, 4); fine = fbm(80, 2)
    plates, _ = bricks(3, 2, 0.05, 23, offset=0.5)
    h = norm01(plates * 0.6 + n * 0.3 + fine * 0.1)
    g = 0.6 + 0.3 * n + 0.1 * fine
    save("carapace", rgb(g) * (0.75 + 0.25 * plates[..., None]), h)


def hair():
    s = fbm(160, 2)
    st = np.array(Image.fromarray((s * 255).astype(np.uint8)).resize((N, N // 24)).resize((N, N))) / 255
    clumps = np.array(Image.fromarray((fbm(20, 2) * 255).astype(np.uint8)).resize((N, N // 8)).resize((N, N))) / 255
    h = norm01(st * 0.7 + clumps * 0.3)
    g = 0.6 + 0.4 * h
    save("hair", rgb(g), h)


def plain():
    n = fbm(6); f = fbm(60, 3)
    g = 0.88 + 0.07 * n + 0.05 * f
    save("plain", rgb(g), norm01(0.5 * n + 0.5 * f))


def foliage():
    d1, d2, ids = voronoi(26, seed=29, jitter=0.9)
    leaf = np.clip(1 - d1 * 1.6, 0, 1)
    shade = per_id(ids, 0.6, 1.1, 30)
    h = norm01(leaf * (0.6 + 0.4 * shade))
    g = (0.45 + 0.5 * leaf) * shade
    var = np.clip(per_id(ids, -0.5, 1.5, 31), 0, 1)
    c = rgb(g, (0.95, 1.05, 0.9), var, (1.2, 1.15, 0.75), 0.4)
    save("foliage", c, h)


def grass_card():
    """Alpha-cut grass blades for ground tufts. Alpha is coverage here (not height)."""
    img = np.zeros((N, N, 4))
    r = np.random.default_rng(37)
    ys, xs = np.mgrid[0:N, 0:N] / N
    for _ in range(70):
        x0 = r.uniform(0.05, 0.95); hgt = r.uniform(0.45, 0.98); lean = r.uniform(-0.25, 0.25); w = r.uniform(0.008, 0.018)
        t = np.clip((1 - ys) / hgt, 0, 1)                       # 0 at root (bottom) .. 1 at tip
        cx = x0 + lean * t ** 2
        inside = (np.abs(xs - cx) < w * (1 - t) + 0.002) & ((1 - ys) < hgt)
        shade = 0.55 + 0.45 * t
        img[inside, 0] = shade[inside] * r.uniform(0.85, 1.05)
        img[inside, 1] = shade[inside]
        img[inside, 2] = shade[inside] * r.uniform(0.7, 0.9)
        img[inside, 3] = 1
    rgba = (np.clip(img, 0, 1) * 255).astype(np.uint8)
    buf = io.BytesIO()
    Image.fromarray(rgba, "RGBA").save(buf, "PNG", optimize=True)
    with open(os.path.join(OUT, "grass_card.bytes"), "wb") as f:
        f.write(buf.getvalue())
    prev = os.environ.get("TEX_PREVIEW")
    if prev:
        Image.fromarray(rgba, "RGBA").save(os.path.join(prev, "grass_card.png"))
    print("grass_card", len(buf.getvalue()) // 1024, "KB")


if __name__ == "__main__":
    grass(); dirt(); cobble(); flagstone(); brick(); stone(); wood(); bark(); plaster(); thatch()
    weave("linen", 64, 0.55, 0.78, (1, 1, 1))
    weave("wool", 40, 0.35, 0.7, (1, 1, 1))
    leather(); metal(); skin(); scales(); carapace(); hair(); plain(); foliage(); grass_card()
