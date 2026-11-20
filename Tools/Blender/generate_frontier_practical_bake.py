"""V19: large gameplay-scale industrial roof and local sandstone detail normals.
Uses the proven V18 Cycles projection code. Rock detail is a planar tangent-space
relief layer on the unchanged V17 BrokenMesa surface, not an FBX displacement.
"""
import argparse
import json
from pathlib import Path
import sys
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import generate_bake_truth_test as truth

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Environment/FrontierPracticalBake'
SOURCE = ROOT / 'Resources/Blender/FrontierPracticalBake.blend'
DATA = ROOT / 'Resources/Blender/FrontierStrata.json'
ROOF_W, ROOF_D = 30., 20.
ROCK_W, ROCK_D = 145., 80.


def smooth(a, b, v):
    return truth.smooth(a, b, v)


def roof_shapes(x, y):
    rect = truth.rect
    bays = [rect(x,y,cx,1.7,3.3,4.5,.55) for cx in (-9,0,9)]
    plates = [rect(x,y,cx,1.7,2.45,3.45,.4) for cx in (-9,0,9)]
    vents = rect(x,y,0,-5.5,10.5,1.6,.45)
    slots = np.zeros_like(x)
    for cx in np.arange(-9,9.1,1.5):
        slots = np.maximum(slots,rect(x,y,cx,-5.5,.28,1.05,.16))
    rail = rect(x,y,0,7.7,12.9,.42,.24)
    fasteners = np.zeros_like(x)
    for cx in (-9,0,9):
        for dx in (-1.8,1.8):
            for dy in (-2.6,2.6):
                fasteners = np.maximum(fasteners,1-smooth(.19,.48,np.hypot(x-cx-dx,y-1.7-dy)))
    # A branched damaged seam on the right edge; no baked directional lighting.
    seam = np.exp(-((y-(-.34*x+3.0+.38*np.sin(x*1.8)))/.30)**2)*smooth(7.5,10.5,x)*(1-smooth(13.0,14.2,x))
    return np.maximum.reduce(bays),np.maximum.reduce(plates),vents,slots,rail,fasteners,seam


def roof_relief(x,y):
    bays,plates,vents,slots,rail,fasteners,seam=roof_shapes(x,y)
    h=-.60*bays+.34*plates+.40*vents-.55*slots+.53*rail+.41*fasteners-.48*seam
    edge=(1-smooth(14.0,14.85,np.abs(x)))*(1-smooth(9.0,9.85,np.abs(y)))
    return h*edge


def roof_color(size):
    x,y=np.meshgrid((np.arange(size)+.5)/size*ROOF_W-ROOF_W/2,(np.arange(size)+.5)/size*ROOF_D-ROOF_D/2)
    bays,plates,vents,slots,rail,fasteners,seam=roof_shapes(x,y)
    def mix(a,b,m): return a*(1-m[:,:,None])+np.array(b)[None,None,:]*m[:,:,None]
    c=np.broadcast_to(np.array([.58,.54,.47]),x.shape+(3,)).copy()
    c=mix(c,[.49,.47,.43],bays*.65); c=mix(c,[.57,.58,.55],plates*.75)
    c=mix(c,[.40,.41,.39],vents*.85); c=mix(c,[.35,.35,.32],slots*.55)
    c=mix(c,[.43,.40,.35],rail*.9); c=mix(c,[.50,.49,.45],fasteners*.7)
    c=mix(c,[.44,.39,.32],seam*.6)
    weather=.025*np.sin(x*.51+y*.71)*np.sin(x*.83-y*.48)+.018*np.sin(x*2.8+y*.31)*np.sin(y*1.9-x*.4)
    return np.clip(c+weather[:,:,None],0,1)


def load_rock(path=DATA):
    data=json.loads(path.read_text(encoding='utf-8-sig'))
    if data['version']!=1 or abs(data['step']-1.25)>1e-6: raise ValueError('Unexpected V17 manifest version/step')
    field=next(f for f in data['fields'] if f['name']=='BrokenMesa')
    if (field['x0'],field['x1'],field['z0'],field['z1'])!=(-125,20,145,225): raise ValueError('Unexpected BrokenMesa bounds')
    h=np.array(field['heights'],dtype=float).reshape(field['nz'],field['nx'])
    if h.shape!=(65,117) or not np.isfinite(h).all() or h.min()<0 or h.max()>27: raise ValueError('Invalid BrokenMesa height grid')
    return h


