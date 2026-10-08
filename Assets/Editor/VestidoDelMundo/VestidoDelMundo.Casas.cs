using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Lo que el vestido cambia de lo que ya estaba en la escena, siempre con registro para deshacerlo:
///
/// · Giro de casas. Las casas que puso el generador del mapa dan la espalda a su calle: el generador suponía
///   que la fachada de los BuildingAT es su +Z local y en casi todos la puerta está en otro lado. Aquí se
///   giran sobre el centro de su huella para que la puerta mire a donde el generador quería que mirase la
///   fachada.
/// · Retirada. Lo que estorba a lo nuevo (muralla vieja, casas de aldea en la capital, lo que queda bajo la
///   laguna…) no se borra: se deja inactivo y con la etiqueta EditorOnly, así no llega a la build.
///
/// El estado original se guarda en la propia escena (un objeto EditorOnly bajo WORLD por cada cosa, con un
/// hijo por objeto), así se guarda o se descarta junto con lo cambiado; «Quitar el vestido» (y cada nueva
/// ejecución, antes de empezar) lo repone.
public static partial class VestidoDelMundo
{
    private const string NombreRegistroCasas = "Vestido del mundo — giro original de las casas (no tocar)";
    private const string NombreRegistroRetirados = "Vestido del mundo — retirado (no tocar)";

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

