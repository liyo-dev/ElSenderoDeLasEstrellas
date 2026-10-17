# Parte de horneado: Prologo_UltimaNoche

2026-10-17 21:11 · guion `Assets/_SEQUENCES/Guiones/Prologo_UltimaNoche.guion.txt`

**3:34,8** de secuencia · 32 frases · 54 planos · 151 efectos · 13 personajes

Reparto medido (m): archimago 1,57/ojos 0,93 · liora 1,58/ojos 0,95 · mago 1,68/ojos 0,94 · nina 1,44/ojos 0,96 · v01 1,31/ojos 0,95 · v02 1,58/ojos 0,94 · v03 1,51/ojos 0,96 · v04 1,68/ojos 0,95 · v06 1,64/ojos 0,95 · v07 1,30/ojos 0,96 · v08 1,58/ojos 0,96 · v09 1,46/ojos 0,95 · v10 1,58/ojos 0,94

## Errores (0)


## Avisos (25)

- línea 139: v07 @saludo_fin+1.2  pasea por k4, k1, k5, k2, k3 ritmo=trote pausa=0.6: v07 roza a v02 a 178,0 s y no he encontrado otro camino. Mueve el destino o el momento.
- línea 267: desde @carreta_magia+0.5 nina anda a nina_carro ritmo=trote llega_mirando=archimago: nina roza a v01 a 39,4 s y no he encontrado otro camino. Mueve el destino o el momento.
- línea 365: mago anda a ladera, entrada ritmo=normal llega_mirando=centro_plaza sin_esperar: no se llega del todo a (5988,0, 6000,5) por el NavMesh
- línea 228: plano general centro_plaza desde=cam_general altura=7 lente=46 suave=1.5: algo del decorado se mete delante y tapa un 47% del cuadro. Prueba lado= o desde=.
- línea 233: plano conjunto archimago desde=cam_calle altura=1.55 lente=36 suave=0.8: algo del decorado se mete delante y tapa un 60% del cuadro. Prueba lado= o desde=.
- línea 248: plano conjunto archimago v06 lado=frente: algo del decorado se mete delante y tapa un 20% del cuadro. Prueba lado= o desde=.
- línea 248: plano conjunto archimago v06 lado=frente: no hay ángulo limpio para «conjunto de archimago y v06» (algo tapa la cara). Prueba con desde= o lado=.
- línea 285: plano conjunto nina v03 v08 contrapicado: no hay ángulo limpio para «conjunto de nina y v03 y v08» (algo tapa la cara). Prueba con desde= o lado=.
- línea 382: plano medio archimago lado=izq: algo del decorado se mete delante y tapa un 67% del cuadro. Prueba lado= o desde=.
- línea 391: plano dos archimago liora: no hay ángulo limpio para «dos: archimago y liora» (algo tapa la cara). Prueba con desde= o lado=.
- línea 394: plano medio liora lado=der: no cabe el contraplano sobre el hombro de archimago; hago un plano medio de liora.
- línea 394: plano medio liora lado=der: no hay ángulo limpio para «medio de liora» (algo tapa la cara). Prueba con desde= o lado=.
- línea 397: plano medio archimago lado=der: no cabe el contraplano sobre el hombro de liora; hago un plano medio de archimago.
- 0:16,0: v01 y v07 se atraviesan.
- 0:33,4: v01 y v07 se atraviesan.
- 0:36,2: v01 y v07 se atraviesan.
- 0:39,6: nina y v01 se atraviesan.
- 0:40,2: v01 y v07 se atraviesan.
- 0:42,4: v01 y v07 se atraviesan.
- 0:45,8: v01 y v07 se atraviesan.
- 0:48,0: v01 y v07 se atraviesan.
- 1:03,2: v01 y v07 se atraviesan.
- 1:06,0: v01 y v07 se atraviesan.
- 0:50,6: en «conjunto de nina y v03 y v08» v06 está 8 s en cuadro sin hacer nada. Dale algo en su pista (charla, bucle, pasea…).
- 1:51,8: en «medio de liora» archimago está 7 s en cuadro sin hacer nada. Dale algo en su pista (charla, bucle, pasea…).

## Silencios largos entre frases (10)

