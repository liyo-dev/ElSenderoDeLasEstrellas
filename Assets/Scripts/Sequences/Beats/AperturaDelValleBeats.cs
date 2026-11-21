using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

// Apertura del prólogo:
//   1. El Sendero: el Mago Oscuro pide su deseo ante el altar, el altar estalla en luz, nota un poder lejano y
//      una estrella dorada crece hasta llenar la pantalla.               → DeseoEnElSenderoBeat
//   2. El mapa del mundo en papel, con el punto del valle que late.     → MapaDeAperturaBeat
//   3. Vuelo de cámara: del mar a la cascada, sube por el agua, sale el logo y llega al valle, donde
//      empieza la grúa de siempre.                                      → VueloDeAperturaBeat
// Los tres llevan su propia cámara de Cinemachine con la prioridad más alta (mandan sobre las del guion) y se
// limpian solos al terminar o al saltar la escena. Pantallas de unión: un velo de color de papel que pasa de un
// beat al siguiente (Velo).

/// Velo a pantalla completa entre los beats de la apertura (se reutiliza de uno a otro).
public static class VeloDeApertura
{
    static Canvas _lienzo;
    static Image _velo;
    public static readonly Color ColorPapel = new Color(1f, 0.93f, 0.76f, 1f);

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _lienzo = null; _velo = null; }
#endif

    public static Image Velo
    {
        get
        {
            if (_velo != null) return _velo;
            var go = new GameObject("_VeloDeApertura");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _lienzo = go.AddComponent<Canvas>();
            _lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
            _lienzo.sortingOrder = 4900;
            var img = new GameObject("Velo").AddComponent<Image>();
            img.transform.SetParent(go.transform, false);
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0f);
            _velo = img;
            return _velo;
        }
    }

    public static void Poner(Color c, float alfa) { var v = Velo; c.a = alfa; v.color = c; }

    public static void Quitar()
    {
        if (_lienzo != null) UnityEngine.Object.Destroy(_lienzo.gameObject);
        _lienzo = null; _velo = null;
    }

    public static CinemachineCamera CamaraPropia(string nombre, float fov)
    {
        var go = new GameObject(nombre);
        var cam = go.AddComponent<CinemachineCamera>();
        cam.Priority = 1000000;
        var lente = cam.Lens;
        lente.FieldOfView = fov;
        lente.NearClipPlane = 0.1f;
        lente.FarClipPlane = 3000f;
        cam.Lens = lente;
        return cam;
    }

    public static float Suave(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
}

/// 1. El deseo en el Sendero.
[Serializable]
public class DeseoEnElSenderoBeat : SequenceBeat
{
    [Tooltip("Plaza del altar del Sendero (prefab Assets/Art/Cinematic/Apertura/SenderoDeLaApertura.prefab).")]
    public GameObject escenario;
    [Tooltip("Modelo del Mago Oscuro (solo se usa su malla y su Animator).")]
    public GameObject mago;
    [Tooltip("Dónde se monta el escenario (lejos del mundo, por encima de todo).")]
    public Vector3 origen = new Vector3(0f, 1500f, 0f);
    public string textoDeseo = "PROLOGO_DESEO";
    public string textoPoder = "PROLOGO_DESEO_PODER_OSCURO";
    public string textoPorFin = "PROLOGO_DESEO_PORFIN_OSCURO";
    public string textoRisa = "PROLOGO_DESEO_RISA_OSCURO";
    public string nombreDelMago = "Mago Oscuro";
    public float segundos = 31.5f;
    [Tooltip("Onda de pinchos que sale del pecho al entrar el poder (vacío = ninguna).")]
    public GameObject ondaDelEstallido;
    [Tooltip("Círculo mágico a los pies en el «¡Por fin!» (vacío = ninguno).")]
    public GameObject circuloMagico;
    [Tooltip("Chispa en el pecho en el «¡Por fin!» (vacío = ninguna).")]
    public GameObject destello;

    // Posiciones dentro del escenario (las del Sendero: el altar está en (0, 6, 656)).
    static readonly Vector3 Altar = new Vector3(0f, 6f, 656f);
    static readonly Vector3 PiesDelMago = new Vector3(0f, 6.05f, 649.5f);
    static readonly Vector3 HaciaLaEstrella = new Vector3(-0.55f, -0.18f, -0.82f);
    const float DistanciaDeLaEstrella = 90f;
    static readonly Color FondoNegro = new Color(0.008f, 0.004f, 0.016f);

