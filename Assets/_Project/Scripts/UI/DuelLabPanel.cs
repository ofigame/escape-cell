using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The duel lab's panel (test builds only): rows of small buttons laid over the duel (a strike, the weapon families,
    /// the reactions, the freeze and shake scales, the pictures' slow motion, the layer tests) and a line of live readings
    /// (the match clock, the game's time scale, the picture clock, the push). Its own canvas, over everything.
    /// </summary>
    public class DuelLabPanel : MonoBehaviour
    {
        private RectTransform panel, body;
        private TextMeshProUGUI info;
        private float y;
        private int col;
        private const int Cols = 4;
        private const float W = 168f, H = 58f, Gap = 6f;

        public static DuelLabPanel Create()
        {
            var canvas = UiFactory.CreateCanvas("DuelLab", out _);
            canvas.sortingOrder = 600;
            var lab = canvas.gameObject.AddComponent<DuelLabPanel>();
            lab.Build(canvas.transform);
            return lab;
        }

        private void Build(Transform root)
        {
            // low on the screen (over the floor in front of the platform): the two foi stay in view above it
            panel = UiFactory.Box("Panel", root, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(Cols * (W + Gap) + 22f, 640f));
            UiFactory.Fill(panel, UiKit.A(UiKit.Style.backDeep, 0.78f), UiSprites.Rounded, 3f);
            info = UiFactory.TextBox("Info", panel, new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(Cols * (W + Gap), 118f), "", 21f, Color.white, FontStyles.Normal, align: TextAlignmentOptions.TopLeft);
            info.textWrappingMode = TextWrappingModes.Normal;
            body = UiFactory.Box("Body", panel, new Vector2(0f, 1f), new Vector2(11f, -128f), new Vector2(Cols * (W + Gap), 500f));
            Flat.Button(root, "LAB", new Vector2(0f, 0f), new Vector2(14f, 670f), new Vector2(120f, 60f), UiKit.Style.orange, Color.white, () => panel.gameObject.SetActive(!panel.gameObject.activeSelf), 26f);
        }

        /// <summary>A button (four to a row).</summary>
        public Button Add(string label, Action act, Color? face = null)
        {
            var b = Flat.Button(body, label, new Vector2(0f, 1f), new Vector2(col * (W + Gap), y), new Vector2(W, H), face ?? UiKit.Style.royal, Color.white, act, 22f);
            col++;
            if (col >= Cols) { col = 0; y -= H + Gap; }
            return b;
        }

        /// <summary>The live readings line.</summary>
        public void SetInfo(string text) => info.text = text;

        /// <summary>A screen point over the panel (no stick, no strike from it).</summary>
        public bool IsOver(Vector2 screen)
        {
            foreach (var r in GetComponentsInChildren<RectTransform>())
            {
                if (r == transform || !r.gameObject.activeInHierarchy || r.GetComponent<Graphic>() == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(r, screen, null)) return true;
            }
            return false;
        }

        public void Close() => Destroy(gameObject);
    }
}