- 0:30,1 → 0:36,9: 6,9 s sin voz entre PROLOGO_FAVOR_CARRETA y PROLOGO_CARRETA_GRACIAS
- 0:48,0 → 0:50,6: 2,6 s sin voz entre PROLOGO_MANANA_GLOBO_OK y PROLOGO_GLOBO_VECINO_1
- 0:58,3 → 1:02,1: 3,8 s sin voz entre PROLOGO_GLOBO_VECINO_3 y PROLOGO_PLAZA_LIORA_1
- 1:17,9 → 1:35,8: 17,9 s sin voz entre PROLOGO_PLAZA_LIORA_2 y PROLOGO_DESPEDIDA_LIORA_1
- 1:41,1 → 1:43,6: 2,6 s sin voz entre PROLOGO_DESPEDIDA_ARCHIMAGO_1 y PROLOGO_HORA_ARCHIMAGO
- 1:59,3 → 2:01,1: 1,8 s sin voz entre PROLOGO_DESPEDIDA_ELEGIR y PROLOGO_EVACUACION_LIORA
- 2:04,5 → 2:14,8: 10,2 s sin voz entre PROLOGO_EVACUACION_LIORA y PROLOGO_DUELO_MAGO
- 2:24,2 → 2:36,5: 12,3 s sin voz entre PROLOGO_DUELO_ARCHIMAGO y PROLOGO_MAGO_VALLE
- 2:39,8 → 2:43,2: 3,4 s sin voz entre PROLOGO_MAGO_VALLE y PROLOGO_PLEGARIA_01
- 3:24,7 → 3:27,1: 2,5 s sin voz entre PROLOGO_PLEGARIA_06 y PROLOGO_HECHIZO

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | plano 1 | general de el punto · 4,5 s · lente 46° |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
| 0:00,0 | efecto | Voz: ExteriorDia, +0,0 dB |
| 0:00,0 | efecto | Ambiente: AMBIENTE ← Ambience_Prologo_Pueblo |
| 0:00,0 | efecto | Post-procesado: Prologo_Dia (0s) |
| 0:00,0 | efecto | Hora del día: Morning (de golpe) |
| 0:00,0 | efecto | Decorado: mover PROP_Carreta (al instante) |
| 0:00,0 | efecto | Apagar: PROP_Nube_Oeste |
| 0:00,0 | efecto | Apagar: PROP_Nube_Este |
| 0:00,0 | efecto | Nubes que se abren (5,5s) |
| 0:04,5 | **@entra** |  |
| 0:04,5 | plano 2 | conjunto de archimago · 5,5 s · lente 36° |
| 0:10,0 | plano 3 | conjunto de v01 y v07 y v02 · 6,1 s · lente 34° · coste 263: tapado 61, cara 142, ángulo 54 |
| 0:16,1 | **@saludo** |  |
| 0:16,1 | plano 4 | medio de archimago · 4,3 s · lente 30° · coste 283: cara 212, ángulo 71 |
| 0:16,1 | archimago | «Buenos días, valle. ¿Qué se te ha estropeado hoy?» (4,3 s) · pose |
| 0:20,4 | plano 5 | conjunto de archimago y v01 y v02 · 4,0 s · lente 34° · coste 559: tapado 122, cara 323, ángulo 108 |
| 0:20,4 | v01 | «¡Buenos días, maestro!» (1,8 s) · pose |
| 0:22,6 | v02 | «¡Muy buenas, maestro!» (1,8 s) |
| 0:24,4 | **@saludo_fin** |  |
| 0:24,4 | plano 6 | medio de v06 · 5,7 s · lente 30° · coste 2: ángulo 2 |
| 0:24,4 | v06 | «¡Maestro! Se me ha volcado otra vez la carreta. Y eso que hoy iba despacio.» (5,7 s) |
| 0:30,1 | plano 7 | conjunto de archimago y v06 · 6,9 s · lente 34° · coste 1197: pegado 100, estorbo 715, cara 317, ángulo 59 |
| 0:33,5 | **@carreta_magia** |  |
| 0:33,8 | efecto | VFX: 1 Flash_magic_ellow_blue en NPC_Archimago |
| 0:33,8 | efecto | SFX: Prologo_MagiaLevitar |
| 0:34,0 | efecto | VFX: 1 Flash_magic_ellow_blue en PROP_Carreta |
| 0:34,1 | efecto | Decorado: mover PROP_Carreta en 1,4s |
| 0:36,2 | efecto | Decorado: mover PROP_Carreta en 0,9s |
| 0:36,9 | plano 8 | dos: archimago y v06 · 4,2 s · lente 32° · coste 140: cara 134 |
| 0:36,9 | v06 | «¡Gracias, maestro! No habría podido enderezarla yo solo.» (4,2 s) · pose |
| 0:37,1 | efecto | VFX: 2 Flash_round_ellow en PROP_Carreta |
| 0:37,1 | efecto | SFX: Prologo_CarretaCae |
| 0:41,1 | plano 9 | medio de nina · 4,3 s · lente 30° · coste 2: ángulo 2 |
| 0:41,1 | nina | «¡Maestro! El globo se ha enganchado en lo alto del campanario.» (4,3 s) |
| 0:45,4 | plano 10 | sobre el hombro de nina a archimago · 2,6 s · lente 24° · coste 455: cara 99, eje 350 |
| 0:45,4 | archimago | «Con cuidado... Eso es.» (2,6 s) |
| 0:45,4 | efecto | Hora del día: AfterNoon |
| 0:45,8 | efecto | SFX: Prologo_MagiaLevitar |
| 0:45,8 | efecto | VFX: 1 Flash_magic_ellow_blue en NPC_Archimago |
| 0:46,3 | efecto | VFX: 1 Flash_magic_ellow_blue en PROP_Globo |
| 0:46,5 | efecto | VFX: 2 Flash_round_ellow en PROP_Globo |
| 0:48,0 | **@globo_sube** |  |
| 0:48,0 | plano 11 | general de el punto · 2,6 s · lente 48° |
| 0:48,0 | efecto | Decorado: mover PROP_Globo en 8,0s |
| 0:50,6 | plano 12 | conjunto de nina y v03 y v08 · 7,7 s · lente 34° · coste 1103: tapado 61, delante 700, cara 286, ángulo 50 |
| 0:50,6 | nina | «¡Hala, ya vuela!» (2,6 s) · pose |
| 0:53,5 | v03 | «¡Mira cómo sube!» (2,1 s) · pose |
| 0:56,0 | v08 | «¡Va a llegar hasta las nubes!» (2,3 s) |
| 0:58,3 | **@globo_fin** |  |
| 0:58,3 | plano 13 | medio de archimago · 2,2 s · lente 30° · coste 2: ángulo 2 |
| 1:00,3 | efecto | Apagar: PROP_Globo |
| 1:00,5 | **@rio** |  |
| 1:00,5 | plano 14 | siguiendo a liora · 6,6 s · lente 34° · mezcla 1,2 s |
| 1:00,5 | efecto | Ambiente: parar AMBIENTE |
| 1:00,5 | efecto | Ambiente: AMBIENTE_RIO ← Ambience_Prologo_Rio |
| 1:00,5 | efecto | Hora del día: Sunset |
| 1:00,5 | efecto | Post-procesado: Prologo_Atardecer (0s) |
| 1:00,5 | efecto | Sol: en cuadro, poniéndose en 19 s |
| 1:01,5 | **@rio_paseo** |  |
| 1:02,1 | liora | «¿Has pensado qué contestar a la academia? Llevas semanas con la carta.» (4,4 s) |
| 1:07,1 | plano 15 | sobre el hombro de liora a archimago · 5,7 s · lente 24° · coste 656: estorbo 300, eje 350 |
| 1:07,1 | archimago | «Hoy pensaba contestar. Pero siempre acaba surgiendo algo en el valle.» (5,7 s) |
| 1:12,9 | plano 16 | siguiendo a liora · 5,0 s · lente 34° |
| 1:12,9 | liora | «No tienes que quedarte solo porque aquí te necesiten. ¿Qué quieres hacer tú?» (5,0 s) · pose |
| 1:17,9 | plano 17 | dos: archimago y liora · 2,2 s · lente 34° · coste 580: estorbo 300, cara 265, ángulo 9 |
| 1:20,1 | **@cielo** |  |
| 1:20,1 | plano 18 | medio de archimago · 1,6 s · lente 30° · coste 423: cara 62, ángulo 11, eje 350 |
| 1:20,1 | efecto | Música: parar (3s) |
| 1:20,1 | efecto | Sol: devolverlo al ciclo |
| 1:20,1 | efecto | Hora del día: Night |
| 1:20,1 | efecto | Tiempo: encender Tormenta |
| 1:20,1 | efecto | Post-procesado: Prologo_Amenaza (6s) |
| 1:20,1 | efecto | SFX: SFX_Prologo_Viento |
| 1:20,1 | efecto | Ambiente: parar AMBIENTE_RIO |
| 1:20,1 | efecto | Tiempo: encender Viento |
| 1:21,7 | plano 19 | general de el punto · 2,8 s · lente 40° |
| 1:21,7 | efecto | Rayo: 2 × fondo del plano |
| 1:24,5 | plano 20 | conjunto de v02 y v10 y v01 y nina · 2,0 s · lente 34° · coste 14: ángulo 14 |
| 1:26,5 | **@llegada** |  |
| 1:26,5 | plano 21 | conjunto de mago · 2,6 s · lente 30° · coste 0: limpio |
| 1:26,5 | efecto | Sacudida de cámara (0,55, 0,6s) |
| 1:26,5 | efecto | SFX: Prologo_Golpe |
| 1:26,5 | efecto | Voz: ExteriorNoche, +0,0 dB |
| 1:26,5 | efecto | Fogonazo (0,18s) |
| 1:26,5 | efecto | Rayo: 1 × fondo del plano |
| 1:26,8 | efecto | SFX: Prologo_PresenciaOscura |
| 1:26,9 | efecto | Música: MAGOOSCURO_REVEAL |
| 1:27,1 | efecto | Ambiente: TORMENTA ← Ambience_Prologo_Tormenta |
| 1:27,1 | efecto | Tiempo: encender Lluvia |
| 1:29,1 | plano 22 | primer plano de mago · 2,2 s · lente 24° · coste 2: ángulo 2 |
| 1:31,3 | plano 23 | medio de archimago · 1,0 s · lente 30° · coste 73: cara 62, ángulo 11 |
| 1:32,3 | plano 24 | siguiendo a mago · 3,5 s · lente 34° |
| 1:35,8 | **@evacuar** |  |
| 1:35,8 | plano 25 | sobre el hombro de archimago a liora · 2,2 s · lente 24° · coste 95: cara 95 |
| 1:35,8 | liora | «¿Quién es? ¿Qué quiere?» (2,2 s) · pose |
| 1:35,8 | efecto | Ambiente: PANICO ← Ambience_Prologo_Panico |
| 1:35,8 | efecto | Encender: PROP_Incendio |
| 1:36,1 | efecto | SFX: SFX_Prologo_Grito |
| 1:36,4 | efecto | Rayo: 1 × fondo del plano |
| 1:38,0 | plano 26 | sobre el hombro de liora a archimago · 3,1 s · lente 24° · coste 95: cara 95 |
| 1:38,0 | archimago | «No lo sé. Llévatelos al puente, deprisa.» (3,1 s) · pose |
| 1:41,1 | plano 27 | medio de archimago · 5,2 s · lente 30° · coste 494: delante 455, ángulo 39 |
| 1:43,6 | **@huida** |  |
| 1:43,6 | archimago | «¡Al puente! ¡Todos, ahora!» (2,6 s) · pose |
| 1:46,3 | plano 28 | general de v06 y v04 y v09 y v03 · 1,6 s · lente 38° |
| 1:46,6 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 1:47,9 | plano 29 | dos: archimago y liora · 3,9 s · lente 32° · coste 1155: tapado 244, cara 878, ángulo 27 |
| 1:47,9 | liora | «¿Y tú?» (1,3 s) |
| 1:49,5 | archimago | «Ve. Yo me encargo de él.» (2,3 s) · pose |
| 1:51,8 | plano 30 | medio de liora · 7,5 s · lente 24° · coste 1119: cara 1050, ángulo 69 |
| 1:51,8 | liora | «Vuelve pronto.» (1,9 s) · pose |
| 1:53,8 | liora | «Y cuando vuelvas, nada de encargos. Te toca descansar.» (5,4 s) · pose |
| 1:59,3 | plano 31 | medio de archimago · 1,8 s · lente 24° · coste 79: cara 25, ángulo 48 |
| 2:01,1 | plano 32 | conjunto de liora y nina · 3,4 s · lente 34° · coste 301: cara 210, ángulo 86 |
| 2:01,1 | liora | «¡Al puente, todos! ¡Deprisa, no miréis atrás!» (3,4 s) · pose |
| 2:04,5 | **@acercan** |  |
| 2:04,5 | plano 33 | siguiendo a archimago · 4,5 s · lente 34° |
| 2:05,7 | efecto | Rayo: 1 × fondo del plano |
| 2:06,5 | efecto | Ambiente: parar PANICO |
| 2:09,0 | plano 34 | siguiendo a mago · 5,7 s · lente 34° |
| 2:09,0 | efecto | Música: MAGOOSCURO_CLIMAX |
| 2:14,8 | **@duelo** |  |
| 2:14,8 | plano 35 | sobre el hombro de archimago a mago · 3,0 s · lente 24° · coste 0: limpio |
| 2:14,8 | mago | «¿Vas a salvarlos a todos tú solo?» (3,0 s) · pose |
| 2:14,8 | efecto | Mezcla: primeraVozOscura |
| 2:15,0 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 2:17,7 | plano 36 | primer plano de mago · 3,1 s · lente 24° · coste 26: ángulo 26 |
| 2:17,7 | mago | «Ni a ti mismo puedes salvarte.» (3,1 s) · pose |
| 2:20,8 | plano 37 | medio de archimago · 3,3 s · lente 30° · coste 2: ángulo 2 |
| 2:20,8 | archimago | «Mientras siga en pie, no llegarás a ellos.» (3,3 s) · pose |
| 2:20,8 | efecto | Mezcla: primeraVozOscura (quitar) |
| 2:24,2 | plano 38 | dos: archimago y mago · 2,5 s · lente 36° · coste 223: cara 109, ángulo 108 |
| 2:24,4 | efecto | Hechizo: NPC_MagoOscuro → NPC_Archimago |
| 2:24,5 | efecto | VFX: Magic shield blue en NPC_Archimago |
| 2:25,0 | efecto | SFX: Prologo_EscudoBloquea |
| 2:25,0 | efecto | Fogonazo (0,12s) |
| 2:25,0 | efecto | Sacudida de cámara (0,35, 0,4s) |
| 2:26,7 | plano 39 | sobre el hombro de mago a archimago · 6,7 s · lente 24° · coste 0: limpio |
| 2:26,9 | efecto | Hechizo: NPC_Archimago → NPC_MagoOscuro |
| 2:29,6 | **@choque** |  |
| 2:29,7 | efecto | SFX: Prologo_Carga |
| 2:29,7 | efecto | VFX: Magic circle en NPC_Archimago |
| 2:29,7 | efecto | VFX: Magic circle en NPC_MagoOscuro |
| 2:30,3 | efecto | VFX: Charge slash purple en NPC_MagoOscuro |
| 2:30,3 | efecto | SFX: SFX_Prologo_Latigazo |
| 2:30,3 | efecto | VFX: Charge slash blue en NPC_Archimago |
| 2:30,6 | efecto | Fogonazo (0,28s) |
| 2:30,6 | efecto | SFX: Prologo_Choque |
| 2:30,6 | efecto | Líneas de concentración: mostrar |
| 2:30,6 | efecto | VFX: Red energy explosion en choque |
| 2:30,6 | efecto | VFX: FX_LightPillar en choque |
| 2:30,6 | efecto | Sacudida de cámara (0,75, 1s) |
| 2:30,7 | efecto | SFX: Prologue_WarClashStinger_A |
| 2:33,4 | plano 40 | medio de archimago · 3,1 s · lente 30° · coste 2: ángulo 2 |
| 2:33,6 | efecto | SFX: Prologo_SueloSeAbre |
| 2:33,7 | efecto | VFX: MagoOscuroGrieta en NPC_Archimago |
| 2:33,9 | efecto | VFX: Ground AOE explosion en NPC_Archimago |
| 2:33,9 | efecto | Sacudida de cámara (0,7, 0,9s) |
| 2:34,0 | efecto | VFX: Dust ground en NPC_Archimago |
| 2:36,5 | **@ultimo** |  |
| 2:36,5 | plano 41 | primer plano de mago · 3,3 s · lente 24° · coste 2: ángulo 2 |
| 2:36,5 | mago | «Que este valle no haya existido nunca.» (3,3 s) · pose |
| 2:39,8 | plano 42 | general de mago · 3,4 s · lente 38° |
| 2:39,8 | efecto | Post-procesado: Prologo_Climax (2,5s) |
| 2:39,9 | efecto | SFX: Prologo_Despegue |
| 2:39,9 | efecto | VFX: Dust ground en NPC_MagoOscuro |
| 2:40,1 | efecto | Rayo: 2 × fondo del plano |
| 2:40,4 | efecto | SFX: Prologo_HechizoGrande |
| 2:40,6 | efecto | SFX: SFX_Prologo_AgujeroNegro_Carga |
| 2:40,6 | efecto | Ambiente: AGUJERO_NEGRO ← SFX_Prologo_AgujeroNegro_Bucle |
| 2:40,6 | efecto | VFX: VFX_AgujeroNegro en agujero |
| 2:40,6 | efecto | Escalar: CARGA_ESFERA ×0,35 en 0 s |
| 2:40,7 | efecto | Escalar: CARGA_ESFERA ×1 en 3,5 s |
| 2:40,8 | efecto | VFX: Magic circle en agujero |
| 2:40,8 | efecto | Sacudida de cámara (0,35, 1,6s) |
| 2:43,2 | plano 43 | medio de archimago · 6,6 s · lente 30° · coste 17: ángulo 17 |
| 2:43,2 | archimago | «No daré ni un paso atrás... ¡No dejaré que los toques! ¡Liora! ¡Nina! ¡A ninguno de ellos!» (6,6 s) |
| 2:43,2 | efecto | Ambiente: CORO ← SFX_Prologo_CoroBucle |
| 2:43,2 | efecto | Mezcla: plegaria |
| 2:43,2 | efecto | Voz: Ninguno, +0,0 dB |
| 2:43,4 | efecto | VFX: Healing circle en NPC_Archimago |
| 2:43,4 | efecto | SFX: Prologo_MagiaLevitar |
| 2:46,5 | efecto | Cut-in 0: NPC_Liora (Cara) |
| 2:48,2 | efecto | Cut-in 0: NPC_Aldeano_05 (Cara) |
| 2:49,7 | plano 44 | general de mago · 1,4 s · lente 36° |
| 2:49,7 | efecto | Escalar: CARGA_ESFERA ×1,5 en 2 s |
| 2:49,7 | efecto | VFX: Magic circle en agujero |
| 2:49,7 | efecto | Sacudida de cámara (0,25, 1s) |
| 2:51,1 | plano 45 | primer plano de archimago · 8,2 s · lente 24° · coste 2: ángulo 2 |
| 2:51,1 | archimago | «Más allá de la noche y del silencio... ¡Luz que naciste antes que el cielo y tendiste el camino entre las estrellas!» (8,2 s) |
| 2:51,5 | efecto | VFX: Magic circle en NPC_Archimago |
| 2:51,9 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 2:55,3 | efecto | VFX: 1 Flash_magic_ellow_blue en NPC_Archimago |
| 2:59,3 | plano 46 | medio de archimago · 8,5 s · lente 30° · coste 17: ángulo 17 |
| 2:59,3 | archimago | «Ningún poder de este mundo puede detenerlo... ¡Por eso llamo a lo que aún no existe! ¡Escucha mi voz y despierta en mis manos!» (8,5 s) |
| 3:02,5 | efecto | Rayo: 1 × fondo del plano |
| 3:02,8 | efecto | SFX: Prologo_Carga |
| 3:02,8 | efecto | VFX: Charge slash blue en NPC_Archimago |
| 3:04,9 | efecto | Escalar: CARGA_ESFERA ×2 en 2 s |
| 3:04,9 | efecto | Sacudida de cámara (0,35, 1,2s) |
| 3:07,8 | plano 47 | general de mago · 1,2 s · lente 34° |
| 3:07,8 | efecto | Rayo: 1 × fondo del plano |
| 3:09,0 | plano 48 | primer plano de archimago · 6,2 s · lente 24° · coste 17: ángulo 17 |
| 3:09,0 | archimago | «Que mi vida sea el precio y mi alma el sello... ¡No importa lo que quede de mí cuando la luz se apague!» (6,2 s) |
| 3:12,5 | efecto | Sacudida de cámara (0,3, 0,9s) |
| 3:12,5 | efecto | SFX: Prologo_MagiaLevitar |
| 3:12,5 | efecto | VFX: FX_LightPillar en NPC_Archimago |
| 3:15,3 | plano 49 | medio de archimago · 5,7 s · lente 30° · coste 2: ángulo 2 |
| 3:15,3 | archimago | «¡Haz de mi último aliento un escudo inquebrantable! ¡Arde conmigo y detén la oscuridad!» (5,7 s) |
| 3:15,3 | efecto | Líneas de concentración: mostrar |
| 3:15,6 | efecto | VFX: 2 Flash_round_ellow en NPC_Archimago |
| 3:18,7 | efecto | Rayo: 2 × fondo del plano |
| 3:18,8 | efecto | Escalar: CARGA_ESFERA ×2,6 en 1,4 s |
| 3:18,8 | efecto | SFX: Prologo_Carga |
| 3:18,8 | efecto | Sacudida de cámara (0,5, 1,6s) |
| 3:21,0 | plano 50 | primer plano de archimago · 6,1 s · lente 24° · coste 2: ángulo 2 |
| 3:21,0 | archimago | «Perdóname, Liora... No voy a volver a casa.» (3,7 s) · pose |
| 3:21,0 | efecto | Líneas de concentración: retirar |
| 3:25,0 | efecto | Líneas de concentración: mostrar |
| 3:25,0 | efecto | SFX: Prologo_Carga |
| 3:25,0 | efecto | Cut-in 0: NPC_MagoOscuro (Cara) |
| 3:25,0 | efecto | Mezcla: plegaria (quitar) |
| 3:25,0 | efecto | Mezcla: grito |
| 3:25,0 | efecto | Ambiente: parar CORO |
| 3:26,0 | efecto | Cut-in 1: NPC_Archimago (Cara) |
| 3:27,1 | **@hechizo** |  |
| 3:27,1 | plano 51 | general de archimago y mago · 2,0 s · lente 44° |
| 3:27,1 | archimago | «¡¡Protégelos a todos!!» (2,0 s) |
| 3:27,1 | efecto | Voz: Ninguno, +4,0 dB |
| 3:27,3 | efecto | Rayo: 2 × fondo del plano |
| 3:27,6 | efecto | Fusión: CARGA_ESFERA +  |
| 3:27,6 | efecto | Sacudida de cámara (0,45, 2s) |
| 3:27,6 | efecto | SFX: Prologo_ProteccionAbsoluta |
| 3:29,1 | plano 52 | primer plano de archimago · 1,3 s · lente 24° · coste 21: ángulo 21 |
| 3:29,1 | efecto | Sacudida de cámara (0,55, 1,3s) |
| 3:29,1 | efecto | Líneas de concentración: mostrar |
| 3:30,4 | plano 53 | primer plano de mago · 1,2 s · lente 24° · coste 115: cara 63, ángulo 53 |
| 3:30,4 | efecto | Sacudida de cámara (0,7, 1,2s) |
| 3:31,3 | **@explosion** |  |
| 3:31,3 | efecto | Ambiente: parar TORMENTA |
| 3:31,3 | efecto | Líneas de concentración: retirar |
| 3:31,3 | efecto | Pantalla: cubrir (1,2s) |
| 3:31,3 | efecto | VFX: recoger efectos registrados |
| 3:31,3 | efecto | Ambiente: parar AGUJERO_NEGRO |
| 3:31,3 | efecto | Mezcla: grito (quitar) |
| 3:31,6 | plano 54 | general de archimago y mago · 3,2 s · lente 48° |
| 3:31,6 | efecto | Sacudida de cámara (0,95, 1,4s) |
| 3:31,7 | efecto | SFX: Prologue_WarClashStinger_B |
| 3:31,7 | efecto | Sacudida de cámara (0,9, 1,6s) |
| 3:32,2 | efecto | Música: parar (1,2s) |
| 3:32,3 | efecto | SFX: SFX_Prologo_SilencioBlanco |
| 3:32,5 | efecto | Hora del día: Morning (de golpe) (y es con la que se queda el mundo) |
| 3:32,7 | efecto | Fogonazo (0,14s) |
| 3:34,3 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### archimago (NPC_Archimago)

