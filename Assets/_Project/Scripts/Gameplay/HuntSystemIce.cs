using SquashBot.Core;
using SquashBot.Audio;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The ice bomb: the enemies round where it lands stand frozen in a block of ice for a few seconds (no steps, no
    /// blows), then the ice cracks and they go on.
    /// </summary>
    public partial class HuntSystem
    {
        private static Material iceMat;

        /// <summary>Freezes every enemy (not foi's soldiers) within <paramref name="radius"/> of a tile and hits it
        /// with <paramref name="damage"/>. Returns how many it caught.</summary>
        public int IceAround(GridPos at, int radius, float seconds, int damage)
        {
            int n = 0;
            var all = guards.ToArray(); // a blow may finish one (and change the list)
            foreach (var g in all)
            {
                if (g.dead || g.ally || g.remote || g.squad || g.mon != null || g == challengeFoe || Chebyshev(g.pos, at) > radius) continue;
                if (damage > 0) Strike(g.pos, damage);
                if (g.dead || g.root == null) continue;
                g.iceUntil = Time.time + seconds;
                g.windup = -1f; // the blow it was winding up is lost
                if (g.root != null) ClearTelegraph(g);
                if (g.ice == null) g.ice = IceBlock(g.root);
                n++;
            }
            return n;
        }

        /// <summary>True while the guard is held in its ice (it does nothing this frame); cracks the block when it ends.</summary>
        private bool Iced(Guard g)
        {
            if (g.ice == null) return false;
            if (Time.time < g.iceUntil) return true;
            fx.Burst(g.ice.position + Vector3.up * 0.4f, new Color(0.7f, 0.9f, 1f), new Color(1.2f, 1.6f, 2f), 16, 4f);
            AudioManager.PlaySfx(Sfx.Blocked, 0.6f, 1.5f);
            Destroy(g.ice.gameObject);
            g.ice = null;
            return false;
        }

        private static Transform IceBlock(Transform root)
        {
            if (iceMat == null) iceMat = MaterialFactory.CreateTransparent(new Color(0.6f, 0.88f, 1f, 0.45f), new Color(0.25f, 0.5f, 0.8f));
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(block.GetComponent<Collider>());
            block.name = "Ice";
            block.GetComponent<Renderer>().sharedMaterial = iceMat;
            var t = block.transform;
            t.SetParent(root, false);
            // Sized to the robot in world units, whatever the root's own scale.
            var s = root.lossyScale;
            t.localScale = new Vector3(0.95f / Mathf.Max(0.01f, s.x), 1.3f / Mathf.Max(0.01f, s.y), 0.95f / Mathf.Max(0.01f, s.z));
            t.localPosition = new Vector3(0f, 0.65f / Mathf.Max(0.01f, s.y), 0f);
            t.localRotation = Quaternion.Euler(0f, 12f, 0f);
            return t;
        }
    }
}
