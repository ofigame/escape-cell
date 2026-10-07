using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Visual;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The arena fight, seen over the robot's shoulder (third person). Stepping into a monster's ring hands the camera
    /// to this view: the camera always sits behind the robot (never in front of it) and looks where the robot looks.
    /// Controls:
    /// - tap: the robot lunges and swings its hammer at the monster (one blow of ammo);
    /// - hold and drag left/right: circle around the monster, sidestepping;
    /// - short swipe down: a quick backstep, still facing the monster (dodges a blow);
    /// - hold down: the robot turns its back on the monster and runs; at the ring's edge the fight is left and the
    ///   isometric view comes back (to collect more blows).
    /// The monster attacks with telegraphed blows: a red slam circle around itself, a red shock wedge toward the robot
    /// and thrown rocks whose landing spot glows first. Every blow can be dodged by moving.
    /// </summary>
    public class ArenaFight : MonoBehaviour
    {
        // ---------- Tuning ----------
        private const float MeleeDistance = 1.05f;
        private const float StrafeSpeed = 2.6f;
        private const float RunSpeed = 3.4f;
        private const float BackstepDistance = 1.1f, BackstepTime = 0.22f;
        private const float LungeTime = 0.16f, RecoverTime = 0.24f, SwingTime = 0.28f;
        private const float HurtGrace = 1.1f;
        private const float CameraBack = 3.7f, CameraUp = 2.3f;

        /// <summary>A blow landed on the monster (damage in health points).</summary>
        public event Action<int> Struck;
        /// <summary>The monster hit the robot (weight: 1 = a full blow).</summary>
        public event Action<float> Hurt;
        /// <summary>The robot ran out of the ring at this point.</summary>
        public event Action<Vector3> Exited;
        /// <summary>A tap with no blows left.</summary>
        public event Action OutOfAmmo;

        public Func<int> Ammo;
        /// <summary>False while the game is paused or over: no input, no blows.</summary>
        public Func<bool> CanAct;
        public Action SpendAmmo;

        public bool Active { get; private set; }
        /// <summary>The robot can't be hurt right now (just hit, or in a backstep).</summary>
        public bool Invulnerable => graceLeft > 0f || state == State.Backstep;

        private enum State { Free, Lunge, Swing, Recover, Backstep, Flee }
        private enum AttackKind { Slam, Wedge, Rock }

        private class Attack
        {
            public AttackKind kind;
            public float t, duration;
            public Vector3 point;
            public float angle;
            public GameObject root;
            public Transform fill;
            public Material mat;
        }

        private Robot robot;
        private CameraRig rig;
        private FxSystem fx;
        private Func<bool> targetGone;
        private Action windup;
        private Vector3 centre;
        private float radius, difficulty;
        private int hammerLevel;
        private Transform heldHammer;
        private LineRenderer ring;

        private State state;
        private float stateT, fromDist, toDist, keepDist;
        private float graceLeft, attackTimer;
        private bool swingDone;
        private readonly List<Attack> attacks = new List<Attack>();

        // Camera
        private Vector3 camPos, camLook;
        private float blendIn;
        private Pose startPose;

        // Input
        private bool tracking;
        private Vector2 startPos;
        private float pressTime;
        private enum Gesture { None, Strafe, Down }
        private Gesture gesture;
        private float strafeInput;

        private Material redMat, redFillMat;

        public static ArenaFight Create(Robot robot, CameraRig rig, FxSystem fx)
        {
            var a = new GameObject("ArenaFight").AddComponent<ArenaFight>();
            a.robot = robot;
            a.rig = rig;
            a.fx = fx;
            a.redMat = MaterialFactory.CreateTransparent(new Color(1f, 0.25f, 0.2f, 0.28f), new Color(1.2f, 0.2f, 0.15f));
            a.redFillMat = MaterialFactory.CreateTransparent(new Color(1f, 0.3f, 0.2f, 0.45f), new Color(2f, 0.35f, 0.2f));
            return a;
        }

        private Vector3 RobotPos => robot.transform.position;
        private float Dist => Flat(RobotPos - centre).magnitude;
        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        private Vector3 ToMonster => Flat(centre - RobotPos).normalized;

        /// <summary>
        /// Starts a fight: the robot is at its current spot inside the ring (radius <paramref name="ringRadius"/>
        /// around <paramref name="arenaCentre"/>); <paramref name="hardness"/> 0-1 speeds the monster up.
        /// </summary>
        public void Begin(Func<bool> gone, Action windUp, Vector3 arenaCentre, float ringRadius, float hardness, int hammer, LineRenderer arenaRing)
        {
            targetGone = gone;
            windup = windUp;
            centre = Flat(arenaCentre) + Vector3.up * arenaCentre.y;
            radius = ringRadius;
            difficulty = Mathf.Clamp01(hardness);
            hammerLevel = hammer;
            ring = arenaRing;
            Active = true;
            state = State.Free;
            graceLeft = 0.8f; // a moment to get one's bearings
            attackTimer = Mathf.Lerp(1.8f, 1.1f, difficulty);
            tracking = false;
            gesture = Gesture.None;
            strafeInput = 0f;

            robot.EnterArena();
            // Start just inside the ring, on the side the robot came from.
            var dir = Flat(RobotPos - centre);
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            float d = Mathf.Clamp(dir.magnitude, MeleeDistance + 0.6f, radius - 0.35f);
            robot.ArenaPlace(Ground(centre + dir.normalized * d), -dir, snapTurn: true);
            keepDist = d;

            if (heldHammer != null) Destroy(heldHammer.gameObject);
            heldHammer = HammerModels.Held(robot.Visual, hammerLevel);

            startPose = rig.CurrentPose;
            blendIn = 0f;
            camPos = DesiredCamPos(ToMonster);
            camLook = centre + Vector3.up * 0.9f;
        }

        /// <summary>Ends the fight (the game puts the robot back on the grid and gives the camera back).</summary>
        public void End()
        {
            if (!Active) return;
            Active = false;
            robot.ExitArena();
            if (heldHammer != null) Destroy(heldHammer.gameObject);
            heldHammer = null;
            foreach (var a in attacks) Destroy(a.root);
            attacks.Clear();
            rig.EndChase();
        }

        /// <summary>The robot is thrown out of the ring (one-hit levels: a blow knocks it out instead of killing it).</summary>
        public void Eject()
        {
            if (!Active) return;
            var at = RobotPos;
            Exited?.Invoke(at);
        }

        private Vector3 Ground(Vector3 p) => new Vector3(p.x, centre.y, p.z);

        private void Update()
        {
            if (!Active) return;
            if (CanAct != null && !CanAct())
            {
                tracking = false;
                strafeInput = 0f;
                return;
            }
            float dt = Time.deltaTime;
            if (graceLeft > 0f) graceLeft -= dt;
            ReadInput();
            UpdateMovement(dt);
            if (!Active) return;
            UpdateAttacks(dt);
        }

        // ---------- Input ----------

        private void ReadInput()
        {
            float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
            bool keyStrafe = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                float k = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
                if (k != 0f) { strafeInput = k; keyStrafe = true; }
                if (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) TryAttack();
                if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) { pressTime = Time.unscaledTime; }
                if ((kb.sKey.isPressed || kb.downArrowKey.isPressed) && Time.unscaledTime - pressTime > 0.35f) StartFlee();
                if ((kb.sKey.wasReleasedThisFrame || kb.downArrowKey.wasReleasedThisFrame))
                {
                    if (state == State.Flee) StopFlee();
                    else Backstep();
                }
            }
