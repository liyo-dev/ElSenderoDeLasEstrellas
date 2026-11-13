# Prendas sueltas (Chuchelandia)

Prendas hechas en Blender 5.2 sobre el esqueleto de RPG Tiny Hero (AllBodiesCloaks.fbx), pesadas
copiando los pesos del cuerpo que llevan debajo. En el juego las pone `PrendasSueltas` (cambia sus
huesos por los del personaje, por nombre) desde los atuendos de escena (`prendasSueltas`).

- `vestido.py`: vestido de candidata de Estela (sobre Body09) → `vestido.blend`.
  Argumentos: base.blend (AllBodiesCloaks importado) y salida.
- `exportar_prenda.py`: FBX sin animaciones → `Assets/Art/Characters/Chuchelandia/Prendas/`.
- `ropa_chicos.py`: disfraces de Will (`armadura` → Armadura_Gominola, sobre Body05) y Liam
  (`paje` → Traje_Paje, sobre Body12, con boina y cuerno de unicornio). Argumentos: base.blend, armadura|paje, salida.

## ropa_lib.py y chequeo_choques.py (13 nov 2026)
- `ropa_lib.py`: pesos de la ropa por zonas en vez de copiar «el vértice más cercano» del cuerpo. Cada pieza dice de qué zona copia:
  - `TRONCO`, `PIERNAS` o `BRAZO('l')`, o una combinación;
  - si es faldón: `Faldon(z_cintura, z_bajo, muslo)`;
  - si es rígida: `{hueso: peso}`;
  - si va pegada a otra prenda: `Zona(sobre=prenda)`.
- Además, `ropa_lib.py`:
  - interpola los pesos en la cara del cuerpo más cercana y los suaviza sobre la prenda;
  - etiqueta cada pieza (atributo «pieza») para que el chequeo diga su nombre.
- Reglas para no repetir los fallos de los magos:
  - el tronco no copia del brazo;
  - la túnica no pasa de ~0,19-0,21 de radio bajo el brazo (el cuerpo mide 0,18 y el brazo arranca en 0,20);
  - los puños, ceñidos a la boca de la manga (el brazo mide 0,08);
  - capas y adornos, por la espalda y pesados como lo que tapan.
- `chequeo_choques.py`: mueve el personaje con las animaciones del pack y con poses de gesto (brazos bajados, cruzados, mano al frente, brazos arriba) y mide:
  - tela que se rasga;
  - cuerpo que asoma;
  - piezas metidas en otra prenda;
  - piezas que se sueltan.
- Su veredicto es PASA o NO PASA, con informe (`chequeo.md`) y fotos con lo malo en rojo (`chequeo.png`). Ninguna prenda nueva debería exportarse sin pasarlo.
- Uso: `pip install bpy==5.3.0` y luego `python chequeo_choques.py -- personaje.blend carpeta_salida --piezas pelo.fbx,capucha.fbx`. Por defecto toma las animaciones de `Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon`.
