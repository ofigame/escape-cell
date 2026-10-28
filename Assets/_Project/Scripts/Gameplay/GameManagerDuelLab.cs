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
    /// The duel lab (test builds, from the settings): a duel against a rival that never fights back and never falls (both
    /// kept at full health), with a panel to try the duel's feel: a strike, each weapon family, the rival's blow, the
    /// reactions by strength, the freeze and shake scales, the pictures' slow motion (1, 0.25, 0.1), the upper-body and
    /// full-body layers; and live readings (the match clock and the game's time scale stay untouched by the freeze).
    /// </summary>
    public partial class GameManager
    {
        private DuelLabPanel duelLab;
        private float labSlow = 1f;
        private float labWalkUntil;
        private int labWalkDir = 1;
        /// <summary>The lab's rival defends (blocks, guards perfectly, dodges big moves) to try the defence against.</summary>
        private bool LabDefends;
        /// <summary>The lab's rival attacks (its plain blows, telegraphed) to try blocking, perfect guards and dodges.</summary>
        private bool LabAttacks;

        /// <summary>The lab is open (a lab duel under way).</summary>
        private bool LabOn => duelLab != null;

        /// <summary>A screen point over the lab's panel.</summary>
        private bool DuelLabOver(Vector2 p) => duelLab != null && duelLab.IsOver(p);

        /// <summary>The settings' DUEL LAB: a lab duel on the temple of blossoms, the lane duel, the panel.</summary>
        private void StartDuelLab()
        {
            if (challengeRun) return;
            ShowMenu();
            HitLog.Clear();
            ShowHitLog(true); // (the developer's blow log: the lab's)
            if (chUi == null) InitChallenge();
            chDuelMinutes = 3; chOnline = false; chLive = false; chMode = 1;
            rival = Challenge.MakeBot(4242, -1);
            BuildDuelBoard(4);
            duelLab = DuelLabPanel.Create();
            BuildLabPanel();
        }

        private static readonly Color LabFamily = new Color(0.55f, 0.35f, 0.8f), LabReact = new Color(0.8f, 0.45f, 0.2f), LabTune = new Color(0.3f, 0.5f, 0.6f);

        private void BuildLabPanel()
        {
            var lab = duelLab;
            lab.Add("VUR", LabStrike, new Color(0.2f, 0.65f, 0.3f));
            lab.Add("4 SERİ", () => cameraRig.StartCoroutine(LabSeries(4)), new Color(0.2f, 0.65f, 0.3f));
            lab.Add("RAKİP VURSUN", LabRivalBlow, new Color(0.75f, 0.3f, 0.3f));
            lab.Add("KUKLA SAVUNSUN", () => LabDefends = !LabDefends, new Color(0.25f, 0.5f, 0.9f));
            lab.Add("KUKLA SALDIRSIN", () => LabAttacks = !LabAttacks, new Color(0.85f, 0.35f, 0.25f));
            lab.Add("KILIÇ", () => LabWeapon(WeaponKind.Sword), LabFamily);
            lab.Add("MIZRAK", () => LabWeapon(WeaponKind.Spear), LabFamily);
            lab.Add("BALTA", () => LabWeapon(WeaponKind.Axe), LabFamily);
            lab.Add("AĞIR SİLAH", () => LabWeapon(WeaponKind.Hammer), LabFamily);
            lab.Add("T: HAFİF", () => LabReaction(HitStrength.Light), LabReact);
            lab.Add("T: ORTA", () => LabReaction(HitStrength.Medium), LabReact);
            lab.Add("T: AĞIR", () => LabReaction(HitStrength.Heavy), LabReact);
            lab.Add("T: FIRLAT", () => LabReaction(HitStrength.Launch), LabReact);
            lab.Add("T: SÜPÜR", LabSweep, LabReact);
            lab.Add("SÜPÜRME!", () => { myPhysReady[0] = 0f; LabPhys(PhysKind.Sweep); }, new Color(0.8f, 0.55f, 0.2f));
            lab.Add("APARKAT", () => { myPhysReady[1] = 0f; LabPhys(PhysKind.Uppercut); }, new Color(0.8f, 0.55f, 0.2f));
            lab.Add("KAFA ATMA", () => { myPhysReady[2] = 0f; myJumpUntil = Time.time + 0.6f; LabPhys(PhysKind.Headbutt); }, new Color(0.8f, 0.55f, 0.2f));
            lab.Add("OMUZ", () => { myPhysReady[3] = 0f; LabPhys(PhysKind.Shoulder); }, new Color(0.8f, 0.55f, 0.2f));
            lab.Add("KUKLA ÇÖMEL", () => rivalCrouchUntil = Time.time + 3f, LabTune);
            lab.Add("T: HAVADA VUR", () => cameraRig.StartCoroutine(LabJuggle(1)), LabReact);
            lab.Add("DONMA −", () => CombatFeel.HitstopMultiplier -= 0.5f, LabTune);
            lab.Add("DONMA +", () => CombatFeel.HitstopMultiplier += 0.5f, LabTune);
            lab.Add("SARSINTI", () => SaveData.ScreenShake = !SaveData.ScreenShake, LabTune);
            lab.Add("SARS. −", () => CombatFeel.ShakeMultiplier -= 0.5f, LabTune);
            lab.Add("SARS. +", () => CombatFeel.ShakeMultiplier += 0.5f, LabTune);
            lab.Add("1×", () => LabSlow(1f), LabTune);
            lab.Add("0,25×", () => LabSlow(0.25f), LabTune);
            lab.Add("0,1×", () => LabSlow(0.1f), LabTune);
            lab.Add("ÜST GÖVDE", LabUpperBody);
            lab.Add("TAM GÖVDE", () => { var s = robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null; if (s != null) s.PlayMove("dance1"); });
            // the collision tools (Docs/DUELLO_CARPISMA_VE_TEMAS.md, section 6)
            lab.Add("KUTULAR", LabBoxes, LabCollide);
            lab.Add("KARE ▶", () => cameraRig.StartCoroutine(LabStep()), LabCollide);
            lab.Add("DEVAM", () => { Time.timeScale = 1f; labStepping = false; }, LabCollide);
            lab.Add("GECİKME", () => LabLatencyMs = LabLatencyMs >= 200 ? 0 : LabLatencyMs + 100, LabCollide);
            lab.Add("SAYAÇ SIFIRLA", SkinnedHero.ResetOverlapCount, LabCollide);
            AddCollisionScenarios(lab);
            lab.Add("ÇIK", CloseDuelLab, new Color(0.45f, 0.45f, 0.5f));
        }

        /// <summary>Each frame of the lab: the rival held still and both at full health; the readings.</summary>
        private void UpdateDuelLab()
        {
            if (duelLab == null) return;
            if (!challengeRun) { CloseDuelLab(); return; }
            if (chPhase == ChPhase.Duel)
            {
                if (!LabAttacks) hunt.StunFoe(1f);
                if (health < 1f) { health = 1f; RefreshHealthBar(); }
                if (hunt.ChallengeFoeHealth < 0.999f) hunt.HealFoeQuiet(1f);
                if (labSlow < 0.99f) VisualTime.SlowAll(labSlow, 0.5f);
                if (Time.time < labWalkUntil)
                {
                    // the upper-body test: foi walks back and forth along the lane while its upper body plays the move
                    var stick = new Vector2(labWalkDir, 0f);
                    FloorWalk(stick);
                    if (Mathf.Repeat(labWalkUntil - Time.time, 1.2f) < Time.deltaTime) labWalkDir = -labWalkDir;
                }
            }
            var mine = robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null;
            var root = CombatFeel.RootOf(robot.Visual);
            var vt = root != null ? root.GetComponent<VisualTime>() : null;
            var vr = root != null ? root.GetComponent<VisualRoot>() : null;
            var foeRoot = CombatFeel.RootOf(hunt.ChallengeFoeModel);
            var foeVr = foeRoot != null ? foeRoot.GetComponent<VisualRoot>() : null;
            int secs = Mathf.Max(0, Mathf.CeilToInt(chDuelLeft));
            duelLab.SetInfo(
                "Maç saati " + secs / 60 + ":" + (secs % 60).ToString("00") + "   Time.timeScale " + Time.timeScale.ToString("0.00") + "\n"
                + "Görsel zaman: foi " + (vt != null ? vt.Scale.ToString("0.00") : "-") + (vt != null && vt.Frozen ? " (DONMA)" : "") + "   genel " + VisualTime.GlobalScale.ToString("0.00") + "\n"
                + "Donma ×" + CombatFeel.HitstopMultiplier.ToString("0.0") + "   Sarsıntı " + (SaveData.ScreenShake ? "açık ×" + CombatFeel.ShakeMultiplier.ToString("0.0") : "kapalı") + "\n"
                + "Silah: " + (heldWeapon != null ? heldWeapon.Family.ToString() : "-") + "   itme foi " + (vr != null ? vr.Offset.magnitude.ToString("0.00") : "0") + " / rakip " + (foeVr != null ? foeVr.Offset.magnitude.ToString("0.00") : "0") + "\n"
                + (SkinnedHero.OverlapFrames > 0 ? "<color=#FF5544>İÇ İÇE: " + SkinnedHero.OverlapFrames + " kare</color>" : "İç içe: 0 kare") + "   temas mesafesi " + SkinnedHero.ContactDistance.ToString("0.00") + "   gecikme " + LabLatencyMs + " ms" + (labStepping ? "   (KARE KARE)" : ""));
        }

        private void CloseDuelLab()
        {
            if (duelLab != null) duelLab.Close();
            if (labBoxes != null) { Destroy(labBoxes.gameObject); labBoxes = null; }
            if (labStepping) { Time.timeScale = 1f; labStepping = false; }
            LabLatencyMs = 0;
            duelLab = null;
            labSlow = 1f;
            VisualTime.SlowAll(1f, 0f);
            if (challengeRun) { EndChallenge(); ShowMenu(); }
        }

        /// <summary>One strike at the rival from the duel's gap (foi stepped there first along the lane).</summary>
        private void LabStrike()
        {
            if (chPhase != ChPhase.Duel || !(hunt.ChallengeFoeTile is GridPos foe)) return;
            int dir = robot.Position.y <= foe.y ? -1 : 1;
            var spot = new GridPos(laneX, foe.y + dir * HuntSystem.DuelGap);
            if (robot.Position != spot && grid.InBounds(spot) && grid.IsStandable(spot)) robot.Spawn(grid, spot);
            strikeCooldown = 0f;
            TryStrikeNow(foe, true);
        }

        private System.Collections.IEnumerator LabSeries(int n)
        {
            for (int i = 0; i < n; i++)
            {
                LabStrike();
                yield return new WaitForSeconds(Mathf.Max(0.12f, weapon != null ? Armory.Cooldown(weapon) : 0.3f) + 0.02f);
            }
        }

        /// <summary>The rival's blow on foi, its feel only (its swing, foi's reaction): the rules not touched.</summary>
        private void LabRivalBlow()
        {
            var foeModel = hunt.ChallengeFoeModel;
            if (foeModel == null || robot.Visual == null) return;
            var held = foeModel.GetComponentInChildren<HeldWeapon>();
            if (held != null) held.Swing();
            Reel(true, 1);
        }

        /// <summary>Foi's weapon for the lab: the first of a family (the rules' weapon too, for the lab duel only).</summary>
        private void LabWeapon(WeaponKind kind)
        {
            var w = Armory.All.Find(x => x.kind == kind && !x.chest);
            if (w == null) return;
            weapon = w;
            ShowHeld(w);
        }

        /// <summary>A reaction of a strength on the rival (its feel with the family's blow of that strength).</summary>
        private void LabReaction(HitStrength s)
        {
            var foeModel = hunt.ChallengeFoeModel;
            if (foeModel == null || robot.Visual == null) return;
            var family = CombatLibrary.Family(heldWeapon != null ? heldWeapon.Family : WeaponFamily.Sword);
            if (s == HitStrength.Launch) { cameraRig.StartCoroutine(BigMoment(false, 1.6f, 1)); return; }
            var blow = s == HitStrength.Heavy ? family.powerHit : family.Blow(s == HitStrength.Medium ? 1 : 0);
            if (heldWeapon != null) heldWeapon.Swing(blow);
            var skin = foeModel.GetComponentInChildren<SkinnedHero>();
            if (skin != null) { skin.Stagger(robot.transform.position, s == HitStrength.Heavy ? 0.8f : s == HitStrength.Medium ? 0.5f : 0.25f); skin.Shudder(robot.transform.position, s == HitStrength.Light ? 0.5f : 1f); }
            CombatFeel.Hit(robot.Visual, foeModel, blow);
        }

        /// <summary>The leg sweep's reaction on the rival (its legs out along the sweep, onto its back, up again).</summary>
        private void LabSweep()
        {
            var skin = hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.GetComponentInChildren<SkinnedHero>() : null;
            if (skin == null) return;
            var across = Vector3.Cross(Vector3.up, skin.transform.position - robot.transform.position).normalized;
            skin.SweptOff(robot.transform.position, across);
        }

        /// <summary>The rival thrown up and hit <paramref name="hits"/> times in the air (the juggle's reaction).</summary>
        private System.Collections.IEnumerator LabJuggle(int hits)
        {
            var skin = hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.GetComponentInChildren<SkinnedHero>() : null;
            if (skin == null) yield break;
            skin.LaunchUp(robot.transform.position);
            for (int i = 0; i < hits; i++)
            {
                yield return new WaitForSeconds(0.3f);
                if (heldWeapon != null) heldWeapon.Swing();
                skin.AirHit(robot.transform.position);
            }
        }

        /// <summary>The player's body attack from the duel's gap (foi stepped there first), its cooldown cleared.</summary>
        private void LabPhys(PhysKind k)
        {
            if (chPhase != ChPhase.Duel || !(hunt.ChallengeFoeTile is GridPos foe)) return;
            int dir = robot.Position.y <= foe.y ? -1 : 1;
            var spot = new GridPos(laneX, foe.y + dir * HuntSystem.DuelGap);
            if (robot.Position != spot && grid.InBounds(spot) && grid.IsStandable(spot)) robot.Spawn(grid, spot);
            strikeCooldown = 0f;
            TryPhys(true, k);
        }

        private void LabSlow(float s)
        {
            labSlow = s;
            VisualTime.SlowAll(s, s < 0.99f ? 0.5f : 0f);
        }

        /// <summary>The upper-body layer: the ultimate's cast on foi's upper body while it walks along the lane.</summary>
        private void LabUpperBody()
        {
            var s = robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null;
            if (s == null) return;
            s.PlayUpper("cast", 1f);
            labWalkUntil = Time.time + 2.4f;
            labWalkDir = 1;
        }
    }
}
