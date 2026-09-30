/// Lo implementa todo lo que deja el mundo cambiado de forma duradera: nodos del grafo y, dentro
/// de las secuencias, los beats que mueven actores.
///
/// Project() describe ese cambio sobre un INarrativeStateWriter sin ejecutar nada: no emite
/// señales, no abre diálogos, no toca la escena. Así se puede saber cómo está el mundo en
/// cualquier punto de la historia sin jugarla (arrancar desde un nodo, validar, documentar).
///
/// Un tipo nuevo que cambie estado implementa esta interfaz y ya está: ninguna herramienta tiene
/// que conocerlo por su nombre.
public interface INarrativeStateEffect
{
    void Project(INarrativeStateWriter state);
}
