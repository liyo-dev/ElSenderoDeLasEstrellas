# Pasa la maqueta guía del templo a audio sin FluidSynth: lee el .mid con mido y sintetiza cada
# canal con una onda sencilla (órgano, coro, cuerdas, timbal, metal, celesta y percusión).
# No es para que suene bonito: es para que Suno tenga una guía con el pulso, las secciones y el tempo.
#
# Uso:  python render.py [entrada.mid] [salida.wav]
import sys
import wave

import mido
import numpy as np

SR = 44100


def leer(ruta):
    mid = mido.MidiFile(ruta)
    ppq = mid.ticks_per_beat
    eventos = []
    tick = 0
    for msg in mido.merge_tracks(mid.tracks):
        tick += msg.time
        eventos.append((tick, msg))

    tempos = [(0, 500000)]
    for t, m in eventos:
        if m.type == 'set_tempo':
            tempos.append((t, m.tempo))
    tempos.sort()

    def segundos(tk):
        s = 0.0
        prev_t, prev_us = 0, tempos[0][1]
        for t, us in tempos:
            if t >= tk:
                break
            s += (t - prev_t) / ppq * prev_us / 1e6
            prev_t, prev_us = t, us
        return s + (tk - prev_t) / ppq * prev_us / 1e6

    notas = []          # (inicio, fin, nota, velocidad, canal)
    sonando = {}
    for t, m in eventos:
        if m.type == 'note_on' and m.velocity > 0:
            sonando[(m.channel, m.note)] = (t, m.velocity)
        elif m.type in ('note_off',) or (m.type == 'note_on' and m.velocity == 0):
            clave = (m.channel, m.note)
            if clave not in sonando:
                continue
            t0, vel = sonando.pop(clave)
            notas.append((segundos(t0), segundos(t), m.note, vel, m.channel))

    return notas, segundos(tick)


# Onda y envolvente de cada canal: lo justo para distinguirlos de oído.
def onda(canal, n, freq, dur):
    t = np.arange(n) / SR
    if canal == 0:                          # órgano de piedra
        return 0.5 * np.sin(2 * np.pi * freq * t) + 0.25 * np.sin(4 * np.pi * freq * t)
    if canal == 1:                          # coro (con vibrato)
        vib = 1 + 0.006 * np.sin(2 * np.pi * 5.2 * t)
        return np.sin(2 * np.pi * freq * t * vib) + 0.3 * np.sin(2 * np.pi * 2 * freq * t * vib)
    if canal == 2:                          # timbal: seno grave que cae
        return np.sin(2 * np.pi * freq * t) * np.exp(-6 * t)
    if canal == 3:                          # cuerdas
        return 0.6 * (2 * ((freq * t) % 1) - 1)
    if canal == 4:                          # metal
        return 0.4 * np.sign(np.sin(2 * np.pi * freq * t))
    if canal == 5:                          # celesta
        return np.sin(2 * np.pi * freq * t) + 0.4 * np.sin(2 * np.pi * 3 * freq * t)
    return np.sin(2 * np.pi * freq * t)


def envolvente(canal, n, dur):
    t = np.arange(n) / SR
    if canal == 2:
        return np.exp(-5 * t)
    if canal == 5:
        return np.exp(-4 * t)
    ataque = min(0.06, dur * 0.2)
    caida = max(0.0, dur - ataque)
    env = np.clip(t / max(ataque, 1e-4), 0, 1)
    if caida > 0:
        env *= np.clip((dur - t) / min(0.15, caida), 0, 1)
    return env


def golpe_percusion(freq):
    n = int(0.25 * SR)
    t = np.arange(n) / SR
    if freq < 45:
        return np.sin(2 * np.pi * 60 * t) * np.exp(-14 * t)
    ruido = np.random.default_rng(7).normal(0, 1, n)
    return ruido * np.exp(-28 * t)


def main():
    entrada = sys.argv[1] if len(sys.argv) > 1 else 'templo_que_canta_guia.mid'
    salida = sys.argv[2] if len(sys.argv) > 2 else 'templo_que_canta_guia.wav'

    notas, duracion = leer(entrada)
    total = int((duracion + 2.0) * SR)
    mezcla = np.zeros(total, dtype=np.float64)
    volumen = {0: 0.5, 1: 0.55, 2: 0.5, 3: 0.35, 4: 0.3, 5: 0.25, 9: 0.55}

    for inicio, fin, nota, vel, canal in notas:
        dur = max(0.05, min(fin - inicio, 6.0))
        i0 = int(inicio * SR)
        n = int(dur * SR)
        if i0 >= total or n <= 0:
            continue
        n = min(n, total - i0)
        freq = 440.0 * 2 ** ((nota - 69) / 12)
        if canal == 9:
            muestra = golpe_percusion(nota)
            n = min(n, len(muestra))
            muestra = muestra[:n]
        else:
            muestra = onda(canal, n, freq, dur) * envolvente(canal, n, dur)
        mezcla[i0:i0 + n] += muestra * (vel / 127.0) * volumen.get(canal, 0.3)

    pico = np.max(np.abs(mezcla)) or 1.0
    mezcla = mezcla / pico * 0.89
    estéreo = np.stack([mezcla, mezcla], axis=1)
    datos = (estéreo * 32767).astype('<i2')
    with wave.open(salida, 'wb') as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(datos.tobytes())
    print('ok', salida, round(len(mezcla) / SR, 1), 's', len(notas), 'notas')


if __name__ == '__main__':
    main()
