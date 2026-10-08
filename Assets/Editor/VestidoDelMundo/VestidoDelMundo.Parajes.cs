using System;
using System.Collections.Generic;
using UnityEngine;

/// Parajes entre zonas para que explorar tenga premio a la vista: ruinas de una casa fuerte, una atalaya
/// rota sobre el acantilado, un caserío abandonado, un claustro viejo y un mirador en la subida al Reino,
/// más oficios y lugares de paso (redil, varadero, cantera, posta, era, almenara y un descanso en el cruce).
///
/// Reglas de canon que respetan (GDD § cobertura espacial y reglas de Raúl): nada en el Bosque Prohibido
/// (ni leñadores); ruinas por el paso del tiempo, no por una guerra (sin quemaduras, armas ni huesos); nada
/// que imite a las Ruinas del Libro (ni monolitos, altares, portales, puertas sueltas o selladas, anillos
/// de piedra, inscripciones o estrellas); ni molinos ni establos fuera del puerto; ni montones de tres
/// piedras, jaulas, luces entre árboles, fuegos o carros volcados (son pistas o semillas de la historia);
/// sin edificios del pack de casas en lugares abandonados (la Noche les enciende luz y ventanas); sin
/// carteles con texto ni objetos que se puedan recoger.
public static partial class VestidoDelMundo
{
    private sealed class Paraje
    {
        public string Nombre;
        public Vector2 Centro;
        /// Senda de tierra desde el camino más cercano hasta el paraje (null si está junto al camino).
        public Vector2[] Senda;
        /// Radio del suelo pisado o del enlosado viejo que se pinta bajo el paraje.
        public float RadioSuelo = 8f;
        public string CapaSuelo = CapaTierraPiedras;
        public Action<Obra, Transform, Paraje> Receta;
        public int Semilla;

        /// Rumbo del frente del paraje: mira por donde llega su senda (su último tramo antes del centro).
        public float Frente
        {
            get
            {
                if (Senda == null) return 0f;
                for (int i = Senda.Length - 1; i >= 0; i--)
                    if (Vector2.Distance(Senda[i], Centro) > 1f) return Rumbo(Senda[i] - Centro);
                return 0f;
            }
        }

        /// Punto de mundo a partir de coordenadas locales (x a la derecha, z hacia el frente).
        public Vector2 Local(float x, float z)
        {
            float r = Frente * Mathf.Deg2Rad;
            var f = new Vector2(Mathf.Sin(r), Mathf.Cos(r));
            var d = new Vector2(f.y, -f.x);
            return Centro + d * x + f * z;
        }
    }

    private const string Columna = Tiny + "BuildingUtilityDeco/Pillar01.prefab";
    private const string ColumnaPartida = Tiny + "BuildingUtilityDeco/Pillar02.prefab";
    private const string ColumnaQuebrada = Tiny + "BuildingUtilityDeco/Pillar04.prefab";
    private const string MuroDerruido = Tiny + "BuildingUtilityDeco/Wall01.prefab";
    private const string MuroEnL = Tiny + "BuildingUtilityDeco/Wall02.prefab";
    private const string MuroCurvo = Tiny + "BuildingUtilityDeco/Wall03.prefab";
    /// Vano de puerta abierto en un muro (Modular Castle va a doble escala: se mide por su alto).
    private const string Vano = ModularCastle + "doorway1.prefab";
    private const string PozoViejo = Tiny + "BuildingUtilityDeco/Well01.prefab";
    private const string ValladoRecto = Tiny + "BuildingUtilityDeco/WoodFence01.prefab";
    private const string Losas = Tiny + "Rock/CobbleStoneCircle01.prefab";
    /// Un solo bloque. Stone01_a02 no se usa: son tres sillares apilados, la señal de la escena 2.7.
    private const string Sillar = FK + "Props/Engineering/Stone01_a01.prefab";
    private const string Roca = FK + "Rock/Rock02_a01.prefab";
    private const string PilaDeTroncos = FK + "Props/Goods/Wood05_a01.prefab";
    private const string Tablones = FK + "Props/Goods/Wood03_a01.prefab";
    private const string FrenteDeCantera = "Assets/Art/World/Unvik_3D/Cross_Plains/Prefabs/CrossPlains_Rock_02.prefab";

