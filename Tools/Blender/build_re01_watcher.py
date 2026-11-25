#!/usr/bin/env python3
"""Fictional arcade recon UAV. Y-up OBJ; optional editable Blender scene.
Python: python Tools/Blender/build_re01_watcher.py
Blender: blender -b --factory-startup --python Tools/Blender/build_re01_watcher.py
"""
from pathlib import Path
from math import sin, cos, pi, sqrt
from collections import Counter

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/_Project/Art/Aircraft/Dominion/RE01_Watcher'
PALETTE={'Hull':(76,94,100),'Upper':(102,122,125),'Equipment':(92,103,70),'Dark':(43,51,54),'Gold':(210,174,62)}
PARTS=[]

def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def center(v): return tuple(sum(p[i] for p in v)/len(v) for i in range(3))

def solid(name,group,v,faces):
    c=center(v); triangles=[]
    for face in faces:
        f=list(face); a,b,d=(v[i] for i in f[:3]); n=cross(sub(b,a),sub(d,a))
        if dot(n,sub(c,a))>0: f.reverse()
        for i in range(1,len(f)-1):
            tri=(f[0],f[i],f[i+1]); a,b,d=(v[j] for j in tri); n=cross(sub(b,a),sub(d,a))
            assert dot(n,n)>1e-10 and dot(n,sub(c,a))<0, (name,tri)
            triangles.append(tri)
    edges=Counter((a,b) for t in triangles for a,b in zip(t,t[1:]+t[:1]))
    assert all(n==1 and edges[b,a]==1 for (a,b),n in edges.items()),name
    PARTS.append((name,group,v,triangles))

def box(name,g,x,y,z,w,h,d):
    v=[(x+sx*w/2,y+sy*h/2,z+sz*d/2) for sz in (-1,1) for sy in (-1,1) for sx in (-1,1)]
    solid(name,g,v,[(0,1,3,2),(4,5,7,6),(0,2,6,4),(1,3,7,5),(0,1,5,4),(2,3,7,6)])

def hull(name,g,sections,sides=12,paint=None):
    # z, center x, center y, half width, half height. Forward = +Z.
    v=[]
    for z,x,y,w,h in sections:
        for k in range(sides):
            a=2*pi*k/sides; v.append((x+w*cos(a),y+h*sin(a),z))
    last=len(v)-sides; faces=[tuple(range(sides)),tuple(range(last,last+sides))]
    for j in range(len(sections)-1):
        for k in range(sides):
            l=(k+1)%sides; faces.append((j*sides+k,j*sides+l,(j+1)*sides+l,(j+1)*sides+k))
    solid(name,g,v,faces)
    if paint:
        # Preserve one closed original surface; partition faces for paint only.
        name,g,v,tris=PARTS.pop(); groups={}
        for t in tris:
            group=paint(center([v[k] for k in t]));groups.setdefault(group,[]).append(t)
        for group,t in groups.items():PARTS.append((name+' '+group,group,v,t))

def fin(name,g,points,thick=.7):
    # Flat polygon in x/z, with specified cambered center y.
    n=len(points); v=[(x,y+s*thick/2,z) for s in (-1,1) for x,y,z in points]
    solid(name,g,v,[tuple(range(n)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)])

def paint(p):
    return 'Upper' if p[1] > .55 else 'Hull'

