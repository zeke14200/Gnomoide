Shader "Custom/OutlineOnly"
{
    Properties
    {
        _OutlineColor ("Cor do Outline", Color) = (0, 1, 1, 1)
        _OutlineThickness ("Espessura", Range(0.0, 0.1)) = 0.015
    }
    SubShader
    {
        // 1. Mudamos a fila (Queue) para Overlay, garantindo que ele seja desenhado por último
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }

        Pass
        {
            Name "Outline"
            
            Cull Front
            // 2. Desliga a escrita de profundidade para não bugar outras sombras
            ZWrite Off      
            // 3. A MÁGICA: Ignora a profundidade. Fica sempre visível por cima de tudo!
            ZTest Always    

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