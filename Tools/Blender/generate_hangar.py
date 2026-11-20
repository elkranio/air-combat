"""Original low-poly hangar, with a reusable UV/color/AO bake pipeline.
Run from Blender CLI: blender -b --python-exit-code 1 --python Tools/Blender/generate_hangar.py
"""
from pathlib import Path
import math
import struct
import zlib

import bpy
import bmesh
import numpy as np
from mathutils import Euler

ROOT = Path(__file__).resolve().parents[2]
NAME = 'Hangar-04'
SOURCE = ROOT / 'Resources' / 'Blender' / f'{NAME}.blend'
OUT = ROOT / 'Assets' / '_Project' / 'Art' / 'Buildings' / NAME
SIZE = 1024
BAKE_MARGIN = 6
AO_DISTANCE = 1.8  # local contact shading, not sunlight
AO_STRENGTH = 0.68

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'  # reliable baseline; no CUDA/OptiX setup needed
scene.cycles.samples = 16
scene.render.bake.target = 'IMAGE_TEXTURES'
scene.render.bake.use_clear = True
scene.render.bake.use_selected_to_active = False
scene.render.bake.margin_type = 'EXTEND'
scene.world = bpy.data.worlds.new('Bake World')
scene.world.use_nodes = True
scene.world.node_tree.nodes.get('Background').inputs['Color'].default_value = (1, 1, 1, 1)
source_collection = bpy.data.collections.new('01 - Editable construction')
scene.collection.children.link(source_collection)
export_collection = bpy.data.collections.new('02 - Game mesh')
scene.collection.children.link(export_collection)


def srgb_to_linear(x):
    return x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4


def material(name, hex_rgb):
    rgb = tuple(srgb_to_linear(int(hex_rgb[i:i+2], 16) / 255) for i in (0, 2, 4))
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = m.diffuse_color
    p.inputs['Roughness'].default_value = 0.85
    return m


wall = material('Wall - concrete', '898C7D')
wall_dark = material('Wall - trim', '5B625F')
roof = material('Roof - olive steel', '67766D')
roof_alt = material('Roof - second tone', '718077')
rib = material('Roof - structural bands', '4B5A53')
door = material('Door - steel', '4E6267')
door_alt = material('Door - panels', '5A7074')
black = material('Recesses', '303C3C')
concrete = material('Foundation', 'A7A89A')
accent = material('Safety ochre', 'C9A15A')
marking = material('Roof markings', 'DAD8BF')


