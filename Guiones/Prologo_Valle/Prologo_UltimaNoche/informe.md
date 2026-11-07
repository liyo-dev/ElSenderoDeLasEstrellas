# Parte de horneado: Prologo_UltimaNoche

2026-11-05 08:15 · guion `Assets/_SEQUENCES/Guiones/Prologo_UltimaNoche.guion.txt`

**3:33,2** de secuencia · 32 frases · 56 planos · 153 efectos · 13 personajes

Reparto medido (m): archimago 1,57/ojos 0,94 · liora 1,58/ojos 0,99 · mago 1,68/ojos 0,94 · nina 1,16/ojos 0,64 · v01 1,31/ojos 0,98 · v02 1,58/ojos 0,98 · v03 1,51/ojos 0,98 · v04 1,68/ojos 0,98 · v06 1,64/ojos 0,98 · v07 1,30/ojos 0,98 · v08 1,58/ojos 0,98 · v09 1,46/ojos 0,97 · v10 1,58/ojos 0,97

## Errores (0)


## Avisos (3)

- línea 467: mago anda a ladera, entrada ritmo=normal llega_mirando=centro_plaza sin_esperar: no se llega del todo a (5988,0, 6000,5) por el NavMesh
- línea 314: plano medio archimago desde=cam_cielo altura=10 mover=cam_baja altura_final=1.15 altura_foco=1.0 lente=40 suave=1.2: algo del decorado se mete delante y tapa un 36% del cuadro. Prueba lado= o desde=.
- línea 483: plano medio archimago lado=frente altura=1.0 altura_foco=1.0 lente=38: algo del decorado se mete delante y tapa un 21% del cuadro. Prueba lado= o desde=.

## Silencios largos entre frases (11)