    public override string Describe() => $"Apertura: el deseo en el Sendero ({segundos:F1}s)";

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (ctx?.Player == null) yield break;
        var rutina = ctx.Player.StartCoroutine(Co(ctx));
        ctx.Player.TrackBackgroundRoutine(rutina);
    }

    IEnumerator Co(SequenceContext ctx)
    {
        // Clave de villano de anime de los 90: solo se ve lo que brilla. Fondo negro (sin cielo ni mundo), el
        // Sendero apagado salvo el halo del altar, el mago como silueta negra recortada contra ese halo y sus
        // ojos violeta. En el estallido de luz el fondo se enciende un instante y la silueta salta a la vista.
        var creados = new List<GameObject>();
        var camara = Camera.main;
        var fondoAntes = camara != null ? camara.clearFlags : CameraClearFlags.Skybox;
        var colorAntes = camara != null ? camara.backgroundColor : Color.black;
        var ciclo = DayNightCycle.Instance;
        const string claveDeNiebla = "Deseo en el Sendero";
        bool limpio = false;
        void Limpiar()
        {
            if (limpio) return;
            limpio = true;
            foreach (var g in creados)
            {
                if (g == null) continue;
                BrilloDeApertura.SoltarMaterial(g);
                UnityEngine.Object.Destroy(g);
            }
            if (camara != null) { camara.clearFlags = fondoAntes; camara.backgroundColor = colorAntes; }
            if (ciclo != null) ciclo.SoltarNiebla(claveDeNiebla);
        }
        ctx.Player.RegisterCleanup(Limpiar);
        if (camara != null) { camara.clearFlags = CameraClearFlags.SolidColor; camara.backgroundColor = FondoNegro; }
        // Sin niebla: en el negro, la del mundo agrisaría las estrellas lejanas.
        if (ciclo != null) ciclo.PrestarNiebla(claveDeNiebla, 0f, FondoNegro, 1f);

        GameObject set = escenario != null ? UnityEngine.Object.Instantiate(escenario, origen, Quaternion.identity) : new GameObject("_SenderoDeLaApertura");
        creados.Add(set);
        // Del escenario solo quedan los brillos del altar; el resto (suelo, plaza, camino, cielos) no se ve.
        Vector3 altarMundo = origen + Altar;
        foreach (var r in set.GetComponentsInChildren<Renderer>(true))
        {
            var sh = r.sharedMaterial != null ? r.sharedMaterial.shader : null;
            Vector3 d = r.bounds.center - altarMundo; d.y = 0f;
            r.enabled = sh != null && sh.name.Contains("LuzSimple") && d.magnitude < 6f;
        }
        foreach (var l in set.GetComponentsInChildren<Light>(true)) l.enabled = false;

        // El mago: solo malla y Animator, de espaldas a cámara, mirando al altar.
        Transform cuerpo = null;
        Transform cabeza = null;
        Animator animMago = null;
        NPCEmotionController emocion = null;
        if (mago != null)
        {
            var m = UnityEngine.Object.Instantiate(mago, origen + PiesDelMago - Vector3.forward * 5f, Quaternion.LookRotation(Vector3.forward));
            m.name = "_MagoDelDeseo";
            foreach (var mb in m.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb is not NPCEmotionController) mb.enabled = false;
            foreach (var c in m.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var a in m.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) a.enabled = false;
            foreach (var rb in m.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            creados.Add(m);
            cuerpo = m.transform;
            // El prefab trae varios cuerpos de demo con Animator; el del NPC es el que lleva NPC_NoWeapon.
            foreach (var a in m.GetComponentsInChildren<Animator>(true))
            {
                if (a.runtimeAnimatorController == null) continue;
                if (animMago == null) animMago = a;
                if (a.runtimeAnimatorController.name.Contains("NPC_NoWeapon")) { animMago = a; break; }
            }
            if (animMago == null) animMago = m.GetComponentInChildren<Animator>(true);
            if (animMago != null) animMago.applyRootMotion = false;
            emocion = m.GetComponentInChildren<NPCEmotionController>(true);
            emocion?.SetEmotion(NPCEmotion.Neutral);
            if (animMago != null && animMago.isHuman)
                cabeza = animMago.GetBoneTransform(HumanBodyBones.Head);
        }
        var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var violeta = new Material(unlit) { color = new Color(0.78f, 0.55f, 1f) };
        var ojoI = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var ojoD = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        foreach (var o in new[] { ojoI, ojoD })
        {
            UnityEngine.Object.Destroy(o.GetComponent<Collider>());
            o.GetComponent<Renderer>().sharedMaterial = violeta;
            o.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            o.transform.localScale = new Vector3(0.05f, 0.022f, 0.02f);
            creados.Add(o);
        }
        Vector3 ojos = origen + PiesDelMago + Vector3.up * 1.6f;
        Vector3 altar = altarMundo + Vector3.up * 1.2f;

        // Brillos: el halo del altar (núcleo cálido y resplandor violeta), su reflejo en el suelo (para que el
        // mago tenga dónde pisar), el brillo de los ojos y unas estrellas tenues muy lejos.
        var nucleo = BrilloDeApertura.Crear("_HaloDelAltar", altar, 2.4f, new Color(2.4f, 1.9f, 1.3f), creados);
        var resplandor = BrilloDeApertura.Crear("_ResplandorDelAltar", altar, 7f, new Color(0.55f, 0.32f, 1.1f), creados);
        var suelo = BrilloDeApertura.Crear("_ReflejoDelAltar", altarMundo + Vector3.up * 0.05f, 10f, new Color(0.32f, 0.2f, 0.5f), creados);
        suelo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var brilloI = BrilloDeApertura.Crear("_BrilloOjoI", ojos, 0.22f, new Color(1.1f, 0.6f, 1.8f), creados);
        var brilloD = BrilloDeApertura.Crear("_BrilloOjoD", ojos, 0.22f, new Color(1.1f, 0.6f, 1.8f), creados);
        // Contraluz pegado a la espalda del mago: recorta la silueta contra el negro. Sin él, de
        // espaldas solo se ve el halo del altar y el resto del cuadro queda vacío.
        var azar = new System.Random(16);
        var contraluz = BrilloDeApertura.Crear("_ContraluzDelMago",
            origen + PiesDelMago + Vector3.forward * 0.7f + Vector3.up * 1.25f, 3.8f,
            new Color(0.30f, 0.14f, 0.52f), creados);
        // Motas cerca del mago: llenan el negro de los primeros planos (los ojos) con algo que se mueve.
        var motes = new List<GameObject>();
        for (int i = 0; i < 26; i++)
        {
            var dir = new Vector3((float)azar.NextDouble() * 2f - 1f, (float)azar.NextDouble(), (float)azar.NextDouble() * 2f - 1f);
            var pm = ojos + dir.normalized * (1.1f + 3.4f * (float)azar.NextDouble());
            motes.Add(BrilloDeApertura.Crear("_Mota", pm, 0.06f + 0.15f * (float)azar.NextDouble(),
                new Color(0.32f, 0.19f, 0.58f), creados));
        }
        // Luz de relleno tenue pegada a la cámara: en los primeros planos le saca la cara del negro.
        var luzMago = new GameObject("_LuzDelMago").AddComponent<Light>();
        luzMago.type = LightType.Point;
        luzMago.color = new Color(0.55f, 0.45f, 0.92f);
        luzMago.intensity = 3.5f;
        luzMago.range = 7f;
        luzMago.shadows = LightShadows.None;
        creados.Add(luzMago.gameObject);
        // El haz del altar al pecho: la luz del poder se mete en el mago (13,5-15,6).
        var pecho = origen + PiesDelMago + Vector3.up * 1.2f;
        var haz = new List<GameObject>();
        for (int i = 0; i < 12; i++)
            haz.Add(BrilloDeApertura.Crear("_HazDelAltar", altar, 0.5f, new Color(1.6f, 1.3f, 2.2f), creados));
        var destelloPecho = BrilloDeApertura.Crear("_DestelloEnElPecho", pecho, 0.01f, new Color(2.5f, 2f, 3f), creados);
        // La onda del altar (INC-1287): un anillo que sale a ras de suelo y barre el Sendero, y tres
        // anillos que vuelven y se cierran sobre el mago hasta metérsele en el pecho. Cada anillo es una
        // corona de brillos, como el haz.
        const int PorAnillo = 40;
        var onda = new List<GameObject>();
        for (int i = 0; i < PorAnillo; i++)
            onda.Add(BrilloDeApertura.Crear("_OndaDelAltar", altar, 0.9f, new Color(1.5f, 1.15f, 2.3f), creados));
        var anillos = new List<List<GameObject>>();
        for (int a = 0; a < 3; a++)
        {
            var anillo = new List<GameObject>();
            for (int i = 0; i < PorAnillo; i++)
                anillo.Add(BrilloDeApertura.Crear("_AnilloQueEntra", pecho, 0.45f, new Color(1.6f, 1.25f, 2.4f), creados));
            anillos.Add(anillo);
        }
        for (int i = 0; i < 110; i++)
        {
            var dir = new Vector3((float)azar.NextDouble() * 2f - 1f, 0.05f + (float)azar.NextDouble(), (float)azar.NextDouble() * 2f - 1f).normalized;
            float tono = 0.32f + 0.42f * (float)azar.NextDouble();
            var e = BrilloDeApertura.Crear("_EstrellaTenue", ojos + dir * (55f + 28f * (float)azar.NextDouble()),
                0.5f + 0.7f * (float)azar.NextDouble(), new Color(0.7f * tono, 0.75f * tono, 1f * tono), creados);
            e.transform.rotation = Quaternion.LookRotation(e.transform.position - ojos);
        }

        // La estrella lejana: un punto dorado que late (dentro del alcance de la cámara, que no ve más allá).
        var estrella = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        estrella.name = "_EstrellaLejana";
        UnityEngine.Object.Destroy(estrella.GetComponent<Collider>());
        var rEst = estrella.GetComponent<Renderer>();
        var matEst = new Material(unlit);
        matEst.color = new Color(1f, 0.82f, 0.4f);
        rEst.sharedMaterial = matEst;
        rEst.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Vector3 posEstrella = ojos + HaciaLaEstrella.normalized * DistanciaDeLaEstrella;
        estrella.transform.position = posEstrella;
        estrella.transform.localScale = Vector3.one * 0.01f;
        creados.Add(estrella);
        var brilloEstrella = BrilloDeApertura.Crear("_BrilloDeLaEstrella", posEstrella, 0.01f, new Color(2.2f, 1.6f, 0.6f), creados);

        var cam = VeloDeApertura.CamaraPropia("_CamaraDelDeseo", 38f);
        var lente = cam.Lens;
        lente.FarClipPlane = DistanciaDeLaEstrella + 40f;
        cam.Lens = lente;
        creados.Add(cam.gameObject);
        VeloDeApertura.Poner(Color.black, 1f);

        // Línea de tiempo (s), con la música «El deseo del Mago Oscuro» encima:
        //   0-6,5     de espaldas contra el halo, la silueta; sale del negro (0-2)
        //   6,5-10    los ojos se abren en la oscuridad (muy cerca)
        //   10-13,5   de frente: «Que ningún poder pueda destruirme» (10,6)
        //   13,5-16,2 el altar manda su luz al pecho del mago; al entrar, le cambia la voz
        //   16,2-17,5 desde abajo, abre los brazos: «¡Por fin!», ya con la voz oscura
        //   17,5-19,1 la carcajada malvada, con los ojos entornados
        //   19,1      «clin»; mira atrás, gira la cabeza hacia la estrella
        //   19,1-21,4 la cámara va a su cara y sus ojos: «…¿Y ese poder?» (21,5)
        //   21,4-27,5 la cámara va a la luz del poder (la estrella), que crece
        //   27,5-fin  por encima del hombro, la estrella llena la pantalla; papel en el último 1,2 s
        var deCara = new[] { nucleo, resplandor, contraluz, brilloI, brilloD, brilloEstrella };
        float t = 0f, fin = Mathf.Max(28f, segundos);
        bool dicho1 = false, dicho2 = false, dicho3 = false, gestoPoder = false, miraAtras = false;
        bool hechizoDelEstallido = false, clin = false, risa = false, sonrisa = false, andando = false;
        var lenteBase = cam.Lens;
        while (t < fin)
        {
            float dt = Time.deltaTime;
            t += dt;
            Vector3 posCam = cam.transform.position;

            // Apertura de los ojos: cerrados al principio, se abren (6,5-7,6), se entornan al reír, se abren de golpe al notar algo.
            float apertura = VeloDeApertura.Suave((t - 6.5f) / 1.1f);
            if (t > 15.2f && t < 17.5f) apertura = Mathf.Max(apertura, Mathf.Lerp(1f, 1.5f, VeloDeApertura.Suave((t - 15.2f) / 0.5f)));
            if (t > 17.5f && t < 19f) apertura *= Mathf.Lerp(1f, 0.5f, VeloDeApertura.Suave((t - 17.5f) / 0.4f));
            if (t >= 19f) apertura = Mathf.Max(apertura, Mathf.Lerp(0.5f, 1.45f, VeloDeApertura.Suave((t - 19f) / 0.45f)));
            // «No es ninguno de los que existen»: entorna los ojos (amenaza).
            if (t >= 22.8f) apertura *= Mathf.Lerp(1f, 0.55f, VeloDeApertura.Suave((t - 22.8f) / 0.5f));
            if (cuerpo != null)
            {
                Vector3 c = cabeza != null ? cabeza.position : cuerpo.position + Vector3.up * 1.55f;
                Vector3 fw = cuerpo.forward, der = cuerpo.right;
                Vector3 pI = c + fw * 0.13f + Vector3.up * 0.03f - der * 0.045f;
                Vector3 pD = c + fw * 0.13f + Vector3.up * 0.03f + der * 0.045f;
                ojoI.transform.SetPositionAndRotation(pI, cuerpo.rotation);
                ojoD.transform.SetPositionAndRotation(pD, cuerpo.rotation);
                var escalaOjo = new Vector3(0.05f, Mathf.Max(0.001f, 0.022f * apertura), 0.02f);
                ojoI.transform.localScale = escalaOjo;
                ojoD.transform.localScale = escalaOjo;
                float brillo = (0.75f + 0.25f * Mathf.Sin(t * 3f)) * Mathf.Clamp01(apertura);
                violeta.color = new Color(0.78f * brillo, 0.55f * brillo, 1f * brillo);
                // El brillo de los ojos solo cuando se le ve la cara (de espaldas no se ven).
                float deFrente = Mathf.Clamp01(Vector3.Dot(fw, (posCam - c).normalized) * 3f);
                // Al mirar atrás por encima del hombro se le ve de perfil: el ojo de este lado sigue encendido.
                if (t >= 19.6f) deFrente = Mathf.Max(deFrente, 0.75f);
                brilloI.transform.position = pI + fw * 0.02f;
                brilloD.transform.position = pD + fw * 0.02f;
                BrilloDeApertura.Intensidad(brilloI, brillo * deFrente);
                BrilloDeApertura.Intensidad(brilloD, brillo * deFrente);
            }

            // El altar respira; en el estallido (14,5-16,3 s) se dispara y enciende el fondo un instante.
            float estallido = t > 15.0f && t < 16.6f ? Mathf.Sin(Mathf.Clamp01((t - 15.0f) / 1.6f) * Mathf.PI) : 0f;
            float respira = 0.8f + 0.2f * Mathf.Sin(t * 1.7f);
            BrilloDeApertura.Intensidad(nucleo, respira * (1f + 5f * estallido));
            BrilloDeApertura.Intensidad(resplandor, respira * (1f + 3f * estallido));
            resplandor.transform.localScale = Vector3.one * 7f * (1f + 1.5f * estallido);
            BrilloDeApertura.Intensidad(suelo, respira * (1f + 2f * estallido));
            BrilloDeApertura.Intensidad(contraluz, (0.55f + 0.15f * Mathf.Sin(t * 1.3f)) * (1f + 2.5f * estallido));
            // El contraluz se pone siempre al otro lado del mago que la cámara: recorta la silueta en
            // todos los planos (si no, en los frontales se cuela delante y solo se ve azul).
            if (cuerpo != null)
            {
                Vector3 atras = cuerpo.position - posCam; atras.y = 0f;
                contraluz.transform.position = cuerpo.position + (atras.sqrMagnitude > 0.01f ? atras.normalized : Vector3.back) * 0.85f + Vector3.up * 1.3f;
            }
            // Luz tenue pegada a la cara, del lado de la cámara: le saca la cara del negro.
            if (cabeza != null)
                luzMago.transform.position = cabeza.position + (posCam - cabeza.position).normalized * 0.35f;
            // El haz del altar al pecho (13,4-15,6).
            float avance = Mathf.Clamp01((t - 13.4f) / 1.9f);
            for (int i = 0; i < haz.Count; i++)
            {
                var g = haz[i];
                if (g == null) continue;
                float u = Mathf.Clamp01(avance * 1.45f - i * 0.035f);
                Vector3 ph = Vector3.Lerp(altar, pecho, u);
                ph.y += Mathf.Sin(u * Mathf.PI) * 0.45f;
                g.transform.position = ph;
                float visible = 0f;   // sustituido por la onda y los anillos (INC-1287)
                BrilloDeApertura.Intensidad(g, visible * (0.45f + 0.55f * Mathf.Sin(u * Mathf.PI)));
                g.transform.rotation = Quaternion.LookRotation(ph - posCam);
            }
            // El altar coge aire (12,9-13,4): el halo se encoge y baja; medio segundo de silencio de luz.
            float inhala = (t > 12.9f && t < 13.5f) ? Mathf.Sin(Mathf.Clamp01((t - 12.9f) / 0.6f) * Mathf.PI) : 0f;
            if (inhala > 0f)
            {
                BrilloDeApertura.Intensidad(nucleo, respira * (1f - 0.75f * inhala));
                nucleo.transform.localScale = Vector3.one * 2.4f * (1f - 0.55f * inhala);
            }
            else nucleo.transform.localScale = Vector3.one * 2.4f;
            // La onda (13,4-14,3): sale del altar a ras de suelo hasta 16 m y enciende el suelo al pasar.
            float uOnda = Mathf.Clamp01((t - 13.4f) / 0.9f);
            bool hayOnda = t > 13.4f && t < 14.4f;
            Vector3 baseOnda = new Vector3(altar.x, origen.y + PiesDelMago.y + 0.15f, altar.z);
            for (int i = 0; i < onda.Count; i++)
            {
                float ang = i * Mathf.PI * 2f / onda.Count;
                float radio = Mathf.Lerp(0.6f, 16f, VeloDeApertura.Suave(uOnda));
                onda[i].transform.position = baseOnda + new Vector3(Mathf.Cos(ang) * radio, 0f, Mathf.Sin(ang) * radio);
                onda[i].transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 2.2f, uOnda);
                BrilloDeApertura.Intensidad(onda[i], hayOnda ? (1f - uOnda) * 1.6f + 0.2f : 0f);
                onda[i].transform.rotation = Quaternion.LookRotation(onda[i].transform.position - posCam);
            }
            if (hayOnda) BrilloDeApertura.Intensidad(suelo, respira * (1f + 4f * (1f - uOnda)));
            // Los anillos vuelven (14,2-15,3): se cierran sobre el mago y suben hasta el pecho, uno tras otro.
            for (int a = 0; a < anillos.Count; a++)
            {
                float t0a = 14.2f + a * 0.22f;
                float ua = Mathf.Clamp01((t - t0a) / 0.85f);
                bool vivo = t > t0a && t < t0a + 0.9f;
                float radio = Mathf.Lerp(7f - a, 0.15f, VeloDeApertura.Suave(ua));
                float y = Mathf.Lerp(0.2f, 1.2f, ua);
                Vector3 centro = origen + PiesDelMago + Vector3.up * y;
                var anillo = anillos[a];
                for (int i = 0; i < anillo.Count; i++)
                {
                    float ang = i * Mathf.PI * 2f / anillo.Count + a * 0.4f;
                    anillo[i].transform.position = centro + new Vector3(Mathf.Cos(ang) * radio, 0f, Mathf.Sin(ang) * radio);
                    BrilloDeApertura.Intensidad(anillo[i], vivo ? 0.5f + 1.3f * ua : 0f);
                    anillo[i].transform.rotation = Quaternion.LookRotation(anillo[i].transform.position - posCam);
                }
            }
            // Al entrar el último, la luz le sale un momento por dentro: el contraluz y su luz se disparan.
            float porDentro = (t > 15.0f && t < 15.9f) ? Mathf.Sin(Mathf.Clamp01((t - 15.0f) / 0.9f) * Mathf.PI) : 0f;
            if (porDentro > 0f) BrilloDeApertura.Intensidad(contraluz, 0.7f + 4f * porDentro);
            float entra = (t > 15.0f && t < 16.8f) ? Mathf.Sin(Mathf.Clamp01((t - 15.0f) / 1.8f) * Mathf.PI) : 0f;
            BrilloDeApertura.Intensidad(destelloPecho, entra * 1.8f);
            destelloPecho.transform.localScale = Vector3.one * (0.15f + 1.4f * entra);
            if (camara != null) camara.backgroundColor = Color.Lerp(FondoNegro, new Color(1f, 0.9f, 0.78f), estallido * estallido);
            // Fundido de entrada desde negro.
            // Sale del negro después del rótulo «Hace siglos…» (0-1,7 s), INC-1287.
            if (t < 3.6f) VeloDeApertura.Poner(Color.black, 1f - VeloDeApertura.Suave((t - 1.8f) / 1.8f));

            float fov = lenteBase.FieldOfView;
            if (t < 6.5f)
            {
                // Viene caminando hacia el altar; la cámara le sigue por detrás.
                float f = VeloDeApertura.Suave(t / 6.5f);
                if (cuerpo != null)
                {
                    cuerpo.position = origen + PiesDelMago - Vector3.forward * Mathf.Lerp(5f, 0f, f);
                    if (!andando) { andando = true; Locomocion(animMago); }
                    Caminar(animMago, 0.5f);
                }
                Vector3 pies = cuerpo != null ? cuerpo.position : origen + PiesDelMago;
                Vector3 desde = pies - Vector3.forward * 5.2f + Vector3.up * 1.55f + Vector3.right * 0.35f;
                cam.transform.SetPositionAndRotation(desde, Quaternion.LookRotation(altar - desde));
                fov = 40f;
            }
            else if (t < 13.5f)
            {
                // Quieto en el altar, de espaldas: pide el deseo. La cara no se ve todavía.
                if (cuerpo != null) { cuerpo.position = origen + PiesDelMago; if (andando) { andando = false; Caminar(animMago, 0f); Gesto(animMago, "Idle_Normal_NoWeapon", 0.2f); } }
                float f = VeloDeApertura.Suave((t - 6.5f) / 7f);
                Vector3 desde = origen + PiesDelMago + new Vector3(0.35f, 1.55f, Mathf.Lerp(-5.2f, -3.9f, f));
                cam.transform.SetPositionAndRotation(desde, Quaternion.LookRotation(altar - desde));
                fov = 40f;
                if (!dicho1 && t > 10.6f)
                {
                    dicho1 = true;
                    PresentadorDeLinea.Mostrar(cabeza != null ? cabeza : cuerpo, PresentadorDeLinea.Texto(textoDeseo), 3.4f,
                        PresentacionDeTexto.Subtitulo, nombreDelMago, new Color(0.71f, 0.55f, 1f), null,
                        enCinematica: true);
                    PresentadorDeLinea.SonarLaVoz(textoDeseo);
                }
            }
            else if (t < 15.2f)
            {
                // De lado: el altar le manda la luz al pecho. La cámara los coge a los dos.
                float f = VeloDeApertura.Suave((t - 13.5f) / 1.7f);
                Vector3 desde = origen + PiesDelMago + new Vector3(6.0f - 0.3f * f, 1.6f, 4.2f - 0.3f * f);
                Vector3 mira = Vector3.Lerp(pecho, altar, 0.38f);
                cam.transform.SetPositionAndRotation(desde, Quaternion.LookRotation(mira - desde));
                fov = 42f;
            }
            else if (t < 16.2f)
            {
                // Los pinchos del poder salen del pecho: empieza pegado a ellos y se aleja hasta su perfil.
                float f = VeloDeApertura.Suave((t - 15.2f) / 1.0f);
                Vector3 desde = Vector3.Lerp(pecho + new Vector3(1.3f, 0.2f, 1.3f),
                                             origen + PiesDelMago + new Vector3(5.6f, 2.1f, 4.4f), f);
                Vector3 mira = Vector3.Lerp(pecho, origen + PiesDelMago + Vector3.up * 1.3f, f);
                cam.transform.SetPositionAndRotation(desde, Quaternion.LookRotation(mira - desde));
                fov = Mathf.Lerp(34f, 46f, f);
                if (!hechizoDelEstallido)
                {
                    hechizoDelEstallido = true;
                    // Recibe el poder con los brazos abriéndose y se queda así (INC-1287): el «Conjuro_Esfuerzo»
                    // de antes se repetía mientras entraba el poder.
                    Gesto(animMago, "Conjuro_Abrirse", 0.3f);
                    if (ondaDelEstallido != null && VfxPoolService.Instance != null)
                        VfxPoolService.Instance.Play(ondaDelEstallido, pecho, Quaternion.identity, 2.5f);
                }
            }
            else if (t < 19.6f)
            {
                // «¡Por fin!» con los brazos al frente; la cámara rodea al mago hasta su cara y su sonrisa.
                float f = VeloDeApertura.Suave((t - 16.2f) / 3.4f);
                Vector3 centro = origen + PiesDelMago + Vector3.up * 1.3f;
                float ang = Mathf.Lerp(95f, 15f, f) * Mathf.Deg2Rad;
                float radio = Mathf.Lerp(4.2f, 2.3f, f);
                Vector3 desde = centro + new Vector3(Mathf.Sin(ang) * radio, 0.05f, Mathf.Cos(ang) * radio);
                Vector3 cara = cabeza != null ? cabeza.position : centro + Vector3.up * 0.3f;
                cam.transform.SetPositionAndRotation(desde, Quaternion.LookRotation(cara - desde));
                fov = Mathf.Lerp(44f, 30f, f);
                if (!gestoPoder)
                {
                    gestoPoder = true;
                    // «¡Por fin!» con los brazos ya abiertos: sin otro gesto encima (se veía repetido).
                    if (circuloMagico != null && VfxPoolService.Instance != null)
                        VfxPoolService.Instance.Play(circuloMagico, origen + PiesDelMago + Vector3.up * 0.05f, Quaternion.identity, 3.5f);
                    if (destello != null && VfxPoolService.Instance != null)
                        VfxPoolService.Instance.Play(destello, pecho, Quaternion.identity, 2f);
                }
                if (!dicho2 && t > 16.2f)
                {
                    dicho2 = true;
                    PresentadorDeLinea.Mostrar(cabeza != null ? cabeza : cuerpo, PresentadorDeLinea.Texto(textoPorFin), 1.8f,
                        PresentacionDeTexto.Subtitulo, nombreDelMago, new Color(0.71f, 0.55f, 1f), null,
                        enCinematica: true);
                    PresentadorDeLinea.SonarLaVoz(textoPorFin);
                }
                if (!risa && t > 17.8f)
                {
                    risa = true;
                    Gesto(animMago, "Laugh_Carcajada");
                    if (!string.IsNullOrEmpty(textoRisa)) PresentadorDeLinea.SonarLaVoz(textoRisa);
                }
                if (!sonrisa && f > 0.8f && emocion != null) { sonrisa = true; emocion.SetEmotion(NPCEmotion.Smirk); }
            }
            else if (t < 25.4f)
            {
                // «Clin» (INC-1287): la risa se corta en seco. Cámara delante de él, junto a su hombro; al
                // fondo, detrás de él y pequeño, late el punto dorado. Mira atrás por encima del hombro (gira
                // el cuerpo hacia la estrella): solo se le ve un ojo encendido. «…¿Y ese poder?» y, con los
                // ojos entornados, «No es ninguno de los que existen».
                Vector3 pies = cuerpo != null ? cuerpo.position : origen + PiesDelMago;
                Vector3 adelante = Vector3.forward;
                Vector3 cara = cabeza != null ? cabeza.position : pies + Vector3.up * 1.6f;
                Vector3 posPunto = pies - adelante * 26f + Vector3.up * 2.0f;
                float aparece = VeloDeApertura.Suave((t - 19.8f) / 0.9f);
                float latido = 0.9f + 0.1f * Mathf.Sin(t * 4f);
                estrella.transform.position = posPunto;
                brilloEstrella.transform.position = posPunto;
                estrella.transform.localScale = Vector3.one * Mathf.Lerp(0.01f, 0.55f, aparece) * latido;
                brilloEstrella.transform.localScale = Vector3.one * Mathf.Lerp(0.01f, 3.5f, aparece) * latido;

                if (!clin)
                {
                    clin = true;
                    Gesto(animMago, "SenseSomethingStart_NoWeapon", 0.12f);
                    AudioService.Instance?.PlaySFX("SFX_Prologo_Plin", 0.8f, pecho);
                }
                // Gira hacia atrás (hacia la estrella) por encima del hombro derecho.
                float giro = VeloDeApertura.Suave((t - 19.9f) / 0.55f);
                if (cuerpo != null) cuerpo.rotation = Quaternion.Euler(0f, Mathf.Lerp(0f, 108f, giro), 0f);
                if (!miraAtras && giro > 0.9f) { miraAtras = true; Gesto(animMago, "SenseSomethingSearching_NoWeapon", 0.2f); }

                // Cámara delante y a la derecha de su cabeza, mirando más allá de su hombro hacia la estrella.
                Vector3 desde = cara + new Vector3(1.35f, 0.12f, 2.3f);
                Vector3 mira = Vector3.Lerp(cara, posPunto, 0.05f) + new Vector3(-0.35f, 0f, 0f);
                cam.transform.SetPositionAndRotation(desde, Quaternion.LookRotation(mira - desde));
                fov = Mathf.Lerp(38f, 33f, VeloDeApertura.Suave((t - 19.6f) / 5.8f));

                if (!dicho3 && t > 20.6f)
                {
                    dicho3 = true;
                    PresentadorDeLinea.Mostrar(cabeza != null ? cabeza : cuerpo, PresentadorDeLinea.Texto(textoPoder), 2.2f,
                        PresentacionDeTexto.Subtitulo, nombreDelMago, new Color(0.71f, 0.55f, 1f), null,
                        enCinematica: true);
                    PresentadorDeLinea.SonarLaVoz(textoPoder);
                }
            }
            else
            {
                // Hacia la estrella: la cámara pasa por encima de su hombro y se va hacia el punto, cada vez
                // más deprisa, hasta que la pantalla se llena de dorado (papel en el último 1,2 s).
                Vector3 pies = cuerpo != null ? cuerpo.position : origen + PiesDelMago;
                Vector3 adelante = Vector3.forward;
                Vector3 cara = cabeza != null ? cabeza.position : pies + Vector3.up * 1.6f;
                Vector3 posPunto = pies - adelante * 26f + Vector3.up * 2.0f;
                float t0 = 25.4f;
                float latido = 0.9f + 0.1f * Mathf.Sin(t * 4f);
                float crece = VeloDeApertura.Suave(Mathf.Clamp01((t - t0) / Mathf.Max(0.1f, fin - t0)));
                estrella.transform.position = posPunto;
                brilloEstrella.transform.position = posPunto;
                estrella.transform.localScale = Vector3.one * Mathf.Lerp(0.55f, 0.9f, crece) * latido;
                brilloEstrella.transform.localScale = Vector3.one * Mathf.Lerp(3.5f, 9f, crece) * latido;

                Vector3 inicio = cara + new Vector3(1.35f, 0.12f, 2.3f);
                Vector3 hombro = cara + new Vector3(0.45f, 0.45f, -0.6f);
                float finViaje = Mathf.Max(0.1f, (fin - 0.6f) - t0);
                float u = Mathf.Clamp01((t - t0) / finViaje);
                float acelera = u * u;   // cada vez más deprisa
                Vector3 pos = u < 0.25f
                    ? Vector3.Lerp(inicio, hombro, VeloDeApertura.Suave(u / 0.25f))
                    : Vector3.Lerp(hombro, posPunto, Mathf.Clamp01((acelera - 0.0625f) / 0.9375f) * 0.96f);
                Vector3 mira = Vector3.Lerp(Vector3.Lerp(cara, posPunto, 0.08f), posPunto, VeloDeApertura.Suave(u / 0.3f));
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((mira - pos).sqrMagnitude > 0.0001f ? mira - pos : -adelante));
                fov = Mathf.Lerp(33f, 40f, u) - 6f * acelera;
                VeloDeApertura.Poner(VeloDeApertura.ColorPapel, VeloDeApertura.Suave((t - (fin - 1.2f)) / 1.2f));
            }

            // Vida en el cuerpo aunque el Animator no tenga el estado pedido: se mece al estar
            // quieto y da un paso con balanceo al andar. Si el Animator sí anima, esto solo mueve
            // la raíz y no se nota.
            if (cuerpo != null)
            {
                if (t < 6.5f)
                    cuerpo.rotation = Quaternion.Euler(0f, 2.2f * Mathf.Sin(t * 7.5f), 0f);
                else if (t < 19.6f)
                    cuerpo.rotation = Quaternion.Euler(Mathf.Sin(t * 1.1f) * 0.8f, Mathf.Sin(t * 0.7f) * 1.6f, 0f);
            }
            if (!Mathf.Approximately(cam.Lens.FieldOfView, fov))
            {
                var lente2 = cam.Lens;
                lente2.FieldOfView = fov;
                cam.Lens = lente2;
            }

            // Los brillos (menos el reflejo del suelo y las estrellas tenues) miran siempre a la cámara.
            posCam = cam.transform.position;
            foreach (var b in deCara)
                b.transform.rotation = Quaternion.LookRotation(b.transform.position - posCam);
            // Las motas suben despacio y se mecen; al salir por arriba vuelven por abajo.
            for (int i = 0; i < motes.Count; i++)
            {
                var m = motes[i];
                if (m == null) continue;
                var pm = m.transform.position;
                pm.y += dt * (0.05f + 0.02f * (i % 3));
                pm.x += Mathf.Sin(t * 0.7f + i) * 0.02f * dt;
                if (pm.y > ojos.y + 2.6f) pm.y = ojos.y - 1.4f;
                m.transform.position = pm;
                m.transform.rotation = Quaternion.LookRotation(pm - posCam);
            }
            yield return null;
        }
        VeloDeApertura.Poner(VeloDeApertura.ColorPapel, 1f);
        Limpiar();
    }

    /// Lanza un estado del Animator del mago, en la capa que lo tenga. Los gestos viven en la capa
    /// «UpperBody», que viene a peso 0 (la sube el script del NPC, que aquí está apagado): si no se
    /// sube, el gesto se reproduce pero no se ve. Se sube al pedir el primero.
    static void Gesto(Animator animator, string estado, float fundido = 0.25f)
    {
        if (animator == null) return;
        int h = Animator.StringToHash(estado);
        for (int capa = 0; capa < animator.layerCount; capa++)
        {
            if (!animator.HasState(capa, h)) continue;
            if (animator.GetLayerWeight(capa) < 0.5f) animator.SetLayerWeight(capa, 1f);
            animator.CrossFadeInFixedTime(h, fundido, capa);
            return;
        }
    }

    /// Entra en el estado de locomoción libre: «Walk» no es un estado, es el blend tree que hay
    /// dentro de este, y es el que mueve las piernas con InputMagnitude (0 quieto, 0,5 andar, 1 correr).
    static void Locomocion(Animator animator, float fundido = 0.2f)
    {
        if (animator == null) return;
        int h = Animator.StringToHash("Base Layer.Free Locomotion.Free Locomotion");
        if (animator.HasState(0, h)) { animator.CrossFadeInFixedTime(h, fundido, 0); return; }
        int corto = Animator.StringToHash("Free Locomotion");
        if (animator.HasState(0, corto)) animator.CrossFadeInFixedTime(corto, fundido, 0);
    }

    /// Pone al Animator a andar (o a pararse) sin motor: los parámetros de locomoción del NPC.
    static void Caminar(Animator animator, float velocidad)
    {
        if (animator == null) return;
        animator.SetFloat("InputVertical", velocidad);
        animator.SetFloat("InputHorizontal", 0f);
        animator.SetFloat("InputMagnitude", velocidad);
    }
}

