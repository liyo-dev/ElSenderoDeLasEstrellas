# Clima: reloj, constancia y efectos de juego

**Fecha:** 17 de noviembre de 2026  
**Estado:** el ciclo de día de 30 min está implementado en `DayNightCycle`. El resto (reloj, menú, previsión, persistencia y afinidades clima-elemento) sigue documentado y sin implementar. No se han tocado escenas, prefabs, assets ni el canon del GDD.  
**Decisión de Raúl (17 nov 2026):** el ciclo de día completo pasa a **30 minutos** (implementado el 17 nov 2026).

---

## 1. Resumen

El sistema de clima actual (`DayNightCycle`) es completo para atmósfera y cinemáticas, pero no se puede leer ni planificar desde el juego:

- No hay reloj ni icono del tiempo en el HUD ni en el menú.
- El ciclo de día ya dura **30 min de franjas** (transiciones aparte) gracias a `DayNightCycle.duracionTotalDelDia`. Antes, MainWorld sumaba 375 s y los valores por defecto del código, 525 s.
- El clima se sortea cada 45–110 s y los eventos duran 40–140 s; el jugador no ve cuánto queda ni qué va a pasar.
- No hay previsión, no se guarda en la partida y no hay una petición narrativa de clima (solo de hora).
- Solo el viento y el rayo peligroso cambian el juego; lluvia y nieve son cosméticas.
- Los NPCs no reaccionan a la lluvia: el relay `NPCWeatherAwareness` existe, pero la suscripción de `NPCBehaviourManagerV2` está comentada.

La propuesta es separar tres cosas que hoy van mezcladas:

1. **Leer el tiempo:** reloj, icono, tiempo restante y previsión.
2. **Que el tiempo importe:** efectos de clima en hechizos y enemigos, con control del jugador.
3. **Que se entienda:** menú que explique los efectos, feedback en el combate y bestiario.

---

## 2. Decisión tomada: ciclo de día de 30 minutos

**Raúl, 17 nov 2026:** «lo que sí: amplía a 30 min el ciclo de día».

Situación actual (valores por defecto en `DayNightCycle`; MainWorld tenía 120 + 120 + 45 + 90 = 375 s):

| Franja | Duración actual | Total actual |
|---|---:|---:|
| Amanecer (`Morning`) | 90 s | |
| Día (`AfterNoon`) | 220 s | |
| Atardecer (`Sunset`) | 65 s | |
| Noche (`Night`) | 150 s | |
| **Total** | | **525 s = 8 min 45 s** |

Las transiciones entre franjas (`transitionDuration`; 16 s por defecto, 10 s en MainWorld) van aparte, así que con los valores del código un día completo real ronda los **9 min 49 s**.

**Reparto de partida propuesto para 30 min (1800 s):**

| Franja | Propuesta | Equivale |
|---|---:|---:|
| Amanecer | 300 s | 5 min |
| Día | 720 s | 12 min |
| Atardecer | 180 s | 3 min |
| Noche | 600 s | 10 min |
| **Total** | **1800 s** | **30 min** |

Es un punto de partida, no una decisión cerrada: se puede conservar la proporción actual (5 min / 12 min 30 s / 3 min 45 s / 8 min 45 s) o repartir a ojo en Play. Las transiciones siguen aparte (40 s en MainWorld).

**Implementado (17 nov 2026):** `DayNightCycle.duracionTotalDelDia` vale 1800 s por defecto. Al arrancar, el ciclo reparte las franjas en proporción a la duración que ya tuviera cada escena; no toca las de hora fija. MainWorld (120/120/45/90) pasa a 576/576/216/432 s (9 min 36 s / 9 min 36 s / 3 min 36 s / 7 min 12 s). Las transiciones de MainWorld (10 s) van aparte: el día real ronda 30 min 40 s. El reparto 5/12/3/10 no se ha impuesto; si se quiere, se cambia en el Inspector o se ajusta el campo.

**Consecuencia obligatoria:** al alargar el día, el sorteo de clima hay que reajustarlo o el tiempo se sentirá vacío. Objetivo inicial: **3–5 eventos de clima por día**, no 15. Punto de partida para probar:

