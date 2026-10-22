#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Corrige las animaciones humanoides que meten manos o brazos dentro de la cabeza o el
/// torso. Mide la pose real sobre los personajes (la cabeza de estos personajes es enorme
/// comparada con el esqueleto del pack), mueve solo los músculos del brazo que hacen falta
/// en los frames que lo necesitan, suaviza el cambio y hornea una copia en
/// Assets/Art/Animations/Corregidas. Los originales no se tocan nunca.
/// </summary>
public static class CorrectorDeAnimaciones
{
    private const string Menu = "El Sendero/Personajes/Animaciones/";
    private const string CarpetaCorregidas = "Assets/Art/Animations/Corregidas";
    private const string RutaManifest = CarpetaCorregidas + "/ManifestDeCorrecciones.asset";
    private const string InformeMd = "docs/correccion-animaciones.md";

    private const float UmbralCabeza = 0.005f;
    private const float UmbralTorso = 0.02f;
    private const float MargenCabeza = 0.015f;
    private const float MargenTorso = 0.02f;
    private const float PasoJacobiano = 0.05f;
    private const float Amortiguacion = 0.02f;
    private const float MaxDeltaMusculo = 0.9f;
    private const int MaxIteraciones = 4;
    private const int MaxFrames = 1200;

    private static readonly string[] PrefabsRig =
    {
        "Assets/_NPCs/Eldran.prefab",
        "Assets/Prefabs/_WILL.prefab",
        "Assets/Prefabs/_LIAM.prefab",
        "Assets/Prefabs/_ESTELA.prefab",
    };

    private static readonly string[] ControladoresObjetivo =
    {
        "Assets/Art/Characters/Animator/NPC_NoWeapon.controller",
        "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller",
        "Assets/Art/Characters/RPG Tiny Hero Duo/Animator/ArrowsCTRL.controller",
        "Assets/Art/Characters/RPG Tiny Hero Duo/Animator/BowsCTRL.controller",
    };

    private static readonly string[] CarpetasCatalogo =
    {
        "Assets/Art/Characters/RPG Tiny Hero Duo/Animation",
        "Assets/Plugins/Kevin Iglesias/Human Animations/Animations",
        "Assets/Art/Animations/Mixamo",
    };

    private static readonly Dictionary<int, int> LadoDeMusculo = new Dictionary<int, int>();
    private static readonly HashSet<string> RutasUsadas = new HashSet<string>();

    [MenuItem(Menu + "Corregir todas las que lo necesitan", priority = 110)]
    public static void CorregirTodas()
    {
        Ejecutar(false, true);
    }

    [MenuItem(Menu + "Corregir solo las usadas", priority = 111)]
    public static void CorregirSoloUsadas()
    {
        Ejecutar(true, true);
    }

    /// <summary>
    /// Reaudita solo las copias corregidas, para comprobar que la corrección se nota.
    /// </summary>
    [MenuItem(Menu + "Validar copias corregidas", priority = 112)]
    public static void ValidarCopias()
    {
        Ejecutar(false, false);
    }

    [MenuItem(Menu + "Aplicar correcciones a los controllers", priority = 120)]
    public static void Aplicar()
    {
        AplicarCorrecciones(true);
    }

    [MenuItem(Menu + "Revertir correcciones de los controllers", priority = 121)]
    public static void Revertir()
    {
        AplicarCorrecciones(false);
    }

    // ---------------------------------------------------------------------------------------
    // Flujo principal
    // ---------------------------------------------------------------------------------------

