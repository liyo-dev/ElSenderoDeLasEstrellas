# Chequeo de choques — posadero.blend
**PASA (22 avisos menores: mirar las fotos)**

Poses revisadas: 97 (WalkFWD_InPlace_NoWeapon, MoveFWD_Normal_InPlace_NoWeapon, Idle_Normal_NoWeapon, InteractWithPeople_NoWeapon, Greeting01_NoWeapon, Victory_NoWeapon, Sleeping_NoWeapon + brazos_bajados, mano_al_frente, brazos_cruzados, brazos_arriba, manos_juntas, sentado, cabeza_arriba, cabeza_abajo, cabeza_girada, tronco_adelante, tronco_atras, tronco_girado, tumbado)

## Tela que se rasga (aristas estiradas)
- Body31_Posadero:Pernera_l (+0.11,0.27): 8 (peor en MoveFWD_Normal_InPlace_NoWeapon@9.0)
- Body31_Posadero:Pernera_r (-0.11,0.27): 6 (peor en MoveFWD_Normal_InPlace_NoWeapon@1.0)
- Body31_Posadero:CintaDelantal (-0.00,0.45): 4 (peor en tronco_atras)
- Body31_Posadero:Vuelta_r (-0.29,0.63): 3 (peor en Victory_NoWeapon@46.8)

## Cuerpo que asoma por la ropa (puntos, por hueso)
- pelvis: 8 (peor en MoveFWD_Normal_InPlace_NoWeapon@5.0)
- upperarm_l: 3 (peor en tumbado)
- upperarm_r: 3 (peor en tumbado)

## Piezas que se meten dentro de otra (puntos)
- Body31_Posadero:Boton60 (+0.00,0.60): 279 (peor en InteractWithPeople_NoWeapon@106.0, dentro de cuerpo/index_01_l)
- Body31_Posadero:Boton52 (+0.00,0.52): 117 (peor en InteractWithPeople_NoWeapon@71.0, dentro de cuerpo/thumb_01_l)
- Body31_Posadero:Puntera_r (-0.12,0.04): 53 (peor en MoveFWD_Normal_InPlace_NoWeapon@15.7, dentro de cuerpo/calf_r)
- Body31_Posadero:Chaleco (-0.00,0.58): 47 (peor en tronco_adelante, dentro de CintaDelantal (-0.00,0.45))
- Body31_Posadero:CintaDelantal (-0.00,0.45): 45 (peor en MoveFWD_Normal_InPlace_NoWeapon@3.7, dentro de cuerpo/upperarm_r)
- Body31_Posadero:Delantal (+0.00,0.34): 39 (peor en Sleeping_NoWeapon@17.7, dentro de cuerpo/lowerarm_r)
- Body31_Posadero:Suela_l (+0.12,0.01): 34 (peor en MoveFWD_Normal_InPlace_NoWeapon@6.3, dentro de cuerpo/foot_l)
- Body31_Posadero:Suela_r (-0.12,0.01): 33 (peor en MoveFWD_Normal_InPlace_NoWeapon@14.3, dentro de cuerpo/foot_r)
- Body31_Posadero:BordeChaleco1 (+0.08,0.58): 19 (peor en InteractWithPeople_NoWeapon@123.5, dentro de cuerpo/lowerarm_l)
- Body31_Posadero:BordeChaleco-1 (-0.08,0.58): 13 (peor en InteractWithPeople_NoWeapon@176.0, dentro de cuerpo/index_01_l)
- Body31_Posadero:Manga_r (-0.22,0.64): 13 (peor en Victory_NoWeapon@34.3, dentro de Hombro_r (-0.16,0.64))
- Body31_Posadero:Manga_l (+0.22,0.64): 10 (peor en Sleeping_NoWeapon@1.0, dentro de Hombro_l (+0.17,0.64))

## Piezas que se separan (cm)
- nada

## Tela tendida sobre otra prenda a menos de 3 mm: parpadea (puntos)
- Body31_Posadero:Hombro_l (+0.17,0.64): 40 (peor en brazos_arriba, sobre Manga_l (+0.22,0.64))
- Body31_Posadero:Hombro_r (-0.16,0.64): 39 (peor en brazos_arriba, sobre Manga_r (-0.22,0.64))

## Cabeza que se hunde en la ropa al moverla (puntos de la cabeza)
- nada

## Piel propia que se mete en la ropa (puntos: carne atravesada por la tela; las manos solo avisan)
- Body31_Posadero:Cuerpo (-0.00,0.63): 9 (peor en Idle_Normal_NoWeapon@129.3, dentro de Hombro_l (+0.17,0.64))

## Pelo que sale por fuera del gorro o sombrero (puntos)
- nada
