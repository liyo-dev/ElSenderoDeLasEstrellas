using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RowSelectionHighlighter : MonoBehaviour
{
    public Color normalColor   = new Color(1,1,1,0f);
    public Color selectedColor = new Color(1f,0.92f,0.16f,0.85f);

    readonly List<Graphic> _rowBacks = new();
    int _selected = 0;

    void Start()
    {
        // Auto-registrar todas las filas que empiezan con "Row_" en los paneles hijos
        foreach (Transform panel in transform)
        {
            if (!panel.name.StartsWith("Panel_")) continue;
            
            foreach (Transform row in panel)
            {
                if (row.name.StartsWith("Row_") && !row.name.Contains("Actions"))
                {
                    RegisterRow(row as RectTransform);
                }
            }
        }
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"RowSelectionHighlighter: {_rowBacks.Count} filas registradas automáticamente");
#endif
        
        // Selección inicial
        if (_rowBacks.Count > 0)
            Refresh();
    }

    public void RegisterRow(RectTransform row)
    {
        if (!row) return;

        // fondo (Image) para poder tintar la fila
        var bg = row.GetComponent<Image>();
        if (bg == null) bg = row.gameObject.AddComponent<Image>();
        bg.raycastTarget = false;
        bg.color = normalColor;

        if (!_rowBacks.Contains(bg))
            _rowBacks.Add(bg);
    }

    public void SetSelected(int index)
    {
        if (_rowBacks.Count == 0) return;
        _selected = Mathf.Clamp(index, 0, _rowBacks.Count - 1);
        Refresh();
    }

    public void Refresh()
    {
        for (int i = 0; i < _rowBacks.Count; i++)
            _rowBacks[i].color = (i == _selected) ? selectedColor : normalColor;
    }

    public int Count => _rowBacks.Count;
    public int SelectedIndex => _selected;
}