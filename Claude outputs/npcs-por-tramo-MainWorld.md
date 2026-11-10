# Qué hace cada NPC por tramo · MainWorld

## Roster y configuración ambiental

### NpcRoster_MainWorld

- **NPC_Oliver** · prefab `Oliver` · spawn `SPAWN_Oliver`
  - ambiente `NPC_Ambient_Oliver`: pasea=False (r=6) · actividad=None · oficio=True `mercado_oliver` · refugio=False
- **_LIAM** · prefab `_LIAM` · spawn `SPAWN__LIAM` · aparece desde `NARRATIVE_FLAG:LIAM_APARECE`
- **NPC_Eldran** · prefab `Eldran` · spawn `SPAWN_Eldran`
  - ambiente `NPC_Ambient_Eldran`: pasea=False (r=6) · actividad=None · oficio=True `mercado_eldran` · refugio=False
  - en el prefab, **DiscusionEnBucle**.tramo: `NPC_Eldran` con `NPC_Victoria` · empieza señal [OLIVER_GREETING_START, PROLOGUE_DONE, HOOK_DISCUSION_START] · empieza misión activa [ELDRAN_MISSION1, WILL_MISSION0] · acaba señal [PERAS_START] · acaba misión completada [ELDRAN_MISSION1]
- **(sin id)** · prefab `Tabernera` · spawn `SPAWN_Tabernera`
- **NPC_Victoria** · prefab `Victoria` · spawn `SPAWN_Victoria`
  - ambiente `NPC_Ambient_Victoria`: pasea=False (r=6) · actividad=None · oficio=True `mercado_victoria` · refugio=False
- **NPC_Tomasa** · prefab `Tomasa` · spawn `SPAWN_Tomasa` en (17, -137)
- **NPC_Boticaria** · prefab `Boticaria` · spawn `SPAWN_Boticaria` en (23, -135) · aparece desde `NARRATIVE_FLAG:CAP2_INICIADO`
  - ambiente `NPC_Ambient_Boticaria`: pasea=False (r=6) · actividad=None · oficio=False `` · refugio=False
- **NPC_Renard** · prefab `Renard` · spawn `SPAWN_Renard` en (-236, 3) · aparece desde `NARRATIVE_FLAG:CAP2_INICIADO`
- **NPC_Erika** · prefab `Erika` · spawn `SPAWN_Erika` en (-20, -125) · aparece desde `NARRATIVE_FLAG:CAP2_INICIADO`
- **npc_aldona** · prefab `Aldona` · spawn `SPAWN_Aldona` en (7, -122)
- **NPC_Posadero** · prefab `Manuel` · spawn `SPAWN_Posadero` · aparece desde `NARRATIVE_FLAG:CAP3_INICIADO`
- **NPC_Lucia** · prefab `AmigaDelNiñoPez` · spawn `SPAWN_Lucia` · aparece desde `NARRATIVE_FLAG:CAP3_INICIADO`
- **NPC_Sargento** · prefab `Guard` · spawn `SPAWN_Sargento` · aparece desde `NARRATIVE_FLAG:CAP3_INICIADO`
- **NPC_GuardiaCruce1** · prefab `Guard` · spawn `SPAWN_GuardiaCruce1` · aparece desde `NARRATIVE_FLAG:CAP3_INICIADO`
- **NPC_GuardiaCruce2** · prefab `Guard` · spawn `SPAWN_GuardiaCruce2` · aparece desde `NARRATIVE_FLAG:CAP3_INICIADO`
- **NPC_Contrabandista1** · prefab `Jorge` · spawn `SPAWN_Contrabandista1` · aparece desde `NARRATIVE_FLAG:CAP3_INICIADO`
- **NPC_Contrabandista2** · prefab `Boy_Pirate` · spawn `SPAWN_Contrabandista2` · aparece desde `NARRATIVE_FLAG:CAP3_INICIADO`
- **NPC_ContrabandistaJefe** · prefab `Boy_Pirate` · spawn `SPAWN_ContrabandistaJefe` · aparece desde `NARRATIVE_FLAG:CAP3_INICIADO`
- **_ESTELA** · prefab `_ESTELA` · spawn `SPAWN__ESTELA` · aparece desde `NARRATIVE_FLAG:ESTELA_APARECE`

### NpcRoster_PrologoValle

