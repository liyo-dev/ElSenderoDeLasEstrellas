# Parte de horneado: Prologo_UltimaNoche

2026-11-24 09:33 · guion `Assets/_SEQUENCES/Guiones/Prologo_UltimaNoche.guion.txt`

**4:30,8** de secuencia · 32 frases · 56 planos · 203 efectos · 19 personajes

Reparto medido (m): archimago 1,36/ojos 0,94 · liora 1,35/ojos 0,94 · mago 1,81/ojos 0,98 · nina 1,16/ojos 0,63 · v01 1,31/ojos 0,98 · v02 1,58/ojos 0,98 · v03 1,51/ojos 0,98 · v04 1,68/ojos 0,98 · v06 1,64/ojos 0,98 · v07 1,30/ojos 0,98 · v08 1,58/ojos 0,98 · v09 1,46/ojos 0,97 · v10 1,58/ojos 0,97 · panadera 1,55/ojos 0,95 · madre 1,44/ojos 0,94 · lucas 1,23/ojos 0,63 · vm 1,58/ojos 0,94 · ab 1,55/ojos 0,95 · vc 1,51/ojos 0,94

## Errores (0)


## Avisos (5)

- línea 660: mago anda a ladera, entrada ritmo=normal llega_mirando=centro_plaza sin_esperar: un punto queda fuera del NavMesh (-202,0, -365,0); voy en línea recta
- El primer plano empieza en 63,1 s: hasta entonces la cámara se queda donde estuviera.
- línea 711: plano conjunto liora nina altura=1.0 altura_foco=0.9 lente=40: liora habla de espaldas a la cámara en 174,9 s (157°). Prueba lado=, otro desde= o un plano nuevo cuando se gire.
- 2:11,4: madre y lucas se atraviesan.
- 3:01,6: v07 y lucas se atraviesan.

## Silencios largos entre frases (11)

- 1:16,4 → 1:26,9: 10,5 s sin voz entre PROLOGO_FAVOR_CARRETA y PROLOGO_CARRETA_GRACIAS
- 1:38,0 → 1:40,6: 2,6 s sin voz entre PROLOGO_MANANA_GLOBO_OK y PROLOGO_GLOBO_VECINO_1
- 1:48,2 → 1:52,1: 3,8 s sin voz entre PROLOGO_GLOBO_VECINO_3 y PROLOGO_PLAZA_LIORA_1
- 2:07,8 → 2:26,7: 18,9 s sin voz entre PROLOGO_PLAZA_LIORA_2 y PROLOGO_DESPEDIDA_LIORA_1
- 2:32,1 → 2:34,6: 2,6 s sin voz entre PROLOGO_DESPEDIDA_ARCHIMAGO_1 y PROLOGO_HORA_ARCHIMAGO
- 2:37,2 → 2:38,8: 1,6 s sin voz entre PROLOGO_HORA_ARCHIMAGO y PROLOGO_DESPEDIDA_LIORA_2
- 2:49,9 → 2:53,1: 3,1 s sin voz entre PROLOGO_DESPEDIDA_ELEGIR y PROLOGO_EVACUACION_LIORA
- 2:56,7 → 3:08,1: 11,5 s sin voz entre PROLOGO_EVACUACION_LIORA y PROLOGO_DUELO_MAGO
- 3:17,5 → 3:28,9: 11,4 s sin voz entre PROLOGO_DUELO_ARCHIMAGO y PROLOGO_MAGO_VALLE
- 3:32,2 → 3:35,6: 3,4 s sin voz entre PROLOGO_MAGO_VALLE y PROLOGO_PLEGARIA_01
- 3:42,9 → 3:45,7: 2,8 s sin voz entre PROLOGO_PLEGARIA_01 y PROLOGO_PLEGARIA_02

## Cinemática viva

Vida de fondo puesta sola (nadie quieto en cuadro):

- archimago: 3 micro-acción(es) de reposo en los huecos en cuadro.
- nina: 2 micro-acción(es) de reposo en los huecos en cuadro.
- v01: 1 micro-acción(es) de reposo en los huecos en cuadro.
- v03: 1 micro-acción(es) de reposo en los huecos en cuadro.
- v06: 1 micro-acción(es) de reposo en los huecos en cuadro.
- v07: 1 micro-acción(es) de reposo en los huecos en cuadro.
- v08: 2 micro-acción(es) de reposo en los huecos en cuadro.
- vc: 1 micro-acción(es) de reposo en los huecos en cuadro.

Reacciones automáticas (7; escribe la tuya en el guion para cambiarla):

- 2:11,9 nina reacciona a el trueno: SenseSomethingStart_NoWeapon
- 2:12,0 v02 reacciona a el trueno: SenseSomethingStart_NoWeapon
- 2:11,9 v06 reacciona a el trueno: SenseSomethingStart_NoWeapon
- 2:12,0 v08 reacciona a el trueno: SenseSomethingStart_NoWeapon
- 2:12,1 v09 reacciona a el trueno: SenseSomethingStart_NoWeapon
- 2:12,1 vc reacciona a el trueno: SenseSomethingStart_NoWeapon
- 3:19,2 archimago para el hechizo de mago: DefendHit_NoWeapon

Gestos que no se ven enteros (2): sepáralos o usa COMPASES.

