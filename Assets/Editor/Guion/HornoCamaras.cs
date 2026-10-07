#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Cámaras: cada plano se resuelve con las posiciones ya conocidas de todo el reparto.
///
/// Tipos: general · conjunto · medio · primer · detalle · dos A B · hombro A B · sigue A · fijo
/// Opciones: desde=P (sitio de la cámara) · mira=P (foco fijo) · altura=m · distancia=m · lente=°
///   · zoom=° (lente al final) · lado=izq|der|frente|espalda · rumbo=° · picado · contrapicado
///   · mezcla=s · mano=0..1 · encuadre=izq|centro|der · mover=P (travelling hasta P) · suave=s · cruza
internal sealed partial class Horno
{
    private sealed class Encuadre
    {
        public TipoDePlano tipo;
        public List<ActorH> sujetos = new();
        public Vector3? focoFijo;
        public string prop;     // un objeto del decorado (PROP_...) al que sigue el foco
        public float t0, t1;
        public GuionTexto.Orden o;
        public Vector3 camara;
        public float azimut;   // rumbo desde el foco hacia la cámara
        public float lente;
        public float coste;
        public float tRef;     // instante en que se calculó la posición de la cámara
        public string desglose;
        public List<Vector3> extras = new();   // puntos que también entran en el cuadro
        public float banda;      // alto de cada banda de cine en ese momento (0 = sin bandas)
        public bool aireArriba;  // con bocadillos, sitio encima de la cabeza para el globo
    }

    /// Alto de las bandas de cine en el instante t (lo que tapan arriba y abajo, en tanto por uno
    /// de la pantalla). El encuadre se calcula sobre lo que queda visible entre las dos bandas.
    private float BandaEn(float t)
    {
        float banda = 0f, tUltimo = float.MinValue;
        foreach (var ef in _efectos)
            if (ef.beat is BandasDeCineBeat b && ef.t <= t + 0.01f && ef.t >= tUltimo)
            { tUltimo = ef.t; banda = b.mostrar ? b.altura : 0f; }
        return banda;
    }

    private Encuadre _anterior;

    /// Desglose del coste del último encuadre evaluado (para el storyboard y el parte).
    private sealed class Desglose
    {
        public float vista, tapado, pegado, delante, estorbo, cara, angulo, eje;
        public override string ToString()
        {
            var partes = new List<string>();
            void P(string n, float v) { if (v >= 1f) partes.Add($"{n} {v:0}"); }
            P("decorado", vista); P("tapado", tapado); P("pegado", pegado); P("delante", delante);
            P("estorbo", estorbo); P("cara", cara); P("ángulo", angulo); P("eje", eje);
            return partes.Count == 0 ? "limpio" : string.Join(", ", partes);
        }
    }
    private readonly Desglose _d = new();
    internal readonly Dictionary<float, string> NotasDePlano = new();

    private void ResolverPlanos()
    {
        if (_planosPedidos.Count == 0)
        {
            Error("El guion no tiene ningún «plano»: sin planos no se ve nada.");
            return;
        }
        if (_planosPedidos[0].t0 > 0.05f)
            Aviso($"El primer plano empieza en {_planosPedidos[0].t0:0.0} s: hasta entonces la cámara se queda donde estuviera.");

        for (int i = 0; i < _planosPedidos.Count; i++)
        {
            var pedido = _planosPedidos[i];
            float t1 = i + 1 < _planosPedidos.Count ? _planosPedidos[i + 1].t0 : _fin;
            if (t1 - pedido.t0 < 0.05f) { Aviso($"{pedido.orden}: dura {t1 - pedido.t0:0.00} s, se ignora (otro plano empieza a la vez)"); continue; }
            var plano = Resolver(pedido.orden, pedido.t0, t1);
            if (plano != null) _salida.planos.Add(plano);
        }
    }

