# Quién mueve la cabeza

Grados. clip = pose del Animator frente al reposo del avatar; quieta = CabezaQuietaAlHablar; resto = código que corre después (mirada, sentado, gestos); total = pose final frente al reposo; cuello = lo mismo del cuello. t = Time.time.

| Personaje | peor total (t, clip/quieta/resto, cuello, estados) | peor del clip (t, estados) | peor de quieta (t) | peor del resto (t, estados) |
|---|---|---|---|---|
| NPC_Aldeano_01 | 56 (t 166,4; 11/0/46; cuello 13; Base Layer:HumanM@Idle03) | 11 (t 165,8; Base Layer:HumanM@Idle03) | 0 (t 31,5) | 46 (t 166,4; Base Layer:HumanM@Idle03) |
| NPC_Aldeano_07 | 55 (t 166,8; 11/0/44; cuello 13; Base Layer:HumanM@Idle03) | 11 (t 166,4; Base Layer:HumanM@Idle03) | 0 (t 31,5) | 44 (t 166,8; Base Layer:HumanM@Idle03) |
| NPC_Aldeano_05 | 46 (t 148,9; 1/0/47; cuello 16; Base Layer:InteractWithPeople_NoWeapon) | 15 (t 132,0; Base Layer:InteractWithPeople_NoWeapon) | 15 (t 132,0) | 47 (t 148,9; Base Layer:InteractWithPeople_NoWeapon) |
| NPC_Aldeano_08 | 43 (t 137,6; 17/0/26; cuello 8; Base Layer:InteractWithPeople_NoWeapon UpperBody:Gesto_Cheer02) | 17 (t 137,6; Base Layer:InteractWithPeople_NoWeapon UpperBody:Gesto_Cheer02) | 13 (t 138,7) | 31 (t 143,9; Base Layer:InteractWithPeople_NoWeapon UpperBody:Gesto_Laugh01) |
| NPC_Archimago | 43 (t 286,1; 20/0/23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar) | 20 (t 292,1; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar) | 12 (t 226,9) | 31 (t 142,2; Base Layer:Idle_Normal_NoWeapon) |
| NPC_Aldeano_02 | 42 (t 133,8; 11/0/31; cuello 7; Base Layer:Idle_Normal_NoWeapon) | 16 (t 91,6; Base Layer:InteractWithPeople_NoWeapon UpperBody:Gesto_Laugh01) | 13 (t 80,8) | 36 (t 141,4; Base Layer:InteractWithPeople_NoWeapon) |
| MadreDelValle | 41 (t 127,2; 0/0/41; cuello 17; Base Layer:Idle_Normal_NoWeapon) | 10 (t 100,2; Base Layer:Idle_Normal_NoWeapon) | 4 (t 100,2) | 41 (t 126,6; Base Layer:Idle_Normal_NoWeapon) |
| AbuelaDelMirador | 39 (t 133,7; 8/0/32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:HumanM@HandClap01) | 11 (t 129,2; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado) | 0 (t 31,5) | 32 (t 133,6; Base Layer:Idle_Normal_NoWeapon UpperBody:HumanM@HandClap01) |
| NPC_Aldeano_03 | 38 (t 134,9; 2/9/27; cuello 8; Base Layer:InteractWithPeople_NoWeapon) | 16 (t 94,6; Base Layer:InteractWithPeople_NoWeapon Cabeza:HumanM@HeadShake01) | 13 (t 75,9) | 34 (t 139,9; Base Layer:InteractWithPeople_NoWeapon) |
| VecinoDelMirador | 36 (t 142,1; 11/0/25; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado) | 11 (t 142,1; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado) | 0 (t 31,5) | 25 (t 143,2; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado) |
| VecinaDelMirador | 32 (t 137,4; 11/0/21; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado) | 11 (t 113,8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado) | 0 (t 31,5) | 26 (t 127,1; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado) |
| NPC_Aldeano_09 | 32 (t 137,0; 0/0/32; cuello 11; Base Layer:InteractWithPeople_NoWeapon) | 16 (t 83,9; Base Layer:InteractWithPeople_NoWeapon UpperBody:Gesto_Laugh01) | 13 (t 71,6) | 32 (t 137,0; Base Layer:InteractWithPeople_NoWeapon) |
| NPC_Aldeano_06 | 32 (t 148,5; 0/0/32; cuello 12; Base Layer:HumanM@Opening01 - Loop) | 15 (t 115,4; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Cheer01) | 13 (t 106,8) | 32 (t 148,5; Base Layer:HumanM@Opening01 - Loop) |
| NPC_Liora | 31 (t 142,1; 0/0/31; cuello 8; Base Layer:Idle_Normal_NoWeapon) | 16 (t 67,1; Base Layer:InteractWithPeople_NoWeapon Cabeza:HumanM@HeadShake01) | 13 (t 71,3) | 31 (t 142,1; Base Layer:Idle_Normal_NoWeapon) |
| NPC_Aldeano_10 | 30 (t 134,3; 4/0/32; cuello 18; Base Layer:Idle_Normal_NoWeapon) | 11 (t 253,6; Base Layer:Idle_Normal_NoWeapon UpperBody:HumanM@Beg01 - Loop) | 0 (t 31,5) | 32 (t 134,3; Base Layer:Idle_Normal_NoWeapon) |
| PanaderaDelValle | 29 (t 161,9; 1/0/29; cuello 8; Base Layer:InteractWithPeople_NoWeapon) | 3 (t 201,0; Base Layer:MoveFWD_Normal_RM_NoWeapon) | 0 (t 31,5) | 29 (t 161,9; Base Layer:InteractWithPeople_NoWeapon) |
| NPC_Aldeano_04 | 28 (t 152,6; 4/0/25; cuello 7; Base Layer:Idle_Normal_NoWeapon) | 10 (t 138,0; Base Layer:Idle_Normal_NoWeapon) | 0 (t 31,5) | 28 (t 137,8; Base Layer:Idle_Normal_NoWeapon) |
| LucasDelValle | 27 (t 161,9; 0/0/27; cuello 9; Base Layer:Idle_Normal_NoWeapon) | 3 (t 189,9; Base Layer:WalkFWD_RM_NoWeapon) | 0 (t 31,5) | 27 (t 161,9; Base Layer:Idle_Normal_NoWeapon) |
| NPC_MagoOscuro | 24 (t 253,4; 1/0/25; cuello 5; Base Layer:JumpAir_InPlace_NoWeapon UpperBody:FoundSomething_NoWeapon) | 21 (t 240,8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Laugh_Carcajada) | 9 (t 221,0) | 25 (t 253,4; Base Layer:JumpAir_InPlace_NoWeapon UpperBody:FoundSomething_NoWeapon) |
| Anselmo | 22 (t 269,5; 11/0/12; cuello 17; Base Layer:HumanM@SitGround01 - Loop) | 11 (t 66,0; Base Layer:HumanM@SitGround01 - Loop) | 0 (t 30,3) | 12 (t 258,7; Base Layer:HumanM@SitGround01 - Loop) |
| _MagoDelDeseo | 21 (t 49,6; 21/0/0; cuello 11; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Laugh_Carcajada) | 21 (t 49,6; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Laugh_Carcajada) | 0 (t 32,5) | 0 (t 32,5; Base Layer:WalkFWD_RM_NoWeapon) |
| Benito | 19 (t 196,6; 11/0/8; cuello 6; Base Layer:HumanM@SitHigh01 - Loop) | 11 (t 66,0; Base Layer:HumanM@SitHigh01 - Loop) | 0 (t 30,3) | 8 (t 195,3; Base Layer:HumanM@SitHigh01 - Loop) |
| Rosario | 19 (t 196,6; 11/0/8; cuello 6; Base Layer:HumanM@SitHigh01 - Loop) | 11 (t 66,0; Base Layer:HumanM@SitHigh01 - Loop) | 0 (t 30,3) | 8 (t 225,8; Base Layer:HumanM@SitHigh01 - Loop) |
| Eusebio | 19 (t 193,8; 11/0/8; cuello 6; Base Layer:HumanM@SitHigh01 - Loop) | 11 (t 66,0; Base Layer:HumanM@SitHigh01 - Loop) | 0 (t 30,3) | 8 (t 198,6; Base Layer:HumanM@SitHigh01 - Loop) |
| PortadaDelMenu/WILL | 11 (t 12,3; 11/0/0; cuello 5; Base Layer:Postura_SitSwing_Loop) | 11 (t 12,3; Base Layer:Postura_SitSwing_Loop) | 0 (t 0,0) | 0 (t 0,0; Base Layer:Postura_SitSwing_Loop) |
| CharacterStage/RetratoVivo_Liam | 6 (t 29,3; 6/0/0; cuello 21; Base Layer:SprintFWD_Battle_InPlace_NoWeapon) | 6 (t 29,3; Base Layer:SprintFWD_Battle_InPlace_NoWeapon) | 0 (t 28,4) | 0 (t 28,4; Base Layer:SprintFWD_Battle_InPlace_NoWeapon) |
| CharacterStage/RetratoVivo_Estela | 6 (t 29,3; 6/0/0; cuello 21; Base Layer:SprintFWD_Battle_InPlace_NoWeapon) | 6 (t 29,3; Base Layer:SprintFWD_Battle_InPlace_NoWeapon) | 0 (t 28,4) | 0 (t 28,4; Base Layer:SprintFWD_Battle_InPlace_NoWeapon) |
| CharacterStage/RetratoVivo_Will | 6 (t 29,3; 6/0/0; cuello 21; Base Layer:SprintFWD_Battle_InPlace_NoWeapon) | 6 (t 29,3; Base Layer:SprintFWD_Battle_InPlace_NoWeapon) | 0 (t 28,4) | 0 (t 28,4; Base Layer:SprintFWD_Battle_InPlace_NoWeapon) |
| EstudioDeRetratos/RetratoVivo_Will | 2 (t 351,9; 2/0/0; cuello 4; Base Layer:Idle_Normal_NoWeapon) | 2 (t 351,9; Base Layer:Idle_Normal_NoWeapon) | 0 (t 309,0) | 0 (t 309,0; Base Layer:Idle_Normal_NoWeapon) |
| Fermin | 2 (t 293,1; 2/0/0; cuello 4; Base Layer:Idle_Normal_NoWeapon) | 2 (t 293,1; Base Layer:Idle_Normal_NoWeapon) | 0 (t 30,3) | 0 (t 30,3; Base Layer:Idle_Normal_NoWeapon) |
| Victoria | 2 (t 68,9; 2/0/0; cuello 4; Base Layer:Idle_Normal_NoWeapon) | 2 (t 68,9; Base Layer:Idle_Normal_NoWeapon) | 0 (t 30,3) | 0 (t 30,3; Base Layer:Idle_Normal_NoWeapon) |
| Aldona | 2 (t 115,6; 2/0/0; cuello 4; Base Layer:Idle_Normal_NoWeapon) | 2 (t 115,6; Base Layer:Idle_Normal_NoWeapon) | 0 (t 30,3) | 0 (t 30,3; Base Layer:Idle_Normal_NoWeapon) |
| EXPEDICION_FLASHBACK/Actor_EldranJoven | 2 (t 64,3; 2/0/0; cuello 4; Base Layer:Idle_Normal_NoWeapon) | 2 (t 64,3; Base Layer:Idle_Normal_NoWeapon) | 0 (t 31,1) | 0 (t 31,1; Base Layer:Idle_Normal_NoWeapon) |
| Tabernera | 2 (t 282,9; 2/0/0; cuello 4; Base Layer:Idle_Normal_NoWeapon) | 2 (t 282,9; Base Layer:Idle_Normal_NoWeapon) | 0 (t 30,3) | 0 (t 30,3; Base Layer:Idle_Normal_NoWeapon) |
| Engracia | 2 (t 64,4; 2/0/0; cuello 4; Base Layer:Idle_Normal_NoWeapon) | 2 (t 64,4; Base Layer:Idle_Normal_NoWeapon) | 0 (t 30,3) | 0 (t 30,3; Base Layer:Idle_Normal_NoWeapon) |
| Eldran | 2 (t 64,2; 2/0/0; cuello 4; Base Layer:Idle_Normal_NoWeapon) | 2 (t 64,2; Base Layer:Idle_Normal_NoWeapon) | 0 (t 30,3) | 0 (t 30,3; Base Layer:Idle_Normal_NoWeapon) |
| Tomasa | 2 (t 64,5; 2/0/0; cuello 4; Base Layer:Idle_Normal_NoWeapon) | 2 (t 64,5; Base Layer:Idle_Normal_NoWeapon) | 0 (t 30,3) | 0 (t 30,3; Base Layer:Idle_Normal_NoWeapon) |
| Oliver | 2 (t 64,2; 2/0/0; cuello 4; Base Layer:Idle_Normal_NoWeapon) | 2 (t 64,2; Base Layer:Idle_Normal_NoWeapon) | 0 (t 30,3) | 0 (t 30,3; Base Layer:Idle_Normal_NoWeapon) |
| Remedios | 2 (t 64,1; 2/0/0; cuello 4; Base Layer:Idle_Normal_NoWeapon) | 2 (t 64,1; Base Layer:Idle_Normal_NoWeapon) | 0 (t 30,3) | 0 (t 30,3; Base Layer:Idle_Normal_NoWeapon) |
| EXPEDICION_FLASHBACK/Actor_Selene | 2 (t 68,9; 2/0/0; cuello 4; Base Layer:Idle_Normal_NoWeapon) | 2 (t 68,9; Base Layer:Idle_Normal_NoWeapon) | 0 (t 31,1) | 0 (t 31,1; Base Layer:Idle_Normal_NoWeapon) |
| EXPEDICION_FLASHBACK/Actor_SilasJoven | 1 (t 31,1; 1/0/0; cuello 6; Base Layer:Idle_Normal_NoWeapon) | 1 (t 31,1; Base Layer:Idle_Normal_NoWeapon) | 0 (t 31,1) | 0 (t 31,1; Base Layer:Idle_Normal_NoWeapon) |
| CONFIG/_WILL | 0 (t 30,4; 0/0/0; cuello 10; Base Layer:Idle_Normal_NoWeapon) | 0 (t 30,4; Base Layer:Idle_Normal_NoWeapon) | 0 (t 30,3) | 0 (t 30,3; Base Layer:Idle_Normal_NoWeapon) |

