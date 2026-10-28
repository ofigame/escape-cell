using System.Globalization;

namespace SquashBot.Data
{
    /// <summary>
    /// The moves' descriptions with their numbers taken from the duel's table (DuelBalanceTable), so the text and the
    /// value never part again: each "move.X.Y.d" text holds {0}, {1}… and the numbers go in here, written the language's
    /// way (0,6 in Turkish, 0.6 in English; per cent through "fmt.pct").
    /// </summary>
    public static class MoveTexts
    {
        /// <summary>A move's description (its key "move.s.Hook" and so on) with the table's numbers in.</summary>
        public static string Describe(string key)
        {
            var t = DuelBalanceTable.Current;
            string text = Loc.T(key + ".d");
            object[] args = Args(key, t);
            if (args.Length == 0) return text;
            try { return string.Format(text, args); }
            catch (System.FormatException) { return text; }
        }

        private static object[] Args(string key, DuelBalanceTable t)
        {
            switch (key)
            {
                case "move.s.Fly": return new object[] { N(t.flyTimes) };
                case "move.s.Backflip": return new object[] { N(t.SpecialUntouchable(SpecialKind.Backflip)) };
                case "move.s.Hook": return new object[] { N(t.SpecialReach(SpecialKind.Hook)), N(t.SpecialHit(SpecialKind.Hook)), N(t.SpecialStun(SpecialKind.Hook)) };
                case "move.s.Shock": return new object[] { N(t.SpecialReach(SpecialKind.Shock)), N(t.SpecialStun(SpecialKind.Shock)) };
                case "move.s.IceWall": return new object[] { N(t.SpecialStun(SpecialKind.IceWall)) };
                case "move.s.Patch": return new object[] { N(t.SpecialUntouchable(SpecialKind.Patch)) };
                case "move.s.SkyStrike": return new object[] { N(t.SpecialHit(SpecialKind.SkyStrike)) };
                case "move.s.Teleport": return new object[] { P(t.SpecialNextBlow(SpecialKind.Teleport) - 1f) };
                case "move.u.Cinematic": return new object[] { N(t.UltiTotal(UltiKind.Cinematic)) };
                case "move.u.Quake": return new object[] { N(t.UltiTotal(UltiKind.Quake)), N(t.UltiSlow(UltiKind.Quake)) };
                case "move.u.Whirlwind": return new object[] { N(t.UltiHits(UltiKind.Whirlwind)), N(t.UltiHit(UltiKind.Whirlwind)) };
                case "move.u.IronBody": return new object[] { N(t.UltiSeconds(UltiKind.IronBody)) };
                case "move.u.Mirror": return new object[] { N(t.UltiSeconds(UltiKind.Mirror)), N(t.mirrorReflect) };
                case "move.u.Copy": return new object[] { P(t.copyShare) };
                case "move.u.ChainLightning": return new object[] { N(t.UltiHits(UltiKind.ChainLightning)), N(t.UltiHit(UltiKind.ChainLightning)) };
                case "move.u.Meteor": return new object[] { N(t.UltiTelegraph(UltiKind.Meteor)), N(t.UltiTotal(UltiKind.Meteor)) };
                case "move.u.CoreBurst": return new object[] { N(t.UltiTotal(UltiKind.CoreBurst)) };
                case "move.p.ComboMaster": return new object[] { N(t.comboMasterGap) };
                case "move.p.HeartMaster": return new object[] { P(t.heartHeal + t.heartMasterHeal) };
                case "move.p.Piercer": return new object[] { P(t.piercerArmor) };
                case "move.p.FastCharge": return new object[] { P(t.fastCharge - 1f) };
                case "move.p.Rage": return new object[] { P(t.rageTimes - 1f), P(t.rageBelow) };
                case "move.p.Vampire": return new object[] { P(t.vampireShare) };
                case "move.p.ShieldMaster": return new object[] { N(t.shieldMasterDurability), N(t.maxDurability) };
                case "move.p.MasterWarrior": return new object[] { N(t.masterWarriorMeter) };
                default: return new object[0];
            }
        }

        /// <summary>A number the language's way (at most two decimals, none when whole).</summary>
        public static string N(float v) => v.ToString("0.##", Culture);

        /// <summary>A share as a per cent the language's way (0.3 → "%30" in Turkish).</summary>
        public static string P(float share) => Loc.F("fmt.pct", (share * 100f).ToString("0.#", Culture));

        private static CultureInfo Culture
        {
            get
            {
                switch (Loc.Current)
                {
                    case Language.Turkish: return CultureInfo.GetCultureInfo("tr-TR");
                    case Language.Azerbaijani: return CultureInfo.GetCultureInfo("tr-TR"); // (a comma for the decimals, as Azerbaijani writes them)
                    case Language.German: return CultureInfo.GetCultureInfo("de-DE");
                    case Language.Spanish: return CultureInfo.GetCultureInfo("es-ES");
                    case Language.Italian: return CultureInfo.GetCultureInfo("it-IT");
                    default: return CultureInfo.InvariantCulture;
                }
            }
        }
    }
}
