using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Long roads are split by checkpoints: a glowing arch every <see cref="CheckpointEvery"/> rows. After a crash the
    /// road starts again from the last arch passed (with the coins gathered up to it), not from the very beginning.
    /// Roads also carry plenty of armor: a shield pickup roughly every <see cref="ShieldEvery"/> rows.
    /// </summary>
    public partial class DuctRunner
    {
        private const int CheckpointEvery = 100;
        /// <summary>Roads shorter than this have no checkpoints (they are quick to run again).</summary>
        private const int CheckpointFrom = 140;
        private const int ShieldEvery = 35;

        private readonly List<int> checkpoints = new List<int>();
        private int nextCheckpoint;
        /// <summary>The last checkpoint passed on this road and the coins held there (kept through restarts).</summary>
        private int resumeRow, resumeCoins;
        private Material checkpointMat;

        /// <summary>The row the road restarts from after a crash (0 = the start).</summary>
        public int ResumeRow => resumeRow;

        /// <summary>Places the checkpoints and the extra shields on a freshly planned road and clears the ground at each arch.</summary>
        private void PlanCheckpoints()
        {
            checkpoints.Clear();
            nextCheckpoint = 0;
            if (!roadMode) return;

            // Shields: one about every 35 rows, in a lane with floor under it, away from the arches.
            var rng = new System.Random(roadLevel * 31 + 7);
            for (int r = 24; r < length - 10; r += ShieldEvery + rng.Next(-5, 6))
            {
                int l = rng.Next(Lanes);
                if (!floorPlan[Mathf.Clamp(r, 0, totalRows - 1), l]) continue;
                obstaclePlan.RemoveAll(o => o.row >= r - 1 && o.row <= r + 1 && o.kind != ObstacleKind.Roller);
                obstaclePlan.Add((ObstacleKind.Shield, l, r));
            }

            if (length < CheckpointFrom) return;
            for (int cp = CheckpointEvery; cp < length - 20; cp += CheckpointEvery)
            {
                checkpoints.Add(cp);
                // A calm stretch around the arch, so a restart never begins in front of an obstacle.
                obstaclePlan.RemoveAll(o => o.row >= cp - 3 && o.row <= cp + 8 && o.kind != ObstacleKind.Shield);
                for (int r = cp - 3; r <= cp + 8 && r < totalRows; r++)
                    for (int l = 0; l < Lanes; l++) floorPlan[r, l] = true;
            }
            while (nextCheckpoint < checkpoints.Count && checkpoints[nextCheckpoint] <= resumeRow) nextCheckpoint++;
        }

        /// <summary>A glowing arch over the road at a checkpoint row.</summary>
        private void BuildCheckpointArch(Transform root)
        {
            if (checkpointMat == null) checkpointMat = MaterialFactory.Create(new Color(0.5f, 1f, 0.7f), new Color(0.5f, 2.2f, 1f));
            foreach (float s in new[] { -1f, 1f })
                Shapes.Rounded("Post", root, new Vector3(s * (WallX - 0.1f), 1.2f, 0f), new Vector3(0.18f, 2.4f, 0.18f), 0.06f, checkpointMat);
            Shapes.Rounded("Top", root, new Vector3(0f, 2.45f, 0f), new Vector3(WallX * 2f, 0.16f, 0.18f), 0.06f, checkpointMat);
            Shapes.Rounded("Flag", root, new Vector3(0.3f, 2.2f, 0f), new Vector3(0.5f, 0.3f, 0.04f), 0.04f, checkpointMat);
        }

        private bool IsCheckpointRow(int r) => checkpoints.Contains(r);

        /// <summary>Passing an arch: the road will restart here from now on.</summary>
        private void UpdateCheckpoints()
        {
            if (crashed || ended || nextCheckpoint >= checkpoints.Count || z < checkpoints[nextCheckpoint]) return;
            resumeRow = checkpoints[nextCheckpoint++];
            resumeCoins = Coins;
            Notice?.Invoke("road.checkpoint", World(0f, 1.6f, z + 1f));
            AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.5f);
            fx.Burst(World(0f, 1.2f, z), new Color(0.5f, 1f, 0.7f), new Color(0.5f, 2.2f, 1f), 24, 5f);
        }

        /// <summary>After a restart: put the robot at the last arch passed and build the road around it.</summary>
        private void JumpToResume()
        {
            if (resumeRow <= 0) return;
            while (rows.Count > 0) Destroy(rows.Dequeue().root.gameObject);
            foreach (var o in obstacles) { Destroy(o.go.gameObject); if (o.ring != null) Destroy(o.ring.gameObject); }
            foreach (var c in coins) Destroy(c.go.gameObject);
            obstacles.Clear();
            coins.Clear();
            z = resumeRow;
            Coins = resumeCoins;
            builtRow = Mathf.Max(0, resumeRow - 6);
            while (builtRow < Mathf.Min(totalRows, resumeRow + BuildAhead)) BuildRow(builtRow++);
        }
    }
}
