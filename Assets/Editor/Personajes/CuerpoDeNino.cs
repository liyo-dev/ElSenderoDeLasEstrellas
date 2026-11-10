using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cuerpo de niño propio para un personaje modular (RPG Tiny Hero): el esqueleto del pack con proporciones
/// de niño, una talla por FBX de esqueleto (Tools/Modelos/Ninos/cuerpo_nino.py). Cambia de sitio los huesos
/// del modelo (los mismos huesos con los mismos giros; solo cambian las posiciones), le hace su Avatar
/// humanoide con esas proporciones (las animaciones del pack se adaptan solas) y lo marca con
/// <see cref="TallaDeNino"/> de cuerpo propio, que ya no lo encoge.
/// Las piezas de cuerpo que lleve tienen que estar hechas sobre la misma talla (las de Tools/Modelos/Ninos);
/// las de cabeza del pack valen tal cual, porque la cabeza no cambia de tamaño. Se puede repetir.
/// </summary>
public static class CuerpoDeNino
{
    /// <summary>El esqueleto del adulto (la referencia de alturas y de lados).</summary>
    const string EsqueletoAdulto = "Assets/Art/Characters/RPG Tiny Hero Duo/Mesh/AllBodiesCloaks.fbx";

    /// <param name="raiz">Raíz del prefab abierto (PrefabUtility.LoadPrefabContents).</param>
    /// <param name="rutaEsqueleto">FBX de la talla (solo el esqueleto).</param>
    /// <param name="rutaAvatar">Dónde se guarda su Avatar (.asset).</param>
    public static bool Poner(GameObject raiz, string rutaEsqueleto, string rutaAvatar, StringBuilder informe, float brazosAbiertos = 0f)
    {
        var nino = AssetDatabase.LoadAssetAtPath<GameObject>(rutaEsqueleto);
        var adulto = AssetDatabase.LoadAssetAtPath<GameObject>(EsqueletoAdulto);
        if (nino == null || adulto == null) { informe.AppendLine($"{raiz.name}: no encuentro {(nino == null ? rutaEsqueleto : EsqueletoAdulto)}."); return false; }
        var animator = raiz.GetComponentsInChildren<Animator>(true)
                           .FirstOrDefault(a => a.avatar != null && a.avatar.isHuman && Huesos(a.transform).ContainsKey("pelvis"));
        if (animator == null) { informe.AppendLine($"{raiz.name}: no tiene un Animator humanoide con el esqueleto del pack."); return false; }

        // Sin la escala de niño de antes: el cuerpo ya es de niño
        var talla = raiz.GetComponent<TallaDeNino>();
        if (talla == null) talla = raiz.AddComponent<TallaDeNino>();
        raiz.transform.localScale = Vector3.one;

        var modelo = animator.transform;
        var huesos = Huesos(modelo);
        var deNino = Huesos(nino.transform);
        var deAdulto = Huesos(adulto.transform);
        foreach (var h in new[] { "head", "hand_l", "pelvis" })
            if (!huesos.ContainsKey(h) || !deNino.ContainsKey(h) || !deAdulto.ContainsKey(h))
            { informe.AppendLine($"{raiz.name}: falta el hueso {h} en el modelo o en los esqueletos."); return false; }

        // Las posiciones van en el espacio del modelo (el del FBX): se comprueba que los tres casan (alturas y lados)
        Vector3 EnModelo(Transform raizModelo, Transform t) => raizModelo.InverseTransformPoint(t.position);
        float altoAdulto = EnModelo(adulto.transform, deAdulto["head"]).y;
        float altoNino = EnModelo(nino.transform, deNino["head"]).y;
        float altoAhora = EnModelo(modelo, huesos["head"]).y;
        float factor = altoNino / altoAdulto;
        bool mismoLado = Mathf.Sign(EnModelo(nino.transform, deNino["hand_l"]).x) == Mathf.Sign(EnModelo(adulto.transform, deAdulto["hand_l"]).x)
                      && Mathf.Sign(EnModelo(modelo, huesos["hand_l"]).x) == Mathf.Sign(EnModelo(adulto.transform, deAdulto["hand_l"]).x);
        bool alturaConocida = Mathf.Abs(altoAhora / altoAdulto - 1f) < 0.03f || Mathf.Abs(altoAhora / altoNino - 1f) < 0.03f;
        if (factor < 0.4f || factor > 0.98f || !mismoLado || !alturaConocida)
        {
            informe.AppendLine($"{raiz.name}: el esqueleto de {System.IO.Path.GetFileName(rutaEsqueleto)} no casa con el modelo " +
                               $"(cabeza: adulto {altoAdulto:0.###}, niño {altoNino:0.###}, modelo {altoAhora:0.###}; mismo lado: {mismoLado}). No se toca.");
            return false;
        }

        // Los huesos a su sitio, de padres a hijos (los giros no cambian)
        foreach (var kv in huesos)
            if (deNino.TryGetValue(kv.Key, out var n))
                kv.Value.position = modelo.TransformPoint(EnModelo(nino.transform, n));

        // Su Avatar: el mapa de huesos del pack, con el esqueleto de ahora y la pose en T del pack
        var hd = animator.avatar.humanDescription;
        var giros = new Dictionary<string, Quaternion>();
        foreach (var s in hd.skeleton) if (!giros.ContainsKey(s.name)) giros[s.name] = s.rotation;
        var esqueleto = new List<SkeletonBone>
            { new SkeletonBone { name = modelo.name, position = Vector3.zero, rotation = Quaternion.identity, scale = Vector3.one } };
        foreach (var kv in huesos)
        {
            var t = kv.Value;
            esqueleto.Add(new SkeletonBone { name = t.name, position = t.localPosition, rotation = giros.TryGetValue(t.name, out var g) ? g : t.localRotation, scale = t.localScale });
        }
        hd.skeleton = esqueleto.ToArray();
        if (brazosAbiertos > 0f) AbrirLosBrazos(ref hd, brazosAbiertos);
        var avatar = AvatarBuilder.BuildHumanAvatar(modelo.gameObject, hd);
        if (avatar == null || !avatar.isValid || !avatar.isHuman) { informe.AppendLine($"{raiz.name}: no sale un Avatar humanoide válido; no se toca."); return false; }
        avatar.name = System.IO.Path.GetFileNameWithoutExtension(rutaAvatar);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(rutaAvatar));
        if (AssetDatabase.LoadAssetAtPath<Avatar>(rutaAvatar) != null) AssetDatabase.DeleteAsset(rutaAvatar);
        AssetDatabase.CreateAsset(avatar, rutaAvatar);
        animator.avatar = avatar;

