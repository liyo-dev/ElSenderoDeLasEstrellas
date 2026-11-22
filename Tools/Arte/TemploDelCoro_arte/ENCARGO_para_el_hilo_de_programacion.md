# Templo del Coro — arte de cartón: lo que hay que meter (encargo del hilo de arte, 21 nov 2026)

El hilo de arte **no ha tocado** `TemploDelCoroBuilder.cs`, la escena, `recortar.py` ni ningún `.meta`.
Todo lo que sigue es para que lo meta el hilo de diseño y programación.

## 1. Recortes: ya están en su sitio (no hay que hacer nada salvo reconstruir)
- Los 34 PNG de `Assets/ElSendero/Content/TemploDelCoro/Recortes/` se han sustituido por su versión final:
  canto de papel crema cortado a tijera, sombra dura abajo a la derecha, grano de cartón y luz suave de arriba.
  Las piezas de piedra (Tenor, órgano, columna) llevan además grietas, desconchones y algo de musgo abajo.
- **Mismo tamaño en píxeles y mismo dibujo en el mismo sitio** → los pivotes de `piezas.json` siguen valiendo. Los `.meta` no se han tocado.
- Sin canto (a propósito): `corista_burbuja_*`, `corista_boca`, `sombra_de_nota`.
- **Ojo si se vuelve a pasar `recortar.py`:** pisa estos PNG con los planos del blockout. Después, volver a pasar
  (en `Tools/Arte/TemploDelCoro_arte/`):
  ```
  python3 detalle_de_piedra.py <salida de recortar.py> <tmp>     # solo cambia las piezas de piedra
  python3 pasar_a_carton.py <tmp> <Recortes de Assets>
  ```
  (`detalle_de_piedra.py` deja tal cual las piezas que no son de piedra; copiar antes todo el blockout a `<tmp>`.)

## 2. Texturas del decorado: falta conectarlas en el builder
Nuevas en `Assets/ElSendero/Content/TemploDelCoro/Texturas/` (512 px, repiten sin costura; Unity creará sus `.meta` al importar):

| Textura | Para el material de `CrearMateriales()` | Tamaño de una repetición en el mundo |
|---|---|---|
| `tablas_escenario.png` (4 tablas a lo ancho) | `Suelo` (`_gris`) | 4 m × 4 m (tablas de 1 m) |
| `muro_de_carton.png` | `Muro` (`_grisOscuro`) | 4 m × 4 m |
| `terciopelo.png` (8 pliegues verticales) | `Telon` (`_telon`) | 4 m de ancho × alto del muro (pliegues siempre en vertical) |
| `madera_oscura.png` | `Madera` (`_madera`) | 2 m × 2 m |

Lo que pide (propuesta, decide el hilo de programación):
1. Que esos cuatro materiales usen el shader oficial **`El Sendero/Dibujo`** (no `URP/Lit`, regla del aspecto de dibujo) con la textura en `_BaseMap` y el color en blanco (`_BaseColor` no tiñe).
   El resto (Pentagrama, Brasa, Luz, Runas, transparentes…) se queda liso.
2. Las cajas son cubos de 1×1 estirados: con la UV del cubo la textura se estira. Hace falta repetir según el tamaño de cada caja, por ejemplo en `Caja(...)`:
   un `MaterialPropertyBlock` con `_BaseMap_ST = (ancho/repetición, alto/repetición, 0, 0)` usando la cara que se ve (suelos: X y Z; muros y telones: largo y alto).
   Si el shader no admite bien el bloque con el SRP Batcher, alternativa: UV en mundo (triplanar) en una variante del material del templo.
3. Tablas del suelo: que corran **hacia el fondo** (a lo largo de Z, como un escenario visto desde el patio de butacas).
4. Al acabar, compilar y mirar `Logs/Editor.log` (regla 6) y reconstruir el templo con su menú.

## 3. Referencia de estilo
- Lienzo de concepto (paleta y capas): https://claude.ai/artifact/EjMsHUtb4Q2M6TBA24ABn6 — el Tenor de ese lienzo es un concepto; manda el de los recortes.
- Reglas del cartón: canto crema y sombra dura en todo lo plano; colores de los elementos solo en lo que se rompe o se devuelve; el grupo en 3D, el decorado en cartón.
- Copia del blockout por si hay que volver atrás: `Tools/Arte/TemploDelCoro_arte/Versiones antiguas/Recortes_blockout/`.
