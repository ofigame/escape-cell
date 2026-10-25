namespace SquashBot.Combat
{
    /// <summary>The marks along a swing (as an animation's events would be): wind-up, the trail on, contact, the trail
    /// off, and the moment the next blow may follow.</summary>
    public enum SwingPhase { WindupStart, TrailStart, Contact, TrailEnd, Recoverable }
}
