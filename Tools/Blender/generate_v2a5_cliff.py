"""V2A5: ONE authored fractured escarpment, deliberately not a terrain generator.

World-space geometric language: an irregular 360m face, deeply cut buttresses,
strong asymmetrical cap, noncontinuous ledges, talus. Blender is used to author,
unwrap, optionally AO-bake and export an editable source mesh.

From repository root (Unity CLOSED):
  blender -b --python-exit-code 1 --python Tools/Blender/generate_v2a5_cliff.py -- --device OPTIX

Debug geometry without Blender:
  python Tools/Blender/generate_v2a5_cliff.py --preview-obj /tmp/v2a5
"""
from __future__ import annotations
import argparse
from pathlib import Path
import math
import sys

# Hand-placed stations across the escarpment: x, toe z, crest z, crest height,
# recess depth. They intentionally DO NOT form a smooth, repeating cross-section.
# The notches at x=-65 and x=45 break the silhouette and create deep shadows.
STATIONS = [
    (-193, -40,  3,  28,  2),
    (-171, -51,  9,  63, -4),
    (-145, -73, 17,  92,  9),
    (-113, -85, 28, 106, -9),
    ( -87, -68, 47, 118, 12),
    ( -65, -47, 58,  71, -27),
    ( -35, -78, 30,  99,  6),
    (  -2, -93, 34, 122, -12),
    (  26, -69, 55, 116,  8),
    (  47, -43, 69,  76, -24),
    (  78, -82, 32, 106, 12),
    ( 111, -65, 24,  91, -3),
    ( 144, -49,  5,  74,  9),
    ( 184, -32, 17,  23,  0),
]

# Front-to-back layers with dissimilar offsets, rather than rings/flat stripes:
# z samples depend on each station. y is fractions of crest height.
# Layer zero lies near the local ground surface (~2.5m).
LAYER_TYPES = ['toe', 'talus', 'lower', 'fracture', 'upper', 'rim', 'cap', 'back', 'heel']
COLORS = {
    'Cap':      (.59, .51, .405),
    'WarmFace': (.43, .365, .30),
    'CoolFace': (.37, .372, .365),
    'Fracture': (.285, .29, .285),
    'Talus':    (.53, .44, .35),
}
MATERIALS = list(COLORS)


def station_vertex(i, layer):
    x, front, crest_z, crest_y, indent = STATIONS[i]
    # Small authored local skew to make the fractured surface nonparallel.
    w = [0, .43, -.56, 1.1, -.6, .2, -1.3, .2, 0][layer]
    skew = (1 if i % 3 == 0 else -1 if i % 3 == 1 else 0) * w
    foot = 2.6
    if layer == 0: return (x, foot, front - 26)
    if layer == 1: return (x + skew * 1.2, foot + (crest_y - foot) * .13, front - 5)
    if layer == 2: return (x + skew * 2, 15 + (crest_y - 80) * .075, front + 8 + indent * .2)
    if layer == 3: return (x + indent * .25, crest_y * .42, front + 11 + indent * .75)
    if layer == 4: return (x - indent * .24, crest_y * .73, front + 17 - indent * .28)
    if layer == 5: return (x + skew * 1.1, crest_y, crest_z)
    if layer == 6: return (x + skew * 2.0, crest_y - (7 if i % 4 else 2), crest_z + 23)
    if layer == 7: return (x, crest_y * .77, crest_z + 90)
    return (x, foot + 2.0, crest_z + 155)


def triangulate_face(a, b, c, d, flip):
    # A/B = adjacent X stations at front layer; C/D = next layer behind.
    # Front-facing winding: (a,d,b), (b,d,c), yielding +Y / -Z normals.
    if flip:
        return [(a, d, c), (a, c, b)]
    return [(a, d, b), (b, d, c)]