    private PlanoHorneado Resolver(GuionTexto.Orden o, float t0, float t1)
    {
        var e = new Encuadre { o = o, t0 = t0, t1 = t1, tRef = t0, banda = BandaEn(t0) };
        string tipo = o.args.FirstOrDefault() ?? "general";
        switch (tipo)
        {
            case "general": e.tipo = TipoDePlano.General; break;
            case "conjunto": e.tipo = TipoDePlano.Conjunto; break;
            case "medio": e.tipo = TipoDePlano.Medio; break;
            case "primer": case "primerplano": e.tipo = TipoDePlano.Primer; break;
            case "detalle": e.tipo = TipoDePlano.Detalle; break;
            case "dos": e.tipo = TipoDePlano.Dos; break;
            case "hombro": e.tipo = TipoDePlano.Hombro; break;
            case "sigue": e.tipo = TipoDePlano.Sigue; break;
            case "fijo": e.tipo = TipoDePlano.Fijo; break;
            default: Error($"{o}: tipo de plano '{tipo}' desconocido"); return null;
        }
        foreach (var n in o.args.Skip(1))
        {
            if (GuionTexto.Marcas.Contains(n)) continue;
            if (_actores.TryGetValue(n, out var a)) e.sujetos.Add(a);
            else if (n == "todos") e.sujetos.AddRange(_actores.Values.Where(x => x.VisibleEn((t0 + t1) * 0.5f)));
            else if (n.StartsWith("PROP_") && PosDeProp(n, out var pp)) { e.prop = n; e.focoFijo = pp; }
            else if (Punto(n, o.linea, out var p)) e.extras.Add(p);
        }
        // Puntos sueltos: sin actores son el foco; con actores, entran también en el cuadro.
        if (e.sujetos.Count == 0 && e.extras.Count > 0 && e.focoFijo == null)
        {
            e.focoFijo = e.extras[0] + Vector3.up * o.OpF("altura_foco", 1.2f);
            e.extras.Clear();
        }
        if (o.opciones.TryGetValue("mira", out var mira) && Punto(mira, o.linea, out var pm)) e.focoFijo = pm + Vector3.up * o.OpF("altura_foco", 1.2f);
        if (e.sujetos.Count == 0 && e.focoFijo == null)
        {
            Error($"{o}: el plano no dice a quién (o a dónde) mira");
            return null;
        }
        // Un plano medio de alguien que está hablando cara a cara con otro a menos de 2,8 m se
        // convierte en contraplano sobre el hombro del otro: si no, el otro se cuela de espaldas
        // en el borde del cuadro. «solo» lo impide.
        if (e.tipo == TipoDePlano.Medio && e.sujetos.Count == 1 && !o.Tiene("solo") && !o.opciones.ContainsKey("desde"))
        {
            var s = e.sujetos[0];
            float tm = Mathf.Lerp(t0, t1, 0.35f);
            Vector3 ps = s.PosEn(tm);
            float rumbo = s.RumboEn(tm, _actores);
            ActorH interlocutor = null; float mejorD = 2.8f;
            foreach (var b in _actores.Values)
            {
                if (b == s || !b.VisibleEn(tm)) continue;
                Vector3 d = Plano(b.PosEn(tm) - ps);
                float dist = d.magnitude;
                if (dist < 0.4f || dist > mejorD) continue;
                if (Mathf.Abs(Mathf.DeltaAngle(rumbo, Rumbo(d))) > 35f) continue;
                // Tiene que ser una conversación: el otro también le mira.
                if (Mathf.Abs(Mathf.DeltaAngle(b.RumboEn(tm, _actores), Rumbo(-d))) > 50f) continue;
                interlocutor = b; mejorD = dist;
            }
            if (interlocutor != null)
            {
                e.tipo = TipoDePlano.Hombro;
                e.sujetos.Insert(0, interlocutor);
            }
        }
        if ((e.tipo == TipoDePlano.Dos || e.tipo == TipoDePlano.Hombro) && e.sujetos.Count < 2)
        {
            Error($"{o}: «{tipo}» necesita dos personajes");
            return null;
        }

        e.aireArriba = _g.texto == PresentacionDeTexto.Bocadillo && (e.tipo == TipoDePlano.Primer || e.tipo == TipoDePlano.Medio);
        var plano = new PlanoHorneado { t0 = t0, t1 = t1, tipo = e.tipo, mezcla = o.OpF("mezcla", 0f), mano = Mathf.Clamp01(o.OpF("mano", 0.12f)) };
        e.lente = o.OpF("lente", LentePorDefecto(e.tipo));
        plano.lente = e.lente;
        plano.lenteFinal = o.OpF("zoom", 0f);

        // Sujetos que se encuadran (en el «hombro», el de espaldas no es foco).
        var foco = e.tipo == TipoDePlano.Hombro ? new List<ActorH> { e.sujetos[1] } : e.sujetos;
        foreach (var s in foco) { plano.sujetos.Add(s.id); plano.pesos.Add(1f); }
        if (foco.Count > 0) plano.extras.AddRange(e.extras);
        if (e.focoFijo != null) plano.focoFijo = e.focoFijo.Value;
        plano.alturaDelFoco = o.OpF("altura_foco", AlturaDelFoco(e.tipo, foco));
        if (e.prop != null && foco.Count == 0) { plano.sujetos.Add(e.prop); plano.pesos.Add(1f); plano.alturaDelFoco = 0f; }
        plano.suavidad = o.OpF("suave", e.tipo == TipoDePlano.Sigue ? 0.35f : e.tipo == TipoDePlano.General ? 1.0f : 0.6f);

        // Dónde va la cámara.
        if (e.tipo == TipoDePlano.Sigue) Seguir(e, plano);
        else
        {
            if (o.opciones.TryGetValue("desde", out var desde))
            {
                if (!Punto(desde, o.linea, out var pc)) return null;
                e.camara = pc + Vector3.up * o.OpF("altura", e.tipo == TipoDePlano.General ? 3f : 1.55f);
                e.azimut = Rumbo(e.camara - Foco(e, t0));
            }
            else if (e.tipo == TipoDePlano.Hombro) Hombro(e);
            else Buscar(e);
            plano.posiciones.Add(new ClaveDePosicion(t0, e.camara));
            bool fijo = o.opciones.ContainsKey("desde") || o.opciones.ContainsKey("mover") || o.Tiene("quieta");
            if (!fijo && Acompana(e.tipo)) Acompanar(e, plano);
            if (o.opciones.TryGetValue("mover", out var mover) && Punto(mover, o.linea, out var pf))
            {
                Vector3 fin = pf + Vector3.up * (e.camara.y - Suelo(e.camara).y);
                plano.posiciones.Add(new ClaveDePosicion(t1, fin));
            }
        }

        if (!o.opciones.ContainsKey("lente")) plano.lente = e.lente;
        plano.tipo = e.tipo;
        if (e.tipo != TipoDePlano.Sigue)
        {
            float estorbo = Estorbo(e.camara, Foco(e, t0), e.lente, e.tipo == TipoDePlano.General);
            if (estorbo > (o.opciones.ContainsKey("desde") ? 0.3f : 0.08f)) Aviso($"{o}: algo del decorado se mete delante y tapa un {estorbo * 100f:0}% del cuadro. Prueba lado= o desde=.");
        }
        plano.encuadre = Composicion(e, foco);
        plano.descripcion = Describir(e);
        if (e.tipo != TipoDePlano.Sigue && !o.opciones.ContainsKey("desde"))
            NotasDePlano[plano.t0] = $"coste {e.coste:0}: {e.desglose}";
        if (e.coste >= 1000f) Aviso($"{o}: no hay ángulo limpio para «{plano.descripcion}» (algo tapa la cara). Prueba con desde= o lado=.");
        _anterior = e;
        return plano;
    }

