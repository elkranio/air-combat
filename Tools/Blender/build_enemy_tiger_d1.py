#!/usr/bin/env python3
"""D1: original mass-production enemy fighter, inspired by the F-5 Tiger II silhouette.

No copyrighted reference geometry. World scale in metres: +Z nose, +Y up,
X wings. OBJ sections are committed so Unity imports them without Blender.
Rebuild them with `python3 Tools/Blender/build_enemy_tiger_d1.py`.
Running INSIDE Blender also creates Resources/Blender/EF01_Tiger_D1.blend:
    blender --background --python-exit-code 1 --python Tools/Blender/build_enemy_tiger_d1.py

The Unity D1 installer automatically creates a visual-only JetChassis prefab and
an editable comparison instance next to the parked Kestrels. No AI or weapons yet.
"""
from pathlib import Path
import math

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Aircraft/EF01_Tiger_D1'
BLEND = ROOT / 'Resources/Blender/EF01_Tiger_D1.blend'

PALETTE = {
    'Fuselage': (0.365, 0.430, 0.418),   # military slate olive
    'Upper': (0.475, 0.546, 0.522),      # sunward wing planes
    'Underside': (0.255, 0.332, 0.335),
    'Steel': (0.194, 0.273, 0.296),
    'Glass': (0.075, 0.256, 0.337),
    'Highlight': (0.220, 0.419, 0.469),
    'Accent': (0.805, 0.330, 0.229),
}


