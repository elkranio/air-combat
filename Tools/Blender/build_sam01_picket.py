#!/usr/bin/env python3
"""Dominion SAM-01 Picket, original visual-only low-poly game prop.
Run with Python to export Y-up OBJs, or Blender --background --factory-startup
--python Tools/Blender/build_sam01_picket.py for the editable .blend as well.
+Z forward, ground Y=0. Imperial Olive palette; twin open-rail visual trial (C2).
"""
from pathlib import Path
from math import sin, cos, pi, radians
from collections import Counter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Environment/Dominion/SAM01_Picket'
PALETTE = {'Hull':(98/255,107/255,66/255), 'Equipment':(75/255,80/255,59/255),
           'Metal':(52/255,59/255,50/255), 'Rubber':(35/255,37/255,34/255),
           'Glass':(40/255,53/255,58/255), 'Caps':(182/255,180/255,156/255),
           'Launcher':(160/255,161/255,124/255), 'Accent':(184/255,59/255,50/255)}
# New OBJ groups reuse the existing Metal and Caps palette in Unity.
PALETTE.update(LauncherDark=PALETTE['Metal'], MissileL=PALETTE['Caps'], MissileR=PALETTE['Caps'])
PARTS = []

def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def center(v): return tuple(sum(p[i] for p in v)/len(v) for i in range(3))

def solid(name, group, v, faces, transform=None):
    v = [tuple(p) for p in v]
    if transform: v = [transform(p) for p in v]
    c = center(v); tris=[]
    for face in faces:
        face=tuple(face); a,b,d=(v[i] for i in face[:3]); n=cross(sub(b,a),sub(d,a))
        assert dot(n,n)>1e-10, name
        if dot(n,sub(c,a))>0: face=face[::-1]
        for i in range(1,len(face)-1): tris.append((face[0],face[i],face[i+1]))
    edges=Counter((a,b) for t in tris for a,b in zip(t,t[1:]+t[:1]))
    assert all(n==1 and edges[b,a]==1 for (a,b),n in edges.items()), name
    for i,j,k in tris:
        a,b,d=v[i],v[j],v[k]; n=cross(sub(b,a),sub(d,a))
        assert dot(n,n)>1e-10 and dot(n,sub(c,a))<0, name
    PARTS.append((name,group,v,tris))

def box(name,g,x,y,z,w,h,d,transform=None):
    v=[(x+sx*w/2,y+sy*h/2,z+sz*d/2) for sz in (-1,1) for sy in (-1,1) for sx in (-1,1)]
    solid(name,g,v,[(0,1,3,2),(4,5,7,6),(0,2,6,4),(1,3,7,5),(0,1,5,4),(2,3,7,6)],transform)

def prism(name,g,x0,x1,profile):
    n=len(profile); v=[(x,y,z) for x in (x0,x1) for y,z in profile]
    solid(name,g,v,[tuple(range(n)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)])

def rings(name,g,sections,axis='x',sides=12):
    # Each section: coordinate along axis, center in perpendicular plane, radius.
    v=[]
    for along,a,b,r in sections:
        for i in range(sides):
            theta=2*pi*i/sides; p=a+r*cos(theta); q=b+r*sin(theta)
            v.append((along,p,q) if axis=='x' else (p,along,q))
    n=sides; last=(len(sections)-1)*n
    faces=[tuple(range(n)),tuple(range(last,last+n))]
    for j in range(len(sections)-1):
        for i in range(n):
            k=(i+1)%n; faces.append((j*n+i,j*n+k,(j+1)*n+k,(j+1)*n+i))
    solid(name,g,v,faces)

# Low frame; three axles and thick polygonal tires. Flat bottom at ground level.
box('Main frame','Metal',0,.93,0,2.28,.42,8.75)
for s in (-1,1):
    box(f'Frame rail {s}','Metal',s*.88,.79,0,.24,.36,8.70)
for j,z in enumerate((2.90,-1.56,-3.10)):
    rings(f'Axle {j}','Metal',[(-1.58,.74,z,.15),(1.58,.74,z,.15)])
    for s in (-1,1):
        x=s*1.48; tag=f'{j} {s}'
        rings('Tire '+tag,'Rubber',[(x-.34,.74,z,.62),(x-.23,.74,z,.74),(x+.23,.74,z,.74),(x+.34,.74,z,.62)])
        outside=x+s*.35
        rings('Wheel hub '+tag,'Metal',sorted([(outside-s*.08,.74,z,.38),(outside,.74,z,.34)]))
        rings('Hub cap '+tag,'Equipment',sorted([(outside,.74,z,.16),(outside+s*.07,.74,z,.16)]),sides=8)

