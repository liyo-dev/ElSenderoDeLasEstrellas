using System.Collections.Generic;
using UnityEngine;

/// El árbitro único de los avisos en pantalla (ver <see cref="IAvisoEnPantalla"/>).
///
/// Decide qué aviso se ve en cada canal y en qué orden, y los calla todos a la vez mientras hay un
/// menú abierto o algo que tapa la pantalla (una cinemática, la intro de un jefe, un texto
/// dramático). Es el único que escucha a <see cref="MenuManager"/> y el único que se apunta al
/// registro <see cref="OcultablesDeCinematica"/> en nombre de los avisos: ningún aviso conoce a
/// los demás ni a esos sistemas.
///
/// Reglas:
/// - En cada canal se ve un aviso: el de prioridad más alta y, a igualdad, el que pidió antes.
///   Si llega uno más importante, el que se veía pierde el turno y vuelve cuando quede libre.
/// - Mientras hay silencio (menú, cinemática…), nadie tiene turno. Los avisos esperan con la
///   cuenta atrás parada y salen al levantarse el silencio.
/// - Un canal cerrado (<see cref="CerrarCanal"/>) no enseña lo que le llegue mientras siga
///   cerrado, ni después: es para reglas del juego del tipo «lo ganado en batalla no saca aviso,
///   lo cuenta el informe de victoria». Lo comprueba quien recibe lo que hay que avisar.
public static class ArbitroDeAvisos
{
    private sealed class Entrada
    {
        public IAvisoEnPantalla Aviso;
        public CanalDeAviso Canal;
        public long Llegada;
        public bool Visible;
    }

    /// Lo que se apunta al registro de cinemáticas: traduce «escóndete» en silencio con la misma clave.
    private sealed class SilencioDeCinematica : IOcultableDuranteCinematica
    {
        public void Ocultar(object quien) => Silenciar(quien);
        public void Mostrar(object quien) => Reanudar(quien);
    }

    private static readonly List<Entrada> s_entradas = new();
    private static readonly List<Entrada> s_copia = new();
    private static readonly List<Entrada> s_descartes = new();
    private static readonly Entrada[] s_mejorPorCanal = new Entrada[System.Enum.GetValues(typeof(CanalDeAviso)).Length];
    private static readonly HashSet<object> s_silencios = new();
    private static readonly List<object> s_silenciosMuertos = new();
    private static readonly Dictionary<CanalDeAviso, HashSet<object>> s_cierres = new();
    private static readonly SilencioDeCinematica s_silencioDeCinematica = new();
    private static readonly object PorMenu = new();

    private static long s_llegadas;
    private static bool s_actualizando;
    private static bool s_otraVez;

    /// True mientras algo tiene callados todos los avisos (un menú, una cinemática…).
    public static bool Silenciado => s_silencios.Count > 0;

    // ── Ciclo de vida ────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_entradas.Clear();
        s_copia.Clear();
        s_descartes.Clear();
        System.Array.Clear(s_mejorPorCanal, 0, s_mejorPorCanal.Length);
        s_silencios.Clear();
        s_cierres.Clear();
        s_llegadas = 0;
        s_actualizando = false;
        s_otraVez = false;
    }
