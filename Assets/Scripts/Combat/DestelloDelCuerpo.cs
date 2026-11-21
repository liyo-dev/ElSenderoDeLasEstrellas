using UnityEngine;

/// <summary>
/// Destello de la piel de un personaje: multiplica el color de sus SkinnedMeshRenderer (propiedades
/// _Tinte y _BaseColor, por MaterialPropertyBlock) y lo devuelve como estaba. Guarda el color de
/// antes al poner el destello, así que convive con otros bloques de propiedades del renderer.
/// Lo usan <see cref="ReaccionAlGolpe"/> y <see cref="AfinidadElemental"/>; cada personaje tiene
/// un solo dueño del destello (si hay ReaccionAlGolpe, él).
/// </summary>
public sealed class DestelloDelCuerpo
{
    private static readonly int TinteId = Shader.PropertyToID("_Tinte");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private readonly Renderer[] _cuerpo;
    private readonly Color[] _tinteOriginal;
    private readonly Color[] _baseOriginal;
    private readonly bool[] _tieneTinte;
    private readonly bool[] _tieneBase;
    private readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();

    public bool Puesto { get; private set; }

    public DestelloDelCuerpo(GameObject raiz)
    {
        var todos = raiz.GetComponentsInChildren<Renderer>(true);
        int n = 0;
        for (int i = 0; i < todos.Length; i++) if (EsDelCuerpo(todos[i])) n++;
        _cuerpo = new Renderer[n];
        _tinteOriginal = new Color[n];
        _baseOriginal = new Color[n];
        _tieneTinte = new bool[n];
        _tieneBase = new bool[n];
        n = 0;
        for (int i = 0; i < todos.Length; i++) if (EsDelCuerpo(todos[i])) _cuerpo[n++] = todos[i];
    }

    // Solo la piel del personaje: ni partículas ni efectos que lleve colgados.
    private static bool EsDelCuerpo(Renderer r)
        => r != null && r is SkinnedMeshRenderer && r.sharedMaterial != null;

    /// Pone el destello con este multiplicador de color (por encima de 1 aclara; con bloom brilla).
    /// Si ya había uno, cambia el color sin perder el original.
    public void Poner(Color multiplicador)
    {
        for (int i = 0; i < _cuerpo.Length; i++)
        {
            var r = _cuerpo[i];
            if (r == null) continue;
            var mat = r.sharedMaterial;
            r.GetPropertyBlock(_mpb);
            if (!Puesto)
            {
                _tieneTinte[i] = mat != null && mat.HasProperty(TinteId);
                _tieneBase[i] = mat != null && mat.HasProperty(BaseColorId);
                if (_tieneTinte[i]) _tinteOriginal[i] = _mpb.HasColor(TinteId) ? _mpb.GetColor(TinteId) : mat.GetColor(TinteId);
                if (_tieneBase[i]) _baseOriginal[i] = _mpb.HasColor(BaseColorId) ? _mpb.GetColor(BaseColorId) : mat.GetColor(BaseColorId);
            }
            if (_tieneTinte[i]) _mpb.SetColor(TinteId, _tinteOriginal[i] * multiplicador);
            if (_tieneBase[i]) _mpb.SetColor(BaseColorId, _baseOriginal[i] * multiplicador);
            r.SetPropertyBlock(_mpb);
        }
        Puesto = true;
    }

    /// Devuelve el color de antes del destello.
    public void Quitar()
    {
        if (!Puesto) return;
        for (int i = 0; i < _cuerpo.Length; i++)
        {
            var r = _cuerpo[i];
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            if (_tieneTinte[i]) _mpb.SetColor(TinteId, _tinteOriginal[i]);
            if (_tieneBase[i]) _mpb.SetColor(BaseColorId, _baseOriginal[i]);
            r.SetPropertyBlock(_mpb);
        }
        Puesto = false;
    }
}
