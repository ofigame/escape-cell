using BrickMuseum.Data;
using TMPro;

namespace BrickMuseum.UI
{
    /// <summary>
    /// Attached to every text the game builds: the Turkish source text is shown in the player's language
    /// (<see cref="Loc.Translate"/>), and Arabic is then shaped and laid out right to left.
    /// </summary>
    public sealed class LocPreprocessor : ITextPreprocessor
    {
        private readonly RtlTextPreprocessor rtl;

        public LocPreprocessor(TMP_Text text) => rtl = new RtlTextPreprocessor(text);

        public string PreprocessText(string source)
        {
            var s = Loc.Translate(source);
            return rtl.PreprocessText(s);   // only text with Arabic letters is touched (also the language list's own name)
        }

        public static void Attach(TMP_Text t) => t.textPreprocessor = new LocPreprocessor(t);
    }
}
