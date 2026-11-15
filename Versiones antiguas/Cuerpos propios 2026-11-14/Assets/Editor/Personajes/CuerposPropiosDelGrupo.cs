using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Invector.vCharacterController;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Da a Will, Estela y Liam su cuerpo propio (INC-717): un prefab común con el cuerpo como raíz
/// (el controlador y los componentes del jugador, la IA de compañero y <see cref="PilotoDelCuerpo"/>)
/// y una variante por personaje con su ficha, su configuración de NPC, sus piezas, su cara y sus
/// componentes propios, sacados de su prefab de antes. Después sustituye las instancias viejas de
/// las escenas y las referencias de los assets por las nuevas, conservando nombre, padre, posición
/// y las referencias a sus componentes, y deja los prefabs viejos y `_WILL_NPC` en
/// <see cref="CarpetaDeRetirados"/> para llevarlos a Versiones antiguas.
///
/// Las escenas se abren en aditivo y solo se guardan las que este menú ha cambiado; una que ya
/// tenía cambios sin guardar de otro no se toca (sale en el informe).
/// </summary>
public static class CuerposPropiosDelGrupo
{
    const string Base = "Assets/Prefabs/Personajes/_CUERPO_DEL_GRUPO.prefab";
    const string CarpetaNueva = "Assets/Prefabs/Personajes";
    const string CarpetaDeRetirados = "Assets/Prefabs/Personajes/Retirados";
    const string Grupo = "Assets/Prefabs/GrupoDelJugador.prefab";
    const string ViejoWill = "Assets/Prefabs/_WILL.prefab";
    const string ViejoWillNpc = "Assets/Prefabs/_WILL_NPC.prefab";
    const string ViejoEstela = "Assets/Prefabs/_ESTELA.prefab";
    const string ViejoLiam = "Assets/Prefabs/_LIAM.prefab";

    // El menú principal solo enseña figuras de los tres: se desempaquetan (son decorado) en vez de
    // ponerles un cuerpo jugable.
    const string MenuPrincipal = "Assets/Scenes/Systems/MainMenu.unity";

    static readonly string[] Escenas =
    {
        "Assets/Scenes/Worlds/MainWorld.unity",
        MenuPrincipal,
        "Assets/Scenes/Worlds/Cascaditas.unity",
        "Assets/Scenes/Worlds/Chuchelandia.unity",
        "Assets/Scenes/Worlds/Parque.unity",
        "Assets/Scenes/Worlds/Sendero.unity",
        "Assets/Scenes/Test/EscapeSendero.unity",
    };

    // Lo que no pasa al cuerpo común: el relevo y el disfraz retirados, escalar (fuera del
    // diseño) y lo que es del grupo (va en GrupoDelJugador).
    static readonly string[] Fuera = { "ActiveCharacterSwapper", "PlayerClimbingController", "PlayerPresetService" };

    // Lo que lleva el cuerpo lo mueva quien lo mueva.
    static readonly Type[] Compartidos =
    {
        typeof(NPCEmotionController), typeof(Game.Player.FootstepHandler), typeof(ModularAutoBuilder),
        typeof(Personaje), typeof(MuelleDeHuesos), typeof(PilotoDelCuerpo),
    };

    // La IA de compañero común a los tres (de _WILL_NPC); lo propio de cada uno va en su variante.
    static readonly Type[] IaComun =
    {
        typeof(NavMeshAgent), typeof(NPCSimpleAnimator), typeof(Game.NPC.NPCBehaviourManagerV2), typeof(NPCObstacleAvoidance),
    };

    // Componentes propios de un personaje que solo trabajan con la IA.
    static bool SoloConLaIA(Component c)
    {
        var n = c.GetType().Name;
        return n == "ChaserAI" || n == "Interactable" || n == "NPCShieldController" || n == "SenueloDeCombate"
            || n.EndsWith("IdleCommentary", StringComparison.Ordinal);
    }

    struct Personaje_
    {
        public Slot slot;
        public string viejo;      // su prefab de antes (de él salen sus datos)
        public string nombre;     // nombre de la raíz
        public string temporal;   // ruta de la variante hasta ocupar la del viejo
    }

    static readonly Personaje_[] Tres =
    {
        new Personaje_ { slot = Slot.Will, viejo = ViejoWill, nombre = "_WILL", temporal = CarpetaNueva + "/_WILL.prefab" },
        new Personaje_ { slot = Slot.Estela, viejo = ViejoEstela, nombre = "_ESTELA", temporal = CarpetaNueva + "/_ESTELA.prefab" },
        new Personaje_ { slot = Slot.Liam, viejo = ViejoLiam, nombre = "_LIAM", temporal = CarpetaNueva + "/_LIAM.prefab" },
    };

