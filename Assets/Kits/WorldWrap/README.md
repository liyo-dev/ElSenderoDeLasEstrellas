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
  objetivo o, p. ej., su barco), con su Rigidbody, sus estelas y sus partículas en espacio mundo, y
  avisa con el evento estático `Shifted(delta)` para lo demás que deba acompañarle. `Wrap(pos)` da la
  posición dentro del rectángulo (para guardar). La costura debe quedar en mar abierto: no se
  duplica nada, así que lo que esté cerca del borde se vería aparecer al cruzarlo.

El kit no conoce el juego: qué objeto es el mar y su tamaño los pone quien lo usa.
