#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// Lanza los tests EditMode del guion y deja el resultado en «Guiones/_ordenes/tests_guion.md», para
/// poder pedirlos desde el buzón («menu El Sendero/Cinemáticas/Tests del guion») sin tocar el Test Runner.
public static class TestsDeGuionMenu
{
    private static readonly string[] Clases =
    {
        "GuionCompasesTests", "GuionBatchEditTests", "TiempoDeGuionTests", "TallerDeRodajeTests",
    };

    private static readonly string Salida = Path.Combine("Guiones", "_ordenes", "tests_guion.md");

    [MenuItem("El Sendero/Cinemáticas/Tests del guion")]
    public static void Lanzar()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Recogedor(api));
        File.WriteAllText(Salida, $"# Tests del guion\n\n{DateTime.Now:yyyy-MM-dd HH:mm:ss} · en marcha…\n");
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, groupNames = Grupos() }));
    }

    private static string[] Grupos()
    {
        var g = new string[Clases.Length];
        for (int i = 0; i < Clases.Length; i++) g[i] = "^" + Clases[i] + @"(\.|$)";
        return g;
    }

    private sealed class Recogedor : ICallbacks
    {
        private readonly TestRunnerApi _api;
        private readonly StringBuilder _fallos = new();
        private int _bien, _mal, _saltados;

        public Recogedor(TestRunnerApi api) => _api = api;

        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.HasChildren) return;
            switch (result.TestStatus)
            {
                case TestStatus.Passed: _bien++; break;
                case TestStatus.Failed:
                    _mal++;
                    _fallos.AppendLine($"- **{result.FullName}**: {result.Message?.Trim()}");
                    break;
                default: _saltados++; break;
            }
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Tests del guion\n");
            sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} · clases: {string.Join(", ", Clases)}\n");
            sb.AppendLine($"**{_bien} bien · {_mal} mal · {_saltados} sin pasar**\n");
            if (_mal > 0) { sb.AppendLine("## Fallos\n"); sb.Append(_fallos); }
            File.WriteAllText(Salida, sb.ToString());
            Debug.Log($"[TestsDeGuion] {_bien} bien, {_mal} mal, {_saltados} sin pasar → {Salida}");
            _api.UnregisterCallbacks(this);
        }
    }
}
#endif
