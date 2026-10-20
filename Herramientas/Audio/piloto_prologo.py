"""Piloto: remezcla del clímax del prólogo con la receta Slayers.

Genera tres ficheros comparables en `Herramientas/Audio/Piloto/`:

  antes.wav          la mezcla de hoy: voz seca (mono + normalización a pico de
                     Unity), música duckeada -8 dB durante la plegaria, coro a
                     0,3, sin respiro, sin capas, sin limiter.
  despues_diseno.wav la receta Slayers pero con el bus de hoy (sin limiter):
                     aísla el cambio de diseño.
  despues.wav        la receta + la mezcla nueva (compresor de bus + limiter a
                     -1 dBTP): el objetivo final.

Además escribe `SFX_Artefacto_Shing.wav`, el sonido nuevo del "destello" del
artefacto (brillante, sin grave) que usa la receta.

Los tiempos son los reales del guion horneado (`Prologo_UltimaNoche_Horneado.asset`),
con el origen de la ventana en t = 160 s de la secuencia.

Uso:
    python Herramientas/Audio/piloto_prologo.py
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsp_audio import (  # noqa: E402
    SR, colocar, compresor, decaer, decodificar, db_a_ganancia, eco_slap,
    fundidos, limitador, lufs, normalizar_lufs, normalizar_pico, paso_alto,
    paso_bajo, banda, pico, reverberacion, retardo, rms_maximo, rampa_ganancia,
    saturar, true_peak, escribir, ganancia_db,
)

RAIZ = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SALIDA = os.path.join(RAIZ, "Herramientas", "Audio", "Piloto")

V = os.path.join(RAIZ, "Assets", "Resources", "Voices", "es")
SFX = os.path.join(RAIZ, "Assets", "Audio", "SFX_Prologo")
DIS = os.path.join(SFX, "Disenados")
MUS = os.path.join(RAIZ, "Assets", "Audio", "Music")
FREE = os.path.join(RAIZ, "Assets", "Audio", "FREE SOUND PACK_TM(355)")

DURACION = 62.0
MUSICA_INICIO = 138.0   # segundo del tema "El Ultimo Duelo" que arranca la ventana

# ── guion horneado (relativo a t=160 s de la secuencia) ────────────────────────
MAGO = 3.19
PLEGARIA = {
    1: (11.15, "PROLOGO_PLEGARIA_01"),
    2: (19.30, "PROLOGO_PLEGARIA_02"),
    3: (29.04, "PROLOGO_PLEGARIA_03"),
    4: (35.26, "PROLOGO_PLEGARIA_04"),
    5: (41.00, "PROLOGO_PLEGARIA_05"),
    6: (47.11, "PROLOGO_PLEGARIA_06"),
}
RESPIRO = 49.20
GRITO = 50.30
IMPACTO = 51.40
COLA = 57.50


# ── utilidades ─────────────────────────────────────────────────────────────────

def buf(n=DURACION):
    return np.zeros((int(n * SR), 2), dtype=np.float32)


def cargar(ruta, mono=False):
    return decodificar(ruta, SR, mono=mono)


def buscar(nombre, carpeta):
    """Busca un fichero por nombre dentro de una carpeta (la música está repartida
    en subcarpetas: Cinematicas, Combate, Zonas, Reserva, _Archivo)."""
    for base, _, ficheros in os.walk(carpeta):
        if nombre in ficheros:
            return os.path.join(base, nombre)
    raise FileNotFoundError(f"'{nombre}' no está en {carpeta}")


def pico_normalizado(x):
    """Lo que hace Unity al importar: downmix a mono y pico a -1 dBFS."""
    m = x.mean(axis=1) if x.ndim > 1 else x
    p = np.max(np.abs(m))
    if p > 0:
        m = m * (db_a_ganancia(-1.0) / p)
    return np.column_stack([m, m])


def duck(bus, t0, t1, db, rampa=0.3):
    """Baja un bus entre t0 y t1 (con rampas)."""
    i0, i1 = int(t0 * SR), int(t1 * SR)
    i0 = max(0, min(i0, len(bus)))
    i1 = max(i0, min(i1, len(bus)))
    if i1 <= i0:
        return bus
    g = db_a_ganancia(db)
    n = int(rampa * SR)
    n = max(1, min(n, (i1 - i0) // 2))
    bus[i0:i0 + n] *= np.linspace(1, g, n)[:, None]
    bus[i0 + n:i1 - n] *= g
    if i1 - n > i0 + n:
        bus[i1 - n:i1] *= np.linspace(g, 1, n)[:, None]
    return bus


def rampa(bus, t0, dur, desde_db, hasta_db):
    if t0 + dur > DURACION:
        return bus
    rampa_ganancia(bus, t0, desde_db, hasta_db, dur)
    return bus


def lowpass_burst(bus, t0, dur, fc=4000):
    """Estrechar la mezcla (quitar brillo) durante un instante."""
    i0, i1 = int(t0 * SR), int(t0 * SR) + int(dur * SR)
    i1 = min(i1, len(bus))
    if i1 <= i0:
        return bus
    bus[i0:i1] = paso_bajo(bus[i0:i1], fc)
    return bus


# ── elementos de sonido nuevos ─────────────────────────────────────────────────

def shing_artefacto(dur=2.2):
    """'Destello' de artefacto: brillante, metálico, SIN grave (como las muñequeras)."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    out = np.zeros((n, 2))
    # transitorio: parciales inarmónicos con caída muy rápida
    for f, tau, amp in [(3150, 0.035, 1.0), (4720, 0.028, 0.85), (6180, 0.022, 0.7),
                        (8340, 0.016, 0.5), (11180, 0.012, 0.35)]:
        e = np.exp(-t / tau)
        onda = np.sin(2 * np.pi * f * t + 0.7 * np.sin(2 * np.pi * 7 * t)) * e * amp
        out[:, 0] += onda
        out[:, 1] += onda * 0.94
    # brillo: ruido de banda alta con ataque lento
    rng = np.random.default_rng(3)
    aire = banda(rng.standard_normal(n), 6000, 13000)
    aire *= (1 - np.exp(-t / 0.22)) * np.exp(-t / 1.1)
    out[:, 0] += aire * 0.35
    out[:, 1] += np.roll(aire, 37) * 0.32
    # cuerpo medio (300 Hz), sin graves por debajo de 150 Hz
    cuerpo = np.sin(2 * np.pi * 305 * t) * np.exp(-t / 0.22) * 0.18
    out[:, 0] += cuerpo
    out[:, 1] += cuerpo
    out = paso_alto(out, 150)
    p = np.max(np.abs(out))
    return (out / p * db_a_ganancia(-3.0)).astype(np.float32)


