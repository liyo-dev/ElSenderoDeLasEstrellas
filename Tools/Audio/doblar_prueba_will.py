"""Dobla las 27 frases de los recuerdos de la Prueba de Will (PRUEBA_WILL_*, INC-876).

Voces (docs/doblaje-sendero/casting y registro_doblaje.csv):
    Will, Estela, Liam ... las de siempre (redoblar_final.py).
    Will niño ........... Leo (la misma voz que Tobías), elegida por Raúl.
    Aldona .............. Rosa - Neutral, Calm and Genuine.
    Chico mayor 1 ....... Jota - peninsular joven.
    Chico mayor 2 ....... David - acento andaluz (además se guarda una toma con Jota para comparar).

Uso (desde la raíz del proyecto):

    python Tools/Audio/doblar_prueba_will.py                 # genera, normaliza y coloca todas
    python Tools/Audio/doblar_prueba_will.py --tomas 3       # tres tomas de cada una
    python Tools/Audio/doblar_prueba_will.py --solo PRUEBA_WILL_CRIO_02
    python Tools/Audio/doblar_prueba_will.py --faltan        # solo las que aún no están colocadas

La toma 1 se coloca en Assets/Resources/Voices/es; todas las tomas quedan en
«Versiones antiguas/Voces Prueba de Will raw (2026-10-30)/» para escucharlas y cambiarlas a mano.
Al terminar, añadir las tomas elegidas a docs/doblaje-cap1/registro_doblaje.csv.
"""
import argparse
import os
import shutil
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from redoblar_final import (AJUSTES, AJUSTES_LIAM, AJUSTES_WILL, NORMALIZADOR, RAIZ, VOCES,  # noqa: E402
                            VOZ_ESTELA, VOZ_LIAM, VOZ_WILL, clave_de_api, generar)

TOMAS = os.path.join(RAIZ, "Versiones antiguas", "Voces Prueba de Will raw (2026-10-30)")

VOZ_WILL_NINO = "1tDEBGOo8EqEPApM49eJ"  # Leo (la voz de Tobías)
VOZ_ALDONA = "ypIbR1aohyRSdDv25DPr"     # Rosa - Neutral, Calm and Genuine
VOZ_CHICO_1 = "d5UAjYhvzEX3wkRfxPTM"    # Jota - peninsular joven
VOZ_CHICO_2 = "LG5CpCuzWTE7pUsAnHo2"    # David - acento andaluz

AJUSTES_NINO = {"stability": 0.40, "similarity_boost": 0.80, "style": 0.30, "use_speaker_boost": True}
AJUSTES_ALDONA = {"stability": 0.50, "similarity_boost": 0.85, "style": 0.20, "use_speaker_boost": True}
AJUSTES_CHICO = {"stability": 0.35, "similarity_boost": 0.80, "style": 0.40, "use_speaker_boost": True}

