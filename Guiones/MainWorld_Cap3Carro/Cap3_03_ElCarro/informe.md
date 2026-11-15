# Parte de horneado: Cap3_03_ElCarro

2026-11-14 17:03 · guion `Assets/_SEQUENCES/Guiones/Cap3_03_ElCarro.guion.txt`

**0:38,0** de secuencia · 5 frases · 7 planos · 4 efectos · 3 personajes

Reparto medido (m): will 1,62/ojos 0,93 · c1 1,30/ojos 0,97 · c2 1,31/ojos 0,98

## Errores (0)


## Avisos (8)

- línea 17: 'Player' no tiene punto de aparición en MainWorld; necesita un «en» al principio.
- línea 53: will dice CAP3_CARRO_01 cara=preocupado: la frase CAP3_CARRO_01 no tiene voz en Voices/es; le doy 1,4 s por su texto
- línea 70: c2 dice CAP3_CARRO_04 cara=molesto mira=fuego: la frase CAP3_CARRO_04 no tiene voz en Voices/es; le doy 2,7 s por su texto
- línea 78: c1 dice CAP3_CARRO_05 cara=molesto gesto=Talk01 mira=c2: la frase CAP3_CARRO_05 no tiene voz en Voices/es; le doy 3,4 s por su texto
- línea 86: will dice CAP3_CARRO_02 cara=pensativo: la frase CAP3_CARRO_02 no tiene voz en Voices/es; le doy 4,2 s por su texto
- línea 87: will dice CAP3_CARRO_03 cara=decidido: la frase CAP3_CARRO_03 no tiene voz en Voices/es; le doy 2,1 s por su texto
- 0:09,5: «conjunto de will y c1 y c2» dura 9,6 s sin voz y con la cámara quieta. ¿Pasa algo dentro?
- No hay captura del escenario: usa «El Sendero → Guion → 1. Capturar el escenario» para tener los dibujos.

## Silencios largos entre frases (3)

- 0:02,9 → 0:19,2: 16,2 s sin voz entre CAP3_CARRO_01 y CAP3_CARRO_04
- 0:21,8 → 0:23,6: 1,7 s sin voz entre CAP3_CARRO_04 y CAP3_CARRO_05
- 0:27,0 → 0:30,5: 3,5 s sin voz entre CAP3_CARRO_05 y CAP3_CARRO_02

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | plano 1 | medio de will · 9,5 s · lente 30° · coste 277: cara 217, ángulo 60 |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
| 0:01,0 | efecto | SFX: Wood_Scratching-009 |
| 0:01,5 | will | «Ruedas… a estas horas.» (1,4 s) · pose |
| 0:02,9 | **@esconde** |  |
| 0:09,5 | **@llegan** |  |
| 0:09,5 | plano 2 | conjunto de will y c1 y c2 · 9,6 s · lente 34° · coste 270: cara 192, ángulo 72 |
| 0:09,5 | efecto | SFX: Wood_Scratching-009 |
| 0:19,2 | **@vigia** |  |
| 0:19,2 | plano 3 | medio de c2 · 3,0 s · lente 30° · coste 352: ángulo 2, eje 350 |
| 0:19,2 | c2 | «Brasas recientes. Aquí ha estado alguien.» (2,7 s) · pose |
| 0:22,2 | plano 4 | primer plano de will · 1,4 s · lente 24° · coste 9: ángulo 3 |
| 0:23,6 | plano 5 | medio de c1 · 3,4 s · lente 30° · coste 2: ángulo 2 |
| 0:23,6 | c1 | «Algún pastor. Vamos, que nos esperan antes del alba.» (3,4 s) · pose |
| 0:27,0 | **@sevan** |  |
| 0:27,0 | plano 6 | conjunto de will y c1 y c2 · 3,5 s · lente 34° · coste 374: cara 269, ángulo 99 |
| 0:30,5 | plano 7 | primer plano de will · 7,5 s · lente 24° · coste 24: ángulo 24 |
| 0:30,5 | will | «La carga tapada y esa luz bajo la paja… lo que contó el posadero.» (4,2 s) · pose |
| 0:34,9 | will | «Voy a seguirles. Sin que me vean.» (2,1 s) · pose |
| 0:38,0 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### will (Player)

- 0:00,0 bucle BrazosCruzados
- 0:01,0 reposo 
- 0:01,5–0:02,9 habla
- 0:01,6 gesto Talk02
- 0:03,0–0:09,6 anda de (-101,1, -208,6) a (-109,5, -212,5)
- 0:19,9 gesto HeadNod01
- 0:30,5–0:34,7 habla
- 0:30,5 gesto Dudar01
- 0:33,3 gesto Talk03
- 0:34,9–0:37,1 habla
- 0:35,0 gesto Decidido01

### c1 (NPC_Contrabandista1)

- 0:09,6–0:18,3 anda de (-84,0, -203,5) a (-92,0, -208,9)
- 0:19,9 gesto HeadNod01
- 0:23,6–0:27,0 habla
- 0:23,7 gesto Talk01

### c2 (NPC_Contrabandista2)

- 0:09,6–0:18,8 anda de (-89,0, -205,5) a (-98,5, -209,4)
- 0:19,2–0:21,8 habla
- 0:19,3 gesto Talk02
- 0:21,8 gesto SenseSomethingStart_NoWeapon
- 0:22,2 gesto SenseSomethingSearching_NoWeapon
- 0:24,3 gesto HeadNod01

