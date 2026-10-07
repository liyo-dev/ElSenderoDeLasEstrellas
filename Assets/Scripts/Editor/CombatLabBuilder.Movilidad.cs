using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Zona de saltos y volteretas del LAB (noreste, al norte de los puzles y al este del agua): todo
/// lo del salto y de la voltereta del jugador en un mismo sitio.
/// <list type="bullet">
/// <item>Lanzadores en cadena: suelo → torre de 6 m → torre de 12 m.</item>
/// <item>Caída larga: bajar de cualquiera de las dos torres.</item>
/// <item>Doble salto: pilares de 1,5, 2,6 y 4,2 m.</item>
/// <item>Remate aéreo y desvío con la B: corral de arañas con un lanzador al lado.</item>
/// <item>Derribo y recuperación: placa que lanza al jugador como un golpe de jefe.</item>
/// </list>
/// La geometría va con la del LAB (entra en el NavMesh); lo demás, en su raíz. Se llega por el
/// viaje rápido del panel. Ver INC-651 a INC-656.
public static partial class CombatLabBuilder
{
    private const string RaizMovilidad = "LAB_SALTOS (lanzadores, volteretas y derribo)";
    private static readonly Vector3 CentroSaltos = new Vector3(32f, 0f, 37f);
    private const float LadoSaltos = 36f;

    /// Destino del viaje rápido de la zona.
    private static readonly Vector3 EntradaSaltos = new Vector3(17f, 0.1f, 22f);

    /// Suelo, muros, torres y pilares (con la geometría del LAB, antes de hornear el NavMesh).
    private static void CrearGeometriaDeSaltos(Transform g, Material suelo, Material muro, Material plataforma)
    {
        var zona = new GameObject("Zona de saltos y volteretas").transform;
        zona.SetParent(g);
        Vector3 c = CentroSaltos;
        float m = LadoSaltos * 0.5f;

        CrearCubo("Suelo", zona, c + new Vector3(0f, -0.25f, 0f), new Vector3(LadoSaltos, 0.5f, LadoSaltos), suelo, CapaSuelo);
        CrearCubo("Muro norte", zona, c + new Vector3(0f, 1.5f, m), new Vector3(LadoSaltos, 3f, 0.5f), muro);
        CrearCubo("Muro este", zona, c + new Vector3(m, 1.5f, 0f), new Vector3(0.5f, 3f, LadoSaltos), muro);

        // Lanzadores en cadena: torre de 6 m y, a su lado, torre de 12 m.
        Pilar("Torre de 6 m", zona, new Vector3(20f, 0f, 37f), new Vector2(4f, 4f), 6f, plataforma);
        Pilar("Torre de 12 m", zona, new Vector3(28f, 0f, 37f), new Vector2(4f, 4f), 12f, plataforma);

        // Doble salto: un salto normal llega al primero; el doble salto, al último.
        Pilar("Pilar 1,5 m", zona, new Vector3(22f, 0f, 50f), new Vector2(3f, 3f), 1.5f, plataforma);
        Pilar("Pilar 2,6 m", zona, new Vector3(28f, 0f, 50f), new Vector2(3f, 3f), 2.6f, plataforma);
        Pilar("Pilar 4,2 m", zona, new Vector3(34f, 0f, 50f), new Vector2(3f, 3f), 4.2f, plataforma);
    }

