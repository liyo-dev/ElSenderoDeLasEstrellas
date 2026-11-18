using System;
using UnityEngine;

namespace Game.NPC.Combate
{
    /// <summary>
    /// Lanza una acción del catálogo con sus tres tiempos: aviso (la bola crece en la mano con
    /// <see cref="CargaDeDisparo"/>, que además avisa al jugador), salida (se cobra el maná, se
    /// inicializa el <see cref="EnemyProjectile"/> con su lanzador y si se puede devolver) y
    /// recuperación (la ventana de castigo, en la que se queda expuesto). Un lanzamiento a la vez.
    /// </summary>
    public sealed class LanzadorDeAcciones
    {
        private const float AnticipoDeAnimacion = 0.4f;
        private const float VelocidadSinDato = 12f;

        private enum Fase { Libre, Cargando, Recuperando }

        private readonly GameObject _cuerpo;
        private readonly Transform _boca;
        private readonly CargaDeDisparo _carga;
        private readonly Func<float, bool> _gastar;
        private readonly Action<int, float> _enfriar;
        private readonly Action<string> _animar;
        private readonly Action<GameObject, Vector3> _alSalir;

        private Fase _fase;
        private AccionDeCombate _accion;
        private int _indice;
        private Transform _objetivo;
        private float _inicio;
        private float _salioEn;
        private bool _animado;
        private bool _conCarga;

        public LanzadorDeAcciones(GameObject cuerpo, Transform boca, CargaDeDisparo carga,
            Func<float, bool> gastar, Action<int, float> enfriar, Action<string> animar)
        {
            _cuerpo = cuerpo;
            _boca = boca;
            _carga = carga;
            _gastar = gastar;
            _enfriar = enfriar;
            _animar = animar;
            _alSalir = AlSalir;
        }

        public bool Ocupado => _fase != Fase.Libre;
        public bool Preparando => _fase == Fase.Cargando;
        public bool Recuperando => _fase == Fase.Recuperando;
        public Transform Boca => _boca;

        /// <summary>Empieza a lanzar la acción 'indice' contra el objetivo. False si no puede.</summary>
        public bool Empezar(int indice, AccionDeCombate accion, Transform objetivo)
        {
            if (Ocupado || accion == null || accion.proyectil == null || objetivo == null) return false;
            _accion = accion;
            _indice = indice;
            _objetivo = objetivo;
            _inicio = Time.time;
            _animado = false;
            _conCarga = accion.segundosDeAviso > 0f && TieneProyectilEnemigo(accion.proyectil) && _carga != null;
            if (_conCarga)
            {
                float velocidad = accion.velocidad > 0f ? accion.velocidad : VelocidadSinDato;
                var tipo = accion.sePuedeDevolver ? TipoDeAmenaza.Devolvible : TipoDeAmenaza.NoBloqueable;
                if (!_carga.Cargar(accion.proyectil, _boca, Vector3.zero, objetivo, velocidad, _alSalir, accion.segundosDeAviso, tipo))
                    return false;
            }
            _fase = Fase.Cargando;
            return true;
        }

        /// <summary>Corta el lanzamiento (le han golpeado o se acaba el combate). Si no ha salido, no cobra.</summary>
        public void Cancelar()
        {
            if (_fase == Fase.Cargando && _conCarga && _carga != null) _carga.Cancelar();
            _fase = Fase.Libre;
        }

        public void Actualizar()
        {
            if (_fase == Fase.Libre) return;
            float pasado = Time.time - _inicio;

            if (_fase == Fase.Cargando)
            {
                float aviso = _conCarga ? _accion.segundosDeAviso : AnticipoDeAnimacion;
                if (!_animado && pasado >= aviso - AnticipoDeAnimacion)
                {
                    _animado = true;
                    _animar(_accion.animacion);
                }
                if (_conCarga)
                {
                    // La carga ya no está y no ha salido: alguien la canceló (p. ej. se apagó).
                    if (!_carga.EnCarga && _fase == Fase.Cargando) _fase = Fase.Libre;
                }
                else if (pasado >= AnticipoDeAnimacion)
                {
                    DispararSinCarga();
                }
                return;
            }

            if (Time.time - _salioEn >= _accion.segundosDeRecuperacion) _fase = Fase.Libre;
        }

        private void AlSalir(GameObject bola, Vector3 direccion)
        {
            if (_fase != Fase.Cargando || !_gastar(_accion.costeDeMana))
            {
                UnityEngine.Object.Destroy(bola);
                _fase = Fase.Libre;
                return;
            }
            Preparar(bola, direccion);
            Salio();
        }

        private void DispararSinCarga()
        {
            if (_objetivo == null || !_gastar(_accion.costeDeMana)) { _fase = Fase.Libre; return; }
            Vector3 origen = _boca.position;
            Vector3 direccion = (SondeoDelTerreno.Pecho(_objetivo.position) - origen).normalized;
            var bola = UnityEngine.Object.Instantiate(_accion.proyectil, origen, Quaternion.LookRotation(direccion));
            Preparar(bola, direccion);
            Salio();
        }

        private void Preparar(GameObject bola, Vector3 direccion)
        {
            var proyectil = bola.GetComponentInChildren<EnemyProjectile>();
            if (proyectil == null) return;
            var rb = proyectil.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;
            proyectil.AsignarLanzador(_cuerpo);
            proyectil.PermitirDevolucion(_accion.sePuedeDevolver);
            if (_accion.velocidad > 0f) proyectil.SetSpeed(_accion.velocidad);
            proyectil.Initialize(direccion, _accion.dano);
        }

        private void Salio()
        {
            _enfriar(_indice, _accion.enfriamiento);
            _salioEn = Time.time;
            _fase = Fase.Recuperando;
        }

        private static bool TieneProyectilEnemigo(GameObject prefab) =>
            prefab.GetComponentInChildren<EnemyProjectile>(true) != null;
    }
}
