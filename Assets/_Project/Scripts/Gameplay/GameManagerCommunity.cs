using System.Collections;
using SquashBot.Audio;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The design's §11: the Community General on the menu (asked of the server once a minute while the menu shows; a tap
    /// takes a reward reached), the welcome back after three days away, and the duel's music following the match (the
    /// tension layer while the score is close in the last minute, the peak in the last ten seconds, through an ultimate
    /// and on a level score's golden knock-down).
    /// </summary>
    public partial class GameManager
    {
        private float generalAskedAt = -999f;
        private bool generalAsking, generalWired;
        private bool rulesAsked;

        /// <summary>Each frame on the menu: the General asked again after a minute, its bar shown.</summary>
        private void UpdateCommunity()
        {
            if (ui == null || State != GameState.Menu) return;
            if (!generalWired) { generalWired = true; ui.GeneralPressed += TakeGeneral; }
            // once a session: the server's duel table (a newer one is used from now on, in the bot duels too)
            if (!rulesAsked) { rulesAsked = true; cameraRig.StartCoroutine(FoiNet.DuelRules(json => { if (json != null) DuelBalanceTable.Offer(json); })); }
            if (!generalAsking && Time.realtimeSinceStartup - generalAskedAt > 60f && Challenge.Open)
            {
                generalAskedAt = Time.realtimeSinceStartup;
                cameraRig.StartCoroutine(AskGeneral());
            }
        }

        private IEnumerator AskGeneral()
        {
            generalAsking = true;
            yield return FoiNet.General(g => { if (g != null) CommunityGeneral.Last = g; });
            generalAsking = false;
            RefreshGeneral();
        }

        private void RefreshGeneral()
        {
            if (ui == null) return;
            ui.SetGeneral(CommunityGeneral.Line(), CommunityGeneral.Share, CommunityGeneral.Waiting > 0);
        }

        /// <summary>The General's bar tapped: the next reward reached (taken on the server first), else what it is.</summary>
        private void TakeGeneral()
        {
            int step = CommunityGeneral.Waiting;
            if (step <= 0) { ui.ShowIntro(Loc.T("gen.title"), Loc.F("gen.about", Loc.T("gen.name"))); return; }
            cameraRig.StartCoroutine(FoiNet.GeneralClaim(step, ok =>
            {
                if (!ok) { generalAskedAt = -999f; return; }
                string gift = CommunityGeneral.Give(step);
                var g = CommunityGeneral.Last;
                if (g != null) { var list = new System.Collections.Generic.List<int>(g.taken ?? new int[0]) { step }; g.taken = list.ToArray(); }
                AudioManager.PlaySfx(Sfx.Win, 1f, 1.1f);
                ui.ShowIntro(Loc.T("gen.title"), Loc.F("gen.gift", step * 25, gift));
                ui.RefreshMenuCoins();
                RefreshGeneral();
            }));
        }

        /// <summary>On the menu: the welcome back after three days away (once), unless the first session's quiet holds.</summary>
        private bool OfferWelcomeBack()
        {
            ReturningPlayer.Check();
            ReturningPlayer.NoteSeason();
            if (!ReturningPlayer.Pending || FirstSession.Quiet) return false;
            string text = ReturningPlayer.Take();
            ui.ShowIntro(Loc.T("back.title"), text);
            ui.RefreshMenuCoins();
            return true;
        }

        /// <summary>
        /// The duel's music: the base layer always; the tension layer while the score is close in the last minute; the
        /// peak in the last ten seconds, through an ultimate, and when the score is level at the end (the golden knock-down).
        /// </summary>
        private float DuelTension()
        {
            var s = DuelMusicSettings.Current;
            bool close = Mathf.Abs(chDuelMe - chDuelThem) <= s.closeScore;
            if (ultimateOn || overtime || chDuelLeft <= s.lastSeconds) return s.peakTension; // (the golden knock-down: the peak throughout)
            if (close && chDuelLeft <= s.lastMinute) return s.closeTension;
            return s.baseTension;
        }
    }
}
