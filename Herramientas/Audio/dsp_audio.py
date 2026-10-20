"""Utilidades de audio para el proyecto (medición y mezcla).

Todo en Python 3 con numpy/scipy; la decodificación usa el ffmpeg del sistema.
Sirve para dos cosas:
  - medir loudness (LUFS BS.1770), true peak y RMS máximo de los clips;
  - construir mezclas de prueba (el piloto del clímax del prólogo).

Convenciones:
  - todo el audio se trabaja a 48000 Hz, estéreo, float32 (n, 2);
  - los niveles se expresan en dBFS o LUFS, nunca en خطares "a ojo".
"""
import os
import re
import subprocess
import tempfile

import numpy as np
from scipy import signal

SR = 48000


# ───────────────────────────── decodificación ─────────────────────────────

def ffmpeg_bin():
    """Ruta al ffmpeg: variable de entorno FFMPEG o el del sistema."""
    return os.environ.get("FFMPEG", "ffmpeg")


def decodificar(ruta, sr=SR, mono=False):
    """Lee un audio (mp3/wav/ogg) a float32 estéreo (n, 2)."""
    cmd = [ffmpeg_bin(), "-hide_banner", "-loglevel", "error", "-i", ruta,
           "-ac", "1" if mono else "2", "-ar", str(sr), "-f", "f32le", "-"]
    p = subprocess.run(cmd, capture_output=True)
    if p.returncode != 0:
        raise RuntimeError(f"ffmpeg no pudo leer {ruta}: {p.stderr.decode('utf-8', 'ignore')[:300]}")
    x = np.frombuffer(p.stdout, dtype=np.float32)
    return x.reshape(-1, 2).copy() if not mono else x.copy()


def escribir(ruta, x, sr=SR):
    """Guarda un array (n,) mono o (n, 2) estéreo como WAV de 24 bits."""
    import wave
    if x.ndim == 1:
        canales, datos = 1, x
    else:
        canales, datos = 2, x.T
    pcm = np.clip(datos, -1.0, 1.0).astype(np.float64)
    entero = (pcm * 2147483647.0).astype(np.int32)
    with wave.open(ruta, "wb") as w:
        w.setnchannels(canales)
        w.setsampwidth(4)
        w.setframerate(sr)
        w.writeframes(entero.tobytes())


# ───────────────────────────── medición ─────────────────────────────

def _filtro_k(x, sr):
    """Filtro K de la BS.1770 (silla de agudos + paso alto)."""
    b1 = np.array([1.53512485958697, -2.69169618940638, 1.19839281085285])
    a1 = np.array([1.0, -1.69065929318241, 0.73248077421585])
    b2 = np.array([1.0, -2.0, 1.0])
    a2 = np.array([1.0, -1.99004745483398, 0.99007225036621])
    y = signal.lfilter(b1, a1, x, axis=0)
    return signal.lfilter(b2, a2, y, axis=0)


def lufs(x, sr=SR):
    """Loudness integrado BS.1770-4 (K-weighting + gating)."""
    if x.ndim == 1:
        x = x[:, None]
    if x.shape[1] > 2:
        x = x[:, :2]
    y = _filtro_k(x, sr)
    bloque = int(0.4 * sr)
    salto = int(0.1 * sr)
    if len(y) < bloque:
        return -np.inf
    n = 1 + (len(y) - bloque) // salto
    energia = np.empty(n)
    for i in range(n):
        seg = y[i * salto:i * salto + bloque]
        energia[i] = np.sum(np.mean(seg ** 2, axis=0))
    with np.errstate(divide="ignore"):
        l = -0.691 + 10 * np.log10(energia)
    mascara = l > -70
    if not np.any(mascara):
        return -np.inf
    j = l[mascara]
    relativa = j >= (j.max() - 10)
    return float(-0.691 + 10 * np.log10(np.mean(energia[mascara][relativa])))


def true_peak(x, sr=SR, sobremuestreo=4):
    """Pico real (interpola 4x para no perder picos intersample)."""
    if x.ndim == 1:
        x = x[:, None]
    y = signal.resample_poly(x, sobremuestreo, 1, axis=0)
    return float(20 * np.log10(max(np.max(np.abs(y)), 1e-12)))


def rms_maximo(x, sr=SR, ventana=0.4):
    """RMS máximo en una ventana deslizante (el 'punch' de un sonido)."""
    if x.ndim > 1:
        x = x.mean(axis=1)
    k = int(ventana * sr)
    if len(x) < k:
        return float(20 * np.log10(max(np.sqrt(np.mean(x ** 2)), 1e-12)))
    c = np.concatenate([[0.0], np.cumsum(x ** 2)])
    rms = np.sqrt((c[k:] - c[:-k]) / k)
    return float(20 * np.log10(max(rms.max(), 1e-12)))


