"""Sparse planar fracture cuts for CL06. Pure Python; no Blender dependencies.
Preserves the navigation-critical local Y70..96 band on tall stones.
"""
from collections import Counter
from math import cos, sin, radians, sqrt
from build_cl05_test_rock import norm


def blend(a,b,t):
    return tuple(x+(y-x)*t for x,y in zip(a,b))


def signed_area(a,b,c):
    return (b[0]-a[0])*(c[2]-a[2])-(b[2]-a[2])*(c[0]-a[0])


def cap_faces(v, loop):
    # Upward face in Unity X/Y/Z has clockwise XZ winding.
    area=sum(v[a][0]*v[b][2]-v[b][0]*v[a][2] for a,b in zip(loop,loop[1:]+loop[:1]))
    if area>0: loop.reverse()
    todo=loop[:]; result=[]
    while len(todo)>3:
        for i,b in enumerate(todo):
            a,c=todo[i-1],todo[(i+1)%len(todo)]
            if signed_area(v[a],v[b],v[c])>=-1e-8: continue
            if any(max(signed_area(v[a],v[b],v[p]),signed_area(v[b],v[c],v[p]),signed_area(v[c],v[a],v[p]))<=1e-8
                   for p in todo if p not in (a,b,c)): continue
            result.append((a,b,c)); todo.pop(i); break
        else: raise ValueError('Cannot triangulate fracture cap')
    if signed_area(*(v[i] for i in todo))>=-1e-8: raise ValueError('Degenerate fracture cap')
    result.append(tuple(todo)); return result


def plane_cut(v,faces,angle,depth,floor,slope):
    # Cut a corner away with one broad inclined plane. The lowest point of the
    # plane over this entire stone stays above floor, even after rotation.
    u,w=cos(radians(angle)),sin(radians(angle))
    proj=[u*x+w*z for x,y,z in v]; hi=max(proj)
    threshold=max(floor,max(y for x,y,z in v)-depth)
    d=[y-(threshold+slope*(hi-p)) for (x,y,z),p in zip(v,proj)]
    if max(d)<1e-7: return v,faces
    assert all(abs(q)>1e-8 for q in d), 'Plane goes through original vertex'
    vv=v[:]; edge_hits={}; out=[]; border=[]
    def hit(a,b):
        key=tuple(sorted((a,b)))
        if key not in edge_hits:
            edge_hits[key]=len(vv); vv.append(blend(v[a],v[b],d[a]/(d[a]-d[b])))
        return edge_hits[key]
    for f in faces:
        poly=[]; cuts=[]
        for a,b in zip(f,f[1:]+f[:1]):
            if d[a]<0: poly.append(a)
            if (d[a]<0)!=(d[b]<0):
                k=hit(a,b); poly.append(k); cuts.append(k)
        for i in range(1,len(poly)-1): out.append((poly[0],poly[i],poly[i+1]))
        if cuts: assert len(cuts)==2; border.append(tuple(cuts))
    adjacency={}
    for a,b in border:
        adjacency.setdefault(a,[]).append(b); adjacency.setdefault(b,[]).append(a)
    assert all(len(n)==2 for n in adjacency.values())
    remaining=set(adjacency)
    while remaining:
        start=min(remaining); loop=[start]; prev=None; cur=start
        while True:
            nxt=next(p for p in adjacency[cur] if p!=prev)
            if nxt==start: break
            loop.append(nxt); prev,cur=cur,nxt
        remaining.difference_update(loop); out.extend(cap_faces(vv,loop))
    used=sorted({i for f in out for i in f}); remap={old:i for i,old in enumerate(used)}
    return [vv[i] for i in used],[tuple(remap[i] for i in f) for f in out]


# Per-stone authored cut directions, depths, slopes and wall fracture indices.
# Different corners are chosen for different rocks, never a continuous belt.
CUTS=[((32,43,.34),(212,34,.40)),((115,53,.41),(310,39,.30)),
      ((-42,36,.24),(144,28,.31)),((201,23,.22),(34,18,.34)),
      ((68,59,.39),(247,45,.34)),((155,22,.32),),
      ((-24,17,.42),),((70,14,.48),)]
WALLS=[(0,5),(1,4),(0,7),(2,6),(1,10),(0,7),(1,5),(0,4)]


def fissure(v, f, max_y, depth):
    # Make a narrow, elongated recessed pocket wholly within one planar face.
    # The outer three vertices/edges never move. Coplanar surround avoids the
    # all-over triangular noise of random vertex displacement.
    pts=[v[i] for i in f]; low=min(pts,key=lambda p:p[1]); high=max(p[1] for p in pts)
    if high-low[1]<15: return [f]
    center=tuple(sum(p[k] for p in pts)/3 for k in range(3))
    inner=[blend(center,p,.74) for p in pts]
    # Stay below the navigation band. Shrink towards the low corner, if needed.
    top=max(p[1] for p in inner)
    if top>max_y:
        t=(max_y-low[1])/(top-low[1]); inner=[blend(low,p,t) for p in inner]
    # Narrow horizontally while keeping points on the original triangle plane:
    # blend toward a point on the long diagonal at the same height.
    hi=max(pts,key=lambda p:p[1])
    for i,p in enumerate(inner):
        spine=blend(low,hi,(p[1]-low[1])/(hi[1]-low[1])); inner[i]=blend(p,spine,.08)
    n=norm(v,f); horiz=sqrt(n[0]*n[0]+n[2]*n[2]); assert horiz>.8
    mid=tuple(sum(p[k] for p in inner)/3 for k in range(3))
    tip=(mid[0]-n[0]/horiz*depth,mid[1],mid[2]-n[2]/horiz*depth)
    start=len(v); v.extend(inner+[tip]); q=[start,start+1,start+2]; t=start+3
    out=[]
    for i in range(3):
        j=(i+1)%3; out.extend(((f[i],f[j],q[j]),(f[i],q[j],q[i]),(q[i],q[j],t)))
    return out


def fracture(v,faces,index):
    v=v[:]; n=(len(v)-2)//3; out=[]
    # First ring of wall triangles only; use alternating sides of each wall,
    # with different selected panels per stone. Keep whole fracture below Y68.
    selected={2*i+(j%2) for j,i in enumerate(WALLS[index]) if i<n}
    for i,f in enumerate(faces):
        if i in selected and abs(norm(v,f)[1])<.25:
            out.extend(fissure(v,f, min(68,max(p[1] for p in v[:3*n])*.65), (12.5,14.5,11.5,12.0,13.5,8.5,4.5,4.0)[index]))
        else: out.append(f)
    # A..E: cut only above all known local cruise slice heights (75..93).
    # F is unused by the dense recipe; protect Y78 for future unit-scale use.
    floor=96.5 if index<5 else 79.0 if index==5 else 15.0
    for angle,depth,slope in CUTS[index]: v,out=plane_cut(v,out,angle,depth,floor,slope)
    edges=Counter((a,b) for f in out for a,b in zip(f,f[1:]+f[:1]))
    assert all(count==1 and edges[b,a]==1 for (a,b),count in edges.items())
    for f in out: norm(v,f)
    return v,out
