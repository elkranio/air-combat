#!/usr/bin/env python3
"""BD01 warehouse. Unity X,Y,Z (+Y up); Blender optional for editable source.
Run with Blender --background --factory-startup --python <this script>.
Plain Python also regenerates the deterministic Unity JSON.
"""
from pathlib import Path
from collections import Counter
import json

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Editor/Mission02/BD01Geometry.json'
PALETTE = {'Concrete': (133, 139, 117), 'Plinth': (83, 92, 76),
           'Roof': (153, 158, 137), 'RoofTrim': (90, 102, 86),
           'Steel': (75, 85, 76), 'Recess': (49, 57, 54),
           'Glass': (86, 113, 116), 'Red': (153, 56, 48),
           'Stencil': (215, 165, 56)}
SOLIDS = []

def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def center(v): return tuple(sum(p[i] for p in v)/len(v) for i in range(3))

def solid(name, assembly, material, vertices, faces):
    v = list(vertices); c = center(v); tris = []
    for face in faces:
        a,b,d = (v[i] for i in face[:3]); n = cross(sub(b,a),sub(d,a))
        assert dot(n,n)>1e-10, name
        if dot(n,sub(c,a))>0: face = face[::-1]
        for i in range(1,len(face)-1): tris.append((face[0],face[i],face[i+1]))
    edges = Counter((a,b) for t in tris for a,b in zip(t,t[1:]+t[:1]))
    assert all(n==1 and edges[b,a]==1 for (a,b),n in edges.items()), name
    for i,j,k in tris:
        a,b,d = v[i],v[j],v[k]; n = cross(sub(b,a),sub(d,a))
        assert dot(n,n)>1e-10 and dot(n,sub(c,a))<0, name
    SOLIDS.append((name,assembly,material,v,tris))

