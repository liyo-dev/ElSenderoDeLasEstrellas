# Parte de horneado: Prologo_UltimaNoche

2026-10-22 18:21 · guion `Assets/_SEQUENCES/Guiones/Prologo_UltimaNoche.guion.txt`

**3:39,2** de secuencia · 32 frases · 56 planos · 155 efectos · 13 personajes

Reparto medido (m): archimago 1,57/ojos 0,93 · liora 1,58/ojos 0,95 · mago 1,68/ojos 0,94 · nina 1,44/ojos 0,96 · v01 1,31/ojos 0,95 · v02 1,58/ojos 0,94 · v03 1,51/ojos 0,96 · v04 1,68/ojos 0,95 · v06 1,64/ojos 0,95 · v07 1,30/ojos 0,96 · v08 1,58/ojos 0,96 · v09 1,46/ojos 0,95 · v10 1,58/ojos 0,94

## Errores (0)


## Avisos (12)

- línea 408: mago anda a ladera, entrada ritmo=normal llega_mirando=centro_plaza sin_esperar: no se llega del todo a (5988,0, 6000,5) por el NavMesh
- línea 257: plano conjunto archimago desde=cam_calle altura=1.55 lente=36 suave=0.8: algo del decorado se mete delante y tapa un 60% del cuadro. Prueba lado= o desde=.
- línea 261: plano medio archimago lado=der: no cabe el contraplano sobre el hombro de v07; hago un plano medio de archimago.
- línea 424: plano medio archimago lado=izq: algo del decorado se mete delante y tapa un 34% del cuadro. Prueba lado= o desde=.
- línea 429: plano general v06 v04 v09 v03 desde=cam_puente_este altura=2.3 lente=38 suave=0.8: la cámara de «cam_puente_este» no ve al sujeto (dentro del decorado o tapada); busco otro ángulo.
- 1:05,8: nina y v01 se atraviesan.
- 1:06,0: nina y v01 se atraviesan.
- 1:11,4: v03 y v07 se atraviesan.
- 1:12,8: nina y v01 se atraviesan.
- 1:13,0: v03 y v07 se atraviesan.
- 1:19,8: nina y v01 se atraviesan.
- 1:21,2: nina y v01 se atraviesan.

## Silencios largos entre frases (11)

