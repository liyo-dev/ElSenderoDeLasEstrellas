#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// El catálogo de efectos que se pueden escribir en un guion («efecto Clase campo=valor»).
/// Lista todos los SequenceBeat con su descripción, sus campos y sus tooltips, y deja copiar una
/// línea base para pegar en el .guion.txt. Se alimenta de FabricaDeEfectos, la misma que usa el
/// horneador, así que no puede ofrecer algo que el horneado no sepa crear. Ver INC-1230.
public class CatalogoDeEfectosWindow : EditorWindow
{
    private string _busqueda = string.Empty;
    private Vector2 _scrollLista;
    private Vector2 _scrollDetalle;
    private Type _seleccionado;
    private string _linea = string.Empty;

    [MenuItem("El Sendero/Cinemáticas/Guion/Catálogo de efectos", false, 21)]
    public static void Abrir()
    {
        var ventana = GetWindow<CatalogoDeEfectosWindow>("Catálogo de efectos");
        ventana.minSize = new Vector2(820, 460);
        ventana.Show();
    }

    private void OnGUI()
    {
        DibujarCabecera();

        EditorGUILayout.BeginHorizontal();
        DibujarLista();
        DibujarDetalle();
        EditorGUILayout.EndHorizontal();
    }

    private void DibujarCabecera()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        _busqueda = GUILayout.TextField(_busqueda, EditorStyles.toolbarSearchField, GUILayout.Width(280));
        if (GUILayout.Button("Limpiar", EditorStyles.toolbarButton, GUILayout.Width(60)))
            _busqueda = string.Empty;
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"{FabricaDeEfectos.TiposDeCatalogo().Count()} efectos", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    private void DibujarLista()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(250)))
        {
            _scrollLista = EditorGUILayout.BeginScrollView(_scrollLista, GUILayout.ExpandHeight(true));
            foreach (Type tipo in FabricaDeEfectos.TiposDeCatalogo())
            {
                if (!Coincide(tipo)) continue;
                bool era = _seleccionado == tipo;
                bool ahora = GUILayout.Toggle(era, tipo.Name, "Button");
                if (ahora && !era) Seleccionar(tipo);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private bool Coincide(Type tipo)
        => string.IsNullOrWhiteSpace(_busqueda)
           || tipo.Name.IndexOf(_busqueda.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;

    private void Seleccionar(Type tipo)
    {
        _seleccionado = tipo;
        _linea = FabricaDeEfectos.LineaBase(tipo);
        GUI.FocusControl(null);
    }

    private void DibujarDetalle()
    {
        using (new EditorGUILayout.VerticalScope())
        {
            if (_seleccionado == null)
            {
                EditorGUILayout.HelpBox("Elige un efecto de la lista para ver qué hace y qué campos acepta.",
                    MessageType.Info);
                return;
            }

            _scrollDetalle = EditorGUILayout.BeginScrollView(_scrollDetalle);
            EditorGUILayout.LabelField(_seleccionado.Name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(Describir(_seleccionado), EditorStyles.wordWrappedLabel);

            if (typeof(IConfigurableDesdeGuion).IsAssignableFrom(_seleccionado))
                EditorGUILayout.HelpBox("Además de los campos, acepta claves propias del guion.",
                    MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Campos", EditorStyles.boldLabel);
            foreach (FieldInfo campo in FabricaDeEfectos.CamposDe(_seleccionado))
            {
                string ayuda = campo.GetCustomAttribute<TooltipAttribute>()?.tooltip;
                EditorGUILayout.LabelField($"{campo.Name}  ({NombreDeTipo(campo.FieldType)})",
                    ayuda ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Línea para el guion", EditorStyles.boldLabel);
            _linea = EditorGUILayout.TextArea(_linea, GUILayout.MinHeight(56));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copiar la línea", GUILayout.Height(26)))
            {
                EditorGUIUtility.systemCopyBuffer = _linea;
                ShowNotification(new GUIContent("Copiada"));
            }
            if (GUILayout.Button("Volver a los valores por defecto", GUILayout.Height(26)))
                _linea = FabricaDeEfectos.LineaBase(_seleccionado);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("Pega la línea en el HILO del guion. Los campos que no pongas se quedan " +
                                    "con su valor por defecto; «dura=» y «onlyIfFlag=» valen en cualquier efecto.",
                MessageType.None);
            EditorGUILayout.EndScrollView();
        }
    }

    private static string Describir(Type tipo)
    {
        try
        {
            var beat = (SequenceBeat)Activator.CreateInstance(tipo);
            return beat?.Describe() ?? string.Empty;
        }
        catch
        {
            return "(sin descripción)";
        }
    }

    private static string NombreDeTipo(Type tipo)
    {
        if (tipo == typeof(string)) return "texto";
        if (tipo == typeof(float)) return "número";
        if (tipo == typeof(int)) return "entero";
        if (tipo == typeof(bool)) return "sí/no";
        if (tipo.IsEnum) return string.Join("|", Enum.GetNames(tipo));
        if (tipo == typeof(Vector3)) return "x;y;z";
        if (tipo == typeof(Color)) return "color";
        if (tipo == typeof(List<string>)) return "lista con |";
        if (typeof(UnityEngine.Object).IsAssignableFrom(tipo)) return "asset (" + tipo.Name + ")";
        return tipo.Name;
    }
}
#endif
