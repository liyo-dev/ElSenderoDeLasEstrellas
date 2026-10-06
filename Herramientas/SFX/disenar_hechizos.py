"""SFX propios para todos los hechizos del juego (5 oct 2026, INC-595).

Mismo estilo que el prólogo (tercera tanda): brillante, comprimido, con zumbidos de muchos
armónicos y chisporroteo. Todo es síntesis propia; no se usa ningún audio de fuera.

Cada hechizo tiene su sonido de lanzamiento, ajustado a cómo se lanza en el juego:
  - Suena al EMPEZAR el gesto (MagicProjectileSpawner.Co_SpawnAfterDelay) y el hechizo sale a los
    castDelaySeconds (0,5 s casi todos). Por eso cada lanzamiento = carga hasta 'retardo' + salida.
  - Proyectil: la cola de la salida depende de la velocidad (rápido = seco y corto; lento = largo).
  - Zona: el sonido dura lo que la zona (estallido al abrirse, cuerpo y cierre).
  - Daño: más daño = más cuerpo, más grave y más cola.
Los impactos van por elemento y tamaño (pequeño < 20 de daño, grande >= 20).
Salen en mono (los impactos y los de los aliados suenan en 3D).
"""
import numpy as np, os, sys
sys.path.insert(0, '/work/sfx')
from dsp import *
src = open('/work/sfx/disenar_dbz.py').read()
exec(src[src.index('def zap('):src.index('S={}')])
exec(src[src.index('def forcejeo('):src.index('dch=3.2')])
src3 = open('/work/sfx/disenar_dbz3.py').read()
exec(src3[src3.index('def chisporroteo('):src3.index('S = {}')])
exec(src3[src3.index('def pared('):src3.index('dch = 3.2')])
OUT = '/work/sfx/hechizos'; os.makedirs(OUT, exist_ok=True)


def T(d): return np.arange(int(d*SR))/SR
def pon(n, x, off):
    y = np.zeros(n); o = int(off*SR); m = min(len(x), n-o)
    if m > 0: y[o:o+m] += x[:m]
    return y
def mono(y): return y.mean(1) if y.ndim == 2 else y
def acabar(y, graves=0.5, corte=200, umbral=-24, sala=0.18, dsala=1.4, seed=1):
    y = reverb(y, sala, dsala, seed=seed)
    return mono(master(y, umbral_db=umbral, graves=graves, corte=corte))


# ── Texturas por elemento (todas sostenidas, se recortan con envolventes) ───────────────
def tex_fuego(d, seed, base=110, brillo=1.0):
    t = T(d)
    rug = bp(noise(d, seed), 120, 2400, 2); rug = am(rug, lambda tt: 9+6*np.sin(tt*3), 0.5)
    chas = bp(crackle(d, 90, seed+1), 1500, 8000)*5
    zz = zumbido(lambda tt: base*(1+0.04*np.sin(2*np.pi*5*tt)), d, 5, 0.03, 250, 4000, 4.0)
    return rug*1.0 + chas*0.7*brillo + zz*0.5
def tex_viento(d, seed, giro=0.0, alto=1.0):
    """Aire que pasa: tres bandas de ruido que se mezclan según una curva lenta (sin cortes)."""
    t = T(d); r = noise(d, seed)
    b1 = bp(r, 300*alto, 900*alto, 2); b2 = bp(r, 800*alto, 2500*alto, 2); b3 = bp(r, 2200*alto, 7000*alto, 2)
    m = 0.5+0.5*np.sin(2*np.pi*(0.7+giro)*t + seed % 7)
    y = b1*(1-m)**1.5 + b2*(1-abs(m-0.5)*1.6) + b3*m**1.5*0.8
    y = y + chisporroteo(d, seed+3, 2500, 9000, (14, 30), 0.9)*0.3
    if giro: y = y*(1+0.35*np.sin(2*np.pi*(3+giro*4)*t))
    return y*1.6
def tex_luz(d, seed, base=420, sube=0.0):
    t = T(d)
    z = zumbido(lambda tt: base*(1+sube*tt/max(d, 0.01)), d, 7, 0.012, 500, 8000, 3.0)
    z2 = zumbido(lambda tt: base*2.01*(1+sube*tt/max(d, 0.01)), d, 3, 0.006, 900, 9000, 2.0)
    return z*0.8 + z2*0.35 + chisporroteo(d, seed, 2000, 9000, (16, 32))*0.7