    /// Objeto EditorOnly de la escena (bajo WORLD o, si no hay WORLD, en la raíz) que guarda un registro del
    /// vestido. Con «crear», lo crea si no existe.
    ///
    /// · Giro de las casas: un hijo por casa, llamado «ruta|GlobalObjectId», con la posición y el giro
    ///   originales, y dentro un hijo «girada» con la pose que le dejó el vestido.
    /// · Retirados: un hijo por objeto, «ruta|GlobalObjectId», y dentro «estado|activo|etiqueta» con su
    ///   estado de antes.
    private static Transform Registro(Scene escena, string nombre, bool crear)
    {
        Transform padre = null;
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            if (raiz.name == nombre) return raiz.transform;
            if (raiz.name != NombrePadre) continue;
            padre = raiz.transform;
            Transform t = padre.Find(nombre);
            if (t != null) return t;
        }
        if (!crear) return null;
        var g = new GameObject(nombre) { tag = "EditorOnly" };
        SceneManager.MoveGameObjectToScene(g, escena);
        if (padre != null) g.transform.SetParent(padre, false);
        return g.transform;
    }

    private static Transform RegistroCasas(Scene escena, bool crear) => Registro(escena, NombreRegistroCasas, crear);

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

    private static bool PareceRetirado(GameObject go) => !go.activeSelf && go.CompareTag("EditorOnly");

    /// Respaldo cuando el identificador de una marca de retirado no se resuelve: el primer objeto con esa
    /// ruta que siga retirado. Hay grupos con muchos hijos del mismo nombre (lienzos de muralla), así no se
    /// toca uno que no estaba retirado.
    private static Transform RetiradoPorRuta(Scene escena, string ruta)
    {
        int corte = ruta.LastIndexOf('/');
        string nombre = ruta.Substring(corte + 1);
        if (corte < 0)
        {
            foreach (GameObject raiz in escena.GetRootGameObjects())
                if (raiz.name == nombre && PareceRetirado(raiz)) return raiz.transform;
            return null;
        }
        Transform padre = BuscarPorRuta(escena, ruta.Substring(0, corte));
        if (padre == null) return null;
        foreach (Transform t in padre)
            if (t.name == nombre && PareceRetirado(t.gameObject)) return t;
        return null;
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
        // Lo retirado no se gira: está inactivo y fuera de la build.
        HashSet<string> retirados = o.Retirados ??= IdsRetirados(escena);
        Transform registro = null;
        int giradas = 0, revertidas = 0, adornos = 0;

        var sueltos = new List<Transform>();
        Transform vida = BuscarGrupo(escena, GrupoVidaDeLosPueblos);
        if (vida != null)
            foreach (Transform t in vida)
            {
                if (System.Array.IndexOf(AdornosDeCasa, t.name) < 0) continue;
                string id = IdDe(t.gameObject);
                if (!yaGiradas.Contains(id) && !retirados.Contains(id)) sueltos.Add(t);
            }

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
                if (yaGiradas.Contains(id) || retirados.Contains(id)) continue;

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

    // ── Retirada de lo que estorba ───────────────────────────────────────────────────────────

    private const string PrefijoEstado = "estado|";

    /// Retira de la escena algo que estorba a lo nuevo sin borrarlo: lo deja inactivo y con la etiqueta
    /// EditorOnly (no llega a la build) y apunta su estado de antes en el registro de retirados. Si ya está
    /// retirado (o es parte de lo generado, que se rehace en cada ejecución), no hace nada. En el informe
    /// queda una línea por motivo con el recuento.
    private static void Retirar(Scene escena, Transform t, string motivo, Obra o)
    {
        if (t == null || t.IsChildOf(o.Raiz)) return;
        string id = IdDe(t.gameObject);
        o.Retirados ??= IdsRetirados(escena);
        if (!o.Retirados.Add(id)) return;

        Transform registro = Registro(escena, NombreRegistroRetirados, crear: true);
        var marca = new GameObject(RutaJerarquia(t) + "|" + id) { tag = "EditorOnly" };
        marca.transform.SetParent(registro, false);
        var estado = new GameObject(PrefijoEstado + t.gameObject.activeSelf + "|" + t.gameObject.tag);
        estado.transform.SetParent(marca.transform, false);
        t.gameObject.SetActive(false);
        t.gameObject.tag = "EditorOnly";
        GuardarOverrides(t.gameObject);

        if (o.RetiradosPorMotivo.TryGetValue(motivo, out (int linea, int cuenta) r))
        {
            o.RetiradosPorMotivo[motivo] = (r.linea, r.cuenta + 1);
            o.Informe[r.linea] = LineaDeRetirados(motivo, r.cuenta + 1);
        }
        else
        {
            o.RetiradosPorMotivo[motivo] = (o.Informe.Count, 1);
            o.Informe.Add(LineaDeRetirados(motivo, 1));
        }
    }

    private static string LineaDeRetirados(string motivo, int cuenta) => $"Retirados (inactivos, fuera de la build): {cuenta} × {motivo}";

    /// Identificadores (GlobalObjectId) de lo que está retirado ahora mismo en la escena.
    private static HashSet<string> IdsRetirados(Scene escena)
    {
        var r = new HashSet<string>();
        Transform registro = Registro(escena, NombreRegistroRetirados, crear: false);
        if (registro == null) return r;
        foreach (Transform marca in registro)
        {
            int corte = marca.name.LastIndexOf('|');
            if (corte >= 0) r.Add(marca.name.Substring(corte + 1));
        }
        return r;
    }

    /// Devuelve lo retirado a su estado de antes (activo y etiqueta). Conserva la marca de lo que no aparece
    /// en la escena y borra el registro si queda vacío.
    private static void ReponerRetirados(Scene escena, List<string> informe)
    {
        Transform registro = Registro(escena, NombreRegistroRetirados, crear: false);
        if (registro == null) return;
        var marcas = new List<Transform>();
        foreach (Transform marca in registro) marcas.Add(marca);

        // Los identificadores se resuelven todos de una vez: uno a uno es lento en una escena tan grande.
        var objetos = new Object[marcas.Count];
        var validos = new List<int>();
        var ids = new List<GlobalObjectId>();
        for (int i = 0; i < marcas.Count; i++)
        {
            int corte = marcas[i].name.LastIndexOf('|');
            if (corte < 0 || !GlobalObjectId.TryParse(marcas[i].name.Substring(corte + 1), out GlobalObjectId gid)) continue;
            validos.Add(i);
            ids.Add(gid);
        }
        if (ids.Count > 0)
        {
            var resueltos = new Object[ids.Count];
            GlobalObjectId.GlobalObjectIdentifiersToObjectsSlow(ids.ToArray(), resueltos);
            for (int j = 0; j < validos.Count; j++) objetos[validos[j]] = resueltos[j];
        }

        int n = 0, perdidas = 0;
        for (int i = 0; i < marcas.Count; i++)
        {
            Transform marca = marcas[i];
            int corte = marca.name.LastIndexOf('|');
            Transform t = objetos[i] is GameObject go && go.scene == escena ? go.transform
                : corte > 0 ? RetiradoPorRuta(escena, marca.name.Substring(0, corte)) : null;
            if (t == null) { perdidas++; continue; }
            ReponerEstado(t.gameObject, marca, informe);
            Object.DestroyImmediate(marca.gameObject);
            n++;
        }
        if (registro.childCount == 0) Object.DestroyImmediate(registro.gameObject);
        informe.Add($"Retirados: {n} repuestos" +
                    (perdidas > 0 ? $"; {perdidas} no aparecen en la escena y su marca se conserva en «{NombreRegistroRetirados}»." : "."));
    }

    /// Pone el activo y la etiqueta que apunta la marca. En las instancias de prefab, si lo repuesto coincide
    /// con el prefab, quita además el override que dejó la retirada: la escena queda como estaba.
    private static void ReponerEstado(GameObject go, Transform marca, List<string> informe)
    {
        bool activo = true;
        string etiqueta = "Untagged";
        foreach (Transform hijo in marca)
        {
            if (!hijo.name.StartsWith(PrefijoEstado)) continue;
            string[] partes = hijo.name.Split(new[] { '|' }, 3);
            if (partes.Length == 3 && bool.TryParse(partes[1], out bool a)) { activo = a; etiqueta = partes[2]; }
            break;
        }
        try { go.tag = etiqueta; }
        catch (UnityException)
        {
            go.tag = "Untagged";
            informe.Add($"  · «{go.name}» tenía la etiqueta «{etiqueta}», que ya no existe: queda sin etiqueta.");
        }
        go.SetActive(activo);
        GuardarOverrides(go);

        GameObject fuente = PrefabUtility.GetCorrespondingObjectFromSource(go);
        if (fuente == null) return;
        var so = new SerializedObject(go);
        SerializedProperty propiedadActivo = so.FindProperty("m_IsActive"), propiedadEtiqueta = so.FindProperty("m_TagString");
        if (propiedadActivo != null && propiedadActivo.prefabOverride && fuente.activeSelf == go.activeSelf)
            PrefabUtility.RevertPropertyOverride(propiedadActivo, InteractionMode.AutomatedAction);
        if (propiedadEtiqueta != null && propiedadEtiqueta.prefabOverride && fuente.tag == go.tag)
            PrefabUtility.RevertPropertyOverride(propiedadEtiqueta, InteractionMode.AutomatedAction);
    }

    /// En una instancia de prefab, apunta como override lo cambiado por código (si no, Unity puede
    /// perderlo al recargar el prefab).
    private static void GuardarOverrides(Object objeto)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(objeto)) PrefabUtility.RecordPrefabInstancePropertyModifications(objeto);
    }

    // ── Búsquedas en la escena ───────────────────────────────────────────────────────────────

    /// Índice de las instancias de prefab de la escena por nombre del asset, fuera de lo generado y de los
    /// registros. Se construye la primera vez que hace falta y se olvida al empezar y al acabar cada menú.
    private static Dictionary<string, List<Transform>> indicePorPrefab;
    private static Scene escenaDelIndice;

    private static void OlvidarIndices() => indicePorPrefab = null;

    /// Lo que no cuenta como «lo que ya estaba»: la raíz generada y los registros del vestido.
    private static bool EsDelVestido(Transform t) =>
        t.name == NombreRaiz || t.name == NombreRegistroCasas || t.name == NombreRegistroRetirados;

    private static void IndexarPrefabs(Scene escena)
    {
        indicePorPrefab = new Dictionary<string, List<Transform>>();
        escenaDelIndice = escena;
        var pila = new Stack<Transform>();
        foreach (GameObject raiz in escena.GetRootGameObjects()) pila.Push(raiz.transform);
        while (pila.Count > 0)
        {
            Transform t = pila.Pop();
            if (EsDelVestido(t)) continue;
            if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
            {
                GameObject fuente = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (fuente != null)
                {
                    if (!indicePorPrefab.TryGetValue(fuente.name, out List<Transform> l)) indicePorPrefab[fuente.name] = l = new List<Transform>();
                    l.Add(t);
                }
            }
            foreach (Transform h in t) pila.Push(h);
        }
    }

    /// Instancias raíz de prefab (activas o no) cuyo asset se llama «nombrePrefab» (p. ej. "BuildingAT23") y
    /// cuya posición en planta está a menos de «tolerancia» de «xz», de la más cercana a la más lejana. Sirve
    /// para localizar piezas del generador del mapa, que repite nombres. No mira lo generado por el vestido.
    private static List<Transform> BuscarPorPrefabYPosicion(Scene escena, string nombrePrefab, Vector2 xz, float tolerancia)
    {
        if (indicePorPrefab == null || escenaDelIndice != escena) IndexarPrefabs(escena);
        var r = new List<Transform>();
        if (!indicePorPrefab.TryGetValue(nombrePrefab, out List<Transform> lista)) return r;
        float Distancia(Transform t) => Vector2.Distance(new Vector2(t.position.x, t.position.z), xz);
        foreach (Transform t in lista)
            if (t != null && Distancia(t) < tolerancia) r.Add(t);
        r.Sort((a, b) => Distancia(a).CompareTo(Distancia(b)));
        return r;
    }

    /// Primer objeto llamado «nombre» a cualquier profundidad bajo WORLD (los menos hondos primero), fuera de
    /// lo generado y de los registros. Null si no hay.
    private static Transform BuscarGrupoPorNombre(Scene escena, string nombre)
    {
        var cola = new Queue<Transform>();
        foreach (GameObject raiz in escena.GetRootGameObjects())
            if (raiz.name == NombrePadre) cola.Enqueue(raiz.transform);
        while (cola.Count > 0)
            foreach (Transform h in cola.Dequeue())
            {
                if (EsDelVestido(h)) continue;
                if (h.name == nombre) return h;
                cola.Enqueue(h);
            }
        return null;
    }
}
