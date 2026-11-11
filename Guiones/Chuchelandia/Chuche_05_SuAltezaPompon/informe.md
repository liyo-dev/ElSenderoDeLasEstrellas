# Parte de horneado: Chuche_05_SuAltezaPompon

2026-11-11 18:18 · guion `Assets/_SEQUENCES/Guiones/Chuche_05_SuAltezaPompon.guion.txt`

**1:39,1** de secuencia · 29 frases · 34 planos · 19 efectos · 11 personajes

Reparto medido (m): will 1,62/ojos 0,93 · estela 1,53/ojos 0,94 · liam 1,68/ojos 0,93 · condesa 1,55/ojos 1,17 · heraldo 2,08/ojos 1,22 · suplente 2,08/ojos 1,22 · pompon 1,97/ojos 1,15 · g1 1,97/ojos 1,15 · g2 1,97/ojos 1,15 · g3 1,97/ojos 1,15 · g4 1,97/ojos 1,15

## Errores (0)


## Avisos (27)

- línea 18: 'Player' no tiene punto de aparición en Chuchelandia; necesita un «en» al principio.
- línea 23: 'Actor_HeraldoSuplente' no tiene punto de aparición en Chuchelandia; necesita un «en» al principio.
- línea 23: 'Actor_HeraldoSuplente' no está en el escenario de la secuencia ni hay en Chuchelandia un NPC que se registre con ese id: en juego no saldrá (salvo que lo traiga otra escena o el grafo). Ponlo en el SequenceStage con ese id.
- línea 76: pompon anda a p_pompon ritmo=paseo llega_mirando=estela: no se llega del todo a (92,9, 3,0) por el NavMesh
- línea 128: liam anda a p_liam_atras ritmo=paseo llega_mirando=heraldo: no se llega del todo a (96,6, 4,9) por el NavMesh
- línea 178: & suplente anda a p_suplente ritmo=normal llega_mirando=condesa: no se llega del todo a (92,2, 1,7) por el NavMesh
- línea 108: plano dos estela condesa: no hay ángulo limpio para «dos: estela y condesa» (algo tapa la cara). Prueba con desde= o lado=.
- línea 113: plano medio condesa solo: no hay ángulo limpio para «medio de condesa» (algo tapa la cara). Prueba con desde= o lado=.
- línea 113: plano medio condesa solo: condesa habla de espaldas a la cámara en 33,6 s (158°). Prueba lado=, otro desde= o un plano nuevo cuando se gire.
- línea 113: plano medio condesa solo: condesa habla de espaldas a la cámara en 37,6 s (158°). Prueba lado=, otro desde= o un plano nuevo cuando se gire.
- línea 189: plano dos estela will: no hay ángulo limpio para «dos: estela y will» (algo tapa la cara). Prueba con desde= o lado=.
- línea 214: plano medio estela lado=frente: no cabe el contraplano sobre el hombro de condesa; hago un plano medio de estela.
- línea 214: plano medio estela lado=frente: estela habla de espaldas a la cámara en 90,5 s (113°). Prueba lado=, otro desde= o un plano nuevo cuando se gire.
- línea 218: plano medio estela lado=frente: no cabe el contraplano sobre el hombro de condesa; hago un plano medio de estela.
- línea 218: plano medio estela lado=frente: estela habla de espaldas a la cámara en 95,1 s (113°). Prueba lado=, otro desde= o un plano nuevo cuando se gire.
- 0:01,2: en «conjunto de pompon y estela» will está 8 s en cuadro sin hacer nada. Dale algo en su pista (charla, bucle, pasea…).
- 0:01,2: en «conjunto de pompon y estela» condesa está 8 s en cuadro sin hacer nada. Dale algo en su pista (charla, bucle, pasea…).
- 0:01,2: en «conjunto de pompon y estela» heraldo está 7 s en cuadro sin hacer nada. Dale algo en su pista (charla, bucle, pasea…).
- 0:01,2: en «conjunto de pompon y estela» g4 está 8 s en cuadro sin hacer nada. Dale algo en su pista (charla, bucle, pasea…).
- 0:08,5: en «medio de heraldo» condesa está 8 s en cuadro sin hacer nada. Dale algo en su pista (charla, bucle, pasea…).
- 0:08,5: en «medio de heraldo» g4 está 8 s en cuadro sin hacer nada. Dale algo en su pista (charla, bucle, pasea…).
- 0:32,3: en «medio de condesa» pompon está 8 s en cuadro sin hacer nada. Dale algo en su pista (charla, bucle, pasea…).
- 0:32,3: en «medio de condesa» g3 está 8 s en cuadro sin hacer nada. Dale algo en su pista (charla, bucle, pasea…).
- 0:47,1: el plano «medio de heraldo» dura 0,6 s (un corte tan corto se lee como un salto).
- 1:23,2: en «dos: condesa y estela» suplente está 7 s en cuadro sin hacer nada. Dale algo en su pista (charla, bucle, pasea…).
- 1:23,2: en «dos: condesa y estela» pompon está 7 s en cuadro sin hacer nada. Dale algo en su pista (charla, bucle, pasea…).
- No hay captura del escenario: usa «El Sendero → Guion → 1. Capturar el escenario» para tener los dibujos.

