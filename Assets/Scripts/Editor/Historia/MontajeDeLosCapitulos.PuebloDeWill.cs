using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// La vida del pueblo de Will en los capítulos: la boticaria no se queda quieta junto a su carro; trabaja
/// en el campo del espantapájaros recogiendo hierbas, de mata en mata, y de vez en cuando va a dejarlas al
/// carro (NPCWorkRoute en faena en el sitio). Ver INC-993 e INC-1034.
/// </summary>
public static partial class MontajeDeLosCapitulos
{
    private const string PuestoDeLaBoticaria = "pueblo_boticaria";
    private const string AmbienteDeLaBoticaria = "Assets/_NPCs/Ambient/NPC_Ambient_Boticaria.asset";

    private static Vector3 Llano(Vector3 p) => new Vector3(p.x, float.NaN, p.z);

    private static void PonerExtrasPuebloDeWill(Transform raiz, StringBuilder informe)
    {
        var ambiente = Cargar<Game.NPC.Modules.NPCAmbientConfig>(AmbienteDeLaBoticaria);
        if (ambiente == null) { informe.AppendLine("AVISO: falta " + AmbienteDeLaBoticaria); return; }
        if (!ambiente.permitirTrabajo || ambiente.puestoId != PuestoDeLaBoticaria)
        {
            ambiente.permitirTrabajo = true;
            ambiente.puestoId = PuestoDeLaBoticaria;
            EditorUtility.SetDirty(ambiente);
        }

        var padre = Hijo(raiz, "Pueblo de Will · vida");
        var ruta = new GameObject("Faena de la boticaria en el campo");
        ruta.transform.SetParent(padre, false);
        var r = ruta.AddComponent<NPCWorkRoute>();
        r.puestoId = PuestoDeLaBoticaria;
        r.puntoRecogida = PuntoDeRuta(ruta.transform, "Recogida (campo del espantapájaros)", new Vector3(25.6f, float.NaN, -139.6f), 160f);
        r.puntoEntrega = PuntoDeRuta(ruta.transform, "Entrega (carro de la boticaria)", new Vector3(22.4f, float.NaN, -133.9f), 0f);
        // Matas del campo, hacia donde mira la recogida (el campo queda a 160°), separadas entre sí.
        var frente = Quaternion.Euler(0f, 160f, 0f) * Vector3.forward;
        var lado = Quaternion.Euler(0f, 250f, 0f) * Vector3.forward;
        var centro = new Vector3(25.6f, 0f, -139.6f);
        r.puntosDeFaena = new System.Collections.Generic.List<Transform>
        {
            PuntoDeRuta(ruta.transform, "Mata 1", Llano(centro + frente * 1.5f), 160f),
            PuntoDeRuta(ruta.transform, "Mata 2", Llano(centro + frente * 3f + lado * 1.5f), 180f),
            PuntoDeRuta(ruta.transform, "Mata 3", Llano(centro + frente * 3f - lado * 1.5f), 140f),
            PuntoDeRuta(ruta.transform, "Mata 4", Llano(centro + frente * 4.5f), 160f),
        };
        // Recoge de pie, inclinada sobre la mata, con el clip propio (El Sendero ▸ Personajes ▸ Animaciones ▸
        // Recoger hierbas); sin gesto de entrada ni de salida: la pose entra y sale con fundido. INC-1034.
        r.animacionFaenaEmpezar = "";
        r.animacionFaena = "RecogerHierbas_Loop";
        r.animacionFaenaAcabar = "";
        r.faenasAntesDeEntregar = 3;
        r.segundosDeFaena = new Vector2(5f, 9f);
        informe.AppendLine("Pueblo de Will: la boticaria recoge hierbas en el campo y de vez en cuando las lleva a su carro.");
    }
}
