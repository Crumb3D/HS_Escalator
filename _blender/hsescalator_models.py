# Escalator balustrade pieces. Origin is the cell min corner. Metres, Y up.
# Local +X runs uphill / into the escalator. The step face is local Z = 0 on a right piece
# and local Z = 1 on a left piece. No rubber — the mod draws the moving handrail.
# Right and left are mirrors. Rotate a piece so the glass faces the steps.
import math
import struct
import sys
import zlib
from pathlib import Path

import bmesh
import bpy

MAT_GLASS = "HS_Glass"
MAT_DARK = "HS_FrameDark"
MAT_SILVER = "HS_FrameSilver"
MAT_LIGHT = "HS_LightGreen"

# Offsets from the rail centreline. The mod's rubber sits on this same line.
SKIRT = (-0.95, -0.72)
GLASS = (-0.70, -0.05)
LIGHT = (-0.76, -0.66)
CAP = (-0.07, -0.03)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not args:
        raise SystemExit("usage: blender --background --python hsescalator_models.py -- <models dir> [icon dir]")
    models = Path(args[0])
    icons = Path(args[1]) if len(args) > 1 else None
    models.mkdir(parents=True, exist_ok=True)
    setup_scene()
    for flip, hand in ((False, "R"), (True, "L")):
        export_piece(build_run("HSSideSlope" + hand, slope_pts(), flip), models / ("HSSideSlope" + hand + ".fbx"))
        export_piece(build_run("HSSideFlat" + hand, flat_pts(), flip), models / ("HSSideFlat" + hand + ".fbx"))
        export_piece(build_run("HSSideEnd" + hand, end_pts(), flip), models / ("HSSideEnd" + hand + ".fbx"))
    if icons is not None:
        icons.mkdir(parents=True, exist_ok=True)
        write_icons(icons)
    print("Exported balustrade FBX to", models)


def setup_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    _mat(MAT_GLASS, (0.62, 0.78, 0.84, 0.22), metallic=0.05, rough=0.04, trans=0.92, alpha=0.22)
    _mat(MAT_DARK, (0.05, 0.05, 0.055, 1.0), metallic=0.7, rough=0.4, trans=0.0, alpha=1.0)
    _mat(MAT_SILVER, (0.72, 0.74, 0.76, 1.0), metallic=0.9, rough=0.35, trans=0.0, alpha=1.0)
    _mat(MAT_LIGHT, (0.15, 0.85, 0.35, 1.0), metallic=0.0, rough=0.4, trans=0.0, alpha=1.0)


def slope_pts():
    # 0.5 m rise over 1 m, centred on a lower-half step. Upper pieces add ModelOffset 0,0.5,0.
    return [(0.0, 1.15), (1.0, 1.65)]


def flat_pts():
    return [(0.0, 1.40), (1.0, 1.40)]


def end_pts():
    # Semicircle bulging toward -X (off the end), then a straight run back into the escalator.
    radius = 0.22
    cx = 0.40
    cy = 1.40 - radius
    pts = []
    steps = 10
    for i in range(steps, -1, -1):
        a = math.pi * i / steps
        pts.append((cx - math.sin(a) * radius, cy + math.cos(a) * radius))
    pts.append((1.0, 1.40))
    return pts


def build_run(name, pts, flip):
    reset_scene_objects()
    parts = []
    parts.append(band("skirt", pts, SKIRT[0], SKIRT[1], zs(flip, 0.00, 0.10), MAT_SILVER))
    parts.append(band("glass", pts, GLASS[0], GLASS[1], zs(flip, 0.055, 0.095), MAT_GLASS))
    parts.append(band("light", pts, LIGHT[0], LIGHT[1], zs(flip, 0.00, 0.03), MAT_LIGHT))
    parts.append(band("frame", pts, GLASS[0], GLASS[1], zs(flip, 0.095, 0.125), MAT_DARK))
    parts.append(band("cap", pts, CAP[0], CAP[1], zs(flip, 0.03, 0.14), MAT_DARK))
    root = join_under(name, parts)
    col = box("COL_0", (1.0, 1.7, 0.22), (0.5, 0.95, 0.90 if flip else 0.10), MAT_DARK)
    col.parent = root
    return root


