using System;
using UnityEngine;

/// Pestaña «Zonas» del LAB: viaje rápido a cada zona (con el teletransporte del juego), vuelta al
/// LAB desde otras escenas de prueba y salida al menú principal.
public sealed class ViajeDelLab : MonoBehaviour, ISeccionDelLab
{
    [Serializable]
    public struct Destino
    {
        public string nombre;
        [Tooltip("Qué se prueba allí (sale debajo del botón).")]
        public string queHay;
        public Transform punto;
    }

    [SerializeField] private Destino[] destinos = Array.Empty<Destino>();

    private static ViajeDelLab s_actual;

    private void OnEnable() => s_actual = this;
    private void OnDisable() { if (s_actual == this) s_actual = null; }

    /// Nombre del destino más cercano a una posición (para el reporte de errores).
    public static string ZonaCercana(Vector3 posicion)
    {
        if (s_actual == null) return "—";
        string mejor = "—";
        float mejorD = float.MaxValue;
        foreach (var d in s_actual.destinos)
        {
            if (d.punto == null) continue;
            float dist = (d.punto.position - posicion).sqrMagnitude;
            if (dist < mejorD) { mejorD = dist; mejor = d.nombre; }
        }
        return mejor;
    }

    public string Titulo => "Zonas";
    public int Orden => 20;

    public void Dibujar()
    {
        if (!VueltaAlLab.EnElLab)
        {
            GUILayout.Label("Estás en otra escena de pruebas.", EstiloDelLab.Etiqueta);
            if (GUILayout.Button("← Volver al LAB (zona de jefes)", EstiloDelLab.BotonActivo)) VueltaAlLab.VolverALaZonaDeJefes();
        }
        else
        {
            GUILayout.Label("Viaje rápido: el panel se cierra y llegas con tu grupo.", EstiloDelLab.Etiqueta);
            foreach (var d in destinos)
            {
                if (d.punto == null) continue;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(d.nombre, EstiloDelLab.Boton, GUILayout.Width(220f))) Ir(d.punto);
                GUILayout.Label(d.queHay, EstiloDelLab.Nota);
                GUILayout.EndHorizontal();
            }
        }

        GUILayout.Space(12f);
        if (GUILayout.Button("Salir al menú principal", EstiloDelLab.Boton, GUILayout.Width(220f))) VueltaAlLab.SalirAlMenu();
        GUILayout.Label("Nada de lo que hagas en el LAB toca tu partida guardada.", EstiloDelLab.Nota);
    }

    private static void Ir(Transform punto)
    {
        PanelDelLab.Cerrar();
        var jugador = PlayerService.Player;
        if (jugador != null) ZonaDeJefesDelLab.Llevar(jugador, punto);
    }
}
