"""V14A: reusable Frontier rock and abandoned refinery kit.

Blender 4.x/5.x, metres, Z up. Generates 20 FBX meshes + unique UVs,
base-color PNGs, optional local AO and tangent-space high-to-low normal PNGs.
No directional lighting or world/terrain shadows are baked.

Run from the repo root (Windows Git Bash):
  blender -b --python-exit-code 1 --python Tools/Blender/generate_frontier_hero_kit.py -- --device OPTIX
  # Fast base-color-only iteration: append --quick
"""
import argparse
import math
import json
import random
import struct
import sys
import zlib
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector, Quaternion

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets' / '_Project' / 'Art' / 'Environment' / 'FrontierHeroKit'
SOURCE = ROOT / 'Resources' / 'Blender' / 'FrontierHeroKit.blend'
MANIFEST = OUT / 'HeroKitManifest.json'
TAU = math.tau

# Restrained stone/industrial spectrum, tuned for an ochre background.
COLORS = {
    'stone': '9D8975', 'stone_warm': 'AD947B', 'stone_light': 'C0AA90',
    'stone_dark': '796D63', 'strata': '8A7463',
    'concrete': 'ADA89C', 'concrete_light': 'C2B8A8', 'fracture': '898176',
    'roof': '85847E', 'steel': '626A6A', 'rust': '986B4E', 'rust_dark': '725647',
    'tank': 'B1A08C', 'tank_top': '8E7967', 'interior': '66645E',
    'warning': 'C6A36E', 'glass': '647A7C', 'pipe': '898C87',
}
MATS = {}


def parse_args():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument('--device', choices=('OPTIX', 'CUDA', 'CPU'), default='OPTIX')
    p.add_argument('--size', type=int, default=1024)
    p.add_argument('--hero-size', type=int, default=2048)
    p.add_argument('--samples', type=int, default=48)
    p.add_argument('--quick', action='store_true', help='512px base color only, CPU or requested GPU; no AO/normal maps')
    a = p.parse_args(argv)
    if a.size not in (512, 1024, 2048) or a.hero_size not in (512, 1024, 2048) or not 1 <= a.samples <= 512:
        p.error('Sizes must be 512/1024/2048 and samples 1..512')
    return a


def select_only(*objects, active=None):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = active or objects[-1]


def srgb_to_linear(v):
    return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4


def make_material(key):
    hex_rgb = COLORS[key]
    rgb = tuple(srgb_to_linear(int(hex_rgb[i:i+2], 16) / 255) for i in (0, 2, 4))
    m = bpy.data.materials.new(key)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    n = m.node_tree.nodes; l = m.node_tree.links
    bsdf = n.get('Principled BSDF'); bsdf.inputs['Roughness'].default_value = .76
    # Very broad wear; don't bake noisy rust speckles at our tiny on-screen scale.
    noise = n.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value = .7 if key.startswith('stone') else 1.35
    noise.inputs['Detail'].default_value = 2.0
    coord = n.new('ShaderNodeTexCoord'); l.new(coord.outputs['Object'], noise.inputs['Vector'])
    ramp = n.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = .12; ramp.color_ramp.elements[0].color = (*(c * .83 for c in rgb), 1)
    ramp.color_ramp.elements[1].position = .88; ramp.color_ramp.elements[1].color = (*(min(1, c * 1.10) for c in rgb), 1)
    l.new(noise.outputs['Fac'], ramp.inputs[0]); l.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    return m