def rock_relief(x,y,field):
    # UV-local world coordinates. Match V17's height field spatially, but bake only
    # additive surface relief on a flat reference, so the existing slope normals
    # are not counted twice. Fade completely into the existing talus/terrain.
    u=np.clip((x+ROCK_W/2)/1.25,0,field.shape[1]-1);v=np.clip((y+ROCK_D/2)/1.25,0,field.shape[0]-1)
    i=np.minimum(u.astype(int),field.shape[1]-2);j=np.minimum(v.astype(int),field.shape[0]-2)
    a=u-i;b=v-j
    h=(1-a)*(1-b)*field[j,i]+a*(1-b)*field[j,i+1]+(1-a)*b*field[j+1,i]+a*b*field[j+1,i+1]
    exposed=smooth(2.0,12.,h)
    edge=(1-smooth(ROCK_W/2-3,ROCK_W/2-.5,np.abs(x)))*(1-smooth(ROCK_D/2-3,ROCK_D/2-.5,np.abs(y)))
    # Irregular short bedding shelves. Break the strike and amplitude with broad
    # masks instead of wrapping the entire hill in evenly spaced contour rings.
    strike=y+.18*x+1.8*np.sin(x*.12)+.8*np.sin(x*.29+y*.19)
    shelves=np.zeros_like(x)
    for k,center in enumerate((-15,-6,5,16)):
        local=(.55+.45*np.sin(x*.095+k*1.9))*(.6+.4*np.sin(x*.23+k*.8)**2)
        shelves += .48*(1-smooth(.20,1.18,np.abs(strike-center)))*local
        shelves -= .21*np.exp(-((strike-center-1.2)/.65)**2)*local
    # Three fractured seams cut across the bedding, with irregular local branches.
    cuts=np.zeros_like(x)
    for k,center in enumerate((-34,-7,31)):
        line=center+.42*y+1.65*np.sin(y*.23+k)+.55*np.sin(y*.59-k)
        extent=smooth(-30,-15,y)*(1-smooth(13,32,y))
        cuts += .56*np.exp(-((x-line)/.68)**2)*extent
        cuts += .24*np.exp(-((x-line-.18*(y-8))/1.0)**2)*smooth(6,12,y)*(1-smooth(21,32,y))
    chips=.15*np.sin(x*.92+y*.63)*np.sin(y*.80-x*.38)+.075*np.sin(x*1.66-y*.77)*np.sin(y*1.42+x*.43)
    return np.clip(shelves-cuts+chips,-.94,.94)*exposed*edge


def geometry(w,d,step,relief):
    xs=np.linspace(-w/2-.25,w/2+.25,round((w+.5)/step)+1)
    ys=np.linspace(-d/2-.25,d/2+.25,round((d+.5)/step)+1)
    x,y=np.meshgrid(xs,ys); h=relief(x,y)
    verts=np.stack((x,y,h),axis=-1).reshape(-1,3)
    n=len(xs)
    faces=[(j*n+i,j*n+i+1,(j+1)*n+i+1,(j+1)*n+i) for j in range(len(ys)-1) for i in range(n-1)]
    if not np.isfinite(verts).all() or np.max(np.abs(h))>=1.2: raise RuntimeError('Relief outside the V18 validated cage')
    return verts,faces


def make_object(bpy,scene,name,verts,faces):
    m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces);m.update()
    if m.validate():raise RuntimeError('Invalid mesh '+name)
    ob=bpy.data.objects.new(name,m);scene.collection.objects.link(ob);return ob


