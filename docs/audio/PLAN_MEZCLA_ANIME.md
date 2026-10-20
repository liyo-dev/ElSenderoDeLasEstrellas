# Plan de mezcla "anime" — El Sendero de las Estrellas

> Documento de traspaso. Nace de una sesión de análisis (2026-10-20) sobre la calidad de
> audio del juego: voces, hechizos y cinemáticas. Contiene el diagnóstico medido, el
> análisis de la referencia (Slayers, Dragon Slave) y el plan de trabajo con tareas
> concretas. **No sustituye a `TDD.md`** (arquitectura) ni a `TRACKER.md`** (incidencias):
> cada cambio de este plan debe llevar su fila en `TRACKER.md` en el mismo commit.

---

## 0. En una línea

El problema no es la herramienta de audio: es la **mezcla** (sin limiter, sin buses, sin
loudness coherente) y el **diseño por capas** (un clip por evento, sin silencio, sin cola).
Krilloud no se usa. Se arregla la mezcla y se reconstruye el conjuro con estructura de
cinemática de anime.

---

## 1. Veredicto sobre Krilloud (cerrado el 2026-10-20)

**No se migra el audio del juego a Krilloud.** Motivos:

| Motivo | Detalle |
|---|---|
| Se salta el `AudioMixer` | Trae motor nativo propio (SoLoud) con mixer propio. Perderíamos los buses, el limiter y los snapshots, que es justo lo que falta. |
| No aporta sonidos | El problema es el material y la mezcla, no el disparador de eventos. |
| Madurez | Release única 1.0.0 (jun 2024); paquete Unity `com.krillaudio.krilloud` en `0.1.0`; FAQ declara pruebas en Unity 2019.4.11f1 y 2020.2.1f1; 6 estrellas, 8 commits, sin issues. |
| Plataforma | Windows x86/x64, macOS, iOS y Android **solo ARMv7** (sin arm64). Sin WebGL ni consolas. |
| Código | Usa `FindObjectOfType` (obsoleto en Unity 6) y arrastra `Newtonsoft.Json` net35 propio (riesgo de conflicto). |

Se puede reevaluar más adelante como *prototipo* tras una fachada `IAudioBackend`, pero no
antes de tener la mezcla bien. Apache 2.0, equipo español (Krill Audio, Málaga): la licencia
no es el problema.

---

## 2. Diagnóstico medido (2026-10-20)

Medido con `ffmpeg ebur128` (LUFS integrado y true peak) y con RMS en ventanas de 400 ms
(momento máximo) sobre los ficheros del proyecto.

### 2.1 Niveles actuales

| Grupo | LUFS mediana | Rango LUFS | True peak mediana |
|---|---:|---:|---:|
| Lanzamientos de hechizo (30) | −12,7 | −15,5 … −10,1 | −2,9 dBFS |
| Impactos de hechizo (12) | −19,1 | −20,9 … −14,5 | −2,9 dBFS |
| Voces es (255) | −17,7 | −30,8 … −10,4 | −1,9 dBFS |
| Reacciones (78) | −17,9 | **−47,9 … −15,2** | −2,1 dBFS |
| Música (48) | −14,3 | −20,3 … −10,4 | **hasta +0,9 dBFS** |

### 2.2 Hallazgos concretos

1. **El golpe suena más flojo que el lanzamiento.** Los hechizos tienen ~6 dB más de energía
   media; en su momento más fuerte (ventana 400 ms) el impacto queda **3,8 dB por debajo**
   (mediana −15,8 dBFS frente a −12,0). En acción, el impacto debe ser el momento más grande.
2. **Los hechizos no tienen graves.** Mediana de 0–20 % de la energía por debajo de 150 Hz; el
   sub está reservado… para nada. Sin cuerpo grave no hay peso.
3. **Los clips están normalizados a pico** (−3,0 / −1,5 dBFS) con crest factor 11–17 dB: se
   perdió el margen dinámico y la loudness no coincide con el pico.