- 0:18,8 → 0:28,5: 9,7 s sin voz entre PROLOGO_FAVOR_CARRETA y PROLOGO_CARRETA_GRACIAS
- 0:39,6 → 0:42,2: 2,6 s sin voz entre PROLOGO_MANANA_GLOBO_OK y PROLOGO_GLOBO_VECINO_1
- 0:49,8 → 0:53,7: 3,8 s sin voz entre PROLOGO_GLOBO_VECINO_3 y PROLOGO_PLAZA_LIORA_1
- 1:09,4 → 1:27,3: 17,9 s sin voz entre PROLOGO_PLAZA_LIORA_2 y PROLOGO_DESPEDIDA_LIORA_1
- 1:32,7 → 1:35,2: 2,6 s sin voz entre PROLOGO_DESPEDIDA_ARCHIMAGO_1 y PROLOGO_HORA_ARCHIMAGO
- 1:50,8 → 1:54,0: 3,1 s sin voz entre PROLOGO_DESPEDIDA_ELEGIR y PROLOGO_EVACUACION_LIORA
- 1:57,6 → 2:09,0: 11,5 s sin voz entre PROLOGO_EVACUACION_LIORA y PROLOGO_DUELO_MAGO
- 2:18,4 → 2:30,8: 12,3 s sin voz entre PROLOGO_DUELO_ARCHIMAGO y PROLOGO_MAGO_VALLE
- 2:34,0 → 2:37,4: 3,4 s sin voz entre PROLOGO_MAGO_VALLE y PROLOGO_PLEGARIA_01
- 2:44,8 → 2:47,5: 2,8 s sin voz entre PROLOGO_PLEGARIA_01 y PROLOGO_PLEGARIA_02
- 3:21,8 → 3:24,3: 2,5 s sin voz entre PROLOGO_PLEGARIA_06 y PROLOGO_HECHIZO

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | **@entra** |  |
| 0:00,0 | plano 1 | medio de archimago · 5,5 s · lente 40° |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
| 0:00,0 | efecto | VFX: Butterfly03_P en mariposas_c |
| 0:00,0 | efecto | VFX: Butterfly02_P en mariposas_b |
| 0:00,0 | efecto | VFX: Butterfly01_P en mariposas_a |
| 0:00,0 | efecto | VFX: Smoke02_P en humo_horno |
| 0:00,0 | efecto | Apagar: PROP_Nube_Este |
| 0:00,0 | efecto | Nubes que se abren (5,5s) |
| 0:00,0 | efecto | Decorado: mover PROP_Carreta (al instante) |
| 0:00,0 | efecto | Hora del día: Morning (de golpe) |
| 0:00,0 | efecto | Post-procesado: PerfilDeCinematica (0s) |
| 0:00,0 | efecto | Ambiente: AMBIENTE ← Ambience_Prologo_Pueblo |
| 0:00,0 | efecto | Voz: ExteriorDia, +0,0 dB |
| 0:00,0 | efecto | Apagar: PROP_Nube_Oeste |
| 0:05,5 | **@saludo** |  |
| 0:05,5 | plano 2 | medio de archimago · 3,7 s · lente 40° |
| 0:05,5 | archimago | «Buenos días, pueblo. ¿Qué se te ha estropeado hoy?» (3,7 s) · pose |
| 0:09,2 | plano 3 | medio de v02 · 4,0 s · lente 38° |
| 0:09,2 | v01 | «¡Buenos días, maestro!» (1,8 s) · pose |
| 0:11,3 | v02 | «¡Muy buenas, maestro!» (1,8 s) · pose |
| 0:13,1 | **@saludo_fin** |  |
| 0:13,1 | plano 4 | medio de v06 · 5,7 s · lente 38° · coste 2: ángulo 2 |
| 0:13,1 | v06 | «¡Maestro! Se me ha volcado otra vez la carreta. Y eso que hoy iba despacio.» (5,7 s) · pose |
| 0:18,8 | plano 5 | conjunto de archimago · 6,6 s · lente 46° |
| 0:18,8 | efecto | Salto: NPC_Aldeano_06 1,2 m y 0,5 m al frente |
| 0:22,7 | **@carreta_magia** |  |
| 0:23,0 | efecto | SFX: Prologo_MagiaLevitar |
| 0:23,1 | efecto | VFX: VFX_RemolinoDelPrologo en carreta |
| 0:23,3 | efecto | Decorado: mover PROP_Carreta en 1,4s |
| 0:25,3 | plano 6 | medio de v06 · 3,2 s · lente 38° · coste 22: ángulo 14 |
| 0:25,4 | efecto | Decorado: mover PROP_Carreta en 0,9s |
| 0:26,3 | efecto | SFX: Prologo_CarretaCae |
| 0:28,5 | plano 7 | dos: archimago y v06 · 4,2 s · lente 38° |
| 0:28,5 | v06 | «¡Gracias, maestro! No habría podido enderezarla yo solo.» (4,2 s) · pose |
| 0:32,6 | plano 8 | sobre el hombro de archimago a nina · 4,3 s · lente 30° · coste 946: tapado 122, delante 790, cara 25 |
| 0:32,6 | nina | «¡Maestro! El globo se ha enganchado en lo alto del campanario.» (4,3 s) · pose |
| 0:37,0 | plano 9 | medio de archimago · 2,6 s · lente 38° · coste 2: ángulo 2 |
| 0:37,0 | archimago | «Con cuidado... Eso es.» (2,6 s) |
| 0:37,0 | efecto | Hora del día: AfterNoon |
| 0:37,4 | efecto | SFX: Prologo_MagiaLevitar |
| 0:39,6 | **@globo_sube** |  |
| 0:39,6 | plano 10 | general de el punto · 2,6 s · lente 48° |
| 0:39,6 | efecto | Decorado: mover PROP_Globo en 8,0s |
| 0:42,2 | plano 11 | conjunto de nina y v03 y v08 · 7,7 s · lente 40° · coste 0: limpio |
| 0:42,2 | nina | «¡Hala, ya vuela!» (2,6 s) · pose |
| 0:45,1 | v03 | «¡Mira cómo sube!» (2,1 s) · pose |
| 0:47,5 | v08 | «¡Va a llegar hasta las nubes!» (2,3 s) · pose |
| 0:49,8 | **@globo_fin** |  |
| 0:49,8 | plano 12 | medio de archimago · 2,2 s · lente 38° · coste 8: ángulo 8 |
| 0:51,8 | efecto | Apagar: PROP_Globo |
| 0:52,1 | **@rio** |  |
| 0:52,1 | plano 13 | siguiendo a liora · 6,6 s · lente 34° · mezcla 1,2 s |
| 0:52,1 | efecto | Ambiente: parar AMBIENTE |
| 0:52,1 | efecto | Ambiente: AMBIENTE_RIO ← Ambience_Prologo_Rio |
| 0:52,1 | efecto | Hora del día: Sunset |
| 0:52,1 | efecto | Sol: en cuadro, poniéndose en 19 s |
| 0:53,1 | **@rio_paseo** |  |
| 0:53,7 | liora | «¿Has pensado qué contestar a la academia? Llevas semanas con la carta.» (4,4 s) |
| 0:58,7 | plano 14 | medio de archimago · 5,7 s · lente 38° · coste 157: cara 137, ángulo 20 |
| 0:58,7 | archimago | «Hoy pensaba contestar. Pero siempre acaba surgiendo algo en el valle.» (5,7 s) |
| 1:04,4 | plano 15 | medio de liora · 5,0 s · lente 38° · coste 163: cara 137, ángulo 20 |
| 1:04,4 | liora | «No tienes que quedarte solo porque aquí te necesiten. ¿Qué quieres hacer tú?» (5,0 s) · pose |
| 1:09,4 | plano 16 | dos: archimago y liora · 2,2 s · lente 40° |
| 1:11,6 | **@cielo** |  |
| 1:11,6 | plano 17 | medio de archimago · 1,6 s · lente 38° · coste 387: cara 25, ángulo 6, eje 350 |
| 1:11,6 | efecto | Música: parar (3s) |
| 1:11,6 | efecto | VFX: recoger efectos registrados |
| 1:11,6 | efecto | Sol: devolverlo al ciclo |
| 1:11,6 | efecto | Hora del día: Night |
| 1:11,6 | efecto | SFX: SFX_Prologo_Viento |
| 1:11,6 | efecto | Tiempo: encender Viento |
| 1:11,6 | efecto | Ambiente: parar AMBIENTE_RIO |
| 1:11,6 | efecto | Tiempo: encender Tormenta |
| 1:13,2 | plano 18 | general de el punto · 2,8 s · lente 40° |
| 1:13,2 | efecto | Rayo: 2 × fondo del plano |
| 1:16,0 | plano 19 | conjunto de v02 y v10 y v01 y nina · 2,0 s · lente 34° · coste 14: ángulo 14 |
| 1:18,0 | **@llegada** |  |
| 1:18,0 | plano 20 | conjunto de mago · 2,6 s · lente 30° · coste 0: limpio |
| 1:18,0 | efecto | Sacudida de cámara (0,55, 0,6s) |
| 1:18,0 | efecto | SFX: Prologo_Golpe |
| 1:18,0 | efecto | Fogonazo (0,18s) |
| 1:18,0 | efecto | Rayo: 1 × fondo del plano |
| 1:18,0 | efecto | Voz: ExteriorNoche, +0,0 dB |
| 1:18,3 | efecto | SFX: Prologo_PresenciaOscura |
| 1:18,4 | efecto | Música: MAGOOSCURO_REVEAL |
| 1:18,6 | efecto | Ambiente: TORMENTA ← Ambience_Prologo_Tormenta |
| 1:18,6 | efecto | Tiempo: encender Lluvia |
| 1:20,6 | plano 21 | primer plano de mago · 2,2 s · lente 34° · coste 2: ángulo 2 |
| 1:22,8 | plano 22 | medio de archimago · 1,0 s · lente 38° · coste 37: cara 25, ángulo 6 |
| 1:23,8 | plano 23 | siguiendo a mago · 3,5 s · lente 34° |
| 1:27,3 | **@evacuar** |  |
| 1:27,3 | plano 24 | medio de liora · 2,2 s · lente 38° · coste 157: cara 137, ángulo 20 |
| 1:27,3 | liora | «¿Quién es? ¿Qué quiere?» (2,2 s) · pose |
| 1:27,3 | efecto | Ambiente: PANICO ← Ambience_Prologo_Panico |
| 1:27,3 | efecto | Encender: PROP_Incendio |
| 1:27,9 | efecto | Rayo: 1 × fondo del plano |
| 1:29,5 | plano 25 | medio de archimago · 3,1 s · lente 38° · coste 199: cara 137, ángulo 62 |
| 1:29,5 | archimago | «No lo sé. Llévatelos al puente, deprisa.» (3,1 s) · pose |
| 1:32,7 | plano 26 | medio de archimago · 5,2 s · lente 38° · coste 490: pegado 100, delante 361, ángulo 23 |
| 1:35,2 | **@huida** |  |
| 1:35,2 | archimago | «¡Al puente! ¡Todos, ahora!» (2,6 s) · pose |
| 1:37,8 | plano 27 | general de v06 y v04 y v09 y v03 · 1,6 s · lente 40° · coste 9: ángulo 9 |
| 1:38,2 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 1:39,4 | plano 28 | dos: archimago y liora · 3,9 s · lente 40° · coste 410: tapado 122, cara 263, ángulo 18 |
| 1:39,4 | liora | «¿Y tú?» (1,3 s) |
| 1:41,1 | archimago | «Ve. Yo me encargo de él.» (2,3 s) · pose |
| 1:43,4 | plano 29 | medio de liora · 7,5 s · lente 38° |
| 1:43,4 | liora | «Vuelve pronto.» (1,9 s) · pose |
| 1:45,4 | liora | «Y cuando vuelvas, nada de encargos. Te toca descansar.» (5,4 s) · pose |
| 1:50,8 | plano 30 | medio de archimago · 1,8 s · lente 38° · coste 157: cara 100, ángulo 57 |
| 1:52,7 | plano 31 | primer plano de nina · 1,3 s · lente 34° · coste 136: cara 100, ángulo 36 |
| 1:54,0 | plano 32 | conjunto de liora y nina · 3,6 s · lente 40° |
| 1:54,0 | liora | «¡Vamos, Nina! ...Ten mucho cuidado, por favor.» (3,6 s) · pose |
| 1:57,6 | **@acercan** |  |
| 1:57,6 | plano 33 | siguiendo a archimago · 4,5 s · lente 34° |
| 1:58,8 | efecto | Rayo: 1 × fondo del plano |
| 1:59,6 | efecto | Ambiente: parar PANICO |
| 2:02,1 | plano 34 | dos: archimago y mago · 7,0 s · lente 46° |
| 2:02,1 | efecto | Música: MAGOOSCURO_CLIMAX |
| 2:09,0 | **@duelo** |  |
| 2:09,0 | plano 35 | sobre el hombro de archimago a mago · 3,0 s · lente 30° · coste 0: limpio |
| 2:09,0 | mago | «¿Vas a salvarlos a todos tú solo?» (3,0 s) · pose |
| 2:09,0 | efecto | Mezcla: primeraVozOscura |
| 2:09,2 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 2:12,0 | plano 36 | medio de mago · 3,1 s · lente 38° · coste 9: ángulo 3 |
| 2:12,0 | mago | «Ni a ti mismo puedes salvarte.» (3,1 s) · pose |
| 2:15,1 | plano 37 | medio de archimago · 3,3 s · lente 38° · coste 7: ángulo 2 |
| 2:15,1 | archimago | «Mientras siga en pie, no llegarás a ellos.» (3,3 s) · pose |
| 2:15,1 | efecto | Mezcla: primeraVozOscura (quitar) |
| 2:18,4 | plano 38 | dos: archimago y mago · 5,4 s · lente 46° |
| 2:18,6 | efecto | Hechizo: NPC_MagoOscuro → NPC_Archimago |
| 2:18,7 | efecto | VFX: Magic shield blue en NPC_Archimago |
| 2:19,2 | efecto | Sacudida de cámara (0,35, 0,4s) |
| 2:19,2 | efecto | SFX: Prologo_EscudoBloquea |
| 2:19,2 | efecto | Fogonazo (0,12s) |
| 2:21,1 | efecto | Hechizo: NPC_Archimago → NPC_MagoOscuro |
| 2:23,8 | **@choque** |  |
| 2:23,8 | plano 39 | dos: archimago y mago · 3,8 s · lente 38° |
| 2:23,9 | efecto | SFX: Prologo_Carga |
| 2:23,9 | efecto | VFX: Magic circle en NPC_Archimago |
| 2:23,9 | efecto | VFX: Magic circle en NPC_MagoOscuro |
| 2:24,0 | efecto | VFX: 1 Flash_magic_ellow_blue en choque |
| 2:24,4 | efecto | Hechizo: NPC_Archimago → PUNTO_CHOQUE |
| 2:24,4 | efecto | Hechizo: NPC_MagoOscuro → PUNTO_CHOQUE |
| 2:24,5 | efecto | SFX: SFX_Prologo_Latigazo |
| 2:24,5 | efecto | VFX: Charge slash blue en NPC_Archimago |
| 2:24,5 | efecto | VFX: Charge slash purple en NPC_MagoOscuro |
| 2:24,8 | efecto | VFX: FX_LightPillar en choque |
| 2:24,8 | efecto | VFX: Red energy explosion en choque |
| 2:24,8 | efecto | Fogonazo (0,28s) |
| 2:24,8 | efecto | SFX: Prologo_Choque |
| 2:24,8 | efecto | Líneas de concentración: mostrar |
| 2:24,8 | efecto | Sacudida de cámara (0,75, 1s) |
| 2:24,9 | efecto | SFX: Prologue_WarClashStinger_A |
| 2:27,7 | plano 40 | medio de mago · 1,1 s · lente 38° · coste 2: ángulo 2 |
| 2:27,9 | efecto | SFX: Prologo_SueloSeAbre |
| 2:27,9 | efecto | VFX: Charge slash purple en NPC_MagoOscuro |
| 2:27,9 | efecto | SFX: SFX_Prologo_Latigazo |
| 2:28,8 | plano 41 | conjunto de archimago · 2,0 s · lente 42° |
| 2:29,1 | efecto | VFX: MagoOscuroGrieta en NPC_Archimago |
| 2:29,3 | efecto | VFX: Ground AOE explosion en NPC_Archimago |
| 2:29,3 | efecto | Sacudida de cámara (0,7, 0,9s) |
| 2:29,3 | efecto | SFX: Prologo_ImpactoHechizo |
| 2:29,3 | efecto | SFX: SFX_Prologo_Impacto |
| 2:30,8 | **@ultimo** |  |
| 2:30,8 | plano 42 | primer plano de mago · 3,3 s · lente 36° · coste 0: limpio |
| 2:30,8 | mago | «Que este valle no haya existido nunca.» (3,3 s) · pose |
| 2:34,0 | plano 43 | general de archimago y mago · 3,4 s · lente 46° |
| 2:34,1 | efecto | VFX: Dust ground en NPC_MagoOscuro |
| 2:34,1 | efecto | SFX: Prologo_Despegue |
| 2:34,4 | efecto | Rayo: 2 × fondo del plano |
| 2:34,6 | efecto | SFX: Prologo_HechizoGrande |
| 2:34,8 | efecto | SFX: SFX_Prologo_AgujeroNegro_Carga |
| 2:34,8 | efecto | Ambiente: AGUJERO_NEGRO ← SFX_Prologo_AgujeroNegro_Bucle |
| 2:34,8 | efecto | VFX: VFX_AgujeroNegro en agujero |
| 2:34,9 | efecto | Escalar: CARGA_ESFERA ×0,35 en 0 s |
| 2:34,9 | efecto | Escalar: CARGA_ESFERA ×1 en 3,5 s |
| 2:35,0 | efecto | Sacudida de cámara (0,35, 1,6s) |
| 2:35,0 | efecto | VFX: Magic circle en agujero |
| 2:37,4 | plano 44 | medio de archimago · 8,7 s · lente 40° · coste 2: ángulo 2 |
| 2:37,4 | archimago | «No daré ni un paso atrás... ¡No dejaré que los toques! ¡Liora! ¡Nina! ¡A ninguno de ellos!» (7,3 s) |
| 2:37,4 | efecto | Mezcla: plegaria |
| 2:37,4 | efecto | Voz: Ninguno, +0,0 dB |
| 2:37,6 | efecto | VFX: Healing circle en NPC_Archimago |
| 2:37,6 | efecto | SFX: Prologo_MagiaLevitar |
| 2:40,7 | efecto | Cut-in 0: NPC_Liora (Cara) |
| 2:41,9 | efecto | Música: parar (3s) |
| 2:42,4 | efecto | Cut-in 0: NPC_Aldeano_05 (Cara) |
| 2:44,8 | efecto | Pantalla: cubrir (1,2s) |
| 2:44,9 | efecto | Mezcla: plegaria |
| 2:44,9 | efecto | Música: ULTIMO_INCANTAZIONE |
| 2:46,1 | plano 45 | general de mago · 1,4 s · lente 46° |
| 2:46,1 | efecto | Pantalla: descubrir (1,2s) |
| 2:46,1 | efecto | Voz: Conjuro, +6,0 dB |
| 2:46,1 | efecto | Escalar: CARGA_ESFERA ×1,5 en 2 s |
| 2:46,1 | efecto | VFX: Magic circle en agujero |
| 2:46,1 | efecto | Sacudida de cámara (0,25, 1s) |
| 2:47,5 | plano 46 | primer plano de archimago · 8,2 s · lente 34° · coste 2: ángulo 2 |
| 2:47,5 | archimago | «Más allá de la noche y del silencio... ¡Luz que naciste antes que el cielo y tendiste el camino entre las estrellas!» (8,2 s) |
| 2:47,5 | efecto | Conjuro en las manos: NPC_Archimago (34,0 s) |
| 2:47,9 | efecto | VFX: Magic circle en NPC_Archimago |
| 2:48,3 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 2:55,7 | plano 47 | medio de archimago · 8,6 s · lente 40° · coste 2: ángulo 2 |
| 2:55,7 | archimago | «Ningún poder de este mundo puede detenerlo... ¡Por eso llamo a lo que aún no existe! ¡Escucha mi voz y despierta en mis manos!» (8,6 s) |
| 2:58,9 | efecto | Rayo: 1 × fondo del plano |
| 2:59,2 | efecto | SFX: Prologo_Carga |
| 2:59,2 | efecto | VFX: Charge slash blue en NPC_Archimago |
| 3:01,3 | efecto | Escalar: CARGA_ESFERA ×2 en 2 s |
| 3:01,3 | efecto | Sacudida de cámara (0,35, 1,2s) |
| 3:04,4 | plano 48 | general de mago · 1,2 s · lente 46° |
| 3:04,4 | efecto | Rayo: 1 × fondo del plano |
| 3:05,6 | plano 49 | primer plano de archimago · 6,3 s · lente 34° · coste 2: ángulo 2 |
| 3:05,6 | archimago | «Que mi vida sea el precio y mi alma el sello... ¡No importa lo que quede de mí cuando la luz se apague!» (6,3 s) |
| 3:05,6 | efecto | Conjuro a la cabeza: NPC_Archimago (18,0 s) |
| 3:09,1 | efecto | VFX: FX_LightPillar en NPC_Archimago |
| 3:09,1 | efecto | Sacudida de cámara (0,3, 0,9s) |
| 3:09,1 | efecto | SFX: Prologo_MagiaLevitar |
| 3:11,9 | plano 50 | medio de archimago · 6,1 s · lente 40° · coste 2: ángulo 2 |
| 3:11,9 | archimago | «¡Haz de mi último aliento un escudo inquebrantable! ¡Arde conmigo y detén la oscuridad!» (6,1 s) |
| 3:11,9 | efecto | Líneas de concentración: mostrar |
| 3:15,3 | efecto | Rayo: 2 × fondo del plano |
| 3:15,4 | efecto | Escalar: CARGA_ESFERA ×2,6 en 1,4 s |
| 3:15,4 | efecto | SFX: Prologo_Carga |
| 3:15,4 | efecto | Sacudida de cámara (0,5, 1,6s) |
| 3:18,0 | plano 51 | primer plano de archimago · 5,3 s · lente 34° · coste 2: ángulo 2 |
| 3:18,0 | archimago | «Perdóname, Liora... No voy a volver a casa.» (3,8 s) |
| 3:18,0 | efecto | Líneas de concentración: retirar |
| 3:22,2 | efecto | Mezcla: plegaria (quitar) |
| 3:22,2 | efecto | Mezcla: grito |
| 3:22,2 | efecto | Líneas de concentración: mostrar |
| 3:22,2 | efecto | Cut-in 1: NPC_Archimago (Cara) |
| 3:23,3 | **@hechizo** |  |
| 3:23,3 | plano 52 | primer plano de archimago · 1,0 s · lente 26° · coste 0: limpio |
| 3:23,3 | efecto | Respiro: 0,9 s (música 0 dB) |
| 3:24,3 | plano 53 | general de archimago y mago · 1,9 s · lente 44° |
| 3:24,3 | archimago | «¡¡Protégelos a todos!!» (2,1 s) |
| 3:24,5 | efecto | Rayo: 2 × fondo del plano |
| 3:24,6 | efecto | Sacudida de cámara (0,45, 2s) |
| 3:24,6 | efecto | Fusión: BOLA_PROTECTORA +  |
| 3:26,2 | plano 54 | primer plano de archimago · 1,3 s · lente 34° · coste 2: ángulo 2 |
| 3:26,2 | efecto | Sacudida de cámara (0,55, 1,3s) |
| 3:26,2 | efecto | Líneas de concentración: mostrar |
| 3:27,5 | plano 55 | primer plano de mago · 1,2 s · lente 30° · coste 123: cara 62, ángulo 53 |
| 3:27,5 | efecto | Sacudida de cámara (0,7, 1,2s) |
| 3:28,7 | plano 56 | general de archimago y mago · 4,6 s · lente 48° |
| 3:28,7 | efecto | Sacudida de cámara (0,95, 1,4s) |
| 3:29,3 | efecto | Fogonazo (0,14s) |
| 3:29,7 | **@explosion** |  |
| 3:29,7 | efecto | Ambiente: parar TORMENTA |
| 3:29,7 | efecto | Pantalla: cubrir (1,2s) |
| 3:29,7 | efecto | Líneas de concentración: retirar |
| 3:29,7 | efecto | VFX: recoger efectos registrados |
| 3:29,7 | efecto | Ambiente: parar AGUJERO_NEGRO |
| 3:29,7 | efecto | Mezcla: grito (quitar) |
| 3:30,1 | efecto | Sacudida de cámara (0,9, 1,6s) |
| 3:30,6 | efecto | Música: parar (1,2s) |
| 3:30,9 | efecto | Hora del día: Morning (de golpe) (y es con la que se queda el mundo) |
| 3:32,7 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### archimago (NPC_Archimago)

