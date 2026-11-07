"""Clave de la API de ElevenLabs para los scripts de Tools/Audio.

Se lee de la variable de entorno ELEVENLABS_API_KEY o, si no está, de Tools/Audio/elevenlabs.key
(una sola línea con la clave). Ese archivo está en .gitignore: la clave nunca va al repositorio.
"""
import os

ARCHIVO = os.path.join(os.path.dirname(os.path.abspath(__file__)), "elevenlabs.key")


def clave_de_api():
    clave = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not clave and os.path.isfile(ARCHIVO):
        clave = open(ARCHIVO, encoding="utf-8").read().strip()
    if not clave:
        raise SystemExit(f"Falta la clave de ElevenLabs: ponla en {ARCHIVO} o en ELEVENLABS_API_KEY")
    return clave
