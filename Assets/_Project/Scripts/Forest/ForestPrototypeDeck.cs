using System.Collections.Generic;
using SquashBot.Audio;
using UnityEngine;

namespace SquashBot.Forest
{
    /// <summary>
    /// The raised deck's hazards, growing with every leg: falling crates (a red mark first), sweeping logs, holes to
    /// jump (a fall costs health and returns the walker to where it last stood), rolling logs from the third leg and
    /// spike traps from the fourth (<see cref="UpdateSpikes"/>). Checkpoints at both stairs.
    /// </summary>
    public partial class ForestPrototype
    {
        private class Crate
        {
            public Vector3 spot;
            public float t;
            public GameObject warn, box;
            public bool landed;
        }

        private class Sweeper
        {
            public Vector3 centre;
            public float angle, speed, length;
            public Transform beam;
        }

        private readonly List<Crate> crates = new List<Crate>();
        private readonly List<Sweeper> sweepers = new List<Sweeper>();
        private readonly List<Transform> rollers = new List<Transform>();
        private float crateTimer = 1f, rollTimer = 2f;
        private Vector3 deckSafe = new Vector3(0f, ForestWorld.DeckHeight + 0.09f, ForestWorld.DeckStart + 1f);

        private void BuildSweepers()
        {
            var bark = Resources.Load<Material>("Forest/Bark_Pine");
            // More logs on every leg's (longer, wider) deck, swinging faster.
            int count = Mathf.Min(2 + (Leg - 1), 6);
            for (int i = 0; i < count; i++)
            {
                float z = Mathf.Lerp(ForestWorld.DeckStart + 10f, world.DeckEnd - 8f, i / (float)(count - 1));
                float speed = (i % 2 == 0 ? 1f : -1f) * (80f + i * 6f) * (1f + (Leg - 1) * 0.12f);
                float x = count > 3 ? (i % 2 == 0 ? -1f : 1f) * world.DeckHalfWidth * 0.35f : 0f;
                var s = new Sweeper { centre = new Vector3(x, ForestWorld.DeckHeight, z), speed = speed, length = world.DeckHalfWidth - 0.5f - Mathf.Abs(x) * 0.3f, angle = z * 7f };
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(post.GetComponent<Collider>());
                post.transform.SetParent(world.transform, false);
                post.transform.localPosition = s.centre + Vector3.up * 0.45f;
                post.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);
                post.GetComponent<MeshRenderer>().sharedMaterial = bark;
                var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(beam.GetComponent<Collider>());
                beam.transform.SetParent(world.transform, false);
                beam.transform.localScale = new Vector3(0.32f, s.length, 0.32f);
                beam.GetComponent<MeshRenderer>().sharedMaterial = bark;
                s.beam = beam.transform;
                sweepers.Add(s);
            }
        }

        /// <summary>Removes the deck's moving pieces (a new land, the end).</summary>
        private void ClearDeck()
        {
            foreach (var c in crates) { Destroy(c.warn); Destroy(c.box); }
            crates.Clear();
            foreach (var r in rollers) if (r != null) Destroy(r.gameObject);
            rollers.Clear();
            sweepers.Clear();
        }

        /// <summary>A slam smashes crates and rolling logs within its reach.</summary>
        private void ClearDeckHazardsNear(Vector3 centre, float radius)
        {
            for (int i = crates.Count - 1; i >= 0; i--)
            {
                var c = crates[i];
                if ((c.spot - centre).magnitude > radius || !c.landed) continue;
                fx.Dust(world.ToWorld(c.spot) + Vector3.up * 0.5f, new Color(0.55f, 0.42f, 0.28f), 14, 3f);
                Destroy(c.warn);
                Destroy(c.box);
                crates.RemoveAt(i);
            }
            for (int i = rollers.Count - 1; i >= 0; i--)
            {
                var r = rollers[i];
                if (Mathf.Abs(r.localPosition.z - centre.z) > radius || Mathf.Abs(r.localPosition.y - centre.y) > 2f) continue;
                fx.Dust(world.ToWorld(r.localPosition), new Color(0.55f, 0.42f, 0.28f), 14, 3f);
                Destroy(r.gameObject);
                rollers.RemoveAt(i);
            }
        }

