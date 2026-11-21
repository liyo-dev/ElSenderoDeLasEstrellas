# Parte de horneado: Prologo_UltimaNoche

2026-11-21 13:35 · guion `Assets/_SEQUENCES/Guiones/Prologo_UltimaNoche.guion.txt`

**4:38,9** de secuencia · 32 frases · 57 planos · 189 efectos · 19 personajes

Reparto medido (m): archimago 1,36/ojos 0,94 · liora 1,35/ojos 0,94 · mago 1,81/ojos 0,98 · nina 1,16/ojos 0,63 · v01 1,31/ojos 0,98 · v02 1,58/ojos 0,98 · v03 1,51/ojos 0,98 · v04 1,68/ojos 0,98 · v06 1,64/ojos 0,98 · v07 1,30/ojos 0,98 · v08 1,58/ojos 0,98 · v09 1,46/ojos 0,97 · v10 1,58/ojos 0,97 · panadera 1,55/ojos 0,95 · madre 1,44/ojos 0,94 · lucas 1,23/ojos 0,63 · vm 1,58/ojos 0,94 · ab 1,55/ojos 0,95 · vc 1,51/ojos 0,94

## Errores (0)


## Avisos (9)

- línea 680: mago anda a ladera, entrada ritmo=normal llega_mirando=centro_plaza sin_esperar: un punto queda fuera del NavMesh (-202,0, -365,0); voy en línea recta
- El primer plano empieza en 63,1 s: hasta entonces la cámara se queda donde estuviera.
- línea 489: plano hombro archimago vm lente=34: no cabe el contraplano sobre el hombro de archimago; hago un plano medio de vm.
- línea 494: plano hombro archimago madre lente=32: no cabe el contraplano sobre el hombro de archimago; hago un plano medio de madre.
- línea 494: plano hombro archimago madre lente=32: algo del decorado se mete delante y tapa un 9% del cuadro. Prueba lado= o desde=.
- línea 731: plano conjunto liora nina altura=1.0 altura_foco=0.9 lente=40: liora habla de espaldas a la cámara en 175,7 s (157°). Prueba lado=, otro desde= o un plano nuevo cuando se gire.
- línea 885: plano general mago desde=cam_final agujero altura_foco=0 altura=1.0 lente=46: algo del decorado se mete delante y tapa un 40% del cuadro. Prueba lado= o desde=.
- línea 914: plano general mago desde=cam_final agujero altura_foco=0 altura=0.9 lente=46: algo del decorado se mete delante y tapa un 41% del cuadro. Prueba lado= o desde=.
- 2:46,2: liora y nina se atraviesan.

## Silencios largos entre frases (11)

- 1:16,1 → 1:26,6: 10,5 s sin voz entre PROLOGO_FAVOR_CARRETA y PROLOGO_CARRETA_GRACIAS
- 1:37,7 → 1:40,3: 2,6 s sin voz entre PROLOGO_MANANA_GLOBO_OK y PROLOGO_GLOBO_VECINO_1
- 1:47,9 → 1:52,9: 4,9 s sin voz entre PROLOGO_GLOBO_VECINO_3 y PROLOGO_PLAZA_LIORA_1
- 2:08,6 → 2:27,5: 18,9 s sin voz entre PROLOGO_PLAZA_LIORA_2 y PROLOGO_DESPEDIDA_LIORA_1
- 2:32,9 → 2:35,4: 2,6 s sin voz entre PROLOGO_DESPEDIDA_ARCHIMAGO_1 y PROLOGO_HORA_ARCHIMAGO
- 2:38,0 → 2:39,6: 1,6 s sin voz entre PROLOGO_HORA_ARCHIMAGO y PROLOGO_DESPEDIDA_LIORA_2
- 2:50,7 → 2:53,9: 3,1 s sin voz entre PROLOGO_DESPEDIDA_ELEGIR y PROLOGO_EVACUACION_LIORA
- 2:57,5 → 3:08,9: 11,5 s sin voz entre PROLOGO_EVACUACION_LIORA y PROLOGO_DUELO_MAGO
- 3:18,3 → 3:37,0: 18,7 s sin voz entre PROLOGO_DUELO_ARCHIMAGO y PROLOGO_MAGO_VALLE
- 3:40,3 → 3:43,7: 3,4 s sin voz entre PROLOGO_MAGO_VALLE y PROLOGO_PLEGARIA_01
- 3:51,0 → 3:53,8: 2,8 s sin voz entre PROLOGO_PLEGARIA_01 y PROLOGO_PLEGARIA_02

## Cinemática viva

Vida de fondo puesta sola (nadie quieto en cuadro):

- archimago: 3 micro-acción(es) de reposo en los huecos en cuadro.
- nina: 2 micro-acción(es) de reposo en los huecos en cuadro.
- v01: 1 micro-acción(es) de reposo en los huecos en cuadro.
- v03: 1 micro-acción(es) de reposo en los huecos en cuadro.
- v06: 1 micro-acción(es) de reposo en los huecos en cuadro.
- v07: 1 micro-acción(es) de reposo en los huecos en cuadro.
- v08: 2 micro-acción(es) de reposo en los huecos en cuadro.

Reacciones automáticas (9; escribe la tuya en el guion para cambiarla):