#endif
            InputReader.ReadPointer(out bool pressed, out Vector2 pos);
            if (pressed && !tracking)
            {
                tracking = true;
                startPos = pos;
                pressTime = Time.unscaledTime;
                gesture = Gesture.None;
            }
            else if (pressed && tracking)
            {
                var d = (pos - startPos) / dpi;
                if (gesture == Gesture.None)
                {
                    if (Mathf.Abs(d.x) > 0.1f && Mathf.Abs(d.x) > Mathf.Abs(d.y)) gesture = Gesture.Strafe;
                    else if (d.y < -0.14f) gesture = Gesture.Down;
                }
                if (gesture == Gesture.Strafe) strafeInput = Mathf.Clamp(d.x / 0.45f, -1f, 1f);
                if (gesture == Gesture.Down && Time.unscaledTime - pressTime > 0.35f) StartFlee();
            }
            else if (!pressed && tracking)
            {
                tracking = false;
                var d = (pos - startPos) / dpi;
                float held = Time.unscaledTime - pressTime;
                if (gesture == Gesture.None && held < 0.35f && d.magnitude < 0.12f) TryAttack();
                else if (gesture == Gesture.Down)
                {
                    if (state == State.Flee) StopFlee();
                    else Backstep();
                }
                gesture = Gesture.None;
                if (!keyStrafe) strafeInput = 0f;
            }
            else if (!keyStrafe && !tracking) strafeInput = 0f;
        }

        private void TryAttack()
        {
            if (state != State.Free) return;
            if (Ammo == null || Ammo() <= 0)
            {
                OutOfAmmo?.Invoke();
                return;
            }
            SpendAmmo?.Invoke();
            keepDist = Dist;
            Go(State.Lunge, Dist, MeleeDistance);
        }

        private void Backstep()
        {
            if (state != State.Free) return;
            Go(State.Backstep, Dist, Mathf.Min(radius - 0.25f, Dist + BackstepDistance));
            AudioManager.PlaySfx(Sfx.Hop, 0.6f, 1.3f);
        }

        private void StartFlee()
        {
            if (state != State.Free) return;
            state = State.Flee;
            stateT = 0f;
        }

        private void StopFlee()
        {
            if (state == State.Flee) state = State.Free;
        }

        private void Go(State s, float from, float to)
        {
            state = s;
            stateT = 0f;
            fromDist = from;
            toDist = to;
            swingDone = false;
        }

        // ---------- Movement ----------

        private void UpdateMovement(float dt)
        {
            var dir = Flat(RobotPos - centre);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.back;
            var outward = dir.normalized;
            float d = dir.magnitude;
            stateT += dt;

            switch (state)
            {
                case State.Free:
                {
                    if (Mathf.Abs(strafeInput) > 0.05f)
                    {
                        // Sidestep around the monster: along the robot's right, then back onto the same circle.
                        var right = Vector3.Cross(Vector3.up, -outward);
                        var p = Flat(RobotPos - centre) + right * strafeInput * StrafeSpeed * dt;
                        p = p.normalized * Mathf.Clamp(d, MeleeDistance + 0.3f, radius - 0.2f);
                        robot.ArenaPlace(Ground(centre + p), -p);
                        robot.ArenaBob(Mathf.Abs(strafeInput));
                    }
                    else robot.ArenaPlace(RobotPos, -outward);
                    break;
                }
                case State.Lunge:
                {
                    float k = Mathf.Clamp01(stateT / LungeTime);
                    robot.ArenaPlace(Ground(centre + outward * Mathf.Lerp(fromDist, toDist, k * k)), -outward);
                    if (k >= 1f)
                    {
                        // The swing: the hammer arcs down onto the monster.
                        ThunderHammer.Swing(RobotPos, centre, hammerLevel);
                        if (heldHammer != null) heldHammer.gameObject.SetActive(false);
                        state = State.Swing;
                        stateT = 0f;
                    }
                    break;
                }
                case State.Swing:
                    if (!swingDone && stateT >= SwingTime)
                    {
                        swingDone = true;
                        if (heldHammer != null) heldHammer.gameObject.SetActive(true);
                        rig.Shake(0.7f);
                        Struck?.Invoke(Data.Weapons.DamageAt(hammerLevel));
                        if (!Active) return;
                        Go(State.Recover, Dist, Mathf.Max(keepDist, MeleeDistance + 0.7f));
                    }
                    break;
                case State.Recover:
                case State.Backstep:
                {
                    float time = state == State.Recover ? RecoverTime : BackstepTime;
                    float k = Mathf.Clamp01(stateT / time);
                    float e = 1f - (1f - k) * (1f - k);
                    robot.ArenaPlace(Ground(centre + outward * Mathf.Lerp(fromDist, toDist, e)), -outward);
                    if (k >= 1f) state = State.Free;
                    break;
                }
                case State.Flee:
                {
                    // Back turned on the monster, running out; the camera swings behind.
                    float nd = d + RunSpeed * dt;
                    robot.ArenaPlace(Ground(centre + outward * nd), outward);
                    robot.ArenaBob(1f);
                    if (nd >= radius + 0.3f)
                    {
                        Exited?.Invoke(RobotPos);
                        return;
                    }
                    break;
                }
            }
        }

        // ---------- Monster attacks ----------

        private void UpdateAttacks(float dt)
        {
            if (targetGone != null && targetGone()) return;
            attackTimer -= dt;
            if (attackTimer <= 0f && attacks.Count < (difficulty > 0.5f ? 2 : 1))
            {
                attackTimer = Mathf.Lerp(2.6f, 1.4f, difficulty) + UnityEngine.Random.Range(0f, 0.6f);
                StartAttack();
            }

            for (int i = attacks.Count - 1; i >= 0; i--)
            {
                var a = attacks[i];
                a.t += dt;
                float k = Mathf.Clamp01(a.t / a.duration);
                if (a.fill != null) a.fill.localScale = new Vector3(k, 1f, k);
                MaterialFactory.SetColors(a.mat, new Color(1f, 0.25f, 0.2f, 0.22f + 0.18f * Mathf.Sin(a.t * 18f)), new Color(1.2f, 0.2f, 0.15f));
                if (k < 1f) continue;
                Resolve(a);
                Destroy(a.root);
                attacks.RemoveAt(i);
                if (!Active) return;
            }
        }

        private void StartAttack()
        {
            float d = Dist;
            float r = UnityEngine.Random.value;
            AttackKind kind = d < 1.9f && r < 0.55f ? AttackKind.Slam : r < 0.75f ? AttackKind.Wedge : AttackKind.Rock;
            var a = new Attack
            {
                kind = kind,
                duration = Mathf.Lerp(1.15f, 0.75f, difficulty) * (kind == AttackKind.Rock ? 1.1f : 1f),
                root = new GameObject("Attack " + kind),
                mat = MaterialFactory.CreateTransparent(new Color(1f, 0.25f, 0.2f, 0.3f), new Color(1.2f, 0.2f, 0.15f))
            };
            var y = centre.y + 0.02f;
            switch (kind)
            {
                case AttackKind.Slam:
                {
                    a.point = centre;
                    float rr = 1.75f;
                    Disc(a, new Vector3(centre.x, y, centre.z), rr);
                    windup?.Invoke();
                    break;
                }
                case AttackKind.Wedge:
                {
                    var to = Flat(RobotPos - centre);
                    a.angle = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    a.root.transform.position = new Vector3(centre.x, y, centre.z);
                    a.root.transform.rotation = Quaternion.Euler(0f, a.angle, 0f);
                    var mesh = WedgeMesh(radius + 0.4f, 46f);
                    AddMesh(a.root.transform, "Outline", mesh, a.mat);
                    var fill = new GameObject("Fill").transform;
                    fill.SetParent(a.root.transform, false);
                    fill.localPosition = Vector3.up * 0.005f;
                    AddMesh(fill, "Fill", mesh, redFillMat);
                    a.fill = fill;
                    break;
                }
                default:
                {
                    // Aimed a little ahead of where the robot is heading.
                    var lead = Flat(RobotPos - centre);
                    var right = Vector3.Cross(Vector3.up, -lead.normalized);
                    a.point = Ground(RobotPos + right * strafeInput * 0.6f);
                    Disc(a, new Vector3(a.point.x, y, a.point.z), 0.62f);
                    break;
                }
            }
        }

        private void Disc(Attack a, Vector3 at, float r)
        {
            a.root.transform.position = at;
            Shapes.Primitive(PrimitiveType.Cylinder, "Outline", a.root.transform, Vector3.zero, new Vector3(r * 2f, 0.004f, r * 2f), a.mat);
            var fill = new GameObject("Fill").transform;
            fill.SetParent(a.root.transform, false);
            fill.localPosition = Vector3.up * 0.006f;
            Shapes.Primitive(PrimitiveType.Cylinder, "Fill", fill, Vector3.zero, new Vector3(r * 2f, 0.004f, r * 2f), redFillMat);
            fill.localScale = new Vector3(0f, 1f, 0f);
            a.fill = fill;
        }

        private void Resolve(Attack a)
        {
            bool hit;
            switch (a.kind)
            {
                case AttackKind.Slam:
                    hit = Dist < 1.75f;
                    fx.Dust(centre + Vector3.up * 0.1f, new Color(0.9f, 0.85f, 0.8f), 30, 5f);
                    rig.Shake(0.8f);
                    AudioManager.PlaySfx(Sfx.Impact, 0.9f, 0.6f);
                    break;
                case AttackKind.Wedge:
                {
                    var to = Flat(RobotPos - centre);
                    float ang = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    hit = Mathf.Abs(Mathf.DeltaAngle(ang, a.angle)) < 25f && to.magnitude < radius + 0.4f;
                    fx.Burst(centre + Quaternion.Euler(0f, a.angle, 0f) * Vector3.forward * 1.5f + Vector3.up * 0.3f, new Color(1f, 0.5f, 0.3f), new Color(2.4f, 0.8f, 0.3f), 26, 6f);
                    rig.Shake(0.6f);
                    AudioManager.PlaySfx(Sfx.Blocked, 0.9f, 0.6f);
                    break;
                }
                default:
                    hit = Flat(RobotPos - a.point).magnitude < 0.68f;
                    fx.Burst(a.point + Vector3.up * 0.2f, new Color(0.6f, 0.5f, 0.45f), new Color(0.8f, 0.6f, 0.4f), 22, 4f);
                    rig.Shake(0.5f);
                    AudioManager.PlaySfx(Sfx.Impact, 0.7f, 0.9f);
                    break;
            }
            if (!hit || Invulnerable || robot.IsShielded) return;
            graceLeft = HurtGrace;
            float weight = a.kind == AttackKind.Slam ? 1f : a.kind == AttackKind.Wedge ? 0.85f : 0.7f;
            Hurt?.Invoke(weight);
            if (!Active) return;
            // Knocked back a little (never out of the ring by a hit).
            var outward = Flat(RobotPos - centre).normalized;
            robot.ArenaPlace(Ground(centre + outward * Mathf.Min(radius - 0.25f, Dist + 0.6f)), -outward);
            if (state == State.Lunge || state == State.Recover) state = State.Free;
        }

        // ---------- Camera ----------

        /// <summary>Always behind the robot: looking at the monster while fighting, along the run while fleeing.</summary>
        private Vector3 DesiredCamPos(Vector3 forward) => RobotPos - forward * CameraBack + Vector3.up * CameraUp;

        private void LateUpdate()
        {
            if (!Active) return;
            float dt = Time.deltaTime;
            bool fleeing = state == State.Flee;
            var forward = fleeing ? Flat(RobotPos - centre).normalized : ToMonster;
            var wantPos = DesiredCamPos(forward);
            var wantLook = fleeing ? RobotPos + forward * 3f + Vector3.up * 0.6f : Vector3.Lerp(RobotPos, centre, 0.75f) + Vector3.up * 0.8f;
            float follow = 1f - Mathf.Exp(-dt * (fleeing ? 5f : 9f));
            camPos = Vector3.Lerp(camPos, wantPos, follow);
            camLook = Vector3.Lerp(camLook, wantLook, follow);
            var rot = Quaternion.LookRotation(camLook - camPos);

            // The hand-over from the isometric view eases in over half a second.
            blendIn = Mathf.Min(1f, blendIn + dt * 2.2f);
            float e = blendIn * blendIn * (3f - 2f * blendIn);
            rig.Chase(Vector3.Lerp(startPose.position, camPos, e), Quaternion.Slerp(startPose.rotation, rot, e), Mathf.Lerp(40f, 62f, e));
        }

        // ---------- Meshes ----------

        private static Mesh WedgeMesh(float length, float degrees)
        {
            const int steps = 12;
            var verts = new Vector3[steps + 2];
            var tris = new int[steps * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(-degrees * 0.5f, degrees * 0.5f, i / (float)steps) * Mathf.Deg2Rad;
                verts[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * length;
            }
            for (int i = 0; i < steps; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }
            var mesh = new Mesh { vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void AddMesh(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
