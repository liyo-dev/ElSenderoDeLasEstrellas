using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Kits.Puzzles;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Templo del Coro en cubos grises, al paso de «La función del guardián»: la sala de la afinación con
/// el Tenor en recortes encima de la puerta; la estrofa 1 por el corredor-pentagrama (notas que caen,
/// losas que se hunden y el hueco de la antorcha con su puente); la estrofa 2 en la nave del órgano
/// (el coro en los muros, columnas que salen del suelo y la puerta de madera que se quema). Todo va a
/// la partitura medida sobre la toma (Tools/Musica/Templo del Coro/partitura.json), que se importa
/// como <see cref="MusicScore"/>.
///
/// Corriendo a 6 m/s, un segundo de la canción son 6 m de recorrido: la Z de cada peligro es
/// 6 × (segundo − 21,5), menos lo que se quedan parados el recuadro y la sombra en cada calderón
/// (ver <see cref="Z"/>). Se rehace entera cada vez. Ver INC-1393.
/// </summary>
public static class TemploDelCoroBuilder
{
    public const string RutaEscena = "Assets/Scenes/Worlds/TemploDelCoro.unity";
    private const string NombreDeLaEscena = "TemploDelCoro";
    private const string CarpetaDatos = "Assets/Scenes/Worlds/TemploDelCoro_data";
    private const string RutaStart = "Assets/Scenes/Systems/Start.unity";
    private const string CarpetaRecortes = "Assets/ElSendero/Content/TemploDelCoro/Recortes";
    private const string CarpetaTexturas = "Assets/ElSendero/Content/TemploDelCoro/Texturas";
    private const string RutaCancion = "Assets/Audio/Music/Zonas/Templo del Coro - La funcion del guardian.mp3";
    private const string CarpetaPartitura = "Assets/ElSendero/Data/TemploDelCoro";
    private const string RutaPartitura = CarpetaPartitura + "/Partitura del guardian.asset";
    private const string PartituraJson = "Tools/Musica/Templo del Coro/partitura.json";
    private const string RutaPum = "Assets/Audio/SFX_Puzzles/SFX_Coro_Pum.wav";
    private const string RutaOrganoGrave = "Assets/Audio/SFX_Puzzles/SFX_Coro_Organo_Grave.wav";
    private const string RutaFogonazo = "Assets/Audio/SFX_Puzzles/SFX_Coro_Fogonazo.wav";

    private const float Velocidad = 6f;        // m/s corriendo
    private const string VozDeLaAfinacion = "COMPAS_AFINACION";
    // De las tres notas grandes antes de «¡salta!», solo las dos últimas (35,24 y 36,08 s).
    private const float DesdeLasGrandes = 34.5f;
    // Golpes del nivel grabados a mano con la K: hoy marcan los pilares de la nave del órgano (sustituyen a
    // los calculados). Donde no hay, queda lo calculado.
    private static readonly List<float> Golpes = new List<float>();
    private const string VozGolpe = "GOLPE";
    // Segundos de las losas que se hunden (las grabadas o las de la partitura).
    private static readonly List<float> SegundosDeLosas = new List<float>();
    // Presentación del Tenor: golpes calculados sobre la toma; se sustituyen por los grabados a mano.
    private static readonly float[] IntroPumCalculado = { 0.093f, 2.74f };
    private static readonly float[] IntroEscalaCalculada = { 0.81f, 1.02f, 1.22f, 1.43f, 1.65f, 1.86f, 2.07f, 2.27f, 2.49f };
    // Dos «pam» antes de «¿se me oye?» (10,4–11 s), callados mientras lo dice, y siguen después.
    private static readonly float[] IntroPamCalculado = { 9.81f, 10.24f, 11.16f, 11.63f, 12.03f, 12.42f, 12.91f, 13.32f };
    private static readonly float[] IntroDoReMiCalculado = { 13.73f, 14.56f, 15.44f };


    private const float SegundoDelCorredor = 21.5f;
    private const float PixelesPorMetro = 160f;
    private const float UnidadDelModelo = 4f / PixelesPorMetro; // metros por unidad del dibujo
    private const float AltoDeLosMuros = 9f;
    private const float AnchoDelCorredor = 10f;
    private const float AdelantoEnCarrera = 1.25f;
    private const string IdDelAncla = "InicioTemploDelCoro";

    private static Transform _pentagramaDeLaSala;
    // Los recortes importados (para el atrezo de lo que sale del suelo).
    private static Dictionary<string, Pieza> _piezas = new Dictionary<string, Pieza>();
    private static Material _gris, _grisOscuro, _linea, _sombra, _aviso, _humo, _sombraQuePersigue, _brasa, _recuadro, _madera, _onda, _regalo, _telon, _luz, _runas, _rayo, _lagrima, _rosa;

    // Calderones del recorrido (X: Z donde se paran el recuadro y la sombra; Y: segundos). Ver PrepararCalderones.
    private static readonly List<Vector2> Calderones = new List<Vector2>();
    private const float SegundoDeOsPresento = 44.588f;
    private const float SegundoDeLaPuerta = 62.6f;
    // Golpe final de la canción («¡Ay, mi suelo!»): se hunde el suelo y se cae al vacío.
    private const float SegundoDeLaMeta = 131.709f;
    private const float FinDeLaCancion = 133.2f;
    // El puente («¡Cerrad las puertas!»): encierro en el fondo del escenario con los coristas.
    private const float SegundoDelPuente = 97.1f;
    private const float SegundoDeLaFraseTriste = 113f;
    // Respiros del estribillo para la magia de Will (luz) y de Liam (mente): X llega, Y segundos.
    private static readonly Vector2 RespiroDeWill = new Vector2(82.1f, 2.6f);
    private static readonly Vector2 RespiroDeLiam = new Vector2(89.6f, 3f);
    private const float LargoDelHuecoDeMagia = 7.8f;
    // Momentos del final: lo grabado (T, Y, U, O) o lo calculado sobre la partitura.
    private static float Truenos, Bravo, Lagrimita, Volvereis;
    // Tramos en los que el recuadro va más deprisa (X: Z de inicio; Y: segundos; Z: metros de más).
    private static readonly List<Vector3> Acelerones = new List<Vector3>();
    // Ventanas de la canción en las que el recuadro está parado (para no poner peligros ahí).
    private static readonly List<Vector2> Respiros = new List<Vector2>();
    private const string VozPuertas = "PUERTAS";
    // Segundos tras «¡Rayos y truenos!» en los que el recuadro sigue parado (para salir del encierro).
    private const float SalidaDelFinal = 1.6f;
    private const string VozLatigazoDelFinal = "LATIGAZO_DEL_FINAL";
    private const string VozRosas = "ROSAS";
    private const string VozLagrima = "LAGRIMA";
    private const string VozCoristaQueAparece = "CORISTA_DEL_FINAL";
    // El estribillo («¡Corred, corred, que empieza la función!»), en el escenario de teatro.
    private const float DesdeElEstribillo = 67.889f;
    private const float HastaElEstribillo = 97f;
    // El triple de 1:13 (clave, nota, pilar), tal cual lo dejó la partitura.
    private const float SegundoDelPilarDelTriple = 73.59f;
    private const float SegundoDeLaNotaDelTriple = 73.172f;
    // Los dos «corred» marcan la entrada; los tres últimos rematan «suelto, nunca, más».
    private static readonly float[] AcentosDeLaLetra = { DesdeElEstribillo, 68.667f, 94.232f, 95.016f, 95.823f };
    private static readonly float[] LadosDeLaLetra = { -2.4f, 2.4f, -2.4f, 0f, 2.4f };
    private const string VozAcentoDeLaLetra = "ACENTO_DE_LA_LETRA";
    private const string VozPlatillo = "PLATILLO_DEL_ESTRIBILLO";
    private const string VozPirotecnia = "PIROTECNIA";
    private const string VozGolpeCentral = "GOLPE_CENTRAL";
    private const float AnchoGolpeCentral = 4.2f;
    private const string VozNotaDelEstribillo = "NOTA_DEL_ESTRIBILLO";
    private const string VozPilarDelEstribillo = "PILAR_DEL_ESTRIBILLO";
    private static readonly List<float> Claves = new List<float>();
    // Lo que se tira en el estribillo, grabado a mano con la L: una nota a por el jugador en cada una.
    private static readonly List<float> Lanzas = new List<float>();
    // Lo que cae del telar, grabado con la M: por turnos, un saco de arena, una luna y una estrella de cartón.
    private static readonly List<float> DelTelar = new List<float>();
    private static readonly string[] VocesDelTelar = { "TELAR_SACO", "TELAR_LUNA", "TELAR_ESTRELLA" };
    private static readonly string[] PiezasDelTelar = { "telar_saco", "telar_luna", "telar_estrella" };
    // El salto y el dash del final (grabados con la J y la H) y las semicorcheas del «pi pi pi» de antes
    // (grabadas con la P): cada una cae delante y rompe una losa; el vacío que dejan se cruza saltando
    // en el salto y con el dash en el aire en el dash.
    private static float SaltoFinal, DashFinal;
    private static readonly List<float> Semicorcheas = new List<float>();
    // Las semicorcheas en grupos: cada grupo rompe un hueco (un compás, un salto); el último, el vacío del final.
    private static readonly List<List<float>> GruposDeSemicorcheas = new List<List<float>>();
    private const string VozSemicorchea = "SEMICORCHEA";
    private const float LargoDelVacioFinal = 6.6f;
    private const float LargoDeUnHuecoDeSemicorcheas = 3.9f;
    // Las semicorcheas de estos segundos antes del salto final rompen el vacío del final; las de antes, un hueco por compás.
    private const float SegundosDelVacioFinal = 2.5f;
    // Metros por delante del centro del recuadro donde empieza un hueco de semicorcheas, en el segundo en que se salta.
    private const float BordeDelVacioFinal = 1.5f;

    // ── El camino ──
    // Una sola línea (la x del pasillo en cada segundo de la canción) por la que se pasa el nivel entero sin
    // que dé nada: los peligros fijos de medio pasillo (latigazos, llamaradas, pirotecnia) la empujan al
    // otro lado y todo lo demás cae o sale fuera de ella, siempre en el mismo sitio. No se marca en el
    // suelo: es para montar el nivel; se lee por el orden de los peligros y se aprende de memoria.
    private const float PasoDelCamino = 0.05f;
    private const float MargenDelCamino = 1f;
    private const float VelocidadLateralDelCamino = 7f;
    private const float MitadDelPasillo = 5f;
    private static readonly float[] Carriles = { -6f, -3.3f, 0f, 3.3f, 6f };
    // Por dónde va el camino de base, por tramos (desde ese segundo, esa x): largos a un lado o al otro,
    // con frases de la canción; los respiros y el encierro, por el centro.
    // Tramos del camino modificados: obligan a cruzar de un lado a otro y pasar por obstáculos y saltos,
    // evitando que el jugador pueda ir siempre por la derecha sin enterarse.
    private static readonly Vector2[] TramosDelCamino =
    {
        new Vector2(21.5f, 4f), new Vector2(24f, -4f), new Vector2(28.0f, 0f), new Vector2(31.2f, 4f), new Vector2(35.0f, -4f), new Vector2(38.5f, 0f),
        new Vector2(67.9f, 4f), new Vector2(72.0f, -4f), new Vector2(75.8f, 2f), new Vector2(80.0f, -4f), new Vector2(84.7f, 4f), new Vector2(89.0f, 0f), new Vector2(92.6f, -4f),
        new Vector2(120.6f, -4f), new Vector2(123.0f, 4f), new Vector2(125.5f, -2f), new Vector2(128.9f, 0f),
    };
    private static float[] _camino = System.Array.Empty<float>();
    private static float[] _tiempos = System.Array.Empty<float>();

    private struct PeligroDelMapa { public float t, x, z, medio; public string que; }
    private static readonly List<PeligroDelMapa> Mapa = new List<PeligroDelMapa>();
    private const string RutaDelMapa = "Tools/Musica/Templo del Coro/mapa_del_camino.json";

    /// <summary>X del camino en ese segundo de la canción.</summary>
    private static float XDelCamino(float t)
    {
        if (_camino.Length == 0) return 0f;
        float f = Mathf.Clamp(t / PasoDelCamino, 0f, _camino.Length - 1.001f);
        int i = (int)f;
        return Mathf.Lerp(_camino[i], _camino[i + 1], f - i);
    }

    /// <summary>Lo menos que se acerca el camino a <paramref name="x"/> entre esos dos segundos.</summary>
    private static float DistanciaAlCamino(float x, float desde, float hasta)
    {
        float menor = float.MaxValue;
        for (float t = desde; t <= hasta; t += PasoDelCamino) menor = Mathf.Min(menor, Mathf.Abs(XDelCamino(t) - x));
        return menor;
    }

    /// <summary>
    /// X de un carril para algo de <paramref name="medio"/> m de medio ancho que golpea en el segundo
    /// <paramref name="t"/> (y se queda <paramref name="dura"/> s): el carril libre más cercano al camino, a
    /// un lado en los compases pares y al otro en los impares, para que enmarque el camino sin pisarlo.
    /// </summary>
    private static float XFueraDelCamino(float t, float medio, ref int turno, float dura = 0.5f)
    {
        float camino = XDelCamino(t);
        float mejor = float.NaN, otro = float.NaN, peor = 0f, lejos = -1f;
        // Todo lo de un mismo compás, al mismo lado; el siguiente compás, al otro: se lee el compás.
        bool aLaIzquierda = (_tiempos.Length > 1 ? CompasDe(_tiempos, t) : turno) % 2 == 0;
        turno++;
        float mitad = t >= Truenos ? AnchoDelFinal * 0.5f : MitadDelPasillo;
        foreach (float c in Carriles)
        {
            if (Mathf.Abs(c) + medio > mitad + 0.5f) continue;
            float d = DistanciaAlCamino(c, t - 0.4f, t + dura);
            if (d > lejos) { lejos = d; peor = c; }
            if (d < medio + MargenDelCamino) continue;
            bool izquierda = c < camino;
            if (izquierda == aLaIzquierda) { if (float.IsNaN(mejor) || Mathf.Abs(c - camino) < Mathf.Abs(mejor - camino)) mejor = c; }
            else if (float.IsNaN(otro) || Mathf.Abs(c - camino) < Mathf.Abs(otro - camino)) otro = c;
        }
        return !float.IsNaN(mejor) ? mejor : !float.IsNaN(otro) ? otro : peor;
    }

    private static void AlMapa(float t, float x, float z, float medio, string que) =>
        Mapa.Add(new PeligroDelMapa { t = t, x = x, z = z, medio = medio, que = que });

    // Los peligros fijos de medio pasillo: dónde está cada uno (los mismos números al trazar el camino y al montarlos).
    private static float XDeLlama(int i) => ((i / 2) % 2 == 0 ? -1f : 1f) * 2.3f;
    // La pirotecnia y los latigazos (del bombardino, del estribillo y del final), en el lado contrario al camino cuando
    // este va por un lado (empujan a quedarse en él); con el camino en el centro, un lado y otro por turnos.
    private static float XDePirotecnia(float t, int i) => LadoContrario(t, i % 2 == 0 ? -1f : 1f) * 2.3f;
    private static float XDePirotecniaDelFinal(float t, int i) => LadoContrario(t, i % 2 == 0 ? 1f : -1f) * 2.3f;
    private static float XDeLatigazo(float t, int i) => LadoContrario(t, (i / 2) % 2 == 0 ? -1f : 1f) * 2.25f;
    private static float LadoContrario(float t, float siVaPorElCentro)
    {
        float camino = XBaseDelCamino(t);
        return Mathf.Abs(camino) >= 1.5f ? -Mathf.Sign(camino) : siVaPorElCentro;
    }

    /// <summary>X de base del camino en ese segundo (por tramos; en el centro en respiros y encierro).</summary>
    private static float XBaseDelCamino(float t)
    {
        if (t < SegundoDelCorredor || EnUnRespiro(t) || (t >= SegundoDelPuente && t < Truenos + SalidaDelFinal)) return 0f;
        // Cruzando un hueco de magia, por la plataforma del centro.
        foreach (var respiro in new[] { RespiroDeWill, RespiroDeLiam })
            if (t >= respiro.x && t < SegundoAlPasarElHueco(respiro)) return 0f;
        float x = 0f;
        foreach (var tramo in TramosDelCamino) if (t >= tramo.x) x = tramo.y;
        return x;
    }
    private const float XDelFogonazo = -2.2f;
    // El barrido del suelo junto al practicable: del muro izquierdo al borde de la tarima.
    private static float MedioDelBarrido => (BordeDelPracticable + MitadDelPasillo) * 0.5f;
    private static float XDelBarrido => BordeDelPracticable - MedioDelBarrido;

    /// <summary>Hasta cuándo dura el practicable: un poco antes del respiro de Liam (y su hueco de magia).</summary>
    private static float HastaElPracticable => RespiroDeLiam.x - 0.4f;

    /// <summary>Segundos de los barridos junto al practicable: uno cada dos tiempos, dejando subir al empezar.</summary>
    private static List<float> BarridosDelPracticable()
    {
        var tiempos = LeerTiempos();
        var barridos = new List<float>();
        int primero = -1;
        for (int i = 0; i < tiempos.Length; i++)
        {
            if (tiempos[i] < DesdeElPracticable + 0.6f || tiempos[i] > HastaElPracticable - 0.3f) continue;
            if (primero < 0) primero = i;
            if ((i - primero) % 2 == 0) barridos.Add(tiempos[i]);
        }
        return barridos;
    }

