# Kit Puzzles (niveles que van al compás)

Piezas genéricas para niveles cuya geometría obedece a una música. Nada de aquí sabe del juego que
las usa: lo que cambia entre juegos va en datos o se enchufa desde fuera.

- `MusicPulse`: reloj de tiempos y compases (BPM + tiempos por compás). Si hay una pista sonando,
  el reloj se lee de ella. Avisa de cada tiempo, de cada fuerte y del tiempo débil anterior.
  Tiene un clic audible para montar el nivel sin música.
- `PulseTile`: losa que late en el tiempo débil (se aplasta y tiembla) y se hunde en el fuerte.
  El patrón se reparte con `cycleMeasures` y `sinkOnMeasureInCycle`.
- `BeatGate`: rejilla o plataforma que dura N compases. Se abre o aparece al pedírselo desde un
  evento y vuelve sola cuando se cumple.
- `MeasureCandles`: cuenta atrás en compases con una vela por compás.
- `PulseTide`: marea que avanza metros por compás, se para (o se sujeta unos compases) y avisa al
  alcanzar. Quién cuenta como alcanzado lo decide el juego con `PulseTide.IsTarget`.
- `WeightPlate`: plato que cuenta cuerpos encima y avisa del peso. Quién es un cuerpo lo decide el
  juego con `WeightPlate.ResolveWeightBody`.

El juego enchufa quién cuenta como alcanzado, quién es un cuerpo y en qué nivel se usan desde su
carpeta de `Connections/` (ver TDD § 31).
