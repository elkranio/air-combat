#!/usr/bin/env python3
"""ED01 Warden visual-only fictional arcade escort drone. Y-up, +Z forward.
Run with Python for OBJ export or Blender background for OBJ + editable blend.
"""
from pathlib import Path
from collections import Counter
from math import sin, cos, pi

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Aircraft/Dominion/ED01_Warden'
PALETTE = {'Hull': (76, 85, 68), 'Upper': (112, 122, 94), 'Dark': (39, 47, 47),
           'Red': (179, 55, 45), 'Steel': (112, 124, 126)}
PARTS = []
TURRETS = {'L': (-2.10, -.26, 3.25), 'R': (2.10, -.26, 3.25)}

def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def center(v): return tuple(sum(p[i] for p in v)/len(v) for i in range(3))

def solid(name,group,v,faces,assembly='Body',paint=None):
    # Every source solid is closed, with outward winding and nonzero triangles.
    ts=[(f[0],f[i],f[i+1]) for f in faces for i in range(1,len(f)-1)]
    edges=Counter((a,b) for t in ts for a,b in zip(t,t[1:]+t[:1]))
    assert all(n==1 and edges[b,a]==1 for (a,b),n in edges.items()),name
    volume=sum(dot(v[a],cross(v[b],v[c])) for a,b,c in ts)/6
    assert abs(volume)>1e-7,name
    if volume<0: ts=[(a,c,b) for a,b,c in ts]
    groups={}
    for t in ts:
        a,b,c=(v[i] for i in t);n=cross(sub(b,a),sub(c,a))
        assert dot(n,n)>1e-10,name
        g=paint(center([a,b,c]),n) if paint else group
        groups.setdefault(g,[]).append(t)
    for g,t in groups.items():PARTS.append((name,g,assembly,v,t))

# Chamfered octagonal cross section. Width and height are half extents.
PROFILE=[(1,.55),(.68,1),(-.68,1),(-1,.55),(-1,-.55),(-.68,-1),(.68,-1),(1,-.55)]

def rings(name,g,sections,assembly='Body',profile=PROFILE,vertical=False,paint=None):
    v=[];n=len(profile)
    for z,x,y,w,h in sections:
        for a,b in profile:
            p=(x+w*a,y+h*b,z)
            v.append((p[0],p[2],p[1]) if vertical else p)
    last=len(v)-n
    faces=[tuple(reversed(range(n))),tuple(range(last,last+n))]
    for j in range(len(sections)-1):
        for k in range(n):
            a=j*n+k;b=j*n+(k+1)%n;faces.append((a,b,b+n,a+n))
    solid(name,g,v,faces,assembly,paint)

def slab(name,g,points,thick,assembly='Body'):
    n=len(points);v=[(x,y+s*thick/2,z) for s in (-1,1) for x,y,z in points]
    solid(name,g,v,[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],assembly)

def box(name,g,x,y,z,w,h,d,assembly='Body'):
    slab(name,g,[(x-w/2,y,z-d/2),(x+w/2,y,z-d/2),(x+w/2,y,z+d/2),(x-w/2,y,z+d/2)],h,assembly)

def cylinder(name,g,sections,assembly='Body',vertical=False,n=10):
    rings(name,g,sections,assembly,[(cos(2*pi*i/n),sin(2*pi*i/n)) for i in range(n)],vertical)

def paint(p,n):return 'Upper' if n[1]>.25 else 'Hull'

# ED01C: twin ducted rotor escort drone. Compact teardrop body, no wings or booms.
# +Z is the nose. The ring shrouds are hollow, not solid discs.
ROTORS = {'L': (-5.0, .16, -1.15), 'R': (5.0, .16, -1.15)}
EXHAUSTS = {'L': (-.66, .05, -4.48), 'R': (.66, .05, -4.48)}

