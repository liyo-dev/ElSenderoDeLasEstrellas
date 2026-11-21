using UnityEngine;

namespace Game.NPC.Combate
{
    /// <summary>
    /// Ponerse a su distancia preferida con línea de fuego, fuera de rincones y sin pegarse a los
    /// compañeros. Prueba puntos alrededor del objetivo cerca de donde está y va al que mejor
    /// cumple. Urge más si el objetivo se le ha echado encima o si no puede disparar.
    /// </summary>
    public sealed class OpcionRecolocarse : OpcionDeCombate
    {
        private static readonly float[] s_giros = { 0f, 25f, -25f, 50f, -50f, 80f, -80f };
        private static readonly float[] s_radios = { 0f, -1.5f, 1.5f };

        private float _hasta;
        private float _desde;

        public OpcionRecolocarse(ICombatiente cuerpo) : base(cuerpo) { }

        public override string Nombre => "Recolocarse";

        public override float Puntuar(in SituacionDeCombate s)
        {
            if (!s.veAlObjetivo || s.objetivo == null) return 0f;
            var k = Cuerpo.Perfil.Caracter;
            float p;
            if (s.distancia < k.distanciaMinima) p = 0.85f;
            else if (s.distancia > k.distanciaMaxima) p = 0.7f;
            else p = 0.4f * Mathf.Abs(s.distancia - k.distanciaPreferida) / Mathf.Max(1f, k.distanciaPreferida);
            if (!s.lineaDeFuego) p = Mathf.Max(p, 0.65f);
            if (s.EnRincon) p = Mathf.Max(p, 0.6f);
            if (SondeoDelTerreno.Apretura(Cuerpo.Transform.position, Cuerpo.Companeros, 2.2f) > 0) p = Mathf.Max(p, 0.5f);
            return p * (0.5f + 0.5f * k.movilidad);
        }

        public override void Empezar(in SituacionDeCombate s)
        {
            _desde = Time.time;
            _hasta = Time.time + 3f;
            var yo = Cuerpo.Transform;
            var k = Cuerpo.Perfil.Caracter;
            Vector3 centro = s.objetivo.position;
            Vector3 rumbo = yo.position - centro;
            rumbo.y = 0f;
            if (rumbo.sqrMagnitude < 0.01f) rumbo = -s.objetivo.forward;
            rumbo.Normalize();

            float mejor = float.MinValue;
            Vector3 destino = yo.position;
            for (int r = 0; r < s_radios.Length; r++)
            {
                float radio = Mathf.Clamp(k.distanciaPreferida + s_radios[r], k.distanciaMinima + 0.5f, k.distanciaMaxima);
                for (int g = 0; g < s_giros.Length; g++)
                {
                    Vector3 prueba = centro + Quaternion.Euler(0f, s_giros[g], 0f) * rumbo * radio;
                    if (!Cuerpo.PuntoAlcanzable(prueba, out Vector3 p)) continue;
                    float puntos = 10f;
                    if (Cuerpo.Sondeo.LineaDeFuego(SondeoDelTerreno.Ojos(p), s.objetivo)) puntos += 8f;
                    puntos -= 2f * Mathf.Max(0, Cuerpo.Sondeo.Encierro(p, 2.5f) - 2);
                    puntos -= 5f * SondeoDelTerreno.Apretura(p, Cuerpo.Companeros, 2.5f);
                    puntos -= 0.6f * Vector3.Distance(yo.position, p);
                    if (puntos > mejor) { mejor = puntos; destino = p; }
                }
            }
            bool corriendo = s.distancia < k.distanciaMinima || s.distancia > k.distanciaMaxima + 2f;
            if (!Cuerpo.MoverA(destino, corriendo)) _hasta = Time.time;
        }

