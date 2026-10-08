using SquashBot.Audio;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Forest
{
    /// <summary>
    /// vanG's robots in the open land, more and more kinds with every leg: the guard (walks up and slams, a red ring
    /// shows where), from the second leg the charger (aims, a red lane shows its line, dashes, then stands dazed:
    /// strike it then for double damage) and from the third the thrower (keeps its distance and lobs rocks at a red
    /// mark). They wait where they stand until the walker comes near.
    /// </summary>
    public partial class ForestPrototype
    {
        private enum EnemyKind { Guard, Charger, Thrower }

        private class Enemy
        {
            public EnemyKind kind;
            public Transform root;
            public Vector3 pos, home, dashDir, target;
            public int hp = 3;
            public float windup = -1f, cooldown, flash, dash = -1f, dazed, phase;
            public GameObject ring, lane;
            public bool dead, awake;
            public GuardBot bot;
        }

        private const float WakeRange = 11f;

        private void BuildEnemies()
        {
            // The clearing's band, then robots along the way, and from the second leg a few guarding the passage.
            int clearing = Mathf.Min(2 + (Leg - 1) / 2, 4);
            var spots = new[] { new Vector2(-4f, 3f), new Vector2(4.5f, -2f), new Vector2(0.5f, 5.5f), new Vector2(-5f, -4f) };
            for (int i = 0; i < clearing; i++)
                AddEnemy(world.ClearingCentre + spots[i], i);
            int alongA = 1 + Leg / 2, alongB = 1 + (Leg - 1) / 2, guards = Leg >= 2 ? 1 + (Leg - 1) / 3 : 0;
            for (int i = 0; i < alongA; i++) AddAlongPath(Mathf.Lerp(28f, 68f, (i + 0.5f) / alongA), i + 1);
            float bFrom = world.StairsDownEnd + 8f, bTo = world.ClearingCentre.y - ForestWorld.ClearingRadius - 4f;
            for (int i = 0; i < alongB; i++) AddAlongPath(Mathf.Lerp(bFrom, bTo, (i + 0.5f) / alongB), i + 2);
            float gFrom = world.ClearingCentre.y + ForestWorld.ClearingRadius + 6f, gTo = world.TunnelZ - 8f;
            for (int i = 0; i < guards; i++) AddAlongPath(Mathf.Lerp(gFrom, gTo, (i + 0.5f) / guards), i + 3);
        }

        private void AddAlongPath(float z, int salt)
        {
            float side = (salt + Leg) % 2 == 0 ? -1f : 1f;
            float x = world.PathX(z) + side * UnityEngine.Random.Range(2f, 4.5f);
            AddEnemy(new Vector2(x, z), salt);
        }

        private EnemyKind PickKind(int salt)
        {
            int kinds = Leg >= 3 ? 3 : Leg >= 2 ? 2 : 1;
            return (EnemyKind)((salt + Leg) % kinds);
        }

        private void AddEnemy(Vector2 at, int salt)
        {
            var e = new Enemy { kind = PickKind(salt), phase = UnityEngine.Random.Range(0f, 6f) };
            e.pos = e.home = new Vector3(at.x, world.TerrainY(at.x, at.y), at.y);
            e.hp = e.kind == EnemyKind.Guard ? 3 : 2;
            e.root = new GameObject("Enemy " + e.kind).transform;
            e.root.SetParent(world.transform, false);
            var plates = e.kind == EnemyKind.Charger ? new Color(0.85f, 0.6f, 0.12f) : e.kind == EnemyKind.Thrower ? new Color(0.35f, 0.3f, 0.7f) : (Color?)null;
            e.bot = GuardBot.Build(e.root, plates);
            e.bot.transform.localScale = Vector3.one * (e.kind == EnemyKind.Charger ? 0.9f : e.kind == EnemyKind.Thrower ? 1.12f : 1f);
            e.ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(e.ring.GetComponent<Collider>());
            e.ring.transform.SetParent(world.transform, false);
            e.ring.GetComponent<MeshRenderer>().sharedMaterial = warnMat;
            e.ring.SetActive(false);
            if (e.kind == EnemyKind.Charger)
            {
                e.lane = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(e.lane.GetComponent<Collider>());
                e.lane.transform.SetParent(world.transform, false);
                e.lane.GetComponent<MeshRenderer>().sharedMaterial = warnMat;
                e.lane.SetActive(false);
            }
            enemies.Add(e);
        }

        /// <summary>A hammer blow lands on <paramref name="e"/> (<paramref name="to"/>: from the walker to it).</summary>
        private void HitEnemy(Enemy e, Vector3 to)
        {
            int damage = e.dazed > 0f ? 2 : 1;
            e.hp -= damage;
            e.flash = 1f;
            e.awake = true;
            e.bot.Flash();
            e.bot.SetRaise(0f);
            Shockwave.Create(world.ToWorld(e.pos), damage > 1 ? 1.8f : 1.2f, new Color(1f, 0.85f, 0.45f));
            rig.Punch(damage > 1 ? 0.9f : 0.6f);
            e.windup = -1f;
            e.dash = -1f;
            e.ring.SetActive(false);
            if (e.lane != null) e.lane.SetActive(false);
            e.pos += to.normalized * 1.2f;
            fx.Burst(world.ToWorld(e.pos) + Vector3.up * 0.6f, Palette.UiGold, Palette.CoinGlow, 24, 5f);
            rig.Shake(0.5f);
            AudioManager.PlaySfx(Sfx.Blocked, 1f, damage > 1 ? 1.1f : 0.8f);
            if (e.hp <= 0) KillEnemy(e);
        }

        private void KillEnemy(Enemy e)
        {
            e.dead = true;
            fx.Burst(world.ToWorld(e.pos) + Vector3.up * 0.6f, new Color(1f, 0.4f, 0.3f), new Color(2.4f, 0.6f, 0.3f), 50, 7f);
            Shockwave.Create(world.ToWorld(e.pos), 2f, new Color(1f, 0.6f, 0.3f));
            e.root.gameObject.SetActive(false);
            e.ring.SetActive(false);
            if (e.lane != null) e.lane.SetActive(false);
            coins += 5;
            kills++;
            AudioManager.PlaySfx(Sfx.Coin, 1f, 0.9f);
            OnMissionProgress();
        }

        private void UpdateEnemies(float dt)
        {
            foreach (var e in enemies)
            {
                if (e.dead) continue;
                var to = pos - e.pos;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist < WakeRange) e.awake = true;
                if (e.cooldown > 0f) e.cooldown -= dt;
                if (e.flash > 0f) e.flash -= dt * 3f;
                float lift = 0f;
                switch (e.kind)
                {
                    case EnemyKind.Guard: lift = UpdateGuard(e, to, dist, dt); break;
                    case EnemyKind.Charger: UpdateCharger(e, to, dist, dt); break;
                    case EnemyKind.Thrower: UpdateThrower(e, to, dist, dt); break;
                }
                // Never far from the way (where the walker can reach it).
                float px = world.PathX(e.pos.z);
                e.pos.x = Mathf.Clamp(e.pos.x, px - 6.5f, px + 6.5f);
                e.pos.y = InPassage(e.pos.z) ? 0f : world.TerrainY(e.pos.x, e.pos.z);
                e.root.localPosition = e.pos + Vector3.up * lift;
                var face = e.dash >= 0f && e.windup < 0f ? e.dashDir : to;
                if (e.dazed > 0f) e.root.localRotation *= Quaternion.Euler(0f, 400f * dt, 0f); // spinning, dazed
                else if (face.sqrMagnitude > 0.01f) e.root.localRotation = Quaternion.Slerp(e.root.localRotation, Quaternion.LookRotation(face), dt * 6f);
                e.root.localScale = Vector3.one * (1f + Mathf.Max(0f, e.flash) * 0.15f);
            }
        }

        private float UpdateGuard(Enemy e, Vector3 to, float dist, float dt)
        {
            if (e.windup >= 0f)
            {
                // Winding up a slam: the red ring grows; standing in it when it lands hurts.
                e.windup += dt;
                float k = e.windup / 0.7f;
                e.bot.SetRaise(k * 1.3f);
                e.ring.transform.localPosition = e.pos + Vector3.up * 0.03f;
                e.ring.transform.localScale = new Vector3(3.8f * k, 0.01f, 3.8f * k);
                if (k >= 1f)
                {
                    e.windup = -1f;
                    e.cooldown = 1.2f;
                    e.bot.SetRaise(0f);
                    e.ring.SetActive(false);
                    Shockwave.Create(world.ToWorld(e.pos), 1.9f, new Color(1f, 0.5f, 0.3f));
                    rig.Shake(0.5f);
                    AudioManager.PlaySfx(Sfx.Impact, 0.8f, 0.7f);
                    if (dist < 1.9f) Hurt(e.pos);
                }
                return e.windup > 0.4f ? (e.windup - 0.4f) * 1.5f : 0f;
            }
            if (!e.awake) return Idle(e, dt);
            if (dist > 1.5f) e.pos += to.normalized * (2.3f * dt);
            else if (e.cooldown <= 0f)
            {
                e.windup = 0f;
                e.ring.SetActive(true);
            }
            return 0f;
        }

        private void UpdateCharger(Enemy e, Vector3 to, float dist, float dt)
        {
            if (e.dazed > 0f)
            {
                e.dazed -= dt;
                e.bot.SetRaise(0.15f + Mathf.Sin(Time.time * 14f) * 0.1f);
                if (e.dazed <= 0f) e.bot.SetRaise(0f);
                return;
            }
            if (e.windup >= 0f)
            {
                // Aiming: a red lane along the line it will run.
                e.windup += dt;
                const float aim = 0.85f, length = 8f;
                if (e.windup < aim * 0.6f && dist > 0.1f) e.dashDir = to.normalized; // locks its line a moment before it goes
                e.lane.transform.localPosition = e.pos + e.dashDir * (length * 0.5f) + Vector3.up * 0.04f;
                e.lane.transform.localRotation = Quaternion.LookRotation(e.dashDir);
                e.lane.transform.localScale = new Vector3(0.9f, 0.01f, length * Mathf.Clamp01(e.windup / aim * 1.4f));
                e.bot.SetRaise(0.6f);
                if (e.windup >= aim)
                {
                    e.windup = -1f;
                    e.dash = 0f;
                    e.lane.SetActive(false);
                    e.bot.SetRaise(0f);
                    AudioManager.PlaySfx(Sfx.Hop, 0.8f, 0.6f);
                }
                return;
            }
            if (e.dash >= 0f)
            {
                e.dash += dt;
                e.pos += e.dashDir * (11f * dt);
                if ((int)(e.dash * 20f) % 2 == 0) fx.Dust(world.ToWorld(e.pos), new Color(0.5f, 0.42f, 0.3f), 2, 1f);
                var rel = pos - e.pos;
                rel.y = 0f;
                if (rel.magnitude < 0.9f && pos.y < e.pos.y + 1.2f) Hurt(e.pos - e.dashDir);
                if (e.dash > 0.72f)
                {
                    // Spent: stands dazed for a moment (the time to strike it).
                    e.dash = -1f;
                    e.dazed = 1.8f;
                    e.cooldown = 2.4f;
                    Shockwave.Create(world.ToWorld(e.pos), 1f, new Color(1f, 0.85f, 0.4f));
                    AudioManager.PlaySfx(Sfx.Bump, 0.7f, 0.8f);
                }
                return;
            }
            if (!e.awake) { Idle(e, dt); return; }
            if (dist > 6.5f) e.pos += to.normalized * (2f * dt);
            else if (e.cooldown <= 0f && dist > 1.2f)
            {
                e.windup = 0f;
                e.dashDir = to.normalized;
                e.lane.SetActive(true);
            }
        }

        private void UpdateThrower(Enemy e, Vector3 to, float dist, float dt)
        {
            if (e.windup >= 0f)
            {
                // A rock in the air: the red mark grows where it will land.
                e.windup += dt;
                const float flight = 1.15f;
                float k = Mathf.Clamp01(e.windup / flight);
                e.ring.transform.localPosition = e.target + Vector3.up * 0.04f;
                e.ring.transform.localScale = new Vector3(2.6f * (0.4f + 0.6f * k), 0.01f, 2.6f * (0.4f + 0.6f * k));
                if (e.windup >= flight)
                {
                    e.windup = -1f;
                    e.ring.SetActive(false);
                    Shockwave.Create(world.ToWorld(e.target), 1.3f, new Color(0.8f, 0.65f, 0.5f));
                    fx.Dust(world.ToWorld(e.target), new Color(0.55f, 0.48f, 0.4f), 14, 3f);
                    AudioManager.PlaySfx(Sfx.Impact, 0.6f, 1.1f);
                    var rel = pos - e.target;
                    rel.y = 0f;
                    if (rel.magnitude < 1.3f && pos.y < e.target.y + 1f) Hurt(e.target);
                }
                return;
            }
            if (!e.awake) { Idle(e, dt); return; }
            // Keeps its distance: backs off when the walker comes close, closes in when far.
            if (dist < 4.5f) e.pos -= to.normalized * (2.2f * dt);
            else if (dist > 9f) e.pos += to.normalized * (1.8f * dt);
            if (e.cooldown <= 0f && dist < 12f)
            {
                e.cooldown = Mathf.Max(1.6f, 2.8f - (Leg - 3) * 0.2f);
                e.windup = 0f;
                // Aims a little ahead of a moving walker.
                var lead = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * (autoRun ? 2.2f : 0.8f);
                e.target = new Vector3(pos.x + lead.x, 0f, pos.z + lead.z);
                e.target.y = GroundY(e.target.x, e.target.z, pos.y);
                e.ring.SetActive(true);
                e.bot.SetRaise(1f);
                ThrownRock.Create(world.ToWorld(e.pos + Vector3.up * 1.3f), world.ToWorld(e.target), 1.15f, new Color(0.4f, 0.35f, 0.7f));
                AudioManager.PlaySfx(Sfx.Hop, 0.6f, 0.5f);
            }
            else if (e.cooldown < 1.2f) e.bot.SetRaise(0f);
        }

        /// <summary>Not yet roused: shuffles about its spot.</summary>
        private float Idle(Enemy e, float dt)
        {
            float t = Time.time * 0.6f + e.phase;
            var want = e.home + new Vector3(Mathf.Sin(t) * 1.2f, 0f, Mathf.Cos(t * 0.7f) * 1.2f);
            e.pos = Vector3.MoveTowards(e.pos, want, 0.8f * dt);
            return 0f;
        }
    }
}
