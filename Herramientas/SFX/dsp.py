import numpy as np
from scipy import signal
from scipy.io import wavfile
SR=48000
rng=np.random.default_rng(7)

def t_(d): return np.arange(int(d*SR))/SR
def noise(d, seed=None):
    r=np.random.default_rng(seed) if seed is not None else rng
    return r.standard_normal(int(d*SR))
def env_adsr(n, a, d, s, r, sustain_level=0.7):
    A=int(a*SR); D=int(d*SR); R=int(r*SR); S=max(0,n-A-D-R)
    e=np.concatenate([np.linspace(0,1,A,endpoint=False)**2 if A else [],
                      np.linspace(1,sustain_level,D,endpoint=False) if D else [],
                      np.full(S,sustain_level),
                      np.linspace(sustain_level,0,R)**1.5 if R else []])
    return np.pad(e,(0,max(0,n-len(e))))[:n]
def expdec(n, tau, attack=0.002):
    t=np.arange(n)/SR
    e=np.exp(-t/tau)
    A=int(attack*SR)
    if A>0: e[:A]*=np.linspace(0,1,A)
    return e
def lp(x, f, order=4):
    b,a=signal.butter(order, min(f, SR/2*0.99)/(SR/2), 'low'); return signal.lfilter(b,a,x)
def hp(x, f, order=4):
    b,a=signal.butter(order, f/(SR/2), 'high'); return signal.lfilter(b,a,x)
def bp(x, f1, f2, order=3):
    b,a=signal.butter(order, [f1/(SR/2), min(f2,SR/2*0.99)/(SR/2)], 'band'); return signal.lfilter(b,a,x)
def sweep_filter(x, f0, f1, kind='low', q=0.7, block=256, curve=1.0):
    """Filtro variable en el tiempo (por bloques, con estado)."""
    y=np.zeros_like(x); n=len(x); zi=None
    for i in range(0,n,block):
        u=(i/n)**curve; f=f0*(f1/f0)**u
        b,a=signal.butter(2, min(f,SR/2*0.98)/(SR/2), kind)
        if zi is None: zi=signal.lfilter_zi(b,a)*x[0]
        y[i:i+block],zi=signal.lfilter(b,a,x[i:i+block],zi=zi)
    return y
def osc(freq_fn, d, shape='sin', phase=0.0):
    n=int(d*SR); t=np.arange(n)/SR
    f=freq_fn(t) if callable(freq_fn) else np.full(n,freq_fn)
    ph=2*np.pi*np.cumsum(f)/SR+phase
    if shape=='sin': return np.sin(ph)
    if shape=='saw': return 2*((ph/(2*np.pi))%1)-1
    if shape=='tri': return 2*np.abs(2*((ph/(2*np.pi))%1)-1)-1
    if shape=='sq': return np.sign(np.sin(ph))
def fm_bell(f, d, ratio=3.5, index=4.0, tau=1.2):
    n=int(d*SR); t=np.arange(n)/SR
    mod=index*np.exp(-t/(tau*0.5))*np.sin(2*np.pi*f*ratio*t)
    return np.sin(2*np.pi*f*t+mod)*np.exp(-t/tau)
def crackle(d, density=60, seed=None, decay=None):
    r=np.random.default_rng(seed)
    n=int(d*SR); x=np.zeros(n)
    k=int(density*d)
    pos=r.integers(0,n,k); amp=r.random(k)**3
    x[pos]=amp*r.choice([-1,1],k)
    x=hp(x,1500,2)
    # cada chasquido con su pequeña cola
    x=signal.fftconvolve(x, np.exp(-np.arange(int(0.004*SR))/(0.0008*SR)))[:n]
    if decay: x*=np.exp(-np.arange(n)/SR/decay)
    return x
def stereo(x, width=0.3, seed=1):
    """Decorrela un mono en estéreo con dos all-pass distintos."""
    if x.ndim==2: return x
    l=x.copy(); r=x.copy()
    for k,(d1,d2) in enumerate([(0.0031,0.0047),(0.0071,0.0053)]):
        g=0.5
        for ch,dd in ((0,d1),(1,d2)):
            D=int(dd*SR); b=np.zeros(D+1); b[0]=-g; b[D]=1; a=np.zeros(D+1); a[0]=1; a[D]=-g
            if ch==0: l=signal.lfilter(b,a,l)
            else: r=signal.lfilter(b,a,r)
    m=(l+r)/2; s=(l-r)/2
    return np.stack([x*(1-width)+l*width, x*(1-width)+r*width],1)
