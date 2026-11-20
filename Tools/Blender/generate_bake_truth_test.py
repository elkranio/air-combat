"""V18 diagnostic: oversized top-facing industrial relief, selected-to-active AO + normal.
Run from repo root with Blender: blender -b --python-exit-code 1 --python Tools/Blender/generate_bake_truth_test.py -- --device OPTIX
No illumination/sun bake. The exported game mesh is a single flat quad.
"""
import argparse
from pathlib import Path
import shutil
import struct
import sys
import zlib
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Environment/FrontierBakeTruth'
SOURCE = ROOT / 'Resources/Blender/FrontierBakeTruth.blend'
WIDTH, DEPTH = 30.0, 18.0


def smooth(a, b, x):
    t = np.clip((x-a)/(b-a), 0, 1)
    return t*t*(3-2*t)


def rect(x, y, cx, cy, hx, hy, feather):
    d = np.maximum(np.abs(x-cx)-hx, np.abs(y-cy)-hy)
    return 1-smooth(-feather, feather, d)


def relief(x, y):
    """Large physical detail on high only; relief in metres, not texture noise."""
    h = np.zeros_like(x, dtype=np.float64)
    # Three sunken service-panel bays, raised inner plates and wide bevels.
    for cx in (-9, 0, 9):
        h -= .55*rect(x,y,cx,1.6,3.35,3.3,.48)
        h += .32*rect(x,y,cx,1.6,2.45,2.40,.38)
        # Raised cross ribs and four broad fasteners per bay.
        h += .26*rect(x,y,cx,1.6,.35,2.1,.18)
        for dx in (-1.65,1.65):
            for dy in (-1.65,1.65):
                r = np.hypot(x-cx-dx,y-1.6-dy)
                h += .43*(1-smooth(.21,.46,r))
    # Two heavy horizontal rails; the upper one has repeating broad clamps.
    h += .64*rect(x,y,0,6.25,12.3,.68,.38)
    h += .53*rect(x,y,0,-5.65,12.3,.75,.4)
    for cx in (-10,-6,-2,2,6,10):
        h += .23*rect(x,y,cx,6.25,.42,1.05,.2)
    # Vent bank: bevelled channels cut into a wide plate.
    h += .28*rect(x,y,0,-3.9,7.5,1.05,.35)
    for cx in np.arange(-6.5,7,.95):
        h -= .44*rect(x,y,cx,-3.9,.19,.72,.13)
    # Keep the outer border flat, so the high source meets the low target cleanly.
    return h * (1-smooth(13.4,14.7,np.abs(x))) * (1-smooth(7.7,8.7,np.abs(y)))


def geometry():
    xs=np.linspace(-WIDTH/2-.25,WIDTH/2+.25,305)
    ys=np.linspace(-DEPTH/2-.25,DEPTH/2+.25,185)
    x,y=np.meshgrid(xs,ys); h=relief(x,y)
    verts=np.stack((x,y,h),axis=-1).reshape(-1,3)
    w=len(xs)
    faces=[(j*w+i,j*w+i+1,(j+1)*w+i+1,(j+1)*w+i) for j in range(len(ys)-1) for i in range(w-1)]
    return verts,faces


def png(path,pixels):
    rgb=np.rint(np.clip(pixels,0,1)*255).astype(np.uint8)[::-1]
    h,w,_=rgb.shape
    def chunk(t,d): return struct.pack('!I',len(d))+t+d+struct.pack('!I',zlib.crc32(t+d)&0xffffffff)
    raw=b''.join(b'\0'+row.tobytes() for row in rgb)
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('!2I5B',w,h,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(raw,6))+chunk(b'IEND',b''))


def setup_device(bpy,scene,device):
    scene.render.engine='CYCLES'
    if device=='CPU': scene.cycles.device='CPU'; return
    prefs=bpy.context.preferences.addons['cycles'].preferences
    prefs.compute_device_type=device
    if hasattr(prefs,'refresh_devices'): prefs.refresh_devices()
    else: prefs.get_devices()
    gpu=[d for d in prefs.devices if d.type==device]
    if not gpu: raise RuntimeError('No '+device+' device; use --device CUDA or CPU explicitly.')
    for d in prefs.devices: d.use=d in gpu
    scene.cycles.device='GPU'
    print('CYCLES DEVICES:',', '.join(d.name for d in gpu),flush=True)


def select(bpy,low,high=None):
    bpy.ops.object.select_all(action='DESELECT')
    if high is not None: high.select_set(True)
    low.select_set(True); bpy.context.view_layer.objects.active=low


def bake(bpy,scene,low,high,mat,size,kind):
    image=bpy.data.images.new('Truth_'+kind,width=size,height=size,alpha=True,float_buffer=True)
    image.colorspace_settings.name='Non-Color'
    image.generated_color=(.5,.5,1,1) if kind=='Normal' else (1,1,1,1)
    target=mat.node_tree.nodes.new('ShaderNodeTexImage'); target.image=image
    for node in mat.node_tree.nodes: node.select=False
    target.select=True; mat.node_tree.nodes.active=target
    try:
        select(bpy,low,high)
        scene.render.bake.use_selected_to_active=True
        scene.render.bake.use_cage=False
        scene.render.bake.cage_extrusion=1.25
        scene.render.bake.max_ray_distance=3.0
        scene.render.bake.normal_space='TANGENT'
        scene.render.bake.normal_r='POS_X'; scene.render.bake.normal_g='POS_Y'; scene.render.bake.normal_b='POS_Z'
        scene.render.bake.margin=8; scene.render.bake.use_clear=True
        result=bpy.ops.object.bake(type='NORMAL' if kind=='Normal' else 'EMIT')
        if 'FINISHED' not in result: raise RuntimeError(kind+' bake failed')
        pixels=np.empty(size*size*4,dtype=np.float32); image.pixels.foreach_get(pixels)
        rgb=pixels.reshape(size,size,4)[:,:,:3].copy()
        if not np.isfinite(rgb).all(): raise RuntimeError('Invalid '+kind+' pixels')
        return rgb
    finally:
        mat.node_tree.nodes.remove(target); bpy.data.images.remove(image)
        scene.render.bake.use_selected_to_active=False


