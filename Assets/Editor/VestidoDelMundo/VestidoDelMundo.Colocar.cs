using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Colocación de piezas sobre el Terrain de MainWorld con comprobaciones: tamaño objetivo medido en el
/// propio prefab, apoyo en el terreno (bajo la huella, bajo el tronco, en el fondo del agua o flotando),
/// desnivel y pendiente máximos, choque con colisionadores que ya estaban en la escena, zonas que hay que
/// dejar libres y caminos pintados. Lo que no pasa una comprobación no se coloca y se anota en el informe.
public static partial class VestidoDelMundo
{
    private enum Medida { Alto, Lado, Escala }

    /// Cómo se apoya una pieza en el terreno.
    private enum Apoyo
    {
        /// Base en el punto más bajo del terreno bajo toda la huella; el desnivel se mide en toda ella.
        Huella,
        /// Árboles, matas y farolas: apoyo, desnivel y choques en un círculo de radio RadioTronco alrededor
        /// de Pos, y el solape con lo demás del vestido con la huella reducida a la mitad (las copas pueden
        /// montarse). Se hunde 0,15 m + RadioTronco × tan(pendiente), hasta 0,6 m; Hundir no se usa.
        Tronco,
        /// Como Huella, pero también bajo el agua (fondo de la laguna): no mira el nivel del mar.
        Fondo,
        /// Sobre la lámina de agua: base en CotaAgua − Hundir, sin mirar el desnivel ni el nivel del mar.
        Flotar,
    }

    /// Una pieza a colocar. Pos es el centro de la huella visible en planta; Rumbo, el giro en grados.
    private sealed class Pieza
    {
        public string Prefab;
        public string Nombre;
        public Vector2 Pos;
        public float Rumbo;
        public float Tamano = 1f;
        public Medida Medida = Medida.Escala;
        /// Metros que se hunde la base por debajo del punto más bajo del terreno bajo la huella (en Flotar,
        /// por debajo de CotaAgua; negativo la deja por encima). En Tronco no se usa.
        public float Hundir = 0.05f;
        /// Inclinación en grados (ruinas, troncos caídos): se aplica alrededor de un eje horizontal según Rumbo.
        public float Inclinar;
        /// Desnivel máximo del terreno bajo la huella (en Tronco, en su círculo); por encima no se coloca.
        public float DesnivelMax = 1.2f;
        public bool PermitirSolapePropio;
        public bool PermitirCamino;
        /// Holgura extra (m) alrededor de la huella al comprobar choques con lo que ya había.
        public float Holgura = 0.3f;
        /// Cómo se apoya en el terreno.
        public Apoyo Apoyo = Apoyo.Huella;
        /// Radio (m) del círculo de apoyo en Tronco; 0: 0,25 × el lado mayor de la huella, con un mínimo de 0,3 m.
        public float RadioTronco;
        /// Pendiente máxima del terreno en Pos, en grados; por encima no se coloca.
        public float PendienteMax = 90f;
        /// Altura de la lámina de agua sobre la que flota (Apoyo.Flotar).
        public float CotaAgua;
        /// No pasa por las zonas libres (piezas que van dentro de ellas a propósito: muralla, escalinata).
        public bool IgnorarZonas;
        /// No recibe NavMeshObstacle al clasificar lo generado (escalinata, puerta, pavimento, juncos…).
        public bool SinObstaculo;
        /// Si no es null, sustituye al Tree.mat del pack FK en todas las mallas de la pieza (follaje matizado
        /// de los árboles verdes, cerezo). Los árboles de color se dejan con Tree.mat.
        public Material MaterialFollaje;
    }

    /// Nivel del mar de MainWorld (WORLD/Mar). Lo que apoyaría por debajo de NivelDelMar + MargenDeOrilla
    /// quedaría en el agua o en la orilla mojada y no se coloca.
    private const float NivelDelMar = 0f;
    private const float MargenDeOrilla = 0.3f;

    /// Huella orientada en planta: centro, ejes unitarios y semitamaño a lo largo de cada eje.
    private struct Huella
    {
        public Vector2 Centro, EjeX, EjeZ;
        public float MedioX, MedioZ;

        private float Radio(Vector2 eje) => Mathf.Abs(Vector2.Dot(EjeX, eje)) * MedioX + Mathf.Abs(Vector2.Dot(EjeZ, eje)) * MedioZ;

