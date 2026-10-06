# Parte de horneado: Prologo_UltimaNoche

2026-10-05 18:12 · guion `Assets/_SEQUENCES/Guiones/Prologo_UltimaNoche.guion.txt`

**3:53,0** de secuencia · 32 frases · 54 planos · 138 efectos · 13 personajes

Reparto medido (m): archimago 1,57/ojos 0,93 · liora 1,58/ojos 0,95 · mago 1,68/ojos 0,94 · nina 1,44/ojos 0,96 · v01 1,31/ojos 0,95 · v02 1,58/ojos 0,94 · v03 1,51/ojos 0,96 · v04 1,68/ojos 0,95 · v06 1,64/ojos 0,95 · v07 1,30/ojos 0,96 · v08 1,58/ojos 0,96 · v09 1,46/ojos 0,95 · v10 1,58/ojos 0,94

## Errores (0)


## Avisos (22)

- línea 139: v07 @saludo_fin+1.2  pasea por k4, k1, k5, k2, k3 ritmo=trote pausa=0.6: v07 roza a v02 a 413,1 s y no he encontrado otro camino. Mueve el destino o el momento.
- línea 267: desde @carreta_magia+0.5 nina anda a nina_carro ritmo=trote llega_mirando=archimago: nina roza a v01 a 39,0 s y no he encontrado otro camino. Mueve el destino o el momento.
- línea 365: mago anda a ladera, entrada ritmo=normal llega_mirando=centro_plaza sin_esperar: no se llega del todo a (5988,0, 6000,5) por el NavMesh
- línea 228: plano general centro_plaza desde=cam_general altura=7 lente=46 suave=1.5: algo del decorado se mete delante y tapa un 47% del cuadro. Prueba lado= o desde=.
- línea 233: plano conjunto archimago desde=cam_calle altura=1.55 lente=36 suave=0.8: algo del decorado se mete delante y tapa un 60% del cuadro. Prueba lado= o desde=.
- línea 248: plano conjunto archimago v06 lado=frente: algo del decorado se mete delante y tapa un 15% del cuadro. Prueba lado= o desde=.
- línea 248: plano conjunto archimago v06 lado=frente: no hay ángulo limpio para «conjunto de archimago y v06» (algo tapa la cara). Prueba con desde= o lado=.
- línea 382: plano medio archimago lado=izq: algo del decorado se mete delante y tapa un 30% del cuadro. Prueba lado= o desde=.
- línea 391: plano dos archimago liora: no hay ángulo limpio para «dos: archimago y liora» (algo tapa la cara). Prueba con desde= o lado=.
- línea 394: plano medio liora lado=der: no cabe el contraplano sobre el hombro de nina; hago un plano medio de liora.
- línea 397: plano medio archimago lado=der: no cabe el contraplano sobre el hombro de liora; hago un plano medio de archimago.
- línea 461: plano medio archimago lado=der mano=0.3: algo del decorado se mete delante y tapa un 32% del cuadro. Prueba lado= o desde=.
- línea 477: plano general mago agujero contrapicado lente=40 suave=0.5: algo del decorado se mete delante y tapa un 42% del cuadro. Prueba lado= o desde=.
- 0:16,0: v01 y v07 se atraviesan.
- 0:33,2: v01 y v07 se atraviesan.
- 0:38,8: nina y v07 se atraviesan.
- 0:39,0: nina y v07 se atraviesan.
- 0:44,4: v01 y v07 se atraviesan.
- 0:45,2: v01 y v07 se atraviesan.
- 1:00,0: v01 y v07 se atraviesan.
- 1:03,0: v01 y v07 se atraviesan.
- 0:49,2: en «conjunto de nina y v03 y v08» v06 está 6 s en cuadro sin hacer nada. Dale algo en su pista (charla, bucle, pasea…).

## Silencios largos entre frases (10)