- **NPC_Archimago** · prefab `_WILL_ORIGINAL` · spawn `SPAWN_NPC_Archimago`
- **NPC_Liora** · prefab `Tendera` · spawn `SPAWN_NPC_Liora`
- **NPC_MagoOscuro** · prefab `_MAGO_OSCURO` · spawn `SPAWN_NPC_MagoOscuro`
- **NPC_Aldeano_01** · prefab `TownNpc#1` · spawn `SPAWN_NPC_Aldeano_01`
- **NPC_Aldeano_02** · prefab `TownNpc#2` · spawn `SPAWN_NPC_Aldeano_02`
- **NPC_Aldeano_03** · prefab `TownNpc#3` · spawn `SPAWN_NPC_Aldeano_03`
- **NPC_Aldeano_04** · prefab `TownNpc#4` · spawn `SPAWN_NPC_Aldeano_04`
- **NPC_Aldeano_05** · prefab `Nina_Prologo` · spawn `SPAWN_NPC_Aldeano_05`
- **NPC_Aldeano_06** · prefab `TownNpc#6` · spawn `SPAWN_NPC_Aldeano_06`
- **NPC_Aldeano_07** · prefab `TownNpc#7` · spawn `SPAWN_NPC_Aldeano_07`
- **NPC_Aldeano_08** · prefab `TownNpc#8` · spawn `SPAWN_NPC_Aldeano_08`
- **NPC_Aldeano_09** · prefab `TownNpc#9` · spawn `SPAWN_NPC_Aldeano_09`
- **NPC_Aldeano_10** · prefab `TownNpc#10` · spawn `SPAWN_NPC_Aldeano_10`

### NpcRoster_TorreDeLiam

- **LIAM_TORRE** · prefab `_LIAM` · spawn `SPAWN_LIAM_TORRE`
- **TOBI_TORRE** · prefab `Tobi` · spawn `SPAWN_TOBI_TORRE`

## Componentes de la escena con tramo de historia

