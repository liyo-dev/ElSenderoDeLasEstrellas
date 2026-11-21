# Chequeo de choques — will.blend
**PASA (24 avisos menores: mirar las fotos)**

Poses revisadas: 97 (WalkFWD_InPlace_NoWeapon, MoveFWD_Normal_InPlace_NoWeapon, Idle_Normal_NoWeapon, InteractWithPeople_NoWeapon, Greeting01_NoWeapon, Victory_NoWeapon, Sleeping_NoWeapon + brazos_bajados, mano_al_frente, brazos_cruzados, brazos_arriba, manos_juntas, sentado, cabeza_arriba, cabeza_abajo, cabeza_girada, tronco_adelante, tronco_atras, tronco_girado, tumbado)

## Tela que se rasga (aristas estiradas)
- Body23_Aventurero:Bota_l (+0.12,0.12): 3 (peor en MoveFWD_Normal_InPlace_NoWeapon@9.0)
- Body23_Aventurero:RibeteManga_r (-0.27,0.63): 3 (peor en Victory_NoWeapon@30.2)
- Body23_Aventurero:Bota_r (-0.12,0.12): 2 (peor en MoveFWD_Normal_InPlace_NoWeapon@1.0)
- Body23_Aventurero:Pernera_l (+0.10,0.31): 1 (peor en MoveFWD_Normal_InPlace_NoWeapon@9.0)
- Body23_Aventurero:RibeteManga_l (+0.27,0.63): 1 (peor en brazos_arriba)

## Cuerpo que asoma por la ropa (puntos, por hueso)
- upperarm_r: 13 (peor en Greeting01_NoWeapon@34.3)
- upperarm_l: 10 (peor en Sleeping_NoWeapon@17.7)

## Piezas que se meten dentro de otra (puntos)
- Body23_Aventurero:Puntera_r (-0.12,0.03): 72 (peor en MoveFWD_Normal_InPlace_NoWeapon@15.7, dentro de Bota_r (-0.12,0.12))
- Body23_Aventurero:RibeteManga_r (-0.27,0.63): 71 (peor en tumbado, dentro de cuerpo/upperarm_r)
- Body23_Aventurero:RibeteManga_l (+0.27,0.63): 69 (peor en Victory_NoWeapon@9.3, dentro de cuerpo/upperarm_l)
- Body23_Aventurero:Puntera_l (+0.12,0.03): 44 (peor en MoveFWD_Normal_InPlace_NoWeapon@6.3, dentro de Bota_l (+0.12,0.12))
- Body23_Aventurero:Suela_r (-0.12,0.01): 42 (peor en MoveFWD_Normal_InPlace_NoWeapon@14.3, dentro de cuerpo/foot_r)
- Body23_Aventurero:Suela_l (+0.12,0.01): 42 (peor en MoveFWD_Normal_InPlace_NoWeapon@6.3, dentro de cuerpo/foot_l)
- Body23_Aventurero:BajoCamiseta (-0.00,0.38): 34 (peor en brazos_bajados, dentro de piel/Brazo_l)
- Body23_Aventurero:Cinturon (-0.00,0.45): 33 (peor en brazos_bajados, dentro de piel/Brazo_l)
- Body23_Aventurero:PanueloPico (+0.00,0.66): 22 (peor en InteractWithPeople_NoWeapon@123.5, dentro de cuerpo/thumb_01_r)
- Body23_Aventurero:Manga_r (-0.22,0.63): 13 (peor en Victory_NoWeapon@26.0, dentro de cuerpo/upperarm_r)
- Body23_Aventurero:Manga_l (+0.22,0.63): 12 (peor en Sleeping_NoWeapon@14.3, dentro de piel/Brazo_l)

## Piezas que se separan (cm)
- nada

## Tela tendida sobre otra prenda a menos de 3 mm: parpadea (puntos)
- Body23_Aventurero:Hombro_r (-0.16,0.64): 46 (peor en Greeting01_NoWeapon@13.5, sobre Manga_r (-0.22,0.63))
- Body23_Aventurero:Hombro_l (+0.16,0.64): 43 (peor en brazos_arriba, sobre Manga_l (+0.22,0.63))

## Cabeza que se hunde en la ropa al moverla (puntos de la cabeza)
- Body23_Aventurero:Camiseta (+0.00,0.61): 4 (peor en tumbado)
- Body23_Aventurero:PanueloCuello (-0.00,0.74): 3 (peor en cabeza_arriba)

## Piel propia que se mete en la ropa (puntos: carne atravesada por la tela)
- Body23_Aventurero:Cuerpo (+0.61,0.62): 66 (peor en InteractWithPeople_NoWeapon@88.5, dentro de Camiseta (+0.00,0.61))
- Body23_Aventurero:Cuerpo (-0.61,0.62): 37 (peor en InteractWithPeople_NoWeapon@106.0, dentro de Camiseta (+0.00,0.61))

## Pelo que sale por fuera del gorro o sombrero (puntos)
- nada