        private static bool Separa(Vector2 eje, Huella a, Huella b) =>
            Mathf.Abs(Vector2.Dot(b.Centro - a.Centro, eje)) > a.Radio(eje) + b.Radio(eje);

        /// Prueba de ejes separadores entre dos rectángulos orientados.
        public bool Solapa(Huella b) =>
            !Separa(EjeX, this, b) && !Separa(EjeZ, this, b) && !Separa(b.EjeX, this, b) && !Separa(b.EjeZ, this, b);

        public bool Contiene(Vector2 p, float margen)
        {
            Vector2 d = p - Centro;
            return Mathf.Abs(Vector2.Dot(d, EjeX)) <= MedioX + margen && Mathf.Abs(Vector2.Dot(d, EjeZ)) <= MedioZ + margen;
        }

        /// La misma huella con los lados multiplicados por «factor» alrededor de su centro.
        public Huella Reducida(float factor) =>
            new Huella { Centro = Centro, EjeX = EjeX, EjeZ = EjeZ, MedioX = MedioX * factor, MedioZ = MedioZ * factor };
    }

    /// Estado de una ejecución: terreno, raíz generada, informe y huellas ya ocupadas.
    private sealed class Obra
    {
        public Terrain Terreno;
        public Lienzo Suelo;
        public Transform Raiz;
        public readonly List<string> Informe = new();
        public readonly List<Huella> Ocupado = new();
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
        /// Lo generado que no debe recibir NavMeshObstacle al clasificar (ver SinObstaculo(Obra, Collider)).
        public readonly HashSet<Transform> SinObstaculo = new();
        /// Identificadores de lo retirado de la escena; se lee del registro la primera vez que hace falta.
        public HashSet<string> Retirados;
        /// Por cada motivo de retirada, su línea en el informe y cuántos objetos van.
        public readonly Dictionary<string, (int linea, int cuenta)> RetiradosPorMotivo = new();

        public void Descartar(string motivo, Pieza p)
        {
            Descartadas++;
            Motivos.TryGetValue(motivo, out int n);
            Motivos[motivo] = n + 1;
            if (n < 6) Informe.Add($"  · descartada «{p.Nombre}» en ({p.Pos.x:0.#}, {p.Pos.y:0.#}): {motivo}");
        }
    }

    /// Zona que debe quedar libre (anclas, arenas, plazas de eventos, recorridos de escolta…): un círculo,
    /// o un rectángulo alineado con los ejes si Medio no es cero.
    private struct Zona
    {
        public string Nombre;
        public Vector2 Centro;
        public float Radio;
        public Vector2 Medio;
        public Zona(string nombre, float x, float z, float radio) { Nombre = nombre; Centro = new Vector2(x, z); Radio = radio; Medio = Vector2.zero; }

        public static Zona Rectangulo(string nombre, float xMin, float zMin, float xMax, float zMax) => new Zona
        {
            Nombre = nombre,
            Centro = new Vector2((xMin + xMax) * 0.5f, (zMin + zMax) * 0.5f),
            Medio = new Vector2((xMax - xMin) * 0.5f, (zMax - zMin) * 0.5f),
        };
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

    /// Altura mínima y máxima del terreno en un círculo (centro y ocho puntos del borde).
    private static void AlturasEnCirculo(Obra o, Vector2 centro, float radio, out float min, out float max)
    {
        min = max = o.Suelo.Altura(centro.x, centro.y);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 0.25f;
            float y = o.Suelo.Altura(centro.x + Mathf.Cos(a) * radio, centro.y + Mathf.Sin(a) * radio);
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
        Vector3 medio = new Vector3(b.extents.x + holgura, Mathf.Max(0.2f, b.extents.y * 0.8f), b.extents.z + holgura);
        // Se sube un poco la caja para no contar el propio terreno ni los bordillos a ras de suelo.
        Vector3 centro = b.center + Vector3.up * (b.extents.y * 0.2f + 0.15f);
        return ChocaEnCaja(o, centro, medio);
    }

    /// Choque de un tronco: columna de radio «radio» (más la holgura) desde un palmo sobre el suelo en
    /// Pos hasta lo alto de la pieza. La copa puede pasar por encima de lo que había.
    private static bool ChocaElTronco(Obra o, Vector2 pos, float radio, Bounds b, float holgura)
    {
        float suelo = o.Suelo.Altura(pos.x, pos.y) + 0.2f;
        float techo = Mathf.Max(b.max.y, suelo + 0.4f);
        float lado = radio + holgura;
        return ChocaEnCaja(o, new Vector3(pos.x, (suelo + techo) * 0.5f, pos.y), new Vector3(lado, (techo - suelo) * 0.5f, lado));
    }

