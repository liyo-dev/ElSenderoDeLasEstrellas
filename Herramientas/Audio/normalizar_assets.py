#!/usr/bin/env python3
"""Normaliza la loudness de los clips de audio del proyecto por categoría.

Objetivos por categoría (loudness integrado BS.1770 y tope de true peak):

    categoría    carpeta                                       LUFS   true peak
    -----------  -------------------------------------------   ----   ---------
    voces        Assets/Resources/Voices/es                    -18    -2 dBTP
    reacciones   Assets/Resources/Voices/reactions             -20    -3 dBTP
    hechizos     Assets/Audio/SFX_Hechizos (SFX_Hechizo_*)      -18    -2 dBTP
    impactos      Assets/Audio/SFX_Hechizos (SFX_Impacto_*)      -14    -1 dBTP
    musica       Assets/Audio/Music (y subcarpetas)               --    -1 dBTP
    ambiente     Assets/Audio/SFX_Prologo (Ambience_*)          -26    -3 dBTP

La música solo recibe el tope de true peak: su loudness integrada no se toca.

Uso:
    python Herramientas/Audio/normalizar_assets.py                       # informe
    python Herramientas/Audio/normalizar_assets.py --aplicar             # escribe
    python Herramientas/Audio/normalizar_assets.py --categorias voces,impactos
    python Herramientas/Audio/normalizar_assets.py --carpeta Assets/Audio/SFX_Puzzles
    python Herramientas/Audio/normalizar_assets.py --detalle             # lista cada clip

El modo por defecto es INFORME: no se toca ningún fichero. Con `--aplicar` los
clips se sobrescriben en su sitio, guardando antes una copia byte a byte en
`Herramientas/Audio/_backup_originales/<ruta_relativa_del_original>`. Si esa
copia ya existe no se sobrescribe (es el original intacto).

ffmpeg: se usa la variable de entorno FFMPEG si está definida; si no, el
binario de referencia del proyecto (FFMPEG_POR_DEFECTO). La variable se
exporta al entorno para que `dsp_audio` use el mismo binario.
"""
import argparse
import os
import re
import shutil
import statistics
import subprocess
import sys
import time
import wave

import numpy as np
from scipy import signal

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsp_audio import db_a_ganancia, escribir, lufs, rms_maximo, true_peak  # noqa: E402

RAIZ = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
CARPETA_BACKUP = os.path.join(RAIZ, "Herramientas", "Audio", "_backup_originales")

# ffmpeg: variable de entorno FFMPEG si está definida; si no, el binario de
# referencia del proyecto. Se exporta al entorno para que dsp_audio.ffmpeg_bin()
# use el mismo binario que este script.
FFMPEG_POR_DEFECTO = os.path.join(
    r"C:\Users\luarb\AppData\Local\Temp\pyaudio\imageio_ffmpeg\binaries",
    "ffmpeg-win-x86_64-v7.1.exe")
os.environ.setdefault("FFMPEG", FFMPEG_POR_DEFECTO)

# Audio decodificable por ffmpeg (lo que hay en el proyecto).
EXTENSIONES = (".wav", ".mp3", ".ogg", ".aiff", ".flac")

# Codec con el que se reescribe cada formato (el original se conserva en canal
# y frecuencia de muestreo).
CODEC_A_ESCRITURA = {
    ".mp3": ["-c:a", "libmp3lame", "-b:a", "192k"],
    ".ogg": ["-c:a", "libvorbis", "-q:a", "5"],
    ".flac": ["-c:a", "flac"],
    ".aiff": ["-c:a", "pcm_s24be"],
}

# Tolerancia de loudness: dentro de esta franja el clip no se toca (salvo que
# el true peak pase del tope).
TOLERANCIA_LUFS = 0.3

# Por encima de este número de muestras el pico real y la curva de ganancia se
# calculan por bloques: sobremuestrear 4x una pista de 7 min son ~700 MB.
LIMITE_DIRECTO = 4_000_000

SIN_CAMBIOS = "sin cambios"


