using System.Collections;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The duel's fighting moves, Mortal Kombat style. A power bar in three parts fills with every blow landed (and a
    /// little with every blow taken). Blows landed one after another make a combo: three open a power hit, five a
    /// knockdown, for a moment. One part of the bar is the special: from anywhere on the floor foi leaps to the rival
    /// with a stunt of its build (foi a front flip, Volt a double spin, Kaya a big jump, Zip a side roll) and strikes
    /// with its own power (a fire blast, lightning from the sky, a ground slam, a whirl of blades). One part breaks the
    /// rival's combo when caught in it; the whole bar is the ultimate (a leap, slow motion, the camera close, a flurry
    /// and a blast). The big blows are big moments: time slows, the robot struck bursts into its pieces and comes back
    /// together, then the fight goes on. After a knock-down: the finish (sparks and flying bolts). The AI rival uses them
    /// all too; in a live match the server checks every move and keeps both bars.
    /// </summary>
    public partial class GameManager
    {
        private DuelSkillBar skillBar;
        private float myMeter, rivalMeter;
        private int myCombo, rivalCombo, myComboMove;
        private float myComboAt, rivalComboAt, comboMoveUntil, finishUntil, rivalThinkAt;
        private Vector3 finishAt, lastFoeAt;
        private bool specialStrike, ultimateOn, finishing, moveBusy;
        private int rivalHero;

        private const float MeterGain = 0.07f, MeterHurt = 0.04f, ComboGap = 1.4f, ComboMoveTime = 2.6f;
        /// <summary>Blows in a row for the power hit and the knockdown — as the server's Rules.</summary>
        private const int HeavyCombo = 5, UpperCombo = 8;

        /// <summary>A blow's share of the bar by its weapon's pace (a quick blade's many blows fill it no faster than a
        /// slow hammer's few) — as the server's Rules.MeterPace.</summary>
        private static float MeterPace(float cooldown) => Mathf.Clamp(cooldown / 0.25f, 0.45f, 1.3f);

        private float MyPace => MeterPace(Armory.Cooldown(weapon ?? Armory.Equipped));
        private float RivalPace => MeterPace(Armory.Cooldown(Armory.Get(rival.weapon), Mathf.Max(1, rival.weaponLevel)));
        /// <summary>What each move takes off (times one of the mover's own blows) — as the server's Rules.</summary>
        private const float SpecialTimes = 1.8f, HeavyHit = 1.8f, UpperHit = 4f, UltimateHit = 6f;
        /// <summary>A blow struck in the air takes this much more (half a blow again; the AI game only: a live one is the server's).</summary>
        private const float JumpExtra = 0.5f;
        private float myJumpUntil, myJumpReadyAt, rivalJumpAt;

        private static readonly Color[] SpecialColour = { new Color(1f, 0.5f, 0.12f), new Color(0.45f, 0.85f, 1f), new Color(0.6f, 0.45f, 0.3f), new Color(0.75f, 0.4f, 1f) };

        // ---------- Start and end ----------

        private bool duelSkillsWired;

        private void BeginDuelSkills()
        {
            if (!duelSkillsWired)
            {
                duelSkillsWired = true;
                hunt.ChallengeFoeStruck += OnFoeStruck;
                hunt.FoiStruckByFoe += OnFoiStruck;
                hunt.FoeParried += OnFoeParried;
            }
            myMeter = rivalMeter = 0f;
            ResetCombos();
            finishUntil = 0f;
            moveBusy = false;
            rivalHero = 0; // one foi for everyone
            if (skillBar != null) Destroy(skillBar.gameObject);
            skillBar = DuelSkillBar.Create(chUi.BattleHud, Loc.T("move.s." + MoveSet.Special)); // (the set's special on its button)
            skillBar.Pressed += OnSkill;
            skillBar.Emoted += e => { ShowEmote(true, e); if (LiveActive) LiveNet.Emote(e); };
            skillBar.SetMeters(0f, 0f);
            DropRings();
            myRing = FootRing.Make(new Color(0.25f, 0.6f, 1f), 3f, true); // (wider than foi with its cape and shield; a soft light over it)
            foeRing = FootRing.Make(new Color(1f, 0.25f, 0.2f), 3f, true);
        }

        /// <summary>The glowing rings under the two foi: which one is the player's at a glance.</summary>
        private FootRing myRing, foeRing;

        private void DropRings()
        {
            if (myRing != null) Destroy(myRing.gameObject);
            if (foeRing != null) Destroy(foeRing.gameObject);
            myRing = foeRing = null;
        }

        private void ResetCombos()
        {
            myCombo = rivalCombo = myComboMove = 0;
            comboMoveUntil = 0f;
        }

        private void EndDuelSkills()
        {
            if (pendingHp != null && LiveActive) ApplyLiveHp(pendingHp); // the last word on health still shows
            pendingHp = null;
            if (skillBar != null) Destroy(skillBar.gameObject);
            skillBar = null;
            DropRings();
            moveBusy = false;
            if (ultimateOn || Time.timeScale < 0.99f) { ultimateOn = false; if (State == GameState.Playing) Time.timeScale = 1f; }
            slowCamUntil = slowCamWeight = 0f;
        }

        // ---------- Blows landing ----------

        /// <summary>foi's blow landed on the rival: the bar fills, the combo grows (three: a power hit; five: a knockdown).</summary>
        private void OnFoeStruck()
        {
            if (specialStrike || !challengeRun || chPhase != ChPhase.Duel) return;
            myMeter = Mathf.Min(3f, myMeter + MeterGain * MyPace * MyCharge);
            if (!LiveActive) rivalMeter = Mathf.Min(3f, rivalMeter + MeterHurt * MyPace * RivalCharge);
            myCombo = Time.time - myComboAt < MyComboGap ? myCombo + 1 : 1;
            myComboAt = Time.time;
            rivalCombo = 0;
            if (skillBar != null) skillBar.ShowCombo(myCombo);
            if (myCombo == HeavyCombo || myCombo == UpperCombo) { myComboMove = myCombo; comboMoveUntil = Time.time + ComboMoveTime; }
            if (myCombo == HeavyCombo) NoteDuel(DuelGoal.Combo5); // (a five-hit combo)
            if (Time.time < myJumpUntil) AirStrike(true);
            else Reel(false, myCombo);
            if (colosseum != null && myCombo >= 3) colosseum.Cheer(0, 0.4f + myCombo * 0.08f);
            duelStirAt = Time.time;
        }

        /// <summary>The AI rival parried foi's blow: foi's combo is broken, foi reels a moment before its next swing.</summary>
        private void OnFoeParried()
        {
            if (!challengeRun || chPhase != ChPhase.Duel) return;
            myCombo = 0;
            myComboMove = 0;
            strikeCooldown = Mathf.Max(strikeCooldown, 0.55f);
            skillBar?.ShowCombo(0);
            skillBar?.Shout(Loc.T("duel.parried"), new Color(0.6f, 0.85f, 1f));
            var skin = robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null;
            if (skin != null && hunt.ChallengeFoeAt is Vector3 foeAt) skin.Stagger(foeAt, 0.3f);
            duelStirAt = Time.time;
        }

        /// <summary>The rival's blow landed on foi: its bar and combo grow, foi's combo is broken.</summary>
        private void OnFoiStruck()
        {
            if (!challengeRun || chPhase != ChPhase.Duel) return;
            myMeter = Mathf.Min(3f, myMeter + MeterHurt * RivalPace * MyCharge);
            if (!LiveActive) rivalMeter = Mathf.Min(3f, rivalMeter + MeterGain * RivalPace * RivalCharge);
            rivalCombo = Time.time - rivalComboAt < RivalComboGap ? rivalCombo + 1 : 1;
            rivalComboAt = Time.time;
            myCombo = 0;
            myComboMove = 0;
            if (colosseum != null && rivalCombo >= 3) colosseum.Cheer(1, 0.4f + rivalCombo * 0.08f);
            Reel(true, rivalCombo);
            duelStirAt = Time.time;
        }

        // ---------- Each frame ----------

        private void UpdateDuelSkills(float dt)
        {
            if (skillBar == null) return;
            if (hunt.ChallengeFoeAt is Vector3 seen) lastFoeAt = seen;
            if (myRing != null) myRing.Follow(robot.IsAlive && robot.Visual != null && robot.Visual.gameObject.activeInHierarchy ? robot.transform.position : (Vector3?)null);
            if (foeRing != null) foeRing.Follow(hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.position : (Vector3?)null);
            if (myCombo > 0 && Time.time - myComboAt > MyComboGap) myCombo = 0;
            if (rivalCombo > 0 && Time.time - rivalComboAt > RivalComboGap) rivalCombo = 0;
            if (myComboMove > 0 && Time.time > comboMoveUntil) myComboMove = 0;
            DuelSkillBar.Move? ctx = null;
            if (Time.time < finishUntil) ctx = DuelSkillBar.Move.Finish;
            else if (chPhase == ChPhase.Duel && !moveBusy)
            {
                if (rivalCombo >= 2 && myMeter >= 1f) ctx = DuelSkillBar.Move.Breaker;
                else if (myComboMove == UpperCombo) ctx = DuelSkillBar.Move.Upper;
                else if (myComboMove == HeavyCombo) ctx = DuelSkillBar.Move.Heavy;
            }
            skillBar.SetContext(ctx);
            skillBar.SetMeters(myMeter, rivalMeter);
            if (!LiveActive && chPhase == ChPhase.Duel && !moveBusy && !DuelCinematic) RivalMoves(); // (never inside a big moment)
            // Standing, foi keeps turned to the rival (after a leap, a shove, a step back: never its back to it)
            if (chPhase == ChPhase.Duel && robot.IsIdle && hunt.ChallengeFoeAt is Vector3 rivalAt) robot.TurnTo(rivalAt);
            Unstick();
        }

        /// <summary>In the duel: a step that would bring foi right up to the rival (a tile is always kept between them).</summary>
        private bool TooCloseToRival(GridPos p)
        {
            if (!challengeRun || chPhase != ChPhase.Duel || !hunt.ChallengeFoeTile.HasValue) return false;
            var foe = hunt.ChallengeFoeTile.Value;
            int d = HuntSystem.Chebyshev(p, foe);
            return d < HuntSystem.DuelGap && d < HuntSystem.Chebyshev(robot.Position, foe);
        }

        // ---------- The player's moves ----------

        private void OnSkill(DuelSkillBar.Move move)
        {
            if (!challengeRun) return;
            if (move == DuelSkillBar.Move.Finish) { DoFinish(); return; }
            if (chPhase != ChPhase.Duel || moveBusy || !hunt.ChallengeFoeTile.HasValue) return;
            var foeTile = hunt.ChallengeFoeTile.Value;
            var foeAt = hunt.ChallengeFoeAt ?? robot.transform.position;
            switch (move)
            {
                case DuelSkillBar.Move.Special:
                    if (myMeter < 1f || !TrySpecial()) return; // (the set's special; out of its reach: nothing spent)
                    myMeter -= 1f;
                    break;
                case DuelSkillBar.Move.Heavy:
                case DuelSkillBar.Move.Upper:
                {
                    bool upper = move == DuelSkillBar.Move.Upper;
                    if (myComboMove != (upper ? UpperCombo : HeavyCombo) || HuntSystem.Chebyshev(robot.Position, foeTile) > StrikeReach + 1) return;
                    myComboMove = 0;
                    myCombo = 0;
                    robot.Strike(foeAt);
                    SlashFx.Create(robot.transform.position, foeAt, upper ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.7f, 0.25f), upper ? 2.4f : 1.8f);
                    Shockwave.Create(foeAt, upper ? 2.6f : 1.8f, upper ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 0.75f, 0.35f));
                    cameraRig.Shake(upper ? 0.7f : 0.45f);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, upper ? 0.7f : 0.85f);
                    skillBar.Shout(Loc.T(upper ? "duel.upper" : "duel.heavy") + "!", new Color(1f, 0.6f, 0.25f));
                    if (LiveActive) LiveNet.Special(upper ? 3 : 2);
                    else
                    {
                        HitFoe((upper ? UpperHit : HeavyHit) * DuelPower.ComboTimes(weapon ?? Armory.Equipped)); // (an axe's a fifth harder)
                        if (upper) PowerSlow(0.3f, 0.35f); // (the knockdown: a breath of slow motion)
                        hunt.StunFoe(upper ? 1.8f : 0.4f); // (short: the rival is back in the fight at once)
                    }
                    if (colosseum != null) colosseum.Cheer(0, 0.9f);
                    if (upper) cameraRig.StartCoroutine(BigMoment(false)); // the knockdown: a big moment
                    break;
                }
                case DuelSkillBar.Move.Breaker:
                    if (myMeter < 1f || rivalCombo < 2) return;
                    if (Time.time < rivalIronUntil) { skillBar.Shout(Loc.T("move.u.IronBody"), new Color(0.85f, 0.85f, 0.9f)); return; } // (its iron body: no breaking it)
                    myMeter -= 1f;
                    rivalCombo = 0;
                    Shockwave.Create(robot.transform.position, 2.4f, new Color(0.4f, 0.8f, 1f));
                    fx.Burst(robot.transform.position + Vector3.up * 0.6f, new Color(0.5f, 0.85f, 1f), new Color(1f, 2f, 3f), 30, 6f);
                    AudioManager.PlaySfx(Sfx.Shield, 1f, 0.8f);
                    skillBar.Shout(Loc.T("duel.broken"), new Color(0.5f, 0.85f, 1f));
                    if (LiveActive) LiveNet.Special(5);
                    else
                    {
                        hunt.StunFoe(1.2f);
                        var away = new GridPos(System.Math.Sign(foeTile.x - robot.Position.x), System.Math.Sign(foeTile.y - robot.Position.y));
                        if (away.x == 0 && away.y == 0) away = new GridPos(0, 1);
                        for (int step = 3; step >= 1; step--)
                        {
                            var to = new GridPos(foeTile.x + away.x * step, foeTile.y + away.y * step);
                            if (grid.InBounds(to) && grid.IsStandable(to) && !grid.IsOccupied(to)) { cameraRig.StartCoroutine(hunt.ShoveFoe(to, 0.3f)); break; }
                        }
                    }
                    break;
                case DuelSkillBar.Move.Jump:
                    Jump();
                    break;
                case DuelSkillBar.Move.Ultimate:
                    if (myMeter < UltiCost) return; // (the quick-ulti week: two parts)
                    myMeter = 0f;
                    UseUlti(true, myUlti); // (the set's ultimate)
                    break;
            }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>A move's damage on the AI rival: through its shield and health like a blow, but no combo or bar for it.</summary>
        private void HitFoe(float times)
        {
            if (!hunt.ChallengeFoeTile.HasValue) return;
            var w = weapon ?? Armory.Equipped;
            int damage = Mathf.RoundToInt(MyBlow(w) * times * MyBlowFactor());
            specialStrike = true;
            hunt.Strike(hunt.ChallengeFoeTile.Value, Mathf.Max(1, damage));
            specialStrike = false;
            MyVampire(damage);
        }

        /// <summary>A move's damage on foi from the AI rival: its own blow times this, through foi's shield like a blow.</summary>
        private void HurtMe(float times)
        {
            float share = hunt.ChallengeFoeShare * times;
            if (share <= 0f) return;
            hitShareOverride = share;
            moveHit = true;
            OnBlockImpact(robot.Position);
            moveHit = false;
            hitShareOverride = -1f;
        }

        // ---------- The leap ----------

        /// <summary>
        /// The mover leaps to the tile beside the other (on its own side), with its build's stunt on the way: a stand-in
        /// does the flying while the robot itself waits hidden, and the robot lands there. Nothing if already beside it.
        /// </summary>
        private IEnumerator LeapTo(bool mine, int stunt, bool plain = false)
        {
            var from = mine ? robot.Position : hunt.ChallengeFoeTile ?? robot.Position;
            var victim = mine ? hunt.ChallengeFoeTile ?? robot.Position : robot.Position;
            if (HuntSystem.Chebyshev(from, victim) <= HuntSystem.DuelGap) yield break;
            var model = mine ? robot.Visual : hunt.ChallengeFoeModel;
            if (model == null) yield break;
            var land = LandingBeside(victim, from);
            if (!land.HasValue) yield break;
            var dbl = StuntDouble.Make(model);
            try
            {
                AudioManager.PlaySfx(Sfx.Hop, 1f, stunt == 2 ? 0.7f : 1.2f);
                fx.Burst(model.position + Vector3.up * 0.2f, SpecialColour[stunt], SpecialColour[stunt] * 1.6f, 14, 4f);
                yield return dbl.Leap(GridView.ToWorld(land.Value) + Vector3.up * GridView.SurfaceY, 1.6f, 0.5f, plain ? -1 : stunt); // (plain: a jump, no somersault)
                if (mine) robot.Spawn(grid, land.Value); else if (!hunt.ChallengeFoeDown) hunt.PlaceFoe(land.Value); // (a live knock-down may have taken it meanwhile)
                FaceEachOther();
            }
            finally { if (dbl != null) dbl.Done(); } // the stand-in never stays behind, whatever happened on the way
            if (stunt == 2) { Shockwave.Create(GridView.ToWorld(land.Value) + Vector3.up * GridView.SurfaceY, 1.6f, SpecialColour[2]); cameraRig.Shake(0.4f); }
        }

        /// <summary>The two foi turned to each other at once.</summary>
        private void FaceEachOther()
        {
            if (!(hunt.ChallengeFoeAt is Vector3 foeAt) || robot.Visual == null) return;
            robot.Face(foeAt);
            hunt.TurnFoe(robot.transform.position - foeAt);
        }

        /// <summary>A free tile the duel's gap away from <paramref name="victim"/>, on the side the mover comes from.</summary>
        private GridPos? LandingBeside(GridPos victim, GridPos from)
        {
            int dx = System.Math.Sign(from.x - victim.x), dy = System.Math.Sign(from.y - victim.y);
            var tries = new[] { new GridPos(dx, dy), new GridPos(dx, 0), new GridPos(0, dy), new GridPos(-dy, dx), new GridPos(dy, -dx), new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1) };
            for (int k = HuntSystem.DuelGap; k <= HuntSystem.DuelGap + 1; k++)
                foreach (var d in tries)
                {
                    if (d.x == 0 && d.y == 0) continue;
                    var p = new GridPos(victim.x + d.x * k, victim.y + d.y * k);
                    if (grid.InBounds(p) && grid.IsStandable(p) && !grid.IsOccupied(p)) return p;
                }
            return null;
        }

        // ---------- The special ----------

        private IEnumerator SpecialMove(bool mine)
        {
            moveBusy = true;
            if (mine) NoteDuel(DuelGoal.Special);
            int hero = mine ? HeroModels.Skill(SaveData.Hero) : rivalHero;
            if (mine) hunt.StunFoe(1.6f);
            strikeCooldown = Mathf.Max(strikeCooldown, mine ? 0.9f : 1.4f);
            yield return LeapTo(mine, hero);
            if (!challengeRun || chPhase != ChPhase.Duel || !hunt.ChallengeFoeAt.HasValue) { moveBusy = false; yield break; }
            var foeAt = hunt.ChallengeFoeAt.Value;
            if (mine) robot.Strike(foeAt);
            PowerFx(hero, mine ? robot.transform.position : foeAt, mine ? foeAt : robot.transform.position);
            if (mine)
            {
                if (LiveActive) LiveNet.Special(1);
                else HitFoe(SpecialTimes);
                if (colosseum != null) colosseum.Cheer(0, 0.9f);
            }
            else HurtMe(SpecialTimes);
            yield return BigMoment(!mine);
            moveBusy = false;
        }

        /// <summary>A build's own power at the end of its special: a fire blast, lightning from the sky, a slam, a whirl of blades.</summary>
        private void PowerFx(int hero, Vector3 from, Vector3 at)
        {
            var c = SpecialColour[hero];
            var up = Vector3.up;
            switch (hero)
            {
                case 1:
                    for (int i = 0; i < 3; i++) Tracer.Create(at + up * 9f + new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.6f, 0.6f)), at + up * 0.3f, new Color(0.6f, 0.9f, 1f));
                    fx.Burst(at + up * 0.5f, c, new Color(1.2f, 2.4f, 3f), 46, 8f);
                    Shockwave.Create(at, 2.2f, c);
                    AudioManager.PlaySfx(Sfx.Shield, 1f, 0.6f);
                    break;
                case 2:
                    fx.Burst(at + up * 0.3f, new Color(0.55f, 0.5f, 0.45f), new Color(0.8f, 0.7f, 0.6f), 50, 9f);
                    Shockwave.Create(at, 3.2f, c);
                    Shockwave.Create(at, 1.8f, new Color(0.9f, 0.8f, 0.6f));
                    cameraRig.Shake(0.9f);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 0.55f);
                    break;
                case 3:
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * 0.5f;
                        var side = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.1f;
                        SlashFx.Create(at - side, at + side, c, 1.8f);
                    }
                    fx.Burst(at + up * 0.6f, c, c * 2.4f, 40, 7f);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 1.3f);
                    break;
                default:
                    for (int i = 0; i < 4; i++) fx.Burst(at + up * (0.3f + i * 0.45f), c, new Color(3f, 1.4f, 0.3f), 30, 6f + i);
                    Shockwave.Create(at, 2.6f, c);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 0.75f);
                    break;
            }
            cameraRig.Shake(0.6f);
        }

        // ---------- The big moment ----------

        /// <summary>
        /// Time slows; the one struck (foi's own, or the rival) flies back off its feet and lands flat on its back, then
        /// gets up; the fight goes on. Nothing if it fell for good (the knock-down shows itself).
        /// </summary>
        private IEnumerator BigMoment(bool meStruck)
        {
            var model = meStruck ? robot.Visual : hunt.ChallengeFoeModel;
            if (model == null || !challengeRun || (meStruck ? !robot.IsAlive : hunt.ChallengeFoeDown)) yield break;
            hunt.StunFoe(1.8f); // the rival stands still through the moment, whoever was struck (and gets up after)
            float was = Time.timeScale;
            Time.timeScale = 0.35f;
            SlowCam(model.position, 1.4f); // the big moment's time (slow motion, no blows); the camera stays on the board
            cameraRig.Shake(1.1f);
            var at = model.position;
            var from = meStruck ? (hunt.ChallengeFoeAt ?? at) : robot.transform.position;
            fx.Burst(at + Vector3.up * 0.6f, new Color(1f, 0.85f, 0.4f), new Color(3f, 2.4f, 0.8f), 26, 5f);
            AudioManager.PlaySfx(Sfx.Impact, 1f, 0.6f);
            Fling(meStruck, from, 1.5f, 2);
            yield return new WaitForSecondsRealtime(0.75f);
            cameraRig.Shake(0.6f); // (landing)
            cameraRig.StartCoroutine(FaceWhenUp(meStruck));
            fx.Burst(at + Vector3.up * 0.2f, new Color(0.8f, 0.7f, 0.55f), new Color(1.4f, 1.2f, 1f), 16, 3f);
            if (State == GameState.Playing) Time.timeScale = ultimateOn ? 0.35f : (was < 0.99f && was > 0.3f ? was : 1f);
        }

        /// <summary>Back on its feet: foi turns straight to its rival (the rival turns by itself as it comes at foi).</summary>
        private IEnumerator FaceWhenUp(bool meStruck)
        {
            yield return new WaitForSeconds(0.75f);
            if (meStruck && challengeRun && hunt.ChallengeFoeAt is Vector3 foe) robot.Face(foe);
        }

        /// <summary>When something last happened between the two (a blow landed, one moved), and where they stood.</summary>
        private float duelStirAt;
        private GridPos stuckMe, stuckFoe;

        /// <summary>
        /// The two foi closer than the duel's gap (a knock, a landing, a shove brought them there) and both standing: after a
        /// moment the rival's reeling is cut short and it is pushed back out to the gap (the AI game).
        /// </summary>
        private void Unstick()
        {
            if (LiveActive || chPhase != ChPhase.Duel || DuelCinematic || moveBusy || !hunt.ChallengeFoeTile.HasValue) { duelStirAt = Time.time; return; }
            var me = robot.Position; var foe = hunt.ChallengeFoeTile.Value;
            int near = HuntSystem.Chebyshev(me, foe);
            if (me != stuckMe || foe != stuckFoe || near >= HuntSystem.DuelGap) { stuckMe = me; stuckFoe = foe; duelStirAt = Time.time; return; }
            if (Time.time - duelStirAt < 0.5f) return;
            duelStirAt = Time.time;
            hunt.StunFoe(0f); hunt.EndFoeStun();
            Push(false, robot.transform.position, HuntSystem.DuelGap - near);
        }

        /// <summary>
        /// The ultimate's blows drive the one struck back a step each (reeling), and the one striking steps after it, so the
        /// ultimate goes on across the floor (the AI game: a live one's places are the server's).
        /// </summary>
        private void DriveBack(bool mine)
        {
            var model = mine ? hunt.ChallengeFoeModel : robot.Visual;
            var skin = model != null ? model.GetComponentInChildren<SkinnedHero>() : null;
            var strikerAt = mine ? robot.transform.position : hunt.ChallengeFoeAt ?? robot.transform.position;
            if (skin != null) skin.Stagger(strikerAt, 0.7f);
            if (LiveActive || !hunt.ChallengeFoeTile.HasValue) return;
            var victim = mine ? hunt.ChallengeFoeTile.Value : robot.Position;
            var striker = mine ? robot.Position : hunt.ChallengeFoeTile.Value;
            var d = new GridPos(victim.x - striker.x, victim.y - striker.y);
            var step = Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? new GridPos(System.Math.Sign(d.x), 0) : new GridPos(0, System.Math.Sign(d.y));
            if (step.x == 0 && step.y == 0) return;
            var to = new GridPos(victim.x + step.x, victim.y + step.y);
            if (!grid.InBounds(to) || !grid.IsStandable(to) || grid.IsOccupied(to) || to == robot.Position) return; // (the edge: it stands its ground)
            var dir = step.x > 0 ? Direction.PlusX : step.x < 0 ? Direction.MinusX : step.y > 0 ? Direction.PlusY : Direction.MinusY;
            if (mine)
            {
                cameraRig.StartCoroutine(hunt.ShoveFoe(to, 0.18f));
                cameraRig.StartCoroutine(FollowUp(() => robot.Shove(dir, 1, 0.15f, 1.6f)));
            }
            else
            {
                robot.Shove(dir, 1, 0.15f, 1.6f);
                var after = victim;
                cameraRig.StartCoroutine(FollowUp(() => cameraRig.StartCoroutine(hunt.ShoveFoe(after, 0.2f))));
            }
        }

        private static IEnumerator FollowUp(System.Action step)
        {
            yield return new WaitForSecondsRealtime(0.08f);
            step();
        }

        // ---------- Reeling and flying ----------

        /// <summary>
        /// A blow landed on foi (<paramref name="meStruck"/>) or the rival: it reels, harder the more blows came in a row;
        /// from the fourth in a row it is knocked a step back (the AI game: a live one's places are the server's).
        /// </summary>
        private void Reel(bool meStruck, int inARow)
        {
            var model = meStruck ? robot.Visual : hunt.ChallengeFoeModel;
            var skin = model != null ? model.GetComponentInChildren<SkinnedHero>() : null;
            if (skin == null) return;
            var from = meStruck ? (hunt.ChallengeFoeAt ?? model.position) : robot.transform.position;
            float strength = Mathf.Clamp01(0.25f + 0.15f * Mathf.Max(0, inARow - 1));
            // where it lands: the head (a blow from overhead, now and then another) or the body
            var striker = meStruck ? hunt.ChallengeFoeModel : robot.Visual;
            var held = striker != null ? striker.GetComponentInChildren<HeldWeapon>() : null;
            bool toHead = (held != null && held.Overhead) || Random.value < 0.25f;
            var at = toHead ? skin.HeadPoint : skin.ChestPoint;
            fx.Burst(at, new Color(1f, 0.92f, 0.6f), new Color(2.4f, 2.1f, 1.2f), 10, 3.2f);
            Shockwave.Create(at - Vector3.up * 0.1f, 0.45f, new Color(1f, 0.95f, 0.75f));
            if (toHead) { skin.JoltHead(from); strength *= 0.6f; } // (the head takes it: the body moves less)
            skin.Stagger(from, strength);
            if (inARow >= 4 && !LiveActive) Push(meStruck, from, 1);
        }

        /// <summary>The one struck flies back off its feet and lands on its back (and is pushed <paramref name="tiles"/> back).</summary>
        private void Fling(bool meStruck, Vector3 from, float height, int tiles)
        {
            var model = meStruck ? robot.Visual : hunt.ChallengeFoeModel;
            var skin = model != null ? model.GetComponentInChildren<SkinnedHero>() : null;
            if (skin != null) skin.Launch(from, height);
            if (!LiveActive) Push(meStruck, from, tiles);
        }

        /// <summary>Foi or the rival pushed up to <paramref name="tiles"/> tiles away from <paramref name="from"/> (as far as the floor lets it).</summary>
        private void Push(bool meStruck, Vector3 from, int tiles)
        {
            var at = meStruck ? robot.Position : hunt.ChallengeFoeTile ?? robot.Position;
            var w = GridView.ToWorld(at);
            var d = new Vector2(w.x - from.x, w.z - from.z);
            if (d.sqrMagnitude < 1e-4f) return;
            var step = Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? new GridPos(System.Math.Sign(d.x), 0) : new GridPos(0, System.Math.Sign(d.y));
            for (int n = tiles; n >= 1; n--)
            {
                var to = new GridPos(at.x + step.x * n, at.y + step.y * n);
                if (!grid.InBounds(to) || !grid.IsStandable(to) || grid.IsOccupied(to)) continue;
                if (meStruck)
                {
                    var dir = step.x > 0 ? Direction.PlusX : step.x < 0 ? Direction.MinusX : step.y > 0 ? Direction.PlusY : Direction.MinusY;
                    robot.Shove(dir, n, 0.25f, 1.3f);
                }
                else
                {
                    hunt.StunFoe(0.2f);
                    cameraRig.StartCoroutine(hunt.ShoveFoe(to, 0.22f + 0.08f * n));
                }
                return;
            }
        }

        // ---------- The ultimate ----------

        /// <summary>A leap to the other (the AI's or foi's own), slow motion, the camera close: four quick blows and a
        /// blast, the last a big moment.</summary>
        private IEnumerator Ultimate(bool mine, bool leap)
        {
            moveBusy = true;
            ultimateOn = true;
            skillBar?.Shout(Loc.T("duel.ultTitle"), mine ? new Color(0.8f, 0.5f, 1f) : new Color(1f, 0.4f, 0.4f));
            AudioManager.PlaySfx(Sfx.Win, 1f, 0.6f);
            if (colosseum != null) colosseum.Cheer(mine ? 0 : 1, 1f);
            if (mine) hunt.StunFoe(6.5f);
            strikeCooldown = Mathf.Max(strikeCooldown, mine ? 0.5f : 2.5f);
            if (mine) NoteDuel(DuelGoal.Ultimate);
            // foi leaps in (a plain jump), then the ultimate's own moves through all of it: its combo, and a spin kick to end
            if (leap) yield return LeapTo(mine, mine ? HeroModels.Skill(SaveData.Hero) : rivalHero, true);
            var caster = mine ? robot.Visual : hunt.ChallengeFoeModel;
            var casterSkin = caster != null ? caster.GetComponentInChildren<SkinnedHero>() : null;
            const float ComboRate = 1.6f;
            float combo = casterSkin != null ? Mathf.Max(SkinnedHero.MoveLength("cast") / ComboRate, 2.2f) : 0f;
            if (casterSkin != null) casterSkin.PlayMove("cast", ComboRate, true, loop: true); // round and round until the ultimate ends
            if (mine && LiveActive) LiveNet.Special(4);
            Time.timeScale = 0.35f;
            for (int i = 0; i < 5; i++)
            {
                if (casterSkin == null) yield return new WaitForSecondsRealtime(i == 4 ? 0.35f : 0.2f);
                else yield return new WaitForSecondsRealtime(combo / 5f); // the blows through the move (it keeps going)
                if (!challengeRun || chPhase != ChPhase.Duel) break;
                var foeAt = hunt.ChallengeFoeAt ?? robot.transform.position;
                var from = mine ? robot.transform.position : foeAt;
                var to = mine ? foeAt : robot.transform.position;
                var colour = mine ? new Color(0.8f, 0.5f, 1f) : new Color(1f, 0.35f, 0.3f);
                if (casterSkin != null)
                {
                    // turned to the other the whole time
                    if (mine) robot.Face(to);
                    else if (caster != null) { var d = to - caster.position; d.y = 0f; if (d.sqrMagnitude > 0.0001f) caster.rotation = Quaternion.LookRotation(d.normalized); }
                }
                else if (mine) robot.Strike(to);
                // its weapon strikes each blow (swinging in the hand the move carries)
                var held = caster != null ? caster.GetComponentInChildren<HeldWeapon>() : null;
                if (held != null) held.Swing();
                SlashFx.Create(from, to, colour, i == 4 ? 3f : 1.6f);
                fx.Burst(to + Vector3.up * 0.6f, colour, colour * 2.5f, i == 4 ? 60 : 20, i == 4 ? 9f : 5f);
                if (i == 4) Shockwave.Create(to, 3.2f, colour);
                cameraRig.Shake(i == 4 ? 0.9f : 0.3f);
                AudioManager.PlaySfx(Sfx.Impact, 1f, i == 4 ? 0.55f : 1.1f);
                if (!LiveActive)
                {
                    float part = i == 4 ? UltimateHit * 0.5f : UltimateHit * 0.125f;
                    if (mine) HitFoe(part); else HurtMe(part);
                }
                if (i < 4) DriveBack(mine); // (struck, it is driven back; the one striking follows it up)
                if (i == 4) yield return BigMoment(!mine);
            }
            if (casterSkin != null) casterSkin.StopMove();
            ultimateOn = false;
            if (State == GameState.Playing) Time.timeScale = 1f;
            moveBusy = false;
        }

        // ---------- The knockdown's fall, the jump ----------

        /// <summary>A foi (its model) knocked flat on its back, away from <paramref name="from"/>, and back up.</summary>
        private static void KnockOver(Transform model, Vector3 from)
        {
            var skin = model != null ? model.GetComponentInChildren<SkinnedHero>() : null;
            if (skin != null) skin.KnockDown(from, 1.2f);
        }

        /// <summary>foi jumps: a blow landed in the air knocks the rival's head off its neck (for a moment) and takes more.</summary>
        private void Jump()
        {
            if (Time.time < myJumpReadyAt || !robot.IsAlive) return;
            myJumpReadyAt = Time.time + 0.9f;
            myJumpUntil = Time.time + 0.65f;
            var skin = robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null;
            if (skin != null) skin.Hop(1.1f, 0.65f);
            AudioManager.PlaySfx(Sfx.Hop, 1f, 1.1f);
        }

        /// <summary>A blow struck in the air: the head of the one struck flies off and comes back, and it takes a little more.</summary>
        private void AirStrike(bool mine)
        {
            if (mine) myJumpUntil = 0f; else rivalJumpAt = Time.time + 9f; // (one per jump)
            var struck = mine ? hunt.ChallengeFoeModel : robot.Visual;
            var at = struck != null ? struck.position : robot.transform.position;
            Fling(!mine, mine ? robot.transform.position : (hunt.ChallengeFoeAt ?? at), 1f, 1); // knocked off its feet, onto its back
            if (mine) hunt.StunFoe(0.7f);
            fx.Burst(at + Vector3.up * 1.4f, new Color(1f, 0.9f, 0.5f), new Color(3f, 2.6f, 1f), 24, 6f);
            cameraRig.Shake(0.5f);
            AudioManager.PlaySfx(Sfx.Impact, 1f, 1.2f);
            skillBar?.Shout(Loc.T("duel.airHit"), mine ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.45f, 0.4f));
            if (!LiveActive) { if (mine) HitFoe(JumpExtra); else HurtMe(JumpExtra); }
        }

        /// <summary>The AI rival jumps in and strikes from the air now and then.</summary>
        private System.Collections.IEnumerator RivalJumpStrike()
        {
            var model = hunt.ChallengeFoeModel;
            var skin = model != null ? model.GetComponentInChildren<SkinnedHero>() : null;
            if (skin == null) yield break;
            skin.Hop(1.1f, 0.65f);
            AudioManager.PlaySfx(Sfx.Hop, 1f, 1.0f);
            yield return new WaitForSeconds(0.32f);
            if (!challengeRun || chPhase != ChPhase.Duel || DuelCinematic || !hunt.ChallengeFoeAt.HasValue) yield break;
            if (Flat(hunt.ChallengeFoeAt.Value - robot.transform.position).magnitude > 3.6f) yield break;
            hunt.TurnFoe(robot.transform.position - hunt.ChallengeFoeAt.Value);
            SlashFx.Create(hunt.ChallengeFoeAt.Value, robot.transform.position, new Color(1f, 0.4f, 0.3f), 1.6f);
            HurtMe(1f);
            AirStrike(false);
        }

        // ---------- The finish ----------

        /// <summary>After foi's knock-down: the finish is offered for a moment.</summary>
        private void OfferFinish(Vector3 at)
        {
            finishAt = at;
            finishUntil = Time.time + 2.2f;
        }

        /// <summary>The finish: the fallen rival goes up in sparks and flying bolts (robots don't bleed: they come apart).</summary>
        private void DoFinish()
        {
            if (Time.time > finishUntil || finishing) return;
            finishUntil = 0f;
            if (LiveActive) LiveNet.Special(6);
            ChallengeQuests.Report(DuelGoal.Finish);
            cameraRig.StartCoroutine(Finisher(finishAt, true));
        }

        private IEnumerator Finisher(Vector3 at, bool mine)
        {
            finishing = true;
            skillBar?.Shout(Loc.T("duel.finished"), mine ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.4f, 0.4f));
            if (colosseum != null) colosseum.Cheer(mine ? 0 : 1, 1f);
            var bolt = new Color(0.62f, 0.64f, 0.7f);
            for (int i = 0; i < 4; i++)
            {
                fx.Burst(at + Vector3.up * (0.3f + i * 0.25f), new Color(1f, 0.85f, 0.4f), new Color(3f, 2.4f, 0.8f), 30, 6f + i);
                fx.Burst(at + Vector3.up * 0.5f, bolt, bolt * 1.4f, 14, 7f);
                Shockwave.Create(at, 1.2f + i * 0.7f, new Color(1f, 0.8f, 0.4f));
                cameraRig.Shake(0.4f + i * 0.15f);
                AudioManager.PlaySfx(i == 3 ? Sfx.Impact : Sfx.Blocked, 1f, 1.3f - i * 0.15f);
                yield return new WaitForSeconds(0.18f);
            }
            finishing = false;
        }

        // ---------- Emotes ----------

        private float emoteAt;

        /// <summary>An emote over a foi's head (the player's in blue, the rival's in red), now and then, not a flood.</summary>
        private void ShowEmote(bool mine, int e)
        {
            if (mine && Time.unscaledTime < emoteAt) return;
            if (mine) emoteAt = Time.unscaledTime + 1.5f;
            var at = mine ? robot.transform.position : hunt.ChallengeFoeAt ?? lastFoeAt;
            FloatAt(at + Vector3.up * 1.3f, Loc.T("duel.em." + Mathf.Clamp(e, 0, 5)), mine ? new Color(0.45f, 0.75f, 1f) : new Color(1f, 0.5f, 0.45f));
            AudioManager.PlaySfx(Sfx.Click, 0.7f, mine ? 1.5f : 1.2f);
        }

        /// <summary>The AI rival's word after a knock-down (now and then): a laugh when it scored, a growl when it fell.</summary>
        private IEnumerator RivalEmote(bool rivalScored)
        {
            if (LiveActive || Random.value > 0.55f) yield break;
            yield return new WaitForSeconds(0.6f);
            if (!challengeRun) yield break;
            int[] pick = rivalScored ? new[] { 1, 0, 3 } : new[] { 4, 2 };
            ShowEmote(false, pick[Random.Range(0, pick.Length)]);
        }

        // ---------- The AI rival's moves ----------

        private void RivalMoves()
        {
            if (Time.time < rivalThinkAt || !hunt.ChallengeFoeAt.HasValue) return;
            var t = botTuning;
            rivalThinkAt = Time.time + (t != null ? Mathf.Max(0.2f, t.React()) : 0.45f); // (never at once: a human's reaction)
            var foeAt = hunt.ChallengeFoeAt.Value;
            float dist = Flat(foeAt - robot.transform.position).magnitude;
            float bold = t != null ? t.aggression : Mathf.Clamp01(rival.boldness);
            // the ultimate when its personality says so: at once (aggressive), when foi opens up (defensive), at a combo's
            // end (combo), once apart (runner), with foi low (the ulti saver)
            bool ultiTime = t == null ? Random.value < 0.3f + 0.3f * bold : t.persona switch
            {
                Persona.Aggressive => true,
                Persona.Defensive => strikeCooldown > 0.05f && dist <= 3.4f,
                Persona.Combo => rivalCombo >= 3,
                Persona.Runner => dist >= 3f,
                Persona.Ulti => health < 0.4f,
                _ => Random.value < 0.45f,
            };
            if (rivalMeter >= UltiCost && ultiTime)
            {
                rivalMeter = 0f;
                UseUlti(false, rivalUlti); // (its set's ultimate)
                return;
            }
            int breakFrom = t != null ? t.breakerFrom : 3;
            float breakLean = t != null ? t.breaker : 0.3f * bold;
            bool keepsBar = t != null && t.persona == Persona.Ulti; // (the ulti saver never spends its bar on a break)
            if (myCombo >= breakFrom && !keepsBar && Time.time >= myIronUntil && (rivalMeter >= 1f ? Random.value < 0.35f + 0.5f * breakLean : Random.value < 0.1f + 0.2f * breakLean))
            {
                rivalMeter = Mathf.Max(0f, rivalMeter - 1f);
                myCombo = 0;
                myComboMove = 0;
                strikeCooldown = Mathf.Max(strikeCooldown, 0.9f); // foi's swing knocked aside
                Shockwave.Create(foeAt, 2.4f, new Color(1f, 0.45f, 0.4f));
                AudioManager.PlaySfx(Sfx.Shield, 1f, 0.75f);
                skillBar?.Shout(Loc.T("duel.broken"), new Color(1f, 0.45f, 0.4f));
                return;
            }
            if (dist <= 3.4f && Time.time > rivalJumpAt && Random.value < 0.06f + 0.05f * bold)
            {
                rivalJumpAt = Time.time + 9f;
                cameraRig.StartCoroutine(RivalJumpStrike());
                return;
            }
            if (rivalCombo >= HeavyCombo && dist <= 3.4f && Random.value < 0.5f)
            {
                rivalCombo = 0;
                FaceEachOther();
                SlashFx.Create(foeAt, robot.transform.position, new Color(1f, 0.35f, 0.3f), 1.8f);
                Shockwave.Create(robot.transform.position, 1.8f, new Color(1f, 0.4f, 0.3f));
                cameraRig.Shake(0.45f);
                HurtMe(HeavyHit);
                return;
            }
            if (rivalMeter >= 1f && !keepsBar && Random.value < (dist >= 3f ? 0.22f : 0.1f) + 0.15f * bold + (t != null && t.persona == Persona.Runner && dist >= 3f ? 0.2f : 0f))
            {
                if (RivalSpecial()) rivalMeter -= 1f; // (its set's special, when it reaches)
            }
        }

        // ---------- A live rival's moves (the server's word) ----------

        /// <summary>The server's bars (when it sends them).</summary>
        private void LiveMeters(float mine, float theirs)
        {
            if (mine >= 0f) myMeter = Mathf.Clamp(mine, 0f, 3f);
            if (theirs >= 0f) rivalMeter = Mathf.Clamp(theirs, 0f, 3f);
        }

        /// <summary>A move of the live rival, as the server passes it on: shown here (its damage comes with the health;
        /// its leaps with its game's positions).</summary>
        private void LiveRivalMove(int kind, int move = 0)
        {
            if (!challengeRun) return;
            var foeAt = hunt.ChallengeFoeAt ?? robot.transform.position;
            // a move of its set: shown as this game shows its own (the server keeps the damage)
            if (kind == 1 && move > 0) { cameraRig.StartCoroutine(DoSpecial(false, (SpecialKind)Mathf.Clamp(move, 0, 7))); return; }
            if (kind == 4 && move > 0) { UseUlti(false, (UltiKind)Mathf.Clamp(move, 0, 8)); return; }
            switch (kind)
            {
                case 1:
                    PowerFx(rivalHero, foeAt, robot.transform.position);
                    cameraRig.StartCoroutine(BigMoment(true));
                    break;
                case 2:
                case 3:
                    SlashFx.Create(foeAt, robot.transform.position, new Color(1f, 0.35f, 0.3f), kind == 3 ? 2.4f : 1.8f);
                    Shockwave.Create(robot.transform.position, kind == 3 ? 2.6f : 1.8f, new Color(1f, 0.4f, 0.3f));
                    cameraRig.Shake(kind == 3 ? 0.7f : 0.45f);
                    if (kind == 3) cameraRig.StartCoroutine(BigMoment(true));
                    break;
                case 4: cameraRig.StartCoroutine(Ultimate(false, leap: false)); break;
                case 5:
                    myCombo = 0; myComboMove = 0;
                    strikeCooldown = Mathf.Max(strikeCooldown, 0.9f);
                    Shockwave.Create(foeAt, 2.4f, new Color(1f, 0.45f, 0.4f));
                    skillBar?.Shout(Loc.T("duel.broken"), new Color(1f, 0.45f, 0.4f));
                    break;
                case 6: cameraRig.StartCoroutine(Finisher(robot.transform.position, false)); break;
            }
        }
    }
}
