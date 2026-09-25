using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// Vuelve a pegar al suelo las marcas y los marcadores de spawn cuando el decorado se mueve
/// (INC-392).
///
/// ── Por qué ───────────────────────────────────────────────────────────────────────────────────
/// «He cambiado el diseño del valle… y ahora se ha roto la secuencia», con trece errores iguales:
///
///     [NpcSpawner] El marcador 'SPAWN_NPC_Archimago' NO tiene NavMesh en 3 m. El NPC aparecerá
///     ahí igualmente, pero su NavMeshAgent no funcionará (no podrá caminar).
///
/// Una secuencia como el prólogo está atada al decorado por dos sitios, y los dos son coordenadas
/// a pelo: los MARCADORES de spawn (dónde aparece cada NPC) y las MARCAS del SequenceStage (M_*:
/// a dónde camina cada uno, dónde se planta el Mago Oscuro, por dónde se cruza el puente). Al
/// rehacer el valle, el suelo se mueve y esos puntos se quedan flotando o enterrados — y entonces
/// no hay NavMesh debajo, los agentes no arrancan y todo el mundo anda en línea recta.
///
/// Esto no adivina el diseño nuevo: lo que hace es bajar cada punto al SUELO que tenga debajo y,
/// si hay NavMesh cerca, pegarlo a él. Lo que se haya movido de sitio de verdad (el puente ahora
/// está en otro lado) hay que moverlo a mano — y por eso el informe dice, uno por uno, cuáles no
/// tienen suelo ni NavMesh cerca.
///
/// ── Orden de uso ──────────────────────────────────────────────────────────────────────────────
///   1. Abre Prologo_Valle.
///   2. «Mundo: rehornear el NavMesh de las escenas abiertas» (el suelo nuevo).
///   3. «Prólogo: pegar marcas y marcadores al suelo» (esto).
///   4. Ctrl+S y PREPARAR TODO.
///
/// Es idempotente: si ya está todo pegado, no toca nada.
public static class PegarMarcasAlSuelo
{
    /// Desde cuánto más arriba se tira el rayo para buscar suelo, y cuánto baja.
    private const float AlturaDelRayo = 80f;
    private const float LargoDelRayo = 300f;

    /// Radio de búsqueda de NavMesh. Más generoso que el del spawner (3 m) porque aquí se está
    /// arreglando a propósito, no sorteando un fallo.
    private const float RadioDeNavMesh = 6f;

    [MenuItem("El Sendero/Prólogo: diagnóstico de marcas y marcadores", priority = 30)]
    public static void Diagnostico() => Ejecutar(arreglar: false);

    [MenuItem("El Sendero/Prólogo: pegar marcas y marcadores al suelo", priority = 31)]
    public static void Arreglar() => Ejecutar(arreglar: true);

