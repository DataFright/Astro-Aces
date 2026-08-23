// Hand-written URP toon shader -- BUILD_PLAN.md 6.1, DESIGN.md Sec 9. Deliberately not
// Shader Graph: its .shadergraph assets are opaque binary-ish JSON, unreadable and
// unmergeable as text, which matters for a project with no version control diffing story
// yet and a preference for reviewable code everywhere else.
//
// Three passes: UniversalForward (banded diffuse + rim, the actual look), ShadowCaster and
// DepthOnly (both reuse URP's own stock HLSL for those -- see the include comments below for
// exactly why no extra properties are needed to plug into them).
Shader "AstroAces/Toon"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.55, 0.55, 0.58, 1)
        _ShadowTint("Shadow Tint", Color) = (0.22, 0.20, 0.32, 1)
        _RimColor("Rim Color", Color) = (0.9, 0.92, 1.0, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 3.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowTint;
                half4 _RimColor;
                half _RimPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();

                float NdotL = dot(normalWS, mainLight.direction);

                // Banded diffuse, three discrete steps (DESIGN.md Sec 9 / BUILD_PLAN 6.1) --
                // no smoothstep blending between them, that would just be a soft gradient
                // again and defeat the point of a toon look.
                half band;
                if (NdotL > 0.55) band = 1.0;
                else if (NdotL > 0.05) band = 0.65;
                else band = 0.0;

                half3 litColor = _BaseColor.rgb * mainLight.color;
                half3 diffuse = lerp(_ShadowTint.rgb, litColor, band);

                // Flat ambient term so the shadow band is a colour, not pure black --
                // SampleSH is the same spherical-harmonics probe URP's own Lit shader uses.
                half3 ambient = SampleSH(normalWS) * _BaseColor.rgb * 0.35;

                float3 viewDirWS = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                half rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                half3 rimContribution = _RimColor.rgb * rim;

                return half4(diffuse + ambient + rimContribution, _BaseColor.a);
            }
            ENDHLSL
        }

        // Reuses URP's own ShadowCasterPass.hlsl verbatim. It only touches _BaseMap/_Cutoff
        // under #if defined(_ALPHATEST_ON), which this shader never defines (no alpha-cutout
        // shader_feature declared) -- so those branches compile out entirely and nothing
        // beyond Core.hlsl is needed for this pass to work.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // Same reasoning as ShadowCaster above -- URP's stock DepthOnlyPass.hlsl, alpha-test
        // branches compiled out since _ALPHATEST_ON is never defined here.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
}
