using System.Collections.Generic;
using UnityEngine;

// ATENCION AL ORDEN DE ESTE ARCHIVO (16 sep 2026)
//
// ShotType y ShotFraming vivian en su propio ShotFraming.cs, que es donde tendrian que estar por
// organizacion. Se han traido aqui porque el Editor dejo ese archivo en un estado de importacion
// del que no salia: existia en disco, con su .meta y su GUID (no duplicado), pero Unity NO lo
// incluia en la lista de fuentes que pasa al compilador — verificado en Logs/Editor.log — asi que
// estos dos tipos "no existian" para el resto del proyecto y salian 30 errores CS0246 en archivos
// que si compilaban. No era un error de codigo: ni reescribir el archivo ni cambiarle el contenido
// (para forzar un hash distinto) hicieron que Unity volviera a mirarlo.
//
// Tenerlos aqui hace que el problema no pueda repetirse, y tampoco es ninguna herejia: son dos
// tipos pequenos que solo existen para alimentar a este solver.

/// Los tipos de plano que el sistema sabe calcular solo.
///
/// La lista es corta a propósito: la gramática de una escena de diálogo es finita, y con estos
/// siete se cubre prácticamente todo lo que hacen las doce secuencias del juego. Añadir un tipo
/// nuevo solo tiene sentido cuando haya una escena que de verdad lo pida.
public enum ShotType
{
    /// Plano general: todos los sujetos en cuadro, con aire. Sirve para situar la escena.
    Wide = 0,

    /// Dos sujetos de perfil, la cámara perpendicular al eje que los une (con un poco de escorzo
    /// para que no quede plano). Es el plano de conversación por excelencia.
    TwoShot = 1,

    /// Por encima del hombro: vemos al sujeto desde detrás del hombro del otro. El hombro en
    /// primer término es lo que da profundidad y lo que ata a los dos personajes en el espacio.
    OverTheShoulder = 2,

    /// Plano medio de un sujeto, de cintura para arriba, en tres cuartos.
    Medium = 3,

    /// Primer plano: cabeza y hombros, en tres cuartos.
    CloseUp = 4,

    /// Primer plano del que ESCUCHA. Geométricamente es un CloseUp, pero se declara aparte porque
    /// obliga a respetar el lado del eje de acción (ver ShotComposer) y porque leer "Reaction" en
    /// el asset dice qué se está montando mucho mejor que "CloseUp del otro".
    Reaction = 5,

    /// Plano pensado para un sujeto EN MOVIMIENTO (alguien corriendo, un proyectil en vuelo).
    /// Encuadra más suelto que un plano medio y está pensado para usarse con 'live' activado,
    /// recalculándose cada frame.
    Tracking = 6,
}

/// La descripción de un plano en palabras, en vez de en coordenadas.
///
/// Esta es la pieza central de todo el asunto: un CinematicShot colocado a mano guarda una
/// posición del mundo ("x=142.3, y=4.1, z=88.7"), que es información sobre el MAPA y deja de
/// valer en cuanto la escena cambia — el jugador llega por otro lado, un NPC se ha movido, el
/// terreno se ha remodelado. Un ShotFraming guarda la INTENCIÓN ("primer plano de Oliver, con
/// Will como referencia"), que no caduca nunca: se resuelve con los actores donde estén en ese
/// momento.
///
/// Por eso un plano descrito así no hay que colocarlo, no hay que recolocarlo cuando algo se
/// mueve, y se puede escribir por texto en el asset sin abrir el Editor.
[System.Serializable]
public class ShotFraming
{
    [Tooltip("Qué tipo de plano se quiere. De él salen el FOV, la distancia y la altura por defecto.")]
    public ShotType type = ShotType.Medium;

    [Tooltip("El sujeto principal del plano: a quién vemos. 'Player' para Will, o el Persistence " +
             "ID del NPC (p. ej. 'NPC_Oliver').")]
    public string subjectId;

    [Tooltip("El segundo personaje. En TwoShot y Wide, el otro sujeto en cuadro. En " +
             "OverTheShoulder, el dueño del hombro que vemos en primer término. En CloseUp, " +
             "Reaction y Medium no sale en cuadro, pero sirve como referencia para el eje de " +
             "acción y para el ángulo de tres cuartos — conviene ponerlo siempre que haya alguien " +
             "con quien el sujeto esté hablando.")]
    public string secondaryId;

    [Header("Ajustes finos (opcionales)")]
    [Tooltip("Sube (+) o baja (-) la cámara, en metros, respecto a la altura que le toca al plano. " +
             "Un valor negativo mira al personaje desde abajo y lo hace parecer más imponente; uno " +
             "positivo lo mira desde arriba y lo empequeñece.")]
    public float heightBias = 0f;

    [Tooltip("Multiplica la distancia calculada. 1 = la que toca al tipo de plano. Menos de 1 " +
             "acerca (plano más cerrado), más de 1 aleja. Mantenerlo entre 0,7 y 1,5: fuera de ahí " +
             "el plano deja de parecerse a lo que su nombre dice.")]
    public float distanceScale = 1f;

    [Tooltip("Campo de visión en grados. 0 = el que corresponde al tipo de plano (los planos " +
             "cerrados usan menos grados, que comprime y separa al personaje del fondo). Tocarlo " +
             "solo con una intención concreta.")]
    public float fovOverride = 0f;

    [Tooltip("Pasa la cámara AL OTRO LADO del eje de acción. Es un salto de eje deliberado: rompe " +
             "la continuidad a propósito y el espectador lo nota, así que se usa para marcar un " +
             "cambio fuerte (una revelación, un giro de la escena), nunca por comodidad. Si un " +
             "plano se ve desde el lado que no toca, lo normal es que el problema esté en otro " +
             "sitio, no aquí.")]
    public bool crossTheLine = false;

    [Tooltip("Deja espacio por encima de la cabeza en vez de centrar al personaje en mitad de la " +
             "pantalla. Es la composición normal y conviene dejarlo puesto; quitarlo centra al " +
             "sujeto, que queda raro en planos cerrados.")]
    public bool headroom = true;

    [Tooltip("Girar al sujeto hacia el secundario ANTES de resolver el plano.\n\n" +
             "Esto no es un adorno: es lo que hace verdadera la suposición sobre la que está " +
             "construido todo el encuadre. Un plano de tres cuartos coloca la cámara HACIA EL " +
             "INTERLOCUTOR dando por hecho que el sujeto lo mira. Si no lo mira, sale de espaldas " +
             "o de perfil — que es lo que pasaba en toda la quinta grabación (INC-293).\n\n" +
             "Declarar un plano de A hablando con B ES declarar que A mira a B, así que por " +
             "defecto se encara. Se desmarca en los planos de ACCIÓN: alguien que huye, esquiva o " +
             "cae no debe girarse hacia quien le habla.\n\n" +
             "Solo hace efecto si el plano tiene secundario.")]
    public bool encara = true;

    /// Resumen de una línea para el Inspector y los avisos de consola.
    public string Describe()
    {
        string s = string.IsNullOrEmpty(subjectId) ? "SIN SUJETO" : subjectId;
        return type switch
        {
            ShotType.TwoShot => $"two-shot de {s} y {Other()}",
            ShotType.OverTheShoulder => $"sobre el hombro de {Other()}, a {s}",
            ShotType.Reaction => $"reacción de {s}",
            ShotType.Wide => $"plano general de {s}" + (HasSecondary ? $" y {Other()}" : ""),
            ShotType.CloseUp => $"primer plano de {s}",
            ShotType.Medium => $"plano medio de {s}",
            ShotType.Tracking => $"siguiendo a {s}",
            _ => $"{type} de {s}",
        };
    }

    public bool HasSecondary => !string.IsNullOrWhiteSpace(secondaryId);

    /// ¿Hay que girar al sujeto para este plano?
    ///
    /// Además de que el encuadre lo pida y tenga a quién mirar, el tipo de plano tiene que ser de
    /// los que DEPENDEN de la orientación. Son los de tres cuartos (Medium, CloseUp, Reaction),
    /// el escorzo y el de dos, que es donde `ComputeThreeQuarter` y compañía colocan la cámara
    /// hacia el interlocutor suponiendo que el sujeto lo mira.
    ///
    /// Quedan fuera:
    ///   · Wide — es un plano de situación. La cámara se coloca por la geometría del grupo, no
    ///     por hacia dónde mira nadie, y girar a alguien ahí solo puede estropear una caminata.
    ///   · Tracking — por definición sigue a alguien que se mueve, o sea acción.
    ///
    /// Importa porque 32 de los 74 encuadres vienen del archivo base y no dicen nada de `encara`:
    /// heredan el valor por defecto, y sin esta puerta un plano general giraría a la gente.
    public bool DebeEncarar => encara && HasSecondary
        && type != ShotType.Wide && type != ShotType.Tracking;

    private string Other() => string.IsNullOrEmpty(secondaryId) ? "NADIE" : secondaryId;
}

/// El resultado de resolver un plano: dónde va la cámara, hacia dónde mira y con qué campo de visión.
public struct ShotSolution
{
    public Vector3 position;
    public Quaternion rotation;
    public float fieldOfView;

    /// Punto del mundo al que apunta el plano (los ojos del sujeto). Se guarda para poder
    /// recalcular la rotación después de mover la cámara en el pase de seguridad.
    public Vector3 lookAt;

    /// Altura aproximada, en metros, de lo que entra en el encuadre a la distancia del sujeto.
    /// Sirve para comparar dos planos consecutivos y avisar de un corte que no cambia nada.
    public float frameHeight;
}