- Comprobación del sorteo cada **3–6 min** (hoy 45–110 s).
- Lluvia de **2–5 min**; tormenta de **1,5–3 min**; nieve de **3–6 min**; viento de **2–4 min**; niebla de **1,5–3 min**.
- Fundidos y rampas algo más largos (+30–50 %), para que un evento no se coma su propia transición.

Estos números **no están implementados** y no son canon; son la base para la primera prueba en Play. Las escenas de hora fija (Parque, Chuchelandia, etc.) no cambian.

---

## 3. Estado actual del sistema (lo que hay)

### 3.1. Núcleo

| Pieza | Archivo | Papel |
|---|---|---|
| Ciclo y clima | `Assets/Scripts/World/DayNightCycle.cs` | Hora, cielo, niebla global, lluvia, tormenta, nieve, viento, niebla ocasional, sorteo, audio, interiores y rayos |
| VFX de relleno | `Assets/Scripts/World/VfxDeClima.cs` | Nieve y viento generados por código si no hay prefab |
| Nubes sueltas | `Assets/Scripts/World/AmbientCloudDirector.cs` + `AmbientCloudDrifter.cs` | Pool de nubes de buen tiempo; informan cobertura al sorteo |
| Cielo | `Assets/Scripts/Shaders/CieloDelSendero.shader` (`El Sendero/Cielo`) | Cénit, horizonte, suelo, bruma, resplandor, nubes pintadas, sol y luna |
| Astros | `Assets/Scripts/World/SolYLunaEnElCielo.cs` | Publica direcciones, colores y tamaños de sol/luna al shader |
| Noche | `NightSkyStarSpawner`, `NightSkyConstellationSpawner`, `EstrellasFugaces`, `NieblaNocturna`, `VentanasIluminadas`, `LuzNocturna` | Estrellas, constelaciones, fugaces, niebla, ventanas y faroles |
| Reacción del grupo | `Assets/Scripts/Player/ClimaSobreElJugador.cs` | Viento (velocidad, arrastre, taparse) y frío (abrazarse, tiritar, cara) |
| NPCs | `Assets/Scripts/Behaviour NPC/Common/NPCWeatherAwareness.cs` | Relay estático de lluvia para NPCs |
| Cinemáticas | `Assets/Scripts/Sequences/Beats/CinematicWorldBeats.cs`, `RayoBeat.cs`, `CinematicTimeOfDay.cs` | `WeatherBeat`, `RayoBeat`, ancla de clima y restauración |
| Narrativa | `Assets/ElSendero/Connections/NarrativeGraph/Peticiones/PedirHoraDelDia.cs` | Nodos de hora: poner, preguntar, esperar |
| Zonas | `Assets/Scripts/Environment/AmbientZone.cs` + `AmbientPreset.forcesMist` | Fuerza niebla y sube nubes sueltas dentro de una zona |
| Herramientas | `Assets/Editor/ClimaDelCicloWiring.cs`, `Assets/Scripts/CombatLab/ClimaDelLab.cs` | Montar cielos/lluvia, fijar hora sin clima, pestaña Clima del LAB |

### 3.2. Lo que ya ofrece