- 0:29,4 → 0:37,8: 8,4 s sin voz entre PROLOGO_FAVOR_CARRETA y PROLOGO_CARRETA_GRACIAS
- 0:48,9 → 0:51,5: 2,6 s sin voz entre PROLOGO_MANANA_GLOBO_OK y PROLOGO_GLOBO_VECINO_1
- 0:59,1 → 1:03,0: 3,8 s sin voz entre PROLOGO_GLOBO_VECINO_3 y PROLOGO_PLAZA_LIORA_1
- 1:18,7 → 1:36,6: 17,9 s sin voz entre PROLOGO_PLAZA_LIORA_2 y PROLOGO_DESPEDIDA_LIORA_1
- 1:41,9 → 1:44,5: 2,6 s sin voz entre PROLOGO_DESPEDIDA_ARCHIMAGO_1 y PROLOGO_HORA_ARCHIMAGO
- 2:00,1 → 2:02,0: 1,8 s sin voz entre PROLOGO_DESPEDIDA_ELEGIR y PROLOGO_EVACUACION_LIORA
- 2:05,5 → 2:17,0: 11,5 s sin voz entre PROLOGO_EVACUACION_LIORA y PROLOGO_DUELO_MAGO
- 2:26,4 → 2:38,7: 12,3 s sin voz entre PROLOGO_DUELO_ARCHIMAGO y PROLOGO_MAGO_VALLE
- 2:42,0 → 2:45,4: 3,4 s sin voz entre PROLOGO_MAGO_VALLE y PROLOGO_PLEGARIA_01
- 2:52,7 → 2:55,5: 2,8 s sin voz entre PROLOGO_PLEGARIA_01 y PROLOGO_PLEGARIA_02
- 3:29,8 → 3:31,4: 1,7 s sin voz entre PROLOGO_PLEGARIA_06 y PROLOGO_HECHIZO

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | plano 1 | general de el punto · 4,5 s · lente 46° |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
| 0:00,0 | efecto | Apagar: PROP_Nube_Este |
| 0:00,0 | efecto | Apagar: PROP_Nube_Oeste |
| 0:00,0 | efecto | Decorado: mover PROP_Carreta (al instante) |
| 0:00,0 | efecto | Nubes que se abren (5,5s) |
| 0:00,0 | efecto | Post-procesado: Prologo_Dia (0s) |
| 0:00,0 | efecto | Ambiente: AMBIENTE ← Ambience_Prologo_Pueblo |
| 0:00,0 | efecto | Voz: ExteriorDia, +0,0 dB |
| 0:00,0 | efecto | Hora del día: Morning (de golpe) |
| 0:04,5 | **@entra** |  |
| 0:04,5 | plano 2 | conjunto de archimago · 5,5 s · lente 36° |
| 0:10,0 | plano 3 | conjunto de v01 y v07 y v02 · 6,1 s · lente 34° · coste 88: tapado 61, cara 11, ángulo 9 |
| 0:16,1 | **@saludo** |  |
| 0:16,1 | plano 4 | medio de archimago · 3,7 s · lente 24° · coste 560: delante 445, cara 100, ángulo 15 |
| 0:16,1 | archimago | «Buenos días, pueblo. ¿Qué se te ha estropeado hoy?» (3,7 s) · pose |
| 0:19,8 | plano 5 | medio de v02 · 4,0 s · lente 30° |
| 0:19,8 | v01 | «¡Buenos días, maestro!» (1,8 s) |
| 0:21,9 | v02 | «¡Muy buenas, maestro!» (1,8 s) · pose |
| 0:23,8 | **@saludo_fin** |  |
| 0:23,8 | plano 6 | medio de v06 · 5,7 s · lente 30° · coste 2: ángulo 2 |
| 0:23,8 | v06 | «¡Maestro! Se me ha volcado otra vez la carreta. Y eso que hoy iba despacio.» (5,7 s) |
| 0:29,4 | plano 7 | medio de archimago · 5,0 s · lente 30° · coste 21: ángulo 21 |
| 0:29,4 | efecto | Salto: NPC_Aldeano_06 1,2 m y 0,5 m al frente |
| 0:34,4 | **@carreta_magia** |  |
| 0:34,4 | plano 8 | medio de archimago · 3,4 s · lente 30° |
| 0:34,7 | efecto | SFX: Prologo_MagiaLevitar |
| 0:34,7 | efecto | Conjuro en las manos: NPC_Archimago (2,6 s) |
| 0:34,8 | efecto | VFX: VFX_RemolinoDelPrologo en carreta |
| 0:35,0 | efecto | Decorado: mover PROP_Carreta en 1,4s |
| 0:37,1 | efecto | Decorado: mover PROP_Carreta en 0,9s |
| 0:37,8 | plano 9 | dos: archimago y v06 · 4,2 s · lente 32° · coste 514: cara 137, ángulo 21, eje 350 |
| 0:37,8 | v06 | «¡Gracias, maestro! No habría podido enderezarla yo solo.» (4,2 s) |
| 0:37,9 | efecto | SFX: Prologo_CarretaCae |
| 0:37,9 | efecto | VFX: 2 Flash_round_ellow en PROP_Carreta |
| 0:41,9 | plano 10 | medio de nina · 4,3 s · lente 30° · coste 831: cara 787, ángulo 38 |
| 0:41,9 | nina | «¡Maestro! El globo se ha enganchado en lo alto del campanario.» (4,3 s) |
| 0:46,2 | plano 11 | medio de archimago · 2,6 s · lente 30° · coste 9: ángulo 3 |
| 0:46,2 | archimago | «Con cuidado... Eso es.» (2,6 s) |
| 0:46,2 | efecto | Hora del día: AfterNoon |
| 0:46,6 | efecto | Conjuro en las manos: NPC_Archimago (2,2 s) |
| 0:46,6 | efecto | SFX: Prologo_MagiaLevitar |
| 0:47,3 | efecto | VFX: 2 Flash_round_ellow en PROP_Globo |
| 0:48,9 | **@globo_sube** |  |
| 0:48,9 | plano 12 | general de el punto · 2,6 s · lente 48° |
| 0:48,9 | efecto | Decorado: mover PROP_Globo en 8,0s |
| 0:51,5 | plano 13 | conjunto de nina y v03 y v08 · 7,7 s · lente 34° |
| 0:51,5 | nina | «¡Hala, ya vuela!» (2,6 s) · pose |
| 0:54,4 | v03 | «¡Mira cómo sube!» (2,1 s) · pose |
| 0:56,8 | v08 | «¡Va a llegar hasta las nubes!» (2,3 s) · pose |
| 0:59,1 | **@globo_fin** |  |
| 0:59,1 | plano 14 | medio de archimago · 2,2 s · lente 30° · coste 2: ángulo 2 |
| 1:01,1 | efecto | Apagar: PROP_Globo |
| 1:01,4 | **@rio** |  |
| 1:01,4 | plano 15 | siguiendo a liora · 6,6 s · lente 34° · mezcla 1,2 s |
| 1:01,4 | efecto | Sol: en cuadro, poniéndose en 19 s |
| 1:01,4 | efecto | Ambiente: parar AMBIENTE |
| 1:01,4 | efecto | Ambiente: AMBIENTE_RIO ← Ambience_Prologo_Rio |
| 1:01,4 | efecto | Hora del día: Sunset |
| 1:01,4 | efecto | Post-procesado: Prologo_Atardecer (0s) |
| 1:02,4 | **@rio_paseo** |  |
| 1:03,0 | liora | «¿Has pensado qué contestar a la academia? Llevas semanas con la carta.» (4,4 s) |
| 1:08,0 | plano 16 | sobre el hombro de liora a archimago · 5,7 s · lente 24° · coste 656: estorbo 300, eje 350 |
| 1:08,0 | archimago | «Hoy pensaba contestar. Pero siempre acaba surgiendo algo en el valle.» (5,7 s) |
| 1:13,7 | plano 17 | siguiendo a liora · 5,0 s · lente 34° |
| 1:13,7 | liora | «No tienes que quedarte solo porque aquí te necesiten. ¿Qué quieres hacer tú?» (5,0 s) · pose |
| 1:18,7 | plano 18 | dos: archimago y liora · 2,2 s · lente 34° · coste 580: estorbo 300, cara 265, ángulo 9 |
| 1:20,9 | **@cielo** |  |
| 1:20,9 | plano 19 | medio de archimago · 1,6 s · lente 30° · coste 423: cara 62, ángulo 11, eje 350 |
| 1:20,9 | efecto | Sol: devolverlo al ciclo |
| 1:20,9 | efecto | Hora del día: Night |
| 1:20,9 | efecto | Tiempo: encender Tormenta |
| 1:20,9 | efecto | Ambiente: parar AMBIENTE_RIO |
| 1:20,9 | efecto | SFX: SFX_Prologo_Viento |
| 1:20,9 | efecto | Post-procesado: Prologo_Amenaza (6s) |
| 1:20,9 | efecto | Música: parar (3s) |
| 1:20,9 | efecto | Tiempo: encender Viento |
| 1:22,5 | plano 20 | general de el punto · 2,8 s · lente 40° |
| 1:22,5 | efecto | Rayo: 2 × fondo del plano |
| 1:25,3 | plano 21 | conjunto de v02 y v10 y v01 y nina · 2,0 s · lente 34° · coste 14: ángulo 14 |
| 1:27,3 | **@llegada** |  |
| 1:27,3 | plano 22 | conjunto de mago · 2,6 s · lente 30° · coste 0: limpio |
| 1:27,3 | efecto | Sacudida de cámara (0,55, 0,6s) |
| 1:27,3 | efecto | SFX: Prologo_Golpe |
| 1:27,3 | efecto | Fogonazo (0,18s) |
| 1:27,3 | efecto | Rayo: 1 × fondo del plano |
| 1:27,3 | efecto | Voz: ExteriorNoche, +0,0 dB |
| 1:27,6 | efecto | SFX: Prologo_PresenciaOscura |
| 1:27,7 | efecto | Música: MAGOOSCURO_REVEAL |
| 1:27,9 | efecto | Ambiente: TORMENTA ← Ambience_Prologo_Tormenta |
| 1:27,9 | efecto | Tiempo: encender Lluvia |
| 1:29,9 | plano 23 | primer plano de mago · 2,2 s · lente 24° · coste 2: ángulo 2 |
| 1:32,1 | plano 24 | medio de archimago · 1,0 s · lente 30° · coste 73: cara 62, ángulo 11 |
| 1:33,1 | plano 25 | siguiendo a mago · 3,5 s · lente 34° |
| 1:36,6 | **@evacuar** |  |
| 1:36,6 | plano 26 | sobre el hombro de archimago a liora · 2,2 s · lente 24° · coste 95: cara 95 |
| 1:36,6 | liora | «¿Quién es? ¿Qué quiere?» (2,2 s) · pose |
| 1:36,6 | efecto | Ambiente: PANICO ← Ambience_Prologo_Panico |
| 1:36,6 | efecto | Encender: PROP_Incendio |
| 1:37,2 | efecto | Rayo: 1 × fondo del plano |
| 1:38,8 | plano 27 | sobre el hombro de liora a archimago · 3,1 s · lente 24° · coste 95: cara 95 |
| 1:38,8 | archimago | «No lo sé. Llévatelos al puente, deprisa.» (3,1 s) · pose |
| 1:41,9 | plano 28 | medio de archimago · 5,2 s · lente 30° · coste 411: pegado 100, delante 303, ángulo 2 |
| 1:44,5 | **@huida** |  |
| 1:44,5 | archimago | «¡Al puente! ¡Todos, ahora!» (2,6 s) · pose |
| 1:47,1 | plano 29 | general de v06 y v04 y v09 y v03 · 1,6 s · lente 38° · coste 736: tapado 440, delante 242, cara 18, ángulo 36 |
| 1:47,5 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 1:48,7 | plano 30 | dos: archimago y liora · 3,9 s · lente 32° · coste 904: tapado 122, delante 508, cara 254, ángulo 14 |
| 1:48,7 | liora | «¿Y tú?» (1,3 s) |
| 1:50,3 | archimago | «Ve. Yo me encargo de él.» (2,3 s) · pose |
| 1:52,6 | plano 31 | medio de liora · 7,5 s · lente 30° |
| 1:52,6 | liora | «Vuelve pronto.» (1,9 s) · pose |
| 1:54,7 | liora | «Y cuando vuelvas, nada de encargos. Te toca descansar.» (5,4 s) · pose |
| 2:00,1 | plano 32 | sobre el hombro de liora a archimago · 1,8 s · lente 24° · coste 755: estorbo 399, eje 350 |
| 2:02,0 | plano 33 | conjunto de liora y nina · 3,6 s · lente 34° · coste 526: estorbo 333, cara 164, ángulo 23 |
| 2:02,0 | liora | «¡Vamos, Nina! ...Ten mucho cuidado, por favor.» (3,6 s) · pose |
| 2:05,5 | **@acercan** |  |
| 2:05,5 | plano 34 | siguiendo a archimago · 4,5 s · lente 34° |
| 2:06,7 | efecto | Rayo: 1 × fondo del plano |
| 2:07,5 | efecto | Ambiente: parar PANICO |
| 2:10,0 | plano 35 | siguiendo a mago · 7,0 s · lente 34° |
| 2:10,0 | efecto | Música: MAGOOSCURO_CLIMAX |
| 2:17,0 | **@duelo** |  |
| 2:17,0 | plano 36 | sobre el hombro de archimago a mago · 3,0 s · lente 24° · coste 0: limpio |
| 2:17,0 | mago | «¿Vas a salvarlos a todos tú solo?» (3,0 s) · pose |
| 2:17,0 | efecto | Mezcla: primeraVozOscura |
| 2:17,2 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 2:20,0 | plano 37 | primer plano de mago · 3,1 s · lente 24° · coste 35: ángulo 35 |
| 2:20,0 | mago | «Ni a ti mismo puedes salvarte.» (3,1 s) · pose |
| 2:23,1 | plano 38 | medio de archimago · 3,3 s · lente 30° · coste 10: ángulo 2 |
| 2:23,1 | archimago | «Mientras siga en pie, no llegarás a ellos.» (3,3 s) · pose |
| 2:23,1 | efecto | Mezcla: primeraVozOscura (quitar) |
| 2:26,4 | plano 39 | dos: archimago y mago · 2,5 s · lente 36° · coste 239: cara 125, ángulo 108 |
| 2:26,6 | efecto | Hechizo: NPC_MagoOscuro → NPC_Archimago |
| 2:26,7 | efecto | VFX: Magic shield blue en NPC_Archimago |
| 2:27,2 | efecto | Sacudida de cámara (0,35, 0,4s) |
| 2:27,2 | efecto | SFX: Prologo_EscudoBloquea |
| 2:27,2 | efecto | Fogonazo (0,12s) |
| 2:28,9 | plano 40 | sobre el hombro de mago a archimago · 6,7 s · lente 24° · coste 0: limpio |
| 2:29,1 | efecto | Hechizo: NPC_Archimago → NPC_MagoOscuro |
| 2:31,8 | **@choque** |  |
| 2:31,9 | efecto | SFX: Prologo_Carga |
| 2:31,9 | efecto | VFX: Magic circle en NPC_Archimago |
| 2:31,9 | efecto | VFX: Magic circle en NPC_MagoOscuro |
| 2:32,5 | efecto | SFX: SFX_Prologo_Latigazo |
| 2:32,5 | efecto | VFX: Charge slash blue en NPC_Archimago |
| 2:32,5 | efecto | VFX: Charge slash purple en NPC_MagoOscuro |
| 2:32,8 | efecto | Sacudida de cámara (0,75, 1s) |
| 2:32,8 | efecto | SFX: Prologo_Choque |
| 2:32,8 | efecto | Líneas de concentración: mostrar |
| 2:32,8 | efecto | VFX: Red energy explosion en choque |
| 2:32,8 | efecto | VFX: FX_LightPillar en choque |
| 2:32,8 | efecto | Fogonazo (0,28s) |
| 2:32,9 | efecto | SFX: Prologue_WarClashStinger_A |
| 2:35,6 | plano 41 | medio de mago · 1,1 s · lente 30° · coste 2: ángulo 2 |
| 2:35,8 | efecto | SFX: Prologo_SueloSeAbre |
| 2:35,9 | efecto | VFX: Charge slash purple en NPC_MagoOscuro |
| 2:36,7 | plano 42 | medio de archimago · 2,0 s · lente 30° · coste 2: ángulo 2 |
| 2:37,0 | efecto | VFX: MagoOscuroGrieta en NPC_Archimago |
| 2:37,2 | efecto | VFX: Ground AOE explosion en NPC_Archimago |
| 2:37,2 | efecto | Sacudida de cámara (0,7, 0,9s) |
| 2:37,3 | efecto | VFX: Dust ground en NPC_Archimago |
| 2:38,7 | **@ultimo** |  |
| 2:38,7 | plano 43 | primer plano de mago · 3,3 s · lente 24° · coste 2: ángulo 2 |
| 2:38,7 | mago | «Que este valle no haya existido nunca.» (3,3 s) · pose |
| 2:42,0 | plano 44 | general de mago · 3,4 s · lente 38° |
| 2:42,0 | efecto | Post-procesado: Prologo_Climax (2,5s) |
| 2:42,1 | efecto | SFX: Prologo_Despegue |
| 2:42,1 | efecto | VFX: Dust ground en NPC_MagoOscuro |
| 2:42,3 | efecto | Rayo: 2 × fondo del plano |
| 2:42,6 | efecto | SFX: Prologo_HechizoGrande |
| 2:42,8 | efecto | VFX: VFX_AgujeroNegro en agujero |
| 2:42,8 | efecto | SFX: SFX_Prologo_AgujeroNegro_Carga |
| 2:42,8 | efecto | Ambiente: AGUJERO_NEGRO ← SFX_Prologo_AgujeroNegro_Bucle |
| 2:42,9 | efecto | Escalar: CARGA_ESFERA ×0,35 en 0 s |
| 2:42,9 | efecto | Escalar: CARGA_ESFERA ×1 en 3,5 s |
| 2:43,0 | efecto | VFX: Magic circle en agujero |
| 2:43,0 | efecto | Sacudida de cámara (0,35, 1,6s) |
| 2:45,4 | plano 45 | medio de archimago · 8,7 s · lente 30° · coste 17: ángulo 17 |
| 2:45,4 | archimago | «No daré ni un paso atrás... ¡No dejaré que los toques! ¡Liora! ¡Nina! ¡A ninguno de ellos!» (7,3 s) |
| 2:45,4 | efecto | Voz: Ninguno, +0,0 dB |
| 2:45,4 | efecto | Mezcla: plegaria |
| 2:45,6 | efecto | SFX: Prologo_MagiaLevitar |
| 2:45,6 | efecto | VFX: Healing circle en NPC_Archimago |
| 2:48,7 | efecto | Cut-in 0: NPC_Liora (Cara) |
| 2:50,4 | efecto | Cut-in 0: NPC_Aldeano_05 (Cara) |
| 2:52,7 | efecto | Pantalla: cubrir (1,2s) |
| 2:54,1 | plano 46 | general de mago · 1,4 s · lente 36° |
| 2:54,1 | efecto | VFX: Magic circle en agujero |
| 2:54,1 | efecto | Sacudida de cámara (0,25, 1s) |
| 2:54,1 | efecto | Música: parar (0,8s) |
| 2:54,1 | efecto | Escalar: CARGA_ESFERA ×1,5 en 2 s |
| 2:54,1 | efecto | Voz: Conjuro, +6,0 dB |
| 2:54,1 | efecto | Pantalla: descubrir (1,2s) |
| 2:54,1 | efecto | Mezcla: plegaria |
| 2:55,5 | plano 47 | primer plano de archimago · 8,2 s · lente 24° · coste 2: ángulo 2 |
| 2:55,5 | archimago | «Más allá de la noche y del silencio... ¡Luz que naciste antes que el cielo y tendiste el camino entre las estrellas!» (8,2 s) |
| 2:55,5 | efecto | Música: ULTIMO_INCANTAZIONE |
| 2:55,9 | efecto | VFX: Magic circle en NPC_Archimago |
| 2:56,3 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 2:59,7 | efecto | Conjuro en las manos: NPC_Archimago (8,0 s) |
| 3:03,7 | plano 48 | medio de archimago · 8,6 s · lente 30° · coste 17: ángulo 17 |
| 3:03,7 | archimago | «Ningún poder de este mundo puede detenerlo... ¡Por eso llamo a lo que aún no existe! ¡Escucha mi voz y despierta en mis manos!» (8,6 s) |
| 3:06,9 | efecto | Rayo: 1 × fondo del plano |
| 3:07,2 | efecto | SFX: Prologo_Carga |
| 3:07,2 | efecto | VFX: Charge slash blue en NPC_Archimago |
| 3:09,3 | efecto | Escalar: CARGA_ESFERA ×2 en 2 s |
| 3:09,3 | efecto | Sacudida de cámara (0,35, 1,2s) |
| 3:12,3 | plano 49 | general de mago · 1,2 s · lente 34° |
| 3:12,3 | efecto | Rayo: 1 × fondo del plano |
| 3:13,5 | plano 50 | primer plano de archimago · 6,3 s · lente 24° · coste 17: ángulo 17 |
| 3:13,5 | archimago | «Que mi vida sea el precio y mi alma el sello... ¡No importa lo que quede de mí cuando la luz se apague!» (6,3 s) |
| 3:17,0 | efecto | VFX: FX_LightPillar en NPC_Archimago |
| 3:17,0 | efecto | SFX: Prologo_MagiaLevitar |
| 3:17,0 | efecto | Sacudida de cámara (0,3, 0,9s) |
| 3:19,8 | plano 51 | medio de archimago · 6,1 s · lente 30° · coste 2: ángulo 2 |
| 3:19,8 | archimago | «¡Haz de mi último aliento un escudo inquebrantable! ¡Arde conmigo y detén la oscuridad!» (6,1 s) |
| 3:19,8 | efecto | Líneas de concentración: mostrar |
| 3:20,1 | efecto | Conjuro en las manos: NPC_Archimago (6,0 s) |
| 3:23,2 | efecto | Rayo: 2 × fondo del plano |
| 3:23,3 | efecto | SFX: Prologo_Carga |
| 3:23,3 | efecto | Sacudida de cámara (0,5, 1,6s) |
| 3:23,3 | efecto | Escalar: CARGA_ESFERA ×2,6 en 1,4 s |
| 3:26,0 | plano 52 | primer plano de archimago · 5,5 s · lente 24° · coste 2: ángulo 2 |
| 3:26,0 | archimago | «Perdóname, Liora... No voy a volver a casa.» (3,8 s) |
| 3:26,0 | efecto | Líneas de concentración: retirar |
| 3:30,3 | efecto | Mezcla: plegaria (quitar) |
| 3:30,3 | efecto | Mezcla: grito |
| 3:30,3 | efecto | Líneas de concentración: mostrar |
| 3:30,3 | efecto | Cut-in 1: NPC_Archimago (Cara) |
| 3:31,4 | **@hechizo** |  |
| 3:31,4 | plano 53 | general de archimago y mago · 2,0 s · lente 44° |
| 3:31,4 | archimago | «¡¡Protégelos a todos!!» (2,1 s) |
| 3:31,4 | efecto | Respiro: 0,9 s (música -10 dB) |
| 3:31,4 | efecto | Voz: Conjuro, +6,0 dB |
| 3:31,6 | efecto | Rayo: 2 × fondo del plano |
| 3:31,9 | efecto | Fusión: CARGA_ESFERA +  |
| 3:31,9 | efecto | Sacudida de cámara (0,45, 2s) |
| 3:31,9 | efecto | SFX: SFX_Proteccion_Absoluta |
| 3:33,4 | plano 54 | primer plano de archimago · 1,3 s · lente 24° · coste 21: ángulo 21 |
| 3:33,4 | efecto | Sacudida de cámara (0,55, 1,3s) |
| 3:33,4 | efecto | Líneas de concentración: mostrar |
| 3:34,7 | plano 55 | primer plano de mago · 1,2 s · lente 24° · coste 115: cara 63, ángulo 53 |
| 3:34,7 | efecto | Sacudida de cámara (0,7, 1,2s) |
| 3:34,8 | efecto | Música: parar (0,8s) |
| 3:35,7 | **@explosion** |  |
| 3:35,7 | efecto | Golpe musical: Prologue_GolpeFinalCola |
| 3:35,7 | efecto | Ambiente: parar TORMENTA |
| 3:35,7 | efecto | Pantalla: cubrir (1,2s) |
| 3:35,7 | efecto | Líneas de concentración: retirar |
| 3:35,7 | efecto | VFX: recoger efectos registrados |
| 3:35,7 | efecto | Ambiente: parar TORMENTA |
| 3:35,7 | efecto | Ambiente: parar AGUJERO_NEGRO |
| 3:35,7 | efecto | Mezcla: grito (quitar) |
| 3:35,9 | plano 56 | general de archimago y mago · 3,3 s · lente 48° |
| 3:35,9 | efecto | Sacudida de cámara (0,95, 1,4s) |
| 3:36,1 | efecto | Sacudida de cámara (0,9, 1,6s) |
| 3:36,7 | efecto | SFX: SFX_Prologo_SilencioBlanco |
| 3:36,9 | efecto | Hora del día: Morning (de golpe) (y es con la que se queda el mundo) |
| 3:37,0 | efecto | Fogonazo (0,14s) |
| 3:38,7 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### archimago (NPC_Archimago)