    /// Si algo que ya estaba en la escena (fuera de lo generado) ocupa la caja de centro y semitamaño dados.
    private static bool ChocaEnCaja(Obra o, Vector3 centro, Vector3 medio)
    {
        Physics.SyncTransforms();
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
        {
            bool dentro = z.Medio == Vector2.zero
                ? Vector2.Distance(centro, z.Centro) < z.Radio + radio
                : Mathf.Abs(centro.x - z.Centro.x) < z.Medio.x + b.extents.x && Mathf.Abs(centro.y - z.Centro.y) < z.Medio.y + b.extents.z;
            if (dentro) { zona = z.Nombre; return true; }
        }
        zona = null;
        return false;
    }

    private static bool SolapaPropio(Obra o, Huella h)
    {
        foreach (Huella q in o.Ocupado)
            if (q.Solapa(h)) return true;
        return false;
    }

    /// Huella de las mallas visibles en los ejes del rumbo (más ajustada que el AABB de mundo en piezas giradas).
    private static Huella HuellaOrientada(GameObject go, float rumbo)
    {
        Quaternion giro = Quaternion.Euler(0f, rumbo, 0f);
        Vector3 x3 = giro * Vector3.right, z3 = giro * Vector3.forward;
        var h = new Huella { EjeX = new Vector2(x3.x, x3.z), EjeZ = new Vector2(z3.x, z3.z) };
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;

        void Sumar(Vector3 p)
        {
            var q = new Vector2(p.x, p.z);
            float a = Vector2.Dot(q, h.EjeX), c = Vector2.Dot(q, h.EjeZ);
            minX = Mathf.Min(minX, a); maxX = Mathf.Max(maxX, a);
            minZ = Mathf.Min(minZ, c); maxZ = Mathf.Max(maxZ, c);
        }

        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            Bounds local;
            Matrix4x4 m;
            if (r is MeshRenderer && r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null) { local = mf.sharedMesh.bounds; m = r.localToWorldMatrix; }
            else if (r is MeshRenderer || r is SkinnedMeshRenderer) { local = r.bounds; m = Matrix4x4.identity; }
            else continue;
            for (int i = 0; i < 8; i++)
                Sumar(m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        }
        if (minX > maxX) { h.Centro = new Vector2(go.transform.position.x, go.transform.position.z); return h; }
        h.MedioX = (maxX - minX) * 0.5f;
        h.MedioZ = (maxZ - minZ) * 0.5f;
        h.Centro = h.EjeX * ((minX + maxX) * 0.5f) + h.EjeZ * ((minZ + maxZ) * 0.5f);
        return h;
    }

    /// Instancia, escala, gira y apoya una pieza. Devuelve null si no pasa las comprobaciones.
    private static GameObject Poner(Obra o, Transform grupo, Pieza p)
    {
        GameObject prefab = CargarPrefab(o, p.Prefab);
        if (prefab == null) { o.Descartar("falta el prefab", p); return null; }
        if (p.PendienteMax < 90f && o.Suelo.Pendiente(p.Pos.x, p.Pos.y) > p.PendienteMax)
        {
            o.Descartar($"pendiente de más de {p.PendienteMax:0}°", p);
            return null;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grupo);
        go.name = p.Nombre;
        Transform t = go.transform;
        Vector3 escalaBase = prefab.transform.localScale;
        // Se compone con el giro propio de la raíz del prefab (alguno lo trae para que su frente mire a +Z).
        Quaternion giroBase = prefab.transform.localRotation;
        t.rotation = Quaternion.Euler(0f, p.Rumbo, 0f) * giroBase;
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
            t.rotation = Quaternion.AngleAxis(p.Inclinar, Quaternion.Euler(0f, p.Rumbo, 0f) * Vector3.right) * Quaternion.Euler(0f, p.Rumbo, 0f) * giroBase;

        // Centrar la huella visible en Pos.
        b = LimitesVisibles(go);
        t.position += new Vector3(p.Pos.x - b.center.x, 0f, p.Pos.y - b.center.z);
        b = LimitesVisibles(go);

        // La huella en planta no depende de la altura: se mide antes de apoyar (el tronco sale de ella).
        Huella huella = HuellaOrientada(go, p.Rumbo);
        float radioTronco = p.Apoyo != Apoyo.Tronco ? 0f
            : p.RadioTronco > 0f ? p.RadioTronco : Mathf.Max(0.3f, 0.5f * Mathf.Max(huella.MedioX, huella.MedioZ));
        string motivo = Apoyar(o, p, t, b, radioTronco);
        if (motivo != null)
        {
            Object.DestroyImmediate(go);
            o.Descartar(motivo, p);
            return null;
        }
        b = LimitesVisibles(go);

        if (p.Apoyo == Apoyo.Tronco) huella = huella.Reducida(0.5f);
        if (!p.PermitirSolapePropio && SolapaPropio(o, huella))
        {
            Object.DestroyImmediate(go);
            o.Descartar("se pisa con otra pieza del vestido", p);
            return null;
        }
        if (!p.IgnorarZonas && EnZonaLibre(o, b, out string zona))
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
        if (p.Apoyo == Apoyo.Tronco ? ChocaElTronco(o, p.Pos, radioTronco, b, p.Holgura) : ChocaConLoQueHabia(o, b, p.Holgura))
        {
            Object.DestroyImmediate(go);
            o.Descartar("choca con algo que ya estaba en la escena", p);
            return null;
        }

        if (p.MaterialFollaje != null) CambiarFollaje(go, p.MaterialFollaje);
        if (p.SinObstaculo) o.SinObstaculo.Add(t);
        o.Ocupado.Add(huella);
        o.Puestas++;
        return go;
    }

