"""Connected, low-poly canopy landforms, not joined copies of individual trees.
Run: blender -b --python-exit-code 1 --python Tools/Blender/generate_forest_mass_pack.py
Z-up; ground pivot at zero; deterministic meshes and a shared color ramp.
"""
from pathlib import Path
from math import sin, cos, pi, exp, sqrt, radians
from collections import Counter
import random
import struct
import zlib

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets' / '_Project' / 'Art' / 'Nature' / 'ForestMasses'
SOURCE = ROOT / 'Resources' / 'Blender' / 'ForestMassPack.blend'
# name, width, depth, max height, bias, seed
SPECS = (
    ('ForestCore_A', 34, 30, 8.5, 'core', 14),
    ('ForestEdge_A', 36, 24, 8.0, 'edge', 29),
    ('ForestEdge_B', 31, 23, 8.0, 'edge', 45),
    ('ForestCorner_A', 30, 30, 8.2, 'corner', 68),
    ('ForestIsland_A', 23, 18, 7.5, 'island', 82),
    ('ForestFinger_A', 17, 34, 7.6, 'finger', 93),
)


def clamp(x, lo=0, hi=1): return min(hi, max(lo, x))


def geometry(spec):
    name, w, d, h, kind, seed = spec
    rng = random.Random(seed)
    phase = [rng.uniform(0, 2*pi) for _ in range(5)]
    n, bands = 64, 9
    rx, ry = w/2, d/2
    # Low frequency lobes define the silhouette. Finer scallops break its edges.
    def radius(a):
        r = 1 + .105*cos(3*a+phase[0]) + .065*sin(5*a+phase[1]) + .036*cos(11*a+phase[2])
        if kind == 'edge': r *= 1-.17*max(0, -sin(a))**2 + .08*cos(2*a)
        if kind == 'corner': r *= 1-.24*max(0, cos(a+1.0))**3
        if kind == 'finger': r *= 1-.15*cos(2*a+phase[3])
        if kind == 'island': r *= 1+.08*cos(2*a+phase[4])
        return r
    # Continuous surface with broad, shallow organic swells, not spherical crowns.
    bumps = [(rng.uniform(-.67,.67),rng.uniform(-.67,.67),rng.uniform(.20,.36),rng.uniform(.35,.70)) for _ in range(8)]
    def crown_height(x,y,r):
        u,v=x/rx,y/ry
        wave=.52+.07*cos(5.0*u+2.0*v+phase[0])+.055*sin(2*u-6*v+phase[1])
        roll=sum(a*exp(-((u-bx)**2+(v-by)**2)/(s*s)) for bx,by,s,a in bumps)
        envelope=1-.32*r**3
        return h*clamp((wave+.28*roll)*envelope,.38,.98)
    vertices=[]; tones=[]; faces=[]; smooth=[]
    def vertex(x,y,z,t):
        vertices.append((x,y,z));tones.append(clamp(t));return len(vertices)-1
    def face(ids, soft=True): faces.append(tuple(ids));smooth.append(soft)
    center=vertex(0,0,crown_height(0,0,0),.62)
    for j in range(1,bands+1):
        r=j/bands
        for i in range(n):
            a=2*pi*i/n
            x,y=rx*radius(a)*r*cos(a),ry*radius(a)*r*sin(a)
            z=crown_height(x,y,r)
            # Color is continuous and spatially coherent; keep highlights restrained.
            tone=.34+.33*z/h+.09*sin(x*.18+y*.11+phase[2])+.055*cos(y*.35-x*.16+phase[3])-.12*r**7
            vertex(x,y,z,tone)
    for i in range(n): face((center,1+i,1+(i+1)%n))
    for j in range(bands-1):
        for i in range(n):
            a=1+j*n+i;b=1+j*n+(i+1)%n;c=b+n;d=a+n
            face((a,d,c));face((a,c,b))
    # Broad scalloped shoulders then a dark recessed skirt down to ground.
    top=1+(bands-1)*n
    for level in range(3):
        for i in range(n):
            a=2*pi*i/n; x,y,z=vertices[top+i]
            if level==0:
                factor=1.025+.018*sin(9*a+phase[1]); zz=z*.73; tone=.34+.04*cos(5*a+phase[0])
            elif level==1:
                factor=.94+.025*sin(7*a+phase[2]); zz=.75+.30*(1+sin(5*a+phase[4])); tone=.16
            else:
                factor=.81; zz=0; tone=.065
            vertex(x*factor,y*factor,zz,tone)
        prev=top+level*n; cur=prev+n
        for i in range(n):
            j=(i+1)%n;face((prev+i,cur+i,cur+j));face((prev+i,cur+j,prev+j))
    base=vertex(0,0,0,.05);start=top+3*n
    for i in range(n): face((base,start+(i+1)%n,start+i),False)
    # Verify one closed, consistently oriented connected surface.
    edges=Counter(tuple(sorted((f[i],f[(i+1)%len(f)]))) for f in faces for i in range(len(f)))
    directed=Counter((f[i],f[(i+1)%len(f)]) for f in faces for i in range(len(f)))
    assert set(edges.values())=={2}
    assert all(directed[(b,a)]==1 and count==1 for (a,b),count in directed.items())
    assert min(z for x,y,z in vertices)==0
    assert set(i for f in faces for i in f)==set(range(len(vertices)))
    return vertices,faces,tones,smooth


