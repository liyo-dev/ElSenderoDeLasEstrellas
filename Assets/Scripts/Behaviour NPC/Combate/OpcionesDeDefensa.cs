using UnityEngine;

namespace Game.NPC.Combate
{
    /// <summary>
    /// Apartarse de un hechizo que llega, o de uno que el jugador está cargando contra él. Solo
    /// esquiva los hechizos que decidió esquivar (una vez por hechizo, según su dificultad), para
    /// no esquivarlo todo. Elige el lado con sitio y sin compañeros.
    /// </summary>
    public sealed class OpcionEsquivar : OpcionDeCombate
    {
        private float _hasta;

        public OpcionEsquivar(ICombatiente cuerpo) : base(cuerpo) { }

        public override string Nombre => "Esquivar";
        public override bool PuedeInterrumpirse => false;

        public override float Puntuar(in SituacionDeCombate s)
        {
            if (!Cuerpo.Perfil.Defensas.esquivar || !Cuerpo.EsquivaLista) return 0f;
            if (s.hechizoEntrante && s.segundosHastaElHechizo < 0.9f && s.decideEsquivarlo) return 0.95f;
            if (s.jugadorMeApunta && s.veAlObjetivo && (s.jugadorCargando || s.segundosDesdeQueLanzo < 0.25f))
                return 0.35f + 0.35f * Cuerpo.Perfil.Caracter.cautela * Cuerpo.Perfil.Dificultad;
            return 0f;
        }

        public override void Empezar(in SituacionDeCombate s)
        {
            var yo = Cuerpo.Transform;
            Vector3 viene = s.hechizoEntrante ? -s.deDondeViene : (s.objetivo != null ? yo.position - s.objetivo.position : yo.forward);
            viene.y = 0f;
            if (viene.sqrMagnitude < 0.01f) viene = yo.forward;
            Vector3 lado = Vector3.Cross(Vector3.up, viene.normalized);
            float metros = Cuerpo.Perfil.Defensas.distanciaDeEsquiva;

            Vector3 mejor = yo.position;
            int mejorEncierro = int.MaxValue;
            bool primeroDerecha = Random.value < 0.5f;
            for (int i = 0; i < 2; i++)
            {
                float signo = (i == 0) == primeroDerecha ? 1f : -1f;
                if (!Cuerpo.PuntoAlcanzable(yo.position + lado * (signo * metros), out Vector3 p)) continue;
                int encierro = Cuerpo.Sondeo.Encierro(p, 2f) + 3 * SondeoDelTerreno.Apretura(p, Cuerpo.Companeros, 2f);
                if (encierro < mejorEncierro) { mejorEncierro = encierro; mejor = p; }
            }
            Cuerpo.MoverA(mejor, corriendo: true);
            Cuerpo.EsquivaHecha();
            _hasta = Time.time + 0.55f;
        }

        public override bool Actualizar(in SituacionDeCombate s) => Time.time < _hasta && !Cuerpo.HaLlegado(0.3f);

        public override void Terminar() => Cuerpo.Parar();
    }

    /// <summary>
    /// Levantar el escudo contra un hechizo que no va a esquivar, o mientras el jugador le carga
    /// uno. Lo baja en cuanto no hay nada que parar (mínimo un momento, para que se lea).
    /// </summary>
    public sealed class OpcionEscudo : OpcionDeCombate
    {
        private const float Minimo = 0.6f;
        private float _desde;
        private float _hasta;

        public OpcionEscudo(ICombatiente cuerpo) : base(cuerpo) { }

        public override string Nombre => "Escudo";
        public override bool PuedeInterrumpirse => Time.time - _desde > Minimo;

        public override float Puntuar(in SituacionDeCombate s)
        {
            if (!Cuerpo.Perfil.Defensas.escudo || !Cuerpo.EscudoListo) return 0f;
            float cautela = Cuerpo.Perfil.Caracter.cautela;
            if (s.hechizoEntrante && s.segundosHastaElHechizo < 1.2f && !s.decideEsquivarlo) return 0.6f + 0.3f * cautela;
            if (s.jugadorCargando && s.jugadorMeApunta && s.veAlObjetivo) return 0.5f * cautela;
            return 0f;
        }

        public override void Empezar(in SituacionDeCombate s)
        {
            Cuerpo.Parar();
            float duracion = Cuerpo.Perfil.Defensas.duracionDelEscudo;
            Cuerpo.LevantarEscudo(duracion);
            _desde = Time.time;
            _hasta = Time.time + duracion;
        }

        public override bool Actualizar(in SituacionDeCombate s)
        {
            if (s.objetivo != null) Cuerpo.Mirar(s.objetivo.position);
            if (Time.time >= _hasta) return false;
            return Time.time - _desde < Minimo || s.hechizoEntrante || s.jugadorCargando;
        }

