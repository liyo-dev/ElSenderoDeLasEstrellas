# Correctivos auditoría § 19.5 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cerrar los INC pendientes de la auditoría § 19.5 (Sep 26, 2026): INC-471 (Alta), INC-472 (Media) con código, y marcar como resueltos en TRACKER los que ya están corregidos en código (INC-474, INC-476, INC-477).

**Architecture:**
- INC-471: `CierreDeBatalla` (clase estática) añade tracking de pasos en vuelo y un método `TerminarForzado()` para limpiar si la corrutina del `BossArenaController` muere antes de que el enumerador llegue al bucle de `Terminar`. `BossArenaController.OnDisable` llama a ese método.
- INC-472: `GuiarJugadorNode` almacena la referencia al NPC escoltado, al animador tocado y al valor original de `updatePosition`. En `Exit()` cancela la `CinematicState` del NPC y restaura esos valores antes de devolver la velocidad y el tope.
- INC-474/476/477: Solo actualizaciones de estado en `TRACKER.md` (código ya está correcto).

**Tech Stack:** C# / Unity 6 URP, no paquetes externos.

---

## Task 1: INC-471 — `CierreDeBatalla.TerminarForzado` + `BossArenaController.OnDisable`

**Files:**
- Modify: `Assets/Scripts/Battle/CierreDeBatalla.cs`
- Modify: `Assets/Scripts/Rooms/BossArenaController.cs`
- Modify: `TRACKER.md` (cambiar estado de INC-471)

### Contexto
`CierreDeBatalla.Ejecutar` es un `IEnumerator` estático lanzado como corrutina por `BossArenaController.Co_CerrarYAvisarVictoria`. Si el `BossArenaController` se desactiva/destruye mientras la corrutina espera (p. ej. descarga de escena con el jugador persistiendo en `DontDestroyOnLoad`), Unity para la corrutina antes de que llegue al bucle de `Terminar()`. `PlayerBattleModeController.TerminarVictoria()` **no** se llama porque ese componente no se ha desactivado, y `ActionMode.Cinematic` queda en la pila.

La solución: `CierreDeBatalla` trackea los pasos que ya se han ejecutado en la pasada actual y su resultado; `TerminarForzado()` los termina en orden inverso; `BossArenaController.OnDisable` lo llama.

- [ ] **Paso 1: Añadir campos de tracking a `CierreDeBatalla`**

En `Assets/Scripts/Battle/CierreDeBatalla.cs`, dentro de la clase `CierreDeBatalla`:

```csharp
// Pasos que ya han llamado a Ejecutar en la pasada actual, para poder terminarlos si
// la corrutina muere antes de llegar al bucle de Terminar.
private static List<IPasoDeCierre> _activosActual;
private static ResultadoDeBatalla _resultadoActual;
```

Y actualizar `ResetStatics`:

```csharp
#if UNITY_EDITOR
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void ResetStatics()
{
    _pasos.Clear();
    _activosActual = null;
    _resultadoActual = null;
}
#endif
```

- [ ] **Paso 2: Actualizar `Ejecutar` para poblar el tracking**

Reemplazar el método `Ejecutar` completo en `CierreDeBatalla.cs`:

```csharp
public static IEnumerator Ejecutar(ResultadoDeBatalla resultado)
{
    var pasos = new List<IPasoDeCierre>(_pasos);
    pasos.Sort((a, b) => a.Orden.CompareTo(b.Orden));

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
    Debug.Log($"[CierreDeBatalla] '{resultado.BattleId}': {pasos.Count} paso(s).");
#endif
    _activosActual = new List<IPasoDeCierre>(pasos.Count);
    _resultadoActual = resultado;

    foreach (var paso in pasos)
    {
        IEnumerator rutina = null;
        try { rutina = paso.Ejecutar(resultado); }
        catch (Exception e) { Debug.LogException(e); }
        _activosActual.Add(paso);
        if (rutina == null) continue;

        while (true)
        {
            object actual;
            try
            {
                if (!rutina.MoveNext()) break;
                actual = rutina.Current;
            }
            catch (Exception e) { Debug.LogException(e); break; }
            yield return actual;
        }
    }

    TerminarForzado();
}
```

