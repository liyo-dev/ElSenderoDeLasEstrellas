# Maqueta guía de «El deseo del Mago Oscuro»: los 31,5 s del principio del prólogo
# (DeseoEnElSenderoBeat), entera con el tema del mago, que se va desarrollando y acaba en un
# golpe («chan») que se apaga mientras el papel tapa la pantalla; justo después entra la crecida
# del vuelo hacia el valle (Tools/Musica/Amanecer en el Valle - apertura/).
#
# Re menor. Motivo del mago: Re-Fa-Mi-Re | Si♭-La. El golpe final es Re vacío (Re-La-Re, sin
# tercera): oscuro, y a la vez la dominante de Sol, la tonalidad de la crecida que viene después,
# así que el enganche no choca.
#
# Un tiempo = 1 s (60 por minuto), así los tiempos son segundos de la escena, y siguen los de la
# cinemática ampliada (AperturaDelValleBeats.DeseoEnElSenderoBeat):
#   0-2      sale del negro: nota grave que aparece, campana lejana
#   2-6,5    de espaldas contra el halo: el motivo abajo, chelos y contrabajos
#   6,5-10   los ojos se abren en la oscuridad; el motivo sube a las trompas con coro grave y un
#            pulso de cuerdas debajo; crece
#   10-14,5  de frente; pide el deseo (10,6): se adelgaza para dejar pasar la voz (temblor de
#            cuerdas, pedal grave, dos suspiros de trompa)
#   14,5     estalla el altar: golpe (no es el final, el pulso sigue); «¡Por fin!» (16,2), acento
#   17,8-22  la carcajada: el motivo corto y seco, en trombones y cuerdas pizzicato, con temblor
#            arriba y un redoble que llena el final
#   22-23,2  los ojos, de golpe muy abiertos: todo se corta y quedan una campana y el corazón
#   23,6     gira la cabeza: acento seco
#   24-27,5  aparece la estrella: celesta, arpa y cuerdas sobre Re, sin tapar «…¿Y ese poder?» (24,4)
#   27,5-30,3 crece la estrella hasta llenar la pantalla (Si♭-Do-Re), con redoble y coro
#   30,3     «CHAN»: tutti en Re vacío; se apaga con el fundido a papel (30,3-31,5)
import mido, sys
PPQ = 480
mid = mido.MidiFile(ticks_per_beat=PPQ)
ev = {}
def tk(s): return int(round(s * PPQ))
def add(ch, t, msg): ev.setdefault(ch, []).append((int(t), msg))
def nota_off(ch, t, p): add(ch, tk(t) - 5, mido.Message('note_off', channel=ch, note=p, velocity=0))
def acorde(ch, t, dur, ps, v):
    for p in ps: add(ch, tk(t), mido.Message('note_on', channel=ch, note=p, velocity=max(1, min(127, int(v))))); nota_off(ch, t + dur, p)
def nota(ch, t, dur, p, v):
    add(ch, tk(t), mido.Message('note_on', channel=ch, note=p, velocity=max(1, min(127, int(v)))))
    nota_off(ch, t + dur, p)
def prog(ch, pr): add(ch, 0, mido.Message('program_change', channel=ch, program=pr))
def expr(ch, t0, t1, v0, v1, pasos=24):
    for i in range(pasos + 1):
        add(ch, tk(t0 + (t1 - t0) * i / pasos), mido.Message('control_change', channel=ch, control=11, value=int(v0 + (v1 - v0) * i / pasos)))

CBAJO, CHELO, CUERDA, TREMOLO, CORO, TROMPA, TROMBON, TUBA, TIMP, PERC, CAMPANA, PULSO, FLAUTA, CELESTA, ARPA = 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14
for ch, p in [(CBAJO, 43), (CHELO, 42), (CUERDA, 48), (TREMOLO, 44), (CORO, 52), (TROMPA, 60),
              (TROMBON, 57), (TUBA, 58), (TIMP, 47), (CAMPANA, 14), (PULSO, 45), (FLAUTA, 73),
              (CELESTA, 8), (ARPA, 46)]:
    prog(ch, p)
for ch in range(15): add(ch, 0, mido.Message('control_change', channel=ch, control=91, value=100))

