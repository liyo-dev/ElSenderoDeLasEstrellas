# Prendas sueltas (Chuchelandia)

Prendas hechas en Blender 5.2 sobre el esqueleto de RPG Tiny Hero (AllBodiesCloaks.fbx), pesadas
copiando los pesos del cuerpo que llevan debajo. En el juego las pone `PrendasSueltas` (cambia sus
huesos por los del personaje, por nombre) desde los atuendos de escena (`prendasSueltas`).

- `vestido.py`: vestido de candidata de Estela (sobre Body09) → `vestido.blend`.
  Argumentos: base.blend (AllBodiesCloaks importado) y salida.
- `exportar_prenda.py`: FBX sin animaciones → `Assets/Art/Characters/Chuchelandia/Prendas/`.
- `ropa_chicos.py`: disfraces de Will (`armadura` → Armadura_Gominola, sobre Body05) y Liam
  (`paje` → Traje_Paje, sobre Body12, con boina y cuerno de unicornio). Argumentos: base.blend, armadura|paje, salida.
