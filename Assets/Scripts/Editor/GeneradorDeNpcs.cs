#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Random = UnityEngine.Random;

/// Generador de NPCs aleatorios (INC-437).
///
/// Los NPCs del pueblo usan el pack modular «RPG Tiny Hero Duo»: cada personaje completo lleva
/// dentro TODAS las piezas (20 cuerpos, 13 peinados, 12 ojos, 12 bocas, sombreros, accesorios…)
/// y solo tiene encendidas las suyas. Cambiar de aspecto es encender otras. Esta ventana lo hace
/// al azar, con reglas de «gente del pueblo» (sin armas, armaduras ni máscaras de ninja).
///
/// Dos usos:
///  1. Aleatorizar el NPC seleccionado (en la escena o en modo prefab), las veces que haga falta
///     hasta que guste. Se deshace con Ctrl+Z.
///  2. Crear un NPC nuevo: copia un prefab «base» completo, le da un aspecto al azar y, si se
///     indica, le pone el comportamiento de otro NPC (su diálogo, su tienda, su marcador…).
///     Sirve para dar cara propia a un NPC que es copia de otro (Tomasa, copia de Patricia).
///
/// OJO: un NPC «recortado» (con solo sus piezas, como Patricia) no se puede aleatorizar: le faltan
/// las demás piezas. Para esos, usar el modo 2 con una base completa y el recortado de plantilla.
public class GeneradorDeNpcs : EditorWindow
{
    enum Genero { Cualquiera, Chica, Chico }

    // ── Piezas ───────────────────────────────────────────────────────────────────────────────
    static readonly Regex RxCuerpo    = new(@"^Body\d+$");
    static readonly Regex RxCapa      = new(@"^Cloak\d+$");
    static readonly Regex RxPelo      = new(@"^Hair\d+$");
    static readonly Regex RxSombrero  = new(@"^Hat\d+$");
    static readonly Regex RxOjos      = new(@"^Eye\d+$");
    static readonly Regex RxCejas     = new(@"^Eyebrow\d+$");
    static readonly Regex RxBoca      = new(@"^Mouth\d+$");
    static readonly Regex RxCabeza    = new(@"^Head\d+_\w+$");
    static readonly Regex RxArmadura  = new(@"^HeadArmor\d+$");
    static readonly Regex RxAccesorio = new(@"^AC\d+_\w+$");
    static readonly Regex RxArma      = new(@"^(Bow\d+|OHS\d+_\w+|Shield\d+|Arrows)$");

    // Accesorios que no pegan con un vecino normal (se permiten con «Toques de fantasía»).
    static readonly string[] AccesoriosDeFantasia = { "AC01_", "AC02_", "AC03_NightVision", "AC04_",
        "AC05_", "AC06_", "AC07_", "AC08_" };
    static readonly string[] Bigotes = { "AC10_", "AC11_" };

    static readonly string[] NombresDeChica = { "Amparo", "Aurora", "Candela", "Casilda", "Consuelo", "Dolores",
        "Engracia", "Filomena", "Herminia", "Inés", "Jacinta", "Leonor", "Lucía", "Marcela", "Petra",
        "Pilar", "Remedios", "Rosario", "Tecla", "Visitación" };
    static readonly string[] NombresDeChico = { "Anselmo", "Basilio", "Benito", "Ceferino", "Cosme", "Damián",
        "Eusebio", "Fermín", "Gaspar", "Hilario", "Isidro", "Lorenzo", "Macario", "Nicanor", "Pascual",
        "Plácido", "Remigio", "Sebastián", "Tadeo", "Venancio" };

    // ── Opciones ─────────────────────────────────────────────────────────────────────────────
    Genero _genero = Genero.Chica;
    bool _fantasia;
    float _probSombrero = 0.2f;
    float _probCapa = 0.2f;
    float _probAccesorio = 0.15f;
    float _probBigote = 0.3f;

    GameObject _base;
    GameObject _plantilla;
    string _nombre = "";
    string _carpeta = "Assets/_NPCs/Pueblo/Generados";
    bool _quitarLoQueLaPlantillaNoTiene = true;
    bool _sustituirEnRoster = true;
    Vector2 _scroll;

    [MenuItem("El Sendero/NPCs/Generador de NPCs")]
    static void Abrir() => GetWindow<GeneradorDeNpcs>("Generador de NPCs");

