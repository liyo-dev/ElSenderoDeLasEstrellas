# P4-T2 (INC-583) — Nadie corre en el sitio y nada de tiempos muertos por caminatas

Lee P4_comun.md y comun.md. En paralelo trabajan P4-T1 (cámara: ShotComposer, CameraBeats, SequencePlayer, CutInUI) y P4-T4 (audio de reacciones). NO toques sus archivos. Tus archivos: CinematicWorldBeats.cs (WalkPathBeat, AndarHasta, Tramo, PegarAlSuelo, Apartarse), SequenceMovement.cs, MoveToBeat/PlaceAtMarkBeat en ActorBeats.cs, SequenceActor.cs. No toques el constructor del prólogo.

## 1. Corredores atascados
Editor.log del pase: WalkPathBeat «no llegó en 25–30 s» para NPC_Aldeano_02/05/07/08/09 hacia M_Huida_05/07/08/09, M_Horno y M_Puente_Sal (fases 5 y 7). Raúl: «los NPCs se quedan en el sitio corriendo, pillados». Encuentra la causa real (candidatos: el límite de desnivel de 0,3 m de PegarAlSuelo frente a rampas/puente, Obstaculo() cortando el tramo y Tramo devolviendo sin avanzar, Apartarse() empujando a varios en sentidos opuestos en el embudo del puente, ReservarLlegada dando ranuras inalcanzables, rodeos hacia dentro de un prop). Reprodúcelo mentalmente con las marcas de la escena (lee sus posiciones en Prologo_Valle.unity con un menú o del YAML solo para leer) y arréglalo de raíz.
Además, red de seguridad general: vigilante de progreso en todo desplazamiento de secuencia (WalkPath, AndarHasta, MoveTo con agente): si en 1 s avanza < 0,15 m hacia su objetivo, replanifica (sin rodeo, luego línea directa ignorando a la gente) hasta 2 veces; si sigue atascado: si NO está en el encuadre de la cámara activa, se coloca en su destino (elipsis invisible); si está en cuadro, deja de andar (idle) mirando a su destino. Jamás piernas corriendo sin desplazarse. Un aviso de instrumentación por actor y tramo con la causa.

## 2. Elipsis: caminatas que no hacen esperar al montaje
Raúl: «la cámara se queda enfocando una escena y no pasa nada». Muchas veces es un beat de movimiento que la secuencia espera mientras el plano no enseña a nadie (0:14–0:24, 2:08–2:18).
- WalkPathBeat y MoveToBeat ganan `elipsis` (por defecto true) y `esperaMaxima` (s, 0 = automático = distancia/velocidad × 1,2 + 1): si el beat se espera y el actor NO está en el encuadre activo, al pasar la espera máxima se le coloca en su destino (o en el siguiente punto de la ruta) y el beat termina. Si está en cuadro, se deja andar (es lo que se está viendo).
- Fuera de cuadro desde el principio y sin nadie que lo vea: puede acortarse la caminata (llegar a la mitad del tiempo) — documenta el criterio.

## Terminado cuando
- Compila (Roslyn). TDD.md documenta el vigilante de progreso y la elipsis. Fila INC-583 en TRACKER.md con la causa del atasco.