- 0:04,6–0:16,2 anda de (6002,6, 6012,3) a (6003,2, 6002,3)
- 0:16,1–0:19,8 habla
- 0:16,2 gesto HandWave02
- 0:29,5–0:32,9 anda de (6003,3, 6002,3) a (6006,4, 6000,6)
- 0:34,4 gesto MagicRight
- 0:37,8 gesto HeadNod01
- 0:46,2 bucle CastingIdle01
- 0:46,2–0:48,9 habla
- 0:48,9 reposo 
- 0:52,1 gesto Laugh01
- 0:59,1 gesto HeadNod01
- 1:02,4–1:08,0 anda de (6016,9, 6002,7) a (6017,4, 5998,3)
- 1:08,0–1:13,7 habla
- 1:08,0 gesto Talk02
- 1:38,8–1:41,9 habla
- 1:38,9 gesto HeadShake01
- 1:40,6 gesto Talk01
- 1:42,0–1:44,5 anda de (6017,4, 5998,3) a (6014,4, 6000,3)
- 1:44,5–1:47,1 habla
- 1:44,6 gesto Challenging_NoWeapon
- 1:50,3–1:52,6 habla
- 1:50,4 gesto HeadNod01
- 2:00,1 gesto HeadNod01
- 2:06,0–2:15,0 anda de (6014,4, 6000,3) a (6004,4, 5999,8)
- 2:23,1–2:26,4 habla
- 2:23,1 gesto Angry02
- 2:24,7 gesto Talk03
- 2:26,7 gesto Defend_NoWeapon
- 2:28,9 gesto MagicLeft
- 2:31,8 gesto MagicSpecial
- 2:37,3 bucle Pain01
- 2:45,4 reposo 
- 2:45,4 bucle CastingIdle01
- 2:45,4–2:52,7 habla
- 2:55,5 bucle CastingIdle01
- 2:55,5–3:03,7 habla
- 3:03,7–3:12,3 habla
- 3:07,0 gesto Angry01
- 3:08,5 bucle CastingIdle01
- 3:13,5–3:19,8 habla
- 3:16,9 gesto Angry02
- 3:18,5 bucle CastingIdle01
- 3:19,8–3:26,0 habla
- 3:26,0 gesto ApretonDeManos_A
- 3:26,0–3:29,8 habla
- 3:31,4 bucle CastingIdle01
- 3:31,4–3:33,5 habla

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
- 1:01,4 reposo 
- 1:02,4–1:08,0 anda de (6015,8, 6002,3) a (6016,1, 5997,9)
- 1:03,0–1:07,3 habla
- 1:13,7–1:18,7 habla
- 1:13,8 gesto Talk01
- 1:16,2 gesto Talk03
- 1:36,6–1:38,8 habla
- 1:36,7 gesto Question01
- 1:48,7–1:50,0 habla
- 1:48,8–1:49,6 anda de (6016,0, 5997,9) a (6015,0, 5997,8)
- 1:52,6–1:54,5 habla
- 1:52,7 gesto Talk01
- 1:54,7–2:00,1 habla
- 1:54,8 gesto Talk03
- 1:57,9 gesto Talk01
- 2:02,0–2:05,5 habla
- 2:02,1 gesto Talk01
- 2:05,6–2:17,5 anda de (6015,0, 5997,9) a (6042,0, 6000,0)

