#!/usr/bin/env python3
import bpy
import bmesh
import math
import os
import random
import sys
from mathutils import Vector

ROOT = os.getcwd()
ART_DIR = os.path.join(ROOT, "Assets", "_Project", "Art", "Environment", "RockKitR1")
FBX_PATH = os.path.join(ART_DIR, "RockKitR1.fbx")
BLEND_PATH = os.path.join(ART_DIR, "RockKitR1.blend")
MARKER_PATH = os.path.join(ART_DIR, "RockKitR1.ready.txt")


def ensure_dirs():
    os.makedirs(ART_DIR, exist_ok=True)


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in list(bpy.data.meshes):
        if block.users == 0:
            bpy.data.meshes.remove(block)
    for block in list(bpy.data.materials):
        if block.users == 0:
            bpy.data.materials.remove(block)


def make_material(name, color):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        # Blender 5.x changed socket ordering; address inputs by name.
        bsdf.inputs['Base Color'].default_value = (*color, 1.0)
        bsdf.inputs['Metallic'].default_value = 0.0
        bsdf.inputs['Roughness'].default_value = 0.85
    return mat


def convex_rock(name, seed, size=(8, 12, 10), points=24, flat_left=False, ridge=False):
    rng = random.Random(seed)
    bm = bmesh.new()
    sx, sy, sz = size
    for _ in range(points):
        x = rng.uniform(-sx, sx)
        y = rng.uniform(-sy, sy)
        z = rng.uniform(0.0, sz)
        # Bias points upward and toward the center for more stable hulls.
        z *= rng.uniform(0.65, 1.15)
        x *= rng.uniform(0.7, 1.1)
        y *= rng.uniform(0.7, 1.1)
        if flat_left and x < -sx * 0.35:
            x = -sx * rng.uniform(0.85, 1.0)
            z *= rng.uniform(0.9, 1.25)
        if ridge:
            y *= 0.45
            x *= 1.3
        bm.verts.new((x, y, z))
    bm.verts.ensure_lookup_table()
    bmesh.ops.convex_hull(bm, input=list(bm.verts))
    # geom_unused and geom_interior can overlap in Blender 5.x. Do not
    # concatenate those lists: delete each unconnected vertex exactly once.
    loose_verts = [vert for vert in bm.verts if not vert.link_faces]
    if loose_verts:
        bmesh.ops.delete(bm, geom=loose_verts, context='VERTS')
    if not bm.faces:
        bm.free()
        raise RuntimeError(f'Convex hull produced no faces for {name}')
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    # Cut a flatter bottom.
    bottom = [v for v in bm.verts if v.co.z < sz * 0.16]
    if bottom:
        avg = sum(v.co.z for v in bottom) / len(bottom)
        for v in bottom:
            v.co.z = avg * 0.2

    # Lightly shape the silhouette.
    for v in bm.verts:
        if flat_left and v.co.x < 0:
            v.co.x *= 0.92
        if ridge and abs(v.co.y) < sy * 0.2 and v.co.z > sz * 0.5:
            v.co.z *= 1.12

    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.shade_flat()
    return obj


def join_named(name, objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    merged = bpy.context.view_layer.objects.active
    merged.name = name
    merged.data.name = name
    return merged


def create_talus(name, seed):
    rng = random.Random(seed)
    chunks = []
    for i in range(7):
        rock = convex_rock(f"{name}_part_{i}", seed + i * 17, size=(2.5, 2.0, 2.1), points=10)
        rock.location = Vector((rng.uniform(-8, 8), rng.uniform(-4, 4), rng.uniform(0, 2.0)))
        rock.rotation_euler[2] = rng.uniform(-1.2, 1.2)
        chunks.append(rock)
    return join_named(name, chunks)


def create_scene():
    clear_scene()
    cliff = convex_rock("Cliff_A", 1001, size=(14, 10, 22), points=32, flat_left=True)
    ridge = convex_rock("Ridge_B", 2001, size=(13, 6, 9), points=22, ridge=True)
    talus = create_talus("Talus_C", 3001)

    cliff.location = Vector((0, 0, 0))
    ridge.location = Vector((42, 0, 0))
    talus.location = Vector((82, 0, 0))

    mats = {
        "Cliff_A": make_material("Cliff_A", (0.44, 0.37, 0.31)),
        "Ridge_B": make_material("Ridge_B", (0.48, 0.41, 0.34)),
        "Talus_C": make_material("Talus_C", (0.57, 0.49, 0.41)),
    }
    for obj in (cliff, ridge, talus):
        obj.data.materials.clear()
        obj.data.materials.append(mats[obj.name])

    root = bpy.data.objects.new("RockKitR1", None)
    bpy.context.collection.objects.link(root)
    for obj in (cliff, ridge, talus):
        obj.parent = root

    return root


def export_assets(root):
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for child in root.children:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        object_types={'EMPTY', 'MESH'},
        mesh_smooth_type='FACE',
        add_leaf_bones=False,
        bake_space_transform=False,
        axis_forward='-Z',
        axis_up='Y',
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_NONE')
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    with open(MARKER_PATH, 'w', encoding='utf-8') as handle:
        handle.write('ROCKKIT_R1_READY\n')


def main():
    ensure_dirs()
    root = create_scene()
    export_assets(root)
    print(f"R1 rock kit exported to {FBX_PATH}")


if __name__ == '__main__':
    main()
