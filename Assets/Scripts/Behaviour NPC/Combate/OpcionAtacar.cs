using UnityEngine;

namespace Game.NPC.Combate
{
    /// <summary>
    /// Lanzar una acción concreta del catálogo. Hay una opción por acción: cada una se puntúa por su
    /// alcance, su maná y enfriamiento, su peso, su función (presión, golpe, remate) y lo que está
    /// haciendo el jugador (si acaba de lanzar, si abusa de la B, si tiene el escudo levantado).
    /// Necesita el turno del grupo; si le golpean mientras prepara, se corta sin cobrar.
    /// </summary>
    public sealed class OpcionAtacar : OpcionDeCombate
    {
        private readonly int _indice;
        private readonly AccionDeCombate _accion;
        private readonly float _pesoRelativo;

        private bool _enMarcha;
        private bool _salio;
        private float _inicio;
        private int _companerosAlEmpezar;

        public OpcionAtacar(ICombatiente cuerpo, int indice, AccionDeCombate accion, float pesoMedio) : base(cuerpo)
        {
            _indice = indice;
            _accion = accion;
            _pesoRelativo = Mathf.Clamp(accion.peso / Mathf.Max(0.01f, pesoMedio), 0.4f, 1.6f);
            Nombre = "Atacar: " + accion.nombre;
        }

        public override string Nombre { get; }

        public override bool PuedeInterrumpirse => !_enMarcha;

        public override float Puntuar(in SituacionDeCombate s)
        {
            if (!s.veAlObjetivo || !s.lineaDeFuego) return 0f;
            if (s.distancia < _accion.distanciaMinima || s.distancia > _accion.distanciaMaxima) return 0f;
            if (!Cuerpo.AccionLista(_indice) || !Cuerpo.PuedePagar(_accion.costeDeMana)) return 0f;
            if (s.rol == RolEnElGrupo.Rodea || s.descansandoTurno) return 0f;

            var k = Cuerpo.Perfil.Caracter;
            float dificultad = Cuerpo.Perfil.Dificultad;
            float pausa = Mathf.Lerp(1.8f, 0.5f, k.agresividad) * Mathf.Lerp(1.3f, 0.8f, dificultad);
            if (s.segundosDesdeMiAtaque < pausa) return 0f;

            float p = (0.45f + 0.35f * k.agresividad) * _pesoRelativo;

            switch (_accion.funcion)
            {
                case FuncionDeAccion.Presion:
                    if (s.rol == RolEnElGrupo.Presiona) p *= 1.15f;
                    break;
                case FuncionDeAccion.Remate:
                    // El jugador acaba de lanzar o está cargando: es el momento del golpe fuerte.
                    p *= (s.segundosDesdeQueLanzo < 0.9f || s.jugadorCargando) ? 1.5f : 0.75f;
                    break;
            }

            if (s.devolucionesRecientes >= 2) p *= _accion.sePuedeDevolver ? 0.3f : 1.3f;
            if (s.jugadorDefendiendo && _accion.sePuedeDevolver) p *= 0.6f;

            float manaDespues = s.mana - _accion.costeDeMana / Mathf.Max(1f, Cuerpo.Perfil.MaxMana);
            if (manaDespues < 0.15f * k.cautela) p *= 0.7f;

            return Mathf.Clamp01(p);
        }

        public override void Empezar(in SituacionDeCombate s)
        {
            _enMarcha = false;
            _salio = false;
            _inicio = Time.time;
            _companerosAlEmpezar = s.companeros;
            Cuerpo.Parar();
            if (s.objetivo == null || !Cuerpo.PedirTurno()) return;
            Cuerpo.Mirar(s.objetivo.position);
            _enMarcha = Cuerpo.Lanzador.Empezar(_indice, _accion, s.objetivo);
            if (!_enMarcha) Cuerpo.SoltarTurno();
        }

        public override bool Actualizar(in SituacionDeCombate s)
        {
            if (!_enMarcha) return false;
            var lanzador = Cuerpo.Lanzador;

            if (lanzador.Preparando)
            {
                if (s.objetivo != null) Cuerpo.Mirar(s.objetivo.position);
                // Golpeado mientras prepara: se le corta el hechizo (no cobra) y pierde el turno.
                if (Cuerpo.Memoria.UltimoGolpeRecibido > _inicio)
                {
                    lanzador.Cancelar();
                    return false;
                }
                return true;
            }

            if (lanzador.Recuperando)
            {
                if (!_salio) { _salio = true; Cuerpo.Memoria.Atacado(); }
                return true;
            }
            return false;
        }

        public override void Terminar()
        {
            if (Cuerpo.Lanzador.Preparando) Cuerpo.Lanzador.Cancelar();
            if (_enMarcha)
            {
                Cuerpo.SoltarTurno();
                if (_salio && _companerosAlEmpezar > 0)
                    Cuerpo.Memoria.DescansoDeTurnoHasta = Time.time + Mathf.Lerp(0.6f, 2.4f, Cuerpo.Perfil.Caracter.paciencia);
            }
            _enMarcha = false;
        }
    }
}
