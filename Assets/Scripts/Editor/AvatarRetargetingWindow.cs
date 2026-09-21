using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ajusta el RETARGETING del avatar humanoide sin tener que entrar en Rig ▸ Configure ▸
/// Muscles &amp; Settings, que es una pantalla aparte, modal y fácil de dejar a medias.
///
/// EL PROBLEMA QUE RESUELVE (ver INC-223 e INC-224). Las animaciones del juego vienen de un pack
/// hecho sobre un humano de proporciones normales, y los personajes son chibis con la cabeza
/// ocupando media altura. Unity retargetea en espacio de músculo NORMALIZADO: "el cuello va al 80%
/// de su recorrido" se aplica igual a los dos cuerpos, y en el chibi ese 80% es una postura rota.
/// De ahí los dos síntomas: el cuello que parece que se parte (Fear01) y las manos que se meten
/// dentro del torso al reírse o saludar.
///
/// El arreglo es recortar el RECORRIDO de esos huesos en el avatar: si el cuello no puede girar
/// tanto, ninguna animación podrá romperlo, ni las de hoy ni las que se añadan mañana. Es un
/// ajuste del avatar, así que vale para TODOS los clips a la vez.
///
/// Los rangos NO se inventan: se leen los valores por defecto de Unity con HumanTrait y se
/// multiplican por el porcentaje elegido. Por eso la ventana puede enseñar, músculo a músculo y
/// con su nombre real, en cuántos grados se va a quedar cada uno.
/// </summary>
public class AvatarRetargetingWindow : EditorWindow
{
    /// El único avatar humanoide del juego: lo comparten Will, Eldran y todos los NPCs humanos.
    /// Los otros dos avatares que aparecen en los prefabs son de arma (arco y flechas) y son
    /// Generic, así que no tienen músculos que ajustar.
    private const string AvatarFbxPath =
        "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/SwordAndShield/Idle_Battle_SwordAndShiled.fbx";

    private static readonly string[] BonesCuello  = { "Neck", "Head" };
    private static readonly string[] BonesBrazos  = { "LeftShoulder", "RightShoulder",
                                                      "LeftUpperArm", "RightUpperArm",
                                                      "LeftLowerArm", "RightLowerArm" };
    private static readonly string[] BonesColumna = { "Spine", "Chest", "UpperChest" };

    private UnityEngine.Object _fbx;
    private float _cuello  = 0.55f;
    private float _brazos  = 0.70f;
    private float _columna = 1f;
    private bool  _translationDoF;
    private bool  _cargado;
    private bool  _verDetalle;
    private Vector2 _scroll;

    // ── Probador ─────────────────────────────────────────────────────────────────────────────
    private const string PersonajePruebaPath = "Assets/_NPCs/Eldran.prefab";
    private const string NombreObjetoPrueba  = "~PRUEBA DE RETARGETING (temporal)";

    /// Las animaciones que dan problemas, que son las que hay que mirar. Se pueden cambiar a mano
    /// en el desplegable de abajo si hace falta probar otra.
    private static readonly string[] ClipsDeProblema =
    {
        "Laugh01", "HandWave01", "HandWave02", "Beg01", "Fear01",
        "Cheer01", "Cheer02", "Angry02", "Question01", "Talk01",
    };

    private GameObject    _personaje;
    private Animator      _animPrueba;
    private int           _clipElegido;
    private AnimationClip _clipPreview;
    private float         _tiempoPreview;
    private double        _ultimoTick;
    private bool          _reproduciendo;

    [MenuItem("El Sendero/Personajes/Retargeting del avatar (cuello y brazos)")]
    public static void Open()
    {
        var w = GetWindow<AvatarRetargetingWindow>("Retargeting del avatar");
        w.minSize = new Vector2(430, 430);
        w.Show();
    }

    private void OnEnable() => Cargar();

    private void Cargar()
    {
        if (_fbx == null)
            _fbx = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AvatarFbxPath);

        var importer = Importer();
        if (importer == null) return;