- 0:04,6–0:16,2 anda de (6002,6, 6012,3) a (6003,2, 6002,3)
- 0:16,1–0:20,4 habla
- 0:16,2 gesto HandWave02
- 0:30,1–0:33,6 anda de (6003,2, 6002,3) a (6006,4, 6000,6)
- 0:33,5 gesto MagicRight
- 0:36,9 gesto HeadNod01
- 0:45,4 bucle CastingIdle01
- 0:45,4–0:48,0 habla
- 0:48,0 reposo 
- 0:51,2 gesto Laugh01
- 0:58,3 gesto HeadNod01
- 1:01,6–1:07,2 anda de (6016,9, 6002,6) a (6017,4, 5998,3)
- 1:07,1–1:12,9 habla
- 1:07,2 gesto Talk02
- 1:38,0–1:41,1 habla
- 1:38,1 gesto HeadShake01
- 1:39,7 gesto Talk01
- 1:41,1–1:43,7 anda de (6017,4, 5998,3) a (6014,4, 6000,3)
- 1:43,6–1:46,3 habla
- 1:43,7 gesto Challenging_NoWeapon
- 1:49,5–1:51,8 habla
- 1:49,6 gesto HeadNod01
- 1:59,3 gesto HeadNod01
- 2:04,6–2:12,6 anda de (6014,4, 6000,3) a (6005,6, 5999,8)
- 2:20,8–2:24,2 habla
- 2:20,9 gesto Angry02
- 2:22,5 gesto Talk03
- 2:24,5 gesto Defend_NoWeapon
- 2:26,7 gesto MagicLeft
- 2:29,6 gesto MagicSpecial
- 2:34,0 bucle Pain01
- 2:43,2 bucle Challenging_NoWeapon
- 2:43,2 reposo 
- 2:43,2–2:49,7 habla
- 2:51,1 bucle CastingIdle01
- 2:51,1–2:59,3 habla
- 2:59,3–3:07,8 habla
- 3:02,6 gesto MagicSpecial
- 3:04,1 bucle CastingIdle01
- 3:09,0–3:15,3 habla
- 3:12,4 gesto Angry02
- 3:14,0 bucle MagicAttackOmni01_Load
- 3:15,3–3:21,0 habla
- 3:21,0 reposo 
- 3:21,0–3:24,7 habla
- 3:21,1 gesto Talk03
- 3:27,1 bucle MagicAttackOmni01_Load
- 3:27,1–3:29,1 habla

