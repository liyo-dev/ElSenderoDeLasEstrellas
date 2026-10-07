using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Colocación de piezas sobre el Terrain de MainWorld con comprobaciones: tamaño objetivo medido en el
/// propio prefab, apoyo en varios puntos de la huella, desnivel máximo, choque con colisionadores que ya
/// estaban en la escena, zonas que hay que dejar libres y caminos pintados. Lo que no pasa una comprobación
/// no se coloca y se anota en el informe.
public static partial class VestidoDelMundo
{
    private enum Medida { Alto, Lado, Escala }

    /// Una pieza a colocar. Pos es el centro de la huella visible en planta; Rumbo, el giro en grados.
    private sealed class Pieza
    {
        public string Prefab;
        public string Nombre;
        public Vector2 Pos;
        public float Rumbo;
        public float Tamano = 1f;
        public Medida Medida = Medida.Escala;
        /// Metros que se hunde la base por debajo del punto más bajo del terreno bajo la huella.
        public float Hundir = 0.05f;
        /// Inclinación en grados (ruinas, troncos caídos): se aplica alrededor de un eje horizontal según Rumbo.
        public float Inclinar;
        /// Desnivel máximo del terreno bajo la huella; por encima no se coloca.
        public float DesnivelMax = 1.2f;
        public bool PermitirSolapePropio;
        public bool PermitirCamino;
        /// Holgura extra (m) alrededor de la huella al comprobar choques con lo que ya había.
        public float Holgura = 0.3f;
    }

    /// Estado de una ejecución: terreno, raíz generada, informe y huellas ya ocupadas.
    private sealed class Obra
    {
        public Terrain Terreno;
        public Lienzo Suelo;
        public Transform Raiz;
        public readonly List<string> Informe = new();
        public readonly List<Rect> Ocupado = new();
        public readonly List<Zona> Libres = new();
        public readonly Dictionary<string, GameObject> Prefabs = new();
        public readonly Collider[] Buffer = new Collider[64];
        public int Puestas, Descartadas;
        public readonly Dictionary<string, int> Motivos = new();
        /// Calles, accesos y sendas de puerta que deben quedar libres (polilínea y semiancho).
        public readonly List<(Vector2[] puntos, float semiancho)> Corredores = new();
        /// Plazas: su interior no se adorna (las del Reino están reservadas para eventos).
        public readonly List<Rect> PlazasLibres = new();
        /// Ejemplares de cada prefab que ya estaban en la escena (para copiar sus materiales matizados).
        public readonly Dictionary<string, List<GameObject>> EjemplaresPorPrefab = new();

        public void Descartar(string motivo, Pieza p)
        {
            Descartadas++;
            Motivos.TryGetValue(motivo, out int n);
            Motivos[motivo] = n + 1;
            if (n < 6) Informe.Add($"  · descartada «{p.Nombre}» en ({p.Pos.x:0.#}, {p.Pos.y:0.#}): {motivo}");
        }
    }

    /// Zona que debe quedar libre (anclas, arenas, plazas de eventos, recorridos de escolta…).
    private struct Zona
    {
        public string Nombre;
        public Vector2 Centro;
        public float Radio;
        public Zona(string nombre, float x, float z, float radio) { Nombre = nombre; Centro = new Vector2(x, z); Radio = radio; }
    }

    private static GameObject CargarPrefab(Obra o, string ruta)
    {
        if (o.Prefabs.TryGetValue(ruta, out GameObject g)) return g;
        g = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        o.Prefabs[ruta] = g;
        if (g == null) o.Informe.Add($"  ! falta el prefab {ruta}: se omiten sus piezas.");
        return g;
    }