def ffmpeg_bin():
    """Ruta del ffmpeg: variable de entorno FFMPEG o el de referencia."""
    return os.environ.get("FFMPEG") or FFMPEG_POR_DEFECTO


# ───────────────────────────── categorías ─────────────────────────────

class Categoria:
    """Una categoría de clips: carpeta, objetivo de loudness y tope de pico.

    `objetivo` a None significa "no tocar la loudness integrada" (música):
    solo se limita el true peak.
    """

    def __init__(self, clave, nombre, carpeta, objetivo=None, techo=-2.0,
                 prefijo=None):
        self.clave = clave
        self.nombre = nombre
        self.carpeta = carpeta
        self.objetivo = objetivo
        self.techo = techo
        self.prefijo = prefijo

    @property
    def ruta(self):
        """Ruta absoluta de la carpeta de la categoría."""
        return self.carpeta if os.path.isabs(self.carpeta) else os.path.join(RAIZ, self.carpeta)

    @property
    def carpeta_rel(self):
        """Ruta de la carpeta relativa a la raíz del proyecto (para el informe)."""
        if os.path.isabs(self.carpeta):
            return os.path.relpath(self.carpeta, RAIZ)
        return self.carpeta.replace("/", os.sep)

    def __str__(self):
        return f"{self.nombre} ({self.clave})"


CATEGORIAS = {
    "voces": Categoria("voces", "Voces", "Assets/Resources/Voices/es", -18.0, -2.0),
    "reacciones": Categoria("reacciones", "Voces (reacciones)",
                            "Assets/Resources/Voices/reactions", -20.0, -3.0),
    "hechizos": Categoria("hechizos", "Hechizos (lanzamientos)",
                          "Assets/Audio/SFX_Hechizos", -18.0, -2.0, prefijo="SFX_Hechizo_"),
    "impactos": Categoria("impactos", "Impactos",
                          "Assets/Audio/SFX_Hechizos", -14.0, -1.0, prefijo="SFX_Impacto_"),
    "musica": Categoria("musica", "Música", "Assets/Audio/Music", None, -1.0),
    "ambiente": Categoria("ambiente", "Ambiente / prólogo",
                          "Assets/Audio/SFX_Prologo", -26.0, -3.0, prefijo="Ambience_"),
}

# Sinónimos aceptados por --categorias.
ALIAS = {
    "voz": "voces", "voces": "voces", "voice": "voces",
    "reaccion": "reacciones", "reacciones": "reacciones", "reaction": "reacciones",
    "reactions": "reacciones",
    "hechizo": "hechizos", "hechizos": "hechizos", "spell": "hechizos",
    "impacto": "impactos", "impactos": "impactos", "impact": "impactos",
    "musica": "musica", "music": "musica",
    "ambiente": "ambiente", "ambientes": "ambiente", "ambience": "ambiente",
    "prologo": "ambiente",
}


# ───────────────────────────── lectura de audio ─────────────────────────────

RE_AUDIO = re.compile(
    r"Stream #\d+:\d+(?:\[[^\]]*\])?(?:\([^)]*\))?:\s*Audio:\s*(?P<codec>[^,]+),"
    r"\s*(?P<sr>\d+)\s*Hz,\s*(?P<canales>[^,]+),")
RE_VIDEO = re.compile(
    r"Stream #\d+:\d+(?:\[[^\]]*\])?(?:\([^)]*\))?:\s*Video:")


def _canales_de(texto):
    """Número de canales a partir de lo que dice ffmpeg (mono/stereo/5.1)."""
    limpio = texto.strip()
    if limpio == "mono":
        return 1
    if limpio == "stereo":
        return 2
    try:
        return int(limpio.split(".")[0])
    except ValueError:
        return 2


