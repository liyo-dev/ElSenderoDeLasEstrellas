"""Tercera tanda (5 oct 2026): mismo carácter anime, pero BRILLANTE y COMPRIMIDO.

Medido con la recopilación de referencia que pasó Raúl (solo como regla de medir,
no se usa ni un trozo de ella):
  - la energía va sobre todo a 1–4 kHz (mediana 59 %), apenas un 8 % por debajo de 250 Hz;
  - ataque casi instantáneo y luego una «pared» sostenida, muy comprimida (cresta ~14 dB);
  - zumbidos con muchos armónicos y chisporroteo eléctrico, no graves limpios.
Nuestra tanda anterior era al revés: 50–70 % de la energía por debajo de 250 Hz (oscura y retumbante).
Aquí los graves quedan como apoyo del golpe y el cuerpo del sonido pasa a la zona media-alta.
"""
import numpy as np, os, sys
from scipy.signal import lfilter
sys.path.insert(0, '/work/sfx')
from dsp import *
src = open('/work/sfx/disenar_dbz.py').read()
exec(src[src.index('def zap('):src.index('S={}')])
exec(src[src.index('def forcejeo('):src.index('dch=3.2')])
OUT = '/work/sfx/out3'; os.makedirs(OUT, exist_ok=True)


# ── Capas nuevas ────────────────────────────────────────────────────────────────────────
def chisporroteo(d, seed, lo=1500, hi=7000, rate=(12, 26), prof=0.8):
    """Siseo eléctrico: ruido en la zona alta con un aleteo irregular rápido."""
    n = int(d*SR); rng = np.random.default_rng(seed)
    r = bp(noise(d, seed), lo, hi, 2)
    # aleteo irregular: frecuencia que deriva entre rate[0] y rate[1]
    f = rate[0] + (rate[1]-rate[0])*lp(rng.random(n), 3, 1)*2
    fase = 2*np.pi*np.cumsum(np.clip(f, rate[0], rate[1]))/SR
    m = 1 - prof*(0.5+0.5*np.sin(fase))**2
    return r*m


