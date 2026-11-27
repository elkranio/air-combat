#!/usr/bin/env python3
"""PG05: fictional, exaggerated low-poly scenery; Unity +Y up.
Python generates JSON; Blender additionally saves an editable visual source.
Not a functional electrical or structural model.
"""
import json
from collections import Counter
from math import cos, sin, pi, sqrt
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Editor/PG05Geometry.json'
PALETTE = {'Concrete': (137,140,130), 'Steel': (99,116,119),
           'Dark': (62,78,81), 'Ceramic': (190,197,184), 'Cable': (74,85,86)}
PARTS = []
SPAN = 58.0

def add(a,b): return tuple(x+y for x,y in zip(a,b))
def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def mul(a,k): return tuple(x*k for x in a)
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def unit(a): return mul(a,1/sqrt(dot(a,a)))
def center(v): return tuple(sum(p[i] for p in v)/len(v) for i in range(3))

def solid(name,group,mat,v,faces):
    v=[tuple(round(x,6) for x in p) for p in v]
    c=center(v); tris=[]
    for face in faces:
        f=list(face); a,b,d=(v[i] for i in f[:3]); n=cross(sub(b,a),sub(d,a))
        assert dot(n,n)>1e-10,name
        if dot(n,sub(c,a))>0: f.reverse()
        tris.extend((f[0],f[i],f[i+1]) for i in range(1,len(f)-1))
    validate(name,v,tris)
    PARTS.append((name,group,mat,v,tris))

def validate(name,v,tris):
    edges=Counter((a,b) for t in tris for a,b in zip(t,t[1:]+t[:1]))
    assert all(n==1 and edges[b,a]==1 for (a,b),n in edges.items()),name
    for a,b,c in tris:
        n=cross(sub(v[b],v[a]),sub(v[c],v[a])); assert dot(n,n)>1e-10,name
    volume=sum(dot(v[a],cross(v[b],v[c])) for a,b,c in tris)/6
    assert volume>0,name

def box(name,mat,x,y,z,w,h,d):
    v=[(x+sx*w/2,y+sy*h/2,z+sz*d/2) for sz in (-1,1) for sy in (-1,1) for sx in (-1,1)]
    solid(name,'Pylon',mat,v,[(0,1,3,2),(4,5,7,6),(0,2,6,4),(1,3,7,5),(0,1,5,4),(2,3,7,6)])

def beam(name,mat,a,b,w0,w1=None):
    axis=unit(sub(b,a)); ref=(0,0,1) if abs(axis[2])<.85 else (1,0,0)
    u=unit(cross(axis,ref)); v=cross(axis,u)
    verts=[]
    for p,w in ((a,w0),(b,w0 if w1 is None else w1)):
        for s,t in ((-1,-1),(1,-1),(1,1),(-1,1)):
            verts.append(add(p,add(mul(u,s*w/2),mul(v,t*w/2))))
    solid(name,'Pylon',mat,verts,[(0,1,2,3),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)])

# Large, sparse structural shapes readable from the fixed gameplay camera.
# Small feet are integral to the prop; there is no individual display slab.
for sx in (-1,1):
    for sz in (-1,1):
        x,z=sx*2.6,sz*2.05
        box('Local foot','Concrete',x,.22,z,1.65,.44,1.65)
        beam('Tapered leg','Steel',(x,.40,z),(sx*.92,13.5,sz*.78),.78,.56)
# Broad diagonal silhouettes, one per face and level rather than a dense lattice.
for sz in (-1,1):
    beam('Lower face brace','Dark',(-2.43,1.45,sz*1.92),(1.65,7.2,sz*1.35),.48)
    beam('Upper face brace','Dark',(1.65,7.2,sz*1.35),(-.97,12.85,sz*.83),.46)
for sx in (-1,1):
    beam('Lower side brace','Dark',(sx*2.43,1.45,1.92),(sx*1.65,7.2,-1.35),.48)
    beam('Upper side brace','Dark',(sx*1.65,7.2,-1.35),(sx*.97,12.85,.83),.46)
