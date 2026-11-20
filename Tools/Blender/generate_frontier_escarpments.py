"""V16: four coherent sandstone landforms. Geometry and albedo only; no lighting bake.
Run from repo root: blender -b --python-exit-code 1 --python Tools/Blender/generate_frontier_escarpments.py
The pure build() function can also be imported without Blender for topology checks.
"""
from math import sin, cos, pi, sqrt
from pathlib import Path
import random
import struct
import zlib

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Environment/FrontierEscarpments'
SOURCE = ROOT / 'Resources/Blender/FrontierEscarpments.blend'
# name, length along X, depth along Y, height from buried base, seed, elongation shape
SPECS = (
    ('BrokenMesa', 152, 80, 30, 104, 2.5),
    ('EastEscarpment', 84, 142, 32, 217, 2.2),
    ('SplitRidge', 152, 80, 28, 391, 1.8),
    ('WesternButte', 80, 105, 29, 482, 2.8),
)
# Local mineral/albedo variations, not a directional light gradient.
PALETTE = ('B09579', 'BDA38A', 'A38669', 'C1A78A', '9A7C62', 'B59A7E', 'AD9277', 'C5AB8D')
S = 24
ATLAS = 128
CELL = 32


def build(spec):
    name, width, depth, height, seed, exponent = spec
    rng = random.Random(seed)
    phases = [rng.uniform(0, 2*pi) for _ in range(4)]
    # Coherent broad lobes, not independent random bulges. Hard cuts at selected sectors.
    notches = [(rng.randrange(S), rng.uniform(.14, .24)) for _ in range(3)]
    edge = []
    for i in range(S):
        a = 2*pi*i/S
        c, s = cos(a), sin(a)
        signed = lambda v: (1 if v >= 0 else -1)*abs(v)**(2/exponent)
        r = 1 + .12*sin(3*a+phases[0]) + .07*cos(5*a+phases[1])
        cut = sum(d*max(0, 1-min((i-j)%S,(j-i)%S)/2) for j,d in notches)
        edge.append((signed(c)*r*(1-cut), signed(s)*r*(1-cut), a, cut))
    # Skirt/talus, foot, exposed steep cliff, broken rim, broad irregular crown.
    # Rim heights rise/fall along the strike instead of forming uniform concentric bands.
    profiles = [(1.06,0),(.87,.25),(.72,.41),(.69,.72),(.61,.92),(.32,1.0)]
    verts, faces, colors = [], [], []
    for k,(scale,elev) in enumerate(profiles):
        for i,(x,y,a,cut) in enumerate(edge):
            undulation = .075*sin(2*a+phases[2])+.045*cos(3*a+phases[3])
            lateral = 1 + (0 if k==0 else .07*sin(3*a+phases[1]+k*.55))
            # Eroded clefts continue vertically through the wall; asymmetric top rim.
            lateral *= 1 - cut*(.2 if k<2 else .5 if k<5 else .1)
            center_x = .07*(k/5)*sin(phases[2]); center_y = .09*(k/5)*cos(phases[3])
            z = 0 if k==0 else max(.10, elev+undulation*(.40 if k<2 else 1.0))
            verts.append(((x*scale*lateral+center_x)*width/2, (y*scale*lateral+center_y)*depth/2, z*height))
    # Normalize measured bounds; exported dimensions match the scene placement contract.
    lo=[min(p[i] for p in verts) for i in range(3)]; hi=[max(p[i] for p in verts) for i in range(3)]
    verts=[((p[0]-(lo[0]+hi[0])/2)*width/(hi[0]-lo[0]),
            (p[1]-(lo[1]+hi[1])/2)*depth/(hi[1]-lo[1]),
            (p[2]-lo[2])*height/(hi[2]-lo[2])) for p in verts]
    def face(indices, color):
        faces.append(tuple(indices)); colors.append(color)
    face(reversed(range(S)), 2)  # buried bottom
    tones = [rng.choice((0,0,0,2,5,6)) for _ in range(S)]
    for k in range(len(profiles)-1):
        for i in range(S):
            j=(i+1)%S; a=k*S+i; b=k*S+j; c=(k+1)*S+j; d=(k+1)*S+i
            tone=tones[i]
            if k==2 and i%7 in (2,3): tone=4 # intermittent mineral beds, never a full ring
            if k==4: tone=rng.choice((0,1,1,5,5,6))
            # Triangulate irregular quads explicitly. Broad planes have shared albedo.
            if (k+i)%2: face((a,b,d),tone); face((b,c,d),tone)
            else: face((a,b,c),tone); face((a,c,d),tone)
    base=(len(profiles)-1)*S
    # A whole broad crown, slight tilt and broken rim. The triangles only split geometry,
    # not a radial alternating color pattern.
    top = verts[base:base+S]
    center = (sum(p[0] for p in top)/S, sum(p[1] for p in top)/S, sum(p[2] for p in top)/S)
    c=len(verts); verts.append(center)
    for i in range(S): face((base+i,base+(i+1)%S,c),1)
    return verts, faces, colors


def write_palette(path):
    def chunk(kind,data): return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
    raw=bytearray()
    for y in range(ATLAS):
        raw.append(0)
        for x in range(ATLAS):
            swatch=((ATLAS-1-y)//CELL)*4+x//CELL
            raw.extend(bytes.fromhex(PALETTE[swatch%len(PALETTE)]))
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',ATLAS,ATLAS,8,2,0,0,0))+
                     chunk(b'IDAT',zlib.compress(raw,9))+chunk(b'IEND',b''))


def main():
    import bpy
    OUT.mkdir(parents=True,exist_ok=True); SOURCE.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    palette=OUT/'EscarpmentPalette.png'; write_palette(palette)
    mat=bpy.data.materials.new('EscarpmentPalette'); mat.use_nodes=True
    image=bpy.data.images.load(str(palette),check_existing=False)
    image.pack()
    node=mat.node_tree.nodes.new('ShaderNodeTexImage'); node.image=image; node.interpolation='Closest'
    bsdf=mat.node_tree.nodes.get('Principled BSDF'); bsdf.inputs['Roughness'].default_value=.95
    mat.node_tree.links.new(node.outputs['Color'],bsdf.inputs['Base Color'])
    for spec in SPECS:
        name=spec[0]; vertices,faces,colors=build(spec)
        mesh=bpy.data.meshes.new(name); mesh.from_pydata(vertices,[],faces); mesh.update()
        if mesh.validate(): raise RuntimeError('Invalid topology: '+name)
        obj=bpy.data.objects.new(name,mesh); bpy.context.scene.collection.objects.link(obj)
        mesh.materials.append(mat); uv=mesh.uv_layers.new(name='PaletteUV')
        for poly,tone in zip(mesh.polygons,colors):
            poly.use_smooth=False
            u=(tone%4+.5)*CELL/ATLAS; v=(tone//4+.5)*CELL/ATLAS
            for j,li in enumerate(poly.loop_indices):
                a=2*pi*j/poly.loop_total; uv.data[li].uv=(u+.018*cos(a),v+.018*sin(a))
        bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active=obj
        bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},
                                 axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False)
        obj.location.x=SPECS.index(spec)*185
    bpy.ops.object.select_all(action='DESELECT')
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
    print('V16 escarpments ready:',OUT,flush=True)


if __name__=='__main__': main()
