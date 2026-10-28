namespace SquashBot.Combat
{
    /// <summary>
    /// What became of a blow (Docs/ARAYUZ_HATA_VE_TEMIZLIK.md, section 3.2), each shown its own way over the rival's head
    /// so the player always knows why a blow did or did not hurt.
    /// </summary>
    public enum HitOutcome
    {
        /// <summary>It hurt (the damage number).</summary>
        Hit,
        /// <summary>The rival's shield took it (a share went through: the small grey number).</summary>
        Blocked,
        /// <summary>The rival's perfect guard: foi reels back.</summary>
        Parried,
        /// <summary>The rival's dodge took it past.</summary>
        Dodged,
        /// <summary>Out of reach, or the rival jumped or crouched under it.</summary>
        Missed,
        /// <summary>The rival untouchable: lying, getting up, its backflip or iron body.</summary>
        Immune,
        /// <summary>The rival's mirror sent it back at foi.</summary>
        Reflected,
        /// <summary>The live match's server did not count it (no words: its effect only fades).</summary>
        Rejected,
    }
}
