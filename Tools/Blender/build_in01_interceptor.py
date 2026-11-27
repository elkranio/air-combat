#!/usr/bin/env python3
"""IN01 Long Dart V2 visual prototype. Unity +Y up, +Z nose; Blender +Z up."""
from pathlib import Path
from math import sin, cos, pi
from collections import Counter
import json

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Editor/IN01Geometry.json'
PALETTE = {'Hull': (81, 93, 107), 'Wing': (105, 119, 133), 'Dark': (35, 42, 52),
           'Steel': (135, 149, 160), 'Glass': (36, 112, 137), 'Mark': (184, 72, 53)}
PARTS = []

def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def mean(v): return tuple(sum(p[i] for p in v)/len(v) for i in range(3))

def add(name, paint, vertices, faces):
    v = [tuple(round(x,6) for x in p) for p in vertices]
    c = mean(v); tris=[]
    for f in faces:
        f=list(f); a,b,d=(v[i] for i in f[:3]); n=cross(sub(b,a),sub(d,a))
        assert dot(n,n)>1e-10, name
        if dot(n,sub(c,a))>0: f.reverse()
        for i in range(1,len(f)-1): tris.append((f[0],f[i],f[i+1]))
    edges=Counter((a,b) for t in tris for a,b in zip(t,t[1:]+t[:1]))
    assert all(n==1 and edges[b,a]==1 for (a,b),n in edges.items()), name
    for a,b,d in tris:
        n=cross(sub(v[b],v[a]),sub(v[d],v[a])); assert dot(n,n)>1e-10, name
    PARTS.append((name,paint,v,tris))

# Loft rings stay aligned; this keeps the surface closed and removes internal caps.
ROUND=[(cos(2*pi*i/10),sin(2*pi*i/10)) for i in range(10)]
CHAMFER=[(1,.48),(.68,1),(-.68,1),(-1,.48),(-1,-.48),(-.68,-1),(.68,-1),(1,-.48)]
def loft(name,paint,stations,profile=ROUND):
    n=len(profile); v=[(x+w*u,y+h*q,z) for z,x,y,w,h in stations for u,q in profile]
    faces=[list(range(n)),list(range(len(v)-n,len(v)))]
    for k in range(len(stations)-1):
        for i in range(n):
            j=(i+1)%n; a=k*n+i; b=k*n+j; faces.append([a,b,b+n,a+n])
    # Use known winding for sections rather than centroid tests on curved/tapered lofts.
    # Normalize profile to counterclockwise in XY.
    area=sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(profile,profile[1:]+profile[:1]))
    if area<0: return loft(name,paint,stations,list(reversed(profile)))
    tris=[]
    for k,f in enumerate(faces):
        if k==0: f=f[::-1]
        # ring edge CCW, then +Z = outward
        for i in range(1,len(f)-1): tris.append((f[0],f[i],f[i+1]))
    vv=[tuple(round(x,6) for x in p) for p in v]
    edges=Counter((a,b) for t in tris for a,b in zip(t,t[1:]+t[:1]))
    assert all(n==1 and edges[b,a]==1 for (a,b),n in edges.items()),name
    assert all(dot(cross(sub(vv[b],vv[a]),sub(vv[d],vv[a])),cross(sub(vv[b],vv[a]),sub(vv[d],vv[a])))>1e-10 for a,b,d in tris),name
    PARTS.append((name,paint,vv,tris))

def plate(name,paint,top,bottom):
    n=len(top); add(name,paint,top+bottom,[list(range(n)),list(range(n,2*n))]+[[i,(i+1)%n,(i+1)%n+n,i+n] for i in range(n)])

def wing(name,paint,s,stations):
    # Two span stations: x, leading z, trailing z, upper/lower Y. Paint bands share edges.
    v=[]
    for x,l,t,u,d in stations:
        v += [(s*x,u,l),(s*x,u,t),(s*x,d,t),(s*x,d,l)]
    add(name,paint,v,[[0,1,2,3],[4,5,6,7],[0,4,5,1],[1,5,6,2],[2,6,7,3],[3,7,4,0]])

