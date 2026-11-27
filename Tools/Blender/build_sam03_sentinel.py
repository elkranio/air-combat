#!/usr/bin/env python3
"""SAM03 Sentinel V1B: fictional low-poly visual asset; Unity Y-up, +Z front.
Blender --background --factory-startup --python Tools/Blender/build_sam03_sentinel.py
Writes deterministic Unity geometry JSON and editable .blend (when bpy is present).
"""
import json
from collections import Counter
from math import cos, sin, radians, pi
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Editor/SAM03Geometry.json'
PALETTE = {
    'Armor': [145, 164, 181], 'Hull': [91, 118, 141],
    'Dark': [41, 54, 66], 'Steel': [113, 134, 151],
    'Sensor': [62, 91, 110], 'Mark': [226, 143, 48],
    'Insignia': [219, 229, 235],
    'BaseHull': [57, 72, 85], 'BaseDeck': [83, 101, 116],
}
PARTS = []

def paint_for(group, mat):
    # Keep original mesh batch names; isolate chassis paint from the turret/pods.
    if group == 'Fixed':
        return {'Hull': 'BaseHull', 'Armor': 'BaseDeck'}.get(mat, mat)
    return mat

def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def center(v): return tuple(sum(p[i] for p in v)/len(v) for i in range(3))

def solid(name, group, mat, vertices, faces):
    v = [tuple(round(x,6) for x in p) for p in vertices]
    c = center(v); tris = []
    for f in faces:
        f = list(f); a,b,d = (v[i] for i in f[:3]); n = cross(sub(b,a),sub(d,a))
        assert dot(n,n) > 1e-10, name
        if dot(n,sub(c,a)) > 0: f.reverse()
        tris.extend((f[0],f[i],f[i+1]) for i in range(1,len(f)-1))
    edges = Counter((a,b) for t in tris for a,b in zip(t,t[1:]+t[:1]))
    assert all(n == 1 and edges[b,a] == 1 for (a,b),n in edges.items()), name
    for a,b,d in tris:
        n = cross(sub(v[b],v[a]),sub(v[d],v[a]))
        assert dot(n,n)>1e-10 and dot(n,sub(c,v[a]))<0, name
    PARTS.append((name,group,mat,v,tris))

def octagon(w,d,cut):
    x,z=w/2,d/2
    return [(x-cut,z),(x,z-cut),(x,-z+cut),(x-cut,-z),(-x+cut,-z),(-x,-z+cut),(-x,z-cut),(-x+cut,z)]

def loft(name,group,mat,sections,profile,transform=lambda p:p):
    # Sections share a convex profile, and are straight or tapered.
    n=len(profile); v=[transform((x+u*wx,y,z+q*wz)) for y,x,z,wx,wz in sections for u,q in profile]
    faces=[list(range(n)),list(range(len(v)-n,len(v)))]
    for k in range(len(sections)-1):
        for i in range(n):
            j=(i+1)%n; a=k*n+i; b=k*n+j; faces.append([a,b,b+n,a+n])
    solid(name,group,mat,v,faces)

def slab(name,group,mat,y0,y1,w,d,cut,x=0,z=0,top=1):
    p=octagon(w,d,cut)
    loft(name,group,mat,[(y0,x,z,1,1),(y1,x,z,top,top)],p)

def box(name,group,mat,x,y,z,w,h,d,transform=lambda p:p):
    v=[transform((x+sx*w/2,y+sy*h/2,z+sz*d/2)) for sz in (-1,1) for sy in (-1,1) for sx in (-1,1)]
    solid(name,group,mat,v,[(0,1,3,2),(4,5,7,6),(0,2,6,4),(1,3,7,5),(0,1,5,4),(2,3,7,6)])

def cylinder(name,group,mat,y0,y1,r0,r1,x=0,z=0,sides=12):
    p=[(cos(2*pi*i/sides),sin(2*pi*i/sides)) for i in range(sides)]
    loft(name,group,mat,[(y0,x,z,r0,r0),(y1,x,z,r1,r1)],p)

# Compact integrated chassis; no concrete pad or separate scenery.
slab('Lower chassis','Fixed','Dark',0,.24,9.55,7.9,1.35,top=.96)
slab('Armored hull','Fixed','Hull',.24,.68,9.2,7.55,1.30,top=.91)
slab('Upper deck','Fixed','Armor',.68,.86,8.38,6.87,1.18,top=.96)
# Rear service strip and high-contrast faction identifier, all low-profile.
box('Rear inset','Fixed','Dark',0,.89,-2.83,3.15,.08,.91)
# Three-pronged Trident symbol, built as simple raised inlays on flat deck.
box('Trident shaft','Fixed','Insignia',0,.947,-2.89,.11,.025,.60)
box('Trident crossbar','Fixed','Insignia',0,.947,-2.82,.65,.025,.105)
for side in (-1,1):
    box('Trident outer tine '+str(side),'Fixed','Insignia',side*.27,.947,-2.66,.11,.025,.40)
    box('Chassis orange tab '+str(side),'Fixed','Mark',side*3.0,.88,-2.0,.62,.08,.34)
    box('Chassis seam '+str(side),'Fixed','Dark',side*3.83,.81,.0,.18,.10,1.4)
