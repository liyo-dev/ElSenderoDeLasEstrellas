"""Caracteriza la voz del Mago Oscuro: pitch grave, formante bajo y un punto de coro.

La interpretación se conserva; solo se cambia el timbre. El efecto va horneado en
los MP3 para que la duración no cambie y el guion no se descoloque. La cadena es
la variante B aprobada por Raúl (22 oct 2026):

  -3 semitonos (rubberband 0.841), formante desplazado, paso bajo a 5 kHz,
  chorus suave, compresión, eco corto y limitador.

Los originales se guardan en `Versiones antiguas/` antes de sustituirlos. El
script es idempotente: si ya hay copia de seguridad, procesa desde ella.

Uso:
    python Herramientas/Audio/caracterizar_voz_mago.py
"""
import os
import re
import shutil
import subprocess
import sys

RAIZ = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
VOCES = os.path.join(RAIZ, "Assets", "Resources", "Voices", "es")
COPIA = os.path.join(RAIZ, "Versiones antiguas", "Voces del Mago Oscuro antes de efecto (2026-10-22)")

# Solo el Mago Oscuro: fuera los ARCHIMAGO y los ESTELA_ARCHIMAGO.
CLAVES = [
    "BF_INTRO_MAGO_01",
    "BF_INTRO_MAGO_02",
    "BF_INTRO_MAGO_03",
    "BF_INTRO_MAGO_04",
    "BF_MOMENT_MAGO_REMEMBER",
    "FINAL_MAGO_DESHACE",
    "FINAL_MAGO_MAREA",
    "FINAL_MAGO_TRAICION",
    "FINAL_MAGO_VENCIDO",
    "PROLOGO_DUELO_MAGO",
    "PROLOGO_DUELO_MAGO_2",
    "PROLOGO_MAGO_VACIO",
    "PROLOGO_MAGO_VALLE",
]

CADENA = (
    "highpass=f=60,"
    "rubberband=pitch=0.841:formant=shifted,"
    "lowpass=f=5000,"
    "chorus=0.7:0.9:40|60:0.35|0.30:0.25|0.30:2|1.5,"
    "acompressor=threshold=-18dB:ratio=3:attack=10:release=120,"
    "aecho=0.8:0.6:50|80:0.20|0.14,"
    "alimiter=limit=0.95,"
    "atrim=duration={duracion:.6f},"
    "afade=t=out:st={fade:.6f}:d=0.03"
)


def ffmpeg_bin():
    if os.environ.get("FFMPEG"):
        return os.environ["FFMPEG"]
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except Exception:
        return "ffmpeg"


def duracion(ruta):
    """Duración del contenedor en segundos."""
    p = subprocess.run([ffmpeg_bin(), "-hide_banner", "-i", ruta],
                       capture_output=True, text=True)
    m = re.search(r"Duration:\s*(\d+):(\d+):(\d+\.\d+)", p.stderr)
    if not m:
        raise RuntimeError(f"No se pudo leer la duración de {ruta}")
    h, mm, ss = int(m.group(1)), int(m.group(2)), float(m.group(3))
    return h * 3600 + mm * 60 + ss


def niveles(ruta):
    """(media, pico) en dBFS, medidos por ffmpeg."""
    p = subprocess.run([ffmpeg_bin(), "-hide_banner", "-i", ruta,
                        "-af", "volumedetect", "-f", "null", "-"],
                       capture_output=True, text=True)
    media = re.search(r"mean_volume:\s*(-?\d+\.\d+) dB", p.stderr)
    pico = re.search(r"max_volume:\s*(-?\d+\.\d+) dB", p.stderr)
    return (float(media.group(1)) if media else float("nan"),
            float(pico.group(1)) if pico else float("nan"))


def main():
    os.makedirs(COPIA, exist_ok=True)
    print(f"{'clave':<28} {'dur':>6} {'->':>6} {'media':>8} {'pico':>8}  estado")
    for clave in CLAVES:
        original = os.path.join(VOCES, clave + ".mp3")
        respaldo = os.path.join(COPIA, clave + ".mp3")
        if not os.path.exists(original):
            print(f"{clave:<28}  falta el MP3 original")
            continue

        # Primera ejecución: guardar el original. Posteriores: procesar desde la copia.
        if not os.path.exists(respaldo):
            shutil.copy2(original, respaldo)
            fuente = original
        else:
            fuente = respaldo

        dur = duracion(fuente)
        temp = original + ".tmp.mp3"
        cadena = CADENA.format(duracion=dur, fade=max(0.0, dur - 0.03))
        cmd = [ffmpeg_bin(), "-y", "-hide_banner", "-loglevel", "error",
               "-i", fuente, "-af", cadena, "-ar", "44100", "-ac", "1",
               "-codec:a", "libmp3lame", "-b:a", "192k", temp]
        p = subprocess.run(cmd, capture_output=True, text=True)
        if p.returncode != 0:
            print(f"{clave:<28}  ERROR: {p.stderr.strip()[:200]}")
            if os.path.exists(temp):
                os.remove(temp)
            continue

        dur_out = duracion(temp)
        media, pico = niveles(temp)
        os.replace(temp, original)
        print(f"{clave:<28} {dur:6.2f} {dur_out:6.2f} {media:8.1f} {pico:8.1f}  ok")

    print(f"\nOriginales en: {COPIA}")


if __name__ == "__main__":
    main()