#endif

    // Después de SubsystemRegistration: MenuManager y OcultablesDeCinematica ya han limpiado lo suyo.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Conectar()
    {
        MenuManager.MenuOpened -= AlAbrirMenu;
        MenuManager.MenuOpened += AlAbrirMenu;
        MenuManager.MenuClosed -= AlCerrarMenu;
        MenuManager.MenuClosed += AlCerrarMenu;
        PlayerPresetService.OnPresetApplying -= AlCargarPartida;
        PlayerPresetService.OnPresetApplying += AlCargarPartida;
        OcultablesDeCinematica.Registrar(s_silencioDeCinematica);
        if (MenuManager.AnyOpen()) s_silencios.Add(PorMenu);
    }

    private static void AlAbrirMenu(MenuKind _) => Silenciar(PorMenu);

    private static void AlCerrarMenu(MenuKind _)
    {
        if (!MenuManager.AnyOpen()) Reanudar(PorMenu);
    }

    // Cargar partida empieza de cero: un canal que alguien cerró y no volvió a abrir (una batalla
    // que no llegó a su cierre) no sobrevive a la carga.
    private static void AlCargarPartida() => s_cierres.Clear();

    // ── Turnos ───────────────────────────────────────────────────────────────────────────────

    /// Pide turno en su canal. Si ya lo había pedido, no hace nada.
    public static void Pedir(IAvisoEnPantalla aviso)
    {
        if (aviso == null || Buscar(aviso) != null) return;
        s_entradas.Add(Nueva(aviso));
        Actualizar();
    }

    /// El aviso ha terminado (o ya no tiene nada que enseñar): deja libre su sitio. Llamarlo de
    /// más no hace nada.
    public static void Terminar(IAvisoEnPantalla aviso)
    {
        var entrada = Buscar(aviso);
        if (entrada == null) return;
        s_entradas.Remove(entrada);
        Actualizar();
    }

    /// El aviso ya no puede esperar (p. ej. la barra de un jefe que ya está recibiendo golpes):
    /// los avisos pasajeros que irían antes que él en su canal se despiden y él pasa a tener el
    /// turno en cuanto no haya silencio. Si no lo había pedido, lo pide.
    public static void Adelantar(IAvisoEnPantalla aviso)
    {
        if (aviso == null) return;
        var propia = Buscar(aviso);
        if (propia == null)
        {
            propia = Nueva(aviso);
            s_entradas.Add(propia);
        }

        s_descartes.Clear();
        for (int i = 0; i < s_entradas.Count; i++)
        {
            var otra = s_entradas[i];
            if (otra != propia && otra.Canal == propia.Canal && otra.Aviso.Pasajero && VaAntes(otra, propia))
                s_descartes.Add(otra);
        }
        for (int i = 0; i < s_descartes.Count; i++)
        {
            var descartada = s_descartes[i];
            s_entradas.Remove(descartada);
            descartada.Visible = false;
            Avisar(descartada.Aviso, Llamada.Descartar);
        }
        s_descartes.Clear();
        Actualizar();
    }

    /// True si el aviso tiene ahora mismo el turno y nada lo tapa.
    public static bool SeEstaViendo(IAvisoEnPantalla aviso)
    {
        var entrada = Buscar(aviso);
        return entrada != null && entrada.Visible;
    }

    // ── Silencio (menús, cinemáticas, intros) ────────────────────────────────────────────────

    /// Calla todos los avisos hasta que <paramref name="quien"/> llame a <see cref="Reanudar"/>.
    /// Lo normal es no llamarlo directamente: las cinemáticas, la intro de un jefe y los textos
    /// dramáticos lo hacen al pasar por <see cref="OcultablesDeCinematica"/>.
    public static void Silenciar(object quien)
    {
        if (quien == null || !s_silencios.Add(quien)) return;
        Actualizar();
    }

    /// Retira el silencio que pidió <paramref name="quien"/>. Llamarlo de más no hace nada.
    public static void Reanudar(object quien)
    {
        if (quien == null || !s_silencios.Remove(quien)) return;
        Actualizar();
    }

    // ── Canales cerrados (reglas del juego sobre qué no se avisa) ───────────────────────────

    /// Mientras <paramref name="quien"/> lo tenga cerrado, lo que llegue a este canal no se avisa
    /// (se descarta, no se enseña después). Lo que ya estaba en pantalla sigue su curso.
    public static void CerrarCanal(CanalDeAviso canal, object quien)
    {
        if (quien == null) return;
        if (!s_cierres.TryGetValue(canal, out var quienes))
        {
            quienes = new HashSet<object>();
            s_cierres[canal] = quienes;
        }
        quienes.Add(quien);
    }

    /// Retira el cierre que pidió <paramref name="quien"/>. Llamarlo de más no hace nada.
    public static void AbrirCanal(CanalDeAviso canal, object quien)
    {
        if (quien != null && s_cierres.TryGetValue(canal, out var quienes)) quienes.Remove(quien);
    }

    /// False mientras alguien tenga cerrado el canal.
    public static bool CanalAbierto(CanalDeAviso canal)
        => !s_cierres.TryGetValue(canal, out var quienes) || quienes.Count == 0;

    // ── Reparto ──────────────────────────────────────────────────────────────────────────────

    private static Entrada Nueva(IAvisoEnPantalla aviso)
        => new Entrada { Aviso = aviso, Canal = aviso.Canal, Llegada = ++s_llegadas };

    private static Entrada Buscar(IAvisoEnPantalla aviso)
    {
        if (aviso == null) return null;
        for (int i = 0; i < s_entradas.Count; i++)
            if (ReferenceEquals(s_entradas[i].Aviso, aviso)) return s_entradas[i];
        return null;
    }

    private static bool VaAntes(Entrada a, Entrada b)
    {
        int pa = a.Aviso.Prioridad, pb = b.Aviso.Prioridad;
        return pa != pb ? pa > pb : a.Llegada < b.Llegada;
    }

    // Los avisos pueden pedir o terminar desde dentro de sus propias llamadas (uno que recibe el
    // turno sin nada que enseñar termina en el acto): esas llamadas anidadas solo piden otra vuelta.
    private static void Actualizar()
    {
        if (s_actualizando) { s_otraVez = true; return; }
        s_actualizando = true;
        try
        {
            int vueltas = 0;
            do
            {
                s_otraVez = false;
                LimpiarMuertos();
                Repartir();
            }
            while (s_otraVez && ++vueltas < 8);
        }
        finally
        {
            s_actualizando = false;
        }
    }

    private static void Repartir()
    {
        bool silencio = Silenciado;

        System.Array.Clear(s_mejorPorCanal, 0, s_mejorPorCanal.Length);
        if (!silencio)
        {
            for (int i = 0; i < s_entradas.Count; i++)
            {
                var e = s_entradas[i];
                int c = (int)e.Canal;
                if (s_mejorPorCanal[c] == null || VaAntes(e, s_mejorPorCanal[c])) s_mejorPorCanal[c] = e;
            }
        }

        // Primero se esconde lo que pierde el turno y después se enseña lo que lo gana, para que
        // en un mismo canal no coincidan dos en pantalla.
        s_copia.Clear();
        s_copia.AddRange(s_entradas);
        for (int i = 0; i < s_copia.Count; i++)
        {
            var e = s_copia[i];
            if (!e.Visible || s_mejorPorCanal[(int)e.Canal] == e || !s_entradas.Contains(e)) continue;
            e.Visible = false;
            Avisar(e.Aviso, Llamada.Perder);
        }
        for (int i = 0; i < s_copia.Count; i++)
        {
            var e = s_copia[i];
            if (e.Visible || s_mejorPorCanal[(int)e.Canal] != e || !s_entradas.Contains(e)) continue;
            e.Visible = true;
            Avisar(e.Aviso, Llamada.Tener);
        }
        s_copia.Clear();
    }

    private static void LimpiarMuertos()
    {
        for (int i = s_entradas.Count - 1; i >= 0; i--)
            if (s_entradas[i].Aviso is Object o && o == null) s_entradas.RemoveAt(i);

        // Un silencio pedido por un objeto que ya no existe (una cinemática destruida a medias) no
        // puede dejar los avisos callados para siempre.
        s_silenciosMuertos.Clear();
        foreach (var quien in s_silencios)
            if (quien is Object o && o == null) s_silenciosMuertos.Add(quien);
        for (int i = 0; i < s_silenciosMuertos.Count; i++) s_silencios.Remove(s_silenciosMuertos[i]);
        s_silenciosMuertos.Clear();

        // MenuManager puede dar por cerrado un menú sin avisar (registro caducado, recuperación de
        // emergencia): el silencio del menú se levanta igualmente en cuanto no queda ninguno.
        if (s_silencios.Contains(PorMenu) && !MenuManager.AnyOpen()) s_silencios.Remove(PorMenu);
    }

    private enum Llamada { Tener, Perder, Descartar }

    private static void Avisar(IAvisoEnPantalla aviso, Llamada llamada)
    {
        try
        {
            switch (llamada)
            {
                case Llamada.Tener: aviso.AlTenerTurno(); break;
                case Llamada.Perder: aviso.AlPerderTurno(); break;
                case Llamada.Descartar: aviso.AlDescartar(); break;
            }
        }
        catch (System.Exception ex)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogException(ex);
#endif
        }
    }
}
