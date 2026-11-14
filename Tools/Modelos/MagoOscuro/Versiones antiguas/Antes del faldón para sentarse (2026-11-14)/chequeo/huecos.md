# Chequeo de huecos vistos desde abajo — mago_oscuro.blend
**PASA (6 vistas con algún hueco pequeño: mirar las fotos)**

Poses: reposo, WalkFWD_InPlace_NoWeapon@1.0, WalkFWD_InPlace_NoWeapon@10.3, WalkFWD_InPlace_NoWeapon@19.7. Vistas por pose: 25 (desde abajo a 88, 65, 40, 15°). Hueco = la cámara ve el revés de una cara, que en Unity no se pinta.

Peor vista: WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 88°, giro 0°: 0.5 % de lo que se ve del muñeco es hueco.

## Piezas por las que se ve hueco: el fondo o el interior vacío (peor parte de una vista)
- BajoLuz: 0.41 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 88°, giro 0°)
- Hair19_MagoOscuro: 0.16 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 88°, giro 0°)
- Capa: 0.03 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 65°, giro 135°)
- Tunica: 0.03 % (peor en WalkFWD_InPlace_NoWeapon@1.0, desde abajo a 15°, giro 45°)
- Cuello: 0.02 % (peor en reposo, desde abajo a 40°, giro 225°)
- Manga_l: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 40°, giro 315°)
- Cuerpo: 0.01 % (peor en reposo, desde abajo a 88°, giro 0°)
- Head01_Male: 0.01 % (peor en reposo, desde abajo a 65°, giro 45°)
- PuntaCapucha: 0.01 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 15°, giro 0°)

## Manos con los brazos caídos (idle): parte que se ve desde algún lado
- mano izquierda: 42 % (mínimo 35 %)
- mano derecha: 60 % (mínimo 35 %)

## Muslos sentado, de frente: parte que se ve
- muslo izquierdo: 86 % (mínimo 15 %)
- muslo derecho: 89 % (mínimo 15 %)

## Revés con algo pintado justo detrás (aviso menor: se ve lo de debajo por el borde)
- BajoLuz: 0.26 % (peor en WalkFWD_InPlace_NoWeapon@19.7, desde abajo a 88°, giro 0°)