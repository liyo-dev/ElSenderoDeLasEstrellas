# Parte de horneado: Escaparate_Hero

2026-11-07 10:50 · guion `Assets/_SEQUENCES/Guiones/Escaparate/Escaparate_Hero.guion.txt`

**0:03,8** de secuencia · 0 frases · 1 planos · 0 efectos · 3 personajes

Reparto medido (m): will 1,62/ojos 0,93 · estela 1,49/ojos 0,94 · liam 1,57/ojos 0,93

## Errores (0)


## Avisos (7)

- línea 14: 'Player' no tiene punto de aparición en MainWorld; necesita un «en» al principio.
- línea 15: 'NPC_Estela' no tiene punto de aparición en MainWorld; necesita un «en» al principio.
- línea 15: 'NPC_Estela' no está en el escenario de la secuencia ni hay en MainWorld un NPC que se registre con ese id: en juego no saldrá (salvo que lo traiga otra escena o el grafo). Ponlo en el SequenceStage con ese id.
- línea 16: 'NPC_Liam' no tiene punto de aparición en MainWorld; necesita un «en» al principio.
- línea 16: 'NPC_Liam' no está en el escenario de la secuencia ni hay en MainWorld un NPC que se registre con ese id: en juego no saldrá (salvo que lo traiga otra escena o el grafo). Ponlo en el SequenceStage con ese id.
- El primer plano empieza en 0,1 s: hasta entonces la cámara se queda donde estuviera.
- línea 39: & plano fijo desde=cam_b5 mira=caras_b lente=46 quieta: algo del decorado se mete delante y tapa un 61% del cuadro. Prueba lado= o desde=.

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,1 | plano 1 | fijo a el punto · 3,7 s · lente 46° |

## Qué hace cada personaje

### will (Player)

- 0:00,1 bucle CastingIdle01

### estela (NPC_Estela)

- 0:00,1 bucle SitMedium_Loop

### liam (NPC_Liam)

- 0:00,1 bucle Idle01

