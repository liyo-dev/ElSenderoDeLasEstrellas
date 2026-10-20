# Parte de horneado: Prologo_UltimaNoche

2026-10-20 18:00 · guion `Assets/_SEQUENCES/Guiones/Prologo_UltimaNoche.guion.txt`

**3:35,9** de secuencia · 32 frases · 54 planos · 153 efectos · 13 personajes

Reparto medido (m): archimago 1,57/ojos 0,93 · liora 1,58/ojos 0,95 · mago 1,68/ojos 0,94 · nina 1,44/ojos 0,96 · v01 1,31/ojos 0,95 · v02 1,58/ojos 0,94 · v03 1,51/ojos 0,96 · v04 1,68/ojos 0,95 · v06 1,64/ojos 0,95 · v07 1,30/ojos 0,96 · v08 1,58/ojos 0,96 · v09 1,46/ojos 0,95 · v10 1,58/ojos 0,94

## Errores (0)


## Avisos (24)

- línea 139: v07 @saludo_fin+1.2  pasea por k4, k1, k5, k2, k3 ritmo=trote pausa=0.6: v07 roza a v02 a 358,4 s y no he encontrado otro camino. Mueve el destino o el momento.
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
- 0:33,6: v01 y v07 se atraviesan.
- 0:36,2: v01 y v07 se atraviesan.
- 0:39,6: v01 y v07 se atraviesan.
- 0:42,2: v01 y v07 se atraviesan.
- 0:45,0: v01 y v07 se atraviesan.
- 0:48,0: v01 y v07 se atraviesan.
- 1:02,6: v01 y v07 se atraviesan.
- 1:03,2: v01 y v07 se atraviesan.
- 1:06,0: v01 y v07 se atraviesan.
- 0:50,0: en «conjunto de nina y v03 y v08» v06 está 8 s en cuadro sin hacer nada. Dale algo en su pista (charla, bucle, pasea…).
- 1:51,1: en «medio de liora» archimago está 7 s en cuadro sin hacer nada. Dale algo en su pista (charla, bucle, pasea…).

## Silencios largos entre frases (10)

