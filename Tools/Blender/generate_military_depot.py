"""G2: editable low/high military depot with a real Cycles roof projection bake.

Run from repo root:
  blender -b --python-exit-code 1 --python Tools/Blender/generate_military_depot.py -- --device OPTIX
Fallback: --device CUDA or --device CPU. Normal/AO are tangent-space,
selected-to-active from physically different high relief; no baked sunlight.
Geometry source: military_depot_geometry.py (pure Python, deterministic).
"""
import argparse
from pathlib import Path
import sys
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import generate_bake_truth_test as truth
import military_depot_geometry as model

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Environment/MilitaryDepot'
SOURCE = ROOT / 'Resources/Blender/MilitaryDepot.blend'
READY = OUT / 'MilitaryDepot.ready.txt'


def smooth(a,b,v):
    return truth.smooth(a,b,v)


def rect(x,y,cx,cy,half_w,half_d,feather):
    return truth.rect(x,y,cx,cy,half_w,half_d,feather)


def relief(x,y):
    """Broad equipment plates, recessed slots, surface seams; metres, not noise."""
    h=np.zeros_like(x,dtype=np.float64)
    for cx in (-8,0,8):
        h -= .38*rect(x,y,cx,-2.1,2.85,3.6,.42)
        h += .28*rect(x,y,cx,-2.1,2.05,2.65,.33)
        h += .24*rect(x,y,cx,-2.1,.28,2.25,.18)
    h += .38*rect(x,y,0,6.5,11.5,.55,.34)
    h += .31*rect(x,y,0,-7,11.8,.60,.32)
    for cx in np.arange(-9.5,10,1.65):
        h -= .46*rect(x,y,cx,6.5,.18,.35,.14)
    # Shallow damaged area; no long radial/cyclic AI-looking scratches.
    seam_y = .24*x - 3.2 + .24*np.sin(1.6*x)
    h -= .18*np.exp(-((y-seam_y)/.37)**2)*smooth(7,10.5,x)*(1-smooth(12.4,14,x))
    border=(1-smooth(13.3,14.55,np.abs(x)))*(1-smooth(8.45,9.55,np.abs(y)))
    return h*border


def albedo(size):
    # Albedo is purely pigment/material wear; NOT pre-baked lighting/AO/sun.
    w,d=model.ROOF_WIDTH,model.ROOF_DEPTH
    xx,yy=np.meshgrid((np.arange(size)+.5)/size*w-w/2,
                      (np.arange(size)+.5)/size*d-d/2)
    color=np.broadcast_to(np.array([.455,.465,.443]),xx.shape+(3,)).copy()
    for cx in (-8,0,8):
        bay=rect(xx,yy,cx,-2.1,2.85,3.6,.40)
        plate=rect(xx,yy,cx,-2.1,2.05,2.65,.29)
        color=color*(1-bay[:,:,None]*.23)+np.array([.37,.389,.376])*bay[:,:,None]*.23
        color=color*(1-plate[:,:,None]*.40)+np.array([.52,.53,.49])*plate[:,:,None]*.40
    rails=np.maximum(rect(xx,yy,0,6.5,11.5,.55,.3),rect(xx,yy,0,-7,11.8,.6,.3))
    color=color*(1-rails[:,:,None]*.5)+np.array([.32,.35,.34])*rails[:,:,None]*.5
    weather=(.012*np.sin(.61*xx+.42*yy)*np.sin(.39*xx-.73*yy)
             +.010*np.sin(1.14*xx+.22*yy)*np.sin(.89*yy-.4*xx))
    return np.clip(color+weather[:,:,None],0,1)


def high_mesh():
    # Top-facing Blender XY plane, world-up Z. Adjacent rim is real low geometry.
    w,d=model.ROOF_WIDTH,model.ROOF_DEPTH
    xs=np.linspace(-w/2,w/2,201)
    ys=np.linspace(-d/2,d/2,135)
    xx,yy=np.meshgrid(xs,ys)
    zz=model.ROOF_Z+relief(xx,yy)
    vertices=np.stack((xx,yy,zz),axis=-1).reshape(-1,3)
    stride=len(xs)
    faces=[(j*stride+i,j*stride+i+1,(j+1)*stride+i+1,(j+1)*stride+i)
           for j in range(len(ys)-1) for i in range(stride-1)]
    return vertices,faces


def make_object(bpy,scene,name,vertices,faces):
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    if mesh.validate():
        raise RuntimeError('Invalid mesh repaired by Blender: '+name)
    result=bpy.data.objects.new(name,mesh)
    scene.collection.objects.link(result)
    return result