MOTIVO = [(0.0, 0.75, 0), (0.75, 0.75, 3), (1.5, 0.5, 2), (2.0, 0.5, 0), (2.5, 0.75, -4), (3.25, 1.25, -5)]  # Re Fa Mi Re | Si♭ La
def motivo(ch, t0, raiz, v, escala=1.0, octava_abajo=None):
    for t, d, i in MOTIVO:
        nota(ch, t0 + t * escala, d * escala, raiz + i, v)
        if octava_abajo is not None: nota(octava_abajo, t0 + t * escala, d * escala, raiz + i - 12, v - 10)

# ── 0-2: sale del negro. ──
nota(CBAJO, 0.0, 2.8, 26, 70); expr(CBAJO, 0.0, 2.0, 20, 110)
nota(TUBA, 0.3, 2.6, 26, 36)
nota(CAMPANA, 0.6, 2.6, 50, 46)

# ── 2-6,5: el motivo abajo, chelos y contrabajos, mientras se acerca la silueta. ──
motivo(CHELO, 2.0, 38, 84, escala=0.8, octava_abajo=CBAJO)
nota(CBAJO, 2.0, 4.4, 26, 80)
nota(TUBA, 2.0, 4.4, 26, 30)
nota(CAMPANA, 5.9, 0.8, 50, 34)   # el eco del motivo

# ── 6,5-10: los ojos se abren; el motivo sube a las trompas con coro grave y pulso; crece. ──
for i in range(14): nota(PULSO, 6.5 + i * 0.25, 0.18, 38, 55 + 3 * i)
motivo(TROMPA, 6.6, 50, 78, escala=0.62)
acorde(CORO, 6.5, 3.4, [38, 45, 50], 58); expr(CORO, 6.5, 9.9, 60, 118)
nota(CBAJO, 6.5, 3.5, 26, 86)
for i in range(16): nota(TIMP, 9.2 + i * 0.05, 0.05, 38, 40 + 5 * i)

# ── 10-14,5: de frente; pide el deseo (10,6). Se adelgaza para dejar pasar la voz. ──
acorde(TREMOLO, 10.0, 4.4, [50, 53, 57, 62], 78); expr(TREMOLO, 10.0, 14.4, 66, 92)
nota(CBAJO, 10.0, 4.4, 26, 78)
acorde(CORO, 10.0, 4.2, [38, 45, 50], 56)
nota(TROMPA, 11.2, 1.1, 57, 58)
nota(TROMPA, 13.0, 1.3, 62, 62)

# ── 14,5-17,8: estalla el altar; recibe el poder; «¡Por fin!» (16,2). ──
acorde(CUERDA, 14.5, 0.9, [34, 50, 53, 58, 62, 65], 118)
acorde(TROMBON, 14.5, 0.8, [34, 41, 46], 118); nota(TUBA, 14.5, 0.8, 22, 118)
nota(TIMP, 14.5, 0.5, 34, 127); nota(CAMPANA, 14.5, 1.4, 50, 100)
nota(PERC, 14.5, 1.1, 49, 115); nota(PERC, 14.5, 0.5, 35, 127)
for ch in (CUERDA, TROMBON, TUBA): expr(ch, 14.7, 15.3, 127, 62, pasos=8)
motivo(TROMBON, 14.8, 38, 100, escala=0.68, octava_abajo=TUBA)
motivo(CORO, 14.8, 50, 86, escala=0.68)
nota(CBAJO, 14.8, 3.1, 26, 92)
for i in range(12): nota(PULSO, 14.8 + i * 0.25, 0.2, 38, 70 + 2 * i)
acorde(TROMPA, 16.2, 0.35, [62, 69], 110)         # «¡Por fin!»
nota(TIMP, 16.2, 0.4, 38, 120); nota(PERC, 16.2, 0.45, 49, 100)

# ── 17,8-22: la carcajada. El motivo corto y seco; temblor arriba; el redoble llena el final. ──
for rep in range(4):
    base = 17.8 + rep * 1.0
    for j, p in enumerate([38, 41, 40, 38, 34, 33]):                 # Re Fa Mi Re | Si♭ La
        nota(TROMBON, base + j * 0.14, 0.10, p, 92 - 3 * j)
        nota(PULSO, base + j * 0.14, 0.08, p + 12, 80)
acorde(TREMOLO, 17.8, 4.0, [62, 65, 69, 74], 60); expr(TREMOLO, 17.8, 21.8, 55, 110)
nota(CBAJO, 17.8, 4.0, 26, 80)
for i in range(5): nota(TIMP, 17.8 + i * 0.8, 0.22, 38, 55 + 6 * i)
nota(PERC, 21.4, 0.5, 51, 70)
for i in range(10): nota(TIMP, 21.2 + i * 0.06, 0.06, 38, 60 + 7 * i)