## Momentos con la cabeza girada más de 35°

### NPC_Aldeano_01
- t 163,88: total 41 = clip 1, quieta 0, resto 40; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 164,14: total 44 = clip 1, quieta 0, resto 44; cuello 18; Base Layer:Idle_Normal_NoWeapon
- t 164,41: total 46 = clip 1, quieta 0, resto 45; cuello 18; Base Layer:Idle_Normal_NoWeapon
- t 164,67: total 46 = clip 1, quieta 0, resto 46; cuello 18; Base Layer:HumanM@Idle03
- t 164,93: total 46 = clip 1, quieta 0, resto 46; cuello 18; Base Layer:HumanM@Idle03
- t 165,19: total 46 = clip 1, quieta 0, resto 46; cuello 18; Base Layer:HumanM@Idle03
- t 165,45: total 46 = clip 1, quieta 0, resto 46; cuello 18; Base Layer:HumanM@Idle03
- t 165,72: total 56 = clip 11, quieta 0, resto 46; cuello 13; Base Layer:HumanM@Idle03
- t 165,99: total 56 = clip 11, quieta 0, resto 46; cuello 13; Base Layer:HumanM@Idle03
- t 166,28: total 56 = clip 11, quieta 0, resto 46; cuello 13; Base Layer:HumanM@Idle03
- t 166,54: total 56 = clip 11, quieta 0, resto 46; cuello 13; Base Layer:HumanM@Idle03
- t 166,80: total 56 = clip 11, quieta 0, resto 46; cuello 13; Base Layer:HumanM@Idle03
- t 167,05: total 47 = clip 1, quieta 0, resto 46; cuello 14; Base Layer:HumanM@Idle03
- t 167,31: total 46 = clip 0, quieta 0, resto 46; cuello 14; Base Layer:Idle_Normal_NoWeapon
- t 167,57: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 167,83: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 168,08: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 168,34: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 168,59: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 168,85: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 169,11: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 169,37: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 169,63: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 169,89: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 170,15: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 170,40: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 170,66: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 170,93: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 171,43: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 171,69: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 171,95: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 172,21: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 172,47: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 172,73: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 172,99: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 173,24: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 173,51: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 173,76: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 174,02: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 174,29: total 46 = clip 1, quieta 0, resto 46; cuello 13; Base Layer:Idle_Normal_NoWeapon
- …