# Thick blunt cab, with an angular roof and a short hood rather than an APC hull.
prism('Cab shell','Hull',-1.27,1.27,[(1.15,1.45),(1.15,3.46),(2.70,3.46),(3.22,2.90),(3.22,1.55)])
prism('Engine hood','Hull',-1.18,1.18,[(1.22,3.34),(1.22,4.41),(2.20,4.41),(2.32,3.95),(2.32,3.34)])
box('Front bumper','Metal',0,1.10,4.46,2.84,.33,.30)
box('Radiator graphic','Metal',0,1.72,4.425,1.62,.66,.055)
for z in (4.46,):
    for s in (-1,1):
        box(f'Headlamp {s}','Caps',s*1.08,1.77,z,.36,.29,.075)
# Windshield follows the slightly sloped cab front; thick center divider.
for s in (-1,1):
    prism(f'Windshield {s}','Glass',s*.59-.49,s*.59+.49,[(2.73,3.465),(3.12,3.045),(3.09,3.015),(2.70,3.435)])
    box(f'Side window {s}','Glass',s*1.285,2.66,2.35,.055,.60,1.12)
    box(f'Cab step {s}','Metal',s*1.40,1.02,2.10,.36,.16,.83)
    box(f'Mirror bracket {s}','Metal',s*1.43,2.50,3.15,.33,.07,.12)
    box(f'Mirror {s}','Equipment',s*1.62,2.56,3.15,.13,.32,.25)
# Raised rear platform, broad mudguards and equipment lockers.
box('Flatbed','Hull',0,1.46,-1.49,2.92,.37,5.75)
box('Platform edge','Metal',0,1.67,-1.49,2.97,.13,5.76)
for s in (-1,1):
    box(f'Rear mudguard {s}','Hull',s*1.53,1.57,-2.32,.63,.18,3.22)
    box(f'Front mudguard {s}','Hull',s*1.47,1.58,2.90,.66,.20,1.62)
    box(f'Field locker {s}','Equipment',s*1.10,1.98,.85,.57,.53,.87)
    box(f'Fuel box {s}','Equipment',s*1.14,1.05,.15,.66,.53,.98)
box('Tail bumper','Metal',0,.96,-4.39,2.82,.23,.25)
# Deployed supports, visible from above. Fictional simplified geometry, no working mechanism.
for j,z in enumerate((.73,-3.72)):
    for s in (-1,1):
        tag=f'{j} {s}'
        box('Outrigger housing '+tag,'Equipment',s*1.25,1.32,z,.64,.40,.53)
        box('Outrigger arm '+tag,'Metal',s*2.00,1.23,z,1.58,.34,.46)
        box('Outrigger upright '+tag,'Equipment',s*2.73,.69,z,.46,1.28,.48)
        box('Outrigger foot '+tag,'Metal',s*2.73,.10,z,.94,.20,.96)
module_start = len(PARTS)
# SAM-01C2: stylized open launcher artwork. Keep the fixed deck in the
# original groups; moving art has its own groups and each display rocket
# is a separate OBJ, so the later animation can hide them independently.
rings('Fixed turntable','Metal',[(1.70,0,-1.75,1.10),(1.96,0,-1.75,1.10)],axis='y')
box('Fixed deck collar','Equipment',0,2.05,-1.75,1.88,.24,1.88)
box('Rotating base','Launcher',0,2.24,-1.75,2.12,.34,1.92)
box('Raised central saddle','Launcher',0,2.74,-1.75,1.02,.78,1.22)
for side in (-1,1):
    box(f'Saddle side {side}','LauncherDark',side*.58,2.65,-1.75,.30,.64,.98)
    box(f'Faction plate {side}','Accent',side*1.075,2.30,-1.75,.04,.20,.70)

# Purely visual, exaggerated silhouettes for the fixed arcade camera.
angle=radians(35)
def tilt(p):
    x,y,z=p; return (x,3.50+y*cos(angle)+z*sin(angle),-1.75-y*sin(angle)+z*cos(angle))

