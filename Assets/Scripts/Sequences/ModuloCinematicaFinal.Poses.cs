using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Poses de suelo (sentado en el suelo, tumbado, dormido, de rodillas): las animaciones dejan el
/// cuerpo por debajo de la raíz y, puestas a ras de suelo, los personajes se metían en la hierba.
/// Aquí, después de la animación, se sube a cada uno lo justo para que el punto más bajo de su
/// malla (la malla de verdad, deformada por la animación) quede encima del suelo, y a los
/// sentados se les levanta la cabeza (las animaciones de sentado la llevan gacha).
/// También se mantienen cerrados los ojos de quien está sin sentido o dormido. Ver INC-866.
public partial class ModuloCinematicaFinal
{
    private static readonly string[] EstadosDeSuelo =
    {
        "Knockdown01_Ground", "Knockdown01_StandUp", "SitGround_Begin", "SitGround_Loop", "SitGround_Exit",
        "SitLow_Begin", "SitLow_Loop", "SitLow_Exit", "Sleeping_NoWeapon", "Die01Stay_NoWeapon", "Die01_NoWeapon",
        "Cry01", "Cry01_Loop",
    };
    private static readonly string[] EstadosSentado = { "SitGround_Loop", "SitLow_Loop" };
    /// Tumbados que se quedan quietos del todo. Sus clips son bucles y la pose sostenida de los NPC
    /// solo congela los que no lo son: seguían en bucle (las piernas se cerraban y abrían) y, al
    /// acabar cada vuelta, `Knockdown01_Ground` salía sola por su transición a
    /// `Idle_Normal_NoWeapon` (un instante de pie: el parpadeo) y la pose sostenida la volvía a meter.
    private static readonly string[] EstadosTumbadosQuietos = { "Knockdown01_Ground", "Die01Stay_NoWeapon" };

    /// Lo que se deja entre el punto más bajo del cuerpo y el suelo.
    private const float HolguraSobreElSuelo = 0.01f;
    /// Nunca se sube más que esto (si la medida diera más, se queda en este tope: no salta).
    private const float SubidaMaxima = 0.45f;
    /// Cuánto se endereza la cabeza de los sentados (0 = nada, 1 = vertical del todo).
    private const float CabezaAlFrente = 0.7f;
    /// Cada cuántos fotogramas se vuelve a medir la malla (las poses de suelo cambian despacio).
    private const int FotogramasEntreMedidas = 4;
    /// De cada malla se mira uno de cada tantos vértices: sobra para encontrar el punto más bajo.
    private const int SaltoDeVertices = 3;
    /// Rapidez con la que la subida alcanza la medida (por segundo).
    private const float RapidezDeLaSubida = 14f;

    private sealed class PoseDeSuelo
    {
        public Transform raiz;
        public Animator animador;
        public SkinnedMeshRenderer[] mallas;
        public Transform cuello, cabeza;
        public float subida;          // lo que lleva subido ahora
        public float objetivo;        // lo que dijo la última medida
        public Vector3 puesta;        // dónde se le dejó (con la subida)
        public bool hayPuesta;
        public int proximaMedida;
        public bool congelado;        // se le paró el animador aquí
        public NPCSimpleAnimator npc;
    }

    /// Fotograma de la pose tumbada en el que se quedan (casi al final del clip, como el jugador).
    private const float TiempoDeTumbado = 0.95f;
    private static readonly int HashDeReposo = Animator.StringToHash("Idle_Normal_NoWeapon");

    private readonly List<PoseDeSuelo> _posesDeSuelo = new();
    private int[] _hashesDeSuelo, _hashesSentado, _hashesQuietos;
    private readonly RaycastHit[] _impactos = new RaycastHit[8];
    private Mesh _mallaHorneada;
    private readonly List<Vector3> _vertices = new();
#if UNITY_EDITOR
    private readonly Dictionary<string, float> _desfasesVistos = new();
#endif