> **Nota:** `TerminarForzado()` al final del flujo normal hace el bucle de Terminar y limpia los campos. Así la ruta normal y la de interrupción comparten el mismo código.

- [ ] **Paso 3: Añadir `TerminarForzado()`**

Añadir antes de los campos privados de `CierreDeBatalla`:

```csharp
/// Llama Terminar en orden inverso sobre los pasos que ya ejecutaron, y limpia el
/// tracking. Es idempotente: si no hay pasada en curso, no hace nada.
/// Lo llama el bucle normal al acabar y BossArenaController.OnDisable si la corrutina
/// muere antes de terminar.
public static void TerminarForzado()
{
    if (_activosActual == null || _resultadoActual == null) return;
    var hechos = _activosActual;
    var resultado = _resultadoActual;
    _activosActual = null;
    _resultadoActual = null;

    for (int i = hechos.Count - 1; i >= 0; i--)
    {
        try { hechos[i].Terminar(resultado); }
        catch (Exception e) { Debug.LogException(e); }
    }
}
```

- [ ] **Paso 4: Llamar a `TerminarForzado` en `BossArenaController.OnDisable`**

En `Assets/Scripts/Rooms/BossArenaController.cs`, en el método `OnDisable`, añadir la llamada antes de desregistrar la arena (primera línea):

```csharp
void OnDisable()
{
    CierreDeBatalla.TerminarForzado();

    BossProgressTracker.OnProgressRestored -= HandleBossProgressRestored;
    // ... resto sin cambios
```

- [ ] **Paso 5: Actualizar TRACKER.md — INC-471**

Cambiar la fila de INC-471. Buscar `| INC-471 |` y cambiar la columna de estado de `Pendiente` a:

```
Resuelto
```

Y añadir en la columna de validación:

```
CierreDeBatalla.TerminarForzado() + BossArenaController.OnDisable. Pendiente de probar: derrotar un jefe y descargar la escena de la arena mientras la celebración está en curso; el control debe volver al jugador.
```

---

## Task 2: INC-472 — `GuiarJugadorNode.Exit` cancela la escolta y restaura estado

**Files:**
- Modify: `Assets/NarrativeGraph/Runtime/Graph/NodeTypes/GuiarJugadorNode.cs`
- Modify: `TRACKER.md` (cambiar estado de INC-472)

### Contexto
`GuiarJugadorNode.Enter` hace tres cambios de estado en el NPC que `Exit` no deshace:
1. Pone `anim.AllowManualRotation = false` — solo se restaura al final normal de la corrutina (línea 198).
2. Pone `agent.updatePosition = true` sin guardar el valor anterior.
3. Llama a `npc.StartCinematicSequence(paseo)` — el `LeadPlayerToAnchorSequence` vive en la FSM del NPC y sigue corriendo aunque el grafo se detenga.

Al parar o reiniciar el grafo, `NarrativeRunner.StopExecution` llama a `Exit`, pero la escolta no se cancela y el NPC sigue andando sin narrativa.

- [ ] **Paso 1: Añadir campos para el estado que hay que restaurar**

En `GuiarJugadorNode.cs`, después de los campos `[NonSerialized]` existentes (línea 69):

```csharp
[NonSerialized] private NPCBehaviourManagerV2 _npcEscortado;
[NonSerialized] private NPCSimpleAnimator _animTocado;
[NonSerialized] private bool _updatePositionOriginal;
```

- [ ] **Paso 2: Almacenar el estado al inicio de `Co_Guiar`**

En `Co_Guiar`, reemplazar el bloque que toca el animador y el agente (líneas 126-155 aprox.) por:

