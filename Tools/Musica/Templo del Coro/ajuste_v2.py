# Segunda pasada de la partitura del Templo del Coro, con lo que Raúl oyó en la comprobación (20 nov):
#  - 0–21,5 s: los pitidos no iban con nada → solo los golpes claros del calentamiento (saltos de volumen > 10 dB).
#  - 21,5–31 s: el contratiempo le gusta, pero cambiaba de fase → contratiempo fijo cada dos tiempos.
#  - 31–97 s: los pitidos iban «antes y fuera de la música» (cogían la «s» de «pasad»/«salta», no el PUM)
#    → se colocan en los PUM: golpes de banda ancha con salto de volumen, en la rejilla de medios tiempos.
#  - 1:37 al final: «perfecto» → se deja igual que en la primera pasada.
# Uso: python3 ajuste_v2.py (desde Tools/Musica/Templo del Coro, con la toma y la primera pasada en su sitio)
import json, numpy as np, librosa, scipy.signal as ss
y, sr = librosa.load('La funcion del guardian (Templo del Coro).mp3', sr=22050)
HOP = 128; FPS = sr / HOP
PRIMERA = 'Versiones antiguas/Partitura primera pasada (2026-11-20)/partitura.json'
v1 = json.load(open(PRIMERA, encoding='utf-8'))
bt = np.array(v1['tiempos_s']); FASE = v1['primer_tiempo_de_compas']
on = librosa.onset.onset_strength(y=y, sr=sr, hop_length=HOP)
S = np.abs(librosa.stft(y, n_fft=2048, hop_length=HOP)); F = librosa.fft_frequencies(sr=sr, n_fft=2048)
m = (F >= 40) & (F < 400); e = np.log1p(S[m].sum(0)); grave = np.maximum(0, np.diff(e, prepend=e[0]))
rmsdb = librosa.amplitude_to_db(librosa.feature.rms(y=y, hop_length=HOP)[0])
def salto(t): i = int(t * FPS); return rmsdb[min(len(rmsdb) - 1, i + int(0.06 * FPS))] - rmsdb[max(0, i - int(0.06 * FPS))]
def ataque(t): i = int(t * FPS); return on[max(0, i - 4):i + 5].max() + grave[max(0, i - 4):i + 5].max() * on.max() / grave.max()

SECC = v1['secciones']
def seccion(t):
    for s in SECC:
        if s['desde_s'] <= t < s['hasta_s']: return s['nombre']
    return 'final'
def compas_y_tiempo(t):
    i = int(np.argmin(np.abs(bt - t))); c = (i - FASE) // 4 + 1; tt = (i - FASE) % 4 + 1
    if abs(bt[i] - t) > 0.05: tt += 0.5 if t > bt[i] else -0.5
    return int(c), tt
ev = [e for e in v1['eventos'] if e['voz'] == 'BASE' or e['segundo'] >= 97.0]
def poner(voz, t, extra=None):
    c, tt = compas_y_tiempo(t)
    d = {'voz': voz, 'segundo': round(float(t), 3), 'compas': c, 'tiempo': tt, 'seccion': seccion(t)}
    if extra: d.update(extra)
    ev.append(d)

# 1) Afinación: golpes claros del calentamiento.
pk, _ = ss.find_peaks(on, distance=int(0.15 * FPS))
for i in pk:
    t = i / FPS
    if t < 21.4 and salto(t) > 10: poner('PRESENTACION', t, {'salto_db': round(float(salto(t)), 1)})

# 2) 21,5–31 s: contratiempo fijo cada dos tiempos (fase del primero que le gustó, 22,52 s).
i0 = int(np.argmin(np.abs(bt - 22.37)))
for i in range(i0, len(bt) - 1, 2):
    t = (bt[i] + bt[i + 1]) / 2
    if t >= 31.0: break
    poner('LOSA', t, {'patron': 'contratiempo'})

