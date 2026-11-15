using System;
using UnityEngine;
using UnityEngine.Events;

namespace Kits.Puzzles
{
    /// <summary>
    /// Plato que pesa: cuenta cuántos cuerpos del grupo tiene encima y avisa. Sirve para balanzas y
    /// ascensores que solo suben con dos personas, para que separar al grupo y repartirlo importe.
    ///
    /// Cuenta por cuerpos (no por colliders ni por etiquetas) para que cambiar de personaje o dejar a
    /// un compañero quieto donde estaba no le haga perder la cuenta: quién es un cuerpo lo decide el
    /// juego con <see cref="ResolveWeightBody"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class WeightPlate : MonoBehaviour
    {
        [Serializable] public sealed class PesoEvent : UnityEvent<int> { }

        /// <summary>Devuelve la raíz de un cuerpo que pesa, o null si ese collider no cuenta.</summary>
        public static Func<Collider, Transform> ResolveWeightBody;

        [Header("Peso")]
        [Tooltip("Cuerpos que hacen falta para que el plato haga su tope.")]
        [SerializeField, Min(1)] private int requiredWeight = 2;
        [Tooltip("Cada cuánto se vuelve a contar (s).")]
        [SerializeField, Min(0.02f)] private float recountEvery = 0.1f;

        [Header("Aspecto")]
        [Tooltip("Lo que baja con el peso. Si se deja vacío, no se mueve nada.")]
        [SerializeField] private Transform plateVisual;
        [Tooltip("Cuánto baja por cuerpo encima (m).")]
        [SerializeField, Min(0f)] private float sinkPerBody = 0.6f;
        [Tooltip("Contrapeso que sube a la vez que baja el plato (opcional).")]
        [SerializeField] private Transform counterweight;
        [Tooltip("Cuánto se mueve el contrapeso por cuerpo (m, en su eje Y local).")]
        [SerializeField] private float counterweightPerBody = 1.2f;
        [SerializeField, Min(0.1f)] private float moveSpeed = 6f;

        [Header("Eventos")]
        [SerializeField] private PesoEvent alCambiarElPeso = new PesoEvent();
        [SerializeField] private UnityEvent alLlegarAlPeso = new UnityEvent();
        [SerializeField] private UnityEvent alPerderElPeso = new UnityEvent();

        public int CurrentWeight { get; private set; }
        public int RequiredWeight => requiredWeight;
        public bool IsMet => CurrentWeight >= requiredWeight;

        /// <summary>Al llegar al peso que pide (para cablearlo desde otro sistema o el Inspector).</summary>
        public UnityEvent AlLlegarAlPeso => alLlegarAlPeso;
        /// <summary>Al dejar de tenerlo.</summary>
        public UnityEvent AlPerderElPeso => alPerderElPeso;

        private readonly Collider[] _dentro = new Collider[16];
        private readonly System.Collections.Generic.HashSet<Transform> _cuerpos = new System.Collections.Generic.HashSet<Transform>();
        private BoxCollider _zona;
        private Vector3 _plateBase, _contrapesoBase;
        private float _siguienteCuenta;
        private bool _cumplia;

        private void Awake()
        {
            _zona = GetComponent<BoxCollider>();
            _zona.isTrigger = true;
            if (plateVisual != null) _plateBase = plateVisual.localPosition;
            if (counterweight != null) _contrapesoBase = counterweight.localPosition;
        }

        /// <summary>Vuelve a contar en el acto (al empezar o reiniciar un tramo).</summary>
        public void ForceRecount() => Contar();

        private void Update()
        {
            if (Time.time < _siguienteCuenta) return;
            _siguienteCuenta = Time.time + recountEvery;
            Contar();
        }

        private void Contar()
        {
            int total = 0;
            _cuerpos.Clear();
            Vector3 centro = _zona.bounds.center;
            int n = Physics.OverlapBoxNonAlloc(centro, _zona.bounds.extents * 0.98f, _dentro, transform.rotation,
                                               ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = _dentro[i];
                if (col == null) continue;
                Transform cuerpo = ResolveWeightBody != null ? ResolveWeightBody(col) : null;
                if (cuerpo == null) continue;
                if (_cuerpos.Add(cuerpo)) total++;
            }

            if (total == CurrentWeight) return;
            CurrentWeight = total;
            alCambiarElPeso.Invoke(CurrentWeight);

            bool cumple = IsMet;
            if (cumple != _cumplia)
            {
                _cumplia = cumple;
                if (cumple) alLlegarAlPeso.Invoke();
                else alPerderElPeso.Invoke();
            }
        }

        private void LateUpdate()
        {
            float peso = Mathf.Min(CurrentWeight, requiredWeight);
            if (plateVisual != null)
            {
                Vector3 objetivo = _plateBase + Vector3.down * (sinkPerBody * peso / Mathf.Max(1, requiredWeight));
                plateVisual.localPosition = Vector3.MoveTowards(plateVisual.localPosition, objetivo, moveSpeed * Time.deltaTime);
            }
            if (counterweight != null)
            {
                Vector3 objetivo = _contrapesoBase + Vector3.down * (counterweightPerBody * peso / Mathf.Max(1, requiredWeight));
                counterweight.localPosition = Vector3.MoveTowards(counterweight.localPosition, objetivo, moveSpeed * Time.deltaTime);
            }
        }
    }
}