### liora (NPC_Liora)

- 0:00,5 bucle InteractWithPeople_NoWeapon
- 0:02,1 gesto Question01
- 0:07,1 gesto HeadShake01
- 0:11,2 gesto Talk01
- 0:14,0 gesto HeadNod01
- 0:17,5 gesto Talk02
- 0:21,5 gesto Laugh01
- 0:24,6 gesto Talk03
- 0:29,7 gesto Question01
- 0:34,4 gesto HeadShake01
- 0:39,5 gesto Talk01
- 0:43,4 gesto HeadNod01
- 0:47,5 gesto Talk02
- 0:52,6 gesto Laugh01
- 0:57,6 gesto Talk03
- 1:00,5 reposo 
- 1:01,6–1:07,2 anda de (6015,8, 6002,2) a (6016,1, 5997,9)
- 1:02,1–1:06,5 habla
- 1:12,9–1:17,9 habla
- 1:12,9 gesto Talk01
- 1:15,4 gesto Talk03
- 1:35,8–1:38,0 habla
- 1:35,8 gesto Question01
- 1:47,9–1:49,2 habla
- 1:47,9–1:48,7 anda de (6016,1, 5997,9) a (6015,0, 5997,8)
- 1:51,8–1:53,7 habla
- 1:51,9 gesto Talk01
- 1:53,8–1:59,3 habla
- 1:53,9 gesto Talk03
- 1:57,1 gesto Talk01
- 2:01,1–2:04,5 habla
- 2:01,2 gesto HandWave02
- 2:04,6–2:16,5 anda de (6015,1, 5997,9) a (6042,0, 6000,0)