- 2:12,7 nina reacciona a el trueno: SenseSomethingStart_NoWeapon
- 2:12,7 v01 reacciona a el trueno: SenseSomethingStart_NoWeapon
- 2:12,8 v02 reacciona a el trueno: SenseSomethingStart_NoWeapon
- 2:12,7 v06 reacciona a el trueno: SenseSomethingStart_NoWeapon
- 2:12,8 v08 reacciona a el trueno: SenseSomethingStart_NoWeapon
- 2:12,9 v09 reacciona a el trueno: SenseSomethingStart_NoWeapon
- 3:26,3 archimago para el hechizo de mago: DefendHit_NoWeapon
- 3:27,3 archimago recibe el hechizo de mago: TakeDamage
- 3:28,2 archimago recibe el hechizo de mago: TakeDamage_2

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
| 0:00,0 | efecto | Voz: ExteriorDia, +0,0 dB |
| 0:00,0 | efecto | Apertura: el deseo en el Sendero (31,5s) |
| 0:00,0 | efecto | Rótulo: PROLOGO_ROTULO_SIGLOS |
| 0:31,5 | efecto | Apertura: vuelo de cámara al valle con el logo (32,0s) |
| 0:31,5 | efecto | VFX: Butterfly03_P en mariposas_c |
| 0:31,5 | efecto | VFX: Butterfly02_P en mariposas_b |
| 0:31,5 | efecto | VFX: Butterfly01_P en mariposas_a |
| 0:31,5 | efecto | Apagar: PROP_Nube_Este |
| 0:31,5 | efecto | Decorado: mover PROP_Carreta (al instante) |
| 0:31,5 | efecto | Hora del día: Morning (de golpe) |
| 0:31,5 | efecto | Post-procesado: PerfilDeCinematica (0s) |
| 0:31,5 | efecto | Ambiente: AMBIENTE ← Ambience_Prologo_Pueblo |
| 0:31,5 | efecto | Música: PROLOGO_AMANECER_VALLE |
| 0:31,5 | efecto | Apagar: PROP_Nube_Oeste |
| 1:03,1 | **@entra** |  |
| 1:03,1 | **@saludo** |  |
| 1:03,1 | plano 1 | medio de archimago · 3,7 s · lente 50° |
| 1:03,1 | archimago | «Buenos días, pueblo. ¿Qué se te ha estropeado hoy?» (3,7 s) · pose |
| 1:03,7 | efecto | Hojas: abrir Puerta de la madre |
| 1:03,7 | efecto | SFX: SFX_Pueblo_Contraventana_Abre |
| 1:06,3 | efecto | SFX: SFX_Pueblo_Contraventana_Abre |
| 1:06,3 | efecto | Hojas: abrir Mirador · Vecino |
| 1:06,8 | plano 2 | medio de vm · 1,8 s · lente 34° · coste 73: cara 25, ángulo 42 |
| 1:06,8 | vm | «¡Buenos días, maestro!» (1,8 s) · pose |
| 1:07,3 | efecto | Hojas: abrir Mirador · Abuela |
| 1:07,3 | efecto | SFX: SFX_Pueblo_Contraventana_Abre |
| 1:08,6 | plano 3 | medio de madre · 1,8 s · lente 32° · coste 626: estorbo 469, cara 100, ángulo 51 |
| 1:08,6 | madre | «¡Muy buenas, maestro!» (1,8 s) · pose |
| 1:10,4 | **@saludo_fin** |  |
| 1:10,4 | plano 4 | medio de v06 · 5,7 s · lente 38° · coste 2: ángulo 2 |
| 1:10,4 | v06 | «¡Maestro! Se me ha volcado otra vez la carreta. Y eso que hoy iba despacio.» (5,7 s) · pose |
| 1:10,8 | efecto | De la mano: MadreDelValle coge a LucasDelValle |
| 1:10,8 | efecto | Hojas: abrir Mirador · Vecina |
| 1:10,8 | efecto | SFX: SFX_Pueblo_Contraventana_Abre |
| 1:13,4 | efecto | Hojas: cerrar Puerta de la madre |
| 1:16,1 | plano 5 | conjunto de archimago · 4,0 s · lente 46° |
| 1:20,0 | **@carreta_magia** |  |
| 1:20,0 | plano 6 | general de archimago · 4,2 s · lente 56° |
| 1:20,3 | efecto | SFX: Prologo_MagiaLevitar |
| 1:20,4 | efecto | VFX: Luz_Remolino en carreta |
| 1:20,6 | efecto | Decorado: mover PROP_Carreta en 1,0s |
| 1:21,7 | efecto | Decorado: mover PROP_Carreta en 1,1s |
| 1:23,0 | efecto | Decorado: mover PROP_Carreta en 0,8s |
| 1:23,8 | efecto | SFX: Prologo_CarretaCae |
| 1:24,2 | plano 7 | medio de v06 · 2,4 s · lente 38° · coste 14: ángulo 14 |
| 1:26,6 | plano 8 | sobre el hombro de archimago a v06 · 4,2 s · lente 30° · coste 80: cara 80 |
| 1:26,6 | v06 | «¡Gracias, maestro! No habría podido enderezarla yo solo.» (4,2 s) · pose |
| 1:30,7 | plano 9 | sobre el hombro de archimago a nina · 4,3 s · lente 30° · coste 859: tapado 122, delante 665, cara 63 |
| 1:30,7 | nina | «¡Maestro! El globo se ha enganchado en lo alto del campanario.» (4,3 s) · pose |
| 1:35,1 | plano 10 | medio de archimago · 2,6 s · lente 38° · coste 2: ángulo 2 |
| 1:35,1 | archimago | «Con cuidado... Eso es.» (2,6 s) |
| 1:35,1 | efecto | Hora del día: AfterNoon |
| 1:35,5 | efecto | SFX: Prologo_MagiaLevitar |
| 1:37,7 | **@globo_sube** |  |
| 1:37,7 | plano 11 | general de el punto · 2,6 s · lente 48° |
| 1:37,7 | efecto | Decorado: mover PROP_Globo en 8,0s |
| 1:40,3 | plano 12 | conjunto de nina y v03 y v08 · 7,7 s · lente 40° · coste 0: limpio |
| 1:40,3 | nina | «¡Hala, ya vuela!» (2,6 s) · pose |
| 1:43,2 | v03 | «¡Mira cómo sube!» (2,1 s) · pose |
| 1:45,6 | v08 | «¡Va a llegar hasta las nubes!» (2,3 s) · pose |
| 1:47,9 | **@globo_fin** |  |
| 1:47,9 | plano 13 | medio de archimago · 3,3 s · lente 38° · coste 3: ángulo 3 |
| 1:49,9 | efecto | Apagar: PROP_Globo |
| 1:50,2 | **@rio** |  |
| 1:50,2 | efecto | Pantalla: cubrir (1s) |
| 1:51,3 | plano 14 | siguiendo a liora · 6,6 s · lente 34° |
| 1:51,3 | efecto | Pantalla: descubrir (1s) |
| 1:51,3 | efecto | Sol: en cuadro, poniéndose en 19 s |
| 1:51,3 | efecto | Música: PROLOGUE_DREAM |
| 1:51,3 | efecto | Ambiente: AMBIENTE_RIO ← Ambience_Prologo_Rio |
| 1:51,3 | efecto | Ambiente: parar AMBIENTE |
| 1:51,3 | efecto | Hora del día: Sunset |
| 1:52,3 | **@rio_paseo** |  |
| 1:52,9 | liora | «¿Has pensado qué contestar a la academia? Llevas semanas con la carta.» (4,4 s) |
| 1:57,9 | plano 15 | medio de archimago · 5,7 s · lente 38° · coste 121: cara 100, ángulo 15 |
| 1:57,9 | archimago | «Hoy pensaba contestar. Pero siempre acaba surgiendo algo en el valle.» (5,7 s) |
| 2:03,6 | plano 16 | medio de liora · 5,0 s · lente 38° · coste 163: cara 100, ángulo 57 |
| 2:03,6 | liora | «No tienes que quedarte solo porque aquí te necesiten. ¿Qué quieres hacer tú?» (5,0 s) · pose |
| 2:08,6 | plano 17 | dos: archimago y liora · 2,2 s · lente 40° |
| 2:10,8 | **@cielo** |  |
| 2:10,8 | plano 18 | medio de archimago · 1,6 s · lente 38° · coste 289: cara 212, ángulo 71 |
| 2:10,8 | efecto | Ambiente: parar AMBIENTE_RIO |
| 2:10,8 | efecto | SFX: SFX_Prologo_Viento |
| 2:10,8 | efecto | Tiempo: encender Viento |
| 2:10,8 | efecto | Pasa el tiempo: Night en 5 s (Animado) · Tormenta |
| 2:10,8 | efecto | Sol: devolverlo al ciclo |
| 2:10,8 | efecto | VFX: recoger efectos registrados |
| 2:10,8 | efecto | Música: parar (3s) |
| 2:11,6 | efecto | Hojas: cerrar Pueblo del prólogo en 3 s |
| 2:11,8 | efecto | Hojas: abrir Puerta de la madre |
| 2:11,9 | efecto | SFX: SFX_Pueblo_Contraventana_Golpe |
| 2:12,0 | efecto | De la mano: MadreDelValle suelta a LucasDelValle |
| 2:12,4 | plano 19 | general de el punto · 2,8 s · lente 40° |
| 2:12,4 | efecto | Rayo: 2 × fondo del plano |
| 2:12,7 | efecto | SFX: SFX_Pueblo_Contraventana_Golpe |
| 2:13,4 | efecto | SFX: SFX_Pueblo_Contraventana_Golpe |
| 2:14,1 | efecto | SFX: SFX_Pueblo_Contraventana_Golpe |
| 2:15,2 | efecto | Hojas: cerrar Puerta de la madre |
| 2:15,2 | plano 20 | conjunto de v02 y v10 y v01 y nina · 2,0 s · lente 34° · coste 14: ángulo 14 |
| 2:17,2 | **@llegada** |  |
| 2:17,2 | plano 21 | conjunto de mago · 4,0 s · lente 30° · coste 299: cara 210, ángulo 81 |
| 2:17,2 | efecto | Voz: ExteriorNoche, +0,0 dB |
| 2:17,2 | efecto | Niebla de suelo en NPC_MagoOscuro (6 m) |
| 2:17,2 | efecto | Fogonazo (0,18s) |
| 2:17,2 | efecto | Rayo: 1 × fondo del plano |
| 2:17,2 | efecto | SFX: Prologo_Golpe |
| 2:17,2 | efecto | Sacudida de cámara (0,55, 0,6s) |
| 2:17,5 | efecto | SFX: Prologo_PresenciaOscura |
| 2:17,6 | efecto | Música: MAGOOSCURO_REVEAL |
| 2:17,8 | efecto | Ambiente: TORMENTA ← Ambience_Prologo_Tormenta |
| 2:17,8 | efecto | Tiempo: encender Lluvia |
| 2:21,2 | plano 22 | medio de mago · 1,8 s · lente 34° · coste 205: cara 137, ángulo 62 |
| 2:23,0 | plano 23 | medio de archimago · 1,0 s · lente 38° · coste 31: cara 25, ángulo 6 |
| 2:24,0 | plano 24 | siguiendo a mago · 3,5 s · lente 34° |
| 2:24,5 | efecto | Rayo: 1 × fondo del plano |
| 2:27,5 | **@evacuar** |  |
| 2:27,5 | plano 25 | medio de liora · 2,2 s · lente 38° · coste 121: cara 100, ángulo 15 |
| 2:27,5 | liora | «¿Quién es? ¿Qué quiere?» (2,2 s) · pose |
| 2:27,5 | efecto | Ambiente: PANICO ← Ambience_Prologo_Panico |
| 2:29,7 | plano 26 | medio de archimago · 3,1 s · lente 38° · coste 121: cara 100, ángulo 15 |
| 2:29,7 | archimago | «No lo sé. Llévatelos al puente, deprisa.» (3,1 s) · pose |
| 2:32,9 | plano 27 | medio de archimago · 5,2 s · lente 38° · coste 90: cara 73, ángulo 9 |
| 2:35,4 | **@huida** |  |
| 2:35,4 | archimago | «¡Al puente! ¡Todos, ahora!» (2,6 s) · pose |
| 2:37,4 | efecto | Hojas: abrir Puerta de la madre |
| 2:37,9 | efecto | De la mano: MadreDelValle coge a LucasDelValle |
| 2:38,0 | plano 28 | general de v06 y v04 y v09 y v03 · 1,6 s · lente 40° · coste 0: limpio |
| 2:38,4 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 2:39,6 | plano 29 | medio de liora · 1,3 s · lente 38° · coste 11: ángulo 5 |
| 2:39,6 | liora | «¿Y tú?» (1,3 s) |
| 2:40,9 | plano 30 | medio de archimago · 2,3 s · lente 38° · coste 808: delante 461, cara 175, ángulo 24 |
| 2:40,9 | archimago | «Ve. Yo me encargo de él.» (2,3 s) · pose |
| 2:43,2 | plano 31 | dos: archimago y liora · 7,5 s · lente 40° |
| 2:43,2 | liora | «Vuelve pronto.» (1,9 s) |
| 2:45,3 | liora | «Y cuando vuelvas, nada de encargos. Te toca descansar.» (5,4 s) |
| 2:50,7 | plano 32 | medio de archimago · 1,8 s · lente 38° · coste 199: cara 175, ángulo 24 |
| 2:52,6 | plano 33 | sobre el hombro de liora a nina · 1,3 s · lente 30° · coste 63: cara 63 |
| 2:53,9 | **@vamos** |  |
| 2:53,9 | plano 34 | conjunto de liora y nina · 3,6 s · lente 40° · coste 331: cara 276, ángulo 50 |
| 2:53,9 | liora | «¡Vamos, Nina! ...Ten mucho cuidado, por favor.» (3,6 s) |
| 2:57,5 | **@acercan** |  |
| 2:57,5 | plano 35 | siguiendo a archimago · 4,5 s · lente 34° |
| 2:58,7 | efecto | Rayo: 1 × fondo del plano |
| 2:59,5 | efecto | Ambiente: parar PANICO |
| 3:02,0 | plano 36 | general de archimago y mago · 7,0 s · lente 50° |
| 3:02,0 | efecto | Música: MAGOOSCURO_CLIMAX |
| 3:08,9 | **@duelo** |  |
| 3:08,9 | plano 37 | sobre el hombro de archimago a mago · 3,0 s · lente 30° · coste 0: limpio |
| 3:08,9 | mago | «¿Vas a salvarlos a todos tú solo?» (3,0 s) · pose |
| 3:08,9 | efecto | Mezcla: primeraVozOscura |
| 3:09,1 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 3:11,9 | plano 38 | medio de mago · 3,1 s · lente 38° · coste 9: ángulo 3 |
| 3:11,9 | mago | «Ni a ti mismo puedes salvarte.» (3,1 s) |
| 3:15,0 | plano 39 | medio de archimago · 3,3 s · lente 38° · coste 3: ángulo 3 |
| 3:15,0 | archimago | «Mientras siga en pie, no llegarás a ellos.» (3,3 s) · pose |
| 3:15,0 | efecto | Mezcla: primeraVozOscura (quitar) |
| 3:18,3 | **@intercambio** |  |
| 3:18,3 | plano 40 | dos: archimago y mago · 7,0 s · lente 58° |
| 3:19,7 | efecto | Salto: NPC_Archimago 2,2 m y 1,6 m de lado |
| 3:20,0 | efecto | Hechizo: NPC_MagoOscuro → NPC_Archimago |
| 3:21,3 | efecto | Hechizo: NPC_Archimago → NPC_MagoOscuro |
| 3:25,3 | plano 41 | dos: archimago y mago · 5,8 s · lente 62° |
| 3:25,4 | efecto | VFX: Luz_Escudo en NPC_Archimago |
| 3:25,7 | efecto | Hechizo: NPC_MagoOscuro → NPC_Archimago |
| 3:26,6 | efecto | Hechizo: NPC_MagoOscuro → NPC_Archimago |
| 3:27,6 | efecto | Hechizo: NPC_MagoOscuro → NPC_Archimago |
| 3:31,1 | **@choque** |  |
| 3:31,1 | plano 42 | dos: archimago y mago · 3,8 s · lente 58° |
| 3:31,2 | efecto | Conjuro en las manos: NPC_Archimago (0,6 s) |
| 3:31,2 | efecto | Conjuro en las manos: NPC_MagoOscuro (0,6 s) |
| 3:31,2 | efecto | VFX: Luz_Circulo en NPC_Archimago |
| 3:31,2 | efecto | VFX: Sombra_Circulo en NPC_MagoOscuro |
| 3:31,3 | efecto | VFX: Marca en choque |
| 3:31,7 | efecto | Hechizo: NPC_Archimago → PUNTO_CHOQUE |
| 3:31,7 | efecto | Hechizo: NPC_MagoOscuro → PUNTO_CHOQUE |
| 3:31,8 | efecto | SFX: SFX_Prologo_Latigazo |
| 3:32,1 | efecto | Sacudida de cámara (0,75, 1s) |
| 3:32,1 | efecto | Fogonazo (0,28s) |
| 3:32,1 | efecto | Líneas de concentración: mostrar |
| 3:32,1 | efecto | VFX: Choque en choque |
| 3:32,1 | efecto | SFX: Prologo_Choque |
| 3:32,1 | efecto | VFX: Pilar en choque |
| 3:32,2 | efecto | SFX: Prologue_WarClashStinger_A |
| 3:35,0 | plano 43 | dos: archimago y mago · 2,1 s · lente 58° |
| 3:35,2 | efecto | Hechizo: NPC_MagoOscuro → NPC_Archimago |
| 3:35,5 | efecto | SFX: Prologo_SueloSeAbre |
| 3:35,5 | efecto | SFX: SFX_Prologo_Latigazo |
| 3:35,8 | efecto | VFX: Sombra_Grieta en NPC_Archimago |
| 3:36,0 | efecto | VFX: Sombra_Onda en NPC_Archimago |
| 3:36,0 | efecto | Sacudida de cámara (0,7, 0,9s) |
| 3:36,0 | efecto | SFX: Prologo_ImpactoHechizo |
| 3:36,1 | efecto | SFX: SFX_Prologo_Impacto |
| 3:37,0 | **@ultimo** |  |
| 3:37,0 | plano 44 | primer plano de mago · 3,3 s · lente 36° · coste 5: ángulo 5 |
| 3:37,0 | mago | «Que este valle no haya existido nunca.» (3,3 s) · pose |
| 3:40,3 | plano 45 | general de archimago y mago · 3,4 s · lente 46° |
| 3:40,4 | efecto | SFX: Prologo_Despegue |
| 3:40,4 | efecto | VFX: Polvo en NPC_MagoOscuro |
| 3:40,6 | efecto | Rayo: 2 × fondo del plano |
| 3:40,9 | efecto | SFX: Prologo_HechizoGrande |
| 3:41,1 | efecto | SFX: SFX_Prologo_AgujeroNegro_Carga |
| 3:41,1 | efecto | Ambiente: AGUJERO_NEGRO ← SFX_Prologo_AgujeroNegro_Bucle |
| 3:41,1 | efecto | VFX: Sombra_CargaGrande en agujero |
| 3:41,1 | efecto | Escalar: CARGA_ESFERA ×0,35 en 0 s |
| 3:41,2 | efecto | Escalar: CARGA_ESFERA ×1 en 3,5 s |
| 3:41,3 | efecto | VFX: Sombra_Circulo en NPC_MagoOscuro |
| 3:41,3 | efecto | Sacudida de cámara (0,35, 1,6s) |
| 3:43,7 | plano 46 | medio de archimago · 8,7 s · lente 42° · coste 0: limpio |
| 3:43,7 | archimago | «No daré ni un paso atrás... ¡No dejaré que los toques! ¡Liora! ¡Nina! ¡A ninguno de ellos!» (7,3 s) |
| 3:43,7 | efecto | Voz: Ninguno, +0,0 dB |
| 3:43,7 | efecto | Mezcla: plegaria |
| 3:43,9 | efecto | VFX: Luz_Circulo en NPC_Archimago |
| 3:43,9 | efecto | SFX: Prologo_MagiaLevitar |
| 3:47,0 | efecto | Cut-in 0: NPC_Liora (Cara) |
| 3:47,0 | efecto | Música: parar (3s) |
| 3:48,7 | efecto | Cut-in 0: NPC_Aldeano_05 (Cara) |
| 3:50,0 | efecto | Música: PROLOGO_CLIMAX_CRESCENDO |
| 3:50,0 | efecto | Mezcla: plegaria |
| 3:51,0 | efecto | Pantalla: cubrir (1,2s) |
| 3:52,4 | plano 47 | general de mago · 1,4 s · lente 46° |
| 3:52,4 | efecto | Pantalla: descubrir (1,2s) |
| 3:52,4 | efecto | Voz: Conjuro, +6,0 dB |
| 3:52,4 | efecto | Escalar: CARGA_ESFERA ×1,5 en 2 s |
| 3:52,4 | efecto | Sacudida de cámara (0,25, 1s) |
| 3:53,8 | plano 48 | conjunto de archimago · 8,2 s · lente 46° · coste 0: limpio |
| 3:53,8 | archimago | «Más allá de la noche y del silencio... ¡Luz que naciste antes que el cielo y tendiste el camino entre las estrellas!» (8,2 s) |
| 3:53,8 | efecto | Conjuro en las manos: NPC_Archimago (34,0 s) |
| 3:53,8 | efecto | SFX: SFX_Prologo_CargaPlegaria |
| 3:54,6 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 4:02,0 | plano 49 | medio de archimago · 8,6 s · lente 48° · coste 0: limpio |
| 4:02,0 | archimago | «Ningún poder de este mundo puede detenerlo... ¡Por eso llamo a lo que aún no existe! ¡Escucha mi voz y despierta en mis manos!» (8,6 s) |
| 4:05,2 | efecto | Rayo: 1 × fondo del plano |
| 4:07,6 | efecto | Sacudida de cámara (0,35, 1,2s) |
| 4:07,6 | efecto | Escalar: CARGA_ESFERA ×2 en 2 s |
| 4:10,6 | plano 50 | general de mago · 1,2 s · lente 46° |
| 4:10,6 | efecto | Rayo: 1 × fondo del plano |
| 4:11,8 | plano 51 | conjunto de archimago · 6,3 s · lente 58° · coste 0: limpio |
| 4:11,8 | archimago | «Que mi vida sea el precio y mi alma el sello... ¡No importa lo que quede de mí cuando la luz se apague!» (6,3 s) |
| 4:11,8 | efecto | Conjuro a la cabeza: NPC_Archimago (16,0 s) |
| 4:15,3 | efecto | VFX: Pilar en NPC_Archimago |
| 4:15,3 | efecto | SFX: Prologo_MagiaLevitar |
| 4:15,3 | efecto | Sacudida de cámara (0,3, 0,9s) |
| 4:18,1 | plano 52 | conjunto de archimago · 6,1 s · lente 58° · coste 260: eje 260 |
| 4:18,1 | archimago | «¡Haz de mi último aliento un escudo inquebrantable! ¡Arde conmigo y detén la oscuridad!» (6,1 s) |
| 4:18,1 | efecto | Líneas de concentración: mostrar |
| 4:21,5 | efecto | Rayo: 2 × fondo del plano |
| 4:21,6 | efecto | Sacudida de cámara (0,5, 1,6s) |
| 4:21,6 | efecto | Escalar: CARGA_ESFERA ×2,6 en 1,4 s |
| 4:24,3 | plano 53 | primer plano de archimago · 4,2 s · lente 32° · coste 0: limpio |
| 4:24,3 | archimago | «Perdóname, Liora... No voy a volver a casa.» (3,8 s) |
| 4:24,3 | efecto | Líneas de concentración: retirar |
| 4:24,3 | efecto | Voz: Ninguno, +0,0 dB |
| 4:28,4 | **@hechizo** |  |
| 4:28,4 | plano 54 | primer plano de archimago · 1,0 s · lente 26° · coste 18: ángulo 18 |
| 4:28,4 | efecto | Mezcla: plegaria (quitar) |
| 4:28,4 | efecto | Mezcla: grito |
| 4:28,4 | efecto | Líneas de concentración: mostrar |
| 4:28,4 | efecto | Respiro: 0,9 s (música 0 dB) |
| 4:29,4 | plano 55 | general de archimago y mago · 2,1 s · lente 44° |
| 4:29,4 | archimago | «¡¡Protégelos a todos!!» (2,1 s) |
| 4:29,4 | efecto | Voz: Conjuro, +6,0 dB |
| 4:29,6 | efecto | Rayo: 2 × fondo del plano |
| 4:29,7 | efecto | SFX: Prologo_ProteccionAbsoluta |
| 4:29,7 | efecto | Escalar: CARGA_ESFERA ×2,8 en 0,6 s |
| 4:29,7 | efecto | Sacudida de cámara (0,45, 2s) |
| 4:30,2 | efecto | Fusión: BOLA_PROTECTORA + CARGA_ESFERA |
| 4:30,2 | efecto | SFX: SFX_Prologo_LanzamientoConjuro |
| 4:31,5 | **@choque_final** |  |
| 4:31,5 | plano 56 | general de archimago y mago · 1,3 s · lente 58° |
| 4:31,5 | efecto | Líneas de concentración: mostrar |
| 4:31,5 | efecto | VFX: Forcejeo en encuentro_hechizos |
| 4:31,5 | efecto | Fogonazo (0,6s) |
| 4:31,5 | efecto | Ambiente: LUCHA_FINAL ← SFX_Prologo_LuchaFinal |
| 4:31,5 | efecto | SFX: SFX_Prologo_Plin |
| 4:31,5 | efecto | VFX: Choque en encuentro_hechizos |
| 4:31,7 | efecto | Sacudida de cámara (0,5, 2,2s) |
| 4:32,5 | efecto | SFX: SFX_Prologo_PreviaEstallido |
| 4:32,8 | plano 57 | general de archimago y mago · 6,1 s · lente 52° |
| 4:33,9 | **@estallido** |  |
| 4:33,9 | efecto | Líneas de concentración: retirar |
| 4:33,9 | efecto | Ambiente: parar LUCHA_FINAL |
| 4:33,9 | efecto | VFX: recoger efectos registrados |
| 4:33,9 | efecto | SFX: Prologo_Choque |
| 4:33,9 | efecto | Ambiente: parar AGUJERO_NEGRO |
| 4:33,9 | efecto | Sacudida de cámara (1, 1,8s) |
| 4:33,9 | efecto | Fogonazo (0,35s) |
| 4:33,9 | efecto | VFX: EstallidoFinal en choque |
| 4:33,9 | efecto | VFX: Choque en encuentro_hechizos |
| 4:33,9 | efecto | SFX: SFX_Prologo_EstallidoSube |
| 4:33,9 | efecto | SFX: Prologue_WarClashStinger_A |
| 4:34,3 | efecto | Pantalla: cubrir (3,5s) |
| 4:38,0 | **@explosion** |  |
| 4:38,0 | efecto | VFX: recoger efectos registrados |
| 4:38,0 | efecto | Mezcla: grito (quitar) |
| 4:38,0 | efecto | Ambiente: parar TORMENTA |
| 4:38,0 | efecto | Música: parar (2s) |
| 4:38,0 | efecto | Rótulo: CAP1_ROTULO_SIGLOS_DESPUES |
| 4:38,2 | efecto | Hora del día: Morning (de golpe) (y es con la que se queda el mundo) |
| 4:38,4 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### archimago (NPC_Archimago)

