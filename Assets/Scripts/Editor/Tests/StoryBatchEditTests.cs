using NarrativeGraphEditor;
using NUnit.Framework;
using UnityEngine;

/// Tests de EditMode de StoryBatchEdit: los cambios por lotes se aplican todos o ninguno.
public class StoryBatchEditTests
{
    NarrativeGraph _grafo;

    [SetUp]
    public void SetUp()
    {
        _grafo = ScriptableObject.CreateInstance<NarrativeGraph>();
        var inicio = new StartNode { guid = "S", displayTitle = "Inicio" };
        _grafo.nodes.Add(inicio);
        _grafo.startNodeGuid = "S";
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_grafo);

    [Test]
    public void AnadeConectaYRellenaCampos()
    {
        var r = StoryBatchEdit.Apply(_grafo, @"{ ""ops"": [
            { ""op"": ""add"", ""id"": ""f"", ""type"": ""SetFlagNode"", ""title"": ""Saluda"", ""after"": ""S"",
              ""fields"": [ { ""name"": ""flagKey"", ""value"": ""SALUDO"" } ] },
            { ""op"": ""add"", ""id"": ""b"", ""type"": ""BranchFlagNode"", ""after"": ""f"",
              ""fields"": [ { ""name"": ""flagKey"", ""value"": ""SALUDO"" } ] } ] }");

        Assert.IsTrue(r.Applied, r.ToString());
        Assert.AreEqual(3, _grafo.nodes.Count);
        var flag = (SetFlagNode)_grafo.nodes[1];
        Assert.AreEqual("SALUDO", flag.flagKey);
        Assert.Contains(flag.guid, _grafo.nodes[0].outputs);
    }

    [Test]
    public void SiUnaOperacionEstaMal_NoSeAplicaNada()
    {
        var r = StoryBatchEdit.Apply(_grafo, @"{ ""ops"": [
            { ""op"": ""add"", ""type"": ""SetFlagNode"", ""after"": ""S"" },
            { ""op"": ""set"", ""step"": ""NO_EXISTE"", ""fields"": [] } ] }");

        Assert.IsFalse(r.Applied);
        Assert.AreEqual(1, _grafo.nodes.Count);
        Assert.IsNotEmpty(r.Errors);
    }

    [Test]
    public void TextoDelCapitulo_NombraLosPasosYSusSalidas()
    {
        StoryBatchEdit.Apply(_grafo, @"{ ""ops"": [ { ""op"": ""add"", ""type"": ""SetFlagNode"", ""title"": ""Saluda"", ""after"": ""S"" } ] }");

        var texto = NarrativeGraphText.Chapter(_grafo);

        StringAssert.Contains("Saluda", texto);
        StringAssert.Contains("SetFlagNode", texto);
        StringAssert.Contains("next → 1.2", texto);
    }
}

/// Tests de EditMode del puente local: lo que contesta a cada ruta.
public class StoryBridgeTests
{
    [Test]
    public void ListaLosGrafosYElManual()
    {
        var grafos = StoryBridge.Handle("GET", "/graphs", null, null, out var s1);
        var manual = StoryBridge.Handle("GET", "/manual", null, null, out var s2);

        Assert.AreEqual(200, s1);
        StringAssert.Contains("Cap3", grafos);
        Assert.AreEqual(200, s2);
        StringAssert.Contains("ChooseByMomentNode", manual);
    }

    [Test]
    public void EditarSinGET_YGrafoDesconocido_Fallan()
    {
        StoryBridge.Handle("GET", "/edit", "Cap3", "{}", out var s1);
        StoryBridge.Handle("GET", "/chapter", "NO_EXISTE", null, out var s2);

        Assert.AreEqual(405, s1);
        Assert.AreEqual(404, s2);
    }
}
