"""Piloto del conjuro del prólogo: la música del hechizo (propuesta A).

Monta la ventana del cántico (desde la frase del Mago Oscuro hasta el silencio
posterior al estallido) tal y como la mezcla el juego, con los tiempos
horneados reales de `Prologo_UltimaNoche_Horneado.asset`, y escribe tres
versiones para comparar:

  00_actual.mp3      la mezcla de hoy: el tema del duelo sigue solo y a tope
                      durante todo el cántico.
  01_propuesta_A.mp3  propuesta A: el tema se corta en el negro, el lecho
                      ritual del cántico lleva las estrofas, la estrofa se apaga
                      en la despedida y el tema vuelve con el grito.
  02_propuesta_A_tema_al_cabeza.mp3
                      como A, pero el tema vuelve desde el principio en vez de
                      desde donde se cortó.

El modelo es el del juego: bus de música con ducking por `MezclaBeat` y por la
voz (manda el más fuerte), lechos rituales exentos de ducking, cabecera de
mezcla de INC-802 y limitador de pico.

Uso:
    python Herramientas/Audio/propuesta_conjuro.py
"""
import os
import re
import subprocess
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsp_audio import (db_a_ganancia, decodificar, eco_slap, escribir,
                       ffmpeg_bin, lufs, normalizar_lufs, reverberacion,
                       true_peak)

SR = 48000
AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", ".."))
HORNEADO = os.path.join(RAIZ, "Assets", "_SEQUENCES", "Guiones",
                        "Prologo_UltimaNoche_Horneado.asset")
PERFIL = os.path.join(RAIZ, "Assets", "_AUDIOPROFILE", "AudioGraphProfile.asset")
VOCES = os.path.join(RAIZ, "Assets", "Resources", "Voices", "es")
SALIDA = os.path.join(AQUI, "Piloto", "ConjuroA")

VENTANA_INICIO = 160.0   # 2:40 - frase del Mago Oscuro
VENTANA_FIN = 222.9      # fin de la secuencia

# Ajustes de voz del juego (AudioService.AplicarEfectoDeVoz).
PRESET_DE_VOZ = {
    "ninguno": dict(decay=0.0, wet=0.0, delay=0.0, feedback=0.0, eco=0.0),
    "exterior_noche": dict(decay=0.9, wet=0.22, delay=0.0, feedback=0.0, eco=0.0),
    "conjuro": dict(decay=9.0, wet=0.32, delay=400.0, feedback=0.45, eco=0.38),
}

# Ducking de voz en cinemáticas (AudioGraphProfile.cinematicVoiceDuck).
DUCK_VOZ = dict(musica=-8.0, ambiente=-10.0, sfx=-6.0,
                ataque=0.08, release=0.65, hold=0.5)

# Cabecera de mezcla (INC-802): suma de potencias 4, tope de -5 dB.
CABECERA_OBJETIVO = 4.0
CABECERA_MINIMO = 0.55

# Frases del cántico: clip -> (clave del guion, preset de voz, momento).
FRASES = (
    ("PROLOGO_MAGO_VALLE", "mago", "exterior_noche"),
    ("PROLOGO_PLEGARIA_01", "p01", "ninguno"),
    ("PROLOGO_PLEGARIA_02", "p02", "conjuro"),
    ("PROLOGO_PLEGARIA_03", "p03", "conjuro"),
    ("PROLOGO_PLEGARIA_04", "p04", "conjuro"),
    ("PROLOGO_PLEGARIA_05", "p05", "conjuro"),
    ("PROLOGO_PLEGARIA_06", "p06", "conjuro"),
    ("PROLOGO_HECHIZO", "grito", "conjuro"),
)