for y,x,z in ((6.9,1.7,1.38),(13.25,.99,.85)):
    for sz in (-1,1): beam('Face belt','Steel',(-x,y,sz*z),(x,y,sz*z),.54)
    for sx in (-1,1): beam('Side belt','Steel',(sx*x,y,-z),(sx*x,y,z),.53)
box('Central crown','Steel',0,13.75,0,2.75,1.15,2.12)
box('Wide crossbar','Steel',0,14.45,0,13.5,.80,1.05)
for sx in (-1,1):
    beam('Crossbar stay','Dark',(sx*1.0,12.35,0),(sx*5.85,14.25,0),.55)
    box('Ceramic silhouette','Ceramic',sx*5.65,13.60,0,.86,1.00,.86)
    box('Cable clasp','Dark',sx*5.65,13.04,0,.60,.38,.64)

# Two intentionally thick, stylized sagging strokes, made as uninterrupted
# tubes (no overlapping cylinders or coplanar segment caps).
for x in (-5.65,5.65):
    n=16; sides=8; radius=.19; v=[]
    for i in range(n+1):
        t=i/n; y=13.04-3.0*4*t*(1-t); z=SPAN*t
        tangent=unit((0,-12*(1-2*t)/SPAN,1)); up=cross(tangent,(1,0,0))
        for j in range(sides):
            angle=2*pi*j/sides
            v.append((x+radius*cos(angle),y+radius*sin(angle)*up[1],z+radius*sin(angle)*up[2]))
    v=[tuple(round(q,6) for q in p) for p in v]
    tris=[]
    for i in range(n):
        for j in range(sides):
            a=i*sides+j; b=i*sides+(j+1)%sides
            tris.extend(((a,b,b+sides),(a,b+sides,a+sides)))
    for j in range(1,sides-1):
        tris.extend(((0,j+1,j),(n*sides,n*sides+j,n*sides+j+1)))
    validate('Continuous cable',v,tris)
    PARTS.append(('Continuous cable','Span','Cable',v,tris))

def main():
    batches={}
    for name,group,mat,v,tris in PARTS:
        b=batches.setdefault((group,mat),[[],[]]); off=len(b[0]); b[0].extend(v); b[1].extend(i+off for t in tris for i in t)
    data={'palette':[{'name':k,'rgb':v} for k,v in PALETTE.items()], 'parts':[
        {'name':g+'_'+m,'group':g,'material':m,'vertices':[dict(zip('xyz',p)) for p in v],'triangles':t}
        for (g,m),(v,t) in batches.items()]}
    OUT.parent.mkdir(parents=True,exist_ok=True)
    OUT.write_text(json.dumps(data,separators=(',',':'))+'\n',encoding='utf-8')
    print('PG05:',len(PARTS),'closed solids;',sum(len(t) for *_,t in PARTS),'triangles;',len(batches),'batches')
    for g in ('Pylon','Span'):
        vv=[p for _,group,_,v,_ in PARTS if group==g for p in v]
        print(g,[(min(p[i] for p in vv),max(p[i] for p in vv)) for i in range(3)])
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    materials={}; collections={}
    for name,rgb in PALETTE.items():
        m=bpy.data.materials.new('PG05_'+name); m.diffuse_color=(*[x/255 for x in rgb],1); m.use_nodes=True
        bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=m.diffuse_color; bs.inputs['Roughness'].default_value=.9
        materials[name]=m
    for group in ('Pylon','Span'):
        col=bpy.data.collections.new(group); bpy.context.scene.collection.children.link(col); collections[group]=col
    for name,group,mat,v,tris in PARTS:
        c=center(v); mesh=bpy.data.meshes.new(name); mesh.from_pydata([(x-c[0],-(z-c[2]),y-c[1]) for x,y,z in v],[],tris)
        assert not mesh.validate(verbose=True),name
        mesh.update(); mesh.materials.append(materials[mat]); ob=bpy.data.objects.new(name,mesh); collections[group].objects.link(ob); ob.location=(c[0],-c[2],c[1])
    bpy.context.scene.unit_settings.system='METRIC'
    path=ROOT/'Resources/Blender/PG05_PowerLine.blend'; path.parent.mkdir(parents=True,exist_ok=True); bpy.ops.wm.save_as_mainfile(filepath=str(path))

if __name__=='__main__': main()