- 0:29,5 → 0:36,4: 6,9 s sin voz entre PROLOGO_FAVOR_CARRETA y PROLOGO_CARRETA_GRACIAS
- 0:46,6 → 0:49,2: 2,6 s sin voz entre PROLOGO_MANANA_GLOBO_OK y PROLOGO_GLOBO_VECINO_1
- 0:55,0 → 0:58,8: 3,8 s sin voz entre PROLOGO_GLOBO_VECINO_3 y PROLOGO_PLAZA_LIORA_1
- 1:14,3 → 1:32,2: 17,9 s sin voz entre PROLOGO_PLAZA_LIORA_2 y PROLOGO_DESPEDIDA_LIORA_1
- 1:37,2 → 1:39,8: 2,6 s sin voz entre PROLOGO_DESPEDIDA_ARCHIMAGO_1 y PROLOGO_HORA_ARCHIMAGO
- 1:53,6 → 1:55,4: 1,8 s sin voz entre PROLOGO_DESPEDIDA_ELEGIR y PROLOGO_EVACUACION_LIORA
- 1:59,3 → 2:09,6: 10,2 s sin voz entre PROLOGO_EVACUACION_LIORA y PROLOGO_DUELO_MAGO
- 2:19,1 → 2:31,5: 12,3 s sin voz entre PROLOGO_DUELO_ARCHIMAGO y PROLOGO_MAGO_VALLE
- 2:34,9 → 2:38,3: 3,4 s sin voz entre PROLOGO_MAGO_VALLE y PROLOGO_PLEGARIA_01
- 3:38,7 → 3:41,1: 2,4 s sin voz entre PROLOGO_PLEGARIA_06 y PROLOGO_HECHIZO

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
| 0:10,0 | plano 3 | conjunto de v01 y v07 y v02 · 6,1 s · lente 34° · coste 257: tapado 61, cara 142, ángulo 54 |
| 0:16,1 | **@saludo** |  |
| 0:16,1 | plano 4 | medio de archimago · 3,8 s · lente 30° · coste 37: cara 25, ángulo 6 |
| 0:16,1 | archimago | «Buenos días, valle. ¿Qué se te ha estropeado hoy?» (3,8 s) · pose |
| 0:20,0 | plano 5 | conjunto de archimago y v01 y v02 · 3,6 s · lente 34° · coste 732: delante 412, cara 270, ángulo 45 |
| 0:20,0 | v01 | «¡Buenos días, maestro!» (1,6 s) · pose |
| 0:21,9 | v02 | «¡Muy buenas, maestro!» (1,6 s) |
| 0:23,5 | **@saludo_fin** |  |
| 0:23,5 | plano 6 | medio de v06 · 6,0 s · lente 30° · coste 2: ángulo 2 |
| 0:23,5 | v06 | «¡Maestro! Se me ha volcado otra vez la carreta. Y eso que hoy iba despacio.» (6,0 s) |
| 0:29,5 | plano 7 | conjunto de archimago y v06 · 6,9 s · lente 34° · coste 1075: tapado 61, pegado 100, estorbo 513, cara 341, ángulo 54 |
| 0:33,0 | **@carreta_magia** |  |
| 0:33,3 | efecto | SFX: Prologo_MagiaLevitar |
| 0:33,3 | efecto | VFX: 1 Flash_magic_ellow_blue en NPC_Archimago |
| 0:33,5 | efecto | VFX: 1 Flash_magic_ellow_blue en PROP_Carreta |
| 0:33,6 | efecto | Decorado: mover PROP_Carreta en 1,4s |
| 0:35,7 | efecto | Decorado: mover PROP_Carreta en 0,9s |
| 0:36,4 | plano 8 | dos: archimago y v06 · 3,3 s · lente 32° · coste 140: cara 134 |
| 0:36,4 | v06 | «¡Gracias, maestro! No habría podido enderezarla yo solo.» (3,3 s) · pose |
| 0:36,5 | efecto | SFX: Prologo_CarretaCae |
| 0:36,5 | efecto | VFX: 2 Flash_round_ellow en PROP_Carreta |
| 0:39,7 | plano 9 | medio de nina · 4,3 s · lente 30° · coste 7: ángulo 2 |
| 0:39,7 | nina | «¡Maestro! El globo se ha enganchado en lo alto del campanario.» (4,3 s) |
| 0:43,9 | plano 10 | sobre el hombro de nina a archimago · 2,6 s · lente 24° · coste 455: cara 99, eje 350 |
| 0:43,9 | archimago | «Con cuidado... Eso es.» (2,6 s) |
| 0:43,9 | efecto | Hora del día: AfterNoon |
| 0:44,3 | efecto | SFX: Prologo_MagiaLevitar |
| 0:44,3 | efecto | VFX: 1 Flash_magic_ellow_blue en NPC_Archimago |
| 0:44,8 | efecto | VFX: 1 Flash_magic_ellow_blue en PROP_Globo |
| 0:45,0 | efecto | VFX: 2 Flash_round_ellow en PROP_Globo |
| 0:46,6 | **@globo_sube** |  |
| 0:46,6 | plano 11 | general de el punto · 2,6 s · lente 48° |
| 0:46,6 | efecto | Decorado: mover PROP_Globo en 8,0s |
| 0:49,2 | plano 12 | conjunto de nina y v03 y v08 · 5,8 s · lente 34° · coste 632: delante 300, cara 276, ángulo 50 |
| 0:49,2 | nina | «¡Hala, ya vuela!» (1,9 s) · pose |
| 0:51,4 | v03 | «¡Mira cómo sube!» (1,4 s) · pose |
| 0:53,1 | v08 | «¡Va a llegar hasta las nubes!» (1,9 s) |
| 0:55,0 | **@globo_fin** |  |
| 0:55,0 | plano 13 | medio de archimago · 2,2 s · lente 30° · coste 2: ángulo 2 |
| 0:57,0 | efecto | Apagar: PROP_Globo |
| 0:57,2 | **@rio** |  |
| 0:57,2 | plano 14 | siguiendo a liora · 6,6 s · lente 34° · mezcla 1,2 s |
| 0:57,2 | efecto | Ambiente: parar AMBIENTE |
| 0:57,2 | efecto | Ambiente: AMBIENTE_RIO ← Ambience_Prologo_Rio |
| 0:57,2 | efecto | Hora del día: Sunset |
| 0:57,2 | efecto | Post-procesado: Prologo_Atardecer (0s) |
| 0:57,2 | efecto | Sol: en cuadro, poniéndose en 19 s |
| 0:58,2 | **@rio_paseo** |  |
| 0:58,8 | liora | «¿Has pensado qué contestar a la academia? Llevas semanas con la carta.» (4,4 s) |
| 1:03,8 | plano 15 | sobre el hombro de liora a archimago · 5,8 s · lente 24° · coste 656: estorbo 300, eje 350 |
| 1:03,8 | archimago | «Hoy pensaba contestar. Pero siempre acaba surgiendo algo en el valle.» (5,8 s) |
| 1:09,6 | plano 16 | siguiendo a liora · 4,7 s · lente 34° |
| 1:09,6 | liora | «No tienes que quedarte solo porque aquí te necesiten. ¿Qué quieres hacer tú?» (4,7 s) · pose |
| 1:14,3 | plano 17 | dos: archimago y liora · 2,2 s · lente 34° · coste 36: ángulo 36 |
| 1:16,5 | **@cielo** |  |
| 1:16,5 | plano 18 | medio de archimago · 1,6 s · lente 30° · coste 283: cara 212, ángulo 71 |
| 1:16,5 | efecto | Sol: devolverlo al ciclo |
| 1:16,5 | efecto | Música: parar (3s) |
| 1:16,5 | efecto | Tiempo: encender Tormenta |
| 1:16,5 | efecto | Hora del día: Night |
| 1:16,5 | efecto | SFX: SFX_Prologo_Viento |
| 1:16,5 | efecto | Tiempo: encender Viento |
| 1:16,5 | efecto | Post-procesado: Prologo_Amenaza (6s) |
| 1:16,5 | efecto | Ambiente: parar AMBIENTE_RIO |
| 1:18,1 | plano 19 | general de el punto · 2,8 s · lente 40° |
| 1:18,1 | efecto | Rayo: 2 × fondo del plano |
| 1:20,9 | plano 20 | conjunto de v02 y v10 y v01 y nina · 2,0 s · lente 34° · coste 14: ángulo 14 |
| 1:22,9 | **@llegada** |  |
| 1:22,9 | plano 21 | conjunto de mago · 2,6 s · lente 30° · coste 0: limpio |
| 1:22,9 | efecto | Sacudida de cámara (0,55, 0,6s) |
| 1:22,9 | efecto | Rayo: 1 × fondo del plano |
| 1:22,9 | efecto | SFX: Prologo_Golpe |
| 1:22,9 | efecto | Voz: ExteriorNoche, +0,0 dB |
| 1:22,9 | efecto | Fogonazo (0,18s) |
| 1:23,2 | efecto | SFX: Prologo_PresenciaOscura |
| 1:23,3 | efecto | Música: MAGOOSCURO_REVEAL |
| 1:23,5 | efecto | Ambiente: TORMENTA ← Ambience_Prologo_Tormenta |
| 1:23,5 | efecto | Tiempo: encender Lluvia |
| 1:25,5 | plano 22 | primer plano de mago · 2,2 s · lente 24° · coste 2: ángulo 2 |
| 1:27,7 | plano 23 | medio de archimago · 1,0 s · lente 30° · coste 31: cara 25, ángulo 6 |
| 1:28,7 | plano 24 | siguiendo a mago · 3,5 s · lente 34° |
| 1:32,2 | **@evacuar** |  |
| 1:32,2 | plano 25 | sobre el hombro de archimago a liora · 1,9 s · lente 24° · coste 95: cara 95 |
| 1:32,2 | liora | «¿Quién es? ¿Qué quiere?» (1,9 s) · pose |
| 1:32,2 | efecto | Encender: PROP_Incendio |
| 1:32,2 | efecto | Ambiente: PANICO ← Ambience_Prologo_Panico |
| 1:32,5 | efecto | SFX: SFX_Prologo_Grito |
| 1:32,8 | efecto | Rayo: 1 × fondo del plano |
| 1:34,2 | plano 26 | sobre el hombro de liora a archimago · 3,1 s · lente 24° · coste 95: cara 95 |
| 1:34,2 | archimago | «No lo sé. Llévatelos al puente, deprisa.» (3,1 s) · pose |
| 1:37,2 | plano 27 | medio de archimago · 5,3 s · lente 30° · coste 391: pegado 100, delante 254, cara 25, ángulo 6 |
| 1:39,8 | **@huida** |  |
| 1:39,8 | archimago | «¡Al puente! ¡Todos, ahora!» (2,7 s) · pose |
| 1:42,5 | plano 28 | general de v06 y v04 y v09 y v03 · 1,6 s · lente 38° |
| 1:42,8 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 1:44,1 | plano 29 | dos: archimago y liora · 3,5 s · lente 32° · coste 1047: tapado 244, delante 529, cara 254, ángulo 14 |
| 1:44,1 | liora | «¿Y tú?» (1,0 s) |
| 1:45,4 | archimago | «Ve. Yo me encargo de él.» (2,2 s) · pose |
| 1:47,6 | plano 30 | medio de liora · 5,9 s · lente 24° · coste 914: pegado 200, delante 425, cara 250, ángulo 33 |
| 1:47,6 | liora | «Vuelve pronto.» (1,5 s) · pose |
| 1:49,3 | liora | «Y cuando vuelvas, nada de encargos. Te toca descansar.» (4,3 s) · pose |
| 1:53,6 | plano 31 | medio de archimago · 1,8 s · lente 24° · coste 50: ángulo 44 |
| 1:55,4 | plano 32 | conjunto de liora y nina · 3,9 s · lente 34° · coste 129: cara 105, ángulo 18 |
| 1:55,4 | liora | «¡Al puente, todos! ¡Deprisa, no miréis atrás!» (3,9 s) · pose |
| 1:59,3 | **@acercan** |  |
| 1:59,3 | plano 33 | siguiendo a archimago · 4,5 s · lente 34° |
| 2:00,5 | efecto | Rayo: 1 × fondo del plano |
| 2:01,3 | efecto | Ambiente: parar PANICO |
| 2:03,8 | plano 34 | siguiendo a mago · 5,7 s · lente 34° |
| 2:03,8 | efecto | Música: MAGOOSCURO_CLIMAX |
| 2:09,6 | **@duelo** |  |
| 2:09,6 | plano 35 | sobre el hombro de archimago a mago · 3,1 s · lente 24° · coste 0: limpio |
| 2:09,6 | mago | «¿Vas a salvarlos a todos tú solo?» (3,1 s) · pose |
| 2:09,6 | efecto | Mezcla: primeraVozOscura |
| 2:09,8 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 2:12,6 | plano 36 | primer plano de mago · 3,2 s · lente 24° · coste 26: ángulo 26 |
| 2:12,6 | mago | «Ni a ti mismo puedes salvarte.» (3,2 s) · pose |
| 2:15,9 | plano 37 | medio de archimago · 3,3 s · lente 30° · coste 2: ángulo 2 |
| 2:15,9 | archimago | «Mientras siga en pie, no llegarás a ellos.» (3,3 s) · pose |
| 2:15,9 | efecto | Mezcla: primeraVozOscura (quitar) |
| 2:19,1 | plano 38 | dos: archimago y mago · 2,5 s · lente 36° · coste 223: cara 109, ángulo 108 |
| 2:19,3 | efecto | Hechizo: NPC_MagoOscuro → NPC_Archimago |
| 2:19,4 | efecto | VFX: Magic shield blue en NPC_Archimago |
| 2:19,9 | efecto | SFX: Prologo_EscudoBloquea |
| 2:19,9 | efecto | Fogonazo (0,12s) |
| 2:19,9 | efecto | Sacudida de cámara (0,35, 0,4s) |
| 2:21,6 | plano 39 | sobre el hombro de mago a archimago · 6,7 s · lente 24° · coste 0: limpio |
| 2:21,8 | efecto | Hechizo: NPC_Archimago → NPC_MagoOscuro |
| 2:24,6 | **@choque** |  |
| 2:24,7 | efecto | SFX: Prologo_Carga |
| 2:24,7 | efecto | VFX: Magic circle en NPC_Archimago |
| 2:24,7 | efecto | VFX: Magic circle en NPC_MagoOscuro |
| 2:25,3 | efecto | VFX: Charge slash blue en NPC_Archimago |
| 2:25,3 | efecto | VFX: Charge slash purple en NPC_MagoOscuro |
| 2:25,3 | efecto | SFX: SFX_Prologo_Latigazo |
| 2:25,6 | efecto | SFX: Prologo_Choque |
| 2:25,6 | efecto | Líneas de concentración: mostrar |
| 2:25,6 | efecto | VFX: Red energy explosion en choque |
| 2:25,6 | efecto | VFX: FX_LightPillar en choque |
| 2:25,6 | efecto | Fogonazo (0,28s) |
| 2:25,6 | efecto | Sacudida de cámara (0,75, 1s) |
| 2:25,7 | efecto | SFX: Prologue_WarClashStinger_A |
| 2:28,4 | plano 40 | medio de archimago · 3,1 s · lente 30° · coste 30: ángulo 30 |
| 2:28,6 | efecto | SFX: Prologo_SueloSeAbre |
| 2:28,7 | efecto | VFX: MagoOscuroGrieta en NPC_Archimago |
| 2:28,9 | efecto | VFX: Ground AOE explosion en NPC_Archimago |
| 2:28,9 | efecto | Sacudida de cámara (0,7, 0,9s) |
| 2:29,0 | efecto | VFX: Dust ground en NPC_Archimago |
| 2:31,5 | **@ultimo** |  |
| 2:31,5 | plano 41 | primer plano de mago · 3,4 s · lente 24° · coste 2: ángulo 2 |
| 2:31,5 | mago | «Que este valle no haya existido nunca.» (3,4 s) · pose |
| 2:34,9 | plano 42 | general de mago · 3,4 s · lente 40° · coste 509: estorbo 480, ángulo 23 |
| 2:34,9 | efecto | Post-procesado: Prologo_Climax (2,5s) |
| 2:35,0 | efecto | SFX: Prologo_Despegue |
| 2:35,0 | efecto | VFX: Dust ground en NPC_MagoOscuro |
| 2:35,1 | efecto | Rayo: 2 × fondo del plano |
| 2:35,5 | efecto | SFX: Prologo_HechizoGrande |
| 2:35,7 | efecto | SFX: SFX_Prologo_AgujeroNegro_Carga |
| 2:35,7 | efecto | Ambiente: AGUJERO_NEGRO ← SFX_Prologo_AgujeroNegro_Bucle |
| 2:35,7 | efecto | VFX: VFX_AgujeroNegro en agujero |
| 2:35,7 | efecto | Escalar: CARGA_ESFERA ×0,35 en 0 s |
| 2:35,8 | efecto | Escalar: CARGA_ESFERA ×1 en 3,5 s |
| 2:35,9 | efecto | VFX: Magic circle en agujero |
| 2:35,9 | efecto | Sacudida de cámara (0,35, 1,6s) |
| 2:38,3 | plano 43 | medio de archimago · 6,8 s · lente 30° · coste 12: ángulo 12 |
| 2:38,3 | archimago | «No... No voy a dejar que los toques. ¡Liora! ¡Nina! ¡Todos vosotros!» (6,8 s) |
| 2:38,3 | efecto | Voz: Ninguno, +0,0 dB |
| 2:38,3 | efecto | Mezcla: plegaria |
| 2:38,3 | efecto | Ambiente: CORO ← SFX_Prologo_CoroBucle |
| 2:38,5 | efecto | VFX: Healing circle en NPC_Archimago |
| 2:38,5 | efecto | SFX: Prologo_MagiaLevitar |
| 2:41,3 | efecto | Cut-in 0: NPC_Liora (Cara) |
| 2:43,1 | efecto | Cut-in 0: NPC_Aldeano_05 (Cara) |
| 2:45,1 | plano 44 | general de mago · 1,5 s · lente 36° · coste 33: ángulo 27 |
| 2:45,1 | efecto | Sacudida de cámara (0,2, 1s) |
| 2:45,1 | efecto | VFX: Magic circle en agujero |
| 2:45,1 | efecto | Escalar: CARGA_ESFERA ×1,5 en 2 s |
| 2:46,6 | plano 45 | primer plano de archimago · 13,5 s · lente 24° · coste 2: ángulo 2 |
| 2:46,6 | archimago | «Luz primera... tú que ardías antes de que hubiera cielo... tú que encendiste las estrellas y tendiste caminos entre ellas cuando aún no había nadie para recorrerlos...» (13,5 s) |
| 2:47,6 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 3:00,0 | plano 46 | medio de archimago · 13,5 s · lente 30° · coste 12: ángulo 12 |
| 3:00,0 | archimago | «No conozco ningún hechizo capaz de pararlo... Así que no te pido uno que exista. Nada arde sin ti... Te lo ruego... ¡préstame tu fuego!» (13,5 s) |
| 3:06,0 | efecto | Escalar: CARGA_ESFERA ×2 en 2 s |
| 3:06,0 | efecto | Sacudida de cámara (0,3, 1,2s) |
| 3:13,6 | plano 47 | general de mago · 1,3 s · lente 34° · coste 33: ángulo 27 |
| 3:14,9 | plano 48 | primer plano de archimago · 8,8 s · lente 24° · coste 12: ángulo 12 |
| 3:14,9 | archimago | «Sé que de esto no se vuelve. Sé que nunca contestaré esa carta... ¡Me da igual lo que quede de mí!» (8,8 s) |
| 3:23,6 | plano 49 | medio de archimago · 11,4 s · lente 30° · coste 2: ángulo 2 |
| 3:23,6 | archimago | «¡Toma mis años! ¡Toma mi voz, mi nombre! ¡Llévate mi alma si la quieres! ¡Pero que ellos vean amanecer!» (11,4 s) |
| 3:23,6 | efecto | Líneas de concentración: mostrar |
| 3:27,6 | efecto | Escalar: CARGA_ESFERA ×2,6 en 1,4 s |
| 3:27,6 | efecto | SFX: Prologo_Carga |
| 3:27,6 | efecto | Sacudida de cámara (0,45, 1,6s) |
| 3:35,0 | plano 50 | primer plano de archimago · 6,1 s · lente 24° · coste 2: ángulo 2 |
| 3:35,0 | archimago | «Perdóname, Liora... no voy a volver pronto.» (3,7 s) |
| 3:35,0 | efecto | Líneas de concentración: retirar |
| 3:39,1 | efecto | Mezcla: plegaria (quitar) |
| 3:39,1 | efecto | Mezcla: grito |
| 3:39,1 | efecto | Ambiente: parar CORO |
| 3:39,1 | efecto | Cut-in 0: NPC_MagoOscuro (Cara) |
| 3:39,1 | efecto | SFX: Prologo_Carga |
| 3:39,1 | efecto | Líneas de concentración: mostrar |
| 3:41,1 | **@hechizo** |  |
| 3:41,1 | plano 51 | general de archimago y mago · 2,6 s · lente 44° |
| 3:41,1 | archimago | «¡Protégelos a todos!» (7,4 s) |
| 3:41,1 | efecto | Voz: Ninguno, +4,0 dB |
| 3:41,7 | efecto | Fusión: CARGA_ESFERA +  |
| 3:41,7 | efecto | SFX: Prologo_ProteccionAbsoluta |
| 3:41,7 | efecto | Sacudida de cámara (0,35, 2s) |
| 3:43,7 | plano 52 | primer plano de archimago · 1,6 s · lente 24° · coste 26: ángulo 26 |
| 3:43,7 | efecto | Sacudida de cámara (0,5, 1,6s) |
| 3:43,7 | efecto | Líneas de concentración: mostrar |
| 3:45,3 | plano 53 | primer plano de mago · 1,2 s · lente 24° · coste 11: ángulo 3 |
| 3:45,3 | efecto | Sacudida de cámara (0,65, 1,2s) |
| 3:46,5 | plano 54 | general de archimago y mago · 6,5 s · lente 48° |
| 3:46,5 | efecto | Sacudida de cámara (0,9, 1,4s) |
| 3:47,7 | efecto | Fogonazo (0,12s) |
| 3:49,5 | **@explosion** |  |
| 3:49,5 | efecto | Ambiente: parar AGUJERO_NEGRO |
| 3:49,5 | efecto | VFX: recoger efectos registrados |
| 3:49,5 | efecto | Mezcla: grito (quitar) |
| 3:49,5 | efecto | Líneas de concentración: retirar |
| 3:49,5 | efecto | Pantalla: cubrir (1,2s) |
| 3:49,5 | efecto | Ambiente: parar TORMENTA |
| 3:49,9 | efecto | SFX: Prologue_WarClashStinger_B |
| 3:49,9 | efecto | Sacudida de cámara (0,9, 1,6s) |
| 3:50,4 | efecto | Música: parar (1,2s) |
| 3:50,5 | efecto | SFX: SFX_Prologo_SilencioBlanco |
| 3:50,7 | efecto | Hora del día: Morning (de golpe) (y es con la que se queda el mundo) |
| 3:52,5 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### archimago (NPC_Archimago)

