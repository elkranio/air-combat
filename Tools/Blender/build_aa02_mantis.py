"""AA-01a fictional Mantis: editable low-poly chassis, yaw and pitch groups.

Run from repository root: python Tools/Blender/build_aa02_mantis.py
or: blender -b --python Tools/Blender/build_aa02_mantis.py
Both write identical Unity-axis OBJ sections. Blender additionally saves the native
hierarchy to Resources/Blender/AA02_Mantis.blend. No third-party meshes.
"""
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/_Project/Art/Ground/AA02_Mantis'
YAW = (0, 2.65, -.25)
PITCH = (0, .83, 1.15)
# Geometry in Unity coordinates, +Z forward; local to the appropriate pivot.
COLORS = {'Armor':'#68745B', 'Deck':'#929A77', 'Track':'#343D3C',
          'Steel':'#4D5A57', 'Dark':'#263331', 'Accent':'#CE9850'}
PARTS = {}

def add(group, material, vertices, faces):
    v, f = PARTS.setdefault((group, material), ([], []))
    offset = len(v)
    v.extend(vertices)
    f.extend(tuple(i+offset for i in face) for face in faces)

def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def convex(group, material, vertices, faces):
    centre = tuple(sum(v[i] for v in vertices)/len(vertices) for i in range(3))
    fixed=[]
    for face in faces:
        a,b,c = [vertices[i] for i in face[:3]]
        normal=cross(sub(b,a),sub(c,a))
        if dot(normal,sub(a,centre))<0: face=tuple(reversed(face))
        fixed.append(face)
    add(group,material,vertices,fixed)

def loft(group,material,rings):
    n=len(rings[0]); k=len(rings)
    faces=[tuple(range(n)),tuple(range((k-1)*n,k*n))]
    for j in range(k-1):
        for i in range(n): faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
    convex(group,material,[v for ring in rings for v in ring],faces)

def octagon(w,d,c):
    x=w/2; z=d/2; c=min(c,x*.9,z*.9)
    return [(-x+c,-z),(x-c,-z),(x,-z+c),(x,z-c),(x-c,z),(-x+c,z),(-x,z-c),(-x,-z+c)]

def hull(group,mat,x,z,levels):
    loft(group,mat,[[(x+a,y,z+b) for a,b in octagon(w,d,c)] for y,w,d,c in levels])

def box(group,mat,x,y,z,w,h,d,b=.10):
    b=min(b,w*.22,h*.22,d*.22)
    hull(group,mat,x,z,[(y-h/2,w-2*b,d-2*b,b),(y-h/2+b,w,d,b),(y+h/2-b,w,d,b),(y+h/2,w-2*b,d-2*b,b)])

def cylinder(group,mat,axis,center,levels,n=12):
    # levels = axial offset and radius; supports stepped collars with bevels.
    rings=[]
    for offset,radius in levels:
        ring=[]
        for j in range(n):
            a=2*math.pi*j/n; u=math.cos(a)*radius; v=math.sin(a)*radius
            p=(offset,u,v) if axis=='x' else (u,offset,v) if axis=='y' else (u,v,offset)
            ring.append(tuple(p[i]+center[i] for i in range(3)))
        rings.append(ring)
    # Flat/stepped profile: orient end caps and side faces via ring winding,
    # not a convex centroid test (some radial steps are concave).
    vertices=[p for ring in rings for p in ring]
    faces=[tuple(reversed(range(n))),tuple(range((len(rings)-1)*n,len(rings)*n))]
    for k in range(len(rings)-1):
        for j in range(n): faces.append((k*n+j,k*n+(j+1)%n,(k+1)*n+(j+1)%n,(k+1)*n+j))
    # Ring normal is +axis for x,z and -axis for y.
    if axis=='y': faces=[tuple(reversed(f)) for f in faces]
    add(group,mat,vertices,faces)

# Broad, faceted tracked hull, sloping glacis, raised rear engine deck.
hull('Body','Armor',0,0,[(.60,4.7,8.8,.55),(1.15,5.65,10.0,.70),(2.15,5.9,10.25,.78),(2.65,4.75,8.6,.85)])
hull('Body','Deck',0,-.4,[(2.44,5.05,8.0,.5),(2.68,4.92,7.8,.6),(2.79,4.35,7.1,.6)])
# Track volumes are closed sculpted prisms with chamfered running profiles.
profile=[(.18,-3.6),(.18,3.55),(.58,4.78),(1.39,5.0),(1.94,4.25),(1.94,-4.2),(1.50,-4.98),(.63,-4.8)]
for s in (-1,1):
    x=s*2.92
    rings=[[(x+dx,y,z) for y,z in profile] for dx in (-.78,.78)]
    loft('Body','Track',rings)
    # Five bold inset wheel faces and a protective stepped upper skirt.
    for z in (-3.7,-1.85,0,1.85,3.7):
        cylinder('Body','Steel','x',(x+s*.73,.94,z),[(-.10,.58),(.10,.72),(.26,.64)],10)
        cylinder('Body','Dark','x',(x+s*.85,.94,z),[(-.10,.22),(.22,.22)],10)
    box('Body','Armor',x,2.04,-.18,1.65,.62,8.45,.24)
    for z in (-3.05,0,3.05): box('Body','Deck',x,2.12,z,1.72,.18,1.34,.07)
    box('Body','Accent',s*2.1,2.43,4.05,.62,.12,.42,.035)
    box('Body','Dark',s*2.1,2.43,-3.95,.62,.12,.42,.035)
