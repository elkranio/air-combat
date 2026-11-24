#!/usr/bin/env python3
"""PF-02 Lynx: fictional stylized arcade fighter exterior, visual trial only.
Game coordinates +Y up, +Z forward. Python writes OBJ; Blender also writes source .blend.
"""
from pathlib import Path
from math import cos, sin, pi

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Aircraft/PF02_Lynx'
COLORS = {'Hull': (.19215687,.3254902,.41960785), 'Wing': (.19215687,.3254902,.41960785),
          'Glass': (.08,.70,.80), 'Dark': (.105,.12,.135), 'Mark': (.8784314,.4745098,.2509804)}
MESH = {k: [] for k in COLORS}

def mean(points):
    return tuple(sum(p[i] for p in points)/len(points) for i in range(3))

def face(part, pts, inside):
    # Orient each face away from a point inside its local solid.
    a,b,c = pts[:3]
    u = [b[i]-a[i] for i in range(3)]; v = [c[i]-a[i] for i in range(3)]
    n = (u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0])
    if sum(n[i]*(inside[i]-a[i]) for i in range(3)) > 0: pts = pts[::-1]
    for i in range(1,len(pts)-1): MESH[part].append((pts[0],pts[i],pts[i+1]))

def loft(part, rings, sides=10):
    # z, center x, center y, half width, half height; ordered rear to front.
    sections = [[(x+w*cos(2*pi*i/sides),y+h*sin(2*pi*i/sides),z)
                 for i in range(sides)] for z,x,y,w,h in rings]
    for a,b in zip(sections,sections[1:]):
        inside = mean(a+b)
        for i in range(sides):
            j=(i+1)%sides
            face(part,[a[i],a[j],b[j],b[i]],inside)
    face(part,sections[0],mean(sections[1]))
    face(part,sections[-1],mean(sections[-2]))

def panel(part, top, bottom):
    # Convex ornamental panels, with tapered thickness.
    inside=mean(top+bottom)
    face(part,top,inside); face(part,bottom,inside)
    for i in range(len(top)):
        j=(i+1)%len(top)
        face(part,[top[i],top[j],bottom[j],bottom[i]],inside)

def wing(part, sign, outline):
    # x, z, upper y, lower y; a flat-shaded graphic wing panel.
    panel(part,[(sign*x,u,z) for x,z,u,l in outline],
               [(sign*x,l,z) for x,z,u,l in outline])

def painted_wing(sign, outline, tip_start=3.85):
    # Split the existing outer surface triangles at a spanwise paint boundary.
    # Both materials share exact seam coordinates; no raised decal or overlapping faces.
    start = len(MESH['Wing'])
    wing('Wing', sign, outline)
    triangles = MESH['Wing'][start:]
    del MESH['Wing'][start:]

    def clip(tri, outer):
        result = []
        for a, b in zip(tri, tri[1:] + tri[:1]):
            da, db = sign*a[0] - tip_start, sign*b[0] - tip_start
            a_in = da >= 0 if outer else da <= 0
            b_in = db >= 0 if outer else db <= 0
            if a_in:
                result.append(a)
            if a_in != b_in:
                t = da / (da - db)
                result.append((sign*tip_start, a[1]+t*(b[1]-a[1]),
                               a[2]+t*(b[2]-a[2])))
        return result

    for tri in triangles:
        for part, outer in (('Wing', False), ('Mark', True)):
            pts = clip(tri, outer)
            for i in range(1, len(pts)-1):
                MESH[part].append((pts[0], pts[i], pts[i+1]))


# Long central fuselage and nose, tapering toward the tail between the pods.
loft('Hull',[(-5.7,0,.02,.49,.22),(-4.2,0,.06,.84,.39),
             (-1.7,0,.12,1.08,.59),(1.05,0,.16,1.08,.69),
             (2.6,0,.14,.78,.64),(4.4,0,.08,.55,.50),
             (5.8,0,.03,.34,.34),(6.65,0,.01,.14,.16)])
