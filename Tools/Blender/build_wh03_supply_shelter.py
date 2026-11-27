#!/usr/bin/env python3
"""WH03 supply shelter. Unity X,Y,Z (+Y up); Blender optional for editable source.
Run with Blender --background --factory-startup --python <this script>.
Plain Python also regenerates the deterministic Unity JSON.
"""
from pathlib import Path
from collections import Counter
import json

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Editor/Mission02/WH03Geometry.json'
PALETTE = {'Concrete': (127, 139, 126), 'Plinth': (83, 92, 76),
           'Roof': (91, 106, 101), 'RoofTrim': (90, 102, 86),
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

# Open-sided, single-pitch shelter. Front loading area is uncovered;
# the stock is visible from the actual oblique gameplay camera.
for x in (-6.4,6.4):
    box('Portable base rail','Foundation','Plinth',x,.20,-3.5,.7,.4,14.5)
for side,label in [(-1,'Left'),(1,'Right')]:
    asm=label+'Wall'
    for z,h in [(-10.4,4.9),(-3.3,5.65),(3.7,6.45)]:
        box('Upright',asm,'Steel',side*7.35,(h+.25)/2,z,.5,h-.25,.5)
        box('Foot plate',asm,'Plinth',side*7.35,.13,z,1,.26,1)
    # Low removable wind screens leave the upper sides open.
    box('Rear wind screen',asm,'Concrete',side*7.35,1.30,-6.85,.22,2.1,6.55)
    box('Screen top',asm,'RoofTrim',side*7.35,2.42,-6.85,.32,.22,6.55)
    yz('Raked side rail',asm,'Steel',side*7.35-.20,side*7.35+.20,
       [(-10.7,4.65),(4.2,6.25),(4.2,6.55),(-10.7,4.95)])
for x in (-4.8,0,4.8):
    box('Rear screen','RearWall','Concrete',x,1.55,-10.45,4.75,2.6,.26)
    box('Screen upright','RearWall','Steel',x-2.35,1.65,-10.45,.22,2.8,.34)
box('Rear beam','RearWall','Steel',0,4.82,-10.45,15.2,.38,.4)
box('Front lintel','FrontWall','Steel',0,6.26,3.7,15.2,.5,.48)
# Two removable roof halves; a real slope in Z, no opaque front wall.
def roof_y(z): return 5.2+(z+11)*1.4/15.5
for i,(a,b) in enumerate(((-11,-3.25),(-3.25,4.5))):
    asm=f'Roof{i+1:02d}'
    yz('Roof skin',asm,'Roof',-8,8,[(a,roof_y(a)-.24),(b,roof_y(b)-.24),(b,roof_y(b)),(a,roof_y(a))])
    for x in (-7.72,7.72):
        yz('Edge rail',asm,'RoofTrim',x-.22,x+.22,[(a,roof_y(a)-.05),(b,roof_y(b)-.05),(b,roof_y(b)+.24),(a,roof_y(a)+.24)])
    for z in (a+.22,b-.22):
        box('Transverse roof rib',asm,'Steel',0,roof_y(z)+.06,z,15.4,.22,.26)
    if i==1:
        # Follow the actual roof slope for flush markings.
        for mat,x0,x1,z0,z1 in [('Red',-3.8,-1.0,.1,1.35),('Stencil',-3.8,-1.0,1.48,1.80)]:
            yz('Field ID',asm,mat,x0,x1,[(z0,roof_y(z0)),(z1,roof_y(z1)),(z1,roof_y(z1)+.045),(z0,roof_y(z0)+.045)])
# Raised front fascia and the high edge establish the 7-unit silhouette.
box('Front fascia','Roof02','RoofTrim',0,6.75,4.32,15.5,.5,.36)
# Two independent cargo clusters, including a loading stack outside the roof.
# Stock is part of the shelter's art, not CG01 or a separate objective.
for asm,x,z,w,d,h in [('Cargo01',-3.65,6.9,4.8,5.8,2.3),('Cargo02',3.5,8.0,4.5,6,2.7)]:
    box('Pallet',asm,'Plinth',x,.18,z,w+.15,.36,d)
    box('Supply case',asm,'Concrete',x,.36+h/2,z,w-.3,h,d-.45)
    for xx in (x-w*.32,x+w*.32):
        box('Case top band',asm,'Steel',xx,.36+h+.075,z,.35,.15,d-.33)
        for zz in (z-d*.5+.18,z+d*.5-.18):
            box('Case end band',asm,'Steel',xx,.36+h/2,zz,.35,h,.16)
    box('Case label',asm,'Stencil',x,.36+h+.09,z,.9,.18,.6)
    if asm=='Cargo01':
        box('Small case',asm,'Plinth',x+.55,.36+h+.5,z-.6,2.2,1,2.4)
        box('Small case lid',asm,'RoofTrim',x+.55,.36+h+1.05,z-.6,2.35,.1,2.55)

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
    assert all(abs(a-b)<1e-6 for a,b in zip(lo,(-8,0,-11))),lo
    assert all(abs(a-b)<1e-6 for a,b in zip(hi,(8,7,11))),hi
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
    root=empty('WH03_SupplyShelter'); mats={}
    for name,rgb in PALETTE.items():
        m=bpy.data.materials.new('WH03_'+name); color=tuple(x/255 for x in rgb)
        m.diffuse_color=(*color,1); m.use_nodes=True
        bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*color,1); bs.inputs['Roughness'].default_value=.88; mats[name]=m
    parents={g['name']:empty(g['name'],tuple(g['pivot'][k] for k in 'xyz'),root) for g in groups}
    for name,asm,mat,v,t in SOLIDS:
        pivot=center(v); mesh=bpy.data.meshes.new(name); mesh.from_pydata([bl(sub(p,pivot)) for p in v],[],t)
        assert not mesh.validate(),name; mesh.update(); mesh.materials.append(mats[mat])
        ob=bpy.data.objects.new(name,mesh); bpy.context.scene.collection.objects.link(ob)
        ob.parent=parents[asm]; ob.location=bl(sub(pivot,tuple(next(g['pivot'] for g in groups if g['name']==asm)[k] for k in 'xyz')))
    target=ROOT/'Resources/Blender/WH03_SupplyShelter.blend'; target.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(target))

if __name__=='__main__': export()
