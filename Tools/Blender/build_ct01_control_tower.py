#!/usr/bin/env python3
"""CT01: original compact arcade airfield control tower, +Y up, local -X faces runway.
Run with Python to rebuild Unity OBJs or with Blender --background --python to also save .blend.
"""
from pathlib import Path
from math import cos, sin, pi

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Buildings/Base/CT01_ControlTower'
COLORS = {'Concrete': (174/255,166/255,145/255), 'Trim': (80/255,91/255,91/255),
          'Glass': (52/255,95/255,105/255), 'Roof': (129/255,143/255,140/255),
          'Beacon': (224/255,90/255,58/255)}
MESH = {k: [] for k in COLORS}

def mean(p): return tuple(sum(v[i] for v in p)/len(p) for i in range(3))

def face(part, pts, inside):
    a,b,c=pts[:3]; u=[b[i]-a[i] for i in range(3)]; v=[c[i]-a[i] for i in range(3)]
    n=(u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0])
    if sum(n[i]*(inside[i]-a[i]) for i in range(3))>0: pts=pts[::-1]
    for i in range(1,len(pts)-1): MESH[part].append((pts[0],pts[i],pts[i+1]))

def ring(x,z,y,w,d,c=.0):
    a=w/2; b=d/2
    coords=[(-a,-b),(a,-b),(a,b),(-a,b)] if not c else [(-a+c,-b),(a-c,-b),(a,-b+c),(a,b-c),(a-c,b),(-a+c,b),(-a,b-c),(-a,-b+c)]
    return [(x+u,y,z+v) for u,v in coords]

def solid(part,lo,hi):
    inside=mean(lo+hi); face(part,lo,inside); face(part,hi,inside)
    for i in range(len(lo)):
        j=(i+1)%len(lo); face(part,[lo[i],lo[j],hi[j],hi[i]],inside)

def box(part,x,z,y,w,d,h,c=0):
    solid(part,ring(x,z,y,w,d,c),ring(x,z,y+h,w,d,c))

def taper(part, y0,y1,w0,d0,w1,d1,c0,c1):
    solid(part,ring(0,0,y0,w0,d0,c0),ring(0,0,y1,w1,d1,c1))

def cylinder(part,x,z,y,r,h,sides=8):
    lo=[(x+r*cos(2*pi*i/sides),y,z+r*sin(2*pi*i/sides)) for i in range(sides)]
    hi=[(a,y+h,b) for a,_,b in lo]; solid(part,lo,hi)

# Concrete plinth and a substantial two-storey base, not a lookout on stilts.
box('Trim',0,0,0,11.8,13.2,.42,.38)
box('Concrete',0,0,.42,10.8,12.2,8.4,.38)
# Broad horizontal belt and an outward-sloping cabin support.
box('Roof',0,0,4.55,11.05,12.45,.30,.4)
taper('Concrete',8.82,10.25,10.8,12.2,13.4,14.0,.38,.85)
box('Trim',0,0,10.25,13.6,14.2,.40,.9)
# Cabin: flared glazing with dark mullions. Glass panels and mullions share
# one exact exterior surface (no overlapping coplanar decals).
lo=ring(0,0,10.65,12.4,13.0,.85)
hi=ring(0,0,14.0,14.0,14.6,1.05)
inside=mean(lo+hi)
def lerp(a,b,t): return tuple(x+(y-x)*t for x,y in zip(a,b))
for i in range(len(lo)):
    j=(i+1)%len(lo); length=sum((a-b)**2 for a,b in zip(lo[i],lo[j]))**.5
    count=3 if length>7 else 1
    for k in range(count):
        a=k/count; b=(k+1)/count; gap=min(.025,.16/max(length,1))
        for part,u,v in [('Trim',a,a+gap),('Glass',a+gap,b-gap),('Trim',b-gap,b)]:
            face(part,[lerp(lo[i],lo[j],u),lerp(lo[i],lo[j],v),lerp(hi[i],hi[j],v),lerp(hi[i],hi[j],u)],inside)
face('Trim',lo,inside); face('Trim',hi,inside)
# Wide roof eaves, chamfered roof slab, small technical roof box.
box('Trim',0,0,14.0,15.2,15.8,.30,1.15)
box('Roof',0,0,14.30,14.6,15.2,.48,1.10)
box('Concrete',2.2,2.9,14.78,3.4,3.8,.80,.15)
box('Trim',2.2,2.9,15.58,3.6,4.0,.18,.15)
# Door and sparse windows on the two readable faces, slightly proud of walls.
box('Trim',-5.43,2.5,.42,.17,1.95,2.65)
box('Glass',-5.46,-2.9,2.55,.16,3.15,1.15)
box('Glass',-5.46,-2.9,5.65,.16,3.15,1.15)
box('Glass',-1.8,-6.13,2.55,3.15,.16,1.15)
box('Glass',-1.8,-6.13,5.65,3.15,.16,1.15)
box('Glass',2.7,-6.13,5.65,2.45,.16,1.15)
# Antennas and a visible small red aviation beacon, decorative only.
for x,z,h in [(-3.9,3.5,2.95),(3.7,-3.8,2.05)]:
    cylinder('Trim',x,z,14.78,.40,.25)
    cylinder('Trim',x,z,15.03,.12,h-.25)
    box('Trim',x,z,14.78+h-.52,1.5,.13,.13)
cylinder('Trim',-3.7,-3.6,14.78,.46,.30)
cylinder('Beacon',-3.7,-3.6,15.08,.36,.42)

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    for name,tris in MESH.items():
        lines=['# CT01 control tower; +Y up',f'o CT01_{name}','s off']; verts={}; faces=[]
        for tri in tris:
            ids=[]
            for p in tri:
                p=tuple(round(x,6) for x in p)
                if p not in verts: verts[p]=len(verts)+1
                ids.append(verts[p])
            faces.append('f '+' '.join(map(str,ids)))
        lines.extend('v '+' '.join(f'{v:.6f}' for v in p) for p in verts)
        lines.extend(faces)
        (OUT/f'CT01_{name}.obj').write_text('\n'.join(lines)+'\n')
        print(f'{name}: {len(tris)} triangles')
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for name,tris in MESH.items():
        mesh=bpy.data.meshes.new('CT01_'+name)
        verts=[(x,-z,y) for tri in tris for x,y,z in tri]
        mesh.from_pydata(verts,[],[tuple(range(i,i+3)) for i in range(0,len(verts),3)])
        mesh.update(); obj=bpy.data.objects.new(mesh.name,mesh); bpy.context.collection.objects.link(obj)
        mat=bpy.data.materials.new('CT01_'+name); mat.diffuse_color=(*COLORS[name],1); mesh.materials.append(mat)
    path=ROOT/'Resources/Blender/CT01_ControlTower.blend'; path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))

if __name__=='__main__': main()
