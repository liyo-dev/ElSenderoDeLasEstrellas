# Parte de horneado: Cap2_02_MananaDespues

2026-10-31 21:25 · guion `Assets/_SEQUENCES/Guiones/Cap2_02_MananaDespues.guion.txt`

**2:56,1** de secuencia · 33 frases · 32 planos · 18 efectos · 5 personajes

Reparto medido (m): will 1,55/ojos 0,93 · eldran 1,55/ojos 0,95 · selene 1,60/ojos 0,96 · eldran_joven 1,55/ojos 0,95 · silas_joven 1,59/ojos 0,94


## Objetos en la mano

- eldran deja PROP_Bollo: Opening01_Stop; suelta +0,133 s (guion 6,698 s), mano a 0,406 m del apoyo. Tanteo: Opening01_Begin 0,91, Opening01_Stop 0,90, Loot01_Begin 1,03, Loot01_Stop 1,08.
- eldran deja PROP_Aro: Opening01_Stop; suelta +0,133 s (guion 112,444 s), mano a 0,406 m del apoyo. Tanteo: Opening01_Begin 0,91, Opening01_Stop 0,90, Loot01_Begin 1,03, Loot01_Stop 1,08.

## Errores (0)


## Avisos (33)

- línea 10: 'Player' no tiene punto de aparición en WillHouse; necesita un «en» al principio.
- línea 11: 'NPC_Eldran' no tiene punto de aparición en WillHouse; necesita un «en» al principio.
- línea 11: 'NPC_Eldran' no está en el escenario de la secuencia ni hay en WillHouse un NPC que se registre con ese id: en juego no saldrá (salvo que lo traiga otra escena o el grafo). Ponlo en el SequenceStage con ese id.
- línea 125: eldran anda a dejar_bollo ritmo=1.4 llega_mirando=apoyo_bollo: un punto queda fuera del NavMesh (115,3, 4,0); voy en línea recta
- línea 133: eldran deja PROP_Bollo en apoyo_bollo: la mano se queda a 0,41 m del apoyo; acerca al personaje o el apoyo al borde del mueble.
- línea 134: eldran sienta silla por=rodeo_norte, rodeo_sur, paso_silla ritmo=1.4: un punto queda fuera del NavMesh (116,9, 3,3); voy en línea recta
- línea 134: eldran sienta silla por=rodeo_norte, rodeo_sur, paso_silla ritmo=1.4: un punto queda fuera del NavMesh (116,9, 2,3); voy en línea recta
- línea 134: eldran sienta silla por=rodeo_norte, rodeo_sur, paso_silla ritmo=1.4: un punto queda fuera del NavMesh (115,3, 1,5); voy en línea recta
- línea 134: eldran sienta silla por=rodeo_norte, rodeo_sur, paso_silla ritmo=1.4: un punto queda fuera del NavMesh (114,5, 2,3); voy en línea recta
- línea 156: will anda a cama_paso ritmo=normal: un punto queda fuera del NavMesh (116,1, 1,2); voy en línea recta
- línea 157: will sienta cama_borde: un punto queda fuera del NavMesh (116,4, 0,7); voy en línea recta
- línea 163: & eldran sienta cama_eldran por=eldran_a_cama, cama_eldran_paso ritmo=1.3: un punto queda fuera del NavMesh (115,0, 2,3); voy en línea recta
- línea 163: & eldran sienta cama_eldran por=eldran_a_cama, cama_eldran_paso ritmo=1.3: un punto queda fuera del NavMesh (115,9, 1,5); voy en línea recta
- línea 163: & eldran sienta cama_eldran por=eldran_a_cama, cama_eldran_paso ritmo=1.3: un punto queda fuera del NavMesh (116,4, 1,1); voy en línea recta
- línea 212: & selene anda a p_exp_selene_paso, p_exp_selene_umbral ritmo=paseo llega_mirando=p_exp_umbral: un punto queda fuera del NavMesh (300,0, 6,5); voy en línea recta
- línea 212: & selene anda a p_exp_selene_paso, p_exp_selene_umbral ritmo=paseo llega_mirando=p_exp_umbral: un punto queda fuera del NavMesh (300,0, 9,5); voy en línea recta
- línea 213: & eldran_joven anda a p_exp_eldran_paso ritmo=paseo llega_mirando=p_exp_selene_umbral: un punto queda fuera del NavMesh (298,5, 4,5); voy en línea recta
- línea 214: & silas_joven anda a p_exp_silas_paso ritmo=paseo llega_mirando=p_exp_selene_umbral: un punto queda fuera del NavMesh (301,5, 4,2); voy en línea recta
- línea 224: & eldran_joven corre a p_exp_eldran_alcance llega_mirando=p_exp_selene_vacio: un punto queda fuera del NavMesh (298,5, 7,5); voy en línea recta
- línea 225: & selene anda a p_exp_selene_vacio ritmo=paseo: un punto queda fuera del NavMesh (300,0, 12,0); voy en línea recta
- línea 232: & silas_joven anda a p_exp_silas_freno ritmo=prisa llega_mirando=p_exp_eldran_alcance: un punto queda fuera del NavMesh (300,4, 7,2); voy en línea recta
- línea 252: eldran anda a dejar_aro ritmo=1.4 llega_mirando=apoyo_aro: un punto queda fuera del NavMesh (115,3, 3,1); voy en línea recta
- línea 253: eldran deja PROP_Aro en apoyo_aro: la mano se queda a 0,41 m del apoyo; acerca al personaje o el apoyo al borde del mueble.
- línea 256: & eldran sienta silla por=paso_silla: un punto queda fuera del NavMesh (115,3, 1,5); voy en línea recta
- línea 256: & eldran sienta silla por=paso_silla: un punto queda fuera del NavMesh (114,5, 2,3); voy en línea recta
- línea 262: will anda a cama_paso, will_mesa ritmo=normal llega_mirando=apoyo_aro: un punto queda fuera del NavMesh (116,1, 1,2); voy en línea recta
- línea 262: will anda a cama_paso, will_mesa ritmo=normal llega_mirando=apoyo_aro: un punto queda fuera del NavMesh (115,7, 2,6); voy en línea recta
- línea 287: eldran anda a paso_silla, rodeo_sur, rodeo_norte, puerta, fuera ritmo=1.4: un punto queda fuera del NavMesh (115,3, 1,5); voy en línea recta
- línea 287: eldran anda a paso_silla, rodeo_sur, rodeo_norte, puerta, fuera ritmo=1.4: un punto queda fuera del NavMesh (116,9, 2,3); voy en línea recta
- línea 287: eldran anda a paso_silla, rodeo_sur, rodeo_norte, puerta, fuera ritmo=1.4: un punto queda fuera del NavMesh (116,9, 3,3); voy en línea recta
- línea 287: eldran anda a paso_silla, rodeo_sur, rodeo_norte, puerta, fuera ritmo=1.4: un punto queda fuera del NavMesh (116,4, 5,0); voy en línea recta
- línea 287: eldran anda a paso_silla, rodeo_sur, rodeo_norte, puerta, fuera ritmo=1.4: un punto queda fuera del NavMesh (116,4, 5,6); voy en línea recta
- línea 261: plano conjunto will eldran desde=cam_mesa_dos altura=1.20 altura_foco=0.85 lente=44 suave=0: algo del decorado se mete delante y tapa un 48% del cuadro. Prueba lado= o desde=.