    private static void Ejecutar(bool arreglar)
    {
        var puntos = Recoger();
        if (puntos.Count == 0)
        {
            EditorUtility.DisplayDialog("Marcas",
                "No he encontrado ni marcadores de spawn ni marcas M_* en las escenas abiertas. " +
                "Abre Prologo_Valle y vuelve a darle.", "Vale");
            return;
        }

        bool hayNavMesh = HayNavMesh();

        int pegados = 0, sinSuelo = 0, sinNavMesh = 0;
        var problemas = new List<string>();

        foreach (var t in puntos)
        {
            Vector3 original = t.position;
            Vector3 destino = original;

            // 1. El suelo. Se tira el rayo desde bien arriba y se coge el primer impacto que sea
            //    suelo de verdad: ni un disparador, ni otro personaje, ni el propio marcador.
            if (BuscarSuelo(original, out Vector3 suelo))
            {
                // Una marca EN EL AIRE (a dónde vuela o salta alguien) no se baja al suelo
                // (INC-398): M_Aire_Oscuro estaba a 6,5 m y acabó en el suelo de la plaza.
                if (original.y - suelo.y > 1.0f)
                {
                    problemas.Add($"• '{t.name}': está a {original.y - suelo.y:F1} m del suelo; " +
                                  "la dejo en el aire (se da por hecho que es a propósito).");
                    continue;
                }
                destino = suelo;
            }
            else
            {
                sinSuelo++;
                problemas.Add($"• '{t.name}': no hay suelo debajo ni encima (está fuera del mapa).");
                continue;
            }

            // 2. El NavMesh, si lo hay. Sin esto el NPC aparece pero no camina, que es justo el
            //    error del log.
            if (hayNavMesh)
            {
                if (NavMesh.SamplePosition(destino, out var hit, RadioDeNavMesh, NavMesh.AllAreas))
                {
                    // Solo se pega si el NavMesh está DEBAJO, no al lado (INC-398). Arrastrar de
                    // lado hasta el NavMesh más cercano es lo que llevó 79 puntos al borde del
                    // camino el 23 sep, con un NavMesh que solo cubría el camino: todos en fila.
                    // Si el NavMesh está a un lado, el problema es el NavMesh, no la marca.
                    Vector2 lado = new Vector2(hit.position.x - destino.x, hit.position.z - destino.z);
                    if (lado.magnitude <= 0.5f)
                    {
                        destino = hit.position;
                    }
                    else
                    {
                        sinNavMesh++;
                        problemas.Add($"• '{t.name}': el NavMesh más cercano está a {lado.magnitude:F1} m " +
                                      "de lado. No la arrastro: o el NavMesh no cubre ese sitio (rehornéalo) " +
                                      "o la marca hay que moverla a mano.");
                    }
                }
                else
                {
                    sinNavMesh++;
                    problemas.Add($"• '{t.name}': hay suelo, pero NO hay NavMesh a {RadioDeNavMesh} m. " +
                                  "O ese sitio ya no es caminable, o hay que moverlo a mano.");
                }
            }

            if (Vector3.Distance(original, destino) < 0.02f) continue;

            if (arreglar)
            {
                Undo.RecordObject(t, "Pegar marca al suelo");
                t.position = destino;
                EditorUtility.SetDirty(t);
                EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
            }

            pegados++;
            Debug.Log($"[Marcas] '{t.name}': {original.ToString("F1")} → {destino.ToString("F1")} " +
                      $"({Vector3.Distance(original, destino):F1} m).", t.gameObject);
        }

        string informe =
            $"{puntos.Count} punto(s) mirados.\n" +
            $"{(arreglar ? "Movidos" : "Se moverían")}: {pegados}\n" +
            $"Sin suelo debajo: {sinSuelo}\n" +
            (hayNavMesh ? $"Con suelo pero sin NavMesh cerca: {sinNavMesh}\n"
                        : "No hay NavMesh horneado en las escenas abiertas: hornéalo primero " +
                          "(El Sendero → Mundo: rehornear el NavMesh) y vuelve a darle.\n");

        if (problemas.Count > 0)
            Debug.LogWarning("[Marcas] Puntos que hay que mirar a mano:\n" + string.Join("\n", problemas.Take(40)));

        Debug.Log("[Marcas] " + informe.Replace("\n", " | "));

        EditorUtility.DisplayDialog(arreglar ? "Marcas pegadas al suelo" : "Diagnóstico de marcas",
            informe + (problemas.Count > 0
                ? $"\nHay {problemas.Count} punto(s) que necesitan mano: están en la consola."
                : "\nTodo cuadra.") +
            (arreglar ? "\n\nGuarda con Ctrl+S." : ""), "Vale");
    }

    /// Marcadores de spawn y marcas de escena (las que empiezan por M_, que es como las nombra
    /// todo el montaje del prólogo).
    private static List<Transform> Recoger()
    {
        var lista = new List<Transform>();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (!escena.isLoaded) continue;

            foreach (var raiz in escena.GetRootGameObjects())
            {
                foreach (var p in raiz.GetComponentsInChildren<NpcSpawnPoint>(true))
                    lista.Add(p.transform);

                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("M_") && !lista.Contains(t))
                        lista.Add(t);
            }
        }

        return lista;
    }

    private static bool HayNavMesh()
    {
        var datos = NavMesh.CalculateTriangulation();
        if (datos.vertices != null && datos.vertices.Length > 0) return true;

        // En el editor, fuera de Play, puede no haber NavMesh cargado aunque la superficie exista.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (!escena.isLoaded) continue;
            foreach (var raiz in escena.GetRootGameObjects())
                if (raiz.GetComponentInChildren<NavMeshSurface>(true) != null) return true;
        }
        return false;
    }

    /// El suelo debajo (o encima, si el punto se quedó enterrado) del sitio donde está el punto.
    private static bool BuscarSuelo(Vector3 desde, out Vector3 suelo)
    {
        suelo = desde;

        Vector3 arriba = desde + Vector3.up * AlturaDelRayo;
        var impactos = Physics.RaycastAll(arriba, Vector3.down, LargoDelRayo, ~0,
                                          QueryTriggerInteraction.Ignore);

        // De arriba abajo, el primero que sea suelo: ni personajes ni cosas que se mueven.
        foreach (var h in impactos.OrderBy(h => h.distance))
        {
            if (EsSuelo(h.collider))
            {
                suelo = h.point + Vector3.up * 0.02f;
                return true;
            }
        }

        return false;
    }

    private static bool EsSuelo(Collider c)
    {
        if (c == null) return false;
        if (c.GetComponentInParent<CharacterController>() != null) return false;
        if (c.GetComponentInParent<NavMeshAgent>() != null) return false;
        if (c.GetComponentInParent<NpcSpawnPoint>() != null) return false;

        // Un Terrain siempre es suelo; lo demás vale mientras sea estático o parte del decorado.
        return true;
    }
}