# ---------------------------------------------------------------- horneado
def leer_horneado():
    """Saca del guion horneado las frases (clave -> t0/t1) y los beats."""
    with open(HORNEADO, encoding="utf-8", errors="replace") as f:
        texto = f.read()

    frases = {}
    bloque = re.search(r"^  lineas:\n(.*?)^  planos:", texto, re.S | re.M)
    actual = None
    for linea in bloque.group(1).splitlines():
        if linea.startswith("  - onlyIfFlag:"):
            if actual:
                frases[actual["clave"]] = (actual["t0"], actual["t1"])
            actual = {"t0": 0.0, "t1": 0.0, "clave": ""}
            continue
        if actual is None:
            continue
        m = re.match(r"    t0: (.*)", linea)
        if m:
            actual["t0"] = float(m.group(1))
        m = re.match(r"    t1: (.*)", linea)
        if m:
            actual["t1"] = float(m.group(1))
        m = re.match(r"    clave: (.*)", linea)
        if m:
            actual["clave"] = m.group(1).strip()
    if actual:
        frases[actual["clave"]] = (actual["t0"], actual["t1"])

    referencias = {}
    bloque = re.search(r"^  references:\n(.*)", texto, re.S | re.M)
    for trozo in re.split(r"\n    - rid: ", bloque.group(1)):
        if not trozo.strip():
            continue
        rid, resto = trozo.split("\n", 1)
        m = re.search(r"type: \{class: (\w+)", resto)
        clase = m.group(1) if m else "?"
        campos = dict(re.findall(r"^        (\w+): (.*)$", resto, re.M))
        referencias[rid.strip()] = (clase, {k: v.strip() for k, v in campos.items()})

    eventos = []
    bloque = re.search(r"^  efectos:\n(.*?)^  etiquetas:", texto, re.S | re.M)
    actual = None
    for linea in bloque.group(1).splitlines():
        if linea.startswith("  - onlyIfFlag:"):
            if actual:
                eventos.append(actual)
            actual = {"t": 0.0, "rid": "", "linea": 0}
            continue
        if actual is None:
            continue
        m = re.match(r"    t: (.*)", linea)
        if m:
            actual["t"] = float(m.group(1))
        m = re.match(r"      rid: (\d+)", linea)
        if m:
            actual["rid"] = m.group(1)
        m = re.match(r"    linea: (\d+)", linea)
        if m:
            actual["linea"] = int(m.group(1))
    if actual:
        eventos.append(actual)

    for evento in eventos:
        clase, campos = referencias.get(evento["rid"], ("?", {}))
        evento["clase"] = clase
        evento["campos"] = campos
    return frases, eventos


def buscar_eventos(eventos, clase, **condiciones):
    return [e for e in eventos
            if e["clase"] == clase
            and all(e["campos"].get(k, "").startswith(v) for k, v in condiciones.items())]


# ------------------------------------------------------------- clave -> archivo
def mapa_guids():
    """GUID -> ruta del asset (un solo recorrido del proyecto)."""
    mapa = {}
    for carpeta, _, archivos in os.walk(os.path.join(RAIZ, "Assets")):
        for archivo in archivos:
            if not archivo.endswith(".meta"):
                continue
            try:
                with open(os.path.join(carpeta, archivo), encoding="utf-8",
                          errors="replace") as f:
                    m = re.search(r"^guid: ([0-9a-f]{32})", f.read(), re.M)
            except OSError:
                continue
            if m:
                mapa[m.group(1)] = os.path.join(carpeta, archivo[:-5])
    return mapa


def eventos_del_perfil():
    """eventKey -> ruta del clip, según el AudioGraphProfile."""
    guids = mapa_guids()
    claves = {}
    with open(PERFIL, encoding="utf-8", errors="replace") as f:
        for bloque in re.split(r"\n  - eventKey: ", f.read())[1:]:
            clave = bloque.split("\n")[0].strip()
            m = re.search(r"guid: ([0-9a-f]{32})", bloque)
            if m and m.group(1) in guids:
                claves[clave] = guids[m.group(1)]
    return claves


# ------------------------------------------------------------------ mezcla
class Capa:
    """Una fuente en la mezcla: muestras, momento y bus."""

    def __init__(self, nombre, bus, muestras, t0, volumen, t1=None, es_voz=False):
        self.nombre = nombre
        self.bus = bus
        self.muestras = muestras
        self.t0 = t0
        self.t1 = t1 if t1 is not None else t0 + len(muestras) / SR
        self.volumen = volumen
        self.es_voz = es_voz


