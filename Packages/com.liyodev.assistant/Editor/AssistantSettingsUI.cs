using System;
using System.Linq;
using UnityEngine.UIElements;

namespace Liyodev.Assistant
{
    /// Los controles de servicio, clave, modelo y dirección (las «otras formas» de conectar la IA),
    /// para que cualquier ventana de ajustes los pinte igual sin copiarlos.
    public static class AssistantSettingsUI
    {
        public static void BuildSection(VisualElement parent)
        {

            var providers = Enum.GetValues(typeof(AssistantSettings.Provider))
                .Cast<AssistantSettings.Provider>().ToList();
            var provider = new DropdownField(
                AssistantText.T("Servicio", "Service"),
                providers.Select(AssistantSettings.Name).ToList(),
                providers.IndexOf(AssistantSettings.Current));
            parent.Add(provider);

            var key = new TextField(AssistantText.T("Clave de API", "API key")) { isPasswordField = true };
            var model = new TextField(AssistantText.T("Modelo", "Model"));
            var address = new TextField(AssistantText.T("Dirección", "Address"));
            var hint = new Label(AssistantText.T(
                "Cualquier servicio con el formato de OpenAI. Ollama en tu ordenador: http://localhost:11434/v1 (sin clave).",
                "Any service with the OpenAI format. Ollama on your computer: http://localhost:11434/v1 (no key)."));
            hint.AddToClassList("story-muted");
            var openCodeHint = new Label(AssistantText.T(
                "OpenCode: usa la clave del Console (Go o saldo) y el id exacto del modelo de https://opencode.ai/inference/v1/models (p. ej. deepseek-v4.1-flash).",
                "OpenCode: use your Console key (Go or balance) and the exact model id from https://opencode.ai/inference/v1/models (e.g. deepseek-v4.1-flash)."));
            openCodeHint.AddToClassList("story-muted");

            var ollamaEstado = new Label();
            ollamaEstado.AddToClassList("story-muted");
            var ollamaBox = new VisualElement();
            var ollamaHint = new Label(AssistantText.T(
                "Ollama: instala Ollama (ollama.com) y ejecuta «ollama pull qwen3:8b». Sin clave; el servidor tiene que estar en marcha.",
                "Ollama: install Ollama (ollama.com) and run “ollama pull qwen3:8b”. No key; the server must be running."));
            ollamaHint.AddToClassList("story-muted");
            Button probar = null;
            probar = new Button(() =>
            {
                probar.SetEnabled(false);
                ollamaEstado.text = AssistantText.T("Buscando…", "Looking…");
                AssistantOllama.ListarModelos(modelos =>
                {
                    probar.SetEnabled(true);
                    if (modelos == null || modelos.Count == 0)
                    {
                        ollamaEstado.text = AssistantText.T(
                            "No hay modelos: ejecuta «ollama pull qwen3:8b».",
                            "No models: run “ollama pull qwen3:8b”.");
                        return;
                    }
                    if (!modelos.Contains(model.value))
                    {
                        model.SetValueWithoutNotify(modelos[0]);
                        AssistantSettings.SetModel(AssistantSettings.Current, modelos[0]);
                    }
                    ollamaEstado.text = AssistantText.T("Modelos: ", "Models: ") + string.Join(", ", modelos);
                }, error =>
                {
                    probar.SetEnabled(true);
                    ollamaEstado.text = AssistantText.T("No pude conectar con Ollama: ", "Could not reach Ollama: ") + error +
                        AssistantText.T(". ¿Está en marcha en http://localhost:11434?", ". Is it running at http://localhost:11434?");
                });
            })
            { text = AssistantText.T("Ver modelos instalados", "List installed models") };
            probar.AddToClassList("story-button");
            probar.style.marginTop = 6;
            ollamaBox.Add(ollamaHint);
            ollamaBox.Add(probar);
            ollamaBox.Add(ollamaEstado);

            // Cuentas (ChatGPT, Google, Claude) por su programa de terminal: sin clave.
            var cuentaBox = new VisualElement();
            var cuentaHint = new Label();
            cuentaHint.AddToClassList("story-muted");
            cuentaHint.style.whiteSpace = WhiteSpace.Normal;
            var comando = new TextField(AssistantText.T("Programa", "Program"))
            {
                tooltip = AssistantText.T(
                    "El nombre del programa (codex, gemini, claude) o su ruta entera si la terminal no lo encuentra.",
                    "The program name (codex, gemini, claude) or its full path if the terminal cannot find it."),
            };
            var cuentaEstado = new Label();
            cuentaEstado.AddToClassList("story-muted");
            cuentaEstado.style.whiteSpace = WhiteSpace.Normal;
            Button comprobar = null;
            comprobar = new Button(() =>
            {
                var actual = AssistantSettings.Current;
                AssistantSettings.SetCommand(actual, comando.value);
                comprobar.SetEnabled(false);
                cuentaEstado.text = AssistantText.T("Comprobando…", "Checking…");
                AssistantCli.Comprobar(actual, version =>
                {
                    comprobar.SetEnabled(true);
                    cuentaEstado.text = AssistantText.T("Instalado: ", "Installed: ") + version +
                        AssistantText.T(". Si aún no has entrado con tu cuenta, abre una terminal y ejecútalo una vez.",
                                        ". If you have not signed in yet, open a terminal and run it once.");
                }, error =>
                {
                    comprobar.SetEnabled(true);
                    cuentaEstado.text = error;
                });
            })
            { text = AssistantText.T("Comprobar", "Check") };
            comprobar.AddToClassList("story-button");
            comprobar.style.marginTop = 6;
            cuentaBox.Add(cuentaHint);
            cuentaBox.Add(comando);
            cuentaBox.Add(comprobar);
            cuentaBox.Add(cuentaEstado);

            parent.Add(key);
            parent.Add(model);
            parent.Add(address);
            parent.Add(hint);
            parent.Add(openCodeHint);
            parent.Add(ollamaBox);
            parent.Add(cuentaBox);

            void Fill()
            {
                var p = AssistantSettings.Current;
                key.SetValueWithoutNotify(AssistantSettings.Key(p));
                model.SetValueWithoutNotify(AssistantSettings.Model(p));
                address.SetValueWithoutNotify(AssistantSettings.Address);
                bool compatible = p == AssistantSettings.Provider.Compatible;
                bool openCode = p == AssistantSettings.Provider.OpenCode;
                bool ollama = p == AssistantSettings.Provider.Ollama;
                bool cuenta = AssistantSettings.IsAccount(p);
                key.style.display = ollama || cuenta ? DisplayStyle.None : DisplayStyle.Flex;
                model.tooltip = cuenta
                    ? AssistantText.T("Vacío: el modelo que tenga elegido tu cuenta.", "Empty: the model your account uses.")
                    : string.Empty;
                cuentaBox.style.display = cuenta ? DisplayStyle.Flex : DisplayStyle.None;
                comando.SetValueWithoutNotify(cuenta ? AssistantSettings.Command(p) : string.Empty);
                cuentaEstado.text = string.Empty;
                cuentaHint.text = HintDeCuenta(p);
                address.style.display = compatible ? DisplayStyle.Flex : DisplayStyle.None;
                hint.style.display = compatible ? DisplayStyle.Flex : DisplayStyle.None;
                openCodeHint.style.display = openCode ? DisplayStyle.Flex : DisplayStyle.None;
                ollamaBox.style.display = ollama ? DisplayStyle.Flex : DisplayStyle.None;
            }

            Fill();
            provider.RegisterValueChangedCallback(_ =>
            {
                AssistantSettings.Current = providers[provider.index];
                Fill();
            });
            key.RegisterCallback<FocusOutEvent>(_ => AssistantSettings.SetKey(AssistantSettings.Current, key.value));
            model.RegisterCallback<FocusOutEvent>(_ => AssistantSettings.SetModel(AssistantSettings.Current, model.value));
            address.RegisterCallback<FocusOutEvent>(_ => AssistantSettings.Address = address.value);
            comando.RegisterCallback<FocusOutEvent>(_ => AssistantSettings.SetCommand(AssistantSettings.Current, comando.value));
        }