- 0:29,4 → 0:36,3: 6,9 s sin voz entre PROLOGO_FAVOR_CARRETA y PROLOGO_CARRETA_GRACIAS
- 0:47,4 → 0:50,0: 2,6 s sin voz entre PROLOGO_MANANA_GLOBO_OK y PROLOGO_GLOBO_VECINO_1
- 0:57,6 → 1:01,5: 3,8 s sin voz entre PROLOGO_GLOBO_VECINO_3 y PROLOGO_PLAZA_LIORA_1
- 1:17,2 → 1:35,1: 17,9 s sin voz entre PROLOGO_PLAZA_LIORA_2 y PROLOGO_DESPEDIDA_LIORA_1
- 1:40,4 → 1:43,0: 2,6 s sin voz entre PROLOGO_DESPEDIDA_ARCHIMAGO_1 y PROLOGO_HORA_ARCHIMAGO
- 1:58,6 → 2:00,5: 1,8 s sin voz entre PROLOGO_DESPEDIDA_ELEGIR y PROLOGO_EVACUACION_LIORA
- 2:04,0 → 2:14,3: 10,2 s sin voz entre PROLOGO_EVACUACION_LIORA y PROLOGO_DUELO_MAGO
- 2:23,7 → 2:36,0: 12,3 s sin voz entre PROLOGO_DUELO_ARCHIMAGO y PROLOGO_MAGO_VALLE
- 2:39,3 → 2:42,7: 3,4 s sin voz entre PROLOGO_MAGO_VALLE y PROLOGO_PLEGARIA_01
- 3:25,7 → 3:28,2: 2,5 s sin voz entre PROLOGO_PLEGARIA_06 y PROLOGO_HECHIZO

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
| 0:16,1 | plano 4 | medio de archimago · 3,7 s · lente 30° · coste 163: cara 100, ángulo 57 |
| 0:16,1 | archimago | «Buenos días, pueblo. ¿Qué se te ha estropeado hoy?» (3,7 s) · pose |
| 0:19,8 | plano 5 | conjunto de archimago y v01 y v02 · 4,0 s · lente 34° · coste 375: cara 302, ángulo 68 |
| 0:19,8 | v01 | «¡Buenos días, maestro!» (1,8 s) |
| 0:21,9 | v02 | «¡Muy buenas, maestro!» (1,8 s) |
| 0:23,8 | **@saludo_fin** |  |
| 0:23,8 | plano 6 | medio de v06 · 5,7 s · lente 30° · coste 2: ángulo 2 |
| 0:23,8 | v06 | «¡Maestro! Se me ha volcado otra vez la carreta. Y eso que hoy iba despacio.» (5,7 s) |
| 0:29,4 | plano 7 | conjunto de archimago y v06 · 6,9 s · lente 34° · coste 1196: pegado 100, estorbo 715, cara 317, ángulo 59 |
| 0:32,9 | **@carreta_magia** |  |
| 0:33,2 | efecto | VFX: 1 Flash_magic_ellow_blue en NPC_Archimago |
| 0:33,2 | efecto | SFX: Prologo_MagiaLevitar |
| 0:33,4 | efecto | VFX: 1 Flash_magic_ellow_blue en PROP_Carreta |
| 0:33,5 | efecto | Decorado: mover PROP_Carreta en 1,4s |
| 0:35,6 | efecto | Decorado: mover PROP_Carreta en 0,9s |
| 0:36,3 | plano 8 | dos: archimago y v06 · 4,2 s · lente 32° · coste 140: cara 134 |
| 0:36,3 | v06 | «¡Gracias, maestro! No habría podido enderezarla yo solo.» (4,2 s) · pose |
| 0:36,4 | efecto | VFX: 2 Flash_round_ellow en PROP_Carreta |
| 0:36,4 | efecto | SFX: Prologo_CarretaCae |
| 0:40,4 | plano 9 | sobre el hombro de archimago a nina · 4,3 s · lente 24° · coste 6: limpio |
| 0:40,4 | nina | «¡Maestro! El globo se ha enganchado en lo alto del campanario.» (4,3 s) · pose |
| 0:44,7 | plano 10 | sobre el hombro de nina a archimago · 2,6 s · lente 24° · coste 455: cara 99, eje 350 |
| 0:44,7 | archimago | «Con cuidado... Eso es.» (2,6 s) |
| 0:44,7 | efecto | Hora del día: AfterNoon |
| 0:45,1 | efecto | SFX: Prologo_MagiaLevitar |
| 0:45,1 | efecto | VFX: 1 Flash_magic_ellow_blue en NPC_Archimago |
| 0:45,6 | efecto | VFX: 1 Flash_magic_ellow_blue en PROP_Globo |
| 0:45,8 | efecto | VFX: 2 Flash_round_ellow en PROP_Globo |
| 0:47,4 | **@globo_sube** |  |
| 0:47,4 | plano 11 | general de el punto · 2,6 s · lente 48° |
| 0:47,4 | efecto | Decorado: mover PROP_Globo en 8,0s |
| 0:50,0 | plano 12 | conjunto de nina y v03 y v08 · 7,7 s · lente 34° · coste 1021: tapado 183, pegado 200, delante 300, cara 287, ángulo 45 |
| 0:50,0 | nina | «¡Hala, ya vuela!» (2,6 s) · pose |
| 0:52,9 | v03 | «¡Mira cómo sube!» (2,1 s) · pose |
| 0:55,3 | v08 | «¡Va a llegar hasta las nubes!» (2,3 s) |
| 0:57,6 | **@globo_fin** |  |
| 0:57,6 | plano 13 | medio de archimago · 2,2 s · lente 30° · coste 2: ángulo 2 |
| 0:59,6 | efecto | Apagar: PROP_Globo |
| 0:59,9 | **@rio** |  |
| 0:59,9 | plano 14 | siguiendo a liora · 6,6 s · lente 34° · mezcla 1,2 s |
| 0:59,9 | efecto | Ambiente: parar AMBIENTE |
| 0:59,9 | efecto | Ambiente: AMBIENTE_RIO ← Ambience_Prologo_Rio |
| 0:59,9 | efecto | Hora del día: Sunset |
| 0:59,9 | efecto | Post-procesado: Prologo_Atardecer (0s) |
| 0:59,9 | efecto | Sol: en cuadro, poniéndose en 19 s |
| 1:00,9 | **@rio_paseo** |  |
| 1:01,5 | liora | «¿Has pensado qué contestar a la academia? Llevas semanas con la carta.» (4,4 s) |
| 1:06,5 | plano 15 | sobre el hombro de liora a archimago · 5,7 s · lente 24° · coste 656: estorbo 300, eje 350 |
| 1:06,5 | archimago | «Hoy pensaba contestar. Pero siempre acaba surgiendo algo en el valle.» (5,7 s) |
| 1:12,2 | plano 16 | siguiendo a liora · 5,0 s · lente 34° |
| 1:12,2 | liora | «No tienes que quedarte solo porque aquí te necesiten. ¿Qué quieres hacer tú?» (5,0 s) · pose |
| 1:17,2 | plano 17 | dos: archimago y liora · 2,2 s · lente 34° · coste 580: estorbo 300, cara 265, ángulo 9 |
| 1:19,4 | **@cielo** |  |
| 1:19,4 | plano 18 | medio de archimago · 1,6 s · lente 30° · coste 423: cara 62, ángulo 11, eje 350 |
| 1:19,4 | efecto | Música: parar (3s) |
| 1:19,4 | efecto | Sol: devolverlo al ciclo |
| 1:19,4 | efecto | Hora del día: Night |
| 1:19,4 | efecto | Tiempo: encender Tormenta |
| 1:19,4 | efecto | Post-procesado: Prologo_Amenaza (6s) |
| 1:19,4 | efecto | SFX: SFX_Prologo_Viento |
| 1:19,4 | efecto | Ambiente: parar AMBIENTE_RIO |
| 1:19,4 | efecto | Tiempo: encender Viento |
| 1:21,0 | plano 19 | general de el punto · 2,8 s · lente 40° |
| 1:21,0 | efecto | Rayo: 2 × fondo del plano |
| 1:23,8 | plano 20 | conjunto de v02 y v10 y v01 y nina · 2,0 s · lente 34° · coste 14: ángulo 14 |
| 1:25,8 | **@llegada** |  |
| 1:25,8 | plano 21 | conjunto de mago · 2,6 s · lente 30° · coste 0: limpio |
| 1:25,8 | efecto | Sacudida de cámara (0,55, 0,6s) |
| 1:25,8 | efecto | SFX: Prologo_Golpe |
| 1:25,8 | efecto | Voz: ExteriorNoche, +0,0 dB |
| 1:25,8 | efecto | Fogonazo (0,18s) |
| 1:25,8 | efecto | Rayo: 1 × fondo del plano |
| 1:26,1 | efecto | SFX: Prologo_PresenciaOscura |
| 1:26,2 | efecto | Música: MAGOOSCURO_REVEAL |
| 1:26,4 | efecto | Ambiente: TORMENTA ← Ambience_Prologo_Tormenta |
| 1:26,4 | efecto | Tiempo: encender Lluvia |
| 1:28,4 | plano 22 | primer plano de mago · 2,2 s · lente 24° · coste 2: ángulo 2 |
| 1:30,6 | plano 23 | medio de archimago · 1,0 s · lente 30° · coste 73: cara 62, ángulo 11 |
| 1:31,6 | plano 24 | siguiendo a mago · 3,5 s · lente 34° |
| 1:35,1 | **@evacuar** |  |
| 1:35,1 | plano 25 | sobre el hombro de archimago a liora · 2,2 s · lente 24° · coste 95: cara 95 |
| 1:35,1 | liora | «¿Quién es? ¿Qué quiere?» (2,2 s) · pose |
| 1:35,1 | efecto | Ambiente: PANICO ← Ambience_Prologo_Panico |
| 1:35,1 | efecto | Encender: PROP_Incendio |
| 1:35,4 | efecto | SFX: SFX_Prologo_Grito |
| 1:35,7 | efecto | Rayo: 1 × fondo del plano |
| 1:37,3 | plano 26 | sobre el hombro de liora a archimago · 3,1 s · lente 24° · coste 95: cara 95 |
| 1:37,3 | archimago | «No lo sé. Llévatelos al puente, deprisa.» (3,1 s) · pose |
| 1:40,4 | plano 27 | medio de archimago · 5,2 s · lente 30° · coste 494: delante 455, ángulo 39 |
| 1:43,0 | **@huida** |  |
| 1:43,0 | archimago | «¡Al puente! ¡Todos, ahora!» (2,6 s) · pose |
| 1:45,6 | plano 28 | general de v06 y v04 y v09 y v03 · 1,6 s · lente 38° |
| 1:46,0 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 1:47,2 | plano 29 | dos: archimago y liora · 3,9 s · lente 32° · coste 1155: tapado 244, cara 878, ángulo 27 |
| 1:47,2 | liora | «¿Y tú?» (1,3 s) |
| 1:48,8 | archimago | «Ve. Yo me encargo de él.» (2,3 s) · pose |
| 1:51,1 | plano 30 | medio de liora · 7,5 s · lente 24° · coste 1119: cara 1050, ángulo 69 |
| 1:51,1 | liora | «Vuelve pronto.» (1,9 s) · pose |
| 1:53,2 | liora | «Y cuando vuelvas, nada de encargos. Te toca descansar.» (5,4 s) · pose |
| 1:58,6 | plano 31 | medio de archimago · 1,8 s · lente 24° · coste 79: cara 25, ángulo 48 |
| 2:00,5 | plano 32 | conjunto de liora y nina · 3,6 s · lente 34° · coste 301: cara 210, ángulo 86 |
| 2:00,5 | liora | «¡Vamos, Nina! ...Ten mucho cuidado, por favor.» (3,6 s) · pose |
| 2:04,0 | **@acercan** |  |
| 2:04,0 | plano 33 | siguiendo a archimago · 4,5 s · lente 34° |
| 2:05,2 | efecto | Rayo: 1 × fondo del plano |
| 2:06,0 | efecto | Ambiente: parar PANICO |
| 2:08,5 | plano 34 | siguiendo a mago · 5,7 s · lente 34° |
| 2:08,5 | efecto | Música: MAGOOSCURO_CLIMAX |
| 2:14,3 | **@duelo** |  |
| 2:14,3 | plano 35 | sobre el hombro de archimago a mago · 3,0 s · lente 24° · coste 0: limpio |
| 2:14,3 | mago | «¿Vas a salvarlos a todos tú solo?» (3,0 s) · pose |
| 2:14,3 | efecto | Mezcla: primeraVozOscura |
| 2:14,5 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 2:17,2 | plano 36 | primer plano de mago · 3,1 s · lente 24° · coste 26: ángulo 26 |
| 2:17,2 | mago | «Ni a ti mismo puedes salvarte.» (3,1 s) · pose |
| 2:20,3 | plano 37 | medio de archimago · 3,3 s · lente 30° · coste 2: ángulo 2 |
| 2:20,3 | archimago | «Mientras siga en pie, no llegarás a ellos.» (3,3 s) · pose |
| 2:20,3 | efecto | Mezcla: primeraVozOscura (quitar) |
| 2:23,7 | plano 38 | dos: archimago y mago · 2,5 s · lente 36° · coste 223: cara 109, ángulo 108 |
| 2:23,9 | efecto | Hechizo: NPC_MagoOscuro → NPC_Archimago |
| 2:24,0 | efecto | VFX: Magic shield blue en NPC_Archimago |
| 2:24,5 | efecto | SFX: Prologo_EscudoBloquea |
| 2:24,5 | efecto | Fogonazo (0,12s) |
| 2:24,5 | efecto | Sacudida de cámara (0,35, 0,4s) |
| 2:26,2 | plano 39 | sobre el hombro de mago a archimago · 6,7 s · lente 24° · coste 0: limpio |
| 2:26,4 | efecto | Hechizo: NPC_Archimago → NPC_MagoOscuro |
| 2:29,1 | **@choque** |  |
| 2:29,2 | efecto | SFX: Prologo_Carga |
| 2:29,2 | efecto | VFX: Magic circle en NPC_Archimago |
| 2:29,2 | efecto | VFX: Magic circle en NPC_MagoOscuro |
| 2:29,8 | efecto | VFX: Charge slash purple en NPC_MagoOscuro |
| 2:29,8 | efecto | SFX: SFX_Prologo_Latigazo |
| 2:29,8 | efecto | VFX: Charge slash blue en NPC_Archimago |
| 2:30,1 | efecto | Sacudida de cámara (0,75, 1s) |
| 2:30,1 | efecto | SFX: Prologo_Choque |
| 2:30,1 | efecto | Líneas de concentración: mostrar |
| 2:30,1 | efecto | VFX: Red energy explosion en choque |
| 2:30,1 | efecto | VFX: FX_LightPillar en choque |
| 2:30,1 | efecto | Fogonazo (0,28s) |
| 2:30,2 | efecto | SFX: Prologue_WarClashStinger_A |
| 2:32,9 | plano 40 | medio de archimago · 3,1 s · lente 30° · coste 2: ángulo 2 |
| 2:33,1 | efecto | SFX: Prologo_SueloSeAbre |
| 2:33,2 | efecto | VFX: MagoOscuroGrieta en NPC_Archimago |
| 2:33,4 | efecto | VFX: Ground AOE explosion en NPC_Archimago |
| 2:33,4 | efecto | Sacudida de cámara (0,7, 0,9s) |
| 2:33,5 | efecto | VFX: Dust ground en NPC_Archimago |
| 2:36,0 | **@ultimo** |  |
| 2:36,0 | plano 41 | primer plano de mago · 3,3 s · lente 24° · coste 2: ángulo 2 |
| 2:36,0 | mago | «Que este valle no haya existido nunca.» (3,3 s) · pose |
| 2:39,3 | plano 42 | general de mago · 3,4 s · lente 38° |
| 2:39,3 | efecto | Post-procesado: Prologo_Climax (2,5s) |
| 2:39,4 | efecto | SFX: Prologo_Despegue |
| 2:39,4 | efecto | VFX: Dust ground en NPC_MagoOscuro |
| 2:39,6 | efecto | Rayo: 2 × fondo del plano |
| 2:39,9 | efecto | SFX: Prologo_HechizoGrande |
| 2:40,1 | efecto | SFX: SFX_Prologo_AgujeroNegro_Carga |
| 2:40,1 | efecto | Ambiente: AGUJERO_NEGRO ← SFX_Prologo_AgujeroNegro_Bucle |
| 2:40,1 | efecto | VFX: VFX_AgujeroNegro en agujero |
| 2:40,1 | efecto | Escalar: CARGA_ESFERA ×0,35 en 0 s |
| 2:40,2 | efecto | Escalar: CARGA_ESFERA ×1 en 3,5 s |
| 2:40,3 | efecto | VFX: Magic circle en agujero |
| 2:40,3 | efecto | Sacudida de cámara (0,35, 1,6s) |
| 2:42,7 | plano 43 | medio de archimago · 7,3 s · lente 30° · coste 17: ángulo 17 |
| 2:42,7 | archimago | «No daré ni un paso atrás... ¡No dejaré que los toques! ¡Liora! ¡Nina! ¡A ninguno de ellos!» (7,3 s) |
| 2:42,7 | efecto | Ambiente: CORO ← SFX_Prologo_CoroBucle |
| 2:42,7 | efecto | Mezcla: plegaria |
| 2:42,7 | efecto | Voz: Plegaria, +1,5 dB |
| 2:42,9 | efecto | VFX: Healing circle en NPC_Archimago |
| 2:42,9 | efecto | SFX: Prologo_MagiaLevitar |
| 2:46,0 | efecto | Cut-in 0: NPC_Liora (Cara) |
| 2:47,7 | efecto | Cut-in 0: NPC_Aldeano_05 (Cara) |
| 2:50,0 | plano 44 | general de mago · 1,4 s · lente 36° |
| 2:50,0 | efecto | Escalar: CARGA_ESFERA ×1,5 en 2 s |
| 2:50,0 | efecto | VFX: Magic circle en agujero |
| 2:50,0 | efecto | Sacudida de cámara (0,25, 1s) |
| 2:51,4 | plano 45 | primer plano de archimago · 8,2 s · lente 24° · coste 2: ángulo 2 |
| 2:51,4 | archimago | «Más allá de la noche y del silencio... ¡Luz que naciste antes que el cielo y tendiste el camino entre las estrellas!» (8,2 s) |
| 2:51,8 | efecto | VFX: Magic circle en NPC_Archimago |
| 2:52,2 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 2:55,6 | efecto | VFX: 1 Flash_magic_ellow_blue en NPC_Archimago |
| 2:59,7 | plano 46 | medio de archimago · 8,6 s · lente 30° · coste 17: ángulo 17 |
| 2:59,7 | archimago | «Ningún poder de este mundo puede detenerlo... ¡Por eso llamo a lo que aún no existe! ¡Escucha mi voz y despierta en mis manos!» (8,6 s) |
| 3:02,9 | efecto | Rayo: 1 × fondo del plano |
| 3:03,2 | efecto | SFX: Prologo_Carga |
| 3:03,2 | efecto | VFX: Charge slash blue en NPC_Archimago |
| 3:05,3 | efecto | Escalar: CARGA_ESFERA ×2 en 2 s |
| 3:05,3 | efecto | Sacudida de cámara (0,35, 1,2s) |
| 3:08,3 | plano 47 | general de mago · 1,2 s · lente 34° |
| 3:08,3 | efecto | Rayo: 1 × fondo del plano |
| 3:09,5 | plano 48 | primer plano de archimago · 6,3 s · lente 24° · coste 17: ángulo 17 |
| 3:09,5 | archimago | «Que mi vida sea el precio y mi alma el sello... ¡No importa lo que quede de mí cuando la luz se apague!» (6,3 s) |
| 3:13,0 | efecto | SFX: Prologo_MagiaLevitar |
| 3:13,0 | efecto | Sacudida de cámara (0,3, 0,9s) |
| 3:13,0 | efecto | VFX: FX_LightPillar en NPC_Archimago |
| 3:15,8 | plano 49 | medio de archimago · 6,1 s · lente 30° · coste 2: ángulo 2 |
| 3:15,8 | archimago | «¡Haz de mi último aliento un escudo inquebrantable! ¡Arde conmigo y detén la oscuridad!» (6,1 s) |
| 3:15,8 | efecto | Líneas de concentración: mostrar |
| 3:16,1 | efecto | VFX: 2 Flash_round_ellow en NPC_Archimago |
| 3:19,2 | efecto | Rayo: 2 × fondo del plano |
| 3:19,3 | efecto | Escalar: CARGA_ESFERA ×2,6 en 1,4 s |
| 3:19,3 | efecto | SFX: Prologo_Carga |
| 3:19,3 | efecto | Sacudida de cámara (0,5, 1,6s) |
| 3:21,9 | plano 50 | primer plano de archimago · 6,3 s · lente 24° · coste 2: ángulo 2 |
| 3:21,9 | archimago | «Perdóname, Liora... No voy a volver a casa.» (3,8 s) · pose |
| 3:21,9 | efecto | Líneas de concentración: retirar |
| 3:26,1 | efecto | Líneas de concentración: mostrar |
| 3:26,1 | efecto | SFX: Prologo_Carga |
| 3:26,1 | efecto | Cut-in 0: NPC_MagoOscuro (Cara) |
| 3:26,1 | efecto | Mezcla: grito |
| 3:26,1 | efecto | Mezcla: plegaria (quitar) |
| 3:26,1 | efecto | Ambiente: parar CORO |
| 3:27,1 | efecto | Cut-in 1: NPC_Archimago (Cara) |
| 3:28,2 | **@hechizo** |  |
| 3:28,2 | plano 51 | general de archimago y mago · 2,0 s · lente 44° |
| 3:28,2 | archimago | «¡¡Protégelos a todos!!» (2,1 s) |
| 3:28,2 | efecto | Respiro: 0,9 s (música -10 dB) |
| 3:28,2 | efecto | Voz: Epico, +4,0 dB |
| 3:28,2 | efecto | Golpe musical: Prologue_WarClashStinger_A |
| 3:28,4 | efecto | Rayo: 2 × fondo del plano |
| 3:28,7 | efecto | Fusión: CARGA_ESFERA +  |
| 3:28,7 | efecto | SFX: Prologo_ProteccionAbsoluta |
| 3:28,7 | efecto | Sacudida de cámara (0,45, 2s) |
| 3:30,2 | plano 52 | primer plano de archimago · 1,3 s · lente 24° · coste 21: ángulo 21 |
| 3:30,2 | efecto | Sacudida de cámara (0,55, 1,3s) |
| 3:30,2 | efecto | Líneas de concentración: mostrar |
| 3:31,5 | plano 53 | primer plano de mago · 1,2 s · lente 24° · coste 115: cara 63, ángulo 53 |
| 3:31,5 | efecto | Sacudida de cámara (0,7, 1,2s) |
| 3:32,4 | **@explosion** |  |
| 3:32,4 | efecto | Ambiente: parar AGUJERO_NEGRO |
| 3:32,4 | efecto | VFX: recoger efectos registrados |
| 3:32,4 | efecto | Mezcla: grito (quitar) |
| 3:32,4 | efecto | Líneas de concentración: retirar |
| 3:32,4 | efecto | Pantalla: cubrir (1,2s) |
| 3:32,4 | efecto | Ambiente: parar TORMENTA |
| 3:32,4 | efecto | Golpe musical: Prologue_WarClashStinger_A |
| 3:32,7 | plano 54 | general de archimago y mago · 3,3 s · lente 48° |
| 3:32,7 | efecto | Sacudida de cámara (0,95, 1,4s) |
| 3:32,8 | efecto | Sacudida de cámara (0,9, 1,6s) |
| 3:33,3 | efecto | Música: parar (1,2s) |
| 3:33,4 | efecto | SFX: SFX_Prologo_SilencioBlanco |
| 3:33,6 | efecto | Hora del día: Morning (de golpe) (y es con la que se queda el mundo) |
| 3:33,8 | efecto | Fogonazo (0,14s) |
| 3:35,4 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### archimago (NPC_Archimago)

