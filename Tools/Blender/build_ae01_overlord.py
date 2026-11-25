#!/usr/bin/env python3
"""AE01 visual-only Overlord. Y up, +Z nose; Python or Blender background."""
from pathlib import Path
from math import sin, cos, pi
from collections import Counter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Aircraft/Dominion/AE01_Overlord'
PALETTE = {'Hull': (82, 95, 82), 'Upper': (111, 124, 110), 'Dark': (43, 53, 57),
           'Gold': (210, 174, 62), 'Glass': (48, 83, 94), 'Dome': (105, 119, 110),
           'DomeBand': (49, 62, 64), 'GlowL': (47, 124, 141), 'GlowR': (47, 124, 141)}
DOME = (0, 4.6, -1.35)
PARTS = []

def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def center(v): return tuple(sum(p[i] for p in v)/len(v) for i in range(3))

def solid(name, group, v, faces, paint=None):
    # Orient a closed surface consistently, then choose outward signed volume.
    triangles = [(f[0],f[i],f[i+1]) for f in faces for i in range(1,len(f)-1)]
    edges = Counter((a,b) for t in triangles for a,b in zip(t,t[1:]+t[:1]))
    assert all(n == 1 and edges[b,a] == 1 for (a,b),n in edges.items()), name
    volume = sum(dot(v[a],cross(v[b],v[c])) for a,b,c in triangles)/6
    assert abs(volume)>1e-6, name
    if volume<0: triangles=[(a,c,b) for a,b,c in triangles]
    groups={}
    for t in triangles:
        a,b,c=(v[i] for i in t); n=cross(sub(b,a),sub(c,a)); assert dot(n,n)>1e-10, name
        g=paint(center([a,b,c]),n) if paint else group
        groups.setdefault(g,[]).append(t)
    for g,ts in groups.items(): PARTS.append((name,g,v,ts))

def rings(name, group, sections, n=10, vertical=False, paint=None):
    # section: axis position, transverse centre, height centre, radii.
    v=[]
    for z,x,y,w,h in sections:
        for k in range(n):
            a=2*pi*k/n; p=(x+w*cos(a),y+h*sin(a),z)
            v.append((p[0],p[2],p[1]) if vertical else p)
    last=len(v)-n
    faces=[tuple(reversed(range(n))),tuple(range(last,last+n))]
    for j in range(len(sections)-1):
        for k in range(n):
            a=j*n+k; b=j*n+(k+1)%n; faces.append((a,b,b+n,a+n))
    solid(name,group,v,faces,paint)

def slab(name,group,points,thick,axis=(0,1,0)):
    n=len(points); v=[tuple(p[i]+s*axis[i]*thick/2 for i in range(3)) for s in (-1,1) for p in points]
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    solid(name,group,v,faces)

def bodypaint(p,n): return 'Upper' if p[1]>.42 and n[1]>0 else 'Hull'

# Central lifting body: long wedge nose, shoulder and tapered tail.
rings('Fuselage','Hull',[(-11,0,.06,.34,.36),(-8.8,0,.05,.86,.71),(-4.5,0,.05,1.65,1.34),(1.0,0,.15,1.85,1.38),(5.5,0,.18,1.43,1.13),(8.7,0,.05,.78,.63),(11.5,0,-.04,.13,.13)],12,paint=bodypaint)
rings('Canopy','Glass',[(4.65,0,1.12,.68,.18),(5.5,0,1.28,.70,.38),(7.0,0,1.10,.48,.29),(8.25,0,.63,.18,.11)],8)
for s in (-1,1):
    def wing(name,g,a,b,za,ta,zb,tb,ya,yb,thick):
        slab(name+str(s),g,[(s*a,ya,za),(s*b,yb,zb),(s*b,yb,tb),(s*a,ya,ta)],thick)
    # Broad swept leading edge, narrow outer section, integral gold stripe.
    wing('Root','Hull',1.25,5.4,6.4,-7.5,2.45,-7.8,.0,.08,.65)
    wing('Outer','Upper',5.4,12.55,2.45,-7.8,-4.5,-8.2,.08,.30,.44)
    wing('Stripe','Gold',12.55,13.35,-4.5,-8.2,-5.25,-8.23,.30,.32,.44)
    wing('Tip','Upper',13.35,14,-5.25,-8.23,-5.9,-8.25,.32,.34,.44)
    # Subtle trailing panel follows the outer wing plane with a tiny offset.
    def panel_point(x, chord):
        t=(x-5.4)/(12.55-5.4)
        lead=2.45+(-4.5-2.45)*t; trail=-7.8+(-8.2+7.8)*t
        return (s*x,.08+(.30-.08)*t+.22+.024,lead+(trail-lead)*chord)
    slab('Outer wing panel '+str(s),'Hull',[panel_point(x,c) for x,c in
        [(7.3,.48),(11.75,.55),(11.75,.86),(7.3,.86)]],.025)
    # Large separate engine nacelles; long intake taper and wide rear nozzles.
    x=s*5.15
    rings('Nacelle '+str(s),'Hull',[(-11.5,x,.28,1.17,1.03),(-9.65,x,.28,1.47,1.31),(-5.8,x,.36,1.60,1.34),(-1.0,x,.32,1.34,1.12),(2.65,x,.20,.88,.73),(3.35,x,.20,.62,.51)],12,paint=bodypaint)
    rings('Intake face '+str(s),'Dark',[(3.28,x,.2,.61,.48),(3.39,x,.2,.56,.43)],10)
    # Dark nozzle collar with independent colored rear energy insert.
    rings('Nozzle '+str(s),'Dark',[(-11.68,x,.28,1.10,.94),(-11.42,x,.28,1.19,1.04),(-10.75,x,.28,1.23,1.08)],12)
    rings('Energy insert '+str(s),'GlowL' if s<0 else 'GlowR',[(-11.72,x,.28,.82,.70),(-11.66,x,.28,.87,.74)],12)
    # Split canted vertical fins mounted above outer nacelles.
    slab('Canted fin '+str(s),'Hull',[(s*5.30,1.16,-5.35),(s*6.95,4.45,-7.48),(s*7.15,4.45,-9.12),(s*5.55,1.16,-10.65)],.34,(1,0,0))
    slab('Fin stripe '+str(s),'Gold',[(s*6.77,4.12,-7.27),(s*6.95,4.45,-7.48),(s*7.15,4.45,-9.12),(s*6.97,4.12,-9.27)],.36,(1,0,0))
    wing('Tailplane','Hull',.75,4.3,-7.35,-10.6,-9.0,-10.7,.22,.33,.36)