- 0:04,6–0:16,2 anda de (6002,6, 6012,3) a (6003,2, 6002,3)
- 0:16,1–0:20,0 habla
- 0:16,2 gesto HandWave02
- 0:29,6–0:33,0 anda de (6003,2, 6002,3) a (6006,4, 6000,6)
- 0:33,0 gesto MagicRight
- 0:36,4 gesto HeadNod01
- 0:43,9 bucle CastingIdle01
- 0:43,9–0:46,6 habla
- 0:46,6 reposo 
- 0:49,8 gesto Laugh01
- 0:55,0 gesto HeadNod01
- 0:58,3–1:03,9 anda de (6016,9, 6002,7) a (6017,4, 5998,3)
- 1:03,8–1:09,6 habla
- 1:03,9 gesto Talk02
- 1:34,2–1:37,2 habla
- 1:34,3 gesto HeadShake01
- 1:37,3–1:39,8 anda de (6017,3, 5998,3) a (6014,4, 6000,3)
- 1:39,8–1:42,5 habla
- 1:39,9 gesto Challenging_NoWeapon
- 1:45,4–1:47,6 habla
- 1:45,5 gesto HeadNod01
- 1:53,6 gesto HeadNod01
- 1:59,4–2:07,4 anda de (6014,4, 6000,3) a (6005,6, 5999,8)
- 2:15,9–2:19,1 habla
- 2:15,9 gesto Angry02
- 2:19,4 gesto Defend_NoWeapon
- 2:21,6 gesto MagicLeft
- 2:24,6 gesto MagicSpecial
- 2:28,9 bucle Pain01
- 2:38,3 bucle Reverence01_Loop
- 2:38,3 reposo 
- 2:38,3–2:45,1 habla
- 2:46,6 bucle Beg01_Loop
- 2:46,6–3:00,0 habla
- 3:00,0–3:13,6 habla
- 3:09,9 reposo 
- 3:09,9 gesto Angry02
- 3:11,4 bucle Beg01_Loop
- 3:14,9–3:23,6 habla
- 3:23,6–3:35,0 habla
- 3:35,0–3:38,7 habla
- 3:41,1 bucle MagicAttackOmni01_Load
- 3:41,1–3:48,5 habla

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
- 0:57,2 reposo 
- 0:58,3–1:03,9 anda de (6015,8, 6002,3) a (6016,1, 5997,9)
- 0:58,8–1:03,3 habla
- 1:09,6–1:14,3 habla
- 1:09,7 gesto Talk01
- 1:32,2–1:34,2 habla
- 1:32,3 gesto Question01
- 1:44,1–1:45,1 habla
- 1:44,2–1:45,0 anda de (6016,0, 5997,9) a (6015,0, 5997,8)
- 1:47,6–1:49,2 habla
- 1:49,3–1:53,6 habla
- 1:49,4 gesto Talk03
- 1:55,4–1:59,3 habla
- 1:55,5 gesto HandWave02
- 1:59,4–2:11,3 anda de (6015,1, 5997,9) a (6042,0, 6000,0)

