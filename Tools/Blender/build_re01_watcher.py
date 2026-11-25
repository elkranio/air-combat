#!/usr/bin/env python3
"""Fictional arcade recon airship. Y-up OBJ; optional editable Blender scene.
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
    x,y,z=p
    if -10.5<z<-7 and y>0: return 'Gold'
    if y>1.2:return 'Upper'
    return 'Hull'

# Broad lifting body: flat silhouette, a tapered rear, dark belly, one yellow band.
hull('Lifting envelope','Hull',[(-17,0,.4,1.9,1.15),(-14,0,.3,5.8,2.6),(-10,0,.1,9.7,4.0),(-7,0,0,11.4,4.7),(0,0,0,12.2,5.2),(7,0,0,10.9,4.9),(13,0,.1,7.6,3.8),(17,0,.25,2.3,1.65)],16,paint)
# Dark lower gondola, angular shoulder engines, capped duct faces.
hull('Belly gondola','Equipment',[(-10,0,-4.0,1.8,.9),(-7,0,-4.6,3.8,1.5),(5,0,-4.6,3.6,1.5),(9,0,-3.9,1.5,.8)],8)
box('Sensor window','Dark',0,-4.5,8.9,2.4,.85,.25)
for s in (-1,1):
    x=s*10.2
    hull('Engine shoulder '+str(s),'Equipment',[(-10,x,-1.5,1.4,1.2),(-8,x,-1.5,2.35,1.75),(-2,x,-1.5,2.3,1.7),(1,x,-1.5,1.2,.95)],8)
    hull('Dark exhaust '+str(s),'Dark',[(-10.5,x,-1.5,1.4,1.15),(-9.8,x,-1.5,1.45,1.2)],8)
    # Tail fins share the military olive; broad angular silhouette, not a Zeppelin copy.
    fin('Tailplane '+str(s),'Equipment',[(s*3,.4,-11),(s*8,.9,-15.0),(s*8,.9,-18),(s*2.5,.4,-16.7)])
    fin('Tail tip '+str(s),'Gold',[(s*7.3,.9,-15),(s*8.2,.9,-15.7),(s*8.2,.9,-18.1),(s*7.3,.9,-17.9)],.78)
# Central raised tail spine, visible upper recon sensor panel.
fin('Vertical tail','Equipment',[(-.75,2.7,-11),(.75,2.7,-11),(.8,4.0,-16.7),(-.8,4.0,-16.7)],2.0)
box('Upper technical panel','Dark',0,5.03,1.0,3.7,.38,6.8)
box('Panel cap','Equipment',0,5.28,2.7,2.8,.3,2.9)
# Simple top markings, high contrast, no text texture. Twin gold ID bars.
box('ID bar L','Gold',-2.7,4.9,3.5,.7,.24,2.0)
box('ID bar R','Gold',2.7,4.9,3.5,.7,.24,2.0)

def export():
    OUT.mkdir(parents=True,exist_ok=True)
    for group in PALETTE:
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
        mesh=bpy.data.meshes.new(name);mesh.from_pydata([(x,-z,y) for x,y,z in v],[],t);mesh.update();mesh.materials.append(mats[g]);ob=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(ob)
        for p in mesh.polygons:p.use_smooth=False
    path=ROOT/'Resources/Blender/RE01_Watcher.blend';path.parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(path))

if __name__=='__main__':export()
