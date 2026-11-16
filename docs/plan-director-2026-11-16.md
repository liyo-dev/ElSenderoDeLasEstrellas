# Plan: Director — cinemáticas con IA (16 nov 2026)

**Estado:** plan aprobado en conversación con Raúl. No hay código de producto todavía: esto fija el
reparto, el contrato y el orden. El trabajo del juego sigue igual; el Director se construye al lado y
se alimenta de lo que ya existe.

**Decisiones ya tomadas**

1. La mesa de trabajo es **web** (no una ventana de Unity).
2. **Dos productos independientes**: el grafo narrativo (`Liyodev.NarrativeGraph`, ya existe) y el
   Director. Cada uno funciona solo; ninguno necesita al otro para arrancar.
3. El **conector** grafo ↔ Director es gratis y se instala aparte.
4. El Director es **un solo producto** con tres partes dentro (núcleo, Studio, puente/MCP), no tres
   productos. Una licencia, una descarga, un manual.
5. El Studio y el MCP hablan con Unity por **un único contrato** local. La IA no ejecuta menús.
6. La fuente de verdad de una cinemática sigue siendo el `.guion.txt`; el tablero y las anotaciones
   son vistas y datos de revisión, no un segundo formato.

---

## 1. Qué es

**El Director** es una herramienta para escribir, hornear, ver, revisar y corregir cinemáticas con una
IA dentro del bucle. No compite con Timeline como editor de curvas: compite con «escribir la escena a
mano y rezar». La unidad de trabajo es el **plano con intención**, no el keyframe.

Bucle:

```
intención (texto o tablero) → guion → horneado (puesta en escena + cámaras) → fotogramas
        ↑                                                                        │
        └──────────────── notas (humanas o de IA) ───────────────────────────────┘
```

Lo que lo hace distinto: el horneado **resuelve** la escena (posiciones, caminos, encuadres, miradas,
alturas) y devuelve **fotogramas reales** con un parte de lo que no rueda. La IA no adivina un
Timeline: lee el resultado y propone cambios sobre el texto.

## 2. Lo que ya existe (base del producto)

| Pieza | Dónde vive hoy | Qué aporta |
|---|---|---|
| Gramática del guion + horneado | `Assets/Editor/Guion` | texto → tiempos, planos, cámaras, informe |
| Solver de puesta en escena y cámaras | `Assets/Editor/Guion/HornoCamaras.cs` | posiciones, caminos, ejes, coste de encuadre |
| Storyboard real | `HornoSalida.Storyboard` | una foto por plano + `storyboard.html` |
| Recorridos | `HornoSalida` | planta con caminos y cámaras en SVG |
| Runtime | `Assets/Scripts/Sequences` | `SequencePlayer`, beats, `SequenceDefinition` |
| Taller de rodaje | `Assets/Editor/Guion/TallerDeRodajeWindow.cs` | encuadre con diff antes de escribir |
| Storyboard anotable | `Tools/Storyboard` | notas/trazos por elemento, resumen, JSON |
| Generador de la página | `Assets/Editor/Guion/StoryboardAnotable.cs` | datos del horneado → página |
| Puente Editor ↔ fuera | `Assets/Editor/Guion/OrdenesDelEditor.cs` (buzón) | ejecuta órdenes por fichero |
| Grafo narrativo | `Packages/com.liyodev.narrativegraph` | historia, misiones, mapa, puertos |

**Lo que falta para que sea producto:** paquetes separados, Studio servido por el Editor, contrato,
MCP, flipbook, modo crear y capa de IA. Y sacar de `Assets/Editor/Guion` todo lo que huele a El
Sendero.

## 3. Los dos productos y el conector

```
Liyodev.NarrativeGraph        (producto 1, ya existe)
        ▲
        │  sin dependencias cruzadas
        ▼
Liyodev.Cinematics.NarrativeGraph   (conector, gratis, opcional)
        ▲
        ▼
Liyodev.Cinematics            (producto 2, el Director)
```

| Instalado | Qué funciona |
|---|---|
| **Solo grafo** | Historia, misiones, señales, diálogos, mapa. Sin Director. |
| **Solo Director** | Guion, horneado, solver, `SequencePlayer`, Studio, storyboard, MCP. Sin una referencia al grafo. Tráiler, intro, escena suelta, juego con historia propia. |
| **Los dos + conector** | Lanzar desde un paso, reparto y estado desde la historia, avisos de continuidad, «probar desde el paso», volver al mapa. |