```csharp
_npcEscortado = npc;

var anim = npc.SimpleAnimator;
if (anim != null)
{
    _animTocado = anim;
    anim.AllowManualRotation = false;
    anim.EnableAutoRotation();
}

if (UnityEngine.AI.NavMesh.SamplePosition(destino, out var navHit, 10f, UnityEngine.AI.NavMesh.AllAreas))
    destino = navHit.position;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
else
    Debug.LogWarning($"[GuiarJugadorNode:{guid}] La marca '{anchorId}' no tiene NavMesh a menos de 10 m: " +
        $"'{npcId}' no podrá llegar. Hay que ampliar el NavMesh hasta ahí o mover la marca.");
#endif

if (agent != null && agent.enabled)
{
    _updatePositionOriginal = agent.updatePosition;
    if (agent.isOnNavMesh) agent.nextPosition = npc.transform.position;
    agent.updatePosition = true;
}

if (agent != null)
{
    _agenteTocado = agent;
    _velocidadOriginal = agent.speed;
    agent.speed = Mathf.Max(agent.speed, velocidad);
}
```

> **Nota:** `_updatePositionOriginal` se asigna justo antes de sobreescribir `updatePosition`.

- [ ] **Paso 3: Limpiar los nuevos campos al final normal de la corrutina**

Al final de `Co_Guiar`, justo antes de `_rutina = null; onReadyToAdvance?.Invoke();`:

```csharp
_npcEscortado = null;
_animTocado = null;
```

> El animador ya restauró `AllowManualRotation = true` en la línea ~198. Los campos se limpian para que `Exit` no los doble si se llama después.

- [ ] **Paso 4: Actualizar `Exit` para cancelar la escolta y restaurar el estado**

Reemplazar el método `Exit` completo:

```csharp
public override void Exit(NarrativeContext ctx)
{
    if (_rutina != null && _runner != null) _runner.StopCoroutine(_rutina);
    if (_rutinaCharla != null && _runner != null) _runner.StopCoroutine(_rutinaCharla);
    _rutina = null;
    _rutinaCharla = null;
    _runner = null;
    _charla?.Callar();
    _charla = null;

    if (_npcEscortado != null)
    {
        if (_npcEscortado.Brain?.CurrentState is Game.NPC.States.CinematicState cs)
            cs.CancelSequence();
        if (_animTocado != null) _animTocado.AllowManualRotation = true;
        if (_agenteTocado != null) _agenteTocado.updatePosition = _updatePositionOriginal;
        _npcEscortado = null;
        _animTocado = null;
    }

    DevolverVelocidad();
    QuitarTope();
}
```

> `CancelSequence()` pone `_sequenceCompleted = true` en `CinematicState`, que en su siguiente `OnUpdate` detecta la secuencia completa y hace la transición al estado idle, parando al NPC.

- [ ] **Paso 5: Actualizar TRACKER.md — INC-472**

Cambiar la fila de INC-472. Buscar `| INC-472 |` y cambiar estado a:

```
Resuelto
```

Columna de validación:

```
GuiarJugadorNode.Exit cancela CinematicState y restaura AllowManualRotation + updatePosition. Pendiente de probar: iniciar una escolta y parar el grafo a mitad; el NPC debe dejar de andar y volver al estado idle.
```

---

## Task 3: Cerrar en TRACKER los INC ya resueltos en código

**Files:**
- Modify: `TRACKER.md` (INC-474, INC-476, INC-477)

### Contexto

**INC-474 (MinimapUIController):** El código actual de `MinimapUIController.cs` ya usa `SetInside(Entry, bool)` y `SetEdge(Entry, bool)` con guards (`if (e.insideShown == shown) return;` y `if (e.edgeShown == shown) return;`) que evitan llamar a `SetActive` si el estado no ha cambiado. La auditoría lo marcó como pendiente, pero el fix ya está aplicado.

