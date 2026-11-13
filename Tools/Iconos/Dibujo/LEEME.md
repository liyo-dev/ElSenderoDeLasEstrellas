# Tools/Iconos/Dibujo — iconos con el aspecto de dibujo (INC-1137)

Iconos de objetos (`Art/UI/Items`), habilidades (`Art/UI/Abilities`) y estados sobre la cabeza de
los NPC (`Art/UI/NpcStatus`, con los 12 fotogramas de la charla) dibujados por código con la misma
regla que los iconos de hechizos: colores planos, banda de sombra de color abajo a la derecha,
brillo pequeño, contorno de tinta `#33263A` y destellos de cuatro puntas. Los estados de los NPC
van sobre una ficha de papel crema, como la interfaz.

| Archivo | Qué hace |
|---|---|
| `dibujo.py` | Piezas comunes (forma con sombra y brillo, tinta, destellos, caminos). |
| `iconos_objetos.py` | Un icono por objeto y por habilidad. `python iconos_objetos.py salida` los deja en `salida/`. |
| `iconos_estado.py` | Fichas de estado y charla animada. |
| `instalar.py` | Los escribe encima de los del proyecto con el mismo tamaño (conserva GUID e import) y guarda los originales en `Versiones antiguas/`. |

Necesita `pycairo` y `Pillow`. Para un objeto nuevo: añadir su función en `iconos_objetos.py`,
su nombre en `OBJETOS` y volver a ejecutar `instalar.py`.