def zs(flip, a, b):
    if not flip:
        return (a, b)
    return (1.0 - b, 1.0 - a)


def band(name, pts, y0, y1, zspan, mat_name):
    z0, z1 = zspan
    verts = []
    for x, yc in pts:
        verts.append((x, yc + y0, z0))
        verts.append((x, yc + y0, z1))
        verts.append((x, yc + y1, z1))
        verts.append((x, yc + y1, z0))
    faces = []
    for i in range(len(pts) - 1):
        a = i * 4
        b = (i + 1) * 4
        faces.append((a, b, b + 1, a + 1))
        faces.append((a + 3, a + 2, b + 2, b + 3))
        faces.append((a, a + 3, b + 3, b))
        faces.append((a + 1, b + 1, b + 2, a + 2))
    faces.append((3, 2, 1, 0))
    n = (len(pts) - 1) * 4
    faces.append((n, n + 1, n + 2, n + 3))
    return new_mesh(name, verts, faces, mat_name)


def to_unity(v):
    # Blender 5 bakes (x, y, z) into Unity (-x, z, -y). Author Y-up, then undo that.
    x, y, z = v
    return (-x, -z, y)


def new_mesh(name, verts, faces, mat_name):
    mesh = bpy.data.meshes.new(name + "_mesh")
    mesh.from_pydata([to_unity(v) for v in verts], [], faces)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mat = bpy.data.materials.get(mat_name)
    if mat is not None:
        mesh.materials.append(mat)
    return obj


def box(name, size, center, mat_name):
    sx, sy, sz = size
    cx, cy, cz = center
    hx, hy, hz = sx * 0.5, sy * 0.5, sz * 0.5
    verts = [
        (cx - hx, cy - hy, cz - hz),
        (cx + hx, cy - hy, cz - hz),
        (cx + hx, cy + hy, cz - hz),
        (cx - hx, cy + hy, cz - hz),
        (cx - hx, cy - hy, cz + hz),
        (cx + hx, cy - hy, cz + hz),
        (cx + hx, cy + hy, cz + hz),
        (cx - hx, cy + hy, cz + hz),
    ]
    faces = [
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (2, 6, 7, 3),
        (0, 3, 7, 4),
        (1, 5, 6, 2),
    ]
    return new_mesh(name, verts, faces, mat_name)


def join_under(name, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    objects[0].name = name
    return objects[0]


def reset_scene_objects():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)


def export_piece(root, path):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for c in root.children:
        c.select_set(True)
    bpy.context.view_layer.objects.active = root
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
    )


def _mat(name, rgba, metallic, rough, trans, alpha):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.blend_method = "BLEND" if alpha < 0.99 else "OPAQUE"
    bsdf = None
    for n in mat.node_tree.nodes:
        if n.type == "BSDF_PRINCIPLED":
            bsdf = n
            break
    if bsdf is None:
        return mat

    def set_in(key, val):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = val

    set_in("Base Color", rgba)
    set_in("Metallic", metallic)
    set_in("Roughness", rough)
    set_in("Alpha", alpha)
    set_in("IOR", 1.45)
    set_in("Transmission Weight", trans)
    set_in("Transmission", trans)
    if name == MAT_LIGHT:
        set_in("Emission Color", (0.2, 1.0, 0.35, 1.0))
        set_in("Emission", (0.2, 1.0, 0.35, 1.0))
        set_in("Emission Strength", 4.0)
    return mat


def write_icons(folder):
    jobs = (
        ("hsescalatorEndL", "end", True),
        ("hsescalatorEndR", "end", False),
        ("hsescalatorSlopeL", "slope", True),
        ("hsescalatorSlopeR", "slope", False),
        ("hsescalatorFlatL", "flat", True),
        ("hsescalatorFlatR", "flat", False),
    )
    for name, kind, flip in jobs:
        write_png(folder / (name + ".png"), icon_pixels(kind, flip))


