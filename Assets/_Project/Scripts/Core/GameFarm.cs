using System.Collections;
using System.Collections.Generic;
using BrickMuseum.Build;
using BrickMuseum.City;
using BrickMuseum.Data;
using BrickMuseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace BrickMuseum.Core
{
    /// <summary>
    /// The fish farm in the city: four floating pens in the lake off the shore park, a pier out to them and a board on
    /// it. Tapping it opens its panel (buy, countdown, collect, the ad that hurries a batch). The fish in the pens grow
    /// in number with the batch and leap about when it is ready.
    /// </summary>
    public sealed partial class Game
    {
        private const string FarmTitle = "Balık Çiftliği";
        private Transform farm;
        private BoxCollider farmHit;
        private TextMeshPro farmState;
        private readonly List<(Transform t, Vector2 pen, float r, float speed, float phase)> farmFish = new List<(Transform, Vector2, float, float, float)>();
        private bool farmPanel;
        private float farmTick;
        private bool farmWasReady;

        private static readonly Vector2[] Pens = { new Vector2(-5.5f, -4f), new Vector2(5.5f, -4f), new Vector2(-5.5f, 7f), new Vector2(5.5f, 7f) };
        private const float PenR = 4.2f;

        /// <summary>Where the farm lies: in the lake south of the shore park (plan point).</summary>
        private static Vector2 FarmPlan => new Vector2(CityLayout.XOf(1), CityLayout.MaxZ + 17f);

        private void BuildFarm()
        {
            farm = new GameObject("FishFarm").transform;
            farm.SetParent(city.root, false);
            farm.localPosition = CityShape.Point(FarmPlan, 0f);
            farm.localRotation = Quaternion.Euler(0, CityShape.Angle(FarmPlan), 0);

            // the pier from the quay and the boards between the pens
            var wood = new MeshBuilder();
            wood.Box(new Vector3(0, CityMap.Lawn - 0.1f, -13f), new Vector3(3f, 0.2f, 8.4f));
            wood.Box(new Vector3(0, 0.3f, 1.5f), new Vector3(2.2f, 0.16f, 21f));
            wood.Box(new Vector3(0, 0.3f, -4f), new Vector3(7f, 0.16f, 1.6f));
            wood.Box(new Vector3(0, 0.3f, 7f), new Vector3(7f, 0.16f, 1.6f));
            for (float z = -16.5f; z <= -9.5f; z += 3.5f)
                foreach (float x in new[] { -1.3f, 1.3f }) wood.Box(new Vector3(x, -0.9f, z), new Vector3(0.3f, 2.4f, 0.3f));
            Part("Pier", wood, Mats.Lit(new Color(0.55f, 0.38f, 0.24f), 0.15f), false);

            // the pens: a black floating ring, a hand rail on posts
            var ring = new MeshBuilder();
            var rail = new MeshBuilder();
            foreach (var p in Pens)
            {
                var c = new Vector3(p.x, -0.15f, p.y);
                ring.Cylinder(c, PenR, PenR, 0.45f, 28, false, false);
                ring.Cylinder(c, PenR - 0.35f, PenR - 0.35f, 0.45f, 28, false, false);
                rail.Cylinder(c + Vector3.up * 1.05f, PenR, PenR, 0.1f, 28, false, false);
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    rail.Box(c + new Vector3(Mathf.Cos(a) * PenR, 0.75f, Mathf.Sin(a) * PenR), new Vector3(0.12f, 0.8f, 0.12f));
                }
            }
            Part("PenRings", ring, TwoSided(new Color(0.12f, 0.13f, 0.15f)), true);
            Part("PenRails", rail, TwoSided(new Color(0.92f, 0.92f, 0.9f)), true);

            // the board at the head of the pier
            var board = new MeshBuilder();
            board.Box(new Vector3(-2.4f, 1.5f, -15.5f), new Vector3(0.25f, 3f, 0.25f));
            board.Box(new Vector3(2.4f, 1.5f, -15.5f), new Vector3(0.25f, 3f, 0.25f));
            Part("BoardPosts", board, Mats.Lit(Color.white, 0.3f), true);
            var panel = new MeshBuilder();
            panel.Box(new Vector3(0, 2.9f, -15.5f), new Vector3(5.4f, 1.9f, 0.15f));
            Part("BoardPanel", panel, Mats.Lit(new Color(0.12f, 0.42f, 0.7f), 0.3f), true);
            foreach (float face in new[] { 0f, 180f })
            {
                float z = face == 0f ? -15.6f : -15.4f;
                var title = Label("BALIK ÇİFTLİĞİ", new Vector3(0, 3.35f, z), face, 0.5f);
                var state = Label("", new Vector3(0, 2.5f, z), face, 0.4f);
                state.color = new Color(1f, 0.92f, 0.5f);
                if (face == 0f) farmState = state;
                else farmStateBack = state;
                title.color = Color.white;
            }

            // the fish: silver backs in the water, six a pen
            var body = new MeshBuilder();
            body.Box(new Vector3(0, 0, 0), new Vector3(0.28f, 0.22f, 0.9f));
            body.Box(new Vector3(0, 0.12f, -0.05f), new Vector3(0.06f, 0.18f, 0.3f));   // the fin
            body.Box(new Vector3(0, 0.02f, -0.55f), new Vector3(0.08f, 0.24f, 0.24f)); // the tail
            var mesh = body.ToMesh("fish");
            var silver = Mats.Lit(new Color(0.72f, 0.76f, 0.8f), 0.7f);
            var rnd = new System.Random(3);
            foreach (var p in Pens)
                for (int i = 0; i < 6; i++)
                {
                    var go = new GameObject("Fish");
                    go.transform.SetParent(farm, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = silver;
                    farmFish.Add((go.transform, p, 1.2f + (float)rnd.NextDouble() * 2f, (0.5f + (float)rnd.NextDouble() * 0.6f) * (rnd.Next(2) == 0 ? 1 : -1), (float)rnd.NextDouble() * 10f));
                }

            farmHit = farm.gameObject.AddComponent<BoxCollider>();
            farmHit.center = new Vector3(0, 1f, -2f);
            farmHit.size = new Vector3(16f, 3f, 28f);
            RefreshFarm();
        }

        private TextMeshPro farmStateBack;

        private void Part(string name, MeshBuilder b, Material m, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(farm, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh(name);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        private static Material TwoSided(Color c)
        {
            var m = new Material(Mats.Lit(c, 0.3f));
            m.SetFloat("_Cull", 0f);
            return m;
        }

        private TextMeshPro Label(string text, Vector3 at, float face, float size)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(farm, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0, face, 0);
            var t = go.AddComponent<TextMeshPro>();
            LocPreprocessor.Attach(t);
            t.font = Fonts.Bold;
            t.text = text;
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(5f, size * 1.6f);
            t.enableAutoSizing = true; t.fontSizeMin = 1f; t.fontSizeMax = size * 10f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        /// <summary>The board's line for the farm's state.</summary>
        private void RefreshFarm()
        {
            string s = !FishFarm.Owned ? $"SATILIK · {FishFarm.Price} lira" : FishFarm.Ready ? "Balıklar hazır!" : $"Hasat: {Progress.LockText(FishFarm.Left)}";
            farmState.text = s; farmStateBack.text = s;
        }

        private bool FarmHitBy(Ray ray) => Physics.Raycast(ray, out var hit, 5000f) && hit.collider == farmHit;

        private void FocusFarm() => cityCam.Focus(farm.position, 75f);

        /// <summary>Every frame in the city: the fish swim (and leap when ready), the board, the HUD button, the countdown.</summary>
        private void FarmTick(float dt)
        {
            if (farm == null) return;
            bool owned = FishFarm.Owned, ready = FishFarm.Ready;
            int shown = !owned ? 0 : ready ? 6 : Mathf.Clamp(1 + Mathf.FloorToInt(FishFarm.Grown * 5f), 1, 6);
            for (int i = 0; i < farmFish.Count; i++)
            {
                var (t, pen, r, speed, phase) = farmFish[i];
                bool on = i % 6 < shown;
                if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
                if (!on) continue;
                float a = phase + time * speed;
                var pos = new Vector3(pen.x + Mathf.Cos(a) * r, -0.02f, pen.y + Mathf.Sin(a) * r);
                if (ready)
                {
                    // now and then a leap: a short arc out of the water
                    float cycle = Mathf.Repeat(time * 0.45f + phase, 3f);
                    if (cycle < 0.7f) pos.y += Mathf.Sin(cycle / 0.7f * Mathf.PI) * 1.1f;
                }
                t.localPosition = pos;
                t.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg + (speed > 0 ? 0f : 180f), 0);
            }
            farmTick -= dt;
            if (farmTick > 0f) return;
            farmTick = 1f;
            RefreshFarm();
            cu.farmBtn.gameObject.SetActive(ready && !cu.panel.activeSelf);
            if (farmPanel && cu.panel.activeSelf && cu.panelTitle.text == FarmTitle && (ready != farmWasReady || !ready)) ShowFarm();
            farmWasReady = ready;
        }

        // ---------- the phone's reminders: planned when the game goes to the background, cancelled when it comes back ----------

        private void OnApplicationPause(bool paused) { if (paused) Notify.Plan(); else Notify.CancelAll(); }
        private void OnApplicationQuit() => Notify.Plan();

        private void OpenFarm()
        {
            panelSlot = null;
            city.Select(null);
            Sfx.Play("pick", 0.6f);
            cu.help.transform.parent.gameObject.SetActive(false);
            farmPanel = true;
            ShowFarm();
        }

        private void ShowFarm()
        {
            farmWasReady = FishFarm.Ready;
            if (!FishFarm.Owned)
            {
                if (!FishFarm.Unlocked)
                    cu.ShowPanel(FarmTitle, $"Gölde balık yetiştir · oltadan çok kazandırır\nSeviye {FishFarm.UnlockLevel}'da açılır", null, ("Kapat", Neutral, ClosePanel));
                else
                    cu.ShowPanel(FarmTitle, $"Gölde balık yetiştir · oltadan çok kazandırır\nHer 2 saatte {FishFarm.Batch} balık · Balık Hali'nde sat\nParan: {FreeBuild.Wallet.Money} lira", null,
                        ($"Satın al · {FishFarm.Price} lira", FreeBuild.Wallet.Money >= FishFarm.Price ? Ui.Yellow : Neutral, BuyFarm), ("Kapat", Neutral, ClosePanel));
            }
            else if (FishFarm.Ready)
                cu.ShowPanel(FarmTitle, $"Balıklar hazır!\n{FishFarm.Batch} balık toplanmayı bekliyor", null, ("Topla", Ui.Yellow, CollectFarm), ("Kapat", Neutral, ClosePanel));
            else
                cu.ShowPanel(FarmTitle, $"Balıklar büyüyor\nHasat: {Progress.LockText(FishFarm.Left)} sonra\nSoğutucuda {Progress.Data.fish.Count} balık", null,
                    ("Reklam izle: hemen hazır", Ui.Yellow, () => StartCoroutine(HurryFarm())), ("Kapat", Neutral, ClosePanel));
        }

        private void BuyFarm()
        {
            if (FreeBuild.Wallet.Money < FishFarm.Price) { Toast($"Yeterli para yok · {FishFarm.Price} lira gerekli"); return; }
            if (!FishFarm.Buy()) return;
            Sfx.Play("win");
            RefreshFarm();
            UpdateCityHud();
            Toast("Balık çiftliği senin! İlk balıklar 2 saatte büyür", 2.5f);
            ShowFarm();
        }

        private IEnumerator HurryFarm()
        {
            ClosePanel();
            yield return TestAd();
            FishFarm.Hurry();
            RefreshFarm();
            OpenFarm();
        }

        private void CollectFarm()
        {
            var got = FishFarm.Collect(new System.Random());
            if (got.Count == 0) return;
            int worth = 0;
            foreach (var f in got) worth += f.price;
            Sfx.Play("win");
            RefreshFarm();
            Toast($"{got.Count} balık soğutucuda · Balık Hali'nde sat ({worth} lira)", 2.5f);
            ShowFarm();
        }
    }
}