        public override bool Actualizar(in SituacionDeCombate s)
        {
            if (Time.time >= _hasta || Cuerpo.HaLlegado(0.5f)) return false;
            // Ya está bien: en su franja y con línea de fuego, tras un momento andando.
            var k = Cuerpo.Perfil.Caracter;
            bool bien = s.lineaDeFuego && s.distancia >= k.distanciaMinima && s.distancia <= k.distanciaMaxima && !s.EnRincon;
            return !(bien && Time.time - _desde > 0.8f);
        }

        public override void Terminar() => Cuerpo.Parar();
    }

    /// <summary>
    /// Mientras otro del grupo tiene el turno y presiona de frente, ir hacia un lado del objetivo
    /// (cada rodeador a uno distinto) para atacar desde ahí cuando le toque. También rodea, solo o
    /// en grupo, al jugador que se queda tras el escudo un rato: los ágiles o difíciles (por encima de
    /// <see cref="CaracterDeCombate.umbralDeVueltasContinuas"/>) le dan vueltas sin parar; los demás
    /// se van a un lado una vez y esperan allí a que lo baje.
    /// </summary>
    public sealed class OpcionRodear : OpcionDeCombate
    {
        private const float SegundosDeEscudoLargo = 1.2f;

        private float _hasta;
        private float _siguienteRumbo;
        private bool _flanqueoUnico;
        private bool _yaFlanqueo;
        private Vector3 _punto;

        public OpcionRodear(ICombatiente cuerpo) : base(cuerpo) { }

        public override string Nombre => "Rodear";

        private static bool TocaRodear(in SituacionDeCombate s) =>
            s.rol == RolEnElGrupo.Rodea || (s.jugadorDefendiendo && s.segundosDefendiendo > SegundosDeEscudoLargo && s.veAlObjetivo);

        public override float Puntuar(in SituacionDeCombate s)
        {
            // El flanqueo único vale para un escudo: al bajarlo, el siguiente se puede volver a flanquear.
            if (!s.jugadorDefendiendo) _yaFlanqueo = false;
            if (!TocaRodear(s) || s.objetivo == null) return 0f;
            if (_yaFlanqueo && s.rol != RolEnElGrupo.Rodea) return 0f;
            Vector3 punto = Cuerpo.PuntoDeRodeo();
            float falta = Vector3.Distance(Cuerpo.Transform.position, punto);
            if (falta < 1.5f) return 0.08f;
            float p = 0.5f + 0.3f * Cuerpo.Perfil.Caracter.movilidad;
            // Contra el escudo, solo si es movido; si no, espera (Esperar) a que lo baje.
            if (s.rol != RolEnElGrupo.Rodea) p *= Cuerpo.Perfil.Caracter.movilidad;
            return p;
        }

        public override void Empezar(in SituacionDeCombate s)
        {
            _hasta = Time.time + 3.5f;
            _siguienteRumbo = 0f;
            _flanqueoUnico = s.rol != RolEnElGrupo.Rodea && !DaVueltas();
            if (_flanqueoUnico)
            {
                // Punto fijo: no se recalcula desde donde va, que le haría girar sin fin.
                if (!Cuerpo.PuntoAlcanzable(Cuerpo.PuntoDeRodeo(), out _punto)) { _hasta = Time.time; return; }
                Cuerpo.MoverA(_punto, corriendo: Vector3.Distance(Cuerpo.Transform.position, _punto) > 4f);
                return;
            }
            Ir();
        }

        public override bool Actualizar(in SituacionDeCombate s)
        {
            if (!TocaRodear(s) || Time.time >= _hasta) return false;
            if (_flanqueoUnico)
            {
                if (!Cuerpo.HaLlegado(0.6f)) return true;
                _yaFlanqueo = true;
                return false;
            }
            if (Time.time >= _siguienteRumbo) Ir();
            return !Cuerpo.HaLlegado(0.6f);
        }

        private bool DaVueltas()
        {
            var perfil = Cuerpo.Perfil;
            float umbral = perfil.Caracter.umbralDeVueltasContinuas;
            return perfil.Caracter.movilidad >= umbral || perfil.Dificultad >= umbral;
        }

