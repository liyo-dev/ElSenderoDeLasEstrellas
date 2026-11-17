#if UNITY_EDITOR
using NUnit.Framework;

/// Tests de EditMode del lote de operaciones del asistente sobre el guion. No hornean ni escriben:
/// comprueban la parte pura, que es donde se puede desplazar una línea o romper una opción.
public class GuionBatchEditTests
{
    private const string Base = @"GUION Prueba
ESCENA MainWorld
SECUENCIA Assets/_SEQUENCES/SEQ_Prueba.asset
TEXTO subtitulo
REPARTO
will Player nombre=Will
PUNTOS
inicio 0 0
HILO
@inicio
will en inicio
plano medio will lente=30 # encuadre
will dice SALUDO
espera 1
";

    [Test]
    public void SetOptionsConservaComentarioYOtrasOpciones()
    {
        string json = "{\"ops\":[{\"op\":\"set_options\",\"line\":12,\"match\":\"plano medio\"," +
                      "\"options\":[{\"name\":\"lente\",\"value\":\"24\"}]}]}";

        Assert.IsTrue(GuionBatchEdit.AplicarTexto(Base, json, out string nuevo, out var resultado),
            string.Join("\n", resultado.Errors));
        StringAssert.Contains("plano medio will lente=24 # encuadre", nuevo);
    }

    [Test]
    public void InsertarEnPuntosNoDesplazaLaLineaDelPlano()
    {
        string json = "{\"ops\":[" +
                      "{\"op\":\"insert_after\",\"anchor\":\"PUNTOS\",\"text\":\"fuego 10 10\"}," +
                      "{\"op\":\"set_options\",\"line\":12,\"match\":\"plano medio\"," +
                      "\"options\":[{\"name\":\"lente\",\"value\":\"20\"}]}" +
                      "]}";

        Assert.IsTrue(GuionBatchEdit.AplicarTexto(Base, json, out string nuevo, out var resultado),
            string.Join("\n", resultado.Errors));
        StringAssert.Contains("fuego 10 10", nuevo);
        StringAssert.Contains("plano medio will lente=20", nuevo);
        Assert.Less(nuevo.IndexOf("fuego 10 10"), nuevo.IndexOf("plano medio"), "La línea nueva debe quedar en PUNTOS.");
    }

    [Test]
    public void ReplaceLineConservaElComentarioFinal()
    {
        string json = "{\"ops\":[{\"op\":\"replace_line\",\"line\":12,\"match\":\"plano medio\"," +
                      "\"text\":\"plano medio will lente=24\"}]}";

        Assert.IsTrue(GuionBatchEdit.AplicarTexto(Base, json, out string nuevo, out var resultado),
            string.Join("\n", resultado.Errors));
        StringAssert.Contains("plano medio will lente=24 # encuadre", nuevo);
    }

    [Test]
    public void SetOptionsConValorVacioQuitaLaOpcion()
    {
        string json = "{\"ops\":[{\"op\":\"set_options\",\"line\":12,\"match\":\"plano medio\"," +
                      "\"options\":[{\"name\":\"lente\",\"value\":\"\"}]}]}";

        Assert.IsTrue(GuionBatchEdit.AplicarTexto(Base, json, out string nuevo, out var resultado),
            string.Join("\n", resultado.Errors));
        StringAssert.DoesNotContain("lente=30", nuevo);
        StringAssert.Contains("plano medio will # encuadre", nuevo);
    }

    [Test]
    public void DeleteLineQuitaSoloEsaLinea()
    {
        string json = "{\"ops\":[{\"op\":\"delete_line\",\"line\":14,\"match\":\"espera 1\"}]}";

        Assert.IsTrue(GuionBatchEdit.AplicarTexto(Base, json, out string nuevo, out var resultado),
            string.Join("\n", resultado.Errors));
        StringAssert.DoesNotContain("espera 1", nuevo);
        StringAssert.Contains("will dice SALUDO", nuevo);
    }

    [Test]
    public void UnaLineaQueNoExisteNoAplicaNada()
    {
        string json = "{\"ops\":[{\"op\":\"replace_line\",\"line\":99,\"text\":\"plano medio will\"}]}";

        Assert.IsFalse(GuionBatchEdit.AplicarTexto(Base, json, out string nuevo, out var resultado));
        Assert.AreEqual(Base, nuevo);
        StringAssert.Contains("no existe", string.Join("\n", resultado.Errors));
    }

    [Test]
    public void UnMatchQueNoCoincideNoAplicaNada()
    {
        string json = "{\"ops\":[{\"op\":\"set_options\",\"line\":12,\"match\":\"primer plano\"," +
                      "\"options\":[{\"name\":\"lente\",\"value\":\"24\"}]}]}";

        Assert.IsFalse(GuionBatchEdit.AplicarTexto(Base, json, out string nuevo, out var resultado));
        Assert.AreEqual(Base, nuevo);
        StringAssert.Contains("no contiene", string.Join("\n", resultado.Errors));
    }

    [Test]
    public void UnEfectoInexistenteNoAplicaNada()
    {
        string json = "{\"ops\":[{\"op\":\"insert_after\",\"anchor\":\"HILO\",\"text\":\"efecto NoExiste\"}]}";

        Assert.IsFalse(GuionBatchEdit.AplicarTexto(Base, json, out string nuevo, out var resultado));
        Assert.AreEqual(Base, nuevo);
        StringAssert.Contains("NoExiste", string.Join("\n", resultado.Errors));
    }
}
#endif
