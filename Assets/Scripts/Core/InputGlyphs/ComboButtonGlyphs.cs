namespace Core.InputGlyphs
{
    /// <summary>
    /// Glifo de cada botón de combo (<see cref="ComboButton"/>) y su texto en el dispositivo activo.
    /// Única traducción ComboButton → botón del mando: la usan el panel del combo, el grimorio, la
    /// pestaña Hechizos y el aviso de hechizo aprendido.
    /// </summary>
    public static class ComboButtonGlyphs
    {
        /// Nombre de <see cref="InputGlyphNames"/> del botón.
        public static string GlyphName(ComboButton b) => b switch
        {
            ComboButton.A => InputGlyphNames.South,
            ComboButton.B => InputGlyphNames.East,
            ComboButton.X => InputGlyphNames.West,
            _ => InputGlyphNames.North,
        };

        /// Texto del botón en el dispositivo activo ("X", "□", "clic izquierdo"...).
        public static string Label(ComboButton b) =>
            InputGlyphLabels.GetLabel(GlyphName(b), InputGlyphService.CurrentFamily);

        /// Secuencia entera ("X · B · A").
        public static string SequenceLabel(ComboButton[] sequence, string separator = " · ")
        {
            if (sequence == null || sequence.Length == 0) return string.Empty;
            var parts = new string[sequence.Length];
            for (int i = 0; i < sequence.Length; i++) parts[i] = Label(sequence[i]);
            return string.Join(separator, parts);
        }

        /// Icono del botón para textos TMP (ver <see cref="InputGlyphService.SpriteTag"/>).
        public static string SpriteTag(ComboButton b) => InputGlyphService.SpriteTag(GlyphName(b));

        /// Secuencia entera en iconos, para textos TMP.
        public static string SequenceSprites(ComboButton[] sequence, string separator = " ")
        {
            if (sequence == null || sequence.Length == 0) return string.Empty;
            var parts = new string[sequence.Length];
            for (int i = 0; i < sequence.Length; i++) parts[i] = SpriteTag(sequence[i]);
            return string.Join(separator, parts);
        }
    }
}
