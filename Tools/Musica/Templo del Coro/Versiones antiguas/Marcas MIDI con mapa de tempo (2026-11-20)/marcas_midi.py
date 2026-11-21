# Pasa la partitura de peligros (partitura.json) a un MIDI para abrirlo junto a la toma en un DAW.
# El mapa de tempo sigue los tiempos medidos de la canción: cada negra cae en un tiempo real de la toma,
# así los compases del DAW coinciden con los de la canción. Una pista por peligro y marcadores de sección.
# Uso: python3 marcas_midi.py  →  templo_del_coro_marcas.mid
import json, mido
p = json.load(open('partitura.json', encoding='utf-8'))
T = p['tiempos_s']; PPQ = 480
NOTA = {'BASE': 36, 'PRESENTACION': 69, 'LOSA': 72, 'COLUMNA': 48, 'PROYECTIL': 64, 'RAYO': 79, 'FUEGO': 41, 'OLEADA': 38, 'FIN': 60}
NOMBRE = {'BASE': 'Pasos del guardian (base)', 'PRESENTACION': 'Afinacion: presentaciones', 'LOSA': 'Cae (notas de piedra)',
          'COLUMNA': 'Columnas de fuego', 'PROYECTIL': 'Regalitos (se devuelven con B)', 'RAYO': 'Rayos', 'FUEGO': 'Fuego que late (puente)',
          'OLEADA': 'Oleadas de coristas', 'FIN': 'Golpe final'}
# Rejilla: tiempo 0 del MIDI = 0 s. Una negra de anacrusa hasta el primer tiempo medido (compás de 1/4) y luego 4/4.
bordes = [0.0] + T
def a_ticks(s):
    import bisect
    i = bisect.bisect_right(bordes, s) - 1
    i = max(0, min(i, len(bordes) - 2))
    frac = (s - bordes[i]) / (bordes[i + 1] - bordes[i])
    return int(round((i + frac) * PPQ))
fin_s = max(e['segundo'] for e in p['eventos']) + 2.0
meta = mido.MidiTrack([mido.MetaMessage('track_name', name='La funcion del guardian - marcas', time=0),
                       mido.MetaMessage('time_signature', numerator=1, denominator=4, time=0)])
eventos_meta = []
for i in range(len(bordes) - 1):
    eventos_meta.append((i * PPQ, mido.MetaMessage('set_tempo', tempo=int((bordes[i + 1] - bordes[i]) * 1e6))))
eventos_meta.append((PPQ, mido.MetaMessage('time_signature', numerator=4, denominator=4)))
for s in p['secciones']:
    eventos_meta.append((a_ticks(s['desde_s']), mido.MetaMessage('marker', text=s['nombre'])))
eventos_meta.sort(key=lambda x: x[0]); last = 0
for t, m in eventos_meta: meta.append(m.copy(time=t - last)); last = t
mid = mido.MidiFile(ticks_per_beat=PPQ); mid.tracks.append(meta)
for ch, voz in enumerate([v for v in NOTA if any(e['voz'] == v for e in p['eventos'])]):
    c = ch if ch < 9 else ch + 1
    tr = mido.MidiTrack([mido.MetaMessage('track_name', name=NOMBRE[voz], time=0)]); evs = []
    for e in p['eventos']:
        if e['voz'] != voz: continue
        t0 = a_ticks(e['segundo'])
        evs.append((t0, mido.Message('note_on', channel=c, note=NOTA[voz], velocity=100)))
        evs.append((t0 + PPQ // 4, mido.Message('note_off', channel=c, note=NOTA[voz], velocity=0)))
    evs.sort(key=lambda x: (x[0], x[1].type == 'note_on')); last = 0
    for t, m in evs: tr.append(m.copy(time=t - last)); last = t
    mid.tracks.append(tr)
mid.save('templo_del_coro_marcas.mid')
# Comprobación: segundos reales de las primeras notas de cada pista
import itertools
for tr in mid.tracks[1:]:
    print(tr.name, end=': ')
print('\nduración MIDI', round(mid.length, 2), 's')
