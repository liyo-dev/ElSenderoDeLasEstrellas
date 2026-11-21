# Encaja los peligros del Templo del Coro en la toma de Suno elegida.
# Mide los tiempos (pulso real de la toma, no una rejilla fija: Suno se mueve entre ~144 y ~146 BPM),
# los compases (el 1 cae en los golpes de entrada de cada parte) y los golpes que destacan por banda:
#   agudo (platillo) → losa · medio-brillante (metales) → proyectil · grave-medio largo (órgano) → columna de fuego.
# Escribe partitura.json y una pista de comprobación (la toma con un pitido distinto por peligro).
# Uso: python3 encajar.py toma.mp3
import sys, json, numpy as np, librosa, soundfile as sf

RUTA = sys.argv[1] if len(sys.argv) > 1 else 'toma.mp3'
y, sr = librosa.load(RUTA, sr=22050)
HOP = 128
FPS = sr / HOP
S = np.abs(librosa.stft(y, n_fft=2048, hop_length=HOP))
F = librosa.fft_frequencies(sr=sr, n_fft=2048)

def flujo(lo, hi):
    m = (F >= lo) & (F < hi)
    e = np.log1p(S[m].sum(0))
    return np.maximum(0, np.diff(e, prepend=e[0]))

def nivel(lo, hi):
    m = (F >= lo) & (F < hi)
    return np.log1p(S[m].sum(0))

grave, medio, agudo = flujo(30, 160), flujo(500, 3000), flujo(5000, 11000)
organo_nivel = nivel(60, 500)

# ─── Pulso ────────────────────────────────────────────────────────────────
oenv = librosa.onset.onset_strength(y=y, sr=sr, hop_length=HOP)
comb = oenv / oenv.max() + grave / grave.max()
_, b = librosa.beat.beat_track(onset_envelope=comb, sr=sr, hop_length=HOP, start_bpm=145.5, tightness=200)
tiempos = librosa.frames_to_time(b, sr=sr, hop_length=HOP)
FASE = 0  # el 1 de cada compás: tiempo con índice % 4 == FASE (comprobado en 21,5 s, 67,9 s y 119,0 s)

# ─── Secciones (de la toma, por energía y parecido armónico) ───────────────
SECCIONES = [
    ('afinacion', 0.0, 21.5),         # calentamiento de voz, golpe de metales y acorde largo
    ('estrofa1', 21.5, 44.5),          # «¡Visitas, visitas!»… «¡salta! ¡salta!»
    ('estrofa2', 44.5, 63.0),          # el órgano
    ('preestribillo', 63.0, 67.9),     # «un regalito… ¡tomad!»
    ('estribillo1', 67.9, 97.1),
    ('puente', 97.1, 113.0),           # encierro: coristas, rayos, fuego que late
    ('puente_triste', 113.0, 118.97),  # «Hace tanto que nadie se queda a escuchar…»: respiro
    ('estribillo_final', 118.97, 131.6),
    ('final', 131.6, 133.3),
]
def seccion(t):
    for n, a, z in SECCIONES:
        if a <= t < z: return n
    return 'final'

# ─── Golpes que destacan (respecto a lo que suena alrededor) ──────────────
def picos(sig, z_min, sep_s):
    med = np.convolve(sig, np.ones(int(FPS * 4)) / int(FPS * 4), mode='same')
    sd = np.sqrt(np.convolve((sig - med) ** 2, np.ones(int(FPS * 4)) / int(FPS * 4), mode='same')) + 1e-6
    z = (sig - med) / sd
    idx, _ = __import__('scipy.signal', fromlist=['find_peaks']).find_peaks(z, height=z_min, distance=int(FPS * sep_s))
    return idx / FPS, z[idx]

t_agudo, z_agudo = picos(agudo, 3.0, 0.35)
t_medio, z_medio = picos(medio, 3.0, 0.35)
t_organo, z_organo = picos(flujo(60, 500), 3.0, 0.35)

def sostiene(t, s=0.25):
    i = int(t * FPS); j = int((t + s) * FPS); k = max(0, i - int(0.15 * FPS))
    return organo_nivel[i:j].mean() - organo_nivel[k:i].mean() if j > i and i > k else 0

def compas_y_tiempo(t):
    i = int(np.argmin(np.abs(tiempos - t)))
    compas = (i - FASE) // 4 + 1
    tiempo = (i - FASE) % 4 + 1
    resto = (t - tiempos[i]) / (tiempos[min(i + 1, len(tiempos) - 1)] - tiempos[max(i - 1, 0)]) * 2
    return int(compas), round(tiempo + float(np.clip(resto, -0.5, 0.5)), 2)

ev = []
def poner(voz, t, extra=None):
    c, tt = compas_y_tiempo(t)
    e = {'voz': voz, 'segundo': round(float(t), 3), 'compas': c, 'tiempo': tt, 'seccion': seccion(t)}
    if extra: e.update(extra)
    ev.append(e)

# BASE: los pasos del guardián, cada tiempo desde que entra el pulso estable.
for t in tiempos:
    if 21.4 <= t < 131.6: poner('BASE', t)

