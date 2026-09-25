using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// Devuelve a su sitio las marcas y los marcadores del prólogo que «Pegar marcas al suelo» movió
/// el 23 sep por la noche (INC-398).
///
/// ── Qué pasó ──────────────────────────────────────────────────────────────────────────────────
/// «Pegar marcas y marcadores al suelo» (INC-392) baja cada punto al suelo y, si hay NavMesh
/// cerca, lo PEGA al NavMesh. Pero el NavMesh de aquella noche estaba horneado con las matas de
/// hierba (INC-396) y solo quedaba limpio el camino de tierra. Así que 79 puntos acabaron
/// arrastrados hasta el borde del camino: en el log salen casi todos a z = 6001,6 o z = 5999,4,
/// los dos bordes de la misma franja. Es el «los NPCs se ponen ahora en fila».
///
/// Peor aún con las marcas AÉREAS: M_Aire_Oscuro estaba a 6,5 m del suelo y acabó en el suelo.
///
/// Los sitios de antes salen del propio log de aquella noche (Logs/Editor.log, «[Marcas] 'X':
/// antes → después»), así que esto no adivina nada: es deshacer.
///
/// ── Regla ─────────────────────────────────────────────────────────────────────────────────────
/// - Solo se toca un punto si SIGUE donde lo dejó el pegado (si lo has movido tú después, se
///   respeta y se avisa).
/// - Si el pegado solo le cambió la altura (misma X/Z), se deja: eso sí era bajarlo al suelo.
/// - Si lo arrastró de lado, vuelve a su X/Z de antes. La altura: la de antes si era una marca en
///   el aire; si no, la del suelo que tenga debajo ahora.
public static class DevolverMarcasASuSitio
{
    private readonly struct Marca
    {
        public readonly string nombre;
        public readonly Vector3 antes;
        public readonly Vector3 despues;
        public Marca(string n, Vector3 a, Vector3 d) { nombre = n; antes = a; despues = d; }
    }

