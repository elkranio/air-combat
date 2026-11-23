#!/usr/bin/env python3
"""B1c / military headquarters and separate courtyard models.

Run from any directory: python3 Tools/Blender/generate_hq_b1c.py
World units are meters. Y = up, HQ frontage points toward -Z.
Each OBJ is a separate editable hard-edge mesh; all sunlight/shadows stay in URP.
Source needs only Python stdlib (Blender is optional for creative editing).
"""
from pathlib import Path
import math

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Art/Buildings/Base/HQ_B1c'


class Mesh:
    def __init__(self, name):
        self.name = name
        self.vertices = []
        self.faces = []

    def face(self, *points):
        # Same vertex winding as our accepted B1 barracks OBJ generator.
        # Duplicate face vertices: intentionally flat, not smoothed.
        points = tuple(reversed(points))
        start = len(self.vertices) + 1
        self.vertices.extend(points)
        if len(points) == 4:
            self.faces.extend(((start, start + 1, start + 2),
                               (start, start + 2, start + 3)))
        elif len(points) == 3:
            self.faces.append((start, start + 1, start + 2))
        else:
            raise ValueError('Only triangles/quads supported')

    def box(self, xmin, ymin, zmin, xmax, ymax, zmax):
        assert xmax > xmin and ymax > ymin and zmax > zmin
        a = (xmin, ymin, zmin); b = (xmax, ymin, zmin)
        c = (xmax, ymin, zmax); d = (xmin, ymin, zmax)
        e = (xmin, ymax, zmin); f = (xmax, ymax, zmin)
        g = (xmax, ymax, zmax); h = (xmin, ymax, zmax)
        self.face(a, d, c, b)
        self.face(e, f, g, h)
        self.face(a, b, f, e)
        self.face(d, h, g, c)
        self.face(a, e, h, d)
        self.face(b, c, g, f)

    def octagonal_post(self, x, z, radius, bottom, top, sides=8):
        bottom_ring = []
        top_ring = []
        for i in range(sides):
            angle = math.tau * i / sides
            dx, dz = radius * math.cos(angle), radius * math.sin(angle)
            bottom_ring.append((x + dx, bottom, z + dz))
            top_ring.append((x + dx, top, z + dz))
        for i in range(sides):
            j = (i + 1) % sides
            self.face(bottom_ring[i], bottom_ring[j], top_ring[j], top_ring[i])
        for i in range(1, sides - 1):
            self.face(top_ring[0], top_ring[i], top_ring[i + 1])
            self.face(bottom_ring[i + 1], bottom_ring[i], bottom_ring[0])

    def bush(self, x, z, width, height):
        # Chunky sculpted 6-sided mound; no needles/leaves that vanish at Y78.
        ring0 = []
        ring1 = []
        for i in range(6):
            a = math.tau * i / 6
            ring0.append((x + width * math.cos(a), .75, z + width * math.sin(a)))
            ring1.append((x + width * .72 * math.cos(a), .75 + height * .60,
                          z + width * .72 * math.sin(a)))
        apex = (x - .08, .75 + height, z + .12)
        for i in range(6):
            j = (i + 1) % 6
            self.face(ring0[i], ring0[j], ring1[j], ring1[i])
            self.face(ring1[i], ring1[j], apex)

    def write(self):
        assert self.vertices and self.faces, self.name
        OUT.mkdir(parents=True, exist_ok=True)
        file = OUT / ('HQ_B1c_' + self.name + '.obj')
        with file.open('w', encoding='utf-8', newline='\n') as output:
            output.write(f'# HQ B1c authored section {self.name}; meters; Y up; -Z front\n')
            output.write(f'o HQ_B1c_{self.name}\ns off\n')
            for v in self.vertices:
                output.write('v ' + ' '.join(f'{a:.4f}' for a in v) + '\n')
            for face in self.faces:
                output.write('f ' + ' '.join(str(i) for i in face) + '\n')
        print(f'{file.relative_to(ROOT)}  {len(self.faces)} triangles')


# COMMAND HQ / winged symmetrical one+two-storey functional architecture.
# 29m width x 19m deep; generous roof stepping is the primary top-down read.
body = Mesh('Body')
body.box(-6.25, .48, -9.0, 6.25, 7.70, 8.7)        # raised command centre
body.box(-14.0, .48, -8.5, -6.25, 4.95, 8.5)      # west office wing
body.box(6.25, .48, -8.5, 14.0, 4.95, 8.5)        # east office wing
body.write()

roof = Mesh('Roof')
# Thick matte slab roofs; slightly generous eaves, no photoreal shingles.
roof.box(-7.0, 7.70, -9.75, 7.0, 8.35, 9.4)
roof.box(-14.75, 4.95, -9.35, -6.05, 5.45, 9.25)
roof.box(6.05, 4.95, -9.35, 14.75, 5.45, 9.25)
# Raised roof ventilation masses and one small communications plinth.
roof.box(-3.35, 8.35, 1.25, -1.0, 9.35, 4.65)
roof.box(1.10, 8.35, 3.10, 4.25, 9.10, 5.60)
roof.write()

