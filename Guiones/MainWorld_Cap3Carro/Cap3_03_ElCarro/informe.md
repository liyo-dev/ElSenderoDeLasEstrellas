# Parte de horneado: Cap3_03_ElCarro

2026-11-18 10:21 · guion `Assets/_SEQUENCES/Guiones/Cap3_03_ElCarro.guion.txt`

**0:40,2** de secuencia · 5 frases · 7 planos · 4 efectos · 3 personajes

Reparto medido (m): will 1,62/ojos 0,93 · c1 1,30/ojos 0,97 · c2 1,31/ojos 0,98

## Errores (0)


## Avisos (5)

- línea 17: 'Player' no tiene punto de aparición en MainWorld; necesita un «en» al principio.
- línea 75: c2 dice CAP3_CARRO_04 cara=molesto mira=fuego: la frase CAP3_CARRO_04 no tiene voz en Voices/es; le doy 2,7 s por su texto
- línea 83: c1 dice CAP3_CARRO_05 cara=molesto gesto=Talk01 mira=c2: la frase CAP3_CARRO_05 no tiene voz en Voices/es; le doy 3,4 s por su texto
- 0:10,0: «general de c1 y c2» dura 9,6 s sin voz y con la cámara quieta. ¿Pasa algo dentro?
- No hay captura del escenario: usa «El Sendero → Cinemáticas → Guion → 1. Capturar el escenario del prólogo» para tener los dibujos.

## Silencios largos entre frases (3)

- 0:03,7 → 0:19,6: 15,9 s sin voz entre CAP3_CARRO_01 y CAP3_CARRO_04
- 0:22,3 → 0:24,0: 1,7 s sin voz entre CAP3_CARRO_04 y CAP3_CARRO_05
- 0:27,4 → 0:30,9: 3,5 s sin voz entre CAP3_CARRO_05 y CAP3_CARRO_02

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | plano 1 | medio de will · 10,0 s · lente 30° · coste 2: ángulo 2 |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
| 0:01,0 | efecto | SFX: Wood_Scratching-009 |
| 0:01,5 | will | «Ruedas… a estas horas.» (2,2 s) · pose |
| 0:03,7 | **@esconde** |  |
| 0:10,0 | **@llegan** |  |
| 0:10,0 | plano 2 | general de c1 y c2 · 9,6 s · lente 40° |
| 0:10,0 | efecto | SFX: Wood_Scratching-009 |
| 0:19,6 | **@vigia** |  |
| 0:19,6 | plano 3 | medio de c2 · 3,0 s · lente 30° · coste 277: cara 213, ángulo 65 |
| 0:19,6 | c2 | «Brasas recientes. Aquí ha estado alguien.» (2,7 s) · pose |
| 0:22,6 | plano 4 | primer plano de will · 1,4 s · lente 24° · coste 9: ángulo 3 |
| 0:24,0 | plano 5 | medio de c1 · 3,4 s · lente 30° · coste 2: ángulo 2 |
| 0:24,0 | c1 | «Algún pastor. Vamos, que nos esperan antes del alba.» (3,4 s) · pose |
| 0:27,4 | **@sevan** |  |
| 0:27,4 | plano 6 | dos: c1 y c2 · 3,5 s · lente 32° · coste 298: cara 227, ángulo 63 |
| 0:30,9 | plano 7 | primer plano de will · 9,3 s · lente 24° · coste 9: ángulo 3 |
| 0:30,9 | will | «La carga tapada y esa luz bajo la paja… lo que contó el posadero.» (4,2 s) · pose |
| 0:35,4 | will | «Voy a seguirles. Sin que me vean.» (3,9 s) · pose |
| 0:40,2 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### will (Player)

- 0:00,0 bucle BrazosCruzados
- 0:01,0 reposo 
- 0:01,5–0:03,7 habla
- 0:01,6 gesto Talk02
- 0:03,8–0:10,1 anda de (-101,1, -208,5) a (-109,5, -212,5)
- 0:20,3 gesto HeadNod01
- 0:30,9–0:35,2 habla
- 0:31,0 gesto Dudar01
- 0:33,8 gesto Talk03
- 0:35,4–0:39,3 habla
- 0:35,5 gesto Decidido01
- 0:37,0 gesto Talk01

### c1 (NPC_Contrabandista1)

- 0:10,1–0:18,8 anda de (-84,1, -203,5) a (-92,0, -208,9)
- 0:20,3 gesto HeadNod01
- 0:24,0–0:27,4 habla
- 0:24,1 gesto Talk01

### c2 (NPC_Contrabandista2)

- 0:10,1–0:19,3 anda de (-89,1, -205,5) a (-98,5, -209,4)
- 0:19,6–0:22,3 habla
- 0:19,7 gesto Talk02
- 0:22,3 gesto SenseSomethingStart_NoWeapon
- 0:22,6 gesto SenseSomethingSearching_NoWeapon
- 0:24,7 gesto HeadNod01