def envoltura_ducking(segmentos, n, sr=SR):
    """Multiplicador por muestra: el ducking más fuerte manda (mínimo)."""
    mult = np.ones(n, dtype=np.float32)
    for t0, t1, db, ataque, release in segmentos:
        a = int(t0 * sr)
        b = min(int(t1 * sr), n)
        if a >= n or b <= a:
            continue
        curva = np.full(b - a, db_a_ganancia(db), dtype=np.float32)
        n_ataque = min(int(ataque * sr), b - a)
        if n_ataque > 1:
            curva[:n_ataque] *= np.linspace(1.0, db_a_ganancia(db), n_ataque)
        n_release = min(int(release * sr), b - a)
        if n_release > 1:
            curva[-n_release:] *= np.linspace(db_a_ganancia(db), 1.0, n_release)
        mult[a:b] = np.minimum(mult[a:b], curva)
    return mult


def factor_cabecera(capas, n, sr=SR, paso=0.05):
    """Cabecera de mezcla de INC-802: suma de potencias de las fuentes vivas."""
    factor = np.ones(n, dtype=np.float32)
    muestras_paso = max(int(paso * sr), 1)
    for i in range(0, n, muestras_paso):
        suma = 0.0
        for capa in capas:
            if capa.es_voz:
                continue
            if capa.t0 <= i / sr < capa.t1:
                suma += capa.volumen * capa.volumen
        if suma > CABECERA_OBJETIVO:
            factor[i:i + muestras_paso] = max(CABECERA_MINIMO,
                                               np.sqrt(CABECERA_OBJETIVO / suma))
    return factor


def preparar_voz(ruta, preset):
    """Voz como en el juego: el import la deja a pico de -1 dBFS y luego el
    preset aplica sala y slap. El bus de diálogo deja la voz en -18 LUFS."""
    x = decodificar(ruta, SR, mono=True)
    pico = float(np.max(np.abs(x))) if len(x) else 0.0
    if pico > 1e-9:
        x = x * (db_a_ganancia(-1.0) / pico)
    p = PRESET_DE_VOZ[preset]
    if p["wet"] > 0:
        sala = reverberacion(x, sr=SR, decay=p["decay"], predelay=0.04, wet=1.0)
        x = 0.1 * x + (10 ** (-30.0 / 20.0)) * sala
    if p["eco"] > 0:
        x = x + p["eco"] * eco_slap(x, retardo_ms=p["delay"],
                                    decaimiento=p["feedback"], humedo=1.0)
    return normalizar_lufs(x, -18.0)


def fundido(capa, t_inicio=None, dur_inicio=0.0, t_fin=None, dur_fin=0.0):
    """Fundidos de entrada y salida sobre una capa (segundos absolutos)."""
    x = capa.muestras
    if x.ndim == 1:
        x = x[:, None]
    if t_inicio is not None and dur_inicio > 0:
        k = int(dur_inicio * SR)
        k = min(k, len(x))
        x[:k] *= np.linspace(0.0, 1.0, k)[:, None]
    if t_fin is not None and dur_fin > 0:
        k = int(dur_fin * SR)
        k = min(k, len(x))
        x[-k:] *= np.linspace(1.0, 0.0, k)[:, None]
    capa.muestras = x


def fundir_desde(capa, t_ini, duracion):
    """Apaga la capa desde el instante t_ini (como el fundido de StopLoopingSFX)."""
    x = capa.muestras
    if x.ndim == 1:
        x = x[:, None]
    i = int((t_ini - capa.t0) * SR)
    if i < 0:
        i = 0
    if i >= len(x):
        return
    n = min(int(duracion * SR), len(x) - i)
    if n <= 1:
        return
    x[i:i + n] *= np.linspace(1.0, 0.0, n)[:, None]
    capa.muestras = x


def escribir_mp3(ruta, x):
    base = os.path.splitext(ruta)[0]
    wav = base + ".wav"
    escribir(wav, x, SR)
    subprocess.run([ffmpeg_bin(), "-hide_banner", "-loglevel", "error", "-y",
                    "-i", wav, "-codec:a", "libmp3lame", "-b:a", "192k", ruta],
                   check=True)
    os.remove(wav)


