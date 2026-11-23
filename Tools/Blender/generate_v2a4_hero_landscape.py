"""V2A4: authored hero gorge and three rocky landmarks for Unity.

Geometry is driven by fixed editorial stations and hand-designed cross-sections,
not Perlin, fractal displacement or an infinite procedural terrain field.

Blender CLI (repo root):
  blender -b --python-exit-code 1 --python Tools/Blender/generate_v2a4_hero_landscape.py -- --device OPTIX

For a no-Blender geometry check (debug only):
  python Tools/Blender/generate_v2a4_hero_landscape.py --preview-obj /tmp/v2a4-preview
"""
from __future__ import annotations
import argparse
import math
import os
from pathlib import Path
import sys

# The source is deliberately readable/editable. These are placed river centreline
# stations, not random points. Each tuple: (Z, centre X, half rim width, floor Y,
# rim Y). The player can see the ravine at the actual 160-unit tall camera span.
GORGE_STATIONS = [
    (-600,  137,  86, -31, 18),
    (-480,  128,  96, -38, 17),
    (-340,   76, 100, -39, 18),
    (-205,   12,  88, -35, 22),
    ( -65,  -42,  96, -38, 24),
    (  85,   36, 104, -42, 21),
    ( 230,  136, 103, -36, 19),
    ( 370,  163,  93, -40, 22),
    ( 515,   94,  97, -34, 19),
    ( 600,   36,  92, -30, 18),
]

# Manually defined irregular mountain footprints, relative to their scene origin.
# Separate ring heights produce steep rock faces and real aircraft-height hazards.
MOUNTAINS = {
    "WestMassif": {
        "at": (-397, 0, -169),
        "height": 112,
        "footprint": [(-155, -59), (-137, -121), (-84, -160), (-19, -152),
                      (43, -119), (103, -52), (116, 14), (77, 92),
                      (19, 119), (-45, 141), (-117, 86), (-170, 10)],
    },
    "EastMassif": {
        "at": (397, 0, 252),
        "height": 106,
        "footprint": [(-175, -36), (-141, -116), (-52, -147), (42, -136),
                      (128, -89), (165, -6), (131, 61), (68, 117),
                      (-21, 153), (-102, 109), (-163, 43)],
    },
    "NorthSpine": {
        "at": (-347, 0, 362),
        "height": 77,
        "footprint": [(-183, -43), (-118, -72), (-28, -68), (67, -41),
                      (151, 20), (169, 57), (74, 69), (-14, 49),
                      (-99, 72), (-179, 23)],
    },
}

# Terrain sections are a designed cross-section: lip at t=1, near-sheer
# rock wall at t~0.8, talus, and a wide floor. Variable Z stations bend them.
CROSS = [1.64, 1.49, 1.36, 1.23, 1.12, 1.055, 1.015, 0.997,
         .964, .931, .902, .861, .821, .774, .725, .678, .633,
         .590, .548, .510, .460, .355, .230, .110, 0.0]


