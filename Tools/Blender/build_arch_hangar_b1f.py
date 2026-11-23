#!/usr/bin/env python3
"""B1f — original faceted-arch aircraft hangar, designed for Blender + Unity.

Run from repo root to regenerate committed .obj sections:
    python3 Tools/Blender/build_arch_hangar_b1f.py

Run *inside Blender* to create an editable .blend scene, using precisely
these same meshes (also regenerates .obj for Unity):
    blender --background --python Tools/Blender/build_arch_hangar_b1f.py

No third-party reference mesh or textures. All units metres, Y up. -X
is the hangar entrance. The generated OBJ sections are model assets, not
Unity primitives. The .blend contains separate named editable objects.
"""

from pathlib import Path
import math

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Buildings/Base/ArchHangar_B1f'
BLEND = ROOT / 'Resources/Blender/ArchHangar_B1f.blend'

PALETTE = {
    'Roof': (0.315, 0.430, 0.462),       # muted slate teal
    'Structure': (0.620, 0.636, 0.587),  # warm/neutral military concrete
    'Dark': (0.151, 0.229, 0.265),       # interior and glazed panels
    'Metal': (0.218, 0.332, 0.370),      # deliberately bold structural ribs
    'Accent': (0.753, 0.480, 0.303),     # muted burnt-orange detail
}


def vector(p, q):
    return (q[0] - p[0], q[1] - p[1], q[2] - p[2])


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0])


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


class Mesh:
    def __init__(self, name, material):
        self.name, self.material = name, material
        self.polygons = []

    def face(self, points, direction):
        """Add a flat-shaded polygon, automatically facing outward."""
        assert len(points) in (3, 4)
        pts = [tuple(map(float, p)) for p in points]
        if dot(cross(vector(pts[0], pts[1]), vector(pts[0], pts[2])), direction) < 0:
            pts.reverse()
        if len(pts) == 4:
            self.polygons.append((pts[0], pts[1], pts[2]))
            self.polygons.append((pts[0], pts[2], pts[3]))
        else:
            self.polygons.append(tuple(pts))

    def box(self, x0, y0, z0, x1, y1, z1):
        assert x0 < x1 and y0 < y1 and z0 < z1
        a=(x0,y0,z0); b=(x1,y0,z0); c=(x1,y0,z1); d=(x0,y0,z1)
        e=(x0,y1,z0); f=(x1,y1,z0); g=(x1,y1,z1); h=(x0,y1,z1)
        for quad, normal in (([a,b,c,d],(0,-1,0)),([e,f,g,h],(0,1,0)),
                             ([a,d,h,e],(-1,0,0)),([b,f,g,c],(1,0,0)),
                             ([a,e,f,b],(0,0,-1)),([d,c,g,h],(0,0,1))):
            self.face(quad,normal)

    def write_obj(self):
        OUT.mkdir(parents=True, exist_ok=True)
        p=OUT/(self.name+'.obj')
        text=[f'# ArchHangar B1f / {self.name} / Y-up metres / -X entrance',
              f'o {self.name}', 's off']
        i=1
        for tri in self.polygons:
            for x,y,z in tri:
                text.append(f'v {x:.5f} {y:.5f} {z:.5f}')
            text.append(f'f {i} {i+1} {i+2}')
            i+=3
        p.write_text('\n'.join(text)+'\n', encoding='utf-8')
        print(f'{p.relative_to(ROOT)} — {len(self.polygons)} faces')
        return p


# The arch is drawn across Z. Eight deliberate facets, NOT a semicircle.
# Outboard walls stay relatively low; the roof is tall and readable at Y78.
ARCH = [(-16.4,4.15),(-15.15,6.65),(-11.2,9.45),(-5.55,11.72),
        (0,12.43),(5.55,11.72),(11.2,9.45),(15.15,6.65),(16.4,4.15)]
X0, X1 = -14.3, 14.4


def arch_surface(mesh, x_start, x_end, arc, outward_up=True):
    for (z0,y0),(z1,y1) in zip(arc,arc[1:]):
        n=(0,1,-(y1-y0)/(z1-z0)) if outward_up else (0,-1,(y1-y0)/(z1-z0))
        mesh.face([(x_start,y0,z0),(x_end,y0,z0),
                   (x_end,y1,z1),(x_start,y1,z1)],n)