def sub_drop(dur=2.0, f_ini=72.0, f_fin=34.0):
    """Caída de sub (40-80 Hz) que entra TARDE y crece, como en la referencia."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    fase = 2 * np.pi * np.cumsum(np.linspace(f_ini, f_fin, n)) / SR
    onda = np.sin(fase) * (1 - np.exp(-t / 0.05)) * np.exp(-t / (dur / 3.0))
    onda = np.tanh(onda * 1.8) * 0.7
    return np.column_stack([onda, onda]).astype(np.float32)


def transitorio(dur=0.12):
    """Crack de 2-5 kHz: el 'golpe' fino del impacto."""
    n = int(dur * SR)
    rng = np.random.default_rng(7)
    x = normalizar_pico(banda(rng.standard_normal(n), 2000, 6000), -6.0)
    x *= np.exp(-np.arange(n) / (0.012 * SR))
    return np.column_stack([x, x]).astype(np.float32)


def cola_impacto(dur=5.0):
    """Cola larga: retumbo grave + escombros + aire que se apaga."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    rng = np.random.default_rng(21)
    retumbo = normalizar_pico(banda(rng.standard_normal(n), 28, 90), -6.0) * np.exp(-t / 1.9)
    escombros = normalizar_pico(banda(rng.standard_normal(n), 700, 6000), -6.0) * np.exp(-t / 0.9) * 0.5
    aire = normalizar_pico(banda(rng.standard_normal(n), 4000, 11000), -8.0) * np.exp(-t / 0.45) * 0.35
    x = retumbo * 1.4 + escombros + aire
    x = np.column_stack([x, np.roll(x, 211) * 0.95])
    return (x * 0.6).astype(np.float32)


# ── construcción de las mezclas ────────────────────────────────────────────────

def voz_juego(ruta):
    """La voz tal y como suena hoy: mono + pico a -1 dBFS (import de Unity)."""
    return pico_normalizado(cargar(ruta))


def voz_conjuro(ruta, objetivo=-18.0, preset="Plegaria"):
    """Voz con la cadena real del juego: la del preset de voz (reverb + eco slap).

    Plegaria: reverb de 3,2 s y eco de 260 ms (el cántico).
    Epico: reverb de 6,5 s y eco de 340 ms (el grito final).
    """
    x = cargar(ruta, mono=True)
    x = normalizar_lufs(x, objetivo)
    x = paso_alto(x, 90)
    x = pico(x, 300, -2.0)
    x = pico(x, 4000, 3.0)
    x = compresor(x, SR, umbral_db=-20, ratio=3.0, ataque=0.010, release=0.120)
    x = saturar(x, 0.12)
    if preset == "Epico":
        x = reverberacion(x, decay=6.5, predelay=0.04, wet=0.45, hp=250)
        x = eco_slap(x, retardo_ms=340.0, decaimiento=0.40, humedo=0.32)
    else:
        x = reverberacion(x, decay=3.2, predelay=0.04, wet=0.30, hp=250)
        x = eco_slap(x, retardo_ms=260.0, decaimiento=0.32, humedo=0.25)
    x = np.column_stack([x, x])
    return fundidos(x, 0.02, 0.25)


