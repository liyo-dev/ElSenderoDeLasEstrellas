using System.Collections.Generic;

namespace Game.NPC
{
    /// <summary>
    /// Comentarios en reposo de Estela: solo aporta sus datos por defecto (comentarios sueltos,
    /// sentarse a quejarse de hambre y plantarse delante del jugador porque se aburre). El
    /// comportamiento es el de <see cref="ComentariosEnReposo"/>; se mantiene esta clase para que
    /// los prefabs que la llevan sigan funcionando sin tocarlos. Los datos se retocan en el Inspector.
    /// </summary>
    public class EstelaIdleCommentary : ComentariosEnReposo
    {
        /// Etiqueta del numerito de sentarse con hambre, al que reacciona Liam.
        public const string SentarseConHambre = "sentarse_con_hambre";

        // Unity crea el componente con este constructor antes de leer lo guardado: lo que el
        // prefab no guarda (los numeritos) se queda con estos valores, igual que un inicializador.
        public EstelaIdleCommentary()
        {
            intervaloMinimo = 25f;
            intervaloMaximo = 55f;
            numeritos = new List<NumeritoEnReposo>
            {
                NumeritoEnReposo.Comentario(3f,
                    "IDLE_ESTELA_COMENTARIO_01", "IDLE_ESTELA_COMENTARIO_02",
                    "IDLE_ESTELA_COMENTARIO_03", "IDLE_ESTELA_COMENTARIO_04"),
                NumeritoEnReposo.Pararse(2f, 4f, null, NPCAmbientActivity.SitGround, SentarseConHambre,
                    "IDLE_ESTELA_HAMBRE_01", "IDLE_ESTELA_HAMBRE_02", "IDLE_ESTELA_HAMBRE_03"),
                NumeritoEnReposo.PlantarseDelante(1.5f, 3.5f, 2.2f, 6f,
                    "IDLE_ESTELA_ABURRIDA_01", "IDLE_ESTELA_ABURRIDA_02", "IDLE_ESTELA_ABURRIDA_03"),
            };
        }
    }
}