- 1:03,1–1:06,8 habla
- 1:03,2 gesto Talk01
- 1:07,5 gesto HeadNod01
- 1:08,6 gesto HeadNod01
- 1:16,1–1:19,6 anda de (-166,8, -362,7) a (-163,6, -364,4)
- 1:20,0 gesto CastingEnter01
- 1:20,5 bucle CastingIdle01
- 1:23,8 reposo 
- 1:26,6 gesto HeadNod01
- 1:27,3 gesto Sobresalto01
- 1:31,4 gesto HeadNod01
- 1:35,1 bucle CastingIdle01
- 1:35,1–1:37,7 habla
- 1:37,7 reposo 
- 1:40,9 gesto Laugh01
- 1:41,0 gesto HeadNod01
- 1:45,0 gesto HeadNod01
- 1:47,9 gesto HeadNod01
- 1:52,3–1:57,9 anda de (-153,1, -362,3) a (-152,6, -366,7)
- 1:57,9–2:03,6 habla
- 1:57,9 gesto Talk02
- 2:04,3 gesto Question01
- 2:09,9 gesto Idle03
- 2:28,2 gesto Question02
- 2:29,7–2:32,9 habla
- 2:29,8 gesto HeadShake01
- 2:31,5 gesto Talk01
- 2:32,9–2:35,5 anda de (-152,6, -366,7) a (-155,6, -364,7)
- 2:35,4–2:38,0 habla
- 2:35,5 gesto Challenging_NoWeapon
- 2:40,1 gesto Question01
- 2:40,9–2:43,2 habla
- 2:41,0 gesto HeadNod01
- 2:43,9 gesto HeadNod01
- 2:46,7 gesto HeadNod01
- 2:50,7 gesto HeadNod01
- 2:54,6 gesto Sobresalto01
- 2:57,5–3:06,5 anda de (-155,6, -364,7) a (-165,6, -365,2)
- 3:09,6 gesto Question01
- 3:15,0–3:18,3 habla
- 3:15,0 gesto Angry02
- 3:16,6 gesto Talk03
- 3:18,3 gesto Decidido01
- 3:21,0 gesto MagicLeft
- 3:22,0 gesto Laugh01
- 3:24,5 bucle Idle_Battle_NoWeapon
- 3:25,3 gesto Defend_NoWeapon
- 3:25,9 bucle Idle_Battle_NoWeapon
- 3:26,3 gesto DefendHit_NoWeapon
- 3:27,3 gesto TakeDamage
- 3:28,2 gesto TakeDamage_2
- 3:31,1 gesto MagicSpecial
- 3:36,1 bucle Pain01
- 3:37,7 gesto HeadShake01
- 3:43,7 bucle CastingIdle01
- 3:43,7 reposo 
- 3:43,7–3:51,0 habla
- 3:53,8 bucle Beg01_Loop
- 3:53,8–4:02,0 habla
- 4:02,0–4:10,6 habla
- 4:11,8 bucle Conjuro_Invocar
- 4:11,8–4:18,1 habla
- 4:18,1–4:24,3 habla
- 4:24,3–4:28,1 habla
- 4:29,4–4:31,5 habla
- 4:30,0 bucle Conjuro_Lanzar
- 4:33,9 reposo 
- 4:33,9 bucle Die01_NoWeapon

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
- 1:00,7 gesto Question01
- 1:03,8 gesto Question01
- 1:04,0 gesto HeadShake01
- 1:07,3 gesto Talk01
- 1:11,6 gesto HeadNod01
- 1:16,4 gesto Talk02
- 1:21,4 gesto Laugh01
- 1:26,1 gesto Talk03
- 1:29,6 gesto Question01
- 1:33,5 gesto HeadShake01
- 1:35,8 gesto HeadNod01
- 1:37,2 gesto Talk01
- 1:41,7 gesto HeadNod01
- 1:43,9 gesto HeadNod01
- 1:45,3 gesto Talk02
- 1:49,6 gesto Laugh01
- 1:51,3 reposo 
- 1:52,3–1:57,9 anda de (-154,2, -362,7) a (-153,9, -367,1)
- 1:52,9–1:57,2 habla
- 1:58,6 gesto HeadNod01
- 2:03,6–2:08,6 habla
- 2:03,7 gesto Talk01
- 2:06,2 gesto Talk03
- 2:11,3 gesto SenseSomethingStart_NoWeapon
- 2:27,5–2:29,7 habla
- 2:27,6 gesto Question01
- 2:30,4 gesto HeadShake01
- 2:36,1 gesto HeadNod01
- 2:39,6–2:40,9 habla
- 2:39,7 gesto Question02
- 2:39,7–2:40,5 anda de (-154,0, -367,1) a (-155,0, -367,2)
- 2:43,2 bucle ManoAlPecho
- 2:43,2–2:45,2 habla
- 2:45,3–2:50,7 habla
- 2:45,4 gesto Talk03
- 2:48,1 bucle ManoAlPecho
- 2:53,9–2:57,5 habla
- 2:54,0 gesto Talk01
- 2:56,0 bucle ManoAlPecho
- 2:57,5–3:09,5 anda de (-155,0, -367,1) a (-128,0, -365,0)