loft('Dark',[(6.65,0,.01,.14,.16),(7.22,0,0,.025,.035)])
# Raised elongated tinted canopy. Opaque material, no modeled interior.
loft('Glass',[(1.40,0,.69,.26,.13),(1.86,0,.76,.48,.35),
              (2.8,0,.77,.52,.46),(3.65,0,.69,.43,.36),
              (4.4,0,.51,.27,.16),(4.68,0,.44,.14,.06)])
# A narrow canopy frame, visually separating the two lobes.
loft('Dark',[(2.72,0,.77,.525,.466),(2.79,0,.77,.525,.466)])
for s in (-1,1):
    # Broad shoulders, two distinct rear pods, dark cosmetic intake faces.
    loft('Hull',[(-5.55,s*1.30,-.16,.73,.59),(-4.65,s*1.32,-.13,.80,.67),
                 (-2.7,s*1.33,-.11,.83,.70),(-.30,s*1.27,-.12,.82,.65),
                 (1.35,s*1.24,-.15,.72,.56),(2.20,s*1.06,-.20,.48,.40)])
    # Dark exhaust collars, closed graphic geometry only.
    loft('Dark',[(-6.23,s*1.30,-.16,.58,.48),(-5.96,s*1.30,-.16,.69,.56),
                 (-5.54,s*1.30,-.16,.74,.60)])
    # Angular forward shoulder / glove and wing panels.
    wing('Hull',s,[(.75,2.10,.26,-.28),(2.12,.68,.22,-.22),
                   (2.64,-1.0,.14,-.15),(1.10,-2.44,.31,-.30)])
    painted_wing(s,[(1.90,.46,.19,-.18),(4.75,-1.72,.035,-.055),
                   (4.78,-2.30,.025,-.045),(2.55,-3.05,.12,-.12),(1.88,-2.32,.17,-.16)])
    # Distinct rear horizontal stabilizers.
    wing('Wing',s,[(1.40,-3.85,.13,-.12),(3.35,-4.87,.08,-.08),
                   (3.40,-5.43,.04,-.05),(1.55,-6.01,.12,-.12)])
    # Tall, slightly canted paired fins: original stylized outline.
    profile=[(.28,-3.15),(1.95,-4.38),(2.06,-5.25),(.29,-5.92)]
    def finpoint(y,z,side): return (s*(1.48+.22*y)+side*.09,y,z)
    panel('Hull',[finpoint(y,z,1) for y,z in profile],
                 [finpoint(y,z,-1) for y,z in profile])
    # Raised dark shoulder markings and an inset-looking forward face.
    loft('Dark',[(1.42,s*1.32,-.14,.47,.39),(1.52,s*1.32,-.14,.47,.39)])


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    for name,tris in MESH.items():
        lines=['# PF-02 Lynx; fictional arcade fighter; +Z forward, +Y up',f'o Lynx_{name}','s off']
        verts={}; faces=[]
        for tri in tris:
            ids=[]
            for p in tri:
                p=tuple(round(x,6) for x in p)
                if p not in verts: verts[p]=len(verts)+1
                ids.append(verts[p])
            faces.append('f '+' '.join(map(str,ids)))
        lines.extend('v '+' '.join(f'{v:.6f}' for v in p) for p in verts)
        lines.extend(faces)
        (OUT/f'Lynx_{name}.obj').write_text('\n'.join(lines)+'\n')
        print(f'{name}: {len(tris)} triangles')
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for name,tris in MESH.items():
        mesh=bpy.data.meshes.new('Lynx_'+name)
        verts=[(x,-z,y) for tri in tris for x,y,z in tri]
        mesh.from_pydata(verts,[],[tuple(range(i,i+3)) for i in range(0,len(verts),3)])
        mesh.update(); obj=bpy.data.objects.new(mesh.name,mesh); bpy.context.collection.objects.link(obj)
        mat=bpy.data.materials.new('PF02_'+name); mat.diffuse_color=(*COLORS[name],1); mesh.materials.append(mat)
    path=ROOT/'Resources/Blender/PF02_Lynx.blend'; path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))

if __name__=='__main__': main()