    private static Paraje[] Parajes() => new[]
    {
        new Paraje
        {
            Nombre = "Ruinas de la casa fuerte", Centro = new Vector2(92f, -274f), RadioSuelo = 10f, CapaSuelo = CapaAdoquin, Semilla = 5101,
            Senda = P(116f, -244f, 104f, -252f, 92f, -264f), Receta = RecetaCasaFuerte,
        },
        new Paraje
        {
            Nombre = "Atalaya rota del acantilado sur", Centro = new Vector2(-57f, -373f), RadioSuelo = 7f, Semilla = 5102,
            Senda = P(-140f, -240f, -144f, -252f, -144f, -264f, -144f, -276f, -140f, -288f, -136f, -300f, -136f, -312f, -136f, -324f,
                -132f, -336f, -124f, -348f, -112f, -360f, -100f, -368f, -90f, -378f, -78f, -381f, -61f, -379f),
            Receta = RecetaAtalaya,
        },
        new Paraje
        {
            Nombre = "Caserío abandonado", Centro = new Vector2(-172f, -200f), RadioSuelo = 13f, CapaSuelo = CapaTierra, Semilla = 5103,
            Senda = P(-152f, -244f, -152f, -232f, -152f, -220f, -160f, -208f), Receta = RecetaCaserio,
        },
        new Paraje
        {
            Nombre = "Claustro viejo", Centro = new Vector2(-24f, -226f), RadioSuelo = 9f, CapaSuelo = CapaAdoquin, Semilla = 5104,
            Senda = P(-60f, -180f, -60f, -192f, -56f, -204f, -44f, -216f, -32f, -224f), Receta = RecetaClaustro,
        },
        new Paraje
        {
            Nombre = "Redil del pastor", Centro = new Vector2(-297f, -230f), RadioSuelo = 9f, CapaSuelo = CapaTierra, Semilla = 5106,
            Senda = P(-280f, -292f, -292f, -292f, -304f, -292f, -316f, -292f, -324f, -280f, -324f, -268f, -316f, -256f, -309f, -243f),
            Receta = RecetaRedil,
        },
        new Paraje
        {
            Nombre = "Varadero de la playa oeste", Centro = new Vector2(-400f, -248f), RadioSuelo = 0f, Semilla = 5107,
            Senda = P(-380f, -284f, -388f, -272f, -400f, -260f), Receta = RecetaVaradero,
        },
        new Paraje
        {
            Nombre = "Cantera vieja", Centro = new Vector2(87f, 66f), RadioSuelo = 10f, Semilla = 5108,
            Senda = P(76f, 60f, 79.8f, 63.8f), Receta = RecetaCantera,
        },
        new Paraje
        {
            Nombre = "Posta del cruce", Centro = new Vector2(164f, 56f), RadioSuelo = 8f, CapaSuelo = CapaTierra, Semilla = 5109,
            Senda = P(172f, 62f, 169f, 60f), Receta = RecetaPosta,
        },
        new Paraje
        {
            Nombre = "Era de trilla", Centro = new Vector2(160f, -76f), RadioSuelo = 9f, CapaSuelo = CapaTierra, Semilla = 5110,
            Senda = P(112f, -40f, 124f, -48f, 136f, -52f, 148f, -60f, 156f, -70f), Receta = RecetaEra,
        },
        new Paraje
        {
            Nombre = "Almenara del acantilado este", Centro = new Vector2(358f, -314f), RadioSuelo = 7f, Semilla = 5111,
            Senda = P(240f, -396f, 232f, -384f, 228f, -372f, 220f, -360f, 220f, -348f, 232f, -340f, 244f, -328f, 256f, -316f, 268f, -308f,
                280f, -308f, 292f, -308f, 304f, -312f, 316f, -312f, 328f, -312f, 340f, -312f, 350f, -314f),
            Receta = RecetaAlmenara,
        },
        new Paraje
        {
            Nombre = "Mirador de la subida al Reino", Centro = new Vector2(10.2f, 190.5f), RadioSuelo = 6f, CapaSuelo = CapaAdoquin, Semilla = 5113,
            Senda = P(8.4f, 226.8f, 9f, 212f, 10f, 199f), Receta = RecetaMirador,
        },
        new Paraje
        {
            Nombre = "Descanso del cruce", Centro = new Vector2(-90f, -124f), RadioSuelo = 6f, CapaSuelo = CapaTierra, Semilla = 5112,
            Senda = P(-92f, -100f, -90f, -111f), Receta = RecetaDescanso,
        },
    };