- **Hora:** cuatro franjas con cielo, luz, ambiente, niebla y duración propias; transición suave o salto; escenas de hora fija sin clima.
- **Cielo:** shader procedural con sol, luna, nubes, bruma, resplandor y cobertura; nubes sueltas 3D; estrellas y constelaciones que se apagan con la nubosidad.
- **Lluvia:** nublado progresivo, oscurecimiento con suelo mínimo, niebla ×2,5, partículas, loop de audio, fundido de salida.
- **Tormenta:** lluvia + rayos periódicos, destello propio, trueno retrasado, trazo por pool o generado, rayos de fondo encuadrados a cámara.
- **Rayo peligroso:** aviso en el suelo, radio, daño al jugador y a `Damageable`, sacudida y vibración; sin aviso no cae.
- **Nieve:** copos de prefab o código, cielo gris perla, luz y niebla atenuadas, loop suave; no se solapa con lluvia.
- **Viento:** dirección, fuerza con rampa, rachas y hojas, nubes más rápidas, jugador más lento, arrastre quieto, grupo tapándose la cara.
- **Niebla ocasional:** espesa la niebla global y opcionalmente partículas; se puede forzar por zona.
- **Sorteo:** temporizador propio, independiente de la hora; orden tormenta → lluvia → nieve → viento → niebla; sin solapes.
- **Interiores:** partículas apagadas, loops amortiguados con paso bajo, rayos y truenos omitidos.
- **Cinemáticas:** `clima <fenómeno>`, `clima <fenómeno> fin`, `immediate=true`, `rayo`, `hora`; el clima se cuelga de la cámara y se restaura al acabar.
- **API:** `IsRaining`, `IsSnowing`, `IsWindy`, `IsMisty`, `IsThunderstorm`, `Nublado`, `VisibilidadDeLasEstrellas`, `PesoDeNoche`, `NocheActual`, `HoraDelDia`, `DireccionDelViento`, `FuerzaDelViento`, `LluviaPedida`, `NievePedida`, eventos de inicio/fin, `StartRain`, `StopRain`, `StartSnow`, `StartWind`, `StartMist`, `StartThunderstorm`, `LanzarRayo`, `PrestarNiebla`.

### 3.3. Huecos y deudas detectadas

- **Sin reloj ni icono:** no hay ningún indicador de hora o clima en el HUD; `TimeOfDayIndicator` se retiró.
- **Sin tiempo restante:** `DayNightCycle` expone el estado como booleanos, pero no `TiempoRestanteDeClima` ni `ProximoCambioDeClima`. Un aro de cuenta atrás no se puede dibujar hoy.
- **Sin previsión:** el sorteo es aleatorio y no anuncia el siguiente cambio; el único aviso es el nublado progresivo de 40 s.
- **Sin persistencia:** `Partida` no guarda el clima. Al cargar, el clima vuelve a lo que diga la escena.
- **Sin petición narrativa de clima:** solo hay hora (`PedirHoraDelDia`, `PedirSegunHoraDelDia`, `PedirEsperarHoraDelDia`). Para clima desde el grafo hoy hace falta una petición propia, un `NarrativeGameStepHook` en escena o una cinemática.
- **NPCs:** `NPCWeatherAwareness` y `SeekShelterState`/`ReturnFromShelterState`/`NPCShelterPoint` existen, pero la suscripción está comentada en `NPCBehaviourManagerV2` desde el 31 ago 2026. Hoy ningún NPC reacciona a la lluvia.
- **Cinemáticas:** `WeatherBeat` pide viento con `StartWind()` y niebla con `StartMist()` sin duración; usan los rangos aleatorios (30–90 s y 20–45 s). En una secuencia larga se caen solos. `immediate` tampoco se aplica a viento/niebla.
- **Instancia única:** `DayNightCycle.Instance` es «el último que despierta»; en cargas aditivas puede no coincidir con el `FindAnyObjectByType` que usa `NPCWeatherAwareness`.
- **Daño sin elemento:** `Damageable.TakeDamage(float)` solo recibe cantidad; el elemento se pierde antes de llegar. El punto natural para un modificador por clima es `FormulasDeCombate.DanoDe` o el impacto del hechizo, no `Damageable`.
- **Canon:** el GDD vigente dice que **no hay debilidades ni resistencias elementales en combate**; los elementos son identidad y puzle. Cualquier matriz clima-elemento es un cambio de diseño que hay que aprobar y reflejar en `GDD.md`.
- **No hay Agua:** los cinco elementos son Fuego, Viento/Tormenta (`MagicElement.Storm`), Luz, Mente y Oscuro. «Lluvia potencia agua» no se puede hacer sin inventar un sexto elemento.
- **No hay «soleado»:** el clima es un evento o su ausencia. Para un mago del clima hace falta un estado **Despejado** explícito y una forma de forzarlo.

---

## 4. Propuesta de interfaz: reloj y constancia

### 4.1. HUD

