"""CL06: reusable solid stone library and Mission01 camera-layout v2 recipe.
Run Python to export OBJ + layout JSON; Blender background to also save source.
The approved CL05 solid builder is used at individual rock scale, never to
stretch a whole mountain. Recipe changes do not require geometry changes.
"""
from pathlib import Path
from math import cos, sin, radians, atan2, degrees, hypot, ceil
import json
import sys
# Blender --python does not necessarily add the script directory to sys.path.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_cl05_test_rock import SPECS, build, norm, PALETTE

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/_Project/Art/Environment/Mission01_FirstSortie/CL06_RockBarrier'
# Template, width/depth multipliers, height, whole-rock tone, lean, cap slope.
SHAPES=[
 ('CL06_A_Bastion',0,1.65,1.70,132,0,(-5,7),(.020,-.030)),
 ('CL06_B_Pillar',1,1.95,1.75,145,1,(6,-4),(-.045,.025)),
 ('CL06_C_Fracture',2,1.75,1.80,119,0,(-7,5),(.035,.050)),
 ('CL06_D_Buttress',3,1.48,1.95,104,1,(4,7),(.055,-.035)),
 ('CL06_E_Crown',0,1.35,1.50,153,0,(6,-3),(-.03,.055)),
 ('CL06_F_Shoulder',4,1.95,1.95,92,2,(-6,4),(-.06,.085)),
 ('CL06_G_Boulder',5,1.65,1.55,46,1,(8,6),(.13,.12)),
 ('CL06_H_Slab',6,1.75,1.80,34,2,(-8,10),(-.12,.14)),
]
# Centerline from approved H2C positions, with extension west of edge.
SPINE=[(-1060,-485),(-968,-395),(-680,-106),(-418,165),(-143,449),(137,729),(404,1004),(638,1203)]
MAIN_ROUTE=[(-355,-738),(-151,-707),(34,-540),(106,-260),(232,-52),(390,79),(665,155),(914,250),(1157,503),(1243,778),(1338,954),(1460,1058),(1582,1090)]
EXTRA_ROUTE=[(1582,1090),(1573,1342),(1356,1532),(1117,1672),(846,1771),(580,1776),(282,1631),(-66,1464),(-350,1270),(-585,1017),(-698,814)]

def models():
    result={}
    for name,t,wx,wz,h,tone,lean,slope in SHAPES:
        src=SPECS[t]; outline=[(x*wx,z*wz) for x,z in src[-2]]
        spec=(name,(0,0),h,tone,lean,slope,outline,src[-1])
        result[name]=build(spec)
    return result

