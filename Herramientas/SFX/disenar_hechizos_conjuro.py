"""SFX de hechizos y combate con el estilo del conjuro del prólogo (13 nov 2026, INC-1139).

Raúl: «ese es el estilillo que quiero darle a TODOS los sfx del juego». El estilo sale de los sonidos
del choque final del prólogo (disenar_estilo_conjuro.py), medidos sobre la referencia del Dragon Slave
(solo como regla de medir, sin usar su audio):
  - la carga SUBE de tono y de volumen hasta que sale el hechizo (aspiración + pared + chisporroteo);
  - la salida es un chasquido brillante, un golpe en 170–300 Hz y un «canto» estrecho de 0,7–1,6 kHz
    que también sube; nada de «pshiuu» que cae;
  - el impacto: chasquido de banda ancha, golpe en 170–300 Hz y un rugido corto que se aclara en vez
    de apagarse hacia el grave;
  - la luz y la mente llevan además una campanada aguda («plin») muy corta.
Reutiliza la tabla de hechizos, las texturas por elemento y los casos especiales de
disenar_hechizos.py (mismos tiempos de juego: retardo, velocidad, duración de zona); solo cambian
la carga, la salida y el impacto. Mismos nombres de archivo → mismos GUID en Unity.
Además: capas por elemento (transitorio, sub, cola) y los sonidos de combate (bloqueo, curación,
daño, caída, fallo, choque de hechizos y ataques de equipo).
"""
import numpy as np, os, sys
sys.path.insert(0, '/work/sfx')
src = open('/work/sfx/disenar_hechizos.py').read()
exec(src[:src.index("if __name__ == '__main__':")])
OUT = '/work/sfx/hechizos_conjuro'; os.makedirs(OUT, exist_ok=True)

CANTO = {'Fuego': 900, 'Viento': 1300, 'Luz': 1500, 'Mente': 1200, 'Oscuro': 700}
PARED = {'Fuego': 200, 'Viento': 300, 'Luz': 360, 'Mente': 280, 'Oscuro': 120}


def plin(d, f=2637, fuerza=1.0):
    return (fm_bell(f, d, 3.5, 2.5, 0.5)*0.7 + fm_bell(f*1.5, d, 2.0, 2.0, 0.35)*0.45)*fuerza


def chasquido(d, seed, tau=0.06):
    return bp(noise(d, seed), 1500, 14000, 2)*expdec(int(d*SR), tau, 0.0004)


def golpe(d, f0=280, f1=150, tau=0.15):
    return osc(lambda tt: (f0-f1)*np.exp(-tt*9)+f1, d)*expdec(int(d*SR), tau, 0.002)


# ── La carga: todo sube hasta la salida ─────────────────────────────────────────────────
def carga(elem, d, seed, fuerza=1.0):
    if d <= 0.02: return np.zeros(int(0.02*SR))
    t = T(d); k = np.clip(t/d, 0, 1)
    pared_ = zumbido(lambda tt: PARED[elem]*(2.6**np.clip(tt/d, 0, 1)), d, 7, 0.015, 300, 8000, 3.5)
    aire = hp(sweep_filter(noise(d, seed+11), 400, 10000, 'low', curve=0.6), 300)
    chis = chisporroteo(d, seed+12, 2500, 10000, (18, 40))
    x = pared_*0.7 + aire*0.8 + chis*0.5 + TEX[elem](d, seed)*0.35
    return x*(0.05 + 0.95*k**(1.4/fuerza))


# ── La salida: chasquido, golpe medio y canto que sube ──────────────────────────────────
def salida(elem, seed, cola, fuerza=1.0, agudo=1.0):
    d = cola+0.3; t = T(d); n = len(t)
    f0 = CANTO[elem]*agudo
    canto = zumbido(lambda tt: f0*(1+0.25*np.clip(tt/max(cola, 0.2), 0, 1)), d, 3, 0.006, 600, 8000, 2.0)*expdec(n, cola*0.6, 0.003)
    pared_ = zumbido(lambda tt: f0/4*(1+0.25*np.clip(tt/max(cola, 0.2), 0, 1)), d, 7, 0.02, 400, 9000, 4.0)*expdec(n, cola*0.5, 0.003)
    cuerpo = TEX[elem](d, seed+7)*expdec(n, cola*0.55, 0.004)
    chis = chisporroteo(d, seed+8, 2000, 10000, (16, 36))*expdec(n, cola*0.4, 0.002)
    y = pon(n, chasquido(0.4, seed, 0.05), 0)*1.2 + canto*0.55 + pared_*0.7 + cuerpo*0.6 + chis*0.5
    y = y + pon(n, golpe(0.4, 300, 160, 0.08+0.06*fuerza), 0)*0.8*fuerza
    if elem in ('Luz', 'Mente'):
        y = y + pon(n, plin(0.6, 2637 if elem == 'Luz' else 2349, 0.35), 0)
    return y


