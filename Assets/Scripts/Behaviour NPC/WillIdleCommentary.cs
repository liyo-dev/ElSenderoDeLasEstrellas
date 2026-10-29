using System.Collections.Generic;

namespace Game.NPC
{
    /// <summary>
    /// Comentarios en reposo de Will cuando lo lleva la IA: solo aporta sus datos por defecto
    /// (comentarios sueltos y pararse a rebuscar entre sus cosas). El comportamiento es el de
    /// <see cref="ComentariosEnReposo"/>; se mantiene esta clase para que los prefabs que la llevan
    /// sigan funcionando sin tocarlos. Los datos se retocan en el Inspector.
    /// </summary>
    public class WillIdleCommentary : ComentariosEnReposo
    {
        // Unity crea el componente con este constructor antes de leer lo guardado: lo que el
        // prefab no guarda (los numeritos) se queda con estos valores, igual que un inicializador.
        public WillIdleCommentary()
        {
            intervaloMinimo = 28f;
            intervaloMaximo = 60f;
            numeritos = new List<NumeritoEnReposo>
            {
                NumeritoEnReposo.Comentario(3f,
                    "IDLE_WILL_COMENTARIO_01", "IDLE_WILL_COMENTARIO_02",
                    "IDLE_WILL_COMENTARIO_03", "IDLE_WILL_COMENTARIO_04"),
                NumeritoEnReposo.Pararse(2f, 3f, "Question01", NPCAmbientActivity.None, null,
                    "IDLE_WILL_REBUSCAR_01", "IDLE_WILL_REBUSCAR_02",
                    "IDLE_WILL_REBUSCAR_03", "IDLE_WILL_REBUSCAR_04"),
            };
        }
    }
}
