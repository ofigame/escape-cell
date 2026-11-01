using System.Globalization;
using SquashBot.Data;
using SquashBot.Gameplay;
using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The v2 fight on screen: each frame the player's keys and buttons (or a bot) go into the rules, the rules tick, and
    /// what happened goes out to the two foi, the screen, the camera and the moments (hit stops, slow motion).
    /// </summary>
    public class DuelFightV2 : MonoBehaviour
    {
        public DuelSim Sim { get; private set; }
        private IDuelHostV2 proto;
        private GameManager game;
        private readonly FighterViewV2[] views = new FighterViewV2[2];
        private readonly DuelBotV2[] bots = new DuelBotV2[2];
        private readonly DuelHudV2 hud = new DuelHudV2();
        private RainViewV2 rains;
        private ReplayRecorderV2 replay;
        private float flashUntil, heartAt;
        private bool replayDone;
        private DuelCameraV2 cam;
        private Vector3 centre, line;
        private float sHeld = -1f;
        private bool playerLeft, live;
        private FighterInput sentHeld = FighterInput.None;

        /// <summary>A live duel's wishes out: every press at once, the held ones (shield, walk) when they change.</summary>
        private void SendLive(FighterInput i)
        {
            bool press = i.attack || i.jump || i.dodge || i.special || i.ulti || i.context || i.heart || i.power >= 0;
            if (!press && i.block == sentHeld.block && Mathf.Approximately(i.walk, sentHeld.walk)) return;
            sentHeld = i;
            proto.SendInput(i);
        }
        private DuelCoreSettings S => proto.Settings;

        public static DuelFightV2 Begin(IDuelHostV2 proto, DuelCameraV2 cam)
        {
            var go = new GameObject("DuelFightV2");
            var f = go.AddComponent<DuelFightV2>();
            f.proto = proto; f.game = proto.Game; f.cam = cam;
            f.Setup();
            return f;
        }

        private void Setup()
        {
            var o = proto.Options;
            centre = game.V2Centre; line = game.V2Line;
            S.platformWidth = o.width;
            S.roundSeconds = o.roundSeconds; S.roundsToWin = o.roundsToWin;
            float half = 5.5f * o.width;
            float scale = game.V2FighterScale;
            views[0] = new FighterViewV2(game, game.V2MeRoot, game.V2MeLook, game.V2MeWeapon, centre, line, scale);
            var foeLook = game.V2FoeRoot != null ? game.V2FoeRoot.GetComponentInChildren<Visual.SkinnedHero>()?.transform.parent : null;
            views[1] = new FighterViewV2(game, game.V2FoeRoot, foeLook != null ? foeLook : game.V2FoeRoot, game.V2FoeWeapon, centre, line, scale);
            float tall = FighterHeight(views[0]);
            Sim = new DuelSim(S, o.family, o.tier, half, tall);
            for (int i = 0; i < 2; i++) views[i].Family = o.family[i];
            playerLeft = o.mode != PrototypeOptions.Mode.BotVsBot;
            live = o.mode == PrototypeOptions.Mode.Live;
            if (live) for (int i = 0; i < 2; i++) views[i].Smooth = true; // (the server's places 20 times a second: shown gliding)
            bots[0] = playerLeft ? null : new DuelBotV2(o.skill[0], o.persona[0], 11);
            if (live && GameDuelHostV2.BotPlaysFoi) bots[0] = new DuelBotV2(0.6f, BotPersonality.Balanced, 11);
            bots[1] = o.mode == PrototypeOptions.Mode.PlayerVsDummy || o.mode == PrototypeOptions.Mode.Live ? null : new DuelBotV2(o.skill[1], o.persona[1], 23);
            MomentsV2.Clear();
            DuelSoundsV2.Warm();
            if (!proto.IsPrototype)
            {
                ui = DuelHudV2Ui.Create(o.names[0], o.names[1], canPause: !live);
                ui.Paused = () => proto.PauseAsked();
                hud.Ui = ui;
            }
            rains = new RainViewV2(game, WorldOf, scale);
            float height = FighterHeight(views[0]);
            if (cam != null) cam.FighterHeight = height;
            if (S.replay) replay = ReplayRecorderV2.Create(game.V2CameraSide, line, height, S.replaySeconds);
            OnStarted();
        }

        /// <summary>The fight's first moment (the rounds' intro from Faz 4 on).</summary>
        private void OnStarted() { }

        /// <summary>Where each fighter's parts look (the self-test): root, look, body, against the line and the camera.</summary>
        public string Facing()
        {
            var sb = new System.Text.StringBuilder("line " + line + " cam " + (cam != null && cam.Cam != null ? cam.Cam.transform.forward.ToString() : "-"));
            for (int i = 0; i < 2; i++)
            {
                var v = views[i];
                sb.Append(" | " + i + " facing " + Sim.F[i].facing + " root " + v.Root.name + " " + v.Root.forward + " look " + (v.Look != null ? v.Look.name + " " + v.Look.forward : "-") + " skin " + (v.Skin != null ? v.Skin.name + " " + v.Skin.transform.forward : "-"));
            }
            return sb.ToString();
        }

        /// <summary>A fighter's height (its body's top over its feet).</summary>
        private static float FighterHeight(FighterViewV2 v)
        {
            if (v.Look == null) return 6f;
            float top = v.Root.position.y;
            foreach (var r in v.Look.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r.GetComponentInParent<Visual.HeldWeapon>() != null) continue;
                top = Mathf.Max(top, r.bounds.max.y);
            }
            return Mathf.Max(1f, top - v.Root.position.y);
        }

        /// <summary>A place on the line in the world (on the floor).</summary>
        public Vector3 WorldOf(float x) => views[0] != null ? views[0].World(x) : centre + line * x;

        private void Update()
        {
            if (Sim == null) return;
            Time.timeScale = MomentsV2.Scale(proto.ToolScale);
            if (Frozen) { for (int i = 0; i < 2; i++) { views[i].Update(Sim.F[i], Sim.F[1 - i], Sim); views[i].FitWeapon(S.weaponLength[Sim.F[i].family] * Sim.Height); } return; }
            if (live)
            {
                // a live duel: the server's states shown (this copy never ticks), this game's wishes sent
                Sim.AdvanceClock(Time.deltaTime);
                while (proto.NextState(out var state)) Sim.Apply(state);
                SendLive(bots[0] != null ? bots[0].Think(Sim, Sim.F[0], Sim.F[1]) : PlayerInput(Sim.F[0], Sim.F[1])); // (the self-test: a bot plays foi live too)
            }
            else
            {
                var a = playerLeft ? PlayerInput(Sim.F[0], Sim.F[1]) : bots[0].Think(Sim, Sim.F[0], Sim.F[1]);
                var b = bots[1] != null ? bots[1].Think(Sim, Sim.F[1], Sim.F[0]) : FighterInput.None;
                Sim.Tick(Time.deltaTime, a, b);
            }
            foreach (var e in Sim.Events) Show(e);
            Sim.Events.Clear();
            Countdown();
            for (int i = 0; i < 2; i++) { views[i].Update(Sim.F[i], Sim.F[1 - i], Sim); views[i].FitWeapon(S.weaponLength[Sim.F[i].family] * Sim.Height); views[i].Aim(Sim.F[i], Sim, views[1 - i].Chest); }
            rains.Update(Sim);
            if (cam != null) cam.Rise(Sim.Rains.Count > 0 ? 0.8f * game.V2FighterScale / 3.5f : 0f); // (a little sky: the fight stays in the picture)
            LastSeconds();
            ShowScreen();
            if (replayAt > 0f && Time.unscaledTime >= replayAt) { replayAt = -1f; PlayReplay(); }
            if (doneAt > 0f && Time.unscaledTime >= doneAt && (replay == null || replay.Frame() == null)) { doneAt = -1f; proto.MatchDone(Sim); }
        }

        private float doneAt = -1f;

        private void LateUpdate()
        {
            if (cam == null || Sim == null) return;
            if (Frozen) cam.Follow(WorldOf(Sim.F[0].x - 0.6f), WorldOf(Sim.F[0].x + 0.6f), Time.unscaledDeltaTime); // (the weapons' sheet: foi alone)
            else cam.Follow(WorldOf(Sim.F[0].x), WorldOf(Sim.F[1].x), Time.unscaledDeltaTime);
            if (replay != null) replay.Record(WorldOf(Sim.F[0].x), WorldOf(Sim.F[1].x));
        }

        private void OnGUI()
        {
            if (Sim == null || ui != null) return; // (the game: its uGUI screen)
            hud.Draw(Sim, cam != null ? cam.Cam : null, proto, S, playerLeft, TopLine());
            if (rains != null && rains.Tint.a > 0.01f) GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), DuelHudV2.Tex(rains.Tint));
            // the ultimate: the world darkened round it; a perfect block: a short white flash
            if (Sim.F[0].state == FighterState.Casting && Sim.F[0].move == MoveKind.Ulti || Sim.F[1].state == FighterState.Casting && Sim.F[1].move == MoveKind.Ulti) GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), DuelHudV2.Tex(new Color(0f, 0f, 0.05f, 0.35f)));
            if (Time.unscaledTime < flashUntil) GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), DuelHudV2.Tex(new Color(1f, 1f, 1f, 0.55f * (flashUntil - Time.unscaledTime) / 0.12f)));
            var frame = replay != null ? replay.Frame() : null;
            if (frame != null)
            {
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), frame, ScaleMode.ScaleAndCrop);
                GUI.Label(new Rect(0f, Screen.height * 0.08f, Screen.width, 60f), Loc.T("v2.replay"), DuelHudV2.Style(34, Color.white, TextAnchor.MiddleCenter));
                if (Event.current.type == EventType.MouseDown || KeysV2.Down(UnityEngine.InputSystem.Key.Space)) replay.Stop();
                return;
            }
            if (matchOver && proto.IsPrototype) MatchPanel();
        }

        /// <summary>The line over the bars: the round, its clock, each side's rounds won.</summary>
        private string TopLine()
        {
            string dots(Fighter f) { var sb = new System.Text.StringBuilder(); for (int i = 0; i < S.roundsToWin; i++) sb.Append(i < f.roundsWon ? "●" : "○"); return sb.ToString(); }
            int secs = Mathf.CeilToInt(Mathf.Max(0f, Sim.RoundLeft));
            string clock = Sim.Golden ? Loc.T("v2.golden") + " " + secs : secs.ToString();
            return dots(Sim.F[0]) + "   " + Loc.F("v2.round", Sim.Round) + "  " + clock + "   " + dots(Sim.F[1]);
        }

        private int countShown = -1;

        /// <summary>The 3-2-1 over the two (the rules count; the screen shows it).</summary>
        private void Countdown()
        {
            if (Sim.Phase != RoundPhase.Countdown) { countShown = -1; return; }
            int n = Mathf.CeilToInt(Sim.PhaseLeft);
            if (n == countShown || n <= 0) return;
            countShown = n;
            hud.Banner(n.ToString(), new Color(1f, 0.85f, 0.3f), 0.9f);
            Audio.AudioManager.PlaySfx(Audio.Sfx.Click, 1f, 0.75f + 0.08f * (3 - n));
        }

        private bool matchOver;

        private void OnDestroy() { Time.timeScale = 1f; MomentsV2.Clear(); rains?.Clear(); if (replay != null) Destroy(replay.gameObject); if (ui != null) Destroy(ui.gameObject); }

        /// <summary>The game's screen (uGUI); null in the prototype (its IMGUI).</summary>
        private DuelHudV2Ui ui;
        public DuelHudV2Ui GameScreen => ui;

        /// <summary>The game's screen this frame: the bars and words, the tints (a rain's sky, the ultimate's dark, a perfect block's flash), the replay.</summary>
        private void ShowScreen()
        {
            if (ui == null) return;
            var c = rains != null ? rains.Tint : Color.clear;
            c.a *= 0.5f; // (a rain's sky on a phone: a hint, the fight still clear)
            if (Sim.F[0].state == FighterState.Casting && Sim.F[0].move == MoveKind.Ulti || Sim.F[1].state == FighterState.Casting && Sim.F[1].move == MoveKind.Ulti) c = Color.Lerp(c, new Color(0f, 0f, 0.05f, 0.35f), 0.7f);
            if (Time.unscaledTime < flashUntil) c = new Color(1f, 1f, 1f, 0.55f * (flashUntil - Time.unscaledTime) / 0.12f);
            ui.Show(Sim, cam != null ? cam.Cam : null, S, TopLine(), c, replay != null ? replay.Frame() : null);
            if (playerLeft) ui.ShowButtons(Sim, Sim.F[0], S);
        }

        // ---------- the player ----------

        private FighterInput PlayerInput(Fighter me, Fighter o)
        {
            if (ui != null)
            {
                // the game: the thumbs (taps, slides, swipes, the buttons; the touch layer reads A/D, W, S too), and the other keys
                var t = ui.Read(o.x >= me.x ? 1f : -1f);
                if (KeysV2.Down(UnityEngine.InputSystem.Key.Space)) t.attack = true;
                t.block |= KeysV2.Held(UnityEngine.InputSystem.Key.LeftShift) || KeysV2.Held(UnityEngine.InputSystem.Key.RightShift);
                if (KeysV2.Down(UnityEngine.InputSystem.Key.Q)) t.special = true;
                if (KeysV2.Down(UnityEngine.InputSystem.Key.E)) t.ulti = true;
                if (KeysV2.Down(UnityEngine.InputSystem.Key.R)) t.context = true;
                if (KeysV2.Down(UnityEngine.InputSystem.Key.H)) t.heart = true;
                if (KeysV2.Down(UnityEngine.InputSystem.Key.Digit1)) t.power = 0;
                if (KeysV2.Down(UnityEngine.InputSystem.Key.Digit2)) t.power = 1;
                if (KeysV2.Down(UnityEngine.InputSystem.Key.Digit3)) t.power = 2;
                return t;
            }
            var inp = hud.Pressed; hud.Pressed = FighterInput.None;
            if (KeysV2.Down(UnityEngine.InputSystem.Key.Space)) inp.attack = true;
            if (KeysV2.MouseDown && !hud.OverButton(new Vector2(KeysV2.MousePos.x, Screen.height - KeysV2.MousePos.y))) inp.attack = true;
            float world = (KeysV2.Held(UnityEngine.InputSystem.Key.D) || hud.HoldRight ? 1f : 0f) - (KeysV2.Held(UnityEngine.InputSystem.Key.A) || hud.HoldLeft ? 1f : 0f);
            inp.walk = world * (o.x >= me.x ? 1f : -1f);
            if (KeysV2.Down(UnityEngine.InputSystem.Key.W)) inp.jump = true;
            // S: a tap dodges, held it crouches (the picture's; the rules have no low blows yet)
            if (KeysV2.Down(UnityEngine.InputSystem.Key.S)) sHeld = Time.unscaledTime;
            if (KeysV2.Up(UnityEngine.InputSystem.Key.S) && sHeld >= 0f) { if (Time.unscaledTime - sHeld < 0.2f) inp.dodge = true; sHeld = -1f; }
            inp.block |= KeysV2.Held(UnityEngine.InputSystem.Key.LeftShift) || KeysV2.Held(UnityEngine.InputSystem.Key.RightShift) || hud.HoldBlock;
            if (KeysV2.Down(UnityEngine.InputSystem.Key.Q)) inp.special = true;
            if (KeysV2.Down(UnityEngine.InputSystem.Key.E)) inp.ulti = true;
            if (KeysV2.Down(UnityEngine.InputSystem.Key.R)) inp.context = true;
            if (KeysV2.Down(UnityEngine.InputSystem.Key.H)) inp.heart = true;
            if (KeysV2.Down(UnityEngine.InputSystem.Key.Digit1)) inp.power = 0;
            if (KeysV2.Down(UnityEngine.InputSystem.Key.Digit2)) inp.power = 1;
            if (KeysV2.Down(UnityEngine.InputSystem.Key.Digit3)) inp.power = 2;
            return inp;
        }

        // ---------- what happened, shown ----------

        /// <summary>The self-test's record of every event and each fighter's numbers.</summary>
        public static readonly System.Text.StringBuilder AutoLog = new System.Text.StringBuilder();

        public string Stats()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var f in Sim.F) sb.AppendLine((f.index == 0 ? "L" : "R") + " fam " + f.family + " hp " + Mathf.RoundToInt(f.hp) + "/" + Mathf.RoundToInt(f.maxHp) + " hits " + f.hitsDealt + " taken " + f.hitsTaken + " blocks " + f.blocks + " perfects " + f.perfects + " whiffs " + f.whiffs + " clashes " + f.clashes + " taps " + f.taps + " wasted " + f.wastedTaps + " meter " + f.meter.ToString("0.0") + " rounds " + f.roundsWon + " breaks " + f.breaks + " minGap " + (f.minGap < 9f ? Mathf.RoundToInt(f.minGap * 1000f) + "ms" : "-"));
            return sb.ToString();
        }

        private void Show(DuelEvent e)
        {
            if (DuelPrototypeV2.AutoDir != null && e.kind != DuelEventKind.Swing) AutoLog.AppendLine(e.time.ToString("0.00") + " " + e.kind + " " + (e.who == 0 ? "L" : "R") + " " + e.move + " " + e.result + " " + e.damage.ToString("0") + " n" + e.count + " " + e.why);
            proto.Event(e);
            var me = Sim.F[e.who]; var other = Sim.F[1 - e.who];
            var mv = views[e.who]; var ov = views[1 - e.who];
            switch (e.kind)
            {
                case DuelEventKind.Swing:
                    float windup = float.TryParse(e.why, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w : 0.1f;
                    mv.OnSwing(me, e, windup);
                    break;
                case DuelEventKind.Result:
                    ShowResult(e, me, other, mv, ov);
                    break;
                case DuelEventKind.ChainOpen: mv.OnChainOpen(S); break;
                case DuelEventKind.ChainBroken: mv.OnChainBroken(); hud.Float(Loc.T("v2.chainBroken"), new Color(1f, 0.5f, 0.4f), mv.Head, 22); hud.Log(Side(e.who) + " " + Loc.T("v2.chainBroken")); break;
                case DuelEventKind.Series: hud.Series(e.who, e.count); break;
                case DuelEventKind.PowerHitOffered:
                    if (e.who == 0 && playerLeft) hud.Float(Loc.T("v2.btn.power") + " (R)", new Color(1f, 0.7f, 0.3f), mv.Head + Vector3.up * 0.6f, 20);
                    if (S.showAdvantageHints) hud.Float(Loc.T("v2.gap"), new Color(0.5f, 1f, 0.7f), ov.Head + Vector3.up * 0.9f, 24); // (the set's end: the struck one's way out)
                    break;
                case DuelEventKind.BreakUsed:
                    hud.Banner(Loc.T("v2.btn.break"), new Color(0.4f, 0.8f, 1f), 0.6f);
                    hud.Log(Side(e.who) + " " + Loc.T("v2.btn.break"));
                    views[1 - e.who].OnChainBroken();
                    game.V2Ring(views[e.who].Root.position, 2.2f, new Color(0.4f, 0.8f, 1f));
                    MomentsV2.HitStop(70f);
                    break;
                case DuelEventKind.KnockdownOffered: if (e.who == 0 && playerLeft) hud.Float(Loc.T("v2.btn.knockdown") + " (R)", new Color(1f, 0.4f, 0.3f), mv.Head + Vector3.up * 0.6f, 22); break;
                case DuelEventKind.Dodge: mv.OnDodge(0.35f); break;
                case DuelEventKind.Jump: mv.OnJump(S.jumpSeconds * S.tempo, e.count == 1 ? Sim.Height * 0.95f : Sim.Height * 0.3f); break; // (over the other: high enough to clear its head)
                case DuelEventKind.ShieldBroke: hud.Float(Loc.T("v2.shieldBroke"), new Color(1f, 0.4f, 0.4f), mv.Head, 26); hud.Log(Side(e.who) + " " + Loc.T("v2.shieldBroke")); break;
                case DuelEventKind.Heart: hud.Float("+♥", new Color(1f, 0.4f, 0.5f), mv.Head, 30); break;
                case DuelEventKind.Special: hud.Float(Loc.T("v2.btn.special"), new Color(0.8f, 0.6f, 1f), mv.Head, 24); break;
                case DuelEventKind.UltiStart:
                    mv.OnUlti(DuelSim.UltiSeconds * S.tempo);
                    hud.Banner(Loc.T("v2.btn.ulti"), new Color(1f, 0.8f, 0.3f), 1.2f);
                    if (cam != null) cam.Zoom(0.15f);
                    // the charge: a ring of light at its feet, the weapon raised glowing, a rising rush of air
                    game.V2Ring(mv.Root.position + Vector3.up * 0.05f, 3.2f, new Color(1f, 0.8f, 0.3f));
                    game.V2Burst(mv.Chest, new Color(1f, 0.85f, 0.4f), new Color(3f, 2.4f, 0.8f), 30, 3f);
                    if (mv.Weapon != null) mv.Weapon.HoldUp(true);
                    DuelSoundsV2.Play(DuelSoundV2.SwingHeavy, 1f, 0.02f);
                    Invoke(nameof(UltiSwing), Mathf.Max(0.1f, DuelSim.UltiSeconds * S.tempo - 0.3f));
                    ultiBy = e.who;
                    break;
                case DuelEventKind.UltiHit:
                    ov.OnUltiStruck(mv.Root.position, S.airSeconds * S.tempo, S.downSeconds * S.tempo);
                    hud.Result(e, ov.Head);
                    if (cam != null) { cam.Zoom(0f); cam.Shake(Vector3.up, 0.8f); }
                    MomentsV2.HitStop(140f);
                    // the crash: a big burst on the struck one, a shockwave on the floor, the heaviest sounds
                    game.V2Burst(ov.Chest, new Color(1f, 0.75f, 0.3f), new Color(4f, 2.6f, 0.8f), 70, 11f);
                    game.V2Ring(ov.Root.position + Vector3.up * 0.05f, 5f, new Color(1f, 0.8f, 0.35f));
                    game.V2Dust(ov.Root.position, new Color(0.6f, 0.55f, 0.48f), 30, 5f);
                    DuelSoundsV2.Play(DuelSoundV2.Smash, 1f, 0.02f);
                    DuelSoundsV2.Play(DuelSoundV2.KO, 0.9f, 0.02f);
                    game.V2Cheer(e.who, 0.8f);
                    break;
                case DuelEventKind.KO:
                    ov.OnKO(mv.Root.position);
                    hud.Banner(Loc.T("v2.ko"), new Color(1f, 0.3f, 0.2f), 2f);
                    MomentsV2.Slow(S.koSlow, 0.25f);
                    if (cam != null) cam.Focus(ov.Chest, 0.5f);
                    OnKnockOut(e.who, e.why == "match");
                    break;
                case DuelEventKind.PowerEarned:
                    hud.Float(Loc.T("v2.power." + e.element), RainViewV2.ColourOf(e.element), mv.Head + Vector3.up * 0.4f, 26);
                    hud.Log(Side(e.who) + " +" + Loc.T("v2.power." + e.element));
                    Audio.AudioManager.PlaySfx(Audio.Sfx.Coin, 0.8f, 1.3f);
                    break;
                case DuelEventKind.PowerLost: hud.Float(Loc.T("v2.powerFull"), new Color(1f, 1f, 1f, 0.8f), mv.Head + Vector3.up * 0.4f, 18); break;
                case DuelEventKind.RainStart:
                    if (mv.Weapon != null) mv.Weapon.HoldUp(true);
                    Invoke(nameof(HandsDown), S.summonSeconds * S.tempo);
                    hud.Log(Side(e.who) + " " + Loc.T("v2.rain." + e.element));
                    break;
                case DuelEventKind.RainDrop:
                    if (e.result == HitResult.Hit || e.result == HitResult.Blocked)
                    {
                        rains.OnDrop(e, WorldOf(e.at));
                        if (e.element == ElementKind.Lightning && cam != null) cam.Shake(Vector3.up, e.why == "last" ? 0.5f : 0.2f); // (a bolt: the ground jolts)
                        if (e.result == HitResult.Hit) { if (ov.Skin != null) ov.Skin.Shudder(WorldOf(e.at) + Vector3.up * 5f, e.why == "last" ? 1f : 0.4f); }
                        hud.Float((e.result == HitResult.Blocked ? Loc.T("v2.res.blocked") + " " : "") + "-" + Mathf.RoundToInt(e.damage), RainViewV2.ColourOf(e.element), ov.Head, e.why == "last" ? 32 : 20);
                        if (e.element == ElementKind.Snow && e.result == HitResult.Hit && ov.Look != null) Combat.StatusFx.On(Combat.CombatFeel.RootOf(ov.Look) ?? ov.Root).Freeze(e.why == "last" ? 1.2f : 0.4f);
                        if (e.element == ElementKind.Fire && e.result == HitResult.Hit && ov.Look != null) Combat.StatusFx.On(Combat.CombatFeel.RootOf(ov.Look) ?? ov.Root).Burn(0.8f);
                    }
                    else if (e.result == HitResult.Dodged) rains.OnDrop(e, WorldOf(e.at));
                    if (e.why == "last" && e.result == HitResult.Hit)
                    {
                        ov.OnFall(WorldOf(e.at) + Vector3.up, S.downSeconds * S.tempo);
                        MomentsV2.Slow(S.rainLastSlow, 0.3f);
                        if (cam != null) { cam.Shake(Vector3.up, 0.6f); cam.Focus(ov.Chest, 0.35f); Invoke(nameof(Unfocus), 0.8f); }
                    }
                    break;
                case DuelEventKind.RoundIntro:
                    if (e.count > 1) foreach (var v in views) if (v.Skin != null) v.Skin.CleanSlate();
                    if (cam != null) cam.Focus(Vector3.zero, 0f);
                    hud.Banner(e.why == "final" ? Loc.T("v2.finalRound") : Loc.F("v2.round", e.count), Color.white, S.roundIntro);
                    break;
                case DuelEventKind.RoundFight: hud.Banner(Loc.T("v2.fight"), new Color(1f, 0.4f, 0.2f), 0.8f); Audio.AudioManager.PlaySfx(Audio.Sfx.Win, 0.8f, 1.1f); break;
                case DuelEventKind.TimeUp: hud.Banner(Loc.T("v2.timeUp"), Color.white, 1.4f); break;
                case DuelEventKind.GoldenRound: hud.Banner(Loc.T("v2.golden"), new Color(1f, 0.85f, 0.2f), 1.6f); break;
                case DuelEventKind.RoundOver:
                    string who = Loc.T(e.who == 0 ? (playerLeft ? "v2.side.me" : "v2.log.left") : "v2.side.rival");
                    hud.Banner(e.why.Contains("flawless") ? Loc.T("v2.flawless") : Loc.F("v2.roundWon", who), e.who == 0 ? new Color(0.5f, 0.85f, 1f) : new Color(1f, 0.55f, 0.45f), S.roundWinPose + 1f);
                    if (mv.Skin != null) mv.Skin.Clap();
                    hud.Log(Loc.F("v2.roundWon", who) + " (" + e.why + ")");
                    break;
                case DuelEventKind.MatchOver:
                    matchOver = true;
                    doneAt = Time.unscaledTime + S.koSlow + 2.5f + (replay != null && S.replay ? S.replaySeconds * 1.6f : 0f); // (the knock-out's slow motion, the pose, the replay: then the match is handed back)
                    hud.Banner(Sim.Comeback ? Loc.T("v2.comeback") : Loc.T(e.who == 0 && playerLeft ? "v2.youWin" : e.who == 1 && playerLeft ? "v2.youLose" : e.who == 0 ? "v2.leftWins" : "v2.rightWins"), Color.white, 4f);
                    if (mv.Weapon != null) mv.Weapon.Flourish();
                    break;
            }
        }

        private void ShowResult(DuelEvent e, Fighter me, Fighter other, FighterViewV2 mv, FighterViewV2 ov)
        {
            if (e.result == HitResult.Clash)
            {
                views[0].OnClash(views[1]);
                hud.Result(e, (views[0].Head + views[1].Head) * 0.5f);
                MomentsV2.HitStop(60f);
                if (cam != null) cam.Shake(line, 0.25f);
                return;
            }
            if (e.result == HitResult.Whiff || e.result == HitResult.Immune) { hud.Result(e, mv.Head); return; }
            ov.OnStruck(other, me, mv, e, S);
            hud.Result(e, ov.Head);
            switch (e.result)
            {
                case HitResult.Hit:
                    if (e.move == MoveKind.PowerHit) { MomentsV2.HitStop(S.powerHitStopMs); if (cam != null) cam.Shake(line * me.facing, 0.45f); }
                    else if (e.move == MoveKind.Knockdown) { MomentsV2.Slow(S.knockdownSlow, 0.3f); if (cam != null) { cam.Shake(Vector3.up, 0.4f); cam.Focus(ov.Chest + Vector3.up * 1.5f, 0.3f); Invoke(nameof(Unfocus), S.airSeconds); } }
                    else
                    {
                        bool third = e.move == MoveKind.Chain && me.chainStep >= S.chainLength - 1;
                        MomentsV2.HitStop(third ? S.thirdHitStopMs : S.hitStopMs[me.family]);
                        if (third && cam != null) { cam.Zoom(0.05f); Invoke(nameof(Unzoom), 0.35f); }
                    }
                    break;
                case HitResult.Perfect:
                    MomentsV2.Slow(S.perfectSlow, 0.3f);
                    flashUntil = Time.unscaledTime + 0.12f;
                    hud.Banner(Loc.T("v2.res.perfect"), Color.white, 0.6f);
                    break;
                case HitResult.Blocked:
                    MomentsV2.HitStop(25f);
                    break;
            }
        }

        private void Unzoom() { if (cam != null) cam.Zoom(0f); }
        private void Unfocus() { if (cam != null) cam.Focus(Vector3.zero, 0f); }
        private int ultiBy = -1;

        /// <summary>The ultimate's blow a moment before it lands: the weapon down in a full swing, the body with it.</summary>
        private void UltiSwing()
        {
            if (ultiBy < 0 || Sim == null) return;
            var v = views[ultiBy];
            if (v.Weapon != null) { v.Weapon.HoldUp(false); v.Weapon.SwingToContact(0.25f); }
            if (v.Skin != null) v.Skin.PlayChainAction(Combat.DuelAction.ChainSlash, 0.25f);
            DuelSoundsV2.Play(DuelSoundV2.SwingHeavy, 1f, 0.02f);
            ultiBy = -1;
        }

        private void HandsDown() { foreach (var v in views) if (v.Weapon != null) v.Weapon.HoldUp(false); }

        /// <summary>A knock-out: the crowd roars for the winner; the match's ends with its replay after the slow motion.</summary>
        private void OnKnockOut(int winner, bool matchEnds)
        {
            game.V2Cheer(winner, 1f);
            if (matchEnds && replay != null && S.replay && !replayDone) { replayDone = true; replayAt = Time.unscaledTime + S.koSlow + 0.3f; }
        }

        private float replayAt = -1f;

        private void PlayReplay() { if (replay != null) replay.Play(S.replaySeconds, S.replaySeconds * 1.6f); }

        /// <summary>The round's last seconds: the camera a touch nearer, a heartbeat each second.</summary>
        private void LastSeconds()
        {
            bool last = Sim.Phase == RoundPhase.Fight && Sim.RoundLeft <= S.lastSeconds;
            if (cam != null && Sim.Rains.Count == 0) cam.Zoom(last ? S.lastSecondsZoom : 0f);
            if (!last) return;
            if (Time.unscaledTime >= heartAt)
            {
                heartAt = Time.unscaledTime + 1f;
                Audio.AudioManager.PlaySfx(Audio.Sfx.Impact, 0.35f, 0.45f);
                Invoke(nameof(HeartSecond), 0.18f);
            }
        }

        private void HeartSecond() => Audio.AudioManager.PlaySfx(Audio.Sfx.Impact, 0.25f, 0.42f);

        /// <summary>The weapons' sheet (Docs 7.2): every hand weapon in foi's hand, standing and mid-blow, fitted to its family's length.</summary>
        /// <summary>The rules stand still (the weapons' sheet): the two only shown where they are.</summary>
        public bool Frozen;

        public System.Collections.IEnumerator WeaponSheet(string dir)
        {
            while (Sim.Phase != RoundPhase.Fight) yield return null;
            Frozen = true;
            Sim.F[0].x = 0f; Sim.F[0].facing = 1;
            Sim.F[1].x = Sim.HalfLength * 3f; // (the rival out of the way)
            if (cam != null) cam.Zoom(0.3f);
            int n = 0;
            foreach (var w in Data.Armory.All)
            {
                if (w.kind == Data.WeaponKind.Gun) continue;
                int fam = (int)Data.Armory.Family(w);
                views[0].Hold(w);
                Sim.F[0].family = fam;
                yield return new WaitForSecondsRealtime(0.35f);
                views[0].FitWeapon(S.weaponLength[fam] * Sim.Height);
                if (cam != null) cam.Focus(views[0].Chest, 0.6f);
                yield return new WaitForSecondsRealtime(0.25f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "w" + (n).ToString("00") + "_" + w.id + "_rest.png"));
                yield return new WaitForSecondsRealtime(0.2f);
                if (views[0].Weapon != null) views[0].Weapon.Swing();
                yield return new WaitForSecondsRealtime(0.11f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "w" + (n++).ToString("00") + "_" + w.id + "_blow.png"));
                yield return new WaitForSecondsRealtime(0.5f);
            }
            AutoLog.AppendLine("weapons " + n + " height " + Sim.Height.ToString("0.00"));
        }

        /// <summary>
        /// The self-test's poses (-v2Pose): the two face to face at striking distance, each family's weapon in both hands'
        /// sides, at rest and through a blow (wind-up, contact, after), from the side; the off arm's place logged.
        /// </summary>
        public System.Collections.IEnumerator PoseSheet(string dir)
        {
            while (Sim.Phase != RoundPhase.Fight) yield return null;
            Frozen = true;
            if (cam != null) cam.Zoom(0f);
            for (int fam = 0; fam < 4; fam++)
            {
                var w = Data.Armory.All.Find(x => x.kind != Data.WeaponKind.Gun && (int)Data.Armory.Family(x) == fam);
                for (int i = 0; i < 2; i++) { views[i].Hold(w); Sim.F[i].family = fam; }
                float d = Sim.ReachOf(fam) * 0.95f;
                Sim.F[0].x = -d * 0.5f; Sim.F[0].facing = 1; Sim.F[1].x = d * 0.5f; Sim.F[1].facing = -1;
                yield return new WaitForSecondsRealtime(0.5f);
                if (cam != null) cam.Focus((views[0].Chest + views[1].Chest) * 0.5f, 0.5f);
                yield return new WaitForSecondsRealtime(0.4f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "pose_" + fam + "_rest.png"));
                if (cam != null) { cam.Zoom(0.5f); cam.Focus(views[1].Chest, 1f); }
                yield return new WaitForSecondsRealtime(0.5f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "pose_" + fam + "_rival.png"));
                yield return null;
                if (cam != null) { cam.Zoom(0f); cam.Focus((views[0].Chest + views[1].Chest) * 0.5f, 0.5f); }
                yield return new WaitForSecondsRealtime(0.4f);
                AutoLog.AppendLine("pose fam " + fam + " " + w.id + ": " + OffArm(views[0]) + " | " + OffArm(views[1]));
                if (fam == 0 || fam == 2) { Around(0, dir, "around_" + fam + "_me"); Around(1, dir, "around_" + fam + "_rival"); }
                for (int i = 0; i < 2; i++)
                {
                    float windup = S.Windup(fam);
                    views[i].OnSwing(Sim.F[i], new DuelEvent { kind = DuelEventKind.Swing, who = i, move = MoveKind.Chain }, windup);
                    float t0 = Time.realtimeSinceStartup;
                    float[] at = { windup * 0.5f, windup, windup + S.S(S.activeMs[fam]) + 0.06f };
                    for (int k = 0; k < at.Length; k++)
                    {
                        while (Time.realtimeSinceStartup - t0 < at[k]) yield return null;
                        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "pose_" + fam + "_" + (i == 0 ? "L" : "R") + k + ".png"));
                        yield return null;
                    }
                    AutoLog.AppendLine("  swing " + (i == 0 ? "L" : "R") + " by body " + views[i].SwungByBody + " contact: " + OffArm(views[i]));
                    yield return new WaitForSecondsRealtime(0.8f);
                }
            }
        }

        /// <summary>The off (left) hand's place against the chest, in body heights (sunk into the trunk when small), and its arm's scale.</summary>
        private string OffArm(FighterViewV2 v)
        {
            if (v.Skin == null) return "no skin";
            Transform Find(string end) { foreach (var t in v.Skin.GetComponentsInChildren<Transform>(true)) if (t.name.EndsWith(":" + end) || t.name == end) return t; return null; }
            var hand = Find("LeftHand"); var chest = Find("Spine2") ?? Find("Spine1"); var arm = Find("LeftArm");
            if (hand == null || chest == null) return "no bones";
            float h = Mathf.Max(0.01f, Sim.Height);
            var local = v.Root.InverseTransformPoint(hand.position) - v.Root.InverseTransformPoint(chest.position);
            var shield = v.Skin.GetComponent<Visual.ShieldHold>();
            return "hand-chest " + (Vector3.Distance(hand.position, chest.position) / h).ToString("0.00") + "h local " + (local / h).ToString("0.00") + " armScale " + (arm != null ? arm.lossyScale.x.ToString("0.000") : "-") + " shield " + (shield != null && shield.Carried) + " twoHanded " + v.Skin.TwoHandedNow;
        }

        /// <summary>
        /// The left forearm's skin as drawn (the self-test): for each skinned mesh, how many of its points the forearm
        /// carries and how far their middle sits from the forearm bone (in body heights), with the blend shapes' weights.
        /// </summary>
        public string ArmMesh(int i)
        {
            var v = views[i];
            if (v.Skin == null) return "no skin";
            var sb = new System.Text.StringBuilder();
            foreach (var smr in v.Skin.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                int fore = System.Array.FindIndex(smr.bones, b => b != null && (b.name == "LeftForeArm" || b.name.EndsWith(":LeftForeArm")));
                sb.Append(" [" + smr.name + " on " + smr.enabled + "/" + smr.gameObject.activeInHierarchy + " v" + mesh.vertexCount);
                for (int s = 0; s < mesh.blendShapeCount; s++) if (smr.GetBlendShapeWeight(s) > 0.01f) sb.Append(" " + mesh.GetBlendShapeName(s) + "=" + smr.GetBlendShapeWeight(s).ToString("0"));
                if (fore >= 0)
                {
                    var baked = new Mesh(); smr.BakeMesh(baked, true);
                    var pts = baked.vertices; var bw = mesh.boneWeights;
                    Vector3 sum = Vector3.zero; int n = 0;
                    for (int k = 0; k < bw.Length && k < pts.Length; k++) if (bw[k].boneIndex0 == fore && bw[k].weight0 > 0.5f) { sum += smr.transform.TransformPoint(pts[k]); n++; }
                    if (n > 0) sb.Append(" foreL pts " + n + " off " + (Vector3.Distance(sum / n, smr.bones[fore].position) / Mathf.Max(0.01f, Sim.Height)).ToString("0.00") + "h");
                    Destroy(baked);
                }
                sb.Append("]");
            }
            return sb.ToString();
        }

        /// <summary>A picture of fighter <paramref name="i"/> from its front, its left and its right (the self-test), each its own file.</summary>
        public void Around(int i, string dir, string tag)
        {
            var v = views[i];
            var go = new GameObject("PoseCam");
            var c = go.AddComponent<Camera>();
            c.fieldOfView = 35f; c.nearClipPlane = 0.05f;
            var rt = new RenderTexture(480, 720, 24);
            c.targetTexture = rt;
            float h = Sim.Height;
            var mid = v.Root.position + Vector3.up * h * 0.55f;
            var sides = new[] { ("front", v.Root.forward), ("left", -v.Root.right), ("right", v.Root.right) };
            foreach (var (name, dirV) in sides)
            {
                go.transform.position = mid + dirV * h * 2.2f;
                go.transform.LookAt(mid);
                c.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, tag + "_" + name + ".png"), tex.EncodeToPNG());
                Destroy(tex);
            }
            RenderTexture.active = null;
            c.targetTexture = null; Destroy(rt); Destroy(go);
        }

        /// <summary>A new match on the same stage.</summary>
        public void Restart()
        {
            matchOver = false;
            replayDone = false;
            doneAt = -1f;
            Sim = new DuelSim(S, proto.Options.family, proto.Options.tier, Sim.HalfLength, Sim.Height);
            foreach (var v in views) if (v.Skin != null) v.Skin.CleanSlate();
            if (cam != null) cam.Focus(Vector3.zero, 0f);
            MomentsV2.Clear();
        }

        private void MatchPanel()
        {
            float k = Mathf.Max(0.6f, Screen.height / 1170f);
            var r = new Rect(Screen.width * 0.15f, Screen.height * 0.55f, Screen.width * 0.7f, 160f * k);
            GUI.Box(r, "");
            GUI.Label(new Rect(r.x, r.y + 6f, r.width, 34f * k), Loc.F("v2.score", Sim.F[0].roundsWon, Sim.F[1].roundsWon), DuelHudV2.Style(26, Color.white, TextAnchor.MiddleCenter));
            if (GUI.Button(new Rect(r.x + 12f, r.y + 54f * k, r.width * 0.5f - 18f, 80f * k), Loc.T("v2.again"))) Restart();
            if (GUI.Button(new Rect(r.center.x + 6f, r.y + 54f * k, r.width * 0.5f - 18f, 80f * k), Loc.T("v2.menu"))) proto.BackToMenu();
        }

        private string Side(int who) => Loc.T(who == 0 ? "v2.log.left" : "v2.log.right");
    }
}
