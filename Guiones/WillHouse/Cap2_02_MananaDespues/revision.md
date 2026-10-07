# Revisión de T1-A · La mañana después

## Estado

Montaje guardado mediante el menú `El Sendero/Archivo/Capítulo 2/Montar la mañana después`. El checkpoint inicial de Cap2 permanece intacto. La misión está una sola vez en el catálogo de Start y ninguno de sus cuatro pasos se completa aquí.

Último horneado terminado: orden 469, 150,6 s, 25 frases alternativas incluidas, 20 planos, 11 efectos, 0 errores y 55 avisos. Se han revisado visualmente los veinte PNG y se han corregido el plano inicial de Eldran de espaldas, la obstrucción del plano de Will y la altura al sentarse. El storyboard muestrea poses de cuerpo; no ejecuta la interacción de input ni los efectos de partículas.

La fuente incluye un último ajuste pendiente de hornear: Eldran entra a 1,3 m/s para llegar antes del primer diálogo; se mantiene el plano de Will hasta 4,5 s para que se vea el resultado de la vela mientras comienza la reacción de Eldran. Las órdenes 471 (refrescar) y 472 (hornear con storyboard) esperan que se cierre el diálogo modal de la validación cruzada. El control de Windows de esta sesión no puede conectarse a su servicio nativo.

## Comprobaciones

- Editor.log: ningún `error CS` tras las tandas compiladas.
- Los ocho JSON modificados son válidos y no contienen IDs/claves duplicados.
- La presentación de subtítulos oficial pagina como máximo en dos líneas, por debajo del límite pedido.
- Validación Interactive ↔ Grafo: 0 errores, 17 avisos sobre quests/eventos anteriores; PREPARA_VIAJE no aparece entre los conflictos.
- Los dos textos EVT_MANANA_01a/01b comparten un único intervalo y tienen condiciones complementarias de velaControlada.
- El InputPromptBeat tiene modo HoldRelease, mínimo 0,8 s, máximo 1,5 s, ventana de 3 s y salida tolerante por expiración.
- La secuencia tiene AnchorEnvironment Bedroom de interior y devuelve el control; la música se restaura mediante AudioService.PedirMusicaDelLugar.

## Avisos del horneado

25 frases sin audio en Voices/es: usan la duración explícita del guion. Ocho avisos de recorridos fuera de NavMesh: los recorridos se hornean en coordenadas de la habitación. Dos avisos de ausencia de spawn del reparto: ambos actores tienen colocación inicial explícita. Veinte avisos de decorado por las envolventes del interior: los PNG muestran las caras despejadas.

El icono del Trozo del aro reutiliza IT_Key (llave metálica). El aro visible utiliza Ring01_a01; el RuneCollar del demonio es un efecto de partículas sin malla apropiada para este prop.

## Archivos creados

- Assets/Editor/Guion/Cap2MananaDespues.cs: montaje idempotente archivado.
- Assets/Scripts/Sequences/Beats/InputPromptBeat.cs: interacción genérica con limpieza al cerrar/desactivar el reproductor.
- Assets/_ITEMS/IT_TrozoDelAro.asset.
- Assets/_QUEST/PRINCIPALES/PRINCIPALES/Q_PREPARA_VIAJE.asset.
- Assets/_DIALOGUES/DIALOGUE NPCS/Eldran/DLG_ELDRAN_PREPARA_VIAJE_EN_CURSO.asset.
- Assets/_SEQUENCES/SEQ_MananaDespues.asset.
- Assets/_SEQUENCES/Guiones/Cap2_02_MananaDespues.guion.txt.
- Assets/_SEQUENCES/Guiones/Cap2_02_MananaDespues_Horneado.asset.
- Assets/_SEQUENCES/Guiones/EntradaMantenerSoltar.asset.
- Assets/_VFX/MananaDespues/: VFX_VelaPequena, VFX_VelaGrande y VFX_Manga.
- Metadatos de Unity correspondientes.
- Guiones/WillHouse/: captura del escenario, reparto, informe, recorridos y storyboard.
- Guiones/_ordenes/445–472: órdenes de montaje, captura, validación y horneado.

## Archivos modificados

- Assets/NarrativeGraph/Cap2.asset: tramo 7–15.
- Assets/Scenes/Interior/WillHouse.unity: secuencia, anchors y props.
- Assets/Scenes/Systems/Start.unity: una referencia nueva en questCatalog.
- Assets/Resources/Localization/{cinematics,quests,dialogues,other}_{es,en}.json: textos nuevos.
- Assets/NarrativeGraph/Runtime/Graph/NodeTypes/PlaceActorNode.cs: teleport oficial del jugador con entorno y visibilidad opcionales.
- Assets/Editor/Guion/{GuionTexto,HorneadorDeGuion,HornoCamaras,HornoSalida}.cs: condiciones genéricas por flag, colocación inicial, tiempos sin voz, altura de foco explícita y poses en storyboard.
- Assets/Scripts/Sequences/Guion/{GuionHorneado,ReproductorDeGuion}.cs: condiciones de líneas/efectos.
- Assets/Scripts/Player/PanicInputDetector.cs: progreso de HoldRelease sin éxito automático al alcanzar el mínimo.
- Assets/Scripts/Sequences/SequenceActor.cs y Assets/Scripts/World/ExteriorWorldRoot.cs: visibilidad sin pisar mallas faciales al traer actores del exterior al interior.

## Cambios incidentales de Unity pendientes de aclarar

Unity reserializó Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Lighting/Light05_a04.prefab al guardar dependencias. El montaje no lo edita directamente. Se preguntó si había cambios pendientes en ese prefab antes de intentar restaurarlo; falta respuesta. Unity también generó Assets/Scripts/Core/GameTags.cs.meta, cuyo script ya existía.

No se han probado en Play las dos ramas del QTE ni el tramo completo. No se han editado GDD.md, TRACKER.md, TorreDeLiam, la secuencia de Liam, Despertar ni Prólogo.