using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sendero.Core.Feedback;

/// Acto 5: Will vuelve por la luz del portal; Estela y Liam echan a correr cuando el Tema
/// Principal empieza a subir (1:55) y los tres se abrazan cuando estalla (2:03). La carrera va en
/// cámara lenta y su velocidad se ajusta sola para llegar justo con la música.
public partial class ModuloCinematicaFinal
{
    /// Segundo del Tema Principal en que empieza la subida.
    private const float SubidaDelTema = 115f;
    /// Segundo del Tema Principal en que estalla.
    private const float ExplosionDelTema = 123f;
    private const float CamaraLentaMinima = 0.18f;

    private float _escalaDeCarrera = 1f;

    private IEnumerator Co_PortalDeRegreso()
    {
        var will = ResolverActor("will", _willActor);
        if (will == null) yield break;
        DevolverFisicaDeWill();
        AspectoFantasma.Quitar(will);
        MostrarActorEntero(will);
        FrenarAWill();
        if (_cintaRojaWill != null) _cintaRojaWill.SetActive(true);

        Vector3 pw = new Vector3(will.position.x, 0f, will.position.z);
        // La luz se abre donde estuvo el portal y Will sale de ella, recortado a contraluz.
        Efecto(_prefabPilarDeLuz, pw, Quaternion.identity, 5f, 1.4f);
        Efecto(_prefabTeletransporte, pw, Quaternion.identity, 4f, 1.2f);
        PulsoDeLuz(pw + Vector3.up * 1.2f, BlancoCalido, 9f, 10f, 2.4f);
        var contraluz = CrearLuz("Contraluz_De_Will", pw + new Vector3(0f, 1.8f, -1.4f), new Color(1f, 0.86f, 0.6f), 16f, 12f);
        AudioService.Instance?.PlaySFX("Prologue_ActorAppear", 0.85f);

        var fantasma = AspectoFantasma.Aplicar(will, new Color(1.8f, 1.7f, 1.4f), 0f);
        fantasma?.FundirA(1f, 1.4f);
        yield return new WaitForSeconds(1.5f);
        AspectoFantasma.Quitar(will);
        MostrarActorEntero(will);
        yield return new WaitForSeconds(2.5f);
        if (contraluz != null) StartCoroutine(Co_ApagarLuz(contraluz, 3f));
    }

    /// Para el reloj del guion (la escena se queda mirando) hasta que el tema llega al punto en
    /// que, siguiendo, la etiqueta @carrera caiga en la subida. Absorbe lo que la música vaya
    /// adelantada; si va retrasada no espera.
    private IEnumerator Co_EsperarSubidaDelTema()
    {
        var audio = AudioService.Instance;
        float tCarrera = Etiqueta("carrera"), ahora = TiempoDelGuion();
        if (audio == null || _ctx == null || tCarrera < 0f || ahora < 0f) yield break;
        float objetivo = SubidaDelTema - (tCarrera - ahora);
        float musica = audio.GetMusicTime();
        if (musica < 0f || musica >= objetivo) yield break;

        _ctx.RetenerReloj();
        _relojRetenidoPorMi = true;
        float limite = Time.unscaledTime + 6f;
        while (Time.unscaledTime < limite)
        {
            musica = audio.GetMusicTime();
            if (musica < 0f || musica >= objetivo) break;
            yield return null;
        }
        _ctx.SoltarReloj();
        _relojRetenidoPorMi = false;
    }

    /// Desde @carrera hasta @llegan el reloj del guion va con el tiempo del juego y el tiempo se
    /// frena lo justo para que lleguen a los brazos de Will cuando el tema estalla.
    private IEnumerator Co_CarreraAlAbrazo()
    {
        var audio = AudioService.Instance;
        float llegan = Etiqueta("llegan");
        if (_ctx == null || llegan < 0f) yield break;
        _ctx.AcompasarRelojAlTiempo();
        _relojAcompasado = true;
        _escalaDeCarrera = 1f;

        while (true)
        {
            float ahora = TiempoDelGuion();
            if (ahora < 0f || ahora >= llegan - 0.01f) break;
            float musica = audio != null ? audio.GetMusicTime() : -1f;
            float realQueQueda = musica >= 0f ? ExplosionDelTema - musica : -1f;
            float guionQueQueda = llegan - ahora;
            float escala = realQueQueda > 0.05f ? Mathf.Clamp(guionQueQueda / realQueQueda, CamaraLentaMinima, 1f) : 1f;
            _escalaDeCarrera = Mathf.Lerp(_escalaDeCarrera, escala, 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
            TimeScaleArbiterService.Request(this, _escalaDeCarrera);
            yield return null;
        }

        AbrazoDeLosTres();

        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            TimeScaleArbiterService.Request(this, Mathf.Lerp(_escalaDeCarrera, 1f, t / 0.4f));
            yield return null;
        }
        TimeScaleArbiterService.Release(this);
        _ctx.SoltarRelojAlCompas();
        _relojAcompasado = false;
    }

