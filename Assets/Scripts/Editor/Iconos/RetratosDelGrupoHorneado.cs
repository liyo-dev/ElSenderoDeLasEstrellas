using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Sendero.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;
using Slot = PartyControlManager.CharacterSlot;

namespace ElSendero.Editor.Iconos
{
    /// <summary>
    /// Hornea los retratos del grupo desde los modelos reales con <see cref="FotografoDeIconos"/>
    /// (mismo contorno que los iconos del juego): por personaje, un busto con cada cara de
    /// <see cref="ExpresionDeRetrato"/> (pose de una animación del pack y ojos y boca del perfil de
    /// emociones) y el retrato redondo de las fichas (informe de victoria). Los bustos van al juego
    /// de retratos del grupo (<see cref="RetratosDelGrupoBuilder.RutaJuego"/>), así que el HUD los
    /// usa sin volver a montarlo; los redondos sobrescriben `Assets/Art/UI/Retratos/retrato_*.png`
    /// conservando su GUID. Se puede repetir cuando cambie un modelo.
    /// </summary>
    public static class RetratosDelGrupoHorneado
    {
        private const string CarpetaBustos = "Assets/Art/UI/Retratos/Grupo";
        private const string CarpetaRedondos = "Assets/Art/UI/Retratos";
        private const string CarpetaAnimaciones = "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon";
        private const int TamanoBusto = 512;
        private const int TamanoRedondo = 384;
        /// Parte del busto que queda por debajo del borde del panel (esconde el corte de la foto).
        private const float FraccionBajoElBorde = 0.1f;
        private static readonly Color FondoRedondo = new Color(42f / 255f, 34f / 255f, 80f / 255f, 1f);

        private static readonly (Slot slot, string prefab, string nombre)[] Personajes =
        {
            (Slot.Liam, "Assets/Prefabs/_LIAM.prefab", "liam"),
            (Slot.Will, "Assets/Prefabs/_WILL_NPC.prefab", "will"),
            (Slot.Estela, "Assets/Prefabs/_ESTELA.prefab", "estela"),
        };

        private readonly struct Cara
        {
            public readonly ExpresionDeRetrato expresion;
            public readonly string clip;
            public readonly float momento;
            public readonly NPCEmotion emocion;
            public readonly bool ojosCerrados;

            public Cara(ExpresionDeRetrato expresion, string clip, float momento, NPCEmotion emocion, bool ojosCerrados = false)
            {
                this.expresion = expresion; this.clip = clip; this.momento = momento;
                this.emocion = emocion; this.ojosCerrados = ojosCerrados;
            }
        }

        // La primera es la Normal: su encuadre se reutiliza en las demás para que la cabeza no
        // cambie de sitio en el HUD al cambiar de cara.
        private static readonly Cara[] Caras =
        {
            new Cara(ExpresionDeRetrato.Normal, "Idle_Normal_NoWeapon", 0.35f, NPCEmotion.Neutral),
            new Cara(ExpresionDeRetrato.Dolorido, "GetHit01_NoWeapon", 0.45f, NPCEmotion.Tired),
            new Cara(ExpresionDeRetrato.Caido, "Dizzy_NoWeapon", 0.3f, NPCEmotion.Sad, ojosCerrados: true),
            new Cara(ExpresionDeRetrato.Contento, "Victory_NoWeapon", 0.6f, NPCEmotion.Happy),
        };

        [MenuItem("El Sendero/UI/Hornear retratos del grupo (INC-730)")]
        public static void Hornear()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("Los retratos del grupo se hornean fuera de Play."); return; }

            var log = new StringBuilder("=== Hornear retratos del grupo (INC-730) ===\n");
            var avisos = new List<string>();
            var sprites = new Dictionary<(Slot, ExpresionDeRetrato), string>();
            try
            {
                for (int i = 0; i < Personajes.Length; i++)
                {
                    var p = Personajes[i];
                    EditorUtility.DisplayProgressBar("Retratos del grupo", "Fotografiando a " + p.nombre, i / (float)Personajes.Length);
                    HornearPersonaje(p.slot, p.prefab, p.nombre, sprites, log, avisos);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var ruta in sprites.Values) ComoSprite(ruta);
            foreach (var p in Personajes) ComoSprite($"{CarpetaRedondos}/retrato_{p.nombre}.png");
            RellenarJuego(sprites, log, avisos);
            AssetDatabase.SaveAssets();

            foreach (var a in avisos) log.AppendLine("• " + a);
            if (avisos.Count == 0) Debug.Log(log.ToString()); else Debug.LogWarning(log.ToString());
        }

