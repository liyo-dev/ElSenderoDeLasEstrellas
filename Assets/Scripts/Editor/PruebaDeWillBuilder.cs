using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Fin = KitDeLaberintoDeEspejos.FinDeCallejon;

/// <summary>
/// Monta la escena de la Prueba de Will (Laberinto.unity) con el kit de laberintos de
/// espejos, a partir del plano de abajo: salas hexagonales encadenadas por pasillos rectos, con
/// callejones, espejos vivos en los sitios clave, puntos con nombre para lo que se monte encima
/// (guardados, recuerdos, muros que caen…) y rótulos de maqueta. Tanda 1: el recorrido de los diez
/// tramos a escala; encima, los recuerdos que ya están hechos (<see cref="PruebaDeWillRecuerdos"/>).
/// La escena se rehace entera cada vez. Ver INC-870, INC-873 e INC-881.
/// </summary>
public static class PruebaDeWillBuilder
{
    public const string RutaEscena = "Assets/Scenes/Worlds/Laberinto.unity";
    private const string CarpetaMateriales = "Assets/Art/Materials/Sendero/PruebaWill";
    private const float AnchoNormal = 4.5f;

    /// <summary>Un callejón sin salida que sale de un lado de una sala.</summary>
    private sealed class Callejon
    {
        public readonly int Lado; public readonly float Largo; public readonly Fin Fin; public readonly string Nombre;
        public Callejon(int lado, float largo, Fin fin, string nombre) { Lado = lado; Largo = largo; Fin = fin; Nombre = nombre; }
    }

    /// <summary>
    /// Una sala del plano. Se coloca respecto a su sala padre: sale por el lado <see cref="LadoDelPadre"/>
    /// del padre, recorre un pasillo de <see cref="Largo"/> m y entra por el lado opuesto.
    /// </summary>
    private sealed class Sala
    {
        public readonly string Id, Nombre, Padre;
        public readonly int LadoDelPadre;
        public readonly float Largo, Ancho, Apotema;
        public readonly int[] Vivos;
        public readonly Callejon[] Callejones;
        public Vector3 Centro;
        public readonly Dictionary<int, float> Aberturas = new Dictionary<int, float>();

        public Sala(string id, string nombre, string padre, int ladoDelPadre, float largo, float ancho, float apotema,
                    int[] vivos = null, Callejon[] callejones = null)
        {
            Id = id; Nombre = nombre; Padre = padre; LadoDelPadre = ladoDelPadre; Largo = largo; Ancho = ancho;
            Apotema = apotema; Vivos = vivos ?? new int[0]; Callejones = callejones ?? new Callejon[0];
        }

        /// <summary>Lado por el que se entra (el opuesto al del padre). -1 en la primera sala.</summary>
        public int LadoDeEntrada => Padre == null ? -1 : (LadoDelPadre + 3) % 6;
    }

