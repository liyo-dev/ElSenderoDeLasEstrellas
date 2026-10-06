# Genera UI_NavegarMenu.wav: «tin» cristalino corto para mover el cursor del menú.
# Familia sonora de UI_ComenzarPartida (campanas en Re mayor + sala suave).
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt, fftconvolve
SR=48000; DUR=0.55; N=int(SR*DUR); t=np.arange(N)/SR
rng=np.random.default_rng(11)
L=np.zeros(N); R=np.zeros(N)
def nf(n): return 440*2**((n-69)/12)
def bell(f,start,amp,tau,pan,bright=0.5):
    n0=int(start*SR); tt=t[:N-n0]; s=np.zeros_like(tt)
    for r,a,k in [(1,1,1),(2.0,0.35*bright,0.55),(3.0,0.15*bright,0.4),(4.16,0.08*bright,0.3)]:
        s+=a*np.sin(2*np.pi*f*r*tt+rng.uniform(0,6.28))*np.exp(-tt/(tau*k))
    s*=np.minimum(1,tt/0.002)*amp
    L[n0:]+=s*np.sqrt(0.5*(1-pan)); R[n0:]+=s*np.sqrt(0.5*(1+pan))
bell(nf(93),0.0,0.9,0.085,-0.1,0.45)   # La6: el golpe principal
bell(nf(98),0.018,0.28,0.06,0.35,0.3)  # Re7: destello que lo adorna
# soplo de aire muy corto
nz=rng.standard_normal(N)*np.exp(-t/0.018)*np.minimum(1,t/0.003)
nz=sosfilt(butter(2,[3500,9000],btype='band',fs=SR,output='sos'),nz)*0.08
L+=nz; R+=np.roll(nz,int(0.004*SR))
# sala corta
irn=int(0.4*SR); it=np.arange(irn)/SR
def ir():
    x=rng.standard_normal(irn)*np.exp(-it*6.9/0.35)
    return x/np.sqrt((x**2).sum())
wL=fftconvolve(L,ir())[:N]; wR=fftconvolve(R,ir())[:N]
L=L*0.85+wL*0.18; R=R*0.85+wR*0.18
st=np.stack([L,R],1)
st=sosfilt(butter(2,9500,btype='low',fs=SR,output='sos'),st,axis=0)
st=sosfilt(butter(2,200,btype='high',fs=SR,output='sos'),st,axis=0)
fo=int(0.08*SR); st[-fo:]*=np.linspace(1,0,fo)[:,None]**2
st/=np.abs(st).max()/10**(-9/20)
wavfile.write('UI_NavegarMenu.wav',SR,(st*32767).astype(np.int16))
print('ok')