### mago (NPC_MagoOscuro)

- 1:22,9 bucle Challenging_NoWeapon
- 1:22,9 aparece
- 1:28,7 reposo 
- 1:28,8–1:55,9 anda de (5957,7, 5996,9) a (5988,0, 6000,5)
- 1:59,4–2:09,6 anda de (5988,0, 6000,5) a (5999,4, 5999,8)
- 2:09,6–2:12,6 habla
- 2:09,7 gesto Talk02
- 2:12,6–2:15,9 habla
- 2:19,1 gesto MagicRight
- 2:22,5 gesto TakeDamage
- 2:24,6 gesto MagicSpecial
- 2:28,4 gesto MagicRight
- 2:31,5–2:34,9 habla
- 2:34,9 bucle MagicAttackOmni01_Load

### nina (NPC_Aldeano_05)

- 0:00,0 bucle SenseSomethingSearching_NoWeapon
- 0:33,4 reposo 
- 0:36,7–0:43,8 anda de (5990,9, 6001,0) a (6004,6, 5999,9)
- 0:39,7–0:43,9 habla
- 0:39,8 gesto Beg01
- 0:49,2–0:51,1 habla
- 0:49,3 gesto Cheer01
- 0:55,0–0:60,0 anda de (6004,6, 5999,9) a (5995,2, 6001,0)
- 0:60,0 bucle InteractWithPeople_NoWeapon
- 1:03,0 gesto Talk01
- 1:06,2 gesto HeadNod01
- 1:09,4 gesto Talk02
- 1:12,3 gesto Laugh01
- 1:15,1 gesto Talk03
- 1:16,8 reposo 
- 1:40,4–1:50,5 anda de (5995,2, 6001,0) a (6014,1, 5998,5)
- 1:59,4–2:09,8 anda de (6014,2, 5998,6) a (6026,6, 6007,5)

