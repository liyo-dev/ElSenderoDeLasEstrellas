using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static VfxProcedural;

/// Piezas de efecto en 3D con el mismo shader del mundo (El Sendero/Dibujo): dos tonos, sombra
/// lila y contorno de tinta del post-proceso, igual que personajes y casas. Lo que es materia
/// (humo, nubes) es sólido; lo que es luz (estrellas, pétalos de luz) lleva además emisión de su
/// color. Las mallas salen de Tools/Modelos/VFX (Blender) y viven en Assets/_VFX/Mallas.
/// Ver INC-1127
public static partial class VfxPropiosDeHechizosBuilder
{
    private const string CarpetaMallas = "Assets/_VFX/Mallas";
    private const string CarpetaMateriales3D = Carpeta + "/Materiales/Dibujo3D";
    private const string ShaderDelMundo = "El Sendero/Dibujo";
    private const string PrefijoLuz = "Dibujo3D_Luz_";
    private const float FuerzaDeLaEmision = 0.55f;

    /// Pieza 2D que pasa a ser malla 3D, y con qué malla.
    private static readonly Dictionary<string, string> PiezasEn3D = new Dictionary<string, string>
    {
        ["estrella"] = "Estrella4",
        ["humo"] = "Bolita",
    };

    private static readonly HashSet<string> PiezasDeLuz = new HashSet<string> { "estrella" };

    private static readonly Dictionary<string, Mesh> _mallas = new Dictionary<string, Mesh>();
    private static readonly Dictionary<string, Material> _materiales3D = new Dictionary<string, Material>();

    private static Mesh Malla(string nombre)
    {
        if (_mallas.TryGetValue(nombre, out var m) && m != null) return m;
        string ruta = $"{CarpetaMallas}/VFX_{nombre}.fbx";
        if (AssetImporter.GetAtPath(ruta) is ModelImporter imp &&
            (imp.materialImportMode != ModelImporterMaterialImportMode.None || imp.importAnimation || !imp.isReadable))
        {
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.isReadable = true;   // las partículas con malla la leen en CPU
            imp.SaveAndReimport();
        }
        m = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Mesh>().FirstOrDefault();
        if (m == null) Debug.LogWarning("[VFX] Falta la malla " + ruta);
        _mallas[nombre] = m;
        return m;
    }

    /// Material del mundo para piezas de efecto: el color lo pone la partícula (colores de vértice);
    /// las de luz llevan además emisión de su color para que brillen y las coja el bloom.
    private static Material Material3D(Color? luz)
    {
        string nombre = luz.HasValue ? PrefijoLuz + ColorUtility.ToHtmlStringRGB(luz.Value) : "Dibujo3D_Solido";
        if (_materiales3D.TryGetValue(nombre, out var mat) && mat != null) return mat;
        CrearCarpeta(CarpetaMateriales3D);
        string ruta = $"{CarpetaMateriales3D}/{nombre}.mat";
        mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            mat = new Material(Shader.Find(ShaderDelMundo)) { name = nombre };
            AssetDatabase.CreateAsset(mat, ruta);
        }
        mat.SetFloat("_VertexColorsEnabled", 1f);
        mat.EnableKeyword("DR_VERTEX_COLORS_ON");
        if (luz.HasValue)
        {
            mat.SetColor("_EmissionColor", luz.Value * FuerzaDeLaEmision);
            mat.EnableKeyword("_EMISSION");
        }
        else
        {
            mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_EMISSION");
        }
        EditorUtility.SetDirty(mat);
        _materiales3D[nombre] = mat;
        return mat;
    }

    /// Material de una pieza 3D suelta (no partícula): el color va en el tinte, no en los vértices,
    /// porque la malla no los trae.
    private static Material MaterialDePieza(Color luz)
    {
        string nombre = "Dibujo3D_Pieza_" + ColorUtility.ToHtmlStringRGB(luz);
        if (_materiales3D.TryGetValue(nombre, out var mat) && mat != null) return mat;
        CrearCarpeta(CarpetaMateriales3D);
        string ruta = $"{CarpetaMateriales3D}/{nombre}.mat";
        mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            mat = new Material(Shader.Find(ShaderDelMundo)) { name = nombre };
            AssetDatabase.CreateAsset(mat, ruta);
        }
        mat.SetColor("_Tinte", luz);
        mat.SetFloat("_VertexColorsEnabled", 0f);
        mat.DisableKeyword("DR_VERTEX_COLORS_ON");
        mat.SetColor("_EmissionColor", luz * FuerzaDeLaEmision);
        mat.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(mat);
        _materiales3D[nombre] = mat;
        return mat;
    }

    private static bool EsLuz3D(ParticleSystemRenderer r)
        => r.renderMode == ParticleSystemRenderMode.Mesh && r.sharedMaterial != null && r.sharedMaterial.name.StartsWith(PrefijoLuz);

    /// Sistema de partículas que pinta una malla 3D con el material del mundo, girada al azar.
    private static ParticleSystem Malla3D(Transform padre, string nombre, string pieza, float duracion, bool bucle)
    {
        string malla = PiezasEn3D[pieza];
        var mat = Material3D(PiezasDeLuz.Contains(pieza) ? Color.white : (Color?)null);
        var ps = Sistema(padre, nombre, mat, ParticleSystemRenderMode.Mesh, duracion, bucle);
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.mesh = Malla(malla);
        r.alignment = ParticleSystemRenderSpace.World;
        var main = ps.main;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startRotationY = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startColor = pieza == "humo" ? new Color(1f, 0.97f, 0.92f) : Color.white;
        return ps;
    }

    // ── Protección: flor de luz ───────────────────────────────────────────

    private const int PetalosDeLaFlor = 6;

    /// Flor de luz abierta alrededor de la cintura, que aparece con rebote y gira despacio. Sustituye
    /// a la burbuja: tiene volumen, la ilumina la escena y lleva el contorno del mundo, y al estar
    /// abierta hacia fuera no tapa al personaje desde la cámara.
    private static GameObject FlorDeLuz(Transform padre, Color color, float radio = 0.55f, float alto = 0.9f, float apertura = 38f)
    {
        var flor = new GameObject("Flor de luz");
        flor.transform.SetParent(padre, false);
        flor.transform.localPosition = Vector3.up * 0.15f;
        var giro = flor.AddComponent<VfxGiroYAparicion>();
        var mat = MaterialDePieza(color);
        var malla = Malla("Petalo");
        for (int i = 0; i < PetalosDeLaFlor; i++)
        {
            float a = i * 360f / PetalosDeLaFlor;
            var fuera = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            var petalo = new GameObject("Pétalo " + (i + 1));
            petalo.transform.SetParent(flor.transform, false);
            petalo.transform.localPosition = fuera * radio;
            // El pétalo crece hacia arriba (Y) y se abre hacia fuera; su cuchara mira al centro.
            petalo.transform.localRotation = Quaternion.LookRotation(-fuera, Vector3.up) * Quaternion.Euler(-apertura, 0f, 0f);
            petalo.transform.localScale = Vector3.one * alto;
            petalo.AddComponent<MeshFilter>().sharedMesh = malla;
            var r = petalo.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        return flor;
    }
}
