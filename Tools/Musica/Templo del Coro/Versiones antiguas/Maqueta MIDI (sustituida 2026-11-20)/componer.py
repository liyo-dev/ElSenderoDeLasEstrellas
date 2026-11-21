# Maqueta guía del «Templo del Coro»: huida al ritmo de un templo secundario del final de la aventura.
# Mi menor (con el Fa del modo frigio), 140 BPM, 4/4, 104 compases (≈ 2:58) y una cola de 2 compases.
#
# Cada voz del templo va en su canal y es también un peligro del nivel. Este mismo MIDI es la
# partitura: `partitura.json` sale de aquí con cada nota de cada voz que dispara algo.
#   BASE     taikos y timbal en 1 y 3: los pasos del guardián (la sombra cabecea). No hace daño.
#   CORO     coro agudo en notas cortas: cae una losa por nota.
#   ORGANO   golpes de órgano: columna de fuego por golpe.
#   CUERDAS  carrerilla de cuerdas en semicorcheas que llega a un tiempo fuerte: el proyectil; se
#            devuelve con B en la nota de llegada.
#   Parte C: RAYOS (coro muy agudo, barridos de luz), FUEGO (órgano grave en cada tiempo, el fuego que
#            late) y OLEADA (platillo y timbal: entra una oleada de acólitos).
# Melodía, bajo y colchón de cuerdas no disparan nada: sostienen la canción para que Suno la entienda.
#
# Uso: python3 componer.py  →  templo_del_coro_guia.mid y partitura.json
import json, mido

PPQ = 480
BAR = 4 * PPQ
BPM = 140
SEG_POR_TIEMPO = 60.0 / BPM
COMPASES = 104
COLA = 2

SECCIONES = [  # (nombre, primer compás, número de compases)
    ('afinacion', 0, 8),
    ('estrofa1', 8, 16),
    ('estrofa2', 24, 16),
    ('preestribillo', 40, 8),
    ('estribillo1', 48, 16),
    ('parteC', 64, 24),
    ('estribillo_final', 88, 16),
]

def seccion_de(bar):
    for n, a, c in SECCIONES:
        if a <= bar < a + c: return n
    return 'cola'

# Canales
MEL, CORO, ORG, CUER, BAJO, PAD, TIMP, RAYO, METAL, PERC = 0, 1, 2, 3, 4, 5, 6, 7, 8, 9
PROGRAMAS = {MEL: 60, CORO: 52, ORG: 19, CUER: 45, BAJO: 43, PAD: 48, TIMP: 47, RAYO: 53, METAL: 61}
NOMBRES = {MEL: 'Melodia (trompas)', CORO: 'CORO - losas', ORG: 'ORGANO - columnas de fuego',
           CUER: 'CUERDAS - proyectil', BAJO: 'Bajo', PAD: 'Colchon de cuerdas', TIMP: 'BASE - timbal',
           RAYO: 'RAYOS - parte C', METAL: 'Metales', PERC: 'BASE - percusion'}

ev = {}
partitura = []

def add(ch, t, msg):
    ev.setdefault(ch, []).append((int(round(t)), msg))

def nota(ch, bar, beat, dur, p, v):
    t = bar * BAR + beat * PPQ
    add(ch, t, mido.Message('note_on', channel=ch, note=p, velocity=max(1, min(127, int(v)))))
    add(ch, t + dur * PPQ - 5, mido.Message('note_off', channel=ch, note=p, velocity=0))

def acorde(ch, bar, beat, dur, ps, v):
    for p in ps: nota(ch, bar, beat, dur, p, v)

def peligro(voz, bar, beat, extra=None):
    """Apunta un evento del nivel. bar empieza en 0; beat en tiempos (0..3.75)."""
    e = {'voz': voz, 'compas': bar + 1, 'tiempo': round(beat + 1, 3),
         'segundo': round((bar * 4 + beat) * SEG_POR_TIEMPO, 4), 'seccion': seccion_de(bar)}
    if extra: e.update(extra)
    partitura.append(e)

# ─── Armonía ────────────────────────────────────────────────────────────────
E, F, G, A, B, C, D = 52, 53, 55, 57, 59, 48, 50   # raíces en la octava del colchón
MENOR, MAYOR = [0, 3, 7], [0, 4, 7]
def tri(raiz, tipo): return [raiz + i for i in tipo]

ESTROFA = [(E, MENOR), (F, MAYOR), (E, MENOR), (D, MAYOR)]
ESTRIBILLO = [(C, MAYOR), (D, MAYOR), (B - 12, MAYOR), (E, MENOR)]
PARTE_C = [(E, MENOR), (F, MAYOR), (G, MAYOR), (F, MAYOR)]