def pan(x, p):
    """x mono -> estéreo; p en [-1,1]"""
    a=(p+1)*np.pi/4
    return np.stack([x*np.cos(a), x*np.sin(a)],1)
def to_st(x):
    return x if x.ndim==2 else np.stack([x,x],1)
def reverb_ir(d=3.0, predelay=0.02, damp_start=9000, damp_end=1500, seed=11, early=True):
    n=int(d*SR); r=np.random.default_rng(seed)
    out=[]
    for ch in range(2):
        x=r.standard_normal(n)*np.exp(-np.arange(n)/SR/(d/6.9))
        # amortiguación: filtro que se cierra con el tiempo
        x=sweep_filter(x, damp_start, damp_end, 'low', block=512, curve=0.5)
        if early:
            for k in range(8):
                p=int((0.008+0.06*r.random())*SR); x[p]+= (0.6-0.06*k)*r.choice([-1,1])
        x=np.concatenate([np.zeros(int(predelay*SR)), x])
        out.append(x)
    ir=np.stack(out,1); ir/=np.sqrt((ir**2).sum(0)).max()
    return ir
def reverb(x, wet=0.35, d=3.0, **kw):
    x=to_st(x); ir=reverb_ir(d, **kw)
    w=np.stack([signal.fftconvolve(x[:,c], ir[:,c]) for c in range(2)],1)
    y=np.zeros((len(w),2)); y[:len(x)]+=x*(1-wet)
    y+=w*wet*1.0
    return y
def sat(x, drive=2.0):
    return np.tanh(x*drive)/np.tanh(drive)
def mix(*parts):
    n=max(len(p[1])+int(p[0]*SR) for p in parts)
    y=np.zeros((n,2))
    for off,x,g in parts:
        x=to_st(x); o=int(off*SR); y[o:o+len(x)]+=x*g
    return y
def fade(y, fin=0.003, fout=0.2):
    n=len(y); a=int(fin*SR); b=int(fout*SR)
    e=np.ones(n)
    if a: e[:a]=np.linspace(0,1,a)
    if b: e[-b:]=np.linspace(1,0,b)**2
    return y*e[:,None] if y.ndim==2 else y*e
def trim_tail(y, thr_db=-60):
    a=np.abs(y).max(1) if y.ndim==2 else np.abs(y)
    thr=a.max()*10**(thr_db/20)
    idx=np.where(a>thr)[0]
    return y[:idx[-1]+int(0.05*SR)] if len(idx) else y
def normalize(y, peak_db=-1.0):
    return y/np.abs(y).max()*10**(peak_db/20)
def write(path, y, peak_db=-1.0):
    y=fade(trim_tail(y), 0.002, 0.15); y=normalize(y, peak_db)
    wavfile.write(path, SR, (np.clip(y,-1,1)*32767).astype(np.int16))
def load(path):
    import subprocess, io
    raw=subprocess.run(['ffmpeg','-v','error','-i',path,'-f','f32le','-ac','2','-ar',str(SR),'-'],capture_output=True).stdout
    return np.frombuffer(raw,np.float32).reshape(-1,2).astype(np.float64)

def flanger(x, rate=0.4, dmin=0.0008, dmax=0.006, fb=0.0, mix_=0.6, phase=0.0):
    n=len(x); t=np.arange(n)/SR
    d=(dmin+(dmax-dmin)*(0.5+0.5*np.sin(2*np.pi*rate*t+phase)))*SR
    idx=np.arange(n)-d; i0=np.floor(idx).astype(int); fr=idx-i0
    i0c=np.clip(i0,0,n-1); i1c=np.clip(i0+1,0,n-1)
    dl=x[i0c]*(1-fr)+x[i1c]*fr; dl[i0<0]=0
    return x*(1-mix_)+dl*mix_
def am(x, freq_fn, depth=0.6):
    n=len(x); t=np.arange(n)/SR
    f=freq_fn(t) if callable(freq_fn) else np.full(n,freq_fn)
    ph=2*np.pi*np.cumsum(f)/SR
    return x*(1-depth*(0.5+0.5*np.sin(ph)))
def eco(y, tiempo=0.32, fb=0.35, n=4, damp=3000):
    y=to_st(y); out=y.copy(); d=int(tiempo*SR)
    out=np.vstack([out,np.zeros((d*n,2))]); cur=y
    for k in range(1,n+1):
        cur=np.stack([lp(cur[:,0],damp),lp(cur[:,1],damp)],1)*fb
        out[d*k:d*k+len(cur)]+=cur[:, ::-1] if k%2 else cur
    return out
