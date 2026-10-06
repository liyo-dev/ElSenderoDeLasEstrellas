import numpy as np, os, sys
sys.path.insert(0,'/work/sfx')
from dsp import *
OUT='/work/sfx/out'; os.makedirs(OUT,exist_ok=True)

def bumps(n, k, seed, width=(0.15,0.6), start=0.0):
    r=np.random.default_rng(seed); t=np.arange(n)/SR; e=np.zeros(n); dur=n/SR
    for i in range(k):
        c=start+r.random()*(dur-start)*0.85; w=r.uniform(*width); a=r.uniform(0.35,1.0)*np.exp(-c/(dur*0.6))
        e+=a*np.exp(-0.5*((t-c)/w)**2)
    return e

# ── TRUENOS ─────────────────────────────────────────────────────────────────────────────
def trueno_cercano(seed):
    r=np.random.default_rng(seed); d=8.0; n=int(d*SR)
    # chasquido: ráfagas brillantes que se rompen en 0,4 s
    crack=hp(noise(0.5,seed),1800)*expdec(int(0.5*SR),0.07,0.0005)
    crack+=crackle(0.5,900,seed+1,decay=0.12)*3
    rip=sweep_filter(noise(0.35,seed+2),9000,700,'low')*expdec(int(0.35*SR),0.12,0.001)
    # golpe grave
    boom=lp(noise(2.5,seed+3),220)*expdec(int(2.5*SR),0.55,0.01)*5
    sub=osc(lambda t:70*np.exp(-t*1.2)+28,2.0)*expdec(int(2.0*SR),0.7,0.005)
    # retumbo que rueda
    rum=lp(noise(d,seed+4),160)*(0.25+bumps(n,7,seed+5,(0.25,0.9),0.4))*np.exp(-np.arange(n)/SR/3.2)*6
    rum2=bp(noise(d,seed+6),180,700)*bumps(n,5,seed+7,(0.1,0.4),0.3)*np.exp(-np.arange(n)/SR/2.5)*1.5
    y=mix((0,stereo(crack,0.6),0.9),(0.02,stereo(rip,0.5),0.6),(0.03,stereo(boom,0.4),0.9),(0.03,sub,0.8),
          (0.25,stereo(rum,0.7),1.0),(0.4,stereo(rum2,0.7),0.6))
    y=sat(y/np.abs(y).max()*1.2,1.6)
    return reverb(y,0.35,4.5,damp_start=6000,damp_end=600,seed=seed)
def trueno_lejano(seed):
    d=9.0; n=int(d*SR)
    rum=lp(noise(d,seed),230)*bumps(n,8,seed+1,(0.3,1.1),0.0)*np.exp(-np.arange(n)/SR/3.5)
    crack=lp(crackle(1.0,200,seed+2,decay=0.3),2500)*0.6
    y=mix((0.3,stereo(crack,0.7),0.5),(0,stereo(rum,0.8),1.0))
    y=fade(y,0.4,0.01)
    return reverb(y,0.45,5.0,damp_start=3000,damp_end=400,seed=seed)

# ── PIEZAS DE HECHIZO ───────────────────────────────────────────────────────────────────
def chispas(d, base, seed, n=14, tau=0.5):
    r=np.random.default_rng(seed); y=np.zeros(int(d*SR)+int(2*SR))
    escala=[1,9/8,5/4,3/2,5/3,2,9/4,5/2,3]
    for i in range(n):
        f=base*r.choice(escala)*r.choice([1,2]); o=int(r.random()*d*SR)
        b=fm_bell(f,1.4,ratio=r.choice([2.0,3.5,1.41]),index=r.uniform(1,3),tau=tau)*r.uniform(0.3,1)
        y[o:o+len(b)]+=b
    return y
def shwing(d=0.45, f0=500, f1=7000, seed=1):
    x=osc(lambda t: 180+0*t, d,'saw')+osc(lambda t:181.3+0*t,d,'saw')+noise(d,seed)*0.4
    y=sweep_filter(x,f0,f1,'low',curve=0.6)
    y=sweep_filter(y,f0*0.5,f1*0.6,'high',curve=0.8)
    return y*env_adsr(len(y),0.05,0.1,0.8,0.2)
def whoosh(d=0.6, f0=300, f1=2500, seed=3, rev=False):
    x=noise(d,seed); t=np.arange(len(x))/SR
    y=bp(x,f0,f1,2)
    e=np.sin(np.pi*np.clip(t/d,0,1))**2
    y=sweep_filter(y,f0*2,f1*2,'low')*e
    return y[::-1] if rev else y
