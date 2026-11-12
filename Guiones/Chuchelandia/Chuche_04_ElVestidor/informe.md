# Parte de horneado: Chuche_04_ElVestidor

2026-11-12 07:30 · guion `Assets/_SEQUENCES/Guiones/Chuche_04_ElVestidor.guion.txt`

**0:45,0** de secuencia · 11 frases · 9 planos · 13 efectos · 4 personajes

Reparto medido (m): will 1,62/ojos 0,93 · estela 1,53/ojos 0,94 · liam 1,68/ojos 0,93 · condesa 1,55/ojos 1,17

## Errores (0)


## Avisos (5)

- línea 20: 'Player' no tiene punto de aparición en Chuchelandia; necesita un «en» al principio.
- línea 72: estela anda a p_fuera ritmo=paseo llega_mirando=will: no se llega del todo a (102,4, 37,0) por el NavMesh
- línea 40: & plano conjunto will liam p_puerta: algo del decorado se mete delante y tapa un 15% del cuadro. Prueba lado= o desde=.
- línea 67: plano conjunto will liam p_fuera rumbo=270 distancia=5.5: algo del decorado se mete delante y tapa un 17% del cuadro. Prueba lado= o desde=.
- No hay captura del escenario: usa «El Sendero → Guion → 1. Capturar el escenario» para tener los dibujos.

## Silencios largos entre frases (3)

- 0:16,3 → 0:18,6: 2,3 s sin voz entre CHUCHELANDIA_VESTIDOR_06 y CHUCHELANDIA_VESTIDOR_07
- 0:20,5 → 0:31,1: 10,6 s sin voz entre CHUCHELANDIA_VESTIDOR_07 y CHUCHELANDIA_VESTIDOR_08
- 0:35,8 → 0:38,1: 2,3 s sin voz entre CHUCHELANDIA_VESTIDOR_09 y CHUCHELANDIA_VESTIDOR_10

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | plano 1 | conjunto de will y liam · 9,1 s · lente 34° · coste 886: estorbo 624, cara 187, ángulo 68 |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
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
| 0:18,6 | plano 3 | medio de liam · 2,4 s · lente 30° · coste 24: ángulo 24 |
| 0:18,6 | liam | «Le han puesto el lazo.» (1,9 s) |
| 0:21,0 | **@sale** |  |
| 0:21,0 | plano 4 | conjunto de will y liam · 3,5 s · lente 34° · coste 819: estorbo 676, cara 79, ángulo 59 |
| 0:21,0 | efecto | SFX: Chimes_Harp-012 |
| 0:21,0 | efecto | VFX: Shine_pink en M_Vestidor_Puerta |
| 0:21,0 | efecto | VFX: Sparkle_ellow_flying en Actor_Estela |
| 0:24,5 | plano 5 | medio de estela · 1,0 s · lente 30° · coste 14: ángulo 8 |
| 0:25,5 | **@aguanta** |  |
| 0:25,5 | plano 6 | medio de will · 5,6 s · lente 30° · coste 867: cara 787, ángulo 74 |
| 0:28,5 | efecto | SFX: Cartoon_Plastic_Boing |
| 0:28,5 | efecto | Salto: Player 0,4 m |
| 0:28,5 | efecto | SFX: Final_Will_Risa |
| 0:31,1 | **@rosa** |  |
| 0:31,1 | plano 7 | medio de liam · 2,0 s · lente 30° · coste 319: cara 250, ángulo 69 |
| 0:31,1 | liam | «Estás muy rosa.» (2,0 s) |
| 0:33,1 | plano 8 | sobre el hombro de liam a estela · 2,7 s · lente 24° · coste 6: limpio |
| 0:33,1 | estela | «Una palabra más y os prendo fuego.» (2,7 s) |
| 0:35,8 | plano 9 | conjunto de estela y will y liam · 9,2 s · lente 34° · coste 828: estorbo 438, cara 281, ángulo 104 |
| 0:35,8 | efecto | VFX: VFX_Hechizo_CorazonEstelar_Impacto en Actor_Estela |
| 0:35,8 | efecto | SFX: Xylophone-011 |
| 0:38,1 | estela | «Bueno. Os lleno de purpurina.» (3,1 s) |
| 0:41,7 | estela | «Pero con mucha rabia.» (2,0 s) |
| 0:45,0 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### will (Player)

- 0:00,0 bucle ManosALaEspalda
- 0:25,5 reposo 
- 0:28,5 gesto Laugh01
- 0:38,8 gesto HeadNod01

### estela (Actor_Estela)

- 0:03,6–0:06,6 habla
- 0:09,1–0:10,2 habla
- 0:13,8–0:16,3 habla
- 0:21,0 aparece
- 0:21,1–0:24,5 anda de (104,9, 37,0) a (102,4, 37,0)
- 0:24,5 bucle BrazosEnJarras
- 0:33,1–0:35,8 habla
- 0:33,1 gesto Angry01
- 0:35,7 bucle BrazosEnJarras
- 0:35,8 gesto MagicRight
- 0:36,9 bucle BrazosEnJarras
- 0:38,1–0:41,2 habla
- 0:41,7–0:43,7 habla
- 0:41,8 gesto Angry02
- 0:43,0 bucle BrazosEnJarras

### liam (Actor_Liam)

- 0:00,0 bucle BrazosCruzados
- 0:04,3 gesto Sobresalto01
- 0:07,6 gesto HeadNod01
- 0:09,6 gesto Question01
- 0:14,5 gesto HeadNod01
- 0:18,6–0:20,5 habla
- 0:31,1–0:33,1 habla
- 0:33,8 gesto HeadNod01
- 0:42,4 gesto HeadNod01

### condesa (Actor_CondesaMerengue)

- 0:01,2–0:03,3 habla
- 0:06,9–0:09,1 habla
- 0:10,5–0:13,5 habla
- 0:44,5 aparece

