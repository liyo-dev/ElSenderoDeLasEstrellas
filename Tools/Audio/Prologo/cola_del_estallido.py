# Cola de reverberación sintética: el clip original intacto + su eco que se apaga despacio (sin estirar ni repetir).
import sys, wave, numpy as np
src, dst = sys.argv[1], sys.argv[2]
w = wave.open(src); sr = w.getframerate(); ch = w.getnchannels()
x = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).reshape(-1, ch).astype(np.float32) / 32768
cola = 4.5
rng = np.random.default_rng(7)
n_ir = int(cola * sr); t = np.arange(n_ir) / sr
y = np.zeros((len(x) + n_ir, ch), np.float32)
for c in range(ch):
    ir = rng.standard_normal(n_ir).astype(np.float32) * np.exp(-t / 1.1)   # RT60 ≈ 7,6 s · 1,1 → caída suave
    # más grave cuanto más tarde: pasa-bajos de un polo con corte que baja
    out = np.empty_like(ir); a = 0.0
    for i in range(n_ir):
        k = 0.35 * np.exp(-t[i] / 1.5) + 0.04
        a += k * (ir[i] - a); out[i] = a
    ir = out; ir[: int(0.03 * sr)] = 0  # sin la parte directa
    N = 1 << int(np.ceil(np.log2(len(x) + n_ir)))
    wet = np.fft.irfft(np.fft.rfft(x[:, c], N) * np.fft.rfft(ir, N), N)[: len(x) + n_ir]
    y[:, c] = wet
# el eco solo se hace notar donde el seco ya cae: ganancia que entra a partir de los 6,5 s
dry = np.zeros_like(y); dry[: len(x)] = x
tt = np.arange(len(y)) / sr
mix = np.clip((tt - 7.6) / 1.4, 0, 1)[:, None]
nivel_seco_final = np.sqrt(np.mean(x[int(6*sr):int(7*sr)] ** 2))
g = nivel_seco_final * 10 ** (-5 / 20) / (np.sqrt(np.mean(y[int(9.0*sr):int(9.5*sr)] ** 2)) + 1e-9)
z = dry + y * mix * g
# fundido final para que acabe en silencio
fin = int(1.5 * sr); z[-fin:] *= np.linspace(1, 0, fin)[:, None] ** 2
z = np.clip(z, -1, 1)
o = wave.open(dst, 'wb'); o.setnchannels(ch); o.setsampwidth(2); o.setframerate(sr)
o.writeframes((z * 32767).astype(np.int16).tobytes()); o.close()
print(len(z) / sr)
