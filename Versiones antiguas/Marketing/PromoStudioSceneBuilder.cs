using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Construye la escena de promo en un camino del bosque, coloca los tres protagonistas y prepara
/// sus cámaras. El montaje se convierte a SequencePlayer y SequenceDefinition con el migrador
/// oficial al finalizar.
///
/// Los personajes se instancian en modo visual para que no registren sistemas de jugador al cargar
/// Start.unity. El encuadre se calcula con la geometría real de sus modelos.
/// </summary>
public static class PromoStudioSceneBuilder
{
    const string SceneFolder = "Assets/Scenes/Marketing";
    const string ScenePath = SceneFolder + "/PromoEstudio.unity";

    const string EstelaPrefabPath = "Assets/Prefabs/_ESTELA.prefab";
    const string LiamPrefabPath = "Assets/Prefabs/_LIAM.prefab";
    const string WillPrefabPath = "Assets/Prefabs/_WILL.prefab";

    const string BackdropContainerName = "Fondo_Estudio";
    const string LightingContainerName = "Iluminacion_Estudio";
    const string CameraGoName = "Camara_Estudio";
    const string CharactersContainerName = "Personajes_Estudio";

    // ── Rig de cinemática (vídeo #1) ───────────────────────────────────────────────────────────
    const string CinematicsContainerName = "Cinematica_SenderoPromo";
    const string CameraDriverGoName = "CinematicCameraDriver";
    const string ShotsContainerName = "Planos_Camara";
    const string GroupMarksContainerName = "Marcas_Plano_Grupo";

    const string ShotEstelaSoloName = "Plano_01_Estela_Solo";
    const string ShotEstelaLiamName = "Plano_02_Medio_Estela_Liam";
    const string ShotWillRevealName = "Plano_03_Revelacion_Will";
    const string ShotGroupName = "Plano_04_Grupo_Final";

    const string MarkEstelaName = "Marca_Estela";
    const string MarkLiamName = "Marca_Liam";
    const string MarkWillName = "Marca_Will";

    const string VistaBosquePath = "Assets/Scenes/Worlds/MainWorld_data/Vista_bosque.png";
    const string MaterialSenderoPath = SceneFolder + "/PromoSenderoBackdrop.mat";
    static readonly Color GroundColor = new Color(0.25f, 0.36f, 0.19f);
    static readonly Color PathColor = new Color(0.34f, 0.25f, 0.14f);

    // ── Cámara: fija, plano medio/americano, centrada en el grupo ──────────────────────────────
    static readonly Vector3 CameraPosition = new Vector3(0f, 1.55f, -3.6f);
    // Altura pecho, ligeramente por delante de Estela hacia el resto del grupo — punto de partida
    // razonable para encuadrar de pie a los 3 personajes; el encuadre fino de cada parte del guion
    // (planos más cerrados, giros, etc.) lo hace Raúl a mano por encima de este punto de partida.
    static readonly Vector3 CameraLookAtPoint = new Vector3(0f, 1.15f, 0.5f);
    const float CameraFieldOfView = 34f;

    // ── Blocking de personajes (posiciones/orientaciones de partida del guion, ver cabecera) ───
    // Estela ocupa el centro del grupo.
    static readonly Vector3 EstelaPosition = Vector3.zero;
    // Liam queda a la izquierda del grupo.
    static readonly Vector3 LiamStartPosition = new Vector3(-1.05f, 0f, 0.25f);
    // Will queda a la derecha del grupo, sobre el camino.
    static readonly Vector3 WillPosition = new Vector3(1.05f, 0f, 0.25f);

    // ── Planos de cámara del vídeo #1 ──────────────────────────────────────────────────────────
    //
    // ⚠ IMPORTANTE (cambio de esta pasada): las constantes Pos/LookAt de los 4 planos YA NO SE USAN
    // COMO POSICIÓN FINAL. Dos rondas de ajuste "a ojo" con posiciones/FOV fijos salieron mal (el
    // plano 01 acabó tan cerca que solo se veía la parte de arriba de la cabeza de Estela, con las
    // coletas cortadas por los bordes) porque asumían proporciones de humano adulto (~1.7 m, cabeza
    // ~1.5 m) y estos personajes son un modelo estilizado tipo chibi (RPG Tiny Hero Duo): cabeza
    // enorme, cuerpo pequeño. Cualquier constante en metros calculada a ojo contra ese supuesto está
    // rota de partida.
    //
    // Ahora el encuadre se CALCULA en tiempo de ejecución de la herramienta a partir de la geometría
    // REAL del personaje ya instanciado y colocado en su marca (ver MedirPersonaje/CalcularEncuadre
    // más abajo): se miden bounds de renderers + huesos del avatar Humanoid, y se despeja la
    // distancia de cámara con la fórmula de cámara en perspectiva.
    //
    // De estas constantes se sigue usando:
    //   · La DIRECCIÓN cámara→sujeto en el plano XZ (el ángulo artístico de cada plano: frontal,
    //     3/4, lateral...), que se conserva tal cual — solo se recalcula la DISTANCIA y la ALTURA.
    //   · El FOV vertical (todos entre 30° y 40°, ver más abajo).
    //   · El valor completo como FALLBACK si la medición falla (personaje ausente, sin renderers...).
    // El ajuste fino final se sigue pudiendo hacer a ojo en la Scene View moviendo estos GameObjects.

    // Parte 1 — plano medio de Estela, ligeramente descentrado para que no quede plano.
    //
    // AJUSTE (esta pasada): estaba en (0.55, 1.45, -2.15) con FOV 30°, es decir, apenas 2.2 m de
    // distancia y un objetivo bastante telefoto — un plano medio corto, casi primer plano. Raúl lo
    // vio DEMASIADO CERCA para el plano de presentación del vídeo, así que se abre:
    //   · La cámara retrocede a z = -3.35 (≈3.45 m reales hasta el punto de mira, +55% de distancia).
    //   · El FOV sube de 30° a 35°: plano medio más abierto, sin llegar a gran angular (y en línea
    //     con los otros 3 planos de esta escena, que ya van a 32-36°, ver más abajo).
    //   · La X sube de 0.55 a 0.85 para CONSERVAR el mismo ángulo de 3/4 (~14° fuera del eje) que
    //     tenía a la distancia anterior: dejándola en 0.55, al alejarse el plano se habría vuelto
    //     casi frontal y habría perdido justo lo que buscaba ese descentrado.
    //   · La altura de mira baja de 1.35 a 1.25 (pecho) para recentrar el encuadre vertical con la
    //     nueva distancia, y la cámara sube a 1.50 para quedarse casi a la altura de los ojos.
    // Sigue siendo un punto de partida: el ajuste fino de encuadre se hace a ojo en la Scene View.
    static readonly Vector3 ShotEstelaSoloPos = new Vector3(0.85f, 1.5f, -3.35f);
    static readonly Vector3 ShotEstelaSoloLookAt = new Vector3(0f, 1.25f, 0f);
    const float ShotEstelaSoloFov = 35f;

    // Parte 2 — plano medio con Estela y Liam los dos en cuadro (y Will fuera).
    static readonly Vector3 ShotEstelaLiamPos = new Vector3(-1.4f, 1.5f, -4.9f);
    static readonly Vector3 ShotEstelaLiamLookAt = new Vector3(-1.35f, 1.15f, -0.15f);
    const float ShotEstelaLiamFov = 36f;

    // Parte 2 — destino del whip-pan: Will de cuerpo entero, con el resto del grupo fuera de cuadro.
    static readonly Vector3 ShotWillRevealPos = new Vector3(1.45f, 1.5f, -3.45f);
    static readonly Vector3 ShotWillRevealLookAt = new Vector3(1.05f, 1.15f, 0.25f);
    const float ShotWillRevealFov = 32f;

    // Parte 3 — plano final de grupo, frontal y centrado sobre las marcas de grupo de más abajo.
    static readonly Vector3 ShotGroupPos = new Vector3(0f, 1.55f, -4.2f);
    static readonly Vector3 ShotGroupLookAt = new Vector3(0f, 1.2f, 0.05f);
    const float ShotGroupFov = 36f;

