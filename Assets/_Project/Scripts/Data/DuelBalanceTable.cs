using System;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The duel's one table of numbers (the fixes design's job 1): the weapon families' blows and paces, the bars and
    /// combos, every special, ultimate and passive, the hearts and shields and the defence. The game and the server read
    /// the very same file (Resources/Duel/duel_rules.json, linked into the server); the bot duel and the live duel both
    /// count with it. The server can send a newer table (its own copy changed without a new game): it is kept on the
    /// phone and used from then on. Arrays follow the enums' order (WeaponFamily, SpecialKind, UltiKind).
    /// </summary>
    [Serializable]
    public class DuelBalanceTable
    {
        public int version;

        // The weapon families (sword, spear, axe, heavy): pace, blows by tier 1..6 (6 a family), the combo moves' factor,
        // the stagger a blow sets, what a blocked blow costs the shield.
        public float[] familyCooldown;
        public int[] familyBlows;
        public float[] familyComboTimes, familyStagger, familyGuardCost;
        /// <summary>How far a duel blow reaches by family (the gap kept is 3; the spear one tile more).</summary>
        public int[] familyReach;

        // The bars and combos.
        public float meterGain, meterHurt, meterPaceDivisor, meterPaceMin, meterPaceMax, meterMax;
        public float comboGap, comboMoveSeconds;
        public int heavyCombo, upperCombo;
        public float flyTimes, heavyTimes, upperTimes, heavyStun, upperStun, breakerStun;
        public int breakerMinCombo;
        /// <summary>A blow struck in the air takes this much more, at most once in this long.</summary>
        public float jumpExtra, airEvery;
        public float specialCost, breakerCost, ultiCost;
        /// <summary>Each special's wait before it can be used again (SpecialKind order; table version 3: the specials no
        /// longer spend the power bar, the user's word 2026-10-28).</summary>
        public float[] specialCooldown;
        /// <summary>The weapon's mastery in the blow (table version 3, the user's word 2026-10-28): this much more a level,
        /// at most this much a league-ceiling tier (tier 5: +10%); none at equal power (friends, events).</summary>
        public float masteryDamagePerLevel, masteryDamagePerTier;

        /// <summary>A blow's factor for a weapon's mastery <paramref name="level"/> under ceiling tier <paramref name="tier"/>.</summary>
        public float MasteryTimes(int level, int tier, bool equalPower) =>
            equalPower ? 1f : 1f + Mathf.Min(Mathf.Max(0, level) * masteryDamagePerLevel, Mathf.Max(1, tier) * masteryDamagePerTier);

        /// <summary>Special <paramref name="kind"/>'s cooldown (8 s when the table has none).</summary>
        public float SpecialCooldown(int kind) => specialCooldown != null && kind >= 0 && kind < specialCooldown.Length ? specialCooldown[kind] : 8f;

        // Hearts and shields.
        public int hearts, shields, shieldHits;
        public float heartHeal, shieldSeconds;

        // The specials (SpecialKind order): times a blow, a stun on the other, the mover untouchable, health back, the
        // next blow's factor, the reach in tiles (0: anywhere).
        public float[] specialHit, specialStun, specialUntouchable, specialHeal, specialNextBlow, specialReach;

        // The ultimates (UltiKind order): times a blow for each of its hits and how many hits, how long it lasts (iron
        // body, mirror), how long the other is slowed, health back, how long its mark shows before it lands.
        public float[] ultiHit, ultiHits, ultiSeconds, ultiSlow, ultiHeal, ultiTelegraph;
        /// <summary>How long an ultimate holds the other where it stands (no blows) while it plays.</summary>
        public float[] ultiHold;
        public float copyShare, mirrorReflect;

        // The passives.
        public float comboMasterGap, heartMasterHeal, piercerArmor, fastCharge, rageBelow, rageTimes, vampireShare;
        public float shieldMasterDurability, masterWarriorMeter;
        public int shieldMasterHits;

        // The defence.
        public float grazeShare, ultiThrough, maxDurability, powerHitCost, airCost, knockdownCost, ultiGuardCost;
        public float regenDelay;
        public float[] regenPerSecond;
        public float breakDaze, breakReturn, breakReturnShare;
        public float perfectBefore, perfectAfter, staggerSeconds, perfectNextBlow, perfectMeter;
        public float tapRelease, tapLockout, dodgeInvulnerable, dodgeCooldown;

        // The golden knock-down: a level score at time up plays on this long (the first knock-down wins), then the larger
        // share of health wins; closer than this share it is a draw.
        public float overtimeSeconds, overtimeHealthTie;

        // The body's attacks (the choreography design's 4.3 .. 4.7), in PhysKind's order (sweep, uppercut, flying headbutt,
        // shoulder charge): the blow's times, the cooldown, the shield's durability it costs, its height (HitHeight: 0 high,
        // 1 middle, 2 low: a crouch lets high ones by, a jump low ones), the stun it leaves, how long the striker stands open
        // when it is blocked, its reach in tiles. A swept fighter lies down this long and gets up in this long (untouchable,
        // nothing to do); a launched one is in the air this long, hit at most this often there, each hit times this.
        public float[] physTimes, physCooldown, physGuardCost, physStun, physOpen, physReach;
        public int[] physHeight;
        public float downSeconds, getUpSeconds, airSeconds;
        public int airHitsMax;
        public float[] airHitTimes;
        /// <summary>How long a jump keeps its jumper in the air (low blows go under it).</summary>
        public float jumpAirSeconds;

        private const string CacheKey = "sb_net_duel_rules"; // (kept on this phone only)
        private static DuelBalanceTable current;

        /// <summary>A newer table came in (the defence's numbers follow it).</summary>
        public static event Action Changed;

        /// <summary>The table in use: the server's newer one kept on the phone, else the game's own.</summary>
        public static DuelBalanceTable Current
        {
            get
            {
                if (current != null) return current;
                var asset = Resources.Load<TextAsset>("Duel/duel_rules");
                current = asset != null ? JsonUtility.FromJson<DuelBalanceTable>(asset.text) : null;
                var kept = Parse(Prefs.GetString(CacheKey, ""));
                if (kept != null && (current == null || kept.version > current.version)) current = kept;
                if (current == null) throw new InvalidOperationException("Resources/Duel/duel_rules.json is missing");
                return current;
            }
        }

        public static int Version => Current.version;

        /// <summary>A table the server sent: used and kept when it is newer than the one in use (true then).</summary>
        public static bool Offer(string json)
        {
            var t = Parse(json);
            if (t == null || t.version <= Version) return false;
            current = t;
            Prefs.SetString(CacheKey, json);
            Prefs.Save();
            Changed?.Invoke();
            return true;
        }

        private static DuelBalanceTable Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var t = JsonUtility.FromJson<DuelBalanceTable>(json);
                return t != null && t.familyBlows != null && t.familyBlows.Length == 24 && t.specialHit != null && t.ultiHit != null && (t.version < 2 || (t.physTimes != null && t.physTimes.Length == 4 && t.airHitTimes != null)) ? t : null;
            }
            catch (Exception) { return null; }
        }

        // ---------- Read by kind ----------

        public float Cooldown(WeaponFamily f) => familyCooldown[(int)f];
        public int Blow(WeaponFamily f, int tier) => familyBlows[(int)f * 6 + Mathf.Clamp(tier, 1, 6) - 1];
        public float ComboTimes(WeaponFamily f) => familyComboTimes[(int)f];
        public float Stagger(WeaponFamily f) => familyStagger[(int)f];
        public int Reach(WeaponFamily f) => familyReach != null ? familyReach[(int)f] : 3;

        /// <summary>A blow's share of the bar by its weapon's pace (a quick blade's many blows fill it no faster than a slow hammer's few).</summary>
        public float MeterPace(float cooldown) => Mathf.Clamp(cooldown / meterPaceDivisor, meterPaceMin, meterPaceMax);

        public float SpecialHit(SpecialKind s) => specialHit[(int)s];
        public float SpecialStun(SpecialKind s) => specialStun[(int)s];
        public float SpecialUntouchable(SpecialKind s) => specialUntouchable[(int)s];
        public float SpecialHeal(SpecialKind s) => specialHeal[(int)s];
        public float SpecialNextBlow(SpecialKind s) => specialNextBlow[(int)s];
        public float SpecialReach(SpecialKind s) => specialReach[(int)s];

        /// <summary>One hit of an ultimate (times a blow), and all of them together.</summary>
        public float UltiHit(UltiKind u) => ultiHit[(int)u];
        public int UltiHits(UltiKind u) => Mathf.RoundToInt(ultiHits[(int)u]);
        public float UltiTotal(UltiKind u) => ultiHit[(int)u] * ultiHits[(int)u];
        public float UltiSeconds(UltiKind u) => ultiSeconds[(int)u];
        public float UltiSlow(UltiKind u) => ultiSlow[(int)u];
        public float UltiHeal(UltiKind u) => ultiHeal[(int)u];
        public float UltiTelegraph(UltiKind u) => ultiTelegraph[(int)u];
        public float UltiHold(UltiKind u) => ultiHold != null ? ultiHold[(int)u] : 0f;
    }
}
