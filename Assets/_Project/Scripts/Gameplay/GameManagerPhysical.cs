using SquashBot.Audio;
using SquashBot.Combat;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The body's attacks (the choreography design's 4.1, 4.3 .. 4.7 and 9): the crouch (high blows go over it), the leg
    /// sweep (down 0.9 s and up again, untouchable meanwhile), the uppercut (up in the air a second, hit there at most three
    /// times for less each time), the flying headbutt (from a jump), the shoulder charge (it breaks the other's combo). The
    /// numbers are the rules table's (phys*, down/air); a live match sends them to the server, which judges them (protocol
    /// 9); against the computer they are judged here, the same way. The computer rival uses them by its personality and
    /// reads the player's high blows (a crouch) and sweeps (a jump), more often the higher its rank.
    /// </summary>
    public partial class GameManager
    {
        private bool myCrouch, rivalCrouchShown;
        private float rivalCrouchUntil, rivalHopUntil;
        private float myDownUntil, rivalDownUntil, myLaunchUntil, rivalLaunchUntil;
        private int myAirHits, rivalAirHits;
        private readonly float[] myPhysReady = new float[4], rivalPhysReady = new float[4];
        /// <summary>The guided duel's sweep step done (a sweep landed).</summary>
        private bool tutSwept;

        /// <summary>A body attack ate this frame's touch (the space bar's strike not fired as well).</summary>
        private bool physAte;

        private static DuelBalanceTable PhysTab => DuelBalanceTable.Current;

        /// <summary>Down after a sweep, or up in the air after an uppercut (and getting up): nothing to do but wait.</summary>
        private bool MeOutOfIt => Time.time < myDownUntil || Time.time < myLaunchUntil;
        private bool RivalOutOfIt => Time.time < rivalDownUntil || Time.time < rivalLaunchUntil;

        /// <summary>In the air: after a jump, or launched (low blows go under it).</summary>
        private bool MeAirborne => Time.time < myJumpUntil || Time.time < myLaunchUntil;
        private bool RivalAirborne => Time.time < rivalHopUntil || Time.time < rivalLaunchUntil;
        private bool RivalCrouching => Time.time < rivalCrouchUntil;

        private bool PhysDuel => challengeRun && chPhase == ChPhase.Duel && !DuelCinematic;

        // ---------- The player's touches ----------

        /// <summary>The frame's touches read for the body's attacks: what they use is taken out of the command.</summary>
        private InputCommand PhysicalInput(InputCommand cmd)
        {
            physAte = false;
            if (!PhysDuel) { SetMyCrouch(false); return cmd; }
            if (MeOutOfIt) { SetMyCrouch(false); return default; } // (down or in the air: nothing)
            SetMyCrouch(cmd.crouch && !moveBusy);
            if (cmd.upper || (myCrouch && cmd.flick.HasValue)) { TryPhys(true, PhysKind.Uppercut); cmd.flick = null; cmd.upper = false; physAte = true; }
            if (myCrouch && (cmd.tap.HasValue || InputReader.AttackKey())) { TryPhys(true, PhysKind.Sweep); cmd.tap = null; physAte = true; }
            if (cmd.charge != 0)
            {
                if (TowardRival(cmd.charge)) TryPhys(true, MeAirborne ? PhysKind.Headbutt : PhysKind.Shoulder);
                cmd.charge = 0; physAte = true;
            }
            // a live match has no lane leaps: a swipe up is a jump on the spot (the server is told: low blows go under it)
            if (LiveActive && cmd.flick.HasValue && !myCrouch && Time.time >= myJumpReadyAt) { Jump(); LiveNet.Jump(); cmd.flick = null; }
            if (myCrouch) { cmd.stick = null; cmd.move = null; } // (no walking crouched)
            return cmd;
        }

        private void SetMyCrouch(bool on)
        {
            if (on != myCrouch && LiveActive) LiveNet.Crouch(on);
            myCrouch = on;
            var skin = robot != null && robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null;
            if (skin != null) skin.CrouchHeld = on;
        }

        /// <summary>A sideways flick on the screen (-1 left, 1 right) toward the rival?</summary>
        private bool TowardRival(int side)
        {
            var cam = cameraRig != null ? cameraRig.Cam : null;
            if (cam == null || !hunt.ChallengeFoeAt.HasValue) return false;
            float me = cam.WorldToScreenPoint(robot.transform.position).x, them = cam.WorldToScreenPoint(hunt.ChallengeFoeAt.Value).x;
            return Mathf.Sign(them - me) == side;
        }

        // ---------- The attack ----------

        /// <summary>A body attack by the player (<paramref name="mine"/>) or the computer rival, if it may: its cooldown,
        /// its reach, not down or in the air, the flying headbutt only from the air.</summary>
        private bool TryPhys(bool mine, PhysKind k)
        {
            if (!PhysDuel || !hunt.ChallengeFoeTile.HasValue) return false;
            int i = (int)k;
            var ready = mine ? myPhysReady : rivalPhysReady;
            if (Time.time < ready[i] || (mine ? MeOutOfIt : RivalOutOfIt) || moveBusy) return false;
            if (mine && (!GuardCanStrike || strikeCooldown > 0.05f)) return false;
            int gap = HuntSystem.Chebyshev(robot.Position, hunt.ChallengeFoeTile.Value);
            if (gap > Mathf.RoundToInt(PhysTab.physReach[i])) return false;
            if (k == PhysKind.Headbutt && mine && !MeAirborne) return false;
            ready[i] = Time.time + PhysTab.physCooldown[i];
            if (mine && k != PhysKind.Sweep) SetMyCrouch(false); // (the uppercut springs out of it)
            if (mine && k == PhysKind.Sweep && !LiveActive) RivalReads(k); // (the rival may jump it)
            cameraRig.StartCoroutine(PhysAttack(mine, k));
            return true;
        }

        private static BodyStrike StrikeOf(PhysKind k) => k == PhysKind.Sweep ? BodyStrike.Sweep : k == PhysKind.Uppercut ? BodyStrike.Uppercut : k == PhysKind.Headbutt ? BodyStrike.FlyingHeadbutt : BodyStrike.ShoulderCharge;

        /// <summary>Its wind-up, strike and recovery (the design's frames at 60 per second).</summary>
        private static (float wind, float hit, float back) PhysTiming(PhysKind k) => k switch
        {
            PhysKind.Sweep => (0.083f, 0.067f, 0.13f),
            PhysKind.Uppercut => (0.1f, 0.067f, 0.2f),
            PhysKind.Headbutt => (0.067f, 0.083f, 0.25f),
            _ => (0.05f, 0.033f, 0.2f),
        };

        private System.Collections.IEnumerator PhysAttack(bool mine, PhysKind k)
        {
            var me = mine ? robot.Visual : hunt.ChallengeFoeModel;
            var them = mine ? hunt.ChallengeFoeModel : robot.Visual;
            var skin = me != null ? me.GetComponentInChildren<SkinnedHero>() : null;
            var (wind, hit, back) = PhysTiming(k);
            if (!mine && k == PhysKind.Headbutt && skin != null) { skin.Hop(1.1f, 0.5f); yield return new WaitForSeconds(0.18f); } // (the bot jumps first)
            if (skin != null && them != null) skin.StrikeWithBody(StrikeOf(k), wind, hit, back, them.position);
            if (me != null && them != null) VisualRoot.On(CombatFeel.RootOf(me)).Lunge(them.position - me.position, 1.5f, wind + hit * 0.5f, hit * 0.5f + 0.05f, back);
            AudioManager.PlaySfx(Sfx.Hop, 0.8f, k == PhysKind.Headbutt ? 0.8f : 1.2f);
            skillBar?.Shout(Loc.T("duel.phys." + (int)k), mine ? new Color(1f, 0.75f, 0.35f) : new Color(1f, 0.45f, 0.4f));
            yield return new WaitForSeconds(wind + hit * 0.5f); // (the contact: the rules' moment and the picture's, together)
            if (!challengeRun || chPhase != ChPhase.Duel) yield break;
            if (LiveActive) { if (mine) LiveNet.Phys((int)k); yield break; } // (the server judges it; its word animates it)
            PhysResolve(mine, k);
        }

        /// <summary>Against the computer: the attack judged at its contact (the server's rules, here).</summary>
        private void PhysResolve(bool mine, PhysKind k)
        {
            int i = (int)k;
            var height = (HitHeight)PhysTab.physHeight[i];
            bool targetDown = mine ? Time.time < rivalDownUntil : Time.time < myDownUntil;
            bool crouched = mine ? RivalCrouching : myCrouch;
            bool aloft = mine ? RivalAirborne : MeAirborne;
            if (targetDown || (height == HitHeight.High && crouched) || (height == HitHeight.Low && aloft))
            {
                PhysShown(mine, k, "miss", crouched && height == HitHeight.High ? "duel.under" : aloft ? "duel.over" : null);
                return;
            }
            float cost = PhysTab.physGuardCost[i];
            if (mine) HitFoe(PhysTab.physTimes[i], BlowKind.Special, cost); else HurtMe(PhysTab.physTimes[i], BlowKind.Special, cost);
            bool landed = Landed(mine);
            if (!landed)
            {
                if (k == PhysKind.Shoulder) { if (mine) strikeCooldown = Mathf.Max(strikeCooldown, PhysTab.physOpen[i]); else hunt.StunFoe(PhysTab.physOpen[i]); } // (blocked: the charger stands open)
                PhysShown(mine, k, "block", null);
                return;
            }
            PhysShown(mine, k, "hit", null);
        }

        /// <summary>
        /// An attack's outcome shown and its effect applied (here: <paramref name="r"/> the judgement; in a live match the
        /// server's): the sweep's fall, the uppercut's launch, the headbutt's stun, the shoulder's broken combo.
        /// </summary>
        private void PhysShown(bool mine, PhysKind k, string r, string missWord)
        {
            var me = mine ? robot.Visual : hunt.ChallengeFoeModel;
            var them = mine ? hunt.ChallengeFoeModel : robot.Visual;
            var theirSkin = them != null ? them.GetComponentInChildren<SkinnedHero>() : null;
            var from = me != null ? me.position : robot.transform.position;
            if (r != "hit")
            {
                if (missWord != null && them != null) FloatAt(them.position + Vector3.up * 2f, Loc.T(missWord), new Color(0.75f, 0.9f, 1f));
                return;
            }
            var tab = PhysTab;
            float now = Time.time;
            if (mine && k == PhysKind.Sweep) tutSwept = true;
            if (mine) TrainingNote(k == PhysKind.Sweep ? TrainingEvent.Sweep : k == PhysKind.Uppercut ? TrainingEvent.Uppercut : k == PhysKind.Headbutt ? TrainingEvent.Headbutt : TrainingEvent.ShoulderCharge);
            switch (k)
            {
                case PhysKind.Sweep:
                {
                    float lie = tab.downSeconds + tab.getUpSeconds;
                    if (mine) { rivalDownUntil = now + lie; if (!LiveActive) { hunt.FoeInvulnUntil = Mathf.Max(hunt.FoeInvulnUntil, now + lie); hunt.StunFoe(lie); } }
                    else { myDownUntil = now + lie; myInvulnUntil = Mathf.Max(myInvulnUntil, now + lie); }
                    var across = them != null ? Vector3.Cross(Vector3.up, them.position - from).normalized : Vector3.right;
                    if (theirSkin != null) theirSkin.SweptOff(from, across, tab.downSeconds);
                    FaceFor(them, FaceMoment.Surprise);
                    cameraRig.Shake(0.5f);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 0.8f);
                    break;
                }
                case PhysKind.Uppercut:
                    PhysLaunch(mine);
                    cameraRig.Shake(0.6f);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 0.7f);
                    break;
                case PhysKind.Headbutt:
                {
                    float stun = tab.physStun[(int)k];
                    if (mine) { if (!LiveActive) hunt.StunFoe(stun); } else strikeCooldown = Mathf.Max(strikeCooldown, stun);
                    if (theirSkin != null) theirSkin.DriveBack(from);
                    FaceFor(them, FaceMoment.KnockedOut); // (the eyes rolling)
                    FaceFor(me, FaceMoment.SlyGrin); // (it hurt, but it was worth it)
                    PowerSlow(0.3f, 0.15f); // (the knock: a breath of slow motion)
                    cameraRig.Shake(0.7f);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, 0.55f);
                    break;
                }
                default: // the shoulder: its combo broken
                    if (mine) rivalCombo = 0; else myCombo = 0;
                    if (theirSkin != null) theirSkin.Rock(from, 1f);
                    cameraRig.Shake(0.35f);
                    AudioManager.PlaySfx(Sfx.Impact, 0.9f, 1f);
                    break;
            }
        }

        /// <summary>The other thrown up into the air (the uppercut, the knockdown): a second up there, hit at most three times.</summary>
        private void PhysLaunch(bool mine)
        {
            var tab = PhysTab;
            float now = Time.time;
            var them = mine ? hunt.ChallengeFoeModel : robot.Visual;
            var me = mine ? robot.Visual : hunt.ChallengeFoeModel;
            if (mine) { rivalLaunchUntil = now + tab.airSeconds + tab.getUpSeconds; rivalAirHits = 0; if (!LiveActive) hunt.StunFoe(tab.airSeconds + tab.getUpSeconds); }
            else { myLaunchUntil = now + tab.airSeconds + tab.getUpSeconds; myAirHits = 0; }
            var skin = them != null ? them.GetComponentInChildren<SkinnedHero>() : null;
            if (skin != null) skin.LaunchUp(me != null ? me.position : robot.transform.position, tab.airSeconds);
            FaceFor(them, FaceMoment.Launched);
            if (!mine && !LiveActive) cameraRig.StartCoroutine(RivalJuggle()); // (the computer follows it up in the air)
        }

        /// <summary>
        /// A blow on the rival as it lands (the player's, here): down, no damage; launched, a juggle hit (its share of the
        /// blow less each time, at most three); crouched under a high blow, a miss. Returns the damage to deal (0: none).
        /// </summary>
        private int PhysOnMyBlow(int damage, HitHeight height)
        {
            if (!PhysDuel || LiveActive) return damage;
            float now = Time.time;
            if (now < rivalDownUntil && now >= rivalLaunchUntil - PhysTab.getUpSeconds) return 0; // (lying or getting up: untouchable)
            if (now < rivalLaunchUntil - PhysTab.getUpSeconds)
            {
                if (rivalAirHits >= PhysTab.airHitsMax) return 0;
                float times = PhysTab.airHitTimes[Mathf.Min(rivalAirHits, PhysTab.airHitTimes.Length - 1)];
                rivalAirHits++;
                var skin = hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.GetComponentInChildren<SkinnedHero>() : null;
                if (skin != null) skin.AirHit(robot.transform.position);
                TrainingNote(TrainingEvent.AirHit);
                if (rivalAirHits >= PhysTab.airHitsMax) hunt.FoeInvulnUntil = Mathf.Max(hunt.FoeInvulnUntil, now + 0.6f); // (the last: down it comes)
                return Mathf.Max(1, Mathf.RoundToInt(damage * times));
            }
            if (height == HitHeight.High && RivalCrouching)
            {
                FloatAt((hunt.ChallengeFoeAt ?? robot.transform.position) + Vector3.up * 2f, Loc.T("duel.under"), new Color(0.75f, 0.9f, 1f));
                return 0;
            }
            return damage;
        }

        /// <summary>The computer rival's blow on the player as it lands: under it in a crouch (a high one), or nothing while down.</summary>
        private bool PhysDodgesRivalBlow()
        {
            if (!PhysDuel || LiveActive) return false;
            if (Time.time < myDownUntil) return true;
            var held = hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.GetComponentInChildren<HeldWeapon>() : null;
            var blow = held != null ? held.Current : null;
            if (myCrouch && blow != null && blow.height == HitHeight.High)
            {
                TrainingNote(TrainingEvent.CrouchUnder);
                FloatAt(robot.transform.position + Vector3.up * 2f, Loc.T("duel.under"), new Color(0.75f, 0.9f, 1f));
                return true;
            }
            return false;
        }

        /// <summary>The computer follows its uppercut up with blows in the air (each less, at most three).</summary>
        private System.Collections.IEnumerator RivalJuggle()
        {
            var held = hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.GetComponentInChildren<HeldWeapon>() : null;
            var skin = robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null;
            for (int i = 0; i < PhysTab.airHitsMax && Time.time < myLaunchUntil - PhysTab.getUpSeconds && challengeRun; i++)
            {
                yield return new WaitForSeconds(0.28f);
                if (held != null) held.Swing();
                yield return new WaitForSeconds(0.08f);
                float times = PhysTab.airHitTimes[Mathf.Min(i, PhysTab.airHitTimes.Length - 1)];
                HurtMe(times, BlowKind.Air);
                if (skin != null) skin.AirHit(hunt.ChallengeFoeAt ?? robot.transform.position);
            }
        }

        // ---------- The computer rival ----------

        /// <summary>The rival may use a body attack (its personality's leanings), when the player is within reach.</summary>
        private bool RivalPhysical(float dist)
        {
            var t = botTuning;
            if (t == null || dist > 3.4f || RivalOutOfIt) return false;
            float sweep = 0.06f, upper = 0.05f, head = 0.04f, shoulder = 0.05f;
            switch (t.persona)
            {
                case Persona.Aggressive: sweep = 0.2f; head = 0.16f; break;
                case Persona.Combo: upper = 0.25f; break;
                case Persona.Runner: shoulder = 0.25f; break;
                case Persona.Defensive: sweep = 0.1f; break;
            }
            float roll = Random.value;
            if (MeOutOfIt) return false; // (never on a fighter that is down)
            if ((roll -= sweep) < 0f) return TryPhys(false, PhysKind.Sweep);
            if ((roll -= upper) < 0f) return TryPhys(false, PhysKind.Uppercut);
            if ((roll -= head) < 0f) return TryPhys(false, PhysKind.Headbutt);
            if ((roll -= shoulder) < 0f) return TryPhys(false, PhysKind.Shoulder);
            return false;
        }

        /// <summary>
        /// The rival reads the player's blow as it starts (more often the higher its rank, the defensive one most): a crouch
        /// under a high one, a jump over a sweep.
        /// </summary>
        private void RivalReads(PhysKind? sweep, HitHeight height = HitHeight.Mid)
        {
            var t = botTuning;
            if (t == null || LiveActive || LabOn || RivalOutOfIt) return;
            if (Random.value >= t.read) return;
            if (sweep.HasValue)
            {
                rivalHopUntil = Time.time + PhysTab.jumpAirSeconds;
                var skin = hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.GetComponentInChildren<SkinnedHero>() : null;
                if (skin != null) skin.Hop(1.1f, PhysTab.jumpAirSeconds);
            }
            else if (height == HitHeight.High) rivalCrouchUntil = Time.time + 0.55f;
        }

        /// <summary>Each frame of the duel: the rival's crouch shown, a launch's landing.</summary>
        private void UpdatePhysical()
        {
            if (!challengeRun) return;
            bool crouch = RivalCrouching;
            if (crouch != rivalCrouchShown)
            {
                rivalCrouchShown = crouch;
                var skin = hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.GetComponentInChildren<SkinnedHero>() : null;
                if (skin != null) skin.CrouchHeld = crouch;
            }
            else if (crouch && hunt.ChallengeFoeModel != null)
            {
                var skin = hunt.ChallengeFoeModel.GetComponentInChildren<SkinnedHero>();
                if (skin != null) skin.CrouchHeld = true; // (held each frame)
            }
            if (myCrouch && robot.Visual != null) { var mySkin = robot.Visual.GetComponentInChildren<SkinnedHero>(); if (mySkin != null) mySkin.CrouchHeld = true; }
        }

        /// <summary>A new duel (or round): nobody down, in the air or crouched; the cooldowns full.</summary>
        private void ResetPhysical()
        {
            myCrouch = false; rivalCrouchShown = false;
            rivalCrouchUntil = rivalHopUntil = myDownUntil = rivalDownUntil = myLaunchUntil = rivalLaunchUntil = 0f;
            myAirHits = rivalAirHits = 0;
            for (int i = 0; i < 4; i++) { myPhysReady[i] = 0f; rivalPhysReady[i] = 0f; }
        }

        // ---------- A live match: the server's word ----------

        /// <summary>The server judged a body attack (protocol 9): <paramref name="mine"/> the player's, the outcome shown.</summary>
        private void PhysFromServer(bool mine, int k, string r)
        {
            if (k < 0 || k > 3) return;
            if (!mine) // (the rival's attack seen as it lands: its picture here, then the outcome)
            {
                var skin = hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.GetComponentInChildren<SkinnedHero>() : null;
                var (wind, hit, back) = PhysTiming((PhysKind)k);
                if (skin != null) skin.StrikeWithBody(StrikeOf((PhysKind)k), 0.02f, hit, back, robot.transform.position);
            }
            PhysShown(mine, (PhysKind)k, r, r == "miss" ? "duel.under" : null);
        }

        /// <summary>The live rival crouched or stood (protocol 9).</summary>
        private void RivalCrouchFromServer(bool on) => rivalCrouchUntil = on ? float.MaxValue : 0f;

        /// <summary>The live rival jumped (protocol 9).</summary>
        private void RivalJumpFromServer()
        {
            rivalHopUntil = Time.time + PhysTab.jumpAirSeconds;
            var skin = hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.GetComponentInChildren<SkinnedHero>() : null;
            if (skin != null) skin.Hop(1.1f, PhysTab.jumpAirSeconds);
        }
    }
}
