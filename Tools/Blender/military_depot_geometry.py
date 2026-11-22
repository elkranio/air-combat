"""G2 Military Depot source geometry, pure Python (no Blender dependency).

Coordinates: Blender metres, X/Y ground plane, Z vertical. Every part is
intentionally broad enough to survive a Unity ortho 80 flight camera.
Meshes are grouped by material: a few game renderers, not dozens of cubes.
"""
import math
from collections import OrderedDict

ROOF_WIDTH = 30.0
ROOF_DEPTH = 20.0
ROOF_Z = 8.25


class Geometry:
    def __init__(self):
        self.groups = OrderedDict()

    def add(self, material, vertices, faces):
        entry = self.groups.setdefault(material, {'vertices': [], 'faces': []})
        start = len(entry['vertices'])
        entry['vertices'].extend(tuple(map(float, v)) for v in vertices)
        entry['faces'].extend(tuple(start + i for i in face) for face in faces)

    def box(self, material, cx, cy, cz, sx, sy, sz, yaw=0):
        x, y, z = sx/2, sy/2, sz/2
        vertices = [(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),
                    (-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
        cs, sn = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
        vertices = [(cx + cs*a - sn*b, cy + sn*a + cs*b, cz+c)
                    for a,b,c in vertices]
        faces = [(3,2,1,0),(4,5,6,7),(0,1,5,4),(2,3,7,6),
                 (1,2,6,5),(3,0,4,7)]
        self.add(material, vertices, faces)

    def chamfer_block(self, material, cx, cy, z0, z1, width, depth, bevel=1.0, top_inset=0.0, yaw=0):
        def ring(half_x,half_y,at_z):
            b=min(bevel,half_x*.45,half_y*.45)
            points=[(-half_x+b,-half_y),(half_x-b,-half_y),
                    (half_x,-half_y+b),(half_x,half_y-b),
                    (half_x-b,half_y),(-half_x+b,half_y),
                    (-half_x,half_y-b),(-half_x,-half_y+b)]
            cs,sn=math.cos(math.radians(yaw)),math.sin(math.radians(yaw))
            return [(cx+cs*x-sn*y,cy+sn*x+cs*y,at_z) for x,y in points]
        vertices = ring(width/2,depth/2,z0) + ring(width/2-top_inset,depth/2-top_inset,z1)
        faces=[tuple(reversed(range(8))),tuple(range(8,16))]
        faces.extend((i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8))
        self.add(material,vertices,faces)

    def cylinder(self, material, cx,cy,z0,height,radius,sides=12,tilt=0):
        angles=[2*math.pi*i/sides+math.radians(tilt) for i in range(sides)]
        vertices=[(cx+radius*math.cos(a),cy+radius*math.sin(a),z0) for a in angles]
        vertices += [(x,y,z0+height) for x,y,_ in vertices]
        faces=[tuple(reversed(range(sides))),tuple(range(sides,2*sides))]
        faces.extend((i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides))
        self.add(material,vertices,faces)


def make_depot():
    g=Geometry()
    # 76 x 54 footprint with obvious human-made hardstand, not a full world tile.
    g.chamfer_block('Depot_Concrete',0,0,0,.48,76,54,3.2)
    g.chamfer_block('Depot_Concrete',0,0,.48,1.0,40,30,1.2)
    # Main shelter has a slightly battered, sloped concrete shoulder.
    g.chamfer_block('Depot_Concrete',0,0,1.0,8.0,34,24,1.1, .65)
    # Massive roof rim casts real light and preserves the bunker silhouette.
    for x in (-15.65,15.65):
        g.box('Depot_DarkSteel',x,0,8.33,.95,22.4,.9)
    for y in (-10.65,10.65):
        g.box('Depot_DarkSteel',0,y,8.33,31.3,.95,.9)
    # Door and loading threshold on southern wall; structural details not texture noise.
    g.box('Depot_DarkSteel',0,-12.05,3.5,7.3,.3,5.1)
    g.box('Depot_Steel',0,-12.26,6.1,9.4,1.15,.7)
    g.box('Depot_Concrete',0,-15.6,.73,9.5,7.2,.48)
    for x in (-2.65,2.65):
        g.box('Depot_Steel',x,-12.33,3.42,.17,.35,4.45)
    # Two roof mechanical units: geometry because it genuinely alters silhouette.
    for x in (-9.5,9.5):
        g.chamfer_block('Depot_Steel',x,4.2,8.35,9.95,6.6,4.3,.55,.16)
        g.box('Depot_DarkSteel',x,4.2,10.05,5.6,2.5,.22)
    # Fuel tanks: readable pair of cylindrical silhouettes at flight distance.
    for cy in (-6.3,6.3):
        g.cylinder('Depot_Fuel',-24.0,cy,.68,7.3,3.5,12,15)
        g.cylinder('Depot_DarkSteel',-24.0,cy,7.98,.47,3.15,12,15)
        g.cylinder('Depot_DarkSteel',-24.0,cy,.68,.36,3.58,12,15)
        g.cylinder('Depot_Steel',-24.0,cy,8.44,.34,.9,10)
    # Single supply manifold. Keep orthogonal so it reads as equipment, not spaghetti.
    g.box('Depot_DarkSteel',-18.4,0,1.22,7.6,.7,.7)
    g.box('Depot_DarkSteel',-22.2,0,1.25,.7,12.6,.7)
    # Three big supply blocks, one staggered stack.
    for cx,cy,cz,sx,sy,sz in ((24,-6,2.6,7,7,4),(24,3.2,2.6,7,7,4),
                               (24,-6,6.1,5,5,3),(29,11,1.95,5.5,5,2.5)):
        g.chamfer_block('Depot_Crates',cx,cy,cz-sz/2,cz+sz/2,sx,sy,.26)
    # Steel lids/strap bands are real shapes; no tiny repetitive details.
    for cx,cy,top,sx,sy in ((24,-6,4.6,7,7),(24,3.2,4.6,7,7),(24,-6,7.6,5,5)):
        g.box('Depot_DarkSteel',cx,cy,top+.11,sx*.82,.56,.24)
    # Gap-bearing concrete blast walls. Four orientations / non-rectangular outline.
    for x,y,sx,sy,angle in ((-21,-21,12,2.4,0),(-6,-21,10,2.4,0),
                           (18,-22,11,2.4,0),(-18,21,12,2.4,0),
                           (6,21,11,2.4,0),(26,21,10,2.4,0),
                           (-34,-8,12,2.4,90),(34,1,13,2.4,90)):
        g.chamfer_block('Depot_Barricade',x,y,.48,2.65,sx,sy,.65,.27,angle)
    # Subtle hazard bands on the front tanks and the entry line.
    g.box('Depot_Warning',0,-17.8,.77,9.2,.75,.08)
    for cy in (-6.3,6.3):
        g.cylinder('Depot_Warning',-24,cy,6.45,.33,3.54,12,15)

    # Unique UV target for high-to-low roof bake (a single 2-triangle quad).
    g.add('Roof_BakedPanel',
          [(-ROOF_WIDTH/2,-ROOF_DEPTH/2,ROOF_Z),
           (ROOF_WIDTH/2,-ROOF_DEPTH/2,ROOF_Z),
           (ROOF_WIDTH/2,ROOF_DEPTH/2,ROOF_Z),
           (-ROOF_WIDTH/2,ROOF_DEPTH/2,ROOF_Z)],
          [(0,1,2,3)])
    return g.groups


if __name__=='__main__':
    geo=make_depot()
    print('groups:',len(geo),'vertices:',sum(len(p['vertices']) for p in geo.values()),
          'polygons:',sum(len(p['faces']) for p in geo.values()))
    for name,p in geo.items():print(name,len(p['vertices']),len(p['faces']))