def decodificar_fiel(ruta):
    """Decodifica a float32 conservando canales y frecuencia originales.

    Devuelve (audio, canales, sr, codec, tiene_caratula). No se usa
    `dsp_audio.decodificar` porque fuerza estéreo a 48 kHz y muchos clips del
    proyecto son mono a 44,1 kHz (convertir a estéreo los deformaría).
    """
    cmd = [ffmpeg_bin(), "-hide_banner", "-i", ruta, "-f", "f32le", "-"]
    p = subprocess.run(cmd, capture_output=True)
    if p.returncode != 0:
        raise RuntimeError(f"ffmpeg no pudo leer {ruta}: "
                           f"{p.stderr.decode('utf-8', 'ignore')[:300]}")
    err = p.stderr.decode("utf-8", "ignore")
    m = RE_AUDIO.search(err)
    if not m:
        raise RuntimeError(f"no se pudo deducir el formato de audio de {ruta}")
    canales = _canales_de(m.group("canales"))
    sr = int(m.group("sr"))
    x = np.frombuffer(p.stdout, dtype=np.float32)
    if x.size % canales:
        raise RuntimeError(f"decodificación incompleta de {ruta}")
    return (np.ascontiguousarray(x.reshape(-1, canales)), canales, sr,
            m.group("codec").strip(), bool(RE_VIDEO.search(err)))


def bits_de_wav(ruta, codec):
    """Profundidad de bits del WAV original (para no degradarlo al escribir)."""
    try:
        with wave.open(ruta, "rb") as w:
            return w.getsampwidth() * 8
    except Exception:
        pass
    m = re.search(r"pcm_(?:s|u|f)(\d+)", codec)
    return int(m.group(1)) if m else 16


# ───────────────────────────── medición ─────────────────────────────

def medir_true_peak(x, sr, sobremuestreo=4, bloque=10.0):
    """Pico real (4x). Para los clips cortos usa `dsp_audio.true_peak` tal cual;
    los largos se miden por bloques para no reservar la memoria del
    sobremuestreo completo."""
    n = len(x)
    if n == 0:
        return float("-inf")
    if n <= LIMITE_DIRECTO:
        return true_peak(x, sr, sobremuestreo)
    pico = 0.0
    paso = int(bloque * sr)
    margen = int(0.25 * sr)
    i = 0
    while i < n:
        j = min(n, i + paso)
        a = max(0, i - margen)
        b = min(n, j + margen)
        ys = signal.resample_poly(x[a:b], sobremuestreo, 1, axis=0)
        if ys.size:
            pico = max(pico, float(np.max(np.abs(ys))))
        i = j
    return 20 * np.log10(max(pico, 1e-12))


def medir(x, sr):
    """LUFS, true peak y RMS máximo del audio."""
    return {
        "lufs": lufs(x, sr),
        "true_peak": medir_true_peak(x, sr),
        "rms": rms_maximo(x, sr),
    }


# ───────────────────────────── limitadores ─────────────────────────────

def envolvente_decay(pico, a, estado=0.0, bloque=8192):
    """Envolvente de pico con ataque instantáneo y caída exponencial.

    Reproduce `env[i] = max(pico[i], a * env[i-1])` (la envolvente de
    `dsp_audio.limitador`) de forma vectorizada, por bloques: aquel recorre
    las muestras en Python y con las pistas de música tardaría minutos.
    """
    n = len(pico)
    if n == 0:
        return np.zeros(0, dtype=np.float64)
    salida = np.empty(n, dtype=np.float64)
    e = float(estado)
    # Las potencias solo dependen del índice dentro del bloque: se calculan
    # una vez y se reutilizan (np.power es el punto lento si se repite).
    decae = np.power(a, np.arange(bloque, dtype=np.float64))
    crece = np.power(a, -np.arange(bloque, dtype=np.float64))
    arrastre = decae * a
    for s in range(0, n, bloque):
        t = min(n, s + bloque)
        k = t - s
        env = np.maximum.accumulate(pico[s:t].astype(np.float64) * crece[:k]) * decae[:k]
        env = np.maximum(env, e * arrastre[:k])
        salida[s:t] = env
        e = env[-1]
    return salida