## Silencios largos entre frases (4)

- 0:31,4 → 0:34,2: 2,7 s sin voz entre EVT_MANANA_05 y EVT_MANANA_06
- 1:41,1 → 1:43,9: 2,8 s sin voz entre EVT_MANANA_13 y EVT_MANANA_14
- 1:49,5 → 1:53,1: 3,5 s sin voz entre EVT_MANANA_15 y EVT_MANANA_15b
- 1:59,1 → 2:01,7: 2,7 s sin voz entre EVT_MANANA_15b y EVT_MANANA_16

## Línea de tiempo

| Tiempo | Qué | Detalle |
|---|---|---|
| 0:00,0 | **@vela** |  |
| 0:00,0 | plano 1 | medio de will · 5,3 s · lente 44° |
| 0:00,0 | efecto | Bandas de cine: mostrar (0,5s) |
| 0:00,8 | will | «Una llama pequeña. Solo la mecha…» (3,2 s) · pose |
| 0:04,0 | efecto | Entrada HoldRelease → velaControlada |
| 0:04,3 | efecto | VFX: VFX_VelaPequena en vela |
| 0:04,3 | efecto | SFX: Player_Damage |
| 0:04,3 | efecto | SFX: SFX_Impacto_Fuego_Pequeno |
| 0:04,3 | efecto | Gesto: Player → CastingDamage01 ×2 |
| 0:04,3 | efecto | VFX: VFX_VelaGrande en vela |
| 0:04,3 | efecto | VFX: VFX_Hechizo_Destello_Fuego en vela |
| 0:04,3 | efecto | VFX: VFX_VelaPequena en vela |
| 0:04,3 | efecto | VFX: VFX_Manga en Player |
| 0:05,3 | plano 2 | conjunto de will y eldran · 6,8 s · lente 36° |
| 0:05,3 | eldran | «Vaya. Ni una chispa de más.» (4,2 s) |
| 0:05,3 | eldran | «Prometedor… si lo que quieres es declararle la guerra a tu ropa.» (5,3 s) · pose |
| 0:06,6 | **@bollo** |  |
| 0:06,6 | efecto | Música: MANANA_DESPUES |
| 0:06,7 | efecto | Dejar PROP_Bollo: Opening01_Stop, suelta en 0,133 s del gesto |
| 0:12,1 | plano 3 | medio de eldran · 3,8 s · lente 34° |
| 0:12,1 | eldran | «Y perdona por lo del bosque. No debí mandarte solo.» (3,8 s) |
| 0:16,0 | plano 4 | medio de will · 3,2 s · lente 34° |
| 0:16,0 | will | «Oye, ayer lo viste todo, ¿no?» (3,2 s) · pose |
| 0:19,2 | plano 5 | primer plano de eldran · 2,8 s · lente 34° |
| 0:19,2 | eldran | «Claro. Si te estaba ayudando.» (2,8 s) |
| 0:22,0 | plano 6 | medio de will · 5,8 s · lente 34° |
| 0:22,0 | will | «Ya, pero cuando le lancé el fuego… Era así de grande. No, más. Así.» (5,8 s) · pose |
| 0:27,7 | plano 7 | dos: will y eldran · 3,7 s · lente 44° |
| 0:27,7 | eldran | «Anoche, cuando me lo contaste, era la mitad.» (3,7 s) |
| 0:31,4 | **@origen** |  |
| 0:31,4 | plano 8 | medio de will · 13,3 s · lente 44° |
| 0:34,2 | will | «¿Siempre he podido hacer esto?» (3,5 s) |
| 0:38,6 | eldran | «No lo sé. Hay algo de la noche en que te encontré que nunca te he contado.» (6,2 s) |
| 0:44,8 | plano 9 | dos: will y eldran · 12,2 s · lente 44° |
| 0:44,8 | eldran | «Ya sabes que te encontré en un carro volcado, junto al camino.» (4,3 s) |
| 0:49,3 | eldran | «Estaba todo tan oscuro que no se veía nada. Si os encontré fue porque había una luz.» (7,3 s) |
| 0:56,9 | plano 10 | medio de eldran · 7,0 s · lente 36° |
| 0:56,9 | eldran | «La mujer que iba contigo tenía una llamita encendida entre los dedos, para que alguien os viera.» (7,0 s) |
| 1:03,9 | plano 11 | primer plano de will · 4,0 s · lente 42° |
| 1:03,9 | eldran | «Todavía respiraba. Nunca supe quién era.» (4,0 s) |
| 1:07,9 | plano 12 | medio de eldran · 9,7 s · lente 36° |
| 1:07,9 | eldran | «Solo le dio tiempo a pedirme que no te dejara solo. Luego la llama se apagó.» (5,8 s) |
| 1:14,5 | eldran | «Se lo prometí.» (2,8 s) |
| 1:17,3 | **@eldran** |  |
| 1:17,6 | plano 13 | primer plano de will · 3,2 s · lente 42° |
| 1:17,6 | will | «¿Y tú? ¿Qué sabes tú de magia?» (3,2 s) |
| 1:20,8 | plano 14 | primer plano de eldran · 4,9 s · lente 32° |
| 1:20,8 | eldran | «¿Otra vez? Ya hemos hablado de eso mil veces.» (4,9 s) |
| 1:25,7 | plano 15 | dos: will y eldran · 4,5 s · lente 44° |
| 1:26,5 | will | «No. Mil veces me has cambiado de tema.» (3,1 s) |
| 1:30,2 | plano 16 | conjunto de selene y eldran_joven y silas_joven · 7,0 s · lente 46° |
| 1:30,2 | eldran | «Hace cuarenta años me fui con unos amigos a buscar algo que no debíamos buscar.» (7,0 s) |
| 1:30,2 | efecto | Fogonazo (0,25s) |
| 1:30,2 | efecto | Post-procesado: Flashback_Recuerdo (0,2s) |
| 1:37,1 | plano 17 | medio de selene · 0,8 s · lente 40° |
| 1:37,9 | plano 18 | medio de eldran_joven · 3,2 s · lente 44° |
| 1:37,9 | eldran | «Volvimos todos menos una.» (3,2 s) |
| 1:41,1 | plano 19 | dos: eldran_joven y silas_joven · 2,8 s · lente 46° |
| 1:43,9 | plano 20 | medio de will · 2,8 s · lente 44° |
| 1:43,9 | will | «¿Qué buscabais?» (2,5 s) |
| 1:43,9 | efecto | Fogonazo (0,25s) |
| 1:43,9 | efecto | Post-procesado: DefaultVolumeProfile (1,2s) |
| 1:46,7 | plano 21 | dos: will y eldran · 2,8 s · lente 44° |
| 1:46,7 | eldran | «Otro día.» (2,8 s) |
| 1:49,5 | **@aro** |  |
| 1:49,5 | plano 22 | general de will y eldran · 3,5 s · lente 42° |
| 1:52,4 | efecto | Dejar PROP_Aro: Opening01_Stop, suelta en 0,133 s del gesto |
| 1:53,1 | plano 23 | detalle de el punto · 6,6 s · lente 44° |
| 1:53,1 | eldran | «Esto es lo que llevaba el demonio al cuello. Lo traje del bosque y lo guardé en sal.» (6,0 s) |
| 1:59,7 | plano 24 | conjunto de will y eldran · 2,1 s · lente 44° |
| 2:01,7 | plano 25 | medio de will · 6,4 s · lente 34° |
| 2:01,7 | will | «¿Y si aparece otro? La próxima vez puede que no sea yo quien esté en el bosque. Puede que sea un crío del pueblo.» (6,4 s) · pose |
| 2:08,1 | plano 26 | dos: will y eldran · 12,8 s · lente 44° |
| 2:08,1 | eldran | «Por eso necesitas a alguien que sepa de verdad. Yo no puedo enseñarte, y aquí nadie sabe leer estas runas.» (7,9 s) |
| 2:16,2 | eldran | «Hay una hechicera en el Bosque Prohibido. Se llama Estela.» (4,8 s) |
| 2:21,0 | plano 27 | primer plano de eldran · 6,7 s · lente 34° |
| 2:21,0 | eldran | «Dicen que es muy poderosa y, según la mitad de las historias, imposible de soportar.» (6,7 s) |
| 2:27,6 | plano 28 | primer plano de will · 2,5 s · lente 34° |
| 2:27,6 | will | «¿Y según la otra mitad?» (2,5 s) · pose |
| 2:30,1 | plano 29 | dos: will y eldran · 7,9 s · lente 44° |
| 2:30,1 | eldran | «También. Pero si alguien puede enseñarte a no incendiarte cada vez que estornudas, es ella.» (7,9 s) |
| 2:38,0 | plano 30 | primer plano de will · 2,5 s · lente 34° |
| 2:38,0 | will | «Iré.» (2,5 s) · pose |
| 2:40,5 | plano 31 | medio de eldran · 8,8 s · lente 34° |
| 2:40,5 | eldran | «Ya lo sé. Pero no así: primero, prepárate.» (4,8 s) |
| 2:45,4 | eldran | «Llévate un trozo del aro. Que ella lo vea.» (3,8 s) |
| 2:49,2 | **@salida** |  |
| 2:49,2 | plano 32 | conjunto de will y eldran · 6,8 s · lente 36° |
| 2:55,6 | efecto | Bandas de cine: retirar (0,5s) |