- **Hora** (`HoraDelDia` ya es pública) en formato `hh:mm` y un **icono del clima actual**.
- **Aro fino de tiempo restante** alrededor del icono, cuando haya un evento activo.
- **Aviso de transición** cuando empieza a nublarse o a despejar, no cuando ya caen gotas.
- Debe ocultarse con el HUD (`PlayerHUDV2.HideHUD`) y en cinemáticas, como el resto.
- Estilo: `PiezasDeUI.Papel`, coherente con el HUD actual.

### 4.2. Menú de pausa: pestaña «Mundo»

`MenuDeStart` tiene hoy cuatro pestañas: Inventario, Magia, Equipo y Sintonía. Se propone una quinta, **Mundo**, con:

- Hora y franja actual; clima actual; tiempo restante.
- Previsión del siguiente cambio (si se implementa).
- Tabla «Qué hace el tiempo»: efectos actuales (viento, frío, rayo) y, cuando existan, afinidades elementales.
- Estado de los modificadores activos, con su origen (clima, debilidad, hechizo).
- Acceso al bestiario si se añaden debilidades.

El HUD informa; el menú explica. No mezclar ambos.

### 4.3. Iconos

Set mínimo: despejado (día), despejado (noche), nublado, lluvia, tormenta, nieve, viento y niebla. Con variantes nocturnas para los estados de cielo. Los iconos deben leerse a tamaño pequeño y sobre papel.

### 4.4. Feedback de combate

Si el clima modifica el daño, el jugador tiene que ver **por qué**:

- Número o color de impacto con el multiplicador.
- Línea de desglose en el menú: `12 × 2 (debilidad) × 2 (sol) = 48`.
- Aviso de tutorial la primera vez.
- Entrada de bestiario con debilidades y rasgos.

Sin esto, un x4 es invisible y parece un bug.

---

## 5. Propuesta de juego: clima, elementos y enemigos

> **Estado:** propuesta para otro scope. No aprobada, no implementada y **no cambia el canon actual**. El GDD vigente sigue diciendo que no hay debilidades ni resistencias elementales en combate.

### 5.1. Principios

1. **El clima debe abrir opciones, no cerrar personajes.** Si llueve y Will (Fuego) queda inútil, el jugador cambia de personaje y el clima no crea estrategia, crea un castigo. Mejor bonus y estados que penalizaciones duras.
2. **El jugador debe poder controlarlo.** Un x4 con clima aleatorio es una lotería. Hace falta un hechizo de cambio de clima con coste, duración y cooldown, y que el sorteo no lo pise.
3. **El jugador debe verlo.** Icono, tiempo restante, previsión y feedback. Sin lectura no hay estrategia.
4. **Los jefes siguen siendo puzles.** El clima puede cambiar la mecánica del jefe (conducir un rayo, ocultar una apertura, empujar rocas), no saltarse el puzle con un multiplicador plano.
5. **Una sola fuente de verdad para el daño.** Nada de `if` por hechizo o enemigo repartidos; una tabla de afinidades en datos y un modificador central.

### 5.2. Estados de clima propuestos

Además de los eventos actuales, hace falta un estado **Despejado** explícito (día y noche) y una intensidad (0–1) por evento. El modelo de lectura sería:

- `tipo`: Despejado, Lluvia, Tormenta, Nieve, Viento, Niebla.
- `intensidad`: 0–1.
- `tiempoRestante`: segundos.
- `origen`: sorteo, forzado por jugador, cinemática o zona.
- `proximoCambio`: tipo y segundos (si hay previsión).

### 5.3. Matriz clima-elemento (propuesta, usando los cinco elementos actuales)

| Clima | Sube | Baja | Nota |
|---|---|---|---|
| **Despejado de día** | Fuego, Luz | Oscuro | El sol como estado del mundo, no como elemento |
| **Lluvia** | Viento/Tormenta | Fuego | Fuego hace vapor; la lluvia alimenta la tormenta |
| **Tormenta** | Viento/Tormenta, Luz | Fuego | El rayo es Viento/Luz, no un sexto elemento |
| **Niebla** | Mente | Luz | La niebla oculta y confunde; la luz se dispersa |
| **Nieve** | Mente | Fuego, Viento | Silencio y quietud; el viento pierde fuerza |
| **Noche despejada** | Oscuro, Mente | Luz | Sin sol no hay bonus de Fuego |

