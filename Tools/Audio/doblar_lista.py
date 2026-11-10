"""Dobla una lista de frases (CSV) con el reparto de docs/doblaje-sendero/reparto.csv.

Sirve para cualquier tanda nueva: no hace falta un script por escena. La lista es un CSV con
las columnas clave, personaje, texto_enviado (con etiquetas de ElevenLabs: [sad], [teasing]…) y
texto_subtitulo. El reparto dice qué voz y qué ajustes lleva cada personaje; si un personaje
aún no tiene voz elegida, sus frases se saltan y se avisa.

Uso (desde la raíz del proyecto, en Windows):

    python Tools\\Audio\\doblar_lista.py docs\\doblaje-sendero\\lineas_cap3_cap4.csv --cuanto
    python Tools\\Audio\\doblar_lista.py docs\\doblaje-sendero\\lineas_cap3_cap4.csv --faltan --max 25
    python Tools\\Audio\\doblar_lista.py docs\\doblaje-sendero\\lineas_cap3_cap4.csv --solo CAP4_CAMPANA_01 --tomas 3

--cuanto   no genera nada: dice cuántas frases faltan, cuántos créditos costarían y cuántos quedan.
--faltan   solo las frases que aún no tienen .mp3 en Assets/Resources/Voices/es.
--max N    como mucho N frases en esta llamada (tandas de ~25 para no pasar de unos 3 minutos).

La toma 1 se normaliza (-18 LUFS, techo -2 dBTP) y se coloca como CLAVE.mp3; todas las tomas quedan
en «Versiones antiguas/Voces <lista> raw/» para escucharlas y cambiar a mano. Cada frase colocada
se apunta en docs/doblaje-cap1/registro_doblaje.csv en estado «revisar» (falta oírla en el juego).
Después: volver a Unity y hornear los guiones de esas cinemáticas para que tomen la duración real.
"""
import argparse
import csv
import json
import os
import shutil
import subprocess
import sys
import time
import urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from redoblar_final import NORMALIZADOR, RAIZ, VOCES, clave_de_api, generar  # noqa: E402

REPARTO = os.path.join(RAIZ, "docs", "doblaje-sendero", "reparto.csv")
REGISTRO = os.path.join(RAIZ, "docs", "doblaje-cap1", "registro_doblaje.csv")


def leer_reparto():
    reparto = {}
    with open(REPARTO, encoding="utf-8-sig") as f:
        for r in csv.DictReader(f):
            if not r["voice_id"].strip():
                continue
            reparto[r["personaje"]] = {
                "nombre": r["voz_elevenlabs"], "id": r["voice_id"].strip(),
                "ajustes": {"stability": float(r["estabilidad"]), "similarity_boost": float(r["similitud"]),
                            "style": float(r["estilo"]), "use_speaker_boost": True},
            }
    return reparto


def creditos(clave):
    peticion = urllib.request.Request("https://api.elevenlabs.io/v1/user/subscription", headers={"xi-api-key": clave})
    with urllib.request.urlopen(peticion, timeout=30) as r:
        s = json.load(r)
    return s["character_limit"] - s["character_count"], time.strftime("%d/%m/%Y", time.localtime(s.get("next_character_count_reset_unix", 0)))


def apuntar(linea, voz, toma, ruta):
    ajustes = voz["ajustes"]
    fila = [linea["clave"], linea["personaje"], voz["nombre"], voz["id"], linea["texto_enviado"],
            f"estab {round(ajustes['stability'] * 100)}, simil {round(ajustes['similarity_boost'] * 100)}, Eleven v4",
            toma, "", "revisar", f"doblar_lista.py ({time.strftime('%Y-%m-%d')}); falta escucharla en el juego",
            os.path.relpath(ruta, RAIZ).replace("\\", "/")]
    with open(REGISTRO, "a", newline="", encoding="utf-8") as f:
        csv.writer(f).writerow(fila)


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("lista", help="CSV con clave, personaje, texto_enviado, texto_subtitulo")
    p.add_argument("--tomas", type=int, default=1, help="tomas por frase (la 1 se coloca)")
    p.add_argument("--solo", nargs="*", default=None, help="claves a doblar (por defecto, todas)")
    p.add_argument("--faltan", action="store_true", help="solo las que no tienen .mp3")
    p.add_argument("--max", type=int, default=0, help="como mucho N frases en esta llamada")
    p.add_argument("--cuanto", action="store_true", help="no genera: cuenta frases y créditos")
    a = p.parse_args()

    with open(a.lista, encoding="utf-8-sig") as f:
        lineas = list(csv.DictReader(f))
    reparto = leer_reparto()
    pendientes, sin_voz = [], {}
    for l in lineas:
        if a.solo and l["clave"] not in a.solo:
            continue
        if a.faltan and os.path.isfile(os.path.join(VOCES, l["clave"] + ".mp3")):
            continue
        if l["personaje"] not in reparto:
            sin_voz[l["personaje"]] = sin_voz.get(l["personaje"], 0) + 1
            continue
        pendientes.append(l)
    for quien, n in sin_voz.items():
        print(f"AVISO: {quien} no tiene voz en reparto.csv; se saltan sus {n} frases.")
    if a.max:
        pendientes = pendientes[:a.max]

    clave = clave_de_api()
    coste = sum(len(l["texto_enviado"]) for l in pendientes) * max(1, a.tomas)
    quedan, renueva = creditos(clave)
    print(f"{len(pendientes)} frases, unos {coste} créditos. Quedan {quedan} (se renuevan el {renueva}).")
    if a.cuanto or not pendientes:
        return
    if coste > quedan:
        raise SystemExit("No hay créditos suficientes: espera a la renovación o baja --max / --tomas.")

    nombre = os.path.splitext(os.path.basename(a.lista))[0]
    tomas = os.path.join(RAIZ, "Versiones antiguas", f"Voces {nombre} raw")
    os.makedirs(tomas, exist_ok=True)
    colocadas = []
    for l in pendientes:
        voz = reparto[l["personaje"]]
        print(f"\n{l['clave']}  ({l['personaje']})")
        for n in range(1, max(1, a.tomas) + 1):
            audio = generar(clave, voz["id"], l["texto_enviado"], voz["ajustes"])
            if audio is None:
                print(f"  toma {n}: FALLÓ")
                continue
            with open(os.path.join(tomas, f"{l['clave']}_toma{n}.mp3"), "wb") as f:
                f.write(audio)
            print(f"  toma {n}: {len(audio) // 1024} KB")
            if n == 1:
                colocadas.append((l, voz))
            time.sleep(1.0)
    if not colocadas:
        print("\nNo se ha colocado ninguna toma.")
        return
    entorno = dict(os.environ)
    if "FFMPEG" not in entorno and shutil.which("ffmpeg") and os.name != "nt":
        entorno["FFMPEG"] = shutil.which("ffmpeg")
    subprocess.call([sys.executable, NORMALIZADOR, "--aplicar", "--carpeta", os.path.relpath(tomas, RAIZ),
                     "--objetivo", "-18", "--techo", "-2"], cwd=RAIZ, env=entorno)
    for l, voz in colocadas:
        destino = os.path.join(VOCES, l["clave"] + ".mp3")
        shutil.copy2(os.path.join(tomas, f"{l['clave']}_toma1.mp3"), destino)
        apuntar(l, voz, 1, destino)
    print(f"\nListo: {len(colocadas)} voces colocadas y apuntadas en el registro. Vuelve a Unity y hornea los guiones.")


if __name__ == "__main__":
    main()
