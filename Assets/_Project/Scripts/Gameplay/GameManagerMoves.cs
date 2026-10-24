using System.Collections;
using SquashBot.Audio;
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
            if (myPassive == PassiveKind.MasterWarrior) myMeter = Mathf.Max(myMeter, 1f);
            if (rivalPassive == PassiveKind.MasterWarrior && !LiveActive) rivalMeter = Mathf.Max(rivalMeter, 1f);
        }

        // ---------- The passives in the numbers ----------

        private float MyComboGap => ComboGap + (myPassive == PassiveKind.ComboMaster ? 0.3f : 0f);
        private float RivalComboGap => ComboGap + (rivalPassive == PassiveKind.ComboMaster ? 0.3f : 0f);
        private float MyCharge => myPassive == PassiveKind.FastCharge ? 1.15f : 1f;
        private float RivalCharge => rivalPassive == PassiveKind.FastCharge ? 1.15f : 1f;

        /// <summary>The player's blow factor in the duel: the piercer's armour cut, rage when low, the teleport's next blow.</summary>
        private float MyBlowFactor()
        {
            float k = 1f;
            if (myPassive == PassiveKind.Piercer && duelTheirs.armor > 0f) k *= (1f - 0.75f * duelTheirs.armor) / (1f - duelTheirs.armor);
            if (myPassive == PassiveKind.Rage && health < 0.3f) k *= 1.15f;
            return k;
        }

        /// <summary>The rival's blow factor on foi: rage when it is low, its teleport's next blow.</summary>
        private float RivalBlowFactor()
        {
            float k = rivalNextBlow;
            rivalNextBlow = 1f;
            if (rivalPassive == PassiveKind.Rage && hunt.ChallengeFoeHealth < 0.3f) k *= 1.15f;
            return k;
        }

        /// <summary>The vampire heals 4% of the damage it deals.</summary>
        private void MyVampire(int damagePoints)
        {
            if (myPassive != PassiveKind.Vampire || LiveActive || duelMine.hp <= 0) return;
            health = Mathf.Min(1f, health + 0.04f * damagePoints / duelMine.hp);
            RefreshHealthBar();
        }

        private void RivalVampire(float shareTaken)
        {
            if (rivalPassive != PassiveKind.Vampire || LiveActive) return;
            hunt.HealFoeQuiet(0.04f * shareTaken * duelMine.hp / Mathf.Max(1, hunt.ChallengeFoeMaxHp));
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
                if (hunt.ChallengeFoeTile.HasValue) { specialStrike = true; hunt.Strike(hunt.ChallengeFoeTile.Value, Mathf.Max(1, Mathf.RoundToInt(share * duelMine.hp * 1.5f))); specialStrike = false; }
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
            hitShareOverride = damagePoints * 1.5f / Mathf.Max(1, duelMine.hp);
            moveHit = true;
            OnBlockImpact(robot.Position);
            moveHit = false;
            hitShareOverride = -1f;
        }

        private void BlockedFx(Vector3 at)
        {
            fx.Burst(at + Vector3.up * 0.8f, new Color(0.7f, 0.9f, 1f), new Color(1.6f, 2.2f, 3f), 16, 4f);
            AudioManager.PlaySfx(Sfx.Blocked, 1f, 1.2f);
        }

        // ---------- Using the moves ----------

        /// <summary>The player pressed the special: its own move (false when it cannot be used now: out of reach).</summary>
        private bool TrySpecial()
        {
            if (!CanUse(true, mySpecial)) { skillBar?.Shout(Loc.T("move.far"), new Color(1f, 1f, 1f, 0.8f)); return false; }
            cameraRig.StartCoroutine(DoSpecial(true, mySpecial));
            return true;
        }

        /// <summary>Whether a special reaches now (the hook 4 tiles, the shock 3).</summary>
        private bool CanUse(bool mine, SpecialKind s)
        {
            if (!hunt.ChallengeFoeTile.HasValue) return false;
            int d = HuntSystem.Chebyshev(robot.Position, hunt.ChallengeFoeTile.Value);
            return s == SpecialKind.Hook ? d <= 4 : s == SpecialKind.Shock ? d <= 3 : true;
        }

        private IEnumerator DoSpecial(bool mine, SpecialKind s)
        {
            if (s == SpecialKind.Fly) { yield return SpecialMove(mine); yield break; } // (the old special: the leap and its blow)
            moveBusy = true;
            if (mine) NoteDuel(DuelGoal.Special);
            if (mine && LiveActive) LiveNet.Move(1, (int)s);
            skillBar?.Shout(Loc.T("move.s." + s), mine ? new Color(0.5f, 0.85f, 1f) : new Color(1f, 0.5f, 0.45f));
            var me = mine ? robot.Position : hunt.ChallengeFoeTile ?? robot.Position;
            var other = mine ? hunt.ChallengeFoeTile ?? robot.Position : robot.Position;
            var meAt = GridView.ToWorld(me) + Vector3.up * GridView.SurfaceY;
            var otherAt = GridView.ToWorld(other) + Vector3.up * GridView.SurfaceY;
            switch (s)
            {
                case SpecialKind.Backflip:
                {
                    // two tiles back, untouchable for a moment
                    var to = TileAway(me, other, 2);
                    if (mine) myInvulnUntil = Time.time + 0.4f; else rivalInvulnUntil = hunt.FoeInvulnUntil = Time.time + 0.4f;
                    if (to.HasValue) yield return LeapToTile(mine, to.Value, 1.2f);
                    break;
                }
                case SpecialKind.Hook:
                {
                    // the other pulled in beside the mover, and a blow
                    Tracer.Create(meAt + Vector3.up * 0.8f, otherAt + Vector3.up * 0.8f, new Color(0.9f, 0.8f, 0.5f));
                    AudioManager.PlaySfx(Sfx.Hop, 1f, 0.6f);
                    var land = LandingBeside(me, other);
                    if (land.HasValue && !LiveActive)
                    {
                        if (mine) { hunt.StunFoe(0.5f); yield return hunt.ShoveFoe(land.Value, 0.25f); }
                        else { robot.Spawn(grid, land.Value); }
                    }
                    FaceEachOther();
                    if (!LiveActive) { if (mine) HitFoe(1f); else HurtMe(1f); }
                    break;
                }
                case SpecialKind.Shock:
                    Tracer.Create(meAt + Vector3.up * 0.9f, otherAt + Vector3.up * 0.9f, new Color(0.6f, 0.9f, 1f));
                    fx.Burst(otherAt + Vector3.up * 0.7f, new Color(0.6f, 0.9f, 1f), new Color(1.5f, 2.5f, 3f), 30, 5f);
                    AudioManager.PlaySfx(Sfx.Shield, 1f, 0.5f);
                    if (!LiveActive) { if (mine) hunt.StunFoe(0.6f); else strikeCooldown = Mathf.Max(strikeCooldown, 0.6f); }
                    else if (!mine) strikeCooldown = Mathf.Max(strikeCooldown, 0.6f);
                    break;
                case SpecialKind.IceWall:
                {
                    // a wall of ice for two seconds on the tile between them
                    var between = new GridPos(me.x + System.Math.Sign(other.x - me.x), me.y + System.Math.Sign(other.y - me.y));
                    if (grid.InBounds(between) && grid.IsStandable(between) && !grid.IsOccupied(between)) cameraRig.StartCoroutine(IceWall(between, 2f));
                    break;
                }
                case SpecialKind.Patch:
                    if (mine) { if (!LiveActive) { health = Mathf.Min(1f, health + 0.15f); RefreshHealthBar(); } fx.Burst(meAt + Vector3.up * 0.6f, new Color(0.5f, 1f, 0.6f), new Color(1f, 2.4f, 1.2f), 24, 4f); }
                    else if (!LiveActive) hunt.HealFoe(0.15f);
                    AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.5f);
                    break;
                case SpecialKind.SkyStrike:
                {
                    // where it lands glows first; then the jump and a double blow
                    var land = LandingBeside(other, me);
                    Shockwave.Create(otherAt, 1.6f, new Color(1f, 0.6f, 0.2f));
                    yield return new WaitForSeconds(0.3f);
                    if (land.HasValue) yield return LeapToTile(mine, land.Value, 2.2f);
                    FaceEachOther();
                    cameraRig.Shake(0.6f);
                    if (!LiveActive && hunt.ChallengeFoeTile.HasValue && HuntSystem.Chebyshev(robot.Position, hunt.ChallengeFoeTile.Value) <= HuntSystem.DuelGap + 1) { if (mine) HitFoe(2f); else HurtMe(2f); }
                    break;
                }
                case SpecialKind.Teleport:
                {
                    // two tiles to the side at once; the next blow 30% harder
                    var to = TileSide(me, other, 2);
                    fx.Burst(meAt + Vector3.up * 0.6f, new Color(0.75f, 0.5f, 1f), new Color(2f, 1.2f, 3f), 24, 4f);
                    if (to.HasValue && !LiveActive) { if (mine) robot.Spawn(grid, to.Value); else hunt.PlaceFoe(to.Value); }
                    if (mine) myNextBlow = 1.3f; else rivalNextBlow = 1.3f;
                    FaceEachOther();
                    AudioManager.PlaySfx(Sfx.Hop, 1f, 1.6f);
                    break;
                }
            }
            yield return new WaitForSeconds(0.2f);
            moveBusy = false;
        }

        /// <summary>The player pressed the ultimate: its own.</summary>
        private void UseUlti(bool mine, UltiKind u, float scale = 1f)
        {
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
                if (copied == UltiKind.Cinematic) { yield return Ultimate(mine, leap: true); yield break; }
                yield return DoUlti(mine, copied, 0.9f);
                yield break;
            }
            moveBusy = true;
            if (mine) NoteDuel(DuelGoal.Ultimate);
            if (mine && LiveActive && scale >= 0.99f) LiveNet.Move(4, (int)u);
            skillBar?.Shout(Loc.T("move.u." + u), mine ? new Color(0.8f, 0.5f, 1f) : new Color(1f, 0.4f, 0.4f));
            AudioManager.PlaySfx(Sfx.Win, 1f, 0.6f);
            if (colosseum != null) colosseum.Cheer(mine ? 0 : 1, 1f);
            Transform Other() => mine ? hunt.ChallengeFoeModel : robot.Visual;
            Vector3 OtherAt() => mine ? hunt.ChallengeFoeAt ?? robot.transform.position : robot.transform.position;
            Vector3 MeAt() => mine ? robot.transform.position : hunt.ChallengeFoeAt ?? robot.transform.position;
            void Damage(float times) { if (LiveActive) return; if (mine) HitFoe(times * scale); else HurtMe(times * scale); }
            int Gap() => hunt.ChallengeFoeTile.HasValue ? HuntSystem.Chebyshev(robot.Position, hunt.ChallengeFoeTile.Value) : 99;
            switch (u)
            {
                case UltiKind.Quake:
                    // the whole floor: a big blow and the other slowed for a second
                    for (int i = 0; i < 3; i++) Shockwave.Create(MeAt(), 3f + i * 2.5f, new Color(0.8f, 0.65f, 0.4f));
                    cameraRig.Shake(1.2f);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 0.45f);
                    Damage(4f);
                    if (mine) { hunt.StunFoe(0.4f); cameraRig.StartCoroutine(SlowFoe(1f)); } else strikeCooldown = Mathf.Max(strikeCooldown, 0.8f);
                    yield return BigMoment(!mine);
                    break;
                case UltiKind.Whirlwind:
                    // three seconds of spinning blows round the mover: eight, each 0.9 of a blow, for whoever stays close
                    var spinner = mine ? robot.Visual : hunt.ChallengeFoeModel;
                    for (int i = 0; i < 8 && challengeRun && chPhase == ChPhase.Duel; i++)
                    {
                        if (spinner != null) spinner.Rotate(0f, 135f, 0f, Space.World);
                        SlashFx.Create(MeAt() + Vector3.left * 1.2f, MeAt() + Vector3.right * 1.2f, new Color(0.7f, 0.95f, 1f), 1.6f);
                        AudioManager.PlaySfx(Sfx.Hop, 0.8f, 1.8f);
                        if (Gap() <= HuntSystem.DuelGap + 1) Damage(0.9f);
                        yield return new WaitForSeconds(0.375f);
                    }
                    FaceEachOther();
                    break;
                case UltiKind.IronBody:
                    // five seconds untouchable, and no combo breaker gets through
                    if (mine) myIronUntil = Time.time + 5f; else rivalIronUntil = hunt.FoeInvulnUntil = Time.time + 5f;
                    cameraRig.StartCoroutine(Glow(mine, new Color(0.85f, 0.85f, 0.9f), 5f));
                    AudioManager.PlaySfx(Sfx.Shield, 1f, 0.5f);
                    break;
                case UltiKind.Mirror:
                    // three seconds: what comes in goes back half again as hard
                    if (mine) myMirrorUntil = Time.time + 3f; else hunt.FoeMirrorUntil = Time.time + 3f;
                    cameraRig.StartCoroutine(Glow(mine, new Color(0.7f, 0.95f, 1f), 3f));
                    AudioManager.PlaySfx(Sfx.Shield, 1f, 1.4f);
                    break;
                case UltiKind.ChainLightning:
                    // three quick bolts within six tiles, two blows each
                    for (int i = 0; i < 3 && challengeRun && chPhase == ChPhase.Duel; i++)
                    {
                        Tracer.Create(MeAt() + Vector3.up * 1f, OtherAt() + Vector3.up * 0.8f, new Color(0.6f, 0.9f, 1f));
                        fx.Burst(OtherAt() + Vector3.up * 0.8f, new Color(0.6f, 0.9f, 1f), new Color(1.6f, 2.4f, 3f), 22, 5f);
                        AudioManager.PlaySfx(Sfx.Shield, 1f, 0.55f + 0.1f * i);
                        if (Gap() <= 6) Damage(2f);
                        yield return new WaitForSeconds(0.22f);
                    }
                    break;
                case UltiKind.Meteor:
                {
                    // a 3×3 patch marked round the other; a second later the meteor: seven blows for whoever is still in it
                    var mark = mine ? hunt.ChallengeFoeTile ?? robot.Position : robot.Position;
                    var markAt = GridView.ToWorld(mark) + Vector3.up * GridView.SurfaceY;
                    for (int i = 0; i < 4; i++) { Shockwave.Create(markAt, 2.6f, new Color(1f, 0.35f, 0.15f)); yield return new WaitForSeconds(0.25f); }
                    Tracer.Create(markAt + Vector3.up * 14f + Vector3.left * 3f, markAt, new Color(1f, 0.6f, 0.2f));
                    fx.Burst(markAt + Vector3.up * 0.4f, new Color(1f, 0.5f, 0.15f), new Color(3f, 1.4f, 0.3f), 70, 10f);
                    Shockwave.Create(markAt, 4f, new Color(1f, 0.45f, 0.2f));
                    cameraRig.Shake(1.3f);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 0.4f);
                    var still = mine ? hunt.ChallengeFoeTile : robot.Position;
                    if (still.HasValue && HuntSystem.Chebyshev(still.Value, mark) <= 1) { Damage(7f); yield return BigMoment(!mine); }
                    break;
                }
                case UltiKind.CoreBurst:
                    // the core's blast: five blows, and a tenth of the mover's health back
                    fx.Burst(MeAt() + Vector3.up * 0.8f, new Color(0.4f, 0.95f, 1f), new Color(1.2f, 3f, 3.4f), 80, 10f);
                    Shockwave.Create(MeAt(), 4.5f, new Color(0.4f, 0.95f, 1f));
                    cameraRig.Shake(1f);
                    Damage(5f);
                    if (!LiveActive) { if (mine) { health = Mathf.Min(1f, health + 0.1f); RefreshHealthBar(); } else hunt.HealFoe(0.1f); }
                    yield return BigMoment(!mine);
                    break;
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
        private IEnumerator LeapToTile(bool mine, GridPos land, float height)
        {
            var model = mine ? robot.Visual : hunt.ChallengeFoeModel;
            if (model == null) yield break;
            var dbl = StuntDouble.Make(model);
            try
            {
                AudioManager.PlaySfx(Sfx.Hop, 1f, 1.1f);
                yield return dbl.Leap(GridView.ToWorld(land) + Vector3.up * GridView.SurfaceY, height, 0.45f, -1);
                if (mine) robot.Spawn(grid, land); else if (!hunt.ChallengeFoeDown) hunt.PlaceFoe(land);
                FaceEachOther();
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
            wall.transform.position = at + Vector3.up * 0.6f;
            wall.transform.localScale = new Vector3(0.95f, 1.2f, 0.95f);
            wall.GetComponent<Renderer>().sharedMaterial = MaterialFactory.CreateTransparent(new Color(0.6f, 0.9f, 1f, 0.7f), new Color(0.3f, 0.7f, 1f));
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
