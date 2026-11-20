"""Three stylized tree meshes with a shared palette atlas. No external assets.
blender -b --python-exit-code 1 --python Tools/Blender/generate_tree_pack.py
Geometry and PNG export are deterministic. Z up, origin at ground contact.
"""
from pathlib import Path
from math import cos, sin, pi, radians
import random
import struct
import zlib

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets' / '_Project' / 'Art' / 'Nature' / 'Trees'
SOURCE = ROOT / 'Resources' / 'Blender' / 'TreePack.blend'
PALETTE = ('624630', '896345', '294D35', '3C663B', '54813E', '729A4C', '91AD5B', '476F50')
ATLAS_SIZE, CELL = 128, 32


class Geometry:
    def __init__(self):
        self.vertices, self.faces, self.colors = [], [], []

    def face(self, indices, color):
        self.faces.append(tuple(indices))
        self.colors.append(color)

    def rings(self, cx, cy, rings, sides, seed, lobes, irregular, tones):
        """Closed, asymmetric ring loft. Each ring: z, radius-x, radius-y, offset-x/y.
        Shared vertices on each loft; broad, intentionally faceted bands of color.
        """
        rng = random.Random(seed)
        phase = rng.uniform(0, 2*pi)
        variation = [rng.uniform(-1, 1) for _ in range(sides)]
        first = len(self.vertices)
        for j, (z, rx, ry, dx, dy) in enumerate(rings):
            for i in range(sides):
                a = i*2*pi/sides + phase + (j % 2)*.09
                radial = 1 + irregular*(.65*cos(lobes*a+phase)+.35*variation[i])
                zz = z + irregular*min(rx, ry)*.15*sin(a*3+phase+j*.3)
                if j == 0 and z == 0: zz = 0
                self.vertices.append((cx+dx+rx*radial*cos(a), cy+dy+ry*radial*sin(a), zz))
        self.face([first+i for i in reversed(range(sides))], tones[0])
        for j in range(len(rings)-1):
            for i in range(sides):
                a=first+j*sides+i; b=first+j*sides+(i+1)%sides
                base=tones[min(j,len(tones)-1)]
                # Two adjacent faces usually share a shade, rather than random speckles.
                color=base
                if base in (3,4,5) and (i+seed)%sides in (0,1): color=base-1
                self.face((a,b,b+sides,a+sides),color)
        last=first+(len(rings)-1)*sides
        self.face([last+i for i in range(sides)],tones[-1])

    def trunk(self, x, y, height, width, seed, lean=(0,0)):
        self.rings(x,y,[(0,width,width,0,0),(.48*height,width*.76,width*.73,lean[0]*.45,lean[1]*.45),
                        (height,width*.36,width*.32,*lean)],7,seed,3,.13,(0,1,1))

    def crown(self, x, y, bottom, width, depth, height, seed, dark=False):
        # Broad shoulders, irregular outline and clipped crown. No perfect spheres.
        rx,ry=width*.5,depth*.5
        rings=[(bottom,rx*.35,ry*.36,0,0),
               (bottom+height*.23,rx*.91,ry*.88,-.10,0),
               (bottom+height*.53,rx,ry,.08,.04),
               (bottom+height*.82,rx*.73,ry*.75,.20,-.10),
               (bottom+height,rx*.29,ry*.32,.30,-.13)]
        self.rings(x,y,rings,11,seed,4,.24,(2,3,4,4,5) if dark else (3,4,4,5,6))

    def check(self):
        assert len(self.faces)==len(self.colors)
        from collections import Counter
        edges=Counter(tuple(sorted((f[i],f[(i+1)%len(f)]))) for f in self.faces for i in range(len(f)))
        assert set(edges.values())=={2}, 'Open or non-manifold loft'
        assert min(v[2] for v in self.vertices)==0
        assert all(0<=c<len(PALETTE) for c in self.colors)


def round_tree():
    g=Geometry()
    g.trunk(0,0,4.5,.55,12,(.4,-.18))
    # Crown overlaps deliberately; visible canopy is a broad asymmetric silhouette.
    g.crown(-.35,.12,2.45,6.5,6.2,5.05,31,True)
    g.crown(-2.2,-.65,3.1,3.8,4.2,3.65,48)
    g.crown(1.9,.95,3.7,4.5,4.1,3.95,57)
    g.crown(.25,-.32,5.25,4.6,4.6,3.65,73)
    return g


