#!/usr/bin/env python3
"""SAM02 Bastion V1: fictional low-poly visual asset; Unity Y-up, +Z front.
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
    'Concrete': [119, 121, 113], 'Foundation': [83, 86, 82],
    'Hull': [66, 72, 65], 'Armor': [88, 94, 81],
    'Dark': [39, 44, 44], 'Steel': [139, 144, 130], 'Mark': [174, 57, 44],
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

# Integrated fortified structure: no props, no oversized decorative ground pad.
slab('Foundation skirt','Fixed','Foundation',0,.44,19.2,17.6,3.1,top=.97)
slab('Sloping concrete bunker','Fixed','Concrete',.44,2.50,18.62,17.07,3.0,top=.77)
slab('Roof coping','Fixed','Foundation',2.50,2.75,14.65,13.45,2.4,top=.985)
slab('Inset roof deck','Fixed','Concrete',2.75,2.90,13.55,12.35,2.15,top=1)
# A recessed-looking service face and two large intake grilles, deliberately sparse.
box('Service hatch surround','Fixed','Foundation',0,1.38,-7.29,3.8,1.82,.20)
box('Service hatch','Fixed','Dark',0,1.38,-7.42,3.35,1.52,.13)
box('Hatch center seam','Fixed','Steel',0,1.38,-7.51,.09,1.42,.05)
for s in (-1,1):
    box('Roof vent '+str(s),'Fixed','Dark',s*5.15,3.04,-3.6,1.35,.28,2.0)
    for j in (-1,0,1):
        box('Vent rib '+str(s)+' '+str(j),'Fixed','Steel',s*5.15,3.21,-3.6+j*.52,1.18,.10,.19)
# Fixed bearing then a low broad traversing silhouette.
cylinder('Fixed bearing','Fixed','Dark',2.88,3.28,3.90,3.90)
cylinder('Rotating collar','Yaw','Steel',3.28,3.51,3.68,3.55)
slab('Turret skirt','Yaw','Dark',3.51,3.85,11.0,8.15,1.45,top=.95)
slab('Turret shoulder','Yaw','Hull',3.85,4.62,10.45,7.75,1.4,top=.88)
slab('Central saddle','Yaw','Armor',4.58,5.65,3.0,4.65,.62,top=.82)
# Two big separate fictional armored pods; sloped nose-up, no real mechanism.
angle=radians(24)
def tilt(p):
    x,y,z=p
    return (x,5.73+y*cos(angle)+z*sin(angle),-.18-y*sin(angle)+z*cos(angle))
for s,group in [(-1,'Left'),(1,'Right')]:
    x=s*3.75
    box(group+' cradle','Yaw','Dark',x,4.57,-.15,2.1,1.55,3.5)
    # Pod chamfers on all long edges. Axis is local Z, using the loft helper via a rotation.
    def orient(p):
        a,b,c=p
        return tilt((a,c,b))
    profile=octagon(3.55,2.35,.42)
    loft(group+' armored pod',group,'Armor',[(-3.75,x,0,.84,.85),(-3.18,x,0,1,1),(3.05,x,0,1,1),(3.78,x,0,.85,.85)],profile,orient)
    # Dark end plates sit just proud of the chamfered end. Broad panel graphic, not a tube mechanism.
    for end in (-1,1):
        z=end*3.81
        box(group+' end plate '+str(end),group,'Dark',x,0,z,2.69,1.63,.14,tilt)
        for q in (-1,1):
            box(group+' end insert '+str(end)+' '+str(q),group,'Steel',x+q*.63,0,z+end*.085,1.04,1.29,.07,tilt)
    box(group+' top spine',group,'Hull',x,1.20,-.12,1.20,.15,5.72,tilt)
    box(group+' faction stripe',group,'Mark',x,1.29,1.87,2.55,.10,.48,tilt)
    box(group+' side band',group,'Hull',x+s*1.79,0,-.5,.11,1.0,3.8,tilt)
# Small central sensor, behind the pod fronts; compact dark face framed in armor.
slab('Sensor pedestal','Yaw','Dark',5.48,6.10,1.75,1.65,.3,z=-1.35,top=.88)
slab('Sensor head','Yaw','Hull',6.10,7.15,2.45,1.45,.32,z=-1.35,top=.87)
box('Sensor face','Yaw','Dark',0,6.60,-.61,1.80,.72,.13)
box('Sensor horizontal bar','Yaw','Steel',0,6.62,-.52,1.53,.13,.08)

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