    /// Sube o baja la pieza (ya centrada en Pos) hasta su apoyo según p.Apoyo. Devuelve el motivo de
    /// descarte, o null si se ha apoyado.
    private static string Apoyar(Obra o, Pieza p, Transform t, Bounds b, float radioTronco)
    {
        if (p.Apoyo == Apoyo.Flotar)
        {
            t.position += Vector3.up * (p.CotaAgua - p.Hundir - b.min.y);
            return null;
        }

        float min, max, baseY;
        if (p.Apoyo == Apoyo.Tronco)
        {
            AlturasEnCirculo(o, p.Pos, radioTronco, out min, out max);
            if (max - min > p.DesnivelMax) return $"terreno demasiado desigual bajo el tronco ({max - min:0.0} m)";
            float pendiente = Mathf.Min(o.Suelo.Pendiente(p.Pos.x, p.Pos.y), 89f);
            baseY = o.Suelo.Altura(p.Pos.x, p.Pos.y) - Mathf.Min(0.6f, 0.15f + radioTronco * Mathf.Tan(pendiente * Mathf.Deg2Rad));
        }
        else
        {
            AlturasBajo(o, b, out min, out max);
            if (max - min > p.DesnivelMax) return $"terreno demasiado desigual ({max - min:0.0} m)";
            baseY = min - p.Hundir;
        }
        if (p.Apoyo != Apoyo.Fondo && min < NivelDelMar + MargenDeOrilla) return "en el agua o en la orilla";
        t.position += Vector3.up * (baseY - b.min.y);
        return null;
    }

    /// Material de serie de los árboles del pack FK: la paleta de color de todas sus variantes.
    private const string FinRutaMaterialArbolFK = "/Fantasy_Kingdom_Pack/Materials/Tree.mat";

    /// Cambia el Tree.mat del pack FK por «follaje» en todas las mallas de la pieza.
    private static void CambiarFollaje(GameObject go, Material follaje)
    {
        foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material[] materiales = r.sharedMaterials;
            bool cambiado = false;
            for (int i = 0; i < materiales.Length; i++)
            {
                if (materiales[i] == null || !AssetDatabase.GetAssetPath(materiales[i]).EndsWith(FinRutaMaterialArbolFK, System.StringComparison.Ordinal)) continue;
                materiales[i] = follaje;
                cambiado = true;
            }
            if (!cambiado) continue;
            r.sharedMaterials = materiales;
            GuardarOverrides(r);
        }
    }

    /// Si el collider cuelga de algo que se puso con SinObstaculo (o se añadió a mano a o.SinObstaculo):
    /// NavMeshAutoSetup.ClasificarBajo no le pone NavMeshObstacle.
    private static bool SinObstaculo(Obra o, Collider c)
    {
        if (c == null || o.SinObstaculo.Count == 0) return false;
        for (Transform t = c.transform; t != null; t = t.parent)
        {
            if (o.SinObstaculo.Contains(t)) return true;
            if (t == o.Raiz) return false;
        }
        return false;
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
