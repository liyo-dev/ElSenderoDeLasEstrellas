# Pega la crecida (guía o la toma de Suno) delante de «Amanecer en el Valle»: la crecida dura 12 compases
# (25,71 s, con su medio tiempo de silencio al final) y en ese instante entra el tema desde `desde` segundos.
# Uso: python3 montar.py crecida.wav amanecer.mp3 desde salida.mp3
import sys, subprocess, numpy as np
crecida, amanecer, desde, salida = sys.argv[1], sys.argv[2], float(sys.argv[3]), sys.argv[4]
SR = 44100; ROMPE = 12 * 4 * 60 / 112
def leer(ruta, ini=0.0, dur=None):
    cmd = ['ffmpeg', '-v', 'error', '-ss', str(ini)] + (['-t', str(dur)] if dur else []) + ['-i', ruta, '-ac', '2', '-ar', str(SR), '-f', 'f32le', '-']
    return np.frombuffer(subprocess.run(cmd, capture_output=True, check=True).stdout, dtype=np.float32).reshape(-1, 2).copy()
a = leer(crecida, 0, ROMPE); b = leer(amanecer, desde)
rms = lambda x: np.sqrt(np.mean(x ** 2) + 1e-12)
# La crecida acaba un poco por debajo del tema (−4 dB) para que al romper suba.
cima = a[int((ROMPE - 5) * SR):int((ROMPE - 0.5) * SR)]
a *= (rms(b[:int(4 * SR)]) * 10 ** (-4 / 20)) / max(rms(cima), 1e-6)
f = int(0.03 * SR); b[:f] *= np.linspace(0, 1, f)[:, None]
x = np.concatenate([a, b]); x /= max(1.0, np.abs(x).max() / 0.98)
subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', '2', '-i', '-', '-b:a', '192k', salida],
               input=x.astype(np.float32).tobytes(), check=True)
print('ok', salida, round(len(x) / SR, 1), 's; rompe en', round(ROMPE, 2), 's')
