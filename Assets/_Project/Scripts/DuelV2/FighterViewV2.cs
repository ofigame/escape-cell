using SquashBot.Audio;
using SquashBot.Combat;
using SquashBot.Gameplay;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// One v2 fighter on screen: its root moved along the line where the rules put it and turned to face the other;
    /// its weapon swung so the blow's picture touches at the rules' strike; the struck body's answer by the result
    /// (a hit's reaction and sparks, a block's raised shield and clang, a perfect block's flash, a clash's sparks between
    /// them, a launch, a fall, a knock-out); the shield held up while the rules hold it; the stun's glint and the
    /// advantage's glint on the weapon (hints the prototype can switch off).
    /// </summary>
    public class FighterViewV2
    {
        public readonly Transform Root, Look;
        public readonly SkinnedHero Skin;
        public HeldWeapon Weapon { get; private set; }
        private ShieldHold shield;
        private readonly GameManager game;
        private readonly Vector3 centre, line;
        private readonly float scale;
        private FighterState shown = FighterState.Idle;
        public int Family;

        /// <summary>Another weapon in the hand (the weapons' sheet): the old one gone, the new one fitted from scratch.</summary>
        public void Hold(Data.WeaponDef w)
        {
            if (Look == null || w == null) return;
            foreach (var h in Look.GetComponentsInChildren<HeldWeapon>(true)) Object.Destroy(h.gameObject);
            Weapon = HeldWeapon.Attach(Look, w);
            weaponLength0 = -1f; weaponFitted = -1f;
        }
        private Vector3 weaponScale0;
        private float weaponLength0 = -1f, weaponFitted = -1f;

        /// <summary>
        /// The weapon at its seen length (Docs 7.2: a share of the fighter's height; the scale only, never the model): the
        /// rules' reach is worked out from the same length, so what is seen is what reaches.
        /// </summary>
        public void FitWeapon(float length)
        {
            if (Weapon == null || length <= 0f || Mathf.Abs(length - weaponFitted) < 0.001f) return;
            if (weaponLength0 < 0f) { weaponScale0 = Weapon.transform.localScale; weaponLength0 = LengthOf(Weapon.transform); }
            if (weaponLength0 <= 0.0001f) return;
            Weapon.transform.localScale = weaponScale0 * (length / weaponLength0);
            weaponFitted = length;
        }

        /// <summary>A weapon's longest extent in the world (its meshes, in its own frame, at its present scale).</summary>
        public static float LengthOf(Transform w)
        {
            var min = Vector3.one * float.MaxValue; var max = Vector3.one * float.MinValue;
            bool any = false;
            foreach (var mf in w.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = w.InverseTransformPoint(mf.transform.TransformPoint(c));
                    min = Vector3.Min(min, p); max = Vector3.Max(max, p); any = true;
                }
            }
            if (!any) return 0f;
            var size = Vector3.Scale(max - min, w.lossyScale);
            return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        }

        public FighterViewV2(GameManager game, Transform root, Transform look, HeldWeapon weapon, Vector3 centre, Vector3 line, float scale)
        {
            this.game = game; Root = root; Look = look; Weapon = weapon; this.centre = centre; this.line = line; this.scale = scale;
            Skin = look != null ? look.GetComponentInChildren<SkinnedHero>() : null;
            shield = look != null ? look.GetComponentInChildren<ShieldHold>() : null;
        }

        public Vector3 World(float x) => new Vector3(centre.x, Root != null ? Root.position.y : centre.y, centre.z) + line * x;

        /// <summary>Head height in the world (for labels, sparks).</summary>
        public Vector3 Head => Root.position + Vector3.up * 2.1f * scale / 3.5f * 1.0f + Vector3.up * scale * 0.35f;
        public Vector3 Chest => Root.position + Vector3.up * scale * 0.42f;

        public void Update(Fighter f, Fighter o, DuelSim sim)
        {
            if (Root == null) return;
            var at = World(f.x);
            Root.position = new Vector3(at.x, Root.position.y, at.z);
            var face = line * f.facing;
            if (f.state != FighterState.Airborne && f.state != FighterState.Down && f.state != FighterState.KO)
            {
                Root.rotation = Quaternion.Slerp(Root.rotation, Quaternion.LookRotation(face), 1f - Mathf.Exp(-18f * Time.deltaTime));
                // the body straight on its root (the old duel's lobby turned foi's body to the camera and left it so)
                if (Look != null && Look != Root && Look.IsChildOf(Root)) Look.localRotation = Quaternion.Slerp(Look.localRotation, Quaternion.identity, 1f - Mathf.Exp(-18f * Time.deltaTime));
            }
            if (shield == null && Look != null) shield = Look.GetComponentInChildren<ShieldHold>();
            if (shield != null) shield.Guarding = f.ShieldUp;
            shown = f.state;
        }

        // ---------- the events ----------

        public void OnSwing(Fighter f, DuelEvent e, float windup)
        {
            if (Weapon == null) return;
            switch (e.move)
            {
                case MoveKind.PowerHit:
                    if (Skin != null) Skin.PlayAction(DuelAction.ComboHeavy, 0f, windup);
                    Weapon.SwingToContact(windup);
                    break;
                case MoveKind.Knockdown:
                    if (Skin != null) Skin.PlayAction(DuelAction.ComboKnockdown, 0f, windup);
                    Weapon.SwingToContact(windup);
                    break;
                case MoveKind.Special:
                    Weapon.Spin();
                    if (Skin != null) Skin.Hop(0.6f, windup + 0.1f);
                    break;
                default:
                    Weapon.SwingToContact(windup);
                    break;
            }
            DuelSoundsV2.Play(f.family == 0 ? DuelSoundV2.SwingSword : f.family == 1 ? DuelSoundV2.SwingSpear : f.family == 2 ? DuelSoundV2.SwingAxe : DuelSoundV2.SwingHeavy, 0.7f, 0.06f);
        }

        /// <summary>This fighter was struck (or its blow was met): its body answers.</summary>
        public void OnStruck(Fighter me, Fighter by, FighterViewV2 byView, DuelEvent e, DuelCoreSettings s)
        {
            var from = byView.Root.position;
            switch (e.result)
            {
                case HitResult.Hit:
                    var strength = e.move == MoveKind.PowerHit ? HitStrength.Heavy : e.move == MoveKind.Knockdown ? HitStrength.Launch : e.move == MoveKind.Ulti ? HitStrength.Finisher : by.family >= 2 ? HitStrength.Medium : HitStrength.Light;
                    if (e.move == MoveKind.Knockdown) { if (Skin != null) Skin.LaunchUp(from, s.airSeconds * s.tempo, s.downSeconds * s.tempo); }
                    else if (e.move == MoveKind.AirHit) { if (Skin != null) Skin.AirHit(from); }
                    else if (Skin != null) Skin.React(HitDirection.Horizontal_RightToLeft, HitHeight.Mid, strength, from, 1f);
                    game.V2Burst(Chest, new Color(1f, 0.85f, 0.4f), new Color(3f, 2.2f, 0.8f), e.move == MoveKind.Chain ? 10 : 24, e.move == MoveKind.Chain ? 4f : 7f);
                    DuelSoundsV2.Play(by.family == 0 ? DuelSoundV2.HitSword : by.family == 1 ? DuelSoundV2.HitSpear : by.family == 2 ? DuelSoundV2.HitAxe : DuelSoundV2.HitHeavy, 1f);
                    if (strength >= HitStrength.Heavy) DuelSoundsV2.Play(DuelSoundV2.Smash, 0.8f); // (a power hit, a knock-down, the ultimate: weight under it)
                    if (s.showAdvantageHints && me.state == FighterState.HitStun) StatusFx.On(CombatFeel.RootOf(Look) ?? Root).Stun(Mathf.Max(0.15f, me.stateEnd - me.stateTime));
                    break;
                case HitResult.Blocked:
                    if (shield != null) shield.Raise();
                    if (Skin != null) Skin.Shudder(from, 0.3f);
                    game.V2Burst(Chest + (from - Root.position).normalized * 0.4f * scale / 3.5f, new Color(0.8f, 0.9f, 1f), new Color(2f, 2.4f, 3f), 8, 4f);
                    DuelSoundsV2.Play(DuelSoundV2.Block, 0.9f);
                    break;
                case HitResult.Perfect:
                    if (shield != null) shield.Raise();
                    game.V2Burst(Chest, Color.white, new Color(3f, 3f, 3f), 26, 6f);
                    DuelSoundsV2.Play(DuelSoundV2.Parry, 1f, 0.02f);
                    if (byView.Skin != null) byView.Skin.Stagger(Root.position, 1f);
                    break;
            }
        }

        public void OnClash(FighterViewV2 other)
        {
            var mid = (Chest + other.Chest) * 0.5f;
            game.V2Burst(mid, new Color(1f, 0.95f, 0.7f), new Color(3.2f, 3f, 2f), 34, 7f);
            game.V2Ring(new Vector3(mid.x, Root.position.y, mid.z), 1.4f, new Color(1f, 0.95f, 0.8f));
            DuelSoundsV2.Play(DuelSoundV2.Clash, 1f);
            if (Skin != null) Skin.Shudder(other.Root.position, 0.6f);
            if (other.Skin != null) other.Skin.Shudder(Root.position, 0.6f);
        }

        public void OnFall(Vector3 from, float lie)
        {
            if (Skin != null) Skin.Launch(from, 0.6f * scale / 3.5f, lie);
            game.V2Dust(Root.position, new Color(0.6f, 0.55f, 0.48f), 16, 3f);
            DuelSoundsV2.Play(DuelSoundV2.BodyFall, 0.9f);
        }

        public void OnKO(Vector3 from)
        {
            if (Skin != null) Skin.Launch(from, 1.4f * scale / 3.5f, 3f);
            game.V2Burst(Chest, new Color(1f, 0.8f, 0.4f), new Color(3f, 2f, 0.6f), 40, 8f);
            DuelSoundsV2.Play(DuelSoundV2.KO, 1f, 0.02f);
        }

        public void OnDodge(float seconds) { if (Skin != null) Skin.DodgeClip(seconds); }
        public void OnJump(float seconds) { if (Skin != null) Skin.Hop(1.1f, seconds); DuelSoundsV2.Play(DuelSoundV2.Jump, 0.6f); }
        public void OnUlti(float seconds) { if (Skin != null) Skin.PlayAction(DuelAction.UltiCinematic, seconds); CombatFeel.UltiStart(Look); }
        public void OnChainOpen(DuelCoreSettings s) { if (Weapon != null && s.showAdvantageHints) Weapon.Tremble(); }
        public void OnChainBroken() { if (Weapon != null) Weapon.Recoil(); }
    }
}