- 3:18,7 archimago: «Defend_NoWeapon» (0,7 s) cortado a los 0,5 s por «DefendHit_NoWeapon»
- 3:21,2 mago: «Defend_NoWeapon» (0,7 s) cortado a los 0,5 s por «TakeDamage»

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@inicio** |  |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
| 0:00,0 | efecto | Rótulo: PROLOGO_ROTULO_SIGLOS |
| 0:00,0 | efecto | De la mano: MadreDelValle coge a LucasDelValle |
| 0:00,0 | efecto | Voz: ExteriorDia, +0,0 dB |
| 0:00,0 | efecto | Apertura: el deseo en el Sendero (31,5s) |
| 0:31,5 | efecto | Música: PROLOGO_AMANECER_VALLE |
| 0:31,5 | efecto | Ambiente: AMBIENTE ← Ambience_Prologo_Pueblo |
| 0:31,5 | efecto | Post-procesado: PerfilDeCinematica (0s) |
| 0:31,5 | efecto | Hora del día: Morning (de golpe) |
| 0:31,5 | efecto | Apagar: PROP_Nube_Oeste |
| 0:31,5 | efecto | Apagar: PROP_Nube_Este |
| 0:31,5 | efecto | VFX: Butterfly01_P en mariposas_a |
| 0:31,5 | efecto | VFX: Butterfly02_P en mariposas_b |
| 0:31,5 | efecto | VFX: Butterfly03_P en mariposas_c |
| 0:31,5 | efecto | Apertura: vuelo de cámara al valle con el logo (32,0s) |
| 0:31,5 | efecto | Decorado: mover PROP_Carreta (al instante) |
| 1:03,1 | **@entra** |  |
| 1:03,1 | **@saludo** |  |
| 1:03,1 | plano 1 | medio de archimago · 3,7 s · lente 50° |
| 1:03,1 | archimago | «Buenos días, pueblo. ¿Qué se te ha estropeado hoy?» (3,7 s) · pose |
| 1:06,3 | efecto | SFX: SFX_Pueblo_Contraventana_Abre |
| 1:06,3 | efecto | Hojas: abrir Mirador · Vecino |
| 1:06,8 | plano 2 | medio de v02 · 4,0 s · lente 38° |
| 1:06,8 | v01 | «¡Buenos días, maestro!» (1,8 s) |
| 1:07,3 | efecto | Hojas: abrir Mirador · Abuela |
| 1:07,3 | efecto | SFX: SFX_Pueblo_Contraventana_Abre |
| 1:08,9 | v02 | «¡Muy buenas, maestro!» (1,8 s) · pose |
| 1:10,7 | **@saludo_fin** |  |
| 1:10,7 | plano 3 | medio de v06 · 5,7 s · lente 38° · coste 2: ángulo 2 |
| 1:10,7 | v06 | «¡Maestro! Se me ha volcado otra vez la carreta. Y eso que hoy iba despacio.» (5,7 s) · pose |
| 1:11,1 | efecto | Hojas: abrir Mirador · Vecina |
| 1:11,1 | efecto | SFX: SFX_Pueblo_Contraventana_Abre |
| 1:16,4 | plano 4 | conjunto de archimago · 4,0 s · lente 46° |
| 1:20,3 | **@carreta_magia** |  |
| 1:20,3 | plano 5 | general de archimago · 4,2 s · lente 56° |
| 1:20,6 | efecto | SFX: Prologo_MagiaLevitar |
| 1:20,7 | efecto | VFX: Luz_Remolino en carreta |
| 1:20,9 | efecto | Decorado: mover PROP_Carreta en 1,0s |
| 1:22,0 | efecto | Decorado: mover PROP_Carreta en 1,1s |
| 1:23,3 | efecto | Decorado: mover PROP_Carreta en 0,8s |
| 1:24,1 | efecto | SFX: Prologo_CarretaCae |
| 1:24,5 | plano 6 | medio de v06 · 2,4 s · lente 38° · coste 14: ángulo 14 |
| 1:26,9 | plano 7 | sobre el hombro de archimago a v06 · 4,2 s · lente 30° · coste 80: cara 80 |
| 1:26,9 | v06 | «¡Gracias, maestro! No habría podido enderezarla yo solo.» (4,2 s) · pose |
| 1:31,0 | plano 8 | sobre el hombro de archimago a nina · 4,3 s · lente 30° · coste 859: tapado 122, delante 665, cara 63 |
| 1:31,0 | nina | «¡Maestro! El globo se ha enganchado en lo alto del campanario.» (4,3 s) · pose |
| 1:35,4 | plano 9 | medio de archimago · 2,6 s · lente 38° · coste 2: ángulo 2 |
| 1:35,4 | archimago | «Con cuidado... Eso es.» (2,6 s) |
| 1:35,4 | efecto | Hora del día: AfterNoon |
| 1:35,8 | efecto | SFX: Prologo_MagiaLevitar |
| 1:38,0 | **@globo_sube** |  |
| 1:38,0 | plano 10 | general de el punto · 2,6 s · lente 48° |
| 1:38,0 | efecto | Decorado: mover PROP_Globo en 8,0s |
| 1:40,6 | plano 11 | conjunto de nina y v03 y v08 · 7,7 s · lente 40° · coste 0: limpio |
| 1:40,6 | nina | «¡Hala, ya vuela!» (2,6 s) · pose |
| 1:43,5 | v03 | «¡Mira cómo sube!» (2,1 s) · pose |
| 1:45,9 | v08 | «¡Va a llegar hasta las nubes!» (2,3 s) · pose |
| 1:48,2 | **@globo_fin** |  |
| 1:48,2 | plano 12 | medio de archimago · 2,2 s · lente 38° · coste 3: ángulo 3 |
| 1:50,2 | efecto | Apagar: PROP_Globo |
| 1:50,5 | **@rio** |  |
| 1:50,5 | plano 13 | siguiendo a liora · 6,6 s · lente 34° |
| 1:50,5 | efecto | Sol: en cuadro, poniéndose en 19 s |
| 1:50,5 | efecto | Pantalla: descubrir (0,5s) |
| 1:50,5 | efecto | Hora del día: Sunset |
| 1:50,5 | efecto | Ambiente: AMBIENTE_RIO ← Ambience_Prologo_Rio |
| 1:50,5 | efecto | Ambiente: parar AMBIENTE |
| 1:50,5 | efecto | Música: PROLOGUE_DREAM |
| 1:50,5 | efecto | Pantalla: cubrir (0,4s) |
| 1:51,5 | **@rio_paseo** |  |
| 1:52,1 | liora | «¿Has pensado qué contestar a la academia? Llevas semanas con la carta.» (4,4 s) |
| 1:57,1 | plano 14 | medio de archimago · 5,7 s · lente 38° · coste 121: cara 100, ángulo 15 |
| 1:57,1 | archimago | «Hoy pensaba contestar. Pero siempre acaba surgiendo algo en el valle.» (5,7 s) |
| 2:02,8 | plano 15 | medio de liora · 5,0 s · lente 38° · coste 163: cara 100, ángulo 57 |
| 2:02,8 | liora | «No tienes que quedarte solo porque aquí te necesiten. ¿Qué quieres hacer tú?» (5,0 s) · pose |
| 2:07,8 | plano 16 | dos: archimago y liora · 2,2 s · lente 40° |
| 2:10,0 | **@cielo** |  |
| 2:10,0 | plano 17 | medio de archimago · 1,6 s · lente 38° · coste 289: cara 212, ángulo 71 |
| 2:10,0 | efecto | Ambiente: parar AMBIENTE_RIO |
| 2:10,0 | efecto | SFX: SFX_Prologo_Viento |
| 2:10,0 | efecto | Tiempo: encender Viento |
| 2:10,0 | efecto | Tiempo: encender Tormenta |
| 2:10,0 | efecto | Hora del día: Night |
| 2:10,0 | efecto | Sol: devolverlo al ciclo |
| 2:10,0 | efecto | VFX: recoger efectos registrados |
| 2:10,0 | efecto | Música: parar (3s) |
| 2:10,8 | efecto | Hojas: cerrar Pueblo del prólogo en 3 s |
| 2:11,0 | efecto | Hojas: abrir Puerta de la madre |
| 2:11,1 | efecto | SFX: SFX_Pueblo_Contraventana_Golpe |
| 2:11,2 | efecto | De la mano: MadreDelValle suelta a LucasDelValle |
| 2:11,6 | plano 18 | general de el punto · 2,8 s · lente 40° |
| 2:11,6 | efecto | Rayo: 2 × fondo del plano |
| 2:11,9 | efecto | SFX: SFX_Pueblo_Contraventana_Golpe |
| 2:12,6 | efecto | SFX: SFX_Pueblo_Contraventana_Golpe |
| 2:13,3 | efecto | SFX: SFX_Pueblo_Contraventana_Golpe |
| 2:14,4 | efecto | Hojas: cerrar Puerta de la madre |
| 2:14,4 | plano 19 | conjunto de v02 y v10 y v01 y nina · 2,0 s · lente 34° · coste 14: ángulo 14 |
| 2:14,7 | efecto | SFX: SFX_Pueblo_Contraventana_Golpe |
| 2:16,4 | **@llegada** |  |
| 2:16,4 | plano 20 | conjunto de mago · 4,0 s · lente 34° |
| 2:16,4 | efecto | Voz: ExteriorNoche, +0,0 dB |
| 2:16,4 | efecto | Fogonazo (0,18s) |
| 2:16,4 | efecto | Rayo: 1 × fondo del plano |
| 2:16,4 | efecto | SFX: Prologo_Golpe |
| 2:16,4 | efecto | Sacudida de cámara (0,55, 0,6s) |
| 2:16,7 | efecto | SFX: Prologo_PresenciaOscura |
| 2:16,8 | efecto | Música: MAGOOSCURO_REVEAL |
| 2:17,0 | efecto | Ambiente: TORMENTA ← Ambience_Prologo_Tormenta |
| 2:17,0 | efecto | Tiempo: encender Lluvia |
| 2:20,4 | plano 21 | medio de mago · 1,8 s · lente 34° · coste 199: cara 138, ángulo 62 |
| 2:22,2 | plano 22 | medio de archimago · 1,0 s · lente 38° · coste 31: cara 25, ángulo 6 |
| 2:23,2 | plano 23 | siguiendo a mago · 3,5 s · lente 34° |
| 2:23,7 | efecto | Rayo: 1 × fondo del plano |
| 2:26,7 | **@evacuar** |  |
| 2:26,7 | plano 24 | medio de liora · 2,2 s · lente 38° · coste 121: cara 100, ángulo 15 |
| 2:26,7 | liora | «¿Quién es? ¿Qué quiere?» (2,2 s) · pose |
| 2:26,7 | efecto | Ambiente: PANICO ← Ambience_Prologo_Panico |
| 2:28,9 | plano 25 | medio de archimago · 3,1 s · lente 38° · coste 121: cara 100, ángulo 15 |
| 2:28,9 | archimago | «No lo sé. Llévatelos al puente, deprisa.» (3,1 s) · pose |
| 2:32,1 | plano 26 | medio de archimago · 5,2 s · lente 38° · coste 546: delante 344, cara 176, ángulo 18 |
| 2:34,6 | **@huida** |  |
| 2:34,6 | archimago | «¡Al puente! ¡Todos, ahora!» (2,6 s) · pose |
| 2:36,6 | efecto | Hojas: abrir Puerta de la madre |
| 2:37,1 | efecto | De la mano: MadreDelValle coge a LucasDelValle |
| 2:37,2 | plano 27 | general de v06 y v04 y v09 y v03 · 1,6 s · lente 40° · coste 232: delante 221, ángulo 5 |
| 2:37,6 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 2:38,8 | plano 28 | medio de liora · 1,3 s · lente 38° · coste 11: ángulo 5 |
| 2:38,8 | liora | «¿Y tú?» (1,3 s) |
| 2:40,1 | plano 29 | medio de archimago · 2,3 s · lente 38° · coste 816: tapado 183, delante 621, ángulo 3 |
| 2:40,1 | archimago | «Ve. Yo me encargo de él.» (2,3 s) · pose |
| 2:42,4 | plano 30 | dos: archimago y liora · 7,5 s · lente 40° |
| 2:42,4 | liora | «Vuelve pronto.» (1,9 s) |
| 2:44,5 | liora | «Y cuando vuelvas, nada de encargos. Te toca descansar.» (5,4 s) |
| 2:49,9 | plano 31 | medio de archimago · 1,8 s · lente 38° · coste 199: cara 175, ángulo 24 |
| 2:51,8 | plano 32 | sobre el hombro de liora a nina · 1,3 s · lente 30° · coste 63: cara 63 |
| 2:53,1 | **@vamos** |  |
| 2:53,1 | plano 33 | conjunto de liora y nina · 3,6 s · lente 40° · coste 331: cara 276, ángulo 50 |
| 2:53,1 | liora | «¡Vamos, Nina! ...Ten mucho cuidado, por favor.» (3,6 s) |
| 2:56,7 | **@acercan** |  |
| 2:56,7 | plano 34 | siguiendo a archimago · 4,5 s · lente 34° |
| 2:57,9 | efecto | Rayo: 1 × fondo del plano |
| 2:58,7 | efecto | Ambiente: parar PANICO |
| 3:01,2 | plano 35 | general de archimago y mago · 7,0 s · lente 50° |
| 3:01,2 | efecto | Música: MAGOOSCURO_CLIMAX |
| 3:08,1 | **@duelo** |  |
| 3:08,1 | plano 36 | sobre el hombro de archimago a mago · 3,0 s · lente 30° · coste 0: limpio |
| 3:08,1 | mago | «¿Vas a salvarlos a todos tú solo?» (3,0 s) · pose |
| 3:08,1 | efecto | Mezcla: primeraVozOscura |
| 3:08,3 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 3:11,1 | plano 37 | medio de mago · 3,1 s · lente 38° · coste 9: ángulo 3 |
| 3:11,1 | mago | «Ni a ti mismo puedes salvarte.» (3,1 s) |
| 3:14,2 | plano 38 | medio de archimago · 3,3 s · lente 38° · coste 3: ángulo 3 |
| 3:14,2 | archimago | «Mientras siga en pie, no llegarás a ellos.» (3,3 s) · pose |
| 3:14,2 | efecto | Mezcla: primeraVozOscura (quitar) |
| 3:17,5 | plano 39 | dos: archimago y mago · 2,7 s · lente 58° |
| 3:17,7 | efecto | Hechizo: NPC_MagoOscuro → NPC_Archimago |
| 3:18,1 | efecto | Hechizo: NPC_Archimago → NPC_MagoOscuro |
| 3:18,5 | efecto | Hechizo: NPC_MagoOscuro → NPC_Archimago |
| 3:18,6 | efecto | SFX: Prologo_ImpactoHechizo |
| 3:18,7 | efecto | VFX: Luz_Escudo en NPC_Archimago |
| 3:19,0 | efecto | SFX: Prologo_EscudoBloquea |
| 3:19,0 | efecto | Fogonazo (0,1s) |
| 3:19,0 | efecto | Sacudida de cámara (0,3, 0,35s) |
| 3:20,2 | **@intercambio** |  |
| 3:20,2 | plano 40 | dos: archimago y mago · 2,9 s · lente 62° |
| 3:20,3 | efecto | Hechizo: NPC_MagoOscuro → NPC_Archimago |
| 3:20,4 | efecto | Sacudida de cámara (0,25, 0,9s) |
| 3:20,5 | efecto | Salto: NPC_Archimago 2,6 m y 2,2 m de lado |
| 3:20,5 | efecto | VFX: Polvo en NPC_Archimago |
| 3:20,5 | efecto | SFX: Prologo_Despegue |
| 3:20,7 | efecto | Hechizo: NPC_MagoOscuro → NPC_Archimago |
| 3:21,0 | efecto | Hechizo: NPC_Archimago → NPC_MagoOscuro |
| 3:21,0 | efecto | Fogonazo (0,1s) |
| 3:21,1 | efecto | Hechizo: NPC_MagoOscuro → NPC_Archimago |
| 3:21,2 | efecto | VFX: Sombra_Escudo en NPC_MagoOscuro |
| 3:21,5 | efecto | SFX: Prologo_EscudoBloquea |
| 3:21,7 | efecto | VFX: Luz_Impacto en NPC_MagoOscuro |
| 3:21,7 | efecto | SFX: Prologo_ImpactoHechizo |
| 3:21,7 | efecto | Fogonazo (0,14s) |
| 3:21,7 | efecto | Sacudida de cámara (0,55, 0,6s) |
| 3:23,0 | **@choque** |  |
| 3:23,0 | plano 41 | dos: archimago y mago · 3,8 s · lente 58° |
| 3:23,1 | efecto | Conjuro en las manos: NPC_Archimago (0,6 s) |
| 3:23,1 | efecto | Conjuro en las manos: NPC_MagoOscuro (0,6 s) |
| 3:23,1 | efecto | VFX: Luz_Circulo en NPC_Archimago |
| 3:23,1 | efecto | VFX: Sombra_Circulo en NPC_MagoOscuro |
| 3:23,2 | efecto | VFX: Marca en choque |
| 3:23,6 | efecto | Hechizo: NPC_MagoOscuro → PUNTO_CHOQUE |
| 3:23,6 | efecto | Hechizo: NPC_Archimago → PUNTO_CHOQUE |
| 3:23,7 | efecto | SFX: SFX_Prologo_Latigazo |
| 3:24,0 | efecto | SFX: Prologo_Choque |
| 3:24,0 | efecto | Líneas de concentración: mostrar |
| 3:24,0 | efecto | VFX: Choque en choque |
| 3:24,0 | efecto | VFX: Pilar en choque |
| 3:24,0 | efecto | Fogonazo (0,28s) |
| 3:24,0 | efecto | Sacudida de cámara (0,75, 1s) |
| 3:24,1 | efecto | SFX: Prologue_WarClashStinger_A |
| 3:26,8 | plano 42 | dos: archimago y mago · 2,1 s · lente 58° |
| 3:27,0 | efecto | Hechizo: NPC_MagoOscuro → NPC_Archimago |
| 3:27,4 | efecto | SFX: Prologo_SueloSeAbre |
| 3:27,4 | efecto | SFX: SFX_Prologo_Latigazo |
| 3:27,7 | efecto | VFX: Sombra_Grieta en NPC_Archimago |
| 3:27,9 | efecto | SFX: Prologo_ImpactoHechizo |
| 3:27,9 | efecto | VFX: Sombra_Onda en NPC_Archimago |
| 3:27,9 | efecto | Sacudida de cámara (0,7, 0,9s) |
| 3:27,9 | efecto | SFX: SFX_Prologo_Impacto |
| 3:28,9 | **@ultimo** |  |
| 3:28,9 | plano 43 | primer plano de mago · 3,3 s · lente 36° · coste 5: ángulo 5 |
| 3:28,9 | mago | «Que este valle no haya existido nunca.» (3,3 s) · pose |
| 3:32,2 | plano 44 | general de archimago y mago · 3,4 s · lente 46° |
| 3:32,3 | efecto | SFX: Prologo_Despegue |
| 3:32,3 | efecto | VFX: Polvo en NPC_MagoOscuro |
| 3:32,5 | efecto | Rayo: 2 × fondo del plano |
| 3:32,8 | efecto | SFX: Prologo_HechizoGrande |
| 3:33,0 | efecto | SFX: SFX_Prologo_AgujeroNegro_Carga |
| 3:33,0 | efecto | Ambiente: AGUJERO_NEGRO ← SFX_Prologo_AgujeroNegro_Bucle |
| 3:33,0 | efecto | VFX: Sombra_CargaGrande en agujero |
| 3:33,0 | efecto | Escalar: CARGA_ESFERA ×0,35 en 0 s |
| 3:33,1 | efecto | Escalar: CARGA_ESFERA ×1 en 3,5 s |
| 3:33,2 | efecto | VFX: Sombra_Circulo en NPC_MagoOscuro |
| 3:33,2 | efecto | Sacudida de cámara (0,35, 1,6s) |
| 3:35,6 | plano 45 | medio de archimago · 8,7 s · lente 42° · coste 0: limpio |
| 3:35,6 | archimago | «No daré ni un paso atrás... ¡No dejaré que los toques! ¡Liora! ¡Nina! ¡A ninguno de ellos!» (7,3 s) |
| 3:35,6 | efecto | Voz: Ninguno, +0,0 dB |
| 3:35,6 | efecto | Mezcla: plegaria |
| 3:35,8 | efecto | SFX: Prologo_MagiaLevitar |
| 3:35,8 | efecto | VFX: Luz_Circulo en NPC_Archimago |
| 3:38,9 | efecto | Cut-in 0: NPC_Liora (Cara) |
| 3:38,9 | efecto | Música: parar (3s) |
| 3:40,6 | efecto | Cut-in 0: NPC_Aldeano_05 (Cara) |
| 3:41,9 | efecto | Mezcla: plegaria |
| 3:41,9 | efecto | Música: PROLOGO_CLIMAX |
| 3:42,9 | efecto | Pantalla: cubrir (1,2s) |
| 3:44,3 | plano 46 | general de mago · 1,4 s · lente 46° |
| 3:44,3 | efecto | Pantalla: descubrir (1,2s) |
| 3:44,3 | efecto | Voz: Conjuro, +6,0 dB |
| 3:44,3 | efecto | Escalar: CARGA_ESFERA ×1,5 en 2 s |
| 3:44,3 | efecto | Sacudida de cámara (0,25, 1s) |
| 3:45,7 | plano 47 | conjunto de archimago · 8,2 s · lente 46° · coste 0: limpio |
| 3:45,7 | archimago | «Más allá de la noche y del silencio... ¡Luz que naciste antes que el cielo y tendiste el camino entre las estrellas!» (8,2 s) |
| 3:45,7 | efecto | Conjuro en las manos: NPC_Archimago (34,0 s) |
| 3:45,7 | efecto | SFX: SFX_Prologo_CargaPlegaria |
| 3:46,5 | efecto | SFX: SFX_Prologo_Trueno_Lejano |
| 3:53,9 | plano 48 | medio de archimago · 8,6 s · lente 48° · coste 0: limpio |
| 3:53,9 | archimago | «Ningún poder de este mundo puede detenerlo... ¡Por eso llamo a lo que aún no existe! ¡Escucha mi voz y despierta en mis manos!» (8,6 s) |
| 3:57,1 | efecto | Rayo: 1 × fondo del plano |
| 3:59,5 | efecto | Escalar: CARGA_ESFERA ×2 en 2 s |
| 3:59,5 | efecto | Sacudida de cámara (0,35, 1,2s) |
| 4:02,5 | plano 49 | general de mago · 1,2 s · lente 46° |
| 4:02,5 | efecto | Rayo: 1 × fondo del plano |
| 4:03,7 | plano 50 | conjunto de archimago · 6,3 s · lente 58° · coste 0: limpio |
| 4:03,7 | archimago | «Que mi vida sea el precio y mi alma el sello... ¡No importa lo que quede de mí cuando la luz se apague!» (6,3 s) |
| 4:03,7 | efecto | Conjuro a la cabeza: NPC_Archimago (16,0 s) |
| 4:07,2 | efecto | VFX: Pilar en NPC_Archimago |
| 4:07,2 | efecto | Sacudida de cámara (0,3, 0,9s) |
| 4:07,2 | efecto | SFX: Prologo_MagiaLevitar |
| 4:10,0 | plano 51 | conjunto de archimago · 6,1 s · lente 58° · coste 260: eje 260 |
| 4:10,0 | archimago | «¡Haz de mi último aliento un escudo inquebrantable! ¡Arde conmigo y detén la oscuridad!» (6,1 s) |
| 4:10,0 | efecto | Líneas de concentración: mostrar |
| 4:13,4 | efecto | Rayo: 2 × fondo del plano |
| 4:13,5 | efecto | Escalar: CARGA_ESFERA ×2,6 en 1,4 s |
| 4:13,5 | efecto | Sacudida de cámara (0,5, 1,6s) |
| 4:16,1 | plano 52 | primer plano de archimago · 4,2 s · lente 32° · coste 0: limpio |
| 4:16,1 | archimago | «Perdóname, Liora... No voy a volver a casa.» (3,8 s) |
| 4:16,1 | efecto | Líneas de concentración: retirar |
| 4:16,1 | efecto | Voz: Ninguno, +0,0 dB |
| 4:20,3 | **@hechizo** |  |
| 4:20,3 | plano 53 | primer plano de archimago · 1,0 s · lente 26° · coste 18: ángulo 18 |
| 4:20,3 | efecto | Mezcla: plegaria (quitar) |
| 4:20,3 | efecto | Mezcla: grito |
| 4:20,3 | efecto | Líneas de concentración: mostrar |
| 4:20,3 | efecto | Respiro: 0,9 s (música 0 dB) |
| 4:21,3 | plano 54 | general de archimago y mago · 2,1 s · lente 44° |
| 4:21,3 | archimago | «¡¡Protégelos a todos!!» (2,1 s) |
| 4:21,3 | efecto | Voz: Conjuro, +6,0 dB |
| 4:21,5 | efecto | Rayo: 2 × fondo del plano |
| 4:21,6 | efecto | SFX: Prologo_ProteccionAbsoluta |
| 4:21,6 | efecto | Escalar: CARGA_ESFERA ×2,8 en 0,6 s |
| 4:21,6 | efecto | Sacudida de cámara (0,45, 2s) |
| 4:22,0 | efecto | Fusión: BOLA_PROTECTORA + CARGA_ESFERA |
| 4:22,0 | efecto | SFX: SFX_Prologo_LanzamientoConjuro |
| 4:23,4 | **@choque_final** |  |
| 4:23,4 | plano 55 | general de archimago y mago · 1,3 s · lente 58° |
| 4:23,4 | efecto | Líneas de concentración: mostrar |
| 4:23,4 | efecto | VFX: Choque en encuentro_hechizos |
| 4:23,4 | efecto | VFX: Forcejeo en encuentro_hechizos |
| 4:23,4 | efecto | SFX: SFX_Prologo_Plin |
| 4:23,4 | efecto | Fogonazo (0,6s) |
| 4:23,4 | efecto | Ambiente: LUCHA_FINAL ← SFX_Prologo_LuchaFinal |
| 4:23,6 | efecto | Sacudida de cámara (0,5, 2,2s) |
| 4:24,4 | efecto | SFX: SFX_Prologo_PreviaEstallido |
| 4:24,7 | plano 56 | general de archimago y mago · 6,1 s · lente 52° |
| 4:25,8 | **@estallido** |  |
| 4:25,8 | efecto | SFX: Prologue_WarClashStinger_A |
| 4:25,8 | efecto | Ambiente: parar LUCHA_FINAL |
| 4:25,8 | efecto | Ambiente: parar AGUJERO_NEGRO |
| 4:25,8 | efecto | VFX: recoger efectos registrados |
| 4:25,8 | efecto | SFX: Prologo_Choque |
| 4:25,8 | efecto | Fogonazo (0,35s) |
| 4:25,8 | efecto | Sacudida de cámara (1, 1,8s) |
| 4:25,8 | efecto | VFX: EstallidoFinal en choque |
| 4:25,8 | efecto | VFX: Choque en encuentro_hechizos |
| 4:25,8 | efecto | Líneas de concentración: retirar |
| 4:25,8 | efecto | SFX: SFX_Prologo_EstallidoSube |
| 4:26,2 | efecto | Pantalla: cubrir (3,5s) |
| 4:29,9 | **@explosion** |  |
| 4:29,9 | efecto | VFX: recoger efectos registrados |
| 4:29,9 | efecto | Mezcla: grito (quitar) |
| 4:29,9 | efecto | Ambiente: parar TORMENTA |
| 4:29,9 | efecto | Música: parar (2s) |
| 4:29,9 | efecto | Rótulo: CAP1_ROTULO_SIGLOS_DESPUES |
| 4:30,1 | efecto | Hora del día: Morning (de golpe) (y es con la que se queda el mundo) |
| 4:30,3 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### archimago (NPC_Archimago)

