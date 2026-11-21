using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC;
using Game.NPC.Modules;

/// <summary>
/// Zona del LAB para pelear contra personas (y alguna criatura) en grupo: cada placa de la entrada
/// empieza una oleada (dos, tres, muchos o mezcla). Cada luchador es una plantilla apagada de la
/// escena, con su config de combate y su escudo elemental ya puestos, que se clona al empezar; al
/// pisar otra placa se retira la oleada anterior. Al vencer a todos se cura el grupo. Solo para
/// escenas de prueba. Ver INC-1357.
/// </summary>
public sealed class PeleaContraNpcsDelLab : MonoBehaviour
{
    [System.Serializable]
    public sealed class Oleada
    {
        public string nombre = "Oleada";
        [Tooltip("Plantillas apagadas de la escena: cada una sale una vez (repetida, sale varias).")]
        public GameObject[] luchadores = new GameObject[0];
    }

    [SerializeField] private Oleada[] oleadas = new Oleada[0];
    [Tooltip("Centro del corro donde aparecen.")]
    [SerializeField] private Transform centro;
    [SerializeField, Min(1f)] private float radio = 7f;
    [Tooltip("Segundos entre que aparecen y que empiezan a pelear.")]
    [SerializeField, Min(0f)] private float espera = 1.5f;
    [Tooltip("Rótulo de estado de la zona (opcional).")]
    [SerializeField] private TextMesh rotulo;

    private readonly List<GameObject> _actuales = new();
    private Coroutine _co;
    private int _oleada = -1;

    public int Oleadas => oleadas != null ? oleadas.Length : 0;
    public string NombreDe(int i) => oleadas != null && i >= 0 && i < oleadas.Length ? oleadas[i].nombre : "";

    /// <summary>Retira lo que hubiera y empieza la oleada 'i'.</summary>
    public void Empezar(int i)
    {
        if (oleadas == null || i < 0 || i >= oleadas.Length || centro == null) return;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Co_Oleada(i));
    }

    /// <summary>Quita la oleada en curso.</summary>
    public void Retirar()
    {
        for (int i = 0; i < _actuales.Count; i++)
        {
            var go = _actuales[i];
            if (go == null) continue;
            ActiveCombatRegistry.UnregisterNPC(go);
            Destroy(go);
        }
        _actuales.Clear();
        _oleada = -1;
    }

    private void OnDisable()
    {
        if (_co != null) { StopCoroutine(_co); _co = null; }
        Retirar();
    }

    private IEnumerator Co_Oleada(int i)
    {
        Retirar();
        _oleada = i;
        var lista = oleadas[i].luchadores;
        var jugador = PlayerService.PlayerTransform;
        int n = lista != null ? lista.Length : 0;
        for (int k = 0; k < n; k++)
        {
            var plantilla = lista[k];
            if (plantilla == null) continue;
            float angulo = (k / (float)Mathf.Max(1, n)) * Mathf.PI * 2f;
            Vector3 punto = centro.position + new Vector3(Mathf.Sin(angulo), 0f, Mathf.Cos(angulo)) * radio;
            if (NavMesh.SamplePosition(punto, out var nav, 3f, NavMesh.AllAreas)) punto = nav.position;
            Vector3 mirar = (jugador != null ? jugador.position : centro.position) - punto;
            mirar.y = 0f;
            Quaternion giro = mirar.sqrMagnitude > 0.01f ? Quaternion.LookRotation(mirar) : Quaternion.identity;
            var clon = Instantiate(plantilla, punto, giro, transform);
            clon.name = plantilla.name.Replace("Plantilla — ", "");
            clon.SetActive(true);
            _actuales.Add(clon);
        }
        Decir($"{oleadas[i].nombre}: ¡en guardia!");

        if (espera > 0f) yield return new WaitForSeconds(espera);
        jugador = PlayerService.PlayerTransform;
        if (jugador != null)
            for (int k = 0; k < _actuales.Count; k++)
                if (_actuales[k] != null && _actuales[k].TryGetComponent(out NPCBehaviourManagerV2 npc))
                    npc.ForceEnterCombat(jugador);
        Decir($"{oleadas[i].nombre}: quedan {Vivos()}");

        var pausa = new WaitForSeconds(0.5f);
        int antes = -1;
        while (true)
        {
            yield return pausa;
            int vivos = Vivos();
            if (vivos == 0) break;
            if (vivos != antes) { antes = vivos; Decir($"{oleadas[i].nombre}: quedan {vivos}"); }
        }

        SaludDelGrupo.Instance?.CurarATodos();
        Decir($"{oleadas[i].nombre}: ¡ganada! Pisa una placa para otra");
        _co = null;
    }

    private int Vivos()
    {
        int n = 0;
        for (int i = 0; i < _actuales.Count; i++)
            if (SigueEnPie(_actuales[i])) n++;
        return n;
    }

    private static bool SigueEnPie(GameObject go)
    {
        if (go == null || !go.activeInHierarchy) return false;
        if (go.TryGetComponent(out NPCCombatLifecycleHandler vida) && vida.IsDefeatedAndInactive) return false;
        var dano = go.GetComponentInChildren<Damageable>();
        return dano == null || dano.IsAlive;
    }

    private void Decir(string texto)
    {
        if (rotulo != null) rotulo.text = texto;
    }
}
