#!/usr/bin/env python3
"""SAM02 Bastion V3: fictional low-poly visual asset; Unity Y-up, +Z front.
Blender --background --factory-startup --python Tools/Blender/build_sam02_bastion.py
Writes deterministic Unity geometry JSON and editable .blend (when bpy is present).
"""
import json
from collections import Counter
from math import cos, sin, radians, pi
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Editor/SAM02Geometry.json'
PALETTE = {
    'Concrete': [146, 149, 146], 'Foundation': [109, 114, 110],
    'Hull': [71, 79, 69], 'Armor': [94, 105, 82],
    'Dark': [31, 37, 40], 'Steel': [158, 162, 148], 'Mark': [192, 66, 52],
}
PARTS = []

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

# V2: low octagonal foundation; the launcher, not the bunker, owns the silhouette.
slab('Foundation skirt','Fixed','Foundation',0,.22,15.4,14.1,2.45,top=.98)
slab('Sloping concrete bunker','Fixed','Concrete',.22,.91,15.09,13.82,2.4,top=.86)
slab('Roof coping','Fixed','Foundation',.91,1.02,13.12,12.0,2.10,top=.985)
slab('Inset roof deck','Fixed','Concrete',1.02,1.13,12.76,11.67,2.0)
# Service hardware becomes flush, subtle graphics rather than a bunker door.
box('Service hatch surround','Fixed','Foundation',0,1.15,-4.65,2.5,.10,1.15)
box('Service hatch','Fixed','Dark',0,1.22,-4.65,2.12,.08,.84)
box('Hatch center seam','Fixed','Steel',0,1.27,-4.65,.08,.04,.72)
for s in (-1,1):
    box('Roof vent '+str(s),'Fixed','Dark',s*4.65,1.18,-3.55,1.05,.10,1.55)
    for j in (-1,0,1):
        box('Vent rib '+str(s)+' '+str(j),'Fixed','Steel',s*4.65,1.25,-3.55+j*.40,.94,.06,.14)
# Exposed collar and compact angular carriage; shoulders stop short of the pods.
cylinder('Fixed bearing','Fixed','Dark',1.10,1.56,3.30,3.12)
cylinder('Rotating collar','Yaw','Steel',1.56,1.82,2.96,2.82)
slab('Turret skirt','Yaw','Dark',1.82,2.15,9.55,6.0,1.35,top=.94)
slab('Turret shoulder','Yaw','Hull',2.15,2.75,8.98,5.65,1.27,top=.77)
slab('Central saddle','Yaw','Armor',2.72,3.80,2.75,3.45,.70,top=.78)
# Open dark cradles give the two raised pods clear separation from the platform.
angle=radians(29)
def tilt(p):
    x,y,z=p
    return (x,4.24+y*cos(angle)+z*sin(angle),-.10-y*sin(angle)+z*cos(angle))
for s,group in [(-1,'Left'),(1,'Right')]:
    x=s*3.58
    slab(group+' cradle','Yaw','Dark',2.34,3.64,1.62,2.32,.45,x=x,z=-.1,top=.72)
    # Angular armor: wide shoulder tapering to a narrower beveled front face.
    def orient(p):
        a,b,c=p
        return tilt((a,c,b))
    profile=octagon(3.55,2.42,.68)
    loft(group+' armored pod',group,'Armor',[
        (-3.85,x,0,.70,.70),(-3.05,x,0,1,1),
        (1.65,x,0,1,1),(3.45,x,0,.76,.78),(3.88,x,0,.68,.69)],profile,orient)
    # End caps remain clear, with a single broad contrasting inset at the front.
    for end in (-1,1):
        z=-3.89 if end<0 else 3.92
        box(group+' end plate '+str(end),group,'Dark',x,0,z,1.97,1.23,.10,tilt)
        for q in (-1,1):
            box(group+' end insert '+str(end)+' '+str(q),group,'Steel',x+q*.45,0,z+end*.065,.76,.91,.06,tilt)
    slab_profile=octagon(1.48,4.65,.36)
    loft(group+' top spine',group,'Hull',[(1.16,x,-.57,1,1),(1.32,x,-.57,.72,.90)],slab_profile,tilt)
    # Short bold stripe on the flat shoulder rather than on the tapered nose.
    box(group+' faction stripe',group,'Mark',x,1.245,1.02,1.96,.09,.43,tilt)
    box(group+' side band',group,'Hull',x+s*1.79,0,-.65,.10,.72,3.2,tilt)