- 0:05,5–0:09,2 habla
- 0:05,6 gesto Talk01
- 0:09,9 gesto HeadNod01
- 0:13,8 gesto HeadNod01
- 0:18,8–0:22,3 anda de (6003,2, 6002,3) a (6006,4, 6000,6)
- 0:22,7 gesto MagicRight
- 0:28,5 gesto HeadNod01
- 0:29,2 gesto SenseSomethingStart_NoWeapon
- 0:33,3 gesto HeadNod01
- 0:37,0 bucle CastingIdle01
- 0:37,0–0:39,6 habla
- 0:39,6 reposo 
- 0:42,8 gesto Laugh01
- 0:42,9 gesto HeadNod01
- 0:48,2 gesto HeadNod01
- 0:49,8 gesto HeadNod01
- 0:53,1–0:58,7 anda de (6016,9, 6002,7) a (6017,4, 5998,3)
- 0:58,7–1:04,4 habla
- 0:58,7 gesto Talk02
- 1:05,1 gesto Question01
- 1:28,0 gesto Question02
- 1:29,5–1:32,7 habla
- 1:29,6 gesto HeadShake01
- 1:31,3 gesto Talk01
- 1:32,7–1:35,3 anda de (6017,4, 5998,3) a (6014,4, 6000,3)
- 1:35,2–1:37,8 habla
- 1:35,3 gesto Challenging_NoWeapon
- 1:40,0 gesto Question01
- 1:41,1–1:43,4 habla
- 1:41,1 gesto HeadNod01
- 1:44,1 gesto HeadNod01
- 1:50,8 gesto HeadNod01
- 1:54,7 gesto SenseSomethingStart_NoWeapon
- 1:57,6–2:06,6 anda de (6014,4, 6000,3) a (6004,4, 5999,8)
- 2:09,7 gesto Question01
- 2:15,1–2:18,4 habla
- 2:15,2 gesto Angry02
- 2:16,7 gesto Talk03
- 2:18,7 gesto Defend_NoWeapon
- 2:20,9 gesto MagicLeft
- 2:23,8 gesto MagicSpecial
- 2:29,3 bucle Pain01
- 2:31,5 gesto HeadShake01
- 2:37,4 reposo 
- 2:37,4 bucle CastingIdle01
- 2:37,4–2:44,8 habla
- 2:47,5 bucle Beg01_Loop
- 2:47,5–2:55,7 habla
- 2:55,7–3:04,4 habla
- 3:05,6 bucle MagicAttackOmni01_Load
- 3:05,6–3:11,9 habla
- 3:11,9–3:18,0 habla
- 3:18,0–3:21,8 habla
- 3:24,3–3:26,3 habla