### mago (NPC_MagoOscuro)

- 2:17,2 bucle fly_idle
- 2:17,2 aparece
- 2:22,8 reposo 
- 2:22,9 gesto Challenging_NoWeapon
- 2:23,0 reposo 
- 2:24,1–2:51,1 anda de (-212,2, -368,1) a (-182,0, -364,5)
- 2:57,5–3:09,0 anda de (-182,0, -364,5) a (-169,2, -365,2)
- 3:08,9–3:11,9 habla
- 3:09,0 gesto Talk02
- 3:11,9 bucle ManosALaEspalda
- 3:11,9–3:15,0 habla
- 3:15,0 reposo 
- 3:18,3 gesto Laugh_Risita
- 3:19,7 gesto MagicRight
- 3:22,0 gesto Angry01
- 3:25,3 gesto MagicRight
- 3:26,3 gesto MagicLeft
- 3:27,2 gesto MagicRight
- 3:28,2 gesto Laugh_Carcajada
- 3:31,1 gesto MagicSpecial
- 3:35,0 gesto MagicRight
- 3:37,0–3:40,3 habla
- 3:37,1 gesto HeadShake01
- 3:38,8 gesto Talk01
- 3:40,3 bucle JumpAir_InPlace_NoWeapon
- 3:40,3 bucle FoundSomething_NoWeapon
- 4:30,0 bucle Conjuro_Lanzar
- 4:33,9 reposo 
- 4:33,9 bucle Die01_NoWeapon