# ── 22-23,2: los ojos, de golpe muy abiertos. Todo se corta; quedan la campana y el corazón. ──
for ch in range(15):
    if ch != PERC: add(ch, tk(22.0), mido.Message('control_change', channel=ch, control=123, value=0))
nota(CAMPANA, 22.02, 1.1, 86, 46)
nota(CBAJO, 22.0, 1.2, 26, 55)
nota(TIMP, 22.15, 0.2, 38, 55); nota(TIMP, 22.85, 0.2, 38, 46)

# ── 23,6: gira la cabeza; acento seco. ──
acorde(TREMOLO, 23.6, 0.18, [50, 53, 56, 62], 120)
nota(TIMP, 23.6, 0.25, 38, 115); nota(PERC, 23.6, 0.3, 35, 110)

# ── 24-27,5: aparece la estrella. Celesta, arpa y cuerdas sobre Re, sin tapar la frase (24,4). ──
nota(CBAJO, 23.6, 3.9, 26, 70)
acorde(CUERDA, 24.0, 3.4, [50, 57, 62, 65], 74); expr(CUERDA, 24.0, 27.4, 68, 96)
for i, p in enumerate([74, 77, 81, 86, 84, 81, 77, 74]):
    nota(CELESTA, 24.0 + i * 0.42, 0.8, p, 72 + (i % 2) * 6)
for i, p in enumerate([62, 65, 69, 74]):
    nota(ARPA, 24.2 + i * 0.7, 1.0, p, 70)

# ── 27,5-30,3: la estrella llena la pantalla; crece (Si♭-Do-Re). ──
acorde(CORO, 27.5, 1.1, [46, 50, 53], 70)
acorde(CORO, 28.6, 1.0, [48, 52, 55], 82)
acorde(CORO, 29.6, 0.7, [50, 54, 57], 96)
acorde(CUERDA, 27.5, 2.8, [34, 46, 50, 53], 60); expr(CUERDA, 27.5, 30.2, 60, 120)
nota(CBAJO, 27.5, 2.8, 26, 85)
nota(TUBA, 27.5, 2.8, 26, 50)
for i in range(14): nota(TIMP, 27.6 + i * 0.18, 0.12, 38, 40 + 6 * i)
for i, p in enumerate([74, 77, 81, 86, 89, 93]):
    nota(ARPA, 27.8 + i * 0.4, 0.6, p - 12, 52 + 4 * i)
    nota(CELESTA, 27.9 + i * 0.4, 0.5, p + 12, 46 + 4 * i)

# ── 30,3: «CHAN» en Re vacío, y se apaga con el fundido a papel. ──
acorde(CUERDA, 30.3, 1.15, [26, 38, 45, 50, 57, 62, 69, 74], 127)
acorde(CORO, 30.3, 1.15, [38, 45, 50, 57], 124)
acorde(TROMBON, 30.3, 1.05, [26, 38, 45], 127); nota(TUBA, 30.3, 1.05, 26, 127)
acorde(TROMPA, 30.3, 1.05, [50, 57, 62], 127)
nota(TIMP, 30.3, 0.6, 38, 127); nota(TIMP, 30.3, 0.6, 26 + 12, 127)
nota(CAMPANA, 30.3, 1.15, 50, 120)
nota(PERC, 30.3, 1.15, 49, 127); nota(PERC, 30.3, 0.6, 35, 127); nota(PERC, 30.32, 0.6, 36, 120)
for ch in (CUERDA, CORO, TROMBON, TUBA, TROMPA, CAMPANA): expr(ch, 30.5, 31.45, 127, 0, pasos=18)

meta = mido.MidiTrack()
meta.append(mido.MetaMessage('set_tempo', tempo=1000000, time=0))
meta.append(mido.MetaMessage('end_of_track', time=tk(31.5)))
mid.tracks.append(meta)
for ch, l in ev.items():
    tr = mido.MidiTrack(); l.sort(key=lambda x: (x[0], x[1].type == 'note_on')); last = 0
    for t, m in l: tr.append(m.copy(time=t - last)); last = t
    mid.tracks.append(tr)
sal = sys.argv[1] if len(sys.argv) > 1 else 'deseo_mago_oscuro_guia_tema.mid'
mid.save(sal); print('ok', round(mid.length, 2), 's')