    [MenuItem("El Sendero/Archivo/Personajes: cuerpos propios del grupo (INC-717)")]
    public static void Ejecutar()
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("Cuerpos propios", "Hazlo fuera de Play."); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Base) != null)
        {
            OrdenesDelEditor.Avisar("Cuerpos propios", $"Ya hecho: existe {Base}.");
            return;
        }
        var informe = new StringBuilder();
        try
        {
            if (!Directory(CarpetaNueva) || !Directory(CarpetaDeRetirados)) { informe.AppendLine("No se pudieron crear las carpetas."); return; }
            if (!CrearBase(informe)) return;
            for (int i = 0; i < Tres.Length; i++)
                if (!CrearVariante(Tres[i], informe)) return;
            PasarElPresetAlGrupo(informe);
            AssetDatabase.SaveAssets();

            var mapa = Mapa();
            foreach (var escena in Escenas) MigrarEscena(escena, mapa, informe);
            MigrarAssets(mapa, informe);
            Retirar(informe);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            informe.AppendLine("ERROR: " + e);
        }
        finally
        {
            Debug.Log("[CuerposPropiosDelGrupo] " + informe.ToString().Replace('\n', ' '));
            OrdenesDelEditor.Avisar("Cuerpos propios del grupo", informe.ToString());
        }
    }

    static bool Directory(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return true;
        int i = ruta.LastIndexOf('/');
        if (!Directory(ruta.Substring(0, i))) return false;
        return !string.IsNullOrEmpty(AssetDatabase.CreateFolder(ruta.Substring(0, i), ruta.Substring(i + 1)));
    }

    // ─── Prefab común ─────────────────────────────────────────────────────

    static bool CrearBase(StringBuilder informe)
    {
        var raizWill = PrefabUtility.LoadPrefabContents(ViejoWill);
        var raizNpc = PrefabUtility.LoadPrefabContents(ViejoWillNpc);
        try
        {
            var controlador = raizWill.GetComponentInChildren<vThirdPersonController>(true);
            if (controlador == null) { informe.AppendLine($"{ViejoWill}: sin vThirdPersonController."); return false; }
            var cuerpoViejo = controlador.gameObject;

            // Copia del cuerpo en la misma escena de previsualización, ya como raíz.
            var cuerpo = Object.Instantiate(cuerpoViejo, raizWill.transform);
            cuerpo.transform.SetParent(null, false);
            cuerpo.transform.localPosition = Vector3.zero;
            cuerpo.transform.localRotation = Quaternion.identity;
            cuerpo.name = "_CUERPO_DEL_GRUPO";

            // Lo que llevaba el contenedor (el muelle de la capa) pasa al cuerpo.
            foreach (var c in raizWill.GetComponents<Component>())
            {
                if (c is Transform || c == null) continue;
                var nuevo = CopiarComoNuevo(c, cuerpo);
                Reapuntar(nuevo, cuerpoViejo.transform, cuerpo.transform);
            }

            foreach (var c in cuerpo.GetComponents<MonoBehaviour>())
                if (c != null && Fuera.Contains(c.GetType().Name)) Object.DestroyImmediate(c);

            // La pila del jugador: lo que traía el cuerpo de Will, menos lo compartido.
            var conJugador = new List<Behaviour>();
            foreach (var b in cuerpo.GetComponents<Behaviour>())
                if (b != null && !(b is Animator) && !Compartidos.Contains(b.GetType())) conJugador.Add(b);

            // La IA de compañero común, de _WILL_NPC.
            var conIa = new List<Behaviour>();
            foreach (var tipo in IaComun)
            {
                var origen = raizNpc.GetComponent(tipo);
                if (origen == null) { informe.AppendLine($"{ViejoWillNpc}: falta {tipo.Name}."); continue; }
                var nuevo = cuerpo.GetComponent(tipo);
                if (nuevo == null) nuevo = CopiarComoNuevo(origen, cuerpo);
                CopiarValores(origen, nuevo);
                Reapuntar(nuevo, raizNpc.transform, cuerpo.transform);
                if (nuevo is Behaviour b) conIa.Add(b);
            }

            var piloto = cuerpo.GetComponent<PilotoDelCuerpo>();
            if (piloto == null) piloto = cuerpo.AddComponent<PilotoDelCuerpo>();
            var animador = cuerpo.GetComponent<Animator>();
            bool raizNpc_ = LeerBool(raizNpc.GetComponent<NPCSimpleAnimator>(), "useRootMotionForSpecialAnims");
            ConfigurarPiloto(piloto, conJugador, conIa, null, animador != null && animador.applyRootMotion, raizNpc_, empiezaConJugador: false);
            Encender(conJugador, false);
            Encender(conIa, true);

            PrefabUtility.SaveAsPrefabAsset(cuerpo, Base, out bool ok);
            Object.DestroyImmediate(cuerpo);
            if (!ok) { informe.AppendLine($"No se pudo guardar {Base}."); return false; }
            informe.AppendLine($"Base: {Base} ({conJugador.Count} con el jugador, {conIa.Count} con la IA).");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raizWill);
            PrefabUtility.UnloadPrefabContents(raizNpc);
        }
    }

    // ─── Variantes ────────────────────────────────────────────────────────

    static bool CrearVariante(Personaje_ p, StringBuilder informe)
    {
        var baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Base);
        var raizVieja = PrefabUtility.LoadPrefabContents(p.viejo);
        var raizNpc = p.slot == Slot.Will ? PrefabUtility.LoadPrefabContents(ViejoWillNpc) : null;
        var escena = EditorSceneManager.NewPreviewScene();
        try
        {
            // De dónde salen los datos: Will, de su cuerpo de jugador y de su NPC; Estela y Liam, de su NPC.
            var datos = p.slot == Slot.Will
                ? raizVieja.GetComponentInChildren<vThirdPersonController>(true).transform
                : raizVieja.transform;
            var ia = p.slot == Slot.Will ? raizNpc.transform : raizVieja.transform;

            var cuerpo = (GameObject)PrefabUtility.InstantiatePrefab(baseAsset, escena);
            cuerpo.name = p.nombre;
            cuerpo.layer = datos.gameObject.layer;
            var t = cuerpo.transform;

            // Piezas y huesos: lo que falta se copia; lo que existe toma la pose, el estado y los
            // materiales de su prefab de antes; las piezas que él no tenía se apagan.
            var copiados = new List<GameObject>();
            CopiarJerarquia(datos, t, copiados, saltar: p.slot == Slot.Will ? null : "Canvas");
            if (p.slot != Slot.Will) CopiarJerarquia(ia, t, copiados, saltar: null, soloRaiz: "Canvas");
            ApagarPiezasQueNoTenia(datos, t);
            foreach (var go in copiados)
                foreach (var c in go.GetComponentsInChildren<Component>(true))
                    if (c != null && !(c is Transform)) { Reapuntar(c, datos, t); Reapuntar(c, ia, t); }

            // Componentes que tiene la base: sus valores.
            CopiarValoresSiHay<Personaje>(datos, t);
            CopiarValoresSiHay<Game.Player.FootstepHandler>(datos, t);
            CopiarValoresTodos<NPCEmotionController>(datos, t);
            CopiarValoresSiHay<MuelleDeHuesos>(p.slot == Slot.Will ? raizVieja.transform : datos, t);
            foreach (var tipo in IaComun)
            {
                var origen = ia.GetComponent(tipo);
                var destino = cuerpo.GetComponent(tipo);
                if (origen == null || destino == null) continue;
                CopiarValores(origen, destino);
                Reapuntar(destino, ia, t);
            }

            // Animator: el del jugador con el jugador; si su NPC llevaba otro avatar o controlador,
            // su avatar y su controlador con la IA.
            var animador = cuerpo.GetComponent<Animator>();
            var animadorIa = ia.GetComponent<Animator>();
            RuntimeAnimatorController controladorIa = null;
            if (animador != null && animadorIa != null)
            {
                if (animadorIa.avatar != null && animadorIa.avatar != animador.avatar) animador.avatar = animadorIa.avatar;
                if (animadorIa.runtimeAnimatorController != animador.runtimeAnimatorController) controladorIa = animadorIa.runtimeAnimatorController;
            }

            // Lo propio de cada uno que no está en la base (charla, comentarios, escudo, señuelo,
            // actor narrativo…). Will con la IA no tiene charla: es del grupo.
            var propiasIa = new List<Behaviour>();
            foreach (var c in ia.GetComponents<Component>())
            {
                if (c == null || c is Transform || cuerpo.GetComponent(c.GetType()) != null) continue;
                if (Fuera.Contains(c.GetType().Name)) continue;
                if (p.slot == Slot.Will && c.GetType().Name == "Interactable") continue;
                var nuevo = CopiarComoNuevo(c, cuerpo);
                Reapuntar(nuevo, ia, t);
                if (nuevo is Behaviour b && SoloConLaIA(c) && ((Behaviour)c).enabled) propiasIa.Add(b);
            }
            if (ia != datos)
                foreach (var c in datos.GetComponents<Component>())
                {
                    if (c == null || c is Transform || cuerpo.GetComponent(c.GetType()) != null) continue;
                    if (Fuera.Contains(c.GetType().Name)) continue;
                    var nuevo = CopiarComoNuevo(c, cuerpo);
                    Reapuntar(nuevo, datos, t);
                }

            var piloto = cuerpo.GetComponent<PilotoDelCuerpo>();
            var so = new SerializedObject(piloto);
            var conJugador = Lista(so.FindProperty("conElJugador"));
            var conIa = Lista(so.FindProperty("conLaIA"));
            conIa.AddRange(propiasIa);
            bool empieza = p.slot == Slot.Will;
            ConfigurarPiloto(piloto, conJugador, conIa, controladorIa,
                so.FindProperty("movimientoRaizConElJugador").boolValue,
                LeerBool(ia.GetComponent<NPCSimpleAnimator>(), "useRootMotionForSpecialAnims"), empieza);
            Encender(conJugador, empieza);
            Encender(conIa, !empieza);
            cuerpo.tag = empieza ? "Player" : "Untagged";

            PrefabUtility.SaveAsPrefabAsset(cuerpo, p.temporal, out bool ok);
            if (!ok) { informe.AppendLine($"No se pudo guardar {p.temporal}."); return false; }
            informe.AppendLine($"{p.nombre}: variante en {p.temporal} ({copiados.Count} piezas copiadas, {propiasIa.Count} propias con la IA" +
                               (controladorIa != null ? ", controlador propio con la IA" : "") + ").");
            return true;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(escena);
            PrefabUtility.UnloadPrefabContents(raizVieja);
            if (raizNpc != null) PrefabUtility.UnloadPrefabContents(raizNpc);
        }
    }

    // Recorre el prefab de antes: lo que no está en el cuerpo nuevo se copia (con sus hijos); lo que
    // está, toma su pose, si va encendido y sus materiales.
    static void CopiarJerarquia(Transform origen, Transform destino, List<GameObject> copiados, string saltar, string soloRaiz = null)
    {
        foreach (Transform hijo in origen)
        {
            if (soloRaiz != null && hijo.name != soloRaiz) continue;
            if (saltar != null && hijo.name == saltar) continue;
            var igual = destino.Find(hijo.name);
            if (igual == null)
            {
                var copia = Object.Instantiate(hijo.gameObject, destino);
                copia.name = hijo.name;
                copiados.Add(copia);
                continue;
            }
            igual.localPosition = hijo.localPosition;
            igual.localRotation = hijo.localRotation;
            igual.localScale = hijo.localScale;
            if (igual.gameObject.activeSelf != hijo.gameObject.activeSelf) igual.gameObject.SetActive(hijo.gameObject.activeSelf);
            var r0 = hijo.GetComponent<Renderer>();
            var r1 = igual.GetComponent<Renderer>();
            if (r0 != null && r1 != null) r1.sharedMaterials = r0.sharedMaterials;
            if (soloRaiz == null) CopiarJerarquia(hijo, igual, copiados, saltar);
        }
    }

    // Las piezas del cuerpo común que el personaje no tenía (las de los otros dos) van apagadas.
    static void ApagarPiezasQueNoTenia(Transform origen, Transform destino)
    {
        foreach (var r in destino.GetComponentsInChildren<Renderer>(true))
        {
            var ruta = Ruta(destino, r.transform);
            if (Buscar(origen, ruta) == null && r.gameObject.activeSelf && r.transform.parent == destino)
                r.gameObject.SetActive(false);
        }
    }

    static void ConfigurarPiloto(PilotoDelCuerpo piloto, List<Behaviour> jugador, List<Behaviour> ia,
        RuntimeAnimatorController controladorIa, bool raizJugador, bool raizIa, bool empiezaConJugador)
    {
        var so = new SerializedObject(piloto);
        Escribir(so.FindProperty("conElJugador"), jugador);
        Escribir(so.FindProperty("conLaIA"), ia);
        so.FindProperty("animadorConLaIA").objectReferenceValue = controladorIa;
        so.FindProperty("movimientoRaizConElJugador").boolValue = raizJugador;
        so.FindProperty("movimientoRaizConLaIA").boolValue = raizIa;
        so.FindProperty("empiezaConElJugador").boolValue = empiezaConJugador;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Escribir(SerializedProperty lista, List<Behaviour> valores)
    {
        lista.arraySize = valores.Count;
        for (int i = 0; i < valores.Count; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = valores[i];
    }

    static List<Behaviour> Lista(SerializedProperty lista)
    {
        var r = new List<Behaviour>();
        for (int i = 0; i < lista.arraySize; i++)
            if (lista.GetArrayElementAtIndex(i).objectReferenceValue is Behaviour b) r.Add(b);
        return r;
    }

    static void Encender(List<Behaviour> lista, bool encender)
    {
        foreach (var b in lista) if (b != null) b.enabled = encender;
    }

    static bool LeerBool(Component c, string campo)
    {
        if (c == null) return false;
        var p = new SerializedObject(c).FindProperty(campo);
        return p != null && p.boolValue;
    }

    // ─── GrupoDelJugador ──────────────────────────────────────────────────

    static void PasarElPresetAlGrupo(StringBuilder informe)
    {
        var raizWill = PrefabUtility.LoadPrefabContents(ViejoWill);
        var grupo = PrefabUtility.LoadPrefabContents(Grupo);
        try
        {
            var origen = raizWill.GetComponentInChildren<PlayerPresetService>(true);
            var destino = grupo.GetComponent<PlayerPresetService>();
            if (destino == null) destino = grupo.AddComponent<PlayerPresetService>();
            if (origen != null) CopiarValores(origen, destino);
            PrefabUtility.SaveAsPrefabAsset(grupo, Grupo);
            informe.AppendLine($"PlayerPresetService pasa a {Grupo}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raizWill);
            PrefabUtility.UnloadPrefabContents(grupo);
        }
    }

    // ─── Migración ────────────────────────────────────────────────────────

    sealed class Cambio
    {
        public GameObject viejo;          // asset viejo
        public GameObject nuevo;          // variante
        public bool cuerpoEsHijo;         // _WILL: el cuerpo era hijo del contenedor
    }

    static List<Cambio> Mapa() => Tres.Select(p => new Cambio
    {
        viejo = AssetDatabase.LoadAssetAtPath<GameObject>(p.viejo),
        nuevo = AssetDatabase.LoadAssetAtPath<GameObject>(p.temporal),
        cuerpoEsHijo = p.slot == Slot.Will,
    }).ToList();

    // El cuerpo de una instancia vieja (en _WILL, el hijo con el controlador).
    static Transform CuerpoDe(GameObject raiz, Cambio c)
    {
        if (!c.cuerpoEsHijo) return raiz.transform;
        var ctl = raiz.GetComponentInChildren<vThirdPersonController>(true);
        return ctl != null ? ctl.transform : raiz.transform;
    }

    static void MigrarEscena(string ruta, List<Cambio> mapa, StringBuilder informe)
    {
        var escena = SceneManager.GetSceneByPath(ruta);
        bool estabaAbierta = escena.IsValid() && escena.isLoaded;
        if (estabaAbierta && escena.isDirty)
        {
            informe.AppendLine($"{ruta}: tiene cambios sin guardar de otro; no se toca.");
            return;
        }
        if (!estabaAbierta) escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);

        int cambiadas = 0, referencias = 0;
        var raices = escena.GetRootGameObjects();
        var instancias = new List<(GameObject go, Cambio c)>();
        foreach (var r in raices)
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                var fuente = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                var c = mapa.FirstOrDefault(m => m.viejo == fuente);
                if (c != null) instancias.Add((t.gameObject, c));
            }

        if (ruta == MenuPrincipal)
        {
            foreach (var (viejo, _) in instancias) Desempaquetar(viejo);
            cambiadas = instancias.Count;
            if (cambiadas > 0) { EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena); }
            informe.AppendLine($"{ruta}: {cambiadas} figuras desempaquetadas (decorado), guardada.");
            if (!estabaAbierta) EditorSceneManager.CloseScene(escena, true);
            return;
        }

        var pares = new List<(Transform raiz, Transform cuerpo, Transform nuevo)>();
        foreach (var (viejo, c) in instancias)
        {
            var padre = viejo.transform.parent;
            if (padre != null && PrefabUtility.IsPartOfPrefabInstance(padre))
            {
                informe.AppendLine($"{ruta}: {viejo.name} va dentro de otro prefab; se deja.");
                continue;
            }
            var cuerpoViejo = CuerpoDe(viejo, c);
            var nuevo = (GameObject)PrefabUtility.InstantiatePrefab(c.nuevo, escena);
            nuevo.transform.SetParent(padre, false);
            nuevo.transform.SetSiblingIndex(viejo.transform.GetSiblingIndex());
            nuevo.transform.SetPositionAndRotation(cuerpoViejo.position, cuerpoViejo.rotation);
            nuevo.name = viejo.name;
            nuevo.SetActive(viejo.activeSelf);
            TrasladarCambios(viejo, c, nuevo, informe, ruta);
            pares.Add((viejo.transform, cuerpoViejo, nuevo.transform));
        }

        // Las referencias de la escena a las instancias viejas pasan a las nuevas (una pasada).
        if (pares.Count > 0)
        {
            foreach (var r in raices)
            {
                if (r == null) continue;
                foreach (var comp in r.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null || comp is Transform) continue;
                    bool propio = false;
                    foreach (var par in pares)
                        if (comp.transform.IsChildOf(par.raiz) || comp.transform.IsChildOf(par.nuevo)) { propio = true; break; }
                    if (propio) continue;
                    foreach (var par in pares)
                        referencias += Reapuntar(comp, par.raiz, par.cuerpo, par.nuevo);
                }
            }
            foreach (var par in pares) Object.DestroyImmediate(par.raiz.gameObject);
            cambiadas = pares.Count;

            // Sin GrupoDelJugador en la escena no habría quien ponga la partida en los cuerpos.
            if (!raices.Any(x => x != null && x.GetComponentInChildren<GrupoDelJugador>(true) != null))
            {
                var grupo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Grupo), escena);
                grupo.transform.SetSiblingIndex(pares[0].nuevo.GetSiblingIndex() + 1);
                informe.AppendLine($"{ruta}: no tenía GrupoDelJugador; puesto.");
            }
        }

        if (cambiadas > 0)
        {
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
        }
        informe.AppendLine($"{ruta}: {cambiadas} cuerpos sustituidos, {referencias} referencias recolocadas" + (cambiadas > 0 ? ", guardada." : "."));
        if (!estabaAbierta) EditorSceneManager.CloseScene(escena, true);
    }

    // Los cambios que la escena había hecho a la instancia vieja (valores, componentes añadidos o
    // quitados, hijos añadidos) pasan a la nueva cuando lo cambiado existe en ella. La pose de los
    // huesos no: es la del cuerpo.
    static void TrasladarCambios(GameObject viejo, Cambio c, GameObject nuevo, StringBuilder informe, string ruta)
    {
        var raizAsset = c.viejo.transform;
        var cuerpoAsset = CuerpoDe(c.viejo, c);
        int valores = 0, perdidos = 0;
        var mods = PrefabUtility.GetPropertyModifications(viejo);
        if (mods != null)
            foreach (var m in mods)
            {
                if (m.target == null || m.target is Transform) continue;
                if (m.propertyPath == "m_Name") continue;
                if (m.target is GameObject g && g.transform == cuerpoAsset && m.propertyPath == "m_IsActive") continue;
                var destino = Equivalente(m.target, raizAsset, cuerpoAsset, nuevo.transform);
                if (destino == null) { perdidos++; continue; }
                var so = new SerializedObject(destino);
                var prop = so.FindProperty(m.propertyPath);
                if (prop == null || !PonerValor(prop, m)) { perdidos++; continue; }
                so.ApplyModifiedPropertiesWithoutUndo();
                valores++;
            }

        foreach (var a in PrefabUtility.GetAddedComponents(viejo))
        {
            var comp = a.instanceComponent;
            if (comp == null) continue;
            var dondeVa = Equivalente(comp.gameObject, viejo.transform, CuerpoDe(viejo, c), nuevo.transform) as GameObject;
            if (dondeVa == null) { perdidos++; continue; }
            var copia = CopiarComoNuevo(comp, dondeVa);
            Reapuntar(copia, viejo.transform, CuerpoDe(viejo, c), nuevo.transform);
            valores++;
        }

        foreach (var q in PrefabUtility.GetRemovedComponents(viejo))
        {
            var quitar = Equivalente(q.assetComponent, raizAsset, cuerpoAsset, nuevo.transform) as Component;
            if (quitar != null && !(quitar is Transform)) { Object.DestroyImmediate(quitar); valores++; }
        }

        foreach (var hijo in PrefabUtility.GetAddedGameObjects(viejo))
        {
            var go = hijo.instanceGameObject;
            if (go == null || go.transform.parent == null) continue;
            var padre = Equivalente(go.transform.parent.gameObject, viejo.transform, CuerpoDe(viejo, c), nuevo.transform) as GameObject;
            if (padre == null) { perdidos++; continue; }
            go.transform.SetParent(padre.transform, true);
            valores++;
        }

        if (valores > 0 || perdidos > 0)
            informe.AppendLine($"{ruta}: {nuevo.name} conserva {valores} cambios de la escena" + (perdidos > 0 ? $" ({perdidos} sin equivalente en el cuerpo nuevo)." : "."));
    }

    static bool PonerValor(SerializedProperty prop, PropertyModification m)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        switch (prop.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.ArraySize:
            case SerializedPropertyType.LayerMask:
            case SerializedPropertyType.Character:
                if (!long.TryParse(m.value, System.Globalization.NumberStyles.Integer, inv, out var l)) return false;
                prop.longValue = l; return true;
            case SerializedPropertyType.Enum:
                if (!int.TryParse(m.value, System.Globalization.NumberStyles.Integer, inv, out var e)) return false;
                prop.intValue = e; return true;
            case SerializedPropertyType.Boolean:
                prop.boolValue = m.value == "1" || m.value == "true"; return true;
            case SerializedPropertyType.Float:
                if (!double.TryParse(m.value, System.Globalization.NumberStyles.Float, inv, out var f)) return false;
                prop.doubleValue = f; return true;
            case SerializedPropertyType.String:
                prop.stringValue = m.value; return true;
            case SerializedPropertyType.ObjectReference:
                prop.objectReferenceValue = m.objectReference; return true;
            default:
                return false;
        }
    }

    // Una figura de decorado deja de depender del prefab: se queda con todo lo que tiene, menos los
    // componentes retirados.
    static void Desempaquetar(GameObject figura)
    {
        PrefabUtility.UnpackPrefabInstance(figura, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var mb in figura.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null && (mb.GetType().Name == "ActiveCharacterSwapper" || mb.GetType().Name == "PlayerClimbingController"))
                Object.DestroyImmediate(mb);
    }

    // Assets (rosters, configuraciones del laboratorio…) que apuntan a los prefabs viejos.
    static void MigrarAssets(List<Cambio> mapa, StringBuilder informe)
    {
        var viejos = new HashSet<string>(mapa.Select(m => AssetDatabase.GetAssetPath(m.viejo)));
        viejos.Add(ViejoWillNpc);
        foreach (var ruta in AssetDatabase.GetAllAssetPaths())
        {
            if (!ruta.StartsWith("Assets/", StringComparison.Ordinal) || viejos.Contains(ruta)) continue;
            if (!ruta.EndsWith(".asset", StringComparison.Ordinal) && !ruta.EndsWith(".prefab", StringComparison.Ordinal)) continue;
            if (ruta.StartsWith(CarpetaNueva, StringComparison.Ordinal)) continue;
            bool ajeno = ruta.StartsWith("Assets/NarrativeGraph/", StringComparison.Ordinal);
            var deps = AssetDatabase.GetDependencies(ruta, false);
            if (!deps.Any(viejos.Contains)) continue;

            if (ajeno || ruta.EndsWith(".prefab", StringComparison.Ordinal))
            {
                informe.AppendLine($"{ruta}: usa un prefab viejo de personaje; revísalo a mano.");
                continue;
            }
            int n = 0;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ruta))
            {
                if (o == null) continue;
                var so = new SerializedObject(o);
                var it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    var destino = EquivalenteEnAsset(it.objectReferenceValue, mapa);
                    if (destino == null) continue;
                    it.objectReferenceValue = destino;
                    n++;
                }
                if (so.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(o);
            }
            if (n > 0) informe.AppendLine($"{ruta}: {n} referencias a los prefabs nuevos.");
        }
    }

    static Object EquivalenteEnAsset(Object o, List<Cambio> mapa)
    {
        var go = o as GameObject ?? (o as Component)?.gameObject;
        if (go == null) return null;
        var raiz = go.transform.root.gameObject;
        var c = mapa.FirstOrDefault(m => m.viejo == raiz);
        if (c == null) return null;
        return Equivalente(o, raiz.transform, CuerpoDe(raiz, c), c.nuevo.transform);
    }

    static void Retirar(StringBuilder informe)
    {
        foreach (var p in Tres)
        {
            Mover(p.viejo, CarpetaDeRetirados + "/" + System.IO.Path.GetFileName(p.viejo), informe);
            Mover(p.temporal, p.viejo, informe);
        }
        Mover(ViejoWillNpc, CarpetaDeRetirados + "/" + System.IO.Path.GetFileName(ViejoWillNpc), informe);
    }

    static void Mover(string de, string a, StringBuilder informe)
    {
        var error = AssetDatabase.MoveAsset(de, a);
        informe.AppendLine(string.IsNullOrEmpty(error) ? $"{de} → {a}." : $"No se pudo mover {de}: {error}");
    }

    // ─── Copiar y reapuntar ───────────────────────────────────────────────

    static Component CopiarComoNuevo(Component origen, GameObject destino)
    {
        UnityEditorInternal.ComponentUtility.CopyComponent(origen);
        UnityEditorInternal.ComponentUtility.PasteComponentAsNew(destino);
        var todos = destino.GetComponents(origen.GetType());
        return todos[todos.Length - 1];
    }

    static void CopiarValores(Component origen, Component destino)
    {
        UnityEditorInternal.ComponentUtility.CopyComponent(origen);
        UnityEditorInternal.ComponentUtility.PasteComponentValues(destino);
    }

    static void CopiarValoresSiHay<T>(Transform origen, Transform destino) where T : Component
    {
        var a = origen.GetComponent<T>();
        var b = destino.GetComponent<T>();
        if (a == null || b == null) return;
        CopiarValores(a, b);
        Reapuntar(b, origen, destino);
    }

    static void CopiarValoresTodos<T>(Transform origen, Transform destino) where T : Component
    {
        var a = origen.GetComponents<T>();
        var b = destino.GetComponents<T>();
        if (a.Length == 0) return;
        for (int i = 0; i < b.Length; i++)
        {
            CopiarValores(a[Mathf.Min(i, a.Length - 1)], b[i]);
            Reapuntar(b[i], origen, destino);
        }
    }

    // Las referencias del componente a objetos de 'origen' pasan a su equivalente en 'destino'.
    static int Reapuntar(Component comp, Transform origen, Transform destino) => Reapuntar(comp, origen, origen, destino);

    // 'raizVieja' es la raíz de lo que se sustituye y 'cuerpoViejo', el cuerpo dentro de ella (el
    // mismo, salvo en _WILL, donde el cuerpo era hijo de un contenedor).
    static int Reapuntar(Component comp, Transform raizVieja, Transform cuerpoViejo, Transform destino)
    {
        var so = new SerializedObject(comp);
        var it = so.GetIterator();
        int n = 0;
        while (it.Next(true))
        {
            if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
            var nuevo = Equivalente(it.objectReferenceValue, raizVieja, cuerpoViejo, destino);
            if (nuevo == null || nuevo == it.objectReferenceValue) continue;
            it.objectReferenceValue = nuevo;
            n++;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return n;
    }

    static Object Equivalente(Object o, Transform raizVieja, Transform cuerpoViejo, Transform destino)
    {
        Transform t = o is GameObject g ? g.transform : (o as Component)?.transform;
        if (t == null) return null;
        Transform igual;
        if (t == raizVieja || t == cuerpoViejo) igual = destino;
        else if (t.IsChildOf(cuerpoViejo)) igual = Buscar(destino, Ruta(cuerpoViejo, t));
        else if (t.IsChildOf(raizVieja)) igual = Buscar(destino, Ruta(raizVieja, t));
        else return null;
        if (igual == null) return null;
        if (o is GameObject) return igual.gameObject;
        var tipo = o.GetType();
        var lista = ((Component)o).GetComponents(tipo);
        int indice = Array.IndexOf(lista, o);
        var destinos = igual.GetComponents(tipo);
        if (destinos.Length == 0) return null;
        return destinos[Mathf.Clamp(indice, 0, destinos.Length - 1)];
    }

    static string Ruta(Transform raiz, Transform t)
    {
        if (t == raiz) return "";
        var partes = new List<string>();
        for (var x = t; x != null && x != raiz; x = x.parent) partes.Add(x.name);
        partes.Reverse();
        return string.Join("/", partes);
    }

    static Transform Buscar(Transform raiz, string ruta) => string.IsNullOrEmpty(ruta) ? raiz : raiz.Find(ruta);
}