class Builder:
    def __init__(self, collection, name):
        self.collection, self.name, self.pieces = collection, name, []

    def mesh(self, name, verts, faces, material, face_materials=None):
        data = bpy.data.meshes.new(name); data.from_pydata(verts, [], faces); data.update()
        obj = bpy.data.objects.new(name, data); self.collection.objects.link(obj)
        keys = list(dict.fromkeys(face_materials)) if face_materials is not None else [material]
        for k in keys: data.materials.append(MATS[k])
        if face_materials is not None:
            for p, key in zip(data.polygons, face_materials): p.material_index = keys.index(key)
        self.pieces.append(obj)
        return obj

    def box(self, name, p, dims, material, yaw=0, bevel=.10, rotation=None):
        """Closed chamfered solid, bevel is real silhouette geometry, not a bake trick."""
        dx, dy, dz = (v * .5 for v in dims)
        b = min(bevel, dx*.26, dy*.26, dz*.26)
        q = rotation or Quaternion((0, 0, 1), math.radians(yaw))
        def outline(x,y,c):
            return [(-x+c,-y), (x-c,-y), (x,-y+c), (x,y-c),
                    (x-c,y),(-x+c,y),(-x,y-c),(-x,-y+c)]
        verts=[]
        for z, inset in ((-dz,b),(-dz+b,0),(dz-b,0),(dz,b)):
            for x,y in outline(dx-inset,dy-inset,max(b*.45,b-inset*.5)):
                verts.append(tuple(q @ Vector((x,y,z)) + Vector(p)))
        faces=[tuple(reversed(range(8)))]
        for j in range(3):
            for i in range(8):
                n=(i+1)%8; faces.append((j*8+i,j*8+n,(j+1)*8+n,(j+1)*8+i))
        faces.append(tuple(range(24,32)))
        return self.mesh(name, verts, faces, material)

    def beam(self, name, a, b, width, material, depth=None):
        a,b = Vector(a),Vector(b); d=b-a
        return self.box(name,(a+b)*.5,(width,depth or width,d.length),material,
                        bevel=min(.08,width*.1),rotation=d.to_track_quat('Z','Y'))

    def profile(self, name, p, rings, material, sides=20, rotation=None, caps=True, ring_materials=None):
        q=rotation or Quaternion((0,0,1),0); verts=[]; faces=[]; keys=[]
        for z,r in rings:
            for i in range(sides):
                a=TAU*i/sides; verts.append(tuple(q@Vector((r*math.cos(a),r*math.sin(a),z))+Vector(p)))
        if caps: faces.append(tuple(reversed(range(sides)))); keys.append(material)
        for j in range(len(rings)-1):
            for i in range(sides):
                n=(i+1)%sides
                faces.append((j*sides+i,j*sides+n,(j+1)*sides+n,(j+1)*sides+i))
                keys.append(ring_materials[j] if ring_materials else material)
        if caps: faces.append(tuple(range((len(rings)-1)*sides,len(rings)*sides))); keys.append(material)
        return self.mesh(name,verts,faces,material,keys)

    def tube(self,name,a,b,r,material,sides=12):
        a,b=Vector(a),Vector(b); d=b-a; e=min(.10,r*.12,d.length*.12)
        return self.profile(name,a,[(0,r-e),(e,r),(d.length-e,r),(d.length,r-e)],material,sides,d.to_track_quat('Z','Y'))

    def pipe(self, name, points, r, material):
        for i,(a,b) in enumerate(zip(points,points[1:])):
            self.tube(name+str(i),a,b,r,material,12)
            if i+1 < len(points)-1:
                # An oversized flange visually joins the angular elbow.
                d=(Vector(b)-Vector(a)).normalized()
                self.tube('Elbow collar',Vector(b)-d*.16,Vector(b)+d*.16,r*1.19,'rust_dark',12)

    def slab(self,name,polygon,z0,z1,material,top=None):
        n=len(polygon); v=[(x,y,z) for z in (z0,z1) for x,y in polygon]
        f=[tuple(reversed(range(n)))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]+[tuple(range(n,n*2))]
        return self.mesh(name,v,f,material,[material]*(n+1)+[top or material])

    def rock(self, name, p, dims, seed, shape='boulder'):
        """Layered closed rock, large planar cuts and displaced strata; no tiny spikes."""
        rng=random.Random(seed); sx,sy,h=dims; n=10 if shape in ('ridge','cliff') else 8
        if shape=='cliff': levels=[(0,.92),(.21,1),(.46,.93),(.49,.81),(.76,.79),(.82,.71),(1,.58)]
        elif shape=='ridge': levels=[(0,.95),(.18,1),(.44,.88),(.48,.76),(.76,.67),(1,.30)]
        else: levels=[(0,.80),(.18,1),(.49,.90),(.54,.79),(.82,.57),(1,.30)]
        angle=[TAU*(i+rng.uniform(-.14,.14))/n for i in range(n)]
        rr=[rng.uniform(.81,1.18) for _ in range(n)]
        crest=[rng.uniform(-.13,.13) for _ in range(n)]
        verts=[]
        for j,(z,r) in enumerate(levels):
            for i,a in enumerate(angle):
                r0=r*rr[i]*rng.uniform(.96,1.04)
                shift=(z*.11 if shape=='cliff' else z*.09)
                xx=p[0]+sx*(.5*math.cos(a)*r0+shift); yy=p[1]+sy*(.5*math.sin(a)*r0+z*.035)
                zz=p[2]+h*(z+(crest[i]*z if j else 0)+(.022*rng.uniform(-1,1) if j else 0))
                verts.append((xx,yy,zz))
        faces=[tuple(reversed(range(n)))]; keys=['stone_dark']
        for j in range(len(levels)-1):
            for i in range(n):
                a=j*n+i;b=j*n+(i+1)%n;c=(j+1)*n+(i+1)%n;d=(j+1)*n+i
                # Mostly gentle value shifts; thin ledges use one darker band.
                key='strata' if 0<j and levels[j+1][0]-levels[j][0]<.09 else rng.choices(['stone','stone_warm','stone_light','stone_dark'],[55,25,13,7])[0]
                faces.extend([(a,b,c),(a,c,d)]); keys.extend([key,key])
        faces.append(tuple(range((len(levels)-1)*n,len(levels)*n)));keys.append('stone_light')
        return self.mesh(name,verts,faces,'stone',keys)

    def finish(self):
        if not self.pieces: raise RuntimeError('Empty module: '+self.name)
        select_only(*self.pieces, active=self.pieces[0]); bpy.ops.object.join(); o=bpy.context.object
        o.name=self.name; o.data.name=self.name+'_GameMesh'
        # Recalculate winding and triangulate before UV/baking so Unity and Blender agree.
        bm=bmesh.new(); bm.from_mesh(o.data); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));
        bmesh.ops.triangulate(bm,faces=list(bm.faces)); bm.to_mesh(o.data); bm.free(); o.data.update()
        for p in o.data.polygons: p.use_smooth=False
        # Join can leave unused/duplicate slots; bake only the materials actually used.
        bpy.ops.object.material_slot_remove_unused()
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(68), island_margin=.012)
        bpy.ops.object.mode_set(mode='OBJECT'); o.data.uv_layers.active.name='AtlasUV'
        return o