### Reglas que garantizan la independencia

1. `Liyodev.Cinematics` **no referencia** `Liyodev.NarrativeGraph` (ni `using`, ni tipos, ni assets).
2. `Liyodev.NarrativeGraph` **no sabe** que existe el Director; sigue pidiendo «reproducir cinemática»
   por su puerto.
3. Solo `Liyodev.Cinematics.NarrativeGraph` referencia a los dos. Si falta uno, no se compila/instala
   y no rompe nada.
4. El Studio enseña la pestaña «Historia» **solo si el conector está**.
5. QA automático: compilar el Director en un proyecto **sin** el grafo. Si aparece una referencia
   cruzada, salta ahí.

## 4. Arquitectura del Director

### 4.1 Núcleo (paquete de Unity, `Liyodev.Cinematics`)

- Gramática del guion y parser (genérico; el vocabulario de El Sendero va en datos).
- Horneado: puesta en escena, caminos, cobertura, cámaras, parte de avisos.
- Render de storyboard y recorridos.
- Runtime: `SequencePlayer`, beats, `SequenceDefinition`.
- Servidor local del Studio y del MCP (solo Editor).
- Herramientas de Editor: hornear, abrir Studio, generar la página.

### 4.2 Studio (web, dentro del paquete)

- Se sirve desde el Editor en `http://localhost:PUERTO/studio` (el usuario no instala Node).
- Los ficheros estáticos van en una carpeta terminada en `~` (Unity no los importa).
- Tres modos:
  - **Ver**: flipbook con las fotos a su duración real + audio; «Play» en Unity para lo exacto;
    exportar mp4 de previz.
  - **Revisar**: lo de la página actual (elementos, notas, flechas, cajas, color, resumen, JSON) más
    una capa **IA** con el mismo formato.
  - **Crear**: tablero de cuadros en blanco + **planta**; flechas de entrada/salida por personaje que
    se convierten en coordenadas y de ahí en líneas `pasea` / `anda`. Genera un esqueleto de guion y
    lo hornea.

### 4.3 Puente y MCP (mismo servicio)

- Un solo contrato local para Studio y MCP (sección 6).
- MCP en `http://localhost:PUERTO/mcp` (HTTP), con token. Nada de ejecutables sueltos.
- El buzón actual (`OrdenesDelEditor`) se queda como herramienta interna del juego; el Director
  **no** lo expone: lista blanca de operaciones.
- Si un cliente MCP solo habla stdio, se añade después un lanzador fino que hable con el mismo
  contrato (decisión pendiente).

## 5. Datos

| Fichero | Fuente de verdad de | Notas |
|---|---|---|
| `.guion.txt` | la cinemática | texto, diff, git; único sitio que se edita |
| `board.json` | modo crear | cuadros, entradas/salidas, intención; compila al guion |
| `review.json` | anotaciones | por elemento y plano; ligado a la huella del horneado |
| `shotplan.json` | exportación neutra | para otros motores; el adaptador de Unity lo consume |
| backups | seguridad | `Versiones antiguas/` o carpeta del proyecto, antes de aplicar |

Reglas:

- Nada se aplica sin **diff** y confirmación (humana o regla explícita).
- Las anotaciones de un horneado viejo no se mezclan con uno nuevo: se guardan con la huella.
- El `board.json` **compila** al `.guion.txt`; no hay dos verdades.

## 6. Contrato del puente (primera entrega)

Operaciones mínimas; el Studio y el MCP usan las mismas.

| Operación | Entrada | Salida |
|---|---|---|
| `estado` | — | Unity abierto o no, proyecto, versión, escena, token válido |
| `cinematicas.listar` | — | id, nombre, escena, duración, planos, estado del horneado |
| `cinematica.leer` | id | texto del guion, huella, reparto, planos del horneado |
| `cinematica.proponer` | id, cambios | diff (líneas antes/después) + token de propuesta |
| `cinematica.aplicar` | id, token | texto escrito + backup; rehorneado opcional |
| `cinematica.hornear` | id | informe, avisos, huella nueva, nº de fotos |
| `cinematica.foto` | id, plano | imagen (resource) |
| `cinematica.reproducir` | id, desde, plano | PlayMode en el punto pedido |
| `cinematica.parar` | — | fin de PlayMode |
| `revision.leer` / `revision.guardar` | id | anotaciones (formato actual) |
| `tablero.leer` / `tablero.guardar` | id | `board.json` |
| `tablero.compilar` | id | guion generado + diff |
| `ia.notas` | id, plano(s) | notas propuestas con el formato de anotación |

