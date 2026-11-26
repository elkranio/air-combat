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

# Central load-bearing beam, with a wide angular forehead and twin tail cheeks.
# Hull is deliberately above the cargo roof, leaving a visible coupling gap.
chamfer('Central keel','Body','Frame',0,2.05,0,6.7,1.7,15.1,.45)
chamfer('Armored central spine','Body','Hull',0,3.68,-.65,9.2,3.55,15.7,.85)
chamfer('Dorsal service cover','Body','Top',0,5.46,-.8,5.8,1.48,8.6,.4)
box('Recessed spine','Body','Frame',0,6.06,-.6,2.1,.15,5.9)
# Blunt, wide cockpit, distinctly forward with a dark faceted visor.
chamfer('Forward cab','Cab','Hull',0,3.66,7.2,7.6,3.0,5.2,.65)
chamfer('Cab upper armor','Cab','Top',0,4.92,7.08,6.3,.94,4.4,.3)
# Sloping cockpit glazing spans the forward shoulder and wraps the sides.
for s in (-1,1):
    slab('Sloped visor','Cab','Glass',[(s*.25,7.76),(s*2.65,7.76),(s*2.85,9.19),(s*.25,9.19)],5.40,5.58)
    box('Front visor','Cab','Glass',s*1.54,3.96,9.83,2.70,.72,.23)
    box('Cab marker','Cab','Red',s*3.52,3.26,9.59,.48,.46,.30)
box('Cab nose bumper','Cab','Frame',0,2.8,9.68,5.9,.40,.64)
# Broad skeletal outriggers. Four pods retain separate origins for later VFX.
for sx,side in [(-1,'L'),(1,'R')]:
    for sz,end in [(-1,'Rear'),(1,'Front')]:
        z=sz*5.3; asm='Engine'+end+side
        slab('Load bearing shoulder','Body','Hull',[(sx*3.5,z-2.75),(sx*7.8,z-2.14),(sx*8.25,z+1.65),(sx*3.5,z+2.75)],2.60,4.25)
        slab('Shoulder armor','Body','Top',[(sx*3.7,z-2.25),(sx*7.1,z-1.72),(sx*7.4,z+1.25),(sx*3.7,z+2.25)],4.25,4.55)
        # Angular rectangular nacelles, not Warden-style circular rotor ducts.
        x=sx*8.4
        chamfer('Engine armor',asm,'Hull',x,3.0,z,5.0,4.5,6.8,.60)
        chamfer('Upper engine plate',asm,'Top',x,5.12,z,4.5,.65,5.65,.24)
        chamfer('Lower nozzle flange',asm,'Frame',x,1.0,z,4.25,.80,5.45,.24)
        box('Dark underside nozzle',asm,'Dark',x,.59,z,3.35,.14,4.55)
        # Longitudinal intakes and protective grilles on top, readable from above.
        box('Recessed intake',asm,'Dark',x,5.47,z-.30,2.9,.13,3.25)
        for dz in (-1.35,-.70,-.05,.60):
            box('Intake rib',asm,'Steel',x,5.56,z+dz,2.94,.12,.23)
        box('Dominion stripe',asm,'Red',x,5.48,z+2.03,3.95,.13,.72)
        box('Outboard armor rail',asm,'Frame',sx*10.73,3.19,z,.50,1.45,4.95)
        box('Outboard pale marker',asm,'Vent',sx*10.96,3.20,z,.08,.42,2.0)
    # Twin rear tail plates with a short industrial vertical stabilizer.
    slab('Rear cheek','Tail'+side,'Frame',[(sx*1.7,-9.7),(sx*4.8,-9.7),(sx*5.05,-6.2),(sx*2.2,-6.2)],2.2,3.85)
    chamfer('Rear tail fin','Tail'+side,'Hull',sx*3.6,4.35,-8.3,1.2,1.85,2.8,.28)
    box('Tail recognition panel','Tail'+side,'Red',sx*3.6,5.32,-8.32,.78,.10,1.15)
box('Rear tow bumper','Body','Frame',0,2.7,-9.8,5.3,.48,.4)
# Four visibly mechanical clamps, exactly on the CG01 capture socket centers.
# No geometry enters negative Y (cargo roof is the shared Y=0 plane).
for i,(x,y,z) in enumerate(SOCKETS,1):
    asm=f'Clamp{i:02d}'
    chamfer('Clamp telescoping housing',asm,'Frame',x,1.28,z,1.72,1.45,1.9,.25)
    box('Clamp shaft',asm,'Steel',x,.57,z,.68,.91,.81)
    chamfer('Capture pad',asm,'Steel',x,.13,z,.68,.26,.77,.06)
    for sx in (-1,1):
        box('Clamp jaw',asm,'Frame',x+sx*.45,.34,z,.22,.54,1.05)
# Cargo-facing beams connect the four mounts without encroaching into cargo volume.
for z in (-4.65,4.65):
    box('Grapple cross beam','Body','Frame',0,1.55,z,6.8,.60,1.75)

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
    assert all(abs(a-b)<1e-6 for a,b in zip(lo,(-11,0,-10))),lo
    assert all(abs(a-b)<1e-6 for a,b in zip(hi,(11,6.2,10))),hi
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
