# Liora: diseño propio (13 nov 2026)

Elegido por Raúl:
- **Relación con el Archimago:** amiga; se deja abierta.
- **Paleta:** verde salvia y crema.

Sale en el prólogo (desayuno, despedida) y en el final, con el Archimago. **Estado (14 nov 2026): terminada y en Unity, sin probar en Play.**

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

## Pelo y cara (14 nov 2026)
- `Hair21_Liora` (`../Caras/pelo_liora.py`, después `../Caras/coleta_a_la_espalda.py`): castaño cálido, raya a su izquierda con
  un mechón que barre la frente, orejas tapadas y coleta baja pegada a la espalda con una cinta crema. Mechones con grosor.
- `Caras_liora` (`../Caras/kit_npcs.py`): **Eye330-339 + Mouth330-339**, ojos almendrados verdes, cejas finas castañas, mejillas
  suaves, boca de reposo con sonrisa suave (`amable`). Cabeza `Head02_Female`.
- Chequeos con el pelo: choques y huecos en PASA. Previa completa en `liora_previa.png`.

## En Unity
- FBX en `Assets/Art/Characters/NPCs/Vestuario/Liora/` (`Body35_Liora`, `Hair21_Liora`) y `Assets/Art/Characters/NPCs/Caras/Caras_NPC_liora.fbx`.
- Liora salía con `Tendera.prefab` (la tendera del Reino). *Vestuario propio de los NPCs* crea `Assets/_NPCs/Liora.prefab` (copia de
  Tendera), cambia la entrada `SPAWN_NPC_Liora` del roster del prólogo a ese prefab y la viste. Después, *Caras de los NPCs* le pone
  su cara (juego `liora`). Pasar los dos menús en ese orden.
