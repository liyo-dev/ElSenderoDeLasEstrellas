"""Redobla las dos frases de la cinemática final cuyo audio no coincide con el subtítulo (INC-864).

    FINAL_ESTELA_RETURN_01  el audio decía «Te la ha cambiado por una vida. Sales ganando.»
                            (la frase de la noche) en vez de «Idiota... No te atrevas a volver
                            a hacer algo así.»
    FINAL_ESTELA_RIO_02     el audio solo decía «Pues te lo cuento yo...» y el subtítulo cuenta
                            toda la historia del sirope.

Uso (desde la raíz del proyecto, en Windows):

    python Tools\\Audio\\redoblar_final.py            # genera, normaliza y coloca las dos
    python Tools\\Audio\\redoblar_final.py --tomas 3  # tres tomas de cada una para elegir

Con varias tomas, la 1 se coloca en Assets/Resources/Voices/es y el resto se quedan en
«Versiones antiguas/Voces Final raw (2026-10-29)/» para escucharlas y cambiarlas a mano.
Los originales se guardan antes en «Versiones antiguas/Voces antes de redoblaje Final (2026-10-29)/».
La clave de ElevenLabs se lee de Tools/Audio/generar_hechizo_slayers.py (la misma de siempre).
Al terminar, añadir las tomas elegidas a docs/doblaje-cap1/registro_doblaje.csv.
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
VOCES = os.path.join(RAIZ, "Assets", "Resources", "Voices", "es")
ORIGINALES = os.path.join(RAIZ, "Versiones antiguas", "Voces antes de redoblaje Final (2026-10-29)")
TOMAS = os.path.join(RAIZ, "Versiones antiguas", "Voces Final raw (2026-10-29)")
NORMALIZADOR = os.path.join(RAIZ, "Herramientas", "Audio", "normalizar_assets.py")

VOZ_ESTELA = "9MKgqtaIyv28bwBeybox"  # Estela - Lina candidata 1 (registro_doblaje.csv)

LINEAS = [
    {
        "clave": "FINAL_ESTELA_RETURN_01",
        "voz": VOZ_ESTELA,
        "texto": "[sniffling] [teary laugh] [scolding softly] Idiota... No te atrevas a volver a hacer algo así.",
    },
    {
        "clave": "FINAL_ESTELA_RIO_02",
        "voz": VOZ_ESTELA,
        "texto": "[teasing] [giggling] [storytelling] Pues te lo cuento yo: la fuente de sirope, "
                 "el mantel como vestido de novia, y el cartel de «¡TE QUIERO!» encima de Liam.",
    },
]

# Mismos ajustes que el redoblaje anime de Estela (estab 35, simil 85, Eleven v4).
AJUSTES = {"stability": 0.35, "similarity_boost": 0.85, "style": 0.25, "use_speaker_boost": True}


def clave_de_api():
    origen = os.path.join(RAIZ, "Tools", "Audio", "generar_hechizo_slayers.py")
    texto = open(origen, encoding="utf-8").read()
    m = re.search(r'API_KEY\s*=\s*"([^"]+)"', texto)
    if not m:
        raise SystemExit(f"No encuentro API_KEY en {origen}")
    return m.group(1)


def generar(clave_api, voz, texto):
    url = f"https://api.elevenlabs.io/v1/text-to-speech/{voz}?output_format=mp3_44100_192"
    cuerpo = {"text": texto, "model_id": "eleven_v4", "language_code": "es", "voice_settings": AJUSTES}
    peticion = urllib.request.Request(
        url, data=json.dumps(cuerpo).encode("utf-8"), method="POST",
        headers={"xi-api-key": clave_api, "Content-Type": "application/json; charset=utf-8"})
    for intento in range(3):
        try:
            with urllib.request.urlopen(peticion, timeout=90) as r:
                return r.read()
        except urllib.error.HTTPError as e:
            print(f"    HTTP {e.code}: {e.read().decode('utf-8', 'ignore')[:300]}")
            if e.code != 429:
                return None
            time.sleep(6)
        except Exception as e:
            print(f"    Error: {e}")
            time.sleep(2)
    return None


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--tomas", type=int, default=1, help="tomas por frase (la 1 se coloca)")
    args = p.parse_args()

    os.makedirs(ORIGINALES, exist_ok=True)
    os.makedirs(TOMAS, exist_ok=True)
    clave_api = clave_de_api()

    colocadas = []
    for linea in LINEAS:
        clave = linea["clave"]
        print(f"\n{clave}")
        destino = os.path.join(VOCES, clave + ".mp3")
        copia = os.path.join(ORIGINALES, clave + ".mp3")
        if os.path.exists(destino) and not os.path.exists(copia):
            shutil.copy2(destino, copia)
            print(f"  original guardado en {copia}")

        for n in range(1, max(1, args.tomas) + 1):
            audio = generar(clave_api, linea["voz"], linea["texto"])
            if audio is None:
                print(f"  toma {n}: FALLÓ")
                continue
            toma = os.path.join(TOMAS, f"{clave}_toma{n}.mp3")
            with open(toma, "wb") as f:
                f.write(audio)
            print(f"  toma {n}: {len(audio) // 1024} KB -> {toma}")
            if n == 1:
                shutil.copy2(toma, destino)
                colocadas.append(destino)
            time.sleep(1.0)

    if not colocadas:
        print("\nNo se ha colocado ninguna toma.")
        return

    # Mismo estándar que el resto de voces: -18 LUFS, techo -2 dBTP.
    print("\nNormalizando las tomas (-18 LUFS, -2 dBTP)...")
    subprocess.call([sys.executable, NORMALIZADOR, "--aplicar", "--carpeta",
                     os.path.relpath(TOMAS, RAIZ), "--objetivo", "-18", "--techo", "-2"], cwd=RAIZ)
    for destino in colocadas:
        clave = os.path.splitext(os.path.basename(destino))[0]
        shutil.copy2(os.path.join(TOMAS, f"{clave}_toma1.mp3"), destino)
    print("\nListo. Vuelve a Unity para que reimporte las voces y escucha la cinemática.")


if __name__ == "__main__":
    main()
