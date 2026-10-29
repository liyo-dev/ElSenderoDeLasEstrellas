using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;
using Sendero.Core.Feedback;

/// Parte 2: semanas después, en el río. Pescan (cañas en la mano, con su sedal y su corcho), Tobías
/// dibuja a Will, el anzuelo de Will se lleva a Estela al agua; atardece y anochece con la cámara
/// mirando al cielo; de noche, tumbados en la hierba, la estrella de madera sigue allí arriba.
public partial class ModuloCinematicaFinal
{
    private const string ClaveVozLejana = "FinalVozQueSeAleja";
    private static readonly Vector3 SolDeLaTarde = new Vector3(-0.45f, 0.62f, -0.64f).normalized;

    private sealed class CanaEnMano
    {
        public Transform cana, mano, cuerpo;
        public Quaternion giro;            // respecto al cuerpo
        public Vector3 mango;               // punto del mango, en local de la caña
        public Vector3 punta;               // punta, en local de la caña
        public Vector3 corcho;              // dónde flota el corcho, en el mundo
        public LineRenderer sedal;
        public Transform bolaDelCorcho;
        public float tiron;                 // grados que se echa hacia atrás
        public Transform anzueloEn;         // si el anzuelo va enganchado a algo
    }

    private readonly List<CanaEnMano> _canas = new();
    private ConstelacionDeLaEstrella _constelacionRio;
    private bool _salpicaduraHecha;

    private float NivelDelAgua => _orillaRioTransform != null ? _orillaRioTransform.position.y : -0.1f;

    // ── De día ───────────────────────────────────────────────────────────────────────────────

    private void PrepararRioDia()
    {
        DevolverFisicaDeWill();
        MostrarSoloZona(_zonaRioRoot);
        FrenarAWill();
        ApagarEstrellas(0f);
        PonerCielo(_skyboxDia);
        AsegurarPublicadorDeAstros();
        SolYLunaEnElCielo.AstrosImpuestos = (SolDeLaTarde, DireccionConAltura(-SolDeLaTarde, -20f));
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.74f);
        RenderSettings.fog = false;
        if (_luzSolar != null)
        {
            _luzSolar.color = _colorSolDia;
            _luzSolar.intensity = 1.3f;
            _luzSolar.transform.rotation = Quaternion.LookRotation(-SolDeLaTarde, Vector3.up);
        }
        if (_luzNocheRioGrupo != null) _luzNocheRioGrupo.gameObject.SetActive(false);
        foreach (var alias in new[] { "will", "estela", "liam", "tobias" })
            MostrarActorEntero(ResolverActor(alias, null));
        AspectoFantasma.Quitar(ResolverActor("will", _willActor));