### NPC_Aldeano_07
- t 164,67: total 40 = clip 0, quieta 0, resto 40; cuello 14; Base Layer:Idle_Normal_NoWeapon
- t 164,93: total 43 = clip 0, quieta 0, resto 43; cuello 15; Base Layer:Idle_Normal_NoWeapon
- t 165,19: total 44 = clip 0, quieta 0, resto 44; cuello 15; Base Layer:HumanM@Idle03
- t 165,45: total 44 = clip 0, quieta 0, resto 44; cuello 16; Base Layer:HumanM@Idle03
- t 165,72: total 55 = clip 11, quieta 0, resto 44; cuello 13; Base Layer:HumanM@Idle03
- t 165,99: total 55 = clip 11, quieta 0, resto 44; cuello 13; Base Layer:HumanM@Idle03
- t 166,28: total 55 = clip 11, quieta 0, resto 44; cuello 13; Base Layer:HumanM@Idle03
- t 166,54: total 55 = clip 11, quieta 0, resto 44; cuello 13; Base Layer:HumanM@Idle03
- t 166,80: total 55 = clip 11, quieta 0, resto 44; cuello 13; Base Layer:HumanM@Idle03
- t 167,05: total 55 = clip 11, quieta 0, resto 44; cuello 13; Base Layer:HumanM@Idle03
- t 167,31: total 53 = clip 9, quieta 0, resto 44; cuello 12; Base Layer:HumanM@Idle03
- t 167,57: total 44 = clip 0, quieta 0, resto 44; cuello 14; Base Layer:Idle_Normal_NoWeapon
- t 167,83: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 168,08: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 168,34: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 168,59: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 168,85: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 169,11: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 169,37: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 169,63: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 169,89: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 170,15: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 170,40: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 170,66: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 170,93: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 171,43: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 171,69: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 171,95: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 172,21: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 172,47: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 172,73: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 172,99: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 173,24: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 173,51: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 173,76: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 174,02: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 174,29: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 174,55: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 174,81: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- t 175,07: total 44 = clip 0, quieta 0, resto 44; cuello 13; Base Layer:Idle_Normal_NoWeapon
- …

