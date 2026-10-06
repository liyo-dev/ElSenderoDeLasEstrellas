# Normas comunes para las tareas de Codex (prólogo, ronda Prologo3)

Proyecto Unity 6 (6000.6.2f1) + URP 17. Lee AGENTS.md antes de nada y respétalo (reglas de código, sistema oficial por responsabilidad, comentarios en presente y en español, nada de diario en comentarios).

- Contexto: Raúl grabó el prólogo (vídeo «Prologo3») y ha dado una lista de fallos. Claude (Cowork) los ha analizado y te reparte el trabajo en tareas. Claude revisa tus cambios después.
- Sistema de secuencias: SequenceDefinition / SequencePlayer / SequenceContext / SequenceBeat (Assets/Scripts/Sequences). El prólogo se monta con el menú «El Sendero/Secuencias/Prólogo: PREPARAR TODO», que ejecuta Assets/Editor/ConstruirPrologoUltimaNoche.cs (≈8400 líneas, CRLF: conserva CRLF).
- No escribas YAML de escenas, prefabs ni .asset a mano: cambios de assets mediante código de Editor con AssetDatabase / EditorSceneManager (regla INC-441).
- No toques archivos fuera de los indicados en tu tarea salvo que sea imprescindible para compilar; si lo haces, dilo en tu respuesta final.
- Al terminar: compila mentalmente con cuidado (no hay Unity en tu sandbox; Raúl tiene el editor abierto y recompila al volver). Revisa usings, nombres y firmas existentes con grep antes de usarlos.
- Documenta: fila en TRACKER.md (arriba del todo, misma estructura que INC-577) y sección en TDD.md para cada sistema/beat nuevo (qué hace, campos, cómo se usa). Comentarios sin fechas ni historia.
- Respuesta final: lista de archivos tocados, qué hace cada cambio, qué no has podido hacer y por qué, y los menús que Raúl debe ejecutar.
