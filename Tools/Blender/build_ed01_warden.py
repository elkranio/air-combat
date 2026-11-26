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

# Armoured, blunt sensor prow, narrow rear spine. No cockpit or conventional tail.
rings('Central armour','Hull',[(-6.8,0,.05,.70,.52),(-4.7,0,.13,1.65,1.05),(-1.5,0,.25,2.35,1.37),(2.5,0,.30,2.60,1.45),(4.65,0,.10,2.25,1.12),(6.0,0,-.12,1.43,.72)],paint=paint)
rings('Sensor jaw','Dark',[(4.5,0,-.4,1.65,.60),(5.9,0,-.45,1.35,.53),(6.35,0,-.43,1.05,.36)])
box('Red optical visor','Red',0,-.28,6.31,1.42,.32,.14)
box('Dorsal spine','Dark',0,1.65,-.95,.85,.25,3.3)
for s in (-1,1):
    # Swept structural shoulder, with a distinct gap forward of the engine.
    slab('Shoulder '+str(s),'Hull',[(s*1.6,.05,2.7),(s*5.7,.10,1.5),(s*6.9,.10,-2.65),(s*2.0,.05,-3.8)],1.08)
    slab('Shoulder armour '+str(s),'Upper',[(s*2.0,.69,2.5),(s*5.45,.70,1.4),(s*5.9,.70,-1.25),(s*2.3,.69,-2.0)],.24)
    # Detached-looking industrial engine pods, wide blunt intakes and short exhausts.
    x=s*6.35
    rings('Engine nacelle '+str(s),'Hull',[(-6.6,x,.20,1.18,.89),(-5.3,x,.20,1.64,1.19),(-2.1,x,.24,1.92,1.44),(1.15,x,.24,1.87,1.42),(3.4,x,.24,1.52,1.12),(4.1,x,.24,1.18,.88)],paint=paint)
    rings('Intake bezel '+str(s),'Dark',[(3.82,x,.24,1.20,.88),(4.18,x,.24,1.14,.83)])
    # Recess appearance using a dark cap and chunky horizontal divider.
    rings('Intake face '+str(s),'Dark',[(4.12,x,.24,.92,.61),(4.21,x,.24,.90,.60)])
    box('Intake crossbar '+str(s),'Steel',x,.24,4.24,1.72,.15,.13)
    rings('Rear nozzle '+str(s),'Dark',[(-7.25,x,.20,.83,.65),(-6.75,x,.20,1.06,.80),(-6.25,x,.20,1.13,.88)])
    rings('Exhaust core '+str(s),'Steel',[(-7.30,x,.20,.58,.43),(-7.20,x,.20,.63,.47)])
    # Outer shoulder stripe on top and side, no floating decals.
    slab('Dominion flash '+str(s),'Red',[(s*6.9,1.54,.8),(s*7.48,1.32,.68),(s*7.48,1.32,-1.0),(s*6.9,1.54,-.88)],.10)
    slab('Rear stabilizer '+str(s),'Upper',[(s*7.1,.48,-3.7),(s*8.85,.55,-4.8),(s*8.45,.55,-6.0),(s*6.9,.48,-5.6)],.42)
    # Fixed circular mount under forward shoulder; local Y is yaw axis.
    tx,ty,tz=TURRETS['L' if s<0 else 'R']
    cylinder('Turret mount '+str(s),'Dark',[(ty-.08,tx,tz,.86,.86),(ty+.65,tx,tz,.96,.96)],vertical=True)

# One reusable turret geometry in local coordinates, instantiated twice.
# Separate yaw and pitch transforms are authored in the Unity visual prefab.
cylinder('Yaw base','Steel',[(-.38,0,0,.71,.71),(.03,0,0,.82,.82)],'Yaw',vertical=True)
rings('Yaw yoke','Dark',[(-.60,0,-.34,.63,.39),(.5,0,-.34,.68,.38)],'Yaw')
rings('Gun housing','Hull',[(-.73,0,0,.48,.43),(-.40,0,0,.61,.49),(.52,0,0,.52,.38),(.82,0,0,.33,.26)],'Pitch')
# Stylised gun shape only: no real-world functional details.
cylinder('Barrel','Dark',[(.65,0,0,.19,.19),(2.35,0,0,.17,.17)],'Pitch',n=8)
cylinder('Muzzle collar','Steel',[(2.13,0,0,.23,.23),(2.47,0,0,.23,.23)],'Pitch',n=8)


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
