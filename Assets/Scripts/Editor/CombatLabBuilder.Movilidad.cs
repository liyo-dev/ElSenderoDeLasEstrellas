using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Zona de saltos y volteretas del LAB (noreste, al norte de los puzles y al este del agua): todo
/// lo del salto y de la voltereta del jugador en un mismo sitio.
/// <list type="bullet">
/// <item>Lanzadores en cadena: suelo → torre de 6 m → torre de 12 m.</item>
/// <item>Entrada al vuelo: doble salto y A otra vez (impulso y a volar).</item>
/// <item>Doble salto: pilares de 1,5, 2,6 y 4,2 m.</item>
/// <item>Remate aéreo: lanzador vertical (los enemigos para practicarlo están en la zona de arañas).</item>
/// <item>Derribo y recuperación: placa que lanza al jugador como un golpe de jefe.</item>
/// <item>Devolver con la B y aviso de combate: lanzadera que dispara las bolas del Demonio.</item>
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
        Pilar("Torre de 12 m", zona, new Vector3(25.5f, 0f, 37f), new Vector2(4f, 4f), 12f, plataforma);

        // Doble salto: un salto normal llega al primero; el doble salto, al último.
        Pilar("Pilar 1,5 m", zona, new Vector3(22f, 0f, 50f), new Vector2(3f, 3f), 1.5f, plataforma);
        Pilar("Pilar 2,6 m", zona, new Vector3(28f, 0f, 50f), new Vector2(3f, 3f), 2.6f, plataforma);
        Pilar("Pilar 4,2 m", zona, new Vector3(34f, 0f, 50f), new Vector2(3f, 3f), 4.2f, plataforma);
    }

    /// Lanzadores, placa de derribo, lanzadera y rótulos (fuera del NavMesh).
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
            // Sube en vertical para rematar desde arriba.
            Lanzador(lanzador, r, "Lanzador 3 (remate desde arriba)", new Vector3(36f, 0f, 30f), 0f, empuje: 0f);
        }
        CrearTexto("1. Runa: sube a la torre de 6 m", r, new Vector3(20f, 3f, 30f), 0f);
        CrearTexto("2. Runa arriba: a la de 12 m", r, new Vector3(20f, 8f, 37f), 0f);
        CrearTexto("Doble salto y A otra vez: impulso y a volar", r, new Vector3(25.5f, 14f, 37f), 0f);
        CrearTexto("Doble salto: 1,5 · 2,6 · 4,2 m", r, new Vector3(28f, 6.5f, 50f), 0f);

        // ── Remate aéreo ──
        CrearTexto("Remate: salta y X, X, X", r, new Vector3(40f, 4f, 24f), 0f);

        // ── Derribo y recuperación ──
        var rojo = GetMaterial("Lab_Derribo", new Color(0.85f, 0.25f, 0.25f));
        var placa = CrearCubo("Placa de derribo", r, new Vector3(32f, 0.05f, 41f), new Vector3(2.5f, 0.1f, 2.5f), rojo, estatico: false);
        var caja = placa.GetComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.size = new Vector3(1f, 20f, 1f);
        caja.center = new Vector3(0f, 10f, 0f);
        placa.AddComponent<PlacaDeDerriboDelLab>();
        CrearTexto("Placa de derribo", r, new Vector3(32f, 2.6f, 43.5f), 0f);
        CrearTexto("A en el aire: caes de pie", r, new Vector3(32f, 1.9f, 43.5f), 0f);

        // ── Devolver con la B (y aviso de combate) ──
        if (CrearLanzadera(r, new Vector3(47f, 1.5f, 53f), 225f, rojo, cadencia: 2.5f, alcance: 7f) != null)
        {
            CrearMarcador("Puesto de la lanzadera", r, new Vector3(43f, 0.02f, 50f), GetMaterial("Lab_Marcadores", new Color(0.20f, 0.48f, 0.70f)));
            CrearTexto("Devuelve sus bolas con B", r, new Vector3(43f, 3f, 48f), 0f);
        }
        else avisos.Add("No encuentro el proyectil del Demonio (Demon.prefab → ImpDemonAI.projectilePrefab): no hay lanzadera.");

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

    /// Lanzadera de bolas del Demonio sobre un pedestal (para devolverlas con la B). 'posicion' es la
    /// de la lanzadera (encima del pedestal). 'boca': desde dónde salen las bolas (null = un metro
    /// por encima). Devuelve null si no se encuentra el proyectil del Demonio.
    private static LanzaderaDelLab CrearLanzadera(Transform padre, Vector3 posicion, float giro, Material pedestal,
                                                  float cadencia, float alcance, Vector3? bocaLocal = null)
    {
        var demonio = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/Demon.prefab");
        var ia = demonio != null ? demonio.GetComponentInChildren<ImpDemonAI>(true) : null;
        var bola = ia != null ? new SerializedObject(ia).FindProperty("projectilePrefab")?.objectReferenceValue as GameObject : null;
        if (bola == null) return null;

        CrearCubo("Pedestal de la lanzadera", padre, posicion - Vector3.up * 0.75f, new Vector3(1f, 1.5f, 1f), pedestal);
        var lanzadera = new GameObject("Lanzadera (bolas del Demonio)");
        lanzadera.transform.SetParent(padre);
        lanzadera.transform.SetPositionAndRotation(posicion, Quaternion.Euler(0f, giro, 0f));
        var componente = lanzadera.AddComponent<LanzaderaDelLab>();
        componente.Configurar(bola);
        var carga = lanzadera.AddComponent<CargaDeDisparo>();
        var cargaSerializada = new SerializedObject(carga);
        cargaSerializada.FindProperty("segundosDeCarga").floatValue = 0.6f;
        cargaSerializada.ApplyModifiedPropertiesWithoutUndo();
        var so = new SerializedObject(componente);
        so.FindProperty("cadencia").floatValue = cadencia;
        so.FindProperty("alcance").floatValue = alcance;
        if (bocaLocal.HasValue)
        {
            var boca = new GameObject("Boca").transform;
            boca.SetParent(lanzadera.transform, false);
            boca.localPosition = bocaLocal.Value;
            so.FindProperty("boca").objectReferenceValue = boca;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return componente;
    }
}