### NPC_Aldeano_05
- t 132,37: total 37 = clip 15, quieta 0, resto 22; cuello 8; Base Layer:InteractWithPeople_NoWeapon UpperBody:Gesto_Cheer01
- t 132,88: total 37 = clip 13, quieta 0, resto 24; cuello 8; Base Layer:InteractWithPeople_NoWeapon UpperBody:Gesto_Cheer01
- t 133,15: total 35 = clip 11, quieta 0, resto 24; cuello 8; Base Layer:InteractWithPeople_NoWeapon UpperBody:Gesto_Cheer01
- t 145,50: total 41 = clip 1, quieta 0, resto 41; cuello 14; Base Layer:InteractWithPeople_NoWeapon
- t 145,75: total 44 = clip 1, quieta 0, resto 45; cuello 15; Base Layer:InteractWithPeople_NoWeapon
- t 146,03: total 46 = clip 1, quieta 0, resto 46; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 146,29: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 146,57: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 146,85: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 147,12: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 147,39: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon UpperBody:HumanM@Talk01
- t 147,66: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon UpperBody:HumanM@Talk01
- t 147,93: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 148,18: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 148,44: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 148,71: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 148,96: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 149,22: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 149,48: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 149,75: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 150,00: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 150,27: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 150,55: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 152,40: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 152,68: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 152,94: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 153,19: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 153,46: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 153,74: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- t 153,99: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon UpperBody:HumanM@Talk01
- t 154,25: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon UpperBody:HumanM@Talk02
- t 154,52: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon UpperBody:HumanM@Talk02
- t 154,77: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon UpperBody:HumanM@Talk02
- t 155,02: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon UpperBody:HumanM@Talk02
- t 155,28: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon UpperBody:HumanM@Talk02
- t 155,55: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon UpperBody:HumanM@Talk02
- t 155,82: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon UpperBody:HumanM@Talk02
- t 156,09: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon UpperBody:HumanM@Talk02
- t 156,35: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon UpperBody:HumanM@Talk02
- t 156,60: total 46 = clip 1, quieta 0, resto 47; cuello 16; Base Layer:InteractWithPeople_NoWeapon
- …

