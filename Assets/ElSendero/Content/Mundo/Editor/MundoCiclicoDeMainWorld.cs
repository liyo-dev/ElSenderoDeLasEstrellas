using System.Linq;
using Kits.WorldWrap;
using Kits.WorldWrap.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// El mundo cíclico de MainWorld con el kit WorldWrap:
/// - el plano «Mar» lleva `EndlessSea` y sigue a la cámara, así su borde no se ve nunca;
/// - «Techo de vuelo» (`AltitudeCeiling`) queda por encima del terreno más alto, con margen;
/// - «Mundo cíclico» (`WorldWrapper` + `MundoCiclicoDelJuego`) cose los bordes: un cuadrado de
///   `LadoDelMundo` centrado en el terreno; al salir por un lado se entra por el contrario.
/// Avisa en el registro (con la revisión de costura del kit) de lo activo que quede fuera del mundo o
/// a menos de la distancia de vista del borde (se vería aparecer al cruzar).
/// Se puede volver a ejecutar: solo ajusta componentes y medidas. Ver INC-1197
public static class MundoCiclicoDeMainWorld
{
    private const string NombreDelMar = "Mar";
    private const float LadoDelMar = 3000f;
    private const string NombreDelTecho = "Techo de vuelo";
    private const float MargenSobreLaCima = 60f;
    private const float FranjaSuaveDelTecho = 30f;
    private const string NombreDelMundo = "Mundo cíclico";
    private const float LadoDelMundo = 3000f;
    // Lo que deja ver la niebla más fina del ciclo (DayNightCycle.visibilidadMaxima).
    private const float DistanciaDeVista = 900f;

    [MenuItem("El Sendero/Escenario/MainWorld: mundo cíclico (mar sin fin, techo de vuelo y vuelta) (INC-1197)")]
    public static void Montar()
    {
        var escena = SceneManager.GetSceneByName("MainWorld");
        if (!escena.IsValid() || !escena.isLoaded)
        {
            Debug.LogWarning("[Mundo cíclico] Abre MainWorld antes de montarlo.");
            return;
        }

        var mar = escena.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true))
            .FirstOrDefault(r => r.name == NombreDelMar && r.GetComponent<MeshFilter>() != null);
        if (mar == null)
        {
            Debug.LogWarning("[Mundo cíclico] MainWorld no tiene el plano «Mar».");
            return;
        }

        MontarMar(mar);
        Transform padre = mar.transform.parent;
        float techo = MontarTecho(padre, escena);
        var mundo = MontarMundo(padre, escena);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Informar($"[Mundo cíclico] MainWorld: mar de {LadoDelMar:0} m que sigue a la cámara, techo de vuelo a {techo:0} m, " +
                  $"mundo de {mundo.Size.x:0} × {mundo.Size.y:0} m con centro en ({mundo.Center.x:0}, {mundo.Center.y:0}).");
        var costura = WorldWrapSeamCheck.Run(mundo, DistanciaDeVista, new[] { mar.gameObject });
        Informar(WorldWrapSeamCheck.Describe(costura, DistanciaDeVista));
    }

    private static void MontarMar(MeshRenderer mar)
    {
        var sea = mar.GetComponent<EndlessSea>();
        if (sea == null) sea = Undo.AddComponent<EndlessSea>(mar.gameObject);
        var so = new SerializedObject(sea);
        so.FindProperty("size").floatValue = LadoDelMar;
        so.ApplyModifiedPropertiesWithoutUndo();
        Undo.RecordObject(mar.transform, "Mar sin fin");
        sea.ApplySize();

        // Un objeto que se mueve no puede ir en lotes estáticos ni en la oclusión horneada.
        var flags = GameObjectUtility.GetStaticEditorFlags(mar.gameObject);
        flags &= ~(StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        GameObjectUtility.SetStaticEditorFlags(mar.gameObject, flags);
        EditorUtility.SetDirty(sea);
        EditorUtility.SetDirty(mar.transform);
    }

    /// Pone el techo de vuelo a la altura de la cima más alta de los terrenos de la escena más el margen.
    private static float MontarTecho(Transform padre, Scene escena)
    {
        float cima = 0f;
        foreach (var terreno in TerrenosDe(escena))
            cima = Mathf.Max(cima, terreno.transform.position.y + terreno.terrainData.bounds.max.y);
        float altura = cima + MargenSobreLaCima;

        var techo = Asegurar<AltitudeCeiling>(padre, NombreDelTecho);
        Undo.RecordObject(techo, "Techo de vuelo");
        techo.Height = altura;
        techo.SoftBand = FranjaSuaveDelTecho;
        EditorUtility.SetDirty(techo);
        return altura;
    }

    /// Pone el mundo cíclico centrado en los terrenos de la escena.
    private static WorldWrapper MontarMundo(Transform padre, Scene escena)
    {
        var mundo = Asegurar<WorldWrapper>(padre, NombreDelMundo);
        if (mundo.GetComponent<MundoCiclicoDelJuego>() == null)
            Undo.AddComponent<MundoCiclicoDelJuego>(mundo.gameObject);

        Bounds? caja = null;
        foreach (var terreno in TerrenosDe(escena))
        {
            var b = terreno.terrainData.bounds;
            b.center += terreno.transform.position;
            if (caja == null) caja = b;
            else { var c = caja.Value; c.Encapsulate(b); caja = c; }
        }
        Vector2 centro = caja != null ? new Vector2(caja.Value.center.x, caja.Value.center.z) : Vector2.zero;

        Undo.RecordObject(mundo, "Mundo cíclico");
        mundo.Center = centro;
        mundo.Size = new Vector2(LadoDelMundo, LadoDelMundo);
        EditorUtility.SetDirty(mundo);
        return mundo;
    }

    /// Al registro y, si se ha lanzado desde el buzón de órdenes, también a su registro.
    private static void Informar(string texto)
    {
        Debug.Log(texto);
        if (OrdenesDelEditor.EnOrden) OrdenesDelEditor.Avisar("Mundo cíclico", texto);
    }

    private static System.Collections.Generic.IEnumerable<Terrain> TerrenosDe(Scene escena)
        => escena.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<Terrain>(true))
            .Where(t => t.terrainData != null);

    private static T Asegurar<T>(Transform padre, string nombre) where T : Component
    {
        Transform t = padre != null ? padre.Find(nombre) : null;
        if (t == null)
        {
            var go = new GameObject(nombre);
            Undo.RegisterCreatedObjectUndo(go, nombre);
            if (padre != null) go.transform.SetParent(padre, false);
            t = go.transform;
        }
        var c = t.GetComponent<T>();
        if (c == null) c = Undo.AddComponent<T>(t.gameObject);
        return c;
    }
}
