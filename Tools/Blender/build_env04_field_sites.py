#!/usr/bin/env python3
"""Fictional low-poly field-site dressing. Python exports OBJ, Blender also saves editable library.
Unity Y-up, ground at zero. No functional equipment geometry or gameplay components.
"""
from pathlib import Path
from math import sin, cos, pi, sqrt, radians
from collections import Counter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Environment/Mission01_FirstSortie/ENV04_FieldSites'
PALETTE = {'Hull':(98,107,66), 'Equipment':(75,80,59), 'Metal':(52,59,50),
           'Caps':(182,180,156), 'Pad':(176,158,134), 'Track':(164,145,122)}
MODELS = {}

def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def norm(v,f):
    n=cross(sub(v[f[1]],v[f[0]]),sub(v[f[2]],v[f[0]])); l=sqrt(dot(n,n)); assert l>1e-8; return tuple(x/l for x in n)

def solid(model, name, group, v, faces):
    center=tuple(sum(p[i] for p in v)/len(v) for i in range(3)); tris=[]
    for f in faces:
        f=tuple(f)
        if dot(norm(v,f),sub(center,v[f[0]]))>0: f=f[::-1]
        tris.extend((f[0],f[i],f[i+1]) for i in range(1,len(f)-1))
    edges=Counter((a,b) for f in tris for a,b in zip(f,f[1:]+f[:1]))
    assert all(n==1 and edges[b,a]==1 for (a,b),n in edges.items()),name
    assert all(dot(norm(v,f),sub(center,v[f[0]]))<0 for f in tris),name
    MODELS.setdefault(model,[]).append((name,group,v,tris))

def box(m,n,g,x,y,z,w,h,d,rotate=0):
    a=radians(rotate); v=[]
    for sz in (-1,1):
        for sy in (-1,1):
            for sx in (-1,1):
                px,py,pz=sx*w/2,sy*h/2,sz*d/2
                v.append((x+px,y+py*cos(a)-pz*sin(a),z+py*sin(a)+pz*cos(a)))
    solid(m,n,g,v,[(0,1,3,2),(4,5,7,6),(0,2,6,4),(1,3,7,5),(0,1,5,4),(2,3,7,6)])

def chamfer(m,n,g,x,y,z,w,h,d,c):
    outline=[(-w/2+c,-d/2),(w/2-c,-d/2),(w/2,-d/2+c),(w/2,d/2-c),(w/2-c,d/2),(-w/2+c,d/2),(-w/2,d/2-c),(-w/2,-d/2+c)]
    v=[(x+a,y+dy,z+b) for dy in (-h/2,h/2) for a,b in outline]
    solid(m,n,g,v,[tuple(range(8)),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)])

def cyl(m,n,g,x,z,y0,y1,r,sides=8):
    v=[(x+r*cos(2*pi*i/sides),y,z+r*sin(2*pi*i/sides)) for y in (y0,y1) for i in range(sides)]
    solid(m,n,g,v,[tuple(range(sides)),tuple(range(sides,2*sides))]+[(i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides)])

def patch(m,n,g,outline):
    # All faces upward; visual-only flat mesh, same palette texel as original sand.
    v=[(x,0,z) for x,z in outline]; f=[]
    tris=[(0,1,2),(0,2,5),(5,2,3),(5,3,4)] if len(v)==6 else [(0,i,i+1) for i in range(1,len(v)-1)]
    for t in tris:
        if norm(v,t)[1]<0:t=t[::-1]
        f.append(t)
    MODELS.setdefault(m,[]).append((n,g,v,f))

# Standalone fictional antenna on a short equipment plinth; intentionally small.
m='ENV04_A_FieldRadar'
chamfer(m,'Base','Equipment',0,.22,0,3.1,.44,2.6,.24)
for s in (-1,1): box(m,'Skid','Metal',s*1.23,.10,0,.32,.2,3.0)
chamfer(m,'Cabinet','Hull',0,.88,0,1.65,1.08,1.48,.12)
box(m,'Cabinet lid','Equipment',0,1.48,0,1.78,.16,1.61)
cyl(m,'Pedestal','Metal',0,0,1.55,2.6,.29)
box(m,'Panel backing','Equipment',0,3.03,0,3.25,1.42,.26, -26)
box(m,'Panel face','Caps',0,3.03+.16*sin(radians(26)),.16*cos(radians(26)),2.93,1.12,.12,-26)
# Graphic face strips, not a functional antenna design.
for x in (-.92,0,.92): box(m,'Panel division','Equipment',x,3.03+.235*sin(radians(26)),.235*cos(radians(26)),.085,1.02,.05,-26)
box(m,'Head bracket','Metal',0,2.47,-.22,.62,.35,.50)