### liora (NPC_Liora)

- 0:00,5 bucle InteractWithPeople_NoWeapon
- 0:02,1 gesto Question01
- 0:06,2 gesto Question01
- 0:07,1 gesto HeadShake01
- 0:11,2 gesto Talk01
- 0:14,0 gesto HeadNod01
- 0:17,5 gesto Talk02
- 0:21,5 gesto Laugh01
- 0:24,6 gesto Talk03
- 0:29,7 gesto Question01
- 0:34,4 gesto HeadShake01
- 0:37,7 gesto HeadNod01
- 0:39,5 gesto Talk01
- 0:42,9 gesto HeadNod01
- 0:43,4 gesto HeadNod01
- 0:47,5 gesto Talk02
- 0:48,2 gesto HeadNod01
- 0:52,1 reposo 
- 0:53,1–0:58,7 anda de (6015,8, 6002,3) a (6016,1, 5997,9)
- 0:53,7–0:58,1 habla
- 0:59,4 gesto HeadNod01
- 1:04,4–1:09,4 habla
- 1:04,5 gesto Talk01
- 1:07,0 gesto Talk03
- 1:12,1 gesto SenseSomethingStart_NoWeapon
- 1:27,3–1:29,5 habla
- 1:27,4 gesto Question01
- 1:30,2 gesto HeadShake01
- 1:35,9 gesto HeadNod01
- 1:39,4–1:40,8 habla
- 1:39,5–1:40,3 anda de (6016,0, 5997,9) a (6015,0, 5997,8)
- 1:39,5 gesto Question02
- 1:41,8 gesto HeadNod01
- 1:43,4–1:45,3 habla
- 1:43,4 gesto Beg01
- 1:45,4–1:50,8 habla
- 1:45,5 gesto Talk03
- 1:48,7 gesto Talk01
- 1:54,0–1:57,6 habla
- 1:54,1 gesto Talk01
- 1:57,6–2:10,2 anda de (6015,1, 5997,8) a (6042,0, 6000,0)