### mago (NPC_MagoOscuro)

- 1:26,5 bucle Challenging_NoWeapon
- 1:26,5 aparece
- 1:32,3 reposo 
- 1:32,3–1:59,4 anda de (5957,7, 5996,9) a (5988,0, 6000,5)
- 2:04,6–2:14,8 anda de (5988,0, 6000,5) a (5999,4, 5999,8)
- 2:14,8–2:17,7 habla
- 2:14,9 gesto Talk02
- 2:17,7–2:20,8 habla
- 2:17,8 gesto Talk03
- 2:24,2 gesto MagicRight
- 2:27,5 gesto TakeDamage
- 2:29,6 gesto MagicSpecial
- 2:33,4 gesto MagicRight
- 2:36,5–2:39,8 habla
- 2:36,6 gesto Talk03
- 2:39,8 bucle MagicAttackOmni01_Load

### nina (NPC_Aldeano_05)

- 0:00,0 bucle SenseSomethingSearching_NoWeapon
- 0:33,9 reposo 
- 0:37,3–0:44,3 anda de (5991,0, 6001,0) a (6004,6, 5999,9)
- 0:41,1–0:45,4 habla
- 0:41,2 gesto Beg01
- 0:50,6–0:53,2 habla
- 0:50,7 gesto Cheer01
- 0:58,3–1:03,3 anda de (6004,5, 5999,9) a (5995,2, 6001,0)
- 1:03,3 bucle InteractWithPeople_NoWeapon
- 1:06,2 gesto Talk01
- 1:09,5 gesto HeadNod01
- 1:12,7 gesto Talk02
- 1:15,6 gesto Laugh01
- 1:18,4 gesto Talk03
- 1:20,4 reposo 
- 1:44,3–1:54,4 anda de (5995,3, 6001,0) a (6014,1, 5998,5)
- 2:04,6–2:15,0 anda de (6014,2, 5998,6) a (6026,6, 6007,5)