def limitador_suave(x, techo_db, sr, release=0.05):
    """Limitador de pico suave: ataque instantáneo y caída de 50 ms.

    Es el mismo criterio que `dsp_audio.limitador`, pero vectorizado.
    """
    if x.size == 0:
        return x
    pico = np.max(np.abs(x), axis=1)
    a = float(np.exp(-1.0 / (release * sr)))
    env = envolvente_decay(pico, a)
    techo = db_a_ganancia(techo_db)
    g = np.ones_like(env)
    sobre = env > techo
    g[sobre] = techo / env[sobre]
    return (x * g[:, None].astype(np.float32)).astype(np.float32)


def curva_ganancia_true_peak(x, techo_db, sr, sobremuestreo=4, bloque=10.0,
                              release=0.05):
    """Ganancia por muestra para que el pico real (4x) quepa bajo el techo.

    Se sobremuestrea a 4x con `scipy.signal.resample_poly` y se calcula el pico
    de ese audio; la ganancia resultante se reduce a la frecuencia original
    tomando el mínimo de cada grupo (conservador) y se aplica sobre el audio.
    """
    n = len(x)
    if n == 0:
        return None
    techo = db_a_ganancia(techo_db)
    g = np.ones(n, dtype=np.float32)
    a4 = float(np.exp(-1.0 / (release * sr * sobremuestreo)))
    estado = 0.0
    paso = int(bloque * sr)
    margen = int(0.25 * sr)
    i = 0
    while i < n:
        j = min(n, i + paso)
        a = max(0, i - margen)
        b = min(n, j + margen)
        ys = signal.resample_poly(x[a:b], sobremuestreo, 1, axis=0)
        if ys.size:
            pico = np.max(np.abs(ys), axis=1)
            env = envolvente_decay(pico, a4, estado)
            gn = np.ones_like(env)
            sobre = env > techo
            gn[sobre] = techo / env[sobre]
            m = len(gn) // sobremuestreo
            if m:
                gb = gn[:m * sobremuestreo].reshape(m, sobremuestreo).min(axis=1)
                g[a:a + m] = np.minimum(g[a:a + m], gb.astype(np.float32))
            estado = env[-1]
        i = j
    return g


def limitar_true_peak(x, techo_db, sr):
    """Limitador suave + limitador de true peak (4x) y recorte global final.

    Si tras los dos limitadores el true peak sigue por encima del techo, se
    baja la ganancia global lo justo para que quepa.
    """
    if x.size == 0:
        return x
    y = limitador_suave(x, techo_db, sr)
    g = curva_ganancia_true_peak(y, techo_db, sr)
    if g is not None:
        y = (y * g[:, None]).astype(np.float32)
    tp = medir_true_peak(y, sr)
    if tp > techo_db:
        y = (y * db_a_ganancia(techo_db - tp)).astype(np.float32)
    return y


# ───────────────────────────── procesado ─────────────────────────────

def aplicar_objetivo(x, antes, cat, sr):
    """Devuelve (audio procesado, descripción de lo que se hace).

    Un clip que ya está dentro del objetivo (±0,3 dB de LUFS) no se toca,
    salvo que su true peak pase del tope: en ese caso solo se ajusta el pico.
    """
    pico_actual = antes["true_peak"]
    dentro_de_pico = pico_actual <= cat.techo + 1e-9

    if cat.objetivo is None:
        # Categoría sin objetivo de loudness (música): solo el tope de pico.
        if dentro_de_pico:
            return x, SIN_CAMBIOS
        return limitar_true_peak(x, cat.techo, sr), "tope de true peak"

    actual = antes["lufs"]
    if not np.isfinite(actual):
        # Clip demasiado corto para medir el loudness: solo se limita el pico.
        if dentro_de_pico:
            return x, "sin cambios (LUFS no medible)"
        return limitar_true_peak(x, cat.techo, sr), "solo pico (LUFS no medible)"

    delta = cat.objetivo - actual
    if abs(delta) <= TOLERANCIA_LUFS:
        if dentro_de_pico:
            return x, SIN_CAMBIOS
        return limitar_true_peak(x, cat.techo, sr), "solo pico"

    y = (x * db_a_ganancia(delta)).astype(np.float32)
    if medir_true_peak(y, sr) > cat.techo:
        y = limitar_true_peak(y, cat.techo, sr)
    return y, f"ganancia {delta:+.1f} dB"