def construir(estilo):
    """estilo: 'antes' | 'despues_diseno' | 'despues'."""
    nuevo = estilo != "antes"
    musica = buf()
    voz = buf()
    sfx = buf()
    coro = buf()

    # ── música ────────────────────────────────────────────────────────────────
    pista = cargar(buscar("El Ultimo Duelo.mp3", MUS))
    i0 = int(MUSICA_INICIO * SR)
    seg = pista[i0:i0 + int(DURACION * SR)]
    if len(seg) < int(DURACION * SR):
        seg = np.pad(seg, ((0, int(DURACION * SR) - len(seg)), (0, 0)))
    musica[:len(seg)] = seg

    # ── voces ────────────────────────────────────────────────────────────────
    if nuevo:
        colocar(voz, voz_conjuro(os.path.join(V, "PROLOGO_MAGO_VALLE.mp3")), MAGO, -1.0)
        for t, clave in PLEGARIA.values():
            colocar(voz, voz_conjuro(os.path.join(V, clave + ".mp3")), t, 0.0)
        # el cántico va con la voz de plegaria y el grito con la épica (el eco largo del conjuro)
        colocar(voz, voz_conjuro(os.path.join(V, "PROLOGO_HECHIZO.mp3"), -15.0, "Epico"), GRITO, 1.5)
    else:
        colocar(voz, voz_juego(os.path.join(V, "PROLOGO_MAGO_VALLE.mp3")), MAGO, -1.0)
        for t, clave in PLEGARIA.values():
            colocar(voz, voz_juego(os.path.join(V, clave + ".mp3")), t, 0.0)
        colocar(voz, voz_juego(os.path.join(V, "PROLOGO_HECHIZO.mp3")), GRITO, 1.5)

    # ── capas de SFX ──────────────────────────────────────────────────────────
    colocar(sfx, cargar(os.path.join(DIS, "SFX_Golpe_Mago.wav")), 3.3, -4.0)
    colocar(sfx, cargar(os.path.join(DIS, "SFX_Despegue.wav")), 10.5, -6.0)
    colocar(sfx, cargar(os.path.join(DIS, "SFX_Hechizo_Grande.wav")), 11.0, -8.0)
    colocar(sfx, cargar(os.path.join(SFX, "SFX_AgujeroNegro_Carga.mp3")), 10.8, -8.0)
    bucle = cargar(os.path.join(SFX, "SFX_AgujeroNegro_Bucle.mp3"))
    for k in range(12):
        colocar(sfx, bucle, 10.8 + k * (len(bucle) / SR), -12.0)
    for t in (22.0, 33.5, 44.5):
        colocar(sfx, cargar(os.path.join(DIS, "SFX_Ki_Carga.wav")), t, -6.0)
    colocar(sfx, cargar(os.path.join(DIS, "SFX_Ki_Carga.wav")), 48.2, -3.0)
    colocar(sfx, cargar(os.path.join(DIS, "SFX_Proteccion_Absoluta.wav")), GRITO + 0.5, -6.0)
    colocar(sfx, cargar(os.path.join(DIS, "SFX_Lucha_Final.wav")), IMPACTO - 0.4, -5.0)
    colocar(sfx, cargar(os.path.join(DIS, "SFX_Explosion_Final_DBZ.wav")), IMPACTO + 0.3, -5.0)
    colocar(sfx, cargar(os.path.join(FREE, "Horror(13)", "Cinematic_Hits-014.wav")), COLA - 4.0, -6.0)
    colocar(sfx, cargar(os.path.join(DIS, "SFX_Silencio_Blanco.wav")), COLA, -8.0)

    # coro
    coro_p = cargar(os.path.join(SFX, "SFX_Prologo_CoroBucle.wav"))
    if nuevo:
        for k in range(10):
            colocar(coro, coro_p, 11.0 + k * (len(coro_p) / SR), -13.0)
        rampa(coro, 11.0, 37.0, -13.0, -4.0)
        decaer(coro, -40.0, 1.5, 48.6)
    else:
        for k in range(10):
            colocar(coro, coro_p, 11.0 + k * (len(coro_p) / SR), -10.0)
        decaer(coro, -40.0, 1.5, 48.6)

    # ── el "destello" del artefacto (solo en la versión nueva) ─────────────────
    if nuevo:
        # hueco de 0,25 s antes del destello
        duck(musica, 10.55, 10.95, -12.0, rampa=0.12)
        duck(sfx, 10.55, 10.95, -12.0, rampa=0.12)
        colocar(sfx, shing_artefacto(), 10.95, -4.0)

    # ── mezcla de buses y duckings ───────────────────────────────────────────
    if nuevo:
        # la música NO se duckea durante el cántico: crece
        rampa(musica, 11.0, 37.0, 0.0, 2.0)
        rampa(coro, 11.0, 37.0, -13.0, -4.0)
        # respiro antes del golpe
        duck(musica, RESPIRO, RESPIRO + 1.0, -10.0, rampa=0.25)
        duck(coro, RESPIRO, RESPIRO + 1.0, -16.0, rampa=0.25)
        duck(sfx, RESPIRO, RESPIRO + 1.0, -8.0, rampa=0.25)
        duck(voz, RESPIRO, RESPIRO + 1.0, -6.0, rampa=0.25)
        # y el golpe: música al máximo + capas nuevas
        rampa(musica, RESPIRO + 1.0, 0.6, -10.0, 1.0)
        rampa(coro, RESPIRO + 1.0, 0.6, -16.0, -12.0)
        colocar(sfx, cargar(os.path.join(FREE, "Horror(13)", "Cinematic_Hits-013.wav")), GRITO, -5.0)
        colocar(sfx, sub_drop(), GRITO + 0.05, -6.0)
        colocar(sfx, transitorio(), GRITO + 0.02, -7.0)
        colocar(sfx, cola_impacto(), IMPACTO + 0.3, -9.0)
        # la cola se lleva el final
        decaer(musica, -14.0, 6.0, COLA - 5.0)
    else:
        # hoy: la música se esconde durante la plegaria y vuelve en el grito
        duck(musica, 11.0, 47.5, -8.0, rampa=0.3)
        duck(sfx, 11.0, 47.5, -4.0, rampa=0.3)
        duck(musica, 48.0, 49.6, -10.0, rampa=0.3)
        duck(sfx, 48.0, 49.6, -6.0, rampa=0.3)
        rampa(musica, 49.6, 0.6, -10.0, 0.0)
        decaer(musica, -30.0, 2.0, COLA)

    mezcla = musica + voz + sfx + coro

    # ── master ───────────────────────────────────────────────────────────────
    if estilo == "despues":
        # master: a -14 LUFS con margen y limiter a -1 dBTP (la compresión por bus
        # va en el mixer de Unity, no aquí: aquí solo el master)
        mezcla = normalizar_lufs(mezcla, -14.0)
        mezcla = limitador(mezcla, -1.0)
    return mezcla.astype(np.float32)