def recipe():
    placements=[]
    def add(section,kind,x,z,yaw,sx=1,sy=1,sz=1):
        placements.append(dict(section=section,model=SHAPES[kind][0],x=round(x,3),z=round(z,3),yaw=round(yaw,3),sx=sx,sy=sy,sz=sz))
    # Sample the spine by distance, not per-segment reset. The main overlapping
    # solids are compact; staggered shoulders create local steps on either side.
    lengths=[hypot(b[0]-a[0],b[1]-a[1]) for a,b in zip(SPINE,SPINE[1:])]
    total=sum(lengths)
    def at(d):
        d=max(0,min(total,d))
        for i,ell in enumerate(lengths):
            if d<=ell or i==len(lengths)-1:
                a,b=SPINE[i:i+2]; u=(b[0]-a[0])/ell; v=(b[1]-a[1])/ell
                return a[0]+u*d,a[1]+v*d,u,v,i
            d-=ell
    n=ceil(total/87)
    for i in range(n+1):
        x,z,u,v,seg=at(total*i/n); yaw=-degrees(atan2(v,u)); side=[-9,11,-3,14,-11,5][i%6]
        kind=[0,2,1,0,4,2,3,1,0][i%9]
        if i==23: x-=3; z-=1  # close a tiny internal seam at cruise height
        # D is still >Y78 at its lower cap. Slight offset / yaw breaks the facade.
        add(f'{seg+1:02d} Main ridge',kind,x-v*side,z+u*side,yaw+[-9,12,-18,5,17,-6,3][i%7], [1.10,1.06,1.18,1.12][i%4],[1,.97,1.04,.95,1.02][i%5],[1.13,1.05,1.15][i%3])
    # Continuous flanks: keep each compact block close to an authored smooth
    # edge, overlap longitudinally, and vary the height rather than leaving
    # missing stones. The two sides have distinct rhythms.
    n2=ceil(total/65)
    for side in (-1,1):
        for i in range(n2+1):
            d=total*max(0,min(1,(i+(0.28 if side>0 else 0))/n2))
            x,z,u,v,seg=at(d); yaw=-degrees(atan2(v,u))
            # A broad gentle bend, small local offsets; taper only at the end.
            envelope=94+13*sin(d/270+side*.7)+6*sin(d/135+side)
            taper=max(.48,min(1,(total-d+75)/260))
            off=side*envelope*taper
            k=[3,2,0,3,1,2,3,0][(i+(3 if side>0 else 0))%8]
            add(f'{seg+1:02d} Main ridge',k,x-v*off,z+u*off,
                yaw+side*[8,-12,17,-5,13,-16][i%6],
                [1.03,.97,1.05,1.0][i%4], [.96,.89,.84,1.02,.9][i%5],
                [1.02,.98,1.08,.96][(i+(1 if side>0 else 0))%4])
        # Low isolated slabs close to the edge, no new tall false passages.
        for i in range(3, n2-2, 8):
            d=total*i/n2; x,z,u,v,seg=at(d); yaw=-degrees(atan2(v,u))
            off=side*(148+10*sin(d/270+side*.7))
            add(f'{seg+1:02d} Main ridge',6+(i+int(side>0))%2,
                x-v*off,z+u*off,yaw+side*27,.88,1,.93)
    # Lower spur follows the old CL02 region. Close the south edge at Y78.
    spur=[(1100,-1115),(1145,-1060),(1190,-1005),(1235,-950),
          (1280,-895),(1320,-840),(1360,-790),(1390,-750)]
    for i,(x,z) in enumerate(spur):
        add('08 Lower spur',[1,0,2,4,0][i%5],x,z,-43+[-8,6,-12,4,17][i%5],1.13,1,1.10)
        for side in (-1,1):
            off=side*(44 if i>=6 else 63)
            add('08 Lower spur',[3,2,0,3,2][(i+int(side>0))%5],
                x-off,z+off,-43+side*15,1.02,.94,1.03)
    add('08 Lower spur',6,1462,-812,23,1.1,1,1)
    return placements

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    library=models()
    for name,(v,f,tone) in library.items():
        lines=['# CL06 solid rock; Unity Y-up; generated by build_cl06_rock_barrier.py',f'o {name}','s off']
        lines+=['v %.6f %.6f %.6f'%p for p in v]; lines+=['vn %.8f %.8f %.8f'%norm(v,face) for face in f]
        lines+=['f '+' '.join(f'{j+1}//{i}' for j in face) for i,face in enumerate(f,1)]
        (OUT/f'{name}.obj').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    data={'models':[dict(name=name,tone=tone) for name,(_,_,tone) in library.items()], 'placements':recipe()}
    (OUT/'Mission01_CL06_Layout.json').write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
    print(len(library),'solid meshes;',len(data['placements']),'placements')
    try: import bpy
    except ImportError: return
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    mats=[]
    def linear(c):
        s=c/255; return s/12.92 if s<=.04045 else ((s+.055)/1.055)**2.4
    for i,rgb in enumerate(PALETTE):
        m=bpy.data.materials.new(f'CL05_Stone_{i}');m.diffuse_color=tuple(linear(c) for c in rgb)+(1,);mats.append(m)
    for i,(name,(v,f,tone)) in enumerate(library.items()):
        mesh=bpy.data.meshes.new(name);mesh.from_pydata([(x,-z,y) for x,y,z in v],[],f);mesh.update();mesh.materials.append(mats[tone]);obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);obj.location=((i%4)*220,(i//4)*220,0)
    dest=ROOT/'Resources/Blender/CL06_RockLibrary.blend';dest.parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(dest))

if __name__=='__main__': main()
