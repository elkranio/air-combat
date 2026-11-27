#!/usr/bin/env python3
"""PG03 V1: stylized low-poly power-grid prop. Unity +Y up, +Z front.
Plain Python regenerates Unity geometry; Blender also saves editable source.
"""
import json
from collections import Counter
from math import cos, sin, pi
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Editor/PG03Geometry.json'
PALETTE = {
    'Concrete': (137, 140, 130), 'Plinth': (90, 98, 94),
    'Hull': (105, 125, 131), 'Lid': (133, 151, 155),
    'Steel': (70, 88, 96), 'Dark': (48, 62, 66),
    'Ceramic': (190, 197, 184), 'Accent': (205, 163, 70),
}
PARTS = []


def sub(a, b): return tuple(x-y for x, y in zip(a, b))
def dot(a, b): return sum(x*y for x, y in zip(a, b))
def cross(a, b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def center(v): return tuple(sum(p[i] for p in v)/len(v) for i in range(3))


def solid(name, group, mat, vertices, faces):
    v = [tuple(round(x, 6) for x in p) for p in vertices]
    c = center(v); tris = []
    for face in faces:
        f = list(face); a, b, d = (v[i] for i in f[:3])
        n = cross(sub(b, a), sub(d, a))
        assert dot(n, n) > 1e-10, name
        if dot(n, sub(c, a)) > 0: f.reverse()
        tris.extend((f[0], f[i], f[i+1]) for i in range(1, len(f)-1))
    edges = Counter((a,b) for t in tris for a,b in zip(t,t[1:]+t[:1]))
    assert all(n == 1 and edges[b,a] == 1 for (a,b), n in edges.items()), name
    for a,b,d in tris:
        n = cross(sub(v[b],v[a]),sub(v[d],v[a]))
        assert dot(n,n)>1e-10 and dot(n,sub(c,v[a]))<0, name
    PARTS.append((name, group, mat, v, tris))


def loft(name, group, mat, sections, profile, transform=lambda p:p):
    n = len(profile)
    v = [transform((x+u*wx,y,z+q*wz)) for y,x,z,wx,wz in sections for u,q in profile]
    faces = [list(range(n)), list(range(len(v)-n,len(v)))]
    for k in range(len(sections)-1):
        for i in range(n):
            j = (i+1)%n; a = k*n+i; b = k*n+j
            faces.append([a,b,b+n,a+n])
    solid(name, group, mat, v, faces)


def slab(name, group, mat, x, z, y0, y1, w, d, cut, top=1):
    a,b = w/2,d/2
    p = [(a-cut,b),(a,b-cut),(a,-b+cut),(a-cut,-b),(-a+cut,-b),(-a,-b+cut),(-a,b-cut),(-a+cut,b)]
    loft(name,group,mat,[(y0,x,z,1,1),(y1,x,z,top,top)],p)


def box(name, group, mat, x,y,z,w,h,d):
    v = [(x+sx*w/2,y+sy*h/2,z+sz*d/2) for sz in (-1,1) for sy in (-1,1) for sx in (-1,1)]
    solid(name,group,mat,v,[(0,1,3,2),(4,5,7,6),(0,2,6,4),(1,3,7,5),(0,1,5,4),(2,3,7,6)])


def cylinder(name, group, mat, x,z,y0,y1,r0,r1,sides=12,transform=lambda p:p):
    profile = [(cos(2*pi*i/sides),sin(2*pi*i/sides)) for i in range(sides)]
    loft(name,group,mat,[(y0,x,z,r0,r0),(y1,x,z,r1,r1)],profile,transform)


# A second fictional model in the same visual family: long tank, split cooler
# silhouettes, two different heights of ceramic details. No functional model.
slab('Foundation','Foundation','Concrete',0,0,0,.27,28,20,.65,top=.995)
slab('Equipment base','Foundation','Plinth',0,0,.27,.8,24.2,16.2,.75)
for x in (-6.5,6.5):
    box('Support skid','Foundation','Steel',x,1.02,0,1.3,.44,11.3)
slab('Lower body','Body','Steel',0,0,1.24,1.82,17.4,10.7,.75)
slab('Main body','Body','Hull',0,0,1.82,5.38,17.8,11.2,.9)
slab('Lid rim','Body','Dark',0,0,5.38,5.66,18.2,11.6,.85)
slab('Lid','Body','Lid',0,0,5.66,6.0,18.3,11.7,.85,top=.96)
# Distinct offset groups, with a visible gap on the right flank.
for s, centers in [(-1, (0,)), (1, (-3.35, 3.35))]:
    for z in centers:
        length = 9.6 if s < 0 else 4.35
        box('Cooling housing','Cooling','Dark',s*10.25,3.1,z,2.45,3.5,length)
        positions = (-3.9,-1.95,0,1.95,3.9) if s < 0 else (-1.12,1.12)
        for dz in positions:
            slab('Cooling panel','Cooling','Steel',s*10.7,z+dz,1.35,4.98,3.6,1.35,.22,top=.94)
            box('Cooling panel cap','Cooling','Hull',s*10.7,5.05,z+dz,3.25,.18,1.12)
        box('Upper rail','Cooling','Steel',s*11.6,5.25,z,.48,.32,length-.3)
        for dz in (-length/2+.18,length/2-.18):
            box('Bank end cap','Cooling','Hull',s*10.3,3.16,z+dz,2.75,3.5,.36)
# Wider crown spacing and a shallow second row give the roof a distinct rhythm.
for x in (-5.6,0,5.6):
    for z,h,r,n in [(2.0,3.55,1.08,4),(-1.1,1.55,.76,2)]:
        cylinder('Crown socket','Crown','Steel',x,z,6.0,6.3,r*1.05,r)
        cylinder('Crown core','Crown','Ceramic',x,z,6.3,6.3+h,r*.56,r*.37)
        for j in range(n):
            y=6.5+j*(h-.35)/n; radius=r*(1-.065*j)
            cylinder('Ceramic ring lower','Crown','Ceramic',x,z,y,y+.14,radius,radius*.9)
            cylinder('Ceramic ring upper','Crown','Ceramic',x,z,y+.14,y+.28,radius*.9,r*.42)
        cylinder('Crown cap','Crown','Steel',x,z,6.3+h,6.55+h,r*.46,r*.46)
# Rear rounded mass sits beside, not over, the lid. Stylized visual only.
def rear_axis(p):
    x,y,z=p
    return (y,5.35-x,z-6.85)
for x in (-4.2,4.2):
    box('Rear assembly saddle','Body','Steel',x,3.8,-6.4,.8,1.4,2.1)
cylinder('Rear assembly barrel','Body','Hull',0,0,-5.65,5.65,1.1,1.1,12,rear_axis)
cylinder('Rear assembly left shoulder','Body','Steel',0,0,-6,-5.65,.76,1.1,12,rear_axis)
cylinder('Rear assembly right shoulder','Body','Steel',0,0,5.65,6,1.1,.76,12,rear_axis)
for x in (-3.8,3.8):
    cylinder('Rear assembly band','Body','Steel',0,0,x-.16,x+.16,1.15,1.15,12,rear_axis)
slab('Roof service housing','Body','Steel',-5.5,-4.3,6,7.1,3.25,1.85,.2)
slab('Roof service lid','Body','Lid',-5.5,-4.3,7.1,7.3,3.35,1.95,.18)
box('Housing identification','Body','Accent',-5.5,7.33,-4.3,1.6,.06,.36)
box('Service face','Body','Dark',2.8,3.2,5.66,4.3,2.5,.17)
box('Service panel','Body','Steel',2.8,3.2,5.77,3.72,2.1,.14)
box('Service stripe','Body','Accent',2.8,3.95,5.85,2.85,.22,.07)
box('Deck seam','Body','Steel',0,6.06,0,15.4,.12,.16)

def main():
    batches = {}
    for name,group,mat,v,tris in PARTS:
        b=batches.setdefault((group,mat),[[],[]]); off=len(b[0])
        b[0].extend(v); b[1].extend(i+off for t in tris for i in t)
    data={'palette':[{'name':k,'rgb':v} for k,v in PALETTE.items()], 'parts':[
        {'name':g+'_'+m,'group':g,'material':m,'vertices':[dict(zip('xyz',p)) for p in v],'triangles':t}
        for (g,m),(v,t) in batches.items()]}
    OUT.parent.mkdir(parents=True,exist_ok=True)
    OUT.write_text(json.dumps(data,separators=(',',':'))+'\n',encoding='utf-8')
    points=[p for _,_,_,v,_ in PARTS for p in v]
    bounds=[(min(p[i] for p in points),max(p[i] for p in points)) for i in range(3)]
    print('PG03:',len(PARTS),'closed solids;',sum(len(t) for *_,t in PARTS),'triangles;',len(batches),'batches; bounds',bounds)
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    materials={}; collections={}
    for name,rgb in PALETTE.items():
        mat=bpy.data.materials.new('PG03_'+name); mat.diffuse_color=(*[x/255 for x in rgb],1)
        mat.use_nodes=True; shader=mat.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Base Color'].default_value=mat.diffuse_color; shader.inputs['Roughness'].default_value=.9
        materials[name]=mat
    for group in ('Foundation','Body','Cooling','Crown'):
        col=bpy.data.collections.new(group); bpy.context.scene.collection.children.link(col); collections[group]=col
    for name,group,mat,v,tris in PARTS:
        c=center(v); mesh=bpy.data.meshes.new(name)
        mesh.from_pydata([(x-c[0],-(z-c[2]),y-c[1]) for x,y,z in v],[],tris)
        assert not mesh.validate(verbose=True), name
        mesh.update(); mesh.materials.append(materials[mat])
        obj=bpy.data.objects.new(name,mesh); collections[group].objects.link(obj)
        obj.location=(c[0],-c[2],c[1])
    bpy.context.scene.unit_settings.system='METRIC'
    path=ROOT/'Resources/Blender/PG03_TransformerLong.blend'; path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))


if __name__=='__main__': main()
