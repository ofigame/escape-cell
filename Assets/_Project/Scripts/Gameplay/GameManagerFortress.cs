using System.Collections;
using SquashBot.Core;
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
            // Polished marble floors everywhere (the arenas' design), in each world's colour — or the arena's own.
            GridView.PlainObstacles = arenaRun;
            GridView.FortressStone = arenaRun ? BattleArenas.Style(arenaIndex).floor : FortressStoneFor(world, theme.accent);
            GridView.Marble = true;
            GridView.FortressMetal = false;
            // Real shadows cost: soft on strong phones, hard on middling ones, none on the weakest (the rest of the look stays).
            var tier = Data.GraphicsQuality.Current;
            bool shadows = on && tier != Data.GraphicsTier.Low && !lowMemoryMode;
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
        private FloorStand floorStand;
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
            if (arenaRun) stage = Mathf.Max(1, stage); // the arenas are always grand fortresses
            // The ramparts rise and thicken further into the campaign, and stage by stage inside a level (each
            // stage's walls higher and thicker than the one before), in a stone tinted by the world.
            float camp = Mathf.InverseLerp(0f, 24f, World);
            float wallHeight = Mathf.Lerp(1.15f, 3.0f, camp);
            float wallThick = Mathf.Lerp(1.0f, 1.9f, camp);
            FortressFloor.WallScaleAt = level.fortressStages > 1 ? p => 1f + 0.22f * StageAt(p) : (System.Func<GridPos, float>)null;
            // The ramparts: the world's colour in a mid-dark tone (dark, never very dark), the floor its lightest tone.
            Color.RGBToHSV(WorldTheme.Current.accent, out float wh, out _, out _);
            FortressFloor.WallTint = Color.HSVToRGB(wh, 0.22f, Mathf.Lerp(0.6f, 0.48f, camp));
            FortressFloor.Fx = FortressFloor.WallFx.None;
            FortressFloor.TowerGrow = Mathf.Lerp(1f, 1.9f, Mathf.InverseLerp(0f, 24f, World)); // the corner towers grow world by world
            if (arenaRun)
            {
                // The arenas: high ramparts and towering corner towers round a huge courtyard.
                wallHeight = 2.6f + arenaIndex * 0.04f; // ramparts only in the gaps between the buildings
                wallThick = 1.4f;
                FortressFloor.TowerGrow = Mathf.Max(FortressFloor.TowerGrow, 1.8f + arenaIndex * 0.04f);
                var st = BattleArenas.Style(arenaIndex);
                FortressFloor.WallTint = st.wall;
                FortressFloor.Fx = (FortressFloor.WallFx)st.fx;
                FortressFloor.FxColour = st.accent;
                FortressFloor.WallScaleAt = null;
            }
            bool duelWalls = challengeRun && !Stage.colosseum;
            if (duelWalls)
            {
                // A duel stage (not the colosseum): low walls in its own stone, crowned with its flames, ice or light.
                var ds = Stage;
                wallHeight = 0.9f;
                wallThick = 1f;
                FortressFloor.WallTint = ds.wall;
                FortressFloor.Fx = (FortressFloor.WallFx)ds.fx;
                FortressFloor.FxColour = ds.fxColour;
                FortressFloor.TowerGrow = 1.6f;
                FortressFloor.WallScaleAt = null;
            }
            FortressFloor.SkipCornerTowers = arenaRun || (challengeRun && !duelWalls);
            FortressFloor.SkipWall = trailRun || (challengeRun && !duelWalls) ? (System.Func<GridPos, bool>)(p => true) // the roads: mountains, no ramparts (the colosseum: its stands)
                : arenaRun ? (System.Func<GridPos, bool>)(p => BattleArenas.BuildingAt(p.x, p.y)) : null;
            FortressFloor.Detailed = arenaRun;
            FortressFloor.WallThick = wallThick;
            fortressFloor = FortressFloor.Build(grid, gridView.transform, stage, WorldTheme.Current.accent, robot.Position, gridView.Surface, FortressRects(), wallHeight);
            if (arenaRun) ArenaDecor.Build(fortressFloor, grid, level.stageBounds, arenaIndex, robot.Position, BattleArenas.Style(arenaIndex).accent, BattleArenas.Style(arenaIndex).trim);
            if (arenaRun) { var look = BattleArenas.Style(arenaIndex); ArenaDecor.City(fortressFloor, BattleArenas.LastBuildings, look.wall, look.accent, look.trim, arenaIndex); }
            if (arenaRun) BuildGunPosts(fortressFloor);
            // The floors: a stand of spectators along the far right wall (longer with the floor, more tiers with more stages).
            floorStand = null;
            if (!arenaRun && !trailRun && !challengeRun)
            {
                float x0 = GridView.ToWorld(new GridPos(grid.Width - 1, 0)).x + 0.5f + wallThick + 0.35f;
                float z0 = GridView.ToWorld(new GridPos(0, 0)).z - 1f, z1 = GridView.ToWorld(new GridPos(0, grid.Height - 1)).z + 1f;
                floorStand = FloorStand.Build(fortressFloor, x0, z0, z1, GridView.SurfaceY + wallHeight + 0.1f, 3 + Mathf.Max(1, level.fortressStages),
                    Color.Lerp(FortressFloor.WallTint ?? new Color(0.7f, 0.66f, 0.6f), Color.white, 0.35f), levelIndex * 31 + 7);
            }
            // The fortress never moves: its thousands of pieces are merged into a few big meshes (far fewer draw calls).
            if (fortressFloor.GetComponentsInChildren<MeshRenderer>().Length < 6000) StaticBatchingUtility.Combine(fortressFloor.gameObject); // never on a huge one (memory)
            // Craft crossing the sky over the later fortresses.
            if (stage >= 1 && !challengeRun) // (not over the challenge board)
                SkyTraffic.Create(fortressFloor, () => robot != null ? robot.transform.position : Vector3.zero,
                    Color.Lerp(new Color(1f, 0.55f, 0.22f), WorldTheme.Current.accent, 0.25f), stage == 2 ? 5 : 3, levelIndex * 17 + 3);
            if (!challengeRun) glassCell = GlassCell.Create(GridView.ToWorld(robot.Position) + Vector3.up * GridView.SurfaceY, Mathf.Max(robot.transform.localScale.x, robot.transform.localScale.y) * 0.62f);
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
            // Always the lightest tone of the world's colour: the floor is never dark (the walls carry the depth).
            float t = Mathf.Clamp01(world / 24f);
            Color.RGBToHSV(accent, out float h, out _, out _);
            var tint = Color.HSVToRGB(h, Mathf.Lerp(0.06f, 0.14f, t), 0.9f);
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