    /// <summary>
    /// Traza el camino: de base, cada dos compases un carril del patrón (más abierto en el escenario), en
    /// el centro en los respiros y el encierro; donde hay un peligro de medio pasillo, al otro lado, con un
    /// margen; y sin pasar de <see cref="VelocidadLateralDelCamino"/> de lado a lado.
    /// </summary>
    private static void PrepararCamino(MusicScore partitura)
    {
        Mapa.Clear();
        _tiempos = LeerTiempos();
        int n = Mathf.CeilToInt(FinDeLaCancion / PasoDelCamino) + 2;
        var x = new float[n];
        var fijo = new bool[n];
        for (int i = 0; i < n; i++) x[i] = XBaseDelCamino(i * PasoDelCamino);
        // Sobre los practicables, el camino va por la tarima que queda de cada fila (de la anterior a esta).
        for (int f = 0; f < Practicables.Count; f++)
        {
            float desde = f == 0 ? Truenos + SalidaDelFinal : (Practicables[f - 1].t + Practicables[f].t) * 0.5f;
            float hasta = f + 1 < Practicables.Count ? (Practicables[f].t + Practicables[f + 1].t) * 0.5f : FinDeLosPracticables;
            for (float u = desde; u < hasta; u += PasoDelCamino)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt(u / PasoDelCamino), 0, n - 1);
                x[i] = Practicables[f].x;
                fijo[i] = true;
            }
        }
        void Fijar(float t, float centro, float medio, float dura, float aviso = 0.15f)
        {
            // Solo lo aparta lo justo: si ya iba al otro lado, se queda donde iba.
            float lado = centro > 0f ? centro - medio - MargenDelCamino : centro + medio + MargenDelCamino;
            for (float u = t - aviso; u <= t + dura; u += PasoDelCamino)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt(u / PasoDelCamino), 0, n - 1);
                if (fijo[i]) continue;
                x[i] = centro > 0f ? Mathf.Min(x[i], lado) : Mathf.Max(x[i], lado);
                fijo[i] = true;
            }
        }
        var pums = DeLaPartitura(partitura, VozBomboPum, DesdeLosLatigazos, DesdeLasGrandesGrabadas);
        for (int i = 0; i < pums.Count; i++) Fijar(pums[i], XDeLatigazo(pums[i], i), 2.75f, 0.35f);
        var llamas = DeLaPartitura(partitura, VozLlama, DesdeLasLlamas - 0.1f, SegundoDeLaPuerta);
        for (int i = 0; i < llamas.Count; i++) Fijar(llamas[i], XDeLlama(i), 2.65f, 0.3f);
        Fijar(Fogonazo, XDelFogonazo, 3.5f, 0.7f);
        var piro = DeLaPartitura(partitura, VozPirotecnia, DesdeElEstribillo, HastaElEstribillo);
        for (int i = 0; i < piro.Count; i++) Fijar(piro[i], XDePirotecnia(piro[i], i), 2.4f, 0.45f);
        var centrales = DeLaPartitura(partitura, VozGolpeCentral, DesdeElEstribillo, HastaElEstribillo);
        for (int i = 0; i < centrales.Count; i++)
        {
            float t = centrales[i];
            // El centro queda cerrado. El foco abre una salida a un lado con el aviso.
            Fijar(t, LadoContrario(t - 0.75f, i % 2 == 0 ? 1f : -1f) * 0.01f, AnchoGolpeCentral * 0.5f, 0.45f, 0.46f);
        }
        for (int i = 0; i < AcentosDeLaLetra.Length; i++)
        {
            float lado = LadosDeLaLetra[i];
            if (lado == 0f) lado = LadoContrario(AcentosDeLaLetra[i] - 0.4f, 1f) * 0.01f;
            Fijar(AcentosDeLaLetra[i], lado, 1.45f, 0.2f, 0.35f);
        }
        foreach (float t in BarridosDelPracticable()) Fijar(t, XDelBarrido, MedioDelBarrido, 0.35f);
        var latigazos = DeLaPartitura(partitura, VozLatigazoDelFinal, Truenos, SegundoDeLaMeta);
        for (int i = 0; i < latigazos.Count; i++) Fijar(latigazos[i], XDeLatigazo(latigazos[i], i), 2.75f, 0.35f);
        var piroFinal = DeLaPartitura(partitura, VozPirotecnia, FinDeLosPracticables, SegundoDeLaMeta);
        for (int i = 0; i < piroFinal.Count; i++) Fijar(piroFinal[i], XDePirotecniaDelFinal(piroFinal[i], i), 2.4f, 0.45f);

        float paso = VelocidadLateralDelCamino * PasoDelCamino;
        for (int i = 1; i < n; i++) if (!fijo[i]) x[i] = Mathf.Clamp(x[i], x[i - 1] - paso, x[i - 1] + paso);
        for (int i = n - 2; i >= 0; i--) if (!fijo[i]) x[i] = Mathf.Clamp(x[i], x[i + 1] - paso, x[i + 1] + paso);
        // El foco anticipa los cambios de carril y los cruza a velocidad alcanzable.
        _camino = new float[n];
        int anticipo = Mathf.CeilToInt(0.24f / PasoDelCamino);
        for (int i = 1; i < n; i++)
            _camino[i] = Mathf.MoveTowards(_camino[i - 1], x[Mathf.Min(i + anticipo, n - 1)], paso);
    }

    /// <summary>
    /// Guarda el mapa del camino (el camino y cada peligro, con su segundo y su sitio) para dibujarlo y
    /// revisar que el nivel está ordenado sin jugarlo.
    /// </summary>
    private static void GuardarMapa()
    {
        var texto = new System.Text.StringBuilder();
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        texto.Append("{\n \"camino\": [");
        bool primero = true;
        for (float t = SegundoDelCorredor; t <= FinDeLaCancion; t += 0.1f)
        {
            texto.Append(primero ? "" : ",").AppendFormat(ci, "[{0:0.00},{1:0.00},{2:0.00}]", t, XDelCamino(t), Z(t));
            primero = false;
        }
        texto.Append("],\n \"peligros\": [");
        for (int i = 0; i < Mapa.Count; i++)
        {
            var m = Mapa[i];
            texto.Append(i == 0 ? "\n  " : ",\n  ").AppendFormat(ci, "{{\"t\":{0:0.000},\"x\":{1:0.00},\"z\":{2:0.00},\"medio\":{3:0.00},\"que\":\"{4}\"}}",
                                                              m.t, m.x, m.z, m.medio, m.que);
        }
        texto.Append("]\n}\n");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", RutaDelMapa), texto.ToString());
    }

    /// <summary>Z por la que pasa el centro del recuadro en ese segundo de la canción (con los calderones).</summary>
    private static float Z(float segundo)
    {
        // Igual que TramoDelCoro.ZEnSegundo: calderones y acelerones en el orden en que se llega a ellos.
        float z = Velocidad * (segundo - SegundoDelCorredor);
        int c = 0, a = 0;
        while (c < Calderones.Count || a < Acelerones.Count)
        {
            if (a >= Acelerones.Count || (c < Calderones.Count && Calderones[c].x <= Acelerones[a].x))
            {
                var k = Calderones[c++];
                if (z <= k.x) return z;
                z = Mathf.Max(k.x, z - Velocidad * k.y);
            }
            else
            {
                var k = Acelerones[a++];
                if (z <= k.x) return z;
                z += k.z * Mathf.Clamp01((z - k.x) / Mathf.Max(0.01f, Velocidad * k.y));
            }
        }
        return z;
    }

    /// <summary>
    /// El hueco de la antorcha (estrofa 1) y la puerta de madera del órgano (fin de la estrofa 2): un
    /// respiro en cada uno para cambiar de personaje y disparar. El del hueco dura hasta que el Tenor
    /// empieza «Os presento a mi órgano» (44,59 s); el de la puerta, 3 s.
    /// </summary>
    private static void PrepararCalderones()
    {
        Calderones.Clear();
        Acelerones.Clear();
        Respiros.Clear();
        float zDelHueco = ZDelHueco - 2f;
        float llega = SegundoDelCorredor + zDelHueco / Velocidad;
        Calderones.Add(new Vector2(zDelHueco, SegundoDeOsPresento - llega));
        Respiros.Add(new Vector2(llega, SegundoDeOsPresento));
        // Ante la puerta, un respiro más largo: llega el tercer regalito y hay que devolverlo.
        Calderon(SegundoDeLaPuerta - 2f / Velocidad, 4.5f);

        // Los momentos del final: lo grabado o lo calculado (sobre el pulso de la partitura).
        var tiempos = LeerTiempos();
        _tiempos = tiempos;
        var grabadas = GrabadorDeMarcas.Leer();
        Truenos = Momento(grabadas, tiempos, GrabadorDeMarcas.Truenos, 118.97f);
        Bravo = Momento(grabadas, tiempos, GrabadorDeMarcas.Bravo, TiempoCercano(tiempos, 125.5f, 0));
        Lagrimita = Momento(grabadas, tiempos, GrabadorDeMarcas.Lagrimita, TiempoCercano(tiempos, Bravo, 4));
        Volvereis = Momento(grabadas, tiempos, GrabadorDeMarcas.Volvereis, TiempoCercano(tiempos, Bravo, 8));
        SaltoFinal = Momento(grabadas, tiempos, GrabadorDeMarcas.Salto, TiempoCercano(tiempos, 131.35f, 0), ultimo: true);
        DashFinal = Mathf.Max(SaltoFinal + 0.2f, Momento(grabadas, tiempos, GrabadorDeMarcas.Dash, TiempoCercano(tiempos, 131.75f, 0), ultimo: true));
        PrepararSemicorcheas(grabadas, tiempos);

        // Estribillo: un respiro en cada hueco de magia (Will y Liam) para cambiar de personaje y disparar.
        Calderon(RespiroDeWill.x, RespiroDeWill.y);
        Calderon(RespiroDeLiam.x, RespiroDeLiam.y);
        // El puente y la frase triste, encerrados en el fondo del escenario hasta «¡Rayos y truenos!», y un
        // poco más: el telón se abre y el grupo (que estaba quieto, quizá al fondo del encierro, casi en la
        // sombra) tiene ese rato para ponerse delante antes de que el recuadro vuelva a andar.
        Calderon(SegundoDelPuente, Truenos + SalidaDelFinal - SegundoDelPuente);
        // «Se me escapa una lagrimita…»: la ola viene detrás y el recuadro corre más hasta «¿Volveréis?».
        Acelerones.Add(new Vector3(Z(Lagrimita), Mathf.Max(0.5f, Volvereis - Lagrimita), 4f));
    }

    /// <summary>
    /// Las semicorcheas grabadas con la P en el final (de «¡Bravo!» al salto final), llevadas a la rejilla
    /// fina de la partitura (cabe la semicorchea y el tresillo), y en grupos: las de los últimos
    /// <see cref="SegundosDelVacioFinal"/> s antes del salto, el vacío del final; las de antes, un hueco por
    /// compás (los de menos de tres notas se juntan con el siguiente). Sin grabar, las 12 semicorcheas de
    /// los tres tiempos de antes del salto, para el vacío del final.
    /// </summary>
    private static void PrepararSemicorcheas(List<GrabadorDeMarcas.Marca> grabadas, float[] tiempos)
    {
        Semicorcheas.Clear();
        GruposDeSemicorcheas.Clear();
        foreach (var m in grabadas)
        {
            if (m.voz != GrabadorDeMarcas.Semicorchea) continue;
            float t = AlDoceavoDeTiempo(tiempos, m.segundo - (m.enBruto ? RetrasoDeLaManoEnBruto : RetrasoDeLaMano));
            if (t > Bravo - 0.3f && t < SaltoFinal - 0.05f) Semicorcheas.Add(t);
        }
        Semicorcheas.Sort();
        for (int i = Semicorcheas.Count - 1; i > 0; i--)
            if (Semicorcheas[i] - Semicorcheas[i - 1] < 0.03f) Semicorcheas.RemoveAt(i);
        if (Semicorcheas.Count == 0)
        {
            int salto = System.Array.BinarySearch(tiempos, SaltoFinal);
            if (salto < 0) salto = ~salto;
            if (salto < 3 || salto >= tiempos.Length) return;
            for (int i = salto - 3; i < salto; i++)
                for (int q = 0; q < 4; q++)
                    Semicorcheas.Add(tiempos[i] + (tiempos[i + 1] - tiempos[i]) * q * 0.25f);
        }
        var vacio = new List<float>();
        List<float> grupo = null;
        int compasDelGrupo = -1;
        foreach (float t in Semicorcheas)
        {
            if (t >= SaltoFinal - SegundosDelVacioFinal) { vacio.Add(t); continue; }
            int compas = CompasDe(tiempos, t);
            if (grupo == null || (compas != compasDelGrupo && grupo.Count >= 3))
            {
                grupo = new List<float>();
                GruposDeSemicorcheas.Add(grupo);
            }
            grupo.Add(t);
            compasDelGrupo = compas;
        }
        if (GruposDeSemicorcheas.Count > 0 && GruposDeSemicorcheas[GruposDeSemicorcheas.Count - 1].Count < 3)
        {
            vacio.InsertRange(0, GruposDeSemicorcheas[GruposDeSemicorcheas.Count - 1]);
            GruposDeSemicorcheas.RemoveAt(GruposDeSemicorcheas.Count - 1);
        }
        if (vacio.Count > 0) GruposDeSemicorcheas.Add(vacio);
    }

    /// <summary>Número de compás (desde 0) en el que suena ese segundo.</summary>
    private static int CompasDe(float[] tiempos, float t)
    {
        int i = System.Array.BinarySearch(tiempos, t + 0.01f);
        if (i < 0) i = ~i - 1;
        return Mathf.Max(0, i) / 4;
    }

    /// <summary>El doceavo de tiempo de la canción más cercano a ese segundo (vale para semicorcheas y tresillos).</summary>
    private static float AlDoceavoDeTiempo(float[] tiempos, float t)
    {
        int i = System.Array.BinarySearch(tiempos, t);
        if (i < 0) i = ~i - 1;
        i = Mathf.Clamp(i, 0, tiempos.Length - 2);
        float a = tiempos[i], paso = (tiempos[i + 1] - a) / 12f;
        return a + Mathf.Clamp(Mathf.Round((t - a) / paso), 0f, 12f) * paso;
    }

    /// <summary>El recuadro llega a su sitio en ese segundo y se queda parado esos segundos.</summary>
    private static void Calderon(float llega, float segundos)
    {
        Calderones.Add(new Vector2(Z(llega), segundos));
        Respiros.Add(new Vector2(llega, llega + segundos));
    }

    private static bool EnUnRespiro(float t)
    {
        foreach (var r in Respiros) if (t >= r.x - 0.2f && t <= r.y + 0.3f) return true;
        return false;
    }

    private static float[] LeerTiempos()
    {
        string json = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", PartituraJson);
        if (!File.Exists(json)) return new float[] { 0f, 1f };
        var datos = JsonUtility.FromJson<PartituraJsonDto>(File.ReadAllText(json));
        return datos.tiempos_s != null && datos.tiempos_s.Length > 1 ? datos.tiempos_s : new float[] { 0f, 1f };
    }

    /// <summary>El tiempo de la partitura más cercano a ese segundo, más <paramref name="tiemposDespues"/> tiempos.</summary>
    private static float TiempoCercano(float[] tiempos, float t, int tiemposDespues)
    {
        int i = System.Array.BinarySearch(tiempos, t);
        if (i < 0) { i = ~i; if (i > 0 && (i >= tiempos.Length || t - tiempos[i - 1] < tiempos[i] - t)) i--; }
        return tiempos[Mathf.Clamp(i + tiemposDespues, 0, tiempos.Length - 1)];
    }

    [MenuItem("El Sendero/Escenario/Templo del Coro: montar (INC-1393)")]
    public static void Montar()
    {
        var temploAbierto = SceneManager.GetSceneByPath(RutaEscena);
        if (EditorApplication.isPlayingOrWillChangePlaymode || (temploAbierto.IsValid() && temploAbierto.isDirty))
        {
            OrdenesDelEditor.Avisar("Templo del Coro", "Para montar el templo, sal de Play y guarda primero los cambios de su escena.");
            return;
        }

        PrepararCalderones();
        PrepararPracticables();
        var partitura = ImportarPartitura();
        if (partitura == null) return;
        var piezas = ImportarRecortes();
        _piezas = piezas;
        PrepararCamino(partitura);

        var activaAntes = SceneManager.GetActiveScene();
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        // Unity conserva al menos una escena abierta. La nueva permite cerrar el templo si estaba solo.
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var abierta = SceneManager.GetSceneAt(i);
            if (abierta.path == RutaEscena) EditorSceneManager.CloseScene(abierta, true);
        }
        CrearCarpeta(CarpetaDatos);
        if (!EditorSceneManager.SaveScene(escena, RutaEscena))
        {
            OrdenesDelEditor.Avisar("Templo del Coro", "Unity no pudo preparar la escena para el montaje.");
            return;
        }
        SceneManager.SetActiveScene(escena);

        CrearMateriales();
        Ambiente(escena);

        var raiz = new GameObject("Templo del Coro").transform;
        SceneManager.MoveGameObjectToScene(raiz.gameObject, escena);

        var reloj = CrearReloj(raiz, partitura);
        CrearSala(raiz, escena);
        CrearTenor(raiz, reloj, piezas, "El Tenor (recortes)", new Vector3(0f, 5.9f, -4.35f), 1f);
        var sueloQueSePierde = CrearCorredor(raiz, reloj);
        CrearLoQueCae(raiz, escena, reloj, partitura, piezas);
        CrearLosasQueSeHunden(raiz, escena, reloj, partitura);
        var montaje = new Montaje();
        var hueco = CrearHuecoConAntorcha(raiz, escena, reloj);
        montaje.Plataformas.AddRange(hueco.Plataformas);
        montaje.Receptores.Add(hueco.Antorcha);
        CrearNaveDelOrgano(raiz, reloj, partitura, piezas);
        CrearRegalitos(raiz, reloj, piezas, montaje);
        CrearEscenario(raiz, reloj, partitura, piezas, montaje, sueloQueSePierde);
        CrearEncierro(raiz, reloj, piezas, montaje);
        CrearFinal(raiz, reloj, partitura, piezas, montaje);
        var sombra = CrearSombra(raiz, escena);
        CrearOla(raiz, sombra, reloj, montaje);
        CrearLoQueCaeDelTelar(raiz, reloj, partitura, piezas);
        CrearCombas(raiz, reloj);
        SinVuelo(raiz);
        GuardarMapa();

        var nav = raiz.gameObject.AddComponent<NavMeshSurface>();
        nav.collectObjects = CollectObjects.Children;
        nav.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        nav.layerMask = LayerMask.GetMask(EscenaJugable.CapaDelSuelo);
        EscenaJugable.HornearNavMesh(nav);

        var entrada = new Vector3(0f, 0.15f, -20f);
        EscenaJugable.ColocarGrupo(escena, entrada, Vector3.forward);
        var ancla = new GameObject("Entrada al templo (ancla)");
        ancla.transform.SetParent(raiz, false);
        ancla.transform.SetPositionAndRotation(entrada, Quaternion.identity);
        ancla.AddComponent<SpawnAnchor>().anchorId = IdDelAncla;
        var grupo = new GameObject("Grupo entero al empezar");
        grupo.transform.SetParent(raiz, false);
        grupo.AddComponent<GrupoAlEmpezar>();

        var doReMi = CrearIntro(raiz, reloj);
        CrearCoristasDeLaSala(raiz, reloj, piezas, partitura);
        CrearBombardino(raiz, reloj, partitura);
        var camara = CrearCamara(raiz, doReMi);
        PonerRef(camara, "tenorDeLaDespedida", montaje.TenorDelFinal);
        var recuadro = CrearRecuadro(raiz, 12f, AdelantoEnCarrera);
        var foco = CrearLuces(raiz, reloj, recuadro);
        PonerPlanosDeEscena(camara, montaje);
        CrearTramo(raiz, reloj, sombra, entrada, camara, recuadro, montaje, sueloQueSePierde.ToArray(), foco);

        EditorSceneManager.MarkSceneDirty(escena);
        if (!EditorSceneManager.SaveScene(escena, RutaEscena))
        {
            OrdenesDelEditor.Avisar("Templo del Coro", "Unity no pudo guardar el templo montado. La escena queda abierta.");
            return;
        }
        var start = SceneManager.GetSceneByPath(RutaStart);
        if (start.IsValid() && start.isLoaded && EscenaJugable.PermitirInterfazDeJuego(start, NombreDeLaEscena) > 0)
            EditorSceneManager.SaveScene(start, RutaStart);
        AssetDatabase.SaveAssets();

        if (activaAntes.IsValid() && activaAntes.isLoaded) SceneManager.SetActiveScene(activaAntes);
        if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(escena, true);

        const string informe = "[Templo del Coro] Montado: afinación, estrofa 1 (notas, losas, antorcha y puente) y " +
                               "estrofa 2 (nave del órgano), regalitos, estribillo en el escenario (huecos de luz y de mente), puente con los coristas, frase triste y final. Abre TemploDelCoro.unity y dale a Play.";
        Debug.Log(informe);
        if (!Application.isBatchMode) OrdenesDelEditor.Avisar("Templo del Coro", informe);
    }

    // ── Datos: partitura y recortes ─────────────────────────────────────────────────────────

    [System.Serializable] private sealed class EventoJson { public string voz; public float segundo; public int compas; public float tiempo; }
    [System.Serializable] private sealed class PartituraJsonDto { public float[] tiempos_s; public int primer_tiempo_de_compas; public EventoJson[] eventos; }

    private static MusicScore ImportarPartitura()
    {
        string json = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", PartituraJson);
        if (!File.Exists(json)) { Debug.LogError($"[Templo del Coro] Falta la partitura: {json}"); return null; }
        var datos = JsonUtility.FromJson<PartituraJsonDto>(File.ReadAllText(json));
        var cancion = AssetDatabase.LoadAssetAtPath<AudioClip>(RutaCancion);
        if (cancion == null) { Debug.LogError($"[Templo del Coro] Falta la canción: {RutaCancion}"); return null; }

        CrearCarpeta(CarpetaPartitura);
        var partitura = AssetDatabase.LoadAssetAtPath<MusicScore>(RutaPartitura);
        if (partitura == null)
        {
            partitura = ScriptableObject.CreateInstance<MusicScore>();
            AssetDatabase.CreateAsset(partitura, RutaPartitura);
        }
        var marcas = new List<MusicScore.Cue>(datos.eventos.Length);
        foreach (var e in datos.eventos)
            if (e.voz != "BASE")
                marcas.Add(new MusicScore.Cue { time = e.segundo, voice = e.voz, measure = e.compas, beat = e.tiempo });
        // En la afinación, una nota cada dos compases, en su primer tiempo: cae a tiempo con la canción
        // sin cansar (los golpes de la voz del calentamiento van sueltos, fuera del pulso).
        for (int i = datos.primer_tiempo_de_compas; i < datos.tiempos_s.Length; i += 8)
        {
            float t = datos.tiempos_s[i];
            if (t < 1f) continue;
            if (t >= SegundoDelCorredor - 0.1f) break;
            marcas.Add(new MusicScore.Cue { time = t, voice = VozDeLaAfinacion, measure = (i - datos.primer_tiempo_de_compas) / 4 + 1, beat = 1f });
        }
        // Lo grabado a mano con el grabador de marcas sustituye a lo calculado de cada cosa.
        var grabadas = GrabadorDeMarcas.Leer();
        Golpes.Clear();
        foreach (var m in grabadas)
            if (m.voz == GrabadorDeMarcas.Golpe) Golpes.Add(Corregido(datos.tiempos_s, m));
        // Sin repetidos (dos pulsaciones en el mismo medio tiempo) y en orden.
        Golpes.Sort();
        for (int i = Golpes.Count - 1; i > 0; i--)
            if (Golpes[i] - Golpes[i - 1] < 0.05f) Golpes.RemoveAt(i);
        foreach (float t in Golpes) marcas.Add(new MusicScore.Cue { time = t, voice = VozGolpe });
        // Las notas grandes antes de «¡saltad!»: las dos de la partitura y las que se hayan grabado antes.
        var grandes = new List<float>();
        foreach (var e in datos.eventos)
            if (e.voz == "LOSA" && e.segundo >= DesdeLasGrandes && e.segundo < HastaLasGrandes) grandes.Add(e.segundo);
        foreach (float t in Golpes)
            if (t >= DesdeLasGrandesGrabadas && t < DesdeLasGrandes) grandes.Add(t);
        foreach (float t in grandes) marcas.Add(new MusicScore.Cue { time = t, voice = VozNotaGrande });
        // El bombardino de la estrofa 1 («pum» en el 1 y el 3, «pam» en el 2 y el 4), sobre el pulso medido.
        for (int i = 0; i < datos.tiempos_s.Length; i++)
        {
            float t = datos.tiempos_s[i];
            if (t < SegundoDelCorredor - 0.1f) continue;
            if (t >= DesdeLasGrandesGrabadas) break;
            int tiempo = ((i - datos.primer_tiempo_de_compas) % 4 + 4) % 4 + 1;
            if (tiempo % 2 == 0 || !EnTramoDeComba(t))
                marcas.Add(new MusicScore.Cue { time = t, voice = tiempo % 2 == 1 ? VozBomboPum : VozBomboPam, beat = tiempo });
        }
        // Las losas de «¡saltad!», las tres de la partitura.
        SegundosDeLosas.Clear();
        foreach (var e in datos.eventos)
            if (e.voz == "LOSA" && e.segundo >= DesdeLasLosas && e.segundo < HastaLasLosas) SegundosDeLosas.Add(e.segundo);
        AnadirIntro(marcas, grabadas, GrabadorDeMarcas.IntroPum, IntroPumCalculado);
        AnadirIntro(marcas, grabadas, GrabadorDeMarcas.IntroEscala, IntroEscalaCalculada);
        AnadirIntro(marcas, grabadas, GrabadorDeMarcas.IntroPam, IntroPamCalculado);
        AnadirIntro(marcas, grabadas, GrabadorDeMarcas.IntroDoReMi, IntroDoReMiCalculado);
        // Momentos de la letra en la estrofa 2: lo grabado (5-8) o lo calculado. Lo grabado llega con el
        // retraso de la mano: se adelanta y se lleva al medio tiempo, como los golpes.
        OsPresento = Momento(grabadas, datos.tiempos_s, GrabadorDeMarcas.OsPresento, SegundoDeOsPresento);
        QueVienen = Momento(grabadas, datos.tiempos_s, GrabadorDeMarcas.QueVienen, 16.5f);
        Pulmones = Momento(grabadas, datos.tiempos_s, GrabadorDeMarcas.Pulmones, 46.736f);
        // Del fogonazo cuenta el último golpe grabado (el de «…ya os lo he dicho»).
        Fogonazo = Momento(grabadas, datos.tiempos_s, GrabadorDeMarcas.Fogonazo, 52.471f, ultimo: true);
        Musical.Clear();
        foreach (var m in grabadas)
            if (m.voz == GrabadorDeMarcas.Musical) Musical.Add(Corregido(datos.tiempos_s, m));
        if (Musical.Count == 0) { Musical.Add(59.242f); Musical.Add(61.286f); }
        Musical.Sort();
        for (int i = 0; i < Musical.Count; i++)
            marcas.Add(new MusicScore.Cue { time = Musical[i], voice = i == Musical.Count - 1 ? VozMusicalFin : VozMusical });
        Lista(grabadas, datos.tiempos_s, GrabadorDeMarcas.Regalito, Ensenar, RegalitoCalculado);
        Lista(grabadas, datos.tiempos_s, GrabadorDeMarcas.Tomad, Tomad, null);
        // Sin «¡tomad!» grabado (o sin uno por regalito), se lanza tres tiempos después de enseñarlo.
        if (Tomad.Count < Mathf.Min(Ensenar.Count, 2))
        {
            Tomad.Clear();
            foreach (float t in Ensenar) Tomad.Add(t + 1.24f);
        }
        // El tercero («si me lo devolvéis a tiempo»), con el grupo parado ante la puerta: se enseña nada
        // más llegar y se lanza un compás después. Se devuelve con la defensa y abre la puerta.
        if (Ensenar.Count < 3)
        {
            float llega = SegundoDelCorredor + Calderones[1].x / Velocidad + Calderones[0].y;
            Ensenar.Add(AlMedioTiempo(datos.tiempos_s, llega + 0.3f));
        }
        if (Tomad.Count < 3) Tomad.Add(AlMedioTiempo(datos.tiempos_s, Ensenar[2] + 1.65f));
        marcas.Add(new MusicScore.Cue { time = Pulmones, voice = GrabadorDeMarcas.Pulmones });
        marcas.Add(new MusicScore.Cue { time = Fogonazo, voice = GrabadorDeMarcas.Fogonazo });
        // Una llamarada en los tiempos fuertes. Los golpes de la letra tienen su propio espacio.
        for (int i = 0; i < datos.tiempos_s.Length; i++)
        {
            float t = datos.tiempos_s[i];
            if (t < DesdeLasLlamas) continue;
            if (t > SegundoDeLaPuerta - 0.5f) break;
            if ((i - datos.primer_tiempo_de_compas) % 2 != 0) continue;
            if (Cerca(t, Fogonazo) || Cerca(t, Musical)) continue;
            if (Cerca(t, Tomad)) continue;
            marcas.Add(new MusicScore.Cue { time = t, voice = VozLlama });
        }
        Lista(grabadas, datos.tiempos_s, GrabadorDeMarcas.Lanza, Lanzas, null);
        Lista(grabadas, datos.tiempos_s, GrabadorDeMarcas.DelTelar, DelTelar, null);
        foreach (float t in Semicorcheas) marcas.Add(new MusicScore.Cue { time = t, voice = VozSemicorchea });
        foreach (float t in BarridosDelPracticable()) marcas.Add(new MusicScore.Cue { time = t, voice = VozDelBarrido });
        foreach (float t in MomentosDeComba()) marcas.Add(new MusicScore.Cue { time = t, voice = VozComba });
        foreach (var fila in Practicables)
        {
            marcas.Add(new MusicScore.Cue { time = fila.rompeLasOtras, voice = VozRompePracticable });
            marcas.Add(new MusicScore.Cue { time = fila.rompeLaSegunda, voice = VozRompePracticable });
            marcas.Add(new MusicScore.Cue { time = fila.rompeLaSuya, voice = VozCaePracticable });
        }
        foreach (var golpe in MomentosDeTralla(datos.tiempos_s))
        {
            marcas.Add(new MusicScore.Cue { time = golpe.segundo, voice = VozGolpeDelCoro });
            for (int fila = 0; fila < FilasDeLaTralla.Length; fila++)
                if ((golpe.filas & (1 << fila)) != 0)
                    marcas.Add(new MusicScore.Cue { time = golpe.segundo, voice = VocesDeLaTralla[fila] });
        }
        AnadirEstribillo(marcas, datos);
        OrdenarPeligros(marcas, datos.primer_tiempo_de_compas);
        partitura.SetData(cancion, datos.tiempos_s, datos.primer_tiempo_de_compas, 4, marcas.ToArray());
        EditorUtility.SetDirty(partitura);

        // La canción suena en 2D y se carga entera: el reloj la lee muestra a muestra.
        var importador = AssetImporter.GetAtPath(RutaCancion) as AudioImporter;
        if (importador != null)
        {
            var ajustes = importador.defaultSampleSettings;
            bool cambia = importador.forceToMono || !ajustes.preloadAudioData || ajustes.loadType != AudioClipLoadType.DecompressOnLoad;
            if (cambia)
            {
                // Precargada y descomprimida: al darle a tocar suena en el acto y desde el segundo pedido.
                importador.forceToMono = false;
                ajustes.preloadAudioData = true;
                ajustes.loadType = AudioClipLoadType.DecompressOnLoad;
                importador.defaultSampleSettings = ajustes;
                importador.SaveAndReimport();
            }
        }
        return partitura;
    }

    private static void OrdenarPeligros(List<MusicScore.Cue> marcas, int primerAcento)
    {
        // Los saltos y acciones de magia marcan la frase. Se guarda un tiempo alrededor de cada
        // acción antes de elegir columnas, fuego o notas que pidan otra respuesta.
        var ocupados = new List<float>(Semicorcheas);
        ocupados.AddRange(SegundosDeLosas);
        ocupados.AddRange(Tomad);
        ocupados.Add(Fogonazo);
        ocupados.Add(SaltoFinal);
        ocupados.Add(DashFinal);
        ocupados.AddRange(AcentosDeLaLetra);
        foreach (var marca in marcas)
            if (marca.voice == VozComba || marca.voice == VozNotaGrande || marca.voice == VozDelBarrido
                || marca.voice == VozMusical || marca.voice == VozMusicalFin)
                ocupados.Add(marca.time);

        bool Ocupado(float segundo, float espacio)
            => ocupados.Exists(t => Mathf.Abs(t - segundo) < espacio);

        // Un golpe central cada dos compases, dejando espacio para el aviso y el cambio de lado.
        float ultimoCentral = float.NegativeInfinity;
        for (int i = primerAcento; i < _tiempos.Length; i += 4)
        {
            float t = _tiempos[i];
            if (t < DesdeElEstribillo + 1f || t >= RespiroDeWill.x - 1.4f) continue;
            if (t - ultimoCentral < 2.8f || Ocupado(t, 1f) || Mathf.Abs(t - SegundoDelPilarDelTriple) < 1f) continue;
            marcas.Add(new MusicScore.Cue { time = t, voice = VozGolpeCentral });
            ocupados.Add(t);
            ultimoCentral = t;
        }

        marcas.Sort((a, b) => a.time.CompareTo(b.time));
        foreach (string voz in new[] { VozLatigazoDelFinal, VozPilarDelEstribillo, VozLlama, VozPirotecnia, VozNotaDelEstribillo, VozGolpe })
        {
            for (int i = 0; i < marcas.Count; i++)
            {
                var marca = marcas[i];
                if (marca.voice != voz || marca.time < DesdeLosPilares) continue;
                float espacio = marca.time < DesdeElEstribillo ? 0.52f : 0.37f;
                if (marcas.Exists(m => m.voice == VozGolpeCentral && Mathf.Abs(m.time - marca.time) < 0.8f)) espacio = 0.8f;
                if (EnUnRespiro(marca.time) || Ocupado(marca.time, espacio)
                    || (marca.time >= Truenos && marca.time <= FinDeLosPracticables))
                    marcas.RemoveAt(i--);
                else ocupados.Add(marca.time);
            }
        }
        Claves.Sort();
        for (int i = 0; i < Claves.Count; i++)
        {
            if (Ocupado(Claves[i], 0.8f) || (Claves[i] >= Truenos && Claves[i] <= FinDeLosPracticables))
                Claves.RemoveAt(i--);
            else ocupados.Add(Claves[i]);
        }
        int delTelar = 0;
        foreach (float t in DelTelar)
        {
            if (!SeEsquivaLoQueCaeDelTelar(t) || Ocupado(t, 1.15f)) continue;
            marcas.Add(new MusicScore.Cue { time = t, voice = VocesDelTelar[delTelar++ % VocesDelTelar.Length] });
            ocupados.Add(t);
        }
    }

    /// <summary>
    /// El estribillo, todo mezclado: el platillo de cada compás (el público salta, sin daño); pirotecnia
    /// del suelo en los golpes de órgano; notas que caen en los agudos (menos donde hay trampilla);
    /// claves de sol que se lanzan en los golpes de metales (se esquivan con la voltereta); el pilar del
    /// triple de 1:13; y, si se graban con la K, pilares a por el jugador. Si hay ritmo grabado con la L,
    /// las notas caen en él en lugar de en los agudos de la partitura (la del triple se conserva).
    /// </summary>
    private static void AnadirEstribillo(List<MusicScore.Cue> marcas, PartituraJsonDto datos)
    {
        for (int i = 0; i < datos.tiempos_s.Length; i++)
        {
            float t = datos.tiempos_s[i];
            bool enElEstribillo = t >= DesdeElEstribillo && t < HastaElEstribillo;
            bool enElFinal = t >= Truenos && t < SegundoDeLaMeta;
            if (!enElEstribillo && !enElFinal) continue;
            if (((i - datos.primer_tiempo_de_compas) % 4 + 4) % 4 == 0) marcas.Add(new MusicScore.Cue { time = t, voice = VozPlatillo, beat = 1f });
        }
        Claves.Clear();
        foreach (var (desde, hasta) in new[] { (DesdeElEstribillo, HastaElEstribillo), (Truenos, SegundoDeLaMeta) })
        {
            bool hayLanzas = HayLanzasEntre(desde, hasta);
            foreach (var e in datos.eventos)
            {
                if (e.segundo < desde || e.segundo >= hasta || EnUnRespiro(e.segundo)) continue;
                if (e.voz == "PROYECTIL") Claves.Add(e.segundo);
                else if (e.voz == "COLUMNA" && EnUnHuecoDeMagia(Z(e.segundo))) continue;
                else if (e.voz == "COLUMNA")
                    marcas.Add(new MusicScore.Cue { time = e.segundo, voice = Cerca(e.segundo, SegundoDelPilarDelTriple) ? VozPilarDelEstribillo : VozPirotecnia });
                else if (e.voz == "LOSA" && !Cerca(e.segundo, Volvereis) && (!hayLanzas || Cerca(e.segundo, SegundoDeLaNotaDelTriple)))
                    marcas.Add(new MusicScore.Cue { time = e.segundo, voice = VozNotaDelEstribillo });
            }
            foreach (float t in Lanzas)
                if (t >= desde && t < hasta && !EnUnRespiro(t) && !Cerca(t, SegundoDeLaNotaDelTriple))
                    marcas.Add(new MusicScore.Cue { time = t, voice = VozNotaDelEstribillo });
        }
        foreach (float t in AcentosDeLaLetra) marcas.Add(new MusicScore.Cue { time = t, voice = VozAcentoDeLaLetra });
        AnadirPuenteYFinal(marcas, datos);
    }

    /// <summary>
    /// El puente y el final: el portazo; los coristas de uno en uno (el siguiente sale al romperse el
    /// anterior; cada uno canta sus «¡Ah!» en el 2 y el 4 mientras está en pie); una nota a por el jugador en cada primer tiempo de compás menos en el del medio (el
    /// corte); y en el final, los latigazos
    /// en el 1 y el 3 tras los practicables, las rosas de «¡Bravo!», la lágrima y un corista más en
    /// cada compás.
    /// </summary>
    private static void AnadirPuenteYFinal(List<MusicScore.Cue> marcas, PartituraJsonDto datos)
    {
        marcas.Add(new MusicScore.Cue { time = SegundoDelPuente, voice = VozPuertas });
        // Primeros tiempos de compás del puente (la base y la salida del primer corista).
        var compases = new List<float>();
        for (int i = 0; i < datos.tiempos_s.Length; i++)
        {
            float t = datos.tiempos_s[i];
            if (t < SegundoDelPuente + 0.2f) continue;
            if (t >= SegundoDeLaFraseTriste) break;
            if (((i - datos.primer_tiempo_de_compas) % 4 + 4) % 4 == 0) compases.Add(t);
        }
        // El primero sale en el primer compás; cada siguiente, al romperse el anterior. Todos tienen sus
        // «¡Ah!» en la partitura; cantan solo mientras están fuera y en pie.
        SalidaDelPrimerCorista = compases.Count > 0 ? compases[0] : SegundoDelPuente + 0.5f;
        foreach (var e in datos.eventos)
        {
            if (e.voz != "RAYO" || e.segundo >= SegundoDeLaFraseTriste || e.segundo < SalidaDelPrimerCorista + 0.4f) continue;
            for (int c = 0; c < Coristas.Length; c++)
                marcas.Add(new MusicScore.Cue { time = e.segundo, voice = VozRayo(c) });
        }
        // La base: una nota en cada primer tiempo, menos en el del medio (el corte).
        for (int i = 0; i < compases.Count; i++)
            if (i != compases.Count / 2) marcas.Add(new MusicScore.Cue { time = compases[i], voice = VozNotaDelPuente });
        float desdeLosLatigazos = FinDeLosPracticables + 1f;
        for (int i = 0; i < datos.tiempos_s.Length; i++)
        {
            float t = datos.tiempos_s[i];
            if (t < Truenos) continue;
            if (t >= SegundoDeLaMeta) break;
            int tiempo = ((i - datos.primer_tiempo_de_compas) % 4 + 4) % 4;
            if (tiempo == 0) marcas.Add(new MusicScore.Cue { time = t, voice = VozCoristaQueAparece });
            if (t >= desdeLosLatigazos && t < Lagrimita - 0.2f && tiempo % 2 == 0) marcas.Add(new MusicScore.Cue { time = t, voice = VozLatigazoDelFinal });
        }
        for (int i = 0; i < 12; i++) marcas.Add(new MusicScore.Cue { time = Bravo + i * 0.11f, voice = VozRosas });
        marcas.Add(new MusicScore.Cue { time = Lagrimita, voice = VozLagrima });
    }

    // Los coristas del puente, en el orden en que salen (uno cada dos compases): escudos (de fuera a
    // dentro), carril y tamaño. Pide un personaje distinto cada vez: Will (luz), Estela (fuego), Liam
    // (mente); el último es grande, de fuego (Estela). Admite varios escudos (uno dentro de otro).
    private static readonly (string[] escudos, float x, float escala)[] Coristas =
    {
        (new[] { "luz" }, -2f, 0.5f), (new[] { "fuego" }, 2f, 0.5f), (new[] { "mente" }, 0f, 0.5f),
    };

    private static MagicElement ElementoDe(string color) => color switch
    {
        "luz" => MagicElement.Light,
        "fuego" => MagicElement.Fire,
        "viento" => MagicElement.Storm,
        "mente" => MagicElement.Mind,
        _ => MagicElement.Dark,
    };
    private static float SalidaDelPrimerCorista;
    private const string VozNotaDelPuente = "NOTA_DEL_PUENTE";
    private static string VozRayo(int corista) => "RAYO_" + Coristas[corista].escudos[0].ToUpperInvariant();
    // El final, más ancho que el pasillo: lo alto del templo.
    private const float AnchoDelFinal = 16f;

    // Los practicables sobre el humo: filas de tres tarimas grandes sobre el vacío, una fila cada dos
    // tiempos. Dos tiempos antes de llegar a una fila, una nota rompe las dos que no son del camino (pa-pa) y
    // queda la del camino; tiempo y medio después de pasarla, se hunde también: o se salta a tiempo o se cae.
    private static readonly float[] ColumnasDeLosPracticables = { -5f, 0f, 5f };
    private static readonly int[] PatronDeLosPracticables = { 1, 0, 1, 2, 1, 0, 1 };
    private const float AnchoDeUnPracticable = 3.8f, LargoDeUnPracticable = 3.4f;
    private const string VozRompePracticable = "ROMPE_PRACTICABLE";
    private const string VozCaePracticable = "CAE_PRACTICABLE";
    private struct FilaDePracticables { public float t, z, x, rompeLasOtras, rompeLaSegunda, rompeLaSuya; }
    private static readonly List<FilaDePracticables> Practicables = new List<FilaDePracticables>();
    private static float FinDeLosPracticables;

    /// <summary>Las filas de practicables del final: desde que caben tras el telón hasta «¡Bravo!».</summary>
    private static void PrepararPracticables()
    {
        Practicables.Clear();
        float zMinimo = ZDelFondoDelEscenario + 0.6f + 1.5f + LargoDeUnPracticable * 0.5f;
        int k = 0;
        for (int i = 2; i + 2 < _tiempos.Length; i++)
        {
            float t = _tiempos[i];
            if (t < Truenos + SalidaDelFinal || Z(t) < zMinimo) continue;
            if (t > Bravo - 0.3f) break;
            if (Practicables.Count > 0 && i % 2 != 0) continue;
            Practicables.Add(new FilaDePracticables
            {
                t = t, z = Z(t), x = ColumnasDeLosPracticables[PatronDeLosPracticables[k % PatronDeLosPracticables.Length]],
                rompeLasOtras = _tiempos[i - 2],
                rompeLaSegunda = Mathf.Lerp(_tiempos[i - 2], _tiempos[i - 1], 0.25f),
                rompeLaSuya = Mathf.Lerp(_tiempos[i + 1], _tiempos[i + 2], 0.25f),
            });
            k++;
            i++;
        }
        FinDeLosPracticables = Practicables.Count > 0 ? Practicables[Practicables.Count - 1].t + 0.9f : Truenos + SalidaDelFinal;
    }

    /// <summary>Segundo en el que el recuadro está tantos metros dentro del final.</summary>
    private static float SegundoEnElFinal(float metros) => Truenos + SalidaDelFinal + metros / Velocidad;

    /// <summary>La Z cae dentro de uno de los huecos de magia del escenario (ahí no sale fuego del suelo).</summary>
    private static bool EnUnHuecoDeMagia(float z)
    {
        foreach (var respiro in new[] { RespiroDeWill, RespiroDeLiam })
        {
            float borde = ZDelHuecoDeMagia(respiro);
            if (z > borde - 1f && z < borde + LargoDelHuecoDeMagia + 1f) return true;
        }
        return false;
    }

    private static bool HayLanzasEntre(float desde, float hasta)
    {
        foreach (float t in Lanzas) if (t >= desde && t < hasta) return true;
        return false;
    }

    // Tramos del nivel (segundos de la canción) para repartir los golpes grabados.
    private const float HastaLasGrandes = 37.5f;
    private const float DesdeLasLosas = 37.5f, HastaLasLosas = 41.3f;
    private const float DesdeLosPilares = 47.5f;

    // Lo tocado a mano llega un poco tarde (lo que tarda la mano en reaccionar al oírlo): se adelanta ese
    // retraso y se lleva al medio tiempo más cercano. Las marcas en bruto se adelantan lo medido en las
    // grabaciones; las guardadas ya en el medio tiempo, el retraso de siempre.
    private const float RetrasoDeLaMano = 0.15f;
    private const float RetrasoDeLaManoEnBruto = 0.09f;

    private static float Corregido(float[] tiempos, GrabadorDeMarcas.Marca m) =>
        AlMedioTiempo(tiempos, m.segundo - (m.enBruto ? RetrasoDeLaManoEnBruto : RetrasoDeLaMano));

    /// <summary>
    /// El telar deja libres los respiros, el encierro y el remate de saltos.
    /// </summary>
    private static bool SeEsquivaLoQueCaeDelTelar(float t)
    {
        if (t < SegundoDelCorredor || t >= SegundoDelPuente - 1.3f) return false;
        foreach (var r in Respiros)
            if (t >= r.x - 1.3f && t <= r.y + 0.3f) return false;
        return true;
    }
    private const float DesdeLasLlamas = 48.3f;
    private const string VozLlama = "LLAMA_DEL_TIEMPO";
    private const string VozMusical = "MUSICAL";
    private const string VozMusicalFin = "MUSICAL_FIN";
    private static float OsPresento, Pulmones, Fogonazo, QueVienen;
    private static readonly List<float> Musical = new List<float>();
    // Los regalitos: cuándo se enseñan y cuándo se lanzan (el «-ad» de «¡tomad!»).
    private static readonly List<float> Ensenar = new List<float>();
    private static readonly List<float> Tomad = new List<float>();
    private static readonly float[] RegalitoCalculado = { 58.41f, 60.02f };

    private static void Lista(List<GrabadorDeMarcas.Marca> grabadas, float[] tiempos, string voz, List<float> en, float[] calculado)
    {
        en.Clear();
        foreach (var m in grabadas)
            if (m.voz == voz) en.Add(Corregido(tiempos, m));
        if (en.Count == 0 && calculado != null) en.AddRange(calculado);
        en.Sort();
        // Dos pulsaciones que caen en el mismo medio tiempo cuentan como una.
        for (int i = en.Count - 1; i > 0; i--)
            if (en[i] - en[i - 1] < 0.05f) en.RemoveAt(i);
    }

    private static float Momento(List<GrabadorDeMarcas.Marca> grabadas, float[] tiempos, string voz, float calculado, bool ultimo = false)
    {
        bool hay = false;
        var elegida = default(GrabadorDeMarcas.Marca);
        foreach (var m in grabadas)
        {
            if (m.voz != voz) continue;
            if (!hay || (ultimo ? m.segundo > elegida.segundo : m.segundo < elegida.segundo)) elegida = m;
            hay = true;
        }
        return hay ? Corregido(tiempos, elegida) : calculado;
    }

    private static bool Cerca(float t, float otro) => Mathf.Abs(t - otro) < 0.3f;

    private static bool Cerca(float t, List<float> otros)
    {
        foreach (float o in otros) if (Cerca(t, o)) return true;
        return false;
    }
    private const float DesdeLosLatigazos = 24f;
    // El borde antes del hueco se hunde en este golpe del respiro: obliga a saltar, encender la
    // antorcha en el aire y hacer el doble salto hasta el puente.
    private const float SegundoDelBordeQueSeHunde = 43.34f;
    private const float LargoDelBorde = 9f;
    private const float DesdeLasGrandesGrabadas = 31.5f;
    private const string VozNotaGrande = "NOTA_GRANDE";
    private const string VozBomboPum = "BOMBARDINO_PUM";
    private const string VozBomboPam = "BOMBARDINO_PAM";

    /// <summary>El tiempo o medio tiempo de la canción más cercano a ese segundo.</summary>
    private static float AlMedioTiempo(float[] tiempos, float t)
    {
        int i = System.Array.BinarySearch(tiempos, t);
        if (i < 0) i = ~i - 1;
        i = Mathf.Clamp(i, 0, tiempos.Length - 2);
        float a = tiempos[i], b = tiempos[i + 1], m = (a + b) * 0.5f;
        float mejor = a;
        if (Mathf.Abs(m - t) < Mathf.Abs(mejor - t)) mejor = m;
        if (Mathf.Abs(b - t) < Mathf.Abs(mejor - t)) mejor = b;
        return mejor;
    }

    private static List<float> DeLaPartitura(MusicScore partitura, string voz, float desde, float hasta)
    {
        var lista = new List<float>();
        for (int i = 0; i < partitura.Cues.Count; i++)
        {
            var c = partitura.Cues[i];
            if (c.voice == voz && c.time >= desde && c.time < hasta) lista.Add(c.time);
        }
        return lista;
    }

    /// <summary>Una voz de la presentación: lo grabado a mano si lo hay; si no, lo calculado.</summary>
    private static void AnadirIntro(List<MusicScore.Cue> marcas, List<GrabadorDeMarcas.Marca> grabadas, string voz, float[] calculado)
    {
        bool hayGrabadas = false;
        foreach (var m in grabadas)
            if (m.voz == voz) { marcas.Add(new MusicScore.Cue { time = m.segundo, voice = voz }); hayGrabadas = true; }
        if (hayGrabadas) return;
        foreach (float t in calculado) marcas.Add(new MusicScore.Cue { time = t, voice = voz });
    }

    private struct Pieza { public Sprite sprite; public Vector2 desdeLaCara; }

    /// <summary>Pasa los PNG de los recortes a sprites con su punto de giro (de piezas.json).</summary>
    private static Dictionary<string, Pieza> ImportarRecortes()
    {
        var piezas = new Dictionary<string, Pieza>();
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>(CarpetaRecortes + "/piezas.json");
        string texto = json != null ? json.text : "";
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CarpetaRecortes }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            string nombre = Path.GetFileNameWithoutExtension(ruta);
            Vector2 pivote = LeerPar(texto, nombre, "pivote_unity", new Vector2(0.5f, 0.5f));
            // Lo que cae se apoya en el suelo por su borde de abajo, no se hunde en él.
            if (nombre == "nota_que_cae") pivote.y = 0.02f;
            Vector2 desde = LeerPar(texto, nombre, "desde_la_cara", Vector2.zero);

            var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = PixelesPorMetro;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            var ajustes = new TextureImporterSettings();
            imp.ReadTextureSettings(ajustes);
            ajustes.spriteAlignment = (int)SpriteAlignment.Custom;
            ajustes.spritePivot = pivote;
            imp.SetTextureSettings(ajustes);
            imp.SaveAndReimport();

            piezas[nombre] = new Pieza { sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ruta), desdeLaCara = desde };
        }
        return piezas;
    }

    private static Vector2 LeerPar(string json, string pieza, string campo, Vector2 siNo)
    {
        // Se busca dentro de «piezas»: antes, en «movimientos», salen las mismas claves con su descripción.
        int desde = Mathf.Max(0, json.IndexOf("\"piezas\"", System.StringComparison.Ordinal));
        int i = json.IndexOf("\"" + pieza + "\":", desde, System.StringComparison.Ordinal);
        if (i < 0) return siNo;
        int fin = json.IndexOf('}', i);
        string bloque = fin > i ? json.Substring(i, fin - i) : json.Substring(i);
        var m = Regex.Match(bloque, "\"" + campo + "\":\\s*\\[\\s*(-?[0-9.]+),\\s*(-?[0-9.]+)\\s*\\]");
        if (!m.Success) return siNo;
        var c = System.Globalization.CultureInfo.InvariantCulture;
        return new Vector2(float.Parse(m.Groups[1].Value, c), float.Parse(m.Groups[2].Value, c));
    }

    // ── Lo que se monta ─────────────────────────────────────────────────────────────────────

    private static TempoClock CrearReloj(Transform raiz, MusicScore partitura)
    {
        var go = new GameObject("Reloj (la canción del guardián)");
        go.transform.SetParent(raiz, false);
        var fuente = go.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;
        var reloj = go.AddComponent<TempoClock>();
        Poner(reloj, "startOnEnable", false);
        Poner(reloj, "scheduleCueAudio", true);
        PonerRef(reloj, "score", partitura);
        PonerRef(reloj, "source", fuente);
        return reloj;
    }

    /// <summary>Sala redonda (en cubos, cuadrada) de la afinación, con la boca del Tenor por puerta.</summary>
    private static void CrearSala(Transform raiz, Scene escena)
    {
        var sala = Hijo(raiz, "0. La afinación");
        Caja("Suelo de la sala", sala, new Vector3(0f, -0.5f, -16f), new Vector3(24f, 1f, 24f), _gris, true);
        Caja("Garganta (suelo)", sala, new Vector3(0f, -0.5f, -2f), new Vector3(4f, 1f, 4f), _gris, true);
        Caja("Muro oeste", sala, new Vector3(-12.2f, AltoDeLosMuros * 0.5f, -16f), new Vector3(0.4f, AltoDeLosMuros, 24f), _grisOscuro, false);
        Caja("Muro este", sala, new Vector3(12.2f, AltoDeLosMuros * 0.5f, -16f), new Vector3(0.4f, AltoDeLosMuros, 24f), _grisOscuro, false);
        Caja("Muro sur", sala, new Vector3(0f, AltoDeLosMuros * 0.5f, -28.2f), new Vector3(24.8f, AltoDeLosMuros, 0.4f), _grisOscuro, false);
        // Muro norte: la cara del Tenor. Su boca es la puerta (4 × 4 m).
        Caja("Muro norte oeste", sala, new Vector3(-7f, 7f, -4f), new Vector3(10f, 14f, 0.4f), _grisOscuro, false);
        Caja("Muro norte este", sala, new Vector3(7f, 7f, -4f), new Vector3(10f, 14f, 0.4f), _grisOscuro, false);
        Caja("Muro norte (encima de la boca)", sala, new Vector3(0f, 9f, -4f), new Vector3(4f, 10f, 0.4f), _grisOscuro, false);
        Caja("Garganta oeste", sala, new Vector3(-2.2f, 2f, -2f), new Vector3(0.4f, 4f, 4f), _grisOscuro, false);
        Caja("Garganta este", sala, new Vector3(2.2f, 2f, -2f), new Vector3(0.4f, 4f, 4f), _grisOscuro, false);
        // El suelo de la sala también es un pentagrama (bota con cada ¡PUM! de la presentación).
        var pentagrama = Hijo(sala, "Pentagrama de la sala");
        _pentagramaDeLaSala = pentagrama;
        for (int i = 0; i < 5; i++)
            Raya($"Línea {i + 1}", pentagrama, new Vector3(-8f + i * 4f, 0.01f, -16f), new Vector3(0.18f, 0.02f, 22f));
    }

    /// <summary>
    /// El principio de la función: los coristas esperan en la sala, junto al Tenor; cuando grita «¡que
    /// vienen, que vienen!» salen corriendo por su boca a colocarse, uno detrás de otro.
    /// </summary>
    private static void CrearCoristasDeLaSala(Transform raiz, TempoClock reloj, Dictionary<string, Pieza> piezas, MusicScore partitura)
    {
        var padre = Hijo(raiz, "0c. Los coristas de la sala (¡que vienen!)");
        string[] colores = { "luz", "fuego", "viento", "mente", "oscuro" };
        float[] xs = { -4.5f, -2.2f, 0f, 2.2f, 4.5f };
        int[] orden = { 2, 1, 3, 0, 4 };
        // La escala del órgano: en cada una de sus primeras notas sale un corista (de derecha a izquierda,
        // bajando, como la escala); en las demás, de vuelta, va botando cada uno.
        var notas = DeLaPartitura(partitura, GrabadorDeMarcas.IntroEscala, 0f, SegundoDelCorredor);
        var oyente = Hijo(padre, "Escala del órgano").gameObject.AddComponent<ScoreCueTrigger>();
        PonerRef(oyente, "clock", reloj);
        PonerTextos(oyente, "voices", new[] { GrabadorDeMarcas.IntroEscala });
        Poner(oyente, "toSeconds", SegundoDelCorredor);
        int n = colores.Length;
        var botes = new PulseMotion[n];
        for (int i = 0; i < n; i++)
        {
            float x = xs[i];
            int deLaEscala = n - 1 - i;
            // Cada movimiento en su pieza: la raíz corre (¡que vienen!), «Sube» sale de detrás de la tarima,
            // «Bote» bota en las notas y la figura se balancea con el compás.
            var corista = Hijo(padre, $"Corista de la sala ({colores[i]})");
            corista.position = new Vector3(x, 3.24f * 0.45f, -7.5f);
            var sube = Hijo(corista, "Sube en su nota");
            if (deLaEscala < notas.Count)
            {
                var aparece = sube.gameObject.AddComponent<CueSinkingTile>();
                PonerRef(aparece, "clock", reloj);
                Poner(aparece, "cueSeconds", 0.02f);
                Poner(aparece, "warningSeconds", 0f);
                Poner(aparece, "shake", 0f);
                Poner(aparece, "downSeconds", Mathf.Max(0.1f, notas[deLaEscala] - 0.14f));
                Poner(aparece, "sinkDepth", 3.5f);
                Poner(aparece, "moveSeconds", 0.12f);
            }
            var bote = Hijo(sube, "Bota en la escala").gameObject.AddComponent<PulseMotion>();
            PonerRef(bote, "clock", reloj);
            PonerVector(bote, "positionKick", new Vector3(0f, 0.6f, 0f));
            PonerVector(bote, "scaleKick", new Vector3(-0.08f, 0.15f, 0f));
            Poner(bote, "decaySeconds", 0.35f);
            botes[i] = bote;
            var figura = Figura("Corista", bote.transform, piezas, colores[i], 0.45f, out _);
            var balanceo = figura.gameObject.AddComponent<PulseMotion>();
            PonerRef(balanceo, "clock", reloj);
            Poner(balanceo, "onEveryBeat", true);
            Poner(balanceo, "alternate", true);
            PonerVector(balanceo, "rotationKick", new Vector3(0f, 0f, 5f));
            Poner(balanceo, "decaySeconds", 0.3f);
            var entra = corista.gameObject.AddComponent<CoristaQueEntra>();
            PonerRef(entra, "reloj", reloj);
            Poner(entra, "segundoDeSalir", QueVienen + System.Array.IndexOf(orden, i) * 0.15f);
            PonerVector(entra, "por", new Vector3(x * 0.25f, corista.position.y, -5f));
            PonerVector(entra, "destino", new Vector3(x * 0.15f, corista.position.y, 1.5f));
        }
        // Las notas de la escala botan a los coristas de derecha a izquierda y luego de vuelta.
        var pasos = new Object[Mathf.Max(notas.Count, n)];
        for (int k = 0; k < pasos.Length; k++)
        {
            int p = k % (2 * (n - 1));
            pasos[k] = botes[p < n ? n - 1 - p : p - (n - 1)];
        }
        var secuencia = oyente.gameObject.AddComponent<CueSequence>();
        PonerRef(secuencia, "trigger", oyente);
        PonerRefs(secuencia, "steps", pasos);
        Poner(secuencia, "loop", false);
    }

    /// <summary>
    /// El bombardino de la estrofa 1: en cada «pum» (1 y 3) sale un latigazo del suelo que barre medio
    /// corredor, dos seguidos por un lado y dos por el otro: hay que ir cambiando de lado corriendo al
    /// compás. Además, las líneas del pentagrama botan con el «pum» y las rayas de compás con el «pam».
    /// </summary>
    private static void CrearBombardino(Transform raiz, TempoClock reloj, MusicScore partitura)
    {
        var bombardino = Hijo(raiz, "1b. El bombardino (pum-pam)");
        var sitios = new List<Vector3>();
        // Sin latigazos al entrar ni durante «¡Visitas, visitas!» (hasta los 24 s): un respiro.
        var pums = DeLaPartitura(partitura, VozBomboPum, DesdeLosLatigazos, DesdeLasGrandesGrabadas);
        for (int i = 0; i < pums.Count; i++)
        {
            float x = XDeLatigazo(pums[i], i);
            sitios.Add(new Vector3(x, 0f, Z(pums[i])));
            AlMapa(pums[i], x, Z(pums[i]), 2.75f, "latigazo");
        }
        var latigos = Columnas("Latigazos del bombardino", bombardino, reloj, VozBomboPum, Puntos(bombardino, "Latigazo", sitios.ToArray()),
                 new Vector3(5.5f, 0.8f, 1.4f), 8f, 2.6f, null, 0f, 0.6f, 0.35f, DesdeLosLatigazos, DesdeLasGrandesGrabadas, atrezo: "atrezo_latigo");
        ConfigurarFranja(latigos, new Vector2(5.5f, 2.4f), 0.75f);
        AjustarAlAvance(latigos, 3.5f);
    }

    /// <summary>
    /// La presentación del Tenor en la sala, a sus golpes: en cada ¡PUM! salta el pentagrama del suelo;
    /// la escala del órgano la cantan los coristas de la sala, saliendo uno por nota; cada «pam» del
    /// bombardino saca una onda de la boca del Tenor por el suelo; y el do-re-mi corta de plano (lo
    /// lleva <see cref="CamaraDelCoro"/>). Devuelve el oyente del do-re-mi.
    /// </summary>
    private static ScoreCueTrigger CrearIntro(Transform raiz, TempoClock reloj)
    {
        var intro = Hijo(raiz, "0b. La presentación del Tenor");

        ScoreCueTrigger Oyente(string nombre, string voz)
        {
            var go = Hijo(intro, nombre);
            var o = go.gameObject.AddComponent<ScoreCueTrigger>();
            PonerRef(o, "clock", reloj);
            PonerTextos(o, "voices", new[] { voz });
            Poner(o, "toSeconds", SegundoDelCorredor);
            return o;
        }

        // ¡PUM!: el pentagrama de la sala da un bote.
        var pum = Oyente("¡PUM!", GrabadorDeMarcas.IntroPum);
        if (_pentagramaDeLaSala != null)
        {
            var bote = _pentagramaDeLaSala.gameObject.AddComponent<PulseMotion>();
            PonerRef(bote, "clock", reloj);
            PonerVector(bote, "positionKick", new Vector3(0f, 0.35f, 0f));
            Poner(bote, "decaySeconds", 0.45f);
            UnityEventTools.AddVoidPersistentListener(pum.onCue, bote.Kick);
        }

        // La escala la hacen los coristas de la sala (ver CrearCoristasDeLaSala).

        // Los «pam» del bombardino: una onda que sale de la boca del Tenor por el suelo.
        var pam = Oyente("Pam del bombardino", GrabadorDeMarcas.IntroPam);
        var onda = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        onda.name = "Onda del pam";
        onda.transform.SetParent(intro, false);
        onda.transform.position = new Vector3(0f, 0.04f, -5f);
        onda.transform.localScale = new Vector3(1f, 0.01f, 1f);
        Object.DestroyImmediate(onda.GetComponent<Collider>());
        onda.GetComponent<MeshRenderer>().sharedMaterial = _onda;
        var latido = onda.AddComponent<PulseMotion>();
        PonerRef(latido, "clock", reloj);
        PonerVector(latido, "scaleKick", new Vector3(16f, 0f, 16f));
        Poner(latido, "decaySeconds", 0.35f);
        UnityEventTools.AddVoidPersistentListener(pam.onCue, latido.Kick);

        return Oyente("Do, re, mi (cortes de cámara)", GrabadorDeMarcas.IntroDoReMi);
    }

    /// <summary>
    /// El Tenor en recortes, encima de su boca (la puerta): la cara cabecea en cada tiempo, las cejas
    /// suben antes de cada peligro, la mandíbula baja en cada golpe y las manos dirigen el compás.
    /// </summary>
    private static Transform CrearTenor(Transform padre, TempoClock reloj, Dictionary<string, Pieza> piezas, string titulo, Vector3 sitio, float escala)
    {
        if (!piezas.TryGetValue("tenor_cara", out var cara) || cara.sprite == null) return null;
        var tenor = Hijo(padre, titulo);
        tenor.position = sitio;
        tenor.localScale = Vector3.one * escala;

        var caraGo = Recorte("Cara", tenor, cara, 0);
        var motionCara = caraGo.AddComponent<PulseMotion>();
        PonerRef(motionCara, "clock", reloj);
        Poner(motionCara, "onEveryBeat", true);
        Poner(motionCara, "alternate", true);
        PonerVector(motionCara, "rotationKick", new Vector3(0f, 0f, 3f));
        PonerVector(motionCara, "positionKick", new Vector3(0f, -0.08f, 0f));
        Poner(motionCara, "decaySeconds", 0.35f);

        Transform pivoteDeLaBoca = null;
        var pivotesDeLasManos = new List<Transform>();
        var golpes = new GameObject("Golpes del Tenor (partitura)");
        golpes.transform.SetParent(tenor, false);
        var oyente = golpes.AddComponent<ScoreCueTrigger>();
        PonerRef(oyente, "clock", reloj);
        PonerTextos(oyente, "voices", new[] { VozDeLaAfinacion, "LOSA", "COLUMNA", "PROYECTIL", "RAYO", "OLEADA", "FIN" });
        Poner(oyente, "warningSeconds", 0.25f);

        if (piezas.TryGetValue("tenor_mandibula", out var mandibula))
        {
            var go = Recorte("Mandíbula", caraGo.transform, mandibula, 1);
            pivoteDeLaBoca = Pivote("Mandíbula (canta)", go.transform);
            var m = go.AddComponent<PulseMotion>();
            PonerRef(m, "clock", reloj);
            PonerVector(m, "positionKick", new Vector3(0f, -0.65f, 0f));
            PonerVector(m, "rotationKick", new Vector3(0f, 0f, -4f));
            Poner(m, "decaySeconds", 0.3f);
            UnityEventTools.AddVoidPersistentListener(oyente.onCue, m.Kick);
        }
        foreach (var nombre in new[] { "tenor_ceja_izq", "tenor_ceja_der" })
        {
            if (!piezas.TryGetValue(nombre, out var ceja)) continue;
            var go = Recorte(nombre == "tenor_ceja_izq" ? "Ceja izquierda" : "Ceja derecha", caraGo.transform, ceja, 2);
            var m = go.AddComponent<PulseMotion>();
            PonerRef(m, "clock", reloj);
            PonerVector(m, "positionKick", new Vector3(0f, 0.32f, 0f));
            Poner(m, "decaySeconds", 0.45f);
            UnityEventTools.AddVoidPersistentListener(oyente.onWarning, m.Kick);
        }
        foreach (var nombre in new[] { "tenor_mano_izq", "tenor_mano_der" })
        {
            if (!piezas.TryGetValue(nombre, out var mano)) continue;
            var go = Recorte(nombre == "tenor_mano_izq" ? "Mano izquierda" : "Mano derecha", tenor, mano, 2);
            go.transform.localPosition *= 1.6f; // un poco más abiertas que en el dibujo: dirigen
            pivotesDeLasManos.Add(Pivote(nombre == "tenor_mano_izq" ? "Mano izquierda (canta)" : "Mano derecha (canta)", go.transform));
            var m = go.AddComponent<PulseMotion>();
            PonerRef(m, "clock", reloj);
            Poner(m, "onDownbeat", true);
            Poner(m, "alternate", true);
            PonerVector(m, "positionKick", new Vector3(0f, 0.7f, 0f));
            PonerVector(m, "rotationKick", new Vector3(0f, 0f, nombre == "tenor_mano_izq" ? -10f : 10f));
            Poner(m, "decaySeconds", 0.6f);
            if (nombre == "tenor_mano_der" && piezas.TryGetValue("tenor_micro", out var micro) && micro.sprite != null)
            {
                // El micro: en la mano derecha; al calentar la voz le da golpecitos y se lo acerca a la boca.
                var microGo = Recorte("Micro", go.transform, micro, 3);
                microGo.transform.localPosition = new Vector3(micro.desdeLaCara.x - mano.desdeLaCara.x, micro.desdeLaCara.y - mano.desdeLaCara.y, 0f) * UnidadDelModelo;
                var calienta = microGo.AddComponent<PulseMotion>();
                PonerRef(calienta, "clock", reloj);
                PonerVector(calienta, "positionKick", new Vector3(-0.35f, 0.25f, 0f));
                PonerVector(calienta, "rotationKick", new Vector3(0f, 0f, 22f));
                Poner(calienta, "decaySeconds", 0.3f);
                var oyeMicro = Hijo(tenor, "Calienta con el micro").gameObject.AddComponent<ScoreCueTrigger>();
                PonerRef(oyeMicro, "clock", reloj);
                PonerTextos(oyeMicro, "voices", new[] { GrabadorDeMarcas.IntroPum, GrabadorDeMarcas.IntroPam, GrabadorDeMarcas.IntroDoReMi, GrabadorDeMarcas.IntroEscala });
                Poner(oyeMicro, "toSeconds", SegundoDelCorredor);
                UnityEventTools.AddVoidPersistentListener(oyeMicro.onCue, calienta.Kick);
            }
        }
        // Canta de verdad: la boca con las sílabas y las manos a la boca en las frases fuertes.
        var canta = tenor.gameObject.AddComponent<TenorQueCanta>();
        PonerRef(canta, "reloj", reloj);
        PonerRef(canta, "mandibula", pivoteDeLaBoca);
        PonerRefs(canta, "manos", pivotesDeLasManos.ToArray());
        return tenor;
    }

    /// <summary>
    /// Mete el recorte dentro de un pivote en su mismo sitio: el pivote lo mueve <see cref="TenorQueCanta"/>
    /// y el recorte se queda para los golpes de <see cref="PulseMotion"/>, que se suman encima.
    /// </summary>
    private static Transform Pivote(string nombre, Transform recorte)
    {
        var pivote = new GameObject(nombre).transform;
        pivote.SetParent(recorte.parent, false);
        pivote.localPosition = recorte.localPosition;
        pivote.localRotation = recorte.localRotation;
        recorte.SetParent(pivote, false);
        recorte.localPosition = Vector3.zero;
        recorte.localRotation = Quaternion.identity;
        return pivote;
    }

    private static GameObject Recorte(string nombre, Transform padre, Pieza pieza, int orden)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = new Vector3(pieza.desdeLaCara.x, pieza.desdeLaCara.y, 0f) * UnidadDelModelo;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = pieza.sprite;
        sr.sortingOrder = orden;
        return go;
    }

    /// <summary>El corredor-pentagrama: del 0 al hueco (120 m), con el tramo de las losas cortado.</summary>
    private static List<Transform> CrearCorredor(Transform raiz, TempoClock reloj)
    {
        var corredor = Hijo(raiz, "1. El corredor");
        var filas = new List<Transform>();
        var cortes = new List<float> { 0f };
        foreach (float t in SegundosDeLosas) { cortes.Add(Z(t) - 1.5f); cortes.Add(Z(t) + 1.5f); }
        cortes.Add(InicioDelBorde);
        for (int i = 0; i + 1 < cortes.Count; i += 2)
        {
            CrearFilasDeSuelo(corredor, filas, cortes[i], cortes[i + 1]);
        }
        float largo = ZDelFinDeLaNave;
        CrearFilasDeSuelo(corredor, filas, ZDelRellano, ZDelFinDeLaNave);
        Caja("Muro oeste", corredor, new Vector3(-AnchoDelCorredor * 0.5f - 0.2f, AltoDeLosMuros * 0.5f, largo * 0.5f),
             new Vector3(0.4f, AltoDeLosMuros, largo), _grisOscuro, false);
        Caja("Muro este", corredor, new Vector3(AnchoDelCorredor * 0.5f + 0.2f, AltoDeLosMuros * 0.5f, largo * 0.5f),
             new Vector3(0.4f, AltoDeLosMuros, largo), _grisOscuro, false);
        var pum = OyenteDelPentagrama(VozBomboPum);
        var pam = OyenteDelPentagrama(VozBomboPam);
        for (int i = 0; i < filas.Count; i++)
        {
            var fila = filas[i];
            float hasta = fila.position.z;
            float largoFila = fila.GetChild(0).localScale.z;
            float desde = hasta - largoFila;
            var lineas = Hijo(fila, "Pentagrama");
            var compases = Hijo(fila, "Compases");
            for (int v = 0; v < 5; v++)
                Raya("Línea", lineas, new Vector3(-4f + v * 2f, 0.012f, (desde + hasta) * 0.5f),
                     new Vector3(0.10f, 0.015f, largoFila));
            const float separacion = Velocidad * 4f * 60f / 145.6f;
            for (float z = Mathf.Ceil(desde / separacion) * separacion; z < hasta; z += separacion)
                Raya("Compás", compases, new Vector3(0f, 0.017f, z), new Vector3(AnchoDelCorredor, 0.02f, 0.10f));
            if (hasta > ZDelHueco) continue;
            Botar(lineas, pum, 0.08f);
            if (compases.childCount > 0) Botar(compases, pam, 0.06f);
        }

        ScoreCueTrigger OyenteDelPentagrama(string voz)
        {
            var oyente = Hijo(corredor, voz).gameObject.AddComponent<ScoreCueTrigger>();
            PonerRef(oyente, "clock", reloj);
            PonerTextos(oyente, "voices", new[] { voz });
            return oyente;
        }

        void Botar(Transform que, ScoreCueTrigger oyente, float alto)
        {
            var bote = que.gameObject.AddComponent<PulseMotion>();
            PonerRef(bote, "clock", reloj);
            PonerVector(bote, "positionKick", new Vector3(0f, alto, 0f));
            Poner(bote, "decaySeconds", 0.25f);
            UnityEventTools.AddVoidPersistentListener(oyente.onCue, bote.Kick);
        }
        return filas;
    }

    private static void CrearFilasDeSuelo(Transform corredor, List<Transform> filas,
                                        float desde, float hasta, Material material = null, TempoClock reloj = null)
    {
        if (hasta - desde < 0.01f) return;
        int cantidad = Mathf.CeilToInt((hasta - desde) / 4f);
        for (int i = 0; i < cantidad; i++)
        {
            float a = Mathf.Lerp(desde, hasta, i / (float)cantidad);
            float b = Mathf.Lerp(desde, hasta, (i + 1f) / cantidad);
            var fila = Hijo(corredor, $"Fila de suelo {filas.Count + 1}");
            fila.position = new Vector3(0f, 0f, b);
            Caja("Suelo", fila, new Vector3(0f, -0.5f, (a + b) * 0.5f), new Vector3(AnchoDelCorredor, 1f, b - a), material != null ? material : _gris, true);
            if (material == _madera)
            {
                for (int v = 1; v < 8; v++)
                    Raya("Veta", fila, new Vector3(-AnchoDelCorredor * 0.5f + v * AnchoDelCorredor / 8f, 0.012f, (a + b) * 0.5f),
                         new Vector3(0.05f, 0.02f, b - a));
                float primero = Mathf.Max(a, Z(DesdeElPracticable)), ultimo = Mathf.Min(b, Z(HastaElPracticable));
                if (ultimo > primero)
                {
                    var ribete = Hijo(fila, "Ribete lateral del practicable");
                    Raya("Línea", ribete, new Vector3(BordeDelPracticable + 0.1f, 0.025f, (primero + ultimo) * 0.5f),
                         new Vector3(0.14f, 0.015f, ultimo - primero));
                    var pulso = ribete.gameObject.AddComponent<PulseMotion>();
                    PonerRef(pulso, "clock", reloj);
                    Poner(pulso, "onEveryBeat", true);
                    PonerVector(pulso, "positionKick", new Vector3(0f, 0.045f, 0f));
                    Poner(pulso, "decaySeconds", 0.25f);
                }
            }
            filas.Add(fila);
        }
    }

    private static float ZDelHueco => Velocidad * (41.558f - SegundoDelCorredor);
    // El borde que se hunde empieza tras la última losa de «¡saltad!» (si no, se montan y parpadean).
    private static float InicioDelBorde
    {
        get
        {
            float z = ZDelHueco - LargoDelBorde;
            foreach (float t in SegundosDeLosas) z = Mathf.Max(z, Z(t) + 1.5f);
            return z;
        }
    }
    private static float ZDelRellano => Z(47.4f);
    private static float ZDeLaPuerta => Calderones[1].x + 2f;
    private static float ZDeLaMeta => Z(SegundoDeLaMeta);
    private static float ZDelBordeDelVacioFinal => Z(SaltoFinal) - 1f;
    // Hasta 8 m más allá de la meta y del vacío del final.
    private static float ZDelFinal => Mathf.Max(ZDeLaMeta, ZDelBordeDelVacioFinal + LargoDelVacioFinal) + 8f;
    private static float ZDelFinDeLaNave => ZDeLaPuerta + 0.4f;
    // Donde se para el recuadro en el puente (el encierro) y el telón del fondo, 6 m por delante.
    private static float ZDeLaSala => Z(SegundoDelPuente);
    private static float ZDelFondoDelEscenario => ZDeLaSala + 6f;
    /// <summary>Segundo en que el centro del recuadro deja atrás un hueco de magia (al acabar su respiro, lo que tarda en cruzarlo).</summary>
    private static float SegundoAlPasarElHueco(Vector2 respiro) => respiro.x + respiro.y + (2f + LargoDelHuecoDeMagia) / Velocidad + 0.1f;

    /// <summary>Borde de un hueco de magia: 2 m por delante de donde se para el recuadro en su respiro.</summary>
    private static float ZDelHuecoDeMagia(Vector2 respiro) => Z(respiro.x) + 2f;
    private static float ZDelOrgano => ZDelRellano + 26f;
    // El órgano salta hacia el grupo desde que llega a la nave hasta antes de las llamaradas fuertes.
    private const float SegundoDeLosSaltosDelOrgano = 47.4f;
    private const float HastaLosSaltosDelOrgano = 50.3f;

    /// <summary>
    /// Lo que cae: las presentaciones de la afinación (lejos, sin daño), el contratiempo a los lados
    /// del corredor y los tres PUM grandes en el carril central. Una nota por marca, en su sitio.
    /// </summary>
    private static void CrearLoQueCae(Transform raiz, Scene escena, TempoClock reloj, MusicScore partitura,
                                      Dictionary<string, Pieza> piezas)
    {
        var padre = Hijo(raiz, "Lo que cae (notas de piedra)");
        piezas.TryGetValue("nota_que_cae", out var nota);

        var pum = AssetDatabase.LoadAssetAtPath<AudioClip>(RutaPum);

        // En la estrofa 1 solo caen las notas grandes de antes de «¡saltad!»: las dos de la partitura y las
        // grabadas con la K un poco antes (no hay notas a los lados).
        var grandesT = DeLaPartitura(partitura, VozNotaGrande, DesdeLasGrandesGrabadas, HastaLasGrandes);
        var centro = new Vector3[grandesT.Count];
        for (int i = 0; i < grandesT.Count; i++)
        {
            float t = grandesT[i];
            float x = i % 2 == 0 ? -1.7f : 1.7f;
            centro[i] = new Vector3(x, 0f, Z(t));
            AlMapa(t, x, Z(t), 1.9f, "nota grande");
        }
        var caida = Caida("Pasad... ¡PUM!", padre, reloj, nota, VozNotaGrande, DesdeLasGrandesGrabadas, HastaLasGrandes,
            Puntos(padre, "Nota", centro), 1.9f, 12f, 1.3f, pum, 1f, 0.85f);
        HacerNotaSolida(caida);
        AjustarAlAvance(caida, 2f);
    }

    private static CueDrop Caida(string nombre, Transform padre, TempoClock reloj, Pieza nota, string voz, float desde, float hasta,
                              Transform[] puntos, float escala, float dano, float radio,
                              AudioClip sonido, float volumen, float segundosDeCaida)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);

        var plantilla = new GameObject("Nota (plantilla)");
        plantilla.transform.SetParent(go.transform, false);
        plantilla.transform.localScale = Vector3.one * escala;
        if (nota.sprite != null)
        {
            var sr = plantilla.AddComponent<SpriteRenderer>();
            sr.sprite = nota.sprite;
        }
        plantilla.AddComponent<CartelQueMiraALaCamara>();

        var sombra = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        sombra.name = "Sombra (plantilla)";
        sombra.transform.SetParent(go.transform, false);
        sombra.transform.localScale = new Vector3(2.2f * escala, 0.01f, 2.2f * escala);
        Object.DestroyImmediate(sombra.GetComponent<Collider>());
        sombra.GetComponent<MeshRenderer>().sharedMaterial = dano > 0f ? _aviso : _sombra;

        var caida = go.AddComponent<CueDrop>();
        PonerRef(caida, "clock", reloj);
        PonerTextos(caida, "voices", new[] { voz });
        Poner(caida, "fromSeconds", desde);
        Poner(caida, "toSeconds", hasta);
        PonerRefs(caida, "landingPoints", puntos);
        PonerRef(caida, "fallingTemplate", plantilla);
        PonerRef(caida, "shadowTemplate", sombra.transform);
        Poner(caida, "fallSeconds", segundosDeCaida);
        Poner(caida, "dropHeight", 14f * Mathf.Max(1f, segundosDeCaida / 0.8f));
        PonerRef(caida, "landingSound", sonido);
        Poner(caida, "soundVolume", volumen);
        Poner(caida, "radius", radio);
        Poner(caida, "damage", dano);
        return caida;
    }

    private static Transform[] Puntos(Transform padre, string nombre, Vector3[] sitios)
    {
        var res = new Transform[sitios.Length];
        for (int i = 0; i < sitios.Length; i++)
        {
            var go = new GameObject($"{nombre} {i + 1}");
            go.transform.SetParent(padre, false);
            go.transform.position = sitios[i];
            res[i] = go.transform;
        }
        return res;
    }

    /// <summary>«¡Salta, salta!»: tres losas que se hunden en sus PUM. Se salta en el golpe.</summary>
    private static void CrearLosasQueSeHunden(Transform raiz, Scene escena, TempoClock reloj, MusicScore partitura)
    {
        var padre = Hijo(raiz, "Losas que se hunden");
        var segundos = SegundosDeLosas;
        for (int i = 0; i < segundos.Count; i++)
        {
            var losa = Caja($"Losa {i + 1}", padre, new Vector3(0f, -0.5f, Z(segundos[i])), new Vector3(AnchoDelCorredor, 1f, 3f), _grisOscuro, true);
            var hunde = losa.AddComponent<CueSinkingTile>();
            PonerRef(hunde, "clock", reloj);
            Poner(hunde, "cueSeconds", segundos[i]);
            Poner(hunde, "warningSeconds", 0.4f);
            Poner(hunde, "downSeconds", 1.4f);
            Poner(hunde, "sinkDepth", 6f);
        }
    }

    private sealed class Hueco
    {
        public PlataformaTemporal[] Plataformas;
        public ReceptorDeMagia Antorcha;
    }

    /// <summary>
    /// El último PUM abre un hueco largo. Fuego a la antorcha que cuelga encima (Estela; también en
    /// el aire) y aparecen tres plataformas unos segundos.
    /// </summary>
    private static Hueco CrearHuecoConAntorcha(Transform raiz, Scene escena, TempoClock reloj)
    {
        var padre = Hijo(raiz, "La antorcha en el aire");
        float a = ZDelHueco, b = ZDelRellano;
        // El borde de antes del hueco: aguanta el respiro y se hunde en su golpe; hay que saltar, encender
        // la antorcha en el aire y llegar al puente con el doble salto. Vuelve a subir al reintentar.
        var borde = Caja("Borde que se hunde", padre, new Vector3(0f, -0.5f, (InicioDelBorde + a) * 0.5f),
                         new Vector3(AnchoDelCorredor, 1f, a - InicioDelBorde), _grisOscuro, true);
        var hunde = borde.AddComponent<CueSinkingTile>();
        PonerRef(hunde, "clock", reloj);
        Poner(hunde, "cueSeconds", SegundoDelBordeQueSeHunde);
        Poner(hunde, "warningSeconds", 0.8f);
        Poner(hunde, "downSeconds", 8f);
        Poner(hunde, "sinkDepth", 10f);
        // Un puente de lado a lado del hueco: se cruza deprisa mientras el Tenor presenta su órgano.
        var plataformas = new PlataformaTemporal[1];
        {
            // Empieza 3 m después del borde: sin doble salto no se llega.
            float desdePuente = a + 3f;
            var p = Caja("Pasarela", padre, new Vector3(0f, -0.25f, (desdePuente + b) * 0.5f), new Vector3(3.5f, 0.5f, b - desdePuente + 0.4f), _madera, true);
            var temporal = p.AddComponent<PlataformaTemporal>();
            Poner(temporal, "visibleAlEmpezar", false);
            PonerRefs(temporal, "colliders", new Object[] { p.GetComponent<Collider>() });
            PonerRefs(temporal, "renderers", new Object[] { p.GetComponent<Renderer>() });
            Poner(temporal, "segundosDeAviso", 1f);
            plataformas[0] = temporal;
        }

        // Un corista de fuego colgado del telar: con el fuego de Estela, sale la pasarela.
        var receptor = CoristaColgado(padre, "Corista colgado (fuego: Estela)", new Vector3(0f, 2.6f, a + 3f), "fuego", reloj);
        foreach (var p in plataformas)
            UnityEventTools.AddFloatPersistentListener(receptor.alActivarse, p.MostrarDurante, 7f);

        return new Hueco { Plataformas = plataformas, Antorcha = receptor };
    }


    /// <summary>
    /// Estrofa 2, la nave del órgano («os presento a mi órgano»): se ve al fondo desde el puente, con
    /// el coro en balcones a los lados. Sus tubos laten con cada nota del órgano. Por el suelo salen
    /// columnas que van a por el jugador: las grandes en los golpes de la voz, las pequeñas en el 2 y
    /// el 4. Al final, una puerta de madera que se quema (con su calderón).
    /// </summary>
    private static void CrearNaveDelOrgano(Transform raiz, TempoClock reloj, MusicScore partitura, Dictionary<string, Pieza> piezas)
    {
        var nave = Hijo(raiz, "2. La nave del órgano");

        // El órgano, con cara («¡os presento a mi órgano!»): sale al fondo de la nave, se hincha en «¡qué
        // pulmones!» y, al llegar el grupo a la nave, va saltando hacia él en cada tiempo para aplastarlo.
        var organo = Hijo(nave, "El órgano (con cara)");
        organo.position = new Vector3(0f, 0f, ZDelOrgano);
        // Tres capas: la raíz salta (OrganoQueSalta), el cuerpo se hincha en «¡qué pulmones!» y el
        // recorte late en cada tiempo.
        var cuerpoDelOrgano = Hijo(organo, "Cuerpo");
        Transform recorteDelOrgano = null;
        if (piezas.TryGetValue("organo", out var recorte) && recorte.sprite != null)
        {
            recorteDelOrgano = RecorteSuelto("Recorte", cuerpoDelOrgano, recorte, organo.position, 0.5f, true);
            var late = recorteDelOrgano.gameObject.AddComponent<PulseMotion>();
            PonerRef(late, "clock", reloj);
            Poner(late, "onEveryBeat", true);
            PonerVector(late, "scaleKick", new Vector3(0.04f, -0.05f, 0f));
            Poner(late, "decaySeconds", 0.25f);
        }
        var sombraDelOrgano = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        sombraDelOrgano.name = "Sombra del órgano";
        sombraDelOrgano.transform.SetParent(nave, false);
        sombraDelOrgano.transform.localScale = new Vector3(4.4f, 0.01f, 4.4f);
        Object.DestroyImmediate(sombraDelOrgano.GetComponent<Collider>());
        sombraDelOrgano.GetComponent<MeshRenderer>().sharedMaterial = _sombra;
        sombraDelOrgano.SetActive(false);
        var caidas = new List<float>();
        for (int i = 0; i < partitura.BeatCount; i++)
        {
            float t = partitura.BeatTime(i);
            if (t > SegundoDeLosSaltosDelOrgano && t <= HastaLosSaltosDelOrgano) caidas.Add(t);
        }
        var salta = organo.gameObject.AddComponent<OrganoQueSalta>();
        PonerRef(salta, "reloj", reloj);
        Poner(salta, "segundoDeAparecer", OsPresento);
        PonerFloats(salta, "caidas", caidas.ToArray());
        // Cae a tu lado en cada tiempo, fuera del camino: casi te aplasta, pero no si vas por él.
        var dondeCae = new Vector3[caidas.Count];
        int turnoDelOrgano = 0;
        for (int i = 0; i < caidas.Count; i++)
        {
            float x = XFueraDelCamino(caidas[i], RadioDelOrgano, ref turnoDelOrgano, 0.6f);
            dondeCae[i] = new Vector3(x, 0f, Z(caidas[i]));
            AlMapa(caidas[i], x, Z(caidas[i]), RadioDelOrgano, "órgano");
        }
        PonerVector3s(salta, "sitios", dondeCae);
        Poner(salta, "radio", RadioDelOrgano);
        PonerRef(salta, "sombra", sombraDelOrgano.transform);
        PonerRef(salta, "golpe", AssetDatabase.LoadAssetAtPath<AudioClip>(RutaPum));

        // «¡Qué pulmones tiene el bicho!»: el órgano se hincha y una bocanada recorre la nave.
        var pulmones = Hijo(nave, "Qué pulmones").gameObject.AddComponent<ScoreCueTrigger>();
        PonerRef(pulmones, "clock", reloj);
        PonerTextos(pulmones, "voices", new[] { GrabadorDeMarcas.Pulmones });
        if (recorteDelOrgano != null)
        {
            var hinchar = cuerpoDelOrgano.gameObject.AddComponent<PulseMotion>();
            PonerRef(hinchar, "clock", reloj);
            PonerVector(hinchar, "scaleKick", new Vector3(0.3f, 0.25f, 0f));
            Poner(hinchar, "decaySeconds", 1.2f);
            UnityEventTools.AddVoidPersistentListener(pulmones.onCue, hinchar.Kick);
        }


        // El coro, en lo alto de los muros a los dos lados, balanceándose al compás.
        string[] colores = { "luz", "fuego", "viento", "mente", "oscuro" };
        piezas.TryGetValue("corista_cabeza", out var cabeza);
        int n = 0;
        for (float z = ZDelRellano + 6f; z < ZDeLaPuerta - 4f; z += 9f)
        {
            foreach (float lado in new[] { -1f, 1f })
            {
                if (!piezas.TryGetValue("corista_cuerpo_" + colores[n % colores.Length], out var cuerpo) || cuerpo.sprite == null) continue;
                // En un balcón pegado al muro, a media altura: dentro del cuadro de la cámara de la huida.
                var balcon = Caja($"Balcón {n + 1}", nave, new Vector3(lado * (AnchoDelCorredor * 0.5f - 0.45f), 2.6f, z),
                                  new Vector3(0.9f, 0.3f, 2.4f), _grisOscuro, false);
                balcon.layer = LayerMask.NameToLayer("Ignore Raycast");
                var corista = new GameObject($"Corista {++n}");
                corista.transform.SetParent(nave, false);
                corista.transform.position = new Vector3(lado * (AnchoDelCorredor * 0.5f - 0.5f), 2.75f, z);
                corista.transform.localScale = Vector3.one * 0.55f;
                corista.AddComponent<CartelQueMiraALaCamara>();
                var sr = corista.AddComponent<SpriteRenderer>();
                sr.sprite = cuerpo.sprite;
                if (cabeza.sprite != null)
                {
                    var c = new GameObject("Cabeza");
                    c.transform.SetParent(corista.transform, false);
                    c.transform.localPosition = new Vector3(0f, cuerpo.sprite.bounds.max.y * 0.92f, -0.01f);
                    var src = c.AddComponent<SpriteRenderer>();
                    src.sprite = cabeza.sprite;
                    src.sortingOrder = 1;
                }
                var balanceo = corista.AddComponent<PulseMotion>();
                PonerRef(balanceo, "clock", reloj);
                Poner(balanceo, "onEveryBeat", true);
                Poner(balanceo, "alternate", true);
                PonerVector(balanceo, "rotationKick", new Vector3(0f, 0f, 6f));
                PonerVector(balanceo, "positionKick", new Vector3(0f, 0.12f, 0f));
                Poner(balanceo, "decaySeconds", 0.3f);
            }
        }

        var grave = AssetDatabase.LoadAssetAtPath<AudioClip>(RutaOrganoGrave);
        var fogonazo = AssetDatabase.LoadAssetAtPath<AudioClip>(RutaFogonazo);

        // El ritmo de la estrofa: una llamarada de los muros en cada tiempo, dos por un lado y dos por el
        // otro (como los latigazos). Hay que ir cambiando de lado al compás.
        var llamas = new List<Vector3>();
        var tiemposDeLlama = DeLaPartitura(partitura, VozLlama, DesdeLasLlamas - 0.1f, SegundoDeLaPuerta);
        for (int i = 0; i < tiemposDeLlama.Count; i++)
        {
            llamas.Add(new Vector3(XDeLlama(i), 0f, Z(tiemposDeLlama[i])));
            AlMapa(tiemposDeLlama[i], XDeLlama(i), Z(tiemposDeLlama[i]), 2.65f, "llamarada");
        }
        var llamaradas = Columnas("Llamaradas del ritmo", nave, reloj, VozLlama, Puntos(nave, "Llamarada", llamas.ToArray()),
                 new Vector3(5.3f, 1.4f, 1.2f), 8f, 2.5f, fogonazo, 0.22f, 0.55f, 0.3f, DesdeLasLlamas - 0.1f, SegundoDeLaPuerta, _brasa, deLaPared: true, atrezo: "atrezo_llama");
        AjustarAlAvance(llamaradas, 3f);

        // «…ya os lo he dicho»: el fogonazo, una llamarada grande de lanzallamas desde el muro izquierdo.
        Columnas("Fogonazo (ya os lo he dicho)", nave, reloj, GrabadorDeMarcas.Fogonazo,
                 Puntos(nave, "Fogonazo", new[] { new Vector3(XDelFogonazo, 0f, Z(Fogonazo)) }),
                 new Vector3(7f, 2.6f, 2.2f), 14f, 3f, fogonazo, 1f, 0.7f, 0.7f, Fogonazo - 1f, Fogonazo + 1f, _brasa, deLaPared: true, atrezo: "atrezo_llama");

        // «¡Suena el musical!»: un pilar en cada golpe, fuera del camino; el último («páan»), más grande.
        int turno = 0;
        var musical = new List<Vector3>();
        for (int i = 0; i < Musical.Count - 1; i++)
        {
            float x = XFueraDelCamino(Musical[i], 0.8f, ref turno, 1.1f);
            musical.Add(new Vector3(x, 0f, Z(Musical[i])));
            AlMapa(Musical[i], x, Z(Musical[i]), 0.8f, "pilar");
        }
        Columnas("Pilares del musical", nave, reloj, VozMusical, Puntos(nave, "Pilar del musical", musical.ToArray()),
                 new Vector3(1.6f, 3.8f, 1.6f), 10f, 1f, grave, 1f, 0.8f, 1.1f, DesdeLasLlamas, SegundoDeLaPuerta, atrezo: "atrezo_columna");
        float paanT = Musical[Musical.Count - 1], paanX = XFueraDelCamino(paanT, 1.3f, ref turno, 1.4f);
        AlMapa(paanT, paanX, Z(paanT), 1.3f, "pilar grande");
        Columnas("Pilar del páan", nave, reloj, VozMusicalFin,
                 Puntos(nave, "Pilar del páan", new[] { new Vector3(paanX, 0f, Z(paanT)) }),
                 new Vector3(2.6f, 5.5f, 2.6f), 14f, 1.6f, grave, 1f, 0.9f, 1.4f, DesdeLasLlamas, SegundoDeLaPuerta, atrezo: "atrezo_columna");

        // Los pilares grabados con la K: momentos concretos, fuera del camino.
        var pilares = DeLaPartitura(partitura, VozGolpe, DesdeLosPilares, SegundoDeLaPuerta);
        if (pilares.Count > 0)
        {
            var sitios = new List<Vector3>();
            foreach (float t in pilares)
            {
                float x = XFueraDelCamino(t, 0.8f, ref turno, 1.3f);
                sitios.Add(new Vector3(x, 0f, Z(t)));
                AlMapa(t, x, Z(t), 0.8f, "pilar");
            }
            Columnas("Pilares grabados", nave, reloj, VozGolpe, Puntos(nave, "Pilar", sitios.ToArray()),
                     new Vector3(1.6f, 3.8f, 1.6f), 10f, 1f, grave, 1f, 0.8f, 1.3f, DesdeLosPilares, atrezo: "atrezo_columna");
        }

    }

    /// <summary>
    /// Los regalitos, al final de la nave antes de la puerta de madera: en cada «un regalito…» el Tenor
    /// enseña un regalo en un balcón del coro, unos metros por delante (tiembla), y en el «-ad» de
    /// «¡tomad!» se lo tira al jugador, a donde va a estar: hay que apartarse o saltarlo. Las llamaradas
    /// del ritmo siguen mientras tanto.
    /// </summary>
    private static void CrearRegalitos(Transform raiz, TempoClock reloj, Dictionary<string, Pieza> piezas, Montaje montaje)
    {
        var padre = Hijo(raiz, "3. Los regalitos");
        piezas.TryGetValue("regalito", out var recorte);
        var bolaDelDemonio = BolaDevolvible();
        var guardian = CrearTenor(padre, reloj, piezas, "Guardián de los regalos", new Vector3(0f, 3.6f, ZDeLaPuerta), 0.9f);
        if (guardian == null) return;
        var cuerpo = guardian.gameObject.AddComponent<BoxCollider>();
        cuerpo.size = new Vector3(AnchoDelCorredor, 7.5f, 0.65f) / guardian.localScale.x;
        var receptor = guardian.gameObject.AddComponent<ReceptorDeMagia>();
        // La devolución conserva el elemento del proyectil, incluso si el regalo cambia de prefab.
        PonerEnumArray(receptor, "elementosAceptados", System.Array.Empty<int>());
        Poner(receptor, "soloMagiaDevuelta", true);
        Poner(receptor, "modo", (int)ReceptorDeMagia.Modo.Permanente);
        Poner(receptor, "sfxAlActivar", "");
        var canta = guardian.GetComponent<TenorQueCanta>();
        PonerRef(canta, "pasoQueBloquea", cuerpo);
        UnityEventTools.AddVoidPersistentListener(receptor.alActivarse, canta.EmpujarConElRegalo);
        montaje.TenorDeLosRegalos = canta;
        montaje.Receptores.Add(receptor);
        for (int i = 0; i < Tomad.Count; i++)
        {
            if (i == 2)
            {
                // El tercero sale de delante del guardián y vuelve a él con la defensa.
                var encima = RecorteSuelto("Regalito 3 (devolver)", padre, recorte, new Vector3(0f, 3.8f, ZDeLaPuerta - 1f), 0.45f, true).gameObject;
                var lanza3 = Hijo(padre, "Lanza el regalito 3 (devolver)").gameObject.AddComponent<RegaloQueSeLanza>();
                montaje.Regalos.Add(lanza3);
                PonerRef(lanza3, "reloj", reloj);
                Poner(lanza3, "segundoDeEnsenar", Ensenar[i]);
                Poner(lanza3, "segundoDeLanzar", Tomad[i]);
                PonerRef(lanza3, "regalo", encima.transform);
                PonerRef(lanza3, "material", _regalo);
                PonerRef(lanza3, "prefabDevolvible", bolaDelDemonio);
                PonerRef(lanza3, "destino", guardian.gameObject);
                Poner(lanza3, "lanzarConSuForma", true);
                Poner(lanza3, "segundosDeVuelo", 1.1f);
                Poner(lanza3, "anticipacion", 0f);
                Poner(lanza3, "alturaDelTiro", 1.1f);
                Poner(lanza3, "tamano", 0.6f);
                Poner(lanza3, "dano", 16f);
                // Mientras se enseña, vuela y se devuelve, la cámara se aleja y se ve todo: el grupo, el
                // regalito encima de la puerta y la puerta.
                var desde = Hijo(padre, "Plano del regalito 3 (desde)");
                var hasta = Hijo(padre, "Plano del regalito 3 (hasta)");
                var mira = new Vector3(0f, 3f, ZDeLaPuerta - 3f);
                // Dentro del corredor (por fuera, los muros lo tapan todo), alto y por detrás.
                desde.position = new Vector3(2f, 7.5f, ZDeLaPuerta - 15f);
                desde.rotation = Quaternion.LookRotation(mira - desde.position, Vector3.up);
                hasta.position = new Vector3(2.5f, 8.3f, ZDeLaPuerta - 17f);
                hasta.rotation = Quaternion.LookRotation(mira - hasta.position, Vector3.up);
                montaje.Planos.Add((new Vector2(Ensenar[i] - 0.2f, Tomad[i] + 2f), desde, hasta, 58f));
                if (bolaDelDemonio == null) Debug.LogWarning("[Templo del Coro] No encuentro la bola del Demonio: el tercer regalito no se podrá devolver.");
                continue;
            }
            float lado = i % 2 == 0 ? -1f : 1f;
            float zBalcon = Mathf.Min(Z(Tomad[i]) + 11f, ZDeLaPuerta - 0.6f);
            var balcon = Caja($"Balcón del regalito {i + 1}", padre, new Vector3(lado * (AnchoDelCorredor * 0.5f - 0.6f), 3.2f, zBalcon),
                              new Vector3(1.2f, 0.3f, 2.4f), _grisOscuro, false);
            balcon.layer = LayerMask.NameToLayer("Ignore Raycast");
            // Sin colisionador: los regalos salen justo encima y chocaban con él nada más salir.
            Object.DestroyImmediate(balcon.GetComponent<Collider>());
            var regalo = RecorteSuelto($"Regalito {i + 1}", padre, recorte, new Vector3(lado * (AnchoDelCorredor * 0.5f - 1f), 3.45f, zBalcon), 0.4f, true).gameObject;
            var lanza = Hijo(padre, $"Lanza el regalito {i + 1}").gameObject.AddComponent<RegaloQueSeLanza>();
            montaje.Regalos.Add(lanza);
            PonerRef(lanza, "reloj", reloj);
            Poner(lanza, "segundoDeEnsenar", Ensenar[i]);
            Poner(lanza, "segundoDeLanzar", Tomad[i]);
            PonerRef(lanza, "regalo", regalo.transform);
            PonerRef(lanza, "material", _regalo);
            Poner(lanza, "lanzarConSuForma", true);
            Poner(lanza, "tamano", 1f);
            // Una ráfaga en vez de uno: se paran con el escudo mantenido y cada choque va a su golpe.
            var choques = ChoquesDeLaRafaga(i);
            PonerFloats(lanza, "choques", choques.ToArray());
            Poner(lanza, "segundosDeVuelo", SegundosDeVueloDeLaRafaga);
            Poner(lanza, "anticipacion", 0f);
            Poner(lanza, "alturaDelTiro", 1.1f);
            Poner(lanza, "dano", 10f);
            if (choques.Count > 0) Poner(lanza, "segundoDeLanzar", choques[0] - SegundosDeVueloDeLaRafaga);
        }
    }

    private const float SegundosDeVueloDeLaRafaga = 0.8f;

    /// <summary>
    /// Los choques de la ráfaga del regalito <paramref name="i"/> (el primero o el segundo): lo grabado con la
    /// G desde su «¡tomad!» hasta el siguiente regalito; sin grabar, uno en cada corchea de ese rato.
    /// </summary>
    private static List<float> ChoquesDeLaRafaga(int i)
    {
        float desde = Tomad[i] + SegundosDeVueloDeLaRafaga - 0.05f;
        float hasta = (i + 1 < Tomad.Count ? (i == 0 ? Tomad[1] : Ensenar[2]) - 0.15f : desde + 1.6f) + SegundosDeVueloDeLaRafaga;
        var tiempos = LeerTiempos();
        var choques = new List<float>();
        foreach (var m in GrabadorDeMarcas.Leer())
        {
            if (m.voz != GrabadorDeMarcas.Escudo) continue;
            float t = Corregido(tiempos, m);
            if (t >= Tomad[i] + 0.2f && t <= hasta) choques.Add(t);
        }
        if (choques.Count > 0)
        {
            choques.Sort();
            return choques;
        }
        for (int k = 0; k + 1 < tiempos.Length; k++)
            for (int q = 0; q < 2; q++)
            {
                float t = tiempos[k] + (tiempos[k + 1] - tiempos[k]) * q * 0.5f;
                if (t >= desde && t <= hasta) choques.Add(t);
            }
        return choques;
    }

    /// <summary>
    /// El estribillo: tras la puerta de madera se sale a un escenario de teatro. Tablas de madera, el
    /// telón a la izquierda, las candilejas y el foso a la derecha con el público de piedra en sus gradas
    /// (salta en el platillo de cada compás) y focos encima. Peligros, todo mezclado: pirotecnia del
    /// suelo, notas que caen a por el jugador, claves de sol que gira el Tenor desde las bambalinas y se
    /// lanzan (borde ciruela: no se devuelven, se esquivan con la voltereta), dos trampillas que se
    /// abren y el triple de 1:13 (clave, nota, pilar).
    /// </summary>
    private static void CrearEscenario(Transform raiz, TempoClock reloj, MusicScore partitura, Dictionary<string, Pieza> piezas, Montaje montaje, List<Transform> sueloQueSePierde)
    {
        var padre = Hijo(raiz, "4. El escenario (estribillo)");
        float a = ZDelFinDeLaNave, b = ZDelFondoDelEscenario, medio = AnchoDelCorredor * 0.5f;

        // Tablas, cortadas en los dos huecos de magia (el foso de la orquesta y las tablas que se caen).
        float huecoWill = ZDelHuecoDeMagia(RespiroDeWill), huecoLiam = ZDelHuecoDeMagia(RespiroDeLiam);
        var cortes = new List<float> { a, huecoWill, huecoWill + LargoDelHuecoDeMagia, huecoLiam, huecoLiam + LargoDelHuecoDeMagia, b };
        for (int i = 0; i + 1 < cortes.Count; i += 2)
            CrearFilasDeSuelo(padre, sueloQueSePierde, cortes[i], cortes[i + 1], _madera, reloj);
        // Will: el foso de la orquesta. Un foco apagado encima: con luz, sale una plataforma de luz.
        HuecoDeMagia("El foso de la orquesta (luz: Will)", padre, huecoWill, MagicElement.Light, _luz, _luz, 0.4f, reloj, montaje);
        // Liam: las tablas se han caído al foso. Una piedra con runas: con mente, sube una tabla.
        HuecoDeMagia("Las tablas caídas (mente: Liam)", padre, huecoLiam, MagicElement.Mind, _runas, _madera, 0.6f, reloj, montaje);

        // A la izquierda, el telón: tan alto como los muros, con sus pliegues.
        var telon = Hijo(padre, "Telón (izquierda)");
        Caja("Telón", telon, new Vector3(-medio - 0.3f, AltoDeLosMuros * 0.5f, (a + b) * 0.5f), new Vector3(0.6f, AltoDeLosMuros, b - a), _telon, false);
        for (float z = a + 1.5f; z < b; z += 3f)
        {
            var pliegue = Caja("Pliegue", telon, new Vector3(-medio + 0.1f, AltoDeLosMuros * 0.5f, z), new Vector3(0.4f, AltoDeLosMuros, 1.2f), _telon, false);
            Object.DestroyImmediate(pliegue.GetComponent<Collider>());
        }
        // Al fondo, el telón cerrado con la cara del Tenor: en «¡Rayos y truenos!» se hunde y se abre el final.
        var fondo = Hijo(padre, "Telón del fondo (se abre en «¡Rayos y truenos!»)");
        Caja("Telón del fondo", fondo, new Vector3(0f, AltoDeLosMuros * 0.5f, b + 0.3f), new Vector3(AnchoDelCorredor + 2f, AltoDeLosMuros, 0.6f), _telon, false);
        var seAbre = fondo.gameObject.AddComponent<CueSinkingTile>();
        PonerRef(seAbre, "clock", reloj);
        Poner(seAbre, "cueSeconds", Truenos);
        Poner(seAbre, "warningSeconds", 0f);
        Poner(seAbre, "shake", 0f);
        Poner(seAbre, "downSeconds", 1000f);
        Poner(seAbre, "sinkDepth", AltoDeLosMuros + 6f);
        Poner(seAbre, "moveSeconds", 0.5f);
        // «¡Corred, corred, que empieza la función!»: el telón de delante del Tenor se abre hacia los lados
        // y se le ve al fondo, cantando. Cada hoja va en un soporte girado: su «hundirse» es abrirse.
        foreach (float lado in new[] { -1f, 1f })
        {
            var soporte = Hijo(padre, lado < 0f ? "Telón que se abre (izquierda)" : "Telón que se abre (derecha)");
            soporte.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 0f, lado < 0f ? -90f : 90f));
            var hojaQueSeAbre = Hijo(soporte, "Hoja");
            Caja("Tela", hojaQueSeAbre, new Vector3(lado * 3.05f, AltoDeLosMuros * 0.5f, b - 0.45f), new Vector3(6.1f, AltoDeLosMuros, 0.2f), _telon, false);
            var abre = hojaQueSeAbre.gameObject.AddComponent<CueSinkingTile>();
            PonerRef(abre, "clock", reloj);
            Poner(abre, "cueSeconds", DesdeElEstribillo);
            Poner(abre, "warningSeconds", 0f);
            Poner(abre, "shake", 0f);
            Poner(abre, "downSeconds", 1000f);
            Poner(abre, "sinkDepth", 6.3f);
            Poner(abre, "moveSeconds", 1.4f);
        }
        var marcaDelTenor = Hijo(fondo, "Segunda posición del guardián");
        marcaDelTenor.position = new Vector3(0f, 3.6f, b - 0.2f);
        if (montaje.TenorDeLosRegalos != null) PonerRef(montaje.TenorDeLosRegalos, "siguienteMarca", marcaDelTenor);

        // La embocadura al salir por la puerta: los dos lados y la bambalina de arriba.
        Caja("Embocadura (izquierda)", padre, new Vector3(-medio - 0.6f, AltoDeLosMuros * 0.5f, a + 0.4f), new Vector3(1.4f, AltoDeLosMuros, 0.8f), _telon, false);
        Caja("Embocadura (derecha)", padre, new Vector3(medio + 0.6f, AltoDeLosMuros * 0.5f, a + 0.4f), new Vector3(1.4f, AltoDeLosMuros, 0.8f), _telon, false);
        var bambalina = Caja("Bambalina", padre, new Vector3(0f, AltoDeLosMuros - 1f, a + 0.4f), new Vector3(AnchoDelCorredor + 2.6f, 2f, 0.8f), _telon, false);
        bambalina.layer = LayerMask.NameToLayer("Ignore Raycast");

        // A la derecha, las candilejas y una barandilla que no se ve (no se cae uno al foso).
        var candilejas = Hijo(padre, "Candilejas");
        // El borde se corta en los huecos de magia: por él no se puede rodear el hueco.
        for (int i = 0; i + 1 < cortes.Count; i += 2)
            Caja("Borde del escenario", candilejas, new Vector3(medio + 0.15f, 0.15f, (cortes[i] + cortes[i + 1]) * 0.5f),
                 new Vector3(0.3f, 0.3f, cortes[i + 1] - cortes[i]), _grisOscuro, false);
        for (float z = a + 2f; z < b; z += 4f)
        {
            var luz = Caja("Candileja", candilejas, new Vector3(medio + 0.15f, 0.38f, z), new Vector3(0.25f, 0.16f, 0.6f), _brasa, false);
            Object.DestroyImmediate(luz.GetComponent<Collider>());
        }
        var barandilla = new GameObject("Barandilla (invisible)");
        barandilla.transform.SetParent(padre, false);
        barandilla.transform.position = new Vector3(medio + 0.4f, 1.5f, (a + b) * 0.5f);
        barandilla.layer = LayerMask.NameToLayer("Ignore Raycast");
        barandilla.AddComponent<BoxCollider>().size = new Vector3(0.4f, 3f, b - a);

        // El foso y el público de piedra en sus gradas: salta en el platillo de cada compás.
        Caja("Foso", padre, new Vector3(medio + 2.4f, -3.5f, (a + b) * 0.5f), new Vector3(4f, 1f, b - a), _grisOscuro, false);
        Publico(padre, reloj, 1f, a, b, null);

        // Focos encima del escenario, cálidos, alternando de lado.
        var focos = Hijo(padre, "Focos");
        int n = 0;
        for (float z = a + 8f; z < b; z += 20f, n++)
        {
            var foco = new GameObject($"Foco {n + 1}");
            foco.transform.SetParent(focos, false);
            float x = n % 2 == 0 ? -2.5f : 2.5f;
            foco.transform.SetPositionAndRotation(new Vector3(x, 13f, z), Quaternion.LookRotation(new Vector3(-x * 0.3f, -13f, 3f)));
            var luz = foco.AddComponent<Light>();
            luz.type = LightType.Spot;
            luz.color = new Color(1f, 0.88f, 0.7f);
            luz.range = 22f;
            luz.spotAngle = 55f;
            luz.intensity = 6f;
            luz.shadows = LightShadows.None;
        }

        var grave = AssetDatabase.LoadAssetAtPath<AudioClip>(RutaOrganoGrave);
        var pum = AssetDatabase.LoadAssetAtPath<AudioClip>(RutaPum);
        var fogonazo = AssetDatabase.LoadAssetAtPath<AudioClip>(RutaFogonazo);

        var acentosDeLaLetra = new Vector3[AcentosDeLaLetra.Length];
        for (int i = 0; i < acentosDeLaLetra.Length; i++)
        {
            float t = AcentosDeLaLetra[i];
            float x = LadosDeLaLetra[i];
            acentosDeLaLetra[i] = new Vector3(x, 0f, Z(t));
            AlMapa(t, x, Z(t), 1.45f, "acento de la letra");
        }
        var letra = Columnas("Corred, corred y suelto, nunca, más", padre, reloj, VozAcentoDeLaLetra,
            Puntos(padre, "Golpe de la letra", acentosDeLaLetra), new Vector3(2.9f, 1.7f, 2.4f),
            12f, 1f, pum, 0.55f, 0.65f, 0.35f, DesdeElEstribillo, HastaElEstribillo, _brasa, atrezo: "atrezo_llama");
        ConfigurarFranja(letra, new Vector2(2.9f, 2.4f), 1.8f);

        var golpesCentrales = DeLaPartitura(partitura, VozGolpeCentral, DesdeElEstribillo, HastaElEstribillo);
        var centrosDeGolpe = new Vector3[golpesCentrales.Count];
        for (int i = 0; i < golpesCentrales.Count; i++)
        {
            float t = golpesCentrales[i];
            centrosDeGolpe[i] = new Vector3(0f, 0f, Z(t));
            AlMapa(t, 0f, Z(t), AnchoGolpeCentral * 0.5f, "golpe central");
        }
        var golpeCentral = Columnas("Golpes centrales del estribillo", padre, reloj, VozGolpeCentral,
            Puntos(padre, "Centro", centrosDeGolpe), new Vector3(AnchoGolpeCentral, 2.35f, 2.7f),
            16f, 1f, fogonazo, 0.5f, 0.75f, 0.48f, DesdeElEstribillo, HastaElEstribillo, _brasa, atrezo: "atrezo_llama");
        ConfigurarFranja(golpeCentral, new Vector2(AnchoGolpeCentral, 2.7f), 2.3f);

        // Pirotecnia: chorros de fuego del suelo en medio carril, cambiando de lado cada vez.
        var piro = new List<Vector3>();
        var tiemposDePiro = DeLaPartitura(partitura, VozPirotecnia, DesdeElEstribillo, HastaElEstribillo);
        for (int i = 0; i < tiemposDePiro.Count; i++)
        {
            float x = XDePirotecnia(tiemposDePiro[i], i);
            piro.Add(new Vector3(x, 0f, Z(tiemposDePiro[i])));
            AlMapa(tiemposDePiro[i], x, Z(tiemposDePiro[i]), 2.4f, "pirotecnia");
        }
        Columnas("Pirotecnia", padre, reloj, VozPirotecnia, Puntos(padre, "Chorro", piro.ToArray()),
                 new Vector3(4.8f, 2.4f, 1.2f), 8f, 2.5f, fogonazo, 0.6f, 0.6f, 0.45f, DesdeElEstribillo, HastaElEstribillo, _brasa, atrezo: "atrezo_llama");

        // La franja del practicable queda al ras. El latigazo barre a su izquierda.
        {
            var barridos = BarridosDelPracticable();
            if (barridos.Count > 0)
            {
                var sitios = new List<Vector3>();
                foreach (float t in barridos)
                {
                    sitios.Add(new Vector3(XDelBarrido, 0f, Z(t)));
                    AlMapa(t, XDelBarrido, Z(t), MedioDelBarrido, "barrido");
                }
                Columnas("Barrido junto al practicable", padre, reloj, VozDelBarrido, Puntos(padre, "Barrido", sitios.ToArray()),
                         new Vector3(MedioDelBarrido * 2f, 0.6f, 1.4f), 8f, MedioDelBarrido, null, 0f, 0.5f, 0.35f,
                         DesdeElPracticable, HastaElPracticable, atrezo: "atrezo_latigo");
            }
        }

        // Las notas que caen (lo grabado con la L), fuera del camino, a un lado y al otro.
        piezas.TryGetValue("nota_que_cae", out var nota);
        int turno = 0;
        var notas = new List<Vector3>();
        foreach (float t in DeLaPartitura(partitura, VozNotaDelEstribillo, DesdeElEstribillo, HastaElEstribillo))
        {
            float x = XFueraDelCamino(t, 1.3f, ref turno);
            float z = Z(t);
            // En un foso, la nota cae sobre su borde de entrada. El compás se conserva.
            if (z >= huecoWill && z < huecoWill + LargoDelHuecoDeMagia) z = huecoWill - 0.5f;
            if (z >= huecoLiam && z < huecoLiam + LargoDelHuecoDeMagia) z = huecoLiam - 0.5f;
            notas.Add(new Vector3(x, 0f, z));
            AlMapa(t, x, z, 1.3f, "nota");
        }
        var notasDelEstribillo = Caida("Notas del estribillo", padre, reloj, nota, VozNotaDelEstribillo, DesdeElEstribillo, HastaElEstribillo,
              Puntos(padre, "Nota", notas.ToArray()), 1.6f, 12f, 1.3f, pum, 1f, 0.85f);
        HacerNotaSolida(notasDelEstribillo);

        // El pilar del triple de 1:13 y los que se graben con la K en el estribillo, fuera del camino.
        float xDelTriple = XFueraDelCamino(SegundoDelPilarDelTriple, 0.8f, ref turno, 1.1f);
        AlMapa(SegundoDelPilarDelTriple, xDelTriple, Z(SegundoDelPilarDelTriple), 0.8f, "pilar");
        Columnas("Pilar del triple (1:13)", padre, reloj, VozPilarDelEstribillo,
                 Puntos(padre, "Pilar del triple", new[] { new Vector3(xDelTriple, 0f, Z(SegundoDelPilarDelTriple)) }),
                 new Vector3(1.6f, 3.8f, 1.6f), 10f, 1f, grave, 1f, 0.8f, 1.1f, DesdeElEstribillo, HastaElEstribillo, atrezo: "atrezo_columna");
        var golpes = DeLaPartitura(partitura, VozGolpe, DesdeElEstribillo, HastaElEstribillo);
        if (golpes.Count > 0)
        {
            var sitios = new List<Vector3>();
            foreach (float t in golpes)
            {
                float x = XFueraDelCamino(t, 0.8f, ref turno, 1.1f);
                sitios.Add(new Vector3(x, 0f, Z(t)));
                AlMapa(t, x, Z(t), 0.8f, "pilar");
            }
            Columnas("Pilares grabados del estribillo", padre, reloj, VozGolpe, Puntos(padre, "Pilar", sitios.ToArray()),
                     new Vector3(1.6f, 3.8f, 1.6f), 10f, 1f, grave, 1f, 0.8f, 1.1f, DesdeElEstribillo, HastaElEstribillo, atrezo: "atrezo_columna");
        }

        // Las claves de sol: el Tenor las enseña girando entre bambalinas, unos metros por delante, y
        // en su golpe las lanza a un sitio fijo fuera del camino. Borde ciruela: no se devuelven.
        var claves = Hijo(padre, "Claves de sol (esquivar)");
        piezas.TryGetValue("clave_que_se_esquiva", out var recorteDeLaClave);
        for (int i = 0; i < Claves.Count; i++)
        {
            float lado = i % 2 == 0 ? -1f : 1f;
            float z = Mathf.Min(Z(Claves[i]) + 10f, (Claves[i] < Truenos ? b : ZDelFinal) - 1f);
            var clave = RecorteSuelto($"Clave de sol {i + 1}", claves, recorteDeLaClave, new Vector3(lado * (medio - 0.9f), 3.2f, z), 0.55f, false);
            var lanza = Hijo(claves, $"Lanza la clave {i + 1}").gameObject.AddComponent<RegaloQueSeLanza>();
            PonerRef(lanza, "reloj", reloj);
            const float vuelo = 0.7f;
            float llega = Claves[i];
            Poner(lanza, "segundoDeEnsenar", llega - vuelo - 0.55f);
            Poner(lanza, "segundoDeLanzar", llega - vuelo);
            PonerRef(lanza, "regalo", clave);
            Poner(lanza, "lanzarConSuForma", true);
            Poner(lanza, "giro", 540f);
            Poner(lanza, "segundosDeVuelo", vuelo);
            float xDeLaClave = XFueraDelCamino(llega, 0.7f, ref turno, 0.6f);
            var adonde = Hijo(claves, $"Adónde va la clave {i + 1}");
            adonde.position = new Vector3(xDeLaClave, 0f, Z(llega));
            PonerRef(lanza, "sitio", adonde);
            AlMapa(llega, xDeLaClave, Z(llega), 0.7f, "clave");
            Poner(lanza, "anticipacion", 0.6f);
            Poner(lanza, "alturaDelTiro", 1f);
            Poner(lanza, "tamano", 1.4f);
            Poner(lanza, "temblor", 0.04f);
        }
    }

    /// <summary>
    /// Un recorte que se enseña y se lanza (clave de sol, regalito), de pie en el sitio. Los que no giran
    /// miran siempre a la cámara; los que giran se quedan de cara al corredor (giran en su plano).
    /// </summary>
    private static Transform RecorteSuelto(string nombre, Transform padre, Pieza pieza, Vector3 sitio, float escala, bool miraALaCamara)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.position = sitio;
        go.transform.localScale = Vector3.one * escala;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = pieza.sprite;
        if (miraALaCamara) go.AddComponent<CartelQueMiraALaCamara>();
        return go.transform;
    }

    /// <summary>Lo montado que tiene que conocer el tramo (para reintentar) y la cámara (planos de escena).</summary>
    private sealed class Montaje
    {
        public readonly List<PlataformaTemporal> Plataformas = new List<PlataformaTemporal>();
        public readonly List<ReceptorDeMagia> Receptores = new List<ReceptorDeMagia>();
        public readonly List<GameObject> Vuelven = new List<GameObject>();
        public readonly List<GameObject> SeVan = new List<GameObject>();
        // La cara entera del Tenor del final: a ella mira la despedida de la cámara.
        public Transform TenorDelFinal;
        public TenorQueCanta TenorDeLosRegalos;
        public readonly List<RegaloQueSeLanza> Regalos = new List<RegaloQueSeLanza>();
        public Transform MarcaDelSaludo;
        public Light FocoDelSaludo;
        public Transform RecintoDelCoro;
        public Transform ControlDeLaCoda;
        public Transform Meta;
        public readonly List<(Vector2 segundos, Transform desde, Transform hasta, float campo)> Planos =
            new List<(Vector2, Transform, Transform, float)>();
    }

    /// <summary>
    /// Un hueco ancho en las tablas con algo colgado encima que pide un tipo de magia: con ella aparecen
    /// tres plataformas grandes (se cae bien en ellas viniendo deprisa) que suben y bajan al compás, cada
    /// una al revés que la de al lado. Se para el recuadro antes (su respiro) para cambiar a quien tiene
    /// esa magia y disparar.
    /// </summary>
    private static void HuecoDeMagia(string nombre, Transform padre, float borde, MagicElement elemento, Material queBrilla,
                                     Material plataforma, float altura, TempoClock reloj, Montaje montaje)
    {
        var go = Hijo(padre, nombre);
        var plataformas = new List<PlataformaTemporal>();
        // Una sola plataforma en medio del hueco, que va de lado a lado al compás (lleva a quien va encima).
        const float largo = 4.2f, ancho = 4f;
        {
            float z = borde + LargoDelHuecoDeMagia * 0.5f;
            var p = Caja("Plataforma", go, new Vector3(0f, altura - 0.25f, z), new Vector3(ancho, 0.5f, largo), plataforma, true);
            var vaiven = p.AddComponent<BeatSway>();
            PonerRef(vaiven, "clock", reloj);
            PonerVector(vaiven, "amplitude", new Vector3(2.5f, 0f, 0f));
            Poner(vaiven, "beatsPerSwing", 4);
            var temporal = p.AddComponent<PlataformaTemporal>();
            Poner(temporal, "visibleAlEmpezar", false);
            PonerRefs(temporal, "colliders", new Object[] { p.GetComponent<Collider>() });
            PonerRefs(temporal, "renderers", new Object[] { p.GetComponent<Renderer>() });
            Poner(temporal, "segundosDeAviso", 1f);
            plataformas.Add(temporal);
        }
        // Un corista de su color colgado del telar sobre el hueco: con su magia, sale la plataforma.
        var receptor = CoristaColgado(go, elemento == MagicElement.Light ? "Corista colgado (luz: Will)" : "Corista colgado (mente: Liam)",
                                      new Vector3(0f, 3.2f, borde + 3f), elemento == MagicElement.Light ? "luz" : "mente", reloj);
        foreach (var p in plataformas)
            UnityEventTools.AddFloatPersistentListener(receptor.alActivarse, p.MostrarDurante, 7f);
        montaje.Plataformas.AddRange(plataformas);
        montaje.Receptores.Add(receptor);
    }

    /// <summary>
    /// Gradas a un lado (1 derecha, −1 izquierda) con el público que se ha hecho el Tenor (lleva siglos
    /// solo): muñecos de cartón clavados en un palo. Botan en las voces dadas.
    /// </summary>
    private static void Publico(Transform padre, TempoClock reloj, float lado, float a, float b, string[] voces, float medioDelPasillo = 0f)
    {
        var aplauso = Hijo(padre, lado > 0f ? "Público (derecha)" : "Público (izquierda)");
        var oyente = aplauso.gameObject.AddComponent<ScoreCueTrigger>();
        PonerRef(oyente, "clock", reloj);
        PonerTextos(oyente, "voices", voces ?? new[] { VozPlatillo });
        float medio = medioDelPasillo > 0f ? medioDelPasillo : AnchoDelCorredor * 0.5f;
        for (int fila = 0; fila < 4; fila++)
        {
            float x = lado * (medio + 5.5f + fila * 3.4f), y = -3f + fila * 2f;
            var grada = Hijo(aplauso, $"Grada {fila + 1}");
            Caja("Escalón", grada, new Vector3(x, y - 0.5f, (a + b) * 0.5f), new Vector3(3.4f, 1f, b - a), _grisOscuro, false);
            var alCompas = Hijo(grada, "Bota al compás");
            var publico = Hijo(alCompas, "Público");
            publico.position = new Vector3(x, y, (a + b) * 0.5f);
            int k = fila;
            for (float z = a + 1f + fila * 1f; z < b; z += 3.2f, k++)
            {
                if (!_piezas.TryGetValue("publico_muneco_" + "abc"[k % 3], out var muneco) || muneco.sprite == null) continue;
                var go = new GameObject("Muñeco de cartón");
                go.transform.SetParent(publico, false);
                go.transform.position = new Vector3(x, y, z);
                // El frente del recorte es -Z: mira hacia el centro del escenario.
                Vector3 escenario = new Vector3(0f, y, Mathf.Min(b, z + 3f));
                go.transform.rotation = Quaternion.LookRotation(go.transform.position - escenario, Vector3.up);
                go.transform.localScale = Vector3.one * 0.8f;
                go.AddComponent<SpriteRenderer>().sprite = muneco.sprite;
            }
            var salto = publico.gameObject.AddComponent<PulseMotion>();
            PonerRef(salto, "clock", reloj);
            PonerVector(salto, "positionKick", new Vector3(0f, fila % 2 == 0 ? 0.5f : 0.35f, 0f));
            Poner(salto, "decaySeconds", 0.35f);
            UnityEventTools.AddVoidPersistentListener(oyente.onCue, salto.Kick);
            // Además, botan con la música en cada tiempo (el escalón se queda quieto).
            var bota = alCompas.gameObject.AddComponent<PulseMotion>();
            PonerRef(bota, "clock", reloj);
            Poner(bota, "onEveryBeat", true);
            PonerVector(bota, "positionKick", new Vector3(0f, fila % 2 == 0 ? 0.25f : 0.18f, 0f));
            PonerVector(bota, "scaleKick", new Vector3(0f, 0.04f, 0f));
            Poner(bota, "decaySeconds", fila % 2 == 0 ? 0.22f : 0.3f);
        }
    }

    /// <summary>
    /// Un corista colgado del telar con su escudo del color de una magia: la diana de los huecos de magia
    /// (así, en el encierro, ya se sabe qué hacer con los coristas). Se balancea en cada tiempo y, al darle
    /// con su magia, avisa (modo pulso: se le puede volver a dar al reintentar).
    /// </summary>
    private static ReceptorDeMagia CoristaColgado(Transform padre, string nombre, Vector3 sitio, string color, TempoClock reloj)
    {
        var raiz = Hijo(padre, nombre);
        raiz.position = sitio;
        var cuerda = Caja("Cuerda", raiz, sitio + Vector3.up * 5f, new Vector3(0.06f, 8f, 0.06f), _linea, false);
        Object.DestroyImmediate(cuerda.GetComponent<Collider>());
        var figura = Figura("Corista", raiz, _piezas, color, 0.4f, out _);
        figura.localPosition = Vector3.zero;
        var balanceo = figura.gameObject.AddComponent<PulseMotion>();
        PonerRef(balanceo, "clock", reloj);
        Poner(balanceo, "onEveryBeat", true);
        Poner(balanceo, "alternate", true);
        PonerVector(balanceo, "rotationKick", new Vector3(0f, 0f, 8f));
        Poner(balanceo, "decaySeconds", 0.35f);
        var escudo = new GameObject($"Escudo ({color})");
        escudo.transform.SetParent(figura, false);
        escudo.transform.localPosition = new Vector3(0f, -0.1f, -0.05f);
        if (_piezas.TryGetValue("corista_burbuja_" + color, out var burbuja) && burbuja.sprite != null)
        {
            var sr = escudo.AddComponent<SpriteRenderer>();
            sr.sprite = burbuja.sprite;
            sr.sortingOrder = 5;
        }
        escudo.AddComponent<BoxCollider>().size = new Vector3(5.2f, 6.4f, 1f);
        var receptor = escudo.AddComponent<ReceptorDeMagia>();
        PonerEnumArray(receptor, "elementosAceptados", new[] { (int)ElementoDe(color) });
        Poner(receptor, "modo", (int)ReceptorDeMagia.Modo.Pulso);
        PonerRefs(receptor, "brillo", new Object[0]);
        // Al darle con su magia, da un respingo.
        var respingo = raiz.gameObject.AddComponent<PulseMotion>();
        PonerRef(respingo, "clock", reloj);
        PonerVector(respingo, "scaleKick", new Vector3(0.25f, 0.25f, 0f));
        PonerVector(respingo, "positionKick", new Vector3(0f, 0.4f, 0f));
        Poner(respingo, "decaySeconds", 0.4f);
        UnityEventTools.AddVoidPersistentListener(receptor.alActivarse, respingo.Kick);
        return receptor;
    }

    /// <summary>Un corista en recortes (cuerpo y cabeza; con boca si se pide). La raíz va a la altura de su cintura.</summary>
    private static Transform Figura(string nombre, Transform padre, Dictionary<string, Pieza> piezas, string color, float escala,
                                    out Transform boca)
    {
        boca = null;
        var raiz = new GameObject(nombre).transform;
        raiz.SetParent(padre, false);
        raiz.localScale = Vector3.one * escala;
        if (!piezas.TryGetValue("corista_cuerpo_" + color, out var cuerpo) || cuerpo.sprite == null) return raiz;
        var srCuerpo = raiz.gameObject.AddComponent<SpriteRenderer>();
        srCuerpo.sprite = cuerpo.sprite;
        if (piezas.TryGetValue("corista_cabeza", out var cabeza) && cabeza.sprite != null)
        {
            var c = new GameObject("Cabeza");
            c.transform.SetParent(raiz, false);
            c.transform.localPosition = new Vector3(0f, cuerpo.sprite.bounds.max.y * 0.92f, -0.01f);
            var src = c.AddComponent<SpriteRenderer>();
            src.sprite = cabeza.sprite;
            src.sortingOrder = 1;
            if (piezas.TryGetValue("corista_boca", out var b) && b.sprite != null)
            {
                var go = new GameObject("Boca");
                go.transform.SetParent(c.transform, false);
                go.transform.localPosition = new Vector3(0f, 0.6f, -0.01f);
                var srb = go.AddComponent<SpriteRenderer>();
                srb.sprite = b.sprite;
                srb.sortingOrder = 2;
                boca = go.transform;
            }
        }
        return raiz;
    }

    /// <summary>
    /// El puente («¡Cerrad las puertas!»): el recuadro se para al fondo del escenario y cae una reja a la
    /// derecha. Los coristas salen de uno en uno a la tarima del fondo, cada uno con su escudo del color
    /// de una magia: con ella se rompe y desaparece. Mientras está en pie, en sus «¡Ah!» (el 2 y el 4)
    /// su línea del pentagrama se enciende y estalla. Luego, la frase triste: el grupo quieto y la cámara
    /// mira al Tenor del telón cantando a su público.
    /// </summary>
    private static void CrearEncierro(Transform raiz, TempoClock reloj, Dictionary<string, Pieza> piezas, Montaje montaje)
    {
        var padre = Hijo(raiz, "4b. El encierro (puente y frase triste)");
        float zs = ZDeLaSala, medio = AnchoDelCorredor * 0.5f;
        var pum = AssetDatabase.LoadAssetAtPath<AudioClip>(RutaPum);

        // «¡Cerrad las puertas!»: una reja cae a la derecha (el lado del público). Se va al empezar la
        // frase triste: si no, tapa al Tenor en el plano desde el público.
        var reja = Columnas("Reja que se cierra (¡Cerrad las puertas!)", padre, reloj, VozPuertas,
                            Puntos(padre, "Reja", new[] { new Vector3(medio + 0.6f, 0f, zs - 2f) }),
                            new Vector3(0.4f, 7f, 18f), 0f, 0.1f, pum, 1f, 0.3f, SegundoDeLaFraseTriste - SegundoDelPuente,
                            SegundoDelPuente - 1f, SegundoDelPuente + 1f, _grisOscuro);
        Poner(reja, "fromBelow", false);

        // La tarima del coro, al fondo, y cuatro braseros que laten en cada tiempo (el fuego de base).
        Caja("Tarima del coro", padre, new Vector3(0f, 0.3f, zs + 5.2f), new Vector3(AnchoDelCorredor, 0.6f, 1.6f), _madera, true);
        var fuego = Hijo(padre, "Fuego de base").gameObject.AddComponent<ScoreCueTrigger>();
        PonerRef(fuego, "clock", reloj);
        PonerTextos(fuego, "voices", new[] { "FUEGO" });
        foreach (var sitio in new[] { new Vector3(-medio + 0.5f, 0.5f, zs - 7f), new Vector3(medio - 0.5f, 0.5f, zs - 7f),
                                      new Vector3(-medio + 0.5f, 0.5f, zs + 3.6f), new Vector3(medio - 0.5f, 0.5f, zs + 3.6f) })
        {
            var brasero = Caja("Brasero", fuego.transform, sitio, new Vector3(0.6f, 1f, 0.6f), _brasa, false);
            Object.DestroyImmediate(brasero.GetComponent<Collider>());
            var late = brasero.AddComponent<PulseMotion>();
            PonerRef(late, "clock", reloj);
            PonerVector(late, "scaleKick", new Vector3(0.3f, 0.8f, 0.3f));
            Poner(late, "decaySeconds", 0.3f);
            UnityEventTools.AddVoidPersistentListener(fuego.onCue, late.Kick);
        }

        // Los coristas, cada uno con su escudo y su línea del pentagrama.
        ReceptorDeMagia anterior = null;
        for (int c = 0; c < Coristas.Length; c++)
        {
            var (escudos, x, escala) = Coristas[c];
            string color = escudos[0];
            var sitio = Hijo(padre, $"Corista {c + 1} ({string.Join(" y ", escudos)})");
            sitio.position = new Vector3(x, 0.6f + 3.24f * escala, zs + 5.2f);
            if (c == 0)
            {
                // El primero sale de la tarima en el primer compás («¡Coristas, al escenario!»).
                var sale = sitio.gameObject.AddComponent<CueSinkingTile>();
                PonerRef(sale, "clock", reloj);
                Poner(sale, "cueSeconds", 0.05f);
                Poner(sale, "warningSeconds", 0f);
                Poner(sale, "shake", 0f);
                Poner(sale, "downSeconds", Mathf.Max(0.2f, SalidaDelPrimerCorista - 0.05f));
                Poner(sale, "sinkDepth", 6f);
                Poner(sale, "moveSeconds", 0.45f);
            }

            var dePie = Figura("De pie", sitio, piezas, escudos[escudos.Length - 1], escala, out var boca);
            var balanceo = dePie.gameObject.AddComponent<PulseMotion>();
            PonerRef(balanceo, "clock", reloj);
            Poner(balanceo, "onEveryBeat", true);
            Poner(balanceo, "alternate", true);
            PonerVector(balanceo, "rotationKick", new Vector3(0f, 0f, 5f));
            Poner(balanceo, "decaySeconds", 0.3f);
            // Sus escudos, uno dentro de otro: cada uno se rompe con su magia y deja ver el siguiente.
            var escudosHechos = new List<(GameObject go, ReceptorDeMagia receptor)>();
            for (int e = 0; e < escudos.Length; e++)
            {
                var go = new GameObject($"Escudo {e + 1} ({escudos[e]})");
                go.transform.SetParent(dePie, false);
                go.transform.localPosition = new Vector3(0f, -0.1f, -0.05f);
                go.transform.localScale = Vector3.one * (1f - 0.12f * e);
                if (piezas.TryGetValue("corista_burbuja_" + escudos[e], out var burbuja) && burbuja.sprite != null)
                {
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = burbuja.sprite;
                    sr.sortingOrder = 5;
                }
                var cuerpo = go.AddComponent<BoxCollider>();
                cuerpo.size = new Vector3(5.2f, 6.4f, 1f);
                var r = go.AddComponent<ReceptorDeMagia>();
                PonerEnumArray(r, "elementosAceptados", new[] { (int)ElementoDe(escudos[e]) });
                Poner(r, "modo", (int)ReceptorDeMagia.Modo.Permanente);
                PonerRefs(r, "brillo", new Object[0]);
                if (e > 0) go.SetActive(false);
                escudosHechos.Add((go, r));
                montaje.Receptores.Add(r);
                if (e == 0) montaje.Vuelven.Add(go); else montaje.SeVan.Add(go);
            }
            for (int e = 0; e + 1 < escudosHechos.Count; e++)
            {
                UnityEventTools.AddBoolPersistentListener(escudosHechos[e].receptor.alActivarse, escudosHechos[e].go.SetActive, false);
                UnityEventTools.AddBoolPersistentListener(escudosHechos[e].receptor.alActivarse, escudosHechos[e + 1].go.SetActive, true);
            }
            var receptor = escudosHechos[escudosHechos.Count - 1].receptor;


            // Su rayo: la línea del pentagrama de su carril, del coro hacia el grupo.
            // Su raya: una línea del pentagrama que en cada tiempo salta al carril de al lado, de un lado a
            // otro (el siguiente carril se ve tenue antes).
            var rayos = CrearRaya($"Raya de {color} (¡Ah!)", padre, reloj, zs + 4.2f - 8f, 16f, c * 2);
            if (boca != null)
            {
                var canta = rayos.gameObject.AddComponent<ScoreCueTrigger>();
                PonerRef(canta, "clock", reloj);
                PonerTextos(canta, "voices", new[] { VozRayo(c) });
                Poner(canta, "warningSeconds", 0.45f);
                var abre = boca.gameObject.AddComponent<PulseMotion>();
                PonerRef(abre, "clock", reloj);
                PonerVector(abre, "scaleKick", new Vector3(0.3f, 1.2f, 0f));
                Poner(abre, "decaySeconds", 0.3f);
                UnityEventTools.AddVoidPersistentListener(canta.onWarning, abre.Kick);
            }

            UnityEventTools.AddBoolPersistentListener(receptor.alActivarse, receptor.gameObject.SetActive, false);
            UnityEventTools.AddBoolPersistentListener(receptor.alActivarse, dePie.gameObject.SetActive, false);
            UnityEventTools.AddBoolPersistentListener(receptor.alActivarse, rayos.gameObject.SetActive, false);
            montaje.Vuelven.Add(dePie.gameObject);
            if (c == 0) montaje.Vuelven.Add(rayos.gameObject);
            else
            {
                // Los demás esperan a que se rompa el anterior: fuera hasta entonces.
                sitio.gameObject.SetActive(false);
                rayos.gameObject.SetActive(false);
                montaje.SeVan.Add(sitio.gameObject);
                montaje.SeVan.Add(rayos.gameObject);
                UnityEventTools.AddBoolPersistentListener(anterior.alActivarse, sitio.gameObject.SetActive, true);
                UnityEventTools.AddBoolPersistentListener(anterior.alActivarse, rayos.gameObject.SetActive, true);
            }
            anterior = receptor;
        }

        // Si queda algún corista en pie, el fondo no se abre: detrás del telón que se hunde en «¡Rayos y
        // truenos!» espera otro igual, que solo se quita al romper al último. Sin él, la sombra alcanza.
        var cierre = Caja("Telón cerrado (hasta romper a todos los coristas)", padre,
                          new Vector3(0f, AltoDeLosMuros * 0.5f, ZDelFondoDelEscenario + 0.75f), new Vector3(AnchoDelCorredor + 2f, AltoDeLosMuros, 0.3f), _telon, false);
        UnityEventTools.AddBoolPersistentListener(anterior.alActivarse, cierre.SetActive, false);
        montaje.Vuelven.Add(cierre);

        // La base del puente: una nota a por el grupo en cada primer tiempo de compás.
        piezas.TryGetValue("nota_que_cae", out var nota);
        // Hasta la tralla: luego ella manda en las filas y lo demás no cae encima.
        var notas = Caida("Notas del puente (a por ti)", padre, reloj, nota, VozNotaDelPuente, SegundoDelPuente, DesdeLaTralla - 0.1f,
                          Puntos(padre, "Nota del puente", new[] { new Vector3(0f, 0f, zs - 2f) }), 1.6f, 12f, 1.3f, pum, 1f, 1.3f);
        Poner(notas, "aimAtTarget", true);
        Poner(notas, "aimMaxDistance", 6f);

        CrearRecintoDelCoro(padre, montaje, medio, zs);
        var tralla = MomentosDeTralla(_tiempos);
        for (int f = 0; f < FilasDeLaTralla.Length; f++)
        {
            float z = FilasDeLaTralla[f] + zs;
            foreach (var golpe in tralla)
                if ((golpe.filas & (1 << f)) != 0) AlMapa(golpe.segundo, 0f, z, medio, "fila de semicorcheas");
            var caida = Caida($"Semicorcheas (fila {f + 1})", padre, reloj, nota, VocesDeLaTralla[f], DesdeLaTralla - 0.1f,
                SegundoDeLaFraseTriste, Puntos(padre, "Fila", new[] { new Vector3(0f, 0f, z) }),
                1.45f, 14f, 1f, null, 0f, 0.72f);
            ConfigurarFranja(caida, new Vector2(AnchoDelCorredor, AnchoDeFilaDelCoro), 5f);
        }
        var golpeDelCoro = Hijo(padre, "Golpe de las semicorcheas").gameObject.AddComponent<CueDrop>();
        PonerRef(golpeDelCoro, "clock", reloj);
        PonerTextos(golpeDelCoro, "voices", new[] { VozGolpeDelCoro });
        PonerRefs(golpeDelCoro, "landingPoints", Puntos(padre, "Centro del golpe", new[] { new Vector3(0f, 0f, zs - 2f) }));
        PonerRef(golpeDelCoro, "landingSound", pum);
        Poner(golpeDelCoro, "soundVolume", 0.5f);
        Poner(golpeDelCoro, "damage", 0f);

        // La frase triste: plano desde el público, acercándose despacio al Tenor del telón.
        var desde = Hijo(padre, "Plano de la frase triste (desde)");
        var hasta = Hijo(padre, "Plano de la frase triste (hasta)");
        var cara = new Vector3(0f, 6.5f, ZDelFondoDelEscenario);
        // De frente y desde abajo, en el eje del escenario: el Tenor canta hacia la cámara (el público).
        desde.position = new Vector3(0f, 2.6f, zs - 5f);
        desde.rotation = Quaternion.LookRotation(cara - desde.position, Vector3.up);
        hasta.position = new Vector3(0f, 3f, zs);
        hasta.rotation = Quaternion.LookRotation(cara - hasta.position, Vector3.up);
        montaje.Planos.Add((new Vector2(SegundoDeLaFraseTriste, Truenos - 0.2f), desde, hasta, 42f));
    }

    private static void CrearRecintoDelCoro(Transform padre, Montaje montaje, float medio, float zs)
    {
        float mitadDeFondo = AnchoDeFilaDelCoro * 1.5f;
        var recinto = Hijo(padre, "Arena de tres filas");
        recinto.position = new Vector3(0f, 0f, zs + FilasDeLaTralla[1]);
        montaje.RecintoDelCoro = recinto;
        void Baranda(Vector3 posicion, Vector3 tamano)
        {
            var baranda = Caja("Baranda", recinto, posicion + Vector3.up * 0.4f, tamano, _madera, false);
            var choque = baranda.GetComponent<BoxCollider>();
            choque.size = new Vector3(1f, 8f, 1f);
            choque.center = Vector3.up * 3.5f;
        }
        foreach (float lado in new[] { -1f, 1f })
        {
            Baranda(recinto.position + Vector3.forward * (lado * mitadDeFondo), new Vector3(AnchoDelCorredor, 0.8f, 0.25f));
            Baranda(recinto.position + Vector3.right * (lado * medio), new Vector3(0.25f, 0.8f, mitadDeFondo * 2f));
            Raya("Separación de filas", recinto, recinto.position + new Vector3(0f, 0.035f, lado * AnchoDeFilaDelCoro * 0.5f),
                new Vector3(AnchoDelCorredor, 0.025f, 0.08f));
        }
        recinto.gameObject.SetActive(false);

        var plano = Hijo(padre, "Plano fijo de las tres filas");
        plano.position = recinto.position + new Vector3(0f, 14f, -12f);
        plano.rotation = Quaternion.LookRotation(recinto.position + Vector3.forward - plano.position, Vector3.up);
        montaje.Planos.Add((new Vector2(SegundoDelPuente, SegundoDeLaFraseTriste), plano, plano, 45f));
    }

    private static void ConfigurarFranja(CueDrop golpe, Vector2 tamano, float alto)
    {
        var datos = new SerializedObject(golpe);
        datos.FindProperty("strikeSize").vector2Value = tamano;
        datos.FindProperty("strikeHeight").floatValue = alto;
        var plantilla = (Transform)datos.FindProperty("shadowTemplate").objectReferenceValue;
        if (plantilla != null)
        {
            var marca = Caja("Aviso (franja)", plantilla.parent, plantilla.position,
                new Vector3(tamano.x, 0.035f, tamano.y), _aviso, false);
            Object.DestroyImmediate(marca.GetComponent<Collider>());
            datos.FindProperty("shadowTemplate").objectReferenceValue = marca.transform;
            Object.DestroyImmediate(plantilla.gameObject);
        }
        datos.FindProperty("shadowStartScale").floatValue = 1f;
        datos.FindProperty("shadowEndScale").floatValue = 1f;
        datos.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void HacerNotaSolida(CueDrop caida)
    {
        var datos = new SerializedObject(caida);
        var plantilla = (GameObject)datos.FindProperty("fallingTemplate").objectReferenceValue;
        plantilla.layer = LayerMask.NameToLayer("Ignore Raycast");
        var choque = plantilla.AddComponent<BoxCollider>();
        float escala = plantilla.transform.localScale.x;
        choque.size = new Vector3(3.8f, 2.2f, 2.8f) / escala;
        choque.center = Vector3.up * (1.1f / escala);
        ConfigurarFranja(caida, new Vector2(3.8f, 2.8f), 2.2f);
        Poner(caida, "lingerSeconds", 0.6f);
    }

    private static void AjustarAlAvance(CueDrop caida, float margen)
    {
        Poner(caida, "aimAtTarget", true);
        Poner(caida, "aimAlongForwardOnly", true);
        Poner(caida, "aimMaxDistance", margen);
    }

    private static RayaQueBarre CrearRaya(string nombre, Transform padre, TempoClock reloj, float zDelCentro, float largo, int primerCarril)
    {
        var go = Hijo(padre, nombre);
        Transform Linea(string n, float alfa)
        {
            Transform t;
            if (_piezas.TryGetValue("atrezo_rayo", out var recorte) && recorte.sprite != null)
            {
                t = Atrezo(go, recorte.sprite, new Vector3(0.9f, 0.5f, largo), true).transform;
                t.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, alfa);
            }
            else
            {
                var caja = Caja(n, go, Vector3.zero, new Vector3(0.6f, 0.05f, largo), _rayo, false);
                Object.DestroyImmediate(caja.GetComponent<Collider>());
                t = caja.transform;
            }
            t.name = n;
            return t;
        }
        var raya = Linea("Raya", 1f);
        var aviso = Linea("Aviso (carril siguiente)", 0.3f);
        var barre = go.gameObject.AddComponent<RayaQueBarre>();
        PonerRef(barre, "reloj", reloj);
        Poner(barre, "primerCarril", primerCarril);
        Poner(barre, "zDelCentro", zDelCentro);
        Poner(barre, "largo", largo);
        Poner(barre, "desde", SalidaDelPrimerCorista + 0.4f);
        Poner(barre, "hasta", SegundoDeLaFraseTriste);
        PonerRef(barre, "raya", raya);
        PonerRef(barre, "aviso", aviso);
        return barre;
    }

    /// <summary>
    /// El final («¡Rayos y truenos! ¡Seguid sin parar!»): se abre el telón del fondo y se sale a lo alto
    /// del templo, más ancho que el pasillo, con la cara entera del Tenor delante. Primero los practicables
    /// sobre el humo (de fila en fila saltando, cada dos tiempos); luego todo lo aprendido a la vez: latigazos
    /// en el 1 y el 3, pirotecnia, notas y claves, y los huecos de las semicorcheas hasta el salto y el dash
    /// del final. En cada compás sale un corista más a cada lado; en «¡Bravo!» llueven rosas; en la lágrima,
    /// la ola viene detrás y el recuadro corre más.
    /// </summary>
    private static void CrearFinal(Transform raiz, TempoClock reloj, MusicScore partitura, Dictionary<string, Pieza> piezas, Montaje montaje)
    {
        var padre = Hijo(raiz, "5. El final (¡Rayos y truenos!)");
        float medio = AnchoDelFinal * 0.5f, z0 = ZDelFondoDelEscenario + 0.6f, fin = ZDelFinal;
        // En la prueba el suelo no se hunde en el golpe final (la caída al vacío la pondrá la historia):
        // se gana y viene la despedida de la cámara con la pregunta de repetir.
        var suelo = Hijo(padre, "Suelo del final");

        float zSuelo = CrearPracticables(padre, suelo, reloj, piezas, z0);
        // De los practicables a la meta, el suelo con los huecos de las semicorcheas: cada grupo rompe
        // delante, losa a losa y de la más cercana a la más lejana, un hueco que se cruza justo después
        // de su última nota (un salto); el último grupo, el vacío del final (salto y dash en el aire).
        var huecos = new List<Vector2>();
        for (int g = 0; g < GruposDeSemicorcheas.Count; g++)
        {
            var grupo = GruposDeSemicorcheas[g];
            bool final = g == GruposDeSemicorcheas.Count - 1;
            float desde = final ? ZDelBordeDelVacioFinal : Z(grupo[grupo.Count - 1] + 0.25f) + BordeDelVacioFinal;
            float largo = final ? LargoDelVacioFinal : LargoDeUnHuecoDeSemicorcheas;
            if (huecos.Count > 0) desde = Mathf.Max(desde, huecos[huecos.Count - 1].y + 4f);
            desde = Mathf.Max(desde, zSuelo + 4f);
            CrearHuecoDeSemicorcheas(padre, suelo, reloj, piezas, grupo, desde, largo, final ? "Vacío del final" : $"Hueco {g + 1}");
            huecos.Add(new Vector2(desde, desde + largo));
        }
        if (huecos.Count == 0)
        {
            float zBorde = ZDelBordeDelVacioFinal;
            Caja("Vacío del final (sin semicorcheas: suelo)", suelo, new Vector3(0f, -0.5f, zBorde + LargoDelVacioFinal * 0.5f),
                 new Vector3(AnchoDelFinal, 1f, LargoDelVacioFinal), _gris, true);
        }
        float zTramo = zSuelo;
        int trozo = 0;
        foreach (var h in huecos)
        {
            SueloDelFinal(suelo, ++trozo, zTramo, h.x);
            zTramo = h.y;
        }
        SueloDelFinal(suelo, ++trozo, zTramo, fin);
        float bordeDelSalto = huecos.Count > 0 ? huecos[huecos.Count - 1].x : ZDelBordeDelVacioFinal;
        float inicioDelRellano = huecos.Count > 1 ? huecos[huecos.Count - 2].y : zSuelo;
        CrearCoda(padre, montaje, inicioDelRellano, bordeDelSalto, zTramo);
        var saludo = Hijo(padre, "Marca del saludo final");
        saludo.SetPositionAndRotation(new Vector3(0f, 0.15f, Mathf.Lerp(zTramo, fin, 0.55f)),
                                      Quaternion.LookRotation(Vector3.back, Vector3.up));
        montaje.MarcaDelSaludo = saludo;
        var focoDelSaludo = Hijo(padre, "Cenital del saludo final");
        focoDelSaludo.SetPositionAndRotation(saludo.position + Vector3.up * 12f,
                                          Quaternion.LookRotation(Vector3.down, Vector3.forward));
        var luzDelSaludo = focoDelSaludo.gameObject.AddComponent<Light>();
        luzDelSaludo.type = LightType.Spot;
        luzDelSaludo.color = new Color(1f, 0.87f, 0.64f);
        luzDelSaludo.intensity = 26f;
        luzDelSaludo.range = 21f;
        luzDelSaludo.spotAngle = 48f;
        luzDelSaludo.innerSpotAngle = 33f;
        luzDelSaludo.enabled = false;
        montaje.FocoDelSaludo = luzDelSaludo;

        // Pretiles a los dos lados (con almenas) y una barandilla que no se ve.
        foreach (float lado in new[] { -1f, 1f })
        {
            Caja("Pretil", suelo, new Vector3(lado * (medio + 0.2f), 0.35f, (z0 + fin) * 0.5f), new Vector3(0.4f, 0.7f, fin - z0), _grisOscuro, false);
            for (float zz = z0 + 1f; zz < fin; zz += 2f)
            {
                var almena = Caja("Almena", suelo, new Vector3(lado * (medio + 0.2f), 0.95f, zz), new Vector3(0.5f, 0.5f, 0.9f), _grisOscuro, false);
                Object.DestroyImmediate(almena.GetComponent<Collider>());
            }
            var barandilla = new GameObject("Barandilla (invisible)");
            barandilla.transform.SetParent(suelo, false);
            barandilla.transform.position = new Vector3(lado * (medio + 0.4f), 1.5f, (z0 + fin) * 0.5f);
            barandilla.layer = LayerMask.NameToLayer("Ignore Raycast");
            barandilla.AddComponent<BoxCollider>().size = new Vector3(0.4f, 3f, fin - z0);
            // El público a los dos lados: salta en cada compás y aplaude en «¡Bravo!».
            Publico(padre, reloj, lado, z0, fin, new[] { VozPlatillo, VozRosas }, medio);
        }

        // Un corista más a cada lado en cada compás, hasta que no cabe ninguno.
        string[] colores = { "luz", "fuego", "viento", "mente", "oscuro" };
        var compases = DeLaPartitura(partitura, VozCoristaQueAparece, Truenos - 0.1f, SegundoDeLaMeta);
        int n = 0;
        foreach (float t in compases)
        {
            foreach (float lado in new[] { -1f, 1f })
            {
                float zc = Mathf.Min(Z(t) + 14f, fin - 1f);
                var sitio = Hijo(suelo, $"Corista del final {++n}");
                sitio.position = new Vector3(lado * (medio + 1.5f), 1f + 3.24f * 0.45f, zc);
                Caja("Peana", sitio, new Vector3(lado * (medio + 1.5f), 0.5f, zc), new Vector3(1.4f, 1f, 1.4f), _grisOscuro, false);
                var sale = sitio.gameObject.AddComponent<CueSinkingTile>();
                PonerRef(sale, "clock", reloj);
                Poner(sale, "cueSeconds", 0.05f);
                Poner(sale, "warningSeconds", 0f);
                Poner(sale, "shake", 0f);
                Poner(sale, "downSeconds", Mathf.Max(0.2f, t - 0.05f));
                Poner(sale, "sinkDepth", 5f);
                Poner(sale, "moveSeconds", 0.35f);
                var figura = Figura("Corista", sitio, piezas, colores[n % colores.Length], 0.45f, out _);
                figura.gameObject.AddComponent<CartelQueMiraALaCamara>();
                var balanceo = figura.gameObject.AddComponent<PulseMotion>();
                PonerRef(balanceo, "clock", reloj);
                Poner(balanceo, "onEveryBeat", true);
                Poner(balanceo, "alternate", true);
                PonerVector(balanceo, "positionKick", new Vector3(0f, 0.15f, 0f));
                PonerVector(balanceo, "rotationKick", new Vector3(0f, 0f, 6f));
                Poner(balanceo, "decaySeconds", 0.3f);
            }
        }

        // La cara entera del Tenor, al fondo, cantando.
        var tenor = CrearTenor(padre, reloj, piezas, "El Tenor (la cara entera)", new Vector3(0f, 1f, fin + 8f), 1.4f);
        if (tenor != null) tenor.gameObject.AddComponent<CartelQueMiraALaCamara>();
        montaje.TenorDelFinal = tenor;

        var grave = AssetDatabase.LoadAssetAtPath<AudioClip>(RutaOrganoGrave);
        var pum = AssetDatabase.LoadAssetAtPath<AudioClip>(RutaPum);
        var fogonazo = AssetDatabase.LoadAssetAtPath<AudioClip>(RutaFogonazo);

        // Latigazos en el 1 y el 3, dos por un lado y dos por el otro.
        var latigazos = DeLaPartitura(partitura, VozLatigazoDelFinal, Truenos, SegundoDeLaMeta);
        var sitios = new List<Vector3>();
        for (int i = 0; i < latigazos.Count; i++)
        {
            float x = XDeLatigazo(latigazos[i], i);
            sitios.Add(new Vector3(x, 0f, Z(latigazos[i])));
            AlMapa(latigazos[i], x, Z(latigazos[i]), 2.75f, "latigazo");
        }
        Columnas("Latigazos del final", padre, reloj, VozLatigazoDelFinal, Puntos(padre, "Latigazo", sitios.ToArray()),
                 new Vector3(5.5f, 0.8f, 1.4f), 8f, 2.6f, null, 0f, 0.6f, 0.35f, Truenos, SegundoDeLaMeta, atrezo: "atrezo_latigo");

        // Pirotecnia y notas, como en el estribillo.
        // Tras los practicables (sobre ellos no hay suelo del que salga fuego).
        var piro = DeLaPartitura(partitura, VozPirotecnia, FinDeLosPracticables, SegundoDeLaMeta);
        sitios.Clear();
        for (int i = 0; i < piro.Count; i++)
        {
            float x = XDePirotecniaDelFinal(piro[i], i);
            sitios.Add(new Vector3(x, 0f, Z(piro[i])));
            AlMapa(piro[i], x, Z(piro[i]), 2.4f, "pirotecnia");
        }
        Columnas("Pirotecnia del final", padre, reloj, VozPirotecnia, Puntos(padre, "Chorro", sitios.ToArray()),
                 new Vector3(4.8f, 2.4f, 1.2f), 8f, 2.5f, fogonazo, 0.6f, 0.6f, 0.45f, FinDeLosPracticables, SegundoDeLaMeta, _brasa, atrezo: "atrezo_llama");
        piezas.TryGetValue("nota_que_cae", out var nota);
        sitios.Clear();
        int turno = 0;
        foreach (float t in DeLaPartitura(partitura, VozNotaDelEstribillo, FinDeLosPracticables, SegundoDeLaMeta))
        {
            float x = XFueraDelCamino(t, 1.3f, ref turno);
            float z = Mathf.Max(Z(t), zSuelo + 0.5f);
            foreach (var hueco in huecos)
                if (z >= hueco.x && z < hueco.y) { z = hueco.x - 0.5f; break; }
            sitios.Add(new Vector3(x, 0f, z));
            AlMapa(t, x, z, 1.3f, "nota");
        }
        if (sitios.Count > 0)
        {
            var notasDelFinal = Caida("Notas del final", padre, reloj, nota, VozNotaDelEstribillo, FinDeLosPracticables, SegundoDeLaMeta,
                  Puntos(padre, "Nota", sitios.ToArray()), 1.6f, 12f, 1.3f, pum, 1f, 0.85f);
            HacerNotaSolida(notasDelFinal);
        }
        var golpes = DeLaPartitura(partitura, VozGolpe, Truenos, SegundoDeLaMeta);
        if (golpes.Count > 0)
        {
            sitios.Clear();
            foreach (float t in golpes)
            {
                float x = XFueraDelCamino(t, 0.8f, ref turno, 1.1f);
                sitios.Add(new Vector3(x, 0f, Z(t)));
                AlMapa(t, x, Z(t), 0.8f, "pilar");
            }
            Columnas("Pilares grabados del final", padre, reloj, VozGolpe, Puntos(padre, "Pilar", sitios.ToArray()),
                     new Vector3(1.6f, 3.8f, 1.6f), 10f, 1f, grave, 1f, 0.8f, 1.1f, Truenos, SegundoDeLaMeta, atrezo: "atrezo_columna");
        }

        // «¡Bravo, bravo!»: llueven rosas (no hacen daño).
        var rosa = new GameObject("Rosa (plantilla)");
        var flor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flor.transform.SetParent(rosa.transform, false);
        flor.transform.localScale = new Vector3(0.45f, 0.35f, 0.45f);
        flor.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        Object.DestroyImmediate(flor.GetComponent<Collider>());
        flor.GetComponent<MeshRenderer>().sharedMaterial = _rosa;
        var rosas = Hijo(padre, "Rosas de «¡Bravo!»");
        rosa.transform.SetParent(rosas, false);
        var lluvia = new List<Vector3>();
        var azar = new System.Random(7);
        for (int i = 0; i < 12; i++)
            lluvia.Add(new Vector3((float)(azar.NextDouble() * 8.0 - 4.0), 0f, Z(Bravo) + (float)(azar.NextDouble() * 12.0)));
        var caenRosas = rosas.gameObject.AddComponent<CueDrop>();
        PonerRef(caenRosas, "clock", reloj);
        PonerTextos(caenRosas, "voices", new[] { VozRosas });
        PonerRefs(caenRosas, "landingPoints", Puntos(rosas, "Rosa", lluvia.ToArray()));
        PonerRef(caenRosas, "fallingTemplate", rosa);
        Poner(caenRosas, "fallSeconds", 1.1f);
        Poner(caenRosas, "dropHeight", 14f);
        Poner(caenRosas, "lingerSeconds", 1.5f);
        Poner(caenRosas, "damage", 0f);

        // «Se me escapa una lagrimita…»: cae una lágrima de piedra enorme detrás del grupo.
        var lagrima = new GameObject("Lágrima (plantilla)");
        var gota = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        gota.transform.SetParent(lagrima.transform, false);
        gota.transform.localScale = new Vector3(2.4f, 3.2f, 2.4f);
        gota.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        Object.DestroyImmediate(gota.GetComponent<Collider>());
        gota.GetComponent<MeshRenderer>().sharedMaterial = _lagrima;
        var cayendo = Hijo(padre, "La lágrima");
        lagrima.transform.SetParent(cayendo, false);
        var caeLagrima = cayendo.gameObject.AddComponent<CueDrop>();
        PonerRef(caeLagrima, "clock", reloj);
        PonerTextos(caeLagrima, "voices", new[] { VozLagrima });
        PonerRefs(caeLagrima, "landingPoints", Puntos(cayendo, "Donde cae", new[] { new Vector3(0f, 0f, Z(Lagrimita) - 9f) }));
        PonerRef(caeLagrima, "fallingTemplate", lagrima);
        Poner(caeLagrima, "fallSeconds", 1f);
        Poner(caeLagrima, "dropHeight", 22f);
        Poner(caeLagrima, "lingerSeconds", 0.4f);
        Poner(caeLagrima, "damage", 0f);
        PonerRef(caeLagrima, "landingSound", pum);

    }

    /// <summary>
    /// Lo que baja del telar («¡corred, corred!»): un saco de arena, una luna y una estrella de cartón, por
    /// turnos, bajan colgados de su cuerda unos metros por delante y se quedan colgados un rato, estorbando:
    /// los bajos (a la altura del cuerpo) cierran un carril fuera del camino; los altos (a la altura del
    /// salto) cuelgan sobre el camino, donde no hay que saltar, y no dejan saltar ahí. En el encierro
    /// cuelgan a los lados. Hacen daño al tocarlos; al acabar su rato suben.
    /// </summary>
    private static void CrearLoQueCaeDelTelar(Transform raiz, TempoClock reloj, MusicScore partitura, Dictionary<string, Pieza> piezas)
    {
        var todos = new List<(float t, int pieza)>();
        for (int k = 0; k < VocesDelTelar.Length; k++)
            foreach (float t in DeLaPartitura(partitura, VocesDelTelar[k], 0f, 100000f)) todos.Add((t, k));
        if (todos.Count == 0) return;
        todos.Sort((a, b) => a.t.CompareTo(b.t));
        var padre = Hijo(raiz, "6. Lo que baja del telar (¡corred, corred!)");
        int turno = 0;
        for (int n = 0; n < todos.Count; n++)
        {
            var (t, k) = todos[n];
            bool encierro = t >= SegundoDelPuente && t < Truenos + SalidaDelFinal;
            float cruza = t;
            // Los altos, solo donde no hay que saltar: lejos de los respiros (huecos de magia y plataformas).
            bool alto = !encierro && n % 2 == 1 && t < HastaElEstribillo && !EnUnRespiro(cruza - 2.5f) && !EnUnRespiro(cruza + 2.5f) && !EnUnRespiro(cruza);
            float x = encierro ? (n % 2 == 0 ? -4.2f : 4.2f) : alto ? XDelCamino(cruza) : XFueraDelCamino(cruza - 0.4f, 0.9f, ref turno, 1.2f);
            float z = encierro ? ZDeLaSala + 1f + (n % 3) * 1.6f : Z(t);
            float abajo = alto ? 2.3f : 0.35f;
            AlMapa(cruza, x, z, 0.9f, alto ? "telar alto" : "telar bajo");

            var colgado = Hijo(padre, $"Del telar {n + 1}: {PiezasDelTelar[k]}{(alto ? " (alto)" : "")}");
            colgado.position = new Vector3(x, abajo + AlturaDelTelar, z);
            piezas.TryGetValue(PiezasDelTelar[k], out var pieza);
            var recorte = RecorteSuelto("Recorte", colgado, pieza, colgado.position, 0.7f, true);
            // Colgado, se balancea en cada tiempo: también marca el ritmo.
            var bota = recorte.gameObject.AddComponent<PulseMotion>();
            PonerRef(bota, "clock", reloj);
            Poner(bota, "onEveryBeat", true);
            Poner(bota, "alternate", true);
            PonerVector(bota, "positionKick", new Vector3(0f, 0.12f, 0f));
            PonerVector(bota, "rotationKick", new Vector3(0f, 0f, 5f));
            Poner(bota, "decaySeconds", 0.3f);
            var dano = new GameObject("Daño al tocarlo");
            dano.transform.SetParent(colgado, false);
            dano.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            dano.AddComponent<BoxCollider>().size = new Vector3(1.6f, 1.6f, 0.8f);
            var zona = dano.AddComponent<ZonaDeDano>();
            Poner(zona, "danoPorSegundo", 30f);
            Poner(zona, "cada", 0.3f);
            var baja = colgado.gameObject.AddComponent<CueSinkingTile>();
            PonerRef(baja, "clock", reloj);
            Poner(baja, "cueSeconds", t - 0.5f);
            Poner(baja, "warningSeconds", 0f);
            Poner(baja, "shake", 0f);
            Poner(baja, "downSeconds", encierro ? 4f : 3f);
            Poner(baja, "sinkDepth", AlturaDelTelar);
            Poner(baja, "moveSeconds", 0.5f);
            Poner(baja, "collidersOffWhileDown", false);
        }
    }

    // Lo que aplasta el órgano al caer: algo menos de lo que ocupa, para que quepa al lado del camino.
    private const float RadioDelOrgano = 1.4f;

    // «Por arriba»: un practicable (tarima de teatro) a la derecha en el tramo del estribillo entre los dos
    // huecos de magia; por el suelo de al lado barre un latigazo cada dos tiempos y solo se pasa por encima.
    // Empieza pasado el hueco de Will (si no, por encima se rodeaba el hueco).
    private static float DesdeElPracticable => SegundoAlPasarElHueco(RespiroDeWill) + 0.2f;
    private const float BordeDelPracticable = 1f;
    private const string VozDelBarrido = "BARRIDO_DEL_PRACTICABLE";

    // La comba: un latigazo que barre todo el suelo, bajo, en el primer tiempo de cada compás; se salta
    // en el 1 (está en el camino). En la estrofa 1 y al final del estribillo.
    private const string VozComba = "COMBA";
    private static readonly Vector2[] TramosDeComba = { new Vector2(24.3f, 31.2f), new Vector2(94.6f, 96.9f) };
    private const float AltoDeLaComba = 0.6f;

    private const string VozGolpeDelCoro = "GOLPE_FILAS_CORO";
    private static readonly string[] VocesDeLaTralla = { "CORO_FILA_1", "CORO_FILA_2", "CORO_FILA_3" };
    private const float DesdeLaTralla = 107.4f;
    private const float AnchoDeFilaDelCoro = 3f;
    private static readonly float[] FilasDeLaTralla = { 1f, -2f, -5f };

    private static bool EnTramoDeComba(float segundo)
    {
        foreach (var tramo in TramosDeComba)
            if (segundo >= tramo.x && segundo <= tramo.y) return true;
        return false;
    }

    private static List<float> MomentosDeComba()
    {
        var lista = new List<float>();
        for (int i = 0; i < _tiempos.Length; i += 4)
            if (EnTramoDeComba(_tiempos[i]) && System.Array.IndexOf(AcentosDeLaLetra, _tiempos[i]) < 0)
                lista.Add(_tiempos[i]);
        return lista;
    }

    private static List<(float segundo, int filas)> MomentosDeTralla(float[] tiempos)
    {
        var lista = new List<(float segundo, int filas)>();
        int[] patron = { 0b001, 0b100, 0b101, 0b110, 0b101, 0b011, 0b101 };
        int primerTiempo = System.Array.FindIndex(tiempos, t => t >= DesdeLaTralla);
        if (primerTiempo < 0) return lista;
        for (int i = primerTiempo; i < tiempos.Length && lista.Count < patron.Length; i += 2)
        {
            if (tiempos[i] >= SegundoDeLaFraseTriste - 0.25f) break;
            lista.Add((tiempos[i], patron[lista.Count]));
        }
        return lista;
    }

    /// <summary>
    /// La comba: en cada uno de sus momentos, un latigazo de cartón barre todo el suelo, bajo; golpea solo
    /// hasta <see cref="AltoDeLaComba"/> m, así que se salta en el 1.
    /// </summary>
    private static void CrearCombas(Transform raiz, TempoClock reloj)
    {
        var momentos = MomentosDeComba();
        if (momentos.Count == 0) return;
        var padre = Hijo(raiz, "7. La comba (saltar en el 1)");
        float hasta = TramosDeComba[TramosDeComba.Length - 1].y + 0.5f;
        var sitios = new List<Vector3>();
        foreach (float t in momentos) sitios.Add(new Vector3(0f, 0f, Z(t) - 0.45f));
        var comba = Columnas("Comba (salto)", padre, reloj, VozComba, Puntos(padre, "Comba", sitios.ToArray()),
                             new Vector3(AnchoDelCorredor, 0.5f, 2.4f), 10f, 1f, AssetDatabase.LoadAssetAtPath<AudioClip>(RutaPum), 0.35f, 0.7f, 0.8f,
                             TramosDeComba[0].x - 0.5f, hasta, atrezo: "atrezo_latigo");
        ConfigurarFranja(comba, new Vector2(AnchoDelCorredor, 2.4f), AltoDeLaComba);
        AjustarAlAvance(comba, 4.5f);
        foreach (float t in momentos) AlMapa(t, 0f, Z(t), MitadDelPasillo, "comba");
    }

    // Metros que sube lo del telar por encima de donde se queda colgado (fuera de la vista).
    private const float AlturaDelTelar = 12f;

    /// <summary>
    /// Los practicables sobre el humo (ver <see cref="Practicables"/>): tres tarimas grandes por fila sobre
    /// el vacío, con humo de hielo seco debajo; las notas que rompen las que no son del camino y el hundirse
    /// de la del camino al pasarla. Devuelve la Z donde vuelve el suelo.
    /// </summary>
    private static float CrearPracticables(Transform padre, Transform suelo, TempoClock reloj, Dictionary<string, Pieza> piezas, float z0)
    {
        if (Practicables.Count == 0) return z0;
        var grupo = Hijo(padre, "Practicables sobre el humo");
        var golpes = new List<Vector3>();
        var caidas = new List<Vector3>();
        for (int f = 0; f < Practicables.Count; f++)
        {
            var fila = Practicables[f];
            caidas.Add(new Vector3(fila.x, 0f, fila.z));
            int lateral = 0;
            foreach (float x in ColumnasDeLosPracticables)
            {
                bool delCamino = Mathf.Approximately(x, fila.x);
                var tarima = Caja($"Practicable {f + 1} ({x:0})", grupo, new Vector3(x, -0.4f, fila.z), new Vector3(AnchoDeUnPracticable, 0.8f, LargoDeUnPracticable),
                                  _madera, true);
                Raya("Borde", tarima.transform, new Vector3(x, 0.01f, fila.z - LargoDeUnPracticable * 0.5f + 0.1f), new Vector3(AnchoDeUnPracticable, 0.02f, 0.2f));
                var cae = tarima.AddComponent<CueSinkingTile>();
                PonerRef(cae, "clock", reloj);
                float segundo = delCamino ? fila.rompeLaSuya : lateral++ == 0 ? fila.rompeLasOtras : fila.rompeLaSegunda;
                Poner(cae, "cueSeconds", segundo);
                Poner(cae, "warningSeconds", delCamino ? 0.4f : 0f);
                Poner(cae, "shake", delCamino ? 0.08f : 0f);
                Poner(cae, "downSeconds", 1000f);
                Poner(cae, "sinkDepth", 14f);
                Poner(cae, "moveSeconds", 0.45f);
                AlMapa(fila.t, x, fila.z, AnchoDeUnPracticable * 0.5f, delCamino ? "practicable" : "se rompe");
                if (!delCamino) golpes.Add(new Vector3(x, 0f, fila.z));
            }
        }
        // Las notas que las rompen, en el orden de sus golpes (dos por fila, pa-pa).
        piezas.TryGetValue("nota_que_cae", out var nota);
        Caida("Notas que rompen los practicables", grupo, reloj, nota, VozRompePracticable, Truenos, FinDeLosPracticables,
              Puntos(grupo, "Rompe", golpes.ToArray()), 1.6f, 0f, 1.5f, AssetDatabase.LoadAssetAtPath<AudioClip>(RutaPum), 1f, 0.8f);
        var golpeDelSuelo = Hijo(grupo, "Sonido de los practicables que ceden").gameObject.AddComponent<CueDrop>();
        PonerRef(golpeDelSuelo, "clock", reloj);
        PonerTextos(golpeDelSuelo, "voices", new[] { VozCaePracticable });
        PonerRefs(golpeDelSuelo, "landingPoints", Puntos(grupo, "Tablas", caidas.ToArray()));
        PonerRef(golpeDelSuelo, "landingSound", AssetDatabase.LoadAssetAtPath<AudioClip>(RutaPum));
        Poner(golpeDelSuelo, "soundVolume", 0.28f);
        Poner(golpeDelSuelo, "damage", 0f);
        float zFin = Practicables[Practicables.Count - 1].z + LargoDeUnPracticable * 0.5f + 1.6f;
        // El humo de hielo seco: el vacío de debajo.
        var humo = Caja("Humo de hielo seco", grupo, new Vector3(0f, -3f, (z0 + zFin) * 0.5f), new Vector3(AnchoDelFinal + 30f, 0.2f, zFin - z0 + 6f), _humo, false);
        Object.DestroyImmediate(humo.GetComponent<Collider>());
        return zFin;
    }

    /// <summary>Un trozo de suelo del final, con su pentagrama.</summary>
    private static void SueloDelFinal(Transform suelo, int numero, float desde, float hasta)
    {
        if (hasta - desde < 0.05f) return;
        Caja($"Suelo del final {numero}", suelo, new Vector3(0f, -0.5f, (desde + hasta) * 0.5f), new Vector3(AnchoDelFinal, 1f, hasta - desde), _gris, true);
        for (int i = 0; i < 5; i++)
            Raya("Pentagrama del final", suelo, new Vector3(-4f + i * 2f, 0.01f, (desde + hasta) * 0.5f), new Vector3(0.14f, 0.02f, hasta - desde));
    }

    /// <summary>
    /// Un hueco de semicorcheas: una losa por nota, como las teclas de un piano, y la semicorchea que cae
    /// sobre cada una (no hace daño: el peligro es el hueco). Cada losa se hunde cuando cae la suya; al
    /// reintentar vuelven.
    /// </summary>
    private static void CrearHuecoDeSemicorcheas(Transform padre, Transform suelo, TempoClock reloj, Dictionary<string, Pieza> piezas,
                                                 List<float> notas, float zBorde, float largoDelHueco, string nombre)
    {
        int n = notas.Count;
        float largo = largoDelHueco / n;
        var sitios = new Vector3[n];
        for (int k = 0; k < n; k++)
        {
            float zc = zBorde + (k + 0.5f) * largo;
            var losa = Caja($"{nombre}: tecla {k + 1}", suelo, new Vector3(0f, -0.5f, zc), new Vector3(AnchoDelFinal, 1f, largo - 0.04f),
                            k % 2 == 0 ? _madera : _grisOscuro, true);
            var seRompe = losa.AddComponent<CueSinkingTile>();
            PonerRef(seRompe, "clock", reloj);
            Poner(seRompe, "cueSeconds", notas[k]);
            Poner(seRompe, "warningSeconds", 0f);
            Poner(seRompe, "shake", 0f);
            Poner(seRompe, "downSeconds", 1000f);
            Poner(seRompe, "sinkDepth", 12f);
            Poner(seRompe, "moveSeconds", 0.35f);
            sitios[k] = new Vector3(k % 2 == 0 ? -1.6f : 1.6f, 0f, zc);
        }
        piezas.TryGetValue("semicorchea", out var semicorchea);
        Caida($"Semicorcheas: {nombre} (rompen el suelo)", padre, reloj, semicorchea, VozSemicorchea, notas[0] - 0.01f, notas[n - 1] + 0.01f,
              Puntos(padre, "Semicorchea", sitios), 0.6f, 0f, 0.5f, AssetDatabase.LoadAssetAtPath<AudioClip>(RutaPum), 0.35f, 0.45f);
    }

    /// <summary>La ola de lágrimas: va con la sombra desde la lágrima hasta el final (se quita al reintentar).</summary>
    private static void CrearOla(Transform raiz, Transform sombra, TempoClock reloj, Montaje montaje)
    {
        var ola = Caja("Ola de lágrimas", sombra, sombra.position + new Vector3(0f, 2f, -3f), new Vector3(AnchoDelCorredor + 4f, 4f, 7f), _lagrima, false);
        Object.DestroyImmediate(ola.GetComponent<Collider>());
        ola.SetActive(false);
        var llega = Hijo(raiz, "Llega la ola (lágrima)").gameObject.AddComponent<ScoreCueTrigger>();
        PonerRef(llega, "clock", reloj);
        PonerTextos(llega, "voices", new[] { VozLagrima });
        UnityEventTools.AddBoolPersistentListener(llega.onCue, ola.SetActive, true);
        montaje.SeVan.Add(ola);
    }

    /// <summary>La bola del Demonio (la que para y devuelve la defensa), para el regalito que se devuelve.</summary>
    private static GameObject BolaDevolvible()
    {
        var demonio = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/Demon.prefab");
        var ia = demonio != null ? demonio.GetComponentInChildren<ImpDemonAI>(true) : null;
        return ia != null ? new SerializedObject(ia).FindProperty("projectilePrefab")?.objectReferenceValue as GameObject : null;
    }

    /// <summary>Columnas que salen del suelo en los golpes de una voz (con su aviso en el suelo).</summary>
    private static CueDrop Columnas(string nombre, Transform padre, TempoClock reloj, string voz, Transform[] puntos, Vector3 tam,
                                    float dano, float radio, AudioClip sonido, float volumen, float aviso, float arriba, float desde,
                                    float hasta = SegundoDeLaPuerta, Material material = null, bool deLaPared = false,
                                    string atrezo = null, bool tumbado = false)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        var plantilla = new GameObject("Columna (plantilla)");
        plantilla.transform.SetParent(go.transform, false);
        // Con atrezo, el cuerpo es un decorado de cartón pintado, plano y de cara al público (o tumbado en
        // el suelo, como el rayo); sin él, una caja.
        var cuerpo = atrezo != null && _piezas.TryGetValue(atrezo, out var recorte) && recorte.sprite != null
            ? Atrezo(plantilla.transform, recorte.sprite, tam, tumbado)
            : Caja("Cuerpo", plantilla.transform, new Vector3(0f, tam.y * 0.5f, 0f), tam, material != null ? material : _grisOscuro, false);
        if (cuerpo.GetComponent<SpriteRenderer>() == null) cuerpo.transform.localPosition = new Vector3(0f, tam.y * 0.5f, 0f);
        // Sólida para el grupo, pero la cámara no se acerca por ella.
        cuerpo.layer = LayerMask.NameToLayer("Ignore Raycast");
        var marca = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marca.name = "Aviso (plantilla)";
        marca.transform.SetParent(go.transform, false);
        marca.transform.localScale = new Vector3(tam.x * 1.4f, 0.01f, tam.z * 1.4f);
        Object.DestroyImmediate(marca.GetComponent<Collider>());
        marca.GetComponent<MeshRenderer>().sharedMaterial = _brasa;

        var caida = go.AddComponent<CueDrop>();
        PonerRef(caida, "clock", reloj);
        PonerTextos(caida, "voices", new[] { voz });
        Poner(caida, "fromSeconds", desde);
        Poner(caida, "toSeconds", hasta);
        PonerRefs(caida, "landingPoints", puntos);
        PonerRef(caida, "fallingTemplate", plantilla);
        PonerRef(caida, "shadowTemplate", marca.transform);
        Poner(caida, "fromBelow", true);
        Poner(caida, "fromWall", deLaPared);
        Poner(caida, "fallSeconds", aviso);
        Poner(caida, "dropHeight", deLaPared ? tam.x + 1f : tam.y + 0.2f);
        Poner(caida, "lingerSeconds", arriba);
        Poner(caida, "radius", radio);
        Poner(caida, "damage", dano);
        PonerRef(caida, "landingSound", sonido);
        Poner(caida, "soundVolume", volumen);
        return caida;
    }

    /// <summary>
    /// Un decorado de cartón pintado del tamaño pedido: de pie (de cara al público, con su caja de choque
    /// del grueso pedido) o tumbado en el suelo a lo largo (sin choque).
    /// </summary>
    private static GameObject Atrezo(Transform padre, Sprite sprite, Vector3 tam, bool tumbado)
    {
        var go = new GameObject("Cuerpo (atrezo)");
        go.transform.SetParent(padre, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        Vector3 medida = sprite.bounds.size;
        if (tumbado)
        {
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            go.transform.localScale = new Vector3(tam.x / medida.x, tam.z / medida.y, 1f);
            return go;
        }
        go.transform.localScale = new Vector3(tam.x / medida.x, tam.y / medida.y, 1f);
        var choque = go.AddComponent<BoxCollider>();
        choque.center = sprite.bounds.center;
        choque.size = new Vector3(medida.x, medida.y, tam.z);
        return go;
    }

    /// <summary>La sombra que viene detrás a la velocidad de la canción: si te alcanza, se repite.</summary>
    private static Transform CrearSombra(Transform raiz, Scene escena)
    {
        // La sombra va en el borde de atrás del recuadro: la mancha llega justo hasta ese borde y el
        // muro va unos metros detrás. Quien pisa la mancha, repite.
        var sombra = Hijo(raiz, "La sombra que persigue");
        sombra.position = new Vector3(0f, 0f, -40f);
        const float largo = 4f;
        var muro = Caja("Muro de sombra", sombra, new Vector3(0f, AltoDeLosMuros * 0.5f, -40f - largo),
                        new Vector3(AnchoDelCorredor + 6f, AltoDeLosMuros + 2f, 1.2f), _sombraQuePersigue, false);
        Object.DestroyImmediate(muro.GetComponent<Collider>());
        var mancha = Caja("Mancha que se arrastra", sombra, new Vector3(0f, 0.025f, -40f - largo * 0.5f),
                          new Vector3(AnchoDelCorredor - 0.2f, 0.02f, largo), _sombraQuePersigue, false);
        Object.DestroyImmediate(mancha.GetComponent<Collider>());
        return sombra;
    }

    /// <summary>
    /// El recuadro que va con la canción por el suelo: dentro se corre libre; detrás espera la sombra
    /// y en el borde de delante no se corre más que la canción.
    /// </summary>
    // Enseñar el recuadro amarillo (solo para pruebas).
    private const bool RecuadroALaVista = false;

    private static Transform CrearRecuadro(Transform raiz, float atras, float adelante)
    {
        var recuadro = Hijo(raiz, "Recuadro (va con la canción)");
        recuadro.position = new Vector3(0f, 0f, -40f);
        // Las líneas solo para probar: en el nivel el recuadro no se ve (lo marcan los peligros y la luz).
        if (!RecuadroALaVista) return recuadro;
        float largo = atras + adelante, medio = (adelante - atras) * 0.5f, x = AnchoDelCorredor * 0.5f - 0.4f;
        foreach (var (nombre, centro, tam) in new[]
        {
            ("Lado izquierdo", new Vector3(-x, 0.03f, medio), new Vector3(0.18f, 0.02f, largo)),
            ("Lado derecho", new Vector3(x, 0.03f, medio), new Vector3(0.18f, 0.02f, largo)),
            ("Borde de delante", new Vector3(0f, 0.03f, adelante), new Vector3(2f * x, 0.02f, 0.18f)),
            ("Borde de atrás", new Vector3(0f, 0.03f, -atras), new Vector3(2f * x, 0.02f, 0.18f)),
        })
        {
            var linea = Caja(nombre, recuadro, recuadro.position + centro, tam, _recuadro, false);
            Object.DestroyImmediate(linea.GetComponent<Collider>());
        }
        return recuadro;
    }

    /// <summary>
    /// La cámara del tramo: en el eje del corredor (su «adelante» es el del corredor). En la
    /// presentación, plano alto y fijo de la sala.
    /// </summary>
    private static CamaraDelCoro CrearCamara(Transform raiz, ScoreCueTrigger doReMi)
    {
        var go = Hijo(raiz, "Cámara del Coro");
        go.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        // Plano alto desde el fondo de la sala: el Tenor, el grupo y lo que cae, todo a la vista.
        var plano = Hijo(go, "Plano de la presentación");
        plano.position = new Vector3(0f, 12f, -31f);
        plano.rotation = Quaternion.LookRotation(new Vector3(0f, 4.5f, -9f) - plano.position, Vector3.up);
        // Do, re, mi: la cara del Tenor de cerca, el grupo de lado y detrás del grupo (donde empieza la huida).
        Transform Corte(string nombre, Vector3 desde, Vector3 mira)
        {
            var t = Hijo(go, nombre);
            t.SetPositionAndRotation(desde, Quaternion.LookRotation(mira - desde, Vector3.up));
            return t;
        }
        var cortes = new Object[]
        {
            Corte("Corte: do (el Tenor)", new Vector3(0f, 5.5f, -13f), new Vector3(0f, 6f, -4.4f)),
            Corte("Corte: re (el grupo de lado)", new Vector3(-6.5f, 2.2f, -19f), new Vector3(0f, 1.2f, -20f)),
            Corte("Corte: mi (detrás del grupo)", new Vector3(0f, 4f, -27f), new Vector3(0f, 2.5f, -12f)),
        };
        var camara = go.gameObject.AddComponent<CamaraDelCoro>();
        PonerRef(camara, "plano", plano);
        PonerRef(camara, "cortes", doReMi);
        PonerRefs(camara, "planosDeCorte", cortes);
        return camara;
    }

    private static void CrearCoda(Transform padre, Montaje montaje, float inicioDelRellano, float bordeDelSalto, float inicioDeLaMeta)
    {
        var coda = Hijo(padre, "Coda (salida y aterrizaje)");
        float zDelControl = Mathf.Max(inicioDelRellano + 0.75f, bordeDelSalto - 6f);
        var control = Hijo(coda, "Punto de control 7 (último salto)");
        control.position = new Vector3(0f, 0.2f, zDelControl);
        montaje.ControlDeLaCoda = control;

        var rayaDeSalida = Caja("Borde del salto", coda, new Vector3(0f, 0.025f, bordeDelSalto - 0.55f),
            new Vector3(AnchoDelFinal, 0.035f, 0.22f), _recuadro, false);
        Object.DestroyImmediate(rayaDeSalida.GetComponent<Collider>());

        var meta = Hijo(coda, "Aterrizaje del salto final");
        meta.position = new Vector3(0f, 0f, inicioDeLaMeta + 0.15f);
        montaje.Meta = meta;
        var rayaDeMeta = Caja("Borde del aterrizaje", coda, meta.position + Vector3.up * 0.025f,
            new Vector3(AnchoDelFinal, 0.035f, 0.22f), _recuadro, false);
        Object.DestroyImmediate(rayaDeMeta.GetComponent<Collider>());
    }

    private static void CrearTramo(Transform raiz, TempoClock reloj, Transform sombra, Vector3 entrada,
                                   CamaraDelCoro camara, Transform recuadro, Montaje montaje, Transform[] sueloQueSePierde, FollowSpot foco)
    {
        var go = Hijo(raiz, "La huida (afinación, estrofas 1 y 2, estribillo)");
        var salida = Hijo(go, "Salida (la sala)");
        salida.position = entrada + Vector3.up * 0.05f;
        // Puntos de control: 10 m antes del corredor y al otro lado del puente. Al reintentar, la
        // canción vuelve un compás antes de que toque salir de ellos.
        var control1 = Hijo(go, "Punto de control 1 (entrada del corredor)");
        control1.position = new Vector3(0f, 0.2f, Z(19.85f));
        var control2 = Hijo(go, "Punto de control 2 (nave del órgano)");
        control2.position = new Vector3(0f, 0.2f, ZDelRellano + 3f);
        // Ante la puerta de madera: al reintentar ahí, se vuelve a quemar y llegan los regalitos.
        var control3 = Hijo(go, "Punto de control 3 (puerta de madera)");
        control3.position = new Vector3(0f, 0.2f, ZDeLaPuerta - 5f);
        // Entrando en el escenario: al reintentar ahí, se vuelve al principio del estribillo.
        var control4 = Hijo(go, "Punto de control 4 (el escenario)");
        control4.position = new Vector3(0f, 0.2f, ZDelFinDeLaNave + 3f);
        // En el fondo del escenario, antes del puente: al reintentar, vuelven a salir los coristas.
        var control5 = Hijo(go, "Punto de control 5 (el encierro)");
        control5.position = new Vector3(0f, 0.2f, ZDeLaSala - 6f);
        // En el primer practicable del final (solo se llega con el telón del fondo abierto).
        var control6 = Hijo(go, "Punto de control 6 (el final)");
        control6.position = Practicables.Count > 0 ? new Vector3(Practicables[0].x, 0.2f, Practicables[0].z) : new Vector3(0f, 0.2f, ZDelFondoDelEscenario + 2f);

        // Grabador de marcas (apagado): se marca para poner a mano, tocando teclas al ritmo, los pilares
        // y los golpes de la presentación.
        var grabador = go.gameObject.AddComponent<GrabadorDeMarcas>();
        PonerRef(grabador, "reloj", reloj);
        grabador.enabled = false;
        var tramo = go.gameObject.AddComponent<TramoDelCoro>();
        PonerRef(tramo, "salidaDelJugador", salida);
        Poner(tramo, "empezarAlActivarse", true);
        Poner(tramo, "segundosDeCuentaAtras", 0);
        Poner(tramo, "claveDeSalida", "");
        Poner(tramo, "textoDeSalida", "");
        Poner(tramo, "fundido", 0.3f);
        Poner(tramo, "pausaAntesDelReintento", 0.4f);
        Poner(tramo, "reintentarAlFallar", true);
        PonerRef(tramo, "reloj", reloj);
        PonerRef(tramo, "camara", camara);
        PonerRef(tramo, "marcaDelSaludo", montaje.MarcaDelSaludo);
        PonerRef(tramo, "focoDelSaludo", montaje.FocoDelSaludo);
        PonerRef(tramo, "confetiDelSaludo", AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Confetti_blast_multicolor.prefab"));
        Poner(tramo, "segundoDeSalida", 0f);
        PonerRefs(tramo, "puntosDeControl", new Object[] { control1, control2, control3, control4, control5, control6, montaje.ControlDeLaCoda });
        Poner(tramo, "tiemposAntesDelReintento", 4);
        PonerRefs(tramo, "objetosQueVuelven", montaje.Vuelven.ToArray());
        PonerRefs(tramo, "objetosQueSeVan", montaje.SeVan.ToArray());
        PonerRef(tramo, "sombra", sombra);
        Poner(tramo, "metrosPorSegundo", Velocidad);
        Poner(tramo, "segundoDeArranque", SegundoDelCorredor);
        Poner(tramo, "zDeArranque", 0f);
        Poner(tramo, "metrosDeVentaja", 12f);
        PonerRefs(tramo, "sueloQueSePierde", sueloQueSePierde);
        PonerRef(tramo, "polvoDelSuelo", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VFX/FootDust_Toon.prefab"));
        // Los huecos de magia necesitan margen para aterrizar durante el calderón.
        Poner(tramo, "metrosAdelante", AdelantoEnCarrera);
        Poner(tramo, "metrosAdelanteEnRespiro", 12f);
        Poner(tramo, "topeLibre", 9f);
        // En el hueco y ante la puerta del órgano, el recuadro y la sombra se paran 3 s: un respiro
        // para cambiar a quien lleva el fuego y disparar. La canción sigue.
        PonerVector2s(tramo, "calderones", Calderones.ToArray());
        PonerVector3s(tramo, "acelerones", Acelerones.ToArray());
        // La frase triste: el grupo quieto mientras la cámara mira al Tenor cantando a su público.
        PonerVector2s(tramo, "tramosSinControl", new[] { new Vector2(SegundoDeLaFraseTriste, Truenos) });
        // Soltar el paso en el tiempo anterior al salto deja ver cómo se rompe el suelo.
        float saludo = Mathf.Max(SegundoDeLaMeta, DashFinal + 0.9f);
        Poner(tramo, "segundoSinTope", TiempoCercano(_tiempos, SaltoFinal, -1));
        Poner(tramo, "segundoDelSaludo", saludo);
        Poner(tramo, "segundoLimiteDeLaCoda", Mathf.Min(saludo + 0.4f, reloj.Score.Clip.length - 0.1f));
        PonerRef(tramo, "controlDeLaCoda", montaje.ControlDeLaCoda);
        PonerRef(tramo, "tenorDeLosRegalos", montaje.TenorDeLosRegalos);
        PonerRefs(tramo, "lanzadoresDeRegalos", montaje.Regalos.ToArray());
        PonerRef(tramo, "meta", montaje.Meta);
        Poner(tramo, "semianchoDeLaMeta", AnchoDelFinal * 0.5f);
        PonerRef(tramo, "recuadro", recuadro);
        Poner(tramo, "alturaDelFoso", -5f);
        PonerRef(tramo, "recintoDelCoro", montaje.RecintoDelCoro);
        PonerRef(tramo, "focoDeRuta", foco);
        Poner(tramo, "segundoDeEstrecharElFoco", DesdeElEstribillo);
        PonerVector2s(tramo, "tramosDelFoco", VentanasDelFoco());
        var ajustesDelTramo = new SerializedObject(tramo);
        ajustesDelTramo.FindProperty("recorridoLateralDelFoco").animationCurveValue = CurvaDelFoco();
        ajustesDelTramo.FindProperty("limitesDelCoro").vector2Value = new Vector2(AnchoDelCorredor * 0.5f, AnchoDeFilaDelCoro * 1.5f);
        ajustesDelTramo.FindProperty("tramoDelCoro").vector2Value = new Vector2(SegundoDelPuente, SegundoDeLaFraseTriste);
        ajustesDelTramo.ApplyModifiedPropertiesWithoutUndo();
        PonerRefs(tramo, "plataformas", montaje.Plataformas.ToArray());
        PonerRefs(tramo, "receptores", montaje.Receptores.ToArray());
    }

    private static GameObject SinVuelo(Transform raiz)
    {
        var go = Hijo(raiz, "Sin vuelo en el templo");
        go.position = new Vector3(0f, 6f, (ZDelFinal - 40f) * 0.5f);
        var zona = go.gameObject.AddComponent<BoxCollider>();
        zona.isTrigger = true;
        zona.size = new Vector3(40f, 40f, ZDelFinal + 50f);
        go.gameObject.AddComponent<ZonaQueBloqueaHabilidades>();
        return go.gameObject;
    }

    // ── Piezas sueltas ──────────────────────────────────────────────────────────────────────

    private static Transform Hijo(Transform padre, string nombre)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        return go.transform;
    }

    private static GameObject Caja(string nombre, Transform padre, Vector3 centro, Vector3 tam, Material material, bool suelo)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(centro, Quaternion.identity);
        go.transform.localScale = tam;
        go.layer = LayerMask.NameToLayer(suelo ? EscenaJugable.CapaDelSuelo : "Default");
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null && material != null) mr.sharedMaterial = material;
        return go;
    }

    private static void Raya(string nombre, Transform padre, Vector3 centro, Vector3 tam)
    {
        var go = Caja(nombre, padre, centro, tam, _linea, false);
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    /// <summary>
    /// La sala a oscuras, como en una función: sin cielo, una luz de ambiente baja y fría y algo de bruma.
    /// La escena la alumbran las varas de focos que van con la canción y el cañón de seguimiento
    /// (<see cref="CrearLuces"/>); los recortes de papel no reciben luz y destacan sobre lo oscuro.
    /// </summary>
    private static void Ambiente(Scene escena)
    {
        var luz = new GameObject("Luz de sala (casi apagada)");
        SceneManager.MoveGameObjectToScene(luz, escena);
        var direccional = luz.AddComponent<Light>();
        direccional.type = LightType.Directional;
        direccional.intensity = 0.25f;
        direccional.color = new Color(0.6f, 0.65f, 1f);
        luz.transform.rotation = Quaternion.Euler(60f, 20f, 0f);
        RenderSettings.skybox = CieloDeTeatro();
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.14f, 0.12f, 0.19f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.06f, 0.04f, 0.09f);
        RenderSettings.fogDensity = 0.012f;
    }

    /// <summary>El cielo: casi negro con un poco de ciruela, como el telar oscuro de un teatro.</summary>
    private static Material CieloDeTeatro()
    {
        string ruta = $"{CarpetaDatos}/CieloDeTeatro.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return null;
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, ruta);
        }
        material.SetFloat("_SunDisk", 0f);
        material.SetFloat("_SunSize", 0f);
        material.SetFloat("_AtmosphereThickness", 0.3f);
        material.SetColor("_SkyTint", new Color(0.16f, 0.08f, 0.2f));
        material.SetColor("_GroundColor", new Color(0.04f, 0.02f, 0.06f));
        material.SetFloat("_Exposure", 0.35f);
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>
    /// La luz de la función: una vara de focos que va con el recuadro (cuatro cenitales de colores que
    /// laten en cada tiempo y cambian de paleta por tramos de la canción) y el cañón de seguimiento sobre
    /// quien va al mando. En la frase triste se apaga todo menos el cañón.
    /// </summary>
    private static FollowSpot CrearLuces(Transform raiz, TempoClock reloj, Transform recuadro)
    {
        var vara = Hijo(recuadro, "Vara de focos (va con la canción)");
        var focos = new List<Light>();
        foreach (var sitio in new[] { new Vector3(-4f, 11f, 1f), new Vector3(0f, 12f, 3f), new Vector3(4f, 11f, 1f), new Vector3(0f, 11f, 13f) })
        {
            var go = Hijo(vara, $"Cenital ({sitio.x:0}, {sitio.z:0})");
            go.localPosition = sitio;
            go.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var foco = go.gameObject.AddComponent<Light>();
            foco.type = LightType.Spot;
            foco.range = 24f;
            foco.spotAngle = 75f;
            foco.innerSpotAngle = 40f;
            foco.shadows = LightShadows.None;
            focos.Add(foco);
        }
        var rig = vara.gameObject.AddComponent<BeatLightRig>();
        PonerRef(rig, "clock", reloj);
        PonerRefs(rig, "lights", focos.ToArray());
        Poner(rig, "baseIntensity", 5.5f);
        var tramos = new SerializedObject(rig);
        var lista = tramos.FindProperty("sections");
        var paletas = new (float desde, Color[] colores, float intensidad, bool cadaTiempo)[]
        {
            (0f, new[] { new Color(1f, 0.78f, 0.5f) }, 0.8f, false),
            (SegundoDelCorredor, new[] { new Color(0.45f, 0.55f, 1f), new Color(0.72f, 0.48f, 1f) }, 1f, false),
            (SegundoDeOsPresento, new[] { new Color(1f, 0.5f, 0.22f), new Color(1f, 0.28f, 0.28f) }, 1f, false),
            (DesdeElEstribillo, new[] { new Color(1f, 0.82f, 0.45f), new Color(1f, 0.38f, 0.72f), new Color(0.45f, 0.85f, 1f) }, 1.1f, false),
            (SegundoDelPuente, new[] { new Color(0.95f, 0.18f, 0.22f), new Color(0.6f, 0.12f, 0.45f) }, 0.9f, false),
            (DesdeLaTralla, new[] { new Color(1f, 1f, 1f), new Color(0.95f, 0.18f, 0.22f) }, 1.2f, true),
            (SegundoDeLaFraseTriste, new[] { new Color(0.5f, 0.6f, 1f) }, 0f, false),
            (Truenos, new[] { new Color(1f, 0.35f, 0.35f), new Color(1f, 0.8f, 0.35f), new Color(0.45f, 1f, 0.55f), new Color(0.45f, 0.7f, 1f), new Color(0.82f, 0.45f, 1f) }, 1.2f, true),
        };
        lista.arraySize = paletas.Length;
        for (int i = 0; i < paletas.Length; i++)
        {
            var e = lista.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("fromSeconds").floatValue = paletas[i].desde;
            e.FindPropertyRelative("intensity").floatValue = paletas[i].intensidad;
            e.FindPropertyRelative("changeEveryBeat").boolValue = paletas[i].cadaTiempo;
            var colores = e.FindPropertyRelative("colors");
            colores.arraySize = paletas[i].colores.Length;
            for (int c = 0; c < paletas[i].colores.Length; c++) colores.GetArrayElementAtIndex(c).colorValue = paletas[i].colores[c];
        }
        tramos.ApplyModifiedPropertiesWithoutUndo();

        // El cañón de seguimiento: luz cálida y cerrada sobre quien va al mando.
        var canon = Hijo(raiz, "Cañón de seguimiento");
        var luz = canon.gameObject.AddComponent<Light>();
        luz.type = LightType.Spot;
        luz.color = new Color(1f, 0.92f, 0.78f);
        luz.intensity = 14f;
        luz.range = 30f;
        luz.spotAngle = 22f;
        luz.innerSpotAngle = 12f;
        luz.shadows = LightShadows.None;
        return canon.gameObject.AddComponent<FollowSpot>();
    }

    private static Vector2[] VentanasDelFoco()
    {
        var pausas = new List<Vector2>(Respiros)
        {
            new Vector2(SegundoDelPuente, Truenos + SalidaDelFinal),
            new Vector2(RespiroDeWill.x, SegundoAlPasarElHueco(RespiroDeWill)),
            new Vector2(RespiroDeLiam.x, SegundoAlPasarElHueco(RespiroDeLiam))
        };
        pausas.Sort((a, b) => a.x.CompareTo(b.x));
        var ventanas = new List<Vector2>();
        float desde = SegundoDelCorredor;
        foreach (var pausa in pausas)
        {
            float hasta = pausa.x - 0.25f;
            if (hasta > desde) ventanas.Add(new Vector2(desde, hasta));
            desde = Mathf.Max(desde, pausa.y + 0.2f);
        }
        if (desde < SaltoFinal - 0.4f) ventanas.Add(new Vector2(desde, SaltoFinal - 0.4f));
        return ventanas.ToArray();
    }

    private static AnimationCurve CurvaDelFoco()
    {
        var claves = new List<Keyframe>();
        for (int i = 0; i < _camino.Length; i++)
        {
            float x = _camino[i];
            float entrada = i > 0 ? (x - _camino[i - 1]) / PasoDelCamino : 0f;
            float salida = i + 1 < _camino.Length ? (_camino[i + 1] - x) / PasoDelCamino : 0f;
            if (i > 0 && i + 1 < _camino.Length && Mathf.Abs(entrada - salida) < 0.001f) continue;
            claves.Add(new Keyframe(i * PasoDelCamino, x, entrada, salida));
        }
        return new AnimationCurve(claves.ToArray());
    }

    private static void CrearMateriales()
    {
        // Gris lila de la roca del juego y el papel de la interfaz para las rayas.
        // El decorado de cartón: tablas de escenario en el suelo (corren hacia el fondo), muros de cartón
        // pintado, terciopelo en los telones (pliegues en vertical) y madera oscura.
        _gris = MaterialDeDecorado("Suelo", "tablas_escenario", 4f, 4f, new Color(0.66f, 0.62f, 0.69f));
        _grisOscuro = MaterialDeDecorado("Muro", "muro_de_carton", 4f, 4f, new Color(0.36f, 0.32f, 0.40f));
        _linea = MaterialLiso("Pentagrama", new Color(0.16f, 0.13f, 0.19f));
        _brasa = MaterialLiso("Brasa", new Color(0.95f, 0.48f, 0.18f));
        _brasa.EnableKeyword("_EMISSION");
        _brasa.SetColor("_EmissionColor", new Color(0.95f, 0.48f, 0.18f) * 0.8f);
        EditorUtility.SetDirty(_brasa);
        _madera = MaterialDeDecorado("Madera", "madera_oscura", 2f, 2f, new Color(0.55f, 0.36f, 0.22f));
        _onda = MaterialTransparente("Onda", new Color(1f, 0.86f, 0.42f, 0.35f));
        _regalo = MaterialLiso("Regalo", new Color(0.92f, 0.3f, 0.5f));
        _telon = MaterialDeDecorado("Telon", "terciopelo", 4f, AltoDeLosMuros, new Color(0.55f, 0.08f, 0.12f));
        _luz = MaterialQueBrilla("Luz", new Color(1f, 0.93f, 0.62f), 0.9f);
        _runas = MaterialQueBrilla("Runas", new Color(0.66f, 0.55f, 0.9f), 0.6f);
        _rayo = MaterialQueBrilla("Rayo", new Color(0.88f, 0.82f, 1f), 1.4f);
        _lagrima = MaterialTransparente("Lagrima", new Color(0.55f, 0.8f, 1f, 0.55f));
        _rosa = MaterialLiso("Rosa", new Color(0.85f, 0.15f, 0.25f));
        _recuadro = MaterialLiso("Recuadro", new Color(1f, 0.86f, 0.42f));
        _recuadro.EnableKeyword("_EMISSION");
        _recuadro.SetColor("_EmissionColor", new Color(1f, 0.86f, 0.42f) * 0.6f);
        EditorUtility.SetDirty(_recuadro);
        _sombra = MaterialTransparente("SombraDeNota", new Color(0.05f, 0.04f, 0.07f, 0.55f));
        // Donde va a caer algo que hace daño: un color vivo, el mismo para todo, que crece hasta el golpe.
        _aviso = MaterialTransparente("AvisoDeLoQueCae", new Color(1f, 0.3f, 0.2f, 0.6f));
        // Brilla: se ve igual en lo oscuro de la sala.
        _aviso.EnableKeyword("_EMISSION");
        _aviso.SetColor("_EmissionColor", new Color(1f, 0.3f, 0.2f) * 0.9f);
        EditorUtility.SetDirty(_aviso);
        _humo = MaterialTransparente("HumoDeHieloSeco", new Color(0.92f, 0.92f, 0.98f, 0.75f));
        _sombraQuePersigue = MaterialTransparente("SombraQuePersigue", new Color(0.08f, 0.04f, 0.10f, 0.75f));
    }

    private static Material MaterialLiso(string nombre, Color color)
    {
        string ruta = $"{CarpetaDatos}/{nombre}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader != null ? shader : Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, ruta);
        }
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>
    /// Material del decorado con textura de cartón: shader de dibujo (el oficial del aspecto) con la textura
    /// en coordenadas del mundo, para que cada caja estirada la repita según su tamaño (una repetición cada
    /// <paramref name="metrosX"/> × <paramref name="metrosY"/> m). Sin la textura o el shader, color liso.
    /// </summary>
    private static Material MaterialDeDecorado(string nombre, string textura, float metrosX, float metrosY, Color siNoHay)
    {
        var material = MaterialLiso(nombre, siNoHay);
        var imagen = AssetDatabase.LoadAssetAtPath<Texture2D>($"{CarpetaTexturas}/{textura}.png");
        var dibujo = Shader.Find("El Sendero/Dibujo");
        if (imagen == null || dibujo == null)
        {
            Debug.LogWarning($"[Templo del Coro] {nombre}: sin {(imagen == null ? "la textura " + textura : "el shader El Sendero/Dibujo")}, se queda en color liso.");
            return material;
        }
        material.shader = dibujo;
        material.SetTexture("_BaseMap", imagen);
        material.SetTextureScale("_BaseMap", new Vector2(1f / metrosX, 1f / metrosY));
        material.SetTextureOffset("_BaseMap", Vector2.zero);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_UvDelMundo", 1f);
        material.EnableKeyword("_UV_DEL_MUNDO");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material MaterialQueBrilla(string nombre, Color color, float brillo)
    {
        var material = MaterialLiso(nombre, color);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * brillo);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material MaterialTransparente(string nombre, Color color)
    {
        var material = MaterialLiso(nombre, color);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CrearCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(padre) && !AssetDatabase.IsValidFolder(padre)) CrearCarpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }

    // ── Campos serializados ─────────────────────────────────────────────────────────────────

    private static SerializedProperty Campo(SerializedObject so, Object componente, string campo)
    {
        var prop = so.FindProperty(campo);
        if (prop == null) Debug.LogError($"[Templo del Coro] {componente.GetType().Name} no tiene el campo «{campo}».");
        return prop;
    }

    private static void Poner(Object c, string campo, bool v)
    { var so = new SerializedObject(c); var p = Campo(so, c, campo); if (p == null) return; p.boolValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }

    private static void Poner(Object c, string campo, float v)
    { var so = new SerializedObject(c); var p = Campo(so, c, campo); if (p == null) return; p.floatValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }

    private static void Poner(Object c, string campo, int v)
    {
        var so = new SerializedObject(c); var p = Campo(so, c, campo); if (p == null) return;
        p.intValue = v; // en los enum, el valor (no el índice)
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Poner(Object c, string campo, string v)
    { var so = new SerializedObject(c); var p = Campo(so, c, campo); if (p == null) return; p.stringValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }

    private static void PonerVector2s(Object c, string campo, Vector2[] v)
    {
        var so = new SerializedObject(c); var p = Campo(so, c, campo); if (p == null) return;
        p.arraySize = v.Length;
        for (int i = 0; i < v.Length; i++) p.GetArrayElementAtIndex(i).vector2Value = v[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PonerFloats(Object c, string campo, float[] v)
    {
        var so = new SerializedObject(c); var p = Campo(so, c, campo); if (p == null) return;
        p.arraySize = v.Length;
        for (int i = 0; i < v.Length; i++) p.GetArrayElementAtIndex(i).floatValue = v[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PonerVector3s(Object c, string campo, Vector3[] v)
    {
        var so = new SerializedObject(c); var p = Campo(so, c, campo); if (p == null) return;
        p.arraySize = v.Length;
        for (int i = 0; i < v.Length; i++) p.GetArrayElementAtIndex(i).vector3Value = v[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PonerPlanosDeEscena(CamaraDelCoro camara, Montaje montaje)
    {
        var so = new SerializedObject(camara); var p = Campo(so, camara, "planosDeEscena"); if (p == null) return;
        p.arraySize = montaje.Planos.Count;
        for (int i = 0; i < montaje.Planos.Count; i++)
        {
            var e = p.GetArrayElementAtIndex(i);
            var (segundos, desde, hasta, campo) = montaje.Planos[i];
            e.FindPropertyRelative("segundos").vector2Value = segundos;
            e.FindPropertyRelative("desde").objectReferenceValue = desde;
            e.FindPropertyRelative("hasta").objectReferenceValue = hasta;
            e.FindPropertyRelative("campo").floatValue = campo;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PonerVector2(Object c, string campo, Vector2 v)
    { var so = new SerializedObject(c); var p = Campo(so, c, campo); if (p == null) return; p.vector2Value = v; so.ApplyModifiedPropertiesWithoutUndo(); }

    private static void PonerVector(Object c, string campo, Vector3 v)
    { var so = new SerializedObject(c); var p = Campo(so, c, campo); if (p == null) return; p.vector3Value = v; so.ApplyModifiedPropertiesWithoutUndo(); }

    private static void PonerRef(Object c, string campo, Object v)
    { var so = new SerializedObject(c); var p = Campo(so, c, campo); if (p == null) return; p.objectReferenceValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }

    private static void PonerRefs(Object c, string campo, Object[] v)
    {
        var so = new SerializedObject(c); var p = Campo(so, c, campo); if (p == null) return;
        p.arraySize = v.Length;
        for (int i = 0; i < v.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = v[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PonerTextos(Object c, string campo, string[] v)
    {
        var so = new SerializedObject(c); var p = Campo(so, c, campo); if (p == null) return;
        p.arraySize = v.Length;
        for (int i = 0; i < v.Length; i++) p.GetArrayElementAtIndex(i).stringValue = v[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PonerEnumArray(Object c, string campo, int[] v)
    {
        var so = new SerializedObject(c); var p = Campo(so, c, campo); if (p == null) return;
        p.arraySize = v.Length;
        for (int i = 0; i < v.Length; i++) p.GetArrayElementAtIndex(i).intValue = v[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