    // ── El plano ────────────────────────────────────────────────────────────────
    // Lados: 0 = noreste, 1 = norte, 2 = noroeste, 3 = suroeste, 4 = sur, 5 = sureste.
    // Para mover o agrandar una sala basta con cambiar sus números y volver a ejecutar el menú.
    private static List<Sala> Plano() => new List<Sala>
    {
        new Sala("R1", "1 · Entrada", null, -1, 0f, AnchoNormal, 5f, vivos: new[] { 2 }),
        new Sala("R2", "1 · El pasillo que cambia", "R1", 1, 6f, AnchoNormal, 6f, callejones: new[]
        {
            new Callejon(0, 10f, Fin.EspejoEnAngulo, "Pasillo falso este"),
            new Callejon(2, 10f, Fin.EspejoEnAngulo, "Pasillo falso oeste"),
        }),
        new Sala("R3", "2 · El discurso", "R2", 1, 8f, AnchoNormal, 6f, vivos: new[] { 1 }),
        new Sala("R4", "3 · La pesadilla", "R3", 0, 8f, AnchoNormal, 10f, vivos: new[] { 0, 2 }),
        new Sala("R5", "4 · El perro", "R4", 1, 10f, AnchoNormal, 5f, vivos: new[] { 0 }),
        new Sala("R6", "5 · La leña de Aldona", "R5", 2, 8f, AnchoNormal, 6f, vivos: new[] { 2 }),
        new Sala("R7a", "6 · Pasillo tras pasillo", "R6", 1, 8f, AnchoNormal, 5f, callejones: new[]
        {
            new Callejon(2, 8f, Fin.Plata, "Callejón"),
        }),
        new Sala("R7b", "6 · Pasillo tras pasillo", "R7a", 0, 8f, AnchoNormal, 5f, callejones: new[]
        {
            new Callejon(5, 6f, Fin.Plata, "Callejón"),
        }),
        new Sala("R8", "7 · La separación", "R7b", 1, 10f, AnchoNormal, 5f, vivos: new[] { 0 }),
        new Sala("R9", "8 · Las llamadas", "R8", 2, 6f, AnchoNormal, 9f),
        new Sala("RA", "Llamada: el incendio", "R9", 0, 5f, 3.5f, 5f),
        new Sala("RB", "Llamada: el río", "R9", 1, 5f, 3.5f, 5f),
        new Sala("RC", "Llamada: el cuarto a oscuras", "R9", 3, 5f, 3.5f, 6f, vivos: new[] { 3 }),
        new Sala("RD", "Llamada: la puerta", "R9", 4, 5f, 3.5f, 5f),
        new Sala("R10", "9 · La cinta y el niño", "R9", 2, 12f, 3.5f, 7f, vivos: new[] { 3 }),
        new Sala("R11", "10 · Por su nombre", "R10", 1, 8f, AnchoNormal, 7f, vivos: new[] { 3, 5 }, callejones: new[]
        {
            new Callejon(0, 4f, Fin.Plata, "Detrás del muro: Estela"),
            new Callejon(2, 4f, Fin.Plata, "Detrás del muro: Liam"),
        }),
        new Sala("R12", "10 · El primer día", "R11", 1, 10f, AnchoNormal, 7f, vivos: new[] { 1 }),
    };

