using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestStepItemUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Sprite spritePending;
    [SerializeField] private Sprite spriteDone;
    [SerializeField] private Color colorPending = Color.white;
    [SerializeField] private Color colorDone = Color.white;

    public void Bind(QuestStep step)
    {
        if (label) label.text = step?.description ?? "";
        UpdateIcon(step);
    }

    public void Bind(QuestStep step, QuestData questData, int stepIndex)
    {
        // Obtener descripción localizada
        string description = step?.description ?? "";
        if (questData != null && stepIndex >= 0)
        {
            description = questData.GetLocalizedStepDescription(stepIndex);
        }
        
        if (label) label.text = description;
        UpdateIcon(step);
    }

    /// <summary>Varios pasos iguales seguidos en una sola fila: texto del primero y «(hechos/total)».</summary>
    public void BindRepetido(QuestData questData, int primerPaso, int hechos, int total)
    {
        string description = questData != null ? questData.GetLocalizedStepDescription(primerPaso) : "";
        if (label) label.text = $"{description} ({hechos}/{total})";
        bool done = hechos >= total;
        if (icon)
        {
            icon.sprite = done ? spriteDone : spritePending;
            icon.color  = done ? colorDone   : colorPending;
        }
    }

    private void UpdateIcon(QuestStep step)
    {
        if (icon)
        {
            bool done = step != null && step.completed;
            icon.sprite = done ? spriteDone : spritePending;
            icon.color  = done ? colorDone   : colorPending;
        }
    }
}