### mago (NPC_MagoOscuro)

- 1:18,0 bucle Challenging_NoWeapon
- 1:18,0 aparece
- 1:23,8 reposo 
- 1:23,9–1:50,9 anda de (5957,8, 5996,9) a (5988,0, 6000,5)
- 1:57,6–2:09,1 anda de (5988,0, 6000,5) a (6000,8, 5999,8)
- 2:09,0–2:12,0 habla
- 2:09,1 gesto Talk02
- 2:12,0–2:15,1 habla
- 2:12,0 gesto Talk03
- 2:15,8 gesto HeadShake01
- 2:18,4 gesto MagicRight
- 2:21,8 gesto TakeDamage
- 2:23,8 gesto MagicSpecial
- 2:27,7 gesto MagicRight
- 2:30,8–2:34,0 habla
- 2:30,8 gesto Talk03
- 2:34,0 bucle FoundSomething_NoWeapon
- 2:34,0 bucle JumpAir_InPlace_NoWeapon
- 2:38,1 gesto SenseSomethingStart_NoWeapon
- 2:48,2 gesto HeadNod01
- 2:56,4 gesto SenseSomethingStart_NoWeapon
- 3:06,3 gesto SenseSomethingStart_NoWeapon
- 3:12,6 gesto HeadNod01
- 3:18,7 gesto HeadShake01
- 3:25,0 gesto HeadNod01