/// Brillos de la apertura: un cuadrado con un degradado redondo que suma luz (el halo del altar, los ojos, las
/// estrellas). Se ven sobre el negro y los recoge el resplandor del postprocesado.
public static class BrilloDeApertura
{
    static Texture2D s_textura;
    static Mesh s_malla;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { s_textura = null; s_malla = null; }
#endif

    public static GameObject Crear(string nombre, Vector3 posicion, float tamano, Color color, List<GameObject> creados)
    {
        var go = new GameObject(nombre);
        go.transform.position = posicion;
        go.transform.localScale = Vector3.one * tamano;
        go.AddComponent<MeshFilter>().sharedMesh = Malla();
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = Material(color);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        creados?.Add(go);
        return go;
    }

    /// Destruye el material propio del brillo (cada brillo lleva el suyo para poder latir por separado).
    public static void SoltarMaterial(GameObject brillo)
    {
        var r = brillo.GetComponent<MeshRenderer>();
        if (r != null && r.sharedMaterial != null && r.sharedMaterial.name == NombreDelMaterial)
            UnityEngine.Object.Destroy(r.sharedMaterial);
    }

    /// Multiplica el color con el que se creó el brillo (0 = apagado).
    public static void Intensidad(GameObject brillo, float k)
    {
        var r = brillo.GetComponent<MeshRenderer>();
        if (r == null) return;
        // Apagado de verdad: si el shader de partículas no está (o no tiene las dos propiedades), el
        // color no se puede cambiar y el brillo se quedaba encendido siempre (INC-1287).
        bool encendido = k > 0.001f;
        if (r.enabled != encendido) r.enabled = encendido;
        if (!encendido) return;
        var m = r.sharedMaterial;
        if (!m.HasProperty(ColorBase) || !m.HasProperty(ColorOriginal)) return;
        m.SetColor(ColorBase, m.GetColor(ColorOriginal) * Mathf.Max(0f, k));
    }

