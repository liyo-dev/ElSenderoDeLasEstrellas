using UnityEngine;
using Core;

public class CreatorGamepadController : MonoBehaviour
{
    public ModularAutoBuilder builder;               // arrastra MC01
    public RowSelectionHighlighter highlighter;      // arrastra Panel (RowSelectionHighlighter)
    public CharacterCreatorUI ui;                    // arrastra el componente del Panel

    float _lastNavY;
    float _lastNavX;

    void OnEnable()
    {
        // Cambiar a modo UI para el character creator
        if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
            pim.PushUIMode();
    }

    void OnDisable()
    {
        // Restaurar modo Gameplay
        if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
            pim.PopUIMode();
    }

    void Start()
    {
        // Selección visual inicial
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"Start - Highlighter: {(highlighter != null ? "OK" : "NULL")}");
#endif
        if (highlighter)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"Highlighter Count: {highlighter.Count}");
#endif
            }
        
        if (highlighter && highlighter.Count > 0) 
        {
            highlighter.SetSelected(0);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Selección inicial establecida en 0");
#endif
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("ERROR: Highlighter es null o no tiene filas registradas!");
#endif
        }
    }

    void Update()
    {
        if (builder == null) return;

        // Obtener controles del PlayerInputManager
        if (!ServiceLocator.TryGet(out Core.PlayerInputManager pim) || pim.Controls == null)
            return;

        var controls = pim.Controls;

        // --- D-Pad / stick VERTICAL para cambiar categoría (arriba/abajo) ---
        Vector2 nav = controls.UI.Navigate.ReadValue<Vector2>();
        
        // Abajo: categoría siguiente
        if (nav.y < -0.5f && _lastNavY >= -0.5f)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Abajo detectado");
            Debug.Log($"Highlighter null? {highlighter == null}");
#endif
            if (highlighter)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"Highlighter.Count = {highlighter.Count}");
                Debug.Log($"SelectedIndex actual = {highlighter.SelectedIndex}");
#endif
            }
            
            if (highlighter && highlighter.Count > 0)
            {
                int newIndex = highlighter.SelectedIndex + 1;
                if (newIndex >= highlighter.Count) newIndex = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"Intentando cambiar a índice: {newIndex}");
#endif
                highlighter.SetSelected(newIndex);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"Nueva selección: {newIndex}");
#endif
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("No se puede cambiar selección: highlighter null o sin filas");
#endif
            }
        }
        
        // Arriba: categoría anterior
        if (nav.y > 0.5f && _lastNavY <= 0.5f)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Arriba detectado");
#endif
            if (highlighter && highlighter.Count > 0)
            {
                int newIndex = highlighter.SelectedIndex - 1;
                if (newIndex < 0) newIndex = highlighter.Count - 1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"Intentando cambiar a índice: {newIndex}");
#endif
                highlighter.SetSelected(newIndex);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"Nueva selección: {newIndex}");
#endif
            }
        }
        
        _lastNavY = nav.y;

        // Categoría actual según highlight
        var cat = ui ? ui.CurrentHighlightedCategory() : PartCategory.Body;

        // --- A = Siguiente variante (derecha) ---
        if (controls.UI.Submit.WasPressedThisFrame())
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"A presionado - Next en {cat}");
#endif
            builder.Next(cat);
        }

        // --- X = Variante anterior (izquierda) ---
        if (controls.GamePlay.AttackMagicWest.WasPressedThisFrame())
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"X presionado - Prev en {cat}");
#endif
            builder.Prev(cat);
        }

        // --- B = Toggle on/off ---
        if (controls.UI.Cancel.WasPressedThisFrame())
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"B presionado - Toggle {cat}");
#endif
            var sel = builder.GetSelection();
            if (sel.ContainsKey(cat))
                builder.SetByName(cat, null);          // apagar
            else
                builder.SetByIndex(cat, 0);            // encender el primero
        }
    }
}