- 0:04,6–0:16,2 anda de (6002,6, 6012,3) a (6003,2, 6002,3)
- 0:16,1–0:19,8 habla
- 0:16,2 gesto HandWave02
- 0:29,5–0:32,9 anda de (6003,3, 6002,3) a (6006,4, 6000,6)
- 0:32,9 gesto MagicRight
- 0:36,3 gesto HeadNod01
- 0:44,7 bucle CastingIdle01
- 0:44,7–0:47,4 habla
- 0:47,4 reposo 
- 0:50,6 gesto Laugh01
- 0:57,6 gesto HeadNod01
- 1:00,9–1:06,5 anda de (6016,9, 6002,7) a (6017,4, 5998,3)
- 1:06,5–1:12,2 habla
- 1:06,5 gesto Talk02
- 1:37,3–1:40,4 habla
- 1:37,4 gesto HeadShake01
- 1:39,1 gesto Talk01
- 1:40,5–1:43,0 anda de (6017,4, 5998,3) a (6014,4, 6000,3)
- 1:43,0–1:45,6 habla
- 1:43,1 gesto Challenging_NoWeapon
- 1:48,8–1:51,1 habla
- 1:48,9 gesto HeadNod01
- 1:58,6 gesto HeadNod01
- 2:04,1–2:12,1 anda de (6014,4, 6000,3) a (6005,6, 5999,8)
- 2:20,3–2:23,7 habla
- 2:20,4 gesto Angry02
- 2:22,0 gesto Talk03
- 2:24,0 gesto Defend_NoWeapon
- 2:26,2 gesto MagicLeft
- 2:29,1 gesto MagicSpecial
- 2:33,5 bucle Pain01
- 2:42,7 bucle Challenging_NoWeapon
- 2:42,7 reposo 
- 2:42,7–2:50,0 habla
- 2:51,4 bucle CastingIdle01
- 2:51,4–2:59,7 habla
- 2:59,7–3:08,3 habla
- 3:03,0 gesto MagicSpecial
- 3:04,5 bucle CastingIdle01
- 3:09,5–3:15,8 habla
- 3:12,9 gesto Angry02
- 3:14,5 bucle MagicAttackOmni01_Load
- 3:15,8–3:21,9 habla
- 3:21,9 reposo 
- 3:21,9–3:25,7 habla
- 3:22,0 gesto Talk03
- 3:28,2 bucle MagicAttackOmni01_Load
- 3:28,2–3:30,2 habla

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
- 0:59,9 reposo 
- 1:00,9–1:06,5 anda de (6015,8, 6002,3) a (6016,1, 5997,9)
- 1:01,5–1:05,8 habla
- 1:12,2–1:17,2 habla
- 1:12,3 gesto Talk01
- 1:14,7 gesto Talk03
- 1:35,1–1:37,3 habla
- 1:35,2 gesto Question01
- 1:47,2–1:48,5 habla
- 1:47,3–1:48,1 anda de (6016,0, 5997,9) a (6015,0, 5997,8)
- 1:51,1–1:53,0 habla
- 1:51,2 gesto Talk01
- 1:53,2–1:58,6 habla
- 1:53,3 gesto Talk03
- 1:56,4 gesto Talk01
- 2:00,5–2:04,0 habla
- 2:00,6 gesto HandWave02
- 2:04,1–2:16,0 anda de (6015,0, 5997,9) a (6042,0, 6000,0)

