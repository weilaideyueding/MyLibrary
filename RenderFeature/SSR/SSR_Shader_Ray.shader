Shader "Unlit/SSR_Shader_Ray"
{
    Properties
    {
        _BlitTexture ("Main Tex", 2D) = "white"{}
        _MainTex ("Main Tex", 2D) = "white"{}
        [IntRange]_StencilRef ("Stencil Ref", Range(0,255)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
 
        // 0 SSR
        Pass
        {
            Tags{"LightMode" = "UniversalForward"  "RenderPipeline" = "UniversalRenderPipeline"}
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature _SSR_RAY _SSR_DDA
            #pragma shader_feature _HIZ_BUFFER
 
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl "
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl "

            CBUFFER_START(UnityPerMaterial)
                // TEXTURE2D(_BlitTexture);
                SAMPLER(sampler_BlitTexture);
                TEXTURE2D(_SSR_ColorTexture);
                SAMPLER(sampler_SSR_ColorTexture);
                TEXTURE2D(_HiZBufferTexture);
                SAMPLER(sampler_HiZBufferTexture);
                TEXTURE2D(SSR_TransparentDepthTex);
                SAMPLER(sampler_SSR_TransparentDepthTex);
                TEXTURE2D(_SSR_TransparentNormalTex);
                SAMPLER(sampler_SSR_TransparentNormalTex);
                TEXTURE2D(_SSR_MaskTex);
                SAMPLER(sampler_SSR_MaskTex);

                float _MaxHizBufferMipLevel;

                int _Ray_StepCount;
                float _Ray_Thickness;
                float _Ray_StepSize;

                float _DDA_MaxDistance;
                float _DDA_StepSize;
                int _DDA_StepCount;
                float _DDA_Thickness;
            CBUFFER_END

            #define RAY_STEP_COUNT _Ray_StepCount
            #define RAY_THICKNESS _Ray_Thickness
            #define STEP_SIZE _Ray_StepSize

            #define MAXDISTANCE _DDA_MaxDistance
            #define STRIDE _DDA_StepSize
            #define STEP_COUNT _DDA_StepCount
            // 能反射和不可能的反射之间的界限  
            #define THICKNESS _DDA_Thickness
            
            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv   : TEXCOORD0;
                float4 interpolatedRay : TEXCOORD1;
                float3 vfDir : TEXCOORD2;
            };

            float4 GetFullScreenTriangleFarPosition(uint vertexID, float z = UNITY_RAW_FAR_CLIP_VALUE)
            {
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                return float4(uv * 2.0 - 1.0, z, 1.0);
            }
 
            v2f vert (Attributes v)
            {
                v2f o;

                // 新版本写法
                float4 pos = GetFullScreenTriangleVertexPosition(v.vertexID);
                float2 uv  = GetFullScreenTriangleTexCoord(v.vertexID);

                o.positionCS = pos;
                o.uv = uv;

                // 计算远裁剪面的三角形顶点
                float4 farPos = GetFullScreenTriangleFarPosition(v.vertexID);

                // 转换到世界空间
                float4 posWS = mul(unity_MatrixInvVP, farPos);
                float3 worldPos = posWS.xyz / posWS.w;

                // 求出相机到远裁剪面三个顶点的方向，长度保留
                o.vfDir  = worldPos.xyz - GetCameraPositionWS();
                return o;
            }

            // 交换
            void swap(inout float v0, inout float v1) {  
                float temp = v0;  
                v0 = v1;    
                v1 = temp;
            }  
            
            
            half4 frag (v2f i) : SV_Target
            {

                // float depth = SampleSceneDepth(i.uv);
                // 需要透明物体的深度图
                float depth = SAMPLE_TEXTURE2D(SSR_TransparentDepthTex, sampler_SSR_TransparentDepthTex, i.uv).r;
                // return depth;
                // float3 normal = normalize(SampleSceneNormals(i.uv));
                float3 normal = SAMPLE_TEXTURE2D(_SSR_TransparentNormalTex, sampler_SSR_TransparentNormalTex, i.uv);
                
                // return depth;
                
                // float3 worldPos = ComputeWorldSpacePosition(i.uv, depth, UNITY_MATRIX_I_VP);

                float linearDepth = Linear01Depth(depth, _ZBufferParams);   
                float3 worldPos = _WorldSpaceCameraPos + linearDepth * i.vfDir;
                float3 vDir = normalize(_WorldSpaceCameraPos - worldPos);
                float3 rDir = normalize(reflect(-vDir, normal));   // 注意方向

                half4 fianlCol = half4(0, 0, 0, 1);

#ifdef _SSR_RAY
                
                UNITY_LOOP
                for (int j = 0; j < RAY_STEP_COUNT; j++)
                {
                    float3 samplePos = worldPos + rDir * STEP_SIZE * j;
                
                    // 获取采样点的uv和深度
                    float4 postionCS = TransformWorldToHClip(float4(samplePos, 1.0f));
                    float4 sampleScreenPos = ComputeScreenPos(postionCS);
                    float2 uv = sampleScreenPos.xy / sampleScreenPos.w;
                    
                    // 获取该点的深度
                    float stepDepth = postionCS.w;
                
                    // 获取深度图深度
                    float stepUVdepth = SampleSceneDepth(uv);
                    stepUVdepth = LinearEyeDepth(stepUVdepth, _ZBufferParams);
                    
                    // 如果点的深度大于深度图的深度，并且还要在厚度范围内，说明该点在表面，进行采样
                    if (stepUVdepth < stepDepth && stepDepth < stepUVdepth + RAY_THICKNESS)
                    {
                        fianlCol = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv);
                    }
                }
                
#elif defined(_SSR_DDA)
                              
                half magnitude = MAXDISTANCE;
            
                rDir = TransformWorldToViewDir(rDir);   // 反射方向转换到视空间（找了一天问题），因为步进不是发生在世界空间，而是发生在观察空间
            
                // 获取起始点的观察空间
                float3 starPosVS = TransformWorldToView(worldPos);

                float end = starPosVS.z + rDir.z * magnitude; // 计算一下最远距离

                if (end > -_ProjectionParams.y)  // 如果结束点的z是比近裁面更近的话，那么限制一下最远距离
                {
                    magnitude = (-_ProjectionParams.y - starPosVS.z) / rDir.z;
                }

                float3 endPos = starPosVS + rDir * magnitude;  // 获取到最远的点

                // 齐次裁剪空间
                float4 starPosHC = TransformWViewToHClip(starPosVS);
                float4 endPosHC  = TransformWViewToHClip(endPos);

                // 齐次屏幕空间
                float4 starPosSS = ComputeScreenPos(starPosHC);
                float4 endPosSS  = ComputeScreenPos(endPosHC);

                // 计算
                float stark = 1.0 / starPosSS.w;
                float endk  = 1.0 / endPosSS.w;

                // 屏幕空间
                float2 starScreen = starPosSS.xy * stark * _ScreenParams.xy;    // 需要去乘上屏幕的宽高，因为后面的步进是每次需要步进1，而不是1/像素
                float2 endScreen = endPosSS.xy * endk * _ScreenParams.xy;

                // 经过齐次除法的观察空间
                float3 startQ = starPosVS * stark;
                float3 endQ = endPos * endk;
                
                // 获取到终点到起点的差距
                float2 delta = endScreen - starScreen;

                bool permute = false;   // 是否需要交换xy

                // 如果 y的差值大于 x的差值，那么意味着斜率大于1，那么就是y方向去变换1
                // 就是如果斜率大于1，那就转换xy，适应后面的变换
                if (abs(delta.x) < abs(delta.y)) 
                {
                    permute = true;
                
                    delta = delta.yx;
                    starScreen = starScreen.yx;
                    endScreen = endScreen.yx;
                }

                // 去计算出差值，同时也考虑到了方向性
                float stepDirection = sign(delta.x);   // 判断正负
                float invdx = stepDirection / delta.x; // 应该是为了同一符号，1/diff.x，应该是这个意思

                // 屏幕坐标，齐次视坐标，inverse-w的增量
                float2 dp = float2(stepDirection, invdx * delta.y); // 前一个走正负1，后面的就进行 diff.y / diff.x
                float3 dq = (endQ - startQ) * invdx;    // 想想，画一条线，如果斜率小于1的话，那么是不是意味着每一步增加的需要总共的去除x
                float dk = (endk - stark) * invdx;  // 大于1的话，那就除y，才能获取增量

                // 乘上步幅
                dp *= STRIDE;
                dq *= STRIDE;
                dk *= STRIDE;

                // 缓存
                float rayZMin = starPosVS.z;
                float rayZMax = starPosVS.z;
                float preZ = starPosVS.z;

                float2 p = starScreen;  // uv
                float3 q = startQ;  // 齐次视坐标
                float k = stark;

                end = endScreen.x * stepDirection;    // 步进方向的结束距离

    // 如果使用HIZ的话
    #ifdef _HIZ_BUFFER
                
                float mipLevel = 0.0;

                UNITY_LOOP
                for (int j = 0; j < STEP_COUNT && p.x * stepDirection <= end; j++)
                {
                    // 按照mipLevel的层级去控制步进大小
                    p += dp * exp2(mipLevel);
                    q += dq * exp2(mipLevel);
                    k += dk * exp2(mipLevel);

                    // 获得深度
                    rayZMin = preZ;
                    rayZMax = (dq.z * exp2(mipLevel) * 0.5 + q.z) / (dk * exp2(mipLevel) * 0.5 + k);
                    preZ = rayZMax;
                    if (rayZMin > rayZMax)
                        swap(rayZMin, rayZMax);

                    // 得到交点uv
                    half2 hitUV = permute ? p.yx : p;
                    hitUV /= _ScreenParams.xy;

                    // 超出屏幕的话
                    if (any(hitUV < 0.0) || any(hitUV > 1.0))
                    {
                        fianlCol = 0;
                        break;
                    }

                    // 采样当前层级的深度
                    float rawDepth = SAMPLE_TEXTURE2D_X_LOD(_HiZBufferTexture, sampler_HiZBufferTexture, hitUV, mipLevel);
                    float surfaceDepth = -LinearEyeDepth(rawDepth, _ZBufferParams);

                    bool behind = rayZMin + 0.1 <= surfaceDepth;

                    // 如果没有击中，那么就加一层级，继续采样
                    if (!behind) {
                        mipLevel = min(mipLevel + 1, _MaxHizBufferMipLevel);
                    }else
                    {
                        // 当前层级为0的时候，并且在厚度内，那么获取该点
                        if (mipLevel == 0) {
                            if (abs(surfaceDepth - rayZMax) < THICKNESS)
                            {
                                fianlCol = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, hitUV);
                                break;
                            }
                        }
                        else // 如果层级不是0的话，那就回退到上一步,并使用更为精细的层级去采样
                        {
                            p -= dp * exp2(mipLevel);
                            q -= dq * exp2(mipLevel);
                            k -= dk * exp2(mipLevel);
                            preZ = q.z / k;

                            mipLevel--;
                        }
                    }
                        
                }
    #else
                
                UNITY_LOOP
                // 如果在步进次数和超出最远距离的时候，结束步进
                for (int j = 0; j < STEP_COUNT && p.x * stepDirection <= end; j++)
                {
                    // 步进
                    p += dp;
                    q.z += dq.z;
                    k += dk;
                
                    // 得到步进前后两点的深度
                    rayZMin = preZ;
                    // 计算未来1/2的深度
                    rayZMax = (q.z  + dq.z * 0.5) / (k + dk * 0.5); // 通过乘w，转回到视空间，获取深度
                    
                    preZ = rayZMax; // 保存深度
                    if (rayZMin > rayZMax)  // 这个有点问题吧，这个时候的深度是负数
                    {
                        swap(rayZMin, rayZMax);
                    }
                
                    // 得到uv ,UV还有点问题
                    float2 hitUV = permute ? p.yx : p;  // 考虑了斜率大于1的情况
                    hitUV /= _ScreenParams.xy; // 因为要将范围从[宽，高]映射到[0,1]
                
                    if (any(hitUV < 0.0) || any(hitUV > 1.0))   // 如果超过屏幕的话，那就使用原先的uv采用，没有反射
                    {
                        fianlCol = 0;
                        break;
                    }
                
                    float surfaceDepth = -LinearEyeDepth(SampleSceneDepth(hitUV), _ZBufferParams);  // 深度图的值是正的，需要转换成负的
                
                    bool isBehind = rayZMin + 0.1 <= surfaceDepth;  // 判断前一个点的深度是否小于深度图，因为rayZMax是负值；
                    bool isIntersecting = isBehind && (rayZMax >= surfaceDepth - THICKNESS);    // 还需要保证步进不超过厚度
                
                    if (isIntersecting)
                    {
                        fianlCol = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, hitUV);
                    }
                    
                }

    #endif
                
                
#endif
                

                // 并不会去增加效率，因为是在计算完后在进行mask的，应该可以去限制相机的绘制
                half mask = SAMPLE_TEXTURE2D(_SSR_MaskTex, sampler_SSR_MaskTex, i.uv).r;
                
                fianlCol = saturate(fianlCol * mask);

                return fianlCol;
            }
            
            ENDHLSL 
        }

        // 1 blur
        Pass
        {
            Tags{"LightMode" = "UniversalForward"  "RenderPipeline" = "UniversalRenderPipeline"}
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
 
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            

            CBUFFER_START(UnityPerMaterial)
                SAMPLER(sampler_BlitTexture);
                float _PexelOffset;
            CBUFFER_END
            
 
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
 
            v2f vert (Attributes v)
            {
                v2f o;
                o.vertex = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv = GetFullScreenTriangleTexCoord(v.vertexID);
                return o;
            }

            // 拥有不错的模糊效果
            half4 KawaseBlur(Texture2D tex, SamplerState sampler_name, float2 uv, float2 texelSize, half pexelOffset)
            {
                half4 o = 0;
                o += SAMPLE_TEXTURE2D(tex, sampler_name, uv + float2(pexelOffset +0.5, pexelOffset +0.5) * texelSize); 
                o += SAMPLE_TEXTURE2D(tex, sampler_name, uv + float2(-pexelOffset -0.5, pexelOffset +0.5) * texelSize); 
                o += SAMPLE_TEXTURE2D(tex, sampler_name, uv + float2(-pexelOffset -0.5, -pexelOffset -0.5) * texelSize); 
                o += SAMPLE_TEXTURE2D(tex, sampler_name, uv + float2(pexelOffset +0.5, -pexelOffset -0.5) * texelSize); 
                return o * 0.25;
            }
 
            half4 frag (v2f i) : SV_Target
            {
                half2 texelSize = half2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);
                half4 col = KawaseBlur(_BlitTexture, sampler_BlitTexture, i.uv, texelSize, _PexelOffset);

                return col;
            }
            ENDHLSL
        }
        
        // 2 White mask
        Pass
        {
            Tags{"LightMode" = "UniversalForward"}
            
            ZTest on
                ZWrite on
                Cull back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
 
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct a2v {
                 float4 positionOS:POSITION;
            };
 
            struct v2f
            {
                float4 vertex:POSITION;
            };
 
            v2f vert (a2v v)
            {
                v2f o;
                float4 pos = TransformObjectToHClip(v.positionOS);
                o.vertex = pos;
                return o;
            }
 
            half4 frag (v2f i) : SV_Target
            {
                return 1;
            }
            ENDHLSL
        }
        
        // 3 Black mask
        Pass
        {
            Tags{"LightMode" = "UniversalForward"}

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
 
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct a2v {
                 float4 positionOS:POSITION;
            };
 
            struct v2f
            {
                float4 vertex:POSITION;
            };
 
            v2f vert (a2v v)
            {
                v2f o;
                float4 pos = TransformObjectToHClip(v.positionOS);
                o.vertex = pos;
                return o;
            }
 
            half4 frag (v2f i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
        
        // 4 Combine
        Pass
        {
            Tags{"LightMode" = "UniversalForward"}
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
 
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                SAMPLER(sampler_BlitTexture);
                TEXTURE2D(_SSR_ColorTexture);
                SAMPLER(sampler_SSR_ColorTexture);

                half _SSR_Intensity;
            CBUFFER_END

            
            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv   : TEXCOORD0;
            };

 
            v2f vert (Attributes v)
            {
                v2f o;

                // 新版本写法
                float4 pos = GetFullScreenTriangleVertexPosition(v.vertexID);
                float2 uv  = GetFullScreenTriangleTexCoord(v.vertexID);

                o.positionCS = pos;
                o.uv = uv;
                return o;
            }
            
            
            half4 frag (v2f i) : SV_Target
            {
                
                half4 ssr = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, i.uv);
                half4 color = SAMPLE_TEXTURE2D(_SSR_ColorTexture, sampler_SSR_ColorTexture, i.uv);
                
                return ssr * _SSR_Intensity + color;
            }
            
            ENDHLSL 
        }
        
        // 5 Hiz_RTHandle
        Pass{
            
            Tags{"LightMode" = "UniversalForward"}

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
 
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                SAMPLER(sampler_BlitTexture);
                float4 _HiZBufferFromMiplevel;
                float4 _HiZBufferToMiplevel;
                float4 _HiZBufferSourceSize;
            CBUFFER_END
 
            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv   : TEXCOORD0;
            };

 
            v2f vert (Attributes v)
            {
                v2f o;

                // 新版本写法
                float4 pos = GetFullScreenTriangleVertexPosition(v.vertexID);
                float2 uv  = GetFullScreenTriangleTexCoord(v.vertexID);

                o.positionCS = pos;
                o.uv = uv;
                return o;
            }
            
            half4 GetSource(half2 uv, float2 offset = 0.0, float mipLevel = 0.0) {
                offset *= _HiZBufferSourceSize.zw;
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, uv + offset, mipLevel);
            }
            
            half4 frag (v2f i) : SV_Target
            {
                half2 uv = i.uv;

                // 采样四边，为啥还要mip采样？
                half4 minDepth = half4(
                    GetSource(uv, float2(-1, -1), _HiZBufferFromMiplevel).r,
                    GetSource(uv, float2(-1, 1), _HiZBufferFromMiplevel).r,
                    GetSource(uv, float2(1, -1), _HiZBufferFromMiplevel).r,
                    GetSource(uv, float2(1, 1), _HiZBufferFromMiplevel).r);

                // 求出最大值
                return max(max(minDepth.r, minDepth.g), max(minDepth.b, minDepth.a));
            }
            

            ENDHLSL
        }
        
        // 6 Hiz_RenderTexture
        Pass{
            Tags{"LightMode" = "UniversalForward"}

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
 
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                TEXTURE2D(_MainTex);
                SAMPLER(sampler_MainTex);
                float4 _HiZBufferSourceSize;
            CBUFFER_END

            struct a2v
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
 
            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv   : TEXCOORD0;
            };

 
            v2f vert (a2v v)
            {
                v2f o;

                o.positionCS = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            half4 GetSource(half2 uv, float2 offset = 0.0) {
                offset *= _HiZBufferSourceSize.zw;
                return SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, uv + offset);
            }
            
            half4 frag (v2f i) : SV_Target
            {
                half2 uv = i.uv;

                // 采样四边，为啥还要mip采样？
                half4 minDepth = half4(
                    GetSource(uv, float2(-1, -1)).r,
                    GetSource(uv, float2(-1, 1)).r,
                    GetSource(uv, float2(1, -1)).r,
                    GetSource(uv, float2(1, 1)).r);

                // 求出最大值
                return max(max(minDepth.r, minDepth.g), max(minDepth.b, minDepth.a));
            }
            

            ENDHLSL
        }
        
        // 7 Normal绘制
        Pass{
            Tags{"LightMode" = "UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
 
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct a2v {
                float4 positionOS:POSITION;
                float3 normal:NORMAL;
            };
 
            struct v2f
            {
                float4 vertex:POSITION;
                float3 normal:NORMAL;
            };
 
            v2f vert (a2v v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.positionOS);
                o.normal = TransformObjectToWorldNormal(v.normal);
                return o;
            }
 
            half4 frag (v2f i) : SV_Target
            {
                // return 1;
                return float4(i.normal, 1);
            }
            ENDHLSL
        }

        // 8 Stencil Mask
        Pass{

            Stencil		    
            {
			  Ref 1
			  Comp Equal
		    }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // int _StencilRef;
            
            struct a2v {
                float4 positionOS:POSITION;
                float3 normal:NORMAL;
            };
 
            struct v2f
            {
                float4 vertex:POSITION;
                float3 normal:NORMAL;
            };
 
            v2f vert (a2v v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.positionOS);
                o.normal = TransformObjectToWorldNormal(v.normal);
                return o;
            }

            
            half frag (v2f i) : SV_Depth
            {
                return 1;
            }
            ENDHLSL
        }
        
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
