using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>The special moves (one part of the power bar).</summary>
    public enum SpecialKind { Fly, Backflip, Hook, Shock, IceWall, Patch, SkyStrike, Teleport }

    /// <summary>The ultimates (the whole bar).</summary>
    public enum UltiKind { Cinematic, Quake, Whirlwind, IronBody, Mirror, Copy, ChainLightning, Meteor, CoreBurst }

    /// <summary>The passives (no cost, always on; the slot opens at duel level 5).</summary>
    public enum PassiveKind { None, ComboMaster, HeartMaster, Piercer, FastCharge, Sturdy, Rage, Vampire, ShieldMaster, MasterWarrior }

    /// <summary>
    /// The move set (the scenario's §3.6): three slots — a special, an ultimate and a passive. Moves give choices, not
    /// raw power. FLY and the CINEMATIC ATTACK are there from the start; every other move is a world monster's own,
    /// won when that world's monster (level 10 of the world) is beaten; and at duel levels 10, 20, 30, 40 and 50 the first
    /// move not yet won is given (so a duel-only player is not left behind). The rival's set is shown at the match's start.
    /// </summary>
    public static class MoveSet
    {
        // The world (1-based) whose monster gives each move (0: from the start).
        private static readonly int[] SpecialWorld = { 0, 2, 4, 9, 12, 15, 18, 22 };
        private static readonly int[] UltiWorld = { 0, 6, 7, 10, 14, 16, 20, 23, 25 };
        private static readonly int[] PassiveWorld = { 0, 3, 5, 8, 11, 13, 17, 19, 21, 24 };

        public const int PassiveLevel = 5;

        public static int WorldOf(SpecialKind m) => SpecialWorld[(int)m];
        public static int WorldOf(UltiKind m) => UltiWorld[(int)m];
        public static int WorldOf(PassiveKind m) => PassiveWorld[(int)m];

        /// <summary>The world (1-based) whose monster has been beaten: its level 10 cleared.</summary>
        public static bool WorldBeaten(int world) => world <= 0 || SaveData.UnlockedLevel > world * LevelCatalog.LevelsPerWorld - 1 || SaveData.TestMode;

        private static bool Gifted(string id) => Prefs.GetInt("sb_move_gift_" + id, 0) == 1;

        public static bool Owned(SpecialKind m) => WorldBeaten(WorldOf(m)) || Gifted("s" + (int)m);
        public static bool Owned(UltiKind m) => WorldBeaten(WorldOf(m)) || Gifted("u" + (int)m);
        public static bool Owned(PassiveKind m) => m == PassiveKind.None || WorldBeaten(WorldOf(m)) || Gifted("p" + (int)m);

        public static bool PassiveOpen => DuelLevel.Level >= PassiveLevel;

        public static SpecialKind Special
        {
            get { var m = (SpecialKind)Mathf.Clamp(Prefs.GetInt("sb_move_s", 0), 0, 7); return Owned(m) ? m : SpecialKind.Fly; }
            set { if (Owned(value)) { Prefs.SetInt("sb_move_s", (int)value); Prefs.Save(); } }
        }

        public static UltiKind Ulti
        {
            get { var m = (UltiKind)Mathf.Clamp(Prefs.GetInt("sb_move_u", 0), 0, 8); return Owned(m) ? m : UltiKind.Cinematic; }
            set { if (Owned(value)) { Prefs.SetInt("sb_move_u", (int)value); Prefs.Save(); } }
        }

        public static PassiveKind Passive
        {
            get { var m = (PassiveKind)Mathf.Clamp(Prefs.GetInt("sb_move_p", 0), 0, 9); return PassiveOpen && Owned(m) ? m : PassiveKind.None; }
            set { if (Owned(value)) { Prefs.SetInt("sb_move_p", (int)value); Prefs.Save(); } }
        }

        /// <summary>The set as the server and the rival's game read it: "special,ulti,passive".</summary>
        public static string Code => (int)Special + "," + (int)Ulti + "," + (int)Passive;

        public static (SpecialKind s, UltiKind u, PassiveKind p) Parse(string code)
        {
            var parts = (code ?? "").Split(',');
            int V(int i, int max) => parts.Length == 3 && int.TryParse(parts[i], out int v) ? Mathf.Clamp(v, 0, max) : 0;
            return ((SpecialKind)V(0, 7), (UltiKind)V(1, 8), (PassiveKind)V(2, 9));
        }

        /// <summary>The move a world's monster gives (its key, for the "new move" card), or null.</summary>
        public static string KeyOfWorld(int world)
        {
            for (int i = 0; i < SpecialWorld.Length; i++) if (SpecialWorld[i] == world && world > 0) return "move.s." + (SpecialKind)i;
            for (int i = 0; i < UltiWorld.Length; i++) if (UltiWorld[i] == world && world > 0) return "move.u." + (UltiKind)i;
            for (int i = 0; i < PassiveWorld.Length; i++) if (PassiveWorld[i] == world && world > 0) return "move.p." + (PassiveKind)i;
            return null;
        }

        /// <summary>The duel level's gift (levels 10..50): the first move not yet won, given now. Returns its key or null.</summary>
        public static string GiftNext()
        {
            // in the order the worlds give them
            var all = new List<(int world, string id, string key)>();
            for (int i = 1; i < SpecialWorld.Length; i++) all.Add((SpecialWorld[i], "s" + i, "move.s." + (SpecialKind)i));
            for (int i = 1; i < UltiWorld.Length; i++) all.Add((UltiWorld[i], "u" + i, "move.u." + (UltiKind)i));
            for (int i = 1; i < PassiveWorld.Length; i++) all.Add((PassiveWorld[i], "p" + i, "move.p." + (PassiveKind)i));
            all.Sort((a, b) => a.world.CompareTo(b.world));
            foreach (var m in all)
            {
                if (WorldBeaten(m.world) || Gifted(m.id)) continue;
                Prefs.SetInt("sb_move_gift_" + m.id, 1);
                Prefs.Save();
                return m.key;
            }
            return null;
        }

        /// <summary>A computer rival's set, fitting its personality (§5.1: the runner a backflip and the whirlwind, the
        /// defender the ice wall, the mirror and the shield master …), from moves the player may not have seen yet.</summary>
        public static (SpecialKind s, UltiKind u, PassiveKind p) ForBot(Persona p, int seed, int duelLevel)
        {
            var rng = new System.Random(seed ^ 0x3c6ef372);
            switch (p)
            {
                case Persona.Aggressive: return (rng.Next(2) == 0 ? SpecialKind.Hook : SpecialKind.SkyStrike, UltiKind.Cinematic, duelLevel >= PassiveLevel ? PassiveKind.Rage : PassiveKind.None);
                case Persona.Defensive: return (SpecialKind.IceWall, UltiKind.Mirror, duelLevel >= PassiveLevel ? PassiveKind.ShieldMaster : PassiveKind.None);
                case Persona.Combo: return (SpecialKind.Fly, rng.Next(2) == 0 ? UltiKind.ChainLightning : UltiKind.Cinematic, duelLevel >= PassiveLevel ? PassiveKind.ComboMaster : PassiveKind.None);
                case Persona.Runner: return (SpecialKind.Backflip, UltiKind.Whirlwind, duelLevel >= PassiveLevel ? PassiveKind.FastCharge : PassiveKind.None);
                case Persona.Ulti: return (SpecialKind.Patch, rng.Next(2) == 0 ? UltiKind.Meteor : UltiKind.Quake, duelLevel >= PassiveLevel ? PassiveKind.MasterWarrior : PassiveKind.None);
                default: return ((SpecialKind)rng.Next(8), (UltiKind)rng.Next(9), duelLevel >= PassiveLevel ? (PassiveKind)(1 + rng.Next(9)) : PassiveKind.None);
            }
        }
    }
}
