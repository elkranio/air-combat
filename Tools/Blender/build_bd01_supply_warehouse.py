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
PALETTE = {'Concrete': (145, 143, 127), 'Plinth': (101, 104, 94),
           'Roof': (75, 85, 79), 'RoofTrim': (102, 113, 102),
           'Steel': (84, 94, 88), 'Recess': (48, 57, 54),
           'Glass': (86, 113, 116), 'Red': (153, 56, 48),
           'Stencil': (193, 194, 169)}
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

# Footprint 44 x 64, foundation at Y0. Four independent perimeter walls.
box('Concrete footing','Foundation','Plinth',0,.4,0,44,.8,64)
for s,label in [(-1,'Left'),(1,'Right')]:
    asm=label+'Wall'
    box('Wall',asm,'Concrete',s*20.4,4.4,0,1.2,7.2,61.6)
    box('Lower band',asm,'Plinth',s*21.03,1.35,0,.22,1.1,61.6)
    for z in (-25,-12.5,0,12.5,25):
        box('Buttress',asm,'Steel',s*21.15,4.4,z,.72,7.2,1.4)
    box('Eave beam',asm,'Steel',s*20.4,7.86,0,1.7,.45,61.6)
for s,label in [(-1,'Rear'),(1,'Front')]:
    asm=label+'Wall'
    box('Wall',asm,'Concrete',0,4.4,s*30.2,39.6,7.2,1.2)
    box('Lower band',asm,'Plinth',0,1.35,s*30.84,39.6,1.1,.24)
    for x in (-19,0,19):
        box('Pilaster',asm,'Steel',x,4.4,s*30.9,1.2,7.2,.7)
    # Deep dark loading doors, split facade; no actual interior in BD01.
    for x in (-9.8,9.8):
        if s==1:
            box('Door frame',asm,'Recess',x,3.6,31.04,12.0,5.6,.46)
            box('Roll-up door',asm,'Steel',x,3.45,31.30,10.8,4.95,.13)
            for y in (2.05,3.25,4.45):
                box('Door joint',asm,'Recess',x,y,31.38,10.55,.13,.04)
            box('Header',asm,'RoofTrim',x,6.25,31.29,11.9,.45,.35)
        else:
            box('Service vent',asm,'Recess',x,5.9,-31.02,6.8,1.4,.24)
            for u in (-2,0,2): box('Vent slat',asm,'Steel',x+u,5.9,-31.18,.25,1.1,.09)

# Three long industrial sawteeth along the length. All roof parts have independent pivots.
# Rising slope from rear (-Z) to front (+Z); the high vertical face is a dark clerestory.
for i in range(3):
    a=-31.5+i*21; b=a+21; asm=f'Roof{i+1:02d}'
    lo,hi=8.2,11.8
    def y(z): return lo+(hi-lo)*(z-a)/(b-a)
    def slab(name,mat,x0,x1,z0,z1,offset,thick):
        yz(name,asm,mat,x0,x1,[(z0,y(z0)+offset),(z1,y(z1)+offset),(z1,y(z1)+offset+thick),(z0,y(z0)+offset+thick)])
    slab('Sloped roofing','Roof',-21.8,21.8,a,b,-.32,.32)
    # Enclosed ends: side profiles follow the roof, separate from the roof skin.
    for x0,x1 in [(-21.0,-20.25),(20.25,21.0)]:
        yz('Sawtooth end',asm,'Concrete',x0,x1,[(a,7.85),(b,7.85),(b,11.48),(a,7.88)])
    box('Clerestory beam',asm,'Steel',0,9.82,b-.38,40.5,3.4,.74)
    for x in (-12.5,0,12.5):
        box('Dark clerestory strip',asm,'Glass',x,10.15,b+.02,11.5,1.55,.09)
    # Silhouette seams: two edge strips and one central seam, not a corrugated texture.
    for x in (-21.55,0,21.55):
        slab('Raised seam','RoofTrim',x-.19,x+.19,a+.2,b-.2,.01,.16)
    slab('High edge cap','RoofTrim',-21.8,21.8,b-.7,b,.01,.19)
    # Restrained red logistics markings on the first and third sections.
    if i != 1:
        slab('Dominion red recognition panel','Red',-17,-10,a+3,a+5,.025,.04)
    if i == 1:
        # Large block stencil 01, painted onto the inclined surface (closed thin geometry).
        glyphs=['111/101/101/101/111','010/110/010/010/111']
        for j,g in enumerate(glyphs):
            for row,line in enumerate(g.split('/')):
                for col,bit in enumerate(line):
                    if bit=='1':
                        x=(j*4+col-3)*1.25+4.5; z=a+13-row*1.25
                        slab('Roof stencil','Stencil',x-.56,x+.56,z-.56,z+.56,.025,.035)

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
    assert all(abs(a-b)<1e-6 for a,b in zip(lo,(-22,0,-32))),lo
    assert all(abs(a-b)<1e-6 for a,b in zip(hi,(22,12,32))),hi
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
