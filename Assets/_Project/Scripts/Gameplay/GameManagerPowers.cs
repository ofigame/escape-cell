using System.Collections;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The floors' big powers. The ULTI fills with every robot felled; when full, a press throws the floor into slow
    /// motion while foi dashes from robot to robot slashing, then ends it with a shockwave. The crowd skills come only
    /// when robots press round foi: a fire ball, an ice ball or a great rusty chain is offered for a few seconds — a
    /// ball leaves foi, grows as it rises and bursts in the middle of the crowd; the chain whips round foi. A floor has
    /// one crowd skill early on, two in the middle levels and three late.
    /// </summary>
    public partial class GameManager
    {
        public enum CrowdSkill { Fire, Ice, Chain }

        /// <summary>The ultimate's bar (0..1) and its share filled by one robot felled.</summary>
        private float ultiMeter;
        private const float UltiPerKill = 1f / 14f;
        /// <summary>How long the ultimate fights on its own.</summary>
        private const float UltiSeconds = 5f;
        private bool powerBusy;
        /// <summary>Slow motion of a power (unscaled seconds left), and how slow.</summary>
        private float powerSlowLeft, powerSlowScale = 1f;

        private int crowdUses;
        private float crowdCooldown, crowdOffer;
        private CrowdSkill crowdKind;
        private const float CrowdOfferSeconds = 8f;

        private static readonly Color UltiColour = new Color(0.75f, 0.45f, 1f);

        /// <summary>A plain floor fight (the campaign's robots), where the powers belong.</summary>
        private bool PowerFloor => State == GameState.Playing && level != null && level.mission == MissionType.Hunt && !challengeRun && !arenaRun
            && !bonusRun && !trailRun && roadPhase == RoadPhase.None && !previewing && !IntroRunning && !introPending && (glassCell == null || glassCell.Broken);

        /// <summary>A new floor: the bar empty, the crowd skills counted out for this level.</summary>
        private void ResetPowers()
        {
            ultiMeter = 0f;
            crowdUses = levelIndex < 50 ? 1 : levelIndex < 150 ? 2 : 3;
            crowdCooldown = 6f;
            crowdOffer = 0f;
            powerBusy = false;
            ui.SetUlti(0f, false);
            ui.SetCrowdSkill((int)crowdKind, false);
        }

        /// <summary>The ball or chain of the power under way (gone with it).</summary>
        private GameObject powerThing;

        /// <summary>A power cut short (a restart, a revive, the menu): nothing of it stays.</summary>
        private void ClearPowers()
        {
            if (powerThing != null) Destroy(powerThing);
            powerThing = null;
            powerBusy = false;
            powerSlowLeft = 0f;
        }

        /// <summary>A boost's head start: the ULTI bar begins this full (0..1).</summary>
        private void StartUltiAt(float share)
        {
            ultiMeter = Mathf.Clamp01(Mathf.Max(ultiMeter, share));
            FloatAt(robot.transform.position + Vector3.up * 1.2f, Loc.T("float.ultiHalf"), UltiColour);
        }

        private void OnFoeFelledForPowers()
        {
            if (!PowerFloor || powerBusy) return;
            ultiMeter = Mathf.Min(1f, ultiMeter + UltiPerKill);
        }

        private void UpdatePowers(float dt)
        {
            if (!PowerFloor) { ui.SetUlti(0f, false); ui.SetCrowdSkill((int)crowdKind, false); crowdOffer = 0f; return; }
            ui.SetUlti(ultiMeter, true);
            if (powerBusy) return;
            if (crowdOffer > 0f)
            {
                crowdOffer -= dt;
                ui.SetCrowdSkill((int)crowdKind, crowdOffer > 0f); // (it breathes while offered)
                if (crowdOffer <= 0f) crowdCooldown = 5f;
                return;
            }
            if (crowdCooldown > 0f) { crowdCooldown -= dt; return; }
            if (crowdUses <= 0) return;
            int threshold = levelIndex < 20 ? 4 : 5;
            if (hunt.CrowdAround(robot.Position, 3) < threshold) return;
            // A crowd: one of the three, in turn through the levels (and a different one each time on the floor).
            crowdKind = (CrowdSkill)((levelIndex + crowdUses) % 3);
            crowdOffer = CrowdOfferSeconds;
            ui.SetCrowdSkill((int)crowdKind, true);
            FloatAt(robot.transform.position + Vector3.up * 1.4f, Loc.T("power.crowd"), new Color(1f, 0.8f, 0.35f));
            AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.3f);
        }

        /// <summary>Slows the floor for a moment (unscaled seconds); <see cref="Update"/> keeps it.</summary>
        private void PowerSlow(float seconds, float scale)
        {
            powerSlowLeft = Mathf.Max(powerSlowLeft, seconds);
            powerSlowScale = scale;
        }

        private int PowerDamage(float times)
        {
            var w = weapon ?? Armory.Equipped;
            return Mathf.Max(1, Mathf.RoundToInt(MyBlow(w) * times));
        }

        // ---------- The ultimate ----------

        private void OnUltiPressed()
        {
            if (!PowerFloor || powerBusy || ultiMeter < 1f) return;
            StartCoroutine(LevelUlti());
        }

        private IEnumerator LevelUlti()
        {
            powerBusy = true;
            ultiMeter = 0f;
            ui.SetUlti(0f, true);
            hurtCooldown = Mathf.Max(hurtCooldown, UltiSeconds + 0.6f); // untouchable through it
            hunt.Freeze();
            PowerSlow(UltiSeconds + 0.4f, 0.75f); // a little slowed, the flips stay quick
            ui.Flash(UltiColour, 0.5f);
            ui.ShowCallout(Loc.T("power.ultiShout"), robot.transform.position + Vector3.up * 1.6f, cameraRig.Cam, 1.4f);
            cameraRig.Punch(1f);
            AudioManager.PlaySfx(Sfx.Win, 1f, 0.6f);
            fx.Burst(robot.transform.position + Vector3.up * 0.6f, UltiColour, UltiColour * 2.5f, 40, 6f);
            // For five seconds foi fights on its own: the nearest enemy each time, left and right in turn, foi turning to
            // each while it plays its combo (the ultimate's own move, through all of it), then a spin kick to finish.
            var moves = robot.Moves;
            float comboLength = SkinnedHero.MoveLength("cast");
            moves?.PlayMove("cast", comboLength > 0f ? comboLength / (UltiSeconds - 0.3f) : 1f, true);
            float until = Time.unscaledTime + UltiSeconds;
            for (int i = 0; Time.unscaledTime < until; i++)
            {
                yield return new WaitForSecondsRealtime(0.06f);
                while (State == GameState.Paused) { until += Time.unscaledDeltaTime; yield return null; }
                if (!PowerFloor) break;
                var near = hunt.FightersAround(robot.Position, 4);
                if (near.Count == 0) { yield return new WaitForSecondsRealtime(0.15f); continue; }
                SortLeftRight(near);
                var at = near[i % 2 == 0 || near.Count == 1 ? 0 : 1];
                var to = GridView.ToWorld(at) + Vector3.up * GridView.SurfaceY;
                bool flip = i % 2 == 0;
                if (moves != null)
                {
                    robot.Face(to);
                    yield return new WaitForSecondsRealtime(0.36f); // (the combo's rhythm)
                }
                else
                {
                    robot.Strike(to);
                    if (flip) yield return Somersault();
                    else
                    {
                        if (heldWeapon != null) heldWeapon.Swing();
                        yield return new WaitForSecondsRealtime(0.12f);
                    }
                }
                SlashFx.Create(robot.transform.position + Vector3.up * 0.5f, to, UltiColour, flip ? 2.4f : 1.8f);
                fx.Burst(to + Vector3.up * 0.6f, UltiColour, UltiColour * 2.5f, 22, 5f);
                cameraRig.Shake(flip ? 0.45f : 0.3f);
                AudioManager.PlaySfx(Sfx.Impact, 1f, flip ? 0.9f : 1.2f);
                hunt.Strike(at, PowerDamage(4f));
            }
            if (moves != null && moves.PlayMove("kick", 1.3f, true)) yield return new WaitForSecondsRealtime(SkinnedHero.MoveLength("kick") / 1.3f * 0.55f); // the spin kick lands
            else yield return new WaitForSecondsRealtime(0.3f);
            while (State == GameState.Paused) yield return null;
            if (PowerFloor)
            {
                // The finish: a shockwave out from foi.
                var c = robot.transform.position;
                Shockwave.Create(c, 4.5f, UltiColour);
                fx.Burst(c + Vector3.up * 0.5f, UltiColour, UltiColour * 3f, 70, 9f);
                cameraRig.Shake(1f);
                cameraRig.Punch(1f);
                AudioManager.PlaySfx(Sfx.Impact, 1f, 0.5f);
                foreach (var at in hunt.FightersAround(robot.Position, 3)) hunt.Strike(at, PowerDamage(3f));
            }
            yield return new WaitForSecondsRealtime(0.25f);
            if (skillFreezeLeft <= 0f) hunt.Resume();
            powerBusy = false;
        }

        /// <summary>The targets in turn from one side to the other: right, left, right... as they stand round foi.</summary>
        private void SortLeftRight(List<GridPos> targets)
        {
            var me = robot.Position;
            var right = targets.FindAll(p => p.x >= me.x);
            var left = targets.FindAll(p => p.x < me.x);
            targets.Clear();
            for (int i = 0; i < Mathf.Max(right.Count, left.Count); i++)
            {
                if (i < right.Count) targets.Add(right[i]);
                if (i < left.Count) targets.Add(left[i]);
            }
        }

        /// <summary>A quick forward somersault: foi's body turns over once in the air and lands.</summary>
        private IEnumerator Somersault()
        {
            var vis = robot.Visual;
            if (vis == null) yield break;
            const float time = 0.2f;
            for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / time)
            {
                if (vis == null) yield break;
                float e = Mathf.SmoothStep(0f, 1f, k);
                vis.localRotation = Quaternion.Euler(-360f * e, 0f, 0f);
                vis.localPosition = Vector3.up * Mathf.Sin(e * Mathf.PI) * 0.6f; // up and over
                yield return null;
            }
            if (vis != null) { vis.localRotation = Quaternion.identity; vis.localPosition = Vector3.zero; }
        }

        // ---------- The crowd skills ----------

        private void OnCrowdSkillPressed()
        {
            if (!PowerFloor || powerBusy || crowdOffer <= 0f || crowdUses <= 0) return;
            crowdOffer = 0f;
            crowdUses--;
            crowdCooldown = 12f;
            ui.SetCrowdSkill((int)crowdKind, false);
            StartCoroutine(crowdKind == CrowdSkill.Chain ? ChainWhip() : BallBurst(crowdKind == CrowdSkill.Fire));
        }

        private static Color SkillColour(CrowdSkill k) =>
            k == CrowdSkill.Fire ? new Color(1f, 0.5f, 0.15f) : k == CrowdSkill.Ice ? new Color(0.55f, 0.9f, 1f) : new Color(0.62f, 0.38f, 0.22f);

        /// <summary>A fire or ice ball: out of foi, growing as it climbs, down into the middle of the crowd, and a burst.</summary>
        private IEnumerator BallBurst(bool fire)
        {
            powerBusy = true;
            var colour = SkillColour(fire ? CrowdSkill.Fire : CrowdSkill.Ice);
            if (!hunt.BombSpot(robot.Position, 7, 2, out var centre))
            {
                var near = hunt.FightersAround(robot.Position, 6);
                centre = near.Count > 0 ? near[0] : robot.Position;
            }
            var mat = MaterialFactory.Create(colour, colour * 2.4f);
            var ball = Shapes.Primitive(PrimitiveType.Sphere, fire ? "FireBall" : "IceBall", null, robot.transform.position + Vector3.up * 0.8f, Vector3.one * 0.2f, mat).transform;
            powerThing = ball.gameObject;
            var halo = Shapes.Primitive(PrimitiveType.Sphere, "Halo", ball, Vector3.zero, Vector3.one * 1.5f,
                MaterialFactory.CreateTransparent(new Color(colour.r, colour.g, colour.b, 0.3f), colour * 1.5f)).transform;
            AudioManager.PlaySfx(Sfx.Hop, 1f, 0.6f);
            var from = robot.transform.position + Vector3.up * 0.8f;
            var to = GridView.ToWorld(centre) + Vector3.up * (GridView.SurfaceY + 0.4f);
            const float flight = 1.0f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / flight)
            {
                while (State == GameState.Paused) yield return null;
                if (ball == null) yield break;
                float e = t * t * (3f - 2f * t);
                ball.position = Vector3.Lerp(from, to, e) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 3.6f;
                ball.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.5f, Mathf.Sin(Mathf.Min(1f, t * 1.4f) * Mathf.PI * 0.5f));
                halo.localScale = Vector3.one * (1.4f + 0.15f * Mathf.Sin(Time.time * 25f));
                if (Random.value < 0.5f) fx.Burst(ball.position, colour, colour * 2f, 2, 1.5f);
                yield return null;
            }
            if (ball != null) Destroy(ball.gameObject);
            if (!PowerFloor) { powerBusy = false; yield break; }
            // The burst, in slow motion.
            PowerSlow(0.9f, 0.3f);
            ui.Flash(colour, 0.35f);
            Shockwave.Create(to, 4f, colour);
            fx.Burst(to, colour, colour * 3f, 80, 9f);
            fx.Dust(to, fire ? new Color(0.35f, 0.3f, 0.3f) : new Color(0.9f, 0.95f, 1f), 30, 5f);
            cameraRig.Shake(1f);
            cameraRig.Punch(0.9f);
            AudioManager.PlaySfx(Sfx.Impact, 1f, fire ? 0.5f : 0.7f);
            Haptics.Medium();
            const int r = 2;
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                    hunt.Strike(centre + new GridPos(x, y), PowerDamage(fire ? 5f : 3f));
            if (fire) StartCoroutine(FireUp(centre, 0f, r)); // the ground burns a while
            else hunt.IceAround(centre, r, 4f, PowerDamage(1f));
            powerBusy = false;
        }

        private Material chainMat;
        private static Mesh linkMesh;

        /// <summary>An oval ring (a chain link) lying in the y-z plane, long along z: half-length <paramref name="a"/>,
        /// half-width <paramref name="b"/>, its iron <paramref name="tube"/> thick.</summary>
        private static Mesh LinkMesh(float a, float b, float tube)
        {
            const int around = 20, sides = 8;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= around; i++)
            {
                float u = i * Mathf.PI * 2f / around;
                var centre = new Vector3(0f, Mathf.Sin(u) * b, Mathf.Cos(u) * a);
                var tangent = new Vector3(0f, Mathf.Cos(u) * b, -Mathf.Sin(u) * a).normalized;
                var outward = Vector3.Cross(tangent, Vector3.right).normalized;
                if (Vector3.Dot(outward, centre) < 0f) outward = -outward;
                for (int j = 0; j <= sides; j++)
                {
                    float w = j * Mathf.PI * 2f / sides;
                    var dir = outward * Mathf.Cos(w) + Vector3.right * Mathf.Sin(w);
                    v.Add(centre + dir * tube);
                    n.Add(dir);
                }
            }
            for (int i = 0; i < around; i++)
                for (int j = 0; j < sides; j++)
                {
                    int p0 = i * (sides + 1) + j, p1 = p0 + sides + 1;
                    tris.Add(p0); tris.Add(p1); tris.Add(p0 + 1);
                    tris.Add(p1); tris.Add(p1 + 1); tris.Add(p0 + 1);
                }
            var m = new Mesh { name = "ChainLink" };
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>The great rusty chain: it swings out from foi's hand and whips twice round, felling all within reach.</summary>
        private IEnumerator ChainWhip()
        {
            powerBusy = true;
            if (chainMat == null) chainMat = MaterialFactory.CreateSurface(new Color(0.5f, 0.32f, 0.2f), new Color(0.1f, 0.04f, 0.02f), 0.6f, 0.55f); // rusty iron, a little shine
            var root = new GameObject("Chain").transform;
            powerThing = root.gameObject;
            // Real chain: oval iron rings, each turned a quarter to the last so they hang through one another.
            var links = new List<Transform>();
            const int count = 13;
            if (linkMesh == null) linkMesh = LinkMesh(0.21f, 0.12f, 0.048f);
            for (int i = 0; i < count; i++)
            {
                var link = new GameObject("Link").transform;
                link.SetParent(root, false);
                link.gameObject.AddComponent<MeshFilter>().sharedMesh = linkMesh;
                var mr = link.gameObject.AddComponent<MeshRenderer>();
                mr.sharedMaterial = chainMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                link.localScale = Vector3.one * (i == count - 1 ? 1.5f : 1.1f); // a heavy last ring
                links.Add(link);
            }
            PowerSlow(0.5f, 0.35f);
            cameraRig.Punch(0.8f);
            AudioManager.PlaySfx(Sfx.Blocked, 1f, 0.5f);
            var hit = new HashSet<GridPos>();
            float angle0 = robot.transform.eulerAngles.y;
            const float spin = 1.1f;
            float nextStrike = 0f;
            for (float t = 0f; t < spin; t += Time.deltaTime)
            {
                while (State == GameState.Paused) yield return null;
                if (!PowerFloor) break;
                float k = t / spin;
                float reach = Mathf.Lerp(0.8f, 3.2f, Mathf.Min(1f, k * 3f));
                float angle = angle0 + k * 720f;
                var c = robot.transform.position + Vector3.up * 0.8f; // from foi's hand
                var dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                for (int i = 0; i < count; i++)
                {
                    float f = (i + 1f) / count;
                    links[i].position = c + dir * reach * f + Vector3.up * Mathf.Sin(f * Mathf.PI) * 0.15f;
                    links[i].rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, i % 2 == 0 ? 0f : 90f);
                }
                if (t >= nextStrike)
                {
                    // Whatever the chain sweeps over this moment is struck (each enemy once a turn).
                    nextStrike = t + 0.05f;
                    if (k > 0.5f && hit.Count > 0 && Mathf.Repeat(angle - angle0, 360f) < 20f) hit.Clear();
                    foreach (var at in hunt.FightersAround(robot.Position, 3))
                    {
                        if (hit.Contains(at)) continue;
                        var to = GridView.ToWorld(at) - robot.transform.position;
                        to.y = 0f;
                        if (to.sqrMagnitude < 0.01f || Vector3.Angle(to, dir) > 24f || to.magnitude > reach + 0.6f) continue;
                        hit.Add(at);
                        hunt.Strike(at, PowerDamage(4f));
                        fx.Burst(GridView.ToWorld(at) + Vector3.up * 0.6f, new Color(1f, 0.7f, 0.4f), new Color(2.4f, 1.2f, 0.5f), 14, 4f);
                        cameraRig.Shake(0.3f);
                        AudioManager.PlaySfx(Sfx.Impact, 0.8f, 1.2f);
                    }
                }
                yield return null;
            }
            Destroy(root.gameObject);
            Shockwave.Create(robot.transform.position, 3.4f, new Color(0.9f, 0.6f, 0.35f));
            powerBusy = false;
        }
    }
}