# A closed annular shell with chamfered edges; separate top/inner paint faces.
def duct(name, x, z):
    n=24
    # cross section (radius, y) around a vertical axis
    section=[(3.15,-.29),(3.34,-.16),(3.40,.33),(3.26,.51),
             (2.91,.51),(2.79,.33),(2.79,-.18),(2.93,-.29)]
    v=[(x+r*cos(2*pi*k/n),y,z+r*sin(2*pi*k/n)) for r,y in section for k in range(n)]
    faces=[]
    for j in range(len(section)):
        q=(j+1)%len(section)
        for k in range(n):
            l=(k+1)%n
            faces.append((j*n+k,j*n+l,q*n+l,q*n+k))
    def duct_paint(p,normal):
        r=((p[0]-x)**2+(p[2]-z)**2)**.5
        if normal[1]>.45*dot(normal,normal)**.5:
            # Red forward-outboard rim segments make nose direction apparent.
            if p[2]-z>1.10 and (p[0]-x)*(1 if x>0 else -1)>1.35:return 'Red'
            return 'Upper'
        return 'Dark' if r<2.96 else 'Hull'
    solid(name,'Hull',v,faces,paint=duct_paint)

# Short tapered body, broad enough to read as a vehicle rather than a stick.
rings('Teardrop chassis','Hull',[(-4.48,0,.02,.64,.38),(-3.1,0,.08,1.00,.57),(-.6,0,.11,1.45,.82),(1.4,0,.10,1.63,.87),(3.0,0,.05,1.46,.68),(4.45,0,-.06,.94,.43),(5.06,0,-.08,.55,.31)],paint=paint)
rings('Sensor prow','Dark',[(3.82,0,-.10,.87,.36),(4.88,0,-.12,.60,.29),(5.20,0,-.12,.45,.22)])
box('Red optic','Red',0,-.05,5.18,.76,.24,.12)
# Top-facing red sensor strip is visible from the flight camera.
slab('Sensor crown','Red',[(-.37,.39,4.43),(.37,.39,4.43),(.24,.27,4.99),(-.24,.27,4.99)],.09)
slab('Dorsal panel','Dark',[(-.42,.99,1.08),(.42,.99,1.08),(.30,.87,-1.74),(-.30,.87,-1.74)],.10)

for s in (-1,1):
    side='L' if s<0 else 'R'
    x,ry,z=ROTORS[side]
    # Single short swept link, no ladder-like rear braces.
    slab('Rotor support '+side,'Hull',[(s*1.00,.12,1.65),(s*2.18,.12,.88),(s*2.21,.12,-.90),(s*.98,.12,-.12)],.62)
    slab('Support upper '+side,'Upper',[(s*1.20,.46,1.28),(s*1.93,.46,.80),(s*2.00,.46,-.46),(s*1.26,.46,-.03)],.12)
    duct('Rotor shroud '+side,x,z)
    # Three narrow fixed spokes sit below the blade plane.
    for k in range(3):
        a=2*pi*k/3+(.20 if s<0 else -.20)
        def point(r,t,y):return (x+r*cos(a)-t*sin(a),y,z+r*sin(a)+t*cos(a))
        slab('Motor spoke '+side+str(k),'Dark',[point(.35,-.105,-.22),point(2.90,-.13,-.22),point(2.90,.13,-.22),point(.35,.105,-.22)],.19)
    cylinder('Motor hub '+side,'Dark',[(-.35,x,z,.50,.50),(.11,x,z,.54,.54),(.32,x,z,.34,.34)],vertical=True,n=12)
    cylinder('Hub cap '+side,'Steel',[(.31,x,z,.25,.25),(.39,x,z,.18,.18)],vertical=True,n=10)
    # Clearly visible broad, tapered blades. Static for this silhouette trial.
    for k in range(3):
        a=2*pi*k/3+(.68 if s<0 else -.68)
        def blade(r,t):return (x+r*cos(a)-s*t*sin(a),.17,z+r*sin(a)+s*t*cos(a))
        slab('Rotor blade '+side+str(k),'Steel',[blade(.38,-.14),blade(1.15,-.40),blade(2.62,-.32),blade(2.65,.09),blade(1.12,.26),blade(.38,.15)],.14)
    # Short forward weapon mounts, clear of the rotor disc in top view.
    slab('Weapon shoulder '+side,'Hull',[(s*1.02,.10,3.66),(s*2.56,.10,3.65),(s*2.52,.10,2.79),(s*1.08,.10,2.20)],.56)
    tx,ty,tz=TURRETS[side]
    cylinder('Turret mount '+side,'Dark',[(ty-.06,tx,tz,.43,.43),(ty+.30,tx,tz,.52,.52)],vertical=True,n=10)
    # Small aft cooling ports retain the two original socket roles.
    ex,ey,ez=EXHAUSTS[side]
    rings('Cooling outlet '+side,'Dark',[(ez,ex,ey,.24,.20),(ez+.52,ex,ey,.32,.28)])

