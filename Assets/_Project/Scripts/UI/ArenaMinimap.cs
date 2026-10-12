using System.Collections.Generic;
using SquashBot.Core;
using SquashBot.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The battle arenas' map in the bottom-right corner, see-through: the squares and roads light, the buildings
    /// dark, the gun posts yellow, foi a white dot with a pointer the way the camera looks, the robots red dots (vanG a
    /// bigger one). The picture is drawn once per arena; the dots move a few times a second.
    /// </summary>
    public class ArenaMinimap : MonoBehaviour
    {
        private const float Box = 290f;

        private RectTransform frame, mapRect, foi, pointer;
        private RawImage image;
        private Texture2D texture;
        private readonly List<RectTransform> dots = new List<RectTransform>();
        private int gridW = 1, gridH = 1;
        private float timer;

        public static ArenaMinimap Create(Transform parent)
        {
            var frame = UiFactory.Box("Minimap", parent, new Vector2(1f, 0f), new Vector2(-Box * 0.5f - 24f, Box * 0.5f + 24f), new Vector2(Box, Box));
            frame.pivot = new Vector2(0.5f, 0.5f);
            var m = frame.gameObject.AddComponent<ArenaMinimap>();
            m.frame = frame;
            var back = UiFactory.Fill(frame, new Color(0.03f, 0.03f, 0.08f, 0.35f), UiSprites.Rounded, 1.4f);
            back.raycastTarget = false;
            m.mapRect = UiFactory.Box("Map", frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Box - 20f, Box - 20f));
            m.mapRect.pivot = new Vector2(0.5f, 0.5f);
            m.image = m.mapRect.gameObject.AddComponent<RawImage>();
            m.image.raycastTarget = false;
            m.image.color = new Color(1f, 1f, 1f, 0.8f);
            m.pointer = UiFactory.Box("Look", m.mapRect, Vector2.zero, Vector2.zero, new Vector2(6f, 26f));
            m.pointer.pivot = new Vector2(0.5f, 0f);
            UiFactory.Fill(m.pointer, new Color(1f, 1f, 1f, 0.9f), UiSprites.Rounded, 6f).raycastTarget = false;
            m.foi = UiFactory.Box("Foi", m.mapRect, Vector2.zero, Vector2.zero, new Vector2(22f, 22f));
            m.foi.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(m.foi, Color.white, UiSprites.Circle).raycastTarget = false;
            UiFactory.Fill(UiFactory.Stretch("Rim", m.foi), new Color(0.2f, 0.6f, 1f), UiSprites.Ring, 0.4f).raycastTarget = false;
            frame.gameObject.SetActive(false);
            return m;
        }

        /// <summary>Draws this arena's map (its floor, buildings and gun posts) and shows the map.</summary>
        public void Build(GridModel grid, IList<BattleArenas.Building> buildings, IList<BattleArenas.Post> posts)
        {
            gridW = grid.Width;
            gridH = grid.Height;
            if (texture != null) Destroy(texture);
            texture = new Texture2D(gridW, gridH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[gridW * gridH];
            var floor = new Color32(235, 228, 210, 200);
            var fire = new Color32(255, 120, 60, 220);
            var house = new Color32(40, 38, 52, 190);
            var gun = new Color32(255, 210, 70, 255);
            for (int y = 0; y < gridH; y++)
                for (int x = 0; x < gridW; x++)
                {
                    var p = new GridPos(x, y);
                    px[y * gridW + x] = grid.IsLava(p) ? fire : grid.IsFloor(p) ? floor : new Color32(0, 0, 0, 0);
                }
            foreach (var b in buildings)
                for (int y = b.y0; y <= b.y1; y++)
                    for (int x = b.x0; x <= b.x1; x++)
                        if (x >= 0 && y >= 0 && x < gridW && y < gridH) px[y * gridW + x] = house;
            foreach (var p in posts)
                for (int y = p.y0; y <= p.y1; y++)
                    for (int x = p.x0; x <= p.x1; x++)
                        if (x >= 0 && y >= 0 && x < gridW && y < gridH) px[y * gridW + x] = gun;
            texture.SetPixels32(px);
            texture.Apply(false, true);
            image.texture = texture;
            // Keep the arena's shape inside the square box.
            float aspect = gridW / (float)gridH, inner = Box - 20f;
            mapRect.sizeDelta = aspect >= 1f ? new Vector2(inner, inner / aspect) : new Vector2(inner * aspect, inner);
            frame.gameObject.SetActive(true);
            timer = 0f;
        }

        public void Hide() => frame.gameObject.SetActive(false);

        /// <summary>Moves the dots (a few times a second): foi, the way the camera looks, and the enemies.</summary>
        public void Tick(float dt, Vector3 foiWorld, float lookYaw, List<(Vector3 at, bool big)> enemies)
        {
            if (!frame.gameObject.activeSelf) return;
            timer -= dt;
            if (timer > 0f) return;
            timer = 0.15f;
            foi.anchoredPosition = ToMap(foiWorld);
            pointer.anchoredPosition = foi.anchoredPosition;
            pointer.localRotation = Quaternion.Euler(0f, 0f, -lookYaw);
            while (dots.Count < enemies.Count)
            {
                var d = UiFactory.Box("Enemy", mapRect, Vector2.zero, Vector2.zero, new Vector2(12f, 12f));
                d.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(d, new Color(1f, 0.2f, 0.2f, 0.95f), UiSprites.Circle).raycastTarget = false;
                d.SetSiblingIndex(1); // under foi
                dots.Add(d);
            }
            for (int i = 0; i < dots.Count; i++)
            {
                bool on = i < enemies.Count;
                if (dots[i].gameObject.activeSelf != on) dots[i].gameObject.SetActive(on);
                if (!on) continue;
                dots[i].anchoredPosition = ToMap(enemies[i].at);
                dots[i].sizeDelta = Vector2.one * (enemies[i].big ? 20f : 12f);
            }
        }

        private Vector2 ToMap(Vector3 world)
        {
            // World x/z are grid x/y (tile centres on whole numbers).
            float u = (world.x + 0.5f) / gridW, v = (world.z + 0.5f) / gridH;
            return new Vector2(u * mapRect.sizeDelta.x, v * mapRect.sizeDelta.y);
        }

        private void OnDestroy()
        {
            if (texture != null) Destroy(texture);
        }
    }
}