def boom(d=1.6, f0=120, f1=34, tau=0.7, seed=5, body=900):
    s=osc(lambda t:(f0-f1)*np.exp(-t*7)+f1,d)*expdec(int(d*SR),tau,0.003)
    punch=lp(noise(0.25,seed),3500)*expdec(int(0.25*SR),0.05,0.0008)
    cuerpo=lp(noise(d,seed+1),body)*expdec(int(d*SR),tau*0.6,0.002)
    y=s.copy(); y[:len(punch)]+=punch*0.8; y+=cuerpo*1.2
    return sat(y/np.abs(y).max(),2.2)
def fuego(d=1.0, seed=8):
    x=lp(noise(d,seed),900)*(0.6+0.4*np.abs(lp(noise(d,seed+1),8)*20))
    x+=crackle(d,120,seed+2)*1.5
    return x*env_adsr(int(d*SR),0.05,0.2,0.7,0.4)
def anillo(f=220, d=2.5, parciales=(1,2.76,5.40,8.93), tau=1.0):
    t=t_(d); y=np.zeros(len(t))
    for i,p in enumerate(parciales):
        y+=np.sin(2*np.pi*f*p*t+i)*np.exp(-t/(tau/(1+0.6*i)))/(1+i*0.7)
    return y
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
def riser(d, f0, f1, seed=0):
    t=t_(d); u=t/d
    x=sum(osc(lambda tt,k=k: (f0*(f1/f0)**(tt/d))*(1+0.007*k),d,'saw') for k in (-1,0,1))
    trem=1-0.5*(0.5+0.5*np.sin(2*np.pi*np.cumsum(6+30*u**2)/SR))
    y=sweep_filter(x,300,7000,'low',curve=1.5)*trem
    y+=hp(noise(d,seed),3000)*u**2*0.6
    return y*np.clip(u*3,0,1)

# ── SONIDOS DEL PRÓLOGO ─────────────────────────────────────────────────────────────────
S={}
S['Trueno_Cercano_1']=trueno_cercano(101)
S['Trueno_Cercano_2']=trueno_cercano(202)
S['Trueno_Cercano_3']=trueno_cercano(303)
S['Trueno_Lejano_Rodante']=trueno_lejano(404)

# Archimago: «kiiin» brillante + bola de fuego que sale
S['Hechizo_Luz_Lanzar']=reverb(mix((0,stereo(shwing(0.4,600,9000,1),0.5),0.7),
                                   (0,stereo(chispas(0.35,1046,21,10,0.45),0.7),0.45),
                                   (0.28,stereo(whoosh(0.7,250,3000,4),0.6),0.9),
                                   (0.3,stereo(fuego(0.9,9),0.6),0.6)),0.3,2.2,seed=21)
# Mago Oscuro: gruñido grave, brillo oscuro y descarga
gru=sat(lp(osc(lambda t:55*(1-0.2*t),0.9,'saw')+osc(lambda t:55.7*(1-0.2*t),0.9,'saw'),700)*env_adsr(int(0.9*SR),0.15,0.2,0.8,0.35),2.5)
S['Hechizo_Oscuro_Lanzar']=reverb(mix((0,stereo(whoosh(0.5,200,1500,6,rev=True),0.6),0.8),
                                      (0.3,stereo(gru,0.4),1.0),
                                      (0.35,stereo(chispas(0.3,311,31,8,0.5),0.7),0.4),
                                      (0.45,stereo(whoosh(0.6,150,1800,7),0.6),0.9)),0.35,2.6,seed=31)
# Impacto de hechizo
S['Hechizo_Impacto']=reverb(mix((0,stereo(boom(1.8,140,36,0.8,41),0.3),1.0),
                                (0,stereo(crackle(1.6,160,42,decay=0.6),0.8),0.7),
                                (0,stereo(fuego(1.4,43),0.6),0.5)),0.35,3.5,damp_end=900,seed=41)
# Escudo que para el golpe
S['Escudo_Bloquea']=reverb(mix((0,stereo(fm_bell(2637,2.0,1.41,3.0,1.1)+0.6*fm_bell(3951,2.0,2.0,2.0,0.9)+0.4*anillo(1318,2.0,(1,2.32,4.25),0.8),0.6),0.8),
                               (0,boom(0.8,100,50,0.25,51),0.6),
                               (0.05,stereo(chispas(0.5,2093,52,8,0.4),0.8),0.35)),0.35,3.0,seed=51)
# Carga a dos (1 s) y el choque
S['Carga_Doble']=reverb(mix((0,stereo(riser(1.0,110,520,61),0.5),1.0),
                            (0.1,stereo(coro([220,330],0.9,'a'),0.6),0.25)),0.25,1.8,seed=61)
