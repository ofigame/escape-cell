using System.Linq;
using BrickMuseum.City;
using BrickMuseum.Data;
using BrickMuseum.UI;
using UnityEngine;

namespace BrickMuseum.Core
{
    /// <summary>
    /// A workshop build is never lost: every piece put on, every mistake is saved (<see cref="PendingBuild"/>), so after
    /// leaving the workshop (or closing the game) the city shows "İnşaya devam et" and the build goes on from where it
    /// stopped — the same model and slot, the pieces already on, the hearts left. Starting another build replaces it.
    /// </summary>
    public sealed partial class Game
    {
        private static PendingBuild Pending => Progress.Data.pending;

        /// <summary>Remembers the build in progress (called after every change).</summary>
        private void SavePending()
        {
            if (current == null || bench.Done) return;
            var p = Pending;
            p.model = current.id;
            p.slot = buildSlot != null ? buildSlot.id : "";
            p.placed = bench.targets.Select((t, i) => (t, i)).Where(x => x.t.placed).Select(x => x.i).ToList();
            p.heartsTotal = heartsTotal; p.heartsLeft = heartsLeft; p.mistakes = mistakes; p.adUsed = adUsed;
            // a build on a plot stays there half-finished (shown in the city) until it is finished or given up
            if (p.slot != "")
            {
                Progress.Data.sites.RemoveAll(x => x.slot == p.slot);
                Progress.Data.sites.Add(p.Copy());
            }
            Progress.Save();
        }

        private static void ClearPending()
        {
            if (Pending.Has && Pending.slot != "") Progress.Data.sites.RemoveAll(x => x.slot == Pending.slot);
            Progress.Data.pending = new PendingBuild();
            Progress.Save();
        }

        /// <summary>The city's "İnşaya devam et": back to the workshop with the build as it was left.</summary>
        /// <summary>The build "İnşaya devam et" goes back to: the one last worked on, else the newest half-finished plot.</summary>
        private static PendingBuild LastSite => Pending.Has ? Pending : Progress.Data.sites.LastOrDefault();

        private static bool AnySite => Pending.Has || Progress.Data.sites.Count > 0;

        private void ResumeBuild()
        {
            var last = LastSite;
            if (last == null) return;
            ResumeSite(last);
        }

        /// <summary>Back to the workshop with a plot's half-finished build.</summary>
        private void ResumeSite(PendingBuild site)
        {
            Progress.Data.pending = site.Copy();
            var p = Pending;
            var m = p.Has ? ModelCatalog.Find(p.model) : null;
            if (m == null) { ClearPending(); UpdateCityHud(); city.RefreshAll(); return; }
            buildSlot = string.IsNullOrEmpty(p.slot) ? null : CityLayout.Find(p.slot);
            ClosePanel();
            EnterWorkshop();
            StartLevel(m, p);
        }

        /// <summary>Puts the saved pieces back on the bench and the saved hearts back (StartLevel's resume part).</summary>
        private void RestorePending(PendingBuild p)
        {
            foreach (int i in p.placed) if (i >= 0 && i < bench.targets.Count && !bench.targets[i].placed) bench.PlaceAt(bench.targets[i]);
            heartsTotal = Mathf.Max(1, p.heartsTotal);
            heartsLeft = p.heartsLeft;
            mistakes = p.mistakes;
            adUsed = p.adUsed;
        }

        /// <summary>Resuming a build whose hearts had run out shows the out-of-hearts card again.</summary>
        private void ShowOutOfHearts()
        {
            wu.failInfo.text = $"{current.name} 3 gün boyunca çarkta çıkmayacak.\nAlan boş kaldı, başka bir model deneyebilirsin.";
            wu.failAdBtn.gameObject.SetActive(!adUsed);
            wu.fail.SetActive(true);
        }
    }
}
