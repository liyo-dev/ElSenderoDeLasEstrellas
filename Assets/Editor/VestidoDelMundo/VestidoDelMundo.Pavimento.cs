using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// Pavimento de las ciudades (Pueblo.Pavimentado): calles, plazas y accesos de las puertas hechos con mallas
/// propias que siguen el terreno, con las texturas de suelo del pack FK (las del suelo de ciudad City01 del
/// propio pack). La pintura del terreno, a 2,5 m por celda, no da bordes nítidos, ni dibujos alineados con la
/// calle, ni bordillos; las mallas sí, y no gastan memoria de terreno.
///
/// · Calle: calzada de adoquín (Ground03) a lo largo de la calle, bordillos de sillares grises (banda gris de
///   Ground04) de 0,25 m de ancho y 0,12 m sobre el terreno, y aceras de losas (Ground01) de 0,75 m. En los
///   cruces sigue de largo la calle con prioridad (el paso de la puerta, después la más ancha) y la otra le
///   abre el bordillo; en las esquinas en L, la que gana se alarga hasta el borde de la otra y se remata con
///   bordillo y acera, así el bordillo de fuera es corrido; los fondos de saco se rematan igual. La calle que
///   atraviesa la muralla (paso de la puerta) va de adoquín de lado a lado, con cintas de piedra a ras y un
///   umbral donde empieza el camino.
/// · Plaza: baldosa (Ground05) alineada con la plaza y centrada en ella, marco de adoquín de 1,2 m y bordillo
///   donde la plaza linda con tierra. Donde entra una calzada, un acceso o se apoya obra (escalinata, muro,
///   casa, balaustrada), el bordillo se abre; entre dos plazas que se tocan no hay ni bordillo ni marco. Con
///   rosetón (Plaza.Roseton): disco de Ground02 con anillo de sillares y un eje de losas que cruza la plaza
///   por el rosetón en su dirección corta (en la Plaza Real, de la escalinata a la explanada del castillo).
///   Un talud de más de PendienteMaximaDePlaza dentro de una plaza no se pavimenta: queda como un ribazo
///   rectangular con su bordillo, como las zonas verdes.
/// · Acceso de puerta: losas como las de la acera, de la puerta de cada casa a la acera o a la plaza que tiene
///   delante, si está a menos de LargoMaximoDeAcceso y no se cruza otra casa por el camino.
///
/// Las zonas verdes del pueblo (Pueblo.Verdes) y lo que queda fuera del recinto no se pavimentan. Nada lleva
/// colisor ni proyecta sombra: el NavMesh y la colocación de piezas no cambian. La malla sigue el terreno a
/// 4 cm (6 cm en rampa) y se parte donde el terreno se arquea, así no asoma por crestas ni pies de talud; lo que
/// una superficie tapa de otra (la calzada que entra en una plaza o cede en un cruce, el final de un acceso) se
/// hunde unos centímetros para que no parpadee. Las mallas se guardan en CarpetaGenerada y se rehacen en cada
/// ejecución. Debajo del pavimento se pinta tierra con piedras (que cuenta como camino en PesoCamino: no se
/// planta ni se coloca nada encima) y un margen de tierra de 0,6 m, sin hierba de detalle. La geometría se
/// calcula aparte del motor (alturas, normales y obstáculos llegan como funciones).
public static partial class VestidoDelMundo
{
    private const string MaterialesDeSuelo = "Assets/Art/World/Fantasy_Kingdom_Pack/Materials/";

    /// Losas del pavimento: cada una es un material de suelo del pack FK y una submalla.
    private enum Losa { Acera, Roseton, Adoquin, Piedra, Baldosa }

    private static readonly string[] MaterialDeLosa = { "Ground01", "Ground02", "Ground03", "Ground04", "Ground05" };

    // ── Medidas (m) ──────────────────────────────────────────────────────────────────────────

    private const float AnchoDeAcera = 0.75f;               // dos losas de 0,375 m
    private const float AnchoDeBordillo = 0.25f;
    private const float AltoDeBordillo = 0.08f;             // sobre la calzada: 0,12 m sobre el terreno en llano
    private const float AnchoDeMarco = 1.2f;
    private const float AnchoDeCintaDePaso = 0.35f;
    private const float AnchoDeUmbral = 0.4f;
    private const float AnchoDeAnilloDeRoseton = 0.35f;
    private const float AnchoDeEjeDeLosas = 4.5f;           // doce losas
    private const float AnchoDeCintaDeEje = 0.3f;
    private const float AnchoDeAcceso = 2.25f;              // seis losas
    private const float LargoMaximoDeAcceso = 8f;           // de la fachada a lo pavimentado
    private const float FondoBajoLaCasa = 1.5f;             // el acceso empieza debajo del alero, contra el muro
    private const float EntraEnLaPlaza = 1f;                // la calzada que acaba en una plaza sigue por debajo
    private const float MetidoEnLoPavimentado = 0.2f;       // el acceso acaba por debajo de la acera o de la plaza
    private const float PasoDeMalla = 1.25f;                // separación máxima entre vértices (media celda del terreno)
    private const float AnchoMinimoConAceras = 4.5f;

    // Alturas sobre el terreno (m). El terreno solo se pisa con su colisor: todo va lo más bajo posible.
    private const float SobreElTerreno = 0.04f, SobreElTerrenoEnRampa = 0.06f;
    private const float CapaDeAcceso = 0.004f, CapaDePlaza = 0.008f, CapaDeRoseton = 0.016f, CapaDeAcera = 0.02f;
    /// Pie de bordillos y faldones: por debajo del terreno, para que no se vea luz por debajo.
    private const float BajoElTerreno = 0.06f;

    // Repeticiones de textura (m por repetición).
    private const float RepeticionDeAdoquin = 6f;           // como la capa de adoquín del terreno
    private const float RepeticionDeLosas = 3.75f;          // diez losas de 0,375 m: la acera lleva dos
    private const float RepeticionDeBordillo = 5.6f;        // sillares de 0,7 m (ocho por repetición)
    private const float RepeticionDeBaldosa = 8f;           // rombos de 2 m; uno amarillo en el centro de la plaza
    private const float SillarDeBordillo = RepeticionDeBordillo / 8f;

    /// Banda de sillares grises de Ground04 (coordenada v): la arista del bordillo y su fondo. El frente baja
    /// desde la arista hacia la junta (v 0,148–0,153), que hace de sombra contra la calzada.
    private const float BandaArista = 0.160f, BandaFondo = 0.195f;

    /// Tierra alrededor del pavimento.
    private const float MargenDeTierra = 0.6f;

    /// Lo que el terreno puede arquearse entre dos vértices sin asomar por encima del pavimento ni dejarlo en el
    /// aire; si se arquea más, la malla se parte ahí (crestas de talud, pies y lomos de las rampas).
    private const float ArqueoMaximo = 0.01f, HundidoMaximo = 0.02f;
    /// Lo que se hunde la parte de una calzada o de un acceso que queda debajo de otra superficie (una plaza, una
    /// calzada con más prioridad, la acera): así no parpadean aunque el terreno no sea plano.
    private const float HundidoBajoOtra = 0.06f;
    /// Talud a partir del que una plaza no se pavimenta: el pavimento acaba en el borde del talud (con faldón) en
    /// vez de colgarse por él. Se avisa en el informe.
    private const float PendienteMaximaDePlaza = 38f;

    // ── Ganchos del vestido ──────────────────────────────────────────────────────────────────

    static partial void PintarSueloDePavimento(Lienzo l, List<string> informe)
    {
        Scene escena = l.Terreno.gameObject.scene;
        HashSet<string> giradas = null;
        foreach (Pueblo pueblo in Pueblos())
        {
            if (!pueblo.Pavimentado) continue;
            giradas ??= CasasGiradas(escena);
            PlanoDePavimento plano = PlanearPavimento(pueblo, PuertasDelPueblo(escena, pueblo, giradas),
                (puerta, llega) => AccesoDespejado(escena, l.Altura, puerta, llega));
            int celdas = PintarBajoElPavimento(l, plano);
            informe.Add($"Suelo bajo el pavimento de {pueblo.Nombre}: {celdas} celdas de tierra con piedras y un margen de tierra de " +
                        $"{MargenDeTierra:0.0} m (sin hierba de detalle); las zonas verdes se quedan con su hierba.");
        }
    }

    static partial void PonerPavimento(Obra o)
    {
        Scene escena = o.Raiz.gameObject.scene;
        var materiales = new Material[MaterialDeLosa.Length];
        for (int k = 0; k < materiales.Length; k++)
        {
            string ruta = MaterialesDeSuelo + MaterialDeLosa[k] + ".mat";
            materiales[k] = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (materiales[k] == null) o.Informe.Add($"  ! falta el material {ruta}: el pavimento sale sin esa losa.");
        }

        TerrainData datos = o.Terreno.terrainData;
        Vector3 origen = o.Terreno.transform.position, tamano = datos.size;
        HashSet<string> giradas = null;
        foreach (Pueblo pueblo in Pueblos())
        {
            if (!pueblo.Pavimentado) continue;
            giradas ??= CasasGiradas(escena);
            PlanoDePavimento plano = PlanearPavimento(pueblo, PuertasDelPueblo(escena, pueblo, giradas),
                (puerta, llega) => AccesoDespejado(escena, o.Suelo.Altura, puerta, llega));
            // Las piezas de la capital ya están puestas: los bordillos de plaza se abren junto a ellas.
            Physics.SyncTransforms();
            var p = new Pavimentador
            {
                Plano = plano,
                Altura = (x, z) => o.Suelo.Altura(x, z),
                Pendiente = (x, z) => o.Suelo.Pendiente(x, z),
                Normal = (x, z) => datos.GetInterpolatedNormal(Mathf.Clamp01((x - origen.x) / tamano.x), Mathf.Clamp01((z - origen.z) / tamano.z)),
                Obstaculo = (borde, fuera) => HayObraJunto(o, borde, fuera),
            };
            p.Construir();

            Transform grupo = Grupo(Grupo(o.Raiz, pueblo.Nombre), "Pavimento");
            int vertices = 0, triangulos = 0;
            foreach (MallaDeLosas m in p.Piezas)
            {
                GameObject go = InstanciarPavimento(m, materiales, grupo, pueblo.Nombre);
                if (go == null) continue;
                o.SinObstaculo.Add(go.transform);
                vertices += m.Vertices.Count;
                foreach (List<int> t in m.Triangulos) triangulos += t.Count / 3;
            }
            if (grupo.lossyScale != Vector3.one || grupo.rotation != Quaternion.identity)
                o.Informe.Add($"  ! «{grupo.name}» cuelga de algo girado o escalado: el pavimento puede no coincidir con el terreno.");
            o.Informe.Add(p.Resumen(pueblo.Nombre, vertices, triangulos));
            o.Informe.AddRange(p.Detalle);
        }
    }

    /// Crea el objeto de una pieza de pavimento con su malla guardada en CarpetaGenerada. Los vértices van en
    /// coordenadas del mundo menos el pivote (el centro de la pieza), y el objeto en ese pivote sin giro.
    private static GameObject InstanciarPavimento(MallaDeLosas m, Material[] materiales, Transform grupo, string pueblo)
    {
        var losas = new List<int>();
        for (int k = 0; k < m.Triangulos.Length; k++)
            if (m.Triangulos[k].Count > 0 && materiales[k] != null) losas.Add(k);
        if (losas.Count == 0) return null;

        Vector3 min = m.Vertices[0], max = m.Vertices[0];
        foreach (Vector3 v in m.Vertices) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
        var pivote = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);
        var locales = new List<Vector3>(m.Vertices.Count);
        foreach (Vector3 v in m.Vertices) locales.Add(v - pivote);

        var malla = new Mesh { name = $"Pavimento de {pueblo} — {m.Nombre}" };
        if (locales.Count > 65000) malla.indexFormat = IndexFormat.UInt32;
        malla.SetVertices(locales);
        malla.SetNormals(m.Normales);
        malla.SetUVs(0, m.Uvs);
        malla.subMeshCount = losas.Count;
        var usados = new Material[losas.Count];
        for (int i = 0; i < losas.Count; i++)
        {
            malla.SetTriangles(m.Triangulos[losas[i]], i);
            usados[i] = materiales[losas[i]];
        }
        malla.RecalculateBounds();
        GuardarMallaGenerada(malla, malla.name);