### nina (NPC_Aldeano_05)

- 0:00,0 bucle SenseSomethingSearching_NoWeapon
- 0:23,1 reposo 
- 0:24,1–0:31,4 anda de (5991,0, 6001,0) a (6004,9, 5998,8)
- 0:32,6–0:37,0 habla
- 0:32,7 gesto Beg01
- 0:37,7 gesto HeadNod01
- 0:42,2–0:44,8 habla
- 0:42,2 gesto Cheer01
- 0:45,8 gesto HeadNod01
- 0:49,9–0:55,0 anda de (6004,8, 5998,8) a (5995,2, 6001,0)
- 0:54,8 bucle InteractWithPeople_NoWeapon
- 0:57,8 gesto Talk01
- 1:01,0 gesto HeadNod01
- 1:04,3 gesto Talk02
- 1:07,1 gesto Laugh01
- 1:10,0 gesto Talk03
- 1:11,9 reposo 
- 1:35,9–1:46,9 anda de (5995,3, 6000,9) a (6016,0, 5998,2)
- 1:39,3 gesto SenseSomethingStart_NoWeapon
- 1:54,7 gesto SenseSomethingStart_NoWeapon
- 1:57,6–2:07,7 anda de (6016,0, 5998,3) a (6026,6, 6007,5)

### v01 (NPC_Aldeano_01)

- 0:00,6–0:01,5 anda de (5995,5, 5998,6) a (5997,0, 5998,4)
- 0:01,9–0:03,4 anda de (5997,0, 5998,4) a (5996,6, 6001,0)
- 0:03,8–0:06,8 anda de (5996,6, 6001,0) a (5998,6, 6001,6)
- 0:04,6 reposo 
- 0:09,2–0:11,0 habla
- 0:09,2 gesto HandWave01
- 0:13,8–0:16,4 anda de (5998,5, 6001,6) a (5995,5, 5998,6)
- 0:17,2–0:18,4 anda de (5995,5, 5998,6) a (5997,3, 5997,6)
- 0:19,2–0:20,4 anda de (5997,3, 5997,6) a (5995,5, 5998,6)
- 0:21,2–0:22,8 anda de (5995,5, 5998,7) a (5994,8, 6001,3)
- 0:23,6–0:25,1 anda de (5994,8, 6001,2) a (5995,5, 5998,6)
- 0:25,9–0:27,1 anda de (5995,5, 5998,6) a (5997,3, 5997,6)
- 0:27,9–0:29,1 anda de (5997,3, 5997,6) a (5995,5, 5998,6)
- 0:29,9–0:31,5 anda de (5995,5, 5998,6) a (5994,8, 6001,3)
- 0:32,3–0:33,9 anda de (5994,8, 6001,3) a (5995,5, 5998,6)
- 0:34,7–0:35,9 anda de (5995,6, 5998,6) a (5997,3, 5997,6)
- 0:36,7–0:37,9 anda de (5997,2, 5997,6) a (5995,5, 5998,6)
- 0:38,7–0:40,8 anda de (5995,5, 5998,7) a (5993,4, 5999,2)
- 0:42,9 gesto HeadNod01
- 0:48,2 gesto HeadNod01
- 0:50,0 gesto Cheer02
- 0:51,4–0:53,5 anda de (5993,5, 5999,2) a (5997,0, 5998,4)
- 0:54,0–0:54,5 anda de (5997,1, 5998,3) a (5997,3, 5997,6)
- 0:55,0–0:55,5 anda de (5997,2, 5997,8) a (5997,0, 5998,4)
- 0:56,0–0:56,8 anda de (5996,8, 5998,4) a (5995,5, 5998,6)
- 0:57,3–0:58,2 anda de (5995,5, 5998,6) a (5997,0, 5998,4)
- 0:58,7–0:59,2 anda de (5997,0, 5998,3) a (5997,3, 5997,6)
- 0:59,7–1:00,2 anda de (5997,3, 5997,7) a (5997,0, 5998,4)
- 1:00,7–1:01,6 anda de (5996,9, 5998,4) a (5995,5, 5998,6)
- 1:02,1–1:03,0 anda de (5995,6, 5998,6) a (5997,0, 5998,4)
- 1:03,5–1:03,9 anda de (5997,1, 5998,2) a (5997,3, 5997,6)
- 1:04,4–1:04,9 anda de (5997,3, 5997,6) a (5997,0, 5998,4)
- 1:05,4–1:06,3 anda de (5997,0, 5998,4) a (5995,5, 5998,6)
- 1:06,8–1:07,7 anda de (5995,5, 5998,6) a (5997,0, 5998,4)
- 1:08,2–1:08,7 anda de (5997,0, 5998,3) a (5997,3, 5997,6)
- 1:09,2–1:09,7 anda de (5997,3, 5997,7) a (5997,0, 5998,4)
- 1:10,2–1:11,1 anda de (5996,9, 5998,4) a (5995,5, 5998,6)
- 1:11,6–1:13,4 anda de (5995,6, 5998,6) a (5996,9, 6001,0)
- 1:11,9 reposo 
- 1:37,7–1:57,5 anda de (5997,1, 6001,0) a (6040,0, 5990,5)

