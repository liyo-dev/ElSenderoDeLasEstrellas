/// Zona de la pantalla que ocupa un aviso. Dentro de un mismo canal se ve uno solo a la vez y los
/// demás esperan su turno; avisos de canales distintos conviven en pantalla.
public enum CanalDeAviso
{
    /// Arriba en el centro: el banner de misión nueva y la barra del jefe (entran uno detrás de otro).
    Cabecera,
    /// Lateral derecho: el popup de habilidad o hechizo desbloqueado.
    Lateral,
    /// La pila de objetos conseguidos.
    Objetos,
    /// El rótulo breve de abajo (mensajes cortos del juego).
    Rotulo,
}

/// Lo implementa todo aviso que aparece en pantalla durante el juego (banner de misión, barra del
/// jefe, popup de desbloqueo, objetos conseguidos, rótulos breves).
///
/// El aviso no conoce a los demás ni sabe si hay un menú, una cinemática o la intro de un jefe:
/// pide turno con <see cref="ArbitroDeAvisos.Pedir"/>, se enseña cuando el árbitro se lo da y
/// avisa con <see cref="ArbitroDeAvisos.Terminar"/> cuando ha acabado. Mientras no tiene el turno,
/// su cuenta atrás no corre, así que nadie se pierde un aviso por estar tapado.
public interface IAvisoEnPantalla
{
    CanalDeAviso Canal { get; }

    /// Dentro de su canal, el de prioridad más alta va primero (a igualdad, el que pidió antes).
    /// Uno más importante que llega aparta al que se está viendo, que espera y vuelve después.
    int Prioridad { get; }

    /// Pasajero: dura unos segundos. Si otro aviso necesita el sitio con prisa
    /// (<see cref="ArbitroDeAvisos.Adelantar"/>), se despide en vez de volver más tarde fuera de contexto.
    bool Pasajero { get; }

    /// Tiene el turno y nada lo tapa: se enseña (o vuelve a enseñarse) y su cuenta atrás corre.
    void AlTenerTurno();

    /// Pierde el turno sin haber terminado (menú, cinemática, intro de un jefe o un aviso más
    /// importante): se esconde y para su cuenta atrás hasta que vuelva a tenerlo.
    void AlPerderTurno();

    /// Lo descarta el árbitro para dejar paso a otro con prisa. Ya no está en la cola: se esconde
    /// y olvida lo que tenía pendiente, sin llamar a Terminar.
    void AlDescartar();
}