/// Calcula planos de cámara a partir de una descripción (ShotFraming) y de dónde están los actores
/// AHORA MISMO.
///
/// ── Por qué existe ────────────────────────────────────────────────────────────────────────────
/// Un plano colocado a mano guarda una posición del mundo, que es información sobre el mapa y
/// caduca: el jugador llega por otro sitio, un NPC deriva medio metro, el terreno se remodela, y
/// el plano ya no encuadra lo que encuadraba. StarAwakeningSequencer ya se topó con esto y lo
/// resolvió a su manera (AlignSequenceRigToWill: mover el molde entero de planos hasta la posición
/// real de Will). Aquello iba en la dirección correcta, pero se queda corto en tres cosas: ancla a
/// un solo actor (si el otro se mueve, el two-shot deja de encuadrarlo), es código suelto de esa
/// escena que ninguna otra hereda, y no comprueba que la cámara no acabe dentro de una pared.
///
/// Aquí el plano no se guarda como un sitio, sino como una intención ("primer plano de Oliver, con
/// Will como referencia"), y se resuelve cada vez con los actores donde estén. Por eso no hay nada
/// que colocar ni que recolocar.
///
/// ── Lo que lo hace parecer rodado y no calculado ──────────────────────────────────────────────
/// Un solver ingenuo da planos correctos y feos. Las tres reglas que lo arreglan, todas aquí:
///
///  1. EL EJE DE ACCIÓN (regla de los 180º). Entre dos personajes hay una línea imaginaria. Todos
///     los planos de una escena tienen que estar del MISMO lado de esa línea; si uno se cruza, el
///     espectador siente que los personajes se han intercambiado el sitio. El lado se elige una
///     vez (ver SequenceContext.ResolveActionSide) y lo heredan todos los planos siguientes.
///
///  2. AIRE SOBRE LA CABEZA Y REGLA DE TERCIOS. Apuntar al centro de la cabeza deja al personaje
///     clavado en mitad de la pantalla, que es la composición de una foto de carnet. Aquí la
///     cámara apunta un poco por debajo, de forma que los ojos caen en el tercio superior.
///
///  3. NO REPETIR PLANO. Dos cortes seguidos con el mismo ángulo y el mismo tamaño se leen como un
///     salto de montaje, no como un corte. El solver lo detecta y lo avisa por consola al montar,
///     en vez de dejar que se descubra viendo la escena.
///
/// Y encima de todo, un pase de seguridad: la cámara no acaba dentro de una pared ni por debajo
/// del suelo, pase lo que pase.
///
/// ── Sobre Cinemachine ─────────────────────────────────────────────────────────────────────────
/// Parte de esto lo daría Cinemachine hecho (CinemachineTargetGroup para encuadrar a varios,
/// CinemachineDeoccluder para las paredes). Se ha escrito a mano a propósito: de ese paquete solo
/// harían falta tres cosas, y meterlo obligaría a que el handoff con vThirdPersonCamera — que ya
/// está calibrado y costó lo suyo, ver CameraDirectorService — pasara por su Brain. Esto son
/// doscientas líneas de trigonometría que no dependen de nadie.
public static class ShotComposer
{
    // ── Valores por tipo de plano ─────────────────────────────────────────────────────────────
    //
    // El campo de visión no es un capricho: los planos cerrados usan menos grados (teleobjetivo)
    // porque comprime la profundidad y despega al personaje del fondo, que es lo que hace que un
    // primer plano parezca de cine y no de cámara de seguridad. Los generales usan más grados para
    // que quepa el sitio.

    private static float DefaultFov(ShotType type) => type switch
    {
        ShotType.CloseUp => 40f,
        ShotType.Reaction => 40f,
        ShotType.Medium => 48f,
        ShotType.OverTheShoulder => 48f,
        ShotType.TwoShot => 52f,
        ShotType.Tracking => 55f,
        ShotType.Wide => 62f,
        _ => 50f,
    };

    /// Altura, en metros, de lo que debe entrar en el cuadro a la distancia del sujeto. De aquí
    /// sale la distancia de la cámara.
    ///
    /// CALIBRADO PARA EL ARTE DE ESTE JUEGO, y por eso no son los valores de manual. Los
    /// personajes son de proporciones de dibujo: Will mide unos 1,15 m y **su cabeza sola mide
    /// casi 0,60 m**, la mitad de su altura. Con los valores pensados para proporciones humanas
    /// reales (cabeza ≈ un séptimo del cuerpo), un "primer plano" de 60 cm encuadraba exactamente
    /// la cabeza y nada más, sin un dedo de aire, y dejaba la cámara a un metro de la cara: es el
    /// cabezón de Eldran que abre el vídeo del 16 sep.
    ///
    /// Si algún día cambian las proporciones de los personajes, esta tabla es lo que hay que
    /// tocar — y el síntoma de que se ha quedado corta es justo ese, caras cortadas por los bordes.
    private static float DefaultFrameHeight(ShotType type) => type switch
    {
        ShotType.CloseUp => 1.35f,          // cabeza, torso y algo de sitio alrededor
        ShotType.Reaction => 1.35f,
        ShotType.Medium => 2.10f,           // el cuerpo entero con aire
        ShotType.OverTheShoulder => 2.30f,
        ShotType.Tracking => 4.20f,
        _ => 3.50f,
    };

    /// Cuánto se aparta la cámara de la línea que une a los dos personajes, en un plano de tres
    /// cuartos. A 0 grados la cámara estaría justo detrás del interlocutor (frontal puro, plano de
    /// videollamada); a 90 sería un perfil completo. 35 es el tres cuartos de toda la vida.
    private const float ThreeQuarterAngle = 35f;

    /// Escorzo del two-shot: lo que se gira la cámara respecto a la perpendicular exacta del eje.
    /// Con 0 los dos personajes salen igual de lejos y el plano queda plano como un friso; con
    /// esto uno queda algo más cerca que el otro y aparece profundidad.
    private const float TwoShotSkew = 18f;

    /// Distancia mínima de la cámara al sujeto. Por debajo de esto se le ve dentro de la cara.
    /// Se suma a esto el radio del propio sujeto (ver SequenceActor.SafeRadius): encuadrar una
    /// esfera de tres metros no es lo mismo que encuadrar a una persona.
    private const float MinSubjectDistance = 1.10f;

    /// A partir de esta separación entre dos sujetos, un two-shot deja de tener sentido: uno queda
    /// pegado a la cámara y el otro convertido en un punto. Ver ComputeTwoShot.
    private const float MaxTwoShotSpan = 7f;

    /// Altura mínima de la cámara sobre el suelo que tenga debajo.
    private const float MinGroundClearance = 0.35f;

    /// Cuanto se sube el encuadre en los planos cercanos, como fraccion de lo que entra en cuadro,
    /// para dejar libre la parte de arriba. Ver el pase de composicion en Compute.
    private const float SitioParaElBocadillo = 0.30f;

    /// Margen que se deja al retirar la cámara de una pared, para que el plano de recorte cercano
    /// no se coma el muro y se vea a través de él.
    private const float WallPadding = 0.28f;

    // Buffer pre-alocado: el pase de seguridad se ejecuta cada frame en los planos 'live', y
    // CLAUDE.md § 2 prohíbe reservar memoria en bucles por frame.
    private static readonly RaycastHit[] s_hits = new RaycastHit[16];

    /// El contexto de la secuencia que se esta rodando ahora mismo. Se guarda al entrar en
    /// TrySolve porque la comprobacion de "la camara se ha quedado dentro de alguien" necesita la
    /// lista de actores, y pasarla por los cinco metodos de la cadena solo para eso ensuciaba
    /// mas que este campo.
    private static SequenceContext s_ctx;

    // ── API ───────────────────────────────────────────────────────────────────────────────────

    /// Resuelve un plano. Devuelve false (con aviso ya logueado) si falta el sujeto o no se puede
    /// resolver; en ese caso el beat que lo pidió debe dejar la cámara donde estaba en vez de
    /// mandarla a un sitio inventado.
    /// 'isCut' distingue un corte de verdad de un refresco. Un plano que sigue a alguien se
    /// resuelve CADA FRAME, y comparar ese resultado con el del frame anterior siempre da cero
    /// grados de diferencia: sin esto, un solo plano en movimiento llenaba la consola de avisos de
    /// "corte repetido" contra sí mismo (11 seguidos en la prueba del 16 sep).
    public static bool TrySolve(SequenceContext ctx, ShotFraming framing, float aspect,
        out ShotSolution solution, bool isCut = true)
    {
        solution = default;

        if (ctx == null || framing == null) return false;

        s_ctx = ctx;

        // Cuánto se alejan todos los planos de esta secuencia. Es un ajuste de gusto y vive en la
        // escena, donde se puede mover en pleno Play. Ver SequenceStage.DistanceMultiplier.
        float globalScale = ctx.Stage != null ? ctx.Stage.DistanceMultiplier : 1f;

        // Un corte empieza de cero: el ángulo por el que entraba el plano anterior no significa
        // nada para el siguiente. En un refresco de plano vivo, en cambio, se conserva.
        if (isCut) OlvidarOrbita();

        var subject = ctx.GetActor(framing.subjectId);
        if (subject?.Transform == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[ShotComposer] No se puede montar el plano '{framing.Describe()}': " +
                $"no hay ningún actor '{framing.subjectId}' en esta escena. La cámara se queda donde está.");
#endif
            return false;
        }

        var secondary = framing.HasSecondary ? ctx.GetActor(framing.secondaryId) : null;

        // El eje de acción se decide una sola vez por escena y lo heredan todos los planos. Ver
        // SequenceContext.ResolveActionSide.
        Vector3 side = ctx.ResolveActionSide(subject, secondary);
        if (framing.crossTheLine) side = -side;

        // Se intenta el lado que toca; si la cámara acaba pegada a una pared o metida dentro de
        // algo, se prueba el espejo y se elige el que respire mejor. Cruzar el eje es peor que un
        // plano un poco más cerrado, así que el espejo solo gana si la diferencia es grande.
        ShotSolution best = Compute(subject, secondary, framing, side, aspect, globalScale);
        float bestClearance = Clearance(best);

