# Chequeo de huecos vistos desde abajo — archimago.blend
**PASA**

Poses: reposo, WalkFWD_InPlace_NoWeapon@1.0, WalkFWD_InPlace_NoWeapon@10.3, WalkFWD_InPlace_NoWeapon@19.7. Vistas por pose: 25 (desde abajo a 88, 65, 40, 15°). Hueco = la cámara ve el revés de una cara, que en Unity no se pinta.

Peor vista: WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 65°, giro 180°: 0.1 % de lo que se ve del muñeco es hueco.

## Piezas por las que se ve hueco: el fondo o el interior vacío (peor parte de una vista)
- Hair20_Archimago: 0.07 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 65°, giro 180°)
- Cuerpo: 0.04 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 40°, giro 90°)
- Head01_Male: 0.03 % (peor en WalkFWD_InPlace_NoWeapon@10.3, desde abajo a 65°, giro 45°)
- Pernera_l: 0.03 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 88°, giro 0°)
- Tunica: 0.03 % (peor en WalkFWD_InPlace_NoWeapon@10.3, desde abajo a 40°, giro 315°)
- Sobretunica: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 65°, giro 135°)
- Manga_l: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 65°, giro 0°)
- RibeteD: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 40°, giro 270°)
- PunoRibete_r: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 40°, giro 225°)
- PunoForro_l: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@10.3, desde abajo a 15°, giro 135°)

## Revés con algo pintado justo detrás (aviso menor: se ve lo de debajo por el borde)
- Hair20_Archimago: 0.08 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 65°, giro 135°)