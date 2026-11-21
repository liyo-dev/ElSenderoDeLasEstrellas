using System.Linq;
using Liyodev.NarrativeGraph.Editor;
using NUnit.Framework;
using UnityEngine;
using Liyodev.NarrativeGraph;

/// Tests de EditMode de StoryBatchEdit: los cambios por lotes se aplican todos o ninguno.
public class StoryBatchEditTests
{
    NarrativeGraph _grafo;

    [SetUp]
    public void SetUp()
    {
        _grafo = ScriptableObject.CreateInstance<NarrativeGraph>();
        var inicio = new StartNode { Guid = "S", DisplayTitle = "Inicio" };
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
        Assert.AreEqual("SALUDO", flag.FlagKey);
        Assert.Contains(flag.Guid, _grafo.nodes[0].Outputs);
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

/// Tests de EditMode del contexto del grafo en el asistente compartido: está registrado aunque el
/// editor del grafo esté cerrado y es el que atiende a las herramientas externas (MCP).
public class StoryAssistantContextTests
{
    [Test]
    public void EstaRegistrado_ListaLosGrafosYElManual()
    {
        var contexto = Liyodev.Assistant.AssistantContexts.Find("narrative-graph");
        Assert.IsNotNull(contexto);
        var seleccion = contexto as Liyodev.Assistant.IAssistantContextSelection;
        Assert.IsNotNull(seleccion);
        Assert.IsTrue(seleccion.Targets().Any(t => t.id == "Cap3"));
        StringAssert.Contains("ChooseByMomentNode", contexto.Manual());
    }

    [Test]
    public void ElegirUnGrafoDesconocido_Falla()
    {
        var seleccion = (Liyodev.Assistant.IAssistantContextSelection)Liyodev.Assistant.AssistantContexts.Find("narrative-graph");
        Assert.IsFalse(seleccion.Select("NO_EXISTE"));
    }
}
