using System.Collections.Generic;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// One elemental rain (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 5.2–5.3): its kind, who called it and on whom, the target
    /// circle (where on the line, how wide; locked for snow and fire, following its target for lightning), when the
    /// warning ends, the drops' times and how many have fallen, each drop's share of the damage (the last one twice).
    /// </summary>
    [System.Serializable]
    public class ElementRain
    {
        public ElementKind kind;
        public int caster, target;
        public float centre, radius;
        public float warnEnd;
        public List<float> dropAt = new List<float>();
        public int fallen;
        /// <summary>A plain drop's damage (% of the reference health); the last one does twice this.</summary>
        public float dropPct;
        public bool follows;
        public bool Done => fallen >= dropAt.Count;
        public bool Last(int i) => i == dropAt.Count - 1;
    }
}