        if (bestClearance < MinSubjectDistance * 1.6f)
        {
            ShotSolution mirrored = Compute(subject, secondary, framing, -side, aspect, globalScale);
            if (Clearance(mirrored) > bestClearance * 1.5f)
            {
                best = mirrored;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ShotComposer] El plano '{framing.Describe()}' se ha cruzado al otro lado " +
                    "del eje: por el lado que tocaba la cámara quedaba contra algo. Si esto pasa en " +
                    "varios planos de la misma escena, lo que hay que mover es a los personajes, no la cámara.");
#endif
            }
        }

        if (isCut)
        {
            WarnIfSameAsPrevious(ctx, framing, best);
            ctx.RememberShot(best);
        }

        solution = best;
        return true;
    }

    // ── Geometría ─────────────────────────────────────────────────────────────────────────────

    private static ShotSolution Compute(SequenceActor subject, SequenceActor secondary,
        ShotFraming f, Vector3 side, float aspect, float globalScale)
    {
        float fov = f.fovOverride > 0f ? f.fovOverride : DefaultFov(f.type);
        float frameHeight = DefaultFrameHeight(f.type);
        float scale = (f.distanceScale > 0.01f ? f.distanceScale : 1f) * globalScale;

        Vector3 eye = subject.EyePosition;
        Vector3 position;
        Vector3 lookAt = eye;

        switch (f.type)
        {
            case ShotType.TwoShot:
                position = ComputeTwoShot(subject, secondary, side, fov, aspect, scale,
                    out lookAt, out frameHeight);
                break;

            case ShotType.OverTheShoulder:
                position = ComputeOverTheShoulder(subject, secondary, side, scale, out lookAt);
                // En un plano sobre el hombro la posición la manda el hombro, no la distancia: lo
                // que se ajusta para que el sujeto quede del tamaño que toca es el campo de visión.
                // Así el plano encuadra bien tanto si los personajes están a un metro como a cinco.
                if (f.fovOverride <= 0f)
                    fov = FovToFrame(frameHeight, Vector3.Distance(position, lookAt));
                break;

            case ShotType.Wide:
                position = ComputeWide(subject, secondary, side, fov, aspect, scale,
                    out lookAt, out frameHeight);
                break;

            default: // Medium, CloseUp, Reaction, Tracking — todos son el mismo plano de tres cuartos
                position = ComputeThreeQuarter(subject, secondary, side, fov, scale, frameHeight,
                    out lookAt);
                break;
        }

        position += Vector3.up * f.heightBias;

        // ── Sitio para el bocadillo ───────────────────────────────────────────────────────────
        //
        // En un plano cercano la cabeza ocupa media pantalla, asi que el bocadillo -- que se ancla
        // justo encima de ella -- acaba tapandole la cara. Se ve en toda la grabacion del 19 sep.
        //
        // La solucion es la que pidio Raul con estas palabras: subir la camara. Se suben A LA VEZ la
        // camara y el punto al que mira, lo mismo, asi que el angulo no cambia -- el encuadre
        // simplemente se desliza hacia arriba y el personaje baja en el cuadro, dejando libre el
        // tercio superior. Ahi es donde cae el bocadillo.
        //
        // No se aplica a los planos generales: ahi el personaje ya es pequeno y el bocadillo tiene
        // sitio de sobra.
        if (f.headroom && f.type != ShotType.Wide)
        {
            float sitioParaElBocadillo = frameHeight * SitioParaElBocadillo;
            position += Vector3.up * sitioParaElBocadillo;
            lookAt += Vector3.up * sitioParaElBocadillo;
        }

        // ── Pase de seguridad ────────────────────────────────────────────────
        // Primero se coloca la cámara y solo al final se decide hacia dónde mira: así cualquier
        // corrección de posición (pared, suelo) sigue apuntando al sujeto en vez de quedarse
        // mirando al vacío, que es el fallo clásico de hacerlo al revés.

        // Lo primero, no meterse dentro de lo que se quiere mirar. El tamaño del sujeto cuenta:
        // una persona ocupa poco, una bola de fuego de tres metros no.
        float minDistance = MinSubjectDistance + SubjectRadius(subject, secondary, f.type);
        position = EnforceMinDistance(lookAt, position, minDistance);

        // El buscador necesita saber qué entra en el cuadro; ver RayosDelCuadroTapados.
        s_fovDelPlano = Mathf.Clamp(fov, 18f, 75f);
        s_aspecto = aspect > 0.1f ? aspect : 16f / 9f;
        s_secundarioNoSale = f.type == ShotType.CloseUp || f.type == ShotType.Reaction
            || f.type == ShotType.Medium;

        position = FindClearPosition(lookAt, position, minDistance, f, subject, secondary);

        Quaternion rotation = Quaternion.LookRotation((lookAt - position).normalized, Vector3.up);
        rotation = ApplyComposition(rotation, position, lookAt, secondary, f, fov, aspect);

        return new ShotSolution
        {
            position = position,
            rotation = rotation,
            fieldOfView = Mathf.Clamp(fov, 18f, 75f),
            lookAt = lookAt,
            frameHeight = frameHeight,
        };
    }

    /// Plano de tres cuartos de un solo personaje (primer plano, plano medio, reacción, tracking).
    ///
    /// La cámara NO se pone de frente: un frontal puro aplana la cara y parece una videollamada.
    /// Se coloca a 35 grados de la línea que une al sujeto con su interlocutor, del lado del eje —
    /// que es, exactamente, el plano/contraplano de toda la vida. Sin interlocutor se usa hacia
    /// dónde mira el propio sujeto.
    private static Vector3 ComputeThreeQuarter(SequenceActor subject, SequenceActor secondary,
        Vector3 side, float fov, float scale, float frameHeight, out Vector3 lookAt)
    {
        Vector3 eye = subject.EyePosition;
        lookAt = eye;

        // Dirección desde el sujeto hacia donde estaría la cámara si fuera un frontal puro.
        Vector3 frontal = secondary?.Transform != null
            ? Flatten(secondary.EyePosition - eye)
            : Flatten(subject.Transform.forward);

        if (frontal.sqrMagnitude < 0.0001f) frontal = Flatten(subject.Transform.forward);
        if (frontal.sqrMagnitude < 0.0001f) frontal = Vector3.forward;
        frontal.Normalize();

        // Girar esos 35 grados hacia el lado del eje de acción.
        float sign = Vector3.Dot(Vector3.Cross(Vector3.up, frontal), side) >= 0f ? 1f : -1f;
        Vector3 dir = Quaternion.AngleAxis(ThreeQuarterAngle * sign, Vector3.up) * frontal;

        float distance = DistanceToFrame(frameHeight, fov) * scale;
        distance = Mathf.Max(distance, MinSubjectDistance);

        return eye + dir * distance;
    }

    /// Two-shot: los dos personajes en cuadro, la cámara en perpendicular al eje que los une, con
    /// un poco de escorzo para que no quede un friso.
    private static Vector3 ComputeTwoShot(SequenceActor a, SequenceActor b, Vector3 side,
        float fov, float aspect, float scale, out Vector3 lookAt, out float frameHeight)
    {
        if (b?.Transform == null)
        {
            // Sin segundo personaje un two-shot no existe; se cae a plano medio en vez de montar
            // un plano roto.
            frameHeight = DefaultFrameHeight(ShotType.Medium);
            lookAt = a.EyePosition;
            return ComputeThreeQuarter(a, null, side, fov, scale, frameHeight, out lookAt);
        }

        Vector3 eyeA = a.EyePosition;
        Vector3 eyeB = b.EyePosition;

        // Un two-shot da por hecho que los dos sujetos están a una distancia parecida de la
        // cámara. Cuando no lo están — el proyectil del Despertar entra a dieciocho metros de
        // Will — el plano se coloca a media distancia entre ambos y el resultado es el peor de los
        // dos mundos: el cercano tapa media pantalla y el lejano se queda del tamaño de un sello.
        // En cine eso no se resuelve con un encuadre, se resuelve con el montaje: un plano de uno,
        // un plano del otro, y el espectador los une. Así que aquí se cae a un plano del sujeto
        // principal y se dice por consola.
        float separation = Vector3.Distance(Flatten(eyeA), Flatten(eyeB));
        if (separation > MaxTwoShotSpan)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[ShotComposer] Two-shot de '{a.Id}' y '{b.Id}' descartado: están a " +
                $"{separation:F0} m, demasiado lejos para caber en un encuadre sin que uno de los dos " +
                "quede diminuto. Se usa un plano medio del primero. Si querías ver a los dos, la " +
                "solución es cortar entre ellos, no abrir más el plano.");
            // DIAG (18 sep 2026, a petición de Raúl -- de dónde sale el "8484 m" si el spawn ya
            // coloca a Archimago bien): imprimir las posiciones reales que está usando este cálculo
            // en el momento exacto del fallo, y con qué GameObject exacto (InstanceID) y escena, para
            // detectar un duplicado o un Transform equivocado.
            Debug.LogWarning($"[ShotComposer:DIAG] '{a.Id}' Transform={(a.Transform != null ? a.Transform.GetEntityId().ToString() : "NULL")} " +
                $"pos={(a.Transform != null ? a.Transform.position.ToString() : "-")} eye={eyeA} " +
                $"activeInHierarchy={(a.Transform != null ? a.Transform.gameObject.activeInHierarchy.ToString() : "-")} " +
                $"escena={(a.Transform != null ? a.Transform.gameObject.scene.name : "-")} | " +
                $"'{b.Id}' Transform={(b.Transform != null ? b.Transform.GetEntityId().ToString() : "NULL")} " +
                $"pos={(b.Transform != null ? b.Transform.position.ToString() : "-")} eye={eyeB} " +
                $"activeInHierarchy={(b.Transform != null ? b.Transform.gameObject.activeInHierarchy.ToString() : "-")} " +
                $"escena={(b.Transform != null ? b.Transform.gameObject.scene.name : "-")}");
