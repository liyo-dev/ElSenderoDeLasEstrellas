using System;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lo que necesita cualquier escena para poder recorrerla con el grupo: el suelo en la capa Floor
/// (la única que pisa el jugador), el grupo con su cámara, el NavMesh por el que le siguen los
/// compañeros y la interfaz de juego de Start.unity encendida en ella. Lo usan los constructores
/// de escenas; cada uno pone solo sus datos (qué piezas son suelo, dónde se empieza).
/// </summary>
public static class EscenaJugable
{
    public const string CapaDelSuelo = "Floor";

    /// <summary>La escena de juego de referencia: la interfaz que se ve en ella es la interfaz de juego.</summary>
    private const string EscenaDeReferencia = "MainWorld";

    private const string PrefabWill = "Assets/Prefabs/_WILL.prefab";
    private const string PrefabEstela = "Assets/Prefabs/_ESTELA.prefab";
    private const string PrefabLiam = "Assets/Prefabs/_LIAM.prefab";
    private const string PrefabCamara = "Assets/Prefabs/CamaraDelJugador.prefab";
    private const string PrefabGrupo = "Assets/Prefabs/GrupoDelJugador.prefab";

    /// <summary>Los objetos que el grupo deja en la escena.</summary>
    public sealed class Grupo
    {
        public GameObject Will, Estela, Liam, Camara, Datos;
        public GameObject[] Personajes => new[] { Will, Estela, Liam };
    }

    /// <summary>
    /// Pone a Will en <paramref name="pos"/> mirando hacia <paramref name="mirar"/>, a Estela y a Liam a
    /// sus lados, la cámara detrás y el objeto del grupo (inventario, vestuario, cargas). Quita antes
    /// los que ya hubiera en la escena con esos nombres, así que se puede repetir.
    /// </summary>
    public static Grupo ColocarGrupo(Scene escena, Vector3 pos, Vector3 mirar)
    {
        mirar.y = 0f;
        if (mirar.sqrMagnitude < 0.0001f) mirar = Vector3.forward;
        Quaternion giro = Quaternion.LookRotation(mirar.normalized);
        Vector3 A(float x, float y, float z) => pos + giro * new Vector3(x, y, z);

        return new Grupo
        {
            Will = Instanciar(PrefabWill, "_WILL", escena, A(0f, 0f, 0f), giro),
            Estela = Instanciar(PrefabEstela, "_ESTELA", escena, A(-1.4f, 0f, 1.3f), giro),
            Liam = Instanciar(PrefabLiam, "_LIAM", escena, A(1.4f, 0f, 1.3f), giro),
            Camara = Instanciar(PrefabCamara, "CamaraDelJugador", escena, A(0f, 2.55f, -1.8f), giro),
            Datos = Instanciar(PrefabGrupo, "GrupoDelJugador", escena, pos, Quaternion.identity),
        };
    }

    /// <summary>
    /// La interfaz de juego de <paramref name="start"/> (HUD, aviso de misión nueva, menús de misiones
    /// y de equipo, avisos de objetos…) se ve solo en las escenas de la lista de su
    /// <see cref="SceneBoundUI"/>. Añade <paramref name="escenaDeJuego"/> a cada lista que incluye
    /// MainWorld, sin tocar la interfaz que se ve en todas partes (lista vacía) ni la de otras
    /// escenas. Devuelve cuántas listas ha cambiado; quien llama guarda Start.unity.
    /// </summary>
    public static int PermitirInterfazDeJuego(Scene start, string escenaDeJuego)
    {
        if (!start.IsValid() || !start.isLoaded || string.IsNullOrEmpty(escenaDeJuego)) return 0;
        int cambiadas = 0;
        foreach (GameObject raiz in start.GetRootGameObjects())
            foreach (var interfaz in raiz.GetComponentsInChildren<SceneBoundUI>(true))
            {
                var so = new SerializedObject(interfaz);
                var lista = so.FindProperty("allowedScenes");
                if (lista == null || !Contiene(lista, EscenaDeReferencia) || Contiene(lista, escenaDeJuego)) continue;
                lista.arraySize++;
                lista.GetArrayElementAtIndex(lista.arraySize - 1).stringValue = escenaDeJuego;
                so.ApplyModifiedPropertiesWithoutUndo();
                cambiadas++;
            }
        return cambiadas;
    }

    private static bool Contiene(SerializedProperty lista, string valor)
    {
        for (int i = 0; i < lista.arraySize; i++)
            if (lista.GetArrayElementAtIndex(i).stringValue == valor) return true;
        return false;
    }

