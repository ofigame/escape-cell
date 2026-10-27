// foi's battle wear (Docs/ESYA_CANTA_VE_BEDEN_SISTEMI.md, section 7): one value opens the marks over the armour's own
// paint, whatever paint it wears — 0.33 scratches and small dents, 0.66 soot and deeper scratches, 1 rust patches.
// Drawn as one more pass of the armour's meshes (as the rim light is), on its texture's coordinates so the marks stay
// put on the steel as it moves. Made in the shader: no textures to load.
Shader "SquashBot/FoiWear"
{
    Properties
    {
        _Wear ("Wear (0 shiny .. 1 rusty)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-1" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Wear"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Wear;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 viewWS : TEXCOORD2; };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1.0), u.x), u.y);
            }
            float Fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 4; k++) { v += a * Noise(p); p *= 2.03; a *= 0.5; }
                return v;
            }

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.uv = i.uv;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float w = _Wear;
                if (w <= 0.001) return half4(0, 0, 0, 0);
                // scratches: short thin strokes, each cell its own way; more and longer with the wear
                float2 cell = i.uv * 22.0;
                float2 id = floor(cell), f = frac(cell) - 0.5;
                float ang = Hash(id) * 6.2832;
                float2 dir = float2(cos(ang), sin(ang));
                float along = dot(f, dir), across = dot(f, float2(-dir.y, dir.x));
                float len = 0.18 + 0.2 * w;
                float scratch = (1.0 - smoothstep(0.0, 0.045, abs(across))) * (1.0 - smoothstep(len, len + 0.08, abs(along)));
                scratch *= step(Hash(id + 7.31), 0.45 + 0.35 * w);
                // dents: small dark dimples
                float dent = smoothstep(0.74, 0.86, Noise(i.uv * 40.0 + 3.1)) * saturate(w * 3.0);
                // soot from the second step
                float soot = smoothstep(0.45, 0.62, Fbm(i.uv * 6.0)) * saturate(w * 3.0 - 1.0);
                // rust at the third
                float rust = smoothstep(0.5, 0.66, Fbm(i.uv * 9.0 + 13.7)) * saturate(w * 3.0 - 2.0);

                // the steel dulled a little more with every step (less shine), under the marks
                half3 col = half3(0.18, 0.17, 0.16);
                half a = 0.1 * w;
                col = lerp(col, half3(0.1, 0.09, 0.09), dent); a = max(a, dent * 0.6);
                col = lerp(col, half3(0.04, 0.035, 0.03), soot); a = max(a, soot * 0.75);
                col = lerp(col, half3(0.36, 0.16, 0.06), rust); a = max(a, rust * 0.85);
                col = lerp(col, half3(0.95, 0.96, 1.0), scratch); a = max(a, scratch * 0.85);
                half facing = saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                return half4(col * (0.55 + 0.45 * facing), a);
            }
            ENDHLSL
        }
    }
}
