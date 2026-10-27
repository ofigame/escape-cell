using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The duel lab's boxes on screen (Docs/DUELLO_CARPISMA_VE_TEMAS.md, section 6): each body's push box (blue; red while
    /// the two overlap), its hurt areas (green: head, chest, legs), its weapon's hit box (red, only while its blow is live),
    /// the last contact point (yellow) and the line between the two trunks (white; red under the contact distance). Rings
    /// flat on the ground plane of each point, drawn with line renderers made once.
    /// </summary>
    public class DuelBoxesView : MonoBehaviour
    {
        private const int Points = 24;
        private readonly List<LineRenderer> rings = new List<LineRenderer>();
        private LineRenderer between;
        private Material blue, green, red, yellow, white;

        /// <summary>The last contact point (the hit effects set it), shown for a moment.</summary>
        public static Vector3? LastContact;
        public static float LastContactAt = -9f;

        public static DuelBoxesView Create()
        {
            var v = new GameObject("DuelBoxes").AddComponent<DuelBoxesView>();
            v.Make();
            return v;
        }

        private void Make()
        {
            blue = MaterialFactory.Create(new Color(0.3f, 0.6f, 1f), new Color(0.6f, 1.2f, 3f));
            green = MaterialFactory.Create(new Color(0.3f, 1f, 0.4f), new Color(0.6f, 2.4f, 0.8f));
            red = MaterialFactory.Create(new Color(1f, 0.25f, 0.2f), new Color(3f, 0.6f, 0.5f));
            yellow = MaterialFactory.Create(new Color(1f, 0.9f, 0.2f), new Color(3f, 2.6f, 0.4f));
            white = MaterialFactory.Create(Color.white, new Color(2f, 2f, 2f));
            between = Line("Between", 2);
        }

        private LineRenderer Line(string name, int points)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var l = go.AddComponent<LineRenderer>();
            l.positionCount = points;
            l.widthMultiplier = 0.05f;
            l.useWorldSpace = true;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            l.receiveShadows = false;
            return l;
        }

        private int used;

        private void Ring(Vector3 centre, float radius, Material m, bool upright = false)
        {
            if (used >= rings.Count) { var l = Line("Ring", Points + 1); l.loop = false; rings.Add(l); }
            var r = rings[used++];
            r.enabled = true;
            r.sharedMaterial = m;
            for (int i = 0; i <= Points; i++)
            {
                float a = i * Mathf.PI * 2f / Points;
                var off = upright ? new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) : new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                r.SetPosition(i, centre + off * radius);
            }
        }

        private void LateUpdate()
        {
            used = 0;
            var a = SkinnedHero.DuelFirst;
            var b = SkinnedHero.DuelSecond;
            bool overlap = false;
            if (a != null && b != null)
            {
                var gap = a.TrunkPoint - b.TrunkPoint; gap.y = 0f;
                overlap = gap.magnitude < (a.PushBoxRadius + b.PushBoxRadius) * 0.97f;
                between.enabled = true;
                between.sharedMaterial = overlap ? red : white;
                between.SetPosition(0, a.TrunkPoint);
                between.SetPosition(1, b.TrunkPoint);
            }
            else between.enabled = false;
            foreach (var s in new[] { a, b })
            {
                if (s == null) continue;
                float r = s.PushBoxRadius;
                var t = s.TrunkPoint;
                Ring(t, r, overlap ? red : blue);
                Ring(t + Vector3.up * r * 1.6f, r, overlap ? red : blue);
                s.HurtSpots(out var head, out var chest, out var legs);
                Ring(head, r * 0.55f, green, true);
                Ring(chest, r * 0.6f, green, true);
                Ring(legs, r * 0.5f, green, true);
                var held = s.transform.parent != null ? s.transform.parent.GetComponentInChildren<HeldWeapon>() : null;
                if (held != null && held.Tip != null && held.Striking) Ring(held.Tip.position, r * 0.35f, red, true);
            }
            if (LastContact.HasValue && Time.time - LastContactAt < 0.6f) Ring(LastContact.Value, 0.12f, yellow, true);
            for (int i = used; i < rings.Count; i++) rings[i].enabled = false;
        }
    }
}
