Shader "Unlit/Bloom"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white"{}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
        
            float4 _MainTex_TexelSize;  // float4(1/w, 1/h, w, h)
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
 
            v2f UniversialVert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 KawaseBlur(Texture2D tex, SamplerState sampler_name, float2 uv, float2 texelSize, half pexelOffset)
            {
                half4 o = 0;
                o += SAMPLE_TEXTURE2D(tex, sampler_name, uv + float2(pexelOffset +0.5, pexelOffset +0.5) * texelSize); 
                o += SAMPLE_TEXTURE2D(tex, sampler_name, uv + float2(-pexelOffset -0.5, pexelOffset +0.5) * texelSize); 
                o += SAMPLE_TEXTURE2D(tex, sampler_name, uv + float2(-pexelOffset -0.5, -pexelOffset -0.5) * texelSize); 
                o += SAMPLE_TEXTURE2D(tex, sampler_name, uv + float2(pexelOffset +0.5, -pexelOffset -0.5) * texelSize); 
                return o * 0.25;
            }

                 // 高斯模糊，也未完成
            float GaussWeight2D(float x, float y, float sigma)
            {
                float E = 2.71828182846;
                float sigma_2 = pow(sigma, 2);

                float a = -(x*x + y*y) / (2.0 * sigma_2);
                return pow(E, a) / (2.0 * PI * sigma_2);
            }

            float3 GaussNxN(Texture2D tex, SamplerState sampler_x, float2 uv, int n, float2 stride, float sigma)
            {
                float3 color = float3(0,0,0);
                int r = n / 2;
                float weight = 0.0;
                            
                for(int i=-r; i<=r; i++)
                {
                    for(int j=-r; j<=r; j++)
                    {
                        // 获取权重值
                        float w = GaussWeight2D(i, j, sigma);
                        float2 coord = uv + float2(i, j) * stride;
                        color += SAMPLE_TEXTURE2D(tex, sampler_x, coord).rgb * w;
                        weight += w;
                    }
                }

                color /= weight;
                return color;
            }
        
        ENDHLSL
        
        // 0: 亮度阈值
        Pass
        {
            Tags{"RenderPipeline" = "UniversalRenderPipeline"}
            
            ZWrite Off 
            Name "LumeThershold"
            
            HLSLPROGRAM
            #pragma vertex UniversialVert
            #pragma fragment thresholdFrag

            half _luminanceThreshole;

            half4 thresholdFrag(v2f i) : SV_Target
            {
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                // 将RGB转换为亮度
                float lum = dot(float3(0.2126, 0.7152, 0.0722), col.rgb);
                
                if (lum > _luminanceThreshole)
                {
                    return col;
                }
                return float4(0,0,0,1);
            }
            
            ENDHLSL
        }
        
        // 1: 降采样
        Pass
        {
            HLSLPROGRAM
            #pragma vertex UniversialVert
            #pragma fragment BloomDownFrag
            
            half _downSampleBlurSize;
            half _downSampleBlurSigma;

            half _bloomDownOffset;

            half4 BloomDownFrag(v2f i) : SV_Target
            {
                float4 color = float4(0,0,0,1);
                float2 uv = i.uv;
                float2 stride = _MainTex_TexelSize.xy;   // 上一级 mip 纹理的 texel size

                // color.rgb = GaussNxN(_MainTex, sampler_MainTex,  uv, _downSampleBlurSize, stride, _downSampleBlurSigma);
                color.rgb = KawaseBlur(_MainTex, sampler_MainTex, uv, _MainTex_TexelSize, _bloomDownOffset);
                
                return color;
            }
            
            ENDHLSL
        }
        
        // 2: 升采样
        Pass
        {
            HLSLPROGRAM
            
            #pragma vertex UniversialVert
            #pragma fragment BloomUpFrag


            TEXTURE2D(_PreTex);
            SAMPLER(sampler_PreTex);

            half _bloomUpOffset;
            
            half _upSampleBlurSize;
            half _upSampleBlurSigma;

            half4 BloomUpFrag(v2f i) : SV_Target
            {
                float4 color = float4(0, 0, 0, 1);
                float2 uv = i.uv;
                
                float2 prev_stride = 0.5 * _MainTex_TexelSize.xy;
                float2 curr_stride = 1.0 * _MainTex_TexelSize.xy;
            
                // float3 pre_tex = GaussNxN(_PreTex, sampler_PreTex, uv, _upSampleBlurSize, prev_stride, _upSampleBlurSigma);
                // float3 curr_tex = GaussNxN(_MainTex, sampler_MainTex, uv, _upSampleBlurSize, curr_stride, _upSampleBlurSigma);

                float3 pre_tex = KawaseBlur(_MainTex, sampler_MainTex, uv, prev_stride, _bloomUpOffset);
                float3 curr_tex = KawaseBlur(_PreTex, sampler_PreTex, uv, curr_stride, _bloomUpOffset);

                
                color.rgb =  curr_tex + pre_tex;
            
                return color;
            }
            ENDHLSL
        }
        
        // 3: 合并输出
        Pass{
            
            HLSLPROGRAM
            #pragma vertex UniversialVert
            #pragma fragment BloomOutFrag

            CBUFFER_START(UnityPerMaterial)
                TEXTURE2D(_BloomTex);
                SAMPLER(sampler_BloomTex);
                half _bloomIntensity;
            CBUFFER_END

            float3 ACESToneMapping(float3 color, float adapted_lum)
            {
                const float A = 2.51f;
                const float B = 0.03f;
                const float C = 2.43f;
                const float D = 0.59f;
                const float E = 0.14f;

                color *= adapted_lum;
                return (color * (A * color + B)) / (color * (C * color + D) + E);
            }

            half4 BloomOutFrag(v2f i) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                float3 bloom = SAMPLE_TEXTURE2D(_BloomTex, sampler_BloomTex, i.uv) * _bloomIntensity;

                bloom = ACESToneMapping(bloom, 1);

                // 进行一次gamma编码，
                float g = 1.0 / 2.2;
                bloom = saturate(pow(bloom, float3(g,g,g)));

                color.rgb += bloom;
                
                return color;
            }
            
            
            ENDHLSL

        }
            
    }
}