# ── El impacto: chasquido, golpe y rugido corto que se aclara ───────────────────────────
def impacto(elem, seed, grande):
    d = 1.8 if grande else 0.9; n = int(d*SR); t = T(d)
    plano = 0.45 if grande else 0.12                                            # lo que se sostiene antes de irse
    env = np.where(t < plano, 1.0, np.exp(-(t-plano)/(0.45 if grande else 0.18)))
    rug = rugido(d, 90, 3000, (20, 34), seed+2, 1.0)
    rug = sweep_filter(rug, 1300, 4500 if grande else 3500, 'low', curve=0.8)
    pared_ = zumbido(lambda tt: (240 if grande else 300)*(1+0.2*np.clip(tt/d, 0, 1)), d, 7, 0.025, 300, 7000, 4.0)
    cuerpo = (rug*0.7 + pared_*0.7 + TEX[elem](d, seed+1)*0.5 +
              chisporroteo(d, seed+3, 1500, 9500, (14, 34))*0.5)*env*np.clip(t/0.02, 0, 1)
    y = (pon(n, chasquido(0.6, seed, 0.09 if grande else 0.05), 0)*1.3 + cuerpo +
         pon(n, golpe(0.8, 280 if grande else 320, 130 if grande else 170, 0.3 if grande else 0.12), 0)*(1.2 if grande else 0.8))
    if elem in ('Luz', 'Mente'):
        y = y + pon(n, plin(1.0, 2637 if elem == 'Luz' else 2349, 0.4 if grande else 0.3), 0.01)
    y = acabar(y, graves=0.7 if grande else 0.55, corte=150, sala=0.22, dsala=1.6 if grande else 1.0, seed=seed)
    return forma(y, 0.0, (plano+0.5) if grande else (plano+0.2), 0.3 if grande else 0.12)


# ── Capas por elemento (eventos por capas de AudioEventSO) ─────────────────────────────
def capas(elem, seed):
    tr = chasquido(0.35, seed, 0.04)*1.2 + pon(int(0.35*SR), plin(0.35, 2637, 0.25) if elem in ('Luz', 'Mente') else np.zeros(1), 0)
    sub = golpe(0.9, 260, 120, 0.3)*1.1
    d = 1.6; t = T(d)
    cola = (sweep_filter(rugido(d, 90, 3000, (20, 34), seed+1, 1.0), 1200, 4000, 'low', curve=0.8)*0.7 +
            TEX[elem](d, seed+2)*0.5)*np.where(t < 0.3, 1.0, np.exp(-(t-0.3)/0.4))*np.clip(t/0.05, 0, 1)
    return (acabar(tr, graves=0.3, sala=0.12, dsala=0.6, seed=seed),
            acabar(sub, graves=1.0, corte=120, sala=0.1, dsala=0.6, seed=seed+1),
            acabar(cola, graves=0.6, sala=0.25, dsala=1.4, seed=seed+2))


