using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Forest
{
    /// <summary>
    /// The open land's tasks: the passage (cave, gorge, ...) is shut by an iron gate until enough of vanG's robots are
    /// beaten and, from the second leg, the energy cores scattered off the path are gathered. Every leg asks for more.
    /// A notice at the start of a leg tells what is new there.
    /// </summary>
    public partial class ForestPrototype
    {
        private int kills, killsNeeded, coresGot, coresNeeded;
        private readonly List<Transform> cores = new List<Transform>();
        private Transform gate;
        private Material gateLamp;
        private float gateOpen = -1f, noticeLeft;
        private bool hunting;
        private string notice;

        private bool MissionDone => kills >= killsNeeded && coresGot >= coresNeeded;
        private bool GateShut => gateOpen < 0f;

        private void BuildMission()
        {
            kills = coresGot = 0;
            hunting = false;
            gateOpen = -1f;
            killsNeeded = Mathf.CeilToInt(enemies.Count * 0.7f);
            coresNeeded = Leg >= 2 ? Mathf.Min(1 + Leg, 7) : 0;
            cores.Clear();
            var coreMat = MaterialFactory.Create(new Color(0.45f, 0.95f, 1f), new Color(0.6f, 2.4f, 2.8f));
            for (int i = 0; i < coresNeeded; i++)
            {
                // Spread over the open stretches, off the path to either side (never on the deck).
                float z = Mathf.Lerp(18f, world.TunnelZ - 14f, (i + 0.5f) / coresNeeded);
                if (z > ForestWorld.StairsUpStart - 4f && z < world.StairsDownEnd + 4f) z = world.StairsDownEnd + 6f + i * 3f;
                float x = world.PathX(z) + (i % 2 == 0 ? -1f : 1f) * UnityEngine.Random.Range(3f, 5.5f);
                var root = new GameObject("EnergyCore").transform;
                root.SetParent(world.transform, false);
                root.localPosition = new Vector3(x, world.TerrainY(x, z) + 0.9f, z);
                foreach (float a in new[] { 0f, 45f })
                {
                    var c = Shapes.Primitive(PrimitiveType.Cube, "Crystal", root, Vector3.zero, new Vector3(0.32f, 0.5f, 0.32f), coreMat);
                    c.transform.localRotation = Quaternion.Euler(0f, a, 45f);
                }
                cores.Add(root);
            }
            BuildGate();
            BuildSpikes();
            notice = Leg switch
            {
                1 => Loc.T("forest.new.mission"),
                2 => Loc.T("forest.new.charger"),
                3 => Loc.T("forest.new.thrower"),
                4 => Loc.T("forest.new.spikes"),
                _ => null,
            };
            noticeLeft = notice != null ? 7f : 0f;
        }

        /// <summary>Iron bars across the passage mouth, a red lamp on either side (green when it opens).</summary>
        private void BuildGate()
        {
            gate = new GameObject("PassageGate").transform;
            gate.SetParent(world.transform, false);
            gate.localPosition = new Vector3(0f, 0f, world.TunnelZ - 0.35f);
            var iron = MaterialFactory.Create(new Color(0.2f, 0.2f, 0.22f), Color.black);
            gateLamp = MaterialFactory.Create(new Color(1f, 0.25f, 0.2f), new Color(3f, 0.4f, 0.25f));
            float half = ForestWorld.PassageHalf;
            for (float x = -half + 0.3f; x <= half - 0.25f; x += 0.42f)
                Shapes.Primitive(PrimitiveType.Cylinder, "Bar", gate, new Vector3(x, 1.65f, 0f), new Vector3(0.09f, 1.65f, 0.09f), iron);
            foreach (float y in new[] { 0.5f, 1.7f, 2.9f })
                Shapes.Rounded("Rail", gate, new Vector3(0f, y, 0f), new Vector3(half * 2f, 0.12f, 0.1f), 0.03f, iron);
            foreach (float s in new[] { -1f, 1f })
                Shapes.Primitive(PrimitiveType.Sphere, "Lamp", world.transform, new Vector3(s * (half + 0.3f), 2.9f, world.TunnelZ - 0.6f), Vector3.one * 0.2f, gateLamp);
        }

        private void OnMissionProgress()
        {
            if (!MissionDone || !GateShut) return;
            gateOpen = 0f;
            MaterialFactory.SetColors(gateLamp, new Color(0.3f, 1f, 0.4f), new Color(0.5f, 3f, 0.6f));
            notice = Loc.T("forest.gateOpen");
            noticeLeft = 4f;
            AudioManager.PlaySfx(Sfx.Win, 0.7f, 1.1f);
            rig.Shake(0.4f);
        }

        private void UpdateMission(float dt)
        {
            float spin = Time.time * 90f;
            for (int i = cores.Count - 1; i >= 0; i--)
            {
                var c = cores[i];
                c.localRotation = Quaternion.Euler(0f, spin, 0f);
                var d = c.localPosition - (pos + Vector3.up * 0.9f);
                d.y *= 0.5f;
                if (d.sqrMagnitude > 1.1f * 1.1f) continue;
                coresGot++;
                fx.Burst(c.position, new Color(0.5f, 1f, 1f), new Color(0.6f, 2.4f, 2.8f), 30, 5f);
                Shockwave.Create(c.position - Vector3.up * 0.8f, 1.4f, new Color(0.5f, 1f, 1f));
                AudioManager.PlaySfx(Sfx.Coin, 1f, 1.5f);
                Destroy(c.gameObject);
                cores.RemoveAt(i);
                OnMissionProgress();
            }
            if (gateOpen >= 0f && gateOpen < 1f)
            {
                // The bars slide up into the rock.
                gateOpen = Mathf.Min(1f, gateOpen + dt / 1.6f);
                gate.localPosition = new Vector3(0f, Mathf.SmoothStep(0f, 3.6f, gateOpen), world.TunnelZ - 0.35f);
                if (gateOpen >= 1f) gate.gameObject.SetActive(false);
            }
            if (noticeLeft > 0f) noticeLeft -= dt;
        }

        /// <summary>The objective line: a fresh notice first, then the tasks left while the gate is shut.</summary>
        private string MissionText(string fallback)
        {
            if (noticeLeft > 0f && notice != null) return notice;
            if (!GateShut) return fallback;
            string text = Loc.F("forest.mission.robots", Mathf.Min(kills, killsNeeded), killsNeeded);
            if (coresNeeded > 0) text += "  ·  " + Loc.F("forest.mission.cores", coresGot, coresNeeded);
            return (pos.z > world.TunnelZ - 14f ? Loc.T("forest.gateLocked") : fallback) + "\n" + text;
        }

        /// <summary>Where the guide points once past the clearing while the gate is shut: the nearest robot or core left.</summary>
        private bool MissionTarget(out Vector3 target)
        {
            target = default;
            if (pos.z > world.ClearingCentre.y + ForestWorld.ClearingRadius) hunting = true; // stays on once past the clearing
            if (!GateShut || !hunting) return false;
            float best = float.MaxValue;
            if (kills < killsNeeded)
                foreach (var e in enemies)
                    if (!e.dead && (e.pos - pos).sqrMagnitude < best) { best = (e.pos - pos).sqrMagnitude; target = e.pos; }
            if (coresGot < coresNeeded)
                foreach (var c in cores)
                    if ((c.localPosition - pos).sqrMagnitude < best) { best = (c.localPosition - pos).sqrMagnitude; target = c.localPosition; }
            return best < float.MaxValue;
        }
    }
}
