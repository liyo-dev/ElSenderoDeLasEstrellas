using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Kits.DrawDistance;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// En Play: mide el tiempo de GPU (FrameTimingManager) de la cámara de juego con cada pieza del render
/// apagada por turnos (recorte por tamaño, sombras, post y contorno, flores del terreno, minimapa, vestido
/// del mundo), mirando desde el jugador hacia los cuatro puntos cardinales. Toma la cámara con
/// CameraDirectorService mientras mide y lo devuelve todo como estaba. Escribe Guiones/_ordenes/coste_del_render.md.
/// Sirve para saber qué parte del dibujo pesa en un sitio concreto antes de tocar nada (INC-1339).
public static class MedirCosteDelRender
{
    const int FotogramasDeEspera = 25;
    const int FotogramasDeMuestra = 60;
    static readonly float[] Rumbos = { 0f, 90f, 180f, 270f };
    static readonly object Dueno = new object();

    sealed class Prueba
    {
        public string nombre;
        public Action<bool> apagar;
    }

    static List<Prueba> s_pruebas;
    static int s_prueba, s_rumbo, s_fase, s_desde;
    static readonly List<float> s_gpu = new List<float>(), s_cpu = new List<float>();
    static readonly FrameTiming[] s_tiempos = new FrameTiming[1];
    static float[,] s_resGpu, s_resCpu;
    static Vector3 s_centro;
    static Camera s_camara;
    static Vector3 s_posOriginal;
    static Quaternion s_rotOriginal;

    [MenuItem("El Sendero/Render/Medir el coste de cada pieza del render (en Play)")]
    static void Medir()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[Coste del render] Solo en Play."); return; }
        var jugador = PlayerService.Player;
        s_camara = Camera.main;
        if (jugador == null || s_camara == null) { Debug.LogWarning("[Coste del render] Falta el jugador o la cámara."); return; }

