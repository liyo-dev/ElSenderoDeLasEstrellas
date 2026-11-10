using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// La vida del pueblo de Will en los capítulos: la boticaria no se queda quieta junto a su carro; va al
/// campo del espantapájaros a por cestas de hierbas y las deja en el carro (NPCWorkRoute). Ver INC-993.
/// </summary>
public static partial class MontajeDeLosCapitulos
{
    private const string PuestoDeLaBoticaria = "pueblo_boticaria";
    private const string AmbienteDeLaBoticaria = "Assets/_NPCs/Ambient/NPC_Ambient_Boticaria.asset";

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
        var ruta = new GameObject("Ruta de cestas de la boticaria");
        ruta.transform.SetParent(padre, false);
        var r = ruta.AddComponent<NPCWorkRoute>();
        r.puestoId = PuestoDeLaBoticaria;
        r.puntoRecogida = PuntoDeRuta(ruta.transform, "Recogida (campo del espantapájaros)", new Vector3(25.6f, float.NaN, -139.6f), 160f);
        r.puntoEntrega = PuntoDeRuta(ruta.transform, "Entrega (carro de la boticaria)", new Vector3(22.4f, float.NaN, -133.9f), 0f);
        r.materialCaja = MaterialDeColor(new Color(0.66f, 0.5f, 0.27f), "Pueblo_CestaDeHierbas");
        r.tamanoCaja = new Vector3(0.4f, 0.24f, 0.3f);
        r.cajasPorPila = 3;
        informe.AppendLine("Pueblo de Will: la boticaria lleva cestas de hierbas del campo a su carro.");
    }
}
