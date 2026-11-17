using System;
using System.Collections.Generic;
using Liyodev.Assistant;
using Newtonsoft.Json.Linq;

namespace Liyodev.Cinematics
{
    /// El contexto del Director para el chat compartido. En esta fase solo lee: el manual del
    /// guion y el estado de la cinemática abierta (encargos, informe y guion numerado).
    public sealed class CinematicsAssistantContext : IAssistantContext, IAssistantContextSelection
    {
        public CinematicsAssistantContext()
        {
            DirectorWindow.SeleccionCambiada += NotifyChanged;
        }

        public string Id => "cinematics";
        public string Name => AssistantText.T("Director", "Director");
        public string TargetName => DirectorWindow.CinematicaActivaId ?? string.Empty;
        public bool HasTarget => !string.IsNullOrEmpty(DirectorWindow.CinematicaActivaId);

        public string Placeholder => AssistantText.T(
            "¿Qué quieres corregir en esta cinemática?",
            "What do you want to fix in this cinematic?");

        public string Intro => AssistantText.T(
            "Abre una cinemática en el Director y pregunta por ella. El asistente lee el guion, el último horneado y los encargos pendientes, y puede proponer cambios que tú aplicas.",
            "Open a cinematic in the Director and ask about it. The assistant reads the guion, the last bake and the pending tasks, and can propose changes you apply.");

        public event Action Changed;

        public string RolePrompt() =>
            "You help a film director work on a cinematic of a Unity game. The guion is a text file (.guion.txt) and is the only source of truth; the scene, the baked data and the storyboard are generated from it. Answer in the language the user writes in, briefly.\n" +
            "To propose changes, end your answer with ONE fenced json block {\"ops\":[...]}; the user reviews it and applies it or not. Operations:\n" +
            "- set_options {line, match, options:[{name,value}]}: change or add options on that line; an empty value removes the option.\n" +
            "- replace_line {line, match, text}: replace the whole line (a trailing # comment is kept if the new text has none).\n" +
            "- insert_after {line|anchor, text} / insert_before {line|anchor, text}: insert one or more lines; anchor can be a section (PUNTOS) or a label (@inicio).\n" +
            "- delete_line {line, match}: remove the line.\n" +
            "- resolve_task {task:\"P06\"|\"all\"}: close those pending tasks after the change.\n" +
            "Lines are the L... from the state, on the original text. Use match whenever you change a line, so the game can check it is still the same line. Apply the minimum change and use only the aliases, points, labels, animation states and beat fields from the manual and the state. Cite the line and the shot. The game writes the guion, bakes it and closes the tasks only if the bake accepts the change.";

        public string Manual()
        {
            var bridge = CinematicsEditorServices.Bridge;
            return bridge != null ? bridge.ManualDeGuion() : "No hay puente de cinemáticas en este proyecto.";
        }

        public string State()
        {
            var bridge = CinematicsEditorServices.Bridge;
            if (bridge == null) return "No hay puente de cinemáticas en este proyecto.";
            string id = DirectorWindow.CinematicaActivaId;
            if (string.IsNullOrEmpty(id)) return "No hay ninguna cinemática abierta en el Director.";
            return bridge.EstadoParaAsistente(id, DirectorWindow.PlanoActivo, DirectorWindow.SegundoActivo);
        }

        public string DescribeOp(JObject op)
        {
            if (op == null) return "?";
            string o = (string)op["op"];
            return o switch
            {
                "set_options" => AssistantText.T(
                    $"cambiar opciones de la línea {(string)op["line"]}",
                    $"change options on line {(string)op["line"]}"),
                "replace_line" => AssistantText.T(
                    $"reescribir la línea {(string)op["line"]}",
                    $"rewrite line {(string)op["line"]}"),
                "insert_after" => AssistantText.T("insertar una línea después", "insert a line after"),
                "insert_before" => AssistantText.T("insertar una línea antes", "insert a line before"),
                "delete_line" => AssistantText.T(
                    $"borrar la línea {(string)op["line"]}",
                    $"delete line {(string)op["line"]}"),
                "resolve_task" => AssistantText.T(
                    $"cerrar el encargo {(string)op["task"]}",
                    $"close task {(string)op["task"]}"),
                _ => o,
            };
        }

        public AssistantApplyResult Apply(string json)
        {
            var result = new AssistantApplyResult();
            var bridge = CinematicsEditorServices.Bridge;
            if (bridge == null)
            {
                result.Errors.Add(AssistantText.T(
                    "No hay puente de cinemáticas en este proyecto.",
                    "There is no cinematics bridge in this project."));
                return result;
            }

            string id = DirectorWindow.CinematicaActivaId;
            if (string.IsNullOrEmpty(id))
            {
                result.Errors.Add(AssistantText.T(
                    "No hay ninguna cinemática abierta en el Director.",
                    "No cinematic is open in the Director."));
                return result;
            }

            var edicion = bridge.AplicarEdicionDeGuion(id, json);
            result.Applied = edicion.aplicado;
            if (!string.IsNullOrEmpty(edicion.resumen))
            {
                if (edicion.aplicado) result.Done.Add(edicion.resumen);
                else result.Errors.Add(edicion.resumen);
            }
            result.Done.AddRange(edicion.hechos);
            result.Diff.AddRange(edicion.diff);
            result.Warnings.AddRange(edicion.avisos);
            result.Errors.AddRange(edicion.errores);
            if (result.Applied) NotifyChanged();
            return result;
        }

        public string QuickActionLabel => AssistantText.T("Resolver encargos", "Resolve tasks");

        public string QuickActionPrompt() =>
            "Resuelve los encargos pendientes de esta cinemática (la lista está en el estado). Lee el estado y el guion, haz los cambios mínimos y termina con un único bloque {\"ops\":[...]}. Incluye resolve_task para cada Pxx que hayas resuelto de verdad; no cierres uno que no hayas podido resolver y explica por qué. Si no hay encargos pendientes, dilo y no emitas ops.";

        public IReadOnlyList<AssistantTarget> Targets()
        {
            var lista = new List<AssistantTarget>();
            var bridge = CinematicsEditorServices.Bridge;
            if (bridge == null) return lista;

            try
            {
                foreach (var cine in bridge.Listar())
                    lista.Add(new AssistantTarget { id = cine.id, name = cine.nombre + " · " + cine.escena });
            }
            catch (Exception)
            {
                // Si el puente falla, el MCP recibe una lista vacía en vez de un error de red.
            }
            return lista;
        }

        public bool Select(string id) => DirectorWindow.SeleccionarPorId(id);

        public void NotifyChanged() => Changed?.Invoke();
    }
}
