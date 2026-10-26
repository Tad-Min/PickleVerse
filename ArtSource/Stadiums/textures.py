"""Deterministic small tiling maps and a single Vietnamese sign atlas. No downloads."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import numpy as np, json

ROOT=Path(r'D:\Github\PickleVerse')
DEST=ROOT/'My project'/'Assets'/'LocalMatchContent'/'Stadiums'/'RefinedTextures'
DEST.mkdir(parents=True,exist_ok=True)
rng=np.random.default_rng(62025)

def noise(n,amount,base):
    fine=rng.normal(0,amount,(n,n))
    coarse=np.array(Image.fromarray(rng.integers(0,255,(16,16),dtype=np.uint8)).resize((n,n),Image.Resampling.BICUBIC),dtype=float)/255-.5
    value=np.clip(base+fine+coarse*amount*1.5,0,255).astype(np.uint8)
    return np.stack([value]*3,axis=-1)

micro=noise(256,2.0,242)
Image.fromarray(micro).save(DEST/'Venue_Micrograin.png',optimize=True)
conc=noise(256,3.5,225)
for i in range(0,256,64):
    conc[i:i+1,:,:]=198;conc[:,i:i+1,:]=198
Image.fromarray(conc).save(DEST/'Venue_Paving.png',optimize=True)
grass=noise(256,6,182).astype(float)
grass*=np.array([.88,1,.73]);grass=np.clip(grass,0,255).astype(np.uint8)
im=Image.fromarray(grass);d=ImageDraw.Draw(im)
for _ in range(1800):
    x,y=rng.integers(0,256,2);color=(int(rng.integers(125,167)),int(rng.integers(161,194)),int(rng.integers(105,143)))
    d.line((int(x),int(y),int(x+1),int(y-3)),fill=color,width=1)
im.save(DEST/'Venue_Lawn.png',optimize=True)
bark=noise(128,5,183)
for x in range(0,128,13):bark[:,x:x+2,:]-=24
Image.fromarray(bark).save(DEST/'Venue_Bark.png',optimize=True)

# One opaque atlas avoids transparent text planes and sorting/overdraw on phones.
atlas=Image.new('RGB',(1024,512),(18,38,48));d=ImageDraw.Draw(atlas)
fontfile=ROOT/'My project'/'Assets'/'Fonts'/'ArialBold.ttf'
labels=['PICKLEVERSE','VÀO SÂN. TỎA SÁNG.','HẾT MÌNH TỪNG PHA BÓNG','CHƠI ĐẸP • CHƠI HẾT MÌNH']
for row,label in enumerate(labels):
    y=row*128
    d.rectangle((0,y,1023,y+127),fill=(20+row*2,42+row*2,52+row*2))
    d.line((16,y+15,1008,y+15),fill=(116,168,158),width=2)
    d.line((16,y+112,1008,y+112),fill=(72,113,115),width=2)
    d.rectangle((20,y+36,28,y+92),fill=(219,196,143))
    size=100 if row==0 else 62
    font=ImageFont.truetype(str(fontfile),size)
    while d.textbbox((0,0),label,font=font)[2]>870:
        size-=1;font=ImageFont.truetype(str(fontfile),size)
    d.text((512,y+65),label,font=font,fill=(231,231,213),anchor='mm',stroke_width=0)
    for side in [52,972]:
        d.polygon([(side,y+51),(side+10,y+64),(side,y+77),(side-10,y+64)],outline=(219,196,143))
atlas.save(DEST/'Venue_Signage.png',optimize=True)
manifest={'signs':labels,'maps':[{ 'name':p.name,'bytes':p.stat().st_size,'size':list(Image.open(p).size)} for p in DEST.glob('*.png')]}
(ROOT/'BlenderWork'/'VenueRefinement20261025'/'texture-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(manifest,ensure_ascii=True))