def montar(capas, frases_voz, n, ducks, sr=SR):
    """Suma las capas aplicando el ducking de cada bus y la cabecera."""
    total = np.zeros((n, 2), dtype=np.float32)
    cabecera = factor_cabecera(capas, n, sr)
    inicio = int(VENTANA_INICIO * sr)

    for capa in capas:
        x = capa.muestras
        if x.ndim == 1:
            x = x[:, None]
        i = int(capa.t0 * sr) - inicio
        if i >= n or i + len(x) <= 0:
            continue
        a = max(i, 0)
        b = min(i + len(x), n)
        trozo = x[a - i:b - i]
        if trozo.shape[1] == 1:
            trozo = np.repeat(trozo, 2, axis=1)
        g = ducks[capa.bus][a:b]
        if not capa.es_voz:
            g = g * cabecera[a:b]
        total[a:b] += trozo * g[:, None] * capa.volumen
    return total


def ducks_del_juego(frases_voz, extra_musica=(), extra_sfx=(), extra_ambiente=()):
    """Ducking por bus: el de la voz en cinemáticas más los `MezclaBeat`."""
    segmentos = {"musica": [], "ambiente": [], "sfx": [], "ritual": [], "voz": []}
    for t0, t1 in frases_voz:
        for bus in ("musica", "ambiente", "sfx"):
            segmentos[bus].append((max(t0 - 0.05, 0.0), t1 + DUCK_VOZ["hold"],
                                    DUCK_VOZ[bus], DUCK_VOZ["ataque"],
                                    DUCK_VOZ["release"]))
    segmentos["musica"].extend(extra_musica)
    segmentos["sfx"].extend(extra_sfx)
    segmentos["ambiente"].extend(extra_ambiente)
    n = int((VENTANA_FIN - VENTANA_INICIO) * SR)
    return {bus: envoltura_ducking(segmentos[bus], n) for bus in segmentos}


# ------------------------------------------------------------------- guion
def construir_timeline(frases, eventos):
    """Tiempos clave del cántico, leídos del guion horneado."""
    tl = {}
    for clave, nombre, _ in FRASES:
        tl[nombre] = frases[clave]

    tema = buscar_eventos(eventos, "MusicBeat", musicId="MAGOOSCURO_CLIMAX")
    tl["tema"] = tema[0]["t"]
    paradas = [e for e in buscar_eventos(eventos, "MusicBeat", musicId="")
               if e["t"] > tl["grito"][0]]
    tl["tema_fin"] = paradas[-1]["t"] if paradas else tl["grito"][1]

    negro = [e for e in buscar_eventos(eventos, "ScreenFadeBeat")
             if e["t"] >= tl["p01"][1] - 0.05]
    tl["negro_entra"] = min(e["t"] for e in negro)
    tl["negro_sale"] = min(e["t"] for e in negro if e["t"] > tl["negro_entra"])

    mezclas = buscar_eventos(eventos, "MezclaBeat")
    plegaria = [e for e in mezclas if e["campos"].get("fuente") == "plegaria"]
    tl["plegaria_baja"] = plegaria[0]["t"]
    tl["plegaria_sube"] = plegaria[1]["t"]
    tl["plegaria_fuera"] = [e for e in plegaria
                            if e["campos"].get("quitar") == "1"][0]["t"]
    grito = [e for e in mezclas if e["campos"].get("fuente") == "grito"]
    tl["grito_mix"] = grito[0]["t"]
    tl["grito_fuera"] = [e for e in grito
                         if e["campos"].get("quitar") == "1"][0]["t"]

    respiro = buscar_eventos(eventos, "RespiroBeat")[0]
    tl["respiro"] = respiro["t"]
    tl["respiro_fin"] = respiro["t"] + float(respiro["campos"].get("duracion", 0.9))

    blanco = [e for e in buscar_eventos(eventos, "ScreenFadeBeat")
              if e["t"] > tl["grito"][1]]
    tl["estallido"] = min(e["t"] for e in blanco)

    agujero = buscar_eventos(eventos, "AmbienteBeat", loopId="AGUJERO_NEGRO")
    tl["agujero"] = agujero[0]["t"]
    return tl


