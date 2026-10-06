using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SquashBot.Visual
{
    public enum ViewMode
    {
        Isometric,
        Perspective
    }

    public enum CameraStyle
    {
        /// <summary>Gentle sway around the base angle while playing.</summary>
        Gameplay,
        /// <summary>Slow continuous orbit behind the menus.</summary>
        MenuOrbit,
        /// <summary>A celebratory sweep around the platform.</summary>
        Victory,
        /// <summary>No sway at all (building in the city: the cell under the finger must stay put).</summary>
        Still
    }

    /// <summary>
    /// A living camera: frames the platform from a true isometric angle (or low-FOV perspective),
    /// flies in at level start, sways, punches on impacts, zooms on close calls and orbits in menus.
    /// Also owns the painted backdrop and the post effects (bloom, menu blur/dim).
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        private const float IsoPitch = 35.264f; // true isometric: equal foreshortening on both grid axes
        private const float PerspectivePitch = 42f;
        private const float BaseYaw = 45f;
        private const float PerspectiveFov = 24f;
        private const float OrthoDistance = 40f;
        private const float IntroDuration = 1.3f;

        public Camera Cam { get; private set; }
        public ViewMode Mode { get; private set; } = ViewMode.Isometric;

        private int gridWidth = 3;
        private int gridHeight = 3;
        private float lastAspect;
        private Vector3 center;
        private float viewHalfHeight = 3f;
        private float perspectiveDistance = 20f;

        private CameraStyle style = CameraStyle.Gameplay;
        private float time;
        private float orbitYaw;
        private float introT = 1f;
        private float shake;
        private float punch;
        private float focusTime;
        private float focusDuration;
        private Vector3 focusPoint;

        private Transform background;
        private Material backgroundMaterial;
        private Volume menuVolume;
        private bool chasing;
        private float showcase, showcaseTarget;
        private float frameLift, frameLiftTarget, frameZoom = 1f, frameZoomTarget = 1f;
        private Transform showcaseSubject;
        private Transform followTarget;
        private int windowW, windowH;
        private Vector3 followPoint;
        private Vector3 chasePosition;
        private Quaternion chaseRotation;
        private float menuFocusTarget;
        private Vector3 userPan;
        private float userZoom = 1f;

        public static CameraRig Create(ViewMode mode)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.BgBottom;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.allowMSAA = true;

            var rig = cam.gameObject.AddComponent<CameraRig>();
            rig.Cam = cam;
            rig.Mode = mode;
            rig.SetupPostProcessing();
            rig.SetupBackground();
            return rig;
        }

        // ---------- Setup ----------

        private void SetupPostProcessing()
        {
            var cameraData = Cam.GetUniversalAdditionalCameraData();
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = true;
                // Phones rely on the pipeline's MSAA alone; SMAA on top costs frames that make input feel late.
                cameraData.antialiasing = Mobile ? AntialiasingMode.None : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cameraData.antialiasingQuality = AntialiasingQuality.High;
            }

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(1.1f);
            bloom.scatter.Override(0.6f);
            bloom.highQualityFiltering.Override(!Mobile);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.6f);

            CreateVolume("PostFX", profile, 0f, 1f);

            // Menu focus: blur and dim the scene so cards and buttons stand out.
            var menuProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var dof = menuProfile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(0f);
            dof.gaussianEnd.Override(0.5f);
            dof.gaussianMaxRadius.Override(1.5f);
            dof.highQualitySampling.Override(!Mobile);
            var color = menuProfile.Add<ColorAdjustments>(true);
            color.postExposure.Override(-0.9f);
            color.saturation.Override(-20f);

            menuVolume = CreateVolume("MenuFX", menuProfile, 1f, 0f);
        }

        private Volume CreateVolume(string name, VolumeProfile profile, float priority, float weight)
        {
            var volume = new GameObject(name).AddComponent<Volume>();
            volume.transform.SetParent(transform, false);
            volume.isGlobal = true;
            volume.priority = priority;
            volume.weight = weight;
            volume.sharedProfile = profile;
            return volume;
        }

        private void SetupBackground()
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Background";
            Destroy(quad.GetComponent<Collider>());
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            background = quad.transform;
            background.SetParent(transform, false);
        }

        private static bool Mobile => Application.isMobilePlatform;

        // ---------- Public controls ----------

        /// <summary>Repaint the backdrop after the world theme changed.</summary>
        public void RefreshTheme()
        {
            Cam.backgroundColor = Palette.BgBottom;
            RegenerateBackground();
        }

        public void SetMode(ViewMode mode)
        {
            Mode = mode;
            Refit();
        }

        public void Frame(int width, int height)
        {
            gridWidth = width;
            gridHeight = height;
            followTarget = null;
            userPan = Vector3.zero;
            userZoom = 1f;
            Refit();
        }

        /// <summary>Player-controlled panning (world units on the ground) and zoom (<1 = closer), reset by <see cref="Frame"/>.</summary>
        public void SetUserView(Vector3 pan, float zoom)
        {
            userPan = new Vector3(pan.x, 0f, pan.z);
            userZoom = zoom;
        }

        /// <summary>The camera's current pose (for blending into a chase view).</summary>
        public Pose CurrentPose => new Pose(transform.position, transform.rotation);

        public void SetStyle(CameraStyle newStyle)
        {
            if (newStyle == CameraStyle.Victory) orbitYaw = 0f;
            style = newStyle;
        }

        /// <summary>Swoop in from high above and to the side.</summary>
        public void PlayIntro() => introT = 0f;

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        /// <summary>Brief zoom-in kick (impacts, pickups).</summary>
        public void Punch(float amount) => punch = Mathf.Max(punch, amount);

        /// <summary>Lean in toward a point for a moment (close calls).</summary>
        public void Focus(Vector3 worldPoint, float duration)
        {
            focusPoint = worldPoint;
            focusDuration = duration;
            focusTime = duration;
        }

        /// <summary>
        /// For platforms too big to show whole: frame a window of <paramref name="width"/> x <paramref name="height"/>
        /// tiles and glide along with <paramref name="target"/>, never showing much past the platform's ends.
        /// Call after <see cref="Frame"/>; Frame turns it off again.
        /// </summary>
        public void Follow(Transform target, int width, int height)
        {
            followTarget = target;
            windowW = width;
            windowH = height;
            followPoint = FollowGoal();
            Refit();
        }

        private Vector3 FollowGoal()
        {
            var p = followTarget.position;
            float hx = (windowW - 1) * 0.5f, hz = (windowH - 1) * 0.5f;
            // Along an axis the platform is narrower than the window, just stay centred on it.
            float x = gridWidth <= windowW ? (gridWidth - 1) * 0.5f : Mathf.Clamp(p.x, hx, gridWidth - 1 - hx);
            float z = gridHeight <= windowH ? (gridHeight - 1) * 0.5f : Mathf.Clamp(p.z, hz, gridHeight - 1 - hz);
            return new Vector3(x, 0f, z);
        }

        /// <summary>
        /// Hand the camera to a chase view (the tunnel runner): a perspective camera at the given pose.
        /// Shake and punch still apply. <see cref="EndChase"/> returns to the platform view.
        /// </summary>
        public void Chase(Vector3 position, Quaternion rotation, float fov)
        {
            chasing = true;
            chasePosition = position;
            chaseRotation = rotation;
            Cam.orthographic = false;
            Cam.fieldOfView = fov;
        }

        public void EndChase()
        {
            if (!chasing) return;
            chasing = false;
            Refit();
        }

        /// <summary>
        /// Lean in on <paramref name="subject"/> (the robot in the menu and garage) and lift it into the upper part of
        /// the screen, above the cards. <paramref name="amount"/> 0 = normal view, 1 = close-up.
        /// </summary>
        /// <summary>
        /// Raises the platform into the upper part of the screen (0 = centred) and zooms (1 = normal), for screens whose
        /// panel covers the lower half (the roof camp).
        /// </summary>
        public void FrameUpper(float lift, float zoom)
        {
            frameLiftTarget = lift;
            frameZoomTarget = zoom;
        }

        public void Showcase(Transform subject, float amount)
        {
            if (subject != null) showcaseSubject = subject;
            showcaseTarget = amount;
        }

        /// <summary>Blur and darken the scene behind menus.</summary>
        public void SetMenuFocus(bool on) => menuFocusTarget = on ? 1f : 0f;

        // ---------- Framing ----------

        private void Refit()
        {
            if (chasing) return; // the chase view owns the camera until EndChase
            bool aspectChanged = !Mathf.Approximately(lastAspect, Cam.aspect);
            lastAspect = Cam.aspect;

            center = new Vector3((gridWidth - 1) * 0.5f, 0f, (gridHeight - 1) * 0.5f);
            bool follow = followTarget != null;
            int fw = follow ? windowW : gridWidth, fh = follow ? windowH : gridHeight;
            var footCenter = new Vector3((fw - 1) * 0.5f, 0f, (fh - 1) * 0.5f);
            if (follow) center = followPoint;

            // Footprint of the platform (plus room for blocks and the pillar) at the base angle.
            var rotation = Quaternion.Euler(BasePitch, BaseYaw, 0f);
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            float halfW = 0f, halfH = 0f;
            for (int i = 0; i < 4; i++)
            {
                float cx = (i & 1) == 0 ? -0.7f : fw - 0.3f;
                float cz = (i & 2) == 0 ? -0.7f : fh - 0.3f;
                foreach (float cy in new[] { -0.6f, 1.2f })
                {
                    var offset = new Vector3(cx, cy, cz) - footCenter;
                    halfW = Mathf.Max(halfW, Mathf.Abs(Vector3.Dot(offset, right)));
                    halfH = Mathf.Max(halfH, Mathf.Abs(Vector3.Dot(offset, up)));
                }
            }

            // Tall phone screens need vertical room for the HUD; wide screens can show the platform bigger.
            float hudRoom = Cam.aspect < 1f ? 1.7f : 1.3f;
            viewHalfHeight = Mathf.Max(halfH * hudRoom, halfW / Cam.aspect) * 1.14f;
            perspectiveDistance = viewHalfHeight / Mathf.Tan(PerspectiveFov * 0.5f * Mathf.Deg2Rad) * 1.08f;

            Cam.orthographic = Mode == ViewMode.Isometric;
            if (!Cam.orthographic) Cam.fieldOfView = PerspectiveFov;

            if (aspectChanged || backgroundMaterial == null) RegenerateBackground();
            Apply();
        }

        private float BasePitch => Mode == ViewMode.Isometric ? IsoPitch : PerspectivePitch;

        private void LateUpdate()
        {
            if (!Mathf.Approximately(lastAspect, Cam.aspect)) Refit();

            float dt = Time.unscaledDeltaTime;
            time += dt;
            introT = Mathf.Min(1f, introT + dt / IntroDuration);
            shake = Mathf.MoveTowards(shake, 0f, dt * 2.5f);
            punch = Mathf.MoveTowards(punch, 0f, dt * 3f);
            focusTime = Mathf.Max(0f, focusTime - dt);

            switch (style)
            {
                case CameraStyle.MenuOrbit: orbitYaw += dt * 7f; break;
                case CameraStyle.Victory: orbitYaw = Mathf.Lerp(orbitYaw, 360f, dt * 1.2f); break;
                default: orbitYaw = Mathf.LerpAngle(orbitYaw, 0f, dt * 2f); break;
            }

            menuVolume.weight = Mathf.MoveTowards(menuVolume.weight, menuFocusTarget, dt * 3f);
            showcase = Mathf.MoveTowards(showcase, showcaseTarget, dt * 2.5f);
            frameLift = Mathf.MoveTowards(frameLift, frameLiftTarget, dt * 1.5f);
            frameZoom = Mathf.MoveTowards(frameZoom, frameZoomTarget, dt * 1.5f);
            if (followTarget != null)
            {
                followPoint = Vector3.Lerp(followPoint, FollowGoal(), 1f - Mathf.Exp(-Time.deltaTime * 4f));
                center = followPoint;
            }
            if (chasing)
            {
                var jitter = Random.insideUnitCircle * shake * 0.08f;
                transform.rotation = chaseRotation;
                transform.position = chasePosition + transform.right * jitter.x + transform.up * jitter.y - transform.forward * (punch * 0.25f);
                FitBackground();
                return;
            }
            Apply();
        }

        private void Apply()
        {
            float intro = 1f - EaseOutCubic(introT);
            float sway = style == CameraStyle.Gameplay ? 1f : style == CameraStyle.Still ? 0f : 0.4f;

            float yaw = BaseYaw + orbitYaw + intro * 70f + Mathf.Sin(time * 0.35f) * 6f * sway;
            float pitch = BasePitch + intro * 25f + Mathf.Sin(time * 0.27f) * 1.5f * sway;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            // Zoom: >1 shows more. Intro starts wide, punches and focus lean in.
            float focus = focusDuration > 0f ? Mathf.Sin(Mathf.Clamp01(focusTime / focusDuration) * Mathf.PI) : 0f;
            float zoom = (1f + intro * 0.7f) * (1f - punch * 0.06f) * (1f - focus * 0.12f);
            var target = Vector3.Lerp(center + userPan, focusPoint, focus * 0.25f);
            zoom *= userZoom;
            float ease = showcase * showcase * (3f - 2f * showcase);
            if (ease > 0.001f && showcaseSubject != null)
            {
                target = Vector3.Lerp(target, showcaseSubject.position + Vector3.up * 0.35f, ease);
                // Close-up framing is absolute (about the robot plus a little floor), whatever the platform size.
                zoom = Mathf.Lerp(zoom, 1.25f / Mathf.Max(0.5f, viewHalfHeight), ease);
            }

            var jitter = Random.insideUnitCircle * shake * 0.12f;
            zoom *= frameZoom;
            var lift = transform.up * (viewHalfHeight * zoom * (0.06f - 0.42f * ease - frameLift));
            var offset = transform.right * jitter.x + transform.up * jitter.y;

            if (Cam.orthographic)
            {
                Cam.orthographicSize = viewHalfHeight * zoom;
                transform.position = target - transform.forward * OrthoDistance + lift + offset;
            }
            else
            {
                transform.position = target - transform.forward * (perspectiveDistance * zoom) + lift + offset;
            }

            FitBackground();
        }

        private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

        // ---------- Backdrop ----------

        private void FitBackground()
        {
            float depth = Cam.farClipPlane * 0.9f;
            float h = Cam.orthographic
                ? Cam.orthographicSize * 2f
                : 2f * depth * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            background.localPosition = new Vector3(0f, 0f, depth);
            background.localRotation = Quaternion.identity;
            background.localScale = new Vector3(h * Cam.aspect * 1.08f, h * 1.08f, 1f);
        }

        private void RegenerateBackground()
        {
            int texH = Mathf.Clamp(Screen.height, 720, Mobile ? 1280 : 2048);
            int texW = Mathf.Clamp(Mathf.RoundToInt(texH * Cam.aspect), 256, 4096);
            if (backgroundMaterial != null) Destroy(backgroundMaterial.mainTexture);
            backgroundMaterial = MaterialFactory.CreateUnlit(BackgroundArt.Generate(texW, texH));
            background.GetComponent<MeshRenderer>().sharedMaterial = backgroundMaterial;
        }
    }
}
