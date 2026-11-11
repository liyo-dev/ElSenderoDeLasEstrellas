# Parte de horneado: Cap1_OliverSaludo

2026-11-11 18:11 · guion `Assets/_SEQUENCES/Guiones/Cap1_OliverSaludo.guion.txt`

**0:38,9** de secuencia · 7 frases · 2 planos · 5 efectos · 2 personajes

Reparto medido (m): will 1,62/ojos 0,93 · oliver 1,70/ojos 0,94

## Errores (0)


## Avisos (4)

- línea 18: 'Player' no tiene punto de aparición en MainWorld; necesita un «en» al principio.
- línea 37: oliver corre a p_llega llega_mirando=will: un punto queda fuera del NavMesh (1,8, -134,1); voy en línea recta
- línea 42: plano conjunto oliver will lente=40 quieta: will habla de espaldas a la cámara en 24,6 s (122°). Prueba lado=, otro desde= o un plano nuevo cuando se gire.
- línea 42: plano conjunto oliver will lente=40 quieta: will habla de espaldas a la cámara en 37,4 s (122°). Prueba lado=, otro desde= o un plano nuevo cuando se gire.

## Silencios largos entre frases (2)

- 0:02,6 → 0:06,9: 4,3 s sin voz entre OLIVER_GREETING_SPOT_WILL y OLIVER_GREETING_OUT_OF_BREATH
- 0:14,1 → 0:21,7: 7,6 s sin voz entre OLIVER_GREETING_NEW_SPELL_INTRO y WILL_GREETING_OLIVER_CUT_OFF

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | plano 1 | general de oliver y will · 6,9 s · lente 40° · coste 60: ángulo 54 |
| 0:00,0 | oliver | «¡Will! ¡Espera!» (2,6 s) · pose |
| 0:02,6 | **@carrera** |  |
| 0:06,9 | **@charla** |  |
| 0:06,9 | plano 2 | conjunto de oliver y will · 32,0 s · lente 40° · coste 204: cara 171, ángulo 27 |
| 0:06,9 | oliver | «Espera un momento... Necesito recuperar el aliento.» (4,5 s) · pose |
| 0:11,6 | oliver | «He aprendido un hechizo nuevo. Mira.» (2,5 s) · pose |
| 0:16,1 | **@hechizo** |  |
| 0:16,6 | efecto | VFX: FX_Smoke_Small en NPC_Oliver |
| 0:16,6 | efecto | VFX: Sparks explode red en NPC_Oliver |
| 0:16,6 | efecto | SFX: SpellFail |
| 0:16,6 | efecto | Sacudida de cámara (0,35, 0,4s) |
| 0:21,7 | will | «¡Ja, sigues igual! ¿Por qué discuten Eldran y Victoria? Se les oye desde aquí.» (5,9 s) · pose |
| 0:27,5 | **@menus** |  |
| 0:27,9 | oliver | «¿Siguen discutiendo? Llevan así un buen rato... Antes de ir a ver qué pasa, quería enseñarte algo.» (6,4 s) · pose |
| 0:34,3 | efecto | Diálogo: DG_Oliver_Menus con NPC_Oliver (+1 reaccionando) |
| 0:34,6 | oliver | «Pues vamos a ver qué pasa.» (1,9 s) · pose |
| 0:36,8 | will | «Vamos.» (1,2 s) · pose |

## Qué hace cada personaje

### will (Player)

- 0:07,6 gesto HeadNod01
- 0:11,6 gesto Question01
- 0:16,6 gesto TakeDamage_2
- 0:17,5 gesto Dizzy_NoWeapon
- 0:19,5 gesto Laugh01
- 0:21,7–0:27,5 habla
- 0:21,8 gesto Laugh01
- 0:24,4 gesto Talk01
- 0:28,6 gesto Question01
- 0:35,3 gesto HeadNod01
- 0:36,8–0:38,0 habla
- 0:36,9 gesto HeadNod01

### oliver (NPC_Oliver)

- 0:00,0–0:02,6 habla
- 0:00,1 gesto Cheer02
- 0:02,6–0:06,9 anda de (6,0, -143,1) a (1,8, -134,1)
- 0:06,9–0:11,4 habla
- 0:07,0 gesto Dizzy_NoWeapon
- 0:08,5 gesto Talk01
- 0:11,6–0:14,1 habla
- 0:11,6 gesto Talk01
- 0:14,1 bucle Conjuro_Recogerse
- 0:15,1 bucle Conjuro_Invocar
- 0:16,0 reposo 
- 0:16,1 gesto MagicSpecial
- 0:16,6 gesto TakeDamage_2
- 0:17,5 gesto Dizzy_NoWeapon
- 0:19,5 gesto Laugh01
- 0:21,7 gesto HeadShake01
- 0:22,4 gesto Question01
- 0:27,9–0:34,3 habla
- 0:28,0 gesto Shrug01
- 0:29,7 gesto Talk01
- 0:32,2 gesto Talk02
- 0:34,6–0:36,5 habla
- 0:34,7 gesto HandWave01
- 0:37,3 gesto HeadNod01

