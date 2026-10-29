# Parte de horneado: Cap2_01_LosPlanesDeLiam

2026-10-27 11:53 · guion `Assets/_SEQUENCES/Guiones/Cap2_01_LosPlanesDeLiam.guion.txt`

**0:46,2** de secuencia · 7 frases · 7 planos · 6 efectos · 2 personajes

Reparto medido (m): liam 1,51/ojos 0,94 · tobi 1,30/ojos 0,81

## Errores (0)


## Avisos (12)

- línea 58: & liam mira PROP_Llave: liam está sentado; no se gira en la silla
- línea 64: liam mira PROP_Bola: liam está sentado; no se gira en la silla
- línea 68: liam mira PROP_Mapa: liam está sentado; no se gira en la silla
- línea 71: liam mira PROP_Bola: liam está sentado; no se gira en la silla
- línea 77: liam mira manos: liam está sentado; no se gira en la silla
- línea 35: plano general liam mira=foco_general altura_foco=0 desde=cam_general altura=1.5 lente=65: algo del decorado se mete delante y tapa un 88% del cuadro. Prueba lado= o desde=.
- línea 39: plano primer liam desde=cam_cara altura=1.25 lente=40: algo del decorado se mete delante y tapa un 100% del cuadro. Prueba lado= o desde=.
- línea 47: plano medio liam PROP_Bola desde=cam_general altura=1.4 lente=42: algo del decorado se mete delante y tapa un 100% del cuadro. Prueba lado= o desde=.
- línea 55: plano detalle detalle_llave altura_foco=0 desde=cam_llave altura=1.0 lente=48: algo del decorado se mete delante y tapa un 100% del cuadro. Prueba lado= o desde=.
- línea 65: plano medio liam desde=cam_sienta altura=1.25 lente=40: algo del decorado se mete delante y tapa un 100% del cuadro. Prueba lado= o desde=.
- línea 67: plano detalle PROP_Mapa desde=cam_mapa altura=1.9 lente=65: algo del decorado se mete delante y tapa un 100% del cuadro. Prueba lado= o desde=.
- línea 72: plano primer liam desde=cam_cara altura=1.25 lente=40: algo del decorado se mete delante y tapa un 100% del cuadro. Prueba lado= o desde=.

## Silencios largos entre frases (6)

- 0:04,8 → 0:07,8: 3,0 s sin voz entre EVT_LIAM_CRYSTAL_01 y EVT_LIAM_CRYSTAL_02
- 0:14,1 → 0:17,1: 3,0 s sin voz entre EVT_LIAM_CRYSTAL_02 y EVT_LIAM_CRYSTAL_03
- 0:18,6 → 0:21,6: 3,0 s sin voz entre EVT_LIAM_CRYSTAL_03 y EVT_LIAM_CRYSTAL_04
- 0:23,2 → 0:25,4: 2,2 s sin voz entre EVT_LIAM_CRYSTAL_04 y EVT_LIAM_CRYSTAL_05
- 0:28,0 → 0:31,0: 3,0 s sin voz entre EVT_LIAM_CRYSTAL_05 y EVT_LIAM_CRYSTAL_06
- 0:33,4 → 0:38,7: 5,3 s sin voz entre EVT_LIAM_CRYSTAL_06 y EVT_LIAM_CRYSTAL_07

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | plano 1 | general de liam · 3,0 s · lente 65° |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
| 0:03,0 | **@mira** |  |
| 0:03,0 | plano 2 | primer plano de liam · 12,8 s · lente 40° |
| 0:03,0 | liam | «No ha huido.» (1,8 s) |
| 0:07,8 | liam | «Se ha quedado delante del demonio. Esta mañana ni sabía que tenía magia.» (6,3 s) |
| 0:15,8 | **@cae** |  |
| 0:15,8 | plano 3 | medio de liam · 3,8 s · lente 42° |
| 0:15,8 | efecto | VFX: 1 Flash_magic_ellow_blue en PROP_Bola |
| 0:17,1 | liam | «Levántate…» (1,5 s) |
| 0:19,6 | **@llave** |  |
| 0:19,6 | plano 4 | detalle de el punto · 4,5 s · lente 48° |
| 0:20,6 | efecto | Apagar: PROP_BrilloLlave |
| 0:21,6 | liam | «Lo ha vencido.» (1,6 s) |
| 0:24,1 | **@decide** |  |
| 0:24,1 | plano 5 | medio de liam · 1,3 s · lente 40° |
| 0:25,4 | plano 6 | detalle de el punto · 5,6 s · lente 65° |
| 0:25,4 | liam | «Es él. Él puede abrir el camino.» (2,6 s) |
| 0:31,0 | plano 7 | primer plano de liam · 15,2 s · lente 40° |
| 0:31,0 | liam | «Ojalá hubiera otra manera…» (2,4 s) |
| 0:34,9 | **@final** |  |
| 0:35,9 | efecto | Cut-in 0: TOBI_TORRE (Busto) |
| 0:38,7 | liam | «Resiste, Tobi. Ya casi lo tengo.» (3,4 s) |
| 0:44,2 | efecto | Pantalla: cubrir (1,5s) |
| 0:45,7 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### liam (LIAM_TORRE)

- 0:00,0 bucle SitMedium_Loop
- 0:03,0–0:04,8 habla
- 0:07,8–0:14,1 habla
- 0:17,1–0:18,6 habla
- 0:21,6–0:23,2 habla
- 0:25,4–0:28,0 habla
- 0:31,0–0:33,4 habla
- 0:38,7–0:42,2 habla

### tobi (TOBI_TORRE)

- 0:00,0 bucle SitGround_Loop

