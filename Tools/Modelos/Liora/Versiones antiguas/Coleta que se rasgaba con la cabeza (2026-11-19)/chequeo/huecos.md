# Chequeo de huecos vistos desde abajo — liora.blend
**PASA**

Poses: reposo, WalkFWD_InPlace_NoWeapon@1.0, WalkFWD_InPlace_NoWeapon@10.3, WalkFWD_InPlace_NoWeapon@19.7. Vistas por pose: 25 (desde abajo a 88, 65, 40, 15°). Hueco = la cámara ve el revés de una cara, que en Unity no se pinta.

Peor vista: WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 40°, giro 180°: 0.1 % de lo que se ve del muñeco es hueco.

## Piezas por las que se ve hueco: el fondo o el interior vacío (peor parte de una vista)
- Hair21_Liora: 0.09 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 40°, giro 180°)
- Vestido: 0.04 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 15°, giro 270°)
- Cuerpo: 0.04 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 65°, giro 135°)
- Manga_l: 0.03 % (peor en WalkFWD_InPlace_NoWeapon@10.3, desde abajo a 40°, giro 315°)
- Head01_Male: 0.02 % (peor en WalkFWD_InPlace_NoWeapon@10.3, desde abajo a 65°, giro 45°)
- Manga_r: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@10.3, desde abajo a 65°, giro 0°)
- Botin_r: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 40°, giro 270°)
- PunoForro_r: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 15°, giro 0°)

## Manos con los brazos caídos (idle): parte que se ve desde algún lado
- mano izquierda: 68 % (mínimo 35 %)
- mano derecha: 90 % (mínimo 35 %)

## Muslos sentado, de frente: parte que se ve
- muslo izquierdo: 61 % (mínimo 15 %)
- muslo derecho: 67 % (mínimo 15 %)

## Revés con algo pintado justo detrás (aviso menor: se ve lo de debajo por el borde)
- Hair21_Liora: 0.08 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 40°, giro 135°)