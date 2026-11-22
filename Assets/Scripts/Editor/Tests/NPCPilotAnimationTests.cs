using System.Collections.Generic;
using System.Reflection;
using Game.NPC;
using Game.NPC.Common;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class NPCPilotAnimationTests
{
    [Test]
    public void VolverALaIA_LimpiaStrafeYRecuperaLocomocion()
    {
        var go = new GameObject("Prueba de relevo");
        try
        {
            var animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller");
            Assert.NotNull(animator.runtimeAnimatorController);
            animator.Rebind();
            animator.Update(0f);
            var piloto = go.AddComponent<PilotoDelCuerpo>();
            var npc = go.AddComponent<NPCSimpleAnimator>();
            Poner(npc, "animator", animator);
            Poner(npc, "_piloto", piloto);
            Poner(npc, "_stateCache", new AnimatorStateCache(animator));
            var hashes = new HashSet<int>();
            foreach (var p in animator.parameters) hashes.Add(p.nameHash);
            Poner(npc, "_animatorParamHashes", hashes);
            Poner(npc, "_currentState", NPCSimpleAnimator.AnimationState.Running);
            animator.SetBool("IsStrafing", true);
            animator.SetBool("IsSprinting", true);
            animator.SetFloat("InputHorizontal", -1f);
            animator.SetFloat("InputVertical", -1f);
            Assert.IsTrue(animator.GetBool("IsStrafing"), "El montaje de prueba debe conservar el strafe inicial.");
            npc.SendMessage("OnEnable", SendMessageOptions.DontRequireReceiver);
            Assert.IsFalse(animator.GetBool("IsStrafing"), "El strafe del jugador no debe sobrevivir al relevo a la IA.");
            Assert.IsFalse(animator.GetBool("IsSprinting"));
            Assert.AreEqual(0f, animator.GetFloat("InputHorizontal"));
            Assert.AreEqual(0f, animator.GetFloat("InputVertical"));
            Assert.AreEqual(NPCSimpleAnimator.AnimationState.Idle, npc.GetCurrentState());
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void ReunionForzada_SinJugadorTodavia_ConservaSolicitudSinDuplicarla()
    {
        var go = new GameObject("Prueba de reunión pendiente");
        go.SetActive(false);
        try
        {
            var party = go.AddComponent<PlayerParty>();
            var member = go.AddComponent<NPCPartyMember>();
            var miembros = (List<NPCPartyMember>)Campo(party, "_members");
            miembros.Add(member);
            party.TeleportAllMembersToPlayer(forzar: true);
            party.TeleportAllMembersToPlayer(forzar: true);
            var pendientes = (List<NPCPartyMember>)Campo(party, "_reunionesPendientes");
            Assert.That(pendientes, Does.Contain(member));
            Assert.AreEqual(1, pendientes.FindAll(m => m == member).Count);
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static object Campo(object target, string nombre) =>
        target.GetType().GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void Poner(object target, string nombre, object valor) =>
        target.GetType().GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, valor);

    [MenuItem("El Sendero/Pruebas/Regresión de animación al cambiar mando")]
    public static void Ejecutar()
    {
        string carpeta = System.IO.Path.Combine(Application.dataPath, "../Guiones/_pruebas");
        System.IO.Directory.CreateDirectory(carpeta);
        string informe = System.IO.Path.Combine(carpeta, "animacion-grupo.txt");
        try
        {
            var pruebas = new NPCPilotAnimationTests();
            pruebas.VolverALaIA_LimpiaStrafeYRecuperaLocomocion();
            pruebas.ReunionForzada_SinJugadorTodavia_ConservaSolicitudSinDuplicarla();
            System.IO.File.WriteAllText(informe, "CORRECTO: relevo de animación y reunión pendiente.\n");
            Debug.Log("[Pruebas del grupo] Relevo de animación correcto.");
        }
        catch (System.Exception e)
        {
            System.IO.File.WriteAllText(informe, "FALLO: " + e + "\n");
            throw;
        }
    }
}
