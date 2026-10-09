from pathlib import Path
import json,struct,wave
import numpy as np

project=Path(__file__).resolve().parents[1]
source=project/'Music'/'Sound EFX'
destination=project/'Assets'/'Audio'/'SFX'
destination.mkdir(parents=True,exist_ok=True)

def read(path):
    raw=path.read_bytes();offset=12;fmt=audio=None
    while offset+8<=len(raw):
        tag=raw[offset:offset+4];size=struct.unpack_from('<I',raw,offset+4)[0];chunk=raw[offset+8:offset+8+size]
        if tag==b'fmt ':fmt=chunk
        if tag==b'data':audio=chunk
        offset+=8+size+(size%2)
    code,channels,rate,_,_,bits=struct.unpack_from('<HHIIHH',fmt)
    if code==65534:code=struct.unpack_from('<H',fmt,24)[0]
    if code==3:a=np.frombuffer(audio,dtype='<f4' if bits==32 else '<f8').astype(np.float64)
    elif bits==24:
        b=np.frombuffer(audio,dtype=np.uint8).reshape(-1,3).astype(np.int32);a=b[:,0]|b[:,1]<<8|b[:,2]<<16;a=((a<<8)>>8).astype(np.float64)/8388608
    elif bits==8:a=(np.frombuffer(audio,dtype=np.uint8).astype(np.float64)-128)/128
    else:a=np.frombuffer(audio,dtype='<i2' if bits==16 else '<i4').astype(np.float64)/(2**(bits-1))
    if channels>1:a=a.reshape(-1,channels).mean(axis=1)
    return rate,a

names=['smash-normal.wav','smash-serve.wav','smash-strong.wav','Ma_Sat_Giay.wav','foot-step.wav','Countdown.wav','Coi.wav','Coi_Large.wav']
report=[]
for name in names:
    rate,samples=read(source/name);original=len(samples)
    # Short transient assets include preparation/noise before their main impact.
    threshold=float(np.abs(samples).max())*(.12 if name in names[:5] else .01)
    onset=int(np.flatnonzero(np.abs(samples)>threshold)[0])
    start=max(0,onset-int(rate*.003))
    end=int(rate*.92) if name=='Countdown.wav' else len(samples)
    if name in ('Coi.wav','Coi_Large.wav'):
        last=int(np.flatnonzero(np.abs(samples)>float(np.abs(samples).max())*.002)[-1]);end=min(end,last+int(rate*.07))
    samples=samples[start:end].copy()
    fade=min(len(samples)//2,int(rate*.002));samples[:fade]*=np.linspace(0,1,fade);samples[-fade:]*=np.linspace(1,0,fade)
    pcm=(np.clip(samples,-1,1)*32767).astype('<i2')
    with wave.open(str(destination/name),'wb') as output:
        output.setnchannels(1);output.setsampwidth(2);output.setframerate(rate);output.writeframes(pcm.tobytes())
    report.append(dict(file=name,original_seconds=round(original/rate,4),trimmed_start_ms=round(start/rate*1000,2),game_seconds=round(len(pcm)/rate,4),rate=rate,channels=1,pcm_bytes=len(pcm)*2))
report_path=project.parent/'UnityWork'/'SFX';report_path.mkdir(parents=True,exist_ok=True)
(report_path/'preparation.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print(json.dumps(report,indent=2))