class RockMesh:
    def __init__(self):
        self.verts = []
        self.faces = []
        self.mats = []
        self.groups = []

    def vertex(self, xyz):
        self.verts.append(tuple(float(v) for v in xyz))
        return len(self.verts) - 1

    def tri(self, a, b, c, material, group):
        self.faces.append((a, b, c))
        self.mats.append(MATERIALS.index(material))
        self.groups.append(group)

    def polygon(self, ids, material, group):
        for k in range(1, len(ids) - 1):
            self.tri(ids[0], ids[k], ids[k + 1], material, group)


def authored_cliff():
    m = RockMesh()
    n = len(STATIONS)
    vid = [[m.vertex(station_vertex(i, k)) for k in range(len(LAYER_TYPES))] for i in range(n)]
    for i in range(n - 1):
        for layer in range(len(LAYER_TYPES) - 1):
            a = vid[i][layer]
            b = vid[i + 1][layer]
            c = vid[i + 1][layer + 1]
            d = vid[i][layer + 1]
            # Alternate diagonals across sections, NOT height-striped normals.
            if layer <= 1: mat = 'Talus'
            elif layer >= 5: mat = 'Cap'
            else:
                key = (i * 7 + layer * 5) % 9
                mat = 'Fracture' if key in (0, 6) else 'CoolFace' if key in (2, 7, 8) else 'WarmFace'
            for face in triangulate_face(a, b, c, d, (i + layer) % 2 == 0):
                m.tri(*face, mat, 'main cliff')

    # Endpoint side walls are visible when flying around the escarpment.
    # Close each profile against a below-ground spine; only geometry, no fake cast shadows.
    for station_i, reverse in ((0, False), (n - 1, True)):
        x = STATIONS[station_i][0]
        underside = [m.vertex((x, -5, STATIONS[station_i][1] - 26)),
                     m.vertex((x, -5, STATIONS[station_i][2] + 155))]
        points = [vid[station_i][k] for k in range(len(LAYER_TYPES))]
        edge = [underside[0]] + points + [underside[1]]
        if reverse: edge.reverse()
        m.polygon(edge, 'CoolFace', 'end cap')

    # Authored projecting buttresses: each is a different four-to-six-sided cage.
    # These are significant 3D overhangs/occluders, not painted stains.
    buttresses = [
        dict(x=-137, toe=-113, top=82, width=49, nose=-41, cant=11),
        dict(x=-42,  toe=-126, top=70, width=43, nose=-47, cant=-9),
        dict(x=21,   toe=-127, top=101, width=52, nose=-37, cant=10),
        dict(x=100,  toe=-100, top=70, width=35, nose=-30, cant=-12),
    ]
    for j, b in enumerate(buttresses):
        x, toe, h, w, nose, cant = (b[q] for q in ('x', 'toe', 'top', 'width', 'nose', 'cant'))
        # 3D irregular frontal triangular wedges with a recessed rear.
        ring = [
            m.vertex((x - w * .54, 2.0, toe + 28)),
            m.vertex((x + w * .48, 2.5, toe + 19)),
            m.vertex((x + w * .62, h * .34, toe - 4)),
            m.vertex((x + w * .25, h * .88, toe + nose + 25)),
            m.vertex((x - w * .28, h, toe + nose + 16)),
            m.vertex((x - w * .64, h * .41, toe - 11)),
        ]
        center = m.vertex((x + cant * .3, h * .47, toe + nose - 2))
        for k in range(len(ring)):
            mat = ('WarmFace', 'CoolFace', 'Fracture', 'WarmFace', 'CoolFace', 'Talus')[k]
            m.tri(ring[k], ring[(k + 1) % len(ring)], center, mat, 'buttress %d' % j)
        rear = m.vertex((x + cant, h * .48, toe + 31))
        m.tri(ring[2], ring[3], rear, 'Fracture', 'buttress rear')
        m.tri(ring[3], ring[4], rear, 'Cap', 'buttress rear')
        m.tri(ring[4], ring[5], rear, 'CoolFace', 'buttress rear')
        m.tri(ring[5], ring[0], rear, 'Talus', 'buttress rear')

    # A few discontinuous horizontal ledges. They must NOT become a band
    # stretching across the full cliff like the rejected V2A4 rings.
    ledges = [(-151, -85, 54, 44, 8), (-54, -70, 68, 29, 8),
              (56, -69, 48, 52, 11), (108, -60, 35, 30, 7)]
    for j, (x, z, h, width, depth) in enumerate(ledges):
        ids = [
            m.vertex((x - width * .55, h - 3, z + depth)),
            m.vertex((x + width * .47, h - 2, z + depth * .65)),
            m.vertex((x + width * .43, h, z - depth * 1.4)),
            m.vertex((x - width * .31, h + 3, z - depth * 1.65)),
            m.vertex((x - width * .6, h + 1, z - depth * .75)),
        ]
        m.polygon(ids, 'Cap', 'ledge %d top' % j)
        underside = m.vertex((x, h - 8, z + depth * .4))
        for k in range(len(ids)):
            m.tri(ids[(k + 1) % len(ids)], ids[k], underside, 'Fracture', 'ledge %d underside' % j)

    # Named and positioned rocks, not pseudo-random scenery scatter.
    scree = [
        (-155,-115, 21,13,9), (-123,-138, 26,17,12), (-88,-112, 14,11,7),
        (-61,-143, 26,19,13), (-21,-122, 18,11,9), (13,-148, 24,16,12),
        (58,-128, 22,17,9), (95,-116, 32,18,11), (131,-86, 17,12,9),
    ]
    for j, (x, z, rx, rz, h) in enumerate(scree):
        p = [m.vertex((x - rx * .84,2.5,z - rz * .6)),
             m.vertex((x + rx * .81,2.3,z - rz)),
             m.vertex((x + rx,2.8,z + rz * .6)),
             m.vertex((x - rx * .7,2.6,z + rz))]
        a = m.vertex((x - rx * .12,h * .76,z - rz * .21))
        b = m.vertex((x + rx * .17,h + 2,z + rz * .34))
        for face, mat in [((p[0],p[1],a),'Talus'), ((p[1],p[2],b),'WarmFace'),
                          ((p[1],b,a),'CoolFace'), ((p[2],p[3],b),'Talus'),
                          ((p[3],p[0],a),'WarmFace'), ((p[3],a,b),'CoolFace')]:
            m.tri(*face, mat, 'scree %d' % j)

    return m