### nina (NPC_Aldeano_05)

- 0:00,0 bucle SenseSomethingSearching_NoWeapon
- 1:20,4 reposo 
- 1:20,6–1:27,8 anda de (-179,0, -364,0) a (-165,1, -366,2)
- 1:30,7–1:35,1 habla
- 1:30,8 gesto Beg01
- 1:35,8 gesto HeadNod01
- 1:40,3–1:42,9 habla
- 1:40,3 gesto Cheer01
- 1:43,9 gesto HeadNod01
- 1:48,0–1:53,1 anda de (-165,2, -366,2) a (-174,8, -364,0)
- 1:52,9 bucle InteractWithPeople_NoWeapon
- 1:55,9 gesto Talk01
- 1:59,1 gesto HeadNod01
- 2:02,4 gesto Talk02
- 2:05,2 gesto Laugh01
- 2:08,1 gesto Talk03
- 2:11,1 reposo 
- 2:12,7 gesto SenseSomethingStart_NoWeapon
- 2:16,5 gesto Idle03
- 2:36,1–2:46,9 anda de (-174,7, -364,1) a (-154,0, -366,8)
- 2:39,5 gesto SenseSomethingStart_NoWeapon
- 2:48,2 gesto Idle03
- 2:54,6 gesto Sobresalto01
- 2:58,3–3:08,0 anda de (-154,0, -366,7) a (-143,4, -357,5)

