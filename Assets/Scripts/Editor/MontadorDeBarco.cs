using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Monta un barco con todo lo que hace falta para que un barco se pueda navegar: los componentes
/// (<see cref="BarcoNavegable"/> y <see cref="ConductorDeBarco"/>), el puesto de conducción, el
/// punto de desembarque, el <see cref="Interactable"/> con su icono y la zona de interacción.
///
/// Sirve para los dos sitios donde hay barcos: el LAB (para probar) y el mundo (el barco de
/// Silas). Todo lo que se deduce del modelo (dónde está la proa, cuál es la cubierta) se calcula
/// leyendo su malla, porque los barcos del pack no vienen con la proa en +Z ni con el pivote en
/// el centro del casco.
/// </summary>
public static class MontadorDeBarco
{
    /// <summary>Instancia el barco en <paramref name="posicion"/> y lo deja listo para navegar.</summary>
    /// <param name="proaMundo">Dirección del mundo hacia la que debe mirar la proa.</param>
    /// <param name="profundidadDeQuilla">Metros que queda la parte más baja del casco bajo el agua.</param>
    /// <param name="musicaDelBarco">Clip que sonará mientras el jugador va a bordo (puede ser null).</param>
    /// <param name="posicionAmarre">Dónde está el punto de amarre (el sitio donde se baja a tierra).</param>
    public static GameObject Montar(string rutaPrefab, Transform padre, Vector3 posicion, Vector3 proaMundo,
                                     float profundidadDeQuilla, AudioClip musicaDelBarco, Vector3 posicionAmarre)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
        if (prefab == null)
        {
            Debug.LogWarning($"[Barco] No se encontró el prefab: {rutaPrefab}");
            return null;
        }

        var barco = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
        if (barco == null) return null;

        // El prefab viene marcado como estático y el barco se mueve: sin esto, Unity avisa de
        // "objeto estático movido" en cada frame.
        barco.isStatic = false;
        foreach (var hijo in barco.GetComponentsInChildren<Transform>()) hijo.gameObject.isStatic = false;