### mago (NPC_MagoOscuro)

- 1:25,8 bucle Challenging_NoWeapon
- 1:25,8 aparece
- 1:31,6 reposo 
- 1:31,7–1:58,7 anda de (5957,8, 5996,9) a (5988,0, 6000,5)
- 2:04,1–2:14,3 anda de (5988,0, 6000,5) a (5999,4, 5999,8)
- 2:14,3–2:17,2 habla
- 2:14,4 gesto Talk02
- 2:17,2–2:20,3 habla
- 2:17,3 gesto Talk03
- 2:23,7 gesto MagicRight
- 2:27,0 gesto TakeDamage
- 2:29,1 gesto MagicSpecial
- 2:32,9 gesto MagicRight
- 2:36,0–2:39,3 habla
- 2:36,1 gesto Talk03
- 2:39,3 bucle MagicAttackOmni01_Load

### nina (NPC_Aldeano_05)

- 0:00,0 bucle SenseSomethingSearching_NoWeapon
- 0:33,3 reposo 
- 0:33,4–0:40,5 anda de (5990,9, 6001,0) a (6004,6, 5999,9)
- 0:40,4–0:44,7 habla
- 0:40,5 gesto Beg01
- 0:50,0–0:52,6 habla
- 0:50,0 gesto Cheer01
- 0:57,7–1:02,6 anda de (6004,5, 5999,9) a (5995,2, 6001,0)
- 1:02,6 bucle InteractWithPeople_NoWeapon
- 1:05,6 gesto Talk01
- 1:08,8 gesto HeadNod01
- 1:12,1 gesto Talk02
- 1:14,9 gesto Laugh01
- 1:17,8 gesto Talk03
- 1:19,7 reposo 
- 1:43,6–1:53,7 anda de (5995,2, 6001,0) a (6014,1, 5998,5)
- 2:04,1–2:14,5 anda de (6014,2, 5998,6) a (6026,6, 6007,5)