### v01 (NPC_Aldeano_01)

- 0:00,6–0:02,7 anda de (5995,5, 5998,6) a (5999,2, 5999,0)
- 0:03,1–0:04,6 anda de (5999,2, 5999,1) a (5999,0, 6001,6)
- 0:05,0–0:07,3 anda de (5998,9, 6001,6) a (5994,8, 6001,3)
- 0:07,7–0:09,3 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 0:09,7–0:11,7 anda de (5995,6, 5998,6) a (5999,2, 5999,0)
- 0:12,1–0:13,6 anda de (5999,2, 5999,1) a (5999,0, 6001,6)
- 0:14,0–0:20,0 anda de (5998,9, 6001,6) a (6002,3, 6000,6)
- 0:20,4–0:22,3 habla
- 0:20,5 gesto HandWave01
- 0:25,1–0:29,0 anda de (6002,2, 6000,6) a (5994,8, 6001,3)
- 0:29,5–0:31,1 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 0:31,6–0:33,6 anda de (5995,6, 5998,6) a (5999,2, 5999,0)
- 0:34,1–0:35,6 anda de (5999,2, 5999,1) a (5999,0, 6001,6)
- 0:37,7–0:40,6 anda de (5998,9, 6001,6) a (5994,8, 6001,3)
- 0:41,1–0:42,6 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 0:43,5–0:45,6 anda de (5995,5, 5998,6) a (5999,2, 5999,0)
- 0:46,1–0:47,6 anda de (5999,2, 5999,1) a (5999,0, 6001,6)
- 0:48,5–0:51,7 anda de (5998,9, 6001,6) a (5993,4, 5999,2)
- 1:01,0–1:03,5 anda de (5993,5, 5999,2) a (5998,0, 6000,2)
- 1:04,0–1:05,0 anda de (5998,0, 6000,2) a (5999,2, 5999,0)
- 1:05,5–1:06,9 anda de (5999,2, 5999,0) a (5997,3, 5997,6)
- 1:07,4–1:08,6 anda de (5997,2, 5997,6) a (5995,5, 5998,6)
- 1:09,1–1:10,7 anda de (5995,6, 5998,6) a (5998,0, 6000,2)
- 1:11,2–1:12,2 anda de (5998,0, 6000,2) a (5999,2, 5999,0)
- 1:12,7–1:14,1 anda de (5999,2, 5999,0) a (5997,3, 5997,6)
- 1:14,6–1:15,8 anda de (5997,2, 5997,6) a (5995,5, 5998,6)
- 1:16,3–1:17,9 anda de (5995,6, 5998,6) a (5998,0, 6000,2)
- 1:18,4–1:19,4 anda de (5998,0, 6000,2) a (5999,2, 5999,0)
- 1:19,9–1:20,4 anda de (5999,2, 5999,0) a (5998,6, 5998,5)
- 1:20,4 reposo 
- 1:21,2–1:23,0 anda de (5998,5, 5998,6) a (5996,9, 6001,0)
- 1:46,1–2:05,9 anda de (5997,0, 6001,0) a (6040,0, 5990,5)