    [MenuItem("El Sendero/Escenario/Prueba de Will: montar el nivel (maqueta)")]
    public static void MontarNivel()
    {
        if (Shader.Find("Sendero/Espejo") == null || Shader.Find("Sendero/CieloDeLuz") == null)
        {
            EditorUtility.DisplayDialog("Prueba de Will", "Los shaders Sendero/Espejo y Sendero/CieloDeLuz aún no están importados. Espera a que Unity termine de compilar y vuelve a intentarlo.", "Aceptar");
            return;
        }
        if (!EditorUtility.DisplayDialog("Prueba de Will",
                "Se rehace Laberinto.unity desde cero con la maqueta del nivel (los diez tramos, sin contenido). Se cierran las escenas abiertas (te pregunta antes si quieres guardarlas). Hornear los reflejos tarda un par de minutos.",
                "Montar", "Cancelar"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var m = KitDeLaberintoDeEspejos.MaterialesDePalacio(CarpetaMateriales);
        BorrarReflejosViejos();
        Scene escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Ambiente(m.Cielo, KitDeLaberintoDeEspejos.ReflejoDelCielo(CarpetaMateriales, m.Cielo));

        var plano = Plano();
        var porId = new Dictionary<string, Sala>();
        Colocar(plano, porId);

        var palacio = new GameObject("Palacio de espejos").transform;
        var puntos = new GameObject("Puntos del nivel").transform;
        var rotulos = new GameObject("Rótulos de maqueta").transform;
        var sondas = new List<ReflectionProbe>();

        foreach (var s in plano)
        {
            KitDeLaberintoDeEspejos.SalaHexagonal(palacio, $"{s.Id} · {s.Nombre}", s.Centro, s.Apotema, s.Aberturas, s.Vivos, m);
            sondas.Add(KitDeLaberintoDeEspejos.SondaQueEnvuelve(palacio, $"Sonda {s.Id}",
                s.Centro + new Vector3(-s.Apotema * 1.155f, 0f, -s.Apotema), s.Centro + new Vector3(s.Apotema * 1.155f, 0f, s.Apotema)));

            if (s.Padre != null)
            {
                var padre = porId[s.Padre];
                Vector3 desde = KitDeLaberintoDeEspejos.CentroDeLado(padre.Centro, padre.Apotema, s.LadoDelPadre);
                Vector3 hasta = KitDeLaberintoDeEspejos.CentroDeLado(s.Centro, s.Apotema, s.LadoDeEntrada);
                KitDeLaberintoDeEspejos.Pasillo(palacio, $"Pasillo {padre.Id} → {s.Id}", desde, hasta, s.Ancho, m);
                Vector3 t = Vector3.Cross(Vector3.up, (hasta - desde).normalized) * (s.Ancho * 0.5f);
                sondas.Add(KitDeLaberintoDeEspejos.SondaQueEnvuelve(palacio, $"Sonda pasillo {s.Id}", desde + t, desde - t, hasta + t, hasta - t));
            }

            foreach (var c in s.Callejones)
            {
                Vector3 boca = KitDeLaberintoDeEspejos.CentroDeLado(s.Centro, s.Apotema, c.Lado);
                Vector3 dir = KitDeLaberintoDeEspejos.NormalDeLado(c.Lado);
                Vector3 fondo = KitDeLaberintoDeEspejos.Callejon(palacio, $"{s.Id} · {c.Nombre}", boca, dir, AnchoNormal, c.Largo, c.Fin, m);
                Vector3 t = Vector3.Cross(Vector3.up, dir) * (AnchoNormal * 0.5f);
                sondas.Add(KitDeLaberintoDeEspejos.SondaQueEnvuelve(palacio, $"Sonda {s.Id} {c.Lado}", boca + t, boca - t, fondo + t, fondo - t));
            }

            Vector3 haciaDentro = s.Padre == null ? Vector3.forward : -KitDeLaberintoDeEspejos.NormalDeLado(s.LadoDeEntrada);
            KitDeLaberintoDeEspejos.Rotulo(rotulos, s.Nombre, s.Centro, haciaDentro);
        }

        Puntos(porId, puntos, rotulos);
        KitDeLaberintoDeEspejos.Suelo(palacio, ZonaDelSuelo(plano), m);

        var navMesh = palacio.gameObject.AddComponent<NavMeshSurface>();
        navMesh.collectObjects = CollectObjects.Children;
        navMesh.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        navMesh.BuildNavMesh();

        // Las sondas se hornean con el nivel vacío: los personajes no deben quedar en la plata.
        KitDeLaberintoDeEspejos.HornearSondas(sondas.ToArray(), CarpetaMateriales);

        Personajes(escena, porId["R1"]);
        PruebaDeWillRecuerdos.Montar(escena);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena, RutaEscena);
        AsegurarEnBuild(RutaEscena);
        AssetDatabase.SaveAssets();

        // Los guiones de los recuerdos usan las marcas de esta escena: se hornean con ella ya montada.
        string horneado = PruebaDeWillRecuerdos.Hornear(escena);
        EditorUtility.DisplayDialog("Prueba de Will", $"Nivel montado: {plano.Count} salas.\n\n{horneado}\n\nDale a Play y acércate al espejo de la sala del discurso.", "Aceptar");
    }

    /// <summary>Calcula el centro de cada sala a partir de su padre y apunta los huecos de cada lado.</summary>
    private static void Colocar(List<Sala> plano, Dictionary<string, Sala> porId)
    {
        foreach (var s in plano)
        {
            if (s.Padre == null) s.Centro = Vector3.zero;
            else
            {
                var padre = porId[s.Padre];
                s.Centro = padre.Centro + KitDeLaberintoDeEspejos.NormalDeLado(s.LadoDelPadre) * (padre.Apotema + s.Largo + s.Apotema);
                padre.Aberturas[s.LadoDelPadre] = s.Ancho;
                s.Aberturas[s.LadoDeEntrada] = s.Ancho;
            }
            foreach (var c in s.Callejones) s.Aberturas[c.Lado] = AnchoNormal;
            porId[s.Id] = s;
        }
    }

    /// <summary>Puntos con nombre que usarán las tandas siguientes, cada uno con su rótulo de maqueta.</summary>
    private static void Puntos(Dictionary<string, Sala> s, Transform puntos, Transform rotulos)
    {
        void Punto(string nombre, Vector3 pos, Vector3 mirar)
        {
            KitDeLaberintoDeEspejos.Punto(puntos, nombre, pos, mirar);
            KitDeLaberintoDeEspejos.Rotulo(rotulos, nombre, pos + Vector3.down * 1.2f, mirar, 4f);
        }
        Vector3 Entrada(Sala sala) => sala.Padre == null
            ? sala.Centro + Vector3.back * (sala.Apotema - 1.5f)
            : KitDeLaberintoDeEspejos.CentroDeLado(sala.Centro, sala.Apotema - 1.5f, sala.LadoDeEntrada);
        Vector3 Dentro(Sala sala) => sala.Padre == null ? Vector3.forward : -KitDeLaberintoDeEspejos.NormalDeLado(sala.LadoDeEntrada);
        Vector3 Lado(Sala sala, int k, float metrosDentro) => KitDeLaberintoDeEspejos.CentroDeLado(sala.Centro, sala.Apotema - metrosDentro, k);

        foreach (var (id, tramo) in new[] { ("R1", 1), ("R4", 3), ("R7a", 6), ("R9", 8), ("R11", 10) })
            Punto($"Guardado · tramo {tramo}", Entrada(s[id]), Dentro(s[id]));

        Punto("Puerta que desaparece", Lado(s["R1"], 4, 0.8f), Vector3.back);
        Punto("Parpadeo: el pasillo se vuelve tres", s["R2"].Centro, Dentro(s["R2"]));
        Punto("Ventana del recuerdo: el discurso", Lado(s["R3"], 1, 1.5f), KitDeLaberintoDeEspejos.NormalDeLado(1));
        Punto("Arena de la pesadilla", s["R4"].Centro, Dentro(s["R4"]));
        Punto("Ventana del recuerdo: el perro", Lado(s["R5"], 0, 1.5f), KitDeLaberintoDeEspejos.NormalDeLado(0));
        Punto("Entrada al recuerdo: la leña", Lado(s["R6"], 2, 1.5f), KitDeLaberintoDeEspejos.NormalDeLado(2));
        Punto("Separación: no hay nadie detrás", s["R8"].Centro, Dentro(s["R8"]));
        Punto("Estela bajo las rocas", s["R9"].Centro, Dentro(s["R9"]));
        Punto("Rescate: el incendio (Aldona)", s["RA"].Centro, Dentro(s["RA"]));
        Punto("Rescate: el río (Lucía)", s["RB"].Centro, Dentro(s["RB"]));
        Punto("Combate 2: el cuarto a oscuras", s["RC"].Centro, Dentro(s["RC"]));
        Punto("Rescate: el niño tras la puerta", s["RD"].Centro, Dentro(s["RD"]));
        Punto("El niño de las chaquetas", s["R10"].Centro, Dentro(s["R10"]));
        Punto("Muro que cae: Estela", Lado(s["R11"], 0, 0f), -KitDeLaberintoDeEspejos.NormalDeLado(0));
        Punto("Muro que cae: Liam", Lado(s["R11"], 2, 0f), -KitDeLaberintoDeEspejos.NormalDeLado(2));
        Punto("Recuerdo: el primer día", s["R12"].Centro, Dentro(s["R12"]));
        Punto("Salida al hub", Lado(s["R12"], 1, 1.5f), KitDeLaberintoDeEspejos.NormalDeLado(1));
    }

    /// <summary>Rectángulo en planta que cubre todas las salas, pasillos y callejones, con margen.</summary>
    private static Rect ZonaDelSuelo(List<Sala> plano)
    {
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var s in plano)
        {
            float alcance = s.Apotema * 1.155f;
            foreach (var c in s.Callejones) alcance = Mathf.Max(alcance, s.Apotema + c.Largo + 1f);
            minX = Mathf.Min(minX, s.Centro.x - alcance); maxX = Mathf.Max(maxX, s.Centro.x + alcance);
            minZ = Mathf.Min(minZ, s.Centro.z - alcance); maxZ = Mathf.Max(maxZ, s.Centro.z + alcance);
        }
        const float margen = 4f;
        return Rect.MinMaxRect(minX - margen, minZ - margen, maxX + margen, maxZ + margen);
    }

