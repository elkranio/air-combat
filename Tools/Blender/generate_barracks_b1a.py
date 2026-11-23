#!/usr/bin/env python3
"""B1a reusable barracks geometry; B1b stylized military pass.

Same OBJ paths, object names and Unity import GUIDs as B1a.
Run to regenerate the existing meshes; their three prefab instances update directly.
(Use git history for an exact B1a comparison.)

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
            f.write('# B1b stylized barracks pass; existing B1a model name and Unity GUID retained\n')
            f.write('o Barracks_B1a_' + self.name + '\ns off\n')
            for x, y, z in self.vertices:
                f.write(f'v {x:.4f} {y:.4f} {z:.4f}\n')
            for a, b, c in self.faces:
                f.write(f'f {a} {b} {c}\n')
        print(f'{path.relative_to(Path(__file__).resolve().parents[2])}: {len(self.faces)} triangles')


# World footprint / wall frame deliberately remain identical to B1a.
# Only hero-readable silhouette details change: roof, canopy, two vents, entrance.
# +X side is the prominent long façade from the approved -45-degree camera.
walls = Builder('Walls')
walls.cuboid(-5.2, .35, -15.25, 5.2, 4.25, 15.25)
# Gable is buried slightly inside the roof underside, so there are no gaps.
walls.face((-5.2, 4.25, -15.25), (5.2, 4.25, -15.25), (0, 6.18, -15.25))
walls.face((5.2, 4.25, 15.25), (-5.2, 4.25, 15.25), (0, 6.18, 15.25))
# Two large, low utility vents; intentionally not detailed ventilation slats.
# On the camera-facing roof slope. Lower half intersects the sloped roof.
for z0, z1 in ((-10.2, -6.8), (7.0, 10.4)):
    walls.cuboid(1.65, 5.22, z0, 4.05, 6.68, z1)
walls.write()

roof = Builder('Roof')
# More deliberate, slightly taller and thicker roof, not cartoonishly oversized.
# Compare B1a: eaves ±5.85, ridge 6.15, thickness .20.
# B1b: eaves ±6.4, ridge 6.62, thickness .38; same 32m building length.
x, eave, ridge = 6.4, 4.00, 6.62
thick = .38
z0, z1 = -16.42, 16.42
roof.face((-x,eave,z0), (0,ridge,z0), (0,ridge,z1), (-x,eave,z1))
roof.face((0,ridge,z0), (x,eave,z0), (x,eave,z1), (0,ridge,z1))
roof.face((-x,eave,z0), (-x,eave,z1), (-x,eave-thick,z1), (-x,eave-thick,z0))
roof.face((x,eave,z1), (x,eave,z0), (x,eave-thick,z0), (x,eave-thick,z1))
# Gable end caps (front and back), full roof depth.
roof.face((-x,eave,z0), (-x,eave-thick,z0), (0,ridge-thick,z0), (0,ridge,z0))
roof.face((0,ridge,z0), (0,ridge-thick,z0), (x,eave-thick,z0), (x,eave,z0))
roof.face((-x,eave-thick,z1), (-x,eave,z1), (0,ridge,z1), (0,ridge-thick,z1))
roof.face((0,ridge-thick,z1), (0,ridge,z1), (x,eave,z1), (x,eave-thick,z1))
# Large entrance porch / canopy, centered on the long visible +X wall.
# It reads primarily from ABOVE at Y150 and gives the roof a distinct plan shape.
roof.cuboid(5.22, 3.66, -3.65, 9.05, 4.08, 3.65)
roof.write()

openings = Builder('Openings')
for side in (-1, 1):
    face_x = side * 5.26
    # Central entrance on the visible wall is clearly wider than a window.
    half_door = 1.55 if side == 1 else 1.16
    if side == 1:
        openings.cuboid(face_x, .35, -half_door, face_x + .11, 3.44, half_door)
    else:
        openings.cuboid(face_x - .11, .35, -half_door, face_x, 3.44, half_door)
    for z in (-11.55, -6.25, 6.25, 11.55):
        xa, xb = (face_x, face_x + .095) if side == 1 else (face_x - .095, face_x)
        openings.cuboid(xa, 2.25, z - 1.29, xb, 3.51, z + 1.29)
openings.cuboid(-1.18, .35, -15.39, 1.18, 3.24, -15.25)
openings.write()

trim = Builder('Trim')
# Subtle warm foundation contrasts the muted ground and cool building body.
trim.cuboid(-5.6, -.10, -15.65, 5.6, .35, 15.65)
for side in (-1, 1):
    xa, xb = (5.21, 5.38) if side == 1 else (-5.38, -5.21)
    for z in (-11.55, -6.25, 6.25, 11.55):
        trim.cuboid(xa, 2.10, z - 1.46, xb, 2.26, z + 1.46)
        trim.cuboid(xa, 3.49, z - 1.46, xb, 3.62, z + 1.46)
    if side == 1:
        # Main entry reads from the side and roof, not only as a painted door.
        trim.cuboid(xa, 3.42, -1.85, xb, 3.60, 1.85)
        trim.cuboid(xa, .35, -1.85, xb, 3.42, -1.67)
        trim.cuboid(xa, .35, 1.67, xb, 3.42, 1.85)
# Two stout canopy posts; visible protruding volume, no tiny railings.
for z in (-3.05, 3.05):
    trim.cuboid(8.42, .35, z - .27, 8.82, 3.66, z + .27)
# One warm highlight on outward face of the overhang; not painted into albedo.
trim.cuboid(9.04, 3.68, -2.05, 9.13, 4.10, 2.05)
# Two metal vent caps, clear silhouettes when flying at Y150.
for z0, z1 in ((-10.2, -6.8), (7.0, 10.4)):
    trim.cuboid(1.48, 6.68, z0 - .15, 4.22, 6.87, z1 + .15)
trim.write()
