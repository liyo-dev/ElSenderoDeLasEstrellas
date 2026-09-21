using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Deja la secuencia de Oliver con DOS planos, y nada más (INC-357).
///
/// «Los planos de cámara a veces me vuelven loco, y en la secuencia de Oliver prefiero que se vean
/// los dos. Yo haría una aérea bonita mientras Oliver va hacia Will, y una bastante abierta en la
/// que se vean los dos de perfil para el resto. Sencillez.»
///
/// En la grabación 12 había siete planos en poco más de un minuto: un aéreo, un two-shot vivo,
/// primeros planos de Oliver desde arriba con Will fuera de cuadro, el de Will, otro de Oliver, un
/// two-shot, y el diálogo del tutorial con SU PROPIA cámara (la de diálogo, que va de primer plano
/// en primer plano). Cada corte con el solver buscando hueco entre la casa y la valla.
///
/// Queda así:
///   1. AÉREO, vivo: desde el primer fotograma y mientras Oliver corre hacia Will. Es el mismo
///      encuadre que el plano de apertura, así que no hay corte al empezar la carrera.
///   2. GENERAL DE PERFIL: en cuanto llega, los dos se encaran y la cámara se queda ahí, quieta,
///      hasta el final. El diálogo del tutorial ya no enciende la cámara de diálogo
///      (DialogueBeat.conservarCamaraDeLaSecuencia).
///
/// Edita el SequenceDefinition como objeto y lo guarda con SetDirty + SaveAssets: nada de tocar el
/// .asset a mano (INC-249). Idempotente: se puede ejecutar las veces que haga falta.
public static class OliverPlanosSencillos
{
    private const string Ruta = "Assets/_SEQUENCES/SEQ_OliverSaludo.asset";
    private const string Oliver = "NPC_Oliver";
    private const string Will = "Player";
    private const string NotaAereo = "AEREO (INC-357)";
    private const string NotaPerfil = "PERFIL (INC-357)";
    private const string NotaEncarar = "ENCARAR (INC-357)";

    [MenuItem("El Sendero/Secuencias/Oliver: dos planos (aéreo + perfil)")]
    public static void Menu()
    {
        string r = Ejecutar(avisar: true);
        EditorUtility.DisplayDialog("Secuencia de Oliver", r, "Vale");
    }

    public static string Ejecutar(bool avisar)
    {
        var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(Ruta);
        if (def == null)
        {
            string e = $"No encuentro '{Ruta}'.";
            Debug.LogWarning("[OliverPlanos] " + e);
            return e;
        }

        // Plano de apertura = el aéreo. Así el primer fotograma y la carrera son el mismo encuadre.
        def.openingShotName = "";
        def.openingShot = Aereo();

        int quitados = 0;
        bool hayAereo = false, hayPerfil = false, hayEncarar = false;
        DialogueBeat dialogo = null;
        int iMoveTo = -1;
        SequencePhase faseMoveTo = null;

        foreach (var fase in def.phases)
        {
            if (fase?.beats == null) continue;
            for (int i = fase.beats.Count - 1; i >= 0; i--)
            {
                var b = fase.beats[i];
                if (b is ShotBeat shot)
                {
                    string n = shot.note ?? "";
                    if (n.StartsWith(NotaAereo)) { Aplicar(shot, Aereo(), true); hayAereo = true; continue; }
                    if (n.StartsWith(NotaPerfil)) { Aplicar(shot, Perfil(), false); hayPerfil = true; continue; }
                    fase.beats.RemoveAt(i);
                    quitados++;
                    continue;
                }
                if (b is FaceBeat fb && (fb.note ?? "").StartsWith(NotaEncarar)) hayEncarar = true;
                if (b is DialogueBeat d) dialogo = d;
                if (b is ParallelBeat par) quitados += QuitarPlanos(par);
            }
        }

        // La llegada: el MoveTo de Oliver hacia Will.
        foreach (var fase in def.phases)
        {
            if (fase?.beats == null) continue;
            for (int i = 0; i < fase.beats.Count; i++)
                if (fase.beats[i] is MoveToBeat mv && mv.actorId == Oliver) { faseMoveTo = fase; iMoveTo = i; }
        }

        if (faseMoveTo == null)
        {
            string e = "No encuentro el MoveTo de Oliver hacia Will; no sé dónde empieza la carrera.";
            Debug.LogWarning("[OliverPlanos] " + e);
            return e;
        }

        // El aéreo, justo antes de que eche a correr. Vivo: le sigue desde arriba.
        if (!hayAereo)
        {
            var aereo = new ShotBeat
            {
                note = NotaAereo + ": desde arriba, siguiendo a Oliver mientras corre hacia Will. " +
                       "Mismo encuadre que el de apertura, así que no se nota el paso.",
            };
            Aplicar(aereo, Aereo(), true);
            faseMoveTo.beats.Insert(iMoveTo, aereo);
            iMoveTo++;
        }

        // Al llegar: se encaran y el general de perfil, para todo lo demás.
        int tras = iMoveTo + 1;
        if (!hayEncarar)
        {
            faseMoveTo.beats.Insert(tras++, new FaceBeat
            {
                note = NotaEncarar + ": se miran los dos. Es lo que hace que el general salga de perfil.",
                actorId = Oliver,
                targetActorId = Will,
                mutual = true,
                turnDuration = 0.3f,
            });
        }
        if (!hayPerfil)
        {
            var perfil = new ShotBeat
            {
                note = NotaPerfil + ": general abierto, los dos de perfil. Se queda quieto hasta el final.",
            };
            Aplicar(perfil, Perfil(), false);
            faseMoveTo.beats.Insert(tras, perfil);
        }

        if (dialogo != null) dialogo.conservarCamaraDeLaSecuencia = true;

        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();

        string r = $"SEQ_OliverSaludo: aéreo + perfil. {quitados} plano(s) quitados" +
                   (dialogo != null ? "; el tutorial ya no cambia a la cámara de diálogo." : ".");
        Debug.Log("[OliverPlanos] " + r);
        return r;
    }

    private static int QuitarPlanos(ParallelBeat par)
    {
        int n = 0;
        if (par.beats == null) return 0;
        for (int i = par.beats.Count - 1; i >= 0; i--)
        {
            if (par.beats[i] is ShotBeat) { par.beats.RemoveAt(i); n++; }
            else if (par.beats[i] is ParallelBeat hijo) n += QuitarPlanos(hijo);
        }
        return n;
    }

    private static void Aplicar(ShotBeat shot, ShotFraming f, bool vivo)
    {
        shot.shotName = "";
        shot.framing = f;
        shot.smooth = false;
        shot.duration = 1.4f;
        shot.waitForArrival = true;
        shot.live = vivo;
    }

    /// Alto (seis metros) y abierto: se ve la plaza, Oliver corriendo y Will esperando.
    private static ShotFraming Aereo() => new ShotFraming
    {
        type = ShotType.Wide,
        subjectId = Oliver,
        secondaryId = Will,
        heightBias = 6f,
        distanceScale = 1.3f,
        fovOverride = 0f,
        crossTheLine = false,
        headroom = true,
        encara = false,
    };

    /// El two-shot es el plano de perfil (cámara perpendicular a la línea que los une). Abierto
    /// para que quepan enteros, gesto y explosión incluidos, y un poco por encima de los ojos.
    private static ShotFraming Perfil() => new ShotFraming
    {
        type = ShotType.TwoShot,
        subjectId = Oliver,
        secondaryId = Will,
        heightBias = 0.4f,
        distanceScale = 1.8f,
        fovOverride = 0f,
        crossTheLine = false,
        headroom = true,
        encara = false,
    };
}