def icon_pixels(kind, flip):
    w = h = 128
    px = bytearray(w * h * 4)
    for y in range(h):
        for x in range(w):
            i = (y * w + x) * 4
            px[i:i + 4] = b"\x2a\x2e\x32\xff"
    # Side view, Y up in the image (row 0 is the top of a PNG, so flip).
    def plot(x, y, rgb):
        if x < 0 or y < 0 or x >= w or y >= h:
            return
        row = h - 1 - y
        i = (row * w + x) * 4
        px[i:i + 3] = bytes(rgb)

    def line(x0, y0, x1, y1, rgb, thick):
        steps = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
        for s in range(steps + 1):
            t = s / steps
            x = x0 + (x1 - x0) * t
            y = y0 + (y1 - y0) * t
            for dy in range(-thick, thick + 1):
                for dx in range(-thick, thick + 1):
                    plot(int(x) + dx, int(y) + dy, rgb)

    if kind == "slope":
        glass = ((18, 28), (110, 78), (110, 108), (18, 58))
        rail = ((16, 62), (112, 112))
    elif kind == "end":
        glass = ((36, 34), (100, 34), (100, 96), (36, 96))
        rail = None
    else:
        glass = ((16, 40), (112, 40), (112, 96), (16, 96))
        rail = ((14, 100), (114, 100))

    if flip:
        def fx(x):
            return 127 - x
        glass = tuple((fx(a), b) for a, b in glass)
        if rail:
            rail = ((fx(rail[0][0]), rail[0][1]), (fx(rail[1][0]), rail[1][1]))

    fill_poly(px, w, h, glass, (150, 190, 200, 255))
    outline(px, w, h, glass, (40, 44, 48, 255))
    if kind == "end":
        # Quarter bulge on the outer end.
        cx, cy, r = (28 if not flip else 100), 70, 26
        if flip:
            cx = 100
        for a in range(0, 180, 4):
            rad = math.radians(a if not flip else 180 - a)
            x = int(cx + math.cos(rad) * r * (1 if not flip else -1))
            y = int(cy + math.sin(rad) * r * 0.7)
            for k in range(-2, 3):
                plot(x, y + k, (20, 20, 22))
                plot(x + k, y, (20, 20, 22))
        line(40 if not flip else 88, 96, 112 if not flip else 16, 96, (20, 20, 22), 3)
    elif rail:
        line(rail[0][0], rail[0][1], rail[1][0], rail[1][1], (16, 16, 18), 3)
    # Green skirt light.
    y0 = glass[0][1] - 4
    x0 = min(p[0] for p in glass) + 4
    x1 = max(p[0] for p in glass) - 4
    for x in range(x0, x1):
        for y in range(y0, y0 + 5):
            plot(x, y, (40, 200, 90))
    return px


def fill_poly(px, w, h, poly, rgba):
    ys = [p[1] for p in poly]
    y0, y1 = int(min(ys)), int(max(ys))
    for y in range(y0, y1 + 1):
        xs = []
        for i in range(len(poly)):
            a = poly[i]
            b = poly[(i + 1) % len(poly)]
            if (a[1] <= y < b[1]) or (b[1] <= y < a[1]):
                t = (y - a[1]) / (b[1] - a[1] or 1)
                xs.append(a[0] + (b[0] - a[0]) * t)
        if len(xs) < 2:
            continue
        xs.sort()
        for x in range(int(xs[0]), int(xs[-1]) + 1):
            if 0 <= x < w and 0 <= y < h:
                row = h - 1 - y
                i = (row * w + x) * 4
                px[i:i + 4] = bytes(rgba)


def outline(px, w, h, poly, rgba):
    def plot(x, y):
        if 0 <= x < w and 0 <= y < h:
            row = h - 1 - int(y)
            i = (row * w + int(x)) * 4
            px[i:i + 4] = bytes(rgba)
    for i in range(len(poly)):
        a = poly[i]
        b = poly[(i + 1) % len(poly)]
        steps = int(max(abs(b[0] - a[0]), abs(b[1] - a[1]))) + 1
        for s in range(steps + 1):
            t = s / steps
            plot(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def write_png(path, rgba):
    w = h = 128
    raw = b"".join(b"\x00" + bytes(rgba[y * w * 4:(y + 1) * w * 4]) for y in range(h))

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9))
    png += chunk(b"IEND", b"")
    path.write_bytes(png)


if __name__ == "__main__":
    main()