- 1:03,1–1:06,8 habla
- 1:03,2 gesto Talk01
- 1:07,5 gesto HeadNod01
- 1:16,4–1:19,9 anda de (-166,8, -362,7) a (-163,6, -364,4)
- 1:20,3 gesto CastingEnter01
- 1:20,8 bucle CastingIdle01
- 1:24,1 reposo 
- 1:26,9 gesto HeadNod01
- 1:27,6 gesto Sobresalto01
- 1:31,7 gesto HeadNod01
- 1:35,4 bucle CastingIdle01
- 1:35,4–1:38,0 habla
- 1:38,0 reposo 
- 1:41,2 gesto Laugh01
- 1:41,3 gesto HeadNod01
- 1:45,3 gesto HeadNod01
- 1:48,2 gesto HeadNod01
- 1:51,5–1:57,1 anda de (-153,1, -362,3) a (-152,6, -366,7)
- 1:57,1–2:02,8 habla
- 1:57,1 gesto Talk02
- 2:03,5 gesto Question01
- 2:09,1 gesto Idle03
- 2:27,4 gesto Question02
- 2:28,9–2:32,1 habla
- 2:29,0 gesto HeadShake01
- 2:30,7 gesto Talk01
- 2:32,1–2:34,7 anda de (-152,6, -366,7) a (-155,6, -364,7)
- 2:34,6–2:37,2 habla
- 2:34,7 gesto Challenging_NoWeapon
- 2:39,3 gesto Question01
- 2:40,1–2:42,4 habla
- 2:40,2 gesto HeadNod01
- 2:43,1 gesto HeadNod01
- 2:45,9 gesto HeadNod01
- 2:49,9 gesto HeadNod01
- 2:53,8 gesto Sobresalto01
- 2:56,7–3:05,7 anda de (-155,6, -364,7) a (-165,6, -365,2)
- 3:08,8 gesto Question01
- 3:14,2–3:17,5 habla
- 3:14,2 gesto Angry02
- 3:15,8 gesto Talk03
- 3:17,9 gesto MagicLeft
- 3:18,7 gesto Defend_NoWeapon
- 3:19,2 gesto DefendHit_NoWeapon
- 3:20,9 gesto MagicLeft
- 3:23,0 gesto MagicSpecial
- 3:27,9 bucle Pain01
- 3:29,6 gesto HeadShake01
- 3:35,6 bucle CastingIdle01
- 3:35,6 reposo 
- 3:35,6–3:42,9 habla
- 3:45,7 bucle Beg01_Loop
- 3:45,7–3:53,9 habla
- 3:53,9–4:02,5 habla
- 4:03,7 bucle Conjuro_Invocar
- 4:03,7–4:10,0 habla
- 4:10,0–4:16,1 habla
- 4:16,1–4:19,9 habla
- 4:21,3–4:23,4 habla
- 4:21,9 bucle Conjuro_Lanzar
- 4:25,8 bucle Die01_NoWeapon
- 4:25,8 reposo 

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
- 1:36,1 gesto HeadNod01
- 1:37,2 gesto Talk01
- 1:41,7 gesto HeadNod01
- 1:44,2 gesto HeadNod01
- 1:45,3 gesto Talk02
- 1:50,5 reposo 
- 1:51,5–1:57,1 anda de (-154,2, -362,7) a (-153,9, -367,1)
- 1:52,1–1:56,4 habla
- 1:57,8 gesto HeadNod01
- 2:02,8–2:07,8 habla
- 2:02,9 gesto Talk01
- 2:05,4 gesto Talk03
- 2:10,5 gesto SenseSomethingStart_NoWeapon
- 2:26,7–2:28,9 habla
- 2:26,8 gesto Question01
- 2:29,6 gesto HeadShake01
- 2:35,3 gesto HeadNod01
- 2:38,8–2:40,1 habla
- 2:38,9 gesto Question02
- 2:38,9–2:39,7 anda de (-154,0, -367,1) a (-155,0, -367,2)
- 2:42,4 bucle ManoAlPecho
- 2:42,4–2:44,4 habla
- 2:44,5–2:49,9 habla
- 2:44,6 gesto Talk03
- 2:47,3 bucle ManoAlPecho
- 2:53,1–2:56,7 habla
- 2:53,2 gesto Talk01
- 2:55,2 bucle ManoAlPecho
- 2:56,7–3:08,7 anda de (-155,0, -367,1) a (-128,0, -365,0)

