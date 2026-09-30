/// Un enemigo que espera a que alguien le dé la salida para pelear: la arena de un jefe al
/// terminar su presentación, o el laboratorio de combate al abrir su estación. Lo implementa su IA;
/// quien da la salida no necesita saber qué IA es. Ver INC-507.
public interface IInicioDeCombate
{
    /// Da la salida al combate. Llamarlo más de una vez no hace nada nuevo.
    void EmpezarCombate();
}
