using System;
using System.Collections.Generic;

namespace SquashBot.Data
{
    /// <summary>The saved city (JSON in PlayerPrefs): the pieces, the residents' wishes and a few timestamps.</summary>
    [Serializable]
    public class CityRecord
    {
        public List<CityPlaced> pieces = new List<CityPlaced>();
        public int nextUid = 1;
        public List<CityRequest> requests = new List<CityRequest>();
        /// <summary>No new wish before this time (4 hours after the last one was granted).</summary>
        public long nextRequest;
        /// <summary>Wishes granted per resident; 5 makes a best friend.</summary>
        public int[] granted = new int[0];
        /// <summary>Best-friend reward already paid, per resident.</summary>
        public bool[] bestPaid = new bool[0];
        /// <summary>Last time the city was opened.</summary>
        public long lastVisit;
        /// <summary>Back after a week away: the first round of repairs is free.</summary>
        public bool freeRound;
    }
}
