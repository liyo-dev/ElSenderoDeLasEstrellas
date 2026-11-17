using System;
using System.Linq;
using UnityEngine.UIElements;

namespace Liyodev.Assistant
{
    /// Los controles de servicio, clave, modelo y dirección, para que cualquier ventana de ajustes
    /// del asistente los pinte igual sin copiarlos.
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

            parent.Add(key);
            parent.Add(model);
            parent.Add(address);
            parent.Add(hint);
            parent.Add(openCodeHint);
            parent.Add(ollamaBox);

            void Fill()
            {
                var p = AssistantSettings.Current;
                key.SetValueWithoutNotify(AssistantSettings.Key(p));
                model.SetValueWithoutNotify(AssistantSettings.Model(p));
                address.SetValueWithoutNotify(AssistantSettings.Address);
                bool compatible = p == AssistantSettings.Provider.Compatible;
                bool openCode = p == AssistantSettings.Provider.OpenCode;
                bool ollama = p == AssistantSettings.Provider.Ollama;
                key.style.display = ollama ? DisplayStyle.None : DisplayStyle.Flex;
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
        }
    }
}