4. **Voces con 20 dB de dispersión y reacciones con 33 dB.** `gasp_01` mide −47,9 LUFS:
   inaudible junto a un `hurt_01` de −15,6 LUFS.
5. **Las 333 voces están importadas con `forceToMono: 1` + `normalize: 1`.** El *normalize*
   de Unity (subopción de *Force To Mono*) las lleva todas a −1 dBFS de pico: aplasta la
   interpretación (un susurro y un grito al mismo nivel) y no es loudness.
6. **La música llega a +0,9 dBTP**: ya clipea antes de sumar nada.
7. **El `AudioMixer` solo tiene `Attenuation` en sus seis grupos** (Master, Music, UI,
   Ambience, SFX, Dialogue). No hay compresor, ni limiter, ni EQ, ni envío a reverb. El único
   snapshot es un "Snapshot" vacío.

### 2.3 Fallos de diseño en el código

| Dónde | Qué pasa | Consecuencia |
|---|---|---|
| `MagicProjectileSpawner.Co_SpawnAfterDelay` | Dispara `PlaySFX(castSFXKey)` **al empezar el gesto**. | El sonido del hechizo no está sincronizado con su salida. |
| `MagicProjectileSpawner.Co_SpawnWithCharge` | La carga no tiene audio propio. | En hechizos con `chargeTime` la carga se queda **muda** tras el primer golpe. |
| `AudioService.PlaySFX` / `AudioGraphProfile.EventSfx` | Un clip por clave (`eventKey` → `sfx`). | Sin capas, sin variación, sin cola independiente, sin pitch aleatorio. |
| `AudioService` (voz) | Reverb/eco como `AudioReverbFilter` por voz, solo en secuencias (`AplicarEfectoDeVoz`). | En juego la voz va seca; no hay cadena (EQ/compresión) ni envío a reverb. |
| `AudioGraphProfile.voiceDuck` / `cinematicVoiceDuck` | −12/−8/−5 dB y −8/−5/−3 dB. | La música se esconde siempre; en un conjuro debe **subir**. |

---

## 3. Referencia: *Slayers* — Dragon Slave (vídeo `M2zls8EEPds`, 64,5 s)

Analizado el audio (espectro, bandas por ventana de 250 ms, ebur128) y los fotogramas.
Mezcla de TV: RMS global −10,2 dBFS, pico 0,0 dBFS. **No se copia el clipping; se copia la
estructura.**

### 3.1 Línea de tiempo

| t | Qué pasa (sonido) |
|---|---|
| 0:05–0:18 | Diálogo seco, música baja. Nadie se tapa. |
| 0:18–0:22 | Últimas frases; la música empieza a moverse. |
| **0:22,2** | **Hueco de 0,25 s** (casi silencio). |
| **0:22,5** | **El "shing" de las muñequeras**: golpe brillante, ~83 % de la energía por encima de 2 kHz, **sin grave** (−32 dB de sub). Un destello, no un golpe. |
| 0:23–0:25 | Entran por capas: cuerpo (150–500 Hz, ~81 % de la energía) y brillo (2–8 kHz). Empieza la letanía. |
| 0:23–0:45 | 22 s de conjuro. La voz con cola larga; **la música no baja, crece con ella**. |
| 0:45–0:56 | La música se queda sola y sube; ya no hay diálogo. |
| **0:56,0** | **Respiro**: caída de ~8 dB (valle a −17,9 dBFS) durante ~1 s. |
| 0:57–1:00 | Vuelve a subir. |
| 1:00 | Grito final (el transcript marca "あ" en 60,04 s). |
| 1:00–1:04 | Impacto + muro de sonido. **El grave entra tarde y se queda** (sub a −16,4 dB en 61,5 s). |

### 3.2 Las cinco lecciones

1. **Contraste, no volumen.** Diálogo seco → hueco → brillo → respiro → muro. El juego actual
   está fuerte de forma continua, por eso nada destaca.
