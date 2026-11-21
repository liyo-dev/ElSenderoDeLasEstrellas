# Chequeo de huecos vistos desde abajo — victoria.blend
**PASA**

Poses: reposo, WalkFWD_InPlace_NoWeapon@1.0, WalkFWD_InPlace_NoWeapon@10.3, WalkFWD_InPlace_NoWeapon@19.7. Vistas por pose: 25 (desde abajo a 88, 65, 40, 15°). Hueco = la cámara ve el revés de una cara, que en Unity no se pinta.

Peor vista: WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 65°, giro 135°: 0.1 % de lo que se ve del muñeco es hueco.

## Piezas por las que se ve hueco: el fondo o el interior vacío (peor parte de una vista)
- Head01_Male: 0.15 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 65°, giro 135°)
- Cuerpo: 0.03 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 15°, giro 90°)
- Falda: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 65°, giro 135°)
- DelantalFalda: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 65°, giro 270°)
- Media_r: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 40°, giro 135°)
- Cuello: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 15°, giro 90°)
- Manga_r: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 15°, giro 0°)
- Blusa: 0.01 % (peor en reposo, desde abajo a 15°, giro 180°)

## Manos con los brazos caídos (idle): parte que se ve desde algún lado
- mano izquierda: 45 % (mínimo 35 %)
- mano derecha: 73 % (mínimo 35 %)

## Muslos sentado, de frente: parte que se ve
- muslo izquierdo: 54 % (mínimo 15 %)
- muslo derecho: 53 % (mínimo 15 %)

## Revés con algo pintado justo detrás (aviso menor: se ve lo de debajo por el borde)
- Cuello: 0.18 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 15°, giro 90°)