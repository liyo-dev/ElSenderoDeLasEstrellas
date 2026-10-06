import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt, fftconvolve
SR=48000; DUR=5.0; N=int(SR*DUR)
rng=np.random.default_rng(7)
t=np.arange(N)/SR
L=np.zeros(N); R=np.zeros(N)
def nf(n): return 440*2**((n-69)/12)
def bell(f, start, amp, decay=2.2, pan=0.0, bright=1.0):
    n0=int(start*SR); tt=t[:N-n0]
    partials=[(1,1.0,1.0),(2.0,0.45*bright,0.6),(3.0,0.22*bright,0.45),(4.16,0.12*bright,0.3),(5.43,0.07*bright,0.22),(8.0,0.03*bright,0.15)]
    s=np.zeros_like(tt)
    for r,a,dk in partials:
        fr=f*r
        if fr>SR/2.2: continue
        s+=a*np.sin(2*np.pi*fr*tt+rng.uniform(0,6.28))*np.exp(-tt/(decay*dk))
    att=np.minimum(1,tt/0.004); s*=att*amp
    gl=np.sqrt(0.5*(1-pan)); gr=np.sqrt(0.5*(1+pan))
    L[n0:]+=s*gl; R[n0:]+=s*gr
# 1) acorde principal (Re mayor add9) — golpe cristalino
for n,a,p in [(62,0.55,-0.2),(69,0.5,0.2),(74,0.45,-0.35),(76,0.32,0.35),(78,0.3,0.0)]:
    bell(nf(n),0.06,a,decay=0.5,pan=p)
# grave cálido
bell(nf(50),0.06,0.25,decay=0.6,pan=0,bright=0.3)
# 2) arpegio ascendente de destellos
arp=[81,86,90,93,98,102]
for i,n in enumerate(arp):
    bell(nf(n),0.10+i*0.065,0.22*(0.92**i),decay=0.7,pan=(-1)**i*0.6,bright=0.4)
# 3) colchón brillante (entra suave, sostiene)
env=np.clip(t/0.35,0,1)*np.exp(-np.maximum(t-0.35,0)/1.6)
pad=np.zeros(N)
for n in [74,81,86]:
    for d in (-4,4):
        f=nf(n)*2**(d/1200)
        pad+=np.sin(2*np.pi*f*t+rng.uniform(0,6.28))
pad*=env*0.045*(1+0.12*np.sin(2*np.pi*4.5*t))
L+=pad; R+=pad*0.97
# 4) barrido de aire que sube hacia la luz (acompaña al fundido a blanco)
noise=rng.standard_normal(N)
seg=int(0.05*SR); out=np.zeros(N)
for i in range(0,N,seg):
    tc=i/SR
    fc=1500*2**(min(tc,1.4)/1.4*2.6)
    sos=butter(2,[fc*0.7,min(fc*1.4,SR/2.1)],btype='band',fs=SR,output='sos')
    blk=noise[max(0,i-2048):i+seg]
    y=sosfilt(sos,blk)[-(min(seg,N-i)):]
    out[i:i+len(y)]=y
renv=np.clip(t/1.25,0,1)**2*np.exp(-np.maximum(t-1.25,0)/0.35)
air=out*renv*0.18
L+=air; R+=np.roll(air,int(0.011*SR))
# 5) ecos ping-pong que se oscurecen
dry_L,dry_R=L.copy(),R.copy()
d=int(0.32*SR); fb=0.55; eL=np.zeros(N); eR=np.zeros(N)
srcL,srcR=dry_L,dry_R; g=1.0
lp=butter(1,6000,fs=SR,output='sos')
for k in range(1,8):
    g*=fb
    srcL=sosfilt(lp,srcL); srcR=sosfilt(lp,srcR)
    sh=d*k
    if sh>=N: break
    if k%2: eR[sh:]+=g*srcL[:N-sh]; eL[sh:]+=g*0.3*srcR[:N-sh]
    else:   eL[sh:]+=g*srcR[:N-sh]; eR[sh:]+=g*0.3*srcL[:N-sh]
L=dry_L+0.75*eL; R=dry_R+0.75*eR
# 6) reverb de sala grande (IR sintética, cola de ~3,5 s)
irn=int(3.8*SR); it=np.arange(irn)/SR
def ir():
    x=rng.standard_normal(irn)*np.exp(-it*6.9/3.5)
    # amortiguación de agudos progresiva
    lo=sosfilt(butter(1,2500,fs=SR,output='sos'),x)
    m=np.exp(-it/1.2)
    return (x*m+lo*(1-m))*np.clip(it/0.02,0,1)
irL,irR=ir(),ir(); irL/=np.sqrt((irL**2).sum()); irR/=np.sqrt((irR**2).sum())
wL=fftconvolve(L,irL)[:N]; wR=fftconvolve(R,irR)[:N]
pre=int(0.025*SR)
wL=np.concatenate([np.zeros(pre),wL[:-pre]]); wR=np.concatenate([np.zeros(pre),wR[:-pre]])
mix=0.35
L=L*(1-mix*0.4)+wL*mix*0.5; R=R*(1-mix*0.4)+wR*mix*0.5
# fundido final y normalizado
fo=int(0.6*SR); L[-fo:]*=np.linspace(1,0,fo)**2; R[-fo:]*=np.linspace(1,0,fo)**2
st=np.stack([L,R],1); st=sosfilt(butter(2,60,btype='high',fs=SR,output='sos'),st,axis=0)
st/=np.abs(st).max()/10**(-1/20)
wavfile.write('UI_ComenzarPartida.wav',SR,(st*32767).astype(np.int16))
print('ok', st.shape, 'rms dB', 20*np.log10(np.sqrt((st**2).mean())))