def informe(rutas, sr=SR):
    """Mide una lista de ficheros y devuelve filas de datos."""
    filas = []
    for ruta in rutas:
        x = decodificar(ruta, sr)
        filas.append({
            "fichero": os.path.basename(ruta),
            "duracion": len(x) / sr,
            "lufs": lufs(x, sr),
            "true_peak": true_peak(x, sr),
            "rms_max": rms_maximo(x, sr),
        })
    return filas


# ───────────────────────────── mezcla ─────────────────────────────

def db_a_ganancia(db):
    return 10 ** (db / 20.0)


def ganancia_db(x):
    return 20 * np.log10(max(np.max(np.abs(x)), 1e-12))


def colocar(destino, fuente, t, ganancia=0.0, pan=0.0):
    """Mezcla `fuente` en `destino` en el instante t (s). `pan` -1..1."""
    i = int(t * SR)
    if i >= len(destino):
        return
    fin = min(len(destino), i + len(fuente))
    seg = fuente[:fin - i]
    if pan != 0.0:
        ang = (pan + 1.0) * 0.25 * np.pi
        gl, gr = np.cos(ang), np.sin(ang)
        seg = seg * np.array([gl, gr])
    destino[i:fin] += seg * db_a_ganancia(ganancia)


def normalizar_lufs(x, objetivo=-18.0):
    """Ajusta la ganancia para alcanzar un LUFS objetivo (no toca picos)."""
    actual = lufs(x)
    if not np.isfinite(actual):
        return x
    return x * db_a_ganancia(objetivo - actual)


def limitador(x, techo_db=-1.0, sr=SR):
    """Limitador de pico con envolvente y ceiling suave."""
    if x.ndim == 1:
        x = x[:, None]
    techo = db_a_ganancia(techo_db)
    pico = np.max(np.abs(x), axis=1)
    # envolvente de pico con ataque instantáneo y caída de 50 ms
    a = np.exp(-1.0 / (0.05 * sr))
    env = np.zeros_like(pico)
    for i in range(len(pico)):
        env[i] = max(pico[i], a * env[i - 1])
    ganho = np.ones_like(env)
    sobre = env > techo
    ganho[sobre] = techo / env[sobre]
    y = x * ganho[:, None]
    return y


def compresor(x, sr=SR, umbral_db=-20.0, ratio=3.0, ataque=0.010, release=0.120):
    """Compresor de feed-forward con detector de pico."""
    if x.ndim == 1:
        x = x[:, None]
    det = np.max(np.abs(x), axis=1)
    a_a = np.exp(-1.0 / (ataque * sr))
    a_r = np.exp(-1.0 / (release * sr))
    env = np.empty_like(det)
    prev = 0.0
    for i in range(len(det)):
        c = a_a if det[i] > prev else a_r
        prev = c * prev + (1 - c) * det[i]
        env[i] = prev
    umbral = db_a_ganancia(umbral_db)
    g = np.ones_like(env)
    sobre = env > umbral
    g[sobre] = (umbral * (env[sobre] / umbral) ** (1 / ratio - 1)) / env[sobre]
    return x * g[:, None]


def paso_alto(x, fc, sr=SR, orden=2):
    """Paso alto. Se usa SOS (no biquad directo): con cortes muy bajos en 48 kHz
    el filtro directo se vuelve numéricamente inestable y revienta el pico."""
    sos = signal.butter(orden, fc / (sr / 2), "high", output="sos")
    return signal.sosfilt(sos, x, axis=0)


def paso_bajo(x, fc, sr=SR, orden=2):
    sos = signal.butter(orden, min(fc, sr / 2 * 0.99) / (sr / 2), "low", output="sos")
    return signal.sosfilt(sos, x, axis=0)


def banda(x, f1, f2, sr=SR, orden=2):
    sos = signal.butter(orden, [f1 / (sr / 2), min(f2, sr / 2 * 0.99) / (sr / 2)],
                        "band", output="sos")
    return signal.sosfilt(sos, x, axis=0)


def normalizar_pico(x, objetivo_db=-3.0):
    """Lleva el pico absoluto al objetivo (los filtros de banda resonant dan
    ganancias enormes a frecuencias muy bajas: hay que corregirlas)."""
    p = float(np.max(np.abs(x))) if x.size else 0.0
    if p <= 0:
        return x
    return x * (db_a_ganancia(objetivo_db) / p)


def pico(x, fc, gain_db, q=1.2, sr=SR):
    """Filtro de pico (EQ) en dB."""
    A = 10 ** (gain_db / 40.0)
    w0 = 2 * np.pi * fc / sr
    alpha = np.sin(w0) / (2 * q)
    b = [1 + alpha * A, -2 * np.cos(w0), 1 - alpha * A]
    a = [1 + alpha / A, -2 * np.cos(w0), 1 - alpha / A]
    return signal.lfilter(b, a, x, axis=0)


