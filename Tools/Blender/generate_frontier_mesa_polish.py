"""V20: a restrained, directional sandstone normal, only for BrokenMesa.
Existing V17 terrain geometry, UV and color are not changed. Same projection as V18.
"""
import argparse
from pathlib import Path
import sys
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import generate_bake_truth_test as truth
import generate_frontier_practical_bake as practical

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Environment/FrontierMesaPolish'
SOURCE = ROOT / 'Resources/Blender/FrontierMesaPolish.blend'
W, D = practical.ROCK_W, practical.ROCK_D
smooth = truth.smooth

# Finite local beds, not long winding cracks. Centers in local metres.
# cx, cy, half length, width of exposed ledge, heading slope, relief meters.
BEDS = (
    (-38, -14, 15, 2.4, .14, .52), (-32, -5, 19, 3.1, .17, .58),
    (-40, 7, 13, 2.7, .11, .47), (-23, 15, 10, 2.3, .16, .44),
    ( 18, -10, 13, 2.8, .12, .46), ( 31, -2, 17, 3.0, .18, .60),
    ( 27, 9, 14, 2.5, .16, .49), ( 38, 17, 9, 2.1, .13, .42),
)


def exposure(x,y,field):
    u=np.clip((x+W/2)/1.25,0,field.shape[1]-1);v=np.clip((y+D/2)/1.25,0,field.shape[0]-1)
    i=np.minimum(u.astype(int),field.shape[1]-2);j=np.minimum(v.astype(int),field.shape[0]-2)
    a=u-i;b=v-j
    h=(1-a)*(1-b)*field[j,i]+a*(1-b)*field[j,i+1]+(1-a)*b*field[j+1,i]+a*b*field[j+1,i+1]
    # No added relief on the talus and outer margin. High-only relief is local.
    return smooth(3.5,13.,h)*(1-smooth(W/2-4,W/2-1,np.abs(x)))*(1-smooth(D/2-4,D/2-1,np.abs(y)))


def relief(x,y,field):
    ledges=np.zeros_like(x,dtype=float)
    for k,(cx,cy,length,width,slope,height) in enumerate(BEDS):
        along=x-cx
        # Restrained angular variation along the strike, not repeated sine contours.
        cross=y-cy-slope*along-.38*np.sin(along*.24+k*.9)
        taper=1-smooth(length*.63,length,np.abs(along))
        # Asymmetric broad ramp with a shorter steep face (a shelf, not a vein).
        shelf=smooth(-width,-width*.05,cross)*(1-smooth(width*.18,width*.64,cross))
        # Vary the height on the long axis without splitting into noisy fragments.
        amplitude=.79+.21*np.cos(along*.13+k*1.4)
        ledges += height*taper*shelf*amplitude
    # A handful of broad chipped surfaces. No network of trenches or micro sine noise.
    chips=np.zeros_like(x,dtype=float)
    for cx,cy,rx,ry,depth in ((-44,0,3.5,2.1,.20),(-19,3,4.8,2.8,.23),(15,1,4,2.4,.21),(39,4,3.6,2.5,.20)):
        xx=x-cx; yy=y-cy-.16*xx
        r=np.maximum(np.abs(xx)/rx,np.abs(yy)/ry)
        chips -= depth*(1-smooth(.55,1.0,r))
    return (ledges+chips)*exposure(x,y,field)


def main():
    import bpy
    p=argparse.ArgumentParser();p.add_argument('--device',choices=('OPTIX','CUDA','CPU'),default='OPTIX')
    p.add_argument('--size',type=int,choices=(1024,2048),default=1024);p.add_argument('--samples',type=int,default=48)
    a=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    if a.samples<1: raise ValueError('samples must be positive')
    field=practical.load_rock();OUT.mkdir(parents=True,exist_ok=True);SOURCE.parent.mkdir(parents=True,exist_ok=True)
    ready=OUT/'MesaPolish.ready.txt';ready.unlink(missing_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;bpy.context.preferences.filepaths.save_version=0
    scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
    truth.setup_device(bpy,scene,a.device);scene.cycles.samples=a.samples
    scene.render.bake.target='IMAGE_TEXTURES';scene.render.bake.margin_type='EXTEND'
    low=practical.make_object(bpy,scene,'BrokenMesa polish target',[(-W/2,-D/2,0),(W/2,-D/2,0),(W/2,D/2,0),(-W/2,D/2,0)],[(0,1,2,3)])
    uv=low.data.uv_layers.new(name='UVMap')
    for li in low.data.polygons[0].loop_indices:
        v=low.data.vertices[low.data.loops[li].vertex_index].co;uv.data[li].uv=(v.x/W+.5,v.y/D+.5)
    lm=bpy.data.materials.new('Bake target');lm.use_nodes=True;low.data.materials.append(lm)
    verts,faces=practical.geometry(W,D,.24,lambda x,y:relief(x,y,field))
    high=practical.make_object(bpy,scene,'BrokenMesa HIGH - directional beds',verts.tolist(),faces)
    for poly in high.data.polygons:poly.use_smooth=True
    hm=bpy.data.materials.new('Sandstone relief');hm.use_nodes=True;high.data.materials.append(hm)
    hm.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.46,.35,.25,1)
    normal=truth.bake(bpy,scene,low,high,lm,a.size,'Normal')
    nxy=np.linalg.norm(normal[:,:,:2]*2-1,axis=-1)
    # A mostly neutral map is intentional. Check that the strongest ledges are real.
    p95,p99=np.percentile(nxy,[95,99]);report=f'Normal XY p95/p99={p95:.3f}/{p99:.3f}; height={verts[:,2].min():.3f}..{verts[:,2].max():.3f}'
    if p99<.075:raise RuntimeError('Weak/failed bake: '+report)
    print(report,flush=True);truth.png(OUT/'BrokenMesaPolish_Normal.png',normal)
    low.location.x=-W*.6;high.location.x=W*.6
    bpy.ops.object.select_all(action='DESELECT');bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
    ready.write_bytes(('V20_MESA_POLISH_1\n'+report+'\n').encode('utf-8'))
    print('V20 MESA POLISH READY:',OUT,flush=True)

if __name__=='__main__':main()
