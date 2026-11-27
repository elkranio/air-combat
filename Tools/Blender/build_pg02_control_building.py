#!/usr/bin/env python3
"""PG02 V1: stylized low-poly control building. Unity +Y up, +Z front.
Plain Python regenerates Unity geometry; Blender also saves editable source.
"""
import json
from collections import Counter
from math import cos, sin, pi
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Editor/PG02Geometry.json'
PALETTE = {
    'Concrete': (137, 140, 130), 'Wall': (168, 173, 160),
    'Annex': (133, 146, 144), 'Roof': (77, 90, 93),
    'Trim': (104, 121, 124), 'Dark': (49, 62, 67),
    'Glass': (75, 102, 111), 'Accent': (205, 163, 70),
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


# Compact, fictional industrial architecture. Local footing only; the yard is separate.
box('Local footing','Foundation','Concrete',0,.13,0,22.6,.26,15.8)
# Offset main volume and a visibly lower side annex.
box('Main plinth','Shell','Trim',-2.6,.48,-.55,15.5,.44,12.2)
box('Main walls','Shell','Wall',-2.6,2.98,-.55,15.1,4.56,11.8)
box('Main roof lip','Roof','Trim',-2.6,5.35,-.55,15.7,.38,12.4)
box('Main roof','Roof','Roof',-2.6,5.57,-.55,15.15,.16,11.85)
# Four broad parapet edges keep the roof silhouette crisp, not a chunky cap.
for z in (-6.57,5.47):
    box('Long parapet','Roof','Wall',-2.6,5.75,z,15.65,.46,.32)
for x in (-10.27,5.07):
    box('Side parapet','Roof','Wall',x,5.75,-.55,.32,.46,11.75)
box('Annex base','Shell','Trim',7.7,.48,-1.65,5.8,.44,8.2)
box('Annex walls','Shell','Annex',7.7,2.13,-1.65,5.6,2.86,8.0)
box('Annex roof trim','Roof','Trim',7.7,3.72,-1.65,5.9,.32,8.3)
box('Annex roof','Roof','Roof',7.7,3.94,-1.65,5.58,.14,7.98)
# Asymmetric entrance: a deep blue-gray recess and thin projecting canopy.
box('Entry recess','Facade','Dark',1.9,1.95,5.39,3.35,3.3,.15)
box('Entry door','Facade','Trim',1.9,1.82,5.49,2.25,2.96,.13)
box('Door seam','Facade','Dark',1.9,1.82,5.57,.09,2.86,.045)
box('Entry step','Foundation','Concrete',1.9,.38,6.05,3.8,.24,1.3)
box('Canopy fascia','Facade','Trim',1.9,3.76,6.12,4.2,.32,1.8)
box('Canopy top','Facade','Roof',1.9,3.96,6.12,4.08,.10,1.68)
# One restrained recognition stripe, not a military badge.
box('Entrance marker','Facade','Accent',1.9,3.76,7.03,1.7,.14,.06)
# Technical glazing, not an office curtain wall. Broad recesses readable at game distance.
for x in (-7.75,-4.85,-1.95):
    box('Front window frame','Facade','Dark',x,3.25,5.39,2.4,1.42,.15)
    box('Front glazing','Facade','Glass',x,3.25,5.49,2.12,1.12,.10)
for x in (-7.7,-3.3,1.1):
    box('Rear vent recess','Facade','Dark',x,3.50,-6.49,2.55,.92,.14)
    for dy in (-.23,.23):
        box('Rear louver','Facade','Trim',x,3.50+dy,-6.58,2.24,.13,.08)
for z in (-3.9,1.15):
    box('Side window frame','Facade','Dark',-10.19,3.25,z,.13,1.42,2.4)
    box('Side glazing','Facade','Glass',-10.28,3.25,z,.10,1.12,2.12)
box('Annex service recess','Facade','Dark',7.7,1.85,2.39,3.7,2.65,.15)
box('Annex service door','Facade','Trim',7.7,1.85,2.49,3.32,2.33,.12)
for x in (6.4,9.0):
    box('Annex jamb','Facade','Dark',x,1.85,2.57,.11,2.22,.08)
# Broad roof features with deliberately different sizes/positions.
box('HVAC curb','Equipment','Trim',-6.0,5.83,-2.55,4.4,.35,2.8)
slab('HVAC housing','Equipment','Annex',-6,-2.55,6.0,6.95,4.05,2.45,.19,top=.96)
box('HVAC dark grille','Equipment','Dark',-6,6.98,-2.55,3.42,.10,1.88)
for z in (-3.12,-2.55,-1.98):
    box('HVAC grille slat','Equipment','Trim',-6,7.07,z,3.22,.10,.18)
box('Roof service hatch','Equipment','Trim',1.85,5.86,2.2,2.7,.45,2.0)
box('Hatch lid','Equipment','Annex',1.85,6.14,2.2,2.55,.16,1.85)
box('Annex ventilation base','Equipment','Trim',8.2,4.15,-3.35,2.8,.35,2.0)
box('Annex ventilation cap','Equipment','Dark',8.2,4.43,-3.35,2.55,.22,1.75)


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
    print('PG02:',len(PARTS),'closed solids;',sum(len(t) for *_,t in PARTS),'triangles;',len(batches),'batches; bounds',bounds)
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    materials={}; collections={}
    for name,rgb in PALETTE.items():
        mat=bpy.data.materials.new('PG02_'+name); mat.diffuse_color=(*[x/255 for x in rgb],1)
        mat.use_nodes=True; shader=mat.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Base Color'].default_value=mat.diffuse_color; shader.inputs['Roughness'].default_value=.9
        materials[name]=mat
    for group in ('Foundation','Shell','Roof','Facade','Equipment'):
        col=bpy.data.collections.new(group); bpy.context.scene.collection.children.link(col); collections[group]=col
    for name,group,mat,v,tris in PARTS:
        c=center(v); mesh=bpy.data.meshes.new(name)
        mesh.from_pydata([(x-c[0],-(z-c[2]),y-c[1]) for x,y,z in v],[],tris)
        assert not mesh.validate(verbose=True), name
        mesh.update(); mesh.materials.append(materials[mat])
        obj=bpy.data.objects.new(name,mesh); collections[group].objects.link(obj)
        obj.location=(c[0],-c[2],c[1])
    bpy.context.scene.unit_settings.system='METRIC'
    path=ROOT/'Resources/Blender/PG02_ControlBuilding.blend'; path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))


if __name__=='__main__': main()
