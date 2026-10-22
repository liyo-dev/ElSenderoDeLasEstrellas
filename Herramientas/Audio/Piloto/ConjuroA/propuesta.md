# Propuesta A — la música del conjuro del prólogo

Piloto: `Herramientas/Audio/propuesta_conjuro.py` (lee los tiempos del guion
horneado y monta la ventana del cántico tal y como la mezcla el juego).

## Los tres ficheros para escuchar

En `Herramientas/Audio/Piloto/ConjuroA/`:

| Fichero | Qué es |
|---|---|
| `00_actual.mp3` | La mezcla de hoy: el tema del duelo (`El Ultimo Duelo`) suena solo desde el duelo hasta el estallido. |
| `01_propuesta_A.mp3` | Propuesta A: el tema se corta en el negro, el lecho ritual del cántico lleva las estrofas y el tema vuelve con el grito **desde donde se cortó** (el juego recuerda la posición del clip). |
| `02_propuesta_A_tema_al_cabeza.mp3` | Igual, pero el tema vuelve **desde el principio** del clip. |

Los tres cubren la misma ventana (2:40–3:42, 62,9 s) y llevan la voz ya
caracterizada del Mago Oscuro (efecto B), el eco del conjuro, los truenos, la
tormenta y el bucle del agujero negro. La única diferencia entre ellos es la
música.

## El problema de hoy

Del negro (2:55,4) al estallido (3:39,3) hay **44 s** con el mismo tema a tope.
El cántico son 6 frases y 4 planos de la misma cosa: la música no cuenta nada
distinto, y lo único que marca el paso es el fundido a negro. Medido en la
mezcla horneada:

| Tramo | Ahora | Con A |
|---|---|---|
| «Perdóname, Liora» (3:28,6–3:32,4) | −15,3 LUFS | −19,4 LUFS |
| Cántico (2:58,1–3:28,6) | −15,7 LUFS | −17,2 LUFS |
| «Que este valle…» del Mago (2:41,3) | −17,4 LUFS | −18,4 LUFS |

## Qué cambia (propuesta A)

1. **El tema se corta en el negro** (2:56,7, fundido de 0,8 s). El tema del
   duelo no acompaña al cántico: es la música de la pelea, y aquí ya no hay
   pelea todavía.
2. **Entra el lecho ritual del cántico** (`Prologue_CoraCanto`, 38 s, a 0,75
   volumen, `ritual=si`: va al bus de música y **no** se duckea con la voz) más
   el bucle de coro (`SFX_Prologo_CoroBucle`, 0,14). Son las capas que ya
   estaban en el guion antes de INC-806 y que quitó «la canción original, sin
   retoques».
3. **La estrofa entra con la quinta estrofa** (`Prologue_EstrofaCanto`, 18 s):
   el cántico sube justo cuando la voz sube.
4. **La despedida se dice en voz baja**: al empezar «Perdóname, Liora» la
   estrofa se apaga (fundido 0,5 s) y solo queda el lecho, que está 7 dB bajo
   la voz. Es el silencio íntimo que el guion pide con el comentario, sin
   tocar la voz ni su texto.
5. **El lecho y el coro se apagan en el respiro** (3:35,1, fundido 0,3 s): el
   impacto tiene que entrar en seco.
6. **El tema vuelve con el grito** (3:35,1), con la campana del golpe
   (`Prologue_GolpeFinal`, la música a −6 dB durante 0,6 s) y la cola del
   estallido (`Prologue_GolpeFinalCola`).

## Los cambios exactos en el guion

`Assets/_SEQUENCES/Guiones/Prologo_UltimaNoche.guion.txt`

**1) En el bloque del negro (líneas 543-549), después del `MezclaBeat`:**

```
# El tema del duelo se corta aquí: el conjuro lo sostiene el lecho del cántico.
silencio 0.8
ambiente CORO eventKey=SFX_Prologo_CoroBucle volumen=0.14 fundido=1.2
ambiente CORA eventKey=Prologue_CoraCanto volumen=0.75 ritual=si fundido=1.2
```

Y el comentario de la línea 548 cambia de «La música del hechizo es la canción
original, sin retoques: una sola pista, a tope» a «El tema se corta en el
negro: el cántico entra con su lecho ritual».

**2) En la quinta estrofa (antes de `PROLOGO_PLEGARIA_05`):**

```
# La estrofa entra con la quinta estrofa: el cántico sube.
ambiente ESTROFA eventKey=Prologue_EstrofaCanto volumen=0.75 ritual=si fundido=1.2
```

**3) En la despedida (antes de `PROLOGO_PLEGARIA_06`):**

```
# La despedida se dice en voz baja: la estrofa se apaga y solo queda el lecho.
ambiente ESTROFA sonar=false fundido=0.5
```

**4) En el grito (después del `RespiroBeat`):**

```
# El lecho y el coro se apagan aquí: el impacto tiene que entrar en seco.
ambiente CORA sonar=false fundido=0.3
ambiente CORO sonar=false fundido=0.3
# El tema del duelo vuelve con el grito, donde se había quedado.
musica MAGOOSCURO_CLIMAX fadeOut=0.3
efecto GolpeMusicalBeat eventKey=Prologue_GolpeFinal volumen=1 musicaDb=-6 duracion=0.6
```

**5) En el estallido (línea 645, con el fundido a blanco):**

```
efecto GolpeMusicalBeat eventKey=Prologue_GolpeFinalCola volumen=1 musicaDb=-6 duracion=0.6
```

## Decisión pendiente: cómo vuelve el tema

- **01 (recommended).** Vuelve **desde donde se cortó** (a los 44,2 s del
  clip): el juego recuerda la posición de cada clip (`AudioService.
  _posicionesDeMusica`), así que el `musica MAGOOSCURO_CLIMAX` del grito
  continúa la misma frase. Suena a «el tema vuelve», no a «empieza otra
  pista».
- **02.** Vuelve **desde el principio** del clip. Suena a una afirmación nueva
  y más grande, pero corta el hilo: el oyente oye el arranque del tema otra
  vez.

## Lo que no se toca

- La voz: ni el texto, ni el doblaje, ni el preset `Conjuro` (9 s de sala y el
  slap a tope). La entrega del Archimago es exactamente la misma.
- Los planos y los tiempos de las frases: la propuesta es solo de música.
- El primer «No daré ni un paso atrás…»: sigue a pelo, sin eco y con la
  música a −20 dB (lo que se oye es silencio).

## Si se aprueba

1. Editar el guion con los cinco cambios de arriba.
2. Rehornear: *El Sendero → Guion → Hornear el guion del prólogo* (o salir de
   Play, que rehornea solo).
3. Escuchar el tramo en Play y decidir entre 01 y 02.