#endif
            frameHeight = DefaultFrameHeight(ShotType.Medium);
            return ComputeThreeQuarter(a, b, side, fov, scale, frameHeight, out lookAt);
        }

        Vector3 mid = (eyeA + eyeB) * 0.5f;
        lookAt = mid;

        // Lo que tiene que caber a lo ancho: la separación entre los dos, más un hombro y medio a
        // cada lado para que no salgan cortados por el borde.
        float width = separation + 1.6f;
        frameHeight = Mathf.Max(2.0f, width / Mathf.Max(aspect, 0.1f));

        float distance = Mathf.Max(
            DistanceToFrameWidth(width, fov, aspect),
            DistanceToFrame(frameHeight, fov)) * scale;

        Vector3 dir = Quaternion.AngleAxis(TwoShotSkew, Vector3.up) * side;
        return mid + dir * Mathf.Max(distance, MinSubjectDistance);
    }

    /// Por encima del hombro: la cámara detrás y al lado del hombro del interlocutor, mirando al
    /// sujeto. El hombro en primer término es lo que ata a los dos personajes en el espacio y lo
    /// que hace que un plano/contraplano se lea como una conversación y no como dos monólogos.
    private static Vector3 ComputeOverTheShoulder(SequenceActor subject, SequenceActor shoulder,
        Vector3 side, float scale, out Vector3 lookAt)
    {
        lookAt = subject.EyePosition;

        if (shoulder?.Transform == null)
        {
            // Sin hombro esto es un plano medio.
            return ComputeThreeQuarter(subject, null, side,
                DefaultFov(ShotType.Medium), scale, DefaultFrameHeight(ShotType.Medium), out lookAt);
        }

        Vector3 shoulderEye = shoulder.EyePosition;
        Vector3 axis = Flatten(subject.EyePosition - shoulderEye);
        if (axis.sqrMagnitude < 0.0001f) axis = Flatten(shoulder.Transform.forward);
        axis.Normalize();

        // Detrás del hombro, a un lado, y un poco por encima de la línea de los ojos: es lo que
        // deja la nuca y el hombro entrando por la esquina del cuadro.
        //
        // CALIBRADO PARA EL ARTE DE ESTE JUEGO (19 sep 2026, grabación del prólogo). Los 0,78 m
        // de retranqueo que había aquí son de proporciones humanas, igual que lo que ya le pasó a
        // DefaultFrameHeight: con una cabeza de casi 60 cm, dejaban la cámara a medio palmo del
        // cráneo del que presta el hombro, y el plano entero era esa cabeza. Se ve en el plano
        // del horno (segundo 0:15 del vídeo): cinco segundos de oreja a pantalla completa.
        //
        // Ahora el retranqueo sale del tamaño real del personaje, no de una constante, así que
        // vale igual para Will, para el Archimago y para lo que venga después.
        float vueloCabeza = Mathf.Max(shoulder.EyeHeight, 0.8f) * 0.52f;   // lo que ocupa la cabeza
        float radioHombro = Mathf.Max(shoulder.SafeRadius, 0.3f);

        return shoulderEye
             - axis * ((vueloCabeza + 0.95f) * scale)
             + side * ((radioHombro + 0.45f) * scale)
             + Vector3.up * (vueloCabeza * 0.35f);
    }

    /// Plano general: todo el mundo en cuadro con aire de sobra, para situar dónde pasa la escena.
    private static Vector3 ComputeWide(SequenceActor a, SequenceActor b, Vector3 side,
        float fov, float aspect, float scale, out Vector3 lookAt, out float frameHeight)
    {
        Vector3 centre = a.ChestPosition;
        float radius = 1.2f;

        if (b?.Transform != null)
        {
            centre = (a.ChestPosition + b.ChestPosition) * 0.5f;
            radius = Vector3.Distance(Flatten(a.ChestPosition), Flatten(b.ChestPosition)) * 0.5f + 1.5f;
        }

        lookAt = centre;
        frameHeight = radius * 2f;

        float halfFovV = fov * 0.5f * Mathf.Deg2Rad;
        float halfFovH = Mathf.Atan(Mathf.Tan(halfFovV) * Mathf.Max(aspect, 0.1f));
        float distance = radius / Mathf.Sin(Mathf.Min(halfFovV, halfFovH)) * scale;

        // Un poco elevada y escorada: un general a la altura de los ojos se parece demasiado a la
        // cámara de juego y no se lee como un plano distinto.
        Vector3 dir = Quaternion.AngleAxis(25f, Vector3.up) * side;
        return centre + dir * distance + Vector3.up * (radius * 0.35f);
    }

    // ── Composición ───────────────────────────────────────────────────────────────────────────

    /// Aire sobre la cabeza y regla de tercios, aplicados como una pequeña corrección de ángulo
    /// sobre la rotación que ya mira al sujeto.
    ///
    /// Apuntar exacto a los ojos los deja en mitad de la pantalla, con media pantalla de vacío por
    /// encima: es la composición de una foto de carnet. Bajando la puntería un sexto de la altura
    /// del encuadre, los ojos suben al tercio superior, que es donde el ojo humano los espera.
    private static Quaternion ApplyComposition(Quaternion rotation, Vector3 position, Vector3 lookAt,
        SequenceActor secondary, ShotFraming f, float fov, float aspect)
    {
        float pitch = 0f;
        float yaw = 0f;

        // El aire sobre la cabeza ya NO se hace aqui. Esta correccion de angulo subia al sujeto en
        // pantalla, que es exactamente lo contrario de lo que hace falta cuando encima va a
        // aparecer un bocadillo. Ahora el encuadre entero se desliza hacia arriba en Compute (ver
        // 'Sitio para el bocadillo'), que ademas no deforma el angulo del plano.
        pitch = 0f;

        // En un plano sobre el hombro, el sujeto no va centrado: va hacia el lado contrario a
        // aquel por el que entra el hombro. Se calcula de dónde cae el hombro en pantalla y se
        // gira la cámara hacia él, lo que empuja al sujeto al otro tercio.
        if (f.type == ShotType.OverTheShoulder && secondary?.Transform != null)
        {
            Vector3 right = rotation * Vector3.right;
            float shoulderSide = Vector3.Dot(secondary.EyePosition - position, right);
            float halfFovV = fov * 0.5f * Mathf.Deg2Rad;
            float halfFovH = Mathf.Atan(Mathf.Tan(halfFovV) * Mathf.Max(aspect, 0.1f));
            yaw = Mathf.Sign(shoulderSide) * Mathf.Atan(Mathf.Tan(halfFovH) / 3f) * Mathf.Rad2Deg;
        }

        return rotation * Quaternion.Euler(pitch, yaw, 0f);
    }

    // ── Pase de seguridad ─────────────────────────────────────────────────────────────────────

    /// El radio que hay que respetar alrededor de lo que se mira. En un two-shot manda el mayor de
    /// los dos, porque la cámara los tiene a los dos delante.
    private static float SubjectRadius(SequenceActor subject, SequenceActor secondary, ShotType type)
    {
        float r = subject != null ? subject.SafeRadius : 0.4f;

        if (type == ShotType.TwoShot && secondary != null)
            r = Mathf.Max(r, secondary.SafeRadius);

        // En un plano sobre el hombro la cámara está pegada al hombro a propósito: ese es el plano.
        if (type == ShotType.OverTheShoulder) r *= 0.5f;

        return r;
    }

    /// Aleja la cámara hasta la distancia mínima, si ha quedado más cerca. Conserva la dirección,
    /// así que el encuadre es el mismo, solo que desde más lejos.
    private static Vector3 EnforceMinDistance(Vector3 target, Vector3 position, float minDistance)
    {
        Vector3 delta = position - target;
        float distance = delta.magnitude;

        if (distance >= minDistance) return position;

        // Si la cámara ha quedado justo en el centro del objetivo no hay dirección que conservar;
        // se retira hacia atrás en horizontal, que es mejor que quedarse dentro.
        Vector3 dir = distance > 0.01f ? delta / distance : Vector3.back;
        return target + dir * minDistance;
    }

    /// Ángulos, en grados, que se prueban alrededor del sujeto cuando la posición ideal está
    /// tapada. Van de menor a mayor y alternando lado: el plano que menos se aparta del que pedía
    /// el asset es el que menos se nota en el montaje.
    private static readonly float[] s_anglesOrbita =
        { 0f, 14f, -14f, 28f, -28f, 45f, -45f, 65f, -65f, 90f, -90f, 115f, -115f, 145f, -145f, 180f };

    /// Alturas extra, en metros, que se prueban cuando ningún ángulo queda libre a la altura
    /// pedida. Asomarse por encima de una carreta, de un murete o de una viga suele bastar.
    private static readonly float[] s_alturasOrbita = { 0f, 0.8f, 1.7f };

    /// Hasta cuántos grados puede rodar la cámara alrededor del sujeto BUSCANDO SITIO, según el
    /// tipo de plano.
    ///
    /// Esto es lo que faltaba el 20 sep. La búsqueda de hueco probaba ángulos hasta 180° y se
    /// quedaba con el primero despejado — y 180° es, literalmente, la nuca. En la octava grabación
    /// eso es la conversación entera de la orilla rodada por detrás de la cabeza del Archimago: no
    /// había ningún ángulo libre por delante (una farola, un vecino, el faldón de una casa), así
    /// que el solver siguió girando hasta que encontró aire, y el aire estaba detrás.
    ///
    /// Un plano cerrado tiene un arco válido y se acaba: pasados unos 70° del frontal ya no se le
    /// ve la cara a nadie, y un primer plano que no enseña una cara no es un primer plano. Ahí
    /// **es mejor un plano más abierto que uno correcto de espaldas**, y para eso ya está el pase
    /// de rescate, que se prueba justo después.
    ///
    /// Los planos generales no llevan tope: un general desde otro ángulo sigue contando el sitio,
    /// que es para lo que existe.
    private static float ArcoDeBusqueda(ShotType tipo) => tipo switch
    {
        ShotType.CloseUp => 70f,
        ShotType.Reaction => 70f,
        ShotType.Medium => 80f,
        ShotType.OverTheShoulder => 60f,   // el más estrecho: el hombro tiene que seguir en cuadro
        ShotType.TwoShot => 90f,
        _ => 180f,                          // Wide y Tracking
    };

    /// Cuánto se ABRE el plano cuando no queda ni un ángulo libre a la distancia pedida. Se prueba
    /// antes de acercar la cámara contra la pared: un plano general de más cuenta la escena, un
    /// plano de una pared no cuenta nada.
    private static readonly float[] s_radiosDeRescate = { 1.4f, 1.9f, 2.6f };

    // ── Memoria de órbita ─────────────────────────────────────────────────────────────────────
    //
    // Un plano VIVO (ShotBeat.live, los que siguen a alguien que anda) vuelve a resolverse entero
    // en cada fotograma. Y como la búsqueda de sitio despejado empieza siempre por 0° y se queda
    // con el primer ángulo libre, basta con que un árbol entre y salga de la línea de visión para
    // que el ángulo elegido salte de 0° a 14° y vuelva — sesenta veces por segundo, y cada salto
    // es un teletransporte de la cámara.
    //
    // En el log de la quinta grabación eso sale como decenas de «se ha rodado 14° a un lado»
    // seguidas durante la bajada del Mago Oscuro. En pantalla se ve como una cámara que da
    // tirones, y de paso hace que un personaje que viene de frente parezca andar de lado.
    //
    // Así que el ángulo se recuerda: mientras siga despejado, se conserva aunque el de partida
    // también lo esté. Solo se busca otro cuando el recordado lleva varios fotogramas tapado.
    private static float s_anguloRecordado;
    private static float s_alturaRecordada;
    // Multiplicador de radio: 1 en el caso normal, y el de s_radiosDeRescate cuando el plano ha
    // tenido que abrirse. Sin esto, un plano vivo que acaba en el rescate volvería a buscar desde
    // cero en cada fotograma -- justo lo que la memoria viene a evitar.
    private static float s_radioRecordado = 1f;
    private static bool s_hayOrbitaRecordada;
    private static int s_fotogramasTapado;

    /// Cuántos fotogramas seguidos tiene que estar tapado el ángulo recordado antes de buscar otro.
    /// Cinco son menos de una décima de segundo: no se nota la espera, y se come todos los
    /// parpadeos de una farola o una rama cruzando el plano.
    private const int AguantarTapadoFotogramas = 5;

    /// Olvida el ángulo recordado. La llama TrySolve en cada CORTE: un plano nuevo no tiene por qué
    /// heredar por dónde entraba el anterior.
    private static void OlvidarOrbita()
    {
        s_hayOrbitaRecordada = false;
        s_radioRecordado = 1f;
        s_fotogramasTapado = 0;
    }

    /// Busca una posición desde la que se VEA al sujeto, conservando la distancia del plano.
    ///
    /// ── Por qué no vale con acercar la cámara ─────────────────────────────────────────────────
    /// Lo que había antes (PullOutOfWalls) es un deoccluder clásico: si hay una pared en medio,
    /// trae la cámara a este lado de la pared por la misma recta. Eso resuelve el caso fácil y
    /// falla en los dos que de verdad se ven, los dos en la grabación del prólogo del 19 sep:
    ///
    ///   · Si el obstáculo está MÁS CERCA que la distancia mínima, la cámara se queda clavada en
    ///     la distancia mínima — es decir, dentro de la pared. Son los cinco segundos de pantalla
    ///     gris del plano de la carta (0:37).
    ///   · Si el obstáculo está a medio camino, un plano general o medio se convierte en un primer
    ///     plano de la cara que nadie pidió: el encuadre que dice el asset deja de existir.
    ///
    /// Lo que hace un operador de cámara real no es acercarse: es moverse de sitio. Eso es esto —
    /// se gira alrededor del sujeto manteniendo la distancia hasta encontrar un ángulo despejado,
    /// y solo si no hay ninguno se recurre a acercarse.
    ///
    /// El eje de acción se respeta por construcción: los ángulos se prueban de menor a mayor, así
    /// que un desvío de 14 grados siempre gana a uno de 90, y solo se cruza al otro lado cuando no
    /// queda literalmente nada más.
    private static Vector3 FindClearPosition(Vector3 target, Vector3 desired, float minDistance,
        ShotFraming framing, SequenceActor sujeto, SequenceActor secundario)
    {
        Vector3 delta = desired - target;
        float distance = delta.magnitude;
        if (distance < 0.01f) return LiftOffGround(desired);

        Vector3 plana = Flatten(delta);
        if (plana.sqrMagnitude < 0.0001f) plana = Vector3.forward;
        float radio = plana.magnitude;
        plana /= radio;
        float altura = delta.y;

        // Primero, el ángulo que ya se estaba usando: si sigue despejado, no se cambia. Ver la
        // memoria de órbita de arriba.
        float arco = ArcoDeBusqueda(framing.type);

        if (s_hayOrbitaRecordada && Mathf.Abs(s_anguloRecordado) <= arco)
        {
            Vector3 dirRecordada = Quaternion.AngleAxis(s_anguloRecordado, Vector3.up) * plana;
            Vector3 recordada = LiftOffGround(
                target + dirRecordada * (radio * s_radioRecordado) + Vector3.up * (altura + s_alturaRecordada));

            if (Penalizacion(target, recordada, sujeto, secundario) == 0)
            {
                s_fotogramasTapado = 0;
                return recordada;
            }

            // Tapado, pero puede ser una rama pasando. Se aguanta unos fotogramas antes de mover
            // la cámara: un plano tapado un instante se perdona, un salto de cámara no.
            if (++s_fotogramasTapado < AguantarTapadoFotogramas) return recordada;
        }

        // La menos mala encontrada dentro del arco, por si no hay ninguna impecable. Vale más un
        // plano con una esquina tapada y la cara del personaje que un plano limpio de su nuca.
        Vector3 mejor = Vector3.zero;
        int mejorPuntos = int.MaxValue;
        float mejorAngulo = 0f, mejorAltura = 0f, mejorRadio = 1f;

        for (int l = 0; l < s_alturasOrbita.Length; l++)
        {
            for (int a = 0; a < s_anglesOrbita.Length; a++)
            {
                if (Mathf.Abs(s_anglesOrbita[a]) > arco) continue;

                Vector3 dir = Quaternion.AngleAxis(s_anglesOrbita[a], Vector3.up) * plana;
                Vector3 candidata = LiftOffGround(
                    target + dir * radio + Vector3.up * (altura + s_alturasOrbita[l]));

                int puntos = Penalizacion(target, candidata, sujeto, secundario);
                if (puntos > 0)
                {
                    // Se penaliza además apartarse del ángulo que pedía el encuadre, para que
                    // entre dos igual de tapadas gane la que respeta el plano.
                    int coste = puntos + Mathf.RoundToInt(Mathf.Abs(s_anglesOrbita[a]) * 0.1f);
                    if (coste < mejorPuntos)
                    {
                        mejorPuntos = coste;
                        mejor = candidata;
                        mejorAngulo = s_anglesOrbita[a];
                        mejorAltura = s_alturasOrbita[l];
                        mejorRadio = 1f;
                    }
                    continue;
                }

                s_anguloRecordado = s_anglesOrbita[a];
                s_alturaRecordada = s_alturasOrbita[l];
                s_radioRecordado = 1f;
                s_hayOrbitaRecordada = true;
                s_fotogramasTapado = 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (l > 0 || a > 0)
                    Debug.Log($"[ShotComposer] El plano '{framing.Describe()}' estaba tapado desde " +
                        $"donde tocaba; se ha rodado {s_anglesOrbita[a]:F0}° a un lado" +
                        (s_alturasOrbita[l] > 0f ? $" y {s_alturasOrbita[l]:F1} m más alto" : "") +
                        ". Si pasa en varios planos de la misma escena, lo que hay que mover es a " +
                        "los personajes o el decorado, no la cámara.");
#endif
                return candidata;
            }
        }

        // Ningún ángulo libre a esta distancia. ANTES de resignarse a acercarse, se prueba a
        // ALEJARSE y subir: casi siempre el problema es que la cámara está metida entre el mobiliario
        // de la plaza, y desde tres metros más atrás y dos más arriba se ve todo. Un plano más
        // abierto de lo pedido sigue contando la escena; uno pegado a una pared, no.
        foreach (float mas in s_radiosDeRescate)
        {
            for (int a = 0; a < s_anglesOrbita.Length; a++)
            {
                if (Mathf.Abs(s_anglesOrbita[a]) > arco) continue;

                Vector3 dir = Quaternion.AngleAxis(s_anglesOrbita[a], Vector3.up) * plana;
                Vector3 candidata = LiftOffGround(
                    target + dir * (radio * mas) + Vector3.up * (altura + (mas - 1f) * 3f));

                int puntos = Penalizacion(target, candidata, sujeto, secundario);
                if (puntos > 0)
                {
                    int coste = puntos + Mathf.RoundToInt(Mathf.Abs(s_anglesOrbita[a]) * 0.1f);
                    if (coste < mejorPuntos)
                    {
                        mejorPuntos = coste;
                        mejor = candidata;
                        mejorAngulo = s_anglesOrbita[a];
                        mejorAltura = (mas - 1f) * 3f;
                        mejorRadio = mas;
                    }
                    continue;
                }

                s_anguloRecordado = s_anglesOrbita[a];
                s_alturaRecordada = (mas - 1f) * 3f;
                s_radioRecordado = mas;
                s_hayOrbitaRecordada = true;
                s_fotogramasTapado = 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ShotComposer] El plano '{framing.Describe()}' no tenía ningún ángulo " +
                    $"libre a su distancia, así que se ha abierto a {mas:F1}× y {(mas - 1f) * 3f:F1} m " +
                    "más alto. Sale más abierto de lo que pide el asset, pero se ve.");
#endif
                return candidata;
            }
        }

        // Ninguna impecable dentro del arco. Antes de dar la vuelta al personaje —que es lo que
        // producía los planos de nuca— se coge la MENOS MALA de las de dentro del arco, siempre
        // que el sujeto se vea desde ella (por debajo de 1000 puntos, ver Penalizacion). Un alero
        // en una esquina se aguanta; una nuca no.
        if (mejorPuntos < 1000)
        {
            s_anguloRecordado = mejorAngulo;
            s_alturaRecordada = mejorAltura;
            s_radioRecordado = mejorRadio;
            s_hayOrbitaRecordada = true;
            s_fotogramasTapado = 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ShotComposer] El plano '{framing.Describe()}' no tenía ningún ángulo " +
                $"impecable dentro de sus {arco:F0}°, así que se ha cogido el menos malo " +
                $"({mejorAngulo:F0}°, {mejorPuntos} puntos de estorbo). Se ve al personaje de " +
                "frente, con algo de decorado en el cuadro. Si molesta, hay que separar al " +
                "personaje del decorado en la escena.");