def capas_comunes(tl, claves):
    """Capas que no cambian entre las tres versiones: voz, efectos y ambiente."""
    capas = []

    for clave, nombre, preset in FRASES:
        ruta = os.path.join(VOCES, clave + ".mp3")
        if not os.path.exists(ruta):
            continue
        muestras = preparar_voz(ruta, preset)
        capas.append(Capa(clave, "voz", muestras, tl[nombre][0], 1.0, es_voz=True))

    for clave, t, volumen in (
        ("SFX_Prologo_Trueno_Lejano", tl["p02"][0] + 0.8, 0.2),
        ("SFX_Prologo_Trueno_1", tl["p03"][0] + 3.2, 0.35),
        ("SFX_Prologo_Trueno_2", tl["p05"][0] + 3.4, 0.35),
        ("SFX_Prologo_Trueno_2", tl["p05"][0] + 3.9, 0.35),
        ("SFX_Prologo_Trueno_2", tl["grito"][0] + 0.2, 1.0),
        ("Prologo_Carga", tl["p03"][0] + 3.5, 0.5),
        ("Prologo_Carga", tl["p05"][0] + 3.5, 0.5),
        ("Prologo_Carga", tl["grito_mix"], 0.4),
        ("SFX_Prologo_SilencioBlanco", tl["tema_fin"] + 0.1, 0.6),
    ):
        ruta = claves.get(clave)
        if not ruta:
            continue
        muestras = decodificar(ruta, SR, mono=False)
        capas.append(Capa(clave, "sfx", muestras, t, volumen))

    for clave, t, volumen, hasta in (
        ("Ambience_Prologo_Tormenta", tl["mago"][0] - 30.0, 0.5, tl["estallido"]),
        ("SFX_Prologo_AgujeroNegro_Bucle", tl["agujero"], 0.12, tl["estallido"]),
    ):
        ruta = claves.get(clave)
        if not ruta:
            continue
        muestras = decodificar(ruta, SR, mono=False)
        if hasta is not None:
            muestras = muestras[:int((hasta - t) * SR)]
        capas.append(Capa(clave, "ambiente", muestras, t, volumen, hasta))
    return capas


