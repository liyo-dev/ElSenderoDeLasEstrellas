using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Las atracciones del parque a las que se sube el grupo (<see cref="AtraccionMontable"/>). Cada una
/// se describe con datos: qué atracción del pack es, qué malla es el vagón, dónde va cada uno dentro
/// (en el espacio de la malla, medido sobre el modelo), dónde se sube y qué planos lleva. Los
/// puntos de subida van bajo «Atracciones montables», fuera del parque, para no tocar los prefabs
/// del pack ni el NavMesh. Se rehacen en cada ejecución.
///
/// Las medidas están tomadas leyendo el FBX del pack («espacio del archivo»). Antes de usarlas se
/// comprueba con los límites de la malla importada por Unity qué ejes salen dados la vuelta, y se
/// convierten. Ver INC-892 e INC-893.
/// </summary>
public static partial class ParqueBuilder
{
    private const string RaizDeAtracciones = "Atracciones montables";

    private sealed class DatosDeAtraccion
    {
        public string Nombre, Prefab, MallaDelVagon, Clave, Pregunta;
        public Vector3[] Puestos;
        public Vector3 Mirar;
        public NPCAmbientActivity Pose;
        /// <summary>Dónde se sube, en el espacio de la atracción (se busca suelo libre cerca).</summary>
        public Vector3 Subida;
        public float Duracion;
        /// <summary>Plano fijo en el suelo, cerca, en el espacio de la atracción, mirando al vagón.</summary>
        public Vector3 PlanoFijo;
        /// <summary>Plano de lejos, en el espacio de la atracción: la atracción entera con sus luces.</summary>
        public Vector3 PlanoLejos;
        /// <summary>Límites de la malla del vagón en el FBX (espacio del archivo, metros).</summary>
        public Vector3 ArchivoMin, ArchivoMax;
        /// <summary>Si &gt; 0, el vagón se balancea con <see cref="BalanceoPendular"/> (grados) en vez de con su Animator.</summary>
        public float Balanceo;
        public float PeriodoDelBalanceo;
        /// <summary>Repertorio de escenitas: «Barco» o «Noria» (ver ParqueBuilder.Escenitas).</summary>
        public string Repertorio;
    }

    private static readonly DatosDeAtraccion[] Montables =
    {
        // La noria: cabina redonda de 2,2 m con el suelo a 2,47 m bajo su eje; de pie, asomados
        // a la barandilla. Una vuelta entera son 24 s.
        new DatosDeAtraccion
        {
            Nombre = "Subir a la noria", Prefab = "attraction_003", MallaDelVagon = "attraction_3_003",
            Clave = "ATRACCION_SUBIR_NORIA", Pregunta = "¿Subir a la noria?",
            Puestos = new[] { new Vector3(0f, -2.47f, -0.35f), new Vector3(-0.55f, -2.47f, 0.25f), new Vector3(0.55f, -2.47f, 0.25f) },
            Mirar = Vector3.forward, Pose = NPCAmbientActivity.None, Repertorio = "Noria",
            Subida = new Vector3(0f, 0f, 6f), Duracion = 24f,
            PlanoFijo = new Vector3(-10f, 2f, 22f), PlanoLejos = new Vector3(-24f, 5f, 32f),
            ArchivoMin = new Vector3(-1.091f, -2.965f, -1.080f), ArchivoMax = new Vector3(1.090f, 0.190f, 1.080f),
        },
        // El barco vikingo: filas de cuatro sillas a lo ancho; la fila de z = +2,7 mira hacia el
        // centro (-z), con el suelo a 11,15 m bajo el eje. La animación del pack lo dejaba boca abajo
        // (la barca por encima del eje): se balancea con BalanceoPendular, ±40° cada 8 s como el pack.
        new DatosDeAtraccion
        {
            Nombre = "Subir al barco vikingo", Prefab = "attraction_012", MallaDelVagon = "attraction_12_002",
            Clave = "ATRACCION_SUBIR_BARCO", Pregunta = "¿Subir al barco vikingo?",
            Puestos = new[] { new Vector3(0.39f, -11.15f, 2.45f), new Vector3(-0.39f, -11.15f, 2.45f), new Vector3(1.17f, -11.15f, 2.45f) },
            Mirar = Vector3.back, Pose = NPCAmbientActivity.SitMedium, Repertorio = "Barco",
            Subida = new Vector3(6f, 3f, 2.5f), Duracion = 24f,
            PlanoFijo = new Vector3(18f, 3f, 0f), PlanoLejos = new Vector3(30f, 6f, -8f),
            ArchivoMin = new Vector3(-2.789f, -12.673f, -7.481f), ArchivoMax = new Vector3(2.789f, -0.064f, 8.764f),
            Balanceo = 40f, PeriodoDelBalanceo = 8f,
        },
    };

