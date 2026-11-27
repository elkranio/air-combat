#!/usr/bin/env python3
"""IN01 Long Dart visual prototype. Unity +Y up, +Z nose; Blender +Z up."""
from pathlib import Path
from math import sin, cos, pi
from collections import Counter
import json

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Editor/IN01Geometry.json'
PALETTE = {'Hull': (76, 88, 90), 'Wing': (113, 126, 125), 'Dark': (37, 43, 47),
           'Steel': (145, 155, 154), 'Glass': (40, 103, 123), 'Mark': (190, 65, 43)}
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

# Long radome, narrow forward cockpit, muscular aft fuselage. 17.6 x 10.1 units.
loft('Central fuselage','Hull',[(-7.60,0,.02,.58,.29),(-6.3,0,.04,.87,.51),(-3.9,0,.08,1.16,.67),(-.6,0,.12,1.21,.72),(2.2,0,.10,.94,.66),(4.7,0,.05,.65,.51),(6.25,0,.0,.45,.37)])
loft('Radome','Dark',[(6.25,0,0,.45,.37),(7.65,0,-.025,.23,.20),(9.15,0,-.06,.025,.035)])
loft('Canopy','Glass',[(1.55,0,.64,.26,.12),(2.25,0,.80,.45,.30),(3.65,0,.74,.49,.37),(4.7,0,.49,.28,.17),(5.15,0,.38,.12,.05)])
loft('Canopy frame','Dark',[(3.18,0,.76,.498,.376),(3.26,0,.75,.498,.376)])
for s,side in [(-1,'L'),(1,'R')]:
    def n(x): return side+' '+x
    # Shoulder intakes ahead of the wing; long faceted twin powerplants.
    loft(n('engine nacelle'),'Hull',[(-7.18,s*1.43,-.10,.81,.73),(-5.85,s*1.46,-.04,.94,.82),(-2.7,s*1.50,-.025,.96,.84),(1.50,s*1.45,-.04,.88,.78),(2.70,s*1.39,-.08,.80,.70)],CHAMFER)
    loft(n('intake lip'),'Steel',[(2.70,s*1.39,-.08,.80,.70),(2.93,s*1.39,-.08,.73,.63)],CHAMFER)
    loft(n('intake face'),'Dark',[(2.93,s*1.39,-.08,.62,.51),(2.945,s*1.39,-.08,.62,.51)],CHAMFER)
    loft(n('exhaust collar'),'Dark',[(-8.42,s*1.43,-.10,.68,.60),(-8.12,s*1.43,-.10,.78,.69),(-7.18,s*1.43,-.10,.81,.73)])
    loft(n('exhaust throat'),'Steel',[(-8.46,s*1.43,-.10,.48,.42),(-8.42,s*1.43,-.10,.54,.48)])
    loft(n('exhaust core'),'Dark',[(-8.49,s*1.43,-.10,.39,.34),(-8.46,s*1.43,-.10,.39,.34)])
    # Strongly swept, short chord outer panels, painted stripe rather than raised geometry.
    sections=[(1.70,1.65,-4.60,.25,-.23),(3.58,-.93,-4.44,.13,-.12),(4.47,-2.12,-4.37,.085,-.08),(5.05,-2.90,-4.32,.055,-.05)]
    # One closed wing with material bands represented by adjacent closed sections. Internal caps hidden.
    for k,mat in enumerate(['Wing','Mark','Wing']): wing(n('wing '+str(k+1)),mat,s,sections[k:k+2])
    wing(n('tailplane'),'Wing',s,[(1.54,-5.12,-7.98,.18,-.15),(3.55,-6.72,-7.65,.07,-.06)])
    # Paired canted fins on nacelles, tall but narrow in plan view.
    profile=[(.43,-4.12),(2.48,-6.02),(2.55,-7.00),(.43,-7.82)]
    def fin(y,z,offset): return (s*(1.48+.19*y)+offset,y,z)
    plate(n('vertical fin'),'Hull',[fin(y,z,.105) for y,z in profile],[fin(y,z,-.105) for y,z in profile])
    # Small bright stripe across top of the fin, sits on outer faces.
    for q in [-1,1]:
        xoff=q*.109
        pp=[(1.88,-5.57),(2.08,-5.76),(2.12,-7.10),(1.93,-7.18)]
        plate(n('fin insignia '+str(q)),'Mark',[fin(y,z,xoff) for y,z in pp],[fin(y,z,xoff+q*.008) for y,z in pp])
    # Two compact external missile bodies for role readability (visual only).
    loft(n('missile body'),'Steel',[(-2.72,s*2.90,-.48,.14,.14),(-.48,s*2.90,-.48,.14,.14),(-.05,s*2.90,-.48,.018,.018)])
    wing(n('missile pylon'),'Dark',s,[(2.77,-.68,-2.20,-.02,-.40),(3.03,-.68,-2.20,-.02,-.40)])


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
