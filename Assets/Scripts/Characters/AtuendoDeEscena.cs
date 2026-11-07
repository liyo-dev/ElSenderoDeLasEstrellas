using System;
using System.Collections.Generic;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Un juego de prendas por personaje que se pone encima de lo que lleva y se le devuelve al
/// quitarlo, sin pasar nunca a la partida guardada: el disfraz de un nivel, la ropa de una
/// ceremonia. Solo datos; lo ponen y lo quitan <see cref="CharacterAppearanceRegistry.PonerAtuendo"/>
/// y <see cref="CharacterAppearanceRegistry.QuitarAtuendo"/>, normalmente desde
/// <see cref="AtuendoEnTramo"/>.
/// </summary>
[CreateAssetMenu(menuName = "El Sendero/Personajes/Atuendo de escena", fileName = "Atuendo_")]
public sealed class AtuendoDeEscena : ScriptableObject
{
    [Serializable]
    public sealed class Tinte
    {
        [Tooltip("Nombre de la pieza en el modelo (p. ej. Body05).")]
        public string pieza;
        [Tooltip("Material que lleva la pieza mientras dura el atuendo: un duplicado teñido del suyo (TinteDeMaterial; en los shaders toon el color principal no tiñe, lo hace la textura).")]
        public Material material;
    }

    [Serializable]
    public sealed class Personaje
    {
        [Tooltip("Personaje al que viste esta parte del atuendo.")]
        public Slot personaje = Slot.Estela;
        [Tooltip("Prendas del armario que se pone. Una por categoría: si dos comparten categoría (p. ej. dos accesorios), se queda la última.")]
        public List<WardrobeItemSO> prendas = new();
        [Tooltip("Piezas del modelo por nombre, para lo que no tiene prenda en el armario. Nombre vacío = quita esa categoría.")]
        public List<AppearanceEntry> otrasPiezas = new();
        [Tooltip("Materiales que cambian mientras dura el atuendo (la pieza ha de llevarse puesta).")]
        public List<Tinte> tintes = new();
        [Tooltip("Lo que el atuendo cambia en su forma de moverse.")]
        public RasgosDeMovimiento rasgos;
        [Tooltip("Prendas hechas aparte con el mismo esqueleto (un vestido, una armadura): se ponen encima cambiando sus huesos por los del personaje (PrendasSueltas).")]
        public List<GameObject> prendasSueltas = new();
    }

    [SerializeField] private List<Personaje> personajes = new();

    public IReadOnlyList<Personaje> Personajes => personajes;

    /// <summary>La parte del atuendo de ese personaje; null si no lo viste.</summary>
    public Personaje De(Slot personaje)
    {
        for (int i = 0; i < personajes.Count; i++)
            if (personajes[i] != null && personajes[i].personaje == personaje) return personajes[i];
        return null;
    }

    /// <summary>Escribe en <paramref name="seleccion"/> las piezas que pone (o quita) a ese personaje.</summary>
    public void Vestir(Slot personaje, Dictionary<PartCategory, string> seleccion)
    {
        var parte = De(personaje);
        if (parte == null || seleccion == null) return;
        foreach (var prenda in parte.prendas)
            if (prenda != null && !string.IsNullOrEmpty(prenda.PartName))
                seleccion[prenda.Category] = prenda.PartName;
        foreach (var pieza in parte.otrasPiezas)
        {
            if (string.IsNullOrEmpty(pieza.partName)) seleccion.Remove(pieza.category);
            else seleccion[pieza.category] = pieza.partName;
        }
    }

#if UNITY_EDITOR
    // El modelo solo enseña una pieza por categoría: se avisa si el atuendo pide dos.
    private void OnValidate()
    {
        var vistas = new HashSet<PartCategory>();
        foreach (var parte in personajes)
        {
            if (parte == null) continue;
            vistas.Clear();
            foreach (var prenda in parte.prendas)
                if (prenda != null && !vistas.Add(prenda.Category))
                    Debug.LogWarning($"[AtuendoDeEscena] {name}: {parte.personaje} lleva dos prendas de la categoría {prenda.Category}; solo se verá la última.", this);
        }
    }
#endif
}
