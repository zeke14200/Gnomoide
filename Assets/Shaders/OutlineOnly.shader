Shader "Custom/OutlineOnly"
{
    Properties
    {
        _OutlineColor ("Cor do Outline", Color) = (0, 1, 1, 1)
        _OutlineThickness ("Espessura", Range(0.0, 0.1)) = 0.015
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1" }

        Pass
        {
            Name "Outline"
            
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineThickness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                float3 pos = input.positionOS.xyz + input.normalOS * _OutlineThickness;
                output.positionCS = TransformObjectToHClip(pos);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}