# ───────────────────────────── escritura ─────────────────────────────

def pasar_a_ffmpeg(x, sr, opciones, destino, origen=None, caratula=False):
    """Codifica el audio float32 con ffmpeg.

    Si se pasa `origen`, ese fichero entra como entrada 0: de él se copian las
    etiquetas ID3 y (si la tiene) la carátula incrustada, para no perderlas al
    reescribir el MP3.
    """
    cmd = [ffmpeg_bin(), "-y", "-hide_banner", "-loglevel", "error"]
    if origen:
        cmd += ["-i", origen]
    cmd += ["-f", "f32le", "-ar", str(sr), "-ac", str(x.shape[1]), "-i", "-"]
    if origen:
        cmd += ["-map", "1:a", "-map_metadata", "0", "-map_metadata:s:a", "0:s:a"]
        if caratula:
            cmd += ["-map", "0:v", "-c:v", "copy"]
    cmd += opciones + [destino]
    datos = np.ascontiguousarray(x, dtype=np.float32).tobytes()
    p = subprocess.run(cmd, input=datos, capture_output=True)
    if p.returncode != 0:
        raise RuntimeError(f"ffmpeg no pudo escribir {destino}: "
                           f"{p.stderr.decode('utf-8', 'ignore')[:300]}")


def guardar(ruta, x, sr, bits=24, caratula=False):
    """Reescribe el clip procesado en su sitio conservando su formato.

    WAV: 32 bits con el escritor de `dsp_audio`; si el original no es de 32
    bits se escribe a pcm_s24le con ffmpeg (no se degrada la profundidad).
    MP3: libmp3lame a 192k, con los canales y la frecuencia originales.
    """
    ext = os.path.splitext(ruta)[1].lower()
    tmp = f"{ruta}.normaliza_tmp{ext}"
    try:
        if ext == ".wav":
            if bits >= 32:
                # dsp_audio.escribir: 1D para mono, (n, 2) para estéreo.
                escribir(tmp, x[:, 0] if x.shape[1] == 1 else x, sr)
            else:
                pasar_a_ffmpeg(x, sr, ["-c:a", "pcm_s24le"], tmp)
        else:
            opciones = CODEC_A_ESCRITURA.get(ext, ["-c:a", "libmp3lame", "-b:a", "192k"])
            pasar_a_ffmpeg(x, sr, opciones, tmp, origen=ruta,
                           caratula=caratula and ext == ".mp3")
        os.replace(tmp, ruta)
    finally:
        if os.path.exists(tmp):
            os.remove(tmp)


def guardar_backup(ruta):
    """Copia byte a byte del original en `_backup_originales`. No sobrescribe."""
    rel = os.path.relpath(ruta, RAIZ)
    destino = os.path.join(CARPETA_BACKUP, rel)
    if os.path.exists(destino):
        return False
    os.makedirs(os.path.dirname(destino), exist_ok=True)
    shutil.copy2(ruta, destino)
    return True


# ───────────────────────────── recorrido ─────────────────────────────

def recoger(cat):
    """Clips de la categoría (recursivo; con prefijo, solo los que lo llevan)."""
    rutas = []
    for base, _, ficheros in os.walk(cat.ruta):
        for f in sorted(ficheros):
            if not f.lower().endswith(EXTENSIONES):
                continue
            if cat.prefijo and not f.startswith(cat.prefijo):
                continue
            rutas.append(os.path.join(base, f))
    return sorted(rutas)


