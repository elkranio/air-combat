"""ENV-01: five low-poly sand relief modules, in Unity Y-up coordinates.
Python exports the OBJ library; Blender --background --python also saves an
editable .blend library (Z-up). No external packages required for OBJ export.
"""
from math import cos, sin, pi, sqrt
from pathlib import Path
from random import Random

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Environment/Mission01_FirstSortie/ENV01_ShallowRelief'
# name, half-width, half-depth, peak height, perimeter count, seed, ridge bias
SPECS = [
    ('ENV01_A_SandHump', 30, 23, 5.0, 10, 61, 0.12),
    ('ENV01_B_LongDrift', 63, 21, 7.0, 14, 72, 0.36),
    ('ENV01_C_BroadRise', 67, 47, 11.0, 14, 83, -0.18),
    ('ENV01_D_DoubleCrest', 48, 32, 9.0, 12, 94, 0.28),
    ('ENV01_E_WindRidge', 77, 31, 12.0, 16, 105, -0.32),
]
# Exact UV of original Mission01 ground: palette texel (0,0), not a new color.
UV = (0.0078125, 0.015625)


def model(spec):
    name, w, d, h, n, seed, lean = spec
    rng = Random(seed)
    verts, faces = [], []
    angles = [2*pi*(i+rng.uniform(-.20,.20))/n for i in range(n)]
    phase = rng.uniform(0,2*pi)
    radii = [1 + .12*sin(3*a+phase) + .07*cos(5*a-phase) + rng.uniform(-.045,.045) for a in angles]
    # Open decorative surface: buried perimeter, gently rising shoulder, irregular crown.
    # No bottom or vertical walls: the sand plane closes the silhouette.
    for ring in range(3):
        for i,a in enumerate(angles):
            r = radii[i]
            if ring == 0:
                x, z, y = w*r*cos(a), d*r*sin(a), -.12
            elif ring == 1:
                x, z = w*r*.70*cos(a), d*r*.70*sin(a)
                y = h*(.15+.12*(1+sin(a+phase))/2+rng.uniform(-.035,.035))
            else:
                x = w*(r*.35*cos(a)+lean*.36)
                z = d*(r*.34*sin(a)+lean*.18)
                crest = .70+.14*sin(2*a+phase)+rng.uniform(-.045,.045)
                if name.endswith('DoubleCrest'):
                    crest = .64+.28*abs(cos(a))-.12*abs(sin(a))
                y = h*crest
            verts.append((x,y,z))
    verts.append((w*lean*.24, h*(.74 if name.endswith('DoubleCrest') else 1), d*lean*.15))
    for k in range(2):
        for i in range(n):
            j=(i+1)%n; a=k*n+i; b=k*n+j; c=(k+1)*n+j; e=(k+1)*n+i
            faces.extend([(a,e,b),(b,e,c)] if (i+k)%2 else [(a,e,c),(a,c,b)])
    for i in range(n):
        faces.append((2*n+i,3*n,2*n+(i+1)%n))
    # Correct every face to upward-facing and reject folded/degenerate geometry.
    for i,(a,b,c) in enumerate(faces):
        p,q,r = (verts[j] for j in (a,b,c))
        ny=(q[2]-p[2])*(r[0]-p[0])-(q[0]-p[0])*(r[2]-p[2])
        assert abs(ny)>1e-5, (name,i)
        if ny<0: faces[i]=(a,c,b)
    return verts,faces


def normal(v,f):
    a,b,c=(v[i] for i in f); u=tuple(b[i]-a[i] for i in range(3)); t=tuple(c[i]-a[i] for i in range(3))
    n=(u[1]*t[2]-u[2]*t[1],u[2]*t[0]-u[0]*t[2],u[0]*t[1]-u[1]*t[0]); l=sqrt(sum(x*x for x in n))
    return tuple(x/l for x in n)


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    library=[]
    for spec in SPECS:
        name=spec[0]; v,f=model(spec); library.append((name,v,f))
        lines=['# ENV01 shallow decorative relief; Unity Y-up',f'o {name}','s off']
        lines += ['v %.6f %.6f %.6f'%p for p in v]
        lines += ['vt %.7f %.7f'%UV]
        lines += ['vn %.8f %.8f %.8f'%normal(v,face) for face in f]
        lines += ['f '+' '.join(f'{j+1}/1/{i}' for j in face) for i,face in enumerate(f,1)]
        (OUT/f'{name}.obj').write_text('\n'.join(lines)+'\n',encoding='utf-8')
        print(name,len(v),'vertices',len(f),'triangles', 'height',max(p[1] for p in v))
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    mat=bpy.data.materials.new('Preview sand'); mat.diffuse_color=(.66,.54,.38,1)
    for i,(name,v,f) in enumerate(library):
        mesh=bpy.data.meshes.new(name); mesh.from_pydata([(x,-z,y) for x,y,z in v],[],f);mesh.update()
        uv=mesh.uv_layers.new(name='GroundPalette')
        for item in uv.data: item.uv=UV
        mesh.materials.append(mat)
        obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);obj.location=((i%3)*210,(i//3)*150,0)
    dest=ROOT/'Resources/Blender/ENV01_ShallowRelief.blend';dest.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(dest))

if __name__=='__main__': main()
