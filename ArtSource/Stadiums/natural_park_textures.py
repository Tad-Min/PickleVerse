"""Small original botanical maps: no downloaded images or alpha-card overdraw."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import numpy as np,json

ROOT=Path(r'D:\Github\PickleVerse')
DEST=ROOT/'My project'/'Assets'/'EnvironmentContent'/'NaturalPark'/'Textures'
DEST.mkdir(parents=True,exist_ok=True)
rng=np.random.default_rng(19477)
n=512;y,x=np.mgrid[0:n,0:n];u=x/(n-1);v=y/(n-1)
noise=rng.normal(0,1.5,(n,n))
# Longitudinal fibres, irregular fissures and pale lichen on a grey-brown trunk.
fibres=np.sin(u*135+np.sin(v*18)*1.3)*3+np.sin(u*355+v*8)*1.8
cracks=np.maximum(0,np.sin(u*62+np.sin(v*10)*.7)-.83)*70
coarse=np.array(Image.fromarray(rng.integers(0,255,(24,24),dtype=np.uint8)).resize((n,n),Image.Resampling.BICUBIC),float)/255-.5
bark=np.stack([104+fibres-cracks+coarse*21+noise,99+fibres-cracks+coarse*19+noise,84+fibres-cracks+coarse*17+noise],axis=-1)
Image.fromarray(np.uint8(np.clip(bark,0,255))).save(DEST/'Bark_Fibres.png',optimize=True)

# Leaf veins follow the midrib and branch toward the margin; geometry supplies
# the real outline, curl and ridge instead of a transparent rectangle.
rib=np.exp(-((u-.5)/.014)**2)
vein=np.zeros((n,n));wav=np.sin(v*24+u*3)*.002
for t in np.linspace(.12,.88,11):
    line=t+abs(u-.5)*.50+wav
    vein=np.maximum(vein,np.exp(-((v-line)/.0045)**2))
rim=np.clip(1-abs(u-.5)*1.4,.22,1)
grain=np.sin(u*226+np.sin(v*80))*.8+noise*.6
leaf=np.stack([47+rim*14+rib*11+vein*7+grain,69+rim*29+rib*17+vein*9+grain,28+rim*12+rib*6+vein*4+grain],axis=-1)
Image.fromarray(np.uint8(np.clip(leaf,0,255))).save(DEST/'Leaf_Venation.png',optimize=True)

n=256;y,x=np.mgrid[0:n,0:n];u=x/(n-1);v=y/(n-1)
fibre=np.sin(u*150+v*7)*2+np.cos(u*310+v*4)
root=np.clip(v*1.5,0,1);tip=np.clip((v-.7)*2,0,1)
grass=np.stack([35+root*22+tip*8+fibre,57+root*36+tip*8+fibre,21+root*12+fibre*.6],axis=-1)
Image.fromarray(np.uint8(np.clip(grass,0,255))).save(DEST/'Grass_Fibres.png',optimize=True)

# Ground remains between individual blades; avoid a flat saturated green base.
n=256;fine=rng.normal(0,3,(n,n))
wide=np.array(Image.fromarray(rng.integers(0,255,(32,32),dtype=np.uint8)).resize((n,n),Image.Resampling.BICUBIC),float)/255-.5
ground=np.stack([66+fine+wide*17,76+fine+wide*21,47+fine*.6+wide*12],axis=-1)
Image.fromarray(np.uint8(np.clip(ground,0,255))).save(DEST/'Ground_Organic.png',optimize=True)
print(json.dumps([{'name':p.name,'bytes':p.stat().st_size} for p in DEST.glob('*.png')]))