    private static float LentePorDefecto(TipoDePlano t) => t switch
    {
        TipoDePlano.General => 42f, TipoDePlano.Conjunto => 34f, TipoDePlano.Medio => 30f, TipoDePlano.Primer => 24f,
        TipoDePlano.Detalle => 18f, TipoDePlano.Dos => 32f, TipoDePlano.Hombro => 24f, TipoDePlano.Sigue => 34f, _ => 36f
    };

    private static float AlturaDelFoco(TipoDePlano t, List<ActorH> foco)
    {
        if (foco.Count == 0) return 0f;
        float ojos = foco.Average(a => a.AlturaDeOjos);
        float cara = foco.Average(a => a.AlturaDeCara);
        return t switch
        {
            TipoDePlano.Primer or TipoDePlano.Detalle or TipoDePlano.Medio or TipoDePlano.Hombro => cara,
            TipoDePlano.Dos => ojos * 0.9f,
            TipoDePlano.General => foco.Average(a => a.altura) * 0.55f,
            _ => foco.Average(a => a.altura) * 0.7f,
        };
    }

    private Vector3 Foco(Encuadre e, float t)
    {
        var foco = e.tipo == TipoDePlano.Hombro ? new List<ActorH> { e.sujetos[1] } : e.sujetos;
        if (foco.Count == 0) return e.focoFijo ?? Vector3.zero;
        float alto = e.o.OpF("altura_foco", AlturaDelFoco(e.tipo, foco));
        Vector3 c = Vector3.zero;
        foreach (var a in foco) c += a.PosEn(t);
        foreach (var x in e.extras) c += x - Vector3.up * alto;
        c /= foco.Count + e.extras.Count;
        return c + Vector3.up * alto;
    }