- `WORLD/Hitos del mapa — faro, velero, puentes, cascada/Santuario ancestral — recinto de piedra y ruinas/Umbral del Sendero (generado)` **ActivoEnTramo**.tramo: · empieza señal [SENDERO_RITUAL_LISTO] · acaba señal [SENDERO_PORTAL_ABIERTO]
- `WORLD/Hitos del mapa — faro, velero, puentes, cascada/Santuario ancestral — recinto de piedra y ruinas/Umbral del Sendero (generado)` **ActivoEnTramo**.tramo: · empieza señal [SENDERO_PORTAL_ABIERTO]
- `GAMEPLAY/Oficios del mercado/Oficio_Victoria` **NPCWorkRoute**.tramo: puesto=`mercado_victoria` recogida=(-2, -145) entrega=(-1, -147) terminaAlVaciar=True alTerminar=`SP_Victoria` · empieza misión activa [WILL_MISSION1] · acaba misión completada [WILL_MISSION1]
- `GAMEPLAY/Oficios del mercado/Oficio_Eldran` **NPCWorkRoute**.tramo: puesto=`mercado_eldran` recogida=(0, -144) entrega=(0, -142) terminaAlVaciar=True alTerminar=`-` · empieza misión activa [WILL_MISSION1] · acaba señal [ESTRELLA_COMPRADA] · acaba misión iniciada [ELDRAN_SIGUEME] · acaba misión completada [WILL_MISSION1]
- `GAMEPLAY/Oficios del mercado/Oficio_Oliver` **NPCWorkRoute**.tramo: puesto=`mercado_oliver` recogida=(7, -139) entrega=(5, -141) terminaAlVaciar=True alTerminar=`-` · empieza misión activa [WILL_MISSION1] · acaba señal [ESTRELLA_COMPRADA] · acaba misión iniciada [ELDRAN_SIGUEME] · acaba misión completada [WILL_MISSION1]
- `Capítulos de la demo (generado)/Cap3 · llegadas/Llegada a la aldea del mar` **ActivoEnTramo**.tramo: · empieza señal [CAP3_ALDEA_EMPIEZA] · acaba señal [CAP3_PISTAS_EMPIEZA]
- `Capítulos de la demo (generado)/Cap4 · llegadas/Llegada al claro de Estela` **ActivoEnTramo**.tramo: · empieza señal [CAP4_BOSQUE_EMPIEZA] · acaba señal [CAP4_CASA_EMPIEZA]
- `Capítulos de la demo (generado)/Cap4 · llegadas/Llegada a la arena del Gólem` **ActivoEnTramo**.tramo: · empieza señal [CAP4_GOLEM_EMPIEZA] · acaba señal [CAP4_BIFURCACION_EMPIEZA]
- `Capítulos de la demo (generado)/Cap4 · llegadas/Llegada a la bifurcación` **ActivoEnTramo**.tramo: · empieza señal [CAP4_BIFURCACION_EMPIEZA] · acaba señal [CAP4_LIAM_EN_EL_GRUPO]
- `Capítulos de la demo (generado)/Cap3 · inspección/Pista · la cerca del huerto` **ActivoEnTramo**.tramo: · empieza señal [CAP3_PISTAS_EMPIEZA] · acaba señal [CAP3_NOCHE_EMPIEZA]
- `Capítulos de la demo (generado)/Cap3 · inspección/Pista · el cobertizo de las redes` **ActivoEnTramo**.tramo: · empieza señal [CAP3_PISTAS_EMPIEZA] · acaba señal [CAP3_NOCHE_EMPIEZA]
- `Capítulos de la demo (generado)/Cap4 · inspección/Casa · la carta` **ActivoEnTramo**.tramo: · empieza señal [CAP4_CASA_EMPIEZA] · acaba señal [CAP4_CAMPANA_EMPIEZA]
- `Capítulos de la demo (generado)/Cap4 · inspección/Casa · los apodos` **ActivoEnTramo**.tramo: · empieza señal [CAP4_CASA_EMPIEZA] · acaba señal [CAP4_CAMPANA_EMPIEZA]
- `Capítulos de la demo (generado)/Cap4 · inspección/Casa · la cena` **ActivoEnTramo**.tramo: · empieza señal [CAP4_CASA_EMPIEZA] · acaba señal [CAP4_CAMPANA_EMPIEZA]
- `Capítulos de la demo (generado)/Cap4 · inspección/Casa · la cama` **ActivoEnTramo**.tramo: · empieza señal [CAP4_CASA_EMPIEZA] · acaba señal [CAP4_CAMPANA_EMPIEZA]
- `Capítulos de la demo (generado)/Cap4 · inspección/Vado · la bota de Liam` **ActivoEnTramo**.tramo: · empieza señal [CAP4_LIAM_EN_EL_GRUPO] · acaba señal [CH_Cap4_BACK_1]
- `Capítulos de la demo (generado)/Cap3 · atrezo/Hoguera de la acampada (tramo)` **ActivoEnTramo**.tramo: · empieza señal [CAP3_NOCHE_EMPIEZA] · acaba señal [CH_Cap3_BACK_1]
- `Capítulos de la demo (generado)/Cap4 · atrezo/Cartel del entrenamiento (tramo)` **ActivoEnTramo**.tramo: · empieza señal [CAP4_CAMPANA_EMPIEZA] · acaba señal [CAP4_CAMPANA_FIN]
- `Capítulos de la demo (generado)/Cap4 · atrezo/La bota de Liam (tramo)` **ActivoEnTramo**.tramo: · empieza señal [CAP4_LIAM_EN_EL_GRUPO] · acaba señal [CAP4_BOTA_RECOGIDA]
- `Capítulos de la demo (generado)/Cap3 · campamento` **ActivoEnTramo**.tramo: · empieza señal [CAP3_NOCHE_EMPIEZA] · acaba señal [CAP3_CONTRABANDISTAS_FIN]
- `Capítulos de la demo (generado)/Cap3 · campamento/PlumaBrillante (tramo de la decisión)` **ActivoEnTramo**.tramo: · empieza señal [CAP3_DECIDE_EMPIEZA] · acaba señal [CAP3_CONTRABANDISTAS_FIN]
- `Capítulos de la demo (generado)/Cap3 · campamento/Claro (tramo de la decisión)` **ActivoEnTramo**.tramo: · empieza señal [CAP3_DECIDE_EMPIEZA] · acaba señal [CAP3_CONTRABANDISTAS_FIN]
- `Capítulos de la demo (generado)/Cap4 · campana y barro/La campana (tramo)` **ActivoEnTramo**.tramo: · empieza señal [CAP4_CAMPANA_EMPIEZA] · acaba señal [CAP4_CAMPANA_FIN]
- `Capítulos de la demo (generado)/Cap4 · campana y barro/Figuras de barro (tramo)` **ActivoEnTramo**.tramo: · empieza señal [CAP4_CAMPANA_EMPIEZA] · acaba señal [CAP4_CAMPANA_FIN]

## Agendas (Resources/Agendas)

- **_LIAM** (`Agenda__LIAM`)
  - «Cap. 4 · en la loma de la campana» · presencia=EnUnaMarca `Cap4_LiamLoma` · frases=0 · empieza señal [CAP4_CAMPANA_FIN] · acaba señal [CAP4_GOLEM_EMPIEZA]
  - «Cap. 4 · en el saliente del Gólem» · presencia=EnUnaMarca `Cap4_LiamSaliente` · frases=0 · empieza señal [CAP4_GOLEM_EMPIEZA] · acaba señal [CAP4_BIFURCACION_EMPIEZA]
  - «Cap. 4 · en la bifurcación» · presencia=EnUnaMarca `Cap4_LiamBifurcacion` · frases=0 · empieza señal [CAP4_BIFURCACION_EMPIEZA] · acaba señal [CAP4_LIAM_EN_EL_GRUPO]
- **Actor_ChucheReino1** (`Agenda_Actor_ChucheReino1`)
  - «Chuchelandia · el reino libre» · presencia=SinCambios `` · frases=2 · empieza señal [CHUCHE_REINO_LIBRE] · acaba señal [CHUCHE_SALON_LLEGADA]
