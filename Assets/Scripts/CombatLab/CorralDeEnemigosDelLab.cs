using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Mantiene vivos N enemigos de un prefab alrededor de este punto: cuando uno muere y desaparece,
/// aparece otro. Sirve para probar drops y contratos de caza sin quedarse sin enemigos.
/// Solo para escenas de prueba.
/// </summary>
public sealed class CorralDeEnemigosDelLab : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField, Min(1)] private int vivos = 6;
    [SerializeField, Min(0.5f)] private float radio = 6f;
    [Tooltip("Cada cuántos segundos se repone un enemigo que falte.")]
    [SerializeField, Min(0.5f)] private float reponerCada = 3f;

    private readonly List<GameObject> _actuales = new();

    public void Configurar(GameObject enemigo, int cuantos, float area)
    {
        prefab = enemigo;
        vivos = Mathf.Max(1, cuantos);
        radio = Mathf.Max(0.5f, area);
    }

    private IEnumerator Start()
    {
        if (prefab == null) yield break;
        for (int i = 0; i < vivos; i++) Aparecer();
        var pausa = new WaitForSeconds(reponerCada);
        while (true)
        {
            yield return pausa;
            _actuales.RemoveAll(e => e == null);
            if (_actuales.Count < vivos) Aparecer();
        }
    }

    private void Aparecer()
    {
        Vector2 circulo = Random.insideUnitCircle * radio;
        Vector3 punto = transform.position + new Vector3(circulo.x, 0f, circulo.y);
        if (NavMesh.SamplePosition(punto, out var nav, 3f, NavMesh.AllAreas)) punto = nav.position;
        var enemigo = Instantiate(prefab, punto, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        _actuales.Add(enemigo);
    }
}