### mago (NPC_MagoOscuro)

- 1:27,3 bucle Challenging_NoWeapon
- 1:27,3 aparece
- 1:33,1 reposo 
- 1:33,2–2:00,2 anda de (5957,8, 5996,9) a (5988,0, 6000,5)
- 2:05,6–2:17,1 anda de (5988,0, 6000,5) a (6000,8, 5999,8)
- 2:17,0–2:20,0 habla
- 2:17,1 gesto Talk02
- 2:20,0–2:23,1 habla
- 2:20,0 gesto Talk03
- 2:26,4 gesto MagicRight
- 2:29,8 gesto TakeDamage
- 2:31,8 gesto MagicSpecial
- 2:35,6 gesto MagicRight
- 2:38,7–2:42,0 habla
- 2:38,8 gesto Talk03
- 2:42,0 bucle MagicAttackOmni01_Load

### nina (NPC_Aldeano_05)

- 0:00,0 bucle SenseSomethingSearching_NoWeapon
- 0:34,8 reposo 
- 0:34,9–0:42,2 anda de (5990,9, 6001,0) a (6004,9, 5998,8)
- 0:41,9–0:46,2 habla
- 0:42,0 gesto Beg01
- 0:51,5–0:54,1 habla
- 0:51,5 gesto Cheer01
- 0:59,2–1:04,3 anda de (6004,8, 5998,8) a (5995,2, 6001,0)
- 1:04,1 bucle InteractWithPeople_NoWeapon
- 1:07,1 gesto Talk01
- 1:10,3 gesto HeadNod01
- 1:13,6 gesto Talk02
- 1:16,4 gesto Laugh01
- 1:19,3 gesto Talk03
- 1:21,2 reposo 
- 1:45,1–1:53,2 anda de (5995,2, 6001,0) a (6010,5, 6000,0)
- 2:05,6–2:17,2 anda de (6010,6, 6000,0) a (6026,6, 6007,5)