### NPC_Aldeano_08
- t 137,12: total 37 = clip 11, quieta 0, resto 26; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:HumanM@Question01
- t 137,37: total 42 = clip 16, quieta 0, resto 26; cuello 8; Base Layer:InteractWithPeople_NoWeapon
- t 137,62: total 43 = clip 17, quieta 0, resto 26; cuello 8; Base Layer:InteractWithPeople_NoWeapon UpperBody:Gesto_Cheer02
- t 137,88: total 40 = clip 14, quieta 0, resto 26; cuello 8; Base Layer:InteractWithPeople_NoWeapon UpperBody:Gesto_Cheer02
- t 138,15: total 38 = clip 12, quieta 0, resto 27; cuello 8; Base Layer:InteractWithPeople_NoWeapon UpperBody:Gesto_Cheer02
- t 138,41: total 40 = clip 14, quieta 0, resto 26; cuello 8; Base Layer:InteractWithPeople_NoWeapon UpperBody:Gesto_Cheer02
- t 138,67: total 37 = clip 1, quieta 12, resto 26; cuello 9; Base Layer:InteractWithPeople_NoWeapon UpperBody:HumanM@Talk01
- t 138,94: total 37 = clip 0, quieta 11, resto 27; cuello 9; Base Layer:InteractWithPeople_NoWeapon UpperBody:HumanM@Talk01
- t 139,20: total 38 = clip 0, quieta 11, resto 27; cuello 8; Base Layer:InteractWithPeople_NoWeapon UpperBody:HumanM@Talk01
- t 139,46: total 38 = clip 0, quieta 11, resto 27; cuello 8; Base Layer:InteractWithPeople_NoWeapon UpperBody:HumanM@Talk01

