# Liora: diseño propio (13 nov 2026)

Elegido por Raúl:
- **Relación con el Archimago:** amiga; se deja abierta.
- **Paleta:** verde salvia y crema.

Sale en el prólogo (desayuno, despedida) y en el final, con el Archimago. **Estado: previa para aprobar; no está en Unity.**

## Piezas
- `liora.py` (Blender 5.x; argumentos: base.blend y salida) → `Body35_Liora`. Hecho con el taller (`../Prendas/taller.py`):
  - vestido largo salvia hasta el tobillo, con el bajo crema;
  - cuello ancho crema con una flor amarilla;
  - faja tejida de salvia oscuro, con el nudo y dos cabos a su izquierda;
  - mangas recogidas en la muñeca con puño crema (`manga_con_puno`);
  - medias crema metidas en botines bajos de cuero (`pernera`, `bota`);
  - corte del cuerpo tapado con el salvia oscuro (`quitar_lo_tapado`).
- Sin delantal, para no confundirse con la boticaria (vestido verde y delantal ámbar) ni con Victoria (lila con delantal).
- FBX en `para_unity/` (no en Assets).

## Chequeos (`chequeo/`)
- Choques: PASA.
- Huecos desde abajo: PASA. Manos con los brazos caídos, 68 % y 90 %. Muslos sentada, 58 % y 61 %.

## Falta
- **Pelo y cara.** La previa lleva la cabeza `Head02_Female` y el pelo `Hair06` del pack solo para la foto. Falta decidir su peinado (pelo propio con `caras_lib`, con `con_grosor` en los mechones) y sus caras (`kit_npcs.py`).
- Visto bueno de Raúl; después, entrar en `VestuarioDeLosNPCs`.