# 3) 31–97 s: los PUM, en la rejilla de medios tiempos.
media = np.sort(np.concatenate([bt, (bt[:-1] + bt[1:]) / 2]))
A = np.array([ataque(t) for t in media]); J = np.array([salto(t) for t in media])
def norm(t):
    w = (media >= t - 8) & (media < t + 8); return np.median(A[w])
PLAN = [  # sección, voz, separación mínima (s), umbral de puntuación
    ('estrofa1', 'LOSA', 0.8, 16), ('estrofa2', 'COLUMNA', 0.8, 16),
    ('preestribillo', 'PROYECTIL', 0.8, 4), ('estribillo1', None, 0.6, 14),
]
for sec, voz, sep, umbral in PLAN:
    cand = []
    for t, a, j in zip(media, A, J):
        if seccion(t) != sec or t < 31.0 or t >= 97.0: continue
        p = (a / norm(t)) * max(j, 0)
        if p >= umbral: cand.append((p, t, a / norm(t), j))
    cand.sort(reverse=True); hechos = []
    for p, t, an, j in cand:
        if any(abs(t - u) < sep for u in hechos): continue
        hechos.append(t)
        v = voz
        if v is None:  # estribillo: losa en los PUM más fuertes, columna en los graves largos, proyectil cada ~6 s
            v = 'LOSA' if an >= 2.2 else 'COLUMNA'
        poner(v, t, {'puntuacion': round(float(p), 1)})
# 1:13: los tres seguidos de la primera pasada le encantaron → se conservan tal cual.
ev = [d for d in ev if not (72.5 <= d['segundo'] < 73.8 and d['voz'] != 'BASE')]
ev += [d for d in v1['eventos'] if 72.5 <= d['segundo'] < 73.8 and d['voz'] != 'BASE']
# En el estribillo, un proyectil cada ~6 s sobre los PUM de más salto.
pro = []
for d in sorted([e for e in ev if e['seccion'] == 'estribillo1' and e['segundo'] < 97], key=lambda e: -e.get('puntuacion', 0)):
    if 72.5 <= d['segundo'] < 73.8: continue
    if all(abs(d['segundo'] - u) >= 6 for u in pro): pro.append(d['segundo']); d['voz'] = 'PROYECTIL'
    if len(pro) >= 3: break

ev = [d for d in ev if not (73.8 <= d['segundo'] < 74.3 and d['voz'] != 'BASE')]  # aire tras los tres seguidos
ev.sort(key=lambda e: (e['segundo'], e['voz']))
v1['eventos'] = ev
v1['nota'] = v1['nota'] + ' Segunda pasada (20 nov) con la comprobación de oído de Raúl: ver ajuste_v2.py.'
json.dump(v1, open('partitura.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

# Comprobación
y44, sr44 = librosa.load('La funcion del guardian (Templo del Coro).mp3', sr=44100, mono=False)
mezcla = y44 * 0.6
TONOS = {'PRESENTACION': 660, 'LOSA': 1320, 'COLUMNA': 220, 'PROYECTIL': 990, 'RAYO': 1760, 'OLEADA': 110, 'FIN': 440}
for d in ev:
    if d['voz'] not in TONOS: continue
    f0 = TONOS[d['voz']]; n = int(0.09 * sr44); tt = np.arange(n) / sr44
    bip = 0.35 * np.sin(2 * np.pi * f0 * tt) * np.exp(-tt * 30); i = int(d['segundo'] * sr44)
    if i + n < mezcla.shape[1]: mezcla[:, i:i + n] += bip
import soundfile as sf; sf.write('comprobacion.wav', mezcla.T, sr44)
from collections import Counter
print(dict(Counter(d['voz'] for d in ev)))
for v in ('PRESENTACION', 'LOSA', 'COLUMNA', 'PROYECTIL'):
    print(v, [d['segundo'] for d in ev if d['voz'] == v and d['segundo'] < 97])
