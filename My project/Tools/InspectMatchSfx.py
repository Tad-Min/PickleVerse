from pathlib import Path
import json
import numpy as np
import struct

def read_wave(path):
    raw=path.read_bytes();offset=12;fmt=None;audio=None
    while offset+8<=len(raw):
        tag=raw[offset:offset+4];size=struct.unpack_from('<I',raw,offset+4)[0];chunk=raw[offset+8:offset+8+size]
        if tag==b'fmt ':fmt=chunk
        if tag==b'data':audio=chunk
        offset+=8+size+(size%2)
    code,channels,rate,_,_,bits=struct.unpack_from('<HHIIHH',fmt)
    if code==65534:code=struct.unpack_from('<H',fmt,24)[0]
    if code==3:values=np.frombuffer(audio,dtype='<f4' if bits==32 else '<f8')
    elif bits==24:
        triplets=np.frombuffer(audio,dtype=np.uint8).reshape(-1,3).astype(np.int32)
        values=triplets[:,0]|triplets[:,1]<<8|triplets[:,2]<<16;values=(values<<8)>>8
        values=values.astype(np.float64)/8388608
    elif bits==8:values=(np.frombuffer(audio,dtype=np.uint8).astype(np.float64)-128)/128
    else:values=np.frombuffer(audio,dtype='<i2' if bits==16 else '<i4')
    return rate,values.reshape(-1,channels) if channels>1 else values

folder=Path(__file__).resolve().parents[1]/'Music'/'Sound EFX'
names=['smash-normal.wav','smash-serve.wav','smash-strong.wav','Ma_Sat_Giay.wav','foot-step.wav','Countdown.wav','Coi.wav','Coi_Large.wav']
results=[]
for name in names:
    path=folder/name
    rate,data=read_wave(path)
    floats=data.astype(np.float64)
    if np.issubdtype(data.dtype,np.integer): floats/=max(abs(np.iinfo(data.dtype).min),np.iinfo(data.dtype).max)
    amplitude=np.abs(floats) if floats.ndim==1 else np.abs(floats).max(axis=1)
    threshold=max(.001,float(amplitude.max())*.01)
    audible=np.flatnonzero(amplitude>threshold)
    peaks=[]
    for start in range(0,len(amplitude),max(1,rate//20)):
        peaks.append(round(float(amplitude[start:start+rate//20].max()),3))
    results.append(dict(name=name,bytes=path.stat().st_size,seconds=round(len(data)/rate,4),rate=rate,channels=1 if data.ndim==1 else data.shape[1],dtype=str(data.dtype),onset_ms=round(int(audible[0])/rate*1000,2) if len(audible) else None,last_ms=round(int(audible[-1])/rate*1000,2) if len(audible) else None,peak=round(float(amplitude.max()),4),peaks_50ms=peaks))
print(json.dumps(results,indent=2))
