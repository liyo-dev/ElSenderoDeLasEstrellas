# Peinados hechos con `pelo_principe.py` (13 nov 2026)

Raúl: «estos peinados están chulos, guárdatelos y hacemos NPCs». Quedan como catálogo para NPCs con peso; Will lleva la C.

| Variante | Qué es | Dónde se ve |
|---|---|---|
| A · Príncipe | Tupé suave que sube y cae hacia un lado, raya al lado, flequillo barrido en dos mechones grandes. Por detrás, liso | `will_pelo_A_C_y_mezcla.png` (fila 1) |
| B · Media melena | Tapa media oreja, puntas hacia fuera, flequillo de cortina partido | `will_pelo_propuestas.png` (fila 3) |
| C · Héroe pulido | Picos anchos con la punta suave hacia arriba y atrás, flequillo de lado. **Es el de Will** (`Hair16_Heroe`) | `will_pelo_C_final.png` |
| D · A + C | Tupé y flequillo de A; en la coronilla y la nuca, mechones largos peinados hacia atrás con la punta levantada | `will_pelo_A_C_y_mezcla.png` (fila 3) |

Para otro personaje:
- `python pelo_principe.py -- modular.blend salida.blend <A|B|C|D>` con `NOMBRE_PELO=HairNN_Nombre SOLO_PELO=1`; se exporta con `../../Prendas/exportar_prenda.py`.
- Los materiales salen como `Pelo` y `PeloClaro`: el color lo pone la ficha del personaje en Unity.
- Pasar `../../Caras/chequeo_pelo.py`. Con las orejas a la vista (A, C, D), la línea «lado» va por encima de la oreja: `1.19 1.10 0.82`.
- La forma se cambia en las tablas de cada variante: volumen (`VOL`, con `campana`), línea del pelo (`LINEA`) y mechones (`petalo`).
