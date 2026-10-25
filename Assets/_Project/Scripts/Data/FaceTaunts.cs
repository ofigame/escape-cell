using System.Collections.Generic;
using SquashBot.Visual;

namespace SquashBot.Data
{
    /// <summary>
    /// The face taunts (the design's §7): the face makes one for a moment, a little balloon over the head — the tongue
    /// out and a raised brow from the start, a wink, a sly grin, surprise and a fake cry opened by the duel level. They go
    /// out as emotes numbered from <see cref="First"/> (after the written ones).
    /// </summary>
    public static class FaceTaunts
    {
        public const int First = 32;
        public static readonly FaceMoment[] All = { FaceMoment.TongueOut, FaceMoment.BrowRaise, FaceMoment.Wink, FaceMoment.SlyGrin, FaceMoment.Surprise, FaceMoment.FakeCry };
        /// <summary>The duel level each opens at (0: from the start).</summary>
        public static readonly int[] Levels = { 0, 0, 4, 8, 15, 22 };

        /// <summary>The face taunt a duel level opens (-1: none).</summary>
        public static int ForLevel(int level) { for (int i = 0; i < Levels.Length; i++) if (Levels[i] > 0 && Levels[i] == level) return i; return -1; }

        public static bool Owned(int i) => i >= 0 && i < All.Length && DuelLevel.Level >= Levels[i];

        /// <summary>The emote number a taunt goes out as (and back).</summary>
        public static int Emote(int i) => First + i;
        public static bool IsTaunt(int emote) => emote >= First && emote < First + All.Length;
        public static FaceMoment Of(int emote) => All[emote - First];

        /// <summary>Every emote the player can pick: the face taunts owned first, then the written ones.</summary>
        public static List<int> Wheel
        {
            get
            {
                var list = new List<int>();
                for (int i = 0; i < All.Length; i++) if (Owned(i)) list.Add(Emote(i));
                list.AddRange(Emotes.All);
                return list;
            }
        }
    }
}