# Two modest but legible game-weapon silhouettes, same yaw/pitch/muzzle hierarchy.
cylinder('Yaw base','Steel',[(-.38,0,0,.48,.48),(.03,0,0,.57,.57)],'Yaw',vertical=True)
rings('Yaw yoke','Dark',[(-.45,0,-.34,.44,.29),(.39,0,-.34,.46,.29)],'Yaw')
rings('Gun housing','Hull',[(-.62,0,0,.36,.27),(-.32,0,0,.45,.34),(.45,0,0,.36,.28),(.70,0,0,.23,.20)],'Pitch')
cylinder('Barrel','Dark',[(.54,0,0,.15,.15),(2.35,0,0,.13,.13)],'Pitch',n=8)
cylinder('Muzzle collar','Steel',[(2.20,0,0,.19,.19),(2.47,0,0,.19,.19)],'Pitch',n=8)


def export():
    OUT.mkdir(parents=True,exist_ok=True)
    keys=sorted({(a,g) for _,g,a,_,_ in PARTS})
    for assembly,group in keys:
        name=f'ED01_{assembly}_{group}'
        lines=['# ED01 Warden / fictional visual / Y-up +Z forward',f'o {name}','s off'];off=0
        for _,g,a,v,t in PARTS:
            if (a,g)!=(assembly,group):continue
            lines+=['v '+' '.join(f'{x:.6f}' for x in p) for p in v]
            lines+=['f '+' '.join(str(i+1+off) for i in tri) for tri in t];off+=len(v)
        (OUT/(name+'.obj')).write_text('\n'.join(lines)+'\n',encoding='utf-8')
    body=[p for _,_,a,v,_ in PARTS if a=='Body' for p in v]
    print('ED01 body bounds:',[round(max(p[i] for p in body)-min(p[i] for p in body),2) for i in range(3)])
    print('ED01 rendered triangles:',sum(len(t)*(1 if a=='Body' else 2) for _,_,a,_,t in PARTS),'groups:',keys)
    try:import bpy
    except ImportError:return
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    mats={}
    for group,rgb in PALETTE.items():
        m=bpy.data.materials.new('ED01_'+group);c=tuple(x/255 for x in rgb);m.diffuse_color=(*c,1);m.use_nodes=True
        bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*c,1);bs.inputs['Roughness'].default_value=.92;mats[group]=m
    def bl(p):return (p[0],-p[2],p[1])
    def empty(name,parent=None,pos=(0,0,0)):
        o=bpy.data.objects.new(name,None);bpy.context.scene.collection.objects.link(o);o.parent=parent;o.location=bl(pos);return o
    root=empty('ED01_Warden');parents={'Body':[root],'Yaw':[],'Pitch':[]}
    for side,pos in EXHAUSTS.items():
        ex=empty('Exhaust_'+side,root,pos);ex.rotation_euler[2]=pi
    for side,pos in TURRETS.items():
        yaw=empty('TurretYaw_'+side,root,pos);pitch=empty('TurretPitch_'+side,yaw,(0,-.34,0));empty('Muzzle_'+side,pitch,(0,0,2.47));parents['Yaw'].append(yaw);parents['Pitch'].append(pitch)
    for name,g,a,v,t in PARTS:
        mesh=bpy.data.meshes.new(name);mesh.from_pydata([bl(p) for p in v],[],t);mesh.update();mesh.materials.append(mats[g])
        for parent in parents[a]:
            ob=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(ob);ob.parent=parent
        for p in mesh.polygons:p.use_smooth=False
    path=ROOT/'Resources/Blender/ED01_Warden.blend';path.parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(path))

if __name__=='__main__':export()