def build_rock(b, dims, seed, shape):
    b.rock('Primary mass',(0,0,0),dims,seed,shape)
    if shape in ('ridge','cliff'):
        x,y,h=dims
        b.rock('Broken shoulder',(-x*.27,y*.20,0),(x*.47,y*.65,h*.48),seed+83,'boulder')
        b.rock('Foot ledge',(x*.24,-y*.31,0),(x*.42,y*.52,h*.27),seed+19,'boulder')


def refinery_main(b):
    b.box('Foundation',(0,0,.35),(34,24,.7),'concrete',bevel=.2)
    b.box('Interior floor',(0,0,.73),(30,20,.18),'interior')
    for x in (-14,-7,0,7,14):
        for y in (-10.5,10.5):
            h=10.5 if x<7 or y<0 else (6.4 if x==7 else 3.0)
            b.box('Column',(x,y,.7+h/2),(1.7,1.6,h),'concrete_light',bevel=.19)
            b.box('Column footing',(x,y,1),(2.3,2.4,.65),'fracture')
    b.box('Solid end wall',(-15,0,5.7),(1.7,20,10),'concrete',bevel=.16)
    for x in (-10.5,-3.5,3.5,10.5):
        for y in (-10.5,10.5):
            b.box('Wall sill',(x,y,1.95),(5.6,1.15,2.6),'concrete')
            if not (x>=3.5 and y>0):
                b.box('Lintel',(x,y,9.9),(5.6,1.3,1.3),'concrete_light')
            if x < 1 and y<0:
                b.box('Vertical frame',(x,y,5.6),(.46,.6,6.2),'steel',bevel=.06)
    roof=[(-16,-11.7),(1,-11.7),(2.8,-8.1),(.6,-5.5),(3.5,-2.2),(-.1,.4),(2.2,3.7),(-1,6.2),(1.4,8.4),(-2.5,11.7),(-16,11.7)]
    b.slab('Fractured roof',roof,10.9,11.8,'fracture','roof')
    for x in (-14.8,-10.2,-5.6):
        b.box('Roof seam',(x,0,11.85),(.19,22,.13),'concrete_light',bevel=.03)
    b.box('Roof ventilation block',(-8,-3,12.45),(5.2,6,1.3),'concrete_light')
    b.box('Vent hood',(-8,-3,13.25),(5.8,6.6,.65),'steel')
    for x in (0,7,14):
        b.beam('Exposed crossbeam',(x,-10.5,10.65),(x,10.5,10.65),.73,'steel')
    b.beam('Bent girder',(7,10.3,10.3),(14,10.3,4.0),.62,'rust')
    b.beam('Roof longitudinal seam',(-14,-9,10.5),(14,-9,10.5),.45,'rust_dark')
    b.box('Pump plinth',(7,-1,1.2),(6.6,5.4,1),'concrete')
    b.tube('Horizontal process vessel',(4,-1,3),(10,-1,3),1.7,'tank',16)
    for x in (4.3,9.7): b.tube('Vessel band',(x-.15,-1,3),(x+.15,-1,3),1.82,'rust',16)
    b.pipe('Interior pipe',[(10,-1,3),(12,-1,3),(12,4,3),(12,4,1.2)],.48,'pipe')
    for x,y,ang in ((12,8,24),(15,5,-18),(7,8,14)):
        b.box('Fallen slab',(x,y,1.1),(4.2,2.7,.75),'fracture',yaw=ang,bevel=.23)