### v01 (NPC_Aldeano_01)

- 0:00,6–0:02,7 anda de (5995,5, 5998,6) a (5999,2, 5999,0)
- 0:03,1–0:04,6 anda de (5999,2, 5999,1) a (5999,0, 6001,6)
- 0:05,0–0:07,3 anda de (5998,9, 6001,6) a (5994,8, 6001,3)
- 0:07,7–0:09,3 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 0:09,7–0:11,7 anda de (5995,6, 5998,6) a (5999,2, 5999,0)
- 0:12,1–0:13,6 anda de (5999,2, 5999,1) a (5999,0, 6001,6)
- 0:14,0–0:20,0 anda de (5998,9, 6001,6) a (6002,3, 6000,6)
- 0:20,0–0:21,6 habla
- 0:20,1 gesto HandWave01
- 0:24,6–0:28,5 anda de (6002,2, 6000,6) a (5994,8, 6001,3)
- 0:29,0–0:30,6 anda de (5994,8, 6001,3) a (5995,5, 5998,6)
- 0:31,5–0:33,5 anda de (5995,6, 5998,6) a (5999,2, 5999,0)
- 0:34,4–0:36,4 anda de (5999,2, 5999,0) a (5999,0, 6001,6)
- 0:37,3–0:39,7 anda de (5998,9, 6001,6) a (5994,8, 6001,3)
- 0:40,2–0:41,7 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 0:42,2–0:44,3 anda de (5995,5, 5998,6) a (5999,2, 5999,0)
- 0:45,2–0:46,6 anda de (5999,2, 5999,1) a (5999,8, 6001,6)
- 0:47,4–0:51,1 anda de (5999,8, 6001,5) a (5993,4, 5999,2)
- 0:57,7–1:00,3 anda de (5993,4, 5999,2) a (5998,0, 6000,2)
- 1:00,8–1:01,8 anda de (5998,1, 6000,1) a (5999,2, 5999,0)
- 1:02,3–1:03,6 anda de (5999,1, 5998,9) a (5997,3, 5997,6)
- 1:04,1–1:05,3 anda de (5997,3, 5997,6) a (5995,5, 5998,6)
- 1:05,8–1:07,5 anda de (5995,5, 5998,6) a (5998,0, 6000,2)
- 1:08,0–1:09,0 anda de (5998,1, 6000,1) a (5999,2, 5999,0)
- 1:09,5–1:10,8 anda de (5999,1, 5998,9) a (5997,3, 5997,6)
- 1:11,3–1:12,5 anda de (5997,3, 5997,6) a (5995,5, 5998,6)
- 1:13,0–1:14,7 anda de (5995,5, 5998,6) a (5998,0, 6000,2)
- 1:15,2–1:16,2 anda de (5998,1, 6000,1) a (5999,2, 5999,0)
- 1:16,7–1:16,9 anda de (5999,1, 5998,9) a (5999,0, 5998,8)
- 1:16,8 reposo 
- 1:18,1–1:19,9 anda de (5998,9, 5998,8) a (5996,9, 6001,0)
- 1:42,2–2:02,1 anda de (5996,9, 6001,0) a (6040,0, 5990,5)