# V2: long continuous wedge, pinched waist, swept shoulders and distinct aft engines.
# Keep the full length and span of V1 and the same named parts for in-place Unity upgrades.
loft('Central fuselage','Hull',[(-7.60,0,.02,.43,.24),(-6.3,0,.04,.63,.40),(-3.9,0,.08,.89,.59),(-.6,0,.12,.96,.68),(2.2,0,.10,.82,.62),(4.7,0,.05,.61,.45),(6.55,0,.0,.34,.29)])
loft('Radome','Dark',[(6.55,0,0,.34,.29),(7.85,0,-.025,.17,.15),(9.15,0,-.06,.025,.035)])
loft('Canopy','Glass',[(1.55,0,.60,.23,.10),(2.25,0,.73,.40,.26),(3.65,0,.68,.43,.32),(4.7,0,.43,.25,.15),(5.15,0,.34,.10,.05)])
loft('Canopy frame','Dark',[(3.18,0,.70,.436,.329),(3.26,0,.70,.436,.329)])
for s,side in [(-1,'L'),(1,'R')]:
    def n(x): return side+' '+x
    # Engines do not read as straight boxes: the forward intakes tuck into the nose shoulders,
    # the pods grow outward and upward towards the engine section, then taper into the nozzles.
    loft(n('engine nacelle'),'Hull',[(-7.18,s*1.25,-.10,.65,.62),(-5.85,s*1.36,-.03,.78,.76),(-3.25,s*1.34,.015,.81,.78),(-.60,s*1.15,-.035,.68,.66),(1.65,s*.94,-.10,.52,.51),(3.15,s*.83,-.12,.40,.39)])
    loft(n('intake lip'),'Steel',[(3.15,s*.83,-.12,.40,.39),(3.35,s*.80,-.13,.34,.33)])
    loft(n('intake face'),'Dark',[(3.35,s*.80,-.13,.29,.27),(3.365,s*.80,-.13,.29,.27)])
    loft(n('exhaust collar'),'Dark',[(-8.42,s*1.25,-.10,.53,.51),(-8.12,s*1.25,-.10,.61,.58),(-7.18,s*1.25,-.10,.65,.62)])
    loft(n('exhaust throat'),'Steel',[(-8.46,s*1.25,-.10,.39,.37),(-8.42,s*1.25,-.10,.43,.41)])
    loft(n('exhaust core'),'Dark',[(-8.49,s*1.25,-.10,.31,.29),(-8.46,s*1.25,-.10,.31,.29)])
    # Larger swept wing plane, long root chord and a narrow outer paint band.
    sections=[(.98,2.55,-4.90,.23,-.22),(4.50,-1.87,-4.69,.075,-.07),(4.77,-2.21,-4.67,.060,-.055),(5.05,-2.56,-4.65,.045,-.04)]
    for k,mat in enumerate(['Wing','Mark','Wing']): wing(n('wing '+str(k+1)),mat,s,sections[k:k+2])
    wing(n('tailplane'),'Wing',s,[(1.20,-5.10,-7.82,.13,-.11),(3.12,-6.76,-7.44,.045,-.04)])
    profile=[(.48,-4.48),(2.16,-6.04),(2.24,-6.80),(.36,-7.66)]
    def fin(y,z,offset): return (s*(1.29+.19*y)+offset,y,z)
    plate(n('vertical fin'),'Hull',[fin(y,z,.09) for y,z in profile],[fin(y,z,-.09) for y,z in profile])
    for q in [-1,1]:
        xoff=q*.094
        pp=[(1.72,-5.97),(1.85,-6.06),(1.89,-6.83),(1.76,-6.91)]
        plate(n('fin insignia '+str(q)),'Mark',[fin(y,z,xoff) for y,z in pp],[fin(y,z,xoff+q*.008) for y,z in pp])
    loft(n('missile body'),'Steel',[(-3.40,s*2.90,-.48,.14,.14),(-1.16,s*2.90,-.48,.14,.14),(-.73,s*2.90,-.48,.018,.018)])
    wing(n('missile pylon'),'Dark',s,[(2.77,-1.30,-2.88,-.02,-.40),(3.03,-1.30,-2.88,-.02,-.40)])


def main():
    OUT.parent.mkdir(parents=True,exist_ok=True)
    data={'palette':[{'name':k,'rgb':v} for k,v in PALETTE.items()], 'parts':[
        {'name':name,'material':mat,'vertices':[dict(zip('xyz',p)) for p in v], 'triangles':[i for t in tris for i in t]}
        for name,mat,v,tris in PARTS]}
    OUT.write_text(json.dumps(data,separators=(',',':'))+'\n',encoding='utf-8')
    print('IN01:',len(PARTS),'parts,',sum(len(t) for _,_,_,t in PARTS),'triangles')
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    materials={}
    for name,rgb in PALETTE.items():
        mat=bpy.data.materials.new('IN01_'+name); mat.diffuse_color=(*[x/255 for x in rgb],1); materials[name]=mat
    for name,mat,v,tris in PARTS:
        mesh=bpy.data.meshes.new(name); mesh.from_pydata([(x,-z,y) for x,y,z in v],[],tris); mesh.update()
        obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj); mesh.materials.append(materials[mat])
    path=ROOT/'Resources/Blender/IN01_Interceptor.blend'; path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))

if __name__=='__main__': main()
