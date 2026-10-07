using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Las casas que puso el generador del mapa dan la espalda a su calle: el generador suponía que la fachada
/// de los BuildingAT es su +Z local y en casi todos la puerta está en otro lado. Aquí se giran sobre el
/// centro de su huella para que la puerta mire a donde el generador quería que mirase la fachada. El giro y
/// la posición originales se guardan en la propia escena (un objeto EditorOnly bajo WORLD con un hijo por
/// casa), así se guardan o se descartan junto con las casas; «Quitar el vestido» (y cada nueva ejecución,
/// antes de girar) los repone.
public static partial class VestidoDelMundo
{
    private const string NombreRegistroCasas = "Vestido del mundo — giro original de las casas (no tocar)";

    /// Lado de la puerta a ras de suelo de cada prefab, en grados respecto al +Z local (medido en sus hijos Door*).
    private static readonly Dictionary<string, float> LadoDeLaPuerta = new()
    {
        { "BuildingAT01", 180f },
        { "BuildingAT03", 90f },
        { "BuildingAT07", 90f },
        { "BuildingAT10", 180f },
        { "BuildingAT12", 180f },
        { "BuildingAT17", 180f },
        { "BuildingAT53", 180f },
    };

    /// Grupos de WORLD cuyas casas se giran (las de Will, granjas e islas no se tocan).
    private static readonly string[] GruposConCasasAGirar =
    {
        "Reino — barrio de montaña y explanada real",
        "Pueblo pesquero — calles y muelles",
        "Pueblo vecino — terraza sobre la playa",
    };

    private static Transform BuscarGrupo(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            if (raiz.name != NombrePadre) continue;
            Transform t = raiz.transform.Find(nombre);
            if (t != null) return t;
        }
        return null;
    }

    private static string RutaJerarquia(Transform t)
    {
        var sb = new StringBuilder(t.name);
        for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }

    private static Transform BuscarPorRuta(Scene escena, string ruta)
    {
        string[] partes = ruta.Split('/');
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            if (raiz.name != partes[0]) continue;
            Transform t = raiz.transform;
            for (int i = 1; i < partes.Length && t != null; i++) t = t.Find(partes[i]);
            if (t != null) return t;
        }
        return null;
    }

    /// Objeto de la escena que guarda el giro original de cada casa girada (hijo = ruta de la casa, con su
    /// posición y giro originales). Con «crear», lo crea si no existe.
    private static Transform RegistroCasas(Scene escena, bool crear)
    {
        Transform padre = null;
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            if (raiz.name == NombreRegistroCasas) return raiz.transform;
            if (raiz.name != NombrePadre) continue;
            padre = raiz.transform;
            Transform t = padre.Find(NombreRegistroCasas);
            if (t != null) return t;
        }
        if (!crear) return null;
        var g = new GameObject(NombreRegistroCasas) { tag = "EditorOnly" };
        SceneManager.MoveGameObjectToScene(g, escena);
        if (padre != null) g.transform.SetParent(padre, false);
        return g.transform;
    }

    /// Rutas de las casas que están giradas ahora mismo en la escena.
    private static HashSet<string> CasasGiradas(Scene escena)
    {
        var r = new HashSet<string>();
        Transform registro = RegistroCasas(escena, crear: false);
        if (registro != null)
            foreach (Transform t in registro) r.Add(t.name);
        return r;
    }

    private static void GirarCasasHaciaSuCalle(Scene escena, Obra o)
    {
        HashSet<string> yaGiradas = CasasGiradas(escena);
        Transform registro = null;
        int giradas = 0, revertidas = 0;
        foreach (string nombreGrupo in GruposConCasasAGirar)
        {
            Transform grupo = BuscarGrupo(escena, nombreGrupo);
            if (grupo == null) { o.Informe.Add($"  · no encuentro el grupo «{nombreGrupo}»: sus casas no se giran."); continue; }
            var nombres = new Dictionary<string, int>();
            foreach (Transform hijo in grupo) nombres[hijo.name] = nombres.TryGetValue(hijo.name, out int k) ? k + 1 : 1;
            foreach (Transform casa in grupo)
            {
                GameObject fuente = PrefabUtility.GetCorrespondingObjectFromSource(casa.gameObject);
                if (fuente == null || !LadoDeLaPuerta.TryGetValue(fuente.name, out float lado)) continue;
                if (Mathf.Abs(Mathf.DeltaAngle(lado, 0f)) < 1f) continue;
                // La reposición busca la casa por su ruta: con el nombre repetido podría devolver otra.
                if (nombres[casa.name] > 1) { o.Informe.Add($"  · «{casa.name}» no se gira: hay otra con el mismo nombre en su grupo."); continue; }
                string ruta = RutaJerarquia(casa);
                if (yaGiradas.Contains(ruta)) continue;

                Vector3 pos = casa.position;
                Quaternion rot = casa.rotation;
                Bounds antes = LimitesVisibles(casa.gameObject);
                casa.RotateAround(new Vector3(antes.center.x, pos.y, antes.center.z), Vector3.up, -lado);

                Bounds despues = LimitesVisibles(casa.gameObject);
                if (ChocaConOtraCasa(casa, despues))
                {
                    casa.SetPositionAndRotation(pos, rot);
                    revertidas++;
                    o.Informe.Add($"  · «{casa.name}» no se gira: chocaría con algo vecino.");
                    continue;
                }
                if (registro == null) registro = RegistroCasas(escena, crear: true);
                var marca = new GameObject(ruta) { tag = "EditorOnly" };
                marca.transform.SetParent(registro, false);
                marca.transform.SetPositionAndRotation(pos, rot);
                giradas++;
            }
        }
        o.Informe.Add($"Casas: {giradas} giradas para que la puerta dé a su calle, {revertidas} sin girar por espacio.");
    }

    private static bool ChocaConOtraCasa(Transform casa, Bounds b)
    {
        Physics.SyncTransforms();
        var buffer = new Collider[64];
        Vector3 medio = new Vector3(b.extents.x - 0.4f, Mathf.Max(0.3f, b.extents.y * 0.6f), b.extents.z - 0.4f);
        int n = Physics.OverlapBoxNonAlloc(b.center + Vector3.up * 0.6f, medio, buffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = buffer[i];
            if (Ignorable(c) || c.transform.IsChildOf(casa)) continue;
            return true;
        }
        return false;
    }

    /// Devuelve a su sitio las casas que giró la última ejecución y borra el registro.
    private static void ReponerCasas(Scene escena, List<string> informe)
    {
        Transform registro = RegistroCasas(escena, crear: false);
        if (registro == null) return;
        int n = 0, perdidas = 0;
        foreach (Transform marca in registro)
        {
            Transform casa = BuscarPorRuta(escena, marca.name);
            if (casa == null) { perdidas++; continue; }
            casa.SetPositionAndRotation(marca.position, marca.rotation);
            n++;
        }
        Object.DestroyImmediate(registro.gameObject);
        informe.Add($"Casas: {n} devueltas a su giro original" + (perdidas > 0 ? $" ({perdidas} no encontradas)." : "."));
    }
}
