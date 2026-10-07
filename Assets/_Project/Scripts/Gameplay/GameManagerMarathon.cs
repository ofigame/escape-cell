using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The long hauls (two to three minutes): every 30 seconds a helicopter flies in and drops a super-power crate a few
    /// tiles from the robot. It waits 10 seconds; grabbing it makes the robot untouchable for 20 seconds, smashing
    /// every block it meets. Now and then a short heavy wave of blocks keeps the pressure on; the blocks of these
    /// levels warn longer, so there is always time to see them coming.
    /// </summary>
    public partial class GameManager
    {
        private const float HeliEvery = 30f, CrateStay = 10f, SuperSeconds = 20f, WaveSeconds = 6f;

        private float heliTimer, crateLeft, superLeft, waveTimer, waveLeft;
        private SuperCrate crate;
        private GridPos crateTile = new GridPos(-99, -99);
        private bool heliOnWay;
        private GameObject superAura;

        private void SetupMarathon()
        {
            heliTimer = HeliEvery - 6f; // the first one comes a little early
            waveTimer = Random.Range(35f, 45f);
            waveLeft = 0f;
            superLeft = 0f;
            heliOnWay = false;
            hazards.IsProtected = p => p == crateTile;
        }

        private void UpdateMarathon(float dt)
        {
            if (previewing) return;

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
                    heliTimer = HeliEvery;
                }
            }

            // The super power.
            if (superLeft > 0f)
            {
                superLeft -= dt;
                if (superLeft <= 0f) EndSuper();
            }

            // A short heavy wave now and then (not while the robot is untouchable: that would waste it).
            if (waveLeft > 0f)
            {
                waveLeft -= dt;
                if (waveLeft <= 0f) hazards.PaceBoost = 1f;
            }
            else if (superLeft <= 0f)
            {
                waveTimer -= dt;
                if (waveTimer <= 0f)
                {
                    waveLeft = WaveSeconds;
                    waveTimer = Random.Range(35f, 45f);
                    hazards.PaceBoost = 2.2f;
                    ui.ShowIntro(Loc.T("wave.title"), Loc.T("wave.text"));
                    AudioManager.PlaySfx(Sfx.Warning, 0.9f, 0.8f);
                    cameraRig.Shake(0.4f);
                }
            }
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
            };
            FloatAt(robot.transform.position + Vector3.up * 0.8f, Loc.T("float.heliComing"), SuperCrate.Gold);
            AudioManager.PlaySfx(Sfx.Shield, 0.6f, 0.6f);
        }

        private void PickUpSuper()
        {
            if (crate == null) return;
            var at = crate.transform.position;
            ClearCrate();
            heliTimer = HeliEvery;
            superLeft = SuperSeconds;
            // Untouchable: a shield that never breaks smashes falling blocks and the blocks the robot hops into,
            // and carries it over holes.
            robot.GiveShield(SuperSeconds, 999);
            if (superAura == null) superAura = MakeSuperAura(robot.transform);
            hazards.PaceBoost = 1f;
            waveLeft = 0f;
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
            waveLeft = 0f;
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