def armonia(bar):
    s = seccion_de(bar)
    if s in ('estribillo1', 'estribillo_final'): return ESTRIBILLO[(bar - 48) % 4]
    if s == 'parteC': return PARTE_C[bar % 4]
    if s == 'cola': return (E, MENOR)
    return ESTROFA[bar % 4]

# ─── Melodías (tiempo, duración, nota) ─────────────────────────────────────
TEMA_A = [  # 4 compases: el canto del templo
    [(0, 1, 64), (1, 0.5, 64), (1.5, 0.5, 65), (2, 1, 67), (3, 1, 69)],
    [(0, 1.5, 71), (1.5, 0.5, 69), (2, 1, 67), (3, 1, 65)],
    [(0, 1, 64), (1, 1, 67), (2, 1, 71), (3, 1, 74)],
    [(0, 2, 72), (2, 2, 71)],
]
TEMA_B = [  # 4 compases: el estribillo, más alto
    [(0, 1, 71), (1, 1.5, 76), (2.5, 0.5, 74), (3, 1, 76)],
    [(0, 1, 77), (1, 1, 76), (2, 1, 74), (3, 1, 72)],
    [(0, 1, 71), (1, 1, 72), (2, 1, 74), (3, 1, 75)],
    [(0, 3, 76), (3, 1, 71)],
]
TEMA_C = [  # parte C: frase corta y sombría que se repite
    [(0, 2, 64), (2, 1, 65), (3, 1, 64)],
    [(0, 3, 62), (3, 1, 64)],
]

def tocar_tema(ch, tema, bar0, compases, transp, v):
    for i in range(compases):
        for b, d, p in tema[i % len(tema)]:
            nota(ch, bar0 + i, b, d, p + transp, v)

# ─── Programas, reverberación y nombres de pista ───────────────────────────
for ch, pr in PROGRAMAS.items():
    add(ch, 0, mido.Message('program_change', channel=ch, program=pr))
    add(ch, 0, mido.Message('control_change', channel=ch, control=91, value=60))

# ─── BASE: pulso en todos los compases ─────────────────────────────────────
for bar in range(COMPASES):
    s = seccion_de(bar)
    raiz = armonia(bar)[0]
    # Bombo en cada tiempo (acento en el 1): el pulso que Suno no debe inventarse.
    for b in range(4):
        nota(PERC, bar, b, 0.25, 36, 112 if b == 0 else 88)
    # Pasos del guardián: taiko grave y timbal en 1 y 3.
    for b in (0, 2):
        nota(PERC, bar, b, 0.5, 41, 120 if b == 0 else 105)   # tom grave = taiko
        nota(TIMP, bar, b, 1, 40, 110 if b == 0 else 95)
        peligro('BASE', bar, b)
    if s in ('estrofa2', 'preestribillo', 'estribillo1', 'parteC', 'estribillo_final'):
        for b in (1, 3): nota(PERC, bar, b, 0.25, 38, 92)      # caja en 2 y 4
    if s == 'estribillo_final':
        for i in range(8):                                      # caja a doble tiempo
            if i % 2: nota(PERC, bar, i * 0.5, 0.25, 38, 70)
        for b in range(4): nota(PERC, bar, b + 0.5, 0.25, 42, 70)
    # Platillo al empezar cada sección
    for n, a, c in SECCIONES:
        if bar == a and bar > 0: nota(PERC, bar, 0, 1, 49, 118)
    # Bajo y colchón (no son peligros)
    if s != 'afinacion' or bar >= 4:
        nota(BAJO, bar, 0, 2, raiz - 12, 92); nota(BAJO, bar, 2, 2, raiz - 12, 84)
    if s != 'afinacion':
        acorde(PAD, bar, 0, 4, tri(*armonia(bar)), 62 if s != 'parteC' else 54)

# ─── Afinación: cada voz se presenta sola ──────────────────────────────────
# 1-2 solo la base; 3-4 el coro; 5-6 el órgano; 7-8 las cuerdas.
for bar, b in ((2, 2), (2, 2.5), (2, 3), (3, 2), (3, 2.5), (3, 3)):
    nota(CORO, bar, b, 0.5, 76 if b == 2 else 79 if b == 2.5 else 83, 112); peligro('CORO', bar, b)
for bar in (4, 5):
    acorde(ORG, bar, 0, 1, [40, 47, 52, 55], 118); peligro('ORGANO', bar, 0)
    acorde(ORG, bar, 2.5, 0.5, [41, 48, 53], 108); peligro('ORGANO', bar, 2.5)