# Rear deck vents and front hatch, deliberately low-frequency detail.
for z in (-3.6,-3.23,-2.86): box('Body','Dark',0,2.82,z,2.65,.10,.19,.035)
box('Body','Armor',-.8,2.84,2.88,1.25,.22,1.30,.16)
box('Body','Dark',-.8,2.99,3.2,.83,.12,.20,.035)
# Turret local coordinates: angular, wide shoulder pods, faceted top.
cylinder('Yaw','Steel','y',(0,0,0),[(0,1.65),(.20,1.85),(.44,1.60)],16)
hull('Yaw','Armor',0,-.12,[(.24,3.5,3.4,.5),(.54,4.35,4.0,.65),(1.42,4.4,3.65,.6),(1.92,3.4,2.8,.55)])
hull('Yaw','Deck',0,-.33,[(1.83,3.25,2.65,.43),(2.00,2.95,2.45,.42)])
for s in (-1,1):
    box('Yaw','Steel',s*1.9,.88,.85,.84,1.34,2.05,.2)
    box('Yaw','Accent',s*1.9,1.59,.45,.53,.07,.72,.025)
    box('Yaw','Dark',s*1.5,1.24,1.56,.62,.53,.19,.06)
# Small rear sensor panel, broad recognizable slab (not a real radar replica).
box('Yaw','Steel',0,2.18,-1.15,.42,.55,.46,.08)
box('Yaw','Dark',0,2.60,-1.15,1.82,.70,.34,.13)
box('Yaw','Deck',0,2.62,-.95,1.55,.52,.12,.07)
# Twin fictional barrel silhouettes, attached to one elevation cradle.
box('Pitch','Steel',0,0,.35,2.7,.86,1.50,.17)
for s in (-1,1):
    x=s*.98
    cylinder('Pitch','Dark','z',(x,0,0),[(-.32,.49),(.08,.56),(.80,.52),(1.1,.38)],12)
    cylinder('Pitch','Steel','z',(x,0,0),[(.74,.35),(1.08,.35),(1.28,.25),(4.53,.22),(4.65,.29),(4.96,.29),(5.10,.23)],12)
    cylinder('Pitch','Dark','z',(x,0,0),[(2.04,.29),(2.13,.32),(2.45,.32),(2.54,.27)],12)
    cylinder('Pitch','Dark','z',(x,0,0),[(5.10,.20),(5.12,.20)],12)


def export_obj():
    ART.mkdir(parents=True,exist_ok=True)
    for (group,mat),(vertices,faces) in sorted(PARTS.items()):
        name=f'{group}_{mat}'
        with (ART/(name+'.obj')).open('w',newline='\n') as out:
            out.write(f'# Mantis / {group} / {mat}\no {name}\ns off\n')
            # OBJ is right-handed, Unity flips X on import. Geometry is mirrored
            # here to preserve source coordinates and positive forward Z in Unity.
            for x,y,z in vertices: out.write(f'v {-x:.5f} {y:.5f} {z:.5f}\n')
            for face in faces: out.write('f '+' '.join(str(i+1) for i in reversed(face))+'\n')


def export_blend():
    try: import bpy
    except ImportError: return False
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    def pos(p): return (p[0],-p[2],p[1])
    def empty(name,parent=None,location=(0,0,0)):
        obj=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(obj)
        obj.parent=parent; obj.location=pos(location); return obj
    root=empty('AA-02 Mantis'); body=empty('Body',root)
    yaw=empty('TurretYaw',root,YAW); pitch=empty('GunPitch',yaw,PITCH)
    groups={'Body':body,'Yaw':yaw,'Pitch':pitch}
    def linear(c): return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4
    mats={}
    for name,hexcolor in COLORS.items():
        rgb=tuple(linear(int(hexcolor[i:i+2],16)/255) for i in (1,3,5))
        m=bpy.data.materials.new(name); m.diffuse_color=(*rgb,1); m.use_nodes=True
        bsdf=m.node_tree.nodes.get('Principled BSDF'); bsdf.inputs['Base Color'].default_value=(*rgb,1); bsdf.inputs['Roughness'].default_value=.86
        mats[name]=m
    for (group,mat),(vertices,faces) in PARTS.items():
        name=f'{group}_{mat}'; mesh=bpy.data.meshes.new(name); mesh.from_pydata([pos(v) for v in vertices],[],faces); mesh.update()
        obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj); obj.parent=groups[group]; mesh.materials.append(mats[mat])
    empty('Muzzle_L',pitch,(-.98,0,5.12)); empty('Muzzle_R',pitch,(.98,0,5.12))
    path=ROOT/'Resources/Blender/AA02_Mantis.blend'; path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path)); return True

if __name__=='__main__':
    export_obj()
    print('OBJ sections:',len(PARTS),'native Blender saved:',export_blend())
