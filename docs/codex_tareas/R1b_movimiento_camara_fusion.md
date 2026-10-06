# R1b (INC-579) — Nadie encima de nadie, caminar sin atravesar props, cámara sin cabezas delante ni bajo el suelo, y la fusión del clímax

Lee antes docs/codex_tareas/comun.md. En paralelo trabaja R1a en: reacciones de escena, GestureBeat, ActuacionAutomatica, NPCEmotionController, AudioService, DayNightCycle, MusicBeat, VocalReactions. NO toques esos archivos/clases. NO toques Assets/Editor/ConstruirPrologoUltimaNoche.cs (lo hará una tarea posterior con lo que crees aquí).

## 1. Personajes que se montan unos encima de otros
Vídeo Prologo3: al ir a la carreta un vecino (NPC_Aldeano_06) acaba encima del Archimago en lugar de uno a cada lado (los dos van a M_Carreta con MoveToBeat); en la conversación de la plaza un NPC está dentro de otro (dos vecinos con sombrero morado superpuestos, 0:26–0:30) y otro queda en mitad del plano.
- En el sistema oficial de movimiento (SequenceActor + SequenceMovement y los beats que lo usan: MoveToBeat, PlaceAtMarkBeat, WalkPathBeat): si el destino ya está ocupado por otro actor (distancia < suma de SafeRadius + 0,25 m), elige una ranura libre alrededor del destino. Si el actor va hacia otro actor (towardsActorId) o a una marca donde ya hay alguien, la ranura preferida es a un lado (perpendicular a la línea entre ambos, lado más cercano libre), de forma que queden uno a cada lado y mirándose/mirando al objeto. Radio de ranura 0,9–1,3 m. Nada de solaparse al llegar ni al estar parados.
- Que esto también valga para PlaceAtMarkBeat (dos actores colocados en la misma marca).

## 2. Caminar sin comerse la farola ni saltar el abrevadero
Vídeo: el Archimago atraviesa una farola al avanzar hacia la plaza y, al ir al baile, «salta» por encima del abrevadero en vez de rodearlo. WalkPathBeat va en línea recta entre marcas pegándose al suelo con raycast, así que sube encima de los props.
- El pegado al suelo no puede subir a props: acepta solo suelo con desnivel ≤ 0,3 m respecto al paso anterior (o capas/colliders de suelo, como resuelva mejor el proyecto; ojo: los personajes viven en Default, ver AGENTS.md).
- Opción `esquivar` (true por defecto) en WalkPathBeat y en los MoveToBeat que no usen NavMeshAgent: entre marcas consecutivas, si hay NavMesh usa NavMesh.CalculatePath y sigue sus esquinas; si no hay o falla, barrido de cápsula y, si choca con un obstáculo que no es suelo ni personaje, inserta un punto de rodeo por el lado más corto de su bounds (+0,5 m). Ruta suavizada, sin giros bruscos.
- Si NavMeshAgent ya se usa en MoveToBeat, comprueba que farolas/abrevaderos no se atraviesen (si no tallan el NavMesh, dilo en la respuesta para que la tarea de montaje añada NavMeshObstacle con carve a esos props).

## 3. Cabezas delante de la cámara (ShotComposer)
Vídeo: en muchos planos de diálogo un personaje que no es el que habla tapa la cara del sujeto o llena medio cuadro en primer término: 0:24 (la cámara detrás de la cabeza de un vecino con sombrero morado, no se ve al que habla), 1:20–1:52 (cabezas en primer término durante la charla de la carreta y del baile; «Me encanta vuestro baile» queda tapado), 2:06 (la cabeza del Archimago negra y enorme tapa la escena), 2:58 (la cabeza de Liora enorme en primer término cuando debería verse su cara). 
- Un personaje que no sea sujeto ni secundario y cuya proyección (cápsula hasta HeadTopHeight) se solape con el rectángulo de la cara del sujeto → penalización de camino bloqueado (1000). Si solo invade el cuadro sin tapar la cara, la penalización actual de «alguien se come el cuadro» se escala con el área ocupada.
- El secundario (contraplano/sobre el hombro) puede ocupar como mucho ~25 % del ancho del cuadro y nunca tapar la cara del sujeto; si no hay candidata limpia, el compositor prefiere alejarse/abrir o rotar el ángulo antes que aceptar el plano tapado, y como último recurso un plano frontal más abierto del sujeto.
- La comprobación «lente dentro de alguien» usa la cápsula completa del personaje (no solo el pecho).
- Diagnóstico solo bajo instrumentación: un log por plano cuando la penalización final supere 250, con el desglose. 

## 4. La cámara se mete bajo el suelo
Vídeo 4:40–4:42: tras golpear al Mago en el aire, el plano que le sigue al caer deja la lente dentro del suelo (se ve el terreno cortado y las casas desde abajo). En ShotComposer y en el seguimiento en vivo (planos live), la lente nunca queda por debajo del suelo + 0,35 m (raycast hacia abajo desde arriba de la lente contra geometría que no sea personaje); si hay que subirla, recompón el ángulo para seguir viendo al sujeto.

## 5. Clímax: fusión del agujero negro y la protección del Archimago
Raúl: «el Mago Oscuro no lanza ahí nada; lo que debe ocurrir es: el agujero negro se empieza a hacer grande, al igual que el hechizo de protección del Archimago, hasta que ambos se mezclan y ocupan toda la pantalla, suena la explosión y ya el blanco».
Hoy el clímax usa ChoqueDeHechizosBeat (Assets/Scripts/Sequences/Beats/ChoqueDeHechizosBeat.cs: dos efectos viajan al punto medio y forcejean). Crea `FusionDeHechizosBeat` (archivo nuevo en la misma carpeta); NO borres todavía ChoqueDeHechizosBeat (lo retirará la tarea de montaje cuando deje de usarse).
- Entradas: efecto A = VFX ya registrado en el contexto (el agujero negro sobre el Mago; en el prólogo se registra como «CARGA_ESFERA», compruébalo) o prefab que nace en un actor; efecto B = VFX registrado o prefab que nace en el Archimago (su protección/luz: esfera blanca-azulada que lo envuelve).
- Fases (tiempo no escalado, todo configurable): (1) crecimiento simultáneo de A y B con curva ease-in durante `duracionCrecimiento` (≈4 s), sus centros se acercan al `puntoDeEncuentro` (por defecto el punto medio) mientras crecen; (2) mezcla: cuando se tocan, siguen creciendo hasta cubrir la pantalla de la cámara activa (calcula la escala con la distancia a la cámara y el FOV), con un velo a pantalla completa que mezcla negro-violeta y blanco (remolino o gradiente radial animado; puede ser un Canvas Overlay con RawImage y textura generada en runtime o un shader sencillo URP, sin assets externos) y temblor creciente; (3) `eventKeyExplosion` (SFX) y fundido a blanco total que se queda sostenido (la fase 9 empieza desde blanco). SFX de subida opcional (`eventKeySubida`) al empezar.
- Plano: el beat no mueve la cámara (eso lo hace el montaje con ShotBeat), pero expón `registrarPuntoComo` para que el montaje pueda encuadrar el punto de encuentro.
- Limpieza con Player.RegisterCleanup (velo destruido, escalas restauradas si se salta la secuencia). VFX con VfxPoolService como el resto (AGENTS.md).

## Terminado cuando
- Compila (revisa firmas con grep). TDD.md documenta las ranuras de llegada, esquivar, las reglas nuevas del compositor, el suelo mínimo de lente y FusionDeHechizosBeat. Fila INC-579 en TRACKER.md.
