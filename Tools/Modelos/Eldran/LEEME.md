# Eldran: vestuario propio (11 nov 2026)

Primera pieza de la propuesta de vestuario de NPCs con peso (`claude/propuesta-vestuario-npcs-con-peso-2026-11-11.md`,
aprobada por Raúl). **Estado: previa hecha; todavía no está en el juego.**

- `eldran.py` (Blender 5.x, módulo bpy): abrigo largo de mago viejo, verde musgo apagado, hasta la rodilla y con
  vuelo; cuello vuelto, solapa y botones de latón, cinturón, bufanda gris con un cabo a la
  espalda, coderas de cuero, zurrón cruzado con dos libros, pantalón y botas (`Body26_AbrigoDeMago`: manos y
  torso de Body05 debajo). Argumentos: base.blend (`AllBodiesCloaks.fbx` importado) y salida.
  Las funciones de modelado son las de `../Liam/liam.py`.
- `previa.py`: junta el traje con la cabeza y el pelo del pack (Head05_Santa, Hair13) para la foto.
  Argumentos: blend del traje, salida, piel r,g,b, pelo r,g,b y los FBX de cabeza y pelo.
- `eldran_previa.png`: frente, tres cuartos, lado y espalda (`../Ninos/foto.py`). La cara no lleva los ojos ni
  la boca del juego: van aparte.
- `para_unity/Body26_AbrigoDeMago.fbx`: exportado con `../Prendas/exportar_prenda.py`. **No está en Assets**:
  se copia a `Assets/Art/Characters/NPCs/Eldran/` cuando Raúl dé el visto bueno y Unity esté libre, y entonces
  falta el menú que se lo pone a `Assets/_NPCs/Eldran.prefab` y `_GrafoNarrativo/Eldran` (hoy llevan Body12),
  con sus materiales en el shader de dibujo.

Sin tocar: cabeza, barba, ojos y pelo. El «pelo con el suavizado de Will» de la propuesta queda pendiente.
Sin comprobar: cómo se mueve el abrigo al andar (los pesos se copian del cuerpo) y si el zurrón roza el brazo.

## Cambio del 11 nov (noche)
En el juego, el cabo de la bufanda que colgaba por delante salía por debajo de la barba de Head05_Santa y la
atravesaba (Raúl: «tiene una cosa gris que le atraviesa»). Se quita ese cabo y su fleco; quedan la vuelta al
cuello (tapada por la barba) y el cabo de la espalda. La previa nueva es con los brazos bajados y la cabeza sin
pelo. Lo anterior, en `Versiones antiguas/Bufanda con cabo delante (2026-11-11)/`.
