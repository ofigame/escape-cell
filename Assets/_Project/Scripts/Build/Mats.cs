using System.Collections.Generic;
using BrickMuseum.Data;
using UnityEngine;

namespace BrickMuseum.Build
{
    /// <summary>
    /// Shared materials. The editor setup saves base materials in Resources/Mats so their shader variants
    /// (lit, transparent, unlit) are kept in builds; runtime materials are copies of those.
    /// </summary>
    public static class Mats
    {
        private static Material baseLit, baseTransparent, baseUnlit, baseUnlitTransparent;
        private static readonly Dictionary<string, Material> solid = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Material> ghost = new Dictionary<string, Material>();
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        private static readonly int Smooth = Shader.PropertyToID("_Smoothness");
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        private static Material Load(string name, string shader, bool transparent)
        {
            var m = Resources.Load<Material>("Mats/" + name);
            if (m != null) return m;
            m = new Material(Shader.Find(shader));
            if (transparent) MakeTransparent(m);
            return m;
        }

        public static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static Material BaseLit => baseLit ??= Load("Lit", "Universal Render Pipeline/Lit", false);
        private static Material BaseTransparent => baseTransparent ??= Load("LitTransparent", "Universal Render Pipeline/Lit", true);
        private static Material BaseUnlit => baseUnlit ??= Load("Unlit", "Universal Render Pipeline/Unlit", false);
        private static Material BaseUnlitTransparent => baseUnlitTransparent ??= Load("UnlitTransparent", "Universal Render Pipeline/Unlit", true);

        /// <summary>A plain lit colour (scenery, furniture).</summary>
        public static Material Lit(Color c, float smooth = 0.2f)
        {
            var m = new Material(BaseLit);
            m.SetColor(BaseColor, c);
            m.SetColor(Emission, Color.black);
            m.SetFloat(Smooth, smooth);
            return m;
        }

        public static Material Unlit(Color c)
        {
            var m = new Material(c.a < 0.999f ? BaseUnlitTransparent : BaseUnlit);
            m.SetColor(BaseColor, c);
            return m;
        }

        public static Material UnlitTexture(Texture t, bool transparent)
        {
            var m = new Material(transparent ? BaseUnlitTransparent : BaseUnlit);
            m.SetTexture(BaseMap, t);
            m.SetColor(BaseColor, Color.white);
            return m;
        }

        public static Material LitTexture(Texture t, bool transparent)
        {
            var m = new Material(transparent ? BaseTransparent : BaseLit);
            m.SetTexture(BaseMap, t);
            m.SetColor(BaseColor, Color.white);
            m.SetColor(Emission, Color.black);
            m.SetFloat(Smooth, 0.35f);
            return m;
        }

        /// <summary>The brick plastic of a palette colour (shared).</summary>
        public static Material Brick(string key)
        {
            if (solid.TryGetValue(key, out var m)) return m;
            var c = Palette.Of(key);
            if (Palette.IsGlass(key))
            {
                // clear glass with a cool tint: you see in and out, the sky shines on it
                m = new Material(BaseTransparent);
                var tint = Color.Lerp(c, new Color(0.22f, 0.38f, 0.55f), 0.65f);
                m.SetColor(BaseColor, new Color(tint.r, tint.g, tint.b, 0.5f));
                m.SetColor(Emission, Color.black);
                m.SetFloat(Smooth, 0.96f);
            }
            else
            {
                m = Lit(c, key == "gold" ? 0.75f : 0.55f);
                if (key == "gold") m.SetFloat("_Metallic", 0.5f);
            }
            m.name = "Brick " + key;
            return solid[key] = m;
        }

        /// <summary>A private copy for the piece in the player's hand (it turns red over a wrong spot).</summary>
        public static Material DragCopy(Material source)
        {
            var m = new Material(source);
            m.EnableKeyword("_EMISSION");
            return m;
        }

        public static void Tint(Material m, bool wrong) => m.SetColor(Emission, wrong ? new Color(0.55f, 0f, 0f) : Color.black);

        /// <summary>See-through marker of where a piece goes (shared per colour; its alpha pulses).</summary>
        public static Material Ghost(string key)
        {
            if (ghost.TryGetValue(key, out var m)) return m;
            var c = Color.Lerp(Palette.Of(key), Color.white, 0.45f);
            m = Unlit(new Color(c.r, c.g, c.b, 0.35f));
            m.SetFloat("_Cull", 0f);
            return ghost[key] = m;
        }

        private static Material hint;

        /// <summary>Yellow blinking marker for the hint's target.</summary>
        public static Material Hint => hint ??= Unlit(new Color(1f, 0.84f, 0f, 0.6f));

        public static void Pulse(float time)
        {
            float a = 0.33f + 0.17f * Mathf.Sin(time * 4f);
            foreach (var m in ghost.Values) { var c = m.GetColor(BaseColor); c.a = a; m.SetColor(BaseColor, c); }
            var h = Hint.GetColor(BaseColor); h.a = 0.45f + 0.4f * Mathf.Sin(time * 9f); Hint.SetColor(BaseColor, h);
        }
    }
}
