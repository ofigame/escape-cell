using System.Collections;
using SquashBot.Audio;
using SquashBot.Combat;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;
using WeaponFamily = SquashBot.Combat.WeaponFamily;

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
        private bool controlsHooked;
        private float myMeter, rivalMeter;
        private int myCombo, rivalCombo, myComboMove;
        private float myComboAt, rivalComboAt, comboMoveUntil, finishUntil, rivalThinkAt;
        private Vector3 finishAt, lastFoeAt;
        private bool specialStrike, ultimateOn, finishing, moveBusy;
        private int rivalHero;

        // The duel's numbers come from its table (DuelBalanceTable: the same file the server reads), the bot duel and the live one alike.
        private static DuelBalanceTable Bal => DuelBalanceTable.Current;
        private static float MeterGain => Bal.meterGain;
        private static float MeterHurt => Bal.meterHurt;
        private static float ComboGap => Bal.comboGap;
        private static float ComboMoveTime => Bal.comboMoveSeconds;
        /// <summary>Blows in a row for the power hit and the knockdown.</summary>
        private static int HeavyCombo => Bal.heavyCombo;
        private static int UpperCombo => Bal.upperCombo;

        /// <summary>A blow's share of the bar by its weapon's pace (a quick blade's many blows fill it no faster than a
        /// slow hammer's few).</summary>
        private static float MeterPace(float cooldown) => Bal.MeterPace(cooldown);

        private float MyPace => MeterPace(Armory.Cooldown(weapon ?? Armory.Equipped));
        private float RivalPace => MeterPace(Armory.Cooldown(Armory.Get(rival.weapon), Mathf.Max(1, rival.weaponLevel)));
        /// <summary>What each move takes off (times one of the mover's own blows).</summary>
        private static float SpecialTimes => Bal.flyTimes;
        private static float HeavyHit => Bal.heavyTimes;
        private static float UpperHit => Bal.upperTimes;
        private static float UltimateHit => Bal.UltiTotal(UltiKind.Cinematic);
        /// <summary>A blow struck in the air takes this much more (half a blow again; live: the server counts it too).</summary>
        private static float JumpExtra => Bal.jumpExtra;
        private float myJumpUntil, myJumpReadyAt, rivalJumpAt;
        /// <summary>FLY in the air: the stand-in trails the thrusters' blue flame.</summary>
        private bool flyThrusters;
        /// <summary>Until when the rival is in the air (its jump): a leap over it meets it there.</summary>
        private float rivalAirUntil;

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
            skillBar = DuelSkillBar.Create(chUi.BattleHud, Loc.T("move.s." + MoveSet.Special), MoveSet.Special); // (the set's special on its button)
            skillBar.Pressed += OnSkill;
            skillBar.ShieldDown += GuardPress;
            skillBar.ShieldUp += GuardRelease;
            skillBar.HeartPressed += UseDuelHeart;
            skillBar.PausePressed += Pause;
            skillBar.LeaveConfirmed += ShowMenu;
            skillBar.SetLive(LiveActive);
            if (!controlsHooked) { controlsHooked = true; DuelControlsPanel.Changed += () => skillBar?.ApplySettings(); }
            ui.SetPauseVisible(false); // (the duel's own small pause, top left)
            BeginGuard();
            skillBar.Emoted += e => { if (ShowEmote(true, e) && LiveActive) LiveNet.Emote(e); };
            skillBar.TauntPressed += KnockdownTaunt;
            emotesUsed = 0;
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
            EndGuard();
            if (ui != null) ui.SetPauseVisible(true);
            if (skillBar != null) Destroy(skillBar.gameObject);
            skillBar = null;
            DropRings();
            moveBusy = false;
            if (ultimateOn || Time.timeScale < 0.99f) { ultimateOn = false; SkinnedHero.ClashOff = false; if (State == GameState.Playing) Time.timeScale = 1f; }
            slowCamUntil = slowCamWeight = 0f;
        }

        // ---------- Blows landing ----------

        /// <summary>foi's blow landed on the rival: the bar fills, the combo grows (three: a power hit; five: a knockdown).</summary>
        private void OnFoeStruck()
        {
            if (specialStrike || !challengeRun || chPhase != ChPhase.Duel) return;
            myMeter = Mathf.Min(3f, myMeter + MeterGain * MyPace * MyCharge);
            if (!LiveActive) rivalMeter = Mathf.Min(3f, rivalMeter + MeterHurt * MyPace * RivalCharge);
            stats.mine.hits++;
            myCombo = Time.time - myComboAt < MyComboGap ? myCombo + 1 : 1;
            myComboAt = Time.time;
            rivalCombo = 0;
            if (skillBar != null) skillBar.ShowCombo(myCombo);
            TrainingNote(TrainingEvent.Hit, myCombo);
            if (myCombo == HeavyCombo || myCombo == UpperCombo) { myComboMove = myCombo; comboMoveUntil = Time.time + ComboMoveTime; stats.mine.comboOffered++; }
            if (myCombo == HeavyCombo) NoteDuel(DuelGoal.Combo5); // (a five-hit combo)
            if (Time.time < myJumpUntil) AirStrike(true);
            else { Reel(false, myCombo); if (myCombo % 4 == 0) ShieldBash(false); }
            if (colosseum != null && myCombo >= 3) colosseum.Cheer(0, 0.4f + myCombo * 0.08f);
            duelStirAt = Time.time;
        }

        /// <summary>The AI rival parried foi's blow: foi's combo is broken, foi reels a moment before its next swing.</summary>
        private void OnFoeParried()
        {
            if (!challengeRun || chPhase != ChPhase.Duel) return;
            if (hunt.FoeGuard != null) return; // (the defence's perfect guard showed itself: see PerfectShown)
            myCombo = 0;
            myComboMove = 0;
            strikeCooldown = Mathf.Max(strikeCooldown, 0.55f);
            skillBar?.ShowCombo(0);
            skillBar?.Shout(Loc.T("duel.parried"), UI.UiKit.Style.info);
            if (heldWeapon != null) heldWeapon.Recoil(); // (foi's blade knocked aside)
            var parrier = hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.GetComponentInChildren<HeldWeapon>() : null;
            if (parrier != null) parrier.Swing(CombatLibrary.Family(parrier.Family).Blow(2)); // (its weapon across to meet it)
            if (hunt.ChallengeFoeAt is Vector3 pAt) CombatFeel.ClashAt((pAt + robot.transform.position) * 0.5f + Vector3.up * 1.2f, pAt - robot.transform.position);
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
            stats.theirs.hits++; stats.theirs.blows++;
            rivalCombo = Time.time - rivalComboAt < RivalComboGap ? rivalCombo + 1 : 1;
            rivalComboAt = Time.time;
            myCombo = 0;
            myComboMove = 0;
            if (colosseum != null && rivalCombo >= 3) colosseum.Cheer(1, 0.4f + rivalCombo * 0.08f);
            Reel(true, rivalCombo);
            if (rivalCombo % 4 == 0) ShieldBash(true);
            duelStirAt = Time.time;
        }

        // ---------- Each frame ----------

        private void UpdateDuelSkills(float dt)
        {
            if (skillBar == null) return;
            // how hurt each looks: smoke under half health, electric arcs under a quarter
            if (robot.Visual != null) StatusFx.On(CombatFeel.RootOf(robot.Visual)).SetDamage(health < 0.25f ? 2 : health < 0.5f ? 1 : 0);
            if (hunt.ChallengeFoeModel != null) { float fh = hunt.ChallengeFoeHealth; StatusFx.On(CombatFeel.RootOf(hunt.ChallengeFoeModel)).SetDamage(fh < 0.25f ? 2 : fh < 0.5f ? 1 : 0); }
            DuelBodiesShown();
            FaceRest(robot.Visual, health < 0.25f ? FaceMoment.LowHealth : FaceMoment.Idle); // (worried, a drop of sweat, when low)
            if (hunt.ChallengeFoeModel != null) FaceRest(hunt.ChallengeFoeModel, hunt.ChallengeFoeHealth < 0.25f ? FaceMoment.LowHealth : FaceMoment.Idle);
            if (hunt.ChallengeFoeAt is Vector3 seen) lastFoeAt = seen;
            if (myRing != null) myRing.Follow(robot.IsAlive && robot.Visual != null && robot.Visual.gameObject.activeInHierarchy ? robot.transform.position : (Vector3?)null);
            if (foeRing != null) foeRing.Follow(hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.position : (Vector3?)null);
            if (myCombo > 0 && Time.time - myComboAt > MyComboGap) myCombo = 0;
            if (rivalCombo > 0 && Time.time - rivalComboAt > RivalComboGap) rivalCombo = 0;
            if (myComboMove > 0 && Time.time > comboMoveUntil) { myComboMove = 0; stats.mine.comboMissed++; }
            stats.Tick(dt, myMeter >= UltiCost, rivalMeter >= UltiCost);
            DuelSkillBar.Move? ctx = null;
            if (Time.time < finishUntil) ctx = DuelSkillBar.Move.Finish;
            else if (chPhase == ChPhase.Duel && !moveBusy)
            {
                if (rivalCombo >= 2 && myMeter >= 1f) ctx = DuelSkillBar.Move.Breaker;
                else if (myComboMove == UpperCombo) ctx = DuelSkillBar.Move.Upper;
                else if (myComboMove == HeavyCombo) ctx = DuelSkillBar.Move.Heavy;
                else if (BodyOffer() is PhysKind offered) { ctx = DuelSkillBar.Move.Body; skillBar.SetBodyOffer(offered == PhysKind.Uppercut); } // (a body special, now and then)
            }
            skillBar.SetContext(ctx);
            if (ctx.HasValue) skillBar.SetContextTime(ctx == DuelSkillBar.Move.Finish ? (finishUntil - Time.time) / 2.2f : ctx == DuelSkillBar.Move.Breaker ? 1f : ctx == DuelSkillBar.Move.Body ? BodyOfferLeft : (comboMoveUntil - Time.time) / ComboMoveTime);
            skillBar.SetMeters(myMeter, rivalMeter, UltiCost);
            skillBar.SetHearts(duelHearts, chPhase == ChPhase.Duel && health < 0.999f);
            UpdateGuard(dt);
            if (!LiveActive && chPhase == ChPhase.Duel && !moveBusy && !DuelCinematic) RivalMoves(); // (never inside a big moment)
            // Standing, foi keeps turned to the rival (after a leap, a shove, a step back: never its back to it)
            if (chPhase == ChPhase.Duel && robot.IsIdle && hunt.ChallengeFoeAt is Vector3 rivalAt) robot.TurnTo(rivalAt);
            Unstick();
        }

        /// <summary>In the duel: a step that would bring foi closer than the gap (three tiles, two empty ones between them).</summary>
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
                    if (!GuardCanStrike) return; // (blocking, staggered or dazed)
                    if (!LiveActive) BotReadsBigMove(false); // (the rival may see it coming and step out of it)
                    myComboMove = 0;
                    myCombo = 0;
                    stats.mine.comboUsed++;
                    { var cs = robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null; if (cs != null) cs.PlayAction(upper ? DuelAction.ComboKnockdown : DuelAction.ComboHeavy, 0f, 0.1f); } // (its own clip, its contact now)
                    robot.Strike(foeAt);
                    ComboMoveShown(true, upper);
                    cameraRig.StartCoroutine(RushThenHit(robot.Visual, hunt.ChallengeFoeModel, upper ? HitHeight.Low : HitHeight.Mid, 5)); // (onto its body, the blow's effect as it touches)
                    Shockwave.Create(foeAt, upper ? 2.6f : 1.8f, upper ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 0.75f, 0.35f));
                    cameraRig.Shake(upper ? 0.7f : 0.45f);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, upper ? 0.7f : 0.85f);
                    skillBar.Shout(Loc.T(upper ? "duel.upper" : "duel.heavy") + "!", UI.UiKit.Style.orange);
                    if (LiveActive) LiveNet.Special(upper ? 3 : 2);
                    else
                    {
                        HitFoe((upper ? UpperHit : HeavyHit) * DuelPower.ComboTimes(weapon ?? Armory.Equipped), upper ? BlowKind.Knockdown : BlowKind.PowerHit); // (an axe's a fifth harder)
                        bool landed = lastGuardFoe == DefenseOutcome.Hit;
                        if (landed) TrainingNote(upper ? TrainingEvent.Knockdown : TrainingEvent.PowerHit);
                        else if (!upper && (lastGuardFoe == DefenseOutcome.Blocked || lastGuardFoe == DefenseOutcome.Broke)) TrainingNote(TrainingEvent.PowerThroughBlock);
                        if (upper && landed) PowerSlow(0.3f, 0.35f); // (the knockdown: a breath of slow motion)
                        if (landed) hunt.StunFoe(upper ? Bal.upperStun : Bal.heavyStun); // (short: the rival is back in the fight at once)
                        else if (lastGuardFoe == DefenseOutcome.Dodged) strikeCooldown = Mathf.Max(strikeCooldown, 0.4f + Def.whiffRecovery); // (into the air: slower back)
                        if (upper && !landed) { if (colosseum != null) colosseum.Cheer(1, 0.6f); break; } // (blocked or dodged: no throw)
                    }
                    if (colosseum != null) colosseum.Cheer(0, 0.9f);
                    if (upper && LiveActive) cameraRig.StartCoroutine(BigMoment(false)); // the knockdown: a big moment
                    else if (upper) PhysLaunch(true); // (the knockdown launches it like the uppercut: it can be hit in the air)
                    break;
                }
                case DuelSkillBar.Move.Breaker:
                    if (myMeter < 1f || rivalCombo < 2) return;
                    if (Time.time < rivalIronUntil) { skillBar.Shout(Loc.T("move.u.IronBody"), UI.UiKit.Style.silver); return; } // (its iron body: no breaking it)
                    PlayStyle.Breaker();
                    stats.mine.breakers++;
                    myMeter -= 1f;
                    rivalCombo = 0;
                    { var ks = robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null; if (ks != null && ks.PlayAction(DuelAction.ComboBreaker)) VisualRoot.On(CombatFeel.RootOf(robot.Visual)).Lunge(foeAt - robot.transform.position, VisualRoot.KeptApart(robot.transform.position, foeAt, 0.5f), 0.12f, 0.06f, 0.25f); } // (KIR: the cut, quicker, and a shoulder pushed in)
                    Shockwave.Create(robot.transform.position, 2.4f, new Color(0.4f, 0.8f, 1f));
                    fx.Burst(robot.transform.position + Vector3.up * 0.6f, new Color(0.5f, 0.85f, 1f), new Color(1f, 2f, 3f), 30, 6f);
                    AudioManager.PlaySfx(Sfx.Shield, 1f, 0.8f);
                    skillBar.Shout(Loc.T("duel.broken"), UI.UiKit.Style.info);
                    BreakerShown(false);
                    if (LiveActive) LiveNet.Special(5);
                    else
                    {
                        hunt.StunFoe(Bal.breakerStun);
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
                case DuelSkillBar.Move.Body:
                    TakeBodyOffer();
                    break;
                case DuelSkillBar.Move.Ultimate:
                    if (myMeter < UltiCost) return; // (the quick-ulti week: two parts)
                    myMeter = 0f;
                    PlayStyle.Ulti(hunt.ChallengeFoeHealth);
                    UseUlti(true, myUlti); // (the set's ultimate)
                    break;
            }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>A move's damage on the AI rival: through its shield and health like a blow, but no combo or bar for it.</summary>
        private void HitFoe(float times, BlowKind kind = BlowKind.Special, float cost = -1f)
        {
            if (!hunt.ChallengeFoeTile.HasValue) return;
            strikeKind = kind;
            strikeCost = cost;
            lastGuardFoe = DefenseOutcome.Hit;
            var w = weapon ?? Armory.Equipped;
            int damage = Mathf.RoundToInt(MyBlow(w) * times * MyBlowFactor());
            specialStrike = true;
            hunt.Strike(hunt.ChallengeFoeTile.Value, Mathf.Max(1, damage));
            specialStrike = false;
            MyVampire(damage);
        }

        /// <summary>A move's damage on foi from the AI rival: its own blow times this, through foi's shield like a blow.</summary>
        private void HurtMe(float times, BlowKind kind = BlowKind.Special, float cost = -1f)
        {
            float share = hunt.ChallengeFoeShare * times;
            if (share <= 0f) return;
            incomingKind = kind;
            incomingCost = cost;
            lastGuardMe = DefenseOutcome.Hit;
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
            if (flyThrusters) ThrusterTrail(dbl.transform, model.lossyScale.y);
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

        /// <summary>A trail of blue thruster flame on a flying stand-in (FLY).</summary>
        private static void ThrusterTrail(Transform flier, float scale)
        {
            var go = new GameObject("Thrusters");
            go.transform.SetParent(flier, false);
            go.transform.localPosition = new Vector3(0f, 0.9f * Mathf.Max(0.3f, scale) / Mathf.Max(0.01f, flier.lossyScale.y), -0.25f);
            var tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = Weather.AddMaterial;
            tr.time = 0.35f;
            tr.widthMultiplier = 0.35f * Mathf.Max(0.3f, scale);
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.85f, 0.95f, 1f), 0f), new GradientColorKey(new Color(0.3f, 0.6f, 1f), 0.4f), new GradientColorKey(new Color(0.2f, 0.3f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
                    if (LaneOn && p.x != laneX) continue;
                    if (grid.InBounds(p) && grid.IsStandable(p) && !grid.IsOccupied(p)) return p;
                }
            return null;
        }

        // ---------- The special ----------

        private IEnumerator SpecialMove(bool mine)
        {
            moveBusy = true;
            if (mine) myFlying = true; else rivalFlying = true;
            if (mine ? rivalFlying : myFlying) cameraRig.StartCoroutine(AirClash()); // (both flying at once: they meet in the air)
            if (mine) NoteDuel(DuelGoal.Special);
            int hero = mine ? HeroModels.Skill(SaveData.Hero) : rivalHero;
            if (mine) hunt.StunFoe(1.6f);
            strikeCooldown = Mathf.Max(strikeCooldown, mine ? 0.9f : 1.4f);
            // the take-off: a crouch, blue flame from the back's thrusters, the weapon drawn back
            var flier = mine ? robot.Visual : hunt.ChallengeFoeModel;
            var flierSkin = flier != null ? flier.GetComponentInChildren<SkinnedHero>() : null;
            if (flierSkin != null) flierSkin.PlayAction(DuelAction.SpecialFly, 0f, 0.65f); // (FLY: the flying punch, its punch on the strike after the leap)
            if (flierSkin != null)
            {
                flierSkin.Crouch(1f, 0.3f);
                var back = flierSkin.BackMount != null ? flierSkin.BackMount.position : flier.position + Vector3.up;
                for (int i = 0; i < 3; i++) fx.Burst(back - flier.forward * 0.2f, new Color(0.45f, 0.75f, 1f), new Color(1.2f, 2f, 3.2f), 10, 3f);
                AudioManager.PlaySfx(Sfx.SwishSpear, 0.8f, 0.7f);
                yield return new WaitForSeconds(0.15f);
            }
            flyThrusters = true;
            yield return LeapTo(mine, hero);
            flyThrusters = false;
            if (mine) myFlying = false; else rivalFlying = false;
            if (Time.time - airClashAt < 1.5f) { moveBusy = false; yield break; } // (met in the air: a clash, no blow)
            if (!challengeRun || chPhase != ChPhase.Duel || !hunt.ChallengeFoeAt.HasValue) { moveBusy = false; yield break; }
            var foeAt = hunt.ChallengeFoeAt.Value;
            // it rushes onto the other's body and stops there: the blow lands as it touches (the collision rules 3.1)
            var flyStruck = mine ? hunt.ChallengeFoeModel : robot.Visual;
            RushToTouch(flier, flyStruck);
            yield return WaitForTouch(flier, flyStruck);
            if (mine) robot.Strike(foeAt);
            // the flying strike by the weapon's family, and a kneeling landing in dust
            var flyHeld = flier != null ? flier.GetComponentInChildren<HeldWeapon>() : null;
            if (flyHeld != null)
            {
                var fly = CombatLibrary.Family(flyHeld.Family).flyStrike;
                flyHeld.SwingQuick(fly, 0.03f);
                CombatFeel.Hit(flier, flyStruck, fly);
            }

            if (flierSkin != null) { flierSkin.Crouch(0.9f, 0.3f); flierSkin.Squash(0.7f); }
            if (flier != null) fx.Dust(flier.position, new Color(0.6f, 0.55f, 0.48f), 16, 3.5f);
            PowerFx(hero, mine ? robot.transform.position : foeAt, mine ? foeAt : robot.transform.position);
            if (mine)
            {
                if (LiveActive) LiveNet.Special(1);
                else HitFoe(SpecialTimes, BlowKind.Air);
                if (colosseum != null) colosseum.Cheer(0, 0.9f);
            }
            else HurtMe(SpecialTimes, BlowKind.Air);
            if (LiveActive || Landed(mine)) ShowHitOn(flier, flyStruck, HitHeight.Mid, 3); // (a hit: its effect on the contact point; a blocked or dodged one: none)
            if (LiveActive || Landed(mine)) yield return BigMoment(!mine); // (blocked or dodged: nobody is thrown)
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
        private IEnumerator BigMoment(bool meStruck, float height = 1.5f, int tiles = 2)
        {
            var model = meStruck ? robot.Visual : hunt.ChallengeFoeModel;
            if (model == null || !challengeRun || (meStruck ? !robot.IsAlive : hunt.ChallengeFoeDown)) yield break;
            hunt.StunFoe(1.8f); // the rival stands still through the moment, whoever was struck (and gets up after)
            CombatFeel.BigMoment(); // (the pictures slow; the game's clock never does)
            var flinger = meStruck ? hunt.ChallengeFoeModel : robot.Visual;
            var flingHeld = flinger != null ? flinger.GetComponentInChildren<HeldWeapon>() : null;
            CombatFeel.Hit(flinger, model, CombatLibrary.Family(flingHeld != null ? flingHeld.Family : WeaponFamily.Heavy).knockdown);
            SlowCam(model.position, 1.4f); // the big moment's time (slow motion, no blows); the camera stays on the board
            cameraRig.Shake(1.1f);
            var at = model.position;
            var from = meStruck ? (hunt.ChallengeFoeAt ?? at) : robot.transform.position;
            fx.Burst(at + Vector3.up * 0.6f, new Color(1f, 0.85f, 0.4f), new Color(3f, 2.4f, 0.8f), 26, 5f);
            AudioManager.PlaySfx(Sfx.Impact, 1f, 0.6f);
            Fling(meStruck, from, height, tiles);
            yield return new WaitForSecondsRealtime(0.55f + 0.1f * height);
            cameraRig.Shake(0.6f); // (landing)
            cameraRig.StartCoroutine(FaceWhenUp(meStruck));
            fx.Burst(at + Vector3.up * 0.2f, new Color(0.8f, 0.7f, 0.55f), new Color(1.4f, 1.2f, 1f), 16, 3f);
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
        /// <summary>A landed blow's effect (once per blow, a few on screen at most): the sparks thrown on the way it struck,
        /// the weapon's slash through the contact point, a ring — sized by its strength and how far the camera is.</summary>
        private void HitShown(int blowId, Vector3 at, Vector3 along, Color glow, int inARow, bool toHead)
        {
            if (!HitEffects.Claim(blowId)) return;
            DuelBoxesView.LastContact = at; DuelBoxesView.LastContactAt = Time.time;
            var cam = Camera.main;
            float k = cam != null ? Mathf.Clamp(Vector3.Distance(cam.transform.position, at) / 22f, 0.7f, 1.6f) : 1f; // (as big on screen near or far)
            int more = Mathf.Min(inARow, 5);
            var dir = along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.forward;
            SlashFx.Create(at - dir * 0.9f * k + Vector3.up * 0.15f * k, at + dir * 0.5f * k - Vector3.up * 0.1f * k, glow, (1f + 0.1f * more) * k);
            if (toHead) return; // (the head: the cartoon puff and stars, CombatFeel.HeadStruck)
            fx.BurstAlong(at, dir, new Color(1f, 0.95f, 0.7f), new Color(3f, 2.6f, 1.6f), Mathf.RoundToInt(14 * k), 4.2f * k);
            fx.BurstAlong(at, dir, glow, glow * 2.4f, Mathf.RoundToInt((10 + 2 * more) * k), 5.5f * k);
            Shockwave.Create(at - Vector3.up * 0.1f, (0.6f + 0.08f * more) * k, Color.Lerp(new Color(1f, 0.95f, 0.75f), glow, 0.4f));
        }

        /// <summary>The same blow confirmed again later (the lab's simulated server): its effect must not show twice.</summary>
        private IEnumerator HitConfirmedLater(float seconds, int blowId, Vector3 at, Vector3 along, Color glow, int inARow, bool toHead)
        {
            yield return new WaitForSecondsRealtime(seconds);
            HitShown(blowId, at, along, glow, inARow, toHead);
        }

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
            // the feel (freeze, the camera's knock along the blow, the picture pushed back, the family's impact sound) on
            // the striker's swing's contact: the same for the player and the rival
            var blow = held != null ? (held.Current ?? CombatLibrary.Family(held.Family).Blow(0)) : CombatLibrary.Family(WeaponFamily.Sword).Blow(0);
            bool toHead = blow.height == HitHeight.High || (held != null && held.Overhead);
            // where it lands: where the hit box (the weapon's tip; a body blow: the striker) meets the struck part (its head,
            // its chest, its legs) — on that part's surface toward the weapon (the collision rules 5.2)
            skin.HurtSpots(out var headAt, out var chestAt, out var legsAt);
            var part = toHead ? headAt : blow.height == HitHeight.Low ? legsAt : chestAt;
            var weaponAt = held != null && held.Tip != null && blow.body == BodyStrike.Weapon ? held.Tip.position : striker != null ? striker.position + Vector3.up * skin.PushBoxRadius * 2f : part;
            var at = HitEffects.ContactPoint(weaponAt, part, skin.PushBoxRadius * (toHead ? 0.55f : 0.6f));
            var along = model.position - (striker != null ? striker.position : from); along.y = 0f;
            // the blow seen: on the body a white flash of sparks and the weapon's colour flying on the way it struck, a ring;
            // on the head a cartoon puff and stars instead (CombatFeel.HeadStruck): robots' bits only off the body. One blow,
            // one effect: a confirmation arriving later is the same blow (the lab's delay shows it)
            var wDef = meStruck ? (rival != null ? Armory.Get(rival.weapon) : null) : Armory.Equipped;
            var glow = wDef != null ? WeaponModels.GlowOf(wDef) : new Color(1f, 0.8f, 0.4f);
            int blowId = !meStruck && HitEffects.CurrentBlow > 0 ? HitEffects.CurrentBlow : HitEffects.NewBlow(); // (foi's landing named it; the rival's blow a new one)
            if (!meStruck) HitEffects.CurrentBlow = 0;
            HitShown(blowId, at, along, glow, inARow, toHead);
            if (LabLatencyMs > 0) cameraRig.StartCoroutine(HitConfirmedLater(LabLatencyMs / 1000f, blowId, at, along, glow, inARow, toHead));
            FaceFor(model, blow.strength >= HitStrength.Heavy || inARow >= 4 ? FaceMoment.HeavyHit : FaceMoment.LightHit);
            if (blow.strength >= HitStrength.Heavy || inARow >= 4) HatFor(model, 2, from); else if (blow.strength >= HitStrength.Medium || inARow >= 2) HatFor(model, 1, from);
            // the body's answer (by the blow's direction, height and strength; harder deeper into a combo) on the contact
            CombatFeel.Hit(striker, model, blow, 1f + 0.12f * Mathf.Min(Mathf.Max(0, inARow - 1), 5));
            if (inARow >= 4 && !LiveActive) Push(meStruck, from, 1);
        }

        /// <summary>
        /// A combo move by family (<paramref name="upper"/>: KNOCKDOWN, else POWER HIT). POWER HIT: a sword's big two-handed
        /// slash, a spear's long lunge with the whole body, an axe's two full turns, a heavy weapon's leap and crash down
        /// (dust round it). KNOCKDOWN: the family's upward blow (the big moment throws the rival). The feel of the blow on
        /// its contact (POWER HIT: a heavy reaction, 90 ms freeze; KNOCKDOWN's is the big moment's).
        /// </summary>
        private void ComboMoveShown(bool mine, bool upper)
        {
            var striker = mine ? robot.Visual : hunt.ChallengeFoeModel;
            var struck = mine ? hunt.ChallengeFoeModel : robot.Visual;
            var held = striker != null ? striker.GetComponentInChildren<HeldWeapon>() : null;
            if (held == null || struck == null) return;
            var family = CombatLibrary.Family(held.Family);
            var blow = upper ? family.knockdown : family.powerHit;
            held.SwingQuick(blow, 0.03f);
            var skin = striker.GetComponentInChildren<SkinnedHero>();
            var toward = struck.position - striker.position;
            switch (held.Family)
            {
                case WeaponFamily.Axe: if (!upper) held.Spin(2); break; // (two full turns)
                case WeaponFamily.Spear: VisualRoot.On(CombatFeel.RootOf(striker)).Push(toward, VisualRoot.KeptApart(striker.position, struck.position, upper ? 0.25f : 0.5f), 0.3f); break; // (the long lunge, never into the other)
                case WeaponFamily.Heavy:
                    if (!upper && skin != null) { skin.Hop(0.9f, 0.32f); cameraRig.StartCoroutine(DustAfter(struck.position, 0.3f)); } // (a leap and a crash down)
                    break;
            }
            if (upper) { cameraRig.Punch(0.45f); return; } // (the knockdown: the camera in a little; the big moment does the rest)
            CombatFeel.Hit(striker, struck, blow);
        }

        /// <summary>A ring of dust kicked up at <paramref name="at"/> a moment later (a heavy weapon crashing down, a landing).</summary>
        private IEnumerator DustAfter(Vector3 at, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            fx.Dust(at, new Color(0.6f, 0.55f, 0.48f), 20, 4.5f);
            Shockwave.Create(at, 1.6f, new Color(0.85f, 0.75f, 0.6f));
        }

        private float myGlowAt, rivalGlowAt;

        /// <summary>
        /// Each frame: the bodies tell their state. Low on health, hunched and heaving; deeper into a combo, the trail
        /// thicker and (from four) the weapon sparkling (the POWER HIT coming); the bar's parts full, the weapon glinting;
        /// the ultimate ready, a ring of energy round the feet now and then.
        /// </summary>
        private void DuelBodiesShown()
        {
            ShowState(robot.Visual, health, myCombo, myMeter, ref myGlowAt);
            if (hunt.ChallengeFoeModel != null) ShowState(hunt.ChallengeFoeModel, hunt.ChallengeFoeHealth, rivalCombo, rivalMeter, ref rivalGlowAt);
        }

        private void ShowState(Transform who, float hp, int combo, float meter, ref float glowAt)
        {
            if (who == null) return;
            var skin = who.GetComponentInChildren<SkinnedHero>();
            if (skin != null) skin.Weary = hp < 0.3f;
            var held = who.GetComponentInChildren<HeldWeapon>();
            if (held != null) held.Combo = combo;
            if (Time.time < glowAt) return;
            glowAt = Time.time + 0.4f;
            var tip = held != null && held.Tip != null ? held.Tip.position : who.position + Vector3.up;
            if (meter >= UltiCost) { glowAt = Time.time + 0.7f; Shockwave.Create(who.position, 1.1f, new Color(0.75f, 0.45f, 1f)); fx.Burst(tip, new Color(0.8f, 0.55f, 1f), new Color(2f, 1.3f, 3f), 6, 1.5f); }
            else if (combo >= 4) fx.Burst(tip, new Color(1f, 0.75f, 0.3f), new Color(3f, 2f, 0.6f), 6, 1.6f);
            else if (meter >= 1f) fx.Burst(tip, new Color(1f, 0.92f, 0.6f), new Color(2.4f, 2.2f, 1.2f), 3, 1f);
        }

        /// <summary>A combo broken (<paramref name="onMe"/>: the rival broke foi's): the one breaking comes forward with its
        /// shoulder, the one broken is thrown back reeling, its weapon knocked aside.</summary>
        private void BreakerShown(bool onMe)
        {
            var breaker = onMe ? hunt.ChallengeFoeModel : robot.Visual;
            var broken = onMe ? robot.Visual : hunt.ChallengeFoeModel;
            if (breaker == null || broken == null) return;
            var dir = broken.position - breaker.position;
            VisualRoot.On(CombatFeel.RootOf(breaker)).Push(dir, 0.3f, 0.35f);
            var skin = broken.GetComponentInChildren<SkinnedHero>();
            if (skin != null) skin.React(HitDirection.Thrust, HitHeight.Mid, HitStrength.Heavy, breaker.position);
            var held = broken.GetComponentInChildren<HeldWeapon>();
            if (held != null) held.Recoil();
            VisualTime.On(CombatFeel.RootOf(breaker)).Freeze(0.08f);
            VisualTime.On(CombatFeel.RootOf(broken)).Freeze(0.08f);
            CameraImpulse.Fire(CameraImpulseLevel.Medium, dir, CombatFeel.ShakeMultiplier);
        }

        /// <summary>Every fourth blow in a row the striker carrying a shield strikes with it too: thrust out, a thud, the
        /// one struck reeling harder (foi's shield: <paramref name="meStruck"/> false).</summary>
        private void ShieldBash(bool meStruck)
        {
            var striker = meStruck ? hunt.ChallengeFoeModel : robot.Visual;
            var target = meStruck ? robot.Visual : hunt.ChallengeFoeModel;
            var hold = striker != null ? striker.GetComponentInChildren<ShieldHold>() : null;
            if (hold == null || !hold.Carried || target == null) return;
            hold.Bash();
            var skin = target.GetComponentInChildren<SkinnedHero>();
            var from = striker.position;
            cameraRig.StartCoroutine(FollowUp(() =>
            {
                if (skin == null) return;
                var at = skin.ChestPoint;
                fx.Burst(at, new Color(0.75f, 0.9f, 1f), new Color(1.8f, 2.2f, 2.6f), 16, 4f);
                Shockwave.Create(at, 0.8f, new Color(0.7f, 0.85f, 1f));
                AudioManager.PlaySfx(Sfx.Bump, 0.8f, 0.6f);
                // (no shake: every fourth blow shaking the view looked unsteady)
                skin.Stagger(from, 0.75f);
                skin.Shudder(from, 1f);
            }));
        }

        /// <summary>The one struck flies back off its feet and lands on its back (and is pushed <paramref name="tiles"/> back).</summary>
        private void Fling(bool meStruck, Vector3 from, float height, int tiles)
        {
            var model = meStruck ? robot.Visual : hunt.ChallengeFoeModel;
            var skin = model != null ? model.GetComponentInChildren<SkinnedHero>() : null;
            if (skin != null) skin.Launch(from, height);
            FaceFor(model, FaceMoment.Launched); // (mouth wide open, eyes big)
            HatFor(model, 2, from);
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
            if (LaneOn) step = new GridPos(0, d.y >= 0f ? 1 : -1); // (the lane duel: back along the line)
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
            ultimateOn = true; SkinnedHero.ClashOff = true;
            skillBar?.Shout(Loc.T("duel.ultTitle"), mine ? UI.UiKit.Style.purple : UI.UiKit.Style.theirs);
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
            if (casterSkin != null && !casterSkin.PlayAction(DuelAction.UltiCinematic, combo)) casterSkin.PlayMove("cast", ComboRate, true, loop: true); // (its own clip over the combo; the old one round and round otherwise)
            if (mine && LiveActive) LiveNet.Special(4);
            CombatFeel.UltiStart(caster);
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
                var ultiStruck = mine ? hunt.ChallengeFoeModel : robot.Visual;
                RushToTouch(caster, ultiStruck, 0.12f); // (step by step onto the other's body, never into it)
                if (held != null) held.Swing();
                yield return WaitForTouch(caster, ultiStruck, 0.3f); // (the effect as the bodies touch, never before)
                if (i == 4) Shockwave.Create(to, 3.2f, colour);
                cameraRig.Shake(i == 4 ? 0.9f : 0.3f);
                AudioManager.PlaySfx(Sfx.Impact, 1f, i == 4 ? 0.55f : 1.1f);
                if (!LiveActive)
                {
                    float part = i == 4 ? UltimateHit * 0.5f : UltimateHit * 0.125f;
                    float cost = Def.ultiCost * part / UltimateHit; // (the whole ultimate blocked costs the shield its 60)
                    if (mine) HitFoe(part, BlowKind.Ulti, cost); else HurtMe(part, BlowKind.Ulti, cost);
                }
                if (LiveActive || Landed(mine)) ShowHitOn(caster, ultiStruck, HitHeight.Mid, i == 4 ? 5 : 1); // (the middle blows' effects small, the last big; blocked: none)
                if (i < 4) DriveBack(mine); // (struck, it is driven back; the one striking follows it up)
                if (i == 4) { CombatFeel.UltiFinal(caster, mine ? hunt.ChallengeFoeModel : robot.Visual); if (LiveActive || Landed(mine)) yield return BigMoment(!mine); } // (the final: the longest freeze, a white flash)
            }
            if (casterSkin != null) casterSkin.StopMove();
            ultimateOn = false; SkinnedHero.ClashOff = false;
            if (State == GameState.Playing) Time.timeScale = 1f;
            moveBusy = false;
        }

        // ---------- The knockdown's fall, the jump ----------

        /// <summary>foi jumps: a blow landed in the air knocks the rival's head off its neck (for a moment) and takes more.</summary>
        private void Jump()
        {
            if (Time.time < myJumpReadyAt || !robot.IsAlive) return;
            myJumpReadyAt = Time.time + 0.9f;
            tutJumped = true;
            if (challengeRun) PlayStyle.Jump();
            myJumpUntil = Time.time + 0.65f;
            var skin = robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null;
            if (skin != null) skin.Hop(1.1f, 0.65f);
            AudioManager.PlaySfx(Sfx.Hop, 1f, 1.1f);
        }

        /// <summary>
        /// The lane duel's flick: straight up, a jump on the spot (a blow in the air knocks the rival down); leaning, a
        /// leap that way along the line, further the more it leans (one to three tiles); leaning hard toward the rival,
        /// a leap right over it, landing behind it.
        /// </summary>
        private void LaneJump(Vector2 flick, bool curl = false)
        {
            if (Time.time < myJumpReadyAt || !robot.IsAlive || moveBusy || DuelCinematic) return;
            tutJumped = true;
            float lean = Mathf.Atan2(Mathf.Abs(flick.x), flick.y) * Mathf.Rad2Deg; // (0 straight up .. 70 nearly flat)
            if (lean < 12f && !curl) { Jump(); return; }
            if (lean < 12f) { myJumpReadyAt = Time.time + 0.9f; myJumpUntil = Time.time + 0.65f; if (challengeRun) PlayStyle.Jump(); cameraRig.StartCoroutine(LaneLeap(robot.Position, true)); return; } // (curled: a somersault on the spot)
            myJumpReadyAt = Time.time + 0.9f;
            if (challengeRun) PlayStyle.Jump();
            int dir = flick.x > 0f ? 1 : -1; // (the screen's right is up the lane)
            var me = robot.Position;
            var foe = hunt.ChallengeFoeTile;
            bool toward = foe.HasValue && System.Math.Sign(foe.Value.y - me.y) == dir;
            GridPos? land = null;
            bool over = false;
            if (toward && System.Math.Abs(foe.Value.y - me.y) <= 7) // (any lean toward it: over it)
            {
                // over the rival: the first free tile behind it (two, then one, then three)
                foreach (int k in new[] { HuntSystem.DuelGap, HuntSystem.DuelGap - 1, HuntSystem.DuelGap + 1 })
                {
                    var p = new GridPos(laneX, foe.Value.y + dir * k);
                    if (grid.InBounds(p) && grid.IsStandable(p) && !grid.IsOccupied(p) && !grid.IsGap(p)) { land = p; over = true; break; }
                }
            }
            if (!land.HasValue)
            {
                int far = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, 3f, Mathf.InverseLerp(12f, 45f, lean))), 1, 3);
                for (int k = far; k >= 1 && !land.HasValue; k--)
                {
                    var p = new GridPos(laneX, me.y + dir * k);
                    if (foe.HasValue && toward && System.Math.Abs(foe.Value.y - p.y) < HuntSystem.DuelGap) continue; // (never inside the duel's gap)
                    if (foe.HasValue && toward && System.Math.Sign(foe.Value.y - p.y) != dir) continue; // (only a hard lean goes over)
                    if (grid.InBounds(p) && grid.IsStandable(p) && !grid.IsOccupied(p) && !grid.IsGap(p)) land = p;
                }
            }
            if (!land.HasValue) { Jump(); return; }
            myInvulnUntil = Time.time + (over ? 0.7f : 0.5f); // (in the air: the rival's swing goes under)
            if (over && !LiveActive && Time.time > rivalAirUntil && Random.value < 0.25f) cameraRig.StartCoroutine(RivalBlockJump()); // (now and then it jumps to meet foi)
            if (over) cameraRig.StartCoroutine(LeapOver(robot.Position, land.Value, curl));
            else cameraRig.StartCoroutine(LaneLeap(land.Value, curl));
        }

        /// <summary>The rival jumps straight up to meet foi's leap over it (a moment after foi takes off).</summary>
        private IEnumerator RivalBlockJump()
        {
            yield return new WaitForSeconds(0.08f);
            var skin = hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.GetComponentInChildren<SkinnedHero>() : null;
            if (skin == null || !challengeRun || chPhase != ChPhase.Duel) yield break;
            skin.Hop(1.4f, 0.65f);
            rivalAirUntil = Time.time + 0.65f;
            AudioManager.PlaySfx(Sfx.Hop, 1f, 1.0f);
        }

        private IEnumerator LaneLeap(GridPos land, bool flip)
        {
            moveBusy = true;
            AudioManager.PlaySfx(Sfx.Hop, 1f, 1.2f);
            fx.Burst(robot.transform.position + Vector3.up * 0.2f, new Color(0.9f, 0.9f, 1f), new Color(1.5f, 1.5f, 2f), 10, 3f);
            yield return LeapToTile(true, land, flip ? 2.4f : 1.4f, flip ? 0 : -1); // (curled: higher, with a somersault)
            FaceEachOther();
            moveBusy = false;
        }

        /// <summary>
        /// The leap over the rival: high, with a somersault, landing behind it. If the rival is in the air as foi comes
        /// over it (its own jump), they crash: sparks where they meet, both shaken, and foi drops back to its own side.
        /// </summary>
        private IEnumerator LeapOver(GridPos start, GridPos land, bool flip)
        {
            moveBusy = true;
            var model = robot.Visual;
            if (model == null) { moveBusy = false; yield break; }
            AudioManager.PlaySfx(Sfx.Hop, 1f, 0.9f);
            fx.Burst(robot.transform.position + Vector3.up * 0.2f, new Color(0.9f, 0.9f, 1f), new Color(1.5f, 1.5f, 2f), 14, 3.5f);
            var dbl = StuntDouble.Make(model);
            bool crashed = false;
            try
            {
                var from = dbl.transform.position;
                var to = GridView.ToWorld(land) + Vector3.up * GridView.SurfaceY;
                var flat = to - from; flat.y = 0f;
                var face = flat.sqrMagnitude > 0.001f ? Quaternion.LookRotation(flat) : dbl.transform.rotation;
                const float T = 0.62f, Height = 5.2f; // (well over the rival's head)
                float t = 0f;
                for (; t < 1f; )
                {
                    t = Mathf.Min(1f, t + Time.unscaledDeltaTime / T);
                    float k = t * t * (3f - 2f * t);
                    dbl.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(t * Mathf.PI) * Height;
                    dbl.transform.rotation = face * (flip ? Quaternion.Euler(360f * k, 0f, 0f) : Quaternion.Euler(Mathf.Sin(t * Mathf.PI) * -25f, 0f, 0f)); // (curled: a somersault; straight: a leaning leap)
                    // over the rival while it is up too: they meet in the air
                    if (t > 0.3f && t < 0.6f && Time.time < rivalAirUntil && !LiveActive) { crashed = true; break; }
                    yield return null;
                }
                if (crashed)
                {
                    var meet = dbl.transform.position;
                    fx.Burst(meet, new Color(1f, 0.9f, 0.6f), new Color(3f, 2.6f, 1.4f), 40, 7f);
                    Shockwave.Create(meet, 1.6f, new Color(1f, 0.9f, 0.6f));
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 0.8f);
                    cameraRig.Shake(0.7f);
                    skillBar?.Shout(Loc.T("duel.clash"), UI.UiKit.Style.gold);
                    var rs = hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.GetComponentInChildren<SkinnedHero>() : null;
                    if (rs != null) { rs.Stagger(meet, 0.6f); rs.Shudder(meet, 1f); }
                    hunt.StunFoe(0.4f);
                    // foi falls back down to its own side, tumbling
                    var back = GridView.ToWorld(start) + Vector3.up * GridView.SurfaceY;
                    var at = dbl.transform.position;
                    var rot = dbl.transform.rotation;
                    for (float u = 0f; u < 1f; )
                    {
                        u = Mathf.Min(1f, u + Time.unscaledDeltaTime / 0.4f);
                        var p = Vector3.Lerp(at, back, u);
                        p.y = Mathf.Lerp(at.y, back.y, u * u) + Mathf.Sin(u * Mathf.PI) * 0.6f;
                        dbl.transform.position = p;
                        dbl.transform.rotation = rot * Quaternion.Euler(-200f * u, 0f, 0f);
                        yield return null;
                    }
                    robot.Spawn(grid, start);
                }
                else robot.Spawn(grid, land);
                FaceEachOther();
            }
            finally { if (dbl != null) dbl.Done(); }
            var skin = model.GetComponentInChildren<SkinnedHero>();
            if (crashed) { if (skin != null) { skin.Stagger(GridView.ToWorld(land), 0.6f); skin.Shudder(GridView.ToWorld(land), 1f); } }
            else { skillBar?.Shout(Loc.T("duel.over"), UI.UiKit.Style.cyan); hunt.StunFoe(0.25f); cameraRig.Shake(0.25f); }
            moveBusy = false;
        }

        /// <summary>A blow struck in the air: the head of the one struck flies off and comes back, and it takes a little more.</summary>
        private void AirStrike(bool mine)
        {
            if (mine) myJumpUntil = 0f; else rivalJumpAt = Time.time + 9f; // (one per jump)
            var struck = mine ? hunt.ChallengeFoeModel : robot.Visual;
            var airman = mine ? robot.Visual : hunt.ChallengeFoeModel;
            var airHeld = airman != null ? airman.GetComponentInChildren<HeldWeapon>() : null;
            if (airHeld != null && struck != null)
            {
                // down from over the head (every family), a bigger burst, a longer freeze; a cloud of dust as it lands
                var air = CombatLibrary.Family(airHeld.Family).airStrike;
                airHeld.SwingQuick(air, 0.03f);
                CombatFeel.Hit(airman, struck, air, 1.3f);
                cameraRig.StartCoroutine(DustAfter(airman.position, 0.35f));
            }
            var at = struck != null ? struck.position : robot.transform.position;
            if (!LiveActive && mine) { HitFoe(JumpExtra, BlowKind.Air); if (!Landed(true)) return; } // (the extra on the rival first: blocked, no throw)
            if (mine) TrainingNote(TrainingEvent.AirHit);
            Fling(!mine, mine ? robot.transform.position : (hunt.ChallengeFoeAt ?? at), 1f, 1); // knocked off its feet, onto its back
            if (mine) hunt.StunFoe(0.7f);
            fx.Burst(at + Vector3.up * 1.4f, new Color(1f, 0.9f, 0.5f), new Color(3f, 2.6f, 1f), 24, 6f);
            cameraRig.Shake(0.5f);
            AudioManager.PlaySfx(Sfx.Impact, 1f, 1.2f);
            skillBar?.Shout(Loc.T("duel.airHit"), mine ? UI.UiKit.Style.gold : UI.UiKit.Style.danger);
            if (!LiveActive && !mine) HurtMe(JumpExtra, BlowKind.Air);
        }

        /// <summary>The AI rival jumps in and strikes from the air now and then.</summary>
        private System.Collections.IEnumerator RivalJumpStrike()
        {
            var model = hunt.ChallengeFoeModel;
            var skin = model != null ? model.GetComponentInChildren<SkinnedHero>() : null;
            if (skin == null) yield break;
            skin.Hop(1.1f, 0.65f);
            rivalAirUntil = Time.time + 0.65f;
            AudioManager.PlaySfx(Sfx.Hop, 1f, 1.0f);
            yield return new WaitForSeconds(0.32f);
            if (!challengeRun || chPhase != ChPhase.Duel || DuelCinematic || !hunt.ChallengeFoeAt.HasValue) yield break;
            if (Flat(hunt.ChallengeFoeAt.Value - robot.transform.position).magnitude > 3.6f) yield break;
            hunt.TurnFoe(robot.transform.position - hunt.ChallengeFoeAt.Value);
            SlashFx.Create(hunt.ChallengeFoeAt.Value, robot.transform.position, new Color(1f, 0.4f, 0.3f), 1.6f);
            HurtMe(1f, BlowKind.Air);
            if (Landed(false)) AirStrike(false); // (blocked or dodged: no throw)
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
            var winner = mine ? robot.Visual : hunt.ChallengeFoeModel;
            var winnerHeld = winner != null ? winner.GetComponentInChildren<HeldWeapon>() : null;
            if (winnerHeld != null) winnerHeld.Flourish(); // (the family's flourish: sheathed, shouldered, planted, struck on the ground)
            skillBar?.Shout(Loc.T("duel.finished"), mine ? UI.UiKit.Style.gold : UI.UiKit.Style.theirs);
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
        private bool ShowEmote(bool mine, int e)
        {
            if (!mine && SaveData.HideRivalEmotes) return false; // (the setting: the rival's emotes not shown)
            if (mine && (Time.unscaledTime < emoteAt || emotesUsed >= EmotesAMatch)) return false; // (one every 3 s, 15 a match)
            if (mine) { emoteAt = Time.unscaledTime + EmoteGap; emotesUsed++; }
            var who = mine ? robot.Visual : hunt.ChallengeFoeModel;
            var at = mine ? robot.transform.position : hunt.ChallengeFoeAt ?? lastFoeAt;
            bool taunt = FaceTaunts.IsTaunt(e);
            // a face taunt: the face makes it for a moment, its name in a little balloon over the head
            if (taunt) FaceFor(who, FaceTaunts.Of(e));
            string text = taunt ? Loc.T("taunt." + FaceTaunts.Of(e)) : Loc.T("duel.em." + Mathf.Clamp(e, 0, Emotes.Count - 1));
            FloatAt(at + Vector3.up * (taunt ? 2.2f : 1.3f), text, mine ? UI.UiKit.Style.info : UI.UiKit.Style.danger);
            AudioManager.PlaySfx(Sfx.Click, 0.7f, mine ? 1.5f : 1.2f);
            return true;
        }

        private int emotesUsed;
        private const float EmoteGap = 3f;
        private const int EmotesAMatch = 15;

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
            if (Time.time < rivalThinkAt || !hunt.ChallengeFoeAt.HasValue || LabOn) return; // (the lab's rival never fights back)
            if (tutorialOn) return; // (the guided duel: the rival only does what the step needs)
            if (trainingOn) return; // (the training room's dummy does only what its mode says)
            if (rivalGuard.Holding || rivalGuard.Staggered(Time.time) || rivalGuard.Dazed(Time.time)) return; // (behind its shield, staggered or dazed: no move)
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
            // (the breaker costs the bot its bar part as it costs the player: never free)
            if (myCombo >= Mathf.Max(breakFrom, Bal.breakerMinCombo) && !keepsBar && Time.time >= myIronUntil && rivalMeter >= Bal.breakerCost && Random.value < 0.35f + 0.5f * breakLean)
            {
                rivalMeter -= Bal.breakerCost;
                myCombo = 0;
                myComboMove = 0;
                stats.mine.brokenByRival++; stats.theirs.breakers++;
                strikeCooldown = Mathf.Max(strikeCooldown, Bal.breakerStun); // foi's swing knocked aside (as long as the player's breaker holds the bot)
                BreakerShown(true);
                Shockwave.Create(foeAt, 2.4f, new Color(1f, 0.45f, 0.4f));
                AudioManager.PlaySfx(Sfx.Shield, 1f, 0.75f);
                skillBar?.Shout(Loc.T("duel.broken"), UI.UiKit.Style.danger);
                return;
            }
            if (RivalPhysical(dist)) return; // (a sweep, an uppercut, a headbutt, a shoulder: its personality's)
            if (dist <= 3.4f && Time.time > rivalJumpAt && Random.value < (t != null ? 0.03f + 0.12f * t.jump : 0.06f + 0.05f * bold))
            {
                rivalJumpAt = Time.time + 9f;
                cameraRig.StartCoroutine(RivalJumpStrike());
                return;
            }
            if (rivalCombo >= HeavyCombo && dist <= 3.4f && Random.value < 0.5f)
            {
                rivalCombo = 0;
                FaceEachOther();
                ComboMoveShown(false, false);
                SlashFx.Create(foeAt, robot.transform.position, new Color(1f, 0.35f, 0.3f), 1.8f);
                Shockwave.Create(robot.transform.position, 1.8f, new Color(1f, 0.4f, 0.3f));
                cameraRig.Shake(0.45f);
                HurtMe(HeavyHit * DuelPower.ComboTimes(Armory.Get(rival.weapon)), BlowKind.PowerHit); // (its axe's a fifth harder, as the player's)
                if (lastGuardMe == DefenseOutcome.Hit) strikeCooldown = Mathf.Max(strikeCooldown, Bal.heavyStun); // (landed: foi stunned as the bot would be)
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
                    strikeCooldown = Mathf.Max(strikeCooldown, kind == 3 ? Bal.upperStun : Bal.heavyStun); // (the server holds foi's blows as long)
                    if (kind == 3) cameraRig.StartCoroutine(BigMoment(true));
                    break;
                case 4: cameraRig.StartCoroutine(Ultimate(false, leap: false)); break;
                case 5:
                    myCombo = 0; myComboMove = 0;
                    stats.mine.brokenByRival++;
                    strikeCooldown = Mathf.Max(strikeCooldown, Bal.breakerStun);
                    Shockwave.Create(foeAt, 2.4f, new Color(1f, 0.45f, 0.4f));
                    skillBar?.Shout(Loc.T("duel.broken"), UI.UiKit.Style.danger);
                    break;
                case 6: cameraRig.StartCoroutine(Finisher(robot.transform.position, false)); break;
            }
        }
    }
}
