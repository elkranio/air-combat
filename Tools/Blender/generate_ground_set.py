"""Five original repeatable stylized ground tiles. No bpy or external art required.
Run with Blender's bundled NumPy: blender -b --python-exit-code 1 --python Tools/Blender/generate_ground_set.py
"""
from pathlib import Path
import struct
import zlib
import numpy as np

OUT = Path(__file__).resolve().parents[2] / 'Assets' / '_Project' / 'Art' / 'Terrain' / 'GroundSet'
SIZE = 512
Y, X = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32)


def rgb(s):
    return np.array([int(s[i:i+2],16) for i in (0,2,4)], dtype=np.float32)


def smooth(a, b, x):
    t = np.clip((x-a)/(b-a),0,1)
    return t*t*(3-2*t)


def noise(rng, cells):
    # Periodic value lattice: no independent edges, no seam correction needed.
    grid = rng.random((cells,cells)).astype(np.float32)
    x, y = X*cells/SIZE, Y*cells/SIZE
    ix, iy = x.astype(int), y.astype(int)
    fx, fy = smooth(0,1,x-ix), smooth(0,1,y-iy)
    a = grid[iy,ix]*(1-fx)+grid[iy,(ix+1)%cells]*fx
    b = grid[(iy+1)%cells,ix]*(1-fx)+grid[(iy+1)%cells,(ix+1)%cells]*fx
    return a*(1-fy)+b*fy


def paint(im, color, weight):
    w = np.clip(weight,0,1)[...,None]
    im[:] = im*(1-w)+rgb(color)*w


def stamp(im, x, y, rx, ry, angle, color, opacity, shape=0):
    # Only evaluate the local footprint; wrap indices on both axes.
    r = int(max(rx,ry)*1.6)+3
    a = np.arange(-r,r+1)
    xx, yy = np.meshgrid(a-(x-int(x)),a-(y-int(y)))
    c,s = np.cos(angle),np.sin(angle)
    u=(c*xx+s*yy)/rx; v=(-s*xx+c*yy)/ry
    if shape == 1:  # broad tapered brush mark
        dist=np.abs(u)+np.abs(v)*.82
    elif shape == 2:  # irregular flat pebble
        dist=np.maximum(np.abs(u)*.78+np.abs(v)*.50,np.abs(v))
    else:
        dist=np.sqrt(u*u+v*v)
    alpha=(1-smooth(.68,1,dist))*opacity
    iy=(int(y)+a)%SIZE; ix=(int(x)+a)%SIZE
    block=im[np.ix_(iy,ix)].copy()
    paint(block,color,alpha)
    im[np.ix_(iy,ix)]=block


# (name, base, dark, light, warm, dark accents, light accents, kind, seed)
SPECS = [
    ('GrassBase','638447','53743D','77964E','84934C','4D703D','91A45A','grass',151),
    ('GrassDry','89994F','778645','A0AA62','B0A86A','6E8243','B5B46E','grass',253),
    ('Dirt','9D8B61','89774F','AD9A73','B3A078','7F7052','C1AF85','dirt',357),
    ('ShoreSand','B5A379','A2936A','C4B58D','C8B586','938866','D4C7A1','sand',459),
    ('AirbaseGround','A49C83','938B73','B1AA93','B6A88D','837D69','C1B9A3','airbase',561),
]


def tile(spec):
    name,base,dark,light,warm,ink,glint,kind,seed=spec
    rng=np.random.default_rng(seed)
    # A few broad brush-shaped tones, secondary smaller patches, very little grain.
    large=noise(rng,5); medium=noise(rng,13); fine=noise(rng,43)
    signal=.72*large+.28*medium
    im=np.broadcast_to(rgb(base),(SIZE,SIZE,3)).copy()
    paint(im,dark,smooth(.62,.86,1-signal)*.72)
    paint(im,light,smooth(.54,.78,signal)*.82)
    paint(im,warm,smooth(.65,.88,medium)*.35)
    im+=(fine-.5)[...,None]*2.8
    if kind == 'grass':
        # Loose grass tufts, not a lawn of thousands of tiny blades. Clustered.
        for _ in range(85):
            x,y=rng.uniform(0,SIZE,2)
            if large[int(y),int(x)]<.24: continue
            size=rng.uniform(4,9); angle=rng.uniform(-1,1)
            stamp(im,x,y,size*1.6,size*.58,angle,ink,.24,0)
            for j in range(3):
                dx=(j-1)*size*.58
                stamp(im,x+dx,y-rng.uniform(1,4),rng.uniform(1.8,3.1),rng.uniform(4,8),angle+(j-1)*.44,glint if j==1 else ink,rng.uniform(.26,.49),1)
        for _ in range(18):
            x,y=rng.uniform(0,SIZE,2)
            stamp(im,x,y,rng.uniform(9,21),rng.uniform(4,10),rng.uniform(0,6.28),warm,.35,0)
    else:
        count={'dirt':64,'sand':38,'airbase':37}[kind]
        for _ in range(count):
            x,y=rng.uniform(0,SIZE,2); a=rng.uniform(0,6.28)
            r=rng.uniform(2.8,6.8) if kind!='airbase' else rng.uniform(2,4.5)
            stamp(im,x,y,r*1.25,r*.74,a,ink,.36,2)
            stamp(im,x-.9,y-1.2,r*.76,r*.42,a,glint,.37,2)
        for _ in range(35):
            x,y=rng.uniform(0,SIZE,2)
            stamp(im,x,y,rng.uniform(7,19),rng.uniform(2,5),rng.uniform(-.7,.7),warm,.29,1)
    return np.rint(np.clip(im,0,255)).astype(np.uint8)


def png(path, pixels):
    def chunk(kind,data):
        return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
    raw=b''.join(b'\0'+row.tobytes() for row in pixels)
    header=struct.pack('>IIBBBBB',SIZE,SIZE,8,2,0,0,0)
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',header)+chunk(b'sRGB',b'\0')+chunk(b'IDAT',zlib.compress(raw,6))+chunk(b'IEND',b''))


if __name__ == '__main__':
    OUT.mkdir(parents=True,exist_ok=True)
    for spec in SPECS:
        pixels=tile(spec)
        png(OUT/(spec[0]+'.png'),pixels)
        print(f'{spec[0]}: {SIZE}x{SIZE}, periodic RGB tile',flush=True)
    print('GROUND SET READY: '+str(OUT),flush=True)
