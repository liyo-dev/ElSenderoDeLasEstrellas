# Maqueta guía de la crecida de la apertura del prólogo: la música del vuelo de cámara, que crece desde
# casi nada y ROMPE en «Amanecer en el Valle» cuando sale el logo, subiendo por la cascada.
#
# «Amanecer en el Valle» está en Sol mayor a ~112 pulsaciones y suena a tope desde el primer segundo,
# así que no tiene crecida propia. Esta crecida va en su tonalidad y a su tempo y acaba en Re (la
# dominante) para que la entrada del tema en Sol se sienta como una resolución, no como un corte.
#
# Un tiempo = 0,5357 s (112 por minuto), compás de 4 tiempos = 2,143 s. La pista empieza con el vuelo
# (VueloDeAperturaBeat, segundo 16,5 del guion):
#   compases 0-3   (0-8,6 s)   el mundo entero desde muy alto, casi quieto: cuerdas altas, arpa, celesta
#   compases 4-7   (8,6-17,1 s) baja y atraviesa el mar de nubes (~9-12 s): entra la flauta y el chelo
#   compases 8-11  (17,1-25,7 s) roza el mar hasta la cascada: todo crece, redoble de timbal sobre Re
#   25,7 s         medio tiempo de silencio (toma aire)
#   ~26 s          ROMPE: entra «Amanecer en el Valle» (lo pega montar.py), con el logo
import mido, sys
PPQ = 480
TIEMPO = 60 / 112
mid = mido.MidiFile(ticks_per_beat=PPQ)
ev = {}
def tk(bar, beat): return int(round((4 * bar + beat) * PPQ))
def add(ch, t, msg): ev.setdefault(ch, []).append((int(t), msg))
def nota(ch, bar, beat, dur, p, v):
    t0 = tk(bar, beat); t1 = int(round((4 * bar + beat + dur) * PPQ))
    add(ch, t0, mido.Message('note_on', channel=ch, note=p, velocity=max(1, min(127, int(v)))))
    add(ch, t1 - 5, mido.Message('note_off', channel=ch, note=p, velocity=0))
def acorde(ch, bar, beat, dur, ps, v):
    for p in ps: nota(ch, bar, beat, dur, p, v)
def prog(ch, pr): add(ch, 0, mido.Message('program_change', channel=ch, program=pr))
def expr(ch, b0, t0_, b1, t1_, v0, v1, pasos=32):
    a = tk(b0, t0_); b = tk(b1, t1_)
    for i in range(pasos + 1):
        add(ch, a + (b - a) * i / pasos, mido.Message('control_change', channel=ch, control=11, value=int(v0 + (v1 - v0) * i / pasos)))

PAD, CUERDA, ARPA, CEL, GLOCK, FLAUTA, CHELO, BAJO, CORO, TIMP, TROMPA, PERC = 0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 9
for ch, p in [(PAD, 89), (CUERDA, 49), (ARPA, 46), (CEL, 8), (GLOCK, 9), (FLAUTA, 73), (CHELO, 42),
              (BAJO, 43), (CORO, 52), (TIMP, 47), (TROMPA, 60)]: prog(ch, p)
for ch in range(12): add(ch, 0, mido.Message('control_change', channel=ch, control=91, value=95))

G = [55, 59, 62]; Em = [52, 55, 59]; C = [48, 52, 55]; D = [50, 54, 57]; Am = [45, 48, 52]
Dsus = [50, 55, 57]; Bm = [47, 50, 54]
def arpegio(bar, ch_, v, oct=12, n=8, paso=0.5):
    notas = ch_ + [ch_[0] + 12, ch_[1] + 12, ch_[2] + 12, ch_[0] + 24, ch_[2] + 12]
    for i in range(n): nota(ARPA, bar, i * paso, paso * 2.5, notas[i % len(notas)] + oct, v + (i % 4 == 0) * 6)

# ── 0-3: el mundo desde muy alto. Cuerdas altas muy suaves, arpa lenta, destellos de celesta. ──
for b, ch_ in enumerate([G, Em, C, D]):
    acorde(PAD, b, 0, 4, [x + 12 for x in ch_], 40 + 3 * b)
    acorde(CUERDA, b, 0, 4, [x + 12 for x in ch_], 26 + 3 * b)
    arpegio(b, ch_, 30 + 3 * b, oct=12, n=4, paso=1.0)
for b, bt, p in [(0, 1.5, 86), (0, 3, 83), (1, 2, 86), (2, 0.5, 91), (2, 3, 88), (3, 1, 86), (3, 3, 90)]:
    nota(CEL, b, bt, 1.5, p, 44)

