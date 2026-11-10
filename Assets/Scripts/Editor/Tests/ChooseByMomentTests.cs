using System.Collections.Generic;
using NUnit.Framework;

/// Tests de EditMode de ChooseByMomentNode: qué opción elige y qué deja en el mundo proyectado.
public class ChooseByMomentTests
{
    static NarrativeCondition FlagNo(string key) => new() { kind = NarrativeCondition.Kind.FlagIsNotSet, key = key };
    static NarrativeCondition Flag(string key) => new() { kind = NarrativeCondition.Kind.FlagIsSet, key = key };

    [Test]
    public void EligeLaOpcionMasConcretaQueSeCumple()
    {
        // Sin almacén de flags, ningún flag está puesto: «no X» se cumple, «X» no.
        var node = new ChooseByMomentNode
        {
            options = new List<ChooseByMomentNode.Option>
            {
                new() { label = "Siempre" },
                new() { label = "Concreta", conditions = { FlagNo("A"), FlagNo("B") } },
                new() { label = "Imposible", conditions = { Flag("A"), FlagNo("B"), FlagNo("C") } },
            }
        };

        Assert.AreEqual(1, node.Choose(null));
    }

    [Test]
    public void SinOpcionQueSeCumpla_SaleporSiNo()
    {
        var node = new ChooseByMomentNode
        {
            options = new List<ChooseByMomentNode.Option> { new() { label = "X", conditions = { Flag("X") } } }
        };

        Assert.AreEqual(1, node.Choose(null));
        Assert.AreEqual(ChooseByMomentNode.OtherwiseLabel, node.GetOutputPorts()[1]);
    }
}