    private static void Ejecutar(bool soloUsadas, bool corregir)
    {
        DateTime inicio = DateTime.Now;
        try
        {
            PrepararLadosDeMusculo();

            if (!corregir)
            {
                ValidarCopiasExistentes();
                return;
            }

            bool estabaAplicado = false;
            ManifestDeCorrecciones manifiestoViejo =
                AssetDatabase.LoadAssetAtPath<ManifestDeCorrecciones>(RutaManifest);
            if (manifiestoViejo != null && manifiestoViejo.entradas.Any(e => e.aplicada))
            {
                estabaAplicado = true;
                AplicarCorrecciones(false);
            }

            AsegurarCarpeta();
            BorrarCopiasAntiguas();

            Dictionary<string, ClipDeTrabajo> clips = RecolectarClips(soloUsadas);
            Debug.Log($"[Corrector] Clips a estudiar: {clips.Count}.");

            List<RigDeCorreccion> rigs = new List<RigDeCorreccion>();
            try
            {
                foreach (string ruta in PrefabsRig)
                {
                    try
                    {
                        rigs.Add(new RigDeCorreccion(ruta));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Corrector] Rig {ruta} no válido: {ex.Message}");
                    }
                }

                if (rigs.Count == 0)
                    throw new InvalidOperationException("No hay ningún rig humanoide válido para medir.");

                List<EntradaDeCorreccion> entradas = new List<EntradaDeCorreccion>();
                int hechos = 0;

                AnimationMode.StartAnimationMode();
                try
                {
                    foreach (ClipDeTrabajo trabajo in clips.Values)
                    {
                        hechos++;
                        EditorUtility.DisplayProgressBar(
                            "Corrección de animaciones",
                            trabajo.Clip.name,
                            clips.Count > 0 ? (float)hechos / clips.Count : 0f);

                        try
                        {
                            EntradaDeCorreccion entrada = ProcesarClip(trabajo, rigs);
                            if (entrada != null)
                                entradas.Add(entrada);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[Corrector] '{trabajo.Clip.name}': {ex.Message}");
                        }

                        if (hechos % 50 == 0)
                            Debug.Log($"[Corrector] {hechos}/{clips.Count} ({trabajo.Clip.name})");
                    }
                }
                finally
                {
                    AnimationMode.StopAnimationMode();
                }

                EscribirManifest(entradas);
                EscribirInforme(entradas, inicio);

                if (estabaAplicado)
                    AplicarCorrecciones(true);

                int sinResolver = entradas.Count(e => e.sinResolver);
                Debug.Log($"[Corrector] Fin. Corregidas: {entradas.Count}; sin resolver: {sinResolver}. " +
                          $"Informe: {InformeMd}");
            }
            finally
            {
                foreach (RigDeCorreccion rig in rigs)
                    rig.Dispose();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Corrector] Error: {ex}");
            throw;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>
    /// Mide el clip en todos los rigs, corrige en el que peor está y hornea la copia.
    /// Devuelve null si el clip ya estaba limpio.
    /// </summary>
    private static EntradaDeCorreccion ProcesarClip(ClipDeTrabajo trabajo, List<RigDeCorreccion> rigs)
    {
        float peorCabeza = 0f;
        float peorTorso = 0f;
        float peorGlobal = 0f;
        RigDeCorreccion rigPeor = null;

        foreach (RigDeCorreccion rig in rigs)
        {
            MedicionRig m = MedirClip(rig, trabajo.Clip);
            peorCabeza = Mathf.Max(peorCabeza, m.Cabeza);
            peorTorso = Mathf.Max(peorTorso, m.Torso);

            float local = Mathf.Max(m.Cabeza, m.Torso);
            if (local > peorGlobal)
            {
                peorGlobal = local;
                rigPeor = rig;
            }
        }

        if (peorCabeza < UmbralCabeza && peorTorso < UmbralTorso)
            return null;

        if (rigPeor == null)
            return null;

        Optimizacion optimizacion = Optimizar(rigPeor, trabajo.Clip);
        if (optimizacion == null || optimizacion.Musculos.Count == 0)
        {
            Debug.LogWarning($"[Corrector] '{trabajo.Clip.name}': no se ha podido corregir.");
            return null;
        }

        string rutaDestino = RutaDestino(trabajo);
        AnimationClip copia = Hornear(trabajo.Clip, optimizacion, rutaDestino);

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(rutaDestino);
        AnimationClip copiaCargada = AssetDatabase.LoadAssetAtPath<AnimationClip>(rutaDestino);

        float cabezaDespues = 0f;
        float torsoDespues = 0f;
        string rigDespues = "";
        foreach (RigDeCorreccion rig in rigs)
        {
            MedicionRig m = MedirClip(rig, copiaCargada);
            if (m.Cabeza > cabezaDespues)
            {
                cabezaDespues = m.Cabeza;
                rigDespues = rig.Rig.Nombre;
            }
            if (m.Torso > torsoDespues)
                torsoDespues = m.Torso;
        }

        bool sinResolver = cabezaDespues >= UmbralCabeza || torsoDespues >= UmbralTorso;
        if (sinResolver)
        {
            Debug.LogWarning($"[Corrector] '{trabajo.Clip.name}' sigue con penetración: " +
                             $"cabeza {cabezaDespues * 100f:0.0} cm, torso {torsoDespues * 100f:0.0} cm.");
        }

        return new EntradaDeCorreccion
        {
            original = trabajo.Clip,
            corregida = copiaCargada,
            rutaOriginal = trabajo.Ruta,
            rutaCorregida = rutaDestino,
            usado = trabajo.Usado,
            usos = trabajo.Usos.Count > 0 ? string.Join(" | ", trabajo.Usos.Take(4)) : "",
            rig = rigPeor.Rig.Nombre,
            cabezaAntes = peorCabeza,
            cabezaDespues = cabezaDespues,
            torsoAntes = peorTorso,
            torsoDespues = torsoDespues,
            sinResolver = sinResolver,
        };
    }

    // ---------------------------------------------------------------------------------------
    // Medida
    // ---------------------------------------------------------------------------------------

    private static MedicionRig MedirClip(RigDeCorreccion rig, AnimationClip clip)
    {
        MedicionRig medicion = new MedicionRig { Rig = rig.Rig.Nombre };
        int frames = NumFrames(clip);
        float fps = FpsDe(clip);

        for (int i = 0; i <= frames; i++)
        {
            float t = Mathf.Min(i / fps, clip.length);
            Muestrear(rig, clip, t);
            AuditorDeAnimaciones.MedicionFrame frame = rig.Rig.Cuerpo.Medir();
            if (frame.Cabeza > medicion.Cabeza)
            {
                medicion.Cabeza = frame.Cabeza;
                medicion.TiempoCabeza = t;
            }
            if (frame.Torso > medicion.Torso)
            {
                medicion.Torso = frame.Torso;
                medicion.TiempoTorso = t;
            }
        }

        return medicion;
    }

    // ---------------------------------------------------------------------------------------
    // Optimización por frame
    // ---------------------------------------------------------------------------------------

    private static Optimizacion Optimizar(RigDeCorreccion rig, AnimationClip clip)
    {
        int frames = NumFrames(clip);
        float fps = FpsDe(clip);
        float[][] deltas = new float[frames + 1][];
        for (int i = 0; i <= frames; i++)
            deltas[i] = new float[HumanTrait.MuscleCount];

        HashSet<int> tocados = new HashSet<int>();
        int framesCorregidos = 0;

        for (int i = 0; i <= frames; i++)
        {
            float t = Mathf.Min(i / fps, clip.length);
            Muestrear(rig, clip, t);

            HumanPose pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
            rig.Handler.GetHumanPose(ref pose);

            float[] delta = CorregirPose(rig, pose);
            if (delta == null)
                continue;

            bool alguno = false;
            for (int m = 0; m < delta.Length; m++)
            {
                if (Mathf.Abs(delta[m]) > 1e-4f)
                {
                    deltas[i][m] = delta[m];
                    tocados.Add(m);
                    alguno = true;
                }
            }

            if (alguno)
                framesCorregidos++;
        }

        if (framesCorregidos == 0)
            return null;

        Suavizar(deltas, tocados, clip);
        return new Optimizacion { Deltas = deltas, Musculos = tocados };
    }

    /// <summary>
    /// Ajusta los músculos del brazo para sacar la mano/antebrazo de la cabeza o el torso.
    /// Trabaja con un Jacobiano numérico de las posiciones de mano y codo respecto a los
    /// músculos del hombro, brazo y antebrazo, y resuelve por mínimos cuadrados.
    /// Devuelve el delta final por músculo, o null si no hacía falta tocar nada.
    /// </summary>
    private static float[] CorregirPose(RigDeCorreccion rig, HumanPose poseOriginal)
    {
        float[] baseMusculos = (float[])poseOriginal.muscles.Clone();
        AuditorDeAnimaciones.CuerpoDePrueba cuerpo = rig.Rig.Cuerpo;

        List<Empuje> empujes = CalcularEmpujes(cuerpo);
        if (empujes.Count == 0)
            return null;

        List<int> musculos = new List<int>();
        foreach (Empuje empuje in empujes)
            AnadirMusculosDeLado(musculos, empuje.Lado);
        if (musculos.Count == 0)
            return null;

        float[] actual = (float[])baseMusculos.Clone();
        float penetracionActual = PeorPenetracion(empujes);

        for (int iteracion = 0; iteracion < MaxIteraciones; iteracion++)
        {
            AplicarPose(rig, poseOriginal, actual);
            empujes = CalcularEmpujes(cuerpo);
            if (empujes.Count == 0)
                break;

            // Jacobiano numérico: posición de mano y codo de cada lado respecto a los músculos
            // de ese lado. Los músculos del otro lado no influyen, así que se dejan a cero.
            int columnas = musculos.Count;
            Vector3[] jMano = new Vector3[columnas];
            Vector3[] jCodo = new Vector3[columnas];

            // La referencia es la pose actual, tomada antes de tocar nada: dentro del bucle el
            // rig se queda perturbado en cada columna.
            Vector3[] manoBase = new Vector3[2];
            Vector3[] codoBase = new Vector3[2];
            for (int lado = 0; lado < 2; lado++)
            {
                manoBase[lado] = cuerpo.Mano(lado).position;
                codoBase[lado] = cuerpo.Antebrazo(lado).position;
            }

            for (int c = 0; c < columnas; c++)
            {
                int musculo = musculos[c];
                int lado = LadoDeMusculo[musculo];

                float[] perturbado = (float[])actual.Clone();
                perturbado[musculo] += PasoJacobiano;
                AplicarPose(rig, poseOriginal, perturbado);

                jMano[c] = (cuerpo.Mano(lado).position - manoBase[lado]) / PasoJacobiano;
                jCodo[c] = (cuerpo.Antebrazo(lado).position - codoBase[lado]) / PasoJacobiano;
            }
            AplicarPose(rig, poseOriginal, actual);

            List<float[]> filas = new List<float[]>();
            List<float> terminos = new List<float>();
            foreach (Empuje empuje in empujes)
            {
                Vector3 b = empuje.Direccion * (empuje.Profundidad +
                    (empuje.Cabeza ? MargenCabeza : MargenTorso));

                for (int eje = 0; eje < 3; eje++)
                {
                    float[] fila = new float[columnas];
                    for (int c = 0; c < columnas; c++)
                    {
                        if (LadoDeMusculo[musculos[c]] != empuje.Lado)
                            continue;

                        Vector3 j;
                        if (empuje.Tipo == TipoEmpuje.Mano)
                            j = jMano[c];
                        else if (empuje.Tipo == TipoEmpuje.Codo)
                            j = jCodo[c];
                        else
                            j = Vector3.Lerp(jCodo[c], jMano[c], empuje.T);

                        fila[c] = eje == 0 ? j.x : (eje == 1 ? j.y : j.z);
                    }

                    filas.Add(fila);
                    terminos.Add(eje == 0 ? b.x : (eje == 1 ? b.y : b.z));
                }
            }

            float[] delta = ResolverMinimosCuadrados(filas, terminos, columnas);
            if (delta == null)
                break;

            float maxAbs = 0f;
            foreach (float d in delta)
                maxAbs = Mathf.Max(maxAbs, Mathf.Abs(d));
            if (maxAbs > MaxDeltaMusculo)
            {
                float escala = MaxDeltaMusculo / maxAbs;
                for (int c = 0; c < delta.Length; c++)
                    delta[c] *= escala;
            }

            float[] prueba = (float[])actual.Clone();
            for (int c = 0; c < columnas; c++)
                prueba[musculos[c]] += delta[c];

            AplicarPose(rig, poseOriginal, prueba);
            float penetracionPrueba = PeorPenetracion(CalcularEmpujes(cuerpo));

            if (penetracionPrueba < penetracionActual - 1e-4f)
            {
                actual = prueba;
                penetracionActual = penetracionPrueba;
                if (penetracionActual <= 0f)
                    break;
                continue;
            }

            // El paso entero empeora: probamos con la mitad, un cuarto y un octavo.
            bool aceptado = false;
            for (int intento = 0; intento < 3; intento++)
            {
                for (int c = 0; c < columnas; c++)
                    delta[c] *= 0.5f;

                prueba = (float[])actual.Clone();
                for (int c = 0; c < columnas; c++)
                    prueba[musculos[c]] += delta[c];

                AplicarPose(rig, poseOriginal, prueba);
                penetracionPrueba = PeorPenetracion(CalcularEmpujes(cuerpo));
                if (penetracionPrueba < penetracionActual - 1e-4f)
                {
                    actual = prueba;
                    penetracionActual = penetracionPrueba;
                    aceptado = true;
                    break;
                }
            }

            if (!aceptado)
                break;
        }

        AplicarPose(rig, poseOriginal, baseMusculos);

        float[] resultado = new float[HumanTrait.MuscleCount];
        bool hayCambio = false;
        for (int m = 0; m < resultado.Length; m++)
        {
            resultado[m] = actual[m] - baseMusculos[m];
            if (Mathf.Abs(resultado[m]) > 1e-4f)
                hayCambio = true;
        }

        return hayCambio ? resultado : null;
    }

    private static float[] ResolverMinimosCuadrados(List<float[]> filas, List<float> terminos, int columnas)
    {
        if (filas.Count == 0 || columnas == 0)
            return null;

        float[,] m = new float[columnas, columnas];
        float[] v = new float[columnas];

        for (int f = 0; f < filas.Count; f++)
        {
            float[] fila = filas[f];
            for (int a = 0; a < columnas; a++)
            {
                if (Mathf.Abs(fila[a]) < 1e-8f)
                    continue;
                for (int b = 0; b < columnas; b++)
                    m[a, b] += fila[a] * fila[b];
                v[a] += fila[a] * terminos[f];
            }
        }

        for (int a = 0; a < columnas; a++)
            m[a, a] += Amortiguacion;

        return ResolverSistema(m, v, columnas);
    }

    private static float[] ResolverSistema(float[,] m, float[] v, int n)
    {
        float[,] a = new float[n, n + 1];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                a[i, j] = m[i, j];
            a[i, n] = v[i];
        }

        for (int col = 0; col < n; col++)
        {
            int pivote = col;
            for (int fila = col + 1; fila < n; fila++)
            {
                if (Mathf.Abs(a[fila, col]) > Mathf.Abs(a[pivote, col]))
                    pivote = fila;
            }

            if (Mathf.Abs(a[pivote, col]) < 1e-9f)
                return null;

            if (pivote != col)
            {
                for (int j = 0; j <= n; j++)
                {
                    float temporal = a[col, j];
                    a[col, j] = a[pivote, j];
                    a[pivote, j] = temporal;
                }
            }

            for (int fila = col + 1; fila < n; fila++)
            {
                float factor = a[fila, col] / a[col, col];
                if (Mathf.Abs(factor) < 1e-9f)
                    continue;
                for (int j = col; j <= n; j++)
                    a[fila, j] -= factor * a[col, j];
            }
        }

        float[] x = new float[n];
        for (int i = n - 1; i >= 0; i--)
        {
            float suma = a[i, n];
            for (int j = i + 1; j < n; j++)
                suma -= a[i, j] * x[j];
            x[i] = suma / a[i, i];
        }
        return x;
    }