    private static string Atracciones(Scene escena, Transform parque)
    {
        var resumen = new StringBuilder();
        Transform raiz = null;
        foreach (GameObject r in escena.GetRootGameObjects())
            if (r.name == RaizDeAtracciones) raiz = r.transform;
        if (raiz == null)
        {
            raiz = new GameObject(RaizDeAtracciones).transform;
            SceneManager.MoveGameObjectToScene(raiz.gameObject, escena);
        }

        foreach (var datos in Montables)
        {
            Transform atraccion = BuscarInstancia(parque, datos.Prefab);
            if (atraccion == null)
            {
                resumen.AppendLine($"Atracciones: no encuentro «{datos.Prefab}» en el parque; «{datos.Nombre}» no se monta.");
                continue;
            }
            Transform anterior = raiz.Find(datos.Nombre);
            if (anterior != null) Object.DestroyImmediate(anterior.gameObject);

            var vagones = new List<Transform>();
            foreach (var mf in atraccion.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.sharedMesh.name == datos.MallaDelVagon) vagones.Add(mf.transform);
            if (vagones.Count == 0)
            {
                resumen.AppendLine($"Atracciones: «{datos.Prefab}» no tiene la malla «{datos.MallaDelVagon}»; «{datos.Nombre}» no se monta.");
                continue;
            }

            // Qué ejes de la malla de Unity salen dados la vuelta respecto al archivo.
            Vector3 signo = Signos(vagones[0].GetComponent<MeshFilter>().sharedMesh.bounds, datos.ArchivoMin, datos.ArchivoMax);
            Vector3 M(Vector3 v) => Vector3.Scale(v, signo);
            var puestos = new Vector3[datos.Puestos.Length];
            for (int i = 0; i < puestos.Length; i++) puestos[i] = M(datos.Puestos[i]);
            Vector3 arriba = M(Vector3.up);

            if (datos.Balanceo > 0f)
            {
                // En reposo, el «arriba» de la barca (en su espacio) apunta hacia arriba del mundo.
                Vector3 reposo = arriba.y < 0f ? new Vector3(180f, 0f, 0f) : Vector3.zero;
                foreach (var vagon in vagones)
                {
                    var animador = vagon.GetComponent<Animator>();
                    if (animador != null) { animador.enabled = false; EditorUtility.SetDirty(animador); }
                    var balanceo = vagon.GetComponent<BalanceoPendular>();
                    if (balanceo == null) balanceo = vagon.gameObject.AddComponent<BalanceoPendular>();
                    balanceo.Configurar(reposo, Vector3.right, datos.Balanceo, datos.PeriodoDelBalanceo);
                    vagon.localRotation = Quaternion.Euler(reposo);
                    EditorUtility.SetDirty(balanceo);
                }
                resumen.AppendLine($"Atracciones: «{datos.Prefab}» se balancea con BalanceoPendular (Animator del pack apagado; reposo {reposo}).");
            }

            Vector3 cerca = atraccion.TransformPoint(datos.Subida);
            if (!EscenaJugable.BuscarSitioLibre(cerca, 4f, out Vector3 pie)) pie = cerca;
            Vector3 fuera = pie - atraccion.position; fuera.y = 0f;

            var punto = new GameObject(datos.Nombre);
            punto.transform.SetParent(raiz, false);
            punto.transform.SetPositionAndRotation(pie, Quaternion.LookRotation(fuera.sqrMagnitude > 0.01f ? fuera.normalized : atraccion.forward));

            int capa = LayerMask.NameToLayer("Interactable");
            var zona = new GameObject("Zona");
            zona.transform.SetParent(punto.transform, false);
            zona.transform.localPosition = Vector3.up * 1f;
            if (capa >= 0) zona.layer = capa;
            var caja = zona.AddComponent<BoxCollider>();
            caja.isTrigger = true;
            caja.size = new Vector3(3f, 2.5f, 3f);

            var interactuable = punto.AddComponent<Interactable>();
            interactuable.SetMode(InteractableMode.Custom);
            MontadorDeBarco.CrearHint(punto.transform, Vector3.up * 1.8f, interactuable);

            Transform PuntoDeCamara(string nombre, Vector3 enLaAtraccion)
            {
                var go = new GameObject(nombre);
                go.transform.SetParent(punto.transform, false);
                go.transform.position = atraccion.TransformPoint(enLaAtraccion);
                return go.transform;
            }
            Transform desdeElSuelo = PuntoDeCamara("Plano desde el suelo", datos.PlanoFijo);
            Transform desdeLejos = PuntoDeCamara("Plano de lejos", datos.PlanoLejos);

            var montable = punto.AddComponent<AtraccionMontable>();
            montable.Configurar(vagones.ToArray(), puestos, M(datos.Mirar), arriba, datos.Pose, punto.transform, datos.Duracion,
                new[]
                {
                    // De lejos, acercándose despacio: la atracción entera con sus luces.
                    new AtraccionMontable.Plano { punto = desdeLejos, seguirAlVagon = true, segundos = 6f, fov = 34f, fovFinal = 24f },
                    // Delante de ellos, a la cara, rodeándoles un poco (ejes del grupo: z hacia donde miran).
                    new AtraccionMontable.Plano { aBordo = new Vector3(0f, 1.3f, 2.3f), mirarABordo = new Vector3(0f, -0.15f, -1f), segundos = 9f, fov = 55f, giro = 50f },
                    // Desde el suelo, viéndolos pasar.
                    new AtraccionMontable.Plano { punto = desdeElSuelo, seguirAlVagon = true, segundos = 5f, fov = 45f, fovFinal = 36f },
                    // Por detrás y por encima, viendo el parque con ellos (corto).
                    new AtraccionMontable.Plano { aBordo = new Vector3(0.5f, 2f, -2.4f), mirarABordo = new Vector3(0f, -0.3f, 1f), segundos = 4f, fov = 62f, giro = 20f },
                },
                datos.Clave, datos.Pregunta, RepertorioDe(datos.Repertorio));
            resumen.AppendLine($"Atracciones: «{datos.Nombre}» montada ({vagones.Count} vagón/es; ejes de la malla {signo}).");
        }
        return resumen.ToString();
    }

