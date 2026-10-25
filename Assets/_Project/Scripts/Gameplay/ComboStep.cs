using System;

namespace SquashBot.Gameplay
{
    /// <summary>One step of a training challenge: what it asks and how many times.</summary>
    [Serializable]
    public class ComboStep
    {
        public ComboStepKind kind;
        public int count = 1;

        public ComboStep() { }
        public ComboStep(ComboStepKind kind, int count = 1) { this.kind = kind; this.count = count; }
    }
}
