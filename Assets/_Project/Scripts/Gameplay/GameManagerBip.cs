using System.Collections.Generic;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Bip, the Observer's partner. It doesn't walk the floors with the robot (except when it has to be guarded), but it
    /// senses danger first: a few short warnings per level in a little speech bubble, each one at most once a level and
    /// never two close together. On the roads between levels it rides in the robot's backpack.
    /// </summary>
    public partial class GameManager
    {
        private const float BipCooldown = 8f;
        /// <summary>How long the robot may stand under a falling block before Bip shouts.</summary>
        private const float BipStillWarning = 0.45f;

        private readonly HashSet<string> bipSaid = new HashSet<string>();
        private float bipLast = -99f, bipStill;
        private Buddy roadBip;

        private void BipReset()
        {
            bipSaid.Clear();
            bipLast = -99f;
            bipStill = 0f;
        }

        /// <summary>Bip says the line "bip.{key}", once per level, unless it spoke a moment ago.</summary>
        private void BipSay(string key)
        {
            if (bonusRun || bipSaid.Contains(key) || Time.time - bipLast < BipCooldown) return;
            bipSaid.Add(key);
            bipLast = Time.time;
            ui.Bip.Say(Loc.T("bip." + key));
        }

        /// <summary>Every playing frame: the dangers Bip watches for.</summary>
        private void UpdateBip(float dt)
        {
            // Standing still where a block is about to land.
            if (!robot.IsHopping && !robot.IsShielded && hazards.IsThreatened(robot.Position))
            {
                bipStill += dt;
                if (bipStill > BipStillWarning) BipSay("move");
            }
            else bipStill = 0f;

            if ((level.rules & FloorRule.Dark) != 0 && elapsed > 2f) BipSay("dark");
            if (level.mission == MissionType.Clone)
                foreach (var c in clones)
                    if (c.down <= 0f && c.pos.Manhattan(robot.Position) == 1) BipSay("clone");
        }

        /// <summary>Bip climbs into the robot's backpack for the road.</summary>
        private void RideBip()
        {
            if (roadBip != null) return;
            roadBip = Buddy.Create(robot.transform.position);
            roadBip.Ride(robot.Visual);
        }

        private void DropBip()
        {
            if (roadBip != null) Destroy(roadBip.gameObject);
            roadBip = null;
        }
    }
}
