using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Liyodev.Assistant
{
    /// El asistente con la cuenta del usuario en vez de con una clave de API: lanza el programa de
    /// terminal de su IA (Codex con ChatGPT, Antigravity con Google, Claude Code con Claude) en modo
    /// de una sola respuesta, le pasa todo por un archivo y lee lo que contesta. El programa ya está
    /// identificado con la cuenta del usuario, así que el uso sale de su plan, no de una API.
    ///
    /// Para gastar lo menos posible, el programa trabaja en una carpeta vacía (no tiene nada del
    /// proyecto que leer), sin herramientas o en solo lectura, y con la instrucción de contestar con
    /// lo que se le da.
    public static class AssistantCli
    {
        private const int TiempoMaximoMs = 240000;

        private static readonly ConcurrentQueue<Action> s_alHiloPrincipal = new();
        private static bool s_enganchado;

        /// El programa por defecto de cada cuenta.
        public static string DefaultCommand(AssistantSettings.Provider p) => p switch
        {
            AssistantSettings.Provider.ChatGptAccount => "codex",
            AssistantSettings.Provider.GoogleAccount => "agy",
            AssistantSettings.Provider.ClaudeAccount => "claude",
            _ => string.Empty,
        };

        public static void Send(AssistantSettings.Provider p, string system, IReadOnlyList<AssistantTurn> turns,
            Action<string> onAnswer, Action<string> onError)
        {
            string carpeta = CarpetaDeTrabajo();
            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            string entrada = Path.Combine(carpeta, $"peticion-{id}.txt");
            string salida = Path.Combine(carpeta, $"respuesta-{id}.txt");
            File.WriteAllText(entrada, Peticion(system, turns), new UTF8Encoding(false));

            string comando = AssistantSettings.Command(p);
            string modelo = AssistantSettings.Model(p);
            string argumentos = Argumentos(p, modelo, salida);

            Ejecutar(comando, argumentos, entrada, carpeta, (codigo, stdout, stderr) =>
            {
                try
                {
                    string respuesta = Leer(p, stdout, salida, out string error);
                    if (!string.IsNullOrWhiteSpace(respuesta)) onAnswer?.Invoke(respuesta);
                    else onError?.Invoke(error ?? Fallo(p, codigo, stderr));
                }
                catch (Exception e) { onError?.Invoke(e.Message); }
                finally
                {
                    Borrar(entrada);
                    Borrar(salida);
                }
            });
        }

        /// Comprueba que el programa está instalado y contesta: lanza «--version».
        public static void Comprobar(AssistantSettings.Provider p, Action<string> onOk, Action<string> onError)
        {
            string comando = AssistantSettings.Command(p);
            Ejecutar(comando, "--version", null, CarpetaDeTrabajo(), (codigo, stdout, stderr) =>
            {
                string version = (stdout ?? string.Empty).Trim();
                if (codigo == 0 && version.Length > 0) onOk?.Invoke(version);
                else onError?.Invoke(string.IsNullOrWhiteSpace(stderr)
                    ? AssistantText.T($"No encuentro «{comando}».", $"Cannot find “{comando}”.")
                    : Recortar(stderr.Trim(), 400));
            });
        }

        // ── La petición ─────────────────────────────────────────────────────────────────────

        /// Todo en un solo texto: las instrucciones de la herramienta y la conversación. El programa
        /// no guarda nada entre llamadas, así que cada petición lleva lo que hace falta y nada más.
        private static string Peticion(string system, IReadOnlyList<AssistantTurn> turns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<instructions>");
            sb.AppendLine(system);
            sb.AppendLine("</instructions>");
            sb.AppendLine();
            sb.AppendLine("<conversation>");
            foreach (var t in turns)
            {
                sb.AppendLine(t.FromUser ? "[user]" : "[assistant]");
                sb.AppendLine(t.Text);
            }
            sb.AppendLine("</conversation>");
            sb.AppendLine();
            sb.AppendLine("Reply to the last user message following the instructions. Do not run commands, " +
                          "read files or use any tool: everything you need is in this message.");
            return sb.ToString();
        }

        private static string Argumentos(AssistantSettings.Provider p, string modelo, string salida)
        {
            bool conModelo = !string.IsNullOrWhiteSpace(modelo);
            switch (p)
            {
                case AssistantSettings.Provider.ChatGptAccount:
                    // exec: una respuesta y sale. «-» lee la petición de la entrada estándar; la última
                    // respuesta queda en un archivo, sin el registro de lo que hace por el camino.
                    return "exec --skip-git-repo-check --sandbox read-only --color never" +
                           (conModelo ? " -m " + Comillas(modelo) : string.Empty) +
                           " --output-last-message " + Comillas(salida) + " -";
                case AssistantSettings.Provider.GoogleAccount:
                    // Antigravity: sin -p lee la petición de la entrada estándar (con -p la ignora).
                    return "--output-format json" + (conModelo ? " --model " + Comillas(modelo) : string.Empty);
                case AssistantSettings.Provider.ClaudeAccount:
                    return "-p --output-format json --tools \"\"" +
                           (conModelo ? " --model " + Comillas(modelo) : string.Empty);
                default:
                    return string.Empty;
            }
        }

        private static string Leer(AssistantSettings.Provider p, string stdout, string salida, out string error)
        {
            error = null;
            switch (p)
            {
                case AssistantSettings.Provider.ChatGptAccount:
                    if (File.Exists(salida))
                    {
                        string texto = File.ReadAllText(salida, Encoding.UTF8);
                        if (!string.IsNullOrWhiteSpace(texto)) return texto.Trim();
                    }
                    return null;

                case AssistantSettings.Provider.GoogleAccount:
                {
                    var json = JsonDe(stdout);
                    if (json == null) return (stdout ?? string.Empty).Trim();
                    if (json["error"] is JObject fallo)
                        error = (string)fallo["message"] ?? fallo.ToString();
                    else if (json["error"] is JValue texto && texto.Type == JTokenType.String)
                        error = (string)texto;
                    string estado = (string)json["status"];
                    if (error == null && !string.IsNullOrEmpty(estado) && estado != "SUCCESS")
                        error = estado;
                    return (string)json["response"];
                }

                case AssistantSettings.Provider.ClaudeAccount:
                {
                    var json = JsonDe(stdout);
                    if (json == null) return (stdout ?? string.Empty).Trim();
                    if ((bool?)json["is_error"] == true)
                    {
                        error = (string)json["result"] ?? json.ToString();
                        return null;
                    }
                    return (string)json["result"];
                }

                default:
                    return null;
            }
        }

        /// La salida en JSON, aunque el programa haya escrito avisos antes.
        private static JObject JsonDe(string stdout)
        {
            if (string.IsNullOrWhiteSpace(stdout)) return null;
            int inicio = stdout.IndexOf('{');
            int fin = stdout.LastIndexOf('}');
            if (inicio < 0 || fin <= inicio) return null;
            try { return JObject.Parse(stdout.Substring(inicio, fin - inicio + 1)); }
            catch (Exception) { return null; }
        }

        private static string Fallo(AssistantSettings.Provider p, int codigo, string stderr)
        {
            string detalle = string.IsNullOrWhiteSpace(stderr) ? $"código {codigo}" : Recortar(stderr.Trim(), 300);
            return AssistantText.T(
                $"Abre «Conectar mi IA» y pulsa «3. Probar» en {AssistantSettings.Name(p)}: si no contesta, repite el paso 2 (entrar con tu cuenta). (Detalle: {detalle})",
                $"Open “Connect my AI” and press “3. Test” on {AssistantSettings.Name(p)}: if it does not answer, repeat step 2 (sign in). (Details: {detalle})");
        }

        // ── El proceso ──────────────────────────────────────────────────────────────────────

        /// Lanza el programa por la terminal del sistema (en Windows los programas de npm son
        /// `.cmd`, y en macOS el PATH de npm solo lo tiene la terminal de inicio de sesión). Corre en
        /// otro hilo; el resultado vuelve al hilo del Editor.
        private static void Ejecutar(string comando, string argumentos, string archivoDeEntrada, string carpeta,
            Action<int, string, string> alAcabar)
        {
            Enganchar();
            string linea = Comillas(comando) + " " + argumentos +
                           (archivoDeEntrada != null ? " < " + Comillas(archivoDeEntrada) : string.Empty);

            var info = new ProcessStartInfo
            {
                WorkingDirectory = carpeta,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            info.EnvironmentVariables["PATH"] = PathActual();
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                info.FileName = "cmd.exe";
                info.Arguments = "/d /s /c \"" + linea + "\"";
            }
            else
            {
                info.FileName = File.Exists("/bin/zsh") ? "/bin/zsh" : "/bin/bash";
                info.Arguments = "-lc " + ComillasSimples(linea);
            }

            var hilo = new Thread(() =>
            {
                int codigo = -1;
                string stdout = string.Empty;
                string stderr = string.Empty;
                try
                {
                    using var proceso = Process.Start(info);
                    if (proceso == null) throw new InvalidOperationException("No se pudo lanzar el programa.");
                    var leerSalida = proceso.StandardOutput.ReadToEndAsync();
                    var leerErrores = proceso.StandardError.ReadToEndAsync();
                    if (!proceso.WaitForExit(TiempoMaximoMs))
                    {
                        Matar(proceso);
                        stderr = AssistantText.T("Tardó demasiado y se ha parado.", "It took too long and was stopped.");
                    }
                    else
                    {
                        proceso.WaitForExit();
                        codigo = proceso.ExitCode;
                        stdout = leerSalida.Result;
                        stderr = leerErrores.Result;
                    }
                }
                catch (Exception e)
                {
                    stderr = e.Message;
                }

                s_alHiloPrincipal.Enqueue(() => alAcabar(codigo, stdout, stderr));
            }) { IsBackground = true, Name = "Liyodev.Assistant.Cli" };
            hilo.Start();
        }

        private static void Matar(Process proceso)
        {
            try
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    using var taskkill = Process.Start(new ProcessStartInfo("taskkill", $"/T /F /PID {proceso.Id}")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    });
                    taskkill?.WaitForExit(5000);
                }
                else
                {
                    proceso.Kill();
                }
            }
            catch (Exception)
            {
                // Ya había terminado.
            }
        }

        private static void Enganchar()
        {
            if (s_enganchado) return;
            s_enganchado = true;
            EditorApplication.update += Vaciar;
        }

        private static void Vaciar()
        {
            while (s_alHiloPrincipal.TryDequeue(out var accion))
            {
                try { accion(); }
                catch (Exception e) { UnityEngine.Debug.LogException(e); }
            }
        }

        /// El PATH de ahora, no el que tenía Unity al abrirse: un programa recién instalado se
        /// añade al PATH del usuario y Unity no lo ve hasta reiniciarse.
        private static string PathActual()
        {
            string actual = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var partes = new List<string>();
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                try
                {
                    partes.Add(Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine));
                    partes.Add(Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User));
                }
                catch (Exception) { /* Sin acceso al registro: vale el de Unity. */ }
                string perfil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                partes.Add(Path.Combine(perfil, ".local", "bin"));
                partes.Add(actual);
                return string.Join(";", partes.FindAll(p => !string.IsNullOrEmpty(p)));
            }

            string casa = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            partes.Add(Path.Combine(casa, ".local", "bin"));
            partes.Add("/opt/homebrew/bin");
            partes.Add("/usr/local/bin");
            partes.Add(actual);
            return string.Join(":", partes.FindAll(p => !string.IsNullOrEmpty(p)));
        }

        /// Abre una terminal visible con una orden: la de instalar o la de entrar con la cuenta, que
        /// piden ver lo que pasa y, a veces, contestar algo.
        public static void AbrirTerminal(string ordenWindows, string ordenUnix)
        {
            try
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    // PowerShell en su propia ventana, con el PATH al día para lo recién instalado.
                    string refrescar = "$env:Path=[Environment]::GetEnvironmentVariable('Path','Machine')+';'+" +
                                       "[Environment]::GetEnvironmentVariable('Path','User')+';'+$env:USERPROFILE+'\\.local\\bin'; ";
                    Process.Start(new ProcessStartInfo("powershell.exe",
                        "-NoExit -ExecutionPolicy Bypass -Command \"" + refrescar + ordenWindows + "\"")
                    {
                        UseShellExecute = true,
                    });
                }
                else if (Application.platform == RuntimePlatform.OSXEditor)
                {
                    string orden = ordenUnix.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    Process.Start("osascript",
                        "-e 'tell application \"Terminal\" to do script \"" + orden + "\"' " +
                        "-e 'tell application \"Terminal\" to activate'");
                }
                else
                {
                    Process.Start("x-terminal-emulator", "-e bash -lc " + ComillasSimples(ordenUnix + "; exec bash"));
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(AssistantText.T("Conectar mi IA", "Connect my AI"),
                    AssistantText.T("No pude abrir una terminal. Ábrela tú y escribe:\n\n", "Could not open a terminal. Open one and type:\n\n") +
                    (Application.platform == RuntimePlatform.WindowsEditor ? ordenWindows : ordenUnix) + "\n\n" + e.Message, "OK");
            }
        }

        // ── Auxiliares ──────────────────────────────────────────────────────────────────────

        /// Una carpeta vacía dentro de Library: el programa no tiene nada del proyecto que leer.
        private static string CarpetaDeTrabajo()
        {
            string carpeta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "LiyodevAssistant", "cli");
            Directory.CreateDirectory(carpeta);
            return carpeta;
        }

        private static void Borrar(string ruta)
        {
            try { if (File.Exists(ruta)) File.Delete(ruta); }
            catch (Exception) { /* Lo borra la próxima limpieza de Library. */ }
        }

        private static string Comillas(string texto) => "\"" + (texto ?? string.Empty).Replace("\"", "\\\"") + "\"";

        private static string ComillasSimples(string texto) => "'" + (texto ?? string.Empty).Replace("'", "'\\''") + "'";

        private static string Recortar(string texto, int largo) =>
            texto.Length <= largo ? texto : "…" + texto.Substring(texto.Length - largo);
    }
}
