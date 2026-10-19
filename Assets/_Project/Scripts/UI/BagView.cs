using System.Collections.Generic;
using BrickMuseum.Build;
using BrickMuseum.Data;
using UnityEngine;
using UnityEngine.UI;

namespace BrickMuseum.UI
{
    /// <summary>One loose piece in the bag.</summary>
    public sealed class BagItem
    {
        public Piece kind;
        public RawImage image;
        public Texture2D tex;
        public float u, v, rot, size;
        public int z;
        public bool hidden;
    }

    /// <summary>
    /// The bag's contents: piece pictures scattered and overlapping. Taps pick the topmost piece whose picture is solid under
    /// the finger (transparent corners don't count); dragging over empty space pushes pieces aside; "Dağıt" spreads them.
    /// </summary>
    public sealed class BagView
    {
        public readonly RectTransform area;
        public readonly List<BagItem> items = new List<BagItem>();
        public string glowKey;

        public BagView(RectTransform area) { this.area = area; }

        public void Fill(IEnumerable<Piece> kinds)
        {
            foreach (var it in items) Object.Destroy(it.image.gameObject);
            items.Clear();
            var list = new List<Piece>(kinds);
            float shrink = Mathf.Clamp(Mathf.Sqrt(26f / Mathf.Max(1, list.Count)), 0.55f, 1f);
            foreach (var k in list)
            {
                var it = new BagItem
                {
                    kind = k, tex = Previews.Piece(k), u = Random.value, v = Random.value, rot = Random.Range(-50f, 50f),
                    size = Mathf.Min(300f, 110f + 34f * k.longSide + (k.h >= 3 ? 26f : 0f)) * shrink, z = Random.Range(1, 1000),
                };
                var r = Ui.Rect(area, "piece");
                it.image = r.gameObject.AddComponent<RawImage>();
                it.image.texture = it.tex;
                it.image.raycastTarget = false;
                r.anchorMin = r.anchorMax = new Vector2(0, 0);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(it.size, it.size);
                items.Add(it);
            }
            items.Sort((a, b) => a.z.CompareTo(b.z));
            foreach (var it in items) it.image.transform.SetAsLastSibling();
        }

        public void Layout()
        {
            var rect = area.rect;
            foreach (var it in items)
            {
                var r = it.image.rectTransform;
                r.anchoredPosition = Center(it, rect);
                r.localRotation = Quaternion.Euler(0, 0, it.rot);
                it.image.gameObject.SetActive(!it.hidden);
                it.image.color = glowKey != null && it.kind.key == glowKey ? new Color(1f, 1f, 0.55f) : Color.white;
                if (glowKey != null && it.kind.key == glowKey) it.image.transform.SetAsLastSibling();
            }
        }

        private static Vector2 Center(BagItem it, Rect rect) =>
            new Vector2(it.size / 2 + it.u * Mathf.Max(1, rect.width - it.size), it.size / 2 + it.v * Mathf.Max(1, rect.height - it.size));

        private Vector2 Local(Vector2 screen, Camera cam)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, cam, out var lp);
            return lp - area.rect.min; // 0,0 = bottom-left of the area
        }

        /// <summary>Topmost piece whose picture is solid under the screen point.</summary>
        public BagItem Hit(Vector2 screen, Camera cam)
        {
            var p = Local(screen, cam);
            var rect = area.rect;
            BagItem best = null; int bestOrder = -1;
            foreach (var it in items)
            {
                if (it.hidden) continue;
                var c = Center(it, rect);
                var d = p - c;
                float a = -it.rot * Mathf.Deg2Rad;
                float lx = d.x * Mathf.Cos(a) - d.y * Mathf.Sin(a), ly = d.x * Mathf.Sin(a) + d.y * Mathf.Cos(a);
                float u = lx / it.size + 0.5f, v = ly / it.size + 0.5f;
                if (u < 0 || u >= 1 || v < 0 || v >= 1) continue;
                if (it.tex.GetPixel((int)(u * it.tex.width), (int)(v * it.tex.height)).a < 0.25f) continue;
                int order = glowKey != null && it.kind.key == glowKey ? 100000 : it.image.transform.GetSiblingIndex();
                if (order > bestOrder) { bestOrder = order; best = it; }
            }
            return best;
        }

        /// <summary>Pushes pieces away from the finger, carrying them a little along its movement.</summary>
        public void Brush(Vector2 screen, Vector2 deltaScreen, Camera cam, float canvasScale)
        {
            var p = Local(screen, cam);
            var move = deltaScreen / Mathf.Max(0.01f, canvasScale);
            var rect = area.rect;
            // walk the finger's path in small steps so fast swipes still push everything they cross
            int steps = Mathf.Max(1, Mathf.CeilToInt(move.magnitude / 20f));
            for (int s = 1; s <= steps; s++)
            {
                var at = p - move * (1f - s / (float)steps);
                var m = move / steps;
                foreach (var it in items)
                {
                    if (it.hidden) continue;
                    var c = Center(it, rect);
                    var d = c - at;
                    float dist = Mathf.Max(0.01f, d.magnitude), reach = 130f + it.size * 0.4f;
                    if (dist > reach) continue;
                    float f = (reach - dist) / reach;
                    var push = d / dist * f * 32f + m * f * 0.8f;
                    it.u = Mathf.Clamp01(it.u + push.x / Mathf.Max(1, rect.width - it.size));
                    it.v = Mathf.Clamp01(it.v + push.y / Mathf.Max(1, rect.height - it.size));
                    it.rot += m.x * f * 0.3f;
                }
            }
            Layout();
        }

        /// <summary>Lays the pieces out on a loose grid so nothing is hidden.</summary>
        public void Spread()
        {
            var rect = area.rect;
            var live = items.FindAll(i => !i.hidden);
            int n = live.Count;
            if (n == 0) return;
            int cols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(n * rect.width / rect.height))), rows = Mathf.CeilToInt(n / (float)cols);
            for (int i = live.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (live[i], live[j]) = (live[j], live[i]); }
            for (int i = 0; i < n; i++)
            {
                var it = live[i];
                float x = ((i % cols) + 0.5f) / cols * rect.width, y = ((i / cols) + 0.5f) / rows * rect.height;
                it.u = Mathf.Clamp01((x - it.size / 2) / Mathf.Max(1, rect.width - it.size));
                it.v = Mathf.Clamp01((y - it.size / 2) / Mathf.Max(1, rect.height - it.size));
                it.rot = Random.Range(-15f, 15f);
            }
            Layout();
        }

        public void Remove(BagItem it)
        {
            items.Remove(it);
            Object.Destroy(it.image.gameObject);
        }

        public int Count => items.Count;
    }
}
