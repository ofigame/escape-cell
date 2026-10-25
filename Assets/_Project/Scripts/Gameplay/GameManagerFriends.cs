using SquashBot.Data;
using SquashBot.UI;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Friends and duel invitations: the friends list (add by user name, answer requests, remove), and inviting a friend
    /// straight to a duel — a code is made for that friend only and they are told (in the game: a card over the menu;
    /// away: a notification). While the menu is up the game looks for an invitation every few seconds; accepting one
    /// enters its code, and the duel starts as with any friend's code.
    /// </summary>
    public partial class GameManager
    {
        private FriendsScreen friendsUi;
        private float inviteLookAt;
        private bool inviteLooking;

        /// <summary>The friends screen, made the first time it is needed.</summary>
        private FriendsScreen FriendsUi
        {
            get
            {
                if (friendsUi != null) return friendsUi;
                friendsUi = FriendsScreen.Create(ui.transform.Find("Canvas/SafeArea") ?? ui.transform.Find("Canvas") ?? ui.transform);
                friendsUi.AddPressed += name => cameraRig.StartCoroutine(FoiNet.AddFriend(name, (err, nowFriends) =>
                {
                    friendsUi.SetStatus(err == null ? Loc.T(nowFriends ? "friends.nowFriends" : "friends.requestSent") : Loc.T(err == "offline" ? "live.offline" : "friends.err." + err));
                    if (err == null) { friendsUi.ClearInput(); RefreshFriends(false); }
                }));
                friendsUi.AcceptPressed += id => cameraRig.StartCoroutine(FoiNet.AcceptFriend(id, _ => RefreshFriends(false)));
                friendsUi.RemovePressed += id => cameraRig.StartCoroutine(FoiNet.RemoveFriend(id, _ => RefreshFriends(false)));
                friendsUi.InvitePressed += InviteFriend;
                friendsUi.CopyPressed += name => cameraRig.StartCoroutine(CopyDuel(name));
                friendsUi.CodePressed += code => cameraRig.StartCoroutine(FoiNet.AddFriendCode(code, (name, err) =>
                {
                    friendsUi.SetStatus(err == null ? Loc.F("friends.addedCode", name) : Loc.T(err == "offline" ? "live.offline" : "friends.err." + err));
                    if (err == null) { friendsUi.ClearInput(); RefreshFriends(false); }
                }));
                friendsUi.CopySettingPressed += on => cameraRig.StartCoroutine(FoiNet.SetCopyAllowed(on, ok =>
                {
                    if (!ok) { friendsUi.SetStatus(Loc.T("live.offline")); return; }
                    Prefs.SetInt("sb_copy_allowed", on ? 1 : 0);
                    friendsUi.SetCopySetting(on);
                }));
                friendsUi.InviteAccepted += AcceptInvite;
                friendsUi.InviteDeclined += inv => cameraRig.StartCoroutine(FoiNet.DeclineInvite(inv.code));
                return friendsUi;
            }
        }

        /// <summary>FRIENDS: the list (the name first, if the player has none yet).</summary>
        private void ShowFriends()
        {
            if (chUi == null) InitChallenge();
            if (!Challenge.NameChosen) { chUi.HideModes(); chUi.ShowName(Challenge.PlayerName); return; }
            FriendsUi.Show();
            RefreshFriends(true);
        }

        private void RefreshFriends(bool first)
        {
            cameraRig.StartCoroutine(SyncThenFriends(first));
        }

        private System.Collections.IEnumerator SyncThenFriends(bool first)
        {
            bool ok = true;
            if (first)
            {
                yield return FoiNet.EnsurePlayer(r => ok = r);
                if (ok && FoiNet.SyncedName != Challenge.PlayerName)
                    yield return FoiNet.SetName(Challenge.PlayerName, (e, p) => { if (e == null) Challenge.Sync(p); });
            }
            if (!ok) { FriendsUi.Fill(null); yield break; }
            yield return FoiNet.Friends(list => FriendsUi.Fill(list));
            FriendsUi.SetCopySetting(Prefs.GetInt("sb_copy_allowed", 1) == 1);
            yield return FoiNet.MyFriendCode(code => { FriendsUi.SetMyCode(code); if (!string.IsNullOrEmpty(code)) Share.MyCode = code; });
            yield return GiveInviteRewards();
        }

        /// <summary>A friend invited to a duel: a code made for them, the friend panel waits for them.</summary>
        private void InviteFriend(string id, string name, int minutes)
        {
            if (chUi == null) InitChallenge();
            FriendsUi.Hide();
            chUi.ShowFriendPanel();
            invitedName = name;
            StartFriend(1, null, minutes, id);
        }

        private string invitedName;

        /// <summary>An invitation link opened the game: its friend code added (the player and the inviter become friends).</summary>
        private void OnDeepLink(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            int at = url.IndexOf("invite/", System.StringComparison.OrdinalIgnoreCase);
            if (at < 0) return;
            string code = url.Substring(at + 7).Trim('/', ' ').ToUpperInvariant();
            if (code.Length > 6) code = code.Substring(0, 6);
            if (code.Length != 6) return;
            cameraRig.StartCoroutine(AcceptInviteLink(code));
        }

        private System.Collections.IEnumerator AcceptInviteLink(string code)
        {
            bool ok = false;
            yield return FoiNet.EnsurePlayer(r => ok = r);
            if (!ok) yield break;
            string friend = null, err = null;
            yield return FoiNet.AddFriendCode(code, (n, e) => { friend = n; err = e; });
            if (friend != null) ui.ShowIntro(Loc.T("friends.title"), Loc.F("friends.addedCode", friend));
            else if (err == "already") ui.ShowIntro(Loc.T("friends.title"), Loc.T("friends.err.already"));
        }

        /// <summary>A duel with an away friend's copy (§5.5): their look, weapon, parts and moves, played by the AI (no rank
        /// points; it counts for the score between you, and a win tells them).</summary>
        private System.Collections.IEnumerator CopyDuel(string name)
        {
            if (chUi == null) InitChallenge();
            if (challengeRun) yield break;
            FoiNet.Rival r = null; string err = null;
            yield return FoiNet.FriendCopy(name, (rv, e) => { r = rv; err = e; });
            if (r == null || string.IsNullOrEmpty(r.name)) { FriendsUi.SetStatus(Loc.T(err == "offline" ? "live.offline" : err == "nocopy" ? "friends.noCopy" : "friends.err.notfound")); yield break; }
            FriendsUi.Hide();
            if (State != GameState.Menu) ShowMenu();
            chCancelled = false; chFriend = 0; chOnline = false; chLive = false; chMatchId = 0;
            copyMatch = true;
            rival = Challenge.FromRival(r);
            rival.seed = Mathf.Abs(r.name.GetHashCode());
            chPhase = ChPhase.Lobby;
            int token = ++chLobbyToken;
            chUi.ShowLobby(MyCard(), Challenge.Rank, Loc.T("friends.copyMode"));
            chUi.ShowRival(RivalCard(rival), rival.Rank);
            chUi.LockLobby();
            chUi.SetLobbyStatus(Loc.F("friends.copyOf", r.name));
            yield return new WaitForSecondsRealtime(1.6f);
            if (token != chLobbyToken) yield break;
            int stage = PickedStage(rival.seed % DuelStageCount);
            yield return SpinToStage(stage);
            if (token != chLobbyToken) yield break;
            chUi.HideLobby();
            BuildDuelBoard(stage);
        }

        /// <summary>A copy duel is being played (no rank points, no chest; it counts for the score between friends).</summary>
        private bool copyMatch;

        /// <summary>Revenge after a lost friend match or copy duel: the friend's copy again.</summary>
        private void Revenge(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            cameraRig.StartCoroutine(RevengeAfterMenu(name));
        }

        private System.Collections.IEnumerator RevengeAfterMenu(string name)
        {
            ShowMenu();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return CopyDuel(name);
        }

        /// <summary>Invitation rewards waiting from the server: a bronze chest and 10 core parts each.</summary>
        private System.Collections.IEnumerator GiveInviteRewards()
        {
            int gifts = 0;
            yield return FoiNet.InviteRewards(n => gifts = n);
            if (gifts <= 0) yield break;
            for (int i = 0; i < gifts; i++) { DuelChests.Add(ChestTier.Mid); CoreParts.Add(10); }
            ui.ShowIntro(Loc.T("invite.title"), Loc.F("invite.gift", gifts));
            ui.RefreshChests();
        }

        /// <summary>A friend's invitation accepted: its code entered (the duel's minutes are the inviter's).</summary>
        private void AcceptInvite(FoiNet.Invitation inv)
        {
            if (challengeRun) return;
            if (chUi == null) InitChallenge();
            if (State != GameState.Menu) ShowMenu();
            chUi.ShowFriendPanel();
            StartFriend(2, inv.code, inv.min);
        }

        /// <summary>While the menu is up: a friend's invitation is looked for every few seconds (and shown when there is one).</summary>
        private void WatchInvites()
        {
            if (inviteLooking || Time.unscaledTime < inviteLookAt || !ui.MenuVisible || challengeRun || !FoiNet.Registered || !Challenge.Enabled || !Challenge.NameChosen) return;
            if (chUi != null && (chUi.FriendOpen || chPhase != ChPhase.Off)) return; // already playing or waiting for a friend
            // Every 15 s, and not at all once the phone has lain untouched a few minutes (the radio can rest).
            if (SquashBot.Visual.FramePacer.IdleSeconds > 180f) return;
            inviteLookAt = Time.unscaledTime + 15f;
            inviteLooking = true;
            cameraRig.StartCoroutine(FoiNet.PollInvite(inv =>
            {
                inviteLooking = false;
                if (inv != null && ui.MenuVisible && !challengeRun) FriendsUi.ShowInvite(inv);
                else if (inv == null && friendsUi != null && friendsUi.InviteOpen) friendsUi.HideInvite();
            }));
        }
    }
}
