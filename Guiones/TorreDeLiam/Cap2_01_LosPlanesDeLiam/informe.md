# Parte de horneado: Cap2_01_LosPlanesDeLiam

2026-10-07 09:40 · guion `Assets/_SEQUENCES/Guiones/Cap2_01_LosPlanesDeLiam.guion.txt`

**0:45,8** de secuencia · 7 frases · 7 planos · 6 efectos · 2 personajes

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

- 0:04,7 → 0:07,7: 3,0 s sin voz entre EVT_LIAM_CRYSTAL_01 y EVT_LIAM_CRYSTAL_02
- 0:14,0 → 0:17,0: 3,0 s sin voz entre EVT_LIAM_CRYSTAL_02 y EVT_LIAM_CRYSTAL_03
- 0:18,4 → 0:21,4: 3,0 s sin voz entre EVT_LIAM_CRYSTAL_03 y EVT_LIAM_CRYSTAL_04
- 0:23,0 → 0:25,2: 2,2 s sin voz entre EVT_LIAM_CRYSTAL_04 y EVT_LIAM_CRYSTAL_05
- 0:27,8 → 0:30,8: 3,0 s sin voz entre EVT_LIAM_CRYSTAL_05 y EVT_LIAM_CRYSTAL_06
- 0:33,1 → 0:38,4: 5,3 s sin voz entre EVT_LIAM_CRYSTAL_06 y EVT_LIAM_CRYSTAL_07

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | plano 1 | general de liam · 3,0 s · lente 65° |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
| 0:03,0 | **@mira** |  |
| 0:03,0 | plano 2 | primer plano de liam · 12,7 s · lente 40° |
| 0:03,0 | liam | «No ha huido.» (1,7 s) |
| 0:07,7 | liam | «Se ha quedado delante del demonio. Esta mañana ni sabía que tenía magia.» (6,3 s) |
| 0:15,7 | **@cae** |  |
| 0:15,7 | plano 3 | medio de liam · 3,7 s · lente 42° |
| 0:15,7 | efecto | VFX: 1 Flash_magic_ellow_blue en PROP_Bola |
| 0:17,0 | liam | «Levántate…» (1,4 s) |
| 0:19,4 | **@llave** |  |
| 0:19,4 | plano 4 | detalle de el punto · 4,5 s · lente 48° |
| 0:20,4 | efecto | Apagar: PROP_BrilloLlave |
| 0:21,4 | liam | «Lo ha vencido.» (1,6 s) |
| 0:23,9 | **@decide** |  |
| 0:23,9 | plano 5 | medio de liam · 1,3 s · lente 40° |
| 0:25,2 | plano 6 | detalle de el punto · 5,6 s · lente 65° |
| 0:25,2 | liam | «Es él. Él puede abrir el camino.» (2,6 s) |
| 0:30,8 | plano 7 | primer plano de liam · 15,0 s · lente 40° |
| 0:30,8 | liam | «Ojalá hubiera otra manera…» (2,4 s) |
| 0:34,6 | **@final** |  |
| 0:35,6 | efecto | Cut-in 0: TOBI_TORRE (Busto) |
| 0:38,4 | liam | «Resiste, Tobi. Ya casi lo tengo.» (3,4 s) |
| 0:43,8 | efecto | Pantalla: cubrir (1,5s) |
| 0:45,3 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### liam (LIAM_TORRE)

- 0:00,0 bucle SitHigh_Loop
- 0:03,0–0:04,7 habla
- 0:07,7–0:14,0 habla
- 0:17,0–0:18,4 habla
- 0:21,4–0:23,0 habla
- 0:25,2–0:27,8 habla
- 0:30,8–0:33,1 habla
- 0:38,4–0:41,8 habla

### tobi (TOBI_TORRE)

- 0:00,0 bucle SitGround_Loop

