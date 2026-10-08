using SquashBot.Audio;
using SquashBot.Visual;
using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Forest
{
    /// <summary>
    /// From the fourth leg on, spike traps in the deck's stones: a plate that glows red for a moment, then iron spikes
    /// spring up from it and sink again. More of them, and quicker, with every leg.
    /// </summary>
    public partial class ForestPrototype
    {
        private class Spike
        {
            public Vector3 centre;
            public Transform spikes;
            public Renderer plate;
            public float t;
        }

        private readonly List<Spike> spikeTraps = new List<Spike>();
        private Material plateIdle, plateWarn;
        private const float SpikeRest = 1.6f, SpikeWarn = 0.8f, SpikeUp = 1f;

        private void BuildSpikes()
        {
            spikeTraps.Clear();
            if (Leg < 4) return;
            plateIdle ??= MaterialFactory.Create(new Color(0.25f, 0.24f, 0.24f), Color.black);
            plateWarn ??= MaterialFactory.Create(new Color(1f, 0.3f, 0.2f), new Color(2.2f, 0.35f, 0.2f));
            var iron = MaterialFactory.Create(new Color(0.55f, 0.56f, 0.6f), Color.black);
            int count = Mathf.Min(3 + (Leg - 4) * 2, 10);
            int cellsX = Mathf.FloorToInt(world.DeckHalfWidth);
            for (int i = 0; i < count; i++)
            {
                float z = Mathf.Lerp(ForestWorld.DeckStart + 6f, world.DeckEnd - 4f, (i + 0.5f) / count);
                z = ForestWorld.DeckStart + 1f + Mathf.Round((z - ForestWorld.DeckStart - 1f) / 2f) * 2f; // on a flag
                float x = -cellsX + 1f + UnityEngine.Random.Range(0, cellsX) * 2f;
                if (InGap(x, z)) continue;
                var s = new Spike { centre = new Vector3(x, ForestWorld.DeckHeight + 0.1f, z), t = i * 0.7f };
                var p = Shapes.Rounded("SpikePlate", world.transform, s.centre + Vector3.up * 0.01f, new Vector3(1.8f, 0.04f, 1.8f), 0.02f, plateIdle);
                s.plate = p.GetComponent<Renderer>();
                s.spikes = new GameObject("Spikes").transform;
                s.spikes.SetParent(world.transform, false);
                for (int a = 0; a < 3; a++)
                    for (int b = 0; b < 3; b++)
                        Shapes.Primitive(PrimitiveType.Cylinder, "Spike", s.spikes, new Vector3(-0.55f + a * 0.55f, 0.3f, -0.55f + b * 0.55f), new Vector3(0.09f, 0.3f, 0.09f), iron);
                s.spikes.localPosition = s.centre - Vector3.up * 0.7f;
                spikeTraps.Add(s);
            }
        }

        private void UpdateSpikes(float dt, bool onDeck)
        {
            float speed = 1f + (Leg - 4) * 0.08f;
            foreach (var s in spikeTraps)
            {
                s.t = (s.t + dt * speed) % (SpikeRest + SpikeWarn + SpikeUp);
                bool warn = s.t >= SpikeRest && s.t < SpikeRest + SpikeWarn;
                bool up = s.t >= SpikeRest + SpikeWarn;
                s.plate.sharedMaterial = warn && Mathf.Repeat(s.t * 8f, 1f) < 0.6f || up ? plateWarn : plateIdle;
                float k = up ? Mathf.Clamp01((s.t - SpikeRest - SpikeWarn) / 0.12f) * Mathf.Clamp01((SpikeRest + SpikeWarn + SpikeUp - s.t) / 0.2f) : 0f;
                bool rising = up && s.spikes.localPosition.y < s.centre.y - 0.6f;
                s.spikes.localPosition = s.centre + Vector3.up * Mathf.Lerp(-0.7f, 0f, k);
                if (rising) AudioManager.PlaySfx(Sfx.Blocked, 0.3f, 1.6f);
                if (!onDeck || !up || k < 0.5f || pos.y > ForestWorld.DeckHeight + 0.6f) continue;
                if (Mathf.Abs(pos.x - s.centre.x) < 0.95f && Mathf.Abs(pos.z - s.centre.z) < 0.95f) Hurt(s.centre);
            }
        }
    }
}