### NPC_Archimago
- t 281,96: total 38 = clip 14, quieta 0, resto 24; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:HumanM@Beg01 - Loop
- t 282,23: total 41 = clip 17, quieta 0, resto 24; cuello 9; Base Layer:Idle_Normal_NoWeapon UpperBody:HumanM@Beg01 - Loop
- t 282,49: total 43 = clip 19, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 282,75: total 42 = clip 18, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 283,01: total 40 = clip 17, quieta 0, resto 23; cuello 9; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 283,27: total 41 = clip 18, quieta 0, resto 23; cuello 9; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 283,53: total 42 = clip 19, quieta 0, resto 23; cuello 9; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 283,79: total 43 = clip 20, quieta 0, resto 23; cuello 9; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 284,06: total 43 = clip 20, quieta 0, resto 23; cuello 9; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 284,33: total 43 = clip 20, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 284,60: total 42 = clip 19, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 284,87: total 41 = clip 18, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 285,15: total 41 = clip 17, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 285,44: total 42 = clip 18, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 285,72: total 43 = clip 20, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 286,00: total 43 = clip 20, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 286,29: total 43 = clip 20, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 286,59: total 42 = clip 19, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 286,84: total 41 = clip 18, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 287,10: total 40 = clip 17, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 287,60: total 42 = clip 19, quieta 0, resto 23; cuello 9; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 287,88: total 43 = clip 20, quieta 0, resto 23; cuello 9; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 288,15: total 43 = clip 20, quieta 0, resto 23; cuello 9; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 288,41: total 43 = clip 20, quieta 0, resto 23; cuello 9; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 288,67: total 42 = clip 19, quieta 0, resto 23; cuello 9; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 288,92: total 41 = clip 18, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 289,20: total 41 = clip 17, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 289,46: total 42 = clip 19, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 289,72: total 43 = clip 20, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 289,97: total 43 = clip 20, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 290,23: total 43 = clip 20, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 290,49: total 43 = clip 19, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 290,77: total 42 = clip 18, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 291,06: total 41 = clip 17, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 291,32: total 41 = clip 18, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 291,59: total 42 = clip 19, quieta 0, resto 23; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 291,84: total 43 = clip 20, quieta 0, resto 23; cuello 9; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 292,10: total 43 = clip 20, quieta 0, resto 23; cuello 9; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 292,39: total 43 = clip 20, quieta 0, resto 23; cuello 9; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- t 292,68: total 42 = clip 19, quieta 0, resto 23; cuello 9; Base Layer:Idle_Normal_NoWeapon UpperBody:Gesto_Conjuro_Invocar
- …