def tex_mente(d, seed, base=330, ratio=1.41, indice=3.0, vaiven=6.0):
    """Metálico y ondulante: FM con relación no armónica + anillo + fase."""
    t = T(d)
    mod = np.sin(2*np.pi*base*ratio*t)*indice*(1+0.3*np.sin(2*np.pi*vaiven*t))
    car = np.sin(2*np.pi*base*t + mod)
    anillo = car*np.sin(2*np.pi*(base*0.5+40*np.sin(2*np.pi*1.3*t))*t)
    y = sat(car*0.6 + anillo*0.6, 2.5)
    y = flanger(y, 1.8, 0.0005, 0.004, mix_=0.6)
    return bp(y, 250, 7000, 2) + chisporroteo(d, seed, 1800, 7000, (20, 40), 0.9)*0.45
def tex_oscuro(d, seed, base=70):
    t = T(d)
    g = rugido(d, 40, 700, (10, 18), seed, 1.3)
    filo = zumbido(lambda tt: base*3*(1+0.02*np.sin(2*np.pi*4*tt)), d, 7, 0.03, 400, 5000, 5.0)
    return g*0.9 + filo*0.55 + chisporroteo(d, seed+2, 1200, 6000, (8, 18))*0.4
TEX = {'Fuego': tex_fuego, 'Viento': tex_viento, 'Luz': tex_luz, 'Mente': tex_mente, 'Oscuro': tex_oscuro}


# ── Partes de un lanzamiento ────────────────────────────────────────────────────────────
def carga(elem, d, seed, fuerza=1.0):
    """Lo que suena durante el gesto: sube de tono y de volumen hasta la salida."""
    if d <= 0.02: return np.zeros(int(0.02*SR))
    t = T(d); n = len(t)
    sube = zumbido(lambda tt: {'Fuego': 160, 'Viento': 300, 'Luz': 380, 'Mente': 260, 'Oscuro': 90}[elem]*(2.2**(tt/d)), d, 5, 0.012, 300, 7000, 3.0)
    x = sube*0.7 + TEX[elem](d, seed)*0.6
    return x*np.clip(t/d, 0, 1)**(1.6/fuerza)
def salida(elem, seed, cola, fuerza=1.0, agudo=1.0):
    """El momento en que sale el hechizo: golpe + «pshiuu» + cola de la textura."""
    d = cola+0.25; t = T(d); n = len(t)
    f0 = {'Fuego': 1800, 'Viento': 2600, 'Luz': 3600, 'Mente': 3000, 'Oscuro': 1500}[elem]*agudo
    f1 = f0*0.22
    silbo = zumbido(lambda tt: f1+(f0-f1)*np.exp(-tt*6/max(cola, 0.2)), d, 3, 0.008, 600, 9000, 2.0)*expdec(n, cola*0.5, 0.002)
    golpe = bp(noise(0.25, seed), 300, 9000, 2)*expdec(int(0.25*SR), 0.05, 0.001)
    cuerpo = TEX[elem](d, seed+7)*expdec(n, cola*0.6, 0.004)
    sub = osc(lambda tt: 90*np.exp(-tt*8)+45, d)*expdec(n, 0.12+0.1*fuerza, 0.002)*0.9*fuerza
    return pon(n, golpe, 0)*1.1 + silbo*0.8 + cuerpo*0.9 + sub
def lanzamiento(elem, seed, retardo, cola, fuerza=1.0, agudo=1.0):
    c = carga(elem, retardo, seed, fuerza)
    s = salida(elem, seed+1, cola, fuerza, agudo)
    n = len(c)+len(s)
    return pon(n, c, 0) + pon(n, s, retardo)