**INC-476:** `DEVELOPMENT_BUILD` ya no aparece en ningún `.cs` de `Assets/` (excluidas `Versiones antiguas`). `AppDomain.GetAssemblies` y `NarrativeFactCatalog` ya eliminados. `NavigationStatic` en `NavMeshFloorOnlySetup.cs` tiene un comentario explícito de que es obsoleto (aceptado). El estado "Aplicado; falta compilar en Unity" ya es pasado.

**INC-477 (`MirrorReflection`):** La búsqueda de GUID no encontró referencias porque `Sendero_PruebaWill.unity` está en binario. `grep -r MirrorReflection Assets/` confirma que el componente está referenciado en `Sendero_PruebaWill.unity` (binario) y que `WillTrialMazeBuilder.cs` lo añade dinámicamente con `AddComponent<MirrorReflection>()`. El componente SÍ tiene uso.

- [ ] **Paso 1: Actualizar INC-474 en TRACKER.md**

Buscar `| INC-474 |` y cambiar la columna de estado de `Parcialmente resuelto (...)` a:

```
Resuelto
```

Columna de validación:

```
MinimapUIController ya usa SetInside/SetEdge con guards que evitan SetActive repetidos. Verificado en código: líneas ~253-265 de MinimapUIController.cs.
```

- [ ] **Paso 2: Actualizar INC-476 en TRACKER.md**

Buscar `| INC-476 |` y cambiar la columna de estado de `Aplicado; falta compilar en Unity` a:

```
Resuelto
```

Columna de validación:

```
Cero DEVELOPMENT_BUILD en Assets/. AppDomain.GetAssemblies y NarrativeFactCatalog eliminados. NavigationStatic en NavMeshFloorOnlySetup.cs con aviso explícito (aceptado). Compilar en Unity para confirmar cero avisos UAC0009.
```

- [ ] **Paso 3: Actualizar INC-477 en TRACKER.md**

Buscar `| INC-477 |` y cambiar la columna de estado de `Pendiente de confirmar uso` a:

```
Resuelto (en uso)
```

Columna de validación:

```
MirrorReflection.cs referenciado en Sendero_PruebaWill.unity (binario) y añadido dinámicamente por WillTrialMazeBuilder.cs. Confirmado con grep. El sistema está en uso y no debe retirarse.
```

---

## Task 4: Verificar compilación y commit

**Files:**
- Read: `Logs/Editor.log`

- [ ] **Paso 1: Leer el log del Editor para detectar errores CS**

```
Logs/Editor.log
```

Buscar líneas con `error CS`. Si hay errores, corregirlos antes de continuar.

- [ ] **Paso 2: Commit**

```
git add Assets/Scripts/Battle/CierreDeBatalla.cs
git add Assets/Scripts/Rooms/BossArenaController.cs
git add Assets/NarrativeGraph/Runtime/Graph/NodeTypes/GuiarJugadorNode.cs
git add TRACKER.md
git commit -m "fix: INC-471/472 cierre batalla interrumpido y escolta sin cancelar (auditoría §19.5)"
```

---

## Self-review

**Cobertura:**
- INC-471 ✓ — `TerminarForzado` en `CierreDeBatalla` + llamada en `BossArenaController.OnDisable`
- INC-472 ✓ — `Exit` cancela `CinematicState` y restaura `AllowManualRotation` + `updatePosition`
- INC-474 ✓ — Solo TRACKER (código ya correcto)
- INC-476 ✓ — Solo TRACKER (código ya correcto)
- INC-477 ✓ — Solo TRACKER (en uso confirmado)

**Riesgos:**
- `TerminarForzado()` se llama al final del flujo normal también, sustituyendo el bucle anterior. Como es la misma lista/resultado y la función es idempotente (limpia campos antes de iterar), no hay doble ejecución.
- `CancelSequence()` en `Exit` solo actúa si el estado actual del NPC ES `CinematicState`. Si el NPC ya cambió de estado (poco probable si Exit se llama rápido), no hace nada — correcto.
- Llamar `TerminarForzado()` en `BossArenaController.OnDisable` cuando no hay ninguna pasada en curso (`_activosActual == null`) es un no-op por el guard.