    // ── Suelo ────────────────────────────────────────────────────────────────────────────────

    private static void PintarSuelosDeParajes(Lienzo l, List<string> informe)
    {
        TerrainData datos = l.Datos;
        Vector3 origen = l.Terreno.transform.position;
        bool Fuera(float x, float z)
        {
            float nx = (x - origen.x) / datos.size.x, nz = (z - origen.z) / datos.size.z;
            if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) return true;
            if (BosqueProhibido.Contains(new Vector2(x, z))) return true;
            return datos.GetSteepness(nx, nz) > 32f || l.Altura(x, z) < 0.6f;
        }
        int n = 0;
        foreach (Paraje p in Parajes())
        {
            if (p.Senda != null && p.Senda.Length > 1)
            {
                var senda = new Vector2[p.Senda.Length + 1];
                Array.Copy(p.Senda, senda, p.Senda.Length);
                senda[senda.Length - 1] = p.Centro;
                PintarSenda(l, senda, 2.6f, p.Semilla, Fuera, CapaTierra, CapaTierra, 1.2f);
            }
            if (p.RadioSuelo > 0f)
            {
                PintarMancha(l, p.Centro, p.RadioSuelo, p.CapaSuelo, 0.75f, p.Semilla + 1, Fuera);
                PintarMancha(l, p.Centro, p.RadioSuelo * 1.25f, CapaTierraPiedras, 0.35f, p.Semilla + 2, Fuera, duro: false);
            }
            n++;
        }
        informe.Add($"Suelo de parajes: {n} lugares con su senda desde el camino.");
    }

    // ── Detalles ─────────────────────────────────────────────────────────────────────────────

    private static void PoblarParajes(Obra o)
    {
        Transform raiz = Grupo(o.Raiz, "Parajes entre zonas");
        foreach (Paraje p in Parajes())
        {
            int antes = o.Puestas;
            Transform grupo = Grupo(raiz, p.Nombre);
            p.Receta(o, grupo, p);
            o.Informe.Add($"{p.Nombre}: {o.Puestas - antes} piezas.");
        }
    }

    /// Pieza de paraje: se toca con las demás del mismo paraje y se hunde un poco en el terreno.
    private static GameObject PP(Obra o, Transform g, Paraje p, string prefab, string nombre, float x, float z, float rumboLocal,
        float tamano = 1f, Medida medida = Medida.Escala, float hundir = 0.2f, float inclinar = 0f, float desnivel = 2.2f)
        => Pon(o, g, prefab, nombre, p.Local(x, z), p.Frente + rumboLocal, tamano, medida, camino: true, holgura: 0.05f,
            desnivel: desnivel, corredor: false, hundir: hundir, inclinar: inclinar, solapePropio: true);

    private static void Sillares(Obra o, Transform g, Paraje p, int cuantos, float radio)
    {
        var dado = new Ruido.Dado(p.Semilla + 77);
        for (int i = 0; i < cuantos; i++)
        {
            float a = dado.Entre(0f, Mathf.PI * 2f), r = dado.Entre(radio * 0.4f, radio);
            PP(o, g, p, Sillar, "Sillar caído", Mathf.Cos(a) * r, Mathf.Sin(a) * r,
                dado.Entre(0f, 360f), dado.Entre(0.9f, 1.4f), hundir: dado.Entre(0.05f, 0.3f), inclinar: dado.Entre(-12f, 12f));
        }
    }

    private static void FloresSilvestres(Obra o, Transform g, Paraje p, int cuantas, float radio)
    {
        var dado = new Ruido.Dado(p.Semilla + 91);
        for (int i = 0; i < cuantas; i++)
        {
            float a = dado.Entre(0f, Mathf.PI * 2f), r = dado.Entre(radio * 0.3f, radio);
            PP(o, g, p, Flor, "Flores silvestres", Mathf.Cos(a) * r, Mathf.Sin(a) * r, dado.Entre(0f, 360f), dado.Entre(0.8f, 1.3f), hundir: 0f);
        }
    }

    private static void ArbolDeParaje(Obra o, Transform g, Paraje p, string prefab, float x, float z, float alto)
    {
        GameObject a = PP(o, g, p, prefab, "Árbol", x, z, x * 37f + z * 11f, alto, Medida.Alto, hundir: 0.2f);
        if (a != null) CopiarMaterialesDelVecino(o, a, prefab);
    }

    /// Casa fuerte en ruinas: muro delantero con su vano de entrada (la puerta de un muro, nunca una puerta
    /// suelta), muros caídos, pórtico de columnas rotas, una columna tumbada y un árbol que ha crecido dentro.
    private static void RecetaCasaFuerte(Obra o, Transform g, Paraje p)
    {
        PP(o, g, p, Vano, "Vano de la entrada", 0f, 7.5f, 0f, 4.6f, Medida.Alto, hundir: 0.35f);
        PP(o, g, p, MuroDerruido, "Muro delantero", -5.3f, 7.5f, 90f, hundir: 0.5f);
        PP(o, g, p, MuroDerruido, "Muro delantero", 5.3f, 7.5f, 270f, hundir: 0.8f, inclinar: 3f);
        PP(o, g, p, MuroDerruido, "Muro derruido", -7.5f, 0.5f, 0f, hundir: 0.6f);
        PP(o, g, p, MuroDerruido, "Muro derruido", 7.5f, -0.5f, 180f, hundir: 0.9f, inclinar: 4f);
        PP(o, g, p, MuroEnL, "Esquina de muro", -4.5f, -6.5f, 180f, hundir: 0.5f);
        PP(o, g, p, MuroCurvo, "Ábside caído", 4.5f, -7f, 90f, hundir: 0.7f);
        PP(o, g, p, Columna, "Columna del pórtico", -3f, 3.5f, 0f, hundir: 0.25f);
        PP(o, g, p, ColumnaQuebrada, "Columna quebrada", 3f, 3.5f, 0f, hundir: 0.25f);
        PP(o, g, p, ColumnaPartida, "Columna partida", -3f, -1.5f, 20f, hundir: 0.3f);
        PP(o, g, p, Columna, "Columna tumbada", 2.6f, -1.2f, 65f, hundir: 0.45f, inclinar: 86f);
        Sillares(o, g, p, 6, 9f);
        ArbolDeParaje(o, g, p, ArbolesDeJardin[1], -0.5f, -2.5f, 7f);
        FloresSilvestres(o, g, p, 6, 8f);
    }

    /// Atalaya: muñón de torre redonda (dos muros curvos enfrentados, uno más hundido), piedras caídas
    /// y rocas del cantil. Sin ventana ni luz: no debe leerse como la torre de Liam.
    private static void RecetaAtalaya(Obra o, Transform g, Paraje p)
    {
        PP(o, g, p, MuroCurvo, "Muro de la atalaya", 0f, 2.6f, 0f, hundir: 0.4f);
        PP(o, g, p, MuroCurvo, "Muro de la atalaya", 0f, -2.6f, 180f, hundir: 1.6f, inclinar: 5f);
        PP(o, g, p, ColumnaQuebrada, "Contrafuerte roto", -4.2f, 0f, 90f, hundir: 0.3f);
        PP(o, g, p, ColumnaPartida, "Piedra de la almena", 4.8f, 3.6f, 30f, hundir: 0.4f, inclinar: 80f);
        Sillares(o, g, p, 7, 8f);
        PP(o, g, p, Roca, "Roca del cantil", 6.5f, -5f, 40f, 2.2f, Medida.Alto, hundir: 0.5f);
        PP(o, g, p, Roca, "Roca del cantil", -6f, -5.5f, 200f, 1.6f, Medida.Alto, hundir: 0.5f);
        FloresSilvestres(o, g, p, 4, 6f);
    }

    /// Caserío: tres casas sin tejado (muros sueltos en ángulo), un pozo seco y un frutal asilvestrado.
    private static void RecetaCaserio(Obra o, Transform g, Paraje p)
    {
        var casas = new (float x, float z, float giro)[] { (-9f, 1f, 10f), (8.5f, 3f, -15f), (0f, -9.5f, 95f) };
        foreach (var (x, z, giro) in casas)
        {
            PP(o, g, p, MuroEnL, "Casa sin tejado", x, z, giro, hundir: 0.6f);
            Vector2 lado = new Vector2(Mathf.Cos(giro * Mathf.Deg2Rad), -Mathf.Sin(giro * Mathf.Deg2Rad));
            PP(o, g, p, MuroDerruido, "Pared caída", x + lado.x * 3.5f, z + lado.y * 3.5f, giro + 90f, hundir: 1.1f, inclinar: 6f);
        }
        PP(o, g, p, PozoViejo, "Pozo seco", 1.5f, 1.5f, 30f, hundir: 0.2f);
        ArbolDeParaje(o, g, p, ArbolesDeJardin[0], -3f, 7.5f, 6f);
        Sillares(o, g, p, 5, 12f);
        FloresSilvestres(o, g, p, 6, 11f);
    }

    /// Claustro viejo: dos crujías de columnas (sin arcos sueltos, que se leerían como portales), un tramo
    /// de muro al fondo y un fuste caído.
    private static void RecetaClaustro(Obra o, Transform g, Paraje p)
    {
        PP(o, g, p, Columna, "Columna del claustro", -5.7f, 3f, 0f, hundir: 0.3f);
        PP(o, g, p, ColumnaPartida, "Columna del claustro", -1.9f, 3f, 30f, hundir: 0.3f);
        PP(o, g, p, Columna, "Columna del claustro", 1.9f, 3f, 0f, hundir: 0.3f);
        PP(o, g, p, ColumnaQuebrada, "Columna del claustro", 5.7f, 3f, -15f, hundir: 0.3f);
        PP(o, g, p, MuroDerruido, "Muro del fondo", 0f, -7.5f, 90f, hundir: 0.9f);
        PP(o, g, p, Columna, "Columna del claustro", -4.5f, -4f, 0f, hundir: 0.3f);
        PP(o, g, p, ColumnaPartida, "Columna del claustro", 0f, -4f, 45f, hundir: 0.3f);
        PP(o, g, p, ColumnaQuebrada, "Columna del claustro", 4.5f, -4f, 10f, hundir: 0.3f);
        PP(o, g, p, ColumnaPartida, "Fuste caído", -1.5f, -0.8f, 100f, hundir: 0.4f, inclinar: 84f);
        Sillares(o, g, p, 5, 8f);
        ArbolDeParaje(o, g, p, ArbolesDeJardin[2], 6.5f, -1f, 5.5f);
        FloresSilvestres(o, g, p, 7, 7f);
    }

    /// Redil: cerca de madera alrededor de un corral, cobertizo de tablones, abrevadero y heno. Sin edificios
    /// del pack de casas: sus ventanas se encienden de noche y en el campo se leerían como una pista.
    private static void RecetaRedil(Obra o, Transform g, Paraje p)
    {
        // Corral cuadrado de unos 7 m; el lado derecho deja un paso junto a la esquina delantera.
        PP(o, g, p, ValladoRecto, "Cerca del redil", 0f, 3.8f, 0f, hundir: 0.05f);
        PP(o, g, p, ValladoRecto, "Cerca del redil", -3.8f, 0f, 90f, hundir: 0.05f);
        PP(o, g, p, ValladoRecto, "Cerca del redil", 0f, -3.8f, 0f, hundir: 0.05f);
        PP(o, g, p, ValladoRecto, "Cerca del redil", 3.8f, -1.6f, 90f, hundir: 0.05f);
        PP(o, g, p, MuroEnL, "Cobertizo del pastor", 9.5f, 2.5f, -90f, hundir: 0.9f);
        PP(o, g, p, Tablones, "Tablones del cobertizo", 8.5f, 4.8f, 10f, hundir: 0.02f);
        PP(o, g, p, Abrevadero, "Abrevadero", 0f, 0f, 30f, hundir: 0.05f);
        PP(o, g, p, Heno, "Paca de heno", 6.5f, -4.5f, 15f, hundir: 0.05f);
        PP(o, g, p, Heno, "Paca de heno", 7.8f, -6f, 60f, hundir: 0.05f);
    }

    /// Varadero: una barca partida medio enterrada, otra boca abajo, redes a secar y material para
    /// repararlas. Sin edificios en la playa, ni nasas (tienen forma de jaula) ni fuegos: no debe leerse
    /// como un campamento.
    private static void RecetaVaradero(Obra o, Transform g, Paraje p)
    {
        PP(o, g, p, Barca, "Barca partida", -3f, -2f, 35f, hundir: 0.45f, inclinar: 22f);
        PP(o, g, p, Barca, "Barca boca abajo", 4f, 1f, -20f, hundir: 0.05f, inclinar: 180f);
        for (int i = 0; i < 3; i++) PP(o, g, p, Red, "Red puesta a secar", -6f + i * 2.4f, 5f, 90f, hundir: 0.1f);
        PP(o, g, p, Tablones, "Tablones para el casco", 6.5f, 3.5f, 30f, hundir: 0.02f);
        PP(o, g, p, Barril, "Barril de brea", 7.5f, 1.2f, 0f, hundir: 0.02f);
        PP(o, g, p, PilaDeTroncos, "Maderos", -6.5f, -4f, 70f, hundir: 0.02f);
    }

    /// Cantera vieja: frente de roca cortado, cobertizo del cantero sin tejado, sillares a medio labrar y
    /// una carreta. Sin edificios del pack de casas (sus ventanas se encienden de noche).
    private static void RecetaCantera(Obra o, Transform g, Paraje p)
    {
        PP(o, g, p, FrenteDeCantera, "Frente de la cantera", 0f, -9f, 90f, 6f, Medida.Alto, hundir: 0.6f, desnivel: 4f);
        PP(o, g, p, MuroEnL, "Cobertizo del cantero", -4f, 2f, 20f, hundir: 0.7f);
        PP(o, g, p, Tablones, "Tablones del cobertizo", -6.5f, 4.5f, 70f, hundir: 0.02f);
        Sillares(o, g, p, 8, 9f);
        PP(o, g, p, Carreta, "Carreta de la cantera", 6f, 3f, 110f, hundir: 0.05f, desnivel: 1.2f);
    }

    /// Posta del cruce, habitada: tienda de paso (de noche, con luz), banco, abrevadero y un barril. Sin poste
    /// indicador: el del pack lleva letras talladas.
    private static void RecetaPosta(Obra o, Transform g, Paraje p)
    {
        PP(o, g, p, PrefabsEdificios + "BuildingAT28.prefab", "Tienda de la posta", 0f, -1.5f, 0f, hundir: 0.1f, desnivel: 1.2f);
        PP(o, g, p, Banco, "Banco de la posta", 4.5f, 2.5f, 0f, hundir: 0.02f);
        PP(o, g, p, Abrevadero, "Abrevadero de la posta", -4.5f, 2.5f, 0f, hundir: 0.05f);
        PP(o, g, p, Barril, "Barril de la posta", -3.2f, -4.5f, 0f, hundir: 0.02f);
    }

    /// Era de trilla: círculo enlosado, almiares y una carretilla.
    private static void RecetaEra(Obra o, Transform g, Paraje p)
    {
        PP(o, g, p, Losas, "Era enlosada", 0f, 0f, 0f, 0.8f, hundir: 0.12f, desnivel: 2f);
        PP(o, g, p, HenoGrande, "Almiar", -7f, -3f, 0f, hundir: 0.1f);
        PP(o, g, p, HenoGrande, "Almiar", -6f, 3.5f, 40f, hundir: 0.1f);
        PP(o, g, p, Heno, "Paca de heno", 6.5f, 4f, 20f, hundir: 0.05f);
        PP(o, g, p, Carretilla, "Carretilla", 6f, -3f, 120f, hundir: 0.05f);
    }

    /// Almenara: parapeto en L hacia el mar con la leña apilada y sin encender a su abrigo y un par de
    /// sillares sueltos. Sin anillos de piedra ni losa redonda (se leerían como un altar) y sin fuego
    /// (Fire0*, que la Noche enciende).
    private static void RecetaAlmenara(Obra o, Transform g, Paraje p)
    {
        PP(o, g, p, MuroEnL, "Parapeto de la almenara", 0f, -3f, 180f, hundir: 0.8f);
        PP(o, g, p, PilaDeTroncos, "Leña apilada, sin encender", 1.6f, 0.6f, 25f, hundir: 0.02f);
        PP(o, g, p, PilaDeTroncos, "Leña apilada, sin encender", 1.1f, 2.2f, 110f, hundir: 0.02f);
        PP(o, g, p, Sillar, "Sillar suelto", -2.8f, 1.9f, 35f, hundir: 0.1f);
        PP(o, g, p, Sillar, "Sillar suelto", 3.6f, -0.4f, 70f, 1.3f, hundir: 0.15f);
    }

    /// Mirador sobre la subida al Reino: pretil roto de cara al valle (se ven el pueblo de Will y el
    /// puerto), dos columnas partidas y sillares para sentarse.
    private static void RecetaMirador(Obra o, Transform g, Paraje p)
    {
        PP(o, g, p, MuroDerruido, "Pretil del mirador", 0f, -6f, 90f, hundir: 2.6f, desnivel: 3f);
        PP(o, g, p, ColumnaPartida, "Columna del mirador", -4.5f, -4.5f, 15f, hundir: 0.3f);
        PP(o, g, p, ColumnaQuebrada, "Columna del mirador", 4.5f, -4.5f, -20f, hundir: 0.3f);
        PP(o, g, p, Sillar, "Sillar para sentarse", -1.6f, -1.5f, 80f, 1.5f, hundir: 0.1f);
        PP(o, g, p, Sillar, "Sillar para sentarse", 1.6f, -1.8f, 100f, 1.5f, hundir: 0.1f);
        FloresSilvestres(o, g, p, 5, 6f);
    }

    /// Descanso del cruce: pozo, abrevadero, dos bancos y un parterre a la sombra.
    private static void RecetaDescanso(Obra o, Transform g, Paraje p)
    {
        PP(o, g, p, Pozo, "Pozo del cruce", 0f, 0f, 0f, 1.2f, hundir: 0.05f);
        PP(o, g, p, Abrevadero, "Abrevadero del cruce", 3f, -1f, 90f, hundir: 0.05f);
        PP(o, g, p, Banco, "Banco del cruce", -3.5f, 1.5f, 90f, hundir: 0.02f);
        PP(o, g, p, Banco, "Banco del cruce", 0f, 4f, 180f, hundir: 0.02f);
        PP(o, g, p, Parterres[0], "Parterre", -4f, -3.5f, 0f, hundir: 0.02f);
        ArbolDeParaje(o, g, p, ArbolesDeJardin[1], 5f, 4.5f, 6.5f);
    }

    /// Bosque Prohibido: ni ruinas ni oficios (canon). Es la caja de Ambient_ForbidenWoods en MainWorld.
    private static readonly Rect BosqueProhibido = Rect.MinMaxRect(-445f, -152f, -135f, 234f);

    /// Zonas que no se tapan (anclas, recorridos de escolta, arenas, reservas, puentes e hitos).
    private static readonly Zona[] ZonasLibres =
    {
        new Zona("Pueblo de Will (referencia de estilo, no se toca)", -0.6f, -130.3f, 72.0f),
        new Zona("Ambient_WillTown (AmbientZone + ZonaSinArenas)", -2.14f, -134.1f, 65.2f),
        new Zona("Woods_Entrance_SavePoint", -5.18f, -81.11f, 5.0f),
        new Zona("Anchor_Eldran_PuntoGuardado", -5.18f, -81.11f, 6.0f),
        new Zona("SPAWN_Renard", -2.18f, -81.11f, 3.0f),
        new Zona("Anchor_Eldran_Bosque", 25.8f, -82.61f, 10.0f),
        new Zona("Escolta de Eldran: plaza → salida norte", 4.0f, -112.0f, 14.0f),
        new Zona("Escolta de Eldran: paso de la puerta norte (INC-463)", 4.0f, -91.0f, 12.0f),
        new Zona("Escolta de Eldran: punto de guardado → bosque", 10.3f, -81.9f, 9.0f),
        new Zona("Arena dinámica del Demonio 1 (estimada)", 41.1f, -54.3f, 26.0f),
        new Zona("Caja de Eldran bajo un árbol", -16.05f, 21.44f, 6.0f),
        Zona.Rectangulo("Plaza Real — audiencia y arena del Demonio 2", -28f, 283.5f, 28f, 312.5f),
        Zona.Rectangulo("Escalinata Real (escolta del guardia al castillo)", -7f, 268.5f, 7f, 283.5f),
        Zona.Rectangulo("Plaza del Mercado y de la Taberna (encuentro y persecución)", -24f, 247f, 24f, 271f),
        Zona.Rectangulo("Bosque Prohibido (caja de Ambient_ForbidenWoods)", BosqueProhibido.xMin, BosqueProhibido.yMin, BosqueProhibido.xMax, BosqueProhibido.yMax),
        Zona.Rectangulo("Solar de Silas: casa y taller del reloj (GDD § 9), por construir", 281f, -118f, 299f, -102f),
        Zona.Rectangulo("Plaza del pueblo vecino (GDD 15)", 315f, -127f, 345f, -103f),
        new Zona("Castillo — acceso despejado", 0f, 320f, 12.0f),
        new Zona("Sala del trono y calabozo (pendiente)", 0f, 355f, 8.0f),
        new Zona("Puerta Real (paso de la muralla)", 92.9f, 243.0f, 12.0f),
        new Zona("Huerto del pueblo vecino", 300f, -140f, 7.5f),
        new Zona("Huerto del pueblo vecino 2", 360f, -92f, 6.7f),
        new Zona("Embarcadero — rampa y pasarela central", 270f, -462f, 12.0f),
        new Zona("Muelle transversal", 269.5f, -484f, 22.0f),
        new Zona("Huerto del puerto", 232f, -405f, 7.5f),
        new Zona("Huerto grande de las granjas", 254f, 166f, 12.5f),
        new Zona("Huerto de la casa 1 (granjas)", 208f, 142f, 7.8f),
        new Zona("Huerto junto al arroyo (granjas)", 274f, 154f, 8.6f),
        new Zona("Huerta del pueblo 1", 84f, -177f, 13.4f),
        new Zona("Huerta del pueblo 2", 102f, -177f, 13.4f),
        new Zona("Huerta del pueblo 3", 120f, -177f, 13.4f),
        new Zona("Pradera del despertar — arena del Demonio 1", -62f, -61f, 20.3f),
        new Zona("Descanso del camino — arena del Gólem", -95f, 65f, 26.0f),
        new Zona("Claro de Estela", -330f, 65f, 22.7f),
        new Zona("Puente_camino_2", 244.8f, 213.9f, 16.0f),
        new Zona("Puente_camino_4", -170.4f, -45.9f, 23.0f),
        new Zona("Puente_camino_5", -117.5f, -226.6f, 18.0f),
        new Zona("Puente_camino_8", -189.8f, 33.4f, 17.0f),
        new Zona("Cascada, poza y mirador", 182.0f, 260.0f, 18.0f),
        new Zona("Faro (torre vigía)", 365f, -470f, 10.0f),
        new Zona("Piedra Ancestral — Ruinas del Libro", 565f, -505f, 16.0f),
        new Zona("Isla de las Ruinas — playa de llegada", 489f, -489f, 8.0f),
        new Zona("El Rompiente", 385f, -565f, 26.0f),
        new Zona("Pasarela derrumbable 1", 403.5f, -557.6f, 10.0f),
        new Zona("Pasarela derrumbable 2", 425.5f, -549.3f, 8.0f),
        new Zona("Pasarela derrumbable 3", 444.0f, -542.0f, 8.0f),
        new Zona("Pasarela derrumbable 4", 461.7f, -518.9f, 20.0f),
        new Zona("Isla del hechicero — claro y cabaña", 545f, -180f, 20.0f),
        new Zona("Isla secreta — cabaña del náufrago", 557f, 472f, 14.0f),
        new Zona("Las Hermanas — cofre de la mayor", -496f, 473f, 5.0f),
        new Zona("Las Hermanas — cofre de la pequeña", -423f, 562f, 5.0f),
        new Zona("Isla del Gigante — guarida", -558f, -562f, 10.0f),
    };
}
