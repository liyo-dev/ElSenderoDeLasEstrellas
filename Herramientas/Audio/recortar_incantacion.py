#!/usr/bin/env python3
"""Recorta el trozo de «Ultimo Incantazione» que entra con el grito del prólogo.

La pieza de Suno son 159 s. El juego no usa la pieza entera: usa el trozo que
encaja con el tiempo de la secuencia. Este script hace el recorte del golpe:

  - el final de la pista (el último estribillo, el passage más fuerte) es lo que
    sonaba bajo el cántico. Con la plegaria en silencio, ese papel desaparece;
  - lo que entra ahora es el MISMO material, pero colocado en su sitio: el último
    estribillo empieza en su punto más fuerte, así que la música entra a todo
    volumen con el grito y se apaga sola con la explosión.

Uso:
    python Herramientas/Audio/recortar_incantacion.py --piloto
    python Herramientas/Audio/recortar_incantacion.py --instalar
"""
import io
import os
import subprocess
import sys

import numpy as np

RAIZ = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsp_audio import SR, decodificar, lufs, true_peak  # noqa: E402

ORIGEN = r"C:/Users/luarb/Downloads/Ultimo Incantazione.mp3"
DESTINO_JUEGO = os.path.join(RAIZ, "Assets", "Audio", "Music", "Cinematicas", "Ultimo Incantazione.mp3")
PILOTO = os.path.join(RAIZ, "Herramientas", "Audio", "Piloto", "IncantacionGrito.wav")

# El último estribillo empieza en su punto más fuerte: la entrada de la música es
# el clímax de la pieza y cae en el mismo frame que el grito.
INICIO = 149.00
# La pista acaba con la cola apagándose sola (el silencio después del estallido).
FIN = None  # hasta el final
FUNDIDO_ENTRADA = 0.010


def ffmpeg_bin():
    return os.environ.get("FFMPEG", "ffmpeg")


def escribir_mp3(ruta, x, bitrate="192k"):
    cmd = [ffmpeg_bin(), "-hide_banner", "-loglevel", "error", "-y",
           "-f", "f32le", "-ar", str(SR), "-ac", "2", "-i", "-",
           "-c:a", "libmp3lame", "-b:a", bitrate, ruta]
    p = subprocess.run(cmd, input=x.astype(np.float32).tobytes(), capture_output=True)
    if p.returncode != 0:
        raise RuntimeError("ffmpeg no pudo escribir el mp3: " + p.stderr.decode("utf-8", "ignore")[:300])


def main():
    if not os.path.exists(ORIGEN):
        print(f"No está el original: {ORIGEN}")
        print("El recorte se hace sobre la generación de Suno completa (159 s).")
        return 1

    x = decodificar(ORIGEN)
    total = len(x) / SR
    fin = FIN if FIN is not None else total
    i = int(INICIO * SR)
    j = int(fin * SR)
    trozo = x[i:j].copy()

    # Fundido de entrada mínimo: sin chasquido al empezar el golpe.
    n = max(1, int(FUNDIDO_ENTRADA * SR))
    curva = np.linspace(0.0, 1.0, n, dtype=np.float32)[:, None]
    trozo[:n] *= curva

    print(f"Origen {ORIGEN}")
    print(f"  {total:.2f} s -> recorte {INICIO:.2f}-{fin:.2f} s = {len(trozo)/SR:.2f} s")
    print(f"  LUFS {lufs(trozo):.1f} | true peak {true_peak(trozo):.1f} dBTP")

    modo = "piloto"
    if "--instalar" in sys.argv:
        modo = "instalar"

    if modo == "piloto":
        from dsp_audio import escribir
        os.makedirs(os.path.dirname(PILOTO), exist_ok=True)
        escribir(PILOTO, trozo)
        print(f"  piloto: {PILOTO}")
    else:
        os.makedirs(os.path.dirname(DESTINO_JUEGO), exist_ok=True)
        escribir_mp3(DESTINO_JUEGO, trozo)
        print(f"  juego: {DESTINO_JUEGO} ({os.path.getsize(DESTINO_JUEGO)/1024:.0f} kB)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