# Convex profiles in the Z/Y plane, extruded across X. Each solid is closed.
def yz(name,asm,mat,x0,x1,profile):
    n=len(profile); v=[(x,y,z) for x in (x0,x1) for z,y in profile]
    faces=[tuple(range(n)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    solid(name,asm,mat,v,faces)

def box(name,asm,mat,x,y,z,w,h,d):
    yz(name,asm,mat,x-w/2,x+w/2,[(z-d/2,y-h/2),(z+d/2,y-h/2),(z+d/2,y+h/2),(z-d/2,y+h/2)])

# Extrude a convex X/Y cross-section along Z.
def xy(name,asm,mat,z0,z1,profile):
    n=len(profile); v=[(x,y,z) for z in (z0,z1) for x,y in profile]
    faces=[tuple(range(n)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    solid(name,asm,mat,v,faces)

# Temporary field shelter: no concrete slab, only two transport skids.
for x in (-6.5,6.5):
    box('Skid','Foundation','Plinth',x,.22,0,1.05,.44,28)
for z in (-12.5,0,12.5):
    box('Cross bearer','Foundation','Steel',0,.28,z,16.8,.36,.8)
for s,label in [(-1,'Left'),(1,'Right')]:
    asm=label+'Wall'
    box('Panel wall',asm,'Concrete',s*8.35,3.17,0,.34,5.46,27)
    box('Lower rail',asm,'Plinth',s*8.48,.70,0,.34,.48,27.1)
    box('Eave rail',asm,'Steel',s*8.48,5.76,0,.48,.46,27.1)
    for z in (-12.85,-4.6,4.6,12.85):
        box('External upright',asm,'Steel',s*8.55,3.17,z,.48,5.46,.48)
    for z in (-8.7,0,8.7):
        box('Panel seam',asm,'RoofTrim',s*8.535,3.1,z,.08,4.95,.11)
for s,label in [(-1,'Rear'),(1,'Front')]:
    asm=label+'Wall'; z=s*13.34
    # Continuous closed wall with a shallow polygonal gable above.
    box('End panels',asm,'Concrete',0,3.17,z,16.36,5.46,.34)
    xy('Gable infill',asm,'Concrete',z-.17,z+.17,[(-8.18,5.8),(8.18,5.8),(3,8.6),(-3,8.6)])
    for x in (-7.95,7.95): box('Corner post',asm,'Steel',x,3.17,s*13.58,.48,5.46,.42)
    box('End rail',asm,'Steel',0,5.76,s*13.57,16.4,.35,.36)
    if s==1:
        box('Door surround',asm,'Recess',0,2.72,13.63,9.2,4.55,.2)
        box('Door left',asm,'Plinth',-2.13,2.64,13.76,4.13,4.17,.18)
        box('Door right',asm,'Plinth',2.13,2.64,13.76,4.13,4.17,.18)
        for x in (-4.45,4.45): box('Door jamb',asm,'Steel',x,2.72,13.83,.28,4.55,.27)
        box('Door header',asm,'Steel',0,5.0,13.83,9.2,.34,.27)
        for x in (-.4,.4): box('Handle',asm,'Stencil',x,2.25,13.91,.16,.56,.12)
    else:
        box('Vent frame',asm,'Recess',0,4.3,-13.60,4.8,.82,.16)
        for x in (-1.5,0,1.5): box('Vent slat',asm,'Steel',x,4.3,-13.72,.18,.62,.10)
    box('Dominion marking',asm,'Red',-6.3,4.7,s*13.64,1.35,.6,.10)

# Three longitudinal roof sections share the same faceted profile.
# Low eaves, two broad sloping shoulders, flat crown. No clerestory.
def roof_y(x): return 8.8-max(0,abs(x)-3)*2.8/6

def strip(name,asm,mat,z0,z1,x0,x1,offset,thick):
    a,b=roof_y(x0)+offset,roof_y(x1)+offset
    xy(name,asm,mat,z0,z1,[(x0,a-thick),(x1,b-thick),(x1,b),(x0,a)])
for i,(a,b) in enumerate(((-14,-4.6),(-4.6,4.6),(4.6,14))):
    asm=f'Roof{i+1:02d}'
    for x0,x1 in ((-9,-3),(-3,3),(3,9)):
        strip('Roof skin',asm,'Roof',a,b,x0,x1,0,.24)
        # Distinct frame seam at the boundary of each transported roof bay.
        z=a+.22
        strip('Transverse roof rib',asm,'RoofTrim',z-.18,z+.18,x0,x1,.16,.22)
        if i==2:
            z=b-.22
            strip('End roof rib',asm,'RoofTrim',z-.18,z+.18,x0,x1,.16,.22)
    for x in (-3,3):
        box('Crown joint',asm,'Steel',x,8.87,(a+b)/2,.16,.26,b-a)
    if i==1:
        strip('Logistics marking',asm,'Red',-.8,.8,-2.2,2.2,.04,.06)
        strip('Hazard stripe',asm,'Stencil',1.0,1.33,-2.2,2.2,.04,.06)

def vector(p): return dict(zip('xyz',(round(x,6) for x in p)))
def export():
    groups=[]
    for asm in dict.fromkeys(a for _,a,_,_,_ in SOLIDS):
        pieces=[p for p in SOLIDS if p[1]==asm]; vv=[p for *_,v,t in pieces for p in v]
        pivot=tuple((min(p[k] for p in vv)+max(p[k] for p in vv))/2 for k in range(3))
        verts=[]; slots=[]
        for mat in dict.fromkeys(m for _,_,m,_,_ in pieces):
            tris=[]
            for _,_,m,v,t in pieces:
                if m!=mat: continue
                off=len(verts); verts.extend(vector(sub(p,pivot)) for p in v)
                tris.extend(i+off for face in t for i in face)
            slots.append({'material':mat,'triangles':tris})
        groups.append({'name':asm,'pivot':vector(pivot),'vertices':verts,'slots':slots})
    points=[p for *_,v,t in SOLIDS for p in v]
    lo=[min(p[k] for p in points) for k in range(3)]; hi=[max(p[k] for p in points) for k in range(3)]
    assert all(abs(a-b)<1e-6 for a,b in zip(lo,(-9,0,-14))),lo
    assert all(abs(a-b)<1e-6 for a,b in zip(hi,(9,9,14))),hi
    OUT.parent.mkdir(parents=True,exist_ok=True)
    OUT.write_text(json.dumps({'palette':[{'name':k,'rgb':list(v)} for k,v in PALETTE.items()], 'assemblies':groups},separators=(',',':'))+'\n',encoding='utf-8')
    print(f'{len(SOLIDS)} source solids / {len(groups)} assemblies / {sum(len(t) for *_,t in SOLIDS)} triangles; bounds {lo} to {hi}')
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    def bl(p): return (p[0],-p[2],p[1])
    def empty(name,pos=(0,0,0),parent=None):
        ob=bpy.data.objects.new(name,None); bpy.context.scene.collection.objects.link(ob)
        ob.parent=parent; ob.location=bl(pos); ob.empty_display_size=1; return ob
    root=empty('BD01_SupplyWarehouse'); mats={}
    for name,rgb in PALETTE.items():
        m=bpy.data.materials.new('BD01_'+name); color=tuple(x/255 for x in rgb)
        m.diffuse_color=(*color,1); m.use_nodes=True
        bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*color,1); bs.inputs['Roughness'].default_value=.88; mats[name]=m
    parents={g['name']:empty(g['name'],tuple(g['pivot'][k] for k in 'xyz'),root) for g in groups}
    for name,asm,mat,v,t in SOLIDS:
        pivot=center(v); mesh=bpy.data.meshes.new(name); mesh.from_pydata([bl(sub(p,pivot)) for p in v],[],t)
        assert not mesh.validate(),name; mesh.update(); mesh.materials.append(mats[mat])
        ob=bpy.data.objects.new(name,mesh); bpy.context.scene.collection.objects.link(ob)
        ob.parent=parents[asm]; ob.location=bl(sub(pivot,tuple(next(g['pivot'] for g in groups if g['name']==asm)[k] for k in 'xyz')))
    target=ROOT/'Resources/Blender/BD01_SupplyWarehouse.blend'; target.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(target))

if __name__=='__main__': export()
