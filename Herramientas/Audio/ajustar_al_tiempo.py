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


def mejor_lecho(curva, duracion):
    """Para el lecho bajo la voz no sirve el trozo más ruidoso: sirve uno de energía
    constante y moderada (que no compita con el cántico). Se puntúa por lo llano que
    es el tramo y por lo cerca de la mediana de la pista."""
    paso = int(duracion / 0.5)
    if paso >= len(curva):
        return 0.0, 0.0
    mediana_global = float(np.median(curva))
    mejor = 0
    mejor_puntuacion = -1e9
    for inicio in range(0, len(curva) - paso):
        tramo = curva[inicio:inicio + paso]
        mediana = float(np.median(tramo))
        llanura = -float(np.std(tramo))
        distancia = -abs(mediana - mediana_global) * 0.5
        puntuacion = llanura + distancia
        if puntuacion > mejor_puntuacion:
            mejor_puntuacion = puntuacion
            mejor = inicio
    return mejor * 0.5, mejor_puntuacion


def ajustar(entrada, salida, modo, duracion=None, objetivo=None, tope=None, inicio=None):
    x = decodificar(entrada, SR)
    if x.ndim == 1:
        x = x[:, None]
    duracion = duracion or DURACIONES_POR_DEFECTO[modo]
    objetivo = objetivo if objetivo is not None else PREDETERMINADOS[modo][0]
    tope = tope if tope is not None else PREDETERMINADOS[modo][1]

    duracion_original = len(x) / SR
    if modo in ("lecho", "estrofa"):
        curva = curva_de_energia(x)
        if inicio is not None:
            i = int(inicio * SR)
            x = x[i:i + int(duracion * SR)]
            informe = f"cortado desde el segundo {inicio:.1f} (a mano)"
        elif modo == "lecho":
            chosen, puntuacion = mejor_lecho(curva, duracion)
            i = int(chosen * SR)
            x = x[i:i + int(duracion * SR)]
            informe = f"lecho plano desde el segundo {chosen:.1f} (llanura {puntuacion:+.1f})"
        else:
            chosen, build = mejor_tramo(curva, duracion)
            i = int(chosen * SR)
            x = x[i:i + int(duracion * SR)]
            informe = f"recortado del segundo {chosen:.1f} (build {build:+.1f} dB)"
        if len(x) < int(duracion * SR):
            x = np.vstack([x, np.zeros((int(duracion * SR) - len(x), 2), dtype=np.float32)])
        if PREDETERMINADOS[modo][2]:
            x = cruzar_extremos(x, 2.0)
    else:
        golpe = impacto_mas_fuerte(x) if inicio is None else inicio
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


def informe(entrada):
    """Mapa de la pista: silueta de energía y dónde cortar."""
    x = decodificar(entrada, SR)
    if x.ndim == 1:
        x = x[:, None]
    duracion = len(x) / SR
    curva = curva_de_energia(x, 0.5)
    lecho, build = mejor_lecho(curva, 38.0)
    estrofa, build_estrofa = mejor_tramo(curva, 22.0)
    golpe = impacto_mas_fuerte(x)
    valle = float(np.argmin(curva)) * 0.5

    print(f"  {os.path.basename(entrada)}  ({duracion:.1f} s, LUFS {lufs(x):.1f}, "
          f"true peak {true_peak(x):.1f} dBTP)")
    print("    silueta (cada bloque = 5 s):")
    paso = int(5 / 0.5)
    for i in range(0, len(curva) - paso, paso):
        bloque = curva[i:i + paso]
        barra = "".join("#" if v > max(curva) - 6 else ("." if v > max(curva) - 18 else " ") for v in bloque)
        marca = ""
        if lecho <= i * 0.5 < lecho + 38:
            marca = "  <-- lecho del cántico (38 s)"
        elif estrofa <= i * 0.5 < estrofa + 22:
            marca = "  <-- estrofa (build)"
        if abs(i * 0.5 - golpe) < 2.5:
            marca = marca or "  <-- impacto más fuerte"
        if abs(i * 0.5 - valle) < 2.5:
            marca = marca or "  <-- punto más silencioso"
        print(f"    {i * 0.5:6.1f}s {barra}{marca}")
    print(f"    lecho recomendado: de {lecho:.1f}s a {lecho + 38:.1f}s (build {build:+.1f} dB)")
    print(f"    estrofa recomendada: de {estrofa:.1f}s a {estrofa + 22:.1f}s (build {build_estrofa:+.1f} dB)")
    print(f"    impacto: {golpe:.2f}s | punto más silencioso: {valle:.1f}s")
    print("    (el resto de la pista se descarta: el juego coloca el trozo, no la pieza entera)")


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
    p.add_argument("--informe", action="store_true", help="Solo dibuja la silueta de la pista")
    p.add_argument("--inicio", type=float, default=None, help="Segundo por el que empieza el corte (a mano)")
    args = p.parse_args()

    if args.informe:
        if args.carpeta:
            carpeta = args.carpeta if os.path.isabs(args.carpeta) else os.path.join(RAIZ, args.carpeta)
            for nombre in sorted(os.listdir(carpeta)):
                if nombre.lower().endswith((".mp3", ".wav")):
                    informe(os.path.join(carpeta, nombre))
            return
        if not args.entrada:
            print("Falta --entrada o --carpeta")
            return
        informe(args.entrada if os.path.isabs(args.entrada) else os.path.join(RAIZ, args.entrada))
        return

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
            ajustar(origen, final, args.modo, args.duracion, args.objetivo, args.tope, args.inicio)
        print(f"\nAjustados en {destino}")
        return

    if not args.entrada:
        print("Falta --entrada o --carpeta")
        return
    salida = args.salida or os.path.join(RAIZ, "Herramientas", "Audio", "_generaciones",
                                         f"{os.path.splitext(os.path.basename(args.entrada))[0]} [{args.modo}].wav")
    os.makedirs(os.path.dirname(salida), exist_ok=True)
    ajustar(args.entrada, salida, args.modo, args.duracion, args.objetivo, args.tope, args.inicio)


if __name__ == "__main__":
    main()