### v01 (NPC_Aldeano_01)

- 0:00,6–0:01,5 anda de (-174,5, -366,4) a (-173,0, -366,6)
- 0:01,9–0:03,4 anda de (-173,0, -366,6) a (-173,4, -364,0)
- 0:03,8–0:04,9 anda de (-173,4, -364,0) a (-175,2, -363,7)
- 0:05,3–0:06,9 anda de (-175,2, -363,8) a (-174,5, -366,4)
- 0:07,3–0:08,1 anda de (-174,4, -366,4) a (-173,0, -366,6)
- 0:08,5–0:10,0 anda de (-173,0, -366,6) a (-173,4, -364,0)
- 0:10,4–0:11,5 anda de (-173,4, -364,0) a (-175,2, -363,7)
- 0:11,9–0:13,5 anda de (-175,2, -363,8) a (-174,5, -366,4)
- 0:13,9–0:14,7 anda de (-174,4, -366,4) a (-173,0, -366,6)
- 0:15,1–0:16,6 anda de (-173,0, -366,6) a (-173,4, -364,0)
- 0:17,0–0:18,1 anda de (-173,4, -364,0) a (-175,2, -363,7)
- 0:18,5–0:20,1 anda de (-175,2, -363,8) a (-174,5, -366,4)
- 0:20,5–0:21,3 anda de (-174,4, -366,4) a (-173,0, -366,6)
- 0:21,7–0:23,2 anda de (-173,0, -366,6) a (-173,4, -364,0)
- 0:23,6–0:24,7 anda de (-173,4, -364,0) a (-175,2, -363,7)
- 0:25,1–0:26,7 anda de (-175,2, -363,8) a (-174,5, -366,4)
- 0:27,1–0:27,9 anda de (-174,4, -366,4) a (-173,0, -366,6)
- 0:28,3–0:29,8 anda de (-173,0, -366,6) a (-173,4, -364,0)
- 0:30,2–0:31,3 anda de (-173,4, -364,0) a (-175,2, -363,7)
- 0:31,7–0:33,3 anda de (-175,2, -363,8) a (-174,5, -366,4)
- 0:33,7–0:34,5 anda de (-174,4, -366,4) a (-173,0, -366,6)
- 0:34,9–0:36,4 anda de (-173,0, -366,6) a (-173,4, -364,0)
- 0:36,8–0:37,9 anda de (-173,4, -364,0) a (-175,2, -363,7)
- 0:38,3–0:39,9 anda de (-175,2, -363,8) a (-174,5, -366,4)
- 0:40,3–0:41,1 anda de (-174,4, -366,4) a (-173,0, -366,6)
- 0:41,5–0:43,0 anda de (-173,0, -366,6) a (-173,4, -364,0)
- 0:43,4–0:44,5 anda de (-173,4, -364,0) a (-175,2, -363,7)
- 0:44,9–0:46,5 anda de (-175,2, -363,8) a (-174,5, -366,4)
- 0:46,9–0:47,8 anda de (-174,4, -366,4) a (-173,0, -366,6)
- 0:48,2–0:49,6 anda de (-173,0, -366,5) a (-173,4, -364,0)
- 0:50,0–0:51,1 anda de (-173,4, -364,0) a (-175,2, -363,7)
- 0:51,5–0:53,1 anda de (-175,2, -363,8) a (-174,5, -366,4)
- 0:53,5–0:54,4 anda de (-174,4, -366,4) a (-173,0, -366,6)
- 0:54,8–0:56,2 anda de (-173,0, -366,5) a (-173,4, -364,0)
- 0:56,6–0:57,7 anda de (-173,4, -364,0) a (-175,2, -363,7)
- 0:58,1–0:59,7 anda de (-175,2, -363,8) a (-174,5, -366,4)
- 1:00,1–1:01,0 anda de (-174,4, -366,4) a (-173,0, -366,6)
- 1:01,4–1:02,8 anda de (-173,0, -366,5) a (-173,4, -364,0)
- 1:03,2–1:04,3 anda de (-173,4, -364,0) a (-175,2, -363,7)
- 1:03,8 gesto Question01
- 1:04,7–1:06,3 anda de (-175,2, -363,8) a (-174,5, -366,4)
- 1:06,7–1:07,6 anda de (-174,4, -366,4) a (-173,0, -366,6)
- 1:07,7 reposo 
- 1:08,3–1:10,2 anda de (-172,9, -366,5) a (-171,4, -363,4)
- 1:11,1–1:13,6 anda de (-171,5, -363,4) a (-174,5, -366,4)
- 1:14,4–1:15,6 anda de (-174,4, -366,4) a (-172,7, -367,4)
- 1:16,4–1:17,6 anda de (-172,8, -367,4) a (-174,5, -366,4)
- 1:18,4–1:20,0 anda de (-174,5, -366,3) a (-175,2, -363,7)
- 1:20,8–1:22,3 anda de (-175,2, -363,8) a (-174,5, -366,4)
- 1:23,1–1:24,3 anda de (-174,5, -366,4) a (-172,7, -367,4)
- 1:25,1–1:26,3 anda de (-172,7, -367,4) a (-174,5, -366,4)
- 1:27,1–1:28,7 anda de (-174,5, -366,4) a (-175,2, -363,7)
- 1:29,5–1:31,1 anda de (-175,2, -363,8) a (-174,5, -366,4)
- 1:31,9–1:33,1 anda de (-174,4, -366,5) a (-172,7, -367,4)
- 1:33,9–1:35,1 anda de (-172,8, -367,4) a (-174,5, -366,4)
- 1:35,8 gesto HeadNod01
- 1:35,9–1:37,4 anda de (-174,5, -366,3) a (-175,2, -363,7)
- 1:37,7–1:39,2 anda de (-175,2, -363,7) a (-176,6, -365,8)
- 1:43,9 gesto HeadNod01
- 1:48,1 gesto Cheer02
- 1:49,5–1:51,6 anda de (-176,5, -365,8) a (-173,0, -366,6)
- 1:52,1–1:52,6 anda de (-172,9, -366,7) a (-172,7, -367,4)
- 1:53,1–1:53,5 anda de (-172,8, -367,2) a (-173,0, -366,6)
- 1:54,0–1:54,9 anda de (-173,0, -366,6) a (-174,5, -366,4)
- 1:55,4–1:56,3 anda de (-174,5, -366,4) a (-173,0, -366,6)
- 1:56,8–1:57,3 anda de (-173,0, -366,7) a (-172,7, -367,4)
- 1:57,8–1:58,3 anda de (-172,7, -367,3) a (-173,0, -366,6)
- 1:58,8–1:59,7 anda de (-173,1, -366,6) a (-174,5, -366,4)
- 2:00,2–2:01,1 anda de (-174,4, -366,4) a (-173,0, -366,6)
- 2:01,6–2:02,0 anda de (-172,9, -366,8) a (-172,7, -367,4)
- 2:02,5–2:03,0 anda de (-172,7, -367,4) a (-173,0, -366,6)
- 2:03,5–2:04,4 anda de (-173,0, -366,6) a (-174,5, -366,4)
- 2:04,9–2:05,8 anda de (-174,5, -366,4) a (-173,0, -366,6)
- 2:06,3–2:06,8 anda de (-173,0, -366,7) a (-172,7, -367,4)
- 2:07,3–2:07,8 anda de (-172,7, -367,3) a (-173,0, -366,6)
- 2:08,3–2:09,2 anda de (-173,1, -366,6) a (-174,5, -366,4)
- 2:09,7–2:10,5 anda de (-174,4, -366,4) a (-173,0, -366,6)
- 2:11,0–2:11,2 anda de (-173,0, -366,6) a (-172,9, -366,8)
- 2:11,1 reposo 
- 2:12,7 gesto SenseSomethingStart_NoWeapon
- 2:12,8–2:14,4 anda de (-172,9, -366,7) a (-173,1, -364,0)
- 2:16,5 gesto Idle03
- 2:37,1–2:56,7 anda de (-172,9, -364,0) a (-130,0, -374,5)

### v02 (NPC_Aldeano_02)