def main():
    import bpy
    parser=argparse.ArgumentParser()
    parser.add_argument('--device',choices=('OPTIX','CUDA','CPU'),default='OPTIX')
    parser.add_argument('--size',type=int,choices=(512,1024,2048),default=1024)
    parser.add_argument('--samples',type=int,default=48)
    a=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    if a.samples<1:raise ValueError('Samples must be positive')
    OUT.mkdir(parents=True,exist_ok=True)
    SOURCE.parent.mkdir(parents=True,exist_ok=True)
    READY.unlink(missing_ok=True) # Never publish stale successful marker.

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    scene=bpy.context.scene
    scene.unit_settings.system='METRIC'
    scene.unit_settings.scale_length=1
    truth.setup_device(bpy,scene,a.device)
    scene.cycles.samples=a.samples
    scene.render.bake.target='IMAGE_TEXTURES'
    scene.render.bake.margin_type='EXTEND'

    game=[]
    for name,part in model.make_depot().items():
        ob=make_object(bpy,scene,name,part['vertices'],part['faces'])
        mat=bpy.data.materials.new(name+'_source')
        mat.use_nodes=True
        ob.data.materials.append(mat)
        game.append(ob)
        if name=='Roof_BakedPanel':
            roof=ob
            uv=ob.data.uv_layers.new(name='UVMap')
            for loop in ob.data.polygons[0].loop_indices:
                vert=ob.data.vertices[ob.data.loops[loop].vertex_index].co
                uv.data[loop].uv=(vert.x/model.ROOF_WIDTH+.5,vert.y/model.ROOF_DEPTH+.5)
            ob.data.polygons[0].use_smooth=False
    verts,faces=high_mesh()
    high=make_object(bpy,scene,'HIGH_Roof_ActualRelief',verts.tolist(),faces)
    for poly in high.data.polygons:poly.use_smooth=True
    hm=bpy.data.materials.new('HIGH_Roof_Relief')
    hm.use_nodes=True
    high.data.materials.append(hm)
    nodes=hm.node_tree.nodes;links=hm.node_tree.links
    output=nodes.get('Material Output')
    bsdf=nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value=(.35,.35,.35,1)
    bsdf.inputs['Roughness'].default_value=.88

    print('DEPOT low render groups:',len(game),'faces:',sum(len(o.data.polygons) for o in game),flush=True)
    print('ROOF high vertices:',len(verts),'high-only height range:',
          float(verts[:,2].min()-model.ROOF_Z),float(verts[:,2].max()-model.ROOF_Z),flush=True)
    print('G2: Cycles selected-to-active TANGENT normal bake...',flush=True)
    normal=truth.bake(bpy,scene,roof,high,roof.data.materials[0],a.size,'Normal')
    print('G2: Cycles local AO -> emission selected-to-active bake...',flush=True)
    aon=nodes.new('ShaderNodeAmbientOcclusion')
    aon.only_local=True
    aon.inputs['Distance'].default_value=2.8
    em=nodes.new('ShaderNodeEmission')
    links.new(aon.outputs['AO'],em.inputs['Color'])
    links.new(em.outputs[0],output.inputs['Surface'])
    ao=truth.bake(bpy,scene,roof,high,roof.data.materials[0],a.size,'AO')
    links.new(bsdf.outputs['BSDF'],output.inputs['Surface'])
    nodes.remove(em);nodes.remove(aon)
    signal=np.linalg.norm(normal[:,:,:2]*2-1,axis=-1)
    p95=float(np.percentile(signal,95))
    a05=float(np.percentile(ao[:,:,0],5));a95=float(np.percentile(ao[:,:,0],95))
    report=f'Normal XY p95={p95:.3f}; AO p05/p95={a05:.3f}/{a95:.3f}'
    print(report,flush=True)
    if p95<.06:raise RuntimeError('G2 invalid/weak projected normal map: '+report)
    if a95-a05<.025:print('G2: AO subtle. Keep optional in material; normal is primary.',flush=True)
    truth.png(OUT/'DepotRoof_Normal.png',normal)
    truth.png(OUT/'DepotRoof_AO.png',ao)
    truth.png(OUT/'DepotRoof_BaseColor.png',albedo(a.size))

    bpy.ops.object.select_all(action='DESELECT')
    for ob in game:ob.select_set(True)
    bpy.context.view_layer.objects.active=roof
    bpy.ops.export_scene.fbx(filepath=str(OUT/'MilitaryDepot.fbx'),use_selection=True,
        object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
        bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
    # Keep source editable: high offset only AFTER bake/export, so output is aligned.
    high.location.x=95
    for ob in game:ob.select_set(False)
    high.select_set(False)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
    for path in ['MilitaryDepot.fbx','DepotRoof_Normal.png','DepotRoof_AO.png','DepotRoof_BaseColor.png']:
        if not (OUT/path).is_file() or (OUT/path).stat().st_size<100:
            raise RuntimeError('G2 missing/empty output: '+path)
    READY.write_bytes(('G2_MILITARY_DEPOT_1\n'+report+'\n').encode('utf-8'))
    print('G2 military depot ready:',str(OUT),flush=True)


if __name__=='__main__':main()