## Silencios largos entre frases (3)

- 0:42,3 → 0:44,8: 2,5 s sin voz entre CHUCHELANDIA_POMPON_12 y CHUCHELANDIA_POMPON_13
- 1:13,6 → 1:15,6: 2,0 s sin voz entre CHUCHELANDIA_POMPON_23 y CHUCHELANDIA_POMPON_24
- 1:16,8 → 1:23,2: 6,3 s sin voz entre CHUCHELANDIA_POMPON_24 y CHUCHELANDIA_POMPON_25

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | plano 1 | general de estela y pompon · 1,2 s · lente 42° · coste 791: delante 552, cara 130, ángulo 104 |
| 0:00,0 | efecto | Escalar: Actor_Heraldo ×1 en 0 s |
| 0:00,0 | efecto | SFX: Trumpet-009 |
| 0:00,0 | efecto | SFX: Trumpet-010 |
| 0:01,2 | **@alfombra** |  |
| 0:01,2 | plano 2 | conjunto de pompon y estela · 7,3 s · lente 34° · coste 543: cara 110, ángulo 77, eje 350 |
| 0:01,2 | efecto | VFX: Sparkle_ellow_flying en Actor_Pompon |
| 0:02,6 | efecto | SFX: cartoon_whistle_down(4) |
| 0:08,5 | **@anuncio** |  |
| 0:08,5 | plano 3 | medio de heraldo · 7,4 s · lente 30° · coste 240: cara 202, ángulo 32 |
| 0:08,5 | heraldo | «¡Su Alteza Pompón! ¡Princesa de Chuchelandia y campeona de ternura durante cuatrocientos años seguidos!» (7,4 s) |
| 0:15,9 | **@oso** |  |
| 0:15,9 | plano 4 | medio de estela · 1,6 s · lente 30° · coste 150: cara 112, ángulo 32 |
| 0:15,9 | estela | «¿Eso es un oso?» (1,6 s) · pose |
| 0:17,5 | plano 5 | medio de heraldo · 1,8 s · lente 30° · coste 240: cara 202, ángulo 32 |
| 0:17,5 | heraldo | «Es la princesa.» (1,8 s) |
| 0:19,3 | plano 6 | medio de estela · 1,3 s · lente 30° · coste 328: cara 273, ángulo 50 |
| 0:19,3 | estela | «Es un oso.» (1,3 s) |
| 0:20,6 | plano 7 | medio de heraldo · 2,0 s · lente 30° · coste 240: cara 202, ángulo 32 |
| 0:20,6 | heraldo | «Es la princesa oso.» (2,0 s) |
| 0:20,6 | efecto | Escalar: Actor_Heraldo ×1,06 en 0,6 s |
| 0:22,6 | plano 8 | medio de estela · 2,0 s · lente 30° · coste 328: cara 273, ángulo 50 |
| 0:22,6 | estela | «Es un oso con una tiara.» (2,0 s) |
| 0:24,6 | plano 9 | medio de heraldo · 3,4 s · lente 30° · coste 240: cara 202, ángulo 32 |
| 0:24,6 | heraldo | «Es. La. Princesa.» (3,4 s) |
| 0:24,6 | efecto | Escalar: Actor_Heraldo ×1,12 en 1,2 s |
| 0:28,0 | plano 10 | medio de estela · 2,5 s · lente 30° · coste 328: cara 273, ángulo 50 |
| 0:28,0 | estela | «Pues tiene cara de oso.» (2,5 s) |
| 0:28,0 | efecto | Escalar: Actor_Heraldo ×1,18 en 1 s |
| 0:30,5 | plano 11 | dos: estela y condesa · 1,8 s · lente 32° · coste 1158: cara 1089, ángulo 63 |
| 0:30,5 | estela | «¿Le pasa algo?» (1,8 s) |
| 0:32,3 | plano 12 | medio de condesa · 7,7 s · lente 30° · coste 1194: cara 1088, ángulo 107 |
| 0:32,3 | condesa | «No le pasa nada, alteza.» (2,7 s) |
| 0:35,2 | condesa | «Es solo que usted es una candidata… complicada.» (4,8 s) |
| 0:40,0 | plano 13 | sobre el hombro de condesa a estela · 2,3 s · lente 24° · coste 703: delante 347, eje 350 |
| 0:40,0 | estela | «Yo solo digo que es un oso.» (2,3 s) |
| 0:40,0 | efecto | Escalar: Actor_Heraldo ×1,3 en 1,4 s |
| 0:42,3 | **@hinchado** |  |
| 0:42,3 | plano 14 | medio de heraldo · 1,0 s · lente 30° · coste 241: cara 150, ángulo 86 |
| 0:42,3 | efecto | SFX: cartoon_whistle_up(1) |
| 0:42,3 | efecto | Escalar: Actor_Heraldo ×1,45 en 1,2 s |
| 0:43,3 | plano 15 | conjunto de will y liam y estela · 1,5 s · lente 34° · coste 54: cara 54 |
| 0:44,8 | plano 16 | sobre el hombro de pompon a estela · 2,3 s · lente 24° · coste 431: cara 81, eje 350 |
| 0:44,8 | estela | «Y bastante pegajoso.» (2,1 s) |
| 0:47,1 | **@pop** |  |
| 0:47,1 | plano 17 | medio de heraldo · 0,6 s · lente 30° · coste 241: cara 150, ángulo 86 |
| 0:47,1 | efecto | VFX: Confetti_blast_multicolor en M_Cine_Salon_Heraldo |
| 0:47,1 | efecto | SFX: Balloon_Pop-003 |
| 0:47,1 | efecto | Sacudida de cámara (0,2, 0,4s) |
| 0:47,7 | **@culpa** |  |
| 0:47,7 | plano 18 | conjunto de estela y condesa y liam y will · 3,1 s · lente 34° · coste 282: cara 240, ángulo 36 |
| 0:47,7 | efecto | SFX: Metal_Hit-013 |
| 0:47,7 | efecto | Escalar: Actor_Heraldo ×1 en 0 s |
| 0:48,5 | estela | «¿Qué? Yo no he hecho nada.» (2,3 s) · pose |
| 0:50,8 | plano 19 | medio de condesa · 4,2 s · lente 30° · coste 283: cara 212, ángulo 65 |
| 0:50,8 | condesa | «Le ha llamado oso a Su Alteza. Cinco veces.» (4,2 s) |
| 0:55,0 | plano 20 | medio de liam · 2,1 s · lente 30° · coste 127: cara 100, ángulo 21 |
| 0:55,0 | liam | «Cinco.» (2,1 s) |
| 0:57,1 | plano 21 | medio de estela · 1,8 s · lente 30° · coste 43: cara 25, ángulo 12 |
| 0:57,1 | estela | «¡Es que es un oso!» (1,8 s) · pose |
| 0:58,8 | plano 22 | medio de liam · 1,5 s · lente 30° · coste 43: cara 25, ángulo 12 |
| 0:58,8 | liam | «Seis.» (1,5 s) |
| 1:00,4 | **@suplente** |  |
| 1:00,4 | plano 23 | medio de condesa · 2,9 s · lente 30° · coste 7: ángulo 2 |
| 1:00,4 | condesa | «¡Que pase el suplente!» (2,9 s) |
| 1:03,2 | plano 24 | medio de g4 · 3,2 s · lente 30° · coste 35: ángulo 29 |
| 1:03,2 | g4 | «Ya van tres esta semana.» (3,2 s) |
| 1:06,4 | plano 25 | conjunto de condesa y suplente · 2,8 s · lente 34° · coste 62: cara 2, ángulo 54 |
| 1:06,4 | suplente | «¿Es ella?» (1,2 s) |
| 1:07,9 | condesa | «Es ella.» (1,3 s) |
| 1:09,2 | plano 26 | conjunto de suplente y estela · 4,4 s · lente 34° · coste 321: cara 43, ángulo 18, eje 260 |
| 1:09,8 | suplente | «¡Su Alteza Pompón! ¡Princesa de Chuchelandia!» (3,8 s) |
| 1:13,6 | **@tapa** |  |
| 1:13,6 | plano 27 | dos: estela y will · 3,2 s · lente 32° · coste 1034: tapado 344, cara 271, ángulo 63, eje 350 |
| 1:15,6 | estela | «¡Mmmf!» (1,2 s) |
| 1:16,8 | **@chapoteo** |  |
| 1:16,8 | plano 28 | medio de pompon · 3,2 s · lente 30° · coste 199: cara 137, ángulo 56 |
| 1:16,8 | efecto | SFX: Bubble-001 |
| 1:17,2 | efecto | SFX: Bubble-002 |
| 1:20,0 | plano 29 | general de estela y pompon y g1 y g2 · 3,1 s · lente 42° · coste 191: cara 99, ángulo 85 |
| 1:20,3 | efecto | SFX: cartoon_whistle_down(6) |
| 1:23,2 | **@duelo** |  |
| 1:23,2 | plano 30 | dos: condesa y estela · 6,6 s · lente 32° · coste 875: cara 846, ángulo 23 |
| 1:23,2 | condesa | «Para abrir la Puerta del Corazón, la candidata debe vencer a la princesa reinante en el Duelo de Ternura.» (6,6 s) |
| 1:29,7 | plano 31 | medio de estela · 1,5 s · lente 24° · coste 930: cara 863, ángulo 68 |
| 1:29,7 | estela | «¿Y si pierdo?» (1,5 s) · pose |
| 1:31,2 | plano 32 | medio de condesa · 2,8 s · lente 30° · coste 211: cara 175, ángulo 30 |
| 1:31,2 | condesa | «Nadie ha vencido nunca a Su Alteza.» (2,8 s) |
| 1:34,0 | plano 33 | medio de estela · 2,1 s · lente 24° · coste 930: cara 863, ángulo 68 |
| 1:34,0 | estela | «Eso no responde a mi pregunta.» (2,1 s) · pose |
| 1:36,2 | plano 34 | medio de liam · 3,0 s · lente 30° · coste 41: ángulo 33 |
| 1:36,2 | liam | «Sí que la responde.» (1,8 s) |

