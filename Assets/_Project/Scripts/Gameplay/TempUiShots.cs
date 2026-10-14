// TEMP-FILE: field battle v2 test (delete after).
using System.Collections;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Gameplay
{
    public partial class GameManager
    {
        private IEnumerator UiShots()
        {
            Application.runInBackground = true;
            SaveData.Language = Language.Turkish; // the tests in Turkish from now on
            string d = "E:/OFIGAME/stage_test/meydan2";
            System.IO.Directory.CreateDirectory(d);
            yield return new WaitForSecondsRealtime(5f);
            SaveData.HeroChosen = true;
            SaveData.UnlockedLevel = Mathf.Max(SaveData.UnlockedLevel, 30);
            Challenge.NameChosen = true;
            ShowMenu(); yield return new WaitForSecondsRealtime(1f);
            chMode = ChMode.Stages; StartChallenge(false);
            for (float t = 0; t < 30f && chPhase != ChPhase.StageIntro; t += 0.1f) yield return new WaitForSecondsRealtime(0.1f);
            yield return new WaitForSecondsRealtime(1.2f); ScreenCapture.CaptureScreenshot($"{d}/0_flyA.png");
            for (float t = 0; t < 30f && chPhase != ChPhase.Stages; t += 0.2f) yield return new WaitForSecondsRealtime(0.2f);
            yield return new WaitForSecondsRealtime(2f); ScreenCapture.CaptureScreenshot($"{d}/1_stageB.png");
            float start = Time.time;
            for (int i = 0; i < 160 && chPhase == ChPhase.Stages; i++)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                health = 1f;
                if (i % 20 == 0 && clash.SkirmishGroup >= 0 && clash.SkirmishGroup < 6) Debug.Log("M2 dbg " + clash.DebugGroup(clash.SkirmishGroup));
                if (i % 20 == 0) Debug.Log($"M2 t{Time.time - start:0} mineOnA {clash.Alive(0)} group {clash.SkirmishGroup} theirsOnB {hunt.ChallengeSoldiersLeft}+{WaitingOnB}");
            }
            Debug.Log($"M2 done t{Time.time - start:0} phase {chPhase} lead {chLead}");
            Application.Quit();
        }
    }
}