    /// <summary>Luz de todas partes y sin sombras, bajo el cielo de luz.</summary>
    private static void Ambiente(Material cielo, Cubemap reflejoDelCielo)
    {
        var luz = new GameObject("Luz del Sendero").AddComponent<Light>();
        luz.type = LightType.Directional;
        luz.intensity = 0.9f;
        luz.color = new Color(1f, 0.97f, 0.92f);
        luz.shadows = LightShadows.None;
        luz.transform.rotation = Quaternion.Euler(65f, 30f, 0f);

        RenderSettings.skybox = cielo;
        RenderSettings.sun = luz;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.82f, 0.83f, 0.92f);
        RenderSettings.ambientEquatorColor = new Color(0.78f, 0.76f, 0.78f);
        RenderSettings.ambientGroundColor = new Color(0.74f, 0.7f, 0.64f);
        RenderSettings.fog = false;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = reflejoDelCielo;
    }

    /// <summary>Will, Estela y Liam en la entrada, mirando al norte; la cámara detrás.</summary>
    private static void Personajes(Scene escena, Sala entrada)
    {
        Vector3 c = entrada.Centro;
        var will = Instanciar("Assets/Prefabs/_WILL.prefab", "_WILL", escena, c + new Vector3(0f, 0.05f, -2.5f), Vector3.forward);
        var estela = Instanciar("Assets/Prefabs/_ESTELA.prefab", "_ESTELA", escena, c + new Vector3(-1.4f, 0.05f, -1.2f), Vector3.forward);
        var liam = Instanciar("Assets/Prefabs/_LIAM.prefab", "_LIAM", escena, c + new Vector3(1.4f, 0.05f, -1.2f), Vector3.forward);
        foreach (var p in new[] { will, estela, liam })
            if (p != null) KitDeLaberintoDeEspejos.SinSombra(p);

        Instanciar("Assets/Prefabs/CamaraDelJugador.prefab", "CamaraDelJugador", escena, c + new Vector3(0f, 2.6f, -4.3f), Vector3.forward);
    }

    private static GameObject Instanciar(string ruta, string nombre, Scene escena, Vector3 pos, Vector3 mirar)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (prefab == null)
        {
            Debug.LogWarning($"[PruebaDeWillBuilder] No se encontró {ruta}.");
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
        go.name = nombre;
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(mirar));
        return go;
    }

    /// <summary>Los cubemaps de las sondas se rehacen en cada montaje: se quitan los de montajes anteriores.</summary>
    private static void BorrarReflejosViejos()
    {
        if (!AssetDatabase.IsValidFolder(CarpetaMateriales)) return;
        foreach (string ruta in Directory.GetFiles(CarpetaMateriales, "Reflejo_Sonda*.exr"))
            AssetDatabase.DeleteAsset(ruta.Replace('\\', '/'));
    }

    private static void AsegurarEnBuild(string ruta)
    {
        var escenas = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < escenas.Count; i++)
        {
            if (escenas[i].path != ruta) continue;
            escenas[i] = new EditorBuildSettingsScene(ruta, true);
            EditorBuildSettings.scenes = escenas.ToArray();
            return;
        }
        escenas.Add(new EditorBuildSettingsScene(ruta, true));
        EditorBuildSettings.scenes = escenas.ToArray();
    }
}
