namespace SquashBot.Combat
{
    /// <summary>
    /// How a blow met a defence: it landed (Hit), was blocked (a fifth through), blocked but broke the shield (Broke),
    /// guarded perfectly (nothing through, the striker staggers) or dodged (nothing at all).
    /// </summary>
    public enum DefenseOutcome { Hit, Blocked, Broke, Perfect, Dodged }
}