# Lo que se coloca: los golpes más fuertes de cada voz en cada sección, con un tope y una separación
# mínima, para que el nivel se lea (no cada pico que suena). Diseño por sección:
#   (voz, picos de, sección, cuántos como mucho, separación mínima en s)
PLAN = [
    ('PRESENTACION', 'medio', 'afinacion', 6, 1.5),
    ('LOSA', 'agudo', 'estrofa1', 14, 0.8),
    ('COLUMNA', 'organo', 'estrofa2', 10, 1.5),
    ('LOSA', 'agudo', 'estrofa2', 4, 3.0),
    ('PROYECTIL', 'medio', 'preestribillo', 3, 0.9),
    ('LOSA', 'agudo', 'estribillo1', 10, 1.2),
    ('COLUMNA', 'organo', 'estribillo1', 8, 2.0),
    ('PROYECTIL', 'medio', 'estribillo1', 3, 6.0),
    ('LOSA', 'agudo', 'estribillo_final', 8, 1.0),
    ('COLUMNA', 'organo', 'estribillo_final', 6, 1.5),
    ('PROYECTIL', 'medio', 'estribillo_final', 2, 4.0),
]
PICOS = {'agudo': (t_agudo, z_agudo), 'medio': (t_medio, z_medio), 'organo': (t_organo, z_organo)}
for voz, banda, sec, tope, sep in PLAN:
    ts, zs = PICOS[banda]
    cand = sorted([(z, t) for t, z in zip(ts, zs) if seccion(t) == sec
                   and (banda != 'organo' or sostiene(t) > 0.15)], reverse=True)
    elegidos = []
    for z, t in cand:
        ocupado = any(abs(t - u) < sep for u in elegidos) or any(abs(t - e['segundo']) < 0.2 for e in ev if e['voz'] not in ('BASE',))
        if not ocupado: elegidos.append(t); poner(voz, t, {'fuerza': round(float(z), 1)})
        if len(elegidos) >= tope: break

# Puente: fuego que late en cada tiempo, rayos en el 2 y el 4, oleadas en los golpes graves más fuertes.
for i, t in enumerate(tiempos):
    if seccion(t) == 'puente':
        poner('FUEGO', t)
        if (i - FASE) % 2 == 1: poner('RAYO', t)
g_t, g_z = picos(grave, 2.5, 6 * 60 / 145.5)
olas = [t for t, z in sorted(zip(g_t, g_z), key=lambda x: -x[1]) if seccion(t) == 'puente'][:3]
for n, t in enumerate(sorted(olas)): poner('OLEADA', t, {'oleada': n + 1})

# Final: el golpe más fuerte de los últimos segundos.
cola = [(t, z) for t, z in zip(t_medio, z_medio) if t >= 129]
if cola: poner('FIN', max(cola, key=lambda x: x[1])[0], {'nota': 'el suelo cede; salto al vacío y vuelve el vuelo'})

ev.sort(key=lambda e: (e['segundo'], e['voz']))
dur = len(y) / sr
bpm = 60 / np.median(np.diff(tiempos[(tiempos > 21.4) & (tiempos < 131.6)]))
with open('partitura.json', 'w', encoding='utf-8') as f:
    json.dump({'toma': 'La función del guardián (Templo del Coro) — toma elegida por Raúl, 20 nov 2026',
               'duracion_s': round(dur, 3), 'bpm_medio': round(float(bpm), 2),
               'nota': 'El tempo de Suno se mueve un poco (≈144–146): los segundos de cada evento mandan, no una rejilla fija.',
               'tiempos_s': [round(float(t), 3) for t in tiempos],
               'primer_tiempo_de_compas': FASE,
               'secciones': [{'nombre': n, 'desde_s': a, 'hasta_s': z} for n, a, z in SECCIONES],
               'eventos': ev}, f, ensure_ascii=False, indent=1)

# ─── Pista de comprobación ────────────────────────────────────────────────
y44, sr44 = librosa.load(RUTA, sr=44100, mono=False)
if y44.ndim == 1: y44 = np.vstack([y44, y44])
mezcla = y44 * 0.6
TONOS = {'PRESENTACION': 660, 'LOSA': 1320, 'COLUMNA': 220, 'PROYECTIL': 990, 'RAYO': 1760,
         'OLEADA': 110, 'FIN': 440}
for e in ev:
    if e['voz'] not in TONOS: continue
    f0 = TONOS[e['voz']]; n = int(0.09 * sr44); t = np.arange(n) / sr44
    bip = 0.35 * np.sin(2 * np.pi * f0 * t) * np.exp(-t * 30)
    i = int(e['segundo'] * sr44)
    if i + n < mezcla.shape[1]: mezcla[:, i:i + n] += bip
sf.write('comprobacion.wav', mezcla.T, sr44)

from collections import Counter
print('BPM medio', round(float(bpm), 2), '·', len(ev), 'eventos ·', dict(Counter(e['voz'] for e in ev)))
for v in ('PRESENTACION', 'LOSA', 'COLUMNA', 'PROYECTIL', 'OLEADA', 'FIN'):
    print(v, [e['segundo'] for e in ev if e['voz'] == v][:40])