def refinery_frame(b):
    b.box('Foundation',(0,0,.28),(25,18,.56),'fracture')
    for i,x in enumerate((-10,-3.4,3.4,10)):
        for y in (-7.5,7.5):
            h=10.6 if not (i==3 and y>0) else 5.8
            b.box('Foot',(x,y,.65),(2,2,.8),'concrete')
            b.box('Steel column',(x,y,.5+h/2),(.7,.8,h),'steel',bevel=.07)
        b.beam('Truss top',(x,-7.5,11),(x,0,12.8),.58,'rust')
        if i!=3: b.beam('Truss top',(x,0,12.8),(x,7.5,11),.58,'rust')
        b.beam('Truss tie',(x,-7.5,11),(x,7.5,11),.45,'steel')
        for y in (-4,4): b.beam('Truss web',(x,y,11),(x,0,12.8),.27,'steel')
        if i<3:
            for y in (-7.5,7.5):
                b.beam('Longitudinal member',(x,y,10.8),(x+6.7,y,10.8 if i!=2 or y<0 else 6.3),.5,'steel')
                if i!=1: b.beam('Lateral brace',(x,y,1.0),(x+6.7,y,10.2 if i!=2 or y<0 else 6),.30,'rust_dark')
    b.slab('Partial roof',[(-11,-7.8),(-4,-7.8),(-3,-4.1),(-5,-2),(-3,1),(-5,5),(-11,5)],11.05,11.45,'rust_dark','roof')