## Qué hace cada personaje

### will (Player)

- 0:09,2 gesto HeadNod01
- 0:18,2 gesto HeadNod01
- 0:25,3 gesto HeadNod01
- 0:33,0 gesto HeadShake01
- 0:43,4–0:44,7 anda de (95,6, 1,6) a (96,8, 1,1)
- 0:49,2 gesto Question01
- 0:55,7 gesto HeadNod01
- 1:03,9 gesto HeadNod01
- 1:06,9 gesto Question01
- 1:10,5 gesto HeadNod01
- 1:13,7–1:15,7 anda de (96,7, 1,1) a (94,4, 2,6)
- 1:15,6 gesto Defend_NoWeapon
- 1:23,9 gesto HeadNod01
- 1:31,9 gesto HeadShake01
- 1:36,9 gesto HeadNod01

### estela (Actor_Estela)

- 0:09,2 gesto HeadNod01
- 0:15,9–0:17,5 habla
- 0:16,0 gesto Question01
- 0:18,2 gesto HeadNod01
- 0:19,3 bucle BrazosEnJarras
- 0:19,3–0:20,6 habla
- 0:22,6–0:24,6 habla
- 0:22,7 gesto Question01
- 0:24,5 bucle BrazosEnJarras
- 0:28,0–0:30,5 habla
- 0:30,5–0:32,3 habla
- 0:40,0–0:42,3 habla
- 0:40,1 gesto Shrug01
- 0:41,4 bucle BrazosEnJarras
- 0:44,8–0:46,9 habla
- 0:47,7 reposo 
- 0:48,5–0:50,8 habla
- 0:48,6 gesto Question02
- 0:51,5 gesto HeadNod01
- 0:57,1–0:58,8 habla
- 0:57,2 gesto Angry02
- 0:59,4 gesto HeadNod01
- 1:06,9 gesto Question01
- 1:10,5 gesto HeadNod01
- 1:13,6 gesto Talk01
- 1:15,6–1:16,8 habla
- 1:23,9 gesto HeadNod01
- 1:29,7–1:31,2 habla
- 1:29,8 gesto Question01
- 1:31,9 gesto HeadShake01
- 1:34,0–1:36,2 habla
- 1:34,1 gesto Talk03

