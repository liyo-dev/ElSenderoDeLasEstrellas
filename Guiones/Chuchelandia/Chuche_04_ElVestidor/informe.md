# Parte de horneado: Chuche_04_ElVestidor

2026-11-01 09:26 · guion `Assets/_SEQUENCES/Guiones/Chuche_04_ElVestidor.guion.txt`

**0:45,2** de secuencia · 11 frases · 9 planos · 11 efectos · 4 personajes

Reparto medido (m): will 1,55/ojos 0,93 · estela 1,68/ojos 0,95 · liam 1,51/ojos 0,94 · condesa 1,55/ojos 1,17

## Errores (0)


## Avisos (4)

- línea 20: 'Player' no tiene punto de aparición en Chuchelandia; necesita un «en» al principio.
- línea 70: estela anda a p_fuera ritmo=paseo llega_mirando=will: no se llega del todo a (102,4, 37,0) por el NavMesh
- línea 40: & plano conjunto will liam p_puerta: algo del decorado se mete delante y tapa un 12% del cuadro. Prueba lado= o desde=.
- No hay captura del escenario: usa «El Sendero → Guion → 1. Capturar el escenario» para tener los dibujos.

## Silencios largos entre frases (3)

- 0:16,3 → 0:18,6: 2,3 s sin voz entre CHUCHELANDIA_VESTIDOR_06 y CHUCHELANDIA_VESTIDOR_07
- 0:20,5 → 0:31,3: 10,8 s sin voz entre CHUCHELANDIA_VESTIDOR_07 y CHUCHELANDIA_VESTIDOR_08
- 0:36,0 → 0:38,3: 2,3 s sin voz entre CHUCHELANDIA_VESTIDOR_09 y CHUCHELANDIA_VESTIDOR_10

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | plano 1 | conjunto de will y liam · 9,1 s · lente 34° · coste 901: estorbo 558, cara 188, ángulo 68 |
| 0:00,0 | efecto | Señal: CHUCHE_ATUENDO_CANDIDATA |
| 0:01,2 | **@voces** |  |
| 0:01,2 | condesa | «¡Levante los brazos, alteza!» (2,1 s) |
| 0:01,2 | efecto | SFX: Object_Moving_Action-015 |
| 0:03,6 | estela | «¡No soy alteza!» (3,0 s) |
| 0:06,9 | condesa | «¡Más volantes!» (2,2 s) |
| 0:09,1 | plano 2 | dos: will y liam · 9,5 s · lente 32° · coste 374: ángulo 18, eje 350 |
| 0:09,1 | estela | «¿¡Más!?» (1,1 s) |
| 0:10,5 | condesa | «¡El lazo! ¡Traed el lazo grande!» (3,0 s) |
| 0:13,8 | estela | «Como me pongáis ese lazo…» (2,5 s) |
| 0:17,7 | efecto | SFX: Cartoon_Rope_Swirl(4) |
| 0:18,6 | plano 3 | medio de liam · 2,4 s · lente 30° · coste 15: ángulo 15 |
| 0:18,6 | liam | «Le han puesto el lazo.» (1,9 s) · pose |
| 0:21,0 | **@sale** |  |
| 0:21,0 | plano 4 | conjunto de will y liam · 3,5 s · lente 34° · coste 910: estorbo 397, cara 144, ángulo 14, eje 350 |
| 0:21,0 | efecto | SFX: Chimes_Harp-012 |
| 0:21,0 | efecto | VFX: Shine_pink en M_Vestidor_Puerta |
| 0:21,0 | efecto | VFX: Sparkle_ellow_flying en Actor_Estela |
| 0:24,5 | plano 5 | medio de estela · 1,0 s · lente 30° · coste 8: ángulo 8 |
| 0:25,5 | **@aguanta** |  |
| 0:25,5 | plano 6 | medio de will · 5,8 s · lente 30° · coste 43: cara 25, ángulo 12 |
| 0:28,5 | efecto | SFX: Cartoon_Plastic_Boing |
| 0:28,5 | efecto | Salto: Player 0,4 m |
| 0:28,5 | efecto | SFX: Final_Will_Risa |
| 0:31,3 | **@rosa** |  |
| 0:31,3 | plano 7 | medio de liam · 2,0 s · lente 30° · coste 327: cara 250, ángulo 69 |
| 0:31,3 | liam | «Estás muy rosa.» (2,0 s) · pose |
| 0:33,3 | plano 8 | sobre el hombro de liam a estela · 2,7 s · lente 24° · coste 0: limpio |
| 0:33,3 | estela | «Una palabra más y os prendo fuego.» (2,7 s) · pose |
| 0:36,0 | plano 9 | conjunto de estela y will y liam · 9,2 s · lente 34° · coste 308: cara 208, ángulo 94 |
| 0:36,0 | efecto | VFX: VFX_Hechizo_CorazonEstelar_Impacto en Actor_Estela |
| 0:36,0 | efecto | SFX: Xylophone-011 |
| 0:38,3 | estela | «Bueno. Os lleno de purpurina.» (3,1 s) |
| 0:41,9 | estela | «Pero con mucha rabia.» (2,0 s) · pose |

## Qué hace cada personaje

### will (Player)

- 0:04,3 gesto SenseSomethingStart_NoWeapon
- 0:07,6 gesto HeadNod01
- 0:09,6 gesto Question01
- 0:14,5 gesto HeadNod01
- 0:19,3 gesto HeadNod01
- 0:28,5 gesto Laugh01
- 0:39,0 gesto HeadNod01
- 0:42,6 gesto HeadNod01

### estela (Actor_Estela)

- 0:03,6–0:06,6 habla
- 0:09,1–0:10,2 habla
- 0:13,8–0:16,3 habla
- 0:21,0 aparece
- 0:21,1–0:24,5 anda de (104,9, 37,0) a (102,4, 37,0)
- 0:32,0 gesto HeadNod01
- 0:33,3–0:36,0 habla
- 0:33,4 gesto Angry01
- 0:36,0 gesto MagicRight
- 0:38,3–0:41,4 habla
- 0:41,9–0:43,9 habla
- 0:42,0 gesto Angry02

### liam (Actor_Liam)

- 0:04,3 gesto SenseSomethingStart_NoWeapon
- 0:07,6 gesto HeadNod01
- 0:09,6 gesto Question01
- 0:14,5 gesto HeadNod01
- 0:18,6–0:20,5 habla
- 0:18,7 gesto Talk01
- 0:31,3–0:33,3 habla
- 0:31,4 gesto Talk03
- 0:34,0 gesto HeadNod01
- 0:39,0 gesto HeadNod01
- 0:42,6 gesto HeadNod01

### condesa (Actor_CondesaMerengue)

- 0:01,2–0:03,3 habla
- 0:06,9–0:09,1 habla
- 0:10,5–0:13,5 habla
- 0:44,7 aparece

