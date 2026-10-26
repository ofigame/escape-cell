using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The training challenges: the assets under Resources/Duel/Challenges when there are any, else the design's 25
    /// (five for each weapon family, five for the defence). Each pays its reward once (kept on the phone and in the
    /// cloud save with the rest of the preferences).
    /// </summary>
    public static class ComboChallenges
    {
        private static List<ComboChallengeData> all;

        public static List<ComboChallengeData> All
        {
            get
            {
                if (all != null) return all;
                all = new List<ComboChallengeData>(Resources.LoadAll<ComboChallengeData>("Duel/Challenges"));
                if (all.Count == 0) all = Defaults();
                all.Sort((a, b) => a.family != b.family ? (a.family < 0 ? 1 : b.family < 0 ? -1 : a.family.CompareTo(b.family)) : a.number.CompareTo(b.number));
                return all;
            }
        }

        public static ComboChallengeData Get(string id) => All.Find(c => c.id == id);

        public static bool Done(ComboChallengeData c) => c != null && Prefs.GetInt("sb_cc_" + c.id, 0) == 1;

        /// <summary>Marks it done and pays its reward (once: false if it was done already).</summary>
        public static bool Grant(ComboChallengeData c)
        {
            if (c == null || Done(c)) return false;
            Prefs.SetInt("sb_cc_" + c.id, 1);
            SaveData.Coins += c.gold;
            if (c.cores > 0) CoreParts.Add(c.cores);
            if (!string.IsNullOrEmpty(c.title)) Prefs.SetInt("sb_title_" + c.title, 1);
            Prefs.Save();
            return true;
        }

        /// <summary>The challenge's name: the family's (or the defence's) and its number.</summary>
        public static string Name(ComboChallengeData c) => Loc.F("cc.name", Loc.T(c.family == BodyFamily ? "cc.body" : c.family < 0 ? "cc.defense" : "cc.family." + c.family), c.number);

        /// <summary>The body attacks' challenges' family (the sweep, the uppercut, the headbutt, the shoulder, the crouch).</summary>
        public const int BodyFamily = -2;

        public static string StepText(ComboStep s) => Loc.F("cc.step." + s.kind, s.count);

        /// <summary>The design's 25: per family a chain of four, of six, five and a POWER HIT, a POWER HIT through a block,
        /// eight and a KNOCKDOWN with a blow in the air; the defence: five blocks in a row, ten without breaking, three
        /// perfect guards, a dodged ultimate, a perfect guard answered by a blow and a POWER HIT (the title "Wall").</summary>
        public static List<ComboChallengeData> Defaults()
        {
            var list = new List<ComboChallengeData>();
            string[] fam = { "sword", "spear", "axe", "heavy" };
            for (int f = 0; f < 4; f++)
            {
                list.Add(ComboChallengeData.Make(fam[f] + "1", f, 1, TrainingDummy.Standing, 100, 0, null, new ComboStep(ComboStepKind.Chain, 4)));
                list.Add(ComboChallengeData.Make(fam[f] + "2", f, 2, TrainingDummy.Standing, 150, 0, null, new ComboStep(ComboStepKind.Chain, 6)));
                list.Add(ComboChallengeData.Make(fam[f] + "3", f, 3, TrainingDummy.Standing, 200, 0, null, new ComboStep(ComboStepKind.Chain, 5), new ComboStep(ComboStepKind.PowerHit)));
                list.Add(ComboChallengeData.Make(fam[f] + "4", f, 4, TrainingDummy.Defending, 200, 0, null, new ComboStep(ComboStepKind.PowerThroughBlock)));
                list.Add(ComboChallengeData.Make(fam[f] + "5", f, 5, TrainingDummy.Standing, 0, 5, null, new ComboStep(ComboStepKind.Chain, 8), new ComboStep(ComboStepKind.Knockdown), new ComboStep(ComboStepKind.AirHit)));
            }
            list.Add(ComboChallengeData.Make("defense1", -1, 1, TrainingDummy.Attacking, 100, 0, null, new ComboStep(ComboStepKind.BlockInRow, 5)));
            list.Add(ComboChallengeData.Make("defense2", -1, 2, TrainingDummy.Attacking, 150, 0, null, new ComboStep(ComboStepKind.BlockNoBreak, 10)));
            list.Add(ComboChallengeData.Make("defense3", -1, 3, TrainingDummy.Attacking, 200, 0, null, new ComboStep(ComboStepKind.Perfects, 3)));
            list.Add(ComboChallengeData.Make("defense4", -1, 4, TrainingDummy.Ulti, 200, 0, null, new ComboStep(ComboStepKind.DodgeUlti)));
            list.Add(ComboChallengeData.Make("defense5", -1, 5, TrainingDummy.Attacking, 0, 5, "title.wall", new ComboStep(ComboStepKind.PerfectCounter), new ComboStep(ComboStepKind.PowerHit)));
            // the body's attacks (the choreography design's 4.1: the training extended with them)
            list.Add(ComboChallengeData.Make("body1", BodyFamily, 1, TrainingDummy.Standing, 100, 0, null, new ComboStep(ComboStepKind.Sweep, 3)));
            list.Add(ComboChallengeData.Make("body2", BodyFamily, 2, TrainingDummy.Standing, 150, 0, null, new ComboStep(ComboStepKind.Uppercut), new ComboStep(ComboStepKind.AirHit, 2)));
            list.Add(ComboChallengeData.Make("body3", BodyFamily, 3, TrainingDummy.Standing, 150, 0, null, new ComboStep(ComboStepKind.Headbutt), new ComboStep(ComboStepKind.ShoulderCharge)));
            list.Add(ComboChallengeData.Make("body4", BodyFamily, 4, TrainingDummy.Attacking, 200, 0, null, new ComboStep(ComboStepKind.CrouchUnder, 3)));
            return list;
        }
    }
}