        private void UpdateDeck(float dt)
        {
            bool onDeck = pos.y > ForestWorld.DeckHeight - 0.5f && pos.z > ForestWorld.DeckStart && pos.z < world.DeckEnd;
            if (pos.z > ForestWorld.StairsUpStart && checkpoint.z < ForestWorld.StairsUpStart) checkpoint = new Vector3(0f, 0f, ForestWorld.StairsUpStart - 2f);
            if (pos.z > world.StairsDownEnd && checkpoint.z < world.StairsDownEnd) checkpoint = new Vector3(0f, 0f, world.StairsDownEnd + 2f);
            checkpoint.y = world.TerrainY(checkpoint.x, checkpoint.z);

            // Holes in the deck: a fall costs health and puts the walker back where it last stood on the stone.
            bool overDeck = Mathf.Abs(pos.x) < world.DeckHalfWidth && pos.z > ForestWorld.DeckStart && pos.z < world.DeckEnd;
            if (onDeck && grounded && !InGap(pos.x, pos.z)) deckSafe = pos;
            if (overDeck && pos.y < ForestWorld.DeckHeight - 1.6f)
            {
                fx.Dust(world.ToWorld(pos), new Color(0.5f, 0.42f, 0.3f), 12, 2f);
                motor.Teleport(deckSafe, yaw);
                hurtLeft = 0f;
                Hurt(pos);
                motor.Teleport(deckSafe, yaw);
            }
            UpdateRollers(dt, onDeck);
            UpdateSpikes(dt, onDeck);

            // Falling crates: a red mark, then a crate drops onto it.
            if (onDeck)
            {
                crateTimer -= dt;
                if (crateTimer <= 0f)
                {
                    crateTimer = Random.Range(1.1f, 1.8f) / (1f + (Leg - 1) * 0.18f);
                    float edge = world.DeckHalfWidth - 0.7f;
                    var spot = new Vector3(Mathf.Clamp(pos.x + Random.Range(-2.5f, 2.5f), -edge, edge), ForestWorld.DeckHeight + 0.1f,
                        Mathf.Clamp(pos.z + Random.Range(1f, 6f), ForestWorld.DeckStart + 1f, world.DeckEnd - 1f));
                    var c = new Crate { spot = spot };
                    c.warn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(c.warn.GetComponent<Collider>());
                    c.warn.transform.SetParent(world.transform, false);
                    c.warn.transform.localPosition = spot + Vector3.up * 0.02f;
                    c.warn.transform.localScale = new Vector3(1.6f, 0.01f, 1.6f);
                    c.warn.GetComponent<MeshRenderer>().sharedMaterial = warnMat;
                    c.box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(c.box.GetComponent<Collider>());
                    c.box.transform.SetParent(world.transform, false);
                    c.box.transform.localScale = Vector3.one * 1.1f;
                    c.box.GetComponent<MeshRenderer>().sharedMaterial = crateMat;
                    c.box.SetActive(false);
                    crates.Add(c);
                }
            }
            for (int i = crates.Count - 1; i >= 0; i--)
            {
                var c = crates[i];
                c.t += dt;
                const float warn = 1.1f;
                if (c.t < warn)
                {
                    float pulse = 0.85f + 0.15f * Mathf.Sin(c.t * 20f);
                    c.warn.transform.localScale = new Vector3(1.6f * pulse, 0.01f, 1.6f * pulse);
                    if (c.t > warn - 0.35f)
                    {
                        c.box.SetActive(true);
                        float k = (c.t - (warn - 0.35f)) / 0.35f;
                        c.box.transform.localPosition = c.spot + Vector3.up * Mathf.Lerp(9f, 0.55f, k * k);
                    }
                    continue;
                }
                if (!c.landed)
                {
                    c.landed = true;
                    c.box.transform.localPosition = c.spot + Vector3.up * 0.55f;
                    Destroy(c.warn);
                    fx.Dust(world.ToWorld(c.spot) + Vector3.up * 0.1f, new Color(0.7f, 0.65f, 0.55f), 16, 3f);
                    rig.Shake(0.3f);
                    AudioManager.PlaySfx(Sfx.Impact, 0.6f, 1f);
                    var d = new Vector2(pos.x - c.spot.x, pos.z - c.spot.z);
                    if (d.magnitude < 0.95f && pos.y < c.spot.y + 1.2f) Hurt(c.spot);
                }
                if (c.t > warn + 2.2f)
                {
                    Destroy(c.box);
                    crates.RemoveAt(i);
                }
            }

            // Sweeping logs at knee height: jump them or slip past behind.
            foreach (var s in sweepers)
            {
                s.angle += s.speed * dt;
                var dir = Quaternion.Euler(0f, s.angle, 0f) * Vector3.forward;
                s.beam.localPosition = s.centre + Vector3.up * 0.45f + dir * (s.length * 0.5f + 0.2f);
                s.beam.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
                if (!onDeck || pos.y > ForestWorld.DeckHeight + 0.75f) continue;
                var rel = pos - s.centre;
                rel.y = 0f;
                float along = Vector3.Dot(rel, dir);
                float side = (rel - dir * along).magnitude;
                if (along > 0.2f && along < s.length + 0.4f && side < 0.45f) Hurt(pos - Vector3.Cross(Vector3.up, dir) * Mathf.Sign(s.speed));
            }
        }

        /// <summary>From the third leg on, logs roll down the deck towards the walker: jump them.</summary>
        private void UpdateRollers(float dt, bool onDeck)
        {
            if (Leg >= 3 && onDeck)
            {
                rollTimer -= dt;
                if (rollTimer <= 0f && pos.z < world.DeckEnd - 10f)
                {
                    rollTimer = Random.Range(3.2f, 4.6f) / (1f + (Leg - 3) * 0.15f);
                    var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(log.GetComponent<Collider>());
                    log.name = "RollingLog";
                    log.transform.SetParent(world.transform, false);
                    log.transform.localScale = new Vector3(0.75f, world.DeckHalfWidth - 0.4f, 0.75f);
                    log.transform.localPosition = new Vector3(0f, ForestWorld.DeckHeight + 0.46f, Mathf.Min(pos.z + 14f, world.DeckEnd - 0.5f));
                    log.GetComponent<MeshRenderer>().sharedMaterial = Resources.Load<Material>("Forest/Bark_Pine");
                    rollers.Add(log.transform);
                    AudioManager.PlaySfx(Sfx.Bump, 0.5f, 0.6f);
                }
            }
            float speed = 4.2f + (Leg - 3) * 0.4f;
            for (int i = rollers.Count - 1; i >= 0; i--)
            {
                var r = rollers[i];
                var p = r.localPosition;
                p.z -= speed * dt;
                r.localPosition = p;
                r.localRotation = Quaternion.Euler(-p.z / 0.375f * Mathf.Rad2Deg, 0f, 90f);
                if (onDeck && Mathf.Abs(pos.z - p.z) < 0.55f && pos.y < ForestWorld.DeckHeight + 0.75f) Hurt(pos + Vector3.forward);
                if (p.z < ForestWorld.DeckStart)
                {
                    fx.Dust(world.ToWorld(p), new Color(0.5f, 0.42f, 0.3f), 10, 2f);
                    Destroy(r.gameObject);
                    rollers.RemoveAt(i);
                }
            }
        }
    }
}