        s_centro = jugador.transform.position;
        // Punto fijo opcional: «x y z» en Guiones/_ordenes/coste_del_render_centro.txt (si no, el jugador).
        string rutaCentro = Path.Combine("Guiones", "_ordenes", "coste_del_render_centro.txt");
        if (File.Exists(rutaCentro))
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var v = File.ReadAllText(rutaCentro).Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (v.Length >= 3) s_centro = new Vector3(float.Parse(v[0], ci), float.Parse(v[1], ci), float.Parse(v[2], ci));
        }
        s_posOriginal = s_camara.transform.position;
        s_rotOriginal = s_camara.transform.rotation;
        s_pruebas = CrearPruebas();
        s_resGpu = new float[s_pruebas.Count, Rumbos.Length];
        s_resCpu = new float[s_pruebas.Count, Rumbos.Length];
        s_prueba = 0; s_rumbo = 0; s_fase = 0;
        CameraDirectorService.Claim(Dueno, false);
        EditorApplication.update -= Paso;
        EditorApplication.update += Paso;
        Debug.Log($"[Coste del render] Midiendo {s_pruebas.Count} pruebas × {Rumbos.Length} rumbos…");
    }

    static List<Prueba> CrearPruebas()
    {
        var lista = new List<Prueba> { new Prueba { nombre = "Todo como en el juego", apagar = _ => { } } };
        lista.Add(new Prueba
        {
            nombre = "Sin recorte por tamaño en pantalla (ScreenSizeCuller)",
            apagar = a => { foreach (var c in UnityEngine.Object.FindObjectsByType<ScreenSizeCuller>()) c.enabled = !a; }
        });
        lista.Add(new Prueba
        {
            nombre = "Sin sombras",
            apagar = a => { var d = s_camara.GetUniversalAdditionalCameraData(); if (d != null) d.renderShadows = !a; }
        });
        lista.Add(new Prueba
        {
            nombre = "Sin post-procesado ni contorno (ni su prepase de normales)",
            apagar = a => { var d = s_camara.GetUniversalAdditionalCameraData(); if (d != null) d.renderPostProcessing = !a; }
        });
        lista.Add(new Prueba
        {
            nombre = "Sin flores ni árboles del terreno",
            apagar = a => { foreach (var t in Terrain.activeTerrains) if (t != null) t.drawTreesAndFoliage = !a; }
        });
        lista.Add(new Prueba
        {
            nombre = "Sin la cámara del minimapa",
            apagar = a =>
            {
                foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>())
                    if (c != s_camara && c.targetTexture != null && c.orthographic) c.enabled = !a;
            }
        });
        var vestidos = new List<Renderer>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (var raiz in SceneManager.GetSceneAt(i).GetRootGameObjects())
            {
                var v = raiz.transform.Find("Vestido del mundo (generado)");
                if (v == null && raiz.name == "Vestido del mundo (generado)") v = raiz.transform;
                if (v != null) vestidos.AddRange(v.GetComponentsInChildren<Renderer>(true));
            }
        var antes = new Dictionary<Renderer, bool>();
        lista.Add(new Prueba
        {
            nombre = $"Sin el vestido del mundo ({vestidos.Count} renderers)",
            apagar = a =>
            {
                foreach (var r in vestidos)
                {
                    if (r == null) continue;
                    if (a) { antes[r] = r.forceRenderingOff; r.forceRenderingOff = true; }
                    else if (antes.TryGetValue(r, out bool b)) r.forceRenderingOff = b;
                }
            }
        });
        return lista;
    }

    static void ColocarCamara()
    {
        var dir = Quaternion.Euler(0f, Rumbos[s_rumbo], 0f) * Vector3.forward;
        s_camara.transform.position = s_centro - dir * 7f + Vector3.up * 3.5f;
        s_camara.transform.rotation = Quaternion.LookRotation(s_centro + dir * 25f + Vector3.up - s_camara.transform.position);
    }

    static void Paso()
    {
        if (!EditorApplication.isPlaying || s_camara == null) { Terminar(false); return; }
        FrameTimingManager.CaptureFrameTimings();
        ColocarCamara();

        if (s_fase == 0)
        {
            if (s_rumbo == 0) s_pruebas[s_prueba].apagar(true);
            s_desde = Time.frameCount;
            s_gpu.Clear(); s_cpu.Clear();
            s_fase = 1;
            return;
        }
        int pasados = Time.frameCount - s_desde;
        if (pasados < FotogramasDeEspera) return;
        if (FrameTimingManager.GetLatestTimings(1, s_tiempos) > 0)
        {
            var t = s_tiempos[0];
            if (t.gpuFrameTime > 0 && t.gpuFrameTime < 1000) s_gpu.Add((float)t.gpuFrameTime);
            if (t.cpuFrameTime > 0 && t.cpuFrameTime < 1000) s_cpu.Add((float)t.cpuFrameTime);
        }
        if (pasados < FotogramasDeEspera + FotogramasDeMuestra) return;

        s_resGpu[s_prueba, s_rumbo] = Mediana(s_gpu);
        s_resCpu[s_prueba, s_rumbo] = Mediana(s_cpu);
        s_fase = 0;
        if (++s_rumbo < Rumbos.Length) return;
        s_rumbo = 0;
        s_pruebas[s_prueba].apagar(false);
        if (++s_prueba < s_pruebas.Count) return;
        Terminar(true);
    }

    static float Mediana(List<float> v)
    {
        if (v.Count == 0) return -1f;
        v.Sort();
        return v[v.Count / 2];
    }

    static void Terminar(bool escribir)
    {
        EditorApplication.update -= Paso;
        if (s_pruebas != null && s_prueba < s_pruebas.Count) s_pruebas[s_prueba].apagar(false);
        if (s_camara != null) { s_camara.transform.position = s_posOriginal; s_camara.transform.rotation = s_rotOriginal; }
        CameraDirectorService.Release(Dueno);
        if (!escribir) { Debug.LogWarning("[Coste del render] Medida cortada."); return; }

        var sb = new StringBuilder("# Coste del render por piezas\n\n");
        sb.Append($"- Escena activa: {SceneManager.GetActiveScene().name}; centro {s_centro}; pantalla {Screen.width}×{Screen.height}; ");
        sb.Append($"{UnityEngine.Object.FindObjectsByType<Renderer>().Length} renderers activos\n");
        foreach (var c in UnityEngine.Object.FindObjectsByType<ScreenSizeCuller>())
            sb.Append($"- {c.gameObject.scene.name}: recorte por tamaño vigila {c.TrackedCount}, sin dibujar ahora {c.CulledCount}\n");
        sb.Append("\nMediana de GPU (ms) / CPU (ms) por rumbo (0 = norte):\n\n| Prueba |");
        foreach (float r in Rumbos) sb.Append($" {r:0}° |");
        sb.Append(" media GPU | ahorro GPU |\n|---|");
        foreach (float _ in Rumbos) sb.Append("---|");
        sb.Append("---|---|\n");
        float baseMedia = 0f;
        for (int p = 0; p < s_pruebas.Count; p++)
        {
            sb.Append($"| {s_pruebas[p].nombre} |");
            float suma = 0f;
            for (int r = 0; r < Rumbos.Length; r++) { sb.Append($" {s_resGpu[p, r]:0.0} / {s_resCpu[p, r]:0.0} |"); suma += s_resGpu[p, r]; }
            float media = suma / Rumbos.Length;
            if (p == 0) baseMedia = media;
            sb.Append($" {media:0.0} | {(p == 0 ? "—" : (baseMedia - media).ToString("0.0"))} |\n");
        }
        string ruta = Path.Combine("Guiones", "_ordenes", "coste_del_render.md");
        File.WriteAllText(ruta, sb.ToString());
        Debug.Log("[Coste del render] " + sb);
    }
}