- **Actor_ChucheReino2** (`Agenda_Actor_ChucheReino2`)
  - «Chuchelandia · el reino libre» · presencia=SinCambios `` · frases=1 · empieza señal [CHUCHE_REINO_LIBRE] · acaba señal [CHUCHE_SALON_LLEGADA]
- **Actor_ChucheReino3** (`Agenda_Actor_ChucheReino3`)
  - «Chuchelandia · el reino libre» · presencia=SinCambios `` · frases=1 · empieza señal [CHUCHE_REINO_LIBRE] · acaba señal [CHUCHE_SALON_LLEGADA]
- **Actor_ChucheReino4** (`Agenda_Actor_ChucheReino4`)
  - «Chuchelandia · el reino libre» · presencia=SinCambios `` · frases=1 · empieza señal [CHUCHE_REINO_LIBRE] · acaba señal [CHUCHE_SALON_LLEGADA]
- **Actor_ChucheReino5** (`Agenda_Actor_ChucheReino5`)
  - «Chuchelandia · el reino libre» · presencia=SinCambios `` · frases=1 · empieza señal [CHUCHE_REINO_LIBRE] · acaba señal [CHUCHE_SALON_LLEGADA]
- **Actor_ChucheReino6** (`Agenda_Actor_ChucheReino6`)
  - «Chuchelandia · el reino libre» · presencia=SinCambios `` · frases=2 · empieza señal [CHUCHE_REINO_LIBRE] · acaba señal [CHUCHE_SALON_LLEGADA]
- **NPC_Boticaria** (`Agenda_Boticaria`)
  - «Siempre · la poción» · presencia=SinCambios `` · frases=1 · siempre
- **NPC_Eldran** (`Agenda_Eldran`)
  - «Tras el Cap. 2 · en la capital» · presencia=NoEsta `` · frases=0 · empieza señal [CH_Cap2_BACK_1]
- **NPC_Contrabandista1** (`Agenda_NPC_Contrabandista1`)
  - «Cap. 3 · antes de la noche» · presencia=NoEsta `` · frases=0 · acaba señal [CAP3_NOCHE_EMPIEZA]
  - «Cap. 3 · detenidos» · presencia=NoEsta `` · frases=0 · empieza señal [CAP3_CONTRABANDISTAS_FIN]
- **NPC_Contrabandista2** (`Agenda_NPC_Contrabandista2`)
  - «Cap. 3 · antes de la noche» · presencia=NoEsta `` · frases=0 · acaba señal [CAP3_NOCHE_EMPIEZA]
  - «Cap. 3 · detenidos» · presencia=NoEsta `` · frases=0 · empieza señal [CAP3_CONTRABANDISTAS_FIN]
- **NPC_ContrabandistaJefe** (`Agenda_NPC_ContrabandistaJefe`)
  - «Cap. 3 · antes de la noche» · presencia=NoEsta `` · frases=0 · acaba señal [CAP3_NOCHE_EMPIEZA]
  - «Cap. 3 · detenidos» · presencia=NoEsta `` · frases=0 · empieza señal [CAP3_CONTRABANDISTAS_FIN]
- **NPC_GuardiaCruce1** (`Agenda_NPC_GuardiaCruce1`)
  - «Cap. 3 · el cruce» · presencia=SinCambios `` · frases=1 · siempre
- **NPC_GuardiaCruce2** (`Agenda_NPC_GuardiaCruce2`)
  - «Cap. 3 · el cruce» · presencia=SinCambios `` · frases=1 · siempre
- **NPC_Lucia** (`Agenda_NPC_Lucia`)
  - «Cap. 3 · las pistas» · presencia=SinCambios `` · frases=1 · empieza señal [CAP3_PISTAS_EMPIEZA] · acaba señal [CAP3_NOCHE_EMPIEZA]
  - «Cap. 3 · después» · presencia=SinCambios `` · frases=1 · empieza señal [CAP3_NOCHE_EMPIEZA]
- **NPC_Posadero** (`Agenda_NPC_Posadero`)
  - «Cap. 3 · tras la hoja» · presencia=SinCambios `` · frases=1 · empieza señal [CAP3_NOCHE_EMPIEZA]
- **NPC_Sargento** (`Agenda_NPC_Sargento`)
  - «Cap. 3 · vuelve al cruce» · presencia=EnUnaMarca `Cap3_Puesto` · frases=1 · empieza señal [CAP3_CONTRABANDISTAS_FIN]
- **NPC_Oliver** (`Agenda_Oliver`)
  - «Siempre · pistas para su colección» · presencia=SinCambios `` · frases=3 · siempre
- **NPC_Victoria** (`Agenda_Victoria`)
  - «Siempre · cuenta cosas de Eldran y de Will de pequeño» · presencia=SinCambios `` · frases=3 · siempre
