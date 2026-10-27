using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The battle wear on foi's steel (Docs/ESYA_CANTA_VE_BEDEN_SISTEMI.md, section 7.4): one more pass over the armour
    /// (the body and the arms' and legs' gear) drawn by Shaders/FoiWear with one value — 0.33 scratches, 0.66 worn, 1
    /// rusty — over whatever paint it wears; at the rust a thin smoke from its back (off on the low graphics setting).
    /// </summary>
    public static class WearLook
    {
        private const string SmokeName = "WearSmoke";
        private static Shader shader;
        private static readonly Material[] mats = new Material[BattleWear.MaxLevel + 1];

        private static Material Mat(int level)
        {
            if (mats[level] != null) return mats[level];
            if (shader == null) shader = Resources.Load<Shader>("Shaders/FoiWear");
            if (shader == null || !shader.isSupported) return null;
            var m = new Material(shader) { name = "FoiWear" + level };
            m.SetFloat("_Wear", level / (float)BattleWear.MaxLevel);
            mats[level] = m;
            return m;
        }

        /// <summary>Puts wear step <paramref name="level"/> (0: none) on the armour under <paramref name="skin"/>.</summary>
        public static void Apply(Transform skin, int level)
        {
            if (skin == null) return;
            level = Mathf.Clamp(level, 0, BattleWear.MaxLevel);
            var add = level > 0 ? Mat(level) : null;
            // the armour: the biggest skinned mesh (the body) and the gear's pieces
            SkinnedMeshRenderer body = null;
            foreach (var sm in skin.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (sm.sharedMesh != null && (body == null || sm.sharedMesh.vertexCount > body.sharedMesh.vertexCount)) body = sm;
            foreach (var r in skin.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                bool armour = r == body || InGear(r.transform, skin) || r.name == "PartLook"; // (the parts' plates too)
                Set(r, armour ? add : null);
            }
            // the rust's smoke
            Transform old = null;
            foreach (var t in skin.GetComponentsInChildren<Transform>(true)) if (t.name == SmokeName) { old = t; break; }
            bool smoke = level >= BattleWear.MaxLevel && GraphicsQuality.Current != GraphicsTier.Low;
            if (!smoke) { if (old != null) { old.name = SmokeName + "(gone)"; old.gameObject.SetActive(false); Object.Destroy(old.gameObject); } return; } // (hidden at once: gone at the frame's end)
            if (old != null) return;
            Transform back = null;
            foreach (var t in skin.GetComponentsInChildren<Transform>(true)) if (t.name == "Spine2" || t.name.EndsWith(":Spine2")) { back = t; break; }
            Smoke(skin, back);
        }

        private static bool InGear(Transform t, Transform skin)
        {
            for (var p = t; p != null && p != skin; p = p.parent) if (p.name.StartsWith("Gear_")) return true; // (the arms' and legs' gear)
            return false;
        }

        /// <summary>The renderer's wear pass set to <paramref name="m"/> (null: taken off), any old one replaced.</summary>
        private static void Set(Renderer r, Material m)
        {
            var ms = r.sharedMaterials;
            int keep = 0;
            foreach (var x in ms) if (x == null || x.shader == null || x.shader.name != "SquashBot/FoiWear") keep++;
            if (keep == ms.Length && m == null) return;
            var all = new Material[keep + (m != null ? 1 : 0)];
            int i = 0;
            foreach (var x in ms) if (x == null || x.shader == null || x.shader.name != "SquashBot/FoiWear") all[i++] = x;
            if (m != null) all[i] = m;
            r.sharedMaterials = all;
        }

        /// <summary>A thin grey smoke rising slowly from its back (a few puffs, from the shared particles' paint).</summary>
        private static void Smoke(Transform skin, Transform back)
        {
            var go = new GameObject(SmokeName);
            go.transform.SetParent(back != null ? back : skin, false); // (on its back: it goes where foi goes)
            var hero = skin.GetComponent<SkinnedHero>();
            float h = hero != null && hero.TopHeight > 0.1f ? hero.TopHeight : 1f; // (its sizes: a share of foi's height, on any stage)
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.6f;
            main.startSpeed = 0.06f * h;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f * h, 0.13f * h);
            main.startColor = new Color(0.5f, 0.48f, 0.46f, 0.35f);
            main.maxParticles = 12;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var em = ps.emission;
            em.rateOverTime = 4f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.03f * h;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(0.12f * h);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Weather.AlphaMaterial;
            ps.Play();
        }
    }
}