def mesh(name, verts, faces, mats, ids=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    for m in mats:
        data.materials.append(m)
    if ids is not None:
        for poly, idx in zip(data.polygons, ids):
            poly.material_index = idx
    # Recalculate outward normals for closed construction pieces.
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    data.update()
    o = bpy.data.objects.new(name, data)
    source_collection.objects.link(o)
    return o


def box(name, x, y, z, dx, dy, dz, mat):
    # Coordinates are centers; the foundation is the only piece touching Z=0.
    a, b, c = dx/2, dy/2, dz/2
    verts = [(x+i*a, y+j*b, z+k*c) for k in (-1, 1) for j in (-1, 1) for i in (-1, 1)]
    faces = [(0,2,3,1), (4,5,7,6), (0,1,5,4), (2,6,7,3), (0,4,6,2), (1,3,7,5)]
    return mesh(name, verts, faces, [mat])


# Building pivot: ground level, main footprint centered. Front faces +Y.
# A faceted barrel roof is readable from the 60-degree game camera.
arch = [(12*math.cos(math.pi*i/8), 5.7+4.1*math.sin(math.pi*i/8)) for i in range(8,-1,-1)]
profile = [(-12,0.35), (12,0.35)] + list(reversed(arch))
# Remove duplicate endpoint? Arch endpoints are at wall top, not ground.
verts = [(x,y,z) for y in (-16,16) for x,z in profile]
n = len(profile)
faces = [tuple(range(n-1,-1,-1)), tuple(range(n,2*n))]
ids = [0,0]
for i in range(n):
    j = (i+1)%n
    faces.append((i,j,n+j,n+i))
    ids.append(0 if i in (0,1,n-1) else (1 if i%3 else 2))
mesh('Barrel vault and walls', verts, faces, [wall,roof,roof_alt], ids)
box('Concrete foundations', 0,0,0.2, 25,33,0.4, concrete)


def roof_height(x):
    for (a,za),(b,zb) in zip(arch,arch[1:]):
        if a-1e-7 <= x <= b+1e-7:
            return za + (zb-za)*(x-a)/(b-a)
    raise ValueError(f'Not on roof: {x}')


def roof_strip(name, x0, x1, y0, y1, lift, mat, thickness=0.12):
    # Follow the exact piecewise roof profile to eliminate floating/deeply buried marks.
    xs = [x0] + [x for x,_ in arch if x0+1e-6 < x < x1-1e-6] + [x1]
    verts = [(x,y,roof_height(x)+lift+dz) for x in xs for y,dz in
             ((y0,0),(y1,0),(y1,-thickness),(y0,-thickness))]
    faces = [(3,2,1,0)]
    for i in range(len(xs)-1):
        for j in range(4):
            k = (j+1)%4
            faces.append((4*i+j,4*i+k,4*(i+1)+k,4*(i+1)+j))
    faces.append(tuple(range(len(verts)-4,len(verts))))
    return mesh(name, verts, faces, [mat])


for i,y in enumerate((-15.5,-8,0,8,15.5)):
    roof_strip(f'Arch rib {i}', -12,12, y-0.24,y+0.24,0.23,rib,0.30)
for side in (-1,1):
    box(f'Eave {side}', side*12.03,0,5.55,0.38,32.3,0.45,rib)
    for y in (-12,-4,4,12):
        box(f'Wall column {side} {y}', side*12.08,y,2.9,0.45,0.65,5.4,wall_dark)
        box(f'Wall vent {side} {y}', side*12.33,y+1,4.65,0.32,1.9,0.72,black)

# Closed sliding doors, deep dark surround and readable vertical seams.
box('Door surround',0,16.12,2.95,19.0,0.7,5.2,wall_dark)
box('Door recess',0,16.51,2.86,17.5,0.15,4.9,black)
for side in (-1,1):
    box(f'Sliding door {side}',side*4.28,16.66,2.85,8.42,0.24,4.65,door)
    for i in range(4):
        x=side*(0.75+i*2.03)
        box(f'Door seam {side} {i}', x,16.81,2.85,0.095,0.10,4.45,door_alt)
    box(f'Jamb {side}',side*9.2,16.66,2.8,0.48,0.56,5.1,rib)
    box(f'Jamb safety marking {side}',side*9.2,16.96,0.92,0.50,0.06,1.32,accent)
box('Top door track',0,16.78,5.38,19.3,0.65,0.42,rib)
box('Rear wall lintel',0,-16.14,4.6,19,0.45,0.6,wall_dark)

# Asymmetric maintenance annex: chunky details rather than tiny greebles.
box('Annex pad',14.6,-7,0.24,6.4,12.5,0.48,concrete)
box('Maintenance annex',14.6,-7,2.0,5.6,11.7,3.6,wall)
box('Annex roof',14.6,-7,3.92,6.15,12.15,0.48,roof_alt)
box('Annex access door',14.8,-1.10,1.55,2.0,0.25,2.6,door)
for y in (-10,-6):
    box(f'Annex HVAC base {y}',14.7,y,4.22,2.9,2.35,0.48,rib)
    box(f'Annex HVAC cap {y}',14.7,y,4.52,2.6,2.1,0.35,wall_dark)
    for x in (14.05,14.7,15.35):
        box(f'HVAC intake {y} {x}',x,y,4.72,0.23,1.65,0.09,black)

# Broad raised roof ventilators, with visible tops from overhead.
for y in (-11.5,11.5):
    roof_strip(f'Ventilator flashing {y}', -2.0,2.0,y-0.83,y+0.83,0.18,wall_dark,0.22)
    box(f'Ventilator {y}',0,y,10.02,3.2,1.1,0.65,rib)
    box(f'Ventilator cap {y}',0,y,10.42,3.45,1.35,0.20,roof_alt)

# Stylized 04 identifier, made of broad stencil-like strokes conforming to roof.
# Digits face the entrance (+Y), and sit between two structural ribs.
segments = {
    'a':(0.18,0.0,2.08,0.37), 'b':(1.85,0.20,2.22,1.89),
    'c':(1.85,2.06,2.22,3.76), 'd':(0.18,3.60,2.08,3.97),
    'e':(0.0,2.06,0.37,3.76), 'f':(0.0,0.20,0.37,1.89),
    'g':(0.18,1.80,2.08,2.17),
}
for digit, x, keys in [('0',-2.6,'abcdef'),('4',0.37,'bcfg')]:
    for key in keys:
        a,b,c,d = segments[key]
        # Front is +Y: top of the digit points toward -Y.
        roof_strip(f'Stencil {digit}{key}',x+a,x+c,3.9+b,3.9+d,0.035,marking,0.045)
# Two broad amber bands identify the entrance without depending on the stencil.
for side in (-1,1):
    roof_strip(f'Entrance band {side}', -9.8 if side<0 else 7.6,
               -7.6 if side<0 else 9.8,13.9,14.65,0.045,accent,0.06)

# Preserve the individual editable construction pieces; export a single static mesh.
parts = list(source_collection.objects)
bpy.ops.object.select_all(action='DESELECT')
for p in parts:
    copy = p.copy()
    copy.data = p.data.copy()
    export_collection.objects.link(copy)
    copy.select_set(True)
    bpy.context.view_layer.objects.active = copy
bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
obj.name = NAME
obj.data.name = NAME + ' mesh'
source_collection.hide_render = True
source_collection.hide_viewport = True
# UV islands are intentionally unique, not repeated, to allow per-surface AO.
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.010)
bpy.ops.object.mode_set(mode='OBJECT')
obj.data.uv_layers.active.name = 'AtlasUV'
obj.data.calc_loop_triangles()
print(f'GEOMETRY READY: {len(parts)} source pieces, {len(obj.data.loop_triangles)} triangles', flush=True)


