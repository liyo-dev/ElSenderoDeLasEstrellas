# Pega la crecida (guía o la toma de Suno) delante de «Amanecer en el Valle»: la crecida dura
# 12 compases (25,71 s a 112), con su respiro al final, y en ese instante entra el tema desde
# `desde` segundos.
# Uso: python montar.py crecida.wav amanecer.mp3 desde salida.mp3 [rompe] [fundido]
#   rompe    segundo de la crecida en el que entra el tema; por defecto, 12 compases (25,71 s).
#            Las tomas de Suno suelen alargar el respiro: se mide y se pasa a mano.
#   fundido  segundos de cruce entre la crecida y el tema (0 = corte seco). Por defecto, 0,6.
import sys, subprocess, numpy as np
crecida, amanecer, desde, salida = sys.argv[1], sys.argv[2], float(sys.argv[3]), sys.argv[4]
SR = 44100; ROMPE = float(sys.argv[5]) if len(sys.argv) > 5 else 12 * 4 * 60 / 112
FUNDIDO = float(sys.argv[6]) if len(sys.argv) > 6 else 0.6
def leer(ruta, ini=0.0, dur=None):
    cmd = ['ffmpeg', '-v', 'error', '-ss', str(ini)] + (['-t', str(dur)] if dur else []) + ['-i', ruta, '-ac', '2', '-ar', str(SR), '-f', 'f32le', '-']
    return np.frombuffer(subprocess.run(cmd, capture_output=True, check=True).stdout, dtype=np.float32).reshape(-1, 2).copy()
a = leer(crecida, 0, ROMPE); b = leer(amanecer, desde)
rms = lambda x: np.sqrt(np.mean(x ** 2) + 1e-12)
# La crecida acaba un poco por debajo del tema (−4 dB) para que al romper suba. Se mide en su
# tramo más fuerte, no en el final: algunas tomas alargan el respiro y hundirían la referencia.
trozo = int(5 * SR)
cima_ini = max(range(0, max(1, len(a) - trozo + 1), SR),
               key=lambda i: rms(a[i:i + trozo]))
a *= (rms(b[:int(4 * SR)]) * 10 ** (-4 / 20)) / max(rms(a[cima_ini:cima_ini + trozo]), 1e-6)
# Cruce: la cola de la crecida y el arranque del tema se solapan para que la entrada no dé un golpe.
xf = int(max(0.0, min(FUNDIDO, len(a) / SR * 0.5, len(b) / SR * 0.5)) * SR)
if xf > 0:
    t = np.linspace(0, 1, xf, dtype=np.float32)[:, None]
    x = np.concatenate([a[:-xf], a[-xf:] * (1 - t) + b[:xf] * t, b[xf:]])
else:
    fa = int(0.03 * SR); a[-fa:] *= np.linspace(1, 0, fa)[:, None]
    fb = int(0.03 * SR); b[:fb] *= np.linspace(0, 1, fb)[:, None]
    x = np.concatenate([a, b])
x /= max(1.0, np.abs(x).max() / 0.98)
subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', '2', '-i', '-', '-b:a', '192k', salida],
               input=x.astype(np.float32).tobytes(), check=True)
print('ok', salida, round(len(x) / SR, 1), 's; rompe en', round(ROMPE, 2), 's; cruce', FUNDIDO, 's')