openings = Mesh('Openings')
# Broad graphic window groups, strong at camera height 78.
for x in (-11.65, -8.5, 8.5, 11.65):
    openings.box(x-1.20, 2.3, -8.67, x+1.20, 3.8, -8.50)
for x in (-4.1, 4.1):
    openings.box(x-1.25, 2.25, -9.18, x+1.25, 4.20, -9.00)
    openings.box(x-1.25, 5.65, -9.18, x+1.25, 6.65, -9.00)
# Central double entrance; physically separate entrance *porch* in the scene.
openings.box(-1.85, .49, -9.20, 1.85, 4.15, -9.00)
for z in (-4.4, 3.4):
    openings.box(-14.15, 2.45, z-1.30, -14.0, 3.8, z+1.30)
    openings.box(14.0, 2.45, z-1.30, 14.15, 3.8, z+1.30)
openings.write()

trim = Mesh('Trim')
trim.box(-14.45, -.12, -9.20, 14.45, .48, 9.0)      # raised foundation
trim.box(-6.55, 4.85, -9.28, 6.55, 5.17, -9.00)  # central horizontal band
trim.box(-6.5, 7.45, -9.85, 6.5, 7.80, -9.67)     # strong command fascia
for x in (-13.45, 13.45):
    trim.box(x-.28, .48, -8.72, x+.28, 4.96, -8.46)  # corners, not ornament
trim.write()

accent = Mesh('Accent')
# One restrained warm service/command highlight, not an emblem or signage baked in.
accent.box(-3.6, 7.83, -9.90, 3.6, 8.16, -9.74)
accent.box(-2.35, 4.17, -9.34, 2.35, 4.40, -9.10)
accent.write()

# Porch / entrance is its own prefab and can be relocated independently.
# Local origin at HQ's front wall, so the site installer places it at Z=-9.
entry_concrete = Mesh('Entry_Concrete')
entry_concrete.box(-5.5, .06, -6.0, 5.5, .56, .25)
entry_concrete.box(-4.55, -.02, -7.35, 4.55, .30, -5.95)
entry_concrete.box(-3.85, -.06, -8.30, 3.85, .13, -7.32)
entry_concrete.write()
entry_dark = Mesh('Entry_Dark')
entry_dark.box(-6.5, 4.48, -5.90, 6.5, 5.03, .45)  # heavy entrance canopy
for x in (-5.65, 5.65):
    entry_dark.box(x-.37, .56, -5.25, x+.37, 4.48, -4.51)
entry_dark.write()
entry_accent = Mesh('Entry_Accent')
entry_accent.box(-2.6, 4.62, -6.01, 2.6, 4.95, -5.90)
entry_accent.write()

# Plaza is a separate prefab, flat on the terrain; extend roads to its outer edge later.
plaza = Mesh('Plaza')
plaza.box(-19.0, -.05, -38.0, 19.0, .12, -16.0)
plaza.write()

planter_concrete = Mesh('Planter_Concrete')
# Hollow trough: each wall an actual separate low concrete volume.
planter_concrete.box(-3.6, .1, -1.45, 3.6, .32, 1.45)
for x0, x1 in ((-3.6, -3.15), (3.15, 3.6)):
    planter_concrete.box(x0, .32, -1.45, x1, 1.03, 1.45)
for z0, z1 in ((-1.45, -1.05), (1.05, 1.45)):
    planter_concrete.box(-3.15, .32, z0, 3.15, 1.03, z1)
planter_concrete.write()
planter_soil = Mesh('Planter_Soil')
planter_soil.box(-3.10, .32, -1.0, 3.10, .72, 1.0)
planter_soil.write()
planter_green = Mesh('Planter_Green')
for x in (-2.05, 0, 2.05):
    planter_green.bush(x, 0.05, .82, .90)
planter_green.write()

# Flagpole separate from building and paving; no baked shadows/transparent texture.
pole = Mesh('Flag_Pole')
pole.box(-.90, -.1, -.9, .90, .65, .9)  # anchor block
pole.octagonal_post(0, 0, .16, .65, 12.35)
pole.write()
flag = Mesh('Flag_Cloth')
# Solid two-sided slightly folded fabric, muted orange. Independent material.
fabric = [(0.18, 11.65, 0), (1.55, 11.50, .08), (3.85, 11.72, -.18),
          (3.80, 9.65, -.12), (1.55, 9.48, .06), (0.18, 9.65, 0)]
for face in ((fabric[0],fabric[1],fabric[4],fabric[5]),
             (fabric[1],fabric[2],fabric[3],fabric[4])):
    flag.face(*face)
    flag.face(*reversed(face))
flag.write()
