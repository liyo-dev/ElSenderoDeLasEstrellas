using System;
using System.Collections.Generic;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

namespace Sendero.UI
{
    /// <summary>
    /// Lo que los retratos del grupo necesitan saber de cada personaje (vida, maná, si ha caído,
    /// si le acaban de dar) y si el trío está listo. La vida y el maná de cada uno salen de
    /// <see cref="SaludDelGrupo"/> (cada personaje tiene los suyos, al mando o con IA; INC-717).
    ///
    /// Solo eventos: nada se lee por frame. Si SaludDelGrupo o la carga aún no existen (llegan con
    /// la escena), lo vuelve a intentar una vez por segundo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EstadoDelGrupoParaRetratos : MonoBehaviour
    {
        public const int Personajes = 3;

        /// Le acaban de dar a ese personaje.
        public event Action<Slot> Golpeado;
        /// Ha cambiado la vida, el maná o el estado de caído de ese personaje.
        public event Action<Slot> SaludCambiada;
        /// La carga del trío se ha llenado (true) o ha dejado de estar llena (false).
        public event Action<bool> TrioCambiado;

        private readonly float[] _vida = { 1f, 1f, 1f };
        private readonly float[] _mana = { 1f, 1f, 1f };
        private readonly bool[] _caido = new bool[Personajes];

        private SaludDelGrupo _salud;
        private DuoSpecialAttackSystem _equipo;
        private SpecialChargeMeter _carga;
        private bool _trioListo;
        private float _proximoIntento;

        /// Vida de 0 a 1.
        public float Vida(Slot personaje) => _vida[(int)personaje];
        /// Maná de 0 a 1 (0 si no tiene maná).
        public float Mana(Slot personaje) => _mana[(int)personaje];
        public bool Caido(Slot personaje) => _caido[(int)personaje];
        /// La carga de equipo está llena y hay compañeros con quien lanzar el trío.
        public bool TrioListo => _trioListo;

        private void OnEnable()
        {
            PlayerService.OnPlayerRegistered += AlRegistrarJugador;
            Enlazar();
        }

        private void OnDisable()
        {
            PlayerService.OnPlayerRegistered -= AlRegistrarJugador;
            SoltarSalud();
            SoltarCarga();
        }

        private void Update()
        {
            if (Time.unscaledTime < _proximoIntento || !FaltaAlgo()) return;
            _proximoIntento = Time.unscaledTime + 1f;
            Enlazar();
        }

        private void AlRegistrarJugador(GameObject _) => Enlazar();

        private bool FaltaAlgo() => _salud == null || _salud != SaludDelGrupo.Instance || _carga == null;

        private void Enlazar()
        {
            EnlazarSalud();
            EnlazarCarga();
        }

        // ─── Vida y maná de cada personaje ─────────────────────────────────────

        private void EnlazarSalud()
        {
            var salud = SaludDelGrupo.Instance;
            if (salud == _salud) return;
            SoltarSalud();
            _salud = salud;
            if (_salud == null) return;
            _salud.SaludCambiada += Leer;
            _salud.Golpeado += AlGolpear;
            for (int i = 0; i < Personajes; i++) Leer((Slot)i);
        }

        private void SoltarSalud()
        {
            if (_salud == null) return;
            _salud.SaludCambiada -= Leer;
            _salud.Golpeado -= AlGolpear;
            _salud = null;
        }

        private void AlGolpear(Slot personaje)
        {
            Leer(personaje);
            Golpeado?.Invoke(personaje);
        }

        private void Leer(Slot personaje)
        {
            if (_salud == null) return;
            float vidaMax = _salud.VidaMax(personaje);
            float manaMax = _salud.ManaMax(personaje);
            Poner((int)personaje,
                  vidaMax > 0f ? _salud.Vida(personaje) / vidaMax : 0f,
                  manaMax > 0f ? _salud.Mana(personaje) / manaMax : 0f,
                  _salud.Caido(personaje));
        }

        private void Poner(int slot, float vida, float mana, bool caido)
        {
            vida = Mathf.Clamp01(vida);
            mana = Mathf.Clamp01(mana);
            if (Mathf.Approximately(_vida[slot], vida) && Mathf.Approximately(_mana[slot], mana) && _caido[slot] == caido) return;
            _vida[slot] = vida;
            _mana[slot] = mana;
            _caido[slot] = caido;
            SaludCambiada?.Invoke((Slot)slot);
        }

        // ─── Carga del trío ────────────────────────────────────────────────────

        private void EnlazarCarga()
        {
            if (_carga != null) { LeerCarga(); return; }
            if (!PlayerService.TryGetComponent(out _equipo, allowSceneLookup: false) || _equipo == null) return;
            _carga = _equipo.TeamGauge;
            if (_carga == null) return;
            _carga.OnChargeUpdated += AlCambiarCarga;
            LeerCarga();
        }

        private void SoltarCarga()
        {
            if (_carga != null) _carga.OnChargeUpdated -= AlCambiarCarga;
            _carga = null;
            _equipo = null;
        }

        private void AlCambiarCarga(float _) => LeerCarga();

        private void LeerCarga()
        {
            bool listo = _carga != null && _carga.IsFullyCharged && _equipo != null && _equipo.HasAnyCompanion;
            if (listo == _trioListo) return;
            _trioListo = listo;
            TrioCambiado?.Invoke(listo);
        }
    }
}
