"""Casting de voces: busca en la biblioteca pública de ElevenLabs y hace que cada candidata diga una
frase del personaje, para elegir de oído.

No añade nada a la cuenta (la API deja usar voces de la biblioteca por su id). La elegida se apunta
en el registro de doblaje con su id y se usa igual que las demás.

Uso (desde la raíz del proyecto):

    python Tools\\Audio\\buscar_voces.py --rol "Aldona" --genero female --edad old --texto "Para mí no es solo leña."
    python Tools\\Audio\\buscar_voces.py --rol "Will niño" --buscar niño --max 6 --texto "Muchas gracias por... por venir."

Con --disenar "descripción" no busca en la biblioteca: pide a ElevenLabs (diseño de voz) tres voces
nuevas que encajen con la descripción y guarda lo que dicen. Para quedarse con una hay que guardarla
en la cuenta con su generated_voice_id (python Tools\\Audio\\buscar_voces.py --guardar ID --nombre "…").
Con --ids se prueban voces concretas de la biblioteca en vez de buscarlas.

Las tomas quedan en docs/doblaje-sendero/casting/<rol>/ con un indice.csv (nombre, acento, edad,
descripción, id de la voz). Se prefieren las voces con acento peninsular; si no hay bastantes, se
completan con las demás.
"""
import argparse
import base64
import csv
import json
import os
import re
import urllib.parse
import urllib.request

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CASTING = os.path.join(RAIZ, "docs", "doblaje-sendero", "casting")


def clave_de_api():
    origen = os.path.join(RAIZ, "Tools", "Audio", "generar_hechizo_slayers.py")
    m = re.search(r'API_KEY\s*=\s*"([^"]+)"', open(origen, encoding="utf-8").read())
    if not m:
        raise SystemExit(f"No encuentro API_KEY en {origen}")
    return m.group(1)


def buscar(clave, **filtros):
    params = {k: v for k, v in filtros.items() if v}
    url = "https://api.elevenlabs.io/v1/shared-voices?" + urllib.parse.urlencode(params)
    peticion = urllib.request.Request(url, headers={"xi-api-key": clave})
    with urllib.request.urlopen(peticion, timeout=30) as r:
        return json.load(r).get("voices", [])


def decir(clave, voz, texto):
    cuerpo = json.dumps({"text": texto, "model_id": "eleven_v4", "language_code": "es"}).encode("utf-8")
    url = f"https://api.elevenlabs.io/v1/text-to-speech/{voz}?output_format=mp3_44100_128"
    peticion = urllib.request.Request(url, data=cuerpo, headers={"xi-api-key": clave, "Content-Type": "application/json"})
    with urllib.request.urlopen(peticion, timeout=120) as r:
        return r.read()


def peticion_json(clave, ruta, cuerpo):
    url = "https://api.elevenlabs.io" + ruta
    peticion = urllib.request.Request(url, data=json.dumps(cuerpo).encode("utf-8"),
                                      headers={"xi-api-key": clave, "Content-Type": "application/json"})
    with urllib.request.urlopen(peticion, timeout=180) as r:
        return json.load(r)


def disenar(clave, descripcion, texto):
    """Tres voces nuevas para la descripción, diciendo el texto (100-1000 caracteres)."""
    if len(texto) < 100:
        texto = (texto + " ") * (100 // max(1, len(texto)) + 1)
    datos = peticion_json(clave, "/v1/text-to-voice/design",
                          {"voice_description": descripcion, "text": texto[:1000], "model_id": "eleven_ttv_v3"})
    return datos.get("previews", [])


def guardar(clave, generada, nombre, descripcion):
    datos = peticion_json(clave, "/v1/text-to-voice",
                          {"voice_name": nombre, "voice_description": descripcion or nombre, "generated_voice_id": generada})
    print(f"Guardada «{nombre}»: voice_id {datos.get('voice_id')}")


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--rol", help="Carpeta del personaje, p. ej. «Aldona».")
    p.add_argument("--disenar", default=None, help="Descripción (en inglés) para diseñar voces nuevas.")
    p.add_argument("--ids", nargs="*", default=None, help="Voces concretas de la biblioteca a probar.")
    p.add_argument("--guardar", default=None, help="generated_voice_id de una voz diseñada para guardarla.")
    p.add_argument("--nombre", default=None, help="Nombre con el que se guarda la voz diseñada.")
    p.add_argument("--genero", choices=["male", "female"], default=None)
    p.add_argument("--edad", choices=["young", "middle_aged", "old"], default=None)
    p.add_argument("--buscar", default=None, help="Texto libre (niño, abuela, teen…).")
    p.add_argument("--max", type=int, default=5)
    p.add_argument("--texto", default="", help="Frase que dice cada candidata (admite etiquetas [sad], [laughs]…).")
    a = p.parse_args()

    clave = clave_de_api()
    if a.guardar:
        guardar(clave, a.guardar, a.nombre or a.guardar, a.disenar)
        return
    if not a.rol or not a.texto:
        raise SystemExit("Faltan --rol y --texto.")
    carpeta = os.path.join(CASTING, a.rol)
    os.makedirs(carpeta, exist_ok=True)

    if a.disenar:
        previas = disenar(clave, a.disenar, a.texto)
        with open(os.path.join(carpeta, "indice_disenadas.csv"), "a", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            for prev in previas:
                n = len([x for x in os.listdir(carpeta) if x.startswith("D")]) + 1
                archivo = f"D{n:02d} diseñada.mp3"
                with open(os.path.join(carpeta, archivo), "wb") as mp3:
                    mp3.write(base64.b64decode(prev["audio_base_64"]))
                w.writerow([archivo, a.disenar, prev.get("generated_voice_id")])
                print(f"{archivo}  generated_voice_id {prev.get('generated_voice_id')}")
        return

    vistas, candidatas = set(), []
    if a.ids:
        candidatas = [{"voice_id": i, "name": i} for i in a.ids]
    for acento in (() if a.ids else ("peninsular", None)):
        for v in buscar(clave, language="es", gender=a.genero, age=a.edad, search=a.buscar,
                        accent=acento, page_size=30):
            if v["voice_id"] in vistas:
                continue
            vistas.add(v["voice_id"])
            candidatas.append(v)
        if len(candidatas) >= a.max:
            break
    candidatas = candidatas[:a.max]

    with open(os.path.join(carpeta, "indice.csv"), "a", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["muestra", "nombre", "acento", "edad", "descripcion", "voice_id", "public_owner_id"])
        for i, v in enumerate(candidatas, 1):
            nombre = re.sub(r'[\\/:*?"<>|]', "", v.get("name", "voz"))[:40].strip()
            archivo = f"{len([x for x in os.listdir(carpeta) if x.endswith('.mp3') and not x.startswith('D')]) + 1:02d} {nombre}.mp3"
            with open(os.path.join(carpeta, archivo), "wb") as mp3:
                mp3.write(decir(clave, v["voice_id"], a.texto))
            w.writerow([archivo, v.get("name"), v.get("accent"), v.get("age"),
                        (v.get("description") or "").replace("\n", " ")[:160], v["voice_id"], v.get("public_owner_id")])
            print(f"{archivo}  ({v.get('accent')}, {v.get('age')})")
    print(f"Muestras en {carpeta}")


if __name__ == "__main__":
    main()
