#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Liyodev.Cinematics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// Tests de EditMode del taller de rodaje: el catálogo no puede ofrecer un efecto que el horneador no
/// sepa crear, la reescritura de una línea no puede tocar el resto del guion, y el diff tiene que
/// detectar un plano movido. Ver INC-1230 y TDD § 33.8.
public class TallerDeRodajeTests
{
    private readonly List<UnityEngine.Object> _temporales = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var objeto in _temporales)
            if (objeto != null) UnityEngine.Object.DestroyImmediate(objeto);
        _temporales.Clear();
    }

    [Test]
    public void TodosLosEfectosDelCatalogoSeInstancian()
    {
        foreach (Type tipo in FabricaDeEfectos.TiposDeCatalogo())
            Assert.DoesNotThrow(() => Activator.CreateInstance(tipo), tipo.Name);
    }

    [Test]
    public void TodosLosEfectosDelCatalogoLosCreaLaFabrica()
    {
        foreach (Type tipo in FabricaDeEfectos.TiposDeCatalogo())
        {
            string error = null;
            var orden = new GuionTexto.Orden { linea = 1, texto = "prueba" };
            SequenceBeat beat = FabricaDeEfectos.Crear(tipo.Name, new Dictionary<string, string>(),
                orden, mensaje => error = mensaje, null);

            Assert.IsNotNull(beat, $"{tipo.Name}: {error}");
        }
    }

    [Test]
    public void LaLineaBaseEmpiezaPorEfectoYSuNombre()
    {
        foreach (Type tipo in FabricaDeEfectos.TiposDeCatalogo())
            StringAssert.StartsWith("efecto " + tipo.Name, FabricaDeEfectos.LineaBase(tipo));
    }

    [Test]
    public void ReescribirCambiaLaClaveYRespetaComentarioYDemasOpciones()
    {
        const string original = "plano primer will lente=35 encuadre=izq # mirar al mago";
        string nuevo = GuionTextoEditable.ReescribirLinea(original,
            new Dictionary<string, string> { ["lente"] = "28.5" });

        StringAssert.Contains("lente=28.5", nuevo);
        StringAssert.Contains("encuadre=izq", nuevo);
        StringAssert.Contains("# mirar al mago", nuevo);
        StringAssert.DoesNotContain("lente=35", nuevo);
    }

    [Test]
    public void ReescribirAnadeLaClaveQueFaltaAntesDelComentario()
    {
        string nuevo = GuionTextoEditable.ReescribirLinea("plano medio will lente=35 # ojo",
            new Dictionary<string, string> { ["mano"] = "0.4" });

        StringAssert.Contains("mano=0.4", nuevo);
        StringAssert.Contains("lente=35", nuevo);
        StringAssert.Contains("# ojo", nuevo);
        Assert.Less(nuevo.IndexOf("mano=0.4", StringComparison.Ordinal),
            nuevo.IndexOf("# ojo", StringComparison.Ordinal));
    }

    [Test]
    public void UnColorConAlmohadillaNoEsUnComentario()
    {
        string nuevo = GuionTextoEditable.ReescribirLinea("vfx Fuego color=#FFAA00 duracion=2",
            new Dictionary<string, string> { ["duracion"] = "3" });

        StringAssert.Contains("color=#FFAA00", nuevo);
        StringAssert.Contains("duracion=3", nuevo);
    }

    [Test]
    public void ReescribirEnTextoCambiaSoloLaLineaPedida()
    {
        const string original = "GUION Prueba\nHILO\nplano primer will lente=35\nvfx Fuego\n";

        Assert.IsTrue(GuionTextoEditable.ReescribirEnTexto(original, 3,
            new Dictionary<string, string> { ["lente"] = "28" }, out string nuevo));

        StringAssert.Contains("lente=28", nuevo);
        StringAssert.Contains("GUION Prueba", nuevo);
        StringAssert.Contains("vfx Fuego", nuevo);
        StringAssert.DoesNotContain("lente=35", nuevo);
    }

    [Test]
    public void ElDiffDetectaUnPlanoConOtraLente()
    {
        GuionHorneado antes = CrearGuion(35f, Vector3.zero);
        GuionHorneado despues = CrearGuion(28f, Vector3.zero);

        List<string> cambios = GuionDiff.Comparar(antes, despues);

        Assert.IsTrue(cambios.Exists(c => c.Contains("lente")), string.Join("\n", cambios));
    }

    [Test]
    public void ElDiffDetectaUnaCamaraMovida()
    {
        GuionHorneado antes = CrearGuion(35f, Vector3.zero);
        GuionHorneado despues = CrearGuion(35f, new Vector3(1.5f, 0f, 0f));

        List<string> cambios = GuionDiff.Comparar(antes, despues);

        Assert.IsTrue(cambios.Exists(c => c.Contains("cámara")), string.Join("\n", cambios));
    }

    [Test]
    public void LaHuellaDeUnaNotaNoDependeDelIdDeLaPagina()
    {
        var nota = new CinemaNota
        {
            id = "1",
            panel = "P02",
            elemento = "general",
            texto = "los contrabandistas deben llegar con el carro",
        };

        string clave = nota.Clave();
        nota.id = "9";

        Assert.AreEqual(clave, nota.Clave(), "El id de la página se repite entre sesiones: no puede entrar en la huella.");
        Assert.AreEqual(CinemaNota.CalcularClave("P02", "general", nota.texto), clave);
        StringAssert.DoesNotContain(" ", clave);
    }

    [Test]
    public void UnDibujoSinTextoSePuedeEncargar()
    {
        var trazo = new CinemaNota
        {
            panel = "P06",
            elemento = "camara",
            tipo = "trazo",
            puntos = new List<CinemaPunto>
            {
                new() { x = 0.1f, y = 0.2f },
                new() { x = 0.4f, y = 0.5f },
            },
        };

        StringAssert.Contains("trazo de 2 puntos", trazo.TextoEfectivo());
        Assert.IsNotEmpty(trazo.Clave());
    }

    private GuionHorneado CrearGuion(float lente, Vector3 posicionCamara)
    {
        var guion = ScriptableObject.CreateInstance<GuionHorneado>();
        _temporales.Add(guion);
        guion.nombre = "prueba";
        guion.duracion = 1f;
        guion.planos = new List<PlanoHorneado>
        {
            new()
            {
                t0 = 0f,
                t1 = 1f,
                lente = lente,
                focoFijo = Vector3.forward * 4f,
                posiciones = new List<ClaveDePosicion> { new(0f, posicionCamara) }
            }
        };
        return guion;
    }
}
#endif
