using UnityEditor;
using UnityEngine;

/// Le pone un NPCEmotionController a los aldeanos que no lo tengan.
///
/// «Las caras no cambian.» De los diez vecinos del prólogo, NUEVE cambian de cara y uno no, y el
/// que no lo hace lo dice la consola en cada partida:
///
///     [SequenceActor:NPC_Aldeano_08] No tiene NPCEmotionController — no se puede cambiar la cara
///
/// `TownNpc#8.prefab` se quedó sin el componente y nadie lo notó, porque un EmotionBeat sobre
/// alguien sin controlador no rompe nada: simplemente no pasa nada con su cara.
///
/// Esto lo arregla en el prefab, copiando el `emotionProfile` de un hermano que sí lo tenga. Los
/// meshes de ojos y boca NO hay que asignarlos: `NPCEmotionController` los busca por prefijo en su
/// propia jerarquía (`Eye…`, `Mouth…`) y, si no se le dice cuáles son los de partida, usa los que
/// estén activos. Así el arreglo vale igual para cualquier otro vecino al que le pase lo mismo.
///
/// Se edita el prefab con PrefabUtility y los campos por SerializedObject — ni el YAML a mano
/// (INC-249) ni API nueva en una clase de juego solo para que la toque una herramienta.
public static class ArreglarCarasDeLosAldeanos
{
    private const string RutaTownNpcs = "Assets/_NPCs/NonInteractable/TownNpc#{0}.prefab";
    private const int CuantosAldeanos = 10;
    private const string CampoPerfil = "emotionProfile";

    [MenuItem("El Sendero/Prólogo: arreglar las caras de los aldeanos")]
    public static void Arreglar()
    {
        int tocados = Ejecutar(avisar: true);
        EditorUtility.DisplayDialog("Caras de los aldeanos",
            tocados == 0
                ? "Todos los aldeanos ya tenían su NPCEmotionController."
                : $"{tocados} aldeano(s) ya pueden cambiar de cara.",
            "Vale");
    }

    /// Lo mismo, sin diálogo, para llamarlo desde PREPARAR TODO.
    public static int Ejecutar(bool avisar)
    {
        // Primero, un perfil de referencia: el de cualquier vecino que sí lo tenga. Son todos el
        // mismo asset, así que vale el primero que aparezca.
        UnityEngine.Object perfil = null;
        for (int i = 1; i <= CuantosAldeanos && perfil == null; i++)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(string.Format(RutaTownNpcs, i));
            var c = p != null ? p.GetComponentInChildren<NPCEmotionController>(true) : null;
            if (c == null) continue;

            var campo = new SerializedObject(c).FindProperty(CampoPerfil);
            if (campo != null) perfil = campo.objectReferenceValue;
        }

        if (perfil == null)
        {
            if (avisar)
                Debug.LogWarning("[Caras] Ningún TownNpc tiene un EmotionProfile del que copiar. " +
                    "Asigna uno a mano en el Inspector de cualquiera y vuelve a ejecutar esto.");
            return 0;
        }

        int tocados = 0;

        for (int i = 1; i <= CuantosAldeanos; i++)
        {
            string ruta = string.Format(RutaTownNpcs, i);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (prefab == null)
            {
                if (avisar) Debug.LogWarning($"[Caras] No encuentro '{ruta}'.");
                continue;
            }

            if (prefab.GetComponentInChildren<NPCEmotionController>(true) != null) continue;

            // Se abre el prefab de verdad, se toca, y se guarda: tocar la instancia que devuelve
            // el AssetDatabase no persiste.
            var contenido = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                // Va en el mismo GameObject que el animador, que es de donde lo resuelve
                // EmotionControllerResolver; si no hay animador, en la raíz.
                var animador = contenido.GetComponentInChildren<NPCSimpleAnimator>(true);
                var donde = animador != null ? animador.gameObject : contenido;

                var nuevo = donde.AddComponent<NPCEmotionController>();

                var so = new SerializedObject(nuevo);
                var campo = so.FindProperty(CampoPerfil);
                if (campo != null)
                {
                    campo.objectReferenceValue = perfil;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else if (avisar)
                {
                    Debug.LogWarning($"[Caras] NPCEmotionController ya no tiene un campo " +
                        $"'{CampoPerfil}'. El componente se añade igual, pero hay que asignarle el " +
                        "perfil a mano.");
                }

                PrefabUtility.SaveAsPrefabAsset(contenido, ruta);
                tocados++;

                Debug.Log($"[Caras] '{System.IO.Path.GetFileNameWithoutExtension(ruta)}' ya tiene " +
                    $"NPCEmotionController (en '{donde.name}'), con el perfil '{perfil.name}'. " +
                    "Los meshes de ojos y boca se resuelven solos por prefijo.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contenido);
            }
        }

        if (tocados > 0) AssetDatabase.SaveAssets();
        return tocados;
    }
}