# Broad shallow rotating mount, visually integrated into the chassis.
cylinder('Recessed bearing','Fixed','Dark',.84,1.10,2.12,2.0)
cylinder('Swivel ring','Yaw','Steel',1.10,1.23,1.86,1.82)
slab('Swivel carapace','Yaw','Hull',1.23,1.58,6.75,3.65,.95,top=.90)
slab('Center bridge','Yaw','Armor',1.58,1.92,2.40,2.95,.60,top=.85)
# Long angular nacelles are much flatter than Bastion. Separate silhouette
# with a generous central channel; narrow faceted front and dark cell covers.
angle=radians(16)
def tilt(p):
    x,y,z=p
    return (x,2.34+y*cos(angle)+z*sin(angle),.12-y*sin(angle)+z*cos(angle))

def orient(p): return tilt((p[0],p[2],p[1]))
for side,group in [(-1,'Left'),(1,'Right')]:
    x=side*2.75
    slab(group+' recessed cradle','Yaw','Dark',1.48,1.97,1.72,2.75,.44,x=x,z=-.10,top=.85)
    loft(group+' swept pod',group,'Hull',[
        (-2.72,x,0,.70,.74),(-2.2,x,0,1,1),
        (.95,x,0,1,1),(2.45,x,0,.77,.80),(2.78,x,0,.67,.70)],
        octagon(2.36,1.47,.40),orient)
    # Raised beveled dorsal armor gives a distinct two-tone wedge.
    loft(group+' dorsal plate',group,'Armor',[
        (.66,x,-.45,1,1),(.82,x,-.45,.86,.90)],octagon(1.50,3.02,.30),tilt)
    box(group+' orange recognition band',group,'Mark',x,.84,.19,1.16,.055,.32,tilt)
    # Front face: two dark inlaid covers; rear cap in dark steel.
    box(group+' front recessed face',group,'Dark',x,0,2.80,1.43,.88,.09,tilt)
    for q in (-1,1):
        box(group+' cell lid '+str(q),group,'Steel',x+q*.33,0,2.86,.54,.68,.06,tilt)
    box(group+' rear service face',group,'Dark',x,0,-2.75,1.47,.87,.08,tilt)
# Low rear-centered flat radar. No tall exposed mast or separate pedestal.
slab('Radar foot','Yaw','Dark',1.56,2.20,1.45,1.37,.34,z=-1.29,top=.75)
angle_sensor=radians(-14)
def sensor(p):
    x,y,z=p
    return (x,2.91+y*cos(angle_sensor)+z*sin(angle_sensor),-1.35-y*sin(angle_sensor)+z*cos(angle_sensor))
loft('Radar chamfered shell','Radar','Hull',[
    (-.19,0,0,.88,.88),(.19,0,0,1,1)],octagon(2.55,1.35,.25),lambda p:sensor((p[0],p[2],p[1])))
box('Radar face','Radar','Sensor',0,0,.205,2.14,.95,.075,sensor)
box('Radar face stripe','Radar','Steel',0,-.30,.252,1.92,.09,.035,sensor)

def main():
    # Batch by stable group/material names for Unity; keep all solids separate in Blender.
    batches={}
    for name,group,mat,v,tris in PARTS:
        b=batches.setdefault((group,mat),[[],[]]); offset=len(b[0]); b[0].extend(v); b[1].extend(i+offset for t in tris for i in t)
    data={'palette':[{'name':k,'rgb':v} for k,v in PALETTE.items()], 'parts':[
        {'name':group+'_'+mat,'group':group,'material':paint_for(group,mat),'vertices':[dict(zip('xyz',p)) for p in v],'triangles':t}
        for (group,mat),(v,t) in batches.items()]}
    OUT.parent.mkdir(parents=True,exist_ok=True)
    OUT.write_text(json.dumps(data,separators=(',',':'))+'\n',encoding='utf-8')
    vertices=[p for _,_,_,v,_ in PARTS for p in v]
    print('SAM03:',len(PARTS),'solids,',sum(len(t) for _,_,_,_,t in PARTS),'triangles,',len(batches),'mesh batches')
    print('Bounds:',[(min(p[i] for p in vertices),max(p[i] for p in vertices)) for i in range(3)])
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    materials={}; collections={}
    for name,rgb in PALETTE.items():
        mat=bpy.data.materials.new('SAM03_'+name); mat.diffuse_color=(*[x/255 for x in rgb],1); mat.use_nodes=True
        shader=mat.node_tree.nodes.get('Principled BSDF'); shader.inputs['Base Color'].default_value=mat.diffuse_color; shader.inputs['Roughness'].default_value=.9; materials[name]=mat
    for name in ('Fixed','Yaw','Left','Right','Radar'):
        col=bpy.data.collections.new(name); bpy.context.scene.collection.children.link(col); collections[name]=col
    for name,group,mat,v,tris in PARTS:
        mesh=bpy.data.meshes.new(name); mesh.from_pydata([(x,-z,y) for x,y,z in v],[],tris)
        assert not mesh.validate(verbose=True), name
        mesh.update(); mesh.materials.append(materials[paint_for(group,mat)]); obj=bpy.data.objects.new(name,mesh); collections[group].objects.link(obj)
        c=center(v); origin=(c[0],-c[2],c[1]); obj.location=origin
        for vert in mesh.vertices:
            for i in range(3): vert.co[i]-=origin[i]
    bpy.context.scene.unit_settings.system='METRIC'
    path=ROOT/'Resources/Blender/SAM03_Sentinel.blend'; path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))

if __name__=='__main__': main()
