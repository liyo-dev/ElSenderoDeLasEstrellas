# Templo del Coro: Cover en Suno

Maqueta guía: `templo_del_coro_guia.mid` (104 compases + 2 de cola, 140 BPM, 4/4, Mi menor con el Fa
frigio; ≈ 3:04), hecha con `componer.py` (mido) y pasada a audio con FluidSynth
(`python3 ../renderizar.py`, o `fluidsynth -F templo_del_coro_guia.wav -r 44100 <soundfont>.sf2 templo_del_coro_guia.mid`).

**El MIDI es también la partitura del nivel**: `partitura.json` lista cada nota de cada voz que
dispara algo (compás, tiempo, segundo, sección). Si la toma de Suno se desvía de tempo o de comienzo,
se mide y se corrige la partitura; el MIDI manda.

| Voz (pista del MIDI) | En el nivel |
|---|---|
| BASE (taikos y timbal en 1 y 3) | Pasos del guardián: la sombra cabecea. No hace daño |
| CORO (notas cortas agudas) | Cae una losa por nota |
| ORGANO (golpes) | Columna de fuego por golpe |
| CUERDAS (carrerilla que llega al 1) | Proyectil; se devuelve con B en la nota de llegada |
| Parte C: RAYOS / FUEGO / OLEADA | Barridos de luz en el 2 y el 4 / fuego que late en cada tiempo / entra una oleada |

Secciones: afinación 1–8 · estrofa 1 9–24 · estrofa 2 25–40 · pre-estribillo 41–48 · estribillo 49–64 ·
parte C 65–88 · estribillo final 89–104 · golpe final en el 105.

## En Suno
1. Crear → Avanzado → **+ Audio → Subir** → `templo_del_coro_guia.mp3`.
2. Comprobar que el desplegable del audio dice **Cover** (a veces sale «Invertir»).
3. Influencia del audio 80 %, rareza 50 %, influencia del estilo 50 %. Duración: la de la guía (≈ 3:04).
4. Instrumental. **Estilos**:

```
epic dark fantasy chase, anime RPG temple escape, haunting temple choir chanting on vowels, huge cathedral pipe organ stabs, giant footsteps on taiko and timpani on beats 1 and 3, staccato string runs, heroic french horns, 140 BPM, 4/4, steady driving pulse, E minor phrygian, tense and thrilling, no EDM, no electronic drums, no synth, no fade out, hard ending
```

5. **Letra** (estructura):

```
[Instrumental]
[Intro: solo los tambores de los pasos; luego el coro llama tres notas, golpes de órgano y una carrerilla de cuerdas]
[Verse 1: entra la melodía de trompas; el coro responde con tres notas cortas cada dos compases]
[Verse 2: golpes de órgano en cada compás, caja en 2 y 4]
[Pre-Chorus: carrerillas de cuerdas que caen en el 1, redoble]
[Chorus: toda la orquesta y el coro cantan la melodía alta, órgano y metales]
[Bridge: oscuro y encerrado, órgano grave latiendo en cada tiempo, coro muy agudo en el 2 y el 4 como rayos, platillo y timbal fuertes cada seis compases]
[Final Chorus: todo junto, percusión a doble tiempo, más intenso]
[Outro: un golpe final enorme y un acorde largo de coro y órgano]
```

6. Título: `Templo del Coro (cover de la guia)`.
