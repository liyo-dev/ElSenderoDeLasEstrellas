using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// Pestaña «Caídas» del LAB: vida y maná de cada personaje del grupo, y botones para dañarlos,
/// tumbarlos y levantarlos, y para darse el objeto que reanima. Para probar el grupo en combate
/// (INC-717): caídas, relevo del mando, Game Over solo si caen los tres y las cuatro formas de
/// levantarse. Solo existe en el LAB (la añade su arranque).
public sealed class CaidasDelLab : MonoBehaviour, ISeccionDelLab
{
    private const string IdDelObjeto = "plumaEstelar";

    public string Titulo => "Caídas";
    public int Orden => 35;

    public void Dibujar()
    {
        var grupo = SaludDelGrupo.Instance;
        if (grupo == null)
        {
            GUILayout.Label("No hay SaludDelGrupo en la escena (va en GrupoDelJugador).", EstiloDelLab.Nota);
            return;
        }

        GUILayout.Label("Cada personaje tiene su vida y su maná. Si cae el que llevas, el mando pasa a otro en pie; " +
                        "Game Over solo si caen los tres.", EstiloDelLab.Etiqueta);
        for (int i = 0; i < ControlDelJugador.Personajes; i++) Fila(grupo, (Slot)i);

        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Levantar a todos (al ganar)", EstiloDelLab.Boton, GUILayout.Width(240f)))
        {
            grupo.LevantarATodos(0.25f);
            PanelDelLab.Aviso("Caídos levantados");
        }
        if (GUILayout.Button("Darme 3 Plumas estelares", EstiloDelLab.Boton, GUILayout.Width(240f))) DarObjeto();
        GUILayout.EndHorizontal();
        GUILayout.Label("Formas de levantar a un caído: ponte a su lado y mantén A; usa una Pluma estelar en Start ▸ Objetos; " +
                        "lanza Brisa Sanadora encima; o gana el combate (se levantan con poca vida).", EstiloDelLab.Nota);
    }

    private static void Fila(SaludDelGrupo grupo, Slot p)
    {
        bool enGrupo = ControlDelJugador.EnElGrupo(p);
        bool activo = p == ControlDelJugador.Activo;
        string estado = !enGrupo ? "no está en el grupo" : grupo.Caido(p) ? "CAÍDO" : activo ? "al mando" : "con IA";
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{ControlDelJugador.Nombre(p)} ({estado}) · vida {grupo.Vida(p):0}/{grupo.VidaMax(p):0} · maná {grupo.Mana(p):0}/{grupo.ManaMax(p):0}",
                        EstiloDelLab.Etiqueta, GUILayout.Width(420f));
        GUI.enabled = enGrupo;
        if (GUILayout.Button("-30 %", EstiloDelLab.Boton, GUILayout.Width(70f))) grupo.Danar(p, 0.3f);
        if (GUILayout.Button("Tumbar", EstiloDelLab.Boton, GUILayout.Width(80f))) grupo.Tumbar(p);
        if (GUILayout.Button("Levantar", EstiloDelLab.Boton, GUILayout.Width(90f))) grupo.Levantar(p, 0.5f);
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    private static void DarObjeto()
    {
        var registro = ItemRegistrySO.LoadDefault();
        var objeto = registro != null ? registro.Get(IdDelObjeto) : null;
        if (objeto == null)
        {
            PanelDelLab.Aviso("Falta la Pluma estelar: pasa el menú «Grupo: montar caídas y revivir (INC-717)»");
            return;
        }
        if (!PlayerService.TryGetInventory(out var inventario) || inventario == null)
        {
            PanelDelLab.Aviso("No encuentro el inventario del grupo");
            return;
        }
        inventario.Add(objeto, 3);
        PanelDelLab.Aviso("3 Plumas estelares en el inventario");
    }
}
