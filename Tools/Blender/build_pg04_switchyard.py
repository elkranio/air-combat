#!/usr/bin/env python3
"""PG04 V1: fictional low-poly industrial scenery, not functional equipment.
Unity +Y up. Plain Python emits JSON; Blender also saves editable source.
"""
import json
from collections import Counter
from math import sqrt
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Editor/PG04Geometry.json'
PALETTE = {
    'Concrete': (137, 140, 130), 'Steel': (95, 113, 118),
    'Dark': (48, 62, 66), 'Ceramic': (190, 197, 184),
    'Hull': (112, 130, 133), 'Accent': (205, 163, 70),
}
PARTS = []

def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def add(a,b): return tuple(x+y for x,y in zip(a,b))
def mul(a,s): return tuple(x*s for x in a)
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def unit(a): return mul(a,1/sqrt(dot(a,a)))
def center(v): return tuple(sum(p[i] for p in v)/len(v) for i in range(3))

def solid(name,group,mat,vertices,faces):
    v=[tuple(round(x,6) for x in p) for p in vertices]; c=center(v); tris=[]
    for face in faces:
        f=list(face); a,b,d=(v[i] for i in f[:3]); n=cross(sub(b,a),sub(d,a))
        assert dot(n,n)>1e-10,name
        if dot(n,sub(c,a))>0: f.reverse()
        tris.extend((f[0],f[i],f[i+1]) for i in range(1,len(f)-1))
    edges=Counter((a,b) for t in tris for a,b in zip(t,t[1:]+t[:1]))
    assert all(n==1 and edges[b,a]==1 for (a,b),n in edges.items()),name
    for a,b,d in tris:
        n=cross(sub(v[b],v[a]),sub(v[d],v[a]))
        assert dot(n,n)>1e-10 and dot(n,sub(c,v[a]))<0,name
    PARTS.append((name,group,mat,v,tris))

FACES=((0,1,3,2),(4,5,7,6),(0,2,6,4),(1,3,7,5),(0,1,5,4),(2,3,7,6))
def box(name,group,mat,x,y,z,w,h,d):
    solid(name,group,mat,[(x+sx*w/2,y+sy*h/2,z+sz*d/2) for sz in (-1,1) for sy in (-1,1) for sx in (-1,1)],FACES)

def beam(name,group,mat,a,b,w,d):
    # Chunky rectangular scenery beams, no fine lattice or coplanar plates.
    axis=unit(sub(b,a)); ref=(0,0,1) if abs(axis[2])<.9 else (1,0,0)
    u=unit(cross(axis,ref)); v=unit(cross(axis,u))
    points=[add(add(p,mul(u,sx*w/2)),mul(v,sy*d/2)) for p in (a,b) for sy in (-1,1) for sx in (-1,1)]
    solid(name,group,mat,points,FACES)

# Separate small footings leave grass visible between modules.
# An open, chunky portal silhouette is intentionally less massive than PG01/03.
for x in (-8.8,8.8):
    box('Pier footing','Foundation','Concrete',x,.16,2.5,3.1,.32,3.1)
    box('Pier shoe','Frame','Dark',x,.57,2.5,1.95,.50,1.95)
    box('Portal upright','Frame','Steel',x,4.55,2.5,1.22,7.55,1.32)
    # Short diagonal knee brace, deliberately bold at flight zoom.
    beam('Knee brace','Frame','Steel',(x,6.25,2.5),(x*.70,8.33,2.5),.72,.86)
box('Portal lintel','Frame','Dark',0,8.72,2.5,19.8,.95,1.72)
box('Lintel upper rail','Frame','Steel',0,9.37,2.5,19.15,.35,1.25)
# Three decorative hanging pods, stylized to read against the opening.
for x in (-5.7,0,5.7):
    box('Pod mount','Details','Hull',x,8.02,2.5,1.05,.55,.94)
    box('Pod body','Details','Ceramic',x,7.12,2.5,.77,1.28,.76)
    for y in (6.71,7.28):
        box('Pod collar','Details','Ceramic',x,y,2.5,1.30,.25,1.22)
    box('Pod tip','Details','Dark',x,6.39,2.5,.57,.35,.58)
# Two small generic utility cabinets offset in front of the portal.
for x,z,w,h,d in [(-5.3,-3.3,3.5,2.65,2.55),(3.8,-3.85,4.8,3.25,2.9)]:
    box('Cabinet footing','Foundation','Concrete',x,.11,z,w+.7,.22,d+.7)
    box('Cabinet body','Cabinets','Hull',x,.22+h/2,z,w,h,d)
    box('Cabinet top','Cabinets','Steel',x,.22+h+.13,z,w+.25,.26,d+.24)
    box('Front recess','Cabinets','Dark',x,.22+h*.53,z-d/2-.055,w*.72,h*.66,.18)
    box('Door plate','Cabinets','Steel',x,.22+h*.53,z-d/2-.18,w*.65,h*.59,.10)
    box('Recognition mark','Cabinets','Accent',x,.22+h*.77,z-d/2-.252,w*.40,.20,.08)


def main():
    batches={}
    for name,g,m,v,t in PARTS:
        b=batches.setdefault((g,m),[[],[]]); off=len(b[0]); b[0].extend(v); b[1].extend(i+off for face in t for i in face)
    data={'palette':[{'name':k,'rgb':v} for k,v in PALETTE.items()], 'parts':[
        {'name':g+'_'+m,'group':g,'material':m,'vertices':[dict(zip('xyz',p)) for p in v],'triangles':t}
        for (g,m),(v,t) in batches.items()]}
    OUT.parent.mkdir(parents=True,exist_ok=True)
    OUT.write_text(json.dumps(data,separators=(',',':'))+'\n',encoding='utf-8')
    vv=[p for *_,v,t in PARTS for p in v]
    print('PG04:',len(PARTS),'solids;',sum(len(t) for *_,t in PARTS),'triangles;',len(batches),'batches; bounds',[(min(p[i] for p in vv),max(p[i] for p in vv)) for i in range(3)])
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    mats={}; cols={}
    for name,rgb in PALETTE.items():
        mat=bpy.data.materials.new('PG04_'+name); mat.diffuse_color=(*[x/255 for x in rgb],1); mat.use_nodes=True
        shader=mat.node_tree.nodes.get('Principled BSDF'); shader.inputs['Base Color'].default_value=mat.diffuse_color; shader.inputs['Roughness'].default_value=.9; mats[name]=mat
    for g in dict.fromkeys(g for _,g,_,_,_ in PARTS):
        col=bpy.data.collections.new(g); bpy.context.scene.collection.children.link(col); cols[g]=col
    for name,g,m,v,t in PARTS:
        c=center(v); mesh=bpy.data.meshes.new(name); mesh.from_pydata([(x-c[0],-(z-c[2]),y-c[1]) for x,y,z in v],[],t)
        assert not mesh.validate(verbose=True),name
        mesh.update(); mesh.materials.append(mats[m]); obj=bpy.data.objects.new(name,mesh); cols[g].objects.link(obj); obj.location=(c[0],-c[2],c[1])
    bpy.context.scene.unit_settings.system='METRIC'
    path=ROOT/'Resources/Blender/PG04_Switchyard.blend'; path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))

if __name__=='__main__': main()
