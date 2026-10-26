using T = SquashBot.Combat.DuelAnimTiming;
using A = SquashBot.Combat.DuelAction;

namespace SquashBot.Combat
{
    /// <summary>
    /// The animation map's starting rows (Docs/ANIMASYON_ATAMA_KURALLARI.md, sections 3 and 4): the sections and contact
    /// frames measured on the clips (E:\CLAUDE\scripts\bl_map_clips.py). The game's times never change: a clip is cut or
    /// sped to them.
    /// </summary>
    public static class DuelAnimationDefaults
    {
        public static DuelAnimationRow[] Rows() => new[]
        {
            // moving, defence and reactions (3.5)
            DuelAnimationRow.Of(A.Idle, "idle_fight", 0f, 0f, T.Speed, loop: true, note: "Unarmed Idle"),
            DuelAnimationRow.Of(A.Walk, "walk_fight", 0f, 0f, T.Speed, loop: true, note: "Standing Walk Forward (backwards: played in reverse)"),
            DuelAnimationRow.Of(A.Crouch, "crouch_idle", 0f, 0f, T.Speed, loop: true, note: "Crouch Idle"),
            DuelAnimationRow.Of(A.Block, "block", 0f, 0f, T.Speed, loop: true, note: "Sword And Shield Block Idle"),
            DuelAnimationRow.Of(A.Dodge, "dodge", 0.25f, 0.95f, T.Fit, note: "Standing Dodge Backward: the leap back, fitted to the dodge's time"),
            DuelAnimationRow.Of(A.HitLight, "hit_small", 0f, 0f, T.Speed, 1.15f, note: "Standing React Small From Front"),
            DuelAnimationRow.Of(A.HitHeavy, "stagger", 0f, 0f, T.Speed, 1f, note: "Standing React Large From Front"),
            DuelAnimationRow.Of(A.SweepFall, "sweep_fall", 1.0f, 2.25f, T.Fit, hold: true, note: "Sweep Fall: the fall only, fitted to the rules' time down"),
            DuelAnimationRow.Of(A.GetUpFaceUp, "getup_face_up", 0.9f, 1.9f, T.Fit, note: "Getting Up: the rising, fitted to the rules' get-up (about 2x)"),
            DuelAnimationRow.Of(A.GetUpFaceDown, "getup_face_down", 3.4f, 4.6f, T.Fit, note: "Getting Up (1): from the knees, the push up and rise"),
            DuelAnimationRow.Of(A.Knockout, "ko_fall", 1.0f, 2.6f, T.Speed, 1.8f, hold: true, note: "Standing React Death Backward: down to lying in about 1.1 s, before the next round (the stumble before and the rest after cut)"),
            // the plain chain (3.1): the two sections in turn
            DuelAnimationRow.Of(A.ChainSlash, "chain_slash", 0.62f, 1.05f, T.Contact, 1f, 0.85f, note: "Stable Sword Outward Slash: the cut only"),
            DuelAnimationRow.Of(A.ChainStab, "chain_stab", 0.70f, 1.12f, T.Contact, 1f, 0.92f, note: "Bayonet Stab: the thrust only"),
            // the combo moves (3.2)
            DuelAnimationRow.Of(A.ComboHeavy, "ulti_1", 1.85f, 2.55f, T.Contact, 1f, 2.17f, note: "ULTI_1: one wide turning swing"),
            DuelAnimationRow.Of(A.ComboKnockdown, "ozel_2", 3.05f, 3.8f, T.Contact, 1f, 3.3f, temporary: true, note: "OZEL 2: rising with the fist up (until an uppercut clip)"),
            DuelAnimationRow.Of(A.ComboBreaker, "chain_slash", 0.62f, 1.05f, T.Speed, 1.3f, temporary: true, note: "the cut, 1.3x, and a shoulder push (coded)"),
            // the specials (3.3)
            DuelAnimationRow.Of(A.SpecialFly, "ozel_3", 0.55f, 2.43f, T.Contact, 1f, 1.33f, note: "OZEL 3: the flying punch (the side step cut)"),
            DuelAnimationRow.Of(A.SpecialBackflip, "dodge", 0f, 0f, T.Speed, 0.8f, note: "Standing Dodge Backward at 0.8x, further (the dodge's own clip, slower)"),
            DuelAnimationRow.Of(A.SpecialSkyStrike, "ozel_1", 0f, 2.9f, T.Contact, 1f, 2.23f, note: "OZEL 1: the somersault and the landing (the landing the contact)"),
            DuelAnimationRow.Of(A.SpecialHook, "chain_slash", 0.62f, 1.05f, T.Speed, 1f, temporary: true, note: "stand-in: the cut, and the hook's own effect"),
            DuelAnimationRow.Of(A.SpecialShock, "chain_stab", 0.70f, 1.12f, T.Speed, 1f, temporary: true, note: "stand-in: the thrust, and the shock's own effect"),
            DuelAnimationRow.Of(A.SpecialIceWall, "chain_slash", 0.62f, 1.05f, T.Speed, 1f, temporary: true, note: "stand-in: the cut, and the ice wall"),
            DuelAnimationRow.Of(A.SpecialPatch, "chain_stab", 0.70f, 1.12f, T.Speed, 1f, temporary: true, note: "stand-in: the thrust, and the patch's own effect"),
            DuelAnimationRow.Of(A.SpecialTeleport, "chain_slash", 0.62f, 1.05f, T.Speed, 1f, temporary: true, note: "stand-in: the cut, and the teleport's flash"),
            // the ultimates (3.4)
            DuelAnimationRow.Of(A.UltiCinematic, "ulti_1", 0f, 0f, T.Fit, note: "ULTI_1 whole, over the ultimate's combo time"),
            DuelAnimationRow.Of(A.UltiWhirlwind, "ulti_3", 0f, 0f, T.Fit, note: "ULTI_3: the body itself spins (no storm model); about 1.2x"),
            DuelAnimationRow.Of(A.UltiQuake, "ozel_2", 0.9f, 2.9f, T.Contact, 1f, 1.67f, note: "OZEL 2: the jump and the slam (the landing on the coded slam)"),
            DuelAnimationRow.Of(A.UltiChainLightning, "ulti_2", 1.6f, 3.3f, T.Fit, note: "ULTI_2: the punches (one bolt each; the wind-up cut)"),
            DuelAnimationRow.Of(A.UltiMirror, "ulti_1", 0f, 0.6f, T.Fit, temporary: true, note: "stand-in: ULTI_1's wind-up, and the mirror's shell"),
            DuelAnimationRow.Of(A.UltiMeteor, "ulti_1", 0f, 0.6f, T.Fit, temporary: true, note: "stand-in: ULTI_1's wind-up, and the meteor"),
            DuelAnimationRow.Of(A.UltiCoreBurst, "ulti_1", 0f, 0.6f, T.Fit, temporary: true, note: "stand-in: ULTI_1's wind-up, and the burst"),
            DuelAnimationRow.Of(A.UltiIronBody, "ulti_1", 0f, 0.6f, T.Fit, temporary: true, note: "stand-in: ULTI_1's wind-up, and the iron shell"),
            // the mastery chain's finishing blows by family (section 5; stand-ins until each family's own)
            DuelAnimationRow.Of(A.ChainFinishSword, "ulti_3", 1.0f, 1.95f, T.Contact, 1f, 1.77f, temporary: true, note: "stand-in: ULTI_3's turning swing"),
            DuelAnimationRow.Of(A.ChainFinishSpear, "chain_stab", 0.35f, 1.25f, T.Contact, 1f, 0.92f, temporary: true, note: "stand-in: the thrust with its whole drawing back"),
            DuelAnimationRow.Of(A.ChainFinishAxe, "ulti_1", 0.25f, 0.85f, T.Contact, 1f, 0.53f, temporary: true, note: "stand-in: ULTI_1's first wide swing"),
            DuelAnimationRow.Of(A.ChainFinishHeavy, "ulti_2", 2.6f, 3.25f, T.Contact, 1f, 2.95f, temporary: true, note: "stand-in: ULTI_2's big punch"),
        };
    }
}