### v02 (NPC_Aldeano_02)

- 0:00,4 bucle InteractWithPeople_NoWeapon
- 0:04,1 gesto Talk02
- 0:08,3 gesto Laugh01
- 0:12,5 gesto Talk03
- 0:16,0 gesto Question01
- 0:20,4 gesto HeadShake01
- 0:22,6–0:24,4 habla
- 0:22,7 gesto HandWave02
- 0:59,8 bucle InteractWithPeople_NoWeapon
- 1:01,3 gesto Talk03
- 1:03,7 gesto Question01
- 1:07,5 gesto HeadShake01
- 1:09,8 gesto Talk01
- 1:13,3 gesto HeadNod01
- 1:16,7 gesto Talk02
- 1:20,5 reposo 
- 1:20,6 gesto SenseSomethingStart_NoWeapon
- 1:27,0 bucle Fear01
- 1:45,1–2:05,4 anda de (5996,2, 6001,9) a (6044,0, 6003,5)
- 2:15,3 bucle Beg01_Loop

### v03 (NPC_Aldeano_03)

- 0:00,2 bucle InteractWithPeople_NoWeapon
- 0:03,8 gesto Talk02
- 0:07,7 gesto Laugh01
- 0:11,6 gesto Talk03
- 0:15,4 gesto Question01
- 0:19,0 gesto HeadShake01
- 0:21,3 gesto Talk01
- 0:24,6 gesto HeadNod01
- 0:28,5 gesto Talk02
- 0:32,3 gesto Laugh01
- 0:34,8 gesto Talk03
- 0:38,8 gesto Question01
- 0:41,4 gesto HeadShake01
- 0:44,1 gesto Talk01
- 0:48,0 reposo 
- 0:48,1–0:52,1 anda de (6000,3, 6004,8) a (5998,6, 6001,0)
- 0:53,5–0:55,7 habla
- 0:53,6 gesto FoundSomething_NoWeapon
- 0:58,3 bucle InteractWithPeople_NoWeapon
- 0:59,0 gesto Question01
- 1:01,2 gesto HeadShake01
- 1:04,2 gesto Talk01
- 1:07,0 gesto HeadNod01
- 1:10,8 gesto Talk02
- 1:13,3 gesto Laugh01
- 1:16,0 gesto Talk03
- 1:20,3 reposo 
- 1:27,1 gesto Fear01
- 1:44,6–2:04,7 anda de (5998,7, 6000,9) a (6046,0, 5999,0)

### v04 (NPC_Aldeano_04)

- 0:00,0 bucle CarryMoveIdle_NoWeapon
- 0:00,8–0:08,5 anda de (5997,8, 5995,8) a (6003,9, 5995,9)
- 0:10,3–0:17,9 anda de (6003,9, 5995,9) a (5997,8, 5995,8)
- 0:19,7–0:27,3 anda de (5997,8, 5995,8) a (6003,9, 5995,9)
- 0:29,1–0:36,8 anda de (6003,9, 5995,9) a (5997,8, 5995,8)
- 0:38,6–0:46,2 anda de (5997,8, 5995,8) a (6003,9, 5995,9)
- 0:48,0–0:55,6 anda de (6003,9, 5995,9) a (5997,8, 5995,8)
- 0:57,4–1:05,1 anda de (5997,8, 5995,8) a (6003,9, 5995,9)
- 1:06,9–1:14,5 anda de (6003,9, 5995,9) a (5997,8, 5995,8)
- 1:16,3–1:23,9 anda de (5997,8, 5995,8) a (6003,9, 5995,9)
- 1:20,3 reposo 
- 1:25,7–1:33,4 anda de (6003,9, 5995,9) a (5997,8, 5995,8)
- 1:35,2–1:42,8 anda de (5997,8, 5995,8) a (6003,9, 5995,9)
- 1:43,9–2:00,8 anda de (6004,0, 5996,0) a (6041,6, 5996,6)

### v06 (NPC_Aldeano_06)

- 0:00,0 bucle Loot01_Loop
- 0:24,4–0:30,1 habla
- 0:24,5 gesto Question01
- 0:33,5 reposo 
- 0:36,9–0:41,1 habla
- 0:37,0 gesto Reverence01
- 0:39,5 gesto Talk01
- 0:58,5 bucle Opening01_Loop
- 1:20,6 reposo 
- 1:43,7–1:56,1 anda de (6010,5, 5999,9) a (6039,0, 6001,0)