        private static void HornearPersonaje(Slot slot, string rutaPrefab, string nombre,
            Dictionary<(Slot, ExpresionDeRetrato), string> sprites, StringBuilder log, List<string> avisos)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
            if (prefab == null) { avisos.Add("No encuentro " + rutaPrefab + "."); return; }
            var personaje = prefab.GetComponentInChildren<Personaje>(true);
            if (personaje == null || personaje.Ficha == null || personaje.Ficha.Personaje != slot)
                avisos.Add(rutaPrefab + " no lleva la ficha de " + slot + " en su componente Personaje; se fotografía igualmente.");

            var escena = EditorSceneManager.NewPreviewScene();
            try
            {
                var modelo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
                modelo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var cara = LeerCaras(modelo, avisos, nombre);
                var animator = BuscarAnimator(modelo, cara);
                if (animator == null) avisos.Add(nombre + ": sin Animator humanoide; se fotografía en la pose del prefab.");

                Bounds? encuadre = null;
                foreach (var c in Caras)
                {
                    var (ojos, boca) = cara.Resolver(c);
                    var ajustes = new AjustesDeFoto
                    {
                        tamano = TamanoBusto, margen = 0.04f, yaw = 12f, pitch = 4f,
                        poseDelOrigen = true, alturaDesdeArriba = 0.62f, encuadre = encuadre,
                    };
                    Texture2D foto = null;
                    using (var pose = Posar(animator, c.clip, c.momento, avisos, nombre))
                    {
                        if (animator != null) EstudioDeRetratos.OrientarDeFrente(modelo.transform, animator);
                        foto = FotografoDeIconos.Fotografiar(modelo, r => cara.Visible(r, ojos, boca), ajustes);
                        if (c.expresion == ExpresionDeRetrato.Normal)
                        {
                            encuadre = ajustes.encuadreUsado;
                            var cabeza = new AjustesDeFoto
                            {
                                tamano = TamanoRedondo, margen = 0.02f, yaw = 12f, pitch = 4f,
                                poseDelOrigen = true, alturaDesdeArriba = 0.47f, grosorContorno = 4,
                            };
                            var primerPlano = FotografoDeIconos.Fotografiar(modelo, r => cara.Visible(r, ojos, boca), cabeza);
                            try { GuardarRedondo(primerPlano, $"{CarpetaRedondos}/retrato_{nombre}.png"); }
                            finally { Object.DestroyImmediate(primerPlano); }
                        }
                    }
                    try
                    {
                        string ruta = $"{CarpetaBustos}/{nombre}_{c.expresion.ToString().ToLowerInvariant()}.png";
                        FotografoDeIconos.GuardarPng(foto, ruta);
                        sprites[(slot, c.expresion)] = ruta;
                    }
                    finally { Object.DestroyImmediate(foto); }
                }
                log.AppendLine($"{nombre}: {Caras.Length} caras y el retrato redondo.");
            }
            catch (Exception ex) { avisos.Add(nombre + ": " + ex.Message); }
            finally { EditorSceneManager.ClosePreviewScene(escena); }
        }

        // ─── Caras ─────────────────────────────────────────────────────────────

        private sealed class CarasDelModelo
        {
            public string prefijoOjos = "Eye", prefijoBoca = "Mouth";
            public string ojosReposo, bocaReposo, ojosCerrados = "Eye09";
            public EmotionProfile perfil;
            public readonly HashSet<string> ojos = new HashSet<string>();
            public readonly HashSet<string> bocas = new HashSet<string>();

            public (string ojos, string boca) Resolver(Cara c)
            {
                string o = ojosReposo, b = bocaReposo;
                if (perfil != null && c.emocion != NPCEmotion.Neutral)
                {
                    var datos = perfil.GetEmotionData(c.emocion);
                    if (!string.IsNullOrEmpty(datos.eyeMeshName) && ojos.Contains(datos.eyeMeshName)) o = datos.eyeMeshName;
                    if (!string.IsNullOrEmpty(datos.mouthMeshName) && bocas.Contains(datos.mouthMeshName)) b = datos.mouthMeshName;
                }
                if (c.ojosCerrados && ojos.Contains(ojosCerrados)) o = ojosCerrados;
                return (o, b);
            }

            /// Solo se ven los ojos y la boca de la cara elegida; lo demás, como está en el prefab.
            public bool Visible(Renderer r, string ojosElegidos, string bocaElegida)
            {
                string n = r.gameObject.name;
                if (EsParte(n, prefijoOjos)) return n == ojosElegidos;
                if (EsParte(n, prefijoBoca)) return n == bocaElegida;
                return r.enabled && r.gameObject.activeInHierarchy;
            }

            public static bool EsParte(string nombre, string prefijo)
            {
                if (string.IsNullOrEmpty(prefijo) || nombre.Length <= prefijo.Length || !nombre.StartsWith(prefijo, StringComparison.Ordinal)) return false;
                for (int i = prefijo.Length; i < nombre.Length; i++) if (!char.IsDigit(nombre[i])) return false;
                return true;
            }
        }

        private static CarasDelModelo LeerCaras(GameObject modelo, List<string> avisos, string nombre)
        {
            var cara = new CarasDelModelo();
            var emociones = modelo.GetComponentInChildren<NPCEmotionController>(true);
            GameObject ojosReposo = null, bocaReposo = null;
            if (emociones != null)
            {
                var so = new SerializedObject(emociones);
                cara.perfil = so.FindProperty("emotionProfile")?.objectReferenceValue as EmotionProfile;
                ojosReposo = so.FindProperty("ojosDeReposo")?.objectReferenceValue as GameObject;
                bocaReposo = so.FindProperty("bocaDeReposo")?.objectReferenceValue as GameObject;
                cara.prefijoOjos = so.FindProperty("eyePrefix")?.stringValue ?? cara.prefijoOjos;
                cara.prefijoBoca = so.FindProperty("mouthPrefix")?.stringValue ?? cara.prefijoBoca;
                var cerrados = so.FindProperty("ojosDelParpadeo")?.stringValue;
                if (!string.IsNullOrEmpty(cerrados)) cara.ojosCerrados = cerrados;
            }
            else avisos.Add(nombre + ": sin NPCEmotionController; se usan los ojos y la boca activos del prefab.");

            string ojosActivos = null, bocaActiva = null;
            foreach (var r in modelo.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                if (CarasDelModelo.EsParte(n, cara.prefijoOjos))
                {
                    cara.ojos.Add(n);
                    if (r.gameObject.activeInHierarchy && (ojosActivos == null || (cara.perfil != null && cara.perfil.EsOjoNeutro(n)))) ojosActivos = n;
                }
                else if (CarasDelModelo.EsParte(n, cara.prefijoBoca))
                {
                    cara.bocas.Add(n);
                    if (r.gameObject.activeInHierarchy && (bocaActiva == null || (cara.perfil != null && cara.perfil.EsBocaNeutra(n)))) bocaActiva = n;
                }
            }
            cara.ojosReposo = ojosReposo != null ? ojosReposo.name : ojosActivos ?? cara.perfil?.ojosDeReposoPorDefecto;
            cara.bocaReposo = bocaReposo != null ? bocaReposo.name : bocaActiva ?? cara.perfil?.bocaDeReposoPorDefecto;
            if (cara.ojos.Count == 0) avisos.Add(nombre + ": no tiene mallas de ojos (" + cara.prefijoOjos + "##); todas las caras saldrán iguales.");
            return cara;
        }

        // ─── Pose ──────────────────────────────────────────────────────────────

        private static Animator BuscarAnimator(GameObject modelo, CarasDelModelo cara)
        {
            foreach (var r in modelo.GetComponentsInChildren<Renderer>(true))
            {
                if (!CarasDelModelo.EsParte(r.gameObject.name, cara.prefijoOjos)) continue;
                var a = r.GetComponentInParent<Animator>(true);
                if (a != null && a.avatar != null && a.avatar.isHuman) return a;
            }
            foreach (var a in modelo.GetComponentsInChildren<Animator>(true))
                if (a.avatar != null && a.avatar.isHuman) return a;
            return null;
        }

        /// Deja el esqueleto en el momento indicado de la animación mientras dura el using.
        private static IDisposable Posar(Animator animator, string nombreClip, float momento, List<string> avisos, string nombre)
        {
            if (animator == null) return new Pose(default);
            var clip = BuscarClip(nombreClip);
            if (clip == null) { avisos.Add($"{nombre}: no encuentro la animación {nombreClip}; esa cara sale en la pose del prefab."); return new Pose(default); }
            var grafo = PlayableGraph.Create("Retrato del grupo");
            grafo.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var salida = AnimationPlayableOutput.Create(grafo, "Pose", animator);
            var reproductor = AnimationClipPlayable.Create(grafo, clip);
            reproductor.SetApplyFootIK(false);
            reproductor.SetTime(clip.length * Mathf.Clamp01(momento));
            reproductor.Pause();
            salida.SetSourcePlayable(reproductor);
            var mano = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            var antes = mano != null ? mano.position : Vector3.zero;
            grafo.Evaluate(0f);
            if (mano != null && (mano.position - antes).sqrMagnitude < 1e-8f && nombreClip != "Idle_Normal_NoWeapon")
                avisos.Add($"{nombre}: la animación {nombreClip} no ha movido el esqueleto; esa cara puede salir en la pose anterior.");
            return new Pose(grafo);
        }

        private readonly struct Pose : IDisposable
        {
            private readonly PlayableGraph _grafo;
            public Pose(PlayableGraph grafo) => _grafo = grafo;
            public void Dispose() { if (_grafo.IsValid()) _grafo.Destroy(); }
        }

        private static AnimationClip BuscarClip(string nombre)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath($"{CarpetaAnimaciones}/{nombre}.fbx"))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal)) return clip;
            return null;
        }

        // ─── Archivos ──────────────────────────────────────────────────────────

        /// Primer plano sobre un círculo con el fondo de los retratos de las fichas.
        private static void GuardarRedondo(Texture2D foto, string ruta)
        {
            int n = foto.width;
            var px = foto.GetPixels();
            var salida = new Color[n * n];
            float radio = n * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - radio, dy = y + 0.5f - radio;
                float dentro = Mathf.Clamp01(radio - Mathf.Sqrt(dx * dx + dy * dy));
                var f = px[y * n + x];
                var c = Color.Lerp(FondoRedondo, new Color(f.r, f.g, f.b, 1f), f.a);
                c.a = dentro;
                salida[y * n + x] = c;
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixels(salida);
                tex.Apply();
                File.WriteAllBytes(ruta, tex.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(tex); }
        }

        private static void ComoSprite(string ruta)
        {
            AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
            if (!(AssetImporter.GetAtPath(ruta) is TextureImporter imp)) return;
            if (imp.textureType == TextureImporterType.Sprite && !imp.mipmapEnabled && imp.alphaIsTransparency) return;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }

        private static void RellenarJuego(Dictionary<(Slot, ExpresionDeRetrato), string> sprites, StringBuilder log, List<string> avisos)
        {
            var juego = AssetDatabase.LoadAssetAtPath<JuegoDeRetratos>(RetratosDelGrupoBuilder.RutaJuego);
            if (juego == null)
            {
                juego = ScriptableObject.CreateInstance<JuegoDeRetratos>();
                AssetDatabase.CreateAsset(juego, RetratosDelGrupoBuilder.RutaJuego);
                avisos.Add("No existía el juego de retratos: ejecuta también «Montar retratos del grupo».");
            }
            var so = new SerializedObject(juego);
            var lista = so.FindProperty("retratos");
            foreach (var p in Personajes)
            {
                if (!sprites.ContainsKey((p.slot, ExpresionDeRetrato.Normal))) continue;
                SerializedProperty e = null;
                for (int i = 0; i < lista.arraySize; i++)
                    if (lista.GetArrayElementAtIndex(i).FindPropertyRelative("personaje").enumValueIndex == (int)p.slot) { e = lista.GetArrayElementAtIndex(i); break; }
                if (e == null)
                {
                    lista.arraySize++;
                    e = lista.GetArrayElementAtIndex(lista.arraySize - 1);
                    e.FindPropertyRelative("personaje").enumValueIndex = (int)p.slot;
                }
                Poner(e, "normal", sprites, p.slot, ExpresionDeRetrato.Normal);
                Poner(e, "dolorido", sprites, p.slot, ExpresionDeRetrato.Dolorido);
                Poner(e, "caido", sprites, p.slot, ExpresionDeRetrato.Caido);
                Poner(e, "contento", sprites, p.slot, ExpresionDeRetrato.Contento);
                e.FindPropertyRelative("fraccionBajoElBorde").floatValue = FraccionBajoElBorde;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(juego);
            log.AppendLine("Juego de retratos del grupo actualizado con las fotos.");
        }

        private static void Poner(SerializedProperty entrada, string campo, Dictionary<(Slot, ExpresionDeRetrato), string> sprites, Slot slot, ExpresionDeRetrato expresion)
        {
            Sprite sprite = sprites.TryGetValue((slot, expresion), out var ruta) ? AssetDatabase.LoadAssetAtPath<Sprite>(ruta) : null;
            entrada.FindPropertyRelative(campo).objectReferenceValue = sprite;
        }
    }
}
