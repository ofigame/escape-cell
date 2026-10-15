using System;
using System.Collections;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The opening scene after the studio logo (made in the engine, no video file): foi wakes in its glass cell, breaks out,
    /// fights through the fortress floors (robots, vanG, the tunnel), a last gate opens on light — and foi stands free in
    /// the Red Desert, looking at the horizon, under the game's logo. About 22 seconds the first time (a SKIP button
    /// cuts it short); every later start only the desert and the logo, 3 seconds. Built far from the game's own scene,
    /// filmed by its own camera, and all of it destroyed when it ends.
    /// </summary>
    public class IntroCinematic : MonoBehaviour
    {
        private const string SeenKey = "sb_intro_seen";
        private static readonly Vector3 Origin = new Vector3(900f, 0f, 900f);

        /// <summary>Whether the full opening has been seen (later starts show the short one).</summary>
        public static bool Seen
        {
            get => PlayerPrefs.GetInt(SeenKey, 0) == 1;
            set { PlayerPrefs.SetInt(SeenKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Set while it plays (the game holds its own music and prompts back).</summary>
        public static bool Playing { get; private set; }

        private Camera cam;
        private Transform root, foi;
        private FxSystem fx;
        private CanvasGroup ui, flash, logo;
        private Image flashImage;
        private readonly List<Material> mats = new List<Material>();
        private readonly List<Canvas> hidden = new List<Canvas>();
        private bool skip, ending;
        private Action done;
        private float shake;

        /// <param name="foiVisual">The hero's model (copied for the film).</param>
        /// <param name="full">The whole opening (else the 3-second desert and logo).</param>
        public static IntroCinematic Play(Transform foiVisual, FxSystem fx, bool full, Action done = null)
        {
            var go = new GameObject("IntroCinematic");
            var c = go.AddComponent<IntroCinematic>();
            c.fx = fx;
            c.done = done;
            Playing = true;
            c.Build(foiVisual);
            c.StartCoroutine(full ? c.Film() : c.Short());
            return c;
        }

        // ---------- Set-up ----------

        private Material M(Color c, float glow = 0f)
        {
            var m = MaterialFactory.Create(c, c * glow);
            mats.Add(m);
            return m;
        }

        private void Build(Transform foiVisual)
        {
            root = new GameObject("IntroSet").transform;
            root.position = Origin;
            var camGo = new GameObject("IntroCamera");
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 48f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            cam.depth = 50f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            foi = Instantiate(foiVisual.gameObject, root).transform;
            foi.name = "IntroFoi";
            foi.localPosition = Vector3.zero;
            foi.localRotation = Quaternion.identity;
            foi.localScale = foiVisual.lossyScale;
            foreach (var mb in foi.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false; // a still model, moved by the film

            // The overlay: black flashes, white flashes, the logo, SKIP.
            var canvas = UiFactory.CreateCanvas("IntroUi", out var scaler);
            canvas.sortingOrder = 190; // under the studio splash (200), over everything else
            canvas.transform.SetParent(transform, false);
            UiFactory.Fit(scaler);
            ui = canvas.gameObject.AddComponent<CanvasGroup>();
            var safe = UiFactory.Stretch("Safe", canvas.transform);
            safe.gameObject.AddComponent<SafeArea>();
            var f = UiFactory.Stretch("Flash", canvas.transform);
            flashImage = UiFactory.Fill(f, Color.black);
            flashImage.raycastTarget = false;
            flash = f.gameObject.AddComponent<CanvasGroup>();
            flash.alpha = 1f;
            var l = UiFactory.Box("LogoRoot", safe, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(1000f, 360f));
            l.pivot = new Vector2(0.5f, 0.5f);
            MenuArt.Logo(l, new Vector2(0.5f, 0.5f), Vector2.zero);
            logo = l.gameObject.AddComponent<CanvasGroup>();
            logo.alpha = 0f;
            var s = UiFactory.MakeButton(safe, Loc.T("intro.skip"), UiFactory.ButtonKind.Secondary, new Vector2(1f, 1f), new Vector2(-150f, -90f), new Vector2(220f, 84f), () => skip = true, 34f);
            ((RectTransform)s.transform).pivot = new Vector2(0.5f, 0.5f);
            // The game's own screens step aside while it plays (they draw over any camera).
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c != canvas && c.enabled && c.isRootCanvas && c.GetComponent<SplashScreen>() == null) { c.enabled = false; hidden.Add(c); }
        }

        // ---------- The film ----------

        private IEnumerator Film()
        {
            // 1. The cell in the dark: foi asleep, its eyes light up.
            var cellAt = Origin;
            var room = new GameObject("Cell").transform;
            room.SetParent(root, false);
            Shapes.Rounded("Floor", room, new Vector3(0f, -0.05f, 0f), new Vector3(9f, 0.1f, 9f), 0.02f, M(new Color(0.12f, 0.13f, 0.18f)));
            for (int i = -3; i <= 3; i++)
                Shapes.Rounded("Bar", room, new Vector3(i * 1.2f, 1.6f, 3.4f), new Vector3(0.12f, 3.2f, 0.12f), 0.04f, M(new Color(0.25f, 0.27f, 0.33f)));
            Shapes.Primitive(PrimitiveType.Sphere, "Lamp", room, new Vector3(0f, 3.4f, 1.5f), Vector3.one * 0.25f, M(new Color(0.5f, 0.85f, 1f), 3f));
            var glass = GlassCell.Create(cellAt + Vector3.up * 0.02f, 0.9f);
            glass.transform.SetParent(root, true);
            foi.position = cellAt;
            foi.rotation = Quaternion.Euler(0f, 180f, 0f);
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
            yield return Fade(1f, 0f, 0.8f);
            yield return Move(cellAt + new Vector3(-1.6f, 1.3f, -3.6f), cellAt + new Vector3(-0.9f, 1f, -2.4f), cellAt + Vector3.up * 0.7f, 2.6f);
            if (skip) { yield return End(); yield break; }

            // 2. The glass breaks; foi bursts out.
            glass.Shatter();
            AudioManager.PlaySfx(Sfx.Impact, 1f, 1.3f);
            yield return Wait(0.35f);
            fx?.Burst(cellAt + Vector3.up * 0.8f, new Color(0.6f, 0.9f, 1f), new Color(1.2f, 1.8f, 2.6f), 40, 6f);
            Shockwave.Create(cellAt, 2.2f, new Color(0.6f, 0.9f, 1f));
            shake = 0.4f;
            yield return Jump(foi, cellAt, cellAt + new Vector3(0f, 0f, -2.2f), 0.55f, 1.2f, cellAt + new Vector3(1.8f, 1.1f, -4.6f), cellAt + new Vector3(0f, 0.7f, -1f));
            yield return Wait(0.6f);
            if (skip) { yield return End(); yield break; }

            // 3. The fortress: a slash through the robots.
            yield return Fade(0f, 1f, 0.15f);
            var fort = Origin + new Vector3(60f, 0f, 0f);
            BuildFortress(fort, out var bots);
            foi.position = fort + new Vector3(0f, 0f, -2.5f);
            foi.rotation = Quaternion.identity;
            cam.backgroundColor = new Color(0.55f, 0.72f, 0.9f);
            yield return Fade(1f, 0f, 0.15f);
            yield return Run(foi, fort + new Vector3(0f, 0f, -2.5f), fort + new Vector3(0f, 0f, 0.4f), 1.3f, fort + new Vector3(-3.2f, 2.2f, -5f), fort + new Vector3(0f, 0.8f, 1f));
            SlashFx.Create(foi.position, fort + new Vector3(0f, 0f, 2f), new Color(1f, 0.85f, 0.3f), 2f);
            AudioManager.PlaySfx(Sfx.Impact, 1f, 0.9f);
            shake = 0.5f;
            foreach (var b in bots) StartCoroutine(Knock(b.transform, (b.transform.position - foi.position).normalized));
            fx?.Burst(fort + new Vector3(0f, 0.6f, 2f), new Color(1f, 0.5f, 0.3f), new Color(2.4f, 0.8f, 0.3f), 50, 7f);
            yield return Move(cam.transform.position, fort + new Vector3(-2.4f, 1.6f, -3.6f), fort + new Vector3(0f, 0.8f, 1.5f), 1.4f);
            if (skip) { yield return End(); yield break; }

            // 4. vanG: a stomp, foi's strike, it falls.
            yield return Fade(0f, 1f, 0.12f);
            var arena = Origin + new Vector3(120f, 0f, 0f);
            Shapes.Rounded("Ground", root, arena - Origin + new Vector3(0f, -0.05f, 2f), new Vector3(14f, 0.1f, 14f), 0.02f, M(new Color(0.72f, 0.66f, 0.6f)));
            var vang = Monster.Create(Monster.Kind.Cyclops, arena + new Vector3(0f, 0f, 3f), 10, new Color(0.62f, 0.2f, 0.28f), foi, 1);
            vang.transform.SetParent(root, true);
            vang.transform.localScale = Vector3.one * 1.6f;
            OwnedMaterials.OwnAllUnder(vang.gameObject);
            foi.position = arena + new Vector3(0f, 0f, -1.5f);
            foi.rotation = Quaternion.identity;
            cam.backgroundColor = new Color(0.32f, 0.2f, 0.3f);
            yield return Fade(1f, 0f, 0.12f);
            vang.Stomp();
            yield return Move(arena + new Vector3(2.6f, 1.2f, -5.2f), arena + new Vector3(1.8f, 1f, -4.4f), arena + new Vector3(0f, 1.8f, 2.4f), 1.1f);
            Shockwave.Create(arena + new Vector3(0f, 0f, 3f), 3.4f, new Color(1f, 0.4f, 0.3f));
            shake = 0.6f;
            AudioManager.PlaySfx(Sfx.Impact, 1f, 0.5f);
            yield return Jump(foi, foi.position, arena + new Vector3(0f, 0f, 1.6f), 0.45f, 1.6f, arena + new Vector3(-3f, 1.2f, -2.6f), arena + new Vector3(0f, 1.6f, 2.5f));
            SlashFx.Create(foi.position, arena + new Vector3(0f, 1.4f, 3f), new Color(1f, 0.85f, 0.3f), 2.4f);
            vang.Hit(0);
            vang.Defeat();
            fx?.Burst(arena + new Vector3(0f, 1.4f, 3f), new Color(1f, 0.85f, 0.4f), new Color(3f, 2f, 0.6f), 70, 8f);
            AudioManager.PlaySfx(Sfx.Win, 0.9f, 1f);
            shake = 0.7f;
            yield return Wait(1.4f);
            if (skip) { yield return End(); yield break; }

            // 5. The tunnel: rings rushing past.
            yield return Fade(0f, 1f, 0.12f);
            var tunnel = Origin + new Vector3(180f, 0f, 0f);
            var rings = BuildTunnel(tunnel);
            foi.position = tunnel;
            foi.rotation = Quaternion.identity;
            cam.backgroundColor = new Color(0.08f, 0.05f, 0.16f);
            cam.transform.position = tunnel + new Vector3(0f, 1.3f, -3.2f);
            cam.transform.LookAt(tunnel + new Vector3(0f, 0.8f, 3f));
            yield return Fade(1f, 0f, 0.12f);
            for (float t = 0f; t < 2.2f && !skip; t += Time.deltaTime)
            {
                foreach (var r in rings)
                {
                    r.localPosition += Vector3.back * Time.deltaTime * 26f;
                    if (r.localPosition.z < -6f) r.localPosition += Vector3.forward * 48f;
                }
                foi.position = tunnel + Vector3.up * Mathf.Abs(Mathf.Sin(t * 14f)) * 0.08f;
                yield return null;
            }
            if (skip) { yield return End(); yield break; }

            // 6. The last gate opens on light.
            yield return Fade(0f, 1f, 0.12f);
            var gate = Origin + new Vector3(240f, 0f, 0f);
            Shapes.Rounded("Ground", root, gate - Origin + new Vector3(0f, -0.05f, 2f), new Vector3(10f, 0.1f, 10f), 0.02f, M(new Color(0.3f, 0.3f, 0.36f)));
            Shapes.Rounded("Light", root, gate - Origin + new Vector3(0f, 2.2f, 4.6f), new Vector3(4f, 4.4f, 0.1f), 0.02f, M(new Color(1f, 0.85f, 0.6f), 4f));
            var left = Shapes.Rounded("DoorL", root, gate - Origin + new Vector3(-1f, 2.2f, 4.4f), new Vector3(2f, 4.4f, 0.3f), 0.05f, M(new Color(0.2f, 0.2f, 0.26f))).transform;
            var right = Shapes.Rounded("DoorR", root, gate - Origin + new Vector3(1f, 2.2f, 4.4f), new Vector3(2f, 4.4f, 0.3f), 0.05f, M(new Color(0.2f, 0.2f, 0.26f))).transform;
            foi.position = gate + new Vector3(0f, 0f, 1.5f);
            foi.rotation = Quaternion.identity;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            cam.transform.position = gate + new Vector3(0f, 1.1f, -2.6f);
            cam.transform.LookAt(gate + new Vector3(0f, 1.6f, 4.4f));
            yield return Fade(1f, 0f, 0.12f);
            AudioManager.PlaySfx(Sfx.Hop, 0.8f, 0.6f);
            for (float t = 0f; t < 2f && !skip; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 2f);
                left.localPosition = gate - Origin + new Vector3(-1f - 2.2f * k, 2.2f, 4.4f);
                right.localPosition = gate - Origin + new Vector3(1f + 2.2f * k, 2.2f, 4.4f);
                cam.transform.position = gate + new Vector3(0f, 1.1f, -2.6f + 1.4f * k);
                yield return null;
            }
            flashImage.color = Color.white;
            yield return Fade(0f, 1f, 0.35f);
            if (skip) { yield return End(); yield break; }

            // 7. Free: the Red Desert, the horizon, the logo.
            yield return Desert(10f);
            Seen = true;
            yield return End();
        }

        private IEnumerator Short()
        {
            yield return Desert(3f);
            yield return End();
        }

        private IEnumerator Desert(float seconds)
        {
            var d = Origin + new Vector3(320f, 0f, 0f);
            BuildDesert(d);
            foi.position = d + Vector3.up * 0.03f; // (the sand is lowered to 0 at its middle)
            foi.rotation = Quaternion.Euler(0f, 20f, 0f);
            cam.backgroundColor = new Color(0.98f, 0.62f, 0.45f);
            var look = d + new Vector3(0f, 1f, 6f);
            cam.transform.position = d + new Vector3(-2.2f, 1.4f, -3.2f);
            cam.transform.LookAt(look);
            yield return Fade(1f, 0f, 0.6f);
            flashImage.color = Color.black;
            AudioManager.PlaySfx(Sfx.Win, 0.7f, 0.8f);
            // The camera's path (relative to foi): behind it, rising over the scrap fields, gliding out to the farms on the
            // green land beyond the desert. The short opening (3 s) only drifts behind foi.
            var keys = seconds < 5f
                ? new[] { (0f, new Vector3(-2.2f, 1.4f, -3.2f), new Vector3(0f, 1f, 6f)), (1f, new Vector3(-0.6f, 2.4f, -5.2f), new Vector3(0f, 2.5f, 6f)) }
                : new[]
                {
                    (0f, new Vector3(-1.2f, 1.2f, -2.8f), new Vector3(0.2f, 0.9f, 4f)),
                    (0.28f, new Vector3(-0.6f, 2.2f, -4.4f), new Vector3(0.2f, 1.2f, 8f)),
                    (0.55f, new Vector3(3f, 7f, 14f), new Vector3(0f, 0f, 60f)),
                    (0.8f, new Vector3(2f, 11f, 40f), new Vector3(0f, 1f, 100f)),
                    (1f, new Vector3(0f, 13f, 58f), new Vector3(0f, 2f, 112f)),
                };
            for (float t = 0f; t < seconds && !skip; t += Time.deltaTime)
            {
                float k = t / seconds;
                int i = 0;
                while (i < keys.Length - 2 && k > keys[i + 1].Item1) i++;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(keys[i].Item1, keys[i + 1].Item1, k));
                cam.transform.position = d + Vector3.Lerp(keys[i].Item2, keys[i + 1].Item2, u);
                cam.transform.LookAt(d + Vector3.Lerp(keys[i].Item3, keys[i + 1].Item3, u));
                logo.alpha = Mathf.Clamp01((t - seconds * (seconds < 5f ? 0.3f : 0.62f)) / 0.8f);
                yield return null;
            }
        }

        private IEnumerator End()
        {
            if (ending) yield break;
            ending = true;
            flashImage.color = Color.black;
            for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime) { ui.alpha = 1f; flash.alpha = Mathf.Max(flash.alpha, t / 0.45f); yield return null; }
            cam.enabled = false;
            for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime) { ui.alpha = 1f - t / 0.45f; yield return null; }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            Playing = false;
            foreach (var c in hidden) if (c != null) c.enabled = true;
            if (root != null) Destroy(root.gameObject);
            foreach (var m in mats) if (m != null) Destroy(m);
            done?.Invoke();
        }

        private void LateUpdate()
        {
            if (skip && !ending) StartCoroutine(End());
            if (shake > 0f && cam != null)
            {
                cam.transform.position += UnityEngine.Random.insideUnitSphere * shake * 0.08f;
                shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 1.5f);
            }
        }

        // ---------- Pieces ----------

        private IEnumerator Wait(float s) { for (float t = 0f; t < s && !skip; t += Time.deltaTime) yield return null; }

        private IEnumerator Fade(float from, float to, float s)
        {
            for (float t = 0f; t < s; t += Time.deltaTime) { flash.alpha = Mathf.Lerp(from, to, t / s); yield return null; }
            flash.alpha = to;
        }

        private IEnumerator Move(Vector3 from, Vector3 to, Vector3 look, float s)
        {
            for (float t = 0f; t < s && !skip; t += Time.deltaTime)
            {
                cam.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / s));
                cam.transform.LookAt(look);
                yield return null;
            }
        }

        private IEnumerator Jump(Transform who, Vector3 from, Vector3 to, float s, float height, Vector3 camAt, Vector3 look)
        {
            var camFrom = cam.transform.position;
            for (float t = 0f; t < s && !skip; t += Time.deltaTime)
            {
                float k = t / s;
                who.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * height;
                cam.transform.position = Vector3.Lerp(camFrom, camAt, Mathf.SmoothStep(0f, 1f, k));
                cam.transform.LookAt(Vector3.Lerp(look, who.position, 0.4f));
                yield return null;
            }
            who.position = to;
        }

        private IEnumerator Run(Transform who, Vector3 from, Vector3 to, float s, Vector3 camAt, Vector3 look)
        {
            var camFrom = camAt + new Vector3(-1f, 0.4f, -1f);
            for (float t = 0f; t < s && !skip; t += Time.deltaTime)
            {
                float k = t / s;
                who.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 5f)) * 0.08f;
                cam.transform.position = Vector3.Lerp(camFrom, camAt, k);
                cam.transform.LookAt(look);
                yield return null;
            }
        }

        private IEnumerator Knock(Transform bot, Vector3 dir)
        {
            dir.y = 0f;
            var from = bot.position;
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                float k = t / 0.7f;
                bot.position = from + dir * k * 2.4f + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.2f;
                bot.Rotate(Vector3.right, 540f * Time.deltaTime, Space.Self);
                bot.localScale = Vector3.one * (1f - k * 0.9f);
                yield return null;
            }
            bot.gameObject.SetActive(false);
        }

        private void BuildFortress(Vector3 at, out List<GuardBot> bots)
        {
            var light = M(new Color(0.9f, 0.88f, 0.84f));
            var grout = M(new Color(0.7f, 0.68f, 0.64f));
            var wall = M(new Color(0.62f, 0.56f, 0.5f));
            var o = at - Origin;
            Shapes.Rounded("Grout", root, o + new Vector3(0f, -0.06f, 2f), new Vector3(12f, 0.1f, 14f), 0.02f, grout);
            for (int x = -3; x <= 3; x++)
                for (int z = -2; z <= 5; z++)
                    Shapes.Rounded("Tile", root, o + new Vector3(x * 1.7f, 0f, z * 1.7f), new Vector3(1.66f, 0.06f, 1.66f), 0.02f, light);
            for (int z = -2; z <= 5; z++)
                foreach (float side in new[] { -6.2f, 6.2f })
                    Shapes.Rounded("Wall", root, o + new Vector3(side, 0.6f, z * 1.7f), new Vector3(0.6f, 1.2f, 1.6f), 0.06f, wall);
            bots = new List<GuardBot>();
            var spots = new[] { new Vector3(-1.2f, 0f, 2.1f), new Vector3(0.4f, 0f, 2.5f), new Vector3(1.6f, 0f, 1.8f), new Vector3(-0.2f, 0f, 3.6f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var b = GuardBot.Build(root, null, i % 5, false, 6 + i);
                b.transform.position = at + spots[i];
                b.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                b.transform.localScale = Vector3.one * 0.9f;
                b.SetRaise(0.6f);
                bots.Add(b);
            }
        }

        private List<Transform> BuildTunnel(Vector3 at)
        {
            var list = new List<Transform>();
            var ring = M(new Color(0.55f, 0.4f, 1f), 1.6f);
            var floor = M(new Color(0.3f, 0.26f, 0.42f));
            var o = at - Origin;
            Shapes.Rounded("Lane", root, o + new Vector3(0f, -0.05f, 18f), new Vector3(3f, 0.1f, 48f), 0.02f, floor);
            for (int i = 0; i < 12; i++)
            {
                var r = new GameObject("Ring").transform;
                r.SetParent(root, false);
                r.localPosition = o + new Vector3(0f, 0f, -4f + i * 4f);
                Shapes.Rounded("L", r, new Vector3(-1.8f, 1.4f, 0f), new Vector3(0.18f, 2.8f, 0.18f), 0.06f, ring);
                Shapes.Rounded("R", r, new Vector3(1.8f, 1.4f, 0f), new Vector3(0.18f, 2.8f, 0.18f), 0.06f, ring);
                Shapes.Rounded("T", r, new Vector3(0f, 2.8f, 0f), new Vector3(3.8f, 0.18f, 0.18f), 0.06f, ring);
                list.Add(r);
            }
            return list;
        }

        // ---------- The Red Desert ----------

        /// <summary>Beyond the desert the land turns green: grass, fields in rows, farmhouses with red roofs, barns, fences,
        /// orchards — where foi will settle.</summary>
        private void BuildFarmland(Vector3 o)
        {
            var grass = M(new Color(0.42f, 0.66f, 0.3f));
            var grassDry = M(new Color(0.62f, 0.62f, 0.32f));
            var soil = M(new Color(0.46f, 0.3f, 0.18f));
            var crop = M(new Color(0.55f, 0.8f, 0.3f));
            var wheat = M(new Color(0.92f, 0.78f, 0.36f));
            var wall = M(new Color(0.95f, 0.92f, 0.85f));
            var roof = M(new Color(0.78f, 0.24f, 0.18f));
            var barnRed = M(new Color(0.7f, 0.18f, 0.14f));
            var wood = M(new Color(0.5f, 0.34f, 0.2f));
            var leaf = M(new Color(0.3f, 0.58f, 0.26f));
            var rng = new System.Random(23);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float H(float x, float z) => Dune(x, z) - Dune(0f, 0f);
            // The ground: dry grass at the desert's edge, then green (tiles laid along the dunes).
            for (float z = 64f; z < 160f; z += 8f)
                for (float x = -80f; x < 80f; x += 8f)
                {
                    var m = z < 76f ? (rng.NextDouble() < 0.5 ? grassDry : null) : z < 86f ? (rng.NextDouble() < 0.7 ? grassDry : grass) : grass;
                    if (m == null) continue;
                    var tile = Shapes.Rounded("Grass", root, o + new Vector3(x + 4f, H(x + 4f, z + 4f) + 0.05f, z + 4f), new Vector3(8.2f, 0.4f, 8.2f), 0.1f, m);
                    tile.transform.localRotation = Quaternion.Euler(R(-3f, 3f), 0f, R(-3f, 3f));
                }
            // Farms: a house, a barn, fields in rows, a fence, an orchard.
            var farms = new[] { new Vector3(-14f, 0f, 96f), new Vector3(16f, 0f, 104f), new Vector3(-30f, 0f, 120f), new Vector3(6f, 0f, 128f), new Vector3(32f, 0f, 132f) };
            foreach (var f in farms)
            {
                var farm = new GameObject("Farm").transform;
                farm.SetParent(root, false);
                farm.localPosition = o + new Vector3(f.x, H(f.x, f.z) + 0.3f, f.z);
                farm.localRotation = Quaternion.Euler(0f, R(-20f, 20f), 0f);
                // The house: white walls, a red pitched roof, a chimney.
                Shapes.Rounded("House", farm, new Vector3(0f, 1.2f, 0f), new Vector3(4f, 2.4f, 3.2f), 0.05f, wall);
                foreach (float s in new[] { -1f, 1f })
                    Shapes.Rounded("Roof", farm, new Vector3(0f, 2.9f, s * 0.85f), new Vector3(4.4f, 0.25f, 2.2f), 0.05f, roof).transform.localRotation = Quaternion.Euler(s * 32f, 0f, 0f);
                Shapes.Rounded("Chimney", farm, new Vector3(1.2f, 3.4f, 0.4f), new Vector3(0.4f, 1f, 0.4f), 0.05f, wood);
                // The barn.
                Shapes.Rounded("Barn", farm, new Vector3(6f, 1.5f, 0.5f), new Vector3(3.6f, 3f, 4.4f), 0.05f, barnRed);
                Shapes.Rounded("BarnRoof", farm, new Vector3(6f, 3.2f, 0.5f), new Vector3(3.9f, 0.4f, 4.7f), 0.05f, wood);
                // Fields in rows: soil with green and golden crops.
                for (int r = 0; r < 7; r++)
                {
                    Shapes.Rounded("Soil", farm, new Vector3(-1f + r * 1.2f, 0.05f, -6f), new Vector3(0.9f, 0.12f, 6f), 0.03f, soil);
                    Shapes.Rounded("Crop", farm, new Vector3(-1f + r * 1.2f, 0.3f, -6f), new Vector3(0.6f, 0.45f, 5.6f), 0.15f, r % 3 == 0 ? wheat : crop);
                }
                // A fence along the fields.
                for (int p = 0; p < 10; p++)
                    Shapes.Rounded("Post", farm, new Vector3(-2f + p * 1.1f, 0.45f, -9.4f), new Vector3(0.14f, 0.9f, 0.14f), 0.03f, wood);
                Shapes.Rounded("Rail", farm, new Vector3(3f, 0.65f, -9.4f), new Vector3(10f, 0.1f, 0.1f), 0.03f, wood);
                // An orchard beside it.
                for (int t = 0; t < 4; t++)
                {
                    var at = new Vector3(-4.5f - (t % 2) * 2.4f, 0f, -2f - (t / 2) * 2.6f);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Trunk", farm, at + new Vector3(0f, 0.7f, 0f), new Vector3(0.3f, 0.7f, 0.3f), wood);
                    Shapes.Primitive(PrimitiveType.Sphere, "Leaves", farm, at + new Vector3(0f, 1.9f, 0f), Vector3.one * R(1.6f, 2.2f), leaf);
                }
            }
            // Lone trees across the green.
            for (int i = 0; i < 40; i++)
            {
                float x = R(-70f, 70f), z = R(84f, 155f);
                var at = o + new Vector3(x, H(x, z) + 0.3f, z);
                Shapes.Primitive(PrimitiveType.Cylinder, "Trunk", root, at + new Vector3(0f, 0.9f, 0f), new Vector3(0.35f, 0.9f, 0.35f), wood);
                Shapes.Primitive(PrimitiveType.Sphere, "Leaves", root, at + new Vector3(0f, 2.4f, 0f), Vector3.one * R(1.8f, 2.8f), leaf);
            }
        }

        private static float Dune(float x, float z) =>
            Mathf.PerlinNoise(x * 0.045f + 3.1f, z * 0.045f + 7.7f) * 2.6f + Mathf.PerlinNoise(x * 0.14f, z * 0.14f) * 0.35f - 1.6f;

        private void BuildDesert(Vector3 at)
        {
            var o = at - Origin;
            // The sand: a gently rolling sheet, flat-shaded red.
            const int n = 92;
            const float step = 2f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    float wx = (x - n / 2) * step, wz = (z - 12) * step;
                    verts.Add(new Vector3(wx, Dune(wx, wz) - Dune(0f, 0f), wz));
                }
            for (int z = 0; z < n - 1; z++)
                for (int x = 0; x < n - 1; x++)
                {
                    int i = z * n + x;
                    tris.AddRange(new[] { i, i + n, i + 1, i + 1, i + n, i + n + 1 });
                }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            var sand = new GameObject("Sand");
            sand.transform.SetParent(root, false);
            sand.transform.localPosition = o;
            sand.AddComponent<MeshFilter>().sharedMesh = mesh;
            sand.AddComponent<MeshRenderer>().sharedMaterial = M(new Color(0.86f, 0.42f, 0.26f));
            sand.AddComponent<OwnedMesh>().mesh = mesh;

            // Scrap: rusty heaps, a wrecked robot, broken towers, rocks.
            var rust = M(new Color(0.42f, 0.24f, 0.16f));
            var dark = M(new Color(0.22f, 0.18f, 0.18f));
            var metal = M(new Color(0.5f, 0.48f, 0.46f));
            var rock = M(new Color(0.55f, 0.27f, 0.18f));
            var rng = new System.Random(11);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            for (int i = 0; i < 64; i++)
            {
                float x = R(-34f, 34f), z = R(5f, 70f);
                if (Mathf.Abs(x) < 4f && z < 14f) continue;
                var heap = new GameObject("Scrap").transform;
                heap.SetParent(root, false);
                heap.localPosition = o + new Vector3(x, Dune(x, z) - Dune(0f, 0f), z);
                heap.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                int parts = rng.Next(4, 9);
                for (int p = 0; p < parts; p++)
                {
                    var size = new Vector3(R(0.8f, 3f), R(0.5f, 2f), R(0.8f, 2.6f));
                    var piece = Shapes.Rounded("Part", heap, new Vector3(R(-1.2f, 1.2f), size.y * 0.4f, R(-1.2f, 1.2f)), size, 0.08f, p % 3 == 0 ? metal : p % 2 == 0 ? dark : rust);
                    piece.transform.localRotation = Quaternion.Euler(R(-25f, 25f), R(0f, 90f), R(-25f, 25f));
                }
            }
            for (int i = 0; i < 18; i++)
            {
                float x = R(-60f, 60f), z = R(20f, 120f);
                var r = Shapes.Rounded("Rock", root, o + new Vector3(x, Dune(x, z) - Dune(0f, 0f) + 0.4f, z), new Vector3(R(1.5f, 5f), R(1f, 4f), R(1.5f, 5f)), 0.4f, rock);
                r.transform.localRotation = Quaternion.Euler(R(-10f, 10f), R(0f, 90f), R(-10f, 10f));
            }
            // Fallen robots, toppled and half in the sand, along the way out.
            for (int i = 0; i < 14; i++)
            {
                float x = R(-16f, 16f), z = R(4f, 56f);
                if (Mathf.Abs(x) < 2.5f && z < 8f) continue;
                var bot = GuardBot.Build(root, new Color(0.45f, 0.28f, 0.2f), rng.Next(5), false, rng.Next(50));
                bot.transform.position = Origin + o + new Vector3(x, Dune(x, z) - Dune(0f, 0f) - R(0.15f, 0.4f), z);
                bot.transform.rotation = Quaternion.Euler(R(60f, 100f), R(0f, 360f), R(-20f, 20f));
                bot.transform.localScale = Vector3.one * R(2.4f, 4f);
                bot.enabled = false; // still: no idle sway
            }
            // A wrecked giant robot half in the sand, a broken tower.
            var wreck = new GameObject("Wreck").transform;
            wreck.SetParent(root, false);
            wreck.localPosition = o + new Vector3(7f, Dune(7f, 16f) - Dune(0f, 0f) - 0.4f, 16f);
            wreck.localRotation = Quaternion.Euler(0f, -30f, 18f);
            Shapes.Rounded("Body", wreck, new Vector3(0f, 1.4f, 0f), new Vector3(3f, 3f, 2.4f), 0.3f, rust);
            Shapes.Rounded("Head", wreck, new Vector3(0.4f, 3.4f, 0f), new Vector3(1.8f, 1.4f, 1.6f), 0.3f, dark);
            Shapes.Primitive(PrimitiveType.Sphere, "Eye", wreck, new Vector3(0.6f, 3.4f, -0.82f), Vector3.one * 0.4f, M(new Color(0.3f, 0.15f, 0.12f)));
            Shapes.Rounded("Arm", wreck, new Vector3(-2.2f, 0.6f, 0.4f), new Vector3(0.8f, 3f, 0.8f), 0.2f, metal).transform.localRotation = Quaternion.Euler(0f, 0f, 70f);
            var tower = new GameObject("Tower").transform;
            tower.SetParent(root, false);
            tower.localPosition = o + new Vector3(-12f, Dune(-12f, 30f) - Dune(0f, 0f), 30f);
            tower.localRotation = Quaternion.Euler(8f, 0f, -6f);
            Shapes.Rounded("Shaft", tower, new Vector3(0f, 3f, 0f), new Vector3(2f, 6f, 2f), 0.2f, dark);
            Shapes.Rounded("Top", tower, new Vector3(0.3f, 6.2f, 0f), new Vector3(2.6f, 0.6f, 2.6f), 0.2f, rust);
            BuildFarmland(o);
            // Two moons over the horizon and a hazy sun glow.
            Shapes.Primitive(PrimitiveType.Sphere, "MoonA", root, o + new Vector3(-30f, 34f, 180f), Vector3.one * 26f, M(new Color(1f, 0.92f, 0.82f), 1.2f));
            Shapes.Primitive(PrimitiveType.Sphere, "MoonB", root, o + new Vector3(24f, 22f, 170f), Vector3.one * 11f, M(new Color(0.95f, 0.75f, 0.7f), 1f));
            Shapes.Rounded("Haze", root, o + new Vector3(0f, 2f, 150f), new Vector3(400f, 6f, 1f), 0.1f, M(new Color(0.98f, 0.66f, 0.5f), 0.25f));
        }
    }
}