### v07 (NPC_Aldeano_07)

- 0:01,6–0:04,0 anda de (5999,0, 6001,6) a (5994,8, 6001,3)
- 0:04,6–0:06,2 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 0:06,8–0:08,0 anda de (5995,6, 5998,5) a (5997,3, 5997,6)
- 0:08,6–0:09,9 anda de (5997,4, 5997,7) a (5999,2, 5999,0)
- 0:10,5–0:12,0 anda de (5999,2, 5999,0) a (5999,0, 6001,6)
- 0:12,6–0:14,9 anda de (5998,9, 6001,6) a (5994,8, 6001,3)
- 0:16,3–0:17,9 anda de (5994,8, 6001,3) a (5995,5, 5998,6)
- 0:18,5–0:19,7 anda de (5995,5, 5998,6) a (5997,3, 5997,6)
- 0:20,7–0:22,1 anda de (5997,3, 5997,6) a (5999,2, 5999,0)
- 0:22,7–0:24,1 anda de (5999,2, 5999,1) a (5999,0, 6001,6)
- 0:24,7–0:27,2 anda de (5999,0, 6001,6) a (5994,8, 6001,3)
- 0:27,8–0:29,3 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 0:29,9–0:31,1 anda de (5995,5, 5998,6) a (5997,3, 5997,6)
- 0:31,7–0:33,1 anda de (5997,3, 5997,6) a (5999,2, 5999,0)
- 0:34,1–0:35,6 anda de (5999,2, 5999,1) a (5999,0, 6001,6)
- 0:36,2–0:38,5 anda de (5998,9, 6001,6) a (5994,8, 6001,3)
- 0:40,7–0:42,3 anda de (5994,8, 6001,3) a (5995,5, 5998,6)
- 0:42,9–0:44,1 anda de (5995,6, 5998,6) a (5997,3, 5997,6)
- 0:44,7–0:46,0 anda de (5997,4, 5997,6) a (5999,2, 5999,0)
- 0:46,6–0:48,1 anda de (5999,2, 5999,0) a (5999,0, 6001,6)
- 0:48,4–0:51,1 anda de (5998,9, 6001,5) a (5994,9, 5998,4)
- 1:01,1–1:03,1 anda de (5994,9, 5998,4) a (5998,0, 6000,2)
- 1:03,8–1:04,8 anda de (5998,1, 6000,1) a (5999,2, 5999,0)
- 1:05,5–1:06,8 anda de (5999,1, 5998,9) a (5997,3, 5997,6)
- 1:09,5–1:11,2 anda de (5997,3, 5997,6) a (5995,5, 5998,6)
- 1:11,9–1:13,6 anda de (5995,5, 5998,6) a (5998,0, 6000,2)
- 1:14,3–1:15,3 anda de (5998,1, 6000,1) a (5999,2, 5999,0)
- 1:16,0–1:17,3 anda de (5999,1, 5998,9) a (5997,3, 5997,6)
- 1:18,0–1:19,2 anda de (5997,3, 5997,6) a (5995,5, 5998,6)
- 1:19,9–1:22,7 anda de (5995,5, 5998,6) a (5999,0, 6001,6)
- 1:45,7–2:05,7 anda de (5999,1, 6001,6) a (6045,0, 6008,0)

### v08 (NPC_Aldeano_08)

- 0:01,0 bucle InteractWithPeople_NoWeapon
- 0:03,6 gesto Laugh01
- 0:07,9 gesto Talk03
- 0:10,5 gesto Question01
- 0:14,0 gesto HeadShake01
- 0:16,8 gesto Talk01
- 0:20,9 gesto HeadNod01
- 0:24,3 gesto Talk02
- 0:26,9 gesto Laugh01
- 0:30,6 gesto Talk03
- 0:34,7 gesto Question01
- 0:37,5 gesto HeadShake01
- 0:40,6 gesto Talk01
- 0:43,5 gesto HeadNod01
- 0:46,9 gesto Talk02
- 0:48,4 reposo 
- 0:48,5–0:58,5 anda de (6011,3, 6002,4) a (6000,2, 6002,6)
- 0:56,0–0:58,3 habla
- 0:56,0 gesto Cheer02
- 0:58,8 bucle InteractWithPeople_NoWeapon
- 0:59,9 gesto Talk01
- 1:03,7 gesto HeadNod01
- 1:07,5 gesto Talk02
- 1:10,2 gesto Laugh01
- 1:13,6 gesto Talk03
- 1:17,1 gesto Question01
- 1:20,6 reposo 
- 1:44,8–2:01,9 anda de (6000,3, 6002,6) a (6038,0, 5993,0)
- 2:15,7 bucle Fear01

### v09 (NPC_Aldeano_09)

- 0:01,4 bucle InteractWithPeople_NoWeapon
- 0:05,4 gesto Talk03
- 0:08,4 gesto Question01
- 0:12,2 gesto HeadShake01
- 0:15,1 gesto Talk01
- 0:19,0 gesto HeadNod01
- 0:21,9 gesto Talk02
- 0:26,0 gesto Laugh01
- 0:29,5 gesto Talk03
- 0:32,9 gesto Question01
- 0:36,8 gesto HeadShake01
- 0:39,9 gesto Talk01
- 0:44,2 gesto HeadNod01
- 0:47,6 gesto Talk02
- 0:52,0 gesto Laugh01
- 0:56,8 gesto Talk03
- 0:59,9 gesto Question01
- 1:04,0 gesto HeadShake01
- 1:07,2 gesto Talk01
- 1:10,6 gesto HeadNod01
- 1:14,4 gesto Talk02
- 1:17,9 gesto Laugh01
- 1:20,5 reposo 
- 1:27,4 gesto Fear01
- 1:44,1–2:02,6 anda de (5999,0, 6005,6) a (6041,0, 6006,0)

### v10 (NPC_Aldeano_10)

- 0:00,0 bucle Opening01_Loop
- 0:48,6 reposo 
- 1:27,3 gesto FoundSomething_NoWeapon
- 1:45,4–2:08,4 anda de (5994,0, 6002,7) a (6048,0, 5997,5)

