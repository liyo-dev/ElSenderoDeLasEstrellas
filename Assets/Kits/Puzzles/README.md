# Kit Puzzles (niveles que van al compás)

Piezas genéricas para niveles cuya geometría obedece a una música. Nada de aquí sabe del juego que
las usa: lo que cambia entre juegos va en datos o se enchufa desde fuera.

- `MusicPulse`: reloj de tiempos y compases (BPM + tiempos por compás). Si hay una pista sonando,
  el reloj se lee de ella. Avisa de cada tiempo, de cada fuerte y del tiempo débil anterior.
  Tiene un clic audible para montar el nivel sin música.
- `MusicScore`: partitura de una canción real: los tiempos medidos en la toma (el tempo puede
  moverse) y las marcas de cada golpe con su voz. Con ella, `TempoClock` toca la canción y sigue sus
  tiempos en lugar de un BPM fijo (`StartAtSeconds`, `SongTime`).
- `ScoreCueTrigger`: avisa un poco antes de cada marca de unas voces y en el golpe (eventos).
- `CueDrop`: lo que cae del techo en el golpe de cada marca, con su sombra creciendo. A quién golpea
  lo decide el juego con `CueDrop.Strike`. Con `strikeLength`, el golpe es una franja a lo largo de su «adelante» (un rayo que barre
  una línea), en círculos seguidos.
- `CueSinkingTile`: losa que tiembla y se hunde en el segundo de una marca y vuelve después.
- `PulseMotion`: empujón de una pieza que vuelve sola (cabeceo, mandíbula, cejas) en cada tiempo,
  en cada fuerte o desde un evento. Para mover recortes al compás sin animar a mano.
- `PulseTile`: losa que late en el tiempo débil (se aplasta y tiembla) y se hunde en el fuerte.
  El patrón se reparte con `cycleMeasures` y `sinkOnMeasureInCycle`.
- `BeatGate`: rejilla o plataforma que dura N compases. Se abre o aparece al pedírselo desde un
  evento y vuelve sola cuando se cumple.
- `MeasureCandles`: cuenta atrás en compases con una vela por compás.
- `PulseTide`: marea que avanza metros por compás, se para (o se sujeta unos compases) y avisa al
  alcanzar. Quién cuenta como alcanzado lo decide el juego con `PulseTide.IsTarget`.
- `WeightPlate`: plato que cuenta cuerpos encima y avisa del peso. Quién es un cuerpo lo decide el
  juego con `WeightPlate.ResolveWeightBody`.
- `StalkingPresence` + `RustlePoint` + `RustleShaker`: algo que acecha a quien lleva un objeto, sin combate
  ni derrota. En cada crujido (aviso con sonido que se acerca, matas en ola y mirada hacia el ruido) hay que
  quedarse quieto hasta que se aleje; si se sigue andando, tira el objeto a un sitio cercano. Crujidos en
  orden o que se repiten, demostración que tira sí o sí, punto de alivio. Quién es el sujeto, si lleva el
  objeto, cómo se tira, la mirada y el tope de velocidad los enchufa el juego con los estáticos de
  `StalkingPresence`.

El juego enchufa quién cuenta como alcanzado, quién es un cuerpo y en qué nivel se usan desde su
carpeta de `Connections/` (ver TDD § 31).
