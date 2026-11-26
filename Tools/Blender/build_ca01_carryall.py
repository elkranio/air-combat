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
PALETTE = {'Hull': (65, 72, 76), 'Top': (104, 116, 123),
           'Frame': (40, 47, 51), 'Steel': (125, 137, 143),
           'Red': (177, 58, 48), 'Dark': (28, 32, 35),
           'Glass': (44, 77, 82), 'Vent': (213, 165, 47)}  # V9A Heavy Duty
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

# V8: remove tall rear fins that obscure the fans from the oblique game camera.
# Preserve the CG01 capture plane and all twelve named assemblies.
from math import sin, cos, pi
PROFILE = [(-.64,-1),(.64,-1),(1,-.40),(1,.38),(.65,1),(-.65,1),(-1,.38),(-1,-.40)]

def loft(name, asm, mat, sections):
    n=len(PROFILE)
    for j,(a,b) in enumerate(zip(sections,sections[1:])):
        v=[(x+u*w,y+v*h,z) for z,x,y,w,h in (a,b) for u,v in PROFILE]
        faces=[tuple(range(n)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
        solid(name+' '+str(j),asm,mat,v,faces)

def yzplate(name,asm,mat,x0,x1,profile):
    n=len(profile);v=[(x,y,z) for x in (x0,x1) for y,z in profile]
    solid(name,asm,mat,v,[tuple(range(n)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)])

def duct(name,asm,x,z,outer,inner):
    # Watertight annular shroud, with actual open bore and distinct lip material.
    n=16
    profile=[(outer-.24,.75),(outer,1.12),(outer,3.45),(outer-.24,4.03),
             (inner+.15,4.03),(inner,3.73),(inner,1.05),(inner+.20,.75)]
    v=[(x+r*cos(2*pi*k/n),y,z+r*sin(2*pi*k/n)) for r,y in profile for k in range(n)]
    tris=[]; groups={}
    for j in range(len(profile)):
        q=(j+1)%len(profile)
        mat='Top' if j in (2,3) else 'Dark' if j in (4,5) else 'Frame' if j in (6,7) else 'Hull'
        for k in range(n):
            l=(k+1)%n;a,b,c,d=j*n+k,j*n+l,q*n+l,q*n+k
            t=[(a,b,c),(a,c,d)];tris.extend(t)
            paint = mat
            if j in (2,3):
                if k in (0,2,8,10): paint = 'Vent'
                elif k in (1,9): paint = 'Frame'
            groups.setdefault(paint,[]).extend(t)
    edges=Counter((a,b) for t in tris for a,b in zip(t,t[1:]+t[:1]))
    assert all(n==1 and edges[b,a]==1 for (a,b),n in edges.items()),name
    vol=sum(dot(v[a],cross(v[b],v[c])) for a,b,c in tris)/6
    assert abs(vol)>1e-6,name
    for mat,ts in groups.items():
        if vol<0:ts=[(a,c,b) for a,b,c in ts]
        for a,b,c in ts:assert dot(cross(sub(v[b],v[a]),sub(v[c],v[a])),cross(sub(v[b],v[a]),sub(v[c],v[a])))>1e-10
        PARTS.append((name,asm,mat,v,ts))

def disk(name,asm,mat,x,z,r,y0,y1,n=12):
    slab(name,asm,mat,[(x+r*cos(2*pi*k/n),z+r*sin(2*pi*k/n)) for k in range(n)],y0,y1)

# Retain exposed load rails under a wider, tapered lifting fuselage.
for s in (-1,1):
    loft('Load rail','Body','Frame',[(-8.65,s*2.45,1.55,.42,.40),(-5.6,s*2.45,1.65,.48,.58),
        (5.6,s*2.45,1.65,.48,.58),(8.0,s*2.45,2.15,.36,.40)])
loft('Armored lifting hull','Body','Hull',[(-8.8,0,2.9,1.45,.65),(-6.6,0,3.25,2.52,1.10),
    (-1.5,0,3.55,2.84,1.20),(4.9,0,3.67,3.12,1.27),(7.0,0,3.8,2.83,1.0)])
loft('Dorsal armor','Body','Top',[(-6.4,0,4.32,1.20,.23),(-4.9,0,4.82,1.80,.43),
    (3.8,0,5.03,2.05,.54),(5.7,0,4.83,1.85,.32)])
# Tapered, faceted wing sections lofted along X. Their chord is broad at the
# body and narrow at the nacelle; the gap is a swept triangular relief.
def shoulder(name,asm,mat,side,sections):
    n=len(PROFILE)
    for j,(a,b) in enumerate(zip(sections,sections[1:])):
        v=[(side*x,y+h*q,z+w*u) for x,z,y,w,h in (a,b) for u,q in PROFILE]
        faces=[tuple(range(n)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
        solid(name+' '+str(j),asm,mat,v,faces)
for z in (-4.65,4.65):
    chamfer('Cross beam','Body','Steel',0,1.70,z,6.6,.76,1.48,.20)
    for s in (-1,1):
        yzplate('Frame buttress','Body','Frame',s*2.45-.28,s*2.45+.28,[(2.0,z-.8),(3.38,z-1.65),(3.38,z+1.65),(2.0,z+.8)])
# Cab extends well ahead of the pod noses, broad dark glazing on top AND front.
loft('Cab prow','Cab','Hull',[(5.4,0,3.80,2.18,1.10),(7.6,0,4.03,2.85,1.50),
    (10.0,0,3.83,2.75,1.48),(12.15,0,3.15,1.95,.88),(12.7,0,2.95,1.35,.52)])
# Broad canopy solid lies above the armor, not buried inside it.
loft('Canopy crown','Cab','Glass',[(7.65,0,5.46,1.72,.18),(8.75,0,5.65,2.12,.39),
    (10.02,0,5.52,1.95,.41),(11.90,0,4.13,1.50,.24)])
loft('Canopy center divider','Cab','Frame',[(7.65,0,5.64,.10,.035),(8.75,0,6.04,.10,.035),
    (10.02,0,5.93,.10,.035),(11.90,0,4.37,.10,.035)])
for s in (-1,1):
    # visible lateral cab windows, sitting outside the hull
    yzplate('Side cockpit window','Cab','Glass',*sorted((s*2.72,s*2.84)),[(3.78,8.1),(4.63,8.1),(4.62,9.75),(3.84,10.05)])
    box('Nose light','Cab','Vent',s*1.0,2.94,12.68,.55,.20,.08)
chamfer('Nose skid','Cab','Frame',0,2.18,11.55,3.45,.35,1.75,.12)

for s,side in [(-1,'L'),(1,'R')]:
    x=s*10.2
    # Broad forward shoulders read as part of the fuselage, not four ribs.
    # Narrower trailing attachments preserve a small oblique opening over the cargo.
    shoulder('Main lifting shoulder','Body','Hull',s,[(2.15,2.55,3.42,5.15,1.10),
        (4.2,2.93,3.38,4.85,.90),(6.4,3.50,3.33,3.85,.66),(7.35,4.6,3.17,2.30,.48)])
    shoulder('Upper swept armor','Body','Top',s,[(2.40,3.0,4.38,3.72,.25),
        (4.35,3.43,4.20,3.40,.22),(6.10,4.0,3.97,2.58,.20),(7.45,4.63,3.75,1.25,.18)])
    shoulder('Aft structural fairing','Body','Frame',s,[(2.0,-4.9,2.95,2.94,.76),
        (4.3,-5.05,2.96,2.42,.72),(6.2,-5.4,3.03,1.80,.54),(7.35,-5.65,3.08,1.28,.43)])
    shoulder('Aft armor cap','Body','Hull',s,[(2.2,-5.05,3.62,2.34,.23),
        (4.4,-5.24,3.62,1.84,.22),(6.25,-5.6,3.52,1.32,.20),(7.35,-5.77,3.46,.80,.15)])
    # Low swept infill extends the main shoulder toward the inner nacelle wall.
    # Leave a smaller rear relief: no new rib, no change to the main shoulder.
    shoulder('Inner shoulder infill','Body','Hull',s,[(4.5,-1.25,3.25,1.65,.47),
        (6.5,-.60,3.24,2.0,.46),(8.6,0.0,3.24,2.12,.44)])
    # Independent turbine pods: retain the V5 lower covers only under each duct.
    for end,z,r,inner in [('Front',4.5,3.25,2.35),('Rear',-4.5,3.45,2.48)]:
        asm='Engine'+end+side
        if end=='Front':
            loft('Continuous nacelle forward keel',asm,'Frame',[(2.0,x,1.95,1.55,.85),(4.5,x,2.0,2.45,.80),(7.3,x,2.0,1.92,.80),(9.15,x,2.85,.82,.58)])
            # Lower cover stays below the dark fan disk, even at the inner end.
            loft('Outer prow armor',asm,'Hull',[(7.05,x,3.0,2.38,.82),(8.25,x-s*.10,2.98,1.65,.78),(9.15,x-s*.30,2.85,.68,.35)])
        else:
            loft('Continuous nacelle rear keel',asm,'Frame',[(-9.6,x,2.2,1.45,.64),(-7.2,x,2.0,2.35,.80),(-4.5,x,2.0,2.55,.80),(-2.0,x,1.95,1.55,.85)])
            loft('Rear engine fairing',asm,'Hull',[(-9.6,x,2.8,1.20,.52),(-8.15,x,3.02,2.0,.78),(-7.05,x,3.0,2.38,.75)])
        duct('Open turbine shroud',asm,x,z,r,inner)
        disk('Dark turbine well',asm,'Dark',x,z,inner-.06,2.94,3.04,16)
        # Seven static readable blades on recessed fan disk, not tiny decorative grilles.
        for k in range(7):
            a=2*pi*k/7+(.18 if s>0 else -.18)
            def p(rad,t):return (x+rad*cos(a)-t*sin(a),z+rad*sin(a)+t*cos(a))
            slab('Swept turbine blade',asm,'Steel',[p(.49,-.13),p(inner-.20,-.36*s-.20),p(inner-.20,-.36*s+.20),p(.49,.13)],3.08,3.25)
        disk('Turbine hub',asm,'Frame',x,z,.68,3.18,3.48)
        disk('Hub cap',asm,'Vent',x,z,.35,3.48,3.57,8)
        # Large dark longitudinal cheek, shared styling across the pair.
        slab('Outboard cheek',asm,'Frame',[(x+s*2.55,z-2.2),(x+s*3.10,z-1.45),(x+s*3.10,z+1.45),(x+s*2.55,z+2.2)],2.12,3.38)
        if end=='Front':
            slab('Dominion shoulder marking',asm,'Red',[(x+s*.65,7.75),(x+s*1.35,7.45),(x+s*1.65,7.88),(x+s*.90,8.17)],3.70,3.83)
    # No bridge across the open space between the turbine rims.
    asm='Tail'+side
    # Keep the low rear tail plate, without a fin above the turbine.
    slab('Rear tail plate',asm,'Vent',[(s*3.05,-7.5),(s*7.95,-9.5),(s*9.4,-8.6),(s*7.8,-6.4),(s*4.0,-6.65)],2.55,2.85)
# Move the entire four engine assemblies, keeping their local geometry coherent.
PARTS[:] = [(name,asm,mat,[(x,y,z+(.9 if asm.startswith('EngineFront') else -.9)) for x,y,z in v],t)
            if asm.startswith('Engine') else (name,asm,mat,v,t)
            for name,asm,mat,v,t in PARTS]
# Keep the exact existing CG01 capture pads.
for i,(x,y,z) in enumerate(SOCKETS,1):
    asm=f'Clamp{i:02d}'
    chamfer('Clamp telescoping housing',asm,'Frame',x,1.28,z,1.72,1.45,1.9,.25)
    box('Clamp shaft',asm,'Steel',x,.57,z,.68,.91,.81)
    chamfer('Capture pad',asm,'Steel',x,.13,z,.68,.26,.77,.06)
    for sx in (-1,1):
        box('Clamp jaw',asm,'Vent',x+sx*.45,.34,z,.22,.54,1.05)

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
    assert all(abs(a-b)<1e-6 for a,b in zip(lo,(-13.65,0,-10.5))),lo
    assert all(abs(a-b)<1e-6 for a,b in zip(hi,(13.65,6.075,12.72))),hi
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