def zona(elem, seed, retardo, dur, fuerza=1.0, cuerpo=None, extra=None):
    """Zona: carga + estallido al abrirse + cuerpo que dura 'dur' + cierre."""
    total = retardo + dur + 0.6; n = int(total*SR)
    c = carga(elem, retardo, seed, fuerza)
    tb = T(dur+0.4)
    env = np.clip(tb/0.08, 0, 1)*np.where(tb < dur, 1.0, np.exp(-(tb-dur)/0.15))
    body = (cuerpo(dur+0.4) if cuerpo else TEX[elem](dur+0.4, seed+3))*env*(0.75+0.25*np.exp(-tb/0.6))
    abre = salida(elem, seed+5, 0.5, fuerza)
    cierre = bp(noise(0.5, seed+9), 200, 5000, 2)*expdec(int(0.5*SR), 0.12, 0.01)*0.5
    y = pon(n, c, 0) + pon(n, abre, retardo)*1.0 + pon(n, body, retardo)*0.8 + pon(n, cierre, retardo+dur)
    if extra is not None: y += extra(n)
    return y


def impacto(elem, seed, grande):
    d = 1.6 if grande else 0.9; n = int(d*SR); t = T(d)
    golpe = bp(noise(d, seed), 300, 9000, 2)*expdec(n, 0.18 if grande else 0.09, 0.001)
    cuerpo = TEX[elem](d, seed+1)*expdec(n, 0.45 if grande else 0.22, 0.002)
    sub = osc(lambda tt: (130 if grande else 170)*np.exp(-tt*7)+40, d)*expdec(n, 0.3 if grande else 0.12, 0.002)
    y = golpe*1.1 + cuerpo*0.9 + sub*(1.2 if grande else 0.7)
    y = acabar(y, graves=0.7 if grande else 0.5, corte=160, sala=0.2, dsala=1.6 if grande else 1.0, seed=seed)
    return forma(y, 0.0, 0.7 if grande else 0.3, 0.3 if grande else 0.15)


# ── Tabla de hechizos (sale de Assets/_SPELLS, 5 oct 2026) ──────────────────────────────
# nombre: (elemento, tipo, retardo, velocidad, daño, duración de zona, caso especial)
H = {
    'BolaFuego':        ('Fuego', 'P', 0.5, 20, 10, 0, None),
    'ChispaIgnea':      ('Fuego', 'P', 0.5, 20, 10, 0, 'chispa'),
    'LlamaAstral':      ('Fuego', 'P', 0.5, 10, 10, 0, 'astral'),
    'Rafaga':           ('Viento', 'P', 0.5, 8, 15, 0, None),
    'Tornado':          ('Viento', 'P', 0.5, 8, 30, 0, 'tornado'),
    'Huracan':          ('Viento', 'P', 0.5, 6, 55, 0, 'huracan'),
    'AuraEstelar':      ('Viento', 'P', 0.5, 10, 30, 0, 'aura'),
    'BolaPrisma':       ('Luz', 'P', 0.5, 18, 25, 0, 'prisma'),
    'EstrellaFugaz':    ('Luz', 'P', 0.5, 16, 14, 0, 'fugaz'),
    'LluviaDeChispas':  ('Luz', 'P', 0.5, 18, 9, 0, 'lluvia'),
    'CorazonEstelar':   ('Luz', 'S', 0.5, 10, 100, 0, None),
    'GarraDelPacto':    ('Mente', 'P', 0.5, 14, 35, 0, 'garra'),
    'DardoMental':      ('Mente', 'P', 0.5, 20, 12, 0, 'dardo'),
    'Eco':              ('Mente', 'P', 0.5, 24, 20, 0, 'eco'),
    'MagoOscuroGolpe':  ('Oscuro', 'P', 0.4, 17, 25, 0, None),
    'PasoSombrio':      ('Mente', 'T', 0.5, 0, 0, 0, None),
    'Levitation':       ('Mente', 'L', 0.15, 0, 10, 0, None),
    'BrisaSanadora':    ('Viento', 'Z', 0.5, 0, 0, 4.0, 'brisa'),
    'Remolino':         ('Viento', 'Z', 0.5, 0, 6, 2.5, 'remolino'),
    'MuroDeFuego':      ('Fuego', 'Z', 0.5, 0, 10, 4.0, 'muro'),
    'TormentaDeFuego':  ('Fuego', 'Z', 0.5, 0, 18, 4.0, 'tormenta'),
    'ChispaIgneaFuego': ('Fuego', 'Z', 0.5, 0, 6, 2.0, 'suelo'),
    'Meteoro':          ('Luz', 'Z', 0.5, 0, 15, 3.0, 'meteoro'),
    'NovaDeLuz':        ('Luz', 'Z', 0.5, 0, 30, 1.2, 'nova'),
    'CupulaEstelar':    ('Luz', 'Z', 0.5, 0, 0, 1.2, 'cupula'),
    'SelloDelPacto':    ('Mente', 'Z', 0.5, 0, 12, 5.0, 'sello'),
    'CadenasDelPacto':  ('Mente', 'Z', 0.5, 0, 4, 3.0, 'cadenas'),
    'JuicioDelPacto':   ('Mente', 'Z', 0.5, 0, 20, 3.0, 'juicio'),
    'MagoOscuroGrieta': ('Oscuro', 'Z', 0.5, 0, 15, 6.0, None),
}


