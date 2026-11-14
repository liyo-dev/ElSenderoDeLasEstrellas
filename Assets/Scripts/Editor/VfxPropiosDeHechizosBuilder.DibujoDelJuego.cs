using UnityEngine;
using static VfxProcedural;

/// Tipos genéricos de efecto de dibujo para lo que el juego usa fuera de los hechizos (destellos,
/// brillos, confeti, humo, golpes, círculos, portales…). Se montan a tamaño 1 (radio de un metro,
/// más o menos) y quien los usa los escala; el color tiñe las piezas crema. Los usa
/// VfxDelJuegoADibujo. Ver INC-1127
public static partial class VfxPropiosDeHechizosBuilder
{
    public static void DibujoGenerico(string tipo, GameObject root, Texturas t, Color c)
    {
        var p = root.transform;
        switch (tipo)
        {
            case "Destello":
                Tintar(Pop(p, t, "Destello", "destello", 1.6f, 0.2f, 1f), c);
                Tintar(Pop(p, t, "Onda", "anillo", 1f, 0.32f, 2.4f), c);
                Tintar(Estrellitas(p, t, "Estrellitas", 6, 0.2f, 3.5f, false), c);
                LuzQueSeApaga(root, c, 2f, 3f);
                break;
            case "Brillo":
                Centelleo(p, t, "Estrellas", c, 1f, 18f, "estrella", 0.18f);
                Centelleo(p, t, "Destellitos", c, 0.9f, 3f, "destello", 0.25f);
                Luz(p, "Luz", c, 1f, 2f);
                break;
            case "Chispitas":
            {
                var ps = Brotes(p, t, "Estrellitas que suben", "estrella", 0.6f, 1f, 10f, 0.25f, 1f, c, 0.7f);
                Forma(ps, ParticleSystemShapeType.Sphere, 0.6f, 1f);
                Girar(ps, 180f);
                break;
            }
            case "Confeti":
            {
                Color[] colores = { new Color(1f, 0.62f, 0.70f), new Color(1f, 0.84f, 0.38f), new Color(0.62f, 0.92f, 0.80f), new Color(0.70f, 0.58f, 1f) };
                for (int i = 0; i < colores.Length; i++)
                {
                    var ps = Tintar(Estallido(p, t, "Confeti " + (i + 1), i % 2 == 0 ? "petalo" : "estrella", 8, 6f, 0.3f, 1.4f), colores[i]);
                    ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;
                    var m = ps.main;
                    m.gravityModifier = 0.8f;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.6f);
                    Girar(ps, 400f);
                    Forma(ps, ParticleSystemShapeType.Cone, 0.2f, 1f);
                    var f = ps.shape;
                    f.angle = 35f;
                    f.rotation = new Vector3(-90f, 0f, 0f);
                    Golpe(ps, 0.05f, 0.8f);
                }
                break;
            }
            case "Humo":
                Tintar(Bocanadas(p, t, "Humo", 8, 1.2f, 0.8f), c);
                break;
            case "Polvo":
            {
                Tintar(Bocanadas(p, t, "Bocanadas", 6, 1.1f, 0.9f), c);
                Brotes(p, t, "Polvo del suelo", "humo", 1f, 0.4f, 8f, 0.9f, 1f, c, 0.5f);
                break;
            }
            case "Chispas":
                Tintar(Pop(p, t, "Destello", "destello", 0.9f, 0.15f, 1f), c);
                Tintar(Estallido(p, t, "Chispas", "estrella", 10, 5f, 0.35f, 0.4f), c);
                break;
            case "Golpe":
                // Una forma principal (el destello) y un acompañamiento (estrellas); la onda, fina.
                Tintar(Pop(p, t, "Destello", "destello", 1.6f, 0.2f, 1f), c);
                Tintar(Pop(p, t, "Onda", "anillo", 1.2f, 0.32f, 2.4f), c);
                Tintar(Estallido(p, t, "Estrellas", "estrella", 7, 6f, 0.3f, 0.45f), c);
                LuzQueSeApaga(root, c, 3f, 4f);
                break;
            case "GolpeDePiedra":
                PolvoDePiedra(root, t, 1f);
                break;
            case "Oscuro":
                Tintar(Pop(p, t, "Destello", "destello", 1.4f, 0.2f, 1f), c);
                Tintar(Bocanadas(p, t, "Humo negro", 4, 0.7f, 0.8f), TOscuro);
                Tintar(Estallido(p, t, "Chispas", "estrella", 6, 5f, 0.25f, 0.4f), c);
                LuzQueSeApaga(root, c, 2f, 3f);
                break;
            case "Circulo":
            {
                Suelo(p, t, "Runa", "runa", 2f, c, 30f);
                Suelo(p, t, "Borde", "anillo", 2.3f, c, 0f, 0.03f);
                Brotes(p, t, "Estrellas que suben", "estrella", 0.9f, 1f, 10f, 0.18f, 0.8f, c, 1.4f);
                Luz(p, "Luz", c, 1.5f, 1.6f).transform.localPosition = Vector3.up * 0.4f;
                break;
            }
            case "Tajo":
                for (int i = 0; i < 3; i++)
                {
                    var g = Tintar(Pop(p, t, "Tajo " + (i + 1), "garra", 1.4f, 0.3f, 1.2f), c);
                    g.transform.localPosition = new Vector3((i - 1) * 0.3f, 0f, 0f);
                    var m = g.main;
                    m.startDelay = i * 0.05f;
                }
                Tintar(Estrellitas(p, t, "Estrellitas", 6, 0.3f, 4f, false), c);
                break;
            case "Columna":
            {
                var haz = Pieza(p, t, "Columna", "haz");
                var mh = haz.main;
                mh.startLifetime = 0.2f;
                mh.startSize3D = true;
                mh.startSizeX = new ParticleSystem.MinMaxCurve(1.8f, 2.1f);
                mh.startSizeY = 8f;
                mh.startSizeZ = 1f;
                mh.startColor = c;
                mh.maxParticles = 3;
                Emitir(haz, 12f);
                haz.transform.localPosition = Vector3.up * 4f;
                haz.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
                Suelo(p, t, "Base", "anillo", 1.6f, c, 0f);
                Brotes(p, t, "Estrellas que suben", "estrella", 0.5f, 1f, 12f, 0.25f, 1.2f, c, 2.5f);
                break;
            }
            case "Llama":
                Cuerpo(p, t, "Llama", "llama", 1f, Color.white);
                Rastro(p, t, "Llamitas", "llama", 0.35f, 8f, Color.white);
                Brotes(p, t, "Chispitas", "estrella", 0.25f, 1f, 4f, 0.15f, 0.6f, TOro, 1.2f);
                Luz(p, "Luz", new Color(1f, 0.62f, 0.30f), 1f, 3f);
                break;
            case "Onda":
            {
                var onda = Tintar(Pop(p, t, "Onda", "anillo", 0.6f, 0.5f, 4.5f), c);
                onda.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                onda.transform.localPosition = Vector3.up * 0.1f;
                Tintar(Bocanadas(p, t, "Polvo", 8, 0.9f, 0.6f), c);
                break;
            }
            case "Implosion":
            {
                var aro = Pieza(p, t, "Aro que se cierra", "anillo", 0.6f, true);
                var m = aro.main;
                m.startLifetime = 0.6f;
                m.startSize = 2.4f;
                m.startColor = c;
                m.maxParticles = 2;
                Emitir(aro, 2f);
                Rafaga(aro, 1);
                Emitir(aro, 2f);
                var tam = aro.sizeOverLifetime;
                tam.enabled = true;
                tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
                var estrellas = Pieza(p, t, "Estrellas que caen dentro", "estrella");
                var me = estrellas.main;
                me.startLifetime = 0.5f;
                me.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
                me.startSpeed = -2.4f;
                me.startColor = c;
                Emitir(estrellas, 16f);
                Forma(estrellas, ParticleSystemShapeType.Sphere, 1.2f, 0f);
                Golpe(estrellas, 0.15f, 0.7f);
                Cuerpo(p, t, "Centro", "destello", 0.6f, c, 30f);
                break;
            }
            case "Portal":
            {
                var esp = Pieza(p, t, "Espiral", "espiral");
                var m = esp.main;
                m.startLifetime = 2f;
                m.startSize = 2.2f;
                m.startColor = c;
                m.maxParticles = 2;
                ConUnaAlEmpezar(esp, 1f);
                Girar(esp, -200f);
                var col = esp.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(g);
                var aro = Pieza(p, t, "Aro", "anillo");
                var ma = aro.main;
                ma.startLifetime = 2f;
                ma.startSize = 2.5f;
                ma.startColor = c;
                ma.maxParticles = 2;
                ConUnaAlEmpezar(aro, 1f);
                var colA = aro.colorOverLifetime;
                colA.enabled = true;
                colA.color = new ParticleSystem.MinMaxGradient(g);
                var est = Pieza(p, t, "Estrellas que entran", "estrella");
                var me = est.main;
                me.startLifetime = 0.6f;
                me.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
                me.startSpeed = -1.8f;
                me.startColor = c;
                Emitir(est, 14f);
                Forma(est, ParticleSystemShapeType.Circle, 1.3f, 0f);
                var f = est.shape;
                f.rotation = Vector3.zero;
                Golpe(est, 0.15f, 0.7f);
                break;
            }
            case "Remolino":
            {
                var esp = Cuerpo(p, t, "Espiral", "espiral", 1.6f, c, 0f);
                Girar(esp, -500f);
                var rem = Brotes(p, t, "Remolinos", "remolino", 0.8f, 1f, 12f, 0.7f, 0.5f, Color.white, 1f);
                Girar(rem, -300f);
                Brotes(p, t, "Estrellitas", "estrella", 0.9f, 1f, 8f, 0.2f, 0.6f, c, 1.5f);
                break;
            }
            case "Curacion":
                Brotes(p, t, "Corazones", "corazon", 0.7f, 1f, 6f, 0.35f, 1.1f, TRosa, 1.4f);
                Brotes(p, t, "Hojas", "hoja", 0.8f, 1f, 5f, 0.22f, 1.2f, Color.white, 1f);
                Brotes(p, t, "Estrellitas", "estrella", 0.8f, 1f, 8f, 0.2f, 0.8f, c, 1.6f);
                Suelo(p, t, "Espiral", "espiral", 1.8f, c, -60f);
                break;
            case "Velocidad":
            {
                Tintar(Estallido(p, t, "Líneas", "linea_viento", 10, 7f, 0.8f, 0.3f), c);
                Tintar(Pop(p, t, "Destello", "destello", 1f, 0.15f, 1f), c);
                break;
            }
            case "Meteoro": ZonaMeteoro(root, t, 1f); break;
            case "Juicio": ZonaJuicio(root, t, 1f); break;
            case "Veneno":
                Suelo(p, t, "Charco", "anillo", 2f, c, 0f);
                Brotes(p, t, "Burbujas", "burbuja", 0.9f, 1f, 8f, 0.3f, 1f, c, 0.8f);
                Brotes(p, t, "Vaho", "humo", 0.8f, 1f, 5f, 0.8f, 1.2f, c, 0.5f);
                break;
            case "Estrellas":
                Suelo(p, t, "Borde", "anillo", 2f, c, 0f);
                Brotes(p, t, "Estrellas", "estrella", 0.9f, 1f, 16f, 0.35f, 0.7f, c, 1.6f);
                break;
            case "Orbe":
                Cuerpo(p, t, "Núcleo", "destello", 1f, c, 30f);
                Golpe(Cuerpo(p, t, "Aro", "anillo", 1.4f, TLila, 0f), 0.3f, 0.6f);
                Rastro(p, t, "Estrellitas", "estrella", 0.3f, 12f, c, 90f);
                Luz(p, "Luz", c, 1f, 2f);
                break;
            case "Chimenea":
            {
                var humo = Brotes(p, t, "Humo que sube", "humo", 0.3f, 1f, 3f, 1f, 2.2f, c, 1.2f);
                var tam = humo.sizeOverLifetime;
                tam.enabled = true;
                tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 0.7f), new Keyframe(0.8f, 1.2f), new Keyframe(1f, 0f)));
                break;
            }
            default:
                Debug.LogWarning("[VFX] Tipo de dibujo desconocido: " + tipo);
                break;
        }
    }

    /// Estrellas (o destellitos) que se encienden y se apagan dentro de una esfera.
    private static ParticleSystem Centelleo(Transform p, Texturas t, string nombre, Color c, float radio, float ritmo, string pieza = "estrella", float tam = 0.3f)
    {
        var ps = Pieza(p, t, nombre, pieza);
        var m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        m.startSize = new ParticleSystem.MinMaxCurve(tam * 0.5f, tam);
        m.startColor = c;
        ConUnaAlEmpezar(ps, ritmo, Mathf.Max(1, Mathf.RoundToInt(ritmo * 0.3f)));
        Forma(ps, ParticleSystemShapeType.Sphere, radio, 1f);
        GiroAlAzar(ps, 45f);
        Girar(ps, 90f);
        Golpe(ps, 0.25f, 0.6f);
        return ps;
    }

    /// Emite a ritmo y, además, suelta algunas al empezar, para que no tarde en verse.
    private static void ConUnaAlEmpezar(ParticleSystem ps, float ritmo, int alEmpezar = 1)
    {
        Rafaga(ps, alEmpezar);
        Emitir(ps, ritmo);
    }

    /// Luz que se enciende con un golpe y se apaga sola: la magia ilumina caras y suelo.
    private static void LuzQueSeApaga(GameObject root, Color c, float intensidad, float alcance)
    {
        var luz = Luz(root.transform, "Luz del golpe", c, intensidad, alcance);
        var fb = root.AddComponent<VfxImpactFeedback>();
        fb.shakeIntensity = 0f;
        fb.shakeDuration = 0f;
        fb.enableScreenFlash = false;
        fb.enableHitStop = false;
        fb.enableRumble = false;
        fb.dynamicLight = luz;
        fb.lightFadeDuration = 0.35f;
    }
}