    // ── Parámetros del cálculo automático de encuadre ──────────────────────────────────────────
    // Todo lo de aquí es adimensional (fracciones de la altura REAL medida del personaje), nunca
    // metros: es justo lo que hace que funcione igual con un chibi que con un humano adulto.

    /// Relación de aspecto de referencia para el encaje HORIZONTAL del encuadre (ancho/alto).
    /// No se puede leer la del Game View desde aquí de forma fiable, así que se asume 16:9, que es
    /// el formato en el que se van a grabar los vídeos. Si algún día se graba en vertical (9:16 para
    /// Shorts/TikTok), cambiar esto a 9f/16f: con un aspecto tan estrecho el ancho pasa a ser el
    /// factor que manda y la cámara tendría que retroceder bastante más.
    const float AspectoObjetivo = 16f / 9f;

    /// Margen de seguridad vertical extra (10%) sobre la altura ya calculada, para absorber
    /// diferencias de aspecto, overscan y el hecho de que la pose de idle no es exactamente la pose
    /// de bind con la que se miden los bounds del SkinnedMeshRenderer.
    const float MargenExtraVertical = 0.10f;

    // Composición de cada plano, en "alturas de cabeza" (distancia línea de hombros → alto del pelo),
    // que es la unidad que de verdad escala bien entre proporciones distintas:
    //   · CabezasBajoHombros_X : cuánto entra en cuadro por debajo de la línea de hombros.
    //   · AireSobreCabeza_X    : aire por encima del pelo (headroom), para que la cabeza no quede
    //                            pegada al borde superior — el fallo exacto de la pasada anterior.
    //   · MargenLateral_X      : margen a cada lado, como fracción del ancho medido (coletas, brazos).
    // Con 0.90 abajo + 0.25 arriba la cara cae en torno al 67% de altura del cuadro, es decir, justo
    // en la línea del tercio superior: composición estándar de plano de presentación a cámara.
    const float CabezasBajoHombros_EstelaSolo = 0.90f;
    const float AireSobreCabeza_EstelaSolo = 0.25f;
    const float MargenLateral_EstelaSolo = 0.15f;

    const float CabezasBajoHombros_EstelaLiam = 1.15f;
    const float AireSobreCabeza_EstelaLiam = 0.30f;
    const float MargenLateral_EstelaLiam = 0.15f;

    // Will se revela de cuerpo entero: no se usa la línea de hombros, se encuadra de los pies (con un
    // poco de suelo) al pelo.
    const float AireSobreCabeza_WillReveal = 0.20f;
    const float MargenLateral_WillReveal = 0.15f;

    const float CabezasBajoHombros_Grupo = 1.80f;
    const float AireSobreCabeza_Grupo = 0.30f;
    const float MargenLateral_Grupo = 0.18f;

    // ── Marcas del plano de grupo (Parte 3) ────────────────────────────────────────────────────
    // El sequencer recoloca a los 3 sobre estas marcas EN EL MISMO FRAME en que corta al plano de
    // grupo, así que el salto de posición no se ve. Sin ellas, los personajes se quedarían tan
    // separados como en la Parte 2 y el plano final no sería un plano de grupo de verdad.
    static readonly Vector3 MarkEstelaPosition = new Vector3(0f, 0f, 0.1f);
    static readonly Vector3 MarkLiamPosition = new Vector3(-1.05f, 0f, 0.25f);
    static readonly Vector3 MarkWillPosition = new Vector3(1.05f, 0f, 0.25f);