        // Cañas: la de Will en su mano y la otra en la de Liam (Tobías dibuja).
        PonerCanaEnMano(_canaPescarWill, ResolverActor("will", _willActor), 3.0f);
        PonerCanaEnMano(_canaPescarTobias, ResolverActor("liam", _liamActor), 2.6f);
        // Tobías no lleva el cuaderno en la mano: se lo dice y luego se ve el dibujo.
        if (_cuadernoDibujoTobias != null) _cuadernoDibujoTobias.SetActive(false);
        _encadenandoParte2 = false;
    }

    private void PonerCanaEnMano(GameObject cana, Transform actor, float distanciaDelCorcho)
    {
        if (cana == null || actor == null) return;
        var anim = AnimadorHumano(actor);
        var mano = anim != null ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
        if (mano == null) return;
        if (!EjeDeLaCana(cana, out Vector3 eje, out Vector3 mango, out Vector3 punta)) return;
        cana.SetActive(true);
        cana.transform.SetParent(transform, true);

        // La caña sale de la mano hacia delante y arriba, hacia el agua.
        Vector3 haciaElAgua = (actor.forward * 0.8f + Vector3.up * 0.62f).normalized;
        Quaternion giroMundo = Quaternion.FromToRotation(eje, haciaElAgua);
        var c = new CanaEnMano
        {
            cana = cana.transform,
            mano = mano,
            cuerpo = actor,
            giro = Quaternion.Inverse(actor.rotation) * giroMundo,
            mango = mango,
            punta = punta,
        };
        Vector3 suelo = new Vector3(actor.position.x, NivelDelAgua + 0.02f, actor.position.z);
        c.corcho = suelo + actor.forward * distanciaDelCorcho;

        var goSedal = new GameObject("Sedal");
        goSedal.transform.SetParent(transform, false);
        c.sedal = goSedal.AddComponent<LineRenderer>();
        c.sedal.useWorldSpace = true;
        c.sedal.positionCount = 10;
        c.sedal.widthMultiplier = 0.009f;
        c.sedal.sharedMaterial = MaterialDeLuz(new Color(0.92f, 0.94f, 0.96f, 0.75f));
        c.sedal.shadowCastingMode = ShadowCastingMode.Off;
        _instanciasTemporales.Add(goSedal);

        var bola = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bola.name = "Corcho";
        Destroy(bola.GetComponent<Collider>());
        bola.transform.SetParent(transform, false);
        bola.transform.localScale = Vector3.one * 0.07f;
        var matCorcho = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Corcho" };
        matCorcho.SetColor("_BaseColor", new Color(0.92f, 0.18f, 0.15f));
        bola.GetComponent<MeshRenderer>().sharedMaterial = matCorcho;
        c.bolaDelCorcho = bola.transform;
        _instanciasTemporales.Add(bola);
        _canas.Add(c);
    }

    private static Animator AnimadorHumano(Transform raiz)
    {
        foreach (var a in raiz.GetComponentsInChildren<Animator>())
            if (a != null && a.isHuman && a.GetBoneTransform(HumanBodyBones.RightHand) != null) return a;
        return null;
    }

    /// El eje largo de la caña (del mango a la punta) y sus dos extremos, en local de la caña.
    private static bool EjeDeLaCana(GameObject cana, out Vector3 eje, out Vector3 mango, out Vector3 punta)
    {
        eje = Vector3.up; mango = Vector3.zero; punta = Vector3.up;
        var mf = cana.GetComponentInChildren<MeshFilter>(true);
        if (mf == null || mf.sharedMesh == null) return false;
        Bounds b = mf.sharedMesh.bounds;
        Vector3 s = b.size;
        Vector3 ejeMalla = s.x >= s.y && s.x >= s.z ? Vector3.right : (s.y >= s.z ? Vector3.up : Vector3.forward);
        float largo = Vector3.Dot(s, ejeMalla);
        bool mangoEnElMinimo = Mathf.Abs(Vector3.Dot(b.min, ejeMalla)) <= Mathf.Abs(Vector3.Dot(b.max, ejeMalla));
        Vector3 haciaLaPunta = mangoEnElMinimo ? ejeMalla : -ejeMalla;
        Vector3 mangoMalla = b.center - haciaLaPunta * largo * 0.5f;
        Vector3 puntaMalla = b.center + haciaLaPunta * largo * 0.5f;
        var t = cana.transform;
        var h = mf.transform;
        mango = t.InverseTransformPoint(h.TransformPoint(mangoMalla));
        punta = t.InverseTransformPoint(h.TransformPoint(puntaMalla));
        eje = (punta - mango).normalized;
        return eje.sqrMagnitude > 0.5f;
    }

    /// Cada fotograma, después de la animación: la caña va con la mano, el sedal cuelga de la
    /// punta hasta el corcho (o hasta lo que se haya enganchado) y el corcho cabecea en el agua.
    private void ActualizarCanas()
    {
        for (int i = 0; i < _canas.Count; i++)
        {
            var c = _canas[i];
            if (c.cana == null || c.mano == null || c.cuerpo == null || !c.cana.gameObject.activeInHierarchy) continue;
            Quaternion giro = c.cuerpo.rotation * c.giro;
            if (c.tiron != 0f) giro = Quaternion.AngleAxis(-c.tiron, c.cuerpo.right) * giro;
            c.cana.rotation = giro;
            Vector3 agarre = Vector3.Lerp(c.mango, c.punta, 0.12f);
            c.cana.position = c.mano.position - giro * Vector3.Scale(agarre, c.cana.lossyScale);

            Vector3 punta = c.cana.TransformPoint(c.punta);
            Vector3 fin;
            if (c.anzueloEn != null) fin = c.anzueloEn.position;
            else
            {
                fin = c.corcho + Vector3.up * (Mathf.Sin(Time.time * 2.2f + i) * 0.015f);
                if (c.bolaDelCorcho != null) c.bolaDelCorcho.position = fin;
            }
            if (c.sedal != null)
            {
                float comba = c.anzueloEn != null ? 0.05f : 0.35f;
                for (int k = 0; k < c.sedal.positionCount; k++)
                {
                    float u = k / (float)(c.sedal.positionCount - 1);
                    c.sedal.SetPosition(k, Vector3.Lerp(punta, fin, u) + Vector3.down * Mathf.Sin(u * Mathf.PI) * comba);
                }
            }
        }
    }

    private void GuardarCanas()
    {
        foreach (var c in _canas)
        {
            if (c.cana != null) c.cana.gameObject.SetActive(false);
            if (c.sedal != null) c.sedal.enabled = false;
            if (c.bolaDelCorcho != null) c.bolaDelCorcho.gameObject.SetActive(false);
        }
    }

    /// Gira solo la cabeza de <paramref name="quien"/> hacia <paramref name="aQuien"/> (null: deja
    /// de mirar). Para los sentados, que no pueden girar el cuerpo.
    private static void MirarConLaCabeza(Transform quien, Transform aQuien)
    {
        if (quien == null) return;
        var mirada = quien.GetComponent<DialogueHeadLook>();
        if (mirada == null)
        {
            if (aQuien == null) return;
            mirada = quien.gameObject.AddComponent<DialogueHeadLook>();
        }
        mirada.SetTarget(aQuien);
    }

    private static string Texto(string clave, string respaldo)
        => LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(clave, respaldo) : respaldo;

    // ── El dibujo de Tobías ──────────────────────────────────────────────────────────────────

    /// Inserto a pantalla: la hoja del cuaderno con el dibujo de Will.
    private IEnumerator Co_DibujoDeTobias()
    {
        if (_dibujoDeTobias == null) yield break;
        var lienzo = new GameObject("Inserto_Dibujo_De_Tobias");
        _instanciasTemporales.Add(lienzo);
        var canvas = lienzo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Por debajo de los subtítulos: mientras se ve el dibujo se oyen y se leen los comentarios.
        canvas.sortingOrder = -1;
        var escalador = lienzo.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.matchWidthOrHeight = 1f;
        var grupo = lienzo.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;

        var fondo = new GameObject("Fondo").AddComponent<Image>();
        fondo.transform.SetParent(lienzo.transform, false);
        fondo.color = new Color(0.08f, 0.1f, 0.06f, 0.92f);
        var rf = fondo.rectTransform;
        rf.anchorMin = Vector2.zero; rf.anchorMax = Vector2.one; rf.offsetMin = rf.offsetMax = Vector2.zero;

        var hoja = new GameObject("Hoja").AddComponent<RawImage>();
        hoja.transform.SetParent(lienzo.transform, false);
        hoja.texture = _dibujoDeTobias;
        float alto = 760f, ancho = alto * _dibujoDeTobias.width / Mathf.Max(1f, _dibujoDeTobias.height);
        hoja.rectTransform.sizeDelta = new Vector2(ancho, alto);
        hoja.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -3.5f);

        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime) { grupo.alpha = t / 0.3f; yield return null; }
        grupo.alpha = 1f;
        // Se queda hasta la etiqueta @fin_dibujo del guion (o 2,6 s si no la hay).
        float fin = Etiqueta("fin_dibujo");
        float dura = fin > 0f && TiempoDelGuion() >= 0f ? Mathf.Max(1f, fin - TiempoDelGuion() - 0.3f) : 2.6f;
        for (float t = 0f; t < dura; t += Time.unscaledDeltaTime)
        {
            hoja.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.05f, t / dura);
            yield return null;
        }
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime) { grupo.alpha = 1f - t / 0.3f; yield return null; }
        _instanciasTemporales.Remove(lienzo);
        Destroy(lienzo);
    }

    // ── El anzuelo se lleva a Estela ─────────────────────────────────────────────────────────

    private IEnumerator Co_TironDeLaCana()
    {
        CanaEnMano cana = null;
        var will = ResolverActor("will", _willActor);
        foreach (var c in _canas) if (c.cuerpo == will) cana = c;
        var estela = ResolverActor("estela", _estelaActor);
        if (cana == null || estela == null) yield break;
        _salpicaduraHecha = false;

        // Will tira con todo hacia atrás...
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            cana.tiron = Mathf.Lerp(0f, 70f, t / 0.25f);
            yield return null;
        }
        // ...el anzuelo sale del agua, pasa por encima de su cabeza y se engancha en Estela.
        var anzuelo = new GameObject("Anzuelo").transform;
        anzuelo.SetParent(transform, false);
        _instanciasTemporales.Add(anzuelo.gameObject);
        Vector3 desde = cana.corcho;
        var animEstela = AnimadorHumano(estela);
        Transform espalda = animEstela != null ? animEstela.GetBoneTransform(HumanBodyBones.Chest) : estela;
        if (cana.bolaDelCorcho != null) cana.bolaDelCorcho.gameObject.SetActive(false);
        cana.anzueloEn = anzuelo;
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            float k = t / 0.35f;
            Vector3 hasta = espalda != null ? espalda.position : estela.position + Vector3.up;
            anzuelo.position = Vector3.Lerp(desde, hasta, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 2.2f;
            yield return null;
        }
        anzuelo.SetParent(espalda != null ? espalda : estela, true);
        anzuelo.localPosition = Vector3.zero;
        AudioService.Instance?.PlaySFX("Prologue_SpellRelease", 0.35f);

        // Estela vuela (el guion la lleva) y al tocar el agua salpica y el sedal se suelta.
        // Si por lo que sea no llega a bajar del nivel del agua, salpica igual al acabar la caída.
        for (float t = 0f; t < 2.2f && !_salpicaduraHecha; t += Time.deltaTime)
        {
            cana.tiron = Mathf.Lerp(cana.tiron, 25f, Time.deltaTime * 3f);
            if (estela.position.y < NivelDelAgua - 0.05f) break;
            yield return null;
        }
        if (!_salpicaduraHecha)
        {
            Salpicar(estela.position, 1.6f);
            AudioService.Instance?.PlaySFX("Agua_Zambullida", 1f);
            FeedbackService.CameraShake(0.3f, 0.3f);
            _salpicaduraHecha = true;
        }
        cana.anzueloEn = null;
        if (cana.bolaDelCorcho != null) cana.bolaDelCorcho.gameObject.SetActive(true);
        if (cana.sedal != null) cana.sedal.enabled = false;
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            cana.tiron = Mathf.Lerp(cana.tiron, 0f, t / 0.6f);
            yield return null;
        }
        cana.tiron = 0f;
    }

    private void Salpicar(Vector3 donde, float escala)
    {
        Vector3 p = new Vector3(donde.x, NivelDelAgua + 0.02f, donde.z);
        SalpicaduraDeAgua(p, escala);
        if (_vfxSalpicaduraRio == null) return;
        var copia = Instantiate(_vfxSalpicaduraRio, p, Quaternion.identity);
        copia.transform.localScale = _vfxSalpicaduraRio.transform.localScale * escala;
        copia.SetActive(true);
        foreach (var ps in copia.GetComponentsInChildren<ParticleSystem>(true)) ps.Play(true);
        _instanciasTemporales.Add(copia);
        Destroy(copia, 4f);
    }

    /// Una zambullida hecha aquí, para que se vea siempre: una corona de gotas que sube y cae,
    /// espuma blanca en el sitio y ondas que se abren sobre el agua.
    private void SalpicaduraDeAgua(Vector3 p, float escala)
    {
        var raiz = new GameObject("Salpicadura_Del_Rio");
        raiz.transform.position = p;
        _instanciasTemporales.Add(raiz);
        Destroy(raiz, 4f);

        // Gotas: salen hacia arriba y hacia fuera, y caen.
        var gotas = SistemaDeAgua(raiz.transform, "Gotas", 0.55f, 1.1f, new ParticleSystem.MinMaxCurve(0.05f * escala, 0.13f * escala), 1.3f);
        var mainG = gotas.main;
        mainG.startSpeed = new ParticleSystem.MinMaxCurve(2.6f * escala, 4.6f * escala);
        var formaG = gotas.shape;
        formaG.enabled = true;
        formaG.shapeType = ParticleSystemShapeType.Cone;
        formaG.angle = 28f;
        formaG.radius = 0.25f * escala;
        formaG.rotation = new Vector3(-90f, 0f, 0f);
        gotas.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 90), new ParticleSystem.Burst(0.08f, 40) });

        // Espuma: bolas blancas grandes que se quedan un momento en el sitio.
        var espuma = SistemaDeAgua(raiz.transform, "Espuma", 0.35f, 0.7f, new ParticleSystem.MinMaxCurve(0.3f * escala, 0.55f * escala), 0.5f);
        var mainE = espuma.main;
        mainE.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * escala, 1.8f * escala);
        var formaE = espuma.shape;
        formaE.enabled = true;
        formaE.shapeType = ParticleSystemShapeType.Hemisphere;
        formaE.radius = 0.3f * escala;
        espuma.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });

        // Ondas: anillos planos sobre el agua que crecen y se apagan.
        var ondas = SistemaDeAgua(raiz.transform, "Ondas", 1.5f, 1.9f, 0.4f * escala, 0f);
        ondas.transform.localPosition = Vector3.up * 0.01f;
        var mainO = ondas.main;
        mainO.startSpeed = 0f;
        var formaO = ondas.shape;
        formaO.enabled = false;
        ondas.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1), new ParticleSystem.Burst(0.3f, 1), new ParticleSystem.Burst(0.65f, 1) });
        var tamO = ondas.sizeOverLifetime;
        tamO.enabled = true;
        tamO.size = new ParticleSystem.MinMaxCurve(6.5f, AnimationCurve.EaseInOut(0f, 0.15f, 1f, 1f));
        var rO = ondas.GetComponent<ParticleSystemRenderer>();
        rO.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        rO.sharedMaterial.mainTexture = TexturaDeOnda();
        foreach (var ps in raiz.GetComponentsInChildren<ParticleSystem>()) ps.Play(false);
    }

    private ParticleSystem SistemaDeAgua(Transform padre, string nombre, float vidaMin, float vidaMax, ParticleSystem.MinMaxCurve tamano, float gravedad)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(vidaMin, vidaMax);
        main.startSize = tamano;
        main.startColor = new Color(0.86f, 0.95f, 1f, 0.95f);
        main.gravityModifier = gravedad;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        var emision = ps.emission;
        emision.rateOverTime = 0f;
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.4f));
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var degradado = new Gradient();
        degradado.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.75f, 0.9f, 1f), 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
        color.color = degradado;
        var r = go.GetComponent<ParticleSystemRenderer>();
        var mat = MaterialDeLuz(new Color(1f, 1f, 1f, 1f));
        mat.mainTexture = TexturaDeChispa();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    private static Texture2D s_texturaDeOnda;

    /// Un anillo blando, para las ondas del agua.
    private static Texture2D TexturaDeOnda()
    {
        if (s_texturaDeOnda != null) return s_texturaDeOnda;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Onda del río", wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) / 0.12f);
            px[y * n + x] = new Color(1f, 1f, 1f, a * a * 0.9f);
        }
        tex.SetPixels(px);
        tex.Apply();
        s_texturaDeOnda = tex;
        return tex;
    }

    private IEnumerator Co_EstelaSaleDelAgua()
    {
        var estela = ResolverActor("estela", _estelaActor);
        if (estela == null) yield break;
        Salpicar(estela.position, 1.1f);
        AudioService.Instance?.PlaySFX("Agua_SalirAFlote", 1f);
        // Se acabó la pesca: las cañas se dejan en la hierba (fuera de cuadro) y no cruzan los planos.
        GuardarCanas();
        // Chorrea al salir.
        var gotas = Chispas(estela.position, new Color(0.7f, 0.85f, 1f), 40f, 0.3f, -1.4f, estela);
        yield return new WaitForSeconds(3.5f);
        if (gotas != null) gotas.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // ── Anochece ─────────────────────────────────────────────────────────────────────────────

    private IEnumerator Co_Anochecer()
    {
        const float duracion = 9f;
        if (_skyboxDia == null || _skyboxNoche == null) { PrepararNocheEnElRio(); yield break; }
        var cielo = new Material(_skyboxDia) { name = "Cielo anocheciendo" };
        PonerCielo(cielo);
        var estrellas = Cielo(true);
        estrellas.BrilloYa(0f);
        Vector3 lunaDesde = DireccionConAltura(DireccionDeLaLuna, -12f);
        Vector3 solHasta = DireccionConAltura(SolDeLaTarde, -16f);
        Color ambienteDia = new(0.62f, 0.66f, 0.74f), ambienteTarde = new(0.55f, 0.42f, 0.4f), ambienteNoche = new(0.1f, 0.12f, 0.22f);
        Color luzTarde = new(1f, 0.55f, 0.32f);

        for (float t = 0f; t < duracion; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duracion);
            if (k < 0.5f && _skyboxAtardecer != null) cielo.Lerp(_skyboxDia, _skyboxAtardecer, k * 2f);
            else if (_skyboxAtardecer != null) cielo.Lerp(_skyboxAtardecer, _skyboxNoche, (k - 0.5f) * 2f);
            else cielo.Lerp(_skyboxDia, _skyboxNoche, k);

            Vector3 sol = Vector3.Slerp(SolDeLaTarde, solHasta, k);
            Vector3 luna = Vector3.Slerp(lunaDesde, DireccionDeLaLuna, Mathf.InverseLerp(0.4f, 1f, k));
            SolYLunaEnElCielo.AstrosImpuestos = (sol, luna);
            RenderSettings.ambientLight = k < 0.5f ? Color.Lerp(ambienteDia, ambienteTarde, k * 2f) : Color.Lerp(ambienteTarde, ambienteNoche, (k - 0.5f) * 2f);
            if (_luzSolar != null)
            {
                _luzSolar.color = k < 0.5f ? Color.Lerp(_colorSolDia, luzTarde, k * 2f) : Color.Lerp(luzTarde, _colorLuzNoche, (k - 0.5f) * 2f);
                _luzSolar.intensity = Mathf.Lerp(1.3f, 0.4f, k);
                Vector3 desdeDonde = k < 0.6f ? sol : luna;
                if (desdeDonde.y < 0.05f) desdeDonde.y = 0.05f;
                _luzSolar.transform.rotation = Quaternion.Slerp(_luzSolar.transform.rotation, Quaternion.LookRotation(-desdeDonde.normalized, Vector3.up), Time.deltaTime * 2f);
            }
            estrellas.BrilloYa(Mathf.InverseLerp(0.45f, 1f, k));
            // La estrella de Will sale poco a poco con las demás, no de golpe.
            if (_constelacionRio == null && k > 0.5f) _constelacionRio = CrearConstelacionDelRio(duracion * 0.5f);
            yield return null;
        }
        PrepararNocheEnElRio();
        Destroy(cielo);
    }

    // ── De noche ─────────────────────────────────────────────────────────────────────────────

    private void PrepararNocheEnElRio()
    {
        MostrarSoloZona(_zonaRioRoot);
        GuardarCanas();
        if (_cuadernoDibujoTobias != null) _cuadernoDibujoTobias.SetActive(false);
        CieloDeNocheDelJuego(true, 0f);
        LuzDeNoche(0.4f, new Color(0.1f, 0.12f, 0.22f));
        if (_luzNocheRioGrupo != null) _luzNocheRioGrupo.gameObject.SetActive(true);

        if (_constelacionRio == null) _constelacionRio = CrearConstelacionDelRio(2.5f);
        if (_luciernagas == null) _luciernagas = Luciernagas(CentroRio + new Vector3(0.5f, 0f, 1f));
    }

    private ParticleSystem _luciernagas;

    /// Luciérnagas sobre la hierba y la orilla: puntitos verdes y amarillos que van y vienen.
    private ParticleSystem Luciernagas(Vector3 centro)
    {
        var go = new GameObject("Luciernagas_Del_Rio");
        go.transform.position = centro + Vector3.up * 0.9f;
        _instanciasTemporales.Add(go);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 6f;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 1f, 0.35f), new Color(1f, 0.9f, 0.35f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        var emision = ps.emission;
        emision.rateOverTime = 14f;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = new Vector3(16f, 1.4f, 9f);
        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.35f;
        ruido.frequency = 0.25f;
        ruido.scrollSpeed = 0.2f;
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var degradado = new Gradient();
        degradado.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.2f, 0.4f),
                                  new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0.3f, 0.8f), new GradientAlphaKey(0f, 1f) });
        color.color = degradado;
        var r = go.GetComponent<ParticleSystemRenderer>();
        var mat = MaterialDeLuz(new Color(2.2f, 2.6f, 1.2f, 1f));
        mat.mainTexture = TexturaDeChispa();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    private ConstelacionDeLaEstrella CrearConstelacionDelRio(float segundosEnAparecer)
    {
        Vector3 centro = PuntoDelGuion("p_constelacion_rio", CentroRio + new Vector3(6f, 24f, -22f));
        if (centro.y < 5f) centro.y = 24f;
        var c = ConstelacionDeLaEstrella.Crear(centro, CentroRio + Vector3.up, 4.2f, transform);
        c.Aparecer(segundosEnAparecer);
        _instanciasTemporales.Add(c.gameObject);
        return c;
    }

    /// El tema de la noche entra en el segundo justo para que su última frase llegue con la frase
    /// final del libro (etiqueta @frase del guion).
    private void MusicaDeLaNoche() => TemaACompas("FINAL_ARCHIMAGO", "frase", 250f, 3f, 150f);

    /// La voz de Will se aleja con la cámara mientras cuenta la historia.
    private IEnumerator Co_VozQueSeAleja()
    {
        var audio = AudioService.Instance;
        if (audio == null) yield break;
        for (float t = 0f; t < 16f; t += Time.unscaledDeltaTime)
        {
            audio.AtenuarBus(ClaveVozLejana, AudioBus.Dialogue, Mathf.Lerp(1f, 0.18f, Mathf.SmoothStep(0f, 1f, t / 16f)));
            yield return null;
        }
    }

    private IEnumerator Co_EnfocarEstrella()
    {
        if (_constelacionRio == null) PrepararNocheEnElRio();
        for (float t = 0f; t < 4f && _constelacionRio != null; t += Time.deltaTime)
        {
            _constelacionRio.Brillo(Mathf.Lerp(1f, 1.9f, Mathf.SmoothStep(0f, 1f, t / 4f)));
            yield return null;
        }
    }

    // ── Liam se ha dormido ───────────────────────────────────────────────────────────────────

    private const float DuracionDelSuenoDeLiam = 45f;

    /// Zetas que le salen de la cabeza a Liam mientras duerme: suben, se mecen y se apagan.
    private IEnumerator Co_LiamDuerme()
    {
        var liam = ResolverActor("liam", _liamActor);
        if (liam == null) yield break;
        MantenerOjosCerrados(liam, true);
        var anim = AnimadorHumano(liam);
        var cabeza = anim != null ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        int n = 0;
        for (float t = 0f; t < DuracionDelSuenoDeLiam && _enMarcha; t += 0.85f, n++)
        {
            Vector3 desde = cabeza != null ? cabeza.position + Vector3.up * 0.25f : liam.position + Vector3.up * 0.5f;
            StartCoroutine(Co_UnaZeta(desde, n % 3 == 2 ? "Z" : "z", n % 2 == 0 ? 1f : -1f));
            yield return new WaitForSeconds(0.85f);
        }
    }

    private IEnumerator Co_UnaZeta(Vector3 desde, string letra, float lado)
    {
        var go = new GameObject("Zeta_De_Liam");
        go.transform.SetParent(transform, false);
        _instanciasTemporales.Add(go);
        var texto = go.AddComponent<TextMeshPro>();
        if (_fuenteDeRotulo != null) texto.font = _fuenteDeRotulo;
        texto.text = letra;
        float tamano = letra == "Z" ? 2.2f : 1.6f;
        texto.alignment = TextAlignmentOptions.Center;
        texto.fontStyle = FontStyles.Bold;
        Color color = new Color(0.85f, 0.92f, 1f, 0f);
        texto.color = color;
        const float dura = 2.6f;
        for (float t = 0f; t < dura && go != null; t += Time.deltaTime)
        {
            float k = t / dura;
            go.transform.position = desde + new Vector3(Mathf.Sin(k * Mathf.PI * 2f) * 0.12f * lado + k * 0.25f * lado, k * 0.75f, 0f);
            var cam = CamaraPrincipal();
            if (cam != null) go.transform.rotation = cam.transform.rotation;
            texto.fontSize = tamano * Mathf.Lerp(0.6f, 1.2f, k);
            color.a = k < 0.2f ? k / 0.2f : 1f - Mathf.InverseLerp(0.6f, 1f, k);
            texto.color = color;
            yield return null;
        }
        if (go != null) { _instanciasTemporales.Remove(go); Destroy(go); }
    }

    private IEnumerator Co_FraseFinal()
    {
        AudioService.Instance?.SoltarAtenuacionDeBus(ClaveVozLejana, AudioBus.Dialogue);
        var frase = FraseEstelar.Mostrar(
            Texto("FINAL_FRASE_LIBRO", "Sobre sus cabezas, entre miles de estrellas, una brillaba un poco más que las demás."),
            _fuenteDeRotulo, 4.5f, 4.0f, 2.5f);
        if (frase != null) _instanciasTemporales.Add(frase.gameObject);
        yield break;
    }
}