def bake(name, ao=False):
    # Float data image: retain linear values, then encode final color PNGs ourselves.
    image = bpy.data.images.new(name, width=SIZE, height=SIZE, alpha=False,
                               float_buffer=True, is_data=True)
    mats = list({m for m in obj.data.materials if m is not None})
    temporary = []
    try:
        for m in mats:
            nodes, links = m.node_tree.nodes, m.node_tree.links
            output = next(n for n in nodes if n.type == 'OUTPUT_MATERIAL')
            old = output.inputs['Surface'].links[0].from_socket
            emit = nodes.new('ShaderNodeEmission')
            tex = nodes.new('ShaderNodeTexImage')
            tex.image = image
            aux = None
            if ao:
                aux = nodes.new('ShaderNodeAmbientOcclusion')
                aux.inputs['Distance'].default_value = AO_DISTANCE
                aux.inputs['Color'].default_value = (1,1,1,1)
                aux.samples = 16
                links.new(aux.outputs['AO'], emit.inputs['Color'])
            else:
                emit.inputs['Color'].default_value = m.diffuse_color
            links.new(emit.outputs['Emission'], output.inputs['Surface'])
            for node in nodes:
                node.select = False
            tex.select = True
            nodes.active = tex
            temporary.append((m,output,old,emit,tex,aux))
        print(f'BAKING {name} ({SIZE}x{SIZE})...', flush=True)
        bpy.ops.object.bake(type='EMIT', margin=BAKE_MARGIN)
        result = np.empty(SIZE*SIZE*4, dtype=np.float32)
        image.pixels.foreach_get(result)
        return result.reshape((SIZE,SIZE,4))[:,:,:3].copy()
    finally:
        for m, output, old, emit, tex, aux in temporary:
            links, nodes = m.node_tree.links, m.node_tree.nodes
            links.new(old, output.inputs['Surface'])
            nodes.remove(emit)
            nodes.remove(tex)
            if aux is not None: nodes.remove(aux)
        bpy.data.images.remove(image)