### mago (NPC_MagoOscuro)

- 2:16,4 bucle fly_idle
- 2:16,4 aparece
- 2:22,0 reposo 
- 2:22,1 gesto Challenging_NoWeapon
- 2:22,2 reposo 
- 2:23,3–2:50,3 anda de (-212,2, -368,1) a (-182,0, -364,5)
- 2:56,7–3:08,2 anda de (-182,0, -364,5) a (-169,2, -365,2)
- 3:08,1–3:11,1 habla
- 3:08,2 gesto Talk02
- 3:11,1 bucle ManosALaEspalda
- 3:11,1–3:14,2 habla
- 3:14,2 reposo 
- 3:17,5 gesto MagicRight
- 3:18,3 gesto MagicLeft
- 3:20,2 gesto MagicRight
- 3:20,6 gesto MagicLeft
- 3:21,0 gesto MagicRight
- 3:21,2 gesto Defend_NoWeapon
- 3:21,7 gesto TakeDamage
- 3:23,0 gesto MagicSpecial
- 3:26,8 gesto MagicRight
- 3:28,9–3:32,2 habla
- 3:29,0 gesto HeadShake01
- 3:30,6 gesto Talk01
- 3:32,2 bucle JumpAir_InPlace_NoWeapon
- 3:32,2 bucle FoundSomething_NoWeapon
- 4:21,9 bucle Conjuro_Lanzar
- 4:25,8 reposo 
- 4:25,8 bucle Die01_NoWeapon

