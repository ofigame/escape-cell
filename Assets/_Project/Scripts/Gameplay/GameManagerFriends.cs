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
