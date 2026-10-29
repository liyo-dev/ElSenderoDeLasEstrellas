"""Sonidos de la cinemática final (INC-866): el llanto de Estela, los ronquidos de Liam dormido y
la risa bajita de Will y Estela tumbados en la hierba.

Uso (desde la raíz del proyecto):  python Tools/Audio/sonidos_final.py

Los deja en Assets/Audio/SFX_Final/ normalizados a -20 LUFS. La clave de ElevenLabs se lee de
Tools/Audio/generar_hechizo_slayers.py. Las claves de sonido (Final_*) están en el perfil de audio.
"""
import json
import os
import re
import shutil
import subprocess
import tempfile
import urllib.request

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DESTINO = os.path.join(RAIZ, "Assets", "Audio", "SFX_Final")
# Las pistas que solo sirven para mezclar no entran en Assets.
FUENTES = os.path.join(RAIZ, "Versiones antiguas", "Sonidos fuente Final (2026-10-29)")
SOLO_PARA_MEZCLAR = {"Final_Will_OhOh", "Final_Liam_OhOh", "Final_Estela_Carcajada", "Final_Will_Carcajada", "Final_Liam_Carcajada"}
FFMPEG = os.environ.get("FFMPEG") or shutil.which("ffmpeg") or "ffmpeg"

VOZ_ESTELA = "9MKgqtaIyv28bwBeybox"
VOZ_WILL = "JwXx2SOucScqglxgpL6T"
VOZ_LIAM = "hwazPFcispEO8ANIs7LE"
VOZ_TOBIAS = "1tDEBGOo8EqEPApM49eJ"

VOCES = [
    ("Final_Estela_Llanto", VOZ_ESTELA,
     "[sobbing] [crying quietly] [sniffles] ...Güil... [sobbing] [shaky breath] [crying] ...no... [sobbing softly] [sniffles]"),
    ("Final_Estela_Risa", VOZ_ESTELA, "[giggling softly] [whispering] Jijiji... [giggles] jiji..."),
    ("Final_Will_Risa", VOZ_WILL, "[chuckling softly] [whispering] Jejeje... [laughs quietly] jeje..."),
    # «Oh, oh...» de Will y Liam: se mezclan con el de Tobías en FINAL_TOBIAS_RIO_04 (lo dicen los tres).
    ("Final_Will_OhOh", VOZ_WILL, "[nervous] [scared] [uh-oh] Oh, oh..."),
    ("Final_Liam_OhOh", VOZ_LIAM, "[nervous] [scared] [uh-oh] Oh, oh..."),
    # Las risas de todos al final de la mañana (se mezclan en Final_Risas_Todos).
    ("Final_Tobias_Carcajada", VOZ_TOBIAS, "[laughing hysterically] [can't stop laughing] ¡Jajajajaja! [gasping] ¡Jajajaja!"),
    ("Final_Estela_Carcajada", VOZ_ESTELA, "[laughing out loud] ¡Jajajaja! [laughing] jajaja..."),
    ("Final_Will_Carcajada", VOZ_WILL, "[laughing despite himself] ¡Jajaja! [laughing] jajaja..."),
    ("Final_Liam_Carcajada", VOZ_LIAM, "[embarrassed laugh] Jajaja... [laughing] ¡jajaja!"),
]
EFECTOS = [
    ("Final_Liam_Ronquido", "a young boy snoring softly and peacefully while sleeping outdoors, gentle slow breaths, cute", 7.0),
]


def clave_de_api():
    texto = open(os.path.join(RAIZ, "Tools", "Audio", "generar_hechizo_slayers.py"), encoding="utf-8").read()
    return re.search(r'API_KEY\s*=\s*"([^"]+)"', texto).group(1)


