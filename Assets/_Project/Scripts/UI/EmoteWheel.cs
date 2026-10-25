using System;
using System.Collections.Generic;
using SquashBot.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The emote wheel (the design's §7): six slots round a circle — the face taunts (a face icon) and the written emotes
    /// — with the middle turning to the next six when there are more. A pick closes it.
    /// </summary>
    public class EmoteWheel : MonoBehaviour
    {
        public event Action<int> Picked;
        private RectTransform root;
        private readonly List<GameObject> slots = new List<GameObject>();
        private int page;
        private const int PerPage = 6;
        private const float Radius = 190f;

        public static EmoteWheel Create(RectTransform parent, Vector2 anchor, Vector2 at)
        {
            var r = UiFactory.Box("EmoteWheel", parent, anchor, at, new Vector2(560f, 560f));
            r.pivot = new Vector2(0.5f, 0.5f);
            var w = r.gameObject.AddComponent<EmoteWheel>();
            w.root = r;
            var disc = UiFactory.Fill(r, new Color(0.04f, 0.05f, 0.12f, 0.72f), UiSprites.Circle);
            disc.raycastTarget = true;
            r.gameObject.SetActive(false);
            return w;
        }

        public bool Open => root.gameObject.activeSelf;

        public void Toggle()
        {
            if (Open) { root.gameObject.SetActive(false); return; }
            page = 0;
            Fill();
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
        }

        public void Close() => root.gameObject.SetActive(false);

        private void Fill()
        {
            foreach (var s in slots) Destroy(s);
            slots.Clear();
            var all = FaceTaunts.Wheel;
            int pages = Mathf.Max(1, (all.Count + PerPage - 1) / PerPage);
            page %= pages;
            for (int i = 0; i < PerPage; i++)
            {
                int k = page * PerPage + i;
                if (k >= all.Count) break;
                int e = all[k];
                float a = Mathf.PI * 0.5f - i * Mathf.PI * 2f / PerPage;
                var at = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius;
                bool taunt = FaceTaunts.IsTaunt(e);
                string label = taunt ? Loc.T("taunt." + FaceTaunts.Of(e)) : Loc.T("duel.em." + e);
                var b = Flat.Button(root, taunt ? "" : label, new Vector2(0.5f, 0.5f), at, new Vector2(150f, 112f), taunt ? new Color(0.95f, 0.55f, 0.3f) : (e == Emotes.Season ? new Color(0.65f, 0.45f, 1f) : Flat.CardHi), Flat.Text, () => { Close(); Picked?.Invoke(e); }, 22f);
                var rt = (RectTransform)b.transform;
                rt.pivot = new Vector2(0.5f, 0.5f);
                if (taunt)
                {
                    var icon = UiFactory.Box("Icon", rt, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(64f, 64f));
                    icon.pivot = new Vector2(0.5f, 0.5f);
                    var img = icon.gameObject.AddComponent<Image>();
                    img.sprite = DuelIcons.Get(DuelIcon.Taunt);
                    img.raycastTarget = false;
                    var t = Flat.Label(rt, label, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(140f, 32f), 18f, Flat.Text, TextAlignmentOptions.Center, bold: true);
                    t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    t.enableAutoSizing = true; t.fontSizeMin = 12f; t.fontSizeMax = 18f;
                }
                slots.Add(b.gameObject);
            }
            if (pages > 1)
            {
                var more = Flat.Button(root, (page + 1) + "/" + pages, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f), new Color(0.3f, 0.75f, 0.55f), Flat.Text, () => { page++; Fill(); }, 28f);
                ((RectTransform)more.transform).pivot = new Vector2(0.5f, 0.5f);
                slots.Add(more.gameObject);
            }
        }

        public bool IsOver(Vector2 screen) => Open && RectTransformUtility.RectangleContainsScreenPoint(root, screen, null);
    }
}
