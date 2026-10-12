using UnityEditor;
using UnityEngine;

/// Terreno de los jefes en la arena de la zona de jefes del LAB (INC-723): el barro del arroyo
/// donde se hunde el Gólem y cuatro torres con almena donde se posa el Demonio 2 en la guerra en
/// el aire, con escalones para subir a ellas. Las torres van con la geometría (entran en el
/// NavMesh como obstáculos); el barro, el arroyo y las almenas, en la raíz de la zona (sin
/// colisión: el barro se pisa).
public static partial class CombatLabBuilder
{
    private const float RadioTorresJefes = 17f;
    private const float AltoTorreJefes = 6f;
    private const float RadioBarroJefes = 3.5f;
    private const string VfxChapoteo = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Stones hit.prefab";

    private static void CrearTerrenoDeJefes(Transform geometria, Transform raiz, Vector3 centro, Material muro)
    {
        centro.y = 0f;

        // Barro del arroyo (Gólem): charco plano junto a una franja de agua, a un lado del centro.
        var barroMat = GetMaterial("Lab_Barro", new Color(0.33f, 0.24f, 0.15f));
        var aguaMat = GetMaterial("Lab_Arroyo", new Color(0.25f, 0.45f, 0.65f));
        Vector3 posBarro = centro + new Vector3(0f, 0f, -9f); // en el eje: lejos de los escalones de las torres
        var barro = Disco("Barro del arroyo", raiz, posBarro + Vector3.up * 0.03f, RadioBarroJefes, barroMat);
        var zona = barro.AddComponent<ZonaDeBarro>();
        var soBarro = new SerializedObject(zona);
        soBarro.FindProperty("radio").floatValue = RadioBarroJefes;
        soBarro.FindProperty("vfxChapoteo").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(VfxChapoteo);
        soBarro.ApplyModifiedPropertiesWithoutUndo();
        var arroyo = CrearCubo("Arroyo", raiz, posBarro + new Vector3(RadioBarroJefes + 1f, 0.02f, 0f), new Vector3(2f, 0.02f, 20f), aguaMat, estatico: false);
        Object.DestroyImmediate(arroyo.GetComponent<Collider>());
        CrearTexto("Barro (Gólem)", raiz, posBarro + new Vector3(0f, 3f, -RadioBarroJefes));

        // Torres con almena (Demonio 2), con escalones hacia el centro de la arena.
        for (int i = 0; i < 4; i++)
        {
            float angulo = 45f + 90f * i;
            Vector3 dir = Quaternion.Euler(0f, angulo, 0f) * Vector3.forward;
            Vector3 pie = centro + dir * RadioTorresJefes;
            string nombre = $"Torre {i + 1}";

            var torre = new GameObject(nombre).transform;
            torre.SetParent(geometria);
            CrearCubo("Cuerpo", torre, pie + Vector3.up * (AltoTorreJefes * 0.5f), new Vector3(3f, AltoTorreJefes, 3f), muro);
            for (int m = 0; m < 4; m++)
            {
                Vector3 esquina = new Vector3(m < 2 ? -1.1f : 1.1f, 0f, m % 2 == 0 ? -1.1f : 1.1f);
                CrearCubo($"Almena {m + 1}", torre, pie + esquina + Vector3.up * (AltoTorreJefes + 0.5f), new Vector3(0.8f, 1f, 0.8f), muro);
            }
            for (int e = 1; e <= 4; e++)
            {
                float alto = AltoTorreJefes * e / 5f;                 // 1,2 · 2,4 · 3,6 · 4,8 m
                float distancia = RadioTorresJefes - 2.5f - 2f * (4 - e);
                Vector3 pos = centro + dir * distancia;
                CrearCubo($"Escalón {e}", torre, pos + Vector3.up * (alto * 0.5f), new Vector3(2f, alto, 2f), muro);
            }

            var almena = Punto($"Almena del Demonio — {nombre}", raiz, pie + Vector3.up * (AltoTorreJefes + 2.5f));
            almena.gameObject.AddComponent<AlmenaDelDemonio>();
        }
        CrearTexto("Almenas (Demonio 2)", raiz, centro + new Vector3(0f, 7f, RadioTorresJefes));
    }

    /// Disco plano sin colisión (un cilindro aplastado).
    private static GameObject Disco(string nombre, Transform padre, Vector3 pos, float radio, Material mat)
    {
        var disco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disco.name = nombre;
        disco.transform.SetParent(padre);
        disco.transform.SetPositionAndRotation(pos, Quaternion.identity);
        disco.transform.localScale = new Vector3(radio * 2f, 0.02f, radio * 2f);
        Object.DestroyImmediate(disco.GetComponent<Collider>());
        disco.GetComponent<Renderer>().sharedMaterial = mat;
        return disco;
    }
}