### v02 (NPC_Aldeano_02)

- 0:00,4 bucle InteractWithPeople_NoWeapon
- 0:04,4 reposo 
- 0:05,4 gesto HandWave02
- 0:06,0–0:10,4 anda de (5996,2, 6001,8) a (6004,3, 6000,6)
- 0:06,2 gesto Question01
- 0:11,3–0:13,1 habla
- 0:11,4 gesto HandWave02
- 0:14,0–0:24,1 anda de (6004,3, 6000,6) a (5996,1, 6001,9)
- 0:37,7 gesto HeadNod01
- 0:42,9 gesto HeadNod01
- 0:48,2 gesto HeadNod01
- 0:51,3 bucle InteractWithPeople_NoWeapon
- 0:54,3 gesto Question01
- 0:57,4 gesto HeadShake01
- 1:01,3 gesto Talk01
- 1:04,9 gesto HeadNod01
- 1:07,7 gesto Talk02
- 1:12,0 reposo 
- 1:12,1 gesto SenseSomethingStart_NoWeapon
- 1:18,5 bucle Fear01
- 1:36,7–1:56,9 anda de (5996,3, 6001,9) a (6044,0, 6003,5)
- 2:09,5 bucle Beg01_Loop

### v03 (NPC_Aldeano_03)

- 0:00,2 bucle InteractWithPeople_NoWeapon
- 0:03,8 gesto Talk02
- 0:06,2 gesto Question01
- 0:07,7 gesto Laugh01
- 0:11,6 gesto Talk03
- 0:15,4 gesto Question01
- 0:19,0 gesto HeadShake01
- 0:21,3 gesto Talk01
- 0:24,6 gesto HeadNod01
- 0:26,1 bucle InteractWithPeople_NoWeapon
- 0:29,3 gesto HeadNod01
- 0:33,2 gesto Talk02
- 0:35,6 reposo 
- 0:35,6–0:38,5 anda de (6000,3, 6004,9) a (6001,3, 6000,9)
- 0:37,7 gesto HeadNod01
- 0:42,9 gesto HeadNod01
- 0:45,1–0:47,2 habla
- 0:45,2 gesto FoundSomething_NoWeapon
- 0:48,2 gesto HeadNod01
- 0:49,8 bucle InteractWithPeople_NoWeapon
- 0:51,1 gesto Question01
- 0:53,4 gesto HeadShake01
- 0:57,2 gesto Talk01
- 0:59,4 gesto HeadNod01
- 1:02,4 gesto Talk02
- 1:05,2 gesto Laugh01
- 1:09,0 gesto Talk03
- 1:11,8 reposo 
- 1:12,8 gesto FoundSomething_NoWeapon
- 1:18,6 gesto Fear01
- 1:36,2–1:55,1 anda de (6001,4, 6001,0) a (6046,0, 5999,0)
- 2:34,8 bucle Reverence01_Loop

### v04 (NPC_Aldeano_04)

- 0:00,0 bucle Idle_Normal_NoWeapon
- 0:00,8 reposo 
- 0:00,8–0:03,7 anda de (5997,8, 5995,8) a (5999,9, 5995,8)
- 0:05,5–0:08,4 anda de (5999,9, 5995,8) a (5997,8, 5995,8)
- 0:10,2–0:13,1 anda de (5997,8, 5995,8) a (5999,9, 5995,8)
- 0:14,9–0:17,8 anda de (5999,9, 5995,8) a (5997,8, 5995,8)
- 0:19,6–0:22,5 anda de (5997,8, 5995,8) a (5999,9, 5995,8)
- 0:22,7 reposo 
- 0:24,3–0:27,2 anda de (5999,9, 5995,8) a (5997,8, 5995,8)
- 0:25,6 gesto HandClap01
- 0:29,0–0:31,8 anda de (5997,9, 5995,8) a (5999,9, 5995,8)
- 0:33,6–0:36,5 anda de (5999,9, 5995,8) a (5997,8, 5995,8)
- 0:37,7 gesto HeadNod01
- 0:38,3–0:41,2 anda de (5997,8, 5995,8) a (5999,9, 5995,8)
- 0:42,9 gesto HeadNod01
- 0:43,0–0:45,9 anda de (5999,9, 5995,8) a (5997,8, 5995,8)
- 0:47,7–0:50,6 anda de (5997,8, 5995,8) a (5999,9, 5995,8)
- 0:48,2 gesto HeadNod01
- 0:52,4–0:55,3 anda de (5999,9, 5995,8) a (5997,8, 5995,8)
- 0:57,1–0:59,9 anda de (5997,8, 5995,8) a (5999,9, 5995,8)
- 1:01,7–1:04,6 anda de (5999,9, 5995,8) a (5997,8, 5995,8)
- 1:06,4–1:09,3 anda de (5997,8, 5995,8) a (5999,9, 5995,8)
- 1:11,1–1:14,0 anda de (5999,9, 5995,8) a (5997,8, 5995,8)
- 1:11,8 reposo 
- 1:15,8–1:18,7 anda de (5997,8, 5995,8) a (5999,9, 5995,8)
- 1:20,5–1:23,4 anda de (5999,9, 5995,8) a (5997,8, 5995,8)
- 1:25,2–1:28,1 anda de (5997,8, 5995,8) a (5999,9, 5995,8)
- 1:29,9–1:32,7 anda de (5999,8, 5995,8) a (5997,8, 5995,8)
- 1:34,5–1:54,4 anda de (5997,8, 5995,8) a (6041,6, 5996,6)

### v06 (NPC_Aldeano_06)

