using System;
using System.Collections;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.OpenWorld
{
    /// <summary>
    /// The open world and the server: before it opens, the server's clock (crops and raids run on it) and its copy of the
    /// farm (taken when newer — a new phone, a reinstall); while playing and on leaving, the farm's copy goes up.
    /// Offline it all just waits: the game plays on the last known clock.
    /// </summary>
    public class OwSync : MonoBehaviour
    {
        private static OwSync runner;
        private bool sending;

        private static OwSync Runner
        {
            get
            {
                if (runner != null) return runner;
                var go = new GameObject("OwSync");
                DontDestroyOnLoad(go);
                return runner = go.AddComponent<OwSync>();
            }
        }

        /// <summary>Fetches the clock and the server's copy (at most a few seconds), then opens the world.</summary>
        public static void Then(Action open) => Runner.StartCoroutine(Runner.Before(open));

        private IEnumerator Before(Action open)
        {
            bool timeDone = false, worldDone = false;
            StartCoroutine(FoiNet.WorldTime(now => { OwSave.SetServerTime(now); timeDone = true; }));
            FoiNet.WorldBody world = null;
            StartCoroutine(FoiNet.GetWorld(w => { world = w; worldDone = true; }));
            float until = Time.realtimeSinceStartup + 4f;
            while ((!timeDone || !worldDone) && Time.realtimeSinceStartup < until) yield return null;
            if (world != null)
            {
                OwSave.SetServerTime(world.now);
                if (world.version > OwSave.Current.version && !string.IsNullOrEmpty(world.json)) OwSave.Replace(world.json);
            }
            open();
        }

        /// <summary>Sends the farm's copy if it changed since the last one went up.</summary>
        public static void Upload()
        {
            if (!OwSave.Dirty || Runner.sending) return;
            Runner.StartCoroutine(Runner.Send());
        }

        private IEnumerator Send()
        {
            sending = true;
            var save = OwSave.Current;
            long version = save.version;
            yield return FoiNet.PutWorld(version, save.Json, ok => { if (ok && save.version == version) OwSave.Dirty = false; });
            sending = false;
        }
    }
}
