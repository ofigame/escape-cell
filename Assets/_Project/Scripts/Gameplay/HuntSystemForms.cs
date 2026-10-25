using System;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The bosses (the fixes design's job 8). Floors 1-9 of a world end with vanG's GUARDIAN: smaller, grey, one stage, a
    /// big attack only now and then, no rage. A world's tenth floor brings a FORM of vanG ("Kanal Bekçisi · vanG's guise")
    /// that fights with the very move the world then gives the player: its specials and ultimates are its big attacks,
    /// each shown first (red tiles, a shell, a mark on foi), its passives change how it fights. The last world's tenth
    /// floor is vanG itself (its core burst in the last stage).
    /// </summary>
    public partial class HuntSystem
    {
        /// <summary>Who this floor's boss is, and the form's move.</summary>
        public BossKind Boss = BossKind.Real;
        public FormMove Form = FormMove.None;
        /// <summary>The player's own ultimate (the copier copies it).</summary>
        public FormMove PlayerUlti = FormMove.Quake;
        /// <summary>A guardian's big attack every this many seconds; a form's first one this soon.</summary>
        public float GuardianBigEvery = 12f, FormFirstMove = 4f;

        /// <summary>A form used its move (where, the move): the game shows its name over it.</summary>
        public event Action<Vector3, FormMove> FormShout;
        /// <summary>A form's move holds foi a moment (no blows), or slows it.</summary>
        public event Action<float> FoiHeld, FoiSlowed;

        // the form's moments: untouchable, mirroring, its front shield; the passives' once-only turns; repeated hits
        private float formIronUntil, formMirrorUntil, formShieldUntil, formShieldAt;
        private bool formHealed, formSturdy, formIronUsed, formRageShown;
        private float formPierceShoutAt;
        private int formRepeats;
        private float formTimes = 1f;
        private const int FormKind = 10; // (a form's big attack: specialKind 10 + its move)

        /// <summary>A new floor: the boss's moments cleared, a form's first move soon.</summary>
        private void BeginBoss()
        {
            formIronUntil = formMirrorUntil = formShieldUntil = 0f;
            formShieldAt = Time.time + 6f;
            formHealed = formIronUsed = formRageShown = false;
            formPierceShoutAt = 0f;
            formSturdy = Form == FormMove.Sturdy;
            formRepeats = 0;
            formTimes = 1f;
            if (Boss == BossKind.Form && Form != FormMove.None) specialTimer = FormFirstMove;
            if (Boss == BossKind.Guardian) specialTimer = GuardianBigEvery;
        }

        /// <summary>The boss's blow on foi (its move's share of a blow).</summary>
        private float BossHitShare => MonsterHitShare * formTimes;

        private bool FormIs(FormMove m) => Boss == BossKind.Form && Form == m;

        /// <summary>The last blow on foi was the piercing form's own (it eats foi's shield three times as fast).</summary>
        public bool BlowPierces => FormIs(FormMove.Piercer) && MonsterUp && (BlowFrom - At(monsterPos)).sqrMagnitude < 0.1f;

        /// <summary>The form's rage shows: a red, faster boss below half its health (the rage form far faster).</summary>
        private float RageTimes(bool enraged) => !enraged || Boss == BossKind.Guardian ? 1f : FormIs(FormMove.Rage) ? 1.7f : 1.35f;

        private void Shout(FormMove m) => FormShout?.Invoke(monster != null ? monster.transform.position + Vector3.up * 2.4f : At(monsterPos), m);

        /// <summary>
        /// A blow on the boss meets its moves first: untouchable (iron body), mirrored back at foi, its front shield (the
        /// shield master), the sturdy spare life. Returns the damage that gets through (0: none).
        /// </summary>
        private int BossTakes(int damage)
        {
            if (Boss != BossKind.Form) return damage;
            if (Time.time < formIronUntil) { fx.Burst(At(monsterPos) + Vector3.up * 1f, new Color(0.8f, 0.85f, 0.95f), new Color(1.6f, 1.7f, 2f), 12, 3f); AudioManager.PlaySfx(Sfx.Blocked, 1f, 1.3f); return 0; }
            if (Time.time < formMirrorUntil)
            {
                // the mirror: foi's blow comes back at foi
                BlowFrom = At(monsterPos);
                Hit?.Invoke(robot.Position, MonsterHitShare * 0.5f);
                fx.Burst(At(monsterPos) + Vector3.up * 1f, new Color(0.7f, 0.95f, 1f), new Color(1.6f, 2.4f, 3f), 16, 4f);
                return 0;
            }
            if (Time.time < formShieldUntil && monster != null)
            {
                var toFoi = robot.transform.position - At(monsterPos); toFoi.y = 0f;
                if (toFoi.sqrMagnitude > 0.01f && Vector3.Dot(monster.Forward, toFoi.normalized) > 0.2f) damage = Mathf.Max(1, Mathf.RoundToInt(damage * 0.3f)); // (from the front: its shield takes most)
            }
            if (formSturdy && monsterHp - damage <= 0) { formSturdy = false; Shout(FormMove.Sturdy); return Mathf.Max(0, monsterHp - 1); }
            return damage;
        }

        /// <summary>The boss's blow landed on foi: the vampire heals off it.</summary>
        private void BossLanded()
        {
            if (FormIs(FormMove.Piercer) && Time.time >= formPierceShoutAt) { formPierceShoutAt = Time.time + 6f; Shout(FormMove.Piercer); } // (its blows eat foi's shield: named now and then)
            if (FormIs(FormMove.Vampire) && monsterHp < MonsterHpTotal)
            {
                monsterHp = Mathf.Min(MonsterHpTotal, monsterHp + Mathf.Max(1, MonsterHpTotal / 33));
                monster.Hit(monsterHp);
                fx.Burst(At(monsterPos) + Vector3.up * 1.2f, new Color(1f, 0.25f, 0.35f), new Color(2.4f, 0.5f, 0.7f), 12, 3f);
                Shout(FormMove.Vampire);
            }
        }

        /// <summary>Each frame of the awake boss: the passives' turns (the heart master's one heal, the shield master's shield).</summary>
        private void UpdateBossPassives()
        {
            if (Boss != BossKind.Form) return;
            if (FormIs(FormMove.Rage) && !formRageShown && monsterHp * 2 <= MonsterHpTotal)
            {
                // the rage shows: red and much faster from half its health
                formRageShown = true;
                monster.Grow(Mathf.Max(1, monsterStage + 1));
                Shockwave.Create(At(monsterPos), 3f, new Color(1f, 0.2f, 0.15f));
                Shout(FormMove.Rage);
            }
            if (FormIs(FormMove.HeartMaster) && !formHealed && monsterHp * 10 <= MonsterHpTotal * 3)
            {
                formHealed = true;
                monsterHp = Mathf.Min(MonsterHpTotal, monsterHp + MonsterHpTotal / 4);
                monster.Hit(monsterHp);
                MoveFx.Pillar(At(monsterPos), new Color(0.45f, 1f, 0.55f), 1.2f, 7f, 1f);
                Shout(FormMove.HeartMaster);
            }
            if (FormIs(FormMove.ShieldMaster) && Time.time >= formShieldAt)
            {
                formShieldAt = Time.time + 8f;
                formShieldUntil = Time.time + 3f;
                if (monster != null) MoveFx.Shell(monster.transform, new Color(0.6f, 0.75f, 1f), 3f, 0.35f);
                Shout(FormMove.ShieldMaster);
            }
        }

        /// <summary>A stage crossed: the iron body hardens (once), the master warrior starts with its big attack ready.</summary>
        private void BossStageCrossed()
        {
            if (FormIs(FormMove.IronBody) && !formIronUsed) { formIronUsed = true; IronUp(); }
            if (FormIs(FormMove.MasterWarrior)) { specialTimer = 0.6f; Shout(FormMove.MasterWarrior); }
            if (FormIs(FormMove.Sturdy)) formSturdy = true; // (each stage's first deadly blow survived)
        }

        private void IronUp()
        {
            formIronUntil = Time.time + 5f;
            if (monster != null) MoveFx.Shell(monster.transform, new Color(0.8f, 0.82f, 0.9f), 5f, 0.4f);
            Shout(FormMove.IronBody);
        }

        /// <summary>The big attack's pace: a guardian's seldom, the fast-charging form's often.</summary>
        private float BigAttackEvery(float hard) =>
            Boss == BossKind.Guardian ? GuardianBigEvery : Mathf.Lerp(9f, 6f, hard) * (FormIs(FormMove.FastCharge) ? 0.6f : 1f);

        /// <summary>The move this form's big attack plays (a passive form: the world's own big attack; null).</summary>
        private FormMove? BigMove()
        {
            if (Boss == BossKind.Real && MonsterStages > 1 && monsterStage >= MonsterStages - 1) return FormMove.CoreBurst; // (vanG itself: its core in the last stage)
            if (Boss != BossKind.Form) return null;
            var m = Form == FormMove.Copy ? PlayerUlti : Form;
            if (m >= FormMove.ComboMaster || m == FormMove.None) return null;
            if (m == FormMove.Patch && monsterHp * 10 > MonsterHpTotal * 7) return null; // (the patch only once it is hurt)
            return m;
        }

        /// <summary>Lays out a form's big attack (its tiles, its show); false when it can't now.</summary>
        private bool StartFormMove(FormMove m)
        {
            attackTiles.Clear();
            formTimes = 1f;
            var foi = robot.Position;
            switch (m)
            {
                case FormMove.Leap:
                case FormMove.SkyStrike:
                    // a leap at foi: where it lands glows first, then the crash round it
                    if (!TryLeap()) return false;
                    Ring(foi, 1, true);
                    formTimes = m == FormMove.SkyStrike ? 1.3f : 1f;
                    break;
                case FormMove.Backflip:
                    // a jump back, then the press: the line from it to foi (close enough first)
                    if (Chebyshev(monsterPos, foi) > 3) return false;
                    TryLeapAway();
                    Line(monsterPos, foi, 4);
                    formTimes = 1.2f;
                    break;
                case FormMove.Hook:
                case FormMove.Shock:
                case FormMove.IceWall:
                    // a line at foi: the hook drags foi in, the shock and the ice hold it (in reach first)
                    if (Chebyshev(monsterPos, foi) > (m == FormMove.Hook ? 5 : 4)) return false;
                    Line(monsterPos, foi, m == FormMove.Hook ? 5 : 4);
                    formTimes = m == FormMove.Hook ? 1f : 0.6f;
                    if (m == FormMove.Hook && monster != null && robot.Visual != null) MoveFx.Chain(monster.transform, robot.Visual, 0.9f);
                    break;
                case FormMove.Patch:
                    MoveFx.Pillar(At(monsterPos), new Color(0.45f, 1f, 0.55f), 1f, 6f, 1.2f); // (no tiles: it mends itself as the light rises)
                    break;
                case FormMove.Teleport:
                {
                    // gone and out at foi's side: a quick strike round it
                    var side = FreeBeside(foi);
                    if (!side.HasValue) return false;
                    MoveFx.Pillar(At(monsterPos), new Color(0.75f, 0.45f, 1f), 0.9f, 4f, 0.4f);
                    grid.SetOccupied(monsterPos, false);
                    monsterPos = side.Value;
                    grid.SetOccupied(monsterPos, true);
                    if (monster != null) monster.transform.position = At(monsterPos);
                    MoveFx.Pillar(At(monsterPos), new Color(0.75f, 0.45f, 1f), 0.9f, 4f, 0.4f);
                    Ring(monsterPos, 1, false);
                    formTimes = 1.1f;
                    break;
                }
                case FormMove.Quake:
                    Ring(monsterPos, 3, false);
                    formTimes = 1.2f;
                    break;
                case FormMove.Whirlwind:
                    Ring(monsterPos, 1, false);
                    formRepeats = 3; // (four spins in all, a step at foi between them)
                    formTimes = 0.6f;
                    if (monster != null) MoveFx.Whirl(monster.transform, new Color(0.7f, 0.95f, 1f), 2.4f);
                    break;
                case FormMove.ChainLightning:
                    attackTiles.Add(foi);
                    formRepeats = 2; // (three bolts, each where foi stands then)
                    formTimes = 0.6f;
                    break;
                case FormMove.Meteor:
                    Ring(foi, 1, true);
                    MoveFx.Meteor(At(foi), 1.35f, 1.6f);
                    formTimes = 1.6f;
                    break;
                case FormMove.CoreBurst:
                    Ring(monsterPos, 3, false);
                    MoveFx.Blast(At(monsterPos) + Vector3.up * 1f, new Color(0.4f, 0.95f, 1f), 3.5f, 1f);
                    formTimes = 1.4f;
                    break;
                case FormMove.IronBody:
                    IronUp();
                    break;
                case FormMove.Mirror:
                    formMirrorUntil = Time.time + 3f;
                    if (monster != null) MoveFx.Shell(monster.transform, new Color(0.65f, 0.95f, 1f), 3f, 0.35f);
                    break;
                default:
                    return false;
            }
            Shout(Form == FormMove.Copy ? FormMove.Copy : m);
            specialKind = FormKind + (int)m;
            monster.Stomp();
            AudioManager.PlaySfx(Sfx.Blocked, 1f, 0.5f);
            if (attackTiles.Count == 0 && m != FormMove.Patch) { specialKind = -1; return true; } // (a shell or shield: no blow to wait for)
            attackWind = 0f;
            return true;
        }

        /// <summary>A form's big attack lands (the tiles' blow is the shared one, at its share): its after-effects.</summary>
        private void LandFormMove(FormMove m)
        {
            bool onFoi = attackTiles.Contains(robot.Position);
            switch (m)
            {
                case FormMove.Hook:
                    if (onFoi) { var beside = FreeBeside(monsterPos); if (beside.HasValue) robot.Spawn(grid, beside.Value); } // (dragged in)
                    break;
                case FormMove.Shock:
                    MoveFx.Bolt(At(monsterPos) + Vector3.up * 1.6f, robot.transform.position + Vector3.up * 0.8f, new Color(0.55f, 0.85f, 1f), 0.3f, 0.4f);
                    if (onFoi) FoiHeld?.Invoke(0.6f);
                    break;
                case FormMove.IceWall:
                    MoveFx.IceSpikes(At(monsterPos), robot.transform.position, 1.6f);
                    if (onFoi) { FoiHeld?.Invoke(0.8f); if (robot.Visual != null) MoveFx.IceBlock(robot.Visual, 0.8f); }
                    break;
                case FormMove.Patch:
                    monsterHp = Mathf.Min(MonsterHpTotal, monsterHp + Mathf.RoundToInt(MonsterHpTotal * 0.15f));
                    monster.Hit(monsterHp);
                    break;
                case FormMove.Quake:
                    if (onFoi) FoiSlowed?.Invoke(1f);
                    break;
                case FormMove.ChainLightning:
                    foreach (var t in attackTiles) MoveFx.Bolt(At(t) + new Vector3(0.6f, 9f, 0.4f), At(t), new Color(0.6f, 0.85f, 1f), 0.35f, 0.35f);
                    break;
                case FormMove.CoreBurst:
                    monsterHp = Mathf.Min(MonsterHpTotal, monsterHp + Mathf.RoundToInt(MonsterHpTotal * 0.1f));
                    monster.Hit(monsterHp);
                    break;
            }
            if (formRepeats > 0)
            {
                // the next spin or bolt at once: a step at foi (the whirlwind), foi's tile again (the chain)
                formRepeats--;
                attackTiles.Clear();
                if (m == FormMove.Whirlwind) { StepToward(robot.Position); Ring(monsterPos, 1, false); }
                else attackTiles.Add(robot.Position);
                specialKind = FormKind + (int)m;
                attackWind = 0.55f; // (the next comes quicker)
            }
        }

        // ---------- The tiles ----------

        private void Ring(GridPos c, int r, bool withCentre)
        {
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                {
                    var t = c + new GridPos(x, y);
                    if ((withCentre || x != 0 || y != 0) && t != monsterPos && grid.IsStandable(t) && !grid.IsSafe(t) && !attackTiles.Contains(t)) attackTiles.Add(t);
                }
        }

        private void Line(GridPos from, GridPos to, int length)
        {
            var d = new Vector2(to.x - from.x, to.y - from.y);
            if (d.sqrMagnitude < 0.5f) return;
            d.Normalize();
            for (int i = 1; i <= length; i++)
            {
                var p = new GridPos(from.x + Mathf.RoundToInt(d.x * i), from.y + Mathf.RoundToInt(d.y * i));
                if (!grid.IsStandable(p) || grid.IsSafe(p)) break;
                if (!attackTiles.Contains(p)) attackTiles.Add(p);
            }
        }

        private GridPos? FreeBeside(GridPos c)
        {
            foreach (var d in DirectionExtensions.All)
            {
                var p = c + d.ToOffset();
                if (grid.IsStandable(p) && !grid.IsOccupied(p) && !grid.IsGap(p) && !grid.IsSafe(p) && p != robot.Position) return p;
            }
            return null;
        }

        /// <summary>A jump two tiles straight away from foi (the backflip), when there is room.</summary>
        private void TryLeapAway()
        {
            int dx = Math.Sign(monsterPos.x - robot.Position.x), dy = Math.Sign(monsterPos.y - robot.Position.y);
            if (dx == 0 && dy == 0) dy = 1;
            for (int k = 2; k >= 1; k--)
            {
                var p = new GridPos(monsterPos.x + dx * k, monsterPos.y + dy * k);
                if (!grid.IsStandable(p) || grid.IsOccupied(p) || grid.IsGap(p) || grid.IsSafe(p)) continue;
                grid.SetOccupied(monsterPos, false);
                monsterPos = p;
                grid.SetOccupied(monsterPos, true);
                leapFrom = monster.transform.position;
                leapTo = At(monsterPos);
                leapTo.y = leapFrom.y;
                leapT = 0f;
                return;
            }
        }

        private void StepToward(GridPos target)
        {
            var p = new GridPos(monsterPos.x + Math.Sign(target.x - monsterPos.x), monsterPos.y + Math.Sign(target.y - monsterPos.y));
            if (p == target || !grid.IsStandable(p) || grid.IsOccupied(p) || grid.IsGap(p) || grid.IsSafe(p)) return;
            grid.SetOccupied(monsterPos, false);
            monsterPos = p;
            grid.SetOccupied(monsterPos, true);
            if (monster != null) monster.transform.position = At(monsterPos);
        }
    }
}