    // ---------------------------------------------------------------------------------------
    // Empujes (qué está dentro de qué)
    // ---------------------------------------------------------------------------------------

    private static List<Empuje> CalcularEmpujes(AuditorDeAnimaciones.CuerpoDePrueba cuerpo)
    {
        List<Empuje> empujes = new List<Empuje>();
        Vector3 centroCabeza = cuerpo.CentroCabeza();
        Vector3 cadera = cuerpo.Caderas.position;
        Vector3 pecho = cuerpo.Pecho.position;

        for (int lado = 0; lado < 2; lado++)
        {
            Vector3 mano = cuerpo.Mano(lado).position;
            Vector3 codo = cuerpo.Antebrazo(lado).position;

            // --- Cabeza ---
            float mejor = 0f;
            int tipo = -1;
            float tMejor = 0f;
            Vector3 direccion = Vector3.zero;

            float dMano = cuerpo.PenetracionCabeza(mano, cuerpo.RadioMano);
            if (dMano > mejor)
            {
                mejor = dMano;
                tipo = 0;
                direccion = mano - centroCabeza;
            }

            float dCodo = cuerpo.PenetracionCabeza(codo, cuerpo.RadioAntebrazo);
            if (dCodo > mejor)
            {
                mejor = dCodo;
                tipo = 1;
                direccion = codo - centroCabeza;
            }

            float tAntebrazo = MejorTDeSegmento(cuerpo, codo, mano, cuerpo.RadioAntebrazo, out float dAntebrazo);
            if (dAntebrazo > mejor)
            {
                mejor = dAntebrazo;
                tipo = 2;
                tMejor = tAntebrazo;
                direccion = Vector3.Lerp(codo, mano, tMejor) - centroCabeza;
            }

            if (tipo >= 0 && direccion.sqrMagnitude > 1e-8f)
            {
                empujes.Add(new Empuje
                {
                    Lado = lado,
                    Tipo = (TipoEmpuje)tipo,
                    T = tMejor,
                    Punto = tipo == 0 ? mano : (tipo == 1 ? codo : Vector3.Lerp(codo, mano, tMejor)),
                    Direccion = direccion.normalized,
                    Profundidad = mejor,
                    Cabeza = true,
                });
            }

            // --- Torso (solo por encima de la cintura) ---
            float mejorTorso = 0f;
            int tipoTorso = -1;
            float tTorso = 0f;
            Vector3 direccionTorso = Vector3.zero;

            if (mano.y > cadera.y + cuerpo.Altura * 0.10f)
            {
                float d = cuerpo.RadioMano + cuerpo.RadioTorso
                    - DistanciaPuntoSegmento(mano, cadera, pecho);
                if (d > mejorTorso)
                {
                    mejorTorso = d;
                    tipoTorso = 0;
                    direccionTorso = mano - PuntoMasCercanoEnSegmento(mano, cadera, pecho);
                }
            }

            if (codo.y > cadera.y + cuerpo.Altura * 0.10f)
            {
                float d = cuerpo.RadioAntebrazo + cuerpo.RadioTorso
                    - DistanciaPuntoSegmento(codo, cadera, pecho);
                if (d > mejorTorso)
                {
                    mejorTorso = d;
                    tipoTorso = 1;
                    direccionTorso = codo - PuntoMasCercanoEnSegmento(codo, cadera, pecho);
                }
            }

            PuntosMasCercanosSegmentos(codo, mano, cadera, pecho, out Vector3 puntoAntebrazo, out Vector3 puntoEje);
            float dAntebrazoTorso = cuerpo.RadioAntebrazo + cuerpo.RadioTorso
                - Vector3.Distance(puntoAntebrazo, puntoEje);
            if (dAntebrazoTorso > mejorTorso)
            {
                mejorTorso = dAntebrazoTorso;
                tipoTorso = 2;
                Vector3 ab = mano - codo;
                float largo = ab.sqrMagnitude;
                tTorso = largo > 1e-8f
                    ? Mathf.Clamp01(Vector3.Dot(puntoAntebrazo - codo, ab) / largo)
                    : 0f;
                direccionTorso = puntoAntebrazo - puntoEje;
            }

            if (tipoTorso >= 0 && mejorTorso > 0f && direccionTorso.sqrMagnitude > 1e-8f)
            {
                empujes.Add(new Empuje
                {
                    Lado = lado,
                    Tipo = (TipoEmpuje)tipoTorso,
                    T = tTorso,
                    Punto = tipoTorso == 0 ? mano : (tipoTorso == 1 ? codo : puntoAntebrazo),
                    Direccion = direccionTorso.normalized,
                    Profundidad = mejorTorso,
                    Cabeza = false,
                });
            }
        }

        return empujes;
    }