## Qué hace cada personaje

### will (Player)

- 0:00,8–0:04,0 habla
- 0:00,9 gesto Talk01
- 0:06,0 gesto HeadNod01
- 0:12,8 gesto HeadShake01
- 0:16,0–0:19,2 habla
- 0:16,0 gesto Talk02
- 0:19,9 gesto HeadNod01
- 0:22,0–0:27,7 habla
- 0:22,1 gesto Talk03
- 0:22,4 gesto CastingDamage01
- 0:23,8 gesto Cheer01
- 0:25,0 gesto HeadShake01
- 0:26,0 gesto Cheer02
- 0:28,4 gesto HeadNod01
- 0:31,5–0:33,5 anda de (115,7, 2,6) a (116,4, 0,7)
- 0:33,5 gesto SitMedium_Begin
- 0:34,1 bucle SitMedium_Loop
- 0:34,2–0:37,7 habla
- 0:56,5 bucle Knockdown01_Ground
- 1:17,6–1:20,8 habla
- 1:25,7 bucle SitMedium_Loop
- 1:26,5–1:29,6 habla
- 1:26,7 gesto Angry01
- 1:43,9–1:46,4 habla
- 1:53,1 gesto SitMedium_Exit
- 1:54,0 reposo 
- 1:59,7–2:01,8 anda de (116,4, 0,7) a (115,7, 2,6)
- 2:01,7–2:08,1 habla
- 2:01,8 gesto Talk02
- 2:04,4 gesto Talk03
- 2:08,8 gesto HeadShake01
- 2:16,9 gesto HeadNod01
- 2:21,7 gesto HeadShake01
- 2:27,6–2:30,1 habla
- 2:27,7 gesto Talk02
- 2:30,8 gesto HeadShake02
- 2:38,0–2:40,5 habla
- 2:38,1 gesto Talk03
- 2:38,3 gesto HeadNod01
- 2:41,2 gesto HeadShake01
- 2:46,1 gesto HeadNod01
- 2:55,6 reposo 