Las bajadas deben ser suaves o inexistentes. La tabla es una propuesta de trabajo, no canon.

### 5.4. Multiplicadores

- **Recomendado:** x1,5–x2 por factor, con un tope total (por ejemplo x3), y solo si el jugador ha montado el combo (clima + debilidad + estado).
- **x4:** solo como combo deliberado y caro, nunca por una tirada aleatoria. Si se usa, que sea visible y de ventana corta.
- **Sin acumulación infinita:** una sola tabla, un solo orden de aplicación y un desglose legible.
- **Jefes:** sin multiplicador elemental plano; usar mecánicas de arena y aperturas.

### 5.5. Hechizo de cambio de clima

Para que la estrategia exista hace falta:

- Un hechizo (o varios) que ponga un clima concreto, con coste de maná, tiempo de lanzamiento, duración y cooldown.
- Un **override de clima del jugador** que impida al sorteo cambiarlo mientras dura, separado de `SorteoDeClimaEnPausa` (cinemáticas) y de `SorteoAutomatico`.
- Previsualización del efecto antes de lanzar: qué clima, qué elementos suben/bajan, cuánto dura.
- Un «volver a lo normal» claro al terminar, sin dejar el cielo bloqueado.

### 5.6. Enemigos y debilidades

- Si se aprueban debilidades, que sean **datos** (ScriptableObject o componente) y se vean en el bestiario.
- Mejor rasgos concretos que una matriz universal: un enemigo de fuego, uno que se moja, uno que se electriza. Así se evita el «todo el mundo tiene una debilidad elemental».
- Los jefes-puzle conservan sus reglas mecánicas; el clima puede ser una de ellas, no un atajo.

---

## 6. Arquitectura técnica propuesta

> Nada de esto está implementado. Es el orden que evita rehacer trabajo.

### 6.1. Estado y API

- `EstadoDelClima` como struct/clase de solo lectura: tipo, intensidad, restante, origen, próximo cambio.
- `DayNightCycle` publica `EstadoDelClima Actual` y eventos `ClimaCambiado`/`PrevisionCambiada`, además de los booleanos actuales (compatibilidad).
- `TiempoRestanteDeClima` y `ProximoCambioDeClima` públicos para la UI.
- `ForzarClima(tipo, duracion, origen)` y `Despejar(duracion)` con prioridad; el sorteo no pisa un forzado.
- Un `WeatherOverride` con clave, para que varios sistemas (jugador, cinemática, zona) no se pisen.

### 6.2. Daño

- No cambiar `Damageable.TakeDamage(float)` para meter elemento: se rompe todo el código existente.
- Añadir el elemento al impacto del hechizo (ya está en `MagicProjectile.Elemento` y en `ImpactoDeMagia`).
- Centralizar en `FormulasDeCombate.DanoDe(instigador, dano, elemento)` o en un `ModificadorDeDanoPorClima` que se llame antes de `TakeDamage`.
- Tabla de afinidades en ScriptableObject: clima × elemento × etiqueta de enemigo → multiplicador.
- Un solo punto de aplicación y un desglose para la UI; nada de `if` repartidos.

### 6.3. Persistencia y narrativa

- Guardar en `Partida` el tipo, la intensidad, el restante y el origen; al cargar, restaurar sin relanzar el sorteo.
- Petición narrativa `PedirClima` y `PedirEsperarClima` (gemelas de las de hora), con proyección de estado.
- `WeatherBeat` con duración explícita para viento y niebla, y `immediate` respetado.

### 6.4. NPCs

- Reactivar el refugio de lluvia: descomentar las tres líneas de `NPCBehaviourManagerV2` y colocar `NPCShelterPoint` bajo tejados/copas.
- Barks y reacciones con `NPCWeatherAwareness`, sin tocar el sorteo.
- Reutilizar los estados `ClimaTaparse`/`ClimaFrio` para NPCs, con las mismas garantías de capa que `ClimaSobreElJugador`.