def export_obj_preview(path: Path, m: RockMesh):
    with path.open('w', encoding='ascii') as f:
        for x, y, z in m.verts: f.write('v %.5f %.5f %.5f\n' % (x, y, z))
        for face in m.faces: f.write('f %d %d %d\n' % tuple(v + 1 for v in face))


def create_blender_object(m: RockMesh):
    mesh = bpy.data.meshes.new('Escarpment / hand-placed planes')
    # Fixed Unity coordinate basis -> Blender Z-up, then correct FBX -Z/Y export.
    mesh.from_pydata([(x, -z, y) for x,y,z in m.verts], [], m.faces)
    mesh.update()
    if mesh.validate(verbose=True):
        raise RuntimeError('Cliff mesh failed geometry validation')
    for name in MATERIALS:
        material = bpy.data.materials.new('V2A5_' + name)
        material.use_nodes = True
        material.diffuse_color = (*COLORS[name],1)
        bsdf = material.node_tree.nodes.get('Principled BSDF')
        bsdf.inputs['Base Color'].default_value = (*COLORS[name],1)
        bsdf.inputs['Metallic'].default_value = 0
        bsdf.inputs['Roughness'].default_value = .91
        mesh.materials.append(material)
    for poly, index in zip(mesh.polygons, m.mats):
        poly.material_index = index
        poly.use_smooth = False  # deliberate broad faceted planes; no fake soft hills
    obj = bpy.data.objects.new('V2A5_Cliff', mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def bake_local_ao(obj, output: Path):
    select(obj)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(island_margin=.012)
    bpy.ops.object.mode_set(mode='OBJECT')
    img = bpy.data.images.new('V2A5_Cliff_AO',width=1024,height=1024,alpha=False)
    img.generated_color=(1,1,1,1)
    for mat in obj.data.materials:
        node=mat.node_tree.nodes.new('ShaderNodeTexImage')
        node.image=img
        mat.node_tree.nodes.active=node
    scene=bpy.context.scene
    scene.render.engine='CYCLES'
    scene.cycles.samples=48
    scene.render.bake.target='IMAGE_TEXTURES'
    scene.render.bake.use_clear=True
    scene.render.bake.use_selected_to_active=False
    scene.render.bake.margin=8
    select(obj)
    bpy.ops.object.bake(type='AO')
    img.filepath_raw=str(output / 'V2A5_Cliff_AO.png')
    img.file_format='PNG'
    img.save()


def select(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active=obj


def gpu(device):
    scene=bpy.context.scene
    scene.render.engine='CYCLES'
    scene.cycles.device='CPU'
    if device=='CPU':return 'CPU'
    try:
        prefs=bpy.context.preferences.addons['cycles'].preferences
        for candidate in ([device] if device!='AUTO' else ['OPTIX','CUDA']):
            prefs.compute_device_type=candidate
            prefs.get_devices()
            matches=[d for d in prefs.devices if d.type==candidate]
            if matches:
                for d in prefs.devices:d.use=d.type==candidate
                scene.cycles.device='GPU'
                return candidate
    except Exception as exc:
        print('V2A5: GPU unavailable:',exc,flush=True)
    return 'CPU'


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--device',choices=('AUTO','OPTIX','CUDA','CPU'),default='AUTO')
    parser.add_argument('--no-bake',action='store_true')
    parser.add_argument('--preview-obj',type=Path)
    raw=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else (sys.argv[sys.argv.index('--preview-obj'):] if '--preview-obj' in sys.argv else [])
    args=parser.parse_args(raw)
    mesh=authored_cliff()
    xs=[v[0] for v in mesh.verts]
    ys=[v[1] for v in mesh.verts]
    zs=[v[2] for v in mesh.verts]
    print('V2A5 model:',len(mesh.verts),'vertices,',len(mesh.faces),'triangles;',
          'x %.1f..%.1f y %.1f..%.1f z %.1f..%.1f'%(min(xs),max(xs),min(ys),max(ys),min(zs),max(zs)), flush=True)
    if args.preview_obj:
        args.preview_obj.mkdir(parents=True,exist_ok=True)
        export_obj_preview(args.preview_obj/'V2A5_Cliff.obj',mesh)
        return
    if bpy is None:raise RuntimeError('Blender required for FBX / Cycles AO')
    root=Path.cwd()
    output=root/'Assets/_Project/Art/Environment/FrontierV2Cliff'
    output.mkdir(parents=True,exist_ok=True)
    source=root/'Resources/Blender/FrontierV2Cliff.blend'
    source.parent.mkdir(parents=True,exist_ok=True)
    marker=output/'V2A5.ready.txt'
    marker.unlink(missing_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.context.scene.unit_settings.system='METRIC'
    bpy.context.scene.unit_settings.scale_length=1
    obj=create_blender_object(mesh)
    backend=gpu(args.device)
    if not args.no_bake:
        bake_local_ao(obj,output)
    else:
        # No stale AO from an earlier export is allowed in a geometry-only build.
        (output/'V2A5_Cliff_AO.png').unlink(missing_ok=True)
        # Even when AO is skipped, ensure there are usable UVs for future tests.
        select(obj);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(island_margin=.012)
        bpy.ops.object.mode_set(mode='OBJECT')
    select(obj)
    bpy.ops.export_scene.fbx(filepath=str(output/'V2A5_Cliff.fbx'),use_selection=True,
         object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
         use_mesh_modifiers=False,bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
    bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
    marker.write_bytes(('V2A5_CLIFF_1\n'+('AO '+backend if not args.no_bake else 'NO_AO')+'\n').encode('utf-8'))
    print('V2A5 complete:',output,flush=True)


try:
    import bpy
except ImportError:
    bpy=None

if __name__=='__main__':main()
