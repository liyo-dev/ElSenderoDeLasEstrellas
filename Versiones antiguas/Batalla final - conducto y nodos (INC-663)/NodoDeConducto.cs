using System;
using System.Collections.Generic;
using Game.NPC;
using UnityEngine;

/// Un nodo de los conductos de sombra que alimentan al Mago Oscuro (fase 3). Se rompe a golpes
/// y, sobre todo, lo drenan los aliados que estén cerca: por eso compensa separar al grupo
/// (modo Libre del equipo, o «quédate aquí» a cada compañero) y dejar a cada uno junto a un nodo.
/// Mientras está activo se ve un rayo del nodo al altar, y uno de cada aliado que lo drena.
/// Ver INC-509.
[RequireComponent(typeof(Damageable))]
public sealed class NodoDeConducto : MonoBehaviour
{
    [SerializeField] private float vida = 120f;
    [Tooltip("Metros alrededor del nodo en los que un aliado lo drena.")]
    [SerializeField] private float radioDrenaje = 6f;
    [SerializeField] private float drenajePorSegundo = 14f;
    [SerializeField] private GameObject visual;
    [Tooltip("Rayo del nodo al altar.")]
    [SerializeField] private LineRenderer conducto;
    [Tooltip("Plantilla del rayo de un aliado que drena (se clona uno por aliado).")]
    [SerializeField] private LineRenderer plantillaRayoAliado;
    [SerializeField] private GameObject vfxRotura;

    private Damageable _vida;
    private Collider _col;
    private Transform _altar;
    private readonly Dictionary<NPCPartyMember, LineRenderer> _rayos = new();

    public bool Activo { get; private set; }
    public bool Roto { get; private set; }
    public float VidaNormalizada => _vida != null && _vida.Max > 0f ? _vida.Current / _vida.Max : 0f;
    public event Action<NodoDeConducto> AlRomperse;

    void Awake()
    {
        _vida = GetComponent<Damageable>();
        _vida.SetDestroyOnDeath(false);
        _vida.OnDied += AlMorir;
        _col = GetComponent<Collider>();
        if (plantillaRayoAliado) plantillaRayoAliado.gameObject.SetActive(false);
        Apagar();
    }

    void OnDestroy() { if (_vida != null) _vida.OnDied -= AlMorir; }

    public void Activar(Transform altar)
    {
        _altar = altar;
        _vida.Revive(vida);
        _vida.SetMaxAndCurrent(vida, vida);
        Activo = true;
        Roto = false;
        if (visual) visual.SetActive(true);
        if (_col) _col.enabled = true;
        if (conducto) conducto.enabled = true;
    }

    public void Apagar()
    {
        Activo = false;
        if (visual) visual.SetActive(false);
        if (_col) _col.enabled = false;
        if (conducto) conducto.enabled = false;
        foreach (var r in _rayos.Values) if (r) r.enabled = false;
    }

    private void AlMorir()
    {
        if (!Activo) return;
        if (vfxRotura && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxRotura, transform.position + Vector3.up, Quaternion.identity, 2f);
        Apagar();
        Roto = true;
        AlRomperse?.Invoke(this);
    }

    void Update()
    {
        if (!Activo) return;

        if (conducto && _altar)
        {
            conducto.SetPosition(0, transform.position + Vector3.up * 1f);
            conducto.SetPosition(1, _altar.position + Vector3.up * 1.5f);
        }

        if (!PlayerParty.HasInstance) return;
        var miembros = PlayerParty.Instance.Members;
        for (int i = 0; i < miembros.Count && Activo; i++)
        {
            var m = miembros[i];
            if (m == null || !m.isActiveAndEnabled) continue;

            Vector3 d = m.transform.position - transform.position;
            d.y = 0f;
            bool drena = d.sqrMagnitude <= radioDrenaje * radioDrenaje;
            var rayo = Rayo(m);
            if (rayo)
            {
                rayo.enabled = drena;
                if (drena)
                {
                    rayo.SetPosition(0, m.transform.position + Vector3.up * 1.2f);
                    rayo.SetPosition(1, transform.position + Vector3.up * 1f);
                }
            }
            if (drena) _vida.TakeDamage(drenajePorSegundo * Time.deltaTime, m.gameObject);
        }
    }

    private LineRenderer Rayo(NPCPartyMember m)
    {
        if (plantillaRayoAliado == null) return null;
        if (_rayos.TryGetValue(m, out var r) && r) return r;
        r = Instantiate(plantillaRayoAliado, transform);
        r.gameObject.SetActive(true);
        r.enabled = false;
        _rayos[m] = r;
        return r;
    }
}