2. **El brillo va antes que el grave.** El artefacto suena brillante y sin sub; el sub se
   reserva para el impacto. En los hechizos del juego pasa al revés.
3. **La música no se duckea durante el conjuro.** En el diálogo sí; en la letanía, la música
   se queda y crece, y la voz es *un instrumento más*, no la protagonista (+8 dB de voz en
   cinemáticas no ayuda: despega la voz de la música).
4. **El silencio es un instrumento.** 0,25 s antes del "shing" y ~1 s antes del impacto. Gratis
   y es lo que más "anime" suena.
5. **La música tiene que estar escrita para el conjuro.** No es un loop de fondo: entra,
   crece, se queda sola y golpea. Con un loop de 2 min no hay mezcla que lo arregle.

---

## 4. La receta aplicada al prólogo (`Assets/_SEQUENCES/Guiones/Prologo_UltimaNoche.guion.txt`)

El guion **ya tiene el esqueleto correcto** (cántico por estrofas, coro, corte al blanco,
silencio y estruendo final). Lo que falta es la estructura de sonido.

| Punto del guion | Línea | Estado actual | Cambio propuesto |
|---|---:|---|---|
| `@ultimo`, arranque de la plegaria | 503–505 | `EfectoDeVozBeat preset=Ninguno`; `MezclaBeat fuente=plegaria musicaDb=-8 sfxDb=-4`; coro a 0,3 | **Preset de voz `Conjuro`** (reverb 2,5–3,5 s, delay 1/8, wet 20 %, transición de 1 s). Música **a 0 dB o subiendo**, no −8. **"Shing" de artefacto** (nuevo SFX) en el segundo 0, con hueco de 0,25 s antes. |
| Estrofas 01–05 | 506–558 | Truenos, `Prologo_Carga`, `AgujeroNegro_Carga`, coro | Mantener. Subir el coro a 0,5 y **quitarle ducking**; que la música crezca por capas. |
| "Silencio íntimo antes del estallido" | 560–565 | `PROLOGO_PLEGARIA_06` + `espera 0.35` | **Respiro real**: bajar música −10 dB, ambiente −16 dB y SFX −8 dB durante 0,8–1,2 s con ataque de 0,25 s. El `espera 0.35` no basta. |
| Preparación del grito | 567–574 | `MezclaBeat fuente=grito musicaDb=-10`; `Coro sonar=false`; `Prologo_Carga` | Subir la música **en vez de bajarla** en el último tramo: es el *build* final. |
| `@hechizo` | 575–583 | `PROLOGO_HECHIZO`; `Prologo_ProteccionAbsoluta`; `FusionDeHechizosBeat` (subida + explosión) | **Golpe musical** en el mismo frame que el grito; sub (40–70 Hz) que entra **tarde**; cola de 4–6 s que sobrevive al VFX. |
| `@explosion` | 595–608 | `WarClashStinger_B`; `silencio 1.2`; `SFX_Prologo_SilencioBlanco` | Cola larga (música + sub + escombros), y el silencio posterior más largo y "vivo" (silencio blanco filtrado). |

---

## 5. Plan por fases

### Fase 1 — Mezcla y loudness (1–2 días) · *máximo impacto audible*

