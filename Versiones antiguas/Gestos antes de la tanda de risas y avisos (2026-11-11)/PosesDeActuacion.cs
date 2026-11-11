#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Poses de actuación para las cinemáticas, para cualquier humanoide (el grupo y los NPC): el Idle01
/// del pack con los brazos, el tronco o la cabeza colocados, y su respiración en el resto. Cada pose es
/// una fila de la tabla; las genera <see cref="PoseSobreClip.CrearHumanoide"/> y quedan como estados
/// de la capa UpperBody (valen de pie y sentado) en el controlador de los NPC y en el del jugador,
/// para los guiones: «bucle ManoAlPecho», o <c>HoldPose("ManoAlPecho")</c>.
/// Añadir una pose es añadir una fila y volver a pasar el menú. Ver INC-1051.
///
/// Los gestos (<see cref="Gestos"/>) son poses con movimiento: sobre la pose, unos músculos se mueven
/// en el tiempo (<see cref="PoseSobreClip.CrearConMusculos"/>). Un gesto puede sustituir al clip de un
/// estado que ya usan los guiones (la risa y el miedo del pack metían los brazos en la cabeza): el
/// estado conserva su nombre y todas las cinemáticas pasan a usar el gesto nuevo. Ver INC-1052.
/// </summary>
public static class PosesDeActuacion
{
    private const string CarpetaDeClips = "Assets/Art/Animations/Propias";
    private const string ClipBase = "Assets/Plugins/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle01.fbx";
    private const string Modelo = "Assets/Prefabs/_LIAM.prefab";
    private const string ModeloDeLasFotos = "Assets/Prefabs/_WILL_NPC.prefab";
    private const string Capa = "UpperBody";

    private static readonly string[] Controladores =
    {
        "Assets/Art/Characters/Animator/NPC_NoWeapon.controller",
        "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller",
    };

    private sealed class Pose
    {
        public string Estado;
        /// Hueso respecto al que se fijan las direcciones (el pecho; la cadera si la pose mueve el tronco).
        public HumanBodyBones Marco = HumanBodyBones.Chest;
        public PoseSobreClip.ApunteHumano[] Apuntes;
    }

    private static PoseSobreClip.ApunteHumano H(HumanBodyBones hueso, HumanBodyBones punta, float x, float y, float z)
        => PoseSobreClip.ApunteHumano.Hacia(hueso, punta, new Vector3(x, y, z));

    /// Direcciones en el espacio del personaje: X a su derecha, Y arriba, Z al frente. El avatar no tiene
    /// dedos: las manos siguen al antebrazo.
    private static readonly Pose[] Poses =
    {
        // Las manos en la cintura, los codos hacia fuera: seguridad, impaciencia, regañina.
        new Pose { Estado = "BrazosEnJarras", Apuntes = new[]
        {
            H(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.8f, -0.55f, -0.15f),
            H(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, -0.75f, -0.55f, 0.35f),
            H(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -0.8f, -0.55f, -0.15f),
            H(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.75f, -0.55f, 0.35f),
        } },
        // La mano derecha sobre el pecho: sinceridad, emoción, promesa.
        new Pose { Estado = "ManoAlPecho", Apuntes = new[]
        {
            H(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.3f, -0.72f, 0.6f),
            H(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, -0.72f, 0.68f, 0.1f),
        } },
        // Las manos cogidas a la espalda: espera, formalidad, paseo tranquilo.
        new Pose { Estado = "ManosALaEspalda", Apuntes = new[]
        {
            H(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.4f, -0.88f, -0.25f),
            H(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, -0.9f, -0.4f, -0.12f),
            H(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -0.4f, -0.88f, -0.25f),
            H(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.9f, -0.4f, -0.12f),
        } },
        // Las dos manos al frente, a la altura del pecho: entregar o enseñar algo.
        new Pose { Estado = "Ofrecer", Apuntes = new[]
        {
            H(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.25f, -0.55f, 0.8f),
            H(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, -0.2f, 0.32f, 1f),
            H(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -0.25f, -0.55f, 0.8f),
            H(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.2f, 0.32f, 1f),
        } },
        // La cabeza caída y los hombros vencidos: pena, vergüenza, derrota.
        new Pose { Estado = "Cabizbajo", Marco = HumanBodyBones.Hips, Apuntes = new[]
        {
            H(HumanBodyBones.Spine, HumanBodyBones.Chest, 0f, 1f, 0.17f),
            H(HumanBodyBones.Neck, HumanBodyBones.Head, 0f, 1f, 0.42f),
            H(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.12f, -0.96f, 0.22f),
            H(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -0.12f, -0.96f, 0.22f),
        } },
    };