def fuerza_de(dmg): return float(np.clip(0.6 + dmg/40, 0.6, 2.2))
def cola_de(vel): return float(np.clip(0.25 + 6.0/max(vel, 1), 0.4, 1.4))


def disenar(nombre, e, tipo, ret, vel, dmg, zd, caso, seed):
    f = fuerza_de(dmg)
    if tipo == 'P':
        cola = cola_de(vel)
        agudo = 1.0
        if caso in ('dardo', 'eco', 'lluvia', 'chispa'): agudo = 1.3
        if caso in ('garra',): agudo = 0.8
        y = lanzamiento(e, seed, ret, cola, f, agudo)
        n = len(y)
        if caso == 'eco':        # se repite: tres ecos que se apagan
            for k, g in ((0.16, 0.5), (0.32, 0.28), (0.48, 0.15)):
                y = y + pon(n, salida(e, seed+20+int(k*100), cola*0.6, f*0.7, 1.4)*g, ret+k)
        if caso == 'lluvia':     # muchas chispas pequeñas
            rng = np.random.default_rng(seed)
            for k in range(7):
                y = y + pon(n, zap(0.25, 4200+rng.random()*1500, 1200, seed+k)*0.35, ret+0.04+k*0.06)
        if caso == 'fugaz':      # silbido largo que cae (estrella que cruza)
            d = 1.2; s = zumbido(lambda tt: 3200*np.exp(-tt*1.4)+600, d, 3, 0.005, 700, 9000, 2.0)*expdec(int(d*SR), 0.6, 0.01)
            y = pon(max(n, int((ret+d)*SR)), y, 0) + pon(max(n, int((ret+d)*SR)), s*0.6, ret)
        if caso == 'garra':      # zarpazo: tres rasgados rápidos
            for k in range(3):
                y = y + pon(n, bp(noise(0.12, seed+k), 1500, 8000, 2)*expdec(int(0.12*SR), 0.03, 0.001)*0.9, ret+k*0.05)
        if caso in ('tornado', 'huracan', 'aura'):  # viento que sigue soplando mientras viaja
            d = {'tornado': 1.6, 'huracan': 3.0, 'aura': 1.8}[caso]
            g = tex_viento(d, seed+30, giro=0.8 if caso != 'aura' else 0.0, alto=1.3 if caso == 'aura' else 0.8)
            g = g*np.clip(T(d)/0.1, 0, 1)*np.exp(-T(d)/(d*0.6))
            if caso == 'aura': g = g + tex_luz(d, seed+31, 520)*0.4*np.exp(-T(d)/(d*0.5))
            if caso == 'huracan': g = g + rugido(d, 40, 400, (6, 10), seed+32, 1.2)*0.5*np.exp(-T(d)/2)
            m = max(n, int((ret+d)*SR)); y = pon(m, y, 0) + pon(m, g, ret)
        if caso == 'prisma':     # dos tonos que se cruzan (luz que se divide)
            d = 0.9; t = T(d)
            a = zumbido(lambda tt: 700*(1+0.5*tt), d, 3, 0.01, 600, 9000, 2.0)
            b = zumbido(lambda tt: 1100*(1-0.3*tt), d, 3, 0.01, 600, 9000, 2.0)
            y = y + pon(n, (a+b)*expdec(int(d*SR), 0.35, 0.003)*0.5, ret)
        if caso == 'astral':     # fuego con brillo de estrella (es de Will)
            d = 1.0; y = y + pon(n, tex_luz(d, seed+40, 600)*expdec(int(d*SR), 0.4, 0.003)*0.45, ret)
        return acabar(y, graves=0.5+0.1*f, sala=0.18, dsala=1.2+0.3*f, seed=seed)
    if tipo == 'S':               # Corazón Estelar: carga larga y rayo enorme
        y = lanzamiento(e, seed, ret, 1.6, 2.2, 0.9)
        d = 2.2; t = T(d)
        rayo = pared(d, seed+50, 0.6, 260)*np.clip(t/0.05, 0, 1)*np.exp(-np.clip(t-0.8, 0, None)/0.6)
        n = max(len(y), int((ret+d)*SR))
        y = pon(n, y, 0) + pon(n, rayo*0.9, ret) + pon(n, boom_dbz(2.0, seed+51, 150, 38, 0.8)*0.8, ret)
        return acabar(y, graves=0.8, corte=150, sala=0.25, dsala=2.4, seed=seed)
    if tipo == 'T':               # Paso Sombrío: aspiración inversa, «fwip» y aparece
        d = ret; t = T(d)
        asp = TEX['Mente'](d, seed)*(t/d)**2.5 + bp(noise(d, seed+1), 800, 7000, 2)*(t/d)**3
        fwip = zap(0.35, 5000, 700, seed+2)
        pop = bp(noise(0.2, seed+3), 200, 4000, 2)*expdec(int(0.2*SR), 0.04, 0.001)
        sub = osc(lambda tt: 70*np.exp(-tt*10)+40, 0.3)*expdec(int(0.3*SR), 0.08, 0.002)
        n = int((ret+0.9)*SR)
        y = pon(n, asp, 0) + pon(n, fwip, ret-0.02)*0.9 + pon(n, pop, ret+0.14) + pon(n, sub, ret+0.14)
        return acabar(y, graves=0.5, sala=0.15, dsala=1.0, seed=seed)
    if tipo == 'L':               # Levitación: agarre corto y zumbido que se queda flotando
        d = 1.3; t = T(d)
        agarre = carga('Mente', ret, seed, 1.0)
        zum = tex_mente(d, seed+1, base=220, ratio=1.5, indice=1.5, vaiven=3)*np.clip(t/0.05, 0, 1)*np.exp(-t/0.6)
        n = int((ret+d)*SR)
        y = pon(n, agarre, 0) + pon(n, zum, ret) + pon(n, bp(noise(0.2, seed+2), 400, 6000, 2)*expdec(int(0.2*SR), 0.05, 0.001), ret)
        return acabar(y, graves=0.4, sala=0.18, dsala=1.2, seed=seed)
    # ── Zonas ──
    cuerpo = None; extra = None
    if caso == 'brisa':       # curación: aire cálido que sube, sin campanillas
        def cuerpo(d):
            t = T(d)
            aire = tex_viento(d, seed+60, 0, 0.7)*0.7
            calor = lp(sum(osc(lambda tt, k=k: 110*k*(1+0.003*np.sin(2*np.pi*0.5*tt)), d, 'saw') for k in (2, 3, 4)), 1400)*0.25
            return aire + calor*np.clip(t/0.8, 0, 1)
    if caso == 'remolino':
        cuerpo = lambda d: tex_viento(d, seed+61, giro=1.2, alto=1.1)
    if caso == 'muro':
        cuerpo = lambda d: tex_fuego(d, seed+62, 90, 1.2)*1.1
    if caso == 'tormenta':
        cuerpo = lambda d: tex_fuego(d, seed+63, 100, 1.3) + tex_viento(d, seed+64, giro=0.9, alto=0.9)*0.7
    if caso == 'suelo':
        cuerpo = lambda d: tex_fuego(d, seed+65, 140, 1.4)*0.7
    if caso == 'nova':
        def extra(n):
            return pon(n, boom_dbz(1.4, seed+66, 160, 40, 0.5)*0.9, ret+0.02) + pon(n, pared(1.0, seed+67, 1.0, 300)*np.exp(-T(1.0)/0.35), ret)
    if caso == 'cupula':      # barrera: zumbido eléctrico que se cierra alrededor
        cuerpo = lambda d: zumbido(lambda tt: 240*(1+0.02*np.sin(2*np.pi*30*tt)), d, 7, 0.02, 400, 7000, 4.0) + chisporroteo(d, seed+68)*0.6
    if caso == 'meteoro':     # silbido que cae y luego golpe y fuego
        def extra(n):
            d = 1.0; t = T(d)
            cae = zumbido(lambda tt: 2600*np.exp(-tt*2.2)+250, d, 5, 0.01, 300, 8000, 3.0)*np.clip(t/0.1, 0, 1)
            return pon(n, cae*0.8, ret) + pon(n, boom_dbz(2.0, seed+69, 140, 30, 0.9)*1.2, ret+1.0)
        cuerpo = lambda d: tex_fuego(d, seed+70, 120)*np.clip((T(d)-1.0)/0.1, 0, 1)
    if caso == 'sello':       # sello que late: pulsos graves y metálicos
        def cuerpo(d):
            t = T(d)
            return tex_mente(d, seed+71, 180, 1.73, 2.0, 2.0)*(0.6+0.4*np.sin(2*np.pi*1.6*t)**2)
    if caso == 'cadenas':     # tintineo metálico seco de cadenas que se tensan
        def cuerpo(d):
            t = T(d); n = len(t); y = tex_mente(d, seed+72, 260, 2.31, 2.5, 4.0)*0.5
            rng = np.random.default_rng(seed)
            for k in range(int(d*9)):
                golpe = bp(noise(0.06, seed+k), 2500, 9000, 2)*expdec(int(0.06*SR), 0.012, 0.0005)
                y = y + pon(n, golpe*0.9, rng.random()*d)
            return y
    if caso == 'juicio':      # golpe de mazo enorme y zumbido que pesa
        def extra(n):
            return pon(n, boom_dbz(2.2, seed+73, 120, 28, 1.0)*1.3, ret+0.05)
        cuerpo = lambda d: tex_mente(d, seed+74, 140, 1.33, 3.5, 1.2)
    y = zona(e, seed, ret, zd, f, cuerpo, extra)
    if nombre == 'MagoOscuroGrieta':
        n = len(y); y = y + pon(n, boom_dbz(3.0, seed+75, 110, 25, 1.4), ret)
    return acabar(y, graves=0.6, sala=0.2, dsala=1.6, seed=seed)


