using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Follows a training challenge step by step from what happens in the training room: each step's count goes up with
    /// its own deed (a chain counts the combo reached, blocks in a row start again when a blow lands, blocks without
    /// breaking when the shield breaks, a perfect guard answered by a blow within a moment), and the next step starts
    /// when one is done.
    /// </summary>
    public class ComboChallengeRunner
    {
        public ComboChallengeData Challenge { get; private set; }
        public int Step { get; private set; }
        public int Progress { get; private set; }
        public bool Finished => Challenge != null && Step >= Challenge.steps.Count;
        private float perfectAt = -9f;
        private const float CounterWindow = 1.5f;

        public void Begin(ComboChallengeData c)
        {
            Challenge = c;
            Step = 0;
            Progress = 0;
            perfectAt = -9f;
        }

        public void Stop() => Challenge = null;

        /// <summary>Something happened (<paramref name="value"/>: the combo reached, for a blow). True when a step was just done.</summary>
        public bool Note(TrainingEvent e, int value = 0)
        {
            if (Challenge == null || Finished) return false;
            var s = Challenge.steps[Step];
            switch (s.kind)
            {
                case ComboStepKind.Chain: if (e == TrainingEvent.Hit) Progress = Mathf.Max(Progress, value); break;
                case ComboStepKind.PowerHit: if (e == TrainingEvent.PowerHit) Progress++; break;
                case ComboStepKind.Knockdown: if (e == TrainingEvent.Knockdown) Progress++; break;
                case ComboStepKind.AirHit: if (e == TrainingEvent.AirHit) Progress++; break;
                case ComboStepKind.PowerThroughBlock: if (e == TrainingEvent.PowerThroughBlock) Progress++; break;
                case ComboStepKind.BlockInRow: if (e == TrainingEvent.Blocked || e == TrainingEvent.Perfect) Progress++; else if (e == TrainingEvent.Hurt || e == TrainingEvent.ShieldBroke) Progress = 0; break;
                case ComboStepKind.BlockNoBreak: if (e == TrainingEvent.Blocked || e == TrainingEvent.Perfect) Progress++; else if (e == TrainingEvent.ShieldBroke) Progress = 0; break;
                case ComboStepKind.Perfects: if (e == TrainingEvent.Perfect) Progress++; break;
                case ComboStepKind.PerfectCounter:
                    if (e == TrainingEvent.Perfect) perfectAt = Time.time;
                    else if (e == TrainingEvent.Hit && Time.time - perfectAt <= CounterWindow) Progress++;
                    break;
                case ComboStepKind.DodgeUlti: if (e == TrainingEvent.DodgeUlti) Progress++; break;
            }
            if (Progress < s.count) return false;
            Step++;
            Progress = 0;
            return true;
        }
    }
}
