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

    class EmiteSenalDePrueba : NodoDePrueba
    {
        public override void Describe(INarrativeNodeDescriber d) => d.Signal("SENAL", SignalRole.Emite);
    }

    class EsperaSenalDePrueba : NodoDePrueba
    {
        public override void Describe(INarrativeNodeDescriber d) => d.Signal("SENAL", SignalRole.Escucha);
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
        public void MarkSignalRaised(string key) { }
        public void AddItem(string itemId, int amount) { }
        public void RemoveItem(string itemId, int amount) { }
        public void EnsureItem(string itemId, int amount) { }
        public void SetFact(string category, string key, string value) { }
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
    public void Decision_SigueLaRamaQueLlevaAlObjetivo()
    {
        Nodo<DecisionDePrueba>("A", "B", "C");
        Nodo<NodoDePrueba>("B");
        Nodo<NodoDePrueba>("C", "D");
        Nodo<NodoDePrueba>("D");

        var r = NarrativeStateProjector.Project(_grafo, "D", _estado);

        Assert.IsTrue(r.ReachedTarget);
        Assert.AreEqual(1, r.Decisions.Count);
        Assert.IsTrue(r.Decisions[0].Forced, "Solo una rama lleva al objetivo: no hay nada que elegir.");
        Assert.AreEqual(1, r.Decisions[0].ChosenPort);
        Assert.IsTrue(_estado.GetFlag("C"));
        Assert.IsFalse(_estado.GetFlag("B"), "La rama que no lleva al objetivo no ha pasado.");
    }

    [Test]
    public void Decision_ConVariasRamasAlObjetivo_UsaLaElegida()
    {
        Nodo<DecisionDePrueba>("A", "B", "C");
        Nodo<NodoDePrueba>("B", "D");
        Nodo<NodoDePrueba>("C", "D");
        Nodo<NodoDePrueba>("D");

        var porDefecto = NarrativeStateProjector.Project(_grafo, "D", _estado);
        Assert.IsTrue(porDefecto.ReachedTarget);
        Assert.IsFalse(porDefecto.Decisions[0].Forced);
        Assert.IsTrue(_estado.GetFlag("B"), "Sin elección se sigue la primera rama que lleva al objetivo.");
        Assert.IsFalse(_estado.GetFlag("C"));

        var otro = new EstadoDePrueba();
        var elegida = NarrativeStateProjector.Project(_grafo, "D", otro, new Dictionary<string, int> { { "A", 1 } });
        Assert.IsTrue(elegida.ReachedTarget);
        Assert.IsTrue(otro.GetFlag("C"));
        Assert.IsFalse(otro.GetFlag("B"));
    }

    [Test]
    public void Orden_EsElDeLaHistoriaNoElDeLaBusqueda()
    {
        // A lanza dos ramas: la corta (B) llega a D y la larga (C → E) también. E ocurre antes que D.
        Nodo<NodoDePrueba>("A", "B", "C");
        Nodo<NodoDePrueba>("B", "D");
        Nodo<NodoDePrueba>("C", "E");
        Nodo<NodoDePrueba>("E", "D");
        Nodo<NodoDePrueba>("D", "F");
        Nodo<NodoDePrueba>("F");

        var r = NarrativeStateProjector.Project(_grafo, "F", _estado);

        Assert.IsTrue(r.ReachedTarget);
        Assert.Less(r.Order.IndexOf("E"), r.Order.IndexOf("D"), "D espera a sus dos entradas: va después de E.");
    }

    [Test]
    public void TodosLosTiposDeNodo_DeclaranLoQueCambian()
    {
        var sinDeclarar = NarrativeEffectContract.FindUndeclared(UnityEditor.TypeCache.GetTypesDerivedFrom<NarrativeNode>());
        Assert.IsEmpty(sinDeclarar,
            "Estos nodos no dicen qué dejan cambiado (INarrativeStateEffect, INarrativeRouteEffect o [NarrativeNoWorldEffect]): "
            + string.Join(", ", sinDeclarar.ConvertAll(t => t.Name)));
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
    public void Bucle_NoMeteLoQueVieneDespuesDelObjetivo()
    {
        // A → B → C → D, y D vuelve a B (un «inténtalo otra vez»).
        Nodo<NodoDePrueba>("A", "B");
        Nodo<NodoDePrueba>("B", "C");
        Nodo<NodoDePrueba>("C", "D");
        Nodo<NodoDePrueba>("D", "B");

        var r = NarrativeStateProjector.Project(_grafo, "C", _estado);

        Assert.IsTrue(r.ReachedTarget);
        Assert.IsTrue(_estado.GetFlag("B"));
        Assert.IsFalse(_estado.GetFlag("D"), "D va después del objetivo aunque vuelva a B.");
    }

    [Test]
    public void RamaParalela_SeParaEnLoQueVieneDespuesDelObjetivo()
    {
        // A abre dos ramas; la segunda acaba uniéndose a lo que hay después del objetivo.
        Nodo<NodoDePrueba>("A", "B", "X");
        Nodo<NodoDePrueba>("B", "T");
        Nodo<NodoDePrueba>("X", "E");
        Nodo<NodoDePrueba>("T", "E");
        Nodo<NodoDePrueba>("E");

        var r = NarrativeStateProjector.Project(_grafo, "T", _estado);

        Assert.IsTrue(r.ReachedTarget);
        Assert.IsTrue(_estado.GetFlag("X"), "La rama paralela sí ha podido pasar.");
        Assert.IsFalse(_estado.GetFlag("E"), "E va después del objetivo.");
    }

    [Test]
    public void EsperarSenal_VaDespuesDeQuienLaEmite()
    {
        // A abre dos ramas: B → T → R (emite la señal) y W (espera la señal) → E.
        Nodo<NodoDePrueba>("A", "B", "W");
        Nodo<NodoDePrueba>("B", "T");
        Nodo<NodoDePrueba>("T", "R");
        Nodo<EmiteSenalDePrueba>("R");
        Nodo<EsperaSenalDePrueba>("W", "E");
        Nodo<NodoDePrueba>("E");

        var r = NarrativeStateProjector.Project(_grafo, "T", _estado);

        Assert.IsTrue(r.ReachedTarget);
        Assert.IsTrue(_estado.GetFlag("B"));
        Assert.IsFalse(_estado.GetFlag("W"), "W espera una señal que se emite después del objetivo.");
        Assert.IsFalse(_estado.GetFlag("E"));
    }

    [Test]
    public void Ciclo_TerminaSinLlegarAUnObjetivoInexistente()
    {
        Nodo<NodoDePrueba>("A", "B");
        Nodo<NodoDePrueba>("B", "A");

        var r = NarrativeStateProjector.Project(_grafo, "NO_EXISTE", _estado);

        Assert.IsFalse(r.ReachedTarget);
        Assert.IsEmpty(r.Order, "Sin camino al objetivo no hay nada que reconstruir.");
    }

    [Test]
    public void PasoSuelto_NoReconstruyeNada()
    {
        Nodo<NodoDePrueba>("A", "B");
        Nodo<NodoDePrueba>("B");
        Nodo<NodoDePrueba>("SUELTO");

        var r = NarrativeStateProjector.Project(_grafo, "SUELTO", _estado);

        Assert.IsFalse(r.ReachedTarget);
        Assert.IsFalse(_estado.GetFlag("A"));
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
