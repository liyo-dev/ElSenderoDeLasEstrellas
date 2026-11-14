# Kit Waterfalls (cascadas de dibujo)

Cascadas con aspecto de dibujo animado que siguen un recorrido (por ejemplo, el centro de un río):

- `Waterfall`: monta la cortina de agua a partir de un recorrido y sus anchos. Solo se ve donde el
  recorrido cae con pendiente (los tramos llanos los deja al agua que ya haya); en cada tramo que
  cae sabe dónde está el labio de arriba y el pie (`Runs`).
- `WaterCurtainZone`: la cortina se puede atravesar. Al cruzarla salta espuma donde la toca y avisa
  con eventos (para mojar al personaje, sonidos…).
- Shaders `Kits/Waterfalls/Curtain` (franjas que bajan, labio y pie de espuma, borde con tinta) y
  `Kits/Waterfalls/Foam` (espuma y salpicaduras sólidas de dos tonos).
- `WaterfallBuilder` (editor): crea la cascada completa con su espuma, salpicaduras, vapor y zonas
  de cortina, y los materiales (en la carpeta que diga quien lo usa).

El kit no conoce el juego: los colores van en `WaterfallLook` y los recorridos los da quien lo usa.
