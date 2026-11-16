# Pasa el MIDI que exporta Suno a la partitura que lee Unity: los tiempos exactos de la toma.
# No hay análisis de audio: los golpes vienen escritos en el MIDI, con su tempo humanizado.
#
# Uso:  python partitura.py "C:\ruta\cancion.mid" "Assets\ElSendero\Data\Templo que canta\partitura.json"
import json
import sys

import mido

entrada = sys.argv[1]
salida = sys.argv[2]

mid = mido.MidiFile(entrada)
ticks = mid.ticks_per_beat

# Tempo por tiempo (Suno lo cambia casi cada tiempo): se usa para pasar ticks a segundos.
tempos = []
for tr in mid.tracks:
    t = 0
    for m in tr:
        t += m.time
        if m.type == 'set_tempo':
            tempos.append((t, m.tempo))
tempos.sort()


def segundos(tick):
    s, prev_t, prev_us = 0.0, 0, 500000
    for t, us in tempos:
        if t >= tick:
            break
        s += (t - prev_t) / ticks * prev_us / 1e6
        prev_t, prev_us = t, us
    return s + (tick - prev_t) / ticks * prev_us / 1e6


tiempos = []      # todos los golpes (el «tan»)
acentos = []      # el «PUM» (nota 38, caja con acento)
for tr in mid.tracks:
    t = 0
    for m in tr:
        t += m.time
        if m.type != 'note_on' or m.velocity == 0:
            continue
        if m.channel != 9:
            continue
        s = segundos(t)
        tiempos.append(round(s, 4))
        if m.note == 38:
            acentos.append(round(s, 4))

tiempos = sorted(set(tiempos))
acentos = sorted(set(acentos))

datos = {
    "cancion": entrada.split('\\')[-1],
    "duracion": round(segundos(max(t for t, _ in tempos) if tempos else 0), 2),
    "bpmMedio": round(len(tiempos) / (tiempos[-1] / 60.0), 1) if len(tiempos) > 1 else 0,
    "compases": len(acentos),
    "tiempos": tiempos,
    "acentos": acentos,
}

with open(salida, "w", encoding="utf-8") as f:
    json.dump(datos, f, ensure_ascii=False, indent=1)

print("golpes:", len(tiempos), "acentos:", len(acentos), "bpm medio:", datos["bpmMedio"])
print("primer acento:", acentos[0] if acentos else "-", "· último:", acentos[-1] if acentos else "-")