# ── Combate (no son hechizos) ───────────────────────────────────────────────────────────
def combate():
    S = {}
    # Bloqueo/contraataque con el escudo: «bzzt-CHANK» brillante que sube
    d = 1.1; t = T(d); n = len(t)
    barrera = zumbido(lambda tt: 320*(1+0.3*np.clip(tt/0.4, 0, 1)), d, 5, 0.02, 500, 8000, 4.0)*expdec(n, 0.3, 0.001)
    y = pon(n, chasquido(0.4, 901, 0.05), 0)*1.3 + barrera + pon(n, golpe(0.5, 320, 180, 0.1), 0) + plin(d, 3136, 0.4)
    S['Combate_Bloqueo'] = acabar(y, graves=0.5, sala=0.2, dsala=1.0, seed=901)
    # Curación: aire cálido y un acorde de campanas que sube
    d = 1.6; t = T(d); n = len(t)
    acorde = sum(pon(n, fm_bell(f, 1.4, 2.0, 1.5, 0.7), k*0.09) for k, f in enumerate((1047, 1319, 1568, 2093)))
    aire = hp(sweep_filter(noise(d, 902), 600, 9000, 'low', curve=0.7), 400)*np.clip(t/0.3, 0, 1)*np.exp(-t/0.7)
    y = acorde*0.6 + aire*0.5 + chisporroteo(d, 903, 3000, 10000, (18, 36))*0.3*np.exp(-t/0.6)
    S['Combate_Curacion'] = acabar(y, graves=0.3, sala=0.3, dsala=1.6, seed=902)
    # Daño recibido: golpe seco medio y chasquido, corto
    d = 0.5; n = int(d*SR)
    y = chasquido(d, 904, 0.03)*0.9 + golpe(d, 260, 140, 0.08)*1.2 + bp(noise(d, 905), 300, 2500, 2)*expdec(n, 0.06, 0.001)*0.6
    S['Combate_Dano'] = acabar(y, graves=0.6, sala=0.1, dsala=0.6, seed=904)
    # Caída: golpe grande que baja y se queda en un zumbido que se apaga (lo único que cae, a propósito)
    d = 2.2; t = T(d); n = len(t)
    zz = zumbido(lambda tt: 300*np.exp(-tt*0.8)+90, d, 5, 0.02, 200, 5000, 3.0)*expdec(n, 0.8, 0.003)
    y = chasquido(d, 906, 0.08)*0.9 + golpe(d, 220, 70, 0.5)*1.3 + zz*0.6
    S['Combate_Caida'] = acabar(y, graves=0.8, corte=140, sala=0.3, dsala=2.0, seed=906)
    # Hechizo que falla: chispa que se ahoga (dos chasquidos y un «fzzt» que se corta)
    d = 0.6; t = T(d); n = len(t)
    fz = chisporroteo(d, 907, 1500, 7000, (24, 40))*np.exp(-t/0.15) + zumbido(lambda tt: 500-250*np.clip(tt/0.3, 0, 1), d, 3, 0.02, 300, 4000, 3.0)*np.exp(-t/0.12)*0.6
    y = fz + pon(n, chasquido(0.2, 908, 0.02), 0)*0.6 + pon(n, chasquido(0.2, 909, 0.02), 0.12)*0.4
    S['Combate_HechizoFalla'] = acabar(y, graves=0.4, sala=0.12, dsala=0.6, seed=907)
    # Choque de dos hechizos: el «plin» del prólogo en pequeño + chasquido y golpe
    d = 1.2; t = T(d); n = len(t)
    y = plin(d, 2637, 0.8) + pon(n, chasquido(0.5, 910, 0.06), 0)*1.1 + pon(n, golpe(0.6, 300, 170, 0.12), 0)*0.8
    S['Combate_ChoqueDeHechizos'] = acabar(y, graves=0.5, sala=0.25, dsala=1.4, seed=910)
    return S


def equipo():
    """Ataques de equipo: carga que sube, salida y gran impacto (los mismos ingredientes en grande)."""
    S = {}
    for nombre, elem, ret, extra in (('Meteoros', 'Luz', 1.2, 'cae'), ('AgujeroNegro', 'Oscuro', 1.4, None),
                                     ('Trio', 'Luz', 1.6, None), ('Liam', 'Oscuro', 1.0, None), ('Estela', 'Viento', 1.0, None)):
        seed = 920 + len(S)*13
        c = carga(elem, ret, seed, 1.6); s = salida(elem, seed+1, 1.4, 2.0, 1.0)
        n = len(c)+len(s); y = pon(n, c, 0) + pon(n, s, ret)
        if extra == 'cae':      # las estrellas que llegan del cielo: silbidos que bajan y golpes
            for k in range(4):
                d = 0.7; cae = zumbido(lambda tt: 2600*np.exp(-tt*2.5)+400, d, 3, 0.01, 400, 9000, 2.5)*expdec(int(d*SR), 0.35, 0.01)
                y = pon(n+int(1.2*SR), y, 0) if len(y) < n+int(1.2*SR) else y
                y = y + pon(len(y), cae*0.5, ret+0.2+k*0.22)
        S['Equipo_'+nombre] = acabar(y, graves=0.7, corte=150, sala=0.25, dsala=2.0, seed=seed)
        S['Equipo_'+nombre] = forma(S['Equipo_'+nombre], ret, ret+1.8, 0.4)
    # Impacto de equipo: el estallido del prólogo en pequeño (chasquido, golpe y rugido plano 1,2 s que se aclara)
    d = 3.0; t = T(d); n = len(t)
    rug = sweep_filter(rugido(d, 90, 3000, (20, 34), 960, 1.0), 1100, 4500, 'low', curve=0.7)
    pared_ = zumbido(lambda tt: 240+60*np.clip(tt/2.5, 0, 1), d, 7, 0.025, 300, 7000, 4.0)
    cuerpo = (rug*0.8 + pared_*0.8 + chisporroteo(d, 961, 1500, 9500, (14, 34))*0.6)*np.where(t < 1.2, 1.0, np.exp(-(t-1.2)/0.6))
    y = pon(n, chasquido(0.7, 962, 0.12), 0)*1.3 + pon(n, golpe(1.5, 260, 120, 0.6), 0)*1.2 + cuerpo*np.clip((t-0.03)/0.2, 0, 1)
    S['Equipo_Impacto'] = acabar(y, graves=0.75, corte=150, sala=0.3, dsala=2.5, seed=962)
    # Bucle del trío: rugido sostenido sin principio ni final marcados (se repite)
    d = 4.0; t = T(d)
    b = sweep_filter(rugido(d, 90, 3000, (20, 34), 963, 1.0), 1500, 1500, 'low') + zumbido(lambda tt: 260+0*tt, d, 7, 0.025, 300, 6000, 3.5)*0.6
    b = acabar(b, graves=0.6, sala=0.2, dsala=1.0, seed=963)
    m = int(0.25*SR); b[:m] = b[:m]*np.linspace(0, 1, m) + b[-m:]*np.linspace(1, 0, m); b = b[:-m]
    S['Equipo_Trio_Bucle'] = b
    return S


