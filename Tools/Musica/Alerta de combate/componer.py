# Maqueta guía de la «Alerta de combate» (INC-994): el aviso de que un NPC te reta y va a empezar la pelea.
# Re menor, 144 BPM, 24 compases en bucle (sin final: el último compás lleva al primero).
# Ostinato de cuerdas en semicorcheas, golpes de metal, taikos y caja, y un motivo de alerta de trompas
# (cuatro notas que suben) que se repite más alto en la segunda mitad. Se versiona en Suno con «Cover».
import sys, mido
PPQ = 480; BAR = 4 * PPQ; BPM = 144; COMPASES = 24
mid = mido.MidiFile(ticks_per_beat=PPQ)
ev = {}
def add(ch, t, msg): ev.setdefault(ch, []).append((int(t), msg))
def nota(ch, bar, beat, dur, p, v):
    t = bar * BAR + beat * PPQ
    add(ch, t, mido.Message('note_on', channel=ch, note=p, velocity=max(1, min(127, int(v)))))
    add(ch, t + dur * PPQ - 5, mido.Message('note_off', channel=ch, note=p, velocity=0))
def acorde(ch, bar, beat, dur, ps, v):
    for p in ps: nota(ch, bar, beat, dur, p, v)
def prog(ch, pr): add(ch, 0, mido.Message('program_change', channel=ch, program=pr))
CUERDA, BAJO, METAL, TROMPA, TIMP, PERC, CEL = 0, 1, 2, 3, 4, 9, 5
for ch, p in [(CUERDA, 48), (BAJO, 43), (METAL, 61), (TROMPA, 60), (TIMP, 47), (CEL, 8)]: prog(ch, p)
for ch in (CUERDA, BAJO, METAL, TROMPA, TIMP, CEL): add(ch, 0, mido.Message('control_change', channel=ch, control=91, value=55))
# Armonía (un acorde por compás): Dm Dm Bb C | Dm Dm Gm A ... ×3
ciclo = [('Dm', 50), ('Dm', 50), ('Bb', 46), ('C', 48), ('Dm', 50), ('Dm', 50), ('Gm', 43), ('A', 45)]
triada = {'Dm': [0, 3, 7], 'Bb': [0, 4, 7], 'C': [0, 4, 7], 'Gm': [0, 3, 7], 'A': [0, 4, 7]}
for bar in range(COMPASES):
    nombre, raiz = ciclo[bar % 8]
    t = [raiz + i for i in triada[nombre]]
    fuerza = 70 + (12 if bar >= 8 else 0) + (12 if bar >= 16 else 0)
    # Ostinato: raíz-quinta-octava-quinta en semicorcheas
    patron = [t[0] + 12, t[2] + 12, t[0] + 24, t[2] + 12]
    for i in range(16): nota(CUERDA, bar, i * 0.25, 0.25, patron[i % 4], fuerza - (0 if i % 4 == 0 else 15))
    nota(BAJO, bar, 0, 1.5, raiz - 12, fuerza + 10); nota(BAJO, bar, 2, 1, raiz - 12, fuerza); nota(BAJO, bar, 3.5, 0.5, raiz - 12, fuerza)
    # Golpes de metal en 1 y en el «y» del 2
    acorde(METAL, bar, 0, 0.5, [x + 12 for x in t], fuerza + 15); acorde(METAL, bar, 2.5, 0.5, [x + 12 for x in t], fuerza)
    # Taiko / bombo y caja
    for b in (0, 1.5, 2, 3): nota(PERC, bar, b, 0.25, 36, fuerza + 20 if b == 0 else fuerza)
    for b in (1, 3): nota(PERC, bar, b, 0.25, 38, fuerza)
    if bar % 4 == 3:  # redoble de caja que empuja al siguiente bloque
        for i in range(8): nota(PERC, bar, 2 + i * 0.25, 0.25, 38, 60 + i * 8)
        nota(TIMP, bar, 3, 1, raiz - 12, 100)
    nota(TIMP, bar, 0, 1, raiz - 12, fuerza)
# Motivo de alerta (trompas): La-Do-Re-Mi… y se queda en Fa
motivo = [(0, 0.5, 69), (0.5, 0.5, 72), (1, 0.5, 74), (1.5, 0.5, 76), (2, 2, 77)]
for bar in (2, 6, 10, 14, 18, 22):
    alto = 12 if bar >= 16 else 0
    for b, d, p in motivo: nota(TROMPA, bar, b, d, p - 12 + alto, 95 + (15 if bar >= 16 else 0))
    for b, d, p in motivo: nota(CEL, bar, b, d, p + 12, 60)
mid.tracks.append(mido.MidiTrack([mido.MetaMessage('set_tempo', tempo=int(60e6 / BPM), time=0), mido.MetaMessage('end_of_track', time=COMPASES * BAR)]))
for ch, l in ev.items():
    tr = mido.MidiTrack(); l.sort(key=lambda x: (x[0], x[1].type == 'note_on')); last = 0
    for t, m in l: tr.append(m.copy(time=t - last)); last = t
    mid.tracks.append(tr)
salida = sys.argv[1] if len(sys.argv) > 1 else 'alerta_de_combate_guia.mid'
mid.save(salida); print('ok', round(mid.length, 1), 's')