def refinery_chimney(b):
    b.box('Foundation',(0,0,.4),(7.4,7.4,.8),'concrete')
    b.profile('Tapered stack',(0,0,0),[(.7,2.75),(1.3,2.7),(4,2.35),(4.2,2.24),(10,2.0),(10.15,2.03),(15.2,1.78),(18.2,1.65)],'concrete',24,
              ring_materials=['fracture','concrete','fracture','concrete','rust_dark','concrete','fracture'])
    # Hollow top lip with a dark inset, not a solid black painted disc on the rim.
    b.profile('Hollow mouth',(0,0,0),[(17.5,1.66),(18.65,1.82),(18.82,1.65),(18.82,1.25),(17.55,1.20),(17.5,1.66)],'fracture',24,caps=False,
              ring_materials=['rust','concrete_light','concrete_light','interior','interior'])
    for z,r in ((9.8,2.1),(13.8,1.91)):
        b.profile('Oxide band',(0,0,z),[(0,r),(.8,r)],'rust',24)
    for x in (-.55,.55): b.beam('Ladder rail',(x,-2.5,1.4),(x,-1.8,17.3),.15,'steel')
    for i in range(19):
        z=2+i*.78; y=-2.5+(z-1.4)/15.9*.7
        b.beam('Rung',(-.55,y,z),(.55,y,z),.13,'rust_dark')


def refinery_tank(b,r,h):
    b.profile('Concrete footing',(0,0,0),[(0,r+1),(0.5,r+1),(.75,r+.7)],'concrete',28)
    b.profile('Shell',(0,0,0),[(.7,r-.08),(.9,r),(h-.25,r),(h,r-.1)],'tank',28)
    for z in (1.1,h*.43,h*.72,h-.3):
        b.profile('Weld/rust band',(0,0,z),[(0,r+.06),(.19,r+.06)],'rust_dark',28)
    b.profile('Low cone roof',(0,0,0),[(h,r+.13),(h+.25,r+.13),(h+1.05,r*.30),(h+1.15,r*.10)],'tank_top',28)
    for i in range(8):
        a=i*TAU/8; d=Vector((math.cos(a),math.sin(a),0))
        b.beam('Roof rib',d*r*.23+Vector((0,0,h+1.0)),d*r*.92+Vector((0,0,h+.37)),.14,'rust_dark')
    b.profile('Access hatch',(1,-.4,h+1),[(0,1.0),(.25,1.0),(.36,.86)],'steel',16)
    b.tube('Vent riser',(-r*.34,0,h+.6),(-r*.34,0,h+1.7),.38,'pipe')
    b.tube('Outlet',(r-.3,0,1.8),(r+2.3,0,1.8),.56,'pipe')
    b.tube('Outlet flange',(r+1.65,0,1.8),(r+1.94,0,1.8),.76,'rust_dark')
    b.box('Service landing',(0,-r-.72,h-.1),(2.4,1.6,.36),'steel')
    for x in (-.68,.68): b.beam('Ladder side',(x,-r-.46,.85),(x,-r-.46,h+.65),.17,'steel')
    for i in range(int(h/.65)):
        z=1.0+i*.65;b.beam('Ladder rung',(-.68,-r-.46,z),(.68,-r-.46,z),.14,'rust_dark')


def pipe_rack(b,elbow=False):
    lx=15 if not elbow else 13
    for x in (-lx*.42,0,lx*.42):
        for y in (-2.25,2.25):
            b.box('Concrete pier',(x,y,.55),(1.6,1.6,1.1),'concrete')
            b.box('Upright',(x,y,3.3),(.56,.56,5.2),'steel')
        b.beam('Crossmember',(x,-2.55,5.7),(x,2.55,5.7),.46,'rust_dark')
        b.beam('Brace',(x,-2.25,1.3),(x,2.25,5.3),.26,'steel')
    for i,y in enumerate((-1.35,0,1.35)):
        z=6.2+i*.13;r=.46 if i!=1 else .58
        pts=[(-lx/2,y,z),(lx/2,y,z)] if not elbow else [(-lx/2,y,z),(lx/2-1.4,y,z),(lx/2,y+1.6,z),(lx/2,y+5,z),(lx/2,y+5,1.2)]
        b.pipe('Process line',pts,r,'pipe' if i!=1 else 'rust')
        for x in (-4,3): b.tube('Flange',(x-.12,y,z),(x+.12,y,z),r*1.22,'rust_dark')
    b.box('Valve mount',(-1,-2.5,6.2),(1.4,1.5,1),'rust')


