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
        private const float SpecialTimes = 1.8f, HeavyHit = 1.8f, UpperHit = 2.5f, UltimateHit = 6f;

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
            }
            myMeter = rivalMeter = 0f;
            ResetCombos();
            finishUntil = 0f;
            moveBusy = false;
            rivalHero = 0; // one foi for everyone
            if (skillBar != null) Destroy(skillBar.gameObject);
            skillBar = DuelSkillBar.Create(chUi.BattleHud, Loc.T("duel.sp." + Mathf.Clamp(SaveData.Hero, 0, 3)));
            skillBar.Pressed += OnSkill;
            skillBar.Emoted += e => { ShowEmote(true, e); if (LiveActive) LiveNet.Emote(e); };
            skillBar.SetMeters(0f, 0f);
        }

        private void ResetCombos()
        {
            myCombo = rivalCombo = myComboMove = 0;
            comboMoveUntil = 0f;
        }

        private void EndDuelSkills()
        {
            pendingHp = null;
            if (skillBar != null) Destroy(skillBar.gameObject);
            skillBar = null;
            moveBusy = false;
            if (ultimateOn || Time.timeScale < 0.99f) { ultimateOn = false; if (State == GameState.Playing) Time.timeScale = 1f; }
            slowCamUntil = slowCamWeight = 0f;
        }

        // ---------- Blows landing ----------

        /// <summary>foi's blow landed on the rival: the bar fills, the combo grows (three: a power hit; five: a knockdown).</summary>
        private void OnFoeStruck()
        {
            if (specialStrike || !challengeRun || chPhase != ChPhase.Duel) return;
            myMeter = Mathf.Min(3f, myMeter + MeterGain * MyPace);
            if (!LiveActive) rivalMeter = Mathf.Min(3f, rivalMeter + MeterHurt * MyPace);
            myCombo = Time.time - myComboAt < ComboGap ? myCombo + 1 : 1;
            myComboAt = Time.time;
            rivalCombo = 0;
            if (skillBar != null) skillBar.ShowCombo(myCombo);
            if (myCombo == HeavyCombo || myCombo == UpperCombo) { myComboMove = myCombo; comboMoveUntil = Time.time + ComboMoveTime; }
            if (myCombo == HeavyCombo) ChallengeQuests.Report(DuelGoal.Combo5); // (a five-hit combo)
            if (colosseum != null && myCombo >= 3) colosseum.Cheer(0, 0.4f + myCombo * 0.08f);
        }

        /// <summary>The rival's blow landed on foi: its bar and combo grow, foi's combo is broken.</summary>
        private void OnFoiStruck()
        {
            if (!challengeRun || chPhase != ChPhase.Duel) return;
            myMeter = Mathf.Min(3f, myMeter + MeterHurt * RivalPace);
            if (!LiveActive) rivalMeter = Mathf.Min(3f, rivalMeter + MeterGain * RivalPace);
            rivalCombo = Time.time - rivalComboAt < ComboGap ? rivalCombo + 1 : 1;
            rivalComboAt = Time.time;
            myCombo = 0;
            myComboMove = 0;
            if (colosseum != null && rivalCombo >= 3) colosseum.Cheer(1, 0.4f + rivalCombo * 0.08f);
        }

        // ---------- Each frame ----------

        private void UpdateDuelSkills(float dt)
        {
            if (skillBar == null) return;
            if (hunt.ChallengeFoeAt is Vector3 seen) lastFoeAt = seen;
            if (myCombo > 0 && Time.time - myComboAt > ComboGap) myCombo = 0;
            if (rivalCombo > 0 && Time.time - rivalComboAt > ComboGap) rivalCombo = 0;
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
            if (!LiveActive && chPhase == ChPhase.Duel && !moveBusy) RivalMoves();
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
                    if (myMeter < 1f) return;
                    myMeter -= 1f;
                    cameraRig.StartCoroutine(SpecialMove(true));
                    break;
                case DuelSkillBar.Move.Heavy:
                case DuelSkillBar.Move.Upper:
                {
                    bool upper = move == DuelSkillBar.Move.Upper;
                    if (myComboMove != (upper ? 5 : 3) || HuntSystem.Chebyshev(robot.Position, foeTile) > StrikeReach + 1) return;
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
                        HitFoe(upper ? UpperHit : HeavyHit);
                        hunt.StunFoe(upper ? 1.6f : 0.8f);
                    }
                    if (colosseum != null) colosseum.Cheer(0, 0.9f);
                    if (upper) cameraRig.StartCoroutine(BigMoment(false)); // the knockdown: a big moment
                    break;
                }
                case DuelSkillBar.Move.Breaker:
                    if (myMeter < 1f || rivalCombo < 2) return;
                    myMeter -= 1f;
                    rivalCombo = 0;
                    Shockwave.Create(robot.transform.position, 2.4f, new Color(0.4f, 0.8f, 1f));
                    fx.Burst(robot.transform.position + Vector3.up * 0.6f, new Color(0.5f, 0.85f, 1f), new Color(1f, 2f, 3f), 30, 6f);
                    AudioManager.PlaySfx(Sfx.Shield, 1f, 0.8f);
                    skillBar.Shout(Loc.T("duel.broken"), new Color(0.5f, 0.85f, 1f));
                    if (LiveActive) LiveNet.Special(5);
                    else hunt.StunFoe(1f);
                    break;
                case DuelSkillBar.Move.Ultimate:
                    if (myMeter < 3f) return;
                    myMeter = 0f;
                    cameraRig.StartCoroutine(Ultimate(true, leap: true));
                    break;
            }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>A move's damage on the AI rival: through its shield and health like a blow, but no combo or bar for it.</summary>
        private void HitFoe(float times)
        {
            if (!hunt.ChallengeFoeTile.HasValue) return;
            var w = weapon ?? Armory.Equipped;
            int damage = Mathf.RoundToInt(Challenge.RankBoost(Armory.Damage(w)) * times);
            specialStrike = true;
            hunt.Strike(hunt.ChallengeFoeTile.Value, Mathf.Max(1, damage));
            specialStrike = false;
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
        private IEnumerator LeapTo(bool mine, int stunt)
        {
            var from = mine ? robot.Position : hunt.ChallengeFoeTile ?? robot.Position;
            var victim = mine ? hunt.ChallengeFoeTile ?? robot.Position : robot.Position;
            if (HuntSystem.Chebyshev(from, victim) <= 1) yield break;
            var model = mine ? robot.Visual : hunt.ChallengeFoeModel;
            if (model == null) yield break;
            var land = LandingBeside(victim, from);
            if (!land.HasValue) yield break;
            var dbl = StuntDouble.Make(model);
            AudioManager.PlaySfx(Sfx.Hop, 1f, stunt == 2 ? 0.7f : 1.2f);
            fx.Burst(model.position + Vector3.up * 0.2f, SpecialColour[stunt], SpecialColour[stunt] * 1.6f, 14, 4f);
            yield return dbl.Leap(GridView.ToWorld(land.Value) + Vector3.up * GridView.SurfaceY, 1.6f, 0.5f, stunt);
            if (mine) robot.Spawn(grid, land.Value); else hunt.PlaceFoe(land.Value);
            dbl.Done();
            if (stunt == 2) { Shockwave.Create(GridView.ToWorld(land.Value) + Vector3.up * GridView.SurfaceY, 1.6f, SpecialColour[2]); cameraRig.Shake(0.4f); }
        }

        /// <summary>A free tile next to <paramref name="victim"/>, on the side the mover comes from.</summary>
        private GridPos? LandingBeside(GridPos victim, GridPos from)
        {
            int dx = System.Math.Sign(from.x - victim.x), dy = System.Math.Sign(from.y - victim.y);
            var tries = new[] { new GridPos(dx, dy), new GridPos(dx, 0), new GridPos(0, dy), new GridPos(-dy, dx), new GridPos(dy, -dx), new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1) };
            foreach (var d in tries)
            {
                if (d.x == 0 && d.y == 0) continue;
                var p = victim + d;
                if (grid.InBounds(p) && grid.IsStandable(p) && !grid.IsOccupied(p)) return p;
            }
            return null;
        }

        // ---------- The special ----------

        private IEnumerator SpecialMove(bool mine)
        {
            moveBusy = true;
            if (mine) ChallengeQuests.Report(DuelGoal.Special);
            int hero = mine ? Mathf.Clamp(SaveData.Hero, 0, 3) : rivalHero;
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
        /// Time slows; the robot struck (foi's own, or the rival's) bursts into its pieces, hangs, and comes back together;
        /// then the fight goes on. Nothing if it fell for good (the knock-down shows itself).
        /// </summary>
        private IEnumerator BigMoment(bool meStruck)
        {
            var model = meStruck ? robot.Visual : hunt.ChallengeFoeModel;
            if (model == null || !challengeRun || (meStruck ? !robot.IsAlive : hunt.ChallengeFoeDown)) yield break;
            hunt.StunFoe(1.4f); // the rival stands still through the moment, whoever was struck
            float was = Time.timeScale;
            Time.timeScale = 0.25f;
            SlowCam(model.position, 1.9f); // the camera goes in close and circles them
            var dbl = StuntDouble.Make(model);
            fx.Burst(model.position + Vector3.up * 0.5f, new Color(1f, 0.85f, 0.4f), new Color(3f, 2.4f, 0.8f), 26, 5f);
            AudioManager.PlaySfx(Sfx.Blocked, 1f, 0.6f);
            yield return dbl.Shatter(model.position + Vector3.up * 0.45f);
            fx.Burst(model.position + Vector3.up * 0.5f, new Color(0.7f, 0.85f, 1f), new Color(1.6f, 2f, 2.6f), 18, 3f);
            AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.5f);
            dbl.Done();
            if (State == GameState.Playing) Time.timeScale = ultimateOn ? 0.35f : (was < 0.99f && was > 0.3f ? was : 1f);
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
            if (mine) hunt.StunFoe(4f);
            strikeCooldown = Mathf.Max(strikeCooldown, mine ? 0.5f : 2.5f);
            if (mine) ChallengeQuests.Report(DuelGoal.Ultimate);
            if (leap) yield return LeapTo(mine, mine ? Mathf.Clamp(SaveData.Hero, 0, 3) : rivalHero);
            if (mine && LiveActive) LiveNet.Special(4);
            Time.timeScale = 0.35f;
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForSecondsRealtime(i == 4 ? 0.35f : 0.2f);
                if (!challengeRun || chPhase != ChPhase.Duel) break;
                var foeAt = hunt.ChallengeFoeAt ?? robot.transform.position;
                var from = mine ? robot.transform.position : foeAt;
                var to = mine ? foeAt : robot.transform.position;
                var colour = mine ? new Color(0.8f, 0.5f, 1f) : new Color(1f, 0.35f, 0.3f);
                if (mine) robot.Strike(to);
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
                if (i == 4) yield return BigMoment(!mine);
            }
            ultimateOn = false;
            if (State == GameState.Playing) Time.timeScale = 1f;
            moveBusy = false;
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
            rivalThinkAt = Time.time + 0.45f;
            var foeAt = hunt.ChallengeFoeAt.Value;
            float dist = Flat(foeAt - robot.transform.position).magnitude;
            float bold = Mathf.Clamp01(rival.boldness);
            if (rivalMeter >= 3f && Random.value < 0.3f + 0.3f * bold)
            {
                rivalMeter = 0f;
                cameraRig.StartCoroutine(Ultimate(false, leap: true));
                return;
            }
            if (rivalMeter >= 1f && myCombo >= 4 && Random.value < 0.3f)
            {
                rivalMeter -= 1f;
                myCombo = 0;
                myComboMove = 0;
                strikeCooldown = Mathf.Max(strikeCooldown, 0.9f); // foi's swing knocked aside
                Shockwave.Create(foeAt, 2.4f, new Color(1f, 0.45f, 0.4f));
                AudioManager.PlaySfx(Sfx.Shield, 1f, 0.75f);
                skillBar?.Shout(Loc.T("duel.broken"), new Color(1f, 0.45f, 0.4f));
                return;
            }
            if (rivalCombo >= HeavyCombo && dist <= 2.2f && Random.value < 0.5f)
            {
                rivalCombo = 0;
                SlashFx.Create(foeAt, robot.transform.position, new Color(1f, 0.35f, 0.3f), 1.8f);
                Shockwave.Create(robot.transform.position, 1.8f, new Color(1f, 0.4f, 0.3f));
                cameraRig.Shake(0.45f);
                HurtMe(HeavyHit);
                return;
            }
            if (rivalMeter >= 1f && Random.value < (dist >= 3f ? 0.22f : 0.1f) + 0.15f * bold)
            {
                rivalMeter -= 1f;
                cameraRig.StartCoroutine(SpecialMove(false));
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
        private void LiveRivalMove(int kind)
        {
            if (!challengeRun) return;
            var foeAt = hunt.ChallengeFoeAt ?? robot.transform.position;
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
