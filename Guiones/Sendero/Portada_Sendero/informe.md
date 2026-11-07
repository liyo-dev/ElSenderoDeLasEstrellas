# Parte de horneado: Portada_Sendero

2026-11-07 12:47 · guion `Assets/_SEQUENCES/Guiones/Escaparate/Portada_Sendero.guion.txt`

**0:01,2** de secuencia · 0 frases · 2 planos · 0 efectos · 3 personajes

Reparto medido (m): will 1,62/ojos 0,93 · estela 1,49/ojos 0,94 · liam 1,68/ojos 0,93

## Errores (0)


## Avisos (11)

- línea 15: 'Player' no tiene punto de aparición en Sendero; necesita un «en» al principio.
- línea 16: 'NPC_Estela' no tiene punto de aparición en Sendero; necesita un «en» al principio.
- línea 16: 'NPC_Estela' no está en el escenario de la secuencia ni hay en Sendero un NPC que se registre con ese id: en juego no saldrá (salvo que lo traiga otra escena o el grafo). Ponlo en el SequenceStage con ese id.
- línea 17: 'NPC_Liam' no tiene punto de aparición en Sendero; necesita un «en» al principio.
- línea 17: 'NPC_Liam' no está en el escenario de la secuencia ni hay en Sendero un NPC que se registre con ese id: en juego no saldrá (salvo que lo traiga otra escena o el grafo). Ponlo en el SequenceStage con ese id.
- El primer plano empieza en 0,5 s: hasta entonces la cámara se queda donde estuviera.
- línea 46: plano fijo desde=cam_frente mira=caras lente=40 altura=0 altura_foco=0 quieta: algo del decorado se mete delante y tapa un 100% del cuadro. Prueba lado= o desde=.
- línea 48: plano fijo desde=cam_cerca mira=mira_cerca lente=40 altura=0 altura_foco=0 quieta: algo del decorado se mete delante y tapa un 100% del cuadro. Prueba lado= o desde=.
- 0:00,5: el plano «fijo a el punto» dura 0,1 s (un corte tan corto se lee como un salto).
- 0:00,6: el plano «fijo a el punto» dura 0,6 s (un corte tan corto se lee como un salto).
- No hay captura del escenario: usa «El Sendero → Guion → 1. Capturar el escenario» para tener los dibujos.

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,5 | plano 1 | fijo a el punto · 0,1 s · lente 40° |
| 0:00,6 | plano 2 | fijo a el punto · 0,6 s · lente 40° |

## Qué hace cada personaje

### will (Player)

- 0:00,1 bucle CastingIdle01

### estela (NPC_Estela)

- 0:00,1 bucle Cheer01

### liam (NPC_Liam)

- 0:00,1 bucle Idle01