        barco.transform.position = posicion;
        Preparar(barco, padre, proaMundo, profundidadDeQuilla, musicaDelBarco, posicionAmarre);
        return barco;
    }

    /// <summary>Prepara un barco ya colocado para que se pueda navegar.</summary>
    public static GameObject Preparar(GameObject barco, Transform padre, Vector3 proaMundo, float profundidadDeQuilla,
                                      AudioClip musicaDelBarco, Vector3 posicionAmarre)
    {
        if (barco == null) return null;

        var datos = Analizar(barco);
        if (datos.cascoMax == datos.cascoMin)
        {
            Debug.LogWarning($"[Barco] El modelo '{barco.name}' no tiene malla: no se puede preparar.");
            return barco;
        }

        Debug.Log($"[Barco] {barco.name}: cubierta a {datos.timonLocal.y:F2} m (local), quilla a {datos.cascoMin.y:F2} m, " +
                  $"proa {datos.proaLocal}, eslora {Vector3.Dot(datos.cascoMax - datos.cascoMin, datos.proaLocal):F2} m, " +
                  $"manga {Vector3.Dot(datos.cascoMax - datos.cascoMin, datos.ejeAncho):F2} m. " +
                  $"Will {datos.timonLocal}, Estela {datos.sitioDeEstela} (ancho {datos.anchoDeEstela:F2} m), " +
                  $"Liam {datos.sitioDeLiam} (ancho {datos.anchoDeLiam:F2} m). " +
                  $"Música a bordo: {(musicaDelBarco != null ? musicaDelBarco.name : "NINGUNA")}.");

        // La proa mira hacia donde se le ha pedido.
        Vector3 proa = proaMundo;
        proa.y = 0f;
        if (proa.sqrMagnitude < 0.0001f) proa = Vector3.forward;
        proa.Normalize();
        barco.transform.rotation = Quaternion.FromToRotation(datos.proaLocal, proa);

        // Puesto de conducción: a la popa, donde está el timón.
        var puesto = new GameObject("Puesto de conducción");
        puesto.transform.SetParent(barco.transform, false);
        puesto.transform.localPosition = datos.timonLocal;

        // Los dos puestos de los compañeros, a babor y estribor, a la altura de la cubierta que
        // calcula el análisis para cada sitio.
        Vector3 sitioBabor = datos.sitioDeEstela;
        Vector3 sitioEstribor = datos.sitioDeLiam;

        // Los puestos miran a proa: el compañero se cuelga de ellos y hereda su orientación.
        Quaternion mirandoAProa = Quaternion.FromToRotation(Vector3.forward, datos.proaLocal);
        var puestoBabor = new GameObject("Puesto de babor");
        puestoBabor.transform.SetParent(barco.transform, false);
        puestoBabor.transform.localPosition = sitioBabor;
        puestoBabor.transform.localRotation = mirandoAProa;
        var puestoEstribor = new GameObject("Puesto de estribor");
        puestoEstribor.transform.SetParent(barco.transform, false);
        puestoEstribor.transform.localPosition = sitioEstribor;
        puestoEstribor.transform.localRotation = mirandoAProa;

        // Punto de desembarque: al costado del casco, a la altura del agua.
        Vector3 costado = Vector3.Cross(Vector3.up, datos.proaLocal).normalized;
        float semiancho = Mathf.Abs(Vector3.Dot(datos.cascoMax - datos.cascoMin, costado)) * 0.5f;
        var desembarque = new GameObject("Punto de desembarque");
        desembarque.transform.SetParent(barco.transform, false);
        desembarque.transform.localPosition = datos.timonLocal + costado * (semiancho + 0.5f);
        desembarque.transform.localPosition = new Vector3(desembarque.transform.localPosition.x,
            datos.cascoMin.y + profundidadDeQuilla, desembarque.transform.localPosition.z);

        // El Interactable va en la raíz del barco (no en el trigger): así, cuando el detector
        // comprueba que no haya un muro entre el jugador y el icono, el casco del propio barco
        // no lo tapa. Se añade antes que el BarcoNavegable para que lo encuentre al engancharse.
        var interactuable = barco.AddComponent<Interactable>();
        // Modo "Custom": el comportamiento (embarcar/desembarcar) lo lleva el BarcoNavegable con el
        // evento OnInteract; no hay diálogo ni popup que abrir.
        interactuable.SetMode(InteractableMode.Custom);

        var navegable = barco.AddComponent<BarcoNavegable>();
        navegable.Configurar(datos.proaLocal, puesto.transform, desembarque.transform,
                              profundidadDeQuilla, musicaDelBarco);
        // La tripulación (todos los del grupo menos quien conduce) y sus dos puestos.
        var tripulacion = barco.AddComponent<TripulacionDelBarco>();
        tripulacion.Configurar(puestoBabor.transform, puestoEstribor.transform, desembarque.transform);

        // Zona de interacción: el jugador tiene que poder subir desde cualquier punto del casco.
        // El centro va a la altura de la cubierta (no al centro de la caja del modelo, que
        // incluye el mástil y quedaría por encima de la cabeza del jugador).
        int capaInteractuable = LayerMask.NameToLayer("Interactable");
        var zonaSubida = new GameObject("Subir al barco");
        zonaSubida.transform.SetParent(barco.transform, false);
        Vector3 centroCasco = (datos.cascoMin + datos.cascoMax) * 0.5f;
        zonaSubida.transform.localPosition = new Vector3(centroCasco.x, datos.timonLocal.y - 1f, centroCasco.z);
        if (capaInteractuable >= 0) zonaSubida.layer = capaInteractuable;
        var disparo = zonaSubida.AddComponent<BoxCollider>();
        disparo.isTrigger = true;
        Vector3 tamanoCasco = datos.cascoMax - datos.cascoMin;
        // Alto generoso: el disparador tiene que alcanzar al jugador que nada al costado del
        // casco (que va por debajo de la cubierta) y al que está de pie encima.
        disparo.size = new Vector3(tamanoCasco.x + 1.2f, 6f, tamanoCasco.z + 1.2f);

        // Punto de amarre: el sitio (muelle, playa) donde el barco se arrima. El icono de
        // interactuar va aquí, no en el barco: aparece cuando el barco se acerca.
        var amarre = new GameObject("Punto de amarre");
        amarre.transform.SetParent(padre, false);
        amarre.transform.position = posicionAmarre;
        if (capaInteractuable >= 0) amarre.layer = capaInteractuable;
        var esferaAmarre = amarre.AddComponent<SphereCollider>();
        esferaAmarre.isTrigger = true;
        esferaAmarre.radius = 1.5f;
        var interactuableAmarre = amarre.AddComponent<Interactable>();
        var puntoDeAmarre = amarre.AddComponent<PuntoDeAmarre>();
        CrearHint(amarre.transform, Vector3.up * 1.2f, interactuableAmarre);
        navegable.ConfigurarAmarre(puntoDeAmarre);

        // Icono del botón de interactuar sobre la cubierta (misma estructura que los
        // interactuables del juego: Canvas en el mundo + BillboardUI + Image con el icono).
        CrearHint(barco.transform, datos.timonLocal + Vector3.up * 0.9f, interactuable);

        return barco;
    }

    /// <summary>Lo que se lee de la malla del barco para poder montarlo bien.</summary>
    public struct DatosDeBarco
    {
        public Vector3 proaLocal;    // dirección local de la proa (horizontal)
        public Vector3 ejeAncho;     // eje de la manga (horizontal, perpendicular a la proa)
        public Vector3 cascoMin;    // esquina mínima del casco, en espacio local
        public Vector3 cascoMax;    // esquina máxima del casco, en espacio local
        public Vector3 timonLocal;  // punto del timón (a la popa), en espacio local
        public Vector3 sitioDeEstela;
        public Vector3 sitioDeLiam;
        public float anchoDeEstela;   // medio ancho pisable en su lado
        public float anchoDeLiam;
    }

    /// <summary>
    /// Lee la malla del barco: qué eje es la eslora, hacia dónde está la proa (el extremo más
    /// estrecho del casco), dónde están la quilla y la cubierta, y dónde va el timón.
    /// </summary>
    public static DatosDeBarco Analizar(GameObject barco)
    {
        var datos = new DatosDeBarco { proaLocal = Vector3.forward };

        var vertices = new List<Vector3>();
        var triangulos = new List<int>();
        foreach (var mf in barco.GetComponentsInChildren<MeshFilter>())
        {
            var malla = mf.sharedMesh;
            if (malla == null) continue;
            var mtx = barco.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var vs = malla.vertices;
            int baseVertices = vertices.Count;
            for (int i = 0; i < vs.Length; i++) vertices.Add(mtx.MultiplyPoint3x4(vs[i]));
            var tris = malla.triangles;
            for (int i = 0; i < tris.Length; i++) triangulos.Add(baseVertices + tris[i]);
        }
        if (vertices.Count == 0) return datos;

        Bounds local = new Bounds(vertices[0], Vector3.zero);
        for (int i = 1; i < vertices.Count; i++) local.Encapsulate(vertices[i]);

        // Eje de eslora: el más largo de los dos horizontales.
        bool esloraEnX = local.size.x >= local.size.z;
        Vector3 ejeEs = esloraEnX ? Vector3.right : Vector3.forward;
        Vector3 ejeAn = esloraEnX ? Vector3.forward : Vector3.right;
        float largo = esloraEnX ? local.size.x : local.size.z;
        float minEje = esloraEnX ? local.min.x : local.min.z;

        // Perfil de anchura: la proa es el extremo más estrecho del casco.
        const int bins = 16;
        var anchos = new float[bins];
        for (int b = 0; b < bins; b++)
        {
            float a0 = minEje + largo * b / bins;
            float a1 = minEje + largo * (b + 1) / bins;
            float min = float.MaxValue, max = float.MinValue;
            foreach (var v in vertices)
            {
                float t = esloraEnX ? v.x : v.z;
                if (t < a0 || t >= a1) continue;
                float p = esloraEnX ? v.z : v.x;
                if (p < min) min = p;
                if (p > max) max = p;
            }
            anchos[b] = max > min ? max - min : 0f;
        }
        int b15 = Mathf.Clamp(Mathf.RoundToInt(bins * 0.15f), 0, bins - 1);
        int b85 = Mathf.Clamp(Mathf.RoundToInt(bins * 0.85f), 0, bins - 1);
        datos.proaLocal = anchos[b15] < anchos[b85] ? -ejeEs : ejeEs;

        // El ancho del casco en una estación concreta. En la popa el casco es bastante más
        // estrecho que en el centro: con la manga máxima del barco, los compañeros de al lado
        // quedaban metidos dentro de la borda.
        float AnchoDelCascoEn(Vector3 puntoLocal)
        {
            float t = (Vector3.Dot(puntoLocal, ejeEs) - minEje) / Mathf.Max(0.01f, largo);
            int b = Mathf.Clamp(Mathf.FloorToInt(t * bins), 0, bins - 1);
            return anchos[b];
        }

        // Casco: desde el primer sitio con anchura hasta el último (la verga no es casco).
        float anchoMax = 0f;
        for (int b = 0; b < bins; b++) anchoMax = Mathf.Max(anchoMax, anchos[b]);
        int primerBin = 0, ultimoBin = bins - 1;
        while (primerBin < bins && anchos[primerBin] < anchoMax * 0.2f) primerBin++;
        while (ultimoBin > primerBin && anchos[ultimoBin] < anchoMax * 0.2f) ultimoBin--;

        float desde = minEje + largo * primerBin / bins;
        float hasta = minEje + largo * (ultimoBin + 1) / bins;
        bool proaAlFinal = datos.proaLocal.x > 0f || datos.proaLocal.z > 0f;
        if (!proaAlFinal) { float t = desde; desde = hasta; hasta = t; }

        datos.cascoMin = local.min;
        datos.cascoMax = local.max;
        if (esloraEnX) { datos.cascoMin.x = desde; datos.cascoMax.x = hasta; }
        else { datos.cascoMin.z = desde; datos.cascoMax.z = hasta; }

        // Timón (Will): a la popa, en la línea de crujía, bien atrás (detrás del mástil).
        float eslora = Mathf.Abs(Vector3.Dot(datos.cascoMax - datos.cascoMin, datos.proaLocal));
        float distanciaDesdeLaPopa = Mathf.Clamp(eslora * 0.09f, 0.6f, 1f);
        Vector3 timon = (datos.cascoMin + datos.cascoMax) * 0.5f - datos.proaLocal * (eslora * 0.5f - distanciaDesdeLaPopa);
        timon.y = 0f;
        datos.ejeAncho = ejeAn;

        // Cubierta: la altura del suelo REAL, con un rayo contra el MeshCollider del propio barco,
        // por la LÍNEA DE CRUJÍA (donde no hay borda que estorbe).
        float topeDeCubierta = local.min.y + 3.8f;   // por encima están las vergas y los mástiles
        Vector3 crujia = timon - ejeAn * Vector3.Dot(timon, ejeAn);
        timon.y = AlturaDelSuelo(barco, crujia, topeDeCubierta);
        if (float.IsNegativeInfinity(timon.y))
            timon.y = AlturaDeCubierta(vertices, triangulos, timon, datos.proaLocal, ejeAn, local.min.y);
        datos.timonLocal = timon;

        // Los compañeros: a la MISMA altura que Will, un poco por delante de él (sin llegar al
        // mástil) y separados entre sí, uno a cada lado. Simétricos: los dos a la misma distancia
        // de la línea de crujía.
        Vector3 centro = timon + datos.proaLocal * 0.4f;
        float separacion = 0.8f;
        Vector3 sitioEstela = centro - ejeAn * separacion;
        Vector3 sitioLiam = centro + ejeAn * separacion;
        sitioEstela.y = timon.y;
        sitioLiam.y = timon.y;
        datos.sitioDeEstela = sitioEstela;
        datos.sitioDeLiam = sitioLiam;
        datos.anchoDeEstela = separacion;
        datos.anchoDeLiam = separacion;
        return datos;
    }

    private static readonly RaycastHit[] _hitsDelSuelo = new RaycastHit[16];

    /// <summary>
    /// La altura del suelo del barco en un punto: se lanza un rayo contra el MeshCollider del propio
    /// barco y se devuelve el PRIMER impacto horizontal que no sea verga ni mástil (de arriba abajo).
    ///
    /// Importa que sea el primero y no el más alto: en la popa, encima de la cubierta está la
    /// borda, y cogiendo la más alta la cubierta salía a la altura de la regola (los personajes
    /// quedaban dentro de la borda o colgando fuera). Con el primero, en el centro sale el suelo y
    /// en los bordes sale la parte de arriba de la borda, que no es pisable.
    /// </summary>
    private static float AlturaDelSuelo(GameObject barco, Vector3 puntoLocal, float alturaMaxima)
    {
        Physics.SyncTransforms();
        Vector3 origen = barco.transform.TransformPoint(puntoLocal + Vector3.up * 20f);
        int hits = Physics.RaycastNonAlloc(origen, Vector3.down, _hitsDelSuelo, 40f, ~0, QueryTriggerInteraction.Ignore);

        // Los impactos horizontales: gana el más cercano (el primero de arriba). Si se cogiera el
        // más alto, en la popa saldría la parte de arriba de la borda en vez del suelo.
        float mejor = float.MinValue;
        float mejorDistancia = float.MaxValue;
        for (int i = 0; i < hits; i++)
        {
            var col = _hitsDelSuelo[i].collider;
            if (col == null || !col.transform.IsChildOf(barco.transform)) continue;   // solo el barco
            if (_hitsDelSuelo[i].normal.y < 0.7f) continue;                        // tiene que ser un suelo
            float y = barco.transform.InverseTransformPoint(_hitsDelSuelo[i].point).y;
            if (y > alturaMaxima) continue;                                          // mástiles y vergas
            if (_hitsDelSuelo[i].distance < mejorDistancia)
            {
                mejorDistancia = _hitsDelSuelo[i].distance;
                mejor = y;
            }
        }
        return mejor;
    }

    /// <summary>
    /// El ancho de la cubierta que se puede pisar en una estación: se recorre la manga desde la
    /// línea de crujía hacia fuera y se mira dónde el suelo sigue a la misma altura que en el
    /// centro. Donde ya no (donde está la borda) no se puede estar de pie.
    /// </summary>
    private static float AnchoPisable(GameObject barco, Vector3 centro, Vector3 ejeAn,
                                      float alturaCubierta, float alturaMaxima)
    {
        float medio = 0f;
        for (float d = 0.1f; d <= 2.4f; d += 0.1f)
        {
            float y = AlturaDelSuelo(barco, centro + ejeAn * d, alturaMaxima);
            if (float.IsNegativeInfinity(y)) break;              // aquí no hay suelo
            if (Mathf.Abs(y - alturaCubierta) > 0.3f) break;    // ya es la borda, no la cubierta
            medio = d;
        }
        return medio;
    }

    /// <summary>
    /// Altura local de la cubierta por el método de área (el de respaldo, cuando el rayo contra el
    /// casco no encuentra suelo): la altura de la superficie horizontal con más superficie (área)
    /// alrededor del punto. La cubierta es un suelo ancho; los mástiles, las vergas y las bordas
    /// son tiras estrechas.
    /// </summary>
    private static float AlturaDeCubierta(List<Vector3> vertices, List<int> triangulos, Vector3 timon,
                                           Vector3 proa, Vector3 ejeAn, float quilla)
    {
        const float paso = 0.05f;
        var areaPorAltura = new Dictionary<int, float>();

        for (int i = 0; i + 2 < triangulos.Count; i += 3)
        {
            Vector3 a = vertices[triangulos[i]];
            Vector3 b = vertices[triangulos[i + 1]];
            Vector3 c = vertices[triangulos[i + 2]];

            Vector3 normal = Vector3.Cross(b - a, c - a);
            float area = normal.magnitude * 0.5f;
            if (area < 0.0005f) continue;
            normal /= normal.magnitude;
            if (normal.y < 0.5f) continue;   // no es un suelo: es un mamparo, un mástil, una borda

            Vector3 centro = (a + b + c) * (1f / 3f);
            if (Mathf.Abs(Vector3.Dot(centro - timon, proa)) > 1.8f) continue;
            if (Mathf.Abs(Vector3.Dot(centro - timon, ejeAn)) > 1.3f) continue;

            int clave = Mathf.RoundToInt(centro.y / paso);
            areaPorAltura[clave] = areaPorAltura.TryGetValue(clave, out float previa) ? previa + area : area;
        }

        int mejorClave = 0;
        float mejorArea = 0f;
        foreach (var kv in areaPorAltura)
        {
            if (kv.Value > mejorArea)
            {
                mejorArea = kv.Value;
                mejorClave = kv.Key;
            }
        }

        if (mejorArea <= 0f) return quilla + 1.5f;
        return mejorClave * paso;
    }

    /// <summary>
    /// Hint de interactuar: el icono del botón que aparece sobre el objeto. Es la misma
    /// estructura que llevan los interactuables del juego (Canvas en el mundo + BillboardUI +
    /// Image con el icono), porque el <see cref="Interactable"/> lo enciende, lo escala y lo
    /// apaga por su cuenta; el sprite correcto según el mando lo pone el icon set.
    /// </summary>
    public static void CrearHint(Transform padre, Vector3 posicionLocal, Interactable interactuable)
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(padre, false);
        canvasGo.transform.localPosition = posicionLocal;
        int capaHint = LayerMask.NameToLayer("InteractHint");
        if (capaHint >= 0) canvasGo.layer = capaHint;

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(100f, 100f);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        var escalador = canvasGo.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        canvasGo.AddComponent<BillboardUI>();

        var iconoGo = new GameObject("Icono", typeof(RectTransform));
        iconoGo.transform.SetParent(canvasGo.transform, false);
        var iconoRect = iconoGo.GetComponent<RectTransform>();
        iconoRect.sizeDelta = new Vector2(1f, 1f);
        var imagen = iconoGo.AddComponent<Image>();
        imagen.preserveAspect = true;
        imagen.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/InputGlyphs/Xbox/south.png");

        Asignar(interactuable, "hint", canvasGo);

        var iconos = AssetDatabase.LoadAssetAtPath<Core.InputGlyphs.InteractionHintIconSet>("Assets/_UI/InteractionHintIconSet.asset");
        if (iconos != null) Asignar(interactuable, "iconSet", iconos);
    }

    private static void Asignar(Object componente, string campo, object valor)
    {
        if (componente == null)
        {
            Debug.LogWarning($"[Barco] No se puede asignar '{campo}': el componente es null.");
            return;
        }
        var so = new SerializedObject(componente);
        var p = so.FindProperty(campo);
        if (p == null)
        {
            Debug.LogWarning($"[Barco] {componente.GetType().Name} no tiene el campo '{campo}'.");
            return;
        }
        switch (valor)
        {
            case int i: p.intValue = i; break;
            case float f: p.floatValue = f; break;
            case bool b: p.boolValue = b; break;
            case Vector3 v3: p.vector3Value = v3; break;
            case AudioClip clip: p.objectReferenceValue = clip; break;
            case Object o: p.objectReferenceValue = o; break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
