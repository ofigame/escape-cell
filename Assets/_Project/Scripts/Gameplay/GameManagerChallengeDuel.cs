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
            public Color wall, fxColour;
        }

        private static readonly DuelStage[] DuelStages =
        {
            new DuelStage { key = "duel.stage.0", world = 6, shape = BattleArenas.Shape.Square, colosseum = true, wall = new Color(0.86f, 0.72f, 0.5f), fxColour = new Color(1f, 0.6f, 0.2f) },             // the Colosseum
            new DuelStage { key = "duel.stage.1", world = 13, shape = BattleArenas.Shape.Hexagon, fx = 2, wall = new Color(0.72f, 0.9f, 1f), fxColour = new Color(0.55f, 0.9f, 1f) },                     // the ice arena
            new DuelStage { key = "duel.stage.2", world = 0, shape = BattleArenas.Shape.Octagon, fx = 1, wall = new Color(0.36f, 0.2f, 0.18f), fxColour = new Color(1f, 0.45f, 0.1f) },                   // the lava pit
            new DuelStage { key = "duel.stage.3", world = 23, shape = BattleArenas.Shape.Round, fx = 3, wall = new Color(0.3f, 0.32f, 0.42f), fxColour = new Color(0.4f, 0.9f, 1f) },                     // the space platform
            new DuelStage { key = "duel.stage.4", world = 2, shape = BattleArenas.Shape.Diamond, fx = 0, wall = new Color(0.5f, 0.56f, 0.4f), fxColour = new Color(0.6f, 1f, 0.4f) },                     // the forest temple
            new DuelStage { key = "duel.stage.5", world = 5, shape = BattleArenas.Shape.Cross, fx = 1, wall = new Color(0.86f, 0.66f, 0.42f), fxColour = new Color(1f, 0.7f, 0.25f) },                    // the desert city
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
                GridPos south = new GridPos(BoardW / 2, BoardH / 2 - 4), north = new GridPos(BoardW / 2, BoardH / 2 + 4);
                return chLive && liveSide == 1 ? (north, south) : (south, north);
            }
        }

        /// <summary>The share of a foi's health one blow of this weapon takes: 1% per point of damage, less its armour.</summary>
        private static float DuelBlow(WeaponDef w, int weaponLevel, float armor) =>
            0.01f * Armory.Damage(w, Mathf.Max(1, weaponLevel)) * (1f - Mathf.Clamp01(armor));

        /// <summary>The Duel's board: the stage, the two foi on their marks with their squads, the clock.</summary>
        private void BuildDuelBoard(int stage)
        {
            duelStage = Mathf.Clamp(stage, 0, DuelStages.Length - 1);
            BuildCourtyard();
            chDuelMe = chDuelThem = 0;
            chDuelLeft = chDuelMinutes * 60f;
            duelHearts = rivalHearts = DuelHearts;
            duelShields = rivalShields = DuelShields;
            rivalShieldUntil = 0f;
            rivalPowerUpAt = Time.time + 20f;
            if (robot.Visual != null) robot.Visual.gameObject.SetActive(true);
            chUi.ShowBattle();
            ui.SetHudTitle(Loc.T("ch.mode.duel") + " · " + Loc.T(Stage.key));
            PlaceDuelists(first: true);
            RefreshSquadBar();
            hunt.Freeze(); // nobody moves while the briefing is read
            chPhase = ChPhase.Lobby; // the briefing first
            cameraRig.StartCoroutine(ChallengeBriefing("duel", 3, () => { if (LiveActive) LiveWaitFor(LiveWait.Start); else BeginDuel(); }));
            AudioManager.PlaySfx(Sfx.Win, 0.6f, 0.9f);
        }

        private void BeginDuel()
        {
            if (!challengeRun) return;
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
            int hp = LiveActive ? 100 : Mathf.RoundToInt(100f / Mathf.Max(0.5f, 1f - rival.armor));
            // Its blows weigh what the player's own would (its flurries of quick blows make up for its slower swings).
            const float rate = 1f;
            hunt.AddChallengeFoe(theirs, hp, DuelBlow(w, rival.weaponLevel, Shop.ArmorShare) * (LiveActive ? 1f : rate), look, rivalHeld, 0);
            if (myTag == null) myTag = SquashBot.Visual.NameTag.Create(robot.transform, Challenge.PlayerName, ArmyBattleColours.Mine);
            if (theirTag != null) Destroy(theirTag.gameObject);
            theirTag = SquashBot.Visual.NameTag.Create(look, rival.name, ArmyBattleColours.Theirs);
            int back = north ? 1 : -1; // behind each foi, away from the other
            if (first)
            {
                hunt.ClearSquads();
                hunt.ResetRemoteSquad();
                for (int g = 0; g < Army.Groups; g++)
                {
                    int n = SoldierPool.ForDuel(g);
                    for (int s = 0; s < n; s++) hunt.AddSquadSoldier(true, g * SoldierPool.PerDuel + s, mine + new GridPos(g - 2, back * (2 + s)), Army.Power(g));
                    if (LiveActive) continue; // the live rival's soldiers come with its state
                    for (int s = 0; s < rival.squad[g]; s++) hunt.AddSquadSoldier(false, g * SoldierPool.PerDuel + s, theirs + new GridPos(g - 2, -back * (2 + s)), rival.Power(g));
                }
            }
            else { hunt.RegroupSquads(); hunt.DelayBotOrders(6f + Random.value * 6f); }
            RefreshSquadBar();
        }

        /// <summary>A knock-down: the one left standing scores, both start again from the middle.</summary>
        private void DuelKnockdown(bool iWon)
        {
            if (!challengeRun || chPhase != ChPhase.Duel) return;
            if (iWon) chDuelMe++; else chDuelThem++;
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
            if (iWon) hunt.RemoveChallengeFoe(); else { health = 0f; RefreshHealthBar(); robot.Squash(); }
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
            hunt.Resume();
            chPhase = ChPhase.Duel;
            duelPan = robot.transform.position - boardCentre; duelPan.y = 0f;
            chUi.SetStatus(Loc.T("ch.duel"));
            RefreshDuelClock();
        }

        private System.Collections.IEnumerator AfterKnockdown()
        {
            yield return new WaitForSeconds(1.8f);
            while (challengeRun && State == GameState.Paused) yield return null;
            if (!challengeRun || chPhase != ChPhase.Break || State != GameState.Playing) yield break;
            if (chDuelLeft <= 0f) { ChallengeEnd(); yield break; }
            PlaceDuelists(first: false);
            hunt.Resume();
            chPhase = ChPhase.Duel;
            chUi.SetStatus(Loc.T("ch.duel"));
        }

        /// <summary>Each frame of the duel: the clock (it only runs while the two fight), a knock-down, time up.</summary>
        private void UpdateDuel(float dt)
        {
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

        /// <summary>A group's tag tapped: sent at the rival foi, or (again) back to guard foi.</summary>
        private void ToggleSquad(int group)
        {
            if (!challengeRun || hunt.SquadAlive(true, group) == 0) return;
            bool attack = !hunt.SquadOrder(true, group);
            hunt.SetSquadOrder(true, group, attack);
            chUi.SetStatus(Loc.F(attack ? "ch.squadGo" : "ch.squadBack", Loc.T("army.kind." + Army.KindOf(group))));
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
        }
    }

    /// <summary>The two sides' colours in the challenge (the player's blue, the rival's red).</summary>
    public static class ArmyBattleColours
    {
        public static readonly Color Mine = new Color(0.3f, 0.62f, 1f), Theirs = new Color(1f, 0.32f, 0.3f);
    }
}
