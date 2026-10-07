using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests de EditMode de <see cref="DefaultNarrativeSignals"/>: entrega «sticky» de señales
/// (una señal sin oyentes espera al primero que se suscriba y la consume una sola vez) y qué
/// sobrevive a cada tipo de reset. Son las reglas de señales de TDD § 10.
/// </summary>
public class DefaultNarrativeSignalsTests
{
    const string Senal = "TEST_SENAL";
    const string Otra = "TEST_OTRA";

    GameObject _go;
    DefaultNarrativeSignals _senales;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("DefaultNarrativeSignalsTests_GO");
        _senales = _go.AddComponent<DefaultNarrativeSignals>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
    }

    [Test]
    public void SenalSinOyentes_SeEntregaAlPrimeroQueSeSuscribe()
    {
        _senales.RaiseCustom(Senal);
        Assert.IsTrue(_senales.IsPendingDelivery(Senal));

        int recibidas = 0;
        _senales.OnCustom(Senal, () => recibidas++);

        Assert.AreEqual(1, recibidas);
        Assert.IsFalse(_senales.IsPendingDelivery(Senal));
    }

    [Test]
    public void SenalPendiente_SoloLaConsumeUnSuscriptor()
    {
        _senales.RaiseCustom(Senal);

        int primero = 0, segundo = 0;
        _senales.OnCustom(Senal, () => primero++);
        _senales.OnCustom(Senal, () => segundo++);

        Assert.AreEqual(1, primero);
        Assert.AreEqual(0, segundo, "El segundo suscriptor queda esperando la próxima, no recibe la ya consumida.");
        Assert.IsTrue(_senales.HasCustomListener(Senal));
    }

    [Test]
    public void ConOyente_SeEntregaAlMomentoYNoQuedaPendiente()
    {
        int recibidas = 0;
        _senales.OnCustom(Senal, () => recibidas++);

        _senales.RaiseCustom(Senal);
        _senales.RaiseCustom(Senal);

        Assert.AreEqual(2, recibidas);
        Assert.IsFalse(_senales.IsPendingDelivery(Senal));
    }

    [Test]
    public void OffCustom_QuitaAlOyenteYLaSenalVuelveAQuedarPendiente()
    {
        int recibidas = 0;
        System.Action oyente = () => recibidas++;
        _senales.OnCustom(Senal, oyente);
        _senales.OffCustom(Senal, oyente);

        _senales.RaiseCustom(Senal);

        Assert.AreEqual(0, recibidas);
        Assert.IsFalse(_senales.HasCustomListener(Senal));
        Assert.IsTrue(_senales.IsPendingDelivery(Senal));
    }

    [Test]
    public void HasEverRaised_SobreviveAConsumirYADescartar()
    {
        _senales.RaiseCustom(Senal);
        _senales.OnCustom(Senal, () => { });
        Assert.IsTrue(_senales.HasEverRaised(Senal));

        _senales.RaiseCustom(Otra);
        _senales.ClearPendingCustom(Otra);
        Assert.IsFalse(_senales.IsPendingDelivery(Otra));
        Assert.IsTrue(_senales.HasEverRaised(Otra), "Descartar una pendiente no borra que ya pasó.");
    }

    [Test]
    public void RequeueCustom_DevuelveLaSenalSinInvocarAlQueLaConsumio()
    {
        int recibidas = 0;
        _senales.RaiseCustom(Senal);
        _senales.OnCustom(Senal, () => recibidas++);

        _senales.RequeueCustom(Senal);
        Assert.AreEqual(1, recibidas);
        Assert.IsTrue(_senales.IsPendingDelivery(Senal));

        int siguiente = 0;
        _senales.OnCustom(Senal, () => siguiente++);
        Assert.AreEqual(1, siguiente);
    }

    [Test]
    public void ResetSuave_ConservaSenalesPendientesYOlvidaSuscriptores()
    {
        _senales.OnCustom(Otra, () => { });
        _senales.RaiseCustom(Senal);

        _senales.ResetState(preservePending: true);

        Assert.IsFalse(_senales.HasCustomListener(Otra));
        Assert.IsTrue(_senales.IsPendingDelivery(Senal), "Una señal emitida antes de cargar no se pierde al recargar el grafo.");
        Assert.IsTrue(_senales.HasEverRaised(Senal));
    }

    [Test]
    public void ResetCompleto_OlvidaTodo()
    {
        _senales.OnCustom(Otra, () => { });
        _senales.RaiseCustom(Senal);

        _senales.ResetState();

        Assert.IsFalse(_senales.HasCustomListener(Otra));
        Assert.IsFalse(_senales.IsPendingDelivery(Senal));
        Assert.IsFalse(_senales.HasEverRaised(Senal));
    }

    [Test]
    public void OlvidarSenalesDePartida_OlvidaSenalesYConservaSuscriptores()
    {
        _senales.OnCustom(Otra, () => { });
        _senales.RaiseCustom(Senal);

        _senales.OlvidarSenalesDePartida();

        Assert.IsTrue(_senales.HasCustomListener(Otra));
        Assert.IsFalse(_senales.IsPendingDelivery(Senal), "Una partida nueva no debe consumir señales de la anterior (INC-448).");
        Assert.IsFalse(_senales.HasEverRaised(Senal));
    }

    [Test]
    public void BatallaGanadaSinOyentes_SeEntregaAlSuscribirse()
    {
        var arena = new object();
        _senales.RaiseBattleWon(arena);

        int recibidas = 0;
        _senales.OnBattleWon(arena, () => recibidas++);
        _senales.OnBattleWon(new object(), () => recibidas += 10);

        Assert.AreEqual(1, recibidas, "Solo la arena ganada entrega su victoria pendiente.");
    }
}
