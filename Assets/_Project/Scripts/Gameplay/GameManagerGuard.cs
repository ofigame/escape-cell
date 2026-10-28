using System.Collections;
using System.Collections.Generic;
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
    /// The duel's defence (the design's §3), for foi and the AI rival alike: hold the SHIELD to block (a fifth of the
    /// damage through, durability spent, half pace, no blows; at nothing it breaks and foi is dazed a second), press it
    /// just before a blow lands for a PERFECT GUARD (nothing through, the striker staggers half a second, the next blow
    /// 30% harder and half a bar part), swipe down to DODGE (a quick step back, a quarter second untouchable). The AI
    /// rival decides by its rank and personality as foi's blow starts (so it is early or late now and then too), learns
    /// a habit foi keeps repeating, and dodges ultimates. In a live match the server judges every blow (the press and
    /// the dodge go to it); this game shows what it says.
    /// </summary>
    public partial class GameManager
    {
        private readonly DefenseState myGuard = new DefenseState(), rivalGuard = new DefenseState();
        private DuelTouchReader duelTouches;
        private DuelTouchReader DuelTouches => duelTouches ??= new DuelTouchReader { Ignore = DuelTouchIgnored };

        /// <summary>The duel reads its own touches (every finger on its own): the duel's controls are up, not in TPS.</summary>
        private bool DuelTouchesOn => challengeRun && skillBar != null && !tpsOn && level != null && level.mission == MissionType.Hunt;

        private bool DuelTouchIgnored(Vector2 p) => (skillBar != null && skillBar.IsOver(p)) || (chUi != null && chUi.IsOverControls(p)) || DuelLabOver(p) || TrainingOver(p) || (ui != null && ui.IsOverTool(p));

        private static DefenseSettings Def => DefenseSettings.Current;

        /// <summary>Blocking: foi walks at half pace.</summary>
        private float GuardWalkScale => myGuard.Holding ? Def.walkWhileBlocking : 1f;

        /// <summary>The floors' defence is on (the design's §3.10: the same defence on the floors): a robot fight on a floor,
        /// upright, foi on its feet.</summary>
        private bool FloorGuardOn => !challengeRun && !arenaRun && level != null && level.mission == MissionType.Hunt && roadPhase == RoadPhase.None && State == GameState.Playing && !tpsOn && robot != null && robot.IsAlive;

        /// <summary>foi may strike (not blocking, not staggered, not dazed); always outside the duel.</summary>
        private bool GuardCanStrike => !((challengeRun && chPhase == ChPhase.Duel) || FloorGuardOn) || myGuard.CanStrike(Time.time);

        /// <summary>The kind of the blow about to land on foi (set by the move dealing it; plain otherwise), and its
        /// block cost when the move sets its own (an ultimate's blow its share of the whole).</summary>
        private BlowKind incomingKind = BlowKind.Plain;
        private float incomingCost = -1f;
        /// <summary>The kind of foi's blow about to land on the rival (plain unless a move sets it).</summary>
        private BlowKind strikeKind = BlowKind.Plain;
        private float strikeCost = -1f;
        /// <summary>How the last blow on foi, and foi's last on the rival, met the defence.</summary>
        private DefenseOutcome lastGuardMe = DefenseOutcome.Hit, lastGuardFoe = DefenseOutcome.Hit;
        /// <summary>The ultimate under way was dodged (the rest of it misses): foi's dodge of the rival's, the rival's of foi's.</summary>
        private bool ultiEvadedMe, ultiEvadedFoe;
        /// <summary>The last blow foi took (for a press a moment late: a perfect guard all the same, the damage back).</summary>
        private float lastHurtAt = -9f, lastHurtDamage;
        private float botHoldUntil;
        private bool keyGuard;
        /// <summary>foi's blows in the last half minute and their place in the combo (the AI learns a habit kept up).</summary>
        private readonly List<Vector2> habit = new List<Vector2>();

        private static readonly Color GuardBlue = new Color(0.45f, 0.8f, 1f), PerfectGold = new Color(1f, 0.92f, 0.55f);

        // ---------- Start and end ----------

        /// <summary>The shield's most durability: the shield master's 130, the shield war week twice that.</summary>
        private float GuardMax(PassiveKind passive)
        {
            float m = passive == PassiveKind.ShieldMaster ? Def.shieldMasterDurability : Def.maxDurability;
            if (duelRule == WeeklyMode.Rule.ShieldWar) m *= Def.shieldWarDurability;
            return m;
        }

        /// <summary>How fast a shield fills again (wood 30 … amethyst 36 a second).</summary>
        private static float RegenOf(int shield)
        {
            var r = Def.regenPerSecond;
            return r[Mathf.Clamp(shield, 0, r.Length - 1)];
        }

        private int MyShieldKind => Shields.All.IndexOf(Shields.Equipped) + 1;

        private void BeginGuard()
        {
            GuardRound();
            hunt.FoeGuard = LiveActive ? null : (System.Func<int, DefenseOutcome>)GuardFoe;
            DuelTouches.Reset();
            habit.Clear();
            if (!guardWired)
            {
                guardWired = true;
                hunt.ChallengeFoeBlocked += () => duelStirAt = Time.time;
                hunt.ChallengeFoeOutcome += (o, dmg, why) => ShowOutcome(o, dmg, why, o == HitOutcome.Hit && heldWeapon != null && heldWeapon.Current != null && heldWeapon.Current.strength >= HitStrength.Heavy);
            }
        }

        private bool guardWired;

        /// <summary>A round starts: both shields whole, nothing held.</summary>
        private void GuardRound()
        {
            myGuard.Reset(GuardMax(myPassive));
            rivalGuard.Reset(GuardMax(rivalPassive));
            hunt.FoeHolding = false;
            botHoldUntil = 0f;
            ultiEvadedMe = ultiEvadedFoe = false;
            skillBar?.ReleaseAll();
            ShowGuarding(robot != null ? robot.Visual : null, false);
            ShowGuarding(hunt.ChallengeFoeModel, false);
        }

        private void EndGuard()
        {
            hunt.FoeGuard = null;
            hunt.FoeHolding = false;
            ShowGuarding(robot != null ? robot.Visual : null, false);
            ShowGuarding(hunt.ChallengeFoeModel, false);
        }

        // ---------- foi's shield and dodge ----------

        /// <summary>The SHIELD pressed (the button or Shift): held from now; a press a moment after a blow landed still
        /// guards it perfectly (its damage back).</summary>
        private void GuardPress()
        {
            if (!challengeRun || chPhase != ChPhase.Duel) return;
            float now = Time.time;
            if (!myGuard.Press(now)) { AudioManager.PlaySfx(Sfx.Blocked, 0.4f, 1.8f); return; } // (locked or broken: a dull tick)
            if (LiveActive) LiveNet.Guard(true);
            PlayStyle.Shield();
            if (!LiveActive && myGuard.LatePerfect(now) && now - lastHurtAt <= Def.perfectAfter + 0.02f)
            {
                health = Mathf.Min(1f, health + lastHurtDamage);
                RefreshHealthBar();
                PerfectShown(true);
            }
        }

        private void GuardRelease()
        {
            if (!myGuard.Holding) return;
            myGuard.Release(Time.time);
            if (LiveActive) LiveNet.Guard(false);
        }

        /// <summary>Computers: Shift held is the shield held.</summary>
        private void GuardKeys()
        {
            bool k = DuelTouchReader.ShieldKeyHeld;
            if (k && !keyGuard) GuardPress();
            else if (!k && keyGuard && (skillBar == null || !skillBar.ShieldHeld)) GuardRelease();
            keyGuard = k;
        }

        /// <summary>A swipe down: a quick step back away from the rival (no room: a lean to the side), untouchable a moment.</summary>
        private void GuardDodge()
        {
            if (!challengeRun || chPhase != ChPhase.Duel || DuelCinematic || !robot.IsAlive || !hunt.ChallengeFoeTile.HasValue) return;
            if (moveBusy && !ultimateOn) return;
            float now = Time.time;
            var foe = hunt.ChallengeFoeTile.Value;
            var me = robot.Position;
            var away = new GridPos(System.Math.Sign(me.x - foe.x), System.Math.Sign(me.y - foe.y));
            if (LaneOn || away.x != 0 && away.y != 0) away = Mathf.Abs(me.y - foe.y) >= Mathf.Abs(me.x - foe.x) ? new GridPos(0, away.y != 0 ? away.y : 1) : new GridPos(away.x, 0);
            if (away.x == 0 && away.y == 0) away = new GridPos(0, -1);
            var to = new GridPos(me.x + away.x * Def.dodgeTiles, me.y + away.y * Def.dodgeTiles);
            bool room = grid.InBounds(to) && grid.IsStandable(to) && !grid.IsOccupied(to) && !grid.IsGap(to);
            if (!myGuard.Dodge(now, room ? Def.dodgeInvulnerable : Def.leanInvulnerable)) return;
            stats.mine.dodges++;
            if (LiveActive) LiveNet.Dodge();
            var from = robot.transform.position;
            if (room)
            {
                var dir = away.x > 0 ? Direction.PlusX : away.x < 0 ? Direction.MinusX : away.y > 0 ? Direction.PlusY : Direction.MinusY;
                robot.Shove(dir, Def.dodgeTiles, 0.25f, 1.6f);
                GuardFx.Streak(from + Vector3.up * 0.8f, GridView.ToWorld(to) + Vector3.up * (GridView.SurfaceY + 0.8f), new Color(0.7f, 0.85f, 1f, 0.6f));
            }
            else if (robot.Visual != null) VisualRoot.On(CombatFeel.RootOf(robot.Visual)).Push(Vector3.Cross(Vector3.up, GridView.ToWorld(foe) - from), 0.35f, 0.25f); // (a lean aside)
            fx.Dust(from, new Color(0.65f, 0.6f, 0.52f), 10, 2.6f);
            var mySkin = robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null;
            if (mySkin != null) mySkin.DodgeClip(); // (the body's own step back out of the way)
            AudioManager.PlaySfx(Sfx.Hop, 0.7f, 1.45f);
            Haptics.Light();
        }

        // ---------- Each frame ----------

        private void UpdateGuard(float dt)
        {
            if (skillBar == null) return;
            float now = Time.time;
            myGuard.Tick(now, dt, RegenOf(MyShieldKind));
            rivalGuard.Tick(now, dt, RegenOf(rival != null ? rival.shield : 1));
            if (!LiveActive) BotGuard(now);
            float share = LiveActive ? liveDurability[0] : myGuard.Share; // (live: the server keeps the durability)
            skillBar.SetShield(share, myGuard.Broken(now) || LiveActive && share <= 0f, myGuard.WaitShare(now));
            ShowGuarding(robot.Visual, myGuard.Holding);
            ShowGuarding(hunt.ChallengeFoeModel, rivalGuard.Holding);
            // the thin durability bar over foi's head while it blocks
            if (myGuard.Holding && cameraRig != null && robot.Visual != null)
            {
                var skin = robot.Visual.GetComponentInChildren<SkinnedHero>();
                var face = skin != null ? skin.FaceRenderer : null;
                var top = face != null ? new Vector3(face.bounds.center.x, face.bounds.max.y + 0.35f, face.bounds.center.z) : robot.transform.position + Vector3.up * 2.6f; // (over the head, hat and hair aside)
                var sp = cameraRig.Cam.WorldToScreenPoint(top);
                skillBar.SetGuardBar(sp.z > 0f ? (Vector2)sp : (Vector2?)null, share, share < Def.lowShare);
            }
            else skillBar.SetGuardBar(null, 0f, false);
            // and the rival's, over its head while it blocks (section 3.2: the player sees its shield wearing down)
            if (rivalGuard.Holding && cameraRig != null && hunt.ChallengeFoeModel != null)
            {
                var fskin = hunt.ChallengeFoeModel.GetComponentInChildren<SkinnedHero>();
                var fface = fskin != null ? fskin.FaceRenderer : null;
                var ftop = fface != null ? new Vector3(fface.bounds.center.x, fface.bounds.max.y + 0.35f, fface.bounds.center.z) : hunt.ChallengeFoeModel.position + Vector3.up * 2.6f;
                var fsp = cameraRig.Cam.WorldToScreenPoint(ftop);
                float fshare = LiveActive ? liveDurability[1] : rivalGuard.Share;
                skillBar.SetGuardBar(1, fsp.z > 0f ? (Vector2)fsp : (Vector2?)null, fshare, fshare < Def.lowShare);
            }
            else skillBar.SetGuardBar(1, null, 0f, false);
            // a key or button let go while not looking (the button hidden): the press ends
            if (myGuard.Holding && !keyGuard && !skillBar.ShieldHeld) GuardRelease();
        }

        /// <summary>A body blocking: its shield up across the front, the knees bent a little.</summary>
        private static void ShowGuarding(Transform who, bool on)
        {
            if (who == null) return;
            var hold = who.GetComponentInChildren<ShieldHold>();
            if (hold != null) hold.Guarding = on;
            var skin = who.GetComponentInChildren<SkinnedHero>();
            if (skin != null) skin.Guarding = on; // (the block's stance)
            if (!on) return;
            if (skin != null) skin.Crouch(0.3f, 0.15f);
        }

        /// <summary>The AI rival's shield over time: let go when its hold is over, or before it breaks (as well as its rank lets it).</summary>
        private void BotGuard(float now)
        {
            if (!rivalGuard.Holding) { hunt.FoeHolding = false; return; }
            var t = botTuning;
            bool letGo = now >= botHoldUntil;
            if (!letGo && rivalGuard.Share < 0.35f && t != null && Random.value < t.release * Time.deltaTime * 6f) letGo = true; // (a good one lets go before it breaks)
            if (letGo) rivalGuard.Release(now);
            hunt.FoeHolding = rivalGuard.Holding;
        }

        // ---------- A blow on foi ----------

        /// <summary>
        /// foi is about to take <paramref name="share"/> of its health from the rival (the AI game): how foi's defence met
        /// the blow decides what gets through. Shows the block, the perfect guard, the break, the dodge.
        /// </summary>
        private float GuardMe(float share)
        {
            var kind = incomingKind;
            float cost = incomingCost;
            incomingKind = BlowKind.Plain;
            incomingCost = -1f;
            if (LiveActive || !challengeRun || chPhase != ChPhase.Duel) { lastGuardMe = DefenseOutcome.Hit; return share; }
            float now = Time.time;
            if (kind == BlowKind.Ulti && ultiEvadedMe) { lastGuardMe = DefenseOutcome.Dodged; return 0f; }
            var o = myGuard.Judge(now, kind, cost >= 0f ? cost : DefenseState.Cost(kind, RivalFamily), out float through, GuardWindow);
            lastGuardMe = o;
            CountGuard(stats.mine, stats.theirs, o, kind, ref ultiCountedMe);
            if (o == DefenseOutcome.Hit) TrainingNote(TrainingEvent.Hurt);
            switch (o)
            {
                case DefenseOutcome.Perfect:
                    hunt.BlowBlocked = true;
                    PerfectShown(true);
                    TrainingNote(TrainingEvent.Perfect);
                    return 0f;
                case DefenseOutcome.Dodged:
                    hunt.BlowBlocked = true;
                    DodgedShown(true, kind);
                    if (kind == BlowKind.Ulti) ultiEvadedMe = true;
                    if (kind == BlowKind.Ulti) TrainingNote(TrainingEvent.DodgeUlti);
                    return 0f;
                case DefenseOutcome.Blocked:
                case DefenseOutcome.Broke:
                    hunt.BlowBlocked = true;
                    BlockShown(true, o == DefenseOutcome.Broke);
                    TrainingNote(o == DefenseOutcome.Broke ? TrainingEvent.ShieldBroke : TrainingEvent.Blocked);
                    return share * through;
            }
            return share;
        }

        /// <summary>The rival's weapon family (its plain blows' block cost).</summary>
        private WeaponFamily RivalFamily => rival != null ? CombatLibrary.FamilyOf(Armory.Get(rival.weapon).kind) : WeaponFamily.Sword;
        private WeaponFamily MyFamily => CombatLibrary.FamilyOf((weapon ?? Armory.Equipped).kind);

        // ---------- foi's blow on the rival ----------

        /// <summary>
        /// foi's blow lands on the AI rival (<paramref name="damage"/> points): the rival decides now, as a human would as
        /// the blow starts — a block by its rank's chance (up when foi keeps one habit), a perfect try (now and then too
        /// early or late: the random press's lock) — then its defence judges the blow.
        /// </summary>
        private DefenseOutcome GuardFoe(int damage)
        {
            var kind = strikeKind;
            float cost = strikeCost;
            strikeKind = BlowKind.Plain;
            strikeCost = -1f;
            float now = Time.time;
            if (!challengeRun || chPhase != ChPhase.Duel) return lastGuardFoe = DefenseOutcome.Hit;
            if ((LabOn || trainingOn) && !DummyDefends) return lastGuardFoe = DefenseOutcome.Hit; // (the lab's and the training room's dummy only defends when set to)
            if (tutorialOn) return lastGuardFoe = DefenseOutcome.Hit; // (the guided duel: no defence against the player)
            if (kind == BlowKind.Ulti && ultiEvadedFoe) return lastGuardFoe = DefenseOutcome.Dodged;
            var t = botTuning;
            if (kind != BlowKind.None && !rivalGuard.Holding && !rivalGuard.Broken(now) && !rivalGuard.Dazed(now) && !rivalGuard.Staggered(now) && !rivalGuard.Locked(now) && !rivalGuard.Untouchable(now))
            {
                float block = t != null ? t.block : 0.2f, perfect = t != null ? t.perfect : 0.05f;
                if (DummyDefends) { block = 0.9f; perfect = 0.35f; }
                float learned = Habit();
                if (kind != BlowKind.Ulti && Random.value < perfect + learned * 0.5f)
                {
                    if (!DummyDefends && Random.value < Def.botMistimed)
                    {
                        // too early: a press let go too soon (locked a moment), and the blow lands
                        rivalGuard.Press(now - 0.3f);
                        rivalGuard.Release(now - 0.15f);
                    }
                    else { rivalGuard.Press(now - Random.Range(0.01f, Def.perfectBefore * 0.9f)); botHoldUntil = now + 0.3f; }
                }
                else if (Random.value < block + learned)
                {
                    rivalGuard.Press(now - Def.perfectBefore - 0.05f - Random.value * 0.2f); // (raised a moment ago: a block, not a perfect guard)
                    botHoldUntil = now + Random.Range(Def.botHoldMin, Def.botHoldMax);
                }
            }
            var o = rivalGuard.Judge(now, kind, cost >= 0f ? cost : DefenseState.Cost(kind, MyFamily), out float through);
            CountGuard(stats.theirs, stats.mine, o, kind, ref ultiCountedFoe);
            hunt.FoeGuardThrough = through;
            hunt.FoeHolding = rivalGuard.Holding;
            lastGuardFoe = o;
            switch (o)
            {
                case DefenseOutcome.Perfect: PerfectShown(false); break;
                case DefenseOutcome.Dodged: DodgedShown(false, kind); if (kind == BlowKind.Ulti) ultiEvadedFoe = true; break;
                case DefenseOutcome.Blocked: case DefenseOutcome.Broke: BlockShown(false, o == DefenseOutcome.Broke); break;
            }
            return o;
        }

        /// <summary>How set in its ways foi has been for the last half minute (the share of its blows deep in a combo): the
        /// AI's block chance rises with it, up to a quarter more.</summary>
        private float Habit()
        {
            float now = Time.time, deep = 0f;
            habit.RemoveAll(h => now - h.x > Def.habitWindow);
            if (habit.Count < 6) return 0f;
            foreach (var h in habit) if (h.y >= 3f) deep++;
            return Def.habitMax * Mathf.Clamp01((deep / habit.Count - 0.3f) / 0.5f);
        }

        /// <summary>A blow of foi's noted for the habit (its place in the combo).</summary>
        private void NoteHabit(int comboAt) => habit.Add(new Vector2(Time.time, comboAt));

        /// <summary>The AI rival's dodge (an ultimate or a power hit coming): a quick step back, untouchable a moment.</summary>
        private void BotDodge()
        {
            if (!hunt.ChallengeFoeTile.HasValue) return;
            float now = Time.time;
            var foe = hunt.ChallengeFoeTile.Value;
            var away = new GridPos(System.Math.Sign(foe.x - robot.Position.x), System.Math.Sign(foe.y - robot.Position.y));
            if (LaneOn || away.x != 0 && away.y != 0) away = Mathf.Abs(foe.y - robot.Position.y) >= Mathf.Abs(foe.x - robot.Position.x) ? new GridPos(0, away.y != 0 ? away.y : 1) : new GridPos(away.x, 0);
            var to = new GridPos(foe.x + away.x * Def.dodgeTiles, foe.y + away.y * Def.dodgeTiles);
            bool room = grid.InBounds(to) && grid.IsStandable(to) && !grid.IsOccupied(to) && !grid.IsGap(to) && to != robot.Position;
            if (!rivalGuard.Dodge(now, room ? Def.dodgeInvulnerable + 0.1f : Def.leanInvulnerable)) return;
            stats.theirs.dodges++;
            if (room) cameraRig.StartCoroutine(hunt.ShoveFoe(to, Def.dodgeSeconds));
            var at = hunt.ChallengeFoeAt ?? GridView.ToWorld(foe);
            fx.Dust(at, new Color(0.65f, 0.6f, 0.52f), 10, 2.6f);
            var foeSkin = hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.GetComponentInChildren<SkinnedHero>() : null;
            if (foeSkin != null) foeSkin.DodgeClip();
            AudioManager.PlaySfx(Sfx.Hop, 0.7f, 1.3f);
        }

        /// <summary>foi's ultimate (or power hit) begins: the AI rival may see it coming and dodge it (its rank's chance;
        /// a power hit half as often), or block through it.</summary>
        /// <summary>When the bot dodges the player's ultimate after it starts (a marked one: as it lands).</summary>
        private float botUltiDodgeWait = 0.15f;

        private void BotReadsBigMove(bool ulti)
        {
            if (LiveActive || !challengeRun || tutorialOn || ((LabOn || trainingOn) && !DummyDefends)) return;
            var t = botTuning;
            float dodge = t != null ? t.ultiDodge : 0.25f;
            if (!ulti) dodge *= 0.5f;
            if (DummyDefends) dodge = 0.5f;
            if (Random.value < dodge) { cameraRig.StartCoroutine(BotDodgeSoon(ulti ? botUltiDodgeWait : 0f, ulti)); return; }
            if (ulti && Random.value < (t != null ? t.block : 0.2f) && rivalGuard.Press(Time.time - Def.perfectBefore - 0.1f)) botHoldUntil = Time.time + 2.5f; // (behind its shield through it)
        }

        private IEnumerator BotDodgeSoon(float wait, bool ulti)
        {
            if (wait > 0f) yield return new WaitForSeconds(wait);
            if (!challengeRun || chPhase != ChPhase.Duel) yield break;
            BotDodge();
            if (ulti && rivalGuard.Untouchable(Time.time)) ultiEvadedFoe = true;
        }

        /// <summary>foi's next blow: a perfect guard's reward on it (+30%), once.</summary>
        private float GuardBonusTake()
        {
            float k = 1f + myGuard.NextBlowBonus;
            myGuard.NextBlowBonus = 0f;
            return k;
        }

        // ---------- Seen ----------

        /// <summary>A blow blocked (<paramref name="mine"/>: foi blocked it): the shield up, a hexagon flash on it, a deep
        /// "dong", the one blocking pushed back a tenth of a tile; broken: the shield shatters and the one blocking is dazed.</summary>
        private void BlockShown(bool mine, bool broke)
        {
            var defender = mine ? robot.Visual : hunt.ChallengeFoeModel;
            var attacker = mine ? hunt.ChallengeFoeModel : robot.Visual;
            if (defender == null) return;
            CombatFeel.Blocked(attacker, defender);
            var hold = defender.GetComponentInChildren<ShieldHold>();
            var toward = attacker != null ? attacker.position - defender.position : defender.forward;
            var at = defender.position + Vector3.up * 0.9f * Mathf.Max(0.5f, defender.lossyScale.y) + toward.normalized * 0.45f;
            var guard = mine ? myGuard : rivalGuard;
            GuardFx.Hex(at, toward, guard.Low || broke ? new Color(1f, 0.45f, 0.35f) : GuardBlue, 0.5f * Mathf.Max(0.6f, defender.lossyScale.y));
            AudioManager.PlaySfx(Sfx.Blocked, 1f, 0.7f); // (a deep "dong")
            if (mine) Haptics.Light();
            // (a block: no shake, the shield's own ring and sound show it)
            if (!broke) return;
            fx.Burst(at, new Color(0.8f, 0.7f, 0.55f), new Color(1.6f, 1.4f, 1.1f), 26, 6f); // (the shield in pieces)
            AudioManager.PlaySfx(Sfx.Impact, 1f, 0.6f);
            AudioManager.PlaySfx(Sfx.Clang, 0.8f, 0.7f);
            skillBar?.Shout(Loc.T("duel.guardBroke"), mine ? UI.UiKit.Style.danger : UI.UiKit.Style.info);
            var skin = defender.GetComponentInChildren<SkinnedHero>();
            if (skin != null) skin.Stagger(attacker != null ? attacker.position : defender.position - defender.forward, 0.8f);
            StatusFx.On(CombatFeel.RootOf(defender)).Stun(Def.breakDaze);
            if (mine) { Haptics.Medium(); strikeCooldown = Mathf.Max(strikeCooldown, Def.breakDaze); }
            else hunt.StunFoe(Def.breakDaze);
            if (hold != null) hold.Guarding = false;
        }

        /// <summary>
        /// A perfect guard (<paramref name="mine"/>: foi's): the pictures slow 150 ms, a gold-white ring of sparks, the
        /// special ring of metal, "PERFECT!"; the striker staggers half a second; the guard's next blow 30% harder and
        /// half a part of the bar (the shield war week: a whole part).
        /// </summary>
        private void PerfectShown(bool mine)
        {
            var defender = mine ? robot.Visual : hunt.ChallengeFoeModel;
            var attacker = mine ? hunt.ChallengeFoeModel : robot.Visual;
            float now = Time.time;
            VisualTime.SlowAll(0.3f, 0.15f);
            if (defender != null)
            {
                var toward = attacker != null ? attacker.position - defender.position : defender.forward;
                var at = defender.position + Vector3.up * 0.9f * Mathf.Max(0.5f, defender.lossyScale.y) + toward.normalized * 0.4f;
                GuardFx.Ring(at, PerfectGold, 0.9f * Mathf.Max(0.6f, defender.lossyScale.y));
                fx.Burst(at, new Color(1f, 0.95f, 0.75f), new Color(3.2f, 3f, 2f), 34, 7f);
                var hold = defender.GetComponentInChildren<ShieldHold>();
                if (hold != null) hold.Raise();
                CombatFeel.Blocked(attacker, defender);
            }
            AudioManager.PlaySfx(Sfx.Clang, 1f, 1.55f);
            AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.8f);
            skillBar?.Shout(Loc.T("duel.perfect"), mine ? PerfectGold : UI.UiKit.Style.danger);
            FaceFor(mine ? robot.Visual : hunt.ChallengeFoeModel, FaceMoment.PerfectGuard); // (a sly grin)
            float meter = duelRule == WeeklyMode.Rule.ShieldWar ? Def.shieldWarPerfectMeter : Def.perfectMeter;
            if (mine)
            {
                Haptics.Medium();
                rivalGuard.Stagger(now);
                if (!LiveActive) hunt.StunFoe(Def.staggerSeconds);
                myGuard.NextBlowBonus = Def.perfectNextBlow;
                if (!LiveActive) myMeter = Mathf.Min(3f, myMeter + meter);
                mcPerfects++;
                NoteDuel(DuelGoal.Perfect);
                if (attacker != null) { var sk = attacker.GetComponentInChildren<SkinnedHero>(); if (sk != null) sk.Stagger(defender != null ? defender.position : robot.transform.position, 0.7f); }
            }
            else
            {
                myGuard.Stagger(now);
                strikeCooldown = Mathf.Max(strikeCooldown, Def.staggerSeconds);
                rivalNextBlow = 1f + Def.perfectNextBlow;
                rivalMeter = Mathf.Min(3f, rivalMeter + meter);
                myCombo = 0; myComboMove = 0;
                if (heldWeapon != null) heldWeapon.Recoil();
                var sk = robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null;
                if (sk != null && hunt.ChallengeFoeAt is Vector3 fa) sk.Stagger(fa, 0.7f);
            }
            duelStirAt = Time.time;
        }

        /// <summary>A blow dodged: a puff of dust; an ultimate or a power hit gone to nothing: "DODGED!".</summary>
        private void DodgedShown(bool mine, BlowKind kind)
        {
            var who = mine ? robot.Visual : hunt.ChallengeFoeModel;
            if (who != null) fx.Burst(who.position + Vector3.up * 0.8f, new Color(0.8f, 0.9f, 1f), new Color(1.6f, 1.9f, 2.4f), 10, 3f);
            AudioManager.PlaySfx(Sfx.Hop, 0.5f, 1.8f);
            if (kind == BlowKind.Ulti || kind == BlowKind.PowerHit || kind == BlowKind.Knockdown)
            {
                if (dodgedShoutAt > Time.time) return;
                dodgedShoutAt = Time.time + 1.5f;
                skillBar?.Shout(Loc.T("duel.dodged"), mine ? UI.UiKit.Style.cyan : UI.UiKit.Style.danger);
                if (mine) { mcUltiDodges += kind == BlowKind.Ulti ? 1 : 0; if (kind == BlowKind.Ulti) NoteDuel(DuelGoal.DodgeUlti); }
                if (!mine && (kind == BlowKind.PowerHit || kind == BlowKind.Knockdown)) strikeCooldown = Mathf.Max(strikeCooldown, 0.4f + Def.whiffRecovery); // (a big blow into the air: slower back)
            }
        }

        private float dodgedShoutAt;

        // ---------- A live match: the server's word ----------

        /// <summary>The server judged a blow (<paramref name="mineDefended"/>: on foi): 1 blocked, 2 broke, 3 perfect, 4 dodged;
        /// <paramref name="durability"/> the defender's shield left (0..100).</summary>
        private void LiveGuard(bool mineDefended, int outcome, int durability)
        {
            switch (outcome)
            {
                case 1: BlockShown(mineDefended, false); break;
                case 2: BlockShown(mineDefended, true); break;
                case 3: PerfectShown(mineDefended); break;
                case 4: DodgedShown(mineDefended, BlowKind.Plain); break;
            }
            liveDurability[mineDefended ? 0 : 1] = Mathf.Clamp(durability, 0, 100) / 100f;
        }

        /// <summary>The live rival raised (or lowered) its shield: shown on it.</summary>
        private void LiveRivalGuard(bool on)
        {
            if (on) rivalGuard.Press(Time.time); else rivalGuard.Release(Time.time);
        }

        private readonly float[] liveDurability = { 1f, 1f };

        /// <summary>The ultimate under way already counted as landed (once per ultimate).</summary>
        private bool ultiCountedMe, ultiCountedFoe;

        /// <summary>A blow met a defence: counted for the defender (blocked, perfect, broken) and the striker (an ultimate landed).</summary>
        private static void CountGuard(MatchStats defender, MatchStats striker, DefenseOutcome o, BlowKind kind, ref bool ultiCounted)
        {
            if (o == DefenseOutcome.Blocked || o == DefenseOutcome.Broke) defender.blocked++;
            if (o == DefenseOutcome.Broke) defender.shieldBreaks++;
            if (o == DefenseOutcome.Perfect) defender.perfects++;
            if (o != DefenseOutcome.Hit) striker.blows++;
            if (kind == BlowKind.Ulti && o != DefenseOutcome.Dodged && !ultiCounted) { ultiCounted = true; defender.ultisTaken++; striker.ultiHits++; }
        }

        // ---------- The floors' defence ----------

        private bool floorGuardWired, floorGuardWas;

        /// <summary>Each frame on a floor: the SHIELD shown while a fight is on, the shield filling, the block's pose; a level's
        /// start gives a whole shield.</summary>
        private void UpdateFloorGuard()
        {
            if (ui == null) return;
            if (!floorGuardWired)
            {
                floorGuardWired = true;
                ui.FloorShieldDown += () => { if (FloorGuardOn && !myGuard.Press(Time.time)) AudioManager.PlaySfx(Sfx.Blocked, 0.4f, 1.8f); };
                ui.FloorShieldUp += () => { if (myGuard.Holding && !challengeRun) myGuard.Release(Time.time); };
            }
            bool on = FloorGuardOn;
            float now = Time.time;
            if (on && !floorGuardWas) myGuard.Reset(Def.maxDurability);
            floorGuardWas = on;
            if (!on)
            {
                if (!challengeRun) { if (myGuard.Holding) myGuard.Release(now); ui.SetFloorShield(false, 1f, false, 0f); ShowGuarding(robot != null ? robot.Visual : null, false); }
                return;
            }
            myGuard.Tick(now, Time.deltaTime, RegenOf(MyShieldKind));
            bool key = DuelTouchReader.ShieldKeyHeld;
            if (key && !keyGuard) myGuard.Press(now); else if (!key && keyGuard && !ui.FloorShieldHeld) myGuard.Release(now);
            keyGuard = key;
            if (myGuard.Holding && !key && !ui.FloorShieldHeld) myGuard.Release(now);
            ui.SetFloorShield(true, myGuard.Share, myGuard.Broken(now), myGuard.WaitShare(now));
            ShowGuarding(robot.Visual, myGuard.Holding);
        }

        /// <summary>
        /// A robot's blow on foi on a floor meets foi's shield: the share of it that gets through (0: a perfect guard — the
        /// robot staggers — or a dodge); a blocked blow is a graze (quiet), a broken shield dazes foi a moment.
        /// </summary>
        private float FloorGuard(out bool grazed)
        {
            grazed = false;
            float now = Time.time;
            var o = myGuard.Judge(now, BlowKind.Plain, Def.blowCost[2] * (hunt.BlowPierces ? 3f : 1f), out float through); // (the piercing form eats the shield three times as fast)
            switch (o)
            {
                case DefenseOutcome.Perfect:
                {
                    hunt.BlowBlocked = true;
                    hunt.StaggerAttacker(hunt.BlowFrom, Def.staggerSeconds * 2f);
                    myGuard.NextBlowBonus = Def.perfectNextBlow;
                    var at = robot.transform.position + Vector3.up * 1f + (hunt.BlowFrom - robot.transform.position).normalized * 0.4f;
                    GuardFx.Ring(at, PerfectGold, 0.9f);
                    fx.Burst(at, new Color(1f, 0.95f, 0.75f), new Color(3.2f, 3f, 2f), 28, 6f);
                    AudioManager.PlaySfx(Sfx.Clang, 1f, 1.55f);
                    Haptics.Medium();
                    FloatAt(robot.transform.position + Vector3.up * 1.8f, Loc.T("duel.perfect"), PerfectGold);
                    return 0f;
                }
                case DefenseOutcome.Blocked:
                case DefenseOutcome.Broke:
                {
                    hunt.BlowBlocked = true;
                    var toward = hunt.BlowFrom - robot.transform.position;
                    GuardFx.Hex(robot.transform.position + Vector3.up * 1f + toward.normalized * 0.45f, toward, o == DefenseOutcome.Broke || myGuard.Low ? new Color(1f, 0.45f, 0.35f) : GuardBlue, 0.55f);
                    AudioManager.PlaySfx(Sfx.Blocked, 1f, 0.7f);
                    Haptics.Light();
                    if (o == DefenseOutcome.Broke)
                    {
                        FloatAt(robot.transform.position + Vector3.up * 1.8f, Loc.T("duel.guardBroke"), UI.UiKit.Style.danger);
                        strikeCooldown = Mathf.Max(strikeCooldown, Def.breakDaze);
                    }
                    grazed = true;
                    return through;
                }
                case DefenseOutcome.Dodged:
                    return 0f;
            }
            return 1f;
        }

        /// <summary>The last blow (<paramref name="mine"/>: foi's on the rival) landed: not blocked, guarded or dodged.</summary>
        private bool Landed(bool mine) => (mine ? lastGuardFoe : lastGuardMe) == DefenseOutcome.Hit;

        /// <summary>An ultimate begins (<paramref name="mine"/>: foi's): nobody has dodged it yet; the AI rival may read foi's.</summary>
        private void UltiBegins(bool mine)
        {
            stats.Of(mine).ultis++;
            FaceFor(mine ? robot.Visual : hunt.ChallengeFoeModel, FaceMoment.UltiStart); // (angry, teeth clenched)
            if (mine) { ultiEvadedFoe = false; ultiCountedFoe = false; BotReadsBigMove(true); }
            else { ultiEvadedMe = false; ultiCountedMe = false; }
        }
    }
}