def make_controls(out):
    # Independently constructed positive controls: impossible to mistake for a weak bake.
    n=256; x=np.arange(n)[None,:]; y=np.arange(n)[:,None]
    ao=np.broadcast_to(np.where((x//32)%2==0,.14,1.0),(n,n))
    png(out/'ControlAO.png',np.repeat(ao[:,:,None],3,axis=2))
    nx=np.broadcast_to(np.where((x//32)%2==0,-.76,.76),(n,n))
    ny=np.broadcast_to(np.where((y//64)%2==0,-.25,.25),(n,n))
    nz=np.sqrt(1-nx*nx-ny*ny)
    png(out/'ControlNormal.png',np.stack((nx,ny,nz),axis=-1)*.5+.5)


def main():
    import bpy
    p=argparse.ArgumentParser(); p.add_argument('--device',choices=('OPTIX','CUDA','CPU'),default='OPTIX')
    p.add_argument('--size',type=int,choices=(512,1024,2048),default=2048)
    p.add_argument('--samples',type=int,default=64)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    if args.samples<1: raise ValueError('samples must be positive')
    OUT.mkdir(parents=True,exist_ok=True); SOURCE.parent.mkdir(parents=True,exist_ok=True)
    ready=OUT/'BakeTruth.ready.txt'; ready.unlink(missing_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True); scene=bpy.context.scene
    bpy.context.preferences.filepaths.save_version=0
    scene.unit_settings.system='METRIC'; scene.unit_settings.scale_length=1
    setup_device(bpy,scene,args.device); scene.cycles.samples=args.samples
    scene.render.bake.target='IMAGE_TEXTURES'; scene.render.bake.margin_type='EXTEND'
    def obj(name,verts,faces):
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
        if mesh.validate(): raise RuntimeError('Invalid mesh '+name)
        o=bpy.data.objects.new(name,mesh);scene.collection.objects.link(o);return o
    low=obj('IndustrialTestPanel',[(-15,-9,0),(15,-9,0),(15,9,0),(-15,9,0)],[(0,1,2,3)])
    uv=low.data.uv_layers.new(name='UVMap')
    for li in low.data.polygons[0].loop_indices:
        v=low.data.vertices[low.data.loops[li].vertex_index].co
        uv.data[li].uv=(v.x/WIDTH+.5,v.y/DEPTH+.5)
    low_mat=bpy.data.materials.new('Bake target');low_mat.use_nodes=True;low.data.materials.append(low_mat)
    verts,faces=geometry(); high=obj('HIGH - actual industrial relief',verts.tolist(),faces)
    for poly in high.data.polygons: poly.use_smooth=True
    high_mat=bpy.data.materials.new('High relief');high_mat.use_nodes=True;high.data.materials.append(high_mat)
    nodes=high_mat.node_tree.nodes; links=high_mat.node_tree.links
    output=nodes.get('Material Output'); bsdf=nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value=(.32,.32,.32,1)
    bsdf.inputs['Roughness'].default_value=.82
    print('HIGH:',len(verts),'vertices, height range',float(verts[:,2].min()),float(verts[:,2].max()),flush=True)
    print('Baking tangent normals...',flush=True)
    normal=bake(bpy,scene,low,high,low_mat,args.size,'Normal')
    print('Baking projected local AO...',flush=True)
    ao_node=nodes.new('ShaderNodeAmbientOcclusion');ao_node.only_local=True;ao_node.inputs['Distance'].default_value=2.8
    emission=nodes.new('ShaderNodeEmission');links.new(ao_node.outputs['AO'],emission.inputs['Color']);links.new(emission.outputs[0],output.inputs['Surface'])
    ao=bake(bpy,scene,low,high,low_mat,args.size,'AO')
    links.new(bsdf.outputs['BSDF'],output.inputs['Surface']);nodes.remove(emission);nodes.remove(ao_node)
    # Report strength before export; a weak bake is useful diagnostic evidence, not a silent success.
    nxy=np.linalg.norm(normal[:,:,:2]*2-1,axis=-1); av=ao[:,:,0]
    report=f'Normal XY p50/p95={np.percentile(nxy,50):.3f}/{np.percentile(nxy,95):.3f}; AO p05/p50/p95={np.percentile(av,5):.3f}/{np.percentile(av,50):.3f}/{np.percentile(av,95):.3f}'
    print(report,flush=True)
    if np.percentile(nxy,95)<.08 or np.percentile(av,95)-np.percentile(av,5)<.03:
        print('WARNING: weak map signal; inspect raw maps and positive controls.',flush=True)
    png(OUT/'Normal.png',normal);shutil.copyfile(OUT/'Normal.png',OUT/'NormalRaw.png')
    png(OUT/'AO.png',ao);png(OUT/'AOStrong.png',np.clip(ao,0,1)**3)
    make_controls(OUT)
    select(bpy,low)
    bpy.ops.export_scene.fbx(filepath=str(OUT/'IndustrialTestPanel.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
    # Keep both sources editable, side by side, with flat neutral material.
    low.location.x=-18; high.location.x=18
    bpy.ops.object.select_all(action='DESELECT');bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
    ready.write_bytes(('V18_BAKE_TRUTH_1\n'+report+'\n').encode('utf-8'))
    print('BAKE TRUTH READY:',OUT,flush=True)


if __name__=='__main__': main()
