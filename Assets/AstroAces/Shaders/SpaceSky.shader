// Custom skybox shader -- BUILD_PLAN.md 6.2, DESIGN.md Sec 9: vertical gradient deep purple
// (horizon) to near-black blue (zenith), procedural hash stars, a faint nebula band. No
// texture/cubemap asset -- everything here is computed from the skybox proxy geometry's
// object-space position, which for Unity's skybox render (Camera.RenderSkybox) is always
// centred on the camera, so it doubles directly as a view direction.
Shader "AstroAces/SpaceSky"
{
    Properties
    {
        _HorizonColor("Horizon Color", Color) = (0.30, 0.10, 0.38, 1)
        _ZenithColor("Zenith Color", Color) = (0.02, 0.02, 0.06, 1)
        _NebulaColor("Nebula Color", Color) = (0.40, 0.18, 0.50, 1)
        _StarDensity("Star Density (grid cells per axis)", Range(50, 2000)) = 400
        _StarBrightness("Star Brightness", Range(0, 3)) = 1.2
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _HorizonColor;
                half4 _ZenithColor;
                half4 _NebulaColor;
                half _StarDensity;
                half _StarBrightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirOS  : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.viewDirOS = input.positionOS.xyz;
                return output;
            }

            // Cheap 3D hash, 0..1. Not a real noise function (no interpolation) -- deliberate,
            // stars are supposed to be pinpoints, not smooth blobs.
            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.viewDirOS);

                // y in [-1,1] (down..up) -> t in [0,1] (horizon..zenith).
                float t = saturate(dir.y * 0.5 + 0.5);
                half3 skyColor = lerp(_HorizonColor.rgb, _ZenithColor.rgb, t);

                // Faint nebula: a coarse hashed grid, thresholded so only a few cells glow,
                // fading out near the horizon so it reads as a band rather than everywhere.
                float nebulaHash = Hash13(floor(dir * 6.0));
                float nebula = smoothstep(0.75, 1.0, nebulaHash) * (1.0 - t) * 0.5;
                skyColor += _NebulaColor.rgb * nebula;

                // Procedural stars: one candidate point per grid cell, only the rare
                // high-hash cells actually light up, faded out near/below the horizon so
                // they only appear in open sky.
                float3 cell = floor(dir * _StarDensity);
                float starHash = Hash13(cell);
                float star = step(0.995, starHash);
                star *= saturate(dir.y * 3.0);
                skyColor += star * _StarBrightness;

                return half4(skyColor, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
