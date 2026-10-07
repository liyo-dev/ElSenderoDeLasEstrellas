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

    /// Objeto de la escena que guarda el giro original de cada casa girada: un hijo por casa, llamado
    /// «ruta|GlobalObjectId», con la posición y el giro originales, y dentro un hijo «girada» con la pose
    /// que le dejó el vestido. Con «crear», lo crea si no existe.
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

    private const string NombrePoseGirada = "girada";

    /// Identificador estable de una casa en su escena: sobrevive a renombrarla o cambiarla de grupo.
    private static string IdDe(GameObject go) => GlobalObjectId.GetGlobalObjectIdSlow(go).ToString();

    /// Casa a la que apunta una marca del registro: por su identificador y, si no, por su ruta.
    private static Transform CasaDeLaMarca(Scene escena, Transform marca, out string id)
    {
        int corte = marca.name.LastIndexOf('|');
        id = corte >= 0 ? marca.name.Substring(corte + 1) : "";
        if (GlobalObjectId.TryParse(id, out GlobalObjectId gid) &&
            GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) is GameObject go && go.scene == escena)
            return go.transform;
        return corte > 0 ? BuscarPorRuta(escena, marca.name.Substring(0, corte)) : null;
    }

    /// Identificadores de las casas que están giradas ahora mismo en la escena.
    private static HashSet<string> CasasGiradas(Scene escena)
    {
        var r = new HashSet<string>();
        Transform registro = RegistroCasas(escena, crear: false);
        if (registro == null) return r;
        foreach (Transform marca in registro)
        {
            Transform casa = CasaDeLaMarca(escena, marca, out string id);
            r.Add(casa != null ? IdDe(casa.gameObject) : id);
        }
        return r;
    }

    /// Adornos que el generador pegó a la fachada que creía delantera: se giran con su casa para que no
    /// queden delante de la puerta.
    private static readonly string[] AdornosDeCasa = { "Maceta junto a vivienda" };
    private const string GrupoVidaDeLosPueblos = "Vida de los pueblos — huertas, enseres y claros";
    private const float AlcanceDeAdornos = 2.5f;

    private static void Registrar(Transform registro, Transform t, Vector3 pos, Quaternion rot)
    {
        var marca = new GameObject(RutaJerarquia(t) + "|" + IdDe(t.gameObject)) { tag = "EditorOnly" };
        marca.transform.SetParent(registro, false);
        marca.transform.SetPositionAndRotation(pos, rot);
        var girada = new GameObject(NombrePoseGirada);
        girada.transform.SetParent(marca.transform, false);
        girada.transform.SetPositionAndRotation(t.position, t.rotation);
    }

    private static void GirarCasasHaciaSuCalle(Scene escena, Obra o)
    {
        HashSet<string> yaGiradas = CasasGiradas(escena);
        Transform registro = null;
        int giradas = 0, revertidas = 0, adornos = 0;

        var sueltos = new List<Transform>();
        Transform vida = BuscarGrupo(escena, GrupoVidaDeLosPueblos);
        if (vida != null)
            foreach (Transform t in vida)
                if (System.Array.IndexOf(AdornosDeCasa, t.name) >= 0 && !yaGiradas.Contains(IdDe(t.gameObject))) sueltos.Add(t);

        foreach (string nombreGrupo in GruposConCasasAGirar)
        {
            Transform grupo = BuscarGrupo(escena, nombreGrupo);
            if (grupo == null) { o.Informe.Add($"  · no encuentro el grupo «{nombreGrupo}»: sus casas no se giran."); continue; }
            foreach (Transform casa in grupo)
            {
                GameObject fuente = PrefabUtility.GetCorrespondingObjectFromSource(casa.gameObject);
                if (fuente == null || !LadoDeLaPuerta.TryGetValue(fuente.name, out float lado)) continue;
                if (Mathf.Abs(Mathf.DeltaAngle(lado, 0f)) < 1f) continue;
                string id = IdDe(casa.gameObject);
                if (yaGiradas.Contains(id)) continue;

                Vector3 pos = casa.position;
                Quaternion rot = casa.rotation;
                Bounds antes = LimitesVisibles(casa.gameObject);
                casa.RotateAround(new Vector3(antes.center.x, pos.y, antes.center.z), Vector3.up, -lado);

                Bounds despues = LimitesVisibles(casa.gameObject);
                if (Choca(casa, despues, 0.4f, null))
                {
                    casa.SetPositionAndRotation(pos, rot);
                    revertidas++;
                    o.Informe.Add($"  · «{casa.name}» no se gira: chocaría con algo vecino.");
                    continue;
                }
                if (registro == null) registro = RegistroCasas(escena, crear: true);
                Registrar(registro, casa, pos, rot);
                giradas++;

                var zona = new Rect(antes.min.x - AlcanceDeAdornos, antes.min.z - AlcanceDeAdornos,
                    antes.size.x + 2f * AlcanceDeAdornos, antes.size.z + 2f * AlcanceDeAdornos);
                for (int i = sueltos.Count - 1; i >= 0; i--)
                {
                    Transform a = sueltos[i];
                    if (!zona.Contains(new Vector2(a.position.x, a.position.z))) continue;
                    sueltos.RemoveAt(i);
                    Vector3 pa = a.position;
                    Quaternion ra = a.rotation;
                    a.RotateAround(new Vector3(antes.center.x, pa.y, antes.center.z), Vector3.up, -lado);
                    if (Choca(a, LimitesVisibles(a.gameObject), 0f, casa))
                    {
                        a.SetPositionAndRotation(pa, ra);
                        o.Informe.Add($"  · «{a.name}» junto a «{casa.name}» no se gira con ella: chocaría con algo.");
                        continue;
                    }
                    Registrar(registro, a, pa, ra);
                    adornos++;
                }
            }
        }
        o.Informe.Add($"Casas: {giradas} giradas para que la puerta dé a su calle ({adornos} macetas del generador giradas con ellas), {revertidas} sin girar por espacio.");
    }

    /// Si algo de la escena (salvo el propio objeto y «ignorar») ocupa su caja, reducida «reducir» metros por lado.
    private static bool Choca(Transform t, Bounds b, float reducir, Transform ignorar)
    {
        Physics.SyncTransforms();
        var buffer = new Collider[64];
        Vector3 medio = new Vector3(Mathf.Max(0.05f, b.extents.x - reducir), Mathf.Max(0.3f, b.extents.y * 0.6f), Mathf.Max(0.05f, b.extents.z - reducir));
        int n = Physics.OverlapBoxNonAlloc(b.center + Vector3.up * 0.6f, medio, buffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = buffer[i];
            if (Ignorable(c) || c.transform.IsChildOf(t) || (ignorar != null && c.transform.IsChildOf(ignorar))) continue;
            return true;
        }
        return false;
    }

    /// Devuelve a su sitio las casas que giró el vestido. Si alguien ha movido una después, solo se le
    /// deshace el giro (se respeta dónde la dejó). Se conservan las marcas de lo que no aparece y de los
    /// adornos movidos a mano.
    private static void ReponerCasas(Scene escena, List<string> informe)
    {
        Transform registro = RegistroCasas(escena, crear: false);
        if (registro == null) return;
        int n = 0, movidas = 0, perdidas = 0;
        var hechas = new List<GameObject>();
        foreach (Transform marca in registro)
        {
            Transform casa = CasaDeLaMarca(escena, marca, out _);
            if (casa == null) { perdidas++; continue; }
            Transform girada = marca.Find(NombrePoseGirada);
            bool intacta = girada == null ||
                           (Vector3.Distance(casa.position, girada.position) < 0.01f && Quaternion.Angle(casa.rotation, girada.rotation) < 0.5f);
            if (intacta) casa.SetPositionAndRotation(marca.position, marca.rotation);
            else
            {
                GameObject fuente = PrefabUtility.GetCorrespondingObjectFromSource(casa.gameObject);
                if (fuente == null || !LadoDeLaPuerta.TryGetValue(fuente.name, out float lado))
                {
                    // Un adorno movido a mano: se queda donde lo dejaron y conserva su marca, para que el
                    // siguiente vestido no lo vuelva a girar.
                    informe.Add($"  · «{casa.name}» se movió a mano después del vestido: se deja donde está.");
                    continue;
                }
                // Solo se deshace el giro si la casa aún lo lleva: un Ctrl+Z de un cambio anterior al vestido
                // (que no usa Undo) puede haberla devuelto ya a su giro de antes.
                if (Quaternion.Angle(casa.rotation, girada.rotation) < Quaternion.Angle(casa.rotation, marca.rotation))
                {
                    Bounds b = LimitesVisibles(casa.gameObject);
                    casa.RotateAround(new Vector3(b.center.x, casa.position.y, b.center.z), Vector3.up, lado);
                }
                movidas++;
                informe.Add($"  · «{casa.name}» se movió a mano después del vestido: se le deshace solo el giro.");
            }
            hechas.Add(marca.gameObject);
            n++;
        }
        foreach (GameObject g in hechas) Object.DestroyImmediate(g);
        if (registro.childCount == 0) Object.DestroyImmediate(registro.gameObject);
        informe.Add($"Casas: {n} devueltas a su giro original" + (movidas > 0 ? $" ({movidas} movidas a mano)" : "") +
                    (perdidas > 0 ? $"; {perdidas} no aparecen en la escena y su marca se conserva en «{NombreRegistroCasas}»." : "."));
    }
}