def carrerilla(bar_llegada, v=100):
    """Cuatro semicorcheas de cuerdas en el tiempo 4 del compás anterior y llegada en el 1."""
    sube = [64, 67, 71, 74]
    for i, p in enumerate(sube):
        nota(CUER, bar_llegada - 1, 3 + i * 0.25, 0.25, p, v - 10 + i * 5)
    nota(CUER, bar_llegada, 0, 0.5, 76, v + 20)
    acorde(METAL, bar_llegada, 0, 0.5, [64, 71, 76], v)
    peligro('CUERDAS', bar_llegada, 0, {'sale': {'compas': bar_llegada, 'tiempo': 4.0}})

carrerilla(7)
carrerilla(8)  # la llegada al compás 9 abre la estrofa

# ─── Estrofa 1: base + coro, entra la melodía ──────────────────────────────
tocar_tema(MEL, TEMA_A, 8, 16, 0, 88)
for bar in range(8, 24):
    if bar < 16:
        if bar % 2 == 1:   # llamada de tres notas cada dos compases: una fila de losas
            for b, p in ((2, 76), (2.5, 79), (3, 83)):
                nota(CORO, bar, b, 0.5, p, 110); peligro('CORO', bar, b)
    else:              # segunda mitad: en todos los compases y en zigzag
        pat = ((1, 79), (3, 76)) if bar % 2 == 0 else ((0.5, 76), (2, 79), (3, 83))
        for b, p in pat:
            nota(CORO, bar, b, 0.5, p, 112); peligro('CORO', bar, b)

# ─── Estrofa 2: entra el órgano ────────────────────────────────────────────
tocar_tema(MEL, TEMA_A, 24, 16, 12, 92)
for bar in range(24, 40):
    raiz = armonia(bar)[0]
    org = [raiz - 12, raiz - 5, raiz]
    acorde(ORG, bar, 0, 1, org, 116); peligro('ORGANO', bar, 0)
    if bar >= 32:
        acorde(ORG, bar, 2.5, 0.5, org, 104); peligro('ORGANO', bar, 2.5)
    if bar % 2 == 1:
        for b, p in ((2, 76), (3, 79)):
            nota(CORO, bar, b, 0.5, p, 108); peligro('CORO', bar, b)

# ─── Pre-estribillo: entran las cuerdas (proyectiles) ──────────────────────
tocar_tema(MEL, TEMA_A[2:], 40, 8, 12, 96)
for llegada in (42, 44, 46):
    carrerilla(llegada, 104)
for bar in range(40, 48):
    acorde(ORG, bar, 0, 1, [40, 47, 52], 110); peligro('ORGANO', bar, 0)
for i in range(16):  # redoble que empuja al estribillo
    nota(PERC, 47, i * 0.25, 0.25, 38, 60 + i * 4)

# ─── Estribillo 1: todas las voces ─────────────────────────────────────────
carrerilla(48, 108)
tocar_tema(MEL, TEMA_B, 48, 16, 0, 108)
tocar_tema(CORO, TEMA_B, 48, 16, -12, 70)   # el coro canta la melodía por debajo (no es peligro)
for bar in range(48, 64):
    raiz = armonia(bar)[0]
    acorde(ORG, bar, 0, 1, [raiz - 12, raiz - 5, raiz], 116); peligro('ORGANO', bar, 0)
    pat = ((1.5, 88), (3.5, 91)) if bar % 2 == 0 else ((0.5, 91), (2.5, 88))
    for b, p in pat:   # losas en los contratiempos, una voz más aguda que la melodía
        nota(CORO, bar, b, 0.5, p, 114); peligro('CORO', bar, b)
for llegada in (52, 56, 60):
    carrerilla(llegada, 108)

# ─── Parte C: el encierro ──────────────────────────────────────────────────
tocar_tema(MEL, TEMA_C, 64, 24, 0, 84)
for bar in range(64, 88):
    # FUEGO: órgano grave en cada tiempo, el fuego que late.
    for b in range(4):
        acorde(ORG, bar, b, 0.75, [28, 40], 104 if b == 0 else 92); peligro('FUEGO', bar, b)
    # RAYOS: coro muy agudo, un barrido en el 2 y otro en el 4 (desde el compás 4 de la parte C,
    # los dos de cada compás; antes, uno cada dos).
    golpes = (1, 3) if bar >= 68 else ((1,) if bar % 2 == 0 else ())
    for b in golpes:
        nota(RAYO, bar, b, 0.5, 88 if b == 1 else 91, 118); peligro('RAYOS', bar, b)
