#!/usr/bin/env python3
"""CQ01 crew quarters. Generate OBJ assets, or run inside Blender to save the editable .blend too.
Model coordinates: +Y up; door faces -X before Unity OBJ conversion.
"""
from collections import Counter
from pathlib import Path
from math import sqrt

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Buildings/Base/CQ01_CrewQuarters'
COLORS = {'Walls': (0.65, 0.63, 0.55), 'Roof': (0.33, 0.44, 0.46),
          'Trim': (0.34, 0.37, 0.35), 'Glass': (0.18, 0.26, 0.28),
          'Door': (0.20, 0.25, 0.26), 'Step': (0.65, 0.63, 0.55)}
PARTS = {p: [] for p in COLORS}

def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])

def solid(part, vertices, polygons):
    """Each solid is convex. Orient a *copy* of each face; never mutate source rings."""
    center = tuple(sum(v[i] for v in vertices)/len(vertices) for i in range(3))
    triangles = []
    for face in polygons:
        face = tuple(face)
        a,b,c = (vertices[i] for i in face[:3])
        n = cross(sub(b,a),sub(c,a))
        assert dot(n,n)>1e-10
        if dot(n,sub(center,a))>0: face=face[::-1]
        for i in range(1,len(face)-1): triangles.append((face[0],face[i],face[i+1]))
    # Check closed oriented manifold and nonzero areas before exporting.
    edges = Counter((a,b) for tri in triangles for a,b in zip(tri,tri[1:]+tri[:1]))
    assert all(n==1 and edges[b,a]==1 for (a,b),n in edges.items())
    volume=0
    for i,j,k in triangles:
        a,b,c=vertices[i],vertices[j],vertices[k]
        normal=cross(sub(b,a),sub(c,a))
        assert dot(normal,normal)>1e-10 and dot(normal,sub(center,a))<0
        volume += dot(a,cross(b,c))/6
    assert volume>1e-8
    PARTS[part].append((tuple(vertices),tuple(triangles)))

def box(part,x,y,z,w,h,d):
    v=[(x+sx*w/2,y+sy*h/2,z+sz*d/2) for sz in (-1,1) for sy in (-1,1) for sx in (-1,1)]
    solid(part,v,[(0,1,3,2),(4,5,7,6),(0,2,6,4),(1,3,7,5),(0,1,5,4),(2,3,7,6)])

def prism_x(part,x0,x1,yz):
    # Identical immutable cross-sections at both ends, with matching indices.
    n=len(yz)
    v=[(x,y,z) for x in (x0,x1) for y,z in yz]
    faces=[tuple(range(n)),tuple(range(n,2*n))]
    faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
    solid(part,v,faces)

# Compact 20 x 16 single-storey building, with a shallow, closed gabled roof.
box('Walls',0,2,0,20,4,16)
box('Trim',0,.15,0,20.3,.30,16.3)
# Roof cross-section has five distinct convex corners; every side is planar.
prism_x('Roof',-10.55,10.55,[(4,-8.55),(4.22,-8.55),(6,0),(4.22,8.55),(4,8.55)])
# Broad entry facing the apron, with a low concrete landing extending to it.
box('Door',-10.055,1.36,0,.16,2.72,2.4)
box('Step',-11.65,.11,0,3.8,.22,5.8)
# Windows on both long walls, with two front windows framing the door.
for z in (-8.035,8.035):
    for x in (-6.6,-2.2,2.2,6.6):
        box('Glass',x,2.35,z,2.35,1.16,.12)
for z in (-4.7,4.7):
    box('Glass',-10.035,2.35,z,.12,1.16,2.35)
    box('Glass',10.035,2.35,z,.12,1.16,2.35)


def mesh_data(solids):
    v=[]; f=[]
    for points,triangles in solids:
        offset=len(v);v.extend(points);f.extend(tuple(i+offset for i in t) for t in triangles)
    return v,f


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    for name,solids in PARTS.items():
        v,f=mesh_data(solids)
        lines=['# CQ01 crew quarters; +Y up',f'o CQ01_{name}','s off']
        lines.extend('v '+' '.join(f'{x:.6f}' for x in p) for p in v)
        lines.extend('f '+' '.join(str(i+1) for i in t) for t in f)
        (OUT/f'CQ01_{name}.obj').write_text('\n'.join(lines)+'\n')
        print(f'{name}: {len(f)} triangles')
    try: import bpy
    except ImportError: return
    # Intended for Blender --background --factory-startup --python <this file>.
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    for name,solids in PARTS.items():
        v,f=mesh_data(solids)
        mesh=bpy.data.meshes.new('CQ01_'+name)
        mesh.from_pydata([(x,-z,y) for x,y,z in v],[],f)
        assert not mesh.validate(verbose=True), 'Unexpected invalid Blender mesh'
        mesh.update()
        obj=bpy.data.objects.new(mesh.name,mesh);bpy.context.collection.objects.link(obj)
        mat=bpy.data.materials.new('CQ01_'+name);mat.diffuse_color=(*COLORS[name],1)
        mesh.materials.append(mat)
        for poly in mesh.polygons: poly.use_smooth=False
    path=ROOT/'Resources/Blender/CQ01_CrewQuarters.blend';path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))

if __name__=='__main__': main()
