using System.Collections.Generic;
using UnityEngine;
using Game.NPC;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Los cuerpos del grupo que están cerca de un punto: el personaje al mando y los compañeros
/// visibles (el Will de la IA incluido cuando manda otro). Para la magia de apoyo: curar y escudo de
/// grupo (INC-500). El compañero que está «dentro» del jugador (el que se lleva al mando) no cuenta
/// dos veces: ese es el cuerpo del jugador.
/// </summary>
public static class GrupoCercano
{
    public struct Miembro
    {
        public Slot slot;
        public Transform cuerpo;
        /// Solo en el personaje al mando.
        public PlayerHealthSystem jugador;
        /// Solo en los compañeros que lleva la IA.
        public Damageable npc;

        public bool EstaVivo => jugador != null ? jugador.IsAlive : npc != null && npc.IsAlive;

        public void Curar(float cantidad)
        {
            if (cantidad <= 0f) return;
            if (jugador != null) jugador.Heal(cantidad);
            else if (npc != null) npc.Heal(cantidad);
        }

        /// El objeto donde van las reglas de daño (IFiltroDeDano) de este miembro.
        public GameObject ObjetoDeVida => jugador != null ? jugador.gameObject : npc != null ? npc.gameObject : null;
    }

    public static void Buscar(Vector3 centro, float radio, List<Miembro> resultado)
    {
        resultado.Clear();
        Slot activo = PartyControlManager.Instance != null ? PartyControlManager.Instance.ActiveSlot : Slot.Will;

        if (PlayerService.TryGetComponent<PlayerHealthSystem>(out var vida, includeInactive: false, allowSceneLookup: true) && vida != null)
            Anadir(resultado, centro, radio, new Miembro { slot = activo, cuerpo = vida.transform, jugador = vida });

        var swapper = ActiveCharacterSwapper.Instance;
        var oculto = swapper != null ? swapper.HiddenNpc : null;

        for (int s = 0; s < 3; s++)
        {
            var slot = (Slot)s;
            if (slot == activo) continue;

            NPCPartyMember npc;
            if (slot == Slot.Will) npc = swapper != null ? swapper.WillNpcInstance : null;
            else
            {
                var party = PlayerParty.HasInstance ? PlayerParty.Instance : null;
                npc = party != null ? party.GetMemberByName(slot == Slot.Liam ? "Liam" : "Estela") : null;
            }
            if (npc == null || npc == oculto || !npc.isActiveAndEnabled) continue;

            var d = npc.GetComponent<Damageable>();
            if (d == null) d = npc.GetComponentInChildren<Damageable>();
            if (d == null) continue;
            Anadir(resultado, centro, radio, new Miembro { slot = slot, cuerpo = npc.transform, npc = d });
        }
    }

    private static void Anadir(List<Miembro> resultado, Vector3 centro, float radio, Miembro m)
    {
        if (m.cuerpo == null || !m.EstaVivo) return;
        Vector3 d = m.cuerpo.position - centro;
        d.y = 0f;
        if (d.sqrMagnitude > radio * radio) return;
        resultado.Add(m);
    }
}
