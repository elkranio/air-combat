#!/usr/bin/env python3
"""CG01 Dominion armored cargo module. Unity metres, Y-up, +Z forward.
Run with Blender --background --factory-startup --python <this script>.
Writes deterministic authoring geometry and an editable Blender file.
Plain Python is sufficient for the geometry export; Blender is optional.
"""
from pathlib import Path
from collections import Counter
import json

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Editor/Mission02/CG01Geometry.json'
PALETTE = {'Armor': (94, 105, 79), 'Top': (128, 139, 105),
           'Frame': (55, 65, 59), 'Steel': (99, 113, 108),
           'Ochre': (192, 139, 54), 'Dark': (36, 43, 41),
           'Red': (177, 58, 48), 'Stencil': (205, 211, 177)}
PARTS = []
SOCKETS = [(x, 5.0, z) for z in (-4.65, 4.65) for x in (-2.45, 2.45)]


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

# Reinforced skid and isolated core; all origins/coordinates are ground-relative.
for x in (-2.72,2.72):
    chamfer('Skid','Cradle','Frame',x,.36,0,1.55,.72,16,.17)
    box('Skid wear strip','Cradle','Steel',x,.12,0,1.24,.16,15.5)
box('Cradle deck','Cradle','Frame',0,.72,0,7.35,.48,15.4)
for z in (-5.8,0,5.8):
    box('Equipment mount','Core','Dark',0,1.15,z,5.55,.40,2.3)
chamfer('Sealed inner equipment','Core','Ochre',0,2.45,0,5.1,2.3,12.4,.42)
for z in (-3.7,3.7):
    chamfer('Core retention collar','Core','Steel',0,2.45,z,5.5,2.62,.52,.36)

# Independently detachable armor plates; a chamfered profile, not corrugated shipping box.
for s,label in [(-1,'Left'),(1,'Right')]:
    # Three convex slabs form the stepped armor cross section.
    extrusion(label+' lower armor',label+'Armor','Armor',[(s*x,y) for x,y in [(2.73,.94),(3.37,.94),(3.78,1.48),(3.19,1.69)]],-7.42,7.42)
    extrusion(label+' main armor',label+'Armor','Armor',[(s*x,y) for x,y in [(3.19,1.69),(3.78,1.48),(3.78,3.48),(3.19,3.19)]],-7.42,7.42)
    extrusion(label+' shoulder armor',label+'Armor','Top',[(s*x,y) for x,y in [(3.19,3.19),(3.78,3.48),(2.91,4.35),(2.57,3.81)]],-7.42,7.42)
    box(label+' recessed belt',label+'Armor','Frame',s*3.79,2.1,0,.10,.50,12.65)
    for z in (-2.8,2.8):
        box(label+' lock plate',label+'Armor','Steel',s*3.86,2.10,z,.13,.74,1.15)
# Roof is one separate plate, with subtly recessed central ID panel.
chamfer('Armored roof','Lid','Top',0,4.09,0,5.83,.54,14.84,.16)
box('Dorsal inset','Lid','Frame',0,4.382,0,2.45,.08,7.0)
# End bulkheads and inset panels (not doors from a generic shipping container).
for s,label in [(-1,'Rear'),(1,'Front')]:
    chamfer(label+' bulkhead',label+'Cap','Armor',0,2.60,s*7.47,7.12,3.35,.58,.47)
    chamfer(label+' inset panel',label+'Cap','Frame',0,2.63,s*7.79,5.46,2.48,.15,.38)
    box(label+' reinforced bar',label+'Cap','Steel',0,2.63,s*7.91,4.78,.48,.14)
    box(label+' warning plate',label+'Cap','Ochre',0,3.37,s*7.89,1.62,.38,.12)
# Two massive protective ribs; matching top grapple points remain distinct pieces.
for i,z in enumerate((-4.65,4.65),1):
    asm=f'Rib{i}'
    box('Crown '+str(i),asm,'Frame',0,4.43,z,5.94,.50,1.02)
    box('Crown band '+str(i),asm,'Ochre',0,4.704,z,5.48,.055,.38)
    for s in (-1,1):
        extrusion('Shoulder rib',asm,'Frame',[(s*x,y) for x,y in [(2.72,4.18),(2.93,4.68),(3.96,3.63),(3.76,3.13)]],z-.51,z+.51)
        extrusion('Vertical rib',asm,'Frame',[(s*x,y) for x,y in [(3.76,3.13),(3.96,3.63),(3.96,1.46),(3.72,1.12)]],z-.51,z+.51)
        box('Side band',asm,'Ochre',s*3.97,2.44,z,.06,1.30,.39)
# Four open-top recessed capture sockets: flat capture plane exactly Y5.
# Their centers are exposed to the future Carryall as named empty transforms.
for i,(x,y,z) in enumerate(SOCKETS,1):
    asm=f'Capture{i:02d}'
    box('Socket seat',asm,'Dark',x,4.73,z,1.26,.12,1.36)
    for s in (-1,1):
        box('Socket cheek',asm,'Steel',x+s*.48,4.86,z,.27,.28,1.36)
        box('Socket end',asm,'Steel',x,4.86,z+s*.55,.69,.28,.26)
# Roof ID, intentionally large enough for arcade camera; CG01, rendered as block stencil.
GLYPHS = {'C':['111','100','100','100','111'], 'G':['111','100','101','101','111'],
          '0':['111','101','101','101','111'], '1':['010','110','010','010','111']}
# Text runs along the cargo length, read from one side. Individual tiles are merged into Lid.
for j,ch in enumerate('CG01'):
    for row, line in enumerate(GLYPHS[ch]):
        for col, bit in enumerate(line):
            if bit=='1': box('Stencil '+ch,'Lid','Stencil',(2-row)*.27,4.43,(j*4+col-7.5)*.27,.255,.022,.255)
# Small red Dominion recognition panels, no oversized hazard paint.
for z in (-3.18,3.18):
    box('Faction recognition','Lid','Red',0,4.435,z,1.42,.035,.38)


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
    assert all(abs(a-b)<1e-6 for a,b in zip(lo,(-4,0,-8))),lo
    assert all(abs(a-b)<1e-6 for a,b in zip(hi,(4,5,8))),hi
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
    root=empty('CG01_SpecialCargo'); mats={}
    for name,rgb in PALETTE.items():
        m=bpy.data.materials.new('CG01_'+name); color=tuple(x/255 for x in rgb)
        m.diffuse_color=(*color,1); m.use_nodes=True
        bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*color,1); bs.inputs['Roughness'].default_value=.86; mats[name]=m
    parents={g['name']:empty(g['name'],tuple(g['pivot'][k] for k in ('x','y','z')),root) for g in groups}
    for name,asm,mat,v,t in PARTS:
        # Each source solid remains individually editable in Blender.
        pivot=center(v); mesh=bpy.data.meshes.new(name); mesh.from_pydata([bl(sub(p,pivot)) for p in v],[],t)
        assert not mesh.validate(),name; mesh.update(); mesh.materials.append(mats[mat])
        ob=bpy.data.objects.new(name,mesh); bpy.context.scene.collection.objects.link(ob); ob.parent=parents[asm]
        ob.location=tuple(a-b for a,b in zip(bl(pivot),parents[asm].location))
    for i,p in enumerate(SOCKETS,1): empty(f'Grab_{i:02d}',p,root)
    bpy.context.scene.unit_settings.system='METRIC'
    path=ROOT/'Resources/Blender/CG01_SpecialCargo.blend'; path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))

if __name__=='__main__': export()