    private static RepertorioDeAtraccion RepertorioDe(string nombre) => nombre switch
    {
        "Barco" => Repertorio("BarcoVikingo", "barco_vikingo", EscenitasBarco),
        "Noria" => Repertorio("Noria", "noria", EscenitasNoria),
        _ => null,
    };

    /// <summary>
    /// Por cada eje, +1 si la malla de Unity está como en el archivo y -1 si sale dada la vuelta: se
    /// mira hacia qué lado del origen cae el centro de la malla en uno y otro. Un eje centrado (la
    /// barca es simétrica a lo ancho) se deja como está.
    /// </summary>
    private static Vector3 Signos(Bounds malla, Vector3 archivoMin, Vector3 archivoMax)
    {
        Vector3 centro = (archivoMin + archivoMax) * 0.5f;
        Vector3 tamano = archivoMax - archivoMin;
        float Eje(float enUnity, float enArchivo, float lado)
            => Mathf.Abs(enArchivo) < lado * 0.05f || Mathf.Sign(enUnity) == Mathf.Sign(enArchivo) ? 1f : -1f;
        return new Vector3(Eje(malla.center.x, centro.x, tamano.x), Eje(malla.center.y, centro.y, tamano.y), Eje(malla.center.z, centro.z, tamano.z));
    }

    /// <summary>La instancia del prefab del pack con ese nombre bajo el parque.</summary>
    private static Transform BuscarInstancia(Transform parque, string prefab)
    {
        foreach (Transform t in parque.GetComponentsInChildren<Transform>(true))
        {
            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
            var origen = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            if (origen != null && origen.name == prefab) return t;
        }
        return null;
    }
}
