using System.Collections;
using SquashBot.Data;
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
        private void ApplyFortressLight(bool on, int world = 0, WorldTheme theme = null)
        {
            GridView.FortressTiles = on;
            theme = theme ?? WorldTheme.Current;
            GridView.FortressStone = FortressStoneFor(world, theme.accent);
            GridView.FortressMetal = FortressFloor.StageFor(world) == 2;
            // Real shadows cost: soft on strong phones, hard on middling ones, none on the weakest (the rest of the look stays).
            var tier = Data.GraphicsQuality.Current;
            bool shadows = on && tier != Data.GraphicsTier.Low;
            Shapes.CastShadows = shadows;
            var key = FindAnyObjectByType<Light>();
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { key = l; break; }
            if (key != null)
            {
                key.shadows = !shadows ? LightShadows.None : tier == Data.GraphicsTier.High ? LightShadows.Soft : LightShadows.Hard;
                key.shadowStrength = 0.75f;
                key.color = on ? new Color(1f, 0.9f, 0.8f) : Color.white;
                key.intensity = on ? Mathf.Lerp(1.15f, 1.5f, 1f - Mathf.Clamp01(GridView.FortressStone.grayscale / 0.6f)) : 1f; // dark decks need a stronger sun for their sheen
                key.transform.rotation = on ? Quaternion.Euler(42f, 35f, 0f) : Quaternion.Euler(50f, 20f, 0f);
            }
            if (on)
            {
                // Light from the sky above, the horizon round about and the dark ground below, in the floor's own colours.
                var t = theme;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = Color.Lerp(t.ambient, t.bgTop, 0.35f) * 0.8f;
                RenderSettings.ambientEquatorColor = Color.Lerp(t.ambient, t.bgGlow, 0.4f) * 0.75f;
                RenderSettings.ambientGroundColor = Color.Lerp(t.bgBottom, Color.black, 0.6f);
            }
            else RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            if (robot != null)
                foreach (var r in robot.Visual.GetComponentsInChildren<Renderer>(true))
                    r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private GlassCell glassCell;
        private float glassSince = -1f;

        /// <summary>
        /// The fortress round the floor (its stage grows with the worlds; level 1 is the stone one), its stairs set on
        /// fire, and foi's glass cell on the start tile.
        /// </summary>
        private void SetupFortressFloor()
        {
            ClearFortressFloor();
            if (level == null || level.mission != MissionType.Hunt) return;
            int stage = level.fortress ? 0 : FortressFloor.StageFor(World);
            // The ramparts rise further into the campaign (the first worlds keep theirs).
            float wallHeight = stage == 0 ? 1f : Mathf.Lerp(1f, 1.7f, Mathf.InverseLerp(5f, 24f, World));
            fortressFloor = FortressFloor.Build(grid, gridView.transform, stage, WorldTheme.Current.accent, robot.Position, gridView.Surface, FortressRects(), wallHeight);
            // Craft crossing the sky over the later fortresses.
            if (stage >= 1)
                SkyTraffic.Create(fortressFloor, () => robot != null ? robot.transform.position : Vector3.zero,
                    Color.Lerp(new Color(1f, 0.55f, 0.22f), WorldTheme.Current.accent, 0.25f), stage == 2 ? 5 : 3, levelIndex * 17 + 3);
            glassCell = GlassCell.Create(GridView.ToWorld(robot.Position) + Vector3.up * GridView.SurfaceY, robot.transform.localScale.x * 0.62f);
            glassSince = -1f;
            foreach (var p in grid.LavaTiles)
            {
                grid.SetTile(p, Core.TileState.Fire);
                gridView.Ignite(p);
            }
        }

        /// <summary>
        /// The colour ramp of the fortress floors: pale, almost white stone in the first worlds; then every world tinted in
        /// its own colour, going from light to deep and dark — the far worlds feel like a war among the stars.
        /// </summary>
        private static Color FortressStoneFor(int world, Color accent)
        {
            // The floor itself never goes very dark (the walls round it are the dark part, see FortressFloor): a soft
            // grey with the world's hue, deeper in the far worlds, like brushed metal under a dusk sky.
            float t = Mathf.Clamp01(world / 24f);
            Color.RGBToHSV(accent, out float h, out _, out _);
            var tint = Color.HSVToRGB(h, Mathf.Lerp(0.06f, 0.16f, t), Mathf.Lerp(0.9f, 0.46f, Mathf.Pow(t, 0.8f)));
            return world < 2 ? Color.Lerp(new Color(0.86f, 0.86f, 0.88f), tint, 0.3f) : tint;
        }

        private void ClearFortressFloor()
        {
            if (fortressFloor != null) Destroy(fortressFloor.gameObject);
            fortressFloor = null;
            if (glassCell != null) Destroy(glassCell.gameObject);
            glassCell = null;
            ClearStageGates();
            if (defenses != null) defenses.Clear();
        }

        /// <summary>
        /// While foi is still in its cell: the first touch (or a moment's wait) breaks the glass and that touch does
        /// nothing else. Returns the command to carry on with.
        /// </summary>
        private InputCommand GlassCellGate(InputCommand command)
        {
            if (glassCell == null || glassCell.Broken || level == null || level.mission != MissionType.Hunt) return command;
            if (glassSince < 0f)
            {
                // Nothing moves while foi is still behind the glass: robots, crates and floor rules wait for it.
                glassSince = Time.time;
                hunt.Freeze();
                hazards.Freeze();
                floorRules.Freeze();
            }
            if (defenseEditing || IntroRunning || introPending) { glassSince = Time.time; return default; } // set-up and the opening shot first
            if (command.IsEmpty && Time.time - glassSince < 2f) return default;
            // foi breaks out: a punch, a white flash in the glass, the shards fly; the floor comes alive.
            glassCell.Shatter();
            if (levelIndex >= 2 && PlayerPrefs.GetInt("sb_spin_tip", 0) == 0)
            {
                // Once, from level 3: how to spin.
                PlayerPrefs.SetInt("sb_spin_tip", 1);
                ui.ShowIntro(Loc.T("spin.tipTitle"), Loc.T("spin.tip"));
            }
            hunt.Resume();
            hazards.Resume();
            floorRules.Resume();
            if (heldWeapon != null) heldWeapon.Swing();
            robot.Strike(robot.transform.position + robot.transform.forward);
            cameraRig.Shake(0.6f);
            cameraRig.Punch(0.5f);
            fx.Burst(robot.transform.position + Vector3.up * 0.8f, new Color(0.55f, 0.9f, 1f), new Color(0.8f, 2f, 3f), 40, 6f);
            Audio.AudioManager.PlaySfx(Audio.Sfx.Blocked, 1f, 1.7f);
            Audio.AudioManager.PlaySfx(Audio.Sfx.Impact, 0.7f, 1.4f);
            Audio.Haptics.Medium();
            return default;
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
