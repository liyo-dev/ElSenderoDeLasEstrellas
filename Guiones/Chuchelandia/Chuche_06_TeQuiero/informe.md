# Parte de horneado: Chuche_06_TeQuiero

2026-11-11 18:12 · guion `Assets/_SEQUENCES/Guiones/Chuche_06_TeQuiero.guion.txt`

**0:49,8** de secuencia · 11 frases · 14 planos · 59 efectos · 10 personajes

Reparto medido (m): will 1,62/ojos 0,93 · estela 1,53/ojos 0,94 · liam 1,68/ojos 0,93 · pompon 1,97/ojos 1,15 · presidente 2,07/ojos 1,22 · malvavisco 1,54/ojos 1,17 · lagrimitas 1,25/ojos 1,06 · g1 1,97/ojos 1,15 · g2 1,97/ojos 1,15 · g3 1,97/ojos 1,15

## Errores (0)


## Avisos (10)

- línea 26: 'Player' no tiene punto de aparición en Chuchelandia; necesita un «en» al principio.
- línea 248: lagrimitas dice CHUCHELANDIA_DUELO_CARTEL_DIEZ texto=bocadillo sin_pose dura=1.4: la frase CHUCHELANDIA_DUELO_CARTEL_DIEZ no tiene voz en Voices/es; usa los 1,4 s del guion
- línea 249: malvavisco dice CHUCHELANDIA_DUELO_CARTEL_DIEZ texto=bocadillo sin_pose dura=1.4: la frase CHUCHELANDIA_DUELO_CARTEL_DIEZ no tiene voz en Voices/es; usa los 1,4 s del guion
- línea 253: presidente dice CHUCHELANDIA_DUELO_CARTEL_DIEZ_Y_UNO texto=bocadillo sin_pose dura=2.6: la frase CHUCHELANDIA_DUELO_CARTEL_DIEZ_Y_UNO no tiene voz en Voices/es; usa los 2,6 s del guion
- PROP_CorazonDeLiam: recogida sin «deja» posterior; al cerrar vuelve a su sitio inicial, oculto como empezó.
- PROP_Velo: recogida sin «deja» posterior; al cerrar vuelve a su sitio inicial, oculto como empezó.
- PROP_LazoDeEstela: recogida sin «deja» posterior; al cerrar vuelve a su sitio inicial, oculto como empezó.
- línea 134: plano conjunto will liam p_fuente desde=cam_lado altura=1.2 lente=36: algo del decorado se mete delante y tapa un 33% del cuadro. Prueba lado= o desde=.
- 0:38,0: en «conjunto de presidente y malvavisco y lagrimitas» pompon está 8 s en cuadro sin hacer nada. Dale algo en su pista (charla, bucle, pasea…).
- No hay captura del escenario: usa «El Sendero → Guion → 1. Capturar el escenario» para tener los dibujos.

## Silencios largos entre frases (3)

