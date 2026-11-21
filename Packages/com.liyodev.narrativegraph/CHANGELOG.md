# Cambios

## 0.10.0 — 19 nov 2026

Revisión de calidad para la Asset Store (INC-1289).

- **Namespaces.** El runtime pasa a `Liyodev.NarrativeGraph` y el editor a `Liyodev.NarrativeGraph.Editor` (y `.Editor.Story`); el ejemplo, a `Liyodev.NarrativeGraph.Samples.ElFaro`. Todos los tipos serializados llevan `[MovedFrom]`, así que los grafos guardados conservan sus pasos y Unity los reescribe con el namespace nuevo al guardarlos.
- **Campos de los pasos privados.** Los campos públicos de `NarrativeNode` y de cada paso son ahora `[SerializeField] private` con el mismo nombre (los datos guardados no cambian). Lo que otro código lee o monta por código tiene propiedad (`Guid`, `Outputs`, `Position`, `DisplayTitle`, `QuestId`, `EventKey`…). El editor, el manual de pasos y las operaciones del asistente siguen usando el nombre del campo.
- **El grafo sin IA.** La dependencia de `com.liyodev.assistant` desaparece del paquete. El chat y las herramientas externas viven en el ensamblado opcional `Liyodev.NarrativeGraph.Editor.Assistant`, que solo se compila si el asistente está en el proyecto (`versionDefines` → `LIYODEV_ASSISTANT`).
- **Un solo camino de IA.** Se retira el servidor MCP propio del grafo (`Tools/mcp`, puerto 47821, `StoryBridge`). Las herramientas externas usan el servidor del asistente (puerto 47822) con el contexto `narrative-graph`, que está registrado aunque la ventana del grafo esté cerrada y deja elegir capítulo (`context_targets` / `context_select`).
- **Tiempos del editor.**
  - El índice del proyecto solo se rehace entero cuando cambia algo que lee: los catálogos declaran los ScriptableObject que leen (`Contributions.WatchAssetTypes`) y los ajustes del propio editor ya no lo invalidan. La sonda de tiempos apunta qué archivo lo obligó a rehacerse.
  - La caché por archivo se comprueba una vez por sesión del editor; después solo se vuelven a mirar los archivos que Unity ha importado (también tras la recarga de código de cada Play).
  - La pestaña «En vivo» en Play se rehace cuando la historia cambia (paso, señal, flag, contador o personaje), como mucho una vez por segundo, y cada cinco si nada avisa.
- API nueva del editor: `StoryGraphWindow.ActiveGraph`, `ActiveStep`, `ActiveChanged`, `SetActiveGraph`, `GraphChangedOutside`; `NarrativeGraphText.SerializedFields` / `SerializedField`; `INarrativeAssistantHost` y `NarrativeAssistant.Host`.