        var go = new GameObject(m.Nombre);
        go.transform.SetParent(grupo, false);
        go.transform.SetPositionAndRotation(pivote, Quaternion.identity);
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        var render = go.AddComponent<MeshRenderer>();
        render.sharedMaterials = usados;
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = true;
        return go;
    }

    /// Si junto al borde de una plaza, por fuera, hay obra que llega al suelo (escalinata, muro, casa,
    /// balaustrada, la fachada del castillo): ahí el bordillo sobra. Se mira una caja de 2 m a lo largo del
    /// borde, de 0,25 a 1,25 m hacia fuera y de 0,2 a 2 m sobre el suelo. No cuenta el atrezo suelto (menos de
    /// 2 m en planta: farolas, estandartes, barriles) ni los volúmenes enormes (zonas de ambiente, agua).
    private static bool HayObraJunto(Obra o, Vector2 borde, Vector2 fuera)
    {
        Vector2 c = borde + fuera * 0.75f;
        float suelo = o.Suelo.Altura(c.x, c.y);
        var centro = new Vector3(c.x, suelo + 1.1f, c.y);
        Quaternion giro = Quaternion.LookRotation(new Vector3(fuera.x, 0f, fuera.y), Vector3.up);
        int n = Physics.OverlapBoxNonAlloc(centro, new Vector3(1f, 0.9f, 0.5f), o.Buffer, giro, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider col = o.Buffer[i];
            if (col == null || col is TerrainCollider || col.isTrigger) continue;
            if (col.gameObject.scene != o.Raiz.gameObject.scene) continue;
            Vector3 t = col.bounds.size;
            if (t.x > 60f || t.z > 60f || Mathf.Max(t.x, t.z) < 2f) continue;
            return true;
        }
        return false;
    }

    private static readonly Collider[] BufferDeAccesos = new Collider[32];

    /// Si entre la puerta y el borde de lo pavimentado no se cruza obra de la escena ni una casa nueva (casas,
    /// muros: lo que mide 2 m o más en planta). Las demás piezas del vestido no cuentan: la pintura del suelo y
    /// las mallas, que van en momentos distintos de la ejecución, tienen que dar los mismos accesos.
    private static bool AccesoDespejado(Scene escena, System.Func<float, float, float> altura, Vector2 puerta, Vector2 llega)
    {
        Vector2 d = llega - puerta;
        float largo = d.magnitude;
        if (largo < 0.3f) return true;
        Vector2 c = (puerta + llega) * 0.5f;
        var centro = new Vector3(c.x, altura(c.x, c.y) + 1.4f, c.y);
        var medio = new Vector3(AnchoDeAcceso * 0.5f - 0.3f, 1.1f, largo * 0.5f);
        Physics.SyncTransforms();
        int n = Physics.OverlapBoxNonAlloc(centro, medio, BufferDeAccesos, Quaternion.LookRotation(new Vector3(d.x, 0f, d.y), Vector3.up),
            ~0, QueryTriggerInteraction.Ignore);
        Transform generado = BuscarRaiz(escena);
        for (int i = 0; i < n; i++)
        {
            Collider col = BufferDeAccesos[i];
            if (col == null || col is TerrainCollider || col.isTrigger || col.gameObject.scene != escena) continue;
            Vector3 t = col.bounds.size;
            if (t.x > 60f || t.z > 60f || Mathf.Max(t.x, t.z) < 2f) continue;
            if (generado != null && col.transform.IsChildOf(generado) && !EsDeCasasNuevas(col.transform, generado)) continue;
            return false;
        }
        return true;
    }

    private static bool EsDeCasasNuevas(Transform t, Transform raiz)
    {
        for (; t != null && t != raiz; t = t.parent)
            if (t.name == GrupoCasasNuevas) return true;
        return false;
    }

    /// Tierra con piedras debajo del pavimento y tierra en un margen de MargenDeTierra alrededor; todo, suelo
    /// duro (sin hierba de detalle). Las zonas verdes y los taludes no se tocan. Devuelve cuántas celdas ha pintado.
    private static int PintarBajoElPavimento(Lienzo l, PlanoDePavimento plano)
    {
        if (!plano.Limites(out Rect caja)) return 0;
        int piedras = l.Capa(CapaTierraPiedras), tierra = l.Capa(CapaTierra);
        float alcance = MargenDeTierra + 1.3f;
        int n = 0;
        l.Recorrer(caja.xMin - alcance, caja.yMin - alcance, caja.xMax + alcance, caja.yMax + alcance, (i, k, x, z) =>
        {
            var p = new Vector2(x, z);
            // Las zonas verdes y los taludes que el pavimento rodea se quedan con su pintura.
            if (plano.EnVerde(p) || l.Pendiente(x, z) > PendienteMaximaDePlaza) return;
            float d = plano.DistanciaAlPavimento(p);
            float s = 1f - Ruido.Suave(MargenDeTierra, alcance, d);
            if (s <= 0f) return;
            l.MezclarPar(i, k, piedras, tierra, s, Mathf.Lerp(0.25f, 1f, Ruido.Suave(0f, MargenDeTierra, d)));
            if (s >= 0.45f) l.Duro[k, i] = true;
            n++;
        });
        return n;
    }

    // ── Plano: calles, plazas y accesos de un pueblo, sin geometría ──────────────────────────

    /// Cómo acaba una calle por cada extremo.
    private enum Remate
    {
        /// Fondo de saco (bordillo y acera de remate) o, en el paso de la puerta, umbral.
        Libre,
        /// Entra en una plaza: la calzada sigue por debajo de la plaza y lo demás acaba en su borde.
        Plaza,
        /// Acaba en medio de otra calle: llega hasta su eje y la otra le abre el bordillo.
        Cruce,
        /// Esquina en L con otra calle de menos prioridad: llega hasta el borde de fuera de la otra y se remata.
        EsquinaQueGana,
        /// Esquina en L con otra de más prioridad: llega hasta su eje, como en un cruce.
        EsquinaQuePierde,
    }

    /// Una calle pavimentada: el trazado alargado por los extremos y su sección. La sección se mide con el
    /// lateral w (izquierda positiva) y el recorrido s. Rango 0: calzada; 1: calzada y bordillos; 2: todo el
    /// ancho. En el paso de la puerta los tres rangos son todo el ancho.
    private sealed class ViaPavimentada
    {
        public int Indice;
        public Vector2[] Trazado;
        public Vector2[] P;
        public float[] S;
        public float L;
        /// Ancho total, semiancho de la calzada, acera y bordillo.
        public float W, C, A, B;
        public bool Paso;
        /// Prioridad en los cruces (0 = la primera).
        public int Orden;
        public Remate Inicio, Fin;
        public float AlargueInicio, AlargueFin;
        public ViaPavimentada OtraInicio, OtraFin;
        /// Dirección del corte del extremo cuando no es perpendicular (esquina que gana: paralelo a la otra calle,
        /// para que el bordillo de remate siga recto el de la otra). Cero: perpendicular.
        public Vector2 CorteInicio, CorteFin;
        /// Recorrido que ocupa cada rango (los remates lo recortan).
        public readonly float[] Desde = new float[3], Hasta = new float[3];
        /// Estaciones de control: recorrido, centro y vector lateral (con inglete en los quiebros, así los bordes
        /// son rectas paralelas al eje que se cortan en la bisectriz).
        private float[] es;
        private Vector2[] ec, ev;

        public float Semiancho(int rango) => Paso ? W * 0.5f : rango == 0 ? C : rango == 1 ? C + B : W * 0.5f;
        public bool TapaInicio => !Paso && Desde[2] < Desde[1];
        public bool TapaFin => !Paso && Hasta[2] > Hasta[1];
        public bool UmbralInicio => Paso && Inicio == Remate.Libre;
        public bool UmbralFin => Paso && Fin == Remate.Libre;
        public float[] Estaciones => es;

        public void Montar(Vector2[] puntos)
        {
            P = puntos;
            int n = P.Length;
            S = new float[n];
            for (int j = 1; j < n; j++) S[j] = S[j - 1] + Vector2.Distance(P[j - 1], P[j]);
            L = S[n - 1];
            var s = new List<float>();
            var c = new List<Vector2>();
            var v = new List<Vector2>();
            // Extremo con corte sesgado: la sección es paralela al corte en todo el remate (bordillo y acera) y
            // vuelve a ser perpendicular un metro más allá; los bordes siguen rectos.
            float tapa = A + B;
            Vector2 n0 = Normal(0), n1 = Normal(n - 2);
            Vector2 sesgo0 = Sesgado(CorteInicio, n0), sesgo1 = Sesgado(CorteFin, n1);
            // Hace falta sitio en el tramo para el remate y la vuelta a la sección recta antes del primer quiebro.
            float sitio = 2f * (tapa + 1f) + 0.1f;
            bool sesgaInicio = sesgo0 != n0 && S[1] > sitio, sesgaFin = sesgo1 != n1 && L - S[n - 2] > sitio;
            s.Add(0f); c.Add(P[0]); v.Add(sesgaInicio ? sesgo0 : n0);
            if (sesgaInicio)
            {
                Vector2 d0 = Dir(0);
                s.Add(tapa); c.Add(P[0] + d0 * tapa); v.Add(sesgo0);
                s.Add(tapa + 1f); c.Add(P[0] + d0 * (tapa + 1f)); v.Add(n0);
            }
            for (int j = 1; j < n - 1; j++)
            {
                Vector2 na = Normal(j - 1), nb = Normal(j);
                float coseno = Mathf.Clamp(Vector2.Dot(na, nb), -0.8f, 1f);
                Vector2 inglete = (na + nb) / (1f + coseno);
                // Junto al quiebro, las filas de vértices del lado de dentro se cruzarían: se dejan solo la del
                // inglete y las de los márgenes.
                float margen = Mathf.Min((W * 0.5f + 0.2f) * Mathf.Tan(Mathf.Acos(coseno) * 0.5f),
                    0.45f * Mathf.Min(S[j] - S[j - 1], S[j + 1] - S[j]));
                if (margen > 0.01f) { s.Add(S[j] - margen); c.Add(P[j] - Dir(j - 1) * margen); v.Add(na); }
                s.Add(S[j]); c.Add(P[j]); v.Add(inglete);
                if (margen > 0.01f) { s.Add(S[j] + margen); c.Add(P[j] + Dir(j) * margen); v.Add(nb); }
            }
            if (sesgaFin)
            {
                Vector2 d1 = Dir(n - 2);
                s.Add(L - tapa - 1f); c.Add(P[n - 1] - d1 * (tapa + 1f)); v.Add(n1);
                s.Add(L - tapa); c.Add(P[n - 1] - d1 * tapa); v.Add(sesgo1);
            }
            s.Add(L); c.Add(P[n - 1]); v.Add(sesgaFin ? sesgo1 : n1);
            es = s.ToArray();
            ec = c.ToArray();
            ev = v.ToArray();
        }

        private Vector2 Dir(int j) => (P[j + 1] - P[j]).normalized;

        /// Vector lateral de un extremo cortado en la dirección «corte» (con componente 1 sobre la normal).
        private static Vector2 Sesgado(Vector2 corte, Vector2 normal)
        {
            if (corte.sqrMagnitude < 1e-6f) return normal;
            float k = Vector2.Dot(corte.normalized, normal);
            return Mathf.Abs(k) < 0.5f ? normal : corte.normalized / k;
        }

        private Vector2 Normal(int j)
        {
            Vector2 d = Dir(j);
            return new Vector2(-d.y, d.x);
        }

        /// Punto en planta a recorrido s y lateral w.
        public Vector2 Punto(float s, float w)
        {
            int k = 0, hi = es.Length - 1;
            if (s >= es[hi]) k = hi - 1;
            else if (s > es[0])
            {
                int lo = 0;
                while (hi - lo > 1)
                {
                    int m = (lo + hi) / 2;
                    if (es[m] <= s) lo = m;
                    else hi = m;
                }
                k = lo;
            }
            float t = es[k + 1] > es[k] ? (s - es[k]) / (es[k + 1] - es[k]) : 0f;
            return Vector2.LerpUnclamped(ec[k], ec[k + 1], t) + Vector2.LerpUnclamped(ev[k], ev[k + 1], t) * w;
        }

        /// Dirección del tramo del trazado en el recorrido s.
        public Vector2 Direccion(float s)
        {
            for (int j = 0; j < P.Length - 2; j++)
                if (s <= S[j + 1]) return Dir(j);
            return Dir(P.Length - 2);
        }

        public Vector2 NormalEn(float s)
        {
            Vector2 d = Direccion(s);
            return new Vector2(-d.y, d.x);
        }

        /// Recorrido y lateral del punto respecto a la calle. Falso si cae más allá de los extremos (más de
        /// «holgura» metros).
        public bool Proyectar(Vector2 p, float holgura, out float s, out float lat)
        {
            s = lat = 0f;
            float mejor = float.MaxValue;
            bool dentro = false;
            int ultimo = P.Length - 2;
            for (int j = 0; j <= ultimo; j++)
            {
                Vector2 a = P[j], d = P[j + 1] - a;
                float largo = d.magnitude;
                if (largo < 1e-4f) continue;
                d /= largo;
                var n = new Vector2(-d.y, d.x);
                float t = Vector2.Dot(p - a, d);
                bool antes = j == 0 && t < 0f, despues = j == ultimo && t > largo;
                float tc = antes || despues ? t : Mathf.Clamp(t, 0f, largo);
                Vector2 q = a + d * tc;
                float dist = antes || despues ? Mathf.Abs(Vector2.Dot(p - q, n)) + (antes ? -t : t - largo) : Vector2.Distance(p, q);
                if (dist >= mejor) continue;
                mejor = dist;
                s = S[j] + tc;
                if (antes || despues)
                {
                    lat = Vector2.Dot(p - q, n);
                    dentro = (antes ? -t : t - largo) <= holgura;
                }
                else
                {
                    lat = tc > 0f && tc < largo ? Vector2.Dot(p - q, n) : Mathf.Sign(Vector2.Dot(p - q, n)) * Vector2.Distance(p, q);
                    dentro = true;
                }
            }
            return dentro;
        }

        /// Si el punto cae en el rango dado de la calle (con sus remates), ensanchado «margen» metros (negativo:
        /// solo lo que queda claramente dentro).
        public bool EnRegion(Vector2 p, int rango, float margen = 1e-4f)
        {
            if (!Proyectar(p, 0f, out float s, out float lat)) return false;
            return s >= Desde[rango] - margen && s <= Hasta[rango] + margen && Mathf.Abs(lat) <= Semiancho(rango) + margen;
        }
    }

    /// Una plaza pavimentada: rectángulo girado con su marco, su campo y, si lo tiene, el rosetón y el eje.
    private sealed class PlazaPavimentada
    {
        public int Indice;
        public Vector2 Centro, Ex, Ez;
        public float Hx, Hz;
        public Losa Campo, Marco;
        public bool ConMarco;
        public float Roseton;
        /// El eje de losas corre a lo largo de la z local (la dirección corta); si no, de la x.
        public bool EjeEnZ;

        public Vector2 Local(Vector2 p)
        {
            Vector2 d = p - Centro;
            return new Vector2(Vector2.Dot(d, Ex), Vector2.Dot(d, Ez));
        }

        public Vector2 Mundo(float lx, float lz) => Centro + Ex * lx + Ez * lz;

        public bool Contiene(Vector2 p, float margen = 0f)
        {
            Vector2 q = Local(p);
            return Mathf.Abs(q.x) <= Hx + margen && Mathf.Abs(q.y) <= Hz + margen;
        }

        public float Distancia(Vector2 p)
        {
            Vector2 q = Local(p);
            float fx = Mathf.Max(Mathf.Abs(q.x) - Hx, 0f), fz = Mathf.Max(Mathf.Abs(q.y) - Hz, 0f);
            return Mathf.Sqrt(fx * fx + fz * fz);
        }
    }

    /// Acceso de una puerta: de debajo de la casa (Desde) a lo pavimentado (Hacia), a lo largo de Dir.
    private struct AccesoPavimentado
    {
        public Vector2 Desde, Dir, Lado;
        public float Largo;
        public Vector2 Hacia => Desde + Dir * Largo;

        public bool Contiene(Vector2 p)
        {
            Vector2 d = p - Desde;
            float a = Vector2.Dot(d, Dir);
            return a >= 0f && a <= Largo && Mathf.Abs(Vector2.Dot(d, Lado)) <= AnchoDeAcceso * 0.5f;
        }
    }

    private sealed class PlanoDePavimento
    {
        public readonly List<ViaPavimentada> Vias = new();
        public readonly List<PlazaPavimentada> Plazas = new();
        public readonly List<AccesoPavimentado> Accesos = new();
        public Rect[] Verdes = new Rect[0];
        public Vector2[] Recinto;

        public bool EnVerde(Vector2 p)
        {
            foreach (Rect r in Verdes)
                if (r.Contains(p)) return true;
            return false;
        }

        public bool FueraDelRecinto(Vector2 p) => Recinto != null && Recinto.Length >= 3 && !DentroDePoligono(p, Recinto);

        /// Si el punto está pavimentado por una calle (todo su ancho) o una plaza (con «margen», como EnRegion).
        public bool EsPavimento(Vector2 p, float margen = 1e-4f)
        {
            if (EnVerde(p)) return false;
            foreach (PlazaPavimentada pl in Plazas)
                if (pl.Contiene(p, margen)) return true;
            foreach (ViaPavimentada v in Vias)
                if (v.EnRegion(p, 2, margen)) return true;
            return false;
        }

        /// Si junto a una plaza, por fuera, el suelo sigue pavimentado a ras (otra plaza, una calzada, el paso de
        /// la puerta o un acceso): ahí la plaza no lleva bordillo.
        public bool PavimentoAlLado(Vector2 p, PlazaPavimentada propia)
        {
            if (EnVerde(p)) return false;
            foreach (PlazaPavimentada pl in Plazas)
                if (pl != propia && pl.Contiene(p)) return true;
            foreach (ViaPavimentada v in Vias)
                if (v.EnRegion(p, 0)) return true;
            foreach (AccesoPavimentado a in Accesos)
                if (a.Contiene(p)) return true;
            return false;
        }

        /// Distancia en planta al pavimento (0 encima).
        public float DistanciaAlPavimento(Vector2 p)
        {
            float d = float.MaxValue;
            foreach (ViaPavimentada v in Vias) d = Mathf.Min(d, DistanciaAPolilinea(p, v.P) - v.W * 0.5f);
            foreach (PlazaPavimentada pl in Plazas) d = Mathf.Min(d, pl.Distancia(p));
            foreach (AccesoPavimentado a in Accesos) d = Mathf.Min(d, DistanciaASegmento(p, a.Desde, a.Hacia) - AnchoDeAcceso * 0.5f);
            return Mathf.Max(0f, d);
        }

        public bool Limites(out Rect caja)
        {
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            void Sumar(Vector2 q, float r)
            {
                x0 = Mathf.Min(x0, q.x - r); x1 = Mathf.Max(x1, q.x + r);
                z0 = Mathf.Min(z0, q.y - r); z1 = Mathf.Max(z1, q.y + r);
            }
            foreach (ViaPavimentada v in Vias) foreach (Vector2 q in v.P) Sumar(q, v.W * 0.5f);
            foreach (PlazaPavimentada pl in Plazas) Sumar(pl.Centro, Mathf.Sqrt(pl.Hx * pl.Hx + pl.Hz * pl.Hz));
            foreach (AccesoPavimentado a in Accesos) { Sumar(a.Desde, AnchoDeAcceso); Sumar(a.Hacia, AnchoDeAcceso); }
            caja = Rect.MinMaxRect(x0, z0, x1, z1);
            return x0 <= x1;
        }
    }

    private static Losa LosaDeCapa(string capa) => capa == CapaBaldosa ? Losa.Baldosa : Losa.Adoquin;

    /// Plano del pavimento de un pueblo: calles con sus remates y prioridades, plazas y accesos de las puertas.
    /// «despejado(puerta, llegada)» dice si entre la puerta y lo pavimentado no se cruza otra obra (null: siempre).
    private static PlanoDePavimento PlanearPavimento(Pueblo pueblo, List<Puerta> puertas, System.Func<Vector2, Vector2, bool> despejado = null)
    {
        var plano = new PlanoDePavimento { Verdes = pueblo.Verdes ?? new Rect[0], Recinto = pueblo.Recinto };

        for (int i = 0; i < pueblo.Plazas.Length; i++)
        {
            Plaza pl = pueblo.Plazas[i];
            float rad = pl.Grados * Mathf.Deg2Rad, cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            var p = new PlazaPavimentada
            {
                Indice = i, Centro = pl.Centro, Ex = new Vector2(cs, -sn), Ez = new Vector2(sn, cs),
                Hx = pl.Tamano.x * 0.5f, Hz = pl.Tamano.y * 0.5f,
                Campo = LosaDeCapa(pl.Capa), Marco = LosaDeCapa(pl.Marco), Roseton = pl.Roseton,
            };
            p.ConMarco = p.Marco != p.Campo;
            p.EjeEnZ = p.Hz <= p.Hx;
            plano.Plazas.Add(p);
        }

        for (int i = 0; i < pueblo.Calles.Length; i++)
        {
            Vector2[] trazado = SinPuntosRepetidos(pueblo.Calles[i]);
            if (trazado.Length < 2) continue;
            var v = new ViaPavimentada { Indice = i, Trazado = trazado, W = pueblo.AnchoDeCalle(i) };
            v.Paso = plano.Recinto != null && plano.Recinto.Length >= 3 && System.Array.Exists(trazado, q => !DentroDePoligono(q, plano.Recinto));
            v.A = !v.Paso && v.W >= AnchoMinimoConAceras ? AnchoDeAcera : 0f;
            v.B = !v.Paso && v.W >= 2.5f ? AnchoDeBordillo : 0f;
            v.C = v.Paso ? v.W * 0.5f : v.W * 0.5f - v.A - v.B;
            v.Montar(trazado);
            plano.Vias.Add(v);
        }

        // Prioridad: el paso de la puerta, después las más anchas y, a igual ancho, por orden en el pueblo.
        var orden = new List<ViaPavimentada>(plano.Vias);
        orden.Sort((a, b) => a.Paso != b.Paso ? (a.Paso ? -1 : 1) : !Mathf.Approximately(a.W, b.W) ? b.W.CompareTo(a.W) : a.Indice.CompareTo(b.Indice));
        for (int k = 0; k < orden.Count; k++) orden[k].Orden = k;

        // Remates con los trazados de partida; después se alargan todas a la vez.
        foreach (ViaPavimentada v in plano.Vias)
        {
            v.Inicio = Rematar(plano, v, false, out v.AlargueInicio, out v.OtraInicio, out v.CorteInicio);
            v.Fin = Rematar(plano, v, true, out v.AlargueFin, out v.OtraFin, out v.CorteFin);
        }
        foreach (ViaPavimentada v in plano.Vias)
        {
            v.Montar(Alargar(v.Trazado, v.AlargueInicio, v.AlargueFin));
            AjustarRangos(v);
        }

        // Accesos de las puertas: recto desde la puerta hasta lo pavimentado que tiene delante.
        var huellas = new List<Huella>();
        foreach (Puerta puerta in puertas)
        {
            if (puerta.Frente.sqrMagnitude < 0.25f) continue;
            Vector2 f = puerta.Frente.normalized, lado = new Vector2(f.y, -f.x);
            Vector2 desde = puerta.Pos - f * (1.2f + FondoBajoLaCasa);
            float tope = 1.2f + FondoBajoLaCasa + LargoMaximoDeAcceso, llega = -1f;
            for (float t = 0f; t <= tope; t += 0.1f)
            {
                Vector2 q = desde + f * t;
                if (plano.EnVerde(q) || plano.FueraDelRecinto(q)) break;
                if (!plano.EsPavimento(q)) continue;
                float a = Mathf.Max(0f, t - 0.1f), b = t;
                for (int k = 0; k < 12; k++)
                {
                    float m = (a + b) * 0.5f;
                    if (plano.EsPavimento(desde + f * m)) b = m;
                    else a = m;
                }
                llega = b;
                break;
            }
            // Sin pavimento delante, o la casa ya está encima de él: no hace falta acceso. Tampoco si por el camino
            // hay otra casa o un muro.
            if (llega < FondoBajoLaCasa + 0.3f) continue;
            if (despejado != null && !despejado(puerta.Pos, desde + f * llega)) continue;
            var acceso = new AccesoPavimentado { Desde = desde, Dir = f, Lado = lado, Largo = llega + MetidoEnLoPavimentado };
            var huella = new Huella
            {
                Centro = desde + f * (acceso.Largo * 0.5f), EjeX = lado, EjeZ = f,
                MedioX = AnchoDeAcceso * 0.5f, MedioZ = acceso.Largo * 0.5f,
            };
            bool solapa = false;
            foreach (Huella h in huellas) if (h.Solapa(huella)) { solapa = true; break; }
            if (solapa) continue;
            huellas.Add(huella);
            plano.Accesos.Add(acceso);
        }
        return plano;
    }

    private static Vector2[] SinPuntosRepetidos(Vector2[] puntos)
    {
        var r = new List<Vector2>();
        foreach (Vector2 q in puntos)
            if (r.Count == 0 || Vector2.Distance(r[r.Count - 1], q) > 0.01f) r.Add(q);
        return r.ToArray();
    }

    private static Vector2[] Alargar(Vector2[] puntos, float alInicio, float alFin)
    {
        var r = (Vector2[])puntos.Clone();
        int n = r.Length;
        r[0] -= (puntos[1] - puntos[0]).normalized * alInicio;
        r[n - 1] += (puntos[n - 1] - puntos[n - 2]).normalized * alFin;
        return r;
    }

    /// Remate de un extremo de la calle «a» (con los trazados sin alargar), cuánto hay que alargarla por él y,
    /// en una esquina que gana, la dirección del corte (la de la otra calle).
    private static Remate Rematar(PlanoDePavimento plano, ViaPavimentada a, bool fin, out float alargue, out ViaPavimentada otra, out Vector2 corte)
    {
        alargue = 0f;
        otra = null;
        corte = Vector2.zero;
        Vector2[] t = a.Trazado;
        int n = t.Length;
        Vector2 e = fin ? t[n - 1] : t[0];
        Vector2 fuera = fin ? (t[n - 1] - t[n - 2]).normalized : (t[0] - t[1]).normalized;
        if (a.Paso && plano.FueraDelRecinto(e)) return Remate.Libre;
        foreach (PlazaPavimentada pl in plano.Plazas)
            if (pl.Contiene(e, 0.5f)) { alargue = EntraEnLaPlaza; return Remate.Plaza; }

        float mejor = float.MaxValue, sOtra = 0f, latOtra = 0f;
        foreach (ViaPavimentada b in plano.Vias)
        {
            if (b == a || !b.Proyectar(e, 0.5f, out float s, out float lat) || Mathf.Abs(lat) > b.W * 0.5f + 0.5f) continue;
            if (Mathf.Abs(lat) >= mejor) continue;
            mejor = Mathf.Abs(lat);
            otra = b;
            sOtra = s;
            latOtra = lat;
        }
        if (otra == null) return Remate.Libre;

        float avance = Vector2.Dot(fuera, otra.NormalEn(sOtra));
        // Esquina: el extremo cae junto a un extremo de la otra y ese extremo cae dentro de esta calle.
        bool juntoAlInicio = sOtra <= a.W * 0.5f + 1f, juntoAlFin = otra.L - sOtra <= a.W * 0.5f + 1f;
        bool esquina = false;
        if ((juntoAlInicio || juntoAlFin) && Mathf.Abs(avance) >= 0.5f)
        {
            Vector2 suyo = juntoAlInicio ? otra.Trazado[0] : otra.Trazado[otra.Trazado.Length - 1];
            esquina = a.Proyectar(suyo, 0.5f, out _, out float latSuyo) && Mathf.Abs(latSuyo) <= a.W * 0.5f + 0.5f;
        }
        if (esquina && a.Orden < otra.Orden)
        {
            alargue = Mathf.Clamp((Mathf.Sign(avance) * otra.W * 0.5f - latOtra) / avance, 0f, otra.W + 2f);
            corte = otra.Direccion(sOtra);
            return Remate.EsquinaQueGana;
        }
        if (Mathf.Abs(avance) >= 0.3f) alargue = Mathf.Clamp(-latOtra / avance, 0f, otra.W * 0.5f + 1f);
        return esquina ? Remate.EsquinaQuePierde : Remate.Cruce;
    }

    /// Recorrido de cada rango según los remates: en una plaza, la calzada entra por debajo y el resto acaba
    /// en su borde; en un fondo de saco o una esquina que gana, la calzada acaba antes y la cierran un bordillo
    /// y una acera de remate.
    private static void AjustarRangos(ViaPavimentada v)
    {
        float tapa = v.A + v.B;
        for (int r = 0; r < 3; r++) { v.Desde[r] = 0f; v.Hasta[r] = v.L; }
        if (v.Paso) return;
        if (v.Inicio == Remate.Plaza) v.Desde[1] = v.Desde[2] = v.AlargueInicio;
        else if ((v.Inicio == Remate.Libre || v.Inicio == Remate.EsquinaQueGana) && tapa > 0f) { v.Desde[0] = tapa; v.Desde[1] = v.A; }
        if (v.Fin == Remate.Plaza) v.Hasta[1] = v.Hasta[2] = v.L - v.AlargueFin;
        else if ((v.Fin == Remate.Libre || v.Fin == Remate.EsquinaQueGana) && tapa > 0f) { v.Hasta[0] = v.L - tapa; v.Hasta[1] = v.L - v.A; }
    }

    // ── Geometría ────────────────────────────────────────────────────────────────────────────

    /// Malla de una pieza (calle, plaza o accesos): una submalla por losa.
    private sealed class MallaDeLosas
    {
        public readonly string Nombre;
        public readonly List<Vector3> Vertices = new();
        public readonly List<Vector3> Normales = new();
        public readonly List<Vector2> Uvs = new();
        public readonly List<int>[] Triangulos = { new(), new(), new(), new(), new() };

        public MallaDeLosas(string nombre) { Nombre = nombre; }

        public int Vertice(Vector3 p, Vector3 n, Vector2 uv)
        {
            Vertices.Add(p);
            Normales.Add(n);
            Uvs.Add(uv);
            return Vertices.Count - 1;
        }

        /// Triángulo con la cara hacia «cara» (el orden de los vértices se corrige solo). Los degenerados se omiten.
        public void Triangulo(Losa losa, int a, int b, int c, Vector3 cara)
        {
            Vector3 n = Vector3.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
            if (n.sqrMagnitude < 1e-10f) return;
            List<int> t = Triangulos[(int)losa];
            t.Add(a);
            if (Vector3.Dot(n, cara) >= 0f) { t.Add(b); t.Add(c); }
            else { t.Add(c); t.Add(b); }
        }

        /// Cuadrilátero a-b-c-d (en orden alrededor).
        public void Cuadrilatero(Losa losa, int a, int b, int c, int d, Vector3 cara)
        {
            Triangulo(losa, a, b, c, cara);
            Triangulo(losa, a, c, d, cara);
        }

        public bool Vacia
        {
            get
            {
                foreach (List<int> t in Triangulos) if (t.Count > 0) return false;
                return true;
            }
        }
    }

    /// Clase de cada celda de una plaza.
    private enum Celda : byte { Fuera, Campo, Marco, Eje, CintaDeEje, Bordillo }

    /// Construye las mallas de un plano sobre el terreno que le dan sus funciones.
    private sealed class Pavimentador
    {
        public PlanoDePavimento Plano;
        public System.Func<float, float, float> Altura;
        public System.Func<float, float, float> Pendiente;
        public System.Func<float, float, Vector3> Normal;
        /// ¿Hay obra junto al borde? (punto del borde, dirección hacia fuera).
        public System.Func<Vector2, Vector2, bool> Obstaculo;

        public readonly List<MallaDeLosas> Piezas = new();
        public readonly List<string> Detalle = new();
        private float largoDeCalzada, largoDeBordillo, areaDeAcera, areaDePlazas;
        private int bordillosAbiertosPorObra, roseton;

        private float Base(Vector2 p) =>
            Altura(p.x, p.y) + Mathf.Lerp(SobreElTerreno, SobreElTerrenoEnRampa, Ruido.Suave(3f, 10f, Pendiente(p.x, p.y)));

        private float Pie(Vector2 p) => Altura(p.x, p.y) - BajoElTerreno;

        /// Si entre a y b el terreno se arquea más de lo que tapa el pavimento (sobre la recta de a a b asoma más
        /// de ArqueoMaximo, o se hunde más de HundidoMaximo y el pavimento quedaría en el aire): hay que partir.
        private bool Arqueado(Vector2 a, Vector2 b)
        {
            float medio = Altura((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f) - (Altura(a.x, a.y) + Altura(b.x, b.y)) * 0.5f;
            return medio > ArqueoMaximo || medio < -HundidoMaximo;
        }

        /// Parte los intervalos de «cortes» (ordenados) donde, en alguna de las líneas que da «punto(c, k)» para
        /// k en [0, lineas), el terreno se arquea; hasta 5 pasadas y sin bajar de 0,15 m.
        private List<float> Afinar(List<float> cortes, int lineas, System.Func<float, int, Vector2> punto)
        {
            var r = new List<float>(cortes);
            for (int pasada = 0; pasada < 5; pasada++)
            {
                var nuevos = new List<float>();
                for (int i = 0; i < r.Count - 1; i++)
                {
                    float a = r[i], b = r[i + 1];
                    if (b - a < 0.3f) continue;
                    for (int k = 0; k < lineas; k++)
                        if (Arqueado(punto(a, k), punto(b, k))) { nuevos.Add((a + b) * 0.5f); break; }
                }
                if (nuevos.Count == 0) break;
                r.AddRange(nuevos);
                r.Sort();
            }
            return r;
        }

        public void Construir()
        {
            foreach (ViaPavimentada v in Plano.Vias)
            {
                MallaDeLosas m = PavimentarVia(v);
                if (!m.Vacia) Piezas.Add(m);
            }
            foreach (PlazaPavimentada pl in Plano.Plazas)
            {
                MallaDeLosas m = PavimentarPlaza(pl);
                if (!m.Vacia) Piezas.Add(m);
            }
            if (Plano.Accesos.Count > 0)
            {
                var m = new MallaDeLosas("Accesos de las puertas");
                foreach (AccesoPavimentado a in Plano.Accesos) PavimentarAcceso(m, a);
                if (!m.Vacia) Piezas.Add(m);
            }
        }

        public string Resumen(string pueblo, int vertices, int triangulos) =>
            $"Pavimento de {pueblo}: {Plano.Vias.Count} calles ({largoDeCalzada:0} m de calzada, {areaDeAcera:0} m² de acera), " +
            $"{Plano.Plazas.Count} plazas ({areaDePlazas:0} m²" + (roseton > 0 ? $", {roseton} con rosetón y eje de losas" : "") + "), " +
            $"{largoDeBordillo:0} m de bordillo y {Plano.Accesos.Count} accesos de puerta; {Piezas.Count} mallas, {vertices} vértices, " +
            $"{triangulos} triángulos, sin colisor ni sombra." +
            (bordillosAbiertosPorObra > 0 ? $" Bordillo de plaza abierto en {bordillosAbiertosPorObra} celdas por obra al lado (escalinata, muros, casas)." : "");

        // ── Primitivas ───────────────────────────────────────────────────────────────────────

        /// Superficie de filas × columnas de puntos en planta, a la cota Base + capa (más «ajuste» en cada punto,
        /// si lo hay).
        private void Rejilla(MallaDeLosas m, Losa losa, Vector2[,] puntos, Vector2[,] uv, float capa, System.Func<Vector2, float> ajuste = null)
        {
            int filas = puntos.GetLength(0), columnas = puntos.GetLength(1);
            var idx = new int[filas, columnas];
            for (int i = 0; i < filas; i++)
                for (int j = 0; j < columnas; j++)
                {
                    Vector2 q = puntos[i, j];
                    float y = Base(q) + capa + (ajuste != null ? ajuste(q) : 0f);
                    idx[i, j] = m.Vertice(new Vector3(q.x, y, q.y), Normal(q.x, q.y), uv[i, j]);
                }
            for (int i = 0; i < filas - 1; i++)
                for (int j = 0; j < columnas - 1; j++)
                    m.Cuadrilatero(losa, idx[i, j], idx[i + 1, j], idx[i + 1, j + 1], idx[i, j + 1], Vector3.up);
        }

        /// Pared vertical a lo largo de «borde», de la cota Base + capa hasta el pie (bajo el terreno), con la
        /// cara hacia «fuera». La UV la da «uv» (recorrido u, metros bajo el borde de arriba).
        private void Pared(MallaDeLosas m, Losa losa, Vector2[] borde, float[] u, float capa, Vector2 fuera, System.Func<float, float, Vector2> uv)
        {
            var hacia = new Vector2[borde.Length];
            for (int i = 0; i < hacia.Length; i++) hacia[i] = fuera;
            Pared(m, losa, borde, u, capa, hacia, uv);
        }

        /// Igual, con la cara de cada punto hacia «fuera[i]» (frentes de bordillo y faldones en curva).
        private void Pared(MallaDeLosas m, Losa losa, Vector2[] borde, float[] u, float capa, Vector2[] fuera, System.Func<float, float, Vector2> uv)
        {
            if (borde.Length < 2) return;
            var arriba = new int[borde.Length];
            var abajo = new int[borde.Length];
            for (int i = 0; i < borde.Length; i++)
            {
                Vector2 q = borde[i];
                var n3 = new Vector3(fuera[i].x, 0f, fuera[i].y);
                float ya = Base(q) + capa, yb = Pie(q);
                arriba[i] = m.Vertice(new Vector3(q.x, ya, q.y), n3, uv(u[i], 0f));
                abajo[i] = m.Vertice(new Vector3(q.x, yb, q.y), n3, uv(u[i], ya - yb));
            }
            for (int i = 0; i < borde.Length - 1; i++)
            {
                Vector2 f = fuera[i] + fuera[i + 1];
                m.Cuadrilatero(losa, arriba[i], arriba[i + 1], abajo[i + 1], abajo[i], new Vector3(f.x, 0f, f.y));
            }
        }

        private static Vector2 UvDeFrente(float u, float bajo) =>
            new Vector2(u / RepeticionDeBordillo, BandaArista - bajo * (BandaFondo - BandaArista) / AnchoDeBordillo);

        /// Bordillo: caja de sillares entre el borde de dentro (la arista, del lado de la calzada) y el de fuera,
        /// con sus dos frentes y las cabezas en los extremos.
        private void Bordillo(MallaDeLosas m, Vector2[] dentro, Vector2[] fuera, float[] u)
        {
            int n = dentro.Length;
            if (n < 2) return;
            var puntos = new Vector2[n, 2];
            var uv = new Vector2[n, 2];
            for (int i = 0; i < n; i++)
            {
                puntos[i, 0] = dentro[i];
                puntos[i, 1] = fuera[i];
                uv[i, 0] = new Vector2(u[i] / RepeticionDeBordillo, BandaArista);
                uv[i, 1] = new Vector2(u[i] / RepeticionDeBordillo, BandaFondo);
            }
            Rejilla(m, Losa.Piedra, puntos, uv, AltoDeBordillo);
            var haciaDentro = new Vector2[n];
            var haciaFuera = new Vector2[n];
            for (int i = 0; i < n; i++) { haciaDentro[i] = (dentro[i] - fuera[i]).normalized; haciaFuera[i] = -haciaDentro[i]; }
            Pared(m, Losa.Piedra, dentro, u, AltoDeBordillo, haciaDentro, UvDeFrente);
            Pared(m, Losa.Piedra, fuera, u, AltoDeBordillo, haciaFuera, UvDeFrente);
            Vector2 avance = (dentro[n - 1] - dentro[0]).normalized;
            float ancho = Vector2.Distance(dentro[0], fuera[0]);
            Pared(m, Losa.Piedra, new[] { dentro[0], fuera[0] }, new[] { 0f, ancho }, AltoDeBordillo, -avance, UvDeFrente);
            ancho = Vector2.Distance(dentro[n - 1], fuera[n - 1]);
            Pared(m, Losa.Piedra, new[] { dentro[n - 1], fuera[n - 1] }, new[] { 0f, ancho }, AltoDeBordillo, avance, UvDeFrente);
            for (int i = 0; i < n - 1; i++) largoDeBordillo += Vector2.Distance(dentro[i], dentro[i + 1]);
        }

        /// Faja plana a ras (acera, cinta, umbral) entre dos bordes, con faldón por fuera y en las cabezas.
        private void Faja(MallaDeLosas m, Losa losa, Vector2[] dentro, Vector2[] fuera, Vector2[] uvDentro, Vector2[] uvFuera, float capa,
            bool faldonFuera, bool faldonInicio, bool faldonFin, System.Func<Vector2, float> ajuste = null)
        {
            int n = dentro.Length;
            if (n < 2) return;
            var puntos = new Vector2[n, 2];
            var uv = new Vector2[n, 2];
            for (int i = 0; i < n; i++)
            {
                puntos[i, 0] = dentro[i];
                puntos[i, 1] = fuera[i];
                uv[i, 0] = uvDentro[i];
                uv[i, 1] = uvFuera[i];
            }
            Rejilla(m, losa, puntos, uv, capa, ajuste);
            Vector2 avance = (dentro[n - 1] - dentro[0]).normalized;
            System.Func<float, float, Vector2> uvFaldon = (u, bajo) => new Vector2(u / RepeticionDeLosas, bajo / RepeticionDeLosas);
            if (faldonFuera)
            {
                var u = new float[n];
                var haciaFuera = new Vector2[n];
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) u[i] = u[i - 1] + Vector2.Distance(fuera[i - 1], fuera[i]);
                    haciaFuera[i] = (fuera[i] - dentro[i]).normalized;
                }
                Pared(m, losa, fuera, u, capa, haciaFuera, uvFaldon);
            }
            if (faldonInicio) Pared(m, losa, new[] { dentro[0], fuera[0] }, new[] { 0f, Vector2.Distance(dentro[0], fuera[0]) }, capa, -avance, uvFaldon);
            if (faldonFin) Pared(m, losa, new[] { dentro[n - 1], fuera[n - 1] }, new[] { 0f, Vector2.Distance(dentro[n - 1], fuera[n - 1]) }, capa, avance, uvFaldon);
        }

        // ── Calles ───────────────────────────────────────────────────────────────────────────

        /// Una tira de la sección de la calle (entre los laterales W0 y W1) y los tramos de recorrido que quedan
        /// tras recortarla con lo demás.
        private sealed class Tira
        {
            public float W0, W1;
            public int Rango;
            public Losa Losa;
            public bool EsBordillo, EsAcera, EsCinta;
            public float Desde, Hasta;
            public List<Vector2> Tramos;
        }

        /// ¿Se recorta en este punto la tira de rango «rango» de la calle «a»? La calzada solo se recorta en las
        /// zonas verdes y fuera del recinto (bajo plazas y calzadas de más prioridad pasa por debajo); bordillos y
        /// aceras se recortan además con las plazas y con las otras calles según su prioridad: la que gana pierde
        /// lo que pisa la parte más de dentro de la otra; la que cede, lo que pisa la parte igual o más de dentro.
        private bool Recortado(ViaPavimentada a, int rango, Vector2 p)
        {
            if (Plano.EnVerde(p)) return true;
            if (a.Paso) return false;
            if (Plano.FueraDelRecinto(p)) return true;
            if (rango == 0) return false;
            foreach (PlazaPavimentada pl in Plano.Plazas)
                if (pl.Contiene(p)) return true;
            foreach (ViaPavimentada b in Plano.Vias)
            {
                if (b == a) continue;
                int q = b.Orden < a.Orden ? rango : rango - 1;
                if (q >= 0 && b.EnRegion(p, q)) return true;
            }
            return false;
        }

        /// Tramos de [s0, s1] en los que la función no recorta: muestreo cada 5 cm y bisección en los cortes.
        private static List<Vector2> Tramos(float s0, float s1, System.Func<float, bool> recortado)
        {
            var r = new List<Vector2>();
            if (s1 - s0 < 0.02f) return r;
            int n = Mathf.Max(1, Mathf.CeilToInt((s1 - s0) / 0.05f));
            bool dentro = !recortado(s0);
            float inicio = s0, anterior = s0;
            for (int i = 1; i <= n; i++)
            {
                float s = s0 + (s1 - s0) * i / n;
                bool ahora = !recortado(s);
                if (ahora != dentro)
                {
                    float a = anterior, b = s;
                    for (int k = 0; k < 14; k++)
                    {
                        float m = (a + b) * 0.5f;
                        if (!recortado(m) == dentro) a = m;
                        else b = m;
                    }
                    float corte = (a + b) * 0.5f;
                    if (dentro && corte - inicio > 0.03f) r.Add(new Vector2(inicio, corte));
                    inicio = corte;
                    dentro = ahora;
                }
                anterior = s;
            }
            if (dentro && s1 - inicio > 0.03f) r.Add(new Vector2(inicio, s1));
            return r;
        }

        /// Estaciones comunes a todas las tiras (así los bordes de una tira y de la de al lado comparten vértices):
        /// las de control, los cortes y lo necesario para no pasar de PasoDeMalla.
        private static List<float> Estaciones(IEnumerable<float> fijas, float s0, float s1)
        {
            var e = new List<float>();
            foreach (float s in fijas)
                if (s >= s0 - 1e-4f && s <= s1 + 1e-4f) e.Add(Mathf.Clamp(s, s0, s1));
            e.Add(s0);
            e.Add(s1);
            e.Sort();
            var r = new List<float>();
            foreach (float s in e)
            {
                if (r.Count > 0 && s - r[r.Count - 1] < 0.005f) continue;
                if (r.Count > 0)
                {
                    float a = r[r.Count - 1];
                    int partes = Mathf.CeilToInt((s - a) / PasoDeMalla);
                    for (int k = 1; k < partes; k++) r.Add(a + (s - a) * k / partes);
                }
                r.Add(s);
            }
            return r;
        }

        private static List<float> Entre(List<float> estaciones, float a, float b)
        {
            var r = new List<float>();
            foreach (float s in estaciones)
                if (s >= a - 1e-3f && s <= b + 1e-3f) r.Add(s);
            if (r.Count > 0) { r[0] = a; r[r.Count - 1] = b; }
            return r;
        }

        /// Columnas de la calzada: partes iguales de PasoDeMalla como mucho entre -semiancho y semiancho.
        private static float[] Columnas(float w0, float w1)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt((w1 - w0) / PasoDeMalla));
            var c = new float[n + 1];
            for (int j = 0; j <= n; j++) c[j] = w0 + (w1 - w0) * j / n;
            return c;
        }

        private MallaDeLosas PavimentarVia(ViaPavimentada v)
        {
            var m = new MallaDeLosas($"Calle {v.Indice} ({v.W:0.#} m)");
            const float capaCalzada = 0f;
            float h = v.W * 0.5f;

            // Columnas de la calzada (o del adoquín del paso), partidas donde el terreno se arquea de través.
            var muestras = new List<float>();
            for (float s = 0f; s <= v.L; s += PasoDeMalla) muestras.Add(s);
            muestras.Add(v.L);
            float wc = v.Paso ? h - AnchoDeCintaDePaso : v.C;
            float[] col = Afinar(new List<float>(Columnas(-wc, wc)), muestras.Count, (w, k) => v.Punto(muestras[k], w)).ToArray();

            // Sección.
            var tiras = new List<Tira>();
            if (v.Paso)
            {
                float ini = v.UmbralInicio ? AnchoDeUmbral : 0f, fin = v.UmbralFin ? v.L - AnchoDeUmbral : v.L;
                tiras.Add(new Tira { W0 = -h, W1 = -wc, Losa = Losa.Piedra, EsCinta = true, Desde = ini, Hasta = fin });
                for (int j = 0; j < col.Length - 1; j++)
                    tiras.Add(new Tira { W0 = col[j], W1 = col[j + 1], Losa = Losa.Adoquin, Desde = ini, Hasta = fin });
                tiras.Add(new Tira { W0 = wc, W1 = h, Losa = Losa.Piedra, EsCinta = true, Desde = ini, Hasta = fin });
            }
            else
            {
                for (int j = 0; j < col.Length - 1; j++)
                    tiras.Add(new Tira { W0 = col[j], W1 = col[j + 1], Rango = 0, Losa = Losa.Adoquin, Desde = v.Desde[0], Hasta = v.Hasta[0] });
                if (v.B > 0f)
                {
                    tiras.Add(new Tira { W0 = -v.C - v.B, W1 = -v.C, Rango = 1, Losa = Losa.Piedra, EsBordillo = true, Desde = v.Desde[1], Hasta = v.Hasta[1] });
                    tiras.Add(new Tira { W0 = v.C, W1 = v.C + v.B, Rango = 1, Losa = Losa.Piedra, EsBordillo = true, Desde = v.Desde[1], Hasta = v.Hasta[1] });
                }
                if (v.A > 0f)
                {
                    tiras.Add(new Tira { W0 = -h, W1 = -h + v.A, Rango = 2, Losa = Losa.Acera, EsAcera = true, Desde = v.Desde[2], Hasta = v.Hasta[2] });
                    tiras.Add(new Tira { W0 = h - v.A, W1 = h, Rango = 2, Losa = Losa.Acera, EsAcera = true, Desde = v.Desde[2], Hasta = v.Hasta[2] });
                }
            }

            // Lo que de la calzada queda debajo de una plaza o de una calzada con más prioridad se hunde un poco;
            // el escalón, a 2 cm del borde y por debajo de la otra superficie.
            bool Tapada(Vector2 p)
            {
                if (Plano.EnVerde(p)) return false;
                foreach (PlazaPavimentada pl in Plano.Plazas)
                    if (pl.Contiene(p, -0.01f)) return true;
                foreach (ViaPavimentada b in Plano.Vias)
                    if (b.Orden < v.Orden && b.EnRegion(p, 0, -0.01f)) return true;
                return false;
            }
            float Hundido(Vector2 p) => Tapada(p) ? -HundidoBajoOtra : 0f;

            var cortes = new List<float>(v.Estaciones);
            foreach (Tira t in tiras)
            {
                float medio = (t.W0 + t.W1) * 0.5f;
                Tira tira = t;
                t.Tramos = Tramos(t.Desde, t.Hasta, s => Recortado(v, tira.Rango, v.Punto(s, medio)));
                foreach (Vector2 tr in t.Tramos) { cortes.Add(tr.x); cortes.Add(tr.y); }
                if (t.EsBordillo || t.EsAcera) continue;
                // El escalón del hundido, en cada borde de la tira (si la otra superficie llega sesgada, cada borde
                // la cruza en un sitio).
                foreach (float w in new[] { t.W0, t.W1 })
                    foreach (Vector2 tr in Tramos(0f, v.L, s => Tapada(v.Punto(s, w))))
                        foreach (float c in new[] { tr.x, tr.y })
                        {
                            cortes.Add(c);
                            cortes.Add(Mathf.Clamp(c - 0.02f, 0f, v.L));
                            cortes.Add(Mathf.Clamp(c + 0.02f, 0f, v.L));
                        }
            }
            // Estaciones comunes, partidas donde el terreno se arquea a lo largo de cualquier borde de la sección.
            var lineas = new List<float>(col) { -h, h };
            if (!v.Paso) { lineas.Add(-v.C - v.B); lineas.Add(v.C + v.B); }
            List<float> estaciones = Afinar(Estaciones(cortes, 0f, v.L), lineas.Count, (s, k) => v.Punto(s, lineas[k]));

            foreach (Tira t in tiras)
                foreach (Vector2 tr in t.Tramos)
                {
                    List<float> s = Entre(estaciones, tr.x, tr.y);
                    if (s.Count < 2) continue;
                    int n = s.Count;
                    float[] u = s.ToArray();
                    var a = new Vector2[n];
                    var b = new Vector2[n];
                    // «a», del lado de la calzada; «b», del de fuera.
                    bool izquierda = t.W1 <= 0f;
                    float wDentro = izquierda ? t.W1 : t.W0, wFuera = izquierda ? t.W0 : t.W1;
                    for (int i = 0; i < n; i++) { a[i] = v.Punto(s[i], wDentro); b[i] = v.Punto(s[i], wFuera); }
                    if (t.EsBordillo) Bordillo(m, a, b, u);
                    else if (t.EsAcera)
                    {
                        // En dos fajas si el terreno se arquea de través (un talud que cruza la acera).
                        var c = new Vector2[n];
                        bool partir = false;
                        for (int i = 0; i < n; i++)
                        {
                            c[i] = (a[i] + b[i]) * 0.5f;
                            partir |= Arqueado(a[i], b[i]);
                        }
                        float ancho = t.W1 - t.W0;
                        var uvA = new Vector2[n];
                        var uvB = new Vector2[n];
                        var uvC = new Vector2[n];
                        for (int i = 0; i < n; i++)
                        {
                            uvA[i] = new Vector2(s[i] / RepeticionDeLosas, 0f);
                            uvC[i] = new Vector2(s[i] / RepeticionDeLosas, ancho * 0.5f / RepeticionDeLosas);
                            uvB[i] = new Vector2(s[i] / RepeticionDeLosas, ancho / RepeticionDeLosas);
                        }
                        if (partir)
                        {
                            Faja(m, Losa.Acera, a, c, uvA, uvC, CapaDeAcera, false, true, true);
                            Faja(m, Losa.Acera, c, b, uvC, uvB, CapaDeAcera, true, true, true);
                        }
                        else Faja(m, Losa.Acera, a, b, uvA, uvB, CapaDeAcera, true, true, true);
                        areaDeAcera += ancho * (tr.y - tr.x);
                    }
                    else if (t.EsCinta)
                    {
                        var uvA = new Vector2[n];
                        var uvB = new Vector2[n];
                        for (int i = 0; i < n; i++)
                        {
                            uvA[i] = new Vector2(s[i] / RepeticionDeBordillo, BandaArista);
                            uvB[i] = new Vector2(s[i] / RepeticionDeBordillo, BandaFondo);
                        }
                        Faja(m, Losa.Piedra, a, b, uvA, uvB, capaCalzada, true, false, false, Hundido);
                    }
                    else
                    {
                        var puntos = new Vector2[n, 2];
                        var uv = new Vector2[n, 2];
                        for (int i = 0; i < n; i++)
                        {
                            puntos[i, 0] = v.Punto(s[i], t.W0);
                            puntos[i, 1] = v.Punto(s[i], t.W1);
                            uv[i, 0] = new Vector2(t.W0 / RepeticionDeAdoquin, s[i] / RepeticionDeAdoquin);
                            uv[i, 1] = new Vector2(t.W1 / RepeticionDeAdoquin, s[i] / RepeticionDeAdoquin);
                        }
                        Rejilla(m, Losa.Adoquin, puntos, uv, capaCalzada, Hundido);
                        if (Mathf.Approximately(t.W0, -wc)) largoDeCalzada += tr.y - tr.x;
                    }
                }

            if (v.TapaInicio) RematarExtremo(m, v, true, col);
            if (v.TapaFin) RematarExtremo(m, v, false, col);
            var secciones = new List<float>(col) { -h, h };
            secciones.Sort();
            if (v.UmbralInicio) Umbral(m, v, true, capaCalzada, secciones);
            if (v.UmbralFin) Umbral(m, v, false, capaCalzada, secciones);
            Detalle.Add($"  · {m.Nombre}: {v.L:0} m; empieza {Contar(v, v.Inicio, v.OtraInicio)} y acaba {Contar(v, v.Fin, v.OtraFin)}.");
            return m;
        }

        private static string Contar(ViaPavimentada v, Remate r, ViaPavimentada otra) => r switch
        {
            Remate.Plaza => "en una plaza",
            Remate.Cruce => $"en la calle {otra.Indice}",
            Remate.EsquinaQueGana => $"en esquina con la calle {otra.Indice} (rematada)",
            Remate.EsquinaQuePierde => $"en esquina con la calle {otra.Indice}",
            _ => v.Paso ? "con umbral (paso de la puerta)" : "en un fondo rematado",
        };

        /// Remate de un extremo: bordillo de lado a lado de la calzada y acera hasta el final, recortados como
        /// los de los lados.
        private void RematarExtremo(MallaDeLosas m, ViaPavimentada v, bool inicio, float[] columnas)
        {
            float sFin = inicio ? 0f : v.L;
            float signo = inicio ? 1f : -1f;
            float sAcera = sFin + signo * v.A, sCalzada = sAcera + signo * v.B;
            float hBordillo = v.C, hAcera = v.C + v.B;

            if (v.B > 0f)
            {
                float medio = (sAcera + sCalzada) * 0.5f;
                foreach (Vector2 tr in Tramos(-hBordillo, hBordillo, w => Recortado(v, 1, v.Punto(medio, w))))
                {
                    float[] w = Estaciones(columnas, tr.x, tr.y).ToArray();
                    var dentro = new Vector2[w.Length];
                    var fuera = new Vector2[w.Length];
                    for (int i = 0; i < w.Length; i++) { dentro[i] = v.Punto(sCalzada, w[i]); fuera[i] = v.Punto(sAcera, w[i]); }
                    Bordillo(m, dentro, fuera, w);
                }
            }
            if (v.A > 0f)
            {
                float medio = (sFin + sAcera) * 0.5f;
                foreach (Vector2 tr in Tramos(-hAcera, hAcera, w => Recortado(v, 2, v.Punto(medio, w))))
                {
                    float[] w = Estaciones(columnas, tr.x, tr.y).ToArray();
                    int n = w.Length;
                    var dentro = new Vector2[n];
                    var fuera = new Vector2[n];
                    var uvA = new Vector2[n];
                    var uvB = new Vector2[n];
                    for (int i = 0; i < n; i++)
                    {
                        dentro[i] = v.Punto(sAcera, w[i]);
                        fuera[i] = v.Punto(sFin, w[i]);
                        uvA[i] = new Vector2(w[i] / RepeticionDeLosas, 0f);
                        uvB[i] = new Vector2(w[i] / RepeticionDeLosas, v.A / RepeticionDeLosas);
                    }
                    Faja(m, Losa.Acera, dentro, fuera, uvA, uvB, CapaDeAcera, true, false, false);
                    areaDeAcera += v.A * (tr.y - tr.x);
                }
            }
        }

        /// Umbral del paso de la puerta donde empieza el camino: una cinta de sillares de lado a lado, a ras.
        private void Umbral(MallaDeLosas m, ViaPavimentada v, bool inicio, float capa, List<float> columnas)
        {
            float sFin = inicio ? 0f : v.L, sDentro = inicio ? AnchoDeUmbral : v.L - AnchoDeUmbral;
            float[] w = columnas.ToArray();
            int n = w.Length;
            var dentro = new Vector2[n];
            var fuera = new Vector2[n];
            var uvA = new Vector2[n];
            var uvB = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                dentro[i] = v.Punto(sDentro, w[i]);
                fuera[i] = v.Punto(sFin, w[i]);
                uvA[i] = new Vector2(w[i] / RepeticionDeBordillo, BandaArista);
                uvB[i] = new Vector2(w[i] / RepeticionDeBordillo, BandaFondo);
            }
            Faja(m, Losa.Piedra, dentro, fuera, uvA, uvB, capa, true, true, true);
        }

        // ── Accesos de las puertas ───────────────────────────────────────────────────────────

        private void PavimentarAcceso(MallaDeLosas m, AccesoPavimentado a)
        {
            float medio = AnchoDeAcceso * 0.5f, llega = a.Largo - MetidoEnLoPavimentado;
            Vector2 Punto(float d, int k) => a.Desde + a.Dir * d + a.Lado * (k - 1) * medio;
            // El final, que entra por debajo de la acera o de la plaza, va hundido; el escalón, a 2 cm de donde cada
            // borde del acceso llega a lo pavimentado.
            var cortes = new List<float>();
            for (int k = 0; k < 3; k++)
            {
                int linea = k;
                foreach (Vector2 tr in Tramos(0f, a.Largo, d => Plano.EsPavimento(Punto(d, linea), -0.01f)))
                    foreach (float c in new[] { tr.x, tr.y })
                    {
                        cortes.Add(c);
                        cortes.Add(Mathf.Max(c - 0.02f, 0f));
                        cortes.Add(Mathf.Min(c + 0.02f, a.Largo));
                    }
            }
            List<float> t = Afinar(Estaciones(cortes, 0f, a.Largo), 3, Punto);
            float Hundido(Vector2 p) => Plano.EsPavimento(p, -0.01f) ? -HundidoBajoOtra : 0f;
            int n = t.Count;
            var izq = new Vector2[n];
            var der = new Vector2[n];
            var centro = new Vector2[n];
            var uvI = new Vector2[n];
            var uvD = new Vector2[n];
            var uvC = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                izq[i] = Punto(t[i], 0);
                centro[i] = Punto(t[i], 1);
                der[i] = Punto(t[i], 2);
                uvI[i] = new Vector2(0f, t[i] / RepeticionDeLosas);
                uvC[i] = new Vector2(medio / RepeticionDeLosas, t[i] / RepeticionDeLosas);
                uvD[i] = new Vector2(AnchoDeAcceso / RepeticionDeLosas, t[i] / RepeticionDeLosas);
            }
            // Dos fajas desde el eje, con faldón a cada lado; la cabeza de la casa y la del final quedan debajo de algo.
            Faja(m, Losa.Acera, centro, izq, uvC, uvI, CapaDeAcceso, true, false, false, Hundido);
            Faja(m, Losa.Acera, centro, der, uvC, uvD, CapaDeAcceso, true, false, false, Hundido);
            areaDeAcera += AnchoDeAcceso * llega;
        }

        // ── Plazas ───────────────────────────────────────────────────────────────────────────

        private MallaDeLosas PavimentarPlaza(PlazaPavimentada pl)
        {
            var m = new MallaDeLosas($"Plaza {pl.Indice}");
            float bordeMarco = AnchoDeBordillo + (pl.ConMarco ? AnchoDeMarco : 0f);
            float ejeMedio = AnchoDeEjeDeLosas * 0.5f, ejeFuera = ejeMedio + AnchoDeCintaDeEje;
            bool conEje = pl.Roseton > 0f;

            // Cortes de la rejilla: bordes, bordillo, marco, eje, zonas verdes y bocas de calles y accesos.
            var xs = new List<float> { -pl.Hx, -pl.Hx + AnchoDeBordillo, -pl.Hx + bordeMarco, pl.Hx - bordeMarco, pl.Hx - AnchoDeBordillo, pl.Hx };
            var zs = new List<float> { -pl.Hz, -pl.Hz + AnchoDeBordillo, -pl.Hz + bordeMarco, pl.Hz - bordeMarco, pl.Hz - AnchoDeBordillo, pl.Hz };
            if (conEje)
            {
                List<float> l = pl.EjeEnZ ? xs : zs;
                l.Add(-ejeFuera); l.Add(-ejeMedio); l.Add(ejeMedio); l.Add(ejeFuera);
            }
            foreach (Rect r in Plano.Verdes)
            {
                if (!pl.Contiene(r.center, Mathf.Max(r.width, r.height))) continue;
                foreach (float dx in new[] { 0f, AnchoDeBordillo, bordeMarco })
                    foreach (Vector2 esquina in new[]
                             {
                                 new Vector2(r.xMin - dx, r.yMin - dx), new Vector2(r.xMax + dx, r.yMax + dx),
                             })
                    {
                        Vector2 q = pl.Local(esquina);
                        xs.Add(q.x);
                        zs.Add(q.y);
                    }
            }
            foreach (ViaPavimentada v in Plano.Vias)
                foreach (float w in new[] { -v.Semiancho(0), v.Semiancho(0) })
                {
                    var linea = new List<Vector2>();
                    foreach (float s in v.Estaciones) linea.Add(v.Punto(s, w));
                    CortesDeLinea(pl, linea, xs, zs);
                }
            foreach (AccesoPavimentado a in Plano.Accesos)
                foreach (float w in new[] { -AnchoDeAcceso * 0.5f, AnchoDeAcceso * 0.5f })
                    CortesDeLinea(pl, new List<Vector2> { a.Desde + a.Lado * w, a.Hacia + a.Lado * w }, xs, zs);
            float[] cx = Particion(xs, -pl.Hx, pl.Hx), cz = Particion(zs, -pl.Hz, pl.Hz);
            // Más cortes donde el terreno se arquea (crestas de talud, el pie de una rampa).
            float[] filas = cz;
            List<float> afinadas = Afinar(new List<float>(cx), filas.Length, (x, k) => pl.Mundo(x, filas[k]));
            cx = afinadas.ToArray();
            float[] columnas = cx;
            cz = Afinar(new List<float>(cz), columnas.Length, (z, k) => pl.Mundo(columnas[k], z)).ToArray();

            // Taludes dentro de la plaza: no se pavimentan y se tratan como las zonas verdes (con su bordillo y su
            // marco alrededor), así se leen como un ribazo entre dos niveles de la plaza.
            var taludes = new List<Rect>();
            float enTalud = 0f;
            Vector2 sumaTalud = Vector2.zero;
            for (int i = 0; i < cx.Length - 1; i++)
                for (int k = 0; k < cz.Length - 1; k++)
                {
                    var celda = Rect.MinMaxRect(cx[i], cz[k], cx[i + 1], cz[k + 1]);
                    Vector2 c = pl.Mundo(celda.center.x, celda.center.y);
                    if (!EnTalud(c) || Plano.EnVerde(c) || Plano.FueraDelRecinto(c)) continue;
                    taludes.Add(celda);
                    enTalud += celda.width * celda.height;
                    sumaTalud += c * celda.width * celda.height;
                }
            if (taludes.Count > 0)
            {
                taludes = Agrupar(taludes);
                var xs2 = new List<float>(cx);
                var zs2 = new List<float>(cz);
                foreach (Rect r in taludes)
                    foreach (float d in new[] { 0f, AnchoDeBordillo, bordeMarco })
                    {
                        xs2.Add(r.xMin - d); xs2.Add(r.xMax + d);
                        zs2.Add(r.yMin - d); zs2.Add(r.yMax + d);
                    }
                cx = Particion(xs2, -pl.Hx, pl.Hx);
                cz = Particion(zs2, -pl.Hz, pl.Hz);
                Vector2 c = sumaTalud / enTalud;
                Detalle.Add($"  · {m.Nombre}: {enTalud:0} m² sin pavimentar por un talud de más de {PendienteMaximaDePlaza:0}° alrededor de ({c.x:0}, {c.y:0}); " +
                            "el pavimento lo rodea con bordillo. Si debe ser llano, hay que allanar ahí el terreno o mover la plaza.");
            }

            // Clase de cada celda.
            int nx = cx.Length - 1, nz = cz.Length - 1;
            var clase = new Celda[nx, nz];
            var alLargo = new bool[nx, nz];       // bordillo: true si corre a lo largo de la x local
            var conFondo = new Vector2[nx, nz];   // bordillo: borde de fuera (local) para medir la profundidad
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                {
                    float lx = (cx[i] + cx[i + 1]) * 0.5f, lz = (cz[k] + cz[k + 1]) * 0.5f;
                    clase[i, k] = Clasificar(pl, lx, lz, bordeMarco, conEje, ejeMedio, ejeFuera, taludes, out alLargo[i, k], out conFondo[i, k]);
                }

            // Superficies.
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                {
                    Celda c = clase[i, k];
                    if (c == Celda.Fuera) continue;
                    var esquinas = new[] { new Vector2(cx[i], cz[k]), new Vector2(cx[i + 1], cz[k]), new Vector2(cx[i + 1], cz[k + 1]), new Vector2(cx[i], cz[k + 1]) };
                    var puntos = new Vector2[2, 2];
                    var uv = new Vector2[2, 2];
                    for (int e = 0; e < 4; e++)
                    {
                        int fi = e == 0 || e == 1 ? 0 : 1, co = e == 0 || e == 3 ? 0 : 1;
                        Vector2 q = esquinas[e];
                        puntos[fi, co] = pl.Mundo(q.x, q.y);
                        uv[fi, co] = UvDeCelda(pl, c, q, alLargo[i, k], conFondo[i, k]);
                    }
                    Losa losa = c switch
                    {
                        Celda.Bordillo => Losa.Piedra,
                        Celda.CintaDeEje => Losa.Piedra,
                        Celda.Eje => Losa.Acera,
                        Celda.Marco => pl.Marco,
                        _ => pl.Campo,
                    };
                    float capa = c == Celda.Bordillo ? AltoDeBordillo : CapaDePlaza;
                    Rejilla(m, losa, puntos, uv, capa);
                    if (c != Celda.Bordillo) areaDePlazas += (cx[i + 1] - cx[i]) * (cz[k + 1] - cz[k]);
                    else largoDeBordillo += alLargo[i, k] ? cx[i + 1] - cx[i] : cz[k + 1] - cz[k];

                    // Frentes del bordillo y faldones hacia lo que queda más bajo o fuera de la plaza.
                    for (int lado = 0; lado < 4; lado++)
                    {
                        int vi = i + (lado == 0 ? -1 : lado == 1 ? 1 : 0), vk = k + (lado == 2 ? -1 : lado == 3 ? 1 : 0);
                        Celda vecina = vi < 0 || vk < 0 || vi >= nx || vk >= nz ? Celda.Fuera : clase[vi, vk];
                        if (c == Celda.Bordillo ? vecina == Celda.Bordillo : vecina != Celda.Fuera) continue;
                        Vector2 a, b, fueraLocal;
                        switch (lado)
                        {
                            case 0: a = esquinas[0]; b = esquinas[3]; fueraLocal = new Vector2(-1f, 0f); break;
                            case 1: a = esquinas[1]; b = esquinas[2]; fueraLocal = new Vector2(1f, 0f); break;
                            case 2: a = esquinas[0]; b = esquinas[1]; fueraLocal = new Vector2(0f, -1f); break;
                            default: a = esquinas[3]; b = esquinas[2]; fueraLocal = new Vector2(0f, 1f); break;
                        }
                        Vector2 fuera = pl.Ex * fueraLocal.x + pl.Ez * fueraLocal.y;
                        float ua = lado < 2 ? a.y : a.x, ub = lado < 2 ? b.y : b.x;
                        System.Func<float, float, Vector2> uvPared = c == Celda.Bordillo
                            ? UvDeFrente
                            : (u, bajo) => new Vector2(u / RepeticionDeAdoquin, bajo / RepeticionDeAdoquin);
                        Pared(m, c == Celda.Bordillo ? Losa.Piedra : losa, new[] { pl.Mundo(a.x, a.y), pl.Mundo(b.x, b.y) }, new[] { ua, ub }, capa, fuera, uvPared);
                    }
                }

            if (conEje)
            {
                Roseton(m, pl);
                roseton++;
            }
            return m;
        }

        /// Añade a xs/zs (coordenadas locales de la plaza) los cruces de la línea con los lados de la plaza y con
        /// los del bordillo.
        private static void CortesDeLinea(PlazaPavimentada pl, List<Vector2> linea, List<float> xs, List<float> zs)
        {
            float[] xl = { -pl.Hx, -pl.Hx + AnchoDeBordillo, pl.Hx - AnchoDeBordillo, pl.Hx };
            float[] zl = { -pl.Hz, -pl.Hz + AnchoDeBordillo, pl.Hz - AnchoDeBordillo, pl.Hz };
            for (int j = 0; j < linea.Count - 1; j++)
            {
                Vector2 a = pl.Local(linea[j]), b = pl.Local(linea[j + 1]);
                foreach (float x in xl)
                {
                    if ((a.x - x) * (b.x - x) > 0f || Mathf.Approximately(a.x, b.x)) continue;
                    float z = Mathf.Lerp(a.y, b.y, (x - a.x) / (b.x - a.x));
                    if (Mathf.Abs(z) <= pl.Hz) zs.Add(z);
                }
                foreach (float z in zl)
                {
                    if ((a.y - z) * (b.y - z) > 0f || Mathf.Approximately(a.y, b.y)) continue;
                    float x = Mathf.Lerp(a.x, b.x, (z - a.y) / (b.y - a.y));
                    if (Mathf.Abs(x) <= pl.Hx) xs.Add(x);
                }
            }
        }

        /// Cortes ordenados, sin repetidos, dentro de [a, b] y a PasoDeMalla como mucho.
        private static float[] Particion(List<float> cortes, float a, float b)
        {
            var e = new List<float>();
            foreach (float c in cortes)
                if (c > a + 0.01f && c < b - 0.01f) e.Add(c);
            return Estaciones(e, a, b).ToArray();
        }

        /// Clase de la celda con centro (lx, lz). Bordillo donde la plaza linda con tierra (a menos de
        /// AnchoDeBordillo del borde) y no hay pavimento a ras ni obra al otro lado; marco a continuación; el eje
        /// del rosetón cruza el marco hasta el borde; el resto, campo. Lo que cae en una zona verde, fuera del
        /// recinto o en una plaza anterior, fuera.
        private Celda Clasificar(PlazaPavimentada pl, float lx, float lz, float bordeMarco, bool conEje, float ejeMedio, float ejeFuera,
            List<Rect> taludes, out bool alLargo, out Vector2 fondo)
        {
            alLargo = false;
            fondo = Vector2.zero;
            Vector2 p = pl.Mundo(lx, lz);
            if (Plano.EnVerde(p) || Plano.FueraDelRecinto(p)) return Celda.Fuera;
            foreach (PlazaPavimentada otra in Plano.Plazas)
                if (otra.Indice < pl.Indice && otra.Contiene(p)) return Celda.Fuera;
            var local = new Vector2(lx, lz);
            foreach (Rect r in taludes)
                if (r.Contains(local)) return Celda.Fuera;

            // Borde más cercano que linda con algo que no es otra plaza: lados de la plaza y zonas verdes.
            float d = float.MaxValue;
            Vector2 borde = Vector2.zero, fuera = Vector2.zero, fondoLocal = Vector2.zero;
            bool largoDelBorde = false;
            void Lado(float distancia, Vector2 puntoLocal, Vector2 fueraLocal, bool largo)
            {
                if (distancia >= d) return;
                Vector2 q = pl.Mundo(puntoLocal.x, puntoLocal.y);
                Vector2 f = pl.Ex * fueraLocal.x + pl.Ez * fueraLocal.y;
                if (PlazaAlLado(pl, q + f * 0.3f)) return;
                d = distancia;
                borde = q;
                fuera = f;
                largoDelBorde = largo;
                fondoLocal = puntoLocal;
            }
            Lado(lx + pl.Hx, new Vector2(-pl.Hx, lz), new Vector2(-1f, 0f), false);
            Lado(pl.Hx - lx, new Vector2(pl.Hx, lz), new Vector2(1f, 0f), false);
            Lado(lz + pl.Hz, new Vector2(lx, -pl.Hz), new Vector2(0f, -1f), true);
            Lado(pl.Hz - lz, new Vector2(lx, pl.Hz), new Vector2(0f, 1f), true);
            foreach (Rect r in Plano.Verdes)
            {
                float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax), dz = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
                float dist = Mathf.Max(dx, dz);
                if (dist >= d) continue;
                d = dist;
                Vector2 f = dx >= dz ? new Vector2(Mathf.Sign(r.center.x - p.x), 0f) : new Vector2(0f, Mathf.Sign(r.center.y - p.y));
                borde = p + f * dist;
                fuera = f;
                fondoLocal = pl.Local(borde);
                largoDelBorde = Mathf.Abs(Vector2.Dot(f, pl.Ez)) > Mathf.Abs(Vector2.Dot(f, pl.Ex));
            }
            foreach (Rect r in taludes)
            {
                float dx = Mathf.Max(r.xMin - lx, 0f, lx - r.xMax), dz = Mathf.Max(r.yMin - lz, 0f, lz - r.yMax);
                float dist = Mathf.Max(dx, dz);
                if (dist >= d) continue;
                d = dist;
                Vector2 fl = dx >= dz ? new Vector2(Mathf.Sign(r.center.x - lx), 0f) : new Vector2(0f, Mathf.Sign(r.center.y - lz));
                fondoLocal = local + fl * dist;
                borde = pl.Mundo(fondoLocal.x, fondoLocal.y);
                fuera = pl.Ex * fl.x + pl.Ez * fl.y;
                largoDelBorde = dx < dz;
            }
            alLargo = largoDelBorde;
            fondo = fondoLocal;

            if (d < AnchoDeBordillo)
            {
                if (!Plano.PavimentoAlLado(borde + fuera * 0.3f, pl))
                {
                    if (!Obstaculo(borde, fuera)) return Celda.Bordillo;
                    bordillosAbiertosPorObra++;
                }
            }
            if (conEje)
            {
                float transversal = Mathf.Abs(pl.EjeEnZ ? lx : lz);
                if (transversal <= ejeFuera) return transversal > ejeMedio ? Celda.CintaDeEje : Celda.Eje;
            }
            return d < bordeMarco && pl.ConMarco ? Celda.Marco : Celda.Campo;
        }

        /// Junta los rectángulos que se tocan en el que los abarca, si así no se deja sin pavimentar más del doble
        /// de lo que ocupan: el hueco del talud sale como un parterre recto y no con dientes.
        private static List<Rect> Agrupar(List<Rect> rectangulos)
        {
            var r = new List<(Rect caja, float area)>();
            foreach (Rect q in rectangulos) r.Add((q, q.width * q.height));
            for (bool junto = true; junto;)
            {
                junto = false;
                for (int i = 0; i < r.Count && !junto; i++)
                    for (int j = i + 1; j < r.Count && !junto; j++)
                    {
                        Rect a = r[i].caja, b = r[j].caja;
                        if (a.xMin > b.xMax + 0.01f || b.xMin > a.xMax + 0.01f || a.yMin > b.yMax + 0.01f || b.yMin > a.yMax + 0.01f) continue;
                        Rect u = Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
                        float area = r[i].area + r[j].area;
                        if (u.width * u.height > 2f * area) continue;
                        r[i] = (u, area);
                        r.RemoveAt(j);
                        junto = true;
                    }
            }
            var cajas = new List<Rect>();
            foreach (var q in r) cajas.Add(q.caja);
            return cajas;
        }

        /// Si la celda con centro p pisa un talud de más de PendienteMaximaDePlaza (en su centro o a medio metro).
        private bool EnTalud(Vector2 p)
        {
            for (int k = 0; k < 5; k++)
            {
                Vector2 q = p + (k == 0 ? Vector2.zero : new Vector2(k == 1 ? 0.5f : k == 2 ? -0.5f : 0f, k == 3 ? 0.5f : k == 4 ? -0.5f : 0f));
                if (Pendiente(q.x, q.y) > PendienteMaximaDePlaza) return true;
            }
            return false;
        }

        private bool PlazaAlLado(PlazaPavimentada propia, Vector2 q)
        {
            if (Plano.EnVerde(q)) return false;
            foreach (PlazaPavimentada otra in Plano.Plazas)
                if (otra != propia && otra.Contiene(q)) return true;
            return false;
        }

        /// UV de una esquina (local) de una celda de plaza según su clase.
        private static Vector2 UvDeCelda(PlazaPavimentada pl, Celda c, Vector2 q, bool alLargo, Vector2 fondo)
        {
            switch (c)
            {
                case Celda.Bordillo:
                {
                    float profundidad = Mathf.Clamp(alLargo ? Mathf.Abs(q.y - fondo.y) : Mathf.Abs(q.x - fondo.x), 0f, AnchoDeBordillo);
                    float a = alLargo ? q.x : q.y;
                    return new Vector2(a / RepeticionDeBordillo, BandaFondo - profundidad / AnchoDeBordillo * (BandaFondo - BandaArista));
                }
                case Celda.CintaDeEje:
                {
                    float transversal = Mathf.Abs(pl.EjeEnZ ? q.x : q.y), a = pl.EjeEnZ ? q.y : q.x;
                    float t = Mathf.Clamp01((transversal - AnchoDeEjeDeLosas * 0.5f) / AnchoDeCintaDeEje);
                    return new Vector2(a / RepeticionDeBordillo, BandaArista + t * (BandaFondo - BandaArista));
                }
                case Celda.Eje:
                    return pl.EjeEnZ
                        ? new Vector2((q.x + AnchoDeEjeDeLosas * 0.5f) / RepeticionDeLosas, q.y / RepeticionDeLosas)
                        : new Vector2(q.x / RepeticionDeLosas, (q.y + AnchoDeEjeDeLosas * 0.5f) / RepeticionDeLosas);
                case Celda.Marco:
                    return new Vector2(q.x / RepeticionDeAdoquin, q.y / RepeticionDeAdoquin);
                default:
                    return pl.Campo == Losa.Baldosa
                        ? new Vector2(q.x / RepeticionDeBaldosa + 0.5f, q.y / RepeticionDeBaldosa + 0.5f)
                        : new Vector2(q.x / RepeticionDeAdoquin, q.y / RepeticionDeAdoquin);
            }
        }

        /// Rosetón: disco de Ground02 del diámetro dado (una repetición de la textura: el dibujo de cuatro
        /// lóbulos con su medallón, alineado con la plaza) y anillo de sillares con un número entero de piezas.
        private void Roseton(MallaDeLosas m, PlazaPavimentada pl)
        {
            float radio = pl.Roseton * 0.5f, fuera = radio + AnchoDeAnilloDeRoseton;
            int lados = Mathf.Max(16, Mathf.CeilToInt(2f * Mathf.PI * fuera / 0.9f / 8f) * 8);
            int anillos = Mathf.Max(1, Mathf.CeilToInt(radio / PasoDeMalla));
            var puntos = new Vector2[anillos + 1, lados + 1];
            var uv = new Vector2[anillos + 1, lados + 1];
            for (int r = 0; r <= anillos; r++)
                for (int j = 0; j <= lados; j++)
                {
                    float ang = j * 2f * Mathf.PI / lados, rr = radio * r / anillos;
                    var q = new Vector2(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr);
                    puntos[r, j] = pl.Mundo(q.x, q.y);
                    uv[r, j] = new Vector2(q.x / pl.Roseton + 0.5f, q.y / pl.Roseton + 0.5f);
                }
            Rejilla(m, Losa.Roseton, puntos, uv, CapaDeRoseton);

            int sillares = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * radio / SillarDeBordillo));
            var anillo = new Vector2[lados + 1, 2];
            var uvAnillo = new Vector2[lados + 1, 2];
            for (int j = 0; j <= lados; j++)
            {
                float ang = j * 2f * Mathf.PI / lados, u = (float)j / lados * sillares * 0.125f;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                anillo[j, 0] = pl.Mundo(dir.x * radio, dir.y * radio);
                anillo[j, 1] = pl.Mundo(dir.x * fuera, dir.y * fuera);
                uvAnillo[j, 0] = new Vector2(u, BandaArista);
                uvAnillo[j, 1] = new Vector2(u, BandaFondo);
            }
            Rejilla(m, Losa.Piedra, anillo, uvAnillo, CapaDeRoseton);
        }
    }
}