# OLEADAS: platillo y timbal fuertes, una cada 6 compases.
for n, bar in enumerate((64, 70, 76, 82)):
    nota(PERC, bar, 0, 1, 49, 124); nota(PERC, bar, 0, 1, 57, 120)
    acorde(METAL, bar, 0, 2, [40, 47, 52, 59], 120)
    peligro('OLEADA', bar, 0, {'oleada': n + 1})
for i in range(16):  # redoble final que lleva al último estribillo
    nota(PERC, 87, i * 0.25, 0.25, 38, 60 + i * 4)
    nota(TIMP, 87, i * 0.25, 0.25, 40, 60 + i * 4)

# ─── Estribillo final: todo mezclado ───────────────────────────────────────
carrerilla(88, 112)
tocar_tema(MEL, TEMA_B, 88, 16, 12, 112)
tocar_tema(METAL, TEMA_B, 88, 16, 0, 96)
tocar_tema(CORO, TEMA_B, 88, 16, -12, 76)
for bar in range(88, 104):
    raiz = armonia(bar)[0]
    acorde(ORG, bar, 0, 1, [raiz - 12, raiz - 5, raiz], 118); peligro('ORGANO', bar, 0)
    acorde(ORG, bar, 2.5, 0.5, [raiz - 12, raiz - 5, raiz], 106); peligro('ORGANO', bar, 2.5)
    pat = ((1.5, 88), (3.5, 91)) if bar % 2 == 0 else ((0.5, 91), (2, 88), (3, 95))
    for b, p in pat:
        nota(CORO, bar, b, 0.5, p, 116); peligro('CORO', bar, b)
    if bar >= 96 and bar % 2 == 1:
        nota(RAYO, bar, 3, 0.5, 91, 118); peligro('RAYOS', bar, 3)
for llegada in (92, 96, 100):
    carrerilla(llegada, 112)

# ─── Cola: el suelo cede (golpe en el 1 del compás 105) y acorde largo ─────
FIN = COMPASES
nota(PERC, FIN, 0, 2, 49, 127); nota(PERC, FIN, 0, 1, 36, 127); nota(TIMP, FIN, 0, 4, 40, 127)
acorde(ORG, FIN, 0, 8, [28, 40, 47, 52], 120)
acorde(METAL, FIN, 0, 6, [52, 59, 64, 71], 118)
acorde(PAD, FIN, 0, 8, [64, 67, 71, 76], 90)
acorde(CORO, FIN, 0, 8, [76, 79, 83], 100)
peligro('FIN', FIN, 0, {'nota': 'el suelo cede; salto al vacío y vuelve el vuelo'})

# ─── Escribir el MIDI ──────────────────────────────────────────────────────
mid = mido.MidiFile(ticks_per_beat=PPQ)
mid.tracks.append(mido.MidiTrack([
    mido.MetaMessage('track_name', name='Templo del Coro - guia', time=0),
    mido.MetaMessage('set_tempo', tempo=int(60e6 / BPM), time=0),
    mido.MetaMessage('time_signature', numerator=4, denominator=4, time=0),
    *[mido.MetaMessage('marker', text=n, time=0) for n in []],
    mido.MetaMessage('end_of_track', time=(COMPASES + COLA) * BAR)]))
# Marcadores de sección en una pista propia
marc = mido.MidiTrack([mido.MetaMessage('track_name', name='Secciones', time=0)])
last = 0
for n, a, c in SECCIONES:
    marc.append(mido.MetaMessage('marker', text=n, time=a * BAR - last)); last = a * BAR
mid.tracks.append(marc)
for ch in sorted(ev):
    tr = mido.MidiTrack([mido.MetaMessage('track_name', name=NOMBRES[ch], time=0)])
    l = sorted(ev[ch], key=lambda x: (x[0], x[1].type == 'note_on'))
    last = 0
    for t, m in l:
        tr.append(m.copy(time=t - last)); last = t
    mid.tracks.append(tr)
mid.save('templo_del_coro_guia.mid')

partitura.sort(key=lambda e: (e['segundo'], e['voz']))
with open('partitura.json', 'w', encoding='utf-8') as f:
    json.dump({'bpm': BPM, 'compas': '4/4', 'compases': COMPASES,
               'segundos_por_compas': round(4 * SEG_POR_TIEMPO, 6),
               'secciones': [{'nombre': n, 'desde_compas': a + 1, 'compases': c,
                              'segundo': round(a * 4 * SEG_POR_TIEMPO, 4)} for n, a, c in SECCIONES],
               'eventos': partitura}, f, ensure_ascii=False, indent=1)

from collections import Counter
print('ok', round(mid.length, 1), 's ·', len(partitura), 'eventos ·', dict(Counter(e['voz'] for e in partitura)))
