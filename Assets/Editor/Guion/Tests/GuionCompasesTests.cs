#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;

/// Tests de EditMode de COMPASES (cinemática viva, 19 nov 2026): la parte pura, cómo se traduce un
/// bloque de pistas alineadas a las órdenes del HILO de siempre. No hornean.
public class GuionCompasesTests
{
    private const string Base = @"GUION Prueba
ESCENA MainWorld
REPARTO
a NPC_A nombre=A
b NPC_B nombre=B
PUNTOS
p1 0 0
HILO
@inicio
PALETA luz vfxProyectil=X.prefab velocidad=14
COMPASES duelo actores=a,b solape=0.1
1 ataque | lanza MagicLeft hechizo=luz a=b | reacciona Sobresalto01 cara=sorpresa
2 nada   | (auto)                         | gesto Laugh01
3 largo dura=2 | -                        | -
FIN_COMPASES
espera 1
";

    private static GuionTexto Leer() => GuionTexto.Leer(Base);

    [Test]
    public void SinErrores()
    {
        var g = Leer();
        Assert.IsEmpty(g.errores, string.Join("\n", g.errores));
    }

    [Test]
    public void LaPrimeraCeldaSolapaYLasDemasEsperan()
    {
        var g = Leer();
        var a = g.hilo.First(o => o.actor == "a" && o.verbo == "gesto");
        Assert.AreEqual(GuionTexto.Arranque.Solape, a.arranque);
        Assert.AreEqual(0.1f, a.desfase, 1e-4f);
        var b = g.hilo.First(o => o.actor == "b" && o.verbo == "gesto" && o.args.FirstOrDefault() == "Sobresalto01");
        Assert.AreEqual(GuionTexto.Arranque.ALaVezYEspera, b.arranque);
    }

    [Test]
    public void LanzaPoneElHechizoCuandoSueltaLaMano()
    {
        var g = Leer();
        var hechizo = g.hilo.First(o => o.verbo == "efecto" && o.args.FirstOrDefault() == "SpellBeat");
        Assert.AreEqual(GuionTexto.Arranque.TrasInicio, hechizo.arranque);
        Assert.AreEqual(0.35f, hechizo.desfase, 1e-4f);          // MagicLeft suelta a los 0,35 s
        Assert.AreEqual("NPC_A", hechizo.opciones["lanzaId"]);
        Assert.AreEqual("NPC_B", hechizo.opciones["objetivoId"]);
        Assert.AreEqual("X.prefab", hechizo.opciones["vfxProyectil"]);  // de la PALETA
        Assert.IsFalse(hechizo.opciones.ContainsKey("hechizo"));
    }

    [Test]
    public void LaCaraVaAparteYEmpiezaConElCompas()
    {
        var g = Leer();
        var cara = g.hilo.First(o => o.actor == "b" && o.verbo == "cara");
        Assert.AreEqual(GuionTexto.Arranque.ALaVez, cara.arranque);
        var gesto = g.hilo.First(o => o.actor == "b" && o.args.FirstOrDefault() == "Sobresalto01");
        Assert.IsFalse(gesto.opciones.ContainsKey("cara"));
    }

    [Test]
    public void AutoYGuionNoPonenOrdenYDuraAlarga()
    {
        var g = Leer();
        Assert.IsFalse(g.hilo.Any(o => o.actor == "a" && o.texto.Contains("(auto)")));
        var espera = g.hilo.Where(o => o.verbo == "espera").ToList();
        Assert.IsTrue(espera.Any(o => o.args.FirstOrDefault() == "2"), "la fila con dura=2 debe alargar el compás");
    }
}
#endif
