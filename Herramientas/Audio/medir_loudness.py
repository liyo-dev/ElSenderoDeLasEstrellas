"""Mide la loudness de los clips del proyecto y genera un informe.

Uso:
    python Herramientas/Audio/medir_loudness.py                 # carpetas por defecto
    python Herramientas/Audio/medir_loudness.py Assets/Audio/Music
    python Herramientas/Audio/medir_loudness.py --csv informe.csv Assets/Resources/Voices

Requiere `ffmpeg` en el PATH (o la variable de entorno FFMPEG).
Los objetivos de loudness por categoría están en `docs/audio/PLAN_MEZCLA_ANIME.md`.
"""
import csv
import os
import statistics
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsp_audio import informe  # noqa: E402

RAIZ = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))

CARPETAS_POR_DEFECTO = [
    ("Voces", os.path.join(RAIZ, "Assets", "Resources", "Voices")),
    ("Hechizos", os.path.join(RAIZ, "Assets", "Audio", "SFX_Hechizos")),
    ("Musica", os.path.join(RAIZ, "Assets", "Audio", "Music")),
    ("Prologo", os.path.join(RAIZ, "Assets", "Audio", "SFX_Prologo")),
]

EXTENSIONES = (".wav", ".mp3", ".ogg", ".aiff", ".flac")


def recoger(carpeta):
    for base, _, ficheros in os.walk(carpeta):
        for f in sorted(ficheros):
            if f.lower().endswith(EXTENSIONES):
                yield os.path.join(base, f)


def resumen(nombre, filas):
    if not filas:
        print(f"{nombre}: sin clips")
        return
    lufs = [f["lufs"] for f in filas if f["lufs"] != float("-inf")]
    picos = [f["true_peak"] for f in filas]
    print(f"\n=== {nombre}  (n={len(filas)})")
    if lufs:
        print(f"  LUFS        min {min(lufs):6.1f} | mediana {statistics.median(lufs):6.1f} | "
              f"max {max(lufs):6.1f} | rango {max(lufs) - min(lufs):5.1f} dB")
    print(f"  True peak   min {min(picos):6.1f} | mediana {statistics.median(picos):6.1f} | "
          f"max {max(picos):6.1f} dBTP")
    flojos = sorted(lufs)[:3]
    fuertes = sorted(lufs)[-3:]
    print("  más flojos:  " + ", ".join(f"{v:.1f}" for v in flojos))
    print("  más fuertes: " + ", ".join(f"{v:.1f}" for v in fuertes))


def main():
    args = sys.argv[1:]
    csv_salida = None
    if args and args[0] == "--csv":
        csv_salida = args[1]
        args = args[2:]

    if args:
        grupos = [(os.path.basename(os.path.normpath(a)), a) for a in args]
    else:
        grupos = CARPETAS_POR_DEFECTO

    todas = []
    for nombre, carpeta in grupos:
        if not os.path.isdir(carpeta):
            print(f"aviso: no existe {carpeta}")
            continue
        rutas = list(recoger(carpeta))
        print(f"midiendo {len(rutas)} clips de {nombre}...")
        filas = informe(rutas)
        for f in filas:
            f["carpeta"] = nombre
        todas.extend(filas)
        resumen(nombre, filas)

    if csv_salida:
        ruta_csv = csv_salida if os.path.isabs(csv_salida) else os.path.join(RAIZ, csv_salida)
        os.makedirs(os.path.dirname(ruta_csv), exist_ok=True)
        with open(ruta_csv, "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=["carpeta", "fichero", "duracion", "lufs",
                                             "true_peak", "rms_max"])
            w.writeheader()
            for fila in todas:
                w.writerow({k: (round(v, 2) if isinstance(v, float) else v)
                            for k, v in fila.items()})
        print(f"\ninforme: {ruta_csv}")


if __name__ == "__main__":
    main()
