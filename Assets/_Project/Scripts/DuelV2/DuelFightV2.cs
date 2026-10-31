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
        private DuelPrototypeV2 proto;
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
        private bool playerLeft;
        private DuelCoreSettings S => DuelCoreSettings.Current;

        public static DuelFightV2 Begin(DuelPrototypeV2 proto, DuelCameraV2 cam)
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
            bots[0] = playerLeft ? null : new DuelBotV2(o.skill[0], o.persona[0], 11);
            bots[1] = o.mode == PrototypeOptions.Mode.PlayerVsDummy ? null : new DuelBotV2(o.skill[1], o.persona[1], 23);
            MomentsV2.Clear();
            rains = new RainViewV2(game, WorldOf, scale);
            float height = FighterHeight(views[0]);
            if (cam != null) cam.FighterHeight = height;
            if (S.replay) replay = ReplayRecorderV2.Create(game.V2CameraSide, line, height, S.replaySeconds);
            OnStarted();
        }

        /// <summary>The fight's first moment (the rounds' intro from Faz 4 on).</summary>
        private void OnStarted() { }

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
            var a = playerLeft ? PlayerInput(Sim.F[0], Sim.F[1]) : bots[0].Think(Sim, Sim.F[0], Sim.F[1]);
            var b = bots[1] != null ? bots[1].Think(Sim, Sim.F[1], Sim.F[0]) : FighterInput.None;
            Sim.Tick(Time.deltaTime, a, b);
            foreach (var e in Sim.Events) Show(e);
            Sim.Events.Clear();
            Countdown();
            for (int i = 0; i < 2; i++) { views[i].Update(Sim.F[i], Sim.F[1 - i], Sim); views[i].FitWeapon(S.weaponLength[Sim.F[i].family] * Sim.Height); }
            rains.Update(Sim);
            if (cam != null) cam.Rise(Sim.Rains.Count > 0 ? 2.2f * game.V2FighterScale / 3.5f : 0f);
            LastSeconds();
            if (replayAt > 0f && Time.unscaledTime >= replayAt) { replayAt = -1f; PlayReplay(); }
        }

        private void LateUpdate()
        {
            if (cam == null || Sim == null) return;
            if (Frozen) cam.Follow(WorldOf(Sim.F[0].x - 0.6f), WorldOf(Sim.F[0].x + 0.6f), Time.unscaledDeltaTime); // (the weapons' sheet: foi alone)
            else cam.Follow(WorldOf(Sim.F[0].x), WorldOf(Sim.F[1].x), Time.unscaledDeltaTime);
            if (replay != null) replay.Record(WorldOf(Sim.F[0].x), WorldOf(Sim.F[1].x));
        }

        private void OnGUI()
        {
            if (Sim == null) return;
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
            if (matchOver) MatchPanel();
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

        private void OnDestroy() { Time.timeScale = 1f; MomentsV2.Clear(); rains?.Clear(); if (replay != null) Destroy(replay.gameObject); }

        // ---------- the player ----------

        private FighterInput PlayerInput(Fighter me, Fighter o)
        {
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
                case DuelEventKind.Jump: mv.OnJump(S.jumpSeconds * S.tempo); break;
                case DuelEventKind.ShieldBroke: hud.Float(Loc.T("v2.shieldBroke"), new Color(1f, 0.4f, 0.4f), mv.Head, 26); hud.Log(Side(e.who) + " " + Loc.T("v2.shieldBroke")); break;
                case DuelEventKind.Heart: hud.Float("+♥", new Color(1f, 0.4f, 0.5f), mv.Head, 30); break;
                case DuelEventKind.Special: hud.Float(Loc.T("v2.btn.special"), new Color(0.8f, 0.6f, 1f), mv.Head, 24); break;
                case DuelEventKind.UltiStart:
                    mv.OnUlti(DuelSim.UltiSeconds * S.tempo);
                    hud.Banner(Loc.T("v2.btn.ulti"), new Color(1f, 0.8f, 0.3f), 1.2f);
                    if (cam != null) cam.Zoom(0.15f);
                    break;
                case DuelEventKind.UltiHit:
                    ov.OnFall(mv.Root.position, S.downSeconds * S.tempo);
                    hud.Result(e, ov.Head);
                    if (cam != null) { cam.Zoom(0f); cam.Shake(Vector3.up, 0.5f); }
                    MomentsV2.HitStop(120f);
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

        /// <summary>A new match on the same stage.</summary>
        public void Restart()
        {
            matchOver = false;
            replayDone = false;
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
