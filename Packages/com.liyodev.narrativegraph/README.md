# Grafo narrativo (Narrative Graph)

**El cerebro de la historia de tu juego en Unity.** Escribe la historia paso a paso, pruébala desde cualquier punto y deja que tus sistemas hagan el resto: diálogos, misiones, combate, cinemáticas, minijuegos.

No es un framework de juego. Es la capa de historia: el flujo, las misiones, los diálogos, los NPCs y las herramientas para depurar todo eso.

## Qué te quita de encima

- **Flujo:** pasos, ramas por condición, fork en paralelo, bucles y reintentos, esperas (señal, tiempo, interacción, misión, objeto), capítulos y subgrafos.
- **Misiones:** iniciar, completar pasos, completar, esperar, consultar estado y diario.
- **Diálogos:** líneas, elecciones y actor, con una presentación básica que puedes sustituir por la tuya.
- **NPCs:** quién está, dónde, qué dice si le hablas cuando la historia no le espera, esperar interacción e iconos.
- **Tarjetas:** un dibujo vectorial por categoría en el hueco de la antigua foto y retratos de los personajes; se cambian por PNGs propios desde **Herramientas ▸ Dibujos del grafo**.
- **Depuración:** mapa por escenas (esquema de sitios y personajes, sin fotos), pestaña «En vivo» con historial de señales, breakpoints y **probar desde cualquier paso** con la historia hasta ahí.

## Qué no hace (y no debe)

Combate, movimiento del jugador, física, cinemáticas, minijuegos, economía, UI del juego, formato de guardado ni multijugador. Eso es tuyo. El grafo lo **pide** con un evento y sigue cuando termina.

## Empezar en cinco minutos

1. Importa el paquete.
2. `Liyodev ▸ Grafo narrativo ▸ Ejemplos ▸ Montar «El faro»`.
3. Abre `Liyodev ▸ Grafo narrativo ▸ Abrir editor` (Ctrl+Mayús+N).
4. Elige un paso y dale a **Probar desde aquí**.
5. Cuando lo entiendas, cambia el ejemplo por tu historia y enchufa tus sistemas.

## Menú

`Liyodev ▸ Grafo narrativo`:

- **Abrir editor** (Ctrl+Mayús+N)
- **Nuevo grafo**
- **Herramientas**
  - Dividir por capítulo...
  - Mapa en ventana aparte
  - Dibujos del grafo...
  - Guardar el manual de pasos
  - Comprobar el kit
- **Ayuda ▸ Abrir el manual**
- **Ejemplos ▸ Montar / Quitar «El faro»**

## Conexiones con tu juego

El kit no obliga a un framework. Pide lo que necesita por interfaces pequeñas (`NarrativeServices`); tú implementas las que uses:

| Puerto | Para qué |
|---|---|
| `IQuestService` | misiones |
| `INarrativeDialoguePlayer` / `INarrativeChoicePresenter` | diálogo y elecciones |
| `INarrativeSpeechBubbles` | frases sueltas |
| `INarrativeInventory` | objetos |
| `INarrativeScreen` | fundidos |
| `INarrativeAudio` | música, SFX y voz |
| `INarrativePlayerControl` | bloquear y soltar al jugador |
| `INarrativeActorDirectory` / `INarrativeActorPlacement` | actores |
| `INarrativeCameraFocus` | cámara |
| `INarrativeSaveData` / `INarrativeFlagStore` / `INarrativeCounterStore` | estado y guardado |
| `INarrativeTextLocalizer` | traducción |
| `INarrativeSceneHooks` | escenas aditivas |
| `INarrativeTitleCardPresenter` | rótulos |

Los **servicios básicos** (`NarrativeBasicServices`, IMGUI) cubren diálogo, elección, frase, misión, inventario, guardado, fundido y actores para que puedas probar sin escribir código. Audio, cámara, bloqueo y rótulos se enchufan con un adaptador de pocas líneas.

### Lo que hace tu juego: «Pedir al juego»

Cualquier cosa que tu juego sepa hacer se pide con **Pedir al juego**: un id, datos (texto, número, asset) y salidas con nombre (`Victoria`, `Derrota`, `Hecho`, `Fallo`...). El juego lo atiende con un manejador y el grafo sigue por la salida que diga. Si nadie lo atiende, la historia no se cuelga: sigue por la salida de reserva y lo avisa en consola.

Esto vale para batallas, cinemáticas, minijuegos, viajes, desbloqueos, cambios de grupo o cualquier mecánica propia.

## Código

- Todo el kit vive en `Liyodev.NarrativeGraph` (runtime) y `Liyodev.NarrativeGraph.Editor` (editor; el lienzo y sus paneles en `Liyodev.NarrativeGraph.Editor.Story`). En tu código: `using Liyodev.NarrativeGraph;`.
- Los datos de cada paso son campos privados con `[SerializeField]`: se leen (y, los que se montan por código, se escriben) con propiedades, p. ej. `new WaitCustomEventNode { EventKey = "PUERTA", AcceptEarlierEvent = true }` o `node.Guid`, `node.Outputs`, `node.Position`. El editor y el asistente los cambian por su nombre de campo (`eventKey`), que es también el que sale en el manual de pasos.
- Dentro de un namespace propio que empiece por `Liyodev`, pon el `using Liyodev.NarrativeGraph;` dentro del namespace: si no, `NarrativeGraph` se lee como el namespace y no como el asset.

## Asistente de IA (opcional)

El grafo funciona entero sin IA. Si el proyecto tiene **Liyodev Assistant** (`com.liyodev.assistant`), se compila además el ensamblado `Liyodev.NarrativeGraph.Editor.Assistant` y aparecen el chat dentro del editor y las herramientas externas: el contexto `narrative-graph` del servidor MCP del asistente (puerto 47822), que está siempre disponible, también con el editor del grafo cerrado (elige capítulo con `context_select`). Si el asistente está importado como carpeta y no como paquete, añade el símbolo `LIYODEV_ASSISTANT` en *Player ▸ Scripting Define Symbols*.

## Requisitos

Unity 6 (6000.0 o superior), uGUI/TextMeshPro y Newtonsoft JSON. El asistente de Liyodev es opcional (ver arriba). El paso de reproducir un Timeline usa el paquete Timeline de tu proyecto si lo tienes. El editor es bilingüe español/inglés.

## Ejemplo

- **El faro:** un capítulo corto con diálogo, decisión, misión, objeto y puntos de arranque, montado desde el menú de ejemplos.

## Documentación

- `Documentation/Manual de usuario.pdf`, también desde **Liyodev ▸ Grafo narrativo ▸ Abrir el manual**.
- **Liyodev ▸ Grafo narrativo ▸ Comprobar el kit** deja un informe en `Logs/NarrativeGraph/qa.txt` con los pasos, las peticiones, los puertos sin implementar, las muestras y qué retrato usa cada personaje del mapa.

## Versión

0.10.0. Cambios en `CHANGELOG.md`.
