using System.Collections;
using SquashBot.Visual;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SquashBot.Gameplay
{
    /// <summary>
    /// A look around the sample fortress battlefield (<see cref="FortressArena"/>), started with "-arena" on the
    /// command line (or F8 on the menu in a desktop build): drag to turn round it, scroll to zoom, Esc to leave.
    /// </summary>
    public partial class GameManager
    {
        private FortressArena fortress;
        private float fortressYaw, fortressPitch = 36f, fortressZoom = 8.4f;
        private Vector2? fortressDrag;

        private Transform fortressFloor;

        /// <summary>
        /// The fortress floor's look: everything built from here on casts real soft shadows from the key light (low and
        /// warm, like the setting sun), and the ambient light comes from the sky, the horizon and the ground instead of
        /// one flat colour. Elsewhere it all goes back to the light, flat, shadowless look (cheaper on phones).
        /// </summary>
        private void ApplyFortressLight(bool on)
        {
            Shapes.CastShadows = on;
            var key = FindAnyObjectByType<Light>();
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { key = l; break; }
            if (key != null)
            {
                key.shadows = on ? LightShadows.Soft : LightShadows.None;
                key.shadowStrength = 0.75f;
                key.color = on ? new Color(1f, 0.9f, 0.8f) : Color.white;
                key.intensity = on ? 1.15f : 1f;
                key.transform.rotation = on ? Quaternion.Euler(42f, 35f, 0f) : Quaternion.Euler(50f, 20f, 0f);
            }
            if (on)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.55f, 0.5f, 0.7f);
                RenderSettings.ambientEquatorColor = new Color(0.62f, 0.45f, 0.42f);
                RenderSettings.ambientGroundColor = new Color(0.18f, 0.16f, 0.2f);
            }
            else RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            if (robot != null)
                foreach (var r in robot.Visual.GetComponentsInChildren<Renderer>(true))
                    r.shadowCastingMode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Level 1's fortress round the floor, and its stairs set on fire.</summary>
        private void SetupFortressFloor()
        {
            ClearFortressFloor();
            if (level == null || !level.fortress) return;
            fortressFloor = FortressFloor.Build(grid, gridView.transform);
            foreach (var p in grid.LavaTiles)
            {
                grid.SetTile(p, Core.TileState.Fire);
                gridView.Ignite(p);
            }
        }

        private void ClearFortressFloor()
        {
            if (fortressFloor != null) Destroy(fortressFloor.gameObject);
            fortressFloor = null;
        }

        private void StartFortressViewIfAsked()
        {
            if (System.Environment.CommandLine.Contains("-arena")) cameraRig.StartCoroutine(OpenFortressSoon());
        }

        private IEnumerator OpenFortressSoon()
        {
            yield return new WaitForSeconds(2.5f); // past the studio logo
            OpenFortress();
        }

        private void OpenFortress()
        {
            if (fortress != null) return;
            fortress = FortressArena.Create(t => robot.BuildLookalike(t), WorldTheme.ForLevel(195));
            fortressYaw = 0f;
            fortressPitch = 36f;
            fortressZoom = 8.4f;
            if (ui != null) ui.gameObject.SetActive(false);
        }

        private void CloseFortress()
        {
            if (fortress == null) return;
            Destroy(fortress.gameObject);
            fortress = null;
            if (ui != null) ui.gameObject.SetActive(true);
        }

        /// <summary>Called every frame: opens on F8 from the menu; while open, turns, zooms and closes it.</summary>
        private void UpdateFortressView()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (fortress == null)
            {
                if (!Application.isMobilePlatform && kb != null && kb.f8Key.wasPressedThisFrame && State == GameState.Menu) OpenFortress();
                return;
            }
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { CloseFortress(); return; }
            if (mouse != null)
            {
                var pos = mouse.position.ReadValue();
                if (mouse.leftButton.isPressed)
                {
                    if (fortressDrag.HasValue)
                    {
                        var d = pos - fortressDrag.Value;
                        fortressYaw += d.x * 0.25f;
                        fortressPitch = Mathf.Clamp(fortressPitch - d.y * 0.2f, 12f, 80f);
                    }
                    fortressDrag = pos;
                }
                else fortressDrag = null;
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) fortressZoom = Mathf.Clamp(fortressZoom * (scroll > 0f ? 0.9f : 1.1f), 1.5f, 14f);
            }
            var cam = fortress.Cam;
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, fortressZoom, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            cam.transform.rotation = Quaternion.Euler(fortressPitch, fortressYaw, 0f);
            // Zoomed in, the view centres on foi; zoomed out, on the whole fortress.
            float focusY = Mathf.Lerp(1.4f, 0.6f, Mathf.InverseLerp(2f, 8.4f, cam.orthographicSize));
            cam.transform.position = fortress.transform.position + new Vector3(0f, focusY, 0f) - cam.transform.forward * 40f;
#endif
        }
    }
}