def control_hut(b):
    b.box('Footing',(0,0,.3),(10,8,.6),'concrete')
    b.box('Cabin',(0,0,2.9),(8.8,6.7,4.7),'concrete_light',bevel=.22)
    b.box('Roof',(0,0,5.4),(9.4,7.3,.65),'roof',bevel=.18)
    b.box('Recessed door',(-1.8,-3.42,1.9),(1.6,.18,2.9),'steel')
    for x in (1,2.8): b.box('Dark window',(x,-3.43,3),(1.35,.16,1.3),'glass')
    b.box('Broken canopy',(-1.8,-4.2,3.7),(2.4,1.7,.28),'rust_dark',yaw=-9)
    b.box('Roof equipment',(2,-1,5.9),(2.6,1.8,.65),'steel')
    b.beam('Antenna',(-2,2,5.6),(-2,2,7.6),.20,'steel')


def barriers(b):
    for i,x in enumerate((-3.6,3.6)):
        b.box('Barrier base',(x,0,.42),(6.7,2.2,.84),'fracture',bevel=.17)
        b.box('Barrier wall',(x,0,1.18),(6.4,1.2,1.55),'concrete_light',bevel=.17)
        for dx in (-2,2): b.box('Faded marker',(x+dx,-.61,1.25),(.8,.12,.65),'warning',bevel=.025)


def debris(b,metal=False):
    if not metal:
        for i,(x,y,z,w,d,h,ang) in enumerate([(-2,-1,.55,4.8,2.3,1,23),(1.2,1,.75,3.4,2.6,1.35,-21),(2.3,-1,.4,2.2,1.9,.75,42),(-.2,2,.35,2.2,1.5,.65,16)]):
            b.box('Broken concrete',(x,y,z),(w,d,h),'fracture' if i==1 else 'concrete',yaw=ang,bevel=.19)
        b.beam('Bent rebar',(-3,-.5,.8),(-4,1.8,1.2),.16,'rust_dark')
    else:
        b.box('Fallen panel',(-1,0,.65),(5.4,2.8,.35),'roof',yaw=18)
        b.beam('Twisted beam',(-3,-2,.4),(2,1,1.6),.48,'rust')
        b.beam('Broken frame',(2,1,1.6),(3.6,2,.4),.46,'steel')
        b.tube('Broken pipe',(-2,1.8,.7),(2.8,2.6,.8),.58,'pipe')


# Sizes are actual game-world units, not rescaled miniature props.
ROCKS = [
    ('Rock_Large_A',(18,13,9),401,'boulder'), ('Rock_Large_B',(15,18,12),509,'boulder'),
    ('Rock_Large_C',(23,12,7),614,'boulder'), ('Boulder_A',(5.5,4.4,3.4),702,'boulder'),
    ('Boulder_B',(7,5,3),817,'boulder'), ('Ridge_Chunk_A',(36,15,15),901,'ridge'),
    ('Ridge_Chunk_B',(30,21,13),1007,'ridge'), ('Cliff_Wall_A',(28,13,17),1113,'cliff'),
    ('Cliff_Wall_B',(34,15,14),1219,'cliff'),
]
INDUSTRIAL = [
    ('Refinery_MainBlock',refinery_main), ('Refinery_Frame_A',refinery_frame),
    ('Refinery_Chimney',refinery_chimney), ('Refinery_Tank_Large',lambda b: refinery_tank(b,6.5,8.6)),
    ('Refinery_Tank_Small',lambda b: refinery_tank(b,4.5,6.1)),
    ('Refinery_PipeRack_A',pipe_rack), ('Refinery_PipeRack_B',lambda b: pipe_rack(b,True)),
    ('Refinery_ControlHut',control_hut), ('Concrete_Barrier_Set',barriers),
    ('Debris_Concrete_A',debris), ('Debris_Metal_A',lambda b: debris(b,True)),
]


