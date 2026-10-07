using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.NPC;

/// <summary>
/// Permite comparar combate en solitario y con los compañeros reales del juego,
/// manteniendo la composición de prueba fuera del progreso guardado.
/// </summary>
public sealed class CombatLabPartyPanel : MonoBehaviour, ISeccionDelLab
{
    private readonly List<string> _savedPartyIds = new();

    private GameObject _liamObject;
    private GameObject _estelaObject;
    private NPCPartyMember _liamMember;
    private NPCPartyMember _estelaMember;
    private PlayerPresetSO _activePreset;
    private int _composition;
    private bool _initialized;
    private string _status = "Solo";

    public void Initialize(GameObject player, int composicionInicial = 0)
    {
        var config = Resources.Load<CombatLabConfig>("CombatLab/CombatLabConfig");
        if (config == null || config.liamPrefab == null || config.estelaPrefab == null)
        {
            _status = "Falta Resources/CombatLab/CombatLabConfig o alguno de sus prefabs.";
            _initialized = true;
            return;
        }

        Vector3 playerPosition = player != null ? player.transform.position : Vector3.zero;
        var membersRoot = new GameObject("LAB_COMPANEROS_DE_PRUEBA");
        _liamObject = Instantiate(config.liamPrefab, playerPosition + new Vector3(-2f, 0f, 0f),
            Quaternion.identity, membersRoot.transform);
        _liamObject.name = "LAB_Liam";
        _estelaObject = Instantiate(config.estelaPrefab, playerPosition + new Vector3(2f, 0f, 0f),
            Quaternion.identity, membersRoot.transform);
        _estelaObject.name = "LAB_Estela";
        SilenciarEscuchasDeCompanero(_liamObject);
        SilenciarEscuchasDeCompanero(_estelaObject);
        _liamObject.SetActive(false);
        _estelaObject.SetActive(false);

        if (GameBootService.Profile != null)
        {
            _activePreset = GameBootService.Profile.GetActivePresetResolved();
            if (_activePreset != null && _activePreset.partyMemberIds != null)
                _savedPartyIds.AddRange(_activePreset.partyMemberIds);
        }

        _initialized = true;
        ApplyComposition(composicionInicial);
    }

    private static void SilenciarEscuchasDeCompanero(GameObject companion)
    {
        if (companion == null) return;
        var listeners = companion.GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            if (listeners[i] != null && listeners[i].enabled)
                listeners[i].enabled = false;
        }
    }

    public string Titulo => "Grupo";
    public int Orden => 30;

    public void Dibujar()
    {
        GUILayout.Label("Con quién pelea Will. Los compañeros usan la IA y los ataques de equipo del juego (LT+RT).", EstiloDelLab.Etiqueta);
        GUILayout.Space(6f);
        if (!_initialized || _liamObject == null || _estelaObject == null)
        {
            GUILayout.Label(_status, EstiloDelLab.Nota);
            return;
        }
        GUILayout.BeginHorizontal();
        Opcion("Solo", 0);
        Opcion("+ Estela", 1);
        Opcion("+ Liam", 2);
        Opcion("Los tres", 3);
        GUILayout.EndHorizontal();
        GUILayout.Label(_status, EstiloDelLab.Nota);
    }

    private void Opcion(string texto, int composicion)
    {
        if (EstiloDelLab.Opcion(texto, _composition == composicion)) ApplyComposition(composicion);
    }

    private void ApplyComposition(int composition)
    {
        if (!_initialized && composition != 0) return;
        _composition = Mathf.Clamp(composition, 0, 3);

        bool includeEstela = _composition == 1 || _composition == 3;
        bool includeLiam = _composition == 2 || _composition == 3;

        SetMemberEnabled(_liamObject, ref _liamMember, includeLiam);
        SetMemberEnabled(_estelaObject, ref _estelaMember, includeEstela);

        RestoreSavedPartyIds();
        string label = _composition switch
        {
            1 => "Estela acompaña a Will.",
            2 => "Liam acompaña a Will.",
            3 => "Estela y Liam acompañan a Will.",
            _ => "Will combate en solitario."
        };
        _status = _composition == 0 ? label : label + " Preparando compañeros…";

        if (includeLiam) StartCoroutine(JoinWhenReady(_liamObject, true));
        if (includeEstela) StartCoroutine(JoinWhenReady(_estelaObject, false));
    }

    private void SetMemberEnabled(GameObject memberObject, ref NPCPartyMember member, bool enabled)
    {
        if (memberObject == null) return;

        if (!enabled)
        {
            if (member == null) member = memberObject.GetComponent<NPCPartyMember>();
            if (member != null && member.IsInParty)
                member.LeaveParty();

            if (memberObject.activeSelf)
                memberObject.SetActive(false);
            RestoreSavedPartyIds();
            return;
        }

        if (!memberObject.activeSelf)
            memberObject.SetActive(true);
    }

    private IEnumerator JoinWhenReady(GameObject memberObject, bool isLiam)
    {
        float timeout = 12f;
        NPCPartyMember member = isLiam ? _liamMember : _estelaMember;

        while (timeout > 0f && memberObject != null && memberObject.activeInHierarchy)
        {
            if (member == null)
                member = memberObject.GetComponent<NPCPartyMember>();

            if (member != null)
            {
                if (!member.IsInParty)
                    member.JoinParty(isRestore: true);

                if (member.IsInParty)
                {
                    if (isLiam) _liamMember = member;
                    else _estelaMember = member;
                    RestoreSavedPartyIds();
                    _status = BuildStatus();
                    yield break;
                }
            }

            RestoreSavedPartyIds();
            yield return new WaitForSeconds(0.25f);
            timeout -= 0.25f;
        }

        RestoreSavedPartyIds();
        _status = BuildStatus() + " No se pudo incorporar a todos: revisa la consola y el NavMesh.";
    }

    private string BuildStatus()
    {
        int count = 0;
        if (_liamMember != null && _liamMember.IsInParty) count++;
        if (_estelaMember != null && _estelaMember.IsInParty) count++;
        return count switch
        {
            0 => "Sin compañeros activos.",
            1 => "1 compañero incorporado al combate.",
            _ => "2 compañeros incorporados al combate."
        };
    }

    private void RestoreSavedPartyIds()
    {
        if (_activePreset == null) return;
        _activePreset.partyMemberIds = _savedPartyIds;
    }

    private void OnDisable()
    {
        if (!_initialized) return;
        LeaveLabMember(_liamMember);
        LeaveLabMember(_estelaMember);
        RestoreSavedPartyIds();
    }

    private void LeaveLabMember(NPCPartyMember member)
    {
        if (member != null && member.IsInParty)
            member.LeaveParty();
    }
}
