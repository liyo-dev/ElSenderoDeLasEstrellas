using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liyodev.Assistant
{
    /// «Conectar mi IA»: elegir la IA que ya se tiene (ChatGPT, Google o Claude), instalar su programa
    /// con el instalador oficial (sin Node ni npm), entrar con la cuenta y probar que contesta. Al
    /// acabar, el asistente queda usando esa cuenta, sin clave de API.
    public sealed class AssistantConnectWindow : EditorWindow
    {
        private sealed class Opcion
        {
            public AssistantSettings.Provider proveedor;
            public string titulo;
            public string precio;
            public string instalarWindows;
            public string instalarUnix;
            public string entrar;
            public string comoEntrar;
            public Label estado;
            public Button usar;
        }

        private readonly List<Opcion> _opciones = new();

        [MenuItem("Liyodev/Conectar mi IA", false, 21)]
        public static void Open()
        {
            var ventana = GetWindow<AssistantConnectWindow>(true, AssistantText.T("Conectar mi IA", "Connect my AI"));
            ventana.minSize = new Vector2(560f, 600f);
            ventana.Show();
        }

        private void OnFocus()
        {
            // Al volver de la terminal, mirar otra vez qué está instalado.
            foreach (var opcion in _opciones) MirarInstalado(opcion);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.AddToClassList("assistant-root");
            string hojaDeEstilo = AssistantPaths.StyleSheet;
            var hoja = string.IsNullOrEmpty(hojaDeEstilo) ? null : AssetDatabase.LoadAssetAtPath<StyleSheet>(hojaDeEstilo);
            if (hoja != null) root.styleSheets.Add(hoja);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.paddingLeft = 14;
            scroll.style.paddingRight = 14;
            scroll.style.paddingTop = 12;
            root.Add(scroll);

            var titulo = new Label(AssistantText.T("Conectar mi IA", "Connect my AI"));
            titulo.style.fontSize = 16;
            titulo.style.unityFontStyleAndWeight = FontStyle.Bold;
            scroll.Add(titulo);

            var intro = new Label(AssistantText.T(
                "Conecta el asistente con la IA que ya usas: ChatGPT, Google o Claude. Entras con tu cuenta de siempre, " +
                "sin claves ni configuraciones raras. Solo hay que hacerlo una vez: sigue los pasos 1, 2 y 3 de la que quieras.",
                "Connect the assistant to the AI you already use: ChatGPT, Google or Claude. You sign in with your usual account, " +
                "no keys or odd settings. You only do it once: follow steps 1, 2 and 3 of the one you want."));
            intro.AddToClassList("story-muted");
            intro.style.whiteSpace = WhiteSpace.Normal;
            intro.style.marginTop = 4;
            intro.style.marginBottom = 10;
            scroll.Add(intro);

            _opciones.Clear();
            _opciones.Add(new Opcion
            {
                proveedor = AssistantSettings.Provider.ChatGptAccount,
                titulo = "ChatGPT",
                precio = AssistantText.T("Vale cualquier cuenta de ChatGPT, también la gratuita.",
                                         "Any ChatGPT account works, the free one too."),
                instalarWindows = "irm https://chatgpt.com/codex/install.ps1 | iex",
                instalarUnix = "curl -fsSL https://chatgpt.com/codex/install.sh | sh",
                entrar = "codex login",
                comoEntrar = AssistantText.T("Se abrirá tu navegador: entra con tu cuenta de ChatGPT. Después cierra la ventana negra y pulsa «3. Probar».",
                                             "Your browser will open: sign in with your ChatGPT account. Then close the black window and press “3. Test”."),
            });
            _opciones.Add(new Opcion
            {
                proveedor = AssistantSettings.Provider.GoogleAccount,
                titulo = "Google (Gemini)",
                precio = AssistantText.T("Gratis con tu cuenta de Google, pero solo unas pocas preguntas al día. Si no pagas Google AI, Google puede usar lo que le envíes para mejorar su IA.",
                                         "Free with your Google account, but only a few questions a day. If you do not pay for Google AI, Google may use what you send to improve its AI."),
                instalarWindows = "irm https://antigravity.google/cli/install.ps1 | iex",
                instalarUnix = "curl -fsSL https://antigravity.google/cli/install.sh | bash",
                entrar = "agy",
                comoEntrar = AssistantText.T("Se abrirá tu navegador: entra con tu cuenta de Google. Cuando la ventana negra te deje escribir, escribe /exit, ciérrala y pulsa «3. Probar».",
                                             "Your browser will open: sign in with Google. When the black window lets you type, type /exit, close it and press “3. Test”."),
            });
            _opciones.Add(new Opcion
            {
                proveedor = AssistantSettings.Provider.ClaudeAccount,
                titulo = "Claude",
                precio = AssistantText.T("Necesitas Claude Pro o superior. Con la cuenta gratuita de Claude no funciona.",
                                         "You need Claude Pro or higher. It does not work with the free Claude account."),
                instalarWindows = "irm https://claude.ai/install.ps1 | iex",
                instalarUnix = "curl -fsSL https://claude.ai/install.sh | bash",
                entrar = "claude",
                comoEntrar = AssistantText.T("Se abrirá tu navegador: entra con tu cuenta de Claude. Cuando la ventana negra te deje escribir, escribe /exit, ciérrala y pulsa «3. Probar».",
                                             "Your browser will open: sign in with your Claude account. When the black window lets you type, type /exit, close it and press “3. Test”."),
            });

            foreach (var opcion in _opciones) scroll.Add(Tarjeta(opcion));

            var pie = new Label(AssistantText.T(
                "Lo que se instala es el programa oficial de cada empresa.",
                "What gets installed is each company's official program."));
            pie.AddToClassList("story-muted");
            pie.style.whiteSpace = WhiteSpace.Normal;
            pie.style.marginTop = 2;
            pie.style.marginBottom = 8;
            scroll.Add(pie);

            // Para quien ya trabaja con una clave de API o con una IA en su ordenador (Ollama).
            var otras = new Foldout
            {
                text = AssistantText.T("Otras formas: clave de API o IA en tu ordenador", "Other ways: API key or AI on your computer"),
                value = false,
            };
            otras.style.marginBottom = 12;
            AssistantSettingsUI.BuildSection(otras);
            scroll.Add(otras);

            foreach (var opcion in _opciones) MirarInstalado(opcion);
        }

        private VisualElement Tarjeta(Opcion opcion)
        {
            var tarjeta = new VisualElement();
            tarjeta.style.borderTopWidth = tarjeta.style.borderBottomWidth = tarjeta.style.borderLeftWidth = tarjeta.style.borderRightWidth = 1;
            var borde = new Color(0.21f, 0.24f, 0.31f);
            tarjeta.style.borderTopColor = tarjeta.style.borderBottomColor = tarjeta.style.borderLeftColor = tarjeta.style.borderRightColor = borde;
            tarjeta.style.borderTopLeftRadius = tarjeta.style.borderTopRightRadius = tarjeta.style.borderBottomLeftRadius = tarjeta.style.borderBottomRightRadius = 6;
            tarjeta.style.paddingLeft = tarjeta.style.paddingRight = 12;
            tarjeta.style.paddingTop = tarjeta.style.paddingBottom = 10;
            tarjeta.style.marginBottom = 10;
            if (AssistantSettings.Current == opcion.proveedor)
                tarjeta.style.backgroundColor = new Color(0.13f, 0.18f, 0.25f);

            var cabecera = new Label(opcion.titulo);
            cabecera.style.unityFontStyleAndWeight = FontStyle.Bold;
            cabecera.style.fontSize = 13;
            tarjeta.Add(cabecera);

            var precio = new Label(opcion.precio);
            precio.AddToClassList("story-muted");
            precio.style.whiteSpace = WhiteSpace.Normal;
            precio.style.marginBottom = 6;
            tarjeta.Add(precio);

            opcion.estado = new Label(AssistantText.T("Mirando si ya está en tu ordenador…", "Checking whether it is already on your computer…"));
            opcion.estado.style.whiteSpace = WhiteSpace.Normal;
            opcion.estado.style.marginBottom = 6;
            tarjeta.Add(opcion.estado);

            var botones = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };

            var instalar = new Button(() =>
            {
                AssistantCli.AbrirTerminal(opcion.instalarWindows, opcion.instalarUnix);
                opcion.estado.text = AssistantText.T(
                    "Se ha abierto una ventana negra que lo instala. Cuando termine, ciérrala y vuelve aquí.",
                    "A black window has opened to install it. When it finishes, close it and come back here.");
            })
            { text = AssistantText.T("1. Instalar", "1. Install") };
            var entrar = new Button(() =>
            {
                AssistantCli.AbrirTerminal(opcion.entrar, opcion.entrar);
                opcion.estado.text = opcion.comoEntrar;
            })
            { text = AssistantText.T("2. Entrar con mi cuenta", "2. Sign in") };
            Button probar = null;
            probar = new Button(() => Probar(opcion, probar)) { text = AssistantText.T("3. Probar", "3. Test") };
            bool enUso = AssistantSettings.Current == opcion.proveedor;
            opcion.usar = new Button(() => Usar(opcion))
            {
                text = enUso ? AssistantText.T("En uso ✓", "In use ✓") : AssistantText.T("Usar esta", "Use this one"),
            };
            opcion.usar.SetEnabled(!enUso);
            opcion.usar.AddToClassList("is-primary");

            foreach (var boton in new[] { instalar, entrar, probar, opcion.usar })
            {
                boton.AddToClassList("story-button");
                boton.style.marginRight = 6;
                boton.style.marginBottom = 4;
                botones.Add(boton);
            }
            tarjeta.Add(botones);
            return tarjeta;
        }

        private void MirarInstalado(Opcion opcion)
        {
            if (opcion.estado == null) return;
            AssistantCli.Comprobar(opcion.proveedor, _ =>
            {
                if (opcion.estado == null) return;
                opcion.estado.text = AssistantSettings.Verified(opcion.proveedor)
                    ? (AssistantSettings.Current == opcion.proveedor
                        ? AssistantText.T("Lista ✓ Es la que usa el asistente.", "Ready ✓ It is the one the assistant uses.")
                        : AssistantText.T("Lista ✓ Funciona con tu cuenta.", "Ready ✓ It works with your account."))
                    : AssistantText.T("Ya está en tu ordenador. Ahora pulsa «2. Entrar con mi cuenta» y después «3. Probar».",
                                      "It is already on your computer. Now press “2. Sign in” and then “3. Test”.");
            }, _ =>
            {
                if (opcion.estado == null) return;
                AssistantSettings.SetVerified(opcion.proveedor, false);
                opcion.estado.text = AssistantText.T("Aún no está en tu ordenador. Empieza por «1. Instalar».",
                                                     "It is not on your computer yet. Start with “1. Install”.");
            });
        }

        /// Una pregunta de verdad, la más corta posible: prueba de una vez programa, cuenta y respuesta.
        private void Probar(Opcion opcion, Button boton)
        {
            boton.SetEnabled(false);
            opcion.estado.text = AssistantText.T("Haciendo una pregunta de prueba…", "Asking a test question…");
            var turnos = new List<AssistantTurn> { new(true, "Reply with exactly: OK") };
            AssistantCli.Send(opcion.proveedor, "You are a connection test. Reply with exactly: OK", turnos, respuesta =>
            {
                boton.SetEnabled(true);
                AssistantSettings.SetVerified(opcion.proveedor, true);
                opcion.estado.text = AssistantSettings.Current == opcion.proveedor
                    ? AssistantText.T("Lista ✓ Ha contestado y es la que usa el asistente.", "Ready ✓ It answered and it is the one the assistant uses.")
                    : AssistantText.T("Lista ✓ Ha contestado. Pulsa «Usar esta».", "Ready ✓ It answered. Press “Use this one”.");
            }, error =>
            {
                boton.SetEnabled(true);
                AssistantSettings.SetVerified(opcion.proveedor, false);
                opcion.estado.text = AssistantText.T(
                    "No ha contestado. Haz el paso 2 (entrar con tu cuenta) y vuelve a probar. Si sigue igual, repite el paso 1.",
                    "It did not answer. Do step 2 (sign in) and test again. If nothing changes, repeat step 1.");
                opcion.estado.tooltip = error;
            });
        }

        private void Usar(Opcion opcion)
        {
            AssistantSettings.Current = opcion.proveedor;
            ShowNotification(new GUIContent(AssistantText.T("Listo: el asistente usa ", "Done: the assistant uses ") + opcion.titulo));
            rootVisualElement.Clear();
            CreateGUI();
        }
    }
}
