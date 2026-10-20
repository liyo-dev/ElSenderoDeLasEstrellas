import os
import sys
import json
import time
import shutil
import urllib.request
import urllib.error

API_KEY = "sk_a52f16364fcd8815a1e5e4d308eb3b84f4f48f826fd5e1d2"
ROOT = r"c:\Users\luarb\dev\unity\ElSenderoDeLasEstrellas"
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Voices", "es")
BACKUP_DIR = os.path.join(ROOT, "Versiones antiguas", "Voces antes de redoblaje Slayers (2026-10-18)")
RAW_DIR = os.path.join(ROOT, "Versiones antiguas", "Voces Slayers raw (2026-10-18)")

os.makedirs(OUT_DIR, exist_ok=True)
os.makedirs(BACKUP_DIR, exist_ok=True)
os.makedirs(RAW_DIR, exist_ok=True)

VOICES = {
    'Archimago': 'JOlPriKZ6hldygMIxHSm',
    'Liora': 'KHCvMklQZZo0O30ERnVn',
}

LINES_TO_GENERATE = [
    {
        "clave": "PROLOGO_EVACUACION_LIORA",
        "speaker": "Liora",
        "prompt": "[urgently to child, then looking back worried and soft] ¡Vamos, Nina! ...Ten mucho cuidado, por favor.",
        "stability": 0.45,
        "similarity_boost": 0.85,
        "style": 0.15
    },
    {
        "clave": "PROLOGO_PLEGARIA_01",
        "speaker": "Archimago",
        "prompt": "[whispering, intense, breathing heavily] No daré ni un paso atrás... ¡No dejaré que los toques! ¡Liora! ¡Nina! ¡A ninguno de ellos!",
        "stability": 0.35,
        "similarity_boost": 0.85,
        "style": 0.25
    },
    {
        "clave": "PROLOGO_PLEGARIA_02",
        "speaker": "Archimago",
        "prompt": "[solemn whisper, rhythmic ancient incantation] Más allá de la noche y del silencio... ¡Luz que naciste antes que el cielo y tendiste el camino entre las estrellas!",
        "stability": 0.35,
        "similarity_boost": 0.85,
        "style": 0.25
    },
    {
        "clave": "PROLOGO_PLEGARIA_03",
        "speaker": "Archimago",
        "prompt": "[determined low chant, building power, resolute] Ningún poder de este mundo puede detenerlo... ¡Por eso llamo a lo que aún no existe! ¡Escucha mi voz y despierta en mis manos!",
        "stability": 0.35,
        "similarity_boost": 0.85,
        "style": 0.30
    },
    {
        "clave": "PROLOGO_PLEGARIA_04",
        "speaker": "Archimago",
        "prompt": "[powerful, dramatic, rising intensity] ¡Que mi vida sea el precio y mi alma el sello! ¡No importa lo que quede de mí cuando la luz se apague!",
        "stability": 0.35,
        "similarity_boost": 0.85,
        "style": 0.35
    },
    {
        "clave": "PROLOGO_PLEGARIA_05",
        "speaker": "Archimago",
        "prompt": "[shouting with immense energy, screaming, anime climax] ¡¡Haz de mi último aliento un escudo inquebrantable!! ¡¡¡Arde conmigo y detén la oscuridad!!!",
        "stability": 0.30,
        "similarity_boost": 0.85,
        "style": 0.45
    },
    {
        "clave": "PROLOGO_PLEGARIA_06",
        "speaker": "Archimago",
        "prompt": "[softly, heartbroken whisper, tender] Perdóname, Liora... No voy a volver a casa.",
        "stability": 0.50,
        "similarity_boost": 0.85,
        "style": 0.15
    },
    {
        "clave": "PROLOGO_HECHIZO",
        "speaker": "Archimago",
        "prompt": "[screaming at the top of his lungs, explosive anime roar] ¡¡¡PROTÉGELOS A TODOS!!!",
        "stability": 0.25,
        "similarity_boost": 0.85,
        "style": 0.50
    }
]

def generate_line(item):
    clave = item["clave"]
    speaker = item["speaker"]
    voice_id = VOICES[speaker]
    prompt = item["prompt"]
    
    print(f"\nGenerando {clave} ({speaker})...")
    print(f"  Prompt: {prompt}")
    
    dest_path = os.path.join(OUT_DIR, f"{clave}.mp3")
    if os.path.exists(dest_path):
        bk_path = os.path.join(BACKUP_DIR, f"{clave}.mp3")
        shutil.copy2(dest_path, bk_path)
        print(f"  Backup guardado en: {bk_path}")
        
    url = f"https://api.elevenlabs.io/v1/text-to-speech/{voice_id}?output_format=mp3_44100_192"
    
    body = {
        "text": prompt,
        "model_id": "eleven_v4",
        "language_code": "es",
        "voice_settings": {
            "stability": item["stability"],
            "similarity_boost": item["similarity_boost"],
            "style": item["style"],
            "use_speaker_boost": True
        }
    }
    
    payload = json.dumps(body).encode("utf-8")
    headers = {
        "xi-api-key": API_KEY,
        "Content-Type": "application/json; charset=utf-8"
    }
    
    for attempt in range(3):
        try:
            req = urllib.request.Request(url, data=payload, headers=headers, method="POST")
            with urllib.request.urlopen(req) as resp:
                audio_bytes = resp.read()
                
                # Guardar en raw
                raw_path = os.path.join(RAW_DIR, f"{clave}.mp3")
                with open(raw_path, "wb") as f_raw:
                    f_raw.write(audio_bytes)
                    
                # Guardar en Voices
                with open(dest_path, "wb") as f_out:
                    f_out.write(audio_bytes)
                    
                size_kb = len(audio_bytes) / 1024.0
                print(f"  -> OK! Guardado ({size_kb:.1f} KB)")
                return True
        except urllib.error.HTTPError as e:
            err_body = e.read().decode("utf-8", errors="ignore")
            print(f"  [HTTPError {e.code}] {err_body}")
            if e.code == 429:
                print("  Esperando 6 segundos por límite de ratio...")
                time.sleep(6)
            else:
                return False
        except Exception as ex:
            print(f"  [Error] {ex}")
            time.sleep(2)
            
    return False

if __name__ == "__main__":
    print("=== Generador de Voces Prólogo (Lina Inverse / Slayers) ===")
    total = len(LINES_TO_GENERATE)
    success = 0
    for idx, item in enumerate(LINES_TO_GENERATE):
        ok = generate_line(item)
        if ok:
            success += 1
        time.sleep(1.0)
    print(f"\nFinalizado: {success}/{total} líneas generadas con éxito.")