### v01 (NPC_Aldeano_01)

- 0:00,6–0:02,7 anda de (5995,5, 5998,6) a (5999,2, 5999,0)
- 0:03,1–0:04,6 anda de (5999,2, 5999,1) a (5999,0, 6001,6)
- 0:05,0–0:07,3 anda de (5998,9, 6001,6) a (5994,8, 6001,3)
- 0:07,7–0:09,3 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 0:09,7–0:11,7 anda de (5995,6, 5998,6) a (5999,2, 5999,0)
- 0:12,1–0:13,6 anda de (5999,2, 5999,1) a (5999,0, 6001,6)
- 0:14,0–0:20,0 anda de (5998,9, 6001,6) a (6002,3, 6000,6)
- 0:19,8–0:21,6 habla
- 0:19,9 gesto HandWave01
- 0:24,8–0:28,7 anda de (6002,2, 6000,6) a (5994,8, 6001,3)
- 0:29,2–0:30,8 anda de (5994,8, 6001,3) a (5995,5, 5998,6)
- 0:31,3–0:33,3 anda de (5995,6, 5998,6) a (5999,2, 5999,0)
- 0:34,2–0:36,2 anda de (5999,2, 5999,0) a (5999,0, 6001,6)
- 0:37,5–0:39,9 anda de (5999,0, 6001,6) a (5994,8, 6001,3)
- 0:40,4–0:41,9 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 0:42,4–0:44,5 anda de (5995,5, 5998,6) a (5999,2, 5999,0)
- 0:45,4–0:47,3 anda de (5999,2, 5999,1) a (5999,0, 6001,6)
- 0:47,8–0:51,0 anda de (5998,9, 6001,6) a (5993,4, 5999,2)
- 1:00,4–1:02,9 anda de (5993,5, 5999,2) a (5998,0, 6000,2)
- 1:03,4–1:04,4 anda de (5998,1, 6000,1) a (5999,2, 5999,0)
- 1:04,9–1:06,2 anda de (5999,1, 5998,9) a (5997,3, 5997,6)
- 1:06,7–1:07,9 anda de (5997,3, 5997,6) a (5995,5, 5998,6)
- 1:08,4–1:10,1 anda de (5995,5, 5998,6) a (5998,0, 6000,2)
- 1:10,6–1:11,6 anda de (5998,1, 6000,1) a (5999,2, 5999,0)
- 1:12,1–1:13,4 anda de (5999,1, 5998,9) a (5997,3, 5997,6)
- 1:13,9–1:15,1 anda de (5997,3, 5997,6) a (5995,5, 5998,6)
- 1:15,6–1:17,3 anda de (5995,5, 5998,6) a (5998,0, 6000,2)
- 1:17,8–1:18,8 anda de (5998,1, 6000,1) a (5999,2, 5999,0)
- 1:19,3–1:19,8 anda de (5999,1, 5998,9) a (5998,6, 5998,5)
- 1:19,7 reposo 
- 1:20,6–1:22,3 anda de (5998,5, 5998,6) a (5996,9, 6001,0)
- 1:45,4–2:05,3 anda de (5996,9, 6001,0) a (6040,0, 5990,5)

