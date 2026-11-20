"""V17: connected escarpment height patches; no baked lighting.
Blender is the editable geometry preview. Unity merges the same height samples
into the existing terrain, instead of placing an FBX on top of it.
Run with Blender or normal Python (--data-only) to export deterministic samples.
"""
import json
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Resources' / 'Blender'
SOURCE = OUT / 'FrontierStrata.blend'
DATA = OUT / 'FrontierStrata.json'
STEP = 1.25
# Bounds on the original 5-unit grid, strictly inside the validated V16 footprints.
# id, minX,maxX,minZ,maxZ, maximum relief, seed, shape variant.
SITES = (
    ('BrokenMesa', -125, 20, 145, 225, 27, 104, 0),
    ('EastEscarpment', 325, 405, 50, 185, 29, 217, 1),
    ('SplitRidge', 100, 245, -230, -155, 25, 391, 2),
    ('WesternButte', -260, -180, 15, 115, 26, 482, 3),
)
# Explicit asymmetric strike profiles. Adjacent cores overlap but their rims and
# summit heights differ, giving rock corridors, saddles and broken terraces.
POLYGONS = (
    ((-.77,-.30),(-.58,-.63),(-.13,-.61),(.08,-.32),(-.02,.04),(.00,.42),(-.35,.63),(-.68,.39)),
    ((-.18,-.43),(.18,-.62),(.58,-.45),(.75,-.15),(.65,.36),(.30,.61),(.04,.40),(-.12,.12)),
    ((-.78,-.30),(-.57,-.57),(-.13,-.47),(.26,-.58),(.68,-.31),(.78,.06),(.43,.30),(.15,.18),(-.12,.40),(-.51,.46)),
    ((-.43,-.59),(-.10,-.66),(.18,-.40),(.54,-.32),(.60,.10),(.32,.57),(-.12,.63),(-.52,.31),(-.64,-.12)),
)


def smooth(a, b, v):
    t = max(0., min(1., (v-a)/(b-a)))
    return t*t*(3-2*t)


def signed_distance(x, y, poly):
    inside = False
    nearest = 100.
    for i, (ax, ay) in enumerate(poly):
        bx, by = poly[(i+1) % len(poly)]
        dx, dy = bx-ax, by-ay
        t = max(0., min(1., ((x-ax)*dx+(y-ay)*dy)/(dx*dx+dy*dy)))
        nearest = min(nearest, math.hypot(x-ax-t*dx, y-ay-t*dy))
        if (ay > y) != (by > y) and x < (bx-ax)*(y-ay)/(by-ay)+ax:
            inside = not inside
    return nearest if inside else -nearest


def height(x, y, variant, seed):
    # Input is in [-1,1]. Local x follows the geological strike, even for north/south cliffs.
    edge = 1-max(abs(x), abs(y))
    skirt = smooth(0,.22,edge)
    if skirt <= 0: return 0.
    if variant in (1,3): x,y = y,-x
    phase = seed*.071
    wx = x + .035*math.sin(11*y+phase) + .026*math.sin(19*x+9*y)
    wy = y + .047*math.sin(8*x-phase) + .018*math.cos(21*x-5*y)
    def mass(poly, scale, shiftx=0., shifty=0.):
        d = signed_distance(wx-shiftx,wy-shifty,poly)
        # A long irregular talus apron precedes the main, relatively steep cliff wall.
        # On the backslope the wall is broader, rather than identical radial bands.
        back = smooth(-.5,.55,wy)
        ramp = .23*smooth(-.26,-.015,d)
        wall = .59*smooth(-.025,.095+.10*back,d)
        rim = .18*smooth(.10,.29,d)
        bed = .93+.035*math.sin(6*wx+phase)+.045*math.cos(4*wy-2*wx)
        return scale*(ramp+wall+rim)*bed
    if variant == 0:
        a,b = mass(POLYGONS[0],1),mass(POLYGONS[1],.85)
    elif variant == 1:
        a,b = mass(POLYGONS[2],1),mass(POLYGONS[1],.66,-.05,.12)
    elif variant == 2:
        a,b = mass(POLYGONS[0],.85,-.03,-.03),mass(POLYGONS[1],1,.03,.04)
    else:
        a,b = mass(POLYGONS[3],1),mass(POLYGONS[0],.58,.09,-.09)
    # Soft union, not one radial summit. Small asymmetric cuts read as erosion channels.
    h = max(a,b) + max(0.,.10-abs(a-b))**2/.4 * smooth(.01,.19,max(a,b))
    gully = math.exp(-((wy-.16*math.sin(5*wx+phase)+.03)/.055)**2)
    grit = .022*math.sin(22*wx+7*wy+phase)*math.sin(17*wy-3*wx)
    h += (grit-.055*gully)*smooth(.12,.5,h)
    return max(0.,min(1., h*skirt))


def build():
    fields=[]
    for name,x0,x1,z0,z1,relief,seed,variant in SITES:
        nx,nz=round((x1-x0)/STEP)+1,round((z1-z0)/STEP)+1
        heights=[round(relief*height(2*x/(nx-1)-1,2*z/(nz-1)-1,variant,seed),5)
                 for z in range(nz) for x in range(nx)]
        assert all(0<=v<=relief for v in heights)
        assert all(heights[z*nx+x]==0 for z in range(nz) for x in range(nx)
                   if x in (0,nx-1) or z in (0,nz-1))
        fields.append(dict(name=name,x0=x0,x1=x1,z0=z0,z1=z1,nx=nx,nz=nz,heights=heights))
    return dict(version=1,step=STEP,fields=fields)


def blend_preview(data):
    import bpy
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    bpy.context.preferences.filepaths.save_version=0
    mat=bpy.data.materials.new('Sandstone preview (Unity uses world albedo)')
    mat.diffuse_color=(.54,.37,.24,1)
    for f in data['fields']:
        nx,nz=f['nx'],f['nz']; w=f['x1']-f['x0']; d=f['z1']-f['z0']
        vertices=[(x*STEP-w/2,z*STEP-d/2,f['heights'][z*nx+x]) for z in range(nz) for x in range(nx)]
        faces=[]
        for z in range(nz-1):
            for x in range(nx-1):
                a=z*nx+x; b=a+nx
                faces.extend(((a,a+1,b),(a+1,b+1,b)))
        mesh=bpy.data.meshes.new(f['name']); mesh.from_pydata(vertices,[],faces); mesh.update()
        if mesh.validate(): raise RuntimeError('Invalid geometry: '+f['name'])
        obj=bpy.data.objects.new(f['name'],mesh); bpy.context.scene.collection.objects.link(obj)
        obj.location=((f['x0']+f['x1'])/2,(f['z0']+f['z1'])/2,0)
        mesh.materials.append(mat)
        for face in mesh.polygons: face.use_smooth=True
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    data=build()
    DATA.write_text(json.dumps(data,separators=(',',':'))+'\n',encoding='utf-8')
    if '--data-only' not in sys.argv: blend_preview(data)
    for f in data['fields']:
        print(f"{f['name']}: {f['nx']*f['nz']} vertices, relief {max(f['heights']):.1f}")
    print('V17 geometry ready:',DATA,flush=True)

if __name__=='__main__': main()