    [MenuItem("El Sendero/Marketing/Preparar Escena de Promo en el Sendero")]
    public static void CreatePromoStudioScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[PromoStudioSceneBuilder] Sal de Play Mode antes de ejecutar esto.");
            return;
        }

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var open = EditorSceneManager.GetSceneAt(i);
            if (open.isDirty)
            {
                Debug.LogError($"[PromoStudioSceneBuilder] La escena '{open.name}' tiene cambios sin guardar. Guarda (Ctrl+S) antes de ejecutar esto.");
                return;
            }
        }

        bool sceneAlreadyExisted = System.IO.File.Exists(ScenePath);
        Scene scene;

        if (sceneAlreadyExisted)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[PromoStudioSceneBuilder] No se pudo abrir {ScenePath}.");
                return;
            }
        }
        else
        {
            EnsureFolderExists(SceneFolder);
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        try
        {
            SetUpBackdrop();
            SetUpLighting();
            SetUpCamera();
            SetUpCharacters();
            SetUpCinematics(); // debe ir DESPUÉS de SetUpCharacters: enlaza los transforms de los 3

            EditorSceneManager.MarkSceneDirty(scene);
            if (sceneAlreadyExisted)
                EditorSceneManager.SaveScene(scene);
            else
                EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            MigrarPromoASequencePlayer.Migrar();
            Debug.Log($"[PromoStudioSceneBuilder] Escena de promo en el Sendero {(sceneAlreadyExisted ? "reparada" : "creada")}. " +
                      "La secuencia se genera con SequencePlayer y SequenceDefinition. Revisa los encuadres de cámara en Scene View y ajusta el texto final con la URL de la web.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PromoStudioSceneBuilder] Error durante la construcción (la escena puede haber quedado a medio guardar): {e}");
        }
    }

    // ── Camino y vista del bosque ───────────────────────────────────────────────────────────────

    static void SetUpBackdrop()
    {
        var container = FindByNameIncludingInactive(BackdropContainerName);
        bool created = container == null;
        if (container == null)
            container = new GameObject(BackdropContainerName);

        // El suelo y el camino prolongan la vista del bosque bajo los personajes.
        var floor = GetOrCreatePrimitiveChild(container.transform, "Suelo", PrimitiveType.Plane);
        floor.transform.localPosition = new Vector3(0f, -0.03f, 5f);
        floor.transform.localScale = new Vector3(10f, 1f, 10f);
        AplicarMaterialDeColor(floor, GroundColor);

        for (int i = 0; i < 5; i++)
        {
            var tramo = GetOrCreatePrimitiveChild(container.transform, $"Camino_{i + 1:00}", PrimitiveType.Plane);
            float z = -2f + i * 4.5f;
            float x = Mathf.Sin(i * 0.65f) * 0.8f;
            tramo.transform.localPosition = new Vector3(x, 0.005f, z);
            tramo.transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(i * 0.65f) * 7f, 0f);
            tramo.transform.localScale = new Vector3(0.42f, 1f, 0.75f);
            AplicarMaterialDeColor(tramo, PathColor);
        }

        // El quad mantiene la captura original del entorno del juego, con la ruta visible entre los árboles.
        var backPanel = GetOrCreatePrimitiveChild(container.transform, "PanelFondo", PrimitiveType.Quad);
        backPanel.transform.localPosition = new Vector3(0f, 2f, 17f);
        backPanel.transform.localScale = new Vector3(27f, 18f, 1f);
        var vista = AssetDatabase.LoadAssetAtPath<Texture2D>(VistaBosquePath);
        var renderer = backPanel.GetComponent<Renderer>();
        var material = CargarMaterialDeFondo(vista);
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        Debug.Log($"[PromoStudioSceneBuilder] Escenario exterior {(created ? "creado" : "reparado")} con vista del bosque y camino bajo '{BackdropContainerName}'.");
    }

    static GameObject GetOrCreatePrimitiveChild(Transform parent, string name, PrimitiveType type)
    {
        var existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);

        // Puramente decorado visual — sin física, no necesita Collider (evita interferencias con
        // cualquier física/raycast si esta escena llega a compartirse con otras en el futuro).
        var col = go.GetComponent<Collider>();
        if (col != null)
            UnityEngine.Object.DestroyImmediate(col);

        return go;
    }

    static void AplicarMaterialDeColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogWarning("[PromoStudioSceneBuilder] No se encontró el shader 'Universal Render Pipeline/Lit' — " +
                              "¿está URP realmente activo en este proyecto? Se deja el material por defecto en " +
                              $"'{go.name}', revísalo a mano.");
            return;
        }

        var mat = renderer.sharedMaterial;
        if (mat == null || mat.shader != shader)
        {
            mat = new Material(shader) { name = $"{go.name}_StudioMat" };
            renderer.sharedMaterial = mat;
        }

        mat.color = color;
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.05f);
    }

    static Material CargarMaterialDeFondo(Texture2D textura)
    {
        if (textura == null)
        {
            Debug.LogError($"[PromoStudioSceneBuilder] No se encontró la vista del bosque: {VistaBosquePath}.");
            return null;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialSenderoPath);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            material = new Material(shader) { name = "PromoSenderoBackdrop" };
            AssetDatabase.CreateAsset(material, MaterialSenderoPath);
        }

        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", textura);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", textura);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    // ── Iluminación (key + relleno, sin sombras duras) ──────────────────────────────────────────

    static void SetUpLighting()
    {
        var container = FindByNameIncludingInactive(LightingContainerName);
        bool created = container == null;
        if (container == null)
            container = new GameObject(LightingContainerName);

        var key = GetOrCreateDirectionalLight(container.transform, "Luz_Key");
        key.transform.localRotation = Quaternion.Euler(45f, -35f, 0f);
        key.intensity = 1.15f;
        key.color = new Color(1f, 0.91f, 0.78f);
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.55f; // suave, no negra — pedido: sin sombras duras feas sobre los personajes

        var fill = GetOrCreateDirectionalLight(container.transform, "Luz_Relleno");
        fill.transform.localRotation = Quaternion.Euler(35f, 150f, 0f);
        fill.intensity = 0.45f;
        fill.color = new Color(0.72f, 0.84f, 0.88f);
        fill.shadows = LightShadows.None; // solo rellena el lado oscuro, no añade una segunda sombra cruzada

        // Ambiente plano gris claro: evita que el lado sin luz directa de suelo/personajes caiga a negro puro.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.48f, 0.57f, 0.48f);

        Debug.Log($"[PromoStudioSceneBuilder] Iluminación de estudio (key + relleno + ambiente plano) {(created ? "creada" : "reparada")} bajo '{LightingContainerName}'.");
    }

    static Light GetOrCreateDirectionalLight(Transform parent, string name)
    {
        var existing = parent.Find(name);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(name, typeof(Light));
            go.transform.SetParent(parent, false);
        }

        var light = go.GetComponent<Light>();
        light.type = LightType.Directional;
        return light;
    }

    // ── Cámara fija (plano medio/americano, centrada en el grupo) ───────────────────────────────

    static void SetUpCamera()
    {
        var existing = FindByNameIncludingInactive(CameraGoName);
        bool created = existing == null;
        var go = existing != null ? existing : new GameObject(CameraGoName, typeof(Camera), typeof(AudioListener));

        var cam = go.GetComponent<Camera>();
        if (cam == null) cam = go.AddComponent<Camera>();
        if (go.GetComponent<AudioListener>() == null) go.AddComponent<AudioListener>();

        go.transform.position = CameraPosition;
        go.transform.rotation = Quaternion.LookRotation((CameraLookAtPoint - CameraPosition).normalized);
        go.tag = "MainCamera"; // misma convención que el resto de escenas del proyecto

        cam.fieldOfView = CameraFieldOfView;
        // El color acompaña el cielo de la vista del bosque si algún plano deja ver los bordes del quad.
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.15f, 0.19f);

        Debug.Log($"[PromoStudioSceneBuilder] Cámara de promo {(created ? "creada" : "reparada")} ('{CameraGoName}', FOV={CameraFieldOfView}°).");
    }

    // ── Personajes (Will, Liam, Estela) ──────────────────────────────────────────────────────────

    static void SetUpCharacters()
    {
        var container = FindByNameIncludingInactive(CharactersContainerName);
        if (container == null)
            container = new GameObject(CharactersContainerName);

        // Estela: centrada, de cara a cámara (Parte 1 del guion).
        PlaceCharacter(container.transform, EstelaPrefabPath, "Estela", EstelaPosition, CameraPosition);

        // Liam: ya en su marca, orientado hacia Estela. No camina — lo descubre la cámara al abrir
        // al plano medio (Parte 2). El sequencer lo gira a cámara cuando le toca hablar de frente.
        PlaceCharacter(container.transform, LiamPrefabPath, "Liam", LiamStartPosition, EstelaPosition);

        // Will: al fondo/lateral, apartado y DE ESPALDAS al plano de revelación, mirando hacia su
        // propia práctica con la espada — el whip-pan del sequencer lo pilla así (Parte 3).
        var willLookTarget = WillPosition + new Vector3(1.2f, 0f, 0.8f);
        PlaceCharacter(container.transform, WillPrefabPath, "Will", WillPosition, willLookTarget);

        Debug.Log($"[PromoStudioSceneBuilder] Los 3 protagonistas colocados/reposicionados bajo '{CharactersContainerName}' según el blocking del guion.");
    }

    static void PlaceCharacter(Transform parent, string prefabPath, string childName, Vector3 position, Vector3 lookAtPoint)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        var existingChild = parent.Find(childName);
        if (existingChild != null)
        {
            // AUTO-REPARACIÓN — ver ComponentesPreservadosQueFaltan() para el porqué completo.
            var faltantes = ComponentesPreservadosQueFaltan(existingChild, prefab);

            if (faltantes.Count == 0)
            {
                ApplyBlockingTransform(existingChild, position, lookAtPoint);
                Debug.Log($"[PromoStudioSceneBuilder] '{childName}' ya existía en la escena y conserva todos los " +
                          "componentes de la allowlist PreservedBehaviourTypes — se da por bueno y solo se reposiciona.");
                return;
            }

            if (prefab == null)
            {
                Debug.LogError($"[PromoStudioSceneBuilder] A '{childName}' le falta(n) {string.Join(", ", faltantes)} " +
                               $"(instancia obsoleta), pero NO se encuentra el prefab '{prefabPath}' para regenerarlo. " +
                               "Se deja como está y solo se reposiciona: revisa la ruta del prefab.");
                ApplyBlockingTransform(existingChild, position, lookAtPoint);
                return;
            }

            Debug.LogWarning($"[PromoStudioSceneBuilder] ♻ '{childName}' ya existía en la escena PERO le falta(n) " +
                             $"{string.Join(", ", faltantes)} de la allowlist PreservedBehaviourTypes. Es una instancia " +
                             "VIEJA, creada por una versión anterior de este script (de antes de que ese componente " +
                             "entrase en la allowlist del saneado). Reposicionarla no lo arregla nunca, así que se " +
                             "DESTRUYE y se vuelve a instanciar limpia desde el prefab, pasando otra vez por " +
                             "StripToVisualOnly() con la allowlist actual. SequenceStage registra sus referencias " +
                             "actualizadas al migrar la escena.");
            UnityEngine.Object.DestroyImmediate(existingChild.gameObject);
        }

        if (prefab == null)
        {
            Debug.LogError($"[PromoStudioSceneBuilder] No se encontró el prefab en '{prefabPath}' — no se puede colocar a '{childName}'. Revisa la ruta.");
            return;
        }

        var instance = InstantiatePrefabVisualOnly(prefab, parent);
        instance.name = childName;
        ApplyBlockingTransform(instance.transform, position, lookAtPoint);
    }

    /// <summary>
    /// Devuelve los nombres de los tipos de PreservedBehaviourTypes que el PREFAB sí trae pero que a
    /// la instancia ya existente en la escena le FALTAN. Lista vacía = la instancia está al día y
    /// basta con reposicionarla.
    ///
    /// POR QUÉ EXISTE (bug real, 23 ago 2026 — "las caras no han cambiado ni una vez"): este builder
    /// es "reparador", y hasta ahora eso significaba que si el GameObject de un personaje YA estaba en
    /// la escena se le actualizaba solo posición/rotación y punto — no se volvía a pasar por
    /// InstantiatePrefabVisualOnly()/StripToVisualOnly(). Consecuencia: los 3 personajes de la escena
    /// de Raúl se habían instanciado en una ejecución ANTERIOR del menú, de cuando el saneado todavía
    /// no tenía allowlist y arrasaba también con NPCEmotionController. Al añadir después la allowlist,
    /// el código nuevo NUNCA llegaba a esos GameObjects viejos: re-ejecutar el menú los daba por
    /// buenos, así que seguían sin el componente de las caras, WireSequencer() enlazaba null en
    /// _estelaEmotion/_liamEmotion/_willEmotion y ninguna expresión cambiaba en el vídeo.
    ///
    /// Es la misma familia de bug que el de las 21 claves de animación (código nuevo que no alcanza a
    /// datos viejos ya serializados), pero allí bastaba con rellenar un campo y aquí falta un
    /// componente entero. Se resuelve destruyendo y reinstanciando desde el prefab en vez de intentar
    /// añadir el componente a mano: reinstanciar reproduce EXACTAMENTE lo que trae el prefab, incluida
    /// la peculiaridad de _WILL.prefab (lleva DOS NPCEmotionController en el mismo GameObject, uno con
    /// EmotionProfile y otro sin él), que sería muy fácil recrear mal a mano.
    ///
    /// GENÉRICO A PROPÓSITO: recorre PreservedBehaviourTypes, no NPCEmotionController en concreto. El
    /// día que se preserve un segundo script, las instancias viejas se autorrepararán igual sin tocar
    /// nada aquí.
    ///
    /// Se exige el componente solo si el PREFAB lo tiene: si el prefab tampoco lo lleva, no hay nada
    /// que reparar y regenerar al personaje sería condenarlo a reinstanciarse en CADA ejecución del
    /// menú, para siempre y sin arreglar nada. Ese caso ya lo avisa FindEmotionController().
    /// </summary>
    static System.Collections.Generic.List<string> ComponentesPreservadosQueFaltan(Transform instancia, GameObject prefab)
    {
        var faltantes = new System.Collections.Generic.List<string>();
        if (instancia == null) return faltantes;

        foreach (var t in PreservedBehaviourTypes)
        {
            if (t == null) continue;
            if (prefab != null && prefab.GetComponentInChildren(t, true) == null) continue;

            if (instancia.GetComponentInChildren(t, true) == null)
                faltantes.Add(t.Name);
        }

        return faltantes;
    }

    static void ApplyBlockingTransform(Transform t, Vector3 position, Vector3 lookAtPoint)
    {
        t.position = position;

        Vector3 dir = lookAtPoint - position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            t.rotation = Quaternion.LookRotation(dir.normalized);
    }

    // ── Rig de cámara de la promo ───────────────────────────────────────────────────────────────
    // Los planos y las marcas se enlazan a SequenceStage al migrar la secuencia.

    static void SetUpCinematics()
    {
        var container = FindByNameIncludingInactive(CinematicsContainerName);
        if (container == null)
        {
            container = FindByNameIncludingInactive("Cinematica_Estudio");
            if (container != null) container.name = CinematicsContainerName;
        }
        bool created = container == null;
        if (container == null)
            container = new GameObject(CinematicsContainerName);

        // Esta escena carga Start.unity de forma aditiva (ver AutoBootstrapOnPlay.cs), así que el
        // botón global de "mantén pulsado para saltar" (GlobalCinematicSkipController/HoldToSkipUI,
        // persistente en Start) se activaría solo con dar Play y disparar la secuencia — no tiene
        // ningún sentido en un plató de grabación y arruinaría cualquier vídeo si sale en pantalla.
        // Ver PromoStudioUISuppressor.
        if (container.GetComponent<PromoStudioUISuppressor>() == null)
            container.AddComponent<PromoStudioUISuppressor>();

        GetOrAddComponentOnChild<CinematicCameraDriver>(container.transform, CameraDriverGoName);

        // ⚠ ORDEN DE EJECUCIÓN — NO REORDENAR: las referencias a los 3 personajes se resuelven AQUÍ
        // ARRIBA, antes de crear los planos, porque el encuadre de los 4 CinematicShot se CALCULA a
        // partir de la geometría real medida sobre esas instancias. Eso exige dos cosas:
        //   1) Que SetUpCharacters() ya se haya ejecutado (lo hace: CreatePromoStudioScene lo llama
        //      justo antes de SetUpCinematics, y ya lo advertía el comentario de esa llamada).
        //   2) Que los personajes estén YA EN SU POSICIÓN DE BLOCKING FINAL cuando se mide — lo
        //      están, porque PlaceCharacter/ApplyBlockingTransform los coloca en el mismo paso en
        //      que los instancia. Si alguien mueve el blocking a después de esta llamada, los planos
        //      se calcularían contra el origen y volverían a salir mal.
        var charactersRoot = FindByNameIncludingInactive(CharactersContainerName);
        Transform estela = charactersRoot != null ? charactersRoot.transform.Find("Estela") : null;
        Transform liam = charactersRoot != null ? charactersRoot.transform.Find("Liam") : null;
        Transform will = charactersRoot != null ? charactersRoot.transform.Find("Will") : null;

        // Medición real (bounds + huesos del avatar) de cada personaje ya colocado.
        var medEstela = MedirPersonaje(estela, "Estela");
        var medLiam = MedirPersonaje(liam, "Liam");
        var medWill = MedirPersonaje(will, "Will");

        var shotsRoot = GetOrCreateChild(container.transform, ShotsContainerName);

        CrearPlanoEncuadrado(shotsRoot, ShotEstelaSoloName, "Estela — primer plano",
            new[] { medEstela }, ShotEstelaSoloFov,
            false, CabezasBajoHombros_EstelaSolo, AireSobreCabeza_EstelaSolo, MargenLateral_EstelaSolo,
            ShotEstelaSoloPos, ShotEstelaSoloLookAt);

        CrearPlanoEncuadrado(shotsRoot, ShotEstelaLiamName, "Estela y Liam — plano medio",
            new[] { medEstela, medLiam }, ShotEstelaLiamFov,
            false, CabezasBajoHombros_EstelaLiam, AireSobreCabeza_EstelaLiam, MargenLateral_EstelaLiam,
            ShotEstelaLiamPos, ShotEstelaLiamLookAt);

        CrearPlanoEncuadrado(shotsRoot, ShotWillRevealName, "Will — plano medio",
            new[] { medWill }, ShotWillRevealFov,
            true, 0f, AireSobreCabeza_WillReveal, MargenLateral_WillReveal,
            ShotWillRevealPos, ShotWillRevealLookAt);

        // El plano general se calcula sobre las marcas donde se reúne el grupo.
        var medEstelaGrupo = DesplazarMedidas(medEstela, MarkEstelaPosition - EstelaPosition);
        var medLiamGrupo = DesplazarMedidas(medLiam, MarkLiamPosition - LiamStartPosition);
        var medWillGrupo = DesplazarMedidas(medWill, MarkWillPosition - WillPosition);

        var shotGroup = CrearPlanoEncuadrado(shotsRoot, ShotGroupName, "Parte 3 — plano final de grupo",
            new[] { medEstelaGrupo, medLiamGrupo, medWillGrupo }, ShotGroupFov,
            false, CabezasBajoHombros_Grupo, AireSobreCabeza_Grupo, MargenLateral_Grupo,
            ShotGroupPos, ShotGroupLookAt);

        // Marcas del plano de grupo: los 3 mirando al punto donde estará la cámara del plano final.
        // Se usa la posición REAL ya calculada del plano de grupo (no la constante ShotGroupPos), que
        // es donde la cámara va a estar de verdad.
        var marksRoot = GetOrCreateChild(container.transform, GroupMarksContainerName);
        var groupCameraGround = shotGroup != null
            ? new Vector3(shotGroup.position.x, 0f, shotGroup.position.z)
            : new Vector3(ShotGroupPos.x, 0f, ShotGroupPos.z);
        var markEstela = CreateOrUpdateMark(marksRoot, MarkEstelaName, MarkEstelaPosition, groupCameraGround);
        var markLiam = CreateOrUpdateMark(marksRoot, MarkLiamName, MarkLiamPosition, groupCameraGround);
        var markWill = CreateOrUpdateMark(marksRoot, MarkWillName, MarkWillPosition, groupCameraGround);

        Debug.Log("Rig de cámara y planos preparado.");
    }

    // ── Medición real del personaje y cálculo de encuadre ──────────────────────────────────────
    //
    // POR QUÉ EXISTE ESTO: hasta esta pasada los 4 planos eran constantes Vector3/float puestas a
    // ojo suponiendo un humano adulto. Los personajes del juego son un modelo estilizado tipo chibi
    // (RPG Tiny Hero Duo): la cabeza ocupa una fracción del cuerpo muchísimo mayor que en un humano
    // real, así que "mirar a 1.25 m de altura desde 3.4 m" no encuadra la cara, encuadra la frente.
    // Dos rondas de ajuste a ojo fallaron. La solución es dejar de adivinar y MEDIR: esta
    // herramienta corre dentro del Editor, con los prefabs ya instanciados, así que tiene acceso a
    // los bounds reales de los renderers y al avatar del Animator.
    //
    // CAMINO ELEGIDO: los 3 prefabs (_ESTELA/_LIAM/_WILL) comparten el mismo avatar HUMANOID
    // (el generado desde Idle_Battle_SwordAndShiled.fbx, animationType: 3), así que la línea de
    // hombros se saca de los huesos reales del avatar vía Animator.GetBoneTransform(). El alto del
    // pelo (coronilla) SIEMPRE sale de los bounds de los renderers, porque las coletas de Estela no
    // son huesos del avatar humanoide y son justo lo que se estaba saliendo del cuadro. Si algún día
    // un personaje llega con avatar Generic, hay fallback por bounds (ver MedirPersonaje).

    /// Medidas reales, EN COORDENADAS DE MUNDO, de un personaje ya colocado en su marca de blocking.
    class MedidasPersonaje
    {
        public string nombre;
        public Bounds bounds;            // combinado de sus renderers de malla visibles
        public float yPies;              // bounds.min.y
        public float yCoronilla;         // bounds.max.y — incluye pelo/coletas/sombrero
        public float yHombros;           // línea de hombros (hueso del avatar, o fallback por bounds)
        public Vector3 centroSuelo;      // X/Z del transform del personaje, Y al nivel de los pies
        public bool viaHuesoHumanoid;    // true = medido con el avatar; false = fallback por bounds
        public string detalleMedicion;   // para el log

        public float AlturaTotal => Mathf.Max(0.01f, yCoronilla - yPies);
        /// Unidad de composición: de la línea de hombros al alto del pelo. Escala sola entre
        /// proporciones distintas (chibi vs humano), que es justo lo que faltaba antes.
        public float AlturaCabeza => Mathf.Max(0.01f, yCoronilla - yHombros);
    }

    /// Resultado del cálculo de encuadre de un plano.
    struct EncuadreCalculado
    {
        public bool valido;
        public Vector3 pos;
        public Vector3 lookAt;
        public float distancia;
        public float alturaEncuadre;
        public float anchoEncuadre;
        public float yInferior;
        public float ySuperior;
        public float yHombrosRef;
        public float yCoronillaRef;
        public float alturaCabezaRef;
        public bool mandaElAncho;   // true si la distancia la impuso el encaje horizontal, no el vertical
    }

    /// Mide un personaje ya instanciado y colocado. Devuelve null si no hay nada medible.
    static MedidasPersonaje MedirPersonaje(Transform personaje, string nombre)
    {
        if (personaje == null)
        {
            Debug.LogWarning($"[PromoStudioSceneBuilder] No se puede medir a '{nombre}' (no está en la escena) — " +
                             "los planos que dependan de él caerán al valor fijo de fallback.");
            return null;
        }

        Bounds? acumulado = AcumularBoundsDeMalla(personaje, false);
        bool incluyendoInactivos = false;
        if (!acumulado.HasValue)
        {
            // Red de seguridad: si por lo que sea los renderers están desactivados en el prefab.
            acumulado = AcumularBoundsDeMalla(personaje, true);
            incluyendoInactivos = acumulado.HasValue;
        }

        if (!acumulado.HasValue)
        {
            Debug.LogWarning($"[PromoStudioSceneBuilder] '{nombre}' no tiene ningún MeshRenderer/SkinnedMeshRenderer " +
                             "del que sacar bounds — no se puede calcular su encuadre, se usará el valor fijo de fallback.");
            return null;
        }

        Bounds b = acumulado.Value;
        var m = new MedidasPersonaje
        {
            nombre = nombre,
            bounds = b,
            yPies = b.min.y,
            yCoronilla = b.max.y,
            centroSuelo = new Vector3(personaje.position.x, b.min.y, personaje.position.z),
        };

        float alturaTotal = Mathf.Max(0.01f, b.max.y - b.min.y);

        // ── Camino 1 (el bueno): avatar Humanoid ────────────────────────────────────────────────
        var anim = BuscarAnimatorHumanoide(personaje);
        if (anim != null)
        {
            var brazoIzq = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var brazoDer = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var pecho = anim.GetBoneTransform(HumanBodyBones.UpperChest);
            if (pecho == null) pecho = anim.GetBoneTransform(HumanBodyBones.Chest);
            if (pecho == null) pecho = anim.GetBoneTransform(HumanBodyBones.Spine);
            var cabeza = anim.GetBoneTransform(HumanBodyBones.Head);

            if (brazoIzq != null && brazoDer != null)
            {
                m.yHombros = (brazoIzq.position.y + brazoDer.position.y) * 0.5f;
                m.viaHuesoHumanoid = true;
                m.detalleMedicion = "Humanoid, media de LeftUpperArm/RightUpperArm";
            }
            else if (pecho != null)
            {
                m.yHombros = pecho.position.y;
                m.viaHuesoHumanoid = true;
                m.detalleMedicion = "Humanoid, hueso de pecho (UpperChest/Chest/Spine)";
            }
            else if (cabeza != null)
            {
                m.yHombros = cabeza.position.y;
                m.viaHuesoHumanoid = true;
                m.detalleMedicion = "Humanoid, hueso Head (sin huesos de brazo/pecho mapeados)";
            }

            if (m.viaHuesoHumanoid && cabeza != null)
                m.detalleMedicion += $"; hueso Head en Y={cabeza.position.y:F3}";
        }

        // ── Camino 2 (fallback): avatar Generic o huesos sin mapear ─────────────────────────────
        // Se toma el tercio superior de los bounds como "zona de cabeza" (regla acordada), y si hay
        // un hueso reconocible por nombre se coge el MENOR de los dos: equivocarse por abajo abre el
        // plano (feo pero utilizable), equivocarse por arriba lo cierra y decapita al personaje —
        // que es exactamente el fallo que se está corrigiendo.
        if (!m.viaHuesoHumanoid)
        {
            float porBounds = b.min.y + (2f / 3f) * alturaTotal;
            m.yHombros = porBounds;
            m.detalleMedicion = "FALLBACK por bounds (avatar no Humanoid): tercio superior de los bounds";

            var huesoPorNombre = BuscarTransformDeCabezaPorNombre(personaje);
            if (huesoPorNombre != null)
            {
                m.yHombros = Mathf.Min(porBounds, huesoPorNombre.position.y);
                m.detalleMedicion += $"; ajustado con el transform '{huesoPorNombre.name}' (Y={huesoPorNombre.position.y:F3}), " +
                                     "se toma el menor de ambos";
            }
        }

        // Un personaje con la cabeza medida por encima del pelo no tiene sentido: red de seguridad.
        if (m.yHombros >= m.yCoronilla - 0.001f)
        {
            m.yHombros = b.min.y + (2f / 3f) * alturaTotal;
            m.detalleMedicion += " [CORREGIDO: la línea de hombros salía por encima de la coronilla, se vuelve al tercio superior]";
        }

        Debug.Log($"[PromoStudioSceneBuilder] 📏 Medida real de '{nombre}': altura total {m.AlturaTotal:F3} m " +
                  $"(pies Y={m.yPies:F3} → coronilla Y={m.yCoronilla:F3}), hombros Y={m.yHombros:F3}, " +
                  $"altura de cabeza (hombros→pelo) {m.AlturaCabeza:F3} m " +
                  $"= {(m.AlturaCabeza / m.AlturaTotal * 100f):F0}% del cuerpo. " +
                  $"Ancho de bounds X={m.bounds.size.x:F3} / Z={m.bounds.size.z:F3}. " +
                  $"Método: {m.detalleMedicion}." +
                  (incluyendoInactivos ? " ⚠ Se han tenido que incluir renderers INACTIVOS para poder medir." : ""));

        return m;
    }

    /// Combina los bounds de mundo de los renderers de MALLA del personaje. Se dejan fuera
    /// ParticleSystemRenderer/TrailRenderer/LineRenderer/SpriteRenderer y demás: un VFX o un trail
    /// perdido dispararía los bounds y alejaría la cámara sin motivo.
    static Bounds? AcumularBoundsDeMalla(Transform personaje, bool incluirInactivos)
    {
        Bounds? acumulado = null;

        foreach (var r in personaje.GetComponentsInChildren<Renderer>(incluirInactivos))
        {
            if (r == null) continue;
            if (!incluirInactivos && !r.enabled) continue;
            if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;

            var b = r.bounds;
            if (b.size.sqrMagnitude <= 0.0000001f) continue;

            if (acumulado.HasValue)
            {
                var acc = acumulado.Value;
                acc.Encapsulate(b);
                acumulado = acc;
            }
            else
            {
                acumulado = b;
            }
        }

        return acumulado;
    }

    /// El Animator cuyo avatar es Humanoid. No vale GetComponentInChildren&lt;Animator&gt;() a secas:
    /// estos prefabs llevan Animator adicionales en las mallas de arma ('Bows', 'Arrows') con avatar
    /// Generic, y GetBoneTransform() sobre ellos devuelve null siempre.
    static Animator BuscarAnimatorHumanoide(Transform personaje)
    {
        foreach (var a in personaje.GetComponentsInChildren<Animator>(true))
        {
            if (a == null) continue;
            if (a.avatar == null || !a.avatar.isValid) continue;
            if (a.isHuman) return a;
        }
        return null;
    }

    /// Último recurso para rigs no humanoides: un Transform que se llame como una cabeza.
    static Transform BuscarTransformDeCabezaPorNombre(Transform personaje)
    {
        foreach (var t in personaje.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            string n = t.name.ToLowerInvariant();
            if (n == "head" || n == "cabeza" || n == "bip01 head" || n == "mixamorig:head")
                return t;
        }
        return null;
    }

    /// Copia unas medidas trasladadas a otra posición (para el plano de grupo, donde el sequencer
    /// teletransporta a los personajes a las marcas antes de que se les vea).
    static MedidasPersonaje DesplazarMedidas(MedidasPersonaje m, Vector3 delta)
    {
        if (m == null) return null;

        var b = m.bounds;
        b.center += delta;

        return new MedidasPersonaje
        {
            nombre = m.nombre,
            bounds = b,
            yPies = m.yPies + delta.y,
            yCoronilla = m.yCoronilla + delta.y,
            yHombros = m.yHombros + delta.y,
            centroSuelo = m.centroSuelo + delta,
            viaHuesoHumanoid = m.viaHuesoHumanoid,
            detalleMedicion = m.detalleMedicion + " (desplazado a la marca de grupo)",
        };
    }

    /// <summary>
    /// Despeja posición y punto de mira de un plano a partir de las medidas reales de sus sujetos.
    ///
    /// Composición vertical (todo en "alturas de cabeza", nunca en metros absolutos):
    ///   ySuperior = coronilla + aireSobreCabeza·alturaCabeza     ← headroom, para que el pelo no
    ///                                                              toque el borde de arriba
    ///   yInferior = hombros  − cabezasBajoHombros·alturaCabeza   ← pecho/cintura (o los pies si
    ///                                                              cuerpoEntero)
    /// El punto de mira va al centro de esa ventana, con lo que la cara queda por encima del centro
    /// del cuadro (≈ línea del tercio superior con los valores por defecto), no clavada en el medio.
    ///
    /// Distancia: fórmula estándar de cámara en perspectiva, resuelta para las DOS dimensiones y
    /// quedándose con la más exigente:
    ///   alturaVisible = 2·d·tan(fovV/2)  →  d = (alturaVisible/2) / tan(fovV/2)
    ///   anchoVisible  = 2·d·tan(fovH/2), con tan(fovH/2) = tan(fovV/2)·aspecto
    /// (Camera.fieldOfView de Unity es el FOV VERTICAL, de ahí que el horizontal salga del aspecto.)
    ///
    /// La cámara se deja A NIVEL (misma altura Y que el punto de mira, desplazada solo en XZ): así el
    /// cálculo de arriba es exacto (sin keystone) y las verticales del personaje no divergen. La
    /// dirección XZ la marca el llamante, que la saca del plano original — el ángulo artístico
    /// (frontal, 3/4, lateral) se conserva; lo que se recalcula es la distancia y la altura.
    /// </summary>
    static EncuadreCalculado CalcularEncuadre(MedidasPersonaje[] sujetos, float fovVertical,
                                              bool cuerpoEntero, float cabezasBajoHombros,
                                              float aireSobreCabeza, float margenLateral,
                                              Vector3 dirCamaraXZ)
    {
        var res = new EncuadreCalculado { valido = false };

        var validos = new System.Collections.Generic.List<MedidasPersonaje>();
        if (sujetos != null)
            foreach (var s in sujetos)
                if (s != null) validos.Add(s);

        if (validos.Count == 0) return res;

        Bounds grupo = validos[0].bounds;
        float yHombrosRef = validos[0].yHombros;      // el hombro MÁS BAJO: así entran los de todos
        float yCoronillaRef = validos[0].yCoronilla;  // la coronilla MÁS ALTA
        float alturaCabezaRef = validos[0].AlturaCabeza;
        Vector3 centroXZ = new Vector3(validos[0].centroSuelo.x, 0f, validos[0].centroSuelo.z);

        for (int i = 1; i < validos.Count; i++)
        {
            grupo.Encapsulate(validos[i].bounds);
            yHombrosRef = Mathf.Min(yHombrosRef, validos[i].yHombros);
            yCoronillaRef = Mathf.Max(yCoronillaRef, validos[i].yCoronilla);
            alturaCabezaRef = Mathf.Max(alturaCabezaRef, validos[i].AlturaCabeza);
            centroXZ += new Vector3(validos[i].centroSuelo.x, 0f, validos[i].centroSuelo.z);
        }
        centroXZ /= validos.Count;

        float alturaTotalGrupo = Mathf.Max(0.01f, grupo.max.y - grupo.min.y);

        float yInferior = cuerpoEntero
            ? grupo.min.y - 0.06f * alturaTotalGrupo   // un poco de suelo bajo los pies
            : Mathf.Max(grupo.min.y, yHombrosRef - cabezasBajoHombros * alturaCabezaRef);
        float ySuperior = yCoronillaRef + aireSobreCabeza * alturaCabezaRef;

        float alturaUtil = Mathf.Max(0.05f, ySuperior - yInferior) * (1f + MargenExtraVertical);
        float centroY = (ySuperior + yInferior) * 0.5f;

        Vector3 dir = dirCamaraXZ;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.back;

        Vector3 lookAt = new Vector3(centroXZ.x, centroY, centroXZ.z);

        // Ancho realmente visible = extensión de los bounds sobre el eje perpendicular a la cámara
        // (no basta con bounds.size.x: en los planos en 3/4 la cámara ve una mezcla de X y Z).
        Vector3 perp = Vector3.Cross(Vector3.up, dir).normalized;
        float medioAncho = 0f;
        Vector3 c = grupo.center, e = grupo.extents;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 esquina = c + new Vector3(sx * e.x, sy * e.y, sz * e.z);
                    medioAncho = Mathf.Max(medioAncho, Mathf.Abs(Vector3.Dot(esquina - lookAt, perp)));
                }
        float anchoUtil = Mathf.Max(0.05f, 2f * medioAncho) * (1f + 2f * margenLateral);

        float tanV = Mathf.Tan(fovVertical * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * AspectoObjetivo;
        float distanciaPorAltura = (alturaUtil * 0.5f) / tanV;
        float distanciaPorAncho = (anchoUtil * 0.5f) / tanH;
        float distancia = Mathf.Max(distanciaPorAltura, distanciaPorAncho);

        res.valido = true;
        res.lookAt = lookAt;
        res.pos = lookAt + dir * distancia;   // misma Y que lookAt → cámara a nivel
        res.distancia = distancia;
        res.alturaEncuadre = alturaUtil;
        res.anchoEncuadre = anchoUtil;
        res.yInferior = yInferior;
        res.ySuperior = ySuperior;
        res.yHombrosRef = yHombrosRef;
        res.yCoronillaRef = yCoronillaRef;
        res.alturaCabezaRef = alturaCabezaRef;
        res.mandaElAncho = distanciaPorAncho > distanciaPorAltura;

        return res;
    }

    /// Crea un plano con el encuadre CALCULADO a partir de las medidas reales de sus sujetos.
    /// Si la medición no fue posible, cae al valor fijo de siempre y lo dice bien claro en consola.
    static Transform CrearPlanoEncuadrado(Transform parent, string name, string label,
                                          MedidasPersonaje[] sujetos, float fov,
                                          bool cuerpoEntero, float cabezasBajoHombros,
                                          float aireSobreCabeza, float margenLateral,
                                          Vector3 posPorDefecto, Vector3 lookAtPorDefecto)
    {
        // Del plano original se conserva SOLO el ángulo (dirección sujeto→cámara en XZ).
        Vector3 dirCamaraXZ = posPorDefecto - lookAtPorDefecto;
        dirCamaraXZ.y = 0f;
        if (dirCamaraXZ.sqrMagnitude <= 0.0001f) dirCamaraXZ = Vector3.back;
        dirCamaraXZ = dirCamaraXZ.normalized;

        var enc = CalcularEncuadre(sujetos, fov, cuerpoEntero, cabezasBajoHombros,
                                   aireSobreCabeza, margenLateral, dirCamaraXZ);

        if (!enc.valido)
        {
            Debug.LogWarning($"[PromoStudioSceneBuilder] ⚠ '{name}': no se pudo medir a ningún personaje del plano, " +
                             $"así que se cae al valor FIJO de fallback (pos {posPorDefecto}, mira {lookAtPorDefecto}, " +
                             $"FOV {fov}°). Ese valor está calculado a ojo y suponiendo proporciones de humano adulto: " +
                             "es muy probable que el encuadre salga mal. Revisa que los 3 personajes estén bajo " +
                             $"'{CharactersContainerName}' y vuelve a ejecutar el menú.");
            return CreateOrUpdateShot(parent, name, label, posPorDefecto, lookAtPorDefecto, fov);
        }

        float anguloFuera = Vector3.Angle(-dirCamaraXZ, Vector3.forward);

        Debug.Log($"[PromoStudioSceneBuilder] 🎥 '{name}' CALCULADO (no adivinado):\n" +
                  $"   · Ventana de encuadre vertical: Y {enc.yInferior:F3} → {enc.ySuperior:F3} " +
                  $"({(enc.ySuperior - enc.yInferior):F3} m; con el {MargenExtraVertical * 100f:F0}% de margen extra, " +
                  $"{enc.alturaEncuadre:F3} m). Referencias: hombros Y={enc.yHombrosRef:F3}, coronilla Y={enc.yCoronillaRef:F3}, " +
                  $"altura de cabeza {enc.alturaCabezaRef:F3} m.\n" +
                  $"   · Ancho a cubrir: {enc.anchoEncuadre:F3} m (margen lateral {margenLateral * 100f:F0}% por lado).\n" +
                  $"   · FOV vertical {fov}° · aspecto de referencia {AspectoObjetivo:F3} → " +
                  $"distancia despejada {enc.distancia:F3} m " +
                  $"({(enc.mandaElAncho ? "MANDA EL ANCHO: el sujeto es más ancho que alto para este cuadro" : "manda la altura, lo normal")}).\n" +
                  $"   · Cámara en {enc.pos} mirando a {enc.lookAt} " +
                  $"(a nivel: misma Y que el punto de mira; {anguloFuera:F1}° fuera del eje frontal, ángulo heredado del plano original).");

        return CreateOrUpdateShot(parent, name, label, enc.pos, enc.lookAt, fov);
    }

    /// Crea (o reposiciona) un punto de plano cinemático. CinematicShot exige un componente Camera
    /// en el mismo GameObject ([RequireComponent]) que él mismo deja siempre desactivado: nunca
    /// renderiza, solo sirve como preview de encuadre en la Scene View. Se deja sin tag para que
    /// Camera.main siga siendo la cámara de estudio y no una de estas.
    static Transform CreateOrUpdateShot(Transform parent, string name, string label,
                                        Vector3 position, Vector3 lookAt, float fov)
    {
        var existing = parent.Find(name);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
        }

        var cam = go.GetComponent<Camera>();
        if (cam == null) cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov;
        cam.enabled = false; // CinematicShot también lo fuerza, pero así queda claro desde el minuto 0

        var shot = go.GetComponent<CinematicShot>();
        if (shot == null) shot = go.AddComponent<CinematicShot>();
        shot.label = label;

        go.transform.position = position;
        Vector3 dir = lookAt - position;
        if (dir.sqrMagnitude > 0.0001f)
            go.transform.rotation = Quaternion.LookRotation(dir.normalized);

        return go.transform;
    }

    /// Marca de blocking del plano de grupo: GameObject vacío, mirando hacia la cámara del plano final.
    static Transform CreateOrUpdateMark(Transform parent, string name, Vector3 position, Vector3 lookAtGroundPoint)
    {
        var mark = GetOrCreateChild(parent, name);
        mark.position = position;

        Vector3 dir = lookAtGroundPoint - position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            mark.rotation = Quaternion.LookRotation(dir.normalized);

        return mark;
    }

    static Transform GetOrCreateChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static T GetOrAddComponentOnChild<T>(Transform parent, string childName) where T : Component
    {
        var child = GetOrCreateChild(parent, childName);
        var comp = child.GetComponent<T>();
        if (comp == null) comp = child.gameObject.AddComponent<T>();
        return comp;
    }

    // ── Instanciado "solo visual" de los prefabs jugables (adaptado de MainMenuStylingBuilder.cs) ─
    // Ver el comentario de cabecera del archivo para el porqué: si el GameObject queda activo desde
    // el primer frame, Awake()/OnEnable() de TODOS sus MonoBehaviour (Invector + sistemas propios:
    // PlayerInputManager, PlayerHealthSystem, PlayerPresetService, WardrobeInventory...) se ejecutan
    // de forma síncrona antes de que cualquier código posterior pueda desactivar nada — si alguno se
    // registra como singleton/servicio, para cuando lo desactivas el daño ya está hecho. La solución
    // es instanciar como hijo de un contenedor temporal DESACTIVADO (Unity no ejecuta Awake/OnEnable
    // de ningún hijo mientras el padre esté inactivo), destruir ahí mismo casi todos los MonoBehaviour/
    // Collider/Rigidbody/Camera/AudioListener, y solo entonces reparentar al contenedor real y activar.
    // El Animator no es un MonoBehaviour (es un Behaviour interno de Unity) y sobrevive intacto.

    static GameObject InstantiatePrefabVisualOnly(GameObject prefab, Transform finalParent)
    {
        var tempHolder = new GameObject("___TempInactiveHolder");
        tempHolder.SetActive(false); // clave: debe desactivarse ANTES de instanciar el hijo

        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, tempHolder.transform);
            StripToVisualOnly(instance);
            instance.transform.SetParent(finalParent, false);
            return instance;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tempHolder);
        }
    }

    /// <summary>
    /// Allowlist del saneado: scripts que SOBREVIVEN a StripToVisualOnly en vez de ser destruidos.
    ///
    /// Ahora mismo solo NPCEmotionController, el componente que cambia las caras de los personajes
    /// (activa/desactiva los meshes de ojos y boca según la emoción). El sequencer lo necesita vivo
    /// para llamar a SetEmotion() en cada línea, igual que hacen TabernaSequencer y
    /// LiamCrystalBallSequencer con los NPC del juego real. Hasta esta pasada caía en la purga
    /// general junto con el resto de MonoBehaviour y por eso las caras no cambiaban en el vídeo.
    ///
    /// Es seguro mantenerlo vivo aunque desaparezca todo lo demás: en Awake() busca
    /// NPCSimpleAnimator y NPCBehaviourManagerV2 pero SIEMPRE los usa con '?.', así que no revienta
    /// si no existen; y en OnEnable() solo se suscribe a eventos estáticos de DialogueManager, que
    /// en esta escena no se disparan nunca (el sequencer llama a SetEmotion() directamente, sin
    /// pasar por DialogueManager). Tampoco declara ningún [RequireComponent], así que no obliga a
    /// mantener vivo nada más.
    /// </summary>
    static readonly Type[] PreservedBehaviourTypes = { typeof(NPCEmotionController) };

    static void StripToVisualOnly(GameObject go)
    {
        int destroyedScripts = 0, destroyedPhysics = 0, destroyedAvOutputs = 0;

        // Varios scripts del rig se declaran dependientes entre sí vía [RequireComponent] (ej.:
        // NPCBehaviourManagerV2 requiere NPCSimpleAnimator). Unity no deja destruir un componente
        // mientras algo en el mismo GameObject dependa de él, así que se lee por reflexión el
        // [RequireComponent] de cada script presente y en cada pasada solo se destruyen los que ya
        // no son requeridos por ningún otro script todavía vivo — el orden correcto sale solo.
        var behaviours = new System.Collections.Generic.List<MonoBehaviour>(go.GetComponentsInChildren<MonoBehaviour>(true));
        behaviours.RemoveAll(mb => mb == null); // referencias de script roto/perdido

        // Allowlist (ver PreservedBehaviourTypes): se apartan ANTES del barrido, así el bucle de
        // abajo ni los mira. Se guardan en su propia lista en vez de descartarlos del todo porque
        // sus [RequireComponent] tienen que seguir contando al decidir el orden de destrucción del
        // resto: si un script preservado dependiera de otro, ese otro tampoco se puede destruir.
        var preserved = new System.Collections.Generic.List<MonoBehaviour>();
        for (int i = behaviours.Count - 1; i >= 0; i--)
        {
            if (IsPreservedBehaviour(behaviours[i]))
            {
                preserved.Add(behaviours[i]);
                behaviours.RemoveAt(i);
            }
        }

        for (int pass = 0; pass < 8 && behaviours.Count > 0; pass++)
        {
            var stillRequired = new System.Collections.Generic.HashSet<Type>();
            CollectRequiredTypes(behaviours, stillRequired);
            CollectRequiredTypes(preserved, stillRequired);

            int removedThisPass = 0;
            for (int i = behaviours.Count - 1; i >= 0; i--)
            {
                var mb = behaviours[i];
                bool isStillNeeded = false;
                foreach (var t in stillRequired)
                {
                    if (t.IsInstanceOfType(mb)) { isStillNeeded = true; break; }
                }
                if (isStillNeeded) continue;

                UnityEngine.Object.DestroyImmediate(mb, true);
                behaviours.RemoveAt(i);
                destroyedScripts++;
                removedThisPass++;
            }

            if (removedThisPass == 0) break;
        }

        if (behaviours.Count > 0)
        {
            // Solo puede pasar con una dependencia [RequireComponent] circular real (que ni el propio
            // Inspector de Unity dejaría deshacer a mano), o si algún día un script de la allowlist
            // PreservedBehaviourTypes declarase un [RequireComponent] sobre otro script (hoy
            // NPCEmotionController no declara ninguno). Red de seguridad: desactivar en vez de
            // destruir (evita Update/comportamiento posterior, aunque su Awake ya se habrá disparado).
            var names = new System.Collections.Generic.List<string>();
            foreach (var mb in behaviours)
            {
                mb.enabled = false;
                names.Add(mb.GetType().Name);
            }
            Debug.LogWarning($"[PromoStudioSceneBuilder] '{go.name}': {behaviours.Count} script(s) con una dependencia " +
                              $"[RequireComponent] circular entre sí no se pudieron eliminar ({string.Join(", ", names)}) — " +
                              "se han desactivado como red de seguridad, revísalo si notas algo raro procedente de ellos.");
        }

        foreach (var col in go.GetComponentsInChildren<Collider>(true))
        {
            UnityEngine.Object.DestroyImmediate(col, true);
            destroyedPhysics++;
        }

        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
        {
            UnityEngine.Object.DestroyImmediate(rb, true);
            destroyedPhysics++;
        }

        // Camera y AudioListener no son MonoBehaviour, así que el barrido de arriba no los toca —
        // pero estos rigs suelen traer su propia cámara en tercera persona colgando del prefab, que
        // competiría con la Cámara de Estudio real de esta escena si se queda activa.
        foreach (var cam in go.GetComponentsInChildren<Camera>(true))
        {
            UnityEngine.Object.DestroyImmediate(cam, true);
            destroyedAvOutputs++;
        }
        foreach (var listener in go.GetComponentsInChildren<AudioListener>(true))
        {
            UnityEngine.Object.DestroyImmediate(listener, true);
            destroyedAvOutputs++;
        }

        Debug.Log($"[PromoStudioSceneBuilder] '{go.name}': {destroyedScripts} script(s), {destroyedPhysics} componente(s) " +
                  $"de física y {destroyedAvOutputs} cámara(s)/listener(s) eliminados antes de activarlo " +
                  $"(quedan Animator/Renderers y {preserved.Count} script(s) preservado(s) de la allowlist: " +
                  $"{(preserved.Count > 0 ? string.Join(", ", preserved.ConvertAll(mb => mb.GetType().Name)) : "ninguno")}).");
    }

    static bool IsPreservedBehaviour(MonoBehaviour mb)
    {
        if (mb == null) return false;
        foreach (var t in PreservedBehaviourTypes)
            if (t.IsInstanceOfType(mb)) return true;
        return false;
    }

    /// Vuelca en 'into' todos los tipos que los scripts de 'source' declaran como obligatorios vía
    /// [RequireComponent] (hasta 3 por atributo, que es el máximo que admite Unity).
    static void CollectRequiredTypes(System.Collections.Generic.List<MonoBehaviour> source,
                                     System.Collections.Generic.HashSet<Type> into)
    {
        foreach (var mb in source)
            foreach (var attr in mb.GetType().GetCustomAttributes(typeof(RequireComponent), true))
            {
                var rc = (RequireComponent)attr;
                if (rc.m_Type0 != null) into.Add(rc.m_Type0);
                if (rc.m_Type1 != null) into.Add(rc.m_Type1);
                if (rc.m_Type2 != null) into.Add(rc.m_Type2);
            }
    }

    // ── Utilidades ───────────────────────────────────────────────────────────────────────────────

    static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        string parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolderExists(parent);

        AssetDatabase.CreateFolder(parent, leaf);
    }

    /// <summary>Busca un GameObject por nombre exacto en TODA la escena cargada, incluidos los inactivos
    /// (GameObject.Find de Unity ignora los inactivos, por eso no se usa aquí directamente).</summary>
    static GameObject FindByNameIncludingInactive(string name)
    {
        var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (var t in all)
            if (t.name == name)
                return t.gameObject;
        return null;
    }
}