    const string NombreDelMaterial = "BrilloDeApertura";
    static readonly int ColorBase = Shader.PropertyToID("_BaseColor");
    static readonly int ColorOriginal = Shader.PropertyToID("_EmissionColor");

    static Material Material(Color color)
    {
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        var m = new Material(sh) { name = NombreDelMaterial };
        m.SetTexture("_BaseMap", Textura());
        m.SetColor(ColorBase, color);
        // El color de partida se guarda en una propiedad que este shader no usa sin su palabra clave.
        m.SetColor(ColorOriginal, color);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    static Texture2D Textura()
    {
        if (s_textura != null) return s_textura;
        const int n = 128;
        var tx = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "BrilloDeApertura" };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 2.2f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tx.SetPixels32(px);
        tx.Apply(false, true);
        return s_textura = tx;
    }

    static Mesh Malla()
    {
        if (s_malla != null) return s_malla;
        var m = new Mesh { name = "BrilloDeApertura" };
        m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        m.RecalculateBounds();
        return s_malla = m;
    }
}

/// 2. El mapa del mundo en papel crema, con el punto del valle que late.
[Serializable]
public class MapaDeAperturaBeat : SequenceBeat
{
    public Texture2D mapa;
    [Tooltip("Dónde está el valle en el mapa (0–1, desde abajo a la izquierda).")]
    public Vector2 puntoDelValle = new Vector2(0.39f, 0.274f);
    public float segundos = 7f;

