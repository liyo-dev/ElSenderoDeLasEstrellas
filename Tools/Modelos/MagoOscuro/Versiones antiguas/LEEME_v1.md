# Mago Oscuro: diseño propio (12 nov 2026)

Dirección elegida por Raúl: **figura imponente de sombra, sin cara hasta el final**. **Estado: primera previa hecha; todavía no está en el juego.**

- `mago_oscuro.py` (Blender 5.x, bpy) → dos piezas sobre el esqueleto de RPG Tiny Hero (base.blend = `AllBodiesCloaks.fbx` importado):
  - `Body33_MagoOscuro`: manos y torso de Body05 con la ropa encima. Túnica hasta el suelo con el bajo ondulado y franja de luz violeta; cinto con hebilla de agujero negro; capa rota con estrellas apagadas y forro violeta; cuello alto en abanico con punta central; cuernos de hombro en media luna; mangas anchas con puño de luz; siete fragmentos de hueso flotando detrás de la cabeza.
  - `AC14_VeloDeSombra` (accesorio aparte): capucha alta con la punta caída hacia atrás, esfera negra que tapa la cara y dos ojos de luz violeta. **Se quita para el final y deja ver la cabeza del pack** (la previa «sin velo» usa Head01_Male solo para que se vea).
  Argumentos: base.blend y salida.
- `mago_oscuro_previa.png`: frente, tres cuartos, lado y espalda (`../Eldran/previa.py` y `../Ninos/foto.py`, con `FOTO_ALTO=1.9 FOTO_CENTRO=0.88`); `mago_oscuro_sin_velo.png`: lo mismo sin el velo.
- `para_unity/`: `Body33_MagoOscuro.fbx` y `AC14_VeloDeSombra.fbx` (`../Prendas/exportar_prenda.py`). **No están en Assets.**

Medidas útiles: la cabeza del pack va de z 0,68 a 1,31, ~0,74 de ancho, centro (0, -0,013, 0,99).

Pendiente / sin comprobar:
- **Qué cara hay bajo el velo** (y si recuerda al Archimago) lo decide Raúl; es historia.
- Los ojos de luz son malla fija en la previa. En el juego deberían salir del sistema de caras (ojos por emoción: se entrecierran, se abren, se apagan) y con material que brille (emisivo).
- Nada se ha visto moverse: túnica y capa copian los pesos del cuerpo (la túnica esconde las piernas al levitar, que era el fallo del prólogo), el velo y los fragmentos van a `head`. La capa no usa los huesos `CloakBone`.
- Entrada en Unity: igual que los siete NPCs (`VestuarioDeLosNPCs`), con una ficha nueva para `_MAGO_OSCURO` y pasar los materiales al shader de dibujo.
- Con el Archimago (hoy el modelo antiguo de Will), pendiente: diseñarlo como pareja.
