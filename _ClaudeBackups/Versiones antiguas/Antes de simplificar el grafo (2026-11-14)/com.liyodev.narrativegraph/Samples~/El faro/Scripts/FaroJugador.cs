using UnityEngine;

namespace NarrativeGraphSamples.ElFaro
{
    /// El jugador del ejemplo: al empezar se coloca donde dicen sus datos y los enseña en una
    /// esquina. Así se ve qué ha puesto la pestaña Arranque al probar desde un paso.
    public sealed class FaroJugador : MonoBehaviour
    {
        public FaroJugadorDatos datos;

        void Start()
        {
            if (datos == null) return;
            var punto = GameObject.Find("Aparecer_" + datos.apareceEn);
            if (punto != null) transform.SetPositionAndRotation(punto.transform.position, punto.transform.rotation);
        }

        void OnGUI()
        {
            if (datos == null) return;
            GUI.Label(new Rect(20, 20, 400, 24), $"Jugador · en {datos.apareceEn} · vida {datos.vida} · farol: {(datos.llevaFarol ? "sí" : "no")}");
        }
    }
}
