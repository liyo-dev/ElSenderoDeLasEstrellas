using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;

/// Actos 2, 3 y 4: Will conjura en el Sendero destrozado (círculos, viento hacia arriba,
/// levitación) y se desvanece; Liam se materializa en la Piedra; el espíritu del Archimago aparece
/// y se convierte en la constelación de la estrella de madera de Will.
public partial class ModuloCinematicaFinal
{
    private static readonly Color Dorado = new(1f, 0.86f, 0.5f);
    private static readonly Color BlancoCalido = new(1f, 0.95f, 0.85f);

    private Transform _circuloWill, _circuloGrande, _circuloLiam, _auraWill, _tornado;
    private ParticleSystem _vientoHaciaArriba;
    private Light _luzDelConjuro;

    // Levitación (Will y el Archimago): altura sobre su sitio horneado, aplicada en LateUpdate.
    private bool _levitando;
    private float _alturaWill, _alturaWillObjetivo, _velocidadAlturaWill;
    private bool _archimagoFlotando;
    private float _alturaArchimago = 0.45f, _alturaArchimagoObjetivo = 0.45f, _velocidadAlturaArchimago = 0.3f;
    private bool _fisicaGuardada, _eraCinematico, _usabaGravedad;

    // ── Acto 2: el conjuro ───────────────────────────────────────────────────────────────────

    private void AplicarSenderoDestruido()
    {
        AplicarAmbienteSendero();
        RenderSettings.ambientLight = new Color(0.16f, 0.15f, 0.3f);
        if (_luzSolar != null)
        {
            _luzSolar.color = new Color(0.62f, 0.6f, 0.95f);
            _luzSolar.intensity = 0.85f;
        }
        // Del camino solo queda un trozo alrededor de Will y Liam, y sigue cayendo.
        _centroQueAguanta = new Vector3(-0.5f, 0f, 4.2f);
        _radioQueAguanta = Mathf.Min(_radioQueAguanta, 7.5f);
        _radioObjetivo = 2.6f;
        _velocidadDelRadio = 0.16f;
        _intensidadDelColapso = 1.2f;
        if (!_colapsoActivo) Lanzar(Co_ColapsoDelSendero());
        MostrarActorEntero(ResolverActor("will", _willActor));
    }

