using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests de EditMode de <see cref="NarrativeStateProjector"/>: qué nodos se proyectan al calcular
/// cómo está el mundo al llegar a un nodo (TDD § 10). Usan nodos de prueba que marcan su guid
/// como flag al proyectarse, así que «flag puesto» = «el nodo se proyectó».
/// </summary>
public class NarrativeStateProjectorTests
{
    class NodoDePrueba : NarrativeNode, INarrativeStateEffect
    {
        public override void Enter(NarrativeContext ctx, System.Action onReadyToAdvance) => onReadyToAdvance?.Invoke();
        public void Project(INarrativeStateWriter state) => state.SetFlag(guid, true);
    }

    /// Bifurcación real: puertos con nombre, la proyección no adivina la rama.
    class DecisionDePrueba : NodoDePrueba
    {
        public override string[] GetOutputPorts() => new[] { "Si", "No" };
    }

    /// Nodo con salida de error: la proyección sigue solo por el camino normal (puerto 0).
    class ConSalidaDeErrorDePrueba : NodoDePrueba
    {
        public override string[] GetOutputPorts() => new[] { "Normal", "Error" };
        public override int ProjectionPort => 0;
    }

    class EstadoDePrueba : INarrativeStateWriter
    {
        public readonly HashSet<string> Flags = new HashSet<string>();

        public bool GetFlag(string key) => Flags.Contains(key);
        public void SetFlag(string key, bool value) { if (value) Flags.Add(key); else Flags.Remove(key); }
        public void StartQuest(string questId) { }
        public void CompleteQuestSteps(string questId, IReadOnlyList<string> stepConditionIds, IReadOnlyList<int> stepIndices) { }
        public void CompleteQuest(string questId) { }
        public void AddItem(string itemId, int amount) { }
        public void PlaceActor(string actorId, NarrativeLocation location) { }
        public void SetActorActive(string actorId, bool active) { }
        public void Note(string message) { }
        public bool TryGetExtension<T>(out T extension) where T : class { extension = null; return false; }
    }

    NarrativeGraph _grafo;
    EstadoDePrueba _estado;

    [SetUp]
    public void SetUp()
    {
        _grafo = ScriptableObject.CreateInstance<NarrativeGraph>();
        _estado = new EstadoDePrueba();
    }

    [TearDown]
    public void TearDown()
    {
        if (_grafo != null) Object.DestroyImmediate(_grafo);
    }

    void Nodo<T>(string guid, params string[] salidas) where T : NarrativeNode, new()
    {
        var nodo = new T { guid = guid, outputs = new List<string>(salidas) };
        _grafo.nodes.Add(nodo);
        if (string.IsNullOrEmpty(_grafo.startNodeGuid)) _grafo.startNodeGuid = guid;
    }

    [Test]
    public void CadenaLineal_ProyectaLoAnteriorPeroNoElNodoObjetivo()
    {
        Nodo<NodoDePrueba>("A", "B");
        Nodo<NodoDePrueba>("B", "C");
        Nodo<NodoDePrueba>("C");

        var r = NarrativeStateProjector.Project(_grafo, "C", _estado);

        Assert.IsTrue(r.ReachedTarget);
        Assert.IsTrue(_estado.GetFlag("A"));
        Assert.IsTrue(_estado.GetFlag("B"));
        Assert.IsFalse(_estado.GetFlag("C"), "El nodo objetivo se va a ejecutar de verdad: no se proyecta.");
    }

    [Test]
    public void Fork_ProyectaTodasLasRamas()
    {
        Nodo<NodoDePrueba>("A", "B", "C");
        Nodo<NodoDePrueba>("B", "D");
        Nodo<NodoDePrueba>("C");
        Nodo<NodoDePrueba>("D");

        var r = NarrativeStateProjector.Project(_grafo, "D", _estado);

        Assert.IsTrue(r.ReachedTarget);
        Assert.IsTrue(_estado.GetFlag("B"));
        Assert.IsTrue(_estado.GetFlag("C"), "Una rama paralela del fork también deja el mundo cambiado.");
        Assert.IsEmpty(r.Decisions);
    }

    [Test]
    public void Decision_SeDetieneYLaRegistra()
    {
        Nodo<DecisionDePrueba>("A", "B", "C");
        Nodo<NodoDePrueba>("B");
        Nodo<NodoDePrueba>("C");

        var r = NarrativeStateProjector.Project(_grafo, "B", _estado);

        Assert.IsFalse(r.ReachedTarget, "La proyección no debe adivinar por qué rama sigue una decisión.");
        Assert.AreEqual(1, r.Decisions.Count);
        Assert.IsTrue(_estado.GetFlag("A"), "El efecto del propio nodo de decisión sí se proyecta.");
        Assert.IsFalse(_estado.GetFlag("B"));
        Assert.IsFalse(_estado.GetFlag("C"));
    }

    [Test]
    public void NodoConSalidaDeError_SigueSoloPorElCaminoNormal()
    {
        Nodo<ConSalidaDeErrorDePrueba>("A", "B", "C");
        Nodo<NodoDePrueba>("B", "D");
        Nodo<NodoDePrueba>("C");
        Nodo<NodoDePrueba>("D");

        var r = NarrativeStateProjector.Project(_grafo, "D", _estado);

        Assert.IsTrue(r.ReachedTarget);
        Assert.IsTrue(_estado.GetFlag("B"));
        Assert.IsFalse(_estado.GetFlag("C"), "La salida de error no se proyecta.");
        Assert.IsEmpty(r.Decisions);
    }

    [Test]
    public void Ciclo_TerminaSinLlegarAUnObjetivoInexistente()
    {
        Nodo<NodoDePrueba>("A", "B");
        Nodo<NodoDePrueba>("B", "A");

        var r = NarrativeStateProjector.Project(_grafo, "NO_EXISTE", _estado);

        Assert.IsFalse(r.ReachedTarget);
        Assert.IsTrue(_estado.GetFlag("A"));
        Assert.IsTrue(_estado.GetFlag("B"));
    }

    [Test]
    public void EntradasVacias_DevuelvenResultadoSinObjetivo()
    {
        Nodo<NodoDePrueba>("A");

        Assert.IsFalse(NarrativeStateProjector.Project(null, "A", _estado).ReachedTarget);
        Assert.IsFalse(NarrativeStateProjector.Project(_grafo, null, _estado).ReachedTarget);
        Assert.IsFalse(NarrativeStateProjector.Project(_grafo, "A", null).ReachedTarget);
    }
}
