using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.Visual
{
    /// <summary>
    /// A duel cape shown on the screen (under its foi's health and ulti bars) instead of on a post by the floor: the cape
    /// hangs far away from the stage before its own small camera, which films it into a picture the HUD shows; it sways
    /// gently. Gone with the duel (<see cref="ClearAll"/>).
    /// </summary>
    public class CapeShowcase : MonoBehaviour
    {
        private const int Width = 192, Height = 240;
        private static readonly List<CapeShowcase> shown = new List<CapeShowcase>();

        private RenderTexture picture;
        private Transform cape;
        private Camera filmer;
        private Quaternion rest = Quaternion.identity;

        /// <summary>Hangs <paramref name="off"/> (a cape taken off a foi) before its own camera and shows it in <paramref name="view"/>.</summary>
        public static CapeShowcase Show(Transform off, RawImage view, int slot, Vector3 outside)
        {
            if (off == null || view == null) return null;
            var root = new GameObject("CapeShowcase").transform;
            root.position = new Vector3(4000f + slot * 40f, -4000f, 0f); // (far from every stage: no main camera sees it)
            var s = root.gameObject.AddComponent<CapeShowcase>();
            s.cape = off;
            // its outside (the way its foi's back faced) towards the camera, which stands on -z
            outside.y = 0f;
            var turn = outside.sqrMagnitude > 1e-4f ? Quaternion.FromToRotation(outside.normalized, Vector3.back) : Quaternion.identity;
            off.rotation = turn * off.rotation;
            off.SetParent(root, true);
            off.localPosition = Vector3.zero;
            s.rest = off.localRotation;
            var rs = off.GetComponentsInChildren<Renderer>(true);
            var b = new Bounds(root.position, Vector3.one * 0.5f);
            if (rs.Length > 0) { b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); }
            // the camera in front of its outside (the cape as taken off: its outside towards -z), framing it whole
            var camGo = new GameObject("CapeCamera");
            camGo.transform.SetParent(root, false);
            s.filmer = camGo.AddComponent<Camera>();
            s.filmer.fieldOfView = 30f;
            float fit = Mathf.Max(b.size.y, b.size.x * Height / (float)Width) * 0.5f / Mathf.Tan(15f * Mathf.Deg2Rad);
            camGo.transform.position = b.center + Vector3.back * Mathf.Max(0.5f, fit);
            camGo.transform.rotation = Quaternion.LookRotation(b.center - camGo.transform.position);
            s.filmer.nearClipPlane = 0.05f;
            s.filmer.farClipPlane = fit * 3f + 5f;
            s.filmer.clearFlags = CameraClearFlags.SolidColor;
            s.filmer.backgroundColor = new Color(0f, 0f, 0f, 0f); // (see-through: only the cape on the screen)
            s.picture = new RenderTexture(Width, Height, 16, RenderTextureFormat.ARGB32) { name = "Cape" };
            s.filmer.targetTexture = s.picture;
            view.texture = s.picture;
            view.gameObject.SetActive(true);
            if (view.transform.parent != null) view.transform.parent.gameObject.SetActive(true); // (its frame too: hidden while the cape flew there)
            shown.Add(s);
            return s;
        }

        /// <summary>The duel over: every shown cape and its camera gone.</summary>
        public static void ClearAll()
        {
            foreach (var s in shown) if (s != null) Destroy(s.gameObject);
            shown.Clear();
        }

        private void Update()
        {
            if (cape != null) cape.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.6f) * 6f, Mathf.Sin(Time.time * 0.9f) * 10f, 0f) * rest; // (a gentle sway)
        }

        private void OnDestroy()
        {
            if (filmer != null) filmer.targetTexture = null;
            if (picture != null) { picture.Release(); Destroy(picture); }
        }
    }
}
