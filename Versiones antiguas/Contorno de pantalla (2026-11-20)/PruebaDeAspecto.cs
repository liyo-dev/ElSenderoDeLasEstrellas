#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Prueba de un solo uso (INC-1325): variantes del contorno y del postprocesado de cinemática para
/// hacer fotos comparativas en Play. «… como está» deja los valores que había al empezar.
public static class PruebaDeAspecto
{
    static VolumeProfile Perfil(string ruta) => AssetDatabase.LoadAssetAtPath<VolumeProfile>(ruta);
    static T De<T>(string ruta) where T : VolumeComponent => Perfil(ruta)?.components.OfType<T>().FirstOrDefault();

    static void Contorno(float grosor, float pliegue, float peso, float suavidad)
    {
        var c = De<ContornoDeTinta>("Assets/DefaultVolumeProfile.asset");
        c.grosor.Override(grosor); c.umbralDePliegue.Override(pliegue); c.pesoDePliegues.Override(peso); c.suavidadDelTrazo.Override(suavidad);
        OrdenesDelEditor.Avisar("Prueba", $"contorno grosor {grosor} pliegue {pliegue}×{peso} suavidad {suavidad}");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: contorno C (elegido)")] static void C() => Contorno(3f, 0.6f, 0.8f, 0.25f);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: contorno D (pliegues solo en esquinas vivas)")] static void D() => Contorno(3f, 0.85f, 0.6f, 0.25f);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: contorno E (solo siluetas)")] static void E() => Contorno(3f, 0.85f, 0f, 0.25f);

    static void CapasYIntensidad(float capas, float intensidad)
    {
        var c = De<ContornoDeTinta>("Assets/DefaultVolumeProfile.asset");
        c.pesoDeCapas.Override(capas); c.intensidad.Override(intensidad);
        OrdenesDelEditor.Avisar("Prueba", $"contorno: capas {capas}, intensidad {intensidad}");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: capas como están")] static void Capas0() => CapasYIntensidad(0.55f, 1f);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: capas apagadas")] static void Capas1() => CapasYIntensidad(0f, 1f);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: sin contorno")] static void SinContorno() => CapasYIntensidad(0.55f, 0f);

    // Apaga o enciende las sombras de las luces direccionales de las escenas abiertas (sin guardar).
    static LightShadows _sombrasPrevias = LightShadows.Soft;
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: sombras del sol (sí/no)")]
    static void SombrasDelSol()
    {
        string estado = "";
        foreach (var luz in Object.FindObjectsByType<Light>())
        {
            if (luz.type != LightType.Directional) continue;
            if (luz.shadows != LightShadows.None) { _sombrasPrevias = luz.shadows; luz.shadows = LightShadows.None; }
            else luz.shadows = _sombrasPrevias;
            estado += $"{luz.name}: {luz.shadows}. ";
        }
        OrdenesDelEditor.Avisar("Prueba", estado.Length > 0 ? estado : "no hay sol en las escenas abiertas");
    }

    // Contorno de prueba como volumen de la escena (no se guarda): la cámara de fotos lo lee siempre.
    static void ContornoEnEscena(float intensidad, float capas, float pliegues) => ContornoEnEscena(intensidad, capas, pliegues, -1f, -1f);
    static void ContornoEnEscena(float intensidad, float capas, float pliegues, float grosor, float suavidad)
    {
        var hay = GameObject.Find("[Prueba de contorno]");
        if (hay != null) Object.DestroyImmediate(hay);
        if (intensidad < 0f) { OrdenesDelEditor.Avisar("Prueba", "contorno de prueba quitado"); return; }
        var go = new GameObject("[Prueba de contorno]") { hideFlags = HideFlags.DontSave };
        var v = go.AddComponent<Volume>();
        v.isGlobal = true; v.priority = 600; v.weight = 1f;
        var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        perfil.hideFlags = HideFlags.DontSave;
        var c = perfil.Add<ContornoDeTinta>(false);
        c.intensidad.Override(intensidad); c.pesoDeCapas.Override(capas); c.pesoDePliegues.Override(pliegues);
        if (grosor > 0f) c.grosor.Override(grosor);
        if (suavidad > 0f) c.suavidadDelTrazo.Override(suavidad);
        v.sharedProfile = perfil;
        OrdenesDelEditor.Avisar("Prueba", $"contorno de prueba: intensidad {intensidad}, capas {capas}, pliegues {pliegues}");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: escena sin contorno")] static void EscenaSinContorno() => ContornoEnEscena(0f, 0.55f, 0.8f);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: escena sin capas")] static void EscenaSinCapas() => ContornoEnEscena(1f, 0f, 0.8f);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: escena solo siluetas")] static void EscenaSoloSiluetas() => ContornoEnEscena(1f, 0f, 0f);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: escena trazo fino y neto")] static void EscenaTrazoFino() => ContornoEnEscena(1f, 0.55f, 0.8f, 2f, 0.1f);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: escena trazo muy fino y neto")] static void EscenaTrazoMuyFino() => ContornoEnEscena(1f, 0.55f, 0.8f, 1f, 0.1f);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: escena tinta negra neta")]
    static void EscenaTintaNegra()
    {
        ContornoEnEscena(1f, 0f, 0.8f, 2f, 0.1f);
        var c = GameObject.Find("[Prueba de contorno]").GetComponent<Volume>().sharedProfile.components.OfType<ContornoDeTinta>().First();
        c.tinta.Override(new Color(0.05f, 0.04f, 0.06f, 1f));
        OrdenesDelEditor.Avisar("Prueba", "tinta negra neta, grosor 2");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: quitar contorno de prueba")] static void QuitarContornoDePrueba() => ContornoEnEscena(-1f, 0f, 0f);

    // Cara del material Tree del pack: doble (0) o solo delante (2). En memoria; «doble» lo deja como estaba.
    static void CaraDelArbol(float cull)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/World/Fantasy_Kingdom_Pack/Materials/Tree.mat");
        m.SetFloat("_Cull", cull);
        OrdenesDelEditor.Avisar("Prueba", $"Tree.mat: cull {cull}");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: árbol a una cara")] static void ArbolUnaCara() => CaraDelArbol(2f);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: árbol a doble cara")] static void ArbolDobleCara() => CaraDelArbol(0f);

    // Recorte por alfa del material Tree del pack (en memoria).
    static void RecorteDelArbol(bool si)
    {
        foreach (var ruta in new[] { "Assets/Art/World/Fantasy_Kingdom_Pack/Materials/Tree.mat", "Assets/Scenes/Worlds/MainWorld_data/Recursos/Follaje_0.mat" })
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (si) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
            m.SetFloat("_AlphaClip", si ? 1f : 0f);
        }
        OrdenesDelEditor.Avisar("Prueba", $"Tree.mat: recorte {si}");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: árbol sin recorte")] static void ArbolSinRecorte() => RecorteDelArbol(false);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: árbol con recorte")] static void ArbolConRecorte() => RecorteDelArbol(true);

    // Cambia el material Tree de los árboles cercanos a (88, 6) por un URP/Lit en memoria; «devolver» lo deshace.
    static readonly System.Collections.Generic.Dictionary<Renderer, Material[]> _cambiados = new();
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: árboles cercanos con Lit")]
    static void ArbolesConLit()
    {
        var tree = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/World/Fantasy_Kingdom_Pack/Materials/Tree.mat");
        var lit = new Material(Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = HideFlags.DontSave };
        lit.SetTexture("_BaseMap", tree.GetTexture("_BaseMap"));
        int n = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>())
        {
            var b = r.bounds.center;
            if ((new Vector2(b.x, b.z) - new Vector2(88f, 6f)).sqrMagnitude > 900f) continue;
            var mats = r.sharedMaterials;
            bool usa = false;
            for (int i = 0; i < mats.Length; i++) if (mats[i] != null && mats[i].shader.name == "El Sendero/Dibujo") { usa = true; }
            if (!usa) continue;
            _cambiados[r] = mats;
            var nuevos = (Material[])mats.Clone();
            for (int i = 0; i < nuevos.Length; i++)
                if (nuevos[i] != null && nuevos[i].shader.name == "El Sendero/Dibujo")
                {
                    var copia = new Material(lit) { hideFlags = HideFlags.DontSave };
                    copia.SetTexture("_BaseMap", nuevos[i].GetTexture("_BaseMap"));
                    if (nuevos[i].IsKeywordEnabled("_ALPHATEST_ON")) { copia.EnableKeyword("_ALPHATEST_ON"); copia.SetFloat("_AlphaClip", 1f); copia.SetFloat("_Cutoff", 0.5f); copia.renderQueue = 2450; }
                    copia.SetFloat("_Cull", nuevos[i].GetFloat("_Cull"));
                    nuevos[i] = copia;
                }
            r.sharedMaterials = nuevos; n++;
        }
        OrdenesDelEditor.Avisar("Prueba", $"{n} árboles con Lit");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: devolver los árboles")]
    static void DevolverArboles()
    {
        foreach (var par in _cambiados) if (par.Key != null) par.Key.sharedMaterials = par.Value;
        OrdenesDelEditor.Avisar("Prueba", $"{_cambiados.Count} árboles devueltos");
        _cambiados.Clear();
    }

    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: materiales cerca de (88, 6)")]
    static void MaterialesCerca()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var r in Object.FindObjectsByType<Renderer>())
        {
            var b = r.bounds;
            var txt = System.IO.File.Exists("Guiones/_ordenes/centro.txt") ? System.IO.File.ReadAllText("Guiones/_ordenes/centro.txt").Split(' ') : new[] { "88", "6", "8" };
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            float cx = float.Parse(txt[0], ci), cz = float.Parse(txt[1], ci), rad = float.Parse(txt[2], ci);
            if (b.SqrDistance(new Vector3(cx, b.center.y, cz)) > rad * rad) continue;
            if (r is ParticleSystemRenderer) continue;
            var mf = r.GetComponent<MeshFilter>();
            sb.Append($"\n    {r.name} [{r.GetType().Name}] malla={(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "-")} on={r.enabled} lod={(r.GetComponentInParent<LODGroup>() != null)} sombras={r.shadowCastingMode} b={b.center:F1}/{b.size:F1}: ");
            foreach (var m in r.sharedMaterials) sb.Append(m == null ? "null; " : $"{m.name} ({m.shader.name}, cola {m.renderQueue}, {string.Join(",", m.shaderKeywords)}); ");
        }
        System.IO.File.WriteAllText("Guiones/_ordenes/materiales_cerca.txt", sb.ToString());
        OrdenesDelEditor.Avisar("Prueba", "materiales en Guiones/_ordenes/materiales_cerca.txt");
    }

    // Will de prueba en la plaza de la Aldea del Mar (no se guarda) para fotos de cerca.
    const string NombreDelWill = "[Prueba: Will]";
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: poner a Will")]
    static void PonerAWill()
    {
        var hay = GameObject.Find(NombreDelWill);
        if (hay != null) Object.DestroyImmediate(hay);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/_WILL.prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, UnityEngine.SceneManagement.SceneManager.GetSceneByName("MainWorld"));
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = NombreDelWill;
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetPositionAndRotation(new Vector3(275f, 4.96f, -410f), Quaternion.Euler(0f, 0f, 0f));
        InkOutlined_EnEditor(go);
        var b = new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) if (r.enabled && r.gameObject.activeInHierarchy) b.Encapsulate(r.bounds);
        var mats = new System.Text.StringBuilder();
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            foreach (var m in r.sharedMaterials)
                if (m != null) mats.Append($"{r.name}:{m.name}:{(m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap")) : "-")}; ");
        }
        System.IO.File.WriteAllText("Guiones/_ordenes/will_de_prueba.txt", $"caja {b.center:F2} {b.size:F2}\n{mats}");
        OrdenesDelEditor.Avisar("Prueba", $"Will puesto; caja {b.center:F2} {b.size:F2}");
    }
    // Línea de funda en el Will de prueba: una copia de cada malla con el material de línea en todas sus partes.
    static void LineaEnWill(float grosor)
    {
        var will = GameObject.Find(NombreDelWill);
        if (will == null) { OrdenesDelEditor.Avisar("Prueba", "no hay Will de prueba"); return; }
        foreach (var viejo in will.GetComponentsInChildren<Transform>(true)) if (viejo != null && viejo.name == "[Línea]") Object.DestroyImmediate(viejo.gameObject);
        if (grosor <= 0f) { OrdenesDelEditor.Avisar("Prueba", "Will sin línea de funda"); return; }
        var mat = new Material(Shader.Find("El Sendero/Línea de personaje")) { hideFlags = HideFlags.DontSave };
        mat.SetFloat("_GrosorDeLinea", grosor);
        int n = 0;
        foreach (var r in will.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || r.sharedMesh == null) continue;
            var go = new GameObject("[Línea]") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(r.transform, false);
            var copia = go.AddComponent<SkinnedMeshRenderer>();
            copia.sharedMesh = r.sharedMesh; copia.bones = r.bones; copia.rootBone = r.rootBone;
            copia.localBounds = r.localBounds;
            var mats = new Material[r.sharedMesh.subMeshCount];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            copia.sharedMaterials = mats;
            copia.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            n++;
        }
        OrdenesDelEditor.Avisar("Prueba", $"línea de funda ({grosor} px) en {n} mallas");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: Will con línea de funda 2")] static void LineaWill2() => LineaEnWill(2f);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: Will con línea de funda 3")] static void LineaWill3() => LineaEnWill(3f);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: Will sin línea de funda")] static void LineaWill0() => LineaEnWill(0f);

    // En el editor (sin Play) el enchufe del juego no ha corrido: se pone a mano para la foto.
    static void InkOutlined_EnEditor(GameObject go)
    {
        Kits.InkOutline.InkOutlined.Excluded = LineaDeTintaDelJuego.EsPiezaDeLaCara;
        var marca = go.GetComponent<Kits.InkOutline.InkOutlined>();
        if (marca == null) marca = go.AddComponent<Kits.InkOutline.InkOutlined>();
        marca.Refresh();
        if (!Kits.InkOutline.InkOutlined.Active.Contains(marca)) Kits.InkOutline.InkOutlined.Active.Add(marca);
    }

    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: quitar a Will")]
    static void QuitarAWill()
    {
        var hay = GameObject.Find(NombreDelWill);
        if (hay != null) Object.DestroyImmediate(hay);
        Kits.InkOutline.InkOutlined.Active.RemoveAll(m => m == null);
        OrdenesDelEditor.Avisar("Prueba", "Will de prueba quitado");
    }

    // Paletas de los personajes sin mezcla (filtro punto, sin mipmaps); «como estaban» lo deshace.
    static readonly string[] Paletas = { "Assets/Art/Characters/RPG Tiny Hero Duo/Texture/Albedo.png", "Assets/Art/Characters/RPG Tiny Hero Duo/Texture/Albedo_Liam.png" };
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: paletas sin mezcla")] static void PaletasSinMezcla() => Paleta(FilterMode.Point, false);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: paletas como estaban")] static void PaletasComoEstaban() => Paleta(FilterMode.Trilinear, true);
    static void Paleta(FilterMode filtro, bool mips)
    {
        foreach (var ruta in Paletas)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
            imp.filterMode = filtro; imp.mipmapEnabled = mips;
            imp.SaveAndReimport();
        }
        OrdenesDelEditor.Avisar("Prueba", $"paletas: {filtro}, mipmaps {mips}");
    }

    // Contorno de pantalla del perfil general: 0 = el mundo sin contorno (los personajes llevan la funda), 1 = como antes.
    static void ContornoDelMundo(float intensidad)
    {
        var perfil = Perfil("Assets/DefaultVolumeProfile.asset");
        var c = perfil.components.OfType<ContornoDeTinta>().First();
        c.intensidad.Override(intensidad);
        EditorUtility.SetDirty(c); EditorUtility.SetDirty(perfil);
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Aplicado", $"contorno de pantalla del mundo: intensidad {intensidad}");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Aplicar: mundo sin contorno de pantalla")] static void MundoSinContorno() => ContornoDelMundo(0f);
    [MenuItem("El Sendero/Aspecto de dibujo/Aplicar: mundo con contorno de pantalla (como antes)")] static void MundoConContorno() => ContornoDelMundo(1f);

    // Suavizado de bordes del pipeline (MSAA). La línea de tinta es geometría y lo aprovecha.
    static void Msaa(int muestras)
    {
        var asset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        asset.msaaSampleCount = muestras;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Aplicado", $"MSAA x{asset.msaaSampleCount}");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Aplicar: suavizado de bordes MSAA x4")] static void Msaa4() => Msaa(4);
    [MenuItem("El Sendero/Aspecto de dibujo/Aplicar: sin suavizado de bordes (como antes)")] static void Msaa1() => Msaa(1);

    // Ajustes del kit de línea para El Sendero: los modelos de personajes llevan normales suavizadas.
    [MenuItem("El Sendero/Aspecto de dibujo/Aplicar: normales suavizadas de la línea en los personajes")]
    static void NormalesDeLaLinea()
    {
        const string ruta = "Assets/ElSendero/Data/InkOutlineSettings.asset";
        var ajustes = AssetDatabase.LoadAssetAtPath<Kits.InkOutline.InkOutlineSettings>(ruta);
        if (ajustes == null)
        {
            ajustes = ScriptableObject.CreateInstance<Kits.InkOutline.InkOutlineSettings>();
            AssetDatabase.CreateAsset(ajustes, ruta);
        }
        var so = new SerializedObject(ajustes);
        var carpetas = so.FindProperty("modelFolders");
        carpetas.arraySize = 1;
        carpetas.GetArrayElementAtIndex(0).stringValue = "Assets/Art/Characters";
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ajustes);
        AssetDatabase.SaveAssets();
        Kits.InkOutline.Editor.InkOutlineSmoothNormals.ReimportAll();
        OrdenesDelEditor.Avisar("Aplicado", "normales suavizadas de la línea en Assets/Art/Characters");
    }

    // Ajustes de la línea de tinta en los renderers del pipeline.
    [MenuItem("El Sendero/Aspecto de dibujo/Aplicar: línea de tinta 2,5 px marrón, se afina de 8 a 25 m")]
    static void AjustesDeLaLinea()
    {
        var asset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        var lista = new SerializedObject(asset).FindProperty("m_RendererDataList");
        int n = 0;
        for (int i = 0; i < lista.arraySize; i++)
        {
            var datos = lista.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
            if (datos == null) continue;
            foreach (var f in datos.rendererFeatures.OfType<Kits.InkOutline.InkOutlineFeature>())
            {
                var so = new SerializedObject(f);
                so.FindProperty("widthPixels").floatValue = 2.5f;
                so.FindProperty("thinFrom").floatValue = 8f;
                so.FindProperty("thinTo").floatValue = 25f;
                so.FindProperty("farWidthFraction").floatValue = 0.4f;
                so.FindProperty("color").colorValue = new Color(0.30f, 0.18f, 0.13f, 1f);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(f); EditorUtility.SetDirty(datos);
                n++;
            }
        }
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Aplicado", $"línea de tinta ajustada en {n} renderer(s)");
    }

    // Los personajes no reciben sombras proyectadas (temblaban en la cara); las siguen proyectando.
    static void SombrasEnPersonajes(bool reciben)
    {
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Art/Characters" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null || m.shader == null || m.shader.name != "El Sendero/Dibujo") continue;
            if (reciben) m.DisableKeyword("_RECEIVE_SHADOWS_OFF"); else m.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            m.SetFloat("_ReceiveShadows", reciben ? 1f : 0f);
            EditorUtility.SetDirty(m); n++;
        }
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Aplicado", $"{n} materiales de personajes: reciben sombras {reciben}");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Aplicar: personajes sin sombras recibidas")] static void PersonajesSinSombras() => SombrasEnPersonajes(false);
    [MenuItem("El Sendero/Aspecto de dibujo/Aplicar: personajes con sombras recibidas (como antes)")] static void PersonajesConSombras() => SombrasEnPersonajes(true);

    // Luz de personaje en los materiales de dibujo de los personajes.
    static void LuzDePersonaje(float valor)
    {
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Art/Characters" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null || m.shader == null || m.shader.name != "El Sendero/Dibujo") continue;
            m.SetFloat("_LuzDePersonaje", valor); EditorUtility.SetDirty(m); n++;
        }
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Aplicado", $"luz de personaje {valor} en {n} materiales");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Aplicar: luz de personaje")] static void LuzDePersonaje1() => LuzDePersonaje(1f);
    [MenuItem("El Sendero/Aspecto de dibujo/Aplicar: sin luz de personaje (como antes)")] static void LuzDePersonaje0() => LuzDePersonaje(0f);

    // Piezas de la cara pegadas al contorno: el cuerpo de los personajes escribe la marca 2 (stencil) y las
    // piezas de la cara (Mat_Caras_*, Mat_Ceja*) solo se pintan donde está, después del cuerpo.
    static void CaraDentroDelContorno(bool si)
    {
        int cuerpo = 0, cara = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Art/Characters" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null || m.shader == null || m.shader.name != "El Sendero/Dibujo") continue;
            bool esCara = m.name.StartsWith("Mat_Caras_") || m.name.StartsWith("Mat_Ceja");
            if (!si) { m.SetInt("_StencilRef", 0); m.SetFloat("_StencilComp", 8f); m.SetFloat("_StencilPass", 0f); if (esCara) m.renderQueue = -1; }
            else if (esCara) { m.SetInt("_StencilRef", 2); m.SetFloat("_StencilComp", 3f); m.SetFloat("_StencilPass", 0f); m.renderQueue = m.IsKeywordEnabled("_ALPHATEST_ON") ? 2460 : 2010; cara++; }
            else { m.SetInt("_StencilRef", 2); m.SetFloat("_StencilComp", 8f); m.SetFloat("_StencilPass", 2f); cuerpo++; }
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Aplicado", si ? $"cara dentro del contorno: {cuerpo} de cuerpo, {cara} de cara" : "cara dentro del contorno: quitado");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Aplicar: piezas de la cara dentro del contorno")] static void CaraDentro() => CaraDentroDelContorno(true);
    [MenuItem("El Sendero/Aspecto de dibujo/Aplicar: piezas de la cara como antes")] static void CaraComoAntes() => CaraDentroDelContorno(false);

    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: línea de tinta encendida (sí/no)")]
    static void LineaSiNo()
    {
        var asset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        var lista = new SerializedObject(asset).FindProperty("m_RendererDataList");
        string estado = "";
        for (int i = 0; i < lista.arraySize; i++)
        {
            var datos = lista.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
            if (datos == null) continue;
            foreach (var f in datos.rendererFeatures.OfType<Kits.InkOutline.InkOutlineFeature>()) { f.SetActive(!f.isActive); estado = f.isActive.ToString(); EditorUtility.SetDirty(datos); }
        }
        OrdenesDelEditor.Avisar("Prueba", "línea de tinta activa: " + estado + "; personajes apuntados: " + Kits.InkOutline.InkOutlined.Active.Count);
    }

    // Pelo de Will como una sola masa: el tono claro de los picos se acerca al del pelo (queda un 30 % de la diferencia).
    static void TonoClaroDelPelo(Color tinte)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Characters/Protagonistas/Will/Materiales/Mat_Hair16_Heroe_PeloClaro.mat");
        m.SetColor("_Tinte", tinte); EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Aplicado", $"tono claro del pelo de Will: {tinte}");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Aplicar: pelo de Will de una sola masa")] static void PeloDeUnaMasa() => TonoClaroDelPelo(new Color(0.89f, 0.81f, 0.70f, 1f));
    [MenuItem("El Sendero/Aspecto de dibujo/Aplicar: pelo de Will con los picos claros (como antes)")] static void PeloComoAntes() => TonoClaroDelPelo(Color.white);

    const string Cine = "Assets/Resources/Postproceso/PerfilDeCinematica.asset";
    static void Cinematica(float grano, float rojo, float bloom, float umbral)
    {
        De<FilmGrain>(Cine).intensity.Override(grano);
        De<ChannelMixer>(Cine).redOutRedIn.Override(rojo);
        var b = De<Bloom>(Cine); b.intensity.Override(bloom); b.threshold.Override(umbral);
        OrdenesDelEditor.Avisar("Prueba", $"cine: grano {grano}, rojo {rojo}, bloom {bloom} desde {umbral}");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: cine como está")] static void Cine0() => Cinematica(0.387f, 80f, 1.5f, 0.8f);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: cine suave")] static void Cine1() => Cinematica(0.12f, 92f, 0.8f, 1f);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: cine limpio")] static void Cine2() => Cinematica(0f, 100f, 0.5f, 1f);

    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: perfil de cine en la escena (sí/no)")]
    static void CineEnLaEscena()
    {
        var hay = GameObject.Find("[Prueba de cine]");
        if (hay != null) { Object.DestroyImmediate(hay); OrdenesDelEditor.Avisar("Prueba", "perfil de cine quitado"); return; }
        var go = new GameObject("[Prueba de cine]") { hideFlags = HideFlags.DontSave };
        var v = go.AddComponent<Volume>();
        v.isGlobal = true; v.priority = 500; v.weight = 1f; v.sharedProfile = Perfil(Cine);
        OrdenesDelEditor.Avisar("Prueba", "perfil de cine puesto");
    }
}
#endif
