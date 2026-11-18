using UnityEngine;

namespace Game.NPC.Combate
{
    /// <summary>
    /// Memoria corta de un NPC en combate: dónde vio por última vez al objetivo, cuánto daño ha
    /// recibido hace poco, qué escondites le han fallado, cuándo atacó y qué hechizo entrante ya
    /// decidió esquivar o no. Todo caduca solo; nada se guarda entre combates.
    /// </summary>
    public sealed class MemoriaDeCombate
    {
        private const int Golpes = 8;
        private const int Escondites = 4;
        private const float SegundosDeEsconditeDescartado = 12f;
        private const float RadioDeEsconditeDescartado = 2.5f;

        private readonly float[] _golpeCuando = new float[Golpes];
        private readonly float[] _golpeCuanto = new float[Golpes];
        private int _golpeSiguiente;

        private readonly Vector3[] _escondite = new Vector3[Escondites];
        private readonly float[] _esconditeCuando = new float[Escondites];
        private int _esconditeSiguiente;

        public Vector3 UltimaPosicionVista { get; private set; }
        public float UltimaVezVisto { get; private set; } = -999f;
        public float UltimoAtaquePropio { get; private set; } = -999f;
        public float UltimoGolpeRecibido { get; private set; } = -999f;
        public Vector3 DeDondeVinoElGolpe { get; private set; }

        /// <summary>Hasta cuándo deja el turno de ataque a los compañeros tras atacar.</summary>
        public float DescansoDeTurnoHasta { get; set; } = -999f;

        private int _amenazaVista;
        private bool _amenazaEsquivable;

        public void Olvidar()
        {
            for (int i = 0; i < Golpes; i++) { _golpeCuando[i] = -999f; _golpeCuanto[i] = 0f; }
            for (int i = 0; i < Escondites; i++) _esconditeCuando[i] = -999f;
            UltimaVezVisto = -999f;
            UltimoAtaquePropio = -999f;
            UltimoGolpeRecibido = -999f;
            DescansoDeTurnoHasta = -999f;
            _amenazaVista = 0;
        }

        public void Visto(Vector3 donde)
        {
            UltimaPosicionVista = donde;
            UltimaVezVisto = Time.time;
        }

        public float SegundosSinVer => Time.time - UltimaVezVisto;

        public void Atacado() => UltimoAtaquePropio = Time.time;
        public float SegundosDesdeMiAtaque => Time.time - UltimoAtaquePropio;

        public void Golpeado(float cuanto, Vector3 desde)
        {
            _golpeCuando[_golpeSiguiente] = Time.time;
            _golpeCuanto[_golpeSiguiente] = cuanto;
            _golpeSiguiente = (_golpeSiguiente + 1) % Golpes;
            UltimoGolpeRecibido = Time.time;
            DeDondeVinoElGolpe = desde;
        }

        public float SegundosDesdeElGolpe => Time.time - UltimoGolpeRecibido;

        /// <summary>Daño recibido en los últimos 'segundos'.</summary>
        public float DanoReciente(float segundos)
        {
            float desde = Time.time - segundos, total = 0f;
            for (int i = 0; i < Golpes; i++)
                if (_golpeCuando[i] >= desde) total += _golpeCuanto[i];
            return total;
        }

        /// <summary>Ese escondite le ha fallado (le han visto o rodeado): no vuelve a él en un rato.</summary>
        public void DescartarCobertura(Vector3 donde)
        {
            _escondite[_esconditeSiguiente] = donde;
            _esconditeCuando[_esconditeSiguiente] = Time.time;
            _esconditeSiguiente = (_esconditeSiguiente + 1) % Escondites;
        }

        public bool CoberturaDescartada(Vector3 donde)
        {
            float desde = Time.time - SegundosDeEsconditeDescartado;
            float r2 = RadioDeEsconditeDescartado * RadioDeEsconditeDescartado;
            for (int i = 0; i < Escondites; i++)
                if (_esconditeCuando[i] >= desde && (_escondite[i] - donde).sqrMagnitude < r2) return true;
            return false;
        }

        /// <summary>
        /// Decide una sola vez por hechizo entrante si lo esquivará (con la probabilidad que dé su
        /// dificultad): así no esquiva todo ni cambia de idea a cada fotograma.
        /// </summary>
        public bool DecideEsquivar(int idDelHechizo, float probabilidad)
        {
            if (idDelHechizo != _amenazaVista)
            {
                _amenazaVista = idDelHechizo;
                _amenazaEsquivable = Random.value < probabilidad;
            }
            return _amenazaEsquivable;
        }
    }
}