def png(path, rgb, srgb):
    # PNG scanlines begin at the top; Blender pixels start at the bottom.
    rgb = np.clip(rgb,0,1)
    if srgb:
        rgb = np.where(rgb <= 0.0031308, 12.92*rgb, 1.055*np.power(rgb,1/2.4)-0.055)
    rgb = np.rint(rgb*255).astype(np.uint8)[::-1]
    def chunk(kind,data):
        return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
    raw = b''.join(b'\0'+row.tobytes() for row in rgb)
    header = struct.pack('>IIBBBBB',SIZE,SIZE,8,2,0,0,0)
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',header)+chunk(b'IDAT',zlib.compress(raw,6))+chunk(b'IEND',b''))


base = bake('Base Color')
ao = bake('Ambient Occlusion', ao=True)[:,:,:1]
covered = np.max(base,axis=2) > 0.01
if not np.any(covered) or not np.all(np.isfinite(base)) or not np.all(np.isfinite(ao)):
    raise RuntimeError('Bake did not produce valid texture data.')
if np.max(ao[:,:,0][covered]) < 0.1 or np.ptp(ao[:,:,0][covered]) < 0.02:
    raise RuntimeError('AO bake has no usable variation; check Cycles bake output.')
combined = base * (1.0-AO_STRENGTH+AO_STRENGTH*np.clip(ao,0,1))
OUT.mkdir(parents=True,exist_ok=True)
SOURCE.parent.mkdir(parents=True,exist_ok=True)
for suffix, pixels, srgb in [('BaseColor',base,True),('AO',np.repeat(ao,3,axis=2),False),('Baked',combined,True)]:
    png(OUT/f'{NAME}_{suffix}.png',pixels,srgb)
# Both test variants share one mesh and one material slot; only the texture differs.
images = {}
for suffix in ('BaseColor','AO','Baked'):
    image = bpy.data.images.load(str(OUT/f'{NAME}_{suffix}.png'),check_existing=False)
    if suffix == 'AO': image.colorspace_settings.name = 'Non-Color'
    image.pack()
    images[suffix] = image
atlas_mat = bpy.data.materials.new('Hangar_Atlas')
atlas_mat.use_nodes = True
atlas_mat.diffuse_color = roof.diffuse_color
nodes, links = atlas_mat.node_tree.nodes, atlas_mat.node_tree.links
tex = nodes.new('ShaderNodeTexImage')
tex.image = images['Baked']
principled = nodes.get('Principled BSDF')
principled.inputs['Roughness'].default_value = 0.85
links.new(tex.outputs['Color'],principled.inputs['Base Color'])
nodes.active = tex
obj.data.materials.clear()
obj.data.materials.append(atlas_mat)
for p in obj.data.polygons: p.material_index = 0
# Pack the comparison textures into the editable source as well.
for image in images.values(): image.use_fake_user = True
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            s = area.spaces.active
            s.shading.type = 'SOLID'
            s.shading.color_type = 'TEXTURE'
            s.region_3d.view_distance = 60
            s.region_3d.view_location = (0,0,3)
            s.region_3d.view_rotation = Euler((math.radians(37),0,math.radians(24)), 'XYZ').to_quaternion()
            s.region_3d.view_perspective = 'ORTHO'
            s.overlay.show_floor = False
            s.overlay.show_axis_x = False
            s.overlay.show_axis_y = False
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
bpy.ops.export_scene.fbx(filepath=str(OUT/f'{NAME}.fbx'),use_selection=True,
                         object_types={'MESH'},axis_forward='-Z',axis_up='Y',
                         apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,
                         path_mode='AUTO')
print(f'HANGAR READY: {len(obj.data.loop_triangles)} triangles; 1 mesh; 1 material; {SIZE}x{SIZE} atlas')
print(f'BLEND: {SOURCE}\nUNITY: {OUT}')