- 0:00,4 bucle InteractWithPeople_NoWeapon
- 0:04,1 gesto Talk02
- 0:08,3 gesto Laugh01
- 0:12,5 gesto Talk03
- 0:16,0 gesto Question01
- 0:20,4 gesto HeadShake01
- 0:24,5 gesto Talk01
- 0:27,7 gesto HeadNod01
- 0:31,6 gesto Talk02
- 0:34,3 gesto Laugh01
- 0:38,5 gesto Talk03
- 0:41,2 gesto Question01
- 0:45,1 gesto HeadShake01
- 0:49,0 gesto Talk01
- 0:53,3 gesto HeadNod01
- 0:56,9 gesto Talk02
- 0:59,6 gesto Laugh01
- 1:03,6 gesto HandWave02
- 1:03,8 gesto Question01
- 1:07,5 reposo 
- 1:08,5 gesto HandWave02
- 1:35,8 gesto HeadNod01
- 1:43,9 gesto HeadNod01
- 1:49,4 bucle InteractWithPeople_NoWeapon
- 1:52,2 gesto Laugh01
- 1:56,2 gesto Talk03
- 1:59,0 gesto Question01
- 2:02,9 gesto HeadShake01
- 2:05,7 gesto Talk01
- 2:09,6 gesto HeadNod01
- 2:11,2 reposo 
- 2:11,3 gesto SenseSomethingStart_NoWeapon
- 2:12,8 gesto SenseSomethingStart_NoWeapon
- 2:17,7 bucle Fear01
- 2:36,9–2:57,1 anda de (-173,7, -363,1) a (-126,0, -361,5)
- 3:09,4 bucle Beg01_Loop

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
- 0:47,7 gesto HeadNod01
- 0:50,1 gesto Talk02
- 0:53,8 gesto Laugh01
- 0:56,1 gesto Talk03
- 0:59,0 gesto Question01
- 1:01,9 gesto HeadShake01
- 1:03,8 gesto Question01
- 1:05,6 gesto Talk01
- 1:08,1 gesto HeadNod01
- 1:10,9 gesto Talk02
- 1:14,5 gesto Laugh01
- 1:18,5 gesto Talk03
- 1:20,9 gesto Question01
- 1:24,4 bucle InteractWithPeople_NoWeapon
- 1:27,4 gesto HeadShake01
- 1:31,1 gesto Talk01
- 1:33,7 reposo 
- 1:33,7–1:36,6 anda de (-169,7, -360,1) a (-168,7, -364,1)
- 1:35,8 gesto HeadNod01
- 1:41,5 gesto HeadNod01
- 1:43,2–1:45,3 habla
- 1:43,3 gesto FoundSomething_NoWeapon
- 1:46,3 gesto HeadNod01
- 1:47,9 bucle InteractWithPeople_NoWeapon
- 1:49,2 gesto Talk03
- 1:52,9 gesto Question01
- 1:55,3 gesto HeadShake01
- 1:57,6 gesto Talk01
- 2:00,3 gesto HeadNod01
- 2:04,2 gesto Talk02
- 2:06,6 gesto Laugh01
- 2:11,0 reposo 
- 2:12,0 gesto FoundSomething_NoWeapon
- 2:17,8 gesto Fear01
- 2:36,4–2:55,3 anda de (-168,6, -364,0) a (-124,0, -366,0)
- 3:41,0 bucle Reverence01_Loop

### v04 (NPC_Aldeano_04)

- 0:00,0 bucle Idle_Normal_NoWeapon
- 0:00,8 reposo 
- 0:00,8–0:03,7 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 0:05,5–0:08,4 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 0:10,2–0:13,1 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 0:14,9–0:17,8 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 0:19,6–0:22,5 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 0:24,3–0:27,1 anda de (-170,2, -369,2) a (-172,2, -369,2)
- 0:28,9–0:31,8 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 0:33,6–0:36,5 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 0:38,3–0:41,2 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 0:43,0–0:45,9 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 0:47,7–0:50,5 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 0:52,3–0:55,2 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 0:57,0–0:59,9 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 1:01,7–1:04,6 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 1:06,4–1:09,3 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 1:11,1–1:13,9 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 1:15,7–1:18,6 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 1:20,0 reposo 
- 1:20,4–1:23,3 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 1:23,9 gesto HandClap01
- 1:25,1–1:28,0 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 1:29,8–1:32,7 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 1:34,5–1:37,3 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 1:37,7–1:40,6 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 1:42,4–1:45,3 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 1:46,3 gesto HeadNod01
- 1:47,1–1:50,0 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 1:51,8–1:54,6 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 1:56,4–1:59,3 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 2:01,1–2:04,0 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 2:05,8–2:08,7 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 2:10,5–2:13,4 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 2:11,0 reposo 
- 2:15,2–2:18,0 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 2:19,8–2:22,7 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 2:24,5–2:27,4 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 2:29,2–2:32,1 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 2:33,9–2:54,2 anda de (-170,1, -369,2) a (-128,4, -368,4)

### v06 (NPC_Aldeano_06)

- 1:03,8 gesto Question01
- 1:10,4–1:16,1 habla
- 1:10,5 gesto Question01
- 1:13,6 gesto Shrug01
- 1:20,2 bucle Idle_Normal_NoWeapon
- 1:20,7 gesto FoundSomething_NoWeapon
- 1:23,7 gesto Cheer01
- 1:24,2 reposo 
- 1:24,3–1:26,6 anda de (-160,0, -364,6) a (-162,3, -364,1)
- 1:26,6–1:30,7 habla
- 1:26,7 gesto HeadNod01
- 1:28,0 gesto Talk01
- 1:35,8 gesto HeadNod01
- 1:41,5 gesto HeadNod01
- 1:43,9 gesto HeadNod01
- 1:48,0–1:53,7 anda de (-162,3, -364,1) a (-159,4, -367,6)
- 1:53,9 bucle Opening01_Loop
- 1:58,6 gesto HeadNod01
- 2:04,3 gesto Question01
- 2:11,3 reposo 
- 2:12,7 gesto SenseSomethingStart_NoWeapon
- 2:28,2 gesto Question02
- 2:30,4 gesto HeadShake01
- 2:35,5–2:47,7 anda de (-159,2, -367,5) a (-131,0, -364,0)
- 2:36,1 gesto HeadNod01

### v07 (NPC_Aldeano_07)

