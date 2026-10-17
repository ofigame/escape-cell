using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The challenge's Duel: foi against the rival foi for 3, 5 or 10 minutes, each with its squad behind it — up to two
    /// soldiers of every group, taken from the army's pool (a soldier who falls is away for an hour; one lost in this duel
    /// stays lost for the rest of it). The six tags at the bottom send a group at the rival foi (tap again: back to
    /// guard); left alone the soldiers guard their foi. Each side has three hearts (a third of its health back) and three
    /// shields (three seconds, two blows) for the whole duel. Whoever puts the other foi down scores a point and both
    /// start again from their marks; when time is up the higher score wins (level: a draw). A foi's blow takes 1% of the
    /// other's health per point of its weapon's damage (armour takes some off). The duel is fought on one of six stages.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The duel's length in minutes (3, 5 or 10).</summary>
        private int chDuelMinutes = 3;

        /// <summary>This duel has squads (the player's choice; in a live match, the match's).</summary>
        private bool chDuelSquads = true;

        /// <summary>Knock-downs scored by each side, and the time left (seconds).</summary>
        private int chDuelMe, chDuelThem;
        private float chDuelLeft;
        private NameTag myTag, theirTag;

        /// <summary>Hearts and shields left this duel: the player's and the rival's.</summary>
        private int duelHearts, duelShields, rivalHearts, rivalShields;
        private float rivalShieldUntil, rivalPowerUpAt;
        private const int DuelHearts = 3, DuelShields = 3;
        private const float DuelHeartHeal = 0.3f, DuelShieldSeconds = 3f;
        private const int DuelShieldHits = 2;

        // ---------- The six stages ----------

        /// <summary>
        /// A duel stage: its name, the world whose look it takes, the floor's shape, what crowns its low walls (none,
        /// flames, ice, glow) and whether the colosseum's stands rise round it instead.
        /// </summary>
        private struct DuelStage
        {
            public string key;
            public int world;
            public BattleArenas.Shape shape;
            public int fx;
            public bool colosseum;
            /// <summary>The backdrop across the floor, facing the camera.</summary>
            public DuelStands.Theme theme;
            public Color wall, fxColour;
        }

        private static readonly DuelStage[] DuelStages =
        {
            new DuelStage { key = "duel.stage.0", world = 6, shape = BattleArenas.Shape.Square, fx = 1, theme = DuelStands.Theme.City, wall = new Color(0.86f, 0.72f, 0.5f), fxColour = new Color(1f, 0.6f, 0.2f) },             // the city stadium
            new DuelStage { key = "duel.stage.1", world = 13, shape = BattleArenas.Shape.Hexagon, fx = 2, theme = DuelStands.Theme.Glacier, wall = new Color(0.72f, 0.9f, 1f), fxColour = new Color(0.55f, 0.9f, 1f) },                     // the ice arena
            new DuelStage { key = "duel.stage.2", world = 0, shape = BattleArenas.Shape.Octagon, fx = 1, theme = DuelStands.Theme.Volcano, wall = new Color(0.36f, 0.2f, 0.18f), fxColour = new Color(1f, 0.45f, 0.1f) },                   // the lava pit
            new DuelStage { key = "duel.stage.3", world = 23, shape = BattleArenas.Shape.Round, fx = 3, theme = DuelStands.Theme.Space, wall = new Color(0.3f, 0.32f, 0.42f), fxColour = new Color(0.4f, 0.9f, 1f) },                     // the space platform
            new DuelStage { key = "duel.stage.4", world = 2, shape = BattleArenas.Shape.Diamond, fx = 0, theme = DuelStands.Theme.Forest, wall = new Color(0.5f, 0.56f, 0.4f), fxColour = new Color(0.6f, 1f, 0.4f) },                     // the forest temple
            new DuelStage { key = "duel.stage.5", world = 5, shape = BattleArenas.Shape.Octagon, fx = 1, theme = DuelStands.Theme.Desert, wall = new Color(0.86f, 0.66f, 0.42f), fxColour = new Color(1f, 0.7f, 0.25f) },                    // the desert city
        };

        /// <summary>The stage being fought on (an index into the six).</summary>
        private int duelStage;
        private DuelStage Stage => DuelStages[Mathf.Clamp(duelStage, 0, DuelStages.Length - 1)];
        public static int DuelStageCount => DuelStages.Length;

        /// <summary>The marks the two foi start from: either side of the middle, the player's to the south (left on screen).</summary>
        private (GridPos mine, GridPos theirs) DuelMarks
        {
            get
            {
                // A live match: the same arena on both screens — the server's side 0 to the south (left), side 1 to the north.
                GridPos south = new GridPos(BoardW / 2, BoardH / 2 - 3), north = new GridPos(BoardW / 2, BoardH / 2 + 3);
                return chLive && liveSide == 1 ? (north, south) : (south, north);
            }
        }

        /// <summary>The share of foi's health one blow of this weapon takes: its damage (health points), less foi's armour, over foi's health points.</summary>
        private static float DuelBlow(WeaponDef w, int weaponLevel, float armor) =>
            Armory.Damage(w, Mathf.Max(1, weaponLevel)) * (1f - Mathf.Clamp01(armor)) / Mathf.Max(1f, Challenge.FoiMaxHp);

        /// <summary>The Duel's board: the stage, the two foi on their marks with their squads, the clock.</summary>
        private void BuildDuelBoard(int stage)
        {
            duelStage = Mathf.Clamp(stage, 0, DuelStages.Length - 1);
            zoneRadius = 99f; zoneDrawn = -1; zoneHurtMe = zoneHurtFoe = 0f; zoneAnnounced = false;
            hunt.Unsafe = null;
            BuildCourtyard();
            chDuelMe = chDuelThem = 0;
            chDuelLeft = chDuelMinutes * 60f;
            duelHearts = rivalHearts = DuelHearts;
            duelShields = rivalShields = DuelShields;
            rivalShieldUntil = 0f;
            rivalPowerUpAt = Time.time + 20f;
            if (robot.Visual != null) robot.Visual.gameObject.SetActive(true);
            chUi.ShowBattle();
            BeginDuelSkills();
            ui.SetHudTitle(Loc.T("ch.mode.duel") + " · " + Loc.T(Stage.key));
            PlaceDuelists(first: true);
            RefreshSquadBar();
            hunt.Freeze(); // nobody moves while the briefing is read
            chPhase = ChPhase.Lobby; // the briefing first
            cameraRig.StartCoroutine(ChallengeBriefing("duel", 3, () =>
            {
                if (chPhase != ChPhase.Lobby) return; // the server started it meanwhile, or it is over (the rival left)
                if (LiveActive) LiveWaitFor(LiveWait.Start); else BeginDuel();
            }));
            AudioManager.PlaySfx(Sfx.Win, 0.6f, 0.9f);
        }

        private void BeginDuel()
        {
            if (!challengeRun || chPhase == ChPhase.Done || chPhase == ChPhase.Duel) return;
            hunt.DelayBotOrders(10f + Random.value * 8f);
            chPhase = ChPhase.Duel;
            duelPan = robot.transform.position - boardCentre; duelPan.y = 0f;
            hunt.Resume();
            chUi.SetStatus(Loc.T("ch.duel"));
            ui.ShowIntro(Loc.T("ch.duel"), Loc.F("ch.duelMinutes", chDuelMinutes));
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 0.8f);
            RefreshDuelClock();
        }

        /// <summary>
        /// Both foi on their marks, full health, a fresh rival foi (its look, weapon and health from its profile). The
        /// first time the squads come out of the pool; after a knock-down the ones left stand back in their places.
        /// </summary>
        private void PlaceDuelists(bool first)
        {
            hunt.RemoveChallengeFoe();
            health = 1f;
            RefreshHealthBar();
            var (mine, theirs) = DuelMarks;
            robot.Spawn(grid, mine);
            bool north = theirs.y < mine.y;
            robot.transform.rotation = Quaternion.identity;
            robot.Visual.rotation = Quaternion.Euler(0f, north ? 180f : 0f, 0f); // facing the rival across the middle
            var look = RivalLook();
            look.position = GridView.ToWorld(theirs) + Vector3.up * GridView.SurfaceY;
            look.rotation = Quaternion.Euler(0f, north ? 0f : 180f, 0f);
            hunt.FoeRemote = LiveActive; // a live rival: its own game moves it, the server keeps its health
            var w = Armory.Get(rival.weapon);
            hunt.FoeBoldness = rival.boldness;
            hunt.ChallengeBattle = true;
            hunt.FoeSoldierDamage = Armory.Damage(w, Mathf.Max(1, rival.weaponLevel));
            // 100 health points behind its armour; foi's blows take its weapon's damage in points (1% each).
            // Its own health points (live: a share of 100, the server keeps the points), its armour taking its share off foi's blows.
            // Both sides' health times the match's level (live: the server does the same).
            int times = Challenge.DuelHpTimes(Mathf.Max(Challenge.Rank, rival.Rank));
            hunt.FoiHp = Challenge.FoiMaxHp * times; // the rival's soldiers' blows off foi's (bigger) health
            int hp = LiveActive ? 100 : Mathf.RoundToInt(Mathf.Max(100, rival.foiHp) / Mathf.Max(0.5f, 1f - rival.armor));
            if (!LiveActive) hp *= times;
            // Its blows weigh what the player's own would (its flurries of quick blows make up for its slower swings).
            const float rate = 1f;
            hunt.AddChallengeFoe(theirs, hp, DuelBlow(w, rival.weaponLevel, Shop.ArmorShare) * (LiveActive ? 1f : rate) / times, look, rivalHeld, 0);
            if (myTag == null) myTag = SquashBot.Visual.NameTag.Create(robot.transform, Challenge.PlayerName, ArmyBattleColours.Mine);
            if (theirTag != null) Destroy(theirTag.gameObject);
            theirTag = SquashBot.Visual.NameTag.Create(look, rival.name, ArmyBattleColours.Theirs);
            int back = north ? 1 : -1; // behind each foi, away from the other
            if (first)
            {
                hunt.ClearSquads();
                hunt.ResetRemoteSquad();
                int myForm = Challenge.DuelFormation, theirForm = Mathf.Abs(rival.name.GetHashCode()) % 3;
                for (int g = 0; g < Army.Groups && chDuelSquads; g++) // foi against foi: no squads at all
                {
                    int n = SoldierPool.ForDuel(g);
                    for (int s = 0; s < n; s++) hunt.AddSquadSoldier(true, g * SoldierPool.PerDuel + s, mine + FormationSpot(myForm, g, s, back), Army.StatsOf(g));
                    if (LiveActive) continue; // the live rival's soldiers come with its state
                    for (int s = 0; s < rival.squad[g]; s++) hunt.AddSquadSoldier(false, g * SoldierPool.PerDuel + s, theirs + FormationSpot(theirForm, g, s, -back), Army.StatsFromTotal(g, rival.points[g]));
                }
                // Every group charges from the start (a tag tapped calls its group back to guard, tapped again: back in).
                // (A guarding squad stays by its foi until a tag sends it in.)
                for (int g = 0; g < Army.Groups; g++) { hunt.SetSquadOrder(true, g, myForm != 1); hunt.SetSquadOrder(false, g, theirForm != 1); }
            }
            // After a knock-down the soldiers left line up behind their foi again (no new ones and no healing until the duel is over).
            else hunt.RegroupSquads();

            RefreshSquadBar();
        }

        /// <summary>
        /// Where soldier <paramref name="s"/> of group <paramref name="g"/> starts, from its foi (<paramref name="back"/>: the
        /// way away from the rival): attack, a row in front; guard, a ring round foi; flanks, two wings at its sides.
        /// </summary>
        private static GridPos FormationSpot(int form, int g, int s, int back)
        {
            int i = g * 2 + s; // 0..11
            switch (form)
            {
                case 1:
                {
                    var ring = new[] { new GridPos(-1, 0), new GridPos(1, 0), new GridPos(0, back), new GridPos(0, -back), new GridPos(-1, back), new GridPos(1, back),
                                       new GridPos(-1, -back), new GridPos(1, -back), new GridPos(-2, 0), new GridPos(2, 0), new GridPos(-2, back), new GridPos(2, back) };
                    return ring[i % ring.Length];
                }
                case 2:
                {
                    int side = i % 2 == 0 ? -1 : 1, k = i / 2;
                    return new GridPos(side * (3 + k % 3), -back * (k / 3));
                }
                default:
                    return new GridPos((g - 3) * 2 + s, -back);
            }
        }

        /// <summary>A knock-down: the one left standing scores, both start again from the middle.</summary>
        private void DuelKnockdown(bool iWon)
        {
            if (!challengeRun || chPhase != ChPhase.Duel) return;
            if (iWon) chDuelMe++; else chDuelThem++;
            if (iWon) OfferFinish(lastFoeAt);
            cameraRig.StartCoroutine(RivalEmote(!iWon));
            chPhase = ChPhase.Break;
            hunt.Freeze();
            RefreshDuelClock();
            chUi.SetStatus(Loc.T(iWon ? "ch.koYou" : "ch.koThem"));
            if (colosseum != null) colosseum.Cheer(iWon ? 0 : 1, 0.9f);
            AudioManager.PlaySfx(iWon ? Sfx.Win : Sfx.Warning, 0.8f, 1f);
            if (iWon) fx.Burst(robot.transform.position + Vector3.up * 0.6f, Palette.Coin, Palette.CoinGlow, 24, 5f);
            cameraRig.StartCoroutine(AfterKnockdown());
        }

        /// <summary>A live knock-down, as the server tells it: its score; both start again when it says.</summary>
        private void LiveKnockdown(bool iWon, int mine, int theirs)
        {
            if (!challengeRun || (chPhase != ChPhase.Duel && chPhase != ChPhase.Wait)) return;
            chDuelMe = mine;
            chDuelThem = theirs;
            chPhase = ChPhase.Break;
            hunt.Freeze();
            if (iWon) { OfferFinish(hunt.ChallengeFoeAt ?? lastFoeAt); hunt.RemoveChallengeFoe(); } else { health = 0f; RefreshHealthBar(); robot.Squash(); }
            RefreshDuelClock();
            chUi.SetStatus(Loc.T(iWon ? "ch.koYou" : "ch.koThem"));
            if (colosseum != null) colosseum.Cheer(iWon ? 0 : 1, 0.9f);
            AudioManager.PlaySfx(iWon ? Sfx.Win : Sfx.Warning, 0.8f, 1f);
            if (iWon) fx.Burst(robot.transform.position + Vector3.up * 0.6f, Palette.Coin, Palette.CoinGlow, 24, 5f);
        }

        /// <summary>The server's word to start (again): both on their marks, the clock as it says.</summary>
        private void LiveDuelGo(float secs)
        {
            if (!challengeRun) return;
            chDuelLeft = secs;
            PlaceDuelists(first: false);
            duelPan = robot.transform.position - boardCentre; duelPan.y = 0f;
            RefreshDuelClock();
            // 3, 2, 1: the server holds the blows and its clock for the same 2.4 s (Rules.DuelCountdown).
            cameraRig.StartCoroutine(CountdownThenGo());
        }

        /// <summary>3, 2, 1 over the two foi on their marks (everything still), then the duel goes on.</summary>
        private System.Collections.IEnumerator CountdownThenGo()
        {
            hunt.Freeze();
            chPhase = ChPhase.Break;
            for (int i = 3; i >= 1; i--)
            {
                if (!challengeRun || chPhase == ChPhase.Done) yield break;
                chUi.SetStatus(i.ToString());
                ui.Float(new Vector2(Screen.width * 0.5f, Screen.height * 0.6f), i.ToString(), Palette.UiGold, 220f);
                AudioManager.PlaySfx(Sfx.Click, 1f, 0.8f + 0.1f * (3 - i));
                yield return new WaitForSeconds(0.8f);
                while (challengeRun && State == GameState.Paused) yield return null;
            }
            if (!challengeRun || chPhase == ChPhase.Done || State != GameState.Playing) yield break;
            hunt.Resume();
            chPhase = ChPhase.Duel;
            chUi.SetStatus(Loc.T("ch.duel"));
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 1.1f);
        }

        private System.Collections.IEnumerator AfterKnockdown()
        {
            yield return new WaitForSeconds(1.2f);
            while (challengeRun && (Time.time < finishUntil || finishing)) yield return null; // the finish offered (or playing) first
            while (challengeRun && State == GameState.Paused) yield return null;
            if (!challengeRun || chPhase != ChPhase.Break || State != GameState.Playing) yield break;
            if (chDuelLeft <= 0f) { ChallengeEnd(); yield break; }
            PlaceDuelists(first: false);
            yield return CountdownThenGo();
        }

        /// <summary>Each frame of the duel: the clock (it only runs while the two fight), a knock-down, time up.</summary>
        // ---------- The closing ring ----------

        /// <summary>The safe ring's radius (tiles from the board's middle) and the last one drawn.</summary>
        private float zoneRadius = 99f;
        private int zoneDrawn = -1;
        private float zoneHurtMe, zoneHurtFoe;
        private bool zoneAnnounced;

        /// <summary>
        /// So nobody runs round the board for ever: from a quarter of the duel's time the safe ring closes in, down to a
        /// ring a few tiles across just before the end. The tiles outside it glow red, and whoever stands out there loses
        /// health (a live duel's health is the server's: there only the ring shows).
        /// </summary>
        private void UpdateDuelZone(float dt)
        {
            float total = Mathf.Max(60f, chDuelMinutes * 60f);
            float elapsed = total - chDuelLeft;
            float k = Mathf.InverseLerp(total * 0.25f, total * 0.9f, elapsed);
            if (k <= 0f) { zoneRadius = 99f; zoneAnnounced = false; hunt.Unsafe = null; return; }
            if (!zoneAnnounced)
            {
                zoneAnnounced = true;
                FloatAt(robot.transform.position + Vector3.up * 1.6f, Loc.T("duel.zone"), new Color(1f, 0.4f, 0.35f));
                AudioManager.PlaySfx(Sfx.Blocked, 0.9f, 0.6f);
            }
            zoneRadius = Mathf.Lerp(13.5f, 2.6f, k);
            var mid = new GridPos(BoardW / 2, BoardH / 2);
            // The rival (and every robot on the board) keeps inside the ring.
            hunt.SafeCentre = mid;
            if (hunt.Unsafe == null) hunt.Unsafe = p => Vector2.Distance(new Vector2(p.x, p.y), new Vector2(BoardW / 2, BoardH / 2)) > zoneRadius;
            int step = Mathf.RoundToInt(zoneRadius * 2f);
            if (step != zoneDrawn)
            {
                zoneDrawn = step;
                var red = new Color(1f, 0.3f, 0.25f);
                for (int y = 0; y < BoardH; y++)
                    for (int x = 0; x < BoardW; x++)
                    {
                        var p = new GridPos(x, y);
                        if (!grid.Exists(p)) continue;
                        bool outside = Vector2.Distance(new Vector2(x, y), new Vector2(mid.x, mid.y)) > zoneRadius;
                        gridView.SetTint(p, outside ? red : (Color?)null);
                    }
            }
            if (LiveActive) return;
            // Out in the red: foi's health drains (in small knocks, so it shows), and the rival's too.
            if (Vector2.Distance(new Vector2(robot.Position.x, robot.Position.y), new Vector2(mid.x, mid.y)) > zoneRadius)
            {
                zoneHurtMe += dt * 0.07f;
                if (zoneHurtMe >= 0.035f)
                {
                    hitShareOverride = zoneHurtMe;
                    moveHit = true;
                    OnBlockImpact(robot.Position);
                    moveHit = false;
                    hitShareOverride = -1f;
                    zoneHurtMe = 0f;
                }
            }
            var foeTile = hunt.ChallengeFoeTile;
            if (foeTile.HasValue && Vector2.Distance(new Vector2(foeTile.Value.x, foeTile.Value.y), new Vector2(mid.x, mid.y)) > zoneRadius)
            {
                zoneHurtFoe += dt;
                if (zoneHurtFoe >= 0.6f) { zoneHurtFoe = 0f; HitFoe(0.35f); }
            }
        }

        private void UpdateDuel(float dt)
        {
            ui.SetRivalHealth(true, hunt.ChallengeFoeHealth); // the rival's bar at the top right, over its name
            UpdateDuelZone(dt);
            UpdateDuelSkills(dt);
            chDuelLeft = Mathf.Max(0f, chDuelLeft - dt);
            RefreshDuelClock();
            RefreshSquadBar();
            if (rivalShieldUntil > 0f && Time.time > rivalShieldUntil) { rivalShieldUntil = 0f; hunt.EndFoeShield(); }
            if (LiveActive) return; // the server keeps the score and the clock
            RivalPowerUps();
            if (hunt.ChallengeFoeDown) { DuelKnockdown(true); return; }
            if (chDuelLeft <= 0f) { chPhase = ChPhase.Break; hunt.Freeze(); ChallengeEnd(); }
        }

        /// <summary>The AI's hearts and shields: a heart when badly hurt, a shield now and then when foi presses it.</summary>
        private void RivalPowerUps()
        {
            if (Time.time < rivalPowerUpAt) return;
            float share = hunt.ChallengeFoeHealth;
            if (rivalHearts > 0 && share < 0.4f && Random.value < 0.6f)
            {
                rivalHearts--;
                hunt.HealFoe(DuelHeartHeal);
                chUi.SetStatus(Loc.F("ch.rivalHeart", rivalHearts));
                rivalPowerUpAt = Time.time + 8f;
                return;
            }
            if (rivalShields > 0 && share < 0.75f && hunt.ChallengeFoeAt is Vector3 at && (at - robot.transform.position).sqrMagnitude < 2.5f * 2.5f && Random.value < 0.35f)
            {
                rivalShields--;
                hunt.ShieldFoe(DuelShieldHits);
                rivalShieldUntil = Time.time + DuelShieldSeconds;
                rivalPowerUpAt = Time.time + 9f;
                return;
            }
            rivalPowerUpAt = Time.time + 1.5f;
        }

        // ---------- The player's hearts, shields and squad orders ----------

        /// <summary>A heart: a third of foi's health back (live: the server heals).</summary>
        private void UseDuelHeart()
        {
            if (chPhase != ChPhase.Duel || duelHearts <= 0 || health >= 0.999f) return;
            duelHearts--;
            if (LiveActive) LiveNet.Heal();
            else Heal(robot.Position);
            AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.6f);
            RefreshSquadBar();
        }

        /// <summary>A shield: three seconds, two blows (live: the server counts the blows too).</summary>
        private void UseDuelShield()
        {
            if (chPhase != ChPhase.Duel || duelShields <= 0 || robot.IsShielded) return;
            duelShields--;
            robot.GiveShield(DuelShieldSeconds, DuelShieldHits);
            if (LiveActive) LiveNet.Shield();
            AudioManager.PlaySfx(Sfx.Shield, 1f, 1.1f);
            RefreshSquadBar();
        }

        private void RefreshSquadBar()
        {
            if (chUi == null || !challengeRun) return;
            var left = hunt.SquadAliveByGroup(true);
            var orders = new bool[Army.Groups];
            for (int g = 0; g < Army.Groups; g++) orders[g] = hunt.SquadOrder(true, g);
            chUi.ShowSquadBar(left, orders, duelHearts, duelShields);
            int mine = 0, theirs = 0;
            foreach (int n in left) mine += n;
            foreach (int n in hunt.SquadAliveByGroup(false)) theirs += n;
            chUi.SetSides(Challenge.PlayerName, Challenge.Rank, mine, rival.name, rival.Rank, theirs);
        }

        private void RefreshDuelClock()
        {
            int s = Mathf.CeilToInt(chDuelLeft);
            chUi.SetClock(ScoreText() + "   " + s / 60 + ":" + (s % 60).ToString("00"));
        }

        /// <summary>The score as the board shows it (mine first).</summary>
        private string ScoreText() => chDuelMe + " - " + chDuelThem;

        private int ScoreMine => chDuelMe;
        private int ScoreTheirs => chDuelThem;

        private void ClearDuel()
        {
            if (myTag != null) Destroy(myTag.gameObject);
            if (theirTag != null) Destroy(theirTag.gameObject);
            myTag = theirTag = null;
            hunt.ClearSquads();
            EndDuelSkills();
        }
    }

    /// <summary>The two sides' colours in the challenge (the player's blue, the rival's red).</summary>
    public static class ArmyBattleColours
    {
        public static readonly Color Mine = new Color(0.3f, 0.62f, 1f), Theirs = new Color(1f, 0.32f, 0.3f);
    }
}