### v02 (NPC_Aldeano_02)

- 0:00,4 bucle InteractWithPeople_NoWeapon
- 0:04,1 gesto Talk02
- 0:08,3 gesto Laugh01
- 0:12,5 gesto Talk03
- 0:16,0 gesto Question01
- 0:20,4 gesto HeadShake01
- 0:21,9–0:23,8 habla
- 0:22,0 gesto HandWave02
- 0:59,1 bucle InteractWithPeople_NoWeapon
- 1:00,7 gesto Talk03
- 1:03,1 gesto Question01
- 1:06,8 gesto HeadShake01
- 1:09,2 gesto Talk01
- 1:12,7 gesto HeadNod01
- 1:16,0 gesto Talk02
- 1:19,8 reposo 
- 1:19,9 gesto SenseSomethingStart_NoWeapon
- 1:26,3 bucle Fear01
- 1:44,4–2:04,7 anda de (5996,1, 6001,9) a (6044,0, 6003,5)
- 2:14,8 bucle Beg01_Loop

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
- 0:47,4 reposo 
- 0:47,4–0:51,4 anda de (6000,3, 6004,9) a (5998,6, 6001,0)
- 0:52,9–0:55,0 habla
- 0:52,9 gesto FoundSomething_NoWeapon
- 0:57,6 bucle InteractWithPeople_NoWeapon
- 0:58,3 gesto Question01
- 1:00,6 gesto HeadShake01
- 1:03,5 gesto Talk01
- 1:06,4 gesto HeadNod01
- 1:10,1 gesto Talk02
- 1:12,6 gesto Laugh01
- 1:15,4 gesto Talk03
- 1:19,6 reposo 
- 1:26,4 gesto Fear01
- 1:43,9–2:04,0 anda de (5998,6, 6001,0) a (6046,0, 5999,0)

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
- 1:19,6 reposo 
- 1:25,7–1:33,4 anda de (6003,9, 5995,9) a (5997,8, 5995,8)
- 1:35,2–1:42,8 anda de (5997,8, 5995,8) a (6003,9, 5995,9)
- 1:43,2–2:00,2 anda de (6003,9, 5995,9) a (6041,6, 5996,6)