- [ ] **Mixer**: master = EQ (HPF 30 Hz) → compresor (2:1, umbral −18 dB, ataque 30 ms, release 200 ms) → limiter (−1 dBTP).
- [ ] **Buses**: Dialogue = HPF 80 Hz → EQ (corta 300–500 Hz −2 dB, presencia +3 dB @ 3–5 kHz) → compresor (3:1, −20 dB, 10/120 ms) → de-esser → saturación suave → envío a reverb.
- [ ] **SFX** y **Ambience**: compresor 2:1 + envío a reverb; snapshot por espacio (exterior / interior / cueva / cinemática) con transición.
- [ ] **Normalización offline** (script, no a mano): voz −18 LUFS / −2 dBTP; reacciones −20; lanzamientos −18; impactos −14; música −20; ambiente −26.
- [ ] **Imports**: quitar `normalize` de las 333 voces (dejar `forceToMono`), y `loadType: Streaming` en música larga (evita los cortes de INC-168).
- [ ] **Rebalanceo**: lanzamientos −6 dB e impactos +4 dB respecto a hoy; revisar el resto de claves tras la normalización.
- [ ] **Informe** de loudness del proyecto (`Herramientas/Audio/medir_loudness.py`) para que cada clip nuevo entre al nivel correcto.

*Acceptación*: el impacto de un hechizo se oye claramente más fuerte que su lanzamiento; la voz se oye igual de fuerte en todas las líneas; nada satura al apilar SFX.

### Fase 2 — Eventos por capas y timing (3–5 días)

- [ ] `AudioEventSO` nuevo: capas (clip, retardo, ganancia dB, rango de pitch, probabilidad, paneo, espacial, bus), lista de **variaciones** con round-robin sin repetir, y bucle de carga opcional.
- [ ] `AudioService.PlayEvent(...)` con **ducking por evento** (qué buses baja y cuánto).
- [ ] `MagicSpellSO`: separar `carga`, `salida` e `impacto`; `chargeTime` → bucle de carga con fade; la salida suena **cuando sale el proyectil**, no al empezar el gesto.
- [ ] `MagicProjectileSpawner`: `Co_SpawnAfterDelay` deja de lanzar el SFX; `Co_SpawnWithCharge` entra bucle de carga y lo cierra al salir.
- [ ] `SequencePlayer`: `RespiroBeat` (silencio/duck antes del golpe) y `GolpeMusicalBeat` (la música golpea en un punto).
- [ ] Empezar por **5 hechizos**: Bola de Fuego, Chispa Ignea, Huracán, Nova de Luz, Meteoro.

*Acceptación*: un hechizo se oye como una frase (preparación → hueco → salida → impacto → cola), no como un clip suelto.

### Fase 3 — Voz anime (3–5 días)

- [ ] Preset de voz `Conjuro` en `EfectoDeVozBeat` (reverb largo + delay + wet, con transición gradual en vez de salto).
- [ ] Voz de hechizo: capa de **grito + susurro/coro**, cola de 1,5–3 s, y que la música **suba** mientras dura.
- [ ] Niveles de voz por tipo: diálogo (manda la voz) vs conjuro (la voz es una capa más).
- [ ] Revisar las 78 reacciones con más de 6 dB de dispersión (`gasp_01` a −47,9 LUFS).
- [ ] Regrabar/reescribir en clave las estrofas del cántico: la actuación, no la mezcla, es lo que más pesa.

*Acceptación*: la plegaria suena ritual y grande, no leída.

### Fase 4 — Contenido (continuo)

- [ ] **Música con sección de clímax**: 2–3 temas con entrada, crecimiento y golpe (o editar los existentes en stems). Sin esto, el conjuro no suena a anime.
- [ ] Los 29 hechizos con capas (sub, cuerpo, transitorio, aire, cola, sweetener).
- [ ] Bibliotecas de referencia: Sonniss GDC (gratuitas), Boom Library, Krotos. El material actual (`FREE SOUND PACK_TM`) es genérico y se nota.

---

## 6. Especificación técnica (la "receta Slayers" en números)

**Niveles objetivo (LUFS integrado / true peak)**

| Bus | Objetivo | Notas |
|---|---|---|
| Voz (diálogo) | −18 / −2 dBTP | Hablado, no cantado |
| Voz (reacciones) | −20 / −3 dBTP | Son acentos breves |
| SFX cuerpo (lanzamiento) | −18 / −2 dBTP | |
| SFX impacto (corto) | −14 / −1 dBTP | **Más fuerte que el lanzamiento** |
| SFX cola (3–6 s) | −24 … −28 | Se oye por debajo |
| Música | −20 / −1 dBTP | Deja margen al limiter |
| Ambiente | −26 | |
| Master | con limiter a −1 dBTP | |