def procesar_categoria(cat, args):
    """Mide, normaliza (en memoria) y opcionalmente reescribe cada clip."""
    if not os.path.isdir(cat.ruta):
        print(f"aviso: no existe la carpeta de {cat.nombre}: {cat.ruta}")
        return []

    rutas = recoger(cat)
    if not rutas:
        print(f"aviso: no hay clips en {cat.carpeta_rel} para {cat.nombre}")
        return []

    if cat.objetivo is not None:
        objetivo = f"objetivo {cat.objetivo:.1f} LUFS | tope {cat.techo:.1f} dBTP"
    else:
        objetivo = f"sin objetivo de loudness (solo tope {cat.techo:.1f} dBTP)"
    print(f"\n--- {cat.nombre}: {len(rutas)} clips   {objetivo}")

    filas = []
    for ruta in rutas:
        try:
            x, canales, sr, codec, caratula = decodificar_fiel(ruta)
            antes = medir(x, sr)
            y, accion = aplicar_objetivo(x, antes, cat, sr)
            # si el clip no se toca, el "después" es el "antes" (no hay que remedir)
            despues = antes if accion == SIN_CAMBIOS else medir(y, sr)
            cambia = accion != SIN_CAMBIOS
            escrito = False
            if cambia and args.aplicar:
                guardar_backup(ruta)
                bits = bits_de_wav(ruta, codec) if ruta.lower().endswith(".wav") else 24
                guardar(ruta, y, sr, bits=bits, caratula=caratula)
                escrito = True
            filas.append({
                "ruta": ruta,
                "rel": os.path.relpath(ruta, RAIZ),
                "canales": canales,
                "sr": sr,
                "antes": antes,
                "despues": despues,
                "accion": accion,
                "cambia": cambia,
                "escrito": escrito,
            })
            del x, y
        except Exception as exc:  # un clip roto no debe parar el informe
            filas.append({
                "ruta": ruta,
                "rel": os.path.relpath(ruta, RAIZ),
                "canales": 0,
                "sr": 0,
                "antes": {"lufs": float("nan"), "true_peak": float("nan"),
                          "rms": float("nan")},
                "despues": {"lufs": float("nan"), "true_peak": float("nan"),
                            "rms": float("nan")},
                "accion": f"error: {exc}",
                "cambia": False,
                "escrito": False,
            })
    return filas


# ───────────────────────────── informe ─────────────────────────────

def _num(v, decimales=1):
    if v is None or not np.isfinite(v):
        return "n/d"
    return f"{v:.{decimales}f}"


def _mediana(valores):
    finitos = [v for v in valores if np.isfinite(v)]
    return statistics.median(finitos) if finitos else float("nan")


def _rango(valores):
    """(mínimo, máximo, amplitud) de los valores finitos."""
    finitos = [v for v in valores if np.isfinite(v)]
    if not finitos:
        return float("nan"), float("nan"), float("nan")
    return min(finitos), max(finitos), max(finitos) - min(finitos)


