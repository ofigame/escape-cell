using System.Collections;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The floors' bosses as the game sets them (the fixes design's job 8; the fighting in HuntSystemForms): floors 1-9
    /// of a world end with vanG's guardian (less health, see Toughen), a world's tenth floor with a form of vanG named
    /// after the floor ("Kanal Bekçisi · vanG's guise") that fights with the move the world then gives, the last world's
    /// tenth floor with vanG itself. A form's move is named over it when it uses it.
    /// </summary>
    public partial class GameManager
    {
        private bool bossWired;

        /// <summary>A campaign floor starts: who waits at its end, and with which move.</summary>
        private void SetupBoss()
        {
            if (!bossWired)
            {
                bossWired = true;
                hunt.FormShout += (at, m) => FloatAt(at, Loc.T(MoveKey(m)), UI.UiKit.Style.orange);
                hunt.FoiHeld += s => strikeCooldown = Mathf.Max(strikeCooldown, s);
                hunt.FoiSlowed += s => StartCoroutine(SlowFoi(s));
            }
            var t = FloorTuning.Current;
            hunt.GuardianBigEvery = t.guardianBigEvery;
            hunt.FormFirstMove = t.formFirstMove;
            hunt.PlayerUlti = MoveSet.Ulti == UltiKind.Cinematic ? FormMove.Quake : ParseMove("u", MoveSet.Ulti.ToString());
            bool campaign = level != null && level.mission == MissionType.Hunt && !arenaRun && !trailRun && !challengeRun && !bonusRun;
            int inWorld = levelIndex % LevelCatalog.LevelsPerWorld, world = levelIndex / LevelCatalog.LevelsPerWorld;
            if (!campaign) { hunt.Boss = BossKind.Real; hunt.Form = FormMove.None; return; }
            if (inWorld < LevelCatalog.LevelsPerWorld - 1) { hunt.Boss = BossKind.Guardian; hunt.Form = FormMove.None; return; }
            if (world >= LevelCatalog.WorldCount - 1) { hunt.Boss = BossKind.Real; hunt.Form = FormMove.None; return; }
            hunt.Boss = BossKind.Form;
            string key = MoveSet.KeyOfWorld(world + 1); // (the move this world gives; the first world's form leaps like FLY)
            hunt.Form = key == null ? FormMove.Leap : ParseMove(key.Substring(5, 1), key.Substring(7));
        }

        /// <summary>The boss's name over its health bar.</summary>
        private string BossName()
        {
            switch (hunt.Boss)
            {
                case BossKind.Guardian: return Loc.T("monster.guardian");
                case BossKind.Form: return Loc.F("monster.form", Loc.T("lvl." + (levelIndex + 1) + ".title"));
                default: return Loc.T("monster.vanG");
            }
        }

        private static FormMove ParseMove(string kind, string name)
        {
            if (kind == "s" && name == "Fly") return FormMove.Leap;
            return System.Enum.TryParse(name, out FormMove m) ? m : FormMove.None;
        }

        /// <summary>A form move's name key (the duel's move names).</summary>
        private static string MoveKey(FormMove m)
        {
            if (m == FormMove.Leap) return "move.s.Fly";
            if (m >= FormMove.Backflip && m <= FormMove.Teleport) return "move.s." + m;
            if (m >= FormMove.Quake && m <= FormMove.CoreBurst) return "move.u." + m;
            return "move.p." + m;
        }

        /// <summary>A quake form's blow: foi slowed a moment.</summary>
        private IEnumerator SlowFoi(float seconds)
        {
            float was = robot.TimeBoost;
            robot.TimeBoost = was * 0.6f;
            yield return new WaitForSeconds(seconds);
            if (Mathf.Approximately(robot.TimeBoost, was * 0.6f)) robot.TimeBoost = was;
        }
    }
}
