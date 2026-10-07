using System.Globalization;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests de EditMode de <see cref="IdDePersistencia"/>: el formato de los ids no puede cambiar
/// porque las partidas guardadas ya los llevan. Se fija la cultura invariante porque el id usa
/// la del sistema para los decimales (ver INC-677).
/// </summary>
public class IdDePersistenciaTests
{
    CultureInfo _culturaPrevia;
    GameObject _go;

    [SetUp]
    public void SetUp()
    {
        _culturaPrevia = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        _go = new GameObject("Caja");
    }

    [TearDown]
    public void TearDown()
    {
        CultureInfo.CurrentCulture = _culturaPrevia;
        if (_go != null) Object.DestroyImmediate(_go);
    }

    [Test]
    public void ClaveDePosicion_RedondeaAUnDecimal()
    {
        Assert.AreEqual("1.5_0.0_-2.3", IdDePersistencia.ClaveDePosicion(new Vector3(1.54f, 0f, -2.26f)));
    }

    [Test]
    public void DeObjeto_TerminaEnNombreYPosicion()
    {
        _go.transform.position = new Vector3(3f, 0f, 12.5f);

        StringAssert.EndsWith("_Caja_3.0_0.0_12.5", IdDePersistencia.DeObjeto(_go));
    }

    [Test]
    public void DeObjeto_ConPosicionDePartida_NoCambiaAunqueElObjetoSeMueva()
    {
        var partida = new Vector3(3f, 0f, 0f);
        _go.transform.position = partida;
        string idInicial = IdDePersistencia.DeObjeto(_go);

        _go.transform.position = new Vector3(10f, 0f, 0f);

        Assert.AreEqual(idInicial, IdDePersistencia.DeObjeto(_go, partida));
        Assert.AreNotEqual(idInicial, IdDePersistencia.DeObjeto(_go));
    }
}
