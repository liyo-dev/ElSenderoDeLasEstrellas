using UnityEngine;

namespace Kits.Puzzles
{
    /// <summary>
    /// La base visible del compás: una fila de piedras (una por tiempo) que se encienden por turno,
    /// 1-2-3-4. Es lo que le enseña al jugador la regla sin carteles: mira la piedra que se enciende y
    /// salta cuando toca. Se engancha al mismo <see cref="TempoClock"/> que mueve el escenario.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BeatIndicator : MonoBehaviour
    {
        [Tooltip("Reloj del tramo. Si se deja vacío, se usa el de la escena.")]
        [SerializeField] private TempoClock pulse;
        [Tooltip("Una marca por tiempo, en orden (1, 2, 3, 4).")]
        [SerializeField] private Renderer[] marcas;
        [Tooltip("Luces que se encienden con su marca (opcional, una por tiempo).")]
        [SerializeField] private Light[] luces;
        [SerializeField] private Color colorEncendida = new Color(1f, 0.82f, 0.45f);
        [SerializeField] private float intensidadEncendida = 2f;

        private TempoClock _pulse;
        private MaterialPropertyBlock _bloque;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private float _siguienteEnganche;

        private void Awake()
        {
            _bloque = new MaterialPropertyBlock();
            ApagarTodas();
        }

        private void OnEnable()
        {
            _pulse = pulse != null ? pulse : TempoClock.Current;
            if (_pulse != null) _pulse.Beat += AlPasarUnTiempo;
        }

        private void OnDisable()
        {
            if (_pulse != null) _pulse.Beat -= AlPasarUnTiempo;
            _pulse = null;
        }

        private void Update()
        {
            if (_pulse != null || Time.time < _siguienteEnganche) return;
            _siguienteEnganche = Time.time + 0.5f;
            _pulse = pulse != null ? pulse : TempoClock.Current;
            if (_pulse != null) _pulse.Beat += AlPasarUnTiempo;
        }

        private void AlPasarUnTiempo(int compas, int tiempo)
        {
            if (marcas == null) return;
            for (int i = 0; i < marcas.Length; i++)
            {
                bool encendida = i == tiempo;
                if (marcas[i] != null)
                {
                    marcas[i].GetPropertyBlock(_bloque);
                    _bloque.SetColor(BaseColorId, encendida ? colorEncendida : Color.black);
                    marcas[i].SetPropertyBlock(_bloque);
                }
                if (luces != null && i < luces.Length && luces[i] != null)
                    luces[i].intensity = encendida ? intensidadEncendida : 0f;
            }
        }

        private void ApagarTodas()
        {
            if (marcas != null)
                for (int i = 0; i < marcas.Length; i++)
                {
                    if (marcas[i] == null) continue;
                    marcas[i].GetPropertyBlock(_bloque);
                    _bloque.SetColor(BaseColorId, Color.black);
                    marcas[i].SetPropertyBlock(_bloque);
                }
            if (luces != null)
                for (int i = 0; i < luces.Length; i++)
                    if (luces[i] != null) luces[i].intensity = 0f;
        }
    }
}
