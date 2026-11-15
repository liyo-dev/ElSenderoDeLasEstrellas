# Maqueta guía de «El deseo del Mago Oscuro» (versión «todo el tema»): los 16,5 s del principio del prólogo
# (DeseoEnElSenderoBeat), entera con el tema del mago, que se va desarrollando y acaba en un golpe («chan»)
# que se apaga mientras el papel tapa la pantalla; justo después entra la crecida del vuelo hacia el valle.
#
# Re menor. Motivo del mago: Re-Fa-Mi-Re | Si♭-La. El golpe final es Re vacío (Re-La-Re, sin tercera): oscuro,
# y a la vez la dominante de Sol, la tonalidad de la crecida que viene después, así que el enganche no choca.
#
# Un tiempo = 1 s (60 por minuto), así los tiempos son segundos de la escena:
#   0-1,5    sale del negro: nota grave que aparece, campana lejana
#   1,5-4,5  de espaldas contra el halo: el motivo abajo, chelos y contrabajos
#   4,5-7,2  de frente, los ojos; pide el deseo (5,0): el motivo sube a las trompas con coro grave y un
#            pulso de cuerdas graves debajo; crece
#   7,2      estalla el altar: golpe (no es el final, el pulso sigue)
#   8,2-12   el motivo entero en trombones y coro, con una segunda frase que se retuerce (Sol-Si♭-La-Sol#);
#            10,2 (gira la cabeza): acento seco de cuerdas
#   12-15,2  todo sube por escalones sobre La (la dominante), redoble de timbal, coro hacia arriba
#   15,2     ¡CHAN!: tutti en Re vacío, platillo, bombo y campana; se apaga con el fundido a papel (15,3-16,5)
import mido, sys
PPQ = 480
mid = mido.MidiFile(ticks_per_beat=PPQ)
ev = {}
def tk(s): return int(round(s * PPQ))
def add(ch, t, msg): ev.setdefault(ch, []).append((int(t), msg))
def nota(ch, t, dur, p, v):
    add(ch, tk(t), mido.Message('note_on', channel=ch, note=p, velocity=max(1, min(127, int(v)))))
    add(ch, tk(t + dur) - 5, mido.Message('note_off', channel=ch, note=p, velocity=0))
def acorde(ch, t, dur, ps, v):
    for p in ps: nota(ch, t, dur, p, v)
def prog(ch, pr): add(ch, 0, mido.Message('program_change', channel=ch, program=pr))
def expr(ch, t0, t1, v0, v1, pasos=24):
    for i in range(pasos + 1):
        add(ch, tk(t0 + (t1 - t0) * i / pasos), mido.Message('control_change', channel=ch, control=11, value=int(v0 + (v1 - v0) * i / pasos)))

CBAJO, CHELO, CUERDA, TREMOLO, CORO, TROMPA, TROMBON, TUBA, TIMP, CAMPANA, PULSO, PERC = 0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 9
for ch, p in [(CBAJO, 43), (CHELO, 42), (CUERDA, 48), (TREMOLO, 44), (CORO, 52), (TROMPA, 60), (TROMBON, 57),
              (TUBA, 58), (TIMP, 47), (CAMPANA, 14), (PULSO, 45)]: prog(ch, p)
for ch in range(12): add(ch, 0, mido.Message('control_change', channel=ch, control=91, value=100))

MOTIVO = [(0.0, 0.75, 0), (0.75, 0.75, 3), (1.5, 0.5, 2), (2.0, 0.5, 0), (2.5, 0.75, -4), (3.25, 1.25, -5)]  # Re Fa Mi Re | Si♭ La
def motivo(ch, t0, raiz, v, escala=1.0, octava_abajo=None):
    for t, d, i in MOTIVO:
        nota(ch, t0 + t * escala, d * escala, raiz + i, v)
        if octava_abajo is not None: nota(octava_abajo, t0 + t * escala, d * escala, raiz + i - 12, v - 10)

# 0-1,5: sale del negro.
nota(CBAJO, 0.0, 4.5, 26, 70); expr(CBAJO, 0.0, 1.5, 20, 110)
nota(TUBA, 0.3, 4.2, 26, 36)
nota(CAMPANA, 0.6, 3.0, 50, 46)

# 1,5-4,5: el motivo abajo, chelos y contrabajos.
motivo(CHELO, 1.5, 38, 84, escala=0.8, octava_abajo=CBAJO)

# 4,5-7,2: pulso de cuerdas graves en Re; el motivo sube a las trompas con el coro grave; crece.
for i in range(10): nota(PULSO, 4.5 + i * 0.25, 0.2, 38, 55 + 3 * i)
for i in range(10): nota(PULSO, 5.5 + i * 0.17, 0.15, 38, 75 + 3 * i)
motivo(TROMPA, 4.6, 50, 78, escala=0.62)
acorde(CORO, 4.5, 2.6, [38, 45, 50], 58); expr(CORO, 4.5, 7.1, 60, 118)
nota(CBAJO, 4.5, 2.7, 26, 80)
for i in range(16): nota(TIMP, 6.2 + i * 0.06, 0.06, 38, 40 + 4 * i)