def vsub(a,b): return tuple(x-y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def dot(a,b): return sum(x*y for x,y in zip(a,b))


class Section:
    def __init__(self, name, material):
        self.name, self.material = name, material
        self.triangles = []

    def face(self, points, outward):
        pts = [tuple(float(n) for n in p) for p in points]
        assert len(pts) in (3,4)
        if dot(cross(vsub(pts[1],pts[0]),vsub(pts[2],pts[0])),outward)<0:
            pts.reverse()
        if len(pts)==4:
            self.triangles.extend(((pts[0],pts[1],pts[2]),(pts[0],pts[2],pts[3])))
        else: self.triangles.append(tuple(pts))

    def box(self,x0,y0,z0,x1,y1,z1):
        assert x0<x1 and y0<y1 and z0<z1
        a=(x0,y0,z0);b=(x1,y0,z0);c=(x1,y0,z1);d=(x0,y0,z1)
        e=(x0,y1,z0);f=(x1,y1,z0);g=(x1,y1,z1);h=(x0,y1,z1)
        for q,n in (([a,b,c,d],(0,-1,0)),([e,f,g,h],(0,1,0)),
                    ([a,d,h,e],(-1,0,0)),([b,f,g,c],(1,0,0)),
                    ([a,e,f,b],(0,0,-1)),([d,c,g,h],(0,0,1))):
            self.face(q,n)

    def write_obj(self):
        OUT.mkdir(parents=True,exist_ok=True)
        p=OUT/(self.name+'.obj')
        lines=[f'# EF-01 Tiger D1; {self.name}; metres, +Z nose, +Y up',
               f'o {self.name}', 's off']
        k=1
        for tri in self.triangles:
            for a in tri:lines.append('v '+ ' '.join(f'{x:.5f}' for x in a))
            lines.append(f'f {k} {k+1} {k+2}')
            k+=3
        p.write_text('\n'.join(lines)+'\n',encoding='utf-8')
        print(f'{p.relative_to(ROOT)}: {len(self.triangles)} triangles')


def ring(z,cx,cy,halfwidth,halfheight,vertices=10):
    return [(cx+halfwidth*math.cos(2*math.pi*i/vertices),
             cy+halfheight*math.sin(2*math.pi*i/vertices),z)
            for i in range(vertices)]


def tube(mesh, sections, sides=10):
    """Each section: (z, centre_x, centre_y, half_width, half_height)."""
    rings=[ring(*station,vertices=sides) for station in sections]
    z0=sections[0][0]; z1=sections[-1][0]
    mesh.face(rings[0][::-1],(0,0,-1)) if sides<=4 else cap(mesh,rings[0],(0,0,-1))
    for ri,(a,b) in enumerate(zip(rings,rings[1:])):
        for i in range(sides):
            j=(i+1)%sides
            p=(a[i],a[j],b[j],b[i])
            n=(sum(t[0] for t in p)/4-(sections[ri][1]+sections[ri+1][1])/2,
               sum(t[1] for t in p)/4-(sections[ri][2]+sections[ri+1][2])/2,
               0)
            mesh.face(p,n)
    cap(mesh,rings[-1],(0,0,1))


def cap(mesh,pts,normal):
    centre=tuple(sum(p[i] for p in pts)/len(pts) for i in range(3))
    for i in range(len(pts)):
        mesh.face([centre,pts[i],pts[(i+1)%len(pts)]],normal)


def wing(mesh,side,sections):
    """(x, leadingZ, trailingZ, y, top_thickness), symmetrical hard-edged profiles."""
    arr=[]
    for x,leading,trailing,y,t in sections:
        x*=side
        arr.append([(x,y+t,leading),(x,y+t*.6,trailing),
                    (x,y-t*.8,trailing),(x,y-t,leading)])
    for a,b in zip(arr,arr[1:]):
        mesh.face([a[0],b[0],b[1],a[1]],(0,1,0))
        mesh.face([a[2],b[2],b[3],a[3]],(0,-1,0))
        mesh.face([a[3],b[3],b[0],a[0]],(0,0,1))
        mesh.face([a[1],b[1],b[2],a[2]],(0,0,-1))
    cap(mesh,arr[-1],(side,0,0))


hull=Section('Tiger_Fuselage','Fuselage')
tube(hull,[(-6.15,0,0.02,.39,.38),(-5.45,0,.02,.82,.57),
           (-3.65,0,.01,1.30,.69),(-1.40,0,.02,1.50,.76),
           (.75,0,.02,1.35,.72),(2.45,0,.01,1.02,.53),
           (4.35,0,.00,.56,.34),(5.78,0,-.03,.22,.19),(6.70,0,-.03,.035,.05)])

wings=Section('Tiger_MainWings','Upper')
for sign in (-1,1):
    wing(wings,sign,[(1.06,1.72,-2.76,.04,.21),
                     (2.00,1.35,-2.78,.07,.175),
                     (4.70,-.54,-2.41,.12,.075),
                     (5.35,-.83,-2.15,.13,.045)])

tail=Section('Tiger_TailSurfaces','Upper')
for sign in (-1,1):
    wing(tail,sign,[(.78,-4.56,-6.01,.10,.14),
                    (1.54,-4.77,-6.16,.11,.095),
                    (3.18,-5.39,-5.99,.15,.035)])
# Single traditional vertical swept fin - distinct from Kestrel's twin tails.
# Side profile in Z/Y; 0.28m thickness, tip slopes aft.
profile=[(-5.92,.35),(-4.35,.70),(-4.86,2.95),(-5.44,3.14),(-6.30,1.00)]
for x,out in ((-.15,(-1,0,0)),(.15,(1,0,0))):
    for i in range(1,len(profile)-1):
        tail.face([(x,profile[0][1],profile[0][0]),
                   (x,profile[i][1],profile[i][0]),
                   (x,profile[i+1][1],profile[i+1][0])],out)
for (za,ya),(zb,yb) in zip(profile,profile[1:]+profile[:1]):
    tail.face([(-.15,ya,za),(.15,ya,za),(.15,yb,zb),(-.15,yb,zb)],(0,ya+yb-.3,za+zb+10))

engines=Section('Tiger_TwinEngines','Underside')
for sign in (-1,1):
    tube(engines,[(-6.63,sign*.98,-.20,.47,.43),
                  (-5.55,sign*.98,-.18,.56,.55),
                  (-3.30,sign*1.00,-.18,.61,.58),
                  (-1.18,sign*1.03,-.17,.52,.48),
                  (-.32,sign*1.00,-.18,.40,.38)],sides=8)

intake=Section('Tiger_Intakes','Steel')
for sign in (-1,1):
    xa,xb=sorted([sign*1.16,sign*1.74])
    # Two angular cheek intakes; front lip visible from elevated 3/4.
    intake.box(xa,-.41,-.52,xb,.47,-.18)
    intake.box(xa,-.29,-.77,xb,.38,-.53)
    dark= None

intakeDark=Section('Tiger_IntakeShadows','Steel')
for sign in (-1,1):
    xa,xb=sorted([sign*1.23,sign*1.67])
    intakeDark.box(xa,-.29,-.164,xb,.32,-.146)

exhaust=Section('Tiger_Exhaust','Steel')
for sign in (-1,1):
    cx=sign*.98; cy=-.19
    for radA,radB,zA,zB in ((.49,.49,-6.63,-6.88),(.49,.34,-6.88,-6.91)):
        for i in range(12):
            a=math.tau*i/12; b=math.tau*(i+1)/12
            quad=[]
            for r,z,theta in ((radA,zA,a),(radA,zA,b),(radB,zB,b),(radB,zB,a)):
                quad.append((cx+r*math.cos(theta),cy+r*math.sin(theta),z))
            exhaust.face(quad,(math.cos((a+b)/2),math.sin((a+b)/2),-.05))
    cap(exhaust,ring(-6.90,cx,cy,.32,.32,12),(0,0,-1))

canopy=Section('Tiger_Canopy','Glass')
# Faceted transparent-looking (opaque shader) bubble, no fake baked lighting.
hood=[(.80,.43,.86,1.04),(1.22,.59,.83,1.36),
      (2.66,.53,.83,1.45),(3.57,.25,.77,1.16),(3.99,.055,.66,.78)]
def arch(z,half,base,top):
    return [(half*math.cos(math.pi-i*math.pi/6),
             base+(top-base)*math.sin(math.pi-i*math.pi/6),z)
            for i in range(7)]
hoods=[arch(*h) for h in hood]
for a,b in zip(hoods,hoods[1:]):
    for i in range(6):
        canopy.face([a[i],a[i+1],b[i+1],b[i]],(0,1,0))
cap(canopy,hoods[0],(0,0,-1))
cap(canopy,hoods[-1],(0,0,1))

trim=Section('Tiger_Trim','Steel')
# 2 canopy hoops, minimal dark steel banding. Small fins without micro-details.
for z,width,base,top in [(1.14,.59,.84,1.30),(3.56,.26,.77,1.16)]:
    points=arch(z,width,base,top)
    for a,b in zip(points,points[1:]):
        n=(0,.8,0)
        trim.face([a,b,(b[0],b[1]+.065,b[2]+.075),(a[0],a[1]+.065,a[2]+.075)],n)
# Structural reinforcing chord as visible dark spine behind canopy.
trim.box(-.17,.60,-3.85,.17,.80,.70)

acc=Section('Tiger_Identification','Accent')
# One wingtip paint stripe per side + tail recognition plate: no micro-font.
for sign in (-1,1):
    aa=sign*4.62; bb=sign*5.06
    # Offset atop wing to avoid z-fighting. Stripe follows wing taper.
    acc.face([(aa,.24,-1.67),(bb,.235,-1.90),
              (bb,.235,-2.07),(aa,.24,-1.98)],(0,1,0))
# Big readable short orange stripe on top vertical fin.
acc.face([(-.158,2.12,-4.88),(-.158,2.47,-4.97),
          (-.158,2.53,-5.25),(-.158,2.12,-5.20)],(-1,0,0))
acc.face([(.158,2.12,-4.88),(.158,2.47,-4.97),
          (.158,2.53,-5.25),(.158,2.12,-5.20)],(1,0,0))

PARTS=[hull,wings,tail,engines,intake,intakeDark,exhaust,canopy,trim,acc]


def save_blender():
    import bpy
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for key,colour in PALETTE.items():
        mat=bpy.data.materials.new('D1_'+key)
        mat.diffuse_color=(*colour,1)
        mat.use_nodes=True
        node=mat.node_tree.nodes.get('Principled BSDF')
        node.inputs['Base Color'].default_value=(*colour,1)
        node.inputs['Metallic'].default_value=0
        node.inputs['Roughness'].default_value=.9
    for part in PARTS:
        verts=[]; faces=[]
        for tri in part.triangles:
            n=len(verts)
            # Blender Z-up, Unity OBJ Y-up. Rotate +90 degrees around X.
            verts.extend((x,-z,y) for x,y,z in tri)
            faces.append((n,n+1,n+2))
        mesh=bpy.data.meshes.new(part.name)
        mesh.from_pydata(verts,[],faces)
        mesh.materials.append(bpy.data.materials['D1_'+part.material])
        mesh.update()
        obj=bpy.data.objects.new(part.name,mesh)
        bpy.context.collection.objects.link(obj)
    bpy.context.scene.unit_settings.system='METRIC'
    bpy.context.scene.unit_settings.scale_length=1.0
    bpy.context.preferences.filepaths.save_version=0
    BLEND.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    print('Editable Blender file: ',BLEND)


def main():
    for part in PARTS:
        assert part.triangles,part.name
        part.write_obj()
    assert max(abs(pt[0]) for part in PARTS for tri in part.triangles for pt in tri) > 5.25
    try:import bpy
    except ImportError:print('Blender is optional for OBJ output; run under Blender to generate .blend.')
    else:save_blender()


if __name__=='__main__':main()