### v06 (NPC_Aldeano_06)

- 0:00,0 bucle Loot01_Loop
- 0:23,8–0:29,4 habla
- 0:23,8 gesto Question01
- 0:32,9 reposo 
- 0:36,3–0:40,4 habla
- 0:36,4 gesto Reverence01
- 0:38,9 gesto Talk01
- 0:57,8 bucle Opening01_Loop
- 1:19,9 reposo 
- 1:43,0–1:55,5 anda de (6010,4, 5999,9) a (6039,0, 6001,0)

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
- 0:32,5–0:33,9 anda de (5997,3, 5997,6) a (5999,2, 5999,0)
- 0:34,9–0:36,4 anda de (5999,2, 5999,1) a (5999,0, 6001,6)
- 0:37,0–0:39,3 anda de (5998,9, 6001,6) a (5994,8, 6001,3)
- 0:39,9–0:41,5 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 0:42,1–0:43,3 anda de (5995,6, 5998,5) a (5997,3, 5997,6)
- 0:43,9–0:45,2 anda de (5997,4, 5997,7) a (5999,2, 5999,0)
- 0:46,2–0:50,5 anda de (5999,2, 5999,0) a (5994,9, 5998,4)
- 1:00,5–1:02,4 anda de (5995,0, 5998,5) a (5998,0, 6000,2)
- 1:03,1–1:04,1 anda de (5998,0, 6000,2) a (5999,2, 5999,0)
- 1:04,8–1:06,2 anda de (5999,2, 5999,0) a (5997,3, 5997,6)
- 1:08,9–1:10,6 anda de (5997,2, 5997,7) a (5995,5, 5998,6)
- 1:11,3–1:12,9 anda de (5995,6, 5998,7) a (5998,0, 6000,2)
- 1:13,6–1:14,6 anda de (5998,0, 6000,2) a (5999,2, 5999,0)
- 1:15,3–1:16,7 anda de (5999,2, 5999,0) a (5997,3, 5997,6)
- 1:17,4–1:18,6 anda de (5997,2, 5997,6) a (5995,5, 5998,6)
- 1:19,3–1:22,1 anda de (5995,6, 5998,6) a (5999,0, 6001,6)
- 1:45,0–2:05,1 anda de (5999,0, 6001,6) a (6045,0, 6008,0)

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
- 0:47,8 reposo 
- 0:47,8–0:57,9 anda de (6011,4, 6002,4) a (6000,2, 6002,6)
- 0:55,3–0:57,6 habla
- 0:55,4 gesto Cheer02
- 0:58,1 bucle InteractWithPeople_NoWeapon
- 1:01,1 gesto HeadNod01
- 1:03,5 gesto Talk02
- 1:07,2 gesto Laugh01
- 1:11,0 gesto Talk03
- 1:13,7 gesto Question01
- 1:17,2 gesto HeadShake01
- 1:19,9 reposo 
- 1:44,1–2:01,2 anda de (6000,2, 6002,6) a (6038,0, 5993,0)
- 2:15,2 bucle Fear01

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
- 1:19,8 reposo 
- 1:26,7 gesto Fear01
- 1:43,4–2:02,0 anda de (5998,9, 6005,6) a (6041,0, 6006,0)

### v10 (NPC_Aldeano_10)

- 0:00,0 bucle Opening01_Loop
- 0:48,0 reposo 
- 1:26,6 gesto FoundSomething_NoWeapon
- 1:44,7–2:07,7 anda de (5994,0, 6002,8) a (6048,0, 5997,5)

