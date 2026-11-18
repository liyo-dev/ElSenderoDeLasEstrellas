# Parte de horneado: Cap1_OliverSaludo

2026-11-18 15:57 · guion `Assets/_SEQUENCES/Guiones/Cap1_OliverSaludo.guion.txt`

**0:37,3** de secuencia · 8 frases · 2 planos · 6 efectos · 2 personajes

Reparto medido (m): will 1,62/ojos 0,93 · oliver 1,75/ojos 0,94

## Errores (0)


## Avisos (5)

- línea 18: 'Player' no tiene punto de aparición en MainWorld; necesita un «en» al principio.
- línea 41: oliver corre a p_llega llega_mirando=will: no se llega del todo a (1,8, -134,1) por el NavMesh
- línea 50: will dice WILL_GREETING_POR_QUE_DISCUTEN gesto=Question01 cara=alegre: la frase WILL_GREETING_POR_QUE_DISCUTEN no tiene voz en Voices/es; le doy 3,8 s por su texto
- línea 51: oliver dice OLIVER_GREETING_BEFORE_MENUS_HANDOFF gesto=Shrug01: la frase OLIVER_GREETING_BEFORE_MENUS_HANDOFF no tiene voz en Voices/es; le doy 5,1 s por su texto
- línea 84: will dice WILL_GREETING_OLIVER_CUT_OFF gesto=Laugh01: la frase WILL_GREETING_OLIVER_CUT_OFF no tiene voz en Voices/es; le doy 1,2 s por su texto

## Silencios largos entre frases (2)

- 0:02,6 → 0:07,0: 4,4 s sin voz entre OLIVER_GREETING_SPOT_WILL y OLIVER_GREETING_OUT_OF_BREATH
- 0:23,9 → 0:31,5: 7,6 s sin voz entre OLIVER_GREETING_NEW_SPELL_INTRO y WILL_GREETING_OLIVER_CUT_OFF

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | plano 1 | general de oliver y will · 7,0 s · lente 40° |
| 0:00,0 | oliver | «¡Will! ¡Espera!» (2,6 s) · pose |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
| 0:02,6 | **@carrera** |  |
| 0:07,0 | **@charla** |  |
| 0:07,0 | plano 2 | conjunto de oliver y will · 30,3 s · lente 40° |
| 0:07,0 | oliver | «Espera un momento... Necesito recuperar el aliento.» (4,5 s) · pose |
| 0:11,8 | will | «¿Por qué discuten Eldran y Victoria? Se les oye desde aquí.» (3,8 s) · pose |
| 0:16,0 | oliver | «Sí… llevan así un buen rato. Antes de ir a ver qué pasa, quería enseñarte algo.» (5,1 s) · pose |
| 0:21,3 | oliver | «He aprendido un hechizo nuevo. Mira.» (2,5 s) · pose |
| 0:25,9 | **@hechizo** |  |
| 0:26,4 | efecto | VFX: VFX_Hechizo_Fallido en NPC_Oliver |
| 0:26,4 | efecto | SFX: SpellFail |
| 0:26,4 | efecto | Sacudida de cámara (0,35, 0,4s) |
| 0:31,5 | will | «¡Ja, sigues igual!» (1,2 s) · pose |
| 0:32,7 | **@menus** |  |
| 0:32,7 | efecto | Diálogo: DG_Oliver_Menus con NPC_Oliver (+1 reaccionando) |
| 0:33,0 | oliver | «Pues vamos a ver qué pasa.» (1,9 s) · pose |
| 0:35,2 | will | «Vamos.» (1,2 s) · pose |
| 0:37,3 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### will (Player)

- 0:07,7 gesto HeadNod01
- 0:11,8–0:15,7 habla
- 0:11,9 gesto Question01
- 0:14,2 gesto Talk01
- 0:16,7 gesto HeadNod01
- 0:26,4 gesto TakeDamage_2
- 0:27,3 gesto Dizzy_NoWeapon
- 0:29,3 gesto Laugh01
- 0:31,5–0:32,7 habla
- 0:31,5 gesto Laugh01
- 0:33,7 gesto HeadNod01
- 0:35,2–0:36,4 habla
- 0:35,3 gesto HeadNod01

### oliver (NPC_Oliver)

- 0:00,0–0:02,6 habla
- 0:00,1 gesto HandWave02
- 0:02,6–0:07,0 anda de (5,9, -143,2) a (1,8, -134,1)
- 0:07,0–0:11,5 habla
- 0:07,1 gesto Dizzy_NoWeapon
- 0:08,6 gesto Talk01
- 0:12,5 gesto Question01
- 0:16,0–0:21,1 habla
- 0:16,1 gesto Shrug01
- 0:17,8 gesto Talk01
- 0:21,3–0:23,9 habla
- 0:21,4 gesto Talk01
- 0:23,9 bucle Conjuro_Recogerse
- 0:24,9 bucle Conjuro_Invocar
- 0:25,8 reposo 
- 0:25,9 gesto MagicSpecial
- 0:26,4 gesto TakeDamage_2
- 0:27,3 gesto Dizzy_NoWeapon
- 0:29,3 gesto Laugh01
- 0:31,5 gesto HeadShake01
- 0:31,9 gesto Laugh01
- 0:33,0–0:34,8 habla
- 0:33,0 gesto HandWave01
- 0:35,7 gesto HeadNod01

