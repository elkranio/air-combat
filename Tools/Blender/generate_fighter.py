"""Generate an original, stylized twin-engine fighter. Run from Blender CLI."""
from pathlib import Path
from math import cos, sin, tau, radians

import bpy
import bmesh
from mathutils import Euler

ROOT = Path(__file__).resolve().parents[2]
NAME = "XF-01-Kestrel"
SOURCE = ROOT / "Resources" / "Blender" / f"{NAME}.blend"
EXPORT = ROOT / "Assets" / "_Project" / "Art" / "Aircraft" / f"{NAME}.fbx"

# Blender coordinates: X = span, +Y = nose, Z = up. Units are game units.
# v3: final canopy, wing tone and nozzle polish for the game camera.
# Keep the original flight pivot and FBX path so existing Unity instances update.
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0


def material(name, rgb, metallic=0.0, roughness=0.7):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1.0)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = m.diffuse_color
    p.inputs['Metallic'].default_value = metallic
    p.inputs['Roughness'].default_value = roughness
    return m


body = material('Kestrel_Hull', (0.060, 0.100, 0.139), 0.0, 0.82)
upper = material('Kestrel_Upper', (0.025, 0.049, 0.080), 0.0, 0.82)
panel = material('Kestrel_Panels', (0.016, 0.028, 0.043), 0.0, 0.8)
metal = material('Kestrel_Exhaust', (0.075, 0.085, 0.095), 0.45)
void = material('Kestrel_Interior', (0.018, 0.024, 0.027))
glass = material('Kestrel_Canopy', (0.016, 0.215, 0.31), 0.0, 0.38)
accent = material('Kestrel_Amber', (0.90, 0.24, 0.025), 0.0, 0.75)