# Raised, angled radar panel. Broad dark face with a readable steel frame.
slab('Sensor pedestal','Yaw','Dark',3.70,5.14,1.54,1.62,.34,z=-1.40,top=.78)
angle_sensor=radians(19)
def sensor(p):
    x,y,z=p
    return (x,5.88+y*cos(angle_sensor)+z*sin(angle_sensor),-1.37-y*sin(angle_sensor)+z*cos(angle_sensor))
loft('Sensor head','Yaw','Hull',[(-.32,0,0,.88,.88),(.30,0,0,1,1)],octagon(2.94,1.95,.32),lambda p:sensor((p[0],p[2],p[1])))
box('Sensor face','Yaw','Dark',0,0,.34,2.32,1.45,.12,sensor)
box('Sensor horizontal bar','Yaw','Steel',0,-.49,.42,2.12,.13,.07,sensor)

def main():
    # Batch by stable group/material names for Unity; keep all solids separate in Blender.
    batches={}
    for name,group,mat,v,tris in PARTS:
        b=batches.setdefault((group,mat),[[],[]]); offset=len(b[0]); b[0].extend(v); b[1].extend(i+offset for t in tris for i in t)
    data={'palette':[{'name':k,'rgb':v} for k,v in PALETTE.items()], 'parts':[
        {'name':group+'_'+mat,'group':group,'material':mat,'vertices':[dict(zip('xyz',p)) for p in v],'triangles':t}
        for (group,mat),(v,t) in batches.items()]}
    OUT.parent.mkdir(parents=True,exist_ok=True)
    OUT.write_text(json.dumps(data,separators=(',',':'))+'\n',encoding='utf-8')
    vertices=[p for _,_,_,v,_ in PARTS for p in v]
    print('SAM02:',len(PARTS),'solids,',sum(len(t) for _,_,_,_,t in PARTS),'triangles,',len(batches),'mesh batches')
    print('Bounds:',[(min(p[i] for p in vertices),max(p[i] for p in vertices)) for i in range(3)])
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    materials={}; collections={}
    for name,rgb in PALETTE.items():
        mat=bpy.data.materials.new('SAM02_'+name); mat.diffuse_color=(*[x/255 for x in rgb],1); mat.use_nodes=True
        shader=mat.node_tree.nodes.get('Principled BSDF'); shader.inputs['Base Color'].default_value=mat.diffuse_color; shader.inputs['Roughness'].default_value=.9; materials[name]=mat
    for name in ('Fixed','Yaw','Left','Right'):
        col=bpy.data.collections.new(name); bpy.context.scene.collection.children.link(col); collections[name]=col
    for name,group,mat,v,tris in PARTS:
        mesh=bpy.data.meshes.new(name); mesh.from_pydata([(x,-z,y) for x,y,z in v],[],tris)
        assert not mesh.validate(verbose=True), name
        mesh.update(); mesh.materials.append(materials[mat]); obj=bpy.data.objects.new(name,mesh); collections[group].objects.link(obj)
        c=center(v); origin=(c[0],-c[2],c[1]); obj.location=origin
        for vert in mesh.vertices:
            for i in range(3): vert.co[i]-=origin[i]
    bpy.context.scene.unit_settings.system='METRIC'
    path=ROOT/'Resources/Blender/SAM02_Bastion.blend'; path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))

if __name__=='__main__': main()