### v01 (NPC_Aldeano_01)

- 0:00,6–0:01,5 anda de (5995,5, 5998,6) a (5997,0, 5998,4)
- 0:01,9–0:03,4 anda de (5997,0, 5998,4) a (5996,6, 6001,0)
- 0:03,8–0:04,9 anda de (5996,6, 6001,0) a (5994,8, 6001,3)
- 0:05,3–0:06,9 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 0:07,3–0:08,1 anda de (5995,6, 5998,6) a (5997,0, 5998,4)
- 0:08,5–0:10,0 anda de (5997,0, 5998,4) a (5996,6, 6001,0)
- 0:10,4–0:11,5 anda de (5996,6, 6001,0) a (5994,8, 6001,3)
- 0:11,9–0:13,5 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 0:13,9–0:14,7 anda de (5995,6, 5998,6) a (5997,0, 5998,4)
- 0:15,1–0:16,6 anda de (5997,0, 5998,4) a (5996,6, 6001,0)
- 0:17,0–0:18,1 anda de (5996,6, 6001,0) a (5994,8, 6001,3)
- 0:18,5–0:20,1 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 0:19,8–0:21,6 habla
- 0:19,9 gesto HandWave01
- 0:20,5–0:21,3 anda de (5995,6, 5998,6) a (5997,0, 5998,4)
- 0:21,7–0:23,2 anda de (5997,0, 5998,4) a (5996,6, 6001,0)
- 0:23,6–0:26,8 anda de (5996,6, 6001,0) a (5997,3, 5997,6)
- 0:27,6–0:28,8 anda de (5997,3, 5997,6) a (5995,5, 5998,6)
- 0:29,6–0:31,2 anda de (5995,5, 5998,6) a (5994,8, 6001,3)
- 0:32,0–0:33,6 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 0:34,4–0:35,6 anda de (5995,6, 5998,6) a (5997,3, 5997,6)
- 0:36,4–0:37,6 anda de (5997,2, 5997,6) a (5995,5, 5998,6)
- 0:38,4–0:39,9 anda de (5995,5, 5998,7) a (5994,8, 6001,3)
- 0:40,7–0:42,3 anda de (5994,8, 6001,3) a (5995,5, 5998,6)
- 0:43,1–0:44,3 anda de (5995,5, 5998,6) a (5997,3, 5997,6)
- 0:45,1–0:46,3 anda de (5997,3, 5997,6) a (5995,5, 5998,6)
- 0:47,1–0:48,7 anda de (5995,5, 5998,6) a (5994,8, 6001,3)
- 0:48,9–0:50,3 anda de (5994,8, 6001,2) a (5993,4, 5999,2)
- 1:00,7–1:02,8 anda de (5993,5, 5999,2) a (5997,0, 5998,4)
- 1:03,3–1:04,7 anda de (5997,0, 5998,5) a (5996,6, 6001,0)
- 1:05,2–1:06,3 anda de (5996,6, 6001,0) a (5994,8, 6001,3)
- 1:06,8–1:08,4 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 1:08,9–1:09,8 anda de (5995,6, 5998,6) a (5997,0, 5998,4)
- 1:10,3–1:11,7 anda de (5997,0, 5998,5) a (5996,6, 6001,0)
- 1:12,2–1:13,3 anda de (5996,6, 6001,0) a (5994,8, 6001,3)
- 1:13,8–1:15,4 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 1:15,9–1:16,8 anda de (5995,6, 5998,6) a (5997,0, 5998,4)
- 1:17,3–1:18,7 anda de (5997,0, 5998,5) a (5996,6, 6001,0)
- 1:19,2–1:20,3 anda de (5996,6, 6001,0) a (5994,8, 6001,3)
- 1:20,8–1:22,4 anda de (5994,8, 6001,3) a (5996,9, 6001,0)
- 1:21,2 reposo 
- 1:46,9–2:06,8 anda de (5996,9, 6001,0) a (6040,0, 5990,5)