        public override void Terminar() => Cuerpo.BajarEscudo();
    }

    /// <summary>
    /// Con poco maná (o poca vida, si es cauto) va a un sitio que le tapa de verdad de todos los que
    /// le atacan, comprobado con rayos, y recarga allí. Si le ven o le rodean, descarta ese
    /// escondite y busca otro. Sin cobertura cerca, se aleja.
    /// </summary>
    public sealed class OpcionCubrirseYRecargar : OpcionDeCombate
    {
        private enum Fase { Yendo, Esperando, Alejandose }

        private const int CambiosDeEsconditeMaximos = 3;

        private Fase _fase;
        private Vector3 _sitio;
        private float _hasta;
        private float _siguienteMirada;
        private int _cambios;
        private float _sinCoberturaHasta = -999f;

        public OpcionCubrirseYRecargar(ICombatiente cuerpo) : base(cuerpo) { }

        public override string Nombre => "Cubrirse y recargar";

        private float ManaParaSalir => 0.55f + 0.25f * Cuerpo.Perfil.Caracter.paciencia;

        public override float Puntuar(in SituacionDeCombate s)
        {
            if (!Cuerpo.Perfil.Defensas.cubrirseParaRecargar) return 0f;
            var k = Cuerpo.Perfil.Caracter;
            float umbral = 0.15f + 0.3f * k.cautela;
            float p = 0f;
            if (!s.manaParaAlgunAtaque || s.mana < umbral)
                p = 0.6f + 0.35f * (1f - Mathf.Clamp01(s.mana / Mathf.Max(0.01f, umbral)));
            if (s.vida < 0.3f && k.cautela > 0.5f) p = Mathf.Max(p, 0.55f + 0.25f * k.cautela);
            // Ya escondido y aún sin maná para salir: sigue aquí.
            if (s.aCubierto && s.mana < ManaParaSalir) p = Mathf.Max(p, 0.7f);
            if (Time.time < _sinCoberturaHasta) p *= 0.6f;
            return p;
        }

        public override void Empezar(in SituacionDeCombate s)
        {
            _cambios = 0;
            IrAUnEscondite(s);
        }

        public override bool Actualizar(in SituacionDeCombate s)
        {
            switch (_fase)
            {
                case Fase.Yendo:
                    if (Cuerpo.HaLlegado(0.6f))
                    {
                        Cuerpo.Parar();
                        Cuerpo.ACubierto = true;
                        _fase = Fase.Esperando;
                        _siguienteMirada = Time.time + 0.4f;
                        return true;
                    }
                    if (Time.time < _hasta) return true;
                    Cuerpo.Memoria.DescartarCobertura(_sitio);
                    return false;

                case Fase.Esperando:
                    Cuerpo.Mirar(Cuerpo.Memoria.UltimaPosicionVista);
                    if (s.mana >= ManaParaSalir && s.manaParaAlgunAtaque) return false;
                    if (Time.time >= _siguienteMirada)
                    {
                        _siguienteMirada = Time.time + 0.4f;
                        // Le ven: ya no es un escondite (le han rodeado o se han movido).
                        if (s.amenazasQueMeVen > 0)
                        {
                            Cuerpo.ACubierto = false;
                            Cuerpo.Memoria.DescartarCobertura(_sitio);
                            if (++_cambios > CambiosDeEsconditeMaximos) return false;
                            IrAUnEscondite(s);
                        }
                    }
                    return true;

                default:
                    return Time.time < _hasta && !Cuerpo.HaLlegado(0.6f);
            }
        }

        public override void Terminar()
        {
            Cuerpo.ACubierto = false;
            Cuerpo.Parar();
        }

        private void IrAUnEscondite(in SituacionDeCombate s)
        {
            if (Cuerpo.BuscarCobertura(out _sitio) && Cuerpo.MoverA(_sitio, corriendo: true))
            {
                _fase = Fase.Yendo;
                _hasta = Time.time + 5f;
                return;
            }

            // Sin escondite: se aleja del objetivo.
            _sinCoberturaHasta = Time.time + 4f;
            _fase = Fase.Alejandose;
            _hasta = Time.time + 2.5f;
            var yo = Cuerpo.Transform;
            Vector3 lejos = s.objetivo != null ? yo.position - s.objetivo.position : -yo.forward;
            lejos.y = 0f;
            if (lejos.sqrMagnitude < 0.01f) lejos = -yo.forward;
            if (Cuerpo.PuntoAlcanzable(yo.position + lejos.normalized * 6f, out Vector3 p)) Cuerpo.MoverA(p, corriendo: true);
        }
    }
}
