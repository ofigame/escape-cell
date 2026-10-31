using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The v2 prototype's screen (IMGUI; Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 2.3, 2.5, 5.5, 9.4): the two health bars with
    /// their ghost (the part lost melts after a moment), hearts, shield and bar; the series in big numbers; every blow's
    /// result over the struck one in its own words and colour, the damage; the advantage bar (who has the turn, by how
    /// many ms); the hit log (the last ten and why); the touch buttons (clickable with the mouse).
    /// </summary>
    public class DuelHudV2
    {
        private struct Label { public string text; public Color colour; public Vector3 at; public float born, size; }
        private readonly List<Label> labels = new List<Label>();
        private readonly List<string> log = new List<string>();
        private readonly float[] ghost = { 1f, 1f };
        private readonly float[] ghostAt = { 0f, 0f };
        private float seriesShown; private int seriesValue, seriesWho; private float seriesAt = -9f;
        private string banner = ""; private float bannerUntil; private Color bannerColour = Color.white;
        public readonly List<Rect> ButtonRects = new List<Rect>();

        /// <summary>The buttons pressed this frame (the touch layer; the fight reads and clears them).</summary>
        public FighterInput Pressed = FighterInput.None;
        public bool HoldBlock, HoldLeft, HoldRight;

        private static readonly Color HitC = new Color(1f, 0.85f, 0.3f), BlockC = new Color(0.6f, 0.85f, 1f), PerfectC = Color.white,
            DodgeC = new Color(0.5f, 1f, 0.7f), WhiffC = new Color(0.75f, 0.75f, 0.75f), ClashC = new Color(1f, 0.6f, 0.2f);

        public void Result(DuelEvent e, Vector3 overHead)
        {
            string word; Color c;
            switch (e.result)
            {
                case HitResult.Hit: word = e.move == MoveKind.PowerHit ? Loc.T("v2.res.power") : e.move == MoveKind.Knockdown ? Loc.T("v2.res.knockdown") : Loc.T("v2.res.hit"); c = HitC; break;
                case HitResult.Blocked: word = Loc.T("v2.res.blocked"); c = BlockC; break;
                case HitResult.Perfect: word = Loc.T("v2.res.perfect"); c = PerfectC; break;
                case HitResult.Dodged: word = Loc.T("v2.res.dodged"); c = DodgeC; break;
                case HitResult.Whiff: word = Loc.T("v2.res.whiff"); c = WhiffC; break;
                case HitResult.Clash: word = Loc.T("v2.res.clash"); c = ClashC; break;
                default: word = Loc.T("v2.res.immune"); c = WhiffC; break;
            }
            labels.Add(new Label { text = word + (e.damage >= 0.5f ? "  -" + Mathf.RoundToInt(e.damage) : ""), colour = c, at = overHead, born = Time.unscaledTime, size = e.result == HitResult.Perfect || e.move != MoveKind.Chain ? 34 : 26 });
            Log(Loc.T(e.who == 0 ? "v2.log.left" : "v2.log.right") + " " + e.move + " → " + e.result + (e.damage >= 0.5f ? " (" + Mathf.RoundToInt(e.damage) + ")" : "") + (string.IsNullOrEmpty(e.why) ? "" : " · " + e.why));
        }

        public void Float(string text, Color c, Vector3 at, float size = 28f) => labels.Add(new Label { text = text, colour = c, at = at, born = Time.unscaledTime, size = size });

        public void Series(int who, int n) { seriesWho = who; seriesValue = n; seriesAt = Time.unscaledTime; }

        public void Banner(string text, Color c, float seconds) { banner = text; bannerColour = c; bannerUntil = Time.unscaledTime + seconds; }

        public void Log(string line)
        {
            log.Add(line);
            if (log.Count > 10) log.RemoveAt(0);
        }

        public void Draw(DuelSim sim, Camera cam, DuelPrototypeV2 proto, DuelCoreSettings s, bool playerLeft, string topLine)
        {
            float k = Mathf.Max(0.6f, Screen.height / 1170f);
            ButtonRects.Clear();
            // the bars
            for (int i = 0; i < 2; i++)
            {
                var f = sim.F[i];
                float h = f.Health01;
                if (h < ghost[i]) { if (Time.unscaledTime - ghostAt[i] > s.ghostDelay) ghost[i] = Mathf.MoveTowards(ghost[i], h, Time.unscaledDeltaTime * 0.8f); }
                else { ghost[i] = h; ghostAt[i] = Time.unscaledTime; }
                if (h < ghost[i] - 0.001f && Time.unscaledTime - ghostAt[i] > s.ghostDelay + 2f) ghostAt[i] = Time.unscaledTime;
                float w = Screen.width * 0.42f, x = i == 0 ? 12f : Screen.width - 12f - w, y = 128f * k;
                Bar(new Rect(x, y, w, 22f * k), ghost[i], new Color(1f, 0.9f, 0.9f, 0.9f), i == 1);
                Bar(new Rect(x, y, w, 22f * k), h, h > 0.3f ? new Color(0.3f, 0.9f, 0.4f) : new Color(1f, 0.3f, 0.25f), i == 1, false);
                GUI.Label(new Rect(x, y - 26f * k, w, 26f * k), (i == 0 ? (playerLeft ? Loc.T("v2.side.me") : "Bot") : Loc.T("v2.side.rival")) + "  " + Mathf.CeilToInt(f.hp) + "/" + Mathf.RoundToInt(f.maxHp) + "   " + new string('♥', f.hearts), Style(16, Color.white, i == 1 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft));
                Bar(new Rect(x, y + 25f * k, w, 7f * k), f.shield / Mathf.Max(1f, s.shieldDurability), sim.Time < f.shieldBrokenUntil ? new Color(0.5f, 0.3f, 0.3f) : new Color(0.55f, 0.75f, 1f), i == 1);
                for (int p = 0; p < 3; p++)
                {
                    float part = Mathf.Clamp01(f.meter - p);
                    var r = new Rect(x + (i == 1 ? w - (p + 1) * (w / 3f) : p * (w / 3f)), y + 35f * k, w / 3f - 3f, 8f * k);
                    Bar(r, part, part >= 1f ? new Color(1f, 0.75f, 0.2f) : new Color(0.8f, 0.6f, 0.3f), i == 1);
                }
                PowerSlots(f, x, y + 47f * k, w, k, i == 1);
            }
            if (!string.IsNullOrEmpty(topLine)) GUI.Label(new Rect(0f, 60f * k, Screen.width, 36f * k), topLine, Style(22, Color.white, TextAnchor.MiddleCenter));
            // the advantage bar
            if (proto.ShowAdvantage)
            {
                int who = sim.Advantage(out float ms);
                var r = new Rect(Screen.width * 0.3f, 210f * k, Screen.width * 0.4f, 14f * k);
                GUI.DrawTexture(r, Tex(new Color(0f, 0f, 0f, 0.5f)));
                if (who >= 0)
                {
                    float share = Mathf.Clamp01(ms / 400f) * 0.5f;
                    var fill = who == 0 ? new Rect(r.center.x - r.width * share, r.y, r.width * share, r.height) : new Rect(r.center.x, r.y, r.width * share, r.height);
                    GUI.DrawTexture(fill, Tex(who == 0 ? new Color(0.4f, 0.8f, 1f) : new Color(1f, 0.5f, 0.4f)));
                }
                GUI.Label(new Rect(r.x, r.yMax, r.width, 22f * k), who < 0 ? Loc.T("v2.adv.even") : Loc.F(who == 0 ? "v2.adv.left" : "v2.adv.right", Mathf.RoundToInt(ms)), Style(14, Color.white, TextAnchor.MiddleCenter));
            }
            // the series
            if (Time.unscaledTime - seriesAt < 1.4f && seriesValue >= 2)
            {
                float a = 1f - Mathf.Clamp01((Time.unscaledTime - seriesAt - 0.9f) / 0.5f);
                float pop = 1f + 0.3f * Mathf.Clamp01(1f - (Time.unscaledTime - seriesAt) / 0.15f);
                GUI.Label(new Rect(0f, Screen.height * 0.2f, Screen.width, 90f * k), Loc.F("v2.series", seriesValue), Style(Mathf.RoundToInt(54 * pop), new Color(1f, 0.8f, 0.2f, a), TextAnchor.MiddleCenter));
            }
            if (Time.unscaledTime < bannerUntil) GUI.Label(new Rect(0f, Screen.height * 0.33f, Screen.width, 120f * k), banner, Style(60, bannerColour, TextAnchor.MiddleCenter));
            // the labels over the fighters
            for (int i = labels.Count - 1; i >= 0; i--)
            {
                var l = labels[i]; float age = Time.unscaledTime - l.born;
                if (age > 1.1f) { labels.RemoveAt(i); continue; }
                if (cam == null) continue;
                var sp = cam.WorldToScreenPoint(l.at + Vector3.up * age * 0.8f);
                if (sp.z < 0f) continue;
                var c = l.colour; c.a = 1f - Mathf.Clamp01((age - 0.7f) / 0.4f);
                GUI.Label(new Rect(sp.x - 200f, Screen.height - sp.y - 30f, 400f, 60f), l.text, Style(Mathf.RoundToInt(l.size), c, TextAnchor.MiddleCenter));
            }
            // the hit log
            if (proto.ShowLog)
            {
                var r = new Rect(8f, Screen.height * 0.62f, Screen.width * 0.62f, 18f * k * 10.5f);
                GUI.DrawTexture(r, Tex(new Color(0f, 0f, 0f, 0.35f)));
                for (int i = 0; i < log.Count; i++) GUI.Label(new Rect(r.x + 4f, r.y + i * 18f * k, r.width - 8f, 20f * k), log[i], Style(12, new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleLeft));
            }
            // the boxes: each fighter's reach along the line at its feet
            if (proto.ShowBoxes && cam != null) for (int i = 0; i < 2; i++) ReachBox(sim, sim.F[i], cam, proto, s);
            if (playerLeft) Buttons(sim, sim.F[0], k, s);
        }

        private void ReachBox(DuelSim sim, Fighter f, Camera cam, DuelPrototypeV2 proto, DuelCoreSettings s)
        {
            var fight = proto.Fight; if (fight == null) return;
            var a = cam.WorldToScreenPoint(fight.WorldOf(f.x)); var b = cam.WorldToScreenPoint(fight.WorldOf(f.x + f.facing * s.reach[f.family]));
            float y = Screen.height - a.y;
            var col = f.state == FighterState.Active ? new Color(1f, 0.2f, 0.2f, 0.7f) : f.state == FighterState.Windup ? new Color(1f, 0.8f, 0.2f, 0.6f) : new Color(1f, 1f, 1f, 0.35f);
            GUI.DrawTexture(new Rect(Mathf.Min(a.x, b.x), y - 4f, Mathf.Abs(b.x - a.x), 8f), Tex(col));
            GUI.Label(new Rect(a.x - 60f, y + 6f, 120f, 20f), f.state.ToString(), Style(12, Color.white, TextAnchor.MiddleCenter));
        }

        private void PowerSlots(Fighter f, float x, float y, float w, float k, bool right)
        {
            for (int p = 0; p < 3; p++)
            {
                float size = 26f * k;
                var r = new Rect(right ? x + w - (p + 1) * (size + 4f) : x + p * (size + 4f), y, size, size);
                GUI.DrawTexture(r, Tex(new Color(0f, 0f, 0f, 0.45f)));
                if (p < f.powers.Count)
                {
                    var e = f.powers[p];
                    GUI.DrawTexture(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), Tex(e == ElementKind.Snow ? new Color(0.6f, 0.85f, 1f) : e == ElementKind.Fire ? new Color(1f, 0.5f, 0.15f) : new Color(1f, 0.95f, 0.3f)));
                    GUI.Label(r, e == ElementKind.Snow ? "❄" : e == ElementKind.Fire ? "✹" : "ϟ", Style(16, Color.black, TextAnchor.MiddleCenter));
                }
            }
        }

        /// <summary>The touch layer (a phone's buttons, here clicked with the mouse): right thumb strike, shield, moves; left thumb walk, jump, dodge.</summary>
        private void Buttons(DuelSim sim, Fighter me, float k, DuelCoreSettings s)
        {
            float b = 96f * k, small = 70f * k, pad = 12f * k;
            float right = Screen.width - pad, bottom = Screen.height - pad - 30f * k;
            var strike = new Rect(right - b * 1.25f, bottom - b * 1.25f, b * 1.25f, b * 1.25f);
            var guard = new Rect(strike.x - small - pad, bottom - small, small, small);
            var special = new Rect(strike.x - small * 0.4f, strike.y - small - pad, small, small);
            var ulti = new Rect(guard.x - small * 0.2f, special.y - small * 0.4f, small, small);
            var ctx = new Rect(strike.x + strike.width * 0.15f, special.y - small - pad, strike.width * 0.85f, small * 0.8f);
            if (Btn(strike, Loc.T("v2.btn.strike"))) Pressed.attack = true;
            HoldBlock = Hold(guard, Loc.T("v2.btn.shield"));
            if (Btn(special, sim.Time < me.specialReadyAt ? Mathf.CeilToInt(me.specialReadyAt - sim.Time).ToString() : Loc.T("v2.btn.special"))) Pressed.special = true;
            if (Btn(ulti, me.meter >= s.meterMax ? Loc.T("v2.btn.ulti") : Mathf.FloorToInt(me.meter) + "/3")) Pressed.ulti = true;
            string ctxText = me.inChain && me.meter >= s.breakCost ? Loc.T("v2.btn.break") : sim.Time <= me.knockdownUntil ? Loc.T("v2.btn.knockdown") : sim.Time <= me.powerHitUntil ? Loc.T("v2.btn.power") : "";
            if (ctxText.Length > 0 && Btn(ctx, ctxText)) Pressed.context = true;
            float left = pad;
            var lb = new Rect(left, bottom - small, small, small);
            var rb = new Rect(left + small + pad, bottom - small, small, small);
            var jb = new Rect(left + (small + pad) * 0.5f, bottom - small * 2f - pad, small, small);
            var db = new Rect(left + (small + pad) * 2f, bottom - small, small, small);
            var hb = new Rect(left, bottom - small * 3f - pad * 3f, small * 0.8f, small * 0.8f);
            HoldLeft = Hold(lb, "◀"); HoldRight = Hold(rb, "▶");
            if (Btn(jb, Loc.T("v2.btn.jump"))) Pressed.jump = true;
            if (Btn(db, Loc.T("v2.btn.dodge"))) Pressed.dodge = true;
            if (me.hearts > 0 && Btn(hb, "♥" + me.hearts)) Pressed.heart = true;
            for (int p = 0; p < me.powers.Count; p++)
            {
                var pr = new Rect(guard.x - (p + 1) * (small * 0.75f + pad), bottom - small * 0.75f, small * 0.75f, small * 0.75f);
                if (Btn(pr, (p + 1).ToString())) Pressed.power = p;
            }
        }

        private bool Btn(Rect r, string text) { ButtonRects.Add(r); return GUI.Button(r, text); }
        private bool Hold(Rect r, string text)
        {
            ButtonRects.Add(r);
            GUI.Box(r, text);
            return Input.GetMouseButton(0) && r.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
        }

        public bool OverButton(Vector2 guiPos) { foreach (var r in ButtonRects) if (r.Contains(guiPos)) return true; return false; }

        private static void Bar(Rect r, float v, Color c, bool fromRight, bool back = true)
        {
            if (back) GUI.DrawTexture(r, Tex(new Color(0f, 0f, 0f, 0.55f)));
            float w = r.width * Mathf.Clamp01(v);
            GUI.DrawTexture(fromRight ? new Rect(r.xMax - w, r.y, w, r.height) : new Rect(r.x, r.y, w, r.height), Tex(c));
        }

        private static readonly Dictionary<Color, Texture2D> texes = new Dictionary<Color, Texture2D>();
        public static Texture2D Tex(Color c)
        {
            if (texes.TryGetValue(c, out var t) && t != null) return t;
            t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); texes[c] = t; return t;
        }

        private static readonly Dictionary<int, GUIStyle> styles = new Dictionary<int, GUIStyle>();
        public static GUIStyle Style(int size, Color c, TextAnchor anchor)
        {
            int px = Mathf.RoundToInt(size * Mathf.Max(0.6f, Screen.height / 1170f));
            int key = px * 31 + (int)anchor;
            if (!styles.TryGetValue(key, out var st)) { st = new GUIStyle(GUI.skin.label) { fontSize = px, alignment = anchor, fontStyle = FontStyle.Bold, richText = true, wordWrap = false, clipping = TextClipping.Overflow }; styles[key] = st; }
            st.normal.textColor = c;
            return st;
        }
    }
}