    private sealed class Gesto
    {
        /// Estado del Animator. Si ya existe en un controlador (en la capa que sea), se le cambia el clip.
        public string Estado;
        public float Duracion;
        public HumanBodyBones Marco = HumanBodyBones.Chest;
        /// La pose de la que parte (los brazos).
        public PoseSobreClip.ApunteHumano[] Apuntes;
        /// Cuánto se suma a cada músculo en cada momento. Front-Back positivo es hacia atrás; Down-Up
        /// positivo, hacia arriba.
        public Dictionary<string, (float t, float delta)[]> Musculos;
    }

    /// Claves que alternan entre dos valores cada medio periodo (la sacudida de la risa, el temblor).
    private static (float t, float delta)[] Botes(float desde, float hasta, float periodo, float a, float b,
                                                 params (float t, float delta)[] alrededor)
    {
        var claves = new List<(float t, float delta)>();
        claves.AddRange(alrededor.Where(c => c.t < desde));
        bool enA = true;
        for (float t = desde; t <= hasta + 0.0001f; t += periodo * 0.5f, enA = !enA) claves.Add((t, enA ? a : b));
        claves.AddRange(alrededor.Where(c => c.t > hasta));
        return claves.ToArray();
    }

    private static readonly Gesto[] Gestos =
    {
        // Risa: las manos en la barriga, el cuerpo se echa atrás y se sacude, la cabeza sube. Nada cerca de la cara.
        new Gesto { Estado = "Laugh01", Duracion = 2.6f,
            Apuntes = new[]
            {
                H(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.35f, -0.85f, 0.35f),
                H(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, -0.6f, -0.1f, 0.7f),
                H(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -0.35f, -0.85f, 0.35f),
                H(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.6f, -0.2f, 0.7f),
            },
            Musculos = new Dictionary<string, (float t, float delta)[]>
            {
                ["Spine Front-Back"] = Botes(0.3f, 2.0f, 0.22f, 0.5f, 0.2f, (0f, 0f), (2.3f, -0.08f), (2.6f, 0f)),
                ["Chest Front-Back"] = Botes(0.3f, 2.0f, 0.22f, 0.35f, 0.12f, (0f, 0f), (2.3f, -0.05f), (2.6f, 0f)),
                ["Head Nod Down-Up"] = Botes(0.3f, 2.0f, 0.22f, 0.6f, 0.35f, (0f, 0f), (2.3f, 0.05f), (2.6f, 0f)),
                ["Left Shoulder Down-Up"] = Botes(0.3f, 2.0f, 0.22f, 0.55f, 0.05f, (0f, 0f), (2.4f, 0f), (2.6f, 0f)),
                ["Right Shoulder Down-Up"] = Botes(0.3f, 2.0f, 0.22f, 0.55f, 0.05f, (0f, 0f), (2.4f, 0f), (2.6f, 0f)),
            } },
        // Miedo: se echa atrás con los hombros encogidos y las manos delante del pecho, temblando.
        new Gesto { Estado = "Fear01", Duracion = 1.5f,
            Apuntes = new[]
            {
                H(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.45f, -0.8f, 0.3f),
                H(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, -0.3f, 0.3f, 0.85f),
                H(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -0.45f, -0.8f, 0.3f),
                H(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.3f, 0.3f, 0.85f),
            },
            Musculos = new Dictionary<string, (float t, float delta)[]>
            {
                ["Spine Front-Back"] = new[] { (0f, 0f), (0.15f, 0.6f), (1.2f, 0.55f), (1.5f, 0.4f) },
                ["Chest Front-Back"] = new[] { (0f, 0f), (0.15f, 0.4f), (1.5f, 0.3f) },
                ["Head Nod Down-Up"] = new[] { (0f, 0f), (0.15f, -0.15f), (1.5f, -0.1f) },
                ["Left Shoulder Down-Up"] = new[] { (0f, 0f), (0.12f, 0.8f), (1.5f, 0.7f) },
                ["Right Shoulder Down-Up"] = new[] { (0f, 0f), (0.12f, 0.8f), (1.5f, 0.7f) },
                ["Chest Twist Left-Right"] = Botes(0.2f, 1.45f, 0.14f, 0.07f, -0.07f, (0f, 0f), (1.5f, 0f)),
            } },
        // Vítor con un puño: el brazo sube hacia fuera, lejos de la cabeza (el del pack la atravesaba), y da dos golpes.
        new Gesto { Estado = "Cheer01", Duracion = 1.4f,
            Apuntes = new[]
            {
                H(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.8f, 0.5f, 0.2f),
                H(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.35f, 0.9f, 0.2f),
            },
            Musculos = new Dictionary<string, (float t, float delta)[]>
            {
                ["Right Arm Down-Up"] = Botes(0.15f, 1.1f, 0.4f, 0.12f, -0.3f, (0f, -0.3f), (1.4f, -0.2f)),
                ["Right Forearm Stretch"] = Botes(0.15f, 1.1f, 0.4f, 0.25f, -0.35f, (0f, -0.3f), (1.4f, 0f)),
                ["Spine Front-Back"] = Botes(0.15f, 1.1f, 0.4f, 0.15f, 0f, (0f, 0f), (1.4f, 0f)),
                ["Head Nod Down-Up"] = Botes(0.15f, 1.1f, 0.4f, 0.2f, 0.05f, (0f, 0f), (1.4f, 0f)),
            } },
        // Vítor con los dos brazos en uve, botando.
        new Gesto { Estado = "Cheer02", Duracion = 1.3f,
            Apuntes = new[]
            {
                H(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.8f, 0.55f, 0.15f),
                H(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.5f, 0.85f, 0.15f),
                H(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -0.8f, 0.55f, 0.15f),
                H(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, -0.5f, 0.85f, 0.15f),
            },
            Musculos = new Dictionary<string, (float t, float delta)[]>
            {
                ["Left Arm Down-Up"] = Botes(0.1f, 1.1f, 0.33f, 0.1f, -0.25f, (0f, -0.35f), (1.3f, -0.25f)),
                ["Right Arm Down-Up"] = Botes(0.1f, 1.1f, 0.33f, 0.1f, -0.25f, (0f, -0.35f), (1.3f, -0.25f)),
                ["Spine Front-Back"] = Botes(0.1f, 1.1f, 0.33f, 0.2f, 0f, (0f, 0f), (1.3f, 0f)),
                ["Head Nod Down-Up"] = Botes(0.1f, 1.1f, 0.33f, 0.3f, 0.1f, (0f, 0f), (1.3f, 0f)),
            } },
        // Encogerse de hombros: palmas hacia arriba a los lados, hombros arriba y la cabeza ladeada.
        new Gesto { Estado = "Shrug01", Duracion = 1.5f,
            Apuntes = new[]
            {
                H(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.4f, -0.9f, 0.15f),
                H(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.7f, 0.15f, 0.7f),
                H(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -0.4f, -0.9f, 0.15f),
                H(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, -0.7f, 0.15f, 0.7f),
            },
            Musculos = new Dictionary<string, (float t, float delta)[]>
            {
                ["Left Shoulder Down-Up"] = new[] { (0f, 0f), (0.3f, 1f), (0.95f, 0.95f), (1.5f, 0f) },
                ["Right Shoulder Down-Up"] = new[] { (0f, 0f), (0.3f, 1f), (0.95f, 0.95f), (1.5f, 0f) },
                ["Left Forearm Stretch"] = new[] { (0f, -0.3f), (0.3f, 0.1f), (0.95f, 0.1f), (1.5f, -0.2f) },
                ["Right Forearm Stretch"] = new[] { (0f, -0.3f), (0.3f, 0.1f), (0.95f, 0.1f), (1.5f, -0.2f) },
                ["Head Tilt Left-Right"] = new[] { (0f, 0f), (0.35f, 0.35f), (0.95f, 0.35f), (1.5f, 0f) },
            } },

        // --- Conjuros de cinemática (referencia de Raúl: los conjuros largos del anime de los 90). ---
        // Con estas proporciones las manos no llegan por encima de la cabeza: los brazos suben en uve por
        // delante y el orbe (ConjuroEnLasManosBeat, FusionDeHechizosBeat) flota encima.

        // Recogerse: muñecas cruzadas ante el pecho, cabeza baja, respiración lenta. Para el cántico.
        new Gesto { Estado = "Conjuro_Recogerse", Duracion = 3f,
            Apuntes = new[]
            {
                H(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.3f, -0.75f, 0.55f),
                H(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, -0.7f, 0.55f, 0.3f),
                H(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -0.3f, -0.75f, 0.55f),
                H(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.7f, 0.45f, 0.4f),
            },
            Musculos = new Dictionary<string, (float t, float delta)[]>
            {
                ["Head Nod Down-Up"] = new[] { (0f, -0.3f), (1.5f, -0.24f), (3f, -0.3f) },
                ["Chest Front-Back"] = new[] { (0f, 0f), (1.5f, 0.08f), (3f, 0f) },
                ["Left Shoulder Down-Up"] = new[] { (0f, 0f), (1.5f, 0.2f), (3f, 0f) },
                ["Right Shoulder Down-Up"] = new[] { (0f, 0f), (1.5f, 0.2f), (3f, 0f) },
            } },
        // Invocar: los brazos en uve hacia arriba y al frente, la mirada arriba; sostiene el orbe.
        new Gesto { Estado = "Conjuro_Invocar", Duracion = 2f,
            Apuntes = new[]
            {
                H(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.55f, 0.45f, 0.7f),
                H(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.2f, 0.7f, 0.7f),
                H(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -0.55f, 0.45f, 0.7f),
                H(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, -0.2f, 0.7f, 0.7f),
            },
            Musculos = new Dictionary<string, (float t, float delta)[]>
            {
                ["Head Nod Down-Up"] = new[] { (0f, 0.4f), (1f, 0.46f), (2f, 0.4f) },
                ["Spine Front-Back"] = new[] { (0f, 0.15f), (1f, 0.2f), (2f, 0.15f) },
                ["Left Arm Down-Up"] = new[] { (0f, 0f), (1f, 0.08f), (2f, 0f) },
                ["Right Arm Down-Up"] = new[] { (0f, 0f), (1f, 0.08f), (2f, 0f) },
            } },
        // Esfuerzo: la misma pose aguantando un hechizo que pesa; echado atrás, hombros arriba, temblando.
        new Gesto { Estado = "Conjuro_Esfuerzo", Duracion = 1.2f,
            Apuntes = new[]
            {
                H(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.55f, 0.45f, 0.7f),
                H(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.2f, 0.7f, 0.7f),
                H(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -0.55f, 0.45f, 0.7f),
                H(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, -0.2f, 0.7f, 0.7f),
            },
            Musculos = new Dictionary<string, (float t, float delta)[]>
            {
                ["Head Nod Down-Up"] = new[] { (0f, 0.15f), (1.2f, 0.15f) },
                ["Spine Front-Back"] = new[] { (0f, 0.4f), (1.2f, 0.4f) },
                ["Left Shoulder Down-Up"] = new[] { (0f, 0.7f), (1.2f, 0.7f) },
                ["Right Shoulder Down-Up"] = new[] { (0f, 0.7f), (1.2f, 0.7f) },
                ["Left Arm Down-Up"] = Botes(0f, 1.2f, 0.12f, -0.12f, -0.2f),
                ["Right Arm Down-Up"] = Botes(0f, 1.2f, 0.12f, -0.2f, -0.12f),
                ["Chest Twist Left-Right"] = Botes(0f, 1.2f, 0.12f, 0.05f, -0.05f),
            } },
        // Abrirse: los brazos abiertos a los lados, palmas al frente, el pecho arriba; respira despacio.
        // Para un ruego o un ritual que pide, no que empuja (el conjuro de la Piedra).
        new Gesto { Estado = "Conjuro_Abrirse", Duracion = 3f,
            Apuntes = new[]
            {
                H(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.85f, -0.1f, 0.3f),
                H(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.8f, 0.4f, 0.35f),
                H(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -0.85f, -0.1f, 0.3f),
                H(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, -0.8f, 0.4f, 0.35f),
            },
            Musculos = new Dictionary<string, (float t, float delta)[]>
            {
                ["Head Nod Down-Up"] = new[] { (0f, 0.25f), (1.5f, 0.32f), (3f, 0.25f) },
                ["Chest Front-Back"] = new[] { (0f, 0.1f), (1.5f, 0.18f), (3f, 0.1f) },
                ["Left Arm Down-Up"] = new[] { (0f, 0f), (1.5f, 0.07f), (3f, 0f) },
                ["Right Arm Down-Up"] = new[] { (0f, 0f), (1.5f, 0.07f), (3f, 0f) },
            } },
        // Lanzar: de los brazos arriba a empujar al frente con las dos manos, con el cuerpo detrás.
        new Gesto { Estado = "Conjuro_Lanzar", Duracion = 1f,
            Apuntes = new[]
            {
                H(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.3f, 0.05f, 0.95f),
                H(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.05f, 0.2f, 1f),
                H(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -0.3f, 0.05f, 0.95f),
                H(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, -0.05f, 0.2f, 1f),
            },
            Musculos = new Dictionary<string, (float t, float delta)[]>
            {
                ["Left Arm Down-Up"] = new[] { (0f, 0.7f), (0.1f, 0.75f), (0.22f, 0f), (1f, 0f) },
                ["Right Arm Down-Up"] = new[] { (0f, 0.7f), (0.1f, 0.75f), (0.22f, 0f), (1f, 0f) },
                ["Spine Front-Back"] = new[] { (0f, 0.3f), (0.1f, 0.35f), (0.24f, -0.14f), (1f, -0.1f) },
                ["Chest Front-Back"] = new[] { (0f, 0.15f), (0.24f, -0.08f), (1f, -0.06f) },
                ["Head Nod Down-Up"] = new[] { (0f, 0.3f), (0.24f, 0.1f), (1f, 0.1f) },
            } },
        // Unir (dúos y tríos): un brazo estirado hacia el punto donde se juntan los hechizos, el otro atrás.
        // _D con el derecho, _I con el izquierdo, para ponerlos en espejo.
        new Gesto { Estado = "Conjuro_Unir_D", Duracion = 1.2f,
            Apuntes = new[]
            {
                H(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.4f, 0.25f, 0.9f),
                H(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.25f, 0.3f, 0.95f),
                H(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -0.55f, -0.75f, -0.3f),
                H(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, -0.5f, -0.8f, -0.1f),
            },
            Musculos = new Dictionary<string, (float t, float delta)[]>
            {
                ["Spine Front-Back"] = new[] { (0f, -0.06f), (1.2f, -0.06f) },
                ["Right Arm Down-Up"] = Botes(0f, 1.2f, 0.12f, 0.04f, -0.04f),
            } },
        new Gesto { Estado = "Conjuro_Unir_I", Duracion = 1.2f,
            Apuntes = new[]
            {
                H(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -0.4f, 0.25f, 0.9f),
                H(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, -0.25f, 0.3f, 0.95f),
                H(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.55f, -0.75f, -0.3f),
                H(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.5f, -0.8f, -0.1f),
            },
            Musculos = new Dictionary<string, (float t, float delta)[]>
            {
                ["Spine Front-Back"] = new[] { (0f, -0.06f), (1.2f, -0.06f) },
                ["Left Arm Down-Up"] = Botes(0f, 1.2f, 0.12f, 0.04f, -0.04f),
            } },
    };