- 0:02,2 → 0:09,3: 7,1 s sin voz entre CHUCHELANDIA_TEQUIERO_01 y CHUCHELANDIA_TEQUIERO_07
- 0:13,4 → 0:20,0: 6,5 s sin voz entre CHUCHELANDIA_TEQUIERO_08 y CHUCHELANDIA_TEQUIERO_02
- 0:30,7 → 0:38,6: 7,8 s sin voz entre CHUCHELANDIA_TEQUIERO_05 y CHUCHELANDIA_DUELO_CARTEL_DIEZ

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
| 0:00,0 | efecto | Encender: PROP_Pancarta |
| 0:00,0 | efecto | Sin física: Player |
| 0:00,0 | efecto | Sin física: Actor_Liam |
| 0:00,1 | plano 1 | conjunto de will · 2,1 s · lente 32° |
| 0:00,1 | will | «¡Vamos, Estela!» (2,1 s) · pose |
| 0:00,1 | efecto | SFX: Pizzicato-005 |
| 0:02,2 | **@estela_se_vuelve** |  |
| 0:02,2 | plano 2 | medio de estela · 1,7 s · lente 30° · coste 14: ángulo 8 |
| 0:03,9 | **@liam_sube** |  |
| 0:03,9 | plano 3 | conjunto de liam · 2,4 s · lente 30° |
| 0:05,5 | efecto | SFX: Cartoon_Plastic_Boing |
| 0:06,3 | **@pancarta** |  |
| 0:06,3 | plano 4 | conjunto de will y liam · 7,1 s · lente 30° |
| 0:08,7 | **@rebote** |  |
| 0:08,7 | efecto | SFX: Cartoon_Metallic_Boing(6) |
| 0:08,7 | efecto | Decorado: mover PROP_Pancarta en 0,2s |
| 0:08,7 | efecto | SFX: Pizzicato-006 |
| 0:08,9 | efecto | Decorado: mover PROP_Pancarta en 0,2s |
| 0:08,9 | efecto | Decorado: mover PROP_SillaWill en 0,2s |
| 0:09,1 | efecto | Decorado: mover PROP_Pancarta en 0,2s |
| 0:09,1 | efecto | SFX: Cartoon_Metallic_Boing(6) |
| 0:09,3 | will | «¡Uy, uy, uy…!» (2,7 s) |
| 0:09,3 | efecto | Decorado: mover PROP_SillaWill en 0,3s |
| 0:12,3 | liam | «¡Will!» (1,1 s) |
| 0:13,4 | **@caida** |  |
| 0:13,4 | plano 5 | conjunto de will y liam · 2,2 s · lente 36° |
| 0:13,4 | efecto | Decorado: mover PROP_SillaLiam en 1,2s |
| 0:13,4 | efecto | Decorado: mover PROP_Pancarta en 1,5s |
| 0:13,4 | efecto | Decorado: mover PROP_SillaWill en 1,1s |
| 0:13,4 | efecto | Mezcla: caida |
| 0:13,4 | efecto | Tiempo: ×0,3 en 0,15s |
| 0:13,4 | efecto | SFX: cartoon_whistle_down(4) |
| 0:15,0 | **@plof** |  |
| 0:15,0 | efecto | Tiempo: ×1 en 0,1s |
| 0:15,0 | efecto | VFX: VFX_SalpicaduraDeSirope en M_Cine_Salon_Fuente_Will |
| 0:15,0 | efecto | VFX: VFX_SalpicaduraDeSirope en M_Cine_Salon_Fuente_Liam |
| 0:15,0 | efecto | SFX: Water_Splash-013 |
| 0:15,0 | efecto | Sacudida de cámara (0,2, 0,45s) |
| 0:15,0 | efecto | Mezcla: caida (quitar) |
| 0:15,0 | efecto | Apagar: PROP_Pancarta |
| 0:15,1 | efecto | SFX: Water_Splash-003 |
| 0:15,6 | **@silencio_fuente** |  |
| 0:15,6 | plano 6 | fijo a el punto · 4,3 s · lente 26° |
| 0:15,6 | efecto | Coger PROP_CorazonDeLiam (Head) |
| 0:15,6 | efecto | Coger PROP_Velo (Head) |
| 0:15,6 | efecto | Decorado: mover PROP_MantelNovios (al instante) |
| 0:15,6 | efecto | Atuendo: poner Atuendo_Chuchelandia_Novios |
| 0:15,6 | efecto | Mezcla: silencio |
| 0:15,6 | efecto | Encender: PROP_MantelNovios |
| 0:16,3 | efecto | SFX: Bubble-001 |
| 0:16,8 | efecto | SFX: Bubble-002 |
| 0:17,3 | **@salen** |  |
| 0:17,3 | efecto | Decorado: mover PROP_MantelNovios en 0,4s |
| 0:17,3 | efecto | SFX: Cartoon_Mouth_harp_boing |
| 0:17,3 | efecto | VFX: VFX_SalpicaduraDeSirope en M_Duelo_Fuente |
| 0:17,7 | efecto | SFX: Water_Drip-004 |
| 0:19,1 | efecto | SFX: Water_Drip-005 |
| 0:20,0 | **@novios** |  |
| 0:20,0 | plano 7 | medio de g2 · 2,6 s · lente 30° · coste 127: cara 100, ángulo 21 |
| 0:20,0 | g2 | «¡Vivan los novios!» (2,6 s) |
| 0:20,0 | efecto | Mezcla: silencio (quitar) |
| 0:22,6 | plano 8 | dos: liam y will · 8,2 s · lente 32° · coste 614: estorbo 395, cara 213 |
| 0:22,6 | efecto | VFX: Confetti_blast_multicolor en Player |
| 0:22,6 | efecto | SFX: Balloon_Pop-003 |
| 0:23,1 | liam | «Esto no es lo que habíamos planeado.» (2,5 s) |
| 0:25,9 | will | «No habíamos planeado nada.» (3,0 s) |
| 0:29,1 | liam | «Pues ya ves.» (1,6 s) |
| 0:30,7 | **@aguanta** |  |
| 0:30,7 | plano 9 | medio de estela · 2,0 s · lente 30° · coste 2: ángulo 2 |
| 0:31,8 | efecto | SFX: Pig_-003 |
| 0:32,7 | **@risa** |  |
| 0:32,7 | plano 10 | conjunto de estela · 4,0 s · lente 36° · coste 23: ángulo 23 |
| 0:32,7 | efecto | SFX: Final_Estela_Risa |
| 0:32,7 | efecto | Atuendo: poner Atuendo_Chuchelandia_LazoTorcido |
| 0:32,7 | efecto | Coger PROP_LazoDeEstela (Head) |
| 0:36,8 | **@silencio_salon** |  |
| 0:36,8 | plano 11 | general de estela y presidente y malvavisco y lagrimitas · 1,2 s · lente 42° · coste 15: ángulo 9 |
| 0:36,8 | efecto | Mezcla: salon |
| 0:38,0 | **@cartelitos** |  |
| 0:38,0 | plano 12 | conjunto de presidente y malvavisco y lagrimitas · 7,3 s · lente 34° · coste 256: cara 228, ángulo 23 |
| 0:38,0 | efecto | SFX: Drum_Rolls-006 |
| 0:38,6 | lagrimitas | «10 corazones» (1,4 s) |
| 0:40,3 | malvavisco | «10 corazones» (1,4 s) |
| 0:42,7 | presidente | «10 corazones + 1 dibujado a lápiz» (2,6 s) |
| 0:42,7 | efecto | SFX: Paper_Wrapping_&_Flip-017 |
| 0:45,3 | **@cuenta** |  |
| 0:45,3 | plano 13 | dos: liam y will · 2,3 s · lente 32° · coste 320: estorbo 300, ángulo 14 |
| 0:45,3 | liam | «Ha ganado.» (1,8 s) |
| 0:45,3 | efecto | Mezcla: salon (quitar) |
| 0:47,5 | **@final** |  |
| 0:47,5 | plano 14 | medio de pompon · 2,3 s · lente 32° · coste 199: cara 137, ángulo 56 |
| 0:47,5 | efecto | Atuendo: quitar Atuendo_Chuchelandia_Novios |
| 0:47,5 | efecto | Apagar: PROP_LazoDeEstela |
| 0:47,5 | efecto | Atuendo: quitar Atuendo_Chuchelandia_LazoTorcido |
| 0:47,5 | efecto | Apagar: PROP_Velo |
| 0:47,5 | efecto | Apagar: PROP_MantelNovios |
| 0:47,5 | efecto | Apagar: PROP_CorazonDeLiam |
| 0:48,8 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### will (Player)