def bake_pair(bpy,scene,name,w,d,step,relief,size,export):
    low=make_object(bpy,scene,name,[(-w/2,-d/2,0),(w/2,-d/2,0),(w/2,d/2,0),(-w/2,d/2,0)],[(0,1,2,3)])
    uv=low.data.uv_layers.new(name='UVMap')
    for li in low.data.polygons[0].loop_indices:
        p=low.data.vertices[low.data.loops[li].vertex_index].co
        uv.data[li].uv=(p.x/w+.5,p.y/d+.5)
    lm=bpy.data.materials.new(name+' target');lm.use_nodes=True;low.data.materials.append(lm)
    verts,faces=geometry(w,d,step,relief)
    high=make_object(bpy,scene,name+' HIGH detail',verts.tolist(),faces)
    for p in high.data.polygons:p.use_smooth=True
    hm=bpy.data.materials.new(name+' physical relief');hm.use_nodes=True;high.data.materials.append(hm)
    nodes=hm.node_tree.nodes;links=hm.node_tree.links;out=nodes.get('Material Output');bsdf=nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value=(.35,.31,.25,1);bsdf.inputs['Roughness'].default_value=.9
    print(f'{name}: {len(verts)} high vertices, relief {verts[:,2].min():.2f}..{verts[:,2].max():.2f}',flush=True)
    normal=truth.bake(bpy,scene,low,high,lm,size,'Normal')
    ao_node=nodes.new('ShaderNodeAmbientOcclusion');ao_node.only_local=True;ao_node.inputs['Distance'].default_value=2.8
    em=nodes.new('ShaderNodeEmission');links.new(ao_node.outputs['AO'],em.inputs['Color']);links.new(em.outputs[0],out.inputs['Surface'])
    ao=truth.bake(bpy,scene,low,high,lm,size,'AO')
    links.new(bsdf.outputs['BSDF'],out.inputs['Surface']);nodes.remove(ao_node);nodes.remove(em)
    nxy=np.linalg.norm(normal[:,:,:2]*2-1,axis=-1)
    signal=float(np.percentile(nxy,95)); report=f'{name}: Normal XY p95={signal:.3f}; AO p05/p95={np.percentile(ao[:,:,0],5):.3f}/{np.percentile(ao[:,:,0],95):.3f}'
    if signal<.06: raise RuntimeError('Weak/failed normal bake: '+report)
    print(report,flush=True)
    truth.png(OUT/(name+'_Normal.png'),normal);truth.png(OUT/(name+'_AO.png'),ao)
    if export:
        truth.select(bpy,low)
        bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
    # Retain both in the editable file; separate them before the next bake.
    low.location.x=-w*.6;high.location.x=w*.6
    low.location.y=high.location.y=0 if export else 90
    low.select_set(False);high.select_set(False)
    return report


def main():
    import bpy
    p=argparse.ArgumentParser();p.add_argument('--device',choices=('OPTIX','CUDA','CPU'),default='OPTIX');p.add_argument('--size',type=int,choices=(1024,2048),default=2048);p.add_argument('--samples',type=int,default=48)
    a=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    if a.samples<1:raise ValueError('samples must be positive')
    field=load_rock();OUT.mkdir(parents=True,exist_ok=True);SOURCE.parent.mkdir(parents=True,exist_ok=True)
    ready=OUT/'PracticalBake.ready.txt';ready.unlink(missing_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;bpy.context.preferences.filepaths.save_version=0
    scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
    truth.setup_device(bpy,scene,a.device);scene.cycles.samples=a.samples
    scene.render.bake.target='IMAGE_TEXTURES';scene.render.bake.margin_type='EXTEND'
    r1=bake_pair(bpy,scene,'RefineryRoof',ROOF_W,ROOF_D,.10,roof_relief,a.size,True)
    truth.png(OUT/'RefineryRoof_BaseColor.png',roof_color(a.size))
    r2=bake_pair(bpy,scene,'BrokenMesaDetail',ROCK_W,ROCK_D,.24,lambda x,y:rock_relief(x,y,field),a.size,False)
    bpy.ops.object.select_all(action='DESELECT');bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
    ready.write_bytes(('V19_PRACTICAL_BAKE_1\n'+r1+'\n'+r2+'\n').encode('utf-8'))
    print('V19 PRACTICAL BAKE READY:',OUT,flush=True)

if __name__=='__main__':main()