    private static float MejorTDeSegmento(
        AuditorDeAnimaciones.CuerpoDePrueba cuerpo, Vector3 a, Vector3 b, float radio, out float profundidad)
    {
        const int pasos = 6;
        float mejorT = 0f;
        profundidad = 0f;
        for (int i = 0; i <= pasos; i++)
        {
            float t = (float)i / pasos;
            float d = cuerpo.PenetracionCabeza(Vector3.Lerp(a, b, t), radio);
            if (d > profundidad)
            {
                profundidad = d;
                mejorT = t;
            }
        }
        return mejorT;
    }

    private static float PeorPenetracion(List<Empuje> empujes)
    {
        float peor = 0f;
        foreach (Empuje empuje in empujes)
            peor = Mathf.Max(peor, empuje.Profundidad);
        return peor;
    }

    private static void AnadirMusculosDeLado(List<int> lista, int lado)
    {
        foreach (int musculo in MusculosDelLado(lado))
        {
            if (!lista.Contains(musculo))
                lista.Add(musculo);
        }
    }

    private static int[] MusculosDelLado(int lado)
    {
        HumanBodyBones[] huesos = lado == 0
            ? new[] { HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm }
            : new[] { HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm };

        List<int> lista = new List<int>();
        foreach (HumanBodyBones hueso in huesos)
        {
            for (int dof = 0; dof < 3; dof++)
            {
                int musculo = HumanTrait.MuscleFromBone((int)hueso, dof);
                if (musculo >= 0 && !lista.Contains(musculo))
                    lista.Add(musculo);
            }
        }
        return lista.ToArray();
    }