        /// Qué da cada cuenta. Instalar y entrar se hace desde «Conectar mi IA».
        private static string HintDeCuenta(AssistantSettings.Provider p) => p switch
        {
            AssistantSettings.Provider.ChatGptAccount => AssistantText.T(
                "Usa tu plan de ChatGPT, también el gratuito, con sus límites. Para instalar Codex y entrar con tu cuenta: «Conectar mi IA».",
                "Uses your ChatGPT plan, the free one too, within its limits. To install Codex and sign in: “Connect my AI”."),
            AssistantSettings.Provider.GoogleAccount => AssistantText.T(
                "Gratis con tu cuenta de Google, con un límite diario pequeño (más con Google AI). Para instalar Antigravity y entrar: «Conectar mi IA». En el plan gratuito Google puede usar lo que envías para entrenar sus modelos.",
                "Free with your Google account, with a small daily limit (more with Google AI). To install Antigravity and sign in: “Connect my AI”. On the free tier Google may use what you send to train its models."),
            AssistantSettings.Provider.ClaudeAccount => AssistantText.T(
                "Usa tu plan de Claude (Pro o superior). Para instalar Claude Code y entrar con tu cuenta: «Conectar mi IA».",
                "Uses your Claude plan (Pro or higher). To install Claude Code and sign in: “Connect my AI”."),
            _ => string.Empty,
        };
    }
}