def pine_tree():
    g=Geometry()
    g.trunk(0,0,8.3,.43,25,(.20,-.16))
    # Three skirted tiers: flat broad lower edge, sloping upper shoulder, slender spire.
    for i,(z,r,h,x,y) in enumerate(((1.65,3.12,3.95,-.10,0),(4.10,2.45,3.65,.05,.12),(6.45,1.72,3.9,.20,-.09))):
        g.rings(x,y,[(z,r*.48,r*.48,0,0),(z+.25,r,r*.92,0,0),
                     (z+h*.32,r*.77,r*.74,.07,0),(z+h*.75,r*.29,r*.31,.12,-.08),
                     (z+h,r*.045,r*.045,.19,-.09)],10,80+i*9,5,.20,(2,3,7,4,5))
    return g


def cluster_tree():
    g=Geometry()
    for x,y,h,seed in ((-1.65,-.38,4.5,101),(1.50,.75,5.15,102),(.12,-1.05,4.3,103)):
        g.trunk(x,y,h,.36,seed,(x*.20,y*.20))
    g.crown(-1.92,-.35,2.4,5.2,5.5,4.4,108,True)
    g.crown(1.66,1.06,2.9,5.9,5.25,5.0,119)
    g.crown(.1,-2.0,3.45,5.15,4.7,4.15,124)
    g.crown(-.7,1.7,4.2,4.4,4.3,3.9,135,True)
    g.crown(.55,.27,5.25,4.8,4.1,3.85,140)
    return g


def write_palette(path):
    colors=[bytes.fromhex(value) for value in PALETTE]
    # PNG rows run top-down, UVs bottom-up. 4 x 4 broad solid swatches.
    raw=b''.join(b'\x00'+b''.join(colors[min(((ATLAS_SIZE-1-y)//CELL)*4+x//CELL,len(colors)-1)]
                     for x in range(ATLAS_SIZE)) for y in range(ATLAS_SIZE))
    def chunk(kind,data):
        return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
    header=struct.pack('>IIBBBBB',ATLAS_SIZE,ATLAS_SIZE,8,2,0,0,0)
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',header)+chunk(b'IDAT',zlib.compress(raw,9))+chunk(b'IEND',b''))


def main():
    import bpy
    from mathutils import Euler
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    scene.unit_settings.system='METRIC'
    OUT.mkdir(parents=True,exist_ok=True)
    SOURCE.parent.mkdir(parents=True,exist_ok=True)
    palette_path=OUT/'TreePalette.png'
    write_palette(palette_path)
    image=bpy.data.images.load(str(palette_path),check_existing=False)
    image.pack()
    mat=bpy.data.materials.new('Nature_TreePalette')
    mat.use_nodes=True
    nodes=mat.node_tree.nodes
    tex=nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation='Closest'
    bsdf=nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=.9
    mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
    nodes.active=tex
    objects=[]
    for name,generate in [('Tree_Round_01',round_tree),('Tree_Pine_01',pine_tree),('Tree_Cluster_01',cluster_tree)]:
        g=generate();g.check()
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(g.vertices,[],g.faces);mesh.update()
        mesh.materials.append(mat)
        uv=mesh.uv_layers.new(name='PaletteUV')
        # Use a tiny inset polygon per face, fully within its solid color swatch.
        for poly,color in zip(mesh.polygons,g.colors):
            poly.use_smooth=False
            u=(color%4+.5)*CELL/ATLAS_SIZE;v=(color//4+.5)*CELL/ATLAS_SIZE
            for i,loop in enumerate(poly.loop_indices):
                a=2*pi*i/poly.loop_total
                uv.data[loop].uv=(u+.022*cos(a),v+.022*sin(a))
        obj=bpy.data.objects.new(name,mesh);scene.collection.objects.link(obj);objects.append(obj)
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,
            object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False)
        mesh.calc_loop_triangles();print(f'{name}: {len(mesh.loop_triangles)} triangles, one material',flush=True)
    # Source is an editable lineup; FBXs above all retain the individual origin.
    for i,obj in enumerate(objects): obj.location.x=(i-1)*13
    bpy.ops.object.select_all(action='DESELECT')
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                s=area.spaces.active;s.shading.type='SOLID';s.shading.color_type='TEXTURE'
                s.region_3d.view_distance=42;s.region_3d.view_location=(0,0,4)
                s.region_3d.view_rotation=Euler((radians(36),0,radians(-12)),'XYZ').to_quaternion()
                s.region_3d.view_perspective='ORTHO';s.overlay.show_floor=False
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
    print(f'TREE PACK READY: {OUT}\nBLEND: {SOURCE}',flush=True)


if __name__=='__main__':
    main()
