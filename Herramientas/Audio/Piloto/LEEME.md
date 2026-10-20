# Piloto: el clímax del prólogo con la receta Slayers

Remezcla de prueba del tramo final del prólogo (`Prologo_UltimaNoche`, de t=160 s a
t=222 s de la secuencia) hecha **fuera de Unity**, con los audios y los tiempos reales
del juego (los del guion horneado). Sirve para decidir la mezcla antes de tocar
código.

## Ficheros

| Fichero | Qué es |
|---|---|
| `antes.wav` | La mezcla de **hoy**: voz seca (el prólogo iba con `preset=Ninguno`, sin eco), música duckeada −8 dB durante la plegaria, coro a 0,3, sin respiro, sin capas, sin limiter. |
| `despues_diseno.wav` | La **receta Slayers** aplicada, pero con el bus de hoy (sin limiter): aísla el cambio de diseño. |
| `despues.wav` | La receta + el **master nuevo**: −14 LUFS con limiter a −1 dBTP. |
| `SFX_Artefacto_Shing.wav` | Sonido nuevo: el "destello" del artefacto (brillante, **sin grave**), como el de las muñequeras de la referencia. Listo para importar. |

Se generan con `python Herramientas/Audio/piloto_prologo.py` (necesita `ffmpeg`).

## La voz del conjuro (esto es lo que no hay que perder)

La voz del cántico usa la **cadena real del juego**: el preset `Plegaria` (reverb de 3,2 s +
eco slap de 260 ms) y, en el grito final, el preset `Epico` (reverb de 6,5 s + eco de 340 ms).
Son los mismos números que aplica `AudioService.AplicarEfectoDeVoz` en Unity: el eco slap
(`AudioEchoFilter`) es lo que hace que una voz gritada suene a anime, y una reverb genérica no
sustituye ese sonido. En `antes.wav` la voz va seca (así estaba el prólogo) y en las versiones
nuevas vuelve el eco, más la música que crece por debajo.

## Qué escuchar (tiempos del WAV)

| t | Qué pasa |
|---|---|
| 0:03 | Habla el Mago Oscuro: diálogo seco, música baja (como en la referencia). |
| 0:10,5 | **El destello del artefacto**: hueco de 0,25 s y un golpe brillante sin grave. Solo en las versiones nuevas. |
| 0:11 | Empieza el cántico (`PROLOGO_PLEGARIA_01`). La voz entra con reverb largo y delay; **la música no se duckea**, crece. |
| 0:11–0:47 | Las seis estrofas. El coro sube de −14 dB a −4 dB mientras la música sube 2 dB. |
| 0:49,2 | **El respiro**: la música baja 10 dB, el coro 16 dB y los SFX 8 dB durante 1 s. Solo en las versiones nuevas. |
| 0:50,3 | **El grito** (`PROLOGO_HECHIZO`) + golpe musical + caída de sub (72→34 Hz) que entra tarde. |
| 0:51,4 | **El impacto**: capas de transitorio, cuerpo, sub y cola de 5 s. |
| 0:52–0:57 | La cola: el sub sigue creciendo mientras la música se apaga. |
| 0:57,5 | Silencio y «silencio blanco». |

## Números (medidos, no de oído)

| | LUFS | Pico de la mezcla | True peak del fichero | Build 0:12→0:46 | Respiro | Golpe |
|---|---:|---:|---:|---:|---:|---:|
| `antes.wav` | −9,5 | **+6,5 dBFS** | +1,5 dBTP (satura) | −15,6 → −14,1 (nada) | ninguno | −8,4 |
| `despues_diseno.wav` | −6,9 | +7,4 dBFS | +1,7 dBTP (satura) | −11,9 → −6,1 (**5,8 dB**) | −16,9 (**10,8 dB de caída**) | −9,0 |
| `despues.wav` | −14,0 | −1,0 dBFS | −0,8 dBTP | −19,0 → −13,3 (**5,7 dB**) | −24,1 (**10,8 dB**) | −16,1 |

(El LUFS se ha medido también con `ffmpeg ebur128`: −9,5 / −6,9 / −14,0. Coincide con la
implementación propia en ±0,2 dB. El «pico de la mezcla» es lo que sale de sumar los buses;
el WAV se escribe recortado a 0 dBFS, así que en `antes.wav` y `despues_diseno.wav` se
oye el recorte.)

Lo que dice la tabla:

1. **La mezcla de hoy clipea 6,5 dB.** No hay limiter en el `AudioMixer`: todo lo que
   suma la cinemática satura el digital. Es la mitad de por qué suena «amateur».
2. **Hoy no hay arco.** El nivel va del −19 al −13 sin dirección: no hay build, no hay
   respiro, no hay contraste. La referencia de Slayers tiene 8 dB de caída justo antes
   del impacto.
3. **Con la receta, el arco aparece** sin tocar los audios: el build lo hacen la música
   y el coro creciendo, el respiro lo hace el ducking de 1 s, y el golpe lo hace el sub
   que entra tarde.

## Qué NO se arregla aquí

- El **mixer de Unity** (limiter, compresores por bus, envíos a reverb): esto es la
  fase 1 del plan y hay que hacerlo en el editor.
- Los **niveles de cada clip**: el piloto normaliza la voz a −18 LUFS y el master a
  −14, pero los 333 clips de voz siguen importándose con `normalize` (pico a −1 dBFS).
- La **música**: «El Ultimo Duelo» es un lecho constante (RMS −18 dBFS de principio a
  fin, sin sección de clímax). El build del piloto sale de las capas, no del tema. Para
  que el conjuro suene a anime de verdad hace falta un tema con sección de clímax.