        var so = new SerializedObject(talla);
        so.FindProperty("cuerpoPropio").boolValue = true;
        so.FindProperty("altura").floatValue = factor;
        so.ApplyModifiedPropertiesWithoutUndo();
        informe.AppendLine($"{raiz.name}: cuerpo de niño ({System.IO.Path.GetFileNameWithoutExtension(rutaEsqueleto)}, {factor:0.00} de un adulto) y su Avatar.");
        return true;
    }

    /// <summary>
    /// Brazos que no bajan del todo: el tope de «brazo abajo» se queda en (1 - cuánto) del de Unity, así que
    /// cualquier animación los deja algo separados del cuerpo. Para quien lleva falda de vuelo: con los
    /// brazos pegados, las manos se metían dentro (Nina, Ver INC-1011).
    /// </summary>
    static void AbrirLosBrazos(ref HumanDescription hd, float cuanto)
    {
        var huesos = hd.human;
        for (int i = 0; i < huesos.Length; i++)
        {
            if (huesos[i].humanName != "LeftUpperArm" && huesos[i].humanName != "RightUpperArm") continue;
            int hueso = System.Array.IndexOf(HumanTrait.BoneName, huesos[i].humanName);
            var limite = huesos[i].limit;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            for (int dof = 0; dof < 3; dof++)
            {
                int musculo = HumanTrait.MuscleFromBone(hueso, dof);
                if (musculo < 0) continue;
                min[dof] = HumanTrait.GetMuscleDefaultMin(musculo);
                max[dof] = HumanTrait.GetMuscleDefaultMax(musculo);
                if (HumanTrait.MuscleName[musculo].Contains("Down-Up")) min[dof] *= 1f - cuanto;
            }
            limite.min = min; limite.max = max; limite.useDefaultValues = false;
            huesos[i].limit = limite;
        }
        hd.human = huesos;
    }

    /// <summary>
    /// Los huesos de un modelo por nombre, de padres a hijos: lo que cuelga de él sin malla, sin meterse en lo que
    /// tiene su propio Animator (armas con su esqueleto).
    /// </summary>
    static Dictionary<string, Transform> Huesos(Transform modelo)
    {
        var huesos = new Dictionary<string, Transform>();
        void Recorrer(Transform t)
        {
            foreach (Transform hijo in t)
            {
                if (hijo.GetComponent<Animator>() != null || hijo.GetComponent<Renderer>() != null) continue;
                if (!huesos.ContainsKey(hijo.name)) huesos[hijo.name] = hijo;
                Recorrer(hijo);
            }
        }
        Recorrer(modelo);
        return huesos;
    }
}
