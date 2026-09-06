using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class FallingPathBuilder : MonoBehaviour
{
    [Header("Puertas (si no las asignas, se buscarán por nombre)")]
    public Transform doorW; // "Door_W"
    public Transform doorE; // "Door_E"

    [Header("Prefabs")]
    public GameObject tilePrefab;       // LM20/LM40 o un cube estilizado
    public GameObject endTorchPrefab;   // antorcha con TorchInteract

    [Header("Contenedores (hijos vacíos opcionales)")]
    public Transform pathContainer;     // por orden, "Path"
    public Transform endContainer;      // por orden, "End"

    [Header("Geometría del camino")]
    public float step = 2.5f;           // distancia entre centros de baldosas
    public float doorClearance = 2.0f;  // deja margen desde cada puerta
    public float lateralJitter = 0.5f;  // serpenteo lateral máximo
    public bool addGaps = true;         // huecos para salto
    public int gapEvery = 5;            // cada N baldosas, potencial hueco
    [Range(0f,1f)] public float gapChance = 0.5f;

    [Header("Baldosas que caen")]
    public int safeTilesAtStart = 1;    // primeras baldosas NO caen
    public Vector2 fallDelayRange = new Vector2(0.25f, 0.6f);
    public float respawnAfter = -1f;    // <0 = no respawn

    [Header("Colocación sobre el suelo")]
    public bool snapToGround = true;
    public float raycastHeight = 8f;
    public LayerMask groundMask = ~0;   // All

    [Header("Antorcha final")]
    public float torchOffsetFromExit = 3.0f; // distancia antes de la salida (hacia atrás)
    public bool faceTorchToEntry = true;

    [Header("Auto-enganchar lógica de sala (opcional)")]
    public FallingPathRoom roomLogic;   // si está, se autoconecta endTorch/exitDoor
    public DoorGate exitDoorOverride;   // si lo dejas vacío, busca Door_E/Barrier

    [Header("Auto-build")]
    public bool buildOnStart = false;

    readonly List<GameObject> spawned = new List<GameObject>();

    void OnEnable()
    {
        if (Application.isPlaying && buildOnStart)
            StartCoroutine(DelayedBuild());
    }
    IEnumerator DelayedBuild()
    {
        yield return null; // espera 1 frame
#if UNITY_EDITOR
        // si el usuario tenía seleccionado algo dentro de Path/End, deselecciona
        if (UnityEditor.Selection.activeTransform &&
            ((pathContainer && UnityEditor.Selection.activeTransform.IsChildOf(pathContainer)) ||
             (endContainer  && UnityEditor.Selection.activeTransform.IsChildOf(endContainer))))
        {
            UnityEditor.Selection.activeObject = null;
        }
#endif
#if UNITY_EDITOR
        Rebuild();
#endif
    }


#if UNITY_EDITOR
    [ContextMenu("Rebuild (Editor)")]
    public void Rebuild() { BuildInternal(); }

    [ContextMenu("Clear (Editor)")]
    public void Clear() { ClearContainer(); }
#endif

    void BuildInternal()
    {
        // Buscar puertas si no están asignadas
        if (doorW == null) doorW = transform.Find("Door_W");
        if (doorE == null) doorE = transform.Find("Door_E");

        if (doorW == null || doorE == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[FallingPathBuilder] No encuentro Door_W/Door_E. Asigna referencias o nómbralas así.");
#endif
            return;
        }
        if (tilePrefab == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[FallingPathBuilder] Falta tilePrefab.");
#endif
            return;
        }
        if (pathContainer == null)
        {
            var pc = transform.Find("Path");
            pathContainer = pc ? pc : new GameObject("Path").transform;
            pathContainer.SetParent(transform, false);
        }
        if (endContainer == null)
        {
            var ec = transform.Find("End");
            endContainer = ec ? ec : new GameObject("End").transform;
            endContainer.SetParent(transform, false);
        }

        ClearContainer();

        // Direcciones en XZ
        Vector3 a = doorW.position;
        Vector3 b = doorE.position;
        a.y = b.y = transform.position.y; // base Y de la sala (ajustaremos por raycast luego)

        Vector3 dir = (b - a);
        dir.y = 0f;
        float dist = dir.magnitude;
        if (dist < 0.1f) { Debug.LogWarning("[FallingPathBuilder] Puertas demasiado juntas."); return; }
        dir.Normalize();

        // corredor útil (quitamos margen cerca de puertas)
        float usableDist = Mathf.Max(0f, dist - doorClearance * 2f);
        int steps = Mathf.Max(2, Mathf.FloorToInt(usableDist / Mathf.Max(0.1f, step)));

        Vector3 start = a + dir * doorClearance;
        Vector3 right = new Vector3(-dir.z, 0f, dir.x);

        int placedCount = 0;
        for (int i = 0; i <= steps; i++)
        {
            // ¿Creamos hueco?
            if (addGaps && i >= safeTilesAtStart && gapEvery > 0 && (i % gapEvery == 0) && Random.value < gapChance)
            {
                continue; // saltamos esta baldos
            }

            Vector3 pos = start + dir * (i * step)
                               + right * Random.Range(-lateralJitter, lateralJitter);

            // Ajustar a suelo
            if (snapToGround)
            {
                var origin = pos + Vector3.up * raycastHeight;
                if (Physics.Raycast(origin, Vector3.down, out var hit, raycastHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
                    pos = hit.point;
            }

            var tile = Instantiate(tilePrefab, pos, Quaternion.identity, pathContainer);
            spawned.Add(tile);

            // Asegurar collider/rigidbody
            var col = tile.GetComponent<Collider>();
            if (!col) col = tile.AddComponent<BoxCollider>();
            col.isTrigger = true;

            var rb = tile.GetComponent<Rigidbody>();
            if (!rb) rb = tile.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Asegurar FallingTile
            var ft = tile.GetComponent<FallingTile>();
            if (!ft) ft = tile.AddComponent<FallingTile>();
            ft.delay = Random.Range(fallDelayRange.x, fallDelayRange.y);
            ft.respawnAfter = respawnAfter;

            // Hacer seguras las primeras N
            if (placedCount < safeTilesAtStart)
            {
                ft.enabled = false; // no caen
            }

            placedCount++;
        }

        // Antorcha al final del camino (antes de la salida)
        if (endTorchPrefab)
        {
            Vector3 torchPos = b - dir * torchOffsetFromExit;
            if (snapToGround)
            {
                var origin = torchPos + Vector3.up * raycastHeight;
                if (Physics.Raycast(origin, Vector3.down, out var hit, raycastHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
                    torchPos = hit.point;
            }

            var torch = Instantiate(endTorchPrefab, torchPos, faceTorchToEntry ? Quaternion.LookRotation(-dir) : Quaternion.identity, endContainer);
            spawned.Add(torch);

            // Auto-wire con FallingPathRoom si está
            var ti = torch.GetComponent<TorchInteract>();
            if (roomLogic && ti)
            {
                roomLogic.endTorch = ti;
                if (!roomLogic.exitDoor)
                {
                    var exitDoor = exitDoorOverride ? exitDoorOverride : FindExitDoorGate(doorE);
                    roomLogic.exitDoor = exitDoor;
                }
                // RoomGoal lo pones en la raíz del prefab; no lo tocamos
            }
        }
    }

    DoorGate FindExitDoorGate(Transform doorETransform)
    {
        if (!doorETransform) return null;
        var gate = doorETransform.GetComponentInChildren<DoorGate>(true);
        if (!gate)
        {
            // intenta buscar un hijo llamado Barrier
            var barrier = doorETransform.Find("Barrier");
            if (barrier) gate = barrier.GetComponent<DoorGate>();
        }
        return gate;
    }
    
    void ClearContainer()
    {
#if UNITY_EDITOR
        var sel = UnityEditor.Selection.activeTransform;
        if (sel && ((pathContainer && sel.IsChildOf(pathContainer)) || (endContainer && sel.IsChildOf(endContainer))))
            UnityEditor.Selection.activeObject = null;
#endif
        void Clear(Transform t)
        {
            if (!t) return;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var go = t.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(go);
#if UNITY_EDITOR
                else UnityEditor.Undo.DestroyObjectImmediate(go);
#else
            else DestroyImmediate(go);
#endif
            }
        }
        Clear(pathContainer);
        Clear(endContainer);
    }


}