def mostrar_categoria(cat, filas, args):
    """Resumen por categoría: antes/después y qué se va a tocar."""
    print(f"\n=== {cat.nombre}  ({len(filas)} clips)")
    if cat.objetivo is not None:
        print(f"    objetivo {cat.objetivo:.1f} LUFS | "
              f"tope de true peak {cat.techo:.1f} dBTP")
    else:
        print(f"    sin objetivo de loudness | "
              f"tope de true peak {cat.techo:.1f} dBTP")

    lufs_antes = [f["antes"]["lufs"] for f in filas]
    lufs_despues = [f["despues"]["lufs"] for f in filas]
    picos_antes = [f["antes"]["true_peak"] for f in filas]
    picos_despues = [f["despues"]["true_peak"] for f in filas]

    r_antes = _rango(lufs_antes)
    r_despues = _rango(lufs_despues)
    print(f"    LUFS       antes  mediana {_num(_mediana(lufs_antes)):>7} | "
          f"rango {_num(r_antes[0]):>7} .. {_num(r_antes[1]):>7} "
          f"({_num(r_antes[2])} dB)")
    print(f"              después mediana {_num(_mediana(lufs_despues)):>7} | "
          f"rango {_num(r_despues[0]):>7} .. {_num(r_despues[1]):>7} "
          f"({_num(r_despues[2])} dB)")
    print(f"    true peak  antes  mediana {_num(_mediana(picos_antes)):>7} | "
          f"máx {_num(max(picos_antes) if picos_antes else float('nan')):>7} dBTP")
    print(f"              después mediana {_num(_mediana(picos_despues)):>7} | "
          f"máx {_num(max(picos_despues) if picos_despues else float('nan')):>7} dBTP")
    rms_antes = [f["antes"]["rms"] for f in filas]
    rms_despues = [f["despues"]["rms"] for f in filas]
    print(f"    RMS máx.   antes  mediana {_num(_mediana(rms_antes)):>7} | "
          f"después mediana {_num(_mediana(rms_despues)):>7} dBFS")

    cambios = [f for f in filas if f["cambia"]]
    grandes = [f for f in filas
                if np.isfinite(f["antes"]["lufs"]) and np.isfinite(f["despues"]["lufs"])
                and abs(f["despues"]["lufs"] - f["antes"]["lufs"]) > 0.5]
    errores = [f for f in filas if f["accion"].startswith("error")]
    print(f"    clips que cambian más de 0,5 dB de LUFS: {len(grandes)} de {len(filas)}")
    print(f"    clips a reescribir: {len(cambios)}  (sin cambios: {len(filas) - len(cambios)})")
    if errores:
        print(f"    clips con error: {len(errores)}")

    if cambios:
        print("    cambios previstos:")
        mostrar = cambios if args.detalle else cambios[:15]
        for f in mostrar:
            etiqueta = "reescrito" if f["escrito"] else "a reescribir"
            print(f"      {f['rel'][-66:]:<66} "
                  f"{_num(f['antes']['lufs']):>7} -> {_num(f['despues']['lufs']):>7} LUFS | "
                  f"pico {_num(f['antes']['true_peak']):>6} -> "
                  f"{_num(f['despues']['true_peak']):>6} dBTP | "
                  f"{f['accion']} ({etiqueta})")
        if len(cambios) > len(mostrar):
            print(f"      ... y {len(cambios) - len(mostrar)} clips más "
                  f"(usa --detalle para verlos todos)")


def mostrar_resumen(resultados):
    """Tabla final por categoría."""
    print("\n" + "=" * 108)
    print("RESUMEN POR CATEGORÍA")
    print(f"{'categoría':<22}{'n':>5}{'LUFS med antes':>16}{'LUFS med después':>18}"
          f"{'rango antes':>14}{'rango después':>15}{'>0,5 dB':>9}{'reescritos':>12}")
    print("-" * 108)

    # Las filas llegan agrupadas por categoría (en el orden en que se procesaron).
    grupos = []
    for f in resultados:
        nombre = f.get("categoria", "?")
        if not grupos or grupos[-1][0] != nombre:
            grupos.append((nombre, []))
        grupos[-1][1].append(f)

    for nombre, filas in grupos:
        lufs_antes = [f["antes"]["lufs"] for f in filas]
        lufs_despues = [f["despues"]["lufs"] for f in filas]
        r_antes = _rango(lufs_antes)
        r_despues = _rango(lufs_despues)
        grandes = sum(1 for f in filas
                      if np.isfinite(f["antes"]["lufs"]) and np.isfinite(f["despues"]["lufs"])
                      and abs(f["despues"]["lufs"] - f["antes"]["lufs"]) > 0.5)
        reescritos = sum(1 for f in filas if f["escrito"])
        print(f"{nombre:<22}{len(filas):>5}"
              f"{_num(_mediana(lufs_antes)):>16}"
              f"{_num(_mediana(lufs_despues)):>18}"
              f"{_num(r_antes[2]) + ' dB':>14}"
              f"{_num(r_despues[2]) + ' dB':>15}"
              f"{grandes:>9}{reescritos:>12}")
    print("=" * 108)


# ───────────────────────────── línea de órdenes ─────────────────────────────