- 0:06,2 gesto Question01
- 0:13,1–0:18,8 habla
- 0:13,2 gesto Question01
- 0:15,5 gesto Talk01
- 0:22,9 bucle Idle_Normal_NoWeapon
- 0:23,4 gesto FoundSomething_NoWeapon
- 0:25,4 gesto Cheer01
- 0:26,1 reposo 
- 0:26,2–0:28,5 anda de (6010,0, 6000,4) a (6007,7, 6000,9)
- 0:28,5–0:32,6 habla
- 0:28,6 gesto HeadNod01
- 0:29,9 gesto Talk01
- 0:37,7 gesto HeadNod01
- 0:42,9 gesto HeadNod01
- 0:48,2 gesto HeadNod01
- 0:49,9–0:53,1 anda de (6007,7, 6000,9) a (6010,0, 6000,4)
- 0:53,2 bucle Opening01_Loop
- 0:59,4 gesto HeadNod01
- 1:05,1 gesto Question01
- 1:12,1 reposo 
- 1:28,0 gesto Question02
- 1:30,2 gesto HeadShake01
- 1:35,3–1:47,8 anda de (6010,2, 6000,4) a (6039,0, 6001,0)
- 1:35,9 gesto HeadNod01

### v07 (NPC_Aldeano_07)

- 0:01,6–0:04,5 anda de (5998,0, 6000,2) a (5998,6, 5998,2)
- 0:05,0 reposo 
- 0:05,3–0:08,2 anda de (5998,6, 5998,2) a (5998,0, 6000,2)
- 0:06,0 gesto HandWave01
- 0:09,0–0:11,8 anda de (5998,0, 6000,2) a (5998,6, 5998,2)
- 0:12,6–0:16,1 anda de (5998,6, 5998,2) a (5998,6, 5998,2)
- 0:16,9–0:19,8 anda de (5998,6, 5998,2) a (5998,0, 6000,2)
- 0:20,6–0:23,5 anda de (5998,0, 6000,2) a (5998,6, 5998,2)
- 0:24,3–0:27,1 anda de (5998,6, 5998,3) a (5998,0, 6000,2)
- 0:27,9–0:30,8 anda de (5998,0, 6000,2) a (5998,6, 5998,2)
- 0:31,6–0:34,4 anda de (5998,6, 5998,2) a (5998,0, 6000,2)
- 0:35,2–0:38,1 anda de (5998,0, 6000,2) a (5998,6, 5998,2)
- 0:38,9–0:41,9 anda de (5998,6, 5998,2) a (5994,9, 5998,4)
- 0:42,9 gesto HeadNod01
- 0:48,2 gesto HeadNod01
- 0:52,3–0:56,9 anda de (5994,9, 5998,4) a (5998,0, 6000,2)
- 0:57,8–1:00,7 anda de (5998,0, 6000,2) a (5998,6, 5998,2)
- 1:03,2–1:09,3 anda de (5998,6, 5998,2) a (5994,9, 5998,4)
- 1:11,4–1:13,9 anda de (5994,9, 5998,4) a (5998,0, 6000,2)
- 1:39,3–1:59,7 anda de (5998,2, 6000,2) a (6045,0, 6008,0)

### v08 (NPC_Aldeano_08)

- 0:01,0 bucle InteractWithPeople_NoWeapon
- 0:03,6 gesto Laugh01
- 0:06,2 gesto Question01
- 0:07,9 gesto Talk03
- 0:10,5 gesto Question01
- 0:14,0 gesto HeadShake01
- 0:16,8 gesto Talk01
- 0:20,9 gesto HeadNod01
- 0:24,3 gesto Talk02
- 0:26,9 gesto Laugh01
- 0:31,6 reposo 
- 0:31,6–0:37,8 anda de (6011,4, 6002,4) a (6002,3, 6000,5)
- 0:37,7 gesto HeadNod01
- 0:42,9 gesto HeadNod01
- 0:47,5–0:49,8 habla
- 0:47,6 gesto Cheer02
- 0:50,3 bucle InteractWithPeople_NoWeapon
- 0:53,5 gesto HeadNod01
- 0:56,5 gesto Talk02
- 0:59,2 gesto Laugh01
- 1:02,4 gesto Talk03
- 1:06,1 gesto Question01
- 1:08,7 gesto HeadShake01
- 1:12,1 reposo 
- 1:38,4–1:54,6 anda de (6002,5, 6000,5) a (6038,0, 5993,0)
- 2:09,9 bucle Fear01

### v09 (NPC_Aldeano_09)

- 0:01,4 bucle InteractWithPeople_NoWeapon
- 0:05,4 gesto Talk03
- 0:06,2 gesto Question01
- 0:08,4 gesto Question01
- 0:12,2 gesto HeadShake01
- 0:15,1 gesto Talk01
- 0:19,0 gesto HeadNod01
- 0:21,9 gesto Talk02
- 0:25,5 gesto HandClap01
- 0:26,7 bucle InteractWithPeople_NoWeapon
- 0:28,7 gesto Talk02
- 0:32,5 gesto Laugh01
- 0:35,6 gesto Talk03
- 0:37,7 gesto HeadNod01
- 0:40,0 gesto Question01
- 0:42,9 gesto HeadNod01
- 0:43,3 gesto HeadShake01
- 0:47,7 gesto Talk01
- 0:48,2 gesto HeadNod01
- 0:52,6 gesto HeadNod01
- 0:55,6 gesto Talk02
- 0:59,8 gesto Laugh01
- 1:03,0 gesto Talk03
- 1:06,4 gesto Question01
- 1:10,2 gesto HeadShake01
- 1:12,0 reposo 
- 1:18,9 gesto Fear01
- 1:36,1–1:54,6 anda de (5999,0, 6005,5) a (6041,0, 6006,0)
- 2:10,2 bucle Fear01

### v10 (NPC_Aldeano_10)

- 0:00,0 bucle Opening01_Loop
- 0:06,2 gesto Question01
- 0:07,7 gesto HeadNod01
- 0:40,2 reposo 
- 0:42,9 gesto HeadNod01
- 0:48,2 gesto HeadNod01
- 0:50,1 gesto HandClap01
- 1:18,8 gesto FoundSomething_NoWeapon
- 1:37,0–1:59,9 anda de (5994,1, 6002,7) a (6048,0, 5997,5)
- 2:35,8 bucle Beg01_Loop

