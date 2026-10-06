# Parte de horneado: Despertar_1_Amenaza

2026-10-06 09:09 · guion `Assets/_SEQUENCES/Guiones/Despertar_1_Amenaza.guion.txt`

**0:13,4** de secuencia · 3 frases · 7 planos · 14 efectos · 2 personajes

Reparto medido (m): will 1,55/ojos 0,93 · eldran 1,55/ojos 0,95

## Errores (0)


## Avisos (1)

- línea 17: 'Player' no tiene punto de aparición en MainWorld; necesita un «en» al principio.

## Silencios largos entre frases (1)

- 0:05,0 → 0:06,6: 1,6 s sin voz entre EVT_AWAKEN_02 y EVT_AWAKEN_06

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | plano 1 | dos: eldran y will · 1,7 s · lente 30° |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
| 0:01,2 | **@aviso** |  |
| 0:01,2 | eldran | «¡Will, cuidado!» (1,9 s) · pose |
| 0:01,2 | efecto | Sacudida de cámara (0,06, 1,8s) |
| 0:01,2 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 0:01,7 | plano 2 | medio de eldran · 1,7 s · lente 34° · coste 2: ángulo 2 |
| 0:03,1 | **@lento** |  |
| 0:03,1 | efecto | Tiempo: ×0,2 (de golpe) |
| 0:03,1 | efecto | Pantalla: cubrir (0,15s) |
| 0:03,3 | efecto | Mecánica: LanzarProyectil |
| 0:03,4 | plano 3 | primer plano de will · 1,6 s · lente 34° · coste 2: ángulo 2 |
| 0:03,4 | efecto | Pantalla: descubrir (0,35s) |
| 0:03,4 | efecto | Sacudida de cámara (0,08, 0,25s) |
| 0:03,6 | will | «¡...!» (1,4 s) · pose |
| 0:05,0 | plano 4 | medio de will · 1,4 s · lente 30° |
| 0:06,4 | **@huida** |  |
| 0:06,4 | plano 5 | siguiendo a eldran · 2,8 s · lente 34° |
| 0:06,4 | efecto | Mecánica: PausarProyectil |
| 0:06,4 | efecto | Tiempo: ×1 (de golpe) |
| 0:06,6 | eldran | «¡Will apartate!» (2,1 s) |
| 0:09,1 | **@despertar** |  |
| 0:09,1 | plano 6 | primer plano de will · 1,6 s · lente 34° · coste 2: ángulo 2 |
| 0:09,1 | efecto | Tiempo: ×0,2 (de golpe) |
| 0:09,8 | efecto | Cut-in 0: Player (Cara) |
| 0:09,8 | efecto | Líneas de concentración: mostrar |
| 0:09,8 | efecto | SFX: Prologo_Carga |
| 0:10,7 | plano 7 | medio de will · 2,7 s · lente 40° · coste 2: ángulo 2 |

## Qué hace cada personaje

### will (Player)

- 0:03,6–0:05,0 habla
- 0:10,7 bucle Idle_Battle_NoWeapon

### eldran (NPC_Eldran)

- 0:01,2–0:03,1 habla
- 0:01,3 gesto SenseSomethingStart_NoWeapon
- 0:06,4–0:08,9 anda de (25,7, -82,4) a (21,3, -85,7)
- 0:06,6–0:08,7 habla