def png(path, pixels, srgb=False):
    # Blender bake pixels are bottom-up and RGB linear for the base-color target.
    rgb=np.asarray(pixels[:,:,:3],dtype=np.float32)
    rgb=np.nan_to_num(rgb,nan=0,posinf=1,neginf=0); rgb=np.clip(rgb,0,1)
    if srgb: rgb=np.where(rgb<=.0031308,12.92*rgb,1.055*np.power(rgb,1/2.4)-.055)
    rgb=np.rint(rgb[::-1]*255).astype(np.uint8); h,w,_=rgb.shape
    def chunk(t,d): return struct.pack('!I',len(d))+t+d+struct.pack('!I',zlib.crc32(t+d)&0xffffffff)
    raw=b''.join(b'\0'+row.tobytes() for row in rgb)
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('!2I5B',w,h,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(raw,6))+chunk(b'IEND',b''))


def bake(scene, obj, size, kind, high=None):
    image=bpy.data.images.new(obj.name+'_'+kind,width=size,height=size,alpha=True,float_buffer=True)
    image.colorspace_settings.name='Non-Color'; image.generated_color=(.5,.5,1,1) if kind=='Normal' else (0,0,0,0)
    temp=[]
    try:
        for m in set(obj.data.materials):
            n=m.node_tree.nodes;l=m.node_tree.links;out=next(x for x in n if x.type=='OUTPUT_MATERIAL')
            old=out.inputs['Surface'].links[0].from_socket
            tex=n.new('ShaderNodeTexImage');tex.image=image;n.active=tex
            extra=[]
            if kind in ('BaseColor','AO'):
                emit=n.new('ShaderNodeEmission');extra.append(emit)
                if kind=='AO':
                    node=n.new('ShaderNodeAmbientOcclusion');node.inputs['Distance'].default_value=2.2
                    node.only_local=True;extra.append(node);l.new(node.outputs['AO'],emit.inputs['Color'])
                else:
                    input0=n.get('Principled BSDF').inputs['Base Color']
                    l.new(input0.links[0].from_socket,emit.inputs['Color'])
                l.new(emit.outputs[0],out.inputs['Surface'])
            temp.append((m,out,old,tex,extra))
        scene.render.bake.use_selected_to_active=high is not None
        scene.render.bake.use_clear=True;scene.render.bake.margin=8
        if high is None: select_only(obj)
        else:
            select_only(high,obj,active=obj)
            scene.render.bake.cage_extrusion=.18;scene.render.bake.max_ray_distance=.42
            scene.render.bake.normal_space='TANGENT'
        result=bpy.ops.object.bake(type='NORMAL' if kind=='Normal' else 'EMIT')
        if 'FINISHED' not in result: raise RuntimeError('Bake cancelled: '+obj.name+' '+kind)
        pixels=np.empty(size*size*4,dtype=np.float32);image.pixels.foreach_get(pixels)
        pixels=pixels.reshape((size,size,4))
        if not np.isfinite(pixels).all() or (kind=='BaseColor' and float(np.max(pixels[:,:,:3]))<.02):
            raise RuntimeError('Empty/invalid bake: '+obj.name+' '+kind)
        png(OUT/(obj.name+'_'+kind+'.png'),pixels,kind=='BaseColor')
    finally:
        for m,out,old,tex,extra in temp:
            m.node_tree.links.new(old,out.inputs['Surface'])
            for node in [tex]+extra: m.node_tree.nodes.remove(node)
        bpy.data.images.remove(image)
        scene.render.bake.use_selected_to_active=False


def make_high(low,collection,is_rock):
    high=low.copy();high.data=low.data.copy();high.name=low.name+'_High';collection.objects.link(high)
    select_only(high)
    bevel=high.modifiers.new('Micro edge relief','BEVEL');bevel.width=.085 if is_rock else .065
    bevel.segments=3;bevel.limit_method='ANGLE';bevel.angle_limit=math.radians(27);bevel.use_clamp_overlap=True
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    # Smooth the new bevel faces via weighted split normals, retaining the large planes.
    for p in high.data.polygons:p.use_smooth=True
    weighted=high.modifiers.new('Weighted plane normals','WEIGHTED_NORMAL');weighted.keep_sharp=True;weighted.weight=50
    bpy.ops.object.modifier_apply(modifier=weighted.name)
    return high


