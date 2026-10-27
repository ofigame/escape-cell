using SquashBot.Combat;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The duel's two bodies never inside each other (Docs/DUELLO_CARPISMA_VE_TEMAS.md, section 2): each frame, after every
    /// clip, lunge, push and pose has moved the body, its trunk (a push box: an upright round as wide as its armour) is
    /// kept a contact distance from the other's along the line between their places. The one coming at the other stops
    /// there; both coming, each stops half way; neither (placed so), both give half. The line's order is kept whatever the
    /// speed (the gap is kept on its own side, so no frame can jump through), but a body over the other's head (a leap over
    /// it) does not clash. Looks only: the places, the rules' and the server's, never move. No allocation per frame.
    /// </summary>
    public partial class SkinnedHero
    {
        private static SkinnedHero duelA, duelB;
        private Vector3 pushPrev, pushFinal;
        private int pushFrame = -1;
        private bool pushComing;
        private float trunkCache = -1f, heightCache = -1f;
        private SkinnedMeshRenderer[] pushRenderers;

        /// <summary>Frames the two bodies overlapped after the check (the lab's counter; 0 is right).</summary>
        public static int OverlapFrames { get; private set; }
        /// <summary>The two bodies' contact distance now (0: no duel pair).</summary>
        public static float ContactDistance { get; private set; }

        /// <summary>The duel's two bodies (null, null: none) — each kept out of the other.</summary>
        public static void PairForDuel(SkinnedHero a, SkinnedHero b)
        {
            duelA = a; duelB = b;
            if (a != null) { a.trunkCache = -1f; a.pushFrame = -1; }
            if (b != null) { b.trunkCache = -1f; b.pushFrame = -1; }
        }

        /// <summary>The duel's pair now (null when none), for the lab's boxes.</summary>
        public static SkinnedHero DuelFirst => duelA;
        public static SkinnedHero DuelSecond => duelB;

        /// <summary>Its push box's radius in the world.</summary>
        public float PushBoxRadius => PushRadius;

        /// <summary>Its trunk's middle at the hips now (the push box's centre).</summary>
        public Vector3 TrunkPoint => hips != null ? hips.position : transform.position;

        /// <summary>Where a blow can land on it (its hurt areas): the head, the chest, the legs (between the knees).</summary>
        public void HurtSpots(out Vector3 headAt, out Vector3 chestAt, out Vector3 legsAt)
        {
            headAt = HeadPoint;
            chestAt = ChestPoint;
            legsAt = kneeL != null && kneeR != null ? (kneeL.position + kneeR.position) * 0.5f : TrunkPoint - Vector3.up * PushRadius;
        }

        /// <summary>The lab's counter back to 0.</summary>
        public static void ResetOverlapCount() => OverlapFrames = 0;

        private SkinnedHero DuelOther => this == duelA ? duelB : this == duelB ? duelA : null;

        /// <summary>Its push box's radius in the world (the trunk measured, times the settings').</summary>
        private float PushRadius
        {
            get
            {
                if (trunkCache < 0f)
                {
                    float r = TrunkRadius;
                    heightCache = Mathf.Max(0.1f, TopHeight);
                    trunkCache = r > 0.01f ? r : heightCache * CollisionSettings.Current.fallbackRadiusShare;
                }
                return trunkCache * CollisionSettings.Current.trunkScale;
            }
        }

        /// <summary>The body shown at all (not hidden while its stand-in flies).</summary>
        private bool BodyShown
        {
            get
            {
                if (!isActiveAndEnabled) return false;
                if (pushRenderers == null) pushRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var r in pushRenderers) if (r != null && r.enabled) return true;
                return false;
            }
        }

        private bool finishPending;
        private static int resolvedFrame = -1;
        private static float clashAt = -9f;
        private SkinnedHeroLate late;

        /// <summary>Coming at the other with a blow (a mapped attack or a body blow playing).</summary>
        private bool Rushing => AttackPlaying || strikeNow != Combat.BodyStrike.Weapon;

        /// <summary>
        /// Both bodies posed this frame: the two trunks kept a contact distance apart along the line between their places
        /// (the one coming at the other stops; both coming, each half; neither, each half), then each hand takes its weapon.
        /// Run once a frame after every body's pose (<see cref="SkinnedHeroLate"/>), so neither is checked against where the
        /// other stood a frame ago.
        /// </summary>
        public void FinishFrame()
        {
            if (!finishPending) return;
            if (resolvedFrame != Time.frameCount) { resolvedFrame = Time.frameCount; ResolvePair(); }
            finishPending = false;
            Grip();
        }

        private static void ResolvePair()
        {
            var a = duelA; var b = duelB;
            if (a == null || b == null || !HeldWeapon.DuelStance || a.hips == null || b.hips == null || a.transform.parent == null || b.transform.parent == null) return;
            var pa = a.hips.position; var pb = b.hips.position;
            bool shown = a.BodyShown && b.BodyShown;
            if (shown)
            {
                // the line between their places (never the pictures: the line's order is the rules')
                var side = a.transform.parent.position - b.transform.parent.position; side.y = 0f;
                var flat = pa - pb; flat.y = 0f;
                if (side.sqrMagnitude < 1e-6f) side = flat;
                if (side.sqrMagnitude > 1e-6f)
                {
                    side.Normalize();
                    float tall = Mathf.Max(a.heightCache, b.heightCache, 0.1f);
                    float contact = a.PushRadius + b.PushRadius;
                    ContactDistance = contact;
                    bool aComes = a.pushFrame >= 0 && Vector3.Dot(pa - a.pushPrev, -side) > 1e-4f;
                    bool bComes = b.pushFrame >= 0 && Vector3.Dot(pb - b.pushPrev, side) > 1e-4f;
                    float along = Vector3.Dot(flat, side);
                    if (Mathf.Abs(pa.y - pb.y) < tall * CollisionSettings.Current.overHeadShare && along < contact) // (one over the other's head: no clash)
                    {
                        float need = contact - along;
                        float shareA = aComes && bComes ? 0.5f : aComes ? 1f : bComes ? 0f : 0.5f;
                        a.hips.position += side * need * shareA;
                        b.hips.position -= side * need * (1f - shareA);
                        if (aComes && bComes && a.Rushing && b.Rushing && Time.time - clashAt > 0.5f) { clashAt = Time.time; BothCame?.Invoke((a.hips.position + b.hips.position) * 0.5f + Vector3.up * a.PushRadius, Vector3.Cross(Vector3.up, side)); } // (both rushed in: they meet in the middle, a clash)
                        (aComes ? a : bComes ? b : null)?.Struck();
                    }
                    a.pushComing = aComes; b.pushComing = bComes;
                }
            }
            a.pushPrev = pa; b.pushPrev = pb;
            a.pushFinal = a.hips.position; b.pushFinal = b.hips.position;
            a.pushFrame = b.pushFrame = Time.frameCount;
            if (shown)
            {
                var gap = a.pushFinal - b.pushFinal;
                float over = Mathf.Abs(gap.y); gap.y = 0f;
                if (over < Mathf.Max(a.heightCache, 0.1f) * CollisionSettings.Current.overHeadShare && gap.magnitude < (a.PushRadius + b.PushRadius) * 0.97f) OverlapFrames++;
            }
        }

        /// <summary>Both came at each other and met in the middle this frame: where, and across (the game shows a clash).</summary>
        public static System.Action<Vector3, Vector3> BothCame;

        /// <summary>Its rush stopped on the other's body: a clip still short of its contact frame jumps to it (the blow
        /// lands as it strikes the body, never hanging in the air before it).</summary>
        private void Struck()
        {
            if (playing == null || actionContact < 0f || playingTime >= actionContact) return;
            playingTime = actionContact;
            actionContact = -1f;
        }
    }
}