### v02 (NPC_Aldeano_02)

- 0:00,4 bucle InteractWithPeople_NoWeapon
- 0:04,1 gesto Talk02
- 0:08,3 gesto Laugh01
- 0:12,5 gesto Talk03
- 0:16,0 gesto Question01
- 0:20,4 gesto HeadShake01
- 0:21,9–0:23,5 habla
- 0:22,0 gesto HandWave02
- 0:56,5 bucle InteractWithPeople_NoWeapon
- 0:58,1 gesto Talk03
- 1:00,5 gesto Question01
- 1:04,2 gesto HeadShake01
- 1:06,6 gesto Talk01
- 1:10,0 gesto HeadNod01
- 1:13,4 gesto Talk02
- 1:16,9 reposo 
- 1:17,0 gesto SenseSomethingStart_NoWeapon
- 1:23,4 bucle Fear01
- 1:41,2–2:01,5 anda de (5996,1, 6001,9) a (6044,0, 6003,5)
- 2:10,1 bucle Beg01_Loop

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
- 0:46,6 reposo 
- 0:46,6–0:50,6 anda de (6000,3, 6004,9) a (5998,6, 6001,0)
- 0:51,4–0:52,8 habla
- 0:51,5 gesto FoundSomething_NoWeapon
- 0:55,0 bucle InteractWithPeople_NoWeapon
- 0:55,7 gesto Question01
- 0:58,0 gesto HeadShake01
- 1:00,9 gesto Talk01
- 1:03,8 gesto HeadNod01
- 1:07,5 gesto Talk02
- 1:10,0 gesto Laugh01
- 1:12,7 gesto Talk03
- 1:16,7 reposo 
- 1:23,5 gesto Fear01
- 1:40,7–2:00,8 anda de (5998,6, 6001,0) a (6046,0, 5999,0)

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
- 1:16,7 reposo 
- 1:25,7–1:33,4 anda de (6003,9, 5995,9) a (5997,8, 5995,8)
- 1:35,2–1:57,7 anda de (5997,8, 5995,8) a (6041,6, 5996,6)