    /// Límites de los MeshRenderer y SkinnedMeshRenderer activos (sin partículas ni proyectores).
    private static Bounds LimitesVisibles(GameObject go)
    {
        bool hay = false;
        var b = new Bounds(go.transform.position, Vector3.zero);
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (!hay) { b = r.bounds; hay = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    /// Altura mínima y máxima del terreno bajo una huella (centro, esquinas y puntos medios).
    private static void AlturasBajo(Obra o, Bounds b, out float min, out float max)
    {
        min = float.MaxValue; max = float.MinValue;
        for (int a = -1; a <= 1; a++)
            for (int c = -1; c <= 1; c++)
            {
                float y = o.Suelo.Altura(b.center.x + a * b.extents.x, b.center.z + c * b.extents.z);
                min = Mathf.Min(min, y); max = Mathf.Max(max, y);
            }
    }

    private static readonly int[] CapasDeCamino = new int[8];

    /// Peso de camino/calle pintado en el punto (capa de camino del mapa, adoquín, baldosa y tierra de pueblo).
    private static float PesoCamino(Obra o, float x, float z)
    {
        Lienzo l = o.Suelo;
        CapasDeCamino[0] = l.Capa("Capa4");
        CapasDeCamino[1] = l.Capa(CapaAdoquin);
        CapasDeCamino[2] = l.Capa(CapaBaldosa);
        CapasDeCamino[3] = l.Capa("SueloUrbano1");
        CapasDeCamino[4] = l.Capa(CapaTierraPiedras);
        CapasDeCamino[5] = -1;
        CapasDeCamino[6] = -1;
        CapasDeCamino[7] = -1;
        float s = 0f;
        foreach (int c in CapasDeCamino) s += l.Peso(x, z, c);
        return s;
    }

    private static bool PisaCamino(Obra o, Bounds b)
    {
        for (int a = -1; a <= 1; a++)
            for (int c = -1; c <= 1; c++)
                if (PesoCamino(o, b.center.x + a * b.extents.x * 0.8f, b.center.z + c * b.extents.z * 0.8f) > 0.55f) return true;
        return false;
    }

    private static bool ChocaConLoQueHabia(Obra o, Bounds b, float holgura)
    {
        Physics.SyncTransforms();
        Vector3 medio = new Vector3(b.extents.x + holgura, Mathf.Max(0.2f, b.extents.y * 0.8f), b.extents.z + holgura);
        // Se sube un poco la caja para no contar el propio terreno ni los bordillos a ras de suelo.
        Vector3 centro = b.center + Vector3.up * (b.extents.y * 0.2f + 0.15f);
        int n = Physics.OverlapBoxNonAlloc(centro, medio, o.Buffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = o.Buffer[i];
            if (Ignorable(c)) continue;
            if (c.transform.IsChildOf(o.Raiz)) continue;
            if (c.gameObject.scene != o.Raiz.gameObject.scene) continue;
            return true;
        }
        return false;
    }

    /// Colisionadores que no cuentan como «algo que ya estaba»: el terreno y los volúmenes enormes
    /// (zonas de ambiente cuya caja solo pasa a trigger en Play, láminas de agua, bloqueadores).
    private static bool Ignorable(Collider c)
    {
        if (c == null || c is TerrainCollider || c.isTrigger) return true;
        Vector3 t = c.bounds.size;
        return t.x > 60f || t.z > 60f;
    }

    private static bool EnZonaLibre(Obra o, Bounds b, out string zona)
    {
        var centro = new Vector2(b.center.x, b.center.z);
        float radio = Mathf.Max(b.extents.x, b.extents.z);
        foreach (Zona z in o.Libres)
            if (Vector2.Distance(centro, z.Centro) < z.Radio + radio) { zona = z.Nombre; return true; }
        zona = null;
        return false;
    }

    private static bool SolapaPropio(Obra o, Rect r)
    {
        foreach (Rect q in o.Ocupado)
            if (q.Overlaps(r)) return true;
        return false;
    }

    /// Instancia, escala, gira y apoya una pieza. Devuelve null si no pasa las comprobaciones.
    private static GameObject Poner(Obra o, Transform grupo, Pieza p)
    {
        GameObject prefab = CargarPrefab(o, p.Prefab);
        if (prefab == null) { o.Descartar("falta el prefab", p); return null; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grupo);
        go.name = p.Nombre;
        Transform t = go.transform;
        Vector3 escalaBase = prefab.transform.localScale;
        t.rotation = Quaternion.Euler(0f, p.Rumbo, 0f);
        t.localScale = escalaBase;
        t.position = new Vector3(p.Pos.x, 0f, p.Pos.y);

        Bounds b = LimitesVisibles(go);
        if (b.size.sqrMagnitude < 1e-6f) { Object.DestroyImmediate(go); o.Descartar("el prefab no tiene mallas visibles", p); return null; }

        if (p.Medida != Medida.Escala)
        {
            float medido = p.Medida == Medida.Alto ? b.size.y : Mathf.Max(b.size.x, b.size.z);
            float factor = Mathf.Clamp(p.Tamano / Mathf.Max(medido, 0.01f), 0.1f, 8f);
            t.localScale = escalaBase * factor;
        }
        else t.localScale = escalaBase * p.Tamano;

        if (Mathf.Abs(p.Inclinar) > 0.01f)
            t.rotation = Quaternion.AngleAxis(p.Inclinar, Quaternion.Euler(0f, p.Rumbo, 0f) * Vector3.right) * Quaternion.Euler(0f, p.Rumbo, 0f);

        // Centrar la huella visible en Pos.
        b = LimitesVisibles(go);
        t.position += new Vector3(p.Pos.x - b.center.x, 0f, p.Pos.y - b.center.z);
        b = LimitesVisibles(go);

        AlturasBajo(o, b, out float min, out float max);
        if (max - min > p.DesnivelMax)
        {
            Object.DestroyImmediate(go);
            o.Descartar($"terreno demasiado desigual ({max - min:0.0} m)", p);
            return null;
        }
        t.position += Vector3.up * (min - p.Hundir - b.min.y);
        b = LimitesVisibles(go);

        var huella = new Rect(b.min.x, b.min.z, b.size.x, b.size.z);
        if (!p.PermitirSolapePropio && SolapaPropio(o, huella))
        {
            Object.DestroyImmediate(go);
            o.Descartar("se pisa con otra pieza del vestido", p);
            return null;
        }
        if (EnZonaLibre(o, b, out string zona))
        {
            Object.DestroyImmediate(go);
            o.Descartar("zona que debe quedar libre: " + zona, p);
            return null;
        }
        if (!p.PermitirCamino && PisaCamino(o, b))
        {
            Object.DestroyImmediate(go);
            o.Descartar("pisa un camino o una calle", p);
            return null;
        }
        if (ChocaConLoQueHabia(o, b, p.Holgura))
        {
            Object.DestroyImmediate(go);
            o.Descartar("choca con algo que ya estaba en la escena", p);
            return null;
        }

        o.Ocupado.Add(huella);
        o.Puestas++;
        return go;
    }

    private static Transform Grupo(Transform padre, string nombre)
    {
        Transform t = padre.Find(nombre);
        if (t != null) return t;
        var g = new GameObject(nombre);
        g.transform.SetParent(padre, false);
        return g.transform;
    }
}
