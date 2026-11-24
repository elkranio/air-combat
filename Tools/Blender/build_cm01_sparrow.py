#!/usr/bin/env python3
"""CM-01 Sparrow: fictional visual-only arcade target, not a functional weapon.
Rebuild OBJ sections with Python; optionally run in Blender to save a source blend.
Coordinates use the game's +Z forward, +Y up convention.
"""
from pathlib import Path
from math import sin, cos, pi

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Aircraft/CM01_Sparrow'
COLORS = {'Hull': (.64,.69,.70), 'Graphite': (.19,.23,.25),
          'Wings': (.51,.57,.59), 'Marking': (.78,.40,.17)}
MESH = {k: [] for k in COLORS}

def face(part, pts, normal):
    a,b,c = pts[:3]
    u = [b[i]-a[i] for i in range(3)]
    v = [c[i]-a[i] for i in range(3)]
    n = (u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0])
    if sum(n[i]*normal[i] for i in range(3)) < 0: pts = pts[::-1]
    for i in range(1,len(pts)-1): MESH[part].append((pts[0],pts[i],pts[i+1]))

def tube(part, stations, sides=16, caps=(True,True)):
    rings = [[(r*cos(2*pi*i/sides), r*sin(2*pi*i/sides), z) for i in range(sides)] for z,r in stations]
    for a,b in zip(rings,rings[1:]):
        for i in range(sides):
            j=(i+1)%sides; ang=2*pi*(i+.5)/sides
            face(part,[a[i],a[j],b[j],b[i]],(cos(ang),sin(ang),0))
    for idx,sign,enabled in ((0,-1,caps[0]),(-1,1,caps[1])):
        if enabled:
            r=rings[idx]; z=stations[idx][0]
            for i in range(sides): face(part,[(0,0,z),r[i],r[(i+1)%sides]],(0,0,sign))

def fin(part, radial, stations):
    # Stylized silhouette only; broad beveled slabs rather than engineered airfoils.
    x,y=radial; tangent=(-y,x)
    rings=[]
    for r,front,back,t in stations:
        rings.append([(x*r+tangent[0]*t*s,y*r+tangent[1]*t*s,z) for s,z in ((1,front),(1,back),(-1,back),(-1,front))])
    for a,b in zip(rings,rings[1:]):
        for i,n in enumerate(((tangent[0],tangent[1],0),(0,0,-1),(-tangent[0],-tangent[1],0),(0,0,1))):
            j=(i+1)%4;face(part,[a[i],a[j],b[j],b[i]],n)
    face(part,rings[-1],(x,y,0)); face(part,rings[0],(-x,-y,0))

# Broad and deliberately exaggerated game silhouette. No internal or functional geometry.
tube('Hull',[(-2.88,.39),(-2.52,.56),(-1.85,.65),(1.75,.65),(2.20,.59)],caps=(True,False))
tube('Graphite',[(2.20,.59),(2.60,.48),(2.90,.29),(3.05,.12),(3.07,.055)],caps=(False,True))
tube('Graphite',[(-3.12,.38),(-3.02,.51),(-2.83,.51),(-2.75,.44)])
# Wide amber ID stripe and narrow rear collar, modeled as surface overlays.
tube('Marking',[(1.49,.657),(1.76,.657)],caps=(False,False))
tube('Graphite',[(-1.99,.654),(-1.86,.657)],caps=(False,False))
for s in (-1,1):
    fin('Wings',(s,0),[(.49,.80,-.55,.11),(1.05,.69,-.58,.10),(2.12,.31,-.53,.06),(2.25,.19,-.44,.035)])
    fin('Wings',(s,0),[(.37,-1.98,-2.85,.09),(.96,-2.18,-2.92,.06),(1.32,-2.48,-2.91,.035)])
fin('Wings',(0,1),[(.40,-1.85,-2.89,.10),(.98,-2.12,-2.94,.065),(1.30,-2.48,-2.94,.035)])
fin('Wings',(0,-1),[(.40,-2.00,-2.83,.075),(.77,-2.36,-2.92,.035)])

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    for name, tris in MESH.items():
        lines=[f'# CM-01 Sparrow; fictional game prop; +Z forward, +Y up',f'o Sparrow_{name}','s off']
        verts={}; faces=[]
        for tri in tris:
            idx=[]
            for p in tri:
                p=tuple(round(x,6) for x in p)
                if p not in verts: verts[p]=len(verts)+1
                idx.append(verts[p])
            faces.append('f '+' '.join(map(str,idx)))
        lines.extend('v '+' '.join(f'{v:.6f}' for v in p) for p in verts)
        lines.extend(faces)
        (OUT/f'Sparrow_{name}.obj').write_text('\n'.join(lines)+'\n')
        print(f'{name}: {len(tris)} triangles')
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for name,tris in MESH.items():
        mesh=bpy.data.meshes.new('Sparrow_'+name)
        # Unity Y-up to Blender Z-up, with forward along -Y (proper rotation).
        vertices=[(x,-z,y) for tri in tris for x,y,z in tri]
        mesh.from_pydata(vertices,[],[tuple(range(i,i+3)) for i in range(0,len(vertices),3)])
        mesh.update(); obj=bpy.data.objects.new(mesh.name,mesh); bpy.context.collection.objects.link(obj)
        mat=bpy.data.materials.new('CM01_'+name);mat.diffuse_color=(*COLORS[name],1);mesh.materials.append(mat)
    path=ROOT/'Resources/Blender/CM01_Sparrow.blend';path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))

if __name__=='__main__': main()
