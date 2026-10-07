using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace SquashBot.UI
{
    /// <summary>
    /// Attached to every text the UI builds. Text without Arabic letters passes through untouched. Arabic text is
    /// shaped (<see cref="ArabicShaper"/>), wrapped into lines in reading order (measured with the text's own font, so
    /// the first line holds the start of the sentence), and each line is turned into right-to-left visual order.
    /// </summary>
    public class RtlTextPreprocessor : ITextPreprocessor
    {
        private readonly TMP_Text text;

        public RtlTextPreprocessor(TMP_Text text) => this.text = text;

        public string PreprocessText(string source)
        {
            if (!ArabicShaper.HasArabic(source)) return source;
            var shaped = ArabicShaper.Shape(source);
            bool wrap = text.textWrappingMode != TextWrappingModes.NoWrap && text.textWrappingMode != TextWrappingModes.PreserveWhitespaceNoWrap;
            float width = text.rectTransform.rect.width - text.margin.x - text.margin.z;
            var sb = new StringBuilder(shaped.Length + 8);
            var paragraphs = shaped.Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0) sb.Append('\n');
                var lines = wrap && width > 1f ? Wrap(paragraphs[p], width) : new List<string> { paragraphs[p] };
                for (int l = 0; l < lines.Count; l++)
                {
                    if (l > 0) sb.Append('\n');
                    sb.Append(ArabicShaper.ToVisual(lines[l]));
                }
            }
            return sb.ToString();
        }

        /// <summary>Greedy word wrap in reading order, measured with the font (and its fallbacks).</summary>
        private List<string> Wrap(string paragraph, float width)
        {
            var lines = new List<string>();
            var words = paragraph.Split(' ');
            var line = new StringBuilder();
            float lineWidth = 0f, space = Measure(" ");
            foreach (var w in words)
            {
                float ww = Measure(w);
                if (line.Length > 0 && lineWidth + space + ww > width)
                {
                    lines.Add(line.ToString());
                    line.Clear();
                    lineWidth = 0f;
                }
                if (line.Length > 0)
                {
                    line.Append(' ');
                    lineWidth += space;
                }
                line.Append(w);
                lineWidth += ww;
            }
            lines.Add(line.ToString());
            return lines;
        }

        private float Measure(string s)
        {
            var font = text.font;
            if (font == null) return s.Length * text.fontSize * 0.5f;
            float scale = text.fontSize / font.faceInfo.pointSize * font.faceInfo.scale;
            float w = 0f;
            foreach (char c in s)
            {
                var ch = Find(font, c);
                if (ch != null) w += ch.glyph.metrics.horizontalAdvance * scale * ch.scale;
                else w += text.fontSize * 0.5f;
            }
            return w + s.Length * text.characterSpacing * 0.01f * text.fontSize;
        }

        private static TMP_Character Find(TMP_FontAsset font, char c)
        {
            if (font.characterLookupTable.TryGetValue(c, out var ch)) return ch;
            if (font.HasCharacter(c, false, true) && font.characterLookupTable.TryGetValue(c, out ch)) return ch;
            if (font.fallbackFontAssetTable != null)
                foreach (var f in font.fallbackFontAssetTable)
                {
                    if (f == null) continue;
                    var found = Find(f, c);
                    if (found != null) return found;
                }
            return null;
        }
    }
}
