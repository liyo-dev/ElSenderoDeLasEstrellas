using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Prepara un personaje modelado y animado en Blender (Tools/Modelos/…) para usarlo como cualquier otro:
/// - Importación: esqueleto genérico con su Avatar, un clip por acción de Blender con el nombre de la
///   acción («Rig|Idle» → «Idle»), en bucle los de la ficha.
/// - Materiales: cada material del FBX se cambia por uno de color liso con el estilo de otro material
///   del juego (por defecto, el Quibli de los personajes del pack), para que case con el resto.
/// - Controlador: la locomoción en un árbol de mezcla sobre un parámetro de velocidad (como los del
///   pack: 0 quieto, 1 andar, 2 correr) y cada acción restante como estado con su nombre (los gestos
///   que piden los guiones); los que no van en bucle vuelven solos a la locomoción.
/// Se puede repetir: rehace la importación, los materiales y el controlador (conserva sus GUID).
/// </summary>
public static class PersonajeHechoEnBlender
{
    public sealed class Ficha
    {
        public string Fbx;
        public string Controlador;
        public string CarpetaMateriales;
        /// <summary>Material o prefab (se toma el material de su primer Renderer) del que se copia el estilo.</summary>
        public string Estilo;
        public (string material, Color color)[] Colores = new (string, Color)[0];
        public string[] EnBucle = { "Idle", "Walk", "Run" };
        public string ParametroDeVelocidad = "Speed";
        public (string clip, float umbral)[] Locomocion = { ("Idle", 0f), ("Walk", 1f), ("Run", 2f) };
    }

    public static bool Preparar(Ficha f, StringBuilder informe)
    {
        if (!(AssetImporter.GetAtPath(f.Fbx) is ModelImporter importador)) { informe.AppendLine($"Personaje: no encuentro el modelo {f.Fbx}."); return false; }

        importador.animationType = ModelImporterAnimationType.Generic;
        importador.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importador.importAnimation = true;
        importador.clipAnimations = importador.defaultClipAnimations.Select(c =>
        {
            c.name = c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name;
            c.loopTime = f.EnBucle.Contains(c.name);
            return c;
        }).ToArray();

        var estilo = MaterialDeEstilo(f.Estilo);
        if (estilo == null) informe.AppendLine($"Personaje: no encuentro el material de estilo {f.Estilo}; se quedan los del FBX.");
        else
        {
            string nombre = Path.GetFileNameWithoutExtension(f.Fbx);
            foreach (var (material, color) in f.Colores)
                importador.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material),
                                    Liso(estilo, $"{f.CarpetaMateriales}/Mat_{nombre}_{material}.mat", color));
        }
        importador.SaveAndReimport();
        int estados = Controlador(f, informe);
        informe.AppendLine($"Personaje {Path.GetFileName(f.Fbx)}: importado, {f.Colores.Length} materiales y controlador con {estados} estados.");
        return true;
    }

    /// <summary>
    /// Una prenda hecha en Blender con el esqueleto de un personaje (para <see cref="PrendasSueltas"/>):
    /// sin animaciones y con sus materiales cambiados por lisos con el estilo dado.
    /// </summary>
    public static bool PrepararPrenda(string fbx, string carpetaMateriales, string estilo, (string material, Color color)[] colores, StringBuilder informe)
    {
        if (!(AssetImporter.GetAtPath(fbx) is ModelImporter importador)) { informe.AppendLine($"Prenda: no encuentro {fbx}."); return false; }
        importador.animationType = ModelImporterAnimationType.Generic;
        importador.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        importador.importAnimation = false;
        var mat = MaterialDeEstilo(estilo);
        if (mat != null)
        {
            string nombre = Path.GetFileNameWithoutExtension(fbx);
            foreach (var (material, color) in colores)
                importador.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material),
                                    Liso(mat, $"{carpetaMateriales}/Mat_{nombre}_{material}.mat", color));
        }
        else informe.AppendLine($"Prenda: no encuentro el material de estilo {estilo}; se quedan los del FBX.");
        importador.SaveAndReimport();
        informe.AppendLine($"Prenda {Path.GetFileName(fbx)} preparada.");
        return true;
    }

    /// <summary>Material de un color liso con el shader y los ajustes de <paramref name="estilo"/> (sin su textura).</summary>
    public static Material Liso(Material estilo, string ruta, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            mat = new Material(estilo);
            AssetDatabase.CreateAsset(mat, ruta);
        }
        else { mat.shader = estilo.shader; mat.CopyPropertiesFromMaterial(estilo); }
        // El color va en una textura lisa de ese color: en los shaders toon (Quibli) el color principal no
        // tiñe (lo hace la textura) y sin textura el albedo por defecto es negro.
        var textura = TexturaDeColor(Path.ChangeExtension(ruta, null) + "_Color.png", color);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", textura);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", textura);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>Textura de 4×4 de un solo color (se rehace si cambia el color).</summary>
    public static Texture2D TexturaDeColor(string ruta, Color color)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ruta));
        var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color32[16];
        Color32 c = color;
        for (int i = 0; i < px.Length; i++) px[i] = c;
        t.SetPixels32(px); t.Apply();
        File.WriteAllBytes(ruta, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    public static Material MaterialDeEstilo(string ruta)
    {
        if (string.IsNullOrEmpty(ruta)) return null;
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat != null) return mat;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        return prefab != null ? prefab.GetComponentInChildren<Renderer>(true)?.sharedMaterial : null;
    }

    private static int Controlador(Ficha f, StringBuilder informe)
    {
        var clips = AssetDatabase.LoadAllAssetRepresentationsAtPath(f.Fbx).OfType<AnimationClip>()
                                 .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(f.Controlador);
        if (ctrl == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(f.Controlador));
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(f.Controlador);
        }
        // Se vacía para rehacerlo: estados, parámetros y los árboles de mezcla que quedaban dentro.
        var maquina = ctrl.layers[0].stateMachine;
        foreach (var s in maquina.states.ToArray()) maquina.RemoveState(s.state);
        foreach (var p in ctrl.parameters.ToArray()) ctrl.RemoveParameter(p);
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(f.Controlador))
            if (o is BlendTree) Object.DestroyImmediate(o, true);

        ctrl.AddParameter(f.ParametroDeVelocidad, AnimatorControllerParameterType.Float);
        var locomocion = ctrl.CreateBlendTreeInController("Locomocion", out var arbol, 0);
        arbol.blendParameter = f.ParametroDeVelocidad;
        arbol.useAutomaticThresholds = false;
        foreach (var (clip, umbral) in f.Locomocion)
            if (clips.TryGetValue(clip, out var c)) arbol.AddChild(c, umbral);
            else informe.AppendLine($"Personaje: falta el clip de locomoción «{clip}» en {f.Fbx}.");
        maquina.defaultState = locomocion;

        int n = 1;
        foreach (var c in clips.Values)
        {
            if (f.Locomocion.Any(l => l.clip == c.name)) continue;
            var estado = maquina.AddState(c.name);
            estado.motion = c;
            n++;
            if (f.EnBucle.Contains(c.name)) continue;
            var vuelta = estado.AddTransition(locomocion);
            vuelta.hasExitTime = true;
            vuelta.exitTime = 0.95f;
            vuelta.duration = 0.15f;
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return n;
    }
}