# Pylon supports disc. Disc mesh local to rotation pivot; striped top reads rotation.
rings('Dome pylon','Dark',[(.9,0,-1.35,.76,1.17),(3.4,0,-1.35,.63,.92),(4.33,0,-1.35,.43,.66)],8,vertical=True)
# Disc built as flattened octadecagonal lens, closed and face-painted.
# Top cap is split at exact stripe boundaries, not angular sectors.
def dome():
    n=24; v=[]
    for y,r in [(-.36,3.92),(-.18,4.43),(.16,4.50),(.40,4.08)]:
        for k in range(n):
            a=2*pi*k/n; v.append((r*cos(a),y,r*sin(a)))
    # Cap center at slightly raised top; retain the lens profile.
    v += [(0,-.36,0),(0,.49,0)]
    faces=[]
    for k in range(n):
        l=(k+1)%n; faces.extend([(4*n,l,k),(4*n+1,3*n+k,3*n+l)])
        for j in range(3): faces.append((j*n+k,j*n+l,(j+1)*n+l,(j+1)*n+k))
    # Start with a validated closed lens. Clip its upper surface at z +/- .95
    # so the contrast stripe is straight and flush instead of fan-shaped.
    solid('Radar disc','Dome',v,faces)
    name,g,v,tris=PARTS.pop()
    groups={'Dome':([],[]),'DomeBand':([],[])}
    def clip(poly, edge, sign):
        out=[]
        for a,b in zip(poly,poly[1:]+poly[:1]):
            da=sign*(a[2]-edge); db=sign*(b[2]-edge)
            if da>=0: out.append(a)
            if (da<0 and db>0) or (da>0 and db<0):
                t=da/(da-db); out.append(tuple(a[i]+t*(b[i]-a[i]) for i in range(3)))
        return out
    def emit(group,poly):
        vs,ts=groups[group]
        for i in range(1,len(poly)-1):
            tri=[poly[0],poly[i],poly[i+1]]
            n=cross(sub(tri[1],tri[0]),sub(tri[2],tri[0]))
            if dot(n,n)<=1e-10: continue
            j=len(vs); vs.extend(tri); ts.append((j,j+1,j+2))
    for t in tris:
        poly=[v[i] for i in t]
        if min(p[1] for p in poly)<.159:
            emit('DomeBand',poly); continue
        emit('Dome',clip(poly,-.95,-1))
        emit('Dome',clip(poly,.95,1))
        emit('DomeBand',clip(clip(poly,-.95,1),.95,-1))
    for g,(vs,ts) in groups.items(): PARTS.append((name,g,vs,ts))
dome()

def export():
    OUT.mkdir(parents=True,exist_ok=True)
    for group in PALETTE:
        lines=['# AE01 Overlord / Y-up +Z forward / visual-only',f'o AE01_{group}','s off']; off=0
        for name,g,v,t in PARTS:
            if g!=group:continue
            used=sorted({i for tri in t for i in tri}); remap={old:off+j+1 for j,old in enumerate(used)}
            lines += ['v '+' '.join(f'{x:.6f}' for x in v[i]) for i in used]
            lines += ['f '+' '.join(str(remap[i]) for i in tri) for tri in t];off+=len(used)
        assert off>0,group
        (OUT/f'AE01_{group}.obj').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    print('AE01:',len(PARTS),'painted parts,',sum(len(t) for _,_,_,t in PARTS),'triangles')
    try: import bpy
    except ImportError:return
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    mats={}
    for g,rgb in PALETTE.items():
        m=bpy.data.materials.new('AE01_'+g); m.diffuse_color=tuple(x/255 for x in rgb)+(1,);mats[g]=m
    for name,g,v,t in PARTS:
        mesh=bpy.data.meshes.new(name+' '+g);mesh.from_pydata([(x,-z,y) for x,y,z in v],[],t);mesh.update();mesh.materials.append(mats[g]);ob=bpy.data.objects.new(name+' '+g,mesh);bpy.context.scene.collection.objects.link(ob)
        if g in ('Dome','DomeBand'): ob.location=(DOME[0],-DOME[2],DOME[1])
    path=ROOT/'Resources/Blender/AE01_Overlord.blend';path.parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(path))

if __name__=='__main__':export()