# Fictional utility UAV: slender fuselage, swollen sensor nose, long wings.
hull('Fuselage','Hull',[(-8.0,0,.15,.62,.68),(-6.7,0,.12,.95,.88),(-3.0,0,0,1.05,1.0),(1.8,0,0,1.3,1.12),(4.9,0,.18,1.52,1.48),(7.2,0,.32,1.3,1.28),(8.5,0,.21,.82,.79),(9.0,0,.16,.24,.29)],12,paint)
# A dark optical turret under the nose, with a flattened front lens.
hull('Sensor turret','Equipment',[(4.9,0,-1.32,.45,.45),(5.45,0,-1.58,.82,.79),(6.35,0,-1.58,.82,.79),(6.9,0,-1.45,.45,.48)],10)
hull('Sensor lens','Dark',[(6.72,0,-1.48,.48,.41),(7.0,0,-1.48,.39,.34)],8)
# Distinct gold wing stripes are integral panels, not floating decals.
for s in (-1,1):
    def wing(name,g,a,b,lead_a,trail_a,lead_b,trail_b):
        fin(name+' '+str(s),g,[(s*a,.04,lead_a),(s*b,.04,lead_b),(s*b,.04,trail_b),(s*a,.04,trail_a)],.52)
    wing('Wing root','Hull',.65,2.5,2.1,-2.3,1.7,-1.9)
    wing('Main wing','Upper',2.5,9.8,1.7,-1.9,.85,-1.45)
    wing('Dominion stripe','Gold',9.8,10.85,.85,-1.45,.73,-1.38)
    wing('Wing tip','Upper',10.85,12.5,.73,-1.38,.48,-1.10)
    # Inverted V: slope downward from the rear fuselage, clear from above.
    fin('Tail '+str(s),'Equipment',[(s*.42,.13,-4.9),(s*3.7,-1.65,-6.8),(s*3.7,-1.65,-8.15),(s*.42,.13,-7.55)],.42)
    # Small dark panel on top of the inner wing, away from the stripe.
    box('Wing panel '+str(s),'Dark',s*3.2,.33,-.82,1.05,.12,.55)
# Rear pusher hub and exhausts. Rotor is an independently animated child.
hull('Pusher hub','Dark',[(-8.85,0,.15,.27,.27),(-8.35,0,.15,.48,.48),(-7.85,0,.15,.50,.50)],10)
for s in (-1,1):
    box('Exhaust '+str(s),'Dark',s*.85,.18,-5.7,.30,.43,.96)
box('Dorsal equipment','Equipment',0,1.18,1.0,.72,.46,2.5)

# Rotor centered on its own pivot, +Z axis. Closed low-poly blades.
for i in range(3):
    a=2*pi*i/3
    def rotor_point(r,t,z):
        return (r*cos(a)-t*sin(a),r*sin(a)+t*cos(a),z)
    v=[rotor_point(r,t,z) for z in (-.09,.09) for r,t in ((.18,-.13),(1.65,-.17),(1.91,.03),(1.68,.24),(.18,.20))]
    solid('Pusher blade '+str(i),'Rotor',v,[(0,1,2,3,4),(5,6,7,8,9)]+[(j,(j+1)%5,(j+1)%5+5,j+5) for j in range(5)])

def export():
    OUT.mkdir(parents=True,exist_ok=True)
    for group in (*PALETTE, 'Rotor'):
        lines=['# RE01 Watcher / visual-only mesh / Y-up +Z forward',f'o RE01_{group}','s off'];off=0
        for name,g,v,t in PARTS:
            if g!=group:continue
            lines += ['v '+' '.join(f'{x:.6f}' for x in p) for p in v]
            lines += ['f '+' '.join(str(i+1+off) for i in tri) for tri in t];off+=len(v)
        assert off>0,group
        (OUT/f'RE01_{group}.obj').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    print('RE01:',len(PARTS),'parts,',sum(len(t) for _,_,_,t in PARTS),'triangles')
    try: import bpy
    except ImportError:return
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    mats={}
    for group,rgb in PALETTE.items():
        m=bpy.data.materials.new('RE01_'+group);c=tuple(x/255 for x in rgb);m.diffuse_color=(*c,1);m.use_nodes=True
        p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*c,1);p.inputs['Roughness'].default_value=.92;mats[group]=m
    for name,g,v,t in PARTS:
        mesh=bpy.data.meshes.new(name);mesh.from_pydata([(x,-z,y) for x,y,z in v],[],t);mesh.update();mesh.materials.append(mats['Dark' if g=='Rotor' else g]);ob=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(ob)
        if g=='Rotor': ob.location=(0,8.85,.15)
        for p in mesh.polygons:p.use_smooth=False
    path=ROOT/'Resources/Blender/RE01_Watcher.blend';path.parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(path))

if __name__=='__main__':export()
