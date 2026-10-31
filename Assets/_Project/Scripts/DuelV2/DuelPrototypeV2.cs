using System.Collections;
using SquashBot.Data;
using SquashBot.Gameplay;
using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The duel fight core v2's prototype scene (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 9): its menu (mode, weapons and tiers,
    /// the bot, the stage and its width, the rounds), the live tuning panel (F1), slow motion (F2), pause and frame by
    /// frame (F3 / F4) and the helpers (F5 hit log, F6 advantage bar, F7 boxes). The game sets the duel up; this takes it.
    /// </summary>
    public class DuelPrototypeV2 : MonoBehaviour, IDuelHostV2
    {
        public enum Phase { Booting, Menu, Loading, Fighting }

        public static DuelPrototypeV2 Instance { get; private set; }
        public Phase State { get; private set; } = Phase.Booting;
        public PrototypeOptions Options { get; } = new PrototypeOptions();
        public GameManager Game { get; private set; }

        private readonly SettingsPanelV2 panel = new SettingsPanelV2();
        private static readonly float[] Speeds = { 1f, 0.5f, 0.25f };
        private int speed;
        private bool paused, stepOnce;
        public bool ShowLog { get; set; }
        public bool ShowAdvantage { get; set; } = true;
        public bool ShowBoxes { get; set; } // (properties: never kept in the scene, the code's defaults)
        public DuelCoreSettings Settings => DuelCoreSettings.Current; // (the live-tuned numbers)
        public bool IsPrototype => true;
        public void MatchDone(DuelSim sim) { } // (its own match panel: again or the menu)
        public bool NextState(out DuelSnapshot state) { state = null; return false; } // (never live)
        public void SendInput(FighterInput input) { }
        public void PauseAsked() { }
        public void Event(DuelEvent e) { }
        private string message = "";

        /// <summary>The prototype's own speed (slow motion, pause, a frame) times any slow motion the fight asks for.</summary>
        public float ToolScale => paused && !stepOnce ? 0f : Speeds[speed];

        private void Awake() => Instance = this;

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            while ((Game = FindAnyObjectByType<GameManager>()) == null) yield return null;
            yield return new WaitForSeconds(4f); // (the game's own start: its world, its screens)
            var startArgs = System.Environment.GetCommandLineArgs();
            int gi = System.Array.IndexOf(startArgs, "-v2Game");
            int li = System.Array.IndexOf(startArgs, "-v2Live"); // (a live duel against a local server: -v2Live http://localhost:5199)
            if (gi >= 0) { Game.V2Boot(prototype: false, server: li >= 0 ? startArgs[li + 1] : "http://127.0.0.1:9"); StartCoroutine(GameAuto(startArgs[gi + 1], float.Parse(startArgs[gi + 2], System.Globalization.CultureInfo.InvariantCulture))); yield break; }
            Game.V2Boot();
            yield return new WaitForSeconds(0.5f);
            Game.V2HideScreens();
            State = Phase.Menu;
            var args = System.Environment.GetCommandLineArgs();
            int ai = System.Array.IndexOf(args, "-v2Auto");
            if (ai >= 0) StartCoroutine(Auto(args[ai + 1], float.Parse(args[ai + 2], System.Globalization.CultureInfo.InvariantCulture)));
        }

        /// <summary>The prototype tests itself (-v2Auto dir seconds): the menu, then a bot-vs-bot fight, pictures now and then, then quits.</summary>
        private IEnumerator Auto(string dir, float seconds)
        {
            System.IO.Directory.CreateDirectory(dir);
            AutoDir = dir;
            yield return new WaitForSeconds(1f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "menu.png"));
            yield return new WaitForSeconds(0.5f);
            var args = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(args, "-v2Sounds") >= 0)
            {
                // the fight sounds as .wav files (to listen to), timed
                float s0 = Time.realtimeSinceStartup;
                DuelSoundsV2.WriteAll(System.IO.Path.Combine(dir, "sesler"));
                DuelFightV2.AutoLog.AppendLine("sounds written in " + Mathf.RoundToInt((Time.realtimeSinceStartup - s0) * 1000f) + " ms");
                OnAutoDone(); Application.Quit(); yield break;
            }
            Options.mode = System.Array.IndexOf(args, "-v2Player") >= 0 ? PrototypeOptions.Mode.PlayerVsBot : PrototypeOptions.Mode.BotVsBot;
            int si = System.Array.IndexOf(args, "-v2Stage"); if (si >= 0) Options.stage = int.Parse(args[si + 1]);
            int ri = System.Array.IndexOf(args, "-v2RoundSec"); if (ri >= 0) Options.roundSeconds = float.Parse(args[ri + 1], System.Globalization.CultureInfo.InvariantCulture);
            int fi = System.Array.IndexOf(args, "-v2Fam"); if (fi >= 0) { Options.family[0] = args[fi + 1][0] - '0'; Options.family[1] = args[fi + 1][1] - '0'; }
            int wi2 = System.Array.IndexOf(args, "-v2Width"); if (wi2 >= 0) Options.width = float.Parse(args[wi2 + 1], System.Globalization.CultureInfo.InvariantCulture);
            yield return Begin();
            if (System.Array.IndexOf(args, "-v2Weapons") >= 0 && Fight != null) { yield return Fight.WeaponSheet(dir); OnAutoDone(); Application.Quit(); yield break; }
            if (System.Array.IndexOf(args, "-v2Pose") >= 0 && Fight != null) { yield return Fight.PoseSheet(dir); OnAutoDone(); Application.Quit(); yield break; }
            if (System.Array.IndexOf(args, "-v2Panel") >= 0)
            {
                // the tuning panel open, saved and read back (its file beside the game)
                yield return new WaitForSecondsRealtime(3f);
                panel.Open = true;
                yield return new WaitForSecondsRealtime(0.5f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "panel.png"));
                float before = DuelCoreSettings.Current.tempo;
                DuelCoreSettings.Current.tempo = 1.1f;
                DuelCoreSettings.Save();
                DuelCoreSettings.Current.tempo = 0.9f;
                bool read = DuelCoreSettings.Load();
                DuelFightV2.AutoLog.AppendLine("panel save/load: file " + DuelCoreSettings.FilePath + " read " + read + " tempo " + DuelCoreSettings.Current.tempo.ToString("0.00"));
                DuelCoreSettings.Current.tempo = before;
                System.IO.File.Delete(DuelCoreSettings.FilePath);
                yield return new WaitForSecondsRealtime(0.3f);
                panel.Open = false;
            }
            if (System.Array.IndexOf(args, "-v2Keys") >= 0) StartCoroutine(PressKeys());
            float t0 = Time.realtimeSinceStartup; int n = 0;
            while (Time.realtimeSinceStartup - t0 < seconds && State == Phase.Fighting)
            {
                if (n == 2 && Fight != null) DuelFightV2.AutoLog.AppendLine("facing: " + Fight.Facing());
                yield return new WaitForSecondsRealtime(2f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "fight_" + (n++).ToString("00") + ".png"));
            }
            OnAutoDone();
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        /// <summary>The self-test's keys: Space and D pressed and let go through the Input System (the player's keyboard path).</summary>
        private IEnumerator PressKeys()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current ?? UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            DuelFightV2.AutoLog.AppendLine("keys: keyboard " + kb.displayName);
            while (State == Phase.Fighting)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Space, UnityEngine.InputSystem.Key.D));
                yield return new WaitForSecondsRealtime(0.1f);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }

        /// <summary>
        /// The game's own v2 duel tested (-v2Game dir seconds [-v2Stage n] [-v2Fam f]): a challenge duel against the
        /// computer as the menu starts it, a bot fighting for foi; pictures every 2 s, the game's state, the events and the
        /// result screen; then it quits.
        /// </summary>
        private IEnumerator GameAuto(string dir, float seconds)
        {
            System.IO.Directory.CreateDirectory(dir);
            AutoDir = dir;
            gameTest = true; // (no prototype words or buttons over the game)
            var args = System.Environment.GetCommandLineArgs();
            int si = System.Array.IndexOf(args, "-v2Stage"), fi = System.Array.IndexOf(args, "-v2Fam");
            int stage = si >= 0 ? int.Parse(args[si + 1]) : 3, fam = fi >= 0 ? int.Parse(args[fi + 1]) : 0;
            bool over = false;
            StartCoroutine(Game.V2GameDuel(stage, fam, () => over));
            float t0 = Time.realtimeSinceStartup; int n = 0; bool seenFight = false;
            while (Time.realtimeSinceStartup - t0 < seconds)
            {
                yield return new WaitForSecondsRealtime(2f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "game_" + (n++).ToString("00") + ".png"));
                DuelFightV2.AutoLog.AppendLine("state " + (Time.realtimeSinceStartup - t0).ToString("0") + "s " + Game.V2GameState);
                if (DuelCoreV2.InGame) seenFight = true;
                if (n == 6 || n == 14)
                    foreach (var g in FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None))
                    {
                        if (!g.isActiveAndEnabled || g.color.a < 0.05f) continue;
                        var r = g.rectTransform.rect; var c = g.canvas;
                        float share = c != null ? r.width * r.height * c.scaleFactor * c.scaleFactor / (Screen.width * (float)Screen.height) : 0f;
                        if (share > 0.4f) DuelFightV2.AutoLog.AppendLine("big ui: " + g.name + " <" + (g.transform.parent != null ? g.transform.parent.name : "") + "> canvas " + (c != null ? c.name : "-") + " a " + g.color.a.ToString("0.00") + " group " + Alpha(g.transform).ToString("0.00") + " t " + n + " share " + share.ToString("0.00"));
                    }
                if (seenFight && Game.V2GameOver) { yield return new WaitForSecondsRealtime(9f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "result.png")); yield return null; break; }
            }
            over = true;
            DuelFightV2.AutoLog.AppendLine("end " + Game.V2GameState);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "events.txt"), DuelFightV2.AutoLog.ToString());
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        private static float Alpha(Transform t) { float a = 1f; for (; t != null; t = t.parent) { var g = t.GetComponent<CanvasGroup>(); if (g != null) a *= g.alpha; } return a; }

        /// <summary>Where the self-test writes (null: not testing).</summary>
        public static string AutoDir;

        /// <summary>The self-test is over (the fight's parts write their numbers).</summary>
        private void OnAutoDone()
        {
            if (AutoDir == null) return;
            System.IO.File.WriteAllText(System.IO.Path.Combine(AutoDir, "events.txt"), DuelFightV2.AutoLog.ToString());
            if (Fight != null) System.IO.File.WriteAllText(System.IO.Path.Combine(AutoDir, "stats.txt"), Fight.Stats());
        }

        private void Update()
        {
            if (KeysV2.Down(UnityEngine.InputSystem.Key.F1)) panel.Open = !panel.Open;
            if (KeysV2.Down(UnityEngine.InputSystem.Key.F2)) speed = (speed + 1) % Speeds.Length;
            if (KeysV2.Down(UnityEngine.InputSystem.Key.F3)) paused = !paused;
            stepOnce = paused && KeysV2.Down(UnityEngine.InputSystem.Key.F4);
            if (KeysV2.Down(UnityEngine.InputSystem.Key.F5)) ShowLog = !ShowLog;
            if (KeysV2.Down(UnityEngine.InputSystem.Key.F6)) ShowAdvantage = !ShowAdvantage;
            if (KeysV2.Down(UnityEngine.InputSystem.Key.F7)) ShowBoxes = !ShowBoxes;
            if (State == Phase.Fighting || State == Phase.Menu) Game?.V2HideScreens();
            if (State != Phase.Fighting) Time.timeScale = 1f;
        }

        private IEnumerator Begin()
        {
            State = Phase.Loading;
            message = Loc.T("v2.loading");
            bool ok = false;
            yield return Game.V2SetupDuel(Options.stage, Options.family, Options.tier, r => ok = r, Options.width);
            if (!ok) { message = Loc.T("v2.loadFailed"); State = Phase.Menu; Game.V2Release(); yield break; }
            message = "";
            State = Phase.Fighting;
            OnFightStart();
        }

        /// <summary>The prototype's camera (its own: the game's waits with its duel).</summary>
        public DuelCameraV2 Camera { get; private set; }

        /// <summary>The fight begins: the prototype's camera on the two (the core takes them from here).</summary>
        private void OnFightStart()
        {
            if (Camera == null) Camera = DuelCameraV2.Create(Game.V2CameraSide, Game.V2Line, Game.V2FighterScale);
            Camera.gameObject.SetActive(true);
            if (Fight != null) Destroy(Fight.gameObject);
            Fight = DuelFightV2.Begin(this, Camera);
        }

        /// <summary>The fight being fought (null on the menu).</summary>
        public DuelFightV2 Fight { get; private set; }

        public void BackToMenu()
        {
            if (Camera != null) Camera.gameObject.SetActive(false);
            if (Fight != null) { Destroy(Fight.gameObject); Fight = null; }
            Game.V2Release();
            State = Phase.Menu;
        }

        // ---------- The screens (IMGUI: the prototype's own, never the game's) ----------

        private float Scale => Mathf.Max(0.6f, Screen.height / 1170f);

        /// <summary>The game's own duel under test (-v2Game): the prototype draws nothing.</summary>
        private bool gameTest;

        private void OnGUI()
        {
            if (gameTest) return;
            GUI.skin.label.fontSize = GUI.skin.button.fontSize = GUI.skin.toggle.fontSize = GUI.skin.textField.fontSize = Mathf.RoundToInt(18 * Scale);
            if (State == Phase.Booting || State == Phase.Loading)
            {
                GUI.Label(new Rect(20f, Screen.height * 0.45f, Screen.width - 40f, 60f), State == Phase.Booting ? Loc.T("v2.booting") : message, Center(28));
                return;
            }
            if (State == Phase.Menu) DrawMenu();
            if (State == Phase.Fighting)
            {
                if (GUI.Button(new Rect(Screen.width - 110f * Scale, 8f, 100f * Scale, 40f * Scale), Loc.T("v2.menu"))) BackToMenu();
                if (GUI.Button(new Rect(Screen.width - 110f * Scale, 52f * Scale, 100f * Scale, 40f * Scale), "F1")) panel.Open = !panel.Open;
                string tools = (paused ? Loc.T("v2.paused") + "  " : "") + (Speeds[speed] < 1f ? "×" + Speeds[speed] : "");
                if (tools.Length > 0) GUI.Label(new Rect(10f, Screen.height - 40f * Scale, Screen.width, 36f * Scale), tools, Center(20));
            }
            panel.Draw(Scale);
        }

        private Vector2 menuScroll;

        private void DrawMenu()
        {
            float s = Scale, w = Screen.width - 30f;
            GUILayout.BeginArea(new Rect(15f, 15f, w, Screen.height - 30f), GUI.skin.box);
            menuScroll = GUILayout.BeginScrollView(menuScroll);
            GUILayout.Label(Loc.T("v2.title"), Center(30));
            if (message.Length > 0) GUILayout.Label(message, Center(20));

            GUILayout.Label("<b>" + Loc.T("v2.mode") + "</b>", Rich());
            Options.mode = (PrototypeOptions.Mode)GUILayout.SelectionGrid((int)Options.mode, new[] { Loc.T("v2.mode.pvb"), Loc.T("v2.mode.bvb"), Loc.T("v2.mode.pvd") }, 3, GUILayout.Height(44f * s));

            string[] families = { Loc.T("v2.fam.sword"), Loc.T("v2.fam.spear"), Loc.T("v2.fam.axe"), Loc.T("v2.fam.heavy") };
            string[] personas = { Loc.T("v2.bot.balanced"), Loc.T("v2.bot.aggressive"), Loc.T("v2.bot.defensive"), Loc.T("v2.bot.spam") };
            for (int side = 0; side < 2; side++)
            {
                GUILayout.Space(8f * s);
                GUILayout.Label("<b>" + Loc.T(side == 0 ? "v2.side.me" : "v2.side.rival") + "</b>", Rich());
                Options.family[side] = GUILayout.SelectionGrid(Options.family[side], families, 4, GUILayout.Height(40f * s));
                GUILayout.BeginHorizontal();
                GUILayout.Label(Loc.T("v2.tier"), GUILayout.Width(120f * s));
                Options.tier[side] = 1 + GUILayout.SelectionGrid(Options.tier[side] - 1, new[] { "1", "2", "3", "4", "5" }, 5, GUILayout.Height(36f * s));
                GUILayout.EndHorizontal();
                bool isBot = side == 1 ? Options.mode != PrototypeOptions.Mode.PlayerVsDummy : Options.mode == PrototypeOptions.Mode.BotVsBot;
                if (!isBot) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(Loc.T("v2.bot.skill") + " " + Mathf.RoundToInt(Options.skill[side] * 100f) + "%", GUILayout.Width(180f * s));
                Options.skill[side] = GUILayout.HorizontalSlider(Options.skill[side], 0f, 1f);
                GUILayout.EndHorizontal();
                Options.persona[side] = (BotPersonality)GUILayout.SelectionGrid((int)Options.persona[side], personas, 4, GUILayout.Height(36f * s));
            }

            GUILayout.Space(8f * s);
            GUILayout.Label("<b>" + Loc.T("v2.stage") + "</b>", Rich());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(50f * s), GUILayout.Height(40f * s))) Options.stage = (Options.stage + GameManager.V2StageCount - 1) % GameManager.V2StageCount;
            GUILayout.Label(GameManager.V2StageName(Options.stage), Center(22), GUILayout.Height(40f * s));
            if (GUILayout.Button(">", GUILayout.Width(50f * s), GUILayout.Height(40f * s))) Options.stage = (Options.stage + 1) % GameManager.V2StageCount;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("v2.width"), GUILayout.Width(160f * s));
            float[] widths = { 1f, 1.25f, 1.5f, 2f };
            int wi = System.Array.IndexOf(widths, Options.width); if (wi < 0) wi = 2;
            Options.width = widths[GUILayout.SelectionGrid(wi, new[] { "×1", "×1,25", "×1,5", "×2" }, 4, GUILayout.Height(36f * s))];
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.F("v2.roundTime", Mathf.RoundToInt(Options.roundSeconds)), GUILayout.Width(200f * s));
            Options.roundSeconds = Mathf.Round(GUILayout.HorizontalSlider(Options.roundSeconds, 20f, 120f) / 5f) * 5f;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("v2.rounds"), GUILayout.Width(200f * s));
            Options.roundsToWin = 1 + GUILayout.SelectionGrid(Options.roundsToWin - 1, new[] { "1", "2", "3" }, 3, GUILayout.Height(36f * s));
            GUILayout.EndHorizontal();

            GUILayout.Space(14f * s);
            if (GUILayout.Button(Loc.T("v2.start"), GUILayout.Height(70f * s))) StartCoroutine(Begin());
            GUILayout.Space(8f * s);
            GUILayout.Label(Loc.T("v2.keysHelp"), Rich());
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static GUIStyle rich;
        public static GUIStyle Rich() => rich ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
        public static GUIStyle Center(int size)
        {
            var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true, fontStyle = FontStyle.Bold };
            st.fontSize = Mathf.RoundToInt(size * Mathf.Max(0.6f, Screen.height / 1170f));
            return st;
        }
    }
}