#endif
            return mejor;
        }

        // Ni eso. Ahora sí, la vuelta entera: un plano de espaldas se ve, y una pared no. Pero
        // esto es lo último y se avisa, porque lo que hay que mover es el decorado o al personaje.
        if (arco < 180f)
        {
            for (int l = 0; l < s_alturasOrbita.Length; l++)
            {
                for (int a = 0; a < s_anglesOrbita.Length; a++)
                {
                    if (Mathf.Abs(s_anglesOrbita[a]) <= arco) continue;   // ya probados arriba

                    Vector3 dir = Quaternion.AngleAxis(s_anglesOrbita[a], Vector3.up) * plana;
                    Vector3 candidata = LiftOffGround(
                        target + dir * (radio * 1.6f) + Vector3.up * (altura + 1.8f));

                    if (Penalizacion(target, candidata, sujeto, secundario) >= 1000) continue;

                    s_anguloRecordado = s_anglesOrbita[a];
                    s_alturaRecordada = 1.8f;
                    s_radioRecordado = 1.6f;
                    s_hayOrbitaRecordada = true;
                    s_fotogramasTapado = 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[ShotComposer] El plano '{framing.Describe()}' no tenía " +
                        $"NINGÚN ángulo libre dentro de sus {arco:F0}°, así que se ha rodado " +
                        $"{s_anglesOrbita[a]:F0}°: va a salir de lado o de espaldas. Lo que hay que " +
                        "mover es el personaje o el decorado, no la cámara.");
#endif
                    return candidata;
                }
            }
        }

        // El sujeto está metido en un sitio del que no se le puede ver desde ningún sitio. Aquí sí
        // toca acercarse, aun a costa del encuadre: un plano cerrado de más se ve y una pared no.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning($"[ShotComposer] El plano '{framing.Describe()}' no tiene NINGÚN ángulo " +
            "despejado alrededor del sujeto, ni siquiera abriendo el plano: está demasiado pegado a " +
            "la geometría. Se acerca la cámara para poder verlo, así que el encuadre no será el que " +
            "dice el asset. Esto se arregla separando al personaje de la pared en la escena, no aquí.");