    public override string Describe() => $"Apertura: el mapa del mundo ({segundos:F1}s)";

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (ctx?.Player == null) yield break;
        var rutina = ctx.Player.StartCoroutine(Co(ctx));
        ctx.Player.TrackBackgroundRoutine(rutina);
    }

    IEnumerator Co(SequenceContext ctx)
    {
        var raiz = new GameObject("_MapaDeApertura");
        bool limpio = false;
        void Limpiar() { if (limpio) return; limpio = true; if (raiz != null) UnityEngine.Object.Destroy(raiz); }
        ctx.Player.RegisterCleanup(Limpiar);
        var lienzo = raiz.AddComponent<Canvas>();
        lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
        lienzo.sortingOrder = 4800;
        var fondo = new GameObject("Papel").AddComponent<Image>();
        fondo.transform.SetParent(raiz.transform, false);
        Estirar(fondo.rectTransform);
        fondo.color = VeloDeApertura.ColorPapel;
        var marco = new GameObject("Marco").AddComponent<RectTransform>();
        marco.SetParent(raiz.transform, false);
        Estirar(marco);
        var img = new GameObject("Mapa").AddComponent<RawImage>();
        img.transform.SetParent(marco, false);
        img.texture = mapa;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        float alto = Screen.height * 0.92f;
        float ancho = mapa != null ? alto * mapa.width / Mathf.Max(1, mapa.height) : alto;
        rt.sizeDelta = new Vector2(ancho, alto);
        var punto = new GameObject("Valle").AddComponent<Image>();
        punto.transform.SetParent(rt, false);
        punto.color = new Color(1f, 0.7f, 0.25f, 1f);
        var rp = punto.rectTransform;
        rp.anchorMin = rp.anchorMax = puntoDelValle;
        rp.sizeDelta = Vector2.one * (alto * 0.022f);
        // El papel tapa el velo; el velo se suelta ya.
        VeloDeApertura.Poner(VeloDeApertura.ColorPapel, 0f);

        float t = 0f;
        Vector2 aCentro = new Vector2((0.5f - puntoDelValle.x) * ancho, (0.5f - puntoDelValle.y) * alto);
        while (t < segundos)
        {
            t += Time.deltaTime;
            // Primero el mapa entero, quieto; luego se acerca despacio al valle.
            float f = VeloDeApertura.Suave((t - 1.8f) / (segundos - 1.8f));
            float zoom = Mathf.Lerp(1f, 1.7f, f * f);
            marco.localScale = Vector3.one * zoom;
            marco.anchoredPosition = aCentro * zoom * f;
            img.color = new Color(1f, 1f, 1f, VeloDeApertura.Suave(t / 0.8f));
            float latido = 1f + 0.45f * Mathf.Abs(Mathf.Sin(t * 4.2f));
            rp.localScale = Vector3.one * latido;
            if (t > segundos - 0.8f) VeloDeApertura.Poner(VeloDeApertura.ColorPapel, VeloDeApertura.Suave((t - (segundos - 0.8f)) / 0.8f));
            yield return null;
        }
        VeloDeApertura.Poner(VeloDeApertura.ColorPapel, 1f);
        Limpiar();
    }

    static void Estirar(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
}

