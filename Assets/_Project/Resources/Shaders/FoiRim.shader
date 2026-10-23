// foi's rim light: a soft warm edge where its body turns away from the camera, added over its own paint, so it stands
// out from any background (drawn as a second pass of each of its meshes; hidden behind what is in front, as ever).
Shader "SquashBot/FoiRim"
{
    Properties
    {
        _RimColor ("Rim colour", Color) = (1, 0.95, 0.85, 1)
        _RimPower ("Rim tightness", Float) = 3.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Rim"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                half _RimPower;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half facing = saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                half rim = pow(1.0h - facing, _RimPower);
                return half4(_RimColor.rgb * rim * _RimColor.a, 0);
            }
            ENDHLSL
        }
    }
}
