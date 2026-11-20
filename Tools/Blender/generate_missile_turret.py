"""Original fictional low-poly turret. Separate fixed and rotating meshes.
Run from repo root with Blender background Python.
"""
from pathlib import Path
from math import cos, sin, tau, radians
import bpy
import bmesh
from mathutils import Euler

ROOT = Path(__file__).resolve().parents[2]
NAME = 'MT-01-Sentinel'
SOURCE = ROOT / 'Resources' / 'Blender' / (NAME + '.blend')
EXPORT = ROOT / 'Assets' / '_Project' / 'Art' / 'Buildings' / NAME / (NAME + '.fbx')
PIVOT_Z = 2.9

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'


def material(name, rgb):
    def linear(v):
        v /= 255
        return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
    m = bpy.data.materials.new('Sentinel_' + name)
    m.diffuse_color = (*[linear(x) for x in rgb], 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = m.diffuse_color
    p.inputs['Roughness'].default_value = 0.85
    return m


palette = [material(name, rgb) for name, rgb in [
    ('Foundation', (143, 147, 129)), ('Armor', (87, 105, 94)),
    ('Upper', (115, 131, 120)), ('Dark', (47, 61, 61)),
    ('Amber', (222, 162, 76)), ('Sensor', (49, 156, 174)),
    ('Panel', (65, 81, 78)),
]]
FOUND, ARMOR, UPPER, DARK, AMBER, SENSOR, PANEL = range(len(palette))


class Geometry:
    def __init__(self):
        self.verts, self.faces, self.ids = [], [], []

    def add(self, verts, faces, ids):
        i = len(self.verts)
        self.verts.extend(verts)
        self.faces.extend(tuple(i + n for n in f) for f in faces)
        self.ids.extend(ids)

    def platform(self, x, y, levels, mats, sides=8, phase=tau / 16):
        # (height, radii x/y), closed stepped octagonal silhouette.
        v = [(x + rx*cos(a*tau/sides+phase), y + ry*sin(a*tau/sides+phase), z)
             for z, rx, ry in levels for a in range(sides)]
        f = [tuple(reversed(range(sides)))]
        ids = [mats[0]]
        for i in range(len(levels)-1):
            for a in range(sides):
                b = (a+1)%sides
                f.append((i*sides+a,i*sides+b,(i+1)*sides+b,(i+1)*sides+a))
                ids.append(mats[min(i,len(mats)-1)])
        f.append(tuple(range(len(v)-sides,len(v))))
        ids.append(mats[-1])
        self.add(v,f,ids)

    def box(self, x,y,z, dx,dy,dz, mat, top=None, tilt=0):
        # Optional fictional angled launcher housing, around its own center.
        a = radians(tilt)
        v = [(x+i*dx/2, y+j*dy/2*cos(a)-k*dz/2*sin(a),
              z+j*dy/2*sin(a)+k*dz/2*cos(a))
             for k in (-1,1) for j in (-1,1) for i in (-1,1)]
        self.add(v, [(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)],
                 [mat,mat if top is None else top,mat,mat,mat,mat])

    def build(self,name,pivot=(0,0,0),parent=None):
        data=bpy.data.meshes.new(name+' mesh')
        data.from_pydata([(x-pivot[0],y-pivot[1],z-pivot[2]) for x,y,z in self.verts],[],self.faces)
        data.update()
        for m in palette: data.materials.append(m)
        for p,i in zip(data.polygons,self.ids): p.material_index=i
        bm=bmesh.new()
        bm.from_mesh(data)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bm.to_mesh(data)
        bm.free()
        data.update()
        obj=bpy.data.objects.new(name,data)
        scene.collection.objects.link(obj)
        obj.parent=parent
        obj.location=(0,0,0) if parent else pivot
        data.calc_loop_triangles()
        return obj


def marker(name,position,parent):
    obj=bpy.data.objects.new(name,None)
    scene.collection.objects.link(obj)
    obj.parent=parent
    obj.location=position
    obj.empty_display_size=0.32
    return obj


base=Geometry()
base.platform(0,0,[(0,6.2,6.2),(0.35,6.2,6.2),(0.65,5.65,5.65)],[FOUND,ARMOR,FOUND])
base.platform(0,0,[(0.60,4.85,4.85),(1.28,4.85,4.85),(2.10,3.45,3.45)], [ARMOR,PANEL,UPPER])
base.platform(0,0,[(2.05,2.7,2.7),(2.55,2.7,2.7),(2.75,2.35,2.35)], [DARK,PANEL,DARK], sides=12, phase=0)
for x in (-3.7,3.7):
    for y in (-3.7,3.7):
        base.box(x,y,0.86,0.95,0.95,0.48,PANEL,UPPER)
for x in (-2.0,2.0):
    base.box(x,4.0,0.93,1.1,0.55,0.34,AMBER)
base_obj=base.build('Base')

# +Y is the visual forward direction. Head rotates around vertical axis at PIVOT_Z.
head=Geometry()
head.platform(0,0,[(2.68,2.48,2.35),(3.12,3.0,2.80),(3.72,2.70,2.50)], [PANEL,ARMOR,UPPER])
head.box(0,-0.25,4.45,2.35,3.35,1.85,ARMOR,UPPER)
head.box(0,1.53,4.65,1.95,0.45,0.88,DARK)
head.box(0,1.80,4.70,1.55,0.13,0.46,SENSOR)
head.box(0,-0.6,5.48,1.45,1.7,0.32,PANEL,UPPER)
head.box(0,-1.4,5.75,0.45,0.55,0.28,AMBER)
for side in (-1,1):
    x=side*2.55
    head.box(side*1.55,0,4.02,1.9,2.7,0.85,PANEL,UPPER)
    # Chunky stylized twin pods; details are decorative, not a functional weapon design.
    head.box(x,-0.05,5.15,2.35,4.35,2.12,ARMOR,UPPER,tilt=18)
    a=radians(18)
    # Front plate and two graphic dark recesses are perpendicular to the pod axis.
    fy=-0.05+2.20*cos(a); fz=5.15+2.20*sin(a)
    head.box(x,fy,fz,2.28,0.24,1.98,PANEL,tilt=18)
    for dx in (-0.51,0.51):
        head.box(x+dx,fy+0.15*cos(a),fz+0.15*sin(a),0.76,0.12,1.40,DARK,tilt=18)
    # Wide safety markings on top, readable from the game camera.
    yy=-0.95; zz=1.10
    head.box(x,-0.05+yy*cos(a)-zz*sin(a),5.15+yy*sin(a)+zz*cos(a),
             2.16,0.62,0.10,AMBER,tilt=18)

pivot=bpy.data.objects.new('TurretHead',None)
scene.collection.objects.link(pivot)
pivot.location=(0,0,PIVOT_Z)
pivot.empty_display_type='PLAIN_AXES'
pivot.empty_display_size=0.65
head_obj=head.build('HeadMesh',(0,0,PIVOT_Z),pivot)
marker('AimForward',(0,3.0,1.0),pivot)
marker('LauncherLeft',(-2.55,2.28,5.15+2.20*sin(radians(18))-PIVOT_Z),pivot)
marker('LauncherRight',(2.55,2.28,5.15+2.20*sin(radians(18))-PIVOT_Z),pivot)

for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            s=area.spaces.active
            s.shading.type='SOLID'
            s.shading.color_type='MATERIAL'
            s.region_3d.view_distance=25
            s.region_3d.view_location=(0,0,2.8)
            s.region_3d.view_rotation=Euler((radians(35),0,radians(-25)),'XYZ').to_quaternion()
            s.region_3d.view_perspective='ORTHO'
            s.overlay.show_floor=False
            s.overlay.show_axis_x=False
            s.overlay.show_axis_y=False

SOURCE.parent.mkdir(parents=True,exist_ok=True)
EXPORT.parent.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
for o in scene.objects: o.select_set(True)
bpy.context.view_layer.objects.active=base_obj
bpy.ops.export_scene.fbx(filepath=str(EXPORT),use_selection=True,object_types={'MESH','EMPTY'},
                         axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,
                         add_leaf_bones=False)
print(f'TURRET READY: 2 meshes, {len(base_obj.data.loop_triangles)+len(head_obj.data.loop_triangles)} triangles')
print(f'BLEND: {SOURCE}\nFBX: {EXPORT}')
