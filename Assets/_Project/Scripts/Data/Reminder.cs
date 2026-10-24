using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace SquashBot.Data
{
    /// <summary>
    /// The phone's own notes (the scenario's §9.6), set again each time the game goes to the background, two a day at
    /// most: the day's first win waiting (19:00, on a day with no duel won), energy full again, the weekend cup (Friday
    /// 18:00) and the season's last two days (with the points to the next rank). iOS asks the player the first time;
    /// Android shows them from its own alarms (no Google services needed: Huawei too).
    /// </summary>
    public static class Reminder
    {
        private const int Slots = 4;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void EscapeCell_ScheduleNote(int slot, string title, string body, double seconds);
#endif

        private static void Set(int slot, DateTime? at, string title, string body)
        {
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                EscapeCell_ScheduleNote(slot, title, body, at.HasValue ? (at.Value - DateTime.Now).TotalSeconds : 0);
#elif UNITY_ANDROID && !UNITY_EDITOR
                long millis = at.HasValue ? new DateTimeOffset(at.Value).ToUnixTimeMilliseconds() : 0;
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var reminder = new AndroidJavaClass("com.ofigame.foi.FoiReminder"))
                    reminder.CallStatic("scheduleSlot", activity, slot, title, body, millis);
#endif
            }
            catch (Exception e) { Debug.LogWarning("[Reminder] " + e.Message); }
        }

        public static void Schedule()
        {
            var now = DateTime.Now;
            var notes = new List<(int slot, DateTime at, string title, string body)>();
            // 0: the day's first win (today 19:00 if still to come and not won today, else tomorrow's)
            var evening = now.Date.AddHours(19);
            if (now > evening.AddMinutes(-30) || !DuelDay.FirstWinWaiting) evening = evening.AddDays(1);
            notes.Add((0, evening, Loc.T("reminder.title"), Loc.T("note.firstWin")));
            // 1: energy full again
            if (!Lives.IsFull)
            {
                var full = now + Lives.UntilNext + TimeSpan.FromMinutes(Lives.RegenMinutes * Mathf.Max(0, Lives.Max - Lives.Count - 1));
                notes.Add((1, full, Loc.T("reminder.title"), Loc.T("note.energy")));
            }
            // 2: the weekend cup on Friday at 18:00
            var friday = now.Date.AddDays(((int)DayOfWeek.Friday - (int)now.DayOfWeek + 7) % 7).AddHours(18);
            if (friday < now) friday = friday.AddDays(7);
            notes.Add((2, friday, Loc.T("reminder.title"), Loc.T("note.cup")));
            // 3: two days before the season ends (with what the next rank needs)
            var end = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-2).AddHours(18);
            if (end > now)
            {
                int rank = Challenge.Rank;
                string body = rank + 1 < Challenge.RankAt.Length ? Loc.F("note.season", Challenge.RankName(rank + 1), Mathf.Max(0, Challenge.RankAt[rank + 1] - Challenge.Trophies)) : Loc.T("note.seasonTop");
                notes.Add((3, end, Loc.T("reminder.title"), body));
            }
            // two a day at most (the soonest win)
            notes.Sort((a, b) => a.at.CompareTo(b.at));
            var perDay = new Dictionary<DateTime, int>();
            var kept = new HashSet<int>();
            foreach (var n in notes)
            {
                if ((n.at - now).TotalMinutes < 1) continue;
                perDay.TryGetValue(n.at.Date, out int c);
                if (c >= 2) continue;
                perDay[n.at.Date] = c + 1;
                Set(n.slot, n.at, n.title, n.body);
                kept.Add(n.slot);
            }
            for (int s = 0; s < Slots; s++) if (!kept.Contains(s)) Set(s, null, "", "");
        }
    }
}
