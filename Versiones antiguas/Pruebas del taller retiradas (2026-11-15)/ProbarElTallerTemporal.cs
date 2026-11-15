#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// Lanzador de un solo uso para comprobar los tests del taller desde el buzón del Editor. Se retira
/// a «Versiones antiguas» en cuanto se ha usado (regla 8 de AGENTS.md).
public static class ProbarElTallerTemporal
{
    [MenuItem("El Sendero/Cinemáticas/Guion/zz Probar el taller (temporal)")]
    public static void Probar()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Registro());
        api.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            assemblyNames = new[] { "Assembly-CSharp-Editor" }
        }));
    }

    private sealed class Registro : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) => Debug.Log("[TallerTests] inicio");

        public void RunFinished(ITestResultAdaptor result)
            => Debug.Log($"[TallerTests] fin: {result.PassCount} ok, {result.FailCount} fallos, {result.SkipCount} saltados");

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.Test.IsSuite) return;
            string estado = result.TestStatus == TestStatus.Passed ? "OK" : "FALLO";
            Debug.Log($"[TallerTests] {estado} {result.Name} {result.Message}");
        }
    }
}
#endif
