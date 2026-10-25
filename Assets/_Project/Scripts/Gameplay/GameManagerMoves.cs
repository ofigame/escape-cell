using System.Collections;
using SquashBot.Audio;
using SquashBot.Combat;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The move sets in the duel (the scenario's §3.6): each side's special, ultimate and passive. The player's come
    /// from <see cref="MoveSet"/>, a computer rival's from its personality, a live rival's from the server. Every move
    /// works for either side ("mine": the player's); in a live match the server keeps the damage, this game shows it.
    /// </summary>
    public partial class GameManager
    {
        private SpecialKind mySpecial, rivalSpecial;
        private UltiKind myUlti, rivalUlti;
        private PassiveKind myPassive, rivalPassive;

        // Moments the moves set: untouchable, mirroring, the next blow stronger, the first deadly blow of the round survived.
        private float myInvulnUntil, rivalInvulnUntil, myMirrorUntil, myIronUntil, rivalIronUntil;
        private float myNextBlow = 1f, rivalNextBlow = 1f;
        private bool mySturdy, rivalSturdy;

        /// <summary>The two sets for this duel (the rival's from its personality, or the live rival's from the server).</summary>
        private void LoadMoveSets()
        {
            mySpecial = MoveSet.Special; myUlti = MoveSet.Ulti; myPassive = MoveSet.Passive;
            if (rival != null && !string.IsNullOrEmpty(rival.moves)) (rivalSpecial, rivalUlti, rivalPassive) = MoveSet.Parse(rival.moves);
            else if (rival != null) (rivalSpecial, rivalUlti, rivalPassive) = MoveSet.ForBot(BotTuning.Of(rival.seed), rival.seed, rival.duelLevel);
            else { rivalSpecial = SpecialKind.Fly; rivalUlti = UltiKind.Cinematic; rivalPassive = PassiveKind.None; }
            hunt.FoeMirrored = OnRivalMirrored;
        }

        /// <summary>A round starts: the passives' round effects (the master warrior's bar part, the sturdy's spare life).</summary>
        private void RoundPassives()
        {
            myInvulnUntil = rivalInvulnUntil = myMirrorUntil = myIronUntil = rivalIronUntil = 0f;
            hunt.FoeInvulnUntil = hunt.FoeMirrorUntil = 0f;
            myNextBlow = rivalNextBlow = 1f;
            mySturdy = myPassive == PassiveKind.Sturdy;
            rivalSturdy = rivalPassive == PassiveKind.Sturdy;
            hunt.FoeSturdy = rivalSturdy && !LiveActive;
            if (myPassive == PassiveKind.MasterWarrior) myMeter = Mathf.Max(myMeter, Bal.masterWarriorMeter);
            if (rivalPassive == PassiveKind.MasterWarrior && !LiveActive) rivalMeter = Mathf.Max(rivalMeter, Bal.masterWarriorMeter);
        }

        // ---------- The passives in the numbers ----------

        private float MyComboGap => (ComboGap + (myPassive == PassiveKind.ComboMaster ? Bal.comboMasterGap : 0f)) * RuleComboGap;
        private float RivalComboGap => (ComboGap + (rivalPassive == PassiveKind.ComboMaster ? Bal.comboMasterGap : 0f)) * RuleComboGap;
        private float MyCharge => myPassive == PassiveKind.FastCharge ? Bal.fastCharge : 1f;
        private float RivalCharge => rivalPassive == PassiveKind.FastCharge ? Bal.fastCharge : 1f;

        /// <summary>The player's blow factor in the duel: the piercer's armour cut, rage when low, the teleport's next blow.</summary>
        private float MyBlowFactor()
        {
            float k = RuleDamageTimes;
            if (myPassive == PassiveKind.Piercer && duelTheirs.armor > 0f) k *= (1f - (1f - Bal.piercerArmor) * duelTheirs.armor) / (1f - duelTheirs.armor);
            if (myPassive == PassiveKind.Rage && health < Bal.rageBelow) k *= Bal.rageTimes;
            return k;
        }

        /// <summary>The rival's blow factor on foi: rage when it is low, its teleport's next blow.</summary>
        private float RivalBlowFactor()
        {
            float k = rivalNextBlow * RuleDamageTimes;
            rivalNextBlow = 1f;
            if (rivalPassive == PassiveKind.Rage && hunt.ChallengeFoeHealth < Bal.rageBelow) k *= Bal.rageTimes;
            return k;
        }

        /// <summary>The vampire heals 4% of the damage it deals.</summary>
        private void MyVampire(int damagePoints)
        {
            if (myPassive != PassiveKind.Vampire || LiveActive || duelMine.hp <= 0) return;
            health = Mathf.Min(1f, health + Bal.vampireShare * damagePoints / duelMine.hp);
            RefreshHealthBar();
        }

        private void RivalVampire(float shareTaken)
        {
            if (rivalPassive != PassiveKind.Vampire || LiveActive) return;
            hunt.HealFoeQuiet(Bal.vampireShare * shareTaken * duelMine.hp / Mathf.Max(1, hunt.ChallengeFoeMaxHp));
        }

        /// <summary>
        /// foi is about to take <paramref name="share"/> of its health in the duel: the moves may change that — untouchable
        /// (backflip, iron body), the mirror (back at the rival half again), the rival's rage, the sturdy spare life.
        /// Returns the share to take (0: none).
        /// </summary>
        private float MovesOnMyHurt(float share)
        {
            if (LiveActive) return share;
            if (Time.time < myInvulnUntil || Time.time < myIronUntil) { BlockedFx(robot.transform.position); return 0f; }
            if (Time.time < myMirrorUntil)
            {
                // the mirror: the blow goes back at the rival, half again as hard
                if (hunt.ChallengeFoeTile.HasValue) { specialStrike = true; hunt.Strike(hunt.ChallengeFoeTile.Value, Mathf.Max(1, Mathf.RoundToInt(share * duelMine.hp * Bal.mirrorReflect))); specialStrike = false; }
                BlockedFx(robot.transform.position);
                return 0f;
            }
            share *= RivalBlowFactor();
            if (mySturdy && health - share <= 0.001f) { mySturdy = false; skillBar?.Shout(Loc.T("move.p.Sturdy"), new Color(1f, 0.8f, 0.3f)); return Mathf.Max(0f, health - 1f / Mathf.Max(1, duelMine.hp)); }
            RivalVampire(share);
            return share;
        }

        /// <summary>The rival's mirror sent foi's blow back at foi (half again as hard).</summary>
        private void OnRivalMirrored(int damagePoints)
        {
            if (!challengeRun || chPhase != ChPhase.Duel) return;
            hitShareOverride = damagePoints * Bal.mirrorReflect / Mathf.Max(1, duelMine.hp);
            moveHit = true;
            OnBlockImpact(robot.Position);
            moveHit = false;
            hitShareOverride = -1f;
        }

        private void BlockedFx(Vector3 at)
        {
            fx.Burst(at + Vector3.up * 0.8f, new Color(0.7f, 0.9f, 1f), new Color(1.6f, 2.2f, 3f), 16, 4f);
            AudioManager.PlaySfx(Sfx.Blocked, 1f, 1.2f);
            AudioManager.PlaySfx(Sfx.HitSword, 0.6f, 1.35f, 0.08f); // (a blow on the iron body: a "tink", sparks)
        }

        // ---------- Using the moves ----------

        /// <summary>The player pressed the special: its own move (false when it cannot be used now: out of reach).</summary>
        private bool TrySpecial()
        {
            if (!CanUse(true, mySpecial)) { skillBar?.Shout(Loc.T("move.far"), new Color(1f, 1f, 1f, 0.8f)); return false; }
            cameraRig.StartCoroutine(DoSpecial(true, mySpecial));
            return true;
        }

        /// <summary>Whether a special reaches now (its reach in the table: the hook 4 tiles, the shock 3; 0 anywhere).</summary>
        private bool CanUse(bool mine, SpecialKind s)
        {
            if (!hunt.ChallengeFoeTile.HasValue) return false;
            int d = HuntSystem.Chebyshev(robot.Position, hunt.ChallengeFoeTile.Value);
            float reach = Bal.SpecialReach(s);
            return reach <= 0f || d <= reach;
        }

        // (A move hits as hard whatever world gave it: the duel's table holds one value for the bot duel and the live one.)

        private IEnumerator DoSpecial(bool mine, SpecialKind s)
        {
            if (s == SpecialKind.Fly) { yield return SpecialMove(mine); yield break; } // (the old special: the leap and its blow)
            moveBusy = true;
            if (mine) NoteDuel(DuelGoal.Special);
            if (mine && LiveActive) LiveNet.Move(1, (int)s);
            skillBar?.Shout(Loc.T("move.s." + s), mine ? new Color(0.5f, 0.85f, 1f) : new Color(1f, 0.5f, 0.45f));
            float hitTimes = Bal.SpecialHit(s), stun = Bal.SpecialStun(s);
            var me = mine ? robot.Position : hunt.ChallengeFoeTile ?? robot.Position;
            var other = mine ? hunt.ChallengeFoeTile ?? robot.Position : robot.Position;
            var meAt = GridView.ToWorld(me) + Vector3.up * GridView.SurfaceY;
            var otherAt = GridView.ToWorld(other) + Vector3.up * GridView.SurfaceY;
            Transform MeModel() => mine ? robot.Visual : hunt.ChallengeFoeModel;
            Transform OtherModel() => mine ? hunt.ChallengeFoeModel : robot.Visual;
            void Damage(float times) { if (LiveActive || times <= 0f) return; if (mine) HitFoe(times); else HurtMe(times); } // (a move with no damage takes nothing)
            void Hold(float seconds) { if (LiveActive) { if (!mine) strikeCooldown = Mathf.Max(strikeCooldown, seconds); return; } if (mine) hunt.StunFoe(seconds); else strikeCooldown = Mathf.Max(strikeCooldown, seconds); }
            void Reels(float strength) { var sk = OtherModel() != null ? OtherModel().GetComponentInChildren<SkinnedHero>() : null; if (sk != null) { sk.Stagger(meAt, strength); sk.Shudder(meAt, 1f); } }
            switch (s)
            {
                case SpecialKind.Backflip:
                {
                    // a kick at the other (a shove, no damage), then a somersault two tiles back; untouchable on the way
                    var mm = MeModel();
                    if (mm != null) robot.Strike(otherAt);
                    fx.Burst(otherAt + Vector3.up * 0.9f, new Color(1f, 0.9f, 0.6f), new Color(3f, 2.4f, 1.2f), 30, 6f);
                    Shockwave.Create(otherAt + Vector3.up * 0.1f, 1.4f, new Color(1f, 0.9f, 0.6f));
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 1.2f);
                    cameraRig.Shake(0.45f);
                    Damage(hitTimes);
                    Reels(0.8f);
                    if (!LiveActive) Push(!mine, meAt, 1);
                    var to = TileAway(me, other, 2);
                    float safe = Bal.SpecialUntouchable(s);
                    if (mine) myInvulnUntil = Time.time + safe; else rivalInvulnUntil = hunt.FoeInvulnUntil = Time.time + safe;
                    if (MeModel() != null) StatusFx.On(CombatFeel.RootOf(MeModel())).Shine(safe); // (untouchable: it shines)
                    if (to.HasValue) yield return LeapToTile(mine, to.Value, 1.6f, 0);
                    var bfSkin = MeModel() != null ? MeModel().GetComponentInChildren<SkinnedHero>() : null;
                    if (bfSkin != null) { bfSkin.Crouch(1f, 0.35f); fx.Dust(MeModel().position, new Color(0.6f, 0.55f, 0.48f), 12, 3f); } // (landing on a knee)
                    break;
                }
                case SpecialKind.Hook:
                {
                    // a chain thrown, the other dragged in beside the mover, and a hard blow
                    MoveFx.Chain(MeModel(), OtherModel(), 0.75f);
                    AudioManager.PlaySfx(Sfx.Hop, 1f, 0.5f);
                    yield return new WaitForSeconds(0.3f);
                    var land = LandingBeside(me, other);
                    if (land.HasValue && HuntSystem.Chebyshev(land.Value, other) > HuntSystem.Chebyshev(me, other)) land = null; // (already close: never pulled round behind the mover)
                    if (land.HasValue && !LiveActive)
                    {
                        if (mine) yield return hunt.ShoveFoe(land.Value, 0.3f);
                        else { robot.Spawn(grid, land.Value); }
                    }
                    FaceEachOther();
                    var hitAt = (mine ? hunt.ChallengeFoeAt ?? otherAt : robot.transform.position) + Vector3.up * 0.9f;
                    fx.Burst(hitAt, new Color(1f, 0.8f, 0.4f), new Color(3f, 2f, 0.6f), 36, 6f);
                    Shockwave.Create(hitAt - Vector3.up * 0.8f, 1.6f, new Color(1f, 0.8f, 0.4f));
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 0.9f);
                    cameraRig.Shake(0.55f);
                    Damage(hitTimes);
                    Hold(stun);
                    Reels(0.9f);
                    break;
                }
                case SpecialKind.Shock:
                {
                    // three bolts into the other; it stands shaking, stunned
                    for (int i = 0; i < 3; i++)
                    {
                        var target = (mine ? hunt.ChallengeFoeAt ?? otherAt : robot.transform.position) + Vector3.up * 0.9f;
                        MoveFx.Bolt(meAt + Vector3.up * 1.1f, target, new Color(0.55f, 0.85f, 1f), 0.24f + 0.06f * i, 0.45f);
                        fx.Burst(target, new Color(0.6f, 0.9f, 1f), new Color(1.5f, 2.5f, 3f), 22, 5f);
                        AudioManager.PlaySfx(Sfx.Shield, 1f, 0.5f + 0.15f * i);
                        Reels(0.5f);
                        yield return new WaitForSeconds(0.12f);
                    }
                    cameraRig.Shake(0.5f);
                    Damage(hitTimes);
                    Hold(stun);
                    if (OtherModel() != null) StatusFx.On(CombatFeel.RootOf(OtherModel())).Stun(stun); // (sparks circling its head)
                    FaceFor(OtherModel(), FaceMoment.Shocked); // (cross-eyed, the hair on end)
                    cameraRig.StartCoroutine(Sparks(!mine, new Color(0.6f, 0.9f, 1f), stun));
                    break;
                }
                case SpecialKind.IceWall:
                {
                    // ice spikes burst out of the floor from the mover to the other: struck and frozen in a block of ice;
                    // the spikes stand between them a while (nobody walks through)
                    var target = mine ? hunt.ChallengeFoeAt ?? otherAt : robot.transform.position;
                    MoveFx.IceSpikes(meAt + (target - meAt).normalized * 0.6f, target, 2.2f);
                    AudioManager.PlaySfx(Sfx.Blocked, 1f, 1.5f);
                    yield return new WaitForSeconds(0.35f);
                    fx.Burst(target + Vector3.up * 0.8f, new Color(0.7f, 0.95f, 1f), new Color(1.4f, 2.4f, 3f), 40, 6f);
                    Shockwave.Create(target, 1.8f, new Color(0.6f, 0.9f, 1f));
                    cameraRig.Shake(0.5f);
                    Damage(hitTimes);
                    Hold(stun);
                    MoveFx.IceBlock(OtherModel(), stun);
                    if (OtherModel() != null) StatusFx.On(CombatFeel.RootOf(OtherModel())).Freeze(stun);
                    FaceFor(OtherModel(), FaceMoment.Frozen); // (teeth chattering, blue)
                    var between = new GridPos(me.x + System.Math.Sign(other.x - me.x), me.y + System.Math.Sign(other.y - me.y));
                    if (grid.InBounds(between) && grid.IsStandable(between) && !grid.IsOccupied(between) && between != other) cameraRig.StartCoroutine(IceWall(between, 2.2f));
                    break;
                }
                case SpecialKind.Patch:
                {
                    // a column of green light: health back
                    float heal = Bal.SpecialHeal(s);
                    MoveFx.Pillar(meAt, new Color(0.45f, 1f, 0.55f), 0.8f, 7f, 1f);
                    for (int i = 0; i < 3; i++) fx.Burst(meAt + Vector3.up * (0.3f + i * 0.6f), new Color(0.5f, 1f, 0.6f), new Color(1f, 2.4f, 1.2f), 20, 3f);
                    if (mine) { if (!LiveActive) { health = Mathf.Min(1f, health + heal); RefreshHealthBar(); } }
                    else if (!LiveActive) hunt.HealFoe(heal);
                    AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.5f);
                    break;
                }
                case SpecialKind.SkyStrike:
                {
                    // where it lands glows first; then the high jump, a crash down and the other thrown back
                    var land = LandingBeside(other, me);
                    MoveFx.Pillar(otherAt, new Color(1f, 0.6f, 0.2f), 0.9f, 4f, 0.5f);
                    Shockwave.Create(otherAt, 1.6f, new Color(1f, 0.6f, 0.2f));
                    yield return new WaitForSeconds(0.3f);
                    if (land.HasValue) yield return SkyLeap(mine, land.Value);
                    FaceEachOther();
                    var hit = mine ? hunt.ChallengeFoeAt ?? otherAt : robot.transform.position;
                    var skyHeld = MeModel() != null ? MeModel().GetComponentInChildren<HeldWeapon>() : null;
                    if (skyHeld != null && OtherModel() != null) { var sky = CombatLibrary.Family(skyHeld.Family).skyStrike; skyHeld.SwingQuick(sky, 0.02f); CombatFeel.Hit(MeModel(), OtherModel(), sky); }
                    var meSkin = MeModel() != null ? MeModel().GetComponentInChildren<SkinnedHero>() : null;
                    if (meSkin != null) { meSkin.Crouch(1f, 0.35f); meSkin.Squash(1f); }
                    fx.Burst(hit + Vector3.up * 0.4f, new Color(1f, 0.6f, 0.2f), new Color(3f, 1.6f, 0.4f), 50, 8f);
                    Shockwave.Create(hit, 2.8f, new Color(1f, 0.6f, 0.2f));
                    Shockwave.Create(hit, 1.6f, new Color(1f, 0.9f, 0.6f));
                    cameraRig.Shake(0.9f);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 0.6f);
                    Damage(hitTimes); // (it lands by the other: no reach to miss by, as on the server)
                    if (!LiveActive) Fling(!mine, mine ? robot.transform.position : hunt.ChallengeFoeAt ?? hit, 1.2f, 1);
                    break;
                }
                case SpecialKind.Teleport:
                {
                    // gone in a purple flash, out at the other's side at once, and a strike from there; the next blow harder
                    var to = LaneOn ? TileAway(other, me, HuntSystem.DuelGap) ?? LandingBeside(other, me) : TileSide(other, me, HuntSystem.DuelGap); // (the lane duel: out behind it on the line, or in front when its back is to the edge)
                    MoveFx.Pillar(meAt, new Color(0.75f, 0.45f, 1f), 0.6f, 3f, 0.4f);
                    fx.Burst(meAt + Vector3.up * 0.8f, new Color(0.75f, 0.5f, 1f), new Color(2f, 1.2f, 3f), 36, 5f);
                    AudioManager.PlaySfx(Sfx.Hop, 1f, 1.6f);
                    if (to.HasValue && !LiveActive) { if (mine) robot.Spawn(grid, to.Value); else hunt.PlaceFoe(to.Value); }
                    FaceEachOther();
                    yield return new WaitForSeconds(0.12f);
                    var nowAt = mine ? robot.transform.position : hunt.ChallengeFoeAt ?? meAt;
                    MoveFx.Pillar(nowAt, new Color(0.75f, 0.45f, 1f), 0.6f, 3f, 0.4f);
                    fx.Burst(nowAt + Vector3.up * 0.8f, new Color(0.75f, 0.5f, 1f), new Color(2f, 1.2f, 3f), 36, 5f);
                    var hit = mine ? hunt.ChallengeFoeAt ?? otherAt : robot.transform.position;
                    SlashFx.Create(nowAt + Vector3.up * 0.9f, hit + Vector3.up * 0.9f, new Color(0.8f, 0.55f, 1f), 1.8f);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 1.3f);
                    cameraRig.Shake(0.4f);
                    Damage(hitTimes);
                    Reels(0.8f);
                    if (mine) myNextBlow = Bal.SpecialNextBlow(s); else rivalNextBlow = Bal.SpecialNextBlow(s);
                    break;
                }
            }
            yield return new WaitForSeconds(0.2f);
            moveBusy = false;
        }

        /// <summary>Electric sparks crawling over a side for a while (stunned by a shock).</summary>
        private IEnumerator Sparks(bool onMe, Color c, float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until && challengeRun)
            {
                var model = onMe ? robot.Visual : hunt.ChallengeFoeModel;
                if (model == null) yield break;
                var at = model.position + Vector3.up * Random.Range(0.4f, 1.4f);
                MoveFx.Bolt(at + Random.insideUnitSphere * 0.5f, at + Random.insideUnitSphere * 0.5f, c, 0.05f, 0.12f);
                fx.Burst(at, c, c * 2f, 5, 2f);
                yield return new WaitForSeconds(0.1f);
            }
        }

        /// <summary>The player pressed the ultimate: its own.</summary>
        private void UseUlti(bool mine, UltiKind u, float scale = 1f)
        {
            // a marked ultimate (the meteor) is dodged as it lands, not as it starts: the bot times its dodge to the mark
            botUltiDodgeWait = Bal.UltiTelegraph(u) > 0f ? Mathf.Max(0.15f, Bal.UltiTelegraph(u) - Def.dodgeInvulnerable * 0.5f) : 0.15f;
            UltiBegins(mine);
            if (u == UltiKind.Cinematic && scale >= 0.99f) { cameraRig.StartCoroutine(Ultimate(mine, leap: true)); return; }
            cameraRig.StartCoroutine(DoUlti(mine, u, scale));
        }

        private IEnumerator DoUlti(bool mine, UltiKind u, float scale)
        {
            if (u == UltiKind.Copy)
            {
                // the other's own ultimate, at 90%
                var copied = mine ? rivalUlti : myUlti;
                if (copied == UltiKind.Copy) copied = UltiKind.Cinematic;
                skillBar?.Shout(Loc.T("move.u.Copy"), new Color(0.9f, 0.9f, 1f));
                if (mine && LiveActive) LiveNet.Move(4, (int)UltiKind.Copy);
                var self = mine ? robot.Visual : hunt.ChallengeFoeModel;
                MoveFx.Shell(self, new Color(0.9f, 0.9f, 1f), 0.6f, 0.4f);
                CombatFeel.UltiStart(self);
                if (copied == UltiKind.Cinematic) { yield return Ultimate(mine, leap: true); yield break; }
                yield return DoUlti(mine, copied, Bal.copyShare);
                yield break;
            }
            moveBusy = true;
            if (mine) NoteDuel(DuelGoal.Ultimate);
            if (mine && LiveActive && scale >= 0.99f) LiveNet.Move(4, (int)u);
            CombatFeel.UltiStart(mine ? robot.Visual : hunt.ChallengeFoeModel); // (the screen dims, the pictures slow a breath, the caster glows)
            skillBar?.Shout(Loc.T("move.u." + u), mine ? new Color(0.8f, 0.5f, 1f) : new Color(1f, 0.4f, 0.4f));
            AudioManager.PlaySfx(Sfx.Win, 1f, 0.6f);
            if (colosseum != null) colosseum.Cheer(mine ? 0 : 1, 1f);
            float power = scale; // (the copy's share; else the table's own numbers)
            float hitTimes = Bal.UltiHit(u);
            Transform Self() => mine ? robot.Visual : hunt.ChallengeFoeModel;
            Transform Other() => mine ? hunt.ChallengeFoeModel : robot.Visual;
            Vector3 OtherAt() => mine ? hunt.ChallengeFoeAt ?? robot.transform.position : robot.transform.position;
            Vector3 MeAt() => mine ? robot.transform.position : hunt.ChallengeFoeAt ?? robot.transform.position;
            int Gap() => hunt.ChallengeFoeTile.HasValue ? HuntSystem.Chebyshev(robot.Position, hunt.ChallengeFoeTile.Value) : 99;
            void Damage(float times) { if (LiveActive || times <= 0f) return; if (mine) HitFoe(times * power, BlowKind.Ulti, Def.ultiCost * Mathf.Min(1f, times / UltimateHit)); else HurtMe(times * power, BlowKind.Ulti, Def.ultiCost * Mathf.Min(1f, times / UltimateHit)); }
            bool Down() => mine ? hunt.ChallengeFoeDown : !robot.IsAlive;
            bool Flings() => !Down() && Landed(mine); // (blocked or dodged: nobody is thrown)
            void Hold(float seconds) { if (LiveActive) return; if (mine) hunt.StunFoe(seconds); else strikeCooldown = Mathf.Max(strikeCooldown, seconds); }
            switch (u)
            {
                case UltiKind.Quake:
                {
                    // a leap and a slam: the floor bursts in rings, rocks fly, the other is thrown high
                    Hold(Bal.UltiHold(u));
                    yield return new WaitForSeconds(0.15f);
                    var at = MeAt();
                    for (int i = 0; i < 4; i++) Shockwave.Create(at, 2.5f + i * 2.2f, new Color(0.85f, 0.65f, 0.4f));
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i * Mathf.PI * 2f / 10f;
                        var p = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(1.2f, 4f);
                        fx.Burst(p + Vector3.up * 0.2f, new Color(0.55f, 0.45f, 0.35f), new Color(0.9f, 0.7f, 0.5f), 14, 7f);
                    }
                    fx.Burst(OtherAt() + Vector3.up * 0.3f, new Color(0.6f, 0.5f, 0.4f), new Color(1f, 0.8f, 0.6f), 50, 10f);
                    cameraRig.Shake(1.5f);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 0.4f);
                    Damage(hitTimes);
                    CombatFeel.UltiFinal(Self(), Other());
                    if (mine) cameraRig.StartCoroutine(SlowFoe(Bal.UltiSlow(u))); else strikeCooldown = Mathf.Max(strikeCooldown, Bal.UltiSlow(u)); // (the other slowed a moment: this game's own)
                    if (Flings()) yield return BigMoment(!mine, 2.4f, 2);
                    break;
                }
                case UltiKind.Whirlwind:
                {
                    // three seconds of a spinning storm round the mover, the other drawn in and cut again and again, then flung
                    var spinner = Self();
                    MoveFx.Whirl(spinner, new Color(0.7f, 0.95f, 1f), 3.2f);
                    Hold(Bal.UltiHold(u));
                    int spins = Bal.UltiHits(u);
                    for (int i = 0; i < spins && challengeRun && chPhase == ChPhase.Duel && !Down(); i++)
                    {
                        float until = Time.time + 0.375f;
                        while (Time.time < until) { if (spinner != null) spinner.Rotate(0f, 1100f * Time.deltaTime, 0f, Space.World); yield return null; }
                        var o = OtherAt();
                        SlashFx.Create(o + Vector3.left * 1f + Vector3.up * 0.9f, o + Vector3.right * 1f + Vector3.up * 0.9f, new Color(0.7f, 0.95f, 1f), 1.8f);
                        fx.Burst(o + Vector3.up * 0.9f, new Color(0.8f, 0.95f, 1f), new Color(2f, 2.6f, 3f), 14, 5f);
                        AudioManager.PlaySfx(Sfx.Hop, 0.8f, 1.8f);
                        cameraRig.Shake(0.25f);
                        { Damage(hitTimes); var sk = Other() != null ? Other().GetComponentInChildren<SkinnedHero>() : null; if (sk != null) sk.Shudder(MeAt(), 1f); }
                    }
                    FaceEachOther();
                    if (Flings()) { CombatFeel.UltiFinal(Self(), Other()); yield return BigMoment(!mine, 2f, 2); }
                    break;
                }
                case UltiKind.IronBody:
                {
                    // the body turns to iron with a clang that pushes the other back (no damage); untouchable a while
                    float iron = Bal.UltiSeconds(u);
                    if (mine) myIronUntil = Time.time + iron; else rivalIronUntil = hunt.FoeInvulnUntil = Time.time + iron;
                    if (Self() != null) StatusFx.On(CombatFeel.RootOf(Self())).Shine(iron);
                    MoveFx.Shell(Self(), new Color(0.8f, 0.82f, 0.9f), iron, 0.4f);
                    MoveFx.Pillar(MeAt(), new Color(0.85f, 0.85f, 0.95f), 0.9f, iron, 0.6f);
                    cameraRig.StartCoroutine(Glow(mine, new Color(0.85f, 0.85f, 0.9f), iron));
                    Shockwave.Create(MeAt(), 4f, new Color(0.85f, 0.85f, 0.95f));
                    AudioManager.PlaySfx(Sfx.Shield, 1f, 0.5f);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 0.7f);
                    cameraRig.Shake(0.8f);
                    Damage(hitTimes); // (none in the table)
                    if (Gap() <= HuntSystem.DuelGap + 2 && !LiveActive) Fling(!mine, MeAt(), 1.2f, 2);
                    break;
                }
                case UltiKind.Mirror:
                {
                    // a shell of mirror glass a while: what comes in goes back half again as hard
                    float glass = Bal.UltiSeconds(u);
                    if (mine) myMirrorUntil = Time.time + glass; else hunt.FoeMirrorUntil = Time.time + glass;
                    MoveFx.Shell(Self(), new Color(0.65f, 0.95f, 1f), glass, 0.35f);
                    cameraRig.StartCoroutine(Glow(mine, new Color(0.7f, 0.95f, 1f), glass));
                    fx.Burst(MeAt() + Vector3.up * 0.9f, new Color(0.7f, 0.95f, 1f), new Color(2f, 2.8f, 3f), 50, 7f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        var d = new Vector3(Mathf.Cos(a), 0.3f, Mathf.Sin(a)) * 2.5f;
                        MoveFx.Bolt(MeAt() + Vector3.up * 0.9f, MeAt() + Vector3.up * 0.9f + d, new Color(0.7f, 0.95f, 1f), 0.08f, 0.3f);
                    }
                    AudioManager.PlaySfx(Sfx.Shield, 1f, 1.4f);
                    cameraRig.Shake(0.5f);
                    Damage(hitTimes); // (none in the table: the mirror only sends blows back)
                    break;
                }
                case UltiKind.ChainLightning:
                {
                    // the sky darkens a moment: three bolts in a chain from the mover, then one huge bolt from the sky,
                    // and the other is flung far
                    Hold(Bal.UltiHold(u));
                    var c = new Color(0.55f, 0.85f, 1f);
                    int bolts = Bal.UltiHits(u);
                    for (int i = 0; i < bolts && challengeRun && chPhase == ChPhase.Duel && !Down(); i++)
                    {
                        var o = OtherAt() + Vector3.up * 0.9f;
                        MoveFx.Bolt(MeAt() + Vector3.up * 1.2f, o, c, 0.18f + 0.06f * i, 0.35f);
                        MoveFx.Bolt(o + new Vector3(Random.Range(-2f, 2f), 9f, Random.Range(-2f, 2f)), o, c, 0.14f, 0.3f);
                        fx.Burst(o, c, new Color(1.6f, 2.4f, 3f), 30, 6f);
                        AudioManager.PlaySfx(Sfx.Shield, 1f, 0.5f + 0.1f * i);
                        cameraRig.Shake(0.4f + 0.1f * i);
                        var sk = Other() != null ? Other().GetComponentInChildren<SkinnedHero>() : null;
                        if (sk != null) { sk.Stagger(MeAt(), 0.5f); sk.Shudder(MeAt(), 1f); }
                        Damage(hitTimes);
                        yield return new WaitForSeconds(0.25f);
                    }
                    if (!Down() && challengeRun)
                    {
                        var o = OtherAt();
                        MoveFx.Bolt(o + Vector3.up * 18f, o, new Color(0.7f, 0.9f, 1f), 0.75f, 0.6f);
                        MoveFx.Pillar(o, new Color(0.6f, 0.85f, 1f), 1.2f, 18f, 0.6f);
                        fx.Burst(o + Vector3.up * 0.5f, c, new Color(2f, 3f, 4f), 80, 11f);
                        Shockwave.Create(o, 4.5f, c);
                        Shockwave.Create(o, 2.5f, Color.white);
                        AudioManager.PlaySfx(Sfx.Impact, 1f, 0.45f);
                        cameraRig.Shake(1.6f);
                        CombatFeel.UltiFinal(Self(), Other());
                        if (Flings()) yield return BigMoment(!mine, 3.2f, 3);
                    }
                    break;
                }
                case UltiKind.Meteor:
                {
                    // the other is marked: a burning rock falls on it after the mark's moment; only a dodge's untouchable
                    // moment as it lands escapes it (as on the server, which knows no positions: it follows the other)
                    float mark = Bal.UltiTelegraph(u);
                    Hold(Bal.UltiHold(u));
                    var markAt = OtherAt();
                    MoveFx.Meteor(markAt, mark, 1.9f);
                    for (float e = 0f; e < mark; e += 0.25f) { Shockwave.Create(OtherAt(), 2.6f, new Color(1f, 0.35f, 0.15f)); yield return new WaitForSeconds(0.25f); }
                    markAt = OtherAt();
                    fx.Burst(markAt + Vector3.up * 0.4f, new Color(1f, 0.5f, 0.15f), new Color(3f, 1.4f, 0.3f), 90, 12f);
                    fx.Burst(markAt + Vector3.up * 0.4f, new Color(0.3f, 0.2f, 0.15f), new Color(0.4f, 0.3f, 0.2f), 40, 8f);
                    MoveFx.Blast(markAt + Vector3.up * 0.5f, new Color(1f, 0.5f, 0.15f), 3.5f, 0.05f);
                    Shockwave.Create(markAt, 5f, new Color(1f, 0.45f, 0.2f));
                    Shockwave.Create(markAt, 3f, new Color(1f, 0.8f, 0.4f));
                    cameraRig.Shake(1.7f);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 0.35f);
                    Damage(hitTimes);
                    if (LiveActive || Landed(mine)) { CombatFeel.UltiFinal(Self(), Other()); if (Flings()) yield return BigMoment(!mine, 3f, 3); }
                    else ScreenFlash.White(0.05f); // (dodged: still a great blast)
                    break;
                }
                case UltiKind.CoreBurst:
                {
                    // the core gathers at the chest, then bursts: a blast of light across the floor, health back
                    Hold(Bal.UltiHold(u));
                    MoveFx.Blast(MeAt() + Vector3.up * 0.9f, new Color(0.4f, 0.95f, 1f), 5f, 0.6f);
                    for (int i = 0; i < 4; i++) { fx.Burst(MeAt() + Vector3.up * 0.9f, new Color(0.4f, 0.95f, 1f), new Color(1.2f, 3f, 3.4f), 10, 2f); yield return new WaitForSeconds(0.15f); }
                    fx.Burst(MeAt() + Vector3.up * 0.8f, new Color(0.4f, 0.95f, 1f), new Color(1.2f, 3f, 3.4f), 100, 12f);
                    Shockwave.Create(MeAt(), 5.5f, new Color(0.4f, 0.95f, 1f));
                    Shockwave.Create(MeAt(), 3f, Color.white);
                    cameraRig.Shake(1.4f);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 0.5f);
                    Damage(hitTimes);
                    CombatFeel.UltiFinal(Self(), Other());
                    float back = Bal.UltiHeal(u);
                    if (!LiveActive) { if (mine) { health = Mathf.Min(1f, health + back); RefreshHealthBar(); } else hunt.HealFoe(back); }
                    if (Flings()) yield return BigMoment(!mine, 2.6f, 3);
                    break;
                }
                default:
                    yield return Ultimate(mine, leap: true);
                    break;
            }
            moveBusy = false;
        }

        // ---------- Helpers ----------

        /// <summary>A tile <paramref name="n"/> away from the other, straight back from it (or the nearest free one short of it).</summary>
        private GridPos? TileAway(GridPos me, GridPos other, int n)
        {
            int dx = System.Math.Sign(me.x - other.x), dy = System.Math.Sign(me.y - other.y);
            if (dx == 0 && dy == 0) dy = 1;
            for (int k = n; k >= 1; k--)
            {
                var p = new GridPos(me.x + dx * k, me.y + dy * k);
                if (grid.InBounds(p) && grid.IsStandable(p) && !grid.IsOccupied(p)) return p;
            }
            return null;
        }

        /// <summary>A tile <paramref name="n"/> to the side of the line between them.</summary>
        private GridPos? TileSide(GridPos me, GridPos other, int n)
        {
            int dx = System.Math.Sign(other.x - me.x), dy = System.Math.Sign(other.y - me.y);
            foreach (var side in new[] { new GridPos(-dy, dx), new GridPos(dy, -dx) })
                for (int k = n; k >= 1; k--)
                {
                    var p = new GridPos(me.x + side.x * k, me.y + side.y * k);
                    if (grid.InBounds(p) && grid.IsStandable(p) && !grid.IsOccupied(p)) return p;
                }
            return null;
        }

        /// <summary>The mover leaps to a tile (a stand-in does the flying; the mover lands there).</summary>
        private IEnumerator LeapToTile(bool mine, GridPos land, float height, int stunt = -1)
        {
            var model = mine ? robot.Visual : hunt.ChallengeFoeModel;
            if (model == null) yield break;
            var dbl = StuntDouble.Make(model);
            try
            {
                AudioManager.PlaySfx(Sfx.Hop, 1f, 1.1f);
                yield return dbl.Leap(GridView.ToWorld(land) + Vector3.up * GridView.SurfaceY, height, 0.5f, stunt);
                if (mine) robot.Spawn(grid, land); else if (!hunt.ChallengeFoeDown) hunt.PlaceFoe(land);
                FaceEachOther();
            }
            finally { if (dbl != null) dbl.Done(); }
        }

        /// <summary>
        /// SKY STRIKE's leap: up high (a body and a half over the floor) toward the landing, a hang at the top (the weapon
        /// raised over the head, the landing's red circle showing), a straight dive and the crash (the mover lands there).
        /// </summary>
        private IEnumerator SkyLeap(bool mine, GridPos land)
        {
            var model = mine ? robot.Visual : hunt.ChallengeFoeModel;
            if (model == null) yield break;
            var skin = model.GetComponentInChildren<SkinnedHero>();
            float height = skin != null ? Mathf.Max(1f, (skin.HeadPoint.y - model.position.y) * 1.5f) : 4f;
            var dbl = StuntDouble.Make(model);
            try
            {
                AudioManager.PlaySfx(Sfx.SwishHeavy, 1f, 1.3f);
                var from = dbl.transform.position;
                var to = GridView.ToWorld(land) + Vector3.up * GridView.SurfaceY;
                var apex = Vector3.Lerp(from, to, 0.8f) + Vector3.up * height;
                var flat = to - from; flat.y = 0f;
                var face = flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat) : dbl.transform.rotation;
                for (float e = 0f; e < 0.32f; e += Time.unscaledDeltaTime)
                {
                    float k = Mathf.SmoothStep(0f, 1f, e / 0.32f);
                    dbl.transform.position = Vector3.Lerp(from, apex, k);
                    dbl.transform.rotation = face * Quaternion.Euler(-20f * k, 0f, 0f);
                    yield return null;
                }
                Shockwave.Create(to, 1.2f, new Color(1f, 0.25f, 0.2f)); // (where it will land: a red ring)
                for (float e = 0f; e < 0.15f; e += Time.unscaledDeltaTime) { dbl.transform.position = apex + Vector3.up * Mathf.Sin(e / 0.15f * Mathf.PI) * 0.05f * height; yield return null; }
                for (float e = 0f; e < 0.12f; e += Time.unscaledDeltaTime)
                {
                    float k = e / 0.12f; k *= k; // (diving faster and faster)
                    dbl.transform.position = Vector3.Lerp(apex, to, k);
                    dbl.transform.rotation = face * Quaternion.Euler(Mathf.Lerp(-20f, 25f, k), 0f, 0f);
                    yield return null;
                }
                if (mine) robot.Spawn(grid, land); else if (!hunt.ChallengeFoeDown) hunt.PlaceFoe(land);
            }
            finally { if (dbl != null) dbl.Done(); }
        }

        /// <summary>A wall of ice on a tile for a while (nobody steps through it).</summary>
        private IEnumerator IceWall(GridPos p, float seconds)
        {
            grid.SetOccupied(p, true);
            var at = GridView.ToWorld(p) + Vector3.up * GridView.SurfaceY;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(wall.GetComponent<Collider>());
            wall.name = "IceWall";
            wall.transform.position = at + Vector3.up * 1f;
            wall.transform.localScale = new Vector3(1.4f, 2f, 1.4f);
            wall.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            var iceMat = MaterialFactory.CreateTransparent(new Color(0.6f, 0.9f, 1f, 0.75f), new Color(0.4f, 0.8f, 1.4f));
            wall.GetComponent<Renderer>().sharedMaterial = iceMat;
            OwnedMaterials.Own(wall, iceMat);
            fx.Burst(at + Vector3.up * 0.6f, new Color(0.7f, 0.95f, 1f), new Color(1.4f, 2.4f, 3f), 26, 4f);
            AudioManager.PlaySfx(Sfx.Blocked, 1f, 1.5f);
            yield return new WaitForSeconds(seconds);
            if (wall != null) Destroy(wall);
            if (robot.Position != p && hunt.ChallengeFoeTile != p) grid.SetOccupied(p, false);
        }

        /// <summary>The rival slowed a while (its wind-ups twice as long).</summary>
        private IEnumerator SlowFoe(float seconds)
        {
            float was = hunt.FoeReactScale;
            hunt.FoeReactScale = was * 2f;
            yield return new WaitForSeconds(seconds);
            hunt.FoeReactScale = was;
        }

        /// <summary>A glow round a side for a while (the iron body, the mirror).</summary>
        private IEnumerator Glow(bool mine, Color c, float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until && challengeRun)
            {
                var at = mine ? robot.transform.position : hunt.ChallengeFoeAt ?? robot.transform.position;
                fx.Burst(at + Vector3.up * 0.8f, c, c * 2f, 6, 2f);
                yield return new WaitForSeconds(0.25f);
            }
        }

        /// <summary>The computer rival's special, by its set (when it reaches).</summary>
        private bool RivalSpecial()
        {
            if (!CanUse(false, rivalSpecial)) return false;
            cameraRig.StartCoroutine(DoSpecial(false, rivalSpecial));
            return true;
        }
    }
}
