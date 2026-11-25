"""ENV-01: five low-poly sand relief modules, in Unity Y-up coordinates.
Python exports the OBJ library; Blender --background --python also saves an
editable .blend library (Z-up). No external packages required for OBJ export.
"""
from math import cos, sin, pi, sqrt, exp
from pathlib import Path
from random import Random

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Environment/Mission01_FirstSortie/ENV01_ShallowRelief'
# name, v0 X/Z bounds (keep existing placement clearance), height, grid, seed,
# plan asymmetry, crest lobes (u, v, width, depth, weight).
SPECS = [
    ('ENV01_A_SandHump', (-25.900422,34.503234,-24.141421,22.753328), 3.5, (8,7), 61, .18,
     [(-.22,.12,.66,.60,1),(.38,-.10,.49,.43,.65)]),
    ('ENV01_B_LongDrift', (-72.018758,61.038692,-22.121087,22.143782), 4.9, (12,6), 72, -.26,
     [(-.38,-.12,.68,.45,.78),(.25,.22,.68,.42,1)]),
    ('ENV01_C_BroadRise', (-73.837,70.697,-43.940,49.353), 7.7, (11,9), 83, .23,
     [(-.30,.18,.64,.55,1),(.35,-.18,.63,.65,.86)]),
    ('ENV01_D_DoubleCrest', (-49.871,49.058,-30.397,36.680), 5.8, (10,7), 94, -.19,
     [(-.44,-.12,.36,.51,1),(.43,.16,.38,.48,.91)]),
    ('ENV01_E_WindRidge', (-77.684,82.593,-30.525,35.027), 8.4, (13,8), 105, .30,
     [(-.46,-.18,.54,.47,.80),(.10,.18,.58,.44,1),(.49,.04,.44,.50,.77)]),
]
# Exact UV of original Mission01 ground: palette texel (0,0), not a new color.
UV = (0.0078125, 0.015625)


def model(spec):
    name, bounds, h, (nx,nz), seed, lean, lobes = spec
    rng = Random(seed)
    verts, faces, weights = [], [], []
    phase = rng.uniform(0,2*pi)
    # Jittered rectangular lattice mapped into an asymmetric rounded outline.
    # All inner vertices have local neighbours: no radial fan or shared apex.
    for j in range(nz+1):
        for i in range(nx+1):
            u, v = 2*i/nx-1, 2*j/nz-1
            if 0<i<nx: u += rng.uniform(-.25,.25)*2/nx
            if 0<j<nz: v += rng.uniform(-.25,.25)*2/nz
            x = u*sqrt(1-v*v/2)
            z = v*sqrt(1-u*u/2)
            # Broad asymmetric curves, not high frequency edge noise.
            x += lean*.19*(1-x*x)*sin(2.4*z+phase)
            z = z*(1+lean*.43*x+.09*sin(3*x+phase)) + lean*.23*(1-x*x)
            # Continuous envelope vanishes with zero slope at the boundary.
            edge = max(0, (1-u*u)*(1-v*v))**1.8
            lobe = sum(weight*exp(-((u-cu)/wu)**2-((v-cv)/wv)**2)
                       for cu,cv,wu,wv,weight in lobes)
            # For the double crest keep a visible but shallow saddle.
            weights.append(edge*lobe)
            verts.append((x,0,z))
    xmin,xmax,zmin,zmax = bounds
    ax,bx = min(p[0] for p in verts),max(p[0] for p in verts)
    az,bz = min(p[2] for p in verts),max(p[2] for p in verts)
    peak = max(weights)
    # Preserve pivots and stay inside the original conservative bounding boxes.
    def fit(v,a,b,lo,hi): return (lo+hi)/2+(hi-lo)*.495*(2*(v-a)/(b-a)-1)
    verts = [(fit(x,ax,bx,xmin,xmax), -.12+(h+.12)*weight/peak,
              fit(z,az,bz,zmin,zmax)) for (x,_,z),weight in zip(verts,weights)]
    for j in range(nz):
        for i in range(nx):
            a=j*(nx+1)+i; b=a+1; c=b+nx+1; d=a+nx+1
            # Pick the shorter diagonal, with a seeded tie-break to avoid stripes.
            def length2(p,q): return sum((verts[p][k]-verts[q][k])**2 for k in (0,2))
            ac,bd=length2(a,c),length2(b,d)
            use_ac = ac < bd if abs(ac-bd)>.08*max(ac,bd) else rng.random()<.5
            faces.extend([(a,d,c),(a,c,b)] if use_ac else [(a,d,b),(b,d,c)])
    for i,(a,b,c) in enumerate(faces):
        # Reject folded faces instead of masking them by reversing normals.
        p,q,r = (verts[k] for k in (a,b,c))
        ny=(q[2]-p[2])*(r[0]-p[0])-(q[0]-p[0])*(r[2]-p[2])
        assert ny>1e-5, (name,i,ny)
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