### nina (NPC_Aldeano_05)

- 0:00,0 bucle SenseSomethingSearching_NoWeapon
- 1:20,7 reposo 
- 1:20,9–1:28,1 anda de (-179,0, -364,0) a (-165,1, -366,2)
- 1:31,0–1:35,4 habla
- 1:31,1 gesto Beg01
- 1:36,1 gesto HeadNod01
- 1:40,6–1:43,2 habla
- 1:40,6 gesto Cheer01
- 1:44,2 gesto HeadNod01
- 1:48,3–1:53,4 anda de (-165,2, -366,2) a (-174,8, -364,0)
- 1:53,2 bucle InteractWithPeople_NoWeapon
- 1:56,2 gesto Talk01
- 1:59,4 gesto HeadNod01
- 2:02,7 gesto Talk02
- 2:05,5 gesto Laugh01
- 2:08,4 gesto Talk03
- 2:10,3 reposo 
- 2:11,9 gesto SenseSomethingStart_NoWeapon
- 2:15,7 gesto Idle03
- 2:35,3–2:46,3 anda de (-174,7, -364,1) a (-154,0, -366,8)
- 2:38,7 gesto SenseSomethingStart_NoWeapon
- 2:47,7 gesto Idle03
- 2:53,8 gesto Sobresalto01
- 2:57,5–3:08,1 anda de (-154,0, -366,7) a (-143,4, -357,5)

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
- 1:06,8–1:08,6 habla
- 1:06,8 gesto HandWave01
- 1:07,7 reposo 
- 1:08,3–1:10,2 anda de (-172,9, -366,5) a (-171,4, -363,4)
- 1:11,4–1:14,0 anda de (-171,5, -363,4) a (-174,5, -366,4)
- 1:14,8–1:16,0 anda de (-174,5, -366,4) a (-172,7, -367,4)
- 1:16,8–1:18,0 anda de (-172,7, -367,4) a (-174,5, -366,4)
- 1:18,8–1:20,4 anda de (-174,5, -366,3) a (-175,2, -363,7)
- 1:21,2–1:22,8 anda de (-175,2, -363,8) a (-174,5, -366,4)
- 1:23,6–1:24,8 anda de (-174,4, -366,5) a (-172,7, -367,4)
- 1:25,6–1:26,8 anda de (-172,8, -367,3) a (-174,5, -366,4)
- 1:27,6–1:29,1 anda de (-174,5, -366,3) a (-175,2, -363,7)
- 1:29,9–1:31,5 anda de (-175,2, -363,7) a (-174,5, -366,4)
- 1:32,3–1:33,5 anda de (-174,4, -366,4) a (-172,7, -367,4)
- 1:34,3–1:35,5 anda de (-172,8, -367,4) a (-174,5, -366,4)
- 1:36,3–1:37,9 anda de (-174,5, -366,3) a (-175,2, -363,7)
- 1:38,0–1:39,5 anda de (-175,2, -363,7) a (-176,6, -365,8)
- 1:41,3 gesto HeadNod01
- 1:48,4 gesto Cheer02
- 1:49,8–1:51,9 anda de (-176,5, -365,8) a (-173,0, -366,6)
- 1:52,4–1:52,9 anda de (-172,9, -366,7) a (-172,7, -367,4)
- 1:53,4–1:53,8 anda de (-172,8, -367,2) a (-173,0, -366,6)
- 1:54,3–1:55,2 anda de (-173,0, -366,6) a (-174,5, -366,4)
- 1:55,7–1:56,6 anda de (-174,5, -366,4) a (-173,0, -366,6)
- 1:57,1–1:57,6 anda de (-173,0, -366,7) a (-172,7, -367,4)
- 1:58,1–1:58,6 anda de (-172,7, -367,3) a (-173,0, -366,6)
- 1:59,1–1:60,0 anda de (-173,1, -366,6) a (-174,5, -366,4)
- 2:00,5–2:01,4 anda de (-174,4, -366,4) a (-173,0, -366,6)
- 2:01,9–2:02,3 anda de (-172,9, -366,8) a (-172,7, -367,4)
- 2:02,8–2:03,3 anda de (-172,7, -367,4) a (-173,0, -366,6)
- 2:03,8–2:04,7 anda de (-173,0, -366,6) a (-174,5, -366,4)
- 2:05,2–2:06,1 anda de (-174,5, -366,4) a (-173,0, -366,6)
- 2:06,6–2:07,1 anda de (-173,0, -366,7) a (-172,7, -367,4)
- 2:07,6–2:08,1 anda de (-172,7, -367,3) a (-173,0, -366,6)
- 2:08,6–2:09,5 anda de (-173,1, -366,6) a (-174,5, -366,4)
- 2:10,0–2:11,8 anda de (-174,4, -366,4) a (-173,1, -364,0)
- 2:10,3 reposo 
- 2:13,1 gesto Idle03
- 2:37,1–2:56,8 anda de (-172,9, -364,0) a (-130,0, -374,5)

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
- 1:03,2–1:07,5 anda de (-173,8, -363,1) a (-165,7, -364,4)
- 1:03,8 gesto Question01
- 1:07,5 reposo 
- 1:08,5 gesto HandWave02
- 1:08,9–1:10,7 habla
- 1:09,0 gesto HandWave02
- 1:11,6–1:21,7 anda de (-165,7, -364,4) a (-173,9, -363,1)
- 1:36,1 gesto HeadNod01
- 1:44,2 gesto HeadNod01
- 1:49,7 bucle InteractWithPeople_NoWeapon
- 1:52,5 gesto Laugh01
- 1:56,5 gesto Talk03
- 1:59,3 gesto Question01
- 2:03,2 gesto HeadShake01
- 2:06,0 gesto Talk01
- 2:10,4 reposo 
- 2:10,5 gesto SenseSomethingStart_NoWeapon
- 2:12,0 gesto SenseSomethingStart_NoWeapon
- 2:16,9 bucle Fear01
- 2:36,1–2:56,3 anda de (-173,7, -363,1) a (-126,0, -361,5)
- 3:08,6 bucle Beg01_Loop

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
- 1:24,7 bucle InteractWithPeople_NoWeapon
- 1:27,7 gesto HeadShake01
- 1:31,4 gesto Talk01
- 1:34,0 reposo 
- 1:34,0–1:36,9 anda de (-169,7, -360,1) a (-168,7, -364,1)
- 1:36,1 gesto HeadNod01
- 1:41,8 gesto HeadNod01
- 1:43,5–1:45,6 habla
- 1:43,6 gesto FoundSomething_NoWeapon
- 1:46,6 gesto HeadNod01
- 1:48,2 bucle InteractWithPeople_NoWeapon
- 1:49,5 gesto Talk03
- 1:53,2 gesto Question01
- 1:55,6 gesto HeadShake01
- 1:57,9 gesto Talk01
- 2:00,6 gesto HeadNod01
- 2:04,5 gesto Talk02
- 2:06,9 gesto Laugh01
- 2:10,2 reposo 
- 2:11,2 gesto FoundSomething_NoWeapon
- 2:17,0 gesto Fear01
- 2:35,6–2:54,5 anda de (-168,6, -364,0) a (-124,0, -366,0)
- 3:32,9 bucle Reverence01_Loop

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
- 1:20,3 reposo 
- 1:20,4–1:23,3 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 1:24,2 gesto HandClap01
- 1:25,1–1:28,0 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 1:29,8–1:32,7 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 1:34,5–1:37,3 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 1:38,0–1:40,9 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 1:42,7–1:45,6 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 1:46,6 gesto HeadNod01
- 1:47,4–1:50,3 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 1:52,1–1:54,9 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 1:56,7–1:59,6 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 2:01,4–2:04,3 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 2:06,1–2:09,0 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 2:10,2 reposo 
- 2:10,8–2:13,7 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 2:15,5–2:18,3 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 2:20,1–2:23,0 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 2:24,8–2:27,7 anda de (-170,1, -369,2) a (-172,2, -369,2)
- 2:29,5–2:32,4 anda de (-172,2, -369,2) a (-170,1, -369,2)
- 2:34,2–2:53,5 anda de (-170,1, -369,2) a (-128,4, -368,4)