def linea_tiempo(x, paso=2.0):
    """RMS en ventanas de `paso` segundos: el 'arco' de la mezcla."""
    n = int(paso * SR)
    filas = []
    for i in range(0, len(x) - n, n):
        seg = x[i:i + n]
        filas.append((i / SR, 20 * np.log10(max(np.sqrt(np.mean(seg ** 2)), 1e-9))))
    return filas


def main():
    os.makedirs(SALIDA, exist_ok=True)
    # el nuevo sonido del artefacto, para usarlo en el juego
    escribir(os.path.join(SALIDA, "SFX_Artefacto_Shing.wav"), shing_artefacto())

    informe = []
    for estilo, nombre in [("antes", "antes.wav"),
                            ("despues_diseno", "despues_diseno.wav"),
                            ("despues", "despues.wav")]:
        x = construir(estilo)
        ruta = os.path.join(SALIDA, nombre)
        escribir(ruta, x)
        print(f"{nombre:<22} LUFS {lufs(x):6.1f}  pico {true_peak(x):6.1f} dBTP  "
              f"punch {rms_maximo(x):6.1f} dBFS")
        informe.append((nombre, linea_tiempo(x)))

    # el arco de cada mezcla, para ver el contraste
    print("\nRMS por ventana de 2 s (dBFS):")
    print(f"{'t':>5} " + "".join(f"{n.replace('.wav',''):>14}" for n, _ in informe))
    n_vent = min(len(f) for _, f in informe)
    for i in range(n_vent):
        t = informe[0][1][i][0]
        print(f"{t:5.1f} " + "".join(f"{f[i][1]:14.1f}" for _, f in informe))

    print("\nEn " + SALIDA)


if __name__ == "__main__":
    main()