    /// Lanzadores, corral, placa de derribo y rótulos (fuera del NavMesh).
    private static List<string> CrearZonaMovilidad(Scene escena)
    {
        var avisos = new List<string>();
        foreach (var raizExistente in escena.GetRootGameObjects())
            if (raizExistente.name == RaizMovilidad) Object.DestroyImmediate(raizExistente);

        var raiz = new GameObject(RaizMovilidad);
        SceneManager.MoveGameObjectToScene(raiz, escena);
        var r = raiz.transform;

        CrearTexto("Saltos y volteretas", r, new Vector3(17f, 4f, 26f), 0f);

        // ── Lanzadores en cadena ──
        var lanzador = AssetDatabase.LoadAssetAtPath<GameObject>(VolteretaDelJugadorBuilder.RutaLanzador);
        if (lanzador == null)
            avisos.Add("Falta el prefab del lanzador: ejecuta antes «Montar voltereta del jugador y sus usos» y regenera el LAB.");
        else
        {
            // Delante de la torre de 6 m, empujando hacia ella (+Z).
            Lanzador(lanzador, r, "Lanzador 1 (a la torre de 6 m)", new Vector3(20f, 0f, 31.5f), 0f);
            // En lo alto de la torre de 6 m, empujando hacia la de 12 m (+X).
            Lanzador(lanzador, r, "Lanzador 2 (a la torre de 12 m)", new Vector3(20.5f, 6f, 37f), 90f);
            // Junto al corral: sube en vertical para rematar desde arriba.
            Lanzador(lanzador, r, "Lanzador 3 (remate desde arriba)", new Vector3(36f, 0f, 30f), 0f, empuje: 0f);
        }
        CrearTexto("1. Runa: sube a la torre de 6 m", r, new Vector3(20f, 3f, 30f), 0f);
        CrearTexto("2. Runa arriba: a la de 12 m", r, new Vector3(20f, 8f, 37f), 0f);
        CrearTexto("Salta abajo: voltereta de caída larga", r, new Vector3(28f, 14f, 37f), 0f);
        CrearTexto("Doble salto: 1,5 · 2,6 · 4,2 m", r, new Vector3(28f, 6.5f, 50f), 0f);

        // ── Remate aéreo y desvío con la B ──
        var arana = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/Spider1.prefab");
        if (arana != null)
        {
            var corral = new GameObject("Corral de arañas (remate aéreo y B)");
            corral.transform.SetParent(r);
            corral.transform.position = new Vector3(42f, 0f, 30f);
            corral.AddComponent<CorralDeEnemigosDelLab>().Configurar(arana, 3, 4f);
        }
        else avisos.Add("No encuentro Spider1.prefab: no hay arañas para el remate aéreo.");
        CrearTexto("Remate: salta y X, X, X", r, new Vector3(40f, 4f, 24f), 0f);
        CrearTexto("B justo al morder: voltereta atrás", r, new Vector3(40f, 3.2f, 24f), 0f);

        // ── Derribo y recuperación ──
        var rojo = GetMaterial("Lab_Derribo", new Color(0.85f, 0.25f, 0.25f));
        var placa = CrearCubo("Placa de derribo", r, new Vector3(42f, 0.05f, 46f), new Vector3(2.5f, 0.1f, 2.5f), rojo, estatico: false);
        var caja = placa.GetComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.size = new Vector3(1f, 20f, 1f);
        caja.center = new Vector3(0f, 10f, 0f);
        placa.AddComponent<PlacaDeDerriboDelLab>();
        CrearTexto("Placa de derribo", r, new Vector3(42f, 2.6f, 43.5f), 0f);
        CrearTexto("A en el aire: caes de pie", r, new Vector3(42f, 1.9f, 43.5f), 0f);

        CrearTexto("Dúo con voltereta: Will + Estela, LT+RT", r, new Vector3(32f, 2.5f, 21f), 0f);
        return avisos;
    }

    private static void Lanzador(GameObject prefab, Transform padre, string nombre, Vector3 posicion, float giro, float empuje = -1f)
    {
        var lanzador = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
        lanzador.name = nombre;
        lanzador.transform.SetPositionAndRotation(posicion, Quaternion.Euler(0f, giro, 0f));
        if (empuje < 0f) return;
        var so = new SerializedObject(lanzador.GetComponent<LanzadorDeSalto>());
        so.FindProperty("empujeHaciaDelante").floatValue = empuje;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
