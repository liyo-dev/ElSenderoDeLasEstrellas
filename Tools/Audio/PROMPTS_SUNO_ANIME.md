# Prompts de Suno para la música anime (INC-798)

La música que hay ahora («El Ultimo Duelo» y compañía) es un **lecho constante**: RMS −18 dBFS
de principio a fin, sin sección de clímax. Por eso el conjuro no suena a anime: el build del
piloto salía de las capas, no del tema. Esto es lo que hay que generar.

## Cómo se usa

**Desde la web de Suno** (recomendado para iterar y para usar *Extend*): pega el **Estilo**
en la caja de estilo y la **Estructura** en la de letra, marca *Instrumental* y dale a
generar. Para alargar o entrar en un punto concreto, usa *Extend* desde el segundo que te
interese.

**Desde el cliente del proyecto:**

```powershell
python Tools\Audio\suno_client.py generate --title "Cora del Cantico" `
  --tags "anime soundtrack, orchestral, dark ritual chant, low male choir, drone, ostinato, ominous, slow build" `
  --prompt-file Tools\Audio\prompts\cora_del_cantico.txt --instrumental --mv chirp-v4
```

`--tags` es la caja de estilo, `--prompt-file` la de letra (aquí, las marcas de sección) y
`--instrumental` evita que cante. El modelo `chirp-v4` es el que entiende las marcas de
sección; con `chirp-v3-5` suenan a menos.

## Las cinco piezas que hacen falta

El conjuro del prólogo dura **38 s** (de 171,1 s a 209,1 s de la secuencia) y el impacto cae
en 209,5 s. Encajar un clímax dentro de una pista de 2 minutos y medio al segundo 38 es
imposible: por eso la música se hace **por módulos**, no como una pista larga. Cada módulo
se coloca en su punto con `AmbienteBeat`/`MusicBeat`/`SfxBeat`.

### 1. Cora del cántico (el lecho ritual) — 38 s, en bucle

Se repite bajo las seis estrofas del cántico. Sin percusión, sin clímax: es el «colchón».

- **Estilo:** `anime soundtrack, orchestral, dark ritual chant, low male choir, drone, ostinato, minimal percussion, ominous, slow build, cinematic`
- **Estructura:**
  ```
  [Intro]
  [Instrumental]
  [Verse]
  [Instrumental]
  [Bridge]
  [Outro]
  ```
- **Dónde:** `Assets/Audio/Music/Cinematicas/Cora del Cantico.mp3`, como `ambiente CORO`
  (ya existe esa ranura en el guion: `SFX_Prologo_CoroBucle`).

### 2. Estrofa del cántico (el build) — 20–25 s

Entra sobre las dos últimas estrofas (PLEGARIA_05 y 06) y sube hasta el respiro.

- **Estilo:** `epic anime soundtrack, orchestral build, choir, taiko drums, strings crescendo, brass swell, rising intensity, key change, cinematic trailer`
- **Estructura:**
  ```
  [Instrumental]
  [Build]
  [Instrumental]
  [Rise]
  [Drop]
  ```
- **Truco:** las palabras `key change` y `rising intensity` en el estilo son las que lanzan el
  salto de tono y el clímax: es el recurso que más se nota de una banda sonora de anime.
- **Dónde:** como `ambiente` con fundido de 1 s al empezar el cántico.

### 3. El golpe (el impacto del hechizo) — 8–10 s

El golpe seco: sub, cuerda, coro y percusión. **El impacto tiene que estar en el segundo 0**
(o en el 1 si lo usas tras el respiro de 0,9 s).

- **Estilo:** `single cinematic impact, huge sub drop, brass hit, choir stab, taiko hit, reverb tail, dark fantasy, powerful`
- **Estructura:**
  ```
  [Impact]
  [Outro]
  ```
- **Dónde:** `SFX_Prologue_ExplosionFinal` ya hace este papel; esta versión sería la
  alternativa "con clímax" para el `GolpeMusicalBeat`.

### 4. Stinger del golpe musical (2–4 s)

Lo que suena en el punto exacto del impacto (`GolpeMusicalBeat`), con la música apartada.

- **Estilo:** `anime impact stinger, single orchestral hit, sharp brass, deep sub, short reverb tail, no build, 3 seconds`
- **Estructura:**
  ```
  [Impact]
  ```
- **Dónde:** como clave nueva en el `AudioGraphProfile` (`eventSfx`), para usarla en
  `efecto GolpeMusicalBeat eventKey=...`.

### 5. Tema del duelo con clímax (reemplazo de MAGOOSCURO_CLIMAX)

El tema del duelo entra en 149 s y tiene que sostener hasta la explosión (209 s): **60 s** con
clímax al final. Si la pista entera no sirve, genera una de 3–4 min y **extiéndela** desde el
minuto 2 para que el clímax caiga donde toca.

- **Estilo:** `epic anime battle soundtrack, orchestral, brass, taiko, choir, fast strings, heroic, building to a massive climax, key change, cinematic`
- **Estructura:**
  ```
  [Intro]
  [Verse]
  [Chorus]
  [Bridge]
  [Instrumental Break]
  [Drop]
  [Outro]
  ```

### (Opcional) Carga del hechizo (bucle de 4–6 s)

El bucle que suena mientras el hechizo se prepara (`chargeSFXKey`).

- **Estilo:** `magic charging loop, rising energy, shimmer, deep drone, no drums, seamless loop, fantasy`
- **Estructura:**
  ```
  [Instrumental]
  ```

## Reglas de estilo que hacen que suene a anime

| Sí | No |
|---|---|
| `key change` (el salto de tono del clímax) | `lo-fi`, `8-bit`, `synthwave`, `chiptune` |
| `taiko drums`, `shakuhachi`, `koto`, `biwa` | `electronic`, `drum and bass`, `EDM drop` (suenan a trailer de vídeo, no a anime) |
| `choir (ah)`, `epic choir`, `low male choir` | `female vocal pop`, `lyrics` (en un cántico no hay letra) |
| `building intensity`, `orchestral swell`, `hybrid orchestral` | `moody`, `lo-fi piano` (suena a fondo de LinkedIn) |
| `anime soundtrack`, `shounen`, `epic anime` | `video game music` (suele salir a chiptune) |

## Después de generar: la loudness

Suno entrega la música **muy alta** (sus pistas suelen medir −8 a −11 LUFS y con picos por
encima de 0 dBTP). Pásale el medidor y luego la normalización:

```powershell
python Herramientas\Audio\medir_loudness.py Assets\Audio\Music\Cinematicas
python Herramientas\Audio\normalizar_assets.py --aplicar --carpeta Assets\Audio\Music\Cinematicas --objetivo -20
```

El objetivo de la música es **−20 LUFS con tope de true peak a −1 dBTP**: es la que deja
margen al limiter del master para que el hechizo y los voces puedan subir sin saturar.

Y al meterla en el juego, toca **midirla en la mezcla**, no al aire: si el tema queda a −20
solo pero el coro suena por encima de la voz, hay que bajar el coro en el `AmbienteBeat`
(`volumen=0.3` en el guion del prólogo es justo el mando de ese nivel).
