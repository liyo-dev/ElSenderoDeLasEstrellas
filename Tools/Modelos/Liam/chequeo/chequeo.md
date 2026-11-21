# Chequeo de choques — liam_chk.blend
**PASA (27 avisos menores: mirar las fotos)**

Poses revisadas: 55 (WalkFWD_InPlace_NoWeapon, MoveFWD_Normal_InPlace_NoWeapon, Idle_Normal_NoWeapon, InteractWithPeople_NoWeapon, Greeting01_NoWeapon, Victory_NoWeapon, Sleeping_NoWeapon + brazos_bajados, mano_al_frente, brazos_cruzados, brazos_arriba, manos_juntas, sentado, cabeza_arriba, cabeza_abajo, cabeza_girada, tronco_adelante, tronco_atras, tronco_girado, tumbado)

## Tela que se rasga (aristas estiradas)
- Body22_Tunica:Bota_l (+0.12,0.08): 2 (peor en Sleeping_NoWeapon@1.0)
- Body22_Tunica:Bota_r (-0.12,0.08): 2 (peor en Sleeping_NoWeapon@21.0)
- Body22_Tunica:Hebilla (+0.05,0.45): 2 (peor en tronco_atras)
- Body22_Tunica:Pernera_l (+0.11,0.27): 1 (peor en MoveFWD_Normal_InPlace_NoWeapon@9.0)
- Body22_Tunica:Cinturon (-0.00,0.45): 1 (peor en tronco_atras)

## Cuerpo que asoma por la ropa (puntos, por hueso)
- upperarm_r: 10 (peor en Greeting01_NoWeapon@34.3)
- upperarm_l: 8 (peor en InteractWithPeople_NoWeapon@141.0)
- lowerarm_r: 5 (peor en MoveFWD_Normal_InPlace_NoWeapon@9.0)
- lowerarm_l: 4 (peor en WalkFWD_InPlace_NoWeapon@1.0)
- pelvis: 3 (peor en MoveFWD_Normal_InPlace_NoWeapon@3.7)

## Piezas que se meten dentro de otra (puntos)
- Body22_Tunica:Hebilla (+0.05,0.45): 139 (peor en InteractWithPeople_NoWeapon@176.0, dentro de cuerpo/lowerarm_l)
- Body22_Tunica:Bolsita (-0.16,0.42): 66 (peor en Sleeping_NoWeapon@1.0, dentro de cuerpo/lowerarm_r)
- Body22_Tunica:Bajo (-0.00,0.27): 61 (peor en tumbado, dentro de Tunica (+0.00,0.51))
- Body22_Tunica:Puntera_r (-0.12,0.03): 48 (peor en MoveFWD_Normal_InPlace_NoWeapon@14.3, dentro de Bota_r (-0.12,0.08))
- Body22_Tunica:Hombro_l (+0.17,0.64): 48 (peor en brazos_arriba, dentro de cuerpo/upperarm_l)
- Body22_Tunica:Suela_l (+0.12,0.01): 42 (peor en MoveFWD_Normal_InPlace_NoWeapon@6.3, dentro de cuerpo/foot_l)
- Body22_Tunica:Suela_r (-0.12,0.01): 42 (peor en MoveFWD_Normal_InPlace_NoWeapon@14.3, dentro de cuerpo/foot_r)
- Body22_Tunica:Hombro_r (-0.17,0.64): 42 (peor en brazos_arriba, dentro de cuerpo/upperarm_r)
- Body22_Tunica:Puntera_l (+0.12,0.03): 40 (peor en MoveFWD_Normal_InPlace_NoWeapon@6.3, dentro de Bota_l (+0.12,0.08))
- Body22_Tunica:Cruce (-0.01,0.60): 33 (peor en InteractWithPeople_NoWeapon@106.0, dentro de cuerpo/index_01_r)
- Body22_Tunica:Cinturon (-0.00,0.45): 28 (peor en brazos_bajados, dentro de cuerpo/lowerarm_l)
- Body22_Tunica:Cuello (-0.00,0.73): 26 (peor en brazos_arriba, dentro de cuerpo/upperarm_r)
- Body22_Tunica:Manga_r (-0.30,0.63): 15 (peor en Victory_NoWeapon@34.3, dentro de cuerpo/upperarm_r)

## Piezas que se separan (cm)
- nada

## Tela tendida sobre otra prenda a menos de 3 mm: parpadea (puntos)
- Body22_Tunica:Hombro_l (+0.17,0.64): 56 (peor en cabeza_arriba, sobre Manga_l (+0.30,0.63))
- Body22_Tunica:Hombro_r (-0.17,0.64): 56 (peor en cabeza_arriba, sobre Manga_r (-0.30,0.63))
- Body22_Tunica:Bajo (-0.00,0.27): 55 (peor en mano_al_frente, sobre Tunica (+0.00,0.51))

## Cabeza que se hunde en la ropa al moverla (puntos de la cabeza)
- Body22_Tunica:Cuello (-0.00,0.73): 5 (peor en Victory_NoWeapon@1.0)

## Piel propia que se mete en la ropa (puntos: carne atravesada por la tela; las manos solo avisan)
- nada

## Pelo que sale por fuera del gorro o sombrero (puntos)
- nada