        _translationDoF = importer.humanDescription.hasTranslationDoF;
        _cargado = true;
    }

    private ModelImporter Importer()
    {
        if (_fbx == null) return null;
        string path = AssetDatabase.GetAssetPath(_fbx);
        return string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path) as ModelImporter;
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.HelpBox(
            "Recorta cuánto se pueden mover el cuello, la cabeza y los brazos de TODOS los " +
            "personajes.\n\n" +
            "Menos recorrido = las animaciones del pack (hechas para un cuerpo normal) dejan de " +
            "romper un cuerpo chibi: ni cuellos imposibles ni manos dentro del torso.\n\n" +
            "Se puede volver al estado original en cualquier momento con el botón de abajo.",
            MessageType.Info);

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        _fbx = EditorGUILayout.ObjectField("Avatar (FBX)", _fbx, typeof(UnityEngine.Object), false);
        if (EditorGUI.EndChangeCheck()) Cargar();

        var importer = Importer();
        if (importer == null)
        {
            EditorGUILayout.HelpBox(
                "Ese archivo no es un modelo importable. El avatar del juego es:\n" + AvatarFbxPath,
                MessageType.Error);
            EditorGUILayout.EndScrollView();
            return;
        }

        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            EditorGUILayout.HelpBox(
                "Ese modelo no está importado como Humanoid, así que no tiene músculos que ajustar.",
                MessageType.Error);
            EditorGUILayout.EndScrollView();
            return;
        }

        if (!_cargado) Cargar();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Recorrido permitido", EditorStyles.boldLabel);

        _cuello  = Porcentaje("Cuello y cabeza", _cuello,
            "Lo que arregla el 'parece que se le parte el cuello'. 100% = rango humano completo.");
        _brazos  = Porcentaje("Brazos y hombros", _brazos,
            "Lo que evita que la mano se meta en el cuerpo al reírse o saludar.");
        _columna = Porcentaje("Columna y pecho", _columna,
            "Tócalo solo si el torso también se dobla raro. Déjalo al 100% si no.");

        EditorGUILayout.Space();
        _translationDoF = EditorGUILayout.Toggle(
            new GUIContent("Translation DoF",
                "Debe estar DESMARCADO en estos personajes. Marcado, el retargeting transfiere " +
                "también desplazamientos de pecho, hombros y cuello, y en un cuerpo con la cabeza " +
                "a media altura eso es un cuello que se estira."),
            _translationDoF);

        if (_translationDoF)
            EditorGUILayout.HelpBox("Recomendado: desmárcalo.", MessageType.Warning);

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
            if (GUILayout.Button("Aplicar", GUILayout.Height(30)))
                Aplicar(importer);
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Volver a los valores originales", GUILayout.Height(30)))
                Restaurar(importer);
        }

        EditorGUILayout.Space();
        DibujarProbador();

        EditorGUILayout.Space();
        _verDetalle = EditorGUILayout.Foldout(_verDetalle,
            "Ver en qué grados se va a quedar cada hueso", true);
        if (_verDetalle) DibujarDetalle();

        EditorGUILayout.EndScrollView();
    }

    private static float Porcentaje(string etiqueta, float valor, string ayuda)
    {
        valor = EditorGUILayout.Slider(new GUIContent(etiqueta, ayuda), valor, 0.2f, 1f);
        EditorGUILayout.LabelField(" ", $"{Mathf.RoundToInt(valor * 100f)}% del recorrido normal",
            EditorStyles.miniLabel);
        return valor;
    }

    /// Devuelve el porcentaje que le toca a un hueso, o -1 si este ajuste no lo toca.
    private float FactorDe(string humanName)
    {
        if (Array.IndexOf(BonesCuello,  humanName) >= 0) return _cuello;
        if (Array.IndexOf(BonesBrazos,  humanName) >= 0) return _brazos;
        if (Array.IndexOf(BonesColumna, humanName) >= 0) return _columna;
        return -1f;
    }

    private void DibujarDetalle()
    {
        EditorGUI.indentLevel++;
        foreach (string hueso in Concat(BonesCuello, BonesBrazos, BonesColumna))
        {
            float f = FactorDe(hueso);
            int bone = Array.IndexOf(HumanTrait.BoneName, hueso);
            if (bone < 0) continue;

            EditorGUILayout.LabelField(hueso, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int dof = 0; dof < 3; dof++)
            {
                int muscle = HumanTrait.MuscleFromBone(bone, dof);
                if (muscle < 0) continue;

                float min = HumanTrait.GetMuscleDefaultMin(muscle) * f;
                float max = HumanTrait.GetMuscleDefaultMax(muscle) * f;
                EditorGUILayout.LabelField(HumanTrait.MuscleName[muscle],
                    $"{min:0.#}° a {max:0.#}°");
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
    }

    private static IEnumerable<string> Concat(params string[][] listas)
    {
        foreach (var l in listas)
            foreach (string s in l)
                yield return s;
    }

    // ── Probador ─────────────────────────────────────────────────────────────────────────────

    private void DibujarProbador()
    {
        EditorGUILayout.LabelField("Probar sin entrar en Play", EditorStyles.boldLabel);

        if (_personaje == null)
        {
            EditorGUILayout.HelpBox(
                "Pon un personaje de prueba y reproduce una animación para ver cómo queda.\n\n" +
                "Aparece en la ventana Scene, es temporal y NO se guarda en la escena: al quitarlo " +
                "no queda rastro.",
                MessageType.None);

            if (GUILayout.Button("1 · Poner personaje de prueba", GUILayout.Height(26)))
                PonerPersonaje();
            return;
        }

        _clipElegido = EditorGUILayout.Popup("Animación", _clipElegido, ClipsDeProblema);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(_reproduciendo ? "⏸ Parar" : "2 · ▶ Reproducir", GUILayout.Height(26)))
            {
                if (_reproduciendo) PararPreview();
                else                ReproducirClip(ClipsDeProblema[_clipElegido]);
            }

            if (GUILayout.Button("Enfocar en la Scene", GUILayout.Height(26)))
                Enfocar();
        }

        if (_reproduciendo)
            EditorGUILayout.HelpBox(
                "Mira la ventana Scene. Mueve los sliders de arriba, dale a Aplicar, y la animación " +
                "sigue corriendo con los valores nuevos: se nota al momento.",
                MessageType.Info);

        if (GUILayout.Button("Quitar personaje de prueba"))
            QuitarPersonaje();
    }

    private void PonerPersonaje()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PersonajePruebaPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Probador",
                "No encuentro el personaje de prueba en:\n" + PersonajePruebaPath, "Vale");
            return;
        }

        _personaje = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (_personaje == null) return;

        _personaje.name = NombreObjetoPrueba;
        // No se guarda con la escena pase lo que pase: esto es un objeto de usar y tirar.
        _personaje.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        var vista = SceneView.lastActiveSceneView;
        if (vista != null)
            _personaje.transform.position = vista.pivot;

        _animPrueba = _personaje.GetComponentInChildren<Animator>(true);

        Selection.activeGameObject = _personaje;
        Enfocar();
    }

    private void Enfocar()
    {
        if (_personaje == null) return;
        Selection.activeGameObject = _personaje;
        var vista = SceneView.lastActiveSceneView;
        if (vista != null) vista.FrameSelected();
    }

    private void QuitarPersonaje()
    {
        PararPreview();
        if (_personaje != null) DestroyImmediate(_personaje);
        _personaje = null;
        _animPrueba = null;
    }

    private void ReproducirClip(string estado)
    {
        if (_animPrueba == null)
        {
            EditorUtility.DisplayDialog("Probador",
                "El personaje de prueba no tiene Animator, así que no hay nada que reproducir.", "Vale");
            return;
        }

        var clip = BuscarClip(estado);
        if (clip == null)
        {
            EditorUtility.DisplayDialog("Probador",
                $"No he encontrado la animación '{estado}' en el Animator del personaje.", "Vale");
            return;
        }

        PararPreview();

        if (!AnimationMode.InAnimationMode())
            AnimationMode.StartAnimationMode();

        _clipPreview   = clip;
        _tiempoPreview = 0f;
        _ultimoTick    = EditorApplication.timeSinceStartup;
        _reproduciendo = true;

        EditorApplication.update += Tick;
        Muestrear();
    }

    /// Busca el AnimationClip de un estado recorriendo el controller del personaje, sin depender
    /// de en qué capa viva.
    private AnimationClip BuscarClip(string estado)
    {
        var controller = _animPrueba.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (controller == null) return null;

        foreach (var capa in controller.layers)
        {
            var encontrado = BuscarEnMaquina(capa.stateMachine, estado);
            if (encontrado != null) return encontrado;
        }
        return null;
    }

    private static AnimationClip BuscarEnMaquina(UnityEditor.Animations.AnimatorStateMachine maquina,
                                                 string estado)
    {
        if (maquina == null) return null;

        foreach (var hijo in maquina.states)
            if (hijo.state != null && hijo.state.name == estado)
                return ResolverClip(hijo.state.motion);

        foreach (var sub in maquina.stateMachines)
        {
            var encontrado = BuscarEnMaquina(sub.stateMachine, estado);
            if (encontrado != null) return encontrado;
        }
        return null;
    }

    private static AnimationClip ResolverClip(Motion motion)
    {
        if (motion is AnimationClip clip) return clip;

        if (motion is UnityEditor.Animations.BlendTree arbol)
        {
            foreach (var hijo in arbol.children)
            {
                var resuelto = ResolverClip(hijo.motion);
                if (resuelto != null) return resuelto;
            }
        }
        return null;
    }

    private void Tick()
    {
        if (_clipPreview == null || _personaje == null)
        {
            PararPreview();
            return;
        }

        double ahora = EditorApplication.timeSinceStartup;
        _tiempoPreview += (float)(ahora - _ultimoTick);
        _ultimoTick = ahora;

        float duracion = Mathf.Max(0.01f, _clipPreview.length);
        _tiempoPreview %= duracion;   // en bucle: así se puede mirar con calma

        Muestrear();
        Repaint();
    }

    private void Muestrear()
    {
        if (_clipPreview == null || _animPrueba == null) return;

        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(_animPrueba.gameObject, _clipPreview, _tiempoPreview);
        AnimationMode.EndSampling();
        SceneView.RepaintAll();
    }

    private void PararPreview()
    {
        EditorApplication.update -= Tick;
        _reproduciendo = false;
        _clipPreview = null;

        if (AnimationMode.InAnimationMode())
            AnimationMode.StopAnimationMode();
    }

    private void OnDisable()
    {
        // La ventana se cierra o Unity recompila: no dejar ni el modo animación puesto ni un
        // personaje suelto en la escena de alguien.
        QuitarPersonaje();
    }

    private void Aplicar(ModelImporter importer)
    {
        var descripcion = importer.humanDescription;
        var huesos = descripcion.human;
        if (huesos == null || huesos.Length == 0)
        {
            EditorUtility.DisplayDialog("Retargeting del avatar",
                "Este modelo no trae el mapa de huesos humanoide, así que no hay nada que ajustar.",
                "Vale");
            return;
        }

        int tocados = 0;

        for (int i = 0; i < huesos.Length; i++)
        {
            float f = FactorDe(huesos[i].humanName);
            if (f < 0f) continue;

            var limite = huesos[i].limit;

            if (f >= 0.999f)
            {
                // Al 100% no se guarda un límite propio: se deja el de Unity. Así el asset no
                // arrastra números que no hacen nada, y el hueso vuelve a ser "por defecto".
                limite.useDefaultValues = true;
            }
            else
            {
                int bone = Array.IndexOf(HumanTrait.BoneName, huesos[i].humanName);
                if (bone < 0) continue;

                Vector3 min = Vector3.zero;
                Vector3 max = Vector3.zero;

                // Los tres grados de libertad de cada hueso van, en orden, a x/y/z del límite.
                // Los valores de partida son los de Unity (HumanTrait), NO números inventados:
                // esto solo los estrecha.
                for (int dof = 0; dof < 3; dof++)
                {
                    int muscle = HumanTrait.MuscleFromBone(bone, dof);
                    if (muscle < 0) continue;

                    min[dof] = HumanTrait.GetMuscleDefaultMin(muscle) * f;
                    max[dof] = HumanTrait.GetMuscleDefaultMax(muscle) * f;
                }

                limite.min = min;
                limite.max = max;
                limite.useDefaultValues = false;
            }

            huesos[i].limit = limite;
            tocados++;
        }

        descripcion.human = huesos;
        descripcion.hasTranslationDoF = _translationDoF;

        importer.humanDescription = descripcion;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        _cargado = false;

        // Si hay una animación corriendo, se vuelve a muestrear con el avatar ya reimportado:
        // el cambio se ve sin tener que darle otra vez a reproducir.
        if (_reproduciendo) Muestrear();

        Debug.Log($"[Retargeting] Avatar '{System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(_fbx))}': " +
                  $"{tocados} huesos ajustados — cuello {Mathf.RoundToInt(_cuello * 100)}%, " +
                  $"brazos {Mathf.RoundToInt(_brazos * 100)}%, " +
                  $"columna {Mathf.RoundToInt(_columna * 100)}%, " +
                  $"Translation DoF {(_translationDoF ? "activado" : "desactivado")}. " +
                  "Afecta a todos los personajes que usan este avatar.", _fbx);
    }

    private void Restaurar(ModelImporter importer)
    {
        if (!EditorUtility.DisplayDialog("Volver a los valores originales",
                "Se devuelven todos los huesos al rango por defecto de Unity y se vuelve a activar " +
                "Translation DoF, que es como venía el pack.\n\n¿Seguro?", "Sí, restaurar", "Cancelar"))
            return;

        var descripcion = importer.humanDescription;
        var huesos = descripcion.human;

        if (huesos != null)
        {
            for (int i = 0; i < huesos.Length; i++)
            {
                var limite = huesos[i].limit;
                limite.useDefaultValues = true;
                huesos[i].limit = limite;
            }
            descripcion.human = huesos;
        }

        descripcion.hasTranslationDoF = true;

        importer.humanDescription = descripcion;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        _cuello = _brazos = _columna = 1f;
        _cargado = false;

        Debug.Log("[Retargeting] Avatar devuelto a los valores originales del pack.", _fbx);
    }
}
