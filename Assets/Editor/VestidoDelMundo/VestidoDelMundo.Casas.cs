using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Las casas que puso el generador del mapa dan la espalda a su calle: el generador suponía que la fachada
/// de los BuildingAT es su +Z local y en casi todos la puerta está en otro lado. Aquí se giran sobre el
/// centro de su huella para que la puerta mire a donde el generador quería que mirase la fachada. Se guarda
/// el giro y la posición originales; «Quitar el vestido» (y cada nueva ejecución, antes de girar) los repone.
public static partial class VestidoDelMundo
{
    private const string ArchivoCasas = "casas_originales.txt";

    /// Lado de la puerta a ras de suelo de cada prefab, en grados respecto al +Z local (medido en sus hijos Door*).
    private static readonly Dictionary<string, float> LadoDeLaPuerta = new()
    {
        { "BuildingAT01", 180f },
        { "BuildingAT03", 90f },
        { "BuildingAT07", 90f },
        { "BuildingAT10", 180f },
        { "BuildingAT12", 180f },
        { "BuildingAT17", 180f },
        { "BuildingAT53", 180f },
    };

    /// Grupos de WORLD cuyas casas se giran (las de Will, granjas e islas no se tocan).
    private static readonly string[] GruposConCasasAGirar =
    {
        "Reino — barrio de montaña y explanada real",
        "Pueblo pesquero — calles y muelles",
        "Pueblo vecino — terraza sobre la playa",
    };

    private static Transform BuscarGrupo(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            if (raiz.name != NombrePadre) continue;
            Transform t = raiz.transform.Find(nombre);
            if (t != null) return t;
        }
        return null;
    }

    private static string RutaJerarquia(Transform t)
    {
        var sb = new StringBuilder(t.name);
        for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }

    private static Transform BuscarPorRuta(Scene escena, string ruta)
    {
        string[] partes = ruta.Split('/');
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            if (raiz.name != partes[0]) continue;
            Transform t = raiz.transform;
            for (int i = 1; i < partes.Length && t != null; i++) t = t.Find(partes[i]);
            if (t != null) return t;
        }
        return null;
    }

    private static void GirarCasasHaciaSuCalle(Scene escena, Obra o)
    {
        var lineas = new List<string>();
        int giradas = 0, revertidas = 0;
        foreach (string nombreGrupo in GruposConCasasAGirar)
        {
            Transform grupo = BuscarGrupo(escena, nombreGrupo);
            if (grupo == null) { o.Informe.Add($"  · no encuentro el grupo «{nombreGrupo}»: sus casas no se giran."); continue; }
            var nombres = new Dictionary<string, int>();
            foreach (Transform hijo in grupo) nombres[hijo.name] = nombres.TryGetValue(hijo.name, out int k) ? k + 1 : 1;
            foreach (Transform casa in grupo)
            {
                GameObject fuente = PrefabUtility.GetCorrespondingObjectFromSource(casa.gameObject);
                if (fuente == null || !LadoDeLaPuerta.TryGetValue(fuente.name, out float lado)) continue;
                if (Mathf.Abs(Mathf.DeltaAngle(lado, 0f)) < 1f) continue;
                // La reposición busca la casa por su ruta: con el nombre repetido podría devolver otra.
                if (nombres[casa.name] > 1) { o.Informe.Add($"  · «{casa.name}» no se gira: hay otra con el mismo nombre en su grupo."); continue; }

                Vector3 pos = casa.position;
                Quaternion rot = casa.rotation;
                Bounds antes = LimitesVisibles(casa.gameObject);
                casa.RotateAround(new Vector3(antes.center.x, pos.y, antes.center.z), Vector3.up, -lado);

                Bounds despues = LimitesVisibles(casa.gameObject);
                if (ChocaConOtraCasa(casa, despues))
                {
                    casa.SetPositionAndRotation(pos, rot);
                    revertidas++;
                    o.Informe.Add($"  · «{casa.name}» no se gira: chocaría con algo vecino.");
                    continue;
                }
                lineas.Add(string.Join("|", RutaJerarquia(casa),
                    pos.x.ToString("R", CultureInfo.InvariantCulture), pos.y.ToString("R", CultureInfo.InvariantCulture), pos.z.ToString("R", CultureInfo.InvariantCulture),
                    rot.x.ToString("R", CultureInfo.InvariantCulture), rot.y.ToString("R", CultureInfo.InvariantCulture), rot.z.ToString("R", CultureInfo.InvariantCulture), rot.w.ToString("R", CultureInfo.InvariantCulture)));
                giradas++;
            }
        }
        if (lineas.Count > 0)
        {
            Directory.CreateDirectory(RutaCopias);
            File.WriteAllLines(Path.Combine(RutaCopias, ArchivoCasas), lineas, Encoding.UTF8);
        }
        o.Informe.Add($"Casas: {giradas} giradas para que la puerta dé a su calle, {revertidas} sin girar por espacio.");
    }

    private static bool ChocaConOtraCasa(Transform casa, Bounds b)
    {
        Physics.SyncTransforms();
        var buffer = new Collider[64];
        Vector3 medio = new Vector3(b.extents.x - 0.4f, Mathf.Max(0.3f, b.extents.y * 0.6f), b.extents.z - 0.4f);
        int n = Physics.OverlapBoxNonAlloc(b.center + Vector3.up * 0.6f, medio, buffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = buffer[i];
            if (Ignorable(c) || c.transform.IsChildOf(casa)) continue;
            return true;
        }
        return false;
    }

    private static float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    /// Devuelve a su sitio las casas que giró la última ejecución.
    private static void ReponerCasas(Scene escena, List<string> informe)
    {
        string ruta = Path.Combine(RutaCopias, ArchivoCasas);
        if (!File.Exists(ruta)) return;
        int n = 0, perdidas = 0;
        foreach (string linea in File.ReadAllLines(ruta, Encoding.UTF8))
        {
            string[] p = linea.Split('|');
            if (p.Length != 8) continue;
            Transform casa = BuscarPorRuta(escena, p[0]);
            if (casa == null) { perdidas++; continue; }
            try
            {
                var pos = new Vector3(F(p[1]), F(p[2]), F(p[3]));
                var rot = new Quaternion(F(p[4]), F(p[5]), F(p[6]), F(p[7]));
                casa.SetPositionAndRotation(pos, rot);
                n++;
            }
            catch (FormatException) { perdidas++; }
        }
        File.Delete(ruta);
        informe.Add($"Casas: {n} devueltas a su giro original" + (perdidas > 0 ? $" ({perdidas} no encontradas)." : "."));
    }
}
