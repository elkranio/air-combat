#!/usr/bin/env python3
"""Split the accepted Picket art into an animation-ready visual assembly.
Run with Python (OBJ) or Blender (OBJ + editable blend). Never overwrites C2.
"""
from pathlib import Path
from collections import defaultdict
import runpy

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Environment/Dominion/SAM01_Picket_Mobile'
BASE = Path(__file__).with_name('build_sam01_picket.py')


def split_parts(parts):
    groups = defaultdict(list)
    for name, group, vertices, triangles in parts:
        if name.startswith('Outrigger arm '):
            if not name.endswith(' 1'): continue  # symmetric pair of supports
            dest, dx = 'SupportArm', 3.0
        elif name.startswith(('Outrigger upright ', 'Outrigger foot ')):
            if not name.endswith(' 1'): continue
            dest = 'SupportFoot' if name.startswith('Outrigger foot ') else 'SupportLeg'
            dx = 4.095
        elif group in ('Metal', 'Equipment'):
            dest, dx = 'Body' + group, 0
        elif group in ('Launcher', 'LauncherDark', 'Accent'):
            moving = name.startswith(('Open rail ', 'Rail side panel ', 'Rail marking '))
            dest, dx = ('Rail' if moving else 'Base') + group, 0
        else:
            continue  # unchanged hull, rubber, glass, caps and two rockets reused
        groups[dest].append((name, [(x-dx, y, z) for x,y,z in vertices], triangles))
    return groups


def write_obj(group, parts):
    lines = ['# Picket mobile visual; Y-up +Z forward', 'o SAM01_Mobile_' + group, 's off']
    offset = 0
    for _, vertices, triangles in parts:
        lines.extend('v ' + ' '.join(f'{v:.6f}' for v in p) for p in vertices)
        lines.extend('f ' + ' '.join(str(i+offset+1) for i in face) for face in triangles)
        offset += len(vertices)
    (OUT / ('SAM01_Mobile_' + group + '.obj')).write_text('\n'.join(lines)+'\n', encoding='utf-8')


def main():
    base = runpy.run_path(str(BASE))
    groups = split_parts(base['PARTS'])
    assert len(groups) == 11, sorted(groups)
    OUT.mkdir(parents=True, exist_ok=True)
    for group, parts in groups.items():
        write_obj(group, parts)
    print(f'{len(groups)} split meshes; original Picket untouched')
    try:
        import bpy
    except ImportError:
        return
    # Full editable reference in the original deployed pose. Each piece
    # has its own origin; the animation-ready Unity prefab owns its pivots.
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    materials = {}
    for group, color in base['PALETTE'].items():
        mat = bpy.data.materials.new(group)
        mat.diffuse_color = (*color, 1)
        materials[group] = mat
    for name, group, verts, faces in base['PARTS']:
        c = tuple(sum(p[i] for p in verts)/len(verts) for i in range(3))
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([(x-c[0], -(z-c[2]), y-c[1]) for x,y,z in verts], [], faces)
        assert not mesh.validate(), name
        mesh.update()
        mesh.materials.append(materials[group])
        ob = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(ob)
        ob.location = (c[0], -c[2], c[1])
    path = ROOT / 'Resources/Blender/SAM01_Picket_Mobile.blend'
    path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))


if __name__ == '__main__':
    main()