# 7,2: estalla el altar (golpe, el pulso sigue).
acorde(CUERDA, 7.2, 1.0, [34, 50, 53, 58, 62, 65], 118)
acorde(TROMBON, 7.2, 0.9, [34, 41, 46], 118); nota(TUBA, 7.2, 0.9, 22, 118)
nota(TIMP, 7.2, 0.5, 34, 127); nota(CAMPANA, 7.2, 1.6, 50, 100)
nota(PERC, 7.2, 1.2, 49, 115); nota(PERC, 7.2, 0.5, 35, 127)
for ch in (CUERDA, TROMBON, TUBA): expr(ch, 7.5, 8.2, 127, 50, pasos=10)

# 8,2-12: el motivo entero en trombones y coro, con pulso; segunda frase retorcida. Acento al girar (10,2).
for i in range(int(3.8 / 0.25)): nota(PULSO, 8.2 + i * 0.25, 0.2, 38 if i % 4 else 26 + 12, 82)
nota(CBAJO, 8.2, 3.8, 26, 88)
motivo(TROMBON, 8.2, 38, 96, escala=0.5, octava_abajo=TUBA)
motivo(CORO, 8.2, 50, 80, escala=0.5)
for t, d, p in [(10.2, 0.4, 55), (10.6, 0.4, 58), (11.0, 0.4, 57), (11.4, 0.6, 56)]:   # Sol Si♭ La Sol#
    nota(TROMPA, t, d, p, 100); nota(CORO, t, d, p, 88); nota(TROMBON, t, d, p - 12, 92)
acorde(TREMOLO, 10.2, 0.25, [50, 53, 56, 62], 120)            # acento seco al girar la cabeza
nota(PERC, 10.2, 0.3, 35, 110); nota(TIMP, 10.2, 0.3, 38, 115)

# 12-15,2: todo sube por escalones sobre La (la dominante); redoble y coro hacia arriba.
nota(CBAJO, 12.0, 3.2, 33, 100); nota(TUBA, 12.0, 3.2, 21, 96)
for k, (t, raiz) in enumerate([(12.0, 45), (12.8, 47), (13.6, 49), (14.4, 50)]):   # La, Si, Do#, Re: escalones
    acorde(TROMBON, t, 0.75, [raiz - 12, raiz - 5, raiz], 96 + 6 * k)
    acorde(CORO, t, 0.75, [raiz, raiz + 3 if k < 3 else raiz + 4, raiz + 7], 90 + 8 * k)
    nota(TROMPA, t, 0.75, raiz + 12, 92 + 8 * k)
acorde(TREMOLO, 12.0, 3.15, [57, 61, 64, 69], 70); expr(TREMOLO, 12.0, 15.1, 60, 127)
for i in range(int(3.1 / 0.06)): nota(TIMP, 12.0 + i * 0.06, 0.06, 33, 50 + 70 * i / 52)
for i in range(16): nota(PERC, 13.6 + i * 0.1, 0.12, 51, 40 + 5 * i)
for ch in range(12):
    if ch != PERC: add(ch, tk(15.12), mido.Message('control_change', channel=ch, control=123, value=0))

# 15,2: ¡CHAN! Re vacío en todo, y se apaga con el fundido a papel.
acorde(CUERDA, 15.2, 1.3, [26, 38, 45, 50, 57, 62, 69, 74], 127)
acorde(CORO, 15.2, 1.3, [38, 45, 50, 57], 124)
acorde(TROMBON, 15.2, 1.2, [26, 38, 45], 127); nota(TUBA, 15.2, 1.2, 14 + 12, 127)
acorde(TROMPA, 15.2, 1.2, [50, 57, 62], 127)
nota(TIMP, 15.2, 0.6, 38, 127); nota(TIMP, 15.2, 0.6, 26 + 12, 127)
nota(CAMPANA, 15.2, 1.3, 50, 120)
nota(PERC, 15.2, 1.3, 49, 127); nota(PERC, 15.2, 0.6, 35, 127); nota(PERC, 15.22, 0.6, 36, 120)
for ch in (CUERDA, CORO, TROMBON, TUBA, TROMPA, CAMPANA): expr(ch, 15.45, 16.45, 127, 0, pasos=20)

meta = mido.MidiTrack()
meta.append(mido.MetaMessage('set_tempo', tempo=1000000, time=0))
meta.append(mido.MetaMessage('end_of_track', time=tk(16.5)))
mid.tracks.append(meta)
for ch, l in ev.items():
    tr = mido.MidiTrack(); l.sort(key=lambda x: (x[0], x[1].type == 'note_on')); last = 0
    for t, m in l: tr.append(m.copy(time=t - last)); last = t
    mid.tracks.append(tr)
sal = sys.argv[1] if len(sys.argv) > 1 else 'deseo_mago_oscuro_guia_tema.mid'
mid.save(sal); print('ok', round(mid.length, 2), 's')
