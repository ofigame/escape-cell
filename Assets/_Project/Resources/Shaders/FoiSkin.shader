// foi's skin and hair: lit by its own light fixed to the camera (above, to one side, in front), never by the scene's,
// so the face looks the same on every stage and still has real light on it: a soft wrapped falloff into shade, a warm
// glow of blood under the skin near the edge of the light, a small dry sheen, fine pores from a normal map, a soft rim.
// The hair: the same light, its sheen running along each lock (Kajiya-Kay) instead of a round highlight.
Shader "SquashBot/FoiSkin"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Normal map", 2D) = "bump" {}
        _BumpScale ("Normal strength", Float) = 1
        _KeyColor ("Key light", Color) = (1, 0.96, 0.9, 1)
        _FillColor ("Ambient", Color) = (0.44, 0.43, 0.48, 1)
        _SubColor ("Under the skin", Color) = (0.95, 0.32, 0.2, 1)
        _Wrap ("Light wrap", Float) = 0.5
        _Sheen ("Sheen colour", Color) = (0.16, 0.15, 0.14, 1)
        _Gloss ("Sheen tightness", Float) = 18
        _RimColor ("Rim", Color) = (1, 0.94, 0.86, 0.35)
        _Hair ("Hair (0 skin, 1 hair)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half4 _KeyColor;
                half4 _FillColor;
                half4 _SubColor;
                half _Wrap;
                half4 _Sheen;
                half _Gloss;
                half4 _RimColor;
                half _Hair;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float3 viewWS : TEXCOORD4;
            };
            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(i.normalOS, i.tangentOS);
                o.positionCS = p.positionCS;
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.normalWS = n.normalWS;
                o.tangentWS = n.tangentWS;
                o.bitangentWS = n.bitangentWS;
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half3 nt = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                half3 N = normalize(TransformTangentToWorld(nt, half3x3(i.tangentWS, i.bitangentWS, i.normalWS)));
                half3 V = normalize(i.viewWS);
                // the key light, fixed to the camera: up, a little to the left, from in front (view space looks down -z)
                half3 L = normalize(mul((float3x3)UNITY_MATRIX_I_V, float3(-0.42, 0.62, 0.66)));
                half ndl = dot(N, L);
                half diffuse = saturate((ndl + _Wrap) / (1.0h + _Wrap));
                // blood under the skin: warmth where the light turns to shade
                half under = saturate(1.0h - abs(ndl + 0.1h) * 2.2h) * (1.0h - _Hair);
                half3 H = normalize(L + V);
                half sheen;
                if (_Hair > 0.5h)
                {
                    half3 T = normalize(i.bitangentWS); // (each lock's length runs along its picture's height)
                    half th = dot(T, H);
                    sheen = pow(saturate(sqrt(1.0h - th * th)), _Gloss) * saturate(ndl + 0.3h);
                }
                else sheen = pow(saturate(dot(N, H)), _Gloss) * saturate(ndl);
                half rim = pow(1.0h - saturate(dot(N, V)), 3.0h) * saturate(ndl + 0.6h);
                half3 col = albedo.rgb * (_FillColor.rgb + _KeyColor.rgb * diffuse)
                          + albedo.rgb * _SubColor.rgb * under * 0.2h
                          + _Sheen.rgb * sheen
                          + _RimColor.rgb * rim * _RimColor.a;
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