### eldran (NPC_Eldran)

- 0:05,3–0:09,5 habla
- 0:05,3–0:10,6 habla
- 0:05,3 aparece
- 0:05,4–0:06,6 anda de (116,3, 4,9) a (115,3, 4,0)
- 0:06,6 reposo 
- 0:06,6 gesto Opening01_Stop
- 0:07,4–0:11,5 anda de (115,3, 4,0) a (114,4, 2,2)
- 0:11,4 gesto SitMedium_Begin
- 0:12,1 bucle SitMedium_Loop
- 0:12,1–0:16,0 habla
- 0:12,2 gesto HeadNod01
- 0:19,2–0:22,0 habla
- 0:19,2 gesto HeadNod01
- 0:27,7–0:31,4 habla
- 0:37,7 gesto SitMedium_Exit
- 0:38,6 reposo 
- 0:38,6–0:44,8 habla
- 0:38,7–0:40,7 anda de (114,5, 2,3) a (116,4, 1,2)
- 0:40,7 gesto SitMedium_Begin
- 0:41,3 bucle SitMedium_Loop
- 0:44,8–0:49,1 habla
- 0:49,3–0:56,5 habla
- 0:56,9–1:03,9 habla
- 1:03,9–1:07,9 habla
- 1:07,9–1:13,7 habla
- 1:14,5–1:17,3 habla
- 1:14,6 gesto HeadNod01
- 1:20,8–1:25,7 habla
- 1:30,2–1:37,1 habla
- 1:37,9–1:41,1 habla
- 1:46,7–1:49,5 habla
- 1:46,8 gesto HeadNod01
- 1:49,5 gesto SitMedium_Exit
- 1:50,5 reposo 
- 1:50,6–1:52,4 anda de (116,3, 1,2) a (115,3, 3,1)
- 1:52,3 gesto Opening01_Stop
- 1:53,1–1:59,1 habla
- 1:53,1–1:55,5 anda de (115,3, 3,1) a (114,5, 2,3)
- 1:55,5 gesto SitMedium_Begin
- 1:56,1 bucle SitMedium_Loop
- 2:08,1–2:16,0 habla
- 2:08,2 gesto Talk01
- 2:16,2–2:21,0 habla
- 2:16,2 gesto HeadShake01
- 2:21,0–2:27,6 habla
- 2:21,0 gesto Talk02
- 2:30,1–2:38,0 habla
- 2:30,2 gesto Talk01
- 2:40,5–2:45,3 habla
- 2:40,6 gesto HeadNod01
- 2:45,4–2:49,2 habla
- 2:45,5 gesto Talk01
- 2:49,2 gesto SitMedium_Exit
- 2:50,2 reposo 
- 2:50,3–2:54,8 anda de (114,5, 2,2) a (116,4, 5,6)
- 2:54,8 desaparece

### selene (Actor_Selene)

- 1:30,2 aparece
- 1:30,2–1:37,7 anda de (300,0, 3,5) a (300,0, 9,5)
- 1:37,3 gesto SenseSomethingStart_NoWeapon
- 1:38,0–1:41,3 anda de (300,0, 9,5) a (300,0, 12,0)
- 1:38,5 desaparece

### eldran_joven (Actor_EldranJoven)

- 1:30,2 aparece
- 1:30,2–1:34,1 anda de (298,5, 1,5) a (298,5, 4,5)
- 1:38,0–1:39,4 anda de (298,5, 4,6) a (298,5, 7,5)
- 1:38,8 gesto DefendHit_NoWeapon
- 1:41,1 gesto Beg01
- 1:43,9 desaparece

### silas_joven (Actor_SilasJoven)

- 1:30,2 aparece
- 1:30,2–1:34,1 anda de (301,5, 1,2) a (301,5, 4,2)
- 1:41,2–1:43,5 anda de (301,5, 4,3) a (300,4, 7,2)
- 1:43,9 desaparece

