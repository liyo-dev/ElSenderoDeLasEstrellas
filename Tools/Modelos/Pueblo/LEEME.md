# Piezas del pueblo del prólogo (Blender)

`pueblo.py` las genera con `bpy` (Blender 5.2 de pip): `python pueblo.py <carpeta>`.

| FBX | Qué es | Notas |
|---|---|---|
| `Contraventana.fbx` | Una hoja de tablas de 1 × 1 m con su Z por las dos caras y dos pernios | Bisagra en x = 0, la hoja va hacia +X. Unity la escala al hueco de cada ventana. |
| `Mirador.fbx` | Ventana saliente con antepecho (1,4 × 2,1 × 0,62 m), jardinera y tejadillo | Para que alguien se asome: el antepecho tapa las piernas. Hojas `Hoja_Izq` / `Hoja_Der` con origen en su bisagra. |
| `PuertaDeCasa.fbx` | Marco, umbral, vano oscuro y la hoja (`Hoja`, bisagra a la izquierda) | Para que alguien entre en casa. |

Materiales por nombre (Madera, Hierro, Marco, Yeso, Teja, Fondo, Tierra, Hojas, Flor0-3, Puerta, Piedra):
`VestidoDelPrologo` los cambia por lisos con el estilo del pack (`FK01`).