    /// Estela y Liam corren llorando de alegría: les salen lágrimas de los ojos que se les quedan
    /// atrás (con la cámara lenta, flotan). Duran hasta que llegan al abrazo.
    private IEnumerator Co_LagrimasAlCorrer()
    {
        float llegan = Etiqueta("llegan"), ahora = TiempoDelGuion();
        float dura = llegan > 0f && ahora >= 0f ? Mathf.Clamp(llegan - ahora, 1f, 8f) : 5f;
        var sistemas = new List<ParticleSystem>();
        foreach (var alias in new[] { "estela", "liam" })
        {
            var actor = ResolverActor(alias, null);
            var anim = actor != null ? AnimadorHumano(actor) : null;
            var cabeza = anim != null ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            if (cabeza == null) continue;
            foreach (float lado in new[] { -1f, 1f })
                sistemas.Add(Lagrimas(actor, cabeza, lado));
        }
        // Guion con el tiempo del juego: «dura» está en segundos de guion; con la cámara lenta,
        // pasan más despacio, igual que la carrera.
        for (float t = 0f; t < dura; t += Time.deltaTime) yield return null;
        foreach (var ps in sistemas)
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private ParticleSystem Lagrimas(Transform actor, Transform cabeza, float lado)
    {
        var go = new GameObject("Lagrimas_" + actor.name);
        go.transform.SetParent(cabeza, false);
        // Los ojos de los chibis: delante y algo por encima del hueso de la cabeza.
        go.transform.position = cabeza.position + actor.up * 0.13f + actor.forward * 0.2f + actor.right * (0.1f * lado);
        _instanciasTemporales.Add(go);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.055f);
        main.startColor = new Color(0.72f, 0.9f, 1f, 0.95f);
        main.gravityModifier = 0.35f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emision = ps.emission;
        emision.rateOverTime = 22f;
        var forma = ps.shape;
        forma.enabled = true;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = 0.015f;
        // Salen hacia atrás y un poco hacia fuera: se quedan detrás mientras corren.
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        Vector3 atras = -actor.forward * 0.9f + actor.right * (0.45f * lado) + Vector3.up * 0.35f;
        vel.x = new ParticleSystem.MinMaxCurve(atras.x * 0.8f, atras.x * 1.2f);
        vel.y = new ParticleSystem.MinMaxCurve(atras.y * 0.8f, atras.y * 1.2f);
        vel.z = new ParticleSystem.MinMaxCurve(atras.z * 0.8f, atras.z * 1.2f);
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f));
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var degradado = new Gradient();
        degradado.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
        color.color = degradado;
        var render = go.GetComponent<ParticleSystemRenderer>();
        var mat = MaterialDeLuz(new Color(0.85f, 1f, 1.4f, 1f));
        mat.mainTexture = TexturaDeChispa();
        render.sharedMaterial = mat;
        render.renderMode = ParticleSystemRenderMode.Billboard;
        ps.Play();
        return ps;
    }

    private void AbrazoDeLosTres()
    {
        var will = ResolverActor("will", _willActor);
        var estela = ResolverActor("estela", _estelaActor);
        var liam = ResolverActor("liam", _liamActor);
        if (will == null) return;
        GestosProcedurales.AbrazarATres(will, estela, liam, 0.25f);

        Vector3 centro = will.position + Vector3.up * 1.1f;
        Efecto(_prefabGolpeSagrado, centro, Quaternion.identity, 3f, 1.6f);
        Efecto(_prefabLluviaDeEstrellas, new Vector3(will.position.x, 0.05f, will.position.z), Quaternion.identity, 6f, 1.3f);
        PulsoDeLuz(centro, BlancoCalido, 12f, 12f, 2.2f);
        FeedbackService.CameraShake(0.18f, 0.35f);
        if (_constelacionPiedra != null) StartCoroutine(Co_ConstelacionLate());
    }

    private IEnumerator Co_ConstelacionLate()
    {
        for (float t = 0f; t < 2.5f && _constelacionPiedra != null; t += Time.deltaTime)
        {
            _constelacionPiedra.Brillo(1f + Mathf.Sin(t / 2.5f * Mathf.PI) * 1.2f);
            yield return null;
        }
    }
}
