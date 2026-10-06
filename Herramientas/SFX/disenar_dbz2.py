import numpy as np, os, sys
sys.path.insert(0,'/work/sfx')
from dsp import *
src=open('/work/sfx/disenar_dbz.py').read()
# reutiliza zap, rugido, chispazos, forcejeo, boom_dbz (sin ejecutar los S de ese fichero)
exec(src[src.index('def zap('):src.index('S={}')])
exec(src[src.index('def forcejeo('):src.index('dch=3.2')])
OUT='/work/sfx/out'
S={}
# Escudo: barrera eléctrica que encaja el golpe («bzzt-THUNK»), nada de campanitas
d=1.6; n=int(d*SR); t=np.arange(n)/SR
zumb=bp(sat(sum(osc(lambda tt,k=k:(120+40*k)*(1+0.02*np.sin(2*np.pi*30*tt)),d,'saw') for k in (0,1,2)),3),200,3500,2)*expdec(n,0.35,0.002)
S['Escudo_Bloquea']=reverb(eco(mix((0,stereo(boom_dbz(1.4,301,120,40,0.4),0.4),1.0),
                                   (0,stereo(zumb,0.6),0.7),
                                   (0,stereo(chispazos(0.8,700,302)*expdec(int(0.8*SR),0.25),0.8),0.8)),
                               0.2,0.25,2,2000),0.25,2.0,seed=301)
# Despegue: el aura que estalla y lo levanta (rugido que sube + golpe)
d=2.4; n=int(d*SR); t=np.arange(n)/SR
aura=rugido(d,50,1200,(20,38),311,0.9)*np.clip(t/0.25,0,1)*np.exp(-np.clip(t-0.9,0,None)/0.7)
S['Despegue']=reverb(mix((0,stereo(aura,0.7),1.0),(0,stereo(boom_dbz(1.5,312,110,35,0.5),0.4),0.8),
                         (0,stereo(chispazos(1.2,500,313)*expdec(int(1.2*SR),0.5),0.8),0.6)),0.3,2.5,seed=311)
# El conjuro del agujero: rugido oscuro enorme que crece, con aleteo lento
d=5.0; n=int(d*SR); t=np.arange(n)/SR
oscuro=rugido(d,35,500,(8,16),321,1.4)*env_adsr(n,2.0,0.4,0.9,1.5)
sub=osc(lambda tt:32+6*tt/d,d)*env_adsr(n,1.5,0.3,0.9,1.5)
silb=bp(osc(lambda tt:180*(2**(tt/d*1.5)),d,'saw'),300,2500,2)*(t/d)**2*0.4
S['Hechizo_Grande']=reverb(mix((0,stereo(oscuro,0.7),1.0),(0,sub,0.9),(0,stereo(silb,0.5),1.0),
                               (0,stereo(chispazos(d,250,322,(800,5000))*(t/d),0.8),0.5)),0.35,4.0,damp_end=500,seed=321)
# «¡Protégelos a todos!»: estallido de poder (aura que ruge y sube de tono)
d=4.0; n=int(d*SR); t=np.arange(n)/SR
poder=forcejeo(d,331,1.2)*np.clip(t/0.3,0,1)*env_adsr(n,0.05,0.3,0.9,1.0)
grito=bp(osc(lambda tt:440*(1+0.6*tt/d),d,'saw'),600,5000,2)*0.25*np.clip(t/1.5,0,1)
S['Proteccion_Absoluta']=reverb(mix((0,stereo(boom_dbz(2.0,332,130,35,0.7),0.4),0.9),
                                    (0,stereo(poder,0.7),0.9),(0,stereo(grito,0.5),1.0)),0.3,3.0,seed=331)
for k,v in S.items():
    write(f'{OUT}/SFX_{k}.wav', v, -0.8); print(k, round(len(trim_tail(v))/SR,2))
