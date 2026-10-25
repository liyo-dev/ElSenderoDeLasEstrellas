using UnityEngine;

/// Pestaña «Puzles» del LAB: qué hechizo usar en cada pieza del kit de puzles, cómo se juegan las
/// Pruebas, el registro de tesoros del LAB y cómo reiniciarlo todo. Solo existe en el LAB (la
/// añade su arranque).
public sealed class PuzlesDelLab : MonoBehaviour, ISeccionDelLab
{
    /// Prefijo de los ids de las piezas del LAB que se guardan (EstadoPersistente).
    public const string PrefijoDeIds = "LAB_";

    private System.Collections.Generic.List<RegistroDeTesoros.Region> _regiones;
    private float _siguienteRecuento;

    public string Titulo => "Puzles";
    public int Orden => 56;

    public void Dibujar()
    {
        GUILayout.Label("Kit de puzles: más al este, por el hueco del muro de la zona de Puzles (o pestaña Zonas).", EstiloDelLab.Etiqueta);
        GUILayout.Label("R3/F fija el cristal que estás mirando; RB cambia al siguiente (enemigo o pieza). Mantén R3/F para activar/desactivar el autoapuntado.", EstiloDelLab.Nota);
        Pieza("Cristal de luz", "Bola Prisma, Estrella Fugaz o Lluvia de Chispas (luz). Se encienden 6 s las plataformas: cruza antes de que se apaguen.");
        Pieza("Molinete", "Ráfaga, Huracán, Ciclón o la zona Remolino (viento). Abre la puerta de la sala. Tormenta de Fuego y Muro de Fuego son de fuego: sirven para el brasero, no para el molinete.");
        Pieza("Brasero", "Bola de Fuego, Chispa Ígnea o Llama Astral (fuego). Sube el ascensor y se queda guardado: pulsa R y sigue arriba.");
        Pieza("Cristal de la lanzadera", "Solo con su propia bola devuelta con la B. Los hechizos normales no lo encienden.");
        Pieza("Zona sin vuelo", "Entra volando: el vuelo se corta. Dentro hay que subir saltando.");
        Pieza("Barrera de vuelo", "Pide tener el vuelo. Quítalo en Magia ▸ Habilidades para ver el aviso y la barrera cerrada.");
        GUILayout.Label("Las zonas de magia (Remolino, Nova de Luz, Muro de Fuego…) también encienden las piezas de su elemento.", EstiloDelLab.Nota);

        GUILayout.Space(8f);
        GUILayout.Label("Pruebas (pestaña Zonas). Zona sin vuelo; al acabar se enciende su estrella sobre la entrada y se guarda.", EstiloDelLab.Etiqueta);
        Pieza("Luz y saltos", "Luz al cristal 1 y cruza; devuelve con B la bola de la lanzadera para que aparezcan los escalones; enciende el cristal 3 y, desde la losa del medio, el 4 antes de que se apague el 3.");
        Pieza("Viento y levitación", "Viento al molinete: abre el almacén. Levita un bloque al pie de la repisa (Levitación, mantener X), súbete y haz doble salto. Arriba, viento al otro molinete: puente de 8 s.");
        Pieza("Liam y cambio de personaje", "Con Liam y otro personaje (pestaña Grupo). Liam cruza la reja con Paso Sombrío y pisa la placa: se abre el portón. Deja a un compañero en la placa de espera (cruceta ↓, modo libre) y entra con el otro a la cámara del cofre.");

        GUILayout.Space(8f);
        GUILayout.Label("Tesoros (pestaña Zonas): uno por capa de requisito. Quita o pon habilidades en Magia ▸ Habilidades para ver el aviso de lo que falta; la llave sale de la placa de al lado; el tesoro 1 da el mapa que marca el punto de tesoro.", EstiloDelLab.Etiqueta);
        // El recuento se rehace como mucho una vez por segundo (OnGUI se llama cada fotograma).
        if (_regiones == null || Time.unscaledTime >= _siguienteRecuento)
        {
            _regiones = RegistroDeTesoros.Regiones();
            _siguienteRecuento = Time.unscaledTime + 1f;
        }
        foreach (var region in _regiones)
            GUILayout.Label($"Registro · {region.Nombre}: {region.Encontrados} / {region.Total}", EstiloDelLab.Nota);

        GUILayout.Space(8f);
        if (GUILayout.Button("Reiniciar los puzles y tesoros del LAB", EstiloDelLab.Boton, GUILayout.Width(300f)))
        {
            int borrados = FlagDePartida.BorrarConPrefijo(EstadoPersistente.Prefijo + PrefijoDeIds)
                         + FlagDePartida.BorrarConPrefijo(Tesoro.Prefijo + PrefijoDeIds);
            PanelDelLab.Aviso(borrados > 0 ? "Puzles y tesoros reiniciados" : "No había nada guardado");
            CombatLabDirector.ReloadScene();
        }
        GUILayout.Label("Borra lo guardado de las piezas, pruebas y tesoros del LAB y lo recarga. R recarga sin borrar (para comprobar que se guarda). Los objetos ganados se quedan en la bolsa.", EstiloDelLab.Nota);
    }

    private static void Pieza(string nombre, string comoSeUsa)
    {
        GUILayout.Label(nombre, EstiloDelLab.Titulo);
        GUILayout.Label(comoSeUsa, EstiloDelLab.Etiqueta);
    }
}