### v06 (NPC_Aldeano_06)

- 0:00,0 bucle Loot01_Loop
- 0:23,5–0:29,5 habla
- 0:23,6 gesto Question01
- 0:33,0 reposo 
- 0:36,4–0:39,7 habla
- 0:36,5 gesto Reverence01
- 0:55,2 bucle Opening01_Loop
- 1:17,0 reposo 
- 1:39,8–1:52,3 anda de (6010,4, 5999,9) a (6039,0, 6001,0)

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
- 0:24,7–0:27,1 anda de (5999,0, 6001,6) a (5994,8, 6001,3)
- 0:27,7–0:29,3 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 0:29,9–0:31,1 anda de (5995,6, 5998,5) a (5997,3, 5997,6)
- 0:31,7–0:33,0 anda de (5997,4, 5997,7) a (5999,2, 5999,0)
- 0:33,6–0:35,1 anda de (5999,2, 5999,0) a (5999,0, 6001,6)
- 0:35,7–0:38,0 anda de (5998,9, 6001,6) a (5994,8, 6001,3)
- 0:38,6–0:40,2 anda de (5994,8, 6001,3) a (5995,5, 5998,6)
- 0:40,8–0:42,0 anda de (5995,5, 5998,6) a (5997,3, 5997,6)
- 0:43,4–0:44,8 anda de (5997,3, 5997,6) a (5999,2, 5999,0)
- 0:45,8–0:49,6 anda de (5999,2, 5999,1) a (5994,9, 5998,4)
- 0:57,8–0:59,8 anda de (5994,9, 5998,4) a (5998,0, 6000,2)
- 1:00,5–1:01,5 anda de (5998,0, 6000,2) a (5999,2, 5999,0)
- 1:02,2–1:03,6 anda de (5999,1, 5999,0) a (5997,3, 5997,6)
- 1:06,3–1:08,0 anda de (5997,2, 5997,7) a (5995,5, 5998,6)
- 1:08,7–1:10,3 anda de (5995,6, 5998,7) a (5998,0, 6000,2)
- 1:11,0–1:12,0 anda de (5998,0, 6000,2) a (5999,2, 5999,0)
- 1:12,7–1:14,1 anda de (5999,2, 5999,0) a (5997,3, 5997,6)
- 1:14,8–1:16,0 anda de (5997,2, 5997,7) a (5995,5, 5998,6)
- 1:16,7–1:19,5 anda de (5995,6, 5998,7) a (5999,0, 6001,6)
- 1:41,8–2:01,9 anda de (5999,0, 6001,6) a (6045,0, 6008,0)

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
- 0:47,0 reposo 
- 0:47,0–0:57,1 anda de (6011,4, 6002,4) a (6000,2, 6002,6)
- 0:53,1–0:55,0 habla
- 0:53,2 gesto Cheer02
- 0:55,5 bucle InteractWithPeople_NoWeapon
- 0:58,5 gesto HeadNod01
- 1:00,9 gesto Talk02
- 1:04,6 gesto Laugh01
- 1:08,4 gesto Talk03
- 1:11,1 gesto Question01
- 1:14,6 gesto HeadShake01
- 1:17,0 reposo 
- 1:40,9–1:58,0 anda de (6000,2, 6002,6) a (6038,0, 5993,0)
- 2:10,5 bucle Fear01

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
- 1:16,9 reposo 
- 1:23,8 gesto Fear01
- 1:40,2–1:58,8 anda de (5998,9, 6005,6) a (6041,0, 6006,0)

### v10 (NPC_Aldeano_10)

- 0:00,0 bucle Opening01_Loop
- 0:47,2 reposo 
- 1:23,7 gesto FoundSomething_NoWeapon
- 1:41,5–2:04,5 anda de (5994,0, 6002,8) a (6048,0, 5997,5)