    void OnEnable()
    {
        if (_base == null) _base = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_NPCs/Sofia.prefab");
        if (string.IsNullOrEmpty(_nombre)) _nombre = NombreAlAzar();
    }

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("Reglas", EditorStyles.boldLabel);
        _genero = (Genero)EditorGUILayout.EnumPopup("Género", _genero);
        _fantasia = EditorGUILayout.Toggle(new GUIContent("Toques de fantasía",
            "Permite cuernos, coronas, orejas de conejo, máscaras… Apagado: vecinos normales."), _fantasia);
        _probSombrero = EditorGUILayout.Slider("Sombrero", _probSombrero, 0f, 1f);
        _probCapa = EditorGUILayout.Slider("Capa", _probCapa, 0f, 1f);
        _probAccesorio = EditorGUILayout.Slider("Gafas / accesorio", _probAccesorio, 0f, 1f);
        if (_genero != Genero.Chica) _probBigote = EditorGUILayout.Slider("Bigote", _probBigote, 0f, 1f);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("1 · Cambiar el NPC seleccionado", EditorStyles.boldLabel);
        var sel = Selection.activeGameObject;
        EditorGUILayout.HelpBox(sel == null
            ? "Selecciona un NPC en la escena (o abre su prefab) y pulsa un botón. Ctrl+Z lo deshace."
            : $"Seleccionado: {sel.name}. {DescribirAspecto(sel)}", MessageType.None);
        using (new EditorGUI.DisabledScope(sel == null))
        {
            if (GUILayout.Button("Todo al azar", GUILayout.Height(28))) AleatorizarSeleccionado(Parte.Todo);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Pelo")) AleatorizarSeleccionado(Parte.Pelo);
            if (GUILayout.Button("Ropa")) AleatorizarSeleccionado(Parte.Ropa);
            if (GUILayout.Button("Cara")) AleatorizarSeleccionado(Parte.Cara);
            if (GUILayout.Button("Complementos")) AleatorizarSeleccionado(Parte.Complementos);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("2 · Crear un NPC nuevo", EditorStyles.boldLabel);
        _base = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Base (completa)",
            "Un NPC con TODAS las piezas del pack dentro (Sofia, Nora, Tabernera, MC01…)."), _base, typeof(GameObject), false);
        _plantilla = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Comportamiento de (opcional)",
            "Se le copian su diálogo, tienda, marcador de misión, etc. Deja vacío para un vecino sin más."),
            _plantilla, typeof(GameObject), false);
        if (_plantilla != null)
        {
            _quitarLoQueLaPlantillaNoTiene = EditorGUILayout.Toggle(new GUIContent("Quitar lo que no tenga",
                "Quita los scripts de la base que la plantilla no tiene (p. ej. que pasee por el pueblo si la plantilla es un vendedor quieto)."),
                _quitarLoQueLaPlantillaNoTiene);
            _sustituirEnRoster = EditorGUILayout.Toggle(new GUIContent("Sustituirlo en los rosters",
                "Donde un roster de NPCs saque a la plantilla, sacará al nuevo."), _sustituirEnRoster);
        }
        EditorGUILayout.BeginHorizontal();
        _nombre = EditorGUILayout.TextField("Nombre", _nombre);
        if (GUILayout.Button("Otro", GUILayout.Width(44))) _nombre = NombreAlAzar();
        EditorGUILayout.EndHorizontal();
        _carpeta = EditorGUILayout.TextField("Carpeta", _carpeta);

        using (new EditorGUI.DisabledScope(_base == null || string.IsNullOrWhiteSpace(_nombre)))
            if (GUILayout.Button("Crear NPC", GUILayout.Height(28))) CrearNpc();

        EditorGUILayout.EndScrollView();
    }

    // ── 1: seleccionado ──────────────────────────────────────────────────────────────────────
    enum Parte { Todo, Pelo, Ropa, Cara, Complementos }

    void AleatorizarSeleccionado(Parte parte)
    {
        var go = Selection.activeGameObject;
        if (go == null) return;
        var raiz = RaizDelNpc(go);
        var piezas = Piezas(raiz);
        if (!piezas.Any(p => RxPelo.IsMatch(p.name)) || piezas.Count(p => RxCuerpo.IsMatch(p.name)) < 2)
        {
            EditorUtility.DisplayDialog("Generador de NPCs",
                $"'{raiz.name}' no trae el juego completo de piezas (es un NPC «recortado»). " +
                "Crea uno nuevo en el apartado 2 con una base completa y este de plantilla.", "Vale");
            return;
        }
        Undo.SetCurrentGroupName("Aspecto al azar");
        int grupo = Undo.GetCurrentGroup();
        Aleatorizar(piezas, parte, registrarUndo: true);
        Undo.CollapseUndoOperations(grupo);
        foreach (var p in piezas) PrefabUtility.RecordPrefabInstancePropertyModifications(p);
        if (raiz.scene.IsValid()) EditorSceneManager.MarkSceneDirty(raiz.scene);
        Repaint();
    }

    static GameObject RaizDelNpc(GameObject go)
    {
        var raizPrefab = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
        if (raizPrefab != null) return raizPrefab;
        var t = go.transform;
        while (t.parent != null && Piezas(t.gameObject).Count == 0) t = t.parent;
        return t.root.gameObject;
    }

    // ── 2: nuevo ─────────────────────────────────────────────────────────────────────────────
    void CrearNpc()
    {
        string rutaBase = AssetDatabase.GetAssetPath(_base);
        if (string.IsNullOrEmpty(rutaBase) || !rutaBase.EndsWith(".prefab"))
        {
            EditorUtility.DisplayDialog("Generador de NPCs", "La base tiene que ser un prefab del proyecto.", "Vale");
            return;
        }
        if (!AssetDatabase.IsValidFolder(_carpeta)) CrearCarpeta(_carpeta);
        string nombreArchivo = string.Join("_", _nombre.Trim().Split(Path.GetInvalidFileNameChars()));
        string ruta = AssetDatabase.GenerateUniqueAssetPath($"{_carpeta}/{nombreArchivo}.prefab");
        if (!AssetDatabase.CopyAsset(rutaBase, ruta))
        {
            EditorUtility.DisplayDialog("Generador de NPCs", $"No se pudo copiar {rutaBase}.", "Vale");
            return;
        }

        var raiz = PrefabUtility.LoadPrefabContents(ruta);
        GameObject plantillaRaiz = null;
        try
        {
            var piezas = Piezas(raiz);
            if (!piezas.Any(p => RxPelo.IsMatch(p.name)) || piezas.Count(p => RxCuerpo.IsMatch(p.name)) < 2)
            {
                EditorUtility.DisplayDialog("Generador de NPCs",
                    $"'{_base.name}' no trae el juego completo de piezas. Usa de base un NPC completo (Sofia, Nora, Tabernera, MC01…).", "Vale");
                PrefabUtility.UnloadPrefabContents(raiz); raiz = null;
                AssetDatabase.DeleteAsset(ruta);
                return;
            }

            raiz.name = _nombre.Trim();
            Aleatorizar(piezas, Parte.Todo, registrarUndo: false);

            if (_plantilla != null)
            {
                plantillaRaiz = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(_plantilla));
                CopiarComportamiento(plantillaRaiz, raiz, _quitarLoQueLaPlantillaNoTiene);
            }

            PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
        }
        finally
        {
            if (raiz != null) PrefabUtility.UnloadPrefabContents(raiz);
            if (plantillaRaiz != null) PrefabUtility.UnloadPrefabContents(plantillaRaiz);
        }

        var nuevo = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        int sustituidos = 0;
        if (_plantilla != null && _sustituirEnRoster) sustituidos = SustituirEnRosters(_plantilla, nuevo);

        EditorGUIUtility.PingObject(nuevo);
        Selection.activeObject = nuevo;
        Debug.Log($"[Generador de NPCs] Creado '{ruta}'" +
                  (_plantilla != null ? $" con el comportamiento de '{_plantilla.name}'" : "") +
                  (sustituidos > 0 ? $"; sustituye a '{_plantilla.name}' en {sustituidos} roster(s)." : "."));
        _nombre = NombreAlAzar();
    }

    static void CrearCarpeta(string ruta)
    {
        var partes = ruta.Split('/');
        string actual = partes[0];
        for (int i = 1; i < partes.Length; i++)
        {
            string siguiente = $"{actual}/{partes[i]}";
            if (!AssetDatabase.IsValidFolder(siguiente)) AssetDatabase.CreateFolder(actual, partes[i]);
            actual = siguiente;
        }
    }

    /// Scripts que ya pone la base y que no dependen de quién es el NPC.
    static readonly HashSet<string> ScriptsDelCuerpo = new() { "NPCSimpleAnimator", "NPCEmotionController", "BillboardUI", "ModularAutoBuilder" };

    /// Copia al NPC nuevo los scripts de la raíz de la plantilla: diálogo (Interactable), tienda,
    /// señales, marcador de misión… Las referencias a objetos de dentro de la plantilla (su globo
    /// de «pulsa para hablar», por ejemplo) no se copian: el NPC nuevo conserva los suyos.
    static void CopiarComportamiento(GameObject plantilla, GameObject destino, bool quitarSobrantes)
    {
        var tiposPlantilla = new HashSet<Type>();
        foreach (var comp in plantilla.GetComponents<Component>())
        {
            if (comp == null || comp is Transform || comp is Animator || comp is Renderer) continue;
            var tipo = comp.GetType();
            tiposPlantilla.Add(tipo);
            if (ScriptsDelCuerpo.Contains(tipo.Name)) continue;
            if (!(comp is MonoBehaviour) && !(comp is UnityEngine.AI.NavMeshAgent) && !(comp is Collider)) continue;

            var dst = destino.GetComponent(tipo) ?? destino.AddComponent(tipo);
            if (dst == null) continue;
            var so = new SerializedObject(comp);
            var sd = new SerializedObject(dst);
            var it = so.GetIterator();
            bool entrar = true;
            while (it.NextVisible(entrar))
            {
                entrar = false;
                if (it.name == "m_Script") continue;
                if (it.propertyType == SerializedPropertyType.ObjectReference && EsDeDentro(it.objectReferenceValue, plantilla))
                    continue;
                sd.CopyFromSerializedProperty(it);
            }
            sd.ApplyModifiedPropertiesWithoutUndo();
        }

        if (!quitarSobrantes) return;
        // Dos pasadas: si un script requiere a otro, se quita primero el que depende.
        for (int pasada = 0; pasada < 2; pasada++)
            foreach (var comp in destino.GetComponents<Component>())
            {
                if (comp == null || comp is Transform || comp is Animator || comp is Renderer || comp is Collider) continue;
                if (!(comp is MonoBehaviour) && !(comp is UnityEngine.AI.NavMeshAgent)) continue;
                var tipo = comp.GetType();
                if (tiposPlantilla.Contains(tipo) || ScriptsDelCuerpo.Contains(tipo.Name)) continue;
                if (PuedeQuitarse(destino, comp)) UnityEngine.Object.DestroyImmediate(comp, true);
            }
    }

    static bool PuedeQuitarse(GameObject go, Component comp)
    {
        foreach (var otro in go.GetComponents<Component>())
        {
            if (otro == null || otro == comp) continue;
            foreach (RequireComponent req in otro.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                if ((req.m_Type0 != null && req.m_Type0.IsInstanceOfType(comp)) ||
                    (req.m_Type1 != null && req.m_Type1.IsInstanceOfType(comp)) ||
                    (req.m_Type2 != null && req.m_Type2.IsInstanceOfType(comp)))
                    return false;
        }
        return true;
    }

    static bool EsDeDentro(UnityEngine.Object obj, GameObject raiz)
    {
        if (obj == null) return false;
        var go = obj as GameObject ?? (obj as Component)?.gameObject;
        return go != null && go.transform.IsChildOf(raiz.transform);
    }

    static int SustituirEnRosters(GameObject plantilla, GameObject nuevo)
    {
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:NpcRosterSO"))
        {
            var roster = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (roster == null) continue;
            var so = new SerializedObject(roster);
            var entradas = so.FindProperty("entries");
            if (entradas == null) continue;
            bool cambiado = false;
            for (int i = 0; i < entradas.arraySize; i++)
            {
                var prefab = entradas.GetArrayElementAtIndex(i).FindPropertyRelative("prefab");
                if (prefab != null && prefab.objectReferenceValue == plantilla)
                {
                    prefab.objectReferenceValue = nuevo;
                    cambiado = true; n++;
                }
            }
            if (cambiado) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(roster); }
        }
        if (n > 0) AssetDatabase.SaveAssets();
        return n;
    }

    // ── El azar ──────────────────────────────────────────────────────────────────────────────
    static List<GameObject> Piezas(GameObject raiz)
    {
        var lista = new List<GameObject>();
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            if (t.GetComponent<Renderer>() == null) continue;   // huesos (BowJoint, CloakBone…) fuera
            if (RxCuerpo.IsMatch(n) || RxCapa.IsMatch(n) || RxPelo.IsMatch(n) || RxSombrero.IsMatch(n) ||
                RxOjos.IsMatch(n) || RxCejas.IsMatch(n) || RxBoca.IsMatch(n) || RxCabeza.IsMatch(n) ||
                RxArmadura.IsMatch(n) || RxAccesorio.IsMatch(n) || RxArma.IsMatch(n))
                lista.Add(t.gameObject);
        }
        return lista;
    }

    void Aleatorizar(List<GameObject> piezas, Parte parte, bool registrarUndo)
    {
        void Poner(GameObject g, bool activo)
        {
            if (g.activeSelf == activo) return;
            if (registrarUndo) Undo.RecordObject(g, "Aspecto al azar");
            g.SetActive(activo);
        }
        GameObject UnaDe(IEnumerable<GameObject> opciones)
        {
            var l = opciones.ToList();
            return l.Count == 0 ? null : l[Random.Range(0, l.Count)];
        }
        void Elegir(Regex rx, Func<GameObject, bool> permitido, float probabilidad = 1f)
        {
            var del = piezas.Where(p => rx.IsMatch(p.name)).ToList();
            var elegida = Random.value < probabilidad ? UnaDe(del.Where(permitido)) : null;
            foreach (var p in del) Poner(p, p == elegida);
        }

        // Género: la cabeza lo decide. Si no se toca la cara, se respeta la que tenga.
        var cabezaActual = piezas.FirstOrDefault(p => RxCabeza.IsMatch(p.name) && p.activeSelf);
        bool chico = _genero == Genero.Chico
                     || (_genero == Genero.Cualquiera && (parte == Parte.Todo || parte == Parte.Cara ? Random.value < 0.5f
                         : cabezaActual != null && cabezaActual.name.Contains("_Male")));
        if (_genero == Genero.Chica) chico = false;

        bool todo = parte == Parte.Todo;
        if (todo || parte == Parte.Cara)
        {
            Elegir(RxCabeza, p => p.name == (chico ? "Head01_Male" : "Head02_Female"));
            Elegir(RxOjos, _ => true);
            Elegir(RxBoca, _ => true);
            Elegir(RxCejas, _ => true);
        }
        if (todo || parte == Parte.Pelo) Elegir(RxPelo, _ => true);
        if (todo || parte == Parte.Ropa)
        {
            Elegir(RxCuerpo, _ => true);
            Elegir(RxCapa, _ => true, _probCapa);
        }
        if (todo || parte == Parte.Complementos)
        {
            Elegir(RxSombrero, _ => true, _probSombrero);
            // Un solo accesorio: bigote (solo chicos) o gafas/otros.
            var accesorios = piezas.Where(p => RxAccesorio.IsMatch(p.name)).ToList();
            GameObject acc = null;
            if (chico && Random.value < _probBigote) acc = UnaDe(accesorios.Where(p => Bigotes.Any(b => p.name.StartsWith(b))));
            if (acc == null && Random.value < _probAccesorio)
                acc = UnaDe(accesorios.Where(p => !Bigotes.Any(b => p.name.StartsWith(b)) &&
                                                  (_fantasia || !AccesoriosDeFantasia.Any(f => p.name.StartsWith(f)))));
            foreach (var p in accesorios) Poner(p, p == acc);
        }
        // Gente del pueblo: sin armas ni armaduras, nunca.
        foreach (var p in piezas.Where(p => RxArma.IsMatch(p.name) || RxArmadura.IsMatch(p.name))) Poner(p, false);
    }

    string NombreAlAzar()
    {
        var lista = _genero == Genero.Chico ? NombresDeChico
                  : _genero == Genero.Chica ? NombresDeChica
                  : (Random.value < 0.5f ? NombresDeChica : NombresDeChico);
        return lista[Random.Range(0, lista.Length)];
    }

    static string DescribirAspecto(GameObject go)
    {
        var piezas = Piezas(RaizDelNpc(go)).Where(p => p.activeSelf).Select(p => p.name).ToList();
        return piezas.Count == 0 ? "No tiene piezas del pack modular." : "Lleva: " + string.Join(", ", piezas);
    }
}
#endif