- 0:01,6–0:04,5 anda de (-172,0, -364,8) a (-171,4, -366,8)
- 0:05,3–0:08,2 anda de (-171,4, -366,8) a (-172,0, -364,8)
- 0:09,0–0:11,8 anda de (-172,0, -364,8) a (-171,4, -366,8)
- 0:12,6–0:15,5 anda de (-171,4, -366,8) a (-172,0, -364,8)
- 0:16,3–0:19,2 anda de (-172,0, -364,8) a (-171,4, -366,8)
- 0:20,0–0:22,8 anda de (-171,4, -366,8) a (-172,0, -364,8)
- 0:23,6–0:26,5 anda de (-172,0, -364,8) a (-171,4, -366,8)
- 0:27,3–0:30,2 anda de (-171,4, -366,8) a (-172,0, -364,8)
- 0:31,0–0:33,8 anda de (-172,0, -364,8) a (-171,4, -366,8)
- 0:34,6–0:37,5 anda de (-171,4, -366,8) a (-172,0, -364,8)
- 0:38,3–0:41,2 anda de (-172,0, -364,8) a (-171,4, -366,8)
- 0:42,0–0:44,8 anda de (-171,4, -366,8) a (-172,0, -364,8)
- 0:45,6–0:48,5 anda de (-172,0, -364,8) a (-171,4, -366,8)
- 0:49,3–0:52,2 anda de (-171,4, -366,8) a (-172,0, -364,8)
- 0:53,0–0:55,8 anda de (-172,0, -364,8) a (-171,4, -366,8)
- 0:56,6–0:59,5 anda de (-171,4, -366,8) a (-172,0, -364,8)
- 1:00,3–1:03,1 anda de (-172,0, -364,8) a (-171,4, -366,8)
- 1:03,8 gesto Question01
- 1:03,9–1:06,8 anda de (-171,4, -366,8) a (-172,0, -364,8)
- 1:07,6–1:10,5 anda de (-172,0, -364,8) a (-171,4, -366,8)
- 1:08,1 reposo 
- 1:09,1 gesto HandWave01
- 1:11,3–1:14,2 anda de (-171,4, -366,8) a (-172,0, -364,8)
- 1:15,0–1:17,9 anda de (-172,0, -364,8) a (-171,4, -366,8)
- 1:18,7–1:21,6 anda de (-171,4, -366,8) a (-172,0, -364,8)
- 1:22,4–1:25,2 anda de (-172,0, -364,8) a (-171,4, -366,8)
- 1:26,0–1:28,9 anda de (-171,4, -366,8) a (-172,0, -364,8)
- 1:29,7–1:32,5 anda de (-172,0, -364,8) a (-171,4, -366,8)
- 1:33,3–1:36,2 anda de (-171,4, -366,8) a (-172,0, -364,8)
- 1:37,0–1:39,9 anda de (-172,0, -364,8) a (-175,1, -366,6)
- 1:41,0 gesto HeadNod01
- 1:50,4–1:55,0 anda de (-175,1, -366,6) a (-172,0, -364,8)
- 1:55,9–1:58,8 anda de (-172,0, -364,8) a (-171,4, -366,8)
- 2:01,3–2:07,4 anda de (-171,4, -366,8) a (-175,1, -366,6)
- 2:09,5–2:12,6 anda de (-175,1, -366,6) a (-172,0, -364,8)
- 2:13,9 gesto Idle03
- 2:38,3–2:58,7 anda de (-171,8, -364,8) a (-125,0, -357,0)

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
- 0:50,9 gesto Laugh01
- 0:53,6 gesto Talk03
- 0:56,1 gesto Question01
- 1:00,1 gesto HeadShake01
- 1:03,8 gesto Question01
- 1:04,2 gesto Talk01
- 1:07,1 gesto HeadNod01
- 1:10,7 gesto Talk02
- 1:14,5 gesto Laugh01
- 1:17,6 gesto Talk03
- 1:21,6 gesto Question01
- 1:24,8 gesto HeadShake01
- 1:29,7 reposo 
- 1:29,7–1:35,9 anda de (-158,6, -362,6) a (-167,7, -364,5)
- 1:35,8 gesto HeadNod01
- 1:41,5 gesto HeadNod01
- 1:43,9 gesto HeadNod01
- 1:45,6–1:47,9 habla
- 1:45,7 gesto Cheer02
- 1:48,4 bucle InteractWithPeople_NoWeapon
- 1:50,3 gesto Laugh01
- 1:52,9 gesto Talk03
- 1:56,5 gesto Question01
- 1:60,0 gesto HeadShake01
- 2:03,6 gesto Talk01
- 2:07,2 gesto HeadNod01
- 2:10,1 gesto Talk02
- 2:11,3 reposo 
- 2:12,8 gesto SenseSomethingStart_NoWeapon
- 2:16,5 gesto Idle03
- 2:36,6–2:52,6 anda de (-167,5, -364,5) a (-132,0, -372,0)
- 3:09,8 bucle Fear01

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
- 1:03,8 gesto Question01
- 1:04,0 gesto HeadShake01
- 1:07,2 gesto Talk01
- 1:10,6 gesto HeadNod01
- 1:14,4 gesto Talk02
- 1:17,9 gesto Laugh01
- 1:23,8 gesto HandClap01
- 1:25,0 bucle InteractWithPeople_NoWeapon
- 1:29,0 gesto Question01
- 1:31,9 gesto HeadShake01
- 1:34,8 gesto Talk01
- 1:35,8 gesto HeadNod01
- 1:39,2 gesto HeadNod01
- 1:43,6 gesto Talk02
- 1:43,9 gesto HeadNod01
- 1:48,1 gesto Laugh01
- 1:52,6 gesto Talk03
- 1:56,8 gesto Question01
- 2:00,6 gesto HeadShake01
- 2:04,1 gesto Talk01
- 2:08,4 gesto HeadNod01
- 2:11,2 reposo 
- 2:12,9 gesto SenseSomethingStart_NoWeapon
- 2:18,1 gesto Fear01
- 2:36,3–2:54,6 anda de (-170,9, -359,4) a (-129,0, -359,0)
- 3:10,1 bucle Fear01

### v10 (NPC_Aldeano_10)

- 0:00,0 bucle Opening01_Loop
- 1:03,8 gesto Question01
- 1:05,3 gesto HeadNod01
- 1:38,3 reposo 
- 1:41,0 gesto HeadNod01
- 1:48,2 gesto HandClap01
- 2:18,0 gesto FoundSomething_NoWeapon
- 2:37,2–3:00,0 anda de (-175,8, -362,2) a (-122,0, -367,5)
- 3:42,0 bucle Beg01_Loop

### panadera (PanaderaDelValle)

- 1:04,6 bucle InteractWithPeople_NoWeapon
- 1:05,1 gesto HeadNod01
- 1:13,4 gesto HeadNod01
- 1:23,9 gesto HandClap01
- 2:17,9 gesto Fear01
- 2:36,2 reposo 
- 2:36,3–3:04,2 anda de (-183,0, -368,5) a (-118,0, -371,5)

### madre (MadreDelValle)

- 0:00,0 desaparece
- 1:03,5 aparece
- 1:03,5 gesto AbrirPuerta
- 1:04,7–1:06,0 anda de (-178,2, -370,2) a (-178,1, -369,3)
- 1:08,6–1:10,4 habla
- 1:08,7 gesto HandWave02
- 1:12,5–1:19,7 anda de (-178,1, -369,2) a (-183,1, -366,3)
- 1:20,2 bucle InteractWithPeople_NoWeapon
- 1:21,3 gesto Laugh01
- 1:23,6 reposo 
- 1:23,8–1:31,1 anda de (-183,1, -366,3) a (-178,1, -369,3)
- 1:43,9 gesto HeadNod01
- 1:48,3 gesto HeadNod01
- 2:13,2–2:13,9 anda de (-178,1, -369,4) a (-178,2, -370,2)
- 2:14,4 desaparece
- 2:37,8 aparece
- 2:38,8–3:03,9 anda de (-178,0, -370,2) a (-120,5, -356,5)

### lucas (LucasDelValle)

- 0:00,0 desaparece
- 1:10,7 aparece
- 1:10,9–1:11,3 anda de (-178,2, -370,2) a (-178,2, -369,8)
- 1:12,5–1:19,8 anda de (-177,8, -369,6) a (-182,8, -365,8)
- 1:23,8–1:31,1 anda de (-183,3, -365,8) a (-178,4, -369,7)
- 1:43,9 gesto HeadNod01
- 2:12,1–2:12,4 anda de (-178,4, -369,8) a (-178,2, -370,2)
- 2:13,0 desaparece
- 2:37,8 aparece
- 2:37,9–2:38,3 anda de (-178,2, -370,2) a (-178,8, -370,2)
- 2:38,7–3:03,9 anda de (-178,7, -370,1) a (-120,7, -356,0)
- 3:12,6 gesto HeadNod01
- 3:37,7 gesto HeadShake01

### vm (VecinoDelMirador)

- 1:03,8 gesto Question01
- 1:06,8–1:08,6 habla
- 1:06,8 bucle Asomado
- 1:06,8 gesto HandWave01
- 1:07,4 gesto HandWave01
- 1:09,7 bucle Asomado
- 1:23,9 gesto HandClap01
- 1:26,1 bucle Asomado
- 2:11,7 gesto Fear01
- 2:13,1 bucle Asomado
- 2:13,8 desaparece

### ab (AbuelaDelMirador)

- 1:05,3 gesto HandWave02
- 1:07,8 bucle Asomado
- 1:41,3 gesto HandClap01
- 1:43,4 bucle Asomado
- 2:13,0 desaparece

### vc (VecinaDelMirador)

- 1:03,8 gesto Question01
- 1:11,3 bucle Asomado
- 1:24,1 gesto HandClap01
- 1:26,3 bucle Asomado
- 1:40,7 gesto HandWave01
- 1:42,9 bucle Asomado
- 1:48,7 gesto HeadNod01
- 1:49,7 bucle Asomado
- 1:52,7 gesto HandWave02
- 1:55,5 bucle Asomado
- 2:13,4 desaparece

