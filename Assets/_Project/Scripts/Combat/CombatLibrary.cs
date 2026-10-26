using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// Where the duel's motion data comes from: the assets under Resources/Combat when there are any (tuned in the
    /// editor), otherwise the design's starting values; and which family each weapon kind belongs to.
    /// </summary>
    public static class CombatLibrary
    {
        public const string Folder = "Combat/";
        private static CombatFeelSettings settings;
        private static HitReactionProfile reactions;
        private static readonly Dictionary<WeaponFamily, WeaponFamilyMotionProfile> families = new Dictionary<WeaponFamily, WeaponFamilyMotionProfile>();

        public static CombatFeelSettings Settings
        {
            get
            {
                if (settings == null) settings = Resources.Load<CombatFeelSettings>(Folder + "CombatFeelSettings") ?? CombatDefaults.Settings();
                return settings;
            }
        }

        private static AttackMotionData ultiFinal;

        /// <summary>An ultimate's final blow (Resources/Combat/UltiFinal, or the design's).</summary>
        public static AttackMotionData UltiFinal
        {
            get
            {
                if (ultiFinal == null) ultiFinal = Resources.Load<AttackMotionData>(Folder + "UltiFinal") ?? CombatDefaults.UltiFinal();
                return ultiFinal;
            }
        }

        public static HitReactionProfile Reactions
        {
            get
            {
                if (reactions == null) reactions = Resources.Load<HitReactionProfile>(Folder + "HitReactions") ?? CombatDefaults.Reactions();
                return reactions;
            }
        }

        public static WeaponFamilyMotionProfile Family(WeaponFamily f)
        {
            if (families.TryGetValue(f, out var p) && p != null) return p;
            p = Resources.Load<WeaponFamilyMotionProfile>(Folder + "Family_" + f) ?? CombatDefaults.Family(f);
            if (p.flyStrike == null || p.airStrike == null || p.skyStrike == null)
            {
                // an asset saved before these blows existed: the design's starting ones
                var d = CombatDefaults.Family(f);
                if (p.flyStrike == null) p.flyStrike = d.flyStrike;
                if (p.airStrike == null) p.airStrike = d.airStrike;
                if (p.skyStrike == null) p.skyStrike = d.skyStrike;
            }
            families[f] = p;
            return p;
        }

        /// <summary>The family a weapon kind fights as (hammers, maces and anything else heavy: the heavy family).</summary>
        /// <summary>
        /// How far a striker's picture slides toward the rival so the blow touches its body at contact (the design's 2.1):
        /// the blow's own value, or the family's default by the blow's direction (from the phase 0 measures: the gap at
        /// contact from the duel's 3 tiles, less the struck one's lean into it).
        /// </summary>
        public static float ContactOffset(WeaponFamily family, AttackMotionData blow)
        {
            float max = Settings != null ? Settings.maxLunge : 1.5f;
            if (blow == null) return 0f;
            if (blow.contactOffset >= 0f) return Mathf.Min(blow.contactOffset, max);
            float d;
            switch (blow.direction)
            {
                case HitDirection.Thrust: d = family == WeaponFamily.Spear ? 0.2f : 1.5f; break; // (a spear's thrust already reaches)
                case HitDirection.Overhead: d = 1.5f; break;
                case HitDirection.Upward: d = 1.5f; break;
                case HitDirection.Spin: d = 1.5f; break;
                case HitDirection.Horizontal_LeftToRight: d = 1.5f; break;
                default: d = family == WeaponFamily.Axe ? 1.45f : 1.3f; break;
            }
            return Mathf.Min(d, max);
        }

        public static WeaponFamily FamilyOf(WeaponKind k) =>
            k == WeaponKind.Sword ? WeaponFamily.Sword : k == WeaponKind.Spear ? WeaponFamily.Spear : k == WeaponKind.Axe ? WeaponFamily.Axe : WeaponFamily.Heavy;
    }
}
