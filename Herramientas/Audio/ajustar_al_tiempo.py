"""Da forma a las generaciones de Suno para que encajen en el juego.

Suno hace canciones (2-3 minutos, con forma de verso/talud). El juego necesita piezas
con duración exacta: el lecho del cántico son 38 s clavados (171,1 s -> 209,1 s de la
secuencia del prólogo), el stinger del golpe son 3 s y el impacto tiene que caer en un
fotograma. Eso no se consigue prompting: se consigue recortando.

Modos:
  lecho    Recorta el tramo con más build del tema, lo hace compatible en bucle y lo
           deja al nivel objetivo (por defecto -20 LUFS, tope de true peak -1 dBTP).
  estrofa  Igual que lecho, pero sin bucle: es la parte que sube sobre el cántico.
  stinger  Corta el impacto más fuerte de la pista (un solo golpe, 2-4 s).
  impacto  Corta el impacto más fuerte y le deja la cola (5-8 s) para el remate.

Uso:
    python Herramientas/Audio/ajustar_al_tiempo.py --carpeta Herramientas/Audio/_generaciones --modo lecho
    python Herramientas/Audio/ajustar_al_tiempo.py --entrada X.mp3 --salida Y.mp3 --modo stinger --duracion 3
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsp_audio import (  # noqa: E402
    SR, db_a_ganancia, decodificar, escribir, lufs, limitador, normalizar_lufs,
    rms_maximo, true_peak,
)

RAIZ = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
PREDETERMINADOS = {
    # modo: (objetivo LUFS, tope dBTP, bucle, informe)
    "lecho": (-20.0, -1.0, True, "Cora del cántico (38 s)"),
    "estrofa": (-20.0, -1.0, False, "Estrofa del cántico (build)"),
    "stinger": (-14.0, -1.0, False, "Stinger del golpe musical"),
    "impacto": (-16.0, -1.0, False, "Golpe del hechizo"),
}
DURACIONES_POR_DEFECTO = {"lecho": 38.0, "estrofa": 22.0, "stinger": 3.0, "impacto": 8.0}


def curva_de_energia(x, ventana=0.5):
    """RMS en ventanas de 0,5 s: la silueta de la pista."""
    n = int(ventana * SR)
    m = x.mean(axis=1) if x.ndim > 1 else x
    total = len(m) // n
    if total < 2:
        return np.zeros(1)
    rms = np.sqrt((m[:total * n].reshape(total, n) ** 2).mean(axis=1))
    return 20 * np.log10(np.maximum(rms, 1e-9))


def mejor_tramo(curva, duracion):
    """El tramo de 'duracion' segundos con más build (sube más dentro de la ventana)."""
    paso = len(curva) / max(1, int(len(curva) / 2))
    mejor = 0
    mejor_puntuacion = -1e9
    for inicio in range(0, max(1, len(curva) - int(duracion / 0.5))):
        fin = min(len(curva), inicio + int(duracion / 0.5))
        if fin - inicio < 4:
            break
        puntuacion = curva[fin - 1] - curva[inicio]
        if puntuacion > mejor_puntuacion:
            mejor_puntuacion = puntuacion
            mejor = inicio
    return mejor * 0.5, mejor_puntuacion


def impacto_mas_fuerte(x, busqueda=40.0):
    """El instante del golpe más fuerte (salto brusco de energía)."""
    curva = curva_de_energia(x, 0.05)
    limite = int(min(busqueda, len(curva) * 0.05))
    if limite < 10:
        return 0.0
    saltos = np.diff(curva[:limite])
    return float(np.argmax(saltos)) * 0.05


def cruzar_extremos(x, segundos=2.0):
    """Hace la pieza compatible en bucle: funde el final con el principio."""
    n = int(segundos * SR)
    if n * 2 >= len(x):
        return x
    inicio = x[:n].copy()
    fin = x[-n:].copy()
    mezcla = np.linspace(1, 0, n)[:, None]
    cuerpo = x[n:-n].copy()
    return np.vstack([inicio * (1 - mezcla) + fin * mezcla, cuerpo, fin * mezcla + inicio * (1 - mezcla)])


def ajustar(entrada, salida, modo, duracion=None, objetivo=None, tope=None):
    x = decodificar(entrada, SR)
    if x.ndim == 1:
        x = x[:, None]
    duracion = duracion or DURACIONES_POR_DEFECTO[modo]
    objetivo = objetivo if objetivo is not None else PREDETERMINADOS[modo][0]
    tope = tope if tope is not None else PREDETERMINADOS[modo][1]

    duracion_original = len(x) / SR
    if modo in ("lecho", "estrofa"):
        curva = curva_de_energia(x)
        inicio, build = mejor_tramo(curva, duracion)
        i = int(inicio * SR)
        x = x[i:i + int(duracion * SR)]
        if len(x) < int(duracion * SR):
            x = np.vstack([x, np.zeros((int(duracion * SR) - len(x), 2), dtype=np.float32)])
        if PREDETERMINADOS[modo][2]:
            x = cruzar_extremos(x, 2.0)
        informe = f"recortado del segundo {inicio:.1f} (build {build:+.1f} dB)"
    else:
        golpe = impacto_mas_fuerte(x)
        i = max(0, int((golpe - 0.05) * SR))
        x = x[i:i + int(duracion * SR)]
        if len(x) < int(duracion * SR):
            x = np.vstack([x, np.zeros((int(duracion * SR) - len(x), 2), dtype=np.float32)])
        informe = f"impacto en el segundo {golpe:.2f}"

    x = normalizar_lufs(x, objetivo)
    x = limitador(x, tope)
    escribir(salida, x)

    print(f"  {os.path.basename(entrada)} -> {os.path.basename(salida)}")
    print(f"    {informe} | duración {duracion_original:.1f}s -> {len(x) / SR:.1f}s | "
          f"LUFS {lufs(x):.1f} | true peak {true_peak(x):.1f} dBTP | punch {rms_maximo(x):.1f} dBFS")
    return salida


def main():
    import argparse
    p = argparse.ArgumentParser(description="Ajusta generaciones de Suno al tiempo del juego")
    p.add_argument("--entrada", default="")
    p.add_argument("--salida", default="")
    p.add_argument("--carpeta", default="")
    p.add_argument("--salidaCarpeta", default="")
    p.add_argument("--modo", default="lecho", choices=list(PREDETERMINADOS))
    p.add_argument("--duracion", type=float, default=None)
    p.add_argument("--objetivo", type=float, default=None)
    p.add_argument("--tope", type=float, default=None)
    args = p.parse_args()

    if args.carpeta:
        carpeta = args.carpeta if os.path.isabs(args.carpeta) else os.path.join(RAIZ, args.carpeta)
        destino = args.salidaCarpeta or os.path.join(carpeta, "ajustado")
        os.makedirs(destino, exist_ok=True)
        if not os.path.isdir(carpeta):
            print(f"No existe la carpeta {carpeta}")
            return
        for nombre in sorted(os.listdir(carpeta)):
            if not nombre.lower().endswith((".mp3", ".wav")):
                continue
            origen = os.path.join(carpeta, nombre)
            final = os.path.join(destino, f"{os.path.splitext(nombre)[0]} [{args.modo}]{os.path.splitext(nombre)[1]}")
            ajustar(origen, final, args.modo, args.duracion, args.objetivo, args.tope)
        print(f"\nAjustados en {destino}")
        return

    if not args.entrada:
        print("Falta --entrada o --carpeta")
        return
    salida = args.salida or os.path.join(RAIZ, "Herramientas", "Audio", "_generaciones",
                                         f"{os.path.splitext(os.path.basename(args.entrada))[0]} [{args.modo}].wav")
    os.makedirs(os.path.dirname(salida), exist_ok=True)
    ajustar(args.entrada, salida, args.modo, args.duracion, args.objetivo, args.tope)


if __name__ == "__main__":
    main()
