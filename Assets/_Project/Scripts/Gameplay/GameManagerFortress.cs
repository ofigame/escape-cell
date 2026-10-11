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
