<p align="center">
  <img src="docs/images/banner.jpg" alt="El Sendero de las Estrellas" width="100%">
</p>

# 🌟 El Sendero de las Estrellas

RPG de acción/aventura en 3D, desarrollado en solitario por [Raúl Báez](https://github.com/liyo-dev).

**Motor:** Unity 6 (6000.6.2f1) · **Render pipeline:** URP 17.6 · **Estado:** en desarrollo activo

<p align="center">
  <img src="docs/screenshots/sendero-trio.jpg" width="100%" alt="Liam, Will y Estela en el Sendero">
</p>
<p align="center"><sub>Liam, Will y Estela en el Sendero (captura del motor, noviembre de 2026).</sub></p>

<p align="center">
  <img src="docs/screenshots/aldea.jpg" width="49%" alt="La aldea">
  <img src="docs/screenshots/castillo.jpg" width="49%" alt="El castillo">
</p>
<p align="center"><sub>Gameplay en el mundo abierto (agosto de 2026, antes del rediseño de personajes).</sub></p>

---

## 📖 Sobre el proyecto

Un RPG de acción/aventura construido sobre una arquitectura multi-escena aditiva: una escena `Start` con los managers persistentes (quests, diálogos, guardado, audio, input) y escenas de mundo que se cargan y descargan dinámicamente sobre ella. Los NPCs usan una FSM modular configurable desde ScriptableObjects, y la narrativa se dirige mediante un grafo de nodos visual (`NarrativeGraph`) más un sistema legacy de diálogo condicional para NPCs ya cerrados.

## 🎨 Universo y personajes

*Arte conceptual/promocional — no son capturas de gameplay.*

<p align="center">
  <img src="docs/images/valle.jpg" width="32%" alt="El valle inicial">
  <img src="docs/images/magia.jpg" width="32%" alt="Magia y hechizos">
  <img src="docs/images/amigos.jpg" width="32%" alt="Los tres amigos">
</p>

<p align="center">
  <img src="docs/images/personajes/will.jpg" width="24%" alt="Will">
  <img src="docs/images/personajes/liam.jpg" width="24%" alt="Liam">
  <img src="docs/images/personajes/estela.jpg" width="24%" alt="Estela">
  <img src="docs/images/personajes/eldran.jpg" width="24%" alt="Eldran">
</p>

## 📘 Documentación

Toda la documentación técnica del proyecto vive en un único documento — sin archivos `.md` sueltos por la raíz que se queden desactualizados:

| Documento | Contenido |
|---|---|
| **[TDD.md](TDD.md)** | Documento técnico de diseño completo y **fuente de verdad única**: arquitectura, API interna de cada sistema, reglas de código no negociables, invariantes del grafo narrativo, bugs conocidos, troubleshooting, diseños en curso, checklist de publicación en Steam y auditorías del proyecto. |
| **[GDD.md](GDD.md)** | Diseño, historia, personajes, hechizos y estado del contenido narrativo. |
| **[TRACKER.md](TRACKER.md)** | Incidencias, estado de implementación y validación pendiente. |
| **README.md** (este archivo) | Portada del repositorio — overview rápido para quien llega nuevo. |

Ante cualquier duda de arquitectura, se consulta `TDD.md` primero. Si vas a añadir documentación nueva de sustancia (una auditoría, un diseño, un checklist), añádela como sección de `TDD.md` en vez de crear otro `.md` suelto — la convención está descrita en `TDD.md` § 20.

## ✅ Estado del proyecto

Estado revisado el **7 de noviembre de 2026**.

**Historia de la demo, montada de principio a fin:**

- **Prólogo** rehecho: planos a la altura de los personajes, reacciones y vida de fondo, y postproceso de cine en las cinemáticas.
- **Capítulos 1 y 2**, probados completos.
- **Capítulo 3 · La pista del mar:** Tobías en el espejo, la aldea del mar con sus pistas y la acampada nocturna siguiendo a los contrabandistas, con una decisión que se toma jugando.
- **Capítulo 4 · El Bosque Prohibido y el Gólem:** Estela entra en el grupo, la casa entre raíces, la Prueba de la campana, el combate contra el Gólem y la bifurcación donde se une Liam. La demo termina en la bota del vado y pasa a los créditos.

**También en marcha:** Chuchelandia, el nivel del Sendero, la cinemática final y su música.

**Arte de personajes:** rediseño de los protagonistas (trajes, pelo de Will, botas de Estela), caras con estilo anime sencillo y emociones para todo el reparto, y niños con cuerpo propio (Tobías y Nina).

**Pendiente antes de publicar la demo:**

- Jugar a mano los combates del jefe contrabandista y del Gólem.
- Voces de los capítulos 3 y 4.
- Cambiar el atrezo provisional (la casa de Estela y la bota).
- Ficha y build de Steam.

Los detalles y las comprobaciones pendientes están en `TRACKER.md`; un arreglo aplicado no implica que esté confirmado en juego. Esta revisión del README no ejecuta tests ni certifica una build.

## 🚀 Quick start

1. Abre el proyecto con **Unity 6 (6000.6.2f1)** o superior.
2. La escena de entrada es `Assets/Scenes/Systems/Start.unity` — contiene todos los managers persistentes (`DontDestroyOnLoad`) y siempre debe estar cargada.
3. Para testear cualquier otra escena (mundo, cinemática, etc.), ábrela directamente y dale a Play: `AutoBootstrapOnPlay.cs` detecta que no es `Start` y la carga aditivamente antes de entrar en PlayMode. No hace falta configuración manual.

**Requisito:** `Start.unity` debe estar en la posición 0 de Build Settings.

Detalle completo del arranque (incluido el paso `Start → MainMenu` vía `BootLoader`), Script Execution Order y presets de testing: `TDD.md` § 1.

## 🏗️ Arquitectura clave

- **Multi-escena aditiva** — `Start` persiste siempre; el resto de escenas se cargan/descargan sobre ella.
- **ServiceLocator** (`Core/ServiceLocator.cs`) — punto de acceso a singletons globales, cachea tras la primera búsqueda.
- **ScriptableObjects como datos** — configuración de NPCs, quests, hechizos y presets. Nunca lógica.
- **Eventos C# (`Action<T>`)** — comunicación desacoplada entre sistemas, sin referencias directas entre managers.
- **FSM de NPCs:** `NPCBehaviourManagerV2 → NPCBrain → NPCStateContext → INPCState` (`Assets/Scripts/Behaviour NPC/`).
- **Narrativa:** `NarrativeGraph` (grafo de nodos, sistema activo para todo NPC/quest nueva; núcleo genérico que habla con el juego por puertos de `NarrativeServices`, con los adaptadores en `Scripts/NarrativeIntegration/`) conviviendo con el executor legacy `NPCInteractiveNarrativeExecutor` (congelado, sin nuevos NPCs) — política formal completa en `TDD.md` § 10.

Detalle completo de cada sistema, reglas de rendimiento y bugs conocidos: ver `TDD.md`.

## 📂 Estructura del proyecto

```
Assets/
├── Scenes/
│   ├── Systems/        ← Start, MainMenu, LoadingScreen
│   ├── Worlds/         ← MainWorld, Chuchelandia, Laberinto…
│   ├── Interior/       ← casa de Will, torre de Liam
│   └── Test/           ← LAB, previa de la cinemática final, batalla final
├── Scripts/
│   ├── Core/               ← GameBootService, Partida/, ServiceLocator, PlayerService, SaveSystem
│   ├── Behaviour NPC/      ← FSM de NPCs
│   ├── Narrative/          ← sistema legacy "Interactive"
│   ├── NarrativeIntegration/ ← el juego conectado al grafo: adaptadores y nodos propios
│   ├── Cinematics/, Sequences/ ← cinemáticas con guion horneado
│   ├── Personajes/         ← grupo, caras y emociones
│   ├── Quests/, Dialogue/, Audio/, Inventory/, Puzzle/, UI/, ...
├── NarrativeGraph/      ← núcleo genérico del grafo (Runtime/, Editor/) y assets del grafo
├── _SEQUENCES/          ← guiones de las cinemáticas
├── _BOOTPROFILE/        ← presets de testing (ScriptableObjects)
├── Resources/Localization/  ← JSON de localización (ES/EN)
└── Plugins/             ← Invector, DOTween, Quibli, Dreamteck Splines…
Tools/                   ← scripts de Blender (modelos de personajes), audio e iconos
```

Árbol completo y detalle de cada carpeta: `TDD.md` § 2.

## 🎮 Stack técnico

- Unity 6 (6000.6.2f1) · URP 17.6 · Shader Graph · VFX Graph
- Input System 1.20 (input centralizado por eventos)
- Cinemachine 6.6 · Timeline 6.6 · AI Navigation 2.0.14 · Recorder 5.1
- Quibli (sombreado toon) · Invector 3rd Person Controller LITE (base de movimiento del jugador) · DOTween · Dreamteck Splines
- Fuera de Unity: Blender (personajes generados por script en `Tools/Modelos/`), ElevenLabs (doblaje) y Suno (música)

## 📝 Convenciones de código (resumen)

Reglas completas y no negociables en [`TDD.md` § 12](TDD.md#12-reglas-de-rendimiento). Las más importantes:

```csharp
// ❌ Nunca en Update/LateUpdate/FixedUpdate
FindObjectOfType<T>();                 // usar registros (ActiveCombatRegistry, PlayerParty...)
GetComponent<T>();                     // cachear en Awake
Physics.OverlapSphere(...);            // usar OverlapSphereNonAlloc con buffer

// ✅ Patrón preferido
var dm = DialogueManager.Instance;
QuestManager.OnQuestCompleted += HandleQuestComplete;
```

- VFX de un solo uso → `VfxPoolService.Instance.Play(...)`, nunca `Instantiate` + `Destroy` directo.
- Comentarios, documentación y mensajes de commit **en español**.
- `Debug.Log` de diagnóstico siempre bajo `#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION`.

## 🔧 Herramientas de desarrollo

- **F3** — debug visual de NPCs · **F4** — panel de debug general
- `Tools/Narrative Graph/Abrir editor` (Ctrl+Mayús+N) — editor del grafo narrativo
- `El Sendero/Narrativa/Validar Interactive vs Grafo (proyecto completo)` — valida que quests/eventos no estén referenciados a la vez por el grafo y por el sistema legacy (política completa en `TDD.md` § 10)

## 🚢 Publicación

Checklist paso a paso para subir la demo a Steam (cuenta de Steamworks, store page, build técnico con SteamPipe): `TDD.md` § 18.

---

Consulta [TDD.md](TDD.md) para toda la documentación técnica del proyecto: arquitectura, sistemas, reglas de código, bugs conocidos, diseños en curso, checklist de Steam y auditorías.
