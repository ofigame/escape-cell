using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The workshop's badges and the old robot's shaped eyes, built from rounded boxes like the robot itself.
    /// Coordinates are in the robot's model space: head centre (0, 0.5, 0), top of the head y ≈ 0.71, face toward +z
    /// (foi's badge mount is laid out in the same space). The hats, capes, back pieces and arm and leg gear are gone.
    /// </summary>
    public static class CosmeticModels
    {
        /// <summary>Badges: the press-arm pin on the shoulder, Kuzgun's CL-1 plate on the chest, vanG's beating gold heart.</summary>
        public static Transform Badge(Transform root, string id, Color color)
        {
            switch (id)
            {
                case "badge.press":
                {
                    var pin = Shapes.Rounded("Pin", root, new Vector3(-0.18f, 0.29f, 0.04f), new Vector3(0.07f, 0.07f, 0.02f), 0.02f,
                        MaterialFactory.Create(color, new Color(0.2f, 0.2f, 0.24f)));
                    Shapes.Rounded("Bar", root, new Vector3(-0.18f, 0.29f, 0.052f), new Vector3(0.05f, 0.015f, 0.01f), 0.005f,
                        MaterialFactory.Create(new Color(0.95f, 0.75f, 0.3f), new Color(0.5f, 0.35f, 0.05f)));
                    return pin.transform;
                }
                case "badge.cl1":
                {
                    var plate = Shapes.Rounded("Plate", root, new Vector3(0f, 0.21f, 0.125f), new Vector3(0.14f, 0.06f, 0.015f), 0.01f,
                        MaterialFactory.Create(color, Color.black));
                    foreach (float x in new[] { -0.035f, 0f, 0.035f })
                        Shapes.Rounded("Mark", root, new Vector3(x, 0.21f, 0.134f), new Vector3(0.016f, 0.03f, 0.005f), 0.003f,
                            MaterialFactory.Create(new Color(0.9f, 0.9f, 1f), new Color(0.5f, 0.5f, 0.6f)));
                    return plate.transform;
                }
                case "badge.heart":
                {
                    var heart = new GameObject("Heart").transform;
                    heart.SetParent(root, false);
                    heart.localPosition = new Vector3(0f, 0.22f, 0.125f);
                    var gold = MaterialFactory.Create(color, new Color(2.4f, 1.6f, 0.4f));
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var lobe = Shapes.Rounded("Lobe", heart, new Vector3(side * 0.016f, 0.008f, 0f), new Vector3(0.04f, 0.055f, 0.012f), 0.012f, gold);
                        lobe.transform.localRotation = Quaternion.Euler(0f, 0f, side * 40f);
                    }
                    heart.gameObject.AddComponent<Pulse>();
                    return heart;
                }
                case "badge.lumi":
                {
                    // Lumi's light: a small gold sun on the chest, eight short rays round a glowing core
                    var sun = new GameObject("Sun").transform;
                    sun.SetParent(root, false);
                    sun.localPosition = new Vector3(0f, 0.22f, 0.125f);
                    var gold = MaterialFactory.Create(color, new Color(0.5f, 0.35f, 0.05f));
                    Shapes.Rounded("Disc", sun, Vector3.zero, new Vector3(0.05f, 0.05f, 0.012f), 0.02f, gold);
                    for (int i = 0; i < 8; i++)
                    {
                        var ray = Shapes.Rounded("Ray", sun, Quaternion.Euler(0f, 0f, i * 45f) * new Vector3(0f, 0.04f, 0f), new Vector3(0.012f, 0.026f, 0.008f), 0.004f, gold);
                        ray.transform.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
                    }
                    Shapes.Rounded("Core", sun, new Vector3(0f, 0f, 0.008f), new Vector3(0.026f, 0.026f, 0.008f), 0.01f,
                        MaterialFactory.Create(new Color(1f, 0.95f, 0.7f), new Color(2.2f, 1.8f, 0.6f)));
                    return sun;
                }
                case "badge.raven":
                {
                    // Kuzgun's seal: a dark round seal, a silver raven's spread wings on it
                    var seal = new GameObject("Seal").transform;
                    seal.SetParent(root, false);
                    seal.localPosition = new Vector3(0f, 0.22f, 0.125f);
                    Shapes.Rounded("Disc", seal, Vector3.zero, new Vector3(0.07f, 0.07f, 0.014f), 0.03f, MaterialFactory.Create(color, Color.black));
                    var silver = MaterialFactory.Create(new Color(0.82f, 0.84f, 0.9f), new Color(0.25f, 0.25f, 0.3f));
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var wing = Shapes.Rounded("Wing", seal, new Vector3(side * 0.014f, 0.004f, 0.009f), new Vector3(0.03f, 0.009f, 0.005f), 0.003f, silver);
                        wing.transform.localRotation = Quaternion.Euler(0f, 0f, side * 25f);
                    }
                    Shapes.Rounded("Body", seal, new Vector3(0f, -0.006f, 0.009f), new Vector3(0.008f, 0.024f, 0.005f), 0.003f, silver);
                    return seal;
                }
            }
            return null;
        }

        /// <summary>
        /// Shaped eyes: children of the two eye pieces (so the blink still squashes them); the plain eye is hidden.
        /// </summary>
        public static void EyeShape(Transform eye, string id, Material glow)
        {
            var holder = new GameObject("Shape").transform;
            holder.SetParent(eye, false);
            holder.localScale = new Vector3(0.09f, 0.09f, 0.025f);
            switch (id)
            {
                case "eyes.happy":
                    // An upturned arch: ^
                    foreach (float side in new[] { -1f, 1f })
                        Shapes.Cube("Arch", holder, new Vector3(side * 0.2f, 0f, 0f), new Vector3(0.55f, 0.18f, 1f), glow)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, side * -35f);
                    break;
                case "eyes.sleepy":
                    Shapes.Cube("Lid", holder, new Vector3(0f, -0.15f, 0f), new Vector3(1f, 0.18f, 1f), glow);
                    break;
                case "eyes.determined":
                    Shapes.Cube("Eye", holder, new Vector3(0f, -0.08f, 0f), new Vector3(0.9f, 0.55f, 1f), glow);
                    break;
                case "eyes.heart":
                    foreach (float side in new[] { -1f, 1f })
                        Shapes.Cube("Lobe", holder, new Vector3(side * 0.18f, 0.05f, 0f), new Vector3(0.45f, 0.7f, 1f), glow)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, side * 40f);
                    break;
                case "eyes.star":
                    for (int k = 0; k < 3; k++)
                        Shapes.Cube("Ray", holder, Vector3.zero, new Vector3(0.22f, 1.1f, 1f), glow)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, k * 60f);
                    break;
            }
        }
    }

    /// <summary>A slow heartbeat: scales the object up and down.</summary>
    public class Pulse : MonoBehaviour
    {
        private void Update()
        {
            float t = Mathf.Repeat(Time.time, 1.1f);
            float beat = Mathf.Exp(-t * 9f) + 0.6f * Mathf.Exp(-Mathf.Abs(t - 0.25f) * 14f);
            transform.localScale = Vector3.one * (1f + beat * 0.25f);
        }
    }
}
