# Parte de horneado: Cap1_OliverSaludo

2026-11-13 18:05 · guion `Assets/_SEQUENCES/Guiones/Cap1_OliverSaludo.guion.txt`

**0:39,0** de secuencia · 7 frases · 2 planos · 6 efectos · 2 personajes

Reparto medido (m): will 1,62/ojos 0,93 · oliver 1,75/ojos 0,94

## Errores (0)


## Avisos (2)

- línea 18: 'Player' no tiene punto de aparición en MainWorld; necesita un «en» al principio.
- línea 45: plano conjunto oliver will desde=cam_charla altura=1.5 lente=40 quieta: algo del decorado se mete delante y tapa un 60% del cuadro. Prueba lado= o desde=.

## Silencios largos entre frases (2)

- 0:02,6 → 0:07,0: 4,4 s sin voz entre OLIVER_GREETING_SPOT_WILL y OLIVER_GREETING_OUT_OF_BREATH
- 0:14,2 → 0:21,8: 7,6 s sin voz entre OLIVER_GREETING_NEW_SPELL_INTRO y WILL_GREETING_OLIVER_CUT_OFF

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | plano 1 | general de oliver y will · 7,0 s · lente 40° |
| 0:00,0 | oliver | «¡Will! ¡Espera!» (2,6 s) · pose |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
| 0:02,6 | **@carrera** |  |
| 0:07,0 | **@charla** |  |
| 0:07,0 | plano 2 | conjunto de oliver y will · 32,0 s · lente 40° |
| 0:07,0 | oliver | «Espera un momento... Necesito recuperar el aliento.» (4,5 s) · pose |
| 0:11,7 | oliver | «He aprendido un hechizo nuevo. Mira.» (2,5 s) · pose |
| 0:16,2 | **@hechizo** |  |
| 0:16,7 | efecto | VFX: VFX_Hechizo_Fallido en NPC_Oliver |
| 0:16,7 | efecto | SFX: SpellFail |
| 0:16,7 | efecto | Sacudida de cámara (0,35, 0,4s) |
| 0:21,8 | will | «¡Ja, sigues igual! ¿Por qué discuten Eldran y Victoria? Se les oye desde aquí.» (5,9 s) · pose |
| 0:27,7 | **@menus** |  |
| 0:28,0 | oliver | «¿Siguen discutiendo? Llevan así un buen rato... Antes de ir a ver qué pasa, quería enseñarte algo.» (6,4 s) · pose |
| 0:34,4 | efecto | Diálogo: DG_Oliver_Menus con NPC_Oliver (+1 reaccionando) |
| 0:34,7 | oliver | «Pues vamos a ver qué pasa.» (1,9 s) · pose |
| 0:36,9 | will | «Vamos.» (1,2 s) · pose |
| 0:39,0 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### will (Player)

- 0:07,7 gesto HeadNod01
- 0:11,7 gesto Question01
- 0:16,7 gesto TakeDamage_2
- 0:17,6 gesto Dizzy_NoWeapon
- 0:19,6 gesto Laugh01
- 0:21,8–0:27,7 habla
- 0:21,9 gesto Laugh01
- 0:24,5 gesto Talk01
- 0:28,7 gesto Question01
- 0:35,4 gesto HeadNod01
- 0:36,9–0:38,1 habla
- 0:37,0 gesto HeadNod01

### oliver (NPC_Oliver)

- 0:00,0–0:02,6 habla
- 0:00,1 gesto Cheer02
- 0:02,6–0:07,0 anda de (5,9, -143,2) a (1,8, -134,1)
- 0:07,0–0:11,5 habla
- 0:07,1 gesto Dizzy_NoWeapon
- 0:08,6 gesto Talk01
- 0:11,7–0:14,2 habla
- 0:11,8 gesto Talk01
- 0:14,2 bucle Conjuro_Recogerse
- 0:15,2 bucle Conjuro_Invocar
- 0:16,1 reposo 
- 0:16,2 gesto MagicSpecial
- 0:16,7 gesto TakeDamage_2
- 0:17,6 gesto Dizzy_NoWeapon
- 0:19,6 gesto Laugh01
- 0:21,8 gesto HeadShake01
- 0:22,5 gesto Question01
- 0:28,0–0:34,4 habla
- 0:28,1 gesto Shrug01
- 0:29,8 gesto Talk01
- 0:32,3 gesto Talk02
- 0:34,7–0:36,6 habla
- 0:34,8 gesto HandWave01
- 0:37,4 gesto HeadNod01

