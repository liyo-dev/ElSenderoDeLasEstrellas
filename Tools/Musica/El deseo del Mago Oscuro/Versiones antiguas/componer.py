# Maqueta guía de «El deseo del Mago Oscuro»: los 16,5 s del principio del prólogo (DeseoEnElSenderoBeat).
# Re menor, oscuro y lento: notas graves sostenidas, coro grave de hombres, campana. Su motivo (Re-Fa-Mi-Re,
# Si♭-La) lo dicen chelos y contrabajos. La estrella que nota a lo lejos es lo único en mayor: la celesta
# dice la primera frase de «La puerta de las estrellas» en Sol mayor, la tonalidad de la crecida del vuelo que
# viene justo después, así que el paso de una pista a otra no choca.
#
# Un tiempo = 1 s (60 por minuto), así los tiempos son segundos de la escena:
#   0-1,5   sale del negro: nota grave que aparece, una campana lejana
#   1,5-4,5 de espaldas contra el halo: el motivo abajo, en chelos y contrabajos
#   4,5-7,2 de frente, solo los ojos; 5,0 «Que ningún poder pueda destruirme»: entra el coro grave, crece
#   7,2     EL ESTALLIDO del altar (pico 8,1): acorde enorme, platillo, bombo, campana; se apaga hasta el 9
#   9-10,2  perfil: queda una nota grave inquieta (tritono)
#   10,2    gira la cabeza: CORTE SECO, silencio
#   10,6    la estrella aparece: celesta, Si-La-Sol-Re, arpa; 11,0 «¿Y ese poder?»
#   13,5-16,5 la estrella llena la pantalla: cuerdas altas y celesta en Sol mayor, muy suave, que suben
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
def cortar(ch, t): add(ch, tk(t), mido.Message('control_change', channel=ch, control=123, value=0))

CBAJO, CHELO, CUERDA, TREMOLO, CORO, TROMPA, TROMBON, TUBA, TIMP, CAMPANA, CEL, ARPA, ALTAS, GLOCK, PERC = 0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 12, 13, 14, 9
for ch, p in [(CBAJO, 43), (CHELO, 42), (CUERDA, 48), (TREMOLO, 44), (CORO, 52), (TROMPA, 60), (TROMBON, 57),
              (TUBA, 58), (TIMP, 47), (CAMPANA, 14), (CEL, 8), (ARPA, 46), (ALTAS, 49), (GLOCK, 9)]: prog(ch, p)
for ch in range(15): add(ch, 0, mido.Message('control_change', channel=ch, control=91, value=100))

# 0-1,5: sale del negro. Re grave que aparece y campana lejana.
nota(CBAJO, 0.0, 7.2, 26, 70); expr(CBAJO, 0.0, 1.5, 20, 110)
nota(TUBA, 0.3, 6.9, 26, 40)
nota(CAMPANA, 0.6, 3.0, 50, 46)

# 1,5-4,5: el motivo del mago, abajo (Re-Fa-Mi-Re | Si♭-La), chelos y contrabajos a la octava.
for t, d, p in [(1.5, 0.75, 38), (2.25, 0.75, 41), (3.0, 0.5, 40), (3.5, 0.5, 38), (4.0, 0.75, 34), (4.75, 1.5, 33)]:
    nota(CHELO, t, d, p, 84); nota(CBAJO, t, d, p - 12, 72)

# 4,5-7,2: de frente; entra el coro grave (Re menor) y todo crece hacia el estallido. Trémolo inquieto.
acorde(CORO, 4.5, 2.7, [38, 45, 50], 60); expr(CORO, 4.5, 7.15, 60, 120)
acorde(TREMOLO, 4.8, 2.4, [50, 53, 57], 50); expr(TREMOLO, 4.8, 7.15, 40, 125)
for t, d, p in [(5.5, 0.5, 50), (6.0, 0.5, 53), (6.5, 0.6, 56)]:   # la trompa sube (Re-Fa-La♭: se tensa)
    nota(TROMPA, t, d, p, 70 + 10 * (t - 5.5))
