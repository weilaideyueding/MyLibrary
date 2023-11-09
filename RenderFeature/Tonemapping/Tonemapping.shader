Shader "Unlit/Tonemapping"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white"{}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
 
        Pass
        {
            Tags{"LightMode" = "UniversalForward"  "RenderPipeline" = "UniversalRenderPipeline"}
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature _TONEMAPPING_OFF _TONEMAPPING_ACES
 
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            half _lumeValue;

            CBUFFER_END
 
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
 
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
 
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float3 ACESToneMapping(float3 color)
            {
	            const float A = 2.51f;
	            const float B = 0.03f;
	            const float C = 2.43f;
	            const float D = 0.59f;
	            const float E = 0.14f;
                
	            return (color * (A * color + B)) / (color * (C * color + D) + E);
            }
 
            half4 frag (v2f i) : SV_Target
            {
                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                #ifdef _TONEMAPPING_ACES
                    color.rgb *= _lumeValue;
                    color.rgb = saturate(ACESToneMapping(color));
                #endif
                
                return color;
            }
            ENDHLSL
        }
    }
}
