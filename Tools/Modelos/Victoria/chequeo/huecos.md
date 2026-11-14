# Chequeo de huecos vistos desde abajo — victoria.blend
**PASA**

Poses: reposo, WalkFWD_InPlace_NoWeapon@1.0, WalkFWD_InPlace_NoWeapon@10.3, WalkFWD_InPlace_NoWeapon@19.7. Vistas por pose: 25 (desde abajo a 88, 65, 40, 15°). Hueco = la cámara ve el revés de una cara, que en Unity no se pinta.

Peor vista: WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 40°, giro 135°: 0.3 % de lo que se ve del muñeco es hueco.

## Piezas por las que se ve hueco: el fondo o el interior vacío (peor parte de una vista)
- Vuelta_l: 0.25 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 40°, giro 135°)
- Vuelta_r: 0.13 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 40°, giro 270°)
- Head01_Male: 0.12 % (peor en WalkFWD_InPlace_NoWeapon@10.3, desde abajo a 65°, giro 225°)
- Cuerpo: 0.04 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 40°, giro 90°)
- Media_r: 0.03 % (peor en WalkFWD_InPlace_NoWeapon@10.3, desde abajo a 65°, giro 135°)
- Falda: 0.02 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 65°, giro 315°)
- Media_l: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 65°, giro 270°)
- Manga_l: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 65°, giro 0°)
- DelantalFalda: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 15°, giro 225°)
- Manga_r: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 15°, giro 0°)
- Blusa: 0.01 % (peor en reposo, desde abajo a 15°, giro 180°)

## Manos con los brazos caídos (idle): parte que se ve desde algún lado
- mano izquierda: 45 % (mínimo 35 %)
- mano derecha: 73 % (mínimo 35 %)

## Muslos sentado, de frente: parte que se ve
- muslo izquierdo: 49 % (mínimo 15 %)
- muslo derecho: 49 % (mínimo 15 %)

## Revés con algo pintado justo detrás (aviso menor: se ve lo de debajo por el borde)
- Vuelta_l: 1.03 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 15°, giro 90°)
- Vuelta_r: 0.57 % (peor en WalkFWD_InPlace_NoWeapon@10.3, desde abajo a 15°, giro 270°)