### 6.5. Kits

- Si el clima se va a reutilizar o vender, el sistema genérico debe vivir en `Assets/Kits/Weather/` con su asmdef, y lo del juego en `ElSendero/Connections/`. Hoy todo está en `DayNightCycle` y en `Assets/Scripts/World/`.
- No mover nada todavía: primero estabilizar la API y la UI; después, separar sin romper assets serializados (`[MovedFrom]` cuando toque).

---

## 7. Etapas

### Etapa 0 — Leer el tiempo (sin tocar combate)

- Ciclo de día de 30 min (**hecho**) y retoque del sorteo (**pendiente**).
- `TiempoRestanteDeClima` y `ProximoCambioDeClima`.
- Reloj + icono en el HUD.
- Pestaña «Mundo» en `MenuDeStart` con estado y tabla de efectos actuales.
- Aviso de transición.

**Criterio de aceptación:** en MainWorld se ve la hora y el clima sin abrir nada; al abrir el menú se entiende qué hace el tiempo; un día completo dura 30 min y no hay una tormenta cada dos minutos.

### Etapa 1 — Controlar el clima

- Override de clima y `Despejar`.
- Hechizo de cambio de clima (prototipo con un efecto: despejar).
- Previsión y guardado en `Partida`.
- Petición narrativa de clima.

**Criterio de aceptación:** el jugador puede cambiar el clima, ve cuánto dura y el sorteo no lo pisa; al guardar y cargar, el clima se mantiene.

### Etapa 2 — Un solo par de prueba

- Fuego/Sol y Viento/Lluvia con x1,5–x2.
- Feedback de daño y entrada de bestiario.
- Medir si se entiende y si no rompe a los personajes.

**Criterio de aceptación:** el jugador explica en voz alta por qué pega más; ningún personaje queda inútil con lluvia; el x4 no existe todavía.

### Etapa 3 — Matriz completa

- Luz/Mente/Oscuro, enemigos concretos y jefes.
- Balance, tutorial y accesibilidad (reducir destellos de rayo).
- Actualizar `GDD.md` y `AGENTS.md` si el canon cambia.

**Criterio de aceptación:** la matriz se lee en el menú, el bestiario la refleja y los jefes siguen siendo puzles.

---

## 8. Decisiones pendientes

1. **Canon:** ¿se cambia el «sin debilidades ni resistencias elementales» del GDD, o el clima solo da bonus y estados?
2. **Agua:** ¿se usa Viento/Tormenta para lluvia y tormenta (recomendado), o se añade un sexto elemento?
3. **Multiplicador:** ¿x1,5–x2 con tope (recomendado) o x4 solo como combo específico?
4. **Jefes:** ¿el clima modifica su mecánica, o también su daño recibido?
5. **Reparto de las 30 min:** ¿5/12/3/10, o se conserva la proporción actual?
6. **NPCs:** ¿se reactiva el refugio de lluvia en este scope o se deja para otro?

---

## 9. Lo que se ha hecho y lo que no

**Hecho (17 nov 2026):**

- `DayNightCycle.duracionTotalDelDia` (1800 s por defecto) reparte las franjas en proporción a su duración actual al arrancar. No toca las escenas de hora fija. MainWorld pasa de 375 s a 1800 s.
- Compila con Roslyn (0 errores). No se han tocado escenas, prefabs ni assets: el ajuste se hace en runtime.

**No hecho:**

- Reloj, icono, pestaña «Mundo», previsión y estado público de tiempo restante.
- Persistencia en `Partida`, petición narrativa de clima y reacciones de NPCs.
- Reajuste del sorteo de clima para el día de 30 min (sigue con los rangos actuales).
- Afinidades clima-elemento, multiplicadores, feedback y bestiario.
- Canon del GDD: la regla «sin debilidades ni resistencias elementales» sigue vigente.

Este documento es la referencia para el resto de la propuesta. La fila de seguimiento está en `TRACKER.md` como **INC-1276**.
