using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>One thing that happened in a v2 tick: who, what, the result, the damage, where.</summary>
    public struct DuelEvent
    {
        public DuelEventKind kind;
        /// <summary>The one who acted (0 left, 1 right); the other is 1 - who.</summary>
        public int who;
        public MoveKind move;
        public HitResult result;
        public float damage;
        public int count;
        public ElementKind element;
        /// <summary>Where on the line (world units from the middle).</summary>
        public float at;
        public string why;
        public float time;
    }
}
