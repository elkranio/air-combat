"""Original arcade Mantis rocket carrier. Rebuild OBJ sections with Python;
run with blender -b --python Tools/Blender/build_aa02_mantis_rocket.py
for the editable Blender scene as well. Coordinates are game-art proportions.
"""
import math
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/_Project/Art/Ground/AA02_Mantis_Rocket'
SOCKET = (0, 5.70, -0.85)
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

# Closed beveled prisms, sculpted cab and running gear. +Z is the nose.
G = 'Body'
hull(G,'Armor',0,0,[(.48,4.65,8.8,.65),(1.03,5.65,10.0,.72),(2.06,5.92,10.25,.68),(2.52,5.03,9.15,.67)])
hull(G,'Deck',0,-.65,[(2.36,5.06,7.75,.42),(2.68,4.95,7.5,.45)])
# Continuous tracks with broad sidewalls and five prominent wheel hubs per side.
profile=[(.12,-3.70),(.12,3.60),(.56,4.78),(1.36,4.94),(1.87,4.26),(1.87,-4.2),(1.43,-4.94),(.58,-4.75)]
for s in (-1,1):
    x=s*2.88
    loft(G,'Track',[[(x+dx,y,z) for y,z in profile] for dx in (-.78,.78)])
    for z in (-3.55,-1.77,0,1.77,3.55):
        cylinder(G,'Steel','x',(x+s*.72,.92,z),[(-.10,.60),(.10,.71),(.23,.62)],12)
        cylinder(G,'Dark','x',(x+s*.85,.92,z),[(-.11,.28),(.20,.28)],10)
        cylinder(G,'Deck','x',(x+s*.98,.92,z),[(-.07,.16),(.10,.16)],8)
    box(G,'Armor',x,1.99,-.15,1.65,.55,8.7,.19)
    for z in (-3.25,-.1,3.05):
        box(G,'Deck',x,2.28,z,1.50,.13,1.6,.05)
    # Bold segmented tread pads, not a noisy full link simulation.
    for i in range(12):
        z=-3.55+i*.65
        box(G,'Steel',x,.15,z,1.55,.13,.41,.035)
        box(G,'Steel',x,1.84,z,1.55,.12,.41,.035)
    box(G,'Dark',s*2.17,2.44,4.43,.79,.38,.24,.06)
    box(G,'Accent',s*2.17,2.46,4.57,.49,.20,.10,.035)
    box(G,'Dark',s*2.15,1.93,-4.96,.65,.30,.13,.04)
    box(G,'Accent',s*2.15,1.93,-5.04,.42,.15,.05,.02)
# Low wedge front cab. Offset upper rings rearward to create broad sloping windshield.
levels=[(2.44,4.80,3.83,.40,2.64),(2.89,4.83,3.74,.43,2.62),
        (3.65,4.20,2.81,.39,2.16),(4.02,3.87,2.40,.35,2.02)]
loft(G,'Armor',[[(x,y,z+cz) for x,z in octagon(w,d,c)] for y,w,d,c,cz in levels])
box(G,'Deck',0,4.07,1.98,3.75,.17,2.14,.14)
# Windshield faces follow the front slope; dark inset panels with a center mullion.
for s in (-1,1):
    x=s*1.03
    v=[(x-.80,2.98,4.41),(x+.80,2.98,4.41),(x+.72,3.70,3.65),(x-.72,3.70,3.65),
       (x-.80,2.98,4.45),(x+.80,2.98,4.45),(x+.72,3.70,3.69),(x-.72,3.70,3.69)]
    convex(G,'Dark',v,[(0,1,2,3),(4,5,6,7),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)])
    box(G,'Steel',s*2.34,3.28,2.44,.14,.49,1.05,.06)
    box(G,'Dark',s*2.42,3.28,2.44,.07,.34,.81,.025)
    box(G,'Deck',s*1.46,4.18,2.14,.81,.15,.94,.07)
