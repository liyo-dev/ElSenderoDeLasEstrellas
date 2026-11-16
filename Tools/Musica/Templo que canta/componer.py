# Maqueta guía de «El templo que canta» (INC-1211): la canción que manda en el nivel entero.
#
# 120 BPM, 4/4 (1 compás = 2 s) hasta el estribillo, que sube a 132.
# Secciones, en compases, como el diseño en papel:
#   Intro            8   solo el pulso y el órgano de piedra, lejos
#   Estrofa 1       16   huida: losas que se hunden, marea detrás
#   Isla 1          24   balanza: el guardián se burla, coro que baja y sube
#   Estrofa 2       16   pasillo de lado: faros de luz
#   Interruptor      4   corto y urgente
#   Isla 2          32   los tres separados: tres voces que no se juntan
#   Estribillo      24   a 132, todo el coro, y el guardián calla al final
#
# La gracia para el blockout: el bombo y el órgano marcan TODOS los tiempos, con acento en el 1,
# para que Suno no se invente el pulso y el nivel pueda cortarse a compás.
#
# Uso:  python componer.py [salida.mid]
# Audio: fluidsynth -F guia.mp3 -r 44100 soundfont.sf2 salida.mid (o el que uses siempre)
import sys, mido

PPQ = 480
BAR = 4 * PPQ

# (nombre, compases, bpm)
SECCIONES = [
    ("Intro", 8, 120),
    ("Estrofa 1", 16, 120),
    ("Isla 1", 24, 120),
    ("Estrofa 2", 16, 120),
    ("Interruptor", 4, 120),
    ("Isla 2", 32, 120),
    ("Estribillo", 24, 132),
]

ORGANO, CORO, TIMP, CUERDA, METAL, CEL = 0, 1, 2, 3, 4, 5
PERC = 9

mid = mido.MidiFile(ticks_per_beat=PPQ)
ev = {}


def add(ch, t, msg):
    ev.setdefault(ch, []).append((int(t), msg))


def nota(ch, bar, beat, dur, p, v):
    t = bar * BAR + int(beat * PPQ)
    add(ch, t, mido.Message('note_on', channel=ch, note=int(p), velocity=max(1, min(127, int(v)))))
    add(ch, t + int(dur * PPQ) - 5, mido.Message('note_off', channel=ch, note=int(p), velocity=0))


def acorde(ch, bar, beat, dur, ps, v):
    for p in ps:
        nota(ch, bar, beat, dur, p, v)


def prog(ch, pr):
    add(ch, 0, mido.Message('program_change', channel=ch, program=pr))


# Órgano de piedra, coro, timbal, cuerdas, metal y celesta
for ch, p in [(ORGANO, 19), (CORO, 52), (TIMP, 47), (CUERDA, 48), (METAL, 61), (CEL, 8)]:
    prog(ch, p)
for ch in (ORGANO, CORO, CUERDA, METAL, CEL):
    add(ch, 0, mido.Message('control_change', channel=ch, control=91, value=70))  # reverb: templo

# Armonía: dos compases por acorde, en re menor con la subida de la isla al relativo mayor.
# raíz MIDI, tercera, quinta
ACORDES = {
    'Dm': [50, 53, 57],
    'Bb': [46, 50, 53],
    'Gm': [43, 46, 50],
    'A':  [45, 49, 52],
    'F':  [41, 45, 48],
    'C':  [48, 52, 55],
}
PROGRESION = ['Dm', 'Bb', 'Gm', 'A', 'Dm', 'Bb', 'F', 'C']

# Motivo del guardián: cuatro notas que bajan y se quedan (se burla del trío)
MOTIVO = [(0, 0.5, 74), (0.5, 0.5, 73), (1, 0.5, 71), (1.5, 2.5, 69)]

compas = 0
for nombre, compases, bpm in SECCIONES:
    isla = nombre.startswith("Isla")
    estribillo = nombre == "Estribillo"
    fuerza = 60 + (10 if isla else 0) + (25 if estribillo else 0)

    for i in range(compases):
        bar = compas + i
        acorde_nombre = PROGRESION[i % len(PROGRESION)]
        t = ACORDES[acorde_nombre]
        v = fuerza + (8 if i % 8 >= 4 else 0)

        # 1) El pulso: timbal y bombo en todos los tiempos, acento en el 1. Es lo que engancha el nivel.
        for b in range(4):
            nota(TIMP, bar, b, 0.4, 38 if b == 0 else 33, v + (18 if b == 0 else 0))
            nota(PERC, bar, b, 0.25, 36, v + (22 if b == 0 else 0))
        # Contratiempo de caja a partir de la estrofa 1
        if nombre != "Intro":
            for b in (1.5, 3.5):
                nota(PERC, bar, b, 0.2, 38, v - 5)

        # 2) Órgano de piedra: acordes largos, la voz del templo
        acorde(ORGANO, bar, 0, 3.9, [x - 12 for x in t], v + 5)

        # 3) Cuerdas: en las huidas, corcheas que empujan; en las islas, notas largas
        if estribillo or nombre.startswith("Estrofa") or nombre == "Interruptor":
            for k in range(8):
                nota(CUERDA, bar, k * 0.5, 0.45, t[k % 3] + 12, v - (0 if k % 2 == 0 else 18))
        else:
            acorde(CUERDA, bar, 0, 3.5, [x + 12 for x in t], v - 10)

        # 4) Coro: en las islas canta el motivo (se burla); en el estribillo, acordes abiertos
        if isla and i % 4 == 0:
            for b, d, p in MOTIVO:
                nota(CORO, bar, b, d, p - 12, v)
        elif estribillo:
            acorde(CORO, bar, 0, 1.9, [x + 12 for x in t], v + 5)
            acorde(CORO, bar, 2, 1.9, [x + 12 for x in t], v)

        # 5) Metal: golpes en el 1 de cada cuatro compases; en el estribillo, cada dos
        if (estribillo and i % 2 == 0) or (not estribillo and i % 4 == 0):
            acorde(METAL, bar, 0, 0.6, [x + 12 for x in t], v + 20)

        # 6) Celesta: la estrella lejana, solo en las islas y en el estribillo
        if isla or estribillo:
            nota(CEL, bar, 3, 0.9, t[2] + 24, v - 15)

    compas += compases

# Mapa de tempo: el estribillo entra a 132
tick_estribillo = sum(c for _, c, _ in SECCIONES[:-1]) * BAR
meta = mido.MidiTrack([
    mido.MetaMessage('set_tempo', tempo=int(60e6 / 120), time=0),
    mido.MetaMessage('set_tempo', tempo=int(60e6 / 132), time=tick_estribillo),
    mido.MetaMessage('end_of_track', time=compas * BAR - tick_estribillo),
])
mid.tracks.append(meta)

for ch, lista in ev.items():
    tr = mido.MidiTrack()
    lista.sort(key=lambda x: (x[0], x[1].type == 'note_on'))
    last = 0
    for t, m in lista:
        tr.append(m.copy(time=t - last))
        last = t
    mid.tracks.append(tr)

salida = sys.argv[1] if len(sys.argv) > 1 else 'templo_que_canta_guia.mid'
mid.save(salida)
print('ok', compas, 'compases,', round(mid.length, 1), 's')
