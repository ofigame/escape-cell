using System;
using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A tiny 3D stage far away from the level, filmed by its own camera into a texture the mission briefing shows in
    /// a big card: the Thunder Hammer turning, the robot swinging it at the monster, the princess in her ice, a key and
    /// the door... Each step of the briefing builds its props here and plays a short looping animation, so the player
    /// sees what to do instead of only reading it.
    /// </summary>
    public class BriefingStage : MonoBehaviour
    {
        private static readonly Vector3 Origin = new Vector3(5000f, 0f, 5000f);
        // The same corner the game's camera looks from, so every model faces the viewer like on the floor.
        private static readonly Vector3 ViewDir = new Vector3(-1f, 0.95f, -1f).normalized;
        private static readonly Vector3 Right = new Vector3(1f, 0f, -1f).normalized;

        public RenderTexture Texture { get; private set; }

        private Camera cam;
        private Transform props;
        private FxSystem fx;
        private Func<Transform, GameObject> robotLook;

        // The step on show and its moving parts.
        private BriefShot shot;
        private float t, loopT;
        private int step;
        private Transform robot, held;
        private Transform block;
        private Thief thiefProp;
        private Buddy buddyProp;
        private Monster monster;
        private QuestGoal goal;
        private ExitPortal door;
        private readonly List<Transform> bits = new List<Transform>();
        private readonly List<Material> tileMats = new List<Material>();
        private Monster.Kind monsterKind;
        private Color tint;
        private QuestKind questKind;
        private int hits;

        public static BriefingStage Create(FxSystem fx, Func<Transform, GameObject> robotLook)
        {
            var go = new GameObject("BriefingStage");
            go.transform.position = Origin;
            var stage = go.AddComponent<BriefingStage>();
            stage.fx = fx;
            stage.robotLook = robotLook;
            stage.Build();
            return stage;
        }

        private void Build()
        {
            Texture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Briefing" };
            var camGo = new GameObject("BriefingCamera");
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 26f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 40f;
            cam.targetTexture = Texture;
            cam.enabled = false;
            props = new GameObject("Props").transform;
            props.SetParent(transform, false);
            Aim(1f);
        }

        /// <summary>Points the camera at the stage from the game's corner, <paramref name="zoom"/> 1 = the default distance.</summary>
        private void Aim(float zoom, float lift = 0.7f)
        {
            var focus = Origin + Vector3.up * lift;
            cam.transform.position = focus + ViewDir * (10.5f * zoom);
            cam.transform.LookAt(focus);
        }

        /// <summary>Sets what the monster and quest steps look like for this level.</summary>
        public void Setup(Monster.Kind kind, Color monsterTint, QuestKind quest, int monsterHits)
        {
            monsterKind = kind;
            tint = monsterTint;
            questKind = quest;
            hits = Mathf.Max(2, monsterHits);
        }

        public void SetVisible(bool on)
        {
            cam.enabled = on;
            if (!on) Clear();
        }

        public void Show(BriefShot newShot)
        {
            Clear();
            shot = newShot;
            t = 0f;
            loopT = 0f;
            step = 0;
            cam.enabled = true;
            Aim(1f);
            var floor = MaterialFactory.Create(new Color(0.2f, 0.18f, 0.36f), new Color(0.05f, 0.04f, 0.12f));
            var rim = MaterialFactory.Create(WorldTheme.Current.accent, WorldTheme.Current.accent * 1.4f);
            Shapes.Primitive(PrimitiveType.Cylinder, "Plinth", props, new Vector3(0f, -0.08f, 0f), new Vector3(3.6f, 0.08f, 3.6f), floor);
            Shapes.Primitive(PrimitiveType.Cylinder, "Rim", props, new Vector3(0f, -0.1f, 0f), new Vector3(3.75f, 0.06f, 3.75f), rim);

            switch (shot)
            {
                case BriefShot.Robot:
                    robot = MakeRobot(Vector3.zero, 2.2f);
                    break;
                case BriefShot.Hammer:
                    // The hammer animates its own scale, so a holder makes it bigger.
                    var big = new GameObject("Big").transform;
                    big.SetParent(props, false);
                    big.localScale = Vector3.one * 1.6f;
                    var hammer = ThunderHammer.Create(Origin);
                    hammer.transform.SetParent(big, false);
                    hammer.transform.localPosition = Vector3.zero;
                    bits.Add(hammer.transform);
                    Aim(0.72f, 1.25f);
                    break;
                case BriefShot.HammerHit:
                    robot = MakeRobot(-Right * 1.1f, 2.2f);
                    monster = MakeMonster(Right * 0.7f);
                    held = ThunderHammer.CreateHeld(robot).transform;
                    break;
                case BriefShot.Stomp:
                    monster = MakeMonster(Vector3.zero);
                    robot = MakeRobot(-Right * 1.15f, 2.2f);
                    Tiles(new Color(0.9f, 0.2f, 0.25f));
                    break;
                case BriefShot.Princess:
                    goal = QuestGoal.Create(QuestKind.Princess, Origin, fx);
                    goal.transform.SetParent(props, true);
                    Aim(0.85f, 1f);
                    break;
                case BriefShot.QuestItem:
                    for (int i = -1; i <= 1; i++)
                    {
                        var item = QuestItem.Create(questKind, Origin + Right * (i * 1.05f) + Vector3.up * (i == 0 ? 0.25f : 0f));
                        item.transform.SetParent(props, true);
                        bits.Add(item.transform);
                    }
                    Aim(0.9f, 0.8f);
                    break;
                case BriefShot.QuestGoal:
                    goal = QuestGoal.Create(questKind, Origin, fx);
                    goal.transform.SetParent(props, true);
                    goal.Ready();
                    Aim(0.9f, 1f);
                    break;
                case BriefShot.Key:
                    var key = KeyPickup.Create(Origin + Vector3.up * 0.3f);
                    key.transform.SetParent(props, true);
                    key.transform.localScale = Vector3.one * 1.8f;
                    Aim(0.7f, 0.9f);
                    break;
                case BriefShot.Door:
                    door = ExitPortal.Create(Origin + Right * 0.6f);
                    door.transform.SetParent(props, true);
                    robot = MakeRobot(-Right * 1.2f, 2.2f);
                    break;
                case BriefShot.Coins:
                    robot = MakeRobot(-Right * 1.4f, 2.2f);
                    var gold = MaterialFactory.Create(Palette.UiGold, Palette.UiGold * 1.5f);
                    for (int i = 0; i < 4; i++)
                    {
                        var c = Shapes.Primitive(PrimitiveType.Cylinder, "Coin", props, -Right * 0.6f + Right * (i * 0.65f) + Vector3.up * 0.45f,
                            new Vector3(0.42f, 0.04f, 0.42f), gold).transform;
                        bits.Add(c);
                    }
                    break;
                case BriefShot.Paint:
                    robot = MakeRobot(Vector3.zero, 2.2f);
                    Tiles(new Color(0.3f, 0.3f, 0.45f));
                    break;
                case BriefShot.Block:
                    Tiles(new Color(0.3f, 0.3f, 0.45f));
                    robot = MakeRobot(Vector3.zero, 2.2f);
                    block = Shapes.Rounded("Block", props, new Vector3(0f, 6f, 0f), new Vector3(0.84f, 0.76f, 0.84f), 0.2f,
                        MaterialFactory.Create(Palette.Block, Palette.BlockGlow * 0.5f)).transform;
                    block.gameObject.SetActive(false);
                    break;
                case BriefShot.Thief:
                    robot = MakeRobot(-Right * 1.3f, 2.2f);
                    thiefProp = MakeThief(Right * 0.7f);
                    break;
                case BriefShot.Escort:
                    door = ExitPortal.Create(Origin + Right * 1.3f);
                    door.transform.SetParent(props, true);
                    door.Open();
                    robot = MakeRobot(-Right * 0.3f, 2.2f);
                    buddyProp = Buddy.Create(Origin - Right * 1.6f);
                    buddyProp.transform.SetParent(props, true);
                    buddyProp.transform.localScale = Vector3.one * 1.9f;
                    break;
                case BriefShot.Warden:
                    var warden = WardenBoss.Create(Origin + Vector3.up * 0.6f, 3, cam.transform);
                    warden.transform.SetParent(props, true);
                    Aim(1.2f, 1.2f);
                    break;
            }
        }

        private Transform MakeRobot(Vector3 at, float scale)
        {
            var holder = new GameObject("Robot").transform;
            holder.SetParent(props, false);
            holder.localPosition = at;
            holder.localScale = Vector3.one * scale;
            robotLook?.Invoke(holder);
            // Facing the right-hand side of the stage (where the monster or the door stands), a little towards the viewer.
            holder.rotation = Quaternion.LookRotation(Right + new Vector3(ViewDir.x, 0f, ViewDir.z) * 0.9f);
            return holder;
        }

        private Thief MakeThief(Vector3 at)
        {
            var t = Thief.Create(Origin + at, new Color(0.6f, 0.6f, 0.68f));
            t.transform.SetParent(props, true);
            t.transform.localScale = Vector3.one * 1.6f;
            t.transform.rotation = Quaternion.LookRotation(new Vector3(ViewDir.x, 0f, ViewDir.z));
            return t;
        }

        private Monster MakeMonster(Vector3 at)
        {
            var m = Monster.Create(monsterKind, Origin + at, hits, tint, robot != null ? robot : cam.transform);
            m.transform.SetParent(props, true);
            return m;
        }

        /// <summary>A 3x3 patch of floor tiles around the stage's middle (red for a stomp, paintable otherwise).</summary>
        private void Tiles(Color color)
        {
            tileMats.Clear();
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                {
                    var m = MaterialFactory.Create(color, Color.black);
                    tileMats.Add(m);
                    var tile = Shapes.Rounded("Tile", props, new Vector3(x * 0.95f, 0.02f, z * 0.95f), new Vector3(0.88f, 0.08f, 0.88f), 0.04f, m).transform;
                    bits.Add(tile);
                }
        }

        private void Clear()
        {
            for (int i = props.childCount - 1; i >= 0; i--) Destroy(props.GetChild(i).gameObject);
            bits.Clear();
            tileMats.Clear();
            robot = held = null;
            block = null;
            thiefProp = null;
            buddyProp = null;
            monster = null;
            goal = null;
            door = null;
        }

        private void Update()
        {
            if (!cam.enabled) return;
            float dt = Time.unscaledDeltaTime;
            t += dt;
            loopT += dt;
            // The whole stage turns slowly back and forth, so the models read as 3D.
            props.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.6f) * 12f, 0f);

            switch (shot)
            {
                case BriefShot.Robot:
                    Bob(robot, 0f);
                    break;
                case BriefShot.HammerHit:
                    UpdateHit();
                    break;
                case BriefShot.Stomp:
                    UpdateStomp();
                    break;
                case BriefShot.Princess:
                    // The ice breaks, then it all starts over.
                    if (loopT > 2.2f && step == 0) { goal.Complete(); step = 1; }
                    if (loopT > 4.4f) Show(BriefShot.Princess);
                    break;
                case BriefShot.QuestItem:
                    for (int i = 0; i < bits.Count; i++)
                        if (bits[i] != null) bits[i].localPosition = new Vector3(bits[i].localPosition.x, 0.25f + Mathf.Sin(t * 2.5f + i) * 0.12f, bits[i].localPosition.z);
                    break;
                case BriefShot.QuestGoal:
                    if (loopT > 2.4f && step == 0) { goal.Complete(); step = 1; }
                    if (loopT > 4.6f) Show(BriefShot.QuestGoal);
                    break;
                case BriefShot.Door:
                    UpdateDoor();
                    break;
                case BriefShot.Coins:
                    UpdateCoins();
                    break;
                case BriefShot.Paint:
                    UpdatePaint();
                    break;
                case BriefShot.Block:
                    UpdateBlock();
                    break;
                case BriefShot.Thief:
                    UpdateThiefShot();
                    break;
                case BriefShot.Escort:
                    UpdateEscortShot();
                    break;
            }
        }

        private void Bob(Transform r, float phase)
        {
            if (r == null) return;
            var p = r.localPosition;
            r.localPosition = new Vector3(p.x, Mathf.Abs(Mathf.Sin((t + phase) * 3f)) * 0.08f, p.z);
        }

        /// <summary>The robot hops up to the monster, the hammer swings down with lightning, the health bar drops.</summary>
        private void UpdateHit()
        {
            const float Cycle = 2.2f;
            var home = -Right * 1.1f;
            var near = -Right * 0.35f;
            float k = loopT / Cycle;
            if (k < 0.35f) robot.localPosition = Vector3.Lerp(home, near, k / 0.35f) + Vector3.up * Mathf.Sin(k / 0.35f * Mathf.PI * 3f) * 0.1f;
            else if (k < 0.45f)
            {
                if (step == 0)
                {
                    step = 1;
                    if (held != null) held.gameObject.SetActive(false);
                    ThunderHammer.Swing(robot.position, monster.transform.position + Vector3.up * 0.2f);
                }
            }
            else if (k < 0.6f)
            {
                if (step == 1)
                {
                    step = 2;
                    int left = hits - 1 - (int)(t / Cycle) % hits;
                    if (left <= 0) left = hits;
                    monster.Hit(left);
                    fx?.Burst(monster.transform.position + Vector3.up * 0.8f, ThunderHammer.Electric, ThunderHammer.ElectricGlow, 30, 5f);
                }
                robot.localPosition = Vector3.Lerp(near, home, (k - 0.45f) / 0.15f);
            }
            else robot.localPosition = home;
            if (loopT >= Cycle)
            {
                loopT = 0f;
                step = 0;
                if (held != null) held.gameObject.SetActive(true);
            }
        }

        /// <summary>The tiles round the monster flash red, it stomps, and the robot standing there is pushed away.</summary>
        private void UpdateStomp()
        {
            const float Cycle = 2.6f;
            float k = loopT / Cycle;
            float flash = k > 0.15f && k < 0.45f ? 0.5f + 0.5f * Mathf.Sin(t * 24f) : 0f;
            foreach (var m in tileMats) MaterialFactory.SetColors(m, Color.Lerp(new Color(0.3f, 0.3f, 0.45f), new Color(1f, 0.25f, 0.25f), flash), new Color(1.6f, 0.2f, 0.2f) * flash);
            if (k >= 0.45f && step == 0)
            {
                step = 1;
                monster.Stomp();
            }
            var near = -Right * 1.0f;
            var far = -Right * 1.9f;
            if (k < 0.5f) robot.localPosition = near;
            else if (k < 0.65f) robot.localPosition = Vector3.Lerp(near, far, (k - 0.5f) / 0.15f) + Vector3.up * Mathf.Sin((k - 0.5f) / 0.15f * Mathf.PI) * 0.4f;
            else robot.localPosition = Vector3.Lerp(far, near, Mathf.Clamp01((k - 0.8f) / 0.2f));
            if (loopT >= Cycle) { loopT = 0f; step = 0; }
        }

        /// <summary>The door opens and the robot runs in.</summary>
        private void UpdateDoor()
        {
            const float Cycle = 3f;
            if (loopT > 0.6f && step == 0) { door.Open(); step = 1; }
            var home = -Right * 1.2f;
            float k = Mathf.Clamp01((loopT - 1.2f) / 1.2f);
            robot.localPosition = Vector3.Lerp(home, Right * 0.4f, k) + Vector3.up * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 4f)) * 0.1f;
            robot.localScale = Vector3.one * 2.2f * (1f - Mathf.Clamp01((k - 0.8f) / 0.2f));
            if (loopT >= Cycle) Show(BriefShot.Door);
        }

        /// <summary>The robot hops along the row of coins, each one popping as it is collected.</summary>
        private void UpdateCoins()
        {
            const float Cycle = 3.2f;
            float k = Mathf.Clamp01(loopT / (Cycle - 0.6f));
            var start = -Right * 1.4f;
            var end = Right * 1.5f;
            robot.localPosition = Vector3.Lerp(start, end, k) + Vector3.up * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 5f)) * 0.12f;
            for (int i = 0; i < bits.Count; i++)
            {
                var c = bits[i];
                bool taken = Vector3.Dot(robot.localPosition - c.localPosition, Right) > -0.1f;
                c.localRotation = Quaternion.Euler(90f, t * 200f, 0f);
                c.gameObject.SetActive(!taken);
            }
            if (loopT >= Cycle) { loopT = 0f; foreach (var c in bits) c.gameObject.SetActive(true); }
        }

        /// <summary>The robot hops tile to tile and every tile it lands on takes the paint.</summary>
        private void UpdatePaint()
        {
            const float StepTime = 0.4f;
            int[] path = { 4, 5, 2, 1, 0, 3, 6, 7, 8 }; // a spiral over the 3x3 patch
            int i = Mathf.Min(path.Length - 1, (int)(loopT / StepTime));
            var target = bits[path[i]].localPosition;
            float k = (loopT % StepTime) / StepTime;
            var from = i > 0 ? bits[path[i - 1]].localPosition : target;
            robot.localPosition = new Vector3(Mathf.Lerp(from.x, target.x, Mathf.Min(1f, k * 2f)), 0.06f + Mathf.Sin(Mathf.Min(1f, k * 2f) * Mathf.PI) * 0.25f, Mathf.Lerp(from.z, target.z, Mathf.Min(1f, k * 2f)));
            var paint = WorldTheme.Current.accent;
            for (int j = 0; j <= i; j++) MaterialFactory.SetColors(tileMats[path[j]], paint, paint * 0.8f);
            if (loopT > path.Length * StepTime + 0.8f)
            {
                loopT = 0f;
                foreach (var m in tileMats) MaterialFactory.SetColors(m, new Color(0.3f, 0.3f, 0.45f), Color.black);
            }
        }

        /// <summary>The robot's tile flashes red, it hops aside, and a block slams down where it stood.</summary>
        private void UpdateBlock()
        {
            const float Cycle = 2.8f;
            float k = loopT;
            var centre = Vector3.zero;
            var side = new Vector3(0.95f, 0f, 0f); // the next tile over
            float flash = k < 1f ? 0.5f + 0.5f * Mathf.Sin(t * 22f) : 0f;
            for (int i = 0; i < tileMats.Count; i++)
            {
                bool warn = i == 4; // the middle tile
                var c = warn ? Color.Lerp(new Color(0.3f, 0.3f, 0.45f), new Color(1f, 0.25f, 0.25f), flash) : new Color(0.3f, 0.3f, 0.45f);
                MaterialFactory.SetColors(tileMats[i], c, warn ? new Color(1.6f, 0.2f, 0.2f) * flash : Color.black);
            }
            // Hop aside at 0.55 s, back again at the end.
            if (k < 0.55f) robot.localPosition = centre;
            else if (k < 0.7f) robot.localPosition = Vector3.Lerp(centre, side, (k - 0.55f) / 0.15f) + Vector3.up * Mathf.Sin((k - 0.55f) / 0.15f * Mathf.PI) * 0.3f;
            else if (k < 2.45f) robot.localPosition = side;
            else robot.localPosition = Vector3.Lerp(side, centre, Mathf.Clamp01((k - 2.45f) / 0.15f)) + Vector3.up * Mathf.Sin(Mathf.Clamp01((k - 2.45f) / 0.15f) * Mathf.PI) * 0.3f;
            // The block falls at 0.85 s and lands at 1 s.
            block.gameObject.SetActive(k > 0.85f && k < 2.3f);
            float fall = Mathf.Clamp01((k - 0.85f) / 0.15f);
            block.localPosition = new Vector3(0f, Mathf.Lerp(6f, 0.42f, fall * fall), 0f);
            if (k >= 1f && step == 0)
            {
                step = 1;
                fx?.Dust(block.position, Palette.Block, 14, 3f);
            }
            if (loopT >= Cycle) { loopT = 0f; step = 0; }
        }

        /// <summary>The thief hops away, the robot corners it and bumps into it: dizzy stars, a coin pops out.</summary>
        private void UpdateThiefShot()
        {
            const float Cycle = 3.4f;
            float k = loopT;
            var tStart = Right * 0.7f;
            var tEnd = Right * 1.45f;
            if (step == 0) // first frame of the loop: off it goes
            {
                step = 1;
                thiefProp.HopTo(Origin + tEnd, 0.3f);
            }
            // The robot hops after it in three steps.
            float chase = Mathf.Clamp01((k - 0.5f) / 1.1f);
            var home = -Right * 1.3f;
            var near = Right * 0.6f;
            robot.localPosition = Vector3.Lerp(home, near, chase) + Vector3.up * Mathf.Abs(Mathf.Sin(chase * Mathf.PI * 3f)) * 0.15f;
            if (k >= 1.65f && step == 1)
            {
                step = 2;
                thiefProp.Catch(1.4f);
                fx?.Burst(thiefProp.transform.position + Vector3.up * 0.6f, Palette.UiGold, Palette.CoinGlow, 24, 4f);
            }
            if (loopT >= Cycle)
            {
                loopT = 0f;
                step = 0;
                robot.localPosition = home;
                Destroy(thiefProp.gameObject);
                thiefProp = MakeThief(tStart);
            }
        }

        /// <summary>The robot walks to the open door with Bip hopping a tile behind; Bip goes in and cheers.</summary>
        private void UpdateEscortShot()
        {
            const float Cycle = 3.6f;
            float k = Mathf.Clamp01(loopT / 2.4f);
            // The robot leads, then steps aside (away from the viewer) to let Bip through the door.
            var away = -new Vector3(ViewDir.x, 0f, ViewDir.z).normalized;
            var rFrom = -Right * 0.3f;
            var rTo = Right * 0.7f + away * 0.9f;
            robot.localPosition = Vector3.Lerp(rFrom, rTo, k) + Vector3.up * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 4f)) * 0.08f;
            var bFrom = -Right * 1.6f;
            var bTo = Right * 1.3f;
            float kb = Mathf.Clamp01((loopT - 0.3f) / 2.6f);
            if (!buddyProp.Hopping) buddyProp.Place(Origin + Vector3.Lerp(bFrom, bTo, kb) + Vector3.up * Mathf.Abs(Mathf.Sin(kb * Mathf.PI * 5f)) * 0.12f);
            if (kb >= 1f && step == 0)
            {
                step = 1;
                buddyProp.Cheer();
                fx?.Burst(buddyProp.transform.position + Vector3.up * 0.5f, Palette.UiGold, Palette.CoinGlow, 20, 4f);
            }
            if (loopT >= Cycle) Show(BriefShot.Escort);
        }

        private void OnDestroy()
        {
            if (Texture != null) Texture.Release();
        }
    }
}