### liam (Actor_Liam)

- 0:09,2 gesto HeadNod01
- 0:18,2 gesto HeadNod01
- 0:25,3 gesto HeadNod01
- 0:33,0 gesto HeadShake01
- 0:43,4–0:44,9 anda de (95,6, 4,4) a (96,6, 4,9)
- 0:49,2 gesto Question01
- 0:55,0 bucle BrazosCruzados
- 0:55,0–0:57,1 habla
- 0:57,8 gesto HeadNod01
- 0:58,8–1:00,4 habla
- 1:03,9 gesto HeadNod01
- 1:06,9 gesto Question01
- 1:10,5 gesto HeadNod01
- 1:23,9 gesto HeadNod01
- 1:31,9 gesto HeadShake01
- 1:36,2–1:37,9 habla

### condesa (Actor_CondesaMerengue)

- 0:32,3–0:35,0 habla
- 0:35,2–0:40,0 habla
- 0:50,8–0:55,0 habla
- 1:00,4 gesto Marshies_Wave
- 1:00,4–1:03,2 habla
- 1:07,9–1:09,2 habla
- 1:23,2–1:29,7 habla
- 1:31,2–1:34,0 habla

### heraldo (Actor_Heraldo)

- 0:08,5 gesto Idle2
- 0:08,5–0:15,9 habla
- 0:17,5–0:19,3 habla
- 0:20,6–0:22,6 habla
- 0:24,6–0:28,0 habla
- 0:42,3 gesto Idle2
- 0:47,1 desaparece

### suplente (Actor_HeraldoSuplente)

- 1:06,4–1:07,6 habla
- 1:06,4 aparece
- 1:06,5–1:11,0 anda de (88,6, 5,0) a (92,2, 1,7)
- 1:09,8–1:13,6 habla

### pompon (Actor_Pompon)

- 0:01,2 gesto Jump
- 0:01,2–0:08,5 anda de (87,1, 3,0) a (92,9, 3,0)
- 0:03,0 gesto Jump
- 0:04,8 gesto Jump
- 1:16,8 gesto Marshies_Happy_Dance

### g1 (Actor_Gominola1)

- 0:00,0 aparece
- 1:20,0 gesto Grummy_Defeat

### g2 (Actor_Gominola2)

- 0:00,0 aparece
- 0:01,2 gesto Marshies_Happy_Dance

### g3 (Actor_Gominola3)

- 0:00,0 aparece
- 0:02,6 gesto Grummy_Defeat

### g4 (Actor_Gominola4)

- 0:00,0 aparece
- 1:03,2–1:06,4 habla
- 1:20,3 gesto Grummy_Defeat

