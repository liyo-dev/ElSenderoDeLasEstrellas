using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// Pestaña «Música» del LAB: toda la música del juego (AudioGraphProfile.ReunirMusica), agrupada
/// por dónde suena, con anterior/siguiente, parar, repetir, en orden o aleatorio y la barra de lo
/// que va sonando. La elegida queda como música de la escena (tras un combate vuelve a ella).
/// Cada vez que cambia la música, por el motivo que sea, avisa con «♪ nombre».
public sealed class JukeboxDelLab : MonoBehaviour, ISeccionDelLab
{
    private enum Modo { Repetir, EnOrden, Aleatorio }

    private const float Fundido = 0.6f;
    private static readonly string[] Grupos = { "Todas", "Escenas", "Batallas", "Zonas", "Secuencias", "Minijuegos" };

    private readonly List<AudioGraphProfile.CancionDelJuego> _canciones = new List<AudioGraphProfile.CancionDelJuego>();
    private int _elegida = -1;
    private int _grupo;
    private Modo _modo = Modo.Repetir;
    private AudioClip _ultimaSonando;
    private float _siguienteBusqueda;

    public string Titulo => "Música";
    public int Orden => 50;

    private void Update()
    {
        var audio = AudioService.Instance;
        if (audio == null) return;
        if (_canciones.Count == 0 && Time.unscaledTime >= _siguienteBusqueda)
        {
            _siguienteBusqueda = Time.unscaledTime + 1f;
            Recargar();
        }

        var sonando = audio.CurrentMusicClip;
        if (sonando != _ultimaSonando)
        {
            _ultimaSonando = sonando;
            if (sonando != null) PanelDelLab.Aviso("♪ " + Bonito(sonando.name));
        }

        // Al acabar una canción elegida aquí, en orden o aleatorio pasa a la siguiente.
        if (_modo == Modo.Repetir || _elegida < 0 || sonando != _canciones[_elegida].clip) return;
        float queda = audio.GetMusicRemainingSeconds();
        if (queda >= 0f && queda < 0.25f)
            Poner(_modo == Modo.Aleatorio ? UnityEngine.Random.Range(0, _canciones.Count) : _elegida + 1);
    }

    private void Recargar()
    {
        _canciones.Clear();
        var audio = AudioService.Instance;
        if (audio != null && audio.profile != null) audio.profile.ReunirMusica(_canciones);
        _canciones.Sort((a, b) => string.Compare(Bonito(a.clip.name), Bonito(b.clip.name), StringComparison.OrdinalIgnoreCase));
    }

    private void Poner(int indice)
    {
        var audio = AudioService.Instance;
        if (audio == null || _canciones.Count == 0) return;
        _elegida = (indice % _canciones.Count + _canciones.Count) % _canciones.Count;
        audio.FijarMusicaDeEscena(_canciones[_elegida].clip, Fundido);
        audio.SetMusicLooping(_modo == Modo.Repetir);
    }

    private void Parar()
    {
        var audio = AudioService.Instance;
        if (audio != null) audio.StopMusic(Fundido);
        _elegida = -1;
    }

    public void Dibujar()
    {
        var audio = AudioService.Instance;
        if (audio == null || _canciones.Count == 0)
        {
            GUILayout.Label("Esperando al servicio de audio…", EstiloDelLab.Nota);
            return;
        }

        var sonando = audio.CurrentMusicClip;
        int actual = IndiceDe(sonando);
        GUILayout.Label(sonando != null ? "♪ " + Bonito(sonando.name) : "Sin música", EstiloDelLab.Titulo);
        GUILayout.Label(actual >= 0 ? $"{_canciones[actual].grupo}: {_canciones[actual].donde}" : " ", EstiloDelLab.Nota);
        float tiempo = audio.GetMusicTime();
        if (sonando != null && tiempo >= 0f)
        {
            EstiloDelLab.Progreso(tiempo / Mathf.Max(0.01f, sonando.length));
            GUILayout.Label($"{Reloj(tiempo)} / {Reloj(sonando.length)}", EstiloDelLab.Nota);
        }

        GUILayout.BeginHorizontal();
        int base_ = actual >= 0 ? actual : Mathf.Max(0, _elegida);
        if (GUILayout.Button("‹ Anterior", EstiloDelLab.Boton)) Poner(base_ - 1);
        if (GUILayout.Button("■ Parar", EstiloDelLab.Boton)) Parar();
        if (GUILayout.Button("Siguiente ›", EstiloDelLab.Boton)) Poner(base_ + 1);
        GUILayout.Space(12f);
        if (EstiloDelLab.Opcion("Repetir", _modo == Modo.Repetir)) CambiarModo(Modo.Repetir);
        if (EstiloDelLab.Opcion("En orden", _modo == Modo.EnOrden)) CambiarModo(Modo.EnOrden);
        if (EstiloDelLab.Opcion("Aleatorio", _modo == Modo.Aleatorio)) CambiarModo(Modo.Aleatorio);
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        for (int g = 0; g < Grupos.Length; g++)
            if (GUILayout.Button(Grupos[g], g == _grupo ? EstiloDelLab.PestanaActiva : EstiloDelLab.Pestana)) _grupo = g;
        GUILayout.EndHorizontal();

        for (int i = 0; i < _canciones.Count; i++)
        {
            var c = _canciones[i];
            if (_grupo > 0 && c.grupo != Grupos[_grupo]) continue;
            string texto = $"{Bonito(c.clip.name)}   <color=#9aa3b5><size=12>{c.grupo} · {c.donde}</size></color>";
            if (GUILayout.Button(texto, c.clip == sonando ? EstiloDelLab.FilaActiva : EstiloDelLab.Fila)) Poner(i);
        }
    }

    private void CambiarModo(Modo modo)
    {
        _modo = modo;
        if (_elegida >= 0 && AudioService.Instance != null) AudioService.Instance.SetMusicLooping(modo == Modo.Repetir);
    }

    private int IndiceDe(AudioClip clip)
    {
        if (clip == null) return -1;
        for (int i = 0; i < _canciones.Count; i++) if (_canciones[i].clip == clip) return i;
        return -1;
    }

    /// «batalla-el-demonio» → «Batalla el demonio».
    private static string Bonito(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return "";
        string s = nombre.Replace('_', ' ').Replace('-', ' ').Trim();
        return s.Length == 0 ? s : char.ToUpper(s[0], CultureInfo.CurrentCulture) + s.Substring(1);
    }

    private static string Reloj(float segundos)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(segundos));
        return $"{s / 60}:{s % 60:00}";
    }
}
