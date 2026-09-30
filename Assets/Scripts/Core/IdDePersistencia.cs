using UnityEngine;

/// <summary>
/// Id estable de un objeto colocado en una escena, para guardar su estado sin configurar nada a
/// mano: «{escena}_{nombre}_{x}_{y}_{z}», con la posición a un decimal. Ver TDD § 9 («IDs de
/// persistencia de mundo»).
///
/// Cada sistema le pone delante su prefijo (PICKUP_, DOOR_UNLOCKED_…). El formato no se puede
/// cambiar: las partidas guardadas ya llevan estos ids.
/// </summary>
public static class IdDePersistencia
{
    /// <summary>Id del objeto en la posición indicada (para objetos que se mueven, la de partida).</summary>
    public static string DeObjeto(GameObject objeto, Vector3 posicion)
    {
        var escena = objeto.scene.IsValid() ? objeto.scene.name : "Unknown";
        return $"{escena}_{objeto.name}_{ClaveDePosicion(posicion)}";
    }

    /// <summary>Id del objeto en su posición actual.</summary>
    public static string DeObjeto(GameObject objeto) => DeObjeto(objeto, objeto.transform.position);

    /// <summary>La parte de la posición del id: «x_y_z» a un decimal.</summary>
    public static string ClaveDePosicion(Vector3 p) => $"{p.x:F1}_{p.y:F1}_{p.z:F1}";
}
