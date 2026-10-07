using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The paint workshop's step marks: a small coloured print left on the tile the robot lands on (a leaf, sparks,
    /// bubbles or star dust) that fades out after a moment. Marks are pooled.
    /// </summary>
    public class StepMark : MonoBehaviour
    {
        private const float Life = 1.4f;
        private static readonly System.Collections.Generic.Stack<StepMark> pool = new System.Collections.Generic.Stack<StepMark>();

        private Material material;
        private Color color;
        private float age;

        /// <summary>Leaves a mark of the given kind at a tile-top position.</summary>
        public static void Leave(Vector3 at, string id, Color color)
        {
            var m = pool.Count > 0 ? pool.Pop() : Create();
            if (m == null) m = Create();
            m.transform.position = at + Vector3.up * 0.012f;
            m.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float max = Mathf.Max(1f, Mathf.Max(color.r, Mathf.Max(color.g, color.b)));
            m.color = new Color(color.r / max, color.g / max, color.b / max, 0.85f);
            m.age = 0f;
            m.Shape(id);
            m.gameObject.SetActive(true);
        }

        private static StepMark Create()
        {
            var go = new GameObject("StepMark");
            var mark = go.AddComponent<StepMark>();
            mark.material = MaterialFactory.CreateTransparent(Color.white, Color.black);
            return mark;
        }

        /// <summary>Rebuilds the print: a leaf, a spark cross, three bubbles or a scatter of star dust.</summary>
        private void Shape(string id)
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            switch (id)
            {
                case "step.leaf":
                    Shapes.Primitive(PrimitiveType.Sphere, "Leaf", transform, Vector3.zero, new Vector3(0.16f, 0.01f, 0.3f), material);
                    break;
                case "step.spark":
                    for (int k = 0; k < 4; k++)
                        Shapes.Cube("Ray", transform, Vector3.zero, new Vector3(0.04f, 0.01f, 0.32f), material).transform.localRotation = Quaternion.Euler(0f, k * 45f, 0f);
                    break;
                case "step.bubble":
                    foreach (var p in new[] { new Vector3(-0.08f, 0f, 0.04f), new Vector3(0.09f, 0f, -0.02f), new Vector3(0f, 0f, -0.1f) })
                        Shapes.Primitive(PrimitiveType.Cylinder, "Bubble", transform, p, new Vector3(0.1f, 0.005f, 0.1f), material);
                    break;
                default:
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * 1.05f;
                        Shapes.Cube("Dust", transform, new Vector3(Mathf.Cos(a) * 0.14f, 0f, Mathf.Sin(a) * 0.14f), new Vector3(0.05f, 0.01f, 0.05f), material)
                            .transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    }
                    break;
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = age / Life;
            if (t >= 1f)
            {
                gameObject.SetActive(false);
                pool.Push(this);
                return;
            }
            MaterialFactory.SetColors(material, new Color(color.r, color.g, color.b, color.a * (1f - t * t)), color * 0.6f);
        }
    }
}
