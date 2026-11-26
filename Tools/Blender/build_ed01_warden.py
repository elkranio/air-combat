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
TURRETS = {'L': (-4.05, -0.95, 2.05), 'R': (4.05, -0.95, 2.05)}

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

# ED01B: lightweight escort UAV. Open frame, small sensor head, separated pods.
# Keep existing Unity turret/exhaust pivot positions and +Z muzzle sockets.
rings('Central spine','Hull',[(-6.35,0,.08,.38,.36),(-4.65,0,.12,.78,.57),(-1.5,0,.16,1.25,.73),(2.4,0,.18,1.42,.78),(4.55,0,.07,1.16,.65),(5.70,0,-.04,.78,.48)],paint=paint)
rings('Sensor head','Dark',[(4.65,0,-.26,.85,.39),(5.68,0,-.25,.77,.36),(6.05,0,-.22,.54,.24)])
box('Red optic','Red',0,-.15,6.05,.78,.18,.12)
box('Dorsal spine','Dark',0,.94,-.7,.46,.17,2.65)
for s in (-1,1):
    x=s*6.35
    # Narrow forward cross-spar and short rear brace leave a large open bay.
    slab('Forward spar '+str(s),'Hull',[(s*1.05,.10,2.75),(s*6.38,.10,2.88),(s*6.38,.10,1.48),(s*1.06,.10,1.38)],.52)
    slab('Rear brace '+str(s),'Hull',[(s*.65,.11,-3.30),(s*6.32,.11,-3.82),(s*6.32,.11,-4.48),(s*.60,.11,-4.05)],.39)
    slab('Spar accent '+str(s),'Upper',[(s*1.22,.37,2.55),(s*5.23,.37,2.66),(s*5.23,.37,2.38),(s*1.22,.37,2.26)],.06)
    # Slender pods preserve span but shed the heavy slab-like mass.
    rings('Engine nacelle '+str(s),'Hull',[(-6.62,x,.20,.67,.55),(-5.15,x,.20,1.01,.76),(-2.0,x,.24,1.18,.86),(1.15,x,.24,1.13,.83),(3.25,x,.24,.91,.70),(4.1,x,.24,.67,.52)],paint=paint)
    rings('Intake bezel '+str(s),'Dark',[(3.82,x,.24,.70,.54),(4.18,x,.24,.65,.49)])
    rings('Intake face '+str(s),'Dark',[(4.12,x,.24,.51,.36),(4.22,x,.24,.49,.34)])
    box('Intake crossbar '+str(s),'Steel',x,.24,4.23,.89,.10,.13)
    rings('Rear nozzle '+str(s),'Dark',[(-7.25,x,.20,.45,.37),(-6.75,x,.20,.62,.49),(-6.25,x,.20,.67,.54)])
    rings('Exhaust core '+str(s),'Steel',[(-7.30,x,.20,.31,.24),(-7.20,x,.20,.35,.28)])
    # Thin painted stripe on the top of each pod, following its slope.
    slab('Dominion flash '+str(s),'Red',[(x-.25,1.095,.55),(x+.25,1.095,.55),(x+.25,1.105,-1.05),(x-.25,1.105,-1.05)],.06)
    slab('Rear stabilizer '+str(s),'Upper',[(s*6.90,.35,-3.90),(s*8.55,.35,-4.82),(s*8.20,.35,-5.65),(s*6.86,.35,-5.12)],.24)
    # Turret mounting point remains at the original prefab pivot.
    tx,ty,tz=TURRETS['L' if s<0 else 'R']
    cylinder('Turret mount '+str(s),'Dark',[(ty-.08,tx,tz,.48,.48),(ty+.95,tx,tz,.61,.61)],vertical=True)

# Shared lightweight turret visuals. Original yaw/pitch/muzzle transforms unchanged.
cylinder('Yaw base','Steel',[(-.38,0,0,.43,.43),(.03,0,0,.54,.54)],'Yaw',vertical=True)
rings('Yaw yoke','Dark',[(-.42,0,-.34,.39,.26),(.36,0,-.34,.43,.26)],'Yaw')
rings('Gun housing','Hull',[(-.59,0,0,.29,.26),(-.31,0,0,.39,.32),(.43,0,0,.33,.25),(.68,0,0,.21,.18)],'Pitch')
cylinder('Barrel','Dark',[(.54,0,0,.115,.115),(2.35,0,0,.105,.105)],'Pitch',n=8)
cylinder('Muzzle collar','Steel',[(2.24,0,0,.155,.155),(2.47,0,0,.155,.155)],'Pitch',n=8)


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
    for side,pos in TURRETS.items():
        yaw=empty('TurretYaw_'+side,root,pos);pitch=empty('TurretPitch_'+side,yaw,(0,-.34,0));empty('Muzzle_'+side,pitch,(0,0,2.47));parents['Yaw'].append(yaw);parents['Pitch'].append(pitch)
    for name,g,a,v,t in PARTS:
        mesh=bpy.data.meshes.new(name);mesh.from_pydata([bl(p) for p in v],[],t);mesh.update();mesh.materials.append(mats[g])
        for parent in parents[a]:
            ob=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(ob);ob.parent=parent
        for p in mesh.polygons:p.use_smooth=False
    path=ROOT/'Resources/Blender/ED01_Warden.blend';path.parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(path))

if __name__=='__main__':export()