def smooth(a: float, b: float, v: float) -> float:
    t = max(0.0, min(1.0, (v - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


def station(z: float):
    for i in range(len(GORGE_STATIONS) - 1):
        a, b = GORGE_STATIONS[i], GORGE_STATIONS[i + 1]
        if z <= b[0]:
            t = smooth(a[0], b[0], z)
            return tuple(a[k] + (b[k] - a[k]) * t for k in range(1, 5))
    return tuple(GORGE_STATIONS[-1][1:])


def profile_y(t, z, floor, rim):
    # Preserve the *physical* side wall. No painted drop-off / giant diffuse blur.
    base = 5.2 + 1.0 * math.sin(z / 145.0)
    if t <= .505:
        return floor + 0.6 * (t / .505) ** 2
    if t <= .65:
        return floor + 0.6 + smooth(.505, .65, t) * 5.0
    if t <= .805:
        return floor + 5.6 + smooth(.65, .805, t) * 9.5
    if t <= .965:
        # Main cliff wall, 30-42 vertical units over ~13 horizontal units.
        return floor + 15.1 + smooth(.805, .965, t) * (rim - floor - 15.1)
    if t <= 1.045:
        return rim + (1.0 - smooth(.965, 1.045, t)) * 1.9
    if t <= 1.65:
        return rim + (base - rim) * smooth(1.045, 1.65, t)
    return base


def terrain_data():
    verts, faces, mats, face_uvs = [], [], [], []
    n_z = 301  # 4-unit Z resolution; horizontal wall geometry has tighter stations.
    for row in range(n_z):
        z = -600.0 + row * 4.0
        centre, half, floor, rim = station(z)
        # A stable number of *authored* cross-section points in every Z slice.
        left_outer = [-600.0 + (centre - half * 1.64 + 600.0) * j / 39.0 for j in range(39)]
        right_outer = [centre + half * 1.64 + (600.0 - (centre + half * 1.64)) * j / 39.0 for j in range(1, 40)]
        xs = left_outer + [centre - half * t for t in CROSS] + [centre + half * t for t in reversed(CROSS[:-1])] + right_outer
        for x in xs:
            t = abs(x - centre) / half
            y = profile_y(t, z, floor, rim)
            verts.append((x, y, z))
        if row == 0:
            n_x = len(xs)
            continue
        for col in range(n_x - 1):
            a = (row - 1) * n_x + col
            b = row * n_x + col
            # Material IDs: 0 open ground, 1 genuine wall rock, 2 canyon bed,
            # 3 rocky apron. Material boundaries use extra profile topology.
            mid = (xs[col] + xs[col + 1]) * 0.5
            t = abs(mid - centre) / half
            kind = 2 if t < .61 else 1 if t < 1.02 else 3 if t < 1.49 else 0
            for tri in [(a, b, a + 1), (a + 1, b, b + 1)]:
                faces.append(tri)
                mats.append(kind)
                uv = []
                for idx in tri:
                    px, py, pz = verts[idx]
                    # Cliff texture follows wall height, not XY top-projection:
                    # 20 m repeated texture footprint, no kilometre-scale blur.
                    uv.append((pz / 20.0, py / 17.0) if kind == 1
                              else (px / 23.0, pz / 23.0))
                face_uvs.append(uv)
    assert n_x > 90
    return verts, faces, mats, face_uvs


def mountain_data(spec):
    verts, faces, mats, face_uvs = [], [], [], []
    base = spec['footprint']
    # Terrain foot has an irregular natural outline, not a round 'tumour'.
    # Narrow shelf, high steep side and a broken cap; all explicit levels.
    h = spec['height']
    levels = [(1.00, 4), (.92, 8), (.85, 17), (.74, 34),
              (.695, 49), (.61, h * .72), (.54, h * .87),
              (.42, h * .95), (.23, h)]
    n = len(base)
    for ring, (scale, y) in enumerate(levels):
        for i, (x, z) in enumerate(base):
            # Fixed asymmetric compression into an upper rock spine.
            skew = (0.065 * ring / (len(levels) - 1))
            verts.append((x * scale + z * skew, y + .9 * math.sin(i * 2.1 + ring * .3), z * scale))
    verts.append((3.0, h + 1.0, -4.0))
    for r in range(len(levels) - 1):
        for i in range(n):
            j = (i + 1) % n
            a, b = r * n + i, r * n + j
            c, d = (r + 1) * n + i, (r + 1) * n + j
            # Lower scree / fractured face / cap bands use different material colors.
            kind = 2 if r <= 1 else 1 if r <= 5 else 0
            for tri in ((a, c, b), (b, c, d)):
                faces.append(tri)
                mats.append(kind)
                face_uvs.append([(verts[q][0] / 220.0 + .5, verts[q][2] / 220.0 + .5) for q in tri])
    top = len(verts) - 1
    for i in range(n):
        tri = ((len(levels) - 1) * n + (i + 1) % n, (len(levels) - 1) * n + i, top)
        faces.append(tri)
        mats.append(0)
        face_uvs.append([(verts[q][0] / 220.0 + .5, verts[q][2] / 220.0 + .5) for q in tri])
    return verts, faces, mats, face_uvs


def export_obj_preview(path: Path, data):
    verts, faces, _, _ = data
    with path.open('w', encoding='ascii') as f:
        for x, y, z in verts:
            f.write(f'v {x:.4f} {y:.4f} {z:.4f}\n')
        for face in faces:
            f.write('f ' + ' '.join(str(i + 1) for i in face) + '\n')


def make_mesh(name: str, data, material_names):
    verts, faces, mat_index, face_uvs = data
    mesh = bpy.data.meshes.new(name)
    # Authoring data are specified in Unity world axes (X, Y-up, Z-north).
    # Blender is Z-up and -Y-forward; convert BEFORE FBX axis export.
    # This rotation is right-handed, so face winding stays unchanged.
    blender_verts = [(x, -z, y) for x, y, z in verts]
    mesh.from_pydata(blender_verts, [], faces)
    mesh.update()
    for name_mat in material_names:
        mesh.materials.append(bpy.data.materials[name_mat])
    uv_layer = mesh.uv_layers.new(name='UV0')
    for i, poly in enumerate(mesh.polygons):
        poly.material_index = mat_index[i]
        poly.use_smooth = name == 'HeroGorgeGround' and mat_index[i] != 1
        for j, loop_idx in enumerate(poly.loop_indices):
            uv_layer.data[loop_idx].uv = face_uvs[i][j]
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def create_material(name, color):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*color, 1.0)
    shader.inputs['Metallic'].default_value = 0.0
    shader.inputs['Roughness'].default_value = .9
    return material


def set_selected(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def bake_local_ao(obj, name, dest, device):
    # Per-rock UNIQUE UV atlas: local crevice-only AO (never sunlight).
    # If Blender bake is unavailable, geometry and URP shadows still work.
    set_selected(obj)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(island_margin=.018)
    bpy.ops.object.mode_set(mode='OBJECT')
    image = bpy.data.images.new(name + '_AO', width=1024, height=1024, alpha=False)
    image.generated_color = (1, 1, 1, 1)
    for mat in obj.data.materials:
        nodes = mat.node_tree.nodes
        image_node = nodes.new('ShaderNodeTexImage')
        image_node.image = image
        nodes.active = image_node
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.render.bake.use_clear = True
    scene.render.bake.use_selected_to_active = False
    scene.render.bake.margin = 6
    scene.render.bake.target = 'IMAGE_TEXTURES'
    # Other meshes are not yet positioned in their Unity world locations.
    # Hide them during this LOCAL AO bake to prevent imaginary occlusion.
    previous = [(other, other.hide_render) for other in bpy.data.objects if other != obj]
    try:
        for other, _ in previous: other.hide_render = True
        set_selected(obj)
        bpy.ops.object.bake(type='AO')
    finally:
        for other, was_hidden in previous: other.hide_render = was_hidden
    image.filepath_raw = str(dest / (name + '_AO.png'))
    image.file_format = 'PNG'
    image.save()
    # Do not wire AO into albedo: importer supplies _OcclusionMap separately.
    return image


def make_bevel_ground_material(name, color):
    return create_material(name, color)


def export_fbx(obj, dest):
    set_selected(obj)
    bpy.ops.export_scene.fbx(filepath=str(dest), use_selection=True, apply_unit_scale=True,
                             object_types={'MESH'}, use_mesh_modifiers=False,
                             add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y')


def parse_args():
    raw = sys.argv
    if '--' in raw:
        raw = raw[raw.index('--') + 1:]
    elif '--preview-obj' in raw:
        raw = raw[raw.index('--preview-obj'):]
    else:
        raw = []
    parser = argparse.ArgumentParser()
    parser.add_argument('--device', default='AUTO', choices=['AUTO', 'OPTIX', 'CUDA', 'CPU'])
    parser.add_argument('--no-bake', action='store_true', help='skip local crevice AO; never skips geometry')
    parser.add_argument('--preview-obj', help='pure-Python debug OBJ export (no Blender needed)')
    return parser.parse_args(raw)


def choose_device(device):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    if device == 'CPU': return 'CPU'
    try:
        prefs = bpy.context.preferences.addons['cycles'].preferences
        order = [device] if device != 'AUTO' else ['OPTIX', 'CUDA']
        for backend in order:
            prefs.compute_device_type = backend
            prefs.get_devices()
            devices = list(prefs.devices)
            enabled = [d for d in devices if d.type == backend]
            if enabled:
                for d in devices: d.use = d.type == backend
                scene.cycles.device = 'GPU'
                return backend
    except Exception as e:
        print('V2A4 GPU bake not available, using CPU:', e)
    return 'CPU'


def main():
    args = parse_args()
    if args.preview_obj:
        path = Path(args.preview_obj).resolve()
        path.mkdir(parents=True, exist_ok=True)
        export_obj_preview(path / 'HeroGorgeGround.obj', terrain_data())
        for name, spec in MOUNTAINS.items():
            export_obj_preview(path / (name + '.obj'), mountain_data(spec))
        print('V2A4 raw OBJ previews:', path)
        return
    if bpy is None:
        raise RuntimeError('Blender executable required. See the header CLI command.')
    root = Path.cwd()
    dest = root / 'Assets/_Project/Art/Environment/FrontierV2Hero'
    dest.mkdir(parents=True, exist_ok=True)
    (root / 'Resources/Blender').mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    materials = {
        'V2A4_OpenGround': (.64, .48, .32),
        'V2A4_CanyonWall': (.43, .33, .27),
        'V2A4_CanyonBed': (.41, .36, .31),
        'V2A4_CanyonApron': (.53, .41, .32),
        'V2A4_RockCap': (.61, .52, .41),
        'V2A4_RockFace': (.40, .39, .37),
        'V2A4_RockTalus': (.47, .43, .38),
    }
    for name, color in materials.items(): create_material(name, color)
    terrain = make_mesh('HeroGorgeGround', terrain_data(),
                        ['V2A4_OpenGround', 'V2A4_CanyonWall', 'V2A4_CanyonBed', 'V2A4_CanyonApron'])
    all_objs = [terrain]
    report = []
    device_used = choose_device(args.device)
    for name, spec in MOUNTAINS.items():
        obj = make_mesh(name, mountain_data(spec),
                        ['V2A4_RockCap', 'V2A4_RockFace', 'V2A4_RockTalus'])
        all_objs.append(obj)
        if not args.no_bake:
            try:
                bake_local_ao(obj, name, dest, device_used)
                report.append(name + ': 1024 local Cycles AO (' + device_used + ')')
            except Exception as e:
                # Do not mark a failed bake as done; imported model remains usable.
                print('V2A4 AO bake failed:', name, repr(e))
                raise
        else:
            report.append(name + ': no AO (--no-bake)')
    for obj in all_objs:
        export_fbx(obj, dest / (obj.name + '.fbx'))
    bpy.ops.wm.save_as_mainfile(filepath=str(root / 'Resources/Blender/FrontierV2Hero.blend'))
    marker = dest / 'V2A4.ready.txt'
    marker.write_text('V2A4_HERO_TERRAIN_1\n' + '\n'.join(report) + '\n', encoding='utf-8')
    print('V2A4 COMPLETE:', marker, '| terrain + 3 mountains |', ' / '.join(report))


try:
    import bpy
except ImportError:
    bpy = None

if __name__ == '__main__':
    main()
