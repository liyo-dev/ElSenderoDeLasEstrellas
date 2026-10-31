using System;

/// Un beat que acepta, desde un guion de texto, opciones que no son campos sueltos (por ejemplo
/// las reacciones línea a línea de un diálogo). El horneado le pasa cada opción del «efecto» antes
/// de buscar un campo con ese nombre.
public interface IConfigurableDesdeGuion
{
    /// Devuelve true si la opción es suya y la ha aplicado. idDeActor traduce un alias del guion
    /// al id del actor.
    bool Configurar(string clave, string valor, Func<string, string> idDeActor);
}