def saturar(x, cantidad=0.15):
    """Saturación suave (tanh) para dar cuerpo a la voz."""
    k = 1.0 + cantidad * 8.0
    return np.tanh(x * k) / np.tanh(k)


def reverberacion(x, sr=SR, decay=3.0, predelay=0.05, width=1.0, wet=0.2, hp=250.0):
    """Reverb tipo placa (Freeverb: 8 peinees en paralelo + 4 paso-todo).

    Se usa en vez de convolver con una IR de ruido porque el gain de la
    convolucion es muy sensible y descontrola el pico de la mezcla.

    Acepta mono (n,) o estereo (n, 2) y devuelve la misma forma.
    """
    mono = x.ndim == 1
    xc = x[:, None] if mono else x
    canales = xc.shape[1]
    escala = sr / 44100.0
    comb_delays = [int(round(d * escala)) for d in (1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617)]
    ap_delays = [int(round(d * escala)) for d in (556, 441, 341, 225)]
    # realimentacion: RT60 = -3*D/ln(fb) con D ~ 31 ms
    fb = float(np.clip(np.exp(-3.0 * 0.031 / max(decay, 0.2)), 0.5, 0.97))
    damping = 0.35

    def comb(entrada, d):
        n = len(entrada)
        salida = np.zeros(n)
        buf = np.zeros(d)
        estado = 0.0
        for i in range(n):
            v = buf[i % d]
            salida[i] = entrada[i] + v
            estado = v * (1 - damping) + estado * damping
            buf[i % d] = estado * fb
        return salida

    def allpass(entrada, d):
        n = len(entrada)
        salida = np.zeros(n)
        buf = np.zeros(d)
        for i in range(n):
            v = buf[i % d]
            salida[i] = -fb * entrada[i] + v
            buf[i % d] = entrada[i] + fb * v
        return salida

    pre = int(predelay * sr)
    salida = np.zeros_like(xc)
    for c in range(canales):
        entrada = xc[:, c]
        if pre:
            entrada = np.concatenate([np.zeros(pre), entrada])[:len(xc)]
        acum = np.zeros(len(entrada))
        for d in comb_delays:
            ret = d + (int(23 * width) if c == 1 else 0)
            acum += comb(entrada, ret) * (0.015 if c == 0 else 0.018)
        for d in ap_delays:
            acum = allpass(acum * 0.5, d) * 0.5
        salida[:, c] = xc[:, c] * (1 - wet) + acum * wet
    return salida[:, 0] if mono else salida


def retardo(x, segundos=0.3, mezcla=0.15, repeticiones=2):
    """Delay simple con realimentación."""
    y = x.copy()
    d = int(segundos * SR)
    g = mezcla
    for _ in range(repeticiones):
        y[d:] += y[:-d] * g
        g *= 0.6
    return y


def eco_slap(x, retardo_ms=260.0, decaimiento=0.32, humedo=0.25, sr=SR):
    """Emula el AudioEchoFilter de Unity (el 'eco' del conjuro): un retardo con
    realimentación. Es lo que hace que una voz gritada suene a anime."""
    d = int(retardo_ms / 1000.0 * sr)
    if d <= 0:
        return x
    y = x.copy()
    g = humedo
    for _ in range(3):
        y[d:] += y[:-d] * g
        g *= decaimiento
    return y


def fundidos(x, entrada=0.0, salida=0.0):
    if entrada > 0:
        n = min(int(entrada * SR), len(x))
        x[:n] *= np.linspace(0, 1, n)[:, None]
    if salida > 0:
        n = min(int(salida * SR), len(x))
        x[-n:] *= np.linspace(1, 0, n)[:, None]
    return x


def rampa_ganancia(x, t, desde_db, hasta_db, duracion, sr=SR):
    """Aplica una rampa de ganancia a partir de t (para builds y duckings)."""
    i = int(t * sr)
    n = int(duracion * sr)
    if i >= len(x) or n <= 0:
        return x
    curva = np.linspace(db_a_ganancia(desde_db), db_a_ganancia(hasta_db), n)
    fin = min(len(x), i + n)
    x[i:fin] *= (curva[:fin - i] / db_a_ganancia(desde_db))[:, None]
    return x


def decaer(x, hasta_db=-40.0, duracion=1.0, t=0.0, sr=SR):
    """Apaga una capa desde t hasta 'hasta_db' en 'duracion' segundos."""
    i = int(t * sr)
    n = int(duracion * sr)
    if i >= len(x):
        return x
    fin = min(len(x), i + n)
    curva = np.linspace(0, db_a_ganancia(hasta_db), fin - i)
    x[i:fin] *= curva[:, None]
    x[fin:] *= db_a_ganancia(hasta_db)
    return x