        public override void Terminar() => Cuerpo.Parar();

        private void Ir()
        {
            _siguienteRumbo = Time.time + 0.6f;
            if (!Cuerpo.PuntoAlcanzable(Cuerpo.PuntoDeRodeo(), out Vector3 p)) { _hasta = Time.time; return; }
            bool lejos = Vector3.Distance(Cuerpo.Transform.position, p) > 4f;
            Cuerpo.MoverA(p, corriendo: lejos);
        }
    }

    /// <summary>
    /// Sin ver al objetivo: va a donde le vio por última vez y mira alrededor. Si pasa el tiempo de
    /// búsqueda de su config sin encontrarle, deja el combate.
    /// </summary>
    public sealed class OpcionBuscar : OpcionDeCombate
    {
        private float _siguientePunto;

        public OpcionBuscar(ICombatiente cuerpo) : base(cuerpo) { }

        public override string Nombre => "Buscar";

        public override float Puntuar(in SituacionDeCombate s) => !s.veAlObjetivo && s.segundosSinVer > 0.5f ? 0.75f : 0f;

        public override void Empezar(in SituacionDeCombate s)
        {
            _siguientePunto = Time.time + 2.5f;
            Cuerpo.MoverA(Cuerpo.Memoria.UltimaPosicionVista, corriendo: false);
        }

        public override bool Actualizar(in SituacionDeCombate s)
        {
            if (s.veAlObjetivo) return false;
            if (s.segundosSinVer > Cuerpo.Perfil.DuracionDeBusqueda)
            {
                Cuerpo.AbandonarCombate();
                return false;
            }
            if (Cuerpo.HaLlegado(1f) && Time.time >= _siguientePunto)
            {
                _siguientePunto = Time.time + 2.5f;
                Vector2 azar = Random.insideUnitCircle * 4f;
                Vector3 prueba = Cuerpo.Memoria.UltimaPosicionVista + new Vector3(azar.x, 0f, azar.y);
                if (Cuerpo.PuntoAlcanzable(prueba, out Vector3 p)) Cuerpo.MoverA(p, corriendo: false);
            }
            return true;
        }

        public override void Terminar() => Cuerpo.Parar();
    }

    /// <summary>
    /// Lo de base cuando no conviene nada más: mirar al objetivo y, si es movido, dar un paso a un
    /// lado para no quedarse plantado como una estatua.
    /// </summary>
    public sealed class OpcionEsperar : OpcionDeCombate
    {
        private float _hasta;

        public OpcionEsperar(ICombatiente cuerpo) : base(cuerpo) { }

        public override string Nombre => "Esperar";

        public override float Puntuar(in SituacionDeCombate s) => 0.12f;

        public override void Empezar(in SituacionDeCombate s)
        {
            _hasta = Time.time + Random.Range(0.5f, 0.9f);
            Cuerpo.Parar();
            float movilidad = Cuerpo.Perfil.Caracter.movilidad;
            if (s.objetivo == null || !s.veAlObjetivo || Random.value > movilidad * 0.6f) return;
            var yo = Cuerpo.Transform;
            Vector3 hacia = s.objetivo.position - yo.position;
            hacia.y = 0f;
            if (hacia.sqrMagnitude < 0.01f) return;
            Vector3 lado = Vector3.Cross(Vector3.up, hacia.normalized) * (Random.value < 0.5f ? 1.5f : -1.5f);
            if (Cuerpo.PuntoAlcanzable(yo.position + lado, out Vector3 p)) Cuerpo.MoverA(p, corriendo: false);
        }

        public override bool Actualizar(in SituacionDeCombate s)
        {
            if (s.objetivo != null && Cuerpo.HaLlegado(0.3f)) Cuerpo.Mirar(s.objetivo.position);
            return Time.time < _hasta;
        }

        public override void Terminar() => Cuerpo.Parar();
    }
}
