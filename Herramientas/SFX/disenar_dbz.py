import numpy as np, os, sys
sys.path.insert(0,'/work/sfx')
from dsp import *
OUT='/work/sfx/out'; os.makedirs(OUT,exist_ok=True)

def zap(d, f0, f1, seed):
    """El «pshiuu» del disparo de ki: chirrido que cae + ráfaga brillante."""
    t=t_(d)
    tono=osc(lambda t: f1+(f0-f1)*np.exp(-t*9), d, 'sq')*0.5+osc(lambda t: (f1+(f0-f1)*np.exp(-t*9))*1.5, d,'saw')*0.4
    rafaga=sweep_filter(noise(d,seed),7000,900,'low',curve=0.35)
    rafaga=bp(rafaga,500,9000,2)
    y=(tono*0.6+rafaga)*expdec(int(d*SR),d*0.35,0.002)
    y=flanger(y,3.0,0.0005,0.003,mix_=0.5)
    return sat(y,1.8)
def rugido(d, f_lo=60, f_hi=500, flutter=(22,30), seed=0, cuerpo=0.8):
    """Rugido de energía (aura, rayo): ruido grave con aleteo rápido + armónicos sucios."""
    n=int(d*SR); t=np.arange(n)/SR
    r=bp(noise(d,seed),f_lo,f_hi,2)
    r=am(r, lambda t: flutter[0]+(flutter[1]-flutter[0])*t/d, 0.55)
    s=sum(osc(lambda tt,k=k: (55+12*tt/d)*(1+0.01*k),d,'saw') for k in (-2,-1,0,1,2))
    s=sat(lp(s,900)*cuerpo,3.0)
    return r*1.6+s*0.5
def chispazos(d, dens, seed, banda=(2000,9000)):
    c=crackle(d,dens,seed)
    return bp(c,*banda)*4

def coro(notas, d, vocal='a', seed=0):
    form={'a':[(700,130),(1220,70),(2600,160)],'o':[(450,80),(800,80),(2830,100)],'u':[(325,50),(700,60),(2530,170)]}[vocal]
    t=t_(d); src=np.zeros(len(t))
    for i,f in enumerate(notas):
        for det in (-0.004,0,0.005):
            vib=1+0.006*np.sin(2*np.pi*(5+i*0.3)*t+i)
            src+=osc(lambda tt,f=f,det=det,vib=vib: f*(1+det)*vib,d,'saw')
    y=np.zeros(len(t))
    for fc,bw in form:
        y+=bp(src,fc-bw,fc+bw,2)
    return y

S={}
# ── Disparos ────────────────────────────────────────────────────────────────────────────
S['Ki_Disparo_Luz']=reverb(eco(mix((0,stereo(zap(0.9,3200,420,201),0.5),1.0),
                                  (0,stereo(chispazos(0.5,300,202),0.7),0.4),
                                  (0.02,stereo(rugido(0.7,80,900,(30,35),203,0.5)*expdec(int(0.7*SR),0.25,0.01),0.6),0.5)),
                               0.22,0.25,2,2500),0.25,1.8,seed=201)
S['Ki_Disparo_Oscuro']=reverb(eco(mix((0,stereo(zap(1.0,1600,180,211),0.5),1.0),
                                     (0,stereo(rugido(0.9,50,600,(18,24),212,1.0)*expdec(int(0.9*SR),0.35,0.01),0.6),0.8),
                                     (0,stereo(chispazos(0.6,200,213,(1200,6000)),0.7),0.4)),
                                  0.25,0.25,2,1800),0.28,2.0,seed=211)
# ── Carga (1 s, justo antes del choque) ─────────────────────────────────────────────────
d=1.05; n=int(d*SR); t=np.arange(n)/SR
silbido=osc(lambda t: 300*(4.0**(t/d)),d,'saw'); silbido=bp(silbido,400,5000,2)*(t/d)**1.5
S['Ki_Carga']=reverb(mix((0,stereo(rugido(d,60,700,(14,40),221)*np.clip(t/d*1.4,0,1)**1.2,0.6),1.0),
                         (0,stereo(silbido,0.4),0.35),
                         (0,stereo(chispazos(d,400,222)*(t/d)**2,0.8),0.6)),0.2,1.5,seed=221)
# ── El choque: golpe + forcejeo de energía (3 s) ────────────────────────────────────────
def forcejeo(d, seed, sube=1.0):
    n=int(d*SR); t=np.arange(n)/SR
    base=rugido(d,70,2500,(26,44),seed,1.2)
    base=flanger(base,0.6,0.0006,0.005,mix_=0.55)
    zumbido=sum(osc(lambda tt,k=k: (110*(1+sube*tt/d))*(1+0.013*k),d,'saw') for k in (-1,0,1))
    zumbido=bp(sat(zumbido,2.5),300,4000,2)
    zumbido=am(zumbido,lambda tt:30+20*tt/d,0.5)
    alto=osc(lambda tt: 1800*(1+0.8*tt/d),d,'sin')*0.15*(t/d)
    y=base*1.0+zumbido*0.55+alto+chispazos(d,350,seed+5)*0.8
    return sat(y/np.abs(y).max()*1.3,1.6)