### NPC_Aldeano_02
- t 130,31: total 37 = clip 11, quieta 0, resto 27; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 130,57: total 40 = clip 11, quieta 0, resto 29; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 130,83: total 41 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 131,08: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 131,34: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 131,60: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 131,85: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 132,11: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 132,37: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 132,63: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 132,88: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 133,15: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 133,42: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 133,69: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 133,94: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 134,20: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 134,45: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 134,73: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 135,00: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 135,26: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 137,12: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 137,37: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 137,62: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 137,88: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 138,15: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 138,41: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 138,67: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 138,94: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 139,20: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 139,46: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 139,72: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 139,98: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 140,25: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 140,52: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 140,77: total 42 = clip 11, quieta 0, resto 31; cuello 7; Base Layer:Idle_Normal_NoWeapon
- t 141,03: total 41 = clip 11, quieta 0, resto 31; cuello 9; Base Layer:InteractWithPeople_NoWeapon
- t 141,29: total 42 = clip 11, quieta 0, resto 35; cuello 14; Base Layer:InteractWithPeople_NoWeapon

### MadreDelValle
- t 123,20: total 38 = clip 0, quieta 0, resto 38; cuello 16; Base Layer:Idle_Normal_NoWeapon
- t 123,47: total 40 = clip 0, quieta 0, resto 40; cuello 16; Base Layer:Idle_Normal_NoWeapon
- t 123,73: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 123,99: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 124,25: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 124,51: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 124,77: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 125,03: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 125,31: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 125,57: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 125,83: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 126,09: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 126,36: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 126,63: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 126,89: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 127,14: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 127,40: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 127,68: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 127,94: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 128,20: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 128,47: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 128,74: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 129,02: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 129,29: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 129,54: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 129,79: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 130,05: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 130,31: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 130,57: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 130,83: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 131,08: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 131,34: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 131,60: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 131,85: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 132,11: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 132,37: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 132,63: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 132,88: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 133,15: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- t 133,42: total 41 = clip 0, quieta 0, resto 41; cuello 17; Base Layer:Idle_Normal_NoWeapon
- …

### AbuelaDelMirador
- t 130,05: total 36 = clip 9, quieta 0, resto 27; cuello 9; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 130,31: total 38 = clip 8, quieta 0, resto 30; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 130,57: total 38 = clip 8, quieta 0, resto 31; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 130,83: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 131,08: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 131,34: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 131,60: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 131,85: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 132,11: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 132,37: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 132,63: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 132,88: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 133,15: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:HumanM@HandClap01
- t 133,42: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:HumanM@HandClap01
- t 133,69: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:HumanM@HandClap01
- t 133,94: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:HumanM@HandClap01
- t 134,20: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:HumanM@HandClap01
- t 134,45: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:HumanM@HandClap01
- t 134,73: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:HumanM@HandClap01
- t 135,00: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:HumanM@HandClap01
- t 135,26: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 135,53: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 135,80: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 136,07: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 136,33: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 136,60: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 136,85: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 137,12: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 137,37: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 137,62: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 137,88: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 138,15: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 138,41: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 138,67: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 138,94: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 139,20: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 139,46: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 139,72: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 139,98: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 140,25: total 39 = clip 8, quieta 0, resto 32; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- …

### NPC_Aldeano_03
- t 134,73: total 37 = clip 10, quieta 0, resto 27; cuello 8; Base Layer:Idle_Normal_NoWeapon UpperBody:HumanM@Talk01

### VecinoDelMirador
- t 130,31: total 35 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 130,57: total 35 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 130,83: total 35 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 131,08: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 131,34: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 131,60: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 131,85: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 132,11: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 132,37: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 132,63: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 132,88: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 133,15: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 133,42: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 133,69: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 133,94: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 134,20: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 134,45: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 134,73: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 135,00: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 135,26: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 135,53: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 135,80: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 136,07: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 136,33: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 136,60: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 136,85: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 137,12: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 137,37: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 137,62: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 137,88: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 138,15: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 138,41: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 138,67: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 138,94: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 139,20: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 139,46: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 139,72: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 139,98: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 140,25: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- t 140,52: total 36 = clip 11, quieta 0, resto 25; cuello 5; Base Layer:Idle_Normal_NoWeapon UpperBody:Pose_Asomado
- …