    private static readonly Marca[] Movidas =
    {
        new Marca("M_Apertura", new Vector3(6006.2f, 100.0f, 6001.4f), new Vector3(6006.2f, 100.1f, 6001.4f)),
        new Marca("M_Horno", new Vector3(5995.6f, 100.0f, 6002.2f), new Vector3(5995.6f, 100.1f, 6001.6f)),
        new Marca("M_Carreta", new Vector3(6008.4f, 100.0f, 6000.6f), new Vector3(6008.4f, 100.8f, 6000.6f)),
        new Marca("M_Globo", new Vector3(5990.8f, 100.0f, 6001.6f), new Vector3(5990.8f, 100.1f, 6001.6f)),
        new Marca("M_Mesa", new Vector3(6010.0f, 100.0f, 6001.5f), new Vector3(6010.0f, 100.2f, 6001.5f)),
        new Marca("M_Mesa_Liora", new Vector3(6012.5f, 100.0f, 6003.7f), new Vector3(6012.5f, 100.2f, 6001.6f)),
        new Marca("M_Viga", new Vector3(6004.6f, 100.0f, 5999.9f), new Vector3(6004.6f, 100.2f, 5999.9f)),
        new Marca("M_Despedida_Mago", new Vector3(6017.4f, 100.0f, 6001.4f), new Vector3(6017.4f, 100.2f, 6001.4f)),
        new Marca("M_Despedida_Liora", new Vector3(6018.0f, 100.0f, 5999.9f), new Vector3(6018.0f, 100.2f, 5999.9f)),
        new Marca("M_Duelo_Mago", new Vector3(6000.5f, 100.0f, 5999.0f), new Vector3(6000.5f, 100.1f, 5999.4f)),
        new Marca("M_Duelo_Oscuro", new Vector3(6000.5f, 100.0f, 6005.0f), new Vector3(6000.5f, 100.1f, 6001.6f)),
        new Marca("M_Espera_Oscuro", new Vector3(6000.0f, 100.0f, 6068.0f), new Vector3(6000.0f, 106.6f, 6068.0f)),
        new Marca("M_Colina_Oscuro", new Vector3(5957.7f, 104.0f, 5996.9f), new Vector3(5957.7f, 116.7f, 5996.9f)),
        new Marca("M_Plaza_Liora", new Vector3(6004.5f, 100.1f, 6002.9f), new Vector3(6004.5f, 100.1f, 6001.6f)),
        new Marca("M_Aldeano_01", new Vector3(5998.3f, 100.1f, 6004.5f), new Vector3(5998.3f, 100.1f, 6001.6f)),
        new Marca("M_Aldeano_02", new Vector3(5999.0f, 100.1f, 5997.0f), new Vector3(5999.0f, 100.1f, 5999.4f)),
        new Marca("M_Aldeano_03", new Vector3(6003.5f, 100.1f, 6005.8f), new Vector3(6003.5f, 100.1f, 6001.6f)),
        new Marca("M_Aldeano_04", new Vector3(6005.0f, 100.2f, 5996.5f), new Vector3(6005.0f, 100.2f, 5999.4f)),
        new Marca("M_Aldeano_05", new Vector3(5993.0f, 100.2f, 6000.5f), new Vector3(5993.0f, 100.1f, 6000.5f)),
        new Marca("M_Aldeano_06", new Vector3(6008.5f, 100.1f, 6003.0f), new Vector3(6008.5f, 100.1f, 6001.6f)),
        new Marca("M_Aldeano_07", new Vector3(5993.9f, 100.0f, 6003.3f), new Vector3(5993.9f, 100.1f, 6001.6f)),
        new Marca("M_Aldeano_08", new Vector3(6000.5f, 100.1f, 6005.5f), new Vector3(6000.5f, 100.1f, 6001.6f)),
        new Marca("M_Aldeano_09", new Vector3(6010.3f, 100.2f, 5998.7f), new Vector3(6010.3f, 100.2f, 5999.4f)),
        new Marca("M_Aldeano_10", new Vector3(5990.9f, 100.2f, 6004.2f), new Vector3(5990.9f, 100.1f, 6001.6f)),
        new Marca("M_Cresta", new Vector3(5925.0f, 137.1f, 6000.0f), new Vector3(5925.0f, 137.1f, 6000.0f)),
        new Marca("M_Ladera_02", new Vector3(5968.0f, 107.6f, 6000.0f), new Vector3(5968.0f, 107.6f, 6000.0f)),
        new Marca("M_Entrada_Villa", new Vector3(5988.0f, 100.1f, 6000.5f), new Vector3(5990.4f, 100.1f, 6000.5f)),
        new Marca("M_Duelo2_Oscuro", new Vector3(5990.4f, 100.2f, 6000.5f), new Vector3(5990.4f, 100.1f, 6000.5f)),
        new Marca("M_Duelo2_Mago", new Vector3(5995.0f, 100.2f, 6000.5f), new Vector3(5995.0f, 100.1f, 6000.5f)),
        new Marca("M_Puente_01", new Vector3(6026.0f, 100.2f, 6002.5f), new Vector3(6026.0f, 100.4f, 6002.5f)),
        new Marca("M_Puente_02", new Vector3(6029.5f, 100.1f, 5999.0f), new Vector3(6027.0f, 100.4f, 5998.9f)),
        new Marca("M_Puente_03", new Vector3(6031.7f, 100.1f, 6005.4f), new Vector3(6027.0f, 100.4f, 6004.3f)),
        new Marca("M_Puente_04", new Vector3(6025.0f, 100.1f, 5996.0f), new Vector3(6025.0f, 100.2f, 5996.0f)),
        new Marca("M_Puente_05", new Vector3(6033.0f, 100.1f, 6000.5f), new Vector3(6033.0f, 100.0f, 6000.5f)),
        new Marca("M_Puente_06", new Vector3(6026.5f, 100.2f, 6007.4f), new Vector3(6026.5f, 100.3f, 6007.4f)),
        new Marca("M_Puente_07", new Vector3(6034.5f, 100.1f, 5997.0f), new Vector3(6034.5f, 100.0f, 5997.0f)),
        new Marca("M_Puente_08", new Vector3(6030.7f, 100.1f, 6000.8f), new Vector3(6027.0f, 100.5f, 6000.9f)),
        new Marca("M_Puente_10", new Vector3(6032.0f, 100.1f, 5993.5f), new Vector3(6027.0f, 100.2f, 5993.5f)),
        new Marca("M_Baile", new Vector3(6002.5f, 100.1f, 6004.2f), new Vector3(6002.5f, 100.1f, 6001.6f)),
        new Marca("M_Duelo_Choque", new Vector3(5992.0f, 100.2f, 6000.5f), new Vector3(5992.0f, 100.1f, 6000.5f)),
        new Marca("M_Duelo_Caida", new Vector3(5997.5f, 100.2f, 6000.5f), new Vector3(5997.5f, 100.1f, 6000.5f)),
        new Marca("M_Huida_01", new Vector3(6012.0f, 100.2f, 5999.0f), new Vector3(6012.0f, 100.2f, 5999.4f)),
        new Marca("M_Huida_02", new Vector3(6014.5f, 100.1f, 5996.5f), new Vector3(6014.5f, 100.2f, 5999.4f)),
        new Marca("M_Huida_05", new Vector3(6013.0f, 100.1f, 5995.0f), new Vector3(6012.6f, 100.2f, 5999.4f)),
        new Marca("M_Huida_06", new Vector3(6015.5f, 100.1f, 6002.5f), new Vector3(6015.5f, 100.2f, 6001.6f)),
        new Marca("M_Huida_07", new Vector3(6010.6f, 100.2f, 5997.0f), new Vector3(6010.6f, 100.2f, 5999.4f)),
        new Marca("M_Huida_08", new Vector3(6017.0f, 100.1f, 5997.0f), new Vector3(6019.0f, 100.1f, 5997.5f)),
        new Marca("M_Huida_09", new Vector3(6012.5f, 100.2f, 6003.5f), new Vector3(6012.5f, 100.2f, 6001.6f)),
        new Marca("M_Lejos_05", new Vector3(6038.0f, 100.0f, 5993.0f), new Vector3(6038.0f, 100.0f, 5993.0f)),
        new Marca("M_Rio_Camino", new Vector3(6014.5f, 100.2f, 6001.5f), new Vector3(6014.5f, 100.2f, 6001.5f)),
        new Marca("M_Rio_Mago", new Vector3(6017.6f, 100.1f, 6003.8f), new Vector3(6019.0f, 100.1f, 6003.8f)),
        new Marca("M_Rio_Liora", new Vector3(6016.4f, 100.1f, 6003.4f), new Vector3(6016.4f, 100.2f, 6001.6f)),
        new Marca("M_Rio_Fin_Mago", new Vector3(6017.8f, 100.1f, 5997.4f), new Vector3(6019.0f, 100.1f, 5997.5f)),
        new Marca("M_Rio_Fin_Liora", new Vector3(6016.6f, 100.1f, 5997.0f), new Vector3(6016.6f, 100.2f, 5999.4f)),
        new Marca("M_Duelo3_Oscuro", new Vector3(6000.0f, 100.1f, 5997.5f), new Vector3(6000.0f, 100.1f, 5999.4f)),
        new Marca("M_Duelo3_Mago", new Vector3(6006.0f, 100.2f, 5997.5f), new Vector3(6006.0f, 100.2f, 5999.4f)),
        new Marca("M_Duelo3_Choque", new Vector3(6003.0f, 100.1f, 5997.5f), new Vector3(6003.0f, 100.2f, 5999.4f)),
        new Marca("M_Puente_Ent", new Vector3(6017.1f, 100.1f, 6000.4f), new Vector3(6017.1f, 100.2f, 6000.4f)),
        new Marca("M_Puente_Sal", new Vector3(6028.7f, 100.0f, 6000.4f), new Vector3(6027.0f, 100.5f, 6000.4f)),
        new Marca("M_Aire_Oscuro", new Vector3(5993.0f, 106.5f, 5997.5f), new Vector3(5993.0f, 100.1f, 5999.4f)),
        new Marca("M_Carrera_Mago", new Vector3(6002.0f, 100.0f, 5997.5f), new Vector3(6002.0f, 100.2f, 5999.4f)),
        new Marca("M_Salto_Mago_Aire", new Vector3(5998.5f, 102.5f, 5997.5f), new Vector3(5998.5f, 100.1f, 5999.4f)),
        new Marca("M_Aire_Oscuro_2", new Vector3(5996.0f, 107.5f, 6002.5f), new Vector3(5996.0f, 100.1f, 6001.6f)),
        new Marca("M_Picado_Oscuro", new Vector3(6004.5f, 100.8f, 5997.5f), new Vector3(6004.5f, 100.2f, 5999.4f)),
        new Marca("M_Aire_Oscuro_3", new Vector3(5995.5f, 108.0f, 5997.5f), new Vector3(5995.5f, 100.1f, 5999.4f)),
        new Marca("M_Aire_Mago_1", new Vector3(6009.5f, 102.2f, 5995.5f), new Vector3(6009.7f, 100.2f, 5999.4f)),
        new Marca("SpawnPoint_NPC_Archimago", new Vector3(6006.2f, 100.0f, 6001.4f), new Vector3(6006.2f, 100.1f, 6001.4f)),
        new Marca("SpawnPoint_NPC_Liora", new Vector3(6012.5f, 100.0f, 6003.7f), new Vector3(6012.5f, 100.2f, 6001.6f)),
        new Marca("SpawnPoint_NPC_MagoOscuro", new Vector3(6000.5f, 100.0f, 6005.0f), new Vector3(6000.5f, 100.1f, 6001.6f)),
        new Marca("SpawnPoint_NPC_Aldeano_01", new Vector3(5998.3f, 100.1f, 6004.5f), new Vector3(5998.3f, 100.1f, 6001.6f)),
        new Marca("SpawnPoint_NPC_Aldeano_02", new Vector3(5999.0f, 100.1f, 5997.0f), new Vector3(5999.0f, 100.1f, 5999.4f)),
        new Marca("SpawnPoint_NPC_Aldeano_03", new Vector3(6003.5f, 100.1f, 6005.8f), new Vector3(6003.5f, 100.1f, 6001.6f)),
        new Marca("SpawnPoint_NPC_Aldeano_04", new Vector3(6005.0f, 100.2f, 5996.5f), new Vector3(6005.0f, 100.2f, 5999.4f)),
        new Marca("SpawnPoint_NPC_Aldeano_05", new Vector3(5993.0f, 100.2f, 6000.5f), new Vector3(5993.0f, 100.1f, 6000.5f)),
        new Marca("SpawnPoint_NPC_Aldeano_06", new Vector3(6008.5f, 100.1f, 6003.0f), new Vector3(6008.5f, 100.1f, 6001.6f)),
        new Marca("SpawnPoint_NPC_Aldeano_07", new Vector3(5993.9f, 100.0f, 6003.3f), new Vector3(5993.9f, 100.1f, 6001.6f)),
        new Marca("SpawnPoint_NPC_Aldeano_08", new Vector3(6000.5f, 100.1f, 6005.5f), new Vector3(6000.5f, 100.1f, 6001.6f)),
        new Marca("SpawnPoint_NPC_Aldeano_09", new Vector3(6010.3f, 100.2f, 5998.7f), new Vector3(6010.3f, 100.2f, 5999.4f)),
        new Marca("SpawnPoint_NPC_Aldeano_10", new Vector3(5990.9f, 100.2f, 6004.2f), new Vector3(5990.9f, 100.1f, 6001.6f)),
    };