for i in range(20):   # redoble de timbal que crece
    nota(TIMP, 6.0 + i * 0.06, 0.06, 38, 30 + 4 * i)
for ch in (CORO, TREMOLO, TROMPA, CBAJO, TUBA, CHELO): cortar(ch, 7.12)

# 7,2: EL ESTALLIDO. Acorde enorme de Re menor con Si♭ debajo, platillo, bombo, campana. Se apaga hasta el 9.
acorde(CUERDA, 7.2, 2.0, [34, 50, 53, 57, 62, 65, 69], 120)
acorde(CORO, 7.2, 2.0, [46, 50, 53, 57], 120)
acorde(TROMBON, 7.2, 1.8, [34, 41, 46], 120); nota(TUBA, 7.2, 1.8, 22, 120)
acorde(TROMPA, 7.2, 1.8, [57, 62, 65], 118)
nota(TIMP, 7.2, 0.6, 34, 127); nota(CAMPANA, 7.2, 2.0, 50, 110)
nota(PERC, 7.2, 1.5, 49, 120); nota(PERC, 7.2, 0.5, 35, 127); nota(PERC, 7.25, 0.5, 35, 110)
for ch in (CUERDA, CORO, TROMBON, TUBA, TROMPA): expr(ch, 7.6, 9.0, 127, 20)

# 9-10,2: perfil. Queda una nota grave inquieta: Re y La♭ (tritono), muy baja.
acorde(TREMOLO, 9.0, 1.2, [38, 44], 46); expr(TREMOLO, 9.0, 10.15, 60, 90)
nota(CBAJO, 9.0, 1.2, 26, 54)
for ch in range(15): cortar(ch, 10.2)          # 10,2: gira la cabeza → silencio

# 10,6: la estrella. Celesta con la primera frase de «La puerta de las estrellas» en Sol mayor (Si-La-Sol-Re),
# arpa en armónicos. Deja sitio a «¿Y ese poder?» (11,0-13,8).
for t, d, p in [(10.6, 0.6, 83), (11.2, 0.4, 81), (11.6, 0.4, 79), (12.0, 1.2, 74)]:
    nota(CEL, t, d, p, 62); nota(GLOCK, t, d, p + 12, 26)
for i, p in enumerate([67, 71, 74, 79]): nota(ARPA, 10.6 + i * 0.12, 2.5, p, 40)
# 13,5-16,5: la estrella llena la pantalla. Cuerdas altas en Sol mayor que suben muy suaves; la celesta
# repite la frase más arriba y la deja abierta en Re (la crecida del vuelo empieza en Sol justo después).
acorde(ALTAS, 13.3, 3.2, [67, 71, 74, 79], 40); expr(ALTAS, 13.3, 15.8, 50, 110); expr(ALTAS, 15.8, 16.5, 110, 0)
for t, d, p in [(13.6, 0.5, 95), (14.1, 0.4, 93), (14.5, 0.4, 91), (14.9, 1.4, 86)]:
    nota(CEL, t, d, p, 56)
for i, p in enumerate([79, 83, 86, 91, 95, 98]): nota(GLOCK, 15.4 + i * 0.1, 1.0, p, 30 + 6 * i)

meta = mido.MidiTrack()
meta.append(mido.MetaMessage('set_tempo', tempo=1000000, time=0))
meta.append(mido.MetaMessage('end_of_track', time=tk(16.5)))
mid.tracks.append(meta)
for ch, l in ev.items():
    tr = mido.MidiTrack(); l.sort(key=lambda x: (x[0], x[1].type == 'note_on')); last = 0
    for t, m in l: tr.append(m.copy(time=t - last)); last = t
    mid.tracks.append(tr)
sal = sys.argv[1] if len(sys.argv) > 1 else 'deseo_mago_oscuro_guia.mid'
mid.save(sal); print('ok', round(mid.length, 2), 's')
