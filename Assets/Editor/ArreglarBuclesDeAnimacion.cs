using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Quita el bucle a los clips que NO tienen que repetirse.
///
/// El pack de animaciones (RPG Tiny Hero Duo) viene con `loopTime = 1` en casi todo, porque su
/// Animator Controller de demostración sale de cada estado por tiempo. Aquí no: los gestos de las
/// cinemáticas se disparan sobre una capa de cuerpo superior y no tienen transición de salida, así
/// que un clip cíclico se repite solo hasta que otra cosa lo pise.
///
/// Eso es, literalmente, «cuando el Archimago se protege sigue haciendo la animación 3 veces» y
/// «hay un momento donde se queda pillado en una animación, creo que es la de protección»:
/// Defend_NoWeapon dura 20 fotogramas y se repetía dos o tres veces mientras el Mago Oscuro caía
/// en picado, y DefendHit_NoWeapon se quedaba dando vueltas para siempre.
///
/// Sin bucle, el clip se queda congelado en su último fotograma — que para una guardia levantada
/// es exactamente la pose que se quiere — y el beat siguiente lo saca cuando toca.
///
/// Se toca la IMPORTACIÓN, no el .meta a mano (INC-249): AssetImporter y reimport.
public static class ArreglarBuclesDeAnimacion
{
    /// Clips que son una acción con principio y final, no un estado que se mantiene.
    private static readonly string[] NoDebenRepetirse =
    {
        "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon/Defend_NoWeapon.fbx",
        "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon/DefendHit_NoWeapon.fbx",
        "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon/GetHit01_NoWeapon.fbx",
        "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon/GetHit02_NoWeapon.fbx",
        // Levantarse es una acción: en bucle, se volvería a tirar al suelo para levantarse otra vez.
        "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon/GetUp_NoWeapon.fbx",
        // Aplaudir se usa como GESTO suelto y, sostenido, es «las manos juntas» de la plegaria
        // (INC-374). En bucle se quedaba aplaudiendo mientras reza. El clip es del pack de Kevin
        // Iglesias, que es de donde salen los gestos sociales de este controller.
        "Assets/Plugins/Kevin Iglesias/Human Animations/Animations/Male/Social/Conversation/HumanM@HandClap01.fbx",

        // Los brazos levantados del final del prologo: «cuando dice ABSOLUTA la animacion debe ser
        // la de found something y que se quede con los brazos arriba». Ciclico se le caerian y
        // volveria a levantarlos una y otra vez; sin bucle se queda arriba, que es la imagen con
        // la que acaba el prologo.
        "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon/FoundSomething_NoWeapon.fbx",
    };

    [MenuItem("El Sendero/Archivo/Animaciones/Arreglar bucles de los clips de una sola vez")]
    public static void Arreglar()
    {
        int tocados = Ejecutar(avisar: true);
        EditorUtility.DisplayDialog("Bucles de animación",
            tocados == 0
                ? "Todos los clips ya estaban bien: ninguno de la lista se repetía."
                : $"{tocados} clip(s) dejan de repetirse. Se han reimportado.",
            "Vale");
    }

    /// Lo mismo, sin diálogo, para llamarlo desde PREPARAR TODO.
    public static int Ejecutar(bool avisar)
    {
        int tocados = 0;

        foreach (string ruta in NoDebenRepetirse)
        {
            var importer = AssetImporter.GetAtPath(ruta) as ModelImporter;
            if (importer == null)
            {
                if (avisar)
                    Debug.LogWarning($"[Bucles] No encuentro el modelo '{ruta}'. Si se ha movido, " +
                        "hay que actualizar la lista de ArreglarBuclesDeAnimacion.");
                continue;
            }

            // Si nadie ha tocado los clips a mano, `clipAnimations` viene vacío y lo que manda es
            // `defaultClipAnimations`. Escribir la lista por defecto ya modificada es lo que
            // convierte el ajuste en explícito.
            var clips = new List<ModelImporterClipAnimation>(
                importer.clipAnimations.Length > 0
                    ? importer.clipAnimations
                    : importer.defaultClipAnimations);

            bool cambiado = false;
            for (int i = 0; i < clips.Count; i++)
            {
                if (!clips[i].loopTime) continue;
                var c = clips[i];
                c.loopTime = false;
                c.loopPose = false;
                clips[i] = c;
                cambiado = true;
            }

            if (!cambiado) continue;

            importer.clipAnimations = clips.ToArray();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            tocados++;

            Debug.Log($"[Bucles] '{System.IO.Path.GetFileNameWithoutExtension(ruta)}' deja de " +
                "repetirse: ahora se queda en su último fotograma hasta que otro beat lo saque.");
        }

        return tocados;
    }
}
