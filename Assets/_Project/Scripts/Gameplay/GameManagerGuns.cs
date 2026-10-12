using System.Collections;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The battle arenas' gun posts: on the north and south ramparts a stair runs along the wall (on obstacle tiles, so
    /// no robot can climb it) up to a mounted gun. foi steps on the glowing foot tile and climbs; up there it is out of
    /// reach, the camera sits behind the gun with a crosshair, the right thumb aims and the strike button fires (held:
    /// a burst). Each gun has 100 bullets; a warlord loses a tenth of its health to a bullet, any other robot half. The
    /// down button (or an empty gun) brings foi back to the foot of the stair.
    /// </summary>
    public partial class GameManager
    {
        private const int GunAmmo = 100;
        private const float GunRate = 0.11f, GunRange = 90f;

        private class GunPost
        {
            public GridPos foot, under;
            public Vector3 seat, inward, footWorld, stairLow, stairHigh;
            public int ammo = GunAmmo;
            public Transform pivot, barrel, pad;
            public Material padMat;
        }

        private readonly List<GunPost> gunPosts = new List<GunPost>();
        private Transform gunRoot;
        private GunPost manned;
        private bool gunBusy;
        private float gunYaw, gunPitch, gunNextShot, gunKick;
        private GridPos? leftPostFoot;

        private bool Gunning => manned != null && !gunBusy;

        /// <summary>The posts of this arena: found from its stair tiles (two rows of obstacle tiles on the walls).</summary>
        private void BuildGunPosts(Transform floorRoot)
        {
            ClearGunPosts();
            WireGunPostHits();
            gunRoot = new GameObject("GunPosts").transform;
            var rows = new Dictionary<int, List<int>>();
            foreach (var p in grid.AllPositions())
                if (grid.IsWall(p)) { if (!rows.TryGetValue(p.y, out var l)) rows[p.y] = l = new List<int>(); l.Add(p.x); }
            float midY = (level.stageBounds[1] + level.stageBounds[3]) * 0.5f;
            var st = BattleArenas.Style(arenaIndex);
            var stone = MaterialFactory.Create(Color.Lerp(st.wall, new Color(0.7f, 0.7f, 0.72f), 0.5f), Color.black);
            var stoneEdge = MaterialFactory.Create(Color.Lerp(st.wall, Color.black, 0.25f), Color.black);
            var metal = MaterialFactory.CreateSurface(new Color(0.22f, 0.23f, 0.27f), Color.black, 0.6f, 0.7f);
            var trim = MaterialFactory.Create(st.accent, st.accent * 1.6f);
            foreach (var kv in rows)
            {
                var xs = kv.Value;
                xs.Sort();
                bool north = kv.Key > midY;
                var outward = new GridPos(0, north ? 1 : -1);
                var top = new GridPos(north ? xs[xs.Count - 1] : xs[0], kv.Key);
                var foot = new GridPos(north ? xs[0] - 1 : xs[xs.Count - 1] + 1, kv.Key);
                if (!grid.IsFloor(foot)) continue;
                var post = new GunPost { foot = foot, under = new GridPos(top.x, top.y - outward.y), inward = new Vector3(0f, 0f, -outward.y) };
                float y0 = GridView.SurfaceY;
                float wallTop = FortressFloor.WallTop(top, outward).y;
                // The stair: steps along the wall over the four tiles, from the foot up to the wall top.
                int steps = 14;
                float x0 = GridView.ToWorld(new GridPos(north ? xs[0] : xs[xs.Count - 1], kv.Key)).x - (north ? 0.5f : -0.5f);
                float span = xs.Count;
                float z = GridView.ToWorld(foot).z + outward.y * 0.12f;
                for (int k = 0; k < steps; k++)
                {
                    float h = y0 + (k + 1f) / steps * (wallTop - y0);
                    float cx = x0 + (north ? 1f : -1f) * (k + 0.5f) / steps * span;
                    Shapes.Rounded("Step", floorRoot, new Vector3(cx, (y0 + h) * 0.5f, z), new Vector3(span / steps + 0.02f, h - y0, 0.95f), 0.02f, k % 2 == 0 ? stone : stoneEdge);
                }
                // A low parapet on the stair's open side.
                var railFrom = new Vector3(x0, y0, z - outward.y * 0.5f);
                var railTo = new Vector3(x0 + (north ? span : -span), wallTop, z - outward.y * 0.5f);
                var rail = Shapes.Rounded("Rail", floorRoot, (railFrom + railTo) * 0.5f + Vector3.up * 0.35f, new Vector3((railTo - railFrom).magnitude, 0.12f, 0.12f), 0.04f, trim);
                rail.transform.localRotation = Quaternion.Euler(0f, 0f, (north ? 1f : -1f) * Mathf.Atan2(wallTop - y0, span) * Mathf.Rad2Deg);
                // The landing on the wall top and the gun on it, facing the courtyard.
                var seat = FortressFloor.WallTop(top, outward);
                post.seat = seat;
                post.footWorld = GridView.ToWorld(foot) + Vector3.up * y0;
                post.stairLow = new Vector3(x0, y0, z);
                post.stairHigh = new Vector3(x0 + (north ? span : -span), wallTop, z);
                Shapes.Rounded("Landing", floorRoot, seat + Vector3.down * 0.06f, new Vector3(1.8f, 0.14f, 1.8f), 0.04f, stoneEdge);
                var gun = new GameObject("Gun").transform;
                gun.SetParent(gunRoot, false);
                gun.position = seat + post.inward * 0.55f;
                Shapes.Rounded("Mount", gun, new Vector3(0f, 0.3f, 0f), new Vector3(0.22f, 0.6f, 0.22f), 0.05f, metal);
                post.pivot = new GameObject("Pivot").transform;
                post.pivot.SetParent(gun, false);
                post.pivot.localPosition = new Vector3(0f, 0.68f, 0f);
                post.pivot.rotation = Quaternion.LookRotation(post.inward);
                Shapes.Rounded("Body", post.pivot, new Vector3(0f, 0f, 0.05f), new Vector3(0.42f, 0.32f, 0.7f), 0.08f, metal);
                Shapes.Rounded("Band", post.pivot, new Vector3(0f, 0.17f, 0.05f), new Vector3(0.44f, 0.04f, 0.5f), 0.02f, trim);
                Shapes.Rounded("Shield", post.pivot, new Vector3(0f, 0.02f, 0.42f), new Vector3(0.7f, 0.3f, 0.05f), 0.04f, metal);
                post.barrel = new GameObject("Barrel").transform;
                post.barrel.SetParent(post.pivot, false);
                foreach (float bx in new[] { -0.09f, 0.09f })
                    Shapes.Rounded("Tube", post.barrel, new Vector3(bx, 0.02f, 0.75f), new Vector3(0.08f, 0.08f, 0.95f), 0.035f, metal);
                // The glowing foot tile that sends foi up.
                post.padMat = MaterialFactory.Create(st.accent, st.accent * 2.4f);
                post.pad = Shapes.Rounded("Pad", gunRoot, post.footWorld + Vector3.up * 0.06f, new Vector3(0.86f, 0.05f, 0.86f), 0.1f, post.padMat).transform;
                gunPosts.Add(post);
            }
        }

        private bool gunHitsWired;

        /// <summary>The warlords' attack on foi up at a gun: they smash the rampart's foot under it.</summary>
        private void WireGunPostHits()
        {
            if (gunHitsWired) return;
            gunHitsWired = true;
            hunt.WallTarget = () => manned != null && !gunBusy ? manned.under : (GridPos?)null;
            hunt.AwayHit += share =>
            {
                if (manned == null || State != GameState.Playing) return;
                cameraRig.Shake(0.9f);
                hitShareOverride = share;
                bool alive = TryTakeHealthHit(robot.Position, crushed: true); // "crushed": foi stays where it is (no knock to a safe tile)
                hitShareOverride = -1f;
                if (alive) return;
                LeavePostNow();
                robot.Squash();
                AudioManager.PlaySfx(Sfx.Squash);
                Haptics.Death();
                Lose(Loc.T("lose.block"));
            };
        }

        private void ClearGunPosts()
        {
            LeavePostNow();
            gunPosts.Clear();
            if (gunRoot != null) Destroy(gunRoot.gameObject);
            gunRoot = null;
            leftPostFoot = null;
        }

        /// <summary>Each frame of an arena: the pads pulse, foi on a pad climbs, a manned gun aims and fires.</summary>
        private void UpdateGunPosts(float dt, Vector2 look, bool fire, bool climbDown)
        {
            if (gunPosts.Count == 0) return;
            foreach (var p in gunPosts)
            {
                bool live = p.ammo > 0;
                p.pad.localScale = new Vector3(1f, 1f, 1f) * (live ? 1f + Mathf.Sin(Time.time * 4f) * 0.06f : 0.8f);
                p.padMat.SetColor("_EmissionColor", live ? BattleArenas.Style(arenaIndex).accent * (1.6f + Mathf.Sin(Time.time * 4f)) : Color.black);
            }
            if (leftPostFoot.HasValue && robot.Position != leftPostFoot.Value && !robot.IsHopping) leftPostFoot = null;
            if (manned == null)
            {
                if (!robot.IsAlive || robot.IsHopping || robot.InArena) return;
                foreach (var p in gunPosts)
                    if (p.ammo > 0 && robot.Position == p.foot && leftPostFoot != p.foot) { StartCoroutine(Climb(p)); return; }
                return;
            }
            if (gunBusy) return;
            gunYaw += look.x * 200f;
            gunPitch = Mathf.Clamp(gunPitch - look.y * 120f, -5f, 62f);
            float face = Mathf.Atan2(manned.inward.x, manned.inward.z) * Mathf.Rad2Deg;
            gunYaw = face + Mathf.Clamp(Mathf.DeltaAngle(face, gunYaw), -80f, 80f);
            manned.pivot.rotation = Quaternion.Euler(gunPitch, gunYaw, 0f);
            gunKick = Mathf.MoveTowards(gunKick, 0f, dt * 2f);
            manned.barrel.localPosition = new Vector3(0f, 0f, -gunKick * 0.6f);
            if (climbDown) { StartCoroutine(ClimbDown()); return; }
            if (fire && Time.time >= gunNextShot && manned.ammo > 0) FireGun();
        }

        private void FireGun()
        {
            gunNextShot = Time.time + GunRate;
            manned.ammo--;
            gunKick = 0.25f;
            var cam = cameraRig.Cam;
            var ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            bool hit = hunt.Shoot(ray, GunRange, out var point);
            var muzzle = manned.pivot.position + manned.pivot.forward * 1.25f;
            Tracer.Create(muzzle, point, hit ? new Color(1f, 0.6f, 0.25f) : new Color(1f, 0.85f, 0.5f));
            fx.Burst(muzzle, new Color(1f, 0.8f, 0.4f), new Color(2.5f, 1.6f, 0.5f), 6, 2.5f);
            if (!hit) fx.Dust(point, new Color(0.6f, 0.55f, 0.5f), 4, 1.5f);
            AudioManager.PlaySfx(Sfx.Impact, 0.35f, 1.9f + Random.Range(-0.1f, 0.1f));
            cameraRig.Shake(0.06f);
            Haptics.Light();
            tps.SetGunMode(true, manned.ammo);
            if (manned.ammo <= 0) StartCoroutine(ClimbDown());
        }

        /// <summary>Up the stair to the gun: off the floor (no robot can reach foi up there).</summary>
        private IEnumerator Climb(GunPost p)
        {
            manned = p;
            gunBusy = true;
            robot.EnterArena();
            hunt.PlayerAway = true;
            AudioManager.PlaySfx(Sfx.Hop, 0.8f, 1.2f);
            yield return Walk(new[] { p.footWorld, p.stairLow, p.stairHigh, p.seat });
            gunYaw = Mathf.Atan2(p.inward.x, p.inward.z) * Mathf.Rad2Deg;
            gunPitch = 16f;
            robot.ArenaPlace(p.seat, p.inward, snapTurn: true);
            gunBusy = false;
            ShowFoi(false); // the view is the gunner's own, from just behind the gun
            tps.SetGunMode(true, p.ammo);
        }

        private Renderer[] foiRenderers;

        /// <summary>foi's body hidden while it mans a gun (the camera is where its head would be).</summary>
        private void ShowFoi(bool on)
        {
            if (!on) foiRenderers = robot.GetComponentsInChildren<Renderer>();
            if (foiRenderers == null) return;
            foreach (var r in foiRenderers) if (r != null) r.enabled = on;
            if (on) foiRenderers = null;
        }

        private IEnumerator ClimbDown()
        {
            if (manned == null || gunBusy) yield break;
            var p = manned;
            gunBusy = true;
            tps.SetGunMode(false, 0);
            ShowFoi(true);
            yield return Walk(new[] { p.seat, p.stairHigh, p.stairLow, p.footWorld });
            LeavePostNow();
        }

        /// <summary>foi walks a path (a short climb), facing the way it goes.</summary>
        private IEnumerator Walk(Vector3[] path)
        {
            for (int i = 0; i + 1 < path.Length; i++)
            {
                float len = Vector3.Distance(path[i], path[i + 1]);
                for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, len * 0.16f))
                {
                    if (manned == null) yield break;
                    var at = Vector3.Lerp(path[i], path[i + 1], t);
                    robot.ArenaPlace(at, path[i + 1] - path[i]);
                    robot.ArenaBob(1f);
                    yield return null;
                }
            }
        }

        /// <summary>Back on the floor at the stair's foot (or at once, if the arena ends while foi is up there).</summary>
        private void LeavePostNow()
        {
            if (manned == null) return;
            var p = manned;
            manned = null;
            gunBusy = false;
            ShowFoi(true);
            robot.ArenaPlace(p.footWorld, -p.inward, snapTurn: true);
            robot.ExitArena();
            if (hunt != null) hunt.PlayerAway = false;
            leftPostFoot = p.foot;
            if (tps != null) tps.SetGunMode(false, 0);
        }

        /// <summary>The camera behind the gun, looking where it aims (the crosshair is the middle of the screen).</summary>
        private void GunPose(out Vector3 pos, out Quaternion rot)
        {
            var aim = Quaternion.Euler(gunPitch, gunYaw, 0f) * Vector3.forward;
            var flat = Quaternion.Euler(0f, gunYaw, 0f) * Vector3.forward;
            var right = new Vector3(flat.z, 0f, -flat.x);
            pos = manned.seat + Vector3.up * 1.62f - flat * 0.3f - right * 0.32f; // over the gun's left shoulder: the gun sits low right, the crosshair clear
            rot = Quaternion.LookRotation(aim);
        }
    }

    /// <summary>A bullet's streak of light from the muzzle to where it struck, fading in a moment.</summary>
    public class Tracer : MonoBehaviour
    {
        private static readonly System.Collections.Generic.Dictionary<Color, Material> mats = new System.Collections.Generic.Dictionary<Color, Material>();
        private LineRenderer line;
        private float life = 0.09f, t;

        public static void Create(Vector3 from, Vector3 to, Color colour)
        {
            var go = new GameObject("Tracer");
            var tr = go.AddComponent<Tracer>();
            tr.line = go.AddComponent<LineRenderer>();
            tr.line.positionCount = 2;
            tr.line.SetPosition(0, from);
            tr.line.SetPosition(1, to);
            tr.line.widthMultiplier = 0.06f;
            if (!mats.TryGetValue(colour, out var m) || m == null) mats[colour] = m = MaterialFactory.Create(colour, colour * 3f);
            tr.line.sharedMaterial = m;
            tr.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Update()
        {
            t += Time.deltaTime;
            line.widthMultiplier = 0.06f * (1f - t / life);
            if (t >= life) Destroy(gameObject);
        }
    }
}
