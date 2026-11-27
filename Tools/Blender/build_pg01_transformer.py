#!/usr/bin/env python3
"""PG01 V1B: stylized low-poly power-grid prop. Unity +Y up, +Z front.
Plain Python regenerates Unity geometry; Blender also saves editable source.
"""
import json
from collections import Counter
from math import cos, sin, pi
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Editor/PG01Geometry.json'
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


# Readable independent prop: small local footing, not the future site slab.
slab('Foundation','Foundation','Concrete',0,0,0,.27,24,18,.55,top=.995)
slab('Equipment base','Foundation','Plinth',0,0,.27,.80,19.8,13.4,.65)
for x in (-5.6,5.6):
    box('Support skid','Foundation','Steel',x,1.02,0,1.25,.44,10.7)
# A heavy, slightly chamfered body with a pale top and a contrasting lower band.
slab('Lower body','Body','Steel',0,0,1.24,1.85,13.8,10.2,.75)
slab('Main body','Body','Hull',0,0,1.85,5.65,14.2,10.6,.8)
slab('Lid rim','Body','Dark',0,0,5.65,5.94,14.6,11.0,.8)
slab('Lid','Body','Lid',0,0,5.94,6.28,14.7,11.1,.8,top=.96)
# Large, deliberately exaggerated side silhouettes rather than tiny realistic details.
for s in (-1,1):
    box('Cooling bank frame','Cooling','Dark',s*8.45,3.25,0,2.5,3.7,8.9)
    for z in (-3.5,-1.75,0,1.75,3.5):
        slab('Cooling panel','Cooling','Steel',s*8.85,z,1.35,5.25,3.55,1.16,.20,top=.94)
        box('Cooling panel cap','Cooling','Hull',s*8.85,5.30,z,3.25,.18,.96)
    # A thin raised crosspiece breaks the comb-like silhouette.
    box('Cooling bank upper rail','Cooling','Steel',s*9.65,5.48,0,.48,.35,8.45)
    for z in (-4.15,4.15):
        box('Bank end cap','Cooling','Hull',s*8.45,3.35,z,2.8,3.8,.38)
# Three large stylized ceramic crowns: strong top-down recognition.
for x,z,h in [(-4.3,1.2,3.8),(0,1.2,3.8),(4.3,1.2,3.8)]:
    cylinder('Crown socket','Crown','Steel',x,z,6.28,6.65,1.10,1.02)
    cylinder('Crown core','Crown','Ceramic',x,z,6.65,6.65+h,.60,.39)
    cylinder('Crown collar','Crown','Steel',x,z,6.62,6.93,.73,.68)
    for j in range(4):
        y=6.98+j*.72; r=1.04-j*.07
        cylinder('Ceramic ring lower','Crown','Ceramic',x,z,y,y+.15,r,r*.92)
        cylinder('Ceramic ring upper','Crown','Ceramic',x,z,y+.15,y+.29,r*.92,.49)
    cylinder('Crown cap','Crown','Steel',x,z,6.65+h,6.92+h,.49,.49)
# Offset rounded rear assembly instead of a centered rectangular roof ornament.
# This is deliberately stylized visual massing, not a functional engineering model.
def rear_axis(p):
    x,y,z=p
    return (y+1.75,7.85-x,z-3.42)
for x in (-.25,3.9):
    box('Rear assembly saddle','Body','Steel',x,6.83,-3.42,.68,1.1,1.55)
cylinder('Rear assembly barrel','Body','Hull',0,0,-3.35,3.35,1.15,1.15,12,rear_axis)
cylinder('Rear assembly left shoulder','Body','Steel',0,0,-3.66,-3.35,.86,1.15,12,rear_axis)
cylinder('Rear assembly right shoulder','Body','Steel',0,0,3.35,3.66,1.15,.86,12,rear_axis)
for x in (-2.3,2.1):
    cylinder('Rear assembly band','Body','Steel',0,0,x-.16,x+.16,1.19,1.19,12,rear_axis)
# Offset service housing balances the rounded assembly without mirroring it.
slab('Service housing','Body','Steel',-5.15,-3.35,6.28,7.58,2.62,2.78,.23)
slab('Service housing lid','Body','Lid',-5.15,-3.35,7.58,7.8,2.82,2.98,.22)
box('Housing identification','Body','Accent',-5.15,7.83,-3.35,1.4,.06,.38)
box('Service face','Body','Dark',0,3.25,5.36,4.3,2.6,.18)
box('Service panel','Body','Steel',0,3.25,5.49,3.75,2.15,.14)
box('Service stripe','Body','Accent',0,4.03,5.58,2.85,.22,.07)
box('Deck seam','Body','Steel',0,6.33,-.35,12.8,.10,.14)


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
    print('PG01:',len(PARTS),'closed solids;',sum(len(t) for *_,t in PARTS),'triangles;',len(batches),'batches; bounds',bounds)
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    materials={}; collections={}
    for name,rgb in PALETTE.items():
        mat=bpy.data.materials.new('PG01_'+name); mat.diffuse_color=(*[x/255 for x in rgb],1)
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
    path=ROOT/'Resources/Blender/PG01_Transformer.blend'; path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))


if __name__=='__main__': main()