def boom_dbz(d, seed, f0=160, f1=28, tau=1.4):
    n=int(d*SR)
    golpe=lp(noise(0.3,seed),5000)*expdec(int(0.3*SR),0.04,0.0005)
    sub=osc(lambda t:(f0-f1)*np.exp(-t*5)+f1,d)*expdec(n,tau,0.004)
    cola=lp(noise(d,seed+1),700)*expdec(n,tau*0.9,0.02)
    cola=am(cola,lambda t:9+0*t,0.35)
    y=sub*1.0+cola*1.8; y[:len(golpe)]+=golpe*1.2
    return sat(y/np.abs(y).max()*1.3,2.0)
dch=3.2; n=int(dch*SR)
f=forcejeo(dch,231,0.3)*env_adsr(n,0.02,0.3,0.85,1.0)
S['Choque_Hechizos_DBZ']=reverb(eco(mix((0,stereo(boom_dbz(3.5,232),0.4),1.0),
                                        (0.05,stereo(f,0.7),0.75),
                                        (0,stereo(chispazos(2.0,600,233)*expdec(int(2*SR),0.6),0.8),0.6)),
                                    0.33,0.3,3,1500),0.35,4.0,damp_end=700,seed=231)
# ── Impacto de hechizo ──────────────────────────────────────────────────────────────────
S['Impacto_DBZ']=reverb(eco(mix((0,stereo(boom_dbz(2.2,241,140,32,0.8),0.4),1.0),
                                (0,stereo(chispazos(1.2,400,242)*expdec(int(1.2*SR),0.4),0.8),0.6)),
                            0.3,0.3,3,1500),0.3,3.0,damp_end=800,seed=241)
# ── El final: los dos hechizos empujando (5,6 s, sube y sube) ───────────────────────────
dfin=5.8; n=int(dfin*SR); t=np.arange(n)/SR
lucha=forcejeo(dfin,251,1.6)*np.clip(t/1.0,0,1)*(0.55+0.45*(t/dfin)**1.5)
coro_=coro([146.8,220,293.7],dfin,'a')*(t/dfin)**2*env_adsr(n,0.5,0.1,1,0.05)
latido=np.zeros(n)
# latidos que se aceleran
tt=0.3; per=0.75
while tt<dfin-0.2:
    o=int(tt*SR); b=osc(lambda x:55*np.exp(-x*6)+38,0.35)*expdec(int(0.35*SR),0.09,0.003)
    latido[o:o+len(b)]+=b[:max(0,min(len(b),n-o))]; tt+=per; per=max(0.32,per*0.9)
S['Lucha_Final']=reverb(mix((0,stereo(lucha,0.7),1.0),(0,stereo(coro_,0.7),0.35),(0,latido,0.9),
                            (0,stereo(chispazos(dfin,500,252)*(t/dfin),0.8),0.5)),0.3,3.0,seed=251)
# ── La explosión que lo blanquea todo, con eco y pitido ─────────────────────────────────
dex=9.0; n=int(dex*SR); t=np.arange(n)/SR
grande=boom_dbz(dex,261,180,22,2.8)
rugido_largo=lp(noise(dex,262),1200)*expdec(n,2.2,0.05)
rugido_largo=am(rugido_largo,lambda t:7+0*t,0.3)
pitido=osc(lambda x:3900+0*x,dex)*0.07*np.clip((t-0.6)/0.4,0,1)*np.exp(-np.clip(t-0.6,0,None)/3.0)
S['Explosion_Final_DBZ']=reverb(eco(mix((0,stereo(grande,0.4),1.0),(0,stereo(rugido_largo,0.8),1.0),
                                        (0,stereo(chispazos(4,500,263)*expdec(int(4*SR),1.2),0.8),0.5)),
                                    0.45,0.35,3,1200),0.4,6.0,damp_end=500,seed=261)
S['Explosion_Final_DBZ']=mix((0,S['Explosion_Final_DBZ'],1.0),(0,pitido,1.0))
# ── Silencio blanco: pitido de oídos que se va (para el paso a Will) ────────────────────
dsi=4.5; n=int(dsi*SR); t=np.arange(n)/SR
ring=(osc(lambda x:4200+0*x,dsi)*0.6+osc(lambda x:4213+0*x,dsi)*0.4)*np.exp(-t/1.6)
apagado=lp(noise(dsi,271),300)*0.25*np.exp(-t/1.2)
S['Silencio_Blanco']=mix((0,stereo(ring,0.3),0.5),(0,stereo(apagado,0.6),1.0))

for k,v in S.items():
    write(f'{OUT}/SFX_{k}.wav', v, -0.8)
    print(k, round(len(trim_tail(v))/SR,2),'s')
