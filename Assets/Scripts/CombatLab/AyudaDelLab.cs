using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Pestaña «Ayuda» del LAB: bienvenida, manual de lo que se puede probar y el botón que copia los
/// datos para un reporte (escena, zona, posición, versión, música, vida y maná).
public sealed class AyudaDelLab : MonoBehaviour, ISeccionDelLab
{
    public string Titulo => "Ayuda";
    public int Orden => 90;

    public void Dibujar()
    {
        GUILayout.Label("¡Bienvenido al LAB!", EstiloDelLab.Titulo);
        GUILayout.Label("Un laboratorio para probar el juego por partes: combate, magia, compañeros, plataformas, puzles, agua, vuelo, tiendas y jefes. Nada de lo que hagas aquí toca tu partida guardada.", EstiloDelLab.Etiqueta);

        GUILayout.Space(4f);
        if (GUILayout.Button("Copiar datos para el reporte", EstiloDelLab.BotonActivo, GUILayout.Width(300f))) CopiarReporte();
        GUILayout.Label("Si encuentras un fallo, pulsa el botón y pega el texto en tu reporte junto a lo que pasó y cómo repetirlo.", EstiloDelLab.Nota);

        Seccion("El panel",
            "<b>Tab</b> (teclado) o <b>Select</b> (mando) abre y cierra este panel; con él abierto el juego se pausa. " +
            "Arriba a la derecha queda solo la pestaña «LAB» y los avisos (por ejemplo, la canción que empieza a sonar).");
        Seccion("Controles",
            "Moverse: stick izquierdo / WASD · Cámara: stick derecho / ratón · Saltar: A / Espacio (otra vez en el aire: doble salto) · Volar: salta, vuelve a saltar en el aire y salta una tercera vez.\n" +
            "Hechizo: X / clic izquierdo (serie de tres; mantener = disparo preciso) · LB / rueda: siguiente hechizo básico.\n" +
            "Combo mágico: Y / Q y teclea la secuencia · Escudo: mantener B / clic derecho; pulsarlo justo antes del golpe = contraataque.\n" +
            "Ataque de equipo: LT+RT / Ctrl con un compañero cerca · Menú de Start: equipar hechizos, ropa y objetos.");
        Seccion("Zonas (pestaña Zonas para ir directo)",
            "<b>Combate</b> (centro): escenarios F1–F4: blanco quieto, un enemigo, un grupo y el Gólem. R reinicia el LAB.\n" +
            "<b>Plataformeo</b> (oeste): escalones, saltos cortos y largos, doble salto, viga estrecha, rampa y muro de escalada.\n" +
            "<b>Puzles</b> (este): puerta con dos placas, placa que eleva, objeto que se quema con fuego y runas que hay que repetir en orden.\n" +
            "<b>Agua</b> (norte): nadar en lo hondo y en lo poco hondo.\n" +
            "<b>Vuelo</b> (sur): torres y plataformas flotantes a 10, 18 y 26 m.\n" +
            "<b>Tiendas y Esencia</b> (sur): Renard (Esencia) y Tomasa (monedas), placas de +100 y un corral de arañas para contratos.\n" +
            "<b>Zona de jefes</b> (oeste, por el pasillo del plataformeo): Demonio 1, Demonio 2, Gólem y Mago Oscuro. Al ganar vuelves a la entrada con la vida llena; si caes, el LAB se recarga allí.");
        Seccion("Pestañas",
            "<b>Combate</b>: escenarios del centro. <b>Zonas</b>: viaje rápido y salir al menú. <b>Grupo</b>: pelear solo, con Estela, con Liam o con los dos. " +
            "<b>Magia</b>: cambiar hechizos y habilidades. <b>Música</b>: toda la música del juego. <b>Trucos</b>: invencible, maná infinito, cámara lenta y curar.");
        Seccion("Qué mirar",
            "Que cada hechizo se vea, vuele e impacte bien · que los enemigos y jefes reaccionen y se les pueda ganar · que los compañeros ayuden · " +
            "que la cámara no se quede atascada · que los textos se lean · que la música y los sonidos encajen · cualquier cosa rara, aunque parezca pequeña.");
    }

    private static void Seccion(string titulo, string texto)
    {
        GUILayout.Label(titulo, EstiloDelLab.Titulo);
        GUILayout.Label(texto, EstiloDelLab.Etiqueta);
    }

    private static void CopiarReporte()
    {
        var sb = new StringBuilder();
        sb.AppendLine("— Datos del LAB —");
        sb.AppendLine($"Versión: {Application.version}");
        sb.AppendLine($"Escena: {SceneManager.GetActiveScene().name}");
        var jugador = PlayerService.Player;
        if (jugador != null)
        {
            Vector3 p = jugador.transform.position;
            sb.AppendLine($"Zona: {ViajeDelLab.ZonaCercana(p)}");
            sb.AppendLine($"Posición: {p.x:0.0}, {p.y:0.0}, {p.z:0.0}");
            var salud = jugador.GetComponentInChildren<PlayerHealthSystem>(true);
            if (salud != null) sb.AppendLine($"Vida: {salud.CurrentHealth:0}/{salud.MaxHealth:0}");
            var mana = jugador.GetComponentInChildren<ManaPool>(true);
            if (mana != null) sb.AppendLine($"Maná: {mana.Current:0}/{mana.Max:0}");
        }
        var musica = AudioService.Instance != null ? AudioService.Instance.CurrentMusicClip : null;
        sb.AppendLine($"Música: {(musica != null ? musica.name : "—")}");
        sb.AppendLine($"Sistema: {SystemInfo.operatingSystem} · {SystemInfo.graphicsDeviceName} · {Screen.width}×{Screen.height}");
        sb.AppendLine($"Fecha: {System.DateTime.Now:yyyy-MM-dd HH:mm}");
        GUIUtility.systemCopyBuffer = sb.ToString();
        PanelDelLab.Aviso("Datos copiados: pégalos en tu reporte");
    }
}
