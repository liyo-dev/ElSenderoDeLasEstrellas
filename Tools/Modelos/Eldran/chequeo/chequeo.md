# Chequeo de choques — eldran.blend
**PASA (25 avisos menores: mirar las fotos)**

Poses revisadas: 98 (WalkFWD_InPlace_NoWeapon, MoveFWD_Normal_InPlace_NoWeapon, Idle_Normal_NoWeapon, InteractWithPeople_NoWeapon, Greeting01_NoWeapon, Victory_NoWeapon, Sleeping_NoWeapon + brazos_bajados, mano_al_frente, brazos_cruzados, brazos_arriba, manos_juntas, sentado, sentado_hablando, cabeza_arriba, cabeza_abajo, cabeza_girada, tronco_adelante, tronco_atras, tronco_girado, tumbado)

## Tela que se rasga (aristas estiradas)
- Body26_AbrigoDeMago:Cinturon (-0.00,0.45): 3 (peor en tronco_atras)
- Body26_AbrigoDeMago:Pernera_l (+0.11,0.28): 2 (peor en MoveFWD_Normal_InPlace_NoWeapon@9.0)
- Body26_AbrigoDeMago:Bota_l (+0.12,0.08): 2 (peor en MoveFWD_Normal_InPlace_NoWeapon@10.3)
- Body26_AbrigoDeMago:Bota_r (-0.12,0.08): 2 (peor en Sleeping_NoWeapon@17.7)

## Cuerpo que asoma por la ropa (puntos, por hueso)
- upperarm_l: 7 (peor en InteractWithPeople_NoWeapon@71.0)
- pelvis: 7 (peor en MoveFWD_Normal_InPlace_NoWeapon@5.0)
- upperarm_r: 6 (peor en InteractWithPeople_NoWeapon@123.5)
- lowerarm_r: 5 (peor en Greeting01_NoWeapon@21.8)
- lowerarm_l: 3 (peor en WalkFWD_InPlace_NoWeapon@1.0)

## Piezas que se meten dentro de otra (puntos)
- Body26_AbrigoDeMago:Boton58 (+0.08,0.58): 314 (peor en manos_juntas, dentro de cuerpo/thumb_01_l)
- Body26_AbrigoDeMago:Boton51 (+0.08,0.51): 314 (peor en InteractWithPeople_NoWeapon@141.0, dentro de cuerpo/lowerarm_l)
- Body26_AbrigoDeMago:Boton65 (+0.08,0.65): 280 (peor en InteractWithPeople_NoWeapon@123.5, dentro de cuerpo/index_02_r)
- Body26_AbrigoDeMago:Hebilla (+0.00,0.45): 146 (peor en InteractWithPeople_NoWeapon@141.0, dentro de cuerpo/hand_l)
- Body26_AbrigoDeMago:Codera_r (-0.31,0.63): 68 (peor en mano_al_frente, dentro de cuerpo/upperarm_r)
- Body26_AbrigoDeMago:Codera_l (+0.31,0.63): 63 (peor en mano_al_frente, dentro de cuerpo/upperarm_l)
- Body26_AbrigoDeMago:Solapa (+0.04,0.58): 56 (peor en InteractWithPeople_NoWeapon@193.5, dentro de cuerpo/hand_l)
- Body26_AbrigoDeMago:Cinturon (-0.00,0.45): 40 (peor en brazos_bajados, dentro de cuerpo/upperarm_l)
- Body26_AbrigoDeMago:Puntera_r (-0.12,0.03): 32 (peor en MoveFWD_Normal_InPlace_NoWeapon@15.7, dentro de cuerpo/calf_r)
- Body26_AbrigoDeMago:Manga_r (-0.30,0.63): 21 (peor en Victory_NoWeapon@38.5, dentro de cuerpo/upperarm_r)
- Body26_AbrigoDeMago:Manga_l (+0.30,0.63): 16 (peor en Sleeping_NoWeapon@1.0, dentro de cuerpo/upperarm_l)

## Piezas que se separan (cm)
- nada

## Tela tendida sobre otra prenda a menos de 3 mm: parpadea (puntos)
- Body26_AbrigoDeMago:Hombro_l (+0.17,0.64): 39 (peor en brazos_arriba, sobre Manga_l (+0.30,0.63))

## Cabeza que se hunde en la ropa al moverla (puntos de la cabeza)
- Body26_AbrigoDeMago:Bufanda (-0.00,0.72): 4 (peor en Idle_Normal_NoWeapon@36.0)
- Body26_AbrigoDeMago:Cuello (-0.00,0.75): 3 (peor en Victory_NoWeapon@42.7)

## Piel propia que se mete en la ropa (puntos: carne atravesada por la tela; las manos solo avisan)
- Body26_AbrigoDeMago:Cuerpo (-0.61,0.62): 18 (peor en InteractWithPeople_NoWeapon@158.5, dentro de Bufanda (-0.00,0.72))
- Body26_AbrigoDeMago:Cuerpo (-0.00,0.63): 9 (peor en tronco_girado, dentro de Hombro_l (+0.17,0.64))

## Pelo que sale por fuera del gorro o sombrero (puntos)
- nada
