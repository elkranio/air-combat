#!/usr/bin/env python3
"""CA01 Dominion heavy cargo lifter. Unity Y-up +Z forward.
Root is the CG01 capture plane: attached cargo local position (0,-5,0).
Run with Blender bundled Python for editable blend and deterministic JSON.
"""
from pathlib import Path
from collections import Counter
import json

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Editor/Mission02/CA01Geometry.json'
PALETTE = {'Hull': (81, 91, 69), 'Top': (117, 129, 96),
           'Frame': (50, 60, 54), 'Steel': (104, 117, 109),
           'Red': (177, 58, 48), 'Dark': (31, 39, 39),
           'Glass': (44, 77, 82), 'Vent': (157, 151, 111)}
PARTS = []
SOCKETS = [(x, 0.0, z) for z in (-4.65,4.65) for x in (-2.45,2.45)]

def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def center(v): return tuple(sum(p[i] for p in v)/len(v) for i in range(3))


def solid(name, assembly, material, vertices, faces):
    v = [tuple(p) for p in vertices]
    c = center(v); tris = []
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
    PARTS.append((name,assembly,material,v,tris))


def extrusion(name, assembly, material, profile, z0,z1):
    n=len(profile); v=[(x,y,z) for z in (z0,z1) for x,y in profile]
    faces=[tuple(range(n)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    solid(name,assembly,material,v,faces)


def box(name, assembly, material, x,y,z,w,h,d):
    extrusion(name,assembly,material,[(x-w/2,y-h/2),(x+w/2,y-h/2),
              (x+w/2,y+h/2),(x-w/2,y+h/2)],z-d/2,z+d/2)


def chamfer(name, assembly, material, x,y,z,w,h,d,cut):
    l,r=x-w/2,x+w/2; b,t=y-h/2,y+h/2
    extrusion(name,assembly,material,[(l+cut,b),(r-cut,b),(r,b+cut),(r,t-cut),
              (r-cut,t),(l+cut,t),(l,t-cut),(l,b+cut)],z-d/2,z+d/2)

def slab(name,asm,mat,outline,y0,y1):
    n=len(outline);v=[(x,y,z) for y in (y0,y1) for x,z in outline]
    solid(name,asm,mat,v,[tuple(range(n)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)])

# V2: wedge-shaped central bridge, swept shoulders and longitudinally tapered
# power nacelles. Root and four cargo contacts deliberately unchanged.
PROFILE = [(-.68,-1),(.68,-1),(1,-.45),(1,.38),(.62,1),(-.62,1),(-1,.38),(-1,-.45)]

def loft(name, asm, mat, sections):
    # Each segment is a closed faceted solid; aligned section rings share a seam.
    # z, x center, y center, half width, half height
    n=len(PROFILE)
    for j,(a,b) in enumerate(zip(sections,sections[1:])):
        v=[(x+u*w,y+v*h,z) for z,x,y,w,h in (a,b) for u,v in PROFILE]
        faces=[tuple(range(n)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
        solid(name+' '+str(j),asm,mat,v,faces)

def yzplate(name,asm,mat,x0,x1,profile):
    n=len(profile);v=[(x,y,z) for x in (x0,x1) for y,z in profile]
    solid(name,asm,mat,v,[tuple(range(n)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)])

loft('Tapered lifting fuselage','Body','Hull',[
    (-10.8,0,3.30,2.15,.85),(-7.8,0,3.65,3.65,1.55),(-3.2,0,3.8,4.75,1.9),
    (3.9,0,3.85,4.6,1.85),(6.4,0,3.65,3.45,1.48)])
loft('Dorsal ridge','Body','Top',[(-7.8,0,5.1,1.5,.23),(-4.0,0,5.58,2.4,.55),
    (2.6,0,5.64,2.35,.56),(5.4,0,5.05,1.75,.26)])
# Visible belly frame, two load rails and crossmembers; wide open underside.
for s in (-1,1):
    loft('Skeletal load rail','Body','Frame',[(-8.5,s*2.45,1.76,.44,.34),(-5.9,s*2.45,1.52,.61,.52),
        (5.9,s*2.45,1.52,.61,.52),(8.25,s*2.45,2.05,.45,.34)])
for z in (-4.65,4.65):
    chamfer('Transverse grapple beam','Body','Frame',0,1.65,z,7.65,.75,1.32,.22)
    for s in (-1,1):
        yzplate('Grapple buttress','Body','Steel',s*2.45-.46,s*2.45+.46,[(1.85,z-.62),(2.75,z-1.5),(2.75,z+.9),(1.85,z+.62)])
# Nose wedge, low forehead, canopy flush with upper sloping facets.
loft('Wedge cockpit','Cab','Hull',[(4.65,0,3.75,3.45,1.5),(7.0,0,3.8,3.6,1.5),
    (9.2,0,3.28,2.95,1.02),(11.2,0,2.95,1.7,.55)])
for s in (-1,1):
    lo,hi=sorted((s*.20,s*1.68))
    yzplate('Swept cockpit glazing','Cab','Glass',lo,hi,[(5.24,7.06),(4.25,9.18),(4.34,9.18),(5.33,7.06)])
    yzplate('Forward glazing','Cab','Glass',lo,hi,[(4.25,9.18),(3.60,10.85),(3.69,10.85),(4.34,9.18)])
    # Small bright recognition panels on the nose shoulders.
    slab('Nose recognition','Cab','Red',[(s*2.15,8.55),(s*2.75,8.55),(s*2.25,9.36),(s*1.87,9.36)],4.26,4.34)
chamfer('Nose skid','Cab','Frame',0,2.41,10.35,3.85,.33,1.3,.12)

for sx,side in [(-1,'L'),(1,'R')]:
    for sz,end in [(-1,'Rear'),(1,'Front')]:
        z=sz*5.55; asm='Engine'+end+side; x=sx*8.15
        # Swept triangular shoulders, not horizontal square bars.
        slab('Swept wing stub','Body','Hull',[(sx*3.2,z-2.1),(sx*7.8,z-2.65),(sx*8.1,z+.85),(sx*3.7,z+2.55)],2.72,3.87)
        slab('Swept shoulder cap','Body','Top',[(sx*3.9,z-1.55),(sx*7.15,z-1.9),(sx*7.4,z+.48),(sx*4.15,z+1.9)],3.87,4.08)
        # Distinct front and rear engine planforms: tapered prow, angular tail,
        # flatter upper surfaces, visible faceted cheeks.
        sections=[(-4.35,1.18,.72,2.85),(-2.7,2.35,1.48,3.10),(-.85,2.68,1.92,3.15),
                  (1.65,2.58,1.86,3.15),(3.50,1.73,1.10,3.05),(4.35,.86,.48,2.95)] if end=='Front' else [
                  (-4.75,1.42,.72,2.8),(-3.05,2.46,1.48,3.03),(-1.0,2.72,1.91,3.16),
                  (1.3,2.5,1.77,3.10),(3.05,1.65,1.02,3.0),(3.9,.91,.51,2.85)]
        loft('Faceted turbine housing',asm,'Hull',[(z+dz,x,y,w,h) for dz,w,h,y in sections])
        # Long intake on the broad dorsal facet, narrower than the outer armor.
        panel=[(x-1.08,z-1.35),(x+1.08,z-1.35),(x+1.28,z-.7),(x+1.15,z+1.35),(x-1.15,z+1.35),(x-1.28,z-.7)]
        slab('Raised intake surround',asm,'Top',panel,4.88,5.10)
        slab('Recessed intake',asm,'Dark',[(x-.82,z-1.10),(x+.82,z-1.10),(x+1.02,z-.57),(x+.9,z+1.08),(x-.9,z+1.08),(x-1.02,z-.57)],5.10,5.15)
        for dz in (-.75,-.24,.27,.78):
            box('Intake vane',asm,'Steel',x,5.18,z+dz,1.68,.10,.15)
        # A diagonal recognition stripe on the outer cheek (not four red rectangles).
        slab('Engine recognition',asm,'Red',[(x+sx*.96,z-1.4),(x+sx*1.32,z-1.4),(x+sx*1.57,z-.8),(x+sx*1.18,z-.8)],5.07,5.15)
        # Eight-sided thrust outlet, inset dark face. Bottom remains above capture plane.
        chamfer('Nozzle shroud',asm,'Frame',x,1.08,z,3.75,1.2,4.0,.52)
        slab('Octagonal thrust outlet',asm,'Dark',[(x+dx,z+dz) for dx,dz in [(-1.02,-1.58),(1.02,-1.58),(1.58,-1.02),(1.58,1.02),(1.02,1.58),(-1.02,1.58),(-1.58,1.02),(-1.58,-1.02)]],.45,.60)
    # Swept tail plates, not upright boxes.
    slab('Tapered tail boom','Tail'+side,'Frame',[(sx*1.7,-10.8),(sx*4.9,-9.5),(sx*4.45,-6.3),(sx*2.1,-6.1)],2.15,3.0)
    yzplate('Swept tail fin','Tail'+side,'Hull',sx*3.65-.27,sx*3.65+.27,[(3.0,-10.4),(5.24,-9.75),(5.02,-8.65),(3.0,-6.8)])
# Original CG01-compatible capture pads, exposed below the frame.
for i,(x,y,z) in enumerate(SOCKETS,1):
    asm=f'Clamp{i:02d}'
    chamfer('Clamp telescoping housing',asm,'Frame',x,1.28,z,1.72,1.45,1.9,.25)
    box('Clamp shaft',asm,'Steel',x,.57,z,.68,.91,.81)
    chamfer('Capture pad',asm,'Steel',x,.13,z,.68,.26,.77,.06)
    for sx in (-1,1):
        box('Clamp jaw',asm,'Frame',x+sx*.45,.34,z,.22,.54,1.05)

def vector(p): return dict(zip(('x','y','z'),(round(x,6) for x in p)))


def export():
    # Merge components by detachable assembly/material. Retain local pivots.
    groups=[]
    for asm in dict.fromkeys(a for _,a,_,_,_ in PARTS):
        pieces=[p for p in PARTS if p[1]==asm]; allv=[p for _,_,_,v,_ in pieces for p in v]
        pivot=tuple((min(p[k] for p in allv)+max(p[k] for p in allv))/2 for k in range(3))
        verts=[]; slots=[]
        for mat in dict.fromkeys(m for _,_,m,_,_ in pieces):
            tris=[]
            for _,_,m,v,t in pieces:
                if m!=mat: continue
                off=len(verts); verts.extend(vector(sub(p,pivot)) for p in v)
                tris.extend(i+off for face in t for i in face)
            slots.append({'material':mat,'triangles':tris})
        groups.append({'name':asm,'pivot':vector(pivot),'vertices':verts,'slots':slots})
    points=[p for _,_,_,v,_ in PARTS for p in v]
    lo=[min(p[i] for p in points) for i in range(3)]; hi=[max(p[i] for p in points) for i in range(3)]
    assert all(abs(a-b)<1e-6 for a,b in zip(lo,(-10.87,0,-10.8))),lo
    assert all(abs(a-b)<1e-6 for a,b in zip(hi,(10.87,6.2,11.2))),hi
    OUT.parent.mkdir(parents=True,exist_ok=True)
    OUT.write_text(json.dumps({'palette':[{'name':k,'rgb':list(v)} for k,v in PALETTE.items()],
        'assemblies':groups,'sockets':[vector(p) for p in SOCKETS]},separators=(',',':'))+'\n',encoding='utf-8')
    print(f'{len(PARTS)} source solids / {len(groups)} detachable assemblies / {sum(len(t) for *_,t in PARTS)} triangles; bounds {lo} to {hi}')
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    def bl(p): return (p[0],-p[2],p[1])
    def empty(name,pos=(0,0,0),parent=None):
        ob=bpy.data.objects.new(name,None); bpy.context.scene.collection.objects.link(ob)
        ob.parent=parent; ob.location=bl(pos); ob.empty_display_size=.4; return ob
    root=empty('CA01_Carryall'); mats={}
    for name,rgb in PALETTE.items():
        m=bpy.data.materials.new('CA01_'+name); color=tuple(x/255 for x in rgb)
        m.diffuse_color=(*color,1); m.use_nodes=True
        bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*color,1); bs.inputs['Roughness'].default_value=.86; mats[name]=m
    parents={g['name']:empty(g['name'],tuple(g['pivot'][k] for k in ('x','y','z')),root) for g in groups}
    for name,asm,mat,v,t in PARTS:
        # Each source solid remains individually editable in Blender.
        pivot=center(v); mesh=bpy.data.meshes.new(name); mesh.from_pydata([bl(sub(p,pivot)) for p in v],[],t)
        assert not mesh.validate(),name; mesh.update(); mesh.materials.append(mats[mat])
        ob=bpy.data.objects.new(name,mesh); bpy.context.scene.collection.objects.link(ob); ob.parent=parents[asm]
        ob.location=tuple(a-b for a,b in zip(bl(pivot),parents[asm].location))
    for i,p in enumerate(SOCKETS,1): empty(f'Clamp_{i:02d}',p,root)
    bpy.context.scene.unit_settings.system='METRIC'
    path=ROOT/'Resources/Blender/CA01_Carryall.blend'; path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))

if __name__=='__main__': export()