#endif
        return LiftOffGround(PullOutOfWalls(target, desired, minDistance));
    }

    /// ¿Vale esta posición de cámara?
    ///
    /// Son tres preguntas, y hasta el 20 sep solo se hacía la primera:
    ///
    ///  1. ¿Se ve al sujeto desde ahí? (IsPathClear)
    ///  2. ¿Se ha quedado la lente DENTRO de alguien? Un raycast no lo contesta: un personaje
    ///     puede no llevar collider, y el que lo lleva lo lleva en el cuerpo. Es el plano de
    ///     'Protección Absoluta' de la séptima grabación: pantalla entera de color pelo.
    ///  3. ¿Hay decorado pegado a la lente, aunque no esté en medio? Lo que tapa el plano de la
    ///     conversación de la plaza es un tejado que está AL LADO, no delante: la línea al sujeto
    ///     estaba despejada y el solver daba el plano por bueno.
    /// Lo malo que es esta posición de cámara, en puntos. 0 = impecable.
    ///
    /// Antes esto era un sí/no, y esa era media causa de los planos de espaldas: en cuanto una
    /// posición no pasaba el corte se descartaba entera, y si no pasaba ninguna dentro del arco se
    /// acababa dando la vuelta al personaje. Con puntos se puede decir "ninguna está limpia, pero
    /// esta lo está bastante más que aquella", que es lo que hace un operador de cámara.
    ///
    /// Los pesos ordenan por gravedad: no ver al sujeto es peor que tener la lente metida en un
    /// muro, y eso es peor que un tejado comiéndose una esquina del cuadro.
    private static int Penalizacion(Vector3 target, Vector3 camara, SequenceActor sujeto, SequenceActor secundario)
    {
        int puntos = 0;

        // De más barato a más caro, y en cuanto la posición ya está descartada se deja de mirar:
        // los ocho rayos del cuadro son lo más caro de todo esto y se resuelven planos en vivo
        // cada fotograma. Si no se ve al sujeto, da igual lo que haya en las esquinas.
        if (!IsPathClear(target, camara, sujeto, secundario)) return 1000;
        if (CamaraMetidaEnAlguien(camara, sujeto, secundario)) puntos += 500;
        if (ElSecundarioSeComeElCuadro(camara, target, secundario)) puntos += 400;
        if (LenteContraElDecorado(camara, target, sujeto, secundario)) puntos += 100;
        if (puntos >= 500) return puntos;

        puntos += RayosDelCuadroTapados(camara, target, sujeto, secundario) * 10;
        return puntos;
    }

    /// El campo de visión y la relación de aspecto del plano que se está resolviendo ahora mismo.
    ///
    /// Hacen falta para saber qué entra en el cuadro, y `Despejado` —que es a quien le hacen
    /// falta— se llama desde dentro del buscador de posiciones, sin acceso al encuadre. Mismo
    /// patrón que `s_ctx`: se dejan puestos antes de buscar.
    private static float s_fovDelPlano = 45f;
    private static float s_aspecto = 16f / 9f;

    /// Ocho direcciones hacia el borde del cuadro: las cuatro esquinas y los cuatro lados, al 90 %
    /// del borde para no rozar justo el filo.
    private static readonly Vector2[] s_bordeDelCuadro =
    {
        new(-0.9f,  0.9f), new(0.9f,  0.9f), new(-0.9f, -0.9f), new(0.9f, -0.9f),
        new( 0f,    0.9f), new(0f,   -0.9f), new(-0.9f,  0f),   new(0.9f,  0f),
    };

    /// A cuántos metros de la cámara el decorado deja de ser fondo y pasa a comerse el encuadre.
    private const float DespejeDelCuadro = 3.2f;

    /// ¿Cuántos bordes del cuadro tienen decorado encima, y cerca?
    ///
    /// Esto es lo que faltaba (INC-300). `LenteContraElDecorado` pregunta si hay algo PEGADO a la
    /// lente; la sexta grabación está llena de planos donde no hay nada pegado y aun así un faldón
    /// de tejado a dos metros cruza la pantalla entera en diagonal — 2:24, 2:28, 2:32, 2:36, 2:40,
    /// 2:48 y 3:00, siete planos seguidos con el mismo alero. La cámara estaba "despejada" según
    /// todas las comprobaciones que había, porque la línea al sujeto sí estaba limpia.
    ///
    /// Aquí se mira lo que de verdad importa: lo que va a SALIR EN EL CUADRO. Se tiran rayos por
    /// los bordes del encuadre, con el campo de visión y el aspecto de verdad, y se cuenta cuántos
    /// se dan con decorado cerca.
    ///
    /// Los personajes no cuentan: un vecino en primer término da profundidad, y de los que tapan
    /// de verdad ya se encargan `CamaraMetidaEnAlguien` y `PersonajeTapaPorDebajoDe`. El suelo
    /// tampoco: se reconoce porque su caja entera queda por debajo de la lente.
    private static int RayosDelCuadroTapados(Vector3 camara, Vector3 lookAt,
        SequenceActor sujeto, SequenceActor secundario)
    {
        Vector3 adelante = lookAt - camara;
        if (adelante.sqrMagnitude < 0.0001f) return 0;
        adelante.Normalize();

        Vector3 derecha = Vector3.Cross(Vector3.up, adelante);
        if (derecha.sqrMagnitude < 0.0001f) return 0;
        derecha.Normalize();
        Vector3 arriba = Vector3.Cross(adelante, derecha);

        float tanV = Mathf.Tan(s_fovDelPlano * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * s_aspecto;

        int tapados = 0;

        foreach (Vector2 e in s_bordeDelCuadro)
        {
            Vector3 dir = (adelante + derecha * (e.x * tanH) + arriba * (e.y * tanV)).normalized;

            int n = Physics.RaycastNonAlloc(camara, dir, s_hits, DespejeDelCuadro, ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < n; i++)
            {
                var col = s_hits[i].collider;
                if (col == null) continue;
                if (EsDe(col, sujeto) || EsDe(col, secundario)) continue;
                if (IsCharacter(col)) continue;
                if (col.bounds.max.y < camara.y - LoQueCuentaComoSuelo) continue;

                tapados++;
                break;
            }
        }

        return tapados;
    }

    /// A partir de qué distancia de la lente la cabeza del secundario deja de «tapar» y pasa a
    /// ser un personaje en segundo término, que da profundidad y está bien.
    ///
    /// (21 sep) Primero fue 1,7 m desde sus pies, en planta. No bastaba: en la grabación 11 la
    /// cabeza del Archimago sigue ocupando un tercio del cuadro en «Mañana rompes dos», y la
    /// espalda de Oliver tapa a Will entero en su saludo. Estos personajes son casi todo cabeza,
    /// y una cabeza de ochenta centímetros a dos metros y pico de un objetivo de 40° llena media
    /// pantalla. Lo que importa no es a cuánto está, sino si SALE EN EL CUADRO y a qué distancia.
    private const float DistanciaQueTapa = 3.6f;

    private static bool s_secundarioNoSale;

    /// ¿Sale la cabeza del secundario en el cuadro, y tan cerca que se come el plano?
    ///
    /// En un primer plano, una reacción o un plano medio el secundario no sale en cuadro: está
    /// para dar el ángulo de tres cuartos y el lado del eje, nada más. `CamaraMetidaEnAlguien` no
    /// lo coge, porque la cámara no está DENTRO de él, y `RayosDelCuadroTapados` tampoco, porque
    /// se salta a los personajes a propósito.
    ///
    /// Se proyecta su cabeza con el campo de visión y el aspecto del plano de verdad: si cae
    /// dentro del encuadre (aunque sea un borde) y a menos de `DistanciaQueTapa`, la posición se
    /// penaliza. No se descarta: el buscador se va a otro ángulo si puede, y si no puede da el
    /// menos malo en vez de dar la vuelta al personaje.
    private static bool ElSecundarioSeComeElCuadro(Vector3 camara, Vector3 lookAt, SequenceActor secundario)
    {
        if (!s_secundarioNoSale || secundario?.Transform == null) return false;

        Vector3 adelante = lookAt - camara;
        if (adelante.sqrMagnitude < 0.0001f) return false;
        adelante.Normalize();

        Vector3 derecha = Vector3.Cross(Vector3.up, adelante);
        if (derecha.sqrMagnitude < 0.0001f) return false;
        derecha.Normalize();
        Vector3 arriba = Vector3.Cross(adelante, derecha);

        float alto = secundario.HeadTopHeight;
        Vector3 cabeza = secundario.Transform.position + Vector3.up * (alto * 0.72f);
        float radioCabeza = Mathf.Max(0.4f, alto * 0.3f);

        Vector3 v = cabeza - camara;
        float fondo = Vector3.Dot(v, adelante);
        if (fondo < 0.05f) return false;                 // detrás de la cámara
        if (fondo - radioCabeza > DistanciaQueTapa) return false;   // lejos: es segundo término

        float tanV = Mathf.Tan(s_fovDelPlano * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * s_aspecto;
        float r = radioCabeza / fondo;

        float x = Mathf.Abs(Vector3.Dot(v, derecha)) / fondo;
        float y = Mathf.Abs(Vector3.Dot(v, arriba)) / fondo;

        return x - r < tanH && y - r < tanV;
    }

    /// ¿Ha quedado la cámara dentro del volumen de algún personaje que no sea el del plano?
    ///
    /// Se compara contra lo que SequenceActor mide de los Renderer (SafeRadius de ancho,
    /// HeadTopHeight de alto), no contra colliders, precisamente porque el problema es el pelo.
    private static bool CamaraMetidaEnAlguien(Vector3 camara, SequenceActor sujeto, SequenceActor secundario)
    {
        if (s_ctx == null) return false;

        foreach (var actor in s_ctx.Actores)
        {
            if (actor?.Transform == null) continue;

            Vector3 pies = actor.Transform.position;

            // Los dos del plano NO se saltan (INC-310). Saltárselos del todo era la razón de que
            // la cámara acabara DENTRO del pelo de Liora en la octava grabación — pantalla rosa
            // entera en 2:48 mientras el Archimago habla. Estar cerca del secundario es legítimo
            // (un escorzo se pone justo detrás de su hombro); estar dentro de él no lo es nunca.
            // Así que a ellos se les mide solo el núcleo del cuerpo, sin el margen del pelo.
            bool esDelPlano = actor == sujeto || actor == secundario;

            float dx = camara.x - pies.x;
            float dz = camara.z - pies.z;
            float radio = esDelPlano
                ? actor.SafeRadius * 0.55f
                : actor.SafeRadius + MargenAlrededorDeUnPersonaje;
            if (dx * dx + dz * dz > radio * radio) continue;

            // Por arriba se deja pasar: una cámara por encima de la cabeza de alguien es un
            // picado, no un tapón.
            if (camara.y < pies.y - 0.3f) continue;
            if (camara.y > pies.y + actor.HeadTopHeight + 0.35f) continue;

            return true;
        }

        return false;
    }

    /// ¿Hay geometría pegada a la lente?
    ///
    /// No pregunta si está en medio — para eso ya está IsPathClear — sino si hay algo tan cerca
    /// que se va a comer el encuadre pase lo que pase. Lo que tapa el plano del segundo 1:03 de
    /// la séptima grabación es el faldón de una casa que está AL LADO de la cámara, no delante:
    /// la línea al sujeto estaba despejada y el solver daba el plano por bueno.
    ///
    /// Se resolvía con cinco rayos cortos HORIZONTALES, abriendo en abanico hacia donde mira la
    /// cámara. Horizontales a propósito: así el suelo —que está a 35 cm por debajo siempre, a
    /// propósito, ver MinGroundClearance— no contaba.
    ///
    /// (20 sep, INC-294) Y ese "a propósito" era el fallo: unos rayos horizontales tampoco ven
    /// nada de lo que está POR ARRIBA. Aleros, tejados, vigas y rampas pasaban limpios, que es
    /// justo lo que tapa el plano en 2:48, 3:04, 3:16, 3:20 y 3:28 de la quinta grabación — faldones
    /// marrones cruzando en diagonal toda la pantalla y cuadros casi enteros en negro.
    ///
    /// Los rayos se quedan (ven lo que está delante un poco más lejos), y se les añade una
    /// comprobación que no tiene dirección: una esfera alrededor de la lente. Lo que toque esa
    /// esfera está pegado a la cámara, venga de donde venga.
    ///
    /// El suelo se descarta por los BOUNDS del collider, no por ClosestPoint: si la caja entera de
    /// un collider queda por debajo de la lente, es suelo y no tapa nada. Los bounds los contesta
    /// Unity siempre, también en un MeshCollider cóncavo, que es donde ClosestPoint miente.
    private static readonly float[] s_abanicoDeLaLente = { 0f, 38f, -38f, 72f, -72f };

    /// Radio de la esfera alrededor de la lente. Un poco mayor que el abanico: aquí no hay
    /// dirección que valga, así que lo que entre está de verdad encima de la cámara.
    private const float BurbujaDeLaLente = 0.85f;

    /// Margen por debajo de la lente para dar algo por "suelo". MinGroundClearance deja la cámara
    /// a 35 cm del suelo, así que 25 cm por debajo de la lente todavía es suelo.
    private const float LoQueCuentaComoSuelo = 0.25f;

    private static readonly Collider[] s_burbuja = new Collider[24];

    private static bool LenteContraElDecorado(Vector3 camara, Vector3 lookAt, SequenceActor sujeto, SequenceActor secundario)
    {
        Vector3 haciaElSujeto = Flatten(lookAt - camara);
        if (haciaElSujeto.sqrMagnitude < 0.0001f) return false;
        haciaElSujeto.Normalize();

        for (int r = 0; r < s_abanicoDeLaLente.Length; r++)
        {
            Vector3 dir = Quaternion.AngleAxis(s_abanicoDeLaLente[r], Vector3.up) * haciaElSujeto;

            int n = Physics.RaycastNonAlloc(camara, dir, s_hits, AireAlrededorDeLaLente, ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < n; i++)
            {
                var col = s_hits[i].collider;
                if (col == null) continue;
                if (EsDe(col, sujeto) || EsDe(col, secundario)) continue;

                // Los personajes los lleva CamaraMetidaEnAlguien, con su volumen real.
                if (IsCharacter(col)) continue;

                return true;
            }
        }

        return HayAlgoPegadoALaLente(camara, sujeto, secundario);
    }

    /// Lo que los rayos no pueden ver: cualquier cosa pegada a la lente, en cualquier dirección.
    ///
    /// Aquí entra también el gentío (INC-295). `CamaraMetidaEnAlguien` solo conoce a los actores
    /// que la secuencia ha resuelto POR NOMBRE, así que un vecino cualquiera —o uno que todavía no
    /// se ha nombrado cuando se resuelven los primeros planos— no estaba en esa lista y la cámara
    /// se le metía dentro. Se ve en 0:48 y 0:52 de la quinta grabación. Un collider de personaje
    /// dentro de la burbuja cuenta igual que un muro.
    private static bool HayAlgoPegadoALaLente(Vector3 camara, SequenceActor sujeto, SequenceActor secundario)
    {
        int n = Physics.OverlapSphereNonAlloc(camara, BurbujaDeLaLente, s_burbuja, ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < n; i++)
        {
            var col = s_burbuja[i];
            if (col == null) continue;
            if (EsDe(col, sujeto) || EsDe(col, secundario)) continue;

            // El suelo no tapa: la cámara va siempre por encima de él a propósito. Se reconoce
            // porque su caja entera queda por debajo de la lente.
            if (!IsCharacter(col) && col.bounds.max.y < camara.y - LoQueCuentaComoSuelo) continue;

            return true;
        }

        return false;
    }

    /// A cuántos metros de la CÁMARA un personaje deja de ser un primer término y pasa a ser una
    /// pared con pelo.
    ///
    /// Un vecino a seis metros delante de la cámara es composición: da profundidad y tamaño. El
    /// mismo vecino a medio metro es una mancha marrón que ocupa media pantalla — y es, tal cual,
    /// lo que se ve en la quinta grabación del prólogo: dos planos del duelo tapados por la cabeza
    /// de alguien, uno de ellos el del hechizo final con el icono de 'Espacio' encima. Pasaba
    /// porque IsPathClear ignoraba a TODOS los personajes, a cualquier distancia, y con diez
    /// aldeanos en la plaza eso ocurre constantemente.
    /// (20 sep) Estaba en 2 m y se quedaba corto. A 2,5 m de la lente, con el campo de vision de
    /// un plano medio, una cabeza de 60 cm sigue ocupando un tercio de la pantalla -- y eso es lo
    /// que tapa la conversacion de la plaza en la septima grabacion. Se sube a 3,2.
    private const float PersonajeTapaPorDebajoDe = 3.2f;

    /// Cuanto tiene que respirar la lente alrededor. Por debajo de esto, un tejado o un muro que
    /// ni siquiera esta entre la camara y el sujeto se come medio encuadre igual: es el plano del
    /// segundo 1:03 de la septima grabacion, con el faldon de una casa ocupando la mitad
    /// izquierda mientras el Archimago habla en la esquina.
    private const float AireAlrededorDeLaLente = 0.75f;

    /// Margen que se deja alrededor del volumen de un personaje al comprobar si la camara se ha
    /// quedado dentro de el. El radio que mide SequenceActor es el del CUERPO; el pelo sobresale.
    private const float MargenAlrededorDeUnPersonaje = 0.55f;

    /// ¿Hay línea de visión limpia entre estos dos puntos, ignorando por completo a los personajes?
    ///
    /// Esta es la versión "de geometría" y es la que usa el beat de cámara para decidir si un
    /// movimiento entre dos planos atravesaría el pueblo (en cuyo caso no puede ser un movimiento:
    /// es un corte). Ahí no hay sujeto ni cámara, solo dos puntos de una trayectoria, así que la
    /// regla de cercanía de abajo no significaría nada y no se aplica.
    public static bool IsPathClear(Vector3 from, Vector3 to)
        => IsPathClear(from, to, null, null);

    /// ¿Se ve al sujeto desde la cámara?
    ///
    /// 'from' es el sujeto y 'to' la cámara. Un personaje en medio NO tapa — un cuerpo delante de
    /// la cámara es un primer término, y así estaba pensado desde el principio — salvo que esté
    /// pegado al objetivo (ver PersonajeTapaPorDebajoDe), donde ya no compone nada: solo estorba.
    ///
    /// LOS DOS PERSONAJES DEL PROPIO PLANO NUNCA TAPAN, estén donde estén. Es lo que diferencia un
    /// plano sobre el hombro de un plano estropeado: en el primero el hombro está a metro y medio
    /// de la lente A PROPÓSITO, y es justo lo que se quiere ver. Sin esta exención, la regla de
    /// arriba se cargaría todos los planos sobre el hombro del juego — y de hecho solo se libraban
    /// por unos centímetros, porque la distancia de cámara está en 1,35; con 1,0 caían todos.
    public static bool IsPathClear(Vector3 from, Vector3 to, SequenceActor sujeto, SequenceActor secundario)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.01f) return true;

        Vector3 dir = delta / distance;

        int count = Physics.RaycastNonAlloc(from, dir, s_hits, distance, ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            var hit = s_hits[i];
            if (hit.collider == null) continue;

            // Los protagonistas del plano nunca tapan, sean personajes o no.
            //
            // Lo de "o no" importa: un objeto del decorado también puede ser el sujeto de un plano
            // —la carreta que levita, el escudo, la puerta del Sendero— y entonces el rayo sale de
            // SU PROPIO centro y lo primero que encuentra es su propio collider. Sin esta
            // comprobación, un plano de la carreta daba por tapados todos los ángulos y acababa
            // en el rescate, abriéndose de más para ver algo que nunca estuvo tapado.
            if (EsDe(hit.collider, sujeto) || EsDe(hit.collider, secundario)) continue;

            // Un personaje delante de la cámara es un primer término, no un problema... mientras
            // no esté pegado a la lente. 'from' es el sujeto, así que lo que está cerca de la
            // cámara es lo que está LEJOS del sujeto: de ahí la resta.
            if (IsCharacter(hit.collider) && (distance - hit.distance) > PersonajeTapaPorDebajoDe)
                continue;

            return false;
        }

        return true;
    }

    /// ¿Este collider pertenece a este actor? Se compara por raíz de la jerarquía, que es la misma
    /// que usa IsCharacter para decidir si algo es un personaje.
    private static bool EsDe(Collider collider, SequenceActor actor)
    {
        if (collider == null || actor?.Transform == null) return false;
        return collider.transform.root == actor.Transform.root;
    }

    /// Si entre el sujeto y la cámara hay geometría, acerca la cámara hasta este lado de la pared.
    ///
    /// Los personajes NO cuentan como obstrucción: viven en la capa Default igual que el
    /// escenario (ver CLAUDE.md § 2), así que se distinguen por componente. Un personaje delante
    /// de la cámara es un primer término, no un problema.
    private static Vector3 PullOutOfWalls(Vector3 target, Vector3 desired, float minDistance)
    {
        Vector3 delta = desired - target;
        float distance = delta.magnitude;
        if (distance < 0.01f) return desired;

        Vector3 dir = delta / distance;

        int count = Physics.RaycastNonAlloc(target, dir, s_hits, distance, ~0,
            QueryTriggerInteraction.Ignore);

        float nearest = distance;
        for (int i = 0; i < count; i++)
        {
            var hit = s_hits[i];
            if (hit.collider == null) continue;
            if (IsCharacter(hit.collider)) continue;   // un personaje no tapa: es primer término
            if (hit.distance < nearest) nearest = hit.distance;
        }

        if (nearest >= distance) return desired;

        float pulled = Mathf.Max(nearest - WallPadding, minDistance);
        return target + dir * pulled;
    }

    /// Sube la cámara si ha quedado por debajo del suelo o pegada a él. Es el fallo que ya se dio
    /// una vez en esta misma cinemática ("se ve por debajo del mundo", 14 sept 2026), allí porque
    /// los planos estaban clavados a coordenadas fijas del mapa.
    private static Vector3 LiftOffGround(Vector3 position)
    {
        // Solo cuenta lo que está POR DEBAJO de la cámara. Con un único Raycast, lo primero que
        // encuentra bajando desde cuatro metros por encima puede ser un tejado o una viga, y
        // entonces esto no sube la cámara sobre el suelo: la sube sobre el tejado (INC-315).
        int n = Physics.RaycastNonAlloc(position + Vector3.up * 4f, Vector3.down, s_hits, 12f, ~0,
            QueryTriggerInteraction.Ignore);

        float suelo = float.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            var hit = s_hits[i];
            if (hit.collider == null) continue;
            if (hit.point.y > position.y) continue;
            if (hit.point.y > suelo) suelo = hit.point.y;
        }

        if (!float.IsNegativeInfinity(suelo))
        {
            float floor = suelo + MinGroundClearance;
            if (position.y < floor) position.y = floor;
        }

        return position;
    }

    /// Caché de "¿este collider es de un personaje?", por InstanceID.
    ///
    /// FIX (auditoría 17 sep 2026): la comprobación por componente la impone CLAUDE.md § 2 —
    /// personajes y escenario comparten la capa Default, así que no hay forma de distinguirlos por
    /// máscara. Pero PullOutOfWalls corre cada frame en los planos que siguen a alguien, y recorre
    /// hasta dieciséis impactos: eso eran hasta 32 GetComponent por frame, que es exactamente lo
    /// que prohíbe la misma regla. Lo que un collider ES no cambia mientras exista, así que se
    /// pregunta una vez.
    //
    // FIX (compilador, 17 sep 2026): la caché iba indexada por collider.GetInstanceID(), que en
    // esta versión de Unity está obsoleto COMO ERROR (CS0619) y rompía la compilación entera.
    // Mismo caso que ya se arregló en NPCCombatBrain.cs. Aquí, en vez de sustituirlo por un hash,
    // se usa el propio Collider como clave: el Dictionary resuelve las colisiones de hash por sí
    // mismo, así que no hay ni riesgo de confundir dos colliders ni una API de Unity que pueda
    // volver a quedar obsoleta. No hay fuga: ClearColliderCache() la vacía en cada secuencia.
    private static readonly Dictionary<Collider, bool> s_esPersonaje = new(64);

    /// Un collider es de personaje si su raíz tiene NPCSimpleAnimator — lo llevan el jugador y
    /// todos los NPCs, y ningún prop de escenario (CLAUDE.md § 2).
    private static bool IsCharacter(Collider collider)
    {
        if (collider == null) return false;
        if (s_esPersonaje.TryGetValue(collider, out bool cacheado)) return cacheado;

        bool esPersonaje = collider.transform.root.GetComponent<NPCSimpleAnimator>() != null;
        s_esPersonaje[collider] = esPersonaje;
        return esPersonaje;
    }

    /// Vacía la caché de colliders. La llama el SequencePlayer al empezar cada secuencia, para no
    /// arrastrar identificadores de objetos de escenas ya descargadas.
    public static void ClearColliderCache()
    {
        s_esPersonaje.Clear();
        OlvidarOrbita();

        // Y el contexto: es de la secuencia que acaba de terminar, y guardarlo de una escena a
        // otra manteniria vivos sus actores.
        s_ctx = null;
    }

    /// Cuánto respira el plano: distancia real entre la cámara y lo que mira, ya con el pase de
    /// seguridad aplicado. Es lo que decide si merece la pena probar el otro lado del eje.
    private static float Clearance(ShotSolution s) => Vector3.Distance(s.position, s.lookAt);

    // ── Aviso de corte repetido ───────────────────────────────────────────────────────────────

    /// Dos planos seguidos que apenas cambian de ángulo ni de tamaño se leen como un fallo de
    /// montaje, no como un corte. La regla de oficio es cambiar el tamaño de plano o moverse más
    /// de 30 grados. Esto no lo corrige — decidirlo es del guion, no del solver — pero lo dice por
    /// consola al montar, que es cuando se puede arreglar barato.
    private static void WarnIfSameAsPrevious(SequenceContext ctx, ShotFraming f, ShotSolution s)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!ctx.TryGetPreviousShot(out ShotSolution prev)) return;

        Vector3 dirNow = (s.lookAt - s.position).normalized;
        Vector3 dirPrev = (prev.lookAt - prev.position).normalized;

        float angle = Vector3.Angle(dirNow, dirPrev);
        float sizeRatio = prev.frameHeight > 0.01f ? s.frameHeight / prev.frameHeight : 1f;

        bool sameAngle = angle < 30f;
        bool sameSize = sizeRatio > 0.75f && sizeRatio < 1.33f;

        if (sameAngle && sameSize)
            Debug.LogWarning($"[ShotComposer] El plano '{f.Describe()}' se parece demasiado al " +
                $"anterior ({angle:F0}° de diferencia de ángulo, mismo tamaño de encuadre): al " +
                "verlo parecerá un salto de montaje, no un corte. Cambia el tipo de plano, o el " +
                "personaje al que mira, o quita uno de los dos cortes.");
