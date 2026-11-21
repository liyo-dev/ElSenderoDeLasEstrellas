# Chequeo de choques — selene.blend
**PASA (20 avisos menores: mirar las fotos)**

Poses revisadas: 97 (WalkFWD_InPlace_NoWeapon, MoveFWD_Normal_InPlace_NoWeapon, Idle_Normal_NoWeapon, InteractWithPeople_NoWeapon, Greeting01_NoWeapon, Victory_NoWeapon, Sleeping_NoWeapon + brazos_bajados, mano_al_frente, brazos_cruzados, brazos_arriba, manos_juntas, sentado, cabeza_arriba, cabeza_abajo, cabeza_girada, tronco_adelante, tronco_atras, tronco_girado, tumbado)

## Tela que se rasga (aristas estiradas)
- Body37_Viajera:Hombro_l (+0.17,0.64): 10 (peor en MoveFWD_Normal_InPlace_NoWeapon@5.0)
- Body37_Viajera:Hombro_r (-0.17,0.64): 10 (peor en brazos_bajados)
- Body37_Viajera:Tunica (-0.00,0.49): 8 (peor en tronco_atras)
- Body37_Viajera:Manga_l (+0.34,0.63): 7 (peor en InteractWithPeople_NoWeapon@141.0)
- Body37_Viajera:Manga_r (-0.34,0.63): 6 (peor en InteractWithPeople_NoWeapon@71.0)

## Cuerpo que asoma por la ropa (puntos, por hueso)
- pelvis: 7 (peor en sentado)
- upperarm_l: 6 (peor en Sleeping_NoWeapon@7.7)
- upperarm_r: 4 (peor en InteractWithPeople_NoWeapon@141.0)

## Piezas que se meten dentro de otra (puntos)
- Body37_Viajera:Tunica (-0.00,0.49): 64 (peor en brazos_bajados, dentro de cuerpo/lowerarm_l)
- Body37_Viajera:Bajo (+0.00,0.30): 46 (peor en brazos_bajados, dentro de cuerpo/lowerarm_l)
- Body37_Viajera:BrocheLuna (+0.07,0.60): 46 (peor en InteractWithPeople_NoWeapon@88.5, dentro de cuerpo/index_02_r)
- Body37_Viajera:Puntera_r (-0.12,0.03): 34 (peor en MoveFWD_Normal_InPlace_NoWeapon@15.7, dentro de cuerpo/calf_r)
- Body37_Viajera:HebillaLuna (-0.00,0.44): 33 (peor en InteractWithPeople_NoWeapon@141.0, dentro de cuerpo/hand_l)
- Body37_Viajera:Manga_r (-0.34,0.63): 31 (peor en Victory_NoWeapon@1.0, dentro de cuerpo/upperarm_r)
- Body37_Viajera:Manga_l (+0.34,0.63): 28 (peor en Sleeping_NoWeapon@14.3, dentro de cuerpo/upperarm_l)
- Body37_Viajera:Cinturon (-0.00,0.45): 21 (peor en MoveFWD_Normal_InPlace_NoWeapon@5.0, dentro de cuerpo/lowerarm_l)
- Body37_Viajera:CapaBajo (-0.00,0.40): 20 (peor en tronco_girado, dentro de cuerpo/lowerarm_l)
- Hair24_Selene:PeloRubioOscuro@spine_03 (+0.00,0.73): 8 (peor en Sleeping_NoWeapon@1.0, dentro de PeloRubio@head (+0.00,1.13))

## Piezas que se separan (cm)
- nada

## Tela tendida sobre otra prenda a menos de 3 mm: parpadea (puntos)
- Body37_Viajera:PunoForro_r (-0.43,0.63): 28 (peor en InteractWithPeople_NoWeapon@53.5, sobre Manga_r (-0.34,0.63))
- Body37_Viajera:PunoForro_l (+0.43,0.63): 25 (peor en WalkFWD_InPlace_NoWeapon@15.0, sobre BocaManga_l (+0.44,0.63))

## Cabeza que se hunde en la ropa al moverla (puntos de la cabeza)
- nada

## Piel propia que se mete en la ropa (puntos: carne atravesada por la tela; las manos solo avisan)
- nada

## Pelo que sale por fuera del gorro o sombrero (puntos)
- nada
