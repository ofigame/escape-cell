using System;
using System.Collections.Generic;
using System.Reflection;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The prototype's live tuning panel (F1; Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 9.3): every number of
    /// <see cref="DuelCoreSettings"/>, read by reflection, a slider and a box each; changes act at once. Save / load to
    /// the json beside the game, back to the defaults.
    /// </summary>
    public class SettingsPanelV2
    {
        public bool Open;
        private Vector2 scroll;
        private DuelCoreSettings defaults;
        private readonly Dictionary<string, string> edits = new Dictionary<string, string>();
        private string note = "";
        private static readonly string[] Families = { "Kılıç", "Mızrak", "Balta", "Ağır" };
        private static readonly string[] Elements = { "Kar", "Ateş", "Yıldırım" };
        private static readonly string[] Tiers = { "K1", "K2", "K3", "K4", "K5" };

        public void Draw(float scale)
        {
            if (!Open) return;
            if (defaults == null) defaults = ScriptableObject.CreateInstance<DuelCoreSettings>();
            var s = DuelCoreSettings.Current;
            float w = Mathf.Min(Screen.width - 20f, 620f * scale), h = Screen.height - 40f;
            GUILayout.BeginArea(new Rect(10f, 20f, w, h), GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>" + Loc.T("v2.panel.title") + "</b>", Rich(), GUILayout.ExpandWidth(true));
            if (GUILayout.Button(Loc.T("v2.panel.save"), GUILayout.Width(90f * scale))) { DuelCoreSettings.Save(); note = Loc.T("v2.panel.saved") + " " + DuelCoreSettings.FilePath; }
            if (GUILayout.Button(Loc.T("v2.panel.load"), GUILayout.Width(90f * scale))) { note = DuelCoreSettings.Load() ? Loc.T("v2.panel.loaded") : Loc.T("v2.panel.nofile"); edits.Clear(); }
            if (GUILayout.Button(Loc.T("v2.panel.reset"), GUILayout.Width(110f * scale))) { DuelCoreSettings.Reset(); edits.Clear(); note = Loc.T("v2.panel.resetDone"); }
            if (GUILayout.Button("X", GUILayout.Width(36f * scale))) Open = false;
            GUILayout.EndHorizontal();
            if (note.Length > 0) GUILayout.Label(note);
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var f in typeof(DuelCoreSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var header = f.GetCustomAttribute<HeaderAttribute>();
                if (header != null) GUILayout.Label("<b>— " + header.header + " —</b>", Rich());
                var tip = f.GetCustomAttribute<TooltipAttribute>();
                string label = f.Name + (tip != null ? "  <i>(" + tip.tooltip + ")</i>" : "");
                if (f.FieldType == typeof(float)) { float v = (float)f.GetValue(s); float d = (float)f.GetValue(defaults); f.SetValue(s, FloatRow(f.Name, label, v, d)); }
                else if (f.FieldType == typeof(int)) { int v = (int)f.GetValue(s); f.SetValue(s, IntRow(label, v)); }
                else if (f.FieldType == typeof(bool)) { bool v = (bool)f.GetValue(s); f.SetValue(s, GUILayout.Toggle(v, " " + label)); }
                else if (f.FieldType == typeof(float[]))
                {
                    var a = (float[])f.GetValue(s); var da = (float[])f.GetValue(defaults);
                    GUILayout.Label(label, Rich());
                    var names = a.Length == 4 ? Families : a.Length == 3 ? Elements : a.Length == 5 ? Tiers : null;
                    for (int i = 0; i < a.Length; i++) a[i] = FloatRow(f.Name + i, "   " + (names != null ? names[i] : i.ToString()), a[i], i < da.Length ? da[i] : a[i]);
                }
                else if (f.FieldType == typeof(int[]))
                {
                    var a = (int[])f.GetValue(s);
                    GUILayout.Label(label, Rich());
                    for (int i = 0; i < a.Length; i++) a[i] = IntRow("   " + (a.Length == 3 ? Elements[i] : i.ToString()), a[i]);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private float FloatRow(string key, string label, float v, float def)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Rich(), GUILayout.Width(Screen.width * 0.5f));
            float max = Mathf.Max(1f, Mathf.Abs(def) * 3f);
            float nv = GUILayout.HorizontalSlider(v, 0f, max, GUILayout.MinWidth(80f));
            if (!edits.TryGetValue(key, out var txt) || Math.Abs(nv - v) > 1e-6f) txt = nv.ToString("0.###");
            string t2 = GUILayout.TextField(txt, GUILayout.Width(70f));
            edits[key] = t2;
            if (t2 != txt && float.TryParse(t2.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var typed)) nv = typed;
            GUILayout.EndHorizontal();
            return nv;
        }

        private static int IntRow(string label, int v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Rich(), GUILayout.Width(Screen.width * 0.5f));
            if (GUILayout.Button("-", GUILayout.Width(30f))) v--;
            GUILayout.Label(v.ToString(), GUILayout.Width(40f));
            if (GUILayout.Button("+", GUILayout.Width(30f))) v++;
            GUILayout.EndHorizontal();
            return Mathf.Max(0, v);
        }

        private static GUIStyle rich;
        private static GUIStyle Rich() => rich ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
    }
}