### v02 (NPC_Aldeano_02)

- 0:00,4 bucle InteractWithPeople_NoWeapon
- 0:04,1 gesto Talk02
- 0:08,3 gesto Laugh01
- 0:12,5 gesto Talk03
- 0:16,1 reposo 
- 0:16,2–0:20,5 anda de (5996,2, 6001,9) a (6004,3, 6000,6)
- 0:21,9–0:23,8 habla
- 0:22,0 gesto HandWave02
- 0:24,6–0:34,9 anda de (6004,3, 6000,6) a (5996,1, 6001,9)
- 1:00,6 bucle InteractWithPeople_NoWeapon
- 1:03,9 gesto Question01
- 1:06,7 gesto HeadShake01
- 1:10,1 gesto Talk01
- 1:12,5 gesto HeadNod01
- 1:16,3 gesto Talk02
- 1:18,6 gesto Laugh01
- 1:21,3 reposo 
- 1:21,4 gesto SenseSomethingStart_NoWeapon
- 1:27,8 bucle Fear01
- 1:45,9–2:06,2 anda de (5996,1, 6001,9) a (6044,0, 6003,5)
- 2:17,5 bucle Beg01_Loop

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
- 0:46,4 reposo 
- 0:47,2–0:51,1 anda de (6000,3, 6004,9) a (6001,3, 6000,9)
- 0:54,4–0:56,5 habla
- 0:54,4 gesto FoundSomething_NoWeapon
- 0:59,1 bucle InteractWithPeople_NoWeapon
- 0:59,8 gesto Question01
- 1:02,1 gesto HeadShake01
- 1:05,0 gesto Talk01
- 1:07,9 gesto HeadNod01
- 1:11,6 gesto Talk02
- 1:14,1 gesto Laugh01
- 1:16,9 gesto Talk03
- 1:21,1 reposo 
- 1:27,9 gesto Fear01
- 1:45,4–2:04,4 anda de (6001,3, 6000,9) a (6046,0, 5999,0)