def main():
    os.makedirs(SALIDA, exist_ok=True)
    frases, eventos = leer_horneado()
    tl = construir_timeline(frases, eventos)
    claves = eventos_del_perfil()
    claves["El Ultimo Duelo"] = os.path.join(RAIZ, "Assets", "Audio", "Music",
                                             "Cinematicas", "El Ultimo Duelo.mp3")

    print("Tiempos del cántico (guion horneado):")
    for nombre in ("mago", "p01", "p02", "p03", "p04", "p05", "p06", "grito"):
        t0, t1 = tl[nombre]
        print(f"  {nombre:6s} {t0:7.2f} -> {t1:7.2f}")
    for nombre in ("tema", "plegaria_baja", "plegaria_sube", "negro_entra",
                   "negro_sale", "plegaria_fuera", "grito_mix", "respiro",
                   "estallido", "tema_fin"):
        print(f"  {nombre:16s} {tl[nombre]:7.2f}")

    n = int((VENTANA_FIN - VENTANA_INICIO) * SR)
    frases_voz = [tl[nombre] for _, nombre, _ in FRASES]

    # El ducking de la plegaria (-20 dB) y el del respiro son comunes.
    comun_musica = [
        (tl["plegaria_baja"], tl["plegaria_sube"], -20.0, 0.6, 0.6),
        (tl["respiro"], tl["respiro_fin"], -10.0, 0.25, 0.35),
    ]
    comun_sfx = [
        (tl["grito_mix"], tl["grito_fuera"], -3.0, 0.3, 0.3),
    ]
    comun_ambiente = [
        (tl["plegaria_baja"], tl["plegaria_sube"], -14.0, 0.6, 0.6),
        (tl["grito_mix"], tl["grito_fuera"], -6.0, 0.3, 0.3),
    ]

    # ------------------------------------------------------------- hoy
    capas = capas_comunes(tl, claves)
    musica = decodificar(claves["El Ultimo Duelo"], SR, mono=False)
    hasta = int((tl["tema_fin"] - tl["tema"]) * SR)
    tema = Capa("tema del duelo", "musica", musica[:hasta], tl["tema"], 1.0,
                tl["tema_fin"])
    fundido(tema, t_fin=tl["tema_fin"], dur_fin=1.2)
    capas.append(tema)
    ducks = ducks_del_juego(frases_voz, comun_musica, comun_sfx, comun_ambiente)
    total = montar(capas, frases_voz, n, ducks)
    total = limitar(total)
    escribir_mp3(os.path.join(SALIDA, "00_actual.mp3"), total)
    print("escrito 00_actual.mp3")

    # ------------------------------------------------------- propuesta A
    for nombre, desde_cabeza in (("01_propuesta_A.mp3", False),
                                 ("02_propuesta_A_tema_al_cabeza.mp3", True)):
        corte = tl["negro_sale"]
        vuelve = tl["grito"][0]
        capas = capas_comunes(tl, claves)

        # El tema: hasta el negro, cortada con fundido, y de vuelta con el
        # grito (el juego recuerda la posición del clip).
        hasta_corte = int((corte - tl["tema"]) * SR)
        tema = Capa("tema del duelo", "musica", musica[:hasta_corte], tl["tema"],
                    1.0, corte)
        fundido(tema, t_fin=corte, dur_fin=0.8)
        capas.append(tema)
        cola = musica if desde_cabeza else musica[hasta_corte:]
        vuelve_capa = Capa("tema del duelo (vuelve)", "musica", cola, vuelve, 1.0)
        fundido(vuelve_capa, t_inicio=vuelve, dur_inicio=0.5)
        capas.append(vuelve_capa)

        # Lecho ritual: el cántico sostiene el conjuro (exento de ducking).
        lecho = claves.get("Prologue_CoraCanto")
        if lecho:
            muestras = decodificar(lecho, SR, mono=False)
            muestras = muestras[:int((tl["respiro"] + 0.3 - corte) * SR)]
            capa = Capa("lecho del cántico", "ritual", muestras, corte, 0.75)
            fundir_desde(capa, tl["respiro"], 0.3)
            capas.append(capa)
        estrofa = claves.get("Prologue_EstrofaCanto")
        if estrofa:
            muestras = decodificar(estrofa, SR, mono=False)
            capa = Capa("estrofa", "ritual", muestras, tl["p05"][0], 0.75)
            # La estrofa se apaga en la despedida ("Perdóname, Liora").
            fundir_desde(capa, tl["p06"][0], 0.5)
            capas.append(capa)
        coro = claves.get("SFX_Prologo_CoroBucle")
        if coro:
            muestras = decodificar(coro, SR, mono=False)
            largo = int((tl["respiro"] + 0.3 - corte) * SR)
            repetido = np.tile(muestras, (int(largo / len(muestras)) + 1, 1))[:largo]
            capa = Capa("coro", "sfx", repetido, corte, 0.14)
            # El coro se apaga con el duelo de miradas.
            fundir_desde(capa, tl["grito_mix"], 1.5)
            capas.append(capa)
        for clave, t in (("Prologue_GolpeFinal", vuelve),
                         ("Prologue_GolpeFinalCola", tl["estallido"])):
            ruta = claves.get(clave)
            if not ruta:
                continue
            muestras = decodificar(ruta, SR, mono=False)
            capas.append(Capa(clave, "sfx", muestras, t, 1.0))

        extra_musica = list(comun_musica)
        extra_musica.append((vuelve, vuelve + 0.6, -6.0, 0.1, 0.6))
        ducks = ducks_del_juego(frases_voz, extra_musica, comun_sfx, comun_ambiente)
        total = montar(capas, frases_voz, n, ducks)
        total = limitar(total)
        escribir_mp3(os.path.join(SALIDA, nombre), total)
        print("escrito", nombre)


def limitar(total):
    """Cabezera de mezcla y limitador: el digital no satura."""
    from dsp_audio import limitador
    p = float(np.max(np.abs(total)))
    if p > 0:
        total = total * (db_a_ganancia(-1.0) / p)
    return limitador(total, techo_db=-1.0)


if __name__ == "__main__":
    main()