**Capas de un impacto (orden de entrada)**
1. Transitorio 2–5 kHz, 10–30 ms.
2. Cuerpo 150–500 Hz, 100–300 ms.
3. Sub 40–80 Hz, entra **tarde** (−100…−200 ms) y crece.
4. Aire/escombros 500 Hz–8 kHz, en stereo, 0,3–1,5 s.
5. Cola de reverb 1,5–4 s, con el sub por debajo.

**Preset de voz `Conjuro`**
HPF 90 Hz · EQ: −2 dB @ 300 Hz, +3 dB @ 3–5 kHz · compresor 3:1 (−20 dB, 10/120 ms) ·
de-esser · saturación leve · envío a reverb (predelay 50 ms, decay 3 s, wet 20 %, HPF 250 Hz) ·
delay 1/8 con 15 % de mezcla · loudness −18 LUFS.

**Ducking**
- Diálogo normal: música −12 dB, ambiente −8 dB, SFX −5 dB (ataque 0,15 s, release 0,6 s, hold 0,25 s). Ya está en el perfil.
- **Conjuro: música −0 dB o +2 dB** (que crezca), ambiente −6 dB, SFX −3 dB.
- Respiro antes del golpe: música −10 dB, ambiente −16 dB, SFX −8 dB, 0,8–1,2 s, ataque 0,25 s.

---

## 7. Piloto (hecho el 2026-10-20)

Remezcla **fuera de Unity** del tramo final del prólogo (`Prologo_UltimaNoche`, de t=160 s
a t=222 s) con los audios y los tiempos reales del guion horneado. Está en
`Herramientas/Audio/Piloto/` (ver su `LEEME.md`) y se genera con
`python Herramientas/Audio/piloto_prologo.py`.

| | LUFS | Pico de la mezcla | Build 0:12→0:46 | Respiro | Golpe |
|---|---:|---:|---:|---:|---:|
| hoy (`antes.wav`) | −9,7 | **+5,2 dBFS** | ninguno | ninguno | −9,3 |
| receta (`despues_diseno.wav`) | −6,3 | +7,5 dBFS | **6,6 dB** | **12 dB de caída** | −8,9 |
| receta + master (`despues.wav`) | −14,0 | −1,0 dBFS | 6,6 dB | 11,7 dB | −16,7 |

Conclusiones:

1. **La cinemática de hoy clipea 5,2 dB** al final (no hay limiter en el mixer). Medido
   también con `ffmpeg ebur128`: −9,5 LUFS, pico de fichero +1,5 dBTP (saturado).
2. **El arco no existe hoy**: el nivel del cántico va del −19 al −13 dB sin dirección. La
   referencia tiene 8 dB de caída justo antes del impacto.
3. **La receta da el arco sin tocar los audios**: el build lo hacen la música y el coro
   creciendo, el respiro el ducking de 1 s, y el golpe el sub que entra tarde.
4. Sale además un **sonido nuevo**, `SFX_Artefacto_Shing.wav`: el destello brillante sin
   grave (el equivalente a las muñequeras de la referencia), listo para importar.
5. **El tema «El Ultimo Duelo» es un lecho constante** (RMS −18 dBFS de principio a fin,
   sin sección de clímax): el build del piloto sale de las capas, no de la música. Para el
   anime hace falta un tema con sección de clímax.

---

## 8. Herramientas

- `Herramientas/Audio/medir_loudness.py` — mide LUFS, true peak y RMS máximo de cada clip y
  genera el informe CSV + resumen por carpeta. Requiere `ffmpeg` en el PATH.
- `Herramientas/Audio/dsp_audio.py` — toolkit de audio (medición BS.1770, compresor,
  reverb Freeverb, EQ, limitador, reverberación, retardos, mezcla con paneo).