    private void PrepararPosesDeSuelo()
    {
        _posesDeSuelo.Clear();
        if (_hashesDeSuelo == null)
        {
            _hashesDeSuelo = new int[EstadosDeSuelo.Length];
            for (int i = 0; i < EstadosDeSuelo.Length; i++) _hashesDeSuelo[i] = Animator.StringToHash(EstadosDeSuelo[i]);
            _hashesSentado = new int[EstadosSentado.Length];
            for (int i = 0; i < EstadosSentado.Length; i++) _hashesSentado[i] = Animator.StringToHash(EstadosSentado[i]);
            _hashesQuietos = new int[EstadosTumbadosQuietos.Length];
            for (int i = 0; i < EstadosTumbadosQuietos.Length; i++) _hashesQuietos[i] = Animator.StringToHash(EstadosTumbadosQuietos[i]);
        }
        foreach (var alias in new[] { "will", "estela", "liam", "tobias" })
        {
            var raiz = ResolverActor(alias, null);
            if (raiz == null) continue;
            var anim = AnimadorHumano(raiz);
            if (anim == null) continue;
            _posesDeSuelo.Add(new PoseDeSuelo
            {
                raiz = raiz,
                animador = anim,
                mallas = raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true),
                cuello = anim.GetBoneTransform(HumanBodyBones.Neck),
                cabeza = anim.GetBoneTransform(HumanBodyBones.Head),
                npc = raiz.GetComponentInChildren<NPCSimpleAnimator>(true),
            });
        }
        Lanzar(Co_CongelarTumbados());
    }

    /// El animador de los NPC vuelve a velocidad 1 en cada Update (NPCSimpleAnimator, cuando están
    /// quietos), así que la velocidad 0 se pone después de los Update y antes de que se evalúe la
    /// animación: en una corrutina, cada fotograma.
    private IEnumerator Co_CongelarTumbados()
    {
        while (_enMarcha)
        {
            yield return null;
            for (int i = 0; i < _posesDeSuelo.Count; i++)
            {
                var p = _posesDeSuelo[i];
                if (p.npc != null && p.raiz != null && p.animador != null && p.raiz.gameObject.activeInHierarchy) CongelarSiEstaTumbado(p);
            }
        }
    }

    /// Después de la animación (LateUpdate). La subida no se acumula: si nadie ha movido la raíz
    /// desde el fotograma anterior, se parte de donde estaba sin la subida.
    private void ApoyarPosesDeSuelo()
    {
        if (_hashesDeSuelo == null) return;
        for (int i = 0; i < _posesDeSuelo.Count; i++)
        {
            var p = _posesDeSuelo[i];
            if (p.raiz == null || p.animador == null || !p.raiz.gameObject.activeInHierarchy) continue;

            Vector3 actual = p.raiz.position;
            bool sinTocar = p.hayPuesta && (actual - p.puesta).sqrMagnitude < 1e-8f;
            Vector3 base_ = sinTocar ? p.puesta - Vector3.up * p.subida : actual;

            if (!EstadoActual(p.animador, _hashesDeSuelo, out _, out int hash))
            {
                if (sinTocar) p.raiz.position = base_;
                p.subida = 0f;
                p.objetivo = 0f;
                p.hayPuesta = false;
                p.proximaMedida = 0;
                continue;
            }

            if (Time.frameCount >= p.proximaMedida)
            {
                p.proximaMedida = Time.frameCount + FotogramasEntreMedidas;
                // La malla está donde la dejó la animación con la raíz en «actual»: se mide su punto
                // más bajo y se le quita lo que la raíz lleva subida.
                float minimo = PuntoMasBajo(p);
                if (minimo < float.MaxValue)
                {
                    minimo -= actual.y - base_.y;
                    float suelo = SueloBajo(base_, base_.y);
                    p.objetivo = Mathf.Clamp(suelo + HolguraSobreElSuelo - minimo, 0f, SubidaMaxima);
#if UNITY_EDITOR
                    ApuntarDesfase(p, hash, suelo - minimo);
#endif
                }
            }

            float k = 1f - Mathf.Exp(-RapidezDeLaSubida * Time.deltaTime);
            p.subida = p.hayPuesta ? Mathf.Lerp(p.subida, p.objetivo, k) : p.objetivo;
            p.puesta = base_ + Vector3.up * p.subida;
            p.raiz.position = p.puesta;
            p.hayPuesta = true;
            if (EsSentado(hash)) EnderezarCabeza(p);
        }
    }

    /// Con el cuerpo ya en su pose de tumbado (sin transición de por medio) se para el animador; al
    /// pasar a otra pose, el propio NPCSimpleAnimator le devuelve la velocidad y aquí se olvida.
    private void CongelarSiEstaTumbado(PoseDeSuelo p)
    {
        var a = p.animador;
        int actual = a.GetCurrentAnimatorStateInfo(0).shortNameHash;
        bool quieto = false;
        for (int i = 0; i < _hashesQuietos.Length; i++) if (_hashesQuietos[i] == actual) quieto = true;
        if (!quieto)
        {
            if (p.congelado && a.speed == 0f) a.speed = 1f;
            p.congelado = false;
            return;
        }
        if (a.IsInTransition(0))
        {
            // Una transición que ha pedido el guion (incorporarse, otra pose, el reposo) se deja
            // correr; la que el controlador hace solo al acabar el clip, no.
            bool salidaSola = a.GetNextAnimatorStateInfo(0).shortNameHash == HashDeReposo && p.npc.SosteniendoPose;
            if (!salidaSola) return;
        }
        a.Play(actual, 0, TiempoDeTumbado);
        a.speed = 0f;
        p.congelado = true;
    }

    /// Al acabar: nadie se queda con el animador parado.
    private void SoltarPosesDeSuelo()
    {
        foreach (var p in _posesDeSuelo)
            if (p.congelado && p.animador != null && p.animador.speed == 0f) p.animador.speed = 1f;
        _posesDeSuelo.Clear();
    }

    /// La altura del vértice más bajo de las mallas visibles, con la pose de este fotograma.
    private float PuntoMasBajo(PoseDeSuelo p)
    {
        if (_mallaHorneada == null) _mallaHorneada = new Mesh { name = "Pose medida" };
        float minimo = float.MaxValue;
        for (int m = 0; m < p.mallas.Length; m++)
        {
            var malla = p.mallas[m];
            if (malla == null || !malla.enabled || !malla.gameObject.activeInHierarchy || malla.sharedMesh == null) continue;
            malla.BakeMesh(_mallaHorneada, true);
            _mallaHorneada.GetVertices(_vertices);
            // Con la escala ya horneada, solo falta la posición y el giro de la malla.
            var aMundo = Matrix4x4.TRS(malla.transform.position, malla.transform.rotation, Vector3.one);
            for (int v = 0; v < _vertices.Count; v += SaltoDeVertices)
            {
                float y = aMundo.MultiplyPoint3x4(_vertices[v]).y;
                if (y < minimo) minimo = y;
            }
        }
        return minimo;
    }

    private bool EstadoActual(Animator animador, int[] hashes, out int capa, out int hash)
    {
        for (capa = 0; capa < animador.layerCount; capa++)
        {
            if (capa > 0 && animador.GetLayerWeight(capa) < 0.5f) continue;
            var info = animador.GetCurrentAnimatorStateInfo(capa);
            for (int i = 0; i < hashes.Length; i++)
                if (info.shortNameHash == hashes[i]) { hash = hashes[i]; return true; }
        }
        hash = 0;
        return false;
    }

    private bool EsSentado(int hash)
    {
        for (int i = 0; i < _hashesSentado.Length; i++) if (_hashesSentado[i] == hash) return true;
        return false;
    }

    /// La altura del suelo bajo la raíz: lo más alto que encuentra un rayo hacia abajo (desde un
    /// poco por encima de la raíz sin subir) que no sea de un personaje. Si no hay nada, la raíz.
    private float SueloBajo(Vector3 punto, float respaldo)
    {
        int n = Physics.RaycastNonAlloc(punto + Vector3.up * 0.6f, Vector3.down, _impactos, 1.2f, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.MinValue;
        for (int i = 0; i < n; i++)
        {
            var c = _impactos[i].collider;
            if (c == null || EsDeUnActor(c.transform)) continue;
            if (_impactos[i].point.y > mejor) mejor = _impactos[i].point.y;
        }
        return mejor > float.MinValue ? mejor : respaldo;
    }

    /// La cabeza de un sentado mira al frente: se lleva el eje cuello → cabeza hacia la vertical.
    private static void EnderezarCabeza(PoseDeSuelo p)
    {
        if (p.cuello == null || p.cabeza == null) return;
        Vector3 arriba = p.cabeza.position - p.cuello.position;
        if (arriba.sqrMagnitude < 1e-6f) return;
        Quaternion giro = Quaternion.FromToRotation(arriba.normalized, Vector3.up);
        Quaternion parte = Quaternion.Slerp(Quaternion.identity, giro, CabezaAlFrente * 0.5f);
        p.cuello.rotation = parte * p.cuello.rotation;
        p.cabeza.rotation = parte * p.cabeza.rotation;
    }

    // ── Ojos cerrados ────────────────────────────────────────────────────────────────────────
    // Las caras vuelven solas a los ojos abiertos (el parpadeo, las reacciones de quien escucha,
    // el reposo tras una emoción). A quien está sin sentido o dormido se le vuelven a cerrar cada
    // fotograma, después de todo eso, hasta que el guion lo despierta.

    private readonly List<(Transform quien, NPCEmotionController cara)> _conLosOjosCerrados = new();

    private void MantenerOjosCerrados(Transform quien, bool cerrados)
    {
        if (quien == null) return;
        int i = _conLosOjosCerrados.FindIndex(o => o.quien == quien);
        if (cerrados)
        {
            var cara = Game.NPC.Common.EmotionControllerResolver.Resolve(quien.gameObject);
            if (cara == null) return;
            if (i < 0) _conLosOjosCerrados.Add((quien, cara));
            cara.AplicarOjos(OjosCerrados);
        }
        else if (i >= 0)
        {
            _conLosOjosCerrados.RemoveAt(i);
            PorteoDeCompanero.RestaurarOjos(quien);
        }
    }

    private const string OjosCerrados = "Eye09";

    private void CerrarOjosRetenidos()
    {
        for (int i = _conLosOjosCerrados.Count - 1; i >= 0; i--)
        {
            var cara = _conLosOjosCerrados[i].cara;
            if (cara == null) { _conLosOjosCerrados.RemoveAt(i); continue; }
            cara.AplicarOjos(OjosCerrados);
        }
    }

#if UNITY_EDITOR
    /// Cuánto mete cada pose al personaje en el suelo (lo máximo visto, sin tope); se escribe al
    /// terminar en Logs/Rodaje/poses_de_suelo.txt para llevar la misma corrección al juego.
    private void ApuntarDesfase(PoseDeSuelo p, int hash, float hundido)
    {
        string estado = null;
        for (int i = 0; i < _hashesDeSuelo.Length; i++) if (_hashesDeSuelo[i] == hash) estado = EstadosDeSuelo[i];
        if (estado == null) return;
        string clave = p.raiz.name + "\t" + estado;
        if (!_desfasesVistos.TryGetValue(clave, out float antes) || hundido > antes) _desfasesVistos[clave] = hundido;
    }

    private void EscribirDesfases()
    {
        if (_desfasesVistos.Count == 0) return;
        try
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in _desfasesVistos)
                sb.Append($"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{kv.Key}\thundido {kv.Value:0.000} m\n");
            string ruta = System.IO.Path.Combine(Application.dataPath, "..", "Logs", "Rodaje", "poses_de_suelo.txt");
            System.IO.File.AppendAllText(ruta, sb.ToString());
        }
        catch (System.Exception) { }
        _desfasesVistos.Clear();
    }
#endif
}
