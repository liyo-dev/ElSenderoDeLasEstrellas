# Kit WorldWrap (mundo cíclico)

Piezas para que un mundo abierto rodeado de mar parezca no tener fin y, más adelante, dé la vuelta
(sales por un borde y entras por el contrario, como los mapas del mundo de los JRPG clásicos).

- `EndlessSea`: plano de mar que se coloca bajo cada cámara de juego antes de dibujar (en X y Z,
  encajado a una rejilla), así su borde nunca se ve. Va con un agua cuyo shader trabaja en
  coordenadas del mundo. `Size` = lado en metros (mayor que el doble del plano lejano de la cámara).
- `AltitudeCeiling`: techo de altura de la escena (`Current`). Quien mueve al personaje en vertical
  pide `Limit(y, velocidad)`: la subida se frena en una franja suave bajo el techo y, por encima, baja.
- `WorldWrapper`: bordes cosidos. Un rectángulo (`Center`, `Size`); cuando lo que vigila
  (`TargetProvider`) pasa de un borde, mueve un lado entero lo que lo lleva (`MoverResolver`: el
  objetivo o, p. ej., su barco) y lo que le acompaña: lo que añada `PassengersProvider` (el grupo que
  le sigue), los `WorldWrapPassenger` (siempre o dentro de su radio) y los cuerpos con Rigidbody a
  menos de `carryRadius` (proyectiles, barcos, cosas que flotan). Cada uno cruza con su Rigidbody
  (sin interpolación durante un paso, para que no se pinte a medio camino), su NavMeshAgent
  (`Warp`), sus estelas y sus partículas en espacio mundo; un hijo de algo que ya cruza no se mueve
  dos veces. Avisa con `ObjectShifted(t, delta)` por objeto (cámaras que lo siguen) y con
  `Shifted(delta)` una vez (lo demás). `Wrap(pos)` da la posición dentro del rectángulo (para
  guardar); `DistanceToEdge(pos)`, lo que falta hasta la costura.
- `TotalShift` y el vector global de shader `_WorldWrapShift` (configurable): suma de lo desplazado.
  Un shader que dibuja en coordenadas del mundo (agua, nubes) usa `posición − _WorldWrapShift` para
  que su dibujo no salte al cruzar.
- `WorldWrapPassenger`: marca algo que cruza con el objetivo (siempre, o si está a menos de su radio).
- Editor: *Tools/WorldWrap/Revisar la costura de las escenas abiertas* y el botón «Revisar la
  costura» del inspector de `WorldWrapper` (`WorldWrapSeamCheck`): renderers y terrenos a menos de la
  distancia de vista del borde (se verían aparecer al cruzar) o fuera del rectángulo.

La costura tiene que quedar en mar abierto y más lejos de cualquier tierra que la distancia de
vista (plano lejano o niebla): no se duplica nada, así que lo que se vea desde el borde aparece o
desaparece de golpe al cruzarlo.

El kit no conoce el juego: qué objeto es el mar y su tamaño los pone quien lo usa.