    private static void PrepararLadosDeMusculo()
    {
        LadoDeMusculo.Clear();
        for (int lado = 0; lado < 2; lado++)
        {
            foreach (int musculo in MusculosDelLado(lado))
                LadoDeMusculo[musculo] = lado;
        }
    }

    // ---------------------------------------------------------------------------------------
    // Horneado
    // ---------------------------------------------------------------------------------------

    private static AnimationClip Hornear(AnimationClip original, Optimizacion optimizacion, string rutaDestino)
    {
        AnimationClip copia = UnityEngine.Object.Instantiate(original);
        AssetDatabase.CreateAsset(copia, rutaDestino);

        // CreateAsset pone al objeto el nombre del fichero (`..._corregida`); se devuelve el
        // nombre del clip original para que las búsquedas por nombre del Animator lo encuentren.
        copia.name = original.name;
        EditorUtility.SetDirty(copia);

        AnimationUtility.SetAnimationEvents(copia, AnimationUtility.GetAnimationEvents(original));
        AnimationUtility.SetAnimationClipSettings(copia, AnimationUtility.GetAnimationClipSettings(original));

        int frames = NumFrames(original);
        float fps = FpsDe(original);

        foreach (int musculo in optimizacion.Musculos)
        {
            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(
                "", typeof(Animator), HumanTrait.MuscleName[musculo]);
            AnimationCurve curva = AnimationUtility.GetEditorCurve(original, binding);

            List<Keyframe> claves = new List<Keyframe>();
            if (curva != null && curva.length > 0)
            {
                foreach (Keyframe clave in curva.keys)
                {
                    int frame = Mathf.Clamp(Mathf.RoundToInt(clave.time * fps), 0, frames);
                    float valor = clave.value + optimizacion.Deltas[frame][musculo];
                    Keyframe nueva = new Keyframe(clave.time, valor, clave.inTangent, clave.outTangent);
                    nueva.inWeight = clave.inWeight;
                    nueva.outWeight = clave.outWeight;
                    nueva.weightedMode = clave.weightedMode;
                    claves.Add(nueva);
                }
            }
            else
            {
                for (int frame = 0; frame <= frames; frame++)
                {
                    if (Mathf.Abs(optimizacion.Deltas[frame][musculo]) > 1e-5f)
                        claves.Add(new Keyframe(frame / fps, optimizacion.Deltas[frame][musculo]));
                }
            }

            if (claves.Count > 0)
                AnimationUtility.SetEditorCurve(copia, binding, new AnimationCurve(claves.ToArray()));
        }

        EditorUtility.SetDirty(copia);
        return copia;
    }

    // ---------------------------------------------------------------------------------------
    // Suavizado
    // ---------------------------------------------------------------------------------------

    private static void Suavizar(float[][] deltas, HashSet<int> musculos, AnimationClip clip)
    {
        int n = deltas.Length;
        if (n <= 1)
            return;

        bool bucle = AnimationUtility.GetAnimationClipSettings(clip).loopTime;

        foreach (int musculo in musculos)
        {
            float[] original = new float[n];
            for (int i = 0; i < n; i++)
                original[i] = deltas[i][musculo];

            // Primero dilata (que el pico no se pierda al suavizar) y luego pasa una media.
            // Se conserva el valor de mayor magnitud con su signo: los deltas negativos
            // (bajar el brazo) son tan válidos como los positivos.
            float[] dilatado = new float[n];
            for (int i = 0; i < n; i++)
            {
                float mejor = 0f;
                for (int k = -2; k <= 2; k++)
                {
                    float valor = Leer(original, i + k, bucle);
                    if (Mathf.Abs(valor) > Mathf.Abs(mejor))
                        mejor = valor;
                }
                dilatado[i] = mejor;
            }

            for (int i = 0; i < n; i++)
            {
                float suma = 0f;
                for (int k = -2; k <= 2; k++)
                    suma += Leer(dilatado, i + k, bucle);
                deltas[i][musculo] = suma / 5f;
            }

            if (bucle)
                deltas[n - 1][musculo] = deltas[0][musculo];
        }
    }

    private static float Leer(float[] valores, int indice, bool bucle)
    {
        int n = valores.Length;
        if (bucle)
            return valores[((indice % n) + n) % n];
        return valores[Mathf.Clamp(indice, 0, n - 1)];
    }

    // ---------------------------------------------------------------------------------------
    // Recolección de clips
    // ---------------------------------------------------------------------------------------

