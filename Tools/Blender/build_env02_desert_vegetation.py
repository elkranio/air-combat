"""ENV02: five stylized desert plants. Python exports Unity Y-up OBJs;
Blender --background --factory-startup --python this_file also saves the library.
All geometry is original, flat shaded, with ground-level pivots.
"""
from pathlib import Path
from math import sin, cos, pi, sqrt
from random import Random
from collections import Counter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Environment/Mission01_FirstSortie/ENV02_DesertVegetation'
PALETTE = {'Bark': (112, 91, 66), 'Leaf': (115, 120, 70), 'Light': (141, 139, 83)}

def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def add(a,b): return tuple(x+y for x,y in zip(a,b))
def mul(a,s): return tuple(x*s for x in a)
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def unit(v): return mul(v,1/sqrt(dot(v,v)))
def normal(v,f): return unit(cross(sub(v[f[1]],v[f[0]]),sub(v[f[2]],v[f[0]])))

class Geometry:
    def __init__(self): self.v=[]; self.f=[]
    def solid(self, v, faces):
        # Propagate consistent winding across shared edges, then orient the
        # closed shell by signed volume. A centroid test alone is unreliable
        # on an irregular nonplanar crown cap.
        faces=[tuple(f) for f in faces]; edges={}
        for i,f in enumerate(faces):
            for a,b in zip(f,f[1:]+f[:1]):
                edges.setdefault(tuple(sorted((a,b))),[]).append((i,a<b))
        neighbors=[[] for _ in faces]
        for pair in edges.values():
            assert len(pair)==2
            (i,d),(j,e)=pair
            neighbors[i].append((j,d==e));neighbors[j].append((i,d==e))
        flip={0:False}; todo=[0]
        while todo:
            i=todo.pop()
            for j,same in neighbors[i]:
                expected=flip[i]^same
                if j in flip: assert flip[j]==expected
                else: flip[j]=expected;todo.append(j)
        assert len(flip)==len(faces)
        faces=[f[::-1] if flip[i] else f for i,f in enumerate(faces)]
        volume=sum(dot(v[a],cross(v[b],v[c])) for a,b,c in faces)
        assert abs(volume)>1e-8
        if volume<0: faces=[f[::-1] for f in faces]
        n=len(self.v); self.v.extend(v)
        self.f.extend(tuple(i+n for i in f) for f in faces)
    def branch(self, a,b,r0,r1, sides=6):
        axis=unit(sub(b,a)); u=unit(cross(axis,(0,0,1) if abs(axis[2])<.9 else (1,0,0))); w=cross(axis,u)
        v=[add(p,mul(add(mul(u,cos(2*pi*i/sides)),mul(w,sin(2*pi*i/sides))),r)) for p,r in [(a,r0),(b,r1)] for i in range(sides)]
        f=[]
        for i in range(sides):
            j=(i+1)%sides; f.extend([(i,j,sides+j),(i,sides+j,sides+i)])
        for i in range(1,sides-1): f.extend([(0,i,i+1),(sides,sides+i,sides+i+1)])
        self.solid(v,f)
    def crown(self, center, size, rng, sides=8, yaw=0):
        # Three irregular rings, low broad top and tapered underside, not spheres.
        rx,ry,rz=size; v=[]; f=[]
        angles=[2*pi*i/sides+rng.uniform(-.10,.10) for i in range(sides)]
        radii=[rng.uniform(.82,1.13) for _ in angles]
        for layer,(h,scale) in enumerate([(-.58,.58),(.02,1),(.64,.64)]):
            for i,a in enumerate(angles):
                r=radii[i]*scale*rng.uniform(.90,1.08); x=rx*r*cos(a); z=rz*r*sin(a)
                v.append(add(center,(x*cos(yaw)-z*sin(yaw),ry*(h+rng.uniform(-.10,.10)),x*sin(yaw)+z*cos(yaw))))
        for k in range(2):
            for i in range(sides):
                j=(i+1)%sides; a=k*sides+i; b=k*sides+j; c=(k+1)*sides+j; d=(k+1)*sides+i
                f.extend([(a,b,d),(b,c,d)] if (i+k)%2 else [(a,b,c),(a,c,d)])
        # Compact cap triangulation, no central tip/fan.
        for k in (0,2*sides):
            lo,hi=0,sides-1
            while hi-lo>1:
                f.append((k+lo,k+lo+1,k+hi));lo+=1
                if hi-lo>1:f.append((k+lo,k+hi-1,k+hi));hi-=1
        self.solid(v,f)

# Each branch is (start, end, basal radius, tip radius). Crown entries are
# (center, radii, yaw); shades assigned by lobe, never randomly per triangle.
def plant(name, branches, crowns, seed):
    g={k:Geometry() for k in PALETTE}; rng=Random(seed)
    for a,b,r,s in branches:g['Bark'].branch(a,b,r,s)
    for i,(p,sz,yaw) in enumerate(crowns):g['Light' if i%4==2 else 'Leaf'].crown(p,sz,rng,8,yaw)
    return name,g