def arch_ring(mesh, x_start, x_end, external, internal):
    """A true hollow faceted portal, with exposed front/back/thickness."""
    for (z0,y0),(z1,y1),(zi0,yi0),(zi1,yi1) in zip(external,external[1:],internal,internal[1:]):
        # -X front face, +X back face, top skin and soffit.
        mesh.face([(x_start,y0,z0),(x_start,y1,z1),
                   (x_start,yi1,zi1),(x_start,yi0,zi0)],(-1,0,0))
        mesh.face([(x_end,y0,z0),(x_end,yi0,zi0),
                   (x_end,yi1,zi1),(x_end,y1,z1)],(1,0,0))
        normal=(0,1,-(y1-y0)/(z1-z0))
        mesh.face([(x_start,y0,z0),(x_end,y0,z0),
                   (x_end,y1,z1),(x_start,y1,z1)],normal)
        mesh.face([(x_start,yi0,zi0),(x_start,yi1,zi1),
                   (x_end,yi1,zi1),(x_end,yi0,zi0)],(0,-1,(yi1-yi0)/(zi1-zi0)))
    for side in (0,-1):
        z,y=external[side]; zi,yi=internal[side]
        n=(0,0,-1 if side==0 else 1)
        mesh.face([(x_start,y,z),(x_start,yi,zi),
                   (x_end,yi,zi),(x_end,y,z)],n)


# Main shell: large roof facets, genuine roof thickness and dark underside.
roof=Mesh('Arch_Roof', 'Roof')
arch_surface(roof,X0,X1,ARCH)
INNER=[(z,y-.53) for z,y in ARCH]
arch_surface(roof,X0,X1,INNER,False)
for x,normal in ((X0,(-1,0,0)),(X1,(1,0,0))):
    for (z0,y0),(z1,y1),(zi0,yi0),(zi1,yi1) in zip(ARCH,ARCH[1:],INNER,INNER[1:]):
        roof.face([(x,y0,z0),(x,y1,z1),(x,yi1,zi1),(x,yi0,zi0)],normal)
for side in (0,-1):
    z,y=ARCH[side]; zi,yi=INNER[side]
    roof.face([(X0,y,z),(X1,y,z),(X1,yi,zi),(X0,yi,zi)],(0,0,-1 if side==0 else 1))

# Concrete wall mass and deep end-wall, not a fake black backplane.
walls=Mesh('Arch_Walls', 'Structure')
for side in (-1,1):
    a,b=(15.1,16.1) if side==1 else (-16.1,-15.1)
    walls.box(-13.75,0,a,13.95,4.42,b)
# Rear wall shaped to the roof; use a fan whose centre remains inside profile.
rear_poly=[(-15.1,0),(15.1,0)] + [(z,y-.72) for z,y in ARCH[::-1]]
centroid_y=3.1
for x,normal in ((13.8,(-1,0,0)),(14.25,(1,0,0))):
    for i in range(len(rear_poly)):
        z0,y0=rear_poly[i]; z1,y1=rear_poly[(i+1)%len(rear_poly)]
        walls.face([(x,centroid_y,0),(x,y0,z0),(x,y1,z1)],normal)
# Visible foundation line and a few stout side buttresses.
for z in (-16.15,15.05):
    walls.box(-14.1,-.13,z,14.1,.57,z+1.1)
for xx in (-11.7,7.3):
    for zz in (-16.85,16.1):
        walls.box(xx-.75,0,zz,xx+.75,5.6,zz+.75)

# Portal silhouette: a solid raised rim plus two tall piers and big apron-facing lintel.
portal=Mesh('Arch_FrontPortal', 'Structure')
OUTER=[(z*1.016,y+.30) for z,y in ARCH]
INTER=[(z*.915,y-1.06) for z,y in ARCH]
arch_ring(portal,-15.45,-14.25,OUTER,INTER)
portal.box(-15.7,-.15,-17.0,-13.95,6.35,-14.75)
portal.box(-15.7,-.15,14.75,-13.95,6.35,17.0)
# Reinforced square feet at the entrance, keep chunky not cartoonish.
portal.box(-16.15,-.18,-17.7,-13.7,.62,-14.25)
portal.box(-16.15,-.18,14.25,-13.7,.62,17.7)

# Two broad roof cross-ribs / architectural rhythm, not busy corrugation.
ribs=Mesh('Arch_Ribs', 'Metal')
for x in (-5.8,8.05):
    section=[(z,y+.11) for z,y in ARCH]
    inner=[(z,y+.52) for z,y in ARCH]
    arch_ring(ribs,x-.37,x+.37,inner,section)
