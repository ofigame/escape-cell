using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The marathons of the scenario (seven of them, 60-130 seconds): halfway through the checkpoint flag goes up and a
    /// helicopter drops a super-power crate a few tiles from the robot. It waits 10 seconds; grabbing it makes the
    /// robot untouchable for 20 seconds, smashing every block it meets. After the flag a crash costs a life and play
    /// goes on from the flag (<see cref="MarathonRespawn"/>). The pace builds up over the level, and every 20-28 seconds a short attack of a different
    /// kind comes (a block storm, sweeping lines, bombs or hunting blocks); the blocks still warn a little longer than
    /// elsewhere, so there is always time to see them coming.
    /// </summary>
    public partial class GameManager
    {
        private const float CrateStay = 10f, SuperSeconds = 20f, WaveSeconds = 7f;

        /// <summary>The kinds of heavy attack, taken in turn (shuffled per level).</summary>
        private enum Wave { Storm, Lines, Bombs, Hunters }

        private float heliTimer, crateLeft, superLeft, waveTimer, waveLeft;
        private float baseLine, baseBombs;
        private readonly List<Wave> waveOrder = new List<Wave>();
        private int waveCount;
        private SuperCrate crate;
        private GridPos crateTile = new GridPos(-99, -99);
        private bool heliOnWay;
        private bool checkpointReached;
        private GameObject superAura;

        private void SetupMarathon()
        {
            // The scenario's marathons have one helicopter: it brings the crate with the checkpoint flag, halfway through.
            heliTimer = level.surviveSeconds * 0.5f - 4f;
            checkpointReached = false;
            waveTimer = Random.Range(16f, 20f);
            waveLeft = 0f;
            superLeft = 0f;
            heliOnWay = false;
            baseLine = level.lineWaveChance;
            baseBombs = level.bombChance;
            waveOrder.Clear();
            waveOrder.AddRange(new[] { Wave.Storm, Wave.Lines, Wave.Bombs, Wave.Hunters });
            for (int i = waveOrder.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (waveOrder[i], waveOrder[j]) = (waveOrder[j], waveOrder[i]);
            }
            waveCount = 0;
            hazards.IsProtected = p => p == crateTile;
        }

        private void UpdateMarathon(float dt)
        {
            if (previewing) return;

            // Halfway: the checkpoint flag. From now on a crash costs a life and play goes on from here.
            if (!checkpointReached && elapsed >= level.surviveSeconds * 0.5f)
            {
                checkpointReached = true;
                ui.ShowIntro(Loc.T("road.checkpoint"), Loc.T("marathon.checkpoint"));
                AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.5f);
                fx.Burst(robot.transform.position + Vector3.up, new Color(0.5f, 1f, 0.7f), new Color(0.5f, 2.2f, 1f), 30, 5f);
            }

            // The helicopter.
            if (!heliOnWay && crate == null)
            {
                heliTimer -= dt;
                if (heliTimer <= 0f) SendHelicopter();
            }

            // The crate waits, blinks in its last seconds, and is gone if nobody takes it.
            if (crate != null)
            {
                crateLeft -= dt;
                crate.Leaving = crateLeft < 3f;
                if (crateLeft <= 0f)
                {
                    FloatAt(crate.transform.position + Vector3.up, Loc.T("float.superMissed"), Palette.UiCyan);
                    ClearCrate();
                    heliTimer = 9999f; // one crate per marathon
                }
            }

            // Standing on the crate picks it up, however the robot got there (a hop, a slide on ice, a current).
            if (crate != null && robot.Position == crateTile) PickUpSuper();

            // The super power.
            if (superLeft > 0f)
            {
                superLeft -= dt;
                if (superLeft <= 0f) EndSuper();
            }

            // The pace builds up over the level: half as fast again by the end.
            float build = 1f + 0.5f * Mathf.Clamp01(elapsed / Mathf.Max(1f, level.surviveSeconds));

            // A short attack now and then (not while the robot is untouchable: that would waste it).
            if (waveLeft > 0f)
            {
                waveLeft -= dt;
                if (waveLeft > 0f) return;
                EndWave();
            }
            hazards.PaceBoost = build;
            if (superLeft > 0f) return;
            waveTimer -= dt;
            if (waveTimer <= 0f) StartWave(build);
        }

        private void StartWave(float build)
        {
            var wave = waveOrder[waveCount++ % waveOrder.Count];
            waveLeft = WaveSeconds;
            waveTimer = Random.Range(20f, 28f);
            switch (wave)
            {
                case Wave.Storm:
                    hazards.PaceBoost = build * 2.1f;
                    break;
                case Wave.Lines:
                    hazards.PaceBoost = build * 1.4f;
                    level.lineWaveChance = 0.6f;
                    break;
                case Wave.Bombs:
                    hazards.PaceBoost = build * 1.4f;
                    level.bombChance = 0.55f;
                    break;
                case Wave.Hunters:
                    hazards.PaceBoost = build * 1.3f;
                    hazards.Hunting = true;
                    break;
            }
            ui.ShowIntro(Loc.T("wave.title"), Loc.T("wave." + wave));
            BipSay("wave");
            AudioManager.PlaySfx(Sfx.Warning, 0.9f, 0.8f);
            cameraRig.Shake(0.4f);
        }

        private void EndWave()
        {
            waveLeft = 0f;
            if (level == null) return;
            level.lineWaveChance = baseLine;
            level.bombChance = baseBombs;
            hazards.Hunting = (level.rules & FloorRule.Hunter) != 0;
        }

        private void SendHelicopter()
        {
            // A free, safe tile two to four steps from the robot, so the crate lands in view.
            var dist = WalkDistances(robot.Position);
            var options = new List<GridPos>();
            foreach (var kv in dist)
                if (kv.Value >= 2 && kv.Value <= 4 && grid.IsStandable(kv.Key) && !hazards.IsThreatened(kv.Key)) options.Add(kv.Key);
            if (options.Count == 0)
            {
                heliTimer = 2f; // try again in a moment
                return;
            }
            var tile = options[Random.Range(0, options.Count)];
            var at = GridView.ToWorld(tile) + Vector3.up * GridView.SurfaceY;
            heliOnWay = true;
            crateTile = tile; // already protected from blocks while it is on the way
            var heli = Helicopter.Create(at, at - robot.transform.position + new Vector3(1f, 0f, -1f));
            heli.Dropped += () =>
            {
                heliOnWay = false;
                if (State != GameState.Playing || level == null || !level.marathon) return;
                crate = SuperCrate.Create(at);
                crateLeft = CrateStay;
                fx.Dust(at + Vector3.up * 0.1f, SuperCrate.Gold, 16, 3f);
                AudioManager.PlaySfx(Sfx.Impact, 0.7f, 1.3f);
                if (robot.Position == crateTile) PickUpSuper();
                else BipSay("crate");
            };
            FloatAt(robot.transform.position + Vector3.up * 0.8f, Loc.T("float.heliComing"), SuperCrate.Gold);
            AudioManager.PlaySfx(Sfx.Shield, 0.6f, 0.6f);
        }

        private void PickUpSuper()
        {
            if (crate == null) return;
            var at = crate.transform.position;
            ClearCrate();
            heliTimer = 9999f;
            superLeft = SuperSeconds;
            // Untouchable: a shield that never breaks smashes falling blocks and the blocks the robot hops into,
            // and carries it over holes.
            robot.GiveShield(SuperSeconds, 999);
            if (superAura == null) superAura = MakeSuperAura(robot.transform);
            if (waveLeft > 0f) EndWave();
            hazards.PaceBoost = 1f;
            fx.Burst(at + Vector3.up * 0.6f, SuperCrate.Gold, SuperCrate.GoldGlow, 40, 6f);
            cameraRig.Punch(1.2f);
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 1.3f);
            Haptics.Medium();
            FloatAt(robot.transform.position + Vector3.up * 0.8f, Loc.F("float.superPower", (int)SuperSeconds), SuperCrate.Gold);
        }

        private void EndSuper()
        {
            superLeft = 0f;
            if (superAura != null) Destroy(superAura);
            superAura = null;
        }

        /// <summary>
        /// A crash after the checkpoint: if a life is left, it is spent and the haul goes on from the flag (the clock back
        /// at halfway, the floor cleared around a safe tile, a moment of shield). False: no checkpoint or no life, a real loss.
        /// </summary>
        private bool MarathonRespawn()
        {
            if (level == null || !level.marathon || !checkpointReached || bonusRun || !Lives.TryConsume()) return false;
            elapsed = level.surviveSeconds * 0.5f;
            hazards.Stop();
            hazards.Begin(grid, level, MissionProgress);
            if (waveLeft > 0f) EndWave();
            var safe = SafeTileNear(grid.CenterFloor(), grid.CenterFloor());
            robot.Spawn(grid, safe);
            robot.GiveShield(3f);
            cameraRig.Shake(0.6f);
            fx.Burst(robot.transform.position + Vector3.up * 0.5f, new Color(0.5f, 1f, 0.7f), new Color(0.5f, 2.2f, 1f), 30, 5f);
            ui.ShowIntro(Loc.T("road.checkpoint"), Loc.T("road.lifeLost") + " · " + Loc.F("lives.left", Lives.Count));
            return true;
        }

        private void ClearCrate()
        {
            if (crate != null) Destroy(crate.gameObject);
            crate = null;
            crateTile = new GridPos(-99, -99);
        }

        private void ClearMarathon()
        {
            ClearCrate();
            EndSuper();
            heliOnWay = false;
            if (waveLeft > 0f) EndWave();
            if (hazards != null) hazards.PaceBoost = 1f;
            foreach (var h in FindObjectsByType<Helicopter>(FindObjectsSortMode.None)) Destroy(h.gameObject);
        }

        /// <summary>A golden glow around the robot while it is untouchable, with sparks circling it.</summary>
        private static GameObject MakeSuperAura(Transform robotTransform)
        {
            var aura = new GameObject("SuperAura");
            aura.transform.SetParent(robotTransform, false);
            aura.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            Shapes.Primitive(PrimitiveType.Sphere, "Glow", aura.transform, Vector3.zero, Vector3.one * 1.15f,
                MaterialFactory.CreateTransparent(new Color(1f, 0.85f, 0.35f, 0.25f), SuperCrate.GoldGlow * 0.8f));
            var spark = MaterialFactory.Create(Color.white, SuperCrate.GoldGlow);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Shapes.Rounded("Spark", aura.transform, new Vector3(Mathf.Cos(a) * 0.6f, (i % 2) * 0.2f - 0.1f, Mathf.Sin(a) * 0.6f), Vector3.one * 0.08f, 0.03f, spark);
            }
            aura.AddComponent<Spin>();
            return aura;
        }
    }
}