def models():
    return [
      plant('ENV02_A_Umbrella', [((0,-.12,0),(.4,4.5,.1),.61,.37),((.4,4.5,.1),(-.5,7.8,.4),.40,.22),
        ((-.2,5.9,.2),(-4.1,9.1,1),.28,.11),((-.3,7.2,.3),(3.8,9.4,-.6),.27,.11),((-.5,7.8,.4),(-.8,10.1,-2.4),.21,.08),((.4,7,0),(1.4,9,3.5),.18,.07)],
        [((-4.3,9.5,1),(4.8,2.15,3.3),.2),((2.8,10,-.8),(5,2.2,3.7),-.4),((-.5,10.8,-2.4),(4.1,1.9,3.4),.5),((1.1,9.7,3.5),(3.6,1.8,3),.1)],71),
      plant('ENV02_B_WindBent', [((0,-.12,0),(-.6,3.2,0),.55,.37),((-.6,3.2,0),(1.2,6.1,.4),.39,.25),((1.2,6.1,.4),(4.5,8.8,1),.27,.12),((1.2,6.1,.4),(1.2,8.7,-2.5),.22,.09),((.1,4.5,.1),(-3.2,6.8,.6),.23,.09),((3.3,8,.8),(6.4,9.1,2.7),.15,.06)],
        [((-3.1,7.5,.4),(3.15,1.85,2.6),.1),((1.8,9.3,-2.2),(3.8,2,2.7),-.3),((5,9.7,1),(4.5,2.1,3.25),.35),((6.8,9.6,3),(2.7,1.5,2.2),.3)],133),
      plant('ENV02_C_Forked', [((0,-.12,0),(.1,3.6,0),.48,.32),((.1,3.6,0),(-1.9,6.8,-.6),.31,.13),((.1,3.6,0),(2.1,7.3,.9),.28,.14),((-1.9,6.8,-.6),(-3,9.3,-1.1),.15,.07),((2.1,7.3,.9),(3.2,10.7,1.5),.16,.07),((1.3,6,.4),(1.4,8.7,-2.1),.15,.06)],
        [((-3.2,9.8,-1.2),(3.3,2.1,2.8),.4),((3.2,11.3,1.5),(3.3,2.4,2.8),-.25),((1.3,9.1,-2.2),(2.55,1.8,2.25),.1)],219),
      plant('ENV02_D_SaltBush', [((0,-.1,0),(-1.2,1.4,.4),.18,.065),((0,-.1,0),(1.1,1.8,-.5),.19,.06),((0,-.1,0),(.2,1.1,1),.15,.06)],
        [((-1.25,1.5,.3),(1.85,1.05,1.55),.2),((1.1,1.9,-.5),(1.65,1.25,1.45),-.4),((.15,1.25,1.2),(1.5,.9,1.45),.2)],311),
      plant('ENV02_E_Scrub', [((0,-.1,0),(-1.1,1.2,-.8),.14,.05),((0,-.1,0),(1.5,1.2,.3),.15,.05),((0,-.1,0),(.3,1.5,-.4),.18,.06)],
        [((-1.35,1.25,-.8),(1.8,.85,1.15),-.2),((1.45,1.2,.3),(1.85,.8,1.3),.15),((.2,1.65,-.4),(1.6,1.1,1.3),.4)],409),
    ]

def validate(g):
    edges=Counter()
    for f in g.f:
        assert len(set(f))==3 and dot(cross(sub(g.v[f[1]],g.v[f[0]]),sub(g.v[f[2]],g.v[f[0]])),cross(sub(g.v[f[1]],g.v[f[0]]),sub(g.v[f[2]],g.v[f[0]])))>1e-10
        for a,b in zip(f,f[1:]+f[:1]): edges[tuple(sorted((a,b)))]+=1
    assert all(n==2 for n in edges.values())

def main():
    OUT.mkdir(parents=True,exist_ok=True); library=models()
    for name,groups in library:
        for kind,g in groups.items():
            validate(g)
            obj=name+'_'+kind
            lines=['# ENV02 original desert vegetation; Unity Y-up','o '+obj,'s off']
            lines+=['v %.6f %.6f %.6f'%v for v in g.v]
            lines+=['vn %.8f %.8f %.8f'%normal(g.v,f) for f in g.f]
            lines+=['f '+' '.join(f'{j+1}//{i}' for j in f) for i,f in enumerate(g.f,1)]
            (OUT/(obj+'.obj')).write_text('\n'.join(lines)+'\n',encoding='utf-8')
        print(name, sum(len(g.f) for g in groups.values()),'triangles')
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    def linear(c):
        s=c/255; return s/12.92 if s<=.04045 else ((s+.055)/1.055)**2.4
    mats={}
    for k,rgb in PALETTE.items():
        m=bpy.data.materials.new('ENV02_'+k);m.diffuse_color=tuple(linear(c) for c in rgb)+(1,);mats[k]=m
    for i,(name,groups) in enumerate(library):
        for kind,g in groups.items():
            mesh=bpy.data.meshes.new(name+'_'+kind);mesh.from_pydata([(x,-z,y) for x,y,z in g.v],[],g.f);mesh.update();mesh.materials.append(mats[kind]);obj=bpy.data.objects.new(mesh.name,mesh);bpy.context.collection.objects.link(obj);obj.location=(i*25,0,0)
    dest=ROOT/'Resources/Blender/ENV02_DesertVegetation.blend';dest.parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(dest))

if __name__=='__main__': main()