# A heavy horizontal brow over the portal on the entry side.
ribs.box(-15.95,4.95,-17.0,-15.45,5.62,-13.25)
ribs.box(-15.95,4.95,13.25,-15.45,5.62,17.0)

# Asymmetric side service block. Its roof is NOT a miniature gable house.
annex=Mesh('Arch_SideAnnex','Structure')
annex.box(1.5,.05,15.90,13.9,5.75,23.5)
# Lean-to roof slope along +Z, with thick overhang; slight slab angle adds variety.
annex.face([(0.85,6.5,15.5),(14.35,6.5,15.5),
            (14.35,5.75,24.25),(0.85,5.75,24.25)],(0,1,0.08))
annex.face([(0.85,6.15,15.5),(0.85,6.5,15.5),
            (0.85,5.75,24.25),(0.85,5.4,24.25)],(-1,0,0))
annex.face([(14.35,6.15,15.5),(14.35,5.4,24.25),
            (14.35,5.75,24.25),(14.35,6.5,15.5)],(1,0,0))
annex.box(1.0,0,23.5,14.3,.45,24.0)
# Deep-but-simple functional service door / horizontal glass opening.
openings=Mesh('Arch_DarkDetails','Dark')
openings.box(4.1,.42,23.52,8.65,4.8,23.68)
openings.box(10.05,3.10,23.52,12.55,4.48,23.68)
# Deep-set floor and back-darkness are real geometry, unlit enough to read as depth.
openings.box(-13.65,.01,-14.9,13.65,.16,14.9)
# Emphasize the shadowed rear wall *inside* the hangar, not a portal-covering flat door.
openings.box(13.28,1.15,-11.0,13.42,6.5,11.0)

vents=Mesh('Arch_RoofVents','Metal')
# Coarse two-box industrial ventilation. Roof at z±6 is around Y11.5.
for z in (-7.95,5.15):
    vents.box(2.0,11.25,z,6.6,12.95,z+2.8)
    vents.box(1.65,12.88,z-.30,6.95,13.28,z+3.1)

acc=Mesh('Arch_Accent','Accent')
# Restrained amber identifier strip visible on the entry arch and service door.
acc.box(-15.89,10.55,-6.0,-15.61,10.9,6.0)
acc.box(4.08,4.83,23.69,8.66,5.16,23.82)

PARTS=[roof,walls,portal,ribs,annex,openings,vents,acc]


def save_blender():
    import bpy  # use bpy only INSIDE Blender. Regular Python still exports all OBJs.
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for mat_name,rgb in PALETTE.items():
        m=bpy.data.materials.new('B1f_' + mat_name)
        m.diffuse_color=(*rgb,1.0)
        m.use_nodes=True
        shader=m.node_tree.nodes.get('Principled BSDF')
        if shader:
            shader.inputs['Base Color'].default_value=(*rgb,1.0)
            shader.inputs['Metallic'].default_value=0.0
            shader.inputs['Roughness'].default_value=0.95
    for part in PARTS:
        verts=[]; faces=[]
        for poly in part.polygons:
            idx=len(verts)
            verts.extend((x,-z,y) for x,y,z in poly)
            faces.append((idx,idx+1,idx+2))
        mesh=bpy.data.meshes.new(part.name)
        mesh.from_pydata(verts, [], faces)
        mesh.update()
        mesh.materials.append(bpy.data.materials['B1f_'+part.material])
        obj=bpy.data.objects.new(part.name,mesh)
        bpy.context.collection.objects.link(obj)
        for f in mesh.polygons:
            f.use_smooth=False
    # Native Blender uses Z up; (x,-z,y) is a +90-degree X rotation of
    # the Y-up Unity/OBJ coordinates. The .blend is upright and editable.
    # Exporting from Blender later must respect the inverse axis mapping.
    bpy.context.scene.unit_settings.system='METRIC'
    bpy.context.scene.unit_settings.scale_length=1.0
    BLEND.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    print('Saved editable Blender scene:', BLEND)


if __name__=='__main__':
    for part in PARTS:
        assert part.polygons, part.name
        part.write_obj()
    try:
        import bpy  # noqa:F401
    except ImportError:
        print('Blender not installed here; .obj geometry ready. Run this script in Blender to save .blend.')
    else:
        save_blender()