    private static Dictionary<string, ClipDeTrabajo> RecolectarClips(bool soloUsadas)
    {
        Dictionary<string, ClipDeTrabajo> clips = new Dictionary<string, ClipDeTrabajo>();

        foreach (string ruta in ControladoresObjetivo)
        {
            AnimatorController controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(ruta);
            if (controlador == null)
            {
                Debug.LogWarning($"[Corrector] No encuentro el controller {ruta}.");
                continue;
            }

            foreach (AnimatorControllerLayer capa in controlador.layers)
            {
                HashSet<AnimatorStateMachine> visitadas = new HashSet<AnimatorStateMachine>();
                RecorrerEstados(capa.stateMachine, controlador.name, capa.name, clips, visitadas);
            }
        }

        if (!soloUsadas)
        {
            foreach (string carpeta in CarpetasCatalogo)
            {
                if (!AssetDatabase.IsValidFolder(carpeta))
                {
                    Debug.LogWarning($"[Corrector] No existe la carpeta {carpeta}.");
                    continue;
                }

                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { carpeta }))
                {
                    string ruta = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ruta))
                    {
                        if (asset is AnimationClip clip)
                            AnadirClipCatalogo(clips, clip, ruta);
                    }
                }

                foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { carpeta }))
                {
                    string ruta = AssetDatabase.GUIDToAssetPath(guid);
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta);
                    if (clip != null)
                        AnadirClipCatalogo(clips, clip, ruta);
                }
            }
        }

        return clips;
    }

    private static void AnadirClipCatalogo(
        Dictionary<string, ClipDeTrabajo> clips, AnimationClip clip, string ruta)
    {
        if (clip.empty || clip.length <= 0f || clip.name.StartsWith("__preview__"))
            return;
        if (!clip.humanMotion)
            return;

        string clave = Clave(clip);
        if (clips.ContainsKey(clave))
            return;

        clips[clave] = new ClipDeTrabajo
        {
            Clip = clip,
            Clave = clave,
            Ruta = ruta,
            Usado = false,
        };
    }

    private static void RecorrerEstados(
        AnimatorStateMachine maquina,
        string controlador,
        string capa,
        Dictionary<string, ClipDeTrabajo> clips,
        HashSet<AnimatorStateMachine> visitadas)
    {
        if (maquina == null || !visitadas.Add(maquina))
            return;

        foreach (ChildAnimatorState hijo in maquina.states)
        {
            if (hijo.state == null)
                continue;
            AnadirMotion(hijo.state.motion, controlador, capa, hijo.state.name, clips);
        }

        foreach (ChildAnimatorStateMachine sub in maquina.stateMachines)
            RecorrerEstados(sub.stateMachine, controlador, capa, clips, visitadas);
    }

    private static void AnadirMotion(
        Motion motion,
        string controlador,
        string capa,
        string estado,
        Dictionary<string, ClipDeTrabajo> clips)
    {
        if (motion == null)
            return;

        if (motion is AnimationClip clip)
        {
            if (clip.empty || clip.length <= 0f || clip.name.StartsWith("__preview__"))
                return;

            string clave = Clave(clip);
            if (!clips.TryGetValue(clave, out ClipDeTrabajo trabajo))
            {
                trabajo = new ClipDeTrabajo
                {
                    Clip = clip,
                    Clave = clave,
                    Ruta = AssetDatabase.GetAssetPath(clip),
                    Usado = true,
                };
                clips[clave] = trabajo;
            }

            trabajo.Usado = true;
            string uso = $"{controlador} / {capa} / {estado}";
            if (!trabajo.Usos.Contains(uso))
                trabajo.Usos.Add(uso);
            return;
        }

        if (motion is BlendTree arbol)
        {
            foreach (ChildMotion hijo in arbol.children)
                AnadirMotion(hijo.motion, controlador, capa, estado, clips);
        }
    }

    private static string Clave(AnimationClip clip)
    {
        return AssetDatabase.GetAssetPath(clip) + "|" + clip.name;
    }

    // ---------------------------------------------------------------------------------------
    // Manifiesto, informe y validación
    // ---------------------------------------------------------------------------------------

    private static void EscribirManifest(List<EntradaDeCorreccion> entradas)
    {
        if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(RutaManifest)))
            AssetDatabase.DeleteAsset(RutaManifest);

        ManifestDeCorrecciones manifiesto = ScriptableObject.CreateInstance<ManifestDeCorrecciones>();
        manifiesto.fecha = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        manifiesto.entradas = entradas;
        AssetDatabase.CreateAsset(manifiesto, RutaManifest);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Corrector] Manifiesto: {RutaManifest} ({entradas.Count} entradas).");
    }

    private static void EscribirInforme(List<EntradaDeCorreccion> entradas, DateTime inicio)
    {
        string raiz = Path.GetDirectoryName(Application.dataPath);
        string ruta = Path.Combine(raiz, InformeMd);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta));

        StringBuilder md = new StringBuilder(16384);
        md.AppendLine("# Corrección de animaciones humanoides");
        md.AppendLine();
        md.AppendLine($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm}. Duración: {(DateTime.Now - inicio).TotalMinutes:0.0} min.");
        md.AppendLine();
        md.AppendLine("Las copias corregidas están en `Assets/Art/Animations/Corregidas/`.");
        md.AppendLine("Los originales no se han tocado.");
        md.AppendLine();
        md.AppendLine("## Resumen");
        md.AppendLine();
        md.AppendLine($"- Copias generadas: **{entradas.Count}**.");
        md.AppendLine($"- De clips usados por los controllers: **{entradas.Count(e => e.usado)}**.");
        md.AppendLine($"- De clips sin usar (catálogo): **{entradas.Count(e => !e.usado)}**.");
        md.AppendLine($"- Sin resolver del todo: **{entradas.Count(e => e.sinResolver)}**.");
        md.AppendLine();
        md.AppendLine("## Correcciones");
        md.AppendLine();
        md.AppendLine("| Clip | Usado | Cabeza antes | Cabeza después | Torso antes | Torso después | Rig | Estado |");
        md.AppendLine("|---|---|---:|---:|---:|---:|---|---|");
        foreach (EntradaDeCorreccion e in entradas
                     .OrderByDescending(x => x.cabezaAntes)
                     .ThenByDescending(x => x.torsoAntes))
        {
            md.AppendLine($"| {Escapar(e.original != null ? e.original.name : e.rutaOriginal)} | " +
                          $"{(e.usado ? "sí" : "no")} | {Cm(e.cabezaAntes)} | {Cm(e.cabezaDespues)} | " +
                          $"{Cm(e.torsoAntes)} | {Cm(e.torsoDespues)} | {e.rig} | " +
                          $"{(e.sinResolver ? "Sin resolver" : "Corregida")} |");
        }
        md.AppendLine();
        md.AppendLine("## Cómo funciona");
        md.AppendLine();
        md.AppendLine("- Se mide el clip en Eldran, Will, Liam y Estela y se elige el rig donde peor está.");
        md.AppendLine("- El elipsoide de la cabeza sale de las mallas activas de cabeza y pelo (sin cintas ni sombreros), con un 8 % de margen hacia dentro.");
        md.AppendLine("- El torso es una cápsula desde la cadera hasta el pecho, con radio proporcional a la altura.");
        md.AppendLine("- En los frames con penetración se resuelve, por mínimos cuadrados, cuánto hay que mover los músculos de hombro, brazo y antebrazo para sacar la mano sin cambiar nada más.");
        md.AppendLine("- El cambio se dilata y se suaviza en el tiempo para que no dé tirones.");
        md.AppendLine("- El resultado se escribe como curvas de músculo del clip humanoide; el resto del clip se queda igual.");
        md.AppendLine();

        File.WriteAllText(ruta, md.ToString(), new UTF8Encoding(false));
        Debug.Log($"[Corrector] Informe: {InformeMd}");
    }

    private static void ValidarCopiasExistentes()
    {
        ManifestDeCorrecciones manifiesto =
            AssetDatabase.LoadAssetAtPath<ManifestDeCorrecciones>(RutaManifest);
        if (manifiesto == null || manifiesto.entradas.Count == 0)
        {
            Debug.LogWarning($"[Corrector] No hay manifiesto en {RutaManifest}.");
            return;
        }

        List<RigDeCorreccion> rigs = new List<RigDeCorreccion>();
        try
        {
            foreach (string ruta in PrefabsRig)
            {
                try
                {
                    rigs.Add(new RigDeCorreccion(ruta));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Corrector] Rig {ruta} no válido: {ex.Message}");
                }
            }

            AnimationMode.StartAnimationMode();
            try
            {
                foreach (EntradaDeCorreccion entrada in manifiesto.entradas)
                {
                    if (entrada.corregida == null)
                        continue;

                    float cabeza = 0f;
                    float torso = 0f;
                    foreach (RigDeCorreccion rig in rigs)
                    {
                        MedicionRig m = MedirClip(rig, entrada.corregida);
                        cabeza = Mathf.Max(cabeza, m.Cabeza);
                        torso = Mathf.Max(torso, m.Torso);
                    }

                    Debug.Log($"[Corrector] {entrada.corregida.name}: cabeza {Cm(cabeza)}, torso {Cm(torso)} " +
                              $"({(cabeza >= UmbralCabeza || torso >= UmbralTorso ? "sin resolver" : "ok")})");
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }
        }
        finally
        {
            foreach (RigDeCorreccion rig in rigs)
                rig.Dispose();
        }
    }

    // ---------------------------------------------------------------------------------------
    // Aplicar / revertir en los controllers
    // ---------------------------------------------------------------------------------------

    private static void AplicarCorrecciones(bool aplicar)
    {
        ManifestDeCorrecciones manifiesto =
            AssetDatabase.LoadAssetAtPath<ManifestDeCorrecciones>(RutaManifest);
        if (manifiesto == null)
        {
            Debug.LogError($"[Corrector] No hay manifiesto en {RutaManifest}. Ejecuta antes la corrección.");
            return;
        }

        Dictionary<AnimationClip, AnimationClip> mapa = new Dictionary<AnimationClip, AnimationClip>();
        foreach (EntradaDeCorreccion entrada in manifiesto.entradas)
        {
            if (entrada.original == null || entrada.corregida == null)
                continue;
            if (aplicar)
                mapa[entrada.original] = entrada.corregida;
            else
                mapa[entrada.corregida] = entrada.original;
        }

        int referencias = 0;
        int controllers = 0;
        foreach (string ruta in ControladoresObjetivo)
        {
            AnimatorController controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(ruta);
            if (controlador == null)
                continue;

            int cambios = 0;
            foreach (AnimatorControllerLayer capa in controlador.layers)
            {
                HashSet<AnimatorStateMachine> visitadas = new HashSet<AnimatorStateMachine>();
                cambios += ReemplazarEnEstados(capa.stateMachine, mapa, visitadas);
            }

            if (cambios > 0)
            {
                EditorUtility.SetDirty(controlador);
                controllers++;
                referencias += cambios;
            }
        }

        foreach (EntradaDeCorreccion entrada in manifiesto.entradas)
        {
            if (aplicar && entrada.original != null && mapa.ContainsKey(entrada.original))
                entrada.aplicada = true;
            if (!aplicar && entrada.corregida != null && mapa.ContainsKey(entrada.corregida))
                entrada.aplicada = false;
        }

        EditorUtility.SetDirty(manifiesto);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Corrector] {(aplicar ? "Aplicadas" : "Revertidas")} {referencias} referencias " +
                  $"en {controllers} controllers.");
    }

    private static int ReemplazarEnEstados(
        AnimatorStateMachine maquina,
        Dictionary<AnimationClip, AnimationClip> mapa,
        HashSet<AnimatorStateMachine> visitadas)
    {
        if (maquina == null || !visitadas.Add(maquina))
            return 0;

        int cambios = 0;
        foreach (ChildAnimatorState hijo in maquina.states)
        {
            if (hijo.state == null)
                continue;
            Motion nuevo = ReemplazarMotion(hijo.state.motion, mapa, ref cambios);
            if (nuevo != hijo.state.motion)
                hijo.state.motion = nuevo;
        }

        foreach (ChildAnimatorStateMachine sub in maquina.stateMachines)
            cambios += ReemplazarEnEstados(sub.stateMachine, mapa, visitadas);

        return cambios;
    }

    private static Motion ReemplazarMotion(
        Motion motion, Dictionary<AnimationClip, AnimationClip> mapa, ref int cambios)
    {
        if (motion == null)
            return null;

        if (motion is AnimationClip clip)
        {
            if (mapa.TryGetValue(clip, out AnimationClip nuevo))
            {
                cambios++;
                return nuevo;
            }
            return clip;
        }

        if (motion is BlendTree arbol)
        {
            ChildMotion[] hijos = arbol.children;
            bool tocado = false;
            for (int i = 0; i < hijos.Length; i++)
            {
                Motion nuevo = ReemplazarMotion(hijos[i].motion, mapa, ref cambios);
                if (nuevo != hijos[i].motion)
                {
                    hijos[i].motion = nuevo;
                    tocado = true;
                }
            }
            if (tocado)
                arbol.children = hijos;
            return arbol;
        }

        return motion;
    }

    // ---------------------------------------------------------------------------------------
    // Utilidades
    // ---------------------------------------------------------------------------------------

    private static void AsegurarCarpeta()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art/Animations"))
            throw new InvalidOperationException("No existe Assets/Art/Animations.");
        if (!AssetDatabase.IsValidFolder(CarpetaCorregidas))
            AssetDatabase.CreateFolder("Assets/Art/Animations", "Corregidas");
    }

    private static void BorrarCopiasAntiguas()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { CarpetaCorregidas }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.DeleteAsset(ruta);
        }
        RutasUsadas.Clear();
    }

    private static string RutaDestino(ClipDeTrabajo trabajo)
    {
        string nombre = Sanear(trabajo.Clip.name);
        string ruta = $"{CarpetaCorregidas}/{nombre}_corregida.anim";
        if (RutasUsadas.Contains(ruta))
        {
            string guid = AssetDatabase.AssetPathToGUID(trabajo.Ruta);
            string sufijo = string.IsNullOrEmpty(guid) ? Mathf.Abs(trabajo.Clave.GetHashCode()).ToString() : guid.Substring(0, 8);
            ruta = $"{CarpetaCorregidas}/{nombre}_{sufijo}_corregida.anim";
        }
        RutasUsadas.Add(ruta);
        return ruta;
    }

    private static string Sanear(string nombre)
    {
        char[] invalidos = Path.GetInvalidFileNameChars();
        StringBuilder sb = new StringBuilder(nombre.Length);
        foreach (char c in nombre)
            sb.Append(Array.IndexOf(invalidos, c) >= 0 ? '_' : c);
        return sb.ToString();
    }

    private static int NumFrames(AnimationClip clip)
    {
        float fps = FpsDe(clip);
        return Mathf.Clamp(Mathf.RoundToInt(clip.length * fps), 1, MaxFrames);
    }

    private static float FpsDe(AnimationClip clip)
    {
        return clip.frameRate > 0.01f ? clip.frameRate : 30f;
    }

    private static void Muestrear(RigDeCorreccion rig, AnimationClip clip, float t)
    {
        AplicarPose(rig, rig.PoseReposo, rig.MusculosReposo);
        AnimationMode.BeginSampling();
        try
        {
            AnimationMode.SampleAnimationClip(rig.Rig.Go, clip, t);
        }
        finally
        {
            AnimationMode.EndSampling();
        }
    }

    private static void AplicarPose(RigDeCorreccion rig, HumanPose basePose, float[] musculos)
    {
        HumanPose pose = new HumanPose
        {
            muscles = musculos,
            bodyPosition = basePose.bodyPosition,
            bodyRotation = basePose.bodyRotation,
        };
        rig.Handler.SetHumanPose(ref pose);
    }

    private static Vector3 PuntoMasCercanoEnSegmento(Vector3 punto, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float largo = ab.sqrMagnitude;
        if (largo < 1e-8f)
            return a;
        float t = Mathf.Clamp01(Vector3.Dot(punto - a, ab) / largo);
        return a + ab * t;
    }

    private static float DistanciaPuntoSegmento(Vector3 punto, Vector3 a, Vector3 b)
    {
        return Vector3.Distance(punto, PuntoMasCercanoEnSegmento(punto, a, b));
    }

    private static void PuntosMasCercanosSegmentos(
        Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 c1, out Vector3 c2)
    {
        Vector3 d1 = q1 - p1;
        Vector3 d2 = q2 - p2;
        Vector3 r = p1 - p2;

        float a = Vector3.Dot(d1, d1);
        float e = Vector3.Dot(d2, d2);
        float f = Vector3.Dot(d2, r);
        float s;
        float t;

        if (a < 1e-8f && e < 1e-8f)
        {
            c1 = p1;
            c2 = p2;
            return;
        }

        if (a < 1e-8f)
        {
            s = 0f;
            t = Mathf.Clamp01(f / e);
        }
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e < 1e-8f)
            {
                t = 0f;
                s = Mathf.Clamp01(-c / a);
            }
            else
            {
                float b = Vector3.Dot(d1, d2);
                float denominador = a * e - b * b;
                s = Mathf.Abs(denominador) > 1e-8f
                    ? Mathf.Clamp01((b * f - c * e) / denominador)
                    : 0f;
                t = (b * s + f) / e;
                if (t < 0f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else if (t > 1f)
                {
                    t = 1f;
                    s = Mathf.Clamp01((b - c) / a);
                }
            }
        }

        c1 = p1 + d1 * s;
        c2 = p2 + d2 * t;
    }

    private static string Cm(float metros)
    {
        return $"{metros * 100f:0.0} cm";
    }

    private static string Escapar(string texto)
    {
        return (texto ?? "").Replace("|", "\\|").Replace(";", ",");
    }

    // ---------------------------------------------------------------------------------------
    // Tipos auxiliares
    // ---------------------------------------------------------------------------------------

    private enum TipoEmpuje
    {
        Mano = 0,
        Codo = 1,
        Antebrazo = 2,
    }

    private class Empuje
    {
        public int Lado;
        public TipoEmpuje Tipo;
        public float T;
        public Vector3 Punto;
        public Vector3 Direccion;
        public float Profundidad;
        public bool Cabeza;
    }

    private class ClipDeTrabajo
    {
        public AnimationClip Clip;
        public string Clave;
        public string Ruta;
        public bool Usado;
        public List<string> Usos = new List<string>();
    }

    private class MedicionRig
    {
        public string Rig;
        public float Cabeza;
        public float TiempoCabeza;
        public float Torso;
        public float TiempoTorso;
    }

    private class Optimizacion
    {
        public float[][] Deltas;
        public HashSet<int> Musculos;
    }

    private sealed class RigDeCorreccion : IDisposable
    {
        public readonly AuditorDeAnimaciones.RigDePrueba Rig;
        public readonly HumanPoseHandler Handler;
        public readonly HumanPose PoseReposo;
        public readonly float[] MusculosReposo;

        public RigDeCorreccion(string rutaPrefab)
        {
            Rig = new AuditorDeAnimaciones.RigDePrueba(rutaPrefab);
            Handler = new HumanPoseHandler(Rig.Animator.avatar, Rig.Animator.transform);

            HumanPose pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
            Handler.GetHumanPose(ref pose);
            PoseReposo = pose;
            MusculosReposo = (float[])pose.muscles.Clone();
        }

        public void Dispose()
        {
            Handler.Dispose();
            Rig.Dispose();
        }
    }
}
#endif
