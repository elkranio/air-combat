#!/usr/bin/env python3
"""UD-01 Moth: original fictional arcade target prop, visual geometry only.
Game coordinates +Y up, +Z forward. No functional or internal geometry.
Run with Python for OBJ, or Blender Python for an editable source blend.
"""
from pathlib import Path
from math import sin, cos, pi
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Aircraft/UD01_Moth'
COLORS = {'Hull': (.14,.15,.16), 'Wing': (.075,.08,.09), 'Dark': (.045,.05,.06), 'Rotor': (.045,.05,.06), 'Mark': (.86,.055,.045)}
MESH = {k: [] for k in COLORS}

def face(part, pts, normal):
    a,b,c = pts[:3]
    u = [b[i]-a[i] for i in range(3)]; v = [c[i]-a[i] for i in range(3)]
    n = (u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0])
    if sum(n[i]*normal[i] for i in range(3)) < 0: pts = pts[::-1]
    for i in range(1,len(pts)-1): MESH[part].append((pts[0],pts[i],pts[i+1]))

def slab(part, outline, lo, hi):
    # Flat decorative extrusions with constant thickness.
    a=[(x,lo,z) for x,z in outline]; b=[(x,hi,z) for x,z in outline]
    face(part,a,(0,-1,0)); face(part,b,(0,1,0))
    area=sum(x*z2-x2*z for (x,z),(x2,z2) in zip(outline,outline[1:]+outline[:1]))
    sign=1 if area>0 else -1
    for i in range(len(a)):
        j=(i+1)%len(a); x,z=outline[i]; x2,z2=outline[j]
        face(part,[a[i],a[j],b[j],b[i]],(sign*(z2-z),0,-sign*(x2-x)))

def shell(part, rings):
    # Faceted closed ornamental body; 8-sided cross sections.
    sections=[]
    for z,w,h,y in rings:
        sections.append([(w*cos(pi/4*i),y+h*sin(pi/4*i),z) for i in range(8)])
    for a,b in zip(sections,sections[1:]):
        for i in range(8):
            j=(i+1)%8; ang=(i+.5)*pi/4
            face(part,[a[i],a[j],b[j],b[i]],(cos(ang),sin(ang),0))
    face(part,sections[0],(0,0,-1)); face(part,sections[-1],(0,0,1))

# Exaggerated game silhouette: broad clipped triangular panel, tall center shape.
# These are graphic masses for readability, not an aerodynamic design.
for s in (-1,1):
    slab('Wing',[(s*x,z) for x,z in [(0,2.05),(.55,1.60),(2.70,-.94),(2.75,-1.70),(1.30,-1.43),(0,-1.85)]],-.10,.12)
    slab('Dark',[(s*x,z) for x,z in [(2.37,-.67),(2.70,-.94),(2.75,-1.70),(2.35,-1.62)]],-.105,.125)
    slab('Mark',[(s*x,z) for x,z in [(.99,.58),(1.25,.58),(2.32,-.90),(2.06,-.90)]],.123,.137)
    # Bold decorative upright tabs, not functional control surfaces.
    x=s*2.53
    face('Dark',[(x-.09,.12,-.86),(x-.09,.65,-1.29),(x-.09,.65,-1.65),(x-.09,.12,-1.72)],(-1,0,0))
    face('Dark',[(x+.09,.12,-.86),(x+.09,.65,-1.29),(x+.09,.65,-1.65),(x+.09,.12,-1.72)],(1,0,0))
    for (y,z),(y2,z2),n in [((.12,-.86),(.65,-1.29),(0,1,1)),((.65,-1.29),(.65,-1.65),(0,1,0)),((.65,-1.65),(.12,-1.72),(0,0,-1)),((.12,-1.72),(.12,-.86),(0,-1,0))]:
        face('Dark',[(x-.09,y,z),(x+.09,y,z),(x+.09,y2,z2),(x-.09,y2,z2)],n)
shell('Hull',[(-1.86,.36,.28,0),(-1.34,.64,.45,.03),(.94,.58,.43,.03),(1.70,.42,.30,.02),(2.35,.17,.16,0)])
shell('Dark',[(2.35,.17,.16,0),(2.69,.035,.035,0)])
# Rear graphic hub and separate cosmetic rotor mesh; Unity animates the visual.
shell('Dark',[(-2.30,.14,.14,0),(-2.12,.27,.27,0),(-1.86,.27,.27,0)])
for s in (-1,1):
    slab('Rotor',[(s*.14,-2.29),(s*1.00,-2.37),(s*1.12,-2.26),(s*.14,-2.15)],-.055,.055)

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    for name,tris in MESH.items():
        lines=['# UD-01 Moth; fictional game prop; +Z forward, +Y up',f'o Moth_{name}','s off']
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
        (OUT/f'Moth_{name}.obj').write_text('\n'.join(lines)+'\n')
        print(f'{name}: {len(tris)} triangles')
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for name,tris in MESH.items():
        mesh=bpy.data.meshes.new('Moth_'+name)
        verts=[(x,-z,y) for tri in tris for x,y,z in tri]
        mesh.from_pydata(verts,[],[tuple(range(i,i+3)) for i in range(0,len(verts),3)])
        mesh.update(); obj=bpy.data.objects.new(mesh.name,mesh); bpy.context.collection.objects.link(obj)
        mat=bpy.data.materials.new('UD01_'+name); mat.diffuse_color=(*COLORS[name],1); mesh.materials.append(mat)
    path=ROOT/'Resources/Blender/UD01_Moth.blend'; path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))

if __name__=='__main__': main()
