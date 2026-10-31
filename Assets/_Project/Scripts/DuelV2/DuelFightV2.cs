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
            Sim = new DuelSim(S, o.family, o.tier, half);
            float scale = game.V2FighterScale;
            views[0] = new FighterViewV2(game, game.V2MeRoot, game.V2MeLook, game.V2MeWeapon, centre, line, scale);
            var foeLook = game.V2FoeRoot != null ? game.V2FoeRoot.GetComponentInChildren<Visual.SkinnedHero>()?.transform.parent : null;
            views[1] = new FighterViewV2(game, game.V2FoeRoot, foeLook != null ? foeLook : game.V2FoeRoot, game.V2FoeWeapon, centre, line, scale);
            playerLeft = o.mode != PrototypeOptions.Mode.BotVsBot;
            bots[0] = playerLeft ? null : new DuelBotV2(o.skill[0], o.persona[0], 11);
            bots[1] = o.mode == PrototypeOptions.Mode.PlayerVsDummy ? null : new DuelBotV2(o.skill[1], o.persona[1], 23);
            MomentsV2.Clear();
            OnStarted();
        }

        /// <summary>The fight's first moment (the rounds' intro from Faz 4 on).</summary>
        private void OnStarted() { }

        /// <summary>A place on the line in the world (on the floor).</summary>
        public Vector3 WorldOf(float x) => views[0] != null ? views[0].World(x) : centre + line * x;

        private void Update()
        {
            if (Sim == null) return;
            Time.timeScale = MomentsV2.Scale(proto.ToolScale);
            var a = playerLeft ? PlayerInput(Sim.F[0], Sim.F[1]) : bots[0].Think(Sim, Sim.F[0], Sim.F[1]);
            var b = bots[1] != null ? bots[1].Think(Sim, Sim.F[1], Sim.F[0]) : FighterInput.None;
            Sim.Tick(Time.deltaTime, a, b);
            foreach (var e in Sim.Events) Show(e);
            Sim.Events.Clear();
            for (int i = 0; i < 2; i++) views[i].Update(Sim.F[i], Sim.F[1 - i], Sim);
        }

        private void LateUpdate()
        {
            if (cam == null || Sim == null) return;
            cam.Follow(WorldOf(Sim.F[0].x), WorldOf(Sim.F[1].x), Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            if (Sim == null) return;
            hud.Draw(Sim, cam != null ? cam.Cam : null, proto, S, playerLeft, TopLine());
        }

        /// <summary>The line over the bars (the rounds' from Faz 4 on).</summary>
        private string TopLine() => "";

        private void OnDestroy() { Time.timeScale = 1f; MomentsV2.Clear(); }

        // ---------- the player ----------

        private FighterInput PlayerInput(Fighter me, Fighter o)
        {
            var inp = hud.Pressed; hud.Pressed = FighterInput.None;
            if (Input.GetKeyDown(KeyCode.Space)) inp.attack = true;
            if (Input.GetMouseButtonDown(0) && !hud.OverButton(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y))) inp.attack = true;
            float world = (Input.GetKey(KeyCode.D) || hud.HoldRight ? 1f : 0f) - (Input.GetKey(KeyCode.A) || hud.HoldLeft ? 1f : 0f);
            inp.walk = world * (o.x >= me.x ? 1f : -1f);
            if (Input.GetKeyDown(KeyCode.W)) inp.jump = true;
            // S: a tap dodges, held it crouches (the picture's; the rules have no low blows yet)
            if (Input.GetKeyDown(KeyCode.S)) sHeld = Time.unscaledTime;
            if (Input.GetKeyUp(KeyCode.S) && sHeld >= 0f) { if (Time.unscaledTime - sHeld < 0.2f) inp.dodge = true; sHeld = -1f; }
            inp.block |= Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || hud.HoldBlock;
            if (Input.GetKeyDown(KeyCode.Q)) inp.special = true;
            if (Input.GetKeyDown(KeyCode.E)) inp.ulti = true;
            if (Input.GetKeyDown(KeyCode.R)) inp.context = true;
            if (Input.GetKeyDown(KeyCode.H)) inp.heart = true;
            if (Input.GetKeyDown(KeyCode.Alpha1)) inp.power = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) inp.power = 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) inp.power = 2;
            return inp;
        }

        // ---------- what happened, shown ----------

        /// <summary>The self-test's record of every event and each fighter's numbers.</summary>
        public static readonly System.Text.StringBuilder AutoLog = new System.Text.StringBuilder();

        public string Stats()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var f in Sim.F) sb.AppendLine((f.index == 0 ? "L" : "R") + " fam " + f.family + " hp " + Mathf.RoundToInt(f.hp) + "/" + Mathf.RoundToInt(f.maxHp) + " hits " + f.hitsDealt + " taken " + f.hitsTaken + " blocks " + f.blocks + " perfects " + f.perfects + " whiffs " + f.whiffs + " clashes " + f.clashes + " taps " + f.taps + " wasted " + f.wastedTaps + " meter " + f.meter.ToString("0.0") + " rounds " + f.roundsWon);
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
                case DuelEventKind.PowerHitOffered: if (e.who == 0 && playerLeft) hud.Float(Loc.T("v2.btn.power") + " (R)", new Color(1f, 0.7f, 0.3f), mv.Head + Vector3.up * 0.6f, 20); break;
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
                    OnKnockOut(e.who);
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
                    else if (e.move == MoveKind.Knockdown) { MomentsV2.Slow(S.knockdownSlow, 0.3f); if (cam != null) cam.Shake(Vector3.up, 0.4f); }
                    else
                    {
                        bool third = e.move == MoveKind.Chain && me.chainStep >= S.chainLength - 1;
                        MomentsV2.HitStop(third ? S.thirdHitStopMs : S.hitStopMs[me.family]);
                        if (third && cam != null) { cam.Zoom(0.05f); Invoke(nameof(Unzoom), 0.35f); }
                    }
                    break;
                case HitResult.Perfect:
                    MomentsV2.Slow(S.perfectSlow, 0.3f);
                    hud.Banner(Loc.T("v2.res.perfect"), Color.white, 0.6f);
                    break;
                case HitResult.Blocked:
                    MomentsV2.HitStop(25f);
                    break;
            }
        }

        private void Unzoom() { if (cam != null) cam.Zoom(0f); }

        /// <summary>A knock-out (the rounds take it from Faz 4 on: here the fight starts over after a moment).</summary>
        private void OnKnockOut(int winner) => Invoke(nameof(Restart), 3.5f);

        private void Restart()
        {
            Sim = new DuelSim(S, proto.Options.family, proto.Options.tier, Sim.HalfLength);
            foreach (var v in views) if (v.Skin != null) v.Skin.CleanSlate();
            if (cam != null) cam.Focus(Vector3.zero, 0f);
        }

        private string Side(int who) => Loc.T(who == 0 ? "v2.log.left" : "v2.log.right");
    }
}
