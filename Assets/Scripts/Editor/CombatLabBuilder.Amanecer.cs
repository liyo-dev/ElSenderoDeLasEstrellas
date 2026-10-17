using UnityEditor;
using UnityEngine;

/// Estación de Amanecer en el LAB (al noroeste, solo por viaje rápido). Ver INC-724.
///
///  · Guardián protegido: solo recibe daño mientras Amanecer le tiene la protección abierta
///    (ProteccionQueSeAbre + SoloDanoCuandoExpuesto). Fuera de esa ventana avisa de golpe mal dado.
///  · Sello de templo: un muro que Amanecer abre para siempre (quedaAbierta); detrás, el premio.
///
/// Amanecer se da o se quita en la pestaña Magia. El Mago Oscuro de la zona de jefes también se
/// abre con él (cristales y regeneración).
public static partial class CombatLabBuilder
{
    private static readonly Vector3 CentroAmanecer = new Vector3(-70f, 0f, 60f);
    private static readonly Vector3 EntradaAmanecer = new Vector3(-70f, 0.1f, 50f);
    private const float AnchoAmanecer = 26f;
    private const float FondoAmanecer = 22f;

    private static void CrearEstacionAmanecer()
    {
        var raiz = new GameObject("LAB_AMANECER").transform;
        var c = CentroAmanecer;
        float mx = AnchoAmanecer * 0.5f, mz = FondoAmanecer * 0.5f;

        var suelo = GetMaterial("Lab_Suelo", new Color(0.19f, 0.22f, 0.28f));
        var muro = GetMaterial("Lab_Cobertura", new Color(0.35f, 0.40f, 0.47f));
        var velo = GetMaterialTransparente("Lab_ProteccionCerrada", new Color(0.45f, 0.15f, 0.75f, 0.30f));
        var sello = GetMaterialEmisivo("Lab_SelloDeTemplo", new Color(0.45f, 0.20f, 0.70f));
        var premio = GetMaterialEmisivo("Lab_PremioAmanecer", new Color(1f, 0.80f, 0.35f));

        CrearCubo("Suelo", raiz, c + new Vector3(0f, -0.25f, 0f), new Vector3(AnchoAmanecer, 0.5f, FondoAmanecer), suelo, CapaSuelo);
        CrearCubo("Muro norte", raiz, c + new Vector3(0f, 1.5f, mz), new Vector3(AnchoAmanecer, 3f, 0.5f), muro);
        CrearCubo("Muro oeste", raiz, c + new Vector3(-mx, 1.5f, 0f), new Vector3(0.5f, 3f, FondoAmanecer), muro);
        CrearCubo("Muro este", raiz, c + new Vector3(mx, 1.5f, 0f), new Vector3(0.5f, 3f, FondoAmanecer), muro);
        CrearTexto("Amanecer: Y y luego X A B Y\n(pestaña Magia para darlo o quitarlo)", raiz, c + new Vector3(0f, 4.5f, -3f));

        CrearGuardianProtegido(raiz, c + new Vector3(-6f, 0f, 4f), velo);
        CrearSelloDeTemplo(raiz, c + new Vector3(6f, 0f, 4f), sello, premio, muro);

        AnadirDestinoAlViaje("Amanecer (noroeste)", "Guardián protegido y sello de templo que abre Amanecer.", EntradaAmanecer, 0f);
    }

    /// Cápsula con vida que solo recibe daño con la protección abierta; el velo violeta se ve
    /// mientras está cerrada.
    private static void CrearGuardianProtegido(Transform raiz, Vector3 pie, Material velo)
    {
        var guardian = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        guardian.name = "Guardián protegido";
        guardian.transform.SetParent(raiz);
        guardian.transform.SetPositionAndRotation(pie + Vector3.up, Quaternion.identity);
        int enemyLyr = LayerMask.NameToLayer("Enemy");
        if (enemyLyr != -1) guardian.layer = enemyLyr;

        var aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        aura.name = "Protección (cerrada)";
        Object.DestroyImmediate(aura.GetComponent<Collider>());
        aura.transform.SetParent(guardian.transform, false);
        aura.transform.localScale = new Vector3(2f, 1.3f, 2f);
        aura.GetComponent<Renderer>().sharedMaterial = velo;

        var vida = guardian.AddComponent<Damageable>();
        var so = new SerializedObject(vida);
        so.FindProperty("maxHealth").floatValue = 600f;
        so.FindProperty("destroyOnDeath").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        var proteccion = guardian.AddComponent<ProteccionQueSeAbre>();
        so = new SerializedObject(proteccion);
        so.FindProperty("visualCerrada").objectReferenceValue = aura;
        so.FindProperty("alturaVfx").floatValue = 0.3f;
        so.ApplyModifiedPropertiesWithoutUndo();
        guardian.AddComponent<SoloDanoCuandoExpuesto>();

        CrearTexto("Guardián: solo le hace daño\nlo que llega con la protección abierta", raiz, pie + new Vector3(0f, 3.2f, 0f));
    }

    /// Muro de sello que se abre para siempre con Amanecer; detrás espera un premio.
    private static void CrearSelloDeTemplo(Transform raiz, Vector3 pie, Material sello, Material premio, Material muro)
    {
        var puerta = new GameObject("Sello de templo").transform;
        puerta.SetParent(raiz);
        puerta.SetPositionAndRotation(pie, Quaternion.identity);
        var losa = CrearCubo("Sello (cerrado)", puerta, pie + new Vector3(0f, 1.5f, 0f), new Vector3(4f, 3f, 0.5f), sello, estatico: false);

        var proteccion = puerta.gameObject.AddComponent<ProteccionQueSeAbre>();
        var so = new SerializedObject(proteccion);
        so.FindProperty("visualCerrada").objectReferenceValue = losa;
        so.FindProperty("quedaAbierta").boolValue = true;
        so.FindProperty("alturaVfx").floatValue = 1.5f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Hornacina detrás del sello, cerrada por los lados.
        CrearCubo("Hornacina (oeste)", raiz, pie + new Vector3(-2.25f, 1.5f, 2f), new Vector3(0.5f, 3f, 4f), muro);
        CrearCubo("Hornacina (este)", raiz, pie + new Vector3(2.25f, 1.5f, 2f), new Vector3(0.5f, 3f, 4f), muro);
        CrearCubo("Premio tras el sello", raiz, pie + new Vector3(0f, 0.5f, 2.5f), Vector3.one, premio, estatico: false);
        CrearTexto("Sello de templo: Amanecer lo abre\n(se queda abierto)", raiz, pie + new Vector3(0f, 3.8f, -0.5f));
    }

    /// Suma un destino al viaje rápido que ya montó CrearDestinos.
    private static void AnadirDestinoAlViaje(string nombre, string queHay, Vector3 posicion, float giro)
    {
        var viaje = Object.FindAnyObjectByType<ViajeDelLab>();
        if (viaje == null) { Debug.LogWarning("[CombatLab] No hay ViajeDelLab: el destino «" + nombre + "» no se añade."); return; }
        var punto = new GameObject("Destino — " + nombre).transform;
        punto.SetParent(viaje.transform);
        punto.SetPositionAndRotation(posicion, Quaternion.Euler(0f, giro, 0f));
        var so = new SerializedObject(viaje);
        var destinos = so.FindProperty("destinos");
        destinos.arraySize++;
        Destino(destinos.GetArrayElementAtIndex(destinos.arraySize - 1), nombre, queHay, punto);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