def pedir(url, cuerpo, clave):
    req = urllib.request.Request(url, data=json.dumps(cuerpo).encode("utf-8"), method="POST",
                                 headers={"xi-api-key": clave, "Content-Type": "application/json; charset=utf-8"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return r.read()


def normalizar(origen, destino):
    subprocess.check_call([FFMPEG, "-y", "-loglevel", "error", "-i", origen,
                           "-af", "loudnorm=I=-20:TP=-2:LRA=11", "-ar", "44100", "-b:a", "192k", destino])


def mezclar(entradas, destino):
    """Mezcla varias pistas (ruta, retraso en s, volumen) en una sola, normalizada."""
    args = [FFMPEG, "-y", "-loglevel", "error"]
    filtros = []
    for i, (ruta, retraso, volumen) in enumerate(entradas):
        args += ["-i", ruta]
        ms = int(retraso * 1000)
        filtros.append(f"[{i}:a]adelay={ms}|{ms},volume={volumen}[a{i}]")
    filtros.append("".join(f"[a{i}]" for i in range(len(entradas))) + f"amix=inputs={len(entradas)}:normalize=0,loudnorm=I=-18:TP=-2[o]")
    subprocess.check_call(args + ["-filter_complex", ";".join(filtros), "-map", "[o]", "-ar", "44100", "-b:a", "192k", destino])


def main():
    os.makedirs(DESTINO, exist_ok=True)
    os.makedirs(FUENTES, exist_ok=True)
    clave = clave_de_api()
    tmp = tempfile.mkdtemp()
    for nombre, voz, texto in VOCES:
        audio = pedir(f"https://api.elevenlabs.io/v1/text-to-speech/{voz}?output_format=mp3_44100_192",
                      {"text": texto, "model_id": "eleven_v4", "language_code": "es",
                       "voice_settings": {"stability": 0.35, "similarity_boost": 0.85, "style": 0.3, "use_speaker_boost": True}}, clave)
        crudo = os.path.join(tmp, nombre + ".mp3")
        open(crudo, "wb").write(audio)
        normalizar(crudo, os.path.join(FUENTES if nombre in SOLO_PARA_MEZCLAR else DESTINO, nombre + ".mp3"))
        print(nombre, "ok")
    for nombre, texto, segundos in EFECTOS:
        audio = pedir("https://api.elevenlabs.io/v1/sound-generation?output_format=mp3_44100_192",
                      {"text": texto, "duration_seconds": segundos, "prompt_influence": 0.5}, clave)
        crudo = os.path.join(tmp, nombre + ".mp3")
        open(crudo, "wb").write(audio)
        normalizar(crudo, os.path.join(DESTINO, nombre + ".mp3"))
        print(nombre, "ok")
    mezclas()


def mezclas():
    f = lambda n: os.path.join(FUENTES if n in SOLO_PARA_MEZCLAR else DESTINO, n + ".mp3")
    if all(os.path.exists(f(n)) for n in ("Final_Tobias_Carcajada", "Final_Estela_Carcajada", "Final_Will_Carcajada", "Final_Liam_Carcajada")):
        mezclar([(f("Final_Tobias_Carcajada"), 0.0, 1.0), (f("Final_Estela_Carcajada"), 0.35, 0.85),
                 (f("Final_Will_Carcajada"), 0.8, 0.75), (f("Final_Liam_Carcajada"), 1.1, 0.75)], f("Final_Risas_Todos"))
        print("Final_Risas_Todos ok")
    # El «Oh, oh...» de los tres va en la voz de Tobías (la frase del guion).
    voces = os.path.join(RAIZ, "Assets", "Resources", "Voices", "es")
    original = os.path.join(RAIZ, "Versiones antiguas", "Voces antes de Güil (2026-10-29)", "FINAL_TOBIAS_RIO_04.mp3")
    if not os.path.exists(original):
        shutil.copy2(os.path.join(voces, "FINAL_TOBIAS_RIO_04.mp3"), original)
    if os.path.exists(f("Final_Will_OhOh")) and os.path.exists(f("Final_Liam_OhOh")):
        mezclar([(original, 0.0, 1.0), (f("Final_Will_OhOh"), 0.05, 0.9), (f("Final_Liam_OhOh"), 0.12, 0.9)],
                os.path.join(voces, "FINAL_TOBIAS_RIO_04.mp3"))
        print("FINAL_TOBIAS_RIO_04 (los tres) ok")


if __name__ == "__main__":
    main()