LINEAS = [
    # ── El discurso del cumpleaños ──
    {"clave": "PRUEBA_WILL_DISCURSO_01", "voz": VOZ_WILL_NINO, "ajustes": AJUSTES_NINO,
     "texto": "[nervous] [stuttering] [shy] [childish] Muchas gracias por... por... por venir a mi..."},
    {"clave": "PRUEBA_WILL_DISCURSO_02", "voz": VOZ_WILL_NINO, "ajustes": AJUSTES_NINO,
     "texto": "[stuttering] [embarrassed] [childish] A mi cum... cumple... [quietly] por venir."},
    {"clave": "PRUEBA_WILL_DISCURSO_03", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[teasing] [amused] [softly] Ay, qué mono. Un idiota en miniatura."},
    {"clave": "PRUEBA_WILL_DISCURSO_04", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[embarrassed] [defensive] [flustered] ¡Solo tenía nueve años!"},
    {"clave": "PRUEBA_WILL_DISCURSO_05", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[gentle] [dry humor] Unas palabras torpes no matan a nadie. [chuckles] Aunque estas casi."},
    # ── La pesadilla ──
    {"clave": "PRUEBA_WILL_PESADILLA_01", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[firm] [reassuring] [close] Respira, idiota. Eso no te puede tocar."},
    # ── El crío ──
    {"clave": "PRUEBA_WILL_CRIO_01", "voz": VOZ_CHICO_1, "ajustes": AJUSTES_CHICO,
     "texto": "[mocking] [bullying] [taunting] ¿Esto es tuyo, enano? Pues ahora es nuestro."},
    {"clave": "PRUEBA_WILL_CRIO_02", "voz": VOZ_CHICO_2, "ajustes": AJUSTES_CHICO,
     "texto": "[mocking] [cruel] [laughing] Venga, llora, a ver si viene el viejo ese."},
    {"clave": "PRUEBA_WILL_CRIO_03", "voz": VOZ_CHICO_1, "ajustes": AJUSTES_CHICO,
     "texto": "[cruel] [sneering] Si hasta tus padres te dejaron tirado."},
    {"clave": "PRUEBA_WILL_CRIO_04", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[quiet] [guilty] [pained] No pude hacer nada."},
    {"clave": "PRUEBA_WILL_CRIO_05", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[softly] [tender] [firm] Tenías ocho años."},
    # ── La leña de Aldona ──
    {"clave": "PRUEBA_WILL_LENA_01", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[surprised] [playful] Anda. Mira quién se esconde."},
    {"clave": "PRUEBA_WILL_LENA_02", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[evasive] [embarrassed] No sé de qué habláis."},
    {"clave": "PRUEBA_WILL_LENA_03", "voz": VOZ_ALDONA, "ajustes": AJUSTES_ALDONA,
     "texto": "[warm] [knowing] [gentle] Llevo semanas sabiendo que eres tú. No hacía falta que te escondieras, muchacho."},
    {"clave": "PRUEBA_WILL_LENA_04", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[shy] [awkward] No quería que se sintiera en deuda. Solo es leña."},
    {"clave": "PRUEBA_WILL_LENA_05", "voz": VOZ_ALDONA, "ajustes": AJUSTES_ALDONA,
     "texto": "[moved] [softly] [tender] Para mí no es solo leña."},
    {"clave": "PRUEBA_WILL_LENA_06", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[surprised] [impressed] [teasing] Y esto no nos lo habías contado."},
    {"clave": "PRUEBA_WILL_LENA_07", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[modest] [brushing it off] No hay nada que contar. Cualquiera hubiera hecho lo mismo."},
    {"clave": "PRUEBA_WILL_LENA_08", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[softly] [touched] [firm] No. No cualquiera lo hubiera hecho, idiota."},
    # ── El primer día ──
    {"clave": "PRUEBA_WILL_PRIMERDIA_01", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[quietly] [gentle] ¿Ese fue el primer día?"},
    {"clave": "PRUEBA_WILL_PRIMERDIA_02", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[quiet] [heavy] Sí."},
    {"clave": "PRUEBA_WILL_PRIMERDIA_03", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[gentle] [kneeling] [to a child] No pasa nada por tener miedo. Yo todavía lo tengo."},
    {"clave": "PRUEBA_WILL_PRIMERDIA_04", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[gentle] [reassuring] [firm] Pero esto no te va a destruir."},
    {"clave": "PRUEBA_WILL_PRIMERDIA_05", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[warm] [wry smile] Vas a llegar tarde a cosas que importan..."},
    {"clave": "PRUEBA_WILL_PRIMERDIA_06", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[warm] [smiling] ...y también vas a dejar leña en la puerta de gente que se va a dar cuenta."},
    {"clave": "PRUEBA_WILL_PRIMERDIA_07", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[surprised] [moved] [softly] Eso no me lo esperaba."},
    {"clave": "PRUEBA_WILL_PRIMERDIA_08", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[soft laugh] [quiet] Yo tampoco."},
]

# Tomas extra que no se colocan: solo para comparar de oído.
COMPARAR = [
    {"clave": "PRUEBA_WILL_CRIO_02", "sufijo": "_con_Jota", "voz": VOZ_CHICO_1},
]


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--tomas", type=int, default=1, help="tomas por frase (la 1 se coloca)")
    p.add_argument("--solo", nargs="*", default=None, help="claves a doblar (por defecto, todas)")
    p.add_argument("--faltan", action="store_true", help="salta las frases que ya están en Voices/es")
    args = p.parse_args()

    os.makedirs(TOMAS, exist_ok=True)
    os.makedirs(VOCES, exist_ok=True)
    clave_api = clave_de_api()
    por_clave = {l["clave"]: l for l in LINEAS}

    colocadas = []
    for linea in LINEAS:
        clave = linea["clave"]
        if args.solo and clave not in args.solo:
            continue
        if args.faltan and os.path.exists(os.path.join(VOCES, clave + ".mp3")):
            continue
        print(f"\n{clave}")
        for n in range(1, max(1, args.tomas) + 1):
            audio = generar(clave_api, linea["voz"], linea["texto"], linea["ajustes"])
            if audio is None:
                print(f"  toma {n}: FALLÓ")
                continue
            toma = os.path.join(TOMAS, f"{clave}_toma{n}.mp3")
            with open(toma, "wb") as f:
                f.write(audio)
            print(f"  toma {n}: {len(audio) // 1024} KB")
            if n == 1:
                colocadas.append(clave)
            time.sleep(1.0)

    for extra in COMPARAR:
        if args.solo and extra["clave"] not in args.solo:
            continue
        if os.path.exists(os.path.join(TOMAS, f"{extra['clave']}{extra['sufijo']}.mp3")):
            continue
        linea = por_clave[extra["clave"]]
        audio = generar(clave_api, extra["voz"], linea["texto"], linea["ajustes"])
        if audio:
            with open(os.path.join(TOMAS, f"{extra['clave']}{extra['sufijo']}.mp3"), "wb") as f:
                f.write(audio)
            print(f"\n{extra['clave']}{extra['sufijo']} (para comparar)")

    if not colocadas:
        print("\nNo se ha colocado ninguna toma.")
        return

    # Mismo estándar que el resto de voces: -18 LUFS, techo -2 dBTP.
    print("\nNormalizando las tomas (-18 LUFS, -2 dBTP)...")
    entorno = dict(os.environ)
    if "FFMPEG" not in entorno and shutil.which("ffmpeg") and os.name != "nt":
        entorno["FFMPEG"] = shutil.which("ffmpeg")  # fuera de Windows, el ffmpeg del sistema
    subprocess.call([sys.executable, NORMALIZADOR, "--aplicar", "--carpeta",
                     os.path.relpath(TOMAS, RAIZ), "--objetivo", "-18", "--techo", "-2"], cwd=RAIZ, env=entorno)
    for clave in colocadas:
        shutil.copy2(os.path.join(TOMAS, f"{clave}_toma1.mp3"), os.path.join(VOCES, clave + ".mp3"))
    print(f"\nListo: {len(colocadas)} voces colocadas. Vuelve a Unity para que las reimporte.")


if __name__ == "__main__":
    main()
