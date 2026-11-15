# Grafo narrativo (Narrative Graph)

**El cerebro de la historia de tu juego en Unity.** Escribe la historia paso a paso, pruébala desde cualquier punto y deja que tus sistemas hagan el resto: diálogos, misiones, combate, cinemáticas, minijuegos.

No es un framework de juego. Es la capa de historia: el flujo, las misiones, los diálogos, los NPCs y las herramientas para depurar todo eso.

## Qué te quita de encima

- **Flujo:** pasos, ramas por condición, fork en paralelo, bucles y reintentos, esperas (señal, tiempo, interacción, misión, objeto), capítulos y subgrafos.
- **Misiones:** iniciar, completar pasos, completar, esperar, consultar estado y diario.
- **Diálogos:** líneas, elecciones y actor, con una presentación básica que puedes sustituir por la tuya.
- **NPCs:** quién está, dónde, qué dice si le hablas cuando la historia no le espera, esperar interacción e iconos.
- **Depuración:** mapa por escenas (esquema de sitios y personajes, sin fotos), pestaña «En vivo», breakpoints y **probar desde cualquier paso** con la historia hasta ahí.

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
- **Mapa de la historia**
- **Monitor de señales**
- **Dividir por capítulo...**
- **Guardar el manual de pasos**
- **Abrir el manual**
- **Comprobar el kit**
- **Ejemplos ▸ Montar / Quitar «El faro»**
- **Permitir herramientas externas (puerto local)**

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

## Requisitos

Unity 6 (6000.0 o superior), uGUI/TextMeshPro, Timeline y Newtonsoft JSON. El editor es bilingüe español/inglés.

## Ejemplo

- **El faro:** un capítulo corto con diálogo, decisión, misión, objeto y puntos de arranque, montado desde el menú de ejemplos.

## Documentación

- `Documentation/Manual de usuario.pdf`, también desde **Liyodev ▸ Grafo narrativo ▸ Abrir el manual**.
- **Liyodev ▸ Grafo narrativo ▸ Comprobar el kit** deja un informe en `Logs/NarrativeGraph/qa.txt` con los pasos, las peticiones, los puertos sin implementar y las muestras.

## Versión

0.9.0.
