"""ENV03 decorative sandstone. Run Python for OBJ exports or Blender --background
--factory-startup --python this_file to also save an editable library.
Unity Y-up, ground pivot, flat faces, no external dependencies.
"""
from pathlib import Path
from math import sin, cos, pi, sqrt
from random import Random
from collections import Counter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Environment/Mission01_FirstSortie/ENV03_DesertStones'
PALETTE = [(177,149,119), (164,136,110)]

def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def normal(v,f):
    n=cross(sub(v[f[1]],v[f[0]]),sub(v[f[2]],v[f[0]])); length=sqrt(dot(n,n))
    assert length>1e-6
    return tuple(x/length for x in n)

def stone(center, radii, height, yaw, rng, sides=7, slab=False):
    # Three faceted rings: buried foot, broken shoulder, sloping broad cap.
    # Cap has alternating diagonals rather than a common central tip.
    rx,rz=radii; v=[]; f=[]
    angles=[2*pi*i/sides+rng.uniform(-.08,.08) for i in range(sides)]
    shape=[rng.uniform(.87,1.12) for _ in angles]
    for layer,(level,scale) in enumerate([(-.12,.84),(.30,1.0),(.88,.72 if slab else .49)]):
        for i,a in enumerate(angles):
            r=shape[i]*scale*rng.uniform(.94,1.06)
            x=rx*r*cos(a)+(layer/2)*rx*.12; z=rz*r*sin(a)-(layer/2)*rz*.08
            y=level if layer==0 else height*(level+rng.uniform(-.12,.12)+(.09*cos(a) if layer==2 else 0))
            v.append((center[0]+x*cos(yaw)-z*sin(yaw),y,center[1]+x*sin(yaw)+z*cos(yaw)))
    for k in range(2):
        for i in range(sides):
            j=(i+1)%sides; a=k*sides+i; b=k*sides+j; c=(k+1)*sides+j; d=(k+1)*sides+i
            f.extend([(a,b,d),(b,c,d)] if (i+k)%2 else [(a,b,c),(a,c,d)])
    for k in (0,2*sides):
        lo,hi=0,sides-1
        while hi-lo>1:
            f.append((k+lo,k+lo+1,k+hi));lo+=1
            if hi-lo>1:f.append((k+lo,k+hi-1,k+hi));hi-=1
    # Orient closed connected shell via shared edges and signed volume.
    edges={}
    for i,face in enumerate(f):
        for a,b in zip(face,face[1:]+face[:1]): edges.setdefault(tuple(sorted((a,b))),[]).append((i,a<b))
    neighbors=[[] for _ in f]
    for pair in edges.values():
        assert len(pair)==2
        (i,d),(j,e)=pair;neighbors[i].append((j,d==e));neighbors[j].append((i,d==e))
    flips={0:False};todo=[0]
    while todo:
        i=todo.pop()
        for j,same in neighbors[i]:
            expected=flips[i]^same
            if j in flips: assert flips[j]==expected
            else: flips[j]=expected;todo.append(j)
    assert len(flips)==len(f)
    f=[face[::-1] if flips[i] else face for i,face in enumerate(f)]
    vol=sum(dot(v[a],cross(v[b],v[c])) for a,b,c in f)
    assert abs(vol)>1e-6
    if vol<0:f=[face[::-1] for face in f]
    for face in f: normal(v,face)
    return v,f

# (center XZ, half-width/depth, height, yaw radians, sides, slab)
SPECS=[
 ('ENV03_A_ExposedSlab',0,[((0,0),(11,5),2.6,.2,7,True),((-9,3),(4,2.5),1.3,-.3,6,True),((9,-4),(3.1,2.4),1.2,.6,6,True)]),
 ('ENV03_B_RubbleScatter',1,[((-5,-2),(3.1,2.5),2.8,.1,6,False),((1,1),(4,2.3),3.4,.6,7,False),((5,-4),(2.1,1.7),1.6,1.3,5,False),((-1,5),(2,1.6),1.3,-.4,5,False),((7,3),(1.4,1.3),1.1,.1,5,False),((-8,4),(1.5,1.1),1.0,.5,5,True)]),
 ('ENV03_C_WeatheredBoulder',1,[((0,0),(5.9,4.3),5.8,.2,8,False),((7,-2),(2.2,1.6),1.8,-.4,6,False)]),
 ('ENV03_D_BrokenOutcrop',0,[((-6,0),(7.6,4.9),4.0,.35,7,True),((5,1),(6.6,3.5),3.1,-.2,7,True),((12,-2),(3.5,2.5),1.9,.8,6,True),((-10,6),(3,2.1),1.5,-.6,6,True)])
]

def models():
    for i,(name,tone,parts) in enumerate(SPECS):
        rng=Random(8321+i*101); v=[]; f=[]
        for center,radii,height,yaw,sides,slab in parts:
            p,q=stone(center,radii,height,yaw,rng,sides,slab); offset=len(v);v+=p;f += [tuple(j+offset for j in face) for face in q]
        edges=Counter(tuple(sorted((a,b))) for face in f for a,b in zip(face,face[1:]+face[:1]))
        assert all(n==2 for n in edges.values())
        yield name,tone,v,f

def main():
    OUT.mkdir(parents=True,exist_ok=True); library=list(models())
    for name,tone,v,f in library:
        lines=['# ENV03 original decorative stone; Unity Y-up','o '+name,'s off']
        lines+=['v %.6f %.6f %.6f'%p for p in v]
        lines+=['vn %.8f %.8f %.8f'%normal(v,face) for face in f]
        lines+=['f '+' '.join(f'{j+1}//{i}' for j in face) for i,face in enumerate(f,1)]
        (OUT/(name+'.obj')).write_text('\n'.join(lines)+'\n',encoding='utf-8')
        print(name,len(f),'triangles', 'height',round(max(p[1] for p in v),2))
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    def linear(c):
        s=c/255;return s/12.92 if s<=.04045 else ((s+.055)/1.055)**2.4
    mats=[]
    for i,rgb in enumerate(PALETTE):
        m=bpy.data.materials.new('ENV03_Stone_'+str(i));m.diffuse_color=tuple(linear(c) for c in rgb)+(1,);mats.append(m)
    for i,(name,tone,v,f) in enumerate(library):
        mesh=bpy.data.meshes.new(name);mesh.from_pydata([(x,-z,y) for x,y,z in v],[],f);mesh.update();mesh.materials.append(mats[tone]);obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);obj.location=(i*45,0,0)
    dest=ROOT/'Resources/Blender/ENV03_DesertStones.blend';dest.parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(dest))

if __name__=='__main__':main()