/// 3. El vuelo: del mar a la cascada, sube por el agua (sale el logo) y llega sobre la plaza del valle, donde
/// empieza la grúa del guion.
[Serializable]
public class VueloDeAperturaBeat : SequenceBeat
{
    public Texture2D logo;
    public float segundos = 35f;
    [Tooltip("Nube del mar de nubes que atraviesa la cámara al bajar (Cloud3D). Vacío = sin nubes.")]
    public GameObject nube;

    // Luz de amanecer de cara a la cámara (contraluz dorado): el sol, bajo y hacia donde vuela.
    const float RumboDelSol = 10f, ElevacionDelSol = 9f;
    // Neblina con color en vez del gris del ciclo: malva lejos, más clara cuanto más alta va la cámara.
    static readonly Color ColorDeLaNeblina = new Color(0.72f, 0.74f, 0.96f);
    const string ClaveDeNiebla = "Vuelo de apertura";
    // El mar de nubes: capa entre estas alturas; la cámara la cruza bajando (velo blanco al pasar).
    const float NubesAbajo = 430f, NubesArriba = 540f;

    // Recorrido (mundo de MainWorld): posición de la cámara y adónde mira. Acaba en la grúa del guion (cam_cielo,
    // 10 m sobre la plaza, mirando al maestro).
    // El mundo entero desde muy alto (es «el mapa»: el mundo de verdad, no un pergamino), luego baja
    // hacia la costa suroeste, roza el mar, sube por la cascada y se posa en el valle.
    static readonly Vector3[] Pos =
    {
        new Vector3(-215f, 880f, -770f), new Vector3(-212f, 830f, -735f), new Vector3(-220f, 380f, -740f),
        new Vector3(-220f, 150f, -660f),
        new Vector3(-168f, 34f, -585f), new Vector3(-160f, 20f, -500f), new Vector3(-153.5f, 7f, -446f),
        new Vector3(-152.6f, 14f, -434f), new Vector3(-152.4f, 30f, -428.5f), new Vector3(-151.5f, 42f, -414f),
        // Llega al maestro a poca altura, de frente, y acaba justo donde empieza el plano del saludo (cam_baja,
        // 1,15 m sobre el suelo, mirándole): sin fundido ni corte, el maestro empieza a hablar (INC-1287).
        new Vector3(-155f, 47f, -392f), new Vector3(-162.5f, 40.5f, -379f), new Vector3(-170.0f, 35.15f, -366.0f),
    };
    static readonly Vector3[] Mira =
    {
        new Vector3(-55f, 0f, -75f), new Vector3(-55f, 0f, -110f), new Vector3(-150f, 30f, -420f),
        new Vector3(-155f, 25f, -440f),
        new Vector3(-152f, 20f, -425f), new Vector3(-152f, 16f, -425f), new Vector3(-152.4f, 14f, -423f),
        new Vector3(-152.4f, 26f, -422f), new Vector3(-151f, 40f, -405f), new Vector3(-158f, 36f, -372f),
        new Vector3(-166f, 34.5f, -364f), new Vector3(-166.8f, 35.0f, -362.7f), new Vector3(-166.8f, 35.0f, -362.7f),
    };
    // Tramo de la curva (en puntos) en el que se ve el logo: subiendo por la cascada.
    const float LogoDesdePunto = 7.4f, LogoHastaPunto = 9.8f;