# ── 4-7: baja y cruza las nubes. Destello de glockenspiel al entrar en la nube (compás 4), la flauta
# canta una frase que sube (todavía sin el tema), el chelo sostiene abajo. ──
for b, ch_ in enumerate([G, Em, C, D], start=4):
    acorde(PAD, b, 0, 4, [x + 12 for x in ch_], 54 + 3 * (b - 4))
    acorde(CUERDA, b, 0, 4, ch_ + [x + 12 for x in ch_], 38 + 5 * (b - 4))
    arpegio(b, ch_, 40 + 4 * (b - 4), oct=12, n=8, paso=0.5)
    nota(CHELO, b, 0, 4, ch_[0] - 12, 50 + 4 * (b - 4))
for i, p in enumerate([79, 81, 83, 86, 88, 91, 93, 95, 98]):  # destello subiendo al entrar en la nube
    nota(GLOCK, 4, 0.5 + i * 0.25, 1.0, p, 50 + 3 * i)
for b, bt, d, p in [(5, 0, 1, 74), (5, 1, 1, 76), (5, 2, 2, 79), (6, 0, 1.5, 81), (6, 1.5, 0.5, 79), (6, 2, 2, 76),
                    (7, 0, 1, 78), (7, 1, 1, 79), (7, 2, 2, 81)]:
    nota(FLAUTA, b, bt, d, p, 64)

# ── 8-11: roza el mar hacia la cascada. Todo crece: coro, cuerdas en tres octavas, trompa con la frase
# de la flauta más ancha, y sobre Re (compases 10-11) redoble de timbal y platillo que suben. ──
for b, ch_ in enumerate([C, Am, Dsus, D], start=8):
    acorde(CUERDA, b, 0, 4, [ch_[0] - 12] + ch_ + [x + 12 for x in ch_], 60 + 8 * (b - 8))
    acorde(CORO, b, 0, 4, [x + 12 for x in ch_], 50 + 9 * (b - 8))
    acorde(PAD, b, 0, 4, [x + 12 for x in ch_], 64 + 4 * (b - 8))
    arpegio(b, ch_, 56 + 6 * (b - 8), oct=12, n=16, paso=0.25)
    nota(BAJO, b, 0, 4, ch_[0] - 12, 58 + 8 * (b - 8))
    nota(CHELO, b, 0, 4, ch_[0] - 24 if ch_[0] - 24 >= 36 else ch_[0] - 12, 60 + 8 * (b - 8))
for b, bt, d, p in [(8, 0, 1, 76), (8, 1, 1, 79), (8, 2, 2, 81), (9, 0, 1.5, 84), (9, 1.5, 0.5, 83), (9, 2, 2, 81),
                    (10, 0, 1, 81), (10, 1, 1, 83), (10, 2, 2, 86), (11, 0, 3.5, 86)]:
    nota(TROMPA, b, bt, d, p - 12, 70 + 4 * (b - 8)); nota(FLAUTA, b, bt, d, p + 12, 66 + 4 * (b - 8))
# Redoble de timbal sobre Re y platillo suspendido que crecen hasta el silencio.
for i in range(int(2 * 4 / 0.125) - 4):
    v = 30 + 90 * i / (2 * 4 / 0.125)
    nota(TIMP, 10, i * 0.125, 0.12, 38, v)
    if i % 2 == 0: nota(PERC, 10, i * 0.125, 0.25, 51, v * 0.8)
nota(PERC, 11, 3.0, 0.5, 49, 110)   # el platillo de remate, justo antes de tomar aire
for ch in (CUERDA, CORO, PAD, BAJO, CHELO, TROMPA, FLAUTA, ARPA): expr(ch, 8, 0, 11, 3.4, 90, 127)
# Toma aire: todo se corta medio tiempo antes de que rompa el tema.
for ch in range(12):
    if ch != PERC: add(ch, tk(11, 3.45), mido.Message('control_change', channel=ch, control=123, value=0))

meta = mido.MidiTrack()
meta.append(mido.MetaMessage('set_tempo', tempo=int(TIEMPO * 1e6), time=0))
meta.append(mido.MetaMessage('end_of_track', time=tk(12, 0)))
mid.tracks.append(meta)
for ch, l in ev.items():
    tr = mido.MidiTrack(); l.sort(key=lambda x: (x[0], x[1].type == 'note_on')); last = 0
    for t, m in l: tr.append(m.copy(time=t - last)); last = t
    mid.tracks.append(tr)
sal = sys.argv[1] if len(sys.argv) > 1 else 'crecida_apertura_guia.mid'
mid.save(sal); print('ok', round(mid.length, 2), 's')
