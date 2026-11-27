#!/usr/bin/env python3
"""WH02 equipment shelter. Unity X,Y,Z (+Y up); Blender optional for editable source.
Run with Blender --background --factory-startup --python <this script>.
Plain Python also regenerates the deterministic Unity JSON.
"""
from pathlib import Path
from collections import Counter
import json

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Editor/Mission02/WH02Geometry.json'
PALETTE = {'Concrete': (127, 139, 126), 'Plinth': (83, 92, 76),
           'Roof': (119, 132, 124), 'RoofTrim': (90, 102, 86),
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

# Two skid-mounted equipment bays with a low, flat roof and raised service boxes.
# No concrete foundation or permanent infrastructure.
for x in (-5.15,5.15):
    box('Transport skid','Foundation','Plinth',x,.22,0,.9,.44,20)
for z in (-9,0,9):
    box('Cross bearer','Foundation','Steel',0,.3,z,13.2,.28,.65)
for side,label in [(-1,'Left'),(1,'Right')]:
    asm=label+'Wall'
    box('Equipment bay skin',asm,'Concrete',side*6.45,2.48,0,.30,4.08,19)
    for z in (-9.05,0,9.05):
        box('Corner or bay post',asm,'Steel',side*6.63,2.48,z,.46,4.08,.48)
    for y in (.62,4.43):
        box('Horizontal frame',asm,'Steel',side*6.65,y,0,.38,.32,19.5)
    # Broad removable access panels rather than windows or tiny rivets.
    for z in (-4.45,4.45):
        box('Access panel',asm,'Plinth',side*6.65,2.52,z,.16,2.85,6.7)
        box('Access latch',asm,'Stencil',side*6.77,2.42,z+2.6,.12,.36,.24)
for side,label in [(-1,'Rear'),(1,'Front')]:
    asm=label+'Wall'; z=side*9.53
    box('End wall',asm,'Concrete',0,2.48,z,12.7,4.08,.3)
    for x in (-6.25,6.25):
        box('End posts',asm,'Steel',x,2.48,side*9.68,.46,4.08,.45)
    box('End top rail',asm,'Steel',0,4.42,side*9.69,12.9,.34,.36)
    if side==1:
        box('Personnel door recess',asm,'Recess',-3.5,2.14,9.72,2.6,3.4,.13)
        box('Personnel door',asm,'Plinth',-3.5,2.14,9.82,2.32,3.12,.12)
        box('Window',asm,'Glass',2.1,3.05,9.72,3.3,1.1,.15)
        box('Window divider',asm,'Steel',2.1,3.05,9.83,.17,1.12,.12)
    else:
        box('Vent recess',asm,'Recess',0,2.8,-9.72,7.4,1.75,.15)
        for y in (2.24,2.65,3.06,3.47):
            box('Louver',asm,'RoofTrim',0,y,-9.85,7.15,.18,.20)
# Low panel roofs are independent; dark perimeter rails and one marked central seam.
for i,(a,b) in enumerate(((-10,-3.4),(-3.4,3.4),(3.4,10))):
    asm=f'Roof{i+1:02d}'; d=b-a; z=(a+b)/2
    box('Roof cassette',asm,'Roof',0,4.75,z,14,.48,d)
    for x in (-6.75,6.75):
        box('Side rim',asm,'Steel',x,5.0,z,.4,.26,d)
    box('End seam',asm,'RoofTrim',0,5.0,a+.16,13.1,.22,.32)
    if i==2: box('End seam',asm,'RoofTrim',0,5.0,b-.16,13.1,.22,.32)
    if i==1:
        box('Red identification',asm,'Red',-3.4,5.015,0,2.7,.05,1.1)
        box('Yellow stripe',asm,'Stencil',-3.4,5.015,.83,2.7,.05,.30)
    if i in (0,2):
        # Large low-profile ventilation cassettes break the flat roof silhouette.
        x=2.65 if i==0 else -2.65
        box('Equipment housing',asm,'Plinth',x,5.43,z,4.3,.86,3.25)
        box('Vent top recess',asm,'Recess',x,5.89,z,3.6,.12,2.65)
        for k in (-1.0,0,1.0):
            box('Vent grille',asm,'RoofTrim',x+k,5.95,z,.20,.10,2.35)

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
    assert all(abs(a-b)<1e-6 for a,b in zip(lo,(-7,0,-10))),lo
    assert all(abs(a-b)<1e-6 for a,b in zip(hi,(7,6,10))),hi
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
    root=empty('WH02_EquipmentShelter'); mats={}
    for name,rgb in PALETTE.items():
        m=bpy.data.materials.new('WH02_'+name); color=tuple(x/255 for x in rgb)
        m.diffuse_color=(*color,1); m.use_nodes=True
        bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*color,1); bs.inputs['Roughness'].default_value=.88; mats[name]=m
    parents={g['name']:empty(g['name'],tuple(g['pivot'][k] for k in 'xyz'),root) for g in groups}
    for name,asm,mat,v,t in SOLIDS:
        pivot=center(v); mesh=bpy.data.meshes.new(name); mesh.from_pydata([bl(sub(p,pivot)) for p in v],[],t)
        assert not mesh.validate(),name; mesh.update(); mesh.materials.append(mats[mat])
        ob=bpy.data.objects.new(name,mesh); bpy.context.scene.collection.objects.link(ob)
        ob.parent=parents[asm]; ob.location=bl(sub(pivot,tuple(next(g['pivot'] for g in groups if g['name']==asm)[k] for k in 'xyz')))
    target=ROOT/'Resources/Blender/WH02_EquipmentShelter.blend'; target.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(target))

if __name__=='__main__': export()
