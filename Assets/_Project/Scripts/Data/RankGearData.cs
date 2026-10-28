using System;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The rank gear (Docs/RUTBE_KUSAMI_TAC_SAC_BIRLESIK.md, sections 2 and 6): each tier's crown and its stand-in bracer,
    /// pauldron and kneepad (simple plates until their models come: then only these rows change), and the crown's fit on
    /// the head and hair. Resources/Rank/RankGearData (an asset made from this, when there is one) or these.
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Rank Gear Data", fileName = "RankGearData")]
    public class RankGearData : ScriptableObject
    {
        [Serializable]
        public class Tier
        {
            [Tooltip("The crown's model under Resources/Rank/Crowns (its .json beside it holds its measures)")]
            public string crown;
            [Tooltip("The highest rank of this tier (rank index: 0 Private .. 12 Marshal)")]
            public int lastRank;
            [Tooltip("The stand-in plates' metal")]
            public Color metal = Color.gray;
            [Tooltip("The stand-in plates' trim (rivets, the gem)")]
            public Color trim = Color.white;
            [Range(0f, 1f)] public float metallic = 0.6f;
            [Tooltip("Hand-tuning: the crown's size × this")]
            public float crownScale = 1f;
            [Tooltip("Hand-tuning: the crown raised by this (head units)")]
            public float crownLift;
            [Tooltip("The crown's height × this (a very tall one squashed a little so it does not tower over foi)")]
            public float crownHeight = 1f;
        }

        public Tier[] tiers =
        {
            new Tier { crown = "Crown_T1", lastRank = 1, metal = new Color(0.62f, 0.38f, 0.2f), trim = new Color(0.85f, 0.6f, 0.35f), metallic = 0.55f },
            new Tier { crown = "Crown_T2", lastRank = 3, metal = new Color(0.5f, 0.52f, 0.55f), trim = new Color(0.32f, 0.33f, 0.36f), metallic = 0.7f },
            new Tier { crown = "Crown_T3", lastRank = 6, metal = new Color(0.78f, 0.8f, 0.84f), trim = new Color(0.25f, 0.55f, 0.95f), metallic = 0.75f, crownHeight = 0.8f },
            new Tier { crown = "Crown_T4", lastRank = 9, metal = new Color(0.92f, 0.72f, 0.3f), trim = new Color(0.85f, 0.1f, 0.12f), metallic = 0.8f, crownHeight = 0.85f },
            new Tier { crown = "Crown_T5", lastRank = 11, metal = new Color(0.82f, 0.88f, 0.94f), trim = new Color(0.55f, 0.85f, 1f), metallic = 0.75f, crownHeight = 0.62f },
            new Tier { crown = "Crown_T6", lastRank = 12, metal = new Color(0.12f, 0.12f, 0.15f), trim = new Color(0.95f, 0.75f, 0.3f), metallic = 0.85f, crownHeight = 0.85f },
        };

        [Header("The crown on the head (head units: the bald head is ~0.98 tall, its neck at 0)")]
        [Tooltip("Where the crown's ring sits on the bald head: on the skull over the forehead, down on the head (0.86 sat on top of the hair)")]
        public float socket = 0.8f;
        [Tooltip("The ring's inner width over the head's there (+3%)")]
        public float ringPad = 1.02f;
        [Tooltip("Tilted forward a little (degrees)")]
        public float tilt = 3f;
        [Tooltip("Method A: the crown may grow at most this much to sit on the hair")]
        public float maxGrow = 1.15f;
        [Tooltip("Method A: and rise at most this much over its bald place; past either, method B")]
        public float maxLift = 0f;
        [Tooltip("Method B: the crown rises this much over its bald place (the pressed hair under it; 0: down on the head itself)")]
        public float pressLift = 0f;
        [Tooltip("Method B: the crown a little wider than on the bald head")]
        public float pressGrow = 1.06f;
        [Tooltip("Method B: hair inside the ring may rise this share of the crown's height over its bottom")]
        public float pressInside = 0.35f;
        [Tooltip("The crown's hop on a blow (head units; ~1 cm)")]
        public float hop = 0.025f;

        [Header("The Marshal crown")]
        public Color marshalGlow = new Color(0.2f, 0.95f, 1f);
        [Tooltip("The core's pulse (seconds a beat)")]
        public float marshalPulse = 1.6f;
        [Tooltip("The motes about it (0: none; none on low quality)")]
        public int marshalMotes = 6;

        private static RankGearData current;

        public static RankGearData Current
        {
            get
            {
                if (current == null) current = Resources.Load<RankGearData>("Rank/RankGearData");
                if (current == null) { current = CreateInstance<RankGearData>(); current.hideFlags = HideFlags.DontSave; }
                return current;
            }
        }

        /// <summary>The gear tier (1..6) of rank <paramref name="rank"/>.</summary>
        public int TierOf(int rank)
        {
            for (int i = 0; i < tiers.Length; i++) if (rank <= tiers[i].lastRank) return i + 1;
            return tiers.Length;
        }

        /// <summary>The first rank of tier <paramref name="tier"/> (1..6).</summary>
        public int FirstRank(int tier) => tier <= 1 ? 0 : tiers[Mathf.Clamp(tier - 2, 0, tiers.Length - 1)].lastRank + 1;

        public Tier Of(int tier) => tiers[Mathf.Clamp(tier - 1, 0, tiers.Length - 1)];
    }
}