def mesh(name, verts, faces, mats, indices=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    for mat in mats:
        data.materials.append(mat)
    if indices is not None:
        for poly, idx in zip(data.polygons, indices):
            poly.material_index = idx
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    data.update()
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    return obj


# Each station is (y, x center, z center, half width, half height).
# Closed faceted cross sections avoid a pile-of-cubes silhouette.
def loft(name, stations, mats, sides=8, top_material=0):
    verts = []
    for y, x, z, w, h in stations:
        for k in range(sides):
            a = tau * (k + 0.5) / sides
            verts.append((x + w*cos(a), y, z + h*sin(a)))
    faces = [tuple(reversed(range(sides)))]
    ids = [0]
    for i in range(len(stations)-1):
        for k in range(sides):
            j = (k+1) % sides
            faces.append((i*sides+k, i*sides+j, (i+1)*sides+j, (i+1)*sides+k))
            ids.append(top_material if sin(tau*(k+1)/sides) > 0.25 else 0)
    faces.append(tuple(range((len(stations)-1)*sides, len(stations)*sides)))
    ids.append(0)
    return mesh(name, verts, faces, mats, ids)


# Tapered flight surfaces from profile stations: x, leading y, trailing y,
# center z and half thickness. Four vertices per station, cambered top.
def wing(name, side, stations, top=upper, edge=body):
    verts = []
    for x, lead, trail, z, t in stations:
        x *= side
        chord = lead-trail
        verts.extend([(x, lead, z), (x, lead-chord*0.32, z+t),
                      (x, trail, z), (x, lead-chord*0.38, z-t*0.65)])
    faces = [(3,2,1,0)]
    ids = [1]
    for i in range(len(stations)-1):
        for j in range(4):
            k = (j+1)%4
            faces.append((i*4+j, i*4+k, (i+1)*4+k, (i+1)*4+j))
            ids.append(0 if j < 2 else 1)
    faces.append(tuple(range(len(verts)-4, len(verts))))
    ids.append(1)
    return mesh(name, verts, faces, [top, edge], ids)


loft('Fuselage', [(-4.65,0,0.0,0.62,0.44), (-3.25,0,0.04,1.04,0.64),
                   (-0.70,0,0.04,1.36,0.73), (1.15,0,0.07,1.10,0.69),
                   (2.65,0,0.03,0.72,0.50), (3.68,0,-0.02,0.40,0.30),
                   (4.48,0,-0.05,0.12,0.095)], [body,upper], top_material=1)
loft('Canopy', [(0.22,0,0.68,0.48,0.15), (0.90,0,0.77,0.65,0.36),
                 (1.98,0,0.77,0.60,0.32), (2.85,0,0.50,0.27,0.10)], [glass], sides=8)
# Rear canopy frame, narrow enough to read at the game camera scale.
loft('CanopyFrame', [(0.29,0,0.69,0.52,0.16), (0.48,0,0.73,0.57,0.20)], [panel])

for side, tag in [(-1,'L'), (1,'R')]:
    # Larger continuous wing mass, lighter than the dark center spine.
    wing(f'Wing_{tag}', side, [(0.68,1.85,-2.65,0.01,0.23),
                               (1.95,0.98,-2.95,0.02,0.17),
                               (3.90,-0.78,-3.20,0.12,0.09)], body, upper)
    wing(f'Wingtip_{tag}', side, [(3.90,-0.78,-3.20,0.12,0.09),
                                  (4.32,-1.10,-3.20,0.13,0.075),
                                  (4.72,-1.64,-3.08,0.14,0.055)], accent, panel)
    wing(f'Tailplane_{tag}', side, [(0.75,-3.08,-4.65,0.10,0.12),
                                    (2.52,-3.75,-4.85,0.15,0.055),
                                    (2.67,-4.03,-4.68,0.16,0.025)], body, upper)
    # Integrated twin engine pods with rectangular intake shoulders.
    loft(f'Engine_{tag}', [(-4.63,side*1.15,-0.10,0.61,0.52),
                            (-3.35,side*1.25,-0.05,0.75,0.63),
                            (-0.65,side*1.24,-0.09,0.78,0.56),
                            (0.88,side*1.08,-0.10,0.63,0.43)], [body,upper], top_material=1)
    loft(f'Intake_{tag}', [(0.84,side*1.08,-0.10,0.63,0.43),
                            (1.07,side*1.06,-0.11,0.57,0.37)], [panel])
    loft(f'IntakeDark_{tag}', [(1.066,side*1.06,-0.11,0.46,0.29),
                                (1.095,side*1.06,-0.11,0.46,0.29)], [void])
    # Recessed nozzle: outer / inner rings, a dark inner back wall.
    verts = []
    for y,r in [(-4.40,0.52),(-5.02,0.49),(-5.02,0.36),(-4.46,0.32)]:
        for k in range(12):
            a=tau*k/12
            verts.append((side*1.15+1.12*r*cos(a), y, -0.10+1.12*r*sin(a)))
    faces=[]
    ids=[]
    for i in range(3):
        for k in range(12):
            j=(k+1)%12
            faces.append((i*12+k,i*12+j,(i+1)*12+j,(i+1)*12+k))
            ids.append(0 if i<2 else 1)
    faces.append(tuple(range(36,48)))
    ids.append(1)
    mesh(f'Nozzle_{tag}', verts, faces, [metal,void], ids)
    # Outward-canted twin vertical stabilizers with a clipped swept crown.
    profile=[(1.12,-2.42,0.35),(1.22,-4.32,0.34),
             (1.95,-4.52,1.65),(1.85,-3.63,1.81)]
    verts=[(side*x+d,y,z) for d in (-0.10,0.10) for x,y,z in profile]
    mesh(f'Fin_{tag}', verts, [(0,1,2,3),(7,6,5,4),(0,4,5,1),
                             (1,5,6,2),(2,6,7,3),(3,7,4,0)], [body,upper], [0,0,1,1,1,1])
    # One broad midtone band per engine shoulder instead of thin surface detail.
    loft(f'DorsalPanel_{tag}', [(-3.58,side*1.25,0.55,0.34,0.045),
                                (-1.48,side*1.24,0.48,0.40,0.07),
                                (-0.82,side*1.22,0.45,0.24,0.045)], [body])

# Keep the source editable; all geometry shares the flight pivot at world zero.
meshes = [o for o in scene.objects if o.type == 'MESH']
for o in meshes:
    o.select_set(True)
    o.data.calc_loop_triangles()
bpy.context.view_layer.objects.active = bpy.data.objects['Fuselage']
# A useful initial viewport for inspecting the .blend without camera or lights.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            space = area.spaces.active
            space.shading.type = 'SOLID'
            space.shading.color_type = 'MATERIAL'
            space.region_3d.view_distance = 16
            space.region_3d.view_location = (0,0,0)
            space.region_3d.view_rotation = Euler((radians(28),0,radians(-18)), 'XYZ').to_quaternion()
            space.region_3d.view_perspective = 'ORTHO'
            space.overlay.show_floor = False
            space.overlay.show_axis_x = False
            space.overlay.show_axis_y = False
for o in meshes:
    o.select_set(False)

SOURCE.parent.mkdir(parents=True, exist_ok=True)
EXPORT.parent.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
for o in meshes:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(EXPORT), use_selection=True,
                         object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
                         apply_unit_scale=True, bake_anim=False, add_leaf_bones=False)
print(f'FIGHTER READY: {len(meshes)} meshes, {sum(len(o.data.loop_triangles) for o in meshes)} triangles')
print(f'BLEND: {SOURCE}')
print(f'FBX:   {EXPORT}')