def main():
    args=parse_args(); OUT.mkdir(parents=True,exist_ok=True);SOURCE.parent.mkdir(parents=True,exist_ok=True)
    if MANIFEST.exists(): MANIFEST.unlink()  # only completed runs have a manifest
    bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene
    bpy.context.preferences.filepaths.save_version=0
    scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
    scene.render.engine='CYCLES';scene.cycles.samples=args.samples if not args.quick else 8
    if args.device!='CPU':
        prefs=bpy.context.preferences.addons['cycles'].preferences
        prefs.compute_device_type=args.device
        if hasattr(prefs,'refresh_devices'): prefs.refresh_devices()
        else: prefs.get_devices()
        gpu=[d for d in prefs.devices if d.type==args.device]
        if not gpu: raise RuntimeError('No '+args.device+' device detected. Try --device CUDA or --device CPU.')
        for d in prefs.devices:d.use=d in gpu
        scene.cycles.device='GPU'; print('CYCLES GPU:',', '.join(d.name for d in gpu),flush=True)
    else:scene.cycles.device='CPU'
    scene.render.bake.target='IMAGE_TEXTURES';scene.render.bake.margin_type='EXTEND'
    scene.world=bpy.data.worlds.new('Neutral bake world');scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.3
    low_col=bpy.data.collections.new('01 - Game meshes');scene.collection.children.link(low_col)
    high_col=bpy.data.collections.new('02 - High poly bake sources');scene.collection.children.link(high_col)
    for key in COLORS:MATS[key]=make_material(key)
    atlas=bpy.data.materials.new('FrontierAtlas')
    specs=[(name,lambda b,d=d,s=s,k=k:build_rock(b,d,s,k),True) for name,d,s,k in ROCKS]+[(n,f,False) for n,f in INDUSTRIAL]
    for i,(name,build,is_rock) in enumerate(specs):
        print(f'[{i+1}/{len(specs)}] {name}',flush=True)
        builder=Builder(low_col,name);build(builder);low=builder.finish()
        size=512 if args.quick else args.hero_size if name in ('Refinery_MainBlock','Refinery_Frame_A','Ridge_Chunk_A','Cliff_Wall_A') else args.size
        bake(scene,low,size,'BaseColor')
        high=None
        if not args.quick:
            bake(scene,low,size,'AO');high=make_high(low,high_col,is_rock);bake(scene,low,size,'Normal',high)
        else:
            # Don't accidentally reuse the previous full bake with regenerated UVs.
            for suffix in ('AO','Normal'):
                p=OUT/(name+'_'+suffix+'.png')
                if p.exists():p.unlink()
        # Export a one-submesh copy; keep the editable procedural material slots on low/high.
        export=low.copy(); export.data=low.data.copy(); low_col.objects.link(export)
        export.data.materials.clear();export.data.materials.append(atlas)
        for face in export.data.polygons:face.material_index=0
        select_only(export)
        bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},
                                 axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,
                                 use_mesh_modifiers=True,path_mode='STRIP')
        export_data=export.data;bpy.data.objects.remove(export,do_unlink=True);bpy.data.meshes.remove(export_data)
        # Keep editable geometry and high sources in one organized Blender file.
        pos=Vector(((i%5)*48,(i//5)*42,0));low.location=pos;low.hide_render=True;low.select_set(False)
        if high is not None:high.location=pos;high.hide_render=True;high.select_set(False);high.hide_set(True)
        low.hide_set(True)
    for o in low_col.objects:o.hide_set(False);o.hide_render=False
    high_col.hide_render=True;high_col.hide_viewport=True
    bpy.ops.object.select_all(action='DESELECT');bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
    MANIFEST.write_text(json.dumps({'version':1,'fullBake':not args.quick,'models':[n for n,_,_ in specs]},indent=2)+'\n',encoding='utf-8')
    print(f'FRONTIER KIT READY: {OUT}\nBLEND: {SOURCE}\n20 FBX; mode={"base only" if args.quick else "base + local AO + normal"}',flush=True)


if __name__=='__main__':main()
