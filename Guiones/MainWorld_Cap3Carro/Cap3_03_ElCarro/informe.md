# Parte de horneado: Cap3_03_ElCarro

2026-11-18 15:25 · guion `Assets/_SEQUENCES/Guiones/Cap3_03_ElCarro.guion.txt`

**0:39,5** de secuencia · 5 frases · 7 planos · 4 efectos · 3 personajes

Reparto medido (m): will 1,62/ojos 0,93 · c1 1,30/ojos 0,97 · c2 1,31/ojos 0,98

## Errores (0)


## Avisos (8)

- línea 18: 'Player' no tiene punto de aparición en MainWorld; necesita un «en» al principio.
- línea 57: will dice CAP3_CARRO_01 cara=decidido: la frase CAP3_CARRO_01 no tiene voz en Voices/es; le doy 2,3 s por su texto
- línea 76: c2 dice CAP3_CARRO_04 cara=molesto mira=fuego: la frase CAP3_CARRO_04 no tiene voz en Voices/es; le doy 2,7 s por su texto
- línea 84: c1 dice CAP3_CARRO_05 cara=molesto gesto=Talk01 mira=c2: la frase CAP3_CARRO_05 no tiene voz en Voices/es; le doy 3,4 s por su texto
- línea 92: will dice CAP3_CARRO_02 cara=pensativo: la frase CAP3_CARRO_02 no tiene voz en Voices/es; le doy 4,6 s por su texto
- línea 93: will dice CAP3_CARRO_03 cara=decidido: la frase CAP3_CARRO_03 no tiene voz en Voices/es; le doy 2,7 s por su texto
- 0:10,1: «general de c1 y c2» dura 9,6 s sin voz y con la cámara quieta. ¿Pasa algo dentro?
- No hay captura del escenario: usa «El Sendero → Cinemáticas → Guion → 1. Capturar el escenario del prólogo» para tener los dibujos.

## Silencios largos entre frases (3)

- 0:03,8 → 0:19,8: 15,9 s sin voz entre CAP3_CARRO_01 y CAP3_CARRO_04
- 0:22,4 → 0:24,2: 1,7 s sin voz entre CAP3_CARRO_04 y CAP3_CARRO_05
- 0:27,5 → 0:31,0: 3,5 s sin voz entre CAP3_CARRO_05 y CAP3_CARRO_02

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | plano 1 | medio de will · 10,1 s · lente 30° · coste 2: ángulo 2 |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
| 0:01,0 | efecto | SFX: Wood_Scratching-009 |
| 0:01,5 | will | «Ahí vienen. El posadero tenía razón.» (2,3 s) · pose |
| 0:03,8 | **@esconde** |  |
| 0:10,1 | **@llegan** |  |
| 0:10,1 | plano 2 | general de c1 y c2 · 9,6 s · lente 40° |
| 0:10,1 | efecto | SFX: Wood_Scratching-009 |
| 0:19,8 | **@vigia** |  |
| 0:19,8 | plano 3 | medio de c2 · 3,0 s · lente 30° · coste 277: cara 213, ángulo 65 |
| 0:19,8 | c2 | «Brasas recientes. Aquí ha estado alguien.» (2,7 s) · pose |
| 0:22,8 | plano 4 | primer plano de will · 1,4 s · lente 24° · coste 9: ángulo 3 |
| 0:24,2 | plano 5 | medio de c1 · 3,4 s · lente 30° · coste 2: ángulo 2 |
| 0:24,2 | c1 | «Algún pastor. Vamos, que nos esperan antes del alba.» (3,4 s) · pose |
| 0:27,5 | **@sevan** |  |
| 0:27,5 | plano 6 | dos: c1 y c2 · 3,5 s · lente 32° · coste 298: cara 227, ángulo 63 |
| 0:31,0 | plano 7 | primer plano de will · 8,5 s · lente 24° · coste 9: ángulo 3 |
| 0:31,0 | will | «Algo brilla bajo la paja… Si llevan animales, quiero ver si tienen aro.» (4,6 s) · pose |
| 0:35,9 | will | «Tengo que ver adónde van. Sin que me vean.» (2,7 s) · pose |
| 0:39,5 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### will (Player)

- 0:00,0 bucle BrazosCruzados
- 0:01,0 reposo 
- 0:01,5–0:03,8 habla
- 0:01,6 gesto Decidido01
- 0:03,9–0:10,2 anda de (-101,1, -208,5) a (-109,5, -212,5)
- 0:20,5 gesto HeadNod01
- 0:31,0–0:35,7 habla
- 0:31,1 gesto Dudar01
- 0:33,9 gesto Talk03
- 0:35,9–0:38,6 habla
- 0:36,0 gesto Decidido01

### c1 (NPC_Contrabandista1)

- 0:10,2–0:18,9 anda de (-84,0, -203,5) a (-92,0, -208,9)
- 0:20,5 gesto HeadNod01
- 0:24,2–0:27,5 habla
- 0:24,2 gesto Talk01

### c2 (NPC_Contrabandista2)

- 0:10,2–0:19,4 anda de (-89,0, -205,5) a (-98,5, -209,4)
- 0:19,8–0:22,4 habla
- 0:19,8 gesto Talk02
- 0:22,4 gesto SenseSomethingStart_NoWeapon
- 0:22,8 gesto SenseSomethingSearching_NoWeapon
- 0:24,9 gesto HeadNod01

