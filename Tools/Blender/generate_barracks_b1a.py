#!/usr/bin/env python3
"""B1a barracks source geometry (Python stdlib, OBJ can be edited in Blender).

World units are meters; Y up, Z long axis. Four independent section meshes
for deterministic URP flat materials, no albedo/normal/AO bake or UV stretching.
Run: python3 Tools/Blender/generate_barracks_b1a.py
"""
from pathlib import Path

OUT = Path(__file__).resolve().parents[2] / 'Assets/_Project/Art/Buildings/Base/Barracks_B1a'


class Builder:
    def __init__(self, name):
        self.name = name
        self.vertices = []
        self.faces = []

    def face(self, *points):
        # Duplicate vertices by face: no accidental smoothed normals on large planes.
        # The authored polygon listing is clockwise when viewed outside;
        # reverse it for correct outward normals after OBJ import.
        points = tuple(reversed(points))
        i = len(self.vertices) + 1
        self.vertices.extend(points)
        if len(points) == 4:
            self.faces.extend([(i, i + 1, i + 2), (i, i + 2, i + 3)])
        elif len(points) == 3:
            self.faces.append((i, i + 1, i + 2))
        else:
            raise ValueError('Only triangles/quads')

    def cuboid(self, xmin, ymin, zmin, xmax, ymax, zmax):
        a = (xmin, ymin, zmin); b = (xmax, ymin, zmin)
        c = (xmax, ymin, zmax); d = (xmin, ymin, zmax)
        e = (xmin, ymax, zmin); f = (xmax, ymax, zmin)
        g = (xmax, ymax, zmax); h = (xmin, ymax, zmax)
        # Outwards normals. Roof/walls are hard-edge intentionally.
        self.face(a, d, c, b)  # down
        self.face(e, f, g, h)  # up
        self.face(a, b, f, e)  # front (-Z)
        self.face(d, h, g, c)  # rear (+Z)
        self.face(a, e, h, d)  # left (-X)
        self.face(b, c, g, f)  # right (+X)

    def write(self):
        OUT.mkdir(parents=True, exist_ok=True)
        path = OUT / ('Barracks_B1a_' + self.name + '.obj')
        with path.open('w') as f:
            f.write('# B1a authored barracks section; dimensions 11.6 x 32 x 6.15m\n')
            f.write('o Barracks_B1a_' + self.name + '\ns off\n')
            for x, y, z in self.vertices:
                f.write(f'v {x:.4f} {y:.4f} {z:.4f}\n')
            for a, b, c in self.faces:
                f.write(f'f {a} {b} {c}\n')
        print(f'{path.relative_to(Path(__file__).resolve().parents[2])}: {len(self.faces)} triangles')


walls = Builder('Walls')
walls.cuboid(-5.2, .35, -15.25, 5.2, 4.25, 15.25)
# Two readable gable triangles, wall-colored, filling under the roof ridge.
walls.face((-5.2, 4.25, -15.25), (5.2, 4.25, -15.25), (0, 5.98, -15.25))
walls.face((5.2, 4.25, 15.25), (-5.2, 4.25, 15.25), (0, 5.98, 15.25))
walls.write()

roof = Builder('Roof')
# One wide low-pitch gable, not a corrugated micro-detail mesh.
# Thick two-slope roof silhouette, eave overhang .65m on all sides.
L = (-5.85, 4.18); R = (5.85, 4.18); C = (0, 6.15)
z0, z1 = -16.05, 16.05
roof.face((L[0],L[1],z0), (0,6.15,z0), (0,6.15,z1), (L[0],L[1],z1))
roof.face((0,6.15,z0), (R[0],R[1],z0), (R[0],R[1],z1), (0,6.15,z1))
# Fascia / visible thickness as a continuous folded silhouette.
roof.face((-5.85,4.18,z0),(-5.85,4.18,z1),(-5.85,3.98,z1),(-5.85,3.98,z0))
roof.face((5.85,4.18,z1),(5.85,4.18,z0),(5.85,3.98,z0),(5.85,3.98,z1))
roof.face((-5.85,4.18,z0),(-5.85,3.98,z0),(0,5.93,z0),(0,6.15,z0))
roof.face((0,6.15,z0),(0,5.93,z0),(5.85,3.98,z0),(5.85,4.18,z0))
roof.face((-5.85,3.98,z1),(-5.85,4.18,z1),(0,6.15,z1),(0,5.93,z1))
roof.face((0,5.93,z1),(0,6.15,z1),(5.85,4.18,z1),(5.85,3.98,z1))
roof.write()

openings = Builder('Openings')
# Doors and window panels are separate from the cool gray walls for gameplay
# readability. Flat shapes, no recessed interiors or costly baked textures.
for side in (-1, 1):
    x = side * 5.26
    # Door midway along side (on the normally visible +X long wall).
    if side == 1:
        openings.cuboid(x, .35, -1.16, x + .11, 3.44, 1.16)
    else:
        openings.cuboid(x - .11, .35, -1.16, x, 3.44, 1.16)
    for z in (-11.55, -6.25, 6.25, 11.55):
        xa, xb = (x, x + .095) if side == 1 else (x - .095, x)
        openings.cuboid(xa, 2.25, z - 1.29, xb, 3.51, z + 1.29)
# Smaller end door at -Z: another readability clue from a flight pass.
openings.cuboid(-1.18, .35, -15.39, 1.18, 3.24, -15.25)
openings.write()

trim = Builder('Trim')
trim.cuboid(-5.6, -.10, -15.65, 5.6, .35, 15.65)  # foundation
# Broad window borders and door frames, not micro-mullions.
for side in (-1, 1):
    xa, xb = (5.21, 5.38) if side == 1 else (-5.38, -5.21)
    for z in (-11.55, -6.25, 6.25, 11.55):
        trim.cuboid(xa, 2.10, z - 1.46, xb, 2.26, z + 1.46)  # sill
        trim.cuboid(xa, 3.49, z - 1.46, xb, 3.62, z + 1.46)  # lintel
    if side == 1:
        trim.cuboid(xa, 3.42, -1.42, xb, 3.59, 1.42)  # door lintel
        trim.cuboid(xa, .35, -1.42, xb, 3.42, -1.26)
        trim.cuboid(xa, .35, 1.26, xb, 3.42, 1.42)
# Ridge cap, continuous and single-color: one high-level detail visible from sky.
trim.cuboid(-.12, 6.11, -16.12, .12, 6.22, 16.12)
trim.write()
