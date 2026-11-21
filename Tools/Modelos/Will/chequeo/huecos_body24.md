# Chequeo de huecos vistos desde abajo — will3.blend
**PASA (47 vistas con algún hueco pequeño: mirar las fotos)**

Poses: reposo, WalkFWD_InPlace_NoWeapon@1.0, WalkFWD_InPlace_NoWeapon@10.3, WalkFWD_InPlace_NoWeapon@19.7. Vistas por pose: 25 (desde abajo a 88, 65, 40, 15°). Hueco = la cámara ve el revés de una cara, que en Unity no se pinta.

Peor vista: WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 15°, giro 180°: 1.2 % de lo que se ve del muñeco es hueco.

## Piezas por las que se ve hueco: el fondo o el interior vacío (peor parte de una vista)
- Bota_r: 0.86 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 15°, giro 225°)
- Pernera_r: 0.70 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 15°, giro 180°)
- Bota_l: 0.66 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 15°, giro 225°)
- Pernera_l: 0.48 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 15°, giro 180°)
- Camiseta: 0.27 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 40°, giro 180°)
- MangaChaqueta_r: 0.19 % (peor en WalkFWD_InPlace_NoWeapon@10.3, desde abajo a 88°, giro 0°)
- Head01_Male: 0.13 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 88°, giro 0°)
- MangaChaqueta_l: 0.12 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 65°, giro 180°)
- Cuerpo: 0.06 % (peor en WalkFWD_InPlace_NoWeapon@10.3, desde abajo a 88°, giro 0°)
- Brazo_r: 0.04 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 65°, giro 45°)
- Brazo_l: 0.02 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 15°, giro 135°)
- HombroChaqueta_r: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 65°, giro 0°)

## Manos con los brazos caídos (idle): parte que se ve desde algún lado
- mano izquierda: 94 % (mínimo 35 %)
- mano derecha: 90 % (mínimo 35 %)

## Muslos sentado, de frente: parte que se ve
- muslo izquierdo: 64 % (mínimo 15 %)
- muslo derecho: 64 % (mínimo 15 %)

## Revés con algo pintado justo detrás (aviso menor: se ve lo de debajo por el borde)
- Camiseta: 1.44 % (peor en reposo, desde abajo a 40°, giro 90°)
- MangaChaqueta_l: 0.14 % (peor en reposo, desde abajo a 40°, giro 90°)
- MangaChaqueta_r: 0.14 % (peor en reposo, desde abajo a 40°, giro 270°)
- Bota_l: 0.11 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 15°, giro 0°)
- Bota_r: 0.11 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 40°, giro 0°)