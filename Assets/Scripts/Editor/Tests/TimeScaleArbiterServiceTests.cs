using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests de EditMode de <see cref="TimeScaleArbiterService"/>: con varias peticiones a la vez manda
/// la más lenta, y liberar una no pisa a las demás (dos hitstops solapados no dejan el juego en
/// cámara lenta). Ver C6 en TDD § 19.
/// </summary>
public class TimeScaleArbiterServiceTests
{
    const float Tolerancia = 1e-5f;

    readonly object _hitstopA = new object();
    readonly object _hitstopB = new object();
    readonly object _pausa = new object();

    [SetUp]
    public void SetUp() => TimeScaleArbiterService.SetBaseline(1f);

    [TearDown]
    public void TearDown()
    {
        TimeScaleArbiterService.Release(_hitstopA);
        TimeScaleArbiterService.Release(_hitstopB);
        TimeScaleArbiterService.Release(_pausa);
        TimeScaleArbiterService.SetBaseline(1f);
    }

    [Test]
    public void SinPeticiones_UsaElBaseline()
    {
        Assert.AreEqual(1f, Time.timeScale, Tolerancia);
    }

    [Test]
    public void VariasPeticiones_MandaLaMasLenta()
    {
        TimeScaleArbiterService.Request(_hitstopA, 0.1f);
        TimeScaleArbiterService.Request(_pausa, 0f);
        Assert.AreEqual(0f, Time.timeScale, Tolerancia);

        TimeScaleArbiterService.Release(_pausa);
        Assert.AreEqual(0.1f, Time.timeScale, Tolerancia, "Cerrar la pausa no debe cortar el hitstop en curso.");

        TimeScaleArbiterService.Release(_hitstopA);
        Assert.AreEqual(1f, Time.timeScale, Tolerancia);
    }

    [Test]
    public void DosHitstopsSolapados_AlTerminarAmbos_VuelveATiempoNormal()
    {
        TimeScaleArbiterService.Request(_hitstopA, 0.1f);
        TimeScaleArbiterService.Request(_hitstopB, 0.05f);

        // A termina antes que B: el tiempo sigue al ritmo de B, no vuelve a 1 ni se queda en 0.1.
        TimeScaleArbiterService.Release(_hitstopA);
        Assert.AreEqual(0.05f, Time.timeScale, Tolerancia);

        TimeScaleArbiterService.Release(_hitstopB);
        Assert.AreEqual(1f, Time.timeScale, Tolerancia, "Dos hitstops solapados no deben dejar cámara lenta permanente.");
    }

    [Test]
    public void MismoDueno_ActualizaSuPeticionSinAcumular()
    {
        TimeScaleArbiterService.Request(_hitstopA, 0.5f);
        TimeScaleArbiterService.Request(_hitstopA, 0.2f);
        Assert.AreEqual(0.2f, Time.timeScale, Tolerancia);

        TimeScaleArbiterService.Release(_hitstopA);
        Assert.AreEqual(1f, Time.timeScale, Tolerancia, "Un solo Release debe bastar aunque el dueño pidiera dos veces.");
    }

    [Test]
    public void ReleaseDeQuienNoPidio_NoCambiaNada()
    {
        TimeScaleArbiterService.Request(_hitstopA, 0.3f);
        TimeScaleArbiterService.Release(_hitstopB);
        TimeScaleArbiterService.Release(null);

        Assert.AreEqual(0.3f, Time.timeScale, Tolerancia);
    }

    [Test]
    public void RequestSinDueno_SeIgnora()
    {
        TimeScaleArbiterService.Request(null, 0f);

        Assert.AreEqual(1f, Time.timeScale, Tolerancia);
    }

    [Test]
    public void BaselineDePausa_NoLoPisaUnaPeticionMasRapida()
    {
        TimeScaleArbiterService.SetBaseline(0f);
        TimeScaleArbiterService.Request(_hitstopA, 0.5f);
        Assert.AreEqual(0f, Time.timeScale, Tolerancia);

        TimeScaleArbiterService.SetBaseline(1f);
        Assert.AreEqual(0.5f, Time.timeScale, Tolerancia, "Al quitar la pausa sigue la petición activa.");
    }
}