- 0:00,1–0:02,2 habla
- 0:00,1 gesto Cheer02
- 0:06,3 gesto Cheer02
- 0:09,3–0:12,0 habla
- 0:12,7 gesto HeadNod01
- 0:13,4 bucle Falling
- 0:14,7 bucle Knockdown01_Ground
- 0:15,6 reposo 
- 0:15,6 reposo 
- 0:15,6 desaparece
- 0:17,3 aparece
- 0:20,7 gesto HeadNod01
- 0:23,8 gesto HeadShake01
- 0:25,9–0:28,8 habla
- 0:26,0 gesto Shrug01
- 0:29,8 gesto HeadNod01
- 0:39,1 gesto HeadNod01

### estela (Actor_Estela)

- 0:10,0 gesto HeadNod01
- 0:20,7 gesto HeadNod01
- 0:29,8 gesto HeadNod01
- 0:31,8 gesto Pain01
- 0:32,7 gesto Laugh01
- 0:35,0 bucle Laugh01_Loop
- 0:36,8 reposo 
- 0:39,1 gesto HeadNod01
- 0:46,0 gesto HeadNod01

### liam (Actor_Liam)

- 0:03,9 gesto HeadShake01
- 0:05,5 gesto JumpFull_InPlace_NoWeapon
- 0:10,0 gesto HeadNod01
- 0:12,3–0:13,4 habla
- 0:13,4 bucle Falling
- 0:14,7 bucle Knockdown01_Ground
- 0:15,6 reposo 
- 0:15,6 reposo 
- 0:15,6 desaparece
- 0:17,3 aparece
- 0:20,7 gesto HeadNod01
- 0:22,6 bucle BrazosCruzados
- 0:23,1–0:25,6 habla
- 0:26,6 gesto HeadShake01
- 0:29,1–0:30,7 habla
- 0:39,1 gesto HeadNod01
- 0:45,3 reposo 
- 0:45,3–0:47,0 habla

### pompon (Actor_Pompon)

- 0:47,5 gesto Jump

### presidente (Actor_JuezPresidente)

- 0:42,7–0:45,3 habla

### malvavisco (Actor_JuezMarshie)

- 0:40,3 gesto Marshies_Jump
- 0:40,3–0:41,7 habla

### lagrimitas (Actor_JuezEyeBud)

- 0:38,6–0:40,0 habla

### g1 (Actor_Gominola1)

- 0:00,0 aparece

### g2 (Actor_Gominola2)

- 0:00,0 aparece
- 0:20,0 gesto Jump
- 0:20,0–0:22,6 habla

### g3 (Actor_Gominola3)

- 0:00,0 aparece