def write_ramp(path):
    stops=[(0,'223D2C'),(.24,'31543A'),(.48,'456D3E'),(.72,'678648'),(1,'91A65C')]
    def rgb(t):
        for (a,c),(b,d) in zip(stops,stops[1:]):
            if a<=t<=b:
                f=(t-a)/(b-a);return bytes(round(x+(y-x)*f) for x,y in zip(bytes.fromhex(c),bytes.fromhex(d)))
        return bytes.fromhex(stops[-1][1])
    row=b'\x00'+b''.join(rgb(i/255) for i in range(256))
    def chunk(kind,data): return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',256,16,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(row*16,9))+chunk(b'IEND',b''))


def main():
    import bpy
    from mathutils import Euler
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene;scene.unit_settings.system='METRIC'
    OUT.mkdir(parents=True,exist_ok=True);SOURCE.parent.mkdir(parents=True,exist_ok=True)
    ramp=OUT/'ForestRamp.png';write_ramp(ramp)
    image=bpy.data.images.load(str(ramp));image.pack()
    mat=bpy.data.materials.new('Forest_Canopy');mat.use_nodes=True
    nodes=mat.node_tree.nodes;tex=nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation='Linear'
    bsdf=nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=.95
    mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color']);nodes.active=tex
    objects=[]
    for spec in SPECS:
        name=spec[0];v,f,tones,soft=geometry(spec)
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(v,[],f);mesh.update();mesh.materials.append(mat)
        uv=mesh.uv_layers.new(name='CanopyRamp')
        for poly,s in zip(mesh.polygons,soft):
            poly.use_smooth=s
            for i in poly.loop_indices:
                uv.data[i].uv=((.5+255*tones[mesh.loops[i].vertex_index])/256,.5)
        obj=bpy.data.objects.new(name,mesh);scene.collection.objects.link(obj);objects.append(obj)
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False)
        print(f'{name}: {len(v)} vertices, {len(f)} triangles, one connected mesh',flush=True)
    for i,obj in enumerate(objects):obj.location=((i%3-1)*44,(i//3-.5)*44,0)
    bpy.ops.object.select_all(action='DESELECT')
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                s=area.spaces.active;s.shading.type='SOLID';s.shading.color_type='TEXTURE'
                s.region_3d.view_distance=155;s.region_3d.view_location=(0,0,3)
                s.region_3d.view_rotation=Euler((radians(36),0,radians(-12)),'XYZ').to_quaternion();s.region_3d.view_perspective='ORTHO';s.overlay.show_floor=False
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
    print(f'FOREST MASS PACK READY: {OUT}\nBLEND: {SOURCE}',flush=True)


if __name__=='__main__': main()