### v06 (NPC_Aldeano_06)

- 1:03,8 gesto Question01
- 1:10,7–1:16,4 habla
- 1:10,8 gesto Question01
- 1:13,9 gesto Shrug01
- 1:20,5 bucle Idle_Normal_NoWeapon
- 1:21,0 gesto FoundSomething_NoWeapon
- 1:24,0 gesto Cheer01
- 1:24,5 reposo 
- 1:24,6–1:26,9 anda de (-160,0, -364,6) a (-162,3, -364,1)
- 1:26,9–1:31,0 habla
- 1:27,0 gesto HeadNod01
- 1:28,3 gesto Talk01
- 1:36,1 gesto HeadNod01
- 1:41,8 gesto HeadNod01
- 1:44,2 gesto HeadNod01
- 1:48,3–1:54,4 anda de (-162,3, -364,1) a (-159,3, -367,6)
- 1:54,6 bucle Opening01_Loop
- 1:57,8 gesto HeadNod01
- 2:03,5 gesto Question01
- 2:10,5 reposo 
- 2:11,9 gesto SenseSomethingStart_NoWeapon
- 2:27,4 gesto Question02
- 2:29,6 gesto HeadShake01
- 2:34,7–2:47,5 anda de (-159,2, -367,5) a (-131,0, -364,0)
- 2:35,3 gesto HeadNod01

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
- 1:29,7–1:32,6 anda de (-172,0, -364,8) a (-171,4, -366,8)
- 1:33,4–1:36,2 anda de (-171,4, -366,8) a (-172,0, -364,8)
- 1:37,0–1:40,2 anda de (-172,0, -364,8) a (-175,1, -366,6)
- 1:41,3 gesto HeadNod01
- 1:50,7–1:55,3 anda de (-175,1, -366,6) a (-172,0, -364,8)
- 1:56,2–1:59,1 anda de (-172,0, -364,8) a (-171,4, -366,8)
- 2:01,6–2:07,7 anda de (-171,4, -366,8) a (-175,1, -366,6)
- 2:09,8–2:12,3 anda de (-175,1, -366,6) a (-172,0, -364,8)
- 2:13,6 gesto Idle03
- 2:38,7–2:59,3 anda de (-171,8, -364,8) a (-125,0, -357,0)

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
- 1:28,5 gesto Talk01
- 1:30,0 reposo 
- 1:30,0–1:36,2 anda de (-158,6, -362,6) a (-167,7, -364,5)
- 1:36,1 gesto HeadNod01
- 1:41,8 gesto HeadNod01
- 1:44,2 gesto HeadNod01
- 1:45,9–1:48,2 habla
- 1:46,0 gesto Cheer02
- 1:48,7 bucle InteractWithPeople_NoWeapon
- 1:50,9 gesto HeadNod01
- 1:54,5 gesto Talk02
- 1:58,0 gesto Laugh01
- 2:01,6 gesto Talk03
- 2:05,2 gesto Question01
- 2:08,1 gesto HeadShake01
- 2:10,5 reposo 
- 2:12,0 gesto SenseSomethingStart_NoWeapon
- 2:15,7 gesto Idle03
- 2:37,8–2:54,0 anda de (-167,5, -364,5) a (-132,0, -372,0)
- 3:09,0 bucle Fear01

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
- 1:22,7 gesto Talk03
- 1:24,1 gesto HandClap01
- 1:25,3 bucle InteractWithPeople_NoWeapon
- 1:28,7 gesto Talk01
- 1:31,6 gesto HeadNod01
- 1:36,0 gesto Talk02
- 1:36,1 gesto HeadNod01
- 1:40,4 gesto Laugh01
- 1:44,2 gesto HeadNod01
- 1:44,9 gesto Talk03
- 1:49,4 gesto Question01
- 1:53,6 gesto HeadShake01
- 1:57,4 gesto Talk01
- 2:00,9 gesto HeadNod01
- 2:05,2 gesto Talk02
- 2:09,2 gesto Laugh01
- 2:10,4 reposo 
- 2:12,1 gesto SenseSomethingStart_NoWeapon
- 2:17,3 gesto Fear01
- 2:35,5–2:54,0 anda de (-171,0, -359,5) a (-129,0, -359,0)
- 3:09,3 bucle Fear01