    [MenuItem("El Sendero/Archivo/Prólogo: devolver las marcas a su sitio (deshacer el pegado del 23 sep)", priority = 32)]
    public static void Menu()
    {
        var porNombre = new Dictionary<string, List<Transform>>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (!escena.isLoaded || !escena.name.StartsWith("Prologo")) continue;
            foreach (var raiz in escena.GetRootGameObjects())
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    if (!porNombre.TryGetValue(t.name, out var lista))
                        porNombre[t.name] = lista = new List<Transform>();
                    lista.Add(t);
                }
        }

        if (porNombre.Count == 0)
        {
            EditorUtility.DisplayDialog("Marcas", "Abre Prologo_Valle y vuelve a darle.", "Vale");
            return;
        }

        int devueltas = 0, soloAltura = 0, tocadasAMano = 0, noEstan = 0;
        var avisos = new List<string>();

        foreach (var m in Movidas)
        {
            if (!porNombre.TryGetValue(m.nombre, out var lista) || lista.Count == 0)
            {
                noEstan++;
                avisos.Add($"• '{m.nombre}': no está en la escena.");
                continue;
            }
            if (lista.Count > 1)
                avisos.Add($"• '{m.nombre}': hay {lista.Count} con ese nombre; se tocan todas las que sigan en el sitio del pegado.");

            Vector2 xzAntes = new Vector2(m.antes.x, m.antes.z);
            Vector2 xzDespues = new Vector2(m.despues.x, m.despues.z);
            bool loArrastro = Vector2.Distance(xzAntes, xzDespues) > 0.05f;

            foreach (var t in lista)
            {
                // ¿Sigue donde lo dejó el pegado? El log redondea a 0,1 m.
                if (Vector3.Distance(t.position, m.despues) > 0.15f)
                {
                    tocadasAMano++;
                    avisos.Add($"• '{m.nombre}': ya no está donde lo dejó el pegado " +
                               $"({t.position.ToString("F1")}); lo has movido tú, no lo toco.");
                    continue;
                }

                if (!loArrastro) { soloAltura++; continue; }

                bool enElAire = m.antes.y - m.despues.y > 1.0f;
                Vector3 destino = new Vector3(m.antes.x, m.antes.y, m.antes.z);
                if (!enElAire && Suelo(destino, out float y)) destino.y = y;

                Undo.RecordObject(t, "Devolver marca a su sitio");
                t.position = destino;
                EditorUtility.SetDirty(t);
                EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                devueltas++;

                Debug.Log($"[Marcas] '{m.nombre}' vuelve a su sitio: {m.despues.ToString("F1")} → " +
                          $"{destino.ToString("F1")}{(enElAire ? " (en el aire, como estaba)" : "")}.", t);
            }
        }

        if (avisos.Count > 0)
            Debug.LogWarning("[Marcas] Al devolver las marcas:\n" + string.Join("\n", avisos));

        EditorUtility.DisplayDialog("Marcas devueltas",
            $"Devueltas a su sitio: {devueltas}\n" +
            $"Solo les cambió la altura (se dejan): {soloAltura}\n" +
            $"Movidas por ti después (no se tocan): {tocadasAMano}\n" +
            $"No están en la escena: {noEstan}\n\n" +
            "Guarda con Ctrl+S.", "Vale");
    }

    /// La altura del suelo bajo un punto: el primer impacto de arriba abajo que no sea un
    /// personaje ni un disparador. Si no encuentra nada cerca, se queda la altura de antes, que
    /// es la que tenía el punto cuando funcionaba.
    private static bool Suelo(Vector3 punto, out float y)
    {
        y = punto.y;
        // Desde poco más arriba del punto, no desde el cielo: en la plaza hay farolas, tejados y
        // copas de árbol, y el primer impacto desde ochenta metros podría ser cualquiera de ellos.
        var impactos = Physics.RaycastAll(punto + Vector3.up * 1.5f, Vector3.down, 4f, ~0,
                                          QueryTriggerInteraction.Ignore);
        System.Array.Sort(impactos, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in impactos)
        {
            if (h.collider == null) continue;
            if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
            if (h.collider.GetComponentInParent<NavMeshAgent>() != null) continue;
            if (h.collider.GetComponentInParent<NpcSpawnPoint>() != null) continue;
            y = h.point.y + 0.02f;
            return true;
        }
        return false;
    }
}
