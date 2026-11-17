using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Liyodev.Assistant
{
    /// Puente local para que clientes MCP lean y apliquen cambios en los contextos del asistente
    /// mientras Unity está abierto. Apagado por defecto y solo escucha en este ordenador.
    ///
    ///   GET  /assistant/contexts
    ///   GET  /assistant/manual?context=cinematics
    ///   GET  /assistant/state?context=cinematics
    ///   GET  /assistant/targets?context=cinematics
    ///   POST /assistant/select?context=cinematics   body: {"id":"..."}
    ///   POST /assistant/apply?context=cinematics    body: {"ops":[...]}
    [InitializeOnLoad]
    public static class AssistantBridge
    {
        public const int Port = 47822;
        const string EnabledKey = "Liyodev.Assistant.Bridge.Enabled";
        const string MenuPath = "Liyodev/Asistente: herramientas externas (puerto local)";

        static HttpListener s_listener;
        static Thread s_thread;
        static readonly ConcurrentQueue<Action> s_mainThread = new();

        static AssistantBridge()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            if (EditorPrefs.GetBool(EnabledKey, false)) Start();
        }

        /// Si las herramientas externas pueden conectar. Se guarda por usuario.
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, false);
            set
            {
                EditorPrefs.SetBool(EnabledKey, value);
                if (value) Start(); else Stop();
            }
        }

        public static bool Listening => s_listener != null && s_listener.IsListening;

        [MenuItem(MenuPath, false, 107)]
        static void ToggleMenu() => Enabled = !Enabled;

        [MenuItem(MenuPath, true, 107)]
        static bool ToggleMenuCheck()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(EnabledKey, false));
            return true;
        }

        static void Start()
        {
            if (s_listener != null) return;
            try
            {
                s_listener = new HttpListener();
                s_listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
                s_listener.Start();
                s_thread = new Thread(Listen) { IsBackground = true, Name = "Liyodev Assistant bridge" };
                s_thread.Start();
                EditorApplication.update -= Pump;
                EditorApplication.update += Pump;
                Debug.Log($"[Liyodev Assistant] Herramientas externas: escuchando en http://127.0.0.1:{Port}/");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Liyodev Assistant] No se pudo abrir el puerto {Port}: {e.Message}");
                s_listener = null;
            }
        }

        static void Stop()
        {
            try { s_listener?.Stop(); s_listener?.Close(); } catch (Exception) { }
            s_listener = null;
            EditorApplication.update -= Pump;
        }

        static void Listen()
        {
            while (s_listener != null && s_listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = s_listener.GetContext(); }
                catch (Exception) { return; }

                string body = string.Empty;
                if (ctx.Request.HasEntityBody)
                    using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                        body = reader.ReadToEnd();

                string answer = null;
                int status = 200;
                using var done = new ManualResetEventSlim(false);
                s_mainThread.Enqueue(() =>
                {
                    try
                    {
                        answer = Handle(ctx.Request.HttpMethod, ctx.Request.Url.AbsolutePath,
                            ctx.Request.QueryString["context"], body, out status);
                    }
                    catch (Exception e)
                    {
                        answer = e.Message;
                        status = 500;
                    }
                    done.Set();
                });
                if (!done.Wait(TimeSpan.FromSeconds(30))) { answer = "The editor is busy."; status = 503; }

                var bytes = Encoding.UTF8.GetBytes(answer ?? string.Empty);
                ctx.Response.StatusCode = status;
                ctx.Response.ContentType = "text/plain; charset=utf-8";
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                ctx.Response.Close();
            }
        }

        static void Pump()
        {
            while (s_mainThread.TryDequeue(out var action)) action();
        }

        static string Handle(string method, string path, string contextId, string body, out int status)
        {
            status = 200;
            if (path == "/assistant/contexts") return Contextos();

            var context = AssistantContexts.Find(contextId);
            if (context == null)
            {
                status = 404;
                return $"No assistant context '{contextId}'.";
            }

            switch (path)
            {
                case "/assistant/manual": return context.Manual();
                case "/assistant/state": return context.State();
                case "/assistant/targets": return Objetivos(context, out status);
                case "/assistant/select": return Seleccionar(context, body, out status);
                case "/assistant/apply": return Aplicar(context, body);
                default:
                    status = 404;
                    return $"Unknown path '{path}'.";
            }
        }

        static string Contextos()
        {
            var array = new JArray();
            foreach (var context in AssistantContexts.All)
            {
                array.Add(new JObject
                {
                    ["id"] = context.Id,
                    ["name"] = context.Name,
                    ["target"] = context.TargetName,
                    ["hasTarget"] = context.HasTarget,
                    ["selectable"] = context is IAssistantContextSelection,
                });
            }
            return new JObject { ["contexts"] = array }.ToString();
        }

        static string Objetivos(IAssistantContext context, out int status)
        {
            status = 200;
            if (!(context is IAssistantContextSelection selection))
            {
                status = 400;
                return "That context has no selectable targets.";
            }

            var array = new JArray();
            foreach (var target in selection.Targets())
                array.Add(new JObject { ["id"] = target.id, ["name"] = target.name });
            return new JObject { ["targets"] = array }.ToString();
        }

        static string Seleccionar(IAssistantContext context, string body, out int status)
        {
            status = 200;
            if (!(context is IAssistantContextSelection selection))
            {
                status = 400;
                return "That context has no selectable targets.";
            }

            string id;
            try { id = (string)JObject.Parse(string.IsNullOrEmpty(body) ? "{}" : body)["id"]; }
            catch (Exception e)
            {
                status = 400;
                return "Invalid JSON: " + e.Message;
            }

            if (string.IsNullOrEmpty(id))
            {
                status = 400;
                return "Missing 'id'.";
            }

            bool selected = selection.Select(id);
            return new JObject { ["selected"] = selected, ["id"] = id }.ToString();
        }

        static string Aplicar(IAssistantContext context, string body)
        {
            var result = context.Apply(string.IsNullOrEmpty(body) ? "{}" : body);
            return new JObject
            {
                ["applied"] = result.Applied,
                ["done"] = new JArray(result.Done),
                ["diff"] = new JArray(result.Diff),
                ["warnings"] = new JArray(result.Warnings),
                ["errors"] = new JArray(result.Errors),
            }.ToString();
        }
    }
}
