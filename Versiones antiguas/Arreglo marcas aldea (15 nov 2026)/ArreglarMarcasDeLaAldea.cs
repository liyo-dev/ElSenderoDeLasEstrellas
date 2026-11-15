using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Arreglo de una sola vez (INC-1211): los marcadores de "Aldea del mar · roster" están todos a
// y=25,02 y el suelo de la aldea a ~5. Se bajan al suelo y, si hay NavMesh cerca, al NavMesh.
// Es un script de un solo uso: cuando haya corrido, se mueve a "Versiones antiguas/" (regla 8).
internal static class ArreglarMarcasDeLaAldea
{
    private const string Clave = "ElSendero.ArregloMarcasAldea.Hecho2";
    private const string NombreEscena = "MainWorld";
    private const string Grupo = "Aldea del mar";
    private const float RadioNavMesh = 10f;

    [InitializeOnLoadMethod]
    private static void Arrancar()
    {
        if (SessionState.GetBool(Clave, false)) return;
        EditorApplication.update += Intentar;
    }

    private static void Intentar()
    {
        if (SessionState.GetBool(Clave, false))
        {
            EditorApplication.update -= Intentar;
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene escena = SceneManager.GetSceneByName(NombreEscena);
        bool abiertaPorMi = false;
        if (!escena.IsValid() || !escena.isLoaded)
        {
            // Se abre en aditivo para no cerrar la escena en la que esté trabajando Raúl.
            escena = EditorSceneManager.OpenScene("Assets/Scenes/Worlds/" + NombreEscena + ".unity",
                                                  OpenSceneMode.Additive);
            abiertaPorMi = true;
        }

        EditorApplication.update -= Intentar;
        SessionState.SetBool(Clave, true);
        Aplicar(escena, abiertaPorMi);
    }

    private static void Aplicar(Scene escena, bool abiertaPorMi)
    {
        bool estabaSucia = escena.isDirty;
        var informe = new StringBuilder("[Marcas aldea] Marcadores de la aldea del mar:\n");
        int movidos = 0, sinSuelo = 0, sinNavMesh = 0;

        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var punto in raiz.GetComponentsInChildren<NpcSpawnPoint>(true))
            {
                Transform t = punto.transform;
                if (t.parent == null || !t.parent.name.Contains(Grupo)) continue;

                Vector3 original = t.position;
                if (!BuscarSuelo(original, out Vector3 suelo))
                {
                    // Sobre la cubierta del puerto no hay suelo en la capa Floor: se usa el
                    // NavMesh directamente (el vértice más cercano en horizontal).
                    if (BuscarNavMesh(original, out Vector3 enLaMalla))
                    {
                        suelo = enLaMalla;
                        informe.AppendLine($"  · {t.name}: sin suelo; al NavMesh ({enLaMalla:F1}).");
                    }
                    else
                    {
                        sinSuelo++;
                        informe.AppendLine($"  · {t.name}: sin suelo debajo ni NavMesh cerca.");
                        continue;
                    }
                }

                Vector3 destino = suelo + Vector3.up * 0.02f;
                string nota = "";
                if (NavMesh.SamplePosition(destino, out var nav, RadioNavMesh, NavMesh.AllAreas))
                {
                    float lado = Vector2.Distance(
                        new Vector2(nav.position.x, nav.position.z),
                        new Vector2(destino.x, destino.z));

                    // En un marcador de spawn (no una marca de guion) sí se permite el arrastre
                    // corto: un NPC que no puede caminar es peor que unos metros de diferencia.
                    destino = nav.position;
                    if (lado > 0.75f)
                        nota = $" (arrastrado {lado:F1} m al NavMesh)";
                }
                else
                {
                    sinNavMesh++;
                    nota = " (sin NavMesh cerca: seguirá sin caminar)";
                }

                if (Vector3.Distance(original, destino) < 0.01f) continue;

                Undo.RecordObject(t, "Bajar marcador de la aldea al suelo");
                t.position = destino;
                EditorUtility.SetDirty(t);
                movidos++;
                informe.AppendLine($"  · {t.name}: {original:F1} → {destino:F1}{nota}");
            }
        }

        if (movidos > 0)
            EditorSceneManager.MarkSceneDirty(escena);

        informe.AppendLine($"Movidos: {movidos}. Sin suelo: {sinSuelo}. Sin NavMesh: {sinNavMesh}.");
        Debug.Log(informe.ToString());

        if (movidos > 0 && !estabaSucia)
        {
            EditorSceneManager.SaveScene(escena);
            Debug.Log("[Marcas aldea] MainWorld guardada.");
            if (abiertaPorMi)
                EditorSceneManager.CloseScene(escena, true);
        }
        else if (movidos > 0)
        {
            Debug.LogWarning("[Marcas aldea] MainWorld tenía cambios sin guardar: no la guardo. Pulsa Ctrl+S.");
        }
        else if (abiertaPorMi)
        {
            EditorSceneManager.CloseScene(escena, true);
        }
    }

    private static bool BuscarSuelo(Vector3 desde, out Vector3 suelo)
    {
        suelo = desde;
        int capaSuelo = LayerMask.NameToLayer("Floor");
        if (capaSuelo < 0) return false;

        Vector3 arriba = desde + Vector3.up * 80f;
        var impactos = Physics.RaycastAll(arriba, Vector3.down, 300f, ~0,
                                          QueryTriggerInteraction.Ignore);
        System.Array.Sort(impactos, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in impactos)
        {
            if (h.collider == null) continue;
            if (h.collider.gameObject.layer != capaSuelo) continue;
            if (h.collider.GetComponentInParent<NPCSimpleAnimator>() != null) continue;
            if (h.collider.GetComponent<Interactable>() != null) continue;
            suelo = h.point;
            return true;
        }

        return false;
    }

    /// Vértice del NavMesh más cercano en horizontal (la altura del marcador no importa: está a
    /// 20 m por encima). Sirve para los puntos que caen sobre la cubierta del puerto, que no es
    /// capa Floor.
    private static bool BuscarNavMesh(Vector3 desde, out Vector3 enLaMalla)
    {
        enLaMalla = desde;
        var datos = NavMesh.CalculateTriangulation();
        if (datos.vertices == null || datos.vertices.Length == 0) return false;

        float mejor = 12f * 12f;
        bool hay = false;
        foreach (var v in datos.vertices)
        {
            float d = (v.x - desde.x) * (v.x - desde.x) + (v.z - desde.z) * (v.z - desde.z);
            if (d >= mejor) continue;
            mejor = d;
            enLaMalla = v;
            hay = true;
        }

        return hay;
    }
}