### v10 (NPC_Aldeano_10)

- 0:00,0 bucle Opening01_Loop
- 1:03,8 gesto Question01
- 1:05,3 gesto HeadNod01
- 1:38,6 reposo 
- 1:41,3 gesto HeadNod01
- 1:48,5 gesto HandClap01
- 2:17,2 gesto FoundSomething_NoWeapon
- 2:36,4–2:59,3 anda de (-175,9, -362,3) a (-122,0, -367,5)
- 3:33,9 bucle Beg01_Loop

### panadera (PanaderaDelValle)

- 1:04,6 bucle InteractWithPeople_NoWeapon
- 1:05,1 gesto HeadNod01
- 1:13,7 gesto HeadNod01
- 1:24,2 gesto HandClap01
- 2:17,1 gesto Fear01
- 2:35,4 reposo 
- 2:35,5–3:03,5 anda de (-183,0, -368,5) a (-118,0, -371,5)

### madre (MadreDelValle)

- 0:00,2–0:10,7 anda de (-191,5, -364,7) a (-183,1, -366,3)
- 1:05,1 bucle InteractWithPeople_NoWeapon
- 1:06,2 gesto Laugh01
- 1:09,3 gesto Talk03
- 1:14,3 gesto HeadNod01
- 1:23,9 reposo 
- 1:24,1–1:31,4 anda de (-183,1, -366,3) a (-178,1, -369,3)
- 1:44,2 gesto HeadNod01
- 1:48,6 gesto HeadNod01
- 2:12,4–2:13,5 anda de (-178,2, -369,2) a (-178,2, -370,2)
- 2:13,6 desaparece
- 2:37,0 aparece
- 2:37,2–3:03,8 anda de (-178,2, -370,1) a (-120,5, -356,5)