#endif
    }

    // ── Trigonometría de encuadre ─────────────────────────────────────────────────────────────

    /// A qué distancia hay que estar para que entren 'height' metros de alto en el cuadro.
    private static float DistanceToFrame(float height, float fov)
        => (height * 0.5f) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

    /// A qué distancia hay que estar para que entren 'width' metros de ancho en el cuadro.
    private static float DistanceToFrameWidth(float width, float fov, float aspect)
    {
        float halfFovV = fov * 0.5f * Mathf.Deg2Rad;
        float halfFovH = Mathf.Atan(Mathf.Tan(halfFovV) * Mathf.Max(aspect, 0.1f));
        return (width * 0.5f) / Mathf.Tan(halfFovH);
    }

    /// El campo de visión que hace que entren 'height' metros a la distancia dada. Es la operación
    /// inversa de DistanceToFrame, y es la que usa el plano sobre el hombro: ahí la cámara no se
    /// puede alejar (está atada al hombro), así que lo que se ajusta es el objetivo.
    private static float FovToFrame(float height, float distance)
    {
        if (distance < 0.01f) return 45f;
        return 2f * Mathf.Atan((height * 0.5f) / distance) * Mathf.Rad2Deg;
    }

    /// Proyecta un vector al plano del suelo. Los planos de cámara se calculan en horizontal y la
    /// altura se pone aparte: así una cuesta o un NPC flotando medio metro no inclinan el encuadre.
    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
