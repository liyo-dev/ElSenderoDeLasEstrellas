using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Objetos de misión que el montaje pone en MainWorld: un prefab tal cual (con sus Interactable,
/// PickupObject, marcador del minimapa…), en su sitio y apoyado en el suelo que haya al montar (el
/// vestido del mundo cambia). Y la red de seguridad de «Capítulos (generado)»: lo que alguien puso a
/// mano dentro de la raíz no se borra en silencio al rehacerla, se saca a «Puesto a mano (rescatado
/// del montaje)» y se avisa.
///
/// Antecedente: la caja de Eldran del Cap. 1 (Goods_Interactable, misión ELDRAN_MISSION2, evento
/// CAP1_CAJA_ELDRAN) estaba puesta a mano dentro de la raíz; el montaje de INC-1287 la borró y el
/// Cap. 1 se quedaba parado en «23.- Recoge la caja» (INC-1307).
/// </summary>
public static partial class MontajeDeLosCapitulos
{
    /// <summary>Un objeto de misión: prefab entero, sin quitarle nada. Con <c>Tramo</c>, solo encendido en él.</summary>
    private sealed class ObjetoDeMision { public string Cap, Nombre, Prefab; public Vector3 Pos; public float Rumbo; public TramoDeHistoria Tramo; }

    public const string NombreRescatado = "Puesto a mano (rescatado del montaje)";
    private const string Cap1 = "Cap1";

    /// <summary>La tabla. Y en NaN: se apoya en el suelo al montar.</summary>
    private static List<ObjetoDeMision> ObjetosDeMision() => new()
    {
        // «21.- HAY QUE BUSCAR LA CAJA BAJO UN ARBOL» → «23.- Recoge la caja» (Cap1.asset). Sitio del 18 nov (a8eef31cb).
        new ObjetoDeMision { Cap = Cap1, Nombre = "Goods_Interactable", Prefab = "Assets/Prefabs/Cap1/Goods_Interactable.prefab",
                             Pos = new Vector3(89.27f, float.NaN, 6.84f), Rumbo = 0f },
    };

    private static void PonerObjetosDeMision(Transform raiz, StringBuilder informe)
    {
        var tabla = ObjetosDeMision();
        foreach (var grupo in tabla.GroupBy(o => o.Cap))
        {
            var padre = Hijo(raiz, grupo.Key + " · objetos de misión");
            foreach (var o in grupo)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(o.Prefab);
                if (prefab == null) { informe.AppendLine($"AVISO: objeto de misión «{o.Nombre}»: falta {o.Prefab}."); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, raiz.gameObject.scene);
                go.name = o.Nombre;
                var pos = AlSuelo(o.Pos);
                if (o.Tramo != null)
                {
                    var tramo = Hijo(padre, o.Nombre + " (tramo)");
                    go.transform.SetParent(tramo, false);
                    go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, o.Rumbo, 0f));
                    EnTramo(tramo, o.Tramo, go);
                }
                else
                {
                    go.transform.SetParent(padre, false);
                    go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, o.Rumbo, 0f));
                }
                informe.AppendLine($"Objeto de misión {o.Cap}: «{o.Nombre}» en ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}).");
            }
        }
    }

    /// <summary>
    /// Compara la raíz anterior con la recién montada: lo que estaba en la anterior y no tiene sitio
    /// (misma ruta) en la nueva no lo creó este montaje; se mueve, con todo lo que lleva, a
    /// <see cref="NombreRescatado"/> (en la raíz de la escena, conservando su posición en el mundo).
    /// </summary>
    private static void RescatarLoPuestoAMano(Scene escena, IEnumerable<GameObject> anteriores, Transform nueva, StringBuilder informe)
    {
        var rutasNuevas = new Dictionary<string, int>();
        foreach (var t in nueva.GetComponentsInChildren<Transform>(true))
            if (t != nueva) { var r = RutaBajo(t, nueva); rutasNuevas[r] = rutasNuevas.GetValueOrDefault(r) + 1; }

        var rescatar = new List<(Transform t, string ruta)>();
        foreach (var vieja in anteriores)
        {
            var vistas = new Dictionary<string, int>();
            var pila = new Stack<Transform>();
            for (int i = vieja.transform.childCount - 1; i >= 0; i--) pila.Push(vieja.transform.GetChild(i));
            while (pila.Count > 0)
            {
                var t = pila.Pop();
                var r = RutaBajo(t, vieja.transform);
                int n = vistas[r] = vistas.GetValueOrDefault(r) + 1;
                if (n > rutasNuevas.GetValueOrDefault(r)) { rescatar.Add((t, r)); continue; }
                for (int i = t.childCount - 1; i >= 0; i--) pila.Push(t.GetChild(i));
            }
        }
        if (rescatar.Count == 0) return;

        var grupo = escena.GetRootGameObjects().FirstOrDefault(g => g.name == NombreRescatado);
        if (grupo == null) { grupo = new GameObject(NombreRescatado); SceneManager.MoveGameObjectToScene(grupo, escena); }
        foreach (var (t, ruta) in rescatar)
        {
            t.SetParent(grupo.transform, true);
            string aviso = $"AVISO: «{NombreGenerado}/{ruta}» no lo crea el montaje (puesto a mano): no se borra, pasa a «{NombreRescatado}». " +
                           "Si hace falta, añádelo a las tablas del montaje (p. ej. objetos de misión) y bórralo de ahí.";
            informe.AppendLine(aviso);
            Debug.LogWarning("[MontajeDeLosCapitulos] " + aviso, t.gameObject);
        }
    }

    private static string RutaBajo(Transform t, Transform raiz)
    {
        var partes = new List<string>();
        for (var x = t; x != null && x != raiz; x = x.parent) partes.Add(x.name);
        partes.Reverse();
        return string.Join("/", partes);
    }
}
