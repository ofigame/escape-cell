using BrickMuseum.Build;
using UnityEngine;

namespace BrickMuseum.Character
{
    /// <summary>
    /// The character workshop's live picture: the brick person on a hidden stage far below the world, lit by its own
    /// point light, filmed into a texture. It breathes, turns slowly, and can be turned by hand.
    /// </summary>
    public sealed class PersonStudio
    {
        private static readonly Vector3 Stage = new Vector3(0f, -4000f, 0f);
        public readonly RenderTexture texture;
        private readonly Camera cam;
        private readonly Transform pivot;
        private BrickPerson person;
        private string shownKey;
        private float idleSpin = 1f;

        public PersonStudio(Transform parent)
        {
            texture = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "PersonStudio" };
            texture.Create();
            var root = new GameObject("PersonStudio").transform;
            root.SetParent(parent, false);
            root.position = Stage;
            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root, false);
            cam = new GameObject("StudioCamera").AddComponent<Camera>();
            cam.transform.SetParent(root, false);
            cam.transform.localPosition = new Vector3(0f, 1.7f, 5.6f);
            cam.transform.LookAt(Stage + new Vector3(0, 1.2f, 0));
            cam.fieldOfView = 30f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << Previews.PictureLayer;
            cam.targetTexture = texture;
            cam.enabled = false;
            foreach (var (pos, intensity) in new[] { (new Vector3(2.5f, 4f, 4f), 6f), (new Vector3(-3f, 2f, 2f), 2.5f) })
            {
                var l = new GameObject("StudioLight").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.localPosition = pos;
                l.type = LightType.Point;
                l.range = 14f;
                l.intensity = intensity;
                l.cullingMask = 1 << Previews.PictureLayer;
            }
        }

        public void Show(Outfit o)
        {
            if (person != null && shownKey == o.Key) return;
            if (person != null) Object.Destroy(person.root.gameObject);
            person = new BrickPerson(pivot, o);
            Previews.SetLayer(person.root.gameObject, Previews.PictureLayer);
            shownKey = o.Key;
        }

        public void Turn(float degrees) { pivot.Rotate(0f, degrees, 0f); idleSpin = 0f; }

        public void Tick(float dt, bool visible)
        {
            cam.enabled = visible;
            if (!visible || person == null) return;
            idleSpin = Mathf.MoveTowards(idleSpin, 1f, dt * 0.3f);       // starts turning again a while after a drag
            pivot.Rotate(0f, 22f * idleSpin * dt, 0f);
            person.Animate(0f, false, dt);
        }
    }
}