# A small off-center sensor housing rather than a large radar dish.
cylinder(G,'Steel','y',(-1.00,4.12,1.32),[(0,.37),(.23,.45),(.38,.32)],10)
box(G,'Armor',-1,4.68,1.32,1.04,.74,.92,.14)
box(G,'Dark',-1,4.68,1.81,.73,.48,.15,.07)
cylinder(G,'Steel','z',(-1,4.68,1.84),[(0,.24),(.09,.26),(.16,.22)],10)
cylinder(G,'Dark','z',(-1,4.68,2.00),[(0,.15),(.035,.15)],10)
# Equipment trays, side armor and rear ventilation.
for s in (-1,1):
    box(G,'Armor',s*2.28,2.94,-.82,.78,.69,3.20,.11)
    box(G,'Steel',s*2.69,2.98,-.74,.10,.42,1.43,.05)
    for z in (-1.22,-.90,-.58,-.26): box(G,'Dark',s*2.76,2.98,z,.045,.30,.12,.014)
    box(G,'Deck',s*2.28,3.34,-2.38,.75,.11,.84,.04)
    box(G,'Accent',s*2.30,3.41,-2.38,.49,.035,.20,.012)
for y in (1.60,1.87,2.14): box(G,'Dark',0,y,-5.02,2.40,.12,.13,.035)
# Launcher is recognizable from overhead: six dark open, faceted cells in a compact frame.
# Cosmetic cells, not a simulation of a real weapon. Fixed vertical stance.
L='Launcher'
box(L,'Steel',0,2.91,-1.80,4.62,.55,3.72,.22)
box(L,'Armor',0,3.25,-1.80,4.65,.34,3.82,.13)

def tube(x,z):
    # Closed annular wall with separately colored recessed inner faces.
    outer=octagon(1.39,1.67,.19); inner=octagon(1.02,1.29,.14)
    rings=[[(x+a,y,z+b) for a,b in ring] for y,ring in
           [(3.35,outer),(5.51,outer),(5.70,octagon(1.43,1.71,.21)),(5.70,inner),(3.57,inner)]]
    n=8; verts=[v for r in rings for v in r]
    # octagon is CCW in XZ, whose normal is -Y. Outer sides point out.
    for i in range(n):
        j=(i+1)%n
        for k,mat in [(0,'Armor'),(1,'Deck'),(2,'Deck'),(3,'Dark')]:
            a,b,c,d=k*n+i,k*n+j,(k+1)*n+j,(k+1)*n+i
            # outer ring runs up; top runs inward; inner wall runs down.
            add(L,mat,[verts[q] for q in (a,d,c,b)],[(0,1,2,3)])
    # Recessed opaque base and lower outer cap close the visible hollow shape.
    add(L,'Dark',rings[-1],[tuple(reversed(range(n)))])
    add(L,'Armor',rings[0],[tuple(range(n))])
for z in (-.85,-2.73):
    for x in (-1.49,0,1.49): tube(x,z)
# A stout frame, bright edge trims and corner blocks reinforce launcher silhouette.
for s in (-1,1):
    box(L,'Steel',s*2.28,4.32,-1.79,.24,1.55,3.70,.08)
    box(L,'Deck',s*2.31,4.89,-1.79,.25,.16,3.44,.045)
    box(L,'Accent',s*2.33,4.88,-1.20,.11,.16,.49,.025)
for z in (-3.61,.06):
    box(L,'Steel',0,3.79,z,4.55,.42,.22,.06)

def export_obj():
    ART.mkdir(parents=True,exist_ok=True)
    for (group,mat),(vertices,faces) in sorted(PARTS.items()):
        name=f'{group}_{mat}'
        with (ART/(name+'.obj')).open('w',newline='\n') as out:
            out.write(f'# Mantis rocket carrier / {group} / {mat}\no {name}\ns off\n')
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
    root=empty('AA-02 Mantis rocket carrier'); body=empty('Body',root)
    launcher=empty('Launcher',root)
    groups={'Body':body,'Launcher':launcher}
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
    empty('LaunchSocket',launcher,SOCKET)
    path=ROOT/'Resources/Blender/AA02_Mantis_Rocket.blend'; path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path)); return True

if __name__=='__main__':
    export_obj()
    print('OBJ sections:',len(PARTS),'native Blender saved:',export_blend())
