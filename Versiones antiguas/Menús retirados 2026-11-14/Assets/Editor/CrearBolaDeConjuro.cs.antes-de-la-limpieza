#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// La bola de conjuro del Archimago: la pequeÃ±a esfera de luz que se forma entre sus manos
/// mientras prepara un hechizo.
///
/// Es PEQUEÃ‘A a propÃ³sito (unos 20 cm). Lo que se usaba antes era un destello genÃ©rico del pack
/// ("1 Flash_magic_ellow_blue"), que lleva una partÃ­cula de 19 m de ancho: en cuanto la cÃ¡mara
/// se acercaba un poco, la "bola" se comÃ­a la pantalla entera y cualquier hechizo menor parecÃ­a
/// el hechizo protector del final del prÃ³logo.
/// Ver INC-801.
public static class CrearBolaDeConjuro
{
    public const string RutaPrefab = "Assets/_VFX/Prologo/VFX_BolaDeConjuro.prefab";
    private const string CarpetaTexturas = "Assets/_VFX/Prologo/Texturas";
    private const string CarpetaMateriales = "Assets/_VFX/Prologo/Materiales";

    [MenuItem("El Sendero/VFX/Crear bola de conjuro INC-801")]
    public static void Menu() => Asegurar();

    public static string Asegurar()
    {
        var t = new VfxProcedural.Texturas(CarpetaTexturas, CarpetaMateriales, "Prologo");
        VfxProcedural.GuardarPrefab("VFX_BolaDeConjuro", RutaPrefab, root => Construir(root, t));
        AssetDatabase.SaveAssets();
        return RutaPrefab;
    }

    private static void Construir(GameObject root, VfxProcedural.Texturas t)
    {
        // El nÃºcleo: la bola en sÃ­, blanca y viva.
        VfxProcedural.Nucleo(root.transform, t, "Nucleo", new Color(1f, 0.98f, 0.92f) * 4.5f, 0.22f);

        // El halo: el aire alrededor de la bola que la hace leerse como una esfera de luz.
        VfxProcedural.Nucleo(root.transform, t, "Halo", new Color(0.78f, 0.9f, 1f) * 1.8f, 0.46f);

        // Solo el nucleo y el halo: las chispas y las motas salian sueltas por delante del
        // conjurante y parecian chispas sueltas que ensuciaban el plano (Prologo_6).
    }
}
#endif
