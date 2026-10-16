using System;
using System.Collections.Generic;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace SquashBot.Kingdom
{
    /// <summary>
    /// foi's kingdom, played (Age of Empires IV style): landscape, its own slanted camera over the land, its own sun,
    /// the palace in the middle with foi on the throne before it. The game's scene, screens and lights step aside while
    /// it runs and come back when the player leaves. Opens once level 10 is won.
    /// </summary>
    public class KmMode : MonoBehaviour
    {
        public const int OpensAt = 10;

        public static bool Active { get; private set; }
        /// <summary>Something moves that needs every frame (the view gliding; later: people at work, battles).</summary>
        public static bool Busy { get; private set; }

        /// <summary>The kingdom is open: ten floors won (or a test build).</summary>
        public static bool Open => SaveData.TestMode || SaveData.UnlockedLevel >= OpensAt;

        private KmSave save;
        private KmLand land;
        private KmCamera view;
        private KmUi ui;
        private KmModels models;
        private Light sun;
        private Transform foi;
        private Action left;
        private bool quitting;
        private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        private readonly List<Camera> hiddenCameras = new List<Camera>();
        private readonly List<Light> hiddenLights = new List<Light>();
        private bool fogWas;
        private Color fogColourWas, ambientWas, skyWas, equatorWas, groundWas;
        private FogMode fogModeWas;
        private float fogStartWas, fogEndWas;
        private UnityEngine.Rendering.AmbientMode ambientModeWas;

        public static KmMode Enter(Transform foiVisual, Action onLeave)
        {
            var go = new GameObject("Kingdom");
            var m = go.AddComponent<KmMode>();
            m.left = onLeave;
            m.Begin(foiVisual);
            return m;
        }

        private void Begin(Transform foiVisual)
        {
            Active = true;
            save = KmSave.Current;
            bool first = !save.founded;
            save.Found();
            Screen.orientation = ScreenOrientation.LandscapeLeft;

            // The game's screens, cameras and lights step aside.
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.enabled && c.isRootCanvas) { c.enabled = false; hiddenCanvases.Add(c); }
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c.enabled) { c.enabled = false; hiddenCameras.Add(c); }
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.enabled && l.type == LightType.Directional) { l.enabled = false; hiddenLights.Add(l); }
            fogWas = RenderSettings.fog; fogColourWas = RenderSettings.fogColor; fogModeWas = RenderSettings.fogMode;
            fogStartWas = RenderSettings.fogStartDistance; fogEndWas = RenderSettings.fogEndDistance;
            ambientModeWas = RenderSettings.ambientMode; ambientWas = RenderSettings.ambientLight;
            skyWas = RenderSettings.ambientSkyColor; equatorWas = RenderSettings.ambientEquatorColor; groundWas = RenderSettings.ambientGroundColor;

            // The sun: warm, from the south-west, with hard shadows for the buildings.
            var sunGo = new GameObject("KmSun");
            sunGo.transform.SetParent(transform, false);
            sunGo.transform.rotation = Quaternion.Euler(48f, 30f, 0f);
            sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.25f;
            sun.shadows = GraphicsQuality.Current == GraphicsTier.Low ? LightShadows.None : LightShadows.Hard;
            sun.shadowStrength = 0.6f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.7f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.56f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.24f, 0.18f);

            view = KmCamera.Create(transform);
            var data = view.Cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = Application.isMobilePlatform ? AntialiasingMode.None : AntialiasingMode.FastApproximateAntialiasing;
            data.renderShadows = sun.shadows != LightShadows.None;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = view.Cam.backgroundColor;

            land = KmLand.Create(transform, view.Cam);
            models = new KmModels();
            BuildTown(foiVisual);

            view.Bounds = KmRegions.OpenBounds();
            view.Focus = new Vector2(save.camX, save.camZ);
            view.Dist = Mathf.Clamp(save.camDist, KmCamera.MinDist, KmCamera.MaxDist);
            view.Yaw = save.camYaw;
            view.Place();

            ui = KmUi.Create(transform);
            ui.BackPressed += Leave;
            ui.TpsPressed += () => ui.Toast(Loc.T("km.tps.soon"));
            Refresh();
            ui.Toast(Loc.T(first ? "km.founded" : "km.welcome"));
        }

        /// <summary>The buildings as saved, and foi on the throne.</summary>
        private void BuildTown(Transform foiVisual)
        {
            var town = new GameObject("Town").transform;
            town.SetParent(land.transform, false);
            Shapes.CastShadows = sun.shadows != LightShadows.None;
            foreach (var b in save.buildings)
            {
                if ((KmKind)b.kind != KmKind.Palace) continue;
                var at = new Vector3(b.x, KmLand.Height(b.x, b.z), b.z);
                var seat = models.Palace(town, at);
                seat.parent.localRotation = Quaternion.Euler(0f, b.rot, 0f);
                if (foiVisual != null && foi == null)
                {
                    foi = Instantiate(foiVisual.gameObject, seat).transform;
                    foi.name = "KmFoi";
                    foi.localPosition = Vector3.zero;
                    foi.localRotation = Quaternion.Euler(0f, 180f, 0f); // facing out over the kingdom (the throne looks south)
                    foi.localScale = foiVisual.lossyScale * 0.9f;
                    foi.gameObject.SetActive(true);
                    foreach (var mb in foi.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
                }
            }
            Shapes.CastShadows = false;
        }

        private void Refresh()
        {
            ui.SetResources(save.food, save.wood, save.stone, save.gold, 1, 5);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Leave(); return; }
            Busy = view.Moving;
            // Far out the haze opens up with the view.
            RenderSettings.fogStartDistance = 60f + view.Dist * 1.6f;
            RenderSettings.fogEndDistance = 140f + view.Dist * 2.4f;
            Refresh();
        }

        private void KeepView()
        {
            if (save == null || view == null) return;
            save.camX = view.Focus.x; save.camZ = view.Focus.y; save.camDist = view.Dist; save.camYaw = view.Yaw;
            save.Save();
        }

        private void Leave()
        {
            KeepView();
            Destroy(gameObject);
        }

        private void OnApplicationQuit() { quitting = true; KeepView(); }
        private void OnApplicationPause(bool paused) { if (paused) KeepView(); }

        private void OnDestroy()
        {
            Active = false;
            Busy = false;
            models?.Destroy();
            RenderSettings.fog = fogWas; RenderSettings.fogColor = fogColourWas; RenderSettings.fogMode = fogModeWas;
            RenderSettings.fogStartDistance = fogStartWas; RenderSettings.fogEndDistance = fogEndWas;
            RenderSettings.ambientMode = ambientModeWas; RenderSettings.ambientLight = ambientWas;
            RenderSettings.ambientSkyColor = skyWas; RenderSettings.ambientEquatorColor = equatorWas; RenderSettings.ambientGroundColor = groundWas;
            foreach (var c in hiddenCanvases) if (c != null) c.enabled = true;
            foreach (var c in hiddenCameras) if (c != null) c.enabled = true;
            foreach (var l in hiddenLights) if (l != null) l.enabled = true;
            Screen.orientation = ScreenOrientation.Portrait;
            if (!quitting) left?.Invoke();
        }
    }
}