def zumbido(f_fn, d, voces=5, det=0.012, lo=500, hi=6000, drive=3.0):
    """Zumbido de energía: pila de sierras desafinadas, saturada y recortada a la zona media-alta."""
    s = sum(osc(lambda t, k=k: f_fn(t)*(1+det*k), d, 'saw') for k in range(-(voces//2), voces//2+1))
    return bp(sat(s/voces*2, drive), lo, hi, 2)


def master(y, umbral_db=-24, ratio=5, graves=0.5, corte=220, brillo=1.0):
    """Baja graves (deja el golpe), sube algo de brillo, comprime fuerte y limita."""
    y = np.atleast_2d(y.T).T if y.ndim == 1 else y
    out = []
    for c in range(y.shape[1]):
        x = y[:, c]
        bajo = lp(x, corte, 2); x = x - bajo*(1-graves)
        if brillo != 1.0:
            x = x + (brillo-1)*hp(x, 2500, 2)
        out.append(x)
    x = np.stack(out, 1)
    # compresor por RMS con ataque 5 ms y suelta 120 ms
    p = (x**2).mean(1)
    a = np.exp(-1/(0.005*SR)); r = np.exp(-1/(0.12*SR))
    env = np.empty_like(p); e = 0.0
    # seguidor sencillo (bucle en bloques de 64 para que vaya rápido)
    blk = 64; nb = len(p)//blk + 1; pe = np.array([p[i*blk:(i+1)*blk].mean() if i*blk < len(p) else 0 for i in range(nb)])
    eb = np.empty(nb); ab = a**blk; rb = r**blk
    for i in range(nb):
        k = ab if pe[i] > e else rb
        e = k*e + (1-k)*pe[i]; eb[i] = e
    env = np.repeat(eb, blk)[:len(p)]
    lvl = 10*np.log10(env+1e-12); ref = lvl.max()
    exceso = np.maximum(lvl - (ref+umbral_db), 0)
    g = 10**(-exceso*(1-1/ratio)/20)
    x = x*g[:, None]
    return sat(x/np.abs(x).max()*1.4, 1.3)


S = {}
# ── Disparos: «pshiuu» brillante y largo, con siseo ─────────────────────────────────────
for nombre, f0, f1, base, seed in (('Ki_Disparo_Luz', 3600, 900, 640, 401), ('Ki_Disparo_Oscuro', 2400, 520, 330, 411)):
    d = 1.9; n = int(d*SR); t = np.arange(n)/SR
    caida = lambda tt, f0=f0, f1=f1: f1+(f0-f1)*np.exp(-tt*5)
    cuerpo = zumbido(lambda tt, base=base: base*(1+0.25*np.exp(-tt*4)), d, 5, 0.015)*env_adsr(n, 0.008, 0.2, 0.75, 0.9)
    silbo = zumbido(caida, d, 3, 0.006, 700, 8000, 2.0)*expdec(n, 0.55, 0.002)
    sis = chisporroteo(d, seed)*env_adsr(n, 0.004, 0.3, 0.6, 0.8)
    y = mix((0, stereo(zap(0.8, f0, f1, seed), 0.5), 0.5),
            (0, stereo(silbo, 0.4), 0.9), (0, stereo(cuerpo, 0.6), 0.8), (0, stereo(sis, 0.8), 0.7))
    S[nombre] = master(reverb(eco(y, 0.22, 0.2, 2, 4000), 0.2, 1.6, seed=seed), graves=0.35)

# ── Carga: zumbido que sube de tono y de volumen, chisporroteo cada vez más denso ───────
d = 1.6; n = int(d*SR); t = np.arange(n)/SR
sube = zumbido(lambda tt: 260*(5.0**(tt/d)), d, 5, 0.01)*np.clip(t/d*1.3, 0, 1)**1.3
tembl = zumbido(lambda tt: 130*(2.0**(tt/d)), d, 3, 0.02, 300, 3000, 4.0)*np.clip(t/0.15, 0, 1)
sis = chisporroteo(d, 421, rate=(10, 40))*(t/d)**1.5
S['Ki_Carga'] = master(reverb(mix((0, stereo(sube, 0.5), 0.9), (0, stereo(tembl, 0.6), 0.6),
                                  (0, stereo(sis, 0.8), 0.9), (0, stereo(chispazos(d, 500, 422)*(t/d)**2, 0.8), 0.4)),
                              0.2, 1.5, seed=421), graves=0.3)


# ── Forcejeo brillante: la pared de energía que empuja ──────────────────────────────────
def pared(d, seed, sube=1.0, base=180):
    n = int(d*SR); t = np.arange(n)/SR
    z1 = zumbido(lambda tt: base*(1+sube*tt/d), d, 7, 0.014, 400, 6000, 3.5)
    z2 = zumbido(lambda tt: base*1.5*(1+sube*tt/d)*(1+0.03*np.sin(2*np.pi*7*tt)), d, 5, 0.02, 800, 7000, 3.0)
    sis = chisporroteo(d, seed, 1200, 8000, (14, 34))
    y = z1*0.8 + z2*0.5 + sis*1.0 + forcejeo(d, seed, sube)*0.35
    return flanger(y, 0.5, 0.0005, 0.004, mix_=0.4)


dch = 3.2; n = int(dch*SR)
f = pared(dch, 431, 0.3)*env_adsr(n, 0.01, 0.2, 0.9, 0.9)
S['Choque_Hechizos_DBZ'] = master(reverb(eco(mix((0, stereo(boom_dbz(2.5, 432), 0.4), 0.9),
                                                 (0.03, stereo(f, 0.7), 1.0),
                                                 (0, stereo(chispazos(1.5, 700, 433)*expdec(int(1.5*SR), 0.5), 0.8), 0.6)),
                                             0.3, 0.25, 3, 3000), 0.3, 3.0, damp_end=1500, seed=431), graves=0.6, corte=180)

# ── Impacto: golpe seco + estallido brillante que se abre ───────────────────────────────
d = 2.4; n = int(d*SR); t = np.arange(n)/SR
estallido = (bp(noise(d, 441), 600, 9000, 2)*expdec(n, 0.45, 0.001)
             + zumbido(lambda tt: 220*np.exp(-tt*1.2)+90, d, 5, 0.03, 300, 5000, 4.0)*expdec(n, 0.6, 0.003)*0.7)
S['Impacto_DBZ'] = master(reverb(eco(mix((0, stereo(boom_dbz(2.2, 442, 140, 40, 0.7), 0.4), 1.0),
                                         (0, stereo(estallido, 0.7), 1.0),
                                         (0, stereo(chisporroteo(d, 443)*expdec(n, 0.7), 0.8), 0.6)),
                                     0.3, 0.25, 3, 2500), 0.3, 2.5, damp_end=1500, seed=441), graves=0.7, corte=160)

# ── Lucha final: la pared sube de tono, latidos que se aceleran, coro ───────────────────
dfin = 5.8; n = int(dfin*SR); t = np.arange(n)/SR
lucha = pared(dfin, 451, 1.8, 160)*np.clip(t/0.4, 0, 1)*(0.6+0.4*(t/dfin)**1.5)
coro_ = coro([293.7, 440, 587.3], dfin, 'a')*(t/dfin)**2*env_adsr(n, 0.5, 0.1, 1, 0.05)
latido = np.zeros(n); tt = 0.3; per = 0.75
while tt < dfin-0.2:
    o = int(tt*SR)
    b = (osc(lambda x: 60*np.exp(-x*6)+40, 0.3) + bp(noise(0.3, int(tt*100)), 800, 3000)*0.4)*expdec(int(0.3*SR), 0.08, 0.002)
    latido[o:o+len(b)] += b[:max(0, min(len(b), n-o))]; tt += per; per = max(0.3, per*0.9)
S['Lucha_Final'] = master(reverb(mix((0, stereo(lucha, 0.7), 1.0), (0, stereo(coro_, 0.7), 0.35), (0, latido, 0.8),
                                     (0, stereo(chispazos(dfin, 600, 452)*(t/dfin), 0.8), 0.5)), 0.25, 2.5, seed=451),
                          graves=0.6, corte=150)

# ── Explosión final: estallido brillante enorme que se va a grave, eco y pitido ─────────
dex = 9.0; n = int(dex*SR); t = np.arange(n)/SR
blanco = bp(noise(dex, 461), 500, 10000, 2)*expdec(n, 1.1, 0.002)
pared_final = zumbido(lambda tt: 200*np.exp(-tt*0.5)+60, dex, 7, 0.03, 250, 6000, 4.0)*expdec(n, 1.6, 0.005)
grande = boom_dbz(dex, 462, 180, 22, 2.8)
ruido_largo = lp(noise(dex, 463), 2500)*expdec(n, 2.4, 0.05)
y = reverb(eco(mix((0, stereo(grande, 0.4), 0.8), (0, stereo(blanco, 0.8), 1.0), (0, stereo(pared_final, 0.7), 0.7),
                   (0, stereo(ruido_largo, 0.8), 0.6), (0, stereo(chisporroteo(4, 464)*expdec(int(4*SR), 1.0), 0.8), 0.6)),
               0.45, 0.35, 3, 2500), 0.4, 6.0, damp_end=800, seed=461)
y = master(y, graves=0.8, corte=140, umbral_db=-26)
pitido = osc(lambda x: 3900+0*x, dex)*0.06*np.clip((t-0.6)/0.4, 0, 1)*np.exp(-np.clip(t-0.6, 0, None)/3.0)
S['Explosion_Final_DBZ'] = mix((0, y, 1.0), (0, pitido, 1.0))

# ── Escudo: «bzzt-CHANK» brillante ──────────────────────────────────────────────────────
d = 1.6; n = int(d*SR); t = np.arange(n)/SR
barrera = zumbido(lambda tt: 300*(1+0.02*np.sin(2*np.pi*30*tt)), d, 5, 0.02, 500, 7000, 4.0)*expdec(n, 0.4, 0.001)
S['Escudo_Bloquea'] = master(reverb(eco(mix((0, stereo(boom_dbz(1.2, 471, 120, 45, 0.35), 0.4), 0.8),
                                            (0, stereo(barrera, 0.6), 1.0),
                                            (0, stereo(chisporroteo(1.0, 472, 2000, 9000)*expdec(int(1.0*SR), 0.3), 0.8), 0.8)),
                                        0.2, 0.2, 2, 3500), 0.25, 1.8, seed=471), graves=0.5)

# ── Despegue: aura que estalla y lo levanta ─────────────────────────────────────────────
d = 2.4; n = int(d*SR); t = np.arange(n)/SR
aura = pared(d, 481, 0.6, 140)*np.clip(t/0.12, 0, 1)*np.exp(-np.clip(t-0.9, 0, None)/0.6)
S['Despegue'] = master(reverb(mix((0, stereo(aura, 0.7), 1.0), (0, stereo(boom_dbz(1.5, 482, 110, 40, 0.5), 0.4), 0.7)),
                              0.25, 2.2, seed=481), graves=0.6)

# ── Conjuro del agujero: pared oscura que crece (más grave que las otras, pero con filo) ──
d = 5.0; n = int(d*SR); t = np.arange(n)/SR
oscuro = pared(d, 491, 0.9, 90)*env_adsr(n, 1.8, 0.4, 0.9, 1.4)
sub = osc(lambda tt: 34+8*tt/d, d)*env_adsr(n, 1.5, 0.3, 0.9, 1.4)
S['Hechizo_Grande'] = master(reverb(mix((0, stereo(oscuro, 0.7), 1.0), (0, sub, 0.5),
                                        (0, stereo(chispazos(d, 300, 492, (1000, 7000))*(t/d), 0.8), 0.5)),
                                    0.3, 3.5, damp_end=900, seed=491), graves=0.8, corte=160)

# ── Protección absoluta: estallido de poder que sube ────────────────────────────────────
d = 4.0; n = int(d*SR); t = np.arange(n)/SR
poder = pared(d, 501, 1.3, 220)*np.clip(t/0.15, 0, 1)*env_adsr(n, 0.05, 0.3, 0.9, 0.9)
S['Proteccion_Absoluta'] = master(reverb(mix((0, stereo(boom_dbz(1.8, 502, 130, 40, 0.6), 0.4), 0.8),
                                             (0, stereo(poder, 0.7), 1.0)), 0.25, 2.8, seed=501), graves=0.6)


# ── El compresor aplana las curvas: se vuelven a imponer después ────────────────────────
def curva(y, g):
    g = np.asarray(g, float); g = np.pad(g, (0, max(0, len(y)-len(g))), mode='edge')[:len(y)]
    return y*g[:, None] if y.ndim == 2 else y*g
def tiempo(y): return np.arange(len(y))/SR
tc = tiempo(S['Ki_Carga']); S['Ki_Carga'] = curva(S['Ki_Carga'], 0.25+0.75*np.clip(tc/1.6, 0, 1)**1.6)
tl = tiempo(S['Lucha_Final']); S['Lucha_Final'] = curva(S['Lucha_Final'], 0.4+0.6*np.clip(tl/5.8, 0, 1)**1.8)
te = tiempo(S['Explosion_Final_DBZ'])
S['Explosion_Final_DBZ'] = curva(S['Explosion_Final_DBZ'], np.where(te < 0.9, 1.0, np.exp(-(te-0.9)/1.7)))
th = tiempo(S['Hechizo_Grande']); S['Hechizo_Grande'] = curva(S['Hechizo_Grande'], 0.3+0.7*np.clip(th/3.5, 0, 1)**1.4)

for k, v in S.items():
    write(f'{OUT}/SFX_{k}.wav', v, -0.8)
    print(k, round(len(trim_tail(v))/SR, 2), 's')
