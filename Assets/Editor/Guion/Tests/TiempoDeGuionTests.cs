#if UNITY_EDITOR
using NUnit.Framework;

/// Las cuentas del reloj del guion: dónde arrancan la música y la voz al entrar por la mitad de una
/// cinemática. Ver INC-1234 y TDD § 34.
public class TiempoDeGuionTests
{
    private const float Tolerancia = 1e-4f;

    [Test]
    public void MusicaSinSegundoConcreto_SigueDondeEstaba()
    {
        // El efecto se pidió en el segundo 10 y se salta al 12: la pista lleva 2 s.
        Assert.AreEqual(2f, TiempoDeGuion.DesplazamientoDeMusica(-1f, 12f, 10f), Tolerancia);
    }

    [Test]
    public void MusicaConSegundoConcreto_SumaEseSegundoYLoTranscurrido()
    {
        // La pista empieza en su segundo 5 y se entra 2 s después del efecto: 7.
        Assert.AreEqual(7f, TiempoDeGuion.DesplazamientoDeMusica(5f, 12f, 10f), Tolerancia);
    }

    [Test]
    public void MusicaAntesDelEfecto_NoResta()
    {
        Assert.AreEqual(0f, TiempoDeGuion.DesplazamientoDeMusica(-1f, 9f, 10f), Tolerancia);
    }

    [Test]
    public void Linea_EntraEnElSegundoQueLlevaDicha()
    {
        Assert.AreEqual(2f, TiempoDeGuion.DesplazamientoDeLinea(3f, 1f), Tolerancia);
    }

    [Test]
    public void LineaAntesDeEmpezar_ArrancaEnCero()
    {
        Assert.AreEqual(0f, TiempoDeGuion.DesplazamientoDeLinea(0.5f, 1f), Tolerancia);
    }

    [Test]
    public void DuracionRestante_DescuentaLoQueYaLleva()
    {
        Assert.AreEqual(3.35f, TiempoDeGuion.DuracionRestanteDeLinea(3f, 1f, 6f), Tolerancia);
    }

    [Test]
    public void DuracionRestante_ConLaLineaYaAcabada_DejaElMargenMinimo()
    {
        Assert.AreEqual(0.4f, TiempoDeGuion.DuracionRestanteDeLinea(7f, 1f, 6f), Tolerancia);
    }
}
#endif
