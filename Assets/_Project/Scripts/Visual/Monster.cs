using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A monster guarding a floor from one fixed tile: a slime, a stone golem, a one-eyed cyclops or a ghost, in the
    /// floor's colours. A health bar floats over its head. It cannot be hurt by bumping into it bare-handed; the robot has
    /// to carry a magic orb into it. Now and then it stomps: the tiles around it flash, and a robot standing there is
    /// knocked back (it never kills).
    /// </summary>
    public class Monster : MonoBehaviour
    {
        public enum Kind { Slime, Golem, Cyclops, Ghost, Cage }

        private Kind kind;
        private Transform body, eye, pupil, barRoot, barFill;
        private Material skin, barMat, eyeMat;
        private Color skinColor;
        private int health, maxHealth;
        private float time, hitT = 1f, stompT = -1f, deadT = -1f;
        private Transform target;
        private int notchEvery = 1;

        public bool Dead => deadT >= 0f;

        /// <summary>The world the next vanG is made for (0-based; the game sets it as a floor starts), when its maker does
        /// not say: its model is chosen by it.</summary>
        public static int CurrentWorld = -1;
        /// <summary>The level being played (0-based; -1: none): its vanG's model, colour and size (see <see cref="VanGRig.LookFor"/>).</summary>
        public static int CurrentLevel = -1;
        /// <summary>This one's own size against the usual (a guard chief smaller, a world's monster bigger).</summary>
        private float sizeMul = 1f;
        private Color rigGlow = Color.black;

        /// <summary>The vanG's model, when it is one (its moves played on its bones).</summary>
        private VanGRig rig;
        public bool HasModel => rig != null;
        private int world = -1;

        /// <summary>The model's body (for the named guardians' props).</summary>
        public Transform Body => body;

        // Big enough to read as the boss of its floor, while still standing on a single tile.
        private const float Size = 1.45f;

        private static int LevelCatalogWorld(int level) => SquashBot.Data.LevelCatalog.WorldOf(level);

        public static Monster Create(Kind kind, Vector3 position, int health, Color tint, Transform lookAt, int notchEvery = 1, int world = -1)
        {
            var go = new GameObject("Monster " + kind);
            go.transform.position = position;
            var m = go.AddComponent<Monster>();
            m.kind = kind;
            m.health = m.maxHealth = health;
            m.notchEvery = Mathf.Max(1, notchEvery);
            m.target = lookAt;
            m.skinColor = tint;
            m.world = world >= 0 ? world : CurrentWorld;
            m.Build();
            return m;
        }

        private void Build()
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            skin = MaterialFactory.Create(skinColor, skinColor * 0.25f);
            var dark = MaterialFactory.Create(skinColor * 0.45f, Color.black);
            var white = MaterialFactory.Create(Color.white, new Color(0.5f, 0.5f, 0.5f));
            eyeMat = MaterialFactory.Create(new Color(1f, 0.95f, 0.5f), new Color(2f, 1.6f, 0.4f));
            var black = MaterialFactory.Create(new Color(0.08f, 0.06f, 0.12f), Color.black);
            // The vanGs made as models (four, by how far on the world is); the cage stays as it is.
            (int model, Color tint, float size, bool boss) look = CurrentLevel >= 0 && LevelCatalogWorld(CurrentLevel) == world ? VanGRig.LookFor(CurrentLevel) : (VanGRig.ModelFor(world), Color.white, 1f, false);
            if (kind != Kind.Cage && world >= 0) rig = VanGRig.Build(body, look.model, out var paint);
            if (rig != null)
            {
                skin = rig.GetComponentInChildren<Renderer>().sharedMaterial; // (it flashes white when hit)
                // its level's colour on its own picture (and a glow for a world's monster)
                rigGlow = look.boss ? look.tint * 0.12f : Color.black;
                MaterialFactory.SetColors(skin, look.tint, rigGlow);
                skinColor = look.tint;
                sizeMul = look.size;
            }
            else switch (kind)
            {
                case Kind.Slime:
                    Shapes.Primitive(PrimitiveType.Sphere, "Blob", body, new Vector3(0f, 0.42f, 0f), new Vector3(1.05f, 0.85f, 1.05f), skin);
                    Shapes.Primitive(PrimitiveType.Sphere, "Top", body, new Vector3(0.05f, 0.85f, 0f), new Vector3(0.5f, 0.4f, 0.5f), skin);
                    foreach (float s in new[] { -0.18f, 0.18f })
                    {
                        Shapes.Primitive(PrimitiveType.Sphere, "Eye", body, new Vector3(s, 0.6f, 0.42f), Vector3.one * 0.22f, white);
                        Shapes.Primitive(PrimitiveType.Sphere, "Pupil", body, new Vector3(s, 0.6f, 0.52f), Vector3.one * 0.1f, black);
                    }
                    Shapes.Rounded("Mouth", body, new Vector3(0f, 0.36f, 0.5f), new Vector3(0.36f, 0.08f, 0.04f), 0.03f, black);
                    break;
                case Kind.Golem:
                    Shapes.Rounded("Torso", body, new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.75f, 0.7f), 0.14f, skin);
                    Shapes.Rounded("Head", body, new Vector3(0f, 1.08f, 0.05f), new Vector3(0.55f, 0.42f, 0.5f), 0.1f, skin);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Shapes.Rounded("Arm", body, new Vector3(s * 0.6f, 0.45f, 0.05f), new Vector3(0.3f, 0.75f, 0.32f), 0.1f, dark);
                        Shapes.Rounded("Leg", body, new Vector3(s * 0.22f, 0.12f, 0f), new Vector3(0.28f, 0.26f, 0.3f), 0.08f, dark);
                        Shapes.Rounded("Eye", body, new Vector3(s * 0.12f, 1.1f, 0.31f), new Vector3(0.1f, 0.07f, 0.03f), 0.02f, eyeMat);
                    }
                    for (int i = 0; i < 3; i++)
                        Shapes.Rounded("Rune", body, new Vector3((i - 1) * 0.2f, 0.6f, 0.36f), new Vector3(0.08f, 0.22f, 0.02f), 0.02f, eyeMat);
                    break;
                case Kind.Cyclops:
                    Shapes.Primitive(PrimitiveType.Sphere, "Body", body, new Vector3(0f, 0.6f, 0f), new Vector3(0.95f, 1.05f, 0.9f), skin);
                    eye = Shapes.Primitive(PrimitiveType.Sphere, "Eye", body, new Vector3(0f, 0.78f, 0.36f), Vector3.one * 0.42f, white).transform;
                    pupil = Shapes.Primitive(PrimitiveType.Sphere, "Pupil", body, new Vector3(0f, 0.78f, 0.55f), Vector3.one * 0.18f, black).transform;
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Shapes.Rounded("Horn", body, new Vector3(s * 0.28f, 1.15f, 0f), new Vector3(0.12f, 0.3f, 0.12f), 0.05f, white).transform.localRotation = Quaternion.Euler(0f, 0f, -s * 25f);
                        Shapes.Rounded("Foot", body, new Vector3(s * 0.25f, 0.08f, 0.1f), new Vector3(0.3f, 0.16f, 0.4f), 0.06f, dark);
                    }
                    Shapes.Rounded("Mouth", body, new Vector3(0f, 0.42f, 0.42f), new Vector3(0.4f, 0.1f, 0.04f), 0.03f, black);
                    for (int i = 0; i < 4; i++)
                        Shapes.Rounded("Tooth", body, new Vector3((i - 1.5f) * 0.09f, 0.45f, 0.44f), new Vector3(0.05f, 0.07f, 0.02f), 0.01f, white);
                    break;
                case Kind.Cage:
                    // Not a monster at all: Princess Mira in vanG's electrified cage (the bars flash when hit).
                    skin = MaterialFactory.Create(new Color(1f, 0.8f, 0.35f), new Color(0.6f, 0.4f, 0.1f));
                    skinColor = new Color(1f, 0.8f, 0.35f);
                    PrincessMira.BuildCage(body, skin);
                    PrincessMira.Build(body, 0.95f).localPosition = new Vector3(0f, 0.1f, 0f);
                    break;
                default:
                    var ghost = MaterialFactory.CreateTransparent(new Color(skinColor.r, skinColor.g, skinColor.b, 0.75f), skinColor * 1.2f);
                    skin = ghost;
                    Shapes.Primitive(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 0.9f, 0f), new Vector3(0.85f, 0.8f, 0.8f), ghost);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Robe", body, new Vector3(0f, 0.5f, 0f), new Vector3(0.82f, 0.4f, 0.78f), ghost);
                    for (int i = 0; i < 5; i++)
                        Shapes.Primitive(PrimitiveType.Sphere, "Hem", body, new Vector3(Mathf.Cos(i * 1.256f) * 0.32f, 0.1f, Mathf.Sin(i * 1.256f) * 0.32f), Vector3.one * 0.26f, ghost);
                    foreach (float s in new[] { -0.15f, 0.15f })
                        Shapes.Primitive(PrimitiveType.Sphere, "Eye", body, new Vector3(s, 0.95f, 0.36f), new Vector3(0.14f, 0.2f, 0.1f), eyeMat);
                    break;
            }
            if (kind != Kind.Cage && rig == null) Menace(skinColor);
            body.localScale = Vector3.one * Size * sizeMul;

            // The health bar over its head.
            barRoot = new GameObject("HealthBar").transform;
            barRoot.SetParent(transform, false);
            barRoot.localPosition = new Vector3(0f, rig != null ? 4.4f * sizeMul : 2.3f, 0f); // (over the taller vanG models, as big as it is)
            barRoot.localScale = Vector3.one * 1.35f;
            Shapes.Rounded("Back", barRoot, Vector3.zero, new Vector3(1.24f, 0.2f, 0.04f), 0.06f, MaterialFactory.Create(new Color(0.1f, 0.08f, 0.16f), Color.black));
            barMat = MaterialFactory.Create(new Color(0.4f, 1f, 0.4f), new Color(0.4f, 1.6f, 0.4f));
            barFill = new GameObject("Fill").transform;
            barFill.SetParent(barRoot, false);
            barFill.localPosition = new Vector3(-0.56f, 0f, -0.03f);
            Shapes.Rounded("Fill", barFill, new Vector3(0.56f, 0f, 0f), new Vector3(1.12f, 0.12f, 0.03f), 0.05f, barMat);
            // Notches show how many hits it takes.
            // (at most ten parts: a strong vanG has thousands of points, and a mark for each was thousands of pieces turning
            // with the bar every frame)
            int step = Mathf.Max(notchEvery, Mathf.CeilToInt(maxHealth / 10f));
            var notchMat = MaterialFactory.Create(new Color(0.1f, 0.08f, 0.16f), Color.black);
            for (int i = step; i < maxHealth; i += step)
                Shapes.Rounded("Notch", barRoot, new Vector3(-0.56f + 1.12f * i / (float)maxHealth, 0f, -0.05f), new Vector3(0.025f, 0.16f, 0.02f), 0.01f, notchMat);
            RefreshBar();
        }

        private void RefreshBar()
        {
            float k = health / (float)maxHealth;
            barFill.localScale = new Vector3(Mathf.Max(0.001f, k), 1f, 1f);
            var c = Color.Lerp(new Color(1f, 0.3f, 0.3f), new Color(0.4f, 1f, 0.4f), k);
            MaterialFactory.SetColors(barMat, c, c * 1.6f);
        }

        /// <summary>
        /// Makes any monster look the part: glowing red eyes, a pair of curved horns, fangs under its face and a row of
        /// spikes down its back, all placed from the size of the body it was built with.
        /// </summary>
        private void Menace(Color skin)
        {
            MaterialFactory.SetColors(eyeMat, new Color(1f, 0.22f, 0.12f), new Color(3.2f, 0.45f, 0.2f));
            var bounds = new Bounds(body.position, Vector3.zero);
            foreach (var r in body.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var size = bounds.size;
            var c = bounds.center - body.position;
            float top = c.y + size.y * 0.5f, front = c.z + size.z * 0.5f, back = c.z - size.z * 0.5f, half = size.x * 0.5f;
            var horn = MaterialFactory.Create(new Color(0.92f, 0.86f, 0.72f), new Color(0.1f, 0.08f, 0.05f));
            var tip = MaterialFactory.Create(new Color(0.25f, 0.08f, 0.08f), Color.black);
            var fang = MaterialFactory.Create(Color.white, new Color(0.4f, 0.4f, 0.4f));
            var spike = MaterialFactory.Create(skin * 0.4f, Color.black);

            // Horns: three tapering pieces each, sweeping out and up from the top of the head.
            foreach (float s in new[] { -1f, 1f })
            {
                var basePos = new Vector3(s * half * 0.45f, top - 0.04f, c.z + size.z * 0.12f);
                for (int i = 0; i < 3; i++)
                {
                    float k = i / 2f;
                    float w = Mathf.Lerp(0.16f, 0.06f, k);
                    var at = basePos + new Vector3(s * (0.06f + 0.1f * i), 0.1f + 0.12f * i - 0.02f * i * i, -0.03f * i);
                    Shapes.Rounded("Horn", body, at, new Vector3(w, 0.16f, w), w * 0.4f, i == 2 ? tip : horn)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, -s * (25f + 18f * i));
                }
            }
            // Fangs: hanging at the front, a little under the middle of the face.
            float jaw = c.y + size.y * 0.05f;
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * half * 0.22f;
                float len = i == 0 || i == 3 ? 0.14f : 0.09f;
                Shapes.Rounded("Fang", body, new Vector3(x, jaw - len * 0.5f, front + 0.01f), new Vector3(0.05f, len, 0.04f), 0.015f, fang)
                    .transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            }
            // Spikes along the back.
            for (int i = 0; i < 4; i++)
            {
                float t = i / 3f;
                var at = new Vector3(0f, Mathf.Lerp(top - 0.05f, c.y + size.y * 0.15f, t), Mathf.Lerp(c.z - size.z * 0.1f, back + 0.02f, t));
                Shapes.Rounded("Spike", body, at, new Vector3(0.05f, 0.2f, 0.12f), 0.03f, spike)
                    .transform.localRotation = Quaternion.Euler(-35f - 25f * t, 0f, 0f);
            }
        }

        /// <summary>A magic hit: it reels, flashes white and its health bar drops.</summary>
        public void Hit(int remaining)
        {
            rig?.Jolt();
            health = Mathf.Max(0, remaining);
            hitT = 0f;
            RefreshBar();
        }

        /// <summary>Winds up and slams the ground (the game handles the knock-back).</summary>
        public void Stomp() { stompT = 0f; rig?.Slam(); }

        /// <summary>Rears back and sweeps forward (a swipe that sends a wave along the floor).</summary>
        public void Swipe() { swipeT = 0f; rig?.Sweep(); }

        /// <summary>Rises up with a boulder and hurls it.</summary>
        public void Throw() { throwT = 0f; rig?.Hurl(); }

        private float swipeT = -1f, throwT = -1f;
        private Quaternion facing = Quaternion.identity;
        private bool facingSet;

        public void Defeat()
        {
            health = 0;
            RefreshBar();
            deadT = 0f;
        }

        private void Update()
        {
            time += Time.deltaTime;
            var cam = Camera.main;
            if (cam != null) barRoot.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);

            // Face the robot.
            if (target != null && deadT < 0f)
            {
                var d = target.position - transform.position;
                d.y = 0f;
                if (!facingSet) { facing = body.rotation; facingSet = true; }
                if (d.sqrMagnitude > 0.01f)
                    facing = Quaternion.Slerp(facing, Quaternion.LookRotation(d), Time.deltaTime * 4f);
                // Attack poses lean the body: back then forward for a swipe, up and over for a throw.
                float lean = 0f;
                if (swipeT >= 0f)
                {
                    swipeT += Time.deltaTime;
                    lean = swipeT < 0.25f ? -20f * (swipeT / 0.25f) : Mathf.Lerp(30f, 0f, (swipeT - 0.25f) / 0.3f);
                    if (swipeT > 0.55f) swipeT = -1f;
                }
                if (throwT >= 0f)
                {
                    throwT += Time.deltaTime;
                    lean = throwT < 0.35f ? -25f * (throwT / 0.35f) : Mathf.Lerp(20f, 0f, (throwT - 0.35f) / 0.3f);
                    if (throwT > 0.65f) throwT = -1f;
                }
                body.rotation = facing * Quaternion.Euler(lean, 0f, 0f);
            }

            float breathe = 1f + Mathf.Sin(time * (kind == Kind.Slime ? 4f : 2f)) * (kind == Kind.Slime ? 0.06f : 0.025f);
            float sz = Size * sizeMul;
            var scale = new Vector3(sz / Mathf.Sqrt(breathe), sz * breathe, sz / Mathf.Sqrt(breathe));
            float y = kind == Kind.Ghost ? 0.15f + Mathf.Sin(time * 2f) * 0.1f : 0f;

            if (stompT >= 0f)
            {
                // Crouch, leap, slam.
                stompT += Time.deltaTime;
                if (stompT < 0.5f) scale.y *= 1f - stompT * 0.5f;
                else if (stompT < 0.75f) y += Mathf.Sin((stompT - 0.5f) / 0.25f * Mathf.PI) * 0.6f;
                else stompT = -1f;
            }

            if (hitT < 1f)
            {
                hitT = Mathf.Min(1f, hitT + Time.deltaTime * 2.5f);
                float flash = 1f - hitT;
                if (rig != null) MaterialFactory.SetColors(skin, Color.Lerp(skinColor, Color.white, flash), Color.Lerp(rigGlow, new Color(1.6f, 1.6f, 1.6f), flash)); // (back to its own colour after)
                else MaterialFactory.SetColors(skin, Color.Lerp(skinColor, Color.white, flash), Color.Lerp(skinColor * 0.25f, new Color(3f, 3f, 3f), flash));
                body.localPosition = Random.insideUnitSphere * 0.08f * flash;
            }
            else body.localPosition = Vector3.zero;

            if (deadT >= 0f)
            {
                deadT += Time.deltaTime;
                float k = Mathf.Clamp01(1f - deadT * 1.2f);
                scale *= k;
                y += deadT * 0.6f;
                body.Rotate(0f, Time.deltaTime * 720f, 0f, Space.World);
                barRoot.gameObject.SetActive(false);
                if (deadT > 1f) gameObject.SetActive(false);
            }
            body.localScale = scale;
            transform.GetChild(0).localPosition = new Vector3(body.localPosition.x, y, body.localPosition.z);

            if (pupil != null && target != null)
            {
                var local = body.InverseTransformPoint(target.position);
                pupil.localPosition = new Vector3(Mathf.Clamp(local.x * 0.05f, -0.08f, 0.08f), 0.78f, 0.55f);
            }
        }
    }
}