    /// Tabla tiempo→s. La rapidez base sube con la altura (arriba planea, abajo no pasa todo de golpe) y
    /// cada tramo lleva un factor ajustado para que el vuelo de 35 s quede así: el mundo entero desde muy
    /// alto y ya bajando desde el primer segundo, 8-18 s baja hasta el mar, 18-26 s roza el agua hasta el
    /// pie de la cascada, 26-31 s sube por ella (con el logo) y 31-35 s se posa en el valle. Los factores
    /// se suavizan medio tramo a cada lado para que la velocidad no pegue saltos en los puntos.
    static readonly float[] FactorDelTramo =
        { 0.8f, 1.562f, 1.520f, 3.208f, 3.189f, 2.085f, 0.770f, 1.275f, 0.647f, 0.866f, 1.742f, 0.417f };
    static float[] s_tiempos;
    const int Muestras = 2400;
    static void PrepararTiempos()
    {
        if (s_tiempos != null) return;
        int tramos = Pos.Length - 1;
        var logF = new float[Muestras + 1];
        for (int i = 0; i <= Muestras; i++)
            logF[i] = Mathf.Log(FactorDelTramo[Mathf.Min(tramos - 1, (int)(i / (float)Muestras * tramos))]);
        int w = Muestras / tramos / 2;
        s_tiempos = new float[Muestras + 1];
        Vector3 prev = Curva(Pos, 0f);
        float acum = 0f;
        for (int i = 1; i <= Muestras; i++)
        {
            float suma = 0f;
            for (int k = -w; k <= w; k++) suma += logF[Mathf.Clamp(i + k, 0, Muestras)];
            float factor = Mathf.Exp(suma / (2 * w + 1));
            Vector3 p = Curva(Pos, i / (float)Muestras);
            float rapidez = (10f + Mathf.Max(0f, p.y - 30f) * 0.15f) * factor;
            acum += Vector3.Distance(prev, p) / rapidez;
            s_tiempos[i] = acum;
            prev = p;
        }
        for (int i = 0; i <= Muestras; i++) s_tiempos[i] /= acum;
    }
    static float SDeTiempo(float f)
    {
        int lo = 0, hi = Muestras;
        while (hi - lo > 1) { int m = (lo + hi) / 2; if (s_tiempos[m] < f) lo = m; else hi = m; }
        float a = s_tiempos[lo], b = s_tiempos[hi];
        return (lo + (b > a ? (f - a) / (b - a) : 0f)) / Muestras;
    }
    static float TiempoDeS(float sv) => s_tiempos[Mathf.Clamp(Mathf.RoundToInt(sv * Muestras), 0, Muestras)];

