namespace Game.NPC.Combate
{
    /// <summary>
    /// Una cosa que el NPC puede hacer en combate. El cerebro puntúa todas en cada decisión (0 = no
    /// tiene sentido ahora, 1 = es lo que toca) y ejecuta la mejor; mientras se ejecuta, se actualiza
    /// cada fotograma y el cerebro puede cambiarla por otra mucho mejor si
    /// <see cref="PuedeInterrumpirse"/> lo permite. Cada opción vive en su clase y solo habla con
    /// el cuerpo a través de <see cref="ICombatiente"/>.
    /// </summary>
    public abstract class OpcionDeCombate
    {
        protected readonly ICombatiente Cuerpo;

        protected OpcionDeCombate(ICombatiente cuerpo) => Cuerpo = cuerpo;

        public abstract string Nombre { get; }

        /// <summary>De 0 a 1: cuánto conviene ahora.</summary>
        public abstract float Puntuar(in SituacionDeCombate s);

        /// <summary>Se puede cambiar por otra a medias (no en pleno ataque ni en plena esquiva).</summary>
        public virtual bool PuedeInterrumpirse => true;

        public abstract void Empezar(in SituacionDeCombate s);

        /// <summary>Cada fotograma mientras se ejecuta. Devuelve false cuando ha terminado.</summary>
        public abstract bool Actualizar(in SituacionDeCombate s);

        /// <summary>Al terminar o al cambiarla por otra: deja el cuerpo como estaba.</summary>
        public virtual void Terminar() { }
    }
}
