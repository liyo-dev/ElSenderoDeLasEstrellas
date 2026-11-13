# Parte de horneado: Cap1_EldranCaja

2026-11-13 13:20 · guion `Assets/_SEQUENCES/Guiones/Cap1_EldranCaja.guion.txt`

**0:18,1** de secuencia · 4 frases · 3 planos · 3 efectos · 3 personajes

Reparto medido (m): will 1,62/ojos 0,93 · eldran 1,55/ojos 0,94 · tomasa 1,55/ojos 0,98

## Errores (0)


## Avisos (3)

- línea 17: 'Player' no tiene punto de aparición en MainWorld; necesita un «en» al principio.
- línea 39: plano dos will tomasa desde=cam_tomasa altura=1.6 lente=34 quieta: algo del decorado se mete delante y tapa un 100% del cuadro. Prueba lado= o desde=.
- línea 39: plano dos will tomasa desde=cam_tomasa altura=1.6 lente=34 quieta: will habla de espaldas a la cámara en 6,5 s (121°). Prueba lado=, otro desde= o un plano nuevo cuando se gire.

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | **@tomasa** |  |
| 0:00,0 | plano 1 | dos: will y tomasa · 11,8 s · lente 34° |
| 0:00,0 | tomasa | «TOMASA_ESTRELLA_1» (4,3 s) · pose |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
| 0:04,6 | will | «WILL_ESTRELLA_1» (3,8 s) · pose |
| 0:08,7 | tomasa | «TOMASA_ESTRELLA_2» (3,1 s) · pose |
| 0:11,8 | **@llamada** |  |
| 0:11,8 | plano 2 | general de eldran y will · 5,1 s · lente 40° · coste 529: cara 99, ángulo 72, eje 350 |
| 0:11,8 | eldran | «DLG_ELDRAN_CAJA_LLAMADA» (1,9 s) · pose |
| 0:13,7 | **@acerca** |  |
| 0:16,9 | **@charla** |  |
| 0:16,9 | plano 3 | dos: eldran y will · 1,2 s · lente 34° · coste 994: cara 907, ángulo 81 |
| 0:16,9 | efecto | Diálogo: DG_ELDRAN_SIGUEME con NPC_Eldran (+1 reaccionando) |
| 0:18,1 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### will (Player)

- 0:04,6–0:08,4 habla
- 0:04,7 gesto Grito01
- 0:06,5 gesto Talk01

### eldran (NPC_Eldran)

- 0:11,8–0:13,7 habla
- 0:11,9 gesto Cheer01
- 0:13,7–0:16,9 anda de (7,0, -137,0) a (11,5, -136,0)
- 0:16,9 bucle BrazosEnJarras

### tomasa (NPC_Tomasa)

- 0:00,0–0:04,3 habla
- 0:00,1 gesto Talk01
- 0:02,5 gesto Talk03
- 0:08,7–0:11,8 habla
- 0:08,8 gesto Laugh01

