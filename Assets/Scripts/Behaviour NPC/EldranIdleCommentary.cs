using System.Collections.Generic;

namespace Game.NPC
{
    /// <summary>
    /// Comentarios en reposo de Eldran: solo aporta sus datos por defecto (mentor protector y su
    /// torpeza cómica de creer que ha perdido el báculo). El comportamiento es el de
    /// <see cref="ComentariosEnReposo"/>; se mantiene esta clase para que los prefabs que la llevan
    /// sigan funcionando sin tocarlos. Los datos se retocan en el Inspector.
    /// </summary>
    public class EldranIdleCommentary : ComentariosEnReposo
    {
        // Unity crea el componente con este constructor antes de leer lo guardado: lo que el
        // prefab no guarda (los numeritos) se queda con estos valores, igual que un inicializador.
        public EldranIdleCommentary()
        {
            intervaloMinimo = 32f;
            intervaloMaximo = 70f;
            numeritos = new List<NumeritoEnReposo>
            {
                NumeritoEnReposo.Comentario(3f,
                    "IDLE_ELDRAN_COMENTARIO_01", "IDLE_ELDRAN_COMENTARIO_02",
                    "IDLE_ELDRAN_COMENTARIO_03", "IDLE_ELDRAN_COMENTARIO_04"),
                NumeritoEnReposo.Pararse(2f, 3f, "Question02", NPCAmbientActivity.None, null,
                    "IDLE_ELDRAN_BACULO_01", "IDLE_ELDRAN_BACULO_02",
                    "IDLE_ELDRAN_BACULO_03", "IDLE_ELDRAN_BACULO_04"),
            };
        }
    }
}