for side in (-1,1):
    x=side*.78
    group='MissileL' if side<0 else 'MissileR'
    box(f'Open rail {side}','LauncherDark',x,-.47,-.20,.43,.24,3.25,tilt)
    box(f'Rail side panel {side}','Launcher',x,-.64,-.20,.60,.18,2.90,tilt)
    box(f'Rail marking {side}','Accent',x,-.65,.84,.62,.10,.18,tilt)
    # Smooth faceted display prop, pointed nose, broad readable tail silhouette.
    n=12
    sections=[(-1.84,.21),(-1.52,.30),(1.02,.30),(2.12,.025)]
    v=[(x+r*cos(2*pi*i/n),r*sin(2*pi*i/n),z) for z,r in sections for i in range(n)]
    faces=[tuple(range(n)),tuple(range((len(sections)-1)*n,len(sections)*n))]
    for j in range(len(sections)-1):
        faces.extend((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for i in range(n))
    solid(f'Display rocket {side}',group,v,faces,tilt)
    # Each fin is a solid graphic silhouette, merged into its display OBJ.
    for k in range(4):
        t=pi/4+k*pi/2
        outline=[(.20,-1.74),(.53,-1.74),(.50,-1.31),(.20,-.84)]
        v=[(x+r*cos(t)-w*sin(t),r*sin(t)+w*cos(t),z) for w in (-.045,.045) for r,z in outline]
        solid(f'Display fin {side} {k}',group,v,[(0,1,2,3),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],tilt)


# ART-SAM-01B: exaggerated, readable game silhouettes rather than scale realism.
# Enlarge the complete decorative module together, anchored at the deck surface.
MODULE_SCALE = 1.28
WORLD_SCALE = 1.5
for i,(name,group,v,t) in enumerate(PARTS):
    if i >= module_start:
        v = [(x*MODULE_SCALE, 1.70+(y-1.70)*MODULE_SCALE,
              -1.75+(z+1.75)*MODULE_SCALE) for x,y,z in v]
    v = [tuple(c*WORLD_SCALE for c in p) for p in v]
    PARTS[i] = (name,group,v,t)


def obj(group):
    lines=['# Dominion SAM-01 Picket; fictional visual prop; Y-up +Z forward',f'o SAM01_{group}','s off']; offset=0
    for name,g,v,t in PARTS:
        if g!=group: continue
        lines.extend('v '+' '.join(f'{x:.6f}' for x in p) for p in v)
        lines.extend('f '+' '.join(str(i+offset+1) for i in tri) for tri in t)
        offset+=len(v)
    return '\n'.join(lines)+'\n'

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    for group in PALETTE:
        (OUT/f'SAM01_{group}.obj').write_text(obj(group),encoding='utf-8')
    print(f'{len(PARTS)} editable pieces, {sum(len(t) for _,_,_,t in PARTS)} triangles')
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    materials={}; collections={}
    for group,rgb in PALETTE.items():
        mat=bpy.data.materials.new('SAM01_'+group); mat.diffuse_color=(*rgb,1); materials[group]=mat
        mat.use_nodes=True; shader=mat.node_tree.nodes.get('Principled BSDF'); shader.inputs['Base Color'].default_value=(*rgb,1); shader.inputs['Roughness'].default_value=.92
        col=bpy.data.collections.new(group); bpy.context.scene.collection.children.link(col); collections[group]=col
    for name,group,v,t in PARTS:
        mesh=bpy.data.meshes.new(name); mesh.from_pydata([(x,-z,y) for x,y,z in v],[],t)
        assert not mesh.validate(verbose=True),name
        mesh.update(); mesh.materials.append(materials[group]); ob=bpy.data.objects.new(name,mesh); collections[group].objects.link(ob)
        # Put local origin at the center for convenient individual edits.
        c=center(v); origin=(c[0],-c[2],c[1]); ob.location=origin
        for vert in mesh.vertices:
            for i in range(3): vert.co[i]-=origin[i]
        for p in mesh.polygons: p.use_smooth=False
    bpy.context.scene.unit_settings.system='METRIC'
    path=ROOT/'Resources/Blender/SAM01_Picket.blend'; path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))
    print(path)

if __name__=='__main__': main()
