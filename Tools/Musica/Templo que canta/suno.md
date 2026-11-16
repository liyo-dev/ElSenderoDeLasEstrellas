# El templo que canta: Cover en Suno (INC-1211)

Maqueta guía: `templo_que_canta_guia.mid` (124 compases, 243,6 s ≈ 4:04) hecha con `componer.py`,
pasada a audio con FluidSynth:

```
fluidsynth -F templo_que_canta_guia.mp3 -r 44100 <soundfont>.sf2 templo_que_canta_guia.mid
```

Estructura (la que manda en el nivel): 8 + 16 + 24 + 16 + 4 + 32 compases a **120** y el estribillo
de **24 a 132**. El bombo y el órgano marcan todos los tiempos con acento en el 1 para que Suno no se
invente el pulso: el nivel se corta a compás.

## Dos tomas, no una

Suno no cambia de tempo a mitad de una toma con fiabilidad, así que se piden **dos covers de la misma
guía**:

1. **`templo_canta_120`**: de la intro al final de la isla 2 (compases 1–100).
2. **`templo_canta_132`**: solo el estribillo (compases 101–124), con la misma guía recortada.

En el juego son dos clips: los tramos 0–5 usan el de 120 y el tramo 6 el de 132. Se empalman a compás
con un crossfade de 60 ms (o sin crossfade: el cambio de tempo ya es un corte).

## En Suno (toma 120)

1. Crear → Avanzado → **+ Audio → Subir** → `templo_que_canta_guia.mp3`.
2. Comprobar que el desplegable del audio dice **Cover** (a veces sale «Invertir»).
3. **Estilos**:

```
anime ritual temple chant, dark fantasy, low male choir chanting on vowels, giant stone organ pipes, taiko and frame drums, hand claps, tremolo strings, mystic and threatening, 120 BPM, 4/4, steady driving pulse, unhurried ritual groove, no EDM, no synthwave, no electronic drums, no fade out, hard ending
```

4. **Letra** (la burla del guardián: **una sola voz**, contando los pasos como una canción de corro macabra; versos con rima consonante):

```
[Intro: solo organo de piedra y tambor grave, hueco y lejano]

[Verse: entra una sola voz de hombre, chulesca y contenta]
Uno, dos, tres... ya te cansaste?
Cuatro, cinco, seis... ya tropezaste?
Siete, ocho, nueve... salta, salta, salta,
que al diez la lava te falta.

[Chorus]
La la la, la la la,
el que corre no descansa.
La la la, la la la,
el que para no avanza.

[Verse]
Pesa, pesa, pesa... pesa poco,
dos que miran y uno que esta loco.
Pesa, pesa, pesa... y el que se queda,
se lo come el pozo, que asi rueda.

[Verse]
De lado, de lado... se ve mejor el miedo,
de lado, de lado... y yo no tengo miedo.
Cuenta los faros, cuenta los que faltan,
cuenta los que van y los que se saltan.

[Chorus]
La la la, la la la,
el que corre no descansa.
La la la, la la la,
el que para no avanza.

[Outro]
Cantad conmigo... no, mejor no.
Canto yo solo... y me callo.
```

**Voz (decidido por Raúl el 16 nov):** **un solo cantante**, el guardián, chulesco y contento; canción
pegadiza y alegre, no coro de iglesia ni voz lírica. Lo que más se acercó fue el **arranque** de la
primera tanda (solo el órgano de piedra y el tambor, hueco y lejano): la voz entra después.
En Estilos: `solo male vocal`, `no choir, no harmonies, no gang vocals, no opera, no soprano,
no female vocals`. Género vocal: **Masculino**.

**Ojo con subir audio:** Suno avisa de que «coincide con una grabación existente» con cualquier
audio que se suba (es su filtro de copyright, no un reconocimiento real: la guía es sintetizada por
`render.py`). Cuando salta, se genera sin audio desde el prompt; se pierde el tempo exacto y se
corrige midiendo la toma (ver «Después»).

5. Título: `Templo que canta (cover de la guía, 120)`.

## Toma del estribillo

Misma guía recortada al compás 101 y el mismo prompt, cambiando el final por:

```
anime ritual temple chant, dark fantasy, triumphant and mocking, full male choir, giant stone organ pipes, taiko and frame drums, 132 BPM, 4/4, driving, no EDM, no fade out, stops dead
```

Letra:

```
[Final Chorus: todo el coro, a 132] Cantad conmigo… no, mejor no.
[Silence]
```

(Para el silencio final: si Suno se empeña en rellenar, se corta en el juego al acabar el tramo y ya
está; el guardián calla en el nivel, no hace falta que la toma lo haga.)

## Después

1. Elegir toma por sensación, no por fidelidad a la guía.
2. **Medir el BPM real** de la toma (contar compases contra el clic de la guía): si sale 119,3, se
   cambia `MusicPulse.bpm` del templo, no se estira la canción.
3. Al juego: `Assets/Audio/Music/Zonas/` (Streaming), regla en `AudioGraphProfile`, y se la das a
   `MusicPulse.musicSource` del tramo. Con pista, el pulso se lee de ella (`timeSamples`) y el clic
   de maqueta se apaga solo.