- `Herramientas/Audio/piloto_prologo.py` — construye las tres mezclas del piloto.
- `Herramientas/SFX/dsp.py` — toolkit de síntesis (env, filtros, reverberación, saturación)
  ya existente: sirve para fabricar las capas que faltan (sub, transitorio, "shing" de
  artefacto, cola).
- `Herramientas/SFX/disenar_hechizos.py`, `disenar_dbz3.py` — scripts de síntesis de los
  SFX existentes.

---

## 9. Estado y decisiones abiertas

**Hecho (2026-10-20)**
- Veredicto sobre Krilloud cerrado (no se migra).
- Diagnóstico medido completo (niveles, imports, mixer, código).
- Referencia Slayers analizada y convertida en receta con números.
- Mapeo de la receta sobre el guion del prólogo, línea a línea.
- **Piloto audible** del clímax del prólogo (`Herramientas/Audio/Piloto/`): tres mezclas
  comparables + el sonido nuevo del destello del artefacto.
- Herramientas de medición, mezcla y normalización en `Herramientas/Audio/`.
- **Código**: `AudioEventSO` (eventos por capas), `AudioService.PlayEvent`,
  `IniciarRespiro` y `GolpeMusical`, beats `RespiroBeat` y `GolpeMusicalBeat`, y la
  separación carga/salida/impacto en `MagicSpellSO` + `MagicProjectileSpawner` +
  `MagicProjectil` (INC-793 a INC-795).
- **Guion del prólogo**: el cántico pasa a `preset=Plegaria` y el grito a `preset=Epico`
  (vuelve el eco anime), la música sube en vez de duckearse, el coro a 0,45 y hay respiro y
  golpe musical antes del impacto.
- **Loudness**: normalizados 362 clips (voz −18 LUFS, reacciones −20, lanzamientos −18,
  impactos −14, ambiente −26) con copia de seguridad en
  `Herramientas/Audio/_backup_originales/`, más el tope de true peak a −1 dBTP en la música.
- **Imports**: menú *El Sendero ▸ Audio ▸ Ajustar imports de audio* (quita el *Normalize* de
  las voces y pone la música en Streaming) y *Informar de la mezcla* para ver el mixer.

**Pendiente**
- Rehornear el prólogo y escuchar el resultado.
- Aplicar la cadena del `AudioMixer` (limiter y compresores) en la ventana del Audio Mixer.
- Crear los primeros eventos por capas (5 hechizos) y rehacerlos con capas.
- Música con sección de clímax: sin eso el conjuro no suena a anime de verdad.
- Rehacer la misma receta en la cinemática final (`Final_01_ElSacrificio`), que ya usa
  `Epico`/`Plegaria` pero no tiene respiro ni golpe musical.

**Decisiones que debe tomar el autor**
- ¿Se acepta re-normalizar los 333 clips de voz (cambia el sonido de todas las líneas)?
- ¿Se busca o se compone música con sección de clímax?
- ¿Se aplican los valores de mezcla al resto de cinemáticas?

---

## 10. Reglas del repo que aplican a este plan

- Comentarios, documentación y mensajes de commit **en español**.
- Cada cambio de este plan añade su fila en **`TRACKER.md`** en el mismo commit.
- **Assets que Unity puede tener abiertos** (`.asset`, escenas, prefabs, el `AudioMixer`) se
  modifican **con un menú de Editor que use `AssetDatabase`**, nunca escribiendo su YAML.
- Nada de `FindObjectOfType`/`GetComponent` sin cachear en `Update`; nada de `new List<T>()`
  por frame; logs de diagnóstico solo bajo `#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION`.
- Lo que un cambio deja sin uso se quita en el mismo cambio (a `Versiones antiguas/`).
- `AudioService` es el sistema oficial de audio: no se crean sistemas paralelos de audio.
