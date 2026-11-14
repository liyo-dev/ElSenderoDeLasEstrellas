# Chequeo de huecos vistos desde abajo — will.blend
**NO PASA (6 vistas con hueco)**

Poses: reposo, WalkFWD_InPlace_NoWeapon@1.0, WalkFWD_InPlace_NoWeapon@10.3, WalkFWD_InPlace_NoWeapon@19.7. Vistas por pose: 25 (desde abajo a 88, 65, 40, 15°). Hueco = la cámara ve el revés de una cara, que en Unity no se pinta.

Peor vista: WalkFWD_InPlace_NoWeapon@10.3, desde abajo a 65°, giro 0°: 2.0 % de lo que se ve del muñeco es hueco.

## Piezas por las que se ve hueco: el fondo o el interior vacío (peor parte de una vista)
- **GRAVE** Hair16_Heroe: 1.90 % (peor en WalkFWD_InPlace_NoWeapon@10.3, desde abajo a 65°, giro 0°)
- Body23_Aventurero: 0.55 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 15°, giro 180°)
- Body24_AventureroChaqueta: 0.42 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 65°, giro 225°)
- Head01_Male: 0.06 % (peor en WalkFWD_InPlace_NoWeapon@10.3, desde abajo a 88°, giro 0°)

## Manos con los brazos caídos (idle): parte que se ve desde algún lado
- mano izquierda: 94 % (mínimo 35 %)
- mano derecha: 90 % (mínimo 35 %)

## Muslos sentado, de frente: parte que se ve
- muslo izquierdo: 70 % (mínimo 15 %)
- muslo derecho: 70 % (mínimo 15 %)

## Revés con algo pintado justo detrás (aviso menor: se ve lo de debajo por el borde)
- Hair16_Heroe: 0.72 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 65°, giro 0°)
- Body24_AventureroChaqueta: 0.46 % (peor en reposo, desde abajo a 40°, giro 90°)
- Body23_Aventurero: 0.37 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 65°, giro 45°)