    /// <summary>
    /// Pone en la capa Floor, con toda su jerarquía, cada instancia de prefab de la escena cuyo
    /// prefab de origen empiece por uno de los prefijos dados. Devuelve cuántas ha cambiado.
    /// </summary>
    public static int PonerSueloEnSuCapa(Scene escena, params string[] prefijosDeSuelo)
    {
        int capa = LayerMask.NameToLayer(CapaDelSuelo);
        if (capa < 0)
        {
            Debug.LogError($"[EscenaJugable] No existe la capa {CapaDelSuelo}.");
            return 0;
        }

        int cambiados = 0;
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
                GameObject origen = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (origen == null || !EmpiezaPorAlguno(origen.name, prefijosDeSuelo)) continue;
                if (PonerCapa(go.transform, capa)) cambiados++;
            }
        return cambiados;
    }

    /// <summary>
    /// Pone en la capa Floor las instancias de prefab cuyo origen empieza por uno de los prefijos y
    /// que, colocadas, hacen de suelo (planchas tumbadas, andenes, tarimas o peldaños): algún
    /// colisionador sólido no más alto que <paramref name="alturaMaxima"/> y más ancho que alto en
    /// planta en las dos direcciones. Las mismas piezas de pie (muros, más altos que gruesos) se
    /// quedan como están.
    /// </summary>
    public static int PonerLosasEnSuCapa(Scene escena, float alturaMaxima, params string[] prefijos)
    {
        int capa = LayerMask.NameToLayer(CapaDelSuelo);
        if (capa < 0) return 0;
        Physics.SyncTransforms();
        int cambiados = 0;
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
                GameObject origen = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (origen == null || !EmpiezaPorAlguno(origen.name, prefijos)) continue;
                bool losa = false;
                foreach (var c in go.GetComponentsInChildren<Collider>(true))
                {
                    if (c.isTrigger) continue;
                    Vector3 tam = c.bounds.size;
                    if (tam.y <= alturaMaxima && Mathf.Min(tam.x, tam.z) > tam.y) { losa = true; break; }
                }
                if (losa && PonerCapa(go.transform, capa)) cambiados++;
            }
        return cambiados;
    }

    /// <summary>
    /// El jugador (cápsula con Rigidbody) no sube peldaños: sobre cada tramo de escalera se pone una
    /// rampa invisible en la capa Floor que va de nariz a nariz de los peldaños. Los peldaños son los
    /// colisionadores de la capa Floor bajo <paramref name="padre"/> que no pasan de
    /// <paramref name="huellaMaxima"/> de lado; un tramo son tres o más seguidos, cada uno algo más
    /// alto y un poco más allá que el anterior, en la misma dirección. Las rampas van bajo
    /// <paramref name="padre"/> (las coge el NavMesh) y se rehacen en cada llamada. Devuelve cuántas.
    /// </summary>
    public static int RampasEnEscaleras(Transform padre, float huellaMaxima = 6f)
    {
        const string NombreDeRaiz = "Rampas de escaleras (generadas)";
        Transform vieja = padre.Find(NombreDeRaiz);
        if (vieja != null) UnityEngine.Object.DestroyImmediate(vieja.gameObject);

        int capa = LayerMask.NameToLayer(CapaDelSuelo);
        if (capa < 0) return 0;
        Physics.SyncTransforms();

        var peldanos = new System.Collections.Generic.List<Bounds>();
        foreach (var c in padre.GetComponentsInChildren<Collider>(true))
        {
            if (c.isTrigger || c.gameObject.layer != capa) continue;
            Bounds b = c.bounds;
            if (b.size.x > huellaMaxima || b.size.z > huellaMaxima) continue;
            peldanos.Add(b);
        }

        // Siguiente peldaño de cada uno: algo más alto y cerca en planta (el más cercano).
        int n = peldanos.Count;
        var siguiente = new int[n];
        var tienePrevio = new bool[n];
        for (int i = 0; i < n; i++)
        {
            siguiente[i] = -1;
            float mejor = float.MaxValue;
            for (int j = 0; j < n; j++)
            {
                if (i == j) continue;
                float subida = peldanos[j].max.y - peldanos[i].max.y;
                if (subida < 0.06f || subida > 0.4f) continue;
                float enPlanta = Plano(peldanos[j].center - peldanos[i].center).magnitude;
                if (enPlanta < 0.1f || enPlanta > 1.2f) continue;
                if (enPlanta < mejor) { mejor = enPlanta; siguiente[i] = j; }
            }
            if (siguiente[i] >= 0) tienePrevio[siguiente[i]] = true;
        }

        Transform raiz = null;
        int rampas = 0;
        for (int i = 0; i < n; i++)
        {
            if (tienePrevio[i] || siguiente[i] < 0) continue;
            var tramo = new System.Collections.Generic.List<Bounds> { peldanos[i] };
            int k = i;
            Vector3 dir = Vector3.zero;
            while (siguiente[k] >= 0 && tramo.Count < 200)
            {
                int s = siguiente[k];
                Vector3 paso = Plano(peldanos[s].center - peldanos[k].center).normalized;
                if (dir != Vector3.zero && Vector3.Angle(dir, paso) > 35f) break;
                dir = dir == Vector3.zero ? paso : (dir + paso).normalized;
                tramo.Add(peldanos[s]);
                k = s;
            }
            if (tramo.Count < 3) continue;

            Bounds primero = tramo[0], ultimo = tramo[tramo.Count - 1];
            Vector3 d = Plano(ultimo.center - primero.center).normalized;
            Vector3 lado = Vector3.Cross(Vector3.up, d);
            float huella = Plano(ultimo.center - primero.center).magnitude / (tramo.Count - 1);
            float contrahuella = (ultimo.max.y - primero.max.y) / (tramo.Count - 1);
            float ancho = float.MaxValue;
            foreach (var b in tramo) ancho = Mathf.Min(ancho, Mathf.Abs(lado.x) * b.size.x + Mathf.Abs(lado.z) * b.size.z);
            ancho = Mathf.Clamp(ancho, 0.6f, huellaMaxima);

            // De la nariz del primer peldaño (un paso antes y una contrahuella más abajo) a la del último.
            Vector3 narizPrimera = Plano(primero.center) - d * (huella * 0.5f) + Vector3.up * primero.max.y;
            Vector3 narizUltima = Plano(ultimo.center) - d * (huella * 0.5f) + Vector3.up * ultimo.max.y;
            Vector3 inicio = narizPrimera - d * huella - Vector3.up * contrahuella;
            Vector3 fin = narizUltima;
            Vector3 eje = (fin - inicio);
            if (eje.sqrMagnitude < 0.01f) continue;
            Vector3 arriba = Vector3.Cross(eje.normalized, lado).normalized;
            if (arriba.y < 0f) arriba = -arriba;

            if (raiz == null)
            {
                raiz = new GameObject(NombreDeRaiz).transform;
                raiz.SetParent(padre, false);
            }
            var rampa = new GameObject($"Rampa {rampas + 1} ({tramo.Count} peldaños)");
            rampa.transform.SetParent(raiz, true);
            rampa.layer = capa;
            rampa.transform.SetPositionAndRotation((inicio + fin) * 0.5f - arriba * 0.05f, Quaternion.LookRotation(eje.normalized, arriba));
            var caja = rampa.AddComponent<BoxCollider>();
            caja.size = new Vector3(ancho, 0.1f, eje.magnitude);
            rampas++;
        }
        return rampas;
    }

    private static Vector3 Plano(Vector3 v) => new Vector3(v.x, 0f, v.z);

    /// <summary>
    /// Busca dónde poner los pies cerca de <paramref name="cerca"/>: suelo (capa Floor) plano y firme,
    /// no el borde de una barandilla, con sitio libre para un personaje, probando en espiral hasta
    /// <paramref name="radio"/> m (ver <see cref="SueloFirme"/>).
    /// </summary>
    public static bool BuscarSitioLibre(Vector3 cerca, float radio, out Vector3 sitio)
    {
        Physics.SyncTransforms();
        return SueloFirme.Buscar(cerca, radio, 60f, LayerMask.GetMask(CapaDelSuelo), SueloFirme.CapasQueNoEstorban, out sitio);
    }

    /// <summary>
    /// Hornea el NavMesh de <paramref name="surface"/> y lo guarda como asset junto a la escena
    /// (carpeta con el nombre de la escena), como hace el inspector del NavMeshSurface.
    /// </summary>
    public static void HornearNavMesh(NavMeshSurface surface)
    {
        surface.BuildNavMesh();
        if (surface.navMeshData == null) return;

        string rutaEscena = surface.gameObject.scene.path;
        if (string.IsNullOrEmpty(rutaEscena)) return;
        string carpeta = Path.Combine(Path.GetDirectoryName(rutaEscena), Path.GetFileNameWithoutExtension(rutaEscena)).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(carpeta))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(carpeta).Replace('\\', '/'), Path.GetFileName(carpeta));
        string ruta = $"{carpeta}/NavMesh-{surface.gameObject.name}.asset";
        AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(surface.navMeshData, ruta);
        EditorUtility.SetDirty(surface);
    }

    /// <summary>Quita de la escena el grupo que haya puesto antes (para buscarle sitio sin que estorbe).</summary>
    public static void QuitarGrupo(Scene escena)
    {
        foreach (string nombre in NombresDelGrupo) QuitarRaiz(escena, nombre);
    }

    private static readonly string[] NombresDelGrupo = { "_WILL", "_ESTELA", "_LIAM", "CamaraDelJugador", "GrupoDelJugador" };

    private static void QuitarRaiz(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
            if (raiz.name == nombre) UnityEngine.Object.DestroyImmediate(raiz);
    }

    private static GameObject Instanciar(string ruta, string nombre, Scene escena, Vector3 pos, Quaternion giro)
    {
        QuitarRaiz(escena, nombre);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (prefab == null)
        {
            Debug.LogWarning($"[EscenaJugable] No se encontró {ruta}.");
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
        go.name = nombre;
        go.transform.SetPositionAndRotation(pos, giro);
        return go;
    }

    private static bool PonerCapa(Transform t, int capa)
    {
        bool cambiado = t.gameObject.layer != capa;
        t.gameObject.layer = capa;
        foreach (Transform hijo in t) cambiado |= PonerCapa(hijo, capa);
        return cambiado;
    }

    private static bool EmpiezaPorAlguno(string nombre, string[] prefijos)
    {
        foreach (string p in prefijos)
            if (nombre.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