### lucas (LucasDelValle)

- 0:00,1–0:10,7 anda de (-191,5, -365,3) a (-183,2, -366,8)
- 1:24,0–1:31,4 anda de (-183,2, -366,8) a (-178,5, -369,7)
- 2:11,3–2:12,2 anda de (-178,5, -369,6) a (-178,2, -370,2)
- 2:12,2 desaparece
- 2:37,0 aparece
- 2:37,1–3:03,8 anda de (-178,2, -370,2) a (-120,7, -356,0)
- 3:11,8 gesto HeadNod01
- 3:29,6 gesto HeadShake01

### vm (VecinoDelMirador)

- 1:03,8 gesto Question01
- 1:04,1 gesto HandWave02
- 1:07,4 gesto HandWave01
- 1:24,2 gesto HandClap01
- 1:36,1 gesto HeadNod01
- 1:44,2 gesto HeadNod01
- 1:57,8 gesto HeadNod01
- 2:03,5 gesto Question01
- 2:10,9 gesto Fear01
- 2:13,0 desaparece

### ab (AbuelaDelMirador)

- 1:05,3 gesto HandWave02
- 1:41,6 gesto HandClap01
- 2:12,2 desaparece

### vc (VecinaDelMirador)

- 1:03,8 gesto Question01
- 1:24,4 gesto HandClap01
- 1:32,3 gesto HeadNod01
- 1:36,1 gesto HeadNod01
- 1:41,0 gesto HandWave01
- 1:44,2 gesto HeadNod01
- 1:49,0 gesto HeadNod01
- 1:53,0 gesto HandWave02
- 1:57,8 gesto HeadNod01
- 2:03,5 gesto Question01
- 2:12,1 gesto SenseSomethingStart_NoWeapon
- 2:12,6 desaparece

