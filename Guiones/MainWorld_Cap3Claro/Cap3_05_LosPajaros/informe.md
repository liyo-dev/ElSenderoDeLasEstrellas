# Parte de horneado: Cap3_05_LosPajaros

2026-11-11 00:48 · guion `Assets/_SEQUENCES/Guiones/Cap3_05_LosPajaros.guion.txt`

**0:32,9** de secuencia · 7 frases · 5 planos · 6 efectos · 6 personajes

Reparto medido (m): will 1,62/ojos 0,93 · sargento 1,51/ojos 0,94 · g1 1,51/ojos 0,94 · g2 1,51/ojos 0,94 · jefe 1,31/ojos 0,98 · c2 1,31/ojos 0,98

## Errores (0)


## Avisos (9)

- línea 10: 'Player' no tiene punto de aparición en MainWorld; necesita un «en» al principio.
- línea 43: sargento dice CAP3_PAJAROS_01 cara=enfado: la frase CAP3_PAJAROS_01 no tiene voz en Voices/es; le doy 2,5 s por su texto
- línea 44: jefe dice CAP3_PAJAROS_02 cara=miedo: la frase CAP3_PAJAROS_02 no tiene voz en Voices/es; le doy 1,2 s por su texto
- línea 45: sargento dice CAP3_PAJAROS_03 cara=decidido: la frase CAP3_PAJAROS_03 no tiene voz en Voices/es; le doy 2,2 s por su texto
- línea 51: will dice CAP3_PAJAROS_04 cara=alegre: la frase CAP3_PAJAROS_04 no tiene voz en Voices/es; le doy 1,2 s por su texto
- línea 65: sargento dice CAP3_PAJAROS_05 cara=aliviado: la frase CAP3_PAJAROS_05 no tiene voz en Voices/es; le doy 4,0 s por su texto
- línea 66: sargento dice CAP3_PAJAROS_06 gesto=HeadNod01: la frase CAP3_PAJAROS_06 no tiene voz en Voices/es; le doy 4,4 s por su texto
- línea 67: will dice CAP3_PAJAROS_07 cara=agradecido: la frase CAP3_PAJAROS_07 no tiene voz en Voices/es; le doy 1,2 s por su texto
- No hay captura del escenario: usa «El Sendero → Guion → 1. Capturar el escenario» para tener los dibujos.

## Silencios largos entre frases (2)

- 0:07,8 → 0:15,4: 7,5 s sin voz entre CAP3_PAJAROS_03 y CAP3_PAJAROS_04
- 0:16,6 → 0:20,1: 3,5 s sin voz entre CAP3_PAJAROS_04 y CAP3_PAJAROS_05

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | plano 1 | general de sargento y jefe y c2 · 7,8 s · lente 42° · coste 61: cara 6, ángulo 50 |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
| 0:01,0 | sargento | «¡Guardia del cruce! Que nadie se mueva.» (2,5 s) · pose |
| 0:04,0 | jefe | «Solo son pájaros…» (1,2 s) |
| 0:05,6 | sargento | «Pájaros que no se venden. Atadlos.» (2,2 s) · pose |
| 0:07,8 | **@jaulas** |  |
| 0:07,8 | plano 2 | siguiendo a will · 7,5 s · lente 34° |
| 0:15,4 | plano 3 | medio de will · 1,2 s · lente 30° · coste 2: ángulo 2 |
| 0:15,4 | will | «Ya está. Volad.» (1,2 s) · pose |
| 0:16,6 | plano 4 | general de will · 3,5 s · lente 42° · coste 9: ángulo 9 |
| 0:16,6 | efecto | VFX: Sparkle_ellow_flying en jaulas |
| 0:17,6 | efecto | VFX: Sparkle_ellow_flying en jaulas |
| 0:18,6 | efecto | VFX: Shine_ellow en Player |
| 0:20,1 | **@sargento** |  |
| 0:20,1 | plano 5 | dos: will y sargento · 12,8 s · lente 32° · coste 521: delante 327, cara 182, ángulo 4 |
| 0:20,1 | sargento | «Avisaste en vez de liarte a golpes. Eso, chico, es lo difícil.» (4,0 s) · pose |
| 0:24,4 | sargento | «Si vas al Bosque Prohibido, ve de día. Y no toques nada que brille.» (4,4 s) · pose |
| 0:29,2 | will | «Gracias. Lo haré.» (1,2 s) · pose |
| 0:31,2 | efecto | Pantalla: cubrir (1,2s) |
| 0:32,9 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### will (Player)

- 0:04,5 gesto HeadNod01
- 0:06,3 gesto HeadShake01
- 0:07,9–0:15,4 anda de (-207,5, -132,0) a (-216,0, -124,5)
- 0:15,4–0:16,6 habla
- 0:15,5 gesto Talk01
- 0:20,8 gesto HeadNod01
- 0:25,1 gesto HeadShake01
- 0:29,2–0:30,4 habla
- 0:29,3 gesto Talk03

### sargento (NPC_Sargento)

- 0:01,0–0:03,5 habla
- 0:01,1 gesto Talk03
- 0:04,5 gesto HeadNod01
- 0:05,6–0:07,8 habla
- 0:05,7 gesto Talk01
- 0:15,9 gesto HeadNod01
- 0:20,1–0:24,1 habla
- 0:20,2 gesto Talk02
- 0:22,7 gesto Talk03
- 0:24,4–0:28,8 habla
- 0:24,5 gesto HeadNod01
- 0:25,8 gesto Talk03
- 0:29,7 gesto HeadNod01

### g1 (NPC_GuardiaCruce1)

- 0:00,0 bucle Idle_Battle_NoWeapon
- 0:04,5 gesto HeadNod01
- 0:06,3 gesto HeadShake01
- 0:15,9 gesto HeadNod01
- 0:20,8 gesto HeadNod01
- 0:25,1 gesto HeadShake01
- 0:29,7 gesto HeadNod01

### g2 (NPC_GuardiaCruce2)

- 0:00,0 bucle Idle_Battle_NoWeapon
- 0:04,5 gesto HeadNod01
- 0:15,9 gesto HeadNod01
- 0:29,7 gesto HeadNod01

### jefe (NPC_ContrabandistaJefe)

- 0:00,0 bucle Beg01_Loop
- 0:04,0–0:05,2 habla
- 0:06,3 gesto HeadShake01
- 0:15,9 gesto HeadNod01
- 0:20,8 gesto HeadNod01
- 0:25,1 gesto HeadShake01
- 0:29,7 gesto HeadNod01

### c2 (NPC_Contrabandista2)

- 0:00,0 bucle Beg01_Loop
- 0:04,5 gesto HeadNod01
- 0:06,3 gesto HeadShake01
- 0:15,9 gesto HeadNod01
- 0:20,8 gesto HeadNod01
- 0:25,1 gesto HeadShake01
- 0:29,7 gesto HeadNod01