    /// Distancia a la que el tipo de plano queda bien con esa lente.
    private float DistanciaDeEncuadre(Encuadre e, float t)
    {
        if (e.o.opciones.ContainsKey("distancia")) return e.o.OpF("distancia", 3f);
        float tanV = Mathf.Tan(e.lente * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * 16f / 9f;
        // Con bandas de cine solo se ve el centro de la pantalla: el alto útil es menor.
        tanV *= Mathf.Max(0.3f, 1f - 2f * e.banda);
        float alto = e.sujetos.Count > 0 ? e.sujetos.Average(a => a.altura) : 1.7f;
        switch (e.tipo)
        {
            // Chibis: la cabeza es grande, así que el primer plano y el medio no encogen con la altura.
            case TipoDePlano.Primer:
            {
                float cabeza = e.sujetos.Count > 0 ? e.sujetos.Average(a => a.Cabeza) : 0.3f;
                return Mathf.Max(0.62f, (e.aireArriba ? 1.4f : 1.2f) * cabeza) / (2f * tanV) + 0.2f;
            }
            case TipoDePlano.Detalle: return 0.3f / (2f * tanV) + 0.15f;
            case TipoDePlano.Medio: return Mathf.Max(0.95f, 0.85f * alto) / (2f * tanV) + 0.3f;
            case TipoDePlano.Dos:
            {
                float sep = Plano(e.sujetos[0].PosEn(t) - e.sujetos[1].PosEn(t)).magnitude;
                return Mathf.Max((sep + 1.3f) / (2f * tanH), 1.6f / (2f * tanV)) + 0.3f;
            }
            case TipoDePlano.Conjunto:
            {
                float ancho = Extension(e, t) + 2.2f;
                return Mathf.Max(ancho / (2f * tanH), Mathf.Max(2.3f, Vertical(e, t) + 1.2f) / (2f * tanV));
            }
            case TipoDePlano.General:
            {
                float ancho = Mathf.Max(Extension(e, t) + 8f, 14f);
                return Mathf.Max(ancho / (2f * tanH), (Vertical(e, t) + 2.5f) / (2f * tanV));
            }
            default: return 4f;
        }
    }

    /// Alto que ocupa el grupo (alguien levitando o en lo alto de la colina cuenta).
    private static float Vertical(Encuadre e, float t)
    {
        if (e.sujetos.Count == 0) return 0f;
        float abajo = e.sujetos.Min(a => a.PosEn(t).y), arriba = e.sujetos.Max(a => a.PosEn(t).y + a.altura);
        foreach (var x in e.extras) { abajo = Mathf.Min(abajo, x.y - 1.5f); arriba = Mathf.Max(arriba, x.y + 1.5f); }
        return arriba - abajo;
    }

    private float Extension(Encuadre e, float t)
    {
        float max = 0f;
        for (int i = 0; i < e.sujetos.Count; i++)
            for (int j = i + 1; j < e.sujetos.Count; j++)
                max = Mathf.Max(max, Plano(e.sujetos[i].PosEn(t) - e.sujetos[j].PosEn(t)).magnitude);
        foreach (var x in e.extras)
            foreach (var a in e.sujetos) max = Mathf.Max(max, Plano(x - a.PosEn(t)).magnitude + 1.5f);
        return max;
    }

    /// Prueba 48 rumbos alrededor del foco y se queda con el que mejor se ve.
    private void Buscar(Encuadre e)
    {
        float tm = Mathf.Lerp(e.t0, e.t1, 0.35f);
        e.tRef = tm;
        Vector3 foco = Foco(e, tm);
        float distancia = DistanciaDeEncuadre(e, tm);

        // Rumbo de referencia: hacia dónde mira el sujeto (o la pareja).
        float referencia;
        if (e.tipo == TipoDePlano.Dos)
        {
            Vector3 ab = Plano(e.sujetos[1].PosEn(tm) - e.sujetos[0].PosEn(tm));
            Vector3 normal = Vector3.Cross(Vector3.up, ab).normalized;
            float ra = e.sujetos[0].RumboEn(tm, _actores), rb = e.sujetos[1].RumboEn(tm, _actores);
            Vector3 frente = Direccion(ra) + Direccion(rb);
            if (Vector3.Dot(frente, normal) < 0f) normal = -normal;
            referencia = Rumbo(normal);
        }
        else if (e.sujetos.Count > 0)
        {
            Vector3 suma = Vector3.zero;
            foreach (var s in e.sujetos) suma += Direccion(s.RumboEn(tm, _actores));
            referencia = suma.sqrMagnitude > 0.01f ? Rumbo(suma) : e.sujetos[0].RumboEn(tm, _actores);
        }
        else referencia = _anterior != null ? _anterior.azimut : 0f;

        float preferido = 0f;
        string lado = e.o.Op("lado");
        if (lado == "izq") preferido = -35f;
        else if (lado == "der") preferido = 35f;
        else if (lado == "espalda") preferido = 180f;
        else if (lado == "frente") preferido = 0f;
        else if (e.tipo == TipoDePlano.Medio || e.tipo == TipoDePlano.Primer || e.tipo == TipoDePlano.Detalle) preferido = 25f;
        if (e.o.opciones.ContainsKey("rumbo")) { referencia = e.o.OpF("rumbo", 0f); preferido = 0f; }

        float alturaCam = e.o.opciones.ContainsKey("altura") ? e.o.OpF("altura", 1.6f) : AlturaDeCamara(e);
        if (e.o.Tiene("picado")) alturaCam += 1.6f;
        if (e.o.Tiene("contrapicado")) alturaCam = Mathf.Max(0.5f, alturaCam - 0.9f);

        float mejorCoste = float.MaxValue;
        for (int k = 0; k < 48; k++)
        {
            float off = -180f + k * 7.5f;
            float az = referencia + off;
            Vector3 dir = Direccion(az);
            foreach (float escala in new[] { 1f, 0.85f, 1.2f })
            {
                Vector3 c = foco + dir * distancia * escala;
                Vector3 suelo = Suelo(new Vector3(c.x, foco.y, c.z));
                c.y = suelo.y + alturaCam;
                float coste = Coste(e, c, az, referencia, preferido, foco);
                coste += Mathf.Abs(escala - 1f) * 40f;
                if (coste < mejorCoste) { mejorCoste = coste; e.camara = c; e.azimut = az; e.desglose = _d.ToString(); }
            }
        }
        e.coste = mejorCoste;
    }

    private float AlturaDeCamara(Encuadre e)
    {
        float ojos = e.sujetos.Count > 0 ? e.sujetos.Average(a => a.AlturaDeOjos) : 1.5f;
        return e.tipo switch
        {
            TipoDePlano.General => 3.2f,
            TipoDePlano.Conjunto => 1.9f,
            TipoDePlano.Dos => ojos - 0.05f,
            _ => ojos - 0.04f,
        };
    }

    private float Coste(Encuadre e, Vector3 c, float az, float referencia, float preferido, Vector3 foco)
    {
        float coste = 0f;
        _d.vista = _d.tapado = _d.pegado = _d.delante = _d.estorbo = _d.cara = _d.angulo = _d.eje = 0f;
        if (Physics.CheckSphere(c, 0.3f, ~0, QueryTriggerInteraction.Ignore)) { _d.vista = 5000f; return 5000f; }
        var suelo = Suelo(c);
        if (c.y < suelo.y + 0.4f) { _d.vista = 5000f; return 5000f; }

        // Que la línea de visión no la corte el decorado.
        foreach (var s in e.sujetos.Count > 0 ? (e.tipo == TipoDePlano.Hombro ? e.sujetos.Skip(1).ToList() : e.sujetos) : new List<ActorH>())
        {
            Vector3 ojos = s.PosEn(e.t0) + Vector3.up * s.AlturaDeOjos;
            Vector3 pecho = s.PosEn(e.t0) + Vector3.up * s.altura * 0.6f;
            if (Linea(c, ojos)) coste += 1000f;
            if (Linea(c, pecho)) coste += 250f;
        }
        if (e.sujetos.Count == 0 && e.focoFijo != null && Linea(c, e.focoFijo.Value)) coste += 1000f;
        _d.vista = coste;

        // Que nadie se cruce delante ni se pegue a la cámara.
        var sujetosVistos = e.tipo == TipoDePlano.Hombro ? new HashSet<ActorH> { e.sujetos[1] } : new HashSet<ActorH>(e.sujetos);
        int muestras = 0; float tapado = 0f; float pegado = 0f; float delante = 0f;
        Vector3 haciaFoco = foco - c;
        var giroC = Quaternion.LookRotation(haciaFoco.sqrMagnitude > 1e-4f ? haciaFoco.normalized : Vector3.forward, Vector3.up);
        var invC = Quaternion.Inverse(giroC);
        float tanVc = Mathf.Tan(e.lente * 0.5f * Mathf.Deg2Rad), tanHc = tanVc * 16f / 9f;
        for (float t = e.t0; t <= e.t1 + 1e-3f; t += Mathf.Max(0.4f, (e.t1 - e.t0) / 8f))
        {
            muestras++;
            foreach (var b in _actores.Values)
            {
                if (sujetosVistos.Contains(b) || !b.VisibleEn(t)) continue;
                if (e.tipo == TipoDePlano.Hombro && b == e.sujetos[0]) continue;
                Vector3 pb = b.PosEn(t);
                if (Plano(pb - c).magnitude < 1.1f + b.Radio) pegado += 1f;
                // Alguien más cerca que el sujeto metido en el cuadro (de espaldas o de lado).
                float hastaSujeto = sujetosVistos.Count > 0 ? sujetosVistos.Min(x => Plano(x.PosEn(t) - c).magnitude) : Plano(foco - c).magnitude;
                float hastaB = Plano(pb - c).magnitude;
                if (hastaB < hastaSujeto - 0.3f)
                {
                    var caja = new Bounds(pb + Vector3.up * b.altura * 0.5f, new Vector3(b.Radio * 2f, b.altura, b.Radio * 2f));
                    var lb = invC * (pb - c);
                    if (lb.z > -b.Radio) delante += AreaEnCuadro(caja, c, invC, tanHc * 1.3f, tanVc);
                }
                foreach (var s in sujetosVistos)
                {
                    Vector3 objetivo = s.PosEn(t) + Vector3.up * s.AlturaDeOjos;
                    if (Tapa(c, objetivo, pb, b.altura, b.Radio)) { tapado += 1f; break; }
                }
                if (sujetosVistos.Count == 0 && e.focoFijo != null && Tapa(c, e.focoFijo.Value, pb, b.altura, b.Radio)) tapado += 0.5f;
            }
            // Que un sujeto no tape a otro (en «dos» y conjuntos).
            if (sujetosVistos.Count > 1)
                foreach (var s1 in sujetosVistos)
                    foreach (var s2 in sujetosVistos)
                        if (s1 != s2 && s1.VisibleEn(t) && Tapa(c, s2.PosEn(t) + Vector3.up * s2.AlturaDeOjos, s1.PosEn(t), s1.altura, s1.Radio))
                            tapado += 0.5f;
            // Que los propios sujetos no se echen encima de la cámara.
            if (!Acompana(e.tipo))
            foreach (var s in sujetosVistos)
            {
                if (!s.VisibleEn(t)) continue;
                if (Plano(s.PosEn(t) - c).magnitude < 0.75f + s.Radio) pegado += 1f;
            }
        }
        if (muestras > 0)
        {
            _d.tapado = 1100f * tapado / muestras;
            _d.pegado = 900f * pegado / muestras;
            float d = delante / muestras;
            if (d > 0.005f) _d.delante = 200f + 3000f * Mathf.Min(1f, d);
            coste += _d.tapado + _d.pegado + _d.delante;
        }

        // Caras: se tiene que ver la cara del que importa.
        float peor = 0f;
        foreach (var s in sujetosVistos)
        {
            float rumbo = s.RumboEn(e.tRef, _actores);
            float angulo = Mathf.Abs(Mathf.DeltaAngle(rumbo, Rumbo(c - s.PosEn(e.tRef))));
            peor = Mathf.Max(peor, angulo);
        }
        if (e.tipo == TipoDePlano.General) _d.cara = Mathf.Max(0f, peor - 80f) * 1.5f;
        else if (e.tipo == TipoDePlano.Conjunto) _d.cara = Mathf.Max(0f, peor - 65f) * 3f;
        else if (e.o.Op("lado") != "espalda")
            _d.cara = Mathf.Max(0f, peor - 40f) * 5f + (peor > 95f ? 500f : 0f);
        coste += _d.cara;

        // Nada pegado a la cámara metido en el cuadro (farolas, esquinas, carteles).
        float estorbo = Estorbo(c, foco, e.lente, e.tipo == TipoDePlano.General);
        if (estorbo > 0f) _d.estorbo = e.tipo == TipoDePlano.General ? 120f + 1200f * estorbo : 250f + 2500f * estorbo;
        coste += _d.estorbo;

        // Preferencia de ángulo (tres cuartos) y lado pedido.
        _d.angulo = Mathf.Abs(Mathf.DeltaAngle(az, referencia + preferido)) * 0.6f;
        coste += _d.angulo;
        float antesDelEje = coste;

        // Eje: no cruzar la línea entre dos que hablan (salvo «cruza»).
        if (_anterior != null && !e.o.Tiene("cruza"))
        {
            var comunes = _anterior.sujetos.Intersect(e.sujetos).ToList();
            var pareja = _anterior.sujetos.Union(e.sujetos).Distinct().Take(2).ToList();
            if (pareja.Count == 2 && (comunes.Count > 0 || _anterior.tipo == TipoDePlano.Dos))
            {
                Vector3 a = pareja[0].PosEn(e.t0), b = pareja[1].PosEn(e.t0);
                float ladoAhora = Mathf.Sign(Vector3.Cross(Plano(b - a), Plano(c - a)).y);
                float ladoAntes = Mathf.Sign(Vector3.Cross(Plano(b - a), Plano(_anterior.camara - a)).y);
                if (ladoAhora != ladoAntes) coste += 350f;
            }
            // 30°: mismo sujeto y mismo tamaño de plano necesita cambiar de ángulo.
            if (comunes.Count > 0 && _anterior.tipo == e.tipo && Mathf.Abs(Mathf.DeltaAngle(_anterior.azimut, az)) < 30f)
                coste += 260f;
        }
        _d.eje = coste - antesDelEje;
        return coste;
    }

    private List<Bounds> _cajas;

    /// Cajas de todo lo que se ve en la escena (muchas farolas y carteles no tienen collider).
    private List<Bounds> Cajas()
    {
        if (_cajas != null) return _cajas;
        _cajas = new List<Bounds>();
        for (int k = 0; k < UnityEngine.SceneManagement.SceneManager.sceneCount; k++)
        {
          var escena = UnityEngine.SceneManagement.SceneManager.GetSceneAt(k);
          if (!escena.isLoaded) continue;
          foreach (var raiz in escena.GetRootGameObjects())
            foreach (var r in raiz.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var b = r.bounds;
                if (b.size.y < 0.4f) continue;                          // suelo, alfombras, charcos
                if (Mathf.Max(b.size.x, b.size.z) > 14f) continue;      // terreno, río
                _cajas.Add(b);
            }
        }
        return _cajas;
    }

    /// Fracción del cuadro que tapa algo del decorado cerca de la cámara (farolas, esquinas,
    /// tejados). Se proyecta la caja de cada objeto cercano que entra en el encuadre; así no se
    /// escapan las cosas finas como una farola entre dos rayos.
    private float Estorbo(Vector3 c, Vector3 foco, float lente, bool general = false)
    {
        Vector3 d = foco - c;
        float dist = d.magnitude;
        // Todo lo que queda entre la cámara y el sujeto (hasta 1 m antes de él): una farola a
        // 5 m con una lente de 34 ya es una barra que corta el cuadro de arriba abajo.
        float cerca = general ? Mathf.Min(dist * 0.25f, 6f) : dist - 1.0f;
        if (cerca < 0.3f) return 0f;
        var giro = Quaternion.LookRotation(d / dist, Vector3.up);
        var inv = Quaternion.Inverse(giro);
        float tanV = Mathf.Tan(lente * 0.5f * Mathf.Deg2Rad), tanH = tanV * 16f / 9f;
        var vista = Matrix4x4.TRS(c, giro, new Vector3(1f, 1f, -1f)).inverse;
        var planos = GeometryUtility.CalculateFrustumPlanes(Matrix4x4.Perspective(lente, 16f / 9f, 0.05f, cerca) * vista);
        float cubierto = 0f;
        foreach (var b in Cajas())
        {
            if (b.SqrDistance(c) > cerca * cerca) continue;
            if (!GeometryUtility.TestPlanesAABB(planos, b)) continue;
            cubierto += Mathf.Max(AreaEnCuadro(b, c, inv, tanH, tanV), 0.02f);
        }
        return Mathf.Min(1f, cubierto);
    }

    private static readonly Vector3[] s_esquinas = new Vector3[8];

    /// Fracción de la pantalla (0..1) que ocupa una caja vista desde c (ya sabiendo que entra).
    private static float AreaEnCuadro(Bounds b, Vector3 c, Quaternion inv, float tanH, float tanV)
    {
        Vector3 mn = b.min, mx = b.max;
        int k = 0;
        for (int ix = 0; ix < 2; ix++) for (int iy = 0; iy < 2; iy++) for (int iz = 0; iz < 2; iz++)
            s_esquinas[k++] = new Vector3(ix == 0 ? mn.x : mx.x, iy == 0 ? mn.y : mx.y, iz == 0 ? mn.z : mx.z);
        float x0 = 1f, x1 = -1f, y0 = 1f, y1 = -1f;
        bool detras = false, alguna = false;
        foreach (var q in s_esquinas)
        {
            var l = inv * (q - c);
            if (l.z < 0.05f) { detras = true; continue; }
            alguna = true;
            float nx = Mathf.Clamp(l.x / (l.z * tanH), -1f, 1f), ny = Mathf.Clamp(l.y / (l.z * tanV), -1f, 1f);
            x0 = Mathf.Min(x0, nx); x1 = Mathf.Max(x1, nx); y0 = Mathf.Min(y0, ny); y1 = Mathf.Max(y1, ny);
        }
        if (!alguna) return 0f;
        float area = x1 > x0 && y1 > y0 ? (x1 - x0) * (y1 - y0) / 4f : 0f;
        if (detras) area = Mathf.Max(area, 0.3f);    // la cámara está casi dentro: tapa mucho
        return area;
    }

    /// Planos cortos que acompañan a sus sujetos cuando estos andan (paseo hablando, etc.).
    private static bool Acompana(TipoDePlano t) =>
        t is TipoDePlano.Medio or TipoDePlano.Primer or TipoDePlano.Detalle or TipoDePlano.Hombro or TipoDePlano.Dos;

    /// Si los sujetos se desplazan durante el plano, la cámara se desplaza con ellos (suavizado):
    /// el encuadre se mantiene en vez de quedarse mirando cómo se van o se le echan encima.
    private void Acompanar(Encuadre e, PlanoHorneado plano)
    {
        if (e.sujetos.Count == 0) return;
        Vector3 Centro(float t)
        {
            Vector3 s = Vector3.zero;
            foreach (var a in e.sujetos) s += a.PosEn(t);
            return Plano(s / e.sujetos.Count);
        }
        Vector3 cRef = Centro(e.tRef);
        float maxMov = 0f;
        for (float t = e.t0; t <= e.t1 + 1e-3f; t += 0.25f) maxMov = Mathf.Max(maxMov, (Centro(t) - cRef).magnitude);
        if (maxMov < 0.8f) return;
        float alto = e.camara.y - Suelo(e.camara).y;
        var crudas = new List<Vector3>();
        var tiempos = new List<float>();
        for (float t = e.t0; t <= e.t1 + 1e-3f; t += 0.25f)
        {
            Vector3 p = e.camara + (Centro(t) - cRef);
            p.y = Suelo(p).y + alto;
            crudas.Add(p); tiempos.Add(t);
        }
        plano.posiciones.Clear();
        for (int i = 0; i < crudas.Count; i++)
        {
            Vector3 s = Vector3.zero; int n = 0;
            for (int j = Mathf.Max(0, i - 3); j <= Mathf.Min(crudas.Count - 1, i + 3); j++) { s += crudas[j]; n++; }
            plano.posiciones.Add(new ClaveDePosicion(tiempos[i], s / n));
        }
    }

    private static bool Linea(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        float largo = d.magnitude;
        if (largo < 0.3f) return false;
        // Se ignoran los últimos 40 cm (el propio personaje no está en la escena del Editor, pero
        // sí puede haber un prop pegado a él).
        return Physics.Raycast(a, d / largo, largo - 0.4f, ~0, QueryTriggerInteraction.Ignore);
    }

    /// ¿Un personaje de pie en pb (alto h) se mete entre la cámara y el objetivo?
    private static bool Tapa(Vector3 cam, Vector3 objetivo, Vector3 pb, float h, float radio)
    {
        Vector3 d = objetivo - cam;
        Vector3 uh = Plano(d);
        float largoH = uh.magnitude;
        if (largoH < 0.5f) return false;
        uh /= largoH;
        float sH = Vector3.Dot(Plano(pb - cam), uh);
        if (sH < 0.3f || sH > largoH - 0.35f) return false;
        Vector3 p = cam + d * (sH / largoH);
        float dh = Plano(p - pb).magnitude;
        float y = p.y - pb.y;
        return dh < radio && y > -0.1f && y < h + 0.1f;
    }

    /// Contraplano sobre el hombro. Se calcula la geometría exacta para que A quede en el borde
    /// del cuadro (a un 37 % del centro) y B en plano medio con aire hacia A. Con chibis de
    /// cabeza enorme es la única forma de que A no se coma el plano.
    private void Hombro(Encuadre e)
    {
        var a = e.sujetos[0]; var b = e.sujetos[1];
        e.tRef = e.t0;
        Vector3 pa = a.PosEn(e.t0), pb = b.PosEn(e.t0);
        Vector3 ba = Plano(pa - pb);
        float s = ba.magnitude;
        if (s < 0.3f) { Buscar(e); return; }
        ba /= s;
        float tanV = Mathf.Tan(e.lente * 0.5f * Mathf.Deg2Rad), tanH = tanV * 16f / 9f;
        float dB = e.o.opciones.ContainsKey("distancia") ? e.o.OpF("distancia", 2.8f)
                 : Mathf.Max(Mathf.Max(0.95f, 0.85f * b.altura) / (2f * tanV) + 0.3f, s + 1.2f);
        // Ángulo A-cámara-B: el borde interior de la cabeza de A al 60 % del medio cuadro y B
        // con su aire. Con lente larga (24°) y cámara lejos, la cabeza de A no se come el plano.
        float dA = Mathf.Max(1f, dB - s);
        float alfa = Mathf.Atan(0.6f * tanH) + Mathf.Asin(Mathf.Clamp01(a.Radio / dA)) + Mathf.Atan(0.26f * tanH);

        string lado = e.o.Op("lado");
        var signos = lado == "der" ? new[] { 1f } : lado == "izq" ? new[] { -1f } : new[] { 1f, -1f };
        float mejor = float.MaxValue;
        foreach (float signo in signos)
            foreach (float escala in new[] { 1f, 1.15f, 0.9f })
            {
                float d = dB * escala;
                float angA = Mathf.PI - Mathf.Asin(Mathf.Clamp(d * Mathf.Sin(alfa) / s, -1f, 1f));
                float beta = Mathf.Max(0.02f, Mathf.PI - angA - alfa);              // ángulo en B
                Vector3 dir = Quaternion.AngleAxis(signo * beta * Mathf.Rad2Deg, Vector3.up) * ba;
                Vector3 c = pb + dir * d;
                // A la altura de la coronilla de A: su cabeza queda abajo, en el borde.
                c.y = Suelo(c).y + Mathf.Max(a.altura * 0.92f, b.AlturaDeOjos);
                float az = Rumbo(c - pb);
                float coste = Coste(e, c, az, az, 0f, Foco(e, e.t0)) + Mathf.Abs(escala - 1f) * 60f;
                if (coste < mejor) { mejor = coste; e.camara = c; e.azimut = az; e.desglose = _d.ToString(); }
            }
        e.coste = mejor;
        if (mejor >= 1000f)
        {
            // No hay sitio para el contraplano (pared, cartel, río): plano medio de B.
            Aviso($"{e.o}: no cabe el contraplano sobre el hombro de {a.alias}; hago un plano medio de {b.alias}.");
            e.tipo = TipoDePlano.Medio;
            e.sujetos.RemoveAt(0);
            Buscar(e);
        }
    }

    /// Travelling: la cámara acompaña de lado al sujeto, con el camino ya suavizado.
    private void Seguir(Encuadre e, PlanoHorneado plano)
    {
        var a = e.sujetos[0];
        float lado = e.o.Op("lado") == "izq" ? -1f : 1f;
        float distancia = e.o.OpF("distancia", 4.2f);
        float alto = e.o.OpF("altura", 1.7f);
        var crudas = new List<Vector3>();
        var tiempos = new List<float>();
        Vector3 ultimaDir = Direccion(a.RumboEn(e.t0, _actores));
        for (float t = e.t0; t <= e.t1 + 1e-3f; t += 0.25f)
        {
            Vector3 v = a.VelEn(t);
            if (v.sqrMagnitude > 0.04f) ultimaDir = Vector3.Slerp(ultimaDir, Plano(v).normalized, 0.35f);
            Vector3 lateral = Vector3.Cross(Vector3.up, ultimaDir) * lado;
            Vector3 c;
            string modo = e.o.Op("lado");
            if (modo == "espalda") c = a.PosEn(t) - ultimaDir * distancia + Vector3.Cross(Vector3.up, ultimaDir) * 0.6f;
            else if (modo == "frente") c = a.PosEn(t) + ultimaDir * distancia + Vector3.Cross(Vector3.up, ultimaDir) * 0.4f;
            else c = a.PosEn(t) + lateral * distancia - ultimaDir * distancia * 0.35f;
            c.y = Suelo(c).y + alto;
            crudas.Add(c); tiempos.Add(t);
        }
        // Suavizado (media de 1,5 s): el travelling no puede dar tirones.
        for (int i = 0; i < crudas.Count; i++)
        {
            Vector3 s = Vector3.zero; int n = 0;
            for (int j = Mathf.Max(0, i - 3); j <= Mathf.Min(crudas.Count - 1, i + 3); j++) { s += crudas[j]; n++; }
            plano.posiciones.Add(new ClaveDePosicion(tiempos[i], s / n));
        }
        e.camara = plano.posiciones[0].p;
        e.azimut = Rumbo(e.camara - a.PosEn(e.t0));
        int tapados = 0;
        foreach (var k in plano.posiciones)
            if (Linea(k.p, a.PosEn(k.t) + Vector3.up * a.AlturaDeOjos)) tapados++;
        e.coste = tapados * 1000f / Mathf.Max(1, plano.posiciones.Count);
        if (tapados > plano.posiciones.Count / 4)
            Aviso($"{e.o}: en el travelling el decorado tapa a {a.alias} un {100 * tapados / plano.posiciones.Count}% del tiempo. Prueba lado=izq o distancia=.");
    }

    /// Dónde queda el foco en pantalla: con aire hacia donde mira el sujeto, ojos en el tercio alto.
    private Vector2 Composicion(Encuadre e, List<ActorH> foco)
    {
        string encuadre = e.o.Op("encuadre");
        float y = e.tipo is TipoDePlano.Medio or TipoDePlano.Primer or TipoDePlano.Detalle or TipoDePlano.Hombro ? -0.1f : 0f;
        if (e.aireArriba) y = -0.16f;
        // Las bandas recortan arriba y abajo: el tercio alto se mide sobre lo que se ve.
        y *= Mathf.Max(0.3f, 1f - 2f * e.banda);
        if (encuadre == "centro") return new Vector2(0f, y);
        if (encuadre == "izq") return new Vector2(-0.14f, y);
        if (encuadre == "der") return new Vector2(0.14f, y);
        if (foco.Count != 1 || e.tipo == TipoDePlano.General || e.tipo == TipoDePlano.Conjunto) return new Vector2(0f, y);
        var s = foco[0];
        float tm = Mathf.Lerp(e.t0, e.t1, 0.3f);
        Vector3 haciaFoco = Plano(s.PosEn(tm) - e.camara).normalized;
        Vector3 derecha = Vector3.Cross(Vector3.up, haciaFoco);
        float mira = Vector3.Dot(Direccion(s.RumboEn(tm, _actores)), derecha);
        float x = e.tipo == TipoDePlano.Primer || e.tipo == TipoDePlano.Detalle ? 0.09f : 0.13f;
        // Si mira a la derecha de la pantalla, el sujeto se pone a la izquierda (aire delante).
        return new Vector2(Mathf.Abs(mira) < 0.2f ? 0f : (mira > 0f ? -x : x), y);
    }

    private string Describir(Encuadre e)
    {
        string quien = e.sujetos.Count > 0 ? string.Join(" y ", e.sujetos.Select(s => s.alias)) : "el punto";
        return e.tipo switch
        {
            TipoDePlano.General => $"general de {quien}",
            TipoDePlano.Conjunto => $"conjunto de {quien}",
            TipoDePlano.Medio => $"medio de {quien}",
            TipoDePlano.Primer => $"primer plano de {quien}",
            TipoDePlano.Detalle => $"detalle de {quien}",
            TipoDePlano.Dos => $"dos: {quien}",
            TipoDePlano.Hombro => $"sobre el hombro de {e.sujetos[0].alias} a {e.sujetos[1].alias}",
            TipoDePlano.Sigue => $"siguiendo a {quien}",
            _ => $"fijo a {quien}",
        };
    }

    private bool PosDeProp(string id, out Vector3 p)
    {
        p = default;
        if (_stage == null) return false;
        foreach (var prop in _stage.Props)
            if (prop.target != null && string.Equals(prop.id, id, System.StringComparison.OrdinalIgnoreCase)) { p = prop.target.position; return true; }
        return false;
    }

    private static Vector3 Direccion(float rumbo) => new(Mathf.Sin(rumbo * Mathf.Deg2Rad), 0f, Mathf.Cos(rumbo * Mathf.Deg2Rad));
    private static float Rumbo(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
}
#endif