    public override string Describe() => $"Apertura: vuelo de cámara al valle con el logo ({segundos:F1}s)";

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (ctx?.Player == null) yield break;
        var rutina = ctx.Player.StartCoroutine(Co(ctx));
        ctx.Player.TrackBackgroundRoutine(rutina);
    }

    IEnumerator Co(SequenceContext ctx)
    {
        var cam = VeloDeApertura.CamaraPropia("_CamaraDelVuelo", 50f);
        var raiz = new GameObject("_LogoDeApertura");
        var nubes = MontarMarDeNubes();
        var ciclo = DayNightCycle.Instance;
        SolForzado sol = null;
        if (DayNightCycle.Sun != null)
        {
            sol = DayNightCycle.Sun.GetComponent<SolForzado>();
            if (sol == null) sol = DayNightCycle.Sun.gameObject.AddComponent<SolForzado>();
            Vector3 haciaElSol = Quaternion.Euler(-ElevacionDelSol, RumboDelSol, 0f) * Vector3.forward;
            sol.Colocar(Quaternion.LookRotation(-haciaElSol, Vector3.up), 0f);
        }
        bool limpio = false, solSuelto = false;
        void Limpiar()
        {
            if (limpio) return;
            limpio = true;
            if (cam != null) UnityEngine.Object.Destroy(cam.gameObject);
            if (raiz != null) UnityEngine.Object.Destroy(raiz);
            if (nubes != null) UnityEngine.Object.Destroy(nubes);
            if (ciclo != null) ciclo.SoltarNiebla(ClaveDeNiebla);
            if (sol != null && !solSuelto) sol.Soltar(0f);
            VeloDeApertura.Quitar();
        }
        ctx.Player.RegisterCleanup(Limpiar);
        var lienzo = raiz.AddComponent<Canvas>();
        lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
        lienzo.sortingOrder = 4700;
        var img = new GameObject("Logo").AddComponent<RawImage>();
        img.transform.SetParent(raiz.transform, false);
        img.texture = logo;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        float ancho = Screen.width * 0.34f;
        rt.sizeDelta = new Vector2(ancho, logo != null ? ancho * logo.height / Mathf.Max(1, logo.width) : ancho * 0.4f);
        img.color = new Color(1f, 1f, 1f, 0f);

        PrepararTiempos();
        float logoDesde = TiempoDeS(LogoDesdePunto / (Pos.Length - 1));
        float logoHasta = TiempoDeS(LogoHastaPunto / (Pos.Length - 1));
        float t = 0f;
        while (t < segundos)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / segundos);
            // Arranca y se posa con suavidad; entre medias, la velocidad la da la altura (PrepararTiempos).
            float fv = Mathf.Lerp(u, VeloDeApertura.Suave(u), 0.5f);
            float s = SDeTiempo(fv);
            Vector3 p = Curva(Pos, s), m = Curva(Mira, s);
            cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(m - p));
            if (t < 2f) VeloDeApertura.Poner(VeloDeApertura.ColorPapel, 1f - VeloDeApertura.Suave(t / 2f));
            else if (p.y < NubesArriba && p.y > NubesAbajo - 40f)
                // Dentro del mar de nubes: blanco que llega y se va.
                VeloDeApertura.Poner(new Color(1f, 0.97f, 0.93f), 0.9f * Mathf.Sin(Mathf.Clamp01((NubesArriba - p.y) / (NubesArriba - NubesAbajo + 40f)) * Mathf.PI));
            else if (t - Time.deltaTime < 2f || p.y <= NubesAbajo - 40f) VeloDeApertura.Poner(VeloDeApertura.ColorPapel, 0f);

            // Neblina: casi transparente arriba (se ve el mundo entero, con un velo malva lejos) y más
            // espesa abajo; en los últimos 3 s se devuelve la del ciclo para que el corte a la grúa no salte.
            if (ciclo != null)
            {
                float alto = VeloDeApertura.Suave((p.y - 40f) / 380f);
                float devolver = VeloDeApertura.Suave((t - (segundos - 3f)) / 3f);
                ciclo.PrestarNiebla(ClaveDeNiebla, Mathf.Lerp(Mathf.Lerp(0.35f, 0.08f, alto), 1f, devolver),
                    ColorDeLaNeblina, Mathf.Lerp(Mathf.Lerp(0.55f, 0.8f, alto), 0f, devolver));
            }
            if (sol != null && !solSuelto && t > segundos - 4f)
            {
                solSuelto = true;
                sol.Soltar(5f);
            }
            float a = VeloDeApertura.Suave((fv - logoDesde) / 0.07f) * (1f - VeloDeApertura.Suave((fv - (logoHasta - 0.07f)) / 0.07f));
            img.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }
        Limpiar();
    }

    /// Capa de nubes por debajo del vuelo alto: deja ver el mundo por los huecos y la cámara la atraviesa al
    /// bajar. Unas pocas más bajas, cerca del descenso hacia el mar, para que se note la velocidad.
    GameObject MontarMarDeNubes()
    {
        if (nube == null) return null;
        var raiz = new GameObject("_MarDeNubes");
        var azar = new System.Random(1200);
        float R(float a, float b) => a + (float)azar.NextDouble() * (b - a);
        void Poner(Vector3 pos, float ancho)
        {
            var n = UnityEngine.Object.Instantiate(nube, pos, Quaternion.Euler(0f, R(0f, 360f), 0f), raiz.transform);
            foreach (var mb in n.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            foreach (var c in n.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            var b = new Bounds(n.transform.position, Vector3.zero);
            foreach (var r in n.GetComponentsInChildren<Renderer>(true)) b.Encapsulate(r.bounds);
            float medida = Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            n.transform.localScale *= ancho / medida;
        }
        for (int i = 0; i < 46; i++)
            Poner(new Vector3(R(-560f, 420f), R(NubesAbajo, NubesArriba), R(-980f, 200f)), R(70f, 160f));
        // La que la cámara atraviesa al bajar.
        Poner(new Vector3(-216f, (NubesAbajo + NubesArriba) * 0.5f, -738f), 120f);
        for (int i = 0; i < 10; i++)
            Poner(new Vector3(R(-320f, -100f), R(180f, 260f), R(-760f, -560f)), R(30f, 60f));
        return raiz;
    }

    /// Catmull-Rom por los puntos, con s de 0 a 1 en todo el recorrido.
    static Vector3 Curva(Vector3[] p, float s)
    {
        int n = p.Length - 1;
        float f = Mathf.Clamp01(s) * n;
        int i = Mathf.Min(n - 1, Mathf.FloorToInt(f));
        float u = f - i;
        Vector3 p0 = p[Mathf.Max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(n, i + 2)];
        return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u);
    }
}