if __name__ == '__main__':
    solo = sys.argv[1:]
    for i, (nombre, p) in enumerate(H.items()):
        if solo and nombre not in solo: continue
        y = disenar(nombre, *p, seed=1000+i*37)
        e, tipo, ret, vel, dmg, zd, caso = p
        y = forma(y, ret, fin_de(tipo, ret, vel, zd, caso), 0.22 if tipo == 'Z' else 0.16)
        write(f'{OUT}/SFX_Hechizo_{nombre}.wav', y, -1.5 if tipo in ('Z', 'S') else -3.0)
        print(nombre, round(len(trim_tail(y))/SR, 2), 's')
    if solo and 'resto' not in solo: sys.exit()
    # Amanecer (el de Will): carga larga de luz, salida enorme con campanas y el mismo impacto grande con un acorde
    c = carga('Luz', 1.1, 7001, 2.0); s = salida('Luz', 7002, 1.6, 2.2, 1.1)
    n = len(c)+len(s); y = pon(n, c, 0) + pon(n, s, 1.1)
    y = y + pon(n, sum(pon(int(2.0*SR), fm_bell(f, 2.0, 2.0, 1.5, 1.0), k*0.07) for k, f in enumerate((1319, 1568, 1976, 2637)))*0.35, 1.1)
    y = forma(acabar(y, graves=0.7, corte=150, sala=0.3, dsala=2.4, seed=7001), 1.1, 2.9, 0.4)
    write(f'{OUT}/SFX_Hechizo_Amanecer.wav', y, -1.0); print('Amanecer')
    y = impacto('Luz', 7003, True)
    write(f'{OUT}/SFX_Impacto_Amanecer.wav', y, -1.0)
    for j, e in enumerate(TEX):
        for g in (False, True):
            y = impacto(e, 5000+j*11+g, g)
            write(f'{OUT}/SFX_Impacto_{e}_{"Grande" if g else "Pequeno"}.wav', y, -3.0)
            print('Impacto', e, 'Grande' if g else 'Pequeno')
    write(f'{OUT}/SFX_Impacto_Levitacion.wav', impacto('Mente', 5099, True), -3.0)
    for j, e in enumerate(TEX):
        tr, sub, cola = capas(e, 6000+j*7)
        write(f'{OUT}/Capa_Transitorio_{e}.wav', tr, -3.0)
        write(f'{OUT}/Capa_Sub_{e}.wav', sub, -3.0)
        write(f'{OUT}/Capa_Cola_{e}.wav', cola, -3.0)
    PICO = {'Combate_Curacion': -5.0, 'Equipo_Trio_Bucle': -6.0}   # suaves: la curación y el bucle van debajo
    for k, y in {**combate(), **equipo()}.items():
        write(f'{OUT}/SFX_{k}.wav', y, PICO.get(k, -1.5))
        print(k)
