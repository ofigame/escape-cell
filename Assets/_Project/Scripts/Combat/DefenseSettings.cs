using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// The duel's defence (the design's §3): BLOCK (hold the shield: a fifth of the damage gets through, durability is
    /// spent), the PERFECT GUARD (pressed just before the blow lands: nothing gets through, the striker staggers) and the
    /// DODGE (a swipe down: a quick step back, a moment untouchable). Every number here; the server's Rules carry the
    /// same ones for live matches. Resources/Duel/DefenseSettings when there is one, else these.
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Duel/Defense Settings", fileName = "DefenseSettings")]
    public class DefenseSettings : ScriptableObject
    {
        [Header("Block")]
        /// <summary>The share of a blocked blow's damage that still gets through.</summary>
        public float grazeShare = 0.2f;
        /// <summary>Blocking, foi walks at this pace and cannot strike.</summary>
        public float walkWhileBlocking = 0.5f;

        [Header("Durability")]
        public float maxDurability = 100f;
        /// <summary>The shield master passive's durability.</summary>
        public float shieldMasterDurability = 130f;
        /// <summary>A blocked plain blow by family (sword, spear, axe, heavy).</summary>
        public float[] blowCost = { 5f, 7f, 10f, 12f };
        public float powerHitCost = 35f, airCost = 25f, knockdownCost = 50f, ultiCost = 60f;
        /// <summary>A blocked ultimate still takes this share of its damage.</summary>
        public float ultiThrough = 0.5f;
        /// <summary>After the shield is let go it fills again after this delay, by this much a second (no shield, wood, steel, golden stag, amethyst).</summary>
        public float regenDelay = 0.8f;
        public float[] regenPerSecond = { 30f, 30f, 32f, 34f, 36f };
        /// <summary>Broken (durability 0): dazed this long (open to everything), the shield back after this long with this share.</summary>
        public float breakDaze = 1f, breakReturn = 3f, breakReturnShare = 0.5f;
        /// <summary>Under this share the ring turns red and the shield shows cracks.</summary>
        public float lowShare = 0.25f;

        [Header("Perfect guard")]
        /// <summary>The press must come this long before the blow lands … or this long after it (seconds).</summary>
        public float perfectBefore = 0.12f, perfectAfter = 0.03f;
        /// <summary>The striker staggers this long (no blows, no guard).</summary>
        public float staggerSeconds = 0.5f;
        /// <summary>The guard's reward: its next blow this much harder, this much of the power bar.</summary>
        public float perfectNextBlow = 0.3f, perfectMeter = 0.5f;
        /// <summary>Against pressing at random: a press let go within this long without a perfect guard locks the shield for that long.</summary>
        public float tapRelease = 0.2f, tapLockout = 0.35f;
        /// <summary>The tutorial's (and the training room's helper) wider window.</summary>
        public float tutorialWindow = 0.3f;

        [Header("Dodge")]
        public int dodgeTiles = 1;
        public float dodgeSeconds = 0.2f, dodgeInvulnerable = 0.25f, dodgeCooldown = 1.2f;
        /// <summary>Nowhere to step back to: a lean to the side, untouchable this long.</summary>
        public float leanInvulnerable = 0.15f;
        /// <summary>A power hit or knockdown that meets nothing: its striker recovers this much slower.</summary>
        public float whiffRecovery = 0.3f;

        [Header("Bots (Er–Başçavuş, Asteğmen–Üsteğmen, Yüzbaşı–Yarbay, Albay–Mareşal)")]
        public float[] botBlock = { 0.15f, 0.25f, 0.35f, 0.45f };
        public float[] botPerfect = { 0.03f, 0.10f, 0.20f, 0.30f };
        public float[] botUltiDodge = { 0.20f, 0.35f, 0.50f, 0.65f };
        /// <summary>How well it lets the shield go before it breaks (0 badly .. 1 very well).</summary>
        public float[] botRelease = { 0.25f, 0.5f, 0.75f, 0.92f };
        /// <summary>The personalities: the defensive one blocks and guards more, the aggressive one less, the runner dodges more.</summary>
        public float defensiveTimes = 1.5f, aggressiveTimes = 0.5f, runnerDodgeTimes = 1.5f;
        /// <summary>A bot's perfect try that comes too early or late (and is punished as a random press).</summary>
        public float botMistimed = 0.35f;
        /// <summary>The dynamic difficulty: each step harder (or easier) changes the chances by this share.</summary>
        public float difficultyStep = 0.12f;
        /// <summary>Learning the player's habit: the last this many seconds watched, the chance up by at most this much.</summary>
        public float habitWindow = 30f, habitMax = 0.25f;
        /// <summary>How long a bot holds its block once it raises it (seconds).</summary>
        public float botHoldMin = 0.5f, botHoldMax = 1.4f;

        [Header("Weekly rules and passives")]
        /// <summary>The shield war week: durability times this, a perfect guard fills this much of the power bar.</summary>
        public float shieldWarDurability = 2f, shieldWarPerfectMeter = 1f;

        private static DefenseSettings loaded;

        public static DefenseSettings Current
        {
            get
            {
                if (loaded == null) loaded = Resources.Load<DefenseSettings>("Duel/DefenseSettings");
                if (loaded == null) loaded = CreateInstance<DefenseSettings>();
                return loaded;
            }
        }

        /// <summary>The rank band (0..3) of a rank (0 Er .. 12 Mareşal).</summary>
        public static int Band(int rank) => rank <= 3 ? 0 : rank <= 6 ? 1 : rank <= 9 ? 2 : 3;
    }
}
