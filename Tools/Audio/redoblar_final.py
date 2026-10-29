"""Dobla las frases nuevas y las que hay que repetir de la cinemática final (INC-864, INC-865).

Repetir (el audio no coincide con el subtítulo o le falta intención):
    FINAL_ESTELA_RETURN_01  «Idiota... No te atrevas a volver a hacer algo así.»
    FINAL_ESTELA_RIO_02     la historia del sirope entera.
    FINAL_ESTELA_RIO_01     «¿Te han contado lo del sirope?», con retintín.

Nuevas:
    El portal ......... «No... no...» y el grito «¡¡WIIIIILL!!» de Estela.
    El regreso ........ «¿Will...?» y «¡¡WILL!!» de Estela antes de la carrera; «Prometido.» de Will.
    El río ............ «¡¡WILL!!» de Estela, furiosa, saliendo del agua; «Oh, oh...» de Tobías.
    La noche .......... «¿Eh? ¿Y mi estrella...?» y «Se la quedó él...» de Will, y las tres frases
                        con las que Will empieza a contarle la historia a Tobías.

Uso (desde la raíz del proyecto, en Windows):

    python Tools\\Audio\\redoblar_final.py            # genera, normaliza y coloca todas
    python Tools\\Audio\\redoblar_final.py --tomas 3  # tres tomas de cada una para elegir
    python Tools\\Audio\\redoblar_final.py --solo FINAL_WILL_NOCHE_03   # solo esa frase

Con varias tomas, la 1 se coloca en Assets/Resources/Voices/es y el resto se quedan en
«Versiones antiguas/Voces Final raw (2026-10-29)/» para escucharlas y cambiarlas a mano.
Los originales se guardan antes en «Versiones antiguas/Voces antes de redoblaje Final (2026-10-29)/».
La clave de ElevenLabs se lee de Tools/Audio/generar_hechizo_slayers.py (la misma de siempre).
FINAL_WILL_RIO_03 es la mezcla de Will y Liam hablando a la vez (Liam entra 0,35 s después):
si se vuelven a doblar FINAL_WILL_RIO_03 o FINAL_LIAM_RIO_03, hay que volver a mezclarlas (las
tomas sueltas están en «Versiones antiguas/Sonidos fuente Final (2026-10-29)/»).
FINAL_ESTELA_RIO_02 (la historia del sirope) se usa partida en tres, FINAL_ESTELA_RIO_02A/B/C, para
que Estela la vaya representando: si se vuelve a doblar, hay que volver a partirla por sus pausas.
Al terminar, añadir las tomas elegidas a docs/doblaje-cap1/registro_doblaje.csv y volver a hornear
las dos partes del final (las frases nuevas duran lo que dure su voz).
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
ORIGINALES = os.path.join(RAIZ, "Versiones antiguas", os.environ.get("CARPETA_ORIGINALES", "Voces antes de redoblaje Final (2026-10-29)"))
TOMAS = os.path.join(RAIZ, "Versiones antiguas", os.environ.get("CARPETA_TOMAS", "Voces Final raw (2026-10-29)"))
NORMALIZADOR = os.path.join(RAIZ, "Herramientas", "Audio", "normalizar_assets.py")

VOZ_ESTELA = "9MKgqtaIyv28bwBeybox"  # Estela - Lina candidata 1 (registro_doblaje.csv)
VOZ_WILL = "JwXx2SOucScqglxgpL6T"    # Will - Anime España 1.2
VOZ_TOBIAS = "1tDEBGOo8EqEPApM49eJ"  # Tobías - Leo, niño
VOZ_LIAM = "hwazPFcispEO8ANIs7LE"    # Liam - Anime candidata 1
VOZ_ARCHIMAGO = "JOlPriKZ6hldygMIxHSm"  # Archimago - Anime candidata 2

AJUSTES = {"stability": 0.35, "similarity_boost": 0.85, "style": 0.25, "use_speaker_boost": True}
AJUSTES_TOBIAS = {"stability": 0.40, "similarity_boost": 0.80, "style": 0.25, "use_speaker_boost": True}
AJUSTES_LIAM = {"stability": 0.45, "similarity_boost": 0.85, "style": 0.25, "use_speaker_boost": True}
AJUSTES_ARCHIMAGO = {"stability": 0.50, "similarity_boost": 0.85, "style": 0.25, "use_speaker_boost": True}
AJUSTES_WILL = {"stability": 0.40, "similarity_boost": 0.85, "style": 0.25, "use_speaker_boost": True}

LINEAS = [
    # ── Repetir ──
    {"clave": "FINAL_ESTELA_RETURN_01", "voz": VOZ_ESTELA,
     "texto": "[sniffling] [teary laugh] [scolding softly] Idiota... No te atrevas a volver a hacer algo así."},
    {"clave": "FINAL_ESTELA_RIO_02", "voz": VOZ_ESTELA,
     "texto": "[storytelling] [playful] [to a child] Verás, Tobi: había una fiesta enorme, con una fuente de sirope así de grande. "
              "Y Güil, corriendo, se tropezó... [giggles] ¡y se lió en el mantel como si fuera una novia! "
              "[building up] Fue rodando, rodando... y acabó encima de Liam. [laughing] ¡Justo debajo de un cartel que ponía «¡TE QUIERO!»!"},
    {"clave": "FINAL_ESTELA_RIO_01", "voz": VOZ_ESTELA,
     "texto": "[sly] [mischievous] [sing-song] ¿Te han contado... lo del sirope?"},
    # ── El portal ──
    {"clave": "FINAL_ESTELA_PORTAL_04", "voz": VOZ_ESTELA,
     "texto": "[in shock] [voice breaking] [starting to sob] No... no... [sobbing]"},
    {"clave": "FINAL_ESTELA_PORTAL_05", "voz": VOZ_ESTELA,
     "texto": "[screaming] [desperate] [heartbroken] [voice cracking] ¡¡GÜIIIIIL!! [breaks down crying] [sobbing uncontrollably]"},
    # ── El regreso ──
    {"clave": "FINAL_ESTELA_RETURN_00", "voz": VOZ_ESTELA,
     "texto": "[whispering] [disbelief] [teary] ¿Güil...?"},
    {"clave": "FINAL_ESTELA_RETURN_00B", "voz": VOZ_ESTELA,
     "texto": "[shouting] [crying with joy] ¡¡GÜIL!!"},
    {"clave": "FINAL_WILL_RETURN_04", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[warm] [smiling] [softly] Prometido."},
    # ── El río ──
    {"clave": "FINAL_ESTELA_RIO_00", "voz": VOZ_ESTELA,
     "texto": "[furious] [shouting] [soaking wet] ¡¡GÜIL!!"},
    {"clave": "FINAL_TOBIAS_RIO_02", "voz": VOZ_TOBIAS, "ajustes": AJUSTES_TOBIAS,
     "texto": "[excited] [proud] [childish] ¡Mira, Güil! Te hice un dibujo mientras estabais de viaje... "
              "[shy] [giggles] Bueno, todavía no te conocía mucho."},
    {"clave": "FINAL_TOBIAS_RIO_04", "voz": VOZ_TOBIAS, "ajustes": AJUSTES_TOBIAS,
     "texto": "[nervous] [childish] [uh-oh] Oh, oh..."},
    {"clave": "FINAL_ESTELA_RIO_00B", "voz": VOZ_ESTELA,
     "texto": "[annoyed] [pushing] [huffy] ¡Aparta!"},
    {"clave": "FINAL_WILL_RIO_03", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[flustered] [embarrassed] [fast] ¡Eso no fue así! ¡Me resbalé con el sirope!"},
    {"clave": "FINAL_LIAM_RIO_03", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[flustered] [panicked] [fast] ¡Que no, que no! ¡Yo solo pasaba por allí!"},
    {"clave": "FINAL_LIAM_RETURN_00", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[disbelief] [teary] [voice trembling] ¿De verdad... eres tú?"},
    {"clave": "FINAL_WILL_RIO_04", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[touched] [happy] [warm] ¡Gracias, Tobi! Me encanta."},
    {"clave": "FINAL_ESTELA_RIO_04", "voz": VOZ_ESTELA,
     "texto": "[teasing] [sing-song] [from a distance] ¡Oy, qué guapo, Güil!"},
    {"clave": "FINAL_LIAM_RIO_04", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[teasing] [mischievous] Tobías, dibuja a Estela."},
    {"clave": "FINAL_ESTELA_RIO_05", "voz": VOZ_ESTELA,
     "texto": "[mocking] [childish] [nasal] Mi mi mi mi..."},
    # ── La noche ──
    {"clave": "FINAL_WILL_NOCHE_00", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[puzzled] [softly] ¿Eh? ¿Y mi estrella...?"},
    {"clave": "FINAL_WILL_NOCHE_03", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[moved] [quiet smile] [whispering] Se la quedó él..."},
    {"clave": "FINAL_WILL_HISTORIA_01", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[storyteller] [warm] [nostalgic] Eldran se la había dejado en el bosque, debajo de un árbol, "
              "y me tocó a mí ir a buscarla."},
    {"clave": "FINAL_WILL_HISTORIA_02", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[storyteller] [mysterious] [warm] Y allí, en mitad del bosque, me pasó algo que todavía no me creo."},
    {"clave": "FINAL_WILL_HISTORIA_03", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[storyteller] [softly] [fading] ...y desde aquel día, nada volvió a ser igual."},
    # ── «Will» dicho a la española («Güil»): el resto de frases del final que le llaman ──
    {"clave": "FINAL_ESTELA_PORTAL_02", "voz": VOZ_ESTELA,
     "texto": "[desperate] [crying] ¡No! ¡Cuídalo tú! ¡Güil, sal de ahí ahora mismo!"},
    {"clave": "FINAL_LIAM_AWAKE_02", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[tense] [demanding] [worried] ¿Dónde está Güil?"},
    {"clave": "FINAL_ARCHIMAGO_01", "voz": VOZ_ARCHIMAGO, "ajustes": AJUSTES_ARCHIMAGO,
     "texto": "[warm] [solemn] [gentle] Lo siento. No soy Güil."},
]


def clave_de_api():
    origen = os.path.join(RAIZ, "Tools", "Audio", "generar_hechizo_slayers.py")
    texto = open(origen, encoding="utf-8").read()
    m = re.search(r'API_KEY\s*=\s*"([^"]+)"', texto)
    if not m:
        raise SystemExit(f"No encuentro API_KEY en {origen}")
    return m.group(1)


def generar(clave_api, voz, texto, ajustes):
    url = f"https://api.elevenlabs.io/v1/text-to-speech/{voz}?output_format=mp3_44100_192"
    cuerpo = {"text": texto, "model_id": "eleven_v4", "language_code": "es", "voice_settings": ajustes}
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
    p.add_argument("--solo", nargs="*", default=None, help="claves a doblar (por defecto, todas)")
    args = p.parse_args()

    os.makedirs(ORIGINALES, exist_ok=True)
    os.makedirs(TOMAS, exist_ok=True)
    clave_api = clave_de_api()

    colocadas = []
    for linea in LINEAS:
        clave = linea["clave"]
        if args.solo and clave not in args.solo:
            continue
        print(f"\n{clave}")
        destino = os.path.join(VOCES, clave + ".mp3")
        copia = os.path.join(ORIGINALES, clave + ".mp3")
        if os.path.exists(destino) and not os.path.exists(copia):
            shutil.copy2(destino, copia)
            print(f"  original guardado en {copia}")

        for n in range(1, max(1, args.tomas) + 1):
            audio = generar(clave_api, linea["voz"], linea["texto"], linea.get("ajustes", AJUSTES))
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
