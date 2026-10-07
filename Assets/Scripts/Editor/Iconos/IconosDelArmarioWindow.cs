using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ElSendero.Editor.Iconos
{
    public sealed class IconosDelArmarioWindow : EditorWindow
    {
        const string CarpetaIconos = "Assets/Art/UI/Wardrobe/";
        const string CarpetaArmario = "Assets/_WARDROBE ITEMS";
        const string CarpetaInventario = "Assets/_ITEMS/_TEST_WARDROBE";
        [SerializeField] GameObject modelo;
        [SerializeField] int tamano = 512;
        [SerializeField] int grosor = 6;
        [SerializeField] Color contorno = new Color(43f / 255f, 30f / 255f, 22f / 255f, 1f);
        [SerializeField] float margen = 0.12f;
        [SerializeField] Vector2 anguloBody = new Vector2(20f, 8f);
        [SerializeField] Vector2 anguloCloak = new Vector2(160f, 8f);
        [SerializeField] Vector2 anguloAccessory = new Vector2(25f, 8f);
        readonly List<WardrobeItemSO> items = new List<WardrobeItemSO>();
        Texture2D previsualizacion;
        WardrobeItemSO seleccionado;
        Vector2 desplazamiento;
        Vector2 desplazamientoResumen;
        string resumen = "";

        [MenuItem("El Sendero/Herramientas/Iconos del armario desde 3D")]
        static void Abrir() => GetWindow<IconosDelArmarioWindow>("Iconos del armario");

        void OnEnable()
        {
            minSize = new Vector2(620f, 640f);
            if (modelo == null) modelo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/_WILL_NPC.prefab");
            items.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:WardrobeItemSO", new[] { CarpetaArmario }))
            {
                var item = AssetDatabase.LoadAssetAtPath<WardrobeItemSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null) items.Add(item);
            }
            items.Sort((a, b) => string.CompareOrdinal(a.PartName, b.PartName));
            if (items.Count > 0) seleccionado = items[0];
        }

        void OnDisable() => LimpiarFoto();

        void LimpiarFoto()
        {
            if (previsualizacion != null) DestroyImmediate(previsualizacion);
            previsualizacion = null;
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Fotografía solo la pieza elegida. Generar todos guarda los PNG pintados fuera de Assets y conserva los GUID.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            modelo = (GameObject)EditorGUILayout.ObjectField("Modelo de ejemplo", modelo, typeof(GameObject), false);
            tamano = EditorGUILayout.IntPopup("Tamaño", tamano, new[] { "256", "512", "1024", "2048" }, new[] { 256, 512, 1024, 2048 });
            contorno = EditorGUILayout.ColorField("Color del contorno", contorno);
            grosor = EditorGUILayout.IntSlider("Grosor (píxeles)", grosor, 0, 32);
            margen = EditorGUILayout.Slider("Margen", margen, 0.02f, 0.4f);
            EditorGUILayout.LabelField("Ángulos: giro horizontal / inclinación vertical", EditorStyles.boldLabel);
            anguloBody = EditorGUILayout.Vector2Field("Ropa (Body)", anguloBody);
            anguloCloak = EditorGUILayout.Vector2Field("Capas (Cloak)", anguloCloak);
            anguloAccessory = EditorGUILayout.Vector2Field("Accesorios", anguloAccessory);
            if (EditorGUI.EndChangeCheck()) LimpiarFoto();
            EditorGUILayout.BeginHorizontal();
            desplazamiento = EditorGUILayout.BeginScrollView(desplazamiento, GUILayout.Width(280f), GUILayout.Height(260f));
            foreach (var item in items)
                if (GUILayout.Toggle(seleccionado == item, item.PartName, "Button") && seleccionado != item)
                { seleccionado = item; LimpiarFoto(); }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(seleccionado != null ? seleccionado.PartName : "Sin selección", EditorStyles.boldLabel);
            var rect = GUILayoutUtility.GetRect(220f, 220f, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.35f));
            if (previsualizacion != null) GUI.DrawTexture(rect, previsualizacion, ScaleMode.ScaleToFit, true);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(modelo == null || seleccionado == null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Previsualizar", GUILayout.Height(32f))) Previsualizar();
                if (GUILayout.Button("Generar todos", GUILayout.Height(32f))) GenerarTodos();
                EditorGUILayout.EndHorizontal();
            }
            desplazamientoResumen = EditorGUILayout.BeginScrollView(desplazamientoResumen, GUILayout.MinHeight(70f));
            EditorGUILayout.LabelField(resumen, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        AjustesDeFoto Ajustes(WardrobeItemSO item)
        {
            Vector2 angulo = item.Category == PartCategory.Cloak ? anguloCloak :
                item.Category == PartCategory.Body ? anguloBody : anguloAccessory;
            return new AjustesDeFoto { tamano = tamano, grosorContorno = grosor, colorContorno = contorno,
                margen = margen, yaw = angulo.x, pitch = angulo.y };
        }

        Texture2D Fotografiar(WardrobeItemSO item)
        {
            var piezas = new HashSet<Transform>();
            foreach (var transformacion in modelo.GetComponentsInChildren<Transform>(true))
                if (string.Equals(transformacion.name, item.PartName, StringComparison.Ordinal)) piezas.Add(transformacion);
            if (piezas.Count == 0) throw new PiezaAusenteException(item.PartName);
            return FotografoDeIconos.Fotografiar(modelo, renderer =>
            {
                // Incluye los subrenderers de una pieza compuesta, sin hacer visibles sus hermanos.
                for (var t = renderer.transform; t != null; t = t.parent)
                    if (piezas.Contains(t)) return true;
                return false;
            }, Ajustes(item));
        }

        void Previsualizar()
        {
            LimpiarFoto();
            try { previsualizacion = Fotografiar(seleccionado); resumen = "Previsualización lista; no se ha guardado ningún asset."; }
            catch (Exception ex) { resumen = ex.Message; }
        }

        void GenerarTodos()
        {
            int generados = 0, renombrados = 0, corregidos = 0;
            var errores = new List<string>();
            var ausentes = new List<string>();
            var sprites = new Dictionary<WardrobeItemSO, Sprite>();
            var nombres = new HashSet<string>(StringComparer.Ordinal);
            string archivo = Path.GetFullPath(Path.Combine(Application.dataPath, "../Versiones antiguas/Iconos armario pintados"));
            try
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    EditorUtility.DisplayProgressBar("Iconos del armario", item.PartName, i / (float)items.Count);
                    Texture2D foto = null;
                    try
                    {
                        if (string.IsNullOrWhiteSpace(item.PartName) || Path.GetFileName(item.PartName) != item.PartName)
                            throw new InvalidOperationException("Nombre de pieza inválido.");
                        if (!nombres.Add(item.PartName)) throw new InvalidOperationException("La pieza aparece en más de un WardrobeItemSO.");
                        string destino = CarpetaIconos + item.PartName + ".png";
                        string origen = ResolverOrigen(item, destino);
                        if (!File.Exists(origen)) throw new FileNotFoundException("No existe el PNG de la pieza: " + origen);
                        foto = Fotografiar(item);
                        // Conserva el primer original también cuando se vuelve a generar con otros ajustes.
                        Directory.CreateDirectory(archivo);
                        string copia = Path.Combine(archivo, Path.GetFileName(origen));
                        string aliasArchivado = item.PartName == "AC04_AngelEars" ? "orejas de angel.png" :
                            item.PartName == "AC04_BunnyEars" ? "BunnyEars.png" : Path.GetFileName(origen);
                        if (!File.Exists(copia) && !File.Exists(Path.Combine(archivo, aliasArchivado))) File.Copy(origen, copia, false);
                        if (origen != destino)
                        {
                            string error = AssetDatabase.MoveAsset(origen, destino);
                            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                            renombrados++;
                        }
                        FotografoDeIconos.GuardarPng(foto, destino);
                        AssetDatabase.ImportAsset(destino, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                        ConfigurarImportador(destino);
                        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destino);
                        if (sprite == null) throw new InvalidOperationException("El PNG no se ha importado como Sprite.");
                        if (AsignarIcono(item, sprite)) corregidos++;
                        sprites.Add(item, sprite);
                        generados++;
                    }
                    catch (PiezaAusenteException) { ausentes.Add(item.PartName); }
                    catch (Exception ex) { errores.Add(item.PartName + ": " + ex.Message); }
                    finally { if (foto != null) DestroyImmediate(foto); }
                }
                foreach (var guid in AssetDatabase.FindAssets("t:ItemData", new[] { CarpetaInventario }))
                {
                    var ruta = AssetDatabase.GUIDToAssetPath(guid);
                    if (!Path.GetFileName(ruta).StartsWith("IT_W_", StringComparison.Ordinal)) continue;
                    try
                    {
                        var item = AssetDatabase.LoadAssetAtPath<ItemData>(ruta);
                        // La referencia al desbloqueo es la autoridad; el nombre permite detectar relaciones cruzadas.
                        string pieza = Path.GetFileNameWithoutExtension(ruta).Substring("IT_W_".Length);
                        var esperado = items.Find(w => string.Equals(w.PartName, pieza, StringComparison.Ordinal));
                        if (esperado == null) throw new InvalidOperationException("No hay una pieza de armario con ese nombre.");
                        if (item.wardrobeUnlock != esperado)
                            throw new InvalidOperationException("wardrobeUnlock no corresponde al nombre del item; no se cambia el desbloqueo automáticamente.");
                        if (!sprites.TryGetValue(esperado, out var sprite))
                            throw new InvalidOperationException("La fotografía de su pieza no se ha generado.");
                        if (AsignarIcono(item, sprite)) corregidos++;
                    }
                    catch (Exception ex) { errores.Add(Path.GetFileName(ruta) + ": " + ex.Message); }
                }
                AssetDatabase.SaveAssets();
            }
            catch (Exception ex) { errores.Add(ex.Message); }
            finally { EditorUtility.ClearProgressBar(); }
            resumen = $"Generados: {generados}/{items.Count}. Renombrados: {renombrados}. Referencias corregidas: {corregidos}. Errores: {errores.Count}. Piezas ausentes: {ausentes.Count}.";
            if (ausentes.Count > 0) resumen += "\nPiezas que no existen en el modelo: " + string.Join(", ", ausentes);
            if (errores.Count > 0) resumen += "\n" + string.Join("\n", errores);
            EditorUtility.DisplayDialog("Iconos del armario", resumen, "Aceptar");
            LimpiarFoto();
        }

        static string ResolverOrigen(WardrobeItemSO item, string destino)
        {
            string actual = AssetDatabase.GetAssetPath(item.Icon);
            if (File.Exists(destino)) return destino;
            string alias = item.PartName == "AC04_AngelEars" ? "orejas de angel.png" :
                item.PartName == "AC04_BunnyEars" ? "BunnyEars.png" : null;
            if (alias != null && File.Exists(CarpetaIconos + alias)) return CarpetaIconos + alias;
            throw new InvalidOperationException("No se encuentra el PNG que corresponde a la pieza. Referencia actual: " + actual);
        }

        static bool AsignarIcono(UnityEngine.Object item, Sprite sprite)
        {
            var serializado = new SerializedObject(item);
            var propiedad = serializado.FindProperty("icon");
            if (propiedad.objectReferenceValue == sprite) return false;
            propiedad.objectReferenceValue = sprite;
            serializado.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return true;
        }

        static void ConfigurarImportador(string ruta)
        {
            var importador = (TextureImporter)AssetImporter.GetAtPath(ruta);
            importador.textureType = TextureImporterType.Sprite;
            importador.spriteImportMode = SpriteImportMode.Single;
            importador.alphaSource = TextureImporterAlphaSource.FromInput;
            importador.alphaIsTransparency = true;
            importador.mipmapEnabled = false;
            importador.sRGBTexture = true;
            importador.maxTextureSize = Mathf.Max(512, importador.maxTextureSize,
                AssetDatabase.LoadAssetAtPath<Texture2D>(ruta).width);
            importador.textureCompression = TextureImporterCompression.CompressedHQ;
            importador.compressionQuality = 100;
            importador.filterMode = FilterMode.Bilinear;
            foreach (string plataforma in new[] { "Standalone", "Android", "iPhone", "WebGL" })
            {
                var ajustes = importador.GetPlatformTextureSettings(plataforma);
                if (!ajustes.overridden) continue;
                ajustes.maxTextureSize = Mathf.Max(importador.maxTextureSize, ajustes.maxTextureSize);
                ajustes.format = TextureImporterFormat.Automatic;
                ajustes.textureCompression = TextureImporterCompression.CompressedHQ;
                ajustes.compressionQuality = 100;
                ajustes.allowsAlphaSplitting = false;
                importador.SetPlatformTextureSettings(ajustes);
            }
            importador.SaveAndReimport();
        }

        sealed class PiezaAusenteException : Exception
        {
            public PiezaAusenteException(string pieza) : base("La pieza no existe en el modelo: " + pieza) { }
        }
    }
}