S['Choque_Hechizos']=reverb(mix((0,stereo(boom(2.6,160,30,1.1,71,1200),0.3),1.0),
                                (0.06,stereo(boom(1.6,110,40,0.6,72),0.5),0.6),
                                (0,stereo(anillo(196,3.0,(1,2.76,5.40,8.93,13.3),1.6),0.8),0.45),
                                (0,stereo(crackle(2.5,220,73,decay=0.9),0.8),0.6),
                                (0.02,stereo(lp(noise(4.0,74),300)*expdec(int(4*SR),1.6,0.05),0.8),1.0)),0.4,5.0,damp_end=700,seed=71)
# El suelo se abre (la grieta)
S['Suelo_Se_Abre']=reverb(mix((0,stereo(lp(noise(2.0,81),180)*env_adsr(int(2*SR),0.25,0.4,0.7,1.0),0.7),1.2),
                              (0.25,stereo(bp(crackle(1.6,300,82,decay=0.7),200,3000),0.8),1.2),
                              (0.3,stereo(boom(1.5,90,30,0.6,83,500),0.4),1.0)),0.3,3.2,damp_end=700,seed=81)
# Despegue (levita)
S['Despegue']=reverb(mix((0,stereo(whoosh(0.9,150,2000,91,rev=True),0.6),0.9),
                         (0.75,stereo(boom(1.2,70,35,0.5,92,400),0.4),0.7),
                         (0.7,stereo(chispas(0.8,622,93,8,0.6),0.8),0.3)),0.35,3.0,seed=91)
# Empieza a conjurar el agujero negro
sub=osc(lambda t:38+3*np.sin(2*np.pi*0.3*t),4.0)*env_adsr(int(4*SR),1.5,0.5,0.9,1.5)
dron=lp(osc(lambda t:55,4.0,'saw')+osc(lambda t:55.4,4.0,'saw')+osc(lambda t:82.6,4.0,'saw'),400)*env_adsr(int(4*SR),2.0,0.3,0.8,1.5)
S['Hechizo_Grande']=reverb(mix((0,stereo(sub,0.2),1.0),(0,stereo(dron,0.6),0.8),
                               (0.4,stereo(coro([146.8,174.6,207.7],3.4,'u')*env_adsr(int(3.4*SR),1.6,0.4,0.8,1.2),0.7),0.35),
                               (0,stereo(whoosh(2.0,80,600,101,rev=True),0.7),0.6)),0.45,4.0,damp_end=500,seed=101)
# «¡Protégelos a todos!»: luz sagrada que crece
S['Proteccion_Absoluta']=reverb(mix((0,stereo(coro([523.3,659.3,784.0,1046.5],3.5,'o')*env_adsr(int(3.5*SR),1.2,0.4,0.9,1.4),0.7),0.8),
                                    (0,stereo(chispas(3.0,1046,111,26,0.7),0.8),0.5),
                                    (0,stereo(shwing(1.5,300,9000,112),0.6),0.4),
                                    (0,stereo(osc(lambda t:65.4,3.5)*env_adsr(int(3.5*SR),1.0,0.5,0.8,1.5),0.2),0.6)),0.45,4.5,seed=111)
# La explosión final
S['Explosion_Final']=reverb(mix((0,stereo(boom(4.0,120,24,1.8,121,1500),0.4),1.0),
                                (0.05,stereo(boom(2.5,90,30,1.0,122,800),0.6),0.7),
                                (0,stereo(crackle(4.0,300,123,decay=1.4),0.8),0.6),
                                (0,stereo(lp(noise(7.0,124),400)*expdec(int(7*SR),2.6,0.08),0.8),1.2),
                                (0,stereo(hp(noise(2.5,125),5000)*expdec(int(2.5*SR),0.9,0.01),0.8),0.25)),0.45,6.0,damp_end=500,seed=121)
# Aparece el Mago: «braam» de villano
braam=sum(osc(lambda t,f=f: f*(1-0.03*t),3.0,'saw') for f in (55,55.3,110,110.6,82.4,164.8))
braam=sat(sweep_filter(braam,1400,300,'low',curve=0.7),2.0)*env_adsr(int(3*SR),0.02,0.6,0.6,1.8)
S['Golpe_Mago']=reverb(mix((0,stereo(braam,0.5),1.0),(0,boom(1.5,130,32,0.6,131),0.9)),0.35,4.0,damp_end=600,seed=131)

for k,v in S.items():
    write(f'{OUT}/SFX_{k}.wav', v, -1.0)
    print(k, round(len(trim_tail(v))/SR,2),'s')
