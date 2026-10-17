using System;
using System.Collections;
using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SquashBot.Visual
{
    /// <summary>
    /// A little stage far from the world where the duel chests are photographed (their pictures on the chest screen)
    /// and opened: the chest shakes harder and harder, the lid bursts open in a flash of light, the chest fades away
    /// and the gift rises in its place, turning slowly. Everything here sits on its own layer, lit by its own lights,
    /// seen by its own camera into a texture the screen shows.
    /// </summary>
    public class ChestStage : MonoBehaviour
    {
        private const int Layer = 30;
        private static readonly Vector3 Origin = new Vector3(-7000f, 0f, 7000f);

        private Camera cam;
        private Transform holder, chest, lid, gift, burst;
        private readonly List<Light> lights = new List<Light>();
        private readonly List<(Transform t, Vector3 v)> sparks = new List<(Transform, Vector3)>();
        private float spin;

        public RenderTexture View { get; private set; }

        private static ChestStage instance;
        private static readonly Dictionary<ChestTier, RenderTexture> thumbs = new Dictionary<ChestTier, RenderTexture>();

        public static ChestStage Get()
        {
            if (instance != null) return instance;
            var go = new GameObject("ChestStage");
            go.transform.position = Origin;
            instance = go.AddComponent<ChestStage>();
            instance.Build();
            go.SetActive(false);
            return instance;
        }

        private void Build()
        {
            var camGo = new GameObject("ChestCamera");
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << Layer;
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 30f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = Application.isMobilePlatform ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.transform.localPosition = new Vector3(0f, 1.9f, -5.3f);
            camGo.transform.LookAt(transform.position + new Vector3(0f, 0.55f, 0f));
            View = new RenderTexture(900, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "ChestView" };
            cam.targetTexture = View;
            cam.enabled = false;

            Lamp(LightType.Directional, Quaternion.Euler(40f, -30f, 0f), Vector3.zero, new Color(1f, 0.96f, 0.9f), 2f, 0f);
            Lamp(LightType.Directional, Quaternion.Euler(20f, 140f, 0f), Vector3.zero, new Color(0.7f, 0.8f, 1f), 0.9f, 0f);
            Lamp(LightType.Point, Quaternion.identity, new Vector3(0f, 1.4f, -1.6f), new Color(1f, 0.9f, 0.75f), 3f, 6f);

            holder = new GameObject("Holder").transform;
            holder.SetParent(transform, false);
        }

        private void Lamp(LightType type, Quaternion rot, Vector3 pos, Color c, float intensity, float range)
        {
            var l = new GameObject("Lamp").AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.localRotation = rot;
            l.transform.localPosition = pos;
            l.type = type; l.color = c; l.intensity = intensity; l.cullingMask = 1 << Layer;
            if (type == LightType.Point) l.range = range;
            l.shadows = LightShadows.None;
            lights.Add(l);
        }

        private static void OnLayer(GameObject g)
        {
            foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
        }

        private void Clear()
        {
            for (int i = holder.childCount - 1; i >= 0; i--) DestroyImmediate(holder.GetChild(i).gameObject);
            chest = lid = gift = burst = null;
            sparks.Clear();
        }

        /// <summary>A chest's picture for its card (taken once, kept).</summary>
        public static Texture Thumbnail(ChestTier t)
        {
            if (thumbs.TryGetValue(t, out var rt) && rt != null && rt.IsCreated()) return rt;
            var s = Get();
            s.gameObject.SetActive(true);
            s.Clear();
            var c = ChestModels.Build(s.holder, t, out _);
            c.localRotation = Quaternion.Euler(0f, -24f, 0f);
            c.localScale = Vector3.one * (t == ChestTier.Advanced ? 1.05f : t == ChestTier.Mid ? 1.15f : 1.25f);
            OnLayer(c.gameObject);
            rt = new RenderTexture(360, 360, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Chest " + t };
            rt.Create();
            s.cam.targetTexture = rt;
            s.cam.Render();
            s.cam.targetTexture = s.View;
            s.Clear();
            s.gameObject.SetActive(false);
            thumbs[t] = rt;
            return rt;
        }

        /// <summary>
        /// The opening: <paramref name="revealed"/> is called once the gift stands there. The stage keeps drawing (the
        /// gift turning) until <see cref="Stop"/>.
        /// </summary>
        public void Play(ChestTier t, ChestReward reward, Action revealed)
        {
            gameObject.SetActive(true);
            cam.enabled = true;
            Clear();
            chest = ChestModels.Build(holder, t, out lid);
            chest.localRotation = Quaternion.Euler(0f, -18f, 0f);
            chest.localScale = Vector3.one * (t == ChestTier.Advanced ? 1.1f : 1.2f);
            OnLayer(chest.gameObject);
            StopAllCoroutines();
            StartCoroutine(Opening(t, reward, revealed));
        }

        public void Stop()
        {
            StopAllCoroutines();
            Clear();
            cam.enabled = false;
            gameObject.SetActive(false);
        }

        private IEnumerator Opening(ChestTier t, ChestReward reward, Action revealed)
        {
            // It shakes, harder and harder.
            var basePos = chest.localPosition;
            for (float e = 0f; e < 1.5f; e += Time.unscaledDeltaTime)
            {
                float k = e / 1.5f;
                float amp = 0.01f + k * k * 0.06f;
                chest.localPosition = basePos + new Vector3(Mathf.Sin(e * 61f) * amp, Mathf.Abs(Mathf.Sin(e * 47f)) * amp * 0.6f, Mathf.Cos(e * 53f) * amp * 0.5f);
                chest.localRotation = Quaternion.Euler(0f, -18f, Mathf.Sin(e * 57f) * amp * 90f);
                yield return null;
            }
            chest.localPosition = basePos;
            chest.localRotation = Quaternion.Euler(0f, -18f, 0f);
            // The lid bursts open in a flash.
            var c = ChestModels.StrapColour(t);
            burst = Shapes.Primitive(PrimitiveType.Sphere, "Flash", holder, new Vector3(0f, 0.7f, 0f), Vector3.one * 0.1f,
                MaterialFactory.CreateTransparent(new Color(1f, 0.95f, 0.75f, 0.85f), new Color(2.2f, 1.9f, 1.2f))).transform;
            OnLayer(burst.gameObject);
            var sparkMat = MaterialFactory.Create(Color.white, Color.Lerp(new Color(2.4f, 2f, 1f), c * 2.5f, 0.35f));
            for (int i = 0; i < 26; i++)
            {
                var s = Shapes.Primitive(PrimitiveType.Cube, "Spark", holder, new Vector3(0f, 0.75f, 0f), Vector3.one * UnityEngine.Random.Range(0.03f, 0.07f), sparkMat).transform;
                OnLayer(s.gameObject);
                var v = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(0.8f, 2.2f), UnityEngine.Random.Range(-0.6f, 0.4f));
                sparks.Add((s, v * 1.6f));
            }
            for (float e = 0f; e < 0.35f; e += Time.unscaledDeltaTime)
            {
                float k = e / 0.35f;
                lid.localRotation = Quaternion.Euler(-115f * (1f - (1f - k) * (1f - k)), 0f, 0f);
                burst.localScale = Vector3.one * Mathf.Lerp(0.1f, 2.2f, k);
                MoveSparks();
                yield return null;
            }
            // The chest fades away (shrinks into the light) and the gift rises.
            gift = new GameObject("Gift").transform;
            gift.SetParent(holder, false);
            if (reward != null && reward.weapon != null)
            {
                var w = WeaponModels.Build(gift, reward.weapon, 1f);
                float len = reward.weapon.kind == WeaponKind.Spear ? 1.05f : 0.85f;
                float l = len * (reward.weapon.Special ? reward.weapon.size : 1f); // (a rank or chest weapon is built bigger)
                w.localPosition = new Vector3(0f, -l * 0.5f, 0f);
                gift.localScale = Vector3.one * (2.1f / l);
                if (reward.weapon.kind == WeaponKind.Gun) w.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            else { CoinHeap(gift); gift.localScale = Vector3.one * 1.6f; }
            OnLayer(gift.gameObject);
            gift.localPosition = new Vector3(0f, 0.3f, 0f);
            var giftScale = gift.localScale;
            for (float e = 0f; e < 0.5f; e += Time.unscaledDeltaTime)
            {
                float k = e / 0.5f;
                chest.localScale = Vector3.one * Mathf.Lerp(1.15f, 0f, k * k);
                burst.localScale = Vector3.one * Mathf.Lerp(2.2f, 0f, k);
                gift.localScale = giftScale * EaseOutBack(k);
                gift.localPosition = new Vector3(0f, Mathf.Lerp(0.3f, 0.6f, k), 0f);
                MoveSparks();
                yield return null;
            }
            chest.gameObject.SetActive(false);
            burst.gameObject.SetActive(false);
            revealed?.Invoke();
            while (true)
            {
                spin += Time.unscaledDeltaTime * 50f;
                gift.localRotation = Quaternion.Euler(0f, spin, 0f);
                gift.localPosition = new Vector3(0f, 0.6f + Mathf.Sin(spin * 0.05f) * 0.04f, 0f);
                MoveSparks();
                yield return null;
            }
        }

        private void MoveSparks()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < sparks.Count; i++)
            {
                var (t, v) = sparks[i];
                if (t == null) continue;
                v += Vector3.down * 3.2f * dt;
                t.localPosition += v * dt;
                t.localScale *= 1f - dt * 0.6f;
                sparks[i] = (t, v);
            }
        }

        private static void CoinHeap(Transform parent)
        {
            var face = MaterialFactory.CreateSurface(new Color(1f, 0.8f, 0.25f), new Color(0.5f, 0.32f, 0.05f), 0.8f, 0.9f);
            var rim = MaterialFactory.CreateSurface(new Color(0.92f, 0.62f, 0.12f), new Color(0.3f, 0.18f, 0.02f), 0.75f, 0.9f);
            var mark = MaterialFactory.CreateSurface(new Color(1f, 0.9f, 0.5f), new Color(0.6f, 0.45f, 0.1f), 0.85f, 0.9f);
            var spots = new[]
            {
                (new Vector3(-0.22f, -0.2f, 0f), new Vector3(0f, 0f, 0f), 5), (new Vector3(0.24f, -0.2f, 0.05f), new Vector3(0f, 30f, 0f), 3),
                (new Vector3(0f, 0.1f, -0.1f), new Vector3(70f, 15f, 0f), 1), (new Vector3(0.18f, 0.25f, 0.05f), new Vector3(80f, -30f, 10f), 1),
            };
            foreach (var (at, rot, n) in spots)
                for (int i = 0; i < n; i++)
                {
                    var c = new GameObject("Coin").transform;
                    c.SetParent(parent, false);
                    c.localPosition = at + Vector3.up * i * 0.06f;
                    c.localRotation = Quaternion.Euler(rot + new Vector3(0f, i * 20f, 0f));
                    c.localScale = Vector3.one * 0.42f;
                    Shapes.Primitive(PrimitiveType.Cylinder, "Rim", c, Vector3.zero, new Vector3(1f, 0.08f, 1f), rim);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Face", c, Vector3.zero, new Vector3(0.82f, 0.095f, 0.82f), face);
                    Shapes.Rounded("Mark", c, new Vector3(0f, 0.1f, 0f), new Vector3(0.18f, 0.03f, 0.5f), 0.04f, mark);
                }
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
