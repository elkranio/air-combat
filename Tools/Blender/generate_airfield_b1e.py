#!/usr/bin/env python3
"""B1e hand-authored faceted hangar + tower. Re-run from project root.
The meshes use metres, Y up, X width/depth depending on building, Z long axis.
No AO or baked light: all lighting in Unity is dynamic.
"""
from pathlib import Path
from math import sqrt

OUT=Path('Assets/_Project/Art/Buildings/Base/Airfield_B1e')
OUT.mkdir(parents=True,exist_ok=True)

class Model:
    def __init__(self,name):self.name=name;self.v=[];self.faces=[]
    def face(self,*pts):
        n=len(self.v)
        self.v.extend(pts)
        self.faces.append(tuple(range(n+1,n+len(pts)+1)))
    def box(self,x0,y0,z0,x1,y1,z1):
        # Outward normals, no interior shading required.
        a=(x0,y0,z0);b=(x1,y0,z0);c=(x1,y1,z0);d=(x0,y1,z0)
        e=(x0,y0,z1);f=(x1,y0,z1);g=(x1,y1,z1);h=(x0,y1,z1)
        self.face(d,c,b,a);self.face(e,f,g,h)
        self.face(a,b,f,e);self.face(h,g,c,d)
        self.face(e,h,d,a);self.face(b,c,g,f)
    def prism(self,profile,z0,z1):
        # Correct outward winding for both generated CW/CCW profiles.
        area=sum(profile[i][0]*profile[(i+1)%len(profile)][1]-profile[(i+1)%len(profile)][0]*profile[i][1] for i in range(len(profile)))
        if area < 0: profile=list(reversed(profile))
        self.face(*[(x,y,z0) for x,y in profile][::-1])
        self.face(*[(x,y,z1) for x,y in profile])
        for i in range(len(profile)):
            x0,y0=profile[i];x1,y1=profile[(i+1)%len(profile)]
            self.face((x0,y0,z0),(x1,y1,z0),(x1,y1,z1),(x0,y0,z1))
    def beam(self,a,b,r):
        # Slanted rectangular strut between two points (XZ diagonal, Y vertical).
        ax,ay,az=a;bx,by,bz=b
        self.box(min(ax,bx)-r,min(ay,by),min(az,bz)-r,max(ax,bx)+r,max(ay,by),max(az,bz)+r)
    def save(self):
        p=OUT/(self.name+'.obj')
        lines=[f'# B1e {self.name}: author source Tools/Blender/generate_airfield_b1e.py',f'o {self.name}', 's off']
        lines.extend('v %.5f %.5f %.5f'%v for v in self.v)
        lines.extend('f '+' '.join(map(str,f)) for f in self.faces)
        p.write_text('\n'.join(lines)+'\n',encoding='utf-8')
        print(f'{p}  {len(self.faces)} faces')

# Hangar 36m deep (X) x 48m wide (Z) x 13m tall.
# Aircraft enters from the -X frontage, between reinforced side piers.
w=Model('Hangar_Walls')
w.box(-17.0,0,-24,-13.4,8.7,-18.5)   # entrance left pier
w.box(-17.0,0,18.5,-13.4,8.7,24)   # entrance right pier
w.box(-17.0,7.5,-18.5,-13.4,8.7,18.5) # overhead beam
w.box(-17.0,0,-24,17,8.7,-22.8)    # left side wall
w.box(-17.0,0,22.8,17,8.7,24)      # right side wall
w.box(15.8,0,-24,17,8.7,24)        # rear wall
w.prism([(-17,8.7),(0,13.35),(17,8.7)],-24,-22.8)
w.prism([(-17,8.7),(0,13.35),(17,8.7)],22.8,24)
w.save()

roof=Model('Hangar_Roof')
# Strong, thick two-facet roof, visible at 50 degree elevated camera.
roof.prism([(-19,8.58),(-19,9.2),(-.25,14),(0,14),(.25,14),(19,9.2),(19,8.58),(0,13.30)],-25.4,25.4)
# deliberately chunky, clean ridge
roof.box(-.42,13.9,-25.4,.42,14.35,25.4)
roof.save()

dark=Model('Hangar_Openings')
# Dark deep interior and rear ventilation elements: not painted fake door slots.
dark.box(-13.2,.05,-19.2,15.6,.11,19.2)  # dark concrete inner floor
# Exterior dark louvres on back/right wall, only big legible details.
for zz in [-13,0,13]:
    dark.box(17.02,3.8,zz-2.2,17.16,6.2,zz+2.2)
dark.save()

trim=Model('Hangar_Trim')
# Thick coloured entrance portal, high contrast without signage noise.
trim.box(-17.8,0,-19.15,-16.9,8.0,-18.15)
trim.box(-17.8,0,18.15,-16.9,8.0,19.15)
trim.box(-18.2,7.82,-19.15,-16.5,8.56,19.15)
# roof ventilators: 2 clear roof boxes, no tiny pipe spam.
trim.box(-3.7,13.5,-12,-.4,14.9,-7)
trim.box(-3.7,13.5,7,-.4,14.9,12)
trim.save()

# Tower: ~22m high, noticeably thinner than HQ and hangar.
tbase=Model('Tower_Structure')
for sx in [-1,1]:
    for sz in [-1,1]:
        # slightly tapered 4-leg structure, use continuous upright beams.
        tbase.prism([ (sx*5.5-0.52,0),(sx*5.5+0.52,0),(sx*3.75+0.52,16.3),(sx*3.75-0.52,16.3) ],sz*5.5-.52,sz*5.5+.52)
# cross-decks + transverse horizontal ties
tbase.box(-5.8,6.7,-5.8,5.8,7.1,5.8)
tbase.box(-4.4,15.6,-4.4,4.4,16.1,4.4)
tbase.box(-6.0,16.05,-6.0,6.0,16.55,6.0)
tbase.save()

tcab=Model('Tower_Cabin')
tcab.box(-5.5,16.5,-5.5,5.5,21.6,5.5)
tcab.box(-6.45,21.5,-6.45,6.45,22.25,6.45)
tcab.save()

tglass=Model('Tower_Glass')
# Big dark window band on 4 sides, non-transparent and matte arcade look.
tglass.box(-5.58,18.05,-5.58,5.58,20.75,-5.39)
tglass.box(-5.58,18.05,5.39,5.58,20.75,5.58)
tglass.box(-5.58,18.05,-5.39,-5.39,20.75,5.39)
tglass.box(5.39,18.05,-5.39,5.58,20.75,5.39)
tglass.save()

for model in ('Hangar_Walls','Hangar_Roof','Hangar_Openings','Hangar_Trim','Tower_Structure','Tower_Cabin','Tower_Glass'):
    assert (OUT/(model+'.obj')).is_file()
