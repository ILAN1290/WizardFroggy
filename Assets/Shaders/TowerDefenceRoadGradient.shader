Shader "WizardFroggy/2D/Road Tile Gradient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Base Tint", Color) = (1,1,1,1)
        _CenterTint ("Tile Center Tint", Color) = (0.97,0.91,0.82,1)
        _EdgeTint ("Tile Edge Tint", Color) = (0.82,0.64,0.43,1)
        _GradientStrength ("Gradient Strength", Range(0,1)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _CenterTint;
            half4 _EdgeTint;
            half _GradientStrength;
        CBUFFER_END

        struct Attributes
        {
            float3 positionOS : POSITION;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            half4 color : COLOR;
            float2 uv : TEXCOORD0;
            float2 tileUV : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings RoadVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.positionHCS = TransformObjectToHClip(input.positionOS);
            output.uv = input.uv;
            output.color = input.color * _Color;

            // Tilemap cells are one world unit and their local vertex positions
            // are aligned to integer cell boundaries. frac() gives the same
            // edge tint on both sides of a shared border, so no dark grid seams appear.
            output.tileUV = frac(input.positionOS.xy);
            return output;
        }

        half4 RoadFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

            float2 fromCenter = abs(input.tileUV - 0.5) * 2.0;
            float radius = saturate(length(fromCenter) * 0.70710678);
            float edge = smoothstep(0.0, 1.0, radius);

            half3 radialTint = lerp(_CenterTint.rgb, _EdgeTint.rgb, edge);
            half3 tint = lerp(half3(1.0, 1.0, 1.0), radialTint, saturate(_GradientStrength));
            color.rgb *= tint;

            return color;
        }
        ENDHLSL

        Pass
        {
            Name "RoadTile2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex RoadVertex
            #pragma fragment RoadFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "RoadTileForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex RoadVertex
            #pragma fragment RoadFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
