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
    public class DuelPrototypeV2 : MonoBehaviour
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
        public bool ShowLog = true, ShowAdvantage = true, ShowBoxes;
        private string message = "";

        /// <summary>The prototype's own speed (slow motion, pause, a frame) times any slow motion the fight asks for.</summary>
        public float ToolScale => paused && !stepOnce ? 0f : Speeds[speed];

        private void Awake() => Instance = this;

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            while ((Game = FindAnyObjectByType<GameManager>()) == null) yield return null;
            yield return new WaitForSeconds(4f); // (the game's own start: its world, its screens)
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
            Options.mode = System.Array.IndexOf(args, "-v2Player") >= 0 ? PrototypeOptions.Mode.PlayerVsBot : PrototypeOptions.Mode.BotVsBot;
            int si = System.Array.IndexOf(args, "-v2Stage"); if (si >= 0) Options.stage = int.Parse(args[si + 1]);
            int fi = System.Array.IndexOf(args, "-v2Fam"); if (fi >= 0) { Options.family[0] = args[fi + 1][0] - '0'; Options.family[1] = args[fi + 1][1] - '0'; }
            yield return Begin();
            float t0 = Time.realtimeSinceStartup; int n = 0;
            while (Time.realtimeSinceStartup - t0 < seconds && State == Phase.Fighting)
            {
                yield return new WaitForSecondsRealtime(2f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "fight_" + (n++).ToString("00") + ".png"));
            }
            OnAutoDone();
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

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
            if (Input.GetKeyDown(KeyCode.F1)) panel.Open = !panel.Open;
            if (Input.GetKeyDown(KeyCode.F2)) speed = (speed + 1) % Speeds.Length;
            if (Input.GetKeyDown(KeyCode.F3)) paused = !paused;
            stepOnce = paused && Input.GetKeyDown(KeyCode.F4);
            if (Input.GetKeyDown(KeyCode.F5)) ShowLog = !ShowLog;
            if (Input.GetKeyDown(KeyCode.F6)) ShowAdvantage = !ShowAdvantage;
            if (Input.GetKeyDown(KeyCode.F7)) ShowBoxes = !ShowBoxes;
            if (State == Phase.Fighting || State == Phase.Menu) Game?.V2HideScreens();
            if (State != Phase.Fighting) Time.timeScale = 1f;
        }

        private IEnumerator Begin()
        {
            State = Phase.Loading;
            message = Loc.T("v2.loading");
            bool ok = false;
            yield return Game.V2SetupDuel(Options.stage, Options.family, Options.tier, r => ok = r);
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

        private void BackToMenu()
        {
            if (Camera != null) Camera.gameObject.SetActive(false);
            if (Fight != null) { Destroy(Fight.gameObject); Fight = null; }
            Game.V2Release();
            State = Phase.Menu;
        }

        // ---------- The screens (IMGUI: the prototype's own, never the game's) ----------

        private float Scale => Mathf.Max(0.6f, Screen.height / 1170f);

        private void OnGUI()
        {
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