### v04 (NPC_Aldeano_04)

- 0:00,0 bucle Idle_Normal_NoWeapon
- 0:00,8 reposo 
- 0:00,8–0:08,5 anda de (5997,8, 5995,8) a (6003,9, 5995,9)
- 0:10,3–0:17,9 anda de (6003,9, 5995,9) a (5997,8, 5995,8)
- 0:19,7–0:27,3 anda de (5997,8, 5995,8) a (6003,9, 5995,9)
- 0:29,1–0:36,8 anda de (6003,9, 5995,9) a (5997,8, 5995,8)
- 0:38,6–0:46,2 anda de (5997,8, 5995,8) a (6003,9, 5995,9)
- 0:48,0–0:55,6 anda de (6003,9, 5995,9) a (5997,8, 5995,8)
- 0:57,4–1:05,1 anda de (5997,8, 5995,8) a (6003,9, 5995,9)
- 1:06,9–1:14,5 anda de (6003,9, 5995,9) a (5997,8, 5995,8)
- 1:16,3–1:23,9 anda de (5997,8, 5995,8) a (6003,9, 5995,9)
- 1:21,1 reposo 
- 1:25,7–1:33,4 anda de (6003,9, 5995,9) a (5997,8, 5995,8)
- 1:35,2–1:42,8 anda de (5997,8, 5995,8) a (6003,9, 5995,9)
- 1:44,6–2:01,7 anda de (6003,9, 5995,9) a (6041,6, 5996,6)