    [MenuItem("El Sendero/Personajes/Animaciones/Poses de actuación: crear los clips y sus estados", priority = 132)]
    public static void Crear()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Modelo);
        var base_ = AssetDatabase.LoadAllAssetsAtPath(ClipBase).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (prefab == null || base_ == null)
        {
            OrdenesDelEditor.Avisar("Poses de actuación", $"falta {(prefab == null ? Modelo : ClipBase)}");
            return;
        }

        var hecho = new StringBuilder();
        foreach (var pose in Poses)
        {
            string ruta = $"{CarpetaDeClips}/Pose_{pose.Estado}.anim";
            float desvio = PoseSobreClip.CrearHumanoide(prefab, base_, ruta, pose.Apuntes, pose.Marco);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta);
            hecho.Append($"{pose.Estado}: desvío máx. {desvio:0.0}°");
            foreach (var rutaCtrl in Controladores)
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(rutaCtrl);
                if (ctrl == null) { hecho.Append($", falta {rutaCtrl}"); continue; }
                bool nuevo = PoseSobreClip.EstadoEnBucle(ctrl, pose.Estado, clip, Capa);
                hecho.Append(nuevo ? $", añadido a {ctrl.name}" : $", al día en {ctrl.name}");
            }
            hecho.Append(". Fotos: ").Append(Fotografiar(clip, pose.Estado)).Append(' ');
        }
        foreach (var gesto in Gestos)
        {
            string rutaDeLaPose = $"{CarpetaDeClips}/Gesto_{gesto.Estado}_pose.anim";
            string ruta = $"{CarpetaDeClips}/Gesto_{gesto.Estado}.anim";
            float desvio = PoseSobreClip.CrearHumanoide(prefab, base_, rutaDeLaPose, gesto.Apuntes, gesto.Marco);
            var pose = AssetDatabase.LoadAssetAtPath<AnimationClip>(rutaDeLaPose);
            PoseSobreClip.CrearConMusculos(pose, ruta, gesto.Duracion, gesto.Musculos);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta);
            hecho.Append($"{gesto.Estado} (gesto, {clip.length:0.00} s): desvío máx. {desvio:0.0}°");
            foreach (var rutaCtrl in Controladores)
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(rutaCtrl);
                if (ctrl == null) { hecho.Append($", falta {rutaCtrl}"); continue; }
                hecho.Append($", {ctrl.name}: {PonerEnSuEstado(ctrl, gesto.Estado, clip)}");
            }
            hecho.Append(". ");
        }
        OrdenesDelEditor.Avisar("Poses de actuación", hecho.ToString());
    }

    /// Pone el clip en el estado con ese nombre de cada capa que lo tenga (antes llevaba el del pack);
    /// si ninguna lo tiene, lo añade a la capa de medio cuerpo. Devuelve lo que ha hecho.
    private static string PonerEnSuEstado(AnimatorController ctrl, string estado, AnimationClip clip)
    {
        var capas = ctrl.layers.Where(l => l.stateMachine.states.Any(s => s.state.name == estado)).Select(l => l.name).ToList();
        if (capas.Count == 0)
        {
            PoseSobreClip.EstadoEnBucle(ctrl, estado, clip, Capa);
            return $"añadido a {Capa}";
        }
        foreach (string capa in capas) PoseSobreClip.EstadoEnBucle(ctrl, estado, clip, capa);
        return "clip cambiado en " + string.Join(", ", capas);
    }

    /// Fotos de la pose de frente y de lado, con el controlador de los NPC (el mismo camino que en
    /// juego: el estado del Animator), para revisarla sin entrar en Play.
    private static string Fotografiar(AnimationClip clip, string estado)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModeloDeLasFotos);
        if (prefab == null) return "(falta " + ModeloDeLasFotos + ")";
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.hideFlags = HideFlags.HideAndDontSave;
        foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
        var sitio = new Vector3(0f, 800f, 0f);
        inst.transform.SetPositionAndRotation(sitio, Quaternion.identity);
        var hechas = new StringBuilder();
        try
        {
            foreach (var a in inst.GetComponentsInChildren<Animator>(true)) { a.enabled = true; a.Rebind(); }
            // Fuera de Play las mallas con huesos no se recalculan en cada render.
            foreach (var malla in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                malla.forceMatrixRecalculationPerRender = true;
                malla.updateWhenOffscreen = true;
            }
            Horno.MuestrearEstado(inst, clip, estado, 0.5f);
            inst.transform.SetPositionAndRotation(sitio, Quaternion.identity);
            var centro = sitio + Vector3.up * 0.6f;
            hechas.Append(CapturaDeEscenario.FotoDesde($"pose_{estado}_frente", centro + new Vector3(0.5f, 0.3f, 2.6f), centro, 35f)).Append(' ');
            hechas.Append(CapturaDeEscenario.FotoDesde($"pose_{estado}_lado", centro + new Vector3(2.6f, 0.3f, 0.5f), centro, 35f));
        }
        finally { Object.DestroyImmediate(inst); }
        return hechas.ToString();
    }
}
#endif
