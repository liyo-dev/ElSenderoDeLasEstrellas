using System.Collections.Generic;

namespace Game.NPC
{
    /// <summary>
    /// Comentarios en reposo de Liam: solo aporta sus datos por defecto (comentarios secos de
    /// estratega y la reacción cuando Estela se sienta a quejarse de hambre: le pide que se
    /// levante, ella replica y se levanta). El comportamiento es el de
    /// <see cref="ComentariosEnReposo"/>; se mantiene esta clase para que los prefabs que la llevan
    /// sigan funcionando sin tocarlos. Los datos se retocan en el Inspector.
    /// </summary>
    public class LiamIdleCommentary : ComentariosEnReposo
    {
        // Unity crea el componente con este constructor antes de leer lo guardado: lo que el
        // prefab no guarda (los numeritos y las reacciones) se queda con estos valores, igual que
        // un inicializador.
        public LiamIdleCommentary()
        {
            intervaloMinimo = 30f;
            intervaloMaximo = 65f;
            numeritos = new List<NumeritoEnReposo>
            {
                NumeritoEnReposo.Comentario(1f,
                    "IDLE_LIAM_COMENTARIO_01", "IDLE_LIAM_COMENTARIO_02",
                    "IDLE_LIAM_COMENTARIO_03", "IDLE_LIAM_COMENTARIO_04"),
            };
            reacciones = new List<ReaccionEnReposo>
            {
                new ReaccionEnReposo
                {
                    etiquetaDelOtro = EstelaIdleCommentary.SentarseConHambre,
                    probabilidad = 0.6f,
                    esperaEntreReacciones = 90f,
                    clavesPropias = new[]
                    {
                        "IDLE_LIAM_LEVANTA_ESTELA_01", "IDLE_LIAM_LEVANTA_ESTELA_02", "IDLE_LIAM_LEVANTA_ESTELA_03",
                    },
                    clavesDeRespuesta = new[]
                    {
                        "IDLE_ESTELA_REPLICA_LIAM_01", "IDLE_ESTELA_REPLICA_LIAM_02", "IDLE_ESTELA_REPLICA_LIAM_03",
                    },
                    pausaHastaLaRespuesta = 1.6f,
                    pausaHastaCortar = 1f,
                    cortarSuNumerito = true,
                },
            };
        }
    }
}