Seguridad: `localhost` + token por sesión, rutas dentro del proyecto, sin ejecutar código ni menús,
límite de tamaño en imágenes, confirmación en operaciones destructivas.

## 7. Qué lo hace distinto (por orden de impacto)

1. **La IA mira los fotogramas**: notas por elemento con el mismo formato que las humanas; se acepta
   o se descarta. Es el demo que vende.
2. **Cobertura automática** a partir de la intención: master + sobre el hombro + primer plano, 180°
   respetado, sin cruzar actores ni paredes.
3. **QA de continuidad**: cruces, paredes, miradas, texto que no cabe en la voz, ojos bajo el foco.
4. **Texto + diff + git**: reproducible, mergeable, auditable. Timeline no lo es.
5. **DSL + skill para modelos**: el modelo compone con vocabulario de planos, no con coordenadas.
6. **Tablero de planta con entradas/salidas** que se convierte en datos.
7. **Exportador neutro** (`shotplan.json`): Unity hoy, Unreal/Godot mañana.
8. **Tres formas de verlo**: flipbook en el Studio, PlayMode exacto, mp4 para enseñar.

## 8. Lo que no se hace

- Un viewport 3D propio.
- Un editor de curvas tipo Timeline.
- Un segundo formato paralelo al guion.
- Que la IA escriba keyframes o beats libres: propone, se acepta el diff.
- Exponer «ejecuta este menú» por MCP.
- Meter nada de El Sendero en el paquete: ids, nombres, rutas y condiciones van a datos o al conector.

## 9. Fases

| Fase | Qué | Se da por hecha cuando |
|---|---|---|
| **F0** | Separar ensamblados y escribir el contrato (sección 6) | `Liyodev.Cinematics` compila en un proyecto **sin** el grafo; el contrato está en un `.md` y en código |
| **F1** | Flipbook del prólogo en el Studio | 58 fotos a su duración + música; play, pausa y arrastre; sin Unity |
| **F2** | Studio servido por el Editor + revisar | La página actual funciona en `localhost`, guarda `review.json` y sigue el horneado por huella |
| **F3** | Crear con tablero y planta | 3 cuadros en blanco, flechas de entrada/salida sobre la planta, esqueleto de guion y horneado desde el Studio |
| **F4** | IA que mira los fotogramas | Una pasada de IA sobre 10 planos del prólogo deja notas útiles y aceptables; se ven junto a las humanas |
| **F5** | Cobertura y QA de continuidad | Una escena de 10 planos propone cobertura sin cruces ni 180° roto; el parte dice lo que no rueda |
| **F6** | Producto | Paquete, manual en inglés, demo (el prólogo), QA de independencia, tienda |

Orden de trabajo acordado: **F0 primero** (aburrido pero barato), luego F1–F2, y decidir con el
flipbook delante si F3 o F4 va antes.

## 10. Empaquetado y venta

- **Dos paquetes** en el Asset Store: `Liyodev.NarrativeGraph` y `Liyodev.Cinematics` (Director).
- **Conector gratis** con cualquiera de los dos.
- **Bundle** con descuento.
- El Director incluye el Studio (servido por el Editor) y el MCP. Sin instaladores aparte.
- Manual de usuario en inglés; código y comentarios en español, como el resto de kits.
- Demo: el prólogo y la cinemática final, con su proyecto de ejemplo.
- Nombre comercial del Director: **pendiente**.

## 11. Riesgos

- **Revisión del Asset Store** de un paquete que levanta un servidor local y abre el navegador: hay
  que documentarlo y dar una alternativa sin servidor (abrir la página con `file://`).
- **Clientes MCP que solo hablan stdio**: lanzador fino o documentar el cliente que sí habla HTTP.
- **Acoplamiento que vuelve**: el QA de independencia es obligatorio en cada release.
- **Convertir esto en un editor general**: el alcance son cinemáticas con guion, no un motor.
- **Soporte**: cada motor/versión nueva multiplica; se empieza solo por Unity 6 + URP.

## 12. Decisiones pendientes

- Nombre del producto.
- Precio y si el MCP va incluido o es un extra para estudios.
- Modo sin Unity (solo lectura de guiones y fotos ya horneadas) para revisar en otra máquina.
- Segundo motor y cuándo.
- Si el `board.json` es la entrada del modo crear o si se escribe directamente el guion con una
  sintaxis más corta.
