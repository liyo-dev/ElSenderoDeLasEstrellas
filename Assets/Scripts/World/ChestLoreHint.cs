using UnityEngine;

/// <summary>
/// Bocadillo de aviso ambiental ("cuidado con los cofres del bosque...") — pista de lore pensada
/// para foreshadowear el cofre mímico (ChestMonsterAI) antes de que el jugador se lo encuentre.
/// No depende de ningún sistema de diálogo/quest nuevo: usa el SpeechBubbleUI que ya existe en el
/// proyecto (Assets/Scripts/UI/SpeechBubbleUI.cs, singleton persistente), que hoy no lo usa ningún
/// NPC todavía.
///
/// Colocar este componente en CUALQUIER NPC ya presente en la escena (candidato natural: el
/// guardia del bosque, si hay uno colocado cerca del Bosque Prohibido — hay diálogo ya preparado
/// para un "GuardiaBosque" en Assets/_DIALOGUES/DIALOGUE NPCS/GuardiaBosque/, aunque no he podido
/// confirmar si ese NPC concreto está colocado en la escena o con qué nombre). Se dispara solo una
/// vez, la primera vez que el jugador se acerca lo suficiente — no bloquea nada, no interrumpe
/// movimiento ni interacción, es puramente ambiental.
/// </summary>
[DisallowMultipleComponent]
public class ChestLoreHint : MonoBehaviour
{
    [Header("Texto")]
    [TextArea(2, 4)]
    [SerializeField]
    string line = "Ten cuidado con los cofres del bosque... no todos son lo que parecen.";
    [SerializeField] float bubbleDuration = 4f;
    [Tooltip("Trigger de animación opcional a reproducir en el NPC mientras habla (déjalo vacío si no quieres ninguno).")]
    [SerializeField] string animTrigger = "";

    [Header("Disparo")]
    [SerializeField] float triggerRadius = 6f;
    [SerializeField] bool onlyOnce = true;

    Transform _player;
    bool _triggered;

    void Awake()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj) _player = playerObj.transform;
    }

    void Update()
    {
        if (_triggered && onlyOnce) return;
        if (_player == null || SpeechBubbleUI.Instance == null) return;

        float dist = Vector3.Distance(transform.position, _player.position);
        if (dist <= triggerRadius)
        {
            SpeechBubbleUI.Instance.Show(
                transform, line, bubbleDuration, null,
                string.IsNullOrEmpty(animTrigger) ? null : animTrigger);
            _triggered = true;
        }
    }
}