def parsear_args():
    p = argparse.ArgumentParser(
        description="Normaliza la loudness de los clips de audio por categoría.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=("Ejemplos:\n"
                "  python Herramientas/Audio/normalizar_assets.py\n"
                "  python Herramientas/Audio/normalizar_assets.py --aplicar\n"
                "  python Herramientas/Audio/normalizar_assets.py --categorias voces,impactos\n"
                "  python Herramientas/Audio/normalizar_assets.py --carpeta Assets/Audio/SFX_Puzzles\n"))
    p.add_argument("--aplicar", action="store_true",
                   help="reescribe los clips (sin este flag solo se informa)")
    p.add_argument("--categorias", default=None,
                   help="filtro de categorías separadas por comas "
                        f"({', '.join(CATEGORIAS)})")
    p.add_argument("--carpeta", default=None,
                   help="carpeta arbitraria a normalizar (relativa a la raíz "
                        "del proyecto; objetivo por defecto -18 LUFS, "
                        "tope -2 dBTP). Sustituye a las categorías del "
                        "proyecto salvo que se combine con --categorias")
    p.add_argument("--objetivo", type=float, default=-18.0,
                   help="objetivo de LUFS para --carpeta (por defecto -18)")
    p.add_argument("--techo", type=float, default=-2.0,
                   help="tope de true peak en dBTP para --carpeta (por defecto -2)")
    p.add_argument("--detalle", action="store_true",
                   help="lista todos los clips que cambian, no solo los primeros")
    return p.parse_args()


def seleccionar_categorias(args):
    """Categorías a trabajar según el filtro y la carpeta extra.

    `--carpeta` sola trabaja solo esa carpeta; si además se pasa
    `--categorias`, se procesan las dos cosas.
    """
    claves = list(CATEGORIAS)
    if args.categorias:
        claves = []
        for bruto in args.categorias.split(","):
            limpio = bruto.strip().lower()
            if not limpio:
                continue
            if limpio not in ALIAS:
                raise SystemExit(f"categoría desconocida: '{bruto}'. "
                                 f"Válidas: {', '.join(CATEGORIAS)}")
            clave = ALIAS[limpio]
            if clave not in claves:
                claves.append(clave)
    cats = [CATEGORIAS[c] for c in claves]
    if args.carpeta:
        if not args.categorias:
            # Solo la carpeta indicada: no se recorren las categorías del proyecto.
            cats = []
        nombre = os.path.basename(os.path.normpath(args.carpeta)) or args.carpeta
        cats.append(Categoria("carpeta", f"Carpeta ({nombre})", args.carpeta,
                              args.objetivo, args.techo))
    return cats


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

    args = parsear_args()
    cats = seleccionar_categorias(args)
    t0 = time.time()

    modo = "APLICAR (se sobrescriben los clips)" if args.aplicar else \
           "INFORME (no se modifica ningún fichero)"
    print("=" * 108)
    print(f"NORMALIZACIÓN DE LOUDNESS - MODO {modo}")
    print("=" * 108)
    print(f"ffmpeg   : {ffmpeg_bin()}")
    print(f"copias   : {os.path.relpath(CARPETA_BACKUP, RAIZ)}")
    print("categorías: " + ", ".join(c.clave for c in cats))
    if not args.aplicar:
        print("\nEsto es un informe: ningún fichero se toca. "
              "Añade --aplicar para reescribir los clips.")
    print("Las cifras 'después' son las del audio procesado; al codificar a "
          "MP3 el fichero pierde algo de precisión respecto a ellas.")

    resultados = []
    for cat in cats:
        filas = procesar_categoria(cat, args)
        for f in filas:
            f["categoria"] = cat.nombre
        mostrar_categoria(cat, filas, args)
        resultados.extend(filas)

    mostrar_resumen(resultados)

    if args.aplicar:
        escritos = sum(1 for f in resultados if f["escrito"])
        print(f"\nclips reescritos: {escritos}")
        print(f"copias de seguridad: {os.path.relpath(CARPETA_BACKUP, RAIZ)} "
              f"(no se sobrescriben las que ya existían)")
    else:
        print("\nPara aplicar estos cambios: vuelve a lanzarlo con --aplicar")
    print(f"tiempo total: {time.time() - t0:.1f} s")


if __name__ == "__main__":
    main()
