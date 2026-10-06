using UnityEngine;

namespace SquashBot.Visual
{
    public static class MaterialFactory
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private static Material baseLit;
        private static Material baseUnlit;

        public static Material Create(Color color, Color emission)
        {
            // Created by the editor setup so the shaders (with emission) are guaranteed to be in builds.
            if (baseLit == null) baseLit = LoadBase("SquashBot_BaseLit", "Universal Render Pipeline/Lit", "Standard");

            var material = new Material(baseLit);
            SetColors(material, color, emission);
            return material;
        }

        private static Material baseTransparent;

        /// <summary>Alpha-blended lit material (shield bubble, soft shadows). Use the color's alpha for opacity.</summary>
        public static Material CreateTransparent(Color color, Color emission)
        {
            if (baseTransparent == null)
            {
                baseTransparent = Resources.Load<Material>("SquashBot_BaseTransparent");
                if (baseTransparent == null)
                {
                    baseTransparent = LoadBase("SquashBot_BaseLit", "Universal Render Pipeline/Lit", "Standard");
                    baseTransparent = new Material(baseTransparent);
                    ConfigureTransparent(baseTransparent);
                }
            }

            var material = new Material(baseTransparent);
            SetColors(material, color, emission);
            return material;
        }

        /// <summary>Switches a URP Lit material to alpha blending (also used by the editor setup).</summary>
        public static void ConfigureTransparent(Material m)
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

        public static Material CreateUnlit(Texture texture)
        {
            if (baseUnlit == null) baseUnlit = LoadBase("SquashBot_BaseUnlit", "Universal Render Pipeline/Unlit", "Unlit/Texture");

            var material = new Material(baseUnlit);
            material.SetTexture(BaseMapId, texture);
            material.SetTexture(MainTexId, texture);
            material.SetColor(BaseColorId, Color.white);
            return material;
        }

        private static Material LoadBase(string resource, string shaderName, string fallbackShader)
        {
            var material = Resources.Load<Material>(resource);
            if (material != null) return material;

            var shader = Shader.Find(shaderName);
            if (shader == null) shader = Shader.Find(fallbackShader);
            return new Material(shader);
        }

        public static void SetColors(Material material, Color color, Color emission)
        {
            material.SetColor(BaseColorId, color);
            material.SetColor(ColorId, color);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.SetColor(EmissionId, emission);
        }
    }
}
