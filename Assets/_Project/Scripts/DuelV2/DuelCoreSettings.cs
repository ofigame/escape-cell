using System;
using System.IO;
using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// Every number of the duel fight core v2 (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md): the moves' phases, stun and push,
    /// the advantage, the rhythmic chain, damage as a share of the reference health, the rounds, the elemental powers,
    /// the camera's cuts and slow motions, the platform's width. The prototype edits them live (F1) and saves them to
    /// DuelCoreSettings.json beside the game; a saved file overrides the defaults below.
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Duel/Core Settings V2", fileName = "DuelCoreSettings")]
    public class DuelCoreSettings : ScriptableObject
    {
        [Header("Tempo")]
        [Tooltip("Every duration together (0.8 = quicker .. 1.2 = slower)")]
        public float tempo = 1f;

        [Header("Moves by family (ms): sword, spear, axe, heavy")]
        public float[] windupMs = { 90f, 120f, 160f, 190f };
        public float[] activeMs = { 50f, 60f, 70f, 80f };
        public float[] recoveryMs = { 160f, 200f, 240f, 280f };
        [Tooltip("The struck one's stun on a hit")]
        public float[] hitStunMs = { 260f, 290f, 330f, 360f };
        [Tooltip("The blocker's push back on a block")]
        public float[] blockPushMs = { 180f, 190f, 200f, 210f };
        [Tooltip("Reach, centre to centre (world units)")]
        public float[] reach = { 2.3f, 3.0f, 2.5f, 2.6f };
        [Tooltip("A missed blow's recovery, more (ms)")]
        public float whiffExtraMs = 120f;
        [Tooltip("No window the other can answer in is shorter than this (ms; the live match's lag)")]
        public float minReactMs = 150f;

        [Header("Advantage")]
        [Tooltip("The perfect block: the attacker staggers this long (ms)")]
        public float perfectStaggerMs = 500f;
        [Tooltip("The perfect block's window from the shield's raising (ms)")]
        public float perfectWindowMs = 120f;
        [Tooltip("A trade on the same frame: both stagger this long (ms)")]
        public float clashStaggerMs = 220f;
        [Tooltip("Show the stun glint and the advantage glint on the weapon")]
        public bool showAdvantageHints = true;

        [Header("Rhythmic chain")]
        [Tooltip("The chain window opens this long after the strike's end (ms)")]
        public float chainOpenMs = 60f;
        [Tooltip("A tap this long before the window opens is kept (ms)")]
        public float bufferMs = 120f;
        [Tooltip("Taps outside the window in one blow that end the chain")]
        public int spamTaps = 2;
        [Tooltip("Blows in a chain set")]
        public int chainLength = 3;
        [Tooltip("The struck one's stun after the set's last blow (ms)")]
        public float lastHitStunMs = 180f;
        [Tooltip("The series ends after this long with no hit (s)")]
        public float seriesGap = 1.2f;
        [Tooltip("Each hit in a series does this share of the one before")]
        public float seriesDecay = 0.9f;
        [Tooltip("...never under this share")]
        public float seriesFloor = 0.5f;
        [Tooltip("The series' hit that offers KNOCKDOWN")]
        public int knockdownAt = 6;
        [Tooltip("Rhythm cue: a light vibration as the window opens")]
        public bool rhythmVibration = false;

        [Header("Damage (share of the reference health, %)")]
        public float[] blowPct = { 4.0f, 4.8f, 6.0f, 6.6f };
        public float powerHitPct = 10f;
        public float knockdownPct = 12f;
        [Tooltip("A blow in the air after a KNOCKDOWN (each, %)")]
        public float airHitPct = 3f;
        public float specialPct = 7.5f;
        public float ultiPct = 25f;
        [Tooltip("A blocked blow's damage share")]
        public float chipShare = 0.2f;
        [Tooltip("Reference health by league tier 1..5 (the tier's duel health ceiling)")]
        public float[] referenceHp = { 620f, 940f, 1260f, 1580f, 1900f };
        [Tooltip("A heart gives back this share of health")]
        public float heartShare = 0.3f;
        public int heartsPerMatch = 3;
        [Tooltip("The ghost bar melts after (s)")]
        public float ghostDelay = 0.4f;

        [Header("Shield")]
        public float shieldDurability = 100f;
        [Tooltip("A blocked blow costs the shield (by family)")]
        public float[] blockCost = { 8f, 9f, 12f, 14f };
        public float powerHitBlockCost = 30f;
        [Tooltip("The shield broken: no blocking this long (s)")]
        public float shieldBrokenSeconds = 3f;
        [Tooltip("Walking while blocking: this share of the pace")]
        public float blockWalkScale = 0.5f;

        [Header("Defence and escapes")]
        [Tooltip("Dodge: untouchable (s)")]
        public float dodgeInvuln = 0.25f;
        [Tooltip("Dodge: distance back (world units)")]
        public float dodgeDistance = 1f;
        public float dodgeCooldown = 1.2f;
        [Tooltip("Getting up: untouchable (s)")]
        public float getUpInvuln = 0.5f;
        [Tooltip("Lying after a fall (s)")]
        public float downSeconds = 0.7f;
        [Tooltip("BREAK costs this many bar parts")]
        public float breakCost = 1f;
        [Tooltip("BREAK: the chained one pushed back and stunned (ms)")]
        public float breakStunMs = 450f;
        [Tooltip("POWER HIT: the struck one thrown back (world units) and stunned (ms)")]
        public float powerHitPush = 2.2f;
        public float powerHitStunMs = 600f;
        [Tooltip("KNOCKDOWN: time in the air (s), air hits at most")]
        public float airSeconds = 1f;
        public int airHitsMax = 3;
        [Tooltip("Jump: time in the air (s)")]
        public float jumpSeconds = 0.6f;
        [Tooltip("Walk speed (units/s)")]
        public float walkSpeed = 4.2f;
        [Tooltip("Bodies never closer than this (centre to centre)")]
        public float bodyGap = 1.5f;

        [Header("Bar (ultimate)")]
        [Tooltip("Bar parts gained per hit dealt / per hit taken / per perfect block")]
        public float meterPerHit = 0.08f;
        public float meterPerTaken = 0.05f;
        public float meterPerPerfect = 0.4f;
        public float meterMax = 3f;
        [Tooltip("The special's cooldown (s)")]
        public float specialCooldown = 8f;

        [Header("Rounds")]
        public float roundSeconds = 60f;
        public int roundsToWin = 2;
        public float goldenRoundSeconds = 15f;
        [Tooltip("Equal within this share: the golden round")]
        public float tieShare = 0.01f;
        public float roundWinPose = 1.5f;
        public float roundIntro = 2f;

        [Header("Elemental powers")]
        public int snowAtSeries = 4;
        public int fireAtSeries = 6;
        public int maxPowers = 3;
        public float powerCooldown = 1.5f;
        [Tooltip("The rain's total damage (% of the reference health)")]
        public float rainPct = 13f;
        [Tooltip("Snow / fire / lightning: drops, seconds, warning before (s)")]
        public int[] rainDrops = { 6, 4, 5 };
        public float[] rainSeconds = { 1.5f, 1.5f, 1.2f };
        public float[] rainWarn = { 0.8f, 0.6f, 0.4f };
        [Tooltip("The target circle's radius (world units)")]
        public float rainRadius = 1.6f;
        [Tooltip("Lightning's circle follows the target this fast (units/s)")]
        public float lightningFollow = 1.6f;
        [Tooltip("The shield's cost per drop: snow, fire, lightning")]
        public float[] rainBlockCost = { 8f, 15f, 10f };
        [Tooltip("A blocked drop's damage share")]
        public float rainBlockShare = 0.2f;
        public float summonSeconds = 0.4f;

        [Header("Camera and slow motion")]
        [Tooltip("Hit stop by family (ms)")]
        public float[] hitStopMs = { 20f, 35f, 50f, 65f };
        public float thirdHitStopMs = 70f;
        public float powerHitStopMs = 90f;
        public float knockdownSlow = 0.3f;
        public float perfectSlow = 0.15f;
        public float rainLastSlow = 0.25f;
        public float koSlow = 0.6f;
        public float replaySeconds = 2f;
        [Tooltip("The camera's nearest and furthest (world units from the fighters' middle)")]
        public float camNear = 11f;
        public float camFar = 26f;
        [Tooltip("Room either side of the two fighters (world units)")]
        public float camSideRoom = 3.2f;
        public float camFollow = 4f;
        public bool screenShake = true;
        public bool replay = true;

        [Header("Platform")]
        [Tooltip("The platform's width along the fighters' line (x the stage's own)")]
        public float platformWidth = 1.5f;

        // ---------- Loading, saving ----------

        private static DuelCoreSettings current;

        /// <summary>The settings in use: the saved file over the defaults.</summary>
        public static DuelCoreSettings Current
        {
            get
            {
                if (current != null) return current;
                current = CreateInstance<DuelCoreSettings>();
                if (File.Exists(FilePath)) { try { JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), current); } catch (Exception e) { Debug.LogWarning("[DuelV2] settings not read: " + e.Message); } }
                return current;
            }
        }

        /// <summary>Beside the game (the build's folder; the project's in the editor).</summary>
        public static string FilePath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "DuelCoreSettings.json"));

        public static void Save() => File.WriteAllText(FilePath, JsonUtility.ToJson(Current, true));

        public static bool Load()
        {
            if (!File.Exists(FilePath)) return false;
            JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), Current);
            return true;
        }

        /// <summary>Back to the defaults (the file is left as it is until saved).</summary>
        public static void Reset()
        {
            var fresh = CreateInstance<DuelCoreSettings>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fresh), Current);
            Destroy(fresh);
        }

        // ---------- Reading ----------

        /// <summary>A duration (ms in the table) in seconds, with the tempo.</summary>
        public float S(float ms) => ms * 0.001f * tempo;
        public float Windup(int f) => S(windupMs[f]);
        public float Active(int f) => S(activeMs[f]);
        public float Recovery(int f) => S(recoveryMs[f]);
        public float HitStun(int f) => Mathf.Max(S(hitStunMs[f]), S(minReactMs));
        public float BlockPush(int f) => S(blockPushMs[f]);
    }
}