    private IEnumerator Co_ConjuroCirculos()
    {
        var will = ResolverActor("will", _willActor);
        var liam = ResolverActor("liam", _liamActor);
        Vector3 pw = will != null ? will.position : new Vector3(0f, 0f, 4.2f);
        Vector3 pl = liam != null ? liam.position : new Vector3(-1.2f, 0f, 4.2f);
        pw.y = 0f; pl.y = 0f;

        _circuloWill = Efecto(_prefabCirculoMagico, pw + Vector3.up * 0.05f, Quaternion.identity, 0f, 1.2f);
        _circuloGrande = Efecto(_prefabCirculoMagico2, (pw + pl) * 0.5f + Vector3.up * 0.03f, Quaternion.identity, 0f, 2.4f);
        _circuloLiam = Efecto(_prefabCirculoMagico2, pl + Vector3.up * 0.06f, Quaternion.identity, 0f, 0.9f);
        if (will != null) _auraWill = Efecto(_prefabAuraEstelar, will.position, Quaternion.identity, 0f, 0.6f, will);
        _luzDelConjuro = CrearLuz("Luz_Del_Conjuro", pw + Vector3.up * 0.6f, Dorado, 0f, 9f);
        AudioService.Instance?.PlaySFX("Prologo_MagiaLevitar", 0.8f);

        // Los círculos crecen y giran mientras dura el conjuro; la luz sube con ellos.
        for (float t = 0f; t < 14f; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / 14f);
            if (_circuloWill != null) { _circuloWill.localScale = Vector3.one * Mathf.Lerp(1.2f, 1.6f, k); _circuloWill.Rotate(0f, 25f * Time.deltaTime, 0f, Space.World); }
            if (_circuloGrande != null) { _circuloGrande.localScale = Vector3.one * Mathf.Lerp(2.4f, 3.0f, k); _circuloGrande.Rotate(0f, -12f * Time.deltaTime, 0f, Space.World); }
            if (_luzDelConjuro != null) _luzDelConjuro.intensity = Mathf.Lerp(0f, 7f, Mathf.Clamp01(t / 2.5f)) * (1f + 0.12f * Mathf.Sin(t * 7f));
            yield return null;
        }
    }

    private IEnumerator Co_LevitarWill()
    {
        var will = ResolverActor("will", _willActor);
        if (will == null) yield break;
        GuardarFisicaDeWill(will);
        _levitando = true;
        _alturaWill = 0f;
        _alturaWillObjetivo = 1.25f;
        _velocidadAlturaWill = 0.42f;

        // El viento sube desde el suelo: un remolino y polvo de luz que asciende alrededor de Will.
        Vector3 pw = new Vector3(will.position.x, 0f, will.position.z);
        _tornado = Efecto(_prefabTornado, pw, Quaternion.identity, 0f, 0.8f);
        _vientoHaciaArriba = Chispas(pw, new Color(0.85f, 0.92f, 1f), 70f, 1.7f, 3.4f);
        AudioService.Instance?.PlaySFX("Prologo_Carga", 0.7f);
        yield break;
    }

    private void GuardarFisicaDeWill(Transform will)
    {
        if (_fisicaGuardada || !will.TryGetComponent<Rigidbody>(out var rb)) return;
        _fisicaGuardada = true;
        _eraCinematico = rb.isKinematic;
        _usabaGravedad = rb.useGravity;
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void DevolverFisicaDeWill()
    {
        _levitando = false;
        if (!_fisicaGuardada) return;
        _fisicaGuardada = false;
        var will = ResolverActor("will", _willActor);
        if (will == null || !will.TryGetComponent<Rigidbody>(out var rb)) return;
        rb.isKinematic = _eraCinematico;
        rb.useGravity = _usabaGravedad;
    }

    /// El guion pone a Will (y al Archimago) en su sitio horneado cada fotograma; aquí se les
    /// sube encima, contando desde ese sitio (no desde donde estén, que ya puede llevar la altura).
    private void AplicarLevitacion()
    {
        var will = _willEnEscena;
        if (will == null) return;
        _alturaWill = Mathf.MoveTowards(_alturaWill, _alturaWillObjetivo, _velocidadAlturaWill * Time.deltaTime);
        float suave = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, Mathf.Max(0.01f, _alturaWillObjetivo), _alturaWill)) * _alturaWillObjetivo;
        float vaiven = Mathf.Sin(Time.time * 1.6f) * 0.05f * Mathf.Clamp01(_alturaWill / 0.4f);
        Vector3 suelo = PosicionHorneada(SequenceActor.PlayerId, will.position);
        will.position = suelo + Vector3.up * (suave + vaiven);
        if (will.TryGetComponent<Rigidbody>(out var rb)) rb.position = will.position;
    }

    private void AplicarFlotacionDelArchimago()
    {
        var arch = ResolverActor("archimago", _archimagoActor);
        if (arch == null) return;
        _alturaArchimago = Mathf.MoveTowards(_alturaArchimago, _alturaArchimagoObjetivo, _velocidadAlturaArchimago * Time.deltaTime);
        float vaiven = Mathf.Sin(Time.time * 1.3f) * 0.07f;
        Vector3 suelo = PosicionHorneada("Actor_Archimago", arch.position);
        arch.position = suelo + Vector3.up * (_alturaArchimago + vaiven);
    }

    /// Dónde pone el guion horneado a un actor en este segundo (o el respaldo si no hay guion).
    private static Vector3 PosicionHorneada(string id, Vector3 respaldo)
    {
        var guion = GuionEnMarcha.Actual;
        var datos = guion?.Guion != null ? guion.Guion.Actor(id) : null;
        if (datos == null || datos.posiciones == null || datos.posiciones.Count == 0) return new Vector3(respaldo.x, 0f, respaldo.z);
        return Interpolar.Posicion(datos.posiciones, guion.Tiempo);
    }

    private IEnumerator Co_ConjuroClimax()
    {
        var will = ResolverActor("will", _willActor);
        var liam = ResolverActor("liam", _liamActor);
        Vector3 pl = liam != null ? liam.position : new Vector3(-1.2f, 0f, 4.2f);
        Vector3 pw = will != null ? will.position : new Vector3(0f, 1.2f, 4.2f);

        // La luz baja sobre Liam y se lo lleva.
        Efecto(_prefabPilarDeLuz, new Vector3(pl.x, 0f, pl.z), Quaternion.identity, 6f, 1.6f);
        Efecto(_prefabGolpeSagrado, pl + Vector3.up * 0.6f, Quaternion.identity, 3f, 1.6f);
        Efecto(_prefabLluviaDeEstrellas, new Vector3(pw.x, 0.05f, pw.z), Quaternion.identity, 5f, 1.4f);
        PulsoDeLuz(pw, BlancoCalido, 34f, 26f, 1.6f);
        FeedbackService.CameraShake(0.8f, 1.2f);
        _intensidadDelColapso = 2.2f;

        if (liam != null)
        {
            var fantasma = AspectoFantasma.Aplicar(liam, new Color(2.2f, 1.9f, 1.3f), 1f, true);
            fantasma?.FundirA(0f, 1.8f);
            var chispas = Chispas(new Vector3(pl.x, 0f, pl.z), Dorado, 140f, 0.6f, 4.5f);
            yield return new WaitForSeconds(2.2f);
            if (chispas != null) chispas.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // Los círculos se apagan: el conjuro ha gastado todo lo que tenía.
        ApagarEfecto(_circuloLiam);
        _circuloLiam = null;
        for (float t = 0f; t < 1.5f && _luzDelConjuro != null; t += Time.deltaTime)
        {
            _luzDelConjuro.intensity = Mathf.Lerp(_luzDelConjuro.intensity, 2f, t / 1.5f);
            yield return null;
        }
    }

    /// Después del conjuro Will se va quedando sin luz: se vuelve translúcido y se deshace en
    /// motas que suben, mientras lo que queda del camino se hunde.
    private IEnumerator Co_WillSeDesvanece()
    {
        var will = ResolverActor("will", _willActor);
        if (will == null) yield break;
        _radioObjetivo = 0f;
        _velocidadDelRadio = 0.5f;
        _alturaWillObjetivo = 1.45f;
        _velocidadAlturaWill = 0.08f;

        var fantasma = AspectoFantasma.Aplicar(will, new Color(1.5f, 1.4f, 1.15f), 1f);
        var motas = Chispas(will.position, Dorado, 90f, 0.45f, 1.6f, will);
        yield return new WaitForSeconds(0.8f);
        fantasma?.FundirA(0f, 5.5f);
        ApagarEfecto(_circuloWill, 4f); _circuloWill = null;
        ApagarEfecto(_circuloGrande, 4f); _circuloGrande = null;
        yield return new WaitForSeconds(2.5f);
        ApagarEfecto(_tornado, 4f); _tornado = null;
        if (_vientoHaciaArriba != null) _vientoHaciaArriba.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        yield return new WaitForSeconds(2.5f);
        ApagarEfecto(_auraWill, 3f); _auraWill = null;
        if (motas != null) motas.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (_luzDelConjuro != null) StartCoroutine(Co_ApagarLuz(_luzDelConjuro, 2f));
    }

    // ── Acto 3: Liam vuelve ──────────────────────────────────────────────────────────────────

    private void OcultarLiam()
    {
        // Ya en la Piedra: Will deja de levitar en el Sendero (no se le ve hasta que vuelve).
        DevolverFisicaDeWill();
        var liam = ResolverActor("liam", _liamActor);
        if (liam == null) return;
        MostrarActorEntero(liam);
        AspectoFantasma.Aplicar(liam, BlancoCalido, 0f);
    }

    private IEnumerator Co_LiamSeMaterializa()
    {
        var liam = ResolverActor("liam", _liamActor);
        if (liam == null) yield break;
        Vector3 pl = new Vector3(liam.position.x, 0f, liam.position.z);

        // Una columna de luz baja del cielo y en ella se va formando Liam.
        Efecto(_prefabPilarDeLuz, pl, Quaternion.identity, 6f, 1.5f);
        PulsoDeLuz(pl + Vector3.up * 1.5f, BlancoCalido, 10f, 14f, 4f);
        var chispas = Chispas(pl, Dorado, 110f, 0.7f, 1.2f);
        yield return new WaitForSeconds(0.6f);

        var fantasma = AspectoFantasma.Aplicar(liam, new Color(1.7f, 1.6f, 1.35f), 0f);
        fantasma?.FundirA(1f, 2.6f);
        yield return new WaitForSeconds(2.7f);
        Efecto(_prefabGolpeSagrado, pl + Vector3.up * 0.5f, Quaternion.identity, 3f, 1.2f);
        AspectoFantasma.Quitar(liam);
        MostrarActorEntero(liam);
        if (chispas != null) chispas.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // ── Acto 4: el Archimago ─────────────────────────────────────────────────────────────────

    private ConstelacionDeLaEstrella _constelacionPiedra;

    /// El Tema Principal entra con el Archimago, desde el segundo justo para que su subida (1:55)
    /// coincida con la carrera de Estela y Liam (etiqueta @carrera del guion).
    /// Lo que dura «Último Incantazione»: suena una sola vez y termina con el fundido del acto.
    private const float LargoDelIncantazione = 38.6f;

    /// Espera en silencio lo justo para que el tema, desde el principio, acabe en @fin_conjuro
    /// (si el acto fuera más corto, empieza ya avanzado). Así no da la vuelta y vuelve a empezar.
    private IEnumerator Co_MusicaDelConjuro()
    {
        float fin = Etiqueta("fin_conjuro");
        if (fin < 0f) { _ctx?.Player?.PlayMusic("ULTIMO_INCANTAZIONE", false, 2f, 0f); yield break; }
        while (TiempoDelGuion() >= 0f && TiempoDelGuion() < fin - LargoDelIncantazione) yield return null;
        float desde = Mathf.Max(0f, LargoDelIncantazione - (fin - TiempoDelGuion()));
        _ctx?.Player?.PlayMusic("ULTIMO_INCANTAZIONE", false, desde > 0.5f ? 1.5f : 2.5f, desde);
    }

    private void MusicaDelArchimago() => TemaACompas("EPILOGUE", "carrera", SubidaDelTema - 1.0f, 2.5f, 0f);

    private IEnumerator Co_EspirituArchimago()
    {
        var arch = ResolverActor("archimago", _archimagoActor);
        if (arch == null) yield break;
        var fantasma = AspectoFantasma.Aplicar(arch, new Color(1.35f, 1.25f, 0.95f), 0f);
        _archimagoFlotando = true;
        _alturaArchimago = 0f;
        _alturaArchimagoObjetivo = 0.45f;
        _velocidadAlturaArchimago = 0.35f;

        Vector3 pa = new Vector3(arch.position.x, 0f, arch.position.z);
        Efecto(_prefabTeletransporte, pa, Quaternion.identity, 4f, 1.1f);
        Efecto(_prefabGolpeSagrado, pa + Vector3.up * 1.0f, Quaternion.identity, 3f, 1.1f);
        PulsoDeLuz(pa + Vector3.up * 1.2f, Dorado, 9f, 12f, 2.4f);
        AudioService.Instance?.PlaySFX("Prologue_ActorAppear", 0.85f);
        var luz = CrearLuz("Luz_Del_Archimago", arch.position + Vector3.up * 1.4f, new Color(1f, 0.9f, 0.65f), 0f, 7f, arch);
        Chispas(arch.position, Dorado, 26f, 0.5f, 0.6f, arch);

        fantasma?.FundirA(0.62f, 2.5f);
        for (float t = 0f; t < 2.5f && luz != null; t += Time.deltaTime)
        {
            luz.intensity = Mathf.Lerp(0f, 3f, t / 2.5f);
            yield return null;
        }
    }

    /// «Ya puedo irme con ellos»: sube, brilla, se deshace en cinco luces que vuelan al cielo y
    /// encienden las puntas de una estrella torcida, la de madera de Will, hecha constelación.
    private IEnumerator Co_ArchimagoSeHaceEstrella()
    {
        var arch = ResolverActor("archimago", _archimagoActor);
        if (arch == null) yield break;
        var estela = ResolverActor("estela", _estelaActor);
        Vector3 testigos = estela != null ? estela.position + Vector3.up * 1.4f : new Vector3(200f, 1.5f, 7.5f);
        Vector3 centro = PuntoDelGuion("p_constelacion", new Vector3(200f, 15f, -12f));
        if (centro.y < 5f) centro.y = 15f;
        _constelacionPiedra = ConstelacionDeLaEstrella.Crear(centro, testigos, 3.4f, transform);
        _instanciasTemporales.Add(_constelacionPiedra.gameObject);

        // Sube y brilla.
        _alturaArchimagoObjetivo = 2.6f;
        _velocidadAlturaArchimago = 0.85f;
        var fantasma = arch.GetComponent<AspectoFantasma>();
        fantasma?.FundirA(0.95f, 1.6f);
        PulsoDeLuz(arch.position + Vector3.up * 2.5f, Dorado, 14f, 16f, 3.2f);
        AudioService.Instance?.PlaySFX("Hechizo_EstrellaFugaz", 0.8f);
        yield return new WaitForSeconds(2.2f);

        // Se deshace en cinco luces.
        Vector3 pecho = arch.position + Vector3.up * 1.1f;
        Efecto(_prefabGolpeSagrado, pecho, Quaternion.identity, 3f, 1.4f);
        fantasma?.FundirA(0f, 0.9f);
        foreach (var l in arch.GetComponentsInChildren<Light>()) StartCoroutine(Co_ApagarLuz(l, 0.8f));
        foreach (var ps in arch.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        for (int i = 0; i < _constelacionPiedra.CuantasPuntas; i++)
            StartCoroutine(Co_LuzQueVuelaAlCielo(pecho, _constelacionPiedra, i, 0.18f * i));
        yield return new WaitForSeconds(0.18f * 4f + 2.6f);
        _archimagoFlotando = false;
        yield return _constelacionPiedra.TrazarLineas(2.2f);
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            _constelacionPiedra.Brillo(1f + Mathf.Sin(t / 1.2f * Mathf.PI) * 1.4f);
            yield return null;
        }
        _constelacionPiedra.Brillo(1f);
    }

    private IEnumerator Co_LuzQueVuelaAlCielo(Vector3 desde, ConstelacionDeLaEstrella constelacion, int punta, float retraso)
    {
        yield return new WaitForSeconds(retraso);
        var orbe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orbe.name = "Luz_Del_Archimago_" + punta;
        Destroy(orbe.GetComponent<Collider>());
        orbe.transform.position = desde;
        orbe.transform.localScale = Vector3.one * 0.16f;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Orbe del Archimago" };
        mat.SetColor("_BaseColor", new Color(4f, 3.4f, 2.2f));
        var mr = orbe.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var estela = orbe.AddComponent<TrailRenderer>();
        estela.time = 0.7f;
        estela.widthMultiplier = 0.12f;
        estela.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        estela.sharedMaterial = MaterialDeLuz(new Color(2.6f, 2.1f, 1.2f, 1f));
        _instanciasTemporales.Add(orbe);

        Vector3 hasta = constelacion.Punta(punta);
        Vector3 lado = Vector3.Cross(hasta - desde, Vector3.up).normalized * ((punta % 2 == 0) ? 1f : -1f);
        Vector3 control = (desde + hasta) * 0.5f + lado * 4f + Vector3.up * 2f;
        const float duracion = 2.4f;
        for (float t = 0f; t < duracion; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duracion);
            Vector3 a = Vector3.Lerp(desde, control, k), b = Vector3.Lerp(control, hasta, k);
            orbe.transform.position = Vector3.Lerp(a, b, k);
            yield return null;
        }
        constelacion.EncenderPunta(punta);
        _instanciasTemporales.Remove(orbe);
        Destroy(estela.sharedMaterial);
        Destroy(mat);
        Destroy(orbe);
    }
}
