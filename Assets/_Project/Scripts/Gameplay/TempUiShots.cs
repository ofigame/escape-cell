// TEMP-FILE: Faz 4 acceptance: POWER HIT per family, KNOCKDOWN (the rival's arc, landing, getting up), FLY (take-off,
// flight, landing), a blow from the air, SKY STRIKE, the winner's flourish. -sbF4 <dir> ; quits after.
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
        private IEnumerator Shots(string dir, string name, float[] at)
        {
            float start = Time.realtimeSinceStartup;
            for (int i = 0; i < at.Length; i++)
            {
                while (Time.realtimeSinceStartup - start < at[i]) yield return null;
                ScreenCapture.CaptureScreenshot(dir + "/" + name + "_" + i + ".png");
            }
        }

        private IEnumerator UiShots()
        {
            var args = Environment.GetCommandLineArgs();
            int ai = Array.IndexOf(args, "-sbF4");
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
            if (duelLab != null) duelLab.gameObject.SetActive(false); // (pictures without the panel)
            // 1. POWER HIT by family
            foreach (var k in new[] { WeaponKind.Sword, WeaponKind.Spear, WeaponKind.Axe, WeaponKind.Hammer })
            {
                LabWeapon(k); yield return new WaitForSeconds(0.5f);
                LabStrike(); yield return new WaitForSeconds(0.4f);
                myComboMove = HeavyCombo; myCombo = HeavyCombo;
                OnSkill(DuelSkillBar.Move.Heavy);
                yield return Shots(dir, "power_" + k, new[] { 0.06f, 0.16f, 0.3f });
                yield return new WaitForSeconds(1.5f);
            }
            // 2. KNOCKDOWN: the rival's arc and landing (its tile before / after: the rules' push)
            LabWeapon(WeaponKind.Sword); yield return new WaitForSeconds(0.4f);
            LabStrike(); yield return new WaitForSeconds(0.4f);
            myComboMove = UpperCombo; myCombo = UpperCombo;
            var tileBefore = hunt.ChallengeFoeTile;
            OnSkill(DuelSkillBar.Move.Upper);
            yield return Shots(dir, "knockdown", new[] { 0.15f, 0.45f, 0.9f, 1.6f, 2.6f });
            yield return new WaitForSeconds(1.5f);
            log.Append("knockdown: rival tile " + tileBefore + " -> " + hunt.ChallengeFoeTile + "\n");
            // 3. FLY
            LabStrike(); yield return new WaitForSeconds(0.3f);
            robot.Spawn(grid, new GridPos(laneX, Mathf.Max(1, (hunt.ChallengeFoeTile ?? robot.Position).y - 6))); yield return new WaitForSeconds(0.4f);
            cameraRig.StartCoroutine(DoSpecial(true, SpecialKind.Fly));
            yield return Shots(dir, "fly", new[] { 0.08f, 0.3f, 0.45f, 0.7f, 1.0f });
            yield return new WaitForSeconds(3f);
            // 4. a blow from the air
            LabStrike(); yield return new WaitForSeconds(0.6f);
            Jump(); yield return new WaitForSeconds(0.2f);
            strikeCooldown = 0f; TryStrikeNow(hunt.ChallengeFoeTile.Value, true);
            yield return Shots(dir, "air", new[] { 0.05f, 0.2f, 0.5f });
            yield return new WaitForSeconds(2.5f);
            // 5. SKY STRIKE
            robot.Spawn(grid, new GridPos(laneX, Mathf.Max(1, (hunt.ChallengeFoeTile ?? robot.Position).y - 5))); yield return new WaitForSeconds(0.4f);
            cameraRig.StartCoroutine(DoSpecial(true, SpecialKind.SkyStrike));
            yield return Shots(dir, "sky", new[] { 0.45f, 0.62f, 0.8f, 1.0f, 1.3f });
            yield return new WaitForSeconds(2.5f);
            // 6. the winner's flourish by family
            foreach (var k in new[] { WeaponKind.Sword, WeaponKind.Hammer, WeaponKind.Axe, WeaponKind.Spear })
            {
                LabWeapon(k); yield return new WaitForSeconds(0.4f);
                heldWeapon.Flourish();
                yield return Shots(dir, "flourish_" + k, new[] { 0.3f, 1.2f });
                yield return new WaitForSeconds(1.2f);
            }
            File.WriteAllText(dir + "/f4.txt", log.ToString());
            CloseDuelLab();
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }
    }
}