def forma(y, ret, fin, tau=0.18, carga_min=0.3):
    """El compresor iguala todo: aquí se vuelve a imponer la forma (carga más baja que la salida,
    y el sonido acaba cuando acaba el hechizo)."""
    t = np.arange(len(y))/SR
    g = np.where(t < ret, carga_min+(0.85-carga_min)*(t/max(ret, 1e-3))**1.5, 1.0)
    g = g*np.where(t < fin, 1.0, np.exp(-(t-fin)/tau))
    return y*g


EXTRA_COLA = {'fugaz': 0.6, 'tornado': 0.9, 'aura': 1.0, 'huracan': 2.2, 'eco': 0.5, 'lluvia': 0.3, 'garra': 0.1}


def fin_de(tipo, ret, vel, zd, caso):
    if tipo == 'P': return ret + cola_de(vel)*0.75 + EXTRA_COLA.get(caso, 0.0)
    if tipo == 'S': return ret + 1.8
    if tipo == 'T': return ret + 0.4
    if tipo == 'L': return ret + 0.8
    return ret + zd + 0.15


if __name__ == '__main__':
    solo = sys.argv[1:]
    for i, (nombre, p) in enumerate(H.items()):
        if solo and nombre not in solo: continue
        y = disenar(nombre, *p, seed=1000+i*37)
        e, tipo, ret, vel, dmg, zd, caso = p
        y = forma(y, ret, fin_de(tipo, ret, vel, zd, caso), 0.22 if tipo == 'Z' else 0.16)
        pico = -1.5 if p[1] in ('Z', 'S') else -3.0
        write(f'{OUT}/SFX_Hechizo_{nombre}.wav', y, pico)
        print(nombre, round(len(trim_tail(y))/SR, 2), 's')
    if not solo or 'impactos' in solo:
        for j, e in enumerate(TEX):
            for g in (False, True):
                y = impacto(e, 5000+j*11+g, g)
                write(f'{OUT}/SFX_Impacto_{e}_{"Grande" if g else "Pequeno"}.wav', y, -3.0)
                print('Impacto', e, 'Grande' if g else 'Pequeno', round(len(trim_tail(y))/SR, 2), 's')
        # Levitación: lo que suena cuando el objeto lanzado choca
        y = impacto('Mente', 5099, True)
        write(f'{OUT}/SFX_Impacto_Levitacion.wav', y, -3.0); print('Impacto Levitacion')
