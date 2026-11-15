# Renderiza un MIDI a WAV con FluidSynth (sin tiempo real): avanza evento a evento y pide las muestras.
import sys, mido, numpy as np, fluidsynth, wave
mid = mido.MidiFile(sys.argv[1]); sal = sys.argv[2]
sr = 44100
fs = fluidsynth.Synth(samplerate=float(sr), gain=0.6)
sf = fs.sfload('/usr/share/sounds/sf2/TimGM6mb.sf2')
for ch in range(16): fs.program_select(ch, sf, 128 if ch == 9 else 0, 0)
fs.setting('synth.reverb.active', 1) if hasattr(fs, 'setting') else None
bloques = []
for msg in mid:  # mido ya da los tiempos en segundos
    n = int(round(msg.time * sr))
    if n > 0: bloques.append(np.array(fs.get_samples(n), dtype=np.int16))
    if msg.is_meta: continue
    if msg.type == 'note_on': fs.noteon(msg.channel, msg.note, msg.velocity)
    elif msg.type == 'note_off': fs.noteoff(msg.channel, msg.note)
    elif msg.type == 'program_change': fs.program_change(msg.channel, msg.program)
    elif msg.type == 'control_change': fs.cc(msg.channel, msg.control, msg.value)
bloques.append(np.array(fs.get_samples(int(sr * 2.0)), dtype=np.int16))
a = np.concatenate(bloques)
with wave.open(sal, 'wb') as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(sr); w.writeframes(a.tobytes())
print('ok', len(a) / 2 / sr)