# Four reusable small equipment forms; intentional chunky silhouette and broad bands.
def crate(m,w,h,d,g):
    chamfer(m,'Body',g,0,h*.5,0,w,h,d,.10)
    chamfer(m,'Lid','Equipment',0,h+.07,0,w+.10,.14,d+.10,.08)
    for z in (-d*.31,d*.31): box(m,'Top band','Metal',0,h+.155,z,w+.12,.065,.12)
    for x in (-w*.35,w*.35): box(m,'Foot','Metal',x,.07,0,.20,.14,d*.92)
    box(m,'Label','Caps',w*.22,h+.16,0,.30,.035,.22)
crate('ENV04_B_SupplyCrate',2.05,1.16,1.65,'Hull')
crate('ENV04_C_LongCase',1.22,.78,3.35,'Hull')
m='ENV04_D_Generator'
chamfer(m,'Body','Hull',0,.82,0,2.35,1.42,1.70,.14)
for x in (-.85,.85):box(m,'Base rail','Metal',x,.10,0,.30,.2,1.90)
box(m,'Top lid','Equipment',0,1.59,0,2.43,.15,1.78)
box(m,'Front panel','Equipment',0,.87,.87,1.83,.90,.10)
for x in (-.55,-.28,0,.28,.55):box(m,'Vent','Metal',x,.88,.94,.13,.65,.06)
box(m,'Top service plate','Caps',.55,1.69,-.36,.61,.06,.50)
m='ENV04_E_Pallet'
for x in (-.63,0,.63):box(m,'Runner','Metal',x,.085,0,.18,.17,1.8)
for z in (-.72,-.24,.24,.72):box(m,'Deck','Equipment',0,.24,z,1.8,.14,.33)
box(m,'Case','Hull',-.30,.67,0,.95,.72,1.23)
box(m,'Case lid','Equipment',-.30,1.07,0,1.02,.12,1.30)
box(m,'Small case','Caps',.46,.52,.22,.42,.42,.67)

# Compact irregular hardstand, two variants. Ground material comes from sand in Unity.
patch('ENV04_F_Hardstand','Pad','Pad',[(-12,-8),(-8,-12),(5,-12.5),(12,-9),(14,0),(11,10),(2,12),(-10,10),(-14,2)])
patch('ENV04_G_HardstandLong','Pad','Pad',[(-9,-12),(-4,-15),(5,-14),(10,-9),(11,5),(8,13),(-3,15),(-10,10),(-12,-2)])
# Broken twin tracks, no textures or noise; gentle kink and tapered ends.
m='ENV04_H_Tracks'
for s in (-1,1):
    x=s*1.42
    patch(m,'Track near','Track',[(x-.32,0),(x+.30,0),(x+.32,-6),(x+.54,-10),(x-.05,-10),(x-.30,-6)])
    patch(m,'Track far','Track',[(x-.01,-10.7),(x+.53,-10.7),(x+.91,-16),(x+.80,-22),(x+.55,-21.3),(x+.34,-16)])

def export():
    OUT.mkdir(parents=True,exist_ok=True)
    count=0
    for model,parts in MODELS.items():
        for group in sorted({g for _,g,_,_ in parts}):
            lines=['# ENV04 fictional decorative prop; Unity Y-up',f'o {model}_{group}','s off','vt 0.0078125 0.0156250']; offset=0; ni=0
            for _,g,v,f in parts:
                if g!=group:continue
                lines.extend('v %.6f %.6f %.6f'%p for p in v)
                for t in f:
                    lines.append('vn %.8f %.8f %.8f'%norm(v,t));ni+=1
                    lines.append('f '+' '.join(f'{j+offset+1}/1/{ni}' for j in t));count+=1
                offset+=len(v)
            (OUT/f'{model}_{group}.obj').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    print(len(MODELS),'models;',count,'triangles')
    try:import bpy
    except ImportError:return
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    mats={}
    for g,rgb in PALETTE.items():
        mat=bpy.data.materials.new('ENV04_'+g);mat.diffuse_color=tuple(c/255 for c in rgb)+(1,);mats[g]=mat
    for i,(model,parts) in enumerate(MODELS.items()):
        col=bpy.data.collections.new(model);bpy.context.scene.collection.children.link(col)
        for n,g,v,f in parts:
            mesh=bpy.data.meshes.new(n);mesh.from_pydata([(x,-z,y) for x,y,z in v],[],f);mesh.update();mesh.materials.append(mats[g]);ob=bpy.data.objects.new(n,mesh);col.objects.link(ob);ob.location=(i%4*45,i//4*45,0)
            for p in mesh.polygons:p.use_smooth=False
    dest=ROOT/'Resources/Blender/ENV04_FieldSites.blend';dest.parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(dest))

if __name__=='__main__':export()