### v06 (NPC_Aldeano_06)

- 0:00,0 bucle Idle_Normal_NoWeapon
- 0:23,8–0:29,4 habla
- 0:23,8 gesto Question01
- 0:34,6 bucle Idle_Normal_NoWeapon
- 0:37,8–0:41,9 habla
- 0:37,9 gesto HeadNod01
- 0:59,1 reposo 
- 0:59,3 bucle Opening01_Loop
- 1:21,4 reposo 
- 1:44,5–1:57,1 anda de (6010,0, 6000,4) a (6039,0, 6001,0)

### v07 (NPC_Aldeano_07)

- 0:01,6–0:06,2 anda de (5998,0, 6000,2) a (6001,5, 6000,5)
- 0:07,0–0:11,5 anda de (6001,5, 6000,5) a (5998,0, 6000,2)
- 0:12,3–0:16,9 anda de (5998,0, 6000,2) a (6001,5, 6000,5)
- 0:17,7–0:22,2 anda de (6001,5, 6000,5) a (5998,0, 6000,2)
- 0:23,0–0:27,8 anda de (5998,0, 6000,2) a (6001,5, 6000,5)
- 0:28,6–0:33,1 anda de (6001,5, 6000,5) a (5998,0, 6000,2)
- 0:33,9–0:38,4 anda de (5998,0, 6000,2) a (6001,5, 6000,5)
- 0:39,2–0:43,8 anda de (6001,5, 6000,5) a (5998,0, 6000,2)
- 0:44,6–0:49,1 anda de (5998,0, 6000,2) a (6001,5, 6000,5)
- 0:49,2–0:52,8 anda de (6001,4, 6000,5) a (5994,9, 5998,4)
- 1:02,0–1:06,6 anda de (5994,9, 5998,4) a (5998,0, 6000,2)
- 1:07,5–1:12,0 anda de (5998,0, 6000,2) a (6001,5, 6000,5)
- 1:12,9–1:23,4 anda de (6001,5, 6000,5) a (5998,0, 6000,2)
- 1:48,5–2:09,0 anda de (5998,0, 6000,2) a (6045,0, 6008,0)

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
- 0:47,3 reposo 
- 0:47,3–0:55,7 anda de (6011,4, 6002,4) a (6002,3, 6000,5)
- 0:56,8–0:59,1 habla
- 0:56,9 gesto Cheer02
- 0:59,6 bucle InteractWithPeople_NoWeapon
- 1:02,6 gesto HeadNod01
- 1:05,0 gesto Talk02
- 1:08,7 gesto Laugh01
- 1:12,5 gesto Talk03
- 1:15,2 gesto Question01
- 1:18,7 gesto HeadShake01
- 1:21,4 reposo 
- 1:47,2–2:03,5 anda de (6002,3, 6000,5) a (6038,0, 5993,0)
- 2:17,9 bucle Fear01

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
- 1:21,3 reposo 
- 1:28,2 gesto Fear01
- 1:44,9–2:03,5 anda de (5998,9, 6005,6) a (6041,0, 6006,0)

### v10 (NPC_Aldeano_10)

- 0:00,0 bucle Opening01_Loop
- 0:49,5 reposo 
- 1:28,1 gesto FoundSomething_NoWeapon
- 1:46,2–2:09,2 anda de (5994,0, 6002,8) a (6048,0, 5997,5)

