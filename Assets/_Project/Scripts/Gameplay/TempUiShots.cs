// TEMP-FILE: Faz 3 acceptance: ten sword blows in a row (each reaction different, the rival's tile unchanged by them),
// the reactions by strength, status effects, the damage states, a knock-out end to end. -sbF3 <dir> ; quits after.
using System;
using System.Collections;
using System.IO;
using System.Text;
using SquashBot.Combat;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    public partial class GameManager
    {
        private IEnumerator UiShots()
        {
            var args = Environment.GetCommandLineArgs();
            int ai = Array.IndexOf(args, "-sbF3");
            if (ai < 0) yield break;
            string dir = args[ai + 1];
            Directory.CreateDirectory(dir);
            yield return new WaitForSeconds(5f);
            if (ui.HeroPicker.IsOpen) typeof(HeroPicker).GetMethod("ChooseHead", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(ui.HeroPicker, new object[] { Cosmetics.Find("head.1") });
            if (lobby != null) { Destroy(lobby.gameObject); lobby = null; }
            var log = new StringBuilder();
            SaveData.LaneDuel = true;
            ShowMenu(); ui.LoginView.Hide(); yield return new WaitForSeconds(1f);
            StartDuelLab();
            float until = Time.time + 20f;
            while (chPhase != ChPhase.Duel && Time.time < until) yield return new WaitForSeconds(0.2f);
            yield return new WaitForSeconds(0.8f);
            LabWeapon(WeaponKind.Sword); yield return new WaitForSeconds(1.6f);
            var foeSkin = hunt.ChallengeFoeModel.GetComponentInChildren<SkinnedHero>();
            // 1. ten sword blows in a row (no 4-in-a-row push: a pause after every third)
            var seq = new StringBuilder();
            int same = 0, moved = 0; string last = "";
            for (int i = 0; i < 10; i++)
            {
                var tileBefore = hunt.ChallengeFoeTile;
                LabStrike();
                yield return new WaitForSeconds(0.12f);
                string r = foeSkin != null ? foeSkin.LastReaction : "-";
                if (r == last) same++;
                last = r;
                seq.Append(r + "  ");
                if (i == 1 || i == 6) ScreenCapture.CaptureScreenshot(dir + "/blow_" + i + ".png");
                yield return new WaitForSeconds(0.25f);
                if (hunt.ChallengeFoeTile != tileBefore) moved++;
                if (i % 3 == 2) yield return new WaitForSeconds(1.6f); // (the combo gap: no rule push)
            }
            log.Append("10 sword blows: " + seq + "\n  same as the one before: " + same + ", rival tile changed: " + moved + "\n");
            // 2. reactions by strength, with pictures at their peak
            foreach (var s in new[] { HitStrength.Medium, HitStrength.Heavy })
            {
                LabReaction(s);
                yield return new WaitForSeconds(0.1f);
                ScreenCapture.CaptureScreenshot(dir + "/react_" + s + ".png");
                yield return new WaitForSeconds(1.2f);
            }
            // 3. statuses
            var root = CombatFeel.RootOf(hunt.ChallengeFoeModel);
            StatusFx.On(root).Stun(2f); StatusFx.On(root).Burn(2f);
            yield return new WaitForSeconds(0.5f);
            ScreenCapture.CaptureScreenshot(dir + "/stun_burn.png"); yield return new WaitForSeconds(2f);
            StatusFx.On(root).Freeze(1.6f); yield return new WaitForSeconds(0.5f);
            ScreenCapture.CaptureScreenshot(dir + "/freeze.png"); yield return new WaitForSeconds(1.5f);
            StatusFx.On(root).SetDamage(2); yield return new WaitForSeconds(2.5f);
            ScreenCapture.CaptureScreenshot(dir + "/damaged.png"); yield return new WaitForSeconds(0.3f);
            StatusFx.On(root).SetDamage(0);
            CloseDuelLab(); yield return new WaitForSeconds(1f);
            // 4. a knock-out end to end in a plain duel
            if (chUi == null) InitChallenge();
            rival = Challenge.MakeBot(55, -1); chOnline = false;
            BuildDuelBoard(4);
            until = Time.time + 20f;
            while (chPhase != ChPhase.Duel && Time.time < until) yield return new WaitForSeconds(0.2f);
            hunt.StunFoe(5f);
            int scoreBefore = chDuelMe;
            if (hunt.ChallengeFoeTile is GridPos ft) { robot.Spawn(grid, new GridPos(laneX, ft.y - HuntSystem.DuelGap)); yield return null; }
            HitFoe(400f);
            yield return new WaitForSeconds(0.25f);
            ScreenCapture.CaptureScreenshot(dir + "/ko.png");
            yield return new WaitForSeconds(5f);
            log.Append("knock-out: score " + scoreBefore + " -> " + chDuelMe + ", phase now " + chPhase + ", rival back " + (hunt.ChallengeFoeModel != null) + "\n");
            ScreenCapture.CaptureScreenshot(dir + "/after_ko.png");
            File.WriteAllText(dir + "/f3.txt", log.ToString());
            Application.Quit();
        }
    }
}
