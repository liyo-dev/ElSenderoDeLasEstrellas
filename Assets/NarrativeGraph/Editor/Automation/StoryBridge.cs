using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace NarrativeGraphEditor
{
    /// Local bridge so outside tools (the MCP server in Tools~/mcp, scripts) can read and change
    /// graphs while the editor is open. Off by default; it only listens on this computer.
    ///
    ///   GET  /graphs                 names of the graphs
    ///   GET  /chapter?graph=Cap3     the chapter as text
    ///   GET  /manual                 every step type
    ///   POST /edit?graph=Cap3        body: batch of operations (see StoryBatchEdit)
    [InitializeOnLoad]
    public static class StoryBridge
    {
        public const int Port = 47821;
        const string EnabledKey = "NarrativeGraph.Bridge.Enabled";
        const string MenuPath = "Tools/Narrative Graph/Permitir herramientas externas (puerto local)";

        static HttpListener s_listener;
        static Thread s_thread;
        static readonly ConcurrentQueue<Action> s_mainThread = new();

        static StoryBridge()
        {
            EditorApplication.update += Pump;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            if (EditorPrefs.GetBool(EnabledKey, false)) Start();
        }

        /// Whether outside tools may connect. Kept per user.
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

        [MenuItem(MenuPath)]
        static void ToggleMenu() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
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
                s_thread = new Thread(Listen) { IsBackground = true, Name = "NarrativeGraph bridge" };
                s_thread.Start();
                Debug.Log($"[Narrative Graph] Herramientas externas: escuchando en http://127.0.0.1:{Port}/");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Narrative Graph] No se pudo abrir el puerto {Port}: {e.Message}");
                s_listener = null;
            }
        }

        static void Stop()
        {
            try { s_listener?.Stop(); s_listener?.Close(); } catch (Exception) { }
            s_listener = null;
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
                    using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8)) body = reader.ReadToEnd();

                string answer = null;
                int status = 200;
                using var done = new ManualResetEventSlim(false);
                s_mainThread.Enqueue(() =>
                {
                    try { answer = Handle(ctx.Request.HttpMethod, ctx.Request.Url.AbsolutePath, ctx.Request.QueryString["graph"], body, out status); }
                    catch (Exception e) { answer = e.Message; status = 500; }
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

        internal static string Handle(string method, string path, string graphName, string body, out int status)
        {
            status = 200;
            switch (path.TrimEnd('/'))
            {
                case "/graphs":
                    return string.Join("\n", AllGraphs().Select(g => g.name));
                case "/manual":
                    return NarrativeGraphText.Manual();
                case "/chapter":
                {
                    var graph = FindGraph(graphName);
                    if (graph == null) { status = 404; return $"No graph called '{graphName}'."; }
                    return NarrativeGraphText.Chapter(graph);
                }
                case "/edit":
                {
                    if (method != "POST") { status = 405; return "Use POST."; }
                    var graph = FindGraph(graphName);
                    if (graph == null) { status = 404; return $"No graph called '{graphName}'."; }
                    var result = StoryBatchEdit.Apply(graph, body);
                    if (!result.Applied) { status = 400; return result.ToString(); }
                    AssetDatabase.SaveAssetIfDirty(graph);
                    return result.ToString();
                }
                default:
                    status = 404;
                    return "Unknown path. Use /graphs, /chapter?graph=, /manual or POST /edit?graph=.";
            }
        }

        static NarrativeGraph[] AllGraphs()
            => AssetDatabase.FindAssets("t:NarrativeGraph")
                .Select(g => AssetDatabase.LoadAssetAtPath<NarrativeGraph>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(g => g != null).OrderBy(g => g.name).ToArray();

        static NarrativeGraph FindGraph(string name)
            => string.IsNullOrEmpty(name) ? null : AllGraphs().FirstOrDefault(g => string.Equals(g.name, name, StringComparison.OrdinalIgnoreCase));
    }
}
