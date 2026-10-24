using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// 2 against 2 (the scenario's §4.1, phase 6): foi and a partner on one side, the rival and its partner on the other.
    /// The partners are foi too (in their own outfits and weapons), fought by the AI through the squads' fighting: they
    /// strike the other side's foi and partner; a partner who falls is back for the next round. A knock-down of a side's
    /// leader scores, as in the duel. Equal power, no rank points.
    /// </summary>
    public partial class GameManager
    {
        private bool twoVsTwo;
        private string myPartner, rivalPartner;
        /// <summary>The smaller glowing rings under the partners (blue the player's side, red the rival's).</summary>
        private FootRing myPartnerRing, rivalPartnerRing;

        /// <summary>Each frame: the partners' rings under them (gone with them).</summary>
        private void UpdatePartnerRings()
        {
            if (!twoVsTwo) { DropPartnerRings(); return; }
            if (myPartnerRing == null) myPartnerRing = FootRing.Make(new Color(0.25f, 0.6f, 1f), 2f, true);
            if (rivalPartnerRing == null) rivalPartnerRing = FootRing.Make(new Color(1f, 0.25f, 0.2f), 2f, true);
            Vector3? mine = null, theirs = null;
            foreach (var (pm, pMine) in hunt.Partners()) { if (pMine) mine = pm.position; else theirs = pm.position; }
            myPartnerRing.Follow(mine);
            rivalPartnerRing.Follow(theirs);
        }

        private void DropPartnerRings()
        {
            if (myPartnerRing != null) Destroy(myPartnerRing.gameObject);
            if (rivalPartnerRing != null) Destroy(rivalPartnerRing.gameObject);
            myPartnerRing = rivalPartnerRing = null;
        }

        /// <summary>The player's partner when it is a friend's copy (their look and weapon; null: a made-up partner).</summary>
        private FoiNet.Rival friendPartner;

        /// <summary>A friend for the partner's place: one of the player's friends whose copy may fight (null: none).</summary>
        private System.Collections.IEnumerator FindFriendPartner()
        {
            friendPartner = null;
            if (!FoiNet.Registered) yield break;
            FoiNet.FriendList list = null;
            yield return FoiNet.Friends(l => list = l);
            if (list?.friends == null || list.friends.Length == 0) yield break;
            var order = new System.Collections.Generic.List<FoiNet.Friend>(list.friends);
            // the friend played most first (the score between you), then any
            order.Sort((a, b) => { var ra = Rivalry.Of(a.name); var rb = Rivalry.Of(b.name); return (rb.wins + rb.losses).CompareTo(ra.wins + ra.losses); });
            foreach (var f in order)
            {
                FoiNet.Rival r = null;
                yield return FoiNet.FriendCopy(f.name, (rv, e) => r = rv);
                if (r != null && !string.IsNullOrEmpty(r.name)) { friendPartner = r; yield break; }
            }
        }

        /// <summary>A partner foi's look: dressed as its name says, its weapon in hand (no shield).</summary>
        private Transform PartnerLook(string name, string weaponId, string look = null)
        {
            var root = new GameObject("Partner " + name).transform;
            var outfit = look != null ? Cosmetics.OutfitFrom(look) : Cosmetics.OutfitFor(name);
            robot.ApplyOutfit(outfit);
            var copy = robot.BuildLookalike(root).transform;
            var own = new System.Collections.Generic.Dictionary<Material, Material>();
            foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (!own.TryGetValue(mats[i], out var m)) own[mats[i]] = m = new Material(mats[i]);
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
            robot.ApplyOutfit(Cosmetics.Outfit());
            root.localScale = robot.Visual.lossyScale;
            var drop = new System.Collections.Generic.List<GameObject>();
            foreach (Transform c in copy.GetComponentsInChildren<Transform>(true)) if (c.name == "HeldWeapon" || c.name == "HeldShield") drop.Add(c.gameObject);
            foreach (var g in drop) DestroyImmediate(g);
            foreach (var sh in copy.GetComponentsInChildren<ShieldHold>(true)) DestroyImmediate(sh);
            HeldWeapon.Attach(copy, Armory.Get(weaponId));
            return root;
        }

        /// <summary>The partners on their marks (the first round), or back for a new round if they fell.</summary>
        private void PlacePartners(bool first)
        {
            if (!twoVsTwo || LiveActive) return;
            var (mine, theirs) = DuelMarks;
            int back = theirs.y < mine.y ? 1 : -1;
            if (first)
            {
                var rng = new System.Random(rival.seed ^ 0x51ed27);
                myPartner = friendPartner != null ? friendPartner.name : Challenge.BotName(rng);
                rivalPartner = Challenge.BotName(rng);
            }
            // a partner deals about half a foi's damage and has about two thirds of its health
            var w = Armory.Equipped;
            // a partner strikes about every 0.7 s: each blow about two of a foi's (half a foi's damage a second)
            var stats = new Army.Stats { hp = Mathf.RoundToInt(duelMine.hp * 0.65f), hit = DuelPower.Blow(w, duelMine.weaponTier) * 2f, armor = duelMine.armor };
            var theirStats = new Army.Stats { hp = Mathf.RoundToInt(duelTheirs.hp * 0.65f), hit = DuelPower.Blow(Armory.Get(rival.weapon), duelTheirs.weaponTier) * 2f, armor = duelTheirs.armor };
            if (hunt.SquadAlive(true) == 0)
                hunt.AddSquadSoldier(true, 0, mine + new GridPos(3, 0), stats, foiModel: friendPartner != null
                    ? PartnerLook(myPartner, Armory.Get(friendPartner.weapon).id, friendPartner.look ?? "")
                    : PartnerLook(myPartner, w.id));
            if (hunt.SquadAlive(false) == 0)
                hunt.AddSquadSoldier(false, 0, theirs + new GridPos(-3, 0), theirStats, foiModel: PartnerLook(rivalPartner, rival.weapon));
            hunt.SetSquadOrder(true, 0, true);
            hunt.SetSquadOrder(false, 0, true);
            // a partner back after a fall: its cape already hangs on the post (it comes without)
            if (!first && capesOff)
                foreach (var (pm, _) in hunt.Partners())
                    foreach (var cape in pm.GetComponentsInChildren<SoftCape>(true)) { var off = cape.TakeOff(); if (off != null) Destroy(off.gameObject); }
        }
